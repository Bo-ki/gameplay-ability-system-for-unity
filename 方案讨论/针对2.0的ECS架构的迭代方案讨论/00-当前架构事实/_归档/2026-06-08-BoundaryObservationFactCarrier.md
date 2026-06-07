# Boundary Observation Fact Carrier 切片事实

> 日期：2026-06-08

本切片把表现、回放、typed event bridge 和 Debugger 的 fact 读面从 `EffectCommandStream` 上的 `GameplayEventBuffer` 迁到 `GameplayEventBus` 上的 `BoundaryObservationFactBuffer`。

## 已落地

- `GameplayEventBusComponent` 定义 `BoundaryObservationFactBuffer` 与来源枚举。
- `GASRuntimeEntityArchetypes.GameplayEventBus()` 与 `GASManager.CreateEventBusSingleton()` 挂载并预分配 Boundary observation carrier。
- `GameplayEventBusClearSystem` 在 FramePrepare 清空 Boundary observation。
- `GameplayBoundaryFactExportSystem` 位于 `GASBoundaryProjectionSystemGroup` 第一段，收集 ASC-local `OwnerLocalGameplayFactBuffer`，并兼容读取本帧 legacy stream `GameplayEventBuffer` 作为 migration input。
- `GameplayFactBoundaryProjectionSystem`、`PresentationOutboxProjectionSystem`、`ReplayLogSystem` 改为读取 `BoundaryObservationFactBuffer`。
- `GasRuntimeDebugger` 的 typed fact count、event bus pressure、damage/request/instanced/removed fact 分类改为读取 Boundary observation。
- `GASRuntimeQueryLayoutPlan` / `GASRuntimeStreamOwnerContract` / `GameplayEffectRuntimePipelineContract` 已同步 Boundary observation carrier 语义。

## 剩余风险

- legacy producer 仍有 direct stream `GameplayEventBuffer` 写入，当前只是被 Boundary export 当 migration input 消费。
- `NextFactSequence` 仍由 `GEEffectCommandStreamComponent` 分配，尚未迁到 owner-local 或 Boundary sequence owner。
- 本切片已通过 runtime build 和边界脚本，但尚未跑 AutoChess x50/x1000 真实业务验收。
