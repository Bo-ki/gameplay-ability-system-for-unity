# GAS CodeGen Validation Report

ManifestVersion: `1`
GeneratorVersion: `EX-GAS-CodeGen-v1`
InputHash: `f4b72a252c3157ea950e958d02d901ec7a9deddc08f965e34301644953cb774b`
RowCount: `7`
OrphansDeleted: `0`
LegacyRuntimeImplementationArtifacts: `0`
RuntimePureGlueArtifacts: `3`
RuntimeLifecycleSystemArtifacts: `0`
RuntimeWorldOwnerArtifacts: `0`
ManifestContractErrors: `0`
MissingRequiredArtifacts: `0`

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

| Phase | File | Layer | RuntimeVisible | ArtifactCategory | GeneratedArtifactOwner | MayAllocate | MayOwnLifecycle | MayOwnStructuralChange | MayOwnNativeContainer |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `LubanNormalizedRows` | `Assets/GAS/Generated/CodeGen/Editor/LubanNormalizedRows.gen.cs` | `Editor` | `False` | `NormalizedDefinitionRow` | `DefinitionCodeGen` | `False` | `False` | `False` | `False` |
| `AssemblyDefinition` | `Assets/GAS/Generated/CodeGen/Runtime/com.exhard.exgas.generated.runtime.asmdef` | `Runtime` | `True` | `AssemblyDefinition` | `DefinitionCodeGen` | `False` | `False` | `False` | `False` |
| `AssemblyDefinition` | `Assets/GAS/Generated/CodeGen/Editor/com.exhard.exgas.generated.editor.asmdef` | `Editor` | `False` | `AssemblyDefinition` | `DefinitionCodeGen` | `False` | `False` | `False` | `False` |
| `RuntimeLifecycleMigration` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `Runtime` | `True` | `RuntimePureGlue` | `DefinitionCodeGen` | `False` | `False` | `False` | `False` |
| `RuntimeLifecycleMigration` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `Runtime` | `True` | `RuntimePureGlue` | `DefinitionCodeGen` | `False` | `False` | `False` | `False` |
| `RuntimeLifecycleMigration` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `Runtime` | `True` | `RuntimePureGlue` | `DefinitionCodeGen` | `False` | `False` | `False` | `False` |
| `ValidationReport` | `Assets/GAS/Generated/CodeGen/GasCodeGenValidationReport.md` | `Editor` | `False` | `ValidationArtifact` | `EditorCi` | `False` | `False` | `False` | `False` |


