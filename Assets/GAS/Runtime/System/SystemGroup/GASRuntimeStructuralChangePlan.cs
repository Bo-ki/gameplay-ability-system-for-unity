using System;
using System.Collections.Generic;

namespace GAS.Runtime
{
    public enum GASRuntimeStructuralChangeEntryId
    {
        None = 0,
        AscOwnerLocalInitialization = 1,
        AbilityCommandBufferConsumption = 2,
        AbilityCommitGateMutation = 3,
        AbilityLifecycleCleanup = 4,
        GameplayEffectApplyRequestConsumption = 6,
        GameplayEffectActiveRuntimeMutation = 7,
        ExecutionCalculationOutputMutation = 8,
        AttributeDirtyRecalculate = 9,
        TagMaskDirtySync = 10,
        ObservationProjectionBoundary = 11,
    }

    [Flags]
    public enum GASRuntimeStructuralOperation
    {
        None = 0,
        CreateEntity = 1 << 0,
        DestroyEntity = 1 << 1,
        AddComponent = 1 << 2,
        RemoveComponent = 1 << 3,
        AddBuffer = 1 << 4,
        SetComponent = 1 << 5,
        SetComponentEnabled = 1 << 6,
        DynamicBufferMutation = 1 << 7,
        QueryMaterialization = 1 << 8,
        ManagedComponentWrite = 1 << 9,
    }

    [Flags]
    public enum GASRuntimeStructuralMigrationStep
    {
        None = 0,
        AlreadyEcb = 1 << 0,
        EcbFirst = 1 << 1,
        EnableableAfterEcb = 1 << 2,
        DirtyTrackingCandidate = 1 << 3,
        ManagedBoundaryOnly = 1 << 4,
        KeepMainThread = 1 << 5,
    }

    [Flags]
    public enum GASRuntimeStructuralEligibility
    {
        None = 0,
        CanUseEcb = 1 << 0,
        AlreadyUsesEcb = 1 << 1,
        CanUseEnableableTransientMarker = 1 << 2,
        CanUseDirtyPipeline = 1 << 3,
        RequiresSemanticDecision = 1 << 4,
        RequiresManagedBoundary = 1 << 5,
        KeepMainThread = 1 << 6,
        MirrorsLayoutStructuralHotspot = 1 << 7,
        NoSimulationStructuralMigration = 1 << 8,
    }

    [Flags]
    public enum GASRuntimeStructuralBoundary
    {
        None = 0,
        TransientCommandConsumption = 1 << 0,
        RuntimeEntityLifecycle = 1 << 1,
        AbilityLifecycleMarker = 1 << 2,
        GameplayEffectInstanceLifecycle = 1 << 3,
        DefinitionRuntimeTransition = 1 << 4,
        AttributeDirtyPropagation = 1 << 5,
        TagDirtyPropagation = 1 << 6,
        ObservationProjectionOnly = 1 << 7,
        ManagedPresentation = 1 << 8,
        CrossEntityLookup = 1 << 9,
        DynamicBufferMutation = 1 << 10,
    }

    [Flags]
    public enum GASRuntimeDirtyPipelineSignal
    {
        None = 0,
        AttributeBaseValueDirty = 1 << 0,
        AttributeCurrentValueProjection = 1 << 1,
        TagMaskDirty = 1 << 2,
        GameplayEffectModifierDirty = 1 << 3,
        GameplayEffectLifecycleDirty = 1 << 4,
        ExecutionCalculationOutputDirty = 1 << 5,
        AbilityLifecycleDirty = 1 << 6,
    }

    [Flags]
    public enum GASRuntimeEnableableScope
    {
        None = 0,
        OwnerLocalPendingMarker = 1 << 0,
        TransientCommandMarker = 1 << 1,
        TransientLifecycleMarker = 1 << 2,
        DirtyMarker = 1 << 3,
        ManagedPresentationState = 1 << 4,
        RuntimeSemanticStateRequiresDecision = 1 << 5,
    }

    public readonly struct GASRuntimeStructuralChangeEntry
    {
        private readonly GASRuntimeLayoutComponentSlot[] _affectedSlots;
        private readonly Type[] _systemTypes;

        public readonly GASRuntimeStructuralChangeEntryId EntryId;
        public readonly GASRuntimeQueryLayoutEntryId LayoutEntryId;
        public readonly GASRuntimeLayoutDomain Domain;
        public readonly GASRuntimeEntityKind EntityKind;
        public readonly GASRuntimeLayoutDecision SourceLayoutDecision;
        public readonly GASRuntimeLayoutCapability SourceLayoutCapabilities;
        public readonly GASRuntimeStructuralOperation Operations;
        public readonly GASRuntimeStructuralMigrationStep MigrationSteps;
        public readonly GASRuntimeStructuralEligibility Eligibility;
        public readonly GASRuntimeStructuralBoundary Boundaries;
        public readonly GASRuntimeDirtyPipelineSignal DirtySignals;
        public readonly GASRuntimeEnableableScope EnableableScopes;

        public GASRuntimeStructuralChangeEntry(
            GASRuntimeStructuralChangeEntryId entryId,
            GASRuntimeQueryLayoutEntry layoutEntry,
            GASRuntimeStructuralOperation operations,
            GASRuntimeStructuralMigrationStep migrationSteps,
            GASRuntimeStructuralEligibility eligibility,
            GASRuntimeStructuralBoundary boundaries,
            GASRuntimeDirtyPipelineSignal dirtySignals,
            GASRuntimeEnableableScope enableableScopes,
            IEnumerable<GASRuntimeLayoutComponentSlot> affectedSlots)
        {
            EntryId = entryId;
            LayoutEntryId = layoutEntry.EntryId;
            Domain = layoutEntry.Domain;
            EntityKind = layoutEntry.EntityKind;
            SourceLayoutDecision = layoutEntry.Decision;
            SourceLayoutCapabilities = layoutEntry.Capabilities;
            Operations = operations;
            MigrationSteps = migrationSteps;
            Eligibility = eligibility;
            Boundaries = boundaries;
            DirtySignals = dirtySignals;
            EnableableScopes = enableableScopes;
            _affectedSlots = Materialize(affectedSlots);
            _systemTypes = Materialize(layoutEntry.SystemTypes);
        }

        public IReadOnlyList<GASRuntimeLayoutComponentSlot> AffectedSlots =>
            _affectedSlots ?? Array.Empty<GASRuntimeLayoutComponentSlot>();

        public IReadOnlyList<Type> SystemTypes => _systemTypes ?? Array.Empty<Type>();

        public bool IsSimulationMigrationCandidate =>
            !HasEligibility(GASRuntimeStructuralEligibility.NoSimulationStructuralMigration)
            && !HasMigrationStep(GASRuntimeStructuralMigrationStep.ManagedBoundaryOnly)
            && !HasBoundary(GASRuntimeStructuralBoundary.ObservationProjectionOnly);

        public bool HasOperation(GASRuntimeStructuralOperation operation)
        {
            return operation != GASRuntimeStructuralOperation.None
                   && (Operations & operation) == operation;
        }

        public bool HasMigrationStep(GASRuntimeStructuralMigrationStep step)
        {
            return step != GASRuntimeStructuralMigrationStep.None
                   && (MigrationSteps & step) == step;
        }

        public bool HasEligibility(GASRuntimeStructuralEligibility eligibility)
        {
            return eligibility != GASRuntimeStructuralEligibility.None
                   && (Eligibility & eligibility) == eligibility;
        }

        public bool HasBoundary(GASRuntimeStructuralBoundary boundary)
        {
            return boundary != GASRuntimeStructuralBoundary.None
                   && (Boundaries & boundary) == boundary;
        }

        public bool HasDirtySignal(GASRuntimeDirtyPipelineSignal signal)
        {
            return signal != GASRuntimeDirtyPipelineSignal.None
                   && (DirtySignals & signal) == signal;
        }

        public bool HasEnableableScope(GASRuntimeEnableableScope scope)
        {
            return scope != GASRuntimeEnableableScope.None
                   && (EnableableScopes & scope) == scope;
        }

        public bool HasAffectedSlot(GASRuntimeLayoutComponentSlot slot)
        {
            if (slot == GASRuntimeLayoutComponentSlot.None)
                return false;

            var slots = AffectedSlots;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] == slot)
                    return true;
            }

            return false;
        }

        private static T[] Materialize<T>(IEnumerable<T> values)
        {
            if (values == null)
                return Array.Empty<T>();

            return values is T[] array ? (T[])array.Clone() : new List<T>(values).ToArray();
        }
    }

    public readonly struct GASRuntimeStructuralChangePlan
    {
        private readonly GASRuntimeStructuralChangeEntry[] _entries;

        public GASRuntimeStructuralChangePlan(IEnumerable<GASRuntimeStructuralChangeEntry> entries)
        {
            _entries = entries is GASRuntimeStructuralChangeEntry[] array
                ? (GASRuntimeStructuralChangeEntry[])array.Clone()
                : new List<GASRuntimeStructuralChangeEntry>(
                    entries ?? Array.Empty<GASRuntimeStructuralChangeEntry>()).ToArray();
        }

        public static GASRuntimeStructuralChangePlan Empty =>
            new(Array.Empty<GASRuntimeStructuralChangeEntry>());

        public IReadOnlyList<GASRuntimeStructuralChangeEntry> Entries =>
            _entries ?? Array.Empty<GASRuntimeStructuralChangeEntry>();

        public int EntryCount => Entries.Count;

        public int AlreadyEcbCount => CountEntriesWithMigrationStep(GASRuntimeStructuralMigrationStep.AlreadyEcb);

        public int EcbFirstCount => CountEntriesWithMigrationStep(GASRuntimeStructuralMigrationStep.EcbFirst);

        public int EnableableAfterEcbCount =>
            CountEntriesWithMigrationStep(GASRuntimeStructuralMigrationStep.EnableableAfterEcb);

        public int DirtyPipelineCandidateCount =>
            CountEntriesWithEligibility(GASRuntimeStructuralEligibility.CanUseDirtyPipeline);

        public int ManagedBoundaryOnlyCount =>
            CountEntriesWithMigrationStep(GASRuntimeStructuralMigrationStep.ManagedBoundaryOnly);

        public int RequiresSemanticDecisionCount =>
            CountEntriesWithEligibility(GASRuntimeStructuralEligibility.RequiresSemanticDecision);

        public bool RequiresEcbMigrationBeforeEnableableRollout => EcbFirstCount > 0;

        public bool HasDirtyPipelineCandidates => DirtyPipelineCandidateCount > 0;

        public bool TryFind(
            GASRuntimeStructuralChangeEntryId entryId,
            out GASRuntimeStructuralChangeEntry entry)
        {
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].EntryId == entryId)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public bool TryFindFirstByLayoutEntry(
            GASRuntimeQueryLayoutEntryId layoutEntryId,
            out GASRuntimeStructuralChangeEntry entry)
        {
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].LayoutEntryId == layoutEntryId)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public int CountEntriesForLayoutEntry(GASRuntimeQueryLayoutEntryId layoutEntryId)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].LayoutEntryId == layoutEntryId)
                    count++;
            }

            return count;
        }

        public int CountEntriesWithMigrationStep(GASRuntimeStructuralMigrationStep step)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].HasMigrationStep(step))
                    count++;
            }

            return count;
        }

        public int CountEntriesWithEligibility(GASRuntimeStructuralEligibility eligibility)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].HasEligibility(eligibility))
                    count++;
            }

            return count;
        }

        public int CountEntriesWithBoundary(GASRuntimeStructuralBoundary boundary)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].HasBoundary(boundary))
                    count++;
            }

            return count;
        }

        public int CountEntriesWithDirtySignal(GASRuntimeDirtyPipelineSignal signal)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].HasDirtySignal(signal))
                    count++;
            }

            return count;
        }
    }

    public static class GASRuntimeStructuralChangePlanner
    {
        public static GASRuntimeStructuralChangePlan CreateCurrent()
        {
            return CreateCurrent(GASRuntimeQueryLayoutPlanner.CreateCurrent());
        }

        public static GASRuntimeStructuralChangePlan CreateCurrent(GASRuntimeQueryLayoutPlan layoutPlan)
        {
            var entries = new List<GASRuntimeStructuralChangeEntry>();

            AddIfLayoutEntryExists(
                entries,
                layoutPlan,
                GASRuntimeStructuralChangeEntryId.AscOwnerLocalInitialization,
                GASRuntimeQueryLayoutEntryId.AscStableState,
                GASRuntimeStructuralOperation.CreateEntity
                | GASRuntimeStructuralOperation.AddComponent
                | GASRuntimeStructuralOperation.AddBuffer
                | GASRuntimeStructuralOperation.SetComponent
                | GASRuntimeStructuralOperation.SetComponentEnabled
                | GASRuntimeStructuralOperation.DynamicBufferMutation,
                GASRuntimeStructuralMigrationStep.AlreadyEcb,
                GASRuntimeStructuralEligibility.CanUseEcb
                | GASRuntimeStructuralEligibility.AlreadyUsesEcb
                | GASRuntimeStructuralEligibility.CanUseEnableableTransientMarker,
                GASRuntimeStructuralBoundary.TransientCommandConsumption
                | GASRuntimeStructuralBoundary.RuntimeEntityLifecycle
                | GASRuntimeStructuralBoundary.DynamicBufferMutation,
                GASRuntimeDirtyPipelineSignal.None,
                GASRuntimeEnableableScope.OwnerLocalPendingMarker,
                GASRuntimeLayoutComponentSlot.AscBasicData,
                GASRuntimeLayoutComponentSlot.TagMask,
                GASRuntimeLayoutComponentSlot.AttributeBuffer,
                GASRuntimeLayoutComponentSlot.ActiveModifierBuffer,
                GASRuntimeLayoutComponentSlot.GrantedAbilityBuffer,
                GASRuntimeLayoutComponentSlot.GameplayEffectBuffer);

            AddIfLayoutEntryExists(
                entries,
                layoutPlan,
                GASRuntimeStructuralChangeEntryId.AbilityCommandBufferConsumption,
                GASRuntimeQueryLayoutEntryId.AbilityCommandBuffer,
                GASRuntimeStructuralOperation.CreateEntity
                | GASRuntimeStructuralOperation.DestroyEntity
                | GASRuntimeStructuralOperation.AddComponent
                | GASRuntimeStructuralOperation.AddBuffer
                | GASRuntimeStructuralOperation.SetComponent
                | GASRuntimeStructuralOperation.QueryMaterialization
                | GASRuntimeStructuralOperation.DynamicBufferMutation,
                GASRuntimeStructuralMigrationStep.AlreadyEcb
                | GASRuntimeStructuralMigrationStep.EnableableAfterEcb,
                GASRuntimeStructuralEligibility.CanUseEcb
                | GASRuntimeStructuralEligibility.CanUseEnableableTransientMarker
                | GASRuntimeStructuralEligibility.RequiresSemanticDecision
                | GASRuntimeStructuralEligibility.MirrorsLayoutStructuralHotspot,
                GASRuntimeStructuralBoundary.TransientCommandConsumption
                | GASRuntimeStructuralBoundary.RuntimeEntityLifecycle
                | GASRuntimeStructuralBoundary.DynamicBufferMutation,
                GASRuntimeDirtyPipelineSignal.None,
                GASRuntimeEnableableScope.OwnerLocalPendingMarker
                | GASRuntimeEnableableScope.TransientCommandMarker,
                GASRuntimeLayoutComponentSlot.AbilityCommandBuffer,
                GASRuntimeLayoutComponentSlot.GrantedAbilityBuffer);

            AddIfLayoutEntryExists(
                entries,
                layoutPlan,
                GASRuntimeStructuralChangeEntryId.AbilityCommitGateMutation,
                GASRuntimeQueryLayoutEntryId.AbilityCommitGate,
                GASRuntimeStructuralOperation.CreateEntity
                | GASRuntimeStructuralOperation.AddComponent
                | GASRuntimeStructuralOperation.RemoveComponent
                | GASRuntimeStructuralOperation.SetComponent
                | GASRuntimeStructuralOperation.QueryMaterialization
                | GASRuntimeStructuralOperation.DynamicBufferMutation,
                GASRuntimeStructuralMigrationStep.EcbFirst
                | GASRuntimeStructuralMigrationStep.EnableableAfterEcb,
                GASRuntimeStructuralEligibility.CanUseEcb
                | GASRuntimeStructuralEligibility.CanUseEnableableTransientMarker
                | GASRuntimeStructuralEligibility.RequiresSemanticDecision
                | GASRuntimeStructuralEligibility.MirrorsLayoutStructuralHotspot,
                GASRuntimeStructuralBoundary.TransientCommandConsumption
                | GASRuntimeStructuralBoundary.AbilityLifecycleMarker
                | GASRuntimeStructuralBoundary.DefinitionRuntimeTransition
                | GASRuntimeStructuralBoundary.CrossEntityLookup
                | GASRuntimeStructuralBoundary.DynamicBufferMutation,
                GASRuntimeDirtyPipelineSignal.None,
                GASRuntimeEnableableScope.TransientCommandMarker
                | GASRuntimeEnableableScope.RuntimeSemanticStateRequiresDecision,
                GASRuntimeLayoutComponentSlot.AbilityCommitRequest,
                GASRuntimeLayoutComponentSlot.AbilityActive,
                GASRuntimeLayoutComponentSlot.AbilityRuntimeState,
                GASRuntimeLayoutComponentSlot.TagMask,
                GASRuntimeLayoutComponentSlot.AttributeBuffer);

            AddIfLayoutEntryExists(
                entries,
                layoutPlan,
                GASRuntimeStructuralChangeEntryId.AbilityLifecycleCleanup,
                GASRuntimeQueryLayoutEntryId.AbilityTickLifecycle,
                GASRuntimeStructuralOperation.DestroyEntity
                | GASRuntimeStructuralOperation.AddComponent
                | GASRuntimeStructuralOperation.RemoveComponent
                | GASRuntimeStructuralOperation.SetComponent
                | GASRuntimeStructuralOperation.QueryMaterialization
                | GASRuntimeStructuralOperation.DynamicBufferMutation,
                GASRuntimeStructuralMigrationStep.EcbFirst
                | GASRuntimeStructuralMigrationStep.EnableableAfterEcb,
                GASRuntimeStructuralEligibility.CanUseEcb
                | GASRuntimeStructuralEligibility.CanUseEnableableTransientMarker
                | GASRuntimeStructuralEligibility.CanUseDirtyPipeline
                | GASRuntimeStructuralEligibility.RequiresSemanticDecision,
                GASRuntimeStructuralBoundary.RuntimeEntityLifecycle
                | GASRuntimeStructuralBoundary.AbilityLifecycleMarker
                | GASRuntimeStructuralBoundary.GameplayEffectInstanceLifecycle
                | GASRuntimeStructuralBoundary.DynamicBufferMutation,
                GASRuntimeDirtyPipelineSignal.AbilityLifecycleDirty,
                GASRuntimeEnableableScope.TransientLifecycleMarker
                | GASRuntimeEnableableScope.RuntimeSemanticStateRequiresDecision,
                GASRuntimeLayoutComponentSlot.AbilityBaseInfo,
                GASRuntimeLayoutComponentSlot.AbilityRuntimeState,
                GASRuntimeLayoutComponentSlot.AbilityActive,
                GASRuntimeLayoutComponentSlot.EffectDestroy);

            AddIfLayoutEntryExists(
                entries,
                layoutPlan,
                GASRuntimeStructuralChangeEntryId.GameplayEffectApplyRequestConsumption,
                GASRuntimeQueryLayoutEntryId.GameplayEffectApplyRequest,
                GASRuntimeStructuralOperation.CreateEntity
                | GASRuntimeStructuralOperation.DestroyEntity
                | GASRuntimeStructuralOperation.AddComponent
                | GASRuntimeStructuralOperation.AddBuffer
                | GASRuntimeStructuralOperation.SetComponent
                | GASRuntimeStructuralOperation.QueryMaterialization
                | GASRuntimeStructuralOperation.DynamicBufferMutation,
                GASRuntimeStructuralMigrationStep.EcbFirst
                | GASRuntimeStructuralMigrationStep.DirtyTrackingCandidate,
                GASRuntimeStructuralEligibility.CanUseEcb
                | GASRuntimeStructuralEligibility.CanUseDirtyPipeline
                | GASRuntimeStructuralEligibility.RequiresSemanticDecision
                | GASRuntimeStructuralEligibility.MirrorsLayoutStructuralHotspot,
                GASRuntimeStructuralBoundary.TransientCommandConsumption
                | GASRuntimeStructuralBoundary.GameplayEffectInstanceLifecycle
                | GASRuntimeStructuralBoundary.DefinitionRuntimeTransition
                | GASRuntimeStructuralBoundary.AttributeDirtyPropagation
                | GASRuntimeStructuralBoundary.TagDirtyPropagation
                | GASRuntimeStructuralBoundary.DynamicBufferMutation,
                GASRuntimeDirtyPipelineSignal.GameplayEffectModifierDirty
                | GASRuntimeDirtyPipelineSignal.AttributeBaseValueDirty
                | GASRuntimeDirtyPipelineSignal.TagMaskDirty,
                GASRuntimeEnableableScope.DirtyMarker,
                GASRuntimeLayoutComponentSlot.ApplyGameplayEffectRequest,
                GASRuntimeLayoutComponentSlot.EffectContext,
                GASRuntimeLayoutComponentSlot.EffectSpecData,
                GASRuntimeLayoutComponentSlot.ResolvedModifierBuffer,
                GASRuntimeLayoutComponentSlot.ActiveModifierBuffer,
                GASRuntimeLayoutComponentSlot.TagMask);

            AddIfLayoutEntryExists(
                entries,
                layoutPlan,
                GASRuntimeStructuralChangeEntryId.GameplayEffectActiveRuntimeMutation,
                GASRuntimeQueryLayoutEntryId.GameplayEffectActiveRuntime,
                GASRuntimeStructuralOperation.DestroyEntity
                | GASRuntimeStructuralOperation.AddComponent
                | GASRuntimeStructuralOperation.RemoveComponent
                | GASRuntimeStructuralOperation.AddBuffer
                | GASRuntimeStructuralOperation.SetComponent
                | GASRuntimeStructuralOperation.QueryMaterialization
                | GASRuntimeStructuralOperation.DynamicBufferMutation,
                GASRuntimeStructuralMigrationStep.EcbFirst
                | GASRuntimeStructuralMigrationStep.EnableableAfterEcb
                | GASRuntimeStructuralMigrationStep.DirtyTrackingCandidate,
                GASRuntimeStructuralEligibility.CanUseEcb
                | GASRuntimeStructuralEligibility.CanUseEnableableTransientMarker
                | GASRuntimeStructuralEligibility.CanUseDirtyPipeline
                | GASRuntimeStructuralEligibility.RequiresSemanticDecision
                | GASRuntimeStructuralEligibility.MirrorsLayoutStructuralHotspot,
                GASRuntimeStructuralBoundary.RuntimeEntityLifecycle
                | GASRuntimeStructuralBoundary.GameplayEffectInstanceLifecycle
                | GASRuntimeStructuralBoundary.AttributeDirtyPropagation
                | GASRuntimeStructuralBoundary.TagDirtyPropagation
                | GASRuntimeStructuralBoundary.CrossEntityLookup
                | GASRuntimeStructuralBoundary.DynamicBufferMutation,
                GASRuntimeDirtyPipelineSignal.GameplayEffectLifecycleDirty
                | GASRuntimeDirtyPipelineSignal.GameplayEffectModifierDirty
                | GASRuntimeDirtyPipelineSignal.AttributeBaseValueDirty
                | GASRuntimeDirtyPipelineSignal.TagMaskDirty,
                GASRuntimeEnableableScope.TransientLifecycleMarker
                | GASRuntimeEnableableScope.DirtyMarker
                | GASRuntimeEnableableScope.RuntimeSemanticStateRequiresDecision,
                GASRuntimeLayoutComponentSlot.EffectContext,
                GASRuntimeLayoutComponentSlot.EffectSpecData,
                GASRuntimeLayoutComponentSlot.EffectLifecycle,
                GASRuntimeLayoutComponentSlot.EffectCleanup,
                GASRuntimeLayoutComponentSlot.EffectDestroy,
                GASRuntimeLayoutComponentSlot.EffectFinalDestroy,
                GASRuntimeLayoutComponentSlot.ActiveEffectGlobalIndexStableRow,
                GASRuntimeLayoutComponentSlot.ActiveEffectGlobalIndexStore,
                GASRuntimeLayoutComponentSlot.ActiveEffectGlobalIndexBuffer,
                GASRuntimeLayoutComponentSlot.DurationRuntime,
                GASRuntimeLayoutComponentSlot.PeriodRuntime,
                GASRuntimeLayoutComponentSlot.StackingRuntime,
                GASRuntimeLayoutComponentSlot.ActiveModifierBuffer,
                GASRuntimeLayoutComponentSlot.GameplayEffectBuffer);

            AddIfLayoutEntryExists(
                entries,
                layoutPlan,
                GASRuntimeStructuralChangeEntryId.ExecutionCalculationOutputMutation,
                GASRuntimeQueryLayoutEntryId.ExecutionCalculationPipeline,
                GASRuntimeStructuralOperation.SetComponent
                | GASRuntimeStructuralOperation.SetComponentEnabled
                | GASRuntimeStructuralOperation.DynamicBufferMutation,
                GASRuntimeStructuralMigrationStep.AlreadyEcb
                | GASRuntimeStructuralMigrationStep.DirtyTrackingCandidate,
                GASRuntimeStructuralEligibility.AlreadyUsesEcb
                | GASRuntimeStructuralEligibility.CanUseEnableableTransientMarker
                | GASRuntimeStructuralEligibility.CanUseDirtyPipeline
                | GASRuntimeStructuralEligibility.NoSimulationStructuralMigration,
                GASRuntimeStructuralBoundary.DefinitionRuntimeTransition
                | GASRuntimeStructuralBoundary.AttributeDirtyPropagation
                | GASRuntimeStructuralBoundary.DynamicBufferMutation,
                GASRuntimeDirtyPipelineSignal.ExecutionCalculationOutputDirty
                | GASRuntimeDirtyPipelineSignal.GameplayEffectModifierDirty
                | GASRuntimeDirtyPipelineSignal.AttributeBaseValueDirty,
                GASRuntimeEnableableScope.DirtyMarker,
                GASRuntimeLayoutComponentSlot.ExecutionCalculationDefinitionBuffer,
                GASRuntimeLayoutComponentSlot.ExecutionCalculationValueBuffer,
                GASRuntimeLayoutComponentSlot.ResolvedModifierBuffer,
                GASRuntimeLayoutComponentSlot.ActiveModifierBuffer);

            AddIfLayoutEntryExists(
                entries,
                layoutPlan,
                GASRuntimeStructuralChangeEntryId.AttributeDirtyRecalculate,
                GASRuntimeQueryLayoutEntryId.AttributeRecalculate,
                GASRuntimeStructuralOperation.DynamicBufferMutation,
                GASRuntimeStructuralMigrationStep.DirtyTrackingCandidate,
                GASRuntimeStructuralEligibility.CanUseDirtyPipeline,
                GASRuntimeStructuralBoundary.AttributeDirtyPropagation
                | GASRuntimeStructuralBoundary.DynamicBufferMutation,
                GASRuntimeDirtyPipelineSignal.AttributeBaseValueDirty
                | GASRuntimeDirtyPipelineSignal.AttributeCurrentValueProjection,
                GASRuntimeEnableableScope.DirtyMarker,
                GASRuntimeLayoutComponentSlot.AttributeBuffer,
                GASRuntimeLayoutComponentSlot.ActiveModifierBuffer);

            AddIfLayoutEntryExists(
                entries,
                layoutPlan,
                GASRuntimeStructuralChangeEntryId.TagMaskDirtySync,
                GASRuntimeQueryLayoutEntryId.TagMaskRuntime,
                GASRuntimeStructuralOperation.DynamicBufferMutation,
                GASRuntimeStructuralMigrationStep.DirtyTrackingCandidate,
                GASRuntimeStructuralEligibility.CanUseDirtyPipeline,
                GASRuntimeStructuralBoundary.TagDirtyPropagation
                | GASRuntimeStructuralBoundary.DynamicBufferMutation,
                GASRuntimeDirtyPipelineSignal.TagMaskDirty,
                GASRuntimeEnableableScope.DirtyMarker,
                GASRuntimeLayoutComponentSlot.TagMask);

            AddIfLayoutEntryExists(
                entries,
                layoutPlan,
                GASRuntimeStructuralChangeEntryId.ObservationProjectionBoundary,
                GASRuntimeQueryLayoutEntryId.ObservationReplayAndOutbox,
                GASRuntimeStructuralOperation.QueryMaterialization
                | GASRuntimeStructuralOperation.DynamicBufferMutation,
                GASRuntimeStructuralMigrationStep.KeepMainThread,
                GASRuntimeStructuralEligibility.KeepMainThread
                | GASRuntimeStructuralEligibility.NoSimulationStructuralMigration,
                GASRuntimeStructuralBoundary.ObservationProjectionOnly
                | GASRuntimeStructuralBoundary.DynamicBufferMutation,
                GASRuntimeDirtyPipelineSignal.None,
                GASRuntimeEnableableScope.None,
                GASRuntimeLayoutComponentSlot.GameplayEventBus,
                GASRuntimeLayoutComponentSlot.PresentationOutbox,
                GASRuntimeLayoutComponentSlot.DebugReplayLog);

            return new GASRuntimeStructuralChangePlan(entries);
        }

        private static void AddIfLayoutEntryExists(
            List<GASRuntimeStructuralChangeEntry> entries,
            GASRuntimeQueryLayoutPlan layoutPlan,
            GASRuntimeStructuralChangeEntryId entryId,
            GASRuntimeQueryLayoutEntryId layoutEntryId,
            GASRuntimeStructuralOperation operations,
            GASRuntimeStructuralMigrationStep migrationSteps,
            GASRuntimeStructuralEligibility eligibility,
            GASRuntimeStructuralBoundary boundaries,
            GASRuntimeDirtyPipelineSignal dirtySignals,
            GASRuntimeEnableableScope enableableScopes,
            params GASRuntimeLayoutComponentSlot[] affectedSlots)
        {
            if (!layoutPlan.TryFind(layoutEntryId, out var layoutEntry))
                return;

            entries.Add(new GASRuntimeStructuralChangeEntry(
                entryId,
                layoutEntry,
                operations,
                migrationSteps,
                eligibility,
                boundaries,
                dirtySignals,
                enableableScopes,
                affectedSlots));
        }
    }
}
