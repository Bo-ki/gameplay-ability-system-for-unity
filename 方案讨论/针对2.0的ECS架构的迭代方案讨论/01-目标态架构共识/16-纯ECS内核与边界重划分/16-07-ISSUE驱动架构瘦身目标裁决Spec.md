# 16-07：ISSUE 驱动架构瘦身目标裁决

> Owner：`01-目标态架构共识/16-纯ECS内核与边界重划分` | 状态：目标态 Spec 子页 | 类型：目标裁决，不记录当前进度

本文件定义下一轮 GAS 架构瘦身的目标态裁决。当前代码命中、统计数字、validation 输出和具体任务状态不写在本文件；现实事实见 `../../00-当前架构事实/架构重划分审查事实/11-ISSUE驱动Runtime架构复审事实.md`，执行计划见 `../../02-主线任务树/2026-06-08-ISSUE驱动新一轮GAS架构瘦身计划.md`。

## 目标裁决

目标态 GAS 不能再以“用了 ECS API”作为架构完成证明。目标态必须满足：

1. Runtime Core 是纯 ECS Core：Gameplay 权威只在 `SystemGroup -> ISystem -> Job -> owner-local data / NativeContainer / Blob / ECB phase` 数据流内。
2. OOP Shell 只做 Application Shell / Runtime Boundary：它只能提交 intent、持有 opaque handle、读取 immutable snapshot、导出 evidence。
3. Thin Adapter 不是万能 wrapper：一个 Adapter 可以组合多个 capability 对象，但每个 public method 只能属于一个 capability，不能共享 ECS handle 解析权。
4. Debugger 是 evidence owner：它只消费 counters、facts、Profiler / Journaling state 和 derived export source，不反向驱动 simulation。
5. SourceGenerator 是 pure glue owner：它只能生成 immutable definition、static lookup、pure evaluator、catalog metadata、validation graph、Baker / Bootstrap glue；不生成 gameplay lifecycle system、query owner、ECB owner 或 NativeContainer owner。
6. AutoChess 是业务验收 Shell：它只证明真实业务链能通过 Boundary 使用 GAS，不是 Runtime Core 的一部分。

## Module Depth 判定门

每个目标态 Module 的 Interface 必须隐藏以下 Implementation 复杂度：

| 隐藏项 | 目标态要求 |
|---|---|
| query | 调用方不知道 `EntityQuery` / query filter / enabled mask；query owner 属于对应 System |
| lookup | 高频跨 owner lookup 不暴露给 Shell / Adapter；需要时先转 owner-local、target-grouped 或 snapshot record |
| allocator | `NativeArray` / `NativeList` / `NativeStream` allocator、dispose、rewind 由 lane owner 声明 |
| dependency | job dependency、manual drain、measurement fence 归 RunnerSync / lane owner，不作为业务 API 副作用 |
| carrier | command/spec/delta/fact 按 data-kind 选 carrier；singleton buffer 只能 proof-only |
| capacity | DynamicBuffer internal capacity、externalized ratio、NativeStream segment budget、spill/drop/reselect trigger 必须可观测 |
| merge | 影响 battle hash 的 fan-in 必须有 deterministic sort key 和 merge evidence |
| structural playback | 结构变化只能进入明确 StructuralCommit / ECB phase，并能用 Profiler / Journaling 归因 |
| diagnostics | Debugger counter、derived export、text/log、Profiler disabled reason 与 Core timing 分离 |

删除测试：

1. 删除某个 Module 后，如果复杂度只散回多个 Shell / helper / generated artifact / Debugger / Demo adapter，该 Module 仍是浅 Module，不能交还完成。
2. 删除某个 Module 后，如果调用方只失去一个小 Interface，但 Implementation complexity 仍留在单一 owner 内，才算目标态深 Module。

## 目标态 Capability 划分

| Capability | Public seam | Internal owner | 禁止泄露 |
|---|---|---|---|
| `RuntimeSession` | session id、install / dispose result、bootstrap validation | World、SystemGroup、registered singleton、bootstrap state | public `World` / `EntityManager` / singleton |
| `CommandPort` | request id、validation reason、opaque target ref | owner-local command buffer、sequence allocator、target resolver | raw `Entity`、writable DynamicBuffer、同步执行 gameplay |
| `SnapshotReadModel` | immutable snapshot、version、cursor / lag | BoundaryProjection ring / copy arena / report key map | live buffer read、raw `Entity` key、command write |
| `DiagnosticsSink` | evidence snapshot、official tool state、derived export handle | Debugger singleton、Profiler / Journaling source、structured export source | gameplay decision、command carrier、Core mutation |
| `RunnerSync` | fixed tick result、drain reason、timing split | tick driver、dependency fence、measurement marker | command write、catalog install、snapshot read side effect |
| `DefinitionCatalogLifetime` | catalog handle、schema hash、install / release result | Blob lifetime、static lookup lifetime、dispose owner | managed row hot path、generated lifecycle、system registration |
| `PresentationBridge` | binding id、presentation event cursor | presentation outbox、managed binding registry、view model cache | Core fact carrier、runtime singleton query、command write |

任何聚合 wrapper 只能持有 capability 引用，不能持有通用 ECS handle 解析权。若为了迁移保留内部 resolver，它只能服务单一 capability owner，并且必须有退出任务。

## Carrier 与 Fan-in 目标裁决

| Data-kind | 目标 carrier | 必填 evidence | 禁止方向 |
|---|---|---|---|
| Boundary command | Boundary command queue / owner-local command buffer | request count、reject count、target miss、write pressure | request entity churn、raw `Entity` public API |
| Effect command fan-in | `NativeStream` + deterministic merge + target owner-local range | stream segment、merge cost、sort key、battle hash impact | singleton DynamicBuffer scale-ready、unordered ParallelWriter 结果 |
| Instant spec | owner-local spec range / chunk scratch | spec count、owner group、capacity / spill、set-by-caller range | global spec buffer 作为终局 |
| Attribute delta | target owner-local delta buffer + deterministic reduce | delta count、target group、reduce order、externalized ratio | random target write、AutoChess 第二套 Attribute apply |
| Active mutation | owner-local active mutation command / slot store | owner group、max owner range、capacity / spill、random lookup count | generated lifecycle / singleton command carrier 扩大 |
| Gameplay fact | Core reaction facts 与 Boundary observation facts 分流 | source lane、fact sequence owner、drop/spill、flush timing、projection count | global EventBus、singleton fact buffer 作为终局 |
| Debug telemetry | sampled counters / derived export snapshot | overhead、sample count、disabled reason | 每帧 string build / file IO 进 Core |
| Presentation outbox | Boundary / presentation queue | outbox lag、binding miss、presentation cost | managed callback 混入 Core timing |

`NativeStream` 只解决并行写入，不自动解决确定性；目标态必须定义 sort key、merge owner、allocator owner、dispose / rewind owner、capacity budget 和 Debugger counter。任何保留 singleton DynamicBuffer 的路径都必须明确 proof-only mask、规模上限、pressure counter、spill counter 和退出任务。

## DefinitionCatalogLifetime 裁决

目标态 `DefinitionCatalogLifetime` 是独立 capability，不能埋在 Shell、AutoChess wrapper 或 generated static helper 内。

必须提供：

1. catalog handle、version、schema hash、install result、release result。
2. Blob / static lookup 的 ownership：谁分配、谁持有、谁释放、何时 orphan / mismatch。
3. Runtime Core hot path 只读 immutable catalog / lookup，不读取 managed row、JSON、Excel row 或 `Dictionary`。
4. SourceGenerator 只生成 catalog data、static lookup、pure evaluator、validation graph；不生成 runtime lifecycle owner。
5. release-ready mode 下，generated `ISystem`、query、ECB、NativeContainer owner、random write lookup、managed config hot path、system registration helper 回流必须 fail-fast。

## GAS Concept Coverage Matrix 裁决

目标态 Runtime 必须能从 immutable definition catalog 与 Runtime resolver 推导机器可读 GAS 概念覆盖，而不是靠人工日志判断“概念完整”。

必填项：

1. Ability lifecycle、GameplayEffect spec、attribute modifier、magnitude evaluator、tag taxonomy、tag requirement、gameplay cue、ASC binding、active effect、execution calculation、set-by-caller、granted ability、AbilityTask-like continuation 必须进入同一 coverage matrix。
2. coverage matrix 必须区分 covered / missing；missing 不能被 AutoChess 战斗胜利、中文战报、Mermaid 图或单次 hash 通过掩盖。
3. Runtime trace preview 必须能从 ability code 推导 activation plan、GE command seed、spec shape、modifier / delta / fact / cue stage 和 missing stage。
4. AutoChess 只能消费该 matrix 作为真实业务 evidence；AutoChess 不得手写第二套 GAS 概念判断。
5. Ability Package / Effect Package 只能作为 Authoring 聚合，不得进入 Runtime Core coverage 判定。

## Debugger / Evidence 裁决

目标态 Debugger 输出的是机器可读 evidence，不是日志总线。

必填 evidence domain：

| Domain | 必须区分 |
|---|---|
| Core | command resolve、spec build、attribute reduce/apply、active mutation、fact flush |
| StructuralCommit | ECB playback、structural intent source、Journaling / Profiler source 或 disabled reason |
| Boundary | snapshot copy、presentation outbox、replay sink、structured log projection |
| Diagnostics | observation materialization、derived export、official diff、TopN attribution |
| Runner | fixed tick、dependency drain、measurement fence |
| Presentation | managed Cue / VFX / UI bridge、binding miss、render disabled reason |

字符串 summary、Mermaid 图、中文战报、file export 都是 derived view。它们只能从 evidence snapshot 派生，不能成为 Core hot path 输入，也不能替代 Profiler / Journaling 状态。

## AutoChess 验收裁决

AutoChess 目标态只允许对外暴露：

1. 业务动作：创建战斗、推进 tick、请求 ability / GE、销毁单位。
2. opaque handle：battle unit key、driver id/version、request id、snapshot version、catalog handle。
3. immutable snapshot：unit result、battle report、presentation snapshot、diagnostics snapshot。
4. structured evidence：summary hash、facts hash、official tool state、timing split、direct EM 分类、disabled reason。

AutoChess public Flow / Session / Report / Presentation 不得持有 `World`、`EntityManager`、runtime singleton、raw `Entity`、live `ASCReadModel` 或 job drain handle。内部 adapter 如需保留这些能力，必须按 capability access matrix 标记 timing domain、required evidence、forbidden reuse 和退出任务。

## 目标验收门

| Gate | 通过条件 |
|---|---|
| Shell / Adapter | public seam 不返回 ECS handle；internal resolver 单 capability；before / after capability matrix 完整 |
| Carrier | command/spec/delta/fact/mutation 每类都有 current / target carrier、allocator owner、merge key、capacity、spill、proof-only 标记 |
| SourceGenerator | manifest/report/file/schedule 对账；generated lifecycle / structural / ownership / random lookup 负例 fail-fast |
| DefinitionCatalogLifetime | catalog install / release / dispose owner 可验证；static lookup 不泄露 unmanaged lifetime |
| StructuralCommit | structural playback source、phase、count 和 official tool state 可归因 |
| Debugger | Core / Boundary / Diagnostics / Runner / Presentation timing 分离；derived export 不污染 performance pass |
| AutoChess | headless / scene 同构 evidence；x50 / x100 / x1000 至少按 R8 规模门输出 hash、timing split、official state 和 disabled reason |
| Performance | 不能只用功能通过、单次 avgTickMs、0 static hit 或 wrapper 集中化证明 DOTS 优秀 |

## 非目标

1. 不恢复旧 OOP runtime 主链。
2. 不把 `GASRuntimeShell`、`AutoChessGasRuntimeAccess`、singleton stream 或 generated marker 改名后当完成态。
3. 不手改 `.gen.cs` 作为架构修复；必须回 template / manifest / report / CLI / validation gate。
4. 不把 Debugger / Presentation / AutoChess 的 derived export 写成 Runtime Core 权威事实。
5. 不在本 Spec 写当前代码行号、当前 hit 数、某次 x50 结果或下一步任务状态。
