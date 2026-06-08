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
| [2026-06-08-DebuggerDerivedExportSink-Run2](2026-06-08-DebuggerDerivedExportSink-Run2.md) | Debugger Data-Oriented Scorecard 第二刀：DerivedExportSink、metric family mask 与 dominant risk 归档 |
| [2026-06-08-DebuggerMetricFamilySnapshot-Run3](2026-06-08-DebuggerMetricFamilySnapshot-Run3.md) | Debugger Data-Oriented Scorecard 第三刀：MetricFamilySnapshot 稳定证据面与外部消费解耦归档 |
| [2026-06-08-DebuggerDiagnosticEvidenceSnapshot-Run4](2026-06-08-DebuggerDiagnosticEvidenceSnapshot-Run4.md) | Debugger Data-Oriented Evidence 第四刀：DiagnosticEvidenceSnapshot envelope 与 AutoChess 外部消费解耦归档 |
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
| [2026-06-08-GameplayEffectRequestWriterActiveMutationOwnerLocalCommand](2026-06-08-GameplayEffectRequestWriterActiveMutationOwnerLocalCommand.md) | Runtime boundary / ability runtime helper 经 GameplayEffectRequestWriter 产生的 active mutation command 直写目标 ASC owner-local command/payload buffer 的事实归档 |
| [2026-06-08-OverflowActiveMutationNextFrameOwnerLocalCommand](2026-06-08-OverflowActiveMutationNextFrameOwnerLocalCommand.md) | overflow active mutation 派生命令退出 singleton command stream，改写 ASC next-frame owner-local carrier 的事实归档 |
| [2026-06-08-OverflowInstantNextFrameOwnerLocalCommand](2026-06-08-OverflowInstantNextFrameOwnerLocalCommand.md) | overflow instant 派生命令退出 singleton command stream，改写 ASC next-frame owner-local instant carrier 的事实归档 |
| [2026-06-08-InstantSpecBuildOwnerLocalConsumer](2026-06-08-InstantSpecBuildOwnerLocalConsumer.md) | instant spec build consumer 直接消费 ASC owner-local command/payload，旧 flush 回 singleton command stream 中间层退场的事实归档 |
| [2026-06-08-InstantSpecCarrierOwnerLocal](2026-06-08-InstantSpecCarrierOwnerLocal.md) | instant spec carrier 与 generated AttributeReduce / cue projection 退出 singleton GEEffectSpecBuffer，改为 ASC owner-local buffer 的事实归档 |
| [2026-06-08-EffectInstantHandwrittenRuntimeOwner](2026-06-08-EffectInstantHandwrittenRuntimeOwner.md) | RuntimeEffectInstant generated lifecycle 退为 marker，instant spec build 与 attribute reduce apply 改由手写 Runtime owner 承担的事实归档 |
| [2026-06-08-ExecutionFactOwnerLocalSpecChain](2026-06-08-ExecutionFactOwnerLocalSpecChain.md) | execution-only GE 恢复 instant spec 链，AutoChess x50 验证 owner-local pending delta / fact / specs 的事实归档 |
| [2026-06-08-BoundaryObservationFactCarrier](2026-06-08-BoundaryObservationFactCarrier.md) | Boundary observation fact carrier 接管表现、回放、typed event bridge 和 Debugger 的 fact 读面 |
| [2026-06-08-AbilityLifecycleFactOwnerLocal](2026-06-08-AbilityLifecycleFactOwnerLocal.md) | generated ability lifecycle fact 退出 singleton GameplayEventBuffer direct append，改写 ASC owner-local fact buffer 的事实归档 |
| [2026-06-08-HandwrittenAbilityFactOwnerLocal](2026-06-08-HandwrittenAbilityFactOwnerLocal.md) | hand-written ASC / ability lifecycle fact producer 退出 singleton GameplayEventBuffer direct append，改写 ASC owner-local fact buffer 的事实归档 |
| [2026-06-08-AttributeDeltaOwnerLocalFactProjection](2026-06-08-AttributeDeltaOwnerLocalFactProjection.md) | AttributeDelta fact 退出 EffectCommandSpecStream singleton delta carrier、改写 ASC owner-local fact 的切片记录 |
| [2026-06-08-AutoChessRuntimeAccessCapability](2026-06-08-AutoChessRuntimeAccessCapability.md) | AutoChess adapter raw GASRuntimeShell ECS seam 集中到 AutoChessGasRuntimeAccess 的切片记录 |
| [2026-06-08-架构重划分审查事实拆分前](2026-06-08-架构重划分审查事实拆分前.md) | `../架构重划分审查事实.md` 拆分前全文快照 |
