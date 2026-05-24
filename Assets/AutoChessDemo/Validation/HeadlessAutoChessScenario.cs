using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Headless RPG auto-chess validation scenario. It has no scene, no UI, and
    /// accepts only replay/structured log facts as observable evidence.
    /// </summary>
    public static partial class HeadlessAutoChessScenario
    {
        public static HeadlessAutoChessResult RunDefault(HeadlessAutoChessOptions options = default)
        {
            return RunVariant(HeadlessAutoChessScenarioVariant.DefaultBalanced, options);
        }

        public static HeadlessAutoChessResult RunVariant(
            HeadlessAutoChessScenarioVariant variant,
            HeadlessAutoChessOptions options = default)
        {
            var normalized = options.Normalize();
            var variantDefinition = CreateRunVariantDefinition(variant, normalized.UnitScale);
            EnsureRuntimeInitialized();
            RegisterTargetCatcher();
            RegisterConfigs();
            ResetObservationState(normalized);
            ResetRuntimeDebugger(normalized);

            var simulationElapsedTicks = 0L;
            var measuredTicks = 0;
            var runtimeTiming = new HeadlessAutoChessRuntimeTickTiming();
            var systemTimingCollector = normalized.CollectSystemTimings
                ? new HeadlessAutoChessRuntimeSystemTimingCollector()
                : null;
            var state = new ScenarioState(CreateUnits(variantDefinition.Variant, normalized.UnitScale));

            try
            {
                BootstrapUnits(state);
                TickRuntimeMeasured();
                AccumulatePresentationOutbox(state);

                var battleTicks = 0;
                var totalTicks = 1;
                var winner = HeadlessAutoChessTeam.None;
                var victoryTick = -1;

                for (var i = 0; i < normalized.MaxTicks; i++)
                {
                    if (battleTicks >= PerformanceWarmupBattleTicks)
                    {
                        var tickTiming = TickRuntimeMeasured(systemTimingCollector);
                        simulationElapsedTicks += tickTiming.TotalTicks;
                        runtimeTiming.Accumulate(tickTiming);
                        RecordRuntimeDiagnostics(tickTiming);
                        measuredTicks++;
                    }
                    else
                    {
                        TickRuntimeMeasured();
                    }

                    AccumulatePresentationOutbox(state);
                    totalTicks++;
                    battleTicks++;
                    RefreshUnits(state);

                    if (TryResolveWinner(state, out var resolvedWinner))
                    {
                        if (victoryTick < 0 || winner != resolvedWinner)
                            victoryTick = battleTicks;
                        winner = resolvedWinner;
                    }
                    else
                    {
                        victoryTick = -1;
                        winner = HeadlessAutoChessTeam.None;
                    }

                    if (victoryTick >= 0 && battleTicks - victoryTick >= normalized.PostVictoryFlushTicks)
                        break;
                }

                if (winner == HeadlessAutoChessTeam.None)
                    TryResolveWinner(state, out winner);

                var systemTimings = systemTimingCollector?.ToSortedTimings() ?? Array.Empty<HeadlessAutoChessSystemTiming>();
                RecordSystemTimingDiagnostics(systemTimings);

                var simulationElapsedMilliseconds = simulationElapsedTicks * 1000d / Stopwatch.Frequency;
                return BuildResult(
                    variantDefinition,
                    state,
                    winner != HeadlessAutoChessTeam.None && winner != HeadlessAutoChessTeam.Draw,
                    winner,
                    battleTicks,
                    totalTicks,
                    measuredTicks,
                    simulationElapsedTicks,
                    simulationElapsedMilliseconds,
                    runtimeTiming,
                    systemTimings,
                    normalized);
            }
            finally
            {
                CleanupUnits(state);
                RestoreDefaultObservationOptions();
                ClearConfigProviders();
            }
        }

        private static void BootstrapUnits(ScenarioState state)
        {
            for (var i = 0; i < state.Units.Length; i++)
            {
                var definition = state.Units[i].Definition;
                var facade = AbilitySystemFacade.Create();
                facade.Init(
                    Array.Empty<int>(),
                    new[]
                    {
                            HeadlessAutoChessDefinitionSource.CreateCombatAttributeSet(
                            definition.InitialHealth,
                            definition.Mana,
                            definition.Health,
                            10f,
                            definition.ArcaneResistance),
                    },
                    definition.CreateAbilityCodes(),
                    1);

                AddAutoChessUnitComponent(facade.Entity, definition);
                RequestInitialEquipment(facade.Entity, definition);
                state.Units[i] = state.Units[i].WithFacade(facade);
            }

            state.DriverEntity = CreateAutoChessDriver();
        }

        private static void AddAutoChessUnitComponent(Entity asc, UnitDefinition definition)
        {
            var em = GASManager.EntityManager;
            if (asc == Entity.Null || !em.Exists(asc))
                return;

            em.AddComponentData(asc, new CHeadlessAutoChessUnit
            {
                Team = definition.Team,
                Slot = definition.Slot,
                BoardX = definition.BoardX,
                BoardY = definition.BoardY,
                TurnOrder = definition.TurnOrder,
                PrimaryAbilityCode = definition.PrimaryAbilityCode,
                ManaAbilityCode = definition.ManaAbilityCode,
                ControlAbilityCode = definition.ControlAbilityCode,
                SupportAbilityCode = definition.SupportAbilityCode,
                SummonAbilityCode = definition.SummonAbilityCode,
                HealthAttrSetCode = AttributeSetCombat,
                HealthAttrCode = AttributeHealth,
                ManaAttrSetCode = AttributeSetCombat,
                ManaAttrCode = AttributeMana,
                ShieldAttrSetCode = AttributeSetCombat,
                ShieldAttrCode = AttributeShield,
                ArcaneResistanceAttrSetCode = AttributeSetCombat,
                ArcaneResistanceAttrCode = AttributeArcaneResistance,
                CounterDamageAttrSetCode = AttributeSetCombat,
                CounterDamageAttrCode = AttributeCounterDamage,
                LifeStealRatioAttrSetCode = AttributeSetCombat,
                LifeStealRatioAttrCode = AttributeLifeStealRatio,
                PrimaryCooldownTagIndex = -1,
                ManaCooldownTagIndex = TagManaBurstCooldown,
                ControlCooldownTagIndex = TagPlayerStunCooldown,
                SupportCooldownTagIndex = ResolveSupportCooldownTagIndex(definition.SupportAbilityCode),
                SummonCooldownTagIndex = TagPlayerSummonCooldown,
                CrowdControlTagIndex = TagAutoChessStunned,
                ManaAbilityThreshold = definition.ManaAbilityThreshold,
                MaxActiveSummons = definition.MaxActiveSummons,
                PrimaryTargetPolicy = definition.PrimaryTargetPolicy,
                ManaTargetPolicy = definition.ManaTargetPolicy,
                ControlTargetPolicy = definition.ControlTargetPolicy,
                SupportTargetPolicy = definition.SupportTargetPolicy,
            });
            em.AddComponentData(asc, new CHeadlessAutoChessDamageState());
            em.AddComponentData(asc, new CHeadlessAutoChessDeathState());

            if (definition.SynergyCode > 0)
            {
                var synergies = em.AddBuffer<BHeadlessAutoChessSynergyMember>(asc);
                synergies.Add(new BHeadlessAutoChessSynergyMember
                {
                    SynergyCode = definition.SynergyCode,
                    Threshold = definition.SynergyThreshold,
                    AllyBuffGameplayEffectCode = definition.SynergyAllyBuffGameplayEffectCode,
                    EnemyDebuffGameplayEffectCode = definition.SynergyEnemyDebuffGameplayEffectCode,
                });
            }

            if (definition.KillManaGainGameplayEffectCode > 0
                || definition.ReviveGameplayEffectCode > 0)
            {
                em.AddComponentData(asc, new CHeadlessAutoChessPassiveRules
                {
                    KillManaGainGameplayEffectCode = definition.KillManaGainGameplayEffectCode,
                    ReviveGameplayEffectCode = definition.ReviveGameplayEffectCode,
                    MaxReviveCount = definition.MaxReviveCount,
                });
                em.AddComponentData(asc, new CHeadlessAutoChessPassiveState());
            }

            if (definition.EquipmentGameplayEffectCode > 0
                || definition.CounterDamageGameplayEffectCode > 0)
            {
                em.AddComponentData(asc, new CHeadlessAutoChessCounterRules
                {
                    EquipmentGameplayEffectCode = definition.EquipmentGameplayEffectCode,
                    CounterDamageGameplayEffectCode = definition.CounterDamageGameplayEffectCode,
                    CounterReadyTagIndex = TagAutoChessCounterReady,
                    CounterDamageAttrSetCode = AttributeSetCombat,
                    CounterDamageAttrCode = AttributeCounterDamage,
                    SetByCallerCounterDamageAmountKey = SetByCallerCounterDamageAmount,
                    HealthAttrSetCode = AttributeSetCombat,
                    HealthAttrCode = AttributeHealth,
                    MinIncomingDamage = 0.01f,
                });
                em.AddComponentData(asc, new CHeadlessAutoChessCounterState());
            }

            if (definition.SupportAbilityCode == AbilityPlayerCleanse)
            {
                em.AddComponentData(asc, new CHeadlessAutoChessCleanseRules
                {
                    CleanseAbilityCode = AbilityPlayerCleanse,
                    CleanseGameplayEffectCode = GameplayEffectPlayerCleanse,
                    RallyGameplayEffectCode = GameplayEffectPlayerCleanseRally,
                    RemovableTagIndex = TagAutoChessStunned,
                    CooldownTagIndex = TagPlayerCleanseCooldown,
                });
                em.AddComponentData(asc, new CHeadlessAutoChessCleanseState
                {
                    LastCleanseFrame = -1,
                });
            }

            if (definition.Team == HeadlessAutoChessTeam.Player)
            {
                em.AddComponentData(asc, new CHeadlessAutoChessRallyComboRules
                {
                    ComboDamageGameplayEffectCode = GameplayEffectPlayerRallyComboDamage,
                    RalliedTagIndex = TagAutoChessCleanseRallied,
                    HealthAttrSetCode = AttributeSetCombat,
                    HealthAttrCode = AttributeHealth,
                    ComboDamage = PlayerRallyComboDamageAmount,
                });
                em.AddComponentData(asc, new CHeadlessAutoChessRallyComboState
                {
                    LastComboFrame = -1,
                });

                em.AddComponentData(asc, new CHeadlessAutoChessLifeStealRules
                {
                    GearGameplayEffectCode = GameplayEffectPlayerLifeStealGear,
                    HealGameplayEffectCode = GameplayEffectPlayerLifeStealHeal,
                    ReadyTagIndex = TagAutoChessLifeStealReady,
                    LifeStealRatioAttrSetCode = AttributeSetCombat,
                    LifeStealRatioAttrCode = AttributeLifeStealRatio,
                    HealthAttrSetCode = AttributeSetCombat,
                    HealthAttrCode = AttributeHealth,
                    SetByCallerHealAmountKey = SetByCallerLifeStealHealAmount,
                    MinDamage = 0.01f,
                });
                em.AddComponentData(asc, new CHeadlessAutoChessLifeStealState
                {
                    LastLifeStealFrame = -1,
                });

                em.AddComponentData(asc, new CHeadlessAutoChessPoisonRules
                {
                    StackGameplayEffectCode = GameplayEffectPlayerPoisonStack,
                    OverflowDamageGameplayEffectCode = GameplayEffectPlayerPoisonOverflowDamage,
                    PeriodDamageGameplayEffectCode = GameplayEffectPlayerPoisonPeriodDamage,
                    StackingCode = PoisonStackingCode,
                    PoisonedTagIndex = TagAutoChessPoisoned,
                    HealthAttrSetCode = AttributeSetCombat,
                    HealthAttrCode = AttributeHealth,
                    DamageTypeCode = DamageTypePoison,
                    MinDamage = 0.01f,
                });
                em.AddComponentData(asc, new CHeadlessAutoChessPoisonState
                {
                    LastPoisonFrame = -1,
                });

                em.AddComponentData(asc, new CHeadlessAutoChessExecuteRules
                {
                    GearGameplayEffectCode = GameplayEffectPlayerExecuteGear,
                    DamageGameplayEffectCode = GameplayEffectPlayerExecuteDamage,
                    DamageTypeCode = DamageTypeExecute,
                    ReadyTagIndex = TagAutoChessExecutionReady,
                    ExecutedTagIndex = TagAutoChessExecuted,
                    HealthAttrSetCode = AttributeSetCombat,
                    HealthAttrCode = AttributeHealth,
                    HealthThreshold = PlayerExecuteHealthThreshold,
                    MinDamage = 0.01f,
                });
                em.AddComponentData(asc, new CHeadlessAutoChessExecuteState
                {
                    LastExecuteFrame = -1,
                });

                em.AddComponentData(asc, new CHeadlessAutoChessDeathBurstRules
                {
                    GearGameplayEffectCode = GameplayEffectPlayerDeathBurstGear,
                    DamageGameplayEffectCode = GameplayEffectPlayerDeathBurstDamage,
                    DamageTypeCode = DamageTypeDeathBurst,
                    ReadyTagIndex = TagAutoChessDeathBurstReady,
                    HealthAttrSetCode = AttributeSetCombat,
                    HealthAttrCode = AttributeHealth,
                    DamageAmount = PlayerDeathBurstDamageAmount,
                    MaxBoardDistance = PlayerDeathBurstMaxBoardDistance,
                });
                em.AddComponentData(asc, new CHeadlessAutoChessDeathBurstState
                {
                    LastDeathBurstFrame = -1,
                });

                em.AddComponentData(asc, new CHeadlessAutoChessEnrageRules
                {
                    EnrageGameplayEffectCode = GameplayEffectPlayerEnrage,
                    EnragedTagIndex = TagAutoChessEnraged,
                    HealthAttrSetCode = AttributeSetCombat,
                    HealthAttrCode = AttributeHealth,
                    CounterDamageAttrSetCode = AttributeSetCombat,
                    CounterDamageAttrCode = AttributeCounterDamage,
                    HealthThresholdRatio = PlayerEnrageHealthThresholdRatio,
                    CounterDamageBonus = PlayerEnrageCounterDamageBonus,
                });
                em.AddComponentData(asc, new CHeadlessAutoChessEnrageState
                {
                    LastEnrageFrame = -1,
                });
            }
        }

        private static int ResolveSupportCooldownTagIndex(int supportAbilityCode)
        {
            return supportAbilityCode == AbilityPlayerCleanse
                ? TagPlayerCleanseCooldown
                : TagPlayerBarrierCooldown;
        }

        private static void RequestInitialEquipment(Entity asc, UnitDefinition definition)
        {
            var em = GASManager.EntityManager;
            if (asc == Entity.Null || !em.Exists(asc))
                return;

            if (definition.EquipmentGameplayEffectCode > 0)
                RequestInitialGameplayEffect(
                    em,
                    asc,
                    definition.EquipmentGameplayEffectCode,
                    "AutoChessEquipment");

            if (definition.Team == HeadlessAutoChessTeam.Player)
            {
                RequestInitialGameplayEffect(
                    em,
                    asc,
                    GameplayEffectPlayerLifeStealGear,
                    "AutoChessLifeStealGear");
                RequestInitialGameplayEffect(
                    em,
                    asc,
                    GameplayEffectPlayerExecuteGear,
                    "AutoChessExecuteGear");
                RequestInitialGameplayEffect(
                    em,
                    asc,
                    GameplayEffectPlayerDeathBurstGear,
                    "AutoChessDeathBurstGear");
            }
        }

        private static void RequestInitialGameplayEffect(
            EntityManager em,
            Entity asc,
            int gameplayEffectCode,
            string namePrefix)
        {
            if (gameplayEffectCode <= 0)
                return;

            GameplayEffectLegacyBridge.ApplyLegacyInstantBypassOrCreateSingleTargetRequest(
                em,
                new CApplyGameplayEffectRequest
                {
                    SourceAsc = asc,
                    Instigator = asc,
                    Causer = asc,
                    GameplayEffectCode = gameplayEffectCode,
                    Level = 1,
                },
                asc,
                ETargetDataKind.Self,
                namePrefix);
        }

        private static Entity CreateAutoChessDriver()
        {
            var em = GASManager.EntityManager;
            var driver = em.CreateEntity();
            em.SetName(driver, "HeadlessAutoChessDriver");
            em.AddBuffer<BPresentationEvent>(driver);
            em.AddBuffer<BHeadlessAutoChessUnitDefeatedFact>(driver);
            em.AddBuffer<BHeadlessAutoChessGameplayEffectAppliedFact>(driver);
            em.AddComponentData(driver, new CHeadlessAutoChessDriver
            {
                Enabled = true,
                BoardWidth = BoardWidth,
                BoardHeight = BoardHeight,
                Round = 1,
                NextTurnOrder = 0,
                LastDecisionFrame = -1,
            });
            em.AddComponentData(driver, new CHeadlessAutoChessBattleFacts
            {
                LastDamageProjectionFrame = -1,
                LastTypedFactFrame = -1,
            });
            em.AddComponentData(driver, new CHeadlessAutoChessGameplayEffectFacts
            {
                LastProjectionFrame = -1,
            });
            em.AddComponentData(driver, new CHeadlessAutoChessPassiveReactionFacts
            {
                LastReactionFrame = -1,
            });
            em.AddComponentData(driver, new CHeadlessAutoChessSynergyFacts
            {
                LastProjectionFrame = -1,
            });
            em.AddComponentData(driver, new CHeadlessAutoChessSummonFacts
            {
                LastProjectionFrame = -1,
            });
            em.AddComponentData(driver, new CHeadlessAutoChessCounterFacts
            {
                LastProjectionFrame = -1,
            });
            em.AddComponentData(driver, new CHeadlessAutoChessCleanseFacts
            {
                LastProjectionFrame = -1,
            });
            em.AddComponentData(driver, new CHeadlessAutoChessRallyComboFacts
            {
                LastProjectionFrame = -1,
            });
            em.AddComponentData(driver, new CHeadlessAutoChessLifeStealFacts
            {
                LastProjectionFrame = -1,
            });
            em.AddComponentData(driver, new CHeadlessAutoChessPoisonFacts
            {
                LastProjectionFrame = -1,
            });
            em.AddComponentData(driver, new CHeadlessAutoChessExecuteFacts
            {
                LastProjectionFrame = -1,
            });
            em.AddComponentData(driver, new CHeadlessAutoChessDeathBurstFacts
            {
                LastProjectionFrame = -1,
            });
            em.AddComponentData(driver, new CHeadlessAutoChessEnrageFacts
            {
                LastProjectionFrame = -1,
            });
            em.AddComponentData(driver, new CHeadlessAutoChessPresentationCueMarkerFacts
            {
                LastProjectionFrame = -1,
            });
            em.AddComponentData(driver, new CHeadlessAutoChessSynergyState
            {
                LastEvaluationFrame = -1,
            });
            return driver;
        }

        private static HeadlessAutoChessResult BuildResult(
            in HeadlessAutoChessScenarioVariantDefinition variantDefinition,
            ScenarioState state,
            bool completed,
            HeadlessAutoChessTeam winner,
            int battleTicks,
            int totalTicks,
            int measuredTicks,
            long elapsedTicks,
            double elapsedMilliseconds,
            in HeadlessAutoChessRuntimeTickTiming runtimeTiming,
            HeadlessAutoChessSystemTiming[] systemTimings,
            in HeadlessAutoChessOptions normalizedOptions)
        {
            RefreshUnits(state);
            var driverStats = GetDriverStats(state);
            var battleFacts = GetBattleFacts(state);
            var passiveFacts = GetPassiveReactionFacts(state);
            var synergyFacts = GetSynergyFacts(state);
            var counterFacts = GetCounterFacts(state);
            var cleanseFacts = GetCleanseFacts(state);
            var rallyComboFacts = GetRallyComboFacts(state);
            var lifeStealFacts = GetLifeStealFacts(state);
            var poisonFacts = GetPoisonFacts(state);
            var executeFacts = GetExecuteFacts(state);
            var deathBurstFacts = GetDeathBurstFacts(state);
            var enrageFacts = GetEnrageFacts(state);
            var summonFacts = GetSummonFacts(state);

            var units = new HeadlessAutoChessUnitResult[state.Units.Length];
            for (var i = 0; i < state.Units.Length; i++)
            {
                var unit = state.Units[i];
                units[i] = new HeadlessAutoChessUnitResult(
                    unit.Definition.Id,
                    unit.Definition.Team,
                    unit.Definition.Slot,
                    unit.Definition.BoardX,
                    unit.Definition.BoardY,
                    unit.Health,
                    unit.Mana,
                    unit.Shield,
                    unit.ArcaneResistance,
                    unit.Alive);
            }

            var em = GASManager.EntityManager;
            var log = em.GetBuffer<BDebugReplayEvent>(GASManager.EntityEventLogSink);
            var sinkState = em.GetComponentData<CGameplayEventLogSink>(GASManager.EntityEventLogSink);
            var snapshot = GasStructuredLogExporter.CreateSnapshot(log, sinkState);
            var diagnosticSnapshot = GasRuntimeDebugger.CreateSnapshot(em, GASManager.EntityRuntimeDebugger);
            var assertionLog = normalizedOptions.CaptureAssertionLog
                ? GasStructuredLogExporter.ExportToText(
                    snapshot,
                    GasStructuredLogFormatOptions.AssertionText)
                : string.Empty;
            var eventCounts = CountEvents(log, snapshot.EntryCount, state.PresentationOutboxCounts);
            var validationReport = BuildValidationReport(
                variantDefinition,
                completed,
                winner,
                battleTicks,
                totalTicks,
                measuredTicks,
                elapsedMilliseconds,
                runtimeTiming,
                systemTimings,
                eventCounts,
                state.PresentationOutboxCounts,
                snapshot,
                diagnosticSnapshot,
                assertionLog,
                normalizedOptions);

            return new HeadlessAutoChessResult(
                variantDefinition.Variant,
                variantDefinition.Name,
                variantDefinition.DeterministicSeed,
                variantDefinition.PlayerUnitCount,
                variantDefinition.EnemyUnitCount,
                completed,
                winner,
                driverStats.BoardWidth,
                driverStats.BoardHeight,
                battleTicks,
                totalTicks,
                measuredTicks,
                driverStats.Round,
                driverStats.TurnCount,
                driverStats.IssuedCommandCount,
                driverStats.IssuedPrimaryCommandCount,
                driverStats.IssuedManaAbilityCommandCount,
                driverStats.IssuedControlAbilityCommandCount,
                driverStats.IssuedSupportAbilityCommandCount,
                driverStats.IssuedSummonAbilityCommandCount,
                driverStats.CrowdControlTurnSkippedCount,
                driverStats.FrontlineTargetCount,
                driverStats.LowestHealthTargetCount,
                battleFacts.PlayerDefeatedCount,
                battleFacts.EnemyDefeatedCount,
                battleFacts.FirstDefeatFrame,
                battleFacts.LastDefeatFrame,
                battleFacts.BattleResolvedFrame,
                passiveFacts.PassiveTriggeredFactCount,
                passiveFacts.KillManaGrantedFactCount,
                passiveFacts.ReviveRequestedFactCount,
                passiveFacts.ReviveAppliedFactCount,
                synergyFacts.SynergyActivatedFactCount,
                synergyFacts.SynergyExpiredFactCount,
                synergyFacts.AllyBuffRequestedFactCount,
                synergyFacts.EnemyDebuffRequestedFactCount,
                synergyFacts.PeriodicTickFactCount,
                battleFacts.ShieldAppliedFactCount,
                battleFacts.ShieldAbsorbedFactCount,
                battleFacts.ShieldBrokenFactCount,
                counterFacts.EquipmentAppliedFactCount,
                counterFacts.CounterTriggeredFactCount,
                counterFacts.CounterDamageAppliedFactCount,
                cleanseFacts.CleanseRequestedFactCount,
                cleanseFacts.CleanseAppliedFactCount,
                cleanseFacts.CleanseEffectRemovedFactCount,
                cleanseFacts.CleanseRallyRequestedFactCount,
                cleanseFacts.CleanseRallyAppliedFactCount,
                rallyComboFacts.RallyComboTriggeredFactCount,
                rallyComboFacts.RallyComboDamageAppliedFactCount,
                lifeStealFacts.LifeStealTriggeredFactCount,
                lifeStealFacts.LifeStealHealedFactCount,
                poisonFacts.PoisonStackRequestedFactCount,
                poisonFacts.PoisonStackChangedFactCount,
                poisonFacts.PoisonOverflowTriggeredFactCount,
                poisonFacts.PoisonOverflowDamageAppliedFactCount,
                poisonFacts.PoisonPeriodDamageAppliedFactCount,
                executeFacts.ExecuteTriggeredFactCount,
                executeFacts.ExecuteDamageAppliedFactCount,
                deathBurstFacts.DeathBurstTriggeredFactCount,
                deathBurstFacts.DeathBurstDamageAppliedFactCount,
                enrageFacts.EnrageTriggeredFactCount,
                enrageFacts.EnrageAppliedFactCount,
                summonFacts.SummonRequestedFactCount,
                summonFacts.SummonSpawnedFactCount,
                summonFacts.SummonExpiredFactCount,
                summonFacts.SummonDespawnedFactCount,
                elapsedTicks,
                elapsedMilliseconds,
                runtimeTiming,
                systemTimings,
                units,
                eventCounts,
                state.PresentationOutboxCounts,
                snapshot,
                assertionLog,
                validationReport);
        }

        private static HeadlessAutoChessValidationReport BuildValidationReport(
            in HeadlessAutoChessScenarioVariantDefinition variantDefinition,
            bool completed,
            HeadlessAutoChessTeam winner,
            int battleTicks,
            int totalTicks,
            int measuredTicks,
            double elapsedMilliseconds,
            in HeadlessAutoChessRuntimeTickTiming runtimeTiming,
            HeadlessAutoChessSystemTiming[] systemTimings,
            in HeadlessAutoChessEventCounts eventCounts,
            in HeadlessAutoChessPresentationOutboxCounts presentationOutboxCounts,
            in GasStructuredLogExportSnapshot snapshot,
            in GasRuntimeDiagnosticSnapshot diagnosticSnapshot,
            string assertionLog,
            in HeadlessAutoChessOptions normalizedOptions)
        {
            var thresholds = normalizedOptions.ValidationThresholds.Normalize();
            var averageTickMilliseconds = measuredTicks > 0
                ? elapsedMilliseconds / measuredTicks
                : battleTicks > 0
                    ? elapsedMilliseconds / battleTicks
                    : totalTicks > 0
                        ? elapsedMilliseconds / totalTicks
                        : 0d;
            var failures = new List<string>();

            AddFailureIf(failures, !completed, "battle did not complete");
            AddFailureIf(
                failures,
                winner != variantDefinition.ExpectedWinner,
                "winner expected " + variantDefinition.ExpectedWinner + " but was " + winner);
            AddFailureIf(failures, snapshot.CursorExpired, "structured log cursor expired");
            AddFailureIf(
                failures,
                snapshot.ReplayStats.DroppedEventCount > 0,
                "structured log dropped events " + snapshot.ReplayStats.DroppedEventCount);
            AddFailureIf(
                failures,
                snapshot.EntryCount != eventCounts.ReplayEvents,
                "structured log entries did not match replay events");
            AddFailureIf(
                failures,
                normalizedOptions.CaptureAssertionLog && string.IsNullOrEmpty(assertionLog),
                "assertion log is empty");
            AddFailureIf(
                failures,
                normalizedOptions.CaptureAssertionLog
                && !string.IsNullOrEmpty(assertionLog)
                && !assertionLog.StartsWith("stats|", StringComparison.Ordinal),
                "assertion log header is missing");

            AddMaxFailure(failures, "battleTicks", battleTicks, thresholds.MaxBattleTicks);
            AddMaxFailure(failures, "totalTicks", totalTicks, thresholds.MaxTotalTicks);
            AddMaxFailure(
                failures,
                "averageTickMilliseconds",
                averageTickMilliseconds,
                thresholds.MaxAverageTickMilliseconds);
            AddMinFailure(failures, "replayEvents", eventCounts.ReplayEvents, thresholds.MinReplayEvents);
            AddMinFailure(
                failures,
                "structuredLogEntries",
                eventCounts.StructuredLogEntries,
                thresholds.MinStructuredLogEntries);
            AddMinFailure(
                failures,
                "abilityCommitSucceeded",
                eventCounts.AbilityCommitSucceeded,
                thresholds.MinAbilityCommitSucceeded);
            AddMinFailure(
                failures,
                "gameplayEffectApplied",
                eventCounts.GameplayEffectApplied,
                thresholds.MinGameplayEffectApplied);
            AddMinFailure(
                failures,
                "attributeChanges",
                eventCounts.AttributeChanges,
                thresholds.MinAttributeChanges);
            AddMinFailure(
                failures,
                "healthDamageAttributeChanges",
                eventCounts.HealthDamageAttributeChanges,
                thresholds.MinHealthDamageAttributeChanges);
            AddMinFailure(failures, "tagChanges", eventCounts.TagChanges, thresholds.MinTagChanges);
            AddMinFailure(failures, "cueRequests", eventCounts.CueRequests, thresholds.MinCueRequests);
            AddMinFailure(failures, "damageEvents", eventCounts.DamageEvents, thresholds.MinDamageEvents);
            AddMinFailure(failures, "unitDefeated", eventCounts.UnitDefeated, thresholds.MinUnitDefeated);
            AddMinFailure(
                failures,
                "battleResolved",
                eventCounts.BattleResolved,
                thresholds.MinBattleResolved);
            AddMinFailure(
                failures,
                "passiveTriggered",
                eventCounts.PassiveTriggered,
                thresholds.MinPassiveTriggered);
            AddMinFailure(
                failures,
                "killManaGranted",
                eventCounts.KillManaGranted,
                thresholds.MinKillManaGranted);
            AddMinFailure(failures, "reviveApplied", eventCounts.ReviveApplied, thresholds.MinReviveApplied);
            AddMinFailure(
                failures,
                "synergyActivated",
                eventCounts.SynergyActivated,
                thresholds.MinSynergyActivated);
            AddMinFailure(
                failures,
                "synergyPeriodicTicks",
                eventCounts.SynergyPeriodicTicked,
                thresholds.MinSynergyPeriodicTicks);
            AddMinFailure(
                failures,
                "controlTurnSkipped",
                eventCounts.ControlTurnSkipped,
                thresholds.MinControlTurnSkipped);
            AddMinFailure(
                failures,
                "shieldApplied",
                eventCounts.ShieldApplied,
                thresholds.MinShieldApplied);
            AddMinFailure(
                failures,
                "shieldAbsorbed",
                eventCounts.ShieldAbsorbed,
                thresholds.MinShieldAbsorbed);
            AddMinFailure(
                failures,
                "shieldBroken",
                eventCounts.ShieldBroken,
                thresholds.MinShieldBroken);
            AddMinFailure(
                failures,
                "damageTypeResolved",
                eventCounts.DamageTypeResolved,
                thresholds.MinDamageTypeResolved);
            AddMinFailure(
                failures,
                "damageResisted",
                eventCounts.DamageResisted,
                thresholds.MinDamageResisted);
            AddMinFailure(
                failures,
                "equipmentApplied",
                eventCounts.EquipmentApplied,
                thresholds.MinEquipmentApplied);
            AddMinFailure(
                failures,
                "counterTriggered",
                eventCounts.CounterTriggered,
                thresholds.MinCounterTriggered);
            AddMinFailure(
                failures,
                "counterDamageApplied",
                eventCounts.CounterDamageApplied,
                thresholds.MinCounterDamageApplied);
            AddMinFailure(
                failures,
                "cleanseRequested",
                eventCounts.CleanseRequested,
                thresholds.MinCleanseRequested);
            AddMinFailure(
                failures,
                "cleanseApplied",
                eventCounts.CleanseApplied,
                thresholds.MinCleanseApplied);
            AddMinFailure(
                failures,
                "cleanseEffectRemoved",
                eventCounts.CleanseEffectRemoved,
                thresholds.MinCleanseEffectRemoved);
            AddMinFailure(
                failures,
                "cleanseRallyRequested",
                eventCounts.CleanseRallyRequested,
                thresholds.MinCleanseRallyRequested);
            AddMinFailure(
                failures,
                "cleanseRallyApplied",
                eventCounts.CleanseRallyApplied,
                thresholds.MinCleanseRallyApplied);
            AddMinFailure(
                failures,
                "rallyComboTriggered",
                eventCounts.RallyComboTriggered,
                thresholds.MinRallyComboTriggered);
            AddMinFailure(
                failures,
                "rallyComboDamageApplied",
                eventCounts.RallyComboDamageApplied,
                thresholds.MinRallyComboDamageApplied);
            AddMinFailure(
                failures,
                "lifeStealTriggered",
                eventCounts.LifeStealTriggered,
                thresholds.MinLifeStealTriggered);
            AddMinFailure(
                failures,
                "lifeStealHealed",
                eventCounts.LifeStealHealed,
                thresholds.MinLifeStealHealed);
            AddMinFailure(
                failures,
                "poisonStackRequested",
                eventCounts.PoisonStackRequested,
                thresholds.MinPoisonStackRequested);
            AddMinFailure(
                failures,
                "poisonOverflowTriggered",
                eventCounts.PoisonOverflowTriggered,
                thresholds.MinPoisonOverflowTriggered);
            AddMinFailure(
                failures,
                "poisonOverflowDamageApplied",
                eventCounts.PoisonOverflowDamageApplied,
                thresholds.MinPoisonOverflowDamageApplied);
            AddMinFailure(
                failures,
                "poisonPeriodDamageApplied",
                eventCounts.PoisonPeriodDamageApplied,
                thresholds.MinPoisonPeriodDamageApplied);
            AddMinFailure(
                failures,
                "executeTriggered",
                eventCounts.ExecuteTriggered,
                thresholds.MinExecuteTriggered);
            AddMinFailure(
                failures,
                "executeDamageApplied",
                eventCounts.ExecuteDamageApplied,
                thresholds.MinExecuteDamageApplied);
            AddMinFailure(
                failures,
                "deathBurstTriggered",
                eventCounts.DeathBurstTriggered,
                thresholds.MinDeathBurstTriggered);
            AddMinFailure(
                failures,
                "deathBurstDamageApplied",
                eventCounts.DeathBurstDamageApplied,
                thresholds.MinDeathBurstDamageApplied);
            AddMinFailure(
                failures,
                "enrageTriggered",
                eventCounts.EnrageTriggered,
                thresholds.MinEnrageTriggered);
            AddMinFailure(
                failures,
                "enrageApplied",
                eventCounts.EnrageApplied,
                thresholds.MinEnrageApplied);
            AddMinFailure(
                failures,
                "summonSpawned",
                eventCounts.SummonSpawned,
                thresholds.MinSummonSpawned);
            AddMinFailure(
                failures,
                "summonDespawned",
                eventCounts.SummonDespawned,
                thresholds.MinSummonDespawned);
            AddMinFailure(
                failures,
                "presentationUiMarkers",
                eventCounts.PresentationUiMarkers,
                thresholds.MinPresentationUiMarkers);
            AddMinFailure(
                failures,
                "presentationVfxMarkers",
                eventCounts.PresentationVfxMarkers,
                thresholds.MinPresentationVfxMarkers);
            AddMinFailure(
                failures,
                "presentationSfxMarkers",
                eventCounts.PresentationSfxMarkers,
                thresholds.MinPresentationSfxMarkers);
            AddMinFailure(
                failures,
                "presentationFloatingTextMarkers",
                eventCounts.PresentationFloatingTextMarkers,
                thresholds.MinPresentationFloatingTextMarkers);
            AddMinFailure(
                failures,
                "presentationCueMarkers",
                eventCounts.PresentationCueMarkers,
                thresholds.MinPresentationCueMarkers);
            AddMinFailure(
                failures,
                "presentationSettlementMarkers",
                eventCounts.PresentationSettlementMarkers,
                thresholds.MinPresentationSettlementMarkers);
            AddMinFailure(failures, "presentationOutboxEvents", presentationOutboxCounts.TotalEvents, 1);
            if (normalizedOptions.ProjectRawPresentationOutbox)
            {
                AddMinFailure(
                    failures,
                    "presentationOutboxCueRequests",
                    presentationOutboxCounts.CueRequests,
                    thresholds.MinCueRequests);
            }
            AddMinFailure(
                failures,
                "presentationOutboxUiMarkers",
                presentationOutboxCounts.UiMarkers,
                thresholds.MinPresentationUiMarkers);
            AddMinFailure(
                failures,
                "presentationOutboxVfxMarkers",
                presentationOutboxCounts.VfxMarkers,
                thresholds.MinPresentationVfxMarkers);
            AddMinFailure(
                failures,
                "presentationOutboxSfxMarkers",
                presentationOutboxCounts.SfxMarkers,
                thresholds.MinPresentationSfxMarkers);
            AddMinFailure(
                failures,
                "presentationOutboxFloatingTextMarkers",
                presentationOutboxCounts.FloatingTextMarkers,
                thresholds.MinPresentationFloatingTextMarkers);
            AddMinFailure(
                failures,
                "presentationOutboxCueMarkers",
                presentationOutboxCounts.CueMarkers,
                thresholds.MinPresentationCueMarkers);
            AddMinFailure(
                failures,
                "presentationOutboxSettlementMarkers",
                presentationOutboxCounts.SettlementMarkers,
                thresholds.MinPresentationSettlementMarkers);
            AddMinFailure(
                failures,
                "presentationOutboxUiHealthBarAttached",
                presentationOutboxCounts.UiHealthBarAttachedMarkers,
                1);
            AddMinFailure(
                failures,
                "presentationOutboxVfxAbilityImpact",
                presentationOutboxCounts.VfxAbilityImpactMarkers,
                1);
            AddMinFailure(
                failures,
                "presentationOutboxSfxImpact",
                presentationOutboxCounts.SfxImpactMarkers,
                1);
            AddMinFailure(
                failures,
                "presentationOutboxFloatingTextDamage",
                presentationOutboxCounts.FloatingTextDamageMarkers,
                1);
            AddMinFailure(
                failures,
                "presentationOutboxCueRequestMarker",
                presentationOutboxCounts.CueRequestPresentationMarkers,
                1);
            AddMinFailure(
                failures,
                "presentationOutboxSettlementScoreboard",
                presentationOutboxCounts.SettlementScoreboardMarkers,
                1);
            AddMinFailure(
                failures,
                "presentationOutboxUiCrowdControlSkipped",
                presentationOutboxCounts.UiCrowdControlSkippedMarkers,
                thresholds.MinControlTurnSkipped);
            AddMinFailure(
                failures,
                "presentationOutboxVfxCrowdControl",
                presentationOutboxCounts.VfxCrowdControlMarkers,
                thresholds.MinControlTurnSkipped);
            AddMinFailure(
                failures,
                "presentationOutboxSfxCrowdControl",
                presentationOutboxCounts.SfxCrowdControlMarkers,
                thresholds.MinControlTurnSkipped);
            AddMinFailure(
                failures,
                "presentationOutboxFloatingTextControl",
                presentationOutboxCounts.FloatingTextControlMarkers,
                thresholds.MinControlTurnSkipped);
            AddMinFailure(
                failures,
                "presentationOutboxCueCrowdControl",
                presentationOutboxCounts.CueCrowdControlMarkers,
                thresholds.MinControlTurnSkipped);
            AddMinFailure(
                failures,
                "presentationOutboxUiShieldChanged",
                presentationOutboxCounts.UiShieldChangedMarkers,
                thresholds.MinShieldApplied);
            AddMinFailure(
                failures,
                "presentationOutboxVfxShield",
                presentationOutboxCounts.VfxShieldMarkers,
                thresholds.MinShieldAbsorbed);
            AddMinFailure(
                failures,
                "presentationOutboxSfxShield",
                presentationOutboxCounts.SfxShieldMarkers,
                thresholds.MinShieldAbsorbed);
            AddMinFailure(
                failures,
                "presentationOutboxFloatingTextShield",
                presentationOutboxCounts.FloatingTextShieldMarkers,
                thresholds.MinShieldAbsorbed);
            AddMinFailure(
                failures,
                "presentationOutboxCueShield",
                presentationOutboxCounts.CueShieldMarkers,
                thresholds.MinShieldAbsorbed);
            AddMinFailure(
                failures,
                "presentationOutboxUiSummon",
                presentationOutboxCounts.UiSummonMarkers,
                thresholds.MinSummonSpawned);
            AddMinFailure(
                failures,
                "presentationOutboxVfxSummon",
                presentationOutboxCounts.VfxSummonMarkers,
                thresholds.MinSummonSpawned);
            AddMinFailure(
                failures,
                "presentationOutboxSfxSummon",
                presentationOutboxCounts.SfxSummonMarkers,
                thresholds.MinSummonSpawned);
            AddMinFailure(
                failures,
                "presentationOutboxFloatingTextSummon",
                presentationOutboxCounts.FloatingTextSummonMarkers,
                thresholds.MinSummonSpawned);
            AddMinFailure(
                failures,
                "presentationOutboxCueSummon",
                presentationOutboxCounts.CueSummonMarkers,
                thresholds.MinSummonSpawned);
            AddMinFailure(
                failures,
                "presentationOutboxUiResistance",
                presentationOutboxCounts.UiResistanceMarkers,
                thresholds.MinDamageResisted);
            AddMinFailure(
                failures,
                "presentationOutboxVfxResistance",
                presentationOutboxCounts.VfxResistanceMarkers,
                thresholds.MinDamageResisted);
            AddMinFailure(
                failures,
                "presentationOutboxSfxResistance",
                presentationOutboxCounts.SfxResistanceMarkers,
                thresholds.MinDamageResisted);
            AddMinFailure(
                failures,
                "presentationOutboxFloatingTextResistance",
                presentationOutboxCounts.FloatingTextResistanceMarkers,
                thresholds.MinDamageResisted);
            AddMinFailure(
                failures,
                "presentationOutboxCueResistance",
                presentationOutboxCounts.CueResistanceMarkers,
                thresholds.MinDamageResisted);
            AddMinFailure(
                failures,
                "presentationOutboxUiEquipment",
                presentationOutboxCounts.UiEquipmentMarkers,
                thresholds.MinEquipmentApplied);
            AddMinFailure(
                failures,
                "presentationOutboxUiCounter",
                presentationOutboxCounts.UiCounterMarkers,
                thresholds.MinCounterTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxVfxCounter",
                presentationOutboxCounts.VfxCounterMarkers,
                thresholds.MinCounterTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxSfxCounter",
                presentationOutboxCounts.SfxCounterMarkers,
                thresholds.MinCounterTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxFloatingTextCounter",
                presentationOutboxCounts.FloatingTextCounterMarkers,
                thresholds.MinCounterTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxCueCounter",
                presentationOutboxCounts.CueCounterMarkers,
                thresholds.MinCounterTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxUiCleanse",
                presentationOutboxCounts.UiCleanseMarkers,
                thresholds.MinCleanseApplied);
            AddMinFailure(
                failures,
                "presentationOutboxVfxCleanse",
                presentationOutboxCounts.VfxCleanseMarkers,
                thresholds.MinCleanseApplied);
            AddMinFailure(
                failures,
                "presentationOutboxSfxCleanse",
                presentationOutboxCounts.SfxCleanseMarkers,
                thresholds.MinCleanseApplied);
            AddMinFailure(
                failures,
                "presentationOutboxFloatingTextCleanse",
                presentationOutboxCounts.FloatingTextCleanseMarkers,
                thresholds.MinCleanseApplied);
            AddMinFailure(
                failures,
                "presentationOutboxCueCleanse",
                presentationOutboxCounts.CueCleanseMarkers,
                thresholds.MinCleanseApplied);
            AddMinFailure(
                failures,
                "presentationOutboxUiCleanseRally",
                presentationOutboxCounts.UiCleanseRallyMarkers,
                thresholds.MinCleanseRallyApplied);
            AddMinFailure(
                failures,
                "presentationOutboxVfxCleanseRally",
                presentationOutboxCounts.VfxCleanseRallyMarkers,
                thresholds.MinCleanseRallyApplied);
            AddMinFailure(
                failures,
                "presentationOutboxSfxCleanseRally",
                presentationOutboxCounts.SfxCleanseRallyMarkers,
                thresholds.MinCleanseRallyApplied);
            AddMinFailure(
                failures,
                "presentationOutboxFloatingTextCleanseRally",
                presentationOutboxCounts.FloatingTextCleanseRallyMarkers,
                thresholds.MinCleanseRallyApplied);
            AddMinFailure(
                failures,
                "presentationOutboxCueCleanseRally",
                presentationOutboxCounts.CueCleanseRallyMarkers,
                thresholds.MinCleanseRallyApplied);
            AddMinFailure(
                failures,
                "presentationOutboxUiRallyCombo",
                presentationOutboxCounts.UiRallyComboMarkers,
                thresholds.MinRallyComboTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxVfxRallyCombo",
                presentationOutboxCounts.VfxRallyComboMarkers,
                thresholds.MinRallyComboTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxSfxRallyCombo",
                presentationOutboxCounts.SfxRallyComboMarkers,
                thresholds.MinRallyComboTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxFloatingTextRallyCombo",
                presentationOutboxCounts.FloatingTextRallyComboMarkers,
                thresholds.MinRallyComboTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxCueRallyCombo",
                presentationOutboxCounts.CueRallyComboMarkers,
                thresholds.MinRallyComboTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxUiLifeSteal",
                presentationOutboxCounts.UiLifeStealMarkers,
                thresholds.MinLifeStealTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxVfxLifeSteal",
                presentationOutboxCounts.VfxLifeStealMarkers,
                thresholds.MinLifeStealTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxSfxLifeSteal",
                presentationOutboxCounts.SfxLifeStealMarkers,
                thresholds.MinLifeStealTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxFloatingTextLifeSteal",
                presentationOutboxCounts.FloatingTextLifeStealMarkers,
                thresholds.MinLifeStealHealed);
            AddMinFailure(
                failures,
                "presentationOutboxCueLifeSteal",
                presentationOutboxCounts.CueLifeStealMarkers,
                thresholds.MinLifeStealTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxUiPoison",
                presentationOutboxCounts.UiPoisonMarkers,
                thresholds.MinPoisonStackRequested);
            AddMinFailure(
                failures,
                "presentationOutboxVfxPoison",
                presentationOutboxCounts.VfxPoisonMarkers,
                thresholds.MinPoisonOverflowTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxSfxPoison",
                presentationOutboxCounts.SfxPoisonMarkers,
                thresholds.MinPoisonOverflowTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxFloatingTextPoison",
                presentationOutboxCounts.FloatingTextPoisonMarkers,
                thresholds.MinPoisonOverflowDamageApplied);
            AddMinFailure(
                failures,
                "presentationOutboxCuePoison",
                presentationOutboxCounts.CuePoisonMarkers,
                thresholds.MinPoisonOverflowTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxUiExecute",
                presentationOutboxCounts.UiExecuteMarkers,
                thresholds.MinExecuteTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxVfxExecute",
                presentationOutboxCounts.VfxExecuteMarkers,
                thresholds.MinExecuteTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxSfxExecute",
                presentationOutboxCounts.SfxExecuteMarkers,
                thresholds.MinExecuteTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxFloatingTextExecute",
                presentationOutboxCounts.FloatingTextExecuteMarkers,
                thresholds.MinExecuteDamageApplied);
            AddMinFailure(
                failures,
                "presentationOutboxCueExecute",
                presentationOutboxCounts.CueExecuteMarkers,
                thresholds.MinExecuteTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxUiDeathBurst",
                presentationOutboxCounts.UiDeathBurstMarkers,
                thresholds.MinDeathBurstTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxVfxDeathBurst",
                presentationOutboxCounts.VfxDeathBurstMarkers,
                thresholds.MinDeathBurstTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxSfxDeathBurst",
                presentationOutboxCounts.SfxDeathBurstMarkers,
                thresholds.MinDeathBurstTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxFloatingTextDeathBurst",
                presentationOutboxCounts.FloatingTextDeathBurstMarkers,
                thresholds.MinDeathBurstDamageApplied);
            AddMinFailure(
                failures,
                "presentationOutboxCueDeathBurst",
                presentationOutboxCounts.CueDeathBurstMarkers,
                thresholds.MinDeathBurstTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxUiEnrage",
                presentationOutboxCounts.UiEnrageMarkers,
                thresholds.MinEnrageTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxVfxEnrage",
                presentationOutboxCounts.VfxEnrageMarkers,
                thresholds.MinEnrageTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxSfxEnrage",
                presentationOutboxCounts.SfxEnrageMarkers,
                thresholds.MinEnrageTriggered);
            AddMinFailure(
                failures,
                "presentationOutboxFloatingTextEnrage",
                presentationOutboxCounts.FloatingTextEnrageMarkers,
                thresholds.MinEnrageApplied);
            AddMinFailure(
                failures,
                "presentationOutboxCueEnrage",
                presentationOutboxCounts.CueEnrageMarkers,
                thresholds.MinEnrageTriggered);

            var assertionLogFile = default(GasStructuredLogFileExportResult);
            var humanReadableLogFile = default(GasStructuredLogFileExportResult);
            var summaryPath = string.Empty;
            if (normalizedOptions.ExportLogs)
            {
                var exportDirectory = ResolveAutoChessExportDirectory(normalizedOptions.ExportDirectory);
                if (normalizedOptions.ExportTextLogs)
                {
                    if (normalizedOptions.CaptureAssertionLog)
                    {
                        assertionLogFile = GasStructuredLogExporter.WriteTextFile(
                            Path.Combine(exportDirectory, "headless-autochess.assertion.log"),
                            snapshot,
                            GasStructuredLogFormatOptions.AssertionText);
                    }

                    humanReadableLogFile = GasStructuredLogExporter.WriteTextFile(
                        Path.Combine(exportDirectory, "headless-autochess.human.log"),
                        snapshot,
                        GasStructuredLogFormatOptions.HumanReadable);
                }

                summaryPath = Path.Combine(exportDirectory, "headless-autochess.validation.txt");

                AddFailureIf(
                    failures,
                    normalizedOptions.ExportTextLogs
                    && normalizedOptions.CaptureAssertionLog
                    && assertionLogFile.ByteCount <= 0,
                    "assertion log export was empty");
                AddFailureIf(
                    failures,
                    normalizedOptions.ExportTextLogs && humanReadableLogFile.ByteCount <= 0,
                    "human readable log export was empty");
            }

            var summaryText = BuildValidationSummary(
                failures,
                variantDefinition,
                thresholds,
                completed,
                winner,
                battleTicks,
                totalTicks,
                measuredTicks,
                elapsedMilliseconds,
                averageTickMilliseconds,
                runtimeTiming,
                systemTimings,
                eventCounts,
                presentationOutboxCounts,
                snapshot,
                diagnosticSnapshot,
                assertionLogFile,
                humanReadableLogFile,
                summaryPath,
                normalizedOptions.CaptureAssertionLog,
                normalizedOptions.ExportTextLogs,
                normalizedOptions.ProjectRawPresentationOutbox);
            var summaryByteCount = 0L;
            if (normalizedOptions.ExportLogs)
            {
                var directory = Path.GetDirectoryName(summaryPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(summaryPath, summaryText, Encoding.UTF8);
                summaryByteCount = Encoding.UTF8.GetByteCount(summaryText);
            }

            return new HeadlessAutoChessValidationReport(
                failures.Count == 0,
                failures.Count,
                thresholds,
                summaryText,
                failures.ToArray(),
                assertionLogFile,
                humanReadableLogFile,
                summaryPath,
                summaryByteCount);
        }

        private static string ResolveAutoChessExportDirectory(string exportDirectory)
        {
            return string.IsNullOrWhiteSpace(exportDirectory)
                ? Path.Combine("TestResults", "AutoChess")
                : exportDirectory;
        }

        private static string BuildValidationSummary(
            List<string> failures,
            in HeadlessAutoChessScenarioVariantDefinition variantDefinition,
            in HeadlessAutoChessValidationThresholds thresholds,
            bool completed,
            HeadlessAutoChessTeam winner,
            int battleTicks,
            int totalTicks,
            int measuredTicks,
            double elapsedMilliseconds,
            double averageTickMilliseconds,
            in HeadlessAutoChessRuntimeTickTiming runtimeTiming,
            HeadlessAutoChessSystemTiming[] systemTimings,
            in HeadlessAutoChessEventCounts eventCounts,
            in HeadlessAutoChessPresentationOutboxCounts presentationOutboxCounts,
            in GasStructuredLogExportSnapshot snapshot,
            in GasRuntimeDiagnosticSnapshot diagnosticSnapshot,
            in GasStructuredLogFileExportResult assertionLogFile,
            in GasStructuredLogFileExportResult humanReadableLogFile,
            string summaryPath,
            bool captureAssertionLog,
            bool exportTextLogs,
            bool projectRawPresentationOutbox)
        {
            var builder = new StringBuilder(2048);
            builder.AppendLine("HeadlessAutoChessValidationReport");
            builder.Append("variant|name=")
                .Append(variantDefinition.Name)
                .Append("|seed=")
                .Append(variantDefinition.DeterministicSeed)
                .Append("|players=")
                .Append(variantDefinition.PlayerUnitCount)
                .Append("|enemies=")
                .Append(variantDefinition.EnemyUnitCount)
                .Append("|expectedWinner=")
                .Append(variantDefinition.ExpectedWinner)
                .AppendLine();
            builder.Append("passed=").AppendLine(failures.Count == 0 ? "true" : "false");
            builder.Append("failureCount=").AppendLine(failures.Count.ToString(CultureInfo.InvariantCulture));
            builder.Append("completed=").AppendLine(completed ? "true" : "false");
            builder.Append("winner=").AppendLine(winner.ToString());
            builder.Append("ticks|battle=")
                .Append(battleTicks)
                .Append("|total=")
                .Append(totalTicks)
                .Append("|measured=")
                .Append(measuredTicks)
                .Append("|elapsedMs=")
                .Append(FormatDouble(elapsedMilliseconds))
                .Append("|avgTickMs=")
                .Append(FormatDouble(averageTickMilliseconds))
                .AppendLine();
            builder.Append("measurement|scope=ecsRuntimeTickOnly|warmupTickExcluded=")
                .Append(PerformanceWarmupBattleTicks)
                .AppendLine("|excluded=bootstrap,presentationOutbox,validationExport");
            builder.Append("measurement|collectSystemTimings=")
                .Append(systemTimings != null && systemTimings.Length > 0 ? "true" : "false")
                .Append("|systemTimingRows=")
                .Append(systemTimings?.Length ?? 0)
                .Append("|captureAssertionLog=")
                .Append(captureAssertionLog ? "true" : "false")
                .Append("|exportTextLogs=")
                .Append(exportTextLogs ? "true" : "false")
                .Append("|projectRawPresentationOutbox=")
                .Append(projectRawPresentationOutbox ? "true" : "false")
                .AppendLine();
            builder.Append("groupTiming|ticks=")
                .Append(runtimeTiming.TickCount)
                .Append("|totalAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageTotalMilliseconds))
                .Append("|commandAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageCommandMilliseconds))
                .Append("|resetDirtyAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageResetDirtyMilliseconds))
                .Append("|tagAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageTagMilliseconds))
                .Append("|effectAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageEffectMilliseconds))
                .Append("|attributeAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageAttributeMilliseconds))
                .Append("|abilityAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageAbilityMilliseconds))
                .Append("|cueAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageCueMilliseconds))
                .AppendLine();
            AppendSystemTimingSummary(builder, systemTimings, 20);
            AppendRuntimeDiagnosticSummary(builder, diagnosticSnapshot);
            builder.Append("events|replay=")
                .Append(eventCounts.ReplayEvents)
                .Append("|structured=")
                .Append(eventCounts.StructuredLogEntries)
                .Append("|dropped=")
                .Append(snapshot.ReplayStats.DroppedEventCount)
                .Append("|cursorExpired=")
                .Append(snapshot.CursorExpired ? "true" : "false")
                .AppendLine();
            builder.Append("facts|abilityCommitSucceeded=")
                .Append(eventCounts.AbilityCommitSucceeded)
                .Append("|gameplayEffectApplied=")
                .Append(eventCounts.GameplayEffectApplied)
                .Append("|attributeChanges=")
                .Append(eventCounts.AttributeChanges)
                .Append("|healthDamageAttributeChanges=")
                .Append(eventCounts.HealthDamageAttributeChanges)
                .Append("|damageEvents=")
                .Append(eventCounts.DamageEvents)
                .Append("|unitDefeated=")
                .Append(eventCounts.UnitDefeated)
                .Append("|battleResolved=")
                .Append(eventCounts.BattleResolved)
                .AppendLine();
            builder.Append("facts2|tagChanges=")
                .Append(eventCounts.TagChanges)
                .Append("|cueRequests=")
                .Append(eventCounts.CueRequests)
                .Append("|passiveTriggered=")
                .Append(eventCounts.PassiveTriggered)
                .Append("|killManaGranted=")
                .Append(eventCounts.KillManaGranted)
                .Append("|reviveApplied=")
                .Append(eventCounts.ReviveApplied)
                .Append("|synergyActivated=")
                .Append(eventCounts.SynergyActivated)
                .Append("|synergyPeriodicTicks=")
                .Append(eventCounts.SynergyPeriodicTicked)
                .Append("|controlTurnSkipped=")
                .Append(eventCounts.ControlTurnSkipped)
                .Append("|shieldApplied=")
                .Append(eventCounts.ShieldApplied)
                .Append("|shieldAbsorbed=")
                .Append(eventCounts.ShieldAbsorbed)
                .Append("|shieldBroken=")
                .Append(eventCounts.ShieldBroken)
                .Append("|damageTypeResolved=")
                .Append(eventCounts.DamageTypeResolved)
                .Append("|damageResisted=")
                .Append(eventCounts.DamageResisted)
                .Append("|equipmentApplied=")
                .Append(eventCounts.EquipmentApplied)
                .Append("|counterTriggered=")
                .Append(eventCounts.CounterTriggered)
                .Append("|counterDamageApplied=")
                .Append(eventCounts.CounterDamageApplied)
                .Append("|cleanseRequested=")
                .Append(eventCounts.CleanseRequested)
                .Append("|cleanseApplied=")
                .Append(eventCounts.CleanseApplied)
                .Append("|cleanseEffectRemoved=")
                .Append(eventCounts.CleanseEffectRemoved)
                .Append("|cleanseRallyRequested=")
                .Append(eventCounts.CleanseRallyRequested)
                .Append("|cleanseRallyApplied=")
                .Append(eventCounts.CleanseRallyApplied)
                .Append("|rallyComboTriggered=")
                .Append(eventCounts.RallyComboTriggered)
                .Append("|rallyComboDamageApplied=")
                .Append(eventCounts.RallyComboDamageApplied)
                .Append("|lifeStealTriggered=")
                .Append(eventCounts.LifeStealTriggered)
                .Append("|lifeStealHealed=")
                .Append(eventCounts.LifeStealHealed)
                .Append("|poisonStackRequested=")
                .Append(eventCounts.PoisonStackRequested)
                .Append("|poisonStackChanged=")
                .Append(eventCounts.PoisonStackChanged)
                .Append("|poisonOverflowTriggered=")
                .Append(eventCounts.PoisonOverflowTriggered)
                .Append("|poisonOverflowDamageApplied=")
                .Append(eventCounts.PoisonOverflowDamageApplied)
                .Append("|poisonPeriodDamageApplied=")
                .Append(eventCounts.PoisonPeriodDamageApplied)
                .Append("|executeTriggered=")
                .Append(eventCounts.ExecuteTriggered)
                .Append("|executeDamageApplied=")
                .Append(eventCounts.ExecuteDamageApplied)
                .Append("|deathBurstTriggered=")
                .Append(eventCounts.DeathBurstTriggered)
                .Append("|deathBurstDamageApplied=")
                .Append(eventCounts.DeathBurstDamageApplied)
                .Append("|enrageTriggered=")
                .Append(eventCounts.EnrageTriggered)
                .Append("|enrageApplied=")
                .Append(eventCounts.EnrageApplied)
                .Append("|summonSpawned=")
                .Append(eventCounts.SummonSpawned)
                .Append("|summonExpired=")
                .Append(eventCounts.SummonExpired)
                .Append("|summonDespawned=")
                .Append(eventCounts.SummonDespawned)
                .AppendLine();
            builder.Append("presentation|uiMarkers=")
                .Append(eventCounts.PresentationUiMarkers)
                .Append("|vfxMarkers=")
                .Append(eventCounts.PresentationVfxMarkers)
                .Append("|sfxMarkers=")
                .Append(eventCounts.PresentationSfxMarkers)
                .Append("|floatingTextMarkers=")
                .Append(eventCounts.PresentationFloatingTextMarkers)
                .Append("|cueMarkers=")
                .Append(eventCounts.PresentationCueMarkers)
                .Append("|settlementMarkers=")
                .Append(eventCounts.PresentationSettlementMarkers)
                .AppendLine();
            builder.Append("presentationOutbox|events=")
                .Append(presentationOutboxCounts.TotalEvents)
                .Append("|gameplayEvents=")
                .Append(presentationOutboxCounts.GameplayEvents)
                .Append("|cueRequests=")
                .Append(presentationOutboxCounts.CueRequests)
                .Append("|uiMarkers=")
                .Append(presentationOutboxCounts.UiMarkers)
                .Append("|vfxMarkers=")
                .Append(presentationOutboxCounts.VfxMarkers)
                .Append("|sfxMarkers=")
                .Append(presentationOutboxCounts.SfxMarkers)
                .Append("|floatingTextMarkers=")
                .Append(presentationOutboxCounts.FloatingTextMarkers)
                .Append("|cueMarkers=")
                .Append(presentationOutboxCounts.CueMarkers)
                .Append("|settlementMarkers=")
                .Append(presentationOutboxCounts.SettlementMarkers)
                .Append("|totalMarkers=")
                .Append(presentationOutboxCounts.TotalMarkers)
                .AppendLine();
            builder.Append("presentationOutboxCodes|healthBarAttached=")
                .Append(presentationOutboxCounts.UiHealthBarAttachedMarkers)
                .Append("|vfxImpact=")
                .Append(presentationOutboxCounts.VfxAbilityImpactMarkers)
                .Append("|sfxImpact=")
                .Append(presentationOutboxCounts.SfxImpactMarkers)
                .Append("|damageText=")
                .Append(presentationOutboxCounts.FloatingTextDamageMarkers)
                .Append("|cueRequestMarker=")
                .Append(presentationOutboxCounts.CueRequestPresentationMarkers)
                .Append("|settlementScoreboard=")
                .Append(presentationOutboxCounts.SettlementScoreboardMarkers)
                .Append("|uiControlSkipped=")
                .Append(presentationOutboxCounts.UiCrowdControlSkippedMarkers)
                .Append("|vfxControl=")
                .Append(presentationOutboxCounts.VfxCrowdControlMarkers)
                .Append("|sfxControl=")
                .Append(presentationOutboxCounts.SfxCrowdControlMarkers)
                .Append("|controlText=")
                .Append(presentationOutboxCounts.FloatingTextControlMarkers)
                .Append("|cueControl=")
                .Append(presentationOutboxCounts.CueCrowdControlMarkers)
                .Append("|uiShield=")
                .Append(presentationOutboxCounts.UiShieldChangedMarkers)
                .Append("|vfxShield=")
                .Append(presentationOutboxCounts.VfxShieldMarkers)
                .Append("|sfxShield=")
                .Append(presentationOutboxCounts.SfxShieldMarkers)
                .Append("|shieldText=")
                .Append(presentationOutboxCounts.FloatingTextShieldMarkers)
                .Append("|cueShield=")
                .Append(presentationOutboxCounts.CueShieldMarkers)
                .Append("|uiSummon=")
                .Append(presentationOutboxCounts.UiSummonMarkers)
                .Append("|vfxSummon=")
                .Append(presentationOutboxCounts.VfxSummonMarkers)
                .Append("|sfxSummon=")
                .Append(presentationOutboxCounts.SfxSummonMarkers)
                .Append("|summonText=")
                .Append(presentationOutboxCounts.FloatingTextSummonMarkers)
                .Append("|cueSummon=")
                .Append(presentationOutboxCounts.CueSummonMarkers)
                .Append("|uiResistance=")
                .Append(presentationOutboxCounts.UiResistanceMarkers)
                .Append("|vfxResistance=")
                .Append(presentationOutboxCounts.VfxResistanceMarkers)
                .Append("|sfxResistance=")
                .Append(presentationOutboxCounts.SfxResistanceMarkers)
                .Append("|resistanceText=")
                .Append(presentationOutboxCounts.FloatingTextResistanceMarkers)
                .Append("|cueResistance=")
                .Append(presentationOutboxCounts.CueResistanceMarkers)
                .Append("|uiEquipment=")
                .Append(presentationOutboxCounts.UiEquipmentMarkers)
                .Append("|uiCounter=")
                .Append(presentationOutboxCounts.UiCounterMarkers)
                .Append("|vfxCounter=")
                .Append(presentationOutboxCounts.VfxCounterMarkers)
                .Append("|sfxCounter=")
                .Append(presentationOutboxCounts.SfxCounterMarkers)
                .Append("|counterText=")
                .Append(presentationOutboxCounts.FloatingTextCounterMarkers)
                .Append("|cueCounter=")
                .Append(presentationOutboxCounts.CueCounterMarkers)
                .Append("|uiCleanse=")
                .Append(presentationOutboxCounts.UiCleanseMarkers)
                .Append("|vfxCleanse=")
                .Append(presentationOutboxCounts.VfxCleanseMarkers)
                .Append("|sfxCleanse=")
                .Append(presentationOutboxCounts.SfxCleanseMarkers)
                .Append("|cleanseText=")
                .Append(presentationOutboxCounts.FloatingTextCleanseMarkers)
                .Append("|cueCleanse=")
                .Append(presentationOutboxCounts.CueCleanseMarkers)
                .Append("|uiCleanseRally=")
                .Append(presentationOutboxCounts.UiCleanseRallyMarkers)
                .Append("|vfxCleanseRally=")
                .Append(presentationOutboxCounts.VfxCleanseRallyMarkers)
                .Append("|sfxCleanseRally=")
                .Append(presentationOutboxCounts.SfxCleanseRallyMarkers)
                .Append("|cleanseRallyText=")
                .Append(presentationOutboxCounts.FloatingTextCleanseRallyMarkers)
                .Append("|cueCleanseRally=")
                .Append(presentationOutboxCounts.CueCleanseRallyMarkers)
                .Append("|uiRallyCombo=")
                .Append(presentationOutboxCounts.UiRallyComboMarkers)
                .Append("|vfxRallyCombo=")
                .Append(presentationOutboxCounts.VfxRallyComboMarkers)
                .Append("|sfxRallyCombo=")
                .Append(presentationOutboxCounts.SfxRallyComboMarkers)
                .Append("|rallyComboText=")
                .Append(presentationOutboxCounts.FloatingTextRallyComboMarkers)
                .Append("|cueRallyCombo=")
                .Append(presentationOutboxCounts.CueRallyComboMarkers)
                .Append("|uiLifeSteal=")
                .Append(presentationOutboxCounts.UiLifeStealMarkers)
                .Append("|vfxLifeSteal=")
                .Append(presentationOutboxCounts.VfxLifeStealMarkers)
                .Append("|sfxLifeSteal=")
                .Append(presentationOutboxCounts.SfxLifeStealMarkers)
                .Append("|lifeStealText=")
                .Append(presentationOutboxCounts.FloatingTextLifeStealMarkers)
                .Append("|cueLifeSteal=")
                .Append(presentationOutboxCounts.CueLifeStealMarkers)
                .Append("|uiPoison=")
                .Append(presentationOutboxCounts.UiPoisonMarkers)
                .Append("|vfxPoison=")
                .Append(presentationOutboxCounts.VfxPoisonMarkers)
                .Append("|sfxPoison=")
                .Append(presentationOutboxCounts.SfxPoisonMarkers)
                .Append("|poisonText=")
                .Append(presentationOutboxCounts.FloatingTextPoisonMarkers)
                .Append("|cuePoison=")
                .Append(presentationOutboxCounts.CuePoisonMarkers)
                .Append("|uiExecute=")
                .Append(presentationOutboxCounts.UiExecuteMarkers)
                .Append("|vfxExecute=")
                .Append(presentationOutboxCounts.VfxExecuteMarkers)
                .Append("|sfxExecute=")
                .Append(presentationOutboxCounts.SfxExecuteMarkers)
                .Append("|executeText=")
                .Append(presentationOutboxCounts.FloatingTextExecuteMarkers)
                .Append("|cueExecute=")
                .Append(presentationOutboxCounts.CueExecuteMarkers)
                .Append("|uiDeathBurst=")
                .Append(presentationOutboxCounts.UiDeathBurstMarkers)
                .Append("|vfxDeathBurst=")
                .Append(presentationOutboxCounts.VfxDeathBurstMarkers)
                .Append("|sfxDeathBurst=")
                .Append(presentationOutboxCounts.SfxDeathBurstMarkers)
                .Append("|deathBurstText=")
                .Append(presentationOutboxCounts.FloatingTextDeathBurstMarkers)
                .Append("|cueDeathBurst=")
                .Append(presentationOutboxCounts.CueDeathBurstMarkers)
                .Append("|uiEnrage=")
                .Append(presentationOutboxCounts.UiEnrageMarkers)
                .Append("|vfxEnrage=")
                .Append(presentationOutboxCounts.VfxEnrageMarkers)
                .Append("|sfxEnrage=")
                .Append(presentationOutboxCounts.SfxEnrageMarkers)
                .Append("|enrageText=")
                .Append(presentationOutboxCounts.FloatingTextEnrageMarkers)
                .Append("|cueEnrage=")
                .Append(presentationOutboxCounts.CueEnrageMarkers)
                .AppendLine();
            builder.Append("thresholds|maxBattleTicks=")
                .Append(thresholds.MaxBattleTicks)
                .Append("|maxTotalTicks=")
                .Append(thresholds.MaxTotalTicks)
                .Append("|maxAvgTickMs=")
                .Append(FormatDouble(thresholds.MaxAverageTickMilliseconds))
                .Append("|minReplayEvents=")
                .Append(thresholds.MinReplayEvents)
                .Append("|minStructuredLogEntries=")
                .Append(thresholds.MinStructuredLogEntries)
                .Append("|minControlTurnSkipped=")
                .Append(thresholds.MinControlTurnSkipped)
                .Append("|minShieldAbsorbed=")
                .Append(thresholds.MinShieldAbsorbed)
                .Append("|minDamageResisted=")
                .Append(thresholds.MinDamageResisted)
                .Append("|minCounterTriggered=")
                .Append(thresholds.MinCounterTriggered)
                .Append("|minCleanseApplied=")
                .Append(thresholds.MinCleanseApplied)
                .Append("|minCleanseRallyApplied=")
                .Append(thresholds.MinCleanseRallyApplied)
                .Append("|minRallyComboTriggered=")
                .Append(thresholds.MinRallyComboTriggered)
                .Append("|minLifeStealTriggered=")
                .Append(thresholds.MinLifeStealTriggered)
                .Append("|minLifeStealHealed=")
                .Append(thresholds.MinLifeStealHealed)
                .Append("|minPoisonStackRequested=")
                .Append(thresholds.MinPoisonStackRequested)
                .Append("|minPoisonOverflowTriggered=")
                .Append(thresholds.MinPoisonOverflowTriggered)
                .Append("|minPoisonOverflowDamageApplied=")
                .Append(thresholds.MinPoisonOverflowDamageApplied)
                .Append("|minPoisonPeriodDamageApplied=")
                .Append(thresholds.MinPoisonPeriodDamageApplied)
                .Append("|minExecuteTriggered=")
                .Append(thresholds.MinExecuteTriggered)
                .Append("|minExecuteDamageApplied=")
                .Append(thresholds.MinExecuteDamageApplied)
                .Append("|minDeathBurstTriggered=")
                .Append(thresholds.MinDeathBurstTriggered)
                .Append("|minDeathBurstDamageApplied=")
                .Append(thresholds.MinDeathBurstDamageApplied)
                .Append("|minEnrageTriggered=")
                .Append(thresholds.MinEnrageTriggered)
                .Append("|minEnrageApplied=")
                .Append(thresholds.MinEnrageApplied)
                .Append("|minSummonSpawned=")
                .Append(thresholds.MinSummonSpawned)
                .AppendLine();
            builder.Append("exports|assertion=")
                .Append(assertionLogFile.Path ?? string.Empty)
                .Append("|assertionBytes=")
                .Append(assertionLogFile.ByteCount)
                .Append("|human=")
                .Append(humanReadableLogFile.Path ?? string.Empty)
                .Append("|humanBytes=")
                .Append(humanReadableLogFile.ByteCount)
                .Append("|summary=")
                .Append(summaryPath ?? string.Empty)
                .AppendLine();

            for (var i = 0; i < failures.Count; i++)
            {
                builder.Append("failure|index=")
                    .Append(i)
                    .Append("|message=")
                    .AppendLine(failures[i]);
            }

            return builder.ToString();
        }

        private static void AddFailureIf(List<string> failures, bool condition, string message)
        {
            if (condition)
                failures.Add(message);
        }

        private static void AddMinFailure(List<string> failures, string name, int actual, int expected)
        {
            if (actual < expected)
            {
                failures.Add(
                    name
                    + " expected >= "
                    + expected.ToString(CultureInfo.InvariantCulture)
                    + " but was "
                    + actual.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AddMaxFailure(List<string> failures, string name, int actual, int expected)
        {
            if (actual > expected)
            {
                failures.Add(
                    name
                    + " expected <= "
                    + expected.ToString(CultureInfo.InvariantCulture)
                    + " but was "
                    + actual.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AddMaxFailure(List<string> failures, string name, double actual, double expected)
        {
            if (actual > expected)
            {
                failures.Add(
                    name
                    + " expected <= "
                    + FormatDouble(expected)
                    + " but was "
                    + FormatDouble(actual));
            }
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("G9", CultureInfo.InvariantCulture);
        }

        private static void AppendSystemTimingSummary(
            StringBuilder builder,
            HeadlessAutoChessSystemTiming[] systemTimings,
            int maxRows)
        {
            if (systemTimings == null || systemTimings.Length == 0 || maxRows <= 0)
                return;

            var totalMilliseconds = 0d;
            var totalCalls = 0;
            var sub005 = 0;
            var sub01 = 0;
            var sub02 = 0;
            var over02 = 0;
            for (var i = 0; i < systemTimings.Length; i++)
            {
                var timing = systemTimings[i];
                totalMilliseconds += timing.TotalMilliseconds;
                totalCalls += timing.CallCount;

                if (timing.AverageMilliseconds < 0.05d)
                    sub005++;
                else if (timing.AverageMilliseconds < 0.1d)
                    sub01++;
                else if (timing.AverageMilliseconds < 0.2d)
                    sub02++;
                else
                    over02++;
            }

            builder.Append("systemTimingDistribution|rows=")
                .Append(systemTimings.Length)
                .Append("|calls=")
                .Append(totalCalls)
                .Append("|totalMs=")
                .Append(FormatDouble(totalMilliseconds))
                .Append("|avgPerRowMs=")
                .Append(FormatDouble(systemTimings.Length > 0 ? totalMilliseconds / systemTimings.Length : 0d))
                .Append("|rowsAvgLt005Ms=")
                .Append(sub005)
                .Append("|rowsAvgLt01Ms=")
                .Append(sub01)
                .Append("|rowsAvgLt02Ms=")
                .Append(sub02)
                .Append("|rowsAvgGe02Ms=")
                .Append(over02)
                .AppendLine();

            var rows = systemTimings.Length < maxRows ? systemTimings.Length : maxRows;
            for (var i = 0; i < rows; i++)
            {
                var timing = systemTimings[i];
                builder.Append("systemTiming|rank=")
                    .Append(i + 1)
                    .Append("|group=")
                    .Append(timing.GroupName)
                    .Append("|system=")
                    .Append(timing.SystemName)
                    .Append("|calls=")
                    .Append(timing.CallCount)
                    .Append("|totalMs=")
                    .Append(FormatDouble(timing.TotalMilliseconds))
                    .Append("|avgMs=")
                    .Append(FormatDouble(timing.AverageMilliseconds))
                    .AppendLine();
            }
        }

        private static void AppendRuntimeDiagnosticSummary(
            StringBuilder builder,
            in GasRuntimeDiagnosticSnapshot diagnosticSnapshot)
        {
            var stats = diagnosticSnapshot.Stats;
            builder.Append("runtimeDiagnostics|events=")
                .Append(stats.RetainedEventCount)
                .Append("|dropped=")
                .Append(stats.DroppedEventCount)
                .Append("|warnings=")
                .Append(stats.WarningCount)
                .Append("|errors=")
                .Append(stats.ErrorCount)
                .Append("|slowSystems=")
                .Append(stats.SlowSystemCount)
                .Append("|bufferPressureWarnings=")
                .Append(stats.BufferPressureWarningCount)
                .AppendLine();
            AppendRuntimeCoreCounterSummary(builder, diagnosticSnapshot.CoreCounters);

            var peakTick = default(BGasRuntimeDiagnosticEvent);
            var peakBuffer = default(BGasRuntimeDiagnosticEvent);
            var hasPeakTick = false;
            var hasPeakBuffer = false;
            var coreCounters = diagnosticSnapshot.CoreCounters;
            var hasCorePeak = coreCounters.PeakActiveEffectEntityCount > 0
                              || coreCounters.PeakApplyRequestEntityCount > 0
                              || coreCounters.PeakEventBusBufferLength > 0
                              || coreCounters.PeakPresentationCursorLag > 0
                              || coreCounters.PeakReplayCursorLag > 0;
            var events = diagnosticSnapshot.Events ?? Array.Empty<BGasRuntimeDiagnosticEvent>();

            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Kind == EGasRuntimeDiagnosticKind.TickSummary
                    && (!hasPeakTick || evt.ElapsedMicroseconds > peakTick.ElapsedMicroseconds))
                {
                    peakTick = evt;
                    hasPeakTick = true;
                }

                if (evt.Kind == EGasRuntimeDiagnosticKind.BufferPressure
                    && (!hasPeakBuffer || evt.Ratio > peakBuffer.Ratio))
                {
                    peakBuffer = evt;
                    hasPeakBuffer = true;
                }
            }

            if (hasPeakTick || hasPeakBuffer || hasCorePeak)
            {
                builder.Append("runtimeDiagnosticsPeak");
                if (hasPeakTick)
                {
                    builder.Append("|tickFrame=")
                        .Append(peakTick.Frame)
                        .Append("|tickUs=")
                        .Append(peakTick.ElapsedMicroseconds);
                }

                if (hasPeakBuffer)
                {
                    builder.Append("|bufferFrame=")
                        .Append(peakBuffer.Frame)
                        .Append("|buffer=")
                        .Append(peakBuffer.BufferName)
                        .Append("|bufferCount=")
                        .Append(peakBuffer.Count)
                        .Append("|bufferCapacity=")
                        .Append(peakBuffer.Capacity)
                        .Append("|bufferRatio=")
                        .Append(FormatDouble(peakBuffer.Ratio));
                }

                if (hasCorePeak)
                {
                    builder.Append("|activeEffectEntities=")
                        .Append(coreCounters.PeakActiveEffectEntityCount)
                        .Append("|applyRequestEntities=")
                        .Append(coreCounters.PeakApplyRequestEntityCount)
                        .Append("|eventBusBufferLength=")
                        .Append(coreCounters.PeakEventBusBufferLength)
                        .Append("|presentationCursorLag=")
                        .Append(coreCounters.PeakPresentationCursorLag)
                        .Append("|replayCursorLag=")
                        .Append(coreCounters.PeakReplayCursorLag);
                }

                builder.AppendLine();
            }

            var slowRank = 1;
            for (var i = 0; i < events.Length && slowRank <= 8; i++)
            {
                var evt = events[i];
                if (evt.Kind != EGasRuntimeDiagnosticKind.SystemTiming
                    || evt.Severity < EGasRuntimeDiagnosticSeverity.Warning)
                {
                    continue;
                }

                builder.Append("runtimeSlowSystem|rank=")
                    .Append(slowRank)
                    .Append("|group=")
                    .Append(evt.GroupName)
                    .Append("|system=")
                    .Append(evt.SystemName)
                    .Append("|severity=")
                    .Append(evt.Severity)
                    .Append("|avgUs=")
                    .Append(evt.ElapsedMicroseconds)
                    .Append("|totalUs=")
                    .Append(evt.TotalMicroseconds)
                    .Append("|calls=")
                    .Append(evt.CallCount)
                    .AppendLine();
                slowRank++;
            }
        }

        private static void AppendRuntimeCoreCounterSummary(
            StringBuilder builder,
            in GasRuntimeCoreDiagnosticCounters counters)
        {
            builder.Append("runtimeCoreCounters|requests=")
                .Append(counters.RequestCount)
                .Append("|specs=")
                .Append(counters.SpecCount)
                .Append("|deltas=")
                .Append(counters.DeltaCount)
                .Append("|facts=")
                .Append(counters.FactCount)
                .Append("|cues=")
                .Append(counters.CueCount)
                .Append("|presentation=")
                .Append(counters.PresentationCount)
                .Append("|entityCreates=")
                .Append(counters.EntityCreateCount)
                .Append("|entityDestroys=")
                .Append(counters.EntityDestroyCount)
                .Append("|ecbPlaybacks=")
                .Append(counters.EcbPlaybackCount)
                .AppendLine();

            builder.Append("runtimeCoreCountersPeak|activeEffectEntities=")
                .Append(counters.PeakActiveEffectEntityCount)
                .Append("|applyRequestEntities=")
                .Append(counters.PeakApplyRequestEntityCount)
                .Append("|eventBusBufferLength=")
                .Append(counters.PeakEventBusBufferLength)
                .Append("|presentationCursorLag=")
                .Append(counters.PeakPresentationCursorLag)
                .Append("|replayCursorLag=")
                .Append(counters.PeakReplayCursorLag)
                .AppendLine();
        }

        private static HeadlessAutoChessEventCounts CountEvents(
            DynamicBuffer<BDebugReplayEvent> replayLog,
            int structuredLogEntries,
            in HeadlessAutoChessPresentationOutboxCounts presentationOutboxCounts)
        {
            var abilityCommitSucceeded = 0;
            var abilityCommitFailed = 0;
            var gameplayEffectInstanced = 0;
            var gameplayEffectApplied = 0;
            var gameplayEffectRemoved = 0;
            var attributeChanges = 0;
            var healthDamageAttributeChanges = 0;
            var tagChanges = 0;
            var cueRequests = 0;
            var damageEvents = 0;
            var unitDefeated = 0;
            var battleResolved = 0;
            var passiveTriggered = 0;
            var killManaGranted = 0;
            var reviveRequested = 0;
            var reviveApplied = 0;
            var synergyActivated = 0;
            var synergyExpired = 0;
            var synergyAllyBuffRequested = 0;
            var synergyEnemyDebuffRequested = 0;
            var synergyPeriodicTicked = 0;
            var controlTurnSkipped = 0;
            var shieldApplied = 0;
            var shieldAbsorbed = 0;
            var shieldBroken = 0;
            var damageTypeResolved = 0;
            var damageResisted = 0;
            var equipmentApplied = 0;
            var counterTriggered = 0;
            var counterDamageApplied = 0;
            var cleanseRequested = 0;
            var cleanseApplied = 0;
            var cleanseEffectRemoved = 0;
            var cleanseRallyRequested = 0;
            var cleanseRallyApplied = 0;
            var rallyComboTriggered = 0;
            var rallyComboDamageApplied = 0;
            var lifeStealTriggered = 0;
            var lifeStealHealed = 0;
            var poisonStackRequested = 0;
            var poisonStackChanged = 0;
            var poisonOverflowTriggered = 0;
            var poisonOverflowDamageApplied = 0;
            var poisonPeriodDamageApplied = 0;
            var executeTriggered = 0;
            var executeDamageApplied = 0;
            var deathBurstTriggered = 0;
            var deathBurstDamageApplied = 0;
            var enrageTriggered = 0;
            var enrageApplied = 0;
            var summonRequested = 0;
            var summonSpawned = 0;
            var summonExpired = 0;
            var summonDespawned = 0;
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
                            case EGameplayEventType.AbilityCommitFailed:
                                abilityCommitFailed++;
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
                            case EGameplayEventType.AutoChessUnitDefeated:
                                unitDefeated++;
                                break;
                            case EGameplayEventType.AutoChessBattleResolved:
                                battleResolved++;
                                break;
                            case EGameplayEventType.AutoChessPassiveTriggered:
                                passiveTriggered++;
                                break;
                            case EGameplayEventType.AutoChessKillManaGranted:
                                killManaGranted++;
                                break;
                            case EGameplayEventType.AutoChessReviveRequested:
                                reviveRequested++;
                                break;
                            case EGameplayEventType.AutoChessReviveApplied:
                                reviveApplied++;
                                break;
                            case EGameplayEventType.AutoChessSynergyActivated:
                                synergyActivated++;
                                break;
                            case EGameplayEventType.AutoChessSynergyExpired:
                                synergyExpired++;
                                break;
                            case EGameplayEventType.AutoChessSynergyAllyBuffRequested:
                                synergyAllyBuffRequested++;
                                break;
                            case EGameplayEventType.AutoChessSynergyEnemyDebuffRequested:
                                synergyEnemyDebuffRequested++;
                                break;
                            case EGameplayEventType.AutoChessSynergyPeriodTicked:
                                synergyPeriodicTicked++;
                                break;
                            case EGameplayEventType.AutoChessControlTurnSkipped:
                                controlTurnSkipped++;
                                break;
                            case EGameplayEventType.AutoChessShieldApplied:
                                shieldApplied++;
                                break;
                            case EGameplayEventType.AutoChessShieldAbsorbed:
                                shieldAbsorbed++;
                                break;
                            case EGameplayEventType.AutoChessShieldBroken:
                                shieldBroken++;
                                break;
                            case EGameplayEventType.AutoChessDamageTypeResolved:
                                damageTypeResolved++;
                                break;
                            case EGameplayEventType.AutoChessDamageResisted:
                                damageResisted++;
                                break;
                            case EGameplayEventType.AutoChessEquipmentApplied:
                                equipmentApplied++;
                                break;
                            case EGameplayEventType.AutoChessCounterTriggered:
                                counterTriggered++;
                                break;
                            case EGameplayEventType.AutoChessCounterDamageApplied:
                                counterDamageApplied++;
                                break;
                            case EGameplayEventType.AutoChessCleanseRequested:
                                cleanseRequested++;
                                break;
                            case EGameplayEventType.AutoChessCleanseApplied:
                                cleanseApplied++;
                                break;
                            case EGameplayEventType.AutoChessCleanseEffectRemoved:
                                cleanseEffectRemoved++;
                                break;
                            case EGameplayEventType.AutoChessCleanseRallyRequested:
                                cleanseRallyRequested++;
                                break;
                            case EGameplayEventType.AutoChessCleanseRallyApplied:
                                cleanseRallyApplied++;
                                break;
                            case EGameplayEventType.AutoChessRallyComboTriggered:
                                rallyComboTriggered++;
                                break;
                            case EGameplayEventType.AutoChessRallyComboDamageApplied:
                                rallyComboDamageApplied++;
                                break;
                            case EGameplayEventType.AutoChessLifeStealTriggered:
                                lifeStealTriggered++;
                                break;
                            case EGameplayEventType.AutoChessLifeStealHealed:
                                lifeStealHealed++;
                                break;
                            case EGameplayEventType.AutoChessPoisonStackRequested:
                                poisonStackRequested++;
                                break;
                            case EGameplayEventType.AutoChessPoisonStackChanged:
                                poisonStackChanged++;
                                break;
                            case EGameplayEventType.AutoChessPoisonOverflowTriggered:
                                poisonOverflowTriggered++;
                                break;
                            case EGameplayEventType.AutoChessPoisonOverflowDamageApplied:
                                poisonOverflowDamageApplied++;
                                break;
                            case EGameplayEventType.AutoChessPoisonPeriodDamageApplied:
                                poisonPeriodDamageApplied++;
                                break;
                            case EGameplayEventType.AutoChessExecuteTriggered:
                                executeTriggered++;
                                break;
                            case EGameplayEventType.AutoChessExecuteDamageApplied:
                                executeDamageApplied++;
                                break;
                            case EGameplayEventType.AutoChessDeathBurstTriggered:
                                deathBurstTriggered++;
                                break;
                            case EGameplayEventType.AutoChessDeathBurstDamageApplied:
                                deathBurstDamageApplied++;
                                break;
                            case EGameplayEventType.AutoChessEnrageTriggered:
                                enrageTriggered++;
                                break;
                            case EGameplayEventType.AutoChessEnrageApplied:
                                enrageApplied++;
                                break;
                            case EGameplayEventType.AutoChessSummonRequested:
                                summonRequested++;
                                break;
                            case EGameplayEventType.AutoChessSummonSpawned:
                                summonSpawned++;
                                break;
                            case EGameplayEventType.AutoChessSummonExpired:
                                summonExpired++;
                                break;
                            case EGameplayEventType.AutoChessSummonDespawned:
                                summonDespawned++;
                                break;
                        }
                        break;
                    case EDebugReplayEventKind.AttributeChange:
                        attributeChanges++;
                        if (evt.AttrSetCode == AttributeSetCombat
                            && evt.AttributeCode == AttributeHealth
                            && evt.NewValue < evt.OldValue)
                        {
                            healthDamageAttributeChanges++;
                        }
                        break;
                    case EDebugReplayEventKind.TagChange:
                        tagChanges++;
                        break;
                    case EDebugReplayEventKind.CueRequest:
                        cueRequests++;
                        break;
                    case EDebugReplayEventKind.Damage:
                        damageEvents++;
                        break;
                }
            }

            return new HeadlessAutoChessEventCounts(
                replayLog.Length,
                structuredLogEntries,
                abilityCommitSucceeded,
                abilityCommitFailed,
                gameplayEffectInstanced,
                gameplayEffectApplied,
                gameplayEffectRemoved,
                attributeChanges,
                healthDamageAttributeChanges,
                tagChanges,
                cueRequests,
                damageEvents,
                unitDefeated,
                battleResolved,
                passiveTriggered,
                killManaGranted,
                reviveRequested,
                reviveApplied,
                synergyActivated,
                synergyExpired,
                synergyAllyBuffRequested,
                synergyEnemyDebuffRequested,
                synergyPeriodicTicked,
                controlTurnSkipped,
                shieldApplied,
                shieldAbsorbed,
                shieldBroken,
                damageTypeResolved,
                damageResisted,
                equipmentApplied,
                counterTriggered,
                counterDamageApplied,
                cleanseRequested,
                cleanseApplied,
                cleanseEffectRemoved,
                cleanseRallyRequested,
                cleanseRallyApplied,
                rallyComboTriggered,
                rallyComboDamageApplied,
                lifeStealTriggered,
                lifeStealHealed,
                poisonStackRequested,
                poisonStackChanged,
                poisonOverflowTriggered,
                poisonOverflowDamageApplied,
                poisonPeriodDamageApplied,
                executeTriggered,
                executeDamageApplied,
                deathBurstTriggered,
                deathBurstDamageApplied,
                enrageTriggered,
                enrageApplied,
                summonRequested,
                summonSpawned,
                summonExpired,
                summonDespawned,
                presentationOutboxCounts.UiMarkers,
                presentationOutboxCounts.VfxMarkers,
                presentationOutboxCounts.SfxMarkers,
                presentationOutboxCounts.FloatingTextMarkers,
                presentationOutboxCounts.CueMarkers,
                presentationOutboxCounts.SettlementMarkers);
        }

        private static void AccumulatePresentationOutbox(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            var counts = state.PresentationOutboxCounts;

            AccumulatePresentationOutbox(em, state.DriverEntity, ref counts);
            for (var i = 0; i < state.Units.Length; i++)
                AccumulatePresentationOutbox(em, state.Units[i].Facade.Entity, ref counts);

            state.PresentationOutboxCounts = counts;
        }

        private static void AccumulatePresentationOutbox(
            EntityManager em,
            Entity entity,
            ref HeadlessAutoChessPresentationOutboxCounts counts)
        {
            if (entity == Entity.Null || !em.Exists(entity) || !em.HasBuffer<BPresentationEvent>(entity))
                return;

            var events = em.GetBuffer<BPresentationEvent>(entity);
            for (var i = 0; i < events.Length; i++)
                counts.Accumulate(events[i]);
        }

        private static CHeadlessAutoChessDriver GetDriverStats(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            return state.DriverEntity != Entity.Null
                   && em.Exists(state.DriverEntity)
                   && em.HasComponent<CHeadlessAutoChessDriver>(state.DriverEntity)
                ? em.GetComponentData<CHeadlessAutoChessDriver>(state.DriverEntity)
                : default;
        }

        private static CHeadlessAutoChessBattleFacts GetBattleFacts(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            return state.DriverEntity != Entity.Null
                   && em.Exists(state.DriverEntity)
                   && em.HasComponent<CHeadlessAutoChessBattleFacts>(state.DriverEntity)
                ? em.GetComponentData<CHeadlessAutoChessBattleFacts>(state.DriverEntity)
                : default;
        }

        private static CHeadlessAutoChessPassiveReactionFacts GetPassiveReactionFacts(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            return state.DriverEntity != Entity.Null
                   && em.Exists(state.DriverEntity)
                   && em.HasComponent<CHeadlessAutoChessPassiveReactionFacts>(state.DriverEntity)
                ? em.GetComponentData<CHeadlessAutoChessPassiveReactionFacts>(state.DriverEntity)
                : default;
        }

        private static CHeadlessAutoChessSynergyFacts GetSynergyFacts(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            return state.DriverEntity != Entity.Null
                   && em.Exists(state.DriverEntity)
                   && em.HasComponent<CHeadlessAutoChessSynergyFacts>(state.DriverEntity)
                ? em.GetComponentData<CHeadlessAutoChessSynergyFacts>(state.DriverEntity)
                : default;
        }

        private static CHeadlessAutoChessCounterFacts GetCounterFacts(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            return state.DriverEntity != Entity.Null
                   && em.Exists(state.DriverEntity)
                   && em.HasComponent<CHeadlessAutoChessCounterFacts>(state.DriverEntity)
                ? em.GetComponentData<CHeadlessAutoChessCounterFacts>(state.DriverEntity)
                : default;
        }

        private static CHeadlessAutoChessCleanseFacts GetCleanseFacts(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            return state.DriverEntity != Entity.Null
                   && em.Exists(state.DriverEntity)
                   && em.HasComponent<CHeadlessAutoChessCleanseFacts>(state.DriverEntity)
                ? em.GetComponentData<CHeadlessAutoChessCleanseFacts>(state.DriverEntity)
                : default;
        }

        private static CHeadlessAutoChessRallyComboFacts GetRallyComboFacts(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            return state.DriverEntity != Entity.Null
                   && em.Exists(state.DriverEntity)
                   && em.HasComponent<CHeadlessAutoChessRallyComboFacts>(state.DriverEntity)
                ? em.GetComponentData<CHeadlessAutoChessRallyComboFacts>(state.DriverEntity)
                : default;
        }

        private static CHeadlessAutoChessLifeStealFacts GetLifeStealFacts(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            return state.DriverEntity != Entity.Null
                   && em.Exists(state.DriverEntity)
                   && em.HasComponent<CHeadlessAutoChessLifeStealFacts>(state.DriverEntity)
                ? em.GetComponentData<CHeadlessAutoChessLifeStealFacts>(state.DriverEntity)
                : default;
        }

        private static CHeadlessAutoChessPoisonFacts GetPoisonFacts(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            return state.DriverEntity != Entity.Null
                   && em.Exists(state.DriverEntity)
                   && em.HasComponent<CHeadlessAutoChessPoisonFacts>(state.DriverEntity)
                ? em.GetComponentData<CHeadlessAutoChessPoisonFacts>(state.DriverEntity)
                : default;
        }

        private static CHeadlessAutoChessExecuteFacts GetExecuteFacts(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            return state.DriverEntity != Entity.Null
                   && em.Exists(state.DriverEntity)
                   && em.HasComponent<CHeadlessAutoChessExecuteFacts>(state.DriverEntity)
                ? em.GetComponentData<CHeadlessAutoChessExecuteFacts>(state.DriverEntity)
                : default;
        }

        private static CHeadlessAutoChessDeathBurstFacts GetDeathBurstFacts(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            return state.DriverEntity != Entity.Null
                   && em.Exists(state.DriverEntity)
                   && em.HasComponent<CHeadlessAutoChessDeathBurstFacts>(state.DriverEntity)
                ? em.GetComponentData<CHeadlessAutoChessDeathBurstFacts>(state.DriverEntity)
                : default;
        }

        private static CHeadlessAutoChessEnrageFacts GetEnrageFacts(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            return state.DriverEntity != Entity.Null
                   && em.Exists(state.DriverEntity)
                   && em.HasComponent<CHeadlessAutoChessEnrageFacts>(state.DriverEntity)
                ? em.GetComponentData<CHeadlessAutoChessEnrageFacts>(state.DriverEntity)
                : default;
        }

        private static CHeadlessAutoChessSummonFacts GetSummonFacts(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            return state.DriverEntity != Entity.Null
                   && em.Exists(state.DriverEntity)
                   && em.HasComponent<CHeadlessAutoChessSummonFacts>(state.DriverEntity)
                ? em.GetComponentData<CHeadlessAutoChessSummonFacts>(state.DriverEntity)
                : default;
        }

    }

}
