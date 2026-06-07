///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Generated
{
    public static class GASGeneratedDefinitionCatalogInfo
    {
        public const int SchemaVersion = 1;
        public const int AbilityCount = 9;
        public const int GameplayEffectCount = 17;
        public const int ModifierCount = 11;
        public const int TagMaskCount = 7;
    }

    public static class GASGeneratedDefinitionCatalogLookup
    {
        public static bool IsCatalogCreated(BlobAssetReference<GASDefinitionCatalogBlob> catalog)
        {
            return catalog.IsCreated && catalog.Value.SchemaVersion == GASGeneratedDefinitionCatalogInfo.SchemaVersion;
        }

        public static bool TryGetAbilityIndex(ref GASDefinitionCatalogBlob catalog, int abilityCode, out int index)
        {
            index = -1;
            var lo = 0;
            var hi = catalog.AbilityCodes.Length - 1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                var midCode = catalog.AbilityCodes[mid];
                if (midCode == abilityCode)
                {
                    index = mid;
                    return true;
                }
                if (midCode < abilityCode) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        public static ref readonly GASCatalogAbilityDefinitionBlob GetAbility(ref GASDefinitionCatalogBlob catalog, int index)
        {
            return ref catalog.Abilities[index];
        }

        public static bool TryGetGameplayEffectIndex(ref GASDefinitionCatalogBlob catalog, int gameplayEffectCode, out int index)
        {
            index = -1;
            var lo = 0;
            var hi = catalog.GameplayEffectCodes.Length - 1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                var midCode = catalog.GameplayEffectCodes[mid];
                if (midCode == gameplayEffectCode)
                {
                    index = mid;
                    return true;
                }
                if (midCode < gameplayEffectCode) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        public static ref readonly GASCatalogGameplayEffectDefinitionBlob GetGameplayEffect(ref GASDefinitionCatalogBlob catalog, int index)
        {
            return ref catalog.GameplayEffects[index];
        }
    }

    public static class GASGeneratedDefinitionCatalogBuilder
    {
        public static BlobAssetReference<GASDefinitionCatalogBlob> BuildCatalog(Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GASDefinitionCatalogBlob>();
            root.SchemaVersion = 1;

            var abilityCodes = builder.Allocate(ref root.AbilityCodes, 9);
            abilityCodes[0] = 1003;
            abilityCodes[1] = 1005;
            abilityCodes[2] = 5000;
            abilityCodes[3] = 9101;
            abilityCodes[4] = 9102;
            abilityCodes[5] = 9103;
            abilityCodes[6] = 9104;
            abilityCodes[7] = 10002;
            abilityCodes[8] = 20001;

            var abilities = builder.Allocate(ref root.Abilities, 9);
            abilities[0] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 1003,
                Level = 1,
                PrimaryGameplayEffectCode = 1003,
                SecondaryGameplayEffectCode = 0,
                CostGameplayEffectCode = 0,
                CooldownGameplayEffectCode = 0,
                CooldownFrames = 0,
                ActivationOwnedTagMaskIndex = -1,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = 0,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };
            abilities[1] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 1005,
                Level = 1,
                PrimaryGameplayEffectCode = 1005,
                SecondaryGameplayEffectCode = 0,
                CostGameplayEffectCode = 0,
                CooldownGameplayEffectCode = 0,
                CooldownFrames = 0,
                ActivationOwnedTagMaskIndex = -1,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = 0,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };
            abilities[2] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 5000,
                Level = 1,
                PrimaryGameplayEffectCode = 1003,
                SecondaryGameplayEffectCode = 0,
                CostGameplayEffectCode = 3002,
                CooldownGameplayEffectCode = 3001,
                CooldownFrames = 120,
                ActivationOwnedTagMaskIndex = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = 1458980686,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };
            abilities[3] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 9101,
                Level = 1,
                PrimaryGameplayEffectCode = 9201,
                SecondaryGameplayEffectCode = 0,
                CostGameplayEffectCode = 0,
                CooldownGameplayEffectCode = 0,
                CooldownFrames = 0,
                ActivationOwnedTagMaskIndex = -1,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = 0,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };
            abilities[4] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 9102,
                Level = 1,
                PrimaryGameplayEffectCode = 9202,
                SecondaryGameplayEffectCode = 0,
                CostGameplayEffectCode = 0,
                CooldownGameplayEffectCode = 0,
                CooldownFrames = 0,
                ActivationOwnedTagMaskIndex = -1,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = 0,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };
            abilities[5] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 9103,
                Level = 1,
                PrimaryGameplayEffectCode = 9207,
                SecondaryGameplayEffectCode = 0,
                CostGameplayEffectCode = 0,
                CooldownGameplayEffectCode = 0,
                CooldownFrames = 0,
                ActivationOwnedTagMaskIndex = -1,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = 0,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };
            abilities[6] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 9104,
                Level = 1,
                PrimaryGameplayEffectCode = 9203,
                SecondaryGameplayEffectCode = 0,
                CostGameplayEffectCode = 0,
                CooldownGameplayEffectCode = 0,
                CooldownFrames = 0,
                ActivationOwnedTagMaskIndex = -1,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = 0,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };
            abilities[7] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 10002,
                Level = 1,
                PrimaryGameplayEffectCode = 1002,
                SecondaryGameplayEffectCode = 1001,
                CostGameplayEffectCode = 0,
                CooldownGameplayEffectCode = 0,
                CooldownFrames = 0,
                ActivationOwnedTagMaskIndex = -1,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = 0,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };
            abilities[8] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 20001,
                Level = 1,
                PrimaryGameplayEffectCode = 2002,
                SecondaryGameplayEffectCode = 0,
                CostGameplayEffectCode = 0,
                CooldownGameplayEffectCode = 0,
                CooldownFrames = 0,
                ActivationOwnedTagMaskIndex = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = -944608227,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };

            var gameplayEffectCodes = builder.Allocate(ref root.GameplayEffectCodes, 17);
            gameplayEffectCodes[0] = 1001;
            gameplayEffectCodes[1] = 1002;
            gameplayEffectCodes[2] = 1003;
            gameplayEffectCodes[3] = 1005;
            gameplayEffectCodes[4] = 1006;
            gameplayEffectCodes[5] = 1007;
            gameplayEffectCodes[6] = 2001;
            gameplayEffectCodes[7] = 2002;
            gameplayEffectCodes[8] = 3001;
            gameplayEffectCodes[9] = 3002;
            gameplayEffectCodes[10] = 3003;
            gameplayEffectCodes[11] = 9001;
            gameplayEffectCodes[12] = 9201;
            gameplayEffectCodes[13] = 9202;
            gameplayEffectCodes[14] = 9203;
            gameplayEffectCodes[15] = 9204;
            gameplayEffectCodes[16] = 9207;

            var gameplayEffects = builder.Allocate(ref root.GameplayEffects, 17);
            gameplayEffects[0] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 1001,
                DurationFrames = -1,
                PeriodFrames = 5,
                PeriodGameplayEffectCode = 9001,
                GrantedTagMaskIndex = 2,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 0,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 0,
                EffectDurationRefreshPolicy = 0,
                EffectPeriodResetPolicy = 0,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 0,
                ModifierCount = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[1] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 1002,
                DurationFrames = -1,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 3,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 0,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 0,
                EffectDurationRefreshPolicy = 0,
                EffectPeriodResetPolicy = 0,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 0,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[2] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 1003,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 1001,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 0,
                EffectDurationRefreshPolicy = 0,
                EffectPeriodResetPolicy = 0,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 1,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[3] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 1005,
                DurationFrames = 600,
                PeriodFrames = 60,
                PeriodGameplayEffectCode = 1001,
                GrantedTagMaskIndex = 4,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 0,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 10,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 1001,
                ModifierStart = 2,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[4] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 1006,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 0,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 0,
                EffectDurationRefreshPolicy = 0,
                EffectPeriodResetPolicy = 0,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 3,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[5] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 1007,
                DurationFrames = -1,
                PeriodFrames = 60,
                PeriodGameplayEffectCode = 1006,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 0,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 0,
                EffectDurationRefreshPolicy = 0,
                EffectPeriodResetPolicy = 0,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 4,
                ModifierCount = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[6] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 2001,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 0,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 0,
                EffectDurationRefreshPolicy = 0,
                EffectPeriodResetPolicy = 0,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 4,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[7] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 2002,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 3000,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 0,
                EffectDurationRefreshPolicy = 0,
                EffectPeriodResetPolicy = 0,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 5,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[8] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 3001,
                DurationFrames = 60,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 5,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 0,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 0,
                EffectDurationRefreshPolicy = 0,
                EffectPeriodResetPolicy = 0,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 6,
                ModifierCount = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[9] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 3002,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 0,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 0,
                EffectDurationRefreshPolicy = 0,
                EffectPeriodResetPolicy = 0,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 6,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[10] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 3003,
                DurationFrames = -1,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 6,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 0,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 0,
                EffectDurationRefreshPolicy = 0,
                EffectPeriodResetPolicy = 0,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 7,
                ModifierCount = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[11] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9001,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 0,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 0,
                EffectDurationRefreshPolicy = 0,
                EffectPeriodResetPolicy = 0,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 7,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[12] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9201,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9301,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 0,
                EffectDurationRefreshPolicy = 0,
                EffectPeriodResetPolicy = 0,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 8,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[13] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9202,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9301,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 0,
                EffectDurationRefreshPolicy = 0,
                EffectPeriodResetPolicy = 0,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 9,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[14] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9203,
                DurationFrames = 8,
                PeriodFrames = 2,
                PeriodGameplayEffectCode = 9204,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9301,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 3,
                StackType = 9203,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 10,
                ModifierCount = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[15] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9204,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9301,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 0,
                EffectDurationRefreshPolicy = 0,
                EffectPeriodResetPolicy = 0,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 10,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[16] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9207,
                DurationFrames = -1,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9301,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 0,
                EffectDurationRefreshPolicy = 0,
                EffectPeriodResetPolicy = 0,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 11,
                ModifierCount = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };

            var modifiers = builder.Allocate(ref root.Modifiers, 11);
            modifiers[0] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 1002,
                ModifierIndex = 0,
                AttributeSetCode = 1,
                AttributeCode = 3,
                Operation = (EModifierOp)1,
                BaseMagnitude = 3f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 1,
                CaptureAttributeCode = 3,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 3f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[1] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 1003,
                ModifierIndex = 0,
                AttributeSetCode = 1,
                AttributeCode = 2,
                Operation = (EModifierOp)3,
                BaseMagnitude = 10f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 1,
                CaptureAttributeCode = 2,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 10f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[2] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 1005,
                ModifierIndex = 0,
                AttributeSetCode = 1,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 1f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 1,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 1f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[3] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 1006,
                ModifierIndex = 0,
                AttributeSetCode = 1,
                AttributeCode = 6,
                Operation = (EModifierOp)0,
                BaseMagnitude = 2f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 1,
                CaptureAttributeCode = 6,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 2f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[4] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 2001,
                ModifierIndex = 0,
                AttributeSetCode = 1,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 3f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 1,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 3f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[5] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 2002,
                ModifierIndex = 0,
                AttributeSetCode = 1,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 10f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 1,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 10f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[6] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 3002,
                ModifierIndex = 0,
                AttributeSetCode = 1,
                AttributeCode = 6,
                Operation = (EModifierOp)3,
                BaseMagnitude = 20f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 1,
                CaptureAttributeCode = 6,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 20f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[7] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9001,
                ModifierIndex = 0,
                AttributeSetCode = 1,
                AttributeCode = 6,
                Operation = (EModifierOp)3,
                BaseMagnitude = 1f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 1,
                CaptureAttributeCode = 6,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 1f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[8] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9201,
                ModifierIndex = 0,
                AttributeSetCode = 9001,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 12f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9001,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 12f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[9] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9202,
                ModifierIndex = 0,
                AttributeSetCode = 9001,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 8f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9001,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 8f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[10] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9204,
                ModifierIndex = 0,
                AttributeSetCode = 9001,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 1f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9001,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 1f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };

            builder.Allocate(ref root.Requirements, 0);

            var tagMaskCodes = builder.Allocate(ref root.TagMaskCodes, 7);
            tagMaskCodes[0] = 3002;
            tagMaskCodes[1] = 3003;
            tagMaskCodes[2] = 4001003;
            tagMaskCodes[3] = 4001002;
            tagMaskCodes[4] = 2006;
            tagMaskCodes[5] = 6001;
            tagMaskCodes[6] = 4001004;

            var tagMasks = builder.Allocate(ref root.TagMasks, 7);
            var tagMask0 = new TagMaskComponent();
            tagMask0.AddTag(3002);
            tagMasks[0] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 3002,
                Mask = tagMask0,
            };
            var tagMask1 = new TagMaskComponent();
            tagMask1.AddTag(3003);
            tagMasks[1] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 3003,
                Mask = tagMask1,
            };
            var tagMask2 = new TagMaskComponent();
            tagMask2.AddTag(4001003);
            tagMasks[2] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 4001003,
                Mask = tagMask2,
            };
            var tagMask3 = new TagMaskComponent();
            tagMask3.AddTag(4001002);
            tagMasks[3] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 4001002,
                Mask = tagMask3,
            };
            var tagMask4 = new TagMaskComponent();
            tagMask4.AddTag(2006);
            tagMasks[4] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 2006,
                Mask = tagMask4,
            };
            var tagMask5 = new TagMaskComponent();
            tagMask5.AddTag(6001);
            tagMasks[5] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 6001,
                Mask = tagMask5,
            };
            var tagMask6 = new TagMaskComponent();
            tagMask6.AddTag(4001004);
            tagMasks[6] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 4001004,
                Mask = tagMask6,
            };

            builder.Allocate(ref root.GrantedAbilities, 0);

            var blob = builder.CreateBlobAssetReference<GASDefinitionCatalogBlob>(allocator);
            builder.Dispose();
            return blob;
        }
    }
}
