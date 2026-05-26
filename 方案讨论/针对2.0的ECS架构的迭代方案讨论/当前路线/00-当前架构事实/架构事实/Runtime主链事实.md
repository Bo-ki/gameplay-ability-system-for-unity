# Runtime 主链事实

> 上次更新：2026-05-26 | 审查范围：Assets/GAS/Runtime ~223 个 C# 文件，39 个 ISystem

## 已成立事实

### 架构骨架

1. `GASManager.cs`(130行) 是纯 ECS 启动器——创建专属 `World("EX_GAS_World")`、`FixedStepSimulationSystemGroup`、`EndSimulationEntityCommandBufferSystem`，通过 `GASSystemScheduleContract.CreateFixedStepGroups` 构建 9 个 SystemGroup 并注册所有 System。
2. `GASGroups.cs`(101行) 定义 9 个 SystemGroup（按 UpdateAfter 顺序）：
   `GASCommandGroup` → `GASResetDirtyGroup` → `GASTagGroup` → `GASEffectGroup` → `GASAttributeGroup` → `GASAbilityGroup` → `GasStructuralPlaybackSystemGroup` → `GASCueGroup`
   外加 `GASExecutionCalculationExtensionGroup`（挂在 CommandGroup 内部）。
3. `GasStructuralPlaybackSystemGroup` 内包含 `GasEndStructuralEcbSystem : EntityCommandBufferSystem`——这是目标态结构变化集中 playback 的唯一合法出口（但当前尚未强制执行）。
4. `GASSystemScheduleContract.cs`(447行) 完整定义 **8-phase 管线契约**：
   `FramePrepare → CommandIngest → SpecEvaluation → ActiveEffectLifecycle → DeltaApply → TypedFactProjection → StructuralPlayback → ObservationProjection`
   每个 phase 定义了 reads/writes/structuralPermission/observationBoundary。
5. 但 8-phase 契约与 9 个 SystemGroup 的映射关系是**逻辑声明式**的——真实 System 的 phase 归属通过 `GASRuntimeCoreFramePhaseSystemContract` 数组声明（8 个 system），与 `EffectSystems`/`AbilitySystems` 等旧分组数组并存。旧分组仍然用 `GASEffectGroup`/`GASAbilityGroup` 等旧 group 名称注册 system，未按 phase 重命名。

### System 与 Job 化状态

6. 全部 System 已迁移为 `partial struct : ISystem`（39 个 ISystem，零 SystemBase 遗留）。
7. **IJobEntity 使用**: 2 个 System——`SAbilityTick`、`SAttributeRecalculate`（均有 `[BurstCompile]`）。
8. **IJobChunk 使用**: 3 个 System——`SEffectTick`（内部的 `CollectDurationEffectCandidatesJob` + `RefreshOwnerLocalChunkSkipIndexJob`）、`SAbilityTick`、`SAttributeRecalculate`。
9. **ToEntityArray 仍存在于 22 个文件中**——包括 4 个核心热路径：`SAbilityCommit`、`SEffectApply`、`SEffectTick`（用于处理排序后的候选队列）、`SApplyGameplayEffectRequest`。
10. **直接 EM 结构变化仍存在于 24 个文件中**（`em.SetComponentData`/`em.AddComponentData`/`em.DestroyEntity`/`em.AddComponent`/`em.RemoveComponent`）。

### Effect Command / Spec Stream 新管线（AM2/AM3/AM5）

11. `CEffectCommandSpecStream` 是全局单例 singleton component，承载 6 个 DynamicBuffer：
    `BEffectCommand`、`BInstantEffectSpec`、`BAttributeDelta`、`BTypedSimulationFact`、`BEffectCommandSetByCallerValue`、`BActiveEffectMutation`。
    通过 `GASManager.EntityEffectCommandSpecStream` 全局引用。
12. `SEffectCommandSpecStreamPhases.cs`(619行, 6 个 partial ISystem) 实现基于**游标(cursor)的索引 for 循环**遍历 DynamicBuffer——是当前代码库中**新代码的正面范例**：
    - `SEffectCommandIngest` — 消费 BEffectCommand 写入 BInstantEffectSpec（Simple Instant 路径）
    - `SInstantEffectSpecBuild` — 从 Command 构建 Spec
    - `SActiveEffectMutationApply` — 标记 ActiveEffect store 变更
    - `SAttributeDeltaApply` — 从 Spec 计算属性 Delta
    - `STypedSimulationFactProjection` — 从 Delta 投影 TypedSimulationFact
    - `STypedSimulationFactEventBridge` — 增量桥接到旧 EventBus
    - `SInstantEffectCueRequestProjection` — 从 Spec 投影 Cue Request
    所有 6 个 phase system 均为 **零 ToEntityArray、零 ECB 碎片化、cursor 驱动** 的主线程 for 循环（未 job 化但结构清晰）。

### Typed Simulation Fact 桥接模式

13. `BTypedSimulationFact` → `STypedSimulationFactProjection` → `STypedSimulationFactEventBridge` → 旧 `CGameplayEventBus` / `Presentation` 双出口。
    该模式**清晰分离了模拟事实与表现投影**，且 Presentation/Replay 已能直接消费 TypedFact 并跳过同源 legacy duplicate（通过 `SourceFactSequence` 去重）。

### ActiveEffectStore（AM5）

14. `CActiveEffectStore` 是 ASC owner-local 的 IComponentData + `BActiveEffectSlot` DynamicBuffer，将旧 duration runtime GE entity 的 Active/Inhibited/PendingRemove/Remove 状态镜像到 owner 本地。
15. AM5 period due / overflow simple instant child GE 已优先写入 `BEffectCommand`(Source=Period/Overflow)，并同步刷新 owner-local `LastPeriodFrame`。路径标记为 **legacy entity-backed mirror + proof-only command stream**。

### Event / Observation 层

16. `EventBusHelper.cs`(391行) 已移除全部 `[ThreadStatic]` 字段，改用 `GameplayEventBusWriter` 显式 struct + `Create`/`Dispose` 生命周期。
17. `SPresentationOutboxProjection`(465行) 和 `SDebugReplayLogProjection`(437行) 已改用索引 for 循环遍历 DynamicBuffer，不再使用 ToEntityArray。
18. `CGameplayEventBus` 仍作为全局 singleton entity 承载 6 个 DynamicBuffer（`BGameplayEvent`、`BAttributeChangeEvent`、`BDamageEvent`、`BTagChangeEvent`、`BCueRequest`、`BPresentationOutboxOwner`）。

### Definition / 配置链

19. `GASDefinitionTable` 已统一 Ability/GE/Attribute/Tag/Cue summary contract。
20. `GASDefinitionGeneratedAdapter` 把 generated/Luban source 映射到 unified definition table。
21. `GASGeneratedDefinitionBakingPlan / BakeContract / BakePipeline / RuntimeIntegrationPlan` 已形成 contract 链。
22. `GameplayEffectConfigRegistry` 是 GE definition cache lifecycle owner（BlobAsset 管理）。

### 外部接入

23. 外部接入以 `AbilitySystemBinding + AbilitySystemFacade` 为主；写操作通过 request entity 进入 ECS。
24. Runtime driver（如 `SHeadlessAutoChessDriver`）可读取 ECS state/buffer/tag/attribute，并通过 request entity 输出意图。

## 当前风险与 DOTS 合规缺口

以下风险对照 Unity DOTS 官方文档参考规则体系（`SYS`/`PRF`/`JOB`/`QRY`/`SC`/`ECB`）。

### P0 致命: 热路径全主线程 foreach (22 个文件)

| 规则 | 违反表现 |
|------|---------|
| `PRF-05` Hot Path 禁止主线程遍历 | 22 个文件使用 `_query.ToEntityArray(Allocator.Temp)` + `foreach` |
| `JOB-01` 并行批处理说明选择理由 | 仅 2 个 System 使用 IJobEntity（SAbilityTick, SAttributeRecalculate） |
| `QRY-01` Hot path 优先 job 化 | 核心热路径 `SAbilityCommit`、`SEffectApply`、`SApplyGameplayEffectRequest` 全线主线程 |

**受影响核心系统**:
- `SAbilityCommit.cs`(515行) — `_query.ToEntityArray` + `foreach (var ability in abilities)`；内部混合 ECB 和直接 `em.SetComponentData`
- `SEffectApply.cs`(174行) — `_effectQuery.ToEntityArray` + `foreach`；每 entity 路径中多次 `EffectRuntimeUtility.PlaybackAndReset`
- `SEffectTick.cs`(689行) — 已部分 job 化（2 个 IJobChunk 收集候选），但 Tick 处理仍在主线程 `for` 循环中逐 entity 处理
- `SApplyGameplayEffectRequest.cs`(881行) — `_query.ToEntityArray` + `foreach`；旧 legacy instant modifier bypass 路径与完整 entity lifecycle 路径并存

### P0 致命: 热路径直接 EntityManager 结构变化 (24 个文件)

| 规则 | 违反表现 |
|------|---------|
| `PRF-02` 禁止 Hot Path 直接执行结构变化 | 24 个文件中存在 `em.SetComponentData`/`em.AddComponentData`/`em.DestroyEntity` |
| `SC-01` Hot path 不直接结构变化 | 结构变化散落在 AbilityRuntimeActions、GameplayEffectEntityFactory、EffectRuntimeUtility 等 |
| `ECB-01` ECB 是延迟结构变化工具 | `SApplyGameplayEffectRequest` 在 foreach 中直接 `em.DestroyEntity(requestEntity)` |

**典型违规位置**:
- `SApplyGameplayEffectRequest.cs` — foreach 内 `em.DestroyEntity(requestEntity)`、`em.SetComponentData`/`em.AddComponentData`
- `SAbilityCommit.cs` — foreach 内 `em.SetComponentData(ability, runtime)`
- `AbilityRuntimeActions.cs` — `entityManager.AddComponentData(ability, new CAbilityInTryEnd{...})`
- `GameplayEffectEntityFactory.cs` — `entityManager.AddComponent<CEffectPendingApply>(entity)` + `AddComponentData` × 3

### P0 致命: ECB Playback 碎片化

| 规则 | 违反表现 |
|------|---------|
| `ECB-01` ECB 不是 gameplay event bus | `EffectRuntimeUtility` 中 15+ 便捷重载创建临时 ECB → 立即 Playback → Dispose |
| `ECB-03` ECB playback 位置属于明确 SystemGroup phase | Playback 散落在 helper 方法中，每次都是独立 sync point |
| `PRF-04` 结构变化必须集中到单一 ECB Playback Phase | `GasStructuralPlaybackSystemGroup` 已声明但**当前未强制执行** |

**典型反模式**（EffectRuntimeUtility.cs, 2125行）:
```csharp
// 15+ 个方法遵循此反模式
var ecb = new EntityCommandBuffer(Allocator.Temp);
// ... ecb operations ...
ecb.Playback(em);  // 独立 sync point
ecb.Dispose();
```
受影响方法包括 `EnsureLifecycle`、`MarkEffectForRemoval`、`ActivateDurationEffect`、`ApplyInactiveDurationEffect`、`DeactivateOngoingEffect`、`HandleDurationExpired`、`DestroyEffectEntity`、`CreateDerivedApplyRequest`、`TryMergeStackingApplication`、`RemoveActiveGameplayEffectsWithTags` 等。

### P1: SystemGroup 层次与 8-phase 契约未完全对齐

| 规则 | 违反表现 |
|------|---------|
| `SYS-02` SystemGroup 是 phase owner | 旧 SystemGroup 名称（`GASEffectGroup` 等）与 8-phase 名称（`ActiveEffectLifecycle` 等）不匹配 |
| `SYS-03` 系统数量是成本源 | 39 个 ISystem 中仅少数按 phase 契约分组，其余沿用旧分组 |

### P1: EventBus + Stream 双重事件路径

| 规则 | 违反表现 |
|------|---------|
| `BUF-02` 单一全局 buffer 限于 proof/低量 | `CGameplayEventBus` 全局 singleton 承载 6 个 buffer，仍是所有事件的汇聚点 |
| `SEL-01` 数据性质分类优先 | Typed Simulation Fact 与新 EventBus 双路径并存，旧 EventBus 未关停 |

### P1: GasRuntimeDebugger 单体过于庞大

`GasRuntimeDebugger.cs`(2389行) 包含 query 创建、计数器收集、ECB 记录、诊断记录，未按关注点拆分。每帧仍创建多个临时 EntityQuery（违反 `PRF-09`）。

## 当前总诊断

当前不应继续把问题描述为"缺少某个业务机制"。真正的当前问题是：

1. **Runtime Core 主流程尚未从旧 lifecycle / foreach 模式迁移到 phase + stream + IJobChunk 驱动模型。**
   22 个文件仍使用 ToEntityArray + foreach；仅 2 个 System（SAttributeRecalculate、SAbilityTick）使用了 IJobEntity。

2. **结构变化散落在 24 个文件中**，ECB Playback 散布在 EffectRuntimeUtility 的 15+ 便捷重载中，
   `GasStructuralPlaybackSystemGroup` 虽已声明但当前未强制执行结构变化集中化。

3. **SEffectCommandSpecStreamPhases 的 cursor 模式是唯一的新代码正面范例**——
   应推广到 SEffectApply/SEffectTick/SAbilityCommit 等核心热路径，但尚未推广。

4. **8-phase 管线契约已完整定义但尚未真实落地到 SystemGroup**：
   当前 System 仍按旧分组（Command/Effect/Attribute/Ability/Cue）注册，未与 phase 契约对齐。

5. **AM2/AM3/AM5 的局部 proof 只能证明方向正确**，不能证明 Runtime Core 已具备 DOTS scale-ready 执行骨架。

下一步是真实 SystemGroup 搬迁、核心热路径 IJobChunk/IJobEntity 化、结构变化集中到 `GasStructuralPlaybackSystemGroup`、以及 Profiler/Journaling 证据采样。
