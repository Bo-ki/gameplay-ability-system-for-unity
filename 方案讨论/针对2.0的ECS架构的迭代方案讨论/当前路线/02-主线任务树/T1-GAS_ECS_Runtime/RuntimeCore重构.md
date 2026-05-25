# GAS ECS Runtime - Runtime Core 重构

## 父节点

[T1 GAS ECS Runtime](README.md)

## 节点定位

本支线负责 Runtime Core 主链重构，承接 T1 主线，把旧 GE lifecycle / global observation stream 混合热路径迁移为 command / spec / delta / store / facts 的 ECS-first 管线。

## 当前问题

1. 旧 GE lifecycle 仍承担 simple instant GE、runtime entity、event projection 和 structural change 成本。
2. 历史 fast path 没有解决 x50 曲线失真，只是在旧管线上继续补丁。
3. Runtime Core 与 Observation / Presentation 边界不清，会继续污染性能诊断。
4. 目标态 Spec 仍需落到 Unity Entities 1.4.6 的 SystemGroup、ISystem/job、ECB playback、DynamicBuffer、Enableable、Blob/Baker 和 Query filter 机制，并沉淀具体使用规则编号，避免“概念正确、实现仍偏自研 ECS 抽象”。
5. 当前目标态对 DOTS API 潜力挖掘仍不足：EffectCommand、ActiveEffectStore、Debugger、Luban 配置链不能只沿用全局 DynamicBuffer / 逐实体 ECB / request entity / singleton 这些当前实现惯性，必须按官方文档重新评估更适合的 API。
6. 本轮继续深读官方文档后，Runtime Core 任务还必须把 Query / Filter / Allocator / Dependency / Chunk layout / DynamicBuffer spill / Burst calculation 当成任务上下文；否则 AM3 / AM5 仍会把 proof 方案误写成目标态。
7. 本轮继续深挖官方案例后，Runtime Core 任务还必须能对照 DocCodeSamples / Tests / PerformanceTests 的实际写法；否则仍可能把入门 foreach、ECB 示例、SceneSystem load、Baking System 示例误搬进 Core hot path。
8. 本轮官方文档查缺补漏后，Runtime Core 任务还必须先读取 `UnityDOTS官方文档参考/README.md`，再对照 `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md` 覆盖矩阵；否则仍可能漏掉 sync point direct reference invalidation、system/job 固定开销、singleton dependency、chunk fragmentation、world time、allocator rewind 和官方诊断工具等流程性缺口。
9. Unity Physics / Entities Graphics 新包接入后，Runtime Core 任务还必须明确二者是否相关：Physics 只允许作为目标获取、命中确认、空间 query、collision / trigger event 输入层；Entities Graphics 只允许作为 Presentation / Boundary 渲染桥，不得反向污染 GAS Core 语义。

## 目标态参考

1. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
2. `01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
3. `01-目标态架构共识/05-ActiveEffectStoreSpec.md`
4. `01-目标态架构共识/10B-AutoChess完整业务案例设计Spec.md`
5. `UnityDOTS官方文档参考/README.md`
6. `UnityDOTS官方文档参考/主题/01-Entities系统与World.md`
7. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`
8. `UnityDOTS官方文档参考/README.md`
9. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
10. `UnityDOTS官方文档参考/主题/12-官方案例模式.md`
11. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`
12. `01-目标态架构共识/90-目标态不变量.md`

## 历史方案参考

1. `历史方案参考/方案14.md`、`方案15.md` 的 AutoChess 验收、四层模型和 Runtime rebuild 方向可参考。
2. `方案10.md`、`方案11.md` 的显式调度、Luban/Blob、Burst-friendly 数据视角可参考。
3. 托管 EventBus、生成 gameplay lifecycle、Debugger 参与 simulation routing 不可照搬。

## 支线目标

用 `Effect Command -> Instant Spec / Active Mutation -> Attribute Delta -> Typed Facts -> Observation Projection` 替换旧 GE lifecycle / global observation stream 混合管线。

## 当前状态

进行中。当前优先级已从冻结旧路径扩张、建立 EffectCommand / SpecStream / AttributeDelta 契约和 simple instant evaluation 新主链迁移，推进到 ActiveEffectStore owner-local store 第一刀、owner-local slot Debugger pressure / state distribution baseline，以及 AM5 period / overflow simple instant derived command proof。基于 Unity DOTS 官方参考复审，AM3 / AM5 当前只能视为局部 proof 小闭环；继续扩张前必须先进入 [RuntimeCoreFrameBackbone.md](RuntimeCoreFrameBackbone.md)。目前 AM2B-A 已完成 phase contract，AM2B-B 已完成 frame budget contract，AM2B-C 已完成 stream owner / deterministic merge contract，AM2B-D 已完成 structural playback gate contract，AM2B-E 已完成 debugger evidence gate contract，AM2B-F 已完成 AM3 / AM5 rebind handoff；当前 AM3 已迁入 activation simple single-target producer、ability cost self producer、Timeline ApplyEffects single-target 与 multi-target simple instant producer，并已用 `EffectCommandSpecStream.CommandWriter` 收缩 multi-target command fan-out 的 per-command frame query，`ResolveCurrentFrame` 的 5 处重复实现已收口到 `GASRuntimeFrameContext`，且 `GASRuntimeFrameContext` fallback query 已删除；attribute typed fact observation bridge、Cue-on-Apply projection、attribute / cue / generic gameplay / damage typed fact native Presentation / Replay consumer 也已接入，AM5 已让 period / overflow simple instant child GE 复用 command/spec/delta/fact 主链；下一步仍在 frame backbone 约束下收缩剩余 simple instant producer、业务 reaction typed fact consumer、全局临时 EntityQuery、真实 frame owner、真实 parallel fan-in / deterministic merge、granted cleanup 和 store-driven lifecycle 缺口。01 目标态架构共识全目录 DOTS 合规审查（AM-1K）已完成，18 个 Spec 内部一致性基线已建立；10B AutoChess 完整业务案例设计（AM-1J）已可作为 T6 验收的目标态设计预演。

## 非目标

1. 不一次性迁移所有 GameplayEffect。
2. 不在本支线内推进 Editor authoring。
3. 不用 Debugger 或 Presentation 输出替代 Runtime Core 状态。

## 前置依赖

1. `00-当前架构事实/核心问题诊断.md` 已确认旧 Runtime 管线是当前核心问题。
2. T4 Runtime Core Debugger 需要提供诊断 counters，辅助迁移验收。

## 执行范围

1. `Assets/GAS/Runtime/System/Effect`
2. `Assets/GAS/Runtime/Effect`
3. `Assets/GAS/Runtime/Attribute`
4. `Assets/GAS/Runtime/Ability`
5. Runtime tests 和 AutoChess validation。

## 执行细则

1. 新入口使用 `EffectCommand / SpecStream / AttributeDelta / ActiveEffectStore / TypedFacts` 命名。
2. 结构变化集中到明确 phase；禁止 helper 在热路径隐式创建 / 销毁实体。
3. Observation projection 只能消费 facts，不能反向改变 gameplay state。
4. Runtime Core 任务必须说明修改的 SystemGroup、使用的 `ISystem` / job 形态、是否涉及 ECB playback、DynamicBuffer 容量和 Enableable / stable archetype 策略。
5. Runtime Core 任务行动报告和交还必须列出适用的 `UnityDOTS官方文档参考/主题/90-规则编号索引.md` 规则编号。
6. Runtime Core 任务行动报告和交还必须包含 API 选型表：候选 API、最终选择、拒绝理由、预期指标和对应 `SEL-*` 规则。
7. Runtime Core 任务行动报告和交还必须包含官方文档覆盖检查：先说明 `UnityDOTS官方文档参考` 中的相关主题，再说明 PackageCache 证据、`ODF-*` 规则、采用 / 拒绝 / 暂不相关理由、反哺 owner 和验收指标。
8. 如果任务涉及目标获取、命中、范围、碰撞、触发器、表现资源或 rendered profile，行动报告必须覆盖 `ODF-15..18`、`PHY-*` 和 `GFX-*` 相关规则；如果不涉及，必须给出 not-related reason。

## 验收门槛

1. 新业务默认不依赖旧 instant GE entity lifecycle。
2. AutoChess x1 通过，x50 热点可由 diagnostics 解释。
3. 相关目标态 Spec 和当前架构事实同步更新。
4. 若任务触及 Physics / Graphics，验收必须拆分 `coreTickMs`、physics fixed-step / query / event cost、presentation marker cost 和 render cost；默认不启用时必须记录 disabled reason。

## 测试链路

1. Runtime EditMode tests。
2. AutoChess headless validation。
3. x50 profile / diagnostics summary。

## 当前任务看板

| 任务ID | 任务名 | 状态 | 目标 Spec |
|---|---|---|---|
| T1-RuntimeCore-AM0 | GAS ECS Runtime - Runtime Core 重构 - Freeze Safety Gate | 契约已确立 | `01/03 RuntimeCore`, `01/04 EffectCommand` |
| T1-RuntimeCore-AM1 | GAS ECS Runtime - Runtime Core 重构 - Unity Entities 机制校准 | 契约已确立 | `UnityDOTS官方文档参考/主题/01`, `UnityDOTS官方文档参考/主题/90` |
| T1-RuntimeCore-AM1B | GAS ECS Runtime - Runtime Core 重构 - Unity DOTS API 选型修正 | 已完成 | `UnityDOTS官方文档参考/主题/20` |
| T1-RuntimeCore-AM1C | GAS ECS Runtime - Runtime Core 重构 - DOTS API 深读反推 Runtime Core | 已完成 | `01/03 RuntimeCore`, `01/04 EffectCommand`, `01/05 ActiveEffectStore`, `UnityDOTS官方文档参考/主题/20` |
| T1-RuntimeCore-AM1D | GAS ECS Runtime - Runtime Core 重构 - Unity DOTS 官方案例反推 Runtime Core | 已完成 | `UnityDOTS官方文档参考/主题/90`, `UnityDOTS官方文档参考/主题/12` |
| T1-RuntimeCore-AM1E | GAS ECS Runtime - Runtime Core 重构 - Unity DOTS 官方文档参考体系主题化与流程闭环 | 已完成 | `UnityDOTS官方文档参考`, `UnityDOTS官方文档参考/主题/21`, `04-当前进度状态/迭代摘要反哺流程规范` |
| T1-RuntimeCore-AM1G | GAS ECS Runtime - Runtime Core 重构 - Unity Physics / Entities Graphics 新包覆盖 | 已完成 | `UnityDOTS官方文档参考`, `UnityDOTS官方文档参考/主题/90`, `UnityDOTS官方文档参考/主题/20`, `UnityDOTS官方文档参考/主题/21` |
| T1-RuntimeCore-AM2 | GAS ECS Runtime - Runtime Core 重构 - EffectCommand 与 SpecStream 契约 | 契约已确立 | `01/04 EffectCommand` |
| T1-RuntimeCore-AM2B | GAS ECS Runtime - Runtime Core 重构 - Runtime Core Frame Backbone | 已完成任务链（AM2B-A -> AM2B-F contract-first / rebind handoff；不再直接领取） | `01/03 RuntimeCore`, `UnityDOTS官方文档参考/主题/01/02/03/04/06/09/10/20/21` |
| T1-RuntimeCore-AM3 | GAS ECS Runtime - Runtime Core 重构 - Instant Spec Evaluation 迁移 | 推荐优先（Timeline ApplyEffects multi-target simple instant fan-out、attribute / cue / generic gameplay / damage typed fact native Presentation / Replay consumer 已接入；基于 AM2B rebind contract 继续迁移剩余 simple instant producer、业务 reaction typed fact consumer 和 parallel fan-in 缺口） | `01/04 EffectCommand` |
| T1-RuntimeCore-AM5 | GAS ECS Runtime - Runtime Core 重构 - Active Effect Store 重建 | 进行中（owner-local store 第一刀、Debugger slot pressure baseline、period / overflow simple instant derived command proof 已落地；后续扩张必须复用 AM2B rebind contract） | `01/05 ActiveEffectStore` |

## 三级任务：Runtime Core Frame Backbone

任务ID：`T1-RuntimeCore-AM2B`

状态：`任务链入口（不直接领取）`

任务名：`GAS ECS Runtime - Runtime Core 重构 - Runtime Core Frame Backbone`

维护入口：[RuntimeCoreFrameBackbone.md](RuntimeCoreFrameBackbone.md)

领取规则：本节点只保留任务链总上下文，不再作为单个粗任务领取。`GAS ECS Runtime - Runtime Core Frame Backbone - Schedule / Phase Contract`（任务ID：`T1-RuntimeCore-AM2B-A`）已完成 contract-first；`GAS ECS Runtime - Runtime Core Frame Backbone - Frame Arena 与 Query Budget`（任务ID：`T1-RuntimeCore-AM2B-B`）已完成 contract-first / budget-first；`GAS ECS Runtime - Runtime Core Frame Backbone - Stream Owner 与 Deterministic Merge`（任务ID：`T1-RuntimeCore-AM2B-C`）已完成 contract-first；`GAS ECS Runtime - Runtime Core Frame Backbone - Structural Playback Gate`（任务ID：`T1-RuntimeCore-AM2B-D`）已完成 contract-first；`GAS ECS Runtime - Runtime Core Frame Backbone - Debugger Evidence Gate`（任务ID：`T1-RuntimeCore-AM2B-E`）已完成 contract-first / debugger evidence gate；`GAS ECS Runtime - Runtime Core Frame Backbone - AM3 / AM5 Rebind 与交还验证`（任务ID：`T1-RuntimeCore-AM2B-F`）已完成 contract-first / rebind handoff。当前推荐领取 `GAS ECS Runtime - Runtime Core 重构 - Instant Spec Evaluation 迁移`（任务ID：`T1-RuntimeCore-AM3`）。

当前问题：

1. AM2 已落地 command / spec / delta / fact 数据契约，AM3 / AM5 也已有局部 proof，但它们仍缺少统一的 DOTS 原生帧骨架。
2. 当前 Runtime Core 任务可以分别讨论 DynamicBuffer、request entity、owner-local slot、Debugger counter，却还不能统一回答每帧 query / lookup / allocator / dependency / structural playback 由谁准备、谁清理、谁归因。
3. 如果继续直接扩展 AM3 / AM5，旧 lifecycle mirror 和 proof-only singleton stream 会继续增长，性能热点仍难归因。
4. Unity DOTS 官方参考已经要求 query、lookup、allocator、dependency、structural change、Burst、Profiler / Journaling 进入任务证据；这些要求需要一个独立 Runtime Core backbone 承载。

目标态参考：

1. `01-目标态架构共识/00-总览Spec.md`
2. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
3. `01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
4. `01-目标态架构共识/05-ActiveEffectStoreSpec.md`
5. `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`
6. `UnityDOTS官方文档参考/README.md`
7. `UnityDOTS官方文档参考/主题/01-Entities系统与World.md`
8. `UnityDOTS官方文档参考/主题/02-查询遍历与Job.md`
9. `UnityDOTS官方文档参考/主题/03-结构变化-ECB-Enableable.md`
10. `UnityDOTS官方文档参考/主题/04-数据承载-Buffer-Chunk-Store.md`
11. `UnityDOTS官方文档参考/主题/06-Diagnostics-Profiler-Journaling.md`
12. `UnityDOTS官方文档参考/主题/09-Burst-编译-向量化-AOT.md`
13. `UnityDOTS官方文档参考/主题/10-Collections-Allocator-NativeStream.md`
14. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
15. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`

当前事实参考：

1. `00-当前架构事实/核心问题诊断/ISSUE-001-GE生命周期管线过重.md`
2. `00-当前架构事实/核心问题诊断/ISSUE-003-RuntimeCoreDebugger证据不足.md`
3. `00-当前架构事实/核心问题诊断/ISSUE-004-结构变化边界脆弱.md`
4. `00-当前架构事实/核心问题诊断/ISSUE-008-目标态Spec尚未充分UnityEntities机制化.md`
5. `00-当前架构事实/核心问题诊断/ISSUE-009-RuntimeCoreFrameBackbone缺失.md`
6. `04-当前进度状态/迭代摘要.md` 中 `T0/T1-AM1I Unity DOTS 官方参考反推目标态与主线重排`

目标 / 目的：

1. 建立 `GasRuntimeFramePrepareSystemGroup`，统一准备 Runtime Core 每帧 query、lookup、type handle、frame scratch、allocator 和 dependency budget。
2. 明确 `EffectCommand`、`InstantEffectSpec`、`ActiveEffectMutation`、`AttributeDelta`、`TypedSimulationFact` 的 frame owner、clear phase、writer phase、reader phase、merge phase 和 deterministic output policy。
3. 建立 `GasStructuralPlaybackSystemGroup` 作为 Runtime Core hot path 唯一结构变化屏障，并禁止 Spec Evaluation / Delta Apply / Typed Fact Projection 直接做 `EntityManager` 结构变化。
4. 建立 Runtime Core Debugger frame backbone counters，覆盖 query count、lookup update count、random lookup count、allocator owner、dependency wait、stream merge、structural playback、ECB command、bulk query、Burst / safety 口径。
5. 将 AM3 / AM5 后续扩张改为基于该 backbone，而不是继续扩展旧 lifecycle mirror。

非目标：

1. 不迁移所有 GameplayEffect。
2. 不完成 ActiveEffectStore 全生命周期。
3. 不推进 AutoChessDemo 目录或业务拆分。
4. 不接入真实 Unity Physics / Entities Graphics 资源。
5. 不把 singleton DynamicBuffer、NativeStream、ECB、Enableable 或 request entity 固化为最终答案；本任务建立选型和证据骨架。

执行范围：

1. `Assets/GAS/Runtime/System/SystemGroup`
2. `Assets/GAS/Runtime/System/Effect`
3. `Assets/GAS/Runtime/Effect/Component/Dynamic`
4. `Assets/GAS/Runtime/Debugger`
5. `Assets/_Test/GAS/Runtime/Effect`
6. `Assets/_Test/GAS/Runtime/Debugger`
7. 相关目标态 Spec、当前事实和当前进度状态。

执行细则：

1. 执行前行动报告必须说明本任务属于 `T1 GAS ECS Runtime / Runtime Core 重构 / Runtime Core Frame Backbone`，并列出本节的目标态参考和当前事实参考。
2. 行动报告必须给出 API 选型表，至少覆盖 DynamicBuffer、NativeStream、per-owner buffer、ECB append、EntityQuery bulk、Enableable、Cleanup Component、Chunk Component、WorldUpdateAllocator、RewindableAllocator。
3. SystemGroup 设计必须先落 `FramePrepare -> CommandIngest -> SpecEvaluation -> ActiveEffectLifecycle -> DeltaApply -> TypedFactProjection -> StructuralPlayback -> ObservationProjection` 的顺序契约。
4. query / lookup / allocator / dependency 不能散落在 helper 中临时创建；如果必须保留临时路径，必须标记 proof-only 和重新选型触发条件。
5. stream / buffer clear 必须有明确 phase；禁止 consumer 读写过程中触发结构变化导致 DynamicBuffer handle invalidation。
6. deterministic merge 必须声明排序键、分区策略、merge phase 和 battle hash / equivalent evidence。
7. Debugger counters 必须能区分 core、physics、render、runner 成本；本任务默认不启用 Physics / Graphics 时要输出 disabled reason 或 not-related reason。
8. 适用规则至少包括 `SYS-*`、`JOB-*`、`QRY-*`、`SC-*`、`ECB-*`、`BUF-*`、`NAT-*`、`BUR-*`、`DBG-*`、`SEL-*`、`ODF-*` 中和任务相关项。

验收标准：

1. 代码或契约文档中存在 Runtime Core frame backbone 的 SystemGroup / phase 顺序，并可被测试或 Debugger 枚举。
2. frame owner 表能说明 command / spec / delta / fact / active mutation 的 owner、clear、write、read、merge 和 deterministic policy。
3. Debugger 能输出 frame backbone counters：query count、lookup update count、allocator owner、dependency wait、stream count / merge policy、structural playback count。
4. `GasStructuralPlaybackSystemGroup` 或等价契约成为唯一 hot path structural boundary；AM3 / AM5 后续任务不得绕过它。
5. AM3 / AM5 任务描述更新为“基于 AM2B backbone 扩张”，不再把局部 proof 当成目标态主线。
6. 如 Unity 验证受 LicensingClient 或 Editor lock 阻塞，必须记录为环境阻塞，不得宣称编译失败。

测试链路：

1. `git diff --check`
2. `rg -n "Runtime Core Frame Backbone|GasRuntimeFramePrepareSystemGroup|GasStructuralPlaybackSystemGroup|FramePrepare|structural playback" Assets/GAS/Runtime Assets/_Test/GAS/Runtime "方案讨论/针对2.0的ECS架构的迭代方案讨论/当前路线"`
3. Runtime EditMode tests；如 Unity LicensingClient 阻塞，记录 return code 和关键日志。
4. Debugger / contract tests 验证 frame backbone counters 和 phase 顺序。

交还内容：

1. 更新 `04-当前进度状态/迭代摘要.md`，记录 frame backbone 的实现范围、发现、验证结果和反哺状态。
2. 如发现目标态 Spec 不足，先反哺 `01-目标态架构共识/03-RuntimeCore管线Spec.md`。
3. 如发现当前实现存在新的核心问题，按 `00-当前架构事实/核心问题诊断` 单问题文档规范新增或更新诊断。
4. 如 AM3 / AM5 的任务切分因此改变，更新本支线任务看板和 `02-主线任务树/README.md`。

## API 选型硬约束

AM3 / AM5 后续功能扩张前，必须先完成 `T1-RuntimeCore-AM2B Runtime Core Frame Backbone`，并在行动报告中说明本任务如何复用 frame owner、structural playback gate、deterministic stream policy 和 Debugger backbone counters。

1. AM3 继续推进前必须提交 EffectCommand 承载选型表：singleton DynamicBuffer、per-owner DynamicBuffer、`NativeStream`、request entity、ECB `AppendToBuffer`、EntityQuery bulk 的采用或拒绝理由。
2. AM5 后续实现必须继续提交 ActiveEffectStore 存储选型表：ASC slot buffer、stable active effect entity、enableable marker、enum state、Cleanup Component、Chunk Component、LinkedEntityGroup 的采用或拒绝理由。
3. T4 Debugger 必须能输出 API 选型健康指标：global buffer pressure、NativeStream merge、per-chunk skip、structural query batch、singleton dependency warning。
4. 上述选型表属于任务行动报告的一部分，不等待批准，但必须让用户能判断 Agent 是否真的掌握了目标态上下文。
5. AM3 / AM5 / T4 继续实现前必须引用本轮新增规则：`SEL-21` 到 `SEL-32`、`QRY-06` 到 `QRY-08`、`BUF-07`、`EN-05`、`NAT-07`、`BUR-06`、`DBG-09`。
6. AM3 行动报告必须区分 Boundary request、Core frame command、parallel fan-in stream、structural mutation request 四类 command，并说明当前任务只处理哪一类。
7. AM5 行动报告必须区分 OwnerLocalStore、GlobalIndexedStore、LifecycleCleanupStore、ChunkSkipIndex 四类 ActiveEffectStore，并说明当前任务先落哪一类。
8. 任何 scale gate 任务必须报告 archetype / chunk / unused entities / DynamicBuffer externalized / enableable wait / job overhead / Burst warmup，而不是只给 `avgTickMs`。
9. AM3 / AM5 / T4 / T2 后续任务行动报告必须新增“官方案例对照”小节，至少引用 `CASE-01` 到 `CASE-12` 中与任务相关的规则。
10. AM3 / AM5 / T4 / T2 后续任务行动报告必须新增“官方文档覆盖检查”小节，先引用 `UnityDOTS官方文档参考` 的主题入口，再至少引用 `ODF-01` 到 `ODF-18` 中与任务相关的规则，并说明覆盖矩阵中相关主题的采用 / 拒绝 / 暂不相关理由。
11. 涉及 Physics / Graphics 的任务必须先说明是否需要 `PhysicsWorldSingleton`、`SimulationSingleton`、query broadphase、collision / trigger event、`RenderMeshArray`、`MaterialMeshInfo`、material override 或 render evidence；这些内容只能作为 Boundary / Presentation 输入输出，不得成为 Core state authority。

## 三级任务：Unity DOTS 官方案例反推 Runtime Core

任务ID：`T1-RuntimeCore-AM1D`

状态：`已完成`

任务名：`GAS ECS Runtime - Runtime Core 重构 - Unity DOTS 官方案例反推 Runtime Core`

当前问题：

1. `UnityDOTS官方文档参考` 已完成官方依据和 API 选型层校准，但仍偏规则和候选 API。
2. Agent 执行代码任务时还需要知道“官方示例实际怎么写”，否则可能把入门 `SystemAPI.Query`、ECB immediate playback、SceneSystem load、Baking System 等案例误当成 Core hot path 模板。
3. Runtime Core 后续 AM3 / AM5 / T4 / T2 任务需要一套 `CASE-*` 规则，能直接进入行动报告、任务交还和 Debugger 验收。

目标态参考：

1. `UnityDOTS官方文档参考/主题/12-官方案例模式.md`
2. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`
3. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
4. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
5. `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`

目标 / 目的：

1. 从官方 `DocCodeSamples.Tests`、`Unity.Entities.Tests`、`Unity.Scenes.Hybrid.Tests` 和 `PerformanceTests` 中提炼可执行模式。
2. 建立 `CASE-01` 到 `CASE-12`，补充 `SEL-*` 不能表达的“官方案例对照”层。
3. 反推 Runtime Core、ActiveEffectStore、EffectCommand、Debugger、Luban / SourceGenerator 和 AutoChess 验收。

非目标：

1. 不修改 `Library/PackageCache`。
2. 不在本任务推进 Runtime 代码实现。
3. 不把官方案例当作 GAS 业务语义第一性来源。

执行范围：

1. `UnityDOTS官方文档参考/主题/12-官方案例模式.md`
2. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`
3. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
4. 本支线任务树和当前进度状态。

执行细则：

1. 官方案例证据必须用本地 PackageCache 路径和行号定位。
2. 每个 CASE 规则必须能反推到一个 GAS Runtime Core 模块或验证链路。
3. 后续任务行动报告必须新增“官方案例对照”小节，说明采用 / 拒绝哪些 CASE 模式。

验收标准：

1. `17` 成为目标态索引的一部分。
2. `14` 中出现 `CASE-*` 规则。
3. RuntimeCore 任务树看板登记 `AM1D`，后续 AM3 / AM5 / T4 / T2 能引用。
4. 当前进度摘要记录本轮文档校准和反哺状态。

测试链路：

1. `rg -n "UnityDOTS官方案例|CASE-01|AM1D|DocCodeSamples|PerformanceTests" 当前路线`
2. `git diff --check -- "方案讨论/针对2.0的ECS架构的迭代方案讨论/当前路线"`

## 三级任务：Unity DOTS 官方文档参考体系主题化与流程闭环

任务ID：`T1-RuntimeCore-AM1E`

状态：`已完成`

任务名：`GAS ECS Runtime - Runtime Core 重构 - Unity DOTS 官方文档参考体系主题化与流程闭环`

当前问题：

1. `UnityDOTS官方文档参考` 已覆盖机制、使用规则、API 选型、官方案例和覆盖矩阵。
2. 行动报告已要求 API 选型、官方案例对照和“官方文档覆盖检查”，但如果没有主题入口，后续任务仍可能只引用文件名，不说明阅读顺序、规则层关系和证据如何影响实现取舍。
3. 迭代摘要反哺流程需要明确官方文档缺口如何先归属到 `UnityDOTS官方文档参考` 对应主题，并反哺业务 Spec、任务树和当前事实。

目标态参考：

1. `UnityDOTS官方文档参考/README.md`
2. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`
3. `UnityDOTS官方文档参考/README.md`
4. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`
5. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
6. `04-当前进度状态/迭代摘要反哺流程规范.md`
7. `Agent行动报告规范.md`

目标 / 目的：

1. 建立官方文档参考体系主题 owner。
2. 建立官方文档覆盖矩阵 owner。
3. 建立 `ODF-01` 到 `ODF-08`，补充 `SEL-*` 和 `CASE-*` 不能表达的“官方文档流程闭环”层。
4. 反推 Runtime Core 后续任务的行动报告、任务交还、Debugger 验收和当前窗口。

非目标：

1. 不修改 Runtime 代码。
2. 不修改 `Library/PackageCache`。
3. 不把在线最新文档直接应用到当前 `1.4.6` 项目版本。

执行范围：

1. `UnityDOTS官方文档参考/README.md`
2. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`
3. `UnityDOTS官方文档参考` 相关单主题文档
4. `02-主线任务树/任务树规范 + 01-目标态架构共识/Spec规范 + 04-当前进度状态/迭代摘要反哺流程规范 + Agent行动报告规范`
5. 本支线任务树和当前进度状态。

执行细则：

1. 官方文档证据优先使用本地 PackageCache 路径和行号定位。
2. 主题入口必须说明第一性版本、阅读顺序、规则层关系和反哺路径。
3. 覆盖矩阵每个主题必须能反推到一个 GAS owner 或验收指标。
4. 后续任务行动报告必须新增“官方文档覆盖检查”小节，先说明主题归属，再说明相关 `ODF-*` 规则。

验收标准：

1. `UnityDOTS官方文档参考` 成为当前路线级官方依据入口，目标态索引只作为引用方。
2. `UnityDOTS官方文档参考/主题/90-规则编号索引.md` 中出现 `ODF-*` 规则，并能追溯到主题入口。
3. 行动报告规范中出现“官方文档覆盖检查”。
4. RuntimeCore 任务树看板登记 `AM1E`，后续 AM3 / AM5 / T4 / T2 能引用。

测试链路：

1. `rg -n "官方文档参考体系|UnityDOTS官方文档参考|ODF-01|AM1E|官方文档覆盖检查" 当前路线`
2. `git diff --check -- "方案讨论/针对2.0的ECS架构的迭代方案讨论/当前路线"`

## 三级任务：Unity Entities 机制校准

任务ID：`T1-RuntimeCore-AM1`

状态：`契约已确立`

任务名：`GAS ECS Runtime - Runtime Core 重构 - Unity Entities 机制校准`

当前问题：

1. 当前目标态概念已明确，但 Runtime Core phase / stream 仍需落到 Unity Entities 1.4.6 的具体机制。
2. 如果不先校准 SystemGroup、ISystem/job、ECB playback、DynamicBuffer、Enableable、Blob/Baker、Query filter，后续 AM2/AM3/AM5 仍可能继续以 request entity、runtime GE entity 或全局 buffer 扫描实现高频链路。
3. 当前 ISSUE-008 已将该问题标记为 P1。

目标态参考：

1. `UnityDOTS官方文档参考/主题/01-Entities系统与World.md`
2. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`
3. `UnityDOTS官方文档参考/README.md`
4. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
5. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
6. `01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
7. `01-目标态架构共识/05-ActiveEffectStoreSpec.md`
8. `01-目标态架构共识/90-目标态不变量.md`

当前事实参考：

1. `00-当前架构事实/核心问题诊断/ISSUE-008-目标态Spec尚未充分UnityEntities机制化.md`
2. `00-当前架构事实/核心问题诊断/ISSUE-004-结构变化边界脆弱.md`
3. `00-当前架构事实/核心问题诊断/ISSUE-001-GE生命周期管线过重.md`

目标 / 目的：

1. 为 AM2 / AM3 / AM5 提供 Unity Entities 机制级实现模板。
2. 明确 Runtime Core SystemGroup 和 ECB playback 边界。
3. 明确高频 command data、DynamicBuffer、Enableable、Blob / Baker 的默认使用规则。
4. 建立后续代码改造前的机制级验收检查表和规则编号引用方式。

非目标：

1. 不在本任务迁移完整 EffectCommand 实现。
2. 不重写所有 Runtime system。
3. 不修改 Unity PackageCache。

执行范围：

1. 目标态 Spec 与任务树。
2. 如进入代码实现阶段，可新增最小 `GASSystemScheduleContract` / SystemGroup skeleton / contract tests，但本任务优先保证设计校准完整。

执行细则：

1. 执行前必须按 `Agent行动报告规范.md` 输出行动报告。
2. 所有 Runtime Core 任务都必须说明使用的 SystemGroup、job 形态、结构变化边界和 Debugger counters。
3. 不得把 Unity change filter 当实体级 gameplay event 语义。
4. request entity 只作为边界低频入口，不作为 high-frequency instant GE 默认承载。
5. 行动报告必须列出适用规则，例如 `SYS-01`, `JOB-01`, `SC-01`, `ECB-02`, `BUF-04`, `DBG-02`。
6. 行动报告必须列出 API 选型表，至少覆盖 `SEL-01`, `SEL-02`, `SEL-03`, `SEL-04`, `SEL-05`, `SEL-09` 中与任务相关的规则。

验收标准：

1. AM2 / AM3 / AM5 的任务描述能直接引用 Unity Entities 机制校准 Spec。
2. Runtime Core 任务模板能要求 SystemGroup / ECB / buffer / enableable / blob / query / debugger evidence 和规则编号。
3. Runtime Core 任务模板能要求 API 选型表，并解释为什么不采用其他候选 DOTS API。
4. 当前事实 ISSUE-008 有退出条件和目标态入口。

测试链路：

1. 文档检查：`rg -n "UnityDOTS官方文档参考|SystemGroup|ISystem|ECB playback|Enableable|DynamicBuffer|Blob|Baker|Query filter" 当前路线`。
2. 若新增代码 skeleton，运行 Runtime EditMode tests；如 Unity LicensingClient 阻塞，记录为环境阻塞。

交还内容：

1. 更新 `04-当前进度状态/迭代摘要.md`。
2. 如发现 Spec 缺口，按 `04-当前进度状态/迭代摘要反哺流程规范.md` 反哺到 `01` 或 `00`。

## 三级任务：EffectCommand 与 SpecStream 契约

任务ID：`T1-RuntimeCore-AM2`

状态：`进行中（代码契约已落地，Unity验证待补跑）`

任务名：`GAS ECS Runtime - Runtime Core 重构 - EffectCommand 与 SpecStream 契约`

当前问题：

1. 旧 `CApplyGameplayEffectRequest -> runtime GE entity -> lifecycle` 仍是 simple instant GE 的主要落点。
2. 如果没有先固定 ECS 数据契约，AM3 迁移 simple instant evaluation 时会继续产生临时 request entity 或复用旧 GE lifecycle。
3. RuntimeCoreDebugger 的 AM1 counters 已有 baseline，但 spec / delta / fact 需要开始接入权威 stream 计数。

目标态参考：

1. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
2. `01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
3. `UnityDOTS官方文档参考/主题/01-Entities系统与World.md`
4. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`
5. `UnityDOTS官方文档参考/README.md`
6. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
7. `01-目标态架构共识/90-目标态不变量.md`

当前事实参考：

1. `00-当前架构事实/核心问题诊断/ISSUE-001-GE生命周期管线过重.md`
2. `00-当前架构事实/核心问题诊断/ISSUE-004-结构变化边界脆弱.md`
3. `00-当前架构事实/核心问题诊断/ISSUE-008-目标态Spec尚未充分UnityEntities机制化.md`

目标 / 目的：

1. 定义 `EffectCommand -> InstantEffectSpec -> AttributeDelta -> TypedSimulationFact` 的具体 ECS 数据承载。
2. 让目标态 pipeline contract、schedule contract、query layout contract 都引用具体类型。
3. 明确 high-frequency instant GE 默认走 command data / DynamicBuffer，不默认创建 request entity 或 runtime GE entity。
4. 为 AM3 的 simple instant spec evaluation 迁移提供稳定输入输出边界。

非目标：

1. 不在本任务迁移完整 GameplayEffect 执行链。
2. 不重写 Active Effect Store。
3. 不继续推进 AutoChess 业务拆分。

执行范围：

1. `Assets/GAS/Runtime/Effect/Component/Dynamic`
2. `Assets/GAS/Runtime/System/Effect`
3. `Assets/GAS/Runtime/System/SystemGroup`
4. `Assets/GAS/Runtime/Debugger`
5. Runtime contract tests。

执行细则：

1. 数据契约优先使用 unmanaged `IComponentData` / `IBufferElementData`，stream owner 使用 singleton entity，实际高频数据使用 DynamicBuffer / 后续 Native stream。
2. `ContextId`、`ParentContextId`、`SetByCaller` 必须从 command/spec/delta/fact 连续传递。
3. 新 phase skeleton 可以进入显式 schedule，但不得在 AM2 中做 gameplay 写入副作用。
4. Debugger counters 开始优先统计权威 command/spec/delta/fact stream；旧 EventBus 计数继续作为迁移期近似。
5. AM2 的 singleton DynamicBuffer 只作为契约落点，不得被任务交还描述为最终高规模承载；AM3 前必须按 `16` 复核 `NativeStream` / per-owner stream / request entity / ECB `AppendToBuffer` / EntityQuery bulk。

验收标准：

1. Runtime pipeline contract 指向 `BEffectCommand`、`BInstantEffectSpec`、`BAttributeDelta`、`BTypedSimulationFact`。
2. Query layout 有 `GameplayEffectCommandSpecStream` entry，标记 `CommandDataBacked`、`NoPerHitStructuralChange`，且不带 `StructuralEntityManagerHotspot`。
3. 新增契约测试证明 bridge command 写入 stream 时不创建 `CApplyGameplayEffectRequest` 或 runtime GE lifecycle entity。
4. Unity Runtime tests 可运行时通过；如 LicensingClient 阻塞，记录为环境阻塞。

测试链路：

1. `git diff --check`
2. `rg -n "EffectCommandSpecStream|BEffectCommand|BInstantEffectSpec|BAttributeDelta|BTypedSimulationFact" Assets/GAS/Runtime Assets/_Test/GAS/Runtime -g "*.cs"`
3. Runtime EditMode tests；如 Unity LicensingClient 阻塞，记录 return code 和关键日志。

本轮 AM2 进展：

1. 新增 `CEffectCommandSpecStream` singleton 和 `BEffectCommand` / `BEffectCommandSetByCallerValue` / `BInstantEffectSpec` / `BAttributeDelta` / `BActiveEffectMutation` / `BTypedSimulationFact` buffer 契约。
2. 新增 `EffectCommandSpecStream` helper，提供 singleton / buffer ensure 与 legacy request bridge command 写入，不把普通 command 表达成 request entity。
3. 新增 `SEffectCommandIngest`、`SInstantEffectSpecBuild`、`SActiveEffectMutationApply`、`SAttributeDeltaApply`、`STypedSimulationFactProjection` phase skeleton，并接入 `GASSystemScheduleContract`。
4. 更新 `GASRuntimeQueryLayoutPlan`，新增 `GameplayEffectCommandSpecStream` layout entry，标记 command-data-backed、no-per-hit-structural-change。
5. 更新 `GasRuntimeDebugger`，当 AM2 stream 存在时把 command/spec/delta/fact 作为权威计数输入。
6. 新增 `EffectCommandSpecStreamContractTests` 覆盖 ECS 数据契约、context / SetByCaller 连续性、非默认实体化、schedule / layout 和 Debugger stream 计数。

## 三级任务：Instant Spec Evaluation 迁移

任务ID：`T1-RuntimeCore-AM3`

状态：`推荐优先（Timeline ApplyEffects multi-target simple instant fan-out、attribute / cue / generic gameplay / damage typed fact native Presentation / Replay consumer 已接入；基于 AM2B rebind contract 继续迁移剩余 simple instant producer、业务 reaction typed fact consumer 和 parallel fan-in 缺口，Unity验证待补跑）`

任务名：`GAS ECS Runtime - Runtime Core 重构 - Instant Spec Evaluation 迁移`

当前问题：

1. AM2 已固定 `EffectCommand -> InstantEffectSpec -> AttributeDelta -> TypedSimulationFact` 数据契约，但 simple instant GE evaluation 仍可能回落到旧 request entity / runtime GE entity 生命周期。
2. 旧 direct bypass 是 migration-only 冻结路径，不能继续作为新业务默认入口。
3. 如果 AM3 不先证明 direct command 主链可用，后续 AutoChess 验收仍无法区分 GAS Runtime 架构问题和业务拆分问题。

目标态参考：

1. `01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
2. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
3. `UnityDOTS官方文档参考/主题/01-Entities系统与World.md`
4. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`
5. `UnityDOTS官方文档参考/README.md`
6. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`

当前事实参考：

1. `00-当前架构事实/核心问题诊断/ISSUE-001-GE生命周期管线过重.md`
2. `00-当前架构事实/核心问题诊断/ISSUE-004-结构变化边界脆弱.md`
3. `00-当前架构事实/核心问题诊断/ISSUE-008-目标态Spec尚未充分UnityEntities机制化.md`
4. `00-当前架构事实/核心问题诊断/ISSUE-009-RuntimeCoreFrameBackbone缺失.md`
5. `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeFrameBackboneRebindContract.cs`

目标 / 目的：

1. direct `BEffectCommand` 能生成 `BInstantEffectSpec`。
2. simple instant modifier 能在新 stream 内解析 constant / SetByCaller magnitude，并直接更新目标 ASC attribute。
3. attribute 写入同时产生 `BAttributeDelta`，再投影为 `BTypedSimulationFact`。
4. direct command 主链不创建 `CApplyGameplayEffectRequest`，也不创建 `CEffectSpecData` / `CEffectLifecycle` runtime GE entity。
5. attribute typed fact 至少能投影到旧观察链路和 Presentation / Replay native consumer，保证迁移期可观察 AM3 simple instant attribute fact 且避免同源重复投影。

非目标：

1. 不迁移所有旧 producer。
2. 不重写 Active Effect Store。
3. 不继续推进 AutoChess 业务拆分。
4. 不恢复 OOP 生命周期。

执行范围：

1. `Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs`
2. `Assets/GAS/Runtime/System/Effect/SEffectCommandSpecStreamPhases.cs`
3. `Assets/GAS/Runtime/System/Effect/GameplayEffectRequestWriter.cs`
4. `Assets/GAS/Runtime/System/Ability/SAbilityCommit.cs`
5. `Assets/GAS/Runtime/Ability/AbilityRuntimeActions.cs`
6. `Assets/GAS/Runtime/Ability/TimelineAbility/TimelineApplyEffectsProducer.cs`
7. `Assets/_Test/GAS/Runtime/Effect/EffectCommandSpecStreamContractTests.cs`
8. `Assets/_Test/GAS/Runtime/Effect/GameplayEffectRequestWriterTests.cs`
9. `Assets/_Test/GAS/Runtime/Ability/AbilityRuntimeActionsTests.cs`
10. `Assets/_Test/GAS/Runtime/Ability/TimelineApplyEffectsProducerTests.cs`
11. `Assets/_Test/GAS/Runtime/Ability/AbilityTimelineActionSystemTests.cs`
12. 相关任务树、目标态 Spec、当前事实和验证摘要。

执行细则：

1. SystemGroup：必须按 AM2B phase contract 绑定到 `CommandIngest / SpecEvaluation / DeltaApply / TypedFactProjection`；当前真实系统仍挂在旧 `GASCommandGroup`，但行动报告必须说明是否继续作为 contract-first 迁移或是否搬迁真实 group。
2. ISystem / job 形态：当前先用 `ISystem` 主线程 EntityManager 路径证明语义；后续 AM5 / Burst 优化再拆 job 化。
3. 结构变化边界：direct command 主链不做 per-hit request entity / runtime GE entity create-destroy；如涉及 structural mutation request，必须 route 到 AM2B-D structural playback gate，不得绕过 `GasStructuralPlaybackSystemGroup` 契约。
4. DynamicBuffer：当前以 singleton owner 上的 `BEffectCommand` / `BInstantEffectSpec` / `BAttributeDelta` / `BTypedSimulationFact` 承载 frame-local 主链数据，但 AM2B-F 已要求不得把 singleton DynamicBuffer 描述为 scale-ready。
5. Debugger：AM2B-E counters / AM2B-F rebind contract 是 AM3 交还证据源；AM3 必须说明 frame budget、stream owner、deterministic merge、structural gate 和 Debugger evidence gate 如何被复用。
6. Command 四分层：本任务行动报告必须区分 Boundary request、Core frame command、parallel fan-in stream、structural mutation request，并说明当前迁移 producer 属于哪一类。
7. API 选型：本任务必须先说明 AM3 是否继续使用 singleton DynamicBuffer、是否切到 `NativeStream` / per-owner buffer、是否需要 ECB `AppendToBuffer`，并给出拒绝其他候选 API 的理由。
8. 适用规则：`SYS-01`, `SYS-04`, `SC-01`, `BUF-01`, `BUF-02`, `BUF-03`, `BUF-04`, `DBG-01`, `DEF-01`, `DEF-02`, `QRY-01`, `SEL-01`, `SEL-02`, `SEL-09`。

本轮 AM3 进展：

1. `CEffectCommandSpecStream` 增加 spec build、active mutation、delta apply、fact projection、event bridge 和 cue projection 消费游标，并提供 frame-local buffer 清理。
2. `AppendCommand` 补齐 frame、target fallback、context / sequence 和 SetByCaller range 写入。
3. `SInstantEffectSpecBuild` 已能消费 direct instant command，按 simple instant definition 生成 `BInstantEffectSpec`。
4. `SAttributeDeltaApply` 已能解析 constant / SetByCaller magnitude，更新目标 `BAttribute`，并写入 `BAttributeDelta`。
5. `STypedSimulationFactProjection` 已能把 attribute delta 投影为 `BTypedSimulationFact`。
6. 新增 AM3 行为测试覆盖 constant modifier、SetByCaller magnitude、spec sequence 回写、attribute 更新、delta/fact 生成，以及不创建 request/runtime GE entity。
7. `GameplayEffectRequestWriter` 新增 `AppendSimpleInstantCommandOrCreateSingleTargetRequest` / `TryAppendSimpleInstantCommand`，simple instant 命中时写入 `BEffectCommand` stream，未命中时保留旧 request fallback。
8. `SAbilityCommit` 的 activation self / target simple single-target producer 已改为优先写入 `EffectCommand` stream。
9. `CanApplySimpleInstantSpec` 已允许 simple instant Cue-on-Apply；`SInstantEffectSpecBuild` 把 `CGameplayEffectCueRequestOnApply.CueCode` 写入 `BInstantEffectSpec.CueRequestOnApplyCode`。
10. 新增 `SInstantEffectCueRequestProjection`，在 `STypedSimulationFactEventBridge` 后按 `CueProjectionSpecCursor` 增量投影旧 `BCueRequest` / `BGameplayEvent(CueRequested)`。
11. `AbilityRuntimeActions.RequestCostGameplayEffect` 已改为优先通过 `AppendSimpleInstantCommandOrCreateSingleTargetRequest` 写入 self `BEffectCommand`；simple instant cost 不再创建 `CApplyGameplayEffectRequest` 和 runtime GE entity。
12. cost GE 若需要 Duration / Period / Stack、Tag requirements、Granted state 或其它复杂语义，仍回落旧 request entity；simple instant Cue-on-Apply 已走 stream + projection；`RequestCooldownGameplayEffect` 保持旧 request 路径，等待 AM5 / ActiveEffectStore 生命周期迁移。
13. `AbilityRuntimeActionsTests` 已覆盖 cost self simple instant command、cue-on-apply stream projection、cost 通过 `GASCommandGroup` 生成 spec / delta / typed fact，以及 ability commit 成功时 cost 直接扣减 attribute、不再产生 cost runtime GE entity。
14. `TimelineApplyEffectsProducer` 的 single-target `ApplyEffects` 已改为优先写入 `BEffectCommand`；命中 AM3 simple instant 条件时返回 `Entity.Null`，不向 `createdRequests` 暴露 request entity。
15. `GameplayEffectRequestWriter` 已新增 multi-target all-or-fallback writer；Timeline ApplyEffects 多目标 simple instant 会按目标 fan-out 为多条 `BEffectCommand`，复杂 Duration / Period / Stack、Tag requirements、Granted state 和其它复杂语义仍由旧 request 管线承接。
16. `TimelineApplyEffectsProducerTests` 与 `AbilityTimelineActionSystemTests` 已覆盖 CatchSelf / CatchTarget single-target simple instant 写 stream、multi-target simple instant fan-out、Cue-on-Apply projection，以及 `GASCommandGroup` 内同帧生成 spec / delta / typed fact 并直接更新目标 attribute。
17. `STypedSimulationFactEventBridge` 已接入 `GASCommandGroup`，在 `STypedSimulationFactProjection` 后按 `EventBridgeFactCursor` 增量消费 `BTypedSimulationFact(AttributeBaseValueChanged)` 并投影旧 `BAttributeChangeEvent`，复用现有 Presentation / Replay observation 链。
18. `EffectCommandSpecStreamContractTests` 覆盖 bridge / cue projection cursor、调度契约、layout entry，以及 direct simple instant command 经 `GASCommandGroup` 只投影一次 attribute event / cue request。
19. `GameplayEffectRequestWriterTests` 已把旧 cue fallback 用例迁移为 stream + projection 断言，确保 simple instant Cue-on-Apply 不再创建 `CApplyGameplayEffectRequest` / runtime GE entity。
20. `SPresentationOutboxProjection` / `SDebugReplayLogProjection` 已直接消费 `BTypedSimulationFact(AttributeBaseValueChanged)`，无 legacy `BAttributeChangeEvent` 时也能输出 Presentation / Replay attribute change。
21. `BAttributeChangeEvent.SourceFactSequence` 已标记 legacy bridge 来源 fact，Presentation / Replay native consumer 会跳过同源 legacy duplicate。
22. `GASRuntimeQueryLayoutPlan` 的 `ObservationReplayAndOutbox` entry 已显式声明 optional `TypedSimulationFactBuffer`，把 AM3 typed fact native observation consumer 纳入 query layout 证据。
23. `BTypedSimulationFact(CueRequested)` 已承载 simple instant Cue-on-Apply，`EventCode` 表示 `EGameplayCueEvent.OnApply`，`ReasonCode` 表示 `CueRequestOnApplyCode`。
24. `SPresentationOutboxProjection` / `SDebugReplayLogProjection` 已直接读取 `BTypedSimulationFact(CueRequested)` 并投影 `BPresentationEvent(CueRequest)` / `BDebugReplayEvent(CueRequest)`，旧 `BCueRequest` / `BGameplayEvent(CueRequested)` 通过 `SourceFactSequence` 跳过同源重复投影。
25. `CommandEventBridgeContractTests` 已覆盖无 legacy cue request 时 Presentation / Replay 仍能观察 cue fact，以及 legacy cue duplicate skip；`EffectCommandSpecStreamContractTests` 已覆盖 instant command cue fact 与 legacy bridge 的 `SourceFactSequence` 关联。
26. `SPresentationOutboxProjection` / `SDebugReplayLogProjection` 已对 attribute / cue 之外的 typed gameplay fact 提供 generic fallback，输出 `BPresentationEvent(GameplayEvent)` / `BDebugReplayEvent(GameplayEvent)`。
27. Generic typed gameplay fact 的 `EventCode` 优先使用 `BTypedSimulationFact.EventCode`，否则回退 `GameplayEffectCode`；同源 legacy `BGameplayEvent` 通过 `SourceFactSequence` 跳过，避免 Observation 重复投影。
28. `CommandEventBridgeContractTests` 已覆盖无 legacy gameplay event 时 Presentation / Replay 仍能观察 generic typed gameplay fact，以及 legacy gameplay event duplicate skip。
29. Damage typed fact 已能直接投影为 Presentation / Replay Damage 输出；旧 `BDamageEvent` 通过 `SourceFactSequence` 跳过同源重复投影。
30. `GameplayEffectRequestWriterTests` 已覆盖 multi-target simple instant fan-out 生成 2 条 command / spec / delta / typed fact，并覆盖复杂 GE fallback 时不产生 partial command。
31. `RuntimeCoreFreezeSafetyGateTests` 已锁定 `TryAppendSimpleInstantCommands` / `AppendSimpleInstantCommandsOrCreateTargetListRequest`，防止 simple instant 多目标默认回退 legacy lifecycle。
32. `EffectCommandSpecStream.CommandWriter` 已接入 `TryAppendSimpleInstantCommands`：all-or-fallback 预检成功后只解析一次 stream entity / current frame / command buffer / SetByCaller buffer，再批量 append 并一次 flush stream counters。该路径只收缩 multi-target simple instant producer 的 per-command query 放大；单条 `AppendCommand` 的 `TryGetSingleton` 仍按 `ISSUE-011` 后续处理。
33. `ResolveCurrentFrame` 的 5 处重复实现已收口到 `GASRuntimeFrameContext`：`GasRuntimeDebugger.ResolveCurrentFrame` 保留兼容 wrapper，`EventBusHelper`、`SPresentationOutboxProjection`、`SDebugReplayLogProjection`、`EffectCommandSpecStream` 改为调用统一 helper；`GASRuntimeFrameContext` fallback `GlobalTimer` 临时 query 已删除，当前 frame 仍由 `GASManager.EntityGlobalTimer` known-owner main-thread read 提供，EventBus / Debugger / Presentation / Replay 其它 query 仍未闭合。

已完成轮次（11轮 AM3 行动报告）已迁出到 `迭代记录/`。各轮原始内容（API选型表、官方文档覆盖检查、GAS概念校准、Command四分层、官方案例对照、PackageCache证据）保留在 git 历史中。

验收标准：

1. direct simple instant command 通过 `GASCommandGroup` 后产生 1 条 spec、1 条 delta、1 条 typed fact。
2. target attribute base/current 值按 modifier 更新，并保留 dirty / previous current value 标记。
3. SetByCaller magnitude 能从 command stream range 解析，且 `BEffectCommandSetByCallerValue.SpecSequence` 回写到生成 spec。
4. 同 effect code 不产生 `CApplyGameplayEffectRequest`，也不产生 runtime `CEffectSpecData` / `CEffectLifecycle`。
5. attribute typed fact 能投影到旧 `BAttributeChangeEvent`，重复更新不重复投影同一 fact。
6. simple instant Cue-on-Apply 能投影到旧 `BCueRequest` / `BGameplayEvent(CueRequested)`，重复更新不重复投影同一 spec。
7. Unity Runtime tests 可运行时通过；如 LicensingClient 阻塞，记录 return code 和关键日志。

测试链路：

1. `git diff --check`
2. `rg -n "GameplayEffectRequestWriter\." Assets/AutoChessDemo -g "*.cs"`
3. `rg -n "TryApplyFastInstantModifier|ApplyFastOrCreateSingleTargetRequest|FastInstant|FastDirect" Assets/GAS/Runtime Assets/AutoChessDemo Assets/_Test/GAS/Runtime -g "*.cs"`
4. Runtime EditMode tests；如 Unity LicensingClient 阻塞，记录为环境阻塞。
5. 辅助 dotnet 近似编译只允许临时纳入 Unity 生成 csproj 缺失的新文件，完成后必须还原 csproj。
6. 若辅助 runtime tests 编译被 AutoChessDemo 迁移残留挡住，只记录边界，不在 AM3 中修复 AutoChessDemo。

## 三级任务：Active Effect Store 重建

任务ID：`T1-RuntimeCore-AM5`

状态：`进行中（owner-local store 第一刀、Debugger slot pressure baseline、period / overflow simple instant derived command proof 已落地；后续扩张必须复用 AM2B rebind contract，Unity验证待补跑）`

任务名：`GAS ECS Runtime - Runtime Core 重构 - Active Effect Store 重建`

当前问题：

1. Duration / Stack / Period / Granted state 仍主要依赖 runtime GE entity lifecycle 和 `BGameplayEffect` 目标 buffer。
2. 如果直接把所有 active effect 迁移成 entity 或全部塞入 ASC buffer，会违反 `05-ActiveEffectStoreSpec` 的分层选型要求。
3. 普通生命周期状态切换不能通过 hot path add/remove component 表达，否则会扩大 ISSUE-004 的结构变化风险。

目标态参考：

1. `01-目标态架构共识/05-ActiveEffectStoreSpec.md`
2. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
3. `UnityDOTS官方文档参考/主题/01-Entities系统与World.md`
4. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`
5. `UnityDOTS官方文档参考/README.md`
6. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`

当前事实参考：

1. `00-当前架构事实/核心问题诊断/ISSUE-001-GE生命周期管线过重.md`
2. `00-当前架构事实/核心问题诊断/ISSUE-004-结构变化边界脆弱.md`
3. `00-当前架构事实/当前未闭合点.md`
4. `00-当前架构事实/核心问题诊断/ISSUE-009-RuntimeCoreFrameBackbone缺失.md`
5. `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeFrameBackboneRebindContract.cs`

目标 / 目的：

1. 先建立 `OwnerLocalStore`：ASC owner 上的 `CActiveEffectStore` + `BActiveEffectSlot` 是当前第一落点。
2. duration runtime GE entity 生命周期可以镜像到 owner slot；period due / overflow simple instant child GE 优先派生为 EffectCommand，后续再逐步迁移复杂 active child GE、stack mutation、granted cleanup 和 store-driven lifecycle。
3. hot path 缺少 store 时必须返回失败，不允许 helper 隐式创建组件或 buffer。
4. Query layout / contract tests 必须能审查 ActiveEffectStore 是 owner-local target contract，而不是结构变化热点。

非目标：

1. 不在第一刀替换完整 duration GE apply/remove。
2. 不移除 `BGameplayEffect`。
3. 不重写 granted tag / ability cleanup。
4. 不推进 AutoChess 业务拆分。

执行范围：

1. `Assets/GAS/Runtime/Effect/Component/Dynamic/CActiveEffectStore.cs`
2. `Assets/GAS/Runtime/AbilitySystem/AbilitySystemEntityFactory.cs`
3. `Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs`
4. `Assets/GAS/Runtime/System/Effect/GameplayEffectRequestWriter.cs`
5. `Assets/GAS/Runtime/System/Effect/SEffectTick.cs`
6. `Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs`
7. `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeQueryLayoutPlan.cs`
8. `Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs`
9. `Assets/_Test/GAS/Runtime/Effect/ActiveEffectStoreTests.cs`
10. `Assets/_Test/GAS/Runtime/Effect/StackingRuntimeTests.cs`
11. `Assets/_Test/GAS/Runtime/Event/RuntimeQueryLayoutPlanTests.cs`
12. `Assets/_Test/GAS/Runtime/Debugger/GasRuntimeDebuggerTests.cs`

执行细则：

1. Store 第一刀只采用 `OwnerLocalStore`，即 ASC `DynamicBuffer` slot；stable active effect entity、Cleanup Component、Chunk Component 进入后续小闭环评估，且不得把 legacy-backed owner-local mirror 描述为 scale-ready。
2. `BActiveEffectSlot` 只镜像跨帧状态摘要和 owner/source/context，不把 definition 与 runtime timer 混写。
3. `PendingApply / Active / Inhibited / PendingRemove` 先用 enum state 表达，避免普通状态切换触发 archetype churn。
4. helper 不负责补 `CActiveEffectStore` 或 `BActiveEffectSlot`，结构创建只允许 ASC factory / 低频 bootstrap 处理。
5. AM5 行动报告必须区分 OwnerLocalStore、GlobalIndexedStore、LifecycleCleanupStore、ChunkSkipIndex 四类 store surface，并说明当前任务继续扩展哪一层和为何不是其他三类。
6. cleanup / grant / remove 相关结构变化必须 route 到 AM2B-D structural playback gate；ActiveEffectLifecycle 只记录 mutation intent，StructuralPlayback 统一 playback。
7. period due / overflow simple instant child GE 已进入 `EffectCommand` proof 主链；复杂 child GE、granted tag / ability cleanup path 和 store-driven lifecycle 收缩仍是后续 AM5 小闭环。
8. slot pressure Debugger baseline 已补，compact / cleanup / chunk skip 指标随对应 store 层继续扩展。
9. 适用规则：`SYS-02`, `SYS-03`, `SYS-04`, `QRY-01`, `SC-01`, `ECB-01`, `ECB-03`, `BUF-01`, `BUF-02`, `BUF-04`, `NAT-02`, `NAT-03`, `FSM-02`, `FSM-06`, `PRF-01`, `PRF-02`, `PRF-05`, `PRF-10`, `PRF-13`, `PRF-14`, `SEL-01`, `SEL-02`, `SEL-04`, `SEL-05`, `ODF-01`, `ODF-02`, `ODF-07`, `ODF-09`, `ODF-10`, `ODF-11`。

本轮 AM5 进展：

1. 新增 `CActiveEffectStore`、`BActiveEffectSlot`、slot state / flags 和 `ActiveEffectStore` helper。
2. ASC 创建路径默认添加 owner-local store 和 slot buffer；slot buffer 使用小内联容量，逻辑上仍以 `ASC_MAX_GAMEPLAY_EFFECT_COUNT` 作为上限。
3. 旧 duration lifecycle 的 Activate / Inhibit / Reactivate / PendingRemove / RefreshDuration / SetStackCount / Remove 已镜像到 owner slot。
4. Query layout 新增 `ActiveEffectStore` entry，标记 `TargetContract`、`NoPerHitStructuralChange` 和 `DynamicBufferMutation`。
5. 新增测试覆盖 store 创建、hot path 不补结构变化、duration lifecycle mirror 和 query layout contract。
6. RuntimeCoreDebugger 已输出 owner-local store owners、slot count、capacity、state distribution、legacy-backed 和 externalized owner baseline。
7. `EffectRuntimeUtility.CreateDerivedApplyRequest` 对 period / overflow 派生 GE 先尝试写 simple instant `BEffectCommand`；失败才创建旧 request。
8. `GameplayEffectRequestWriter` 支持指定 `EEffectCommandSource`，并允许从派生 runtime GE 的 `BSetByCallerValue` 复制 SetByCaller range。
9. `EEffectCommandSource` 新增 `Overflow`，period / overflow 派生命令可被 Debugger / battle hash 后续区分来源。
10. `ActiveEffectStore.TryRefreshPeriodFrame` 会在 period cursor 更新后刷新 owner-local slot 的 `PeriodFrame` / `LastPeriodFrame`。
11. `StackingRuntimeTests` 覆盖 period simple instant child GE 写 command、复制 SetByCaller、同步 slot cursor，并经 command group 生成 spec / delta / typed fact。

验收标准：

1. ASC factory 创建的 ASC 必有 `CActiveEffectStore` 和 `BActiveEffectSlot`。
2. duration GE 激活、抑制、恢复、待移除和清理能同步 owner slot。
3. 缺少 store 的目标不会在 hot path 被自动补结构变化。
4. Query layout 明确 ActiveEffectStore 不属于 `StructuralEntityManagerHotspot`。
5. Debugger 能输出 owner-local slot pressure 和 legacy mirror 证据。
6. period due simple instant child GE 不创建 apply request / runtime child GE entity，能进入 `BEffectCommand -> BInstantEffectSpec -> BAttributeDelta -> BTypedSimulationFact`。
7. Unity Runtime tests 可运行时通过；如 LicensingClient 阻塞，记录 return code 和关键日志。

测试链路：

1. `git diff --check`
2. `rg -n "GameplayEffectRequestWriter\." Assets/AutoChessDemo -g "*.cs"`
3. `rg -n "TryApplyFastInstantModifier|ApplyFastOrCreateSingleTargetRequest|FastInstant|FastDirect" Assets/GAS/Runtime Assets/AutoChessDemo Assets/_Test/GAS/Runtime -g "*.cs"`
4. `rg -n "PeriodSimpleInstantDerivedEffectWritesEffectCommandAndUpdatesStoreCursor|TryRefreshPeriodFrame|EEffectCommandSource\\.Overflow|TryAppendDerivedEffectCommand" Assets/GAS/Runtime Assets/_Test/GAS/Runtime`
5. Runtime EditMode tests；如 Unity LicensingClient 阻塞，记录为环境阻塞。
6. 辅助 dotnet 近似编译只允许临时纳入 Unity 生成 csproj 缺失的新文件，完成后必须还原 csproj。
7. 若辅助 runtime tests 编译被 AutoChessDemo 迁移残留挡住，只记录边界，不在 AM5 中修复 AutoChessDemo。

## 三级任务：Freeze Safety Gate

任务ID：`T1-RuntimeCore-AM0`

状态：`契约已确立`

任务名：`GAS ECS Runtime - Runtime Core 重构 - Freeze Safety Gate`

当前问题：

1. 当前旧 GE lifecycle pipeline 仍承担过多 simple instant GE、runtime entity、event projection 和 structural change 成本。
2. 历史上继续加 fast path 没有解决 x50 曲线失真，只是在旧管线上继续补丁。
3. 新业务如果继续接入旧 instant GE entity lifecycle，会扩大后续迁移面。

目标态参考：

1. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
2. `01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
3. `01-目标态架构共识/90-目标态不变量.md`

历史方案参考：

1. `../../历史方案参考/方案14.md`、`方案15.md` 的自走棋验收和四层模型可参考。
2. 其中托管 EventBus、Debugger 作为 simulation 路由、SourceGenerator 生成 lifecycle 的示例不可照搬。

目标 / 目的：

1. 冻结旧 GE lifecycle fast path 扩张。
2. 标记旧 instant GE entity lifecycle 为待迁移路径。
3. 新增业务不得继续依赖 global observation stream 作为 high-frequency reaction 主输入。
4. 为 `AM-1/AM-2` 提供稳定迁移边界。
5. 新增或外部保留入口必须用 `LegacyInstant...Bypass` 或 `EffectCommand / InstantEffectSpec / AttributeDelta` 术语表达职责，禁止继续使用 `FastInstant` 作为目标态命名。

非目标：

1. 不在本任务迁移所有 GE。
2. 不改写完整 Attribute pipeline。
3. 不新增更多 AutoChess 业务机制。

执行范围：

1. Runtime Core 文档和契约测试。
2. 旧 pipeline 入口的注释、guard、测试命名和任务看板。
3. 涉及目录优先在 `Assets/GAS/Runtime/System/Effect`、`Assets/GAS/Runtime/Effect`、`Assets/_Test/GAS/Runtime` 中定位。

执行细则：

1. 命名必须使用 `EffectCommand / InstantSpec / AttributeDelta / ActiveEffectStore` 目标态术语。
2. 不引入新的 managed event bus。
3. 不新增绕过 `GASSystemScheduleContract` 的系统调度方式。
4. 如发现旧路径新增入口，必须写入禁止方向或契约测试。

验收方式：

1. 文档明确旧 instant GE entity lifecycle 是待迁移对象。
2. `02` 看板明确后续 AM1/AM2 顺序。
3. 新任务无法在未说明理由的情况下继续扩展旧 fast path。

测试链路：

1. 文档检查：`rg "fast path|旧 GE lifecycle|EffectCommand" 当前路线`。
2. 若改代码，按影响范围运行 runtime / runtime tests build。

交还内容：

1. 更新 `00-当前架构事实/核心问题诊断.md` 中 AM0 相关事实。
2. 更新本任务状态。
3. 若新增契约测试，记录验证命令到 `迭代记录/`。

本轮 AM0 进展：

1. 新增 `GameplayEffectRuntimePipelineContract`，将 `LegacyInstantEntityLifecycle` 标记为 `LegacyFrozen`、`MigrationOnly`、`NoNewFeatureExpansion`、`NotDefaultNewBusinessPath`。
2. 将旧 instant direct bypass API 命名收敛为 `TryApplyLegacyInstantModifierBypass` / `ApplyLegacyInstantBypassOrCreateSingleTargetRequest`。
3. 新增 `GameplayEffectLegacyBridge` 作为公开 migration-only 桥接，避免 Demo assembly 直接访问 Runtime internal writer，同时不把它塑造成目标态新入口。
4. 新增 `RuntimeCoreFreezeSafetyGateTests`，用契约测试锁定旧路径冻结和目标态 `EffectCommand / InstantEffectSpec / AttributeDelta / TypedSimulationFact` 术语。
5. `git diff --check` 通过；Unity Runtime tests 因 LicensingClient IPC 超时退出，未进入编译阶段。
