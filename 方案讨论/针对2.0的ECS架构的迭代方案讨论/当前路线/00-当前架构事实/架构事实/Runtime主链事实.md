# Runtime 主链事实

## 已成立事实

1. `Assets/GAS/Runtime` 中旧 OOP 主链已退出当前 runtime 主线。
2. 外部接入以 `AbilitySystemBinding + AbilitySystemFacade` 为主。
3. 写操作应通过 request entity 进入 ECS。
4. Runtime driver 可读取 ECS state / buffer / tag / attribute，并通过 request entity 输出意图。
5. Ability commit、GE Spec/Context、Magnitude/Capture、ExecutionCalculation、Tag Requirement、Observation read model 已有当前 contract。
6. Presentation outbox、Replay sink、GameplayEventBus 已经分层，但 Observation 仍需要继续从 hot path 中拆离。
7. AM2 已落地 `EffectCommandSpecStream` 数据契约；AM3 已完成 simple instant 局部 proof；AM5 已落地 owner-local ActiveEffectStore mirror 和 slot pressure counters。
8. **2026-05-26 审查**: 代码库已全面 ECS 化（~222 文件，38 个 ISystem），`GASSystemScheduleContract`(447行) 完整定义 8-phase 管线契约，`GASManager.cs`(130行) 已重写为纯 ECS 启动器。
9. `SEffectCommandSpecStreamPhases`(619行, 6 个 partial ISystem) 使用基于游标的索引 for 循环遍历 DynamicBuffer——这是新代码的正面范例。
10. Typed Simulation Fact 桥接模式（`STypedSimulationFactProjection` → `STypedSimulationFactEventBridge` → 旧 EventBus / Presentation 双出口）已实现清晰的模拟-表现分离。
11. `EventBusHelper.cs`(391行) 已移除全部 `[ThreadStatic]` 字段，改为 `GameplayEventBusWriter` 显式 struct。
12. `SPresentationOutboxProjection`(465行) 和 `SDebugReplayLogProjection`(437行) 已改用索引 for 循环遍历 DynamicBuffer，不再使用 ToEntityArray。
13. `SAbilityTick` 和 `SAttributeRecalculate` 已使用 IJobEntity + Burst——是第一批 job 化的系统。

## 当前风险

1. 旧 GE lifecycle pipeline 仍是待迁移对象。
2. 高频业务 reaction 仍有从全局事件流读取的历史残留。
3. **热路径仍全主线程**: `SAbilityCommit`、`SEffectApply`、`SEffectTick`、`SApplyGameplayEffectRequest` 仍全部使用 `_query.ToEntityArray(Allocator.Temp)` + `foreach`，零 IJobChunk 使用。
4. **直接 EM 结构变化**: 40+ 处 `em.SetComponentData`/`em.AddComponentData`/`em.DestroyEntity` 分散在热路径中（PRF-02/SC-01）。
5. **ECB 碎片化**: `EffectRuntimeUtility`(2125行) 仍有 8+ 处 `PlaybackAndReset` 创建独立 sync point（ECB-01/ECB-03）。
6. `GASSystemScheduleContract` 8-phase 契约已定义但系统尚未按契约完成搬迁。
7. `GasRuntimeDebugger`(2389行) 单体过于庞大，需按关注点拆分。
