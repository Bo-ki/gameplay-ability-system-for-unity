# GAS CodeGen Validation Report

InputHash: `a39391dc4d25597299ad8449396d6b083033a46df2e2e53bc47954014b12f7c6`
RowCount: `10`
OrphansDeleted: `1`
LubanCSharpOutput: `Assets/DataGenerated/Luban/CSharp`
LubanJsonOutput: `Assets/DataGenerated/Luban/Json/GAS`
RuntimeForbiddenDependencyHits: `0`
RuntimeGeneratedNamingDebtHits: `0`
GeneratedNamingDebtHits: `0`

## Rows

| Row | DefinitionKind | CodeField | Blob | Lookup | RowFactory | BlobMembers | SourceRows |
| --- | --- | --- | --- | --- | --- | ---: | ---: |
| `GAS.Runtime.HeadlessAutoChessAbilityDefinitionRow` | `Ability` | `AbilityCode` | `AbilityDefinitionBlob` | `AbilityDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateAbilityRows()` | `7` | `7` |
| `GAS.Runtime.HeadlessAutoChessAttributeDefinitionRow` | `Attribute` | `AttributeCode` | `AttributeDefinitionBlob` | `AttributeDefinitionLookup` | `manual authoring row` | `7` | `6` |
| `GAS.Runtime.HeadlessAutoChessAttributeSetDefinitionRow` | `AttributeSet` | `AttributeSetCode` | `AttributeSetDefinitionBlob` | `AttributeSetDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateAttributeSetRows()` | `1` | `1` |
| `GAS.Runtime.HeadlessAutoChessGameplayCueDefinitionRow` | `GameplayCue` | `GameplayCueCode` | `GameplayCueDefinitionBlob` | `GameplayCueDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateGameplayCueRows()` | `2` | `1` |
| `GAS.Runtime.HeadlessAutoChessGameplayEffectDefinitionRow` | `GameplayEffect` | `GameplayEffectCode` | `GameplayEffectDefinitionBlob` | `GameplayEffectDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateGameplayEffectRows()` | `27` | `33` |
| `GAS.Runtime.HeadlessAutoChessGameplayTagDefinitionRow` | `GameplayTag` | `GameplayTagCode` | `GameplayTagDefinitionBlob` | `GameplayTagDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateGameplayTagRows()` | `3` | `19` |
| `GAS.Runtime.HeadlessAutoChessScenarioSpawnDefinitionRow` | `None` | `ScenarioSpawnCode` | `ScenarioSpawnDefinitionBlob` | `ScenarioSpawnDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateScenarioSpawnRows()` | `7` | `4` |
| `GAS.Runtime.HeadlessAutoChessSummonDefinitionRow` | `None` | `SummonGameplayEffectCode` | `SummonDefinitionBlob` | `SummonDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateSummonRows()` | `16` | `1` |
| `GAS.Runtime.HeadlessAutoChessTimelineDefinitionRow` | `TimelineAbility` | `TimelineId` | `TimelineDefinitionBlob` | `TimelineDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateTimelineRows()` | `5` | `7` |
| `GAS.Runtime.HeadlessAutoChessUnitDefinitionRow` | `None` | `UnitCode` | `UnitDefinitionBlob` | `UnitDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateUnitRows()` | `20` | `5` |

## Layer Checks

| Layer | RuntimeVisible | Artifacts | Contract |
| --- | --- | --- | --- |
| Runtime | yes | Runtime asmdef, definition index, definition blobs, static lookups, component type sets | No row, no JSON, no `cfg.*`, no mutable managed registry; runtime asmdef does not reference row source assemblies |
| Baking | no | Editor asmdef, row-based Blob builders, lookup builders, authoring/Baker glue | `Baker<TAuthoring>` only adds outputs and calls `AddBlobAsset()`; row source assemblies are editor/baking-only references |
| Editor/CI | no | Query layout hints, manifest, validation report, dependency scan | Diagnostics only; not gameplay input |

## Luban Compile Boundary

| Artifact | UnityCompiled | RuntimeVisible | Allowed Dependencies | Contract |
| --- | --- | --- | --- | --- |
| Luban generated C# | yes | no direct GAS Runtime Core dependency | `Assets/DataGenerated/Luban/CSharp` may use `cfg.*`, `Luban.Runtime`, `SimpleJSON` | Source row / table API boundary; compile errors are real gate failures, not hidden by moving files out of Assets |
| Luban generated JSON | asset/data | no | `Assets/DataGenerated/Luban/Json/GAS` | Data input for loaders / authoring / baking; not queried by Runtime Core hot path |
| GAS generated Runtime | yes | yes | GAS Runtime, Unity.Collections, Unity.Entities | May consume IDs, blobs, unmanaged lookups and component type sets only; no `cfg.*` / JSON reader / managed row reference |
| GAS generated Editor/Baking | Editor only | no | row source assemblies, GAS Editor, GAS Runtime, Unity DOTS | May convert rows into BlobAssets and Baker outputs; no gameplay lifecycle ownership |

## Manifest Entries

| Phase | File | Layer | RuntimeVisible | VersionControlled |
| --- | --- | --- | --- | --- |
| `AssemblyDefinition` | `Assets/GAS/Generated/CodeGen/Runtime/com.exhard.exgas.generated.runtime.asmdef` | `Runtime` | `True` | `True` |
| `AssemblyDefinition` | `Assets/GAS/Generated/CodeGen/Editor/com.exhard.exgas.generated.editor.asmdef` | `Baking` | `False` | `True` |
| `DefinitionIndex` | `Assets/GAS/Generated/CodeGen/Runtime/DefinitionIndex.gen.cs` | `Runtime` | `True` | `True` |
| `BlobSchema` | `Assets/GAS/Generated/CodeGen/Runtime/BlobSchemas.gen.cs` | `Runtime` | `True` | `True` |
| `BlobSchema` | `Assets/GAS/Generated/CodeGen/Editor/BlobBuilders.gen.cs` | `Baking` | `False` | `True` |
| `StaticLookup` | `Assets/GAS/Generated/CodeGen/Runtime/StaticLookups.gen.cs` | `Runtime` | `True` | `True` |
| `StaticLookup` | `Assets/GAS/Generated/CodeGen/Editor/StaticLookupBuilders.gen.cs` | `Baking` | `False` | `True` |
| `BakerGlue` | `Assets/GAS/Generated/CodeGen/Runtime/DefinitionComponents.gen.cs` | `Runtime` | `True` | `True` |
| `BakerGlue` | `Assets/GAS/Generated/CodeGen/Editor/BakerGlue.gen.cs` | `Baking` | `False` | `True` |
| `ComponentTypeSet` | `Assets/GAS/Generated/CodeGen/Runtime/ComponentTypeSets.gen.cs` | `Runtime` | `True` | `True` |
| `QueryLayout` | `Assets/GAS/Generated/CodeGen/Editor/QueryLayouts.gen.cs` | `Editor/CI` | `False` | `True` |
| `ValidationReport` | `Assets/GAS/Generated/CodeGen/GasCodeGenValidationReport.md` | `Editor/CI` | `False` | `True` |

## DOTS And Naming Checks

| Rule | Status | Reason |
| --- | --- | --- |
| `BAKE-01` / `CASE-39` | Adopted | Generated Baker glue only adds components and BlobAssets; it does not read other Baker outputs. |
| `BAKE-02` / `CASE-40` | Adopted | Generated Bakers do not cache instance state. |
| `BLOB-01` / `BLOB-02` / `CASE-24` | Adopted | Static definitions are emitted as immutable Blob root structs and builder methods are Editor/Baking side. |
| `QRY-01` / `JOB-01` / `PRF-05` | Deferred | CodeGen emits query layout descriptions only; it does not generate runtime lifecycle systems or hot path traversal. |
| `SC-01` / `PRF-02` / `ECB-03` | Deferred | CodeGen does not hide structural changes; runtime playback ownership remains a Runtime Core contract. |
| `BUR-01` / `BUR-02` | Adopted | Runtime-visible lookup data is unmanaged / Blob based; managed delegate registries remain forbidden. |
| `NAT-01` / `NAT-04` | Adopted | Generated lookup structs expose `OwnsMemory`; `Dispose()` only releases NativeArray and Blob memory for owning instances. |
| `ASM-01` | Adopted | Generated runtime/editor asmdefs are produced by the same pipeline; only the editor/baking asmdef references row source assemblies. |
| `12-命名规范Spec` | Adopted | New generated Core names use `GAS*` for framework artifacts and `*DefinitionBlob` for Blob root types. |
| `ODF-13` | Adopted | Luban generated C# is Unity-compiled boundary code, while GAS generated Runtime remains free of managed Luban / JSON dependencies. |
| `ODF-*` | Deferred | Official DOTS coverage is reported here as a gate; Player/AOT evidence is still a later CI artifact. |
