using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace GAS.Runtime.Tests.Event
{
    public sealed class RuntimeStreamOwnerContractTests
    {
        [Test]
        public void CurrentStreamOwnerPlanMarksEffectCommandSpecStreamAsMigrationCarrier()
        {
            var plan = GASRuntimeFrameStreamOwnerPlanner.CreateCurrent();

            Assert.That(plan.EntryCount, Is.EqualTo(6));
            Assert.That(plan.SingletonDynamicBufferCount, Is.EqualTo(plan.EntryCount));
            Assert.That(plan.MigrationCarrierCount, Is.EqualTo(plan.EntryCount));
            Assert.That(plan.ScaleReadyCount, Is.EqualTo(0));
            Assert.That(plan.NativeStreamCandidateCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(plan.OwnerLocalBufferCandidateCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(plan.BattleHashStreamCount, Is.EqualTo(plan.EntryCount));

            Assert.That(
                plan.TryFind(EGasRuntimeFrameStreamId.EffectCommand, out var commands),
                Is.True);
            Assert.That(commands.CurrentCarrier, Is.EqualTo(EGasRuntimeFrameStreamCarrier.SingletonDynamicBuffer));
            Assert.That(commands.TargetCarrier, Is.EqualTo(EGasRuntimeFrameStreamCarrier.PerThreadNativeStream));
            Assert.That(commands.ScaleStatus, Is.EqualTo(EGasRuntimeFrameStreamScaleStatus.MigrationCarrier));
            Assert.That(commands.ClearPhase, Is.EqualTo(EGasRuntimeCoreFramePhase.FramePrepare));
            Assert.That(commands.WritePhase, Is.EqualTo(EGasRuntimeCoreFramePhase.CommandIngest));
            Assert.That(commands.ReadPhase, Is.EqualTo(EGasRuntimeCoreFramePhase.SpecEvaluation));
            Assert.That(commands.MergePhase, Is.EqualTo(EGasRuntimeCoreFramePhase.CommandIngest));
            Assert.That(commands.MergePolicy, Is.EqualTo(EGasRuntimeFrameStreamMergePolicy.StableSortByTargetThenSequence));
            Assert.That(commands.SortKey, Is.EqualTo(EGasRuntimeFrameStreamSortKey.TargetAscThenCommandSequence));
            Assert.That(commands.InternalBufferCapacity, Is.EqualTo(64));
        }

        [Test]
        public void GameplayStreamsHaveDeterministicMergePolicyAndReselectTriggers()
        {
            var plan = GASRuntimeFrameStreamOwnerPlanner.CreateCurrent();
            for (var i = 0; i < plan.Entries.Count; i++)
            {
                var entry = plan.Entries[i];
                Assert.That(entry.Authority, Is.Not.EqualTo(EGasRuntimeFrameStreamAuthority.ObservationOnly));
                Assert.That(entry.Authority, Is.Not.EqualTo(EGasRuntimeFrameStreamAuthority.DebugTelemetry));
                Assert.That(entry.MergePolicy, Is.Not.EqualTo(EGasRuntimeFrameStreamMergePolicy.None));
                Assert.That(entry.MergePolicy, Is.Not.EqualTo(EGasRuntimeFrameStreamMergePolicy.ExcludedFromGameplayMerge));
                Assert.That(entry.SortKey, Is.Not.EqualTo(EGasRuntimeFrameStreamSortKey.None));
                Assert.That(entry.AffectsBattleHash, Is.True);
                Assert.That(entry.HasEvidence(EGasRuntimeFrameStreamEvidence.DeterministicSortKey), Is.True);
                Assert.That(entry.HasEvidence(EGasRuntimeFrameStreamEvidence.BattleHashInput), Is.True);
                Assert.That(entry.HasEvidence(EGasRuntimeFrameStreamEvidence.ProofOrMigrationMarker), Is.True);
                Assert.That(entry.HasReselectTrigger(EGasRuntimeFrameStreamReselectTrigger.X50BufferPressure), Is.True);
                Assert.That(entry.HasReselectTrigger(EGasRuntimeFrameStreamReselectTrigger.BufferExternalized), Is.True);
                Assert.That(entry.HasReselectTrigger(EGasRuntimeFrameStreamReselectTrigger.BattleHashInstability), Is.True);
            }
        }

        [Test]
        public void StreamOwnerEntriesMapToRuntimeCoreBackbonePhasesAndLayout()
        {
            var phaseSet = new HashSet<EGasRuntimeCoreFramePhase>();
            var phases = GASSystemScheduleContract.RuntimeCoreFramePhases;
            for (var i = 0; i < phases.Count; i++)
                phaseSet.Add(phases[i].Phase);

            var plan = GASRuntimeFrameStreamOwnerPlanner.CreateCurrent();
            Assert.That(plan.CountEntriesWithWritePhase(EGasRuntimeCoreFramePhase.CommandIngest), Is.EqualTo(2));
            Assert.That(plan.CountEntriesWithWritePhase(EGasRuntimeCoreFramePhase.SpecEvaluation), Is.EqualTo(1));
            Assert.That(plan.CountEntriesWithWritePhase(EGasRuntimeCoreFramePhase.ActiveEffectLifecycle), Is.EqualTo(1));
            Assert.That(plan.CountEntriesWithWritePhase(EGasRuntimeCoreFramePhase.DeltaApply), Is.EqualTo(1));
            Assert.That(plan.CountEntriesWithWritePhase(EGasRuntimeCoreFramePhase.TypedFactProjection), Is.EqualTo(1));

            for (var i = 0; i < plan.Entries.Count; i++)
            {
                var entry = plan.Entries[i];
                Assert.That(entry.LayoutEntryId, Is.EqualTo(GASRuntimeQueryLayoutEntryId.GameplayEffectCommandSpecStream));
                Assert.That(phaseSet.Contains(entry.ClearPhase), Is.True);
                Assert.That(phaseSet.Contains(entry.WritePhase), Is.True);
                Assert.That(phaseSet.Contains(entry.ReadPhase), Is.True);
                Assert.That(phaseSet.Contains(entry.MergePhase), Is.True);
                Assert.That(entry.ClearPhase, Is.EqualTo(EGasRuntimeCoreFramePhase.FramePrepare));
            }

            var layout = GASRuntimeQueryLayoutPlanner.CreateCurrent();
            Assert.That(
                layout.TryFind(GASRuntimeQueryLayoutEntryId.GameplayEffectCommandSpecStream, out var streamLayout),
                Is.True);
            Assert.That(streamLayout.HasBoundary(GASRuntimeLayoutBoundary.HighFrequencyCommandDataBoundary), Is.True);
            Assert.That(streamLayout.HasBoundary(GASRuntimeLayoutBoundary.RuntimeCoreStreamBoundary), Is.True);
            Assert.That(streamLayout.Decision, Is.EqualTo(GASRuntimeLayoutDecision.TargetContract));
        }

        [Test]
        public void CurrentStreamOwnerPlanCoversAllEffectCommandSpecStreamBuffers()
        {
            var expectedSlots = new[]
            {
                GASRuntimeLayoutComponentSlot.EffectCommandBuffer,
                GASRuntimeLayoutComponentSlot.EffectCommandSetByCallerBuffer,
                GASRuntimeLayoutComponentSlot.InstantEffectSpecBuffer,
                GASRuntimeLayoutComponentSlot.ActiveEffectMutationBuffer,
                GASRuntimeLayoutComponentSlot.AttributeDeltaBuffer,
                GASRuntimeLayoutComponentSlot.TypedSimulationFactBuffer,
            };
            var plan = GASRuntimeFrameStreamOwnerPlanner.CreateCurrent();

            for (var i = 0; i < expectedSlots.Length; i++)
            {
                var found = false;
                for (var j = 0; j < plan.Entries.Count; j++)
                {
                    if (plan.Entries[j].ComponentSlot == expectedSlots[i])
                    {
                        found = true;
                        break;
                    }
                }

                Assert.That(found, Is.True, expectedSlots[i] + " must have a stream owner entry.");
            }
        }

        [Test]
        public void RuntimeStreamOwnerContractDoesNotExposeUnityRuntimeHandles()
        {
            var checkedTypes = new[]
            {
                typeof(EGasRuntimeFrameStreamId),
                typeof(EGasRuntimeFrameStreamAuthority),
                typeof(EGasRuntimeFrameStreamCarrier),
                typeof(EGasRuntimeFrameStreamScaleStatus),
                typeof(EGasRuntimeFrameStreamMergePolicy),
                typeof(EGasRuntimeFrameStreamSortKey),
                typeof(EGasRuntimeFrameStreamReselectTrigger),
                typeof(EGasRuntimeFrameStreamEvidence),
                typeof(GASRuntimeFrameStreamOwnerEntry),
                typeof(GASRuntimeFrameStreamOwnerPlan),
                typeof(GASRuntimeFrameStreamOwnerPlanner),
            };
            var forbiddenTypeNames = new[]
            {
                "UnityEditor",
                "Sire" + "nix",
                "Od" + "in",
                "XLuban",
                "SimpleJSON",
                "cfg.",
                "BlobAssetReference",
                "EntityManager",
                "EntityQuery",
            };

            foreach (var type in checkedTypes)
            {
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    AssertNoForbiddenTypeName(type, field.FieldType, forbiddenTypeNames);
                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    AssertNoForbiddenTypeName(type, property.PropertyType, forbiddenTypeNames);
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                {
                    if (method.IsSpecialName)
                        continue;

                    AssertNoForbiddenTypeName(type, method.ReturnType, forbiddenTypeNames);
                    foreach (var parameter in method.GetParameters())
                        AssertNoForbiddenTypeName(type, parameter.ParameterType, forbiddenTypeNames);
                }
            }
        }

        private static void AssertNoForbiddenTypeName(
            Type owner,
            Type type,
            IReadOnlyList<string> forbiddenTypeNames)
        {
            var name = type.FullName ?? type.Name;
            for (var i = 0; i < forbiddenTypeNames.Count; i++)
            {
                Assert.That(
                    name.Contains(forbiddenTypeNames[i]),
                    Is.False,
                    owner.Name + " exposes forbidden type token " + forbiddenTypeNames[i] + " through " + name);
            }
        }
    }
}
