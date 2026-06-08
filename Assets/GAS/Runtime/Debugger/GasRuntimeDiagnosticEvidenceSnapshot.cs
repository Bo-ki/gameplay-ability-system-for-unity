using System;

namespace GAS.Runtime
{
    public readonly struct GasRuntimeWorkloadEvidenceSnapshot
    {
        public readonly int RequestCount;
        public readonly int SpecCount;
        public readonly int DeltaCount;
        public readonly int FactCount;
        public readonly int CueCount;
        public readonly int PresentationCount;
        public readonly int PeakEventBusBufferLength;
        public readonly int PeakReplayCursorLag;

        public GasRuntimeWorkloadEvidenceSnapshot(in GasRuntimeCoreDiagnosticCounters counters)
        {
            RequestCount = counters.RequestCount;
            SpecCount = counters.SpecCount;
            DeltaCount = counters.DeltaCount;
            FactCount = counters.FactCount;
            CueCount = counters.CueCount;
            PresentationCount = counters.PresentationCount;
            PeakEventBusBufferLength = counters.PeakEventBusBufferLength;
            PeakReplayCursorLag = counters.PeakReplayCursorLag;
        }
    }

    public readonly struct GasRuntimeActiveEffectEvidenceSnapshot
    {
        public readonly int StoreOwnerCount;
        public readonly int SlotCount;
        public readonly int SlotCapacity;
        public readonly int SlotActiveCount;
        public readonly int SlotPendingApplyCount;
        public readonly int SlotInhibitedCount;
        public readonly int SlotPendingRemoveCount;
        public readonly int ChunkSkipDuePeriodSlotCount;
        public readonly int PreTickChunkCount;
        public readonly int PreTickScannedOwnerCount;
        public readonly int PreTickSkippedOwnerCount;
        public readonly int PreTickProcessedOwnerCount;
        public readonly int PreTickScannedSlotCount;
        public readonly int PreTickDueSlotCount;
        public readonly int PreTickNoopSlotCount;
        public readonly int PreTickMutationWriteCount;

        public GasRuntimeActiveEffectEvidenceSnapshot(in GasRuntimeCoreDiagnosticCounters counters)
        {
            StoreOwnerCount = counters.ActiveEffectStoreOwnerCount;
            SlotCount = counters.ActiveEffectSlotCount;
            SlotCapacity = counters.ActiveEffectSlotCapacity;
            SlotActiveCount = counters.ActiveEffectSlotActiveCount;
            SlotPendingApplyCount = counters.ActiveEffectSlotPendingApplyCount;
            SlotInhibitedCount = counters.ActiveEffectSlotInhibitedCount;
            SlotPendingRemoveCount = counters.ActiveEffectSlotPendingRemoveCount;
            ChunkSkipDuePeriodSlotCount = counters.ActiveEffectChunkSkipDuePeriodSlotCount;
            PreTickChunkCount = counters.ActiveEffectPreTickChunkCount;
            PreTickScannedOwnerCount = counters.ActiveEffectPreTickScannedOwnerCount;
            PreTickSkippedOwnerCount = counters.ActiveEffectPreTickSkippedOwnerCount;
            PreTickProcessedOwnerCount = counters.ActiveEffectPreTickProcessedOwnerCount;
            PreTickScannedSlotCount = counters.ActiveEffectPreTickScannedSlotCount;
            PreTickDueSlotCount = counters.ActiveEffectPreTickDueSlotCount;
            PreTickNoopSlotCount = counters.ActiveEffectPreTickNoopSlotCount;
            PreTickMutationWriteCount = counters.ActiveEffectPreTickMutationWriteCount;
        }
    }

    public readonly struct GasRuntimeActiveMutationEvidenceSnapshot
    {
        public readonly int CommandCount;
        public readonly int OwnerGroupCount;
        public readonly int MaxOwnerRange;
        public readonly int SortMoveCount;
        public readonly int EstimatedRandomLookupCount;
        public readonly int OwnerResourceLookupCount;
        public readonly int MigrationCarrierCount;

        public GasRuntimeActiveMutationEvidenceSnapshot(in GasRuntimeCoreDiagnosticCounters counters)
        {
            CommandCount = counters.ActiveMutationCommandCount;
            OwnerGroupCount = counters.ActiveMutationOwnerGroupCount;
            MaxOwnerRange = counters.ActiveMutationMaxOwnerRange;
            SortMoveCount = counters.ActiveMutationSortMoveCount;
            EstimatedRandomLookupCount = counters.ActiveMutationEstimatedRandomLookupCount;
            OwnerResourceLookupCount = counters.ActiveMutationOwnerResourceLookupCount;
            MigrationCarrierCount = counters.ActiveMutationMigrationCarrierCount;
        }
    }

    public readonly struct GasRuntimeAttributeFactEvidenceSnapshot
    {
        public readonly int PendingDeltaCount;
        public readonly int PendingAppliedDeltaCount;
        public readonly int PendingSkippedDeltaCount;
        public readonly int PendingTargetGroupCount;
        public readonly int PendingMaxTargetRange;
        public readonly int PendingEstimatedRandomLookupCount;
        public readonly int PendingFactPatchCount;
        public readonly int PendingMigrationCarrierCount;
        public readonly int OwnerLocalFactCount;
        public readonly int OwnerLocalFactOwnerGroupCount;
        public readonly int OwnerLocalFactMaxOwnerRange;
        public readonly int OwnerLocalFactFlushCount;
        public readonly int OwnerLocalFactChangedChunkCount;
        public readonly int OwnerLocalFactScannedOwnerCount;
        public readonly int OwnerLocalFactDirtyOwnerCount;
        public readonly int OwnerLocalFactSkippedOwnerCount;
        public readonly int OwnerLocalFactClearedOwnerCount;
        public readonly int OwnerLocalInstantPrepareChunkCount;
        public readonly int OwnerLocalInstantPrepareScannedOwnerCount;
        public readonly int OwnerLocalInstantPrepareSkippedOwnerCount;
        public readonly int OwnerLocalInstantPrepareDirtyOwnerCount;
        public readonly int OwnerLocalInstantPrepareClearedCommandCount;
        public readonly int OwnerLocalInstantPrepareClearedSpecCount;
        public readonly int OwnerLocalInstantPreparePromotedCommandCount;
        public readonly int ActiveMutationPrepareChunkCount;
        public readonly int ActiveMutationPrepareScannedOwnerCount;
        public readonly int ActiveMutationPrepareSkippedOwnerCount;
        public readonly int ActiveMutationPrepareDirtyOwnerCount;
        public readonly int ActiveMutationPrepareClearedMutationCount;
        public readonly int ActiveMutationPreparePromotedCommandCount;

        public GasRuntimeAttributeFactEvidenceSnapshot(in GasRuntimeCoreDiagnosticCounters counters)
        {
            PendingDeltaCount = counters.PendingAttributeDeltaCount;
            PendingAppliedDeltaCount = counters.PendingAttributeAppliedDeltaCount;
            PendingSkippedDeltaCount = counters.PendingAttributeSkippedDeltaCount;
            PendingTargetGroupCount = counters.PendingAttributeTargetGroupCount;
            PendingMaxTargetRange = counters.PendingAttributeMaxTargetRange;
            PendingEstimatedRandomLookupCount = counters.PendingAttributeEstimatedRandomLookupCount;
            PendingFactPatchCount = counters.PendingAttributeFactPatchCount;
            PendingMigrationCarrierCount = counters.PendingAttributeMigrationCarrierCount;
            OwnerLocalFactCount = counters.OwnerLocalFactCount;
            OwnerLocalFactOwnerGroupCount = counters.OwnerLocalFactOwnerGroupCount;
            OwnerLocalFactMaxOwnerRange = counters.OwnerLocalFactMaxOwnerRange;
            OwnerLocalFactFlushCount = counters.OwnerLocalFactFlushCount;
            OwnerLocalFactChangedChunkCount = counters.OwnerLocalFactChangedChunkCount;
            OwnerLocalFactScannedOwnerCount = counters.OwnerLocalFactScannedOwnerCount;
            OwnerLocalFactDirtyOwnerCount = counters.OwnerLocalFactDirtyOwnerCount;
            OwnerLocalFactSkippedOwnerCount = counters.OwnerLocalFactSkippedOwnerCount;
            OwnerLocalFactClearedOwnerCount = counters.OwnerLocalFactClearedOwnerCount;
            OwnerLocalInstantPrepareChunkCount = counters.OwnerLocalInstantPrepareChunkCount;
            OwnerLocalInstantPrepareScannedOwnerCount = counters.OwnerLocalInstantPrepareScannedOwnerCount;
            OwnerLocalInstantPrepareSkippedOwnerCount = counters.OwnerLocalInstantPrepareSkippedOwnerCount;
            OwnerLocalInstantPrepareDirtyOwnerCount = counters.OwnerLocalInstantPrepareDirtyOwnerCount;
            OwnerLocalInstantPrepareClearedCommandCount = counters.OwnerLocalInstantPrepareClearedCommandCount;
            OwnerLocalInstantPrepareClearedSpecCount = counters.OwnerLocalInstantPrepareClearedSpecCount;
            OwnerLocalInstantPreparePromotedCommandCount = counters.OwnerLocalInstantPreparePromotedCommandCount;
            ActiveMutationPrepareChunkCount = counters.ActiveMutationPrepareChunkCount;
            ActiveMutationPrepareScannedOwnerCount = counters.ActiveMutationPrepareScannedOwnerCount;
            ActiveMutationPrepareSkippedOwnerCount = counters.ActiveMutationPrepareSkippedOwnerCount;
            ActiveMutationPrepareDirtyOwnerCount = counters.ActiveMutationPrepareDirtyOwnerCount;
            ActiveMutationPrepareClearedMutationCount = counters.ActiveMutationPrepareClearedMutationCount;
            ActiveMutationPreparePromotedCommandCount = counters.ActiveMutationPreparePromotedCommandCount;
        }
    }

    public readonly struct GasRuntimeApiHealthEvidenceSnapshot
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

        public GasRuntimeApiHealthEvidenceSnapshot(in GasRuntimeCoreDiagnosticCounters counters)
        {
            QueryBudget = counters.QueryBudget;
            FilteredQueryBudget = counters.FilteredQueryBudget;
            UnfilteredQueryBudget = counters.UnfilteredQueryBudget;
            LookupUpdateBudget = counters.LookupUpdateBudget;
            RandomLookupBudget = counters.RandomLookupBudget;
            SyncQueryBudget = counters.SyncQueryBudget;
            HelperTempQueryRiskCount = counters.HelperTempQueryRiskCount;
            DependencyWaitRiskCount = counters.DependencyWaitRiskCount;
            WorldUpdateAllocatorOwnerCount = counters.WorldUpdateAllocatorOwnerCount;
            RewindableAllocatorCandidateCount = counters.RewindableAllocatorCandidateCount;
        }
    }

    public readonly struct GasRuntimeStructuralEvidenceSnapshot
    {
        public readonly int EntityCreateCount;
        public readonly int EntityDestroyCount;
        public readonly int EcbPlaybackCount;
        public readonly int RequiredStructuralPlaybackCount;
        public readonly int RecordedStructuralPlaybackCount;
        public readonly int EcbCommandCount;
        public readonly int BulkQueryCount;

        public int RuntimeStructuralApproximationCount => EntityCreateCount + EntityDestroyCount;

        public GasRuntimeStructuralEvidenceSnapshot(
            in GasRuntimeCoreDiagnosticCounters counters,
            in GasRuntimeFrameBackboneDiagnosticCounters frameBackbone)
        {
            EntityCreateCount = counters.EntityCreateCount;
            EntityDestroyCount = counters.EntityDestroyCount;
            EcbPlaybackCount = counters.EcbPlaybackCount;
            RequiredStructuralPlaybackCount = frameBackbone.RequiredStructuralPlaybackCount;
            RecordedStructuralPlaybackCount = frameBackbone.RecordedStructuralPlaybackCount;
            EcbCommandCount = frameBackbone.EcbCommandCount;
            BulkQueryCount = frameBackbone.BulkQueryCount;
        }
    }

    public readonly struct GasRuntimeFrameBackboneEvidenceSnapshot
    {
        public readonly int PhaseCount;
        public readonly int StreamCount;
        public readonly int MigrationCarrierCount;
        public readonly int ProfilerMarkerCount;
        public readonly int JournalingMarkerCount;
        public readonly int PhysicsDisabledReasonCount;
        public readonly int RenderDisabledReasonCount;
        public readonly int EvidenceMask;

        public GasRuntimeFrameBackboneEvidenceSnapshot(
            in GasRuntimeFrameBackboneDiagnosticCounters counters)
        {
            PhaseCount = counters.PhaseCount;
            StreamCount = counters.StreamCount;
            MigrationCarrierCount = counters.MigrationCarrierCount;
            ProfilerMarkerCount = counters.ProfilerMarkerCount;
            JournalingMarkerCount = counters.JournalingMarkerCount;
            PhysicsDisabledReasonCount = counters.PhysicsDisabledReasonCount;
            RenderDisabledReasonCount = counters.RenderDisabledReasonCount;
            EvidenceMask = counters.EvidenceMask;
        }
    }

    public readonly struct GasRuntimeObservationEvidenceSnapshot
    {
        public readonly int MaterializedQueryCount;
        public readonly int MaterializedEntityCount;
        public readonly int ElapsedMicroseconds;
        public readonly int PerformancePollutionRiskCount;

        public GasRuntimeObservationEvidenceSnapshot(
            in GasRuntimeObservationMaterializationCounters counters)
        {
            MaterializedQueryCount = counters.MaterializedQueryCount;
            MaterializedEntityCount = counters.MaterializedEntityCount;
            ElapsedMicroseconds = counters.ElapsedMicroseconds;
            PerformancePollutionRiskCount = counters.PerformancePollutionRiskCount;
        }
    }

    public readonly struct GasRuntimeMagnitudeSourceEvidenceSnapshot
    {
        public readonly int CurrentValueLookupCount;
        public readonly int CapturedValueHitCount;
        public readonly int CaptureMissCount;
        public readonly int CaptureMissLiveLookupCount;
        public readonly int FallbackValueCount;
        public readonly int FallbackFactCount;
        public readonly int SourceAttributeLookupCount;
        public readonly int TargetAttributeLookupCount;
        public readonly int ExecutionInputLookupCount;

        public GasRuntimeMagnitudeSourceEvidenceSnapshot(
            in GasRuntimeMagnitudeSourceCounters counters)
        {
            CurrentValueLookupCount = counters.CurrentValueLookupCount;
            CapturedValueHitCount = counters.CapturedValueHitCount;
            CaptureMissCount = counters.CaptureMissCount;
            CaptureMissLiveLookupCount = counters.CaptureMissLiveLookupCount;
            FallbackValueCount = counters.FallbackValueCount;
            FallbackFactCount = counters.FallbackFactCount;
            SourceAttributeLookupCount = counters.SourceAttributeLookupCount;
            TargetAttributeLookupCount = counters.TargetAttributeLookupCount;
            ExecutionInputLookupCount = counters.ExecutionInputLookupCount;
        }
    }

    public readonly struct GasRuntimeBufferPressureEvidenceSnapshot
    {
        public readonly int WarningCount;
        public readonly int PeakCount;
        public readonly int PeakCapacity;

        public GasRuntimeBufferPressureEvidenceSnapshot(
            int warningCount,
            int peakCount,
            int peakCapacity)
        {
            WarningCount = warningCount;
            PeakCount = peakCount;
            PeakCapacity = peakCapacity;
        }
    }

    public readonly struct GasRuntimeDiagnosticEventEvidenceSnapshot
    {
        private const string EffectCommandSpecStreamName = "EffectCommandSpecStream";

        public readonly int EventCount;
        public readonly int DroppedEventCount;
        public readonly int WarningCount;
        public readonly int ErrorCount;
        public readonly int SlowSystemCount;
        public readonly int BufferPressureWarningCount;
        public readonly int BlockingErrorCount;
        public readonly GasRuntimeBufferPressureEvidenceSnapshot EffectCommandStreamPressure;

        private GasRuntimeDiagnosticEventEvidenceSnapshot(
            int eventCount,
            int droppedEventCount,
            int warningCount,
            int errorCount,
            int slowSystemCount,
            int bufferPressureWarningCount,
            int blockingErrorCount,
            in GasRuntimeBufferPressureEvidenceSnapshot effectCommandStreamPressure)
        {
            EventCount = eventCount;
            DroppedEventCount = droppedEventCount;
            WarningCount = warningCount;
            ErrorCount = errorCount;
            SlowSystemCount = slowSystemCount;
            BufferPressureWarningCount = bufferPressureWarningCount;
            BlockingErrorCount = blockingErrorCount;
            EffectCommandStreamPressure = effectCommandStreamPressure;
        }

        public static GasRuntimeDiagnosticEventEvidenceSnapshot Create(
            in GasRuntimeDiagnosticStats stats,
            GASRuntimeDiagnosticEventBuffer[] rawEvents)
        {
            var events = rawEvents ?? Array.Empty<GASRuntimeDiagnosticEventBuffer>();
            var blockingErrorCount = 0;
            var effectCommandWarnings = 0;
            var effectCommandPeakCount = 0;
            var effectCommandPeakCapacity = 0;

            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Severity >= EGasRuntimeDiagnosticSeverity.Error
                    && evt.Kind != EGasRuntimeDiagnosticKind.SystemTiming
                    && evt.Kind != EGasRuntimeDiagnosticKind.TickSummary)
                {
                    blockingErrorCount++;
                }

                if (evt.Kind != EGasRuntimeDiagnosticKind.BufferPressure
                    || evt.Module != EGasRuntimeDiagnosticModule.Effect
                    || evt.GroupName.ToString() != EffectCommandSpecStreamName)
                {
                    continue;
                }

                if (evt.Severity >= EGasRuntimeDiagnosticSeverity.Warning
                    || evt.ValueB > 0)
                {
                    effectCommandWarnings++;
                }

                if (evt.Count > effectCommandPeakCount)
                {
                    effectCommandPeakCount = evt.Count;
                    effectCommandPeakCapacity = evt.Capacity;
                }
            }

            return new GasRuntimeDiagnosticEventEvidenceSnapshot(
                stats.RetainedEventCount,
                stats.DroppedEventCount,
                stats.WarningCount,
                stats.ErrorCount,
                stats.SlowSystemCount,
                stats.BufferPressureWarningCount,
                blockingErrorCount,
                new GasRuntimeBufferPressureEvidenceSnapshot(
                    effectCommandWarnings,
                    effectCommandPeakCount,
                    effectCommandPeakCapacity));
        }
    }

    public readonly struct GasRuntimeDiagnosticEvidenceSnapshot
    {
        public readonly GasRuntimeDiagnosticEventEvidenceSnapshot Events;
        public readonly GasRuntimeMetricFamilySnapshot MetricFamilies;
        public readonly GasRuntimeWorkloadEvidenceSnapshot Workload;
        public readonly GasRuntimeActiveEffectEvidenceSnapshot ActiveEffect;
        public readonly GasRuntimeActiveMutationEvidenceSnapshot ActiveMutation;
        public readonly GasRuntimeAttributeFactEvidenceSnapshot AttributeFact;
        public readonly GasRuntimeApiHealthEvidenceSnapshot ApiHealth;
        public readonly GasRuntimeStructuralEvidenceSnapshot Structural;
        public readonly GasRuntimeFrameBackboneEvidenceSnapshot FrameBackbone;
        public readonly GasRuntimeObservationEvidenceSnapshot Observation;
        public readonly GasRuntimeMagnitudeSourceEvidenceSnapshot MagnitudeSource;

        private GasRuntimeDiagnosticEvidenceSnapshot(
            in GasRuntimeDiagnosticEventEvidenceSnapshot events,
            in GasRuntimeMetricFamilySnapshot metricFamilies,
            in GasRuntimeWorkloadEvidenceSnapshot workload,
            in GasRuntimeActiveEffectEvidenceSnapshot activeEffect,
            in GasRuntimeActiveMutationEvidenceSnapshot activeMutation,
            in GasRuntimeAttributeFactEvidenceSnapshot attributeFact,
            in GasRuntimeApiHealthEvidenceSnapshot apiHealth,
            in GasRuntimeStructuralEvidenceSnapshot structural,
            in GasRuntimeFrameBackboneEvidenceSnapshot frameBackbone,
            in GasRuntimeObservationEvidenceSnapshot observation,
            in GasRuntimeMagnitudeSourceEvidenceSnapshot magnitudeSource)
        {
            Events = events;
            MetricFamilies = metricFamilies;
            Workload = workload;
            ActiveEffect = activeEffect;
            ActiveMutation = activeMutation;
            AttributeFact = attributeFact;
            ApiHealth = apiHealth;
            Structural = structural;
            FrameBackbone = frameBackbone;
            Observation = observation;
            MagnitudeSource = magnitudeSource;
        }

        public static GasRuntimeDiagnosticEvidenceSnapshot Create(
            in GasRuntimeDiagnosticStats stats,
            in GasRuntimeCoreDiagnosticCounters core,
            in GasRuntimeFrameBackboneDiagnosticCounters frameBackbone,
            in GasRuntimeObservationMaterializationCounters observation,
            in GasRuntimeMagnitudeSourceCounters magnitudeSource,
            in GasRuntimeMetricFamilySnapshot metricFamilies,
            GASRuntimeDiagnosticEventBuffer[] rawEvents)
        {
            return new GasRuntimeDiagnosticEvidenceSnapshot(
                GasRuntimeDiagnosticEventEvidenceSnapshot.Create(stats, rawEvents),
                metricFamilies,
                new GasRuntimeWorkloadEvidenceSnapshot(core),
                new GasRuntimeActiveEffectEvidenceSnapshot(core),
                new GasRuntimeActiveMutationEvidenceSnapshot(core),
                new GasRuntimeAttributeFactEvidenceSnapshot(core),
                new GasRuntimeApiHealthEvidenceSnapshot(core),
                new GasRuntimeStructuralEvidenceSnapshot(core, frameBackbone),
                new GasRuntimeFrameBackboneEvidenceSnapshot(frameBackbone),
                new GasRuntimeObservationEvidenceSnapshot(observation),
                new GasRuntimeMagnitudeSourceEvidenceSnapshot(magnitudeSource));
        }
    }
}
