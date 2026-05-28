///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

#if UNITY_EDITOR
using GAS.Runtime;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime.Generated
{
    public static class GASGeneratedDefinitionCatalogBuilder
    {
        public static BlobAssetReference<GASDefinitionCatalogBlob> BuildCatalog(Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GASDefinitionCatalogBlob>();
            root.SchemaVersion = 1;

            var abilityCodes = builder.Allocate(ref root.AbilityCodes, 7);
            abilityCodes[0] = 9611;
            abilityCodes[1] = 9612;
            abilityCodes[2] = 9613;
            abilityCodes[3] = 9614;
            abilityCodes[4] = 9615;
            abilityCodes[5] = 9616;
            abilityCodes[6] = 9617;

            var abilities = builder.Allocate(ref root.Abilities, 7);
            abilities[0] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 9611,
                Level = 1,
                PrimaryGameplayEffectCode = 9621,
                SecondaryGameplayEffectCode = 0,
                CostGameplayEffectCode = 0,
                CooldownGameplayEffectCode = 0,
                CooldownFrames = 0,
                ActivationOwnedTagMaskIndex = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = -58436998,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };
            abilities[1] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 9612,
                Level = 1,
                PrimaryGameplayEffectCode = 9622,
                SecondaryGameplayEffectCode = 0,
                CostGameplayEffectCode = 0,
                CooldownGameplayEffectCode = 0,
                CooldownFrames = 0,
                ActivationOwnedTagMaskIndex = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = -58436998,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };
            abilities[2] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 9613,
                Level = 1,
                PrimaryGameplayEffectCode = 9623,
                SecondaryGameplayEffectCode = 0,
                CostGameplayEffectCode = 9624,
                CooldownGameplayEffectCode = 9625,
                CooldownFrames = 2,
                ActivationOwnedTagMaskIndex = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = -58436998,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };
            abilities[3] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 9614,
                Level = 1,
                PrimaryGameplayEffectCode = 9634,
                SecondaryGameplayEffectCode = 0,
                CostGameplayEffectCode = 0,
                CooldownGameplayEffectCode = 9635,
                CooldownFrames = 12,
                ActivationOwnedTagMaskIndex = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = -58436998,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };
            abilities[4] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 9615,
                Level = 1,
                PrimaryGameplayEffectCode = 9637,
                SecondaryGameplayEffectCode = 9638,
                CostGameplayEffectCode = 0,
                CooldownGameplayEffectCode = 9639,
                CooldownFrames = 4,
                ActivationOwnedTagMaskIndex = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = -58436998,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };
            abilities[5] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 9616,
                Level = 1,
                PrimaryGameplayEffectCode = 9642,
                SecondaryGameplayEffectCode = 0,
                CostGameplayEffectCode = 0,
                CooldownGameplayEffectCode = 9643,
                CooldownFrames = 12,
                ActivationOwnedTagMaskIndex = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = -58436998,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };
            abilities[6] = new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = 9617,
                Level = 1,
                PrimaryGameplayEffectCode = 9647,
                SecondaryGameplayEffectCode = 0,
                CostGameplayEffectCode = 0,
                CooldownGameplayEffectCode = 9648,
                CooldownFrames = 5,
                ActivationOwnedTagMaskIndex = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                TargetRuleCode = -58436998,
                TargetRuleParam0 = 0,
                TargetRuleParam1 = 0,
            };

            var gameplayEffectCodes = builder.Allocate(ref root.GameplayEffectCodes, 33);
            gameplayEffectCodes[0] = 9621;
            gameplayEffectCodes[1] = 9622;
            gameplayEffectCodes[2] = 9623;
            gameplayEffectCodes[3] = 9624;
            gameplayEffectCodes[4] = 9625;
            gameplayEffectCodes[5] = 9626;
            gameplayEffectCodes[6] = 9627;
            gameplayEffectCodes[7] = 9628;
            gameplayEffectCodes[8] = 9629;
            gameplayEffectCodes[9] = 9630;
            gameplayEffectCodes[10] = 9634;
            gameplayEffectCodes[11] = 9635;
            gameplayEffectCodes[12] = 9637;
            gameplayEffectCodes[13] = 9638;
            gameplayEffectCodes[14] = 9639;
            gameplayEffectCodes[15] = 9642;
            gameplayEffectCodes[16] = 9643;
            gameplayEffectCodes[17] = 9645;
            gameplayEffectCodes[18] = 9646;
            gameplayEffectCodes[19] = 9647;
            gameplayEffectCodes[20] = 9648;
            gameplayEffectCodes[21] = 9664;
            gameplayEffectCodes[22] = 9665;
            gameplayEffectCodes[23] = 9666;
            gameplayEffectCodes[24] = 9667;
            gameplayEffectCodes[25] = 9668;
            gameplayEffectCodes[26] = 9669;
            gameplayEffectCodes[27] = 9672;
            gameplayEffectCodes[28] = 9673;
            gameplayEffectCodes[29] = 9674;
            gameplayEffectCodes[30] = 9676;
            gameplayEffectCodes[31] = 9677;
            gameplayEffectCodes[32] = 9679;

            var gameplayEffects = builder.Allocate(ref root.GameplayEffects, 33);
            gameplayEffects[0] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9621,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 9662,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
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
            gameplayEffects[1] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9622,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 9662,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
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
            gameplayEffects[2] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9623,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 9663,
                ResistanceAttributeSetCode = 9601,
                ResistanceAttributeCode = 4,
                ResistanceCap = 0.75f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 2,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[3] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9624,
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
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
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
            gameplayEffects[4] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9625,
                DurationFrames = 2,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 0,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
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
            gameplayEffects[5] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9626,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
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
            gameplayEffects[6] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9627,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
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
            gameplayEffects[7] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9628,
                DurationFrames = 10,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 2,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
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
            gameplayEffects[8] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9629,
                DurationFrames = 10,
                PeriodFrames = 2,
                PeriodGameplayEffectCode = 9630,
                GrantedTagMaskIndex = 3,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
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
            gameplayEffects[9] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9630,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 9663,
                ResistanceAttributeSetCode = 9601,
                ResistanceAttributeCode = 4,
                ResistanceCap = 0.75f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
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
            gameplayEffects[10] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9634,
                DurationFrames = 8,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 4,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 8,
                ModifierCount = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[11] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9635,
                DurationFrames = 12,
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
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 8,
                ModifierCount = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[12] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9637,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
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
                GameplayEffectCode = 9638,
                DurationFrames = 6,
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
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 9,
                ModifierCount = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[14] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9639,
                DurationFrames = 4,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 7,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 0,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 9,
                ModifierCount = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[15] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9642,
                DurationFrames = 1,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 9,
                ModifierCount = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[16] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9643,
                DurationFrames = 12,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 8,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 0,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 9,
                ModifierCount = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[17] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9645,
                DurationFrames = 128,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 9,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
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
            gameplayEffects[18] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9646,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 9662,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
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
            gameplayEffects[19] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9647,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = 4,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
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
            gameplayEffects[20] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9648,
                DurationFrames = 5,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 17,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 0,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
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
            gameplayEffects[21] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9664,
                DurationFrames = 6,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 10,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 11,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[22] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9665,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 9662,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 12,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[23] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9666,
                DurationFrames = 128,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 11,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 13,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[24] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9667,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 14,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[25] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9668,
                DurationFrames = 18,
                PeriodFrames = 3,
                PeriodGameplayEffectCode = 9672,
                GrantedTagMaskIndex = 12,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 9670,
                StackLimitCount = 2,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 9669,
                ModifierStart = 15,
                ModifierCount = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[26] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9669,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 9671,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 15,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[27] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9672,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 9671,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 16,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[28] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9673,
                DurationFrames = 128,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 13,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 17,
                ModifierCount = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[29] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9674,
                DurationFrames = 16,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 14,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 9675,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 17,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[30] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9676,
                DurationFrames = 128,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 15,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 18,
                ModifierCount = 0,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[31] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9677,
                DurationFrames = 0,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 9678,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 18,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };
            gameplayEffects[32] = new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = 9679,
                DurationFrames = 128,
                PeriodFrames = 0,
                PeriodGameplayEffectCode = 0,
                GrantedTagMaskIndex = 16,
                RemoveGameplayEffectTagMaskIndex = -1,
                GameplayCueCode = 9641,
                DamageTypeCode = 0,
                ResistanceAttributeSetCode = 0,
                ResistanceAttributeCode = 0,
                ResistanceCap = 0f,
                StackingCode = 0,
                StackLimitCount = 0,
                StackType = 1,
                EffectDurationRefreshPolicy = 1,
                EffectPeriodResetPolicy = 1,
                EffectExpirationPolicy = 0,
                DenyOverflowApplication = (byte)0,
                ClearStackOnOverflow = (byte)0,
                OverflowGameplayEffectCode = 0,
                ModifierStart = 19,
                ModifierCount = 1,
                RequirementStart = 0,
                RequirementCount = 0,
                GrantedAbilityStart = 0,
                GrantedAbilityCount = 0,
            };

            var modifiers = builder.Allocate(ref root.Modifiers, 20);
            modifiers[0] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9621,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 9f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 9f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[1] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9622,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 5f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 5f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[2] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9623,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 24f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 24f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[3] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9624,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 2,
                Operation = (EModifierOp)3,
                BaseMagnitude = 3f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 2,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 3f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[4] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9626,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 2,
                Operation = (EModifierOp)0,
                BaseMagnitude = 2f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 2,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 2f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[5] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9627,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 1,
                Operation = (EModifierOp)0,
                BaseMagnitude = 18f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 18f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[6] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9628,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 2,
                Operation = (EModifierOp)0,
                BaseMagnitude = 1f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 2,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 1f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[7] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9630,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 3f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 3f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[8] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9637,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 3,
                Operation = (EModifierOp)0,
                BaseMagnitude = 10f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 3,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 10f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[9] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9645,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 5,
                Operation = (EModifierOp)0,
                BaseMagnitude = 4f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 5,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 4f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[10] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9646,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 4f,
                MagnitudeSource = (EMagnitudeSource)1,
                MagnitudeKey = 9680,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 4f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[11] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9664,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 2,
                Operation = (EModifierOp)0,
                BaseMagnitude = 2f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 2,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 2f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[12] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9665,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 6f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 6f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[13] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9666,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 6,
                Operation = (EModifierOp)0,
                BaseMagnitude = 0.35f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 6,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 0.35f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[14] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9667,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 1,
                Operation = (EModifierOp)0,
                BaseMagnitude = 0f,
                MagnitudeSource = (EMagnitudeSource)1,
                MagnitudeKey = 9654,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 0f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[15] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9669,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 2f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 2f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[16] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9672,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 1f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 1f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[17] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9674,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 5f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 5f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[18] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9677,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 1,
                Operation = (EModifierOp)3,
                BaseMagnitude = 2f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 1,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 2f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };
            modifiers[19] = new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = 9679,
                ModifierIndex = 0,
                AttributeSetCode = 9601,
                AttributeCode = 5,
                Operation = (EModifierOp)0,
                BaseMagnitude = 2f,
                MagnitudeSource = (EMagnitudeSource)0,
                MagnitudeKey = 0,
                CaptureAttributeSetCode = 9601,
                CaptureAttributeCode = 5,
                CaptureTiming = (EAttributeCaptureTiming)0,
                FallbackMagnitude = 2f,
                Coefficient = 1f,
                PreAdd = 0f,
                PostAdd = 0f,
            };

            builder.Allocate(ref root.Requirements, 0);

            var tagMaskCodes = builder.Allocate(ref root.TagMaskCodes, 18);
            tagMaskCodes[0] = 4;
            tagMaskCodes[1] = 5;
            tagMaskCodes[2] = 6;
            tagMaskCodes[3] = 7;
            tagMaskCodes[4] = 8;
            tagMaskCodes[5] = 9;
            tagMaskCodes[6] = 10;
            tagMaskCodes[7] = 11;
            tagMaskCodes[8] = 13;
            tagMaskCodes[9] = 14;
            tagMaskCodes[10] = 16;
            tagMaskCodes[11] = 17;
            tagMaskCodes[12] = 18;
            tagMaskCodes[13] = 19;
            tagMaskCodes[14] = 20;
            tagMaskCodes[15] = 21;
            tagMaskCodes[16] = 22;
            tagMaskCodes[17] = 15;

            var tagMasks = builder.Allocate(ref root.TagMasks, 18);
            var tagMask0 = new TagMaskComponent();
            tagMask0.AddTag(4);
            tagMasks[0] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 4,
                Mask = tagMask0,
            };
            var tagMask1 = new TagMaskComponent();
            tagMask1.AddTag(5);
            tagMasks[1] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 5,
                Mask = tagMask1,
            };
            var tagMask2 = new TagMaskComponent();
            tagMask2.AddTag(6);
            tagMasks[2] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 6,
                Mask = tagMask2,
            };
            var tagMask3 = new TagMaskComponent();
            tagMask3.AddTag(7);
            tagMasks[3] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 7,
                Mask = tagMask3,
            };
            var tagMask4 = new TagMaskComponent();
            tagMask4.AddTag(8);
            tagMasks[4] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 8,
                Mask = tagMask4,
            };
            var tagMask5 = new TagMaskComponent();
            tagMask5.AddTag(9);
            tagMasks[5] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 9,
                Mask = tagMask5,
            };
            var tagMask6 = new TagMaskComponent();
            tagMask6.AddTag(10);
            tagMasks[6] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 10,
                Mask = tagMask6,
            };
            var tagMask7 = new TagMaskComponent();
            tagMask7.AddTag(11);
            tagMasks[7] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 11,
                Mask = tagMask7,
            };
            var tagMask8 = new TagMaskComponent();
            tagMask8.AddTag(13);
            tagMasks[8] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 13,
                Mask = tagMask8,
            };
            var tagMask9 = new TagMaskComponent();
            tagMask9.AddTag(14);
            tagMasks[9] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 14,
                Mask = tagMask9,
            };
            var tagMask10 = new TagMaskComponent();
            tagMask10.AddTag(16);
            tagMasks[10] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 16,
                Mask = tagMask10,
            };
            var tagMask11 = new TagMaskComponent();
            tagMask11.AddTag(17);
            tagMasks[11] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 17,
                Mask = tagMask11,
            };
            var tagMask12 = new TagMaskComponent();
            tagMask12.AddTag(18);
            tagMasks[12] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 18,
                Mask = tagMask12,
            };
            var tagMask13 = new TagMaskComponent();
            tagMask13.AddTag(19);
            tagMasks[13] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 19,
                Mask = tagMask13,
            };
            var tagMask14 = new TagMaskComponent();
            tagMask14.AddTag(20);
            tagMasks[14] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 20,
                Mask = tagMask14,
            };
            var tagMask15 = new TagMaskComponent();
            tagMask15.AddTag(21);
            tagMasks[15] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 21,
                Mask = tagMask15,
            };
            var tagMask16 = new TagMaskComponent();
            tagMask16.AddTag(22);
            tagMasks[16] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 22,
                Mask = tagMask16,
            };
            var tagMask17 = new TagMaskComponent();
            tagMask17.AddTag(15);
            tagMasks[17] = new GASCatalogTagMaskDefinitionBlob
            {
                TagCode = 15,
                Mask = tagMask17,
            };

            builder.Allocate(ref root.GrantedAbilities, 0);

            var blob = builder.CreateBlobAssetReference<GASDefinitionCatalogBlob>(allocator);
            builder.Dispose();
            return blob;
        }
    }

    public sealed class GASGeneratedDefinitionCatalogAuthoring : MonoBehaviour
    {
        public int Revision = GASGeneratedDefinitionCatalogInfo.SchemaVersion;
    }

    public sealed class GASGeneratedDefinitionCatalogBaker : Baker<GASGeneratedDefinitionCatalogAuthoring>
    {
        public override void Bake(GASGeneratedDefinitionCatalogAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var catalog = GASGeneratedDefinitionCatalogBuilder.BuildCatalog();
            AddBlobAsset(ref catalog, out _);
            AddComponent(entity, new GASDefinitionCatalogComponent
            {
                Catalog = catalog,
                Revision = authoring.Revision,
            });
        }
    }
}
#endif
