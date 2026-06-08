using System;
using System.Collections;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    internal readonly struct AutoChessHeadlessLogicBudgetResult
    {
        public const double MeasuredAverageTickBudgetMs = 1.50d;
        public const double GasTickAverageBudgetMs = 1.50d;
        public const double GasTickMaxBudgetMs = 3.00d;
        public const double CoreRuntimeAverageBudgetMs = 1.00d;
        public const double CoreSimulationAverageBudgetMs = 0.90d;
        public const double BoundaryAverageBudgetMs = 0.35d;
        public const double RunnerAverageBudgetMs = 0.15d;

        private const int FailureNoPerformanceSamples = 1 << 0;
        private const int FailureMeasuredAverageTick = 1 << 1;
        private const int FailureGasTickAverage = 1 << 2;
        private const int FailureGasTickMax = 1 << 3;
        private const int FailureCoreRuntimeAverage = 1 << 4;
        private const int FailureCoreSimulationAverage = 1 << 5;
        private const int FailureBoundaryAverage = 1 << 6;
        private const int FailureRunnerAverage = 1 << 7;
        private const int FailurePerformanceObservationPollution = 1 << 8;

        public readonly bool Passed;
        public readonly bool PerformanceTimingAvailable;
        public readonly bool MeasuredAverageTickPassed;
        public readonly bool GasTickAveragePassed;
        public readonly bool GasTickMaxPassed;
        public readonly bool CoreRuntimeAveragePassed;
        public readonly bool CoreSimulationAveragePassed;
        public readonly bool BoundaryAveragePassed;
        public readonly bool RunnerAveragePassed;
        public readonly bool PerformanceObservationCleanPassed;
        public readonly bool ProfilerEvidencePassed;
        public readonly int FailureMask;
        public readonly GasRuntimeDataOrientedScorecard RuntimeScorecard;
        public readonly int UnitCount;
        public readonly int MeasuredTicks;
        public readonly int CommandCount;
        public readonly int CoreFactCount;
        public readonly int ActiveMutationCommandCount;
        public readonly int ActiveMutationOwnerGroupCount;
        public readonly int ActiveMutationMaxOwnerRange;
        public readonly int ActiveMutationEstimatedRandomLookupCount;
        public readonly int PendingAttributeDeltaCount;
        public readonly int PendingAttributeTargetGroupCount;
        public readonly int PendingAttributeMaxTargetRange;
        public readonly int PendingAttributeEstimatedRandomLookupCount;
        public readonly int OwnerLocalFactCount;
        public readonly int OwnerLocalFactOwnerGroupCount;
        public readonly int OwnerLocalFactMaxOwnerRange;
        public readonly int OwnerLocalFactFlushCount;
        public readonly int ActiveEffectSlotCount;
        public readonly int ActiveEffectSlotCapacity;
        public readonly int ActiveEffectChunkSkipDuePeriodSlotCount;
        public readonly int QueryBudget;
        public readonly int LookupUpdateBudget;
        public readonly int RandomLookupBudget;
        public readonly int SyncQueryBudget;
        public readonly int DependencyWaitRiskCount;
        public readonly int PerformanceObservationPollutionRiskCount;
        public readonly double MeasuredAverageTickMilliseconds;
        public readonly double GasTickAverageMilliseconds;
        public readonly double GasTickMaxMilliseconds;
        public readonly double CoreRuntimeAverageMilliseconds;
        public readonly double CoreSimulationAverageMilliseconds;
        public readonly double BoundaryAverageMilliseconds;
        public readonly double RunnerAverageMilliseconds;
        public readonly double CommandsPerMeasuredTick;
        public readonly double CoreFactsPerMeasuredTick;
        public readonly double MeasuredMicrosecondsPerUnit;
        public readonly double GasTickMicrosecondsPerUnit;
        public readonly double CoreSimulationMicrosecondsPerUnit;
        public readonly double GasTickMicrosecondsPerCommand;
        public readonly double CoreSimulationMicrosecondsPerCoreFact;
        public readonly string ProfilerCaptureState;

        public bool PerformanceExcellentPassed => Passed && ProfilerEvidencePassed;

        private AutoChessHeadlessLogicBudgetResult(
            bool passed,
            bool performanceTimingAvailable,
            bool measuredAverageTickPassed,
            bool gasTickAveragePassed,
            bool gasTickMaxPassed,
            bool coreRuntimeAveragePassed,
            bool coreSimulationAveragePassed,
            bool boundaryAveragePassed,
            bool runnerAveragePassed,
            bool performanceObservationCleanPassed,
            bool profilerEvidencePassed,
            int failureMask,
            in GasRuntimeDataOrientedScorecard runtimeScorecard,
            int unitCount,
            int measuredTicks,
            int commandCount,
            int coreFactCount,
            int activeMutationCommandCount,
            int activeMutationOwnerGroupCount,
            int activeMutationMaxOwnerRange,
            int activeMutationEstimatedRandomLookupCount,
            int pendingAttributeDeltaCount,
            int pendingAttributeTargetGroupCount,
            int pendingAttributeMaxTargetRange,
            int pendingAttributeEstimatedRandomLookupCount,
            int ownerLocalFactCount,
            int ownerLocalFactOwnerGroupCount,
            int ownerLocalFactMaxOwnerRange,
            int ownerLocalFactFlushCount,
            int activeEffectSlotCount,
            int activeEffectSlotCapacity,
            int activeEffectChunkSkipDuePeriodSlotCount,
            int queryBudget,
            int lookupUpdateBudget,
            int randomLookupBudget,
            int syncQueryBudget,
            int dependencyWaitRiskCount,
            int performanceObservationPollutionRiskCount,
            double measuredAverageTickMilliseconds,
            double gasTickAverageMilliseconds,
            double gasTickMaxMilliseconds,
            double coreRuntimeAverageMilliseconds,
            double coreSimulationAverageMilliseconds,
            double boundaryAverageMilliseconds,
            double runnerAverageMilliseconds,
            double commandsPerMeasuredTick,
            double coreFactsPerMeasuredTick,
            double measuredMicrosecondsPerUnit,
            double gasTickMicrosecondsPerUnit,
            double coreSimulationMicrosecondsPerUnit,
            double gasTickMicrosecondsPerCommand,
            double coreSimulationMicrosecondsPerCoreFact,
            string profilerCaptureState)
        {
            Passed = passed;
            PerformanceTimingAvailable = performanceTimingAvailable;
            MeasuredAverageTickPassed = measuredAverageTickPassed;
            GasTickAveragePassed = gasTickAveragePassed;
            GasTickMaxPassed = gasTickMaxPassed;
            CoreRuntimeAveragePassed = coreRuntimeAveragePassed;
            CoreSimulationAveragePassed = coreSimulationAveragePassed;
            BoundaryAveragePassed = boundaryAveragePassed;
            RunnerAveragePassed = runnerAveragePassed;
            PerformanceObservationCleanPassed = performanceObservationCleanPassed;
            ProfilerEvidencePassed = profilerEvidencePassed;
            FailureMask = failureMask;
            RuntimeScorecard = runtimeScorecard;
            UnitCount = unitCount;
            MeasuredTicks = measuredTicks;
            CommandCount = commandCount;
            CoreFactCount = coreFactCount;
            ActiveMutationCommandCount = activeMutationCommandCount;
            ActiveMutationOwnerGroupCount = activeMutationOwnerGroupCount;
            ActiveMutationMaxOwnerRange = activeMutationMaxOwnerRange;
            ActiveMutationEstimatedRandomLookupCount = activeMutationEstimatedRandomLookupCount;
            PendingAttributeDeltaCount = pendingAttributeDeltaCount;
            PendingAttributeTargetGroupCount = pendingAttributeTargetGroupCount;
            PendingAttributeMaxTargetRange = pendingAttributeMaxTargetRange;
            PendingAttributeEstimatedRandomLookupCount = pendingAttributeEstimatedRandomLookupCount;
            OwnerLocalFactCount = ownerLocalFactCount;
            OwnerLocalFactOwnerGroupCount = ownerLocalFactOwnerGroupCount;
            OwnerLocalFactMaxOwnerRange = ownerLocalFactMaxOwnerRange;
            OwnerLocalFactFlushCount = ownerLocalFactFlushCount;
            ActiveEffectSlotCount = activeEffectSlotCount;
            ActiveEffectSlotCapacity = activeEffectSlotCapacity;
            ActiveEffectChunkSkipDuePeriodSlotCount = activeEffectChunkSkipDuePeriodSlotCount;
            QueryBudget = queryBudget;
            LookupUpdateBudget = lookupUpdateBudget;
            RandomLookupBudget = randomLookupBudget;
            SyncQueryBudget = syncQueryBudget;
            DependencyWaitRiskCount = dependencyWaitRiskCount;
            PerformanceObservationPollutionRiskCount = performanceObservationPollutionRiskCount;
            MeasuredAverageTickMilliseconds = measuredAverageTickMilliseconds;
            GasTickAverageMilliseconds = gasTickAverageMilliseconds;
            GasTickMaxMilliseconds = gasTickMaxMilliseconds;
            CoreRuntimeAverageMilliseconds = coreRuntimeAverageMilliseconds;
            CoreSimulationAverageMilliseconds = coreSimulationAverageMilliseconds;
            BoundaryAverageMilliseconds = boundaryAverageMilliseconds;
            RunnerAverageMilliseconds = runnerAverageMilliseconds;
            CommandsPerMeasuredTick = commandsPerMeasuredTick;
            CoreFactsPerMeasuredTick = coreFactsPerMeasuredTick;
            MeasuredMicrosecondsPerUnit = measuredMicrosecondsPerUnit;
            GasTickMicrosecondsPerUnit = gasTickMicrosecondsPerUnit;
            CoreSimulationMicrosecondsPerUnit = coreSimulationMicrosecondsPerUnit;
            GasTickMicrosecondsPerCommand = gasTickMicrosecondsPerCommand;
            CoreSimulationMicrosecondsPerCoreFact = coreSimulationMicrosecondsPerCoreFact;
            ProfilerCaptureState = profilerCaptureState ?? string.Empty;
        }

        public static AutoChessHeadlessLogicBudgetResult Evaluate(
            in AutoChessBattleResult performanceResult)
        {
            var timing = performanceResult.RuntimeTiming;
            var performanceTimingAvailable = timing.TickTotal.Samples > 0;
            var profilerEvidencePassed =
                performanceResult.OfficialToolDiff.ProfilerAvailable
                && performanceResult.OfficialToolDiff.ProfilerEnabled;
            var scorecardInput = new GasRuntimeDataOrientedScorecardInput(
                performanceResult.Units.Length,
                performanceResult.MeasuredTicks,
                performanceResult.DriverIssuedCommands,
                performanceTimingAvailable,
                profilerEvidencePassed,
                performanceResult.AverageTickMilliseconds,
                timing.TickTotal.AverageMilliseconds,
                timing.TickTotal.MaxMilliseconds,
                timing.CoreRuntime.AverageMilliseconds,
                timing.CoreSimulation.AverageMilliseconds,
                timing.Boundary.AverageMilliseconds,
                timing.Runner.AverageMilliseconds,
                performanceResult.OfficialToolDiff.ProfilerCaptureState);
            var scorecard = GasRuntimeDataOrientedScorecard.Create(
                scorecardInput,
                performanceResult.RuntimeDiagnostics);
            var observationPollution = scorecard.PerformanceObservationPollutionRiskCount;

            var measuredAverageTickPassed =
                scorecard.MeasuredAverageTickMilliseconds <= MeasuredAverageTickBudgetMs;
            var gasTickAveragePassed =
                performanceTimingAvailable
                && scorecard.GasTickAverageMilliseconds <= GasTickAverageBudgetMs;
            var gasTickMaxPassed =
                performanceTimingAvailable
                && scorecard.GasTickMaxMilliseconds <= GasTickMaxBudgetMs;
            var coreRuntimeAveragePassed =
                performanceTimingAvailable
                && scorecard.CoreRuntimeAverageMilliseconds <= CoreRuntimeAverageBudgetMs;
            var coreSimulationAveragePassed =
                performanceTimingAvailable
                && scorecard.CoreSimulationAverageMilliseconds <= CoreSimulationAverageBudgetMs;
            var boundaryAveragePassed =
                performanceTimingAvailable
                && scorecard.BoundaryAverageMilliseconds <= BoundaryAverageBudgetMs;
            var runnerAveragePassed =
                performanceTimingAvailable
                && scorecard.RunnerAverageMilliseconds <= RunnerAverageBudgetMs;
            var performanceObservationCleanPassed = observationPollution == 0;

            var failureMask = 0;
            if (!performanceTimingAvailable)
                failureMask |= FailureNoPerformanceSamples;
            if (!measuredAverageTickPassed)
                failureMask |= FailureMeasuredAverageTick;
            if (!gasTickAveragePassed)
                failureMask |= FailureGasTickAverage;
            if (!gasTickMaxPassed)
                failureMask |= FailureGasTickMax;
            if (!coreRuntimeAveragePassed)
                failureMask |= FailureCoreRuntimeAverage;
            if (!coreSimulationAveragePassed)
                failureMask |= FailureCoreSimulationAverage;
            if (!boundaryAveragePassed)
                failureMask |= FailureBoundaryAverage;
            if (!runnerAveragePassed)
                failureMask |= FailureRunnerAverage;
            if (!performanceObservationCleanPassed)
                failureMask |= FailurePerformanceObservationPollution;

            return new AutoChessHeadlessLogicBudgetResult(
                failureMask == 0,
                performanceTimingAvailable,
                measuredAverageTickPassed,
                gasTickAveragePassed,
                gasTickMaxPassed,
                coreRuntimeAveragePassed,
                coreSimulationAveragePassed,
                boundaryAveragePassed,
                runnerAveragePassed,
                performanceObservationCleanPassed,
                profilerEvidencePassed,
                failureMask,
                scorecard,
                scorecard.UnitCount,
                scorecard.MeasuredTicks,
                scorecard.CommandCount,
                scorecard.CoreFactCount,
                scorecard.ActiveMutationCommandCount,
                scorecard.ActiveMutationOwnerGroupCount,
                scorecard.ActiveMutationMaxOwnerRange,
                scorecard.ActiveMutationEstimatedRandomLookupCount,
                scorecard.PendingAttributeDeltaCount,
                scorecard.PendingAttributeTargetGroupCount,
                scorecard.PendingAttributeMaxTargetRange,
                scorecard.PendingAttributeEstimatedRandomLookupCount,
                scorecard.OwnerLocalFactCount,
                scorecard.OwnerLocalFactOwnerGroupCount,
                scorecard.OwnerLocalFactMaxOwnerRange,
                scorecard.OwnerLocalFactFlushCount,
                scorecard.ActiveEffectSlotCount,
                scorecard.ActiveEffectSlotCapacity,
                scorecard.ActiveEffectChunkSkipDuePeriodSlotCount,
                scorecard.QueryBudget,
                scorecard.LookupUpdateBudget,
                scorecard.RandomLookupBudget,
                scorecard.SyncQueryBudget,
                scorecard.DependencyWaitRiskCount,
                scorecard.PerformanceObservationPollutionRiskCount,
                scorecard.MeasuredAverageTickMilliseconds,
                scorecard.GasTickAverageMilliseconds,
                scorecard.GasTickMaxMilliseconds,
                scorecard.CoreRuntimeAverageMilliseconds,
                scorecard.CoreSimulationAverageMilliseconds,
                scorecard.BoundaryAverageMilliseconds,
                scorecard.RunnerAverageMilliseconds,
                scorecard.CommandsPerMeasuredTick,
                scorecard.CoreFactsPerMeasuredTick,
                scorecard.MeasuredMicrosecondsPerUnit,
                scorecard.GasTickMicrosecondsPerUnit,
                scorecard.CoreSimulationMicrosecondsPerUnit,
                scorecard.GasTickMicrosecondsPerCommand,
                scorecard.CoreSimulationMicrosecondsPerCoreFact,
                scorecard.ProfilerCaptureState);
        }
    }

    internal readonly struct AutoChessRepeatRunEvidence
    {
        public readonly bool Passed;
        public readonly int FirstCommands;
        public readonly int SecondCommands;
        public readonly int FirstAttributeChanges;
        public readonly int SecondAttributeChanges;
        public readonly int FirstExecutionOutputs;
        public readonly int SecondExecutionOutputs;
        public readonly int FirstCueRequests;
        public readonly int SecondCueRequests;
        public readonly int FirstPeriodTickDamageFacts;
        public readonly int SecondPeriodTickDamageFacts;
        public readonly double FirstAverageTickMilliseconds;
        public readonly double SecondAverageTickMilliseconds;
        public readonly bool DeterministicCountsPassed;
        public readonly bool FirstRuntimeChainPassed;
        public readonly bool SecondRuntimeChainPassed;
        public readonly int FirstRuntimeEvents;
        public readonly int SecondRuntimeEvents;
        public readonly int FirstBlockingDiagnosticErrors;
        public readonly int SecondBlockingDiagnosticErrors;
        public readonly int FirstPendingAttributeAppliedDeltas;
        public readonly int SecondPendingAttributeAppliedDeltas;
        public readonly bool FirstBattleLogPassed;
        public readonly bool SecondBattleLogPassed;

        public AutoChessRepeatRunEvidence(
            bool passed,
            int firstCommands,
            int secondCommands,
            int firstAttributeChanges,
            int secondAttributeChanges,
            int firstExecutionOutputs,
            int secondExecutionOutputs,
            int firstCueRequests,
            int secondCueRequests,
            int firstPeriodTickDamageFacts,
            int secondPeriodTickDamageFacts,
            double firstAverageTickMilliseconds,
            double secondAverageTickMilliseconds,
            bool deterministicCountsPassed,
            bool firstRuntimeChainPassed,
            bool secondRuntimeChainPassed,
            int firstRuntimeEvents,
            int secondRuntimeEvents,
            int firstBlockingDiagnosticErrors,
            int secondBlockingDiagnosticErrors,
            int firstPendingAttributeAppliedDeltas,
            int secondPendingAttributeAppliedDeltas,
            bool firstBattleLogPassed,
            bool secondBattleLogPassed)
        {
            Passed = passed;
            FirstCommands = firstCommands;
            SecondCommands = secondCommands;
            FirstAttributeChanges = firstAttributeChanges;
            SecondAttributeChanges = secondAttributeChanges;
            FirstExecutionOutputs = firstExecutionOutputs;
            SecondExecutionOutputs = secondExecutionOutputs;
            FirstCueRequests = firstCueRequests;
            SecondCueRequests = secondCueRequests;
            FirstPeriodTickDamageFacts = firstPeriodTickDamageFacts;
            SecondPeriodTickDamageFacts = secondPeriodTickDamageFacts;
            FirstAverageTickMilliseconds = firstAverageTickMilliseconds;
            SecondAverageTickMilliseconds = secondAverageTickMilliseconds;
            DeterministicCountsPassed = deterministicCountsPassed;
            FirstRuntimeChainPassed = firstRuntimeChainPassed;
            SecondRuntimeChainPassed = secondRuntimeChainPassed;
            FirstRuntimeEvents = firstRuntimeEvents;
            SecondRuntimeEvents = secondRuntimeEvents;
            FirstBlockingDiagnosticErrors = firstBlockingDiagnosticErrors;
            SecondBlockingDiagnosticErrors = secondBlockingDiagnosticErrors;
            FirstPendingAttributeAppliedDeltas = firstPendingAttributeAppliedDeltas;
            SecondPendingAttributeAppliedDeltas = secondPendingAttributeAppliedDeltas;
            FirstBattleLogPassed = firstBattleLogPassed;
            SecondBattleLogPassed = secondBattleLogPassed;
        }
    }

    internal readonly struct AutoChessValidationRunResult
    {
        public readonly AutoChessBattleResult PerformanceResult;
        public readonly AutoChessBattleResult DiagnosticResult;
        public readonly AutoChessRepeatRunEvidence RepeatRunEvidence;
        public readonly bool HasRepeatRunEvidence;
        public readonly AutoChessPresentationSnapshot Presentation;
        public readonly AutoChessValidationEvidence Evidence;
        public readonly AutoChessHeadlessLogicBudgetResult HeadlessLogicBudget;
        public readonly bool GeneratedThresholdsPassed;
        public readonly bool RuntimeChainPassed;
        public readonly bool RequireOfficialToolDiff;

        public bool Passed =>
            GeneratedThresholdsPassed
            && HeadlessLogicBudget.Passed
            && RuntimeChainPassed
            && (!HasRepeatRunEvidence || RepeatRunEvidence.Passed);

        public AutoChessValidationRunResult(
            AutoChessBattleResult performanceResult,
            AutoChessBattleResult diagnosticResult,
            AutoChessRepeatRunEvidence repeatRunEvidence,
            bool hasRepeatRunEvidence,
            AutoChessPresentationSnapshot presentation,
            AutoChessValidationEvidence evidence,
            AutoChessHeadlessLogicBudgetResult headlessLogicBudget,
            bool generatedThresholdsPassed,
            bool runtimeChainPassed,
            bool requireOfficialToolDiff)
        {
            PerformanceResult = performanceResult;
            DiagnosticResult = diagnosticResult;
            RepeatRunEvidence = repeatRunEvidence;
            HasRepeatRunEvidence = hasRepeatRunEvidence;
            Presentation = presentation;
            Evidence = evidence;
            HeadlessLogicBudget = headlessLogicBudget;
            GeneratedThresholdsPassed = generatedThresholdsPassed;
            RuntimeChainPassed = runtimeChainPassed;
            RequireOfficialToolDiff = requireOfficialToolDiff;
        }
    }

    internal static class AutoChessBattleValidationRun
    {
        public static AutoChessValidationRunResult RunHeadlessValidation(
            AutoChessGeneratedScenarioProfile scenario,
            IAutoChessPresentationOutboxBridge presentationOutboxBridge,
            int maxPresentationLines)
        {
            RunProcessWarmupBattles(scenario);
            var repeatRunEvidence = RunRepeatRunCleanupProbe(scenario);

            var performanceResult = RunGeneratedScenario(
                scenario,
                captureOfficialToolDiff: false,
                debuggerEnabled: false,
                captureSystemTimings: false,
                captureBufferPressure: false);
            AutoChessBattleManager.ShutdownRuntime();

            var diagnosticResult = RunGeneratedScenario(
                scenario,
                captureOfficialToolDiff: false,
                debuggerEnabled: true,
                captureSystemTimings: true,
                captureBufferPressure: true);
            ValidateOfficialDiffRun(performanceResult, diagnosticResult);
            AutoChessBattleManager.ShutdownRuntime();

            var officialDiffResult = RunGeneratedScenario(
                scenario,
                captureOfficialToolDiff: true,
                debuggerEnabled: false,
                captureSystemTimings: false,
                captureBufferPressure: false);
            ValidateOfficialDiffRun(performanceResult, officialDiffResult);
            performanceResult = performanceResult.WithOfficialToolDiff(officialDiffResult.OfficialToolDiff);

            return CreateRunResult(
                scenario,
                performanceResult,
                diagnosticResult,
                repeatRunEvidence,
                true,
                true,
                presentationOutboxBridge,
                maxPresentationLines);
        }

        public static void RunProcessWarmupBattles(AutoChessGeneratedScenarioProfile scenario)
        {
            for (var i = 0; i < scenario.ProcessWarmupRuns; i++)
            {
                RunWarmupPass(CreateGeneratedScenarioOptions(
                    scenario,
                    captureOfficialToolDiff: false,
                    debuggerEnabled: false,
                    captureSystemTimings: false,
                    captureBufferPressure: false));
            }
        }

        public static void RunWarmupPass(AutoChessBattleOptions options)
        {
            try
            {
                AutoChessBattleManager.RunDefault(options);
            }
            finally
            {
                AutoChessBattleManager.ShutdownRuntime();
            }
        }

        public static AutoChessRepeatRunEvidence RunRepeatRunCleanupProbe(
            AutoChessGeneratedScenarioProfile scenario)
        {
            try
            {
                var first = RunGeneratedScenario(
                    scenario,
                    captureOfficialToolDiff: false);
                var second = RunGeneratedScenario(
                    scenario,
                    captureOfficialToolDiff: false);
                var firstRuntimeChainPassed = HasRequiredRuntimeChain(
                    first,
                    first,
                    requireOfficialToolDiff: false);
                var secondRuntimeChainPassed = HasRequiredRuntimeChain(
                    second,
                    second,
                    requireOfficialToolDiff: false);
                var deterministicCountsPassed =
                    first.DriverIssuedCommands == second.DriverIssuedCommands
                    && first.EventCounts.AttributeChanges == second.EventCounts.AttributeChanges
                    && first.EventCounts.ExecutionCalculationOutputUpdated
                    == second.EventCounts.ExecutionCalculationOutputUpdated
                    && first.EventCounts.CueRequests == second.EventCounts.CueRequests
                    && first.EventCounts.PeriodTickDamageFacts
                    == second.EventCounts.PeriodTickDamageFacts;
                var passed = firstRuntimeChainPassed
                             && secondRuntimeChainPassed
                             && deterministicCountsPassed;

                var evidence = new AutoChessRepeatRunEvidence(
                    passed,
                    first.DriverIssuedCommands,
                    second.DriverIssuedCommands,
                    first.EventCounts.AttributeChanges,
                    second.EventCounts.AttributeChanges,
                    first.EventCounts.ExecutionCalculationOutputUpdated,
                    second.EventCounts.ExecutionCalculationOutputUpdated,
                    first.EventCounts.CueRequests,
                    second.EventCounts.CueRequests,
                    first.EventCounts.PeriodTickDamageFacts,
                    second.EventCounts.PeriodTickDamageFacts,
                    first.AverageTickMilliseconds,
                    second.AverageTickMilliseconds,
                    deterministicCountsPassed,
                    firstRuntimeChainPassed,
                    secondRuntimeChainPassed,
                    first.RuntimeDiagnostics.EventCount,
                    second.RuntimeDiagnostics.EventCount,
                    CountBlockingDiagnosticErrors(first.RuntimeDiagnostics),
                    CountBlockingDiagnosticErrors(second.RuntimeDiagnostics),
                    first.RuntimeDiagnostics.CoreCounters.PendingAttributeAppliedDeltaCount,
                    second.RuntimeDiagnostics.CoreCounters.PendingAttributeAppliedDeltaCount,
                    HasRequiredBattleLog(first.BattleLog),
                    HasRequiredBattleLog(second.BattleLog));

                return evidence;
            }
            finally
            {
                AutoChessBattleManager.ShutdownRuntime();
            }
        }

        public static AutoChessBattleResult RunGeneratedScenario(
            AutoChessGeneratedScenarioProfile scenario,
            bool captureOfficialToolDiff,
            bool debuggerEnabled = true,
            bool captureSystemTimings = true,
            bool captureBufferPressure = true)
        {
            return AutoChessBattleManager.RunDefault(CreateGeneratedScenarioOptions(
                scenario,
                captureOfficialToolDiff,
                debuggerEnabled,
                captureSystemTimings,
                captureBufferPressure));
        }

        public static IEnumerator RunScenarioStepped(
            AutoChessBattleOptions options,
            AutoChessBattleProfileHooks profileHooks,
            Action<AutoChessBattleResult> completed)
        {
            return AutoChessBattleManager.RunDefaultStepped(
                options,
                profileHooks,
                completed);
        }

        public static AutoChessBattleResult RunDiagnosticPass(
            in AutoChessBattleResult performanceResult,
            AutoChessBattleOptions replayOptions)
        {
            AutoChessBattleManager.ShutdownRuntime();
            var diagnosticResult = AutoChessBattleManager.RunDefault(
                CreateReplayOptions(
                    in performanceResult,
                    replayOptions,
                    captureOfficialToolDiff: false,
                    debuggerEnabled: true,
                    captureSystemTimings: true,
                    captureBufferPressure: true));
            ValidateOfficialDiffRun(performanceResult, diagnosticResult);
            return diagnosticResult;
        }

        public static AutoChessBattleResult RunOfficialDiffPass(
            in AutoChessBattleResult performanceResult,
            AutoChessBattleOptions replayOptions)
        {
            AutoChessBattleManager.ShutdownRuntime();
            var officialDiffResult = AutoChessBattleManager.RunDefault(
                CreateReplayOptions(
                    in performanceResult,
                    replayOptions,
                    captureOfficialToolDiff: true,
                    debuggerEnabled: false,
                    captureSystemTimings: false,
                    captureBufferPressure: false));
            ValidateOfficialDiffRun(performanceResult, officialDiffResult);
            return performanceResult.WithOfficialToolDiff(officialDiffResult.OfficialToolDiff);
        }

        public static AutoChessValidationRunResult CreateRunResult(
            AutoChessGeneratedScenarioProfile scenario,
            in AutoChessBattleResult performanceResult,
            in AutoChessBattleResult diagnosticResult,
            in AutoChessRepeatRunEvidence repeatRunEvidence,
            bool hasRepeatRunEvidence,
            bool requireOfficialToolDiff,
            IAutoChessPresentationOutboxBridge presentationOutboxBridge,
            int maxPresentationLines)
        {
            var presentation = CreatePresentationSnapshot(
                performanceResult,
                presentationOutboxBridge,
                maxPresentationLines);
            var evidence = AutoChessBattleValidationReport.CreateEvidence(
                scenario,
                performanceResult,
                diagnosticResult,
                presentation,
                officialDiffSeparatePass: requireOfficialToolDiff);
            var headlessLogicBudget =
                AutoChessHeadlessLogicBudgetResult.Evaluate(performanceResult);
            return new AutoChessValidationRunResult(
                performanceResult,
                diagnosticResult,
                repeatRunEvidence,
                hasRepeatRunEvidence,
                presentation,
                evidence,
                headlessLogicBudget,
                HasGeneratedScenarioThresholds(performanceResult, diagnosticResult, scenario),
                HasRequiredRuntimeChain(
                    performanceResult,
                    diagnosticResult,
                    requireOfficialToolDiff),
                requireOfficialToolDiff);
        }

        public static AutoChessPresentationSnapshot CreatePresentationSnapshot(
            in AutoChessBattleResult result,
            IAutoChessPresentationOutboxBridge presentationOutboxBridge,
            int maxPresentationLines)
        {
            presentationOutboxBridge ??= AutoChessLogPresentationOutboxBridge.Instance;
            return presentationOutboxBridge.CreateSnapshot(
                AutoChessBattlePresentationSource.FromResult(result),
                maxPresentationLines);
        }

        public static void ValidateOfficialDiffRun(
            in AutoChessBattleResult performanceResult,
            in AutoChessBattleResult officialDiffResult)
        {
            if (performanceResult.DriverIssuedCommands != officialDiffResult.DriverIssuedCommands
                || performanceResult.EventCounts.AttributeChanges != officialDiffResult.EventCounts.AttributeChanges
                || performanceResult.EventCounts.ExecutionCalculationOutputUpdated
                != officialDiffResult.EventCounts.ExecutionCalculationOutputUpdated
                || performanceResult.EventCounts.CueRequests != officialDiffResult.EventCounts.CueRequests
                || performanceResult.EventCounts.PeriodTickDamageFacts
                != officialDiffResult.EventCounts.PeriodTickDamageFacts)
            {
                throw new InvalidOperationException(
                    "AutoChessBattle validation pass diverged from performance pass: "
                    + "performanceCommands="
                    + performanceResult.DriverIssuedCommands
                    + ", validationCommands="
                    + officialDiffResult.DriverIssuedCommands
                    + ", performanceAttributeChanges="
                    + performanceResult.EventCounts.AttributeChanges
                    + ", validationAttributeChanges="
                    + officialDiffResult.EventCounts.AttributeChanges
                    + ", performanceExecutionOutputs="
                    + performanceResult.EventCounts.ExecutionCalculationOutputUpdated
                    + ", validationExecutionOutputs="
                    + officialDiffResult.EventCounts.ExecutionCalculationOutputUpdated
                    + ", performanceCueRequests="
                    + performanceResult.EventCounts.CueRequests
                    + ", validationCueRequests="
                    + officialDiffResult.EventCounts.CueRequests
                    + ", performancePeriodTickDamageFacts="
                    + performanceResult.EventCounts.PeriodTickDamageFacts
                    + ", validationPeriodTickDamageFacts="
                    + officialDiffResult.EventCounts.PeriodTickDamageFacts);
            }
        }

        public static bool HasGeneratedScenarioThresholds(
            in AutoChessBattleResult result,
            AutoChessGeneratedScenarioProfile scenario)
        {
            return HasGeneratedScenarioThresholds(result, result, scenario);
        }

        public static bool HasGeneratedScenarioThresholds(
            in AutoChessBattleResult result,
            in AutoChessBattleResult diagnosticResult,
            AutoChessGeneratedScenarioProfile scenario)
        {
            var counters = diagnosticResult.RuntimeDiagnostics.CoreCounters;
            return result.Completed
                   && result.Winner == scenario.ExpectedWinner
                   && result.DriverIssuedCommands >= scenario.MinDriverIssuedCommands
                   && result.EventCounts.AttributeChanges >= scenario.MinAttributeChanges
                   && result.EventCounts.ExecutionCalculationOutputUpdated >= scenario.MinExecutionOutputs
                   && result.EventCounts.CueRequests >= scenario.MinCueRequests
                   && counters.ActiveEffectSlotCount >= scenario.MinActiveEffectSlots
                   && result.EventCounts.PeriodTickDamageFacts >= scenario.MinPeriodTickDamageFacts
                   && counters.ActiveMutationCommandCount >= scenario.MinActiveMutationCommands
                   && counters.ActiveMutationOwnerGroupCount >= scenario.MinActiveMutationOwnerGroups
                   && counters.ActiveMutationEstimatedRandomLookupCount <= scenario.MaxActiveMutationEstimatedRandomLookups
                   && counters.ActiveMutationOwnerResourceLookupCount <= scenario.MaxActiveMutationOwnerResourceLookups
                   && counters.ActiveMutationMigrationCarrierCount <= scenario.MaxActiveMutationMigrationCarriers;
        }

        public static bool HasRequiredRuntimeChain(
            in AutoChessBattleResult result,
            in AutoChessBattleResult diagnosticResult,
            bool requireOfficialToolDiff)
        {
            var counters = diagnosticResult.RuntimeDiagnostics.CoreCounters;
            return result.Completed
                   && result.DriverIssuedCommands > 0
                   && result.EventCounts.AttributeChanges > 0
                   && result.EventCounts.ExecutionCalculationOutputUpdated > 0
                   && result.EventCounts.PeriodTickDamageFacts > 0
                   && result.EventCounts.CueRequests > 0
                   && diagnosticResult.RuntimeDiagnostics.EventCount > 0
                    && counters.SpecCount > 0
                    && counters.FactCount > 0
                    && counters.PendingAttributeAppliedDeltaCount > 0
                    && counters.OwnerLocalFactFlushCount > 0
                    && HasRequiredBattleLog(result.BattleLog)
                    && AutoChessBattleValidationReport.HasBoundaryReportKeyCoverage(
                        result.StructuredLogSnapshot)
                    && !HasBlockingDiagnosticErrors(diagnosticResult.RuntimeDiagnostics)
                    && (!requireOfficialToolDiff || result.OfficialToolDiff.JournalingCaptured);
        }

        public static bool HasBlockingDiagnosticErrors(GAS.Runtime.GasRuntimeDiagnosticSnapshot diagnostics)
        {
            return CountBlockingDiagnosticErrors(diagnostics) > 0;
        }

        public static int CountBlockingDiagnosticErrors(GAS.Runtime.GasRuntimeDiagnosticSnapshot diagnostics)
        {
            var events = diagnostics.Events ?? Array.Empty<GAS.Runtime.GASRuntimeDiagnosticEventBuffer>();
            var count = 0;
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Severity < GAS.Runtime.EGasRuntimeDiagnosticSeverity.Error)
                    continue;

                if (evt.Kind == GAS.Runtime.EGasRuntimeDiagnosticKind.SystemTiming
                    || evt.Kind == GAS.Runtime.EGasRuntimeDiagnosticKind.TickSummary)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        public static string CreateRepeatRunEvidenceSummary(
            in AutoChessRepeatRunEvidence evidence)
        {
            return $"passed={evidence.Passed}, "
                   + $"firstCommands={evidence.FirstCommands}, "
                   + $"secondCommands={evidence.SecondCommands}, "
                   + $"firstAttributeChanges={evidence.FirstAttributeChanges}, "
                   + $"secondAttributeChanges={evidence.SecondAttributeChanges}, "
                   + $"firstExecutionOutputs={evidence.FirstExecutionOutputs}, "
                   + $"secondExecutionOutputs={evidence.SecondExecutionOutputs}, "
                    + $"firstCueRequests={evidence.FirstCueRequests}, "
                    + $"secondCueRequests={evidence.SecondCueRequests}, "
                    + $"firstPeriodTickDamageFacts={evidence.FirstPeriodTickDamageFacts}, "
                    + $"secondPeriodTickDamageFacts={evidence.SecondPeriodTickDamageFacts}, "
                    + $"firstAvgTickMs={evidence.FirstAverageTickMilliseconds:0.000}, "
                   + $"secondAvgTickMs={evidence.SecondAverageTickMilliseconds:0.000}, "
                   + $"deterministicCountsPassed={evidence.DeterministicCountsPassed}, "
                   + $"firstRuntimeChainPassed={evidence.FirstRuntimeChainPassed}, "
                   + $"secondRuntimeChainPassed={evidence.SecondRuntimeChainPassed}, "
                   + $"firstRuntimeEvents={evidence.FirstRuntimeEvents}, "
                   + $"secondRuntimeEvents={evidence.SecondRuntimeEvents}, "
                   + $"firstBlockingErrors={evidence.FirstBlockingDiagnosticErrors}, "
                   + $"secondBlockingErrors={evidence.SecondBlockingDiagnosticErrors}, "
                   + $"firstPendingAttributeAppliedDeltas={evidence.FirstPendingAttributeAppliedDeltas}, "
                   + $"secondPendingAttributeAppliedDeltas={evidence.SecondPendingAttributeAppliedDeltas}, "
                   + $"firstBattleLogPassed={evidence.FirstBattleLogPassed}, "
                   + $"secondBattleLogPassed={evidence.SecondBattleLogPassed}";
        }

        private static AutoChessBattleOptions CreateGeneratedScenarioOptions(
            AutoChessGeneratedScenarioProfile scenario,
            bool captureOfficialToolDiff,
            bool debuggerEnabled = true,
            bool captureSystemTimings = true,
            bool captureBufferPressure = true)
        {
            return new AutoChessBattleOptions(
                scenario.MaxTicks,
                scenario.PostVictoryFlushTicks,
                scenario.Scale,
                captureOfficialToolDiff,
                debuggerEnabled,
                captureSystemTimings,
                captureBufferPressure,
                healthMultiplier: scenario.HealthMultiplier);
        }

        private static AutoChessBattleOptions CreateReplayOptions(
            in AutoChessBattleResult performanceResult,
            AutoChessBattleOptions replayOptions,
            bool captureOfficialToolDiff,
            bool debuggerEnabled,
            bool captureSystemTimings,
            bool captureBufferPressure)
        {
            replayOptions = replayOptions.Normalize();
            return new AutoChessBattleOptions(
                Math.Max(1, performanceResult.BattleTicks),
                replayOptions.PostVictoryFlushTicks,
                replayOptions.Scale,
                captureOfficialToolDiff,
                debuggerEnabled,
                captureSystemTimings,
                captureBufferPressure,
                replayOptions.HealthMultiplier,
                replayOptions.MinimumBattleSeconds);
        }

        private static bool HasRequiredBattleLog(in AutoChessBattleLogSnapshot battleLog)
        {
            return HasBattleLogLine(battleLog, "游戏开始")
                   && HasBattleLogLine(battleLog, "发动了")
                   && HasBattleLogLine(battleLog, "受到致命伤害，死亡")
                   && HasBattleLogLine(battleLog, "战斗结束");
        }

        private static bool HasBattleLogLine(
            in AutoChessBattleLogSnapshot battleLog,
            string messagePart)
        {
            var lines = battleLog.Lines ?? Array.Empty<AutoChessBattleLogLine>();
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Message.IndexOf(messagePart, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }
    }
}
