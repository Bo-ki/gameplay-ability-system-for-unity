namespace GAS.Runtime
{
    public readonly struct GasRuntimeWorkloadMetricSnapshot
    {
        public readonly int RequestCount;
        public readonly int SpecCount;
        public readonly int DeltaCount;
        public readonly int FactCount;
        public readonly int CueCount;
        public readonly int PresentationCount;

        public GasRuntimeWorkloadMetricSnapshot(
            int requestCount,
            int specCount,
            int deltaCount,
            int factCount,
            int cueCount,
            int presentationCount)
        {
            RequestCount = requestCount;
            SpecCount = specCount;
            DeltaCount = deltaCount;
            FactCount = factCount;
            CueCount = cueCount;
            PresentationCount = presentationCount;
        }

        public bool HasEvidence =>
            RequestCount > 0
            || SpecCount > 0
            || DeltaCount > 0
            || FactCount > 0
            || CueCount > 0
            || PresentationCount > 0;
    }

    public readonly struct GasRuntimeGasConceptMetricSnapshot
    {
        public readonly int ActiveMutationCommandCount;
        public readonly int PendingAttributeDeltaCount;
        public readonly int OwnerLocalFactCount;
        public readonly int ActiveEffectSlotCount;
        public readonly int ActiveEffectDuePeriodSlotCount;
        public readonly int MagnitudeSourceLookupCount;
        public readonly int MagnitudeSourceCaptureMissCount;
        public readonly int MagnitudeSourceFallbackCount;

        public GasRuntimeGasConceptMetricSnapshot(
            int activeMutationCommandCount,
            int pendingAttributeDeltaCount,
            int ownerLocalFactCount,
            int activeEffectSlotCount,
            int activeEffectDuePeriodSlotCount,
            int magnitudeSourceLookupCount,
            int magnitudeSourceCaptureMissCount,
            int magnitudeSourceFallbackCount)
        {
            ActiveMutationCommandCount = activeMutationCommandCount;
            PendingAttributeDeltaCount = pendingAttributeDeltaCount;
            OwnerLocalFactCount = ownerLocalFactCount;
            ActiveEffectSlotCount = activeEffectSlotCount;
            ActiveEffectDuePeriodSlotCount = activeEffectDuePeriodSlotCount;
            MagnitudeSourceLookupCount = magnitudeSourceLookupCount;
            MagnitudeSourceCaptureMissCount = magnitudeSourceCaptureMissCount;
            MagnitudeSourceFallbackCount = magnitudeSourceFallbackCount;
        }

        public bool HasEvidence =>
            ActiveMutationCommandCount > 0
            || PendingAttributeDeltaCount > 0
            || OwnerLocalFactCount > 0
            || ActiveEffectSlotCount > 0
            || ActiveEffectDuePeriodSlotCount > 0
            || MagnitudeSourceLookupCount > 0
            || MagnitudeSourceCaptureMissCount > 0
            || MagnitudeSourceFallbackCount > 0;
    }

    public readonly struct GasRuntimeDataShapeMetricSnapshot
    {
        public readonly int ActiveMutationOwnerGroupCount;
        public readonly int ActiveMutationMaxOwnerRange;
        public readonly int PendingAttributeTargetGroupCount;
        public readonly int PendingAttributeMaxTargetRange;
        public readonly int OwnerLocalFactOwnerGroupCount;
        public readonly int OwnerLocalFactMaxOwnerRange;
        public readonly int OwnerLocalFactFlushCount;
        public readonly int OwnerLocalFactChangedChunkCount;
        public readonly int OwnerLocalFactScannedOwnerCount;
        public readonly int OwnerLocalFactDirtyOwnerCount;
        public readonly int OwnerLocalFactSkippedOwnerCount;
        public readonly int OwnerLocalFactClearedOwnerCount;
        public readonly int ActiveEffectSlotCount;
        public readonly int ActiveEffectSlotCapacity;
        public readonly int ActiveEffectSlotSourceSnapshotCapacity;
        public readonly int ActiveEffectSlotSourceSnapshotSpillCount;

        public GasRuntimeDataShapeMetricSnapshot(
            int activeMutationOwnerGroupCount,
            int activeMutationMaxOwnerRange,
            int pendingAttributeTargetGroupCount,
            int pendingAttributeMaxTargetRange,
            int ownerLocalFactOwnerGroupCount,
            int ownerLocalFactMaxOwnerRange,
            int ownerLocalFactFlushCount,
            int ownerLocalFactChangedChunkCount,
            int ownerLocalFactScannedOwnerCount,
            int ownerLocalFactDirtyOwnerCount,
            int ownerLocalFactSkippedOwnerCount,
            int ownerLocalFactClearedOwnerCount,
            int activeEffectSlotCount,
            int activeEffectSlotCapacity,
            int activeEffectSlotSourceSnapshotCapacity,
            int activeEffectSlotSourceSnapshotSpillCount)
        {
            ActiveMutationOwnerGroupCount = activeMutationOwnerGroupCount;
            ActiveMutationMaxOwnerRange = activeMutationMaxOwnerRange;
            PendingAttributeTargetGroupCount = pendingAttributeTargetGroupCount;
            PendingAttributeMaxTargetRange = pendingAttributeMaxTargetRange;
            OwnerLocalFactOwnerGroupCount = ownerLocalFactOwnerGroupCount;
            OwnerLocalFactMaxOwnerRange = ownerLocalFactMaxOwnerRange;
            OwnerLocalFactFlushCount = ownerLocalFactFlushCount;
            OwnerLocalFactChangedChunkCount = ownerLocalFactChangedChunkCount;
            OwnerLocalFactScannedOwnerCount = ownerLocalFactScannedOwnerCount;
            OwnerLocalFactDirtyOwnerCount = ownerLocalFactDirtyOwnerCount;
            OwnerLocalFactSkippedOwnerCount = ownerLocalFactSkippedOwnerCount;
            OwnerLocalFactClearedOwnerCount = ownerLocalFactClearedOwnerCount;
            ActiveEffectSlotCount = activeEffectSlotCount;
            ActiveEffectSlotCapacity = activeEffectSlotCapacity;
            ActiveEffectSlotSourceSnapshotCapacity = activeEffectSlotSourceSnapshotCapacity;
            ActiveEffectSlotSourceSnapshotSpillCount = activeEffectSlotSourceSnapshotSpillCount;
        }

        public bool HasEvidence =>
            ActiveMutationOwnerGroupCount > 0
            || ActiveMutationMaxOwnerRange > 0
            || PendingAttributeTargetGroupCount > 0
            || PendingAttributeMaxTargetRange > 0
            || OwnerLocalFactOwnerGroupCount > 0
            || OwnerLocalFactMaxOwnerRange > 0
            || OwnerLocalFactFlushCount > 0
            || OwnerLocalFactChangedChunkCount > 0
            || OwnerLocalFactScannedOwnerCount > 0
            || OwnerLocalFactDirtyOwnerCount > 0
            || OwnerLocalFactSkippedOwnerCount > 0
            || OwnerLocalFactClearedOwnerCount > 0
            || ActiveEffectSlotCount > 0
            || ActiveEffectSlotCapacity > 0
            || ActiveEffectSlotSourceSnapshotCapacity > 0
            || ActiveEffectSlotSourceSnapshotSpillCount > 0;
    }

    public readonly struct GasRuntimeApiHealthMetricSnapshot
    {
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
        public readonly int ActiveMutationEstimatedRandomLookupCount;
        public readonly int PendingAttributeEstimatedRandomLookupCount;

        public GasRuntimeApiHealthMetricSnapshot(
            int queryBudget,
            int filteredQueryBudget,
            int unfilteredQueryBudget,
            int lookupUpdateBudget,
            int randomLookupBudget,
            int syncQueryBudget,
            int helperTempQueryRiskCount,
            int dependencyWaitRiskCount,
            int worldUpdateAllocatorOwnerCount,
            int rewindableAllocatorCandidateCount,
            int activeMutationEstimatedRandomLookupCount,
            int pendingAttributeEstimatedRandomLookupCount)
        {
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
            ActiveMutationEstimatedRandomLookupCount = activeMutationEstimatedRandomLookupCount;
            PendingAttributeEstimatedRandomLookupCount = pendingAttributeEstimatedRandomLookupCount;
        }

        public bool HasEvidence =>
            QueryBudget > 0
            || FilteredQueryBudget > 0
            || UnfilteredQueryBudget > 0
            || LookupUpdateBudget > 0
            || RandomLookupBudget > 0
            || SyncQueryBudget > 0
            || HelperTempQueryRiskCount > 0
            || DependencyWaitRiskCount > 0
            || WorldUpdateAllocatorOwnerCount > 0
            || RewindableAllocatorCandidateCount > 0
            || ActiveMutationEstimatedRandomLookupCount > 0
            || PendingAttributeEstimatedRandomLookupCount > 0;
    }

    public readonly struct GasRuntimeStructuralMetricSnapshot
    {
        public readonly int EntityCreateCount;
        public readonly int EntityDestroyCount;
        public readonly int EcbPlaybackCount;
        public readonly int RequiredStructuralPlaybackCount;
        public readonly int RecordedStructuralPlaybackCount;
        public readonly int EcbCommandCount;
        public readonly int BulkQueryCount;

        public GasRuntimeStructuralMetricSnapshot(
            int entityCreateCount,
            int entityDestroyCount,
            int ecbPlaybackCount,
            int requiredStructuralPlaybackCount,
            int recordedStructuralPlaybackCount,
            int ecbCommandCount,
            int bulkQueryCount)
        {
            EntityCreateCount = entityCreateCount;
            EntityDestroyCount = entityDestroyCount;
            EcbPlaybackCount = ecbPlaybackCount;
            RequiredStructuralPlaybackCount = requiredStructuralPlaybackCount;
            RecordedStructuralPlaybackCount = recordedStructuralPlaybackCount;
            EcbCommandCount = ecbCommandCount;
            BulkQueryCount = bulkQueryCount;
        }

        public bool HasEvidence =>
            EntityCreateCount > 0
            || EntityDestroyCount > 0
            || EcbPlaybackCount > 0
            || RequiredStructuralPlaybackCount > 0
            || RecordedStructuralPlaybackCount > 0
            || EcbCommandCount > 0
            || BulkQueryCount > 0;
    }

    public readonly struct GasRuntimeOverheadMetricSnapshot
    {
        public readonly int ObservationMaterializedQueryCount;
        public readonly int ObservationMaterializedEntityCount;
        public readonly int ObservationMaterializationElapsedMicroseconds;
        public readonly int ObservationPerformancePollutionRiskCount;
        public readonly int ProfilerMarkerCount;
        public readonly int JournalingMarkerCount;
        public readonly int DebuggerOverheadBudgetMicroseconds;
        public readonly int SamplingInterval;

        public GasRuntimeOverheadMetricSnapshot(
            int observationMaterializedQueryCount,
            int observationMaterializedEntityCount,
            int observationMaterializationElapsedMicroseconds,
            int observationPerformancePollutionRiskCount,
            int profilerMarkerCount,
            int journalingMarkerCount,
            int debuggerOverheadBudgetMicroseconds,
            int samplingInterval)
        {
            ObservationMaterializedQueryCount = observationMaterializedQueryCount;
            ObservationMaterializedEntityCount = observationMaterializedEntityCount;
            ObservationMaterializationElapsedMicroseconds = observationMaterializationElapsedMicroseconds;
            ObservationPerformancePollutionRiskCount = observationPerformancePollutionRiskCount;
            ProfilerMarkerCount = profilerMarkerCount;
            JournalingMarkerCount = journalingMarkerCount;
            DebuggerOverheadBudgetMicroseconds = debuggerOverheadBudgetMicroseconds;
            SamplingInterval = samplingInterval;
        }

        public bool HasEvidence =>
            ObservationMaterializedQueryCount > 0
            || ObservationMaterializedEntityCount > 0
            || ObservationMaterializationElapsedMicroseconds > 0
            || ObservationPerformancePollutionRiskCount > 0
            || ProfilerMarkerCount > 0
            || JournalingMarkerCount > 0
            || DebuggerOverheadBudgetMicroseconds > 0
            || SamplingInterval > 0;
    }

    public readonly struct GasRuntimeMetricFamilySnapshot
    {
        public readonly GasRuntimeWorkloadMetricSnapshot Workload;
        public readonly GasRuntimeGasConceptMetricSnapshot GasConcept;
        public readonly GasRuntimeDataShapeMetricSnapshot DataShape;
        public readonly GasRuntimeApiHealthMetricSnapshot ApiHealth;
        public readonly GasRuntimeStructuralMetricSnapshot Structural;
        public readonly GasRuntimeOverheadMetricSnapshot Overhead;
        public readonly GasRuntimeDiagnosticsMetricFamilyMask MetricFamilyMask;

        public static GasRuntimeMetricFamilySnapshot Empty => default;

        public GasRuntimeMetricFamilySnapshot(
            in GasRuntimeWorkloadMetricSnapshot workload,
            in GasRuntimeGasConceptMetricSnapshot gasConcept,
            in GasRuntimeDataShapeMetricSnapshot dataShape,
            in GasRuntimeApiHealthMetricSnapshot apiHealth,
            in GasRuntimeStructuralMetricSnapshot structural,
            in GasRuntimeOverheadMetricSnapshot overhead)
        {
            Workload = workload;
            GasConcept = gasConcept;
            DataShape = dataShape;
            ApiHealth = apiHealth;
            Structural = structural;
            Overhead = overhead;
            MetricFamilyMask = ComputeMetricFamilyMask(
                workload,
                gasConcept,
                dataShape,
                apiHealth,
                structural,
                overhead);
        }

        public bool HasEvidence => MetricFamilyMask != GasRuntimeDiagnosticsMetricFamilyMask.None;

        public static GasRuntimeMetricFamilySnapshot Create(
            in GasRuntimeCoreDiagnosticCounters core,
            in GasRuntimeFrameBackboneDiagnosticCounters frameBackbone,
            in GasRuntimeObservationMaterializationCounters observation,
            in GasRuntimeMagnitudeSourceCounters magnitude)
        {
            var workload = new GasRuntimeWorkloadMetricSnapshot(
                core.RequestCount,
                core.SpecCount,
                core.DeltaCount,
                core.FactCount,
                core.CueCount,
                core.PresentationCount);
            var coreMagnitudeSourceLookupCount =
                core.MagnitudeSourceCurrentValueLookupCount
                + core.MagnitudeSourceSourceAttributeLookupCount
                + core.MagnitudeSourceTargetAttributeLookupCount
                + core.MagnitudeSourceExecutionInputLookupCount;
            var diagnosticMagnitudeSourceLookupCount =
                magnitude.CurrentValueLookupCount
                + magnitude.SourceAttributeLookupCount
                + magnitude.TargetAttributeLookupCount
                + magnitude.ExecutionInputLookupCount;
            var magnitudeSourceLookupCount = Max(
                coreMagnitudeSourceLookupCount,
                diagnosticMagnitudeSourceLookupCount);
            var magnitudeSourceCaptureMissCount = Max(
                core.MagnitudeSourceCaptureMissCount,
                magnitude.CaptureMissCount);
            var magnitudeSourceFallbackCount = Max(
                core.MagnitudeSourceFallbackValueCount + core.MagnitudeSourceFallbackFactCount,
                magnitude.FallbackValueCount + magnitude.FallbackFactCount);
            var gasConcept = new GasRuntimeGasConceptMetricSnapshot(
                core.ActiveMutationCommandCount,
                core.PendingAttributeDeltaCount,
                core.OwnerLocalFactCount,
                core.ActiveEffectSlotCount,
                core.ActiveEffectChunkSkipDuePeriodSlotCount,
                magnitudeSourceLookupCount,
                magnitudeSourceCaptureMissCount,
                magnitudeSourceFallbackCount);
            var dataShape = new GasRuntimeDataShapeMetricSnapshot(
                core.ActiveMutationOwnerGroupCount,
                core.ActiveMutationMaxOwnerRange,
                core.PendingAttributeTargetGroupCount,
                core.PendingAttributeMaxTargetRange,
                core.OwnerLocalFactOwnerGroupCount,
                core.OwnerLocalFactMaxOwnerRange,
                core.OwnerLocalFactFlushCount,
                core.OwnerLocalFactChangedChunkCount,
                core.OwnerLocalFactScannedOwnerCount,
                core.OwnerLocalFactDirtyOwnerCount,
                core.OwnerLocalFactSkippedOwnerCount,
                core.OwnerLocalFactClearedOwnerCount,
                core.ActiveEffectSlotCount,
                core.ActiveEffectSlotCapacity,
                magnitude.ActiveEffectSlotSourceSnapshotCapacity,
                magnitude.ActiveEffectSlotSourceSnapshotSpillCount);
            var apiHealth = new GasRuntimeApiHealthMetricSnapshot(
                core.QueryBudget,
                core.FilteredQueryBudget,
                core.UnfilteredQueryBudget,
                core.LookupUpdateBudget,
                core.RandomLookupBudget,
                core.SyncQueryBudget,
                core.HelperTempQueryRiskCount,
                core.DependencyWaitRiskCount,
                core.WorldUpdateAllocatorOwnerCount,
                core.RewindableAllocatorCandidateCount,
                core.ActiveMutationEstimatedRandomLookupCount,
                core.PendingAttributeEstimatedRandomLookupCount);
            var structural = new GasRuntimeStructuralMetricSnapshot(
                core.EntityCreateCount,
                core.EntityDestroyCount,
                core.EcbPlaybackCount,
                frameBackbone.RequiredStructuralPlaybackCount,
                frameBackbone.RecordedStructuralPlaybackCount,
                frameBackbone.EcbCommandCount,
                frameBackbone.BulkQueryCount);
            var overhead = new GasRuntimeOverheadMetricSnapshot(
                observation.MaterializedQueryCount,
                observation.MaterializedEntityCount,
                observation.ElapsedMicroseconds,
                observation.PerformancePollutionRiskCount,
                frameBackbone.ProfilerMarkerCount,
                frameBackbone.JournalingMarkerCount,
                frameBackbone.DebuggerOverheadBudgetMicroseconds,
                frameBackbone.SamplingInterval);
            return new GasRuntimeMetricFamilySnapshot(
                workload,
                gasConcept,
                dataShape,
                apiHealth,
                structural,
                overhead);
        }

        private static GasRuntimeDiagnosticsMetricFamilyMask ComputeMetricFamilyMask(
            in GasRuntimeWorkloadMetricSnapshot workload,
            in GasRuntimeGasConceptMetricSnapshot gasConcept,
            in GasRuntimeDataShapeMetricSnapshot dataShape,
            in GasRuntimeApiHealthMetricSnapshot apiHealth,
            in GasRuntimeStructuralMetricSnapshot structural,
            in GasRuntimeOverheadMetricSnapshot overhead)
        {
            var mask = GasRuntimeDiagnosticsMetricFamilyMask.None;

            if (workload.HasEvidence)
                mask |= GasRuntimeDiagnosticsMetricFamilyMask.Workload;
            if (gasConcept.HasEvidence)
                mask |= GasRuntimeDiagnosticsMetricFamilyMask.GasConcept;
            if (dataShape.HasEvidence)
                mask |= GasRuntimeDiagnosticsMetricFamilyMask.DataShape;
            if (apiHealth.HasEvidence)
                mask |= GasRuntimeDiagnosticsMetricFamilyMask.ApiHealth;
            if (structural.HasEvidence)
                mask |= GasRuntimeDiagnosticsMetricFamilyMask.Structural;
            if (overhead.HasEvidence)
                mask |= GasRuntimeDiagnosticsMetricFamilyMask.Overhead;

            return mask;
        }

        private static int Max(int left, int right)
        {
            return left >= right ? left : right;
        }
    }
}
