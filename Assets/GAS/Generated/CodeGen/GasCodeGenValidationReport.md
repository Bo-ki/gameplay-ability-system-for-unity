# GAS CodeGen Validation Report

InputHash: `fce23ddf02035dd89e6ae773f259d4361de190b9a27bdcf3fb7bc5b5b767c44d`
RowCount: `7`
OrphansDeleted: `0`
LegacyRuntimeImplementationArtifacts: `0`
RuntimePureGlueArtifacts: `3`
RuntimeLifecycleSystemArtifacts: `0`
RuntimeWorldOwnerArtifacts: `0`

## Generation Contract

- Runtime v1 gameplay owner、Tick DAG、ECB playback 与 Boundary drain 均由手写 Runtime 持有。
- CodeGen 只保留 Luban normalized rows、Editor asmdef、生命周期防回流 marker 与 validation report。
- 旧 catalog、lookup、Baker、SystemGroup 与 Runtime lifecycle 产物不再生成。

## Rows

| Row | DefinitionKind | CodeField | SourceRows |
| --- | --- | --- | ---: |
| `GAS.Editor.AbilityDefinitionRow` | `Ability` | `AbilityCode` | `9` |
| `GAS.Editor.AttributeDefinitionRow` | `Attribute` | `AttributeCode` | `13` |
| `GAS.Editor.AttributeSetDefinitionRow` | `AttributeSet` | `AttributeSetCode` | `3` |
| `GAS.Editor.GameplayCueDefinitionRow` | `GameplayCue` | `GameplayCueCode` | `4` |
| `GAS.Editor.GameplayEffectDefinitionRow` | `GameplayEffect` | `GameplayEffectCode` | `17` |
| `GAS.Editor.GameplayTagDefinitionRow` | `GameplayTag` | `GameplayTagCode` | `33` |
| `GAS.Editor.TimelineDefinitionRow` | `None` | `TimelineId` | `3` |

## Manifest Entries

| Phase | File | Layer | RuntimeVisible | ArtifactCategory |
| --- | --- | --- | --- | --- |
| `LubanNormalizedRows` | `Assets/GAS/Generated/CodeGen/Editor/LubanNormalizedRows.gen.cs` | `Editor` | `False` | `` |
| `AssemblyDefinition` | `Assets/GAS/Generated/CodeGen/Editor/com.exhard.exgas.generated.editor.asmdef` | `Editor` | `False` | `` |
| `RuntimeLifecycleMigration` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `Runtime` | `True` | `RuntimePureGlue` |
| `RuntimeLifecycleMigration` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `Runtime` | `True` | `RuntimePureGlue` |
| `RuntimeLifecycleMigration` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `Runtime` | `True` | `RuntimePureGlue` |
| `ValidationReport` | `Assets/GAS/Generated/CodeGen/GasCodeGenValidationReport.md` | `Editor` | `False` | `` |
