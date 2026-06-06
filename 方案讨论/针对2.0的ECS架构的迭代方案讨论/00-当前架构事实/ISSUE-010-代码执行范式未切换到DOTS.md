# ISSUE-010 代码执行范式未切换到 DOTS

> 最近复核：2026-06-06 | 状态：Active | 严重度：P1

## 当前结论

旧“全是 `ToEntityArray` + foreach”的诊断已经过期。当前代码已经大量改为 `ISystem`、`QueryBuilder`、ECB system singleton、generated blob catalog 和 scheduled job。`AbilityCatalogCommit`、instant spec/reduce、active pre-tick/remove 这些旧 generated 风险也已按 DOTS 规则补齐。当前执行范式问题降为 P1：主要风险转为 generated active mutation 的 `EntityManager` direct access、singleton stream owner、legacy EventBus writer、static validation 缺口和 generated serial store。

## 当前事实

- Runtime + generated runtime 当前有 33 个 `ISystem`。
- Runtime 仍有 1 个 `SystemBase`：`GASManagerInputSystem`，未注册进 5 段 GAS 主链。
- `SystemAPI.Query<...>` 使用多行正则复核后，在 `Assets/GAS` 当前只命中 Cue start/tick/end/destroy managed boundary；ASC command resolve、generated ability commit、generated active remove、ability cleanup、attribute recalc fallback、AutoChess command drive 等旧主线程 foreach 均已退场。
- `state.Dependency.Complete()` 当前 `Assets/GAS/**/*.cs` 扫描为 0；`ASCDestroyFinalizeSystem`、ExecutionCalculation、generated active effect pre-tick 的同步等待已退场。
- `CalculateChunkCount()` 已从 Core stream/job 前置计数中退场，相关 NativeStream for-each count 改用 `CalculateChunkCountWithoutFiltering()`，避免 enableable/filter 同步并匹配 `IJobChunk` 的 unfiltered chunk index。
- `GEEffectCommandSpecStreamFramePrepareSystem` 和 `GameplayFactProjectionSystem` 已从主线程 `EntityManager.GetBuffer` 路径迁到 scheduled `IJob`；旧 `GameplayFactEventBridgeSystem` 与 `GEInstantEffectCueRequestProjectionSystem` 已删除。
- `AbilityCatalogCommitJob` 已从 `ComponentLookup.SetComponentEnabled` 随机访问切到 chunk `EnabledMask`；当前符合 `EN-03` / `CASE-20` 的批量 enableable 口径。
- `RuntimeEffectInstant.gen.cs` 的 `InstantSpecBuildJob` 与 `AttributeSetReduceApplyJob` 已是 scheduled `[BurstCompile] IJob`；`CanBuildInstantSpec` 支持 cue-only instant GE。
- generated `IsDestroyingAsc` 已按 `ASCDestroyingComponent` enabled bit 判断销毁态；不再用 `HasComponent` 误判默认 disabled 的 ASC。
- generated active lifecycle 仍存在 `ComponentLookup.SetComponentEnabled(...)` 随机 enableable 开关；它不是旧 ability commit request 风险，但仍违反 `EN-03` 对高频路径优先迭代式 enableable 的要求。
- `ToEntityArray()` 当前主要在 Debugger observation 和 AutoChess catalog 初始化。

## 仍成立风险

1. generated systems 需要同等接受 Burst/job/query 审查；已修复链路需要 static validation，未修复重点是 `GASActiveEffectMutationApplySystem` 与 active lifecycle random enableable。
2. singleton stream owner 仍是 proof carrier，不是 high-scale fan-in 终局。
3. managed Cue / Registry / Helper 不能进入 CoreSimulation hot path。
4. `EntityManager.GetBuffer/GetComponentData/SetComponentData` 在 generated active mutation、legacy helper 和 boundary/prototype 路径中仍多，必须按 owner 与相位分类治理。
5. 最新 AutoChess runner 能跑通且 `debugErrors=0`，但 `requiredStructuralPlaybacks=5`、`recordedStructuralPlaybacks=0` 暴露结构变化证据采集仍未闭合，不能把“能跑”写成结构变化已合规。

## 退出条件

1. CoreSimulation hot path 的主扫描迁移到 `IJobChunk` / 明确 job chain，或有充分规模证据证明主线程路径可接受。
2. `Complete()` 保持清零；`CalculateEntityCount()` / `ToEntityArray()` / `CalculateChunkCount()` 这类 query sync 风险保持在 observation 或明确低频路径。
3. generated runtime 输出 DOTS 审查报告与 static validation，至少覆盖 `[BurstCompile]`、enableable mask、`Complete()`、Temp ECB playback、ASCDestroying enabled bit 判定。
4. managed boundary 与 unmanaged core 的成本报告分离。
