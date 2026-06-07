# GAS CodeGen Validation Report

InputHash: `c2f4bf1737cd689d5a3e4a17f81df2cb407e677bff1e628fd178d9f5b45c158f`
RowCount: `7`
OrphansDeleted: `0`
LubanCSharpOutput: `Assets/DataGenerated/Luban/CSharp`
LubanJsonOutput: `Assets/DataGenerated/Luban/Json/GAS`
RuntimeForbiddenDependencyHits: `0`
RuntimeGeneratedNamingDebtHits: `0`
GeneratedNamingDebtHits: `0`
GeneratedHotPathRegressionHits: `0`
GeneratedDuplicateMethodHits: `0`
LubanNormalizedRowBoundaryHits: `0`
AutoChessConfigBoundaryHits: `0`
GeneratedAbilityCommitQueryHits: `0`
GeneratedRuntimeBoundaryHits: `118`
GeneratedRuntimeLifecycleHits: `21`
GeneratedRuntimeSystemRegistrationHits: `0`
GeneratedRuntimeStructuralChangeHits: `8`
GeneratedRuntimeOwnershipHits: `3`
GeneratedRuntimeRandomWriteLookupHits: `86`
GeneratedRuntimeManagedConfigHits: `0`
GeneratedRuntimeBoundaryGateMode: `blocking-unclassified-migration-proof`
GeneratedRuntimeUnclassifiedBoundaryHits: `0`

## Rows

| Row | DefinitionKind | CodeField | Blob | Lookup | RowFactory | BlobMembers | SourceRows |
| --- | --- | --- | --- | --- | --- | ---: | ---: |
| `GAS.Editor.AbilityDefinitionRow` | `Ability` | `AbilityCode` | `AbilityDefinitionBlob` | `AbilityDefinitionLookup` | `GAS.Editor.GASGeneratedLubanNormalizedRows.CreateAbilityRows()` | `10` | `9` |
| `GAS.Editor.AttributeDefinitionRow` | `Attribute` | `AttributeCode` | `AttributeDefinitionBlob` | `AttributeDefinitionLookup` | `GAS.Editor.GASGeneratedLubanNormalizedRows.CreateAttributeRows()` | `7` | `13` |
| `GAS.Editor.AttributeSetDefinitionRow` | `AttributeSet` | `AttributeSetCode` | `AttributeSetDefinitionBlob` | `AttributeSetDefinitionLookup` | `GAS.Editor.GASGeneratedLubanNormalizedRows.CreateAttributeSetRows()` | `1` | `3` |
| `GAS.Editor.GameplayCueDefinitionRow` | `GameplayCue` | `GameplayCueCode` | `GameplayCueDefinitionBlob` | `GameplayCueDefinitionLookup` | `GAS.Editor.GASGeneratedLubanNormalizedRows.CreateGameplayCueRows()` | `2` | `4` |
| `GAS.Editor.GameplayEffectDefinitionRow` | `GameplayEffect` | `GameplayEffectCode` | `GameplayEffectDefinitionBlob` | `GameplayEffectDefinitionLookup` | `GAS.Editor.GASGeneratedLubanNormalizedRows.CreateGameplayEffectRows()` | `33` | `17` |
| `GAS.Editor.GameplayTagDefinitionRow` | `GameplayTag` | `GameplayTagCode` | `GameplayTagDefinitionBlob` | `GameplayTagDefinitionLookup` | `GAS.Editor.GASGeneratedLubanNormalizedRows.CreateGameplayTagRows()` | `3` | `33` |
| `GAS.Editor.TimelineDefinitionRow` | `None` | `TimelineId` | `TimelineDefinitionBlob` | `TimelineDefinitionLookup` | `GAS.Editor.GASGeneratedLubanNormalizedRows.CreateTimelineRows()` | `4` | `3` |

## Layer Checks

| Layer | RuntimeVisible | Artifacts | Contract |
| --- | --- | --- | --- |
| Runtime | yes | Runtime asmdef, definition index, definition blobs, static lookups, catalog lookup, runtime definition glue, component type sets | No row, no JSON, no `cfg.*`, no mutable managed registry; runtime asmdef does not reference row source assemblies |
| Baking | no | Editor asmdef, row-based Blob builders, lookup builders, catalog builder, authoring/Baker glue | `Baker<TAuthoring>` only adds components and BlobAssets; row source assemblies are editor/baking-only references |
| Editor/CI | no | Query layout hints, manifest, validation report, dependency scan | Diagnostics only; not gameplay input |

## Luban Compile Boundary

| Artifact | UnityCompiled | RuntimeVisible | Allowed Dependencies | Contract |
| --- | --- | --- | --- | --- |
| Luban generated C# | yes | no direct GAS Runtime Core dependency | `Assets/DataGenerated/Luban/CSharp` may use `cfg.*`, `Luban.Runtime`, `SimpleJSON` | Source row / table API boundary; compile errors are real gate failures, not hidden by moving files out of Assets |
| Luban generated JSON | asset/data | no | `Assets/DataGenerated/Luban/Json/GAS` | Data input for loaders / authoring / baking; not queried by Runtime Core hot path |
| GAS generated Runtime | yes | yes | GAS Runtime, Unity.Collections, Unity.Entities, Unity.Burst | May consume IDs, blobs, unmanaged lookups and component type sets only; no `cfg.*` / JSON reader / managed row reference |
| GAS generated Editor/Baking | Editor only | no | row source assemblies, GAS Editor, GAS Runtime, Unity DOTS | May convert rows into BlobAssets and Baker outputs; no gameplay lifecycle ownership |

## Manifest Entries

| Phase | File | Layer | RuntimeVisible | VersionControlled |
| --- | --- | --- | --- | --- |
| `LubanNormalizedRows` | `Assets/GAS/Generated/CodeGen/Editor/LubanNormalizedRows.gen.cs` | `Editor` | `False` | `True` |
| `AssemblyDefinition` | `Assets/GAS/Generated/CodeGen/Runtime/com.exhard.exgas.generated.runtime.asmdef` | `Runtime` | `True` | `True` |
| `AssemblyDefinition` | `Assets/GAS/Generated/CodeGen/Editor/com.exhard.exgas.generated.editor.asmdef` | `Baking` | `False` | `True` |
| `DefinitionIndex` | `Assets/GAS/Generated/CodeGen/Runtime/DefinitionIndex.gen.cs` | `Runtime` | `True` | `True` |
| `BlobSchema` | `Assets/GAS/Generated/CodeGen/Runtime/BlobSchemas.gen.cs` | `Runtime` | `True` | `True` |
| `BlobSchema` | `Assets/GAS/Generated/CodeGen/Editor/BlobBuilders.gen.cs` | `Baking` | `False` | `True` |
| `StaticLookup` | `Assets/GAS/Generated/CodeGen/Runtime/StaticLookups.gen.cs` | `Runtime` | `True` | `True` |
| `StaticLookup` | `Assets/GAS/Generated/CodeGen/Editor/StaticLookupBuilders.gen.cs` | `Baking` | `False` | `True` |
| `DefinitionCatalog` | `Assets/GAS/Generated/CodeGen/Runtime/DefinitionCatalog.gen.cs` | `Runtime` | `True` | `True` |
| `DefinitionCatalog` | `Assets/GAS/Generated/CodeGen/Editor/DefinitionCatalogBuilder.gen.cs` | `Baking` | `False` | `True` |
| `RuntimeDefinitionGlue` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeDefinitionGlue.gen.cs` | `Runtime` | `True` | `True` |
| `RuntimeDefinitionGlue` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `Runtime` | `True` | `True` |
| `RuntimeDefinitionGlue` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `Runtime` | `True` | `True` |
| `RuntimeDefinitionGlue` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `Runtime` | `True` | `True` |
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
| `CAT-01` | Adopted | Generated catalog stores Ability/GE data in one Blob root with sorted code arrays and range-based child arrays; Runtime glue consumes the catalog by ref and never queries row entities. Timeline rows are flattened at generation time and are not Runtime Core state. |
| `QRY-01` / `JOB-01` / `PRF-05` / `PRF-33` | Partial | Generated Runtime stored queries use `state.GetEntityQuery(EntityQueryDesc)` and generated hot path gate rejects `SystemAPI.QueryBuilder().Build()` / `CreateEntityQuery` regressions. Full chunk-job traversal remains a later optimization pass. |
| `SC-01` / `PRF-02` / `ECB-03` | Deferred | CodeGen does not hide structural changes; runtime playback ownership remains a Runtime Core contract. |
| `BUR-01` / `BUR-02` | Adopted | Runtime-visible lookup data is unmanaged / Blob based; managed delegate registries remain forbidden. |
| `NAT-01` / `NAT-04` | Adopted | Generated lookup structs expose `OwnsMemory`; `Dispose()` only releases NativeArray and Blob memory for owning instances. |
| `ASM-01` | Adopted | Generated runtime/editor asmdefs are produced by the same pipeline; only the editor/baking asmdef references row source assemblies. |
| `12-命名规范Spec` | Adopted | New generated Core names use `GAS*` for framework artifacts and `*DefinitionBlob` for Blob root types. |
| `ODF-13` | Adopted | Luban generated C# is Unity-compiled boundary code, while GAS generated Runtime remains free of managed Luban / JSON dependencies. |
| `ODF-*` | Deferred | Official DOTS coverage is reported here as a gate; Player/AOT evidence is still a later CI artifact. |

## Generated Runtime Boundary Gate

CurrentMode: `blocking-unclassified-migration-proof`
Target: SourceGenerator emits definition / blob / lookup / pure glue / validation only; Runtime lifecycle and ownership stay in handwritten ECS systems.
AllowedMigrationProof: `RuntimeAbilityActivation.gen.cs`, `RuntimeEffectInstant.gen.cs`, `RuntimeActiveEffect.gen.cs` lifecycle / lookup / structural owner hits must remain explicitly classified and bound to R2/R3/R5 exit work.

| Rule | Gate | Disposition | File | Line | Evidence |
| --- | --- | --- | --- | ---: | --- |
| `NAT-01/NAT-03` | `native-container-owner` | `BootstrapDefinitionOwner` | `Assets/GAS/Generated/CodeGen/Runtime/DefinitionCatalog.gen.cs` | `81` | `public static BlobAssetReference<GASDefinitionCatalogBlob> BuildCatalog(Allocator allocator = Allocator.Persistent)` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `18` | `public partial struct AbilityCatalogCommitSystem : ISystem` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `22` | `public void OnCreate(ref SystemState state)` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `38` | `public void OnUpdate(ref SystemState state)` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `59` | `TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `61` | `TemporaryTagSourceLookup = SystemAPI.GetBufferLookup<TagTemporarySourceBuffer>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `64` | `CancelRequestLookup = SystemAPI.GetComponentLookup<AbilityCancelRequestComponent>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `67` | `StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `68` | `CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `70` | `FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `85` | `public ComponentLookup<TagMaskComponent> TagMaskLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `87` | `public BufferLookup<TagTemporarySourceBuffer> TemporaryTagSourceLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `90` | `public ComponentLookup<AbilityCancelRequestComponent> CancelRequestLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `93` | `public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `94` | `public BufferLookup<GEEffectCommandBuffer> CommandLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `96` | `public BufferLookup<GameplayEventBuffer> FactLookup;` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `19` | `public partial struct GEEffectCommandCatalogNormalizeSystem : ISystem` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `23` | `public void OnCreate(ref SystemState state)` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `36` | `public void OnUpdate(ref SystemState state)` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `85` | `public partial struct GASActiveEffectMutationApplySystem : ISystem` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `89` | `public void OnCreate(ref SystemState state)` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `115` | `public void OnUpdate(ref SystemState state)` |
| `SC-01/ECB-03` | `structural-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `131` | `.CreateCommandBuffer(state.WorldUnmanaged);` |
| `NAT-01/NAT-03` | `native-container-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `136` | `var activeMutationCommands = new NativeList<GEEffectCommandBuffer>(commandCapacity, state.WorldUpdateAllocator);` |
| `NAT-01/NAT-03` | `native-container-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `138` | `new NativeParallelHashMap<Entity, GASGeneratedActiveEffectRuntime.ActiveMutationCommandRange>(` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `144` | `StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `172` | `StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `173` | `CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `174` | `CommandSetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `176` | `SystemAPI.GetBufferLookup<AttributeOwnerMarkerRequestBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `177` | `AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `180` | `SystemAPI.GetBufferLookup<AbilityLifecycleRequestBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `181` | `FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: false),` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `196` | `public partial struct GASActiveEffectPreTickSystem : ISystem` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `200` | `public void OnCreate(ref SystemState state)` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `215` | `public void OnUpdate(ref SystemState state)` |
| `SC-01/ECB-03` | `structural-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `231` | `.CreateCommandBuffer(state.WorldUnmanaged);` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `238` | `SystemAPI.GetBufferLookup<ActiveGameplayEffectCleanupRecordBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `240` | `SystemAPI.GetBufferLookup<ActiveGameplayEffectSetByCallerValueBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `241` | `AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `242` | `ActiveModifierLookup = SystemAPI.GetBufferLookup<AttributeActiveModifierBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `244` | `SystemAPI.GetBufferLookup<AttributeOwnerMarkerRequestBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `245` | `TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `247` | `TagSourceLookup = SystemAPI.GetBufferLookup<TagTemporarySourceBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `248` | `AbilitySlotLookup = SystemAPI.GetBufferLookup<AbilitySlotBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `249` | `AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `254` | `StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `255` | `CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `256` | `CommandSetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `257` | `MutationLookup = SystemAPI.GetBufferLookup<ActiveEffectMutationBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `259` | `SystemAPI.GetBufferLookup<AbilityLifecycleRequestBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `260` | `FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: false),` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `276` | `public partial struct GASActiveEffectRemoveSystem : ISystem` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `280` | `public void OnCreate(ref SystemState state)` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `295` | `public void OnUpdate(ref SystemState state)` |
| `SC-01/ECB-03` | `structural-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `307` | `.CreateCommandBuffer(state.WorldUnmanaged);` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `316` | `SystemAPI.GetBufferLookup<ActiveGameplayEffectCleanupRecordBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `318` | `SystemAPI.GetBufferLookup<ActiveGameplayEffectSetByCallerValueBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `319` | `AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `320` | `ActiveModifierLookup = SystemAPI.GetBufferLookup<AttributeActiveModifierBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `322` | `SystemAPI.GetBufferLookup<AttributeOwnerMarkerRequestBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `323` | `TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `325` | `TagSourceLookup = SystemAPI.GetBufferLookup<TagTemporarySourceBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `326` | `AbilitySlotLookup = SystemAPI.GetBufferLookup<AbilitySlotBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `327` | `AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `331` | `StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `332` | `CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `333` | `CommandSetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `334` | `MutationLookup = SystemAPI.GetBufferLookup<ActiveEffectMutationBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `336` | `SystemAPI.GetBufferLookup<AbilityLifecycleRequestBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `337` | `FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: false),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `364` | `public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `511` | `public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `512` | `public BufferLookup<GEEffectCommandBuffer> CommandLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `513` | `public BufferLookup<GESetByCallerValueBuffer> CommandSetByCallerLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `514` | `public BufferLookup<AttributeOwnerMarkerRequestBuffer> AttributeOwnerMarkerRequestLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `515` | `public ComponentLookup<AbilityStateComponent> AbilityStateLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `517` | `public BufferLookup<AbilityLifecycleRequestBuffer> AbilityLifecycleRequestLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `518` | `public BufferLookup<GameplayEventBuffer> FactLookup;` |
| `SC-01/ECB-03` | `structural-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `519` | `public EntityCommandBuffer StructuralEcb;` |
| `SC-01/ECB-03` | `structural-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1105` | `var ability = StructuralEcb.CreateEntity(GrantedAbilityArchetype);` |
| `SC-01/ECB-03` | `structural-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1222` | `StructuralEcb.DestroyEntity(ability);` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1624` | `public BufferLookup<ActiveGameplayEffectCleanupRecordBuffer> CleanupRecordLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1625` | `public BufferLookup<ActiveGameplayEffectSetByCallerValueBuffer> SetByCallerSnapshotLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1626` | `public BufferLookup<AttributeValueBuffer> AttributeLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1627` | `public BufferLookup<AttributeActiveModifierBuffer> ActiveModifierLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1628` | `public BufferLookup<AttributeOwnerMarkerRequestBuffer> AttributeOwnerMarkerRequestLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1629` | `public ComponentLookup<TagMaskComponent> TagMaskLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1631` | `public BufferLookup<TagTemporarySourceBuffer> TagSourceLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1632` | `public BufferLookup<AbilitySlotBuffer> AbilitySlotLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1636` | `public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1637` | `public BufferLookup<GEEffectCommandBuffer> CommandLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1638` | `public BufferLookup<GESetByCallerValueBuffer> CommandSetByCallerLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1639` | `public BufferLookup<ActiveEffectMutationBuffer> MutationLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1640` | `public BufferLookup<AbilityLifecycleRequestBuffer> AbilityLifecycleRequestLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1641` | `public BufferLookup<GameplayEventBuffer> FactLookup;` |
| `SC-01/ECB-03` | `structural-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1642` | `public EntityCommandBuffer StructuralEcb;` |
| `SC-01/ECB-03` | `structural-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `2283` | `StructuralEcb.DestroyEntity(ability);` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `17` | `public partial struct GEEffectSpecBuildSystem : ISystem` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `19` | `public void OnCreate(ref SystemState state)` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `25` | `public void OnUpdate(ref SystemState state)` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `38` | `StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `40` | `SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `41` | `SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `52` | `public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `54` | `public BufferLookup<GEEffectSpecBuffer> SpecLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `55` | `public BufferLookup<GESetByCallerValueBuffer> SetByCallerLookup;` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `211` | `public partial struct GASAttributeSetReduceApplySystem : ISystem` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `213` | `public void OnCreate(ref SystemState state)` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `219` | `public void OnUpdate(ref SystemState state)` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `232` | `StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `233` | `SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `234` | `DeltaLookup = SystemAPI.GetBufferLookup<AttributeModifierBuffer>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `236` | `AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `246` | `public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `247` | `public BufferLookup<GEEffectSpecBuffer> SpecLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `248` | `public BufferLookup<AttributeModifierBuffer> DeltaLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `250` | `public BufferLookup<AttributeValueBuffer> AttributeLookup;` |

## Generated Runtime Hot Path Gate

| Rule | Hit | File | Line | Evidence |
| --- | --- | --- | ---: | --- |
| `QRY-01` / `PRF-05` / `BUR-01` / `EN-03` | none | - | - | generated runtime passed static hot path regression gate |

## Generated Ability Commit Query Gate

| Rule | File | Line | Evidence |
| --- | --- | ---: | --- |
| `EN-03/ABILITY-COMMIT-01` | - | - | ability commit query ignores enableable state and filters by enabled `AbilityCommitRequestComponent` mask inside the job |

## Generated Duplicate Method Gate

| Rule | File | Type | Signature | FirstLine | DuplicateLine |
| --- | --- | --- | --- | ---: | ---: |
| `GEN-01` | - | - | - | - | - |

## Luban Normalized Row Boundary Gate

| Rule | File | Line | Evidence |
| --- | --- | ---: | --- |
| `ODF-13/BLOB-01` | - | - | normalized rows are editor-only literal factories without JSON / Luban runtime dependencies |

## AutoChess Config Boundary Gate

| Rule | File | Line | Evidence |
| --- | --- | ---: | --- |
| `AUTOCHESS-CONFIG-SPLIT` | - | - | AutoChessDemoConfig is generated by the separated AutoChessDemo sourcegen pass, not by the GAS Core validation report |
