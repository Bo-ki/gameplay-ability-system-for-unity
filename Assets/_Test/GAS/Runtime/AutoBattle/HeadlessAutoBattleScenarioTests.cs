using NUnit.Framework;

namespace GAS.Runtime.Tests.AutoBattle
{
    public sealed class HeadlessAutoBattleScenarioTests
    {
        [Test]
        public void RunDefaultDrivesRuntimeChainAndExportsReplay()
        {
            var result = HeadlessAutoBattleScenario.RunDefault(
                new HeadlessAutoBattleOptions(maxTicks: 96, postVictoryFlushTicks: 4));

            Assert.That(result.Completed, Is.True, result.AssertionLog);
            Assert.That(result.Winner, Is.EqualTo(HeadlessAutoBattleTeam.Player), result.AssertionLog);
            Assert.That(result.BattleTicks, Is.GreaterThan(0));
            Assert.That(result.TotalTicks, Is.GreaterThanOrEqualTo(result.BattleTicks));
            Assert.That(result.DriverIssuedCommands, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.DriverIssuedPrimaryCommands, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.DriverIssuedFinisherCommands, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.DriverLowestHealthTargetSelections, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.AverageTickMilliseconds, Is.GreaterThanOrEqualTo(0d));

            Assert.That(result.Units, Has.Length.EqualTo(4));
            Assert.That(HasAliveUnit(result, HeadlessAutoBattleTeam.Player), Is.True, result.AssertionLog);
            Assert.That(HasDefeatedUnit(result, HeadlessAutoBattleTeam.Enemy), Is.True, result.AssertionLog);

            Assert.That(result.EventCounts.ReplayEvents, Is.GreaterThan(0));
            Assert.That(result.EventCounts.StructuredLogEntries, Is.EqualTo(result.EventCounts.ReplayEvents));
            Assert.That(result.EventCounts.AbilityCommitSucceeded, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.GameplayEffectInstanced, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.GameplayEffectApplied, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.GameplayEffectRemoved, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.ExecutionCalculationOutputUpdated, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.AttributeChanges, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.TagChanges, Is.GreaterThan(0), result.AssertionLog);
            Assert.That(result.EventCounts.CueRequests, Is.GreaterThan(0), result.AssertionLog);

            StringAssert.Contains("type=AbilityCommitSucceeded", result.AssertionLog);
            StringAssert.Contains("type=ExecutionCalculationOutputUpdated", result.AssertionLog);
            StringAssert.Contains("type=GameplayEffectApplied", result.AssertionLog);
            StringAssert.Contains("kind=AttributeChange", result.AssertionLog);
            StringAssert.Contains("kind=TagChange", result.AssertionLog);
            StringAssert.Contains("kind=CueRequest", result.AssertionLog);
        }

        private static bool HasAliveUnit(
            HeadlessAutoBattleResult result,
            HeadlessAutoBattleTeam team)
        {
            for (var i = 0; i < result.Units.Length; i++)
            {
                if (result.Units[i].Team == team && result.Units[i].Alive)
                    return true;
            }

            return false;
        }

        private static bool HasDefeatedUnit(
            HeadlessAutoBattleResult result,
            HeadlessAutoBattleTeam team)
        {
            for (var i = 0; i < result.Units.Length; i++)
            {
                if (result.Units[i].Team == team && !result.Units[i].Alive)
                    return true;
            }

            return false;
        }
    }
}
