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
        public int ActivationOwnedTagCode;
        public int CostGameplayEffectCode;
        public int CooldownGameplayEffectCode;
        public int CooldownFrames;
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

    /// <summary>
    /// BlobAsset definition generated for ScenarioSpawn.
    /// </summary>
    public struct ScenarioSpawnDefinitionBlob
    {
        public int ScenarioSpawnCode;
        public int ScenarioId;
        public int UnitCode;
        public int Team;
        public int BoardX;
        public int BoardY;
        public int TurnOrder;
    }

    /// <summary>
    /// BlobAsset definition generated for Summon.
    /// </summary>
    public struct SummonDefinitionBlob
    {
        public int SummonGameplayEffectCode;
        public int SummonedUnitCode;
        public int FixedTagCode;
        public int PrimaryAbilityCode;
        public int LifetimeTurns;
        public int SlotOffset;
        public int BoardXOffset;
        public int BoardYOffset;
        public int TurnOrderOffset;
        public float Health;
        public float Mana;
        public float Shield;
        public float MaxHealth;
        public float MaxMana;
        public float MaxShield;
        public int PrimaryTargetPolicy;
    }

    /// <summary>
    /// BlobAsset definition generated for Timeline.
    /// </summary>
    public struct TimelineDefinitionBlob
    {
        public int TimelineId;
        public BlobString Name;
        public int GameplayEffectCode;
        public int SecondaryGameplayEffectCode;
        public BlobString TargetCatcherName;
    }

    /// <summary>
    /// BlobAsset definition generated for Unit.
    /// </summary>
    public struct UnitDefinitionBlob
    {
        public int UnitCode;
        public BlobString Name;
        public int FixedTagCode;
        public int PrimaryAbilityCode;
        public int ManaAbilityCode;
        public int ControlAbilityCode;
        public int SupportAbilityCode;
        public int SummonAbilityCode;
        public float Health;
        public float Mana;
        public float Shield;
        public float ArcaneResistance;
        public float MaxHealth;
        public float MaxMana;
        public float MaxShield;
        public float MaxArcaneResistance;
        public int PrimaryTargetPolicy;
        public int ManaTargetPolicy;
        public int ControlTargetPolicy;
        public int SupportTargetPolicy;
    }
}
