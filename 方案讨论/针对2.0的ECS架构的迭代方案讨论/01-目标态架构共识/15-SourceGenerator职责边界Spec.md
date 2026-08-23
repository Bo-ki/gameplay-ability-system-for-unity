# SourceGenerator 职责边界 Spec

## 结论

SourceGenerator 负责把开放的策划 schema 压缩成封闭、可验证、Burst-compatible 的 Runtime 定义面；它不生成 Runtime lifecycle。生成越多不等于架构越数据化，隐藏 query/ECB/container owner 反而会破坏可审查性。

本文件只拥有“允许/禁止生成什么”的权限边界。canonical graph、typed support matrix、稳定 RuleId/provenance、Live/Grant/EmittedRef 与 CapacityProof 字段由 [25-配置语义编译契约与CapacityProof统一裁决Spec](25-配置语义编译契约与CapacityProof统一裁决Spec.md) 唯一维护；candidate、四 hash、原子 promotion/LKG 由 [08](08-Luban-SourceGenerator配置生成链路Spec.md) 唯一维护。

## 允许生成

| 产物 | 目的 |
|---|---|
| Stable IDs / enum-like constants | 消除字符串查找 |
| Blob schema/build metadata | Catalog/Layout materialization |
| code→index/range lookup | hot path O(1) 预解析访问 |
| pure requirement/magnitude/target evaluator | 封闭业务计算输入 |
| CaptureProjectionContract/accessor | 限制可读 view 与绑定时机 |
| CostMutationContract / CooldownGateContract | 编译 owner-local Commit invariant，不投递普通 self GE |
| StackTemporalContract / SpawnInitializationProgram | 冻结完整 temporal/bootstrap 闭世界语义 |
| DirectEffectProgram | 闭合、静态有界的同 tick Definition DAG |
| dependency graph | ongoing/tag/live cycle validation |
| FrozenProjectionPayload / coalesce metadata | Cross-ASC Live 的 revision/value 原子投影与 bytes/fanout 上界 |
| LayoutProof / CapacityProof / consumer map | 固定布局与 admission 实际消费的生成期硬上界 |
| Editor Binding/diagnostics | 策划编辑、引用、错误定位 |
| schema/content hash | Session/install/replay/determinism |
| candidate artifact metadata | canonical path、kind、owner、byte hash；供 manifest/promotion gate 使用 |
| Scale report | 只展示 proof 与观测 high-water，不替代 CapacityProof |

## 禁止生成

- `ISystem`、`SystemBase`、`OnUpdate`、system registration/order。
- `EntityQuery`、type handle/lookup refresh owner。
- ECB owner、`EntityManager` create/destroy/add/remove/write。
- NativeContainer allocation、lifetime、rewind、跨 System carrier。
- Ability/Effect/Tag/Attribute/Cue lifecycle 或 Boundary Drain。
- managed Runtime registry、reflection、JSON/Luban row hot lookup。
- Definition 级 Entity/slot、dense/sparse、旧/新 Runtime selector。
- PredictionKey、prediction window、ack/rollback/undo schema。

## 闭世界 Capture API

选择性 Snapshot 只有在 Calculation 不能绕过声明读取任意 Aggregator 视图时才安全。生成器必须为每个 calculation 产出强类型 accessor，只暴露声明的 Base/Final/Bonus/Channel/Contribution/ModList 与 filter/ignore 输入。

以下必须生成失败：运行时动态 CaptureDefinition、未声明视图、任意 IgnoreSet、反射/通用完整 snapshot accessor、跨 ASC Live 未实现、强制不安全 ScalarSnapshot。缺失输入不得静默采用 `FallbackMagnitude`；missing/source-gone policy 必须是 contract 字段。

跨 ASC Live 只有在生成 `FrozenProjectionPayload`、`MaxProjectionBytes`、revision/value 原子配对、canonical consumer-field coalesce key、fanout/update 上界和 source-gone/cycle policy 时才允许。只生成 binding identity/LastSeenRevision，或收到 revision 后再读取 source 可变值，均视为未实现并 publish-fail。

## DirectEffectProgram

生成器验证并输出：静态 Effect 边、拓扑序、node/edge ordinal、最大节点/输出、target 限制、禁止 post-apply fact read 的证明。只验证“无环”不够；无法静态定界展开量或存在动态回边必须失败。

生成器还必须输出 target mutation program 的 canonical op、read/write set、sparse overlay layout、FaultKey schema 与 publish delta bound，供手写 Runtime owner 实现 `TargetPrepare -> SessionFaultReduce -> TargetPublish`。生成器不得输出 durable target writer、fault reducer、ECB playback 或 System lifecycle。

## Requirement 与依赖图

Application、Ongoing、Removal、Immunity 四个 phase 使用不同 ranges，不合并成一个 mask。根据 granted tags、ongoing/removal requirements、inhibition 与 Live Capture 生成有符号依赖图；静态负向环/SCC 保守拒绝。运行时动态组合仍由 Stabilization safety 检测。

Grant dependency 必须继续覆盖 effect remove/inhibit -> granted ability detach/remove -> child Activation End -> OwnedContribution/Continuation/Subscription cleanup。`EmittedApplicationRef` 必须生成 `AuditOnly/CleanupRight` 分类、retention watermark、每 Activation/ASC 上界；普通 audit ref 不拥有 Effect，只有具精确 ledger 的 CleanupRight 可产生 remove work。

## Catalog 构建

SourceGenerator 可以生成 builder glue/metadata，但 Blob 的实际创建、安装、引用计数与 dispose 由 Baker/Bootstrap/Catalog lifetime owner 负责。Runtime hot path 不调用 BlobBuilder。

## Runtime 调用形态

Kernel 准备冻结输入并调用静态 pure function；生成函数返回标量、判定/reason、有界 record 或 index/range，不写 ASC、不发 fact、不分配长期内存。

```text
Kernel-owned input snapshot
  -> Generated lookup/evaluator
  -> pure result
  -> Kernel-owned target transaction
```

Entity 即使作为 Core 内瞬时空间查询结果，也不应进入通用 generated Definition input；先由 Kernel 解析成 stable identity/phase snapshot。

## 生成报告

每次生成至少输出：

- 四元 install identity 与每个 candidate artifact 的 canonical path/kind/owner/byte hash；
- Definition 数、各 range 大小、typed support result/RuleId、最大 DirectEffectProgram/target 展开量；
- Scalar/AggregatorSnapshot/Live contracts、projection bytes/fanout/coalesce 上界与拒绝原因；
- dependency SCC/cycle、Grant-child cleanup、EmittedRef retention/rights 结果；
- LayoutProof 与 `ASC × AttributeCount/TagCount/slot size`、overlay/publish/projection/intent `CapacityProof`；
- CapacityProof consumer map 与 runtime-visible artifact forbidden API scan。

报告是 validation 输入，不是实现完成证明。Profiler/Journaling、语义测试和 Runtime 静态扫描仍需独立执行。

## 验收

1. Runtime-visible generated artifact 零 lifecycle/query/ECB/container/managed hot lookup。
2. 相同 normalized rows 生成相同 content hash 与 binary-relevant order。
3. phase、capture、program、dependency 和规模校验能定位到业务 Definition/字段。
4. generated pure functions 有固定向量测试并可在 Burst 路径调用。
5. 删除 generator 输出不会暴露另一套 hand-written fallback runtime。
6. Cost/Cooldown 对 cue/application/ongoing/removal/grant/live/execution/dynamic-child 的每一字段都有唯一 allow/deny、RuleId 与 provenance；未知字段默认 deny。
7. Cross-ASC Live 负例证明缺 payload/revision/value/bytes/fanout/coalesce 任一项即失败，不降级为 per-phase sampling。
8. Grant removal、child End、OwnedContribution/Continuation/Subscription cleanup 与 EmittedRef retention 均有生成期硬上界。
9. CapacityProof 逐字段映射到 `WholeTickInfraAdmission` consumer；只输出报告但 Runtime 未消费时验收失败。
10. generated target program 只描述 overlay/op/fault/publish proof，扫描确认没有 Target lifecycle、durable writer 或 ECB owner。
