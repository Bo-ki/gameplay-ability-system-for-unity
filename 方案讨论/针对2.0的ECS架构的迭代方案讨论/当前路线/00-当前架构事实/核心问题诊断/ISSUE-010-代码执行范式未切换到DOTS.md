# ISSUE-010 代码执行范式未切换到 DOTS

## 状态

| 字段 | 内容 |
|---|---|
| 状态 | Active |
| 严重度 | P0 |
| 最近复核 | 2026-05-25 |
| 所属层 | GAS Runtime Core Layer |

## DOTS规则引用

| 规则编号 | 规则摘要 | 关联原因 |
|---|---|---|
| `PRF-05` | Hot Path 禁止主线程遍历 | 全部热路径系统使用 `ToEntityArray` + `foreach`，零 `IJobEntity`/`IJobChunk` |
| `PRF-02` | 禁止在 Hot Path 直接执行结构变化 | `AbilityRuntimeActions` 直接 `EntityManager.AddComponentData` |
| `JOB-01` | 并行批处理说明 IJobEntity/IJobChunk 选择理由 | 全主线程，Burst Job 覆盖率为零 |
| `SC-01` | Hot path 不直接结构变化；集中在 mutation phase | 结构变化散落在 AbilityRuntimeActions、GameplayEffectEntityFactory、EffectRuntimeUtility |
| `ECB-01` | ECB 是延迟结构变化工具 | ECB.Playback 散落在 15+ helper 便捷重载中，每次都是独立 sync point |
| `ECB-03` | ECB playback 位置属于明确 SystemGroup phase | `GasStructuralPlaybackSystemGroup` 已声明但未强制执行 |
| `CASE-02` | IJobEntity 遍历 — Runtime Core hot path 主力 | 当前零使用 |
| `CASE-03` | IJobChunk 遍历 — 批量统计、enableable 过滤 | 当前零使用 |

## 问题陈述

当前 Runtime Core 约 15 个关键热路径文件（~8000 行 C#）全部运行在主线程，使用 `EntityQuery.ToEntityArray(Allocator.Temp)` + `foreach` 遍历模式。没有任何一个热路径系统使用 `IJobEntity` 或 `IJobChunk` 并行遍历。同时，热路径中散落大量直接的 `EntityManager.AddComponentData`/`SetComponentData` 调用和 "创建临时 ECB → 立即 Playback → Dispose" 模式。

这不是"某些细节不对"，而是整个执行范式尚未从 OOP 主线程模式切换到 DOTS Job/Burst 模式。

## 当前证据

代码证据（详见 `../代码DOTS合规审查报告.md`）：

### 子缺陷 A: 全系统主线 foreach

| 文件 | 行号 | 违规模式 |
|---|---|---|
| `SAbilityCommit.cs` | 31,35 | `_query.ToEntityArray(Allocator.Temp)` + `foreach (var ability in abilities)` |
| `STryActivateAbility.cs` | 30,33 | 同上 |
| `SEffectApply.cs` | 全文 | `_effectQuery.ToEntityArray(Allocator.Temp)` + `foreach` |
| `SEffectTick.cs` | 全文 | 同上 |
| `SApplyGameplayEffectRequest.cs` | 全文 | `_query.ToEntityArray(Allocator.Temp)` + `foreach (var requestEntity in requests)` |
| `SEffectCommandSpecStreamPhases.cs` | 全文 | 6 个 partial struct 全部 `for (var i = 0; i < buffer.Length; i++)` 主线程遍历 |
| `SPresentationOutboxProjection.cs` | 54-67 | 主线程 for 循环遍历 5 种 EventBus buffer |
| `SDebugReplayLogProjection.cs` | 54-67 | 同上 |

### 子缺陷 B: 热路径直接 EntityManager 结构变化

| 文件 | 行号 | 违规代码 |
|---|---|---|
| `AbilityRuntimeActions.cs` | 77 | `entityManager.AddComponentData(ability, new CAbilityInTryEnd{...})` |
| `AbilityRuntimeActions.cs` | 107 | `entityManager.AddComponentData(ability, new CAbilityInTryCancel{...})` |
| `GameplayEffectEntityFactory.cs` | 79-92 | `entityManager.AddComponent<CEffectPendingApply>(entity)` + `AddComponentData` × 3 |
| `GameplayEffectEntityFactory.cs` | 121 | `entityManager.AddBuffer<BGrantedTagConfig>(ge)` |

### 子缺陷 D: ECB.Playback 散布

`EffectRuntimeUtility.cs` 中 15+ 对便捷重载遵循同一反模式：

```csharp
public static void EnsureLifecycle(EntityManager em, Entity ge, ...)
{
    var ecb = new EntityCommandBuffer(Allocator.Temp);  // 创建临时 ECB
    EnsureLifecycle(em, ref ecb, ge, state, currentFrame);
    ecb.Playback(em);  // 立即 playback = sync point
    ecb.Dispose();
}
```

影响方法包括：`EnsureLifecycle`、`MarkEffectForRemoval`、`ActivateDurationEffect`、`ApplyInactiveDurationEffect`、`DeactivateOngoingEffect`、`ReactivateOngoingEffect`、`HandleDurationExpired`、`CleanupActiveEffect`、`DestroyEffectEntity`、`CreateDerivedApplyRequest`、`TryMergeStackingApplication`、`CopySetByCallerValues`、`CreateOverflowRequests`、`RemoveActiveGameplayEffectsWithTags` 等。

Profile 证据：

1. x50 AutoChess profile 中 `avgTickMs=13.77ms`，全主线 foreach 是主要耗时来源。
2. system timing 排序中 `SEffectApply`、`SApplyGameplayEffectRequest`、`SEffectTick` 均为热点。

## 执行路径

```text
每帧 OnUpdate:
  _query.ToEntityArray(Allocator.Temp)  // 分配临时数组 + sync point
  -> foreach (var entity in entities)   // 主线程逐个遍历
     -> EntityManager.GetComponentData  // 主线程逐个读取
     -> EntityManager.SetComponentData  // 主线程逐个写入
     -> new EntityCommandBuffer(Allocator.Temp)  // 创建临时 ECB
        -> ecb.AddComponent / ecb.AddBuffer
        -> ecb.Playback(em)  // sync point
        -> ecb.Dispose()
  -> entities.Dispose()
```

## 影响

1. x50 AutoChess 仅 300 单位已出现 `avgTickMs=13.77ms`。`IJobChunk` 可将此降低 5-10x。
2. 所有计算在主线程串行执行，无法利用 Burst 编译和多线程并行。
3. 每次 `ToEntityArray` 和 ECB `Playback` 都引入 sync point，打断 Job 依赖链。
4. 后续继续补局部 fast path 会扩大分支复杂度，但不能消除范式级性能瓶颈。

## 根因反推

当前实现从历史方案的 OOP GE entity lifecycle 直接迁移，沿用 "在主线程 OnUpdate 中处理所有 entity" 的模式。但 Unity DOTS 的热路径范式要求：

1. 用 `IJobEntity`/`IJobChunk` 并行遍历替代主线程 foreach。
2. 用 Burst 编译的 job 替代主线程 managed 计算。
3. 用 ECB 批量延迟 playback（在统一 structural playback phase）替代散落的即时 ECB + Playback。
4. 高频状态切换用 `IEnableableComponent` 替代 add/remove component。

## 目标态入口

1. `../../01-目标态架构共识/03-RuntimeCore管线Spec.md`
2. `../../01-目标态架构共识/13-EntityComponent物理布局Spec.md`
3. `../../UnityDOTS官方文档参考/主题/02-查询遍历与Job.md`
4. `../../UnityDOTS官方文档参考/主题/03-结构变化-ECB-Enableable.md`
5. `../../UnityDOTS官方文档参考/主题/13-DOTS编写规范与性能陷阱.md`

## 任务入口

1. `../../02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCore重构.md`
2. `../../02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCoreFrameBackbone.md`

## 退出条件

1. 所有 Runtime Core hot path 系统改用 `IJobEntity`/`IJobChunk` 并行遍历，零主线程 `ToEntityArray` + `foreach`。
2. 热路径中零直接 `EntityManager` 结构变化调用；所有结构变化通过 ECB 在 `GasStructuralPlaybackSystemGroup` 集中 playback。
3. x50 AutoChess 的 `avgTickMs` 不随 entity 数量线性增长。
4. `EffectRuntimeUtility` 的 15+ 便捷重载已消除临时 ECB + Playback 反模式；ECB 调用统一到 system-level ECB chain。
5. Debugger 能输出 job count、main-thread system count、ECB playback count per phase。
