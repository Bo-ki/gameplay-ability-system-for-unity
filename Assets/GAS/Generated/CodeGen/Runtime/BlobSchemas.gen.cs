///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using Unity.Entities;

namespace GAS.Runtime.Generated
{

    /// <summary>
    /// BlobAsset definition generated for Ability.
    /// </summary>
    public struct AbilityDefinitionBlob
    {
        public int AbilityCode;
        public int Level;
        public int TimelineId;
        public int PrimaryGameplayEffectCode;
        public int SecondaryGameplayEffectCode;
        public int ActivationOwnedTagCode;
        public int CostGameplayEffectCode;
        public int CooldownGameplayEffectCode;
        public int CooldownFrames;
        public int TargetRuleCode;
    }

    /// <summary>
    /// BlobAsset definition generated for Attribute.
    /// </summary>
    public struct AttributeDefinitionBlob
    {
        public int AttributeSetCode;
        public int AttributeCode;
        public float InitialValue;
        public bool IsClampMin;
        public bool IsClampMax;
        public float MinValue;
        public float MaxValue;
    }

    /// <summary>
    /// BlobAsset definition generated for AttributeSet.
    /// </summary>
    public struct AttributeSetDefinitionBlob
    {
        public int AttributeSetCode;
    }

    /// <summary>
    /// BlobAsset definition generated for GameplayCue.
    /// </summary>
    public struct GameplayCueDefinitionBlob
    {
        public int GameplayCueCode;
        public BlobString PresentationKey;
    }

    /// <summary>
    /// BlobAsset definition generated for GameplayEffect.
    /// </summary>
    public struct GameplayEffectDefinitionBlob
    {
        public int GameplayEffectCode;
        public BlobString Name;
        public int ModifierAttributeSetCode;
        public int ModifierAttributeCode;
        public int ModifierOperation;
        public float ModifierMagnitude;
        public int ModifierMagnitudeSource;
        public int ModifierMagnitudeKey;
        public BlobArray<int> ModifierAttributeSetCodes;
        public BlobArray<int> ModifierAttributeCodes;
        public BlobArray<int> ModifierOperations;
        public BlobArray<float> ModifierMagnitudes;
        public BlobArray<int> ModifierMagnitudeSources;
        public BlobArray<int> ModifierMagnitudeKeys;
        public int DurationFrames;
        public int PeriodFrames;
        public int PeriodGameplayEffectCode;
        public int GrantedTagCode;
        public int GameplayCueCode;
        public int DamageTypeCode;
        public int ResistanceAttributeSetCode;
        public int ResistanceAttributeCode;
        public float ResistanceCap;
        public int RemoveGameplayEffectTagCode;
        public int StackingCode;
        public int StackLimitCount;
        public int StackType;
        public int EffectDurationRefreshPolicy;
        public int EffectPeriodResetPolicy;
        public int EffectExpirationPolicy;
        public bool DenyOverflowApplication;
        public bool ClearStackOnOverflow;
        public int OverflowGameplayEffectCode;
    }

    /// <summary>
    /// BlobAsset definition generated for GameplayTag.
    /// </summary>
    public struct GameplayTagDefinitionBlob
    {
        public int GameplayTagCode;
        public BlobArray<int> ParentCodes;
        public BlobArray<int> ChildCodes;
    }
}
