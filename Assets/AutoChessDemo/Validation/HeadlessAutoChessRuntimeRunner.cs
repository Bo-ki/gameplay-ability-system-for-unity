using System;
using System.Text;
using UnityEngine;
using GAS.Runtime;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GAS.AutoChessDemo
{
    public static class HeadlessAutoChessRuntimeRunner
    {
        private const string RunArgument = "-gasAutoChessHeadless";
        private const int ProcessWarmupRuns = 1;
        private const int ValidationScale = 50;
        private const int ValidationMaxTicks = 96;
        private const int ValidationPostVictoryFlushTicks = 4;

        public static void RunHeadlessAutoBattleOnce()
        {
            var result = RunAutoBattleOnce();
            if (!result.Completed
                || result.DriverIssuedCommands <= 0
                || result.EventCounts.AttributeChanges <= 0
                || result.EventCounts.ExecutionCalculationOutputUpdated <= 0
                || result.EventCounts.CueRequests <= 0
                || result.RuntimeDiagnostics.EventCount <= 0
                || HasBlockingDiagnosticErrors(result.RuntimeDiagnostics)
                || !result.OfficialToolDiff.JournalingCaptured)
            {
                throw new InvalidOperationException(
                    "AutoBattle minimal Runtime Core validation failed: "
                    + CreateSummary(result));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunOnPlayerLaunch()
        {
            if (!HasArgument(RunArgument))
                return;

            RunAutoBattleOnce();
        }

        private static HeadlessAutoBattleResult RunAutoBattleOnce()
        {
            try
            {
                RunProcessWarmupBattles();

                var result = RunScenario(captureOfficialToolDiff: false);
                HeadlessAutoBattleScenario.ShutdownRuntime();

                var officialDiffResult = RunScenario(captureOfficialToolDiff: true);
                ValidateOfficialDiffRun(result, officialDiffResult);
                result = result.WithOfficialToolDiff(officialDiffResult.OfficialToolDiff);

                Debug.Log("HeadlessAutoChessRuntimeRunner: " + CreateSummary(result));
                Debug.Log("HeadlessAutoChessRuntimeDebugger: " + CreateDebuggerSummary(result));
                Debug.Log("HeadlessAutoChessRuntimeTiming: " + CreateTimingSummary(result));
                Debug.Log("HeadlessAutoChessOfficialToolDiff: " + CreateOfficialToolDiffSummary(result));
                Debug.Log("HeadlessAutoChessRuntimeDataFlow:\n" + CreateDataFlowDiagram(result));
                Debug.Log("HeadlessAutoChessRuntimeSequence:\n" + CreateSequenceDiagram(result));
                Debug.Log("HeadlessAutoChessRuntimeDiagnostics:\n" + result.RuntimeDiagnosticsLog);
                return result;
            }
            finally
            {
                HeadlessAutoBattleScenario.ShutdownRuntime();
            }
        }

        internal static string CreateSummary(HeadlessAutoBattleResult result)
        {
            var factsHash = CalculateFactsHash(result.StructuredLogSnapshot);
            var summaryHash = CalculateSummaryHash(result, factsHash);
            return $"completed={result.Completed}, "
                   + $"winner={result.Winner}, "
                   + $"scale={result.ScenarioScale}, "
                   + $"units={result.Units.Length}, "
                   + $"battleTicks={result.BattleTicks}, "
                   + $"totalTicks={result.TotalTicks}, "
                   + $"warmupDroppedTicks={result.WarmupDroppedTicks}, "
                   + $"measuredTicks={result.MeasuredTicks}, "
                   + $"commands={result.DriverIssuedCommands}, "
                   + $"finishers={result.DriverIssuedFinisherCommands}, "
                   + $"attributeChanges={result.EventCounts.AttributeChanges}, "
                   + $"executionOutputs={result.EventCounts.ExecutionCalculationOutputUpdated}, "
                   + $"cueRequests={result.EventCounts.CueRequests}, "
                   + $"debugEvents={result.RuntimeDiagnostics.EventCount}, "
                   + $"debugWarnings={result.RuntimeDiagnostics.Stats.WarningCount}, "
                   + $"debugErrors={result.RuntimeDiagnostics.Stats.ErrorCount}, "
                   + $"blockingDebugErrors={CountBlockingDiagnosticErrors(result.RuntimeDiagnostics)}, "
                   + $"coreRequests={result.RuntimeDiagnostics.CoreCounters.RequestCount}, "
                   + $"coreFacts={result.RuntimeDiagnostics.CoreCounters.FactCount}, "
                   + $"coreDeltas={result.RuntimeDiagnostics.CoreCounters.DeltaCount}, "
                   + $"coreCues={result.RuntimeDiagnostics.CoreCounters.CueCount}, "
                   + $"peakEventBus={result.RuntimeDiagnostics.CoreCounters.PeakEventBusBufferLength}, "
                   + $"replayLag={result.RuntimeDiagnostics.CoreCounters.PeakReplayCursorLag}, "
                   + $"journalingRecords={result.OfficialToolDiff.JournalingWorldRecordCount}, "
                   + $"processWarmupRuns={ProcessWarmupRuns}, "
                   + $"totalElapsedMs={result.ElapsedMilliseconds:0.000}, "
                   + $"factsHash=0x{factsHash:X8}, "
                   + $"summaryHash=0x{summaryHash:X8}, "
                   + $"avgTickMs={result.AverageTickMilliseconds:0.000}";
        }

        internal static string CreateDebuggerSummary(HeadlessAutoBattleResult result)
        {
            var stats = result.RuntimeDiagnostics.Stats;
            var counters = result.RuntimeDiagnostics.CoreCounters;
            var backbone = result.RuntimeDiagnostics.FrameBackboneCounters;
            return $"runtimeDiagnostics events={stats.RetainedEventCount}, "
                   + $"dropped={stats.DroppedEventCount}, "
                   + $"warnings={stats.WarningCount}, "
                   + $"errors={stats.ErrorCount}, "
                   + $"blockingErrors={CountBlockingDiagnosticErrors(result.RuntimeDiagnostics)}, "
                   + $"requests={counters.RequestCount}, "
                   + $"specs={counters.SpecCount}, "
                   + $"deltas={counters.DeltaCount}, "
                   + $"facts={counters.FactCount}, "
                   + $"cues={counters.CueCount}, "
                   + $"presentation={counters.PresentationCount}, "
                   + $"activeEffectOwners={counters.ActiveEffectStoreOwnerCount}, "
                   + $"activeEffectSlots={counters.ActiveEffectSlotCount}, "
                   + $"queryBudget={counters.QueryBudget}, "
                   + $"lookupBudget={counters.LookupUpdateBudget}, "
                   + $"randomLookupBudget={counters.RandomLookupBudget}, "
                   + $"syncQueryBudget={counters.SyncQueryBudget}, "
                   + $"frameBackbonePhases={backbone.PhaseCount}, "
                   + $"streams={backbone.StreamCount}, "
                   + $"migrationCarriers={backbone.MigrationCarrierCount}, "
                   + $"profilerMarkerContracts={backbone.ProfilerMarkerCount}, "
                   + $"journalingMarkerContracts={backbone.JournalingMarkerCount}";
        }

        internal static string CreateTimingSummary(HeadlessAutoBattleResult result)
        {
            var builder = new StringBuilder(512);
            builder.Append("ecsRuntimeTickOnly=true");
            AppendTiming(builder, "GASTickTotal", result.RuntimeTiming.TickTotal);
            AppendTiming(builder, nameof(GASFramePrepareSystemGroup), result.RuntimeTiming.FramePrepare);
            AppendTiming(builder, nameof(GASCommandResolveSystemGroup), result.RuntimeTiming.CommandResolve);
            AppendTiming(builder, nameof(GASCoreSimulationSystemGroup), result.RuntimeTiming.CoreSimulation);
            AppendTiming(builder, nameof(GASStructuralCommitSystemGroup), result.RuntimeTiming.StructuralCommit);
            AppendTiming(builder, nameof(GASBoundaryProjectionSystemGroup), result.RuntimeTiming.BoundaryProjection);
            return builder.ToString();
        }

        private static void RunProcessWarmupBattles()
        {
            for (var i = 0; i < ProcessWarmupRuns; i++)
            {
                try
                {
                    HeadlessAutoBattleScenario.RunDefault(new HeadlessAutoBattleOptions(
                        ValidationMaxTicks,
                        ValidationPostVictoryFlushTicks,
                        ValidationScale,
                        captureOfficialToolDiff: false));
                }
                finally
                {
                    HeadlessAutoBattleScenario.ShutdownRuntime();
                }
            }
        }

        private static HeadlessAutoBattleResult RunScenario(bool captureOfficialToolDiff)
        {
            return HeadlessAutoBattleScenario.RunDefault(new HeadlessAutoBattleOptions(
                ValidationMaxTicks,
                ValidationPostVictoryFlushTicks,
                ValidationScale,
                captureOfficialToolDiff));
        }

        internal static void ValidateOfficialDiffRun(
            in HeadlessAutoBattleResult performanceResult,
            in HeadlessAutoBattleResult officialDiffResult)
        {
            if (performanceResult.DriverIssuedCommands != officialDiffResult.DriverIssuedCommands
                || performanceResult.EventCounts.AttributeChanges != officialDiffResult.EventCounts.AttributeChanges
                || performanceResult.EventCounts.ExecutionCalculationOutputUpdated != officialDiffResult.EventCounts.ExecutionCalculationOutputUpdated
                || performanceResult.EventCounts.CueRequests != officialDiffResult.EventCounts.CueRequests)
            {
                throw new InvalidOperationException(
                    "AutoBattle official diff pass diverged from performance pass: "
                    + CreateSummary(performanceResult)
                    + " | official="
                    + CreateSummary(officialDiffResult));
            }
        }

        private static void AppendTiming(
            StringBuilder builder,
            string systemName,
            in HeadlessAutoBattleSystemTiming timing)
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

        internal static string CreateOfficialToolDiffSummary(HeadlessAutoBattleResult result)
        {
            var official = result.OfficialToolDiff;
            var runtime = result.RuntimeDiagnostics.CoreCounters;
            var runtimeStructural = runtime.EntityCreateCount + runtime.EntityDestroyCount;
            var officialStructural = official.JournalingStructuralRecordCount;
            return $"runtimeSelfDiagnostics=events:{result.RuntimeDiagnostics.EventCount}, "
                   + "officialDiffSeparatePass=True, "
                   + $"journalingAvailable={official.JournalingAvailable}, "
                   + $"journalingCaptured={official.JournalingCaptured}, "
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
                   + $"profilerAvailable={official.ProfilerAvailable}, "
                   + $"profilerEnabled={official.ProfilerEnabled}, "
                   + $"structuralProfilerCategoryEnabled={official.StructuralChangesProfilerCategoryEnabled}, "
                   + $"memoryProfilerCategoryEnabled={official.MemoryProfilerCategoryEnabled}, "
                   + $"profilerCaptureState={official.ProfilerCaptureState}, "
                   + $"journalingRecordTopN={official.JournalingRecordTopN}, "
                   + $"journalingSystemTopN={official.JournalingSystemTopN}, "
                   + $"journalingComponentTopN={official.JournalingComponentTopN}";
        }

        internal static bool HasBlockingDiagnosticErrors(GasRuntimeDiagnosticSnapshot diagnostics)
        {
            return CountBlockingDiagnosticErrors(diagnostics) > 0;
        }

        private static int CountBlockingDiagnosticErrors(GasRuntimeDiagnosticSnapshot diagnostics)
        {
            var events = diagnostics.Events ?? Array.Empty<GASRuntimeDiagnosticEventBuffer>();
            var count = 0;
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Severity < EGasRuntimeDiagnosticSeverity.Error)
                    continue;

                if (evt.Kind == EGasRuntimeDiagnosticKind.SystemTiming
                    || evt.Kind == EGasRuntimeDiagnosticKind.TickSummary)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        internal static string CreateDataFlowDiagram(HeadlessAutoBattleResult result)
        {
            return "```mermaid\n"
                   + "flowchart LR\n"
                   + $"    CommandDrive[\"AutoBattleCommandDriveSystem\\nscale: {result.ScenarioScale}, units: {result.Units.Length}\\ncommands: {result.DriverIssuedCommands}\"] --> AbilityBuffer[\"AbilityCommandBuffer\\nrequest entities avoided\"]\n"
                   + $"    AbilityBuffer --> RuntimeCore[\"GAS Runtime Core\\nrequests: {result.RuntimeDiagnostics.CoreCounters.RequestCount}\"]\n"
                   + $"    RuntimeCore --> GEStream[\"GEEffectCommandBuffer / Spec / Delta\\ndeltas: {result.RuntimeDiagnostics.CoreCounters.DeltaCount}\"]\n"
                   + $"    GEStream --> Execution[\"AutoBattleExecuteDamageCalculationSystem\\nexecution outputs: {result.EventCounts.ExecutionCalculationOutputUpdated}\"]\n"
                   + $"    Execution --> Attribute[\"AttributeModifierBuffer + AttributeValueBuffer\\nattribute changes: {result.EventCounts.AttributeChanges}\"]\n"
                   + $"    Attribute --> Facts[\"GameplayEventBuffer typed facts\\nfacts: {result.RuntimeDiagnostics.CoreCounters.FactCount}\"]\n"
                   + $"    Facts --> Projection[\"Replay / Presentation / Layer 2 Diagnostics\\nreplay events: {result.EventCounts.ReplayEvents}\"]\n"
                   + $"    Projection --> OfficialDiff[\"Official tool diff\\nseparate pass, journaling records: {result.OfficialToolDiff.JournalingWorldRecordCount}\"]\n"
                   + "```";
        }

        internal static string CreateSequenceDiagram(HeadlessAutoBattleResult result)
        {
            return "```mermaid\n"
                   + "sequenceDiagram\n"
                   + "    participant Runner as Headless Runner\n"
                   + "    participant Drive as AutoBattle Command Drive\n"
                   + "    participant Core as GAS Runtime Core\n"
                   + "    participant Exec as Execution Calculation\n"
                   + "    participant Obs as Replay / Projection\n"
                   + "    participant Debug as Layer 2 DiagnosticsSink\n"
                   + "    participant Unity as Unity Journaling / Profiler\n"
                   + $"    Runner->>Drive: fixed ticks {result.TotalTicks}, battle ticks {result.BattleTicks}, scale {result.ScenarioScale}\n"
                   + $"    Drive->>Core: AbilityCommandBuffer commands {result.DriverIssuedCommands}\n"
                   + $"    Core->>Exec: execute GE commands, finishers {result.DriverIssuedFinisherCommands}\n"
                   + $"    Exec->>Core: modifier + typed fact outputs {result.EventCounts.ExecutionCalculationOutputUpdated}\n"
                   + $"    Core->>Obs: attribute changes {result.EventCounts.AttributeChanges}, cue requests {result.EventCounts.CueRequests}\n"
                   + $"    Obs->>Debug: counters {result.RuntimeDiagnostics.EventCount}, warnings {result.RuntimeDiagnostics.Stats.WarningCount}, errors {result.RuntimeDiagnostics.Stats.ErrorCount}\n"
                   + $"    Debug->>Unity: read EntitiesJournaling records in a separate official-diff pass\n"
                   + $"    Unity-->>Debug: journaling structural records {result.OfficialToolDiff.JournalingStructuralRecordCount}, profiler state {result.OfficialToolDiff.ProfilerCaptureState}\n"
                   + "```";
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
                    hash = AppendHash(hash, entry.SourceAsc.Index);
                    hash = AppendHash(hash, entry.TargetAsc.Index);
                    hash = AppendHash(hash, entry.SourceAbility.Index);
                    hash = AppendHash(hash, entry.GameplayEffect.Index);
                }

                return hash;
            }
        }

        private static uint CalculateSummaryHash(HeadlessAutoBattleResult result, uint factsHash)
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
                hash = AppendHash(hash, result.EventCounts.ExecutionCalculationOutputUpdated);
                hash = AppendHash(hash, result.EventCounts.CueRequests);
                hash = AppendHash(hash, result.RuntimeDiagnostics.EventCount);
                hash = AppendHash(hash, result.OfficialToolDiff.JournalingWorldRecordCount);
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

        private static bool HasArgument(string argument)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
