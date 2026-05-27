///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime.Generated
{
    public static class GasGeneratedDefinitionRowResolver
    {

        public static bool TryGetBlobAbilityDefinitionRow(int code, out GAS.Runtime.HeadlessAutoChessAbilityDefinitionRow row)
        {
            var rows = GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateAbilityRows();
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].AbilityCode != code)
                    continue;
                row = rows[i];
                return true;
            }
            row = default;
            return false;
        }

        public static bool TryGetBlobAttributeSetDefinitionRow(int code, out GAS.Runtime.HeadlessAutoChessAttributeSetDefinitionRow row)
        {
            var rows = GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateAttributeSetRows();
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].AttributeSetCode != code)
                    continue;
                row = rows[i];
                return true;
            }
            row = default;
            return false;
        }

        public static bool TryGetBlobGameplayCueDefinitionRow(int code, out GAS.Runtime.HeadlessAutoChessGameplayCueDefinitionRow row)
        {
            var rows = GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateGameplayCueRows();
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].GameplayCueCode != code)
                    continue;
                row = rows[i];
                return true;
            }
            row = default;
            return false;
        }

        public static bool TryGetBlobGameplayEffectDefinitionRow(int code, out GAS.Runtime.HeadlessAutoChessGameplayEffectDefinitionRow row)
        {
            var rows = GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateGameplayEffectRows();
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].GameplayEffectCode != code)
                    continue;
                row = rows[i];
                return true;
            }
            row = default;
            return false;
        }

        public static bool TryGetBlobGameplayTagDefinitionRow(int code, out GAS.Runtime.HeadlessAutoChessGameplayTagDefinitionRow row)
        {
            var rows = GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateGameplayTagRows();
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].GameplayTagCode != code)
                    continue;
                row = rows[i];
                return true;
            }
            row = default;
            return false;
        }

        public static bool TryGetBlobScenarioSpawnDefinitionRow(int code, out GAS.Runtime.HeadlessAutoChessScenarioSpawnDefinitionRow row)
        {
            var rows = GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateScenarioSpawnRows();
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].ScenarioSpawnCode != code)
                    continue;
                row = rows[i];
                return true;
            }
            row = default;
            return false;
        }

        public static bool TryGetBlobSummonDefinitionRow(int code, out GAS.Runtime.HeadlessAutoChessSummonDefinitionRow row)
        {
            var rows = GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateSummonRows();
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].SummonGameplayEffectCode != code)
                    continue;
                row = rows[i];
                return true;
            }
            row = default;
            return false;
        }

        public static bool TryGetBlobTimelineDefinitionRow(int code, out GAS.Runtime.HeadlessAutoChessTimelineDefinitionRow row)
        {
            var rows = GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateTimelineRows();
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].TimelineId != code)
                    continue;
                row = rows[i];
                return true;
            }
            row = default;
            return false;
        }

        public static bool TryGetBlobUnitDefinitionRow(int code, out GAS.Runtime.HeadlessAutoChessUnitDefinitionRow row)
        {
            var rows = GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateUnitRows();
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].UnitCode != code)
                    continue;
                row = rows[i];
                return true;
            }
            row = default;
            return false;
        }
    }

    public abstract class GasGeneratedDefinitionAuthoring : MonoBehaviour
    {
        public int Code;
    }

    public sealed class BlobAbilityDefinitionAuthoring : GasGeneratedDefinitionAuthoring
    {
    }

    public sealed class BlobAbilityDefinitionBaker : Baker<BlobAbilityDefinitionAuthoring>
    {
        public override void Bake(BlobAbilityDefinitionAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GasGeneratedDefinitionRowResolver.TryGetBlobAbilityDefinitionRow(authoring.Code, out var row))
                return;
            var definitionCode = row.AbilityCode;
            var blob = BlobDefinitionBuilder.BakeBlobAbilityDefinition(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new DefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GeneratedDefinitionBlobComponent<BlobAbilityDefinition> { Value = blob });
        }
    }

    public sealed class BlobAttributeDefinitionAuthoring : GasGeneratedDefinitionAuthoring
    {
        public GAS.Runtime.HeadlessAutoChessAttributeDefinitionRow Row;
    }

    public sealed class BlobAttributeDefinitionBaker : Baker<BlobAttributeDefinitionAuthoring>
    {
        public override void Bake(BlobAttributeDefinitionAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var row = authoring.Row;
            var definitionCode = row.AttributeCode;
            if (definitionCode <= 0)
                return;
            var blob = BlobDefinitionBuilder.BakeBlobAttributeDefinition(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new DefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GeneratedDefinitionBlobComponent<BlobAttributeDefinition> { Value = blob });
        }
    }

    public sealed class BlobAttributeSetDefinitionAuthoring : GasGeneratedDefinitionAuthoring
    {
    }

    public sealed class BlobAttributeSetDefinitionBaker : Baker<BlobAttributeSetDefinitionAuthoring>
    {
        public override void Bake(BlobAttributeSetDefinitionAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GasGeneratedDefinitionRowResolver.TryGetBlobAttributeSetDefinitionRow(authoring.Code, out var row))
                return;
            var definitionCode = row.AttributeSetCode;
            var blob = BlobDefinitionBuilder.BakeBlobAttributeSetDefinition(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new DefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GeneratedDefinitionBlobComponent<BlobAttributeSetDefinition> { Value = blob });
        }
    }

    public sealed class BlobGameplayCueDefinitionAuthoring : GasGeneratedDefinitionAuthoring
    {
    }

    public sealed class BlobGameplayCueDefinitionBaker : Baker<BlobGameplayCueDefinitionAuthoring>
    {
        public override void Bake(BlobGameplayCueDefinitionAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GasGeneratedDefinitionRowResolver.TryGetBlobGameplayCueDefinitionRow(authoring.Code, out var row))
                return;
            var definitionCode = row.GameplayCueCode;
            var blob = BlobDefinitionBuilder.BakeBlobGameplayCueDefinition(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new DefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GeneratedDefinitionBlobComponent<BlobGameplayCueDefinition> { Value = blob });
        }
    }

    public sealed class BlobGameplayEffectDefinitionAuthoring : GasGeneratedDefinitionAuthoring
    {
    }

    public sealed class BlobGameplayEffectDefinitionBaker : Baker<BlobGameplayEffectDefinitionAuthoring>
    {
        public override void Bake(BlobGameplayEffectDefinitionAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GasGeneratedDefinitionRowResolver.TryGetBlobGameplayEffectDefinitionRow(authoring.Code, out var row))
                return;
            var definitionCode = row.GameplayEffectCode;
            var blob = BlobDefinitionBuilder.BakeBlobGameplayEffectDefinition(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new DefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GeneratedDefinitionBlobComponent<BlobGameplayEffectDefinition> { Value = blob });
        }
    }

    public sealed class BlobGameplayTagDefinitionAuthoring : GasGeneratedDefinitionAuthoring
    {
    }

    public sealed class BlobGameplayTagDefinitionBaker : Baker<BlobGameplayTagDefinitionAuthoring>
    {
        public override void Bake(BlobGameplayTagDefinitionAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GasGeneratedDefinitionRowResolver.TryGetBlobGameplayTagDefinitionRow(authoring.Code, out var row))
                return;
            var definitionCode = row.GameplayTagCode;
            var blob = BlobDefinitionBuilder.BakeBlobGameplayTagDefinition(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new DefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GeneratedDefinitionBlobComponent<BlobGameplayTagDefinition> { Value = blob });
        }
    }

    public sealed class BlobScenarioSpawnDefinitionAuthoring : GasGeneratedDefinitionAuthoring
    {
    }

    public sealed class BlobScenarioSpawnDefinitionBaker : Baker<BlobScenarioSpawnDefinitionAuthoring>
    {
        public override void Bake(BlobScenarioSpawnDefinitionAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GasGeneratedDefinitionRowResolver.TryGetBlobScenarioSpawnDefinitionRow(authoring.Code, out var row))
                return;
            var definitionCode = row.ScenarioSpawnCode;
            var blob = BlobDefinitionBuilder.BakeBlobScenarioSpawnDefinition(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new DefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GeneratedDefinitionBlobComponent<BlobScenarioSpawnDefinition> { Value = blob });
        }
    }

    public sealed class BlobSummonDefinitionAuthoring : GasGeneratedDefinitionAuthoring
    {
    }

    public sealed class BlobSummonDefinitionBaker : Baker<BlobSummonDefinitionAuthoring>
    {
        public override void Bake(BlobSummonDefinitionAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GasGeneratedDefinitionRowResolver.TryGetBlobSummonDefinitionRow(authoring.Code, out var row))
                return;
            var definitionCode = row.SummonGameplayEffectCode;
            var blob = BlobDefinitionBuilder.BakeBlobSummonDefinition(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new DefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GeneratedDefinitionBlobComponent<BlobSummonDefinition> { Value = blob });
        }
    }

    public sealed class BlobTimelineDefinitionAuthoring : GasGeneratedDefinitionAuthoring
    {
    }

    public sealed class BlobTimelineDefinitionBaker : Baker<BlobTimelineDefinitionAuthoring>
    {
        public override void Bake(BlobTimelineDefinitionAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GasGeneratedDefinitionRowResolver.TryGetBlobTimelineDefinitionRow(authoring.Code, out var row))
                return;
            var definitionCode = row.TimelineId;
            var blob = BlobDefinitionBuilder.BakeBlobTimelineDefinition(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new DefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GeneratedDefinitionBlobComponent<BlobTimelineDefinition> { Value = blob });
        }
    }

    public sealed class BlobUnitDefinitionAuthoring : GasGeneratedDefinitionAuthoring
    {
    }

    public sealed class BlobUnitDefinitionBaker : Baker<BlobUnitDefinitionAuthoring>
    {
        public override void Bake(BlobUnitDefinitionAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GasGeneratedDefinitionRowResolver.TryGetBlobUnitDefinitionRow(authoring.Code, out var row))
                return;
            var definitionCode = row.UnitCode;
            var blob = BlobDefinitionBuilder.BakeBlobUnitDefinition(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new DefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GeneratedDefinitionBlobComponent<BlobUnitDefinition> { Value = blob });
        }
    }
}
