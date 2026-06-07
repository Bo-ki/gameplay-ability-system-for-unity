# CodeGen validation report ArtifactCategory gate 目标态兑现记录

> 日期：2026-06-08

本记录对应 `08-Luban-SourceGenerator配置生成链路Spec`、`14-DefinitionCodeGen目标链路Spec` 与 Runtime Core 目标态中 “generated runtime 债务必须可分类、可审查、可阻断回流” 的报告 gate 切片。

## 兑现点

- `GasCodeGenValidationReport.md` 的 Manifest Entries 表已显式展示 `ArtifactCategory`。
- 未分类 artifact 在报告里显示为 `None`，分类债务不再依赖读 generator 内部字段。
- `RuntimeDefinitionGlue` 被报告为 `RuntimePureGlue`，用于区分纯 definition glue 和 lifecycle migration debt。
- `RuntimeAbilityActivation`、`RuntimeEffectInstant`、`RuntimeActiveEffect` 被报告为 `RuntimeLifecycleMigration`，generated runtime boundary gate 继续按迁移证明管理。
- `Verify-GAS-RuntimeCoreBoundary.ps1` 已把报告列、模板列和关键 artifact 分类写成防回流断言。
- `RuntimeEffectInstant` 的 generated scratch list 已从 `Allocator.TempJob` 收敛到 `state.WorldUpdateAllocator`，本切片的 `GeneratedHotPathRegressionHits` 回到 `0`。
- 重复 helper 输出已收敛，`GeneratedDuplicateMethodHits` 回到 `0`。

## 未完成点

- `GeneratedRuntimeBoundaryHits` 仍为 `66`，这些 hit 只是被 `RuntimeLifecycleMigration` 分类接住，不能视为最终目标态。
- `RuntimeLifecycleMigration` artifact 里仍存在跨 owner lookup、lifecycle owner 和 structural owner 面，后续仍要按 owner-local fan-in、chunk-local owner group 或更窄的 generated contract 继续拆。
- 本切片未运行 Unity headless AutoChess、Unity Test Runner 或性能 profile，不能作为端到端业务承载结论。
