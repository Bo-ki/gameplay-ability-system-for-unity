# 代码DOTS合规审查报告

## 审查范围

审查了 Runtime Core 热路径约15个关键文件（~8000行C#代码），覆盖：
- Effect 管线（SEffectApply, SEffectTick, SApplyGameplayEffectRequest, EffectRuntimeUtility, SEffectCommandSpecStreamPhases）
- Ability 管线（SAbilityCommit, STryActivateAbility, AbilityRuntimeActions）
- Event/Stream 系统（EventBusHelper, SEventBusClear, EffectCommandSpecStream, GameplayEffectRequestWriter）
- Observation/Replay（SPresentationOutboxProjection, SDebugReplayLogProjection）
- Store/Debugger/Factory（CActiveEffectStore, GasRuntimeDebugger, GameplayEffectEntityFactory）
- SystemGroup/Contract（GASGroups, GASRuntimeFrameBudgetContract）

## P0 致命缺陷（违反 `PRF-01`~`PRF-05`、`PRF-09`、`SYS-01`、`ECB-01`）

### 缺陷 A: 全系统主线 foreach + EntityManager — `PRF-05` `PRF-02` `JOB-01`

**严重程度**: P0 × 暴露面 = 全部热路径

**代码证据**:

| 文件 | 行号 | 违规模式 |
|---|---|---|
| `SAbilityCommit.cs` | 31,35 | `_query.ToEntityArray(Allocator.Temp)` + `foreach (var ability in abilities)` |
| `STryActivateAbility.cs` | 30,33 | 同上 |
| `SEffectApply.cs` | 全文 | `_effectQuery.ToEntityArray(Allocator.Temp)` + `foreach` |
| `SEffectTick.cs` | 全文 | 同上 |
| `SApplyGameplayEffectRequest.cs` | 全文 | `_query.ToEntityArray(Allocator.Temp)` + `foreach (var requestEntity in requests)` |
| `SEffectCommandSpecStreamPhases.cs` | 全文 | 6个 partial struct 全部 `for (var i = 0; i < buffer.Length; i++)` 主线程遍历 DynamicBuffer |
| `SPresentationOutboxProjection.cs` | 54-67 | 主线程 for 循环遍历 5 种 EventBus buffer |
| `SDebugReplayLogProjection.cs` | 54-67 | 同上 |

**违反规则**:
- `PRF-05`: 热路径必须使用 `IJobEntity`/`IJobChunk` 并行遍历，禁止主线程 foreach
- `PRF-02`: 热路径禁止每 entity 调用 `EntityManager.GetComponentData`/`SetComponentData`
- `JOB-01`: Runtime Core 计算必须在 Burst Job 中执行

**影响**: 当前 x50 AutoChess profile 中 `avgTickMs=13.77ms`（ISSUE-001），全主线 foreach 是主要耗时来源。IJobChunk 可将此降低 5-10x。

---

### 缺陷 B: 热路径直接 EntityManager 结构变化 — `PRF-02` `SC-01` `ECB-01`

**严重程度**: P0

**代码证据**:

| 文件 | 行号 | 违规代码 |
|---|---|---|
| `AbilityRuntimeActions.cs` | 77 | `entityManager.AddComponentData(ability, new CAbilityInTryEnd{...})` — **直接** EM 结构变化！ |
| `AbilityRuntimeActions.cs` | 107 | `entityManager.AddComponentData(ability, new CAbilityInTryCancel{...})` — **直接** EM 结构变化！ |
| `AbilityRuntimeActions.cs` | 138 | `ecb.AddComponent(ability, new CAbilityInTryCancel{...})` — ECB 版本（较好但仍在 foreach 中） |
| `GameplayEffectEntityFactory.cs` | 79-92 | `entityManager.AddComponent<CEffectPendingApply>(entity)` + `AddComponentData` × 3 — 每个 GE 实例化 4 次结构变化 |
| `GameplayEffectEntityFactory.cs` | 121 | `entityManager.AddBuffer<BGrantedTagConfig>(ge)` — 结构变化 |
| `EffectRuntimeUtility.cs` | 1549-1550 | `ecb.AddBuffer<BGrantedAbilityRuntime>(ge)` + 立即 `PlaybackAndReset(ref ecb, em)` — 在 foreach 内创建 sync point |

**违反规则**:
- `PRF-02`: 热路径禁止 `EntityManager` 结构变化操作
- `SC-01`: 结构变化只能在 `GasStructuralPlaybackSystemGroup` 中发生
- `ECB-01`: ECB playback 是结构变化成本，也是同批 GE apply 的语义可见性屏障

---

### 缺陷 C: 热路径 helper 中大量临时 EntityQuery — `PRF-09` `PRF-33`

**严重程度**: P0

**代码证据**:

| 文件 | 行号 | 调用频率 |
|---|---|---|
| `SGlobalTimer.cs` | `GASRuntimeFrameContext` | current-frame helper 已统一；fallback query 已删除，现为 `GASManager.EntityGlobalTimer` known-owner main-thread read |
| `GasRuntimeDebugger.cs` | `ResolveCurrentFrame` wrapper | 每次 diagnostic 记录仍读取 current frame；重复实现已转发到统一 helper |
| `EventBusHelper.cs` | `GASRuntimeFrameContext.ResolveCurrentFrame` | 每次事件入队 / 批量操作仍读取 current frame；重复实现已删除 |
| `SPresentationOutboxProjection.cs` | `GASRuntimeFrameContext.ResolveCurrentFrame` | 每帧仍读取 current frame；重复实现已删除 |
| `SDebugReplayLogProjection.cs` | `GASRuntimeFrameContext.ResolveCurrentFrame` | 每帧仍读取 current frame；重复实现已删除 |
| `EffectCommandSpecStream.cs` | `BeginCommandWriter(em)` | 单条 `AppendCommand` / writer 创建仍读取 current frame；重复实现已删除 |
| `EffectCommandSpecStream.cs` | 217-218 | `TryGetSingleton` — 每次 `EnsureSingleton` 调用！ |
| `GasRuntimeDebugger.cs` | 1458-1459 | `CountEntitiesWith<T>` — 每帧调用 6+ 次（debugger counter 收集） |
| `GasRuntimeDebugger.cs` | 1551-1552 | `ReadActiveEffectStoreCounters` — 每帧调用 |
| `GasRuntimeDebugger.cs` | 1616 | `CountPresentationOutboxEvents` fallback — 按需调用 |

**违反规则**:
- `PRF-09`: hot path 禁止临时创建 query（"Query / Filter / Allocator / Dependency / Chunk layout 是架构输入，不是调优阶段才补的实现细节"）
- `PRF-33`: EntityQuery 必须通过 `SystemState.GetEntityQuery` 创建，禁止 `EntityManager.CreateEntityQuery`
- 不变量 #40: "EntityQuery 创建必须通过 `SystemState.GetEntityQuery`"

**特别严重**: 历史上的 `EffectCommandSpecStream.ResolveCurrentFrame` 和其它 4 处重复 current-frame helper 已收口到 `GASRuntimeFrameContext`，且统一 helper 的 `GlobalTimer` fallback 临时 query 已删除。2026-05-25 已新增 `EffectCommandSpecStream.CommandWriter`，并让 `GameplayEffectRequestWriter.TryAppendSimpleInstantCommands` 多目标 simple instant fan-out 一次解析 stream entity / current frame / buffer 后批量 append，局部消除了该 producer 的 per-command query 放大。但单条 `AppendCommand` 的 `TryGetSingleton`、其它 producer、EventBus、Debugger、Presentation / Replay 的其它临时 query 仍存在；current-frame 仍是 `GASManager.EntityGlobalTimer` static known-owner main-thread read，真实 frame owner 未落地，本缺陷仍保持 P0。

---

### 缺陷 D: ECB.Playback 散布在 helper 方法中造成不可控 sync point — `ECB-01` `PRF-09`

**严重程度**: P0

**代码证据**:

`EffectRuntimeUtility.cs` 中有 **大量的** "创建临时 ECB → 操作 → 立即 Playback → Dispose" 模式：

```csharp
// 模式示例 (EffectRuntimeUtility.cs:21-30)
public static void EnsureLifecycle(EntityManager em, Entity ge, ...)
{
    var ecb = new EntityCommandBuffer(Allocator.Temp);  // 创建临时 ECB
    EnsureLifecycle(em, ref ecb, ge, state, currentFrame);
    ecb.Playback(em);  // ← 立即 playback = sync point!
    ecb.Dispose();
}
```

这种模式出现在以下方法中（不完全列表）：
- `EnsureLifecycle` (独立重载)
- `MarkEffectForRemoval` (独立重载)
- `ActivateDurationEffect` (独立重载)
- `ApplyInactiveDurationEffect` (独立重载)
- `DeactivateOngoingEffect` (独立重载)
- `ReactivateOngoingEffect` (独立重载)
- `HandleDurationExpired` (独立重载)
- `CleanupActiveEffect` (独立重载)
- `DestroyEffectEntity` (独立重载)
- `CreateDerivedApplyRequest` (独立重载)
- `TryMergeStackingApplication` (独立重载)
- `PlaybackAndReset` (本身)
- `CopySetByCallerValues` (独立重载)
- `CreateOverflowRequests` (独立重载)
- `RemoveActiveGameplayEffectsWithTags` (独立重载)

每个重载都是一对：一个 `(EntityManager, ...)` 的便捷重载 + 一个 `(EntityManager, ref EntityCommandBuffer, ...)` 的 ECB 重载。便捷重载创建临时 ECB → 立即 Playback → Dispose。

**问题**: 调用者无法控制 ECB 合并 —— 即使在 foreach 循环内，每次操作都是独立的 ECB + sync point。

---

## P1 严重缺陷

### 缺陷 E: SystemGroup 层次未迁移到目标态 — `SYS-02`

**严重程度**: P1

**代码证据**: `GASGroups.cs`

当前 SystemGroup 层次：
```
SimulationSystemGroup
├── GASCommandGroup         ← 旧结构
│   ├── GASExecutionCalculationExtensionGroup
├── GASResetDirtyGroup       ← 旧结构
├── GASTagGroup              ← 旧结构
├── GASEffectGroup           ← 旧结构
├── GASAttributeGroup        ← 旧结构
├── GASAbilityGroup          ← 旧结构
│   ...
├── GASCueGroup              ← 旧结构
│   ├── GasStructuralPlaybackSystemGroup ← 存在但未实际使用
```

目标态层次（Spec 03-RuntimeCore管线Spec.md）：
```
GasRuntimeFramePrepareSystemGroup
├── GasCommandIngestSystemGroup
├── GasSpecEvalSystemGroup
├── GasActiveLifecycleSystemGroup
├── GasDeltaApplySystemGroup
├── GasTypedFactSystemGroup
├── GasStructuralPlaybackSystemGroup  ← 唯一结构变化屏障
├── GasObservationSystemGroup
```

**差距**: 完全不匹配。当前代码没有任何新 SystemGroup 的实际创建和使用。

---

### 缺陷 F: EventBus + Stream 双重事件路径反模式 — `BUF-02` `SEL-02`

**严重程度**: P1

**代码证据**:

1. `EffectRuntimeUtility` 中每产生一个事件同时写入两条路径：
   - `EnqueueGameplayEvent` → `EventBusHelper.EnqueueGameplayEvent(em, GASManager.EntityEventBus, event)` ← 旧 EventBus 路径
   - `EnqueueCueRequest` → `EventBusHelper.EnqueueCueRequest(em, GASManager.EntityEventBus, ...)` ← 旧 EventBus 路径

2. AM3 新代码 `SEffectCommandSpecStreamPhases.STypedSimulationFactEventBridge` 同时写入：
   - 新的 `BTypedSimulationFact` buffer（stream 模式）
   - 旧的 `BGameplayEvent` buffer（EventBus 模式）

3. `GASManager.EntityEventBus` 是一个全局 singleton entity，承载 5 种 Buffer：
   - `BGameplayEvent`, `BAttributeChangeEvent`, `BCueRequest`, `BTagChangeEvent`, `BDamageEvent`

**违反规则**:
- `BUF-02`: "DynamicBuffer 不是无限全局消息总线"
- `SEL-02`: 外部观察只通过 fact stream、presentation outbox、replay sink、read model
- 不变量 #2: "外部写入只通过 request entity 或等价 command data"

---

### 缺陷 G: Period/Overflow 复杂派生仍可能回退 Request Entity — `SEL-01`

**严重程度**: P1

**代码证据**:

`EffectRuntimeUtility.CreateDerivedApplyRequest`（line 379-478）:
- Period tick / overflow 产生派生 GE apply → 调用 `CreateDerivedApplyRequest`
- 当前 simple instant child GE 已先走 `TryAppendDerivedEffectCommand`，写入 `BEffectCommand(Source=Period/Overflow)`；派生 runtime GE 上的 `BSetByCallerValue` 会复制到 command stream
- 复杂 child GE 仍会创建 `CApplyGameplayEffectRequest` + `GameplayEffectRequestWriter.Create(ref ecb, request, ...)`，保留完整旧 request fallback
- 因此本缺陷从 "simple period 默认实体化" 收缩为 "复杂 period / overflow child fallback 仍可能实体化"

`SAbilityCommit.ExecuteActivationEffects`（line 258-285）:
- 遍历 `BAbilityEffectOnActivate` buffer
- 对每个 effect 调用 `GameplayEffectRequestWriter.AppendSimpleInstantCommandOrCreateSingleTargetRequest`
- 虽然是 "AppendSimpleInstantCommand **Or** Create"，但 fallback 仍是创建 entity

**剩余违反 / 风险**:
- `SEL-01`: 外部写入只通过 request entity 或等价 command data
- 不变量 #20: "request entity 不是高频 instant GE 的默认承载；高频路径优先 command data / buffer / stream"

---

### 缺陷 H: ThreadStatic 批量状态阻止 Burst Job 化 — `PRF-12` `BUR-01`

**严重程度**: P1

**代码证据**: `EventBusHelper.cs` line 13-22:
```csharp
[ThreadStatic] private static bool _gameplayEventBatchActive;
[ThreadStatic] private static EntityManager _gameplayEventBatchEntityManager;
[ThreadStatic] private static Entity _gameplayEventBatchEventBusEntity;
[ThreadStatic] private static CGameplayEventBus _gameplayEventBatchEventBus;
[ThreadStatic] private static int _gameplayEventBatchFrame;
[ThreadStatic] private static bool _gameplayEventBatchCanAppend;
// ... 还有 4 个 ThreadStatic 字段
```

**问题**: `[ThreadStatic]` 在 Burst 编译的 job 中不工作。类的 XML doc 声称 "在 Burst 并行 Job 中使用 AsParallelWriter 安全入队"，但 ThreadStatic 批量机制与此矛盾。

---

### 缺陷 I: EffectRuntimeUtility 承载过多职责 — `FSM-04`

**严重程度**: P1

**代码证据**: `EffectRuntimeUtility.cs` 2126 行，包含：
- GE 生命周期管理（apply/activate/deactivate/reactivate/expire/cleanup）
- Stack 合并逻辑（FindMatchingStackedEffect, TryMergeStackingApplication, SetStackCount, RefreshStackedEffect）
- Granted tag/ability 管理（AddGrantedTags, RemoveGrantedTags, AddGrantedAbilities, RemoveGrantedAbilities, AddRuntimeGrantedAbility）
- Modifier 管理（ApplyInstantModifiers, AddRuntimeModifiers, RemoveRuntimeModifiers, SyncRuntimeModifiersFromResolved）
- Cue 请求（6种 EnqueueCueRequests 泛型重载 + EnqueueCueRequestOnApply + EnqueueStopCueRequests + EnqueueKillCueRequests）
- 事件入队（EnqueueGameplayEvent, EnqueueAttributeChangeEvent, EnqueueTagChangeEvent）
- Derived apply request 创建（CreateDerivedApplyRequest, TryAppendDerivedEffectCommand）
- Static definition blob 查询（TryGetStaticDefinitionBlob, MeetsOngoingRequirements, HasOngoingRequirements）
- ECB 管理（PlaybackAndReset）
- Duration/Period/Stack effect 清理（DestroyEffectEntity, CleanupActiveEffect, RemoveActiveGameplayEffectsWithTags）

**违反规则**:
- `FSM-04`: 单 entity 上 >3 个独立生命期决策时应评估拆分
- 当前所有 GE 相关逻辑全部耦合在一个 2126 行的 static class 中

---

## P2 改进项

### 缺陷 J: `ResolveCurrentFrame` 重复实现 5 次 — `PRF-32`

本缺陷已局部闭合：`GasRuntimeDebugger.cs` 保留 wrapper，`EventBusHelper.cs`、`SPresentationOutboxProjection.cs`、`SDebugReplayLogProjection.cs`、`EffectCommandSpecStream.cs` 的重复实现已删除并统一调用 `GASRuntimeFrameContext`，且 `GASRuntimeFrameContext` fallback 临时 query 已删除。剩余缺口不是重复代码，而是 frame owner 仍由 `GASManager.EntityGlobalTimer` static known owner 提供；该缺口归并到缺陷 C 继续追踪。

### 缺陷 K: managed 数组分配在每帧调用的代码路径中

- `GASRuntimeFrameBudgetPlanner.CreateCurrent()` — 返回 `new GASRuntimeFrameBudgetPlan(new[] { 10个 entry })` — **每次调用**分配 managed 数组
- `GasRuntimeDebugger.CreateSnapshot` — `new BGasRuntimeDiagnosticEvent[log.Length]` — managed 数组分配
- `EffectRuntimeUtility.RemoveGrantedAbilities` — `new NativeArray<BGrantedAbilityRuntime>(runtimeBuffer.Length, Allocator.Temp)` — NativeArray 分配（Temp 可以接受但频繁调用会有压力）

### 缺陷 L: IComponentData 上挂 NativeArray — `PRF-34`

`CCueOnApply.cues`、`CCueOnAdd.cues` 等 Cue 组件持有 `NativeArray<Entity>` 字段。这是 `PRF-34` 的直接违规 —— 虽然当前在主线程通过 EntityManager 访问（不触发 job safety 问题），但阻止了这些组件被 IJobChunk/IJobEntity 处理。

---

## 汇总结论

| 严重度 | 缺陷数 | 关键违规规则 |
|---|---|---|
| P0 | 4 (A, B, C, D) | `PRF-05`, `PRF-02`, `PRF-09`, `PRF-33`, `JOB-01`, `SC-01`, `ECB-01` |
| P1 | 5 (E, F, G, H, I) | `SYS-02`, `BUF-02`, `SEL-01`, `SEL-02`, `PRF-12`, `FSM-04` |
| P2 | 2 未闭合 (K, L) + 1 已局部闭合 (J) | `PRF-32`, `PRF-34` |

**当前代码与目标态 Spec 的核心差距不是"某些细节不对"，而是整个执行范式尚未切换到 DOTS 模式**:
1. 全系统主线程 foreach + EntityManager（应改为 IJobChunk/IJobEntity）
2. 无 SystemGroup 迁移（旧 6-group 结构未动）
3. 全局 singleton EventBus + Stream 双重路径（应统一到 typed facts + per-owner outbox）
4. 热路径中散落大量临时 EntityQuery、临时 ECB + Playback、直接 EntityManager 结构变化
