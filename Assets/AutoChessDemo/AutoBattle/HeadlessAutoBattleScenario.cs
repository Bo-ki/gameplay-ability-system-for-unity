using System;
using System.Collections;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    public enum HeadlessAutoBattleTeam : byte
    {
        None = 0,
        Player = 1,
        Enemy = 2,
        Draw = 3,
    }

    public readonly struct HeadlessAutoBattleOptions
    {
        public readonly int MaxTicks;
        public readonly int PostVictoryFlushTicks;
        public readonly int Scale;
        public readonly float HealthMultiplier;
        public readonly double MinimumBattleSeconds;
        private readonly byte _captureOfficialToolDiff;
        private readonly byte _debuggerEnabled;
        private readonly byte _captureSystemTimings;
        private readonly byte _captureBufferPressure;

        public bool CaptureOfficialToolDiff => _captureOfficialToolDiff != 2;
        public bool DebuggerEnabled => _debuggerEnabled != 2;
        public bool CaptureSystemTimings => _captureSystemTimings != 2;
        public bool CaptureBufferPressure => _captureBufferPressure != 2;

        public HeadlessAutoBattleOptions(
            int maxTicks,
            int postVictoryFlushTicks,
            int scale = 1,
            bool captureOfficialToolDiff = true,
            bool debuggerEnabled = true,
            bool captureSystemTimings = true,
            bool captureBufferPressure = true,
            float healthMultiplier = 1f,
            double minimumBattleSeconds = 0d)
        {
            MaxTicks = maxTicks;
            PostVictoryFlushTicks = postVictoryFlushTicks;
            Scale = scale;
            HealthMultiplier = healthMultiplier;
            MinimumBattleSeconds = minimumBattleSeconds;
            _captureOfficialToolDiff = captureOfficialToolDiff ? (byte)1 : (byte)2;
            _debuggerEnabled = debuggerEnabled ? (byte)1 : (byte)2;
            _captureSystemTimings = captureSystemTimings ? (byte)1 : (byte)2;
            _captureBufferPressure = captureBufferPressure ? (byte)1 : (byte)2;
        }

        public HeadlessAutoBattleOptions Normalize()
        {
            return new HeadlessAutoBattleOptions(
                MaxTicks > 0 ? MaxTicks : 64,
                PostVictoryFlushTicks >= 0 ? PostVictoryFlushTicks : 4,
                Scale > 0 ? Scale : 1,
                CaptureOfficialToolDiff,
                DebuggerEnabled,
                CaptureSystemTimings,
                CaptureBufferPressure,
                HealthMultiplier > 0f ? HealthMultiplier : 1f,
                MinimumBattleSeconds > 0d ? MinimumBattleSeconds : 0d);
        }
    }

    public readonly struct HeadlessAutoBattleProfileHooks
    {
        public readonly Action BeforeMeasuredWindow;
        public readonly Action AfterMeasuredWindow;

        public HeadlessAutoBattleProfileHooks(
            Action beforeMeasuredWindow,
            Action afterMeasuredWindow)
        {
            BeforeMeasuredWindow = beforeMeasuredWindow;
            AfterMeasuredWindow = afterMeasuredWindow;
        }
    }

    public readonly struct HeadlessAutoBattleUnitResult
    {
        public readonly string Id;
        public readonly HeadlessAutoBattleTeam Team;
        public readonly int Slot;
        public readonly float Health;
        public readonly float Energy;
        public readonly bool Alive;

        public HeadlessAutoBattleUnitResult(
            string id,
            HeadlessAutoBattleTeam team,
            int slot,
            float health,
            float energy,
            bool alive)
        {
            Id = id;
            Team = team;
            Slot = slot;
            Health = health;
            Energy = energy;
            Alive = alive;
        }
    }

    public readonly struct HeadlessAutoBattleEventCounts
    {
        public readonly int ReplayEvents;
        public readonly int StructuredLogEntries;
        public readonly int AbilityCommitSucceeded;
        public readonly int GameplayEffectInstanced;
        public readonly int GameplayEffectApplied;
        public readonly int GameplayEffectRemoved;
        public readonly int ExecutionCalculationOutputUpdated;
        public readonly int AttributeChanges;
        public readonly int TagChanges;
        public readonly int CueRequests;

        public HeadlessAutoBattleEventCounts(
            int replayEvents,
            int structuredLogEntries,
            int abilityCommitSucceeded,
            int gameplayEffectInstanced,
            int gameplayEffectApplied,
            int gameplayEffectRemoved,
            int executionCalculationOutputUpdated,
            int attributeChanges,
            int tagChanges,
            int cueRequests)
        {
            ReplayEvents = replayEvents;
            StructuredLogEntries = structuredLogEntries;
            AbilityCommitSucceeded = abilityCommitSucceeded;
            GameplayEffectInstanced = gameplayEffectInstanced;
            GameplayEffectApplied = gameplayEffectApplied;
            GameplayEffectRemoved = gameplayEffectRemoved;
            ExecutionCalculationOutputUpdated = executionCalculationOutputUpdated;
            AttributeChanges = attributeChanges;
            TagChanges = tagChanges;
            CueRequests = cueRequests;
        }
    }

    public struct HeadlessAutoBattleSystemTiming
    {
        public int Samples;
        public long TotalTicks;
        public long MaxTicks;

        public double AverageMilliseconds => Samples > 0 ? ToMilliseconds(TotalTicks) / Samples : 0d;

        public double MaxMilliseconds => ToMilliseconds(MaxTicks);

        public void Add(long elapsedTicks)
        {
            Samples++;
            TotalTicks += elapsedTicks;
            if (elapsedTicks > MaxTicks)
                MaxTicks = elapsedTicks;
        }

        private static double ToMilliseconds(long stopwatchTicks)
        {
            return stopwatchTicks * 1000d / Stopwatch.Frequency;
        }
    }

    public struct HeadlessAutoBattleRuntimeTiming
    {
        public HeadlessAutoBattleSystemTiming TickTotal;
        public HeadlessAutoBattleSystemTiming FramePrepare;
        public HeadlessAutoBattleSystemTiming CommandResolve;
        public HeadlessAutoBattleSystemTiming CoreSimulation;
        public HeadlessAutoBattleSystemTiming StructuralCommit;
        public HeadlessAutoBattleSystemTiming BoundaryProjection;

        public void Add(
            long framePrepareTicks,
            long commandResolveTicks,
            long coreSimulationTicks,
            long structuralCommitTicks,
            long boundaryProjectionTicks)
        {
            var totalTicks = framePrepareTicks
                             + commandResolveTicks
                             + coreSimulationTicks
                             + structuralCommitTicks
                             + boundaryProjectionTicks;
            TickTotal.Add(totalTicks);
            FramePrepare.Add(framePrepareTicks);
            CommandResolve.Add(commandResolveTicks);
            CoreSimulation.Add(coreSimulationTicks);
            StructuralCommit.Add(structuralCommitTicks);
            BoundaryProjection.Add(boundaryProjectionTicks);
        }
    }

    public readonly struct HeadlessAutoBattleResult
    {
        public readonly bool Completed;
        public readonly HeadlessAutoBattleTeam Winner;
        public readonly int ScenarioScale;
        public readonly int BattleTicks;
        public readonly int TotalTicks;
        public readonly int WarmupDroppedTicks;
        public readonly int MeasuredTicks;
        public readonly int DriverIssuedCommands;
        public readonly int DriverIssuedPrimaryCommands;
        public readonly int DriverIssuedFinisherCommands;
        public readonly int DriverLowestHealthTargetSelections;
        public readonly long ElapsedTicks;
        public readonly double ElapsedMilliseconds;
        public readonly long MeasuredElapsedTicks;
        public readonly double MeasuredElapsedMilliseconds;
        public readonly double AverageTickMilliseconds;
        public readonly HeadlessAutoBattleRuntimeTiming RuntimeTiming;
        public readonly HeadlessAutoBattleUnitResult[] Units;
        public readonly HeadlessAutoBattleEventCounts EventCounts;
        public readonly GasRuntimeDiagnosticSnapshot RuntimeDiagnostics;
        public readonly string RuntimeDiagnosticsLog;
        public readonly GasRuntimeOfficialToolDiffSnapshot OfficialToolDiff;
        public readonly GasStructuredLogExportSnapshot StructuredLogSnapshot;
        public readonly string AssertionLog;

        public HeadlessAutoBattleResult(
            bool completed,
            HeadlessAutoBattleTeam winner,
            int scenarioScale,
            int battleTicks,
            int totalTicks,
            int warmupDroppedTicks,
            int measuredTicks,
            int driverIssuedCommands,
            int driverIssuedPrimaryCommands,
            int driverIssuedFinisherCommands,
            int driverLowestHealthTargetSelections,
            long elapsedTicks,
            double elapsedMilliseconds,
            long measuredElapsedTicks,
            double measuredElapsedMilliseconds,
            HeadlessAutoBattleRuntimeTiming runtimeTiming,
            HeadlessAutoBattleUnitResult[] units,
            HeadlessAutoBattleEventCounts eventCounts,
            GasRuntimeDiagnosticSnapshot runtimeDiagnostics,
            string runtimeDiagnosticsLog,
            GasRuntimeOfficialToolDiffSnapshot officialToolDiff,
            GasStructuredLogExportSnapshot structuredLogSnapshot,
            string assertionLog)
        {
            Completed = completed;
            Winner = winner;
            ScenarioScale = scenarioScale;
            BattleTicks = battleTicks;
            TotalTicks = totalTicks;
            WarmupDroppedTicks = warmupDroppedTicks;
            MeasuredTicks = measuredTicks;
            DriverIssuedCommands = driverIssuedCommands;
            DriverIssuedPrimaryCommands = driverIssuedPrimaryCommands;
            DriverIssuedFinisherCommands = driverIssuedFinisherCommands;
            DriverLowestHealthTargetSelections = driverLowestHealthTargetSelections;
            ElapsedTicks = elapsedTicks;
            ElapsedMilliseconds = elapsedMilliseconds;
            MeasuredElapsedTicks = measuredElapsedTicks;
            MeasuredElapsedMilliseconds = measuredElapsedMilliseconds;
            AverageTickMilliseconds = measuredTicks > 0 ? measuredElapsedMilliseconds / measuredTicks : 0d;
            RuntimeTiming = runtimeTiming;
            Units = units ?? Array.Empty<HeadlessAutoBattleUnitResult>();
            EventCounts = eventCounts;
            RuntimeDiagnostics = runtimeDiagnostics;
            RuntimeDiagnosticsLog = runtimeDiagnosticsLog ?? string.Empty;
            OfficialToolDiff = officialToolDiff;
            StructuredLogSnapshot = structuredLogSnapshot;
            AssertionLog = assertionLog ?? string.Empty;
        }

        public HeadlessAutoBattleResult WithOfficialToolDiff(GasRuntimeOfficialToolDiffSnapshot officialToolDiff)
        {
            return new HeadlessAutoBattleResult(
                Completed,
                Winner,
                ScenarioScale,
                BattleTicks,
                TotalTicks,
                WarmupDroppedTicks,
                MeasuredTicks,
                DriverIssuedCommands,
                DriverIssuedPrimaryCommands,
                DriverIssuedFinisherCommands,
                DriverLowestHealthTargetSelections,
                ElapsedTicks,
                ElapsedMilliseconds,
                MeasuredElapsedTicks,
                MeasuredElapsedMilliseconds,
                RuntimeTiming,
                Units,
                EventCounts,
                RuntimeDiagnostics,
                RuntimeDiagnosticsLog,
                officialToolDiff,
                StructuredLogSnapshot,
                AssertionLog);
        }
    }

    /// <summary>
    /// Deterministic, no-UI validation scenario for the ECS Runtime GAS chain.
    /// It drives battle AI through request entities and validates from replay/log facts.
    /// </summary>
    public static class HeadlessAutoBattleScenario
    {
        public const int AttributeSetCombat = 9001;
        public const int AttributeHealth = 1;
        public const int AttributeEnergy = 2;

        public const int AbilityPlayerAttack = 9101;
        public const int AbilityEnemyAttack = 9102;
        public const int AbilityPlayerExecute = 9103;

        public const int GameplayEffectPlayerAttackDamage = 9201;
        public const int GameplayEffectEnemyAttackDamage = 9202;
        public const int GameplayEffectPlayerExecute = 9207;

        public const int ExecutionCalculationExecuteDamage = 9401;
        public const int ExecutionCalculationExecuteDamageOutput = 9402;

        public const int TagAttackCooldown = 1;
        private const int WarmupRuntimeTicks = 3;

        public static HeadlessAutoBattleResult RunDefault(
            HeadlessAutoBattleOptions options = default,
            HeadlessAutoBattleProfileHooks profileHooks = default)
        {
            var normalized = options.Normalize();
            EnsureRuntimeInitialized();
            ResetObservationState(normalized);

            var officialToolDiffCapture = default(GasRuntimeOfficialToolDiffCapture);
            var officialToolDiffClosed = false;
            var officialToolDiffStarted = false;
            var officialToolDiff = GasRuntimeOfficialToolDiffSnapshot.Unavailable;
            var measuredWindowOpened = false;
            var measuredWindowClosed = false;
            var stopwatch = Stopwatch.StartNew();
            var measuredElapsedTicks = 0L;
            var measuredTicks = 0;
            var droppedWarmupTicks = 0;
            var runtimeTiming = new HeadlessAutoBattleRuntimeTiming();
            var state = new ScenarioState(CreateDefaultUnits(normalized.Scale, normalized.HealthMultiplier));

            try
            {
                BootstrapUnits(state);
                TickRuntime(recordTiming: false, ref runtimeTiming);
                CacheGrantedAbilityEntities(state);
                droppedWarmupTicks++;

                var battleTicks = 0;
                var totalTicks = 1;
                var winner = HeadlessAutoBattleTeam.None;
                var victoryTick = -1;

                for (var i = 0; i < normalized.MaxTicks; i++)
                {
                    var shouldRecordTiming = droppedWarmupTicks >= WarmupRuntimeTicks;
                    if (shouldRecordTiming && !measuredWindowOpened)
                    {
                        if (normalized.CaptureOfficialToolDiff)
                        {
                            officialToolDiffCapture = GasRuntimeOfficialToolDiffCapture.Begin(GASManager.ExWorld);
                            officialToolDiffStarted = true;
                        }

                        profileHooks.BeforeMeasuredWindow?.Invoke();
                        measuredWindowOpened = true;
                    }

                    var tickStart = shouldRecordTiming ? Stopwatch.GetTimestamp() : 0L;
                    TickRuntime(shouldRecordTiming, ref runtimeTiming);
                    if (!shouldRecordTiming)
                        CacheGrantedAbilityEntities(state);
                    if (shouldRecordTiming)
                    {
                        measuredElapsedTicks += Stopwatch.GetTimestamp() - tickStart;
                        measuredTicks++;
                    }
                    else
                    {
                        droppedWarmupTicks++;
                    }

                    totalTicks++;
                    battleTicks++;

                    if (victoryTick < 0 && TryResolveWinner(state, out winner))
                    {
                        victoryTick = battleTicks;
                    }

                    if (victoryTick >= 0
                        && battleTicks - victoryTick >= normalized.PostVictoryFlushTicks
                        && HasReachedMinimumBattleSeconds(normalized, stopwatch))
                    {
                        break;
                    }
                }

                if (measuredWindowOpened)
                {
                    profileHooks.AfterMeasuredWindow?.Invoke();
                    measuredWindowClosed = true;
                }

                if (officialToolDiffStarted)
                {
                    officialToolDiff = officialToolDiffCapture.End();
                    officialToolDiffClosed = true;
                }

                if (winner == HeadlessAutoBattleTeam.None)
                    TryResolveWinner(state, out winner);

                stopwatch.Stop();
                var driverStats = GetDriverStats(state);
                return BuildResult(
                    state,
                    IsCompleted(normalized, stopwatch, winner),
                    winner,
                    normalized.Scale,
                    battleTicks,
                    totalTicks,
                    droppedWarmupTicks,
                    measuredTicks,
                    driverStats,
                    stopwatch.ElapsedTicks,
                    stopwatch.Elapsed.TotalMilliseconds,
                    measuredElapsedTicks,
                    ToMilliseconds(measuredElapsedTicks),
                    runtimeTiming,
                    officialToolDiff);
            }
            finally
            {
                if (measuredWindowOpened && !measuredWindowClosed)
                    profileHooks.AfterMeasuredWindow?.Invoke();

                if (officialToolDiffStarted && !officialToolDiffClosed)
                    officialToolDiffCapture.End();

                CleanupUnits(state);
            }
        }

        public static IEnumerator RunDefaultStepped(
            HeadlessAutoBattleOptions options,
            HeadlessAutoBattleProfileHooks profileHooks,
            Action<HeadlessAutoBattleResult> completed)
        {
            var normalized = options.Normalize();
            EnsureRuntimeInitialized();
            ResetObservationState(normalized);

            var officialToolDiffCapture = default(GasRuntimeOfficialToolDiffCapture);
            var officialToolDiffClosed = false;
            var officialToolDiffStarted = false;
            var officialToolDiff = GasRuntimeOfficialToolDiffSnapshot.Unavailable;
            var measuredWindowOpened = false;
            var measuredWindowClosed = false;
            var stopwatch = Stopwatch.StartNew();
            var measuredElapsedTicks = 0L;
            var measuredTicks = 0;
            var droppedWarmupTicks = 0;
            var runtimeTiming = new HeadlessAutoBattleRuntimeTiming();
            var state = new ScenarioState(CreateDefaultUnits(normalized.Scale, normalized.HealthMultiplier));
            var result = default(HeadlessAutoBattleResult);

            try
            {
                BootstrapUnits(state);
                TickRuntime(recordTiming: false, ref runtimeTiming);
                CacheGrantedAbilityEntities(state);
                droppedWarmupTicks++;
                yield return null;

                var battleTicks = 0;
                var totalTicks = 1;
                var winner = HeadlessAutoBattleTeam.None;
                var victoryTick = -1;

                for (var i = 0; i < normalized.MaxTicks; i++)
                {
                    var shouldRecordTiming = droppedWarmupTicks >= WarmupRuntimeTicks;
                    if (shouldRecordTiming && !measuredWindowOpened)
                    {
                        if (normalized.CaptureOfficialToolDiff)
                        {
                            officialToolDiffCapture = GasRuntimeOfficialToolDiffCapture.Begin(GASManager.ExWorld);
                            officialToolDiffStarted = true;
                        }

                        profileHooks.BeforeMeasuredWindow?.Invoke();
                        measuredWindowOpened = true;
                    }

                    var tickStart = shouldRecordTiming ? Stopwatch.GetTimestamp() : 0L;
                    TickRuntime(shouldRecordTiming, ref runtimeTiming);
                    if (!shouldRecordTiming)
                        CacheGrantedAbilityEntities(state);
                    if (shouldRecordTiming)
                    {
                        measuredElapsedTicks += Stopwatch.GetTimestamp() - tickStart;
                        measuredTicks++;
                    }
                    else
                    {
                        droppedWarmupTicks++;
                    }

                    totalTicks++;
                    battleTicks++;

                    if (victoryTick < 0 && TryResolveWinner(state, out winner))
                        victoryTick = battleTicks;

                    if (victoryTick >= 0
                        && battleTicks - victoryTick >= normalized.PostVictoryFlushTicks
                        && HasReachedMinimumBattleSeconds(normalized, stopwatch))
                    {
                        break;
                    }

                    if (victoryTick < 0 && HasReachedMinimumBattleSeconds(normalized, stopwatch))
                        break;

                    yield return null;
                }

                if (measuredWindowOpened)
                {
                    profileHooks.AfterMeasuredWindow?.Invoke();
                    measuredWindowClosed = true;
                }

                if (officialToolDiffStarted)
                {
                    officialToolDiff = officialToolDiffCapture.End();
                    officialToolDiffClosed = true;
                }

                if (winner == HeadlessAutoBattleTeam.None)
                    TryResolveWinner(state, out winner);

                stopwatch.Stop();
                var driverStats = GetDriverStats(state);
                result = BuildResult(
                    state,
                    IsCompleted(normalized, stopwatch, winner),
                    winner,
                    normalized.Scale,
                    battleTicks,
                    totalTicks,
                    droppedWarmupTicks,
                    measuredTicks,
                    driverStats,
                    stopwatch.ElapsedTicks,
                    stopwatch.Elapsed.TotalMilliseconds,
                    measuredElapsedTicks,
                    ToMilliseconds(measuredElapsedTicks),
                    runtimeTiming,
                    officialToolDiff);
            }
            finally
            {
                if (measuredWindowOpened && !measuredWindowClosed)
                    profileHooks.AfterMeasuredWindow?.Invoke();

                if (officialToolDiffStarted && !officialToolDiffClosed)
                    officialToolDiffCapture.End();

                CleanupUnits(state);
            }

            completed?.Invoke(result);
        }

        public static void ShutdownRuntime()
        {
            if (!GASManager.IsInitialized)
                return;

            AutoBattleDefinitionCatalogBuilder.Uninstall(GASManager.EntityManager);
            HeadlessAutoChessRuntimeSystemBootstrap.Reset();
            GASManager.Shutdown();
        }

        private static void EnsureRuntimeInitialized()
        {
            if (!GASManager.IsInitialized)
                GASManager.Initialize(attachToPlayerLoop: false);

            HeadlessAutoChessRuntimeSystemBootstrap.RegisterSystems(GASManager.ExWorld);
            AutoBattleDefinitionCatalogBuilder.Install(GASManager.EntityManager);
        }

        private static UnitDefinition[] CreateDefaultUnits(int scale, float healthMultiplier)
        {
            var multiplier = healthMultiplier > 0f ? healthMultiplier : 1f;
            var baseUnits = new[]
            {
                new UnitDefinition(
                    "player-knight",
                    0,
                    HeadlessAutoBattleTeam.Player,
                    0,
                    72f * multiplier,
                    8f,
                    AbilityPlayerAttack,
                    AbilityPlayerExecute,
                    44f * multiplier,
                    AutoBattleTargetPolicy.Frontline,
                    AutoBattleTargetPolicy.LowestHealth),
                new UnitDefinition(
                    "player-ranger",
                    0,
                    HeadlessAutoBattleTeam.Player,
                    1,
                    54f * multiplier,
                    8f,
                    AbilityPlayerAttack,
                    AbilityPlayerExecute,
                    44f * multiplier,
                    AutoBattleTargetPolicy.LowestHealth,
                    AutoBattleTargetPolicy.LowestHealth),
                new UnitDefinition(
                    "enemy-brute",
                    0,
                    HeadlessAutoBattleTeam.Enemy,
                    0,
                    48f * multiplier,
                    8f,
                    AbilityEnemyAttack,
                    0,
                    0f,
                    AutoBattleTargetPolicy.Frontline,
                    AutoBattleTargetPolicy.Frontline),
                new UnitDefinition(
                    "enemy-caster",
                    0,
                    HeadlessAutoBattleTeam.Enemy,
                    1,
                    42f * multiplier,
                    8f,
                    AbilityEnemyAttack,
                    0,
                    0f,
                    AutoBattleTargetPolicy.Frontline,
                    AutoBattleTargetPolicy.Frontline),
            };

            if (scale <= 1)
                return baseUnits;

            var units = new UnitDefinition[baseUnits.Length * scale];
            var index = 0;
            for (var group = 0; group < scale; group++)
            {
                for (var i = 0; i < baseUnits.Length; i++)
                {
                    units[index++] = baseUnits[i].WithScaleGroup(group);
                }
            }

            return units;
        }

        private static void BootstrapUnits(ScenarioState state)
        {
            for (var i = 0; i < state.Units.Length; i++)
            {
                var definition = state.Units[i].Definition;
                var commandGateway = ASCCommandGateway.Create();
                commandGateway.Init(
                    Array.Empty<int>(),
                    new[]
                    {
                        new AttrSetConfig(
                            AttributeSetCombat,
                            new[]
                            {
                                new AttributeBaseSetting(
                                    AttributeHealth,
                                    definition.Health,
                                    true,
                                    true,
                                    0f,
                                    definition.Health),
                                new AttributeBaseSetting(
                                    AttributeEnergy,
                                    definition.Energy,
                                    true,
                                    true,
                                    0f,
                                    definition.Energy),
                            }),
                    },
                    definition.CreateAbilityCodes(),
                    1);

                AddAutoBattleUnitComponent(commandGateway.Entity, definition);
                state.Units[i] = state.Units[i].WithCommandGateway(commandGateway);
            }

            state.DriverEntity = CreateAutoBattleDriver();
        }

        private static void AddAutoBattleUnitComponent(Entity asc, UnitDefinition definition)
        {
            var em = GASManager.EntityManager;
            if (asc == Entity.Null || !em.Exists(asc))
                return;

            em.AddComponentData(asc, new AutoBattleUnitComponent
            {
                BattleGroup = definition.BattleGroup,
                Team = definition.Team,
                Slot = definition.Slot,
                PrimaryAbilityCode = definition.PrimaryAbilityCode,
                FinisherAbilityCode = definition.FinisherAbilityCode,
                PrimaryAbilityEntity = Entity.Null,
                FinisherAbilityEntity = Entity.Null,
                HealthAttrSetCode = AttributeSetCombat,
                HealthAttrCode = AttributeHealth,
                EnergyAttrSetCode = AttributeSetCombat,
                EnergyAttrCode = AttributeEnergy,
                CooldownTagIndex = TagAttackCooldown,
                FinisherHealthThreshold = definition.FinisherHealthThreshold,
                PrimaryTargetPolicy = definition.PrimaryTargetPolicy,
                FinisherTargetPolicy = definition.FinisherTargetPolicy,
            });
        }

        private static Entity CreateAutoBattleDriver()
        {
            var em = GASManager.EntityManager;
            var driver = em.CreateEntity();
            em.SetName(driver, "AutoBattleCommandDriver");
            em.AddComponentData(driver, new AutoBattleCommandDriverComponent
            {
                Enabled = true,
                LastDecisionFrame = -1,
                LastExecutionFrame = -1,
                LastOutcomeFrame = -1,
            });
            return driver;
        }

        private static void CacheGrantedAbilityEntities(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            for (var i = 0; i < state.Units.Length; i++)
            {
                var unit = state.Units[i];
                var asc = unit.CommandGateway.Entity;
                if (asc == Entity.Null
                    || !em.Exists(asc)
                    || !em.HasComponent<AutoBattleUnitComponent>(asc)
                    || !em.HasBuffer<AbilitySlotBuffer>(asc))
                {
                    continue;
                }

                var component = em.GetComponentData<AutoBattleUnitComponent>(asc);
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

        private static void RefreshUnits(ScenarioState state)
        {
            for (var i = 0; i < state.Units.Length; i++)
                state.Units[i] = state.Units[i].Refresh();
        }

        private static bool TryResolveWinner(ScenarioState state, out HeadlessAutoBattleTeam winner)
        {
            var driverStats = GetDriverStats(state);
            if (driverStats.LastOutcomeFrame >= 0)
            {
                var playerAliveFromDriver = driverStats.PlayerAliveCount > 0;
                var enemyAliveFromDriver = driverStats.EnemyAliveCount > 0;
                if (playerAliveFromDriver && enemyAliveFromDriver)
                {
                    winner = HeadlessAutoBattleTeam.None;
                    return false;
                }

                winner = playerAliveFromDriver == enemyAliveFromDriver
                    ? HeadlessAutoBattleTeam.Draw
                    : playerAliveFromDriver
                        ? HeadlessAutoBattleTeam.Player
                        : HeadlessAutoBattleTeam.Enemy;
                return true;
            }

            var playerAlive = false;
            var enemyAlive = false;

            for (var i = 0; i < state.Units.Length; i++)
            {
                var unit = state.Units[i];
                if (!unit.Alive)
                    continue;

                if (unit.Definition.Team == HeadlessAutoBattleTeam.Player)
                    playerAlive = true;
                else if (unit.Definition.Team == HeadlessAutoBattleTeam.Enemy)
                    enemyAlive = true;
            }

            if (playerAlive && enemyAlive)
            {
                winner = HeadlessAutoBattleTeam.None;
                return false;
            }

            winner = playerAlive == enemyAlive
                ? HeadlessAutoBattleTeam.Draw
                : playerAlive
                    ? HeadlessAutoBattleTeam.Player
                    : HeadlessAutoBattleTeam.Enemy;
            return true;
        }

        private static HeadlessAutoBattleResult BuildResult(
            ScenarioState state,
            bool completed,
            HeadlessAutoBattleTeam winner,
            int scenarioScale,
            int battleTicks,
            int totalTicks,
            int warmupDroppedTicks,
            int measuredTicks,
            AutoBattleCommandDriverComponent driverStats,
            long elapsedTicks,
            double elapsedMilliseconds,
            long measuredElapsedTicks,
            double measuredElapsedMilliseconds,
            HeadlessAutoBattleRuntimeTiming runtimeTiming,
            GasRuntimeOfficialToolDiffSnapshot officialToolDiff)
        {
            RefreshUnits(state);

            var units = new HeadlessAutoBattleUnitResult[state.Units.Length];
            for (var i = 0; i < state.Units.Length; i++)
            {
                var unit = state.Units[i];
                units[i] = new HeadlessAutoBattleUnitResult(
                    unit.Definition.Id,
                    unit.Definition.Team,
                    unit.Definition.Slot,
                    unit.Health,
                    unit.Energy,
                    unit.Alive);
            }

            var em = GASManager.EntityManager;
            var log = em.GetBuffer<ReplayLogEventBuffer>(GASManager.EntityEventLogSink);
            var sinkState = em.GetComponentData<GameplayEventLogSinkComponent>(GASManager.EntityEventLogSink);
            var snapshot = GasStructuredLogExporter.CreateSnapshot(log, sinkState);
            var assertionLog = GasStructuredLogExporter.ExportToText(
                snapshot,
                GasStructuredLogFormatOptions.AssertionText);
            var runtimeDiagnostics = GasRuntimeDebugger.CreateSnapshot(em, GASManager.EntityRuntimeDebugger);
            var runtimeDiagnosticsLog = GasRuntimeDebugger.ExportToText(runtimeDiagnostics, maxEvents: 96);

            return new HeadlessAutoBattleResult(
                completed,
                winner,
                scenarioScale,
                battleTicks,
                totalTicks,
                warmupDroppedTicks,
                measuredTicks,
                driverStats.IssuedCommandCount,
                driverStats.IssuedPrimaryCommandCount,
                driverStats.IssuedFinisherCommandCount,
                driverStats.LowestHealthTargetCount,
                elapsedTicks,
                elapsedMilliseconds,
                measuredElapsedTicks,
                measuredElapsedMilliseconds,
                runtimeTiming,
                units,
                CountEvents(log, snapshot.EntryCount),
                runtimeDiagnostics,
                runtimeDiagnosticsLog,
                officialToolDiff,
                snapshot,
                assertionLog);
        }

        private static HeadlessAutoBattleEventCounts CountEvents(
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

            return new HeadlessAutoBattleEventCounts(
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

        private static void TickRuntime(bool recordTiming, ref HeadlessAutoBattleRuntimeTiming runtimeTiming)
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

        private static long UpdateTimed(ComponentSystemGroup group)
        {
            var start = Stopwatch.GetTimestamp();
            group.Update();
            return Stopwatch.GetTimestamp() - start;
        }

        private static double ToMilliseconds(long stopwatchTicks)
        {
            return stopwatchTicks * 1000d / Stopwatch.Frequency;
        }

        private static bool HasReachedMinimumBattleSeconds(
            in HeadlessAutoBattleOptions options,
            Stopwatch stopwatch)
        {
            return options.MinimumBattleSeconds <= 0d
                   || stopwatch.Elapsed.TotalSeconds >= options.MinimumBattleSeconds;
        }

        private static bool IsCompleted(
            in HeadlessAutoBattleOptions options,
            Stopwatch stopwatch,
            HeadlessAutoBattleTeam winner)
        {
            if (winner != HeadlessAutoBattleTeam.None && winner != HeadlessAutoBattleTeam.Draw)
                return true;

            return options.MinimumBattleSeconds > 0d
                   && stopwatch.Elapsed.TotalSeconds >= options.MinimumBattleSeconds;
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

        private static float GetAttribute(Entity asc, int attributeCode)
        {
            var em = GASManager.EntityManager;
            if (asc == Entity.Null || !em.Exists(asc) || !em.HasBuffer<AttributeValueBuffer>(asc))
                return 0f;

            var attributes = em.GetBuffer<AttributeValueBuffer>(asc);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttrSetCode == AttributeSetCombat && attribute.Code == attributeCode)
                    return attribute.CurrentValue;
            }

            return 0f;
        }

        private static AutoBattleCommandDriverComponent GetDriverStats(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            return state.DriverEntity != Entity.Null
                   && em.Exists(state.DriverEntity)
                   && em.HasComponent<AutoBattleCommandDriverComponent>(state.DriverEntity)
                ? em.GetComponentData<AutoBattleCommandDriverComponent>(state.DriverEntity)
                : default;
        }

        private static void ResetObservationState(in HeadlessAutoBattleOptions options)
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

        private static void ClearBuffer<T>(EntityManager em, Entity entity)
            where T : unmanaged, IBufferElementData
        {
            if (em.Exists(entity) && em.HasBuffer<T>(entity))
                em.GetBuffer<T>(entity).Clear();
        }

        private static void CleanupUnits(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            if (state.DriverEntity != Entity.Null && em.Exists(state.DriverEntity))
                em.DestroyEntity(state.DriverEntity);

            for (var i = 0; i < state.Units.Length; i++)
            {
                var asc = state.Units[i].CommandGateway.Entity;
                if (asc == Entity.Null || !em.Exists(asc))
                    continue;

                DestroyGrantedAbilities(em, asc);
                DestroyActiveEffects(em, asc);
                em.DestroyEntity(asc);
            }
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

        private readonly struct UnitDefinition
        {
            public readonly string Id;
            public readonly int BattleGroup;
            public readonly HeadlessAutoBattleTeam Team;
            public readonly int Slot;
            public readonly float Health;
            public readonly float Energy;
            public readonly int PrimaryAbilityCode;
            public readonly int FinisherAbilityCode;
            public readonly float FinisherHealthThreshold;
            public readonly AutoBattleTargetPolicy PrimaryTargetPolicy;
            public readonly AutoBattleTargetPolicy FinisherTargetPolicy;

            public UnitDefinition(
                string id,
                int battleGroup,
                HeadlessAutoBattleTeam team,
                int slot,
                float health,
                float energy,
                int primaryAbilityCode,
                int finisherAbilityCode,
                float finisherHealthThreshold,
                AutoBattleTargetPolicy primaryTargetPolicy,
                AutoBattleTargetPolicy finisherTargetPolicy)
            {
                Id = id;
                BattleGroup = battleGroup;
                Team = team;
                Slot = slot;
                Health = health;
                Energy = energy;
                PrimaryAbilityCode = primaryAbilityCode;
                FinisherAbilityCode = finisherAbilityCode;
                FinisherHealthThreshold = finisherHealthThreshold;
                PrimaryTargetPolicy = primaryTargetPolicy;
                FinisherTargetPolicy = finisherTargetPolicy;
            }

            public UnitDefinition WithScaleGroup(int battleGroup)
            {
                if (battleGroup == BattleGroup)
                    return this;

                return new UnitDefinition(
                    Id + "-g" + battleGroup,
                    battleGroup,
                    Team,
                    Slot,
                    Health,
                    Energy,
                    PrimaryAbilityCode,
                    FinisherAbilityCode,
                    FinisherHealthThreshold,
                    PrimaryTargetPolicy,
                    FinisherTargetPolicy);
            }

            public int[] CreateAbilityCodes()
            {
                return FinisherAbilityCode > 0
                    ? new[] { PrimaryAbilityCode, FinisherAbilityCode }
                    : new[] { PrimaryAbilityCode };
            }
        }

        private readonly struct UnitRuntime
        {
            public readonly UnitDefinition Definition;
            public readonly ASCCommandGateway CommandGateway;
            public readonly float Health;
            public readonly float Energy;
            public readonly bool Alive;

            public UnitRuntime(UnitDefinition definition)
                : this(definition, default, definition.Health, definition.Energy, false)
            {
            }

            private UnitRuntime(
                UnitDefinition definition,
                ASCCommandGateway commandGateway,
                float health,
                float energy,
                bool alive)
            {
                Definition = definition;
                CommandGateway = commandGateway;
                Health = health;
                Energy = energy;
                Alive = alive;
            }

            public UnitRuntime WithCommandGateway(ASCCommandGateway commandGateway)
            {
                return new UnitRuntime(Definition, commandGateway, Health, Energy, true);
            }

            public UnitRuntime Refresh()
            {
                var health = GetAttribute(CommandGateway.Entity, AttributeHealth);
                var energy = GetAttribute(CommandGateway.Entity, AttributeEnergy);
                return new UnitRuntime(Definition, CommandGateway, health, energy, health > 0f);
            }
        }

        private sealed class ScenarioState
        {
            public readonly UnitRuntime[] Units;
            public Entity DriverEntity;

            public ScenarioState(UnitDefinition[] definitions)
            {
                Units = new UnitRuntime[definitions.Length];
                for (var i = 0; i < definitions.Length; i++)
                    Units[i] = new UnitRuntime(definitions[i]);
            }
        }

    }
}
