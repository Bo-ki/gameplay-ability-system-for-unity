using System.Globalization;
using System.IO;
using NUnit.Framework;

namespace GAS.Runtime.Tests.AutoChess
{
    public sealed class HeadlessAutoChessScenarioTests
    {
        [Test]
        public void RunDefaultSettlesAutoChessBattleFromReplayAndStructuredLog()
        {
            var result = HeadlessAutoChessScenario.RunDefault(
                new HeadlessAutoChessOptions(maxTicks: 128, postVictoryFlushTicks: 4));

            Assert.That(result.Completed, Is.True, result.AssertionLog);
            Assert.That(result.Variant, Is.EqualTo(HeadlessAutoChessScenarioVariant.DefaultBalanced));
            Assert.That(result.VariantName, Is.EqualTo("DefaultBalanced"));
            Assert.That(result.Winner, Is.EqualTo(HeadlessAutoChessTeam.Player), result.AssertionLog);
            Assert.That(result.BoardWidth, Is.EqualTo(HeadlessAutoChessScenario.BoardWidth));
            Assert.That(result.BoardHeight, Is.EqualTo(HeadlessAutoChessScenario.BoardHeight));
            Assert.That(result.Round, Is.GreaterThanOrEqualTo(1));
            Assert.That(result.TurnCount, Is.GreaterThan(0));
            Assert.That(result.BattleTicks, Is.GreaterThan(0));
            Assert.That(result.TotalTicks, Is.GreaterThanOrEqualTo(result.BattleTicks));
            Assert.That(result.AverageTickMilliseconds, Is.GreaterThanOrEqualTo(0d));
            Assert.That(result.ValidationReport.Passed, Is.True, result.ValidationReport.SummaryText);

            Assert.That(result.DriverIssuedCommands, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.DriverIssuedPrimaryCommands, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.DriverIssuedManaAbilityCommands, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.DriverIssuedControlAbilityCommands, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.DriverIssuedSupportAbilityCommands, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.DriverIssuedSummonAbilityCommands, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.CrowdControlTurnSkippedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.DriverFrontlineTargetSelections, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.DriverLowestHealthTargetSelections, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.PlayerDefeatedCount, Is.EqualTo(0), result.AssertionLog);
            Assert.That(result.EnemyDefeatedCount, Is.EqualTo(4), result.AssertionLog);
            Assert.That(result.FirstDefeatFrame, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.LastDefeatFrame, Is.GreaterThanOrEqualTo(result.FirstDefeatFrame), result.AssertionLog);
            Assert.That(result.BattleResolvedFrame, Is.GreaterThanOrEqualTo(result.LastDefeatFrame), result.AssertionLog);
            Assert.That(result.PassiveTriggeredCount, Is.GreaterThanOrEqualTo(2), result.AssertionLog);
            Assert.That(result.KillManaGrantedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.ReviveRequestedCount, Is.EqualTo(1), result.AssertionLog);
            Assert.That(result.ReviveAppliedCount, Is.EqualTo(1), result.AssertionLog);
            Assert.That(result.SynergyActivatedCount, Is.EqualTo(1), result.AssertionLog);
            Assert.That(result.SynergyExpiredCount, Is.EqualTo(0), result.AssertionLog);
            Assert.That(result.SynergyAllyBuffRequestedCount, Is.EqualTo(3), result.AssertionLog);
            Assert.That(result.SynergyEnemyDebuffRequestedCount, Is.EqualTo(3), result.AssertionLog);
            Assert.That(result.SynergyPeriodicTickCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.ShieldAppliedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.ShieldAbsorbedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.ShieldBrokenCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.DamageTypeResolved, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.DamageResisted, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EquipmentAppliedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.CounterTriggeredCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.CounterDamageAppliedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.CleanseRequestedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.CleanseAppliedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.CleanseEffectRemovedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.CleanseRallyRequestedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.CleanseRallyAppliedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.RallyComboTriggeredCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.RallyComboDamageAppliedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.LifeStealTriggeredCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.LifeStealHealedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.PoisonStackRequestedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.PoisonStackChangedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.PoisonOverflowTriggeredCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.PoisonOverflowDamageAppliedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.PoisonPeriodDamageAppliedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.ExecuteTriggeredCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.ExecuteDamageAppliedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.DeathBurstTriggeredCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.DeathBurstDamageAppliedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EnrageTriggeredCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EnrageAppliedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.SummonRequestedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.SummonSpawnedCount, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.SummonDespawnedCount, Is.GreaterThan(0), result.AssertionLog);

            Assert.That(result.Units, Has.Length.EqualTo(6));
            Assert.That(result.PlayerUnitCount, Is.EqualTo(3));
            Assert.That(result.EnemyUnitCount, Is.EqualTo(3));
            Assert.That(HasAliveUnit(result, HeadlessAutoChessTeam.Player), Is.True, result.AssertionLog);
            Assert.That(HasDefeatedUnit(result, HeadlessAutoChessTeam.Enemy), Is.True, result.AssertionLog);
            Assert.That(HasUnitAt(result, "player-mage", 0, 0), Is.True);
            Assert.That(HasUnitAt(result, "enemy-brute", 4, 1), Is.True);

            Assert.That(result.EventCounts.ReplayEvents, Is.GreaterThan(0));
            Assert.That(result.EventCounts.StructuredLogEntries, Is.EqualTo(result.EventCounts.ReplayEvents));
            Assert.That(result.EventCounts.AbilityCommitSucceeded, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.GameplayEffectInstanced, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.GameplayEffectApplied, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.AttributeChanges, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.HealthDamageAttributeChanges, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.TagChanges, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.CueRequests, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.DamageEvents, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.UnitDefeated, Is.EqualTo(4), result.AssertionLog);
            Assert.That(result.EventCounts.BattleResolved, Is.EqualTo(1), result.AssertionLog);
            Assert.That(result.EventCounts.PassiveTriggered, Is.EqualTo(result.PassiveTriggeredCount), result.AssertionLog);
            Assert.That(result.EventCounts.KillManaGranted, Is.EqualTo(result.KillManaGrantedCount), result.AssertionLog);
            Assert.That(result.EventCounts.ReviveRequested, Is.EqualTo(1), result.AssertionLog);
            Assert.That(result.EventCounts.ReviveApplied, Is.EqualTo(1), result.AssertionLog);
            Assert.That(result.EventCounts.SynergyActivated, Is.EqualTo(result.SynergyActivatedCount), result.AssertionLog);
            Assert.That(result.EventCounts.SynergyExpired, Is.EqualTo(result.SynergyExpiredCount), result.AssertionLog);
            Assert.That(result.EventCounts.SynergyAllyBuffRequested, Is.EqualTo(result.SynergyAllyBuffRequestedCount), result.AssertionLog);
            Assert.That(result.EventCounts.SynergyEnemyDebuffRequested, Is.EqualTo(result.SynergyEnemyDebuffRequestedCount), result.AssertionLog);
            Assert.That(result.EventCounts.SynergyPeriodicTicked, Is.EqualTo(result.SynergyPeriodicTickCount), result.AssertionLog);
            Assert.That(result.EventCounts.ControlTurnSkipped, Is.EqualTo(result.CrowdControlTurnSkippedCount), result.AssertionLog);
            Assert.That(result.EventCounts.ShieldApplied, Is.EqualTo(result.ShieldAppliedCount), result.AssertionLog);
            Assert.That(result.EventCounts.ShieldAbsorbed, Is.EqualTo(result.ShieldAbsorbedCount), result.AssertionLog);
            Assert.That(result.EventCounts.ShieldBroken, Is.EqualTo(result.ShieldBrokenCount), result.AssertionLog);
            Assert.That(result.EventCounts.DamageTypeResolved, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.DamageResisted, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.EquipmentApplied, Is.EqualTo(result.EquipmentAppliedCount), result.AssertionLog);
            Assert.That(result.EventCounts.CounterTriggered, Is.EqualTo(result.CounterTriggeredCount), result.AssertionLog);
            Assert.That(result.EventCounts.CounterDamageApplied, Is.EqualTo(result.CounterDamageAppliedCount), result.AssertionLog);
            Assert.That(result.EventCounts.CleanseRequested, Is.EqualTo(result.CleanseRequestedCount), result.AssertionLog);
            Assert.That(result.EventCounts.CleanseApplied, Is.EqualTo(result.CleanseAppliedCount), result.AssertionLog);
            Assert.That(result.EventCounts.CleanseEffectRemoved, Is.EqualTo(result.CleanseEffectRemovedCount), result.AssertionLog);
            Assert.That(result.EventCounts.CleanseRallyRequested, Is.EqualTo(result.CleanseRallyRequestedCount), result.AssertionLog);
            Assert.That(result.EventCounts.CleanseRallyApplied, Is.EqualTo(result.CleanseRallyAppliedCount), result.AssertionLog);
            Assert.That(result.EventCounts.RallyComboTriggered, Is.EqualTo(result.RallyComboTriggeredCount), result.AssertionLog);
            Assert.That(result.EventCounts.RallyComboDamageApplied, Is.EqualTo(result.RallyComboDamageAppliedCount), result.AssertionLog);
            Assert.That(result.EventCounts.LifeStealTriggered, Is.EqualTo(result.LifeStealTriggeredCount), result.AssertionLog);
            Assert.That(result.EventCounts.LifeStealHealed, Is.EqualTo(result.LifeStealHealedCount), result.AssertionLog);
            Assert.That(result.EventCounts.PoisonStackRequested, Is.EqualTo(result.PoisonStackRequestedCount), result.AssertionLog);
            Assert.That(result.EventCounts.PoisonStackChanged, Is.EqualTo(result.PoisonStackChangedCount), result.AssertionLog);
            Assert.That(result.EventCounts.PoisonOverflowTriggered, Is.EqualTo(result.PoisonOverflowTriggeredCount), result.AssertionLog);
            Assert.That(result.EventCounts.PoisonOverflowDamageApplied, Is.EqualTo(result.PoisonOverflowDamageAppliedCount), result.AssertionLog);
            Assert.That(result.EventCounts.PoisonPeriodDamageApplied, Is.EqualTo(result.PoisonPeriodDamageAppliedCount), result.AssertionLog);
            Assert.That(result.EventCounts.ExecuteTriggered, Is.EqualTo(result.ExecuteTriggeredCount), result.AssertionLog);
            Assert.That(result.EventCounts.ExecuteDamageApplied, Is.EqualTo(result.ExecuteDamageAppliedCount), result.AssertionLog);
            Assert.That(result.EventCounts.DeathBurstTriggered, Is.EqualTo(result.DeathBurstTriggeredCount), result.AssertionLog);
            Assert.That(result.EventCounts.DeathBurstDamageApplied, Is.EqualTo(result.DeathBurstDamageAppliedCount), result.AssertionLog);
            Assert.That(result.EventCounts.EnrageTriggered, Is.EqualTo(result.EnrageTriggeredCount), result.AssertionLog);
            Assert.That(result.EventCounts.EnrageApplied, Is.EqualTo(result.EnrageAppliedCount), result.AssertionLog);
            Assert.That(result.EventCounts.SummonRequested, Is.EqualTo(result.SummonRequestedCount), result.AssertionLog);
            Assert.That(result.EventCounts.SummonSpawned, Is.EqualTo(result.SummonSpawnedCount), result.AssertionLog);
            Assert.That(result.EventCounts.SummonDespawned, Is.EqualTo(result.SummonDespawnedCount), result.AssertionLog);
            Assert.That(result.EventCounts.PresentationUiMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.PresentationVfxMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.PresentationSfxMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.PresentationFloatingTextMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.PresentationCueMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.PresentationSettlementMarkers, Is.GreaterThan(0), result.AssertionLog);
            AssertPresentationOutboxMarkers(result);

            StringAssert.Contains("type=AbilityCommitSucceeded", result.AssertionLog);
            StringAssert.Contains("type=GameplayEffectInstanced", result.AssertionLog);
            StringAssert.Contains("type=GameplayEffectApplied", result.AssertionLog);
            StringAssert.Contains("type=AutoChessUnitDefeated", result.AssertionLog);
            StringAssert.Contains("type=AutoChessBattleResolved", result.AssertionLog);
            StringAssert.Contains("type=AutoChessPassiveTriggered", result.AssertionLog);
            StringAssert.Contains("type=AutoChessKillManaGranted", result.AssertionLog);
            StringAssert.Contains("type=AutoChessReviveRequested", result.AssertionLog);
            StringAssert.Contains("type=AutoChessReviveApplied", result.AssertionLog);
            StringAssert.Contains("type=AutoChessSynergyActivated", result.AssertionLog);
            StringAssert.Contains("type=AutoChessSynergyAllyBuffRequested", result.AssertionLog);
            StringAssert.Contains("type=AutoChessSynergyEnemyDebuffRequested", result.AssertionLog);
            StringAssert.Contains("type=AutoChessSynergyPeriodTicked", result.AssertionLog);
            StringAssert.Contains("type=AutoChessControlTurnSkipped", result.AssertionLog);
            StringAssert.Contains("type=AutoChessShieldApplied", result.AssertionLog);
            StringAssert.Contains("type=AutoChessShieldAbsorbed", result.AssertionLog);
            StringAssert.Contains("type=AutoChessShieldBroken", result.AssertionLog);
            StringAssert.Contains("type=AutoChessDamageTypeResolved", result.AssertionLog);
            StringAssert.Contains("type=AutoChessDamageResisted", result.AssertionLog);
            StringAssert.Contains("type=AutoChessEquipmentApplied", result.AssertionLog);
            StringAssert.Contains("type=AutoChessCounterTriggered", result.AssertionLog);
            StringAssert.Contains("type=AutoChessCounterDamageApplied", result.AssertionLog);
            AssertGameplayEventValue(
                result.AssertionLog,
                "AutoChessCounterTriggered",
                HeadlessAutoChessScenario.GameplayEffectPlayerCounterDamage,
                HeadlessAutoChessScenario.TagAutoChessCounterReady,
                HeadlessAutoChessScenario.MaxCounterDamageAmount);
            StringAssert.Contains("type=AutoChessCleanseRequested", result.AssertionLog);
            StringAssert.Contains("type=AutoChessCleanseApplied", result.AssertionLog);
            StringAssert.Contains("type=AutoChessCleanseEffectRemoved", result.AssertionLog);
            StringAssert.Contains("type=AutoChessCleanseRallyRequested", result.AssertionLog);
            StringAssert.Contains("type=AutoChessCleanseRallyApplied", result.AssertionLog);
            StringAssert.Contains("type=AutoChessRallyComboTriggered", result.AssertionLog);
            StringAssert.Contains("type=AutoChessRallyComboDamageApplied", result.AssertionLog);
            StringAssert.Contains("type=AutoChessLifeStealTriggered", result.AssertionLog);
            StringAssert.Contains("type=AutoChessLifeStealHealed", result.AssertionLog);
            StringAssert.Contains("type=AutoChessPoisonStackRequested", result.AssertionLog);
            StringAssert.Contains("type=AutoChessPoisonStackChanged", result.AssertionLog);
            StringAssert.Contains("type=AutoChessPoisonOverflowTriggered", result.AssertionLog);
            StringAssert.Contains("type=AutoChessPoisonOverflowDamageApplied", result.AssertionLog);
            StringAssert.Contains("type=AutoChessPoisonPeriodDamageApplied", result.AssertionLog);
            StringAssert.Contains("type=AutoChessExecuteTriggered", result.AssertionLog);
            StringAssert.Contains("type=AutoChessExecuteDamageApplied", result.AssertionLog);
            StringAssert.Contains("type=AutoChessDeathBurstTriggered", result.AssertionLog);
            StringAssert.Contains("type=AutoChessDeathBurstDamageApplied", result.AssertionLog);
            StringAssert.Contains("type=AutoChessEnrageTriggered", result.AssertionLog);
            StringAssert.Contains("type=AutoChessEnrageApplied", result.AssertionLog);
            StringAssert.Contains("type=AutoChessSummonRequested", result.AssertionLog);
            StringAssert.Contains("type=AutoChessSummonSpawned", result.AssertionLog);
            StringAssert.Contains("type=AutoChessSummonDespawned", result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.AttributeShield, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.DamageTypeArcane, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.AttributeCounterDamage, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.TagAutoChessCounterReady, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.GameplayEffectPlayerCounterDamage, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.GameplayEffectPlayerCleanse, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.GameplayEffectPlayerCleanseRally, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.GameplayEffectPlayerRallyComboDamage, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.GameplayEffectPlayerLifeStealHeal, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.SetByCallerLifeStealHealAmount, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.GameplayEffectPlayerPoisonStack, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.GameplayEffectPlayerPoisonOverflowDamage, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.GameplayEffectPlayerPoisonPeriodDamage, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.GameplayEffectPlayerExecuteGear, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.GameplayEffectPlayerExecuteDamage, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.DamageTypeExecute, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.GameplayEffectPlayerDeathBurstGear, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.GameplayEffectPlayerDeathBurstDamage, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.DamageTypeDeathBurst, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.GameplayEffectPlayerEnrage, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.SetByCallerCounterDamageAmount, result.AssertionLog);
            StringAssert.Contains(
                "type=AutoChessCounterDamageApplied|cueEvent=OnApply|event="
                + HeadlessAutoChessScenario.GameplayEffectPlayerCounterDamage
                + "|reason="
                + HeadlessAutoChessScenario.SetByCallerCounterDamageAmount,
                result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.PoisonStackingCode, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.DamageTypePoison, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.TagAutoChessCleanseRallied, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.TagAutoChessLifeStealReady, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.TagAutoChessPoisoned, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.TagAutoChessDeathBurstReady, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.TagAutoChessEnraged, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.TagAutoChessStunned, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.TagAutoChessShielded, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.SummonedUnitArcaneWisp, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.TagAutoChessSummoned, result.AssertionLog);
            StringAssert.Contains("event=" + HeadlessAutoChessScenario.GameplayEffectArcaneStormPeriodDamage, result.AssertionLog);
            StringAssert.Contains("kind=AttributeChange", result.AssertionLog);
            StringAssert.Contains("kind=Damage", result.AssertionLog);
            StringAssert.Contains("kind=TagChange", result.AssertionLog);
            StringAssert.Contains("kind=CueRequest", result.AssertionLog);
            StringAssert.Contains("domain=Presentation", result.AssertionLog);
            StringAssert.Contains("type=AutoChessPresentationUiMarker", result.AssertionLog);
            StringAssert.Contains("type=AutoChessPresentationVfxMarker", result.AssertionLog);
            StringAssert.Contains("type=AutoChessPresentationSfxMarker", result.AssertionLog);
            StringAssert.Contains("type=AutoChessPresentationFloatingTextMarker", result.AssertionLog);
            StringAssert.Contains("type=AutoChessPresentationCueMarker", result.AssertionLog);
            StringAssert.Contains("type=AutoChessPresentationSettlementMarker", result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiBattleStarted, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiCrowdControlSkipped, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueCrowdControl, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiShieldChanged, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxShield, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxShield, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextShield, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueShield, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiResistanceChanged, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxResistance, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxResistance, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextResistance, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueResistance, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiEquipmentChanged, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiCounterTriggered, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxCounter, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxCounter, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextCounter, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueCounter, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiCleanseTriggered, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxCleanse, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxCleanse, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextCleanse, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueCleanse, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiCleanseRallyTriggered, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxCleanseRally, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxCleanseRally, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextCleanseRally, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueCleanseRally, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiRallyComboTriggered, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxRallyCombo, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxRallyCombo, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextRallyCombo, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueRallyCombo, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiLifeStealTriggered, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxLifeSteal, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxLifeSteal, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextLifeSteal, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueLifeSteal, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiPoisonStacked, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxPoison, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxPoison, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextPoison, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CuePoison, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiExecuteTriggered, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxExecute, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxExecute, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextExecute, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueExecute, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiDeathBurstTriggered, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxDeathBurst, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxDeathBurst, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextDeathBurst, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueDeathBurst, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiEnrageTriggered, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxEnrage, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxEnrage, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextEnrage, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueEnrage, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiSummonSpawned, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxSummon, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxSummon, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextSummon, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueSummon, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueRequest, result.AssertionLog);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SettlementPanel, result.AssertionLog);
        }

        [Test]
        public void RunDefaultExportsValidationReportAndMeetsPerformanceGate()
        {
            var exportDirectory = Path.GetFullPath(Path.Combine("TestResults", "AutoChess", "T6-CHESS-AF"));
            var result = HeadlessAutoChessScenario.RunDefault(
                new HeadlessAutoChessOptions(
                    maxTicks: 128,
                    postVictoryFlushTicks: 4,
                    exportLogs: true,
                    exportDirectory: exportDirectory));
            var report = result.ValidationReport;

            Assert.That(result.Completed, Is.True, report.SummaryText);
            Assert.That(report.Passed, Is.True, report.SummaryText);
            Assert.That(report.FailureCount, Is.EqualTo(0), report.SummaryText);
            Assert.That(
                result.AverageTickMilliseconds,
                Is.LessThanOrEqualTo(report.Thresholds.MaxAverageTickMilliseconds),
                report.SummaryText);

            Assert.That(File.Exists(report.AssertionLogFile.Path), Is.True, report.SummaryText);
            Assert.That(File.Exists(report.HumanReadableLogFile.Path), Is.True, report.SummaryText);
            Assert.That(File.Exists(report.SummaryPath), Is.True, report.SummaryText);
            Assert.That(report.AssertionLogFile.ByteCount, Is.GreaterThan(0), report.SummaryText);
            Assert.That(report.HumanReadableLogFile.ByteCount, Is.GreaterThan(0), report.SummaryText);
            Assert.That(report.SummaryByteCount, Is.GreaterThan(0), report.SummaryText);

            var assertionText = File.ReadAllText(report.AssertionLogFile.Path);
            var humanText = File.ReadAllText(report.HumanReadableLogFile.Path);
            var summaryText = File.ReadAllText(report.SummaryPath);

            StringAssert.StartsWith("stats|", assertionText);
            StringAssert.Contains("type=AutoChessBattleResolved", assertionText);
            StringAssert.Contains("type=AutoChessSynergyPeriodTicked", assertionText);
            StringAssert.Contains("type=AutoChessCleanseApplied", assertionText);
            StringAssert.Contains("type=AutoChessCleanseRallyApplied", assertionText);
            StringAssert.Contains("type=AutoChessRallyComboDamageApplied", assertionText);
            StringAssert.Contains("type=AutoChessLifeStealHealed", assertionText);
            StringAssert.Contains("type=AutoChessPoisonOverflowDamageApplied", assertionText);
            StringAssert.Contains("type=AutoChessPoisonPeriodDamageApplied", assertionText);
            StringAssert.Contains("type=AutoChessExecuteDamageApplied", assertionText);
            StringAssert.Contains("type=AutoChessDeathBurstDamageApplied", assertionText);
            StringAssert.Contains("type=AutoChessEnrageApplied", assertionText);
            StringAssert.Contains("type=AutoChessSummonSpawned", assertionText);
            StringAssert.Contains("type=AutoChessPresentationCueMarker", assertionText);
            StringAssert.Contains("type=AutoChessPresentationSettlementMarker", assertionText);
            StringAssert.Contains("AutoChessBattleResolved", humanText);
            StringAssert.Contains("AutoChessSynergyPeriodTicked", humanText);
            StringAssert.Contains("AutoChessCounterTriggered", humanText);
            StringAssert.Contains("AutoChessCleanseApplied", humanText);
            StringAssert.Contains("AutoChessCleanseRallyApplied", humanText);
            StringAssert.Contains("AutoChessRallyComboDamageApplied", humanText);
            StringAssert.Contains("AutoChessLifeStealHealed", humanText);
            StringAssert.Contains("AutoChessPoisonOverflowDamageApplied", humanText);
            StringAssert.Contains("AutoChessPoisonPeriodDamageApplied", humanText);
            StringAssert.Contains("AutoChessExecuteDamageApplied", humanText);
            StringAssert.Contains("AutoChessDeathBurstDamageApplied", humanText);
            StringAssert.Contains("AutoChessEnrageApplied", humanText);
            StringAssert.Contains("AutoChessSummonSpawned", humanText);
            StringAssert.Contains("AutoChessPresentationCueMarker", humanText);
            StringAssert.Contains("passed=true", summaryText);
            StringAssert.Contains("facts|abilityCommitSucceeded=", summaryText);
            StringAssert.Contains("controlTurnSkipped=", summaryText);
            StringAssert.Contains("shieldAbsorbed=", summaryText);
            StringAssert.Contains("damageResisted=", summaryText);
            StringAssert.Contains("counterTriggered=", summaryText);
            StringAssert.Contains("counterDamageApplied=", summaryText);
            StringAssert.Contains("cleanseApplied=", summaryText);
            StringAssert.Contains("cleanseEffectRemoved=", summaryText);
            StringAssert.Contains("cleanseRallyApplied=", summaryText);
            StringAssert.Contains("rallyComboDamageApplied=", summaryText);
            StringAssert.Contains("lifeStealHealed=", summaryText);
            StringAssert.Contains("poisonOverflowDamageApplied=", summaryText);
            StringAssert.Contains("poisonPeriodDamageApplied=", summaryText);
            StringAssert.Contains("executeTriggered=", summaryText);
            StringAssert.Contains("executeDamageApplied=", summaryText);
            StringAssert.Contains("deathBurstTriggered=", summaryText);
            StringAssert.Contains("deathBurstDamageApplied=", summaryText);
            StringAssert.Contains("enrageTriggered=", summaryText);
            StringAssert.Contains("enrageApplied=", summaryText);
            StringAssert.Contains("summonSpawned=", summaryText);
            StringAssert.Contains("presentation|uiMarkers=", summaryText);
            StringAssert.Contains("presentationOutbox|events=", summaryText);
            StringAssert.Contains("presentationOutboxCodes|healthBarAttached=", summaryText);
            StringAssert.Contains("cueShield=", summaryText);
            StringAssert.Contains("cueResistance=", summaryText);
            StringAssert.Contains("cueCounter=", summaryText);
            StringAssert.Contains("cueCleanse=", summaryText);
            StringAssert.Contains("cueCleanseRally=", summaryText);
            StringAssert.Contains("cueRallyCombo=", summaryText);
            StringAssert.Contains("cueLifeSteal=", summaryText);
            StringAssert.Contains("cuePoison=", summaryText);
            StringAssert.Contains("cueExecute=", summaryText);
            StringAssert.Contains("cueDeathBurst=", summaryText);
            StringAssert.Contains("uiEnrage=", summaryText);
            StringAssert.Contains("enrageText=", summaryText);
            StringAssert.Contains("cueEnrage=", summaryText);
            StringAssert.Contains("cueSummon=", summaryText);
            StringAssert.Contains("thresholds|maxBattleTicks=", summaryText);
        }

        [Test]
        public void RunScaleValidationExportsBatchSummaryAndStaysDeterministic()
        {
            var exportDirectory = Path.GetFullPath(Path.Combine("TestResults", "AutoChess", "T6-CHESS-AF"));
            var result = HeadlessAutoChessScaleValidation.Run(
                new HeadlessAutoChessScaleValidationOptions(
                    runCount: 8,
                    exportLogs: true,
                    exportDirectory: exportDirectory,
                    scenarioOptions: new HeadlessAutoChessOptions(
                        maxTicks: 128,
                        postVictoryFlushTicks: 4)));
            var report = result.Report;

            Assert.That(report.Passed, Is.True, report.SummaryText);
            Assert.That(report.FailureCount, Is.EqualTo(0), report.SummaryText);
            Assert.That(result.RunCount, Is.EqualTo(8), report.SummaryText);
            Assert.That(result.Variants, Has.Length.EqualTo(1), report.SummaryText);
            Assert.That(result.Variants[0].VariantName, Is.EqualTo("DefaultBalanced"), report.SummaryText);
            Assert.That(result.PassedRunCount, Is.EqualTo(result.RunCount), report.SummaryText);
            Assert.That(result.FailedRunCount, Is.EqualTo(0), report.SummaryText);
            Assert.That(result.Deterministic, Is.True, report.SummaryText);
            Assert.That(result.ReferenceDeterminismSignature, Is.Not.Empty, report.SummaryText);
            Assert.That(result.TotalTicks, Is.GreaterThan(0), report.SummaryText);
            Assert.That(result.TotalReplayEvents, Is.GreaterThan(0), report.SummaryText);
            Assert.That(result.TotalStructuredLogEntries, Is.EqualTo(result.TotalReplayEvents), report.SummaryText);
            Assert.That(
                result.AverageTickMilliseconds,
                Is.LessThanOrEqualTo(HeadlessAutoChessValidationThresholds.Default.MaxAverageTickMilliseconds),
                report.SummaryText);
            Assert.That(
                result.MaxRunAverageTickMilliseconds,
                Is.LessThanOrEqualTo(HeadlessAutoChessValidationThresholds.Default.MaxAverageTickMilliseconds),
                report.SummaryText);

            for (var i = 0; i < result.Runs.Length; i++)
            {
                var run = result.Runs[i];
                Assert.That(run.Result.ValidationReport.Passed, Is.True, run.Result.ValidationReport.SummaryText);
                Assert.That(run.Result.Completed, Is.True, run.Result.AssertionLog);
                Assert.That(run.Result.Winner, Is.EqualTo(HeadlessAutoChessTeam.Player), run.Result.AssertionLog);
                Assert.That(
                    run.Result.EventCounts.StructuredLogEntries,
                    Is.EqualTo(run.Result.EventCounts.ReplayEvents),
                    run.Result.AssertionLog);
                AssertPresentationMarkers(run.Result);
                Assert.That(
                    run.DeterminismSignature,
                    Is.EqualTo(result.ReferenceDeterminismSignature),
                    report.SummaryText);
                Assert.That(File.Exists(run.Result.ValidationReport.SummaryPath), Is.True, report.SummaryText);
            }

            Assert.That(File.Exists(report.SummaryPath), Is.True, report.SummaryText);
            Assert.That(report.SummaryByteCount, Is.GreaterThan(0), report.SummaryText);

            var summaryText = File.ReadAllText(report.SummaryPath);
            StringAssert.Contains("HeadlessAutoChessScaleValidationReport", summaryText);
            StringAssert.Contains("passed=true", summaryText);
            StringAssert.Contains("runs|count=8|passed=8|failed=0|deterministic=true", summaryText);
            StringAssert.Contains("variants|count=1", summaryText);
            StringAssert.Contains("variant|name=DefaultBalanced|runs=8|passed=8|failed=0|deterministic=true", summaryText);
            StringAssert.Contains("events|replayTotal=", summaryText);
            StringAssert.Contains("signature|reference=", summaryText);
            StringAssert.Contains("run|index=0|passed=true|winner=Player", summaryText);
        }

        [Test]
        public void RunVariantScaleValidationExportsVariantMatrixAndStaysDeterministic()
        {
            var exportDirectory = Path.GetFullPath(Path.Combine("TestResults", "AutoChess", "T6-CHESS-AF"));
            var thresholds = new HeadlessAutoChessValidationThresholds
            {
                MaxBattleTicks = 192,
                MaxTotalTicks = 196,
            };
            var result = HeadlessAutoChessScaleValidation.Run(
                new HeadlessAutoChessScaleValidationOptions(
                    runCount: 2,
                    exportLogs: true,
                    exportDirectory: exportDirectory,
                    scenarioOptions: new HeadlessAutoChessOptions(
                        maxTicks: 192,
                        postVictoryFlushTicks: 4,
                        validationThresholds: thresholds),
                    variants: new[]
                    {
                        HeadlessAutoChessScenarioVariant.DefaultBalanced,
                        HeadlessAutoChessScenarioVariant.PlayerAdvantage,
                        HeadlessAutoChessScenarioVariant.EnemyPressure,
                        HeadlessAutoChessScenarioVariant.LargeBoard,
                    }));
            var report = result.Report;

            Assert.That(report.Passed, Is.True, report.SummaryText);
            Assert.That(report.FailureCount, Is.EqualTo(0), report.SummaryText);
            Assert.That(result.RunCount, Is.EqualTo(8), report.SummaryText);
            Assert.That(result.Variants, Has.Length.EqualTo(4), report.SummaryText);
            Assert.That(result.PassedRunCount, Is.EqualTo(result.RunCount), report.SummaryText);
            Assert.That(result.FailedRunCount, Is.EqualTo(0), report.SummaryText);
            Assert.That(result.Deterministic, Is.True, report.SummaryText);
            Assert.That(result.TotalReplayEvents, Is.GreaterThan(0), report.SummaryText);
            Assert.That(result.TotalStructuredLogEntries, Is.EqualTo(result.TotalReplayEvents), report.SummaryText);

            for (var i = 0; i < result.Variants.Length; i++)
            {
                var variant = result.Variants[i];
                Assert.That(variant.RunCount, Is.EqualTo(2), report.SummaryText);
                Assert.That(variant.PassedRunCount, Is.EqualTo(2), report.SummaryText);
                Assert.That(variant.FailedRunCount, Is.EqualTo(0), report.SummaryText);
                Assert.That(variant.Deterministic, Is.True, report.SummaryText);
                Assert.That(variant.ReferenceDeterminismSignature, Is.Not.Empty, report.SummaryText);
                Assert.That(variant.TotalStructuredLogEntries, Is.EqualTo(variant.TotalReplayEvents), report.SummaryText);
                Assert.That(variant.ExpectedWinner, Is.EqualTo(HeadlessAutoChessTeam.Player), report.SummaryText);
            }

            var largeBoard = FindVariant(result, HeadlessAutoChessScenarioVariant.LargeBoard);
            Assert.That(largeBoard.PlayerUnitCount, Is.GreaterThanOrEqualTo(5), report.SummaryText);
            Assert.That(largeBoard.EnemyUnitCount, Is.GreaterThanOrEqualTo(5), report.SummaryText);

            for (var i = 0; i < result.Runs.Length; i++)
            {
                var run = result.Runs[i];
                Assert.That(run.Result.ValidationReport.Passed, Is.True, run.Result.ValidationReport.SummaryText);
                Assert.That(run.Result.Completed, Is.True, run.Result.AssertionLog);
                Assert.That(run.Result.Winner, Is.EqualTo(HeadlessAutoChessTeam.Player), run.Result.AssertionLog);
                Assert.That(run.Result.EventCounts.StructuredLogEntries, Is.EqualTo(run.Result.EventCounts.ReplayEvents));
                AssertPresentationMarkers(run.Result);
                Assert.That(File.Exists(run.Result.ValidationReport.SummaryPath), Is.True, report.SummaryText);
            }

            Assert.That(File.Exists(report.SummaryPath), Is.True, report.SummaryText);
            var summaryText = File.ReadAllText(report.SummaryPath);
            StringAssert.Contains("variants|count=4", summaryText);
            StringAssert.Contains("variant|name=DefaultBalanced|runs=2|passed=2|failed=0|deterministic=true", summaryText);
            StringAssert.Contains("variant|name=PlayerAdvantage|runs=2|passed=2|failed=0|deterministic=true", summaryText);
            StringAssert.Contains("variant|name=EnemyPressure|runs=2|passed=2|failed=0|deterministic=true", summaryText);
            StringAssert.Contains("variant|name=LargeBoard|runs=2|passed=2|failed=0|deterministic=true", summaryText);
            StringAssert.Contains("run|index=0|passed=true|winner=Player|variant=DefaultBalanced", summaryText);
            StringAssert.Contains("variant=LargeBoard", summaryText);
            StringAssert.Contains("uiMarkers=", summaryText);
            StringAssert.Contains("settlementMarkers=", summaryText);
        }

        [Test]
        public void RunPresentationCueMarkerValidationExportsAllHeadlessChannels()
        {
            var exportDirectory = Path.GetFullPath(Path.Combine("TestResults", "AutoChess", "T6-CHESS-AF"));
            var result = HeadlessAutoChessScenario.RunDefault(
                new HeadlessAutoChessOptions(
                    maxTicks: 128,
                    postVictoryFlushTicks: 4,
                    exportLogs: true,
                    exportDirectory: exportDirectory));
            var report = result.ValidationReport;

            Assert.That(result.Completed, Is.True, result.AssertionLog);
            Assert.That(report.Passed, Is.True, report.SummaryText);
            AssertPresentationMarkers(result);
            Assert.That(File.Exists(report.AssertionLogFile.Path), Is.True, report.SummaryText);
            Assert.That(File.Exists(report.HumanReadableLogFile.Path), Is.True, report.SummaryText);
            Assert.That(File.Exists(report.SummaryPath), Is.True, report.SummaryText);

            var assertionText = File.ReadAllText(report.AssertionLogFile.Path);
            var humanText = File.ReadAllText(report.HumanReadableLogFile.Path);
            var summaryText = File.ReadAllText(report.SummaryPath);

            StringAssert.Contains("domain=Presentation", assertionText);
            StringAssert.Contains("module=Presentation", assertionText);
            StringAssert.Contains("type=AutoChessPresentationUiMarker", assertionText);
            StringAssert.Contains("type=AutoChessPresentationVfxMarker", assertionText);
            StringAssert.Contains("type=AutoChessPresentationSfxMarker", assertionText);
            StringAssert.Contains("type=AutoChessPresentationFloatingTextMarker", assertionText);
            StringAssert.Contains("type=AutoChessPresentationCueMarker", assertionText);
            StringAssert.Contains("type=AutoChessPresentationSettlementMarker", assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiHealthBarAttached, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxAbilityImpact, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxImpact, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextDamage, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueRequest, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiCrowdControlSkipped, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxCrowdControl, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxCrowdControl, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextControl, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueCrowdControl, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiShieldChanged, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxShield, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxShield, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextShield, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueShield, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiResistanceChanged, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxResistance, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxResistance, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextResistance, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueResistance, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiEquipmentChanged, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiCounterTriggered, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxCounter, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxCounter, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextCounter, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueCounter, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiCleanseTriggered, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxCleanse, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxCleanse, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextCleanse, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueCleanse, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiCleanseRallyTriggered, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxCleanseRally, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxCleanseRally, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextCleanseRally, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueCleanseRally, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiRallyComboTriggered, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxRallyCombo, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxRallyCombo, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextRallyCombo, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueRallyCombo, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiLifeStealTriggered, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxLifeSteal, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxLifeSteal, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextLifeSteal, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueLifeSteal, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiPoisonStacked, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxPoison, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxPoison, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextPoison, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CuePoison, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiExecuteTriggered, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxExecute, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxExecute, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextExecute, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueExecute, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiDeathBurstTriggered, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxDeathBurst, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxDeathBurst, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextDeathBurst, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueDeathBurst, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiEnrageTriggered, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxEnrage, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxEnrage, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextEnrage, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueEnrage, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.UiSummonSpawned, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.VfxSummon, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SfxSummon, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.FloatingTextSummon, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.CueSummon, assertionText);
            StringAssert.Contains("event=" + (int)HeadlessAutoChessPresentationMarkerCode.SettlementScoreboard, assertionText);
            StringAssert.Contains("AutoChessPresentationUiMarker", humanText);
            StringAssert.Contains("AutoChessPresentationSettlementMarker", humanText);
            StringAssert.Contains("presentation|uiMarkers=", summaryText);
            StringAssert.Contains("cueMarkers=", summaryText);
            StringAssert.Contains("settlementMarkers=", summaryText);
            StringAssert.Contains("presentationOutbox|events=", summaryText);
            StringAssert.Contains("presentationOutboxCodes|healthBarAttached=", summaryText);
            StringAssert.Contains("uiControlSkipped=", summaryText);
            StringAssert.Contains("cueControl=", summaryText);
            StringAssert.Contains("uiShield=", summaryText);
            StringAssert.Contains("shieldText=", summaryText);
            StringAssert.Contains("cueShield=", summaryText);
            StringAssert.Contains("uiEquipment=", summaryText);
            StringAssert.Contains("uiCounter=", summaryText);
            StringAssert.Contains("counterText=", summaryText);
            StringAssert.Contains("cueCounter=", summaryText);
            StringAssert.Contains("uiCleanse=", summaryText);
            StringAssert.Contains("cleanseText=", summaryText);
            StringAssert.Contains("cueCleanse=", summaryText);
            StringAssert.Contains("uiCleanseRally=", summaryText);
            StringAssert.Contains("cleanseRallyText=", summaryText);
            StringAssert.Contains("cueCleanseRally=", summaryText);
            StringAssert.Contains("uiRallyCombo=", summaryText);
            StringAssert.Contains("rallyComboText=", summaryText);
            StringAssert.Contains("cueRallyCombo=", summaryText);
            StringAssert.Contains("uiLifeSteal=", summaryText);
            StringAssert.Contains("lifeStealText=", summaryText);
            StringAssert.Contains("cueLifeSteal=", summaryText);
            StringAssert.Contains("uiPoison=", summaryText);
            StringAssert.Contains("poisonText=", summaryText);
            StringAssert.Contains("cuePoison=", summaryText);
            StringAssert.Contains("uiExecute=", summaryText);
            StringAssert.Contains("executeText=", summaryText);
            StringAssert.Contains("cueExecute=", summaryText);
            StringAssert.Contains("uiDeathBurst=", summaryText);
            StringAssert.Contains("deathBurstText=", summaryText);
            StringAssert.Contains("cueDeathBurst=", summaryText);
            StringAssert.Contains("uiEnrage=", summaryText);
            StringAssert.Contains("enrageText=", summaryText);
            StringAssert.Contains("cueEnrage=", summaryText);
            StringAssert.Contains("uiResistance=", summaryText);
            StringAssert.Contains("resistanceText=", summaryText);
            StringAssert.Contains("cueResistance=", summaryText);
            StringAssert.Contains("uiSummon=", summaryText);
            StringAssert.Contains("summonText=", summaryText);
            StringAssert.Contains("cueSummon=", summaryText);
        }

        [Test]
        public void RunPerformanceProfileExportsRuntimeBudgetsAndVariantMetrics()
        {
            var exportDirectory = Path.GetFullPath(Path.Combine("TestResults", "AutoChess", "T6-CHESS-AF"));
            var thresholds = new HeadlessAutoChessValidationThresholds
            {
                MaxBattleTicks = 192,
                MaxTotalTicks = 196,
            };
            var result = HeadlessAutoChessPerformanceProfile.Run(
                new HeadlessAutoChessPerformanceProfileOptions(
                    runCountPerVariant: 2,
                    exportLogs: true,
                    exportScaleRunLogs: false,
                    exportDirectory: exportDirectory,
                    scenarioOptions: new HeadlessAutoChessOptions(
                        maxTicks: 192,
                        postVictoryFlushTicks: 4,
                        validationThresholds: thresholds)));
            var report = result.Report;

            Assert.That(report.Passed, Is.True, report.SummaryText);
            Assert.That(report.FailureCount, Is.EqualTo(0), report.SummaryText);
            Assert.That(result.RunCount, Is.EqualTo(8), report.SummaryText);
            Assert.That(result.VariantCount, Is.EqualTo(4), report.SummaryText);
            Assert.That(result.Deterministic, Is.True, report.SummaryText);
            Assert.That(result.TotalTicks, Is.GreaterThan(0), report.SummaryText);
            Assert.That(result.TotalReplayEvents, Is.GreaterThan(0), report.SummaryText);
            Assert.That(result.TotalStructuredLogEntries, Is.EqualTo(result.TotalReplayEvents), report.SummaryText);
            Assert.That(result.TotalPresentationMarkers, Is.GreaterThan(0), report.SummaryText);
            Assert.That(
                result.AverageTickMilliseconds,
                Is.LessThanOrEqualTo(HeadlessAutoChessValidationThresholds.Default.MaxAverageTickMilliseconds),
                report.SummaryText);
            Assert.That(
                result.P95RunAverageTickMilliseconds,
                Is.LessThanOrEqualTo(HeadlessAutoChessValidationThresholds.Default.MaxAverageTickMilliseconds),
                report.SummaryText);
            Assert.That(
                result.MaxRunAverageTickMilliseconds,
                Is.LessThanOrEqualTo(HeadlessAutoChessValidationThresholds.Default.MaxAverageTickMilliseconds),
                report.SummaryText);
            Assert.That(result.TicksPerSecond, Is.GreaterThanOrEqualTo(40d), report.SummaryText);
            Assert.That(result.ReplayEventsPerTick, Is.GreaterThan(0d), report.SummaryText);
            Assert.That(result.PresentationMarkersPerTick, Is.GreaterThan(0d), report.SummaryText);

            for (var i = 0; i < result.Variants.Length; i++)
            {
                var variant = result.Variants[i];
                Assert.That(variant.RunCount, Is.EqualTo(2), report.SummaryText);
                Assert.That(variant.PassedRunCount, Is.EqualTo(2), report.SummaryText);
                Assert.That(variant.Deterministic, Is.True, report.SummaryText);
                Assert.That(variant.TotalReplayEvents, Is.GreaterThan(0), report.SummaryText);
                Assert.That(variant.TotalStructuredLogEntries, Is.EqualTo(variant.TotalReplayEvents), report.SummaryText);
                Assert.That(variant.TotalPresentationMarkers, Is.GreaterThan(0), report.SummaryText);
                Assert.That(variant.TicksPerSecond, Is.GreaterThan(0d), report.SummaryText);
                Assert.That(variant.P95RunAverageTickMilliseconds, Is.LessThanOrEqualTo(25d), report.SummaryText);
            }

            Assert.That(File.Exists(report.SummaryPath), Is.True, report.SummaryText);
            Assert.That(report.SummaryByteCount, Is.GreaterThan(0), report.SummaryText);

            var summaryText = File.ReadAllText(report.SummaryPath);
            StringAssert.Contains("HeadlessAutoChessPerformanceProfileReport", summaryText);
            StringAssert.Contains("passed=true", summaryText);
            StringAssert.Contains("profile|runs=8|variants=4|deterministic=true|scalePassed=true", summaryText);
            StringAssert.Contains("runtime|totalTicks=", summaryText);
            StringAssert.Contains("percentiles|p50RunAvgTickMs=", summaryText);
            StringAssert.Contains("throughput|ticksPerSecond=", summaryText);
            StringAssert.Contains("events|replayTotal=", summaryText);
            StringAssert.Contains("presentation|markersTotal=", summaryText);
            StringAssert.Contains("thresholds|maxAvgTickMs=", summaryText);
            StringAssert.Contains("variantProfile|name=DefaultBalanced|runs=2", summaryText);
            StringAssert.Contains("variantProfile|name=LargeBoard|runs=2", summaryText);
            StringAssert.Contains("runProfile|index=0|variant=DefaultBalanced", summaryText);
        }

        public static void RunExportValidationFromCommandLine()
        {
            new HeadlessAutoChessScenarioTests().RunDefaultExportsValidationReportAndMeetsPerformanceGate();
        }

        public static void RunScaleValidationFromCommandLine()
        {
            new HeadlessAutoChessScenarioTests().RunScaleValidationExportsBatchSummaryAndStaysDeterministic();
        }

        public static void RunVariantScaleValidationFromCommandLine()
        {
            new HeadlessAutoChessScenarioTests().RunVariantScaleValidationExportsVariantMatrixAndStaysDeterministic();
        }

        public static void RunPresentationCueMarkerValidationFromCommandLine()
        {
            new HeadlessAutoChessScenarioTests().RunPresentationCueMarkerValidationExportsAllHeadlessChannels();
        }

        public static void RunPerformanceProfileFromCommandLine()
        {
            new HeadlessAutoChessScenarioTests().RunPerformanceProfileExportsRuntimeBudgetsAndVariantMetrics();
        }

        private static bool HasAliveUnit(
            HeadlessAutoChessResult result,
            HeadlessAutoChessTeam team)
        {
            for (var i = 0; i < result.Units.Length; i++)
            {
                if (result.Units[i].Team == team && result.Units[i].Alive)
                    return true;
            }

            return false;
        }

        private static void AssertGameplayEventValue(
            string assertionLog,
            string type,
            int eventCode,
            int reasonCode,
            float value)
        {
            var expectedValue = value.ToString("0.###", CultureInfo.InvariantCulture);
            var reader = new StringReader(assertionLog ?? string.Empty);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Contains("|type=" + type + "|")
                    && line.Contains("|event=" + eventCode + "|")
                    && line.Contains("|reason=" + reasonCode + "|")
                    && line.Contains("|value=" + expectedValue + "|"))
                {
                    return;
                }
            }

            Assert.Fail(
                "Expected gameplay event value not found: type="
                + type
                + ", event="
                + eventCode
                + ", reason="
                + reasonCode
                + ", value="
                + expectedValue);
        }

        private static void AssertPresentationMarkers(HeadlessAutoChessResult result)
        {
            Assert.That(result.EventCounts.PresentationUiMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.PresentationVfxMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.PresentationSfxMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.PresentationFloatingTextMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.PresentationCueMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.PresentationSettlementMarkers, Is.GreaterThan(0), result.AssertionLog);
            AssertPresentationOutboxMarkers(result);
        }

        private static void AssertPresentationOutboxMarkers(HeadlessAutoChessResult result)
        {
            var counts = result.PresentationOutboxCounts;
            Assert.That(counts.TotalEvents, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CueRequests, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.VfxMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SfxMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.FloatingTextMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CueMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SettlementMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiHealthBarAttachedMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.VfxAbilityImpactMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SfxImpactMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.FloatingTextDamageMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CueRequestPresentationMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SettlementScoreboardMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiCrowdControlSkippedMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.VfxCrowdControlMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SfxCrowdControlMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.FloatingTextControlMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CueCrowdControlMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiShieldChangedMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.VfxShieldMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SfxShieldMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.FloatingTextShieldMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CueShieldMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiResistanceMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.VfxResistanceMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SfxResistanceMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.FloatingTextResistanceMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CueResistanceMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiEquipmentMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiCounterMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.VfxCounterMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SfxCounterMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.FloatingTextCounterMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CueCounterMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiCleanseMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.VfxCleanseMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SfxCleanseMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.FloatingTextCleanseMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CueCleanseMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiCleanseRallyMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.VfxCleanseRallyMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SfxCleanseRallyMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.FloatingTextCleanseRallyMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CueCleanseRallyMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiRallyComboMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.VfxRallyComboMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SfxRallyComboMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.FloatingTextRallyComboMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CueRallyComboMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiLifeStealMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.VfxLifeStealMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SfxLifeStealMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.FloatingTextLifeStealMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CueLifeStealMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiPoisonMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.VfxPoisonMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SfxPoisonMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.FloatingTextPoisonMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CuePoisonMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiExecuteMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.VfxExecuteMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SfxExecuteMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.FloatingTextExecuteMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CueExecuteMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiDeathBurstMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.VfxDeathBurstMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SfxDeathBurstMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.FloatingTextDeathBurstMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CueDeathBurstMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiEnrageMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.VfxEnrageMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SfxEnrageMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.FloatingTextEnrageMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CueEnrageMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.UiSummonMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.VfxSummonMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.SfxSummonMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.FloatingTextSummonMarkers, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(counts.CueSummonMarkers, Is.GreaterThan(0), result.AssertionLog);
        }

        private static bool HasDefeatedUnit(
            HeadlessAutoChessResult result,
            HeadlessAutoChessTeam team)
        {
            for (var i = 0; i < result.Units.Length; i++)
            {
                if (result.Units[i].Team == team && !result.Units[i].Alive)
                    return true;
            }

            return false;
        }

        private static bool HasUnitAt(
            HeadlessAutoChessResult result,
            string id,
            int boardX,
            int boardY)
        {
            for (var i = 0; i < result.Units.Length; i++)
            {
                var unit = result.Units[i];
                if (unit.Id == id && unit.BoardX == boardX && unit.BoardY == boardY)
                    return true;
            }

            return false;
        }

        private static HeadlessAutoChessScaleVariantResult FindVariant(
            HeadlessAutoChessScaleValidationResult result,
            HeadlessAutoChessScenarioVariant variant)
        {
            for (var i = 0; i < result.Variants.Length; i++)
            {
                if (result.Variants[i].Variant == variant)
                    return result.Variants[i];
            }

            Assert.Fail("variant not found: " + variant);
            return default;
        }
    }
}
