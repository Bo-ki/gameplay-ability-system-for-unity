using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
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

        public void Accumulate(in BPresentationEvent evt)
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

    /// <summary>
    /// Headless RPG auto-chess validation scenario. It has no scene, no UI, and
    /// accepts only replay/structured log facts as observable evidence.
    /// </summary>
    public static class HeadlessAutoChessScenario
    {
        public const int BoardWidth = 6;
        public const int BoardHeight = 3;
        public const int PerformanceWarmupBattleTicks = 4;

        public const int AttributeSetCombat = 9601;
        public const int AttributeHealth = 1;
        public const int AttributeMana = 2;
        public const int AttributeShield = 3;
        public const int AttributeArcaneResistance = 4;
        public const int AttributeCounterDamage = 5;
        public const int AttributeLifeStealRatio = 6;

        public const int AbilityPlayerStrike = 9611;
        public const int AbilityEnemyStrike = 9612;
        public const int AbilityPlayerManaBurst = 9613;
        public const int AbilityPlayerControlStun = 9614;
        public const int AbilityPlayerBarrier = 9615;
        public const int AbilityPlayerSummon = 9616;
        public const int AbilityPlayerCleanse = 9617;

        public const int GameplayEffectPlayerStrikeDamage = 9621;
        public const int GameplayEffectEnemyStrikeDamage = 9622;
        public const int GameplayEffectPlayerManaBurstDamage = 9623;
        public const int GameplayEffectManaBurstCost = 9624;
        public const int GameplayEffectManaBurstCooldown = 9625;
        public const int GameplayEffectKillManaGain = 9626;
        public const int GameplayEffectSelfRevive = 9627;
        public const int GameplayEffectArcaneTeamBuff = 9628;
        public const int GameplayEffectArcaneStormDebuff = 9629;
        public const int GameplayEffectArcaneStormPeriodDamage = 9630;
        public const int GameplayEffectPlayerStun = 9634;
        public const int GameplayEffectPlayerStunCooldown = 9635;
        public const int GameplayEffectPlayerBarrierShield = 9637;
        public const int GameplayEffectPlayerBarrierStatus = 9638;
        public const int GameplayEffectPlayerBarrierCooldown = 9639;
        public const int GameplayEffectPlayerSummonRequest = 9642;
        public const int GameplayEffectPlayerSummonCooldown = 9643;
        public const int GameplayEffectPlayerCounterGear = 9645;
        public const int GameplayEffectPlayerCounterDamage = 9646;
        public const int GameplayEffectPlayerCleanse = 9647;
        public const int GameplayEffectPlayerCleanseCooldown = 9648;
        public const int GameplayEffectPlayerCleanseRally = 9664;
        public const int GameplayEffectPlayerRallyComboDamage = 9665;
        public const int GameplayEffectPlayerLifeStealGear = 9666;
        public const int GameplayEffectPlayerLifeStealHeal = 9667;
        public const int GameplayEffectPlayerPoisonStack = 9668;
        public const int GameplayEffectPlayerPoisonOverflowDamage = 9669;
        public const int GameplayEffectPlayerPoisonPeriodDamage = 9672;
        public const int GameplayEffectPlayerExecuteGear = 9673;
        public const int GameplayEffectPlayerExecuteDamage = 9674;
        public const int GameplayEffectPlayerDeathBurstGear = 9676;
        public const int GameplayEffectPlayerDeathBurstDamage = 9677;
        public const int GameplayEffectPlayerEnrage = 9679;

        public const int TimelinePlayerStrike = 9631;
        public const int TimelineEnemyStrike = 9632;
        public const int TimelinePlayerManaBurst = 9633;
        public const int TimelinePlayerControlStun = 9636;
        public const int TimelinePlayerBarrier = 9640;
        public const int TimelinePlayerSummon = 9644;
        public const int TimelinePlayerCleanse = 9649;

        public const int ExecutionCalculationShieldDamageOutput = 9651;
        public const int ExecutionCalculationHealthDamageOutput = 9652;
        public const int ExecutionCalculationResistedDamageOutput = 9653;
        public const int SetByCallerLifeStealHealAmount = 9654;
        public const int SetByCallerCounterDamageAmount = 9680;
        public const int SummonedUnitArcaneWisp = 9661;
        public const int DamageTypePhysical = 9662;
        public const int DamageTypeArcane = 9663;
        public const int PoisonStackingCode = 9670;
        public const int DamageTypePoison = 9671;
        public const int DamageTypeExecute = 9675;
        public const int DamageTypeDeathBurst = 9678;

        public const int TagAbilityActing = 4;
        public const int TagManaBurstCooldown = 5;
        public const int TagArcaneTeamBuff = 6;
        public const int TagArcaneStormDebuff = 7;
        public const int TagAutoChessStunned = 8;
        public const int TagPlayerStunCooldown = 9;
        public const int TagAutoChessShielded = 10;
        public const int TagPlayerBarrierCooldown = 11;
        public const int TagAutoChessSummoned = 12;
        public const int TagPlayerSummonCooldown = 13;
        public const int TagAutoChessCounterReady = 14;
        public const int TagPlayerCleanseCooldown = 15;
        public const int TagAutoChessCleanseRallied = 16;
        public const int TagAutoChessLifeStealReady = 17;
        public const int TagAutoChessPoisoned = 18;
        public const int TagAutoChessExecutionReady = 19;
        public const int TagAutoChessExecuted = 20;
        public const int TagAutoChessDeathBurstReady = 21;
        public const int TagAutoChessEnraged = 22;

        public const float KillManaGainAmount = 2f;
        public const float ReviveHealthAmount = 18f;
        public const int PlayerStunDurationFrames = 8;
        public const int PlayerStunCooldownFrames = 12;
        public const float PlayerBarrierShieldAmount = 10f;
        public const int PlayerBarrierStatusDurationFrames = 6;
        public const int PlayerBarrierCooldownFrames = 4;
        public const int PlayerSummonCooldownFrames = 12;
        public const int PlayerSummonLifetimeTurns = 6;
        public const int PlayerSummonMaxActiveCount = 1;
        public const int SummonedUnitSlotOffset = 100;
        public const int SummonedUnitTurnOrderOffset = 20;
        public const float PlayerSummonHealth = 16f;
        public const float PlayerCounterDamageAmount = 4f;
        public const float PlayerEnrageCounterDamageBonus = 2f;
        public const float MaxCounterDamageAmount = PlayerCounterDamageAmount + PlayerEnrageCounterDamageBonus;
        public const int PlayerCounterGearDurationFrames = 128;
        public const int PlayerEnrageDurationFrames = 128;
        public const float PlayerEnrageHealthThresholdRatio = 0.85f;
        public const int PlayerCleanseCooldownFrames = 5;
        public const int PlayerCleanseRallyDurationFrames = 6;
        public const float PlayerCleanseRallyManaAmount = 2f;
        public const float PlayerRallyComboDamageAmount = 6f;
        public const float PlayerLifeStealRatio = 0.35f;
        public const int PlayerLifeStealGearDurationFrames = 128;
        public const int PlayerPoisonStackDurationFrames = 18;
        public const int PlayerPoisonPeriodFrames = 3;
        public const int PlayerPoisonStackLimit = 2;
        public const float PlayerPoisonOverflowDamageAmount = 2f;
        public const float PlayerPoisonPeriodDamageAmount = 1f;
        public const int PlayerExecuteGearDurationFrames = 128;
        public const int PlayerExecutedMarkDurationFrames = 16;
        public const float PlayerExecuteHealthThreshold = 12f;
        public const float PlayerExecuteDamageAmount = 5f;
        public const int PlayerDeathBurstGearDurationFrames = 128;
        public const float PlayerDeathBurstDamageAmount = 2f;
        public const int PlayerDeathBurstMaxBoardDistance = 2;
        public const float MaxArcaneResistance = 0.75f;
        public const float EnemyArcaneResistance = 0.25f;
        public const float MaxShieldAmount = 32f;
        public const float MaxLifeStealRatio = 1f;
        public const int SynergyArcane = (int)HeadlessAutoChessSynergyKind.Arcane;
        public const int SynergyArcaneThreshold = 2;
        public const float ArcaneTeamBuffManaAmount = 1f;
        public const float ArcaneStormTickDamage = 3f;

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

        private static void EnsureRuntimeInitialized()
        {
            if (!GASManager.IsInitialized)
                GASManager.Initialize();
        }

        private static void RegisterTargetCatcher()
        {
            TargetCatcherHelper.RegisterTargetCatcher(
                HeadlessAutoChessDefinitionSource.TargetCatcherName,
                typeof(CatchTarget),
                typeof(XParamNone));
        }

        private static void RegisterConfigs()
        {
            HeadlessAutoChessDefinitionSource.RegisterRuntimeProviders();
        }

        private static void ClearConfigProviders()
        {
            HeadlessAutoChessDefinitionSource.ClearRuntimeProviders();
        }

        public static HeadlessAutoChessScenarioVariantDefinition GetVariantDefinition(
            HeadlessAutoChessScenarioVariant variant)
        {
            var normalized = NormalizeVariant(variant);
            var units = CreateUnits(normalized);
            return new HeadlessAutoChessScenarioVariantDefinition(
                normalized,
                GetVariantName(normalized),
                GetDeterministicSeed(normalized),
                CountUnits(units, HeadlessAutoChessTeam.Player),
                CountUnits(units, HeadlessAutoChessTeam.Enemy),
                HeadlessAutoChessTeam.Player);
        }

        private static HeadlessAutoChessScenarioVariantDefinition CreateRunVariantDefinition(
            HeadlessAutoChessScenarioVariant variant,
            int unitScale)
        {
            var normalized = NormalizeVariant(variant);
            var scale = unitScale > 0 ? unitScale : 1;
            var units = CreateUnits(normalized, scale);
            var name = GetVariantName(normalized);
            if (scale > 1)
                name += "x" + scale.ToString(CultureInfo.InvariantCulture);

            return new HeadlessAutoChessScenarioVariantDefinition(
                normalized,
                name,
                GetDeterministicSeed(normalized),
                CountUnits(units, HeadlessAutoChessTeam.Player),
                CountUnits(units, HeadlessAutoChessTeam.Enemy),
                HeadlessAutoChessTeam.Player);
        }

        public static string GetVariantName(HeadlessAutoChessScenarioVariant variant)
        {
            switch (NormalizeVariant(variant))
            {
                case HeadlessAutoChessScenarioVariant.PlayerAdvantage:
                    return "PlayerAdvantage";
                case HeadlessAutoChessScenarioVariant.EnemyPressure:
                    return "EnemyPressure";
                case HeadlessAutoChessScenarioVariant.LargeBoard:
                    return "LargeBoard";
                default:
                    return "DefaultBalanced";
            }
        }

        private static HeadlessAutoChessScenarioVariant NormalizeVariant(
            HeadlessAutoChessScenarioVariant variant)
        {
            return variant == HeadlessAutoChessScenarioVariant.PlayerAdvantage
                   || variant == HeadlessAutoChessScenarioVariant.EnemyPressure
                   || variant == HeadlessAutoChessScenarioVariant.LargeBoard
                ? variant
                : HeadlessAutoChessScenarioVariant.DefaultBalanced;
        }

        private static int GetDeterministicSeed(HeadlessAutoChessScenarioVariant variant)
        {
            switch (NormalizeVariant(variant))
            {
                case HeadlessAutoChessScenarioVariant.PlayerAdvantage:
                    return 2001;
                case HeadlessAutoChessScenarioVariant.EnemyPressure:
                    return 3001;
                case HeadlessAutoChessScenarioVariant.LargeBoard:
                    return 4001;
                default:
                    return 1001;
            }
        }

        private static UnitDefinition[] CreateUnits(HeadlessAutoChessScenarioVariant variant)
        {
            switch (NormalizeVariant(variant))
            {
                case HeadlessAutoChessScenarioVariant.PlayerAdvantage:
                    return CreatePlayerAdvantageUnits();
                case HeadlessAutoChessScenarioVariant.EnemyPressure:
                    return CreateEnemyPressureUnits();
                case HeadlessAutoChessScenarioVariant.LargeBoard:
                    return CreateLargeBoardUnits();
                default:
                    return CreateDefaultUnits();
            }
        }

        private static UnitDefinition[] CreateUnits(HeadlessAutoChessScenarioVariant variant, int unitScale)
        {
            var units = CreateUnits(variant);
            return unitScale > 1 ? ScaleUnits(units, unitScale) : units;
        }

        private static UnitDefinition[] ScaleUnits(UnitDefinition[] source, int unitScale)
        {
            if (source == null || source.Length == 0 || unitScale <= 1)
                return source ?? Array.Empty<UnitDefinition>();

            var scaled = new UnitDefinition[source.Length * unitScale];
            var index = 0;
            for (var copy = 0; copy < unitScale; copy++)
            {
                for (var i = 0; i < source.Length; i++)
                {
                    scaled[index] = copy == 0
                        ? source[i]
                        : source[i].WithScaleCopy(copy, copy * source.Length);
                    index++;
                }
            }

            return scaled;
        }

        private static int CountUnits(
            UnitDefinition[] units,
            HeadlessAutoChessTeam team)
        {
            var count = 0;
            for (var i = 0; i < units.Length; i++)
            {
                if (units[i].Team == team)
                    count++;
            }

            return count;
        }

        private static UnitDefinition[] CreateDefaultUnits()
        {
            return new[]
            {
                new UnitDefinition(
                    "player-guardian",
                    HeadlessAutoChessTeam.Player,
                    slot: 0,
                    boardX: 1,
                    boardY: 1,
                    turnOrder: 0,
                    health: 72f,
                    mana: 0f,
                    primaryAbilityCode: AbilityPlayerStrike,
                    manaAbilityCode: 0,
                    manaAbilityThreshold: 0f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    supportAbilityCode: AbilityPlayerBarrier,
                    supportTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    killManaGainGameplayEffectCode: GameplayEffectKillManaGain,
                    equipmentGameplayEffectCode: GameplayEffectPlayerCounterGear,
                    counterDamageGameplayEffectCode: GameplayEffectPlayerCounterDamage),
                new UnitDefinition(
                    "player-mage",
                    HeadlessAutoChessTeam.Player,
                    slot: 1,
                    boardX: 0,
                    boardY: 0,
                    turnOrder: 1,
                    health: 54f,
                    mana: 3f,
                    primaryAbilityCode: AbilityPlayerStrike,
                    manaAbilityCode: AbilityPlayerManaBurst,
                    manaAbilityThreshold: 3f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    controlAbilityCode: AbilityPlayerControlStun,
                    controlTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    supportAbilityCode: AbilityPlayerBarrier,
                    supportTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    synergyCode: SynergyArcane,
                    synergyThreshold: SynergyArcaneThreshold,
                    synergyAllyBuffGameplayEffectCode: GameplayEffectArcaneTeamBuff,
                    synergyEnemyDebuffGameplayEffectCode: GameplayEffectArcaneStormDebuff,
                    killManaGainGameplayEffectCode: GameplayEffectKillManaGain,
                    equipmentGameplayEffectCode: GameplayEffectPlayerCounterGear,
                    counterDamageGameplayEffectCode: GameplayEffectPlayerCounterDamage,
                    initialHealth: 52f),
                new UnitDefinition(
                    "player-ranger",
                    HeadlessAutoChessTeam.Player,
                    slot: 2,
                    boardX: 0,
                    boardY: 2,
                    turnOrder: 2,
                    health: 50f,
                    mana: 0f,
                    primaryAbilityCode: AbilityPlayerStrike,
                    manaAbilityCode: 0,
                    manaAbilityThreshold: 0f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    supportAbilityCode: AbilityPlayerCleanse,
                    supportTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    summonAbilityCode: AbilityPlayerSummon,
                    maxActiveSummons: PlayerSummonMaxActiveCount,
                    synergyCode: SynergyArcane,
                    synergyThreshold: SynergyArcaneThreshold,
                    synergyAllyBuffGameplayEffectCode: GameplayEffectArcaneTeamBuff,
                    synergyEnemyDebuffGameplayEffectCode: GameplayEffectArcaneStormDebuff,
                    killManaGainGameplayEffectCode: GameplayEffectKillManaGain,
                    equipmentGameplayEffectCode: GameplayEffectPlayerCounterGear,
                    counterDamageGameplayEffectCode: GameplayEffectPlayerCounterDamage,
                    initialHealth: 46f),
                new UnitDefinition(
                    "enemy-brute",
                    HeadlessAutoChessTeam.Enemy,
                    slot: 0,
                    boardX: 4,
                    boardY: 1,
                    turnOrder: 3,
                    health: 44f,
                    mana: 0f,
                    primaryAbilityCode: AbilityEnemyStrike,
                    manaAbilityCode: 0,
                    manaAbilityThreshold: 0f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    arcaneResistance: EnemyArcaneResistance),
                new UnitDefinition(
                    "enemy-rogue",
                    HeadlessAutoChessTeam.Enemy,
                    slot: 1,
                    boardX: 5,
                    boardY: 0,
                    turnOrder: 4,
                    health: 36f,
                    mana: 0f,
                    primaryAbilityCode: AbilityEnemyStrike,
                    manaAbilityCode: 0,
                    manaAbilityThreshold: 0f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    arcaneResistance: EnemyArcaneResistance),
                new UnitDefinition(
                    "enemy-shaman",
                    HeadlessAutoChessTeam.Enemy,
                    slot: 2,
                    boardX: 5,
                    boardY: 2,
                    turnOrder: 5,
                    health: 34f,
                    mana: 0f,
                    primaryAbilityCode: AbilityEnemyStrike,
                    manaAbilityCode: 0,
                    manaAbilityThreshold: 0f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    controlAbilityCode: AbilityPlayerControlStun,
                    controlTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    reviveGameplayEffectCode: GameplayEffectSelfRevive,
                    maxReviveCount: 1,
                    arcaneResistance: EnemyArcaneResistance),
            };
        }

        private static UnitDefinition[] CreatePlayerAdvantageUnits()
        {
            var units = CreateDefaultUnits();
            units[0] = units[0].WithStats(84f, 1f);
            units[1] = units[1].WithStats(62f, 3f);
            units[2] = units[2].WithStats(58f, 1f);
            units[3] = units[3].WithStats(36f, 0f);
            units[4] = units[4].WithStats(30f, 0f);
            units[5] = units[5].WithStats(28f, 0f);
            return units;
        }

        private static UnitDefinition[] CreateEnemyPressureUnits()
        {
            var defaultUnits = CreateDefaultUnits();
            var units = new UnitDefinition[defaultUnits.Length + 1];
            for (var i = 0; i < defaultUnits.Length; i++)
                units[i] = defaultUnits[i];

            units[0] = units[0].WithStats(66f, 0f);
            units[1] = units[1].WithStats(48f, 3f);
            units[2] = units[2].WithStats(26f, 0f);
            units[3] = units[3].WithStats(50f, 0f);
            units[4] = units[4].WithStats(42f, 0f);
            units[5] = units[5].WithStats(38f, 0f);
            units[6] = new UnitDefinition(
                "enemy-vanguard",
                HeadlessAutoChessTeam.Enemy,
                slot: 3,
                boardX: 4,
                boardY: 0,
                turnOrder: 6,
                health: 28f,
                mana: 0f,
                primaryAbilityCode: AbilityEnemyStrike,
                manaAbilityCode: 0,
                manaAbilityThreshold: 0f,
                primaryTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                manaTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                arcaneResistance: EnemyArcaneResistance);
            return units;
        }

        private static UnitDefinition[] CreateLargeBoardUnits()
        {
            return new[]
            {
                new UnitDefinition(
                    "player-guardian",
                    HeadlessAutoChessTeam.Player,
                    slot: 0,
                    boardX: 1,
                    boardY: 1,
                    turnOrder: 0,
                    health: 82f,
                    mana: 0f,
                    primaryAbilityCode: AbilityPlayerStrike,
                    manaAbilityCode: 0,
                    manaAbilityThreshold: 0f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    supportAbilityCode: AbilityPlayerBarrier,
                    supportTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    killManaGainGameplayEffectCode: GameplayEffectKillManaGain,
                    equipmentGameplayEffectCode: GameplayEffectPlayerCounterGear,
                    counterDamageGameplayEffectCode: GameplayEffectPlayerCounterDamage),
                new UnitDefinition(
                    "enemy-brute",
                    HeadlessAutoChessTeam.Enemy,
                    slot: 0,
                    boardX: 4,
                    boardY: 1,
                    turnOrder: 1,
                    health: 44f,
                    mana: 0f,
                    primaryAbilityCode: AbilityEnemyStrike,
                    manaAbilityCode: 0,
                    manaAbilityThreshold: 0f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    arcaneResistance: EnemyArcaneResistance),
                new UnitDefinition(
                    "player-mage",
                    HeadlessAutoChessTeam.Player,
                    slot: 1,
                    boardX: 0,
                    boardY: 0,
                    turnOrder: 2,
                    health: 58f,
                    mana: 3f,
                    primaryAbilityCode: AbilityPlayerStrike,
                    manaAbilityCode: AbilityPlayerManaBurst,
                    manaAbilityThreshold: 3f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    controlAbilityCode: AbilityPlayerControlStun,
                    controlTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    supportAbilityCode: AbilityPlayerBarrier,
                    supportTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    synergyCode: SynergyArcane,
                    synergyThreshold: SynergyArcaneThreshold,
                    synergyAllyBuffGameplayEffectCode: GameplayEffectArcaneTeamBuff,
                    synergyEnemyDebuffGameplayEffectCode: GameplayEffectArcaneStormDebuff,
                    killManaGainGameplayEffectCode: GameplayEffectKillManaGain,
                    equipmentGameplayEffectCode: GameplayEffectPlayerCounterGear,
                    counterDamageGameplayEffectCode: GameplayEffectPlayerCounterDamage),
                new UnitDefinition(
                    "enemy-rogue",
                    HeadlessAutoChessTeam.Enemy,
                    slot: 1,
                    boardX: 5,
                    boardY: 0,
                    turnOrder: 3,
                    health: 36f,
                    mana: 0f,
                    primaryAbilityCode: AbilityEnemyStrike,
                    manaAbilityCode: 0,
                    manaAbilityThreshold: 0f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    arcaneResistance: EnemyArcaneResistance),
                new UnitDefinition(
                    "player-ranger",
                    HeadlessAutoChessTeam.Player,
                    slot: 2,
                    boardX: 0,
                    boardY: 2,
                    turnOrder: 4,
                    health: 52f,
                    mana: 0f,
                    primaryAbilityCode: AbilityPlayerStrike,
                    manaAbilityCode: 0,
                    manaAbilityThreshold: 0f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    supportAbilityCode: AbilityPlayerCleanse,
                    supportTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    summonAbilityCode: AbilityPlayerSummon,
                    maxActiveSummons: PlayerSummonMaxActiveCount,
                    synergyCode: SynergyArcane,
                    synergyThreshold: SynergyArcaneThreshold,
                    synergyAllyBuffGameplayEffectCode: GameplayEffectArcaneTeamBuff,
                    synergyEnemyDebuffGameplayEffectCode: GameplayEffectArcaneStormDebuff,
                    killManaGainGameplayEffectCode: GameplayEffectKillManaGain,
                    equipmentGameplayEffectCode: GameplayEffectPlayerCounterGear,
                    counterDamageGameplayEffectCode: GameplayEffectPlayerCounterDamage),
                new UnitDefinition(
                    "enemy-shaman",
                    HeadlessAutoChessTeam.Enemy,
                    slot: 2,
                    boardX: 5,
                    boardY: 2,
                    turnOrder: 5,
                    health: 34f,
                    mana: 0f,
                    primaryAbilityCode: AbilityEnemyStrike,
                    manaAbilityCode: 0,
                    manaAbilityThreshold: 0f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    controlAbilityCode: AbilityPlayerControlStun,
                    controlTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    reviveGameplayEffectCode: GameplayEffectSelfRevive,
                    maxReviveCount: 1,
                    arcaneResistance: EnemyArcaneResistance),
                new UnitDefinition(
                    "player-cleric",
                    HeadlessAutoChessTeam.Player,
                    slot: 3,
                    boardX: 1,
                    boardY: 0,
                    turnOrder: 6,
                    health: 58f,
                    mana: 1f,
                    primaryAbilityCode: AbilityPlayerStrike,
                    manaAbilityCode: 0,
                    manaAbilityThreshold: 0f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    synergyCode: SynergyArcane,
                    synergyThreshold: SynergyArcaneThreshold,
                    synergyAllyBuffGameplayEffectCode: GameplayEffectArcaneTeamBuff,
                    synergyEnemyDebuffGameplayEffectCode: GameplayEffectArcaneStormDebuff,
                    supportAbilityCode: AbilityPlayerBarrier,
                    supportTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    killManaGainGameplayEffectCode: GameplayEffectKillManaGain),
                new UnitDefinition(
                    "enemy-vanguard",
                    HeadlessAutoChessTeam.Enemy,
                    slot: 3,
                    boardX: 4,
                    boardY: 0,
                    turnOrder: 7,
                    health: 40f,
                    mana: 0f,
                    primaryAbilityCode: AbilityEnemyStrike,
                    manaAbilityCode: 0,
                    manaAbilityThreshold: 0f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.Frontline,
                    arcaneResistance: EnemyArcaneResistance),
                new UnitDefinition(
                    "player-battlemage",
                    HeadlessAutoChessTeam.Player,
                    slot: 4,
                    boardX: 1,
                    boardY: 2,
                    turnOrder: 8,
                    health: 56f,
                    mana: 3f,
                    primaryAbilityCode: AbilityPlayerStrike,
                    manaAbilityCode: AbilityPlayerManaBurst,
                    manaAbilityThreshold: 3f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    controlAbilityCode: AbilityPlayerControlStun,
                    controlTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    synergyCode: SynergyArcane,
                    synergyThreshold: SynergyArcaneThreshold,
                    synergyAllyBuffGameplayEffectCode: GameplayEffectArcaneTeamBuff,
                    synergyEnemyDebuffGameplayEffectCode: GameplayEffectArcaneStormDebuff,
                    killManaGainGameplayEffectCode: GameplayEffectKillManaGain),
                new UnitDefinition(
                    "enemy-archer",
                    HeadlessAutoChessTeam.Enemy,
                    slot: 4,
                    boardX: 5,
                    boardY: 1,
                    turnOrder: 9,
                    health: 32f,
                    mana: 0f,
                    primaryAbilityCode: AbilityEnemyStrike,
                    manaAbilityCode: 0,
                    manaAbilityThreshold: 0f,
                    primaryTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    manaTargetPolicy: HeadlessAutoChessTargetPolicy.LowestHealth,
                    arcaneResistance: EnemyArcaneResistance),
            };
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

            GameplayEffectRequestWriter.ApplyFastOrCreateSingleTargetRequest(
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

        private static void RefreshUnits(ScenarioState state)
        {
            for (var i = 0; i < state.Units.Length; i++)
                state.Units[i] = state.Units[i].Refresh();
        }

        private static bool TryResolveWinner(ScenarioState state, out HeadlessAutoChessTeam winner)
        {
            var playerAlive = false;
            var enemyAlive = false;

            for (var i = 0; i < state.Units.Length; i++)
            {
                var unit = state.Units[i];
                if (!unit.CanStillParticipateInResolution)
                    continue;

                if (unit.Definition.Team == HeadlessAutoChessTeam.Player)
                    playerAlive = true;
                else if (unit.Definition.Team == HeadlessAutoChessTeam.Enemy)
                    enemyAlive = true;
            }

            if (playerAlive && enemyAlive)
            {
                winner = HeadlessAutoChessTeam.None;
                return false;
            }

            winner = playerAlive == enemyAlive
                ? HeadlessAutoChessTeam.Draw
                : playerAlive
                    ? HeadlessAutoChessTeam.Player
                    : HeadlessAutoChessTeam.Enemy;
            return true;
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

            var peakTick = default(BGasRuntimeDiagnosticEvent);
            var peakBuffer = default(BGasRuntimeDiagnosticEvent);
            var hasPeakTick = false;
            var hasPeakBuffer = false;
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

            if (hasPeakTick || hasPeakBuffer)
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

        private static void TickRuntime()
        {
            TickRuntimeMeasured();
        }

        private static RuntimeTickGroupTiming TickRuntimeMeasured(
            HeadlessAutoChessRuntimeSystemTimingCollector systemTimingCollector = null)
        {
            var world = GASManager.ExWorld;
            var command = world.GetExistingSystemManaged<GASCommandGroup>();
            var resetDirty = world.GetExistingSystemManaged<GASResetDirtyGroup>();
            var tag = world.GetExistingSystemManaged<GASTagGroup>();
            var effect = world.GetExistingSystemManaged<GASEffectGroup>();
            var attribute = world.GetExistingSystemManaged<GASAttributeGroup>();
            var ability = world.GetExistingSystemManaged<GASAbilityGroup>();
            var cue = world.GetExistingSystemManaged<GASCueGroup>();

            var totalStartTicks = Stopwatch.GetTimestamp();
            var commandTicks = UpdateGroupMeasured(command, "Command", systemTimingCollector);
            var resetDirtyTicks = UpdateGroupMeasured(resetDirty, "ResetDirty", systemTimingCollector);
            var tagTicks = UpdateGroupMeasured(tag, "Tag", systemTimingCollector);
            var effectTicks = UpdateGroupMeasured(effect, "Effect", systemTimingCollector);
            var attributeTicks = UpdateGroupMeasured(attribute, "Attribute", systemTimingCollector);
            var abilityTicks = UpdateGroupMeasured(ability, "Ability", systemTimingCollector);
            var cueTicks = UpdateGroupMeasured(cue, "Cue", systemTimingCollector);
            var totalTicks = Stopwatch.GetTimestamp() - totalStartTicks;

            return new RuntimeTickGroupTiming(
                totalTicks,
                commandTicks,
                resetDirtyTicks,
                tagTicks,
                effectTicks,
                attributeTicks,
                abilityTicks,
                cueTicks);
        }

        private static void RecordRuntimeDiagnostics(in RuntimeTickGroupTiming timing)
        {
            var em = GASManager.EntityManager;
            var debugger = GASManager.EntityRuntimeDebugger;
            if (debugger == Entity.Null || !em.Exists(debugger))
                return;

            var frame = GASManager.CurrentFrame;
            GasRuntimeDebugger.RecordTickTiming(
                em,
                debugger,
                frame,
                timing.TotalTicks,
                timing.CommandTicks,
                timing.ResetDirtyTicks,
                timing.TagTicks,
                timing.EffectTicks,
                timing.AttributeTicks,
                timing.AbilityTicks,
                timing.CueTicks,
                Stopwatch.Frequency);
            GasRuntimeDebugger.RecordEventBusPressure(
                em,
                debugger,
                GASManager.EntityEventBus,
                frame);
        }

        private static void RecordSystemTimingDiagnostics(HeadlessAutoChessSystemTiming[] systemTimings)
        {
            if (systemTimings == null || systemTimings.Length == 0)
                return;

            var em = GASManager.EntityManager;
            var debugger = GASManager.EntityRuntimeDebugger;
            if (debugger == Entity.Null || !em.Exists(debugger))
                return;

            var frame = GASManager.CurrentFrame;
            for (var i = 0; i < systemTimings.Length; i++)
            {
                var timing = systemTimings[i];
                GasRuntimeDebugger.RecordSystemTimingAggregate(
                    em,
                    debugger,
                    frame,
                    timing.GroupName,
                    timing.SystemName,
                    timing.CallCount,
                    timing.ElapsedTicks,
                    Stopwatch.Frequency);
            }
        }

        private static long UpdateGroupMeasured(
            ComponentSystemGroup group,
            string groupName,
            HeadlessAutoChessRuntimeSystemTimingCollector systemTimingCollector)
        {
            var startTicks = Stopwatch.GetTimestamp();
            if (systemTimingCollector == null)
            {
                group.Update();
            }
            else
            {
                var systems = systemTimingCollector.GetSystems(group, groupName);
                for (var i = 0; i < systems.Length; i++)
                    UpdateSystemMeasured(group.World, systems[i], groupName, systemTimingCollector);
            }

            return Stopwatch.GetTimestamp() - startTicks;
        }

        private static long UpdateSystemMeasured(
            World world,
            SystemHandle system,
            string groupName,
            HeadlessAutoChessRuntimeSystemTimingCollector systemTimingCollector)
        {
            var systemTypeIndex = world.Unmanaged.GetSystemTypeIndex(system);
            var systemName = GetShortSystemName(systemTypeIndex);
            var managedSystem = world.GetExistingSystemManaged(systemTypeIndex);
            var childGroup = managedSystem as ComponentSystemGroup;

            if (childGroup != null)
            {
                var childGroupName = string.IsNullOrEmpty(groupName)
                    ? systemName
                    : groupName + "/" + systemName;
                return UpdateGroupMeasured(childGroup, childGroupName, systemTimingCollector);
            }

            var startTicks = Stopwatch.GetTimestamp();
            system.Update(world.Unmanaged);
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            systemTimingCollector.Record(groupName, systemName, elapsedTicks);
            return elapsedTicks;
        }

        private static string GetShortSystemName(SystemTypeIndex systemTypeIndex)
        {
            var systemName = TypeManager.GetSystemName(systemTypeIndex).ToString();
            var separatorIndex = systemName.LastIndexOf('.');
            return separatorIndex >= 0 && separatorIndex + 1 < systemName.Length
                ? systemName.Substring(separatorIndex + 1)
                : systemName;
        }

        private static float GetAttribute(Entity asc, int attributeCode)
        {
            var em = GASManager.EntityManager;
            if (asc == Entity.Null || !em.Exists(asc) || !em.HasBuffer<BAttribute>(asc))
                return 0f;

            var attributes = em.GetBuffer<BAttribute>(asc);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttrSetCode == AttributeSetCombat && attribute.Code == attributeCode)
                    return attribute.CurrentValue;
            }

            return 0f;
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

        private static void ResetObservationState(in HeadlessAutoChessOptions normalizedOptions)
        {
            var em = GASManager.EntityManager;
            if (em.Exists(GASManager.EntityGlobalTimer))
                em.SetComponentData(GASManager.EntityGlobalTimer, new GlobalTimer());

            if (em.Exists(GASManager.EntityEventBus))
            {
                em.SetComponentData(GASManager.EntityEventBus, new CGameplayEventBus());
                if (em.HasComponent<CPresentationOutboxProjectionState>(GASManager.EntityEventBus))
                    em.SetComponentData(GASManager.EntityEventBus, new CPresentationOutboxProjectionState());
                if (em.HasComponent<CPresentationOutboxProjectionOptions>(GASManager.EntityEventBus))
                {
                    em.SetComponentData(GASManager.EntityEventBus, new CPresentationOutboxProjectionOptions
                    {
                        ProjectRawFacts = normalizedOptions.ProjectRawPresentationOutbox ? (byte)1 : (byte)0,
                    });
                }
                ClearBuffer<BDamageEvent>(em, GASManager.EntityEventBus);
                ClearBuffer<BTagChangeEvent>(em, GASManager.EntityEventBus);
                ClearBuffer<BGameplayEvent>(em, GASManager.EntityEventBus);
                ClearBuffer<BAttributeChangeEvent>(em, GASManager.EntityEventBus);
                ClearBuffer<BCueRequest>(em, GASManager.EntityEventBus);
                ClearBuffer<BPresentationOutboxOwner>(em, GASManager.EntityEventBus);
            }

            if (em.Exists(GASManager.EntityEventLogSink))
            {
                em.SetComponentData(GASManager.EntityEventLogSink, new CGameplayEventLogSink());
                ClearBuffer<BDebugReplayEvent>(em, GASManager.EntityEventLogSink);
            }
        }

        private static void ResetRuntimeDebugger(in HeadlessAutoChessOptions normalizedOptions)
        {
            var em = GASManager.EntityManager;
            var debugger = GASManager.EntityRuntimeDebugger;
            if (debugger == Entity.Null || !em.Exists(debugger))
                return;

            GasRuntimeDebugger.Reset(em, debugger);
            GasRuntimeDebugger.Configure(
                em,
                debugger,
                enabled: true,
                captureSystemTimings: normalizedOptions.CollectSystemTimings,
                captureBufferPressure: true);
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

            using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<CHeadlessAutoChessSummonedUnit>()))
            using (var summons = query.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < summons.Length; i++)
                    DestroyAscRuntime(em, summons[i]);
            }

            for (var i = 0; i < state.Units.Length; i++)
                DestroyAscRuntime(em, state.Units[i].Facade.Entity);
        }

        private static void RestoreDefaultObservationOptions()
        {
            var em = GASManager.EntityManager;
            if (em.Exists(GASManager.EntityEventBus)
                && em.HasComponent<CPresentationOutboxProjectionOptions>(GASManager.EntityEventBus))
            {
                em.SetComponentData(GASManager.EntityEventBus, new CPresentationOutboxProjectionOptions
                {
                    ProjectRawFacts = 1,
                });
            }
        }

        private static void DestroyAscRuntime(EntityManager em, Entity asc)
        {
            if (asc == Entity.Null || !em.Exists(asc))
                return;

            DestroyGrantedAbilities(em, asc);
            DestroyActiveEffects(em, asc);
            em.DestroyEntity(asc);
        }

        private static void DestroyGrantedAbilities(EntityManager em, Entity asc)
        {
            if (!em.HasBuffer<BGrantedAbility>(asc))
                return;

            var abilities = em.GetBuffer<BGrantedAbility>(asc);
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

                    if (em.HasComponent<CAbilityConfig>(ability))
                    {
                        var config = em.GetComponentData<CAbilityConfig>(ability).Config;
                        if (config.IsCreated)
                            config.Dispose();
                    }

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
            if (!em.HasBuffer<BGameplayEffect>(asc))
                return;

            var effects = em.GetBuffer<BGameplayEffect>(asc);
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
            public readonly HeadlessAutoChessTeam Team;
            public readonly int Slot;
            public readonly int BoardX;
            public readonly int BoardY;
            public readonly int TurnOrder;
            public readonly float Health;
            public readonly float Mana;
            public readonly int PrimaryAbilityCode;
            public readonly int ManaAbilityCode;
            public readonly int ControlAbilityCode;
            public readonly int SupportAbilityCode;
            public readonly int SummonAbilityCode;
            public readonly float ManaAbilityThreshold;
            public readonly HeadlessAutoChessTargetPolicy PrimaryTargetPolicy;
            public readonly HeadlessAutoChessTargetPolicy ManaTargetPolicy;
            public readonly HeadlessAutoChessTargetPolicy ControlTargetPolicy;
            public readonly HeadlessAutoChessTargetPolicy SupportTargetPolicy;
            public readonly int SynergyCode;
            public readonly int SynergyThreshold;
            public readonly int SynergyAllyBuffGameplayEffectCode;
            public readonly int SynergyEnemyDebuffGameplayEffectCode;
            public readonly int KillManaGainGameplayEffectCode;
            public readonly int ReviveGameplayEffectCode;
            public readonly int MaxReviveCount;
            public readonly int MaxActiveSummons;
            public readonly float ArcaneResistance;
            public readonly int EquipmentGameplayEffectCode;
            public readonly int CounterDamageGameplayEffectCode;
            public readonly float InitialHealth;

            public UnitDefinition(
                string id,
                HeadlessAutoChessTeam team,
                int slot,
                int boardX,
                int boardY,
                int turnOrder,
                float health,
                float mana,
                int primaryAbilityCode,
                int manaAbilityCode,
                float manaAbilityThreshold,
                HeadlessAutoChessTargetPolicy primaryTargetPolicy,
                HeadlessAutoChessTargetPolicy manaTargetPolicy,
                int controlAbilityCode = 0,
                HeadlessAutoChessTargetPolicy controlTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                int supportAbilityCode = 0,
                HeadlessAutoChessTargetPolicy supportTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                int summonAbilityCode = 0,
                int maxActiveSummons = 0,
                int synergyCode = 0,
                int synergyThreshold = 0,
                int synergyAllyBuffGameplayEffectCode = 0,
                int synergyEnemyDebuffGameplayEffectCode = 0,
                int killManaGainGameplayEffectCode = 0,
                int reviveGameplayEffectCode = 0,
                int maxReviveCount = 0,
                float arcaneResistance = 0f,
                int equipmentGameplayEffectCode = 0,
                int counterDamageGameplayEffectCode = 0,
                float initialHealth = -1f)
            {
                Id = id;
                Team = team;
                Slot = slot;
                BoardX = boardX;
                BoardY = boardY;
                TurnOrder = turnOrder;
                Health = health;
                Mana = mana;
                PrimaryAbilityCode = primaryAbilityCode;
                ManaAbilityCode = manaAbilityCode;
                ControlAbilityCode = controlAbilityCode;
                SupportAbilityCode = supportAbilityCode;
                SummonAbilityCode = summonAbilityCode;
                ManaAbilityThreshold = manaAbilityThreshold;
                PrimaryTargetPolicy = primaryTargetPolicy;
                ManaTargetPolicy = manaTargetPolicy;
                ControlTargetPolicy = controlTargetPolicy;
                SupportTargetPolicy = supportTargetPolicy;
                SynergyCode = synergyCode;
                SynergyThreshold = synergyThreshold;
                SynergyAllyBuffGameplayEffectCode = synergyAllyBuffGameplayEffectCode;
                SynergyEnemyDebuffGameplayEffectCode = synergyEnemyDebuffGameplayEffectCode;
                KillManaGainGameplayEffectCode = killManaGainGameplayEffectCode;
                ReviveGameplayEffectCode = reviveGameplayEffectCode;
                MaxReviveCount = maxReviveCount;
                MaxActiveSummons = maxActiveSummons;
                ArcaneResistance = arcaneResistance;
                EquipmentGameplayEffectCode = equipmentGameplayEffectCode;
                CounterDamageGameplayEffectCode = counterDamageGameplayEffectCode;
                InitialHealth = initialHealth > 0f && initialHealth < health ? initialHealth : health;
            }

            public UnitDefinition WithStats(float health, float mana)
            {
                var initialHealth = health;
                if (InitialHealth > 0f && InitialHealth < Health)
                {
                    var deficit = Health - InitialHealth;
                    initialHealth = health > deficit ? health - deficit : health;
                }

                return new UnitDefinition(
                    Id,
                    Team,
                    Slot,
                    BoardX,
                    BoardY,
                    TurnOrder,
                    health,
                    mana,
                    PrimaryAbilityCode,
                    ManaAbilityCode,
                    ManaAbilityThreshold,
                    PrimaryTargetPolicy,
                    ManaTargetPolicy,
                    ControlAbilityCode,
                    ControlTargetPolicy,
                    SupportAbilityCode,
                    SupportTargetPolicy,
                    SummonAbilityCode,
                    MaxActiveSummons,
                    SynergyCode,
                    SynergyThreshold,
                    SynergyAllyBuffGameplayEffectCode,
                    SynergyEnemyDebuffGameplayEffectCode,
                    KillManaGainGameplayEffectCode,
                    ReviveGameplayEffectCode,
                    MaxReviveCount,
                    ArcaneResistance,
                    EquipmentGameplayEffectCode,
                    CounterDamageGameplayEffectCode,
                    initialHealth);
            }

            public UnitDefinition WithScaleCopy(int copyIndex, int turnOrderOffset)
            {
                return new UnitDefinition(
                    Id + "#" + copyIndex.ToString("D4", CultureInfo.InvariantCulture),
                    Team,
                    Slot + copyIndex * 100,
                    BoardX,
                    (BoardY + copyIndex) % BoardHeight,
                    TurnOrder + turnOrderOffset,
                    Health,
                    Mana,
                    PrimaryAbilityCode,
                    ManaAbilityCode,
                    ManaAbilityThreshold,
                    PrimaryTargetPolicy,
                    ManaTargetPolicy,
                    ControlAbilityCode,
                    ControlTargetPolicy,
                    SupportAbilityCode,
                    SupportTargetPolicy,
                    SummonAbilityCode,
                    MaxActiveSummons,
                    SynergyCode,
                    SynergyThreshold,
                    SynergyAllyBuffGameplayEffectCode,
                    SynergyEnemyDebuffGameplayEffectCode,
                    KillManaGainGameplayEffectCode,
                    ReviveGameplayEffectCode,
                    MaxReviveCount,
                    ArcaneResistance,
                    EquipmentGameplayEffectCode,
                    CounterDamageGameplayEffectCode,
                    InitialHealth);
            }

            public int[] CreateAbilityCodes()
            {
                var codes = new List<int>(5);
                if (PrimaryAbilityCode > 0)
                    codes.Add(PrimaryAbilityCode);
                if (ManaAbilityCode > 0)
                    codes.Add(ManaAbilityCode);
                if (ControlAbilityCode > 0)
                    codes.Add(ControlAbilityCode);
                if (SupportAbilityCode > 0)
                    codes.Add(SupportAbilityCode);
                if (SummonAbilityCode > 0)
                    codes.Add(SummonAbilityCode);

                return codes.ToArray();
            }
        }

        private readonly struct UnitRuntime
        {
            public readonly UnitDefinition Definition;
            public readonly AbilitySystemFacade Facade;
            public readonly float Health;
            public readonly float Mana;
            public readonly float Shield;
            public readonly float ArcaneResistance;
            public readonly bool Alive;
            public readonly bool RevivePending;
            public readonly int ReviveCount;

            public UnitRuntime(UnitDefinition definition)
                : this(
                    definition,
                    default,
                    definition.Health,
                    definition.Mana,
                    0f,
                    definition.ArcaneResistance,
                    false,
                    false,
                    0)
            {
            }

            private UnitRuntime(
                UnitDefinition definition,
                AbilitySystemFacade facade,
                float health,
                float mana,
                float shield,
                float arcaneResistance,
                bool alive,
                bool revivePending,
                int reviveCount)
            {
                Definition = definition;
                Facade = facade;
                Health = health;
                Mana = mana;
                Shield = shield;
                ArcaneResistance = arcaneResistance;
                Alive = alive;
                RevivePending = revivePending;
                ReviveCount = reviveCount;
            }

            public UnitRuntime WithFacade(AbilitySystemFacade facade)
            {
                return new UnitRuntime(
                    Definition,
                    facade,
                    Health,
                    Mana,
                    Shield,
                    ArcaneResistance,
                    true,
                    RevivePending,
                    ReviveCount);
            }

            public UnitRuntime Refresh()
            {
                var health = GetAttribute(Facade.Entity, AttributeHealth);
                var mana = GetAttribute(Facade.Entity, AttributeMana);
                var shield = GetAttribute(Facade.Entity, AttributeShield);
                var arcaneResistance = GetAttribute(Facade.Entity, AttributeArcaneResistance);
                var revivePending = false;
                var reviveCount = 0;
                var em = GASManager.EntityManager;
                if (Facade.Entity != Entity.Null
                    && em.Exists(Facade.Entity)
                    && em.HasComponent<CHeadlessAutoChessPassiveState>(Facade.Entity))
                {
                    var passiveState = em.GetComponentData<CHeadlessAutoChessPassiveState>(Facade.Entity);
                    revivePending = passiveState.RevivePending;
                    reviveCount = passiveState.ReviveCount;
                }

                return new UnitRuntime(
                    Definition,
                    Facade,
                    health,
                    mana,
                    shield,
                    arcaneResistance,
                    health > 0f,
                    revivePending,
                    reviveCount);
            }

            public bool CanStillParticipateInResolution
            {
                get
                {
                    return Alive
                           || RevivePending
                           || (Definition.ReviveGameplayEffectCode > 0
                               && Definition.MaxReviveCount > 0
                               && ReviveCount < Definition.MaxReviveCount);
                }
            }
        }

        private sealed class ScenarioState
        {
            public readonly UnitRuntime[] Units;
            public Entity DriverEntity;
            public HeadlessAutoChessPresentationOutboxCounts PresentationOutboxCounts;

            public ScenarioState(UnitDefinition[] definitions)
            {
                Units = new UnitRuntime[definitions.Length];
                for (var i = 0; i < definitions.Length; i++)
                    Units[i] = new UnitRuntime(definitions[i]);
            }
        }

    }

    public sealed class HeadlessAutoChessNoopCue : GameplayCueBase<XParamNone>
    {
    }
}
