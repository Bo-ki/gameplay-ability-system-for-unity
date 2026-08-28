# 配置语义编译契约与 CapacityProof 统一裁决 Spec

## 结论

Luban schema / rows 是长期 authoring 事实源；配置生成链必须先在内存中构建唯一、不可变、可追溯的 `CanonicalNormalizedSemanticGraph`，再由同一张图生成 typed contract、layout、dependency graph、`CapacityProof`、Runtime artifact 与 Editor metadata。SourceGenerator 不得通过 sidecar、已编译 row factory、默认值补丁或生成后 override/append 改写 gameplay 语义。

本 Spec 是以下主题的唯一正文 owner：

1. canonical in-memory semantic graph 与逐字段 provenance；
2. typed contract 的逐字段 allow/deny matrix 与稳定 `RuleId`；
3. Cross-ASC Live projection、Grant cleanup 与 `EmittedApplicationRef` 权利模型；
4. `AttributeLayout` / `TagCatalog` 所需 layout proof；
5. 可被 `WholeTickInfraAdmission` 实际消费的 `CapacityProof`；
6. 配置生成的 target program 如何约束 `TargetPrepare -> SessionFaultReduce -> TargetPublish`。

它不重复维护配置文件的候选目录、四 hash 编码、原子 promotion 或 LKG；这些由 [08-Luban-SourceGenerator配置生成链路Spec](08-Luban-SourceGenerator配置生成链路Spec.md) 唯一拥有。Artifact 分类与权限分别由 [14-DefinitionCodeGen目标链路Spec](14-DefinitionCodeGen目标链路Spec.md) 和 [15-SourceGenerator职责边界Spec](15-SourceGenerator职责边界Spec.md) 拥有。

## 1. Owner 边界

| 主题 | 唯一 owner | 本文件与相邻文件的关系 |
|---|---|---|
| Luban 输入、candidate、hash、CI gate、promotion、LKG | `08` | 本文件只声明进入 candidate 的 canonical graph/proof 必须满足什么 |
| semantic graph、typed contract、RuleId、CapacityProof | 本文件 | 其他文件只引用 contract/proof id，不复制字段矩阵 |
| generated artifact kind、owner、byte hash 权限 | `14` | 本文件声明必需 artifact，不定义 assembly/file 分类第二正文 |
| SourceGenerator allowed/forbidden API | `15` | 本文件声明语义产物，`15` 判定生成权限 |
| Runtime Target transaction 算法 | `03` / Runtime Kernel owner | 本文件只生成 program、proof、canonical key 与 no-fail 前置条件 |
| Business Package / Publish UI | `19` / `20` | 只消费 RuleId、provenance、candidate 与 promotion evidence |
| AutoChess schema / acceptance sample | `11` / `21` | 只实例化本文件的统一契约 |

## 2. CanonicalNormalizedSemanticGraph

### 2.1 单次生成规则

```text
Luban schema + rows + toolchain identity
  -> parse typed source model
  -> normalize / validate / attach provenance
  -> CanonicalNormalizedSemanticGraph
  -> contracts + dependency/layout/capacity proofs
  -> candidate artifacts
```

硬规则：

1. graph 必须在一次生成调用内直接从 Luban schema/rows 构建；禁止先写 `*.gen.cs`，再扫描当前 AppDomain 中可能尚未重新编译的 row type/factory。
2. generated normalized row C# 可以作为 Editor/CI 派生证据，但不得反向成为 graph、hash 或 Runtime Catalog 的输入权威。
3. graph 节点按 `DomainOrdinal + StableDefinitionId` 排序；字段按版本化 `FieldOrdinal` 排序；range、edge、program node 使用版本化 canonical ordinal。
4. canonical 编码不得包含 `AssemblyQualifiedName`、绝对路径、当前 culture、reflection 枚举顺序、worker/chunk 顺序或 wall-clock。
5. 同一输入在一次运行、连续两次运行、不同机器路径和不同 culture 下必须得到相同 graph bytes、contract bytes 与 artifact bytes。

### 2.2 Provenance

每个 canonical 字段和每条 validation failure 至少携带：

| 字段 | 含义 |
|---|---|
| `WorkbookId / TableId` | 稳定 authoring 来源，不使用绝对路径作为身份 |
| `RowStableId / FieldPath` | 唯一行与字段位置 |
| `RawValue / NormalizedValue` | 原始值和 canonical 值 |
| `RuleId / RuleVersion` | 触发的稳定规则与版本 |
| `GeneratorVersion` | 生成器工具链身份 |
| `RelatedDefinitionIds` | 冲突、引用、dependency edge 涉及的稳定定义 |

一个 gameplay 字段只能有一个 authoring owner。发现 sidecar、override、append、dummy modifier、隐藏默认值或两个 row projection 同时拥有同一字段时，必须以 `CFG1001 SemanticOwnerConflict` 失败，禁止 last-writer-wins。

## 3. Typed Contract Support Matrix

### 3.1 必需生成物

| Contract / Program | 必须表达 | 不得压平为 |
|---|---|---|
| `CostMutationContract` | owner-local Attribute mutation、precondition、ValueView、canonical mutation order | 普通 self GE、独立 cost store |
| `CooldownGateContract` | GateKey、duration/due、reject/expiry policy、可选 owned Tag contribution | ActiveEffect、Activation-owned cooldown、普通 self GE |
| `CaptureProjectionContract` | Source/Target、Snapshot/Live、phase、ValueView、missing/source-gone policy、payload bound | `FallbackMagnitude`、phase 采样别名、完整 Aggregator 任意读取 |
| `StackTemporalContract` | key/payload、refresh/reset、execute-on-apply、limit/overflow、expiry/final-period、inhibit/cue | `StackType + StackLimit` 两字段 |
| `DirectEffectProgram` | 静态 DAG、node/edge ordinal、target bound、输出上界、禁止 post-apply fact read 证明 | 动态 child、运行时递归、隐藏 lifecycle |
| `SpawnInitializationProgram` | Attribute/Tag init、default grant、initial self effect、全量准入与 canonical publish plan | runner 常量、serialized defaults、逐项可见半初始化 |

### 3.2 Cost / Cooldown 逐字段裁决

| GE-like authoring 字段 | Cost | Cooldown | 失败规则 |
|---|---|---|---|
| owner-local Attribute mutation | allow；必须有 AttributeId、op、ValueView、bound | deny | `CFG1101` |
| gate key / duration / due / expiry | deny | allow；v1 固定 `RejectWhileActive + ExpireOnly` | `CFG1101` |
| Activation/Commit precondition | allow；只读 canonical owner shadow | allow；只读 canonical owner shadow | 缺 ValueView/phase 为 `CFG1102` |
| Application requirement | 仅当能等价编译为 owner Commit precondition；否则 deny | 同左 | `CFG1101` |
| Ongoing / Removal / Immunity requirement | deny | deny | `CFG1101` |
| Apply/While/Executed/Remove Cue | deny；需另 author 普通 GE/Boundary binding | deny；需另 author 普通 GE/Boundary binding | `CFG1101` |
| granted Tag | deny | 仅 allow gate-owned、随 gate expiry 精确撤销的 Tag contribution | `CFG1103` |
| granted Ability | deny | deny | `CFG1101` |
| duration GE / period / stack / overflow / dispel | deny | deny；gate 字段不是 GE duration/stack | `CFG1101` |
| Execution calculation | deny，除非已归一为有界 owner-local pure evaluator且矩阵显式批准 | deny | `CFG1104` |
| Snapshot capture | 只允许 owner-local、明确 ValueView 和 bytes bound | 只允许 gate seed 所需 owner-local projection | `CFG1201` |
| Live capture | deny | deny | `CFG1101` |
| dynamic child / reaction / post-apply read | deny | deny | `CFG1301` |

Editor Binding 必须逐字段显示 `Allowed / Denied / RequiredProjection / RuleId`；CI 与 SourceGenerator 使用同一矩阵，禁止 Editor 自己维护另一套支持列表。

### 3.3 其他 program 关闭条件

1. `DirectEffectProgram` 只有在 target 数、node/edge、每 node 输出与同 Tick 总 work 全部静态有界时才允许。
2. `SpawnInitializationProgram` 的 initial effect 只能 self-target、生成期闭合、静态有界；跨 ASC/空间目标、Live capture、动态 reaction/child 必须 publish-fail。
3. 异质 stack payload/expiry 或 `RemoveOnActivationEnd` shared stack 必须生成逐 application ledger proof；只有同质 `payload × StackCount` 可使用 count-only slot。
4. 未覆盖的字段组合默认 deny，不得因 schema 新增字段而隐式放行。

## 4. Capture 与 Cross-ASC Live

### 4.1 FrozenProjectionPayload

跨 ASC Live 只能运输冻结投影，不运输 source accumulator 指针或可变引用。生成物至少包含：

```text
FrozenProjectionPayload
  ProjectionContractId
  SourceAscStableId
  SourceFieldId
  SourceRevision
  ValueKind
  ScalarValue | AggregatorSnapshotRange
  PayloadByteCount
  CanonicalEdgeOrdinal
```

`SourceRevision` 与 payload value/snapshot 必须在 source writer 的同一线性化点原子冻结。只收到 revision、随后再读取可变 source value，或只收到 value、不携 revision，均为非法实现。

### 4.2 Projection 上界与 coalesce

每个 `CaptureProjectionContract` 必须生成：

- `MaxProjectionBytes`；
- `MaxAggregatorSnapshotContributors`；
- `MaxLiveDirtyFanout`；
- `MaxUpdatesPerConsumerFieldBeforeCoalesce`；
- canonical coalesce key：`(ConsumerAscStableId, ActiveEffectHandle, ConsumerNodeId, ConsumerFieldId)`；
- source gone/cycle/propagation budget policy。

同 Tick 多 revision 只能按上述 key 保留 canonical 最大 revision 对应的完整 payload；结果至多产生一个 consumer-field update work。revision 相同但 payload 不同是 fatal consistency fault，不得按到达顺序选择。

## 5. Grant、EmittedApplicationRef 与 DependencyProof

### 5.1 EmittedApplicationRef 权利

生成 cleanup policy 时必须显式分型：

| 权利 | 语义 | Activation End 行为 |
|---|---|---|
| `AuditOnly` | 只记录已发射 ApplicationId/target/provenance，不拥有 Effect | 不生成 remove work；在 audit watermark 后回收 |
| `CleanupRight` | Definition 明确声明 `RemoveOnActivationEnd`，且存在精确 application/contributor ledger | 生成有界、幂等的精确 remove work |

每个 Definition/Profile 必须生成 `AuditRetentionTicks`、`MaxAuditRefsPerActivation`、`MaxAuditRefsPerAsc` 和 watermark 回收规则。缺少任一硬上界不得发布；不能用“通常 activation 很短”替代证明。

### 5.2 Grant cleanup 有符号依赖图

dependency graph 必须包含：

```text
effect ongoing/inhibit/remove
  -> granted ability detach/remove policy
  -> child Activation End
  -> OwnedContribution revoke
  -> Continuation cancel/resume disposition
  -> Subscription unregister
  -> optional CleanupRight application removal
```

每条 edge 标记 trigger、positive/negative dependency、work class、canonical ordinal 与最大展开量。必须生成：

- `MaxGrantedAbilitiesPerEffect`；
- `MaxActiveChildrenPerGrant`；
- `MaxGrantRemovalWork`；
- `MaxContinuationCleanupWork`；
- `MaxSubscriptionCleanupWork`；
- `MaxCleanupRightRemovalWork`。

静态负向环/SCC、动态回边或任一 cleanup work 无上界时以 `CFG1301 DependencyOrCleanupUnbounded` 失败。

## 6. LayoutProof 与 CapacityProof

### 6.1 LayoutProof

`LayoutProof` 至少固定：

1. stable AttributeId -> dense `AttributeLayoutIndex`；
2. stable TagId -> dense `TagCatalogIndex`、ancestor range 与 `TagQueryProgram`；
3. `AttributeValueSlot[]` / `TagCountSlot[]` 的 count、slot bytes、alignment 与 revision owner；
4. 可选 `TagPresenceWord[]` 的 word count 与 derived-cache rebuild rule；
5. Blob range 的 Start/Count、排序规则与越界拒绝；
6. `LayoutHash` 及可报告的 `AttributeLayoutHash` / `TagCatalogHash` 子哈希。

未知 Attribute/Tag、超出 layout、重复 stable id 或 suffix alias 必须 publish-fail；禁止截断、no-op 或按当前 machine word 容量静默丢弃。

### 6.2 CapacityProof 字段

每个 Definition、每个 target application 和每个 ScaleProfile 必须能组合出：

| 维度 | 必需上界 |
|---|---|
| expansion | targets、program nodes/edges、applications、dynamic-next-tick work |
| touched authority | Attribute、Tag、ActiveEffect、Grant、Activation、Continuation、Subscription slots |
| target transaction | sparse overlay entries/bytes、RYW index bytes、publish delta entries/bytes |
| stabilization | transitions、rounds、work units、signed dependency edges、state hashes |
| projection/capture | payload bytes、snapshot contributors、live dirty fanout、coalesced updates |
| cleanup/audit | grant-child cleanup、CleanupRight remove、EmittedRef retention/high-water |
| observation/structure | facts、Cue intents、Boundary records、ECB intents |

`CapacityProof` 必须携 `ProofSchemaVersion`、`DefinitionId`、`ContractMatrixHash`、`LayoutHash`、各上界、推导表达式和 provenance。`capacity hint`、运行后 high-water 或平均值只能作为调优证据，不能替代生成期证明。

### 6.3 Consumer map

| Proof 字段 | 第一消费 owner | 消费时机 | 失败语义 |
|---|---|---|---|
| program/target expansion | OwnerPlanBuild / TargetResolve | 展开前 | business definition 已在发布期拒绝；运行时只校验输入规模 |
| projection/live fanout/bytes | SourceSpecProjection / live propagation owner | admission 前 | `InfraAdmissionFault`，整 Tick gameplay authority 零写 |
| target overlay/publish bytes | `WholeTickInfraAdmission` | TargetPrepare 前 | 同上 |
| slot/cleanup/audit work | `WholeTickInfraAdmission` + ASC slab owner | Owner/Target publish 前 | 同上；admitted path no-fail |
| fact/Cue/Boundary/ECB intents | `WholeTickInfraAdmission` | 任何 gameplay mutation 前 | 同上，无 silent drop |
| observed high-water | V7 scale/Profiler gate | 运行后 | 不能反向放宽 proof；超阈值阻断 release |

## 7. Target Program 与三阶段 durable publish

SourceGenerator 只能生成 target mutation program、read/write set、canonical ordinal、overlay layout、fault schema 与 `CapacityProof`；它不生成 Runtime lifecycle/System/ECB owner。

### 7.1 TargetPrepare

1. 每个 target 只有一个逻辑 writer。
2. writer 只写 tick-local sparse shadow/overlay，覆盖 Attribute/Tag/Effect/Grant/Activation cleanup 以及 fact/Cue/ECB intent。
3. 不写任何 target durable Buffer/component。
4. 同 target application 在 shadow 中按 generated canonical key执行，并保持 read-your-writes。

### 7.2 SessionFaultReduce

所有 target prepare 完成后，以 Session 为粒度归并 fatal fault；唯一首因键固定为：

`(TargetAscStableId, ApplicationId, StabilizationRound, StateHash)`。

任一 fatal fault 均丢弃本 Tick 全部 target shadow 与 intent。已经由 `AscOwnerCommandWave` 成功提交的 source Cost/Gate/Committed state 保留，不回滚，也不撤回已分配的正式 application identity/audit ref。

### 7.3 TargetPublish

仅当 `SessionFaultReduce=Success` 时，按 target 并行执行已 admission 的 no-fail sparse delta，一次写入 target durable authority；完成后才开放 StableFact、Boundary 与 ECB 后续 lane。

验收不变量：

- fault Tick 无 target durable 写、Fact、Cue 或 ECB intent 可见；
- 不同 worker、chunk、batch 切分得到相同 `FaultId`、`CommittedPrefixHash` 与 raw durable state；
- admission 后不得再出现容量分支；
- `TargetPublish` 是 gameplay Tick 内 durable publish，不等同于 `08` 的离线 `ConfigArtifactPromotion`。

## 8. Candidate eligibility 与四 hash 输入

本文件只定义 candidate eligibility；hash 编码和 promotion 线性化点见 `08`。

1. `SchemaHash` 输入：canonical schema、FieldOrdinal、typed contract ABI、RuleId domain。
2. `ContentHash` 输入：验证后的 semantic graph gameplay fields、program/dependency ordinals；不含绝对 provenance path。
3. `LayoutHash` 输入：Blob/range、AttributeLayout、TagCatalog、slot/payload layout 与 `CapacityProof` schema。
4. `ArtifactManifestHash` 输入：按 canonical artifact path/kind/owner 排序的逐文件 byte hash。
5. `SourceInputHash` 只做复现/provenance，不能代替上述四元 install identity。

candidate 缺少任一 contract、proof、provenance、byte hash 或 consumer map 时，不具备 promotion 资格。

## 9. 固定 Publish-Fail RuleId

| RuleId | 条件 | 必需 evidence |
|---|---|---|
| `CFG1001 SemanticOwnerConflict` | sidecar/override/append/dummy/default patch 或同字段双 owner | 两端 provenance、DefinitionId、FieldPath |
| `CFG1002 InvalidDomainOrReference` | 非法 enum/range、未知/重复引用、suffix alias | raw/normalized、目标 domain |
| `CFG1101 UnsupportedContractField` | typed support matrix 明确 deny | ContractKind、FieldPath、允许替代建模 |
| `CFG1102 MissingValueViewOrPhase` | capture/requirement 缺 View/phase/missing policy | consumer field、projection provenance |
| `CFG1103 InvalidOwnedContribution` | gate/grant contribution 无精确 owner/revoke policy | owner/cleanup policy |
| `CFG1104 UnsupportedExecutionProjection` | execution 无法归一为有界 pure evaluator | evaluator id、input/output set |
| `CFG1201 ProjectionUnbounded` | Live/snapshot 无 payload、fanout、coalesce 上界 | projection contract 与缺失 bound |
| `CFG1301 DependencyOrCleanupUnbounded` | SCC、动态回边、grant/child/cleanup 无上界 | cycle path、edge provenance |
| `CFG1401 CapacityProofMissing` | touched slots/overlay/work/intent 任一无证明 | Definition/Profile 与缺失维度 |
| `CFG1501 NonCanonicalIdentity` | 单遍/重复/跨环境 bytes 或 hash 不一致 | 两组 input/hash/artifact diff |

所有诊断码必须稳定、机器可读，并由 Generated Editor Binding、CI 和命令行报告共享；本地化文本不是身份。

## 10. 验收

1. 一次生成即可得到与第二次相同的 graph、四 hash 和 artifact bytes；不得依赖 Unity 再编译一次。
2. 删除/修改任意 Luban 字段时，所有 affected contract/proof/artifact 在同一 candidate 中同步变化，不产生混代文件。
3. 9203 非法 enum 与 sidecar modifier/override 负例以 `CFG1001/CFG1002` 阻断，且 active generation 不变。
4. Cost/Cooldown 的 cue/application/ongoing/removal/grant/live/execution/dynamic-child 字段均有固定 allow/deny 与 RuleId 测试。
5. Source/Target × Snapshot/Live 组合只有在 projection payload、revision/value 原子配对和 bytes/fanout/coalesce 证明齐全时通过。
6. Grant removal、child End、OwnedContribution/Continuation/Subscription cleanup 与 EmittedRef retention 均有生成上界和固定向量。
7. 257 个以上 Tag 的测试要么由 layout/profile 合法承载，要么显式 publish-fail；不得截断或 no-op。
8. `CapacityProof` 字段逐项被 admission consumer 读取；仅生成 report 未消费视为失败。
9. Target fault 注入证明 durable target state、Fact、Cue、ECB intent 零写，且物理切分不影响 fault identity。
10. Runtime 不含 managed row、sidecar、ScriptableObject 或旧 Catalog fallback；Catalog identity 不匹配直接启动失败。

## 11. 禁止方向

1. 把 `normalized rows`、generated C# factory 或 reflection snapshot 当成 semantic graph 权威。
2. 用 `InputHash`、整数 `SchemaVersion` 或 revision 单独判定 Catalog 兼容。
3. 用 `buffer capacity hint`、Scale report 或观测 high-water 冒充 `CapacityProof`。
4. 用 `TagMaskComponent`、单 word/四 word mask 或 per-attribute component 作为 Runtime authority。
5. 用 Runtime fallback、旧 schema reader、sidecar merge 或重新读取 authoring 数据处理 candidate/install 失败。
6. 在 SourceGenerator 中生成 Target lifecycle、durable writer、fault reducer、ECB playback 或 NativeContainer owner。
