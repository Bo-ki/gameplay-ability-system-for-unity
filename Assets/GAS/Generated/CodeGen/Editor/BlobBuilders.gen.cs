///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using Unity.Collections;
using Unity.Entities;

#if UNITY_EDITOR
namespace GAS.Runtime.Generated
{
    public static class GASGeneratedDefinitionBlobBuilder
    {

        public static BlobAssetReference<AbilityDefinitionBlob> BuildAbilityDefinitionBlob(
            GAS.Runtime.HeadlessAutoChessAbilityDefinitionRow row,
            Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<AbilityDefinitionBlob>();

            root.AbilityCode = row.AbilityCode;
            root.Level = row.Level;
            root.TimelineId = row.TimelineId;
            root.ActivationOwnedTagCode = row.ActivationOwnedTagCode;
            root.CostGameplayEffectCode = row.CostGameplayEffectCode;
            root.CooldownGameplayEffectCode = row.CooldownGameplayEffectCode;
            root.CooldownFrames = row.CooldownFrames;

            var blob = builder.CreateBlobAssetReference<AbilityDefinitionBlob>(allocator);
            builder.Dispose();
            return blob;
        }

        public static BlobAssetReference<AttributeDefinitionBlob> BuildAttributeDefinitionBlob(
            GAS.Runtime.HeadlessAutoChessAttributeDefinitionRow row,
            Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<AttributeDefinitionBlob>();

            root.AttributeSetCode = row.AttributeSetCode;
            root.AttributeCode = row.AttributeCode;
            root.InitialValue = row.InitialValue;
            root.IsClampMin = row.IsClampMin;
            root.IsClampMax = row.IsClampMax;
            root.MinValue = row.MinValue;
            root.MaxValue = row.MaxValue;

            var blob = builder.CreateBlobAssetReference<AttributeDefinitionBlob>(allocator);
            builder.Dispose();
            return blob;
        }

        public static BlobAssetReference<AttributeSetDefinitionBlob> BuildAttributeSetDefinitionBlob(
            GAS.Runtime.HeadlessAutoChessAttributeSetDefinitionRow row,
            Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<AttributeSetDefinitionBlob>();

            root.AttributeSetCode = row.AttributeSetCode;

            var blob = builder.CreateBlobAssetReference<AttributeSetDefinitionBlob>(allocator);
            builder.Dispose();
            return blob;
        }

        public static BlobAssetReference<GameplayCueDefinitionBlob> BuildGameplayCueDefinitionBlob(
            GAS.Runtime.HeadlessAutoChessGameplayCueDefinitionRow row,
            Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GameplayCueDefinitionBlob>();

            root.GameplayCueCode = row.GameplayCueCode;
            builder.AllocateString(ref root.PresentationKey, row.PresentationKey ?? string.Empty);

            var blob = builder.CreateBlobAssetReference<GameplayCueDefinitionBlob>(allocator);
            builder.Dispose();
            return blob;
        }

        public static BlobAssetReference<GameplayEffectDefinitionBlob> BuildGameplayEffectDefinitionBlob(
            GAS.Runtime.HeadlessAutoChessGameplayEffectDefinitionRow row,
            Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GameplayEffectDefinitionBlob>();

            root.GameplayEffectCode = row.GameplayEffectCode;
            builder.AllocateString(ref root.Name, row.Name ?? string.Empty);
            root.ModifierAttributeSetCode = row.ModifierAttributeSetCode;
            root.ModifierAttributeCode = row.ModifierAttributeCode;
            root.ModifierOperation = (int)row.ModifierOperation;
            root.ModifierMagnitude = row.ModifierMagnitude;
            root.ModifierMagnitudeSource = (int)row.ModifierMagnitudeSource;
            root.ModifierMagnitudeKey = row.ModifierMagnitudeKey;
            root.DurationFrames = row.DurationFrames;
            root.PeriodFrames = row.PeriodFrames;
            root.PeriodGameplayEffectCode = row.PeriodGameplayEffectCode;
            root.GrantedTagCode = row.GrantedTagCode;
            root.GameplayCueCode = row.GameplayCueCode;
            root.DamageTypeCode = row.DamageTypeCode;
            root.ResistanceAttributeSetCode = row.ResistanceAttributeSetCode;
            root.ResistanceAttributeCode = row.ResistanceAttributeCode;
            root.ResistanceCap = row.ResistanceCap;
            root.RemoveGameplayEffectTagCode = row.RemoveGameplayEffectTagCode;
            root.StackingCode = row.StackingCode;
            root.StackLimitCount = row.StackLimitCount;
            root.StackType = (int)row.StackType;
            root.EffectDurationRefreshPolicy = (int)row.EffectDurationRefreshPolicy;
            root.EffectPeriodResetPolicy = (int)row.EffectPeriodResetPolicy;
            root.EffectExpirationPolicy = (int)row.EffectExpirationPolicy;
            root.DenyOverflowApplication = row.DenyOverflowApplication;
            root.ClearStackOnOverflow = row.ClearStackOnOverflow;
            root.OverflowGameplayEffectCode = row.OverflowGameplayEffectCode;

            var blob = builder.CreateBlobAssetReference<GameplayEffectDefinitionBlob>(allocator);
            builder.Dispose();
            return blob;
        }

        public static BlobAssetReference<GameplayTagDefinitionBlob> BuildGameplayTagDefinitionBlob(
            GAS.Runtime.HeadlessAutoChessGameplayTagDefinitionRow row,
            Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GameplayTagDefinitionBlob>();

            root.GameplayTagCode = row.GameplayTagCode;
            if (row.ParentCodes != null)
            {
                var count = row.ParentCodes.Count;
                var target = builder.Allocate(ref root.ParentCodes, count);
                for (var i = 0; i < count; i++) target[i] = row.ParentCodes[i];
            }
            if (row.ChildCodes != null)
            {
                var count = row.ChildCodes.Count;
                var target = builder.Allocate(ref root.ChildCodes, count);
                for (var i = 0; i < count; i++) target[i] = row.ChildCodes[i];
            }

            var blob = builder.CreateBlobAssetReference<GameplayTagDefinitionBlob>(allocator);
            builder.Dispose();
            return blob;
        }

        public static BlobAssetReference<ScenarioSpawnDefinitionBlob> BuildScenarioSpawnDefinitionBlob(
            GAS.Runtime.HeadlessAutoChessScenarioSpawnDefinitionRow row,
            Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ScenarioSpawnDefinitionBlob>();

            root.ScenarioSpawnCode = row.ScenarioSpawnCode;
            root.ScenarioId = row.ScenarioId;
            root.UnitCode = row.UnitCode;
            root.Team = (int)row.Team;
            root.BoardX = row.BoardX;
            root.BoardY = row.BoardY;
            root.TurnOrder = row.TurnOrder;

            var blob = builder.CreateBlobAssetReference<ScenarioSpawnDefinitionBlob>(allocator);
            builder.Dispose();
            return blob;
        }

        public static BlobAssetReference<SummonDefinitionBlob> BuildSummonDefinitionBlob(
            GAS.Runtime.HeadlessAutoChessSummonDefinitionRow row,
            Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<SummonDefinitionBlob>();

            root.SummonGameplayEffectCode = row.SummonGameplayEffectCode;
            root.SummonedUnitCode = row.SummonedUnitCode;
            root.FixedTagCode = row.FixedTagCode;
            root.PrimaryAbilityCode = row.PrimaryAbilityCode;
            root.LifetimeTurns = row.LifetimeTurns;
            root.SlotOffset = row.SlotOffset;
            root.BoardXOffset = row.BoardXOffset;
            root.BoardYOffset = row.BoardYOffset;
            root.TurnOrderOffset = row.TurnOrderOffset;
            root.Health = row.Health;
            root.Mana = row.Mana;
            root.Shield = row.Shield;
            root.MaxHealth = row.MaxHealth;
            root.MaxMana = row.MaxMana;
            root.MaxShield = row.MaxShield;
            root.PrimaryTargetPolicy = (int)row.PrimaryTargetPolicy;

            var blob = builder.CreateBlobAssetReference<SummonDefinitionBlob>(allocator);
            builder.Dispose();
            return blob;
        }

        public static BlobAssetReference<TimelineDefinitionBlob> BuildTimelineDefinitionBlob(
            GAS.Runtime.HeadlessAutoChessTimelineDefinitionRow row,
            Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TimelineDefinitionBlob>();

            root.TimelineId = row.TimelineId;
            builder.AllocateString(ref root.Name, row.Name ?? string.Empty);
            root.GameplayEffectCode = row.GameplayEffectCode;
            root.SecondaryGameplayEffectCode = row.SecondaryGameplayEffectCode;
            builder.AllocateString(ref root.TargetCatcherName, row.TargetCatcherName ?? string.Empty);

            var blob = builder.CreateBlobAssetReference<TimelineDefinitionBlob>(allocator);
            builder.Dispose();
            return blob;
        }

        public static BlobAssetReference<UnitDefinitionBlob> BuildUnitDefinitionBlob(
            GAS.Runtime.HeadlessAutoChessUnitDefinitionRow row,
            Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<UnitDefinitionBlob>();

            root.UnitCode = row.UnitCode;
            builder.AllocateString(ref root.Name, row.Name ?? string.Empty);
            root.FixedTagCode = row.FixedTagCode;
            root.PrimaryAbilityCode = row.PrimaryAbilityCode;
            root.ManaAbilityCode = row.ManaAbilityCode;
            root.ControlAbilityCode = row.ControlAbilityCode;
            root.SupportAbilityCode = row.SupportAbilityCode;
            root.SummonAbilityCode = row.SummonAbilityCode;
            root.Health = row.Health;
            root.Mana = row.Mana;
            root.Shield = row.Shield;
            root.ArcaneResistance = row.ArcaneResistance;
            root.MaxHealth = row.MaxHealth;
            root.MaxMana = row.MaxMana;
            root.MaxShield = row.MaxShield;
            root.MaxArcaneResistance = row.MaxArcaneResistance;
            root.PrimaryTargetPolicy = (int)row.PrimaryTargetPolicy;
            root.ManaTargetPolicy = (int)row.ManaTargetPolicy;
            root.ControlTargetPolicy = (int)row.ControlTargetPolicy;
            root.SupportTargetPolicy = (int)row.SupportTargetPolicy;

            var blob = builder.CreateBlobAssetReference<UnitDefinitionBlob>(allocator);
            builder.Dispose();
            return blob;
        }
    }
}
#endif
