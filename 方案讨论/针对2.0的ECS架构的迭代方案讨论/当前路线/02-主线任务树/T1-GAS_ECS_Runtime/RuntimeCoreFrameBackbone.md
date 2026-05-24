# GAS ECS Runtime - Runtime Core Frame Backbone

## 父节点

[T1 GAS ECS Runtime](README.md)

## 节点定位

本支线从 `RuntimeCore重构.md` 中拆出 `T1-RuntimeCore-AM2B Runtime Core Frame Backbone`，把原本过粗的单个任务拆成可连续领取、可独立交还、可逐步验证的任务链。

本支线不重新设计 GAS 语义。它只负责把 Unity DOTS 官方参考要求的 SystemGroup、Frame Arena、query / lookup、allocator、dependency、stream owner、structural playback 和 Debugger evidence 变成 Runtime Core 的可执行骨架。

## 当前问题

1. `00-当前架构事实/核心问题诊断/ISSUE-009-RuntimeCoreFrameBackbone缺失.md` 已确认当前实现缺少统一 frame backbone。
2. AM2 / AM3 / AM5 已有局部 proof，但仍挂在旧 group / helper / singleton buffer 形态上。
3. 如果继续把 AM2B 作为单个粗任务领取，Agent 很容易一轮同时改 SystemGroup、stream、Debugger 和 tests，导致上下文膨胀、验收边界不清。
4. DOTS 官方参考要求 query / buffer / job / structural / allocator / determinism / Profiler 证据分别进入交还；这些证据需要拆成连续任务链。

## 目标态参考

1. `../../01-目标态架构共识/00-总览Spec.md`
2. `../../01-目标态架构共识/03-RuntimeCore管线Spec.md`
3. `../../01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
4. `../../01-目标态架构共识/05-ActiveEffectStoreSpec.md`
5. `../../01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`
6. `../../UnityDOTS官方文档参考/README.md`
7. `../../UnityDOTS官方文档参考/主题/02-查询遍历与Job.md`
8. `../../UnityDOTS官方文档参考/主题/03-结构变化-ECB-Enableable.md`
9. `../../UnityDOTS官方文档参考/主题/04-数据承载-Buffer-Chunk-Store.md`
10. `../../UnityDOTS官方文档参考/主题/06-Diagnostics-Profiler-Journaling.md`
11. `../../UnityDOTS官方文档参考/主题/10-Collections-Allocator-NativeStream.md`
12. `../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
13. `../../UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`

## 当前事实参考

1. `../../00-当前架构事实/核心问题诊断/ISSUE-001-GE生命周期管线过重.md`
2. `../../00-当前架构事实/核心问题诊断/ISSUE-003-RuntimeCoreDebugger证据不足.md`
3. `../../00-当前架构事实/核心问题诊断/ISSUE-004-结构变化边界脆弱.md`
4. `../../00-当前架构事实/核心问题诊断/ISSUE-008-目标态Spec尚未充分UnityEntities机制化.md`
5. `../../00-当前架构事实/核心问题诊断/ISSUE-009-RuntimeCoreFrameBackbone缺失.md`

## 支线目标

建立 Runtime Core DOTS frame backbone，让后续 AM3 / AM5 / AM4 / T4 / T6 任务都能复用同一套：

1. SystemGroup / phase 顺序。
2. Frame preparation owner。
3. Query / lookup / allocator / dependency budget。
4. Stream / store frame ownership。
5. Deterministic merge policy。
6. 唯一 structural playback gate。
7. Debugger evidence gate。

## 非目标

1. 不迁移所有 GameplayEffect。
2. 不完成 ActiveEffectStore 全生命周期。
3. 不推进 AutoChessDemo 业务拆分。
4. 不接入真实 Physics / Graphics 资源。
5. 不把 singleton DynamicBuffer、NativeStream、ECB、Enableable 或 request entity 固化为最终答案。

## 任务链看板

| 顺序 | 任务ID | 任务名 | 状态 | 交付物 |
|---|---|---|---|---|
| 1 | T1-RuntimeCore-AM2B-A | GAS ECS Runtime - Runtime Core Frame Backbone - Schedule / Phase Contract | 已完成（contract-first，Unity验证待补跑） | SystemGroup / phase contract 与测试 |
| 2 | T1-RuntimeCore-AM2B-B | GAS ECS Runtime - Runtime Core Frame Backbone - Frame Arena 与 Query Budget | 推荐优先 | query / lookup / allocator / dependency budget |
| 3 | T1-RuntimeCore-AM2B-C | GAS ECS Runtime - Runtime Core Frame Backbone - Stream Owner 与 Deterministic Merge | 候选 | stream owner 表、clear/write/read/merge phase |
| 4 | T1-RuntimeCore-AM2B-D | GAS ECS Runtime - Runtime Core Frame Backbone - Structural Playback Gate | 候选 | 唯一结构变化屏障、ECB / bulk query policy |
| 5 | T1-RuntimeCore-AM2B-E | GAS ECS Runtime - Runtime Core Frame Backbone - Debugger Evidence Gate | 候选 | frame backbone counters 与 Profiler / Journaling 对照口径 |
| 6 | T1-RuntimeCore-AM2B-F | GAS ECS Runtime - Runtime Core Frame Backbone - AM3 / AM5 Rebind 与交还验证 | 候选 | AM3 / AM5 任务重绑定、验收摘要和后续入口 |

## 官方文档覆盖矩阵

| 任务 | 必读主题 | 必须交还的 DOTS 证据 |
|---|---|---|
| AM2B-A | `01`, `03`, `20`, `21` | SystemGroup 顺序、phase owner、结构变化权限 |
| AM2B-B | `02`, `10`, `20`, `21` | query contract、lookup update count、allocator owner、dependency wait |
| AM2B-C | `04`, `10`, `20`, `21` | stream owner、buffer pressure、deterministic merge、proof-only marker |
| AM2B-D | `03`, `04`, `06`, `20`, `21` | structural playback count、ECB command count、bulk query policy |
| AM2B-E | `02`, `03`, `04`, `06`, `09`, `10`, `21` | Debugger counters、Profiler / Journaling 对照、overhead / disable policy |
| AM2B-F | `20`, `21` | AM3 / AM5 action report 模板、验收和反哺状态 |

## 任务领取总规则

1. 当前只推荐领取 `GAS ECS Runtime - Runtime Core Frame Backbone - Frame Arena 与 Query Budget`（任务ID：`T1-RuntimeCore-AM2B-B`）。
2. 后续任务必须按顺序领取；除非当前窗口明确调整，不跳过前置任务。
3. 每个任务行动报告都必须引用 `ISSUE-009`，并说明本任务解决 ISSUE-009 的哪一段。
4. 每个任务交还后必须更新 `04-当前进度状态/迭代摘要.md`，若改变当前事实则更新 `00-当前架构事实/核心问题诊断/ISSUE-009-RuntimeCoreFrameBackbone缺失.md`。
5. 如果任务发现目标态 Spec 不足，先更新 `01-目标态架构共识/03-RuntimeCore管线Spec.md`，再继续实现。

## 任务链状态流转规则

同一时间只允许一个 AM2B 子任务处于 `推荐优先`。当前节点验收通过后，必须把下一节点提升为 `推荐优先`，并同步 `02-主线任务树/README.md`、`T1-GAS_ECS_Runtime/README.md`、`RuntimeCore重构.md`、`04-当前进度状态/当前窗口.md` 和 `04-当前进度状态/迭代摘要.md`。

| 完成节点 | 下一推荐节点 | 必须确认的交还条件 |
|---|---|---|
| AM2B-A | AM2B-B | Schedule / phase contract 已可测试；结构变化权限已经进入 contract；后续任务有稳定 phase owner 可引用 |
| AM2B-B | AM2B-C | Query / lookup / allocator / dependency budget 已形成显式表；不再依赖隐式 helper 查询作为任务前置 |
| AM2B-C | AM2B-D | Stream / store owner、clear / write / read / merge phase 和 deterministic merge policy 已明确 |
| AM2B-D | AM2B-E | 结构变化入口收敛到唯一 playback gate；ECB / bulk query policy 有证据；旧分散结构变化点有迁移计划 |
| AM2B-E | AM2B-F | Debugger 能观测 frame backbone 的 phase、query、stream、structural 和 overhead 证据，且具备关闭口径 |
| AM2B-F | AM3 / AM5 / AM4 或 AutoChess 验收 | AM3 / AM5 已重绑定新骨架；如果 Runtime Core 主链证据闭合，则回到 AutoChess 做业务验收，否则进入 AM4 或继续 Runtime Core 缺口任务 |

如果某一节点未通过验收，只更新该节点的失败证据，不提升下一节点。若必须跳过节点，必须在本文件、`04-当前进度状态/当前窗口.md` 和 `00-当前架构事实/核心问题诊断/ISSUE-009-RuntimeCoreFrameBackbone缺失.md` 写清跳过原因和风险。

## 三级任务：Schedule / Phase Contract

任务ID：`T1-RuntimeCore-AM2B-A`

状态：`已完成（contract-first，Unity验证待补跑）`

任务名：`GAS ECS Runtime - Runtime Core Frame Backbone - Schedule / Phase Contract`

当前问题：

1. 当前 `GASSystemScheduleContract` 仍以 `GASCommandGroup / GASEffectGroup / GASAttributeGroup / GASAbilityGroup / GASCueGroup` 为主。
2. AM2 的 stream phase 直接挂在 `GASCommandGroup`，还没有独立的 Runtime Core phase 顺序契约。
3. 后续任务如果没有先固定 phase contract，会继续出现 helper 临时查询、stream 清理时机不统一、structural playback 分散的问题。

目标 / 目的：

1. 定义 Runtime Core frame backbone 的 phase 顺序契约。
2. 明确哪些 phase 可读、可写、可结构变化、可观察。
3. 为 AM2B-B 到 AM2B-F 提供可测试的 schedule owner。

执行范围：

1. `Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs`
2. `Assets/_Test/GAS/Runtime/Event/SystemScheduleContractTests.cs`
3. 必要时新增最小 schedule contract 类型或测试辅助。

执行细则：

1. 先建立契约，不一次性搬迁所有系统。
2. 目标 phase 至少覆盖 `FramePrepare / CommandIngest / SpecEvaluation / ActiveEffectLifecycle / DeltaApply / TypedFactProjection / StructuralPlayback / ObservationProjection`。
3. 每个 phase 必须声明结构变化权限。
4. 如果现阶段只能以 contract / tests 表达而不创建真实 SystemGroup，必须标记为 contract-first，并给出下一任务落地入口。

API 选型：

| 候选 | 本任务用途 | 采用 / 拒绝口径 |
|---|---|---|
| `ComponentSystemGroup` | phase owner | 默认采用或以 contract 表达 |
| `ISystem` | 后续 phase system | 本任务只登记，不强制实现 |
| ECB playback group | structural gate | 只定义入口，不实现所有 playback |
| 自定义 helper 顺序 | 旧实现惯性 | 不作为目标态 phase owner |

验收标准：

1. 存在可检索的 Runtime Core backbone phase contract。
2. 测试能验证 phase 顺序和结构变化权限。
3. `RuntimeCore重构.md` 和当前窗口仍指向 `GAS ECS Runtime - Runtime Core Frame Backbone - Frame Arena 与 Query Budget`（任务ID：`T1-RuntimeCore-AM2B-B`）作为下一任务。

测试链路：

1. `git diff --check`
2. `rg -n "FramePrepare|CommandIngest|SpecEvaluation|StructuralPlayback|RuntimeCoreFrame" Assets/GAS/Runtime Assets/_Test/GAS/Runtime`
3. Runtime schedule contract tests；如 Unity 阻塞，记录环境阻塞。

交还内容：

1. 更新本文件任务状态。
2. 更新 `04-当前进度状态/迭代摘要.md`。
3. 若发现 ISSUE-009 证据变化，更新 ISSUE-009。

本轮 AM2B-A 进展：

1. `GASSystemScheduleContract` 已新增 contract-first 的 `RuntimeCoreFramePhases`，覆盖 `FramePrepare / CommandIngest / SpecEvaluation / ActiveEffectLifecycle / DeltaApply / TypedFactProjection / StructuralPlayback / ObservationProjection`。
2. 每个 phase 已声明读写访问口径、结构变化权限和 observation boundary；当前只有 `StructuralPlayback` 允许 `PlaybackOnly`，`FramePrepare / ActiveEffectLifecycle` 仅允许记录结构变化意图。
3. `EffectCommandSpecStreamTargetSystems` 已映射到 Runtime Core backbone phase：ingest、spec build、active mutation、delta apply、typed fact projection。
4. 新增 `SystemScheduleContractTests` 覆盖 phase 顺序、结构变化权限和 AM2 stream system -> phase 映射。
5. 当前仍是 contract-first：真实 `ComponentSystemGroup` 搬迁、query / allocator budget、stream owner、deterministic merge 和 structural playback gate 继续由 AM2B-B 到 AM2B-F 承接。

## 三级任务：Frame Arena 与 Query Budget

任务ID：`T1-RuntimeCore-AM2B-B`

状态：`候选`

任务名：`GAS ECS Runtime - Runtime Core Frame Backbone - Frame Arena 与 Query Budget`

当前问题：

1. `EffectCommandSpecStream.TryGetSingleton` 和 `ResolveCurrentFrame` 仍有 helper 级临时 query。
2. 当前 Debugger 不输出 query count、lookup update count、dependency wait。
3. Runtime Core 没有统一的 frame arena owner。

目标 / 目的：

1. 建立 Frame Arena / Query Preparation 的契约或最小实现。
2. 明确 Runtime Core 每帧 query / lookup / allocator / dependency 的 owner。
3. 输出可被 Debugger 消费的 budget 数据结构或 contract。

执行范围：

1. `Assets/GAS/Runtime/System/SystemGroup`
2. `Assets/GAS/Runtime/System/Effect`
3. `Assets/GAS/Runtime/Debugger`
4. 对应 Runtime tests。

执行细则：

1. 不在本任务迁移所有 helper。
2. 优先将 helper 级 query 标记并收束到 frame prepare contract。
3. allocator 必须说明 `WorldUpdateAllocator`、system group allocator 或 Rewindable allocator 的 owner 和生命周期。
4. dependency wait 先做可记录口径，不要求一次性全 job 化。

API 选型：

| 候选 | 本任务用途 | 采用 / 拒绝口径 |
|---|---|---|
| EntityQuery / EntityQueryBuilder | query contract | 必须声明 All / Any / None / filter |
| ComponentLookup / BufferLookup | lookup budget | 必须记录 update count 和随机访问风险 |
| WorldUpdateAllocator | frame scratch | 优先评估 |
| RewindableAllocator | 批量 frame scratch | 需要 owner 和 rewind 时机 |

验收标准：

1. Runtime Core 有 query / lookup / allocator / dependency budget 口径。
2. helper 临时 query 风险被记录或迁移到 frame prepare 入口。
3. Debugger 后续可读取这些预算字段。

测试链路：

1. `git diff --check`
2. `rg -n "QueryBudget|LookupBudget|AllocatorOwner|DependencyBudget|WorldUpdateAllocator|Rewindable" Assets/GAS/Runtime Assets/_Test/GAS/Runtime`

## 三级任务：Stream Owner 与 Deterministic Merge

任务ID：`T1-RuntimeCore-AM2B-C`

状态：`候选`

任务名：`GAS ECS Runtime - Runtime Core Frame Backbone - Stream Owner 与 Deterministic Merge`

当前问题：

1. `EffectCommandSpecStream` 当前以 singleton DynamicBuffer 承载 command/spec/delta/fact。
2. 当前 stream 有 frame-local clear，但没有统一 owner 表和 deterministic merge policy。
3. 百万实体目标下，单一全局 buffer 需要被证明为 proof-only 或重新选型。

目标 / 目的：

1. 定义 command / spec / delta / fact / active mutation 的 frame owner 表。
2. 明确 clear / write / read / merge phase。
3. 给出 singleton DynamicBuffer、per-owner buffer、NativeStream、ECB append 的采用 / 拒绝理由和重新选型触发条件。

执行范围：

1. `Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs`
2. `Assets/GAS/Runtime/System/Effect/SEffectCommandSpecStreamPhases.cs`
3. `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeQueryLayoutPlan.cs`
4. 对应 Runtime tests。

执行细则：

1. 不必立即切换 NativeStream，但必须明确何时切换。
2. 影响 gameplay result 的 merge 必须 deterministic。
3. Debug telemetry 和 presentation marker 不得混入 gameplay deterministic stream。

验收标准：

1. 存在 stream owner 表。
2. `EffectCommandSpecStream` 被明确标记为 proof-only、migration carrier 或 scale-ready carrier。
3. 有 deterministic output policy 和 battle hash / equivalent evidence 入口。

测试链路：

1. `git diff --check`
2. `rg -n "StreamOwner|DeterministicMerge|ProofOnly|NativeStream|MergePolicy" Assets/GAS/Runtime Assets/_Test/GAS/Runtime`

## 三级任务：Structural Playback Gate

任务ID：`T1-RuntimeCore-AM2B-D`

状态：`候选`

任务名：`GAS ECS Runtime - Runtime Core Frame Backbone - Structural Playback Gate`

当前问题：

1. 当前 Runtime 已有 `RecordRuntimeCoreEcbPlayback`，但结构变化还不是唯一 playback gate。
2. ISSUE-004 说明结构变化边界脆弱，ObjectDisposed / handle invalidation 风险可能回归。
3. AM5 store 后续 cleanup / compact / granted cleanup 都会触发结构变化策略选择。

目标 / 目的：

1. 定义 `GasStructuralPlaybackSystemGroup` 或等价 structural playback gate。
2. 明确哪些 phase 只能记录 mutation，不能直接结构变化。
3. 明确 ECB command、EntityQuery bulk、ComponentTypeSet、cleanup component 的采用口径。

执行范围：

1. `Assets/GAS/Runtime/System/SystemGroup`
2. `Assets/GAS/Runtime/System/Effect`
3. `Assets/GAS/Runtime/Debugger`
4. `Assets/_Test/GAS/Runtime/Event/RuntimeStructuralChangePlanTests.cs`

验收标准：

1. Runtime Core hot path 有唯一 structural playback gate 契约。
2. Debugger 能按 gate 统计 structural playback。
3. AM3 / AM5 后续任务不得绕过该 gate。

测试链路：

1. `git diff --check`
2. `rg -n "StructuralPlayback|RuntimeStructuralChange|ComponentTypeSet|AtPlayback|Cleanup" Assets/GAS/Runtime Assets/_Test/GAS/Runtime`

## 三级任务：Debugger Evidence Gate

任务ID：`T1-RuntimeCore-AM2B-E`

状态：`候选`

任务名：`GAS ECS Runtime - Runtime Core Frame Backbone - Debugger Evidence Gate`

当前问题：

1. Debugger 已有业务 counters、ECB playback、slot pressure。
2. 仍缺 query count、lookup update、allocator owner、dependency wait、stream segment、merge cost、Burst / safety 口径。
3. 缺少这些 counters 时，AutoChess 性能优秀线无法作为自动停止 Goal 的证据。

目标 / 目的：

1. 将 AM2B-A 到 AM2B-D 的 frame backbone 证据接入 RuntimeCoreDebugger。
2. 建立 frame backbone counters export。
3. 明确 Debugger 自身 overhead、采样策略和 disable policy。

执行范围：

1. `Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs`
2. `Assets/_Test/GAS/Runtime/Debugger/GasRuntimeDebuggerTests.cs`
3. AutoChess summary 若需要只接字段，不推进业务拆分。

验收标准：

1. Debugger 输出 frame backbone counters。
2. counters 能区分 core / physics / render / runner。
3. 性能报告不再只依赖 `avgTickMs`。

测试链路：

1. `git diff --check`
2. `rg -n "FrameBackbone|QueryCount|LookupUpdate|AllocatorOwner|DependencyWait|MergeCost|StructuralPlayback" Assets/GAS/Runtime Assets/_Test/GAS/Runtime Assets/AutoChessDemo`

## 三级任务：AM3 / AM5 Rebind 与交还验证

任务ID：`T1-RuntimeCore-AM2B-F`

状态：`候选`

任务名：`GAS ECS Runtime - Runtime Core Frame Backbone - AM3 / AM5 Rebind 与交还验证`

当前问题：

1. AM3 / AM5 当前任务描述仍保留 proof 小闭环历史。
2. Frame backbone 完成后，后续功能迁移必须重新绑定到新 phase、stream owner 和 debugger gate。

目标 / 目的：

1. 更新 AM3 / AM5 任务描述，使其显式依赖 AM2B backbone。
2. 更新 `02-主线任务树/README.md` 当前看板，决定下一推荐领取 AM3、AM4 还是 AM5。
3. 更新 `04-当前进度状态/当前窗口.md` 和 `00/ISSUE-009` 状态。

执行范围：

1. `02-主线任务树/README.md`
2. `02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCore重构.md`
3. 本文件
4. `00-当前架构事实/核心问题诊断/ISSUE-009-RuntimeCoreFrameBackbone缺失.md`
5. `04-当前进度状态/当前窗口.md`

验收标准：

1. AM2B 子任务链完成状态明确。
2. AM3 / AM5 后续任务不再允许绕过 frame backbone。
3. 当前窗口推荐领取下一个功能迁移任务，并说明原因。

测试链路：

1. `git diff --check`
2. `rg -n "AM2B|Runtime Core Frame Backbone|AM3|AM5|当前推荐领取" "方案讨论/针对2.0的ECS架构的迭代方案讨论/当前路线"`
