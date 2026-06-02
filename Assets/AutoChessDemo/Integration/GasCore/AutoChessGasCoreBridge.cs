using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasCoreBridge
    {
        public static GasRuntimeOfficialToolDiffSnapshot UnavailableOfficialToolDiff
            => GasRuntimeOfficialToolDiffSnapshot.Unavailable;

        public static void EnsureRuntimeInitialized()
        {
            if (!GASManager.IsInitialized)
                GASManager.Initialize(attachToPlayerLoop: false);

            AutoChessRuntimeSystemBootstrap.RegisterSystems(GASManager.ExWorld);
            AutoChessBattleDefinitionCatalogBuilder.Install(GASManager.EntityManager);
        }

        public static void ShutdownRuntime()
        {
            if (!GASManager.IsInitialized)
                return;

            AutoChessBattleDefinitionCatalogBuilder.Uninstall(GASManager.EntityManager);
            AutoChessRuntimeSystemBootstrap.Reset();
            GASManager.Shutdown();
        }

        public static AutoChessGasCoreOfficialToolDiffCapture BeginOfficialToolDiffCapture()
        {
            return new AutoChessGasCoreOfficialToolDiffCapture(
                GasRuntimeOfficialToolDiffCapture.Begin(GASManager.ExWorld));
        }

        public static void TickRuntime(
            bool recordTiming,
            ref AutoChessBattleRuntimeTiming runtimeTiming)
        {
            var world = GASManager.ExWorld;
            var framePrepareTicks = UpdateTimed(world.GetExistingSystemManaged<GASFramePrepareSystemGroup>());
            var commandResolveTicks = UpdateTimed(world.GetExistingSystemManaged<GASCommandResolveSystemGroup>());
            var coreSimulationTicks = UpdateTimed(world.GetExistingSystemManaged<GASCoreSimulationSystemGroup>());
            var structuralCommitTicks = UpdateTimed(world.GetExistingSystemManaged<GASStructuralCommitSystemGroup>());
            var boundaryProjectionTicks = UpdateTimed(world.GetExistingSystemManaged<GASBoundaryProjectionSystemGroup>());

            if (recordTiming)
            {
                runtimeTiming.Add(
                    framePrepareTicks,
                    commandResolveTicks,
                    coreSimulationTicks,
                    structuralCommitTicks,
                    boundaryProjectionTicks);
                RecordRuntimeTickTiming(
                    framePrepareTicks,
                    commandResolveTicks,
                    coreSimulationTicks,
                    structuralCommitTicks,
                    boundaryProjectionTicks);
            }
        }

        public static void ResetObservationState(in AutoChessBattleOptions options)
        {
            var em = GASManager.EntityManager;
            if (em.Exists(GASManager.EntityGlobalTimer))
                em.SetComponentData(GASManager.EntityGlobalTimer, new GlobalTimer());

            if (em.Exists(GASManager.EntityEventBus))
            {
                em.SetComponentData(GASManager.EntityEventBus, new GameplayEventBusComponent());
                if (em.HasComponent<PresentationOutboxProjectionStateComponent>(GASManager.EntityEventBus))
                    em.SetComponentData(GASManager.EntityEventBus, new PresentationOutboxProjectionStateComponent());
                if (em.HasComponent<PresentationOutboxProjectionOptionsComponent>(GASManager.EntityEventBus))
                {
                    em.SetComponentData(GASManager.EntityEventBus, new PresentationOutboxProjectionOptionsComponent
                    {
                        ProjectRawFacts = options.DebuggerEnabled ? (byte)1 : (byte)0,
                    });
                }

                ClearBuffer<DamageEventBuffer>(em, GASManager.EntityEventBus);
                ClearBuffer<TagChangeEventBuffer>(em, GASManager.EntityEventBus);
                ClearBuffer<GameplayEventBusEventBuffer>(em, GASManager.EntityEventBus);
                ClearBuffer<AttributeChangeEventBuffer>(em, GASManager.EntityEventBus);
                ClearBuffer<CueRequestBuffer>(em, GASManager.EntityEventBus);
            }

            if (em.Exists(GASManager.EntityEventLogSink))
            {
                em.SetComponentData(GASManager.EntityEventLogSink, new GameplayEventLogSinkComponent());
                ClearBuffer<ReplayLogEventBuffer>(em, GASManager.EntityEventLogSink);
            }

            if (em.Exists(GASManager.EntityRuntimeDebugger))
            {
                GasRuntimeDebugger.Reset(em, GASManager.EntityRuntimeDebugger);
                GasRuntimeDebugger.Configure(
                    em,
                    GASManager.EntityRuntimeDebugger,
                    options.DebuggerEnabled,
                    options.CaptureSystemTimings,
                    options.CaptureBufferPressure);
            }
        }

        public static AutoChessGasBattleUnitHandle CreateBattleUnit(AutoChessUnitDefinition definition)
        {
            var commandGateway = ASCCommandGateway.Create();
            commandGateway.Init(
                Array.Empty<int>(),
                new[]
                {
                    new AttrSetConfig(
                        AutoChessBattleRules.AttributeSetCombat,
                        new[]
                        {
                            new AttributeBaseSetting(
                                AutoChessBattleRules.AttributeHealth,
                                definition.Health,
                                true,
                                true,
                                0f,
                                definition.Health),
                            new AttributeBaseSetting(
                                AutoChessBattleRules.AttributeEnergy,
                                definition.Energy,
                                true,
                                true,
                                0f,
                                definition.Energy),
                        }),
                },
                definition.CreateAbilityCodes(),
                1);

            AddBattleUnitComponent(commandGateway.Entity, definition);
            return new AutoChessGasBattleUnitHandle(commandGateway.Entity);
        }

        public static Entity CreateBattleDriver()
        {
            var em = GASManager.EntityManager;
            var driver = em.CreateEntity();
            em.SetName(driver, "AutoChessBattleCommandDriver");
            em.AddComponentData(driver, new AutoChessBattleDriverComponent
            {
                Enabled = true,
                LastDecisionFrame = -1,
                LastExecutionFrame = -1,
                LastOutcomeFrame = -1,
            });
            return driver;
        }

        public static void CacheGrantedAbilityEntities(AutoChessGasBattleUnitHandle handle)
        {
            var em = GASManager.EntityManager;
            var asc = handle.AscEntity;
            if (asc == Entity.Null
                || !em.Exists(asc)
                || !em.HasComponent<AutoChessBattleUnitComponent>(asc)
                || !em.HasBuffer<AbilitySlotBuffer>(asc))
            {
                return;
            }

            var component = em.GetComponentData<AutoChessBattleUnitComponent>(asc);
            var abilitySlots = em.GetBuffer<AbilitySlotBuffer>(asc);
            component.PrimaryAbilityEntity = ResolveGrantedAbilityEntity(
                em,
                abilitySlots,
                component.PrimaryAbilityCode);
            component.FinisherAbilityEntity = ResolveGrantedAbilityEntity(
                em,
                abilitySlots,
                component.FinisherAbilityCode);
            em.SetComponentData(asc, component);
        }

        public static AutoChessBattleDriverComponent GetBattleDriverStats(Entity driverEntity)
        {
            var em = GASManager.EntityManager;
            return driverEntity != Entity.Null
                   && em.Exists(driverEntity)
                   && em.HasComponent<AutoChessBattleDriverComponent>(driverEntity)
                ? em.GetComponentData<AutoChessBattleDriverComponent>(driverEntity)
                : default;
        }

        public static void DestroyBattleDriver(Entity driverEntity)
        {
            var em = GASManager.EntityManager;
            if (driverEntity != Entity.Null && em.Exists(driverEntity))
                em.DestroyEntity(driverEntity);
        }

        public static void DestroyBattleUnit(AutoChessGasBattleUnitHandle handle)
        {
            var em = GASManager.EntityManager;
            var asc = handle.AscEntity;
            if (asc == Entity.Null || !em.Exists(asc))
                return;

            DestroyGrantedAbilities(em, asc);
            DestroyActiveEffects(em, asc);
            em.DestroyEntity(asc);
        }

        public static float ReadCombatAttribute(Entity asc, int attributeCode)
        {
            var em = GASManager.EntityManager;
            if (asc == Entity.Null || !em.Exists(asc) || !em.HasBuffer<AttributeValueBuffer>(asc))
                return 0f;

            var attributes = em.GetBuffer<AttributeValueBuffer>(asc);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttrSetCode == AutoChessBattleRules.AttributeSetCombat
                    && attribute.Code == attributeCode)
                {
                    return attribute.CurrentValue;
                }
            }

            return 0f;
        }

        public static AutoChessGasCoreObservationSnapshot CreateObservationSnapshot()
        {
            var em = GASManager.EntityManager;
            var log = em.GetBuffer<ReplayLogEventBuffer>(GASManager.EntityEventLogSink);
            var sinkState = em.GetComponentData<GameplayEventLogSinkComponent>(GASManager.EntityEventLogSink);
            var structuredLog = GasStructuredLogExporter.CreateSnapshot(log, sinkState);
            var assertionLog = GasStructuredLogExporter.ExportToText(
                structuredLog,
                GasStructuredLogFormatOptions.AssertionText);
            var runtimeDiagnostics = GasRuntimeDebugger.CreateSnapshot(em, GASManager.EntityRuntimeDebugger);
            var runtimeDiagnosticsLog = GasRuntimeDebugger.ExportToText(runtimeDiagnostics, maxEvents: 96);

            return new AutoChessGasCoreObservationSnapshot(
                structuredLog,
                assertionLog,
                runtimeDiagnostics,
                runtimeDiagnosticsLog,
                CountEvents(log, structuredLog.EntryCount));
        }

        public static double ToMilliseconds(long stopwatchTicks)
        {
            return stopwatchTicks * 1000d / Stopwatch.Frequency;
        }

        private static long UpdateTimed(ComponentSystemGroup group)
        {
            var start = Stopwatch.GetTimestamp();
            group.Update();
            return Stopwatch.GetTimestamp() - start;
        }

        private static void AddBattleUnitComponent(Entity asc, AutoChessUnitDefinition definition)
        {
            var em = GASManager.EntityManager;
            if (asc == Entity.Null || !em.Exists(asc))
                return;

            em.AddComponentData(asc, new AutoChessBattleUnitComponent
            {
                BattleGroup = definition.BattleGroup,
                Team = definition.Team,
                Slot = definition.Slot,
                PrimaryAbilityCode = definition.PrimaryAbilityCode,
                FinisherAbilityCode = definition.FinisherAbilityCode,
                PrimaryAbilityEntity = Entity.Null,
                FinisherAbilityEntity = Entity.Null,
                HealthAttrSetCode = AutoChessBattleRules.AttributeSetCombat,
                HealthAttrCode = AutoChessBattleRules.AttributeHealth,
                EnergyAttrSetCode = AutoChessBattleRules.AttributeSetCombat,
                EnergyAttrCode = AutoChessBattleRules.AttributeEnergy,
                CooldownTagIndex = AutoChessBattleRules.TagAttackCooldown,
                FinisherHealthThreshold = definition.FinisherHealthThreshold,
                PrimaryTargetPolicy = definition.PrimaryTargetPolicy,
                FinisherTargetPolicy = definition.FinisherTargetPolicy,
            });
        }

        private static Entity ResolveGrantedAbilityEntity(
            EntityManager em,
            DynamicBuffer<AbilitySlotBuffer> abilitySlots,
            int abilityCode)
        {
            if (abilityCode <= 0)
                return Entity.Null;

            for (var i = 0; i < abilitySlots.Length; i++)
            {
                var ability = abilitySlots[i].AbilityEntity;
                if (ability == Entity.Null
                    || !em.Exists(ability)
                    || !em.HasComponent<AbilityStateComponent>(ability))
                {
                    continue;
                }

                if (em.GetComponentData<AbilityStateComponent>(ability).Code == abilityCode)
                    return ability;
            }

            return Entity.Null;
        }

        private static void DestroyGrantedAbilities(EntityManager em, Entity asc)
        {
            if (!em.HasBuffer<AbilitySlotBuffer>(asc))
                return;

            var abilities = em.GetBuffer<AbilitySlotBuffer>(asc);
            var abilityEntities = new NativeArray<Entity>(abilities.Length, Allocator.Temp);
            for (var i = 0; i < abilities.Length; i++)
                abilityEntities[i] = abilities[i].AbilityEntity;

            try
            {
                for (var i = abilityEntities.Length - 1; i >= 0; i--)
                {
                    var ability = abilityEntities[i];
                    if (ability == Entity.Null || !em.Exists(ability))
                        continue;

                    em.DestroyEntity(ability);
                }
            }
            finally
            {
                abilityEntities.Dispose();
            }
        }

        private static void DestroyActiveEffects(EntityManager em, Entity asc)
        {
            if (!em.HasBuffer<LegacyGameplayEffectEntityBuffer>(asc))
                return;

            var effects = em.GetBuffer<LegacyGameplayEffectEntityBuffer>(asc);
            var effectEntities = new NativeArray<Entity>(effects.Length, Allocator.Temp);
            for (var i = 0; i < effects.Length; i++)
                effectEntities[i] = effects[i].GameplayEffect;

            try
            {
                for (var i = effectEntities.Length - 1; i >= 0; i--)
                {
                    var effect = effectEntities[i];
                    if (effect != Entity.Null && em.Exists(effect))
                        em.DestroyEntity(effect);
                }
            }
            finally
            {
                effectEntities.Dispose();
            }
        }

        private static void RecordRuntimeTickTiming(
            long framePrepareTicks,
            long commandResolveTicks,
            long coreSimulationTicks,
            long structuralCommitTicks,
            long boundaryProjectionTicks)
        {
            var em = GASManager.EntityManager;
            if (!em.Exists(GASManager.EntityRuntimeDebugger))
                return;

            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            var totalTicks = framePrepareTicks
                             + commandResolveTicks
                             + coreSimulationTicks
                             + structuralCommitTicks
                             + boundaryProjectionTicks;
            var frequency = Stopwatch.Frequency;

            GasRuntimeDebugger.RecordSystemTimingAggregate(
                em,
                GASManager.EntityRuntimeDebugger,
                frame,
                "PhysicalGroup",
                "GASTickTotal",
                1,
                totalTicks,
                frequency);
            GasRuntimeDebugger.RecordSystemTimingAggregate(
                em,
                GASManager.EntityRuntimeDebugger,
                frame,
                "PhysicalGroup",
                nameof(GASFramePrepareSystemGroup),
                1,
                framePrepareTicks,
                frequency);
            GasRuntimeDebugger.RecordSystemTimingAggregate(
                em,
                GASManager.EntityRuntimeDebugger,
                frame,
                "PhysicalGroup",
                nameof(GASCommandResolveSystemGroup),
                1,
                commandResolveTicks,
                frequency);
            GasRuntimeDebugger.RecordSystemTimingAggregate(
                em,
                GASManager.EntityRuntimeDebugger,
                frame,
                "PhysicalGroup",
                nameof(GASCoreSimulationSystemGroup),
                1,
                coreSimulationTicks,
                frequency);
            GasRuntimeDebugger.RecordSystemTimingAggregate(
                em,
                GASManager.EntityRuntimeDebugger,
                frame,
                "PhysicalGroup",
                nameof(GASStructuralCommitSystemGroup),
                1,
                structuralCommitTicks,
                frequency);
            GasRuntimeDebugger.RecordSystemTimingAggregate(
                em,
                GASManager.EntityRuntimeDebugger,
                frame,
                "PhysicalGroup",
                nameof(GASBoundaryProjectionSystemGroup),
                1,
                boundaryProjectionTicks,
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
                tagChanges,
                cueRequests);
        }

        private static void ClearBuffer<T>(EntityManager em, Entity entity)
            where T : unmanaged, IBufferElementData
        {
            if (em.Exists(entity) && em.HasBuffer<T>(entity))
                em.GetBuffer<T>(entity).Clear();
        }
    }

    internal readonly struct AutoChessGasBattleUnitHandle
    {
        public readonly Entity AscEntity;

        public AutoChessGasBattleUnitHandle(Entity ascEntity)
        {
            AscEntity = ascEntity;
        }
    }

    internal struct AutoChessGasCoreOfficialToolDiffCapture
    {
        private GasRuntimeOfficialToolDiffCapture _capture;
        private byte _started;

        public AutoChessGasCoreOfficialToolDiffCapture(GasRuntimeOfficialToolDiffCapture capture)
        {
            _capture = capture;
            _started = 1;
        }

        public GasRuntimeOfficialToolDiffSnapshot End()
        {
            if (_started == 0)
                return GasRuntimeOfficialToolDiffSnapshot.Unavailable;

            _started = 0;
            return _capture.End();
        }
    }

    internal readonly struct AutoChessGasCoreObservationSnapshot
    {
        public readonly GasStructuredLogExportSnapshot StructuredLog;
        public readonly string AssertionLog;
        public readonly GasRuntimeDiagnosticSnapshot RuntimeDiagnostics;
        public readonly string RuntimeDiagnosticsLog;
        public readonly AutoChessBattleEventCounts EventCounts;

        public AutoChessGasCoreObservationSnapshot(
            GasStructuredLogExportSnapshot structuredLog,
            string assertionLog,
            GasRuntimeDiagnosticSnapshot runtimeDiagnostics,
            string runtimeDiagnosticsLog,
            AutoChessBattleEventCounts eventCounts)
        {
            StructuredLog = structuredLog;
            AssertionLog = assertionLog ?? string.Empty;
            RuntimeDiagnostics = runtimeDiagnostics;
            RuntimeDiagnosticsLog = runtimeDiagnosticsLog ?? string.Empty;
            EventCounts = eventCounts;
        }
    }
}
