# GAS CodeGen Validation Report

InputHash: `3147276489687716b093ae7874cdb06b0d64f3b8ed9ce80d2daed29d5e1495bc`
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
GeneratedRuntimeBoundaryHits: `63`
GeneratedRuntimePureGlueArtifacts: `1`
GeneratedRuntimeLifecycleMigrationArtifacts: `3`
GeneratedRuntimeLifecycleHits: `9`
GeneratedRuntimeSystemRegistrationHits: `0`
GeneratedRuntimeStructuralChangeHits: `5`
GeneratedRuntimeOwnershipHits: `1`
GeneratedRuntimeRandomWriteLookupHits: `48`
GeneratedRuntimeManagedConfigHits: `0`
GeneratedRuntimeBoundaryGateMode: `blocking-unclassified-lifecycle-migration`
GeneratedRuntimeUnclassifiedBoundaryHits: `0`

## Rows

| Row | DefinitionKind | CodeField | Blob | Lookup | RowFactory | BlobMembers | SourceRows |
| --- | --- | --- | --- | --- | --- | ---: | ---: |
| `GAS.Editor.AbilityDefinitionRow` | `Ability` | `AbilityCode` | `AbilityDefinitionBlob` | `AbilityDefinitionLookup` | `GAS.Editor.GASGeneratedLubanNormalizedRows.CreateAbilityRows()` | `16` | `9` |
| `GAS.Editor.AttributeDefinitionRow` | `Attribute` | `AttributeCode` | `AttributeDefinitionBlob` | `AttributeDefinitionLookup` | `GAS.Editor.GASGeneratedLubanNormalizedRows.CreateAttributeRows()` | `7` | `13` |
| `GAS.Editor.AttributeSetDefinitionRow` | `AttributeSet` | `AttributeSetCode` | `AttributeSetDefinitionBlob` | `AttributeSetDefinitionLookup` | `GAS.Editor.GASGeneratedLubanNormalizedRows.CreateAttributeSetRows()` | `1` | `3` |
| `GAS.Editor.GameplayCueDefinitionRow` | `GameplayCue` | `GameplayCueCode` | `GameplayCueDefinitionBlob` | `GameplayCueDefinitionLookup` | `GAS.Editor.GASGeneratedLubanNormalizedRows.CreateGameplayCueRows()` | `2` | `4` |
| `GAS.Editor.GameplayEffectDefinitionRow` | `GameplayEffect` | `GameplayEffectCode` | `GameplayEffectDefinitionBlob` | `GameplayEffectDefinitionLookup` | `GAS.Editor.GASGeneratedLubanNormalizedRows.CreateGameplayEffectRows()` | `45` | `17` |
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
| `RuntimeLifecycleMigration` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `Runtime` | `True` | `True` |
| `RuntimeLifecycleMigration` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `Runtime` | `True` | `True` |
| `RuntimeLifecycleMigration` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `Runtime` | `True` | `True` |
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

CurrentMode: `blocking-unclassified-lifecycle-migration`
Target: SourceGenerator emits definition / blob / lookup / pure glue / validation only; Runtime lifecycle and ownership stay in handwritten ECS systems.
AllowedMigrationProof: only manifest artifacts categorized as `RuntimeLifecycleMigration` may carry lifecycle / lookup / structural owner hits, and they remain bound to R2/R3/R5 exit work.

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
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `15` | `public partial struct GEEffectSpecBuildSystem : ISystem` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `17` | `public void OnCreate(ref SystemState state)` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `23` | `public void OnUpdate(ref SystemState state)` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `36` | `StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `38` | `SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `39` | `SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `51` | `public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `53` | `public BufferLookup<GEEffectSpecBuffer> SpecLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `54` | `public BufferLookup<GESetByCallerValueBuffer> SetByCallerLookup;` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `223` | `public partial struct GASAttributeSetReduceApplySystem : ISystem` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `225` | `public void OnCreate(ref SystemState state)` |
| `SYS-01/SYS-03` | `lifecycle-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `231` | `public void OnUpdate(ref SystemState state)` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `244` | `StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `245` | `SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `246` | `DeltaLookup = SystemAPI.GetBufferLookup<AttributeModifierBuffer>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `248` | `AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(),` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `258` | `public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `259` | `public BufferLookup<GEEffectSpecBuffer> SpecLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `260` | `public BufferLookup<AttributeModifierBuffer> DeltaLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `262` | `public BufferLookup<AttributeValueBuffer> AttributeLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `205` | `public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `419` | `public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `420` | `public BufferLookup<GEEffectCommandBuffer> CommandLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `421` | `public BufferLookup<GESetByCallerValueBuffer> CommandSetByCallerLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `422` | `public BufferLookup<AttributeOwnerMarkerRequestBuffer> AttributeOwnerMarkerRequestLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `423` | `public ComponentLookup<AbilityStateComponent> AbilityStateLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `425` | `public BufferLookup<AbilityLifecycleRequestBuffer> AbilityLifecycleRequestLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `426` | `public BufferLookup<GameplayEventBuffer> FactLookup;` |
| `SC-01/ECB-03` | `structural-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `427` | `public EntityCommandBuffer StructuralEcb;` |
| `SC-01/ECB-03` | `structural-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1073` | `var ability = StructuralEcb.CreateEntity(GrantedAbilityArchetype);` |
| `SC-01/ECB-03` | `structural-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1190` | `StructuralEcb.DestroyEntity(ability);` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1697` | `public BufferLookup<ActiveGameplayEffectCleanupRecordBuffer> CleanupRecordLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1698` | `public BufferLookup<ActiveGameplayEffectSetByCallerValueBuffer> SetByCallerSnapshotLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1699` | `public BufferLookup<AttributeValueBuffer> AttributeLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1700` | `public BufferLookup<AttributeActiveModifierBuffer> ActiveModifierLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1701` | `public BufferLookup<AttributeOwnerMarkerRequestBuffer> AttributeOwnerMarkerRequestLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1702` | `public ComponentLookup<TagMaskComponent> TagMaskLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1704` | `public BufferLookup<TagTemporarySourceBuffer> TagSourceLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1705` | `public BufferLookup<AbilitySlotBuffer> AbilitySlotLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1709` | `public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1710` | `public BufferLookup<GEEffectCommandBuffer> CommandLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1711` | `public BufferLookup<GESetByCallerValueBuffer> CommandSetByCallerLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1712` | `public BufferLookup<ActiveEffectMutationBuffer> MutationLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1713` | `public BufferLookup<AbilityLifecycleRequestBuffer> AbilityLifecycleRequestLookup;` |
| `QRY-04/PRF-06/PRF-19` | `random-lookup-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1714` | `public BufferLookup<GameplayEventBuffer> FactLookup;` |
| `SC-01/ECB-03` | `structural-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `1715` | `public EntityCommandBuffer StructuralEcb;` |
| `SC-01/ECB-03` | `structural-owner` | `MigrationProofOnly` | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `2457` | `StructuralEcb.DestroyEntity(ability);` |

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
