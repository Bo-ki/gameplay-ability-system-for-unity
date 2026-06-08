using System.Globalization;
using System.Text;

namespace GAS.Runtime
{
    public static class GasRuntimeDerivedExportSink
    {
        public static string ExportToText(
            in GasRuntimeDiagnosticSnapshot snapshot,
            in GasRuntimeDataOrientedScorecard scorecard,
            int maxEvents = 0)
        {
            var diagnosticsText = GasRuntimeDebugger.ExportToText(snapshot, maxEvents);
            var builder = new StringBuilder(diagnosticsText.Length + 3072);
            builder.Append(diagnosticsText);
            AppendDiagnosticEvidenceSnapshot(builder, snapshot.Evidence);
            AppendMetricFamilySnapshot(builder, snapshot.MetricFamilies);
            AppendDataOrientedScorecard(builder, scorecard);
            AppendHotspotAttributionMatrix(builder, scorecard);
            return builder.ToString();
        }

        public static string ExportDataOrientedScorecardToText(
            in GasRuntimeDataOrientedScorecard scorecard)
        {
            var builder = new StringBuilder(1024);
            AppendDataOrientedScorecard(builder, scorecard);
            AppendHotspotAttributionMatrix(builder, scorecard);
            return builder.ToString();
        }

        private static void AppendDiagnosticEvidenceSnapshot(
            StringBuilder builder,
            in GasRuntimeDiagnosticEvidenceSnapshot evidence)
        {
            builder.Append("runtimeDiagnosticEvidenceSnapshot|source=GasRuntimeDiagnosticEvidenceSnapshot")
                .Append("|passMode=")
                .Append(nameof(GasRuntimeDiagnosticsPassMode.DerivedExport))
                .Append("|costDomain=")
                .Append(nameof(GasRuntimeDiagnosticsCostDomain.Debugger))
                .Append("|evidenceTier=")
                .Append(nameof(GasRuntimeDiagnosticsEvidenceTier.DerivedExport))
                .Append("|events=")
                .Append(evidence.Events.EventCount)
                .Append("|dropped=")
                .Append(evidence.Events.DroppedEventCount)
                .Append("|warnings=")
                .Append(evidence.Events.WarningCount)
                .Append("|errors=")
                .Append(evidence.Events.ErrorCount)
                .Append("|blockingErrors=")
                .Append(evidence.Events.BlockingErrorCount)
                .Append("|effectCommandStreamPressureWarnings=")
                .Append(evidence.Events.EffectCommandStreamPressure.WarningCount)
                .Append("|effectCommandStreamPeakCount=")
                .Append(evidence.Events.EffectCommandStreamPressure.PeakCount)
                .Append("|effectCommandStreamPeakCapacity=")
                .Append(evidence.Events.EffectCommandStreamPressure.PeakCapacity)
                .Append("|workloadRequests=")
                .Append(evidence.Workload.RequestCount)
                .Append("|workloadFacts=")
                .Append(evidence.Workload.FactCount)
                .Append("|activeMutationCommands=")
                .Append(evidence.ActiveMutation.CommandCount)
                .Append("|pendingAttributeAppliedDeltas=")
                .Append(evidence.AttributeFact.PendingAppliedDeltaCount)
                .Append("|runtimeStructuralApprox=")
                .Append(evidence.Structural.RuntimeStructuralApproximationCount)
                .AppendLine();
        }

        private static void AppendMetricFamilySnapshot(
            StringBuilder builder,
            in GasRuntimeMetricFamilySnapshot metricFamilies)
        {
            builder.Append("runtimeMetricFamilySnapshot|source=GasRuntimeMetricFamilySnapshot")
                .Append("|passMode=")
                .Append(nameof(GasRuntimeDiagnosticsPassMode.DerivedExport))
                .Append("|costDomain=")
                .Append(nameof(GasRuntimeDiagnosticsCostDomain.Debugger))
                .Append("|evidenceTier=")
                .Append(nameof(GasRuntimeDiagnosticsEvidenceTier.DerivedExport))
                .Append("|metricFamilyMask=0x")
                .Append(((int)metricFamilies.MetricFamilyMask).ToString("X", CultureInfo.InvariantCulture))
                .Append("|workloadRequests=")
                .Append(metricFamilies.Workload.RequestCount)
                .Append("|workloadSpecs=")
                .Append(metricFamilies.Workload.SpecCount)
                .Append("|workloadDeltas=")
                .Append(metricFamilies.Workload.DeltaCount)
                .Append("|workloadFacts=")
                .Append(metricFamilies.Workload.FactCount)
                .Append("|workloadCues=")
                .Append(metricFamilies.Workload.CueCount)
                .Append("|workloadPresentations=")
                .Append(metricFamilies.Workload.PresentationCount)
                .Append("|gasConceptActiveMutationCommands=")
                .Append(metricFamilies.GasConcept.ActiveMutationCommandCount)
                .Append("|gasConceptPendingAttributeDeltas=")
                .Append(metricFamilies.GasConcept.PendingAttributeDeltaCount)
                .Append("|gasConceptOwnerLocalFacts=")
                .Append(metricFamilies.GasConcept.OwnerLocalFactCount)
                .Append("|gasConceptActiveEffectSlots=")
                .Append(metricFamilies.GasConcept.ActiveEffectSlotCount)
                .Append("|gasConceptDuePeriodSlots=")
                .Append(metricFamilies.GasConcept.ActiveEffectDuePeriodSlotCount)
                .Append("|gasConceptMagnitudeSourceLookups=")
                .Append(metricFamilies.GasConcept.MagnitudeSourceLookupCount)
                .Append("|gasConceptMagnitudeSourceCaptureMisses=")
                .Append(metricFamilies.GasConcept.MagnitudeSourceCaptureMissCount)
                .Append("|gasConceptMagnitudeSourceFallbacks=")
                .Append(metricFamilies.GasConcept.MagnitudeSourceFallbackCount)
                .Append("|dataShapeActiveMutationOwnerGroups=")
                .Append(metricFamilies.DataShape.ActiveMutationOwnerGroupCount)
                .Append("|dataShapeActiveMutationMaxOwnerRange=")
                .Append(metricFamilies.DataShape.ActiveMutationMaxOwnerRange)
                .Append("|dataShapePendingAttributeTargetGroups=")
                .Append(metricFamilies.DataShape.PendingAttributeTargetGroupCount)
                .Append("|dataShapePendingAttributeMaxTargetRange=")
                .Append(metricFamilies.DataShape.PendingAttributeMaxTargetRange)
                .Append("|dataShapeOwnerLocalFactOwnerGroups=")
                .Append(metricFamilies.DataShape.OwnerLocalFactOwnerGroupCount)
                .Append("|dataShapeOwnerLocalFactMaxOwnerRange=")
                .Append(metricFamilies.DataShape.OwnerLocalFactMaxOwnerRange)
                .Append("|dataShapeOwnerLocalFactFlushes=")
                .Append(metricFamilies.DataShape.OwnerLocalFactFlushCount)
                .Append("|dataShapeOwnerLocalFactChangedChunks=")
                .Append(metricFamilies.DataShape.OwnerLocalFactChangedChunkCount)
                .Append("|dataShapeOwnerLocalFactScannedOwners=")
                .Append(metricFamilies.DataShape.OwnerLocalFactScannedOwnerCount)
                .Append("|dataShapeOwnerLocalFactDirtyOwners=")
                .Append(metricFamilies.DataShape.OwnerLocalFactDirtyOwnerCount)
                .Append("|dataShapeOwnerLocalFactSkippedOwners=")
                .Append(metricFamilies.DataShape.OwnerLocalFactSkippedOwnerCount)
                .Append("|dataShapeOwnerLocalFactClearedOwners=")
                .Append(metricFamilies.DataShape.OwnerLocalFactClearedOwnerCount)
                .Append("|dataShapeActiveEffectSlots=")
                .Append(metricFamilies.DataShape.ActiveEffectSlotCount)
                .Append("|dataShapeActiveEffectSlotCapacity=")
                .Append(metricFamilies.DataShape.ActiveEffectSlotCapacity)
                .Append("|dataShapeSourceSnapshotCapacity=")
                .Append(metricFamilies.DataShape.ActiveEffectSlotSourceSnapshotCapacity)
                .Append("|dataShapeSourceSnapshotSpills=")
                .Append(metricFamilies.DataShape.ActiveEffectSlotSourceSnapshotSpillCount)
                .Append("|apiHealthQueryBudget=")
                .Append(metricFamilies.ApiHealth.QueryBudget)
                .Append("|apiHealthFilteredQueryBudget=")
                .Append(metricFamilies.ApiHealth.FilteredQueryBudget)
                .Append("|apiHealthUnfilteredQueryBudget=")
                .Append(metricFamilies.ApiHealth.UnfilteredQueryBudget)
                .Append("|apiHealthLookupUpdateBudget=")
                .Append(metricFamilies.ApiHealth.LookupUpdateBudget)
                .Append("|apiHealthRandomLookupBudget=")
                .Append(metricFamilies.ApiHealth.RandomLookupBudget)
                .Append("|apiHealthSyncQueryBudget=")
                .Append(metricFamilies.ApiHealth.SyncQueryBudget)
                .Append("|apiHealthHelperTempQueryRisks=")
                .Append(metricFamilies.ApiHealth.HelperTempQueryRiskCount)
                .Append("|apiHealthDependencyWaitRisks=")
                .Append(metricFamilies.ApiHealth.DependencyWaitRiskCount)
                .Append("|apiHealthWorldUpdateAllocatorOwners=")
                .Append(metricFamilies.ApiHealth.WorldUpdateAllocatorOwnerCount)
                .Append("|apiHealthRewindableAllocatorCandidates=")
                .Append(metricFamilies.ApiHealth.RewindableAllocatorCandidateCount)
                .Append("|apiHealthActiveMutationEstimatedRandomLookups=")
                .Append(metricFamilies.ApiHealth.ActiveMutationEstimatedRandomLookupCount)
                .Append("|apiHealthPendingAttributeEstimatedRandomLookups=")
                .Append(metricFamilies.ApiHealth.PendingAttributeEstimatedRandomLookupCount)
                .Append("|structuralEntityCreates=")
                .Append(metricFamilies.Structural.EntityCreateCount)
                .Append("|structuralEntityDestroys=")
                .Append(metricFamilies.Structural.EntityDestroyCount)
                .Append("|structuralEcbPlaybacks=")
                .Append(metricFamilies.Structural.EcbPlaybackCount)
                .Append("|structuralRequiredPlaybacks=")
                .Append(metricFamilies.Structural.RequiredStructuralPlaybackCount)
                .Append("|structuralRecordedPlaybacks=")
                .Append(metricFamilies.Structural.RecordedStructuralPlaybackCount)
                .Append("|structuralEcbCommands=")
                .Append(metricFamilies.Structural.EcbCommandCount)
                .Append("|structuralBulkQueries=")
                .Append(metricFamilies.Structural.BulkQueryCount)
                .Append("|overheadObservationMaterializedQueries=")
                .Append(metricFamilies.Overhead.ObservationMaterializedQueryCount)
                .Append("|overheadObservationMaterializedEntities=")
                .Append(metricFamilies.Overhead.ObservationMaterializedEntityCount)
                .Append("|overheadObservationElapsedUs=")
                .Append(metricFamilies.Overhead.ObservationMaterializationElapsedMicroseconds)
                .Append("|overheadObservationPollutionRisks=")
                .Append(metricFamilies.Overhead.ObservationPerformancePollutionRiskCount)
                .Append("|overheadProfilerMarkers=")
                .Append(metricFamilies.Overhead.ProfilerMarkerCount)
                .Append("|overheadJournalingMarkers=")
                .Append(metricFamilies.Overhead.JournalingMarkerCount)
                .Append("|overheadDebuggerBudgetUs=")
                .Append(metricFamilies.Overhead.DebuggerOverheadBudgetMicroseconds)
                .Append("|overheadSamplingInterval=")
                .Append(metricFamilies.Overhead.SamplingInterval)
                .AppendLine();
        }

        private static void AppendDataOrientedScorecard(
            StringBuilder builder,
            in GasRuntimeDataOrientedScorecard scorecard)
        {
            builder.Append("runtimeDataOrientedScorecard|source=GasRuntimeDataOrientedScorecard")
                .Append("|readModel=PerformanceTiming+MetricFamilySnapshot")
                .Append("|passMode=")
                .Append(nameof(GasRuntimeDiagnosticsPassMode.DerivedExport))
                .Append("|costDomain=")
                .Append(nameof(GasRuntimeDiagnosticsCostDomain.Debugger))
                .Append("|evidenceTier=")
                .Append(nameof(GasRuntimeDiagnosticsEvidenceTier.ValidationEvidence))
                .Append("|units=")
                .Append(scorecard.UnitCount)
                .Append("|measuredTicks=")
                .Append(scorecard.MeasuredTicks)
                .Append("|commands=")
                .Append(scorecard.CommandCount)
                .Append("|commandsPerMeasuredTick=");
            AppendInvariantDouble(builder, scorecard.CommandsPerMeasuredTick);
            builder.Append("|coreFacts=")
                .Append(scorecard.CoreFactCount)
                .Append("|coreFactsPerMeasuredTick=");
            AppendInvariantDouble(builder, scorecard.CoreFactsPerMeasuredTick);
            builder.Append("|activeMutationCommands=")
                .Append(scorecard.ActiveMutationCommandCount)
                .Append("|activeMutationOwnerGroups=")
                .Append(scorecard.ActiveMutationOwnerGroupCount)
                .Append("|activeMutationMaxOwnerRange=")
                .Append(scorecard.ActiveMutationMaxOwnerRange)
                .Append("|activeMutationEstimatedRandomLookups=")
                .Append(scorecard.ActiveMutationEstimatedRandomLookupCount)
                .Append("|pendingAttributeDeltas=")
                .Append(scorecard.PendingAttributeDeltaCount)
                .Append("|pendingAttributeTargetGroups=")
                .Append(scorecard.PendingAttributeTargetGroupCount)
                .Append("|pendingAttributeMaxTargetRange=")
                .Append(scorecard.PendingAttributeMaxTargetRange)
                .Append("|pendingAttributeEstimatedRandomLookups=")
                .Append(scorecard.PendingAttributeEstimatedRandomLookupCount)
                .Append("|ownerLocalFacts=")
                .Append(scorecard.OwnerLocalFactCount)
                .Append("|ownerLocalFactOwnerGroups=")
                .Append(scorecard.OwnerLocalFactOwnerGroupCount)
                .Append("|ownerLocalFactMaxOwnerRange=")
                .Append(scorecard.OwnerLocalFactMaxOwnerRange)
                .Append("|ownerLocalFactFlushes=")
                .Append(scorecard.OwnerLocalFactFlushCount)
                .Append("|ownerLocalFactChangedChunks=")
                .Append(scorecard.OwnerLocalFactChangedChunkCount)
                .Append("|ownerLocalFactScannedOwners=")
                .Append(scorecard.OwnerLocalFactScannedOwnerCount)
                .Append("|ownerLocalFactDirtyOwners=")
                .Append(scorecard.OwnerLocalFactDirtyOwnerCount)
                .Append("|ownerLocalFactSkippedOwners=")
                .Append(scorecard.OwnerLocalFactSkippedOwnerCount)
                .Append("|ownerLocalFactClearedOwners=")
                .Append(scorecard.OwnerLocalFactClearedOwnerCount)
                .Append("|activeEffectSlots=")
                .Append(scorecard.ActiveEffectSlotCount)
                .Append("|activeEffectSlotCapacity=")
                .Append(scorecard.ActiveEffectSlotCapacity)
                .Append("|activeEffectDuePeriodSlots=")
                .Append(scorecard.ActiveEffectChunkSkipDuePeriodSlotCount)
                .Append("|queryBudget=")
                .Append(scorecard.QueryBudget)
                .Append("|lookupUpdateBudget=")
                .Append(scorecard.LookupUpdateBudget)
                .Append("|randomLookupBudget=")
                .Append(scorecard.RandomLookupBudget)
                .Append("|syncQueryBudget=")
                .Append(scorecard.SyncQueryBudget)
                .Append("|dependencyWaitRisks=")
                .Append(scorecard.DependencyWaitRiskCount)
                .Append("|performanceObservationPollutionRisks=")
                .Append(scorecard.PerformanceObservationPollutionRiskCount)
                .Append("|metricFamilyMask=0x")
                .Append(((int)scorecard.MetricFamilyMask).ToString("X", CultureInfo.InvariantCulture))
                .Append("|dominantRisk=")
                .Append(scorecard.DominantRisk)
                .Append("|performanceTimingAvailable=")
                .Append(scorecard.PerformanceTimingAvailable)
                .Append("|profilerEvidencePassed=")
                .Append(scorecard.ProfilerEvidencePassed)
                .Append("|measuredAvgTickMs=");
            AppendInvariantDouble(builder, scorecard.MeasuredAverageTickMilliseconds);
            builder.Append("|gasTickAvgMs=");
            AppendInvariantDouble(builder, scorecard.GasTickAverageMilliseconds);
            builder.Append("|gasTickMaxMs=");
            AppendInvariantDouble(builder, scorecard.GasTickMaxMilliseconds);
            builder.Append("|coreRuntimeAvgMs=");
            AppendInvariantDouble(builder, scorecard.CoreRuntimeAverageMilliseconds);
            builder.Append("|coreSimulationAvgMs=");
            AppendInvariantDouble(builder, scorecard.CoreSimulationAverageMilliseconds);
            builder.Append("|boundaryAvgMs=");
            AppendInvariantDouble(builder, scorecard.BoundaryAverageMilliseconds);
            builder.Append("|runnerAvgMs=");
            AppendInvariantDouble(builder, scorecard.RunnerAverageMilliseconds);
            builder.Append("|measuredUsPerUnit=");
            AppendInvariantDouble(builder, scorecard.MeasuredMicrosecondsPerUnit);
            builder.Append("|gasTickUsPerUnit=");
            AppendInvariantDouble(builder, scorecard.GasTickMicrosecondsPerUnit);
            builder.Append("|coreSimulationUsPerUnit=");
            AppendInvariantDouble(builder, scorecard.CoreSimulationMicrosecondsPerUnit);
            builder.Append("|gasTickUsPerCommand=");
            AppendInvariantDouble(builder, scorecard.GasTickMicrosecondsPerCommand);
            builder.Append("|coreSimulationUsPerCoreFact=");
            AppendInvariantDouble(builder, scorecard.CoreSimulationMicrosecondsPerCoreFact);
            builder.Append("|profilerCaptureState=")
                .Append(scorecard.ProfilerCaptureState)
                .AppendLine();
        }

        private static void AppendHotspotAttributionMatrix(
            StringBuilder builder,
            in GasRuntimeDataOrientedScorecard scorecard)
        {
            AppendHotspotAttribution(
                builder,
                "GAS-ARCH-07",
                "High",
                "GameplayFact",
                nameof(GASBoundaryProjectionSystemGroup),
                "OwnerLocalFactDirtySpan",
                "GameplayBoundaryFactExportSystem",
                "OwnerLocalGameplayFactBuffer",
                "OwnerLocalFactFlush",
                scorecard.OwnerLocalFactFlushCount,
                "OwnerLocality",
                "R3",
                "generated dirty owner/fact span lane",
                "ownerLocalFactMaxOwnerRange=" + scorecard.OwnerLocalFactMaxOwnerRange
                + ";ownerLocalFactDirtyOwners=" + scorecard.OwnerLocalFactDirtyOwnerCount
                + ";ownerLocalFactSkippedOwners=" + scorecard.OwnerLocalFactSkippedOwnerCount);

            AppendHotspotAttribution(
                builder,
                "GAS-ARCH-04",
                "High",
                "AttributeFactFanIn",
                nameof(GASCoreSimulationSystemGroup),
                "PendingAttributeDeltaApply",
                "GASAttributeModifierDeltaApplySystem",
                "OwnerLocalGameplayFactBuffer",
                "OwnerLocalFactAppend",
                scorecard.PendingAttributeDeltaCount,
                "BroadBufferRW",
                "R3",
                "fact reduce/apply lane",
                "pendingAttributeMaxTargetRange=" + scorecard.PendingAttributeMaxTargetRange);

            AppendHotspotAttribution(
                builder,
                "GAS-ARCH-ActiveEffect",
                "High",
                "ActiveEffectLifecycle",
                nameof(GASCoreSimulationSystemGroup),
                "ActiveEffectPreTick",
                "GASActiveEffectPreTickSystem",
                "ActiveGameplayEffectBuffer",
                "ActiveSlotTick",
                scorecard.ActiveEffectSlotCount,
                "PerFrameSlotScan",
                "R7/R3",
                "dirty/due active-effect slot lane",
                "activeEffectDuePeriodSlots=" + scorecard.ActiveEffectChunkSkipDuePeriodSlotCount);

            AppendHotspotAttribution(
                builder,
                "GAS-ARCH-01",
                "High",
                "RuntimeCoreApiHealth",
                "AllRuntimeGroups",
                "DependencyAndSync",
                "GASDependencyDrain",
                "ComponentLookup+BufferLookup",
                "DependencyWait",
                scorecard.DependencyWaitRiskCount + scorecard.SyncQueryBudget,
                "DependencyWait",
                "R4",
                "per-owner API health attribution",
                "syncQueryBudget=" + scorecard.SyncQueryBudget);

            AppendHotspotAttribution(
                builder,
                "GAS-DBG-01",
                "High",
                "DebuggerObservation",
                "DebuggerOwner",
                "DiagnosticMaterialization",
                "DiagnosticsSnapshotSystem",
                "ToEntityArray",
                "Materialization",
                scorecard.PerformanceObservationPollutionRiskCount,
                "ObservationPollution",
                "R4",
                "budgeted diagnostic materialization owner",
                "performanceObservationPollutionRisks=" + scorecard.PerformanceObservationPollutionRiskCount);

            AppendHotspotAttribution(
                builder,
                "GAS-MEASURE-02",
                "Medium",
                "OfficialToolEvidence",
                "Validation",
                "ProfilerCapture",
                "GasRuntimeOfficialToolDiff",
                "Profiler",
                "ProfilerEvidence",
                scorecard.ProfilerEvidencePassed ? 0 : 1,
                "MissingProfilerEvidence",
                "R8",
                "profiler-enabled scale gate",
                "profilerCaptureState=" + scorecard.ProfilerCaptureState);
        }

        private static void AppendHotspotAttribution(
            StringBuilder builder,
            string id,
            string severity,
            string gasConcept,
            string phase,
            string lane,
            string system,
            string buffer,
            string operation,
            int count,
            string dotsRisk,
            string nextOwner,
            string recommendation,
            string evidence)
        {
            if (count <= 0)
                return;

            builder.Append("hotspotAttribution|source=GasRuntimeDerivedExportSink")
                .Append("|readModel=PerformanceTiming+MetricFamilySnapshot")
                .Append("|passMode=")
                .Append(nameof(GasRuntimeDiagnosticsPassMode.DerivedExport))
                .Append("|costDomain=")
                .Append(nameof(GasRuntimeDiagnosticsCostDomain.Debugger))
                .Append("|evidenceTier=")
                .Append(nameof(GasRuntimeDiagnosticsEvidenceTier.ValidationEvidence))
                .Append("|id=")
                .Append(id)
                .Append("|severity=")
                .Append(severity)
                .Append("|gasConcept=")
                .Append(gasConcept)
                .Append("|phase=")
                .Append(phase)
                .Append("|lane=")
                .Append(lane)
                .Append("|system=")
                .Append(system)
                .Append("|buffer=")
                .Append(buffer)
                .Append("|operation=")
                .Append(operation)
                .Append("|count=")
                .Append(count)
                .Append("|dotsRisk=")
                .Append(dotsRisk)
                .Append("|nextOwner=")
                .Append(nextOwner)
                .Append("|recommendation=")
                .Append(recommendation)
                .Append("|evidence=")
                .Append(evidence)
                .AppendLine();
        }

        private static void AppendInvariantDouble(StringBuilder builder, double value)
        {
            builder.Append(value.ToString("G9", CultureInfo.InvariantCulture));
        }
    }
}
