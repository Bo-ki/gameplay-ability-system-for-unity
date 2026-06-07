using GAS.Runtime;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasObservationGateway
    {
        public static GasRuntimeOfficialToolDiffSnapshot UnavailableOfficialToolDiff
            => GasRuntimeOfficialToolDiffSnapshot.Unavailable;

        public static AutoChessGasCoreOfficialToolDiffCapture BeginOfficialToolDiffCapture()
        {
            return GASRuntimeShell.TryResolveRuntimeWorld(out var world)
                ? new AutoChessGasCoreOfficialToolDiffCapture(
                    GasRuntimeOfficialToolDiffCapture.Begin(world))
                : default;
        }

        public static void ResetObservationState(in AutoChessBattleOptions options)
        {
            if (GASRuntimeShell.TryResolveGlobalTimer(out var entityManager, out var globalTimer))
                entityManager.SetComponentData(globalTimer, new GlobalTimer());

            if (GASRuntimeShell.TryResolveEventBus(out entityManager, out var eventBus))
            {
                entityManager.SetComponentData(eventBus, new GameplayEventBusComponent());
                if (entityManager.HasComponent<PresentationOutboxProjectionStateComponent>(eventBus))
                    entityManager.SetComponentData(eventBus, new PresentationOutboxProjectionStateComponent());
                if (entityManager.HasComponent<PresentationOutboxProjectionOptionsComponent>(eventBus))
                {
                    entityManager.SetComponentData(eventBus, new PresentationOutboxProjectionOptionsComponent
                    {
                        ProjectRawFacts = options.DebuggerEnabled ? (byte)1 : (byte)0,
                    });
                }

                ClearBuffer<TagChangeEventBuffer>(entityManager, eventBus);
                ClearBuffer<AttributeChangeEventBuffer>(entityManager, eventBus);
                ClearBuffer<CueRequestBuffer>(entityManager, eventBus);
            }

            if (GASRuntimeShell.TryResolveEventLogSink(out entityManager, out var eventLogSink))
            {
                entityManager.SetComponentData(eventLogSink, new GameplayEventLogSinkComponent());
                ClearBuffer<ReplayLogEventBuffer>(entityManager, eventLogSink);
            }

            if (GASRuntimeShell.TryResolveRuntimeDebugger(out entityManager, out var runtimeDebugger))
            {
                GasRuntimeDebugger.Reset(entityManager, runtimeDebugger);
                GasRuntimeDebugger.Configure(
                    entityManager,
                    runtimeDebugger,
                    options.DebuggerEnabled,
                    options.CaptureSystemTimings,
                    options.CaptureBufferPressure);
            }
        }

        public static AutoChessGasCoreObservationSnapshot CreateObservationSnapshot()
        {
            if (!GASRuntimeShell.TryResolveEventLogSink(out var entityManager, out var eventLogSink)
                || !GASRuntimeShell.TryResolveRuntimeDebugger(out _, out var runtimeDebugger))
            {
                return default;
            }

            var log = entityManager.GetBuffer<ReplayLogEventBuffer>(eventLogSink);
            var sinkState = entityManager.GetComponentData<GameplayEventLogSinkComponent>(eventLogSink);
            var structuredLog = GasStructuredLogExporter.CreateSnapshot(log, sinkState);
            var assertionLog = GasStructuredLogExporter.ExportToText(
                structuredLog,
                GasStructuredLogFormatOptions.AssertionText);
            var runtimeDiagnostics = GasRuntimeDebugger.CreateSnapshot(entityManager, runtimeDebugger);
            var runtimeDiagnosticsLog = GasRuntimeDebugger.ExportToText(runtimeDiagnostics, maxEvents: 96);

            return new AutoChessGasCoreObservationSnapshot(
                structuredLog,
                assertionLog,
                runtimeDiagnostics,
                runtimeDiagnosticsLog,
                CountEvents(log, structuredLog.EntryCount));
        }

        public static void RecordRuntimeTickTiming(
            long framePrepareTicks,
            long commandResolveTicks,
            long coreSimulationTicks,
            long structuralCommitTicks,
            long boundaryProjectionTicks,
            long dependencyDrainTicks,
            long frequency)
        {
            if (!GASRuntimeShell.TryResolveRuntimeDebugger(out var entityManager, out var runtimeDebugger))
                return;

            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(entityManager);
            var totalTicks = framePrepareTicks
                             + commandResolveTicks
                             + coreSimulationTicks
                             + structuralCommitTicks
                             + boundaryProjectionTicks
                             + dependencyDrainTicks;

            GasRuntimeDebugger.RecordSystemTimingAggregate(
                entityManager,
                runtimeDebugger,
                frame,
                "PhysicalGroup",
                "GASTickTotal",
                1,
                totalTicks,
                frequency);
            GasRuntimeDebugger.RecordSystemTimingAggregate(
                entityManager,
                runtimeDebugger,
                frame,
                "PhysicalGroup",
                nameof(GASFramePrepareSystemGroup),
                1,
                framePrepareTicks,
                frequency);
            GasRuntimeDebugger.RecordSystemTimingAggregate(
                entityManager,
                runtimeDebugger,
                frame,
                "PhysicalGroup",
                nameof(GASCommandResolveSystemGroup),
                1,
                commandResolveTicks,
                frequency);
            GasRuntimeDebugger.RecordSystemTimingAggregate(
                entityManager,
                runtimeDebugger,
                frame,
                "PhysicalGroup",
                nameof(GASCoreSimulationSystemGroup),
                1,
                coreSimulationTicks,
                frequency);
            GasRuntimeDebugger.RecordSystemTimingAggregate(
                entityManager,
                runtimeDebugger,
                frame,
                "PhysicalGroup",
                nameof(GASStructuralCommitSystemGroup),
                1,
                structuralCommitTicks,
                frequency);
            GasRuntimeDebugger.RecordSystemTimingAggregate(
                entityManager,
                runtimeDebugger,
                frame,
                "PhysicalGroup",
                nameof(GASBoundaryProjectionSystemGroup),
                1,
                boundaryProjectionTicks,
                frequency);
            GasRuntimeDebugger.RecordSystemTimingAggregate(
                entityManager,
                runtimeDebugger,
                frame,
                "PhysicalGroup",
                "GASDependencyDrain",
                1,
                dependencyDrainTicks,
                frequency);
        }

        private static AutoChessBattleEventCounts CountEvents(
            DynamicBuffer<ReplayLogEventBuffer> replayLog,
            int structuredLogEntries)
        {
            var abilityCommitSucceeded = 0;
            var gameplayEffectInstanced = 0;
            var gameplayEffectApplied = 0;
            var gameplayEffectRemoved = 0;
            var executionCalculationOutputUpdated = 0;
            var attributeChanges = 0;
            var periodTickDamageFacts = 0;
            var periodTickDamageTotal = 0f;
            var tagChanges = 0;
            var cueRequests = 0;

            for (var i = 0; i < replayLog.Length; i++)
            {
                var evt = replayLog[i];
                switch (evt.Kind)
                {
                    case EDebugReplayEventKind.GameplayEvent:
                        switch (evt.GameplayEventType)
                        {
                            case EGameplayEventType.AbilityCommitSucceeded:
                                abilityCommitSucceeded++;
                                break;
                            case EGameplayEventType.GameplayEffectInstanced:
                                gameplayEffectInstanced++;
                                break;
                            case EGameplayEventType.GameplayEffectApplied:
                                gameplayEffectApplied++;
                                break;
                            case EGameplayEventType.GameplayEffectRemoved:
                                gameplayEffectRemoved++;
                                break;
                            case EGameplayEventType.ExecutionCalculationOutputUpdated:
                                executionCalculationOutputUpdated++;
                                break;
                        }
                        break;
                    case EDebugReplayEventKind.AttributeChange:
                        attributeChanges++;
                        if (IsPoisonPeriodDamageFact(in evt))
                        {
                            periodTickDamageFacts++;
                            periodTickDamageTotal += evt.OldValue - evt.NewValue;
                        }
                        break;
                    case EDebugReplayEventKind.TagChange:
                        tagChanges++;
                        break;
                    case EDebugReplayEventKind.CueRequest:
                        cueRequests++;
                        break;
                }
            }

            return new AutoChessBattleEventCounts(
                replayLog.Length,
                structuredLogEntries,
                abilityCommitSucceeded,
                gameplayEffectInstanced,
                gameplayEffectApplied,
                gameplayEffectRemoved,
                executionCalculationOutputUpdated,
                attributeChanges,
                periodTickDamageFacts,
                periodTickDamageTotal,
                tagChanges,
                cueRequests);
        }

        private static bool IsPoisonPeriodDamageFact(in ReplayLogEventBuffer evt)
        {
            return evt.EventCode == AutoChessBattleRules.GameplayEffectPoisonTickDamage
                   && evt.AttrSetCode == AutoChessBattleRules.AttributeSetCombat
                   && evt.AttributeCode == AutoChessBattleRules.AttributeHealth
                   && evt.NewValue < evt.OldValue;
        }

        private static void ClearBuffer<T>(EntityManager entityManager, Entity entity)
            where T : unmanaged, IBufferElementData
        {
            if (entityManager.Exists(entity) && entityManager.HasBuffer<T>(entity))
                entityManager.GetBuffer<T>(entity).Clear();
        }
    }
}
