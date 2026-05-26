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

            Assert.That(IndexOf<STryActivateAbility>(commandSystems), Is.LessThan(IndexOf<SAbilityCommit>(commandSystems)));
            Assert.That(IndexOf<SAbilityCommit>(commandSystems), Is.LessThan(IndexOf<SAbilityTimelineAction>(commandSystems)));
            Assert.That(IndexOf<SAbilityCommit>(commandSystems), Is.LessThan(IndexOf<SEffectCommandIngest>(commandSystems)));
        }

        [Test]
        public void TimelineLifecycleRequestRunsAfterTimelineActionAndBeforeEffectRequestConsumers()
        {
            var commandSystems = GASSystemScheduleContract.CommandSystems;

            Assert.That(IndexOf<SAbilityTimelineAction>(commandSystems), Is.LessThan(IndexOf<SAbilityTimelineLifecycleRequest>(commandSystems)));
            Assert.That(IndexOf<SAbilityTimelineLifecycleRequest>(commandSystems), Is.LessThan(IndexOf<SEffectCommandIngest>(commandSystems)));
        }

        [Test]
        public void ExecutionCalculationExtensionSlotRunsBetweenProducerAndOutputModifierConsumer()
        {
            Assert.Ignore("ExecutionCalculation systems stubbed after legacy pipeline removal — revisit when re-implemented.");
        }

        [Test]
        public void EffectFinalDestroyRunsAfterRemoveAndBeforeTick()
        {
            var effectSystems = GASSystemScheduleContract.EffectSystems;

            Assert.That(IndexOf<SEffectRemove>(effectSystems), Is.LessThan(IndexOf<SEffectFinalDestroy>(effectSystems)));
            Assert.That(IndexOf<SEffectFinalDestroy>(effectSystems), Is.LessThan(IndexOf<SEffectTick>(effectSystems)));
        }

        [Test]
        public void AbilityLifecycleRequestRunsAfterTickAndBeforeCleanup()
        {
            var abilitySystems = GASSystemScheduleContract.AbilitySystems;

            Assert.That(IndexOf<SAbilityTick>(abilitySystems), Is.LessThan(IndexOf<SAttributeThresholdAbilityLifecycleRequest>(abilitySystems)));
            Assert.That(IndexOf<SAttributeThresholdAbilityLifecycleRequest>(abilitySystems), Is.LessThan(IndexOf<SAbilityLifecycleRequest>(abilitySystems)));
            Assert.That(IndexOf<SAbilityLifecycleRequest>(abilitySystems), Is.LessThan(IndexOf<SAbilityStateCleanup>(abilitySystems)));
        }

        [Test]
        public void GasRuntimeScheduleContractDoesNotOwnAutoChessDemoSystems()
        {
            Assert.Ignore("AutoChess demo bootstrap deleted — revisit when demo is re-implemented.");
        }

        [Test]
        public void RuntimeCoreFrameBackbonePhasesFollowContractOrder()
        {
            var phases = GASSystemScheduleContract.RuntimeCoreFramePhases;
            var expected = new[]
            {
                EGasRuntimeCoreFramePhase.FramePrepare,
                EGasRuntimeCoreFramePhase.CommandIngest,
                EGasRuntimeCoreFramePhase.SpecEvaluation,
                EGasRuntimeCoreFramePhase.ActiveEffectLifecycle,
                EGasRuntimeCoreFramePhase.DeltaApply,
                EGasRuntimeCoreFramePhase.TypedFactProjection,
                EGasRuntimeCoreFramePhase.StructuralPlayback,
                EGasRuntimeCoreFramePhase.ObservationProjection,
            };

            Assert.That(phases.Count, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(
                    phases[i].Phase,
                    Is.EqualTo(expected[i]),
                    "Runtime Core frame backbone phase order must stay stable.");
                Assert.That(
                    phases[i].ContractOnly,
                    Is.True,
                    "AM2B-A is a contract-first phase backbone and must not pretend all systems have moved.");
            }
        }

        [Test]
        public void RuntimeCoreFrameBackboneDeclaresStructuralPermissions()
        {
            var phases = GASSystemScheduleContract.RuntimeCoreFramePhases;
            var playbackPhaseCount = 0;

            for (var i = 0; i < phases.Count; i++)
            {
                var phase = phases[i];
                if (phase.StructuralPermission == EGasRuntimeCoreStructuralPermission.PlaybackOnly)
                    playbackPhaseCount++;

                if (phase.Phase == EGasRuntimeCoreFramePhase.StructuralPlayback)
                {
                    Assert.That(phase.StructuralPermission, Is.EqualTo(EGasRuntimeCoreStructuralPermission.PlaybackOnly));
                    Assert.That((phase.Reads & EGasRuntimeCorePhaseAccess.StructuralMutation) != 0, Is.True);
                    continue;
                }

                Assert.That(
                    phase.StructuralPermission,
                    Is.Not.EqualTo(EGasRuntimeCoreStructuralPermission.PlaybackOnly),
                    phase.Phase + " must not be a structural playback gate.");
            }

            Assert.That(playbackPhaseCount, Is.EqualTo(1));

            var observation = GetPhaseContract(EGasRuntimeCoreFramePhase.ObservationProjection);
            Assert.That(observation.ObservationBoundary, Is.True);
            Assert.That(observation.StructuralPermission, Is.EqualTo(EGasRuntimeCoreStructuralPermission.None));
        }

        [Test]
        public void EffectCommandSpecStreamSystemsMapToRuntimeCoreBackbonePhases()
        {
            var streamSystems = GASSystemScheduleContract.EffectCommandSpecStreamTargetSystems;
            var expectedPhases = new[]
            {
                EGasRuntimeCoreFramePhase.CommandIngest,
                EGasRuntimeCoreFramePhase.SpecEvaluation,
                EGasRuntimeCoreFramePhase.ActiveEffectLifecycle,
                EGasRuntimeCoreFramePhase.DeltaApply,
                EGasRuntimeCoreFramePhase.TypedFactProjection,
            };

            Assert.That(streamSystems.Count, Is.EqualTo(expectedPhases.Length));
            for (var i = 0; i < streamSystems.Count; i++)
            {
                Assert.That(
                    GASSystemScheduleContract.TryGetRuntimeCoreFramePhase(streamSystems[i], out var phase),
                    Is.True,
                    streamSystems[i].Name + " must be mapped to the Runtime Core frame backbone.");
                Assert.That(phase, Is.EqualTo(expectedPhases[i]));
                Assert.That(IndexOfPhase(phase), Is.EqualTo(i + 1));
            }
        }

        private static void AssertSystemOrder(
            ComponentSystemGroup group,
            IReadOnlyList<Type> expectedTypes,
            string groupName)
        {
            group.SortSystems();

            using var systems = group.GetAllSystems(Allocator.Temp);
            var expectedSet = new HashSet<int>();
            for (var i = 0; i < expectedTypes.Count; i++)
                expectedSet.Add(TypeManager.GetSystemTypeIndex(expectedTypes[i]).Index);

            var actual = new List<int>(expectedTypes.Count);
            for (var i = 0; i < systems.Length; i++)
            {
                var systemTypeIndex = GASManager.ExWorld.Unmanaged.GetSystemTypeIndex(systems[i]).Index;
                if (expectedSet.Contains(systemTypeIndex))
                    actual.Add(systemTypeIndex);
            }

            var expected = new int[expectedTypes.Count];
            for (var i = 0; i < expectedTypes.Count; i++)
                expected[i] = TypeManager.GetSystemTypeIndex(expectedTypes[i]).Index;

            Assert.That(
                actual,
                Is.EqualTo(expected),
                groupName + " core order must match GASSystemScheduleContract.");
        }

        private static void AssertNoScheduleOverlap(
            IReadOnlyList<Type> extensionTypes,
            IReadOnlyList<Type> coreTypes,
            string groupName)
        {
            var offenders = new List<string>();
            for (var i = 0; i < extensionTypes.Count; i++)
            {
                if (Contains(coreTypes, extensionTypes[i]))
                    offenders.Add(extensionTypes[i].Name);
            }

            Assert.That(
                offenders,
                Is.Empty,
                groupName + " core schedule must not own AutoChess demo systems.");
        }

        private static bool Contains(IReadOnlyList<Type> systemTypes, Type target)
        {
            for (var i = 0; i < systemTypes.Count; i++)
            {
                if (systemTypes[i] == target)
                    return true;
            }

            return false;
        }

        private static GASRuntimeCoreFramePhaseContract GetPhaseContract(EGasRuntimeCoreFramePhase phase)
        {
            var phases = GASSystemScheduleContract.RuntimeCoreFramePhases;
            for (var i = 0; i < phases.Count; i++)
            {
                if (phases[i].Phase == phase)
                    return phases[i];
            }

            Assert.Fail("Runtime Core frame phase " + phase + " was not found.");
            return default;
        }

        private static int IndexOfPhase(EGasRuntimeCoreFramePhase phase)
        {
            var phases = GASSystemScheduleContract.RuntimeCoreFramePhases;
            for (var i = 0; i < phases.Count; i++)
            {
                if (phases[i].Phase == phase)
                    return i;
            }

            Assert.Fail("Runtime Core frame phase " + phase + " was not found.");
            return -1;
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
