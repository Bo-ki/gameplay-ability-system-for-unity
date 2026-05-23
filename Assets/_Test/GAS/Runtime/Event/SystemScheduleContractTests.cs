using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Tests.Event
{
    public sealed class SystemScheduleContractTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (!GASManager.IsInitialized)
                GASManager.Initialize();
        }

        [Test]
        public void FixedStepGasGroupsFollowContractOrder()
        {
            AssertSystemOrder(
                GASManager.ExWorld.GetExistingSystemManaged<FixedStepSimulationSystemGroup>(),
                GASSystemScheduleContract.FixedStepGroups,
                nameof(FixedStepSimulationSystemGroup));
        }

        [Test]
        public void GasRuntimeGroupsFollowContractOrder()
        {
            AssertSystemOrder(
                GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>(),
                GASSystemScheduleContract.CommandSystems,
                nameof(GASCommandGroup));
            AssertSystemOrder(
                GASManager.ExWorld.GetExistingSystemManaged<GASExecutionCalculationExtensionGroup>(),
                GASSystemScheduleContract.ExecutionCalculationExtensionSystems,
                nameof(GASExecutionCalculationExtensionGroup));
            AssertSystemOrder(
                GASManager.ExWorld.GetExistingSystemManaged<GASResetDirtyGroup>(),
                GASSystemScheduleContract.ResetDirtySystems,
                nameof(GASResetDirtyGroup));
            AssertSystemOrder(
                GASManager.ExWorld.GetExistingSystemManaged<GASTagGroup>(),
                GASSystemScheduleContract.TagSystems,
                nameof(GASTagGroup));
            AssertSystemOrder(
                GASManager.ExWorld.GetExistingSystemManaged<GASEffectGroup>(),
                GASSystemScheduleContract.EffectSystems,
                nameof(GASEffectGroup));
            AssertSystemOrder(
                GASManager.ExWorld.GetExistingSystemManaged<GASAttributeGroup>(),
                GASSystemScheduleContract.AttributeSystems,
                nameof(GASAttributeGroup));
            AssertSystemOrder(
                GASManager.ExWorld.GetExistingSystemManaged<GASAbilityGroup>(),
                GASSystemScheduleContract.AbilitySystems,
                nameof(GASAbilityGroup));
            AssertSystemOrder(
                GASManager.ExWorld.GetExistingSystemManaged<GASCueGroup>(),
                GASSystemScheduleContract.CueSystems,
                nameof(GASCueGroup));
        }

        [Test]
        public void AbilityCommitRunsAfterTryActivateAndBeforeEffectRequestConsumers()
        {
            var commandSystems = GASSystemScheduleContract.CommandSystems;

            Assert.That(IndexOf<SHeadlessAutoBattleDriver>(commandSystems), Is.LessThan(IndexOf<SAbilityCommandRequest>(commandSystems)));
            Assert.That(IndexOf<SHeadlessAutoChessDriver>(commandSystems), Is.LessThan(IndexOf<SAbilityCommandRequest>(commandSystems)));
            Assert.That(IndexOf<STryActivateAbility>(commandSystems), Is.LessThan(IndexOf<SAbilityCommit>(commandSystems)));
            Assert.That(IndexOf<SAbilityCommit>(commandSystems), Is.LessThan(IndexOf<SAbilityTimelineAction>(commandSystems)));
            Assert.That(IndexOf<SAbilityCommit>(commandSystems), Is.LessThan(IndexOf<SApplyGameplayEffectRequest>(commandSystems)));
        }

        [Test]
        public void TimelineLifecycleRequestRunsAfterTimelineActionAndBeforeEffectRequestConsumers()
        {
            var commandSystems = GASSystemScheduleContract.CommandSystems;

            Assert.That(IndexOf<SAbilityTimelineAction>(commandSystems), Is.LessThan(IndexOf<SAbilityTimelineLifecycleRequest>(commandSystems)));
            Assert.That(IndexOf<SAbilityTimelineLifecycleRequest>(commandSystems), Is.LessThan(IndexOf<SApplyGameplayEffectRequest>(commandSystems)));
        }

        [Test]
        public void ExecutionCalculationExtensionSlotRunsBetweenProducerAndOutputModifierConsumer()
        {
            var commandSystems = GASSystemScheduleContract.CommandSystems;
            var extensionSystems = GASSystemScheduleContract.ExecutionCalculationExtensionSystems;

            Assert.That(IndexOf<SApplyGameplayEffectRequest>(commandSystems), Is.LessThan(IndexOf<SExecutionCalculation>(commandSystems)));
            Assert.That(IndexOf<SExecutionCalculation>(commandSystems), Is.LessThan(IndexOf<GASExecutionCalculationExtensionGroup>(commandSystems)));
            Assert.That(IndexOf<GASExecutionCalculationExtensionGroup>(commandSystems), Is.LessThan(IndexOf<SExecutionCalculationOutputModifier>(commandSystems)));
            Assert.That(IndexOf<SHeadlessAutoBattleExecuteCalculation>(extensionSystems), Is.GreaterThanOrEqualTo(0));
            Assert.That(IndexOf<SHeadlessAutoChessShieldDamageCalculation>(extensionSystems), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void AbilityLifecycleRequestRunsAfterTickAndBeforeCleanup()
        {
            var abilitySystems = GASSystemScheduleContract.AbilitySystems;

            Assert.That(IndexOf<SAbilityTick>(abilitySystems), Is.LessThan(IndexOf<SAttributeThresholdAbilityLifecycleRequest>(abilitySystems)));
            Assert.That(IndexOf<SAttributeThresholdAbilityLifecycleRequest>(abilitySystems), Is.LessThan(IndexOf<SAbilityLifecycleRequest>(abilitySystems)));
            Assert.That(IndexOf<SAbilityLifecycleRequest>(abilitySystems), Is.LessThan(IndexOf<SAbilityStateCleanup>(abilitySystems)));
            Assert.That(IndexOf<SAbilityStateCleanup>(abilitySystems), Is.LessThan(IndexOf<SHeadlessAutoChessGameplayEffectFactProjection>(abilitySystems)));
            Assert.That(IndexOf<SHeadlessAutoChessGameplayEffectFactProjection>(abilitySystems), Is.LessThan(IndexOf<SHeadlessAutoChessSummonLifecycle>(abilitySystems)));
            Assert.That(IndexOf<SHeadlessAutoChessSummonLifecycle>(abilitySystems), Is.LessThan(IndexOf<SHeadlessAutoChessPassiveReaction>(abilitySystems)));
            Assert.That(IndexOf<SHeadlessAutoChessPassiveReaction>(abilitySystems), Is.LessThan(IndexOf<SHeadlessAutoChessEnrageReaction>(abilitySystems)));
            Assert.That(IndexOf<SHeadlessAutoChessEnrageReaction>(abilitySystems), Is.LessThan(IndexOf<SHeadlessAutoChessCounterReaction>(abilitySystems)));
            Assert.That(IndexOf<SHeadlessAutoChessCounterReaction>(abilitySystems), Is.LessThan(IndexOf<SHeadlessAutoChessCleanseReaction>(abilitySystems)));
            Assert.That(IndexOf<SHeadlessAutoChessCleanseReaction>(abilitySystems), Is.LessThan(IndexOf<SHeadlessAutoChessRallyComboReaction>(abilitySystems)));
            Assert.That(IndexOf<SHeadlessAutoChessRallyComboReaction>(abilitySystems), Is.LessThan(IndexOf<SHeadlessAutoChessLifeStealReaction>(abilitySystems)));
            Assert.That(IndexOf<SHeadlessAutoChessLifeStealReaction>(abilitySystems), Is.LessThan(IndexOf<SHeadlessAutoChessPoisonReaction>(abilitySystems)));
            Assert.That(IndexOf<SHeadlessAutoChessPoisonReaction>(abilitySystems), Is.LessThan(IndexOf<SHeadlessAutoChessExecuteReaction>(abilitySystems)));
            Assert.That(IndexOf<SHeadlessAutoChessExecuteReaction>(abilitySystems), Is.LessThan(IndexOf<SHeadlessAutoChessDeathBurstReaction>(abilitySystems)));
            Assert.That(IndexOf<SHeadlessAutoChessDeathBurstReaction>(abilitySystems), Is.LessThan(IndexOf<SHeadlessAutoChessSynergyProjection>(abilitySystems)));
        }

        [Test]
        public void AutoChessBattleFactProjectionRunsAfterAttributeChangeProjection()
        {
            var attributeSystems = GASSystemScheduleContract.AttributeSystems;

            Assert.That(IndexOf<SAttributeChangeEventProjection>(attributeSystems), Is.LessThan(IndexOf<SHeadlessAutoChessBattleFactProjection>(attributeSystems)));
        }

        [Test]
        public void HeadlessPresentationMarkersRunBeforeOutboxAndReplayLog()
        {
            var cueSystems = GASSystemScheduleContract.CueSystems;

            Assert.That(IndexOf<SHeadlessAutoChessPresentationCueMarkerProjection>(cueSystems), Is.LessThan(IndexOf<SPresentationOutboxProjection>(cueSystems)));
            Assert.That(IndexOf<SHeadlessAutoChessPresentationCueMarkerProjection>(cueSystems), Is.LessThan(IndexOf<SDebugReplayLogProjection>(cueSystems)));
        }

        private static void AssertSystemOrder(
            ComponentSystemGroup group,
            IReadOnlyList<Type> expectedTypes,
            string groupName)
        {
            group.SortSystems();

            using var systems = group.GetAllSystems(Allocator.Temp);
            var actual = new int[systems.Length];
            for (var i = 0; i < systems.Length; i++)
                actual[i] = GASManager.ExWorld.Unmanaged.GetSystemTypeIndex(systems[i]).Index;

            var expected = new int[expectedTypes.Count];
            for (var i = 0; i < expectedTypes.Count; i++)
                expected[i] = TypeManager.GetSystemTypeIndex(expectedTypes[i]).Index;

            Assert.That(
                actual,
                Is.EqualTo(expected),
                groupName + " order must match GASSystemScheduleContract.");
        }

        private static int IndexOf<TSystem>(IReadOnlyList<Type> systemTypes)
        {
            var target = typeof(TSystem);
            for (var i = 0; i < systemTypes.Count; i++)
            {
                if (systemTypes[i] == target)
                    return i;
            }

            Assert.Fail("System " + target.Name + " was not found in GASSystemScheduleContract.");
            return -1;
        }
    }
}
