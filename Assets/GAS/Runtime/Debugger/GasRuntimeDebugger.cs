using System;
using System.Diagnostics;
using System.Text;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public enum EGasRuntimeDiagnosticKind : byte
    {
        TickSummary = 0,
        GroupTiming = 1,
        SystemTiming = 2,
        BufferPressure = 3,
        StructuralChange = 4,
        RuntimeCoreCounters = 5,
        RuntimeCoreFrameBackbone = 6,
        ObservationMaterialization = 7,
    }

    public enum EGasRuntimeDiagnosticSeverity : byte
    {
        Trace = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    public enum EGasRuntimeDiagnosticModule : byte
    {
        Runtime = 0,
        Command = 1,
        ResetDirty = 2,
        Tag = 3,
        Effect = 4,
        Attribute = 5,
        Ability = 6,
        Cue = 7,
        EventBus = 8,
        Presentation = 9,
    }

    public struct GASRuntimeDebuggerComponent : IComponentData
    {
        public byte Enabled;
        public byte CaptureSystemTimings;
        public byte CaptureBufferPressure;
        public int NextSequence;
        public int FirstRetainedSequence;
        public int DroppedEventCount;
        public int MaxRetainedEvents;
        public int SlowTickMicroseconds;
        public int SlowSystemMicroseconds;
        public int BufferPressureWarningPermille;
        public int BufferPressureErrorPermille;
        public int RuntimeCoreRequestCount;
        public int RuntimeCoreSpecCount;
        public int RuntimeCoreDeltaCount;
        public int RuntimeCoreFactCount;
        public int RuntimeCoreCueCount;
        public int RuntimeCorePresentationCount;
        public int RuntimeCoreEntityCreateCount;
        public int RuntimeCoreEntityDestroyCount;
        public int RuntimeCoreEcbPlaybackCount;
        public int RuntimeCorePeakActiveEffectEntityCount;
        public int RuntimeCorePeakPendingApplyCommandOwnerCount;
        public int RuntimeCorePeakEventBusBufferLength;
        public int RuntimeCorePeakPresentationCursorLag;
        public int RuntimeCorePeakReplayCursorLag;
        public int RuntimeCoreActiveEffectStoreOwnerCount;
        public int RuntimeCoreActiveEffectSlotCount;
        public int RuntimeCoreActiveEffectSlotCapacity;
        public int RuntimeCoreActiveEffectSlotPendingApplyCount;
        public int RuntimeCoreActiveEffectSlotActiveCount;
        public int RuntimeCoreActiveEffectSlotInhibitedCount;
        public int RuntimeCoreActiveEffectSlotPendingRemoveCount;
        public int RuntimeCoreActiveEffectSlotLegacyBackedCount;
        public int RuntimeCoreActiveEffectSlotExternalizedOwnerCount;
        public int RuntimeCoreActiveEffectSlotGrantedTagCount;
        public int RuntimeCoreActiveEffectSlotGrantedAbilityCount;
        public int RuntimeCoreActiveEffectChunkSkipMatchedSlotCount;
        public int RuntimeCoreActiveEffectChunkSkipSkippedSlotCount;
        public int RuntimeCoreActiveEffectChunkSkipDuePeriodSlotCount;
        public int RuntimeCoreActiveEffectChunkSkipNoopSlotCount;
        public int RuntimeCoreActiveEffectChunkSkipOwnerCount;
        public int RuntimeCoreActiveEffectGlobalIndexOwnerCount;
        public int RuntimeCoreActiveEffectGlobalIndexCount;
        public int RuntimeCoreActiveEffectGlobalIndexActiveCount;
        public int RuntimeCoreActiveEffectGlobalIndexInhibitedCount;
        public int RuntimeCoreActiveEffectGlobalIndexPendingRemoveCount;
        public int RuntimeCoreActiveEffectGlobalIndexPeriodDueCount;
        public int RuntimeCoreActiveEffectGlobalIndexDurationDueCount;
        public int RuntimeCoreActiveEffectGlobalIndexStaleCount;
        public int RuntimeCoreActiveEffectGlobalIndexBucketOwnerCount;
        public int RuntimeCoreActiveEffectGlobalIndexBucketIndexCount;
        public int RuntimeCoreActiveEffectGlobalIndexMaxBucketLength;
        public int RuntimeCoreActiveEffectGlobalIndexStableRowCount;
        public int RuntimeCoreActiveEffectGlobalIndexStaleStableRowCount;
        public int RuntimeCoreActiveMutationCommandCount;
        public int RuntimeCoreActiveMutationOwnerGroupCount;
        public int RuntimeCoreActiveMutationMaxOwnerRange;
        public int RuntimeCoreActiveMutationSortMoveCount;
        public int RuntimeCoreActiveMutationEstimatedRandomLookupCount;
        public int RuntimeCoreActiveMutationOwnerResourceLookupCount;
        public int RuntimeCoreActiveMutationMigrationCarrierCount;
        public int RuntimeCorePendingAttributeDeltaCount;
        public int RuntimeCorePendingAttributeAppliedDeltaCount;
        public int RuntimeCorePendingAttributeSkippedDeltaCount;
        public int RuntimeCorePendingAttributeTargetGroupCount;
        public int RuntimeCorePendingAttributeMaxTargetRange;
        public int RuntimeCorePendingAttributeEstimatedRandomLookupCount;
        public int RuntimeCorePendingAttributeFactPatchCount;
        public int RuntimeCorePendingAttributeMigrationCarrierCount;
        public int RuntimeCoreQueryBudget;
        public int RuntimeCoreFilteredQueryBudget;
        public int RuntimeCoreUnfilteredQueryBudget;
        public int RuntimeCoreLookupUpdateBudget;
        public int RuntimeCoreRandomLookupBudget;
        public int RuntimeCoreSyncQueryBudget;
        public int RuntimeCoreHelperTempQueryRiskCount;
        public int RuntimeCoreDependencyWaitRiskCount;
        public int RuntimeCoreWorldUpdateAllocatorOwnerCount;
        public int RuntimeCoreRewindableAllocatorCandidateCount;
        public int RuntimeCoreFrameBackbonePhaseCount;
        public int RuntimeCoreFrameBackboneContractOnlyPhaseCount;
        public int RuntimeCoreFrameBackboneStreamCount;
        public int RuntimeCoreFrameBackboneMigrationCarrierCount;
        public int RuntimeCoreFrameBackboneNativeStreamCandidateCount;
        public int RuntimeCoreFrameBackboneOwnerLocalBufferCandidateCount;
        public int RuntimeCoreFrameBackboneBattleHashStreamCount;
        public int RuntimeCoreFrameBackboneDeterministicMergePolicyCount;
        public int RuntimeCoreFrameBackboneMergeCostMeasuredCount;
        public int RuntimeCoreFrameBackboneMergeCostMicroseconds;
        public int RuntimeCoreFrameBackboneRequiredStructuralPlaybackCount;
        public int RuntimeCoreFrameBackboneRecordedStructuralPlaybackCount;
        public int RuntimeCoreFrameBackboneEcbCommandCount;
        public int RuntimeCoreFrameBackboneBulkQueryCount;
        public int RuntimeCoreFrameBackboneProfilerMarkerCount;
        public int RuntimeCoreFrameBackboneJournalingMarkerCount;
        public int RuntimeCoreFrameBackboneCoreCostGroupCount;
        public int RuntimeCoreFrameBackbonePhysicsCostGroupCount;
        public int RuntimeCoreFrameBackboneRenderCostGroupCount;
        public int RuntimeCoreFrameBackboneRunnerCostGroupCount;
        public int RuntimeCoreFrameBackbonePhysicsDisabledReasonCount;
        public int RuntimeCoreFrameBackboneRenderDisabledReasonCount;
        public int RuntimeCoreFrameBackboneDebuggerOverheadBudgetMicroseconds;
        public int RuntimeCoreFrameBackboneSamplingInterval;
        public int RuntimeCoreFrameBackboneDisablePolicyCount;
        public int RuntimeCoreFrameBackboneBurstWarmupPolicyCount;
        public int RuntimeCoreFrameBackboneHotPathManagedStringCount;
        public int RuntimeCoreFrameBackboneEvidenceMask;
    }

    [InternalBufferCapacity(0)]
    public struct GASRuntimeDiagnosticEventBuffer : IBufferElementData
    {
        public int Sequence;
        public int Frame;
        public EGasRuntimeDiagnosticKind Kind;
        public EGasRuntimeDiagnosticSeverity Severity;
        public EGasRuntimeDiagnosticModule Module;
        public FixedString64Bytes GroupName;
        public FixedString64Bytes SystemName;
        public FixedString64Bytes BufferName;
        public Entity Entity;
        public int ElapsedMicroseconds;
        public int TotalMicroseconds;
        public int CallCount;
        public int Count;
        public int Capacity;
        public int ValueA;
        public int ValueB;
        public int EntityCreateCount;
        public int EntityDestroyCount;
        public int EcbPlaybackCount;
        public int ActiveEffectEntityCount;
        public int PendingApplyCommandOwnerCount;
        public int EventBusBufferLength;
        public int PresentationCursorLag;
        public int ReplayCursorLag;
        public int ActiveEffectStoreOwnerCount;
        public int ActiveEffectSlotCount;
        public int ActiveEffectSlotCapacity;
        public int ActiveEffectSlotPendingApplyCount;
        public int ActiveEffectSlotActiveCount;
        public int ActiveEffectSlotInhibitedCount;
        public int ActiveEffectSlotPendingRemoveCount;
        public int ActiveEffectSlotLegacyBackedCount;
        public int ActiveEffectSlotExternalizedOwnerCount;
        public int ActiveEffectSlotGrantedTagCount;
        public int ActiveEffectSlotGrantedAbilityCount;
        public int ActiveEffectChunkSkipMatchedSlotCount;
        public int ActiveEffectChunkSkipSkippedSlotCount;
        public int ActiveEffectChunkSkipDuePeriodSlotCount;
        public int ActiveEffectChunkSkipNoopSlotCount;
        public int ActiveEffectChunkSkipOwnerCount;
        public int ActiveEffectGlobalIndexOwnerCount;
        public int ActiveEffectGlobalIndexCount;
        public int ActiveEffectGlobalIndexActiveCount;
        public int ActiveEffectGlobalIndexInhibitedCount;
        public int ActiveEffectGlobalIndexPendingRemoveCount;
        public int ActiveEffectGlobalIndexPeriodDueCount;
        public int ActiveEffectGlobalIndexDurationDueCount;
        public int ActiveEffectGlobalIndexStaleCount;
        public int ActiveEffectGlobalIndexBucketOwnerCount;
        public int ActiveEffectGlobalIndexBucketIndexCount;
        public int ActiveEffectGlobalIndexMaxBucketLength;
        public int ActiveEffectGlobalIndexStableRowCount;
        public int ActiveEffectGlobalIndexStaleStableRowCount;
        public int ActiveMutationCommandCount;
        public int ActiveMutationOwnerGroupCount;
        public int ActiveMutationMaxOwnerRange;
        public int ActiveMutationSortMoveCount;
        public int ActiveMutationEstimatedRandomLookupCount;
        public int ActiveMutationOwnerResourceLookupCount;
        public int ActiveMutationMigrationCarrierCount;
        public int PendingAttributeDeltaCount;
        public int PendingAttributeAppliedDeltaCount;
        public int PendingAttributeSkippedDeltaCount;
        public int PendingAttributeTargetGroupCount;
        public int PendingAttributeMaxTargetRange;
        public int PendingAttributeEstimatedRandomLookupCount;
        public int PendingAttributeFactPatchCount;
        public int PendingAttributeMigrationCarrierCount;
        public int QueryBudget;
        public int FilteredQueryBudget;
        public int UnfilteredQueryBudget;
        public int LookupUpdateBudget;
        public int RandomLookupBudget;
        public int SyncQueryBudget;
        public int HelperTempQueryRiskCount;
        public int DependencyWaitRiskCount;
        public int WorldUpdateAllocatorOwnerCount;
        public int RewindableAllocatorCandidateCount;
        public int FrameBackbonePhaseCount;
        public int FrameBackboneContractOnlyPhaseCount;
        public int FrameBackboneStreamCount;
        public int FrameBackboneMigrationCarrierCount;
        public int FrameBackboneNativeStreamCandidateCount;
        public int FrameBackboneOwnerLocalBufferCandidateCount;
        public int FrameBackboneBattleHashStreamCount;
        public int FrameBackboneDeterministicMergePolicyCount;
        public int FrameBackboneMergeCostMeasuredCount;
        public int FrameBackboneMergeCostMicroseconds;
        public int FrameBackboneRequiredStructuralPlaybackCount;
        public int FrameBackboneRecordedStructuralPlaybackCount;
        public int FrameBackboneEcbCommandCount;
        public int FrameBackboneBulkQueryCount;
        public int FrameBackboneProfilerMarkerCount;
        public int FrameBackboneJournalingMarkerCount;
        public int FrameBackboneCoreCostGroupCount;
        public int FrameBackbonePhysicsCostGroupCount;
        public int FrameBackboneRenderCostGroupCount;
        public int FrameBackboneRunnerCostGroupCount;
        public int FrameBackbonePhysicsDisabledReasonCount;
        public int FrameBackboneRenderDisabledReasonCount;
        public int FrameBackboneDebuggerOverheadBudgetMicroseconds;
        public int FrameBackboneSamplingInterval;
        public int FrameBackboneDisablePolicyCount;
        public int FrameBackboneBurstWarmupPolicyCount;
        public int FrameBackboneHotPathManagedStringCount;
        public int FrameBackboneEvidenceMask;
        public int ObservationMaterializedQueryCount;
        public int ObservationMaterializedEntityCount;
        public int ObservationMaterializationElapsedMicroseconds;
        public int ObservationActiveEffectStoreQueryCount;
        public int ObservationActiveEffectStoreEntityCount;
        public int ObservationPresentationOutboxQueryCount;
        public int ObservationPresentationOutboxEntityCount;
        public int ObservationPerformancePollutionRiskCount;
        public float Ratio;
    }

    public readonly struct GasRuntimeDiagnosticStats
    {
        public readonly int FirstRetainedSequence;
        public readonly int NextSequence;
        public readonly int DroppedEventCount;
        public readonly int RetainedEventCount;
        public readonly int WarningCount;
        public readonly int ErrorCount;
        public readonly int SlowSystemCount;
        public readonly int BufferPressureWarningCount;

        public GasRuntimeDiagnosticStats(
            int firstRetainedSequence,
            int nextSequence,
            int droppedEventCount,
            int retainedEventCount,
            int warningCount,
            int errorCount,
            int slowSystemCount,
            int bufferPressureWarningCount)
        {
            FirstRetainedSequence = firstRetainedSequence;
            NextSequence = nextSequence;
            DroppedEventCount = droppedEventCount;
            RetainedEventCount = retainedEventCount;
            WarningCount = warningCount;
            ErrorCount = errorCount;
            SlowSystemCount = slowSystemCount;
            BufferPressureWarningCount = bufferPressureWarningCount;
        }
    }

    public readonly struct GasRuntimeObservationMaterializationCounters
    {
        public readonly int MaterializedQueryCount;
        public readonly int MaterializedEntityCount;
        public readonly int ElapsedMicroseconds;
        public readonly int ActiveEffectStoreQueryCount;
        public readonly int ActiveEffectStoreEntityCount;
        public readonly int PresentationOutboxQueryCount;
        public readonly int PresentationOutboxEntityCount;
        public readonly int PerformancePollutionRiskCount;

        public static GasRuntimeObservationMaterializationCounters Empty => default;

        public GasRuntimeObservationMaterializationCounters(
            int materializedQueryCount,
            int materializedEntityCount,
            int elapsedMicroseconds,
            int activeEffectStoreQueryCount,
            int activeEffectStoreEntityCount,
            int presentationOutboxQueryCount,
            int presentationOutboxEntityCount,
            int performancePollutionRiskCount)
        {
            MaterializedQueryCount = materializedQueryCount;
            MaterializedEntityCount = materializedEntityCount;
            ElapsedMicroseconds = elapsedMicroseconds;
            ActiveEffectStoreQueryCount = activeEffectStoreQueryCount;
            ActiveEffectStoreEntityCount = activeEffectStoreEntityCount;
            PresentationOutboxQueryCount = presentationOutboxQueryCount;
            PresentationOutboxEntityCount = presentationOutboxEntityCount;
            PerformancePollutionRiskCount = performancePollutionRiskCount;
        }

        public bool HasMaterialization => MaterializedQueryCount > 0 || MaterializedEntityCount > 0 || ElapsedMicroseconds > 0;

        public GasRuntimeObservationMaterializationCounters AddActiveEffectStoreMaterialization(
            int entityCount,
            int elapsedMicroseconds)
        {
            return Add(
                activeEffectStoreQueries: 1,
                activeEffectStoreEntities: entityCount,
                presentationOutboxQueries: 0,
                presentationOutboxEntities: 0,
                elapsedMicroseconds: elapsedMicroseconds);
        }

        public GasRuntimeObservationMaterializationCounters AddPresentationOutboxMaterialization(
            int entityCount,
            int elapsedMicroseconds)
        {
            return Add(
                activeEffectStoreQueries: 0,
                activeEffectStoreEntities: 0,
                presentationOutboxQueries: 1,
                presentationOutboxEntities: entityCount,
                elapsedMicroseconds: elapsedMicroseconds);
        }

        public GasRuntimeObservationMaterializationCounters Add(
            in GasRuntimeObservationMaterializationCounters other)
        {
            return new GasRuntimeObservationMaterializationCounters(
                MaterializedQueryCount + other.MaterializedQueryCount,
                MaterializedEntityCount + other.MaterializedEntityCount,
                ElapsedMicroseconds + other.ElapsedMicroseconds,
                ActiveEffectStoreQueryCount + other.ActiveEffectStoreQueryCount,
                ActiveEffectStoreEntityCount + other.ActiveEffectStoreEntityCount,
                PresentationOutboxQueryCount + other.PresentationOutboxQueryCount,
                PresentationOutboxEntityCount + other.PresentationOutboxEntityCount,
                PerformancePollutionRiskCount + other.PerformancePollutionRiskCount);
        }

        private GasRuntimeObservationMaterializationCounters Add(
            int activeEffectStoreQueries,
            int activeEffectStoreEntities,
            int presentationOutboxQueries,
            int presentationOutboxEntities,
            int elapsedMicroseconds)
        {
            var queryCount = activeEffectStoreQueries + presentationOutboxQueries;
            var entityCount = activeEffectStoreEntities + presentationOutboxEntities;
            return new GasRuntimeObservationMaterializationCounters(
                MaterializedQueryCount + queryCount,
                MaterializedEntityCount + entityCount,
                ElapsedMicroseconds + elapsedMicroseconds,
                ActiveEffectStoreQueryCount + activeEffectStoreQueries,
                ActiveEffectStoreEntityCount + activeEffectStoreEntities,
                PresentationOutboxQueryCount + presentationOutboxQueries,
                PresentationOutboxEntityCount + presentationOutboxEntities,
                PerformancePollutionRiskCount + (queryCount > 0 ? 1 : 0));
        }
    }

    public readonly struct GasRuntimeCoreDiagnosticCounters
    {
        public readonly int RequestCount;
        public readonly int SpecCount;
        public readonly int DeltaCount;
        public readonly int FactCount;
        public readonly int CueCount;
        public readonly int PresentationCount;
        public readonly int EntityCreateCount;
        public readonly int EntityDestroyCount;
        public readonly int EcbPlaybackCount;
        public readonly int PeakActiveEffectEntityCount;
        public readonly int PeakPendingApplyCommandOwnerCount;
        public readonly int PeakEventBusBufferLength;
        public readonly int PeakPresentationCursorLag;
        public readonly int PeakReplayCursorLag;
        public readonly int ActiveEffectStoreOwnerCount;
        public readonly int ActiveEffectSlotCount;
        public readonly int ActiveEffectSlotCapacity;
        public readonly int ActiveEffectSlotPendingApplyCount;
        public readonly int ActiveEffectSlotActiveCount;
        public readonly int ActiveEffectSlotInhibitedCount;
        public readonly int ActiveEffectSlotPendingRemoveCount;
        public readonly int ActiveEffectSlotLegacyBackedCount;
        public readonly int ActiveEffectSlotExternalizedOwnerCount;
        public readonly int ActiveEffectSlotGrantedTagCount;
        public readonly int ActiveEffectSlotGrantedAbilityCount;
        public readonly int ActiveEffectChunkSkipMatchedSlotCount;
        public readonly int ActiveEffectChunkSkipSkippedSlotCount;
        public readonly int ActiveEffectChunkSkipDuePeriodSlotCount;
        public readonly int ActiveEffectChunkSkipNoopSlotCount;
        public readonly int ActiveEffectChunkSkipOwnerCount;
        public readonly int ActiveEffectGlobalIndexOwnerCount;
        public readonly int ActiveEffectGlobalIndexCount;
        public readonly int ActiveEffectGlobalIndexActiveCount;
        public readonly int ActiveEffectGlobalIndexInhibitedCount;
        public readonly int ActiveEffectGlobalIndexPendingRemoveCount;
        public readonly int ActiveEffectGlobalIndexPeriodDueCount;
        public readonly int ActiveEffectGlobalIndexDurationDueCount;
        public readonly int ActiveEffectGlobalIndexStaleCount;
        public readonly int ActiveEffectGlobalIndexBucketOwnerCount;
        public readonly int ActiveEffectGlobalIndexBucketIndexCount;
        public readonly int ActiveEffectGlobalIndexMaxBucketLength;
        public readonly int ActiveEffectGlobalIndexStableRowCount;
        public readonly int ActiveEffectGlobalIndexStaleStableRowCount;
        public readonly int ActiveMutationCommandCount;
        public readonly int ActiveMutationOwnerGroupCount;
        public readonly int ActiveMutationMaxOwnerRange;
        public readonly int ActiveMutationSortMoveCount;
        public readonly int ActiveMutationEstimatedRandomLookupCount;
        public readonly int ActiveMutationOwnerResourceLookupCount;
        public readonly int ActiveMutationMigrationCarrierCount;
        public readonly int PendingAttributeDeltaCount;
        public readonly int PendingAttributeAppliedDeltaCount;
        public readonly int PendingAttributeSkippedDeltaCount;
        public readonly int PendingAttributeTargetGroupCount;
        public readonly int PendingAttributeMaxTargetRange;
        public readonly int PendingAttributeEstimatedRandomLookupCount;
        public readonly int PendingAttributeFactPatchCount;
        public readonly int PendingAttributeMigrationCarrierCount;
        public readonly int QueryBudget;
        public readonly int FilteredQueryBudget;
        public readonly int UnfilteredQueryBudget;
        public readonly int LookupUpdateBudget;
        public readonly int RandomLookupBudget;
        public readonly int SyncQueryBudget;
        public readonly int HelperTempQueryRiskCount;
        public readonly int DependencyWaitRiskCount;
        public readonly int WorldUpdateAllocatorOwnerCount;
        public readonly int RewindableAllocatorCandidateCount;

        public GasRuntimeCoreDiagnosticCounters(
            int requestCount,
            int specCount,
            int deltaCount,
            int factCount,
            int cueCount,
            int presentationCount,
            int entityCreateCount,
            int entityDestroyCount,
            int ecbPlaybackCount,
            int peakActiveEffectEntityCount,
            int peakPendingApplyCommandOwnerCount,
            int peakEventBusBufferLength,
            int peakPresentationCursorLag,
            int peakReplayCursorLag,
            int activeEffectStoreOwnerCount = 0,
            int activeEffectSlotCount = 0,
            int activeEffectSlotCapacity = 0,
            int activeEffectSlotPendingApplyCount = 0,
            int activeEffectSlotActiveCount = 0,
            int activeEffectSlotInhibitedCount = 0,
            int activeEffectSlotPendingRemoveCount = 0,
            int activeEffectSlotLegacyBackedCount = 0,
            int activeEffectSlotExternalizedOwnerCount = 0,
            int activeEffectSlotGrantedTagCount = 0,
            int activeEffectSlotGrantedAbilityCount = 0,
            int activeEffectChunkSkipMatchedSlotCount = 0,
            int activeEffectChunkSkipSkippedSlotCount = 0,
            int activeEffectChunkSkipDuePeriodSlotCount = 0,
            int activeEffectChunkSkipNoopSlotCount = 0,
            int activeEffectChunkSkipOwnerCount = 0,
            int activeEffectGlobalIndexOwnerCount = 0,
            int activeEffectGlobalIndexCount = 0,
            int activeEffectGlobalIndexActiveCount = 0,
            int activeEffectGlobalIndexInhibitedCount = 0,
            int activeEffectGlobalIndexPendingRemoveCount = 0,
            int activeEffectGlobalIndexPeriodDueCount = 0,
            int activeEffectGlobalIndexDurationDueCount = 0,
            int activeEffectGlobalIndexStaleCount = 0,
            int activeEffectGlobalIndexBucketOwnerCount = 0,
            int activeEffectGlobalIndexBucketIndexCount = 0,
            int activeEffectGlobalIndexMaxBucketLength = 0,
            int activeEffectGlobalIndexStableRowCount = 0,
            int activeEffectGlobalIndexStaleStableRowCount = 0,
            int activeMutationCommandCount = 0,
            int activeMutationOwnerGroupCount = 0,
            int activeMutationMaxOwnerRange = 0,
            int activeMutationSortMoveCount = 0,
            int activeMutationEstimatedRandomLookupCount = 0,
            int activeMutationOwnerResourceLookupCount = 0,
            int activeMutationMigrationCarrierCount = 0,
            int pendingAttributeDeltaCount = 0,
            int pendingAttributeAppliedDeltaCount = 0,
            int pendingAttributeSkippedDeltaCount = 0,
            int pendingAttributeTargetGroupCount = 0,
            int pendingAttributeMaxTargetRange = 0,
            int pendingAttributeEstimatedRandomLookupCount = 0,
            int pendingAttributeFactPatchCount = 0,
            int pendingAttributeMigrationCarrierCount = 0,
            int queryBudget = 0,
            int filteredQueryBudget = 0,
            int unfilteredQueryBudget = 0,
            int lookupUpdateBudget = 0,
            int randomLookupBudget = 0,
            int syncQueryBudget = 0,
            int helperTempQueryRiskCount = 0,
            int dependencyWaitRiskCount = 0,
            int worldUpdateAllocatorOwnerCount = 0,
            int rewindableAllocatorCandidateCount = 0)
        {
            RequestCount = requestCount;
            SpecCount = specCount;
            DeltaCount = deltaCount;
            FactCount = factCount;
            CueCount = cueCount;
            PresentationCount = presentationCount;
            EntityCreateCount = entityCreateCount;
            EntityDestroyCount = entityDestroyCount;
            EcbPlaybackCount = ecbPlaybackCount;
            PeakActiveEffectEntityCount = peakActiveEffectEntityCount;
            PeakPendingApplyCommandOwnerCount = peakPendingApplyCommandOwnerCount;
            PeakEventBusBufferLength = peakEventBusBufferLength;
            PeakPresentationCursorLag = peakPresentationCursorLag;
            PeakReplayCursorLag = peakReplayCursorLag;
            ActiveEffectStoreOwnerCount = activeEffectStoreOwnerCount;
            ActiveEffectSlotCount = activeEffectSlotCount;
            ActiveEffectSlotCapacity = activeEffectSlotCapacity;
            ActiveEffectSlotPendingApplyCount = activeEffectSlotPendingApplyCount;
            ActiveEffectSlotActiveCount = activeEffectSlotActiveCount;
            ActiveEffectSlotInhibitedCount = activeEffectSlotInhibitedCount;
            ActiveEffectSlotPendingRemoveCount = activeEffectSlotPendingRemoveCount;
            ActiveEffectSlotLegacyBackedCount = activeEffectSlotLegacyBackedCount;
            ActiveEffectSlotExternalizedOwnerCount = activeEffectSlotExternalizedOwnerCount;
            ActiveEffectSlotGrantedTagCount = activeEffectSlotGrantedTagCount;
            ActiveEffectSlotGrantedAbilityCount = activeEffectSlotGrantedAbilityCount;
            ActiveEffectChunkSkipMatchedSlotCount = activeEffectChunkSkipMatchedSlotCount;
            ActiveEffectChunkSkipSkippedSlotCount = activeEffectChunkSkipSkippedSlotCount;
            ActiveEffectChunkSkipDuePeriodSlotCount = activeEffectChunkSkipDuePeriodSlotCount;
            ActiveEffectChunkSkipNoopSlotCount = activeEffectChunkSkipNoopSlotCount;
            ActiveEffectChunkSkipOwnerCount = activeEffectChunkSkipOwnerCount;
            ActiveEffectGlobalIndexOwnerCount = activeEffectGlobalIndexOwnerCount;
            ActiveEffectGlobalIndexCount = activeEffectGlobalIndexCount;
            ActiveEffectGlobalIndexActiveCount = activeEffectGlobalIndexActiveCount;
            ActiveEffectGlobalIndexInhibitedCount = activeEffectGlobalIndexInhibitedCount;
            ActiveEffectGlobalIndexPendingRemoveCount = activeEffectGlobalIndexPendingRemoveCount;
            ActiveEffectGlobalIndexPeriodDueCount = activeEffectGlobalIndexPeriodDueCount;
            ActiveEffectGlobalIndexDurationDueCount = activeEffectGlobalIndexDurationDueCount;
            ActiveEffectGlobalIndexStaleCount = activeEffectGlobalIndexStaleCount;
            ActiveEffectGlobalIndexBucketOwnerCount = activeEffectGlobalIndexBucketOwnerCount;
            ActiveEffectGlobalIndexBucketIndexCount = activeEffectGlobalIndexBucketIndexCount;
            ActiveEffectGlobalIndexMaxBucketLength = activeEffectGlobalIndexMaxBucketLength;
            ActiveEffectGlobalIndexStableRowCount = activeEffectGlobalIndexStableRowCount;
            ActiveEffectGlobalIndexStaleStableRowCount = activeEffectGlobalIndexStaleStableRowCount;
            ActiveMutationCommandCount = activeMutationCommandCount;
            ActiveMutationOwnerGroupCount = activeMutationOwnerGroupCount;
            ActiveMutationMaxOwnerRange = activeMutationMaxOwnerRange;
            ActiveMutationSortMoveCount = activeMutationSortMoveCount;
            ActiveMutationEstimatedRandomLookupCount = activeMutationEstimatedRandomLookupCount;
            ActiveMutationOwnerResourceLookupCount = activeMutationOwnerResourceLookupCount;
            ActiveMutationMigrationCarrierCount = activeMutationMigrationCarrierCount;
            PendingAttributeDeltaCount = pendingAttributeDeltaCount;
            PendingAttributeAppliedDeltaCount = pendingAttributeAppliedDeltaCount;
            PendingAttributeSkippedDeltaCount = pendingAttributeSkippedDeltaCount;
            PendingAttributeTargetGroupCount = pendingAttributeTargetGroupCount;
            PendingAttributeMaxTargetRange = pendingAttributeMaxTargetRange;
            PendingAttributeEstimatedRandomLookupCount = pendingAttributeEstimatedRandomLookupCount;
            PendingAttributeFactPatchCount = pendingAttributeFactPatchCount;
            PendingAttributeMigrationCarrierCount = pendingAttributeMigrationCarrierCount;
            QueryBudget = queryBudget;
            FilteredQueryBudget = filteredQueryBudget;
            UnfilteredQueryBudget = unfilteredQueryBudget;
            LookupUpdateBudget = lookupUpdateBudget;
            RandomLookupBudget = randomLookupBudget;
            SyncQueryBudget = syncQueryBudget;
            HelperTempQueryRiskCount = helperTempQueryRiskCount;
            DependencyWaitRiskCount = dependencyWaitRiskCount;
            WorldUpdateAllocatorOwnerCount = worldUpdateAllocatorOwnerCount;
            RewindableAllocatorCandidateCount = rewindableAllocatorCandidateCount;
        }
    }

    public struct GasRuntimeCoreCounterQueries : IDisposable
    {
        private readonly bool _ownsQueries;
        private readonly bool _isCreated;

        public GasRuntimeCoreCounterQueries(
            EntityQuery ascCommandPendingOwners,
            EntityQuery gameplayEffectRemovePendingOwners,
            EntityQuery effectSpecs,
            EntityQuery activeEffectStores,
            EntityQuery presentationOutboxes,
            bool ownsQueries)
        {
            AscCommandPendingOwners = ascCommandPendingOwners;
            GameplayEffectRemovePendingOwners = gameplayEffectRemovePendingOwners;
            EffectSpecs = effectSpecs;
            ActiveEffectStores = activeEffectStores;
            PresentationOutboxes = presentationOutboxes;
            _ownsQueries = ownsQueries;
            _isCreated = true;
        }

        public EntityQuery AscCommandPendingOwners { get; }

        public EntityQuery GameplayEffectRemovePendingOwners { get; }

        public EntityQuery EffectSpecs { get; }

        public EntityQuery ActiveEffectStores { get; }

        public EntityQuery PresentationOutboxes { get; }

        public static GasRuntimeCoreCounterQueries CreateOwned(EntityManager em)
        {
            return default;
        }

        public static GasRuntimeCoreCounterQueries Create(ref SystemState state)
        {
            return new GasRuntimeCoreCounterQueries(
                CreateSystemQuery<ASCCommandPendingComponent>(ref state),
                CreateSystemQuery<GERemoveCommandPendingComponent>(ref state),
                CreateSystemQuery<GEEffectSpecComponent>(ref state),
                state.GetEntityQuery(new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<ASCActiveEffectsComponent>(),
                        ComponentType.ReadOnly<ActiveGameplayEffectBuffer>(),
                    },
                }),
                CreateSystemQuery<PresentationEventBuffer>(ref state),
                ownsQueries: false);
        }

        public int CountPendingCommandOwners()
        {
            return Count(AscCommandPendingOwners)
                   + Count(GameplayEffectRemovePendingOwners);
        }

        public int CountActiveEffectEntities()
        {
            return Count(EffectSpecs);
        }

        public int CountActiveEffectStoreOwners()
        {
            return Count(ActiveEffectStores);
        }

        public int CountPendingApplyCommandOwners()
        {
            return 0;
        }

        public void Dispose()
        {
            if (!_ownsQueries)
                return;

            AscCommandPendingOwners.Dispose();
            GameplayEffectRemovePendingOwners.Dispose();
            EffectSpecs.Dispose();
            ActiveEffectStores.Dispose();
            PresentationOutboxes.Dispose();
        }

        private static EntityQuery CreateSystemQuery<T>(ref SystemState state)
        {
            return state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<T>(),
                },
            });
        }

        private int Count(EntityQuery query)
        {
            return _isCreated ? query.CalculateEntityCount() : 0;
        }
    }

    public readonly struct GasRuntimeFrameBackboneDiagnosticCounters
    {
        public readonly int PhaseCount;
        public readonly int ContractOnlyPhaseCount;
        public readonly int QueryBudget;
        public readonly int FilteredQueryBudget;
        public readonly int UnfilteredQueryBudget;
        public readonly int LookupUpdateBudget;
        public readonly int RandomLookupBudget;
        public readonly int SyncQueryBudget;
        public readonly int DependencyWaitRiskCount;
        public readonly int WorldUpdateAllocatorOwnerCount;
        public readonly int RewindableAllocatorCandidateCount;
        public readonly int StreamCount;
        public readonly int MigrationCarrierCount;
        public readonly int NativeStreamCandidateCount;
        public readonly int OwnerLocalBufferCandidateCount;
        public readonly int BattleHashStreamCount;
        public readonly int DeterministicMergePolicyCount;
        public readonly int MergeCostMeasuredCount;
        public readonly int MergeCostMicroseconds;
        public readonly int RequiredStructuralPlaybackCount;
        public readonly int RecordedStructuralPlaybackCount;
        public readonly int EcbCommandCount;
        public readonly int BulkQueryCount;
        public readonly int ProfilerMarkerCount;
        public readonly int JournalingMarkerCount;
        public readonly int CoreCostGroupCount;
        public readonly int PhysicsCostGroupCount;
        public readonly int RenderCostGroupCount;
        public readonly int RunnerCostGroupCount;
        public readonly int PhysicsDisabledReasonCount;
        public readonly int RenderDisabledReasonCount;
        public readonly int DebuggerOverheadBudgetMicroseconds;
        public readonly int SamplingInterval;
        public readonly int DisablePolicyCount;
        public readonly int BurstWarmupPolicyCount;
        public readonly int HotPathManagedStringCount;
        public readonly int EvidenceMask;

        public static GasRuntimeFrameBackboneDiagnosticCounters Empty => default;

        public GasRuntimeFrameBackboneDiagnosticCounters(
            int phaseCount,
            int contractOnlyPhaseCount,
            int queryBudget,
            int filteredQueryBudget,
            int unfilteredQueryBudget,
            int lookupUpdateBudget,
            int randomLookupBudget,
            int syncQueryBudget,
            int dependencyWaitRiskCount,
            int worldUpdateAllocatorOwnerCount,
            int rewindableAllocatorCandidateCount,
            int streamCount,
            int migrationCarrierCount,
            int nativeStreamCandidateCount,
            int ownerLocalBufferCandidateCount,
            int battleHashStreamCount,
            int deterministicMergePolicyCount,
            int mergeCostMeasuredCount,
            int mergeCostMicroseconds,
            int requiredStructuralPlaybackCount,
            int recordedStructuralPlaybackCount,
            int ecbCommandCount,
            int bulkQueryCount,
            int profilerMarkerCount,
            int journalingMarkerCount,
            int coreCostGroupCount,
            int physicsCostGroupCount,
            int renderCostGroupCount,
            int runnerCostGroupCount,
            int physicsDisabledReasonCount,
            int renderDisabledReasonCount,
            int debuggerOverheadBudgetMicroseconds,
            int samplingInterval,
            int disablePolicyCount,
            int burstWarmupPolicyCount,
            int hotPathManagedStringCount,
            int evidenceMask)
        {
            PhaseCount = phaseCount;
            ContractOnlyPhaseCount = contractOnlyPhaseCount;
            QueryBudget = queryBudget;
            FilteredQueryBudget = filteredQueryBudget;
            UnfilteredQueryBudget = unfilteredQueryBudget;
            LookupUpdateBudget = lookupUpdateBudget;
            RandomLookupBudget = randomLookupBudget;
            SyncQueryBudget = syncQueryBudget;
            DependencyWaitRiskCount = dependencyWaitRiskCount;
            WorldUpdateAllocatorOwnerCount = worldUpdateAllocatorOwnerCount;
            RewindableAllocatorCandidateCount = rewindableAllocatorCandidateCount;
            StreamCount = streamCount;
            MigrationCarrierCount = migrationCarrierCount;
            NativeStreamCandidateCount = nativeStreamCandidateCount;
            OwnerLocalBufferCandidateCount = ownerLocalBufferCandidateCount;
            BattleHashStreamCount = battleHashStreamCount;
            DeterministicMergePolicyCount = deterministicMergePolicyCount;
            MergeCostMeasuredCount = mergeCostMeasuredCount;
            MergeCostMicroseconds = mergeCostMicroseconds;
            RequiredStructuralPlaybackCount = requiredStructuralPlaybackCount;
            RecordedStructuralPlaybackCount = recordedStructuralPlaybackCount;
            EcbCommandCount = ecbCommandCount;
            BulkQueryCount = bulkQueryCount;
            ProfilerMarkerCount = profilerMarkerCount;
            JournalingMarkerCount = journalingMarkerCount;
            CoreCostGroupCount = coreCostGroupCount;
            PhysicsCostGroupCount = physicsCostGroupCount;
            RenderCostGroupCount = renderCostGroupCount;
            RunnerCostGroupCount = runnerCostGroupCount;
            PhysicsDisabledReasonCount = physicsDisabledReasonCount;
            RenderDisabledReasonCount = renderDisabledReasonCount;
            DebuggerOverheadBudgetMicroseconds = debuggerOverheadBudgetMicroseconds;
            SamplingInterval = samplingInterval;
            DisablePolicyCount = disablePolicyCount;
            BurstWarmupPolicyCount = burstWarmupPolicyCount;
            HotPathManagedStringCount = hotPathManagedStringCount;
            EvidenceMask = evidenceMask;
        }

        public GasRuntimeFrameBackboneDiagnosticCounters(
            in GASRuntimeDebuggerEvidenceGatePlan plan,
            int recordedStructuralPlaybackCount = 0,
            int ecbCommandCount = 0,
            int bulkQueryCount = 0)
        {
            PhaseCount = plan.PhaseCount;
            ContractOnlyPhaseCount = plan.ContractOnlyPhaseCount;
            QueryBudget = plan.QueryBudget;
            FilteredQueryBudget = plan.FilteredQueryBudget;
            UnfilteredQueryBudget = plan.UnfilteredQueryBudget;
            LookupUpdateBudget = plan.LookupUpdateBudget;
            RandomLookupBudget = plan.RandomLookupBudget;
            SyncQueryBudget = plan.SyncQueryBudget;
            DependencyWaitRiskCount = plan.DependencyWaitRiskCount;
            WorldUpdateAllocatorOwnerCount = plan.WorldUpdateAllocatorOwnerCount;
            RewindableAllocatorCandidateCount = plan.RewindableAllocatorCandidateCount;
            StreamCount = plan.StreamCount;
            MigrationCarrierCount = plan.MigrationCarrierCount;
            NativeStreamCandidateCount = plan.NativeStreamCandidateCount;
            OwnerLocalBufferCandidateCount = plan.OwnerLocalBufferCandidateCount;
            BattleHashStreamCount = plan.BattleHashStreamCount;
            DeterministicMergePolicyCount = plan.DeterministicMergePolicyCount;
            MergeCostMeasuredCount = plan.MergeCostMeasuredCount;
            MergeCostMicroseconds = plan.MergeCostMicroseconds;
            RequiredStructuralPlaybackCount = plan.RequiredStructuralPlaybackCount;
            RecordedStructuralPlaybackCount = recordedStructuralPlaybackCount;
            EcbCommandCount = ecbCommandCount;
            BulkQueryCount = bulkQueryCount;
            ProfilerMarkerCount = plan.ProfilerMarkerCount;
            JournalingMarkerCount = plan.JournalingMarkerCount;
            CoreCostGroupCount = plan.CoreCostGroupCount;
            PhysicsCostGroupCount = plan.PhysicsCostGroupCount;
            RenderCostGroupCount = plan.RenderCostGroupCount;
            RunnerCostGroupCount = plan.RunnerCostGroupCount;
            PhysicsDisabledReasonCount = plan.PhysicsDisabledReasonCount;
            RenderDisabledReasonCount = plan.RenderDisabledReasonCount;
            DebuggerOverheadBudgetMicroseconds = plan.Gate.OverheadBudgetMicroseconds;
            SamplingInterval = plan.Gate.SamplingInterval;
            DisablePolicyCount = plan.DisablePolicyCount;
            BurstWarmupPolicyCount = plan.BurstWarmupPolicyCount;
            HotPathManagedStringCount = plan.HotPathManagedStringCount;
            EvidenceMask = (int)plan.Gate.Coverage;
        }
    }

    public readonly struct GasRuntimeDiagnosticSnapshot
    {
        public readonly GasRuntimeDiagnosticStats Stats;
        public readonly GasRuntimeCoreDiagnosticCounters CoreCounters;
        public readonly GasRuntimeFrameBackboneDiagnosticCounters FrameBackboneCounters;
        public readonly GasRuntimeObservationMaterializationCounters ObservationMaterializationCounters;
        public readonly GASRuntimeDiagnosticEventBuffer[] Events;

        public GasRuntimeDiagnosticSnapshot(
            in GasRuntimeDiagnosticStats stats,
            in GasRuntimeCoreDiagnosticCounters coreCounters,
            GASRuntimeDiagnosticEventBuffer[] events)
            : this(
                stats,
                coreCounters,
                GasRuntimeFrameBackboneDiagnosticCounters.Empty,
                GasRuntimeObservationMaterializationCounters.Empty,
                events)
        {
        }

        public GasRuntimeDiagnosticSnapshot(
            in GasRuntimeDiagnosticStats stats,
            in GasRuntimeCoreDiagnosticCounters coreCounters,
            in GasRuntimeFrameBackboneDiagnosticCounters frameBackboneCounters,
            GASRuntimeDiagnosticEventBuffer[] events)
            : this(
                stats,
                coreCounters,
                frameBackboneCounters,
                GasRuntimeObservationMaterializationCounters.Empty,
                events)
        {
        }

        public GasRuntimeDiagnosticSnapshot(
            in GasRuntimeDiagnosticStats stats,
            in GasRuntimeCoreDiagnosticCounters coreCounters,
            in GasRuntimeFrameBackboneDiagnosticCounters frameBackboneCounters,
            in GasRuntimeObservationMaterializationCounters observationMaterializationCounters,
            GASRuntimeDiagnosticEventBuffer[] events)
        {
            Stats = stats;
            CoreCounters = coreCounters;
            FrameBackboneCounters = frameBackboneCounters;
            ObservationMaterializationCounters = observationMaterializationCounters;
            Events = events ?? Array.Empty<GASRuntimeDiagnosticEventBuffer>();
        }

        public int EventCount => Events?.Length ?? 0;
    }

    public static class GasRuntimeDebugger
    {
        public const int DefaultDiagnosticCapacity = 4096;
        private const int DefaultSlowTickMicroseconds = 1000;
        private const int DefaultSlowSystemMicroseconds = 100;
        private const int DefaultBufferPressureWarningPermille = 700;
        private const int DefaultBufferPressureErrorPermille = 900;
        private static EntityManager _cachedDebuggerEntityManager;
        private static Entity _cachedDebuggerEntity;
        private static bool _hasCachedDebugger;

        public static Entity CreateSingleton(EntityManager em)
        {
            var entity = em.CreateEntity(GASRuntimeEntityArchetypes.RuntimeDebugger(em));
            em.SetComponentData(entity, CreateDefaultState());
            em.GetBuffer<GASRuntimeDiagnosticEventBuffer>(entity).EnsureCapacity(DefaultDiagnosticCapacity);
            em.SetName(entity, "GasRuntimeDebugger");
            RegisterKnownSingleton(em, entity);
            return entity;
        }

        public static void RegisterKnownSingleton(EntityManager em, Entity debuggerEntity)
        {
            if (!IsValidDebugger(em, debuggerEntity))
                return;

            _cachedDebuggerEntityManager = em;
            _cachedDebuggerEntity = debuggerEntity;
            _hasCachedDebugger = true;
        }

        public static void ResetKnownSingleton(EntityManager em)
        {
            if (_hasCachedDebugger && _cachedDebuggerEntityManager.Equals(em))
            {
                _hasCachedDebugger = false;
                _cachedDebuggerEntity = Entity.Null;
                _cachedDebuggerEntityManager = default;
            }
        }

        public static bool TryGetSingleton(EntityManager em, out Entity debuggerEntity)
        {
            if (TryResolveCachedSingleton(em, out debuggerEntity))
                return true;

            return TryResolveRegisteredSingleton(em, out debuggerEntity);
        }

        private static bool TryResolveCachedSingleton(EntityManager em, out Entity debuggerEntity)
        {
            if (!_hasCachedDebugger || !_cachedDebuggerEntityManager.Equals(em))
            {
                debuggerEntity = Entity.Null;
                return false;
            }

            if (IsValidDebugger(em, _cachedDebuggerEntity))
            {
                debuggerEntity = _cachedDebuggerEntity;
                return true;
            }

            _hasCachedDebugger = false;
            _cachedDebuggerEntity = Entity.Null;
            debuggerEntity = Entity.Null;
            return false;
        }

        private static bool TryResolveRegisteredSingleton(EntityManager em, out Entity debuggerEntity)
        {
            debuggerEntity = Entity.Null;
            if (!GASManager.IsInitialized || !GASManager.EntityManager.Equals(em))
                return false;

            var registeredDebugger = GASManager.EntityRuntimeDebugger;
            if (!IsValidDebugger(em, registeredDebugger))
                return false;

            debuggerEntity = registeredDebugger;
            RegisterKnownSingleton(em, debuggerEntity);
            return true;
        }

        private static bool IsValidDebugger(EntityManager em, Entity debuggerEntity)
        {
            return debuggerEntity != Entity.Null
                   && em.World != null
                   && em.World.IsCreated
                   && em.Exists(debuggerEntity)
                   && em.HasComponent<GASRuntimeDebuggerComponent>(debuggerEntity);
        }

        public static GASRuntimeDebuggerComponent CreateDefaultState()
        {
            return new GASRuntimeDebuggerComponent
            {
                Enabled = 1,
                CaptureSystemTimings = 0,
                CaptureBufferPressure = 1,
                MaxRetainedEvents = DefaultDiagnosticCapacity,
                SlowTickMicroseconds = DefaultSlowTickMicroseconds,
                SlowSystemMicroseconds = DefaultSlowSystemMicroseconds,
                BufferPressureWarningPermille = DefaultBufferPressureWarningPermille,
                BufferPressureErrorPermille = DefaultBufferPressureErrorPermille,
            };
        }

        public static void Reset(EntityManager em, Entity debuggerEntity)
        {
            if (!CanUse(em, debuggerEntity))
                return;

            var state = em.GetComponentData<GASRuntimeDebuggerComponent>(debuggerEntity);
            state.NextSequence = 0;
            state.FirstRetainedSequence = 0;
            state.DroppedEventCount = 0;
            state.RuntimeCoreRequestCount = 0;
            state.RuntimeCoreSpecCount = 0;
            state.RuntimeCoreDeltaCount = 0;
            state.RuntimeCoreFactCount = 0;
            state.RuntimeCoreCueCount = 0;
            state.RuntimeCorePresentationCount = 0;
            state.RuntimeCoreEntityCreateCount = 0;
            state.RuntimeCoreEntityDestroyCount = 0;
            state.RuntimeCoreEcbPlaybackCount = 0;
            state.RuntimeCorePeakActiveEffectEntityCount = 0;
            state.RuntimeCorePeakPendingApplyCommandOwnerCount = 0;
            state.RuntimeCorePeakEventBusBufferLength = 0;
            state.RuntimeCorePeakPresentationCursorLag = 0;
            state.RuntimeCorePeakReplayCursorLag = 0;
            state.RuntimeCoreActiveEffectStoreOwnerCount = 0;
            state.RuntimeCoreActiveEffectSlotCount = 0;
            state.RuntimeCoreActiveEffectSlotCapacity = 0;
            state.RuntimeCoreActiveEffectSlotPendingApplyCount = 0;
            state.RuntimeCoreActiveEffectSlotActiveCount = 0;
            state.RuntimeCoreActiveEffectSlotInhibitedCount = 0;
            state.RuntimeCoreActiveEffectSlotPendingRemoveCount = 0;
            state.RuntimeCoreActiveEffectSlotLegacyBackedCount = 0;
            state.RuntimeCoreActiveEffectSlotExternalizedOwnerCount = 0;
            state.RuntimeCoreActiveEffectSlotGrantedTagCount = 0;
            state.RuntimeCoreActiveEffectSlotGrantedAbilityCount = 0;
            state.RuntimeCoreActiveEffectChunkSkipMatchedSlotCount = 0;
            state.RuntimeCoreActiveEffectChunkSkipSkippedSlotCount = 0;
            state.RuntimeCoreActiveEffectChunkSkipDuePeriodSlotCount = 0;
            state.RuntimeCoreActiveEffectChunkSkipNoopSlotCount = 0;
            state.RuntimeCoreActiveEffectChunkSkipOwnerCount = 0;
            state.RuntimeCoreActiveEffectGlobalIndexOwnerCount = 0;
            state.RuntimeCoreActiveEffectGlobalIndexCount = 0;
            state.RuntimeCoreActiveEffectGlobalIndexActiveCount = 0;
            state.RuntimeCoreActiveEffectGlobalIndexInhibitedCount = 0;
            state.RuntimeCoreActiveEffectGlobalIndexPendingRemoveCount = 0;
            state.RuntimeCoreActiveEffectGlobalIndexPeriodDueCount = 0;
            state.RuntimeCoreActiveEffectGlobalIndexDurationDueCount = 0;
            state.RuntimeCoreActiveEffectGlobalIndexStaleCount = 0;
            state.RuntimeCoreActiveEffectGlobalIndexBucketOwnerCount = 0;
            state.RuntimeCoreActiveEffectGlobalIndexBucketIndexCount = 0;
            state.RuntimeCoreActiveEffectGlobalIndexMaxBucketLength = 0;
            state.RuntimeCoreActiveEffectGlobalIndexStableRowCount = 0;
            state.RuntimeCoreActiveEffectGlobalIndexStaleStableRowCount = 0;
            state.RuntimeCoreActiveMutationCommandCount = 0;
            state.RuntimeCoreActiveMutationOwnerGroupCount = 0;
            state.RuntimeCoreActiveMutationMaxOwnerRange = 0;
            state.RuntimeCoreActiveMutationSortMoveCount = 0;
            state.RuntimeCoreActiveMutationEstimatedRandomLookupCount = 0;
            state.RuntimeCoreActiveMutationOwnerResourceLookupCount = 0;
            state.RuntimeCoreActiveMutationMigrationCarrierCount = 0;
            state.RuntimeCorePendingAttributeDeltaCount = 0;
            state.RuntimeCorePendingAttributeAppliedDeltaCount = 0;
            state.RuntimeCorePendingAttributeSkippedDeltaCount = 0;
            state.RuntimeCorePendingAttributeTargetGroupCount = 0;
            state.RuntimeCorePendingAttributeMaxTargetRange = 0;
            state.RuntimeCorePendingAttributeEstimatedRandomLookupCount = 0;
            state.RuntimeCorePendingAttributeFactPatchCount = 0;
            state.RuntimeCorePendingAttributeMigrationCarrierCount = 0;
            state.RuntimeCoreQueryBudget = 0;
            state.RuntimeCoreFilteredQueryBudget = 0;
            state.RuntimeCoreUnfilteredQueryBudget = 0;
            state.RuntimeCoreLookupUpdateBudget = 0;
            state.RuntimeCoreRandomLookupBudget = 0;
            state.RuntimeCoreSyncQueryBudget = 0;
            state.RuntimeCoreHelperTempQueryRiskCount = 0;
            state.RuntimeCoreDependencyWaitRiskCount = 0;
            state.RuntimeCoreWorldUpdateAllocatorOwnerCount = 0;
            state.RuntimeCoreRewindableAllocatorCandidateCount = 0;
            state.RuntimeCoreFrameBackbonePhaseCount = 0;
            state.RuntimeCoreFrameBackboneContractOnlyPhaseCount = 0;
            state.RuntimeCoreFrameBackboneStreamCount = 0;
            state.RuntimeCoreFrameBackboneMigrationCarrierCount = 0;
            state.RuntimeCoreFrameBackboneNativeStreamCandidateCount = 0;
            state.RuntimeCoreFrameBackboneOwnerLocalBufferCandidateCount = 0;
            state.RuntimeCoreFrameBackboneBattleHashStreamCount = 0;
            state.RuntimeCoreFrameBackboneDeterministicMergePolicyCount = 0;
            state.RuntimeCoreFrameBackboneMergeCostMeasuredCount = 0;
            state.RuntimeCoreFrameBackboneMergeCostMicroseconds = 0;
            state.RuntimeCoreFrameBackboneRequiredStructuralPlaybackCount = 0;
            state.RuntimeCoreFrameBackboneRecordedStructuralPlaybackCount = 0;
            state.RuntimeCoreFrameBackboneEcbCommandCount = 0;
            state.RuntimeCoreFrameBackboneBulkQueryCount = 0;
            state.RuntimeCoreFrameBackboneProfilerMarkerCount = 0;
            state.RuntimeCoreFrameBackboneJournalingMarkerCount = 0;
            state.RuntimeCoreFrameBackboneCoreCostGroupCount = 0;
            state.RuntimeCoreFrameBackbonePhysicsCostGroupCount = 0;
            state.RuntimeCoreFrameBackboneRenderCostGroupCount = 0;
            state.RuntimeCoreFrameBackboneRunnerCostGroupCount = 0;
            state.RuntimeCoreFrameBackbonePhysicsDisabledReasonCount = 0;
            state.RuntimeCoreFrameBackboneRenderDisabledReasonCount = 0;
            state.RuntimeCoreFrameBackboneDebuggerOverheadBudgetMicroseconds = 0;
            state.RuntimeCoreFrameBackboneSamplingInterval = 0;
            state.RuntimeCoreFrameBackboneDisablePolicyCount = 0;
            state.RuntimeCoreFrameBackboneBurstWarmupPolicyCount = 0;
            state.RuntimeCoreFrameBackboneHotPathManagedStringCount = 0;
            state.RuntimeCoreFrameBackboneEvidenceMask = 0;
            em.SetComponentData(debuggerEntity, state);
            em.GetBuffer<GASRuntimeDiagnosticEventBuffer>(debuggerEntity).Clear();
        }

        public static void Configure(
            EntityManager em,
            Entity debuggerEntity,
            bool enabled,
            bool captureSystemTimings,
            bool captureBufferPressure)
        {
            if (!CanUse(em, debuggerEntity))
                return;

            var state = em.GetComponentData<GASRuntimeDebuggerComponent>(debuggerEntity);
            state.Enabled = enabled ? (byte)1 : (byte)0;
            state.CaptureSystemTimings = captureSystemTimings ? (byte)1 : (byte)0;
            state.CaptureBufferPressure = captureBufferPressure ? (byte)1 : (byte)0;
            em.SetComponentData(debuggerEntity, state);
        }

        public static void RecordTickTiming(
            EntityManager em,
            Entity debuggerEntity,
            int frame,
            long totalTicks,
            long commandTicks,
            long resetDirtyTicks,
            long tagTicks,
            long effectTicks,
            long attributeTicks,
            long abilityTicks,
            long cueTicks,
            long stopwatchFrequency)
        {
            if (!TryGetWritableLog(em, debuggerEntity, out var state, out var log))
                return;

            var totalMicroseconds = ToMicroseconds(totalTicks, stopwatchFrequency);
            Append(
                log,
                ref state,
                new GASRuntimeDiagnosticEventBuffer
                {
                    Frame = frame,
                    Kind = EGasRuntimeDiagnosticKind.TickSummary,
                    Severity = SeverityForElapsed(totalMicroseconds, state.SlowTickMicroseconds),
                    Module = EGasRuntimeDiagnosticModule.Runtime,
                    GroupName = "Runtime",
                    ElapsedMicroseconds = totalMicroseconds,
                    TotalMicroseconds = totalMicroseconds,
                });

            AppendGroupTiming(log, ref state, frame, "Command", EGasRuntimeDiagnosticModule.Command, commandTicks, totalTicks, stopwatchFrequency);
            AppendGroupTiming(log, ref state, frame, "ResetDirty", EGasRuntimeDiagnosticModule.ResetDirty, resetDirtyTicks, totalTicks, stopwatchFrequency);
            AppendGroupTiming(log, ref state, frame, "Tag", EGasRuntimeDiagnosticModule.Tag, tagTicks, totalTicks, stopwatchFrequency);
            AppendGroupTiming(log, ref state, frame, "Effect", EGasRuntimeDiagnosticModule.Effect, effectTicks, totalTicks, stopwatchFrequency);
            AppendGroupTiming(log, ref state, frame, "Attribute", EGasRuntimeDiagnosticModule.Attribute, attributeTicks, totalTicks, stopwatchFrequency);
            AppendGroupTiming(log, ref state, frame, "Ability", EGasRuntimeDiagnosticModule.Ability, abilityTicks, totalTicks, stopwatchFrequency);
            AppendGroupTiming(log, ref state, frame, "Cue", EGasRuntimeDiagnosticModule.Cue, cueTicks, totalTicks, stopwatchFrequency);
            ApplyRetention(log, ref state);
            em.SetComponentData(debuggerEntity, state);
        }

        public static void RecordSystemTimingAggregate(
            EntityManager em,
            Entity debuggerEntity,
            int frame,
            string groupName,
            string systemName,
            int callCount,
            long elapsedTicks,
            long stopwatchFrequency)
        {
            if (!TryGetWritableLog(em, debuggerEntity, out var state, out var log)
                || state.CaptureSystemTimings == 0)
            {
                return;
            }

            var totalMicroseconds = ToMicroseconds(elapsedTicks, stopwatchFrequency);
            var averageMicroseconds = callCount > 0 ? totalMicroseconds / callCount : totalMicroseconds;
            Append(
                log,
                ref state,
                new GASRuntimeDiagnosticEventBuffer
                {
                    Frame = frame,
                    Kind = EGasRuntimeDiagnosticKind.SystemTiming,
                    Severity = SeverityForElapsed(averageMicroseconds, state.SlowSystemMicroseconds),
                    Module = ModuleFromGroup(groupName),
                    GroupName = groupName ?? string.Empty,
                    SystemName = systemName ?? string.Empty,
                    ElapsedMicroseconds = averageMicroseconds,
                    TotalMicroseconds = totalMicroseconds,
                    CallCount = callCount,
                });

            ApplyRetention(log, ref state);
            em.SetComponentData(debuggerEntity, state);
        }

        public static void RecordRuntimeCoreCounters(
            EntityManager em,
            Entity debuggerEntity,
            int frame,
            in GasRuntimeCoreDiagnosticCounters counters)
        {
            RecordRuntimeCoreCounters(
                em,
                debuggerEntity,
                frame,
                counters.RequestCount,
                counters.SpecCount,
                counters.DeltaCount,
                counters.FactCount,
                counters.CueCount,
                counters.PresentationCount,
                counters.EntityCreateCount,
                counters.EntityDestroyCount,
                counters.EcbPlaybackCount,
                counters.PeakActiveEffectEntityCount,
                counters.PeakPendingApplyCommandOwnerCount,
                counters.PeakEventBusBufferLength,
                counters.PeakPresentationCursorLag,
                counters.PeakReplayCursorLag,
                counters.ActiveEffectStoreOwnerCount,
                counters.ActiveEffectSlotCount,
                counters.ActiveEffectSlotCapacity,
                counters.ActiveEffectSlotPendingApplyCount,
                counters.ActiveEffectSlotActiveCount,
                counters.ActiveEffectSlotInhibitedCount,
                counters.ActiveEffectSlotPendingRemoveCount,
                counters.ActiveEffectSlotLegacyBackedCount,
                counters.ActiveEffectSlotExternalizedOwnerCount,
                counters.ActiveEffectSlotGrantedTagCount,
                counters.ActiveEffectSlotGrantedAbilityCount,
                counters.ActiveEffectChunkSkipMatchedSlotCount,
                counters.ActiveEffectChunkSkipSkippedSlotCount,
                counters.ActiveEffectChunkSkipDuePeriodSlotCount,
                counters.ActiveEffectChunkSkipNoopSlotCount,
                counters.ActiveEffectChunkSkipOwnerCount,
                counters.ActiveEffectGlobalIndexOwnerCount,
                counters.ActiveEffectGlobalIndexCount,
                counters.ActiveEffectGlobalIndexActiveCount,
                counters.ActiveEffectGlobalIndexInhibitedCount,
                counters.ActiveEffectGlobalIndexPendingRemoveCount,
                counters.ActiveEffectGlobalIndexPeriodDueCount,
                counters.ActiveEffectGlobalIndexDurationDueCount,
                counters.ActiveEffectGlobalIndexStaleCount,
                counters.ActiveEffectGlobalIndexBucketOwnerCount,
                counters.ActiveEffectGlobalIndexBucketIndexCount,
                counters.ActiveEffectGlobalIndexMaxBucketLength,
                counters.ActiveEffectGlobalIndexStableRowCount,
                counters.ActiveEffectGlobalIndexStaleStableRowCount,
                counters.ActiveMutationCommandCount,
                counters.ActiveMutationOwnerGroupCount,
                counters.ActiveMutationMaxOwnerRange,
                counters.ActiveMutationSortMoveCount,
                counters.ActiveMutationEstimatedRandomLookupCount,
                counters.ActiveMutationOwnerResourceLookupCount,
                counters.ActiveMutationMigrationCarrierCount,
                counters.PendingAttributeDeltaCount,
                counters.PendingAttributeAppliedDeltaCount,
                counters.PendingAttributeSkippedDeltaCount,
                counters.PendingAttributeTargetGroupCount,
                counters.PendingAttributeMaxTargetRange,
                counters.PendingAttributeEstimatedRandomLookupCount,
                counters.PendingAttributeFactPatchCount,
                counters.PendingAttributeMigrationCarrierCount,
                counters.QueryBudget,
                counters.FilteredQueryBudget,
                counters.UnfilteredQueryBudget,
                counters.LookupUpdateBudget,
                counters.RandomLookupBudget,
                counters.SyncQueryBudget,
                counters.HelperTempQueryRiskCount,
                counters.DependencyWaitRiskCount,
                counters.WorldUpdateAllocatorOwnerCount,
                counters.RewindableAllocatorCandidateCount);
        }

        public static GasRuntimeCoreDiagnosticCounters CollectRuntimeCoreCounters(
            EntityManager em,
            Entity eventBusEntity,
            Entity eventLogSinkEntity)
        {
            using var queries = GasRuntimeCoreCounterQueries.CreateOwned(em);
            return CollectRuntimeCoreCounters(em, eventBusEntity, eventLogSinkEntity, queries);
        }

        public static GasRuntimeCoreDiagnosticCounters CollectRuntimeCoreCounters(
            EntityManager em,
            Entity eventBusEntity,
            Entity eventLogSinkEntity,
            out GasRuntimeObservationMaterializationCounters observationCounters)
        {
            using var queries = GasRuntimeCoreCounterQueries.CreateOwned(em);
            return CollectRuntimeCoreCounters(em, eventBusEntity, eventLogSinkEntity, queries, out observationCounters);
        }

        public static GasRuntimeCoreDiagnosticCounters CollectRuntimeCoreCounters(
            EntityManager em,
            Entity eventBusEntity,
            Entity eventLogSinkEntity,
            in GasRuntimeCoreCounterQueries queries)
        {
            return CollectRuntimeCoreCounters(em, eventBusEntity, eventLogSinkEntity, queries, out _);
        }

        public static GasRuntimeCoreDiagnosticCounters CollectRuntimeCoreCounters(
            EntityManager em,
            Entity eventBusEntity,
            Entity eventLogSinkEntity,
            in GasRuntimeCoreCounterQueries queries,
            out GasRuntimeObservationMaterializationCounters observationCounters)
        {
            observationCounters = GasRuntimeObservationMaterializationCounters.Empty;
            var pendingCommandOwnerCount = queries.CountPendingCommandOwners();
            var activeEffectEntityCount = queries.CountActiveEffectEntities();
            var pendingApplyCommandOwnerCount = queries.CountPendingApplyCommandOwners();

            ReadEventBusCounters(
                em,
                eventBusEntity,
                out var gameplayEventCount,
                out var attributeChangeCount,
                out var cueRequestCount,
                out var tagChangeCount,
                out var damageEventCount,
                out var gameplayRequestFactCount,
                out var gameplayEffectInstancedCount,
                out var gameplayEffectRemovedCount);
            ReadEffectCommandSpecStreamCounters(
                em,
                out var effectCommandCount,
                out var instantSpecCount,
                out var attributeDeltaCount,
                out var typedFactCount,
                out var activeMutationCommandCount,
                out var activeMutationOwnerGroupCount,
                out var activeMutationMaxOwnerRange,
                out var activeMutationSortMoveCount,
                out var activeMutationEstimatedRandomLookupCount,
                out var activeMutationOwnerResourceLookupCount,
                out var activeMutationMigrationCarrierCount,
                out var pendingAttributeDeltaCount,
                out var pendingAttributeAppliedDeltaCount,
                out var pendingAttributeSkippedDeltaCount,
                out var pendingAttributeTargetGroupCount,
                out var pendingAttributeMaxTargetRange,
                out var pendingAttributeEstimatedRandomLookupCount,
                out var pendingAttributeFactPatchCount,
                out var pendingAttributeMigrationCarrierCount);
            var currentFrame = ResolveCurrentFrame(em);
            ReadActiveEffectGlobalIndexCounters(
                em,
                currentFrame,
                out var activeEffectGlobalIndexOwnerCount,
                out var activeEffectGlobalIndexCount,
                out var activeEffectGlobalIndexActiveCount,
                out var activeEffectGlobalIndexInhibitedCount,
                out var activeEffectGlobalIndexPendingRemoveCount,
                out var activeEffectGlobalIndexPeriodDueCount,
                out var activeEffectGlobalIndexDurationDueCount,
                out var activeEffectGlobalIndexStaleCount,
                out var activeEffectGlobalIndexBucketOwnerCount,
                out var activeEffectGlobalIndexBucketIndexCount,
                out var activeEffectGlobalIndexMaxBucketLength,
                out var activeEffectGlobalIndexStableRowCount,
                out var activeEffectGlobalIndexStaleStableRowCount);
            var activeEffectStoreOwnerCount = queries.CountActiveEffectStoreOwners();
            var activeEffectSlotCount = 0;
            var activeEffectSlotCapacity = activeEffectStoreOwnerCount * ActiveEffectStore.InlineSlotCapacity;
            var activeEffectSlotPendingApplyCount = 0;
            var activeEffectSlotActiveCount = 0;
            var activeEffectSlotInhibitedCount = 0;
            var activeEffectSlotPendingRemoveCount = 0;
            var activeEffectSlotLegacyBackedCount = 0;
            var activeEffectSlotExternalizedOwnerCount = 0;
            var activeEffectSlotGrantedTagCount = 0;
            var activeEffectSlotGrantedAbilityCount = 0;
            var activeEffectChunkSkipMatchedSlotCount = 0;
            var activeEffectChunkSkipSkippedSlotCount = 0;
            var activeEffectChunkSkipDuePeriodSlotCount = 0;
            var activeEffectChunkSkipNoopSlotCount = 0;
            var activeEffectChunkSkipOwnerCount = activeEffectStoreOwnerCount;

            if (activeEffectStoreOwnerCount > 0)
            {
                ReadActiveEffectStoreCounters(
                    em,
                    queries.ActiveEffectStores,
                    currentFrame,
                    out activeEffectStoreOwnerCount,
                    out activeEffectSlotCount,
                    out activeEffectSlotCapacity,
                    out activeEffectSlotPendingApplyCount,
                    out activeEffectSlotActiveCount,
                    out activeEffectSlotInhibitedCount,
                    out activeEffectSlotPendingRemoveCount,
                    out activeEffectSlotLegacyBackedCount,
                    out activeEffectSlotExternalizedOwnerCount,
                    out activeEffectSlotGrantedTagCount,
                    out activeEffectSlotGrantedAbilityCount,
                    out activeEffectChunkSkipMatchedSlotCount,
                    out activeEffectChunkSkipSkippedSlotCount,
                    out activeEffectChunkSkipDuePeriodSlotCount,
                    out activeEffectChunkSkipNoopSlotCount,
                    out activeEffectChunkSkipOwnerCount,
                    ref observationCounters);
            }

            var boundaryBridgeEventCount = attributeChangeCount + cueRequestCount + tagChangeCount + damageEventCount;
            var factCount = typedFactCount;
            var deltaCount = attributeDeltaCount;
            var streamBufferPeak = Math.Max(
                Math.Max(effectCommandCount, instantSpecCount),
                Math.Max(attributeDeltaCount, typedFactCount));
            var presentationCount = CountPresentationOutboxEvents(
                em,
                eventBusEntity,
                queries.PresentationOutboxes,
                ref observationCounters);
            var presentationCursorLag = CalculatePresentationCursorLag(
                em,
                eventBusEntity,
                currentFrame,
                typedFactCount);
            var replayCursorLag = CalculateReplayCursorLag(
                em,
                eventLogSinkEntity,
                currentFrame,
                typedFactCount);
            var frameBudget = GASRuntimeFrameBudgetPlanner.CreateCurrent();

            return new GasRuntimeCoreDiagnosticCounters(
                pendingCommandOwnerCount + gameplayRequestFactCount + effectCommandCount,
                gameplayEffectInstancedCount + instantSpecCount,
                deltaCount,
                factCount,
                boundaryBridgeEventCount,
                presentationCount,
                gameplayEffectInstancedCount,
                gameplayEffectRemovedCount,
                0,
                activeEffectEntityCount,
                pendingApplyCommandOwnerCount,
                Math.Max(factCount, streamBufferPeak),
                presentationCursorLag,
                replayCursorLag,
                activeEffectStoreOwnerCount,
                activeEffectSlotCount,
                activeEffectSlotCapacity,
                activeEffectSlotPendingApplyCount,
                activeEffectSlotActiveCount,
                activeEffectSlotInhibitedCount,
                activeEffectSlotPendingRemoveCount,
                activeEffectSlotLegacyBackedCount,
                activeEffectSlotExternalizedOwnerCount,
                activeEffectSlotGrantedTagCount,
                activeEffectSlotGrantedAbilityCount,
                activeEffectChunkSkipMatchedSlotCount,
                activeEffectChunkSkipSkippedSlotCount,
                activeEffectChunkSkipDuePeriodSlotCount,
                activeEffectChunkSkipNoopSlotCount,
                activeEffectChunkSkipOwnerCount,
                activeEffectGlobalIndexOwnerCount,
                activeEffectGlobalIndexCount,
                activeEffectGlobalIndexActiveCount,
                activeEffectGlobalIndexInhibitedCount,
                activeEffectGlobalIndexPendingRemoveCount,
                activeEffectGlobalIndexPeriodDueCount,
                activeEffectGlobalIndexDurationDueCount,
                activeEffectGlobalIndexStaleCount,
                activeEffectGlobalIndexBucketOwnerCount,
                activeEffectGlobalIndexBucketIndexCount,
                activeEffectGlobalIndexMaxBucketLength,
                activeEffectGlobalIndexStableRowCount,
                activeEffectGlobalIndexStaleStableRowCount,
                activeMutationCommandCount,
                activeMutationOwnerGroupCount,
                activeMutationMaxOwnerRange,
                activeMutationSortMoveCount,
                activeMutationEstimatedRandomLookupCount,
                activeMutationOwnerResourceLookupCount,
                activeMutationMigrationCarrierCount,
                pendingAttributeDeltaCount,
                pendingAttributeAppliedDeltaCount,
                pendingAttributeSkippedDeltaCount,
                pendingAttributeTargetGroupCount,
                pendingAttributeMaxTargetRange,
                pendingAttributeEstimatedRandomLookupCount,
                pendingAttributeFactPatchCount,
                pendingAttributeMigrationCarrierCount,
                frameBudget.TotalQueryBudget,
                frameBudget.TotalFilteredQueryBudget,
                frameBudget.TotalUnfilteredQueryBudget,
                frameBudget.TotalLookupUpdateBudget,
                frameBudget.TotalRandomLookupBudget,
                frameBudget.TotalSyncQueryBudget,
                frameBudget.HelperTempQueryRiskCount,
                frameBudget.DependencyWaitRiskCount,
                frameBudget.WorldUpdateAllocatorOwnerCount,
                frameBudget.RewindableAllocatorCandidateCount);
        }

        public static void CollectAndRecordRuntimeCoreCounters(
            EntityManager em,
            Entity debuggerEntity,
            int frame,
            Entity eventBusEntity,
            Entity eventLogSinkEntity)
        {
            var counters = CollectRuntimeCoreCounters(
                em,
                eventBusEntity,
                eventLogSinkEntity,
                out var observationCounters);
            RecordRuntimeCoreCounters(em, debuggerEntity, frame, counters);
            RecordObservationMaterialization(em, debuggerEntity, frame, observationCounters);
        }

        public static void CollectAndRecordRuntimeCoreCounters(
            EntityManager em,
            Entity debuggerEntity,
            int frame,
            Entity eventBusEntity,
            Entity eventLogSinkEntity,
            in GasRuntimeCoreCounterQueries queries)
        {
            var counters = CollectRuntimeCoreCounters(
                em,
                eventBusEntity,
                eventLogSinkEntity,
                queries,
                out var observationCounters);
            RecordRuntimeCoreCounters(em, debuggerEntity, frame, counters);
            RecordObservationMaterialization(em, debuggerEntity, frame, observationCounters);
        }

        public static void RecordRuntimeCoreEcbPlayback(
            EntityManager em,
            int frame,
            EGasRuntimeDiagnosticModule module,
            int playbackCount = 1)
        {
            if (!TryGetSingleton(em, out var debuggerEntity))
                return;

            RecordRuntimeCoreEcbPlayback(
                em,
                debuggerEntity,
                frame,
                module,
                playbackCount);
        }

        public static void RecordRuntimeCoreEcbPlayback(
            EntityManager em,
            Entity debuggerEntity,
            int frame,
            EGasRuntimeDiagnosticModule module,
            int playbackCount = 1)
        {
            if (playbackCount <= 0 || !TryGetWritableLog(em, debuggerEntity, out var state, out var log))
                return;

            state.RuntimeCoreEcbPlaybackCount += playbackCount;
            Append(
                log,
                ref state,
                new GASRuntimeDiagnosticEventBuffer
                {
                    Frame = frame,
                    Kind = EGasRuntimeDiagnosticKind.StructuralChange,
                    Severity = EGasRuntimeDiagnosticSeverity.Trace,
                    Module = module,
                    EcbPlaybackCount = playbackCount,
                    Count = playbackCount,
                });

            ApplyRetention(log, ref state);
            em.SetComponentData(debuggerEntity, state);
        }

        public static void RecordRuntimeCoreStructuralPlaybackGate(
            EntityManager em,
            Entity debuggerEntity,
            int frame,
            EGasRuntimeDiagnosticModule module,
            int playbackCount = 1,
            int ecbCommandCount = 0,
            int bulkQueryCount = 0)
        {
            if ((playbackCount <= 0 && ecbCommandCount <= 0 && bulkQueryCount <= 0)
                || !TryGetWritableLog(em, debuggerEntity, out var state, out var log))
            {
                return;
            }

            if (playbackCount > 0)
                state.RuntimeCoreEcbPlaybackCount += playbackCount;
            if (playbackCount > 0)
                state.RuntimeCoreFrameBackboneRecordedStructuralPlaybackCount += playbackCount;
            if (ecbCommandCount > 0)
                state.RuntimeCoreFrameBackboneEcbCommandCount += ecbCommandCount;
            if (bulkQueryCount > 0)
                state.RuntimeCoreFrameBackboneBulkQueryCount += bulkQueryCount;

            Append(
                log,
                ref state,
                new GASRuntimeDiagnosticEventBuffer
                {
                    Frame = frame,
                    Kind = EGasRuntimeDiagnosticKind.StructuralChange,
                    Severity = EGasRuntimeDiagnosticSeverity.Trace,
                    Module = module,
                    GroupName = GASRuntimeStructuralPlaybackGateNames.DebuggerGroupName,
                    EcbPlaybackCount = playbackCount,
                    ValueA = ecbCommandCount,
                    ValueB = bulkQueryCount,
                });

            ApplyRetention(log, ref state);
            em.SetComponentData(debuggerEntity, state);
        }

        public static void RecordCurrentRuntimeCoreFrameBackboneEvidence(
            EntityManager em,
            Entity debuggerEntity,
            int frame)
        {
            var plan = GASRuntimeDebuggerEvidenceGatePlanner.CreateCurrent();
            RecordRuntimeCoreFrameBackboneEvidence(em, debuggerEntity, frame, plan);
        }

        public static void RecordRuntimeCoreFrameBackboneEvidence(
            EntityManager em,
            Entity debuggerEntity,
            int frame,
            in GASRuntimeDebuggerEvidenceGatePlan plan)
        {
            if (!TryGetWritableLog(em, debuggerEntity, out var state, out var log))
                return;

            var counters = new GasRuntimeFrameBackboneDiagnosticCounters(
                plan,
                state.RuntimeCoreFrameBackboneRecordedStructuralPlaybackCount,
                state.RuntimeCoreFrameBackboneEcbCommandCount,
                state.RuntimeCoreFrameBackboneBulkQueryCount);
            ApplyRuntimeCoreFrameBackboneCounters(ref state, counters);

            Append(
                log,
                ref state,
                new GASRuntimeDiagnosticEventBuffer
                {
                    Frame = frame,
                    Kind = EGasRuntimeDiagnosticKind.RuntimeCoreFrameBackbone,
                    Severity = EGasRuntimeDiagnosticSeverity.Trace,
                    Module = EGasRuntimeDiagnosticModule.Runtime,
                    GroupName = GASRuntimeDebuggerEvidenceGateNames.DebuggerGroupName,
                    FrameBackbonePhaseCount = counters.PhaseCount,
                    FrameBackboneContractOnlyPhaseCount = counters.ContractOnlyPhaseCount,
                    QueryBudget = counters.QueryBudget,
                    FilteredQueryBudget = counters.FilteredQueryBudget,
                    UnfilteredQueryBudget = counters.UnfilteredQueryBudget,
                    LookupUpdateBudget = counters.LookupUpdateBudget,
                    RandomLookupBudget = counters.RandomLookupBudget,
                    SyncQueryBudget = counters.SyncQueryBudget,
                    DependencyWaitRiskCount = counters.DependencyWaitRiskCount,
                    WorldUpdateAllocatorOwnerCount = counters.WorldUpdateAllocatorOwnerCount,
                    RewindableAllocatorCandidateCount = counters.RewindableAllocatorCandidateCount,
                    FrameBackboneStreamCount = counters.StreamCount,
                    FrameBackboneMigrationCarrierCount = counters.MigrationCarrierCount,
                    FrameBackboneNativeStreamCandidateCount = counters.NativeStreamCandidateCount,
                    FrameBackboneOwnerLocalBufferCandidateCount = counters.OwnerLocalBufferCandidateCount,
                    FrameBackboneBattleHashStreamCount = counters.BattleHashStreamCount,
                    FrameBackboneDeterministicMergePolicyCount = counters.DeterministicMergePolicyCount,
                    FrameBackboneMergeCostMeasuredCount = counters.MergeCostMeasuredCount,
                    FrameBackboneMergeCostMicroseconds = counters.MergeCostMicroseconds,
                    FrameBackboneRequiredStructuralPlaybackCount = counters.RequiredStructuralPlaybackCount,
                    FrameBackboneRecordedStructuralPlaybackCount = counters.RecordedStructuralPlaybackCount,
                    FrameBackboneEcbCommandCount = counters.EcbCommandCount,
                    FrameBackboneBulkQueryCount = counters.BulkQueryCount,
                    FrameBackboneProfilerMarkerCount = counters.ProfilerMarkerCount,
                    FrameBackboneJournalingMarkerCount = counters.JournalingMarkerCount,
                    FrameBackboneCoreCostGroupCount = counters.CoreCostGroupCount,
                    FrameBackbonePhysicsCostGroupCount = counters.PhysicsCostGroupCount,
                    FrameBackboneRenderCostGroupCount = counters.RenderCostGroupCount,
                    FrameBackboneRunnerCostGroupCount = counters.RunnerCostGroupCount,
                    FrameBackbonePhysicsDisabledReasonCount = counters.PhysicsDisabledReasonCount,
                    FrameBackboneRenderDisabledReasonCount = counters.RenderDisabledReasonCount,
                    FrameBackboneDebuggerOverheadBudgetMicroseconds = counters.DebuggerOverheadBudgetMicroseconds,
                    FrameBackboneSamplingInterval = counters.SamplingInterval,
                    FrameBackboneDisablePolicyCount = counters.DisablePolicyCount,
                    FrameBackboneBurstWarmupPolicyCount = counters.BurstWarmupPolicyCount,
                    FrameBackboneHotPathManagedStringCount = counters.HotPathManagedStringCount,
                    FrameBackboneEvidenceMask = counters.EvidenceMask,
                });

            ApplyRetention(log, ref state);
            em.SetComponentData(debuggerEntity, state);
        }

        public static int ResolveCurrentFrame(EntityManager em)
        {
            return GASRuntimeFrameContext.ResolveCurrentFrame(em);
        }

        public static void RecordRuntimeCoreCounters(
            EntityManager em,
            Entity debuggerEntity,
            int frame,
            int requestCount,
            int specCount,
            int deltaCount,
            int factCount,
            int cueCount,
            int presentationCount,
            int entityCreateCount,
            int entityDestroyCount,
            int ecbPlaybackCount,
            int activeEffectEntityCount,
            int pendingApplyCommandOwnerCount,
            int eventBusBufferLength = 0,
            int presentationCursorLag = 0,
            int replayCursorLag = 0,
            int activeEffectStoreOwnerCount = 0,
            int activeEffectSlotCount = 0,
            int activeEffectSlotCapacity = 0,
            int activeEffectSlotPendingApplyCount = 0,
            int activeEffectSlotActiveCount = 0,
            int activeEffectSlotInhibitedCount = 0,
            int activeEffectSlotPendingRemoveCount = 0,
            int activeEffectSlotLegacyBackedCount = 0,
            int activeEffectSlotExternalizedOwnerCount = 0,
            int activeEffectSlotGrantedTagCount = 0,
            int activeEffectSlotGrantedAbilityCount = 0,
            int activeEffectChunkSkipMatchedSlotCount = 0,
            int activeEffectChunkSkipSkippedSlotCount = 0,
            int activeEffectChunkSkipDuePeriodSlotCount = 0,
            int activeEffectChunkSkipNoopSlotCount = 0,
            int activeEffectChunkSkipOwnerCount = 0,
            int activeEffectGlobalIndexOwnerCount = 0,
            int activeEffectGlobalIndexCount = 0,
            int activeEffectGlobalIndexActiveCount = 0,
            int activeEffectGlobalIndexInhibitedCount = 0,
            int activeEffectGlobalIndexPendingRemoveCount = 0,
            int activeEffectGlobalIndexPeriodDueCount = 0,
            int activeEffectGlobalIndexDurationDueCount = 0,
            int activeEffectGlobalIndexStaleCount = 0,
            int activeEffectGlobalIndexBucketOwnerCount = 0,
            int activeEffectGlobalIndexBucketIndexCount = 0,
            int activeEffectGlobalIndexMaxBucketLength = 0,
            int activeEffectGlobalIndexStableRowCount = 0,
            int activeEffectGlobalIndexStaleStableRowCount = 0,
            int activeMutationCommandCount = 0,
            int activeMutationOwnerGroupCount = 0,
            int activeMutationMaxOwnerRange = 0,
            int activeMutationSortMoveCount = 0,
            int activeMutationEstimatedRandomLookupCount = 0,
            int activeMutationOwnerResourceLookupCount = 0,
            int activeMutationMigrationCarrierCount = 0,
            int pendingAttributeDeltaCount = 0,
            int pendingAttributeAppliedDeltaCount = 0,
            int pendingAttributeSkippedDeltaCount = 0,
            int pendingAttributeTargetGroupCount = 0,
            int pendingAttributeMaxTargetRange = 0,
            int pendingAttributeEstimatedRandomLookupCount = 0,
            int pendingAttributeFactPatchCount = 0,
            int pendingAttributeMigrationCarrierCount = 0,
            int queryBudget = 0,
            int filteredQueryBudget = 0,
            int unfilteredQueryBudget = 0,
            int lookupUpdateBudget = 0,
            int randomLookupBudget = 0,
            int syncQueryBudget = 0,
            int helperTempQueryRiskCount = 0,
            int dependencyWaitRiskCount = 0,
            int worldUpdateAllocatorOwnerCount = 0,
            int rewindableAllocatorCandidateCount = 0)
        {
            if (!TryGetWritableLog(em, debuggerEntity, out var state, out var log))
                return;

            state.RuntimeCoreRequestCount += requestCount;
            state.RuntimeCoreSpecCount += specCount;
            state.RuntimeCoreDeltaCount += deltaCount;
            state.RuntimeCoreFactCount += factCount;
            state.RuntimeCoreCueCount += cueCount;
            state.RuntimeCorePresentationCount += presentationCount;
            state.RuntimeCoreEntityCreateCount += entityCreateCount;
            state.RuntimeCoreEntityDestroyCount += entityDestroyCount;
            state.RuntimeCoreEcbPlaybackCount += ecbPlaybackCount;
            if (activeEffectEntityCount > state.RuntimeCorePeakActiveEffectEntityCount)
                state.RuntimeCorePeakActiveEffectEntityCount = activeEffectEntityCount;
            if (pendingApplyCommandOwnerCount > state.RuntimeCorePeakPendingApplyCommandOwnerCount)
                state.RuntimeCorePeakPendingApplyCommandOwnerCount = pendingApplyCommandOwnerCount;
            if (eventBusBufferLength > state.RuntimeCorePeakEventBusBufferLength)
                state.RuntimeCorePeakEventBusBufferLength = eventBusBufferLength;
            if (presentationCursorLag > state.RuntimeCorePeakPresentationCursorLag)
                state.RuntimeCorePeakPresentationCursorLag = presentationCursorLag;
            if (replayCursorLag > state.RuntimeCorePeakReplayCursorLag)
                state.RuntimeCorePeakReplayCursorLag = replayCursorLag;
            state.RuntimeCoreActiveEffectStoreOwnerCount = activeEffectStoreOwnerCount;
            state.RuntimeCoreActiveEffectSlotCount = activeEffectSlotCount;
            state.RuntimeCoreActiveEffectSlotCapacity = activeEffectSlotCapacity;
            state.RuntimeCoreActiveEffectSlotPendingApplyCount = activeEffectSlotPendingApplyCount;
            state.RuntimeCoreActiveEffectSlotActiveCount = activeEffectSlotActiveCount;
            state.RuntimeCoreActiveEffectSlotInhibitedCount = activeEffectSlotInhibitedCount;
            state.RuntimeCoreActiveEffectSlotPendingRemoveCount = activeEffectSlotPendingRemoveCount;
            state.RuntimeCoreActiveEffectSlotLegacyBackedCount = activeEffectSlotLegacyBackedCount;
            state.RuntimeCoreActiveEffectSlotExternalizedOwnerCount = activeEffectSlotExternalizedOwnerCount;
            state.RuntimeCoreActiveEffectSlotGrantedTagCount = activeEffectSlotGrantedTagCount;
            state.RuntimeCoreActiveEffectSlotGrantedAbilityCount = activeEffectSlotGrantedAbilityCount;
            state.RuntimeCoreActiveEffectChunkSkipMatchedSlotCount = activeEffectChunkSkipMatchedSlotCount;
            state.RuntimeCoreActiveEffectChunkSkipSkippedSlotCount = activeEffectChunkSkipSkippedSlotCount;
            state.RuntimeCoreActiveEffectChunkSkipDuePeriodSlotCount = activeEffectChunkSkipDuePeriodSlotCount;
            state.RuntimeCoreActiveEffectChunkSkipNoopSlotCount = activeEffectChunkSkipNoopSlotCount;
            state.RuntimeCoreActiveEffectChunkSkipOwnerCount = activeEffectChunkSkipOwnerCount;
            state.RuntimeCoreActiveEffectGlobalIndexOwnerCount = activeEffectGlobalIndexOwnerCount;
            state.RuntimeCoreActiveEffectGlobalIndexCount = activeEffectGlobalIndexCount;
            state.RuntimeCoreActiveEffectGlobalIndexActiveCount = activeEffectGlobalIndexActiveCount;
            state.RuntimeCoreActiveEffectGlobalIndexInhibitedCount = activeEffectGlobalIndexInhibitedCount;
            state.RuntimeCoreActiveEffectGlobalIndexPendingRemoveCount = activeEffectGlobalIndexPendingRemoveCount;
            state.RuntimeCoreActiveEffectGlobalIndexPeriodDueCount = activeEffectGlobalIndexPeriodDueCount;
            state.RuntimeCoreActiveEffectGlobalIndexDurationDueCount = activeEffectGlobalIndexDurationDueCount;
            state.RuntimeCoreActiveEffectGlobalIndexStaleCount = activeEffectGlobalIndexStaleCount;
            state.RuntimeCoreActiveEffectGlobalIndexBucketOwnerCount = activeEffectGlobalIndexBucketOwnerCount;
            state.RuntimeCoreActiveEffectGlobalIndexBucketIndexCount = activeEffectGlobalIndexBucketIndexCount;
            state.RuntimeCoreActiveEffectGlobalIndexMaxBucketLength = activeEffectGlobalIndexMaxBucketLength;
            state.RuntimeCoreActiveEffectGlobalIndexStableRowCount = activeEffectGlobalIndexStableRowCount;
            state.RuntimeCoreActiveEffectGlobalIndexStaleStableRowCount = activeEffectGlobalIndexStaleStableRowCount;
            state.RuntimeCoreActiveMutationCommandCount += activeMutationCommandCount;
            state.RuntimeCoreActiveMutationOwnerGroupCount += activeMutationOwnerGroupCount;
            if (activeMutationMaxOwnerRange > state.RuntimeCoreActiveMutationMaxOwnerRange)
                state.RuntimeCoreActiveMutationMaxOwnerRange = activeMutationMaxOwnerRange;
            state.RuntimeCoreActiveMutationSortMoveCount += activeMutationSortMoveCount;
            state.RuntimeCoreActiveMutationEstimatedRandomLookupCount += activeMutationEstimatedRandomLookupCount;
            state.RuntimeCoreActiveMutationOwnerResourceLookupCount += activeMutationOwnerResourceLookupCount;
            state.RuntimeCoreActiveMutationMigrationCarrierCount += activeMutationMigrationCarrierCount;
            state.RuntimeCorePendingAttributeDeltaCount += pendingAttributeDeltaCount;
            state.RuntimeCorePendingAttributeAppliedDeltaCount += pendingAttributeAppliedDeltaCount;
            state.RuntimeCorePendingAttributeSkippedDeltaCount += pendingAttributeSkippedDeltaCount;
            state.RuntimeCorePendingAttributeTargetGroupCount += pendingAttributeTargetGroupCount;
            if (pendingAttributeMaxTargetRange > state.RuntimeCorePendingAttributeMaxTargetRange)
                state.RuntimeCorePendingAttributeMaxTargetRange = pendingAttributeMaxTargetRange;
            state.RuntimeCorePendingAttributeEstimatedRandomLookupCount += pendingAttributeEstimatedRandomLookupCount;
            state.RuntimeCorePendingAttributeFactPatchCount += pendingAttributeFactPatchCount;
            state.RuntimeCorePendingAttributeMigrationCarrierCount += pendingAttributeMigrationCarrierCount;
            state.RuntimeCoreQueryBudget = queryBudget;
            state.RuntimeCoreFilteredQueryBudget = filteredQueryBudget;
            state.RuntimeCoreUnfilteredQueryBudget = unfilteredQueryBudget;
            state.RuntimeCoreLookupUpdateBudget = lookupUpdateBudget;
            state.RuntimeCoreRandomLookupBudget = randomLookupBudget;
            state.RuntimeCoreSyncQueryBudget = syncQueryBudget;
            state.RuntimeCoreHelperTempQueryRiskCount = helperTempQueryRiskCount;
            state.RuntimeCoreDependencyWaitRiskCount = dependencyWaitRiskCount;
            state.RuntimeCoreWorldUpdateAllocatorOwnerCount = worldUpdateAllocatorOwnerCount;
            state.RuntimeCoreRewindableAllocatorCandidateCount = rewindableAllocatorCandidateCount;

            Append(
                log,
                ref state,
                new GASRuntimeDiagnosticEventBuffer
                {
                    Frame = frame,
                    Kind = EGasRuntimeDiagnosticKind.RuntimeCoreCounters,
                    Severity = EGasRuntimeDiagnosticSeverity.Trace,
                    Module = EGasRuntimeDiagnosticModule.Runtime,
                    GroupName = "RuntimeCore",
                    Count = requestCount,
                    ValueA = specCount,
                    ValueB = deltaCount,
                    Capacity = factCount,
                    CallCount = cueCount,
                    TotalMicroseconds = presentationCount,
                    ElapsedMicroseconds = activeEffectEntityCount,
                    EntityCreateCount = entityCreateCount,
                    EntityDestroyCount = entityDestroyCount,
                    EcbPlaybackCount = ecbPlaybackCount,
                    ActiveEffectEntityCount = activeEffectEntityCount,
                    PendingApplyCommandOwnerCount = pendingApplyCommandOwnerCount,
                    EventBusBufferLength = eventBusBufferLength,
                    PresentationCursorLag = presentationCursorLag,
                    ReplayCursorLag = replayCursorLag,
                    ActiveEffectStoreOwnerCount = activeEffectStoreOwnerCount,
                    ActiveEffectSlotCount = activeEffectSlotCount,
                    ActiveEffectSlotCapacity = activeEffectSlotCapacity,
                    ActiveEffectSlotPendingApplyCount = activeEffectSlotPendingApplyCount,
                    ActiveEffectSlotActiveCount = activeEffectSlotActiveCount,
                    ActiveEffectSlotInhibitedCount = activeEffectSlotInhibitedCount,
                    ActiveEffectSlotPendingRemoveCount = activeEffectSlotPendingRemoveCount,
                    ActiveEffectSlotLegacyBackedCount = activeEffectSlotLegacyBackedCount,
                    ActiveEffectSlotExternalizedOwnerCount = activeEffectSlotExternalizedOwnerCount,
                    ActiveEffectSlotGrantedTagCount = activeEffectSlotGrantedTagCount,
                    ActiveEffectSlotGrantedAbilityCount = activeEffectSlotGrantedAbilityCount,
                    ActiveEffectChunkSkipMatchedSlotCount = activeEffectChunkSkipMatchedSlotCount,
                    ActiveEffectChunkSkipSkippedSlotCount = activeEffectChunkSkipSkippedSlotCount,
                    ActiveEffectChunkSkipDuePeriodSlotCount = activeEffectChunkSkipDuePeriodSlotCount,
                    ActiveEffectChunkSkipNoopSlotCount = activeEffectChunkSkipNoopSlotCount,
                    ActiveEffectChunkSkipOwnerCount = activeEffectChunkSkipOwnerCount,
                    ActiveEffectGlobalIndexOwnerCount = activeEffectGlobalIndexOwnerCount,
                    ActiveEffectGlobalIndexCount = activeEffectGlobalIndexCount,
                    ActiveEffectGlobalIndexActiveCount = activeEffectGlobalIndexActiveCount,
                    ActiveEffectGlobalIndexInhibitedCount = activeEffectGlobalIndexInhibitedCount,
                    ActiveEffectGlobalIndexPendingRemoveCount = activeEffectGlobalIndexPendingRemoveCount,
                    ActiveEffectGlobalIndexPeriodDueCount = activeEffectGlobalIndexPeriodDueCount,
                    ActiveEffectGlobalIndexDurationDueCount = activeEffectGlobalIndexDurationDueCount,
                    ActiveEffectGlobalIndexStaleCount = activeEffectGlobalIndexStaleCount,
                    ActiveEffectGlobalIndexBucketOwnerCount = activeEffectGlobalIndexBucketOwnerCount,
                    ActiveEffectGlobalIndexBucketIndexCount = activeEffectGlobalIndexBucketIndexCount,
                    ActiveEffectGlobalIndexMaxBucketLength = activeEffectGlobalIndexMaxBucketLength,
                    ActiveEffectGlobalIndexStableRowCount = activeEffectGlobalIndexStableRowCount,
                    ActiveEffectGlobalIndexStaleStableRowCount = activeEffectGlobalIndexStaleStableRowCount,
                    ActiveMutationCommandCount = activeMutationCommandCount,
                    ActiveMutationOwnerGroupCount = activeMutationOwnerGroupCount,
                    ActiveMutationMaxOwnerRange = activeMutationMaxOwnerRange,
                    ActiveMutationSortMoveCount = activeMutationSortMoveCount,
                    ActiveMutationEstimatedRandomLookupCount = activeMutationEstimatedRandomLookupCount,
                    ActiveMutationOwnerResourceLookupCount = activeMutationOwnerResourceLookupCount,
                    ActiveMutationMigrationCarrierCount = activeMutationMigrationCarrierCount,
                    PendingAttributeDeltaCount = pendingAttributeDeltaCount,
                    PendingAttributeAppliedDeltaCount = pendingAttributeAppliedDeltaCount,
                    PendingAttributeSkippedDeltaCount = pendingAttributeSkippedDeltaCount,
                    PendingAttributeTargetGroupCount = pendingAttributeTargetGroupCount,
                    PendingAttributeMaxTargetRange = pendingAttributeMaxTargetRange,
                    PendingAttributeEstimatedRandomLookupCount = pendingAttributeEstimatedRandomLookupCount,
                    PendingAttributeFactPatchCount = pendingAttributeFactPatchCount,
                    PendingAttributeMigrationCarrierCount = pendingAttributeMigrationCarrierCount,
                    QueryBudget = queryBudget,
                    FilteredQueryBudget = filteredQueryBudget,
                    UnfilteredQueryBudget = unfilteredQueryBudget,
                    LookupUpdateBudget = lookupUpdateBudget,
                    RandomLookupBudget = randomLookupBudget,
                    SyncQueryBudget = syncQueryBudget,
                    HelperTempQueryRiskCount = helperTempQueryRiskCount,
                    DependencyWaitRiskCount = dependencyWaitRiskCount,
                    WorldUpdateAllocatorOwnerCount = worldUpdateAllocatorOwnerCount,
                    RewindableAllocatorCandidateCount = rewindableAllocatorCandidateCount,
                });

            ApplyRetention(log, ref state);
            em.SetComponentData(debuggerEntity, state);
        }

        public static void RecordObservationMaterialization(
            EntityManager em,
            Entity debuggerEntity,
            int frame,
            in GasRuntimeObservationMaterializationCounters counters)
        {
            if (!counters.HasMaterialization
                || !TryGetWritableLog(em, debuggerEntity, out var state, out var log))
            {
                return;
            }

            Append(
                log,
                ref state,
                new GASRuntimeDiagnosticEventBuffer
                {
                    Frame = frame,
                    Kind = EGasRuntimeDiagnosticKind.ObservationMaterialization,
                    Severity = EGasRuntimeDiagnosticSeverity.Trace,
                    Module = EGasRuntimeDiagnosticModule.Presentation,
                    GroupName = "DebuggerObservation",
                    SystemName = nameof(DiagnosticsSnapshotSystem),
                    BufferName = "ToEntityArray",
                    Count = counters.MaterializedQueryCount,
                    Capacity = counters.MaterializedEntityCount,
                    ElapsedMicroseconds = counters.ElapsedMicroseconds,
                    ObservationMaterializedQueryCount = counters.MaterializedQueryCount,
                    ObservationMaterializedEntityCount = counters.MaterializedEntityCount,
                    ObservationMaterializationElapsedMicroseconds = counters.ElapsedMicroseconds,
                    ObservationActiveEffectStoreQueryCount = counters.ActiveEffectStoreQueryCount,
                    ObservationActiveEffectStoreEntityCount = counters.ActiveEffectStoreEntityCount,
                    ObservationPresentationOutboxQueryCount = counters.PresentationOutboxQueryCount,
                    ObservationPresentationOutboxEntityCount = counters.PresentationOutboxEntityCount,
                    ObservationPerformancePollutionRiskCount = counters.PerformancePollutionRiskCount,
                });

            ApplyRetention(log, ref state);
            em.SetComponentData(debuggerEntity, state);
        }

        public static void RecordEventBusPressure(
            EntityManager em,
            Entity debuggerEntity,
            Entity eventBusEntity,
            int frame)
        {
            if (!TryGetWritableLog(em, debuggerEntity, out var state, out var log)
                || state.CaptureBufferPressure == 0
                || eventBusEntity == Entity.Null
                || !em.Exists(eventBusEntity))
            {
                return;
            }

            RecordBufferPressure<AttributeChangeEventBuffer>(em, eventBusEntity, "AttributeChangeEventBuffer", frame, log, ref state);
            RecordBufferPressure<CueRequestBuffer>(em, eventBusEntity, "CueRequestBuffer", frame, log, ref state);
            RecordBufferPressure<TagChangeEventBuffer>(em, eventBusEntity, "TagChangeEventBuffer", frame, log, ref state);
            ApplyRetention(log, ref state);
            em.SetComponentData(debuggerEntity, state);
        }

        public static void RecordEffectCommandSpecStreamPressure(
            EntityManager em,
            Entity debuggerEntity,
            int frame)
        {
            if (!TryGetWritableLog(em, debuggerEntity, out var state, out var log)
                || state.CaptureBufferPressure == 0
                || !EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity)
                || streamEntity == Entity.Null
                || !em.Exists(streamEntity))
            {
                return;
            }

            var streamPlan = GASRuntimeFrameStreamOwnerPlanner.CreateCurrent();
            RecordFrameStreamBufferPressure<GEEffectCommandBuffer>(
                em,
                streamEntity,
                "GEEffectCommandBuffer",
                EGasRuntimeFrameStreamId.EffectCommand,
                streamPlan,
                frame,
                log,
                ref state);
            RecordFrameStreamBufferPressure<GESetByCallerValueBuffer>(
                em,
                streamEntity,
                "GESetByCallerValueBuffer",
                EGasRuntimeFrameStreamId.EffectCommandSetByCaller,
                streamPlan,
                frame,
                log,
                ref state);
            RecordFrameStreamBufferPressure<GEEffectSpecBuffer>(
                em,
                streamEntity,
                "GEEffectSpecBuffer",
                EGasRuntimeFrameStreamId.InstantEffectSpec,
                streamPlan,
                frame,
                log,
                ref state);
            RecordFrameStreamBufferPressure<ActiveEffectMutationBuffer>(
                em,
                streamEntity,
                "ActiveEffectMutationBuffer",
                EGasRuntimeFrameStreamId.ActiveEffectMutation,
                streamPlan,
                frame,
                log,
                ref state);
            RecordFrameStreamBufferPressure<AttributeModifierBuffer>(
                em,
                streamEntity,
                "AttributeModifierBuffer",
                EGasRuntimeFrameStreamId.AttributeDelta,
                streamPlan,
                frame,
                log,
                ref state);
            RecordFrameStreamBufferPressure<GameplayEventBuffer>(
                em,
                streamEntity,
                "GameplayEventBuffer",
                EGasRuntimeFrameStreamId.TypedSimulationFact,
                streamPlan,
                frame,
                log,
                ref state);

            ApplyRetention(log, ref state);
            em.SetComponentData(debuggerEntity, state);
        }

        private static void ApplyRuntimeCoreFrameBackboneCounters(
            ref GASRuntimeDebuggerComponent state,
            in GasRuntimeFrameBackboneDiagnosticCounters counters)
        {
            state.RuntimeCoreQueryBudget = counters.QueryBudget;
            state.RuntimeCoreFilteredQueryBudget = counters.FilteredQueryBudget;
            state.RuntimeCoreUnfilteredQueryBudget = counters.UnfilteredQueryBudget;
            state.RuntimeCoreLookupUpdateBudget = counters.LookupUpdateBudget;
            state.RuntimeCoreRandomLookupBudget = counters.RandomLookupBudget;
            state.RuntimeCoreSyncQueryBudget = counters.SyncQueryBudget;
            state.RuntimeCoreDependencyWaitRiskCount = counters.DependencyWaitRiskCount;
            state.RuntimeCoreWorldUpdateAllocatorOwnerCount = counters.WorldUpdateAllocatorOwnerCount;
            state.RuntimeCoreRewindableAllocatorCandidateCount = counters.RewindableAllocatorCandidateCount;
            state.RuntimeCoreFrameBackbonePhaseCount = counters.PhaseCount;
            state.RuntimeCoreFrameBackboneContractOnlyPhaseCount = counters.ContractOnlyPhaseCount;
            state.RuntimeCoreFrameBackboneStreamCount = counters.StreamCount;
            state.RuntimeCoreFrameBackboneMigrationCarrierCount = counters.MigrationCarrierCount;
            state.RuntimeCoreFrameBackboneNativeStreamCandidateCount = counters.NativeStreamCandidateCount;
            state.RuntimeCoreFrameBackboneOwnerLocalBufferCandidateCount = counters.OwnerLocalBufferCandidateCount;
            state.RuntimeCoreFrameBackboneBattleHashStreamCount = counters.BattleHashStreamCount;
            state.RuntimeCoreFrameBackboneDeterministicMergePolicyCount = counters.DeterministicMergePolicyCount;
            state.RuntimeCoreFrameBackboneMergeCostMeasuredCount = counters.MergeCostMeasuredCount;
            state.RuntimeCoreFrameBackboneMergeCostMicroseconds = counters.MergeCostMicroseconds;
            state.RuntimeCoreFrameBackboneRequiredStructuralPlaybackCount = counters.RequiredStructuralPlaybackCount;
            state.RuntimeCoreFrameBackboneRecordedStructuralPlaybackCount = counters.RecordedStructuralPlaybackCount;
            state.RuntimeCoreFrameBackboneEcbCommandCount = counters.EcbCommandCount;
            state.RuntimeCoreFrameBackboneBulkQueryCount = counters.BulkQueryCount;
            state.RuntimeCoreFrameBackboneProfilerMarkerCount = counters.ProfilerMarkerCount;
            state.RuntimeCoreFrameBackboneJournalingMarkerCount = counters.JournalingMarkerCount;
            state.RuntimeCoreFrameBackboneCoreCostGroupCount = counters.CoreCostGroupCount;
            state.RuntimeCoreFrameBackbonePhysicsCostGroupCount = counters.PhysicsCostGroupCount;
            state.RuntimeCoreFrameBackboneRenderCostGroupCount = counters.RenderCostGroupCount;
            state.RuntimeCoreFrameBackboneRunnerCostGroupCount = counters.RunnerCostGroupCount;
            state.RuntimeCoreFrameBackbonePhysicsDisabledReasonCount = counters.PhysicsDisabledReasonCount;
            state.RuntimeCoreFrameBackboneRenderDisabledReasonCount = counters.RenderDisabledReasonCount;
            state.RuntimeCoreFrameBackboneDebuggerOverheadBudgetMicroseconds =
                counters.DebuggerOverheadBudgetMicroseconds;
            state.RuntimeCoreFrameBackboneSamplingInterval = counters.SamplingInterval;
            state.RuntimeCoreFrameBackboneDisablePolicyCount = counters.DisablePolicyCount;
            state.RuntimeCoreFrameBackboneBurstWarmupPolicyCount = counters.BurstWarmupPolicyCount;
            state.RuntimeCoreFrameBackboneHotPathManagedStringCount = counters.HotPathManagedStringCount;
            state.RuntimeCoreFrameBackboneEvidenceMask = counters.EvidenceMask;
        }

        private static GasRuntimeFrameBackboneDiagnosticCounters CreateFrameBackboneCounters(
            in GASRuntimeDebuggerComponent state)
        {
            return new GasRuntimeFrameBackboneDiagnosticCounters(
                state.RuntimeCoreFrameBackbonePhaseCount,
                state.RuntimeCoreFrameBackboneContractOnlyPhaseCount,
                state.RuntimeCoreQueryBudget,
                state.RuntimeCoreFilteredQueryBudget,
                state.RuntimeCoreUnfilteredQueryBudget,
                state.RuntimeCoreLookupUpdateBudget,
                state.RuntimeCoreRandomLookupBudget,
                state.RuntimeCoreSyncQueryBudget,
                state.RuntimeCoreDependencyWaitRiskCount,
                state.RuntimeCoreWorldUpdateAllocatorOwnerCount,
                state.RuntimeCoreRewindableAllocatorCandidateCount,
                state.RuntimeCoreFrameBackboneStreamCount,
                state.RuntimeCoreFrameBackboneMigrationCarrierCount,
                state.RuntimeCoreFrameBackboneNativeStreamCandidateCount,
                state.RuntimeCoreFrameBackboneOwnerLocalBufferCandidateCount,
                state.RuntimeCoreFrameBackboneBattleHashStreamCount,
                state.RuntimeCoreFrameBackboneDeterministicMergePolicyCount,
                state.RuntimeCoreFrameBackboneMergeCostMeasuredCount,
                state.RuntimeCoreFrameBackboneMergeCostMicroseconds,
                state.RuntimeCoreFrameBackboneRequiredStructuralPlaybackCount,
                state.RuntimeCoreFrameBackboneRecordedStructuralPlaybackCount,
                state.RuntimeCoreFrameBackboneEcbCommandCount,
                state.RuntimeCoreFrameBackboneBulkQueryCount,
                state.RuntimeCoreFrameBackboneProfilerMarkerCount,
                state.RuntimeCoreFrameBackboneJournalingMarkerCount,
                state.RuntimeCoreFrameBackboneCoreCostGroupCount,
                state.RuntimeCoreFrameBackbonePhysicsCostGroupCount,
                state.RuntimeCoreFrameBackboneRenderCostGroupCount,
                state.RuntimeCoreFrameBackboneRunnerCostGroupCount,
                state.RuntimeCoreFrameBackbonePhysicsDisabledReasonCount,
                state.RuntimeCoreFrameBackboneRenderDisabledReasonCount,
                state.RuntimeCoreFrameBackboneDebuggerOverheadBudgetMicroseconds,
                state.RuntimeCoreFrameBackboneSamplingInterval,
                state.RuntimeCoreFrameBackboneDisablePolicyCount,
                state.RuntimeCoreFrameBackboneBurstWarmupPolicyCount,
                state.RuntimeCoreFrameBackboneHotPathManagedStringCount,
                state.RuntimeCoreFrameBackboneEvidenceMask);
        }

        public static GasRuntimeDiagnosticSnapshot CreateSnapshot(EntityManager em, Entity debuggerEntity)
        {
            if (!CanUse(em, debuggerEntity))
            {
                return new GasRuntimeDiagnosticSnapshot(
                    new GasRuntimeDiagnosticStats(0, 0, 0, 0, 0, 0, 0, 0),
                    new GasRuntimeCoreDiagnosticCounters(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                    Array.Empty<GASRuntimeDiagnosticEventBuffer>());
            }

            var state = em.GetComponentData<GASRuntimeDebuggerComponent>(debuggerEntity);
            var log = em.GetBuffer<GASRuntimeDiagnosticEventBuffer>(debuggerEntity);
            var events = new GASRuntimeDiagnosticEventBuffer[log.Length];
            var warningCount = 0;
            var errorCount = 0;
            var slowSystemCount = 0;
            var bufferPressureWarningCount = 0;
            var observationMaterializationCounters = GasRuntimeObservationMaterializationCounters.Empty;

            for (var i = 0; i < log.Length; i++)
            {
                var evt = log[i];
                events[i] = evt;
                if (evt.Severity >= EGasRuntimeDiagnosticSeverity.Warning)
                    warningCount++;
                if (evt.Severity >= EGasRuntimeDiagnosticSeverity.Error)
                    errorCount++;
                if (evt.Kind == EGasRuntimeDiagnosticKind.SystemTiming
                    && evt.Severity >= EGasRuntimeDiagnosticSeverity.Warning)
                {
                    slowSystemCount++;
                }
                if (evt.Kind == EGasRuntimeDiagnosticKind.BufferPressure
                    && evt.Severity >= EGasRuntimeDiagnosticSeverity.Warning)
                {
                    bufferPressureWarningCount++;
                }
                if (evt.Kind == EGasRuntimeDiagnosticKind.ObservationMaterialization)
                {
                    observationMaterializationCounters = observationMaterializationCounters.Add(
                        new GasRuntimeObservationMaterializationCounters(
                            evt.ObservationMaterializedQueryCount,
                            evt.ObservationMaterializedEntityCount,
                            evt.ObservationMaterializationElapsedMicroseconds,
                            evt.ObservationActiveEffectStoreQueryCount,
                            evt.ObservationActiveEffectStoreEntityCount,
                            evt.ObservationPresentationOutboxQueryCount,
                            evt.ObservationPresentationOutboxEntityCount,
                            evt.ObservationPerformancePollutionRiskCount));
                }
            }

            return new GasRuntimeDiagnosticSnapshot(
                new GasRuntimeDiagnosticStats(
                    state.FirstRetainedSequence,
                    state.NextSequence,
                    state.DroppedEventCount,
                    log.Length,
                    warningCount,
                    errorCount,
                    slowSystemCount,
                    bufferPressureWarningCount),
                new GasRuntimeCoreDiagnosticCounters(
                    state.RuntimeCoreRequestCount,
                    state.RuntimeCoreSpecCount,
                    state.RuntimeCoreDeltaCount,
                    state.RuntimeCoreFactCount,
                    state.RuntimeCoreCueCount,
                    state.RuntimeCorePresentationCount,
                    state.RuntimeCoreEntityCreateCount,
                    state.RuntimeCoreEntityDestroyCount,
                    state.RuntimeCoreEcbPlaybackCount,
                    state.RuntimeCorePeakActiveEffectEntityCount,
                    state.RuntimeCorePeakPendingApplyCommandOwnerCount,
                    state.RuntimeCorePeakEventBusBufferLength,
                    state.RuntimeCorePeakPresentationCursorLag,
                    state.RuntimeCorePeakReplayCursorLag,
                    state.RuntimeCoreActiveEffectStoreOwnerCount,
                    state.RuntimeCoreActiveEffectSlotCount,
                    state.RuntimeCoreActiveEffectSlotCapacity,
                    state.RuntimeCoreActiveEffectSlotPendingApplyCount,
                    state.RuntimeCoreActiveEffectSlotActiveCount,
                    state.RuntimeCoreActiveEffectSlotInhibitedCount,
                    state.RuntimeCoreActiveEffectSlotPendingRemoveCount,
                    state.RuntimeCoreActiveEffectSlotLegacyBackedCount,
                    state.RuntimeCoreActiveEffectSlotExternalizedOwnerCount,
                    state.RuntimeCoreActiveEffectSlotGrantedTagCount,
                    state.RuntimeCoreActiveEffectSlotGrantedAbilityCount,
                    state.RuntimeCoreActiveEffectChunkSkipMatchedSlotCount,
                    state.RuntimeCoreActiveEffectChunkSkipSkippedSlotCount,
                    state.RuntimeCoreActiveEffectChunkSkipDuePeriodSlotCount,
                    state.RuntimeCoreActiveEffectChunkSkipNoopSlotCount,
                    state.RuntimeCoreActiveEffectChunkSkipOwnerCount,
                    state.RuntimeCoreActiveEffectGlobalIndexOwnerCount,
                    state.RuntimeCoreActiveEffectGlobalIndexCount,
                    state.RuntimeCoreActiveEffectGlobalIndexActiveCount,
                    state.RuntimeCoreActiveEffectGlobalIndexInhibitedCount,
                    state.RuntimeCoreActiveEffectGlobalIndexPendingRemoveCount,
                    state.RuntimeCoreActiveEffectGlobalIndexPeriodDueCount,
                    state.RuntimeCoreActiveEffectGlobalIndexDurationDueCount,
                    state.RuntimeCoreActiveEffectGlobalIndexStaleCount,
                    state.RuntimeCoreActiveEffectGlobalIndexBucketOwnerCount,
                    state.RuntimeCoreActiveEffectGlobalIndexBucketIndexCount,
                    state.RuntimeCoreActiveEffectGlobalIndexMaxBucketLength,
                    state.RuntimeCoreActiveEffectGlobalIndexStableRowCount,
                    state.RuntimeCoreActiveEffectGlobalIndexStaleStableRowCount,
                    state.RuntimeCoreActiveMutationCommandCount,
                    state.RuntimeCoreActiveMutationOwnerGroupCount,
                    state.RuntimeCoreActiveMutationMaxOwnerRange,
                    state.RuntimeCoreActiveMutationSortMoveCount,
                    state.RuntimeCoreActiveMutationEstimatedRandomLookupCount,
                    state.RuntimeCoreActiveMutationOwnerResourceLookupCount,
                    state.RuntimeCoreActiveMutationMigrationCarrierCount,
                    state.RuntimeCorePendingAttributeDeltaCount,
                    state.RuntimeCorePendingAttributeAppliedDeltaCount,
                    state.RuntimeCorePendingAttributeSkippedDeltaCount,
                    state.RuntimeCorePendingAttributeTargetGroupCount,
                    state.RuntimeCorePendingAttributeMaxTargetRange,
                    state.RuntimeCorePendingAttributeEstimatedRandomLookupCount,
                    state.RuntimeCorePendingAttributeFactPatchCount,
                    state.RuntimeCorePendingAttributeMigrationCarrierCount,
                    state.RuntimeCoreQueryBudget,
                    state.RuntimeCoreFilteredQueryBudget,
                    state.RuntimeCoreUnfilteredQueryBudget,
                    state.RuntimeCoreLookupUpdateBudget,
                    state.RuntimeCoreRandomLookupBudget,
                    state.RuntimeCoreSyncQueryBudget,
                    state.RuntimeCoreHelperTempQueryRiskCount,
                    state.RuntimeCoreDependencyWaitRiskCount,
                    state.RuntimeCoreWorldUpdateAllocatorOwnerCount,
                    state.RuntimeCoreRewindableAllocatorCandidateCount),
                CreateFrameBackboneCounters(state),
                observationMaterializationCounters,
                events);
        }

        public static string ExportToText(in GasRuntimeDiagnosticSnapshot snapshot, int maxEvents = 0)
        {
            var stats = snapshot.Stats;
            var builder = new StringBuilder(1024);
            builder.Append("runtimeDiagnostics|events=")
                .Append(stats.RetainedEventCount)
                .Append("|dropped=")
                .Append(stats.DroppedEventCount)
                .Append("|warnings=")
                .Append(stats.WarningCount)
                .Append("|errors=")
                .Append(stats.ErrorCount)
                .Append("|slowSystems=")
                .Append(stats.SlowSystemCount)
                .Append("|bufferPressureWarnings=")
                .Append(stats.BufferPressureWarningCount)
                .AppendLine();
            AppendRuntimeCoreCounters(builder, snapshot.CoreCounters);
            AppendRuntimeCoreFrameBackboneCounters(builder, snapshot.FrameBackboneCounters);
            AppendObservationMaterializationCounters(builder, snapshot.ObservationMaterializationCounters);

            var events = snapshot.Events ?? Array.Empty<GASRuntimeDiagnosticEventBuffer>();
            var count = maxEvents > 0 && maxEvents < events.Length ? maxEvents : events.Length;
            for (var i = 0; i < count; i++)
                AppendEventLine(builder, events[i]);

            return builder.ToString();
        }

        private static bool CanUse(EntityManager em, Entity debuggerEntity)
        {
            return debuggerEntity != Entity.Null
                   && em.Exists(debuggerEntity)
                   && em.HasComponent<GASRuntimeDebuggerComponent>(debuggerEntity)
                   && em.HasBuffer<GASRuntimeDiagnosticEventBuffer>(debuggerEntity);
        }

        private static void ReadEventBusCounters(
            EntityManager em,
            Entity eventBusEntity,
            out int gameplayEventCount,
            out int attributeChangeCount,
            out int cueRequestCount,
            out int tagChangeCount,
            out int damageEventCount,
            out int gameplayRequestFactCount,
            out int gameplayEffectInstancedCount,
            out int gameplayEffectRemovedCount)
        {
            gameplayEventCount = 0;
            attributeChangeCount = 0;
            cueRequestCount = 0;
            tagChangeCount = 0;
            damageEventCount = 0;
            gameplayRequestFactCount = 0;
            gameplayEffectInstancedCount = 0;
            gameplayEffectRemovedCount = 0;

            if (eventBusEntity == Entity.Null || !em.Exists(eventBusEntity))
                return;

            attributeChangeCount = GetBufferLength<AttributeChangeEventBuffer>(em, eventBusEntity);
            cueRequestCount = GetBufferLength<CueRequestBuffer>(em, eventBusEntity);
            tagChangeCount = GetBufferLength<TagChangeEventBuffer>(em, eventBusEntity);
        }

        private static void ReadEffectCommandSpecStreamCounters(
            EntityManager em,
            out int effectCommandCount,
            out int instantSpecCount,
            out int attributeDeltaCount,
            out int typedFactCount,
            out int activeMutationCommandCount,
            out int activeMutationOwnerGroupCount,
            out int activeMutationMaxOwnerRange,
            out int activeMutationSortMoveCount,
            out int activeMutationEstimatedRandomLookupCount,
            out int activeMutationOwnerResourceLookupCount,
            out int activeMutationMigrationCarrierCount,
            out int pendingAttributeDeltaCount,
            out int pendingAttributeAppliedDeltaCount,
            out int pendingAttributeSkippedDeltaCount,
            out int pendingAttributeTargetGroupCount,
            out int pendingAttributeMaxTargetRange,
            out int pendingAttributeEstimatedRandomLookupCount,
            out int pendingAttributeFactPatchCount,
            out int pendingAttributeMigrationCarrierCount)
        {
            effectCommandCount = 0;
            instantSpecCount = 0;
            attributeDeltaCount = 0;
            typedFactCount = 0;
            activeMutationCommandCount = 0;
            activeMutationOwnerGroupCount = 0;
            activeMutationMaxOwnerRange = 0;
            activeMutationSortMoveCount = 0;
            activeMutationEstimatedRandomLookupCount = 0;
            activeMutationOwnerResourceLookupCount = 0;
            activeMutationMigrationCarrierCount = 0;
            pendingAttributeDeltaCount = 0;
            pendingAttributeAppliedDeltaCount = 0;
            pendingAttributeSkippedDeltaCount = 0;
            pendingAttributeTargetGroupCount = 0;
            pendingAttributeMaxTargetRange = 0;
            pendingAttributeEstimatedRandomLookupCount = 0;
            pendingAttributeFactPatchCount = 0;
            pendingAttributeMigrationCarrierCount = 0;

            if (!EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity))
                return;

            effectCommandCount = GetBufferLength<GEEffectCommandBuffer>(em, streamEntity);
            instantSpecCount = GetBufferLength<GEEffectSpecBuffer>(em, streamEntity);
            attributeDeltaCount = GetBufferLength<AttributeModifierBuffer>(em, streamEntity);
            typedFactCount = GetBufferLength<GameplayEventBuffer>(em, streamEntity);
            if (!em.HasComponent<GEEffectCommandStreamComponent>(streamEntity))
                return;

            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            activeMutationCommandCount = stream.ActiveMutationCommandCount;
            activeMutationOwnerGroupCount = stream.ActiveMutationOwnerGroupCount;
            activeMutationMaxOwnerRange = stream.ActiveMutationMaxOwnerRange;
            activeMutationSortMoveCount = stream.ActiveMutationSortMoveCount;
            activeMutationEstimatedRandomLookupCount = stream.ActiveMutationEstimatedRandomLookupCount;
            activeMutationOwnerResourceLookupCount = stream.ActiveMutationOwnerResourceLookupCount;
            activeMutationMigrationCarrierCount = stream.ActiveMutationMigrationCarrierCount;
            pendingAttributeDeltaCount = stream.PendingAttributeDeltaCount;
            pendingAttributeAppliedDeltaCount = stream.PendingAttributeAppliedDeltaCount;
            pendingAttributeSkippedDeltaCount = stream.PendingAttributeSkippedDeltaCount;
            pendingAttributeTargetGroupCount = stream.PendingAttributeTargetGroupCount;
            pendingAttributeMaxTargetRange = stream.PendingAttributeMaxTargetRange;
            pendingAttributeEstimatedRandomLookupCount = stream.PendingAttributeEstimatedRandomLookupCount;
            pendingAttributeFactPatchCount = stream.PendingAttributeFactPatchCount;
            pendingAttributeMigrationCarrierCount = stream.PendingAttributeMigrationCarrierCount;
        }

        private static int CountTypedDamageFacts(EntityManager em)
        {
            if (!EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity)
                || !em.HasBuffer<GameplayEventBuffer>(streamEntity))
            {
                return 0;
            }

            var facts = em.GetBuffer<GameplayEventBuffer>(streamEntity);
            var count = 0;
            for (var i = 0; i < facts.Length; i++)
            {
                if (facts[i].Domain == EGameplayFactDomain.Damage)
                    count++;
            }

            return count;
        }

        private static void ReadActiveEffectStoreCounters(
            EntityManager em,
            EntityQuery activeEffectStoreQuery,
            int currentFrame,
            out int ownerCount,
            out int slotCount,
            out int slotCapacity,
            out int pendingApplyCount,
            out int activeCount,
            out int inhibitedCount,
            out int pendingRemoveCount,
            out int legacyBackedCount,
            out int externalizedOwnerCount,
            out int grantedTagCount,
            out int grantedAbilityCount,
            out int chunkSkipMatchedSlotCount,
            out int chunkSkipSkippedSlotCount,
            out int chunkSkipDuePeriodSlotCount,
            out int chunkSkipNoopSlotCount,
            out int chunkSkipOwnerCount,
            ref GasRuntimeObservationMaterializationCounters observationCounters)
        {
            ownerCount = 0;
            slotCount = 0;
            slotCapacity = 0;
            pendingApplyCount = 0;
            activeCount = 0;
            inhibitedCount = 0;
            pendingRemoveCount = 0;
            legacyBackedCount = 0;
            externalizedOwnerCount = 0;
            grantedTagCount = 0;
            grantedAbilityCount = 0;
            chunkSkipMatchedSlotCount = 0;
            chunkSkipSkippedSlotCount = 0;
            chunkSkipDuePeriodSlotCount = 0;
            chunkSkipNoopSlotCount = 0;
            chunkSkipOwnerCount = 0;

            var materializationStart = Stopwatch.GetTimestamp();
            using var owners = activeEffectStoreQuery.ToEntityArray(Allocator.Temp);
            observationCounters = observationCounters.AddActiveEffectStoreMaterialization(
                owners.Length,
                ToMicroseconds(Stopwatch.GetTimestamp() - materializationStart));
            ownerCount = owners.Length;

            for (var ownerIndex = 0; ownerIndex < owners.Length; ownerIndex++)
            {
                var slots = em.GetBuffer<ActiveGameplayEffectBuffer>(owners[ownerIndex]);
                slotCount += slots.Length;
                slotCapacity += slots.Capacity;
                if (slots.Capacity > ActiveEffectStore.InlineSlotCapacity)
                    externalizedOwnerCount++;

                var chunkSkip = ActiveEffectStore.CreateChunkSkipIndexSnapshot(slots, currentFrame);
                chunkSkipMatchedSlotCount += chunkSkip.MatchedSlotCount;
                chunkSkipSkippedSlotCount += chunkSkip.SkippedSlotCount;
                chunkSkipDuePeriodSlotCount += chunkSkip.DuePeriodSlotCount;
                chunkSkipNoopSlotCount += chunkSkip.NoopSlotCount;
                if (chunkSkip.CanSkipOwner)
                    chunkSkipOwnerCount++;

                for (var slotIndex = 0; slotIndex < slots.Length; slotIndex++)
                {
                    var slot = slots[slotIndex];
                    switch (slot.State)
                    {
                        case ActiveEffectSlotState.PendingApply:
                            pendingApplyCount++;
                            break;
                        case ActiveEffectSlotState.Active:
                            activeCount++;
                            break;
                        case ActiveEffectSlotState.Inhibited:
                            inhibitedCount++;
                            break;
                        case ActiveEffectSlotState.PendingRemove:
                            pendingRemoveCount++;
                            break;
                    }

                    if ((slot.Flags & (int)ActiveEffectSlotFlags.LegacyEntityBacked) != 0)
                        legacyBackedCount++;

                    grantedTagCount += slot.ActiveGrantedTagCount;
                    grantedAbilityCount += slot.ActiveGrantedAbilityCount;
                }
            }
        }

        private static void ReadActiveEffectGlobalIndexCounters(
            EntityManager em,
            int currentFrame,
            out int ownerCount,
            out int indexCount,
            out int activeCount,
            out int inhibitedCount,
            out int pendingRemoveCount,
            out int periodDueCount,
            out int durationDueCount,
            out int staleCount,
            out int bucketOwnerCount,
            out int bucketIndexCount,
            out int maxBucketLength,
            out int stableRowCount,
            out int staleStableRowCount)
        {
            ownerCount = 0;
            indexCount = 0;
            activeCount = 0;
            inhibitedCount = 0;
            pendingRemoveCount = 0;
            periodDueCount = 0;
            durationDueCount = 0;
            staleCount = 0;
            bucketOwnerCount = 0;
            bucketIndexCount = 0;
            maxBucketLength = 0;
            stableRowCount = 0;
            staleStableRowCount = 0;

            if (!ActiveEffectStore.TryGetGlobalIndexStore(em, out var indexOwner)
                || indexOwner == Entity.Null
                || !em.Exists(indexOwner)
                || !em.HasBuffer<ActiveGameplayEffectGlobalIndexBuffer>(indexOwner))
            {
                return;
            }

            ownerCount = 1;
            if (em.HasComponent<ActiveGameplayEffectGlobalIndexComponent>(indexOwner))
            {
                var store = em.GetComponentData<ActiveGameplayEffectGlobalIndexComponent>(indexOwner);
                stableRowCount = store.IndexedStableRowCount;
                staleStableRowCount = store.StaleStableRowCount;
            }

            var indices = em.GetBuffer<ActiveGameplayEffectGlobalIndexBuffer>(indexOwner);
            indexCount = indices.Length;
            for (var i = 0; i < indices.Length; i++)
            {
                var index = indices[i];
                if (index.ActiveEffectEntity == Entity.Null
                    || index.OwnerAsc == Entity.Null
                    || !em.Exists(index.ActiveEffectEntity)
                    || !em.Exists(index.OwnerAsc))
                {
                    staleCount++;
                }

                switch (index.State)
                {
                    case ActiveEffectSlotState.Active:
                        activeCount++;
                        break;
                    case ActiveEffectSlotState.Inhibited:
                        inhibitedCount++;
                        break;
                    case ActiveEffectSlotState.PendingRemove:
                        pendingRemoveCount++;
                        break;
                }

                if (index.State == ActiveEffectSlotState.Active
                    && index.PeriodDueFrame > 0
                    && currentFrame >= index.PeriodDueFrame)
                {
                    periodDueCount++;
                }

                if ((index.State == ActiveEffectSlotState.Active
                    || ((index.SlotFlags & (int)ActiveEffectSlotFlags.TicksWhenInactive) != 0
                        && index.State == ActiveEffectSlotState.Inhibited))
                    && index.DurationDueFrame > 0
                    && currentFrame >= index.DurationDueFrame)
                {
                    durationDueCount++;
                }
            }

            if (!em.HasBuffer<ActiveGameplayEffectGlobalIndexBucketOwnerBuffer>(indexOwner))
                return;

            var bucketOwners = em.GetBuffer<ActiveGameplayEffectGlobalIndexBucketOwnerBuffer>(indexOwner);
            for (var i = 0; i < bucketOwners.Length; i++)
            {
                var bucketOwnerRef = bucketOwners[i];
                var bucketOwner = bucketOwnerRef.BucketOwner;
                if (bucketOwner == Entity.Null
                    || !em.Exists(bucketOwner)
                    || !em.HasComponent<ActiveGameplayEffectGlobalIndexBucketComponent>(bucketOwner)
                    || !em.HasBuffer<ActiveGameplayEffectGlobalIndexBuffer>(bucketOwner))
                {
                    continue;
                }

                var bucket = em.GetComponentData<ActiveGameplayEffectGlobalIndexBucketComponent>(bucketOwner);
                if (bucket.RootOwner != indexOwner
                    || bucket.BucketIndex != bucketOwnerRef.BucketIndex
                    || bucket.BucketCount != ActiveEffectStore.GlobalIndexBucketCount)
                {
                    continue;
                }

                var bucketLength = em.GetBuffer<ActiveGameplayEffectGlobalIndexBuffer>(bucketOwner).Length;
                bucketOwnerCount++;
                bucketIndexCount += bucketLength;
                if (bucketLength > maxBucketLength)
                    maxBucketLength = bucketLength;
            }
        }

        private static int GetBufferLength<T>(EntityManager em, Entity entity)
            where T : unmanaged, IBufferElementData
        {
            return entity != Entity.Null && em.Exists(entity) && em.HasBuffer<T>(entity)
                ? em.GetBuffer<T>(entity).Length
                : 0;
        }

        private static int ToMicroseconds(long stopwatchTicks)
        {
            if (stopwatchTicks <= 0)
                return 0;

            var microseconds = stopwatchTicks * 1000000d / Stopwatch.Frequency;
            return microseconds >= int.MaxValue ? int.MaxValue : (int)Math.Ceiling(microseconds);
        }

        private static int CountPresentationOutboxEvents(
            EntityManager em,
            Entity eventBusEntity,
            EntityQuery presentationOutboxQuery,
            ref GasRuntimeObservationMaterializationCounters observationCounters)
        {
            if (eventBusEntity != Entity.Null
                && em.Exists(eventBusEntity)
                && em.HasBuffer<PresentationOutboxOwnerBuffer>(eventBusEntity))
            {
                var owners = em.GetBuffer<PresentationOutboxOwnerBuffer>(eventBusEntity);
                var count = 0;
                for (var i = 0; i < owners.Length; i++)
                {
                    var asc = owners[i].ASC;
                    if (asc != Entity.Null && em.Exists(asc) && em.HasBuffer<PresentationEventBuffer>(asc))
                        count += em.GetBuffer<PresentationEventBuffer>(asc).Length;
                }

                return count;
            }

            var materializationStart = Stopwatch.GetTimestamp();
            using var entities = presentationOutboxQuery.ToEntityArray(Allocator.Temp);
            observationCounters = observationCounters.AddPresentationOutboxMaterialization(
                entities.Length,
                ToMicroseconds(Stopwatch.GetTimestamp() - materializationStart));
            var fallbackCount = 0;
            for (var i = 0; i < entities.Length; i++)
                fallbackCount += em.GetBuffer<PresentationEventBuffer>(entities[i]).Length;

            return fallbackCount;
        }

        private static int CalculatePresentationCursorLag(
            EntityManager em,
            Entity eventBusEntity,
            int currentFrame,
            int typedFactCount)
        {
            if (eventBusEntity == Entity.Null
                || !em.Exists(eventBusEntity)
                || !em.HasComponent<PresentationOutboxProjectionStateComponent>(eventBusEntity))
            {
                return 0;
            }

            var projectionState = em.GetComponentData<PresentationOutboxProjectionStateComponent>(eventBusEntity);
            var sameFrame = projectionState.LastProjectedFrame == currentFrame;
            return PositiveLag(
                typedFactCount,
                sameFrame ? projectionState.ProcessedTypedFactCount : 0);
        }

        private static int CalculateReplayCursorLag(
            EntityManager em,
            Entity eventLogSinkEntity,
            int currentFrame,
            int typedFactCount)
        {
            if (eventLogSinkEntity == Entity.Null
                || !em.Exists(eventLogSinkEntity)
                || !em.HasComponent<GameplayEventLogSinkComponent>(eventLogSinkEntity))
            {
                return 0;
            }

            var sinkState = em.GetComponentData<GameplayEventLogSinkComponent>(eventLogSinkEntity);
            var sameFrame = sinkState.LastProjectedFrame == currentFrame;
            return PositiveLag(
                typedFactCount,
                sameFrame ? sinkState.ProcessedTypedFactCount : 0);
        }

        private static int PositiveLag(int count, int processedCount)
        {
            return processedCount < count ? count - processedCount : 0;
        }

        private static bool TryGetWritableLog(
            EntityManager em,
            Entity debuggerEntity,
            out GASRuntimeDebuggerComponent state,
            out DynamicBuffer<GASRuntimeDiagnosticEventBuffer> log)
        {
            state = default;
            log = default;
            if (!CanUse(em, debuggerEntity))
                return false;

            state = em.GetComponentData<GASRuntimeDebuggerComponent>(debuggerEntity);
            if (state.Enabled == 0)
                return false;

            log = em.GetBuffer<GASRuntimeDiagnosticEventBuffer>(debuggerEntity);
            return true;
        }

        private static void AppendGroupTiming(
            DynamicBuffer<GASRuntimeDiagnosticEventBuffer> log,
            ref GASRuntimeDebuggerComponent state,
            int frame,
            string groupName,
            EGasRuntimeDiagnosticModule module,
            long groupTicks,
            long totalTicks,
            long stopwatchFrequency)
        {
            if (groupTicks <= 0)
                return;

            var elapsedMicroseconds = ToMicroseconds(groupTicks, stopwatchFrequency);
            Append(
                log,
                ref state,
                new GASRuntimeDiagnosticEventBuffer
                {
                    Frame = frame,
                    Kind = EGasRuntimeDiagnosticKind.GroupTiming,
                    Severity = EGasRuntimeDiagnosticSeverity.Trace,
                    Module = module,
                    GroupName = groupName,
                    ElapsedMicroseconds = elapsedMicroseconds,
                    TotalMicroseconds = ToMicroseconds(totalTicks, stopwatchFrequency),
                    Ratio = totalTicks > 0 ? (float)((double)groupTicks / totalTicks) : 0f,
                });
        }

        private static void RecordBufferPressure<T>(
            EntityManager em,
            Entity entity,
            string bufferName,
            int frame,
            DynamicBuffer<GASRuntimeDiagnosticEventBuffer> log,
            ref GASRuntimeDebuggerComponent state)
            where T : unmanaged, IBufferElementData
        {
            if (!em.HasBuffer<T>(entity))
                return;

            var buffer = em.GetBuffer<T>(entity);
            if (buffer.Length == 0 && buffer.Capacity > 0)
                return;

            var ratio = buffer.Capacity > 0
                ? (float)buffer.Length / buffer.Capacity
                : 1f;
            var severity = SeverityForPressure(
                buffer.Length,
                buffer.Capacity,
                state.BufferPressureWarningPermille,
                state.BufferPressureErrorPermille);

            Append(
                log,
                ref state,
                new GASRuntimeDiagnosticEventBuffer
                {
                    Frame = frame,
                    Kind = EGasRuntimeDiagnosticKind.BufferPressure,
                    Severity = severity,
                    Module = EGasRuntimeDiagnosticModule.EventBus,
                    BufferName = bufferName,
                    Entity = entity,
                    Count = buffer.Length,
                    Capacity = buffer.Capacity,
                    Ratio = ratio,
                });
        }

        private static void RecordFrameStreamBufferPressure<T>(
            EntityManager em,
            Entity entity,
            string bufferName,
            EGasRuntimeFrameStreamId streamId,
            in GASRuntimeFrameStreamOwnerPlan streamPlan,
            int frame,
            DynamicBuffer<GASRuntimeDiagnosticEventBuffer> log,
            ref GASRuntimeDebuggerComponent state)
            where T : unmanaged, IBufferElementData
        {
            if (!em.HasBuffer<T>(entity))
                return;

            var buffer = em.GetBuffer<T>(entity);
            if (buffer.Length == 0)
                return;

            var budget = 0;
            if (streamPlan.TryFind(streamId, out var entry))
                budget = entry.InternalBufferCapacity;

            var overBudget = budget > 0 && buffer.Length > budget;
            var ratio = budget > 0
                ? (float)buffer.Length / budget
                : buffer.Capacity > 0
                    ? (float)buffer.Length / buffer.Capacity
                    : 1f;

            Append(
                log,
                ref state,
                new GASRuntimeDiagnosticEventBuffer
                {
                    Frame = frame,
                    Kind = EGasRuntimeDiagnosticKind.BufferPressure,
                    Severity = overBudget
                        ? EGasRuntimeDiagnosticSeverity.Warning
                        : EGasRuntimeDiagnosticSeverity.Trace,
                    Module = EGasRuntimeDiagnosticModule.Effect,
                    GroupName = "EffectCommandSpecStream",
                    BufferName = bufferName,
                    Entity = entity,
                    Count = buffer.Length,
                    Capacity = buffer.Capacity,
                    ValueA = budget,
                    ValueB = overBudget ? 1 : 0,
                    Ratio = ratio,
                });
        }

        private static void Append(
            DynamicBuffer<GASRuntimeDiagnosticEventBuffer> log,
            ref GASRuntimeDebuggerComponent state,
            GASRuntimeDiagnosticEventBuffer evt)
        {
            evt.Sequence = state.NextSequence;
            state.NextSequence++;
            log.Add(evt);
        }

        private static void ApplyRetention(
            DynamicBuffer<GASRuntimeDiagnosticEventBuffer> log,
            ref GASRuntimeDebuggerComponent state)
        {
            if (state.MaxRetainedEvents <= 0 || log.Length <= state.MaxRetainedEvents)
                return;

            var removeCount = log.Length - state.MaxRetainedEvents;
            log.RemoveRange(0, removeCount);
            state.FirstRetainedSequence = log.Length > 0 ? log[0].Sequence : state.NextSequence;
            state.DroppedEventCount += removeCount;
        }

        private static EGasRuntimeDiagnosticSeverity SeverityForElapsed(int elapsedMicroseconds, int warningThreshold)
        {
            if (warningThreshold <= 0 || elapsedMicroseconds < warningThreshold)
                return EGasRuntimeDiagnosticSeverity.Trace;

            return EGasRuntimeDiagnosticSeverity.Warning;
        }

        private static EGasRuntimeDiagnosticSeverity SeverityForPressure(
            int count,
            int capacity,
            int warningPermille,
            int errorPermille)
        {
            if (count <= 0 || capacity <= 0)
                return EGasRuntimeDiagnosticSeverity.Trace;

            var permille = count * 1000 / capacity;
            if (errorPermille > 0 && permille >= errorPermille)
                return EGasRuntimeDiagnosticSeverity.Error;
            if (warningPermille > 0 && permille >= warningPermille)
                return EGasRuntimeDiagnosticSeverity.Warning;
            return EGasRuntimeDiagnosticSeverity.Trace;
        }

        private static int ToMicroseconds(long ticks, long stopwatchFrequency)
        {
            if (ticks <= 0 || stopwatchFrequency <= 0)
                return 0;

            var microseconds = ticks * 1000000d / stopwatchFrequency;
            if (microseconds >= int.MaxValue)
                return int.MaxValue;

            return (int)Math.Round(microseconds);
        }

        private static EGasRuntimeDiagnosticModule ModuleFromGroup(string groupName)
        {
            return groupName switch
            {
                "Command" => EGasRuntimeDiagnosticModule.Command,
                "ResetDirty" => EGasRuntimeDiagnosticModule.ResetDirty,
                "Tag" => EGasRuntimeDiagnosticModule.Tag,
                "Effect" => EGasRuntimeDiagnosticModule.Effect,
                "Attribute" => EGasRuntimeDiagnosticModule.Attribute,
                "Ability" => EGasRuntimeDiagnosticModule.Ability,
                "Cue" => EGasRuntimeDiagnosticModule.Cue,
                _ => EGasRuntimeDiagnosticModule.Runtime,
            };
        }

        private static void AppendEventLine(StringBuilder builder, in GASRuntimeDiagnosticEventBuffer evt)
        {
            builder.Append("runtimeDiagnostic|seq=")
                .Append(evt.Sequence)
                .Append("|frame=")
                .Append(evt.Frame)
                .Append("|kind=")
                .Append(evt.Kind)
                .Append("|severity=")
                .Append(evt.Severity)
                .Append("|module=")
                .Append(evt.Module);

            if (evt.GroupName.Length > 0)
                builder.Append("|group=").Append(evt.GroupName);
            if (evt.SystemName.Length > 0)
                builder.Append("|system=").Append(evt.SystemName);
            if (evt.BufferName.Length > 0)
                builder.Append("|buffer=").Append(evt.BufferName);
            if (evt.Kind == EGasRuntimeDiagnosticKind.RuntimeCoreCounters)
            {
                builder.Append("|requests=")
                    .Append(evt.Count)
                    .Append("|specs=")
                    .Append(evt.ValueA)
                    .Append("|deltas=")
                    .Append(evt.ValueB)
                    .Append("|facts=")
                    .Append(evt.Capacity)
                    .Append("|cues=")
                    .Append(evt.CallCount)
                    .Append("|presentation=")
                    .Append(evt.TotalMicroseconds)
                    .Append("|entityCreates=")
                    .Append(evt.EntityCreateCount)
                    .Append("|entityDestroys=")
                    .Append(evt.EntityDestroyCount)
                    .Append("|ecbPlaybacks=")
                    .Append(evt.EcbPlaybackCount)
                    .Append("|activeEffectEntities=")
                    .Append(evt.ActiveEffectEntityCount)
                    .Append("|pendingApplyCommandOwners=")
                    .Append(evt.PendingApplyCommandOwnerCount)
                    .Append("|eventBusBufferLength=")
                    .Append(evt.EventBusBufferLength)
                    .Append("|presentationCursorLag=")
                    .Append(evt.PresentationCursorLag)
                    .Append("|replayCursorLag=")
                    .Append(evt.ReplayCursorLag)
                    .Append("|activeEffectStoreOwners=")
                    .Append(evt.ActiveEffectStoreOwnerCount)
                    .Append("|activeEffectSlots=")
                    .Append(evt.ActiveEffectSlotCount)
                    .Append("|activeEffectSlotCapacity=")
                    .Append(evt.ActiveEffectSlotCapacity)
                    .Append("|activeEffectSlotPendingApply=")
                    .Append(evt.ActiveEffectSlotPendingApplyCount)
                    .Append("|activeEffectSlotActive=")
                    .Append(evt.ActiveEffectSlotActiveCount)
                    .Append("|activeEffectSlotInhibited=")
                    .Append(evt.ActiveEffectSlotInhibitedCount)
                    .Append("|activeEffectSlotPendingRemove=")
                    .Append(evt.ActiveEffectSlotPendingRemoveCount)
                    .Append("|activeEffectSlotLegacyBacked=")
                    .Append(evt.ActiveEffectSlotLegacyBackedCount)
                    .Append("|activeEffectSlotExternalizedOwners=")
                    .Append(evt.ActiveEffectSlotExternalizedOwnerCount)
                    .Append("|activeEffectSlotGrantedTags=")
                    .Append(evt.ActiveEffectSlotGrantedTagCount)
                    .Append("|activeEffectSlotGrantedAbilities=")
                    .Append(evt.ActiveEffectSlotGrantedAbilityCount)
                    .Append("|activeEffectChunkSkipMatchedSlots=")
                    .Append(evt.ActiveEffectChunkSkipMatchedSlotCount)
                    .Append("|activeEffectChunkSkipSkippedSlots=")
                    .Append(evt.ActiveEffectChunkSkipSkippedSlotCount)
                    .Append("|activeEffectChunkSkipDuePeriodSlots=")
                    .Append(evt.ActiveEffectChunkSkipDuePeriodSlotCount)
                    .Append("|activeEffectChunkSkipNoopSlots=")
                    .Append(evt.ActiveEffectChunkSkipNoopSlotCount)
                .Append("|activeEffectChunkSkipOwners=")
                .Append(evt.ActiveEffectChunkSkipOwnerCount)
                .Append("|activeEffectGlobalIndexOwners=")
                .Append(evt.ActiveEffectGlobalIndexOwnerCount)
                .Append("|activeEffectGlobalIndexCount=")
                .Append(evt.ActiveEffectGlobalIndexCount)
                .Append("|activeEffectGlobalIndexActive=")
                .Append(evt.ActiveEffectGlobalIndexActiveCount)
                .Append("|activeEffectGlobalIndexInhibited=")
                .Append(evt.ActiveEffectGlobalIndexInhibitedCount)
                .Append("|activeEffectGlobalIndexPendingRemove=")
                .Append(evt.ActiveEffectGlobalIndexPendingRemoveCount)
                .Append("|activeEffectGlobalIndexPeriodDue=")
                .Append(evt.ActiveEffectGlobalIndexPeriodDueCount)
                .Append("|activeEffectGlobalIndexDurationDue=")
                .Append(evt.ActiveEffectGlobalIndexDurationDueCount)
                .Append("|activeEffectGlobalIndexStale=")
                .Append(evt.ActiveEffectGlobalIndexStaleCount)
                .Append("|activeEffectGlobalIndexBucketOwners=")
                .Append(evt.ActiveEffectGlobalIndexBucketOwnerCount)
                .Append("|activeEffectGlobalIndexBucketIndexCount=")
                .Append(evt.ActiveEffectGlobalIndexBucketIndexCount)
                .Append("|activeEffectGlobalIndexMaxBucketLength=")
                .Append(evt.ActiveEffectGlobalIndexMaxBucketLength)
                .Append("|activeEffectGlobalIndexStableRows=")
                .Append(evt.ActiveEffectGlobalIndexStableRowCount)
                .Append("|activeEffectGlobalIndexStaleStableRows=")
                .Append(evt.ActiveEffectGlobalIndexStaleStableRowCount)
                .Append("|activeMutationCommands=")
                .Append(evt.ActiveMutationCommandCount)
                .Append("|activeMutationOwnerGroups=")
                .Append(evt.ActiveMutationOwnerGroupCount)
                .Append("|activeMutationMaxOwnerRange=")
                .Append(evt.ActiveMutationMaxOwnerRange)
                .Append("|activeMutationSortMoves=")
                .Append(evt.ActiveMutationSortMoveCount)
                .Append("|activeMutationEstimatedRandomLookups=")
                .Append(evt.ActiveMutationEstimatedRandomLookupCount)
                .Append("|activeMutationOwnerResourceLookups=")
                .Append(evt.ActiveMutationOwnerResourceLookupCount)
                .Append("|activeMutationMigrationCarriers=")
                .Append(evt.ActiveMutationMigrationCarrierCount)
                .Append("|pendingAttributeDeltas=")
                .Append(evt.PendingAttributeDeltaCount)
                .Append("|pendingAttributeAppliedDeltas=")
                .Append(evt.PendingAttributeAppliedDeltaCount)
                .Append("|pendingAttributeSkippedDeltas=")
                .Append(evt.PendingAttributeSkippedDeltaCount)
                .Append("|pendingAttributeTargetGroups=")
                .Append(evt.PendingAttributeTargetGroupCount)
                .Append("|pendingAttributeMaxTargetRange=")
                .Append(evt.PendingAttributeMaxTargetRange)
                .Append("|pendingAttributeEstimatedRandomLookups=")
                .Append(evt.PendingAttributeEstimatedRandomLookupCount)
                .Append("|pendingAttributeFactPatches=")
                .Append(evt.PendingAttributeFactPatchCount)
                .Append("|pendingAttributeMigrationCarriers=")
                .Append(evt.PendingAttributeMigrationCarrierCount)
                .Append("|queryBudget=")
                .Append(evt.QueryBudget)
                    .Append("|filteredQueryBudget=")
                    .Append(evt.FilteredQueryBudget)
                    .Append("|unfilteredQueryBudget=")
                    .Append(evt.UnfilteredQueryBudget)
                    .Append("|lookupUpdateBudget=")
                    .Append(evt.LookupUpdateBudget)
                    .Append("|randomLookupBudget=")
                    .Append(evt.RandomLookupBudget)
                    .Append("|syncQueryBudget=")
                    .Append(evt.SyncQueryBudget)
                    .Append("|helperTempQueryRisks=")
                    .Append(evt.HelperTempQueryRiskCount)
                    .Append("|dependencyWaitRisks=")
                    .Append(evt.DependencyWaitRiskCount)
                    .Append("|worldUpdateAllocatorOwners=")
                    .Append(evt.WorldUpdateAllocatorOwnerCount)
                    .Append("|rewindableAllocatorCandidates=")
                    .Append(evt.RewindableAllocatorCandidateCount);
                builder.AppendLine();
                return;
            }
            if (evt.Kind == EGasRuntimeDiagnosticKind.RuntimeCoreFrameBackbone)
            {
                builder.Append("|phases=")
                    .Append(evt.FrameBackbonePhaseCount)
                    .Append("|contractOnlyPhases=")
                    .Append(evt.FrameBackboneContractOnlyPhaseCount)
                    .Append("|queryBudget=")
                    .Append(evt.QueryBudget)
                    .Append("|lookupUpdateBudget=")
                    .Append(evt.LookupUpdateBudget)
                    .Append("|dependencyWaitRisks=")
                    .Append(evt.DependencyWaitRiskCount)
                    .Append("|streams=")
                    .Append(evt.FrameBackboneStreamCount)
                    .Append("|migrationCarriers=")
                    .Append(evt.FrameBackboneMigrationCarrierCount)
                    .Append("|nativeStreamCandidates=")
                    .Append(evt.FrameBackboneNativeStreamCandidateCount)
                    .Append("|battleHashStreams=")
                    .Append(evt.FrameBackboneBattleHashStreamCount)
                    .Append("|deterministicMergePolicies=")
                    .Append(evt.FrameBackboneDeterministicMergePolicyCount)
                    .Append("|mergeCostUs=")
                    .Append(evt.FrameBackboneMergeCostMicroseconds)
                    .Append("|mergeCostMeasured=")
                    .Append(evt.FrameBackboneMergeCostMeasuredCount)
                    .Append("|requiredStructuralPlaybacks=")
                    .Append(evt.FrameBackboneRequiredStructuralPlaybackCount)
                    .Append("|recordedStructuralPlaybacks=")
                    .Append(evt.FrameBackboneRecordedStructuralPlaybackCount)
                    .Append("|ecbCommands=")
                    .Append(evt.FrameBackboneEcbCommandCount)
                    .Append("|bulkQueries=")
                    .Append(evt.FrameBackboneBulkQueryCount)
                    .Append("|profilerMarkers=")
                    .Append(evt.FrameBackboneProfilerMarkerCount)
                    .Append("|journalingMarkers=")
                    .Append(evt.FrameBackboneJournalingMarkerCount)
                    .Append("|coreCostGroups=")
                    .Append(evt.FrameBackboneCoreCostGroupCount)
                    .Append("|physicsCostGroups=")
                    .Append(evt.FrameBackbonePhysicsCostGroupCount)
                    .Append("|renderCostGroups=")
                    .Append(evt.FrameBackboneRenderCostGroupCount)
                    .Append("|runnerCostGroups=")
                    .Append(evt.FrameBackboneRunnerCostGroupCount)
                    .Append("|overheadBudgetUs=")
                    .Append(evt.FrameBackboneDebuggerOverheadBudgetMicroseconds)
                    .Append("|samplingInterval=")
                    .Append(evt.FrameBackboneSamplingInterval)
                    .Append("|disablePolicy=")
                    .Append(evt.FrameBackboneDisablePolicyCount)
                    .Append("|hotPathManagedStrings=")
                    .Append(evt.FrameBackboneHotPathManagedStringCount);
                builder.AppendLine();
                return;
            }
            if (evt.Kind == EGasRuntimeDiagnosticKind.ObservationMaterialization)
            {
                builder.Append("|materializedQueries=")
                    .Append(evt.ObservationMaterializedQueryCount)
                    .Append("|materializedEntities=")
                    .Append(evt.ObservationMaterializedEntityCount)
                    .Append("|elapsedUs=")
                    .Append(evt.ObservationMaterializationElapsedMicroseconds)
                    .Append("|activeEffectStoreQueries=")
                    .Append(evt.ObservationActiveEffectStoreQueryCount)
                    .Append("|activeEffectStoreEntities=")
                    .Append(evt.ObservationActiveEffectStoreEntityCount)
                    .Append("|presentationOutboxQueries=")
                    .Append(evt.ObservationPresentationOutboxQueryCount)
                    .Append("|presentationOutboxEntities=")
                    .Append(evt.ObservationPresentationOutboxEntityCount)
                    .Append("|performancePollutionRisks=")
                    .Append(evt.ObservationPerformancePollutionRiskCount);
                builder.AppendLine();
                return;
            }
            if (evt.Kind == EGasRuntimeDiagnosticKind.StructuralChange)
            {
                if (evt.EcbPlaybackCount != 0)
                    builder.Append("|ecbPlaybacks=").Append(evt.EcbPlaybackCount);
                if (evt.ValueA != 0)
                    builder.Append("|ecbCommands=").Append(evt.ValueA);
                if (evt.ValueB != 0)
                    builder.Append("|bulkQueries=").Append(evt.ValueB);
                builder.AppendLine();
                return;
            }
            if (evt.ElapsedMicroseconds != 0)
                builder.Append("|elapsedUs=").Append(evt.ElapsedMicroseconds);
            if (evt.TotalMicroseconds != 0)
                builder.Append("|totalUs=").Append(evt.TotalMicroseconds);
            if (evt.CallCount != 0)
                builder.Append("|calls=").Append(evt.CallCount);
            if (evt.Capacity != 0 || evt.Count != 0)
            {
                builder.Append("|count=")
                    .Append(evt.Count)
                    .Append("|capacity=")
                    .Append(evt.Capacity)
                    .Append("|ratio=")
                    .Append(evt.Ratio.ToString("G9", System.Globalization.CultureInfo.InvariantCulture));
            }
            if (evt.Kind == EGasRuntimeDiagnosticKind.BufferPressure
                && evt.ValueA > 0)
            {
                builder.Append("|budget=")
                    .Append(evt.ValueA)
                    .Append("|overBudget=")
                    .Append(evt.ValueB);
            }

            builder.AppendLine();
        }

        private static void AppendRuntimeCoreCounters(
            StringBuilder builder,
            in GasRuntimeCoreDiagnosticCounters counters)
        {
            builder.Append("runtimeCoreCounters|requests=")
                .Append(counters.RequestCount)
                .Append("|specs=")
                .Append(counters.SpecCount)
                .Append("|deltas=")
                .Append(counters.DeltaCount)
                .Append("|facts=")
                .Append(counters.FactCount)
                .Append("|cues=")
                .Append(counters.CueCount)
                .Append("|presentation=")
                .Append(counters.PresentationCount)
                .Append("|entityCreates=")
                .Append(counters.EntityCreateCount)
                .Append("|entityDestroys=")
                .Append(counters.EntityDestroyCount)
                .Append("|ecbPlaybacks=")
                .Append(counters.EcbPlaybackCount)
                .AppendLine();
            builder.Append("runtimeCoreCountersPeak|activeEffectEntities=")
                .Append(counters.PeakActiveEffectEntityCount)
                .Append("|pendingApplyCommandOwners=")
                .Append(counters.PeakPendingApplyCommandOwnerCount)
                .Append("|eventBusBufferLength=")
                .Append(counters.PeakEventBusBufferLength)
                .Append("|presentationCursorLag=")
                .Append(counters.PeakPresentationCursorLag)
                .Append("|replayCursorLag=")
                .Append(counters.PeakReplayCursorLag)
                .AppendLine();
            builder.Append("runtimeCoreActiveEffectStore|owners=")
                .Append(counters.ActiveEffectStoreOwnerCount)
                .Append("|slots=")
                .Append(counters.ActiveEffectSlotCount)
                .Append("|capacity=")
                .Append(counters.ActiveEffectSlotCapacity)
                .Append("|pendingApply=")
                .Append(counters.ActiveEffectSlotPendingApplyCount)
                .Append("|active=")
                .Append(counters.ActiveEffectSlotActiveCount)
                .Append("|inhibited=")
                .Append(counters.ActiveEffectSlotInhibitedCount)
                .Append("|pendingRemove=")
                .Append(counters.ActiveEffectSlotPendingRemoveCount)
                .Append("|legacyBacked=")
                .Append(counters.ActiveEffectSlotLegacyBackedCount)
                .Append("|externalizedOwners=")
                .Append(counters.ActiveEffectSlotExternalizedOwnerCount)
                .Append("|grantedTags=")
                .Append(counters.ActiveEffectSlotGrantedTagCount)
                .Append("|grantedAbilities=")
                .Append(counters.ActiveEffectSlotGrantedAbilityCount)
                .Append("|chunkSkipMatched=")
                .Append(counters.ActiveEffectChunkSkipMatchedSlotCount)
                .Append("|chunkSkipSkipped=")
                .Append(counters.ActiveEffectChunkSkipSkippedSlotCount)
                .Append("|chunkSkipDuePeriod=")
                .Append(counters.ActiveEffectChunkSkipDuePeriodSlotCount)
                .Append("|chunkSkipNoop=")
                .Append(counters.ActiveEffectChunkSkipNoopSlotCount)
                .Append("|chunkSkipOwners=")
                .Append(counters.ActiveEffectChunkSkipOwnerCount)
                .Append("|globalIndexOwners=")
                .Append(counters.ActiveEffectGlobalIndexOwnerCount)
                .Append("|globalIndexCount=")
                .Append(counters.ActiveEffectGlobalIndexCount)
                .Append("|globalIndexActive=")
                .Append(counters.ActiveEffectGlobalIndexActiveCount)
                .Append("|globalIndexInhibited=")
                .Append(counters.ActiveEffectGlobalIndexInhibitedCount)
                .Append("|globalIndexPendingRemove=")
                .Append(counters.ActiveEffectGlobalIndexPendingRemoveCount)
                .Append("|globalIndexPeriodDue=")
                .Append(counters.ActiveEffectGlobalIndexPeriodDueCount)
                .Append("|globalIndexDurationDue=")
                .Append(counters.ActiveEffectGlobalIndexDurationDueCount)
                .Append("|globalIndexStale=")
                .Append(counters.ActiveEffectGlobalIndexStaleCount)
                .Append("|globalIndexBucketOwners=")
                .Append(counters.ActiveEffectGlobalIndexBucketOwnerCount)
                .Append("|globalIndexBucketIndexCount=")
                .Append(counters.ActiveEffectGlobalIndexBucketIndexCount)
                .Append("|globalIndexMaxBucketLength=")
                .Append(counters.ActiveEffectGlobalIndexMaxBucketLength)
                .Append("|globalIndexStableRows=")
                .Append(counters.ActiveEffectGlobalIndexStableRowCount)
                .Append("|globalIndexStaleStableRows=")
                .Append(counters.ActiveEffectGlobalIndexStaleStableRowCount)
                .AppendLine();
            builder.Append("runtimeCoreActiveMutation|commands=")
                .Append(counters.ActiveMutationCommandCount)
                .Append("|ownerGroups=")
                .Append(counters.ActiveMutationOwnerGroupCount)
                .Append("|maxOwnerRange=")
                .Append(counters.ActiveMutationMaxOwnerRange)
                .Append("|sortMoves=")
                .Append(counters.ActiveMutationSortMoveCount)
                .Append("|estimatedRandomLookups=")
                .Append(counters.ActiveMutationEstimatedRandomLookupCount)
                .Append("|ownerResourceLookups=")
                .Append(counters.ActiveMutationOwnerResourceLookupCount)
                .Append("|migrationCarriers=")
                .Append(counters.ActiveMutationMigrationCarrierCount)
                .AppendLine();
            builder.Append("runtimeCorePendingAttributeDelta|pending=")
                .Append(counters.PendingAttributeDeltaCount)
                .Append("|applied=")
                .Append(counters.PendingAttributeAppliedDeltaCount)
                .Append("|skipped=")
                .Append(counters.PendingAttributeSkippedDeltaCount)
                .Append("|targetGroups=")
                .Append(counters.PendingAttributeTargetGroupCount)
                .Append("|maxTargetRange=")
                .Append(counters.PendingAttributeMaxTargetRange)
                .Append("|estimatedRandomLookups=")
                .Append(counters.PendingAttributeEstimatedRandomLookupCount)
                .Append("|factPatches=")
                .Append(counters.PendingAttributeFactPatchCount)
                .Append("|migrationCarriers=")
                .Append(counters.PendingAttributeMigrationCarrierCount)
                .AppendLine();
            builder.Append("runtimeCoreFrameBudget|queryBudget=")
                .Append(counters.QueryBudget)
                .Append("|filteredQueryBudget=")
                .Append(counters.FilteredQueryBudget)
                .Append("|unfilteredQueryBudget=")
                .Append(counters.UnfilteredQueryBudget)
                .Append("|lookupUpdateBudget=")
                .Append(counters.LookupUpdateBudget)
                .Append("|randomLookupBudget=")
                .Append(counters.RandomLookupBudget)
                .Append("|syncQueryBudget=")
                .Append(counters.SyncQueryBudget)
                .Append("|helperTempQueryRisks=")
                .Append(counters.HelperTempQueryRiskCount)
                .Append("|dependencyWaitRisks=")
                .Append(counters.DependencyWaitRiskCount)
                .Append("|worldUpdateAllocatorOwners=")
                .Append(counters.WorldUpdateAllocatorOwnerCount)
                .Append("|rewindableAllocatorCandidates=")
                .Append(counters.RewindableAllocatorCandidateCount)
                .AppendLine();
        }

        private static void AppendObservationMaterializationCounters(
            StringBuilder builder,
            in GasRuntimeObservationMaterializationCounters counters)
        {
            builder.Append("runtimeObservationMaterialization|queries=")
                .Append(counters.MaterializedQueryCount)
                .Append("|entities=")
                .Append(counters.MaterializedEntityCount)
                .Append("|elapsedUs=")
                .Append(counters.ElapsedMicroseconds)
                .Append("|activeEffectStoreQueries=")
                .Append(counters.ActiveEffectStoreQueryCount)
                .Append("|activeEffectStoreEntities=")
                .Append(counters.ActiveEffectStoreEntityCount)
                .Append("|presentationOutboxQueries=")
                .Append(counters.PresentationOutboxQueryCount)
                .Append("|presentationOutboxEntities=")
                .Append(counters.PresentationOutboxEntityCount)
                .Append("|performancePollutionRisks=")
                .Append(counters.PerformancePollutionRiskCount)
                .AppendLine();
        }

        private static void AppendRuntimeCoreFrameBackboneCounters(
            StringBuilder builder,
            in GasRuntimeFrameBackboneDiagnosticCounters counters)
        {
            builder.Append("runtimeCoreFrameBackbone|phases=")
                .Append(counters.PhaseCount)
                .Append("|contractOnlyPhases=")
                .Append(counters.ContractOnlyPhaseCount)
                .Append("|queryBudget=")
                .Append(counters.QueryBudget)
                .Append("|filteredQueryBudget=")
                .Append(counters.FilteredQueryBudget)
                .Append("|unfilteredQueryBudget=")
                .Append(counters.UnfilteredQueryBudget)
                .Append("|lookupUpdateBudget=")
                .Append(counters.LookupUpdateBudget)
                .Append("|randomLookupBudget=")
                .Append(counters.RandomLookupBudget)
                .Append("|syncQueryBudget=")
                .Append(counters.SyncQueryBudget)
                .Append("|dependencyWaitRisks=")
                .Append(counters.DependencyWaitRiskCount)
                .Append("|allocatorOwners=")
                .Append(counters.WorldUpdateAllocatorOwnerCount)
                .Append("|rewindableAllocatorCandidates=")
                .Append(counters.RewindableAllocatorCandidateCount)
                .Append("|streams=")
                .Append(counters.StreamCount)
                .Append("|migrationCarriers=")
                .Append(counters.MigrationCarrierCount)
                .Append("|nativeStreamCandidates=")
                .Append(counters.NativeStreamCandidateCount)
                .Append("|ownerLocalBufferCandidates=")
                .Append(counters.OwnerLocalBufferCandidateCount)
                .Append("|battleHashStreams=")
                .Append(counters.BattleHashStreamCount)
                .Append("|deterministicMergePolicies=")
                .Append(counters.DeterministicMergePolicyCount)
                .Append("|mergeCostUs=")
                .Append(counters.MergeCostMicroseconds)
                .Append("|mergeCostMeasured=")
                .Append(counters.MergeCostMeasuredCount)
                .Append("|requiredStructuralPlaybacks=")
                .Append(counters.RequiredStructuralPlaybackCount)
                .Append("|recordedStructuralPlaybacks=")
                .Append(counters.RecordedStructuralPlaybackCount)
                .Append("|ecbCommands=")
                .Append(counters.EcbCommandCount)
                .Append("|bulkQueries=")
                .Append(counters.BulkQueryCount)
                .AppendLine();

            builder.Append("runtimeCoreFrameBackboneEvidence|profilerMarkers=")
                .Append(counters.ProfilerMarkerCount)
                .Append("|journalingMarkers=")
                .Append(counters.JournalingMarkerCount)
                .Append("|evidenceMask=")
                .Append(counters.EvidenceMask)
                .Append("|burstWarmupPolicy=")
                .Append(counters.BurstWarmupPolicyCount)
                .AppendLine();

            builder.Append("runtimeCoreCostSplit|core=")
                .Append(counters.CoreCostGroupCount)
                .Append("|physics=")
                .Append(counters.PhysicsCostGroupCount)
                .Append("|render=")
                .Append(counters.RenderCostGroupCount)
                .Append("|runner=")
                .Append(counters.RunnerCostGroupCount)
                .Append("|physicsDisabledReasons=")
                .Append(counters.PhysicsDisabledReasonCount)
                .Append("|renderDisabledReasons=")
                .Append(counters.RenderDisabledReasonCount)
                .AppendLine();

            builder.Append("runtimeCoreDebuggerOverhead|samplingInterval=")
                .Append(counters.SamplingInterval)
                .Append("|overheadBudgetUs=")
                .Append(counters.DebuggerOverheadBudgetMicroseconds)
                .Append("|disablePolicy=")
                .Append(counters.DisablePolicyCount)
                .Append("|hotPathManagedStrings=")
                .Append(counters.HotPathManagedStringCount)
                .AppendLine();
        }
    }
}
