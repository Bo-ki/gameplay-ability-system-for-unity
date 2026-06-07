using System;
using System.Collections;

namespace GAS.AutoChessDemo
{
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
        public readonly bool GeneratedThresholdsPassed;
        public readonly bool RuntimeChainPassed;
        public readonly bool RequireOfficialToolDiff;

        public bool Passed =>
            GeneratedThresholdsPassed
            && RuntimeChainPassed
            && (!HasRepeatRunEvidence || RepeatRunEvidence.Passed);

        public AutoChessValidationRunResult(
            AutoChessBattleResult performanceResult,
            AutoChessBattleResult diagnosticResult,
            AutoChessRepeatRunEvidence repeatRunEvidence,
            bool hasRepeatRunEvidence,
            AutoChessPresentationSnapshot presentation,
            AutoChessValidationEvidence evidence,
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

            var result = RunGeneratedScenario(
                scenario,
                captureOfficialToolDiff: false);
            AutoChessBattleManager.ShutdownRuntime();

            var officialDiffResult = RunGeneratedScenario(
                scenario,
                captureOfficialToolDiff: true);
            ValidateOfficialDiffRun(result, officialDiffResult);
            result = result.WithOfficialToolDiff(officialDiffResult.OfficialToolDiff);

            return CreateRunResult(
                scenario,
                result,
                result,
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
                    captureOfficialToolDiff: false));
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
            bool captureOfficialToolDiff)
        {
            return AutoChessBattleManager.RunDefault(CreateGeneratedScenarioOptions(
                scenario,
                captureOfficialToolDiff));
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
                presentation,
                officialDiffSeparatePass: requireOfficialToolDiff);
            return new AutoChessValidationRunResult(
                performanceResult,
                diagnosticResult,
                repeatRunEvidence,
                hasRepeatRunEvidence,
                presentation,
                evidence,
                HasGeneratedScenarioThresholds(performanceResult, scenario),
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
            return result.Completed
                   && result.Winner == scenario.ExpectedWinner
                   && result.DriverIssuedCommands >= scenario.MinDriverIssuedCommands
                   && result.EventCounts.AttributeChanges >= scenario.MinAttributeChanges
                   && result.EventCounts.ExecutionCalculationOutputUpdated >= scenario.MinExecutionOutputs
                   && result.EventCounts.CueRequests >= scenario.MinCueRequests
                   && result.RuntimeDiagnostics.CoreCounters.ActiveEffectSlotCount >= scenario.MinActiveEffectSlots
                   && result.EventCounts.PeriodTickDamageFacts >= scenario.MinPeriodTickDamageFacts
                   && result.RuntimeDiagnostics.CoreCounters.ActiveMutationCommandCount >= scenario.MinActiveMutationCommands
                   && result.RuntimeDiagnostics.CoreCounters.ActiveMutationOwnerGroupCount >= scenario.MinActiveMutationOwnerGroups
                   && result.RuntimeDiagnostics.CoreCounters.ActiveMutationEstimatedRandomLookupCount <= scenario.MaxActiveMutationEstimatedRandomLookups
                   && result.RuntimeDiagnostics.CoreCounters.ActiveMutationOwnerResourceLookupCount <= scenario.MaxActiveMutationOwnerResourceLookups
                   && result.RuntimeDiagnostics.CoreCounters.ActiveMutationMigrationCarrierCount <= scenario.MaxActiveMutationMigrationCarriers;
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
                   && counters.RequestCount > 0
                   && counters.FactCount > 0
                   && counters.PendingAttributeAppliedDeltaCount > 0
                   && HasRequiredBattleLog(result.BattleLog)
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
            bool captureOfficialToolDiff)
        {
            return new AutoChessBattleOptions(
                scenario.MaxTicks,
                scenario.PostVictoryFlushTicks,
                scenario.Scale,
                captureOfficialToolDiff,
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
