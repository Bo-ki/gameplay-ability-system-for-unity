# 核心问题诊断归档

本目录存放已经退出当前看板的问题诊断。

归档不是删除历史，而是说明问题已经满足退出条件，或已经被更高层级的问题合并。归档文档必须保留：

1. 原问题 ID 和标题。
2. 归档日期。
3. 解决证据或合并原因。
4. 关联提交、验证记录或目标态 Spec。
5. 后续如果复发，应从哪个新问题重新打开。

## 归档索引

| 文件 | 内容 |
|---|---|
| [2026-06-07-GAS架构瘦身执行记录](2026-06-07-GAS架构瘦身执行记录.md) | 本轮 GAS Runtime 瘦身执行记录、验证证据和复发入口 |
| [2026-06-08-AutoChessBattleValidation-DebuggerRegisteredOwner-Run1](2026-06-08-AutoChessBattleValidation-DebuggerRegisteredOwner-Run1.log) | Debugger registered owner 切片的 AutoChess x50 原始日志 |
| [2026-06-08-AutoChessBattleValidation-ObservationMaterialization-Run1](2026-06-08-AutoChessBattleValidation-ObservationMaterialization-Run1.log) | Debugger observation materialization 归因切片的 AutoChess x50 原始日志 |
| [2026-06-08-AutoChessBattleValidation-PerformanceObservationIsolation-Run1](2026-06-08-AutoChessBattleValidation-PerformanceObservationIsolation-Run1.log) | Debugger observation materialization 从 performance pass 隔离后的 AutoChess x50 原始日志 |
| [2026-06-08-AutoChessBattleValidation-PassSplitMagnitudeSource-Run1](2026-06-08-AutoChessBattleValidation-PassSplitMagnitudeSource-Run1.log) | performance / diagnostic / official diff pass 拆分与 Magnitude Source evidence 贯通后的 AutoChess x50 原始日志 |
| [2026-06-08-AutoChessBattleValidation-PreTickSourceAttributeSnapshot-Run3](2026-06-08-AutoChessBattleValidation-PreTickSourceAttributeSnapshot-Run3.log) | active effect slot pre-tick SourceAttribute snapshot gather 修复后的 AutoChess x50 原始日志 |
| [2026-06-08-AutoChessBattleValidation-TagRequirementQuery-Run5](2026-06-08-AutoChessBattleValidation-TagRequirementQuery-Run5.log) | TagRequirement all-any-none catalog / runtime evaluator / instant GE gate 贯通后的 AutoChess x50 原始日志 |
| [2026-06-08-AutoChessBattleValidation-OpaqueDriverHandle-Run7](2026-06-08-AutoChessBattleValidation-OpaqueDriverHandle-Run7.log) | AutoChess driver public raw Entity handle 退出后的 AutoChess x50 原始日志 |
| [2026-06-08-AutoChessDriverOwnerSnapshot](2026-06-08-AutoChessDriverOwnerSnapshot.md) | AutoChess driver owner snapshot 与 raw driver Entity 防回流切片记录 |
| [2026-06-08-AutoChessTimingOwnerSplit](2026-06-08-AutoChessTimingOwnerSplit.md) | AutoChess Runtime Debugger timing owner split 与防回流门归档 |
| [2026-06-08-OwnerLocalGameplayFactLane](2026-06-08-OwnerLocalGameplayFactLane.md) | pending AttributeDelta 产生的 Attribute fact 迁入 ASC owner-local fact lane 的切片记录 |
| [2026-06-08-ActiveEffectMutationOwnerLocalCarrier](2026-06-08-ActiveEffectMutationOwnerLocalCarrier.md) | ActiveEffectMutationBuffer 与 active mutation command source 退出 singleton stream owner、转为 ASC owner-local carrier 的事实归档 |
| [2026-06-08-ActiveEffectMutationSetByCallerOwnerLocalPayload](2026-06-08-ActiveEffectMutationSetByCallerOwnerLocalPayload.md) | active mutation set-by-caller payload 退出 singleton stream 输入、转为 ASC owner-local payload + frame-local apply list 的事实归档 |
| [2026-06-08-AbilityCommitActiveMutationOwnerLocalCommand](2026-06-08-AbilityCommitActiveMutationOwnerLocalCommand.md) | ability commit / active period GE 跳过 singleton command stream，pre-tick/remove mutation output 改写 ASC owner-local buffer 的事实归档 |
| [2026-06-08-AttributeDeltaOwnerLocalFactProjection](2026-06-08-AttributeDeltaOwnerLocalFactProjection.md) | AttributeDelta fact 退出 EffectCommandSpecStream singleton delta carrier、改写 ASC owner-local fact 的切片记录 |
| [2026-06-08-AutoChessRuntimeAccessCapability](2026-06-08-AutoChessRuntimeAccessCapability.md) | AutoChess adapter raw GASRuntimeShell ECS seam 集中到 AutoChessGasRuntimeAccess 的切片记录 |
| [2026-06-08-架构重划分审查事实拆分前](2026-06-08-架构重划分审查事实拆分前.md) | `../架构重划分审查事实.md` 拆分前全文快照 |
