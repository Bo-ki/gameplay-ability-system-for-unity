using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
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

        public HeadlessAutoBattleOptions(int maxTicks, int postVictoryFlushTicks)
        {
            MaxTicks = maxTicks;
            PostVictoryFlushTicks = postVictoryFlushTicks;
        }

        public HeadlessAutoBattleOptions Normalize()
        {
            return new HeadlessAutoBattleOptions(
                MaxTicks > 0 ? MaxTicks : 64,
                PostVictoryFlushTicks >= 0 ? PostVictoryFlushTicks : 4);
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

    public readonly struct HeadlessAutoBattleResult
    {
        public readonly bool Completed;
        public readonly HeadlessAutoBattleTeam Winner;
        public readonly int BattleTicks;
        public readonly int TotalTicks;
        public readonly int DriverIssuedCommands;
        public readonly int DriverIssuedPrimaryCommands;
        public readonly int DriverIssuedFinisherCommands;
        public readonly int DriverLowestHealthTargetSelections;
        public readonly long ElapsedTicks;
        public readonly double ElapsedMilliseconds;
        public readonly double AverageTickMilliseconds;
        public readonly HeadlessAutoBattleUnitResult[] Units;
        public readonly HeadlessAutoBattleEventCounts EventCounts;
        public readonly GasStructuredLogExportSnapshot StructuredLogSnapshot;
        public readonly string AssertionLog;

        public HeadlessAutoBattleResult(
            bool completed,
            HeadlessAutoBattleTeam winner,
            int battleTicks,
            int totalTicks,
            int driverIssuedCommands,
            int driverIssuedPrimaryCommands,
            int driverIssuedFinisherCommands,
            int driverLowestHealthTargetSelections,
            long elapsedTicks,
            double elapsedMilliseconds,
            HeadlessAutoBattleUnitResult[] units,
            HeadlessAutoBattleEventCounts eventCounts,
            GasStructuredLogExportSnapshot structuredLogSnapshot,
            string assertionLog)
        {
            Completed = completed;
            Winner = winner;
            BattleTicks = battleTicks;
            TotalTicks = totalTicks;
            DriverIssuedCommands = driverIssuedCommands;
            DriverIssuedPrimaryCommands = driverIssuedPrimaryCommands;
            DriverIssuedFinisherCommands = driverIssuedFinisherCommands;
            DriverLowestHealthTargetSelections = driverLowestHealthTargetSelections;
            ElapsedTicks = elapsedTicks;
            ElapsedMilliseconds = elapsedMilliseconds;
            AverageTickMilliseconds = totalTicks > 0 ? elapsedMilliseconds / totalTicks : 0d;
            Units = units ?? Array.Empty<HeadlessAutoBattleUnitResult>();
            EventCounts = eventCounts;
            StructuredLogSnapshot = structuredLogSnapshot;
            AssertionLog = assertionLog ?? string.Empty;
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

        public const int GameplayEffectPlayerBurn = 9201;
        public const int GameplayEffectEnemyBurn = 9202;
        public const int GameplayEffectPlayerPeriodDamage = 9203;
        public const int GameplayEffectEnemyPeriodDamage = 9204;
        public const int GameplayEffectAttackCost = 9205;
        public const int GameplayEffectAttackCooldown = 9206;
        public const int GameplayEffectPlayerExecute = 9207;

        public const int ExecutionCalculationExecuteDamage = 9401;
        public const int ExecutionCalculationExecuteDamageOutput = 9402;

        public const int TagAbilityAttacking = 0;
        public const int TagAttackCooldown = 1;
        public const int TagBurning = 2;

        private const string TargetCatcherName = "HeadlessAutoBattle.Target";

        public static HeadlessAutoBattleResult RunDefault(HeadlessAutoBattleOptions options = default)
        {
            var normalized = options.Normalize();
            EnsureRuntimeInitialized();
            RegisterTargetCatcher();
            RegisterConfigs();
            ResetObservationState();

            var stopwatch = Stopwatch.StartNew();
            var state = new ScenarioState(CreateDefaultUnits());

            try
            {
                BootstrapUnits(state);
                TickRuntime();

                var battleTicks = 0;
                var totalTicks = 1;
                var winner = HeadlessAutoBattleTeam.None;
                var victoryTick = -1;

                for (var i = 0; i < normalized.MaxTicks; i++)
                {
                    TickRuntime();
                    totalTicks++;
                    battleTicks++;
                    RefreshUnits(state);

                    if (victoryTick < 0 && TryResolveWinner(state, out winner))
                    {
                        victoryTick = battleTicks;
                    }

                    if (victoryTick >= 0 && battleTicks - victoryTick >= normalized.PostVictoryFlushTicks)
                        break;
                }

                if (winner == HeadlessAutoBattleTeam.None)
                    TryResolveWinner(state, out winner);

                stopwatch.Stop();
                var driverStats = GetDriverStats(state);
                return BuildResult(
                    state,
                    winner != HeadlessAutoBattleTeam.None && winner != HeadlessAutoBattleTeam.Draw,
                    winner,
                    battleTicks,
                    totalTicks,
                    driverStats,
                    stopwatch.ElapsedTicks,
                    stopwatch.Elapsed.TotalMilliseconds);
            }
            finally
            {
                CleanupUnits(state);
                ClearConfigProviders();
            }
        }

        private static void EnsureRuntimeInitialized()
        {
            if (!GASManager.IsInitialized)
                GASManager.Initialize();
        }

        private static void RegisterTargetCatcher()
        {
            TargetCatcherHelper.RegisterTargetCatcher(
                TargetCatcherName,
                typeof(CatchTarget),
                typeof(XParamNone));
        }

        private static void RegisterConfigs()
        {
            AbilityConfigRegistry.RegisterGetConfigByIDFunc(CreateAbilityConfig);
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(CreateGameplayEffectConfig);
            GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(_ => null);
        }

        private static void ClearConfigProviders()
        {
            AbilityConfigRegistry.RegisterGetConfigByIDFunc(null);
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
            GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(null);
        }

        private static AbilityConfig CreateAbilityConfig(int abilityCode)
        {
            return abilityCode switch
            {
                AbilityPlayerAttack => CreateAttackAbility(
                    AbilityPlayerAttack,
                    GameplayEffectPlayerBurn),
                AbilityEnemyAttack => CreateAttackAbility(
                    AbilityEnemyAttack,
                    GameplayEffectEnemyBurn),
                AbilityPlayerExecute => CreateAttackAbility(
                    AbilityPlayerExecute,
                    GameplayEffectPlayerExecute),
                _ => null,
            };
        }

        private static AbilityConfig CreateAttackAbility(int abilityCode, int targetEffectCode)
        {
            return new AbilityConfig(new AbilityComponentConfig[]
            {
                new ConfAbilityBaseInfo
                {
                    Code = abilityCode,
                    Level = 1,
                },
                new DirectMaskAbilityTagsConfig(new[] { TagAbilityAttacking }),
                new ConfAbilityCost
                {
                    GameplayEffectCode = GameplayEffectAttackCost,
                },
                new ConfAbilityCooldown
                {
                    GameplayEffectCode = GameplayEffectAttackCooldown,
                    Cooldown = 2,
                },
                new ConfAbilityTargetEffectsOnActivate
                {
                    EffectCodes = new[] { targetEffectCode },
                    AutoEndOnCommit = true,
                },
            });
        }

        private static GameplayEffectConfig CreateGameplayEffectConfig(int gameplayEffectCode)
        {
            return gameplayEffectCode switch
            {
                GameplayEffectPlayerBurn => CreateBurnEffect(
                    "PlayerBurn",
                    GameplayEffectPlayerPeriodDamage),
                GameplayEffectEnemyBurn => CreateBurnEffect(
                    "EnemyBurn",
                    GameplayEffectEnemyPeriodDamage),
                GameplayEffectPlayerPeriodDamage => CreateInstantDamageEffect("PlayerPeriodDamage", 12f),
                GameplayEffectEnemyPeriodDamage => CreateInstantDamageEffect("EnemyPeriodDamage", 8f),
                GameplayEffectAttackCost => CreateCostEffect(),
                GameplayEffectAttackCooldown => CreateCooldownEffect(),
                GameplayEffectPlayerExecute => CreateExecuteEffect(),
                _ => null,
            };
        }

        private static GameplayEffectConfig CreateBurnEffect(string name, int periodDamageEffectCode)
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfEffectBasicInfo
                {
                    Name = name,
                },
                new ConfDuration
                {
                    duration = 3,
                    timeUnit = TimeUnit.Frame,
                    ResetStartTimeWhenActivated = true,
                    StopTickWhenDeactivated = false,
                },
                new ConfPeriod
                {
                    Period = 1,
                    ResetTimeCountWhenDeactivated = false,
                    GameplayEffectCodes = new[] { periodDamageEffectCode },
                },
                new DirectMaskGrantedTagsConfig(new[] { TagBurning }),
                new ConfCueOnApply
                {
                    cues = new[]
                    {
                        new GameplayCueConfig(typeof(HeadlessAutoBattleNoopCue), new XParamNone()),
                    },
                },
            });
        }

        private static GameplayEffectConfig CreateInstantDamageEffect(string name, float damage)
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfEffectBasicInfo
                {
                    Name = name,
                },
                new ConfModifierConfig
                {
                    ModifierSettings = new[]
                    {
                        new ModifierDefinitionSetting
                        {
                            AttrSetCode = AttributeSetCombat,
                            AttrCode = AttributeHealth,
                            Operation = EModifierOp.Subtract,
                            Magnitude = damage,
                        },
                    },
                },
            });
        }

        private static GameplayEffectConfig CreateExecuteEffect()
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfEffectBasicInfo
                {
                    Name = "PlayerExecute",
                },
                new DirectExecuteCalculationConfig(
                    ExecutionCalculationExecuteDamage,
                    ExecutionCalculationExecuteDamageOutput,
                    baseDamage: 10f,
                    missingHealthCoefficient: 0.5f,
                    minDamage: 10f,
                    maxDamage: 36f),
            });
        }

        private static GameplayEffectConfig CreateCostEffect()
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfEffectBasicInfo
                {
                    Name = "AttackCost",
                },
                new ConfModifierConfig
                {
                    ModifierSettings = new[]
                    {
                        new ModifierDefinitionSetting
                        {
                            AttrSetCode = AttributeSetCombat,
                            AttrCode = AttributeEnergy,
                            Operation = EModifierOp.Subtract,
                            Magnitude = 1f,
                        },
                    },
                },
            });
        }

        private static GameplayEffectConfig CreateCooldownEffect()
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfEffectBasicInfo
                {
                    Name = "AttackCooldown",
                },
                new ConfDuration
                {
                    duration = 2,
                    timeUnit = TimeUnit.Frame,
                    ResetStartTimeWhenActivated = true,
                    StopTickWhenDeactivated = false,
                },
                new DirectMaskGrantedTagsConfig(new[] { TagAttackCooldown }),
            });
        }

        private static UnitDefinition[] CreateDefaultUnits()
        {
            return new[]
            {
                new UnitDefinition(
                    "player-knight",
                    HeadlessAutoBattleTeam.Player,
                    0,
                    72f,
                    8f,
                    AbilityPlayerAttack,
                    AbilityPlayerExecute,
                    32f,
                    HeadlessAutoBattleTargetPolicy.Frontline,
                    HeadlessAutoBattleTargetPolicy.LowestHealth),
                new UnitDefinition(
                    "player-ranger",
                    HeadlessAutoBattleTeam.Player,
                    1,
                    54f,
                    8f,
                    AbilityPlayerAttack,
                    AbilityPlayerExecute,
                    32f,
                    HeadlessAutoBattleTargetPolicy.LowestHealth,
                    HeadlessAutoBattleTargetPolicy.LowestHealth),
                new UnitDefinition(
                    "enemy-brute",
                    HeadlessAutoBattleTeam.Enemy,
                    0,
                    48f,
                    8f,
                    AbilityEnemyAttack,
                    0,
                    0f,
                    HeadlessAutoBattleTargetPolicy.Frontline,
                    HeadlessAutoBattleTargetPolicy.Frontline),
                new UnitDefinition(
                    "enemy-caster",
                    HeadlessAutoBattleTeam.Enemy,
                    1,
                    42f,
                    8f,
                    AbilityEnemyAttack,
                    0,
                    0f,
                    HeadlessAutoBattleTargetPolicy.Frontline,
                    HeadlessAutoBattleTargetPolicy.Frontline),
            };
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

            em.AddComponentData(asc, new CHeadlessAutoBattleUnit
            {
                Team = definition.Team,
                Slot = definition.Slot,
                PrimaryAbilityCode = definition.PrimaryAbilityCode,
                FinisherAbilityCode = definition.FinisherAbilityCode,
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
            em.SetName(driver, "HeadlessAutoBattleDriver");
            em.AddComponentData(driver, new CHeadlessAutoBattleDriver
            {
                Enabled = true,
                LastDecisionFrame = -1,
            });
            return driver;
        }

        private static void RefreshUnits(ScenarioState state)
        {
            for (var i = 0; i < state.Units.Length; i++)
                state.Units[i] = state.Units[i].Refresh();
        }

        private static bool TryResolveWinner(ScenarioState state, out HeadlessAutoBattleTeam winner)
        {
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
            int battleTicks,
            int totalTicks,
            CHeadlessAutoBattleDriver driverStats,
            long elapsedTicks,
            double elapsedMilliseconds)
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

            return new HeadlessAutoBattleResult(
                completed,
                winner,
                battleTicks,
                totalTicks,
                driverStats.IssuedCommandCount,
                driverStats.IssuedPrimaryCommandCount,
                driverStats.IssuedFinisherCommandCount,
                driverStats.LowestHealthTargetCount,
                elapsedTicks,
                elapsedMilliseconds,
                units,
                CountEvents(log, snapshot.EntryCount),
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

        private static void TickRuntime()
        {
            var world = GASManager.ExWorld;
            world.GetExistingSystemManaged<GASFramePrepareSystemGroup>().Update();
            world.GetExistingSystemManaged<GASCommandResolveSystemGroup>().Update();
            world.GetExistingSystemManaged<GASCoreSimulationSystemGroup>().Update();
            world.GetExistingSystemManaged<GASStructuralCommitSystemGroup>().Update();
            world.GetExistingSystemManaged<GASBoundaryProjectionSystemGroup>().Update();
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

        private static CHeadlessAutoBattleDriver GetDriverStats(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            return state.DriverEntity != Entity.Null
                   && em.Exists(state.DriverEntity)
                   && em.HasComponent<CHeadlessAutoBattleDriver>(state.DriverEntity)
                ? em.GetComponentData<CHeadlessAutoBattleDriver>(state.DriverEntity)
                : default;
        }

        private static TagMaskComponent CreateDenseMask(IEnumerable<int> tagIndices)
        {
            var mask = new TagMaskComponent();
            if (tagIndices == null)
                return mask;

            foreach (var tagIndex in tagIndices)
                mask.AddTag(tagIndex);

            return mask;
        }

        private static void ResetObservationState()
        {
            var em = GASManager.EntityManager;
            if (em.Exists(GASManager.EntityGlobalTimer))
                em.SetComponentData(GASManager.EntityGlobalTimer, new GlobalTimer());

            if (em.Exists(GASManager.EntityEventBus))
            {
                em.SetComponentData(GASManager.EntityEventBus, new GameplayEventBusComponent());
                if (em.HasComponent<PresentationOutboxProjectionStateComponent>(GASManager.EntityEventBus))
                    em.SetComponentData(GASManager.EntityEventBus, new PresentationOutboxProjectionStateComponent());
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
            public readonly HeadlessAutoBattleTeam Team;
            public readonly int Slot;
            public readonly float Health;
            public readonly float Energy;
            public readonly int PrimaryAbilityCode;
            public readonly int FinisherAbilityCode;
            public readonly float FinisherHealthThreshold;
            public readonly HeadlessAutoBattleTargetPolicy PrimaryTargetPolicy;
            public readonly HeadlessAutoBattleTargetPolicy FinisherTargetPolicy;

            public UnitDefinition(
                string id,
                HeadlessAutoBattleTeam team,
                int slot,
                float health,
                float energy,
                int primaryAbilityCode,
                int finisherAbilityCode,
                float finisherHealthThreshold,
                HeadlessAutoBattleTargetPolicy primaryTargetPolicy,
                HeadlessAutoBattleTargetPolicy finisherTargetPolicy)
            {
                Id = id;
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

        private sealed class DirectMaskAbilityTagsConfig : AbilityComponentConfig
        {
            private readonly TagMaskComponent _tags;

            public DirectMaskAbilityTagsConfig(IEnumerable<int> tagIndices)
            {
                _tags = CreateDenseMask(tagIndices);
            }

            public override void LoadToGameplayAbilityEntity(Entity ability)
            {
                _entityManager.AddComponentData(ability, new AbilityActivationOwnedTagsComponent
                {
                    Tags = _tags,
                });
            }
        }

        private sealed class DirectMaskGrantedTagsConfig : GameplayEffectComponentConfig
        {
            private readonly TagMaskComponent _tags;

            public DirectMaskGrantedTagsConfig(IEnumerable<int> tagIndices)
            {
                _tags = CreateDenseMask(tagIndices);
            }

            public override void LoadToGameplayEffectEntity(Entity ge)
            {
                _entityManager.AddComponentData(ge, new GEGrantedTagsComponent
                {
                    Tags = _tags,
                });
            }
        }

        private sealed class DirectExecuteCalculationConfig : GameplayEffectComponentConfig
        {
            private readonly int _calculationCode;
            private readonly int _outputKey;
            private readonly float _baseDamage;
            private readonly float _missingHealthCoefficient;
            private readonly float _minDamage;
            private readonly float _maxDamage;

            public DirectExecuteCalculationConfig(
                int calculationCode,
                int outputKey,
                float baseDamage,
                float missingHealthCoefficient,
                float minDamage,
                float maxDamage)
            {
                _calculationCode = calculationCode;
                _outputKey = outputKey;
                _baseDamage = baseDamage;
                _missingHealthCoefficient = missingHealthCoefficient;
                _minDamage = minDamage;
                _maxDamage = maxDamage;
            }

            public override void LoadToGameplayEffectEntity(Entity ge)
            {
                _entityManager.AddComponentData(ge, new CHeadlessAutoBattleExecuteCalculation
                {
                    CalculationCode = _calculationCode,
                    OutputKey = _outputKey,
                    HealthAttrSetCode = AttributeSetCombat,
                    HealthAttrCode = AttributeHealth,
                    BaseDamage = _baseDamage,
                    MissingHealthCoefficient = _missingHealthCoefficient,
                    MinDamage = _minDamage,
                    MaxDamage = _maxDamage,
                });

                var definitions = _entityManager.HasBuffer<GEExecutionCalculationOutputModifierDefinitionBuffer>(ge)
                    ? _entityManager.GetBuffer<GEExecutionCalculationOutputModifierDefinitionBuffer>(ge)
                    : _entityManager.AddBuffer<GEExecutionCalculationOutputModifierDefinitionBuffer>(ge);
                definitions.Add(new GEExecutionCalculationOutputModifierDefinitionBuffer
                {
                    CalculationCode = _calculationCode,
                    OutputKey = _outputKey,
                    AttrSetCode = AttributeSetCombat,
                    AttributeCode = AttributeHealth,
                    Op = EModifierOp.Subtract,
                    FallbackMagnitude = _baseDamage,
                    Coefficient = 1f,
                });
            }
        }
    }

    public sealed class HeadlessAutoBattleNoopCue : GameplayCueBase<XParamNone>
    {
    }
}
