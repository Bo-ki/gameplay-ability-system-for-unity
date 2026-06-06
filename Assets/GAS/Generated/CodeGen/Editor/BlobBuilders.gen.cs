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
            GAS.Editor.AbilityDefinitionRow row,
            Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<AbilityDefinitionBlob>();

            root.AbilityCode = row.AbilityCode;
            root.Level = row.Level;
            root.TimelineId = row.TimelineId;
            root.PrimaryGameplayEffectCode = row.PrimaryGameplayEffectCode;
            root.SecondaryGameplayEffectCode = row.SecondaryGameplayEffectCode;
            root.ActivationOwnedTagCode = row.ActivationOwnedTagCode;
            root.CostGameplayEffectCode = row.CostGameplayEffectCode;
            root.CooldownGameplayEffectCode = row.CooldownGameplayEffectCode;
            root.CooldownFrames = row.CooldownFrames;
            root.TargetRuleCode = row.TargetRuleCode;

            var blob = builder.CreateBlobAssetReference<AbilityDefinitionBlob>(allocator);
            builder.Dispose();
            return blob;
        }

        public static BlobAssetReference<AttributeDefinitionBlob> BuildAttributeDefinitionBlob(
            GAS.Editor.AttributeDefinitionRow row,
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
            GAS.Editor.AttributeSetDefinitionRow row,
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
            GAS.Editor.GameplayCueDefinitionRow row,
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
            GAS.Editor.GameplayEffectDefinitionRow row,
            Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GameplayEffectDefinitionBlob>();

            root.GameplayEffectCode = row.GameplayEffectCode;
            builder.AllocateString(ref root.Name, row.Name ?? string.Empty);
            root.ModifierAttributeSetCode = row.ModifierAttributeSetCode;
            root.ModifierAttributeCode = row.ModifierAttributeCode;
            root.ModifierOperation = row.ModifierOperation;
            root.ModifierMagnitude = row.ModifierMagnitude;
            root.ModifierMagnitudeSource = row.ModifierMagnitudeSource;
            root.ModifierMagnitudeKey = row.ModifierMagnitudeKey;
            if (row.ModifierAttributeSetCodes != null)
            {
                var count = row.ModifierAttributeSetCodes.Length;
                var target = builder.Allocate(ref root.ModifierAttributeSetCodes, count);
                for (var i = 0; i < count; i++) target[i] = row.ModifierAttributeSetCodes[i];
            }
            if (row.ModifierAttributeCodes != null)
            {
                var count = row.ModifierAttributeCodes.Length;
                var target = builder.Allocate(ref root.ModifierAttributeCodes, count);
                for (var i = 0; i < count; i++) target[i] = row.ModifierAttributeCodes[i];
            }
            if (row.ModifierOperations != null)
            {
                var count = row.ModifierOperations.Length;
                var target = builder.Allocate(ref root.ModifierOperations, count);
                for (var i = 0; i < count; i++) target[i] = row.ModifierOperations[i];
            }
            if (row.ModifierMagnitudes != null)
            {
                var count = row.ModifierMagnitudes.Length;
                var target = builder.Allocate(ref root.ModifierMagnitudes, count);
                for (var i = 0; i < count; i++) target[i] = row.ModifierMagnitudes[i];
            }
            if (row.ModifierMagnitudeSources != null)
            {
                var count = row.ModifierMagnitudeSources.Length;
                var target = builder.Allocate(ref root.ModifierMagnitudeSources, count);
                for (var i = 0; i < count; i++) target[i] = row.ModifierMagnitudeSources[i];
            }
            if (row.ModifierMagnitudeKeys != null)
            {
                var count = row.ModifierMagnitudeKeys.Length;
                var target = builder.Allocate(ref root.ModifierMagnitudeKeys, count);
                for (var i = 0; i < count; i++) target[i] = row.ModifierMagnitudeKeys[i];
            }
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
            root.StackType = row.StackType;
            root.EffectDurationRefreshPolicy = row.EffectDurationRefreshPolicy;
            root.EffectPeriodResetPolicy = row.EffectPeriodResetPolicy;
            root.EffectExpirationPolicy = row.EffectExpirationPolicy;
            root.DenyOverflowApplication = row.DenyOverflowApplication;
            root.ClearStackOnOverflow = row.ClearStackOnOverflow;
            root.OverflowGameplayEffectCode = row.OverflowGameplayEffectCode;

            var blob = builder.CreateBlobAssetReference<GameplayEffectDefinitionBlob>(allocator);
            builder.Dispose();
            return blob;
        }

        public static BlobAssetReference<GameplayTagDefinitionBlob> BuildGameplayTagDefinitionBlob(
            GAS.Editor.GameplayTagDefinitionRow row,
            Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GameplayTagDefinitionBlob>();

            root.GameplayTagCode = row.GameplayTagCode;
            if (row.ParentCodes != null)
            {
                var count = row.ParentCodes.Length;
                var target = builder.Allocate(ref root.ParentCodes, count);
                for (var i = 0; i < count; i++) target[i] = row.ParentCodes[i];
            }
            if (row.ChildCodes != null)
            {
                var count = row.ChildCodes.Length;
                var target = builder.Allocate(ref root.ChildCodes, count);
                for (var i = 0; i < count; i++) target[i] = row.ChildCodes[i];
            }

            var blob = builder.CreateBlobAssetReference<GameplayTagDefinitionBlob>(allocator);
            builder.Dispose();
            return blob;
        }
    }
}
#endif
