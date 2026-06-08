using System;
using System.Text;
using GAS.Runtime;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessBattleValidationReport
    {
        public static AutoChessValidationEvidence CreateEvidence(
            AutoChessGeneratedScenarioProfile scenario,
            in AutoChessBattleResult result,
            in AutoChessPresentationSnapshot presentation,
            bool officialDiffSeparatePass)
        {
            return CreateEvidence(
                scenario,
                result,
                result,
                presentation,
                officialDiffSeparatePass);
        }

        public static AutoChessValidationEvidence CreateEvidence(
            AutoChessGeneratedScenarioProfile scenario,
            in AutoChessBattleResult performanceResult,
            in AutoChessBattleResult diagnosticResult,
            in AutoChessPresentationSnapshot presentation,
            bool officialDiffSeparatePass)
        {
            var diagnosticEvidence = diagnosticResult.RuntimeDiagnostics.Evidence;
            var performanceEvidence = performanceResult.RuntimeDiagnostics.Evidence;
            var eventEvidence = diagnosticEvidence.Events;
            var workload = diagnosticEvidence.Workload;
            var activeEffect = diagnosticEvidence.ActiveEffect;
            var mutation = diagnosticEvidence.ActiveMutation;
            var attributeFact = diagnosticEvidence.AttributeFact;
            var apiHealth = diagnosticEvidence.ApiHealth;
            var backbone = diagnosticEvidence.FrameBackbone;
            var observation = diagnosticEvidence.Observation;
            var performanceObservation = performanceEvidence.Observation;
            var magnitudeSource = diagnosticEvidence.MagnitudeSource;
            var factsHash = CalculateFactsHash(performanceResult.StructuredLogSnapshot);
            var summaryHash = CalculateSummaryHash(performanceResult, presentation.RuntimeMarkerCount, factsHash);
            var streamCarrierPressure = CalculateStreamCarrierPressure(diagnosticResult.RuntimeDiagnostics);
            var reselectTriggerMask = 0;
            if (apiHealth.RandomLookupBudget > 0)
                reselectTriggerMask |= 1;
            if (performanceResult.DriverIssuedCommands > 1000)
                reselectTriggerMask |= 2;
            if (mutation.MigrationCarrierCount > 0)
                reselectTriggerMask |= 4;
            if (mutation.EstimatedRandomLookupCount > 0)
                reselectTriggerMask |= 8;
            if (streamCarrierPressure.WarningCount > 0)
                reselectTriggerMask |= 16;
            if (performanceObservation.PerformancePollutionRiskCount > 0)
                reselectTriggerMask |= 32;
            if (magnitudeSource.CaptureMissLiveLookupCount > 0
                || magnitudeSource.FallbackValueCount > 0
                || magnitudeSource.FallbackFactCount > 0)
            {
                reselectTriggerMask |= 64;
            }

            var traceAbilityCode = AutoChessBattleRules.AbilityPlayerExecute;
            AutoChessBattleDefinitionCatalogBuilder.TryCreateRuntimeConceptEvidence(
                traceAbilityCode,
                performanceResult.BattleTicks,
                out var gasConceptCoverage,
                out var runtimeTrace);

            return new AutoChessValidationEvidence(
                performanceResult.Completed,
                performanceResult.Winner,
                scenario.ExpectedWinner,
                performanceResult.ScenarioScale,
                performanceResult.Units.Length,
                performanceResult.BattleTicks,
                performanceResult.TotalTicks,
                performanceResult.WarmupDroppedTicks,
                performanceResult.MeasuredTicks,
                performanceResult.DriverIssuedCommands,
                performanceResult.EventCounts.AttributeChanges,
                performanceResult.EventCounts.PeriodTickDamageFacts,
                performanceResult.EventCounts.PeriodTickDamageTotal,
                performanceResult.EventCounts.ExecutionCalculationOutputUpdated,
                performanceResult.DriverExecutionSpecScans,
                performanceResult.DriverExecutionMatchedEffectSpecs,
                performanceResult.DriverExecutionTargetOwnerMismatches,
                performanceResult.DriverExecutionMissingAttributes,
                performanceResult.DriverExecutionEvaluatorRejects,
                performanceResult.DriverExecutionOutputWrites,
                performanceResult.EventCounts.CueRequests,
                eventEvidence.EventCount,
                eventEvidence.WarningCount,
                eventEvidence.ErrorCount,
                eventEvidence.BlockingErrorCount,
                workload.RequestCount,
                workload.FactCount,
                workload.DeltaCount,
                workload.CueCount,
                presentation.RuntimeMarkerCount,
                presentation.SourceLineCount,
                presentation.DisplayLineCount,
                presentation.DroppedLineCount,
                workload.PeakEventBusBufferLength,
                workload.PeakReplayCursorLag,
                scenario.ProcessWarmupRuns,
                backbone.EvidenceMask,
                reselectTriggerMask,
                AutoChessGasRuntimeAccessContract.EntryCount,
                AutoChessGasRuntimeAccessContract.EcsHandleProxyCount,
                AutoChessGasRuntimeAccessContract.ManualSyncCount,
                AutoChessGasRuntimeAccessContract.PerformancePassRiskCount,
                AutoChessGasRuntimeAccessContract.BattleHashAffectingCount,
                AutoChessGasRuntimeAccessContract.CapabilityMask,
                gasConceptCoverage.CoveredConceptMask,
                gasConceptCoverage.MissingConceptMask,
                traceAbilityCode,
                runtimeTrace.StageMaskValue,
                runtimeTrace.MissingStageMaskValue,
                runtimeTrace.SeedCount,
                runtimeTrace.ModifierCount,
                runtimeTrace.FactCount,
                runtimeTrace.CueCount,
                runtimeTrace.ActiveMutationSeedCount,
                runtimeTrace.ExecutionCalculationModifierCount,
                activeEffect.SlotCount,
                activeEffect.SlotActiveCount,
                activeEffect.ChunkSkipDuePeriodSlotCount,
                mutation.CommandCount,
                mutation.OwnerGroupCount,
                mutation.MaxOwnerRange,
                mutation.SortMoveCount,
                mutation.EstimatedRandomLookupCount,
                mutation.OwnerResourceLookupCount,
                mutation.MigrationCarrierCount,
                attributeFact.PendingDeltaCount,
                attributeFact.PendingAppliedDeltaCount,
                attributeFact.PendingSkippedDeltaCount,
                attributeFact.PendingTargetGroupCount,
                attributeFact.PendingMaxTargetRange,
                attributeFact.PendingEstimatedRandomLookupCount,
                attributeFact.PendingFactPatchCount,
                attributeFact.PendingMigrationCarrierCount,
                attributeFact.OwnerLocalFactCount,
                attributeFact.OwnerLocalFactOwnerGroupCount,
                attributeFact.OwnerLocalFactMaxOwnerRange,
                attributeFact.OwnerLocalFactFlushCount,
                streamCarrierPressure.WarningCount,
                streamCarrierPressure.PeakCount,
                streamCarrierPressure.PeakCapacity,
                observation.MaterializedQueryCount,
                observation.MaterializedEntityCount,
                observation.ElapsedMicroseconds,
                performanceObservation.PerformancePollutionRiskCount,
                magnitudeSource.CurrentValueLookupCount,
                magnitudeSource.CapturedValueHitCount,
                magnitudeSource.CaptureMissCount,
                magnitudeSource.CaptureMissLiveLookupCount,
                magnitudeSource.FallbackValueCount,
                magnitudeSource.FallbackFactCount,
                magnitudeSource.SourceAttributeLookupCount,
                magnitudeSource.TargetAttributeLookupCount,
                magnitudeSource.ExecutionInputLookupCount,
                performanceResult.ElapsedMilliseconds,
                performanceResult.AverageTickMilliseconds,
                factsHash,
                summaryHash,
                true,
                officialDiffSeparatePass,
                performanceResult.OfficialToolDiff.JournalingAvailable,
                performanceResult.OfficialToolDiff.JournalingCaptured,
                performanceResult.OfficialToolDiff.ProfilerAvailable,
                performanceResult.OfficialToolDiff.ProfilerCaptureState,
                backbone.PhysicsDisabledReasonCount > 0 ? "reported-by-runtime" : "headless-profile-disabled",
                backbone.RenderDisabledReasonCount > 0 ? "reported-by-runtime" : "headless-profile-disabled",
                presentation.DisabledReason);
        }

        public static string CreateRunResultSummary(in AutoChessValidationRunResult runResult)
        {
            return $"passed={runResult.Passed}, "
                   + $"thresholdsPassed={runResult.GeneratedThresholdsPassed}, "
                   + $"headlessLogicBudgetPassed={runResult.HeadlessLogicBudget.Passed}, "
                   + $"performanceExcellentPassed={runResult.HeadlessLogicBudget.PerformanceExcellentPassed}, "
                   + $"runtimeChainPassed={runResult.RuntimeChainPassed}, "
                   + $"repeatRunProbe={runResult.HasRepeatRunEvidence}, "
                   + $"repeatRunPassed={(!runResult.HasRepeatRunEvidence || runResult.RepeatRunEvidence.Passed)}, "
                   + $"officialDiffRequired={runResult.RequireOfficialToolDiff}, "
                   + $"presentationMarkers={runResult.Presentation.RuntimeMarkerCount}, "
                   + $"presentationDisplayedLines={runResult.Presentation.DisplayLineCount}, "
                   + $"presentationDroppedLines={runResult.Presentation.DroppedLineCount}, "
                   + $"presentationDisabledReason={runResult.Presentation.DisabledReason}, "
                   + $"summaryHash=0x{runResult.Evidence.SummaryHash:X8}";
        }

        public static string CreateHeadlessLogicBudgetSummary(
            in AutoChessHeadlessLogicBudgetResult budget)
        {
            return $"passed={budget.Passed}, "
                   + $"performanceExcellentPassed={budget.PerformanceExcellentPassed}, "
                   + $"failureMask=0x{budget.FailureMask:X}, "
                   + $"performanceTimingAvailable={budget.PerformanceTimingAvailable}, "
                   + $"scorecardSource=GasRuntimeDataOrientedScorecard, "
                   + $"metricFamilySource=DiagnosticPassGasData+PerformancePassOverhead, "
                   + $"metricFamilyMask=0x{((int)budget.RuntimeScorecard.MetricFamilyMask):X}, "
                   + $"dominantRisk={budget.RuntimeScorecard.DominantRisk}, "
                   + $"units={budget.UnitCount}, "
                   + $"measuredTicks={budget.MeasuredTicks}, "
                   + $"commands={budget.CommandCount}, "
                   + $"commandsPerMeasuredTick={budget.CommandsPerMeasuredTick:0.000}, "
                   + $"coreFacts={budget.CoreFactCount}, "
                   + $"coreFactsPerMeasuredTick={budget.CoreFactsPerMeasuredTick:0.000}, "
                   + $"activeMutationCommands={budget.ActiveMutationCommandCount}, "
                   + $"activeMutationOwnerGroups={budget.ActiveMutationOwnerGroupCount}, "
                   + $"activeMutationMaxOwnerRange={budget.ActiveMutationMaxOwnerRange}, "
                   + $"activeMutationEstimatedRandomLookups="
                   + $"{budget.ActiveMutationEstimatedRandomLookupCount}, "
                   + $"pendingAttributeDeltas={budget.PendingAttributeDeltaCount}, "
                   + $"pendingAttributeTargetGroups={budget.PendingAttributeTargetGroupCount}, "
                   + $"pendingAttributeMaxTargetRange={budget.PendingAttributeMaxTargetRange}, "
                   + $"pendingAttributeEstimatedRandomLookups="
                   + $"{budget.PendingAttributeEstimatedRandomLookupCount}, "
                   + $"ownerLocalFacts={budget.OwnerLocalFactCount}, "
                   + $"ownerLocalFactOwnerGroups={budget.OwnerLocalFactOwnerGroupCount}, "
                   + $"ownerLocalFactMaxOwnerRange={budget.OwnerLocalFactMaxOwnerRange}, "
                   + $"ownerLocalFactFlushes={budget.OwnerLocalFactFlushCount}, "
                   + $"activeEffectSlots={budget.ActiveEffectSlotCount}, "
                   + $"activeEffectSlotCapacity={budget.ActiveEffectSlotCapacity}, "
                   + $"activeEffectDuePeriodSlots="
                   + $"{budget.ActiveEffectChunkSkipDuePeriodSlotCount}, "
                   + $"queryBudget={budget.QueryBudget}, "
                   + $"lookupUpdateBudget={budget.LookupUpdateBudget}, "
                   + $"randomLookupBudget={budget.RandomLookupBudget}, "
                   + $"syncQueryBudget={budget.SyncQueryBudget}, "
                   + $"dependencyWaitRisks={budget.DependencyWaitRiskCount}, "
                   + $"measuredAvgTickMs={budget.MeasuredAverageTickMilliseconds:0.000}/"
                   + $"{AutoChessHeadlessLogicBudgetResult.MeasuredAverageTickBudgetMs:0.000}, "
                   + $"measuredAvgTickPassed={budget.MeasuredAverageTickPassed}, "
                   + $"gasTickAvgMs={budget.GasTickAverageMilliseconds:0.000}/"
                   + $"{AutoChessHeadlessLogicBudgetResult.GasTickAverageBudgetMs:0.000}, "
                   + $"gasTickAvgPassed={budget.GasTickAveragePassed}, "
                   + $"gasTickMaxMs={budget.GasTickMaxMilliseconds:0.000}/"
                   + $"{AutoChessHeadlessLogicBudgetResult.GasTickMaxBudgetMs:0.000}, "
                   + $"gasTickMaxPassed={budget.GasTickMaxPassed}, "
                   + $"coreRuntimeOwnerAvgMs={budget.CoreRuntimeAverageMilliseconds:0.000}/"
                   + $"{AutoChessHeadlessLogicBudgetResult.CoreRuntimeAverageBudgetMs:0.000}, "
                   + $"coreRuntimeOwnerAvgPassed={budget.CoreRuntimeAveragePassed}, "
                   + $"coreSimulationAvgMs={budget.CoreSimulationAverageMilliseconds:0.000}/"
                   + $"{AutoChessHeadlessLogicBudgetResult.CoreSimulationAverageBudgetMs:0.000}, "
                   + $"coreSimulationAvgPassed={budget.CoreSimulationAveragePassed}, "
                   + $"boundaryOwnerAvgMs={budget.BoundaryAverageMilliseconds:0.000}/"
                   + $"{AutoChessHeadlessLogicBudgetResult.BoundaryAverageBudgetMs:0.000}, "
                   + $"boundaryOwnerAvgPassed={budget.BoundaryAveragePassed}, "
                   + $"runnerOwnerAvgMs={budget.RunnerAverageMilliseconds:0.000}/"
                   + $"{AutoChessHeadlessLogicBudgetResult.RunnerAverageBudgetMs:0.000}, "
                   + $"runnerOwnerAvgPassed={budget.RunnerAveragePassed}, "
                   + $"measuredUsPerUnit={budget.MeasuredMicrosecondsPerUnit:0.000}, "
                   + $"gasTickUsPerUnit={budget.GasTickMicrosecondsPerUnit:0.000}, "
                   + $"coreSimulationUsPerUnit={budget.CoreSimulationMicrosecondsPerUnit:0.000}, "
                   + $"gasTickUsPerCommand={budget.GasTickMicrosecondsPerCommand:0.000}, "
                   + $"coreSimulationUsPerCoreFact="
                   + $"{budget.CoreSimulationMicrosecondsPerCoreFact:0.000}, "
                   + $"performanceObservationPollutionRisks="
                   + $"{budget.PerformanceObservationPollutionRiskCount}, "
                   + $"performanceObservationCleanPassed="
                   + $"{budget.PerformanceObservationCleanPassed}, "
                   + $"profilerEvidencePassed={budget.ProfilerEvidencePassed}, "
                   + $"profilerCaptureState={budget.ProfilerCaptureState}";
        }

        public static string CreatePresentationSummary(in AutoChessPresentationSnapshot presentation)
        {
            return $"markers={presentation.RuntimeMarkerCount}, "
                   + $"sourceLines={presentation.SourceLineCount}, "
                   + $"displayedLines={presentation.DisplayLineCount}, "
                   + $"droppedLines={presentation.DroppedLineCount}, "
                   + $"disabledReason={presentation.DisabledReason}";
        }

        public static string CreateBoundaryReportKeyCoverageSummary(
            in GasStructuredLogExportSnapshot snapshot)
        {
            var coverage = CalculateBoundaryReportKeyCoverage(snapshot);
            return $"passed={coverage.Passed}, "
                   + $"entries={coverage.EntryCount}, "
                   + $"sourceAscRefs={coverage.SourceAscReferenceCount}, "
                   + $"sourceReportKeys={coverage.SourceReportKeyCount}, "
                   + $"missingSourceReportKeys={coverage.MissingSourceReportKeyCount}, "
                   + $"targetAscRefs={coverage.TargetAscReferenceCount}, "
                   + $"targetReportKeys={coverage.TargetReportKeyCount}, "
                   + $"missingTargetReportKeys={coverage.MissingTargetReportKeyCount}";
        }

        public static bool HasBoundaryReportKeyCoverage(
            in GasStructuredLogExportSnapshot snapshot)
        {
            return CalculateBoundaryReportKeyCoverage(snapshot).Passed;
        }

        public static string CreateSummary(in AutoChessValidationEvidence evidence)
        {
            return $"completed={evidence.Completed}, "
                   + $"winner={evidence.Winner}, "
                   + $"expectedWinner={evidence.ExpectedWinner}, "
                   + $"scale={evidence.ScenarioScale}, "
                   + $"units={evidence.UnitCount}, "
                   + $"battleTicks={evidence.BattleTicks}, "
                   + $"totalTicks={evidence.TotalTicks}, "
                   + $"warmupDroppedTicks={evidence.WarmupDroppedTicks}, "
                   + $"measuredTicks={evidence.MeasuredTicks}, "
                   + $"commands={evidence.CommandCount}, "
                   + $"attributeChanges={evidence.AttributeChangeCount}, "
                    + $"periodTickDamageFacts={evidence.PeriodTickDamageFactCount}, "
                    + $"periodTickDamageTotal={evidence.PeriodTickDamageTotal:0.###}, "
                    + $"executionOutputs={evidence.ExecutionOutputCount}, "
                    + $"executionSpecScans={evidence.ExecutionSpecScanCount}, "
                    + $"executionMatchedEffectSpecs={evidence.ExecutionMatchedEffectSpecCount}, "
                    + $"executionTargetOwnerMismatches={evidence.ExecutionTargetOwnerMismatchCount}, "
                    + $"executionMissingAttributes={evidence.ExecutionMissingAttributeCount}, "
                    + $"executionEvaluatorRejects={evidence.ExecutionEvaluatorRejectCount}, "
                    + $"executionOutputWrites={evidence.ExecutionOutputWriteCount}, "
                    + $"cueRequests={evidence.CueRequestCount}, "
                   + $"debugEvents={evidence.RuntimeEventCount}, "
                   + $"debugWarnings={evidence.DebugWarningCount}, "
                   + $"debugErrors={evidence.DebugErrorCount}, "
                   + $"blockingDebugErrors={evidence.BlockingDebugErrorCount}, "
                   + $"coreRequests={evidence.CoreRequestCount}, "
                   + $"coreFacts={evidence.CoreFactCount}, "
                   + $"coreDeltas={evidence.CoreDeltaCount}, "
                   + $"coreCues={evidence.CoreCueCount}, "
                   + $"presentationMarkers={evidence.PresentationMarkerCount}, "
                   + $"presentationSourceLines={evidence.PresentationSourceLineCount}, "
                   + $"presentationDisplayedLines={evidence.PresentationDisplayLineCount}, "
                   + $"presentationDroppedLines={evidence.PresentationDroppedLineCount}, "
                   + $"presentationDisabledReason={evidence.PresentationDisabledReason}, "
                   + $"peakEventBus={evidence.PeakEventBusLength}, "
                    + $"replayLag={evidence.ReplayLag}, "
                    + $"proofOnlyApiMask={evidence.ProofOnlyApiMask}, "
                    + $"reselectTriggerMask={evidence.ReselectTriggerMask}, "
                    + $"runtimeAccessContractEntries={evidence.RuntimeAccessContractEntryCount}, "
                    + $"runtimeAccessEcsHandleProxies={evidence.RuntimeAccessEcsHandleProxyCount}, "
                    + $"runtimeAccessManualSync={evidence.RuntimeAccessManualSyncCount}, "
                    + $"runtimeAccessPerformancePassRisks={evidence.RuntimeAccessPerformancePassRiskCount}, "
                    + $"runtimeAccessBattleHashAffecting={evidence.RuntimeAccessBattleHashAffectingCount}, "
                    + $"runtimeAccessCapabilityMask=0x{evidence.RuntimeAccessCapabilityMask:X}, "
                    + $"gasConceptCoverageMask=0x{evidence.GasConceptCoverageMask:X}, "
                    + $"gasConceptMissingMask=0x{evidence.GasConceptMissingMask:X}, "
                    + $"runtimeTraceAbilityCode={evidence.RuntimeTraceAbilityCode}, "
                    + $"runtimeTraceStageMask=0x{evidence.RuntimeTraceStageMask:X}, "
                    + $"runtimeTraceMissingStageMask=0x{evidence.RuntimeTraceMissingStageMask:X}, "
                    + $"runtimeTraceSeeds={evidence.RuntimeTraceSeedCount}, "
                    + $"runtimeTraceModifiers={evidence.RuntimeTraceModifierCount}, "
                    + $"runtimeTraceFacts={evidence.RuntimeTraceFactCount}, "
                    + $"runtimeTraceCues={evidence.RuntimeTraceCueCount}, "
                    + $"runtimeTraceActiveMutationSeeds={evidence.RuntimeTraceActiveMutationSeedCount}, "
                    + $"runtimeTraceExecutionCalculationModifiers={evidence.RuntimeTraceExecutionCalculationModifierCount}, "
                    + $"activeEffectSlots={evidence.ActiveEffectSlotCount}, "
                    + $"activeEffectActiveSlots={evidence.ActiveEffectSlotActiveCount}, "
                    + $"activeEffectDuePeriodSlots={evidence.ActiveEffectChunkSkipDuePeriodSlotCount}, "
                    + $"activeMutationCommands={evidence.ActiveMutationCommandCount}, "
                    + $"activeMutationOwnerGroups={evidence.ActiveMutationOwnerGroupCount}, "
                    + $"activeMutationMaxOwnerRange={evidence.ActiveMutationMaxOwnerRange}, "
                    + $"activeMutationSortMoves={evidence.ActiveMutationSortMoveCount}, "
                    + $"activeMutationEstimatedRandomLookups={evidence.ActiveMutationEstimatedRandomLookupCount}, "
                    + $"activeMutationOwnerResourceLookups={evidence.ActiveMutationOwnerResourceLookupCount}, "
                    + $"activeMutationMigrationCarriers={evidence.ActiveMutationMigrationCarrierCount}, "
                    + $"pendingAttributeDeltas={evidence.PendingAttributeDeltaCount}, "
                    + $"pendingAttributeAppliedDeltas={evidence.PendingAttributeAppliedDeltaCount}, "
                    + $"pendingAttributeSkippedDeltas={evidence.PendingAttributeSkippedDeltaCount}, "
                    + $"pendingAttributeTargetGroups={evidence.PendingAttributeTargetGroupCount}, "
                    + $"pendingAttributeMaxTargetRange={evidence.PendingAttributeMaxTargetRange}, "
                    + $"pendingAttributeEstimatedRandomLookups={evidence.PendingAttributeEstimatedRandomLookupCount}, "
                    + $"pendingAttributeFactPatches={evidence.PendingAttributeFactPatchCount}, "
                    + $"pendingAttributeMigrationCarriers={evidence.PendingAttributeMigrationCarrierCount}, "
                    + $"ownerLocalFacts={evidence.OwnerLocalFactCount}, "
                    + $"ownerLocalFactOwnerGroups={evidence.OwnerLocalFactOwnerGroupCount}, "
                    + $"ownerLocalFactMaxOwnerRange={evidence.OwnerLocalFactMaxOwnerRange}, "
                    + $"ownerLocalFactFlushes={evidence.OwnerLocalFactFlushCount}, "
                    + $"streamCarrierPressureWarnings={evidence.StreamCarrierPressureWarningCount}, "
                    + $"streamCarrierPeak={evidence.StreamCarrierPeakCount}, "
                    + $"streamCarrierCapacity={evidence.StreamCarrierPeakCapacity}, "
                    + $"observationMaterializedQueries={evidence.ObservationMaterializedQueryCount}, "
                    + $"observationMaterializedEntities={evidence.ObservationMaterializedEntityCount}, "
                    + $"observationMaterializationUs={evidence.ObservationMaterializationElapsedMicroseconds}, "
                    + $"performancePassObservationPollutionRisks={evidence.ObservationPerformancePollutionRiskCount}, "
                    + $"magnitudeSourceCurrentValueLookups={evidence.MagnitudeSourceCurrentValueLookupCount}, "
                    + $"magnitudeSourceCapturedValueHits={evidence.MagnitudeSourceCapturedValueHitCount}, "
                    + $"magnitudeSourceCaptureMisses={evidence.MagnitudeSourceCaptureMissCount}, "
                    + $"magnitudeSourceCaptureMissLiveLookups={evidence.MagnitudeSourceCaptureMissLiveLookupCount}, "
                    + $"magnitudeSourceFallbackValues={evidence.MagnitudeSourceFallbackValueCount}, "
                    + $"magnitudeSourceFallbackFacts={evidence.MagnitudeSourceFallbackFactCount}, "
                    + $"magnitudeSourceSourceAttributeLookups={evidence.MagnitudeSourceSourceAttributeLookupCount}, "
                    + $"magnitudeSourceTargetAttributeLookups={evidence.MagnitudeSourceTargetAttributeLookupCount}, "
                    + $"magnitudeSourceExecutionInputLookups={evidence.MagnitudeSourceExecutionInputLookupCount}, "
                    + $"journalingCaptured={evidence.JournalingCaptured}, "
                   + $"profilerCaptureState={evidence.ProfilerCaptureState}, "
                   + $"ecsRuntimeTickOnly={evidence.EcsRuntimeTickOnly}, "
                   + $"officialDiffSeparatePass={evidence.OfficialDiffSeparatePass}, "
                   + $"physicsDisabledReason={evidence.PhysicsDisabledReason}, "
                   + $"renderDisabledReason={evidence.RenderDisabledReason}, "
                   + $"processWarmupRuns={evidence.ProcessWarmupRuns}, "
                   + $"totalElapsedMs={evidence.TotalElapsedMilliseconds:0.000}, "
                   + $"factsHash=0x{evidence.FactsHash:X8}, "
                   + $"summaryHash=0x{evidence.SummaryHash:X8}, "
                   + $"avgTickMs={evidence.AverageTickMilliseconds:0.000}";
        }

        public static string CreateDebuggerSummary(in AutoChessBattleResult result)
        {
            var evidence = result.RuntimeDiagnostics.Evidence;
            var events = evidence.Events;
            var workload = evidence.Workload;
            var activeEffect = evidence.ActiveEffect;
            var mutation = evidence.ActiveMutation;
            var attributeFact = evidence.AttributeFact;
            var apiHealth = evidence.ApiHealth;
            var backbone = evidence.FrameBackbone;
            var observation = evidence.Observation;
            var magnitudeSource = evidence.MagnitudeSource;
            return $"runtimeDiagnosticsSource=GasRuntimeDiagnosticEvidenceSnapshot, "
                   + $"events={events.EventCount}, "
                   + $"dropped={events.DroppedEventCount}, "
                   + $"warnings={events.WarningCount}, "
                   + $"errors={events.ErrorCount}, "
                   + $"blockingErrors={events.BlockingErrorCount}, "
                   + $"requests={workload.RequestCount}, "
                   + $"specs={workload.SpecCount}, "
                   + $"deltas={workload.DeltaCount}, "
                   + $"facts={workload.FactCount}, "
                   + $"cues={workload.CueCount}, "
                   + $"presentation={workload.PresentationCount}, "
                   + $"activeEffectOwners={activeEffect.StoreOwnerCount}, "
                   + $"activeEffectSlots={activeEffect.SlotCount}, "
                   + $"periodTickDamageFacts={result.EventCounts.PeriodTickDamageFacts}, "
                   + $"queryBudget={apiHealth.QueryBudget}, "
                   + $"lookupBudget={apiHealth.LookupUpdateBudget}, "
                   + $"randomLookupBudget={apiHealth.RandomLookupBudget}, "
                   + $"syncQueryBudget={apiHealth.SyncQueryBudget}, "
                   + $"activeMutationCommands={mutation.CommandCount}, "
                   + $"activeMutationOwnerGroups={mutation.OwnerGroupCount}, "
                   + $"activeMutationMaxOwnerRange={mutation.MaxOwnerRange}, "
                   + $"activeMutationSortMoves={mutation.SortMoveCount}, "
                   + $"activeMutationEstimatedRandomLookups={mutation.EstimatedRandomLookupCount}, "
                   + $"activeMutationOwnerResourceLookups={mutation.OwnerResourceLookupCount}, "
                   + $"activeMutationMigrationCarriers={mutation.MigrationCarrierCount}, "
                   + $"pendingAttributeDeltas={attributeFact.PendingDeltaCount}, "
                   + $"pendingAttributeAppliedDeltas={attributeFact.PendingAppliedDeltaCount}, "
                   + $"pendingAttributeSkippedDeltas={attributeFact.PendingSkippedDeltaCount}, "
                   + $"pendingAttributeTargetGroups={attributeFact.PendingTargetGroupCount}, "
                   + $"pendingAttributeMaxTargetRange={attributeFact.PendingMaxTargetRange}, "
                   + $"pendingAttributeEstimatedRandomLookups={attributeFact.PendingEstimatedRandomLookupCount}, "
                   + $"pendingAttributeFactPatches={attributeFact.PendingFactPatchCount}, "
                   + $"pendingAttributeMigrationCarriers={attributeFact.PendingMigrationCarrierCount}, "
                   + $"ownerLocalFacts={attributeFact.OwnerLocalFactCount}, "
                   + $"ownerLocalFactOwnerGroups={attributeFact.OwnerLocalFactOwnerGroupCount}, "
                   + $"ownerLocalFactMaxOwnerRange={attributeFact.OwnerLocalFactMaxOwnerRange}, "
                   + $"ownerLocalFactFlushes={attributeFact.OwnerLocalFactFlushCount}, "
                   + $"streamCarrierPressureWarnings={events.EffectCommandStreamPressure.WarningCount}, "
                   + $"observationMaterializedQueries={observation.MaterializedQueryCount}, "
                   + $"observationMaterializedEntities={observation.MaterializedEntityCount}, "
                   + $"observationMaterializationUs={observation.ElapsedMicroseconds}, "
                   + $"performancePassObservationPollutionRisks={observation.PerformancePollutionRiskCount}, "
                   + $"magnitudeSourceCurrentValueLookups={magnitudeSource.CurrentValueLookupCount}, "
                   + $"magnitudeSourceCapturedValueHits={magnitudeSource.CapturedValueHitCount}, "
                   + $"magnitudeSourceCaptureMisses={magnitudeSource.CaptureMissCount}, "
                   + $"magnitudeSourceCaptureMissLiveLookups={magnitudeSource.CaptureMissLiveLookupCount}, "
                   + $"magnitudeSourceFallbackValues={magnitudeSource.FallbackValueCount}, "
                   + $"magnitudeSourceFallbackFacts={magnitudeSource.FallbackFactCount}, "
                   + $"magnitudeSourceSourceAttributeLookups={magnitudeSource.SourceAttributeLookupCount}, "
                   + $"magnitudeSourceTargetAttributeLookups={magnitudeSource.TargetAttributeLookupCount}, "
                   + $"magnitudeSourceExecutionInputLookups={magnitudeSource.ExecutionInputLookupCount}, "
                   + $"frameBackbonePhases={backbone.PhaseCount}, "
                   + $"streams={backbone.StreamCount}, "
                   + $"migrationCarriers={backbone.MigrationCarrierCount}, "
                   + $"profilerMarkerContracts={backbone.ProfilerMarkerCount}, "
                   + $"journalingMarkerContracts={backbone.JournalingMarkerCount}";
        }

        public static string CreateTimingSummary(
            in AutoChessBattleResult result,
            in AutoChessValidationEvidence evidence)
        {
            var builder = new StringBuilder(512);
            builder.Append("ecsRuntimeTickOnly=")
                .Append(evidence.EcsRuntimeTickOnly)
                .Append(", ownerSplit=core/boundary/debugger/runner/physics/render")
                .Append(", physicsDisabledReason=")
                .Append(evidence.PhysicsDisabledReason)
                .Append(", renderDisabledReason=")
                .Append(evidence.RenderDisabledReason)
                .Append(", presentationDisabledReason=")
                .Append(evidence.PresentationDisabledReason);
            AppendTiming(builder, "CoreRuntimeOwner", result.RuntimeTiming.CoreRuntime);
            AppendTiming(builder, "BoundaryOwner", result.RuntimeTiming.Boundary);
            AppendTiming(builder, "DebuggerOwner", result.RuntimeTiming.Debugger);
            AppendTiming(builder, "RunnerOwner", result.RuntimeTiming.Runner);
            AppendTiming(builder, "PhysicsOwner", result.RuntimeTiming.Physics);
            AppendTiming(builder, "RenderOwner", result.RuntimeTiming.Render);
            AppendTiming(builder, "GASTickTotal", result.RuntimeTiming.TickTotal);
            AppendTiming(builder, nameof(GASFramePrepareSystemGroup), result.RuntimeTiming.FramePrepare);
            AppendTiming(builder, nameof(GASCommandResolveSystemGroup), result.RuntimeTiming.CommandResolve);
            AppendTiming(builder, nameof(GASCoreSimulationSystemGroup), result.RuntimeTiming.CoreSimulation);
            AppendTiming(builder, nameof(GASStructuralCommitSystemGroup), result.RuntimeTiming.StructuralCommit);
            AppendTiming(builder, nameof(GASBoundaryProjectionSystemGroup), result.RuntimeTiming.BoundaryProjection);
            AppendTiming(builder, "GASDependencyDrain", result.RuntimeTiming.DependencyDrain);
            return builder.ToString();
        }

        public static string CreateHotspotSummary(in AutoChessBattleResult result)
        {
            return CreateHotspotSummary(result, result);
        }

        public static string CreateHotspotSummary(
            in AutoChessBattleResult performanceResult,
            in AutoChessBattleResult diagnosticResult)
        {
            var diagnosticEvidence = diagnosticResult.RuntimeDiagnostics.Evidence;
            var performanceEvidence = performanceResult.RuntimeDiagnostics.Evidence;
            var mutation = diagnosticEvidence.ActiveMutation;
            var attributeFact = diagnosticEvidence.AttributeFact;
            var observation = diagnosticEvidence.Observation;
            var performanceObservation = performanceEvidence.Observation;
            var magnitudeSource = diagnosticEvidence.MagnitudeSource;
            return $"primaryCommands={performanceResult.DriverIssuedPrimaryCommands}, "
                   + $"finisherCommands={performanceResult.DriverIssuedFinisherCommands}, "
                   + $"lowestHealthSelections={performanceResult.DriverLowestHealthTargetSelections}, "
                   + $"tickAvgMs={performanceResult.RuntimeTiming.TickTotal.AverageMilliseconds:0.000}, "
                   + $"coreRuntimeOwnerAvgMs={performanceResult.RuntimeTiming.CoreRuntime.AverageMilliseconds:0.000}, "
                   + $"boundaryOwnerAvgMs={performanceResult.RuntimeTiming.Boundary.AverageMilliseconds:0.000}, "
                   + $"runnerOwnerAvgMs={performanceResult.RuntimeTiming.Runner.AverageMilliseconds:0.000}, "
                   + $"debuggerOwnerAvgMs={performanceResult.RuntimeTiming.Debugger.AverageMilliseconds:0.000}, "
                   + $"commandResolveAvgMs={performanceResult.RuntimeTiming.CommandResolve.AverageMilliseconds:0.000}, "
                   + $"coreSimulationAvgMs={performanceResult.RuntimeTiming.CoreSimulation.AverageMilliseconds:0.000}, "
                   + $"activeMutationCommands={mutation.CommandCount}, "
                   + $"activeMutationOwnerGroups={mutation.OwnerGroupCount}, "
                   + $"activeMutationMaxOwnerRange={mutation.MaxOwnerRange}, "
                   + $"activeMutationEstimatedRandomLookups={mutation.EstimatedRandomLookupCount}, "
                   + $"periodTickDamageFacts={performanceResult.EventCounts.PeriodTickDamageFacts}, "
                   + $"executionSpecScans={performanceResult.DriverExecutionSpecScans}, "
                   + $"executionMatchedEffectSpecs={performanceResult.DriverExecutionMatchedEffectSpecs}, "
                   + $"executionTargetOwnerMismatches={performanceResult.DriverExecutionTargetOwnerMismatches}, "
                   + $"executionMissingAttributes={performanceResult.DriverExecutionMissingAttributes}, "
                   + $"executionEvaluatorRejects={performanceResult.DriverExecutionEvaluatorRejects}, "
                   + $"executionOutputWrites={performanceResult.DriverExecutionOutputWrites}, "
                   + $"pendingAttributeDeltas={attributeFact.PendingDeltaCount}, "
                   + $"pendingAttributeAppliedDeltas={attributeFact.PendingAppliedDeltaCount}, "
                   + $"pendingAttributeTargetGroups={attributeFact.PendingTargetGroupCount}, "
                   + $"pendingAttributeEstimatedRandomLookups={attributeFact.PendingEstimatedRandomLookupCount}, "
                   + $"ownerLocalFacts={attributeFact.OwnerLocalFactCount}, "
                   + $"ownerLocalFactOwnerGroups={attributeFact.OwnerLocalFactOwnerGroupCount}, "
                   + $"ownerLocalFactFlushes={attributeFact.OwnerLocalFactFlushCount}, "
                   + $"streamCarrierPressureWarnings={diagnosticEvidence.Events.EffectCommandStreamPressure.WarningCount}, "
                   + $"observationMaterializedQueries={observation.MaterializedQueryCount}, "
                   + $"observationMaterializationUs={observation.ElapsedMicroseconds}, "
                   + $"performancePassObservationPollutionRisks={performanceObservation.PerformancePollutionRiskCount}, "
                   + $"magnitudeSourceCaptureMisses={magnitudeSource.CaptureMissCount}, "
                   + $"magnitudeSourceCaptureMissLiveLookups={magnitudeSource.CaptureMissLiveLookupCount}, "
                   + $"magnitudeSourceFallbackValues={magnitudeSource.FallbackValueCount}, "
                   + $"magnitudeSourceFallbackFacts={magnitudeSource.FallbackFactCount}, "
                   + $"magnitudeSourceExecutionInputLookups={magnitudeSource.ExecutionInputLookupCount}, "
                   + $"structuralCommitAvgMs={performanceResult.RuntimeTiming.StructuralCommit.AverageMilliseconds:0.000}, "
                   + $"boundaryProjectionAvgMs={performanceResult.RuntimeTiming.BoundaryProjection.AverageMilliseconds:0.000}, "
                   + $"dependencyDrainAvgMs={performanceResult.RuntimeTiming.DependencyDrain.AverageMilliseconds:0.000}";
        }

        public static string CreateBoundaryOwnerSummary(in AutoChessBattleResult result)
        {
            var driverOwner = result.DriverOwnerSnapshot;
            return "shellPublicRawEcsSurface=false, "
                   + "unitIdentity=AutoChessBattleUnitKey, "
                   + "unitRuntimeHandle=ASCHandle, "
                   + "unitCreateOwner=RuntimeShell.ASCCommandCapability, "
                   + "unitDestroyOwner=ASCCommandPort.RequestDestroy, "
                   + "driverLifecycleOwner=AutoChessBattleDriverRuntimeStore, "
                   + "driverHandle=OpaqueDriverIdVersion, "
                   + "driverAdapterRawEntity=false, "
                   + $"driverOwnerInstalled={Bool(driverOwner.OwnerInstalled)}, "
                   + $"driverOwnerEnabled={Bool(driverOwner.OwnerEnabled)}, "
                   + $"driverOwnerHandleMatched={Bool(driverOwner.HandleMatched)}, "
                   + $"driverStructuralCreates={driverOwner.StructuralCreateCount}, "
                   + $"driverEnableRequests={driverOwner.EnableCount}, "
                   + $"driverDisableRequests={driverOwner.DisableCount}, "
                   + $"driverUninstallRequests={driverOwner.UninstallCount}, "
                   + "catalogOwner=AutoChessGasCatalogSession, "
                   + "snapshotOwner=AutoChessGasBattleUnitSnapshotProjector.StructuredLog, "
                   + "observationOwner=AutoChessGasObservationGateway, "
                   + "runnerSyncOwner=GASDependencyDrain, "
                   + "reportProjectionOwner=AutoChessRuntimeUnitResolver.BoundaryReportKey, "
                   + $"runtimeAccessContractEntries={AutoChessGasRuntimeAccessContract.EntryCount}, "
                   + $"runtimeAccessEcsHandleProxies={AutoChessGasRuntimeAccessContract.EcsHandleProxyCount}, "
                   + $"runtimeAccessManualSync={AutoChessGasRuntimeAccessContract.ManualSyncCount}, "
                   + $"runtimeAccessPerformancePassRisks={AutoChessGasRuntimeAccessContract.PerformancePassRiskCount}, "
                   + CreateBoundaryReportKeyCoverageSummary(result.StructuredLogSnapshot)
                   + $", factsHash=0x{CalculateFactsHash(result.StructuredLogSnapshot):X8}";
        }

        public static string CreateRuntimeAccessContractSummary()
        {
            return AutoChessGasRuntimeAccessContract.CreateSummary();
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        public static string CreateOfficialToolDiffSummary(
            in AutoChessBattleResult result,
            in AutoChessValidationEvidence evidence)
        {
            var official = result.OfficialToolDiff;
            var runtime = result.RuntimeDiagnostics.Evidence.Structural;
            var runtimeStructural = runtime.RuntimeStructuralApproximationCount;
            var officialStructural = official.JournalingStructuralRecordCount;
            return $"runtimeSelfDiagnostics=events:{result.RuntimeDiagnostics.Evidence.Events.EventCount}, "
                   + $"officialDiffSeparatePass={evidence.OfficialDiffSeparatePass}, "
                   + $"journalingAvailable={evidence.JournalingAvailable}, "
                   + $"journalingCaptured={evidence.JournalingCaptured}, "
                   + $"journalingWorldRecords={official.JournalingWorldRecordCount}, "
                   + $"runtimeStructuralApprox={runtimeStructural}, "
                   + $"journalingStructural={officialStructural}, "
                   + $"deltaStructural={runtimeStructural - officialStructural}, "
                   + $"runtimeCreates={runtime.EntityCreateCount}, "
                   + $"journalingCreates={official.JournalingCreateEntityCount}, "
                   + $"deltaCreates={runtime.EntityCreateCount - official.JournalingCreateEntityCount}, "
                   + $"runtimeDestroys={runtime.EntityDestroyCount}, "
                   + $"journalingDestroys={official.JournalingDestroyEntityCount}, "
                   + $"deltaDestroys={runtime.EntityDestroyCount - official.JournalingDestroyEntityCount}, "
                   + $"journalingAddComponents={official.JournalingAddComponentCount}, "
                   + $"journalingRemoveComponents={official.JournalingRemoveComponentCount}, "
                   + $"journalingEnableComponents={official.JournalingEnableComponentCount}, "
                   + $"journalingDisableComponents={official.JournalingDisableComponentCount}, "
                   + $"journalingSetComponentData={official.JournalingSetComponentDataCount}, "
                   + $"journalingSetBuffer={official.JournalingSetBufferCount}, "
                   + $"journalingGetComponentDataRW={official.JournalingGetComponentDataRwCount}, "
                   + $"journalingGetBufferRW={official.JournalingGetBufferRwCount}, "
                   + $"profilerAvailable={evidence.ProfilerAvailable}, "
                   + $"profilerEnabled={official.ProfilerEnabled}, "
                   + $"structuralProfilerCategoryEnabled={official.StructuralChangesProfilerCategoryEnabled}, "
                   + $"memoryProfilerCategoryEnabled={official.MemoryProfilerCategoryEnabled}, "
                   + $"profilerCaptureState={evidence.ProfilerCaptureState}, "
                   + $"journalingRecordTopN={official.JournalingRecordTopN}, "
                   + $"journalingSystemTopN={official.JournalingSystemTopN}, "
                   + $"journalingComponentTopN={official.JournalingComponentTopN}";
        }

        public static string CreateDataFlowDiagram(in AutoChessBattleResult result)
        {
            var workload = result.RuntimeDiagnostics.Evidence.Workload;
            return "```mermaid\n"
                   + "flowchart LR\n"
                   + $"    CommandDrive[\"AutoChessBattleCommandDriveSystem\\nscale: {result.ScenarioScale}, units: {result.Units.Length}\\ncommands: {result.DriverIssuedCommands}\"] --> AbilityBuffer[\"AbilityCommandBuffer\\nrequest entities avoided\"]\n"
                   + $"    AbilityBuffer --> RuntimeCore[\"GAS Runtime Core\\nspecs: {workload.SpecCount}\"]\n"
                   + $"    RuntimeCore --> GEStream[\"GEEffectCommandBuffer / Spec / Delta\\ndeltas: {workload.DeltaCount}\"]\n"
                   + $"    GEStream --> Execution[\"AutoChessExecuteDamageCalculationSystem\\nexecution outputs: {result.EventCounts.ExecutionCalculationOutputUpdated}\"]\n"
                   + $"    Execution --> PendingDelta[\"pending AttributeModifierBuffer\\nexecution outputs: {result.EventCounts.ExecutionCalculationOutputUpdated}\"]\n"
                   + $"    PendingDelta --> Attribute[\"GASAttributeModifierDeltaApplySystem\\nattribute changes: {result.EventCounts.AttributeChanges}\"]\n"
                   + $"    Attribute --> Facts[\"GameplayEventBuffer typed facts\\nfacts: {workload.FactCount}\"]\n"
                   + $"    Facts --> Projection[\"Replay / Presentation / Layer 2 Diagnostics\\nreplay events: {result.EventCounts.ReplayEvents}\"]\n"
                   + $"    Projection --> OfficialDiff[\"Official tool diff\\nseparate pass, journaling records: {result.OfficialToolDiff.JournalingWorldRecordCount}\"]\n"
                   + "```";
        }

        public static string CreateSequenceDiagram(in AutoChessBattleResult result)
        {
            var diagnosticEvents = result.RuntimeDiagnostics.Evidence.Events;
            return "```mermaid\n"
                   + "sequenceDiagram\n"
                   + "    participant Runner as AutoChess Demo Runner\n"
                   + "    participant Drive as AutoChessBattle Command Drive\n"
                   + "    participant Core as GAS Runtime Core\n"
                   + "    participant Exec as Execution Calculation\n"
                   + "    participant Obs as Replay / Projection\n"
                   + "    participant Debug as Layer 2 DiagnosticsSink\n"
                   + "    participant Unity as Unity Journaling / Profiler\n"
                   + $"    Runner->>Drive: fixed ticks {result.TotalTicks}, battle ticks {result.BattleTicks}, scale {result.ScenarioScale}\n"
                   + $"    Drive->>Core: AbilityCommandBuffer commands {result.DriverIssuedCommands}\n"
                   + $"    Core->>Exec: execute GE commands, finishers {result.DriverIssuedFinisherCommands}\n"
                   + $"    Exec->>Core: pending modifier + typed fact outputs {result.EventCounts.ExecutionCalculationOutputUpdated}\n"
                   + $"    Core->>Core: apply pending modifier deltas {result.EventCounts.AttributeChanges}\n"
                   + $"    Core->>Obs: attribute changes {result.EventCounts.AttributeChanges}, cue requests {result.EventCounts.CueRequests}\n"
                   + $"    Obs->>Debug: counters {diagnosticEvents.EventCount}, warnings {diagnosticEvents.WarningCount}, errors {diagnosticEvents.ErrorCount}\n"
                   + $"    Debug->>Unity: read EntitiesJournaling records in a separate official-diff pass\n"
                   + $"    Unity-->>Debug: journaling structural records {result.OfficialToolDiff.JournalingStructuralRecordCount}, profiler state {result.OfficialToolDiff.ProfilerCaptureState}\n"
                   + "```";
        }

        private static void AppendTiming(
            StringBuilder builder,
            string systemName,
            in AutoChessBattleSystemTiming timing)
        {
            builder.Append(" | ")
                .Append(systemName)
                .Append("(samples=")
                .Append(timing.Samples)
                .Append(",avgMs=")
                .Append(timing.AverageMilliseconds.ToString("0.000"))
                .Append(",maxMs=")
                .Append(timing.MaxMilliseconds.ToString("0.000"))
                .Append(')');
        }

        private static uint CalculateFactsHash(in GasStructuredLogExportSnapshot snapshot)
        {
            unchecked
            {
                var hash = 2166136261u;
                var entries = snapshot.Entries ?? Array.Empty<GasStructuredLogEntry>();
                for (var i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    hash = AppendHash(hash, entry.Frame);
                    hash = AppendHash(hash, entry.Sequence);
                    hash = AppendHash(hash, (int)entry.ReplayKind);
                    hash = AppendHash(hash, (int)entry.GameplayEventType);
                    hash = AppendHash(hash, entry.EventCode);
                    hash = AppendHash(hash, entry.ReasonCode);
                    hash = AppendHash(hash, entry.RelatedAbilityCode);
                    hash = AppendHash(hash, entry.ContextId);
                    hash = AppendHash(hash, entry.AttrSetCode);
                    hash = AppendHash(hash, entry.AttributeCode);
                    hash = AppendHash(hash, entry.TagIndex);
                    hash = AppendHash(hash, entry.SourceReportKey);
                    hash = AppendHash(hash, entry.TargetReportKey);
                    hash = AppendHash(hash, entry.SourceAbility.Index);
                    hash = AppendHash(hash, entry.GameplayEffect.Index);
                }

                return hash;
            }
        }

        private static BoundaryReportKeyCoverage CalculateBoundaryReportKeyCoverage(
            in GasStructuredLogExportSnapshot snapshot)
        {
            var entries = snapshot.Entries ?? Array.Empty<GasStructuredLogEntry>();
            var sourceAscReferenceCount = 0;
            var targetAscReferenceCount = 0;
            var sourceReportKeyCount = 0;
            var targetReportKeyCount = 0;
            var missingSourceReportKeyCount = 0;
            var missingTargetReportKeyCount = 0;

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.SourceAsc != Entity.Null)
                {
                    sourceAscReferenceCount++;
                    if (entry.SourceReportKey > 0)
                        sourceReportKeyCount++;
                    else
                        missingSourceReportKeyCount++;
                }

                if (entry.TargetAsc != Entity.Null)
                {
                    targetAscReferenceCount++;
                    if (entry.TargetReportKey > 0)
                        targetReportKeyCount++;
                    else
                        missingTargetReportKeyCount++;
                }
            }

            return new BoundaryReportKeyCoverage(
                entries.Length,
                sourceAscReferenceCount,
                sourceReportKeyCount,
                missingSourceReportKeyCount,
                targetAscReferenceCount,
                targetReportKeyCount,
                missingTargetReportKeyCount);
        }

        private static StreamCarrierPressure CalculateStreamCarrierPressure(
            in GasRuntimeDiagnosticSnapshot diagnostics)
        {
            var pressure = diagnostics.Evidence.Events.EffectCommandStreamPressure;
            return new StreamCarrierPressure(
                pressure.WarningCount,
                pressure.PeakCount,
                pressure.PeakCapacity);
        }

        private static uint CalculateSummaryHash(
            in AutoChessBattleResult result,
            int presentationMarkerCount,
            uint factsHash)
        {
            unchecked
            {
                var hash = AppendHash(2166136261u, (int)factsHash);
                hash = AppendHash(hash, result.Completed ? 1 : 0);
                hash = AppendHash(hash, (int)result.Winner);
                hash = AppendHash(hash, result.ScenarioScale);
                hash = AppendHash(hash, result.BattleTicks);
                hash = AppendHash(hash, result.DriverIssuedCommands);
                hash = AppendHash(hash, result.EventCounts.AttributeChanges);
                hash = AppendHash(hash, result.EventCounts.PeriodTickDamageFacts);
                hash = AppendHash(hash, result.EventCounts.ExecutionCalculationOutputUpdated);
                hash = AppendHash(hash, result.EventCounts.CueRequests);
                hash = AppendHash(hash, result.RuntimeDiagnostics.Evidence.Events.EventCount);
                hash = AppendHash(hash, result.OfficialToolDiff.JournalingWorldRecordCount);
                hash = AppendHash(hash, presentationMarkerCount);
                return hash;
            }
        }

        private static uint AppendHash(uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                return hash * 16777619u;
            }
        }

        private readonly struct BoundaryReportKeyCoverage
        {
            public readonly int EntryCount;
            public readonly int SourceAscReferenceCount;
            public readonly int SourceReportKeyCount;
            public readonly int MissingSourceReportKeyCount;
            public readonly int TargetAscReferenceCount;
            public readonly int TargetReportKeyCount;
            public readonly int MissingTargetReportKeyCount;

            public bool Passed =>
                EntryCount > 0
                && SourceAscReferenceCount > 0
                && TargetAscReferenceCount > 0
                && MissingSourceReportKeyCount == 0
                && MissingTargetReportKeyCount == 0;

            public BoundaryReportKeyCoverage(
                int entryCount,
                int sourceAscReferenceCount,
                int sourceReportKeyCount,
                int missingSourceReportKeyCount,
                int targetAscReferenceCount,
                int targetReportKeyCount,
                int missingTargetReportKeyCount)
            {
                EntryCount = entryCount;
                SourceAscReferenceCount = sourceAscReferenceCount;
                SourceReportKeyCount = sourceReportKeyCount;
                MissingSourceReportKeyCount = missingSourceReportKeyCount;
                TargetAscReferenceCount = targetAscReferenceCount;
                TargetReportKeyCount = targetReportKeyCount;
                MissingTargetReportKeyCount = missingTargetReportKeyCount;
            }
        }

        private readonly struct StreamCarrierPressure
        {
            public readonly int WarningCount;
            public readonly int PeakCount;
            public readonly int PeakCapacity;

            public StreamCarrierPressure(
                int warningCount,
                int peakCount,
                int peakCapacity)
            {
                WarningCount = warningCount;
                PeakCount = peakCount;
                PeakCapacity = peakCapacity;
            }
        }
    }
}
