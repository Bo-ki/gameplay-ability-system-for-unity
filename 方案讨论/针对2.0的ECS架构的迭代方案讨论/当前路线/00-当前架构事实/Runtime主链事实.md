# Runtime 主链事实

## 已成立事实

1. `Assets/GAS/Runtime` 中旧 OOP 主链已退出当前 runtime 主线。
2. 外部接入以 `AbilitySystemBinding + AbilitySystemFacade` 为主。
3. 写操作应通过 request entity 进入 ECS。
4. Runtime driver 可读取 ECS state / buffer / tag / attribute，并通过 request entity 输出意图。
5. Ability commit、GE Spec/Context、Magnitude/Capture、ExecutionCalculation、Tag Requirement、Observation read model 已有当前 contract。
6. Presentation outbox、Replay sink、GameplayEventBus 已经分层，但 Observation 仍需要继续从 hot path 中拆离。
7. AM2 已落地 `EffectCommandSpecStream` 数据契约；AM3 已完成 simple instant 局部 proof；AM5 已落地 owner-local ActiveEffectStore mirror 和 slot pressure counters。

## 当前风险

1. 旧 GE lifecycle pipeline 仍是待迁移对象。
2. 高频业务 reaction 仍有从全局事件流读取的历史残留。
3. Runtime Core Debugger counters 不足，导致热点定位成本过高。
4. 目标态 Spec 已开始补 Unity Entities 1.4.6 机制校准，但当前实现仍需按 SystemGroup、ISystem/job、ECB playback、DynamicBuffer、Enableable、Blob/Baker、Query filter 逐项落地。
5. 当前 Runtime Core 仍缺少统一 frame backbone：`EffectCommandSpecStream`、ActiveEffectStore 和 Debugger baseline 尚未统一到 `FramePrepare / QueryBudget / AllocatorBudget / DependencyBudget / StructuralPlaybackGate / DeterministicMerge` 执行骨架。
6. 当前 `EffectCommandSpecStream` 仍以 singleton DynamicBuffer 和 helper 查询为主，必须继续证明它是 proof-only 还是 scale-ready 承载。
7. 当前 Debugger 尚不能直接输出 query / lookup / allocator / dependency / deterministic merge 等 DOTS 官方诊断口径。
