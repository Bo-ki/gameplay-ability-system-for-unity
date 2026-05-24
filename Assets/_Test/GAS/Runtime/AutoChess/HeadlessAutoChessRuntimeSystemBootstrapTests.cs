using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Tests.AutoChess
{
    public sealed class HeadlessAutoChessRuntimeSystemBootstrapTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (!GASManager.IsInitialized)
                GASManager.Initialize();

            HeadlessAutoChessRuntimeSystemBootstrap.RegisterSystems(GASManager.ExWorld);
        }

        [Test]
        public void RegistersAutoChessSystemsIntoGasRuntimeGroups()
        {
            AssertRegistered(
                GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>(),
                HeadlessAutoChessRuntimeSystemBootstrap.CommandSystems,
                nameof(GASCommandGroup));
            AssertRegistered(
                GASManager.ExWorld.GetExistingSystemManaged<GASExecutionCalculationExtensionGroup>(),
                HeadlessAutoChessRuntimeSystemBootstrap.ExecutionCalculationExtensionSystems,
                nameof(GASExecutionCalculationExtensionGroup));
            AssertRegistered(
                GASManager.ExWorld.GetExistingSystemManaged<GASAttributeGroup>(),
                HeadlessAutoChessRuntimeSystemBootstrap.AttributeSystems,
                nameof(GASAttributeGroup));
            AssertRegistered(
                GASManager.ExWorld.GetExistingSystemManaged<GASAbilityGroup>(),
                HeadlessAutoChessRuntimeSystemBootstrap.AbilitySystems,
                nameof(GASAbilityGroup));
            AssertRegistered(
                GASManager.ExWorld.GetExistingSystemManaged<GASCueGroup>(),
                HeadlessAutoChessRuntimeSystemBootstrap.CueSystems,
                nameof(GASCueGroup));
        }

        [Test]
        public void RegisterSystemsIsIdempotent()
        {
            var before = CountRegisteredAutoChessSystems();
            HeadlessAutoChessRuntimeSystemBootstrap.RegisterSystems(GASManager.ExWorld);
            var after = CountRegisteredAutoChessSystems();

            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void AutoChessSystemsRespectRuntimeCoreOrderingBoundaries()
        {
            var command = GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>();
            var extension = GASManager.ExWorld.GetExistingSystemManaged<GASExecutionCalculationExtensionGroup>();
            var attribute = GASManager.ExWorld.GetExistingSystemManaged<GASAttributeGroup>();
            var ability = GASManager.ExWorld.GetExistingSystemManaged<GASAbilityGroup>();
            var cue = GASManager.ExWorld.GetExistingSystemManaged<GASCueGroup>();

            AssertBefore<SAscCommandRequest, SHeadlessAutoChessDriver>(command);
            AssertBefore<SHeadlessAutoChessDriver, SAbilityCommandRequest>(command);
            AssertRegistered(extension, HeadlessAutoChessRuntimeSystemBootstrap.ExecutionCalculationExtensionSystems, nameof(GASExecutionCalculationExtensionGroup));
            AssertBefore<SAttributeChangeEventProjection, SHeadlessAutoChessBattleFactProjection>(attribute);

            AssertBefore<SAbilityStateCleanup, SHeadlessAutoChessGameplayEffectFactProjection>(ability);
            AssertBefore<SHeadlessAutoChessGameplayEffectFactProjection, SHeadlessAutoChessSummonLifecycle>(ability);
            AssertBefore<SHeadlessAutoChessSummonLifecycle, SHeadlessAutoChessPassiveReaction>(ability);
            AssertBefore<SHeadlessAutoChessPassiveReaction, SHeadlessAutoChessEnrageReaction>(ability);
            AssertBefore<SHeadlessAutoChessEnrageReaction, SHeadlessAutoChessCounterReaction>(ability);
            AssertBefore<SHeadlessAutoChessCounterReaction, SHeadlessAutoChessCleanseReaction>(ability);
            AssertBefore<SHeadlessAutoChessCleanseReaction, SHeadlessAutoChessRallyComboReaction>(ability);
            AssertBefore<SHeadlessAutoChessRallyComboReaction, SHeadlessAutoChessLifeStealReaction>(ability);
            AssertBefore<SHeadlessAutoChessLifeStealReaction, SHeadlessAutoChessPoisonReaction>(ability);
            AssertBefore<SHeadlessAutoChessPoisonReaction, SHeadlessAutoChessExecuteReaction>(ability);
            AssertBefore<SHeadlessAutoChessExecuteReaction, SHeadlessAutoChessDeathBurstReaction>(ability);
            AssertBefore<SHeadlessAutoChessDeathBurstReaction, SHeadlessAutoChessSynergyProjection>(ability);

            AssertBefore<SHeadlessAutoChessPresentationCueMarkerProjection, SPresentationOutboxProjection>(cue);
            AssertBefore<SHeadlessAutoChessPresentationCueMarkerProjection, SDebugReplayLogProjection>(cue);
        }

        private static void AssertRegistered(
            ComponentSystemGroup group,
            IReadOnlyList<Type> expectedTypes,
            string groupName)
        {
            var actual = CreateSystemTypeIndexSet(group);
            var missing = new List<string>();
            for (var i = 0; i < expectedTypes.Count; i++)
            {
                var expectedIndex = TypeManager.GetSystemTypeIndex(expectedTypes[i]).Index;
                if (!actual.Contains(expectedIndex))
                    missing.Add(expectedTypes[i].Name);
            }

            Assert.That(missing, Is.Empty, groupName + " is missing AutoChess systems.");
        }

        private static void AssertBefore<TBefore, TAfter>(ComponentSystemGroup group)
        {
            var before = IndexOf<TBefore>(group);
            var after = IndexOf<TAfter>(group);
            Assert.That(before, Is.LessThan(after), typeof(TBefore).Name + " must run before " + typeof(TAfter).Name + ".");
        }

        private static int CountRegisteredAutoChessSystems()
        {
            return CountRegistered(GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>(), HeadlessAutoChessRuntimeSystemBootstrap.CommandSystems)
                   + CountRegistered(GASManager.ExWorld.GetExistingSystemManaged<GASExecutionCalculationExtensionGroup>(), HeadlessAutoChessRuntimeSystemBootstrap.ExecutionCalculationExtensionSystems)
                   + CountRegistered(GASManager.ExWorld.GetExistingSystemManaged<GASAttributeGroup>(), HeadlessAutoChessRuntimeSystemBootstrap.AttributeSystems)
                   + CountRegistered(GASManager.ExWorld.GetExistingSystemManaged<GASAbilityGroup>(), HeadlessAutoChessRuntimeSystemBootstrap.AbilitySystems)
                   + CountRegistered(GASManager.ExWorld.GetExistingSystemManaged<GASCueGroup>(), HeadlessAutoChessRuntimeSystemBootstrap.CueSystems);
        }

        private static int CountRegistered(
            ComponentSystemGroup group,
            IReadOnlyList<Type> expectedTypes)
        {
            var actual = CreateSystemTypeIndexSet(group);
            var count = 0;
            for (var i = 0; i < expectedTypes.Count; i++)
            {
                var expectedIndex = TypeManager.GetSystemTypeIndex(expectedTypes[i]).Index;
                if (actual.Contains(expectedIndex))
                    count++;
            }

            return count;
        }

        private static HashSet<int> CreateSystemTypeIndexSet(ComponentSystemGroup group)
        {
            group.SortSystems();
            var set = new HashSet<int>();
            using var systems = group.GetAllSystems(Allocator.Temp);
            for (var i = 0; i < systems.Length; i++)
                set.Add(GASManager.ExWorld.Unmanaged.GetSystemTypeIndex(systems[i]).Index);

            return set;
        }

        private static int IndexOf<TSystem>(ComponentSystemGroup group)
        {
            group.SortSystems();
            var target = TypeManager.GetSystemTypeIndex(typeof(TSystem)).Index;
            using var systems = group.GetAllSystems(Allocator.Temp);
            for (var i = 0; i < systems.Length; i++)
            {
                if (GASManager.ExWorld.Unmanaged.GetSystemTypeIndex(systems[i]).Index == target)
                    return i;
            }

            Assert.Fail("System " + typeof(TSystem).Name + " was not registered.");
            return -1;
        }
    }
}
