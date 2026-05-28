using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public readonly struct HeadlessAutoChessOptions
    {
        public readonly int MaxTicks;
        public readonly int PostVictoryFlushTicks;
        public readonly bool ExportLogs;
        public readonly string ExportDirectory;
        public readonly HeadlessAutoChessValidationThresholds ValidationThresholds;
        public readonly bool CollectSystemTimings;
        public readonly int UnitScale;
        public readonly bool SuppressAssertionLog;
        public readonly bool SuppressTextLogExports;
        public readonly bool ProjectRawPresentationOutbox;

        public bool CaptureAssertionLog => !SuppressAssertionLog;
        public bool ExportTextLogs => !SuppressTextLogExports;

        public HeadlessAutoChessOptions(
            int maxTicks,
            int postVictoryFlushTicks,
            bool exportLogs = false,
            string exportDirectory = null,
            HeadlessAutoChessValidationThresholds validationThresholds = default,
            bool collectSystemTimings = false,
            int unitScale = 1,
            bool captureAssertionLog = true,
            bool exportTextLogs = true,
            bool projectRawPresentationOutbox = false)
        {
            MaxTicks = maxTicks;
            PostVictoryFlushTicks = postVictoryFlushTicks;
            ExportLogs = exportLogs;
            ExportDirectory = exportDirectory;
            ValidationThresholds = validationThresholds;
            CollectSystemTimings = collectSystemTimings;
            UnitScale = unitScale;
            SuppressAssertionLog = !captureAssertionLog;
            SuppressTextLogExports = !exportTextLogs;
            ProjectRawPresentationOutbox = projectRawPresentationOutbox;
        }

        public HeadlessAutoChessOptions Normalize()
        {
            return new HeadlessAutoChessOptions(
                MaxTicks > 0 ? MaxTicks : 128,
                PostVictoryFlushTicks >= 0 ? PostVictoryFlushTicks : 4,
                ExportLogs,
                ExportDirectory,
                ValidationThresholds.Normalize(),
                CollectSystemTimings,
                UnitScale > 0 ? UnitScale : 1,
                CaptureAssertionLog,
                ExportTextLogs,
                ProjectRawPresentationOutbox);
        }
    }

    public struct HeadlessAutoChessValidationThresholds
    {
        public int MaxBattleTicks;
        public int MaxTotalTicks;
        public double MaxAverageTickMilliseconds;
        public int MinReplayEvents;
        public int MinStructuredLogEntries;
        public int MinAbilityCommitSucceeded;
        public int MinGameplayEffectApplied;
        public int MinAttributeChanges;
        public int MinHealthDamageAttributeChanges;
        public int MinTagChanges;
        public int MinCueRequests;
        public int MinDamageEvents;
        public int MinUnitDefeated;
        public int MinBattleResolved;
        public int MinPassiveTriggered;
        public int MinKillManaGranted;
        public int MinReviveApplied;
        public int MinSynergyActivated;
        public int MinSynergyPeriodicTicks;
        public int MinControlTurnSkipped;
        public int MinShieldApplied;
        public int MinShieldAbsorbed;
        public int MinShieldBroken;
        public int MinDamageTypeResolved;
        public int MinDamageResisted;
        public int MinEquipmentApplied;
        public int MinCounterTriggered;
        public int MinCounterDamageApplied;
        public int MinCleanseRequested;
        public int MinCleanseApplied;
        public int MinCleanseEffectRemoved;
        public int MinCleanseRallyRequested;
        public int MinCleanseRallyApplied;
        public int MinRallyComboTriggered;
        public int MinRallyComboDamageApplied;
        public int MinLifeStealTriggered;
        public int MinLifeStealHealed;
        public int MinPoisonStackRequested;
        public int MinPoisonOverflowTriggered;
        public int MinPoisonOverflowDamageApplied;
        public int MinPoisonPeriodDamageApplied;
        public int MinExecuteTriggered;
        public int MinExecuteDamageApplied;
        public int MinDeathBurstTriggered;
        public int MinDeathBurstDamageApplied;
        public int MinEnrageTriggered;
        public int MinEnrageApplied;
        public int MinSummonSpawned;
        public int MinSummonDespawned;
        public int MinPresentationUiMarkers;
        public int MinPresentationVfxMarkers;
        public int MinPresentationSfxMarkers;
        public int MinPresentationFloatingTextMarkers;
        public int MinPresentationCueMarkers;
        public int MinPresentationSettlementMarkers;

        public static HeadlessAutoChessValidationThresholds Default =>
            new HeadlessAutoChessValidationThresholds
            {
                MaxBattleTicks = 128,
                MaxTotalTicks = 132,
                MaxAverageTickMilliseconds = 25d,
                MinReplayEvents = 50,
                MinStructuredLogEntries = 50,
                MinAbilityCommitSucceeded = 4,
                MinGameplayEffectApplied = 8,
                MinAttributeChanges = 8,
                MinHealthDamageAttributeChanges = 4,
                MinTagChanges = 1,
                MinCueRequests = 1,
                MinDamageEvents = 4,
                MinUnitDefeated = 4,
                MinBattleResolved = 1,
                MinPassiveTriggered = 2,
                MinKillManaGranted = 1,
                MinReviveApplied = 1,
                MinSynergyActivated = 1,
                MinSynergyPeriodicTicks = 1,
                MinControlTurnSkipped = 1,
                MinShieldApplied = 1,
                MinShieldAbsorbed = 1,
                MinShieldBroken = 1,
                MinDamageTypeResolved = 1,
                MinDamageResisted = 1,
                MinEquipmentApplied = 1,
                MinCounterTriggered = 1,
                MinCounterDamageApplied = 1,
                MinCleanseRequested = 1,
                MinCleanseApplied = 1,
                MinCleanseEffectRemoved = 1,
                MinCleanseRallyRequested = 1,
                MinCleanseRallyApplied = 1,
                MinRallyComboTriggered = 1,
                MinRallyComboDamageApplied = 1,
                MinLifeStealTriggered = 1,
                MinLifeStealHealed = 1,
                MinPoisonStackRequested = 1,
                MinPoisonOverflowTriggered = 1,
                MinPoisonOverflowDamageApplied = 1,
                MinPoisonPeriodDamageApplied = 1,
                MinExecuteTriggered = 1,
                MinExecuteDamageApplied = 1,
                MinDeathBurstTriggered = 1,
                MinDeathBurstDamageApplied = 1,
                MinEnrageTriggered = 1,
                MinEnrageApplied = 1,
                MinSummonSpawned = 1,
                MinSummonDespawned = 1,
                MinPresentationUiMarkers = 1,
                MinPresentationVfxMarkers = 1,
                MinPresentationSfxMarkers = 1,
                MinPresentationFloatingTextMarkers = 1,
                MinPresentationCueMarkers = 1,
                MinPresentationSettlementMarkers = 1,
            };

        public HeadlessAutoChessValidationThresholds Normalize()
        {
            var defaults = Default;
            return new HeadlessAutoChessValidationThresholds
            {
                MaxBattleTicks = PickPositive(MaxBattleTicks, defaults.MaxBattleTicks),
                MaxTotalTicks = PickPositive(MaxTotalTicks, defaults.MaxTotalTicks),
                MaxAverageTickMilliseconds = PickPositive(MaxAverageTickMilliseconds, defaults.MaxAverageTickMilliseconds),
                MinReplayEvents = PickPositive(MinReplayEvents, defaults.MinReplayEvents),
                MinStructuredLogEntries = PickPositive(MinStructuredLogEntries, defaults.MinStructuredLogEntries),
                MinAbilityCommitSucceeded = PickPositive(MinAbilityCommitSucceeded, defaults.MinAbilityCommitSucceeded),
                MinGameplayEffectApplied = PickPositive(MinGameplayEffectApplied, defaults.MinGameplayEffectApplied),
                MinAttributeChanges = PickPositive(MinAttributeChanges, defaults.MinAttributeChanges),
                MinHealthDamageAttributeChanges = PickPositive(MinHealthDamageAttributeChanges, defaults.MinHealthDamageAttributeChanges),
                MinTagChanges = PickPositive(MinTagChanges, defaults.MinTagChanges),
                MinCueRequests = PickPositive(MinCueRequests, defaults.MinCueRequests),
                MinDamageEvents = PickPositive(MinDamageEvents, defaults.MinDamageEvents),
                MinUnitDefeated = PickPositive(MinUnitDefeated, defaults.MinUnitDefeated),
                MinBattleResolved = PickPositive(MinBattleResolved, defaults.MinBattleResolved),
                MinPassiveTriggered = PickPositive(MinPassiveTriggered, defaults.MinPassiveTriggered),
                MinKillManaGranted = PickPositive(MinKillManaGranted, defaults.MinKillManaGranted),
                MinReviveApplied = PickPositive(MinReviveApplied, defaults.MinReviveApplied),
                MinSynergyActivated = PickPositive(MinSynergyActivated, defaults.MinSynergyActivated),
                MinSynergyPeriodicTicks = PickPositive(MinSynergyPeriodicTicks, defaults.MinSynergyPeriodicTicks),
                MinControlTurnSkipped = PickPositive(MinControlTurnSkipped, defaults.MinControlTurnSkipped),
                MinShieldApplied = PickPositive(MinShieldApplied, defaults.MinShieldApplied),
                MinShieldAbsorbed = PickPositive(MinShieldAbsorbed, defaults.MinShieldAbsorbed),
                MinShieldBroken = PickPositive(MinShieldBroken, defaults.MinShieldBroken),
                MinDamageTypeResolved = PickPositive(MinDamageTypeResolved, defaults.MinDamageTypeResolved),
                MinDamageResisted = PickPositive(MinDamageResisted, defaults.MinDamageResisted),
                MinEquipmentApplied = PickPositive(MinEquipmentApplied, defaults.MinEquipmentApplied),
                MinCounterTriggered = PickPositive(MinCounterTriggered, defaults.MinCounterTriggered),
                MinCounterDamageApplied = PickPositive(MinCounterDamageApplied, defaults.MinCounterDamageApplied),
                MinCleanseRequested = PickPositive(MinCleanseRequested, defaults.MinCleanseRequested),
                MinCleanseApplied = PickPositive(MinCleanseApplied, defaults.MinCleanseApplied),
                MinCleanseEffectRemoved = PickPositive(
                    MinCleanseEffectRemoved,
                    defaults.MinCleanseEffectRemoved),
                MinCleanseRallyRequested = PickPositive(
                    MinCleanseRallyRequested,
                    defaults.MinCleanseRallyRequested),
                MinCleanseRallyApplied = PickPositive(
                    MinCleanseRallyApplied,
                    defaults.MinCleanseRallyApplied),
                MinRallyComboTriggered = PickPositive(
                    MinRallyComboTriggered,
                    defaults.MinRallyComboTriggered),
                MinRallyComboDamageApplied = PickPositive(
                    MinRallyComboDamageApplied,
                    defaults.MinRallyComboDamageApplied),
                MinLifeStealTriggered = PickPositive(
                    MinLifeStealTriggered,
                    defaults.MinLifeStealTriggered),
                MinLifeStealHealed = PickPositive(
                    MinLifeStealHealed,
                    defaults.MinLifeStealHealed),
                MinPoisonStackRequested = PickPositive(
                    MinPoisonStackRequested,
                    defaults.MinPoisonStackRequested),
                MinPoisonOverflowTriggered = PickPositive(
                    MinPoisonOverflowTriggered,
                    defaults.MinPoisonOverflowTriggered),
                MinPoisonOverflowDamageApplied = PickPositive(
                    MinPoisonOverflowDamageApplied,
                    defaults.MinPoisonOverflowDamageApplied),
                MinPoisonPeriodDamageApplied = PickPositive(
                    MinPoisonPeriodDamageApplied,
                    defaults.MinPoisonPeriodDamageApplied),
                MinExecuteTriggered = PickPositive(
                    MinExecuteTriggered,
                    defaults.MinExecuteTriggered),
                MinExecuteDamageApplied = PickPositive(
                    MinExecuteDamageApplied,
                    defaults.MinExecuteDamageApplied),
                MinDeathBurstTriggered = PickPositive(
                    MinDeathBurstTriggered,
                    defaults.MinDeathBurstTriggered),
                MinDeathBurstDamageApplied = PickPositive(
                    MinDeathBurstDamageApplied,
                    defaults.MinDeathBurstDamageApplied),
                MinEnrageTriggered = PickPositive(
                    MinEnrageTriggered,
                    defaults.MinEnrageTriggered),
                MinEnrageApplied = PickPositive(
                    MinEnrageApplied,
                    defaults.MinEnrageApplied),
                MinSummonSpawned = PickPositive(MinSummonSpawned, defaults.MinSummonSpawned),
                MinSummonDespawned = PickPositive(MinSummonDespawned, defaults.MinSummonDespawned),
                MinPresentationUiMarkers = PickPositive(MinPresentationUiMarkers, defaults.MinPresentationUiMarkers),
                MinPresentationVfxMarkers = PickPositive(MinPresentationVfxMarkers, defaults.MinPresentationVfxMarkers),
                MinPresentationSfxMarkers = PickPositive(MinPresentationSfxMarkers, defaults.MinPresentationSfxMarkers),
                MinPresentationFloatingTextMarkers = PickPositive(
                    MinPresentationFloatingTextMarkers,
                    defaults.MinPresentationFloatingTextMarkers),
                MinPresentationCueMarkers = PickPositive(
                    MinPresentationCueMarkers,
                    defaults.MinPresentationCueMarkers),
                MinPresentationSettlementMarkers = PickPositive(
                    MinPresentationSettlementMarkers,
                    defaults.MinPresentationSettlementMarkers),
            };
        }

        private static int PickPositive(int value, int fallback)
        {
            return value > 0 ? value : fallback;
        }

        private static double PickPositive(double value, double fallback)
        {
            return value > 0d ? value : fallback;
        }
    }

    public readonly struct HeadlessAutoChessScenarioVariantDefinition
    {
        public readonly HeadlessAutoChessScenarioVariant Variant;
        public readonly string Name;
        public readonly int DeterministicSeed;
        public readonly int PlayerUnitCount;
        public readonly int EnemyUnitCount;
        public readonly HeadlessAutoChessTeam ExpectedWinner;

        public HeadlessAutoChessScenarioVariantDefinition(
            HeadlessAutoChessScenarioVariant variant,
            string name,
            int deterministicSeed,
            int playerUnitCount,
            int enemyUnitCount,
            HeadlessAutoChessTeam expectedWinner)
        {
            Variant = variant;
            Name = name ?? string.Empty;
            DeterministicSeed = deterministicSeed;
            PlayerUnitCount = playerUnitCount;
            EnemyUnitCount = enemyUnitCount;
            ExpectedWinner = expectedWinner;
        }
    }

    public readonly struct HeadlessAutoChessUnitResult
    {
        public readonly string Id;
        public readonly HeadlessAutoChessTeam Team;
        public readonly int Slot;
        public readonly int BoardX;
        public readonly int BoardY;
        public readonly float Health;
        public readonly float Mana;
        public readonly float Shield;
        public readonly float ArcaneResistance;
        public readonly bool Alive;

        public HeadlessAutoChessUnitResult(
            string id,
            HeadlessAutoChessTeam team,
            int slot,
            int boardX,
            int boardY,
            float health,
            float mana,
            float shield,
            float arcaneResistance,
            bool alive)
        {
            Id = id;
            Team = team;
            Slot = slot;
            BoardX = boardX;
            BoardY = boardY;
            Health = health;
            Mana = mana;
            Shield = shield;
            ArcaneResistance = arcaneResistance;
            Alive = alive;
        }
    }

    public readonly struct HeadlessAutoChessEventCounts
    {
        public readonly int ReplayEvents;
        public readonly int StructuredLogEntries;
        public readonly int AbilityCommitSucceeded;
        public readonly int AbilityCommitFailed;
        public readonly int GameplayEffectInstanced;
        public readonly int GameplayEffectApplied;
        public readonly int GameplayEffectRemoved;
        public readonly int AttributeChanges;
        public readonly int HealthDamageAttributeChanges;
        public readonly int TagChanges;
        public readonly int CueRequests;
        public readonly int DamageEvents;
        public readonly int UnitDefeated;
        public readonly int BattleResolved;
        public readonly int PassiveTriggered;
        public readonly int KillManaGranted;
        public readonly int ReviveRequested;
        public readonly int ReviveApplied;
        public readonly int SynergyActivated;
        public readonly int SynergyExpired;
        public readonly int SynergyAllyBuffRequested;
        public readonly int SynergyEnemyDebuffRequested;
        public readonly int SynergyPeriodicTicked;
        public readonly int ControlTurnSkipped;
        public readonly int ShieldApplied;
        public readonly int ShieldAbsorbed;
        public readonly int ShieldBroken;
        public readonly int DamageTypeResolved;
        public readonly int DamageResisted;
        public readonly int EquipmentApplied;
        public readonly int CounterTriggered;
        public readonly int CounterDamageApplied;
        public readonly int CleanseRequested;
        public readonly int CleanseApplied;
        public readonly int CleanseEffectRemoved;
        public readonly int CleanseRallyRequested;
        public readonly int CleanseRallyApplied;
        public readonly int RallyComboTriggered;
        public readonly int RallyComboDamageApplied;
        public readonly int LifeStealTriggered;
        public readonly int LifeStealHealed;
        public readonly int PoisonStackRequested;
        public readonly int PoisonStackChanged;
        public readonly int PoisonOverflowTriggered;
        public readonly int PoisonOverflowDamageApplied;
        public readonly int PoisonPeriodDamageApplied;
        public readonly int ExecuteTriggered;
        public readonly int ExecuteDamageApplied;
        public readonly int DeathBurstTriggered;
        public readonly int DeathBurstDamageApplied;
        public readonly int EnrageTriggered;
        public readonly int EnrageApplied;
        public readonly int SummonRequested;
        public readonly int SummonSpawned;
        public readonly int SummonExpired;
        public readonly int SummonDespawned;
        public readonly int PresentationUiMarkers;
        public readonly int PresentationVfxMarkers;
        public readonly int PresentationSfxMarkers;
        public readonly int PresentationFloatingTextMarkers;
        public readonly int PresentationCueMarkers;
        public readonly int PresentationSettlementMarkers;

        public HeadlessAutoChessEventCounts(
            int replayEvents,
            int structuredLogEntries,
            int abilityCommitSucceeded,
            int abilityCommitFailed,
            int gameplayEffectInstanced,
            int gameplayEffectApplied,
            int gameplayEffectRemoved,
            int attributeChanges,
            int healthDamageAttributeChanges,
            int tagChanges,
            int cueRequests,
            int damageEvents,
            int unitDefeated,
            int battleResolved,
            int passiveTriggered,
            int killManaGranted,
            int reviveRequested,
            int reviveApplied,
            int synergyActivated,
            int synergyExpired,
            int synergyAllyBuffRequested,
            int synergyEnemyDebuffRequested,
            int synergyPeriodicTicked,
            int controlTurnSkipped,
            int shieldApplied,
            int shieldAbsorbed,
            int shieldBroken,
            int damageTypeResolved,
            int damageResisted,
            int equipmentApplied,
            int counterTriggered,
            int counterDamageApplied,
            int cleanseRequested,
            int cleanseApplied,
            int cleanseEffectRemoved,
            int cleanseRallyRequested,
            int cleanseRallyApplied,
            int rallyComboTriggered,
            int rallyComboDamageApplied,
            int lifeStealTriggered,
            int lifeStealHealed,
            int poisonStackRequested,
            int poisonStackChanged,
            int poisonOverflowTriggered,
            int poisonOverflowDamageApplied,
            int poisonPeriodDamageApplied,
            int executeTriggered,
            int executeDamageApplied,
            int deathBurstTriggered,
            int deathBurstDamageApplied,
            int enrageTriggered,
            int enrageApplied,
            int summonRequested,
            int summonSpawned,
            int summonExpired,
            int summonDespawned,
            int presentationUiMarkers,
            int presentationVfxMarkers,
            int presentationSfxMarkers,
            int presentationFloatingTextMarkers,
            int presentationCueMarkers,
            int presentationSettlementMarkers)
        {
            ReplayEvents = replayEvents;
            StructuredLogEntries = structuredLogEntries;
            AbilityCommitSucceeded = abilityCommitSucceeded;
            AbilityCommitFailed = abilityCommitFailed;
            GameplayEffectInstanced = gameplayEffectInstanced;
            GameplayEffectApplied = gameplayEffectApplied;
            GameplayEffectRemoved = gameplayEffectRemoved;
            AttributeChanges = attributeChanges;
            HealthDamageAttributeChanges = healthDamageAttributeChanges;
            TagChanges = tagChanges;
            CueRequests = cueRequests;
            DamageEvents = damageEvents;
            UnitDefeated = unitDefeated;
            BattleResolved = battleResolved;
            PassiveTriggered = passiveTriggered;
            KillManaGranted = killManaGranted;
            ReviveRequested = reviveRequested;
            ReviveApplied = reviveApplied;
            SynergyActivated = synergyActivated;
            SynergyExpired = synergyExpired;
            SynergyAllyBuffRequested = synergyAllyBuffRequested;
            SynergyEnemyDebuffRequested = synergyEnemyDebuffRequested;
            SynergyPeriodicTicked = synergyPeriodicTicked;
            ControlTurnSkipped = controlTurnSkipped;
            ShieldApplied = shieldApplied;
            ShieldAbsorbed = shieldAbsorbed;
            ShieldBroken = shieldBroken;
            DamageTypeResolved = damageTypeResolved;
            DamageResisted = damageResisted;
            EquipmentApplied = equipmentApplied;
            CounterTriggered = counterTriggered;
            CounterDamageApplied = counterDamageApplied;
            CleanseRequested = cleanseRequested;
            CleanseApplied = cleanseApplied;
            CleanseEffectRemoved = cleanseEffectRemoved;
            CleanseRallyRequested = cleanseRallyRequested;
            CleanseRallyApplied = cleanseRallyApplied;
            RallyComboTriggered = rallyComboTriggered;
            RallyComboDamageApplied = rallyComboDamageApplied;
            LifeStealTriggered = lifeStealTriggered;
            LifeStealHealed = lifeStealHealed;
            PoisonStackRequested = poisonStackRequested;
            PoisonStackChanged = poisonStackChanged;
            PoisonOverflowTriggered = poisonOverflowTriggered;
            PoisonOverflowDamageApplied = poisonOverflowDamageApplied;
            PoisonPeriodDamageApplied = poisonPeriodDamageApplied;
            ExecuteTriggered = executeTriggered;
            ExecuteDamageApplied = executeDamageApplied;
            DeathBurstTriggered = deathBurstTriggered;
            DeathBurstDamageApplied = deathBurstDamageApplied;
            EnrageTriggered = enrageTriggered;
            EnrageApplied = enrageApplied;
            SummonRequested = summonRequested;
            SummonSpawned = summonSpawned;
            SummonExpired = summonExpired;
            SummonDespawned = summonDespawned;
            PresentationUiMarkers = presentationUiMarkers;
            PresentationVfxMarkers = presentationVfxMarkers;
            PresentationSfxMarkers = presentationSfxMarkers;
            PresentationFloatingTextMarkers = presentationFloatingTextMarkers;
            PresentationCueMarkers = presentationCueMarkers;
            PresentationSettlementMarkers = presentationSettlementMarkers;
        }
    }

    public struct HeadlessAutoChessPresentationOutboxCounts
    {
        public int TotalEvents;
        public int GameplayEvents;
        public int CueRequests;
        public int UiMarkers;
        public int VfxMarkers;
        public int SfxMarkers;
        public int FloatingTextMarkers;
        public int CueMarkers;
        public int SettlementMarkers;
        public int UiHealthBarAttachedMarkers;
        public int VfxAbilityImpactMarkers;
        public int SfxImpactMarkers;
        public int FloatingTextDamageMarkers;
        public int CueRequestPresentationMarkers;
        public int SettlementScoreboardMarkers;
        public int UiCrowdControlSkippedMarkers;
        public int VfxCrowdControlMarkers;
        public int SfxCrowdControlMarkers;
        public int FloatingTextControlMarkers;
        public int CueCrowdControlMarkers;
        public int UiShieldChangedMarkers;
        public int VfxShieldMarkers;
        public int SfxShieldMarkers;
        public int FloatingTextShieldMarkers;
        public int CueShieldMarkers;
        public int UiSummonMarkers;
        public int VfxSummonMarkers;
        public int SfxSummonMarkers;
        public int FloatingTextSummonMarkers;
        public int CueSummonMarkers;
        public int UiResistanceMarkers;
        public int VfxResistanceMarkers;
        public int SfxResistanceMarkers;
        public int FloatingTextResistanceMarkers;
        public int CueResistanceMarkers;
        public int UiEquipmentMarkers;
        public int UiCounterMarkers;
        public int VfxCounterMarkers;
        public int SfxCounterMarkers;
        public int FloatingTextCounterMarkers;
        public int CueCounterMarkers;
        public int UiCleanseMarkers;
        public int VfxCleanseMarkers;
        public int SfxCleanseMarkers;
        public int FloatingTextCleanseMarkers;
        public int CueCleanseMarkers;
        public int UiCleanseRallyMarkers;
        public int VfxCleanseRallyMarkers;
        public int SfxCleanseRallyMarkers;
        public int FloatingTextCleanseRallyMarkers;
        public int CueCleanseRallyMarkers;
        public int UiRallyComboMarkers;
        public int VfxRallyComboMarkers;
        public int SfxRallyComboMarkers;
        public int FloatingTextRallyComboMarkers;
        public int CueRallyComboMarkers;
        public int UiLifeStealMarkers;
        public int VfxLifeStealMarkers;
        public int SfxLifeStealMarkers;
        public int FloatingTextLifeStealMarkers;
        public int CueLifeStealMarkers;
        public int UiPoisonMarkers;
        public int VfxPoisonMarkers;
        public int SfxPoisonMarkers;
        public int FloatingTextPoisonMarkers;
        public int CuePoisonMarkers;
        public int UiExecuteMarkers;
        public int VfxExecuteMarkers;
        public int SfxExecuteMarkers;
        public int FloatingTextExecuteMarkers;
        public int CueExecuteMarkers;
        public int UiDeathBurstMarkers;
        public int VfxDeathBurstMarkers;
        public int SfxDeathBurstMarkers;
        public int FloatingTextDeathBurstMarkers;
        public int CueDeathBurstMarkers;
        public int UiEnrageMarkers;
        public int VfxEnrageMarkers;
        public int SfxEnrageMarkers;
        public int FloatingTextEnrageMarkers;
        public int CueEnrageMarkers;

        public int TotalMarkers =>
            UiMarkers
            + VfxMarkers
            + SfxMarkers
            + FloatingTextMarkers
            + CueMarkers
            + SettlementMarkers;

        public void Accumulate(in PresentationEventBuffer evt)
        {
            TotalEvents++;
            if (evt.Kind == EPresentationEventKind.CueRequest)
                CueRequests++;
            else if (evt.Kind == EPresentationEventKind.GameplayEvent)
                AccumulateGameplayEvent(evt.GameplayEventType, evt.EventCode);
        }

        private void AccumulateGameplayEvent(EGameplayEventType type, int markerCode)
        {
            GameplayEvents++;
            switch (type)
            {
                case EGameplayEventType.AutoChessPresentationUiMarker:
                    UiMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiHealthBarAttached)
                        UiHealthBarAttachedMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiCrowdControlSkipped)
                        UiCrowdControlSkippedMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiShieldChanged)
                        UiShieldChangedMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiSummonSpawned
                        || markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiSummonExpired)
                        UiSummonMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiResistanceChanged)
                        UiResistanceMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiEquipmentChanged)
                        UiEquipmentMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiCounterTriggered)
                        UiCounterMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiCleanseTriggered)
                        UiCleanseMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiCleanseRallyTriggered)
                        UiCleanseRallyMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiRallyComboTriggered)
                        UiRallyComboMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiLifeStealTriggered)
                        UiLifeStealMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiPoisonStacked)
                        UiPoisonMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiExecuteTriggered)
                        UiExecuteMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiDeathBurstTriggered)
                        UiDeathBurstMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.UiEnrageTriggered)
                        UiEnrageMarkers++;
                    break;
                case EGameplayEventType.AutoChessPresentationVfxMarker:
                    VfxMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.VfxAbilityImpact)
                        VfxAbilityImpactMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.VfxCrowdControl)
                        VfxCrowdControlMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.VfxShield)
                        VfxShieldMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.VfxSummon
                        || markerCode == (int)HeadlessAutoChessPresentationMarkerCode.VfxSummonExpired)
                        VfxSummonMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.VfxResistance)
                        VfxResistanceMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.VfxCounter)
                        VfxCounterMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.VfxCleanse)
                        VfxCleanseMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.VfxCleanseRally)
                        VfxCleanseRallyMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.VfxRallyCombo)
                        VfxRallyComboMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.VfxLifeSteal)
                        VfxLifeStealMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.VfxPoison)
                        VfxPoisonMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.VfxExecute)
                        VfxExecuteMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.VfxDeathBurst)
                        VfxDeathBurstMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.VfxEnrage)
                        VfxEnrageMarkers++;
                    break;
                case EGameplayEventType.AutoChessPresentationSfxMarker:
                    SfxMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SfxImpact)
                        SfxImpactMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SfxCrowdControl)
                        SfxCrowdControlMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SfxShield)
                        SfxShieldMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SfxSummon
                        || markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SfxSummonExpired)
                        SfxSummonMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SfxResistance)
                        SfxResistanceMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SfxCounter)
                        SfxCounterMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SfxCleanse)
                        SfxCleanseMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SfxCleanseRally)
                        SfxCleanseRallyMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SfxRallyCombo)
                        SfxRallyComboMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SfxLifeSteal)
                        SfxLifeStealMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SfxPoison)
                        SfxPoisonMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SfxExecute)
                        SfxExecuteMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SfxDeathBurst)
                        SfxDeathBurstMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SfxEnrage)
                        SfxEnrageMarkers++;
                    break;
                case EGameplayEventType.AutoChessPresentationFloatingTextMarker:
                    FloatingTextMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextDamage)
                        FloatingTextDamageMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextControl)
                        FloatingTextControlMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextShield)
                        FloatingTextShieldMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextSummon
                        || markerCode == (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextSummonExpired)
                        FloatingTextSummonMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextResistance)
                        FloatingTextResistanceMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextCounter)
                        FloatingTextCounterMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextCleanse)
                        FloatingTextCleanseMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextCleanseRally)
                        FloatingTextCleanseRallyMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextRallyCombo)
                        FloatingTextRallyComboMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextLifeSteal)
                        FloatingTextLifeStealMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextPoison)
                        FloatingTextPoisonMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextExecute)
                        FloatingTextExecuteMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextDeathBurst)
                        FloatingTextDeathBurstMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextEnrage)
                        FloatingTextEnrageMarkers++;
                    break;
                case EGameplayEventType.AutoChessPresentationCueMarker:
                    CueMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.CueRequest)
                        CueRequestPresentationMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.CueCrowdControl)
                        CueCrowdControlMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.CueShield)
                        CueShieldMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.CueSummon
                        || markerCode == (int)HeadlessAutoChessPresentationMarkerCode.CueSummonExpired)
                        CueSummonMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.CueResistance)
                        CueResistanceMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.CueCounter)
                        CueCounterMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.CueCleanse)
                        CueCleanseMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.CueCleanseRally)
                        CueCleanseRallyMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.CueRallyCombo)
                        CueRallyComboMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.CueLifeSteal)
                        CueLifeStealMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.CuePoison)
                        CuePoisonMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.CueExecute)
                        CueExecuteMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.CueDeathBurst)
                        CueDeathBurstMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.CueEnrage)
                        CueEnrageMarkers++;
                    break;
                case EGameplayEventType.AutoChessPresentationSettlementMarker:
                    SettlementMarkers++;
                    if (markerCode == (int)HeadlessAutoChessPresentationMarkerCode.SettlementScoreboard)
                        SettlementScoreboardMarkers++;
                    break;
            }
        }
    }

    public readonly struct HeadlessAutoChessValidationReport
    {
        public readonly bool Passed;
        public readonly int FailureCount;
        public readonly HeadlessAutoChessValidationThresholds Thresholds;
        public readonly string SummaryText;
        public readonly string[] FailureMessages;
        public readonly GasStructuredLogFileExportResult AssertionLogFile;
        public readonly GasStructuredLogFileExportResult HumanReadableLogFile;
        public readonly string SummaryPath;
        public readonly long SummaryByteCount;

        public HeadlessAutoChessValidationReport(
            bool passed,
            int failureCount,
            HeadlessAutoChessValidationThresholds thresholds,
            string summaryText,
            string[] failureMessages,
            GasStructuredLogFileExportResult assertionLogFile,
            GasStructuredLogFileExportResult humanReadableLogFile,
            string summaryPath,
            long summaryByteCount)
        {
            Passed = passed;
            FailureCount = failureCount;
            Thresholds = thresholds;
            SummaryText = summaryText ?? string.Empty;
            FailureMessages = failureMessages ?? Array.Empty<string>();
            AssertionLogFile = assertionLogFile;
            HumanReadableLogFile = humanReadableLogFile;
            SummaryPath = summaryPath ?? string.Empty;
            SummaryByteCount = summaryByteCount;
        }
    }

    public struct HeadlessAutoChessRuntimeTickTiming
    {
        public int TickCount;
        public long TotalTicks;
        public long CommandTicks;
        public long ResetDirtyTicks;
        public long TagTicks;
        public long EffectTicks;
        public long AttributeTicks;
        public long AbilityTicks;
        public long CueTicks;

        public void Accumulate(in RuntimeTickGroupTiming timing)
        {
            TickCount++;
            TotalTicks += timing.TotalTicks;
            CommandTicks += timing.CommandTicks;
            ResetDirtyTicks += timing.ResetDirtyTicks;
            TagTicks += timing.TagTicks;
            EffectTicks += timing.EffectTicks;
            AttributeTicks += timing.AttributeTicks;
            AbilityTicks += timing.AbilityTicks;
            CueTicks += timing.CueTicks;
        }

        public void Accumulate(in HeadlessAutoChessRuntimeTickTiming timing)
        {
            TickCount += timing.TickCount;
            TotalTicks += timing.TotalTicks;
            CommandTicks += timing.CommandTicks;
            ResetDirtyTicks += timing.ResetDirtyTicks;
            TagTicks += timing.TagTicks;
            EffectTicks += timing.EffectTicks;
            AttributeTicks += timing.AttributeTicks;
            AbilityTicks += timing.AbilityTicks;
            CueTicks += timing.CueTicks;
        }

        public double AverageTotalMilliseconds => AverageMilliseconds(TotalTicks);

        public double AverageCommandMilliseconds => AverageMilliseconds(CommandTicks);

        public double AverageResetDirtyMilliseconds => AverageMilliseconds(ResetDirtyTicks);

        public double AverageTagMilliseconds => AverageMilliseconds(TagTicks);

        public double AverageEffectMilliseconds => AverageMilliseconds(EffectTicks);

        public double AverageAttributeMilliseconds => AverageMilliseconds(AttributeTicks);

        public double AverageAbilityMilliseconds => AverageMilliseconds(AbilityTicks);

        public double AverageCueMilliseconds => AverageMilliseconds(CueTicks);

        private double AverageMilliseconds(long ticks)
        {
            return TickCount > 0
                ? ticks * 1000d / Stopwatch.Frequency / TickCount
                : 0d;
        }
    }

    public readonly struct RuntimeTickGroupTiming
    {
        public readonly long TotalTicks;
        public readonly long CommandTicks;
        public readonly long ResetDirtyTicks;
        public readonly long TagTicks;
        public readonly long EffectTicks;
        public readonly long AttributeTicks;
        public readonly long AbilityTicks;
        public readonly long CueTicks;

        public RuntimeTickGroupTiming(
            long totalTicks,
            long commandTicks,
            long resetDirtyTicks,
            long tagTicks,
            long effectTicks,
            long attributeTicks,
            long abilityTicks,
            long cueTicks)
        {
            TotalTicks = totalTicks;
            CommandTicks = commandTicks;
            ResetDirtyTicks = resetDirtyTicks;
            TagTicks = tagTicks;
            EffectTicks = effectTicks;
            AttributeTicks = attributeTicks;
            AbilityTicks = abilityTicks;
            CueTicks = cueTicks;
        }
    }

    public readonly struct HeadlessAutoChessSystemTiming
    {
        public readonly string GroupName;
        public readonly string SystemName;
        public readonly int CallCount;
        public readonly long ElapsedTicks;
        public readonly double TotalMilliseconds;
        public readonly double AverageMilliseconds;

        public HeadlessAutoChessSystemTiming(
            string groupName,
            string systemName,
            int callCount,
            long elapsedTicks)
        {
            GroupName = groupName ?? string.Empty;
            SystemName = systemName ?? string.Empty;
            CallCount = callCount;
            ElapsedTicks = elapsedTicks;
            TotalMilliseconds = elapsedTicks * 1000d / Stopwatch.Frequency;
            AverageMilliseconds = callCount > 0
                ? TotalMilliseconds / callCount
                : 0d;
        }
    }

    internal sealed class HeadlessAutoChessRuntimeSystemTimingCollector
    {
        private readonly Dictionary<string, SystemTimingCounter> _counterByKey = new();
        private readonly List<SystemTimingCounter> _counters = new();
        private readonly Dictionary<string, SystemHandle[]> _systemsByGroup = new();

        public void Record(string groupName, string systemName, long elapsedTicks)
        {
            var safeGroupName = groupName ?? string.Empty;
            var safeSystemName = systemName ?? string.Empty;
            var key = safeGroupName + "|" + safeSystemName;

            if (!_counterByKey.TryGetValue(key, out var counter))
            {
                counter = new SystemTimingCounter(safeGroupName, safeSystemName);
                _counterByKey.Add(key, counter);
                _counters.Add(counter);
            }

            counter.CallCount++;
            counter.ElapsedTicks += elapsedTicks;
        }

        public SystemHandle[] GetSystems(ComponentSystemGroup group, string groupName)
        {
            var safeGroupName = groupName ?? string.Empty;
            if (_systemsByGroup.TryGetValue(safeGroupName, out var systems))
                return systems;

            group.SortSystems();
            using var nativeSystems = group.GetAllSystems(Allocator.Temp);
            systems = new SystemHandle[nativeSystems.Length];
            for (var i = 0; i < nativeSystems.Length; i++)
                systems[i] = nativeSystems[i];

            _systemsByGroup.Add(safeGroupName, systems);
            return systems;
        }

        public HeadlessAutoChessSystemTiming[] ToSortedTimings()
        {
            if (_counters.Count == 0)
                return Array.Empty<HeadlessAutoChessSystemTiming>();

            var counters = _counters.ToArray();
            Array.Sort(
                counters,
                (left, right) => right.ElapsedTicks.CompareTo(left.ElapsedTicks));

            var timings = new HeadlessAutoChessSystemTiming[counters.Length];
            for (var i = 0; i < counters.Length; i++)
            {
                var counter = counters[i];
                timings[i] = new HeadlessAutoChessSystemTiming(
                    counter.GroupName,
                    counter.SystemName,
                    counter.CallCount,
                    counter.ElapsedTicks);
            }

            return timings;
        }

        private sealed class SystemTimingCounter
        {
            public readonly string GroupName;
            public readonly string SystemName;
            public int CallCount;
            public long ElapsedTicks;

            public SystemTimingCounter(string groupName, string systemName)
            {
                GroupName = groupName;
                SystemName = systemName;
            }
        }
    }

    public readonly struct HeadlessAutoChessResult
    {
        public readonly HeadlessAutoChessScenarioVariant Variant;
        public readonly string VariantName;
        public readonly int DeterministicSeed;
        public readonly int PlayerUnitCount;
        public readonly int EnemyUnitCount;
        public readonly bool Completed;
        public readonly HeadlessAutoChessTeam Winner;
        public readonly int BoardWidth;
        public readonly int BoardHeight;
        public readonly int BattleTicks;
        public readonly int TotalTicks;
        public readonly int MeasuredTicks;
        public readonly int Round;
        public readonly int TurnCount;
        public readonly int DriverIssuedCommands;
        public readonly int DriverIssuedPrimaryCommands;
        public readonly int DriverIssuedManaAbilityCommands;
        public readonly int DriverIssuedControlAbilityCommands;
        public readonly int DriverIssuedSupportAbilityCommands;
        public readonly int DriverIssuedSummonAbilityCommands;
        public readonly int CrowdControlTurnSkippedCount;
        public readonly int DriverFrontlineTargetSelections;
        public readonly int DriverLowestHealthTargetSelections;
        public readonly int PlayerDefeatedCount;
        public readonly int EnemyDefeatedCount;
        public readonly int FirstDefeatFrame;
        public readonly int LastDefeatFrame;
        public readonly int BattleResolvedFrame;
        public readonly int PassiveTriggeredCount;
        public readonly int KillManaGrantedCount;
        public readonly int ReviveRequestedCount;
        public readonly int ReviveAppliedCount;
        public readonly int SynergyActivatedCount;
        public readonly int SynergyExpiredCount;
        public readonly int SynergyAllyBuffRequestedCount;
        public readonly int SynergyEnemyDebuffRequestedCount;
        public readonly int SynergyPeriodicTickCount;
        public readonly int ShieldAppliedCount;
        public readonly int ShieldAbsorbedCount;
        public readonly int ShieldBrokenCount;
        public readonly int EquipmentAppliedCount;
        public readonly int CounterTriggeredCount;
        public readonly int CounterDamageAppliedCount;
        public readonly int CleanseRequestedCount;
        public readonly int CleanseAppliedCount;
        public readonly int CleanseEffectRemovedCount;
        public readonly int CleanseRallyRequestedCount;
        public readonly int CleanseRallyAppliedCount;
        public readonly int RallyComboTriggeredCount;
        public readonly int RallyComboDamageAppliedCount;
        public readonly int LifeStealTriggeredCount;
        public readonly int LifeStealHealedCount;
        public readonly int PoisonStackRequestedCount;
        public readonly int PoisonStackChangedCount;
        public readonly int PoisonOverflowTriggeredCount;
        public readonly int PoisonOverflowDamageAppliedCount;
        public readonly int PoisonPeriodDamageAppliedCount;
        public readonly int ExecuteTriggeredCount;
        public readonly int ExecuteDamageAppliedCount;
        public readonly int DeathBurstTriggeredCount;
        public readonly int DeathBurstDamageAppliedCount;
        public readonly int EnrageTriggeredCount;
        public readonly int EnrageAppliedCount;
        public readonly int SummonRequestedCount;
        public readonly int SummonSpawnedCount;
        public readonly int SummonExpiredCount;
        public readonly int SummonDespawnedCount;
        public readonly long ElapsedTicks;
        public readonly double ElapsedMilliseconds;
        public readonly double AverageTickMilliseconds;
        public readonly HeadlessAutoChessRuntimeTickTiming RuntimeTiming;
        public readonly HeadlessAutoChessSystemTiming[] SystemTimings;
        public readonly HeadlessAutoChessUnitResult[] Units;
        public readonly HeadlessAutoChessEventCounts EventCounts;
        public readonly HeadlessAutoChessPresentationOutboxCounts PresentationOutboxCounts;
        public readonly GasStructuredLogExportSnapshot StructuredLogSnapshot;
        public readonly string AssertionLog;
        public readonly HeadlessAutoChessValidationReport ValidationReport;

        public HeadlessAutoChessResult(
            HeadlessAutoChessScenarioVariant variant,
            string variantName,
            int deterministicSeed,
            int playerUnitCount,
            int enemyUnitCount,
            bool completed,
            HeadlessAutoChessTeam winner,
            int boardWidth,
            int boardHeight,
            int battleTicks,
            int totalTicks,
            int measuredTicks,
            int round,
            int turnCount,
            int driverIssuedCommands,
            int driverIssuedPrimaryCommands,
            int driverIssuedManaAbilityCommands,
            int driverIssuedControlAbilityCommands,
            int driverIssuedSupportAbilityCommands,
            int driverIssuedSummonAbilityCommands,
            int crowdControlTurnSkippedCount,
            int driverFrontlineTargetSelections,
            int driverLowestHealthTargetSelections,
            int playerDefeatedCount,
            int enemyDefeatedCount,
            int firstDefeatFrame,
            int lastDefeatFrame,
            int battleResolvedFrame,
            int passiveTriggeredCount,
            int killManaGrantedCount,
            int reviveRequestedCount,
            int reviveAppliedCount,
            int synergyActivatedCount,
            int synergyExpiredCount,
            int synergyAllyBuffRequestedCount,
            int synergyEnemyDebuffRequestedCount,
            int synergyPeriodicTickCount,
            int shieldAppliedCount,
            int shieldAbsorbedCount,
            int shieldBrokenCount,
            int equipmentAppliedCount,
            int counterTriggeredCount,
            int counterDamageAppliedCount,
            int cleanseRequestedCount,
            int cleanseAppliedCount,
            int cleanseEffectRemovedCount,
            int cleanseRallyRequestedCount,
            int cleanseRallyAppliedCount,
            int rallyComboTriggeredCount,
            int rallyComboDamageAppliedCount,
            int lifeStealTriggeredCount,
            int lifeStealHealedCount,
            int poisonStackRequestedCount,
            int poisonStackChangedCount,
            int poisonOverflowTriggeredCount,
            int poisonOverflowDamageAppliedCount,
            int poisonPeriodDamageAppliedCount,
            int executeTriggeredCount,
            int executeDamageAppliedCount,
            int deathBurstTriggeredCount,
            int deathBurstDamageAppliedCount,
            int enrageTriggeredCount,
            int enrageAppliedCount,
            int summonRequestedCount,
            int summonSpawnedCount,
            int summonExpiredCount,
            int summonDespawnedCount,
            long elapsedTicks,
            double elapsedMilliseconds,
            in HeadlessAutoChessRuntimeTickTiming runtimeTiming,
            HeadlessAutoChessSystemTiming[] systemTimings,
            HeadlessAutoChessUnitResult[] units,
            HeadlessAutoChessEventCounts eventCounts,
            HeadlessAutoChessPresentationOutboxCounts presentationOutboxCounts,
            GasStructuredLogExportSnapshot structuredLogSnapshot,
            string assertionLog,
            HeadlessAutoChessValidationReport validationReport)
        {
            Variant = variant;
            VariantName = variantName ?? string.Empty;
            DeterministicSeed = deterministicSeed;
            PlayerUnitCount = playerUnitCount;
            EnemyUnitCount = enemyUnitCount;
            Completed = completed;
            Winner = winner;
            BoardWidth = boardWidth;
            BoardHeight = boardHeight;
            BattleTicks = battleTicks;
            TotalTicks = totalTicks;
            MeasuredTicks = measuredTicks;
            Round = round;
            TurnCount = turnCount;
            DriverIssuedCommands = driverIssuedCommands;
            DriverIssuedPrimaryCommands = driverIssuedPrimaryCommands;
            DriverIssuedManaAbilityCommands = driverIssuedManaAbilityCommands;
            DriverIssuedControlAbilityCommands = driverIssuedControlAbilityCommands;
            DriverIssuedSupportAbilityCommands = driverIssuedSupportAbilityCommands;
            DriverIssuedSummonAbilityCommands = driverIssuedSummonAbilityCommands;
            CrowdControlTurnSkippedCount = crowdControlTurnSkippedCount;
            DriverFrontlineTargetSelections = driverFrontlineTargetSelections;
            DriverLowestHealthTargetSelections = driverLowestHealthTargetSelections;
            PlayerDefeatedCount = playerDefeatedCount;
            EnemyDefeatedCount = enemyDefeatedCount;
            FirstDefeatFrame = firstDefeatFrame;
            LastDefeatFrame = lastDefeatFrame;
            BattleResolvedFrame = battleResolvedFrame;
            PassiveTriggeredCount = passiveTriggeredCount;
            KillManaGrantedCount = killManaGrantedCount;
            ReviveRequestedCount = reviveRequestedCount;
            ReviveAppliedCount = reviveAppliedCount;
            SynergyActivatedCount = synergyActivatedCount;
            SynergyExpiredCount = synergyExpiredCount;
            SynergyAllyBuffRequestedCount = synergyAllyBuffRequestedCount;
            SynergyEnemyDebuffRequestedCount = synergyEnemyDebuffRequestedCount;
            SynergyPeriodicTickCount = synergyPeriodicTickCount;
            ShieldAppliedCount = shieldAppliedCount;
            ShieldAbsorbedCount = shieldAbsorbedCount;
            ShieldBrokenCount = shieldBrokenCount;
            EquipmentAppliedCount = equipmentAppliedCount;
            CounterTriggeredCount = counterTriggeredCount;
            CounterDamageAppliedCount = counterDamageAppliedCount;
            CleanseRequestedCount = cleanseRequestedCount;
            CleanseAppliedCount = cleanseAppliedCount;
            CleanseEffectRemovedCount = cleanseEffectRemovedCount;
            CleanseRallyRequestedCount = cleanseRallyRequestedCount;
            CleanseRallyAppliedCount = cleanseRallyAppliedCount;
            RallyComboTriggeredCount = rallyComboTriggeredCount;
            RallyComboDamageAppliedCount = rallyComboDamageAppliedCount;
            LifeStealTriggeredCount = lifeStealTriggeredCount;
            LifeStealHealedCount = lifeStealHealedCount;
            PoisonStackRequestedCount = poisonStackRequestedCount;
            PoisonStackChangedCount = poisonStackChangedCount;
            PoisonOverflowTriggeredCount = poisonOverflowTriggeredCount;
            PoisonOverflowDamageAppliedCount = poisonOverflowDamageAppliedCount;
            PoisonPeriodDamageAppliedCount = poisonPeriodDamageAppliedCount;
            ExecuteTriggeredCount = executeTriggeredCount;
            ExecuteDamageAppliedCount = executeDamageAppliedCount;
            DeathBurstTriggeredCount = deathBurstTriggeredCount;
            DeathBurstDamageAppliedCount = deathBurstDamageAppliedCount;
            EnrageTriggeredCount = enrageTriggeredCount;
            EnrageAppliedCount = enrageAppliedCount;
            SummonRequestedCount = summonRequestedCount;
            SummonSpawnedCount = summonSpawnedCount;
            SummonExpiredCount = summonExpiredCount;
            SummonDespawnedCount = summonDespawnedCount;
            ElapsedTicks = elapsedTicks;
            ElapsedMilliseconds = elapsedMilliseconds;
            AverageTickMilliseconds = measuredTicks > 0
                ? elapsedMilliseconds / measuredTicks
                : battleTicks > 0
                    ? elapsedMilliseconds / battleTicks
                    : totalTicks > 0
                        ? elapsedMilliseconds / totalTicks
                        : 0d;
            RuntimeTiming = runtimeTiming;
            SystemTimings = systemTimings ?? Array.Empty<HeadlessAutoChessSystemTiming>();
            Units = units ?? Array.Empty<HeadlessAutoChessUnitResult>();
            EventCounts = eventCounts;
            PresentationOutboxCounts = presentationOutboxCounts;
            StructuredLogSnapshot = structuredLogSnapshot;
            AssertionLog = assertionLog ?? string.Empty;
            ValidationReport = validationReport;
        }
    }
}