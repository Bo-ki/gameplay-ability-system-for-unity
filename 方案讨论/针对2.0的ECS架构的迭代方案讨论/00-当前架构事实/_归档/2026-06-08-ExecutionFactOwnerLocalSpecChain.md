# Execution fact owner-local spec chain 归档

> 日期：2026-06-08
> 范围：execution-only GE / owner-local instant spec / pending AttributeDelta / AutoChess x50 validation
> 状态：本切片已通过 AutoChess 无头业务验收

## 变更事实

本切片修复了 execution-only / cue-only GameplayEffect 在 generated command seed 分类中被误判为 `ActiveMutation` 的问题，并把 AutoChess execution 诊断接入 validation evidence：

1. `GECommandSeedRecord` / `GEEffectCommandBuffer` 的 ActiveMutation 判定不再由 `ModifierCount == 0` 推断。
2. execution-only GE 重新走 instant spec 链，进入 ASC owner-local `GEEffectSpecBuffer`。
3. `AutoChessExecuteDamageCalculationSystem` 扫描 owner-local spec，产生 execution output，并写目标 ASC owner-local pending `AttributeModifierBuffer`。
4. pending AttributeDelta 继续由 Runtime Core owner-local chunk apply 处理。
5. execution fact 走 ASC owner-local `OwnerLocalGameplayFactBuffer`，不回退 singleton gameplay event stream。
6. AutoChess validation 的 runtime chain gate 从 legacy `RequestCount > 0` 调整为 `SpecCount > 0`；当前 owner-local command/spec/fact 链路下 `coreRequests=0` 是预期。

## 验证证据

有效原始日志：

`Temp/AutoChessBattleValidation-ExecutionFactOwnerLocal-FinalRun2.log`

关键字段：

| 字段 | 值 |
|---|---:|
| `passed` | `True` |
| `thresholdsPassed` | `True` |
| `runtimeChainPassed` | `True` |
| `repeatRunPassed` | `True` |
| `winner / expectedWinner` | `Player / Player` |
| `executionOutputs` | `350` |
| `executionSpecScans` | `1400` |
| `executionMatchedEffectSpecs` | `350` |
| `executionTargetOwnerMismatches` | `0` |
| `executionMissingAttributes` | `0` |
| `executionEvaluatorRejects` | `0` |
| `executionOutputWrites` | `350` |
| `pendingAttributeDeltas` | `350` |
| `pendingAttributeAppliedDeltas` | `250` |
| `ownerLocalFacts` | `5200` |
| `ownerLocalFactFlushes` | `5200` |
| `coreRequests` | `0` |
| `core specs` | `1400` |
| `debugErrors / blockingDebugErrors` | `0 / 0` |
| `GASTickTotal avgMs` | `2.212` |
| `GASCoreSimulationSystemGroup avgMs` | `1.282` |

## 代码证据

| 事实 | 路径 |
|---|---|
| execution 诊断字段贯通 | `Assets/AutoChessDemo/Battle/AutoChessBattleContracts.cs` |
| driver 统计字段 | `Assets/AutoChessDemo/Battle/Ecs/AutoChessBattleDriverComponents.cs` |
| execution spec scan / pending delta / owner-local fact | `Assets/AutoChessDemo/Battle/Ecs/AutoChessExecuteDamageCalculationSystem.cs` |
| validation runtime chain 改看 `SpecCount` | `Assets/AutoChessDemo/Battle/Validation/AutoChessBattleValidationRun.cs` |
| report 输出 specs 与 execution 诊断 | `Assets/AutoChessDemo/Battle/Validation/AutoChessBattleValidationReport.cs` |
| batchmode 参数入口 | `Assets/AutoChessDemo/Editor/AutoChessDemoBatchRunner.cs` |
| seed 分类不变量实现 | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeDefinitionGlue.gen.cs`、`Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs` |

## 不能推出的结论

1. 不能把 `coreRequests=0` 写成 Runtime chain 断裂；当前链路已经转为 owner-local command/spec/fact，legacy request entity count 不再是通过条件。
2. 不能把 x50 `avgMs` 写成性能终局；仍需 x100 / x1000、Profiler enabled、Journaling diff 和 buffer pressure 曲线。
3. 不能把 execution extension 写成 Runtime Core owner；AutoChess execution 仍是 demo extension，只是通过 Runtime Core 的 spec / pending delta / fact contract 接入。

## 复发入口

如果 validation 再次出现 `executionOutputs=0`、`executionMatchedEffectSpecs=0`，或 execution-only GE 被送入 ActiveMutation 而不是 instant spec 链，应重新打开 DefinitionCodeGen seed 分类和 AutoChess execution spec chain 子项。
