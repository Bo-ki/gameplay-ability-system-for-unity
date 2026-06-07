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
            var counters = result.RuntimeDiagnostics.CoreCounters;
            var backbone = result.RuntimeDiagnostics.FrameBackboneCounters;
            var factsHash = CalculateFactsHash(result.StructuredLogSnapshot);
            var summaryHash = CalculateSummaryHash(result, presentation.RuntimeMarkerCount, factsHash);
            var streamCarrierPressure = CalculateStreamCarrierPressure(result.RuntimeDiagnostics);
            var reselectTriggerMask = 0;
            if (counters.RandomLookupBudget > 0)
                reselectTriggerMask |= 1;
            if (result.DriverIssuedCommands > 1000)
                reselectTriggerMask |= 2;
            if (counters.ActiveMutationMigrationCarrierCount > 0)
                reselectTriggerMask |= 4;
            if (counters.ActiveMutationEstimatedRandomLookupCount > 0)
                reselectTriggerMask |= 8;
            if (streamCarrierPressure.WarningCount > 0)
                reselectTriggerMask |= 16;

            return new AutoChessValidationEvidence(
                result.Completed,
                result.Winner,
                scenario.ExpectedWinner,
                result.ScenarioScale,
                result.Units.Length,
                result.BattleTicks,
                result.TotalTicks,
                result.WarmupDroppedTicks,
                result.MeasuredTicks,
                result.DriverIssuedCommands,
                result.EventCounts.AttributeChanges,
                result.EventCounts.PeriodTickDamageFacts,
                result.EventCounts.PeriodTickDamageTotal,
                result.EventCounts.ExecutionCalculationOutputUpdated,
                result.EventCounts.CueRequests,
                result.RuntimeDiagnostics.EventCount,
                result.RuntimeDiagnostics.Stats.WarningCount,
                result.RuntimeDiagnostics.Stats.ErrorCount,
                AutoChessBattleValidationRun.CountBlockingDiagnosticErrors(result.RuntimeDiagnostics),
                counters.RequestCount,
                counters.FactCount,
                counters.DeltaCount,
                counters.CueCount,
                presentation.RuntimeMarkerCount,
                presentation.SourceLineCount,
                presentation.DisplayLineCount,
                presentation.DroppedLineCount,
                counters.PeakEventBusBufferLength,
                counters.PeakReplayCursorLag,
                scenario.ProcessWarmupRuns,
                backbone.EvidenceMask,
                reselectTriggerMask,
                counters.ActiveEffectSlotCount,
                counters.ActiveEffectSlotActiveCount,
                counters.ActiveEffectChunkSkipDuePeriodSlotCount,
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
                streamCarrierPressure.WarningCount,
                streamCarrierPressure.PeakCount,
                streamCarrierPressure.PeakCapacity,
                result.ElapsedMilliseconds,
                result.AverageTickMilliseconds,
                factsHash,
                summaryHash,
                true,
                officialDiffSeparatePass,
                result.OfficialToolDiff.JournalingAvailable,
                result.OfficialToolDiff.JournalingCaptured,
                result.OfficialToolDiff.ProfilerAvailable,
                result.OfficialToolDiff.ProfilerCaptureState,
                backbone.PhysicsDisabledReasonCount > 0 ? "reported-by-runtime" : "headless-profile-disabled",
                backbone.RenderDisabledReasonCount > 0 ? "reported-by-runtime" : "headless-profile-disabled",
                presentation.DisabledReason);
        }

        public static string CreateRunResultSummary(in AutoChessValidationRunResult runResult)
        {
            return $"passed={runResult.Passed}, "
                   + $"thresholdsPassed={runResult.GeneratedThresholdsPassed}, "
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
                      + $"streamCarrierPressureWarnings={evidence.StreamCarrierPressureWarningCount}, "
                      + $"streamCarrierPeak={evidence.StreamCarrierPeakCount}, "
                      + $"streamCarrierCapacity={evidence.StreamCarrierPeakCapacity}, "
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
            var stats = result.RuntimeDiagnostics.Stats;
            var counters = result.RuntimeDiagnostics.CoreCounters;
            var backbone = result.RuntimeDiagnostics.FrameBackboneCounters;
            return $"runtimeDiagnostics events={stats.RetainedEventCount}, "
                   + $"dropped={stats.DroppedEventCount}, "
                   + $"warnings={stats.WarningCount}, "
                   + $"errors={stats.ErrorCount}, "
                   + $"blockingErrors={AutoChessBattleValidationRun.CountBlockingDiagnosticErrors(result.RuntimeDiagnostics)}, "
                   + $"requests={counters.RequestCount}, "
                   + $"specs={counters.SpecCount}, "
                   + $"deltas={counters.DeltaCount}, "
                   + $"facts={counters.FactCount}, "
                   + $"cues={counters.CueCount}, "
                   + $"presentation={counters.PresentationCount}, "
                   + $"activeEffectOwners={counters.ActiveEffectStoreOwnerCount}, "
                   + $"activeEffectSlots={counters.ActiveEffectSlotCount}, "
                   + $"periodTickDamageFacts={result.EventCounts.PeriodTickDamageFacts}, "
                   + $"queryBudget={counters.QueryBudget}, "
                   + $"lookupBudget={counters.LookupUpdateBudget}, "
                   + $"randomLookupBudget={counters.RandomLookupBudget}, "
                    + $"syncQueryBudget={counters.SyncQueryBudget}, "
                    + $"activeMutationCommands={counters.ActiveMutationCommandCount}, "
                    + $"activeMutationOwnerGroups={counters.ActiveMutationOwnerGroupCount}, "
                    + $"activeMutationMaxOwnerRange={counters.ActiveMutationMaxOwnerRange}, "
                    + $"activeMutationSortMoves={counters.ActiveMutationSortMoveCount}, "
                     + $"activeMutationEstimatedRandomLookups={counters.ActiveMutationEstimatedRandomLookupCount}, "
                     + $"activeMutationOwnerResourceLookups={counters.ActiveMutationOwnerResourceLookupCount}, "
                     + $"activeMutationMigrationCarriers={counters.ActiveMutationMigrationCarrierCount}, "
                     + $"pendingAttributeDeltas={counters.PendingAttributeDeltaCount}, "
                     + $"pendingAttributeAppliedDeltas={counters.PendingAttributeAppliedDeltaCount}, "
                     + $"pendingAttributeSkippedDeltas={counters.PendingAttributeSkippedDeltaCount}, "
                     + $"pendingAttributeTargetGroups={counters.PendingAttributeTargetGroupCount}, "
                     + $"pendingAttributeMaxTargetRange={counters.PendingAttributeMaxTargetRange}, "
                      + $"pendingAttributeEstimatedRandomLookups={counters.PendingAttributeEstimatedRandomLookupCount}, "
                      + $"pendingAttributeFactPatches={counters.PendingAttributeFactPatchCount}, "
                      + $"pendingAttributeMigrationCarriers={counters.PendingAttributeMigrationCarrierCount}, "
                      + $"streamCarrierPressureWarnings={CalculateStreamCarrierPressure(result.RuntimeDiagnostics).WarningCount}, "
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
            return $"primaryCommands={result.DriverIssuedPrimaryCommands}, "
                   + $"finisherCommands={result.DriverIssuedFinisherCommands}, "
                   + $"lowestHealthSelections={result.DriverLowestHealthTargetSelections}, "
                   + $"tickAvgMs={result.RuntimeTiming.TickTotal.AverageMilliseconds:0.000}, "
                   + $"coreRuntimeOwnerAvgMs={result.RuntimeTiming.CoreRuntime.AverageMilliseconds:0.000}, "
                   + $"boundaryOwnerAvgMs={result.RuntimeTiming.Boundary.AverageMilliseconds:0.000}, "
                   + $"runnerOwnerAvgMs={result.RuntimeTiming.Runner.AverageMilliseconds:0.000}, "
                   + $"debuggerOwnerAvgMs={result.RuntimeTiming.Debugger.AverageMilliseconds:0.000}, "
                   + $"commandResolveAvgMs={result.RuntimeTiming.CommandResolve.AverageMilliseconds:0.000}, "
                   + $"coreSimulationAvgMs={result.RuntimeTiming.CoreSimulation.AverageMilliseconds:0.000}, "
                   + $"activeMutationCommands={result.RuntimeDiagnostics.CoreCounters.ActiveMutationCommandCount}, "
                   + $"activeMutationOwnerGroups={result.RuntimeDiagnostics.CoreCounters.ActiveMutationOwnerGroupCount}, "
                   + $"activeMutationMaxOwnerRange={result.RuntimeDiagnostics.CoreCounters.ActiveMutationMaxOwnerRange}, "
                   + $"activeMutationEstimatedRandomLookups={result.RuntimeDiagnostics.CoreCounters.ActiveMutationEstimatedRandomLookupCount}, "
                   + $"periodTickDamageFacts={result.EventCounts.PeriodTickDamageFacts}, "
                    + $"pendingAttributeDeltas={result.RuntimeDiagnostics.CoreCounters.PendingAttributeDeltaCount}, "
                    + $"pendingAttributeAppliedDeltas={result.RuntimeDiagnostics.CoreCounters.PendingAttributeAppliedDeltaCount}, "
                    + $"pendingAttributeTargetGroups={result.RuntimeDiagnostics.CoreCounters.PendingAttributeTargetGroupCount}, "
                    + $"pendingAttributeEstimatedRandomLookups={result.RuntimeDiagnostics.CoreCounters.PendingAttributeEstimatedRandomLookupCount}, "
                    + $"streamCarrierPressureWarnings={CalculateStreamCarrierPressure(result.RuntimeDiagnostics).WarningCount}, "
                    + $"structuralCommitAvgMs={result.RuntimeTiming.StructuralCommit.AverageMilliseconds:0.000}, "
                   + $"boundaryProjectionAvgMs={result.RuntimeTiming.BoundaryProjection.AverageMilliseconds:0.000}, "
                   + $"dependencyDrainAvgMs={result.RuntimeTiming.DependencyDrain.AverageMilliseconds:0.000}";
        }

        public static string CreateBoundaryOwnerSummary(in AutoChessBattleResult result)
        {
            return "shellPublicRawEcsSurface=false, "
                   + "unitIdentity=AutoChessBattleUnitKey, "
                   + "unitRuntimeHandle=ASCHandle, "
                   + "unitCreateOwner=RuntimeShell.ASCCommandCapability, "
                   + "unitDestroyOwner=ASCCommandPort.RequestDestroy, "
                   + "driverLifecycleOwner=AutoChessBattleDriverRuntimeStore, "
                   + "catalogOwner=AutoChessGasCatalogSession, "
                   + "snapshotOwner=AutoChessGasBattleUnitSnapshotProjector.StructuredLog, "
                   + "observationOwner=AutoChessGasObservationGateway, "
                   + "runnerSyncOwner=GASDependencyDrain, "
                   + "reportProjectionOwner=AutoChessRuntimeUnitResolver.BoundaryReportKey, "
                   + CreateBoundaryReportKeyCoverageSummary(result.StructuredLogSnapshot)
                   + $", factsHash=0x{CalculateFactsHash(result.StructuredLogSnapshot):X8}";
        }

        public static string CreateOfficialToolDiffSummary(
            in AutoChessBattleResult result,
            in AutoChessValidationEvidence evidence)
        {
            var official = result.OfficialToolDiff;
            var runtime = result.RuntimeDiagnostics.CoreCounters;
            var runtimeStructural = runtime.EntityCreateCount + runtime.EntityDestroyCount;
            var officialStructural = official.JournalingStructuralRecordCount;
            return $"runtimeSelfDiagnostics=events:{result.RuntimeDiagnostics.EventCount}, "
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
            return "```mermaid\n"
                   + "flowchart LR\n"
                   + $"    CommandDrive[\"AutoChessBattleCommandDriveSystem\\nscale: {result.ScenarioScale}, units: {result.Units.Length}\\ncommands: {result.DriverIssuedCommands}\"] --> AbilityBuffer[\"AbilityCommandBuffer\\nrequest entities avoided\"]\n"
                   + $"    AbilityBuffer --> RuntimeCore[\"GAS Runtime Core\\nrequests: {result.RuntimeDiagnostics.CoreCounters.RequestCount}\"]\n"
                   + $"    RuntimeCore --> GEStream[\"GEEffectCommandBuffer / Spec / Delta\\ndeltas: {result.RuntimeDiagnostics.CoreCounters.DeltaCount}\"]\n"
                   + $"    GEStream --> Execution[\"AutoChessExecuteDamageCalculationSystem\\nexecution outputs: {result.EventCounts.ExecutionCalculationOutputUpdated}\"]\n"
                   + $"    Execution --> PendingDelta[\"pending AttributeModifierBuffer\\nexecution outputs: {result.EventCounts.ExecutionCalculationOutputUpdated}\"]\n"
                   + $"    PendingDelta --> Attribute[\"GASAttributeModifierDeltaApplySystem\\nattribute changes: {result.EventCounts.AttributeChanges}\"]\n"
                   + $"    Attribute --> Facts[\"GameplayEventBuffer typed facts\\nfacts: {result.RuntimeDiagnostics.CoreCounters.FactCount}\"]\n"
                   + $"    Facts --> Projection[\"Replay / Presentation / Layer 2 Diagnostics\\nreplay events: {result.EventCounts.ReplayEvents}\"]\n"
                   + $"    Projection --> OfficialDiff[\"Official tool diff\\nseparate pass, journaling records: {result.OfficialToolDiff.JournalingWorldRecordCount}\"]\n"
                   + "```";
        }

        public static string CreateSequenceDiagram(in AutoChessBattleResult result)
        {
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
                   + $"    Obs->>Debug: counters {result.RuntimeDiagnostics.EventCount}, warnings {result.RuntimeDiagnostics.Stats.WarningCount}, errors {result.RuntimeDiagnostics.Stats.ErrorCount}\n"
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
            var events = diagnostics.Events ?? Array.Empty<GASRuntimeDiagnosticEventBuffer>();
            var warningCount = 0;
            var peakCount = 0;
            var peakCapacity = 0;

            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Kind != EGasRuntimeDiagnosticKind.BufferPressure
                    || evt.Module != EGasRuntimeDiagnosticModule.Effect
                    || evt.GroupName.ToString() != "EffectCommandSpecStream")
                {
                    continue;
                }

                if (evt.Severity >= EGasRuntimeDiagnosticSeverity.Warning
                    || evt.ValueB > 0)
                {
                    warningCount++;
                }

                if (evt.Count > peakCount)
                {
                    peakCount = evt.Count;
                    peakCapacity = evt.Capacity;
                }
            }

            return new StreamCarrierPressure(warningCount, peakCount, peakCapacity);
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
                hash = AppendHash(hash, result.RuntimeDiagnostics.EventCount);
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
