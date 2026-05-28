using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Runtime world entry point for immutable generated GAS definitions.
    /// Runtime systems read this singleton and pass the Blob root by ref to generated lookup/glue helpers.
    /// </summary>
    public struct GASDefinitionCatalogComponent : IComponentData
    {
        public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
        public int Revision;
    }

    public struct GASDefinitionCatalogBlob
    {
        public int SchemaVersion;
        public BlobArray<int> AbilityCodes;
        public BlobArray<GASCatalogAbilityDefinitionBlob> Abilities;
        public BlobArray<int> GameplayEffectCodes;
        public BlobArray<GASCatalogGameplayEffectDefinitionBlob> GameplayEffects;
        public BlobArray<GASCatalogModifierDefinitionBlob> Modifiers;
        public BlobArray<GASCatalogRequirementDefinitionBlob> Requirements;
        public BlobArray<GASCatalogTagMaskDefinitionBlob> TagMasks;
        public BlobArray<int> TagMaskCodes;
        public BlobArray<GASCatalogGrantedAbilityDefinitionBlob> GrantedAbilities;
    }

    public struct GASCatalogAbilityDefinitionBlob
    {
        public int AbilityCode;
        public int Level;
        public int PrimaryGameplayEffectCode;
        public int SecondaryGameplayEffectCode;
        public int CostGameplayEffectCode;
        public int CooldownGameplayEffectCode;
        public int CooldownFrames;
        public int ActivationOwnedTagMaskIndex;
        public int RequirementStart;
        public int RequirementCount;
        public int TargetRuleCode;
        public int TargetRuleParam0;
        public int TargetRuleParam1;
    }

    public struct GASCatalogGameplayEffectDefinitionBlob
    {
        public int GameplayEffectCode;
        public int DurationFrames;
        public int PeriodFrames;
        public int PeriodGameplayEffectCode;
        public int GrantedTagMaskIndex;
        public int RemoveGameplayEffectTagMaskIndex;
        public int GameplayCueCode;
        public int DamageTypeCode;
        public int ResistanceAttributeSetCode;
        public int ResistanceAttributeCode;
        public float ResistanceCap;
        public int StackingCode;
        public int StackLimitCount;
        public int StackType;
        public int EffectDurationRefreshPolicy;
        public int EffectPeriodResetPolicy;
        public int EffectExpirationPolicy;
        public byte DenyOverflowApplication;
        public byte ClearStackOnOverflow;
        public int OverflowGameplayEffectCode;
        public int ModifierStart;
        public int ModifierCount;
        public int RequirementStart;
        public int RequirementCount;
        public int GrantedAbilityStart;
        public int GrantedAbilityCount;
    }

    public struct GASCatalogModifierDefinitionBlob
    {
        public int GameplayEffectCode;
        public int ModifierIndex;
        public int AttributeSetCode;
        public int AttributeCode;
        public EModifierOp Operation;
        public float BaseMagnitude;
        public EMagnitudeSource MagnitudeSource;
        public int MagnitudeKey;
        public int CaptureAttributeSetCode;
        public int CaptureAttributeCode;
        public EAttributeCaptureTiming CaptureTiming;
        public float FallbackMagnitude;
        public float Coefficient;
        public float PreAdd;
        public float PostAdd;
    }

    public struct GASCatalogRequirementDefinitionBlob
    {
        public int RequirementKind;
        public int TagMaskIndex;
        public int AttributeSetCode;
        public int AttributeCode;
        public int CompareOp;
        public float CompareValue;
    }

    public struct GASCatalogTagMaskDefinitionBlob
    {
        public int TagCode;
        public TagMaskComponent Mask;
    }

    public struct GASCatalogGrantedAbilityDefinitionBlob
    {
        public int GameplayEffectCode;
        public int AbilityCode;
        public int Level;
        public int ActivationPolicy;
        public int RemovePolicy;
    }

    public struct AbilityActivationPlanRecord
    {
        public int Frame;
        public int AbilityCode;
        public int AbilityDefinitionIndex;
        public int Level;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public int PrimaryGameplayEffectCode;
        public int SecondaryGameplayEffectCode;
        public int CostGameplayEffectCode;
        public int CooldownGameplayEffectCode;
        public int CooldownFrames;
        public int TargetRuleCode;
        public int FailureReasonCode;
        public int Flags;

        public readonly bool Succeeded => FailureReasonCode == GASFailureReasonCodes.None;
        public readonly bool HasAnyGameplayEffect =>
            PrimaryGameplayEffectCode > 0
            || SecondaryGameplayEffectCode > 0
            || CostGameplayEffectCode > 0
            || CooldownGameplayEffectCode > 0;
    }

    public struct AbilityTargetRecord
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int TargetRuleCode;
        public int TargetIndex;
        public int Flags;
    }

    public struct GECommandSeedRecord
    {
        public int Frame;
        public int SeedKind;
        public GEEffectCommandSource Source;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public Entity SourceEffect;
        public int GameplayEffectCode;
        public int GameplayEffectDefinitionIndex;
        public int Level;
        public int ContextId;
        public int ParentContextId;
        public int DurationFrameOverride;
        public int FailureReasonCode;
        public int Flags;
    }

    public struct MagnitudeEvalContext
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int GameplayEffectCode;
        public int Level;
        public int StackCount;
        public int SetByCallerKey;
        public float SetByCallerValue;
        public float SourceAttributeValue;
        public float TargetAttributeValue;
        public float ExecutionValue;
        public byte HasSetByCallerValue;
        public byte HasSourceAttributeValue;
        public byte HasTargetAttributeValue;
        public byte HasExecutionValue;
    }

    public struct ResolvedModifierRecord
    {
        public int GameplayEffectCode;
        public int ModifierIndex;
        public int AttributeSetCode;
        public int AttributeCode;
        public EModifierOp Operation;
        public float Magnitude;
        public EMagnitudeSource MagnitudeSource;
        public int MagnitudeKey;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int Flags;
    }

    public struct AttributeSnapshotRecord
    {
        public Entity OwnerAsc;
        public int AttributeSetCode;
        public int AttributeCode;
        public float CurrentValue;
        public float BaseValue;
        public byte IsValid;
    }

    public static class GASFailureReasonCodes
    {
        public const int None = 0;
        public const int MissingCatalog = 1;
        public const int AbilityNotFound = 2;
        public const int GameplayEffectNotFound = 3;
        public const int RequirementFailed = 4;
        public const int InvalidTarget = 5;
        public const int UnsupportedMagnitudeSource = 6;
    }

    public static class GASGESeedKind
    {
        public const int Primary = 1;
        public const int Secondary = 2;
        public const int Cost = 3;
        public const int Cooldown = 4;
        public const int Period = 5;
        public const int Overflow = 6;
    }

    public static class GASGECommandSeedFlags
    {
        public const int None = 0;
        public const int ActiveMutation = 1 << 0;
    }

    public static class GASRequirementKind
    {
        public const int None = 0;
        public const int RequiredTags = 1;
        public const int BlockedTags = 2;
        public const int AttributeCompare = 3;
    }

    public static class GASMagnitudeEvaluatorCodes
    {
        public const int Constant = (int)EMagnitudeSource.Constant;
        public const int SetByCaller = (int)EMagnitudeSource.SetByCaller;
        public const int SourceAttribute = (int)EMagnitudeSource.SourceAttribute;
        public const int TargetAttribute = (int)EMagnitudeSource.TargetAttribute;
        public const int ExecutionCalculation = (int)EMagnitudeSource.ExecutionCalculation;
        public const int StackCount = (int)EMagnitudeSource.StackCount;
    }

    public static class GASAttributeCodes
    {
        public const int None = 0;
    }

    public static class GASGeneratedAttributeSnapshotAccessor
    {
        public static bool TryRead(
            NativeArray<AttributeSnapshotRecord> snapshots,
            Entity ownerAsc,
            int attributeSetCode,
            int attributeCode,
            out AttributeSnapshotRecord snapshot)
        {
            for (var i = 0; i < snapshots.Length; i++)
            {
                var candidate = snapshots[i];
                if (candidate.IsValid == 0)
                    continue;

                if (candidate.OwnerAsc == ownerAsc
                    && candidate.AttributeSetCode == attributeSetCode
                    && candidate.AttributeCode == attributeCode)
                {
                    snapshot = candidate;
                    return true;
                }
            }

            snapshot = default;
            return false;
        }
    }
}
