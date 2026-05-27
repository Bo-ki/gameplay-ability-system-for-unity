# GAS CodeGen Validation Report

InputHash: `a39391dc4d25597299ad8449396d6b083033a46df2e2e53bc47954014b12f7c6`
RowCount: `10`
RuntimeForbiddenDependencyHits: `0`

## Rows

| Row | DefinitionKind | CodeField | Blob | Lookup | RowFactory | BlobMembers | SourceRows |
| --- | --- | --- | --- | --- | --- | ---: | ---: |
| `GAS.Runtime.HeadlessAutoChessAbilityDefinitionRow` | `Ability` | `AbilityCode` | `BlobAbilityDefinition` | `BlobAbilityDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateAbilityRows()` | `7` | `7` |
| `GAS.Runtime.HeadlessAutoChessAttributeDefinitionRow` | `Attribute` | `AttributeCode` | `BlobAttributeDefinition` | `BlobAttributeDefinitionLookup` | `manual authoring row` | `7` | `6` |
| `GAS.Runtime.HeadlessAutoChessAttributeSetDefinitionRow` | `AttributeSet` | `AttributeSetCode` | `BlobAttributeSetDefinition` | `BlobAttributeSetDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateAttributeSetRows()` | `1` | `1` |
| `GAS.Runtime.HeadlessAutoChessGameplayCueDefinitionRow` | `GameplayCue` | `GameplayCueCode` | `BlobGameplayCueDefinition` | `BlobGameplayCueDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateGameplayCueRows()` | `2` | `1` |
| `GAS.Runtime.HeadlessAutoChessGameplayEffectDefinitionRow` | `GameplayEffect` | `GameplayEffectCode` | `BlobGameplayEffectDefinition` | `BlobGameplayEffectDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateGameplayEffectRows()` | `27` | `33` |
| `GAS.Runtime.HeadlessAutoChessGameplayTagDefinitionRow` | `GameplayTag` | `GameplayTagCode` | `BlobGameplayTagDefinition` | `BlobGameplayTagDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateGameplayTagRows()` | `3` | `19` |
| `GAS.Runtime.HeadlessAutoChessScenarioSpawnDefinitionRow` | `None` | `ScenarioSpawnCode` | `BlobScenarioSpawnDefinition` | `BlobScenarioSpawnDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateScenarioSpawnRows()` | `7` | `4` |
| `GAS.Runtime.HeadlessAutoChessSummonDefinitionRow` | `None` | `SummonGameplayEffectCode` | `BlobSummonDefinition` | `BlobSummonDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateSummonRows()` | `16` | `1` |
| `GAS.Runtime.HeadlessAutoChessTimelineDefinitionRow` | `TimelineAbility` | `TimelineId` | `BlobTimelineDefinition` | `BlobTimelineDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateTimelineRows()` | `5` | `7` |
| `GAS.Runtime.HeadlessAutoChessUnitDefinitionRow` | `None` | `UnitCode` | `BlobUnitDefinition` | `BlobUnitDefinitionLookup` | `GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateUnitRows()` | `20` | `5` |

## Layer Checks

- Runtime artifacts: Blob schemas, static lookups, component type sets, query layouts.
- Baking artifacts: row-based Blob builders, lookup builders, authoring/Baker glue.
- Row references are generated only under `#if UNITY_EDITOR` files.
- Baker glue emits `Baker<TAuthoring>`, `AddBlobAsset()` and `GeneratedDefinitionBlobComponent<T>` writes.
- AutoChess runtime artifacts now include attribute components, tag masks, unit configs, MMC evaluator and scenario build plan.
- Runtime Core must continue to reject `cfg.*`, `XLuban`, `SimpleJSON`, managed row, and JSON reader references.
