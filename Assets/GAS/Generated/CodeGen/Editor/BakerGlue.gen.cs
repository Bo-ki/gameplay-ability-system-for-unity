///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime.Generated
{
    public static class GASGeneratedDefinitionRowResolver
    {

        public static bool TryGetAbilityDefinitionBlobRow(int code, out GAS.Runtime.HeadlessAutoChessAbilityDefinitionRow row)
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

        public static bool TryGetAttributeSetDefinitionBlobRow(int code, out GAS.Runtime.HeadlessAutoChessAttributeSetDefinitionRow row)
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

        public static bool TryGetGameplayCueDefinitionBlobRow(int code, out GAS.Runtime.HeadlessAutoChessGameplayCueDefinitionRow row)
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

        public static bool TryGetGameplayEffectDefinitionBlobRow(int code, out GAS.Runtime.HeadlessAutoChessGameplayEffectDefinitionRow row)
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

        public static bool TryGetGameplayTagDefinitionBlobRow(int code, out GAS.Runtime.HeadlessAutoChessGameplayTagDefinitionRow row)
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

        public static bool TryGetScenarioSpawnDefinitionBlobRow(int code, out GAS.Runtime.HeadlessAutoChessScenarioSpawnDefinitionRow row)
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

        public static bool TryGetSummonDefinitionBlobRow(int code, out GAS.Runtime.HeadlessAutoChessSummonDefinitionRow row)
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

        public static bool TryGetTimelineDefinitionBlobRow(int code, out GAS.Runtime.HeadlessAutoChessTimelineDefinitionRow row)
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

        public static bool TryGetUnitDefinitionBlobRow(int code, out GAS.Runtime.HeadlessAutoChessUnitDefinitionRow row)
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

    public abstract class GASGeneratedDefinitionAuthoring : MonoBehaviour
    {
        public int Code;
    }

    public sealed class AbilityDefinitionBlobAuthoring : GASGeneratedDefinitionAuthoring
    {
    }

    public sealed class AbilityDefinitionBlobBaker : Baker<AbilityDefinitionBlobAuthoring>
    {
        public override void Bake(AbilityDefinitionBlobAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GASGeneratedDefinitionRowResolver.TryGetAbilityDefinitionBlobRow(authoring.Code, out var row))
                return;
            var definitionCode = row.AbilityCode;
            var blob = GASGeneratedDefinitionBlobBuilder.BuildAbilityDefinitionBlob(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new GASDefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GASGeneratedDefinitionBlobComponent<AbilityDefinitionBlob> { Value = blob });
        }
    }

    public sealed class AttributeDefinitionBlobAuthoring : GASGeneratedDefinitionAuthoring
    {
        public GAS.Runtime.HeadlessAutoChessAttributeDefinitionRow Row;
    }

    public sealed class AttributeDefinitionBlobBaker : Baker<AttributeDefinitionBlobAuthoring>
    {
        public override void Bake(AttributeDefinitionBlobAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var row = authoring.Row;
            var definitionCode = row.AttributeCode;
            if (definitionCode <= 0)
                return;
            var blob = GASGeneratedDefinitionBlobBuilder.BuildAttributeDefinitionBlob(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new GASDefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GASGeneratedDefinitionBlobComponent<AttributeDefinitionBlob> { Value = blob });
        }
    }

    public sealed class AttributeSetDefinitionBlobAuthoring : GASGeneratedDefinitionAuthoring
    {
    }

    public sealed class AttributeSetDefinitionBlobBaker : Baker<AttributeSetDefinitionBlobAuthoring>
    {
        public override void Bake(AttributeSetDefinitionBlobAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GASGeneratedDefinitionRowResolver.TryGetAttributeSetDefinitionBlobRow(authoring.Code, out var row))
                return;
            var definitionCode = row.AttributeSetCode;
            var blob = GASGeneratedDefinitionBlobBuilder.BuildAttributeSetDefinitionBlob(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new GASDefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GASGeneratedDefinitionBlobComponent<AttributeSetDefinitionBlob> { Value = blob });
        }
    }

    public sealed class GameplayCueDefinitionBlobAuthoring : GASGeneratedDefinitionAuthoring
    {
    }

    public sealed class GameplayCueDefinitionBlobBaker : Baker<GameplayCueDefinitionBlobAuthoring>
    {
        public override void Bake(GameplayCueDefinitionBlobAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GASGeneratedDefinitionRowResolver.TryGetGameplayCueDefinitionBlobRow(authoring.Code, out var row))
                return;
            var definitionCode = row.GameplayCueCode;
            var blob = GASGeneratedDefinitionBlobBuilder.BuildGameplayCueDefinitionBlob(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new GASDefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GASGeneratedDefinitionBlobComponent<GameplayCueDefinitionBlob> { Value = blob });
        }
    }

    public sealed class GameplayEffectDefinitionBlobAuthoring : GASGeneratedDefinitionAuthoring
    {
    }

    public sealed class GameplayEffectDefinitionBlobBaker : Baker<GameplayEffectDefinitionBlobAuthoring>
    {
        public override void Bake(GameplayEffectDefinitionBlobAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GASGeneratedDefinitionRowResolver.TryGetGameplayEffectDefinitionBlobRow(authoring.Code, out var row))
                return;
            var definitionCode = row.GameplayEffectCode;
            var blob = GASGeneratedDefinitionBlobBuilder.BuildGameplayEffectDefinitionBlob(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new GASDefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GASGeneratedDefinitionBlobComponent<GameplayEffectDefinitionBlob> { Value = blob });
        }
    }

    public sealed class GameplayTagDefinitionBlobAuthoring : GASGeneratedDefinitionAuthoring
    {
    }

    public sealed class GameplayTagDefinitionBlobBaker : Baker<GameplayTagDefinitionBlobAuthoring>
    {
        public override void Bake(GameplayTagDefinitionBlobAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GASGeneratedDefinitionRowResolver.TryGetGameplayTagDefinitionBlobRow(authoring.Code, out var row))
                return;
            var definitionCode = row.GameplayTagCode;
            var blob = GASGeneratedDefinitionBlobBuilder.BuildGameplayTagDefinitionBlob(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new GASDefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GASGeneratedDefinitionBlobComponent<GameplayTagDefinitionBlob> { Value = blob });
        }
    }

    public sealed class ScenarioSpawnDefinitionBlobAuthoring : GASGeneratedDefinitionAuthoring
    {
    }

    public sealed class ScenarioSpawnDefinitionBlobBaker : Baker<ScenarioSpawnDefinitionBlobAuthoring>
    {
        public override void Bake(ScenarioSpawnDefinitionBlobAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GASGeneratedDefinitionRowResolver.TryGetScenarioSpawnDefinitionBlobRow(authoring.Code, out var row))
                return;
            var definitionCode = row.ScenarioSpawnCode;
            var blob = GASGeneratedDefinitionBlobBuilder.BuildScenarioSpawnDefinitionBlob(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new GASDefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GASGeneratedDefinitionBlobComponent<ScenarioSpawnDefinitionBlob> { Value = blob });
        }
    }

    public sealed class SummonDefinitionBlobAuthoring : GASGeneratedDefinitionAuthoring
    {
    }

    public sealed class SummonDefinitionBlobBaker : Baker<SummonDefinitionBlobAuthoring>
    {
        public override void Bake(SummonDefinitionBlobAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GASGeneratedDefinitionRowResolver.TryGetSummonDefinitionBlobRow(authoring.Code, out var row))
                return;
            var definitionCode = row.SummonGameplayEffectCode;
            var blob = GASGeneratedDefinitionBlobBuilder.BuildSummonDefinitionBlob(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new GASDefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GASGeneratedDefinitionBlobComponent<SummonDefinitionBlob> { Value = blob });
        }
    }

    public sealed class TimelineDefinitionBlobAuthoring : GASGeneratedDefinitionAuthoring
    {
    }

    public sealed class TimelineDefinitionBlobBaker : Baker<TimelineDefinitionBlobAuthoring>
    {
        public override void Bake(TimelineDefinitionBlobAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GASGeneratedDefinitionRowResolver.TryGetTimelineDefinitionBlobRow(authoring.Code, out var row))
                return;
            var definitionCode = row.TimelineId;
            var blob = GASGeneratedDefinitionBlobBuilder.BuildTimelineDefinitionBlob(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new GASDefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GASGeneratedDefinitionBlobComponent<TimelineDefinitionBlob> { Value = blob });
        }
    }

    public sealed class UnitDefinitionBlobAuthoring : GASGeneratedDefinitionAuthoring
    {
    }

    public sealed class UnitDefinitionBlobBaker : Baker<UnitDefinitionBlobAuthoring>
    {
        public override void Bake(UnitDefinitionBlobAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (!GASGeneratedDefinitionRowResolver.TryGetUnitDefinitionBlobRow(authoring.Code, out var row))
                return;
            var definitionCode = row.UnitCode;
            var blob = GASGeneratedDefinitionBlobBuilder.BuildUnitDefinitionBlob(row);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new GASDefinitionCodeComponent { Value = definitionCode });
            AddComponent(entity, new GASGeneratedDefinitionBlobComponent<UnitDefinitionBlob> { Value = blob });
        }
    }
}
