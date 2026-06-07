# Ability commit 手写 Runtime owner 目标态兑现记录

> 日期：2026-06-08

本记录对应 Runtime Core 目标态中 “generated runtime 只能提供 pure glue，生命周期 owner 必须收口到手写 Runtime Core” 的 Ability commit 切片。

## 兑现点

- `AbilityCommitSystem` 已从 ordering anchor 升级为手写 Runtime Core lifecycle owner。
- ability commit 的 query、enabled-mask、job dependency、auto-end、tag 变更、command seed 和 owner-local fact 写入都由 runtime assembly 承担。
- runtime 侧新增 `GASRuntimeDefinitionResolver` 和 `GASRuntimeRequirementEvaluator`，避免 hand-written runtime owner 反向依赖 generated runtime assembly。
- `RuntimeAbilityActivation.gen.cs` 退为 marker，`ArtifactCategory` 从 `RuntimeLifecycleMigration` 改为 `RuntimePureGlue`。
- schedule contract 不再注册 `GAS.Runtime.Generated.AbilityCatalogCommitSystem`。
- validation report 显示 generated runtime boundary hit 从 `66` 降到 `47`，lifecycle migration artifact 从 `3` 降到 `2`。
- 诊断脚本已把 ability activation marker、手写 owner-local 写入、schedule contract 退场和 report 分类写成防回流规则。

## 未完成点

- `RuntimeEffectInstant.gen.cs` 和 `RuntimeActiveEffect.gen.cs` 仍属于 `RuntimeLifecycleMigration`，generated runtime lifecycle debt 尚未清零。
- 当前 `AbilityCommitSystem` 仍保留跨 owner lookup 写入面，后续要继续向 chunk-local owner group、显式 fan-in 或更窄 command resolve contract 收敛。
- `RuntimeAbilityActivationSystemTemplate` 旧 raw string 仍残留在 codegen 源文件中，当前不再被 `WriteRuntimeAbilityActivationSystem(...)` 使用，后续可作为清理项删除。
- 本切片未运行 Unity headless AutoChess、Unity Test Runner 或性能 profile，不能作为业务全链路承载结论。
