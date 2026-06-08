using Unity.Collections;
using Unity.Entities;
using System;

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

    [Flags]
    public enum EGASOfficialConceptCoverageFlags
    {
        None = 0,
        AbilityLifecycle = 1 << 0,
        GameplayEffectSpec = 1 << 1,
        AttributeModifier = 1 << 2,
        MagnitudeEvaluation = 1 << 3,
        TagTaxonomy = 1 << 4,
        TagRequirement = 1 << 5,
        GameplayCue = 1 << 6,
        AbilitySystemComponentBinding = 1 << 7,
        ActiveGameplayEffect = 1 << 8,
        SetByCallerMagnitude = 1 << 9,
        ExecutionCalculation = 1 << 10,
        GrantedAbility = 1 << 11,
        AbilityTaskContinuation = 1 << 12,
    }

    [Flags]
    public enum EGASRuntimeTraceStageFlags
    {
        None = 0,
        AbilityActivationPlan = 1 << 0,
        AbilityRequirementQuery = 1 << 1,
        GECommandSeed = 1 << 2,
        GESpecShape = 1 << 3,
        ModifierMagnitude = 1 << 4,
        AttributeDelta = 1 << 5,
        GameplayFact = 1 << 6,
        GameplayCue = 1 << 7,
        ActiveEffectMutation = 1 << 8,
        BoundaryProjection = 1 << 9,
        AbilityTaskContinuation = 1 << 10,
    }

    public readonly struct GASRuntimeConceptCoverageSnapshot
    {
        public readonly int SchemaVersion;
        public readonly int AbilityCount;
        public readonly int GameplayEffectCount;
        public readonly int ModifierCount;
        public readonly int RequirementCount;
        public readonly int TagMaskCount;
        public readonly int GameplayCueCount;
        public readonly int GrantedAbilityCount;
        public readonly int ActiveGameplayEffectDefinitionCount;
        public readonly int SetByCallerModifierCount;
        public readonly int ExecutionCalculationModifierCount;
        public readonly EGASOfficialConceptCoverageFlags CoveredConcepts;
        public readonly EGASOfficialConceptCoverageFlags MissingConcepts;

        public GASRuntimeConceptCoverageSnapshot(
            int schemaVersion,
            int abilityCount,
            int gameplayEffectCount,
            int modifierCount,
            int requirementCount,
            int tagMaskCount,
            int gameplayCueCount,
            int grantedAbilityCount,
            int activeGameplayEffectDefinitionCount,
            int setByCallerModifierCount,
            int executionCalculationModifierCount,
            EGASOfficialConceptCoverageFlags coveredConcepts,
            EGASOfficialConceptCoverageFlags missingConcepts)
        {
            SchemaVersion = schemaVersion;
            AbilityCount = abilityCount;
            GameplayEffectCount = gameplayEffectCount;
            ModifierCount = modifierCount;
            RequirementCount = requirementCount;
            TagMaskCount = tagMaskCount;
            GameplayCueCount = gameplayCueCount;
            GrantedAbilityCount = grantedAbilityCount;
            ActiveGameplayEffectDefinitionCount = activeGameplayEffectDefinitionCount;
            SetByCallerModifierCount = setByCallerModifierCount;
            ExecutionCalculationModifierCount = executionCalculationModifierCount;
            CoveredConcepts = coveredConcepts;
            MissingConcepts = missingConcepts;
        }

        public readonly int CoveredConceptMask => (int)CoveredConcepts;

        public readonly int MissingConceptMask => (int)MissingConcepts;

        public readonly bool HasMinimumRuntimeCoverage =>
            (MissingConcepts & GASRuntimeConceptCoverage.RequiredMinimumConcepts)
            == EGASOfficialConceptCoverageFlags.None;
    }

    public readonly struct GASRuntimeTracePreview
    {
        public readonly int Frame;
        public readonly int AbilityCode;
        public readonly int Level;
        public readonly Entity SourceAsc;
        public readonly Entity TargetAsc;
        public readonly Entity SourceAbility;
        public readonly int AbilityFailureReasonCode;
        public readonly int SeedCount;
        public readonly int ModifierCount;
        public readonly int FactCount;
        public readonly int CueCount;
        public readonly int ActiveMutationSeedCount;
        public readonly int ExecutionCalculationModifierCount;
        public readonly EGASRuntimeTraceStageFlags StageMask;
        public readonly EGASRuntimeTraceStageFlags MissingStageMask;
        public readonly EGASOfficialConceptCoverageFlags ConceptMask;
        public readonly EGASOfficialConceptCoverageFlags MissingConceptMask;

        public GASRuntimeTracePreview(
            int frame,
            int abilityCode,
            int level,
            Entity sourceAsc,
            Entity targetAsc,
            Entity sourceAbility,
            int abilityFailureReasonCode,
            int seedCount,
            int modifierCount,
            int factCount,
            int cueCount,
            int activeMutationSeedCount,
            int executionCalculationModifierCount,
            EGASRuntimeTraceStageFlags stageMask,
            EGASRuntimeTraceStageFlags missingStageMask,
            EGASOfficialConceptCoverageFlags conceptMask,
            EGASOfficialConceptCoverageFlags missingConceptMask)
        {
            Frame = frame;
            AbilityCode = abilityCode;
            Level = level;
            SourceAsc = sourceAsc;
            TargetAsc = targetAsc;
            SourceAbility = sourceAbility;
            AbilityFailureReasonCode = abilityFailureReasonCode;
            SeedCount = seedCount;
            ModifierCount = modifierCount;
            FactCount = factCount;
            CueCount = cueCount;
            ActiveMutationSeedCount = activeMutationSeedCount;
            ExecutionCalculationModifierCount = executionCalculationModifierCount;
            StageMask = stageMask;
            MissingStageMask = missingStageMask;
            ConceptMask = conceptMask;
            MissingConceptMask = missingConceptMask;
        }

        public readonly int StageMaskValue => (int)StageMask;

        public readonly int MissingStageMaskValue => (int)MissingStageMask;

        public readonly int ConceptMaskValue => (int)ConceptMask;

        public readonly int MissingConceptMaskValue => (int)MissingConceptMask;
    }

    public static class GASRuntimeConceptCoverage
    {
        public const EGASOfficialConceptCoverageFlags RequiredMinimumConcepts =
            EGASOfficialConceptCoverageFlags.AbilityLifecycle
            | EGASOfficialConceptCoverageFlags.GameplayEffectSpec
            | EGASOfficialConceptCoverageFlags.AttributeModifier
            | EGASOfficialConceptCoverageFlags.MagnitudeEvaluation
            | EGASOfficialConceptCoverageFlags.TagTaxonomy
            | EGASOfficialConceptCoverageFlags.TagRequirement
            | EGASOfficialConceptCoverageFlags.GameplayCue
            | EGASOfficialConceptCoverageFlags.AbilitySystemComponentBinding
            | EGASOfficialConceptCoverageFlags.ActiveGameplayEffect
            | EGASOfficialConceptCoverageFlags.ExecutionCalculation;

        public const EGASOfficialConceptCoverageFlags OfficialConceptMatrix =
            RequiredMinimumConcepts
            | EGASOfficialConceptCoverageFlags.SetByCallerMagnitude
            | EGASOfficialConceptCoverageFlags.GrantedAbility
            | EGASOfficialConceptCoverageFlags.AbilityTaskContinuation;

        public const EGASRuntimeTraceStageFlags RequiredTraceStages =
            EGASRuntimeTraceStageFlags.AbilityActivationPlan
            | EGASRuntimeTraceStageFlags.GECommandSeed
            | EGASRuntimeTraceStageFlags.GESpecShape
            | EGASRuntimeTraceStageFlags.ModifierMagnitude
            | EGASRuntimeTraceStageFlags.AttributeDelta
            | EGASRuntimeTraceStageFlags.GameplayFact
            | EGASRuntimeTraceStageFlags.BoundaryProjection
            | EGASRuntimeTraceStageFlags.AbilityTaskContinuation;
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
        public TagRequirementMask RemoveGameplayEffectTagQuery;
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
        public TagRequirementMask TagQuery;
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
