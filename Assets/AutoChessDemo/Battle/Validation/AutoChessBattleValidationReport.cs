using System;
using System.Text;
using GAS.Runtime;

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
            // ValidationEvidence 的语义字段只从 Runtime v1 immutable Boundary read-model 派生。
            var observation = performanceResult.RuntimeV1Observation;
            var factsHash = observation.BoundarySequenceHash;
            var summaryHash = CalculateSummaryHash(performanceResult, factsHash);
            var reselectTriggerMask = 0;
            if (observation.InvalidFactCount > 0)
                reselectTriggerMask |= 1;
            if (observation.DrainFailureCount > 0)
                reselectTriggerMask |= 2;
            if (observation.RejectedEffectFactCount > 0)
                reselectTriggerMask |= 4;
            if (observation.RingCapacity > 0 && observation.RingHighWater >= observation.RingCapacity)
                reselectTriggerMask |= 8;
            if (!observation.HasSessionObservation)
                reselectTriggerMask |= 16;
            if (observation.FaultFactCount > 0)
                reselectTriggerMask |= 32;

            var traceAbilityCode = AutoChessBattleRules.AbilityPlayerExecute;
            var gasConceptCoverageMask = observation.BoundaryFactCount > 0 ? 1 : 0;
            var gasConceptMissingMask = observation.BoundaryFactCount > 0 ? 0 : 1;
            var runtimeTraceStageMask = observation.BoundaryFactCount > 0 ? 1 : 0;
            var runtimeTraceMissingStageMask = observation.BoundaryFactCount > 0 ? 0 : 1;

            return new AutoChessValidationEvidence(
                performanceResult.Completed,
                performanceResult.Winner,
                scenario.ExpectedWinner,
                performanceResult.ScenarioScale,
                performanceResult.Units.Length,
                performanceResult.BattleTicks,
                performanceResult.TotalTicks,
                performanceResult.SpawnFinalizeMaintenanceTicks,
                 performanceResult.MeasuredTicks,
                 performanceResult.AcceptedCommandCount,
                 observation.AttributeFactCount,
                 observation.PeriodTickFactCount,
                 observation.PeriodTickDamageTotal,
                 observation.ExecutionCalculationFactCount,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 observation.CueFactCount,
                 observation.BoundaryFactCount,
                 observation.RejectedEffectFactCount,
                 observation.FaultFactCount + observation.InvalidFactCount,
                 observation.FaultFactCount + observation.InvalidFactCount
                 + observation.DrainFailureCount,
                 performanceResult.AcceptedCommandCount,
                 observation.BoundaryFactCount,
                 observation.AttributeFactCount,
                 observation.CueFactCount,
                 presentation.RuntimeMarkerCount,
                 presentation.SourceLineCount,
                 presentation.DisplayLineCount,
                 presentation.DroppedLineCount,
                 observation.RingHighWater,
                 0,
                 0,
                 reselectTriggerMask,
                 AutoChessGasRuntimeAccessContract.EntryCount,
                 AutoChessGasRuntimeAccessContract.EcsHandleProxyCount,
                AutoChessGasRuntimeAccessContract.ManualSyncCount,
                AutoChessGasRuntimeAccessContract.PerformancePassRiskCount,
                AutoChessGasRuntimeAccessContract.BattleHashAffectingCount,
                AutoChessGasRuntimeAccessContract.CapabilityMask,
                 gasConceptCoverageMask,
                 gasConceptMissingMask,
                 traceAbilityCode,
                 runtimeTraceStageMask,
                 runtimeTraceMissingStageMask,
                 observation.EffectLifecycleFactCount,
                 observation.AttributeFactCount,
                 observation.BoundaryFactCount,
                 observation.CueFactCount,
                  observation.EffectLifecycleFactCount,
                  observation.ExecutionCalculationFactCount,
                 observation.EffectLifecycleFactCount,
                 observation.EffectLifecycleFactCount > observation.RejectedEffectFactCount
                     ? observation.EffectLifecycleFactCount - observation.RejectedEffectFactCount
                     : 0,
                 0,
                 performanceResult.AcceptedCommandCount,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 observation.AttributeFactCount,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 observation.RejectedEffectFactCount,
                 observation.RingHighWater,
                 observation.RingCapacity,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
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
                 "runtime-v1-boundary-read-model",
                 "runtime-v1-boundary-read-model",
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
                   + $"summaryHash=0x{runResult.Evidence.SummaryHash:X8}, "
                   + CreateRuntimeV1Summary(in runResult.PerformanceResult);
        }

        /// <summary>
        /// 输出 Runtime v1 Boundary 与 Session 诊断字段，作为报告的事实源摘要。
        /// </summary>
        private static string CreateRuntimeV1Summary(in AutoChessBattleResult result)
        {
            return CreateRuntimeV1Summary(in result.RuntimeV1Observation);
        }

        /// <summary>
        /// 输出 immutable Boundary read-model 的完整计数与确定性字段。
        /// </summary>
        private static string CreateRuntimeV1Summary(
            in AutoChessGasV1ObservationSnapshot observation)
        {
            return $"runtimeV1BoundaryBatches={observation.BoundaryBatchCount}, "
                   + $"runtimeV1BoundaryFacts={observation.BoundaryFactCount}, "
                   + $"runtimeV1GameplayFacts={observation.GameplayFactCount}, "
                   + $"runtimeV1TeardownFacts={observation.TeardownFactCount}, "
                   + $"runtimeV1NoFactReceipts={observation.NoFactReceiptCount}, "
                   + $"runtimeV1DeadShells={observation.DeadShellCount}, "
                   + $"runtimeV1AttributeFacts={observation.AttributeFactCount}, "
                   + $"runtimeV1TagFacts={observation.TagFactCount}, "
                   + $"runtimeV1AbilityLifecycleFacts={observation.AbilityLifecycleFactCount}, "
                   + $"runtimeV1EffectLifecycleFacts={observation.EffectLifecycleFactCount}, "
                   + $"runtimeV1ExecutionCalculationFacts={observation.ExecutionCalculationFactCount}, "
                   + $"runtimeV1PeriodTickFacts={observation.PeriodTickFactCount}, "
                   + $"runtimeV1PeriodTickDamageTotal={observation.PeriodTickDamageTotal:0.###}, "
                   + $"runtimeV1CueFacts={observation.CueFactCount}, "
                   + $"runtimeV1BattleOutcomeFacts={observation.BattleOutcomeFactCount}, "
                   + $"runtimeV1SessionLifecycleFacts={observation.SessionLifecycleFactCount}, "
                   + $"runtimeV1DeathFacts={observation.DeathFactCount}, "
                   + $"runtimeV1FaultFacts={observation.FaultFactCount}, "
                   + $"runtimeV1RejectedEffectFacts={observation.RejectedEffectFactCount}, "
                   + $"runtimeV1ProjectedEntries={observation.StructuredLogEntryCount}, "
                   + $"runtimeV1NegativeHealthFacts={observation.NegativeHealthFactCount}, "
                   + $"runtimeV1NegativeHealthTotal={observation.NegativeHealthTotal:0.###}, "
                   + $"runtimeV1InvalidFacts={observation.InvalidFactCount}, "
                   + $"runtimeV1RingHighWater={observation.RingHighWater}/{observation.RingCapacity}, "
                   + $"runtimeV1DrainFailures={observation.DrainFailureCount}, "
                   + $"runtimeV1LastDrainFailure={observation.LastDrainFailure}, "
                   + $"runtimeV1BoundarySequenceHash=0x{observation.BoundarySequenceHash:X8}, "
                   + CreateRuntimeV1SessionSummary(in observation);
        }

        /// <summary>
        /// 输出 Session observation 的准入、inbox 与首 ASC 健康值。
        /// </summary>
        private static string CreateRuntimeV1SessionSummary(
            in AutoChessGasV1ObservationSnapshot observation)
        {
            if (!observation.HasSessionObservation)
                return "runtimeV1SessionObservation=unavailable";

            var session = observation.SessionObservation;
            return $"runtimeV1SessionTick={session.CurrentTick}, "
                   + $"runtimeV1SessionState={session.SessionState}, "
                   + $"runtimeV1SessionFaultReason={session.FaultReasonCode}, "
                   + $"runtimeV1SessionFaultId={session.FaultId}, "
                   + $"runtimeV1BattleCount={session.BattleCount}, "
                   + $"runtimeV1ReadyBattleCount={session.ReadyBattleCount}, "
                   + $"runtimeV1AscCount={session.AscCount}, "
                   + $"runtimeV1ReadyAscCount={session.ReadyAscCount}, "
                   + $"runtimeV1FirstBattleState={session.FirstBattleState}, "
                   + $"runtimeV1FirstBattleIngressOpen={session.FirstBattleIngressOpen}, "
                   + $"runtimeV1Inbox={session.InboxCount}/{session.PendingInboxCount}/{session.ConsumedInboxCount}, "
                   + $"runtimeV1FirstAscScenarioUnitId={session.FirstAscScenarioUnitId}, "
                   + $"runtimeV1FirstAscLifecycle={session.FirstAscLifecycle}, "
                   + $"runtimeV1FirstAscHealth={session.FirstAscHealth:0.###}, "
                   + $"runtimeV1FirstAscEnergy={session.FirstAscEnergy:0.###}, "
                   + $"runtimeV1AdmissionSucceeded={session.TickDiagnostics.AdmissionSucceeded != 0}, "
                   + $"runtimeV1AdmissionReason={session.TickDiagnostics.AdmissionReasonCode}, "
                   + $"runtimeV1ExecutedLaneMask=0x{session.TickDiagnostics.ExecutedLaneMask:X}, "
                   + $"runtimeV1SealedCommandCount={session.TickDiagnostics.SealedCommandCount}, "
                   + $"runtimeV1ApplicationOutcomeCount={session.TickDiagnostics.ApplicationOutcomeCount}, "
                   + $"runtimeV1AttributeMutationCount={session.TickDiagnostics.AttributeMutationCount}, "
                   + $"runtimeV1DeathFactCount={session.TickDiagnostics.DeathFactCount}, "
                   + $"runtimeV1TickBoundaryFactCount={session.TickDiagnostics.BoundaryFactCount}";
        }

        public static string CreateHeadlessLogicBudgetSummary(
            in AutoChessHeadlessLogicBudgetResult budget)
        {
            return $"passed={budget.Passed}, "
                   + $"performanceExcellentPassed={budget.PerformanceExcellentPassed}, "
                   + $"failureMask=0x{budget.FailureMask:X}, "
                   + $"performanceTimingAvailable={budget.PerformanceTimingAvailable}, "
                   + $"scorecardSource=RuntimeV1BoundaryPerformanceScorecard, "
                   + $"metricFamilySource=performance-only-secondary, "
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
                   + $"ownerLocalFactChangedChunks={budget.OwnerLocalFactChangedChunkCount}, "
                   + $"ownerLocalFactScannedOwners={budget.OwnerLocalFactScannedOwnerCount}, "
                   + $"ownerLocalFactDirtyOwners={budget.OwnerLocalFactDirtyOwnerCount}, "
                   + $"ownerLocalFactSkippedOwners={budget.OwnerLocalFactSkippedOwnerCount}, "
                   + $"ownerLocalFactClearedOwners={budget.OwnerLocalFactClearedOwnerCount}, "
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

        /// <summary>
        /// 输出 Runtime v1 Boundary read-model 的完整性门槛。
        /// </summary>
        public static string CreateRuntimeV1BoundaryCoverageSummary(
            in AutoChessBattleResult result)
        {
            var observation = result.RuntimeV1Observation;
            return $"passed={HasRuntimeV1BoundaryCoverage(in result)}, "
                   + $"batches={observation.BoundaryBatchCount}, "
                   + $"facts={observation.BoundaryFactCount}, "
                   + $"projectedEntries={observation.StructuredLogEntryCount}, "
                   + $"deadShells={observation.DeadShellCount}, "
                   + $"invalidFacts={observation.InvalidFactCount}, "
                   + $"drainFailures={observation.DrainFailureCount}, "
                   + $"ringHighWater={observation.RingHighWater}/{observation.RingCapacity}, "
                   + $"sequenceHash=0x{observation.BoundarySequenceHash:X8}";
        }

        /// <summary>
        /// 验证本局 Boundary facts 已完整进入唯一 managed read-model。
        /// </summary>
        public static bool HasRuntimeV1BoundaryCoverage(in AutoChessBattleResult result)
        {
            var observation = result.RuntimeV1Observation;
            return observation.HasSessionObservation
                   && observation.BoundaryBatchCount > 0
                   && observation.BoundaryFactCount > 0
                   && observation.StructuredLogEntryCount == observation.BoundaryFactCount
                   && observation.InvalidFactCount == 0
                   && observation.DrainFailureCount == 0;
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
                   + $"spawnFinalizeMaintenanceTicks={evidence.SpawnFinalizeMaintenanceTicks}, "
                   + $"measuredTicks={evidence.MeasuredTicks}, "
                   + $"commands={evidence.CommandCount}, "
                   + $"runtimeV1BoundaryFacts={evidence.CoreFactCount}, "
                   + $"runtimeV1AttributeFacts={evidence.AttributeChangeCount}, "
                   + $"runtimeV1PeriodTickFacts={evidence.PeriodTickDamageFactCount}, "
                   + $"runtimeV1PeriodTickDamageTotal={evidence.PeriodTickDamageTotal:0.###}, "
                   + $"runtimeV1ExecutionCalculationFacts={evidence.ExecutionOutputCount}, "
                   + $"runtimeV1CueFacts={evidence.CueRequestCount}, "
                   + $"runtimeV1RejectedEffectFacts={evidence.DebugWarningCount}, "
                   + $"runtimeV1FaultFacts={evidence.DebugErrorCount}, "
                   + $"runtimeV1BlockingFaults={evidence.BlockingDebugErrorCount}, "
                   + $"runtimeV1BoundaryRingHighWater={evidence.PeakBoundaryRingLength}, "
                   + $"presentationMarkers={evidence.PresentationMarkerCount}, "
                   + $"presentationSourceLines={evidence.PresentationSourceLineCount}, "
                   + $"presentationDisplayedLines={evidence.PresentationDisplayLineCount}, "
                   + $"presentationDroppedLines={evidence.PresentationDroppedLineCount}, "
                   + $"presentationDisabledReason={evidence.PresentationDisabledReason}, "
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
                    + $"runtimeV1TraceFacts={evidence.RuntimeTraceFactCount}, "
                    + $"runtimeV1TraceCues={evidence.RuntimeTraceCueCount}, "
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
                    + $"ownerLocalFactChangedChunks={evidence.OwnerLocalFactChangedChunkCount}, "
                    + $"ownerLocalFactScannedOwners={evidence.OwnerLocalFactScannedOwnerCount}, "
                    + $"ownerLocalFactDirtyOwners={evidence.OwnerLocalFactDirtyOwnerCount}, "
                    + $"ownerLocalFactSkippedOwners={evidence.OwnerLocalFactSkippedOwnerCount}, "
                    + $"ownerLocalFactClearedOwners={evidence.OwnerLocalFactClearedOwnerCount}, "
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
                   + $"totalElapsedMs={evidence.TotalElapsedMilliseconds:0.000}, "
                    + $"runtimeV1BoundarySequenceHash=0x{evidence.FactsHash:X8}, "
                    + $"summaryHash=0x{evidence.SummaryHash:X8}, "
                   + $"avgTickMs={evidence.AverageTickMilliseconds:0.000}";
        }

        public static string CreateDebuggerSummary(in AutoChessBattleResult result)
        {
            var observation = result.RuntimeV1Observation;
            return "runtimeObservationSource=RuntimeV1BoundaryReadModel, "
                   + $"runtimeV1SessionPresent={observation.HasSessionObservation}, "
                   + $"runtimeV1BoundaryFacts={observation.BoundaryFactCount}, "
                   + $"runtimeV1GameplayFacts={observation.GameplayFactCount}, "
                   + $"runtimeV1TeardownFacts={observation.TeardownFactCount}, "
                   + $"runtimeV1NoFactReceipts={observation.NoFactReceiptCount}, "
                   + $"runtimeV1DeadShells={observation.DeadShellCount}, "
                   + $"runtimeV1AttributeFacts={observation.AttributeFactCount}, "
                   + $"runtimeV1TagFacts={observation.TagFactCount}, "
                   + $"runtimeV1AbilityLifecycleFacts={observation.AbilityLifecycleFactCount}, "
                   + $"runtimeV1EffectLifecycleFacts={observation.EffectLifecycleFactCount}, "
                   + $"runtimeV1ExecutionCalculationFacts={observation.ExecutionCalculationFactCount}, "
                   + $"runtimeV1PeriodTickFacts={observation.PeriodTickFactCount}, "
                   + $"runtimeV1PeriodTickDamageTotal={observation.PeriodTickDamageTotal:0.###}, "
                   + $"runtimeV1CueFacts={observation.CueFactCount}, "
                   + $"runtimeV1BattleOutcomeFacts={observation.BattleOutcomeFactCount}, "
                   + $"runtimeV1SessionLifecycleFacts={observation.SessionLifecycleFactCount}, "
                   + $"runtimeV1DeathFacts={observation.DeathFactCount}, "
                   + $"runtimeV1FaultFacts={observation.FaultFactCount}, "
                   + $"runtimeV1RejectedEffectFacts={observation.RejectedEffectFactCount}, "
                   + $"runtimeV1InvalidFacts={observation.InvalidFactCount}, "
                   + $"runtimeV1ProjectedEntries={observation.StructuredLogEntryCount}, "
                   + $"runtimeV1NegativeHealthFacts={observation.NegativeHealthFactCount}, "
                   + $"runtimeV1NegativeHealthTotal={observation.NegativeHealthTotal:0.###}, "
                   + $"runtimeV1RingHighWater={observation.RingHighWater}/{observation.RingCapacity}, "
                   + $"runtimeV1DrainFailures={observation.DrainFailureCount}, "
                   + $"runtimeV1BoundarySequenceHash=0x{observation.BoundarySequenceHash:X8}, "
                   + CreateRuntimeV1SessionSummary(in observation);
        }

        public static string CreateTimingSummary(
            in AutoChessBattleResult result,
            in AutoChessValidationEvidence evidence)
        {
            var builder = new StringBuilder(512);
            builder.Append("ecsRuntimeTickOnly=")
                .Append(evidence.EcsRuntimeTickOnly)
                .Append(", ownerSplit=GasFixedTick/GasCommandIngress/GasTickKernel/GasBoundaryDrain/runner")
                .Append(", physicsDisabledReason=")
                .Append(evidence.PhysicsDisabledReason)
                .Append(", renderDisabledReason=")
                .Append(evidence.RenderDisabledReason)
                .Append(", presentationDisabledReason=")
                .Append(evidence.PresentationDisabledReason);
            AppendTiming(builder, "CoreRuntimeOwner", result.RuntimeTiming.CoreRuntime);
            AppendTiming(builder, "BoundaryOwner", result.RuntimeTiming.Boundary);
            AppendTiming(builder, "RuntimeV1ObservationExport", result.RuntimeTiming.Debugger);
            AppendTiming(builder, "AutoChessValidationRunner", result.RuntimeTiming.Runner);
            AppendTiming(builder, "PhysicsOwner", result.RuntimeTiming.Physics);
            AppendTiming(builder, "RenderOwner", result.RuntimeTiming.Render);
            AppendTiming(builder, "GASTickTotal", result.RuntimeTiming.TickTotal);
            AppendTiming(builder, "GasFixedTickSystemGroup", result.RuntimeTiming.FramePrepare);
            AppendTiming(builder, "GasCommandIngressSystem", result.RuntimeTiming.CommandResolve);
            AppendTiming(builder, "GasTickKernelCore", result.RuntimeTiming.CoreSimulation);
            AppendTiming(builder, "GasTickKernelStructuralCommit", result.RuntimeTiming.StructuralCommit);
            AppendTiming(builder, "GasBoundaryDrainProjection", result.RuntimeTiming.BoundaryProjection);
            AppendTiming(builder, "GasBoundaryDrainDependency", result.RuntimeTiming.DependencyDrain);
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
            var observation = performanceResult.RuntimeV1Observation;
            var diagnosticObservation = diagnosticResult.RuntimeV1Observation;
            return $"acceptedCommands={performanceResult.AcceptedCommandCount}, "
                   + $"tickAvgMs={performanceResult.RuntimeTiming.TickTotal.AverageMilliseconds:0.000}, "
                   + $"gasFixedTickAvgMs={performanceResult.RuntimeTiming.FramePrepare.AverageMilliseconds:0.000}, "
                   + $"commandIngressAvgMs={performanceResult.RuntimeTiming.CommandResolve.AverageMilliseconds:0.000}, "
                   + $"gasTickKernelAvgMs={performanceResult.RuntimeTiming.CoreSimulation.AverageMilliseconds:0.000}, "
                   + $"boundaryDrainAvgMs={performanceResult.RuntimeTiming.BoundaryProjection.AverageMilliseconds:0.000}, "
                   + $"runtimeV1BoundaryBatches={observation.BoundaryBatchCount}, "
                   + $"runtimeV1BoundaryFacts={observation.BoundaryFactCount}, "
                   + $"runtimeV1AttributeFacts={observation.AttributeFactCount}, "
                   + $"runtimeV1EffectLifecycleFacts={observation.EffectLifecycleFactCount}, "
                   + $"runtimeV1ExecutionCalculationFacts={observation.ExecutionCalculationFactCount}, "
                   + $"runtimeV1PeriodTickFacts={observation.PeriodTickFactCount}, "
                   + $"runtimeV1PeriodTickDamageTotal={observation.PeriodTickDamageTotal:0.###}, "
                   + $"runtimeV1CueFacts={observation.CueFactCount}, "
                   + $"runtimeV1DeathFacts={observation.DeathFactCount}, "
                   + $"runtimeV1RejectedEffectFacts={observation.RejectedEffectFactCount}, "
                   + $"runtimeV1InvalidFacts={observation.InvalidFactCount}, "
                   + $"runtimeV1RingHighWater={observation.RingHighWater}/{observation.RingCapacity}, "
                   + $"runtimeV1DrainFailures={observation.DrainFailureCount}, "
                   + $"runtimeV1SequenceHash=0x{observation.BoundarySequenceHash:X8}, "
                   + $"diagnosticSequenceHashMatch={observation.BoundarySequenceHash == diagnosticObservation.BoundarySequenceHash}, "
                   + $"runtimeV1NegativeHealthTotal={observation.NegativeHealthTotal:0.###}";
        }

        public static string CreateHotspotAttributionMatrix(
            in AutoChessBattleResult performanceResult,
            in AutoChessBattleResult diagnosticResult,
            in AutoChessHeadlessLogicBudgetResult budget)
        {
            var observation = performanceResult.RuntimeV1Observation;
            var diagnosticObservation = diagnosticResult.RuntimeV1Observation;
            var builder = new StringBuilder(1024);
            AppendAttributionRow(
                builder,
                "RUNTIME-V1-BOUNDARY",
                "High",
                "BoundaryFacts",
                "GasBoundaryDrainCoordinator",
                "BatchDrain",
                "GasBoundaryDrainCoordinator",
                "GasBoundaryDrainBatch",
                "Facts",
                observation.BoundaryFactCount,
                "BoundedRingDrain",
                "RuntimeV1BoundaryReadModel",
                "preserve immutable facts and sequence order",
                $"batches={observation.BoundaryBatchCount};facts={observation.BoundaryFactCount}");
            AppendAttributionRow(
                builder,
                "RUNTIME-V1-EFFECT",
                "High",
                "EffectLifecycle",
                "GasTickKernelSystem",
                "ApplicationOutcome",
                "GasGameplayEffectTransaction",
                "BoundaryFactBuffer",
                "EffectLifecycle",
                observation.EffectLifecycleFactCount,
                "TypedOutcome",
                "RuntimeV1BoundaryReadModel",
                "retain applied and rejected outcomes",
                $"rejected={observation.RejectedEffectFactCount}");
            AppendAttributionRow(
                builder,
                "RUNTIME-V1-EXECUTION",
                "High",
                "ExecutionCalculation",
                "GasTickKernelSystem",
                "ExecutionFact",
                "GasTickKernelSystem",
                "BoundaryFactBuffer",
                "ExecutionCalculation",
                observation.ExecutionCalculationFactCount,
                "TypedFact",
                "RuntimeV1BoundaryReadModel",
                "consume execution output facts only",
                $"sequenceHash=0x{observation.BoundarySequenceHash:X8}");
            AppendAttributionRow(
                builder,
                "RUNTIME-V1-ATTRIBUTE",
                "High",
                "AttributeState",
                "GasTickKernelSystem",
                "AttributeChanged",
                "GasGameplayEffectTransaction",
                "BoundaryFactBuffer",
                "AttributeDelta",
                observation.AttributeFactCount,
                "TargetOwnedMutation",
                "RuntimeV1BoundaryReadModel",
                "consume canonical attribute facts",
                $"negativeHealthFacts={observation.NegativeHealthFactCount}");
            AppendAttributionRow(
                builder,
                "RUNTIME-V1-PERIOD",
                "Medium",
                "PeriodTick",
                "GasTickKernelSystem",
                "PeriodTick",
                "GasGameplayEffectTransaction",
                "BoundaryFactBuffer",
                "PeriodDamage",
                observation.PeriodTickFactCount,
                "TypedFact",
                "RuntimeV1BoundaryReadModel",
                "read damage from period and attribute facts",
                $"damageTotal={observation.PeriodTickDamageTotal:0.###}");
            AppendAttributionRow(
                builder,
                "RUNTIME-V1-CUE",
                "Medium",
                "CueLifecycle",
                "GasBoundaryDrainCoordinator",
                "CueFact",
                "GasBoundaryDrainCoordinator",
                "BoundaryFactBuffer",
                "Cue",
                observation.CueFactCount,
                "TypedFact",
                "RuntimeV1BoundaryReadModel",
                "keep cue lifecycle in boundary plane",
                $"deadShells={observation.DeadShellCount}");
            AppendAttributionRow(
                builder,
                "RUNTIME-V1-FAULT",
                "High",
                "BoundaryIntegrity",
                "RuntimeV1BoundaryReadModel",
                "FaultAndReject",
                "RuntimeV1BoundaryReadModel",
                "GasBoundaryDrainFailure",
                "Integrity",
                observation.FaultFactCount + observation.InvalidFactCount
                    + observation.DrainFailureCount,
                "ExplicitFailure",
                "Validation",
                "fail validation on any unbounded or invalid boundary state",
                $"invalid={observation.InvalidFactCount};drainFailures={observation.DrainFailureCount}");
            builder.Append("runtimeV1HotspotSource=RuntimeV1BoundaryReadModel")
                .Append("|diagnosticSequenceHashMatch=")
                .Append(observation.BoundarySequenceHash == diagnosticObservation.BoundarySequenceHash)
                .Append("|profilerPass=")
                .Append(budget.ProfilerCaptureState)
                .AppendLine();
            return builder.ToString();
        }

        public static string CreateBoundaryOwnerSummary(in AutoChessBattleResult result)
        {
            var observation = result.RuntimeV1Observation;
            return "shellPublicRawEcsSurface=false, "
                   + "unitIdentity=ScenarioUnitId, "
                   + "unitRuntimeHandle=OwnerAscHandle, "
                   + "unitCreateOwner=GasStageBBootstrapRecorder, "
                   + "unitDestroyOwner=GasBoundaryDrainCoordinator, "
                   + "catalogOwner=AutoChessGasCatalogSession, "
                   + "boundaryFactOwner=GasBoundaryDrainCoordinator, "
                   + "observationOwner=RuntimeV1BoundaryReadModel, "
                   + "reportProjectionOwner=RuntimeV1BoundaryReadModel, "
                   + "runnerSyncOwner=AutoChessGasRuntimeTicker, "
                   + $"runtimeV1BoundaryBatches={observation.BoundaryBatchCount}, "
                   + $"runtimeV1BoundaryFacts={observation.BoundaryFactCount}, "
                   + $"runtimeV1DeadShells={observation.DeadShellCount}, "
                   + $"runtimeV1RingHighWater={observation.RingHighWater}/{observation.RingCapacity}, "
                   + $"runtimeV1DrainFailures={observation.DrainFailureCount}, "
                   + $"runtimeV1InvalidFacts={observation.InvalidFactCount}, "
                   + $"runtimeV1BoundarySequenceHash=0x{observation.BoundarySequenceHash:X8}, "
                   + $"runtimeAccessContractEntries={AutoChessGasRuntimeAccessContract.EntryCount}, "
                   + $"runtimeAccessEcsHandleProxies={AutoChessGasRuntimeAccessContract.EcsHandleProxyCount}, "
                   + $"runtimeAccessManualSync={AutoChessGasRuntimeAccessContract.ManualSyncCount}, "
                   + $"runtimeAccessPerformancePassRisks={AutoChessGasRuntimeAccessContract.PerformancePassRiskCount}";
        }

        public static string CreateRuntimeAccessContractSummary()
        {
            return AutoChessGasRuntimeAccessContract.CreateSummary();
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static void AppendAttributionRow(
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

            builder.Append("hotspotAttribution|source=AutoChessBattleValidationReport")
                .Append("|readModel=RuntimeV1BoundaryReadModel")
                .Append("|passMode=DerivedExport")
                .Append("|costDomain=RuntimeV1Boundary")
                .Append("|evidenceTier=ValidationEvidence")
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

        public static string CreateOfficialToolDiffSummary(
            in AutoChessBattleResult result,
            in AutoChessValidationEvidence evidence)
        {
            var official = result.OfficialToolDiff;
            var observation = result.RuntimeV1Observation;
            return $"runtimeV1BoundarySequenceHash=0x{observation.BoundarySequenceHash:X8}, "
                   + $"runtimeV1BoundaryFacts={observation.BoundaryFactCount}, "
                   + $"runtimeV1DrainFailures={observation.DrainFailureCount}, "
                   + $"officialDiffSeparatePass={evidence.OfficialDiffSeparatePass}, "
                   + $"journalingAvailable={evidence.JournalingAvailable}, "
                   + $"journalingCaptured={evidence.JournalingCaptured}, "
                   + $"journalingWorldRecords={official.JournalingWorldRecordCount}, "
                   + $"journalingStructuralRecords={official.JournalingStructuralRecordCount}, "
                   + $"journalingCreates={official.JournalingCreateEntityCount}, "
                   + $"journalingDestroys={official.JournalingDestroyEntityCount}, "
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
            var observation = result.RuntimeV1Observation;
            var tick = observation.HasSessionObservation
                ? observation.SessionObservation.TickDiagnostics
                : default;
            return "```mermaid\n"
                   + "flowchart LR\n"
                   + $"    CommandPort[\"ASCCommandPort\\naccepted commands: {result.AcceptedCommandCount}\"] --> Ingress[\"GasCommandIngressSystem\\nsealed commands: {tick.SealedCommandCount}\"]\n"
                   + $"    Ingress --> Kernel[\"GasTickKernelSystem\\napplication outcomes: {tick.ApplicationOutcomeCount}\"]\n"
                   + $"    Kernel --> Project[\"BoundaryProject\\ncore facts: {tick.CoreFactCount}\"]\n"
                   + $"    Project --> Ring[\"GasBoundaryDrainCoordinator\\nbatches: {observation.BoundaryBatchCount}, high-water: {observation.RingHighWater}/{observation.RingCapacity}\"]\n"
                   + $"    Ring --> ReadModel[\"RuntimeV1BoundaryReadModel\\nfacts: {observation.BoundaryFactCount}, invalid: {observation.InvalidFactCount}\"]\n"
                   + $"    ReadModel --> Validation[\"AutoChessBattleValidationReport\\nsequence hash: 0x{observation.BoundarySequenceHash:X8}\"]\n"
                   + $"    ReadModel -. separate official-tool pass .-> Official[\"Journaling / Profiler\\nrecords: {result.OfficialToolDiff.JournalingWorldRecordCount}\"]\n"
                   + "```";
        }

        public static string CreateSequenceDiagram(in AutoChessBattleResult result)
        {
            var observation = result.RuntimeV1Observation;
            var tick = observation.HasSessionObservation
                ? observation.SessionObservation.TickDiagnostics
                : default;
            return "```mermaid\n"
                   + "sequenceDiagram\n"
                   + "    participant Runner as AutoChess Validation Runner\n"
                   + "    participant Port as ASCCommandPort\n"
                   + "    participant Ingress as GasCommandIngressSystem\n"
                   + "    participant Kernel as GasTickKernelSystem\n"
                   + "    participant Project as BoundaryProject\n"
                   + "    participant Ring as GasBoundaryDrainCoordinator\n"
                   + "    participant ReadModel as RuntimeV1BoundaryReadModel\n"
                   + "    participant Validation as ValidationReport\n"
                   + $"    Runner->>Port: enqueue commands {result.AcceptedCommandCount}\n"
                   + $"    Port->>Ingress: seal commands {tick.SealedCommandCount}\n"
                   + $"    Ingress->>Kernel: admitted={tick.AdmissionSucceeded != 0}, tick={tick.CandidateTick}\n"
                   + $"    Kernel->>Project: outcomes {tick.ApplicationOutcomeCount}, attributes {tick.AttributeMutationCount}\n"
                   + $"    Project->>Ring: immutable facts {tick.BoundaryFactCount}\n"
                   + $"    Ring->>ReadModel: batches {observation.BoundaryBatchCount}, facts {observation.BoundaryFactCount}\n"
                   + $"    ReadModel->>Validation: hash 0x{observation.BoundarySequenceHash:X8}, faults {observation.FaultFactCount}, invalid {observation.InvalidFactCount}\n"
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

        private static uint CalculateSummaryHash(
            in AutoChessBattleResult result,
            uint factsHash)
        {
            unchecked
            {
                var observation = result.RuntimeV1Observation;
                var hash = AppendHash(2166136261u, (int)factsHash);
                hash = AppendHash(hash, result.Completed ? 1 : 0);
                hash = AppendHash(hash, (int)result.Winner);
                hash = AppendHash(hash, result.ScenarioScale);
                hash = AppendHash(hash, result.BattleTicks);
                hash = AppendHash(hash, result.AcceptedCommandCount);
                hash = AppendHash(hash, observation.GameplayFactCount);
                hash = AppendHash(hash, observation.AttributeFactCount);
                hash = AppendHash(hash, observation.EffectLifecycleFactCount);
                hash = AppendHash(hash, observation.ExecutionCalculationFactCount);
                hash = AppendHash(hash, observation.PeriodTickFactCount);
                hash = AppendHash(hash, observation.CueFactCount);
                hash = AppendHash(hash, observation.BattleOutcomeFactCount);
                hash = AppendHash(hash, observation.DeathFactCount);
                hash = AppendHash(hash, observation.RejectedEffectFactCount);
                hash = AppendHash(hash, observation.InvalidFactCount);
                hash = AppendHash(hash, observation.DrainFailureCount);
                if (observation.HasSessionObservation)
                {
                    var session = observation.SessionObservation;
                    hash = AppendHash(hash, unchecked((int)session.CurrentTick));
                    hash = AppendHash(hash, session.FaultReasonCode);
                    hash = AppendHash(hash, session.TickDiagnostics.ApplicationOutcomeCount);
                    hash = AppendHash(hash, session.TickDiagnostics.AttributeMutationCount);
                }
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

    }
}
