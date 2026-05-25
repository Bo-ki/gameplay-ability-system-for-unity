using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.Runtime.Tests.Event
{
    public sealed class RuntimeFrameBudgetContractTests
    {
        [Test]
        public void CurrentFrameBudgetDeclaresFrameArenaOwnerAndBudgetTotals()
        {
            var plan = GASRuntimeFrameBudgetPlanner.CreateCurrent();

            Assert.That(plan.EntryCount, Is.GreaterThanOrEqualTo(7));
            Assert.That(plan.TotalQueryBudget, Is.GreaterThan(0));
            Assert.That(plan.TotalUnfilteredQueryBudget, Is.EqualTo(plan.TotalQueryBudget));
            Assert.That(plan.TotalFilteredQueryBudget, Is.EqualTo(0));
            Assert.That(plan.TotalLookupUpdateBudget, Is.GreaterThan(0));
            Assert.That(plan.TotalRandomLookupBudget, Is.GreaterThan(0));
            Assert.That(plan.TotalSyncQueryBudget, Is.GreaterThan(0));
            Assert.That(plan.HelperTempQueryRiskCount, Is.EqualTo(0));
            Assert.That(plan.DependencyWaitRiskCount, Is.GreaterThanOrEqualTo(5));
            Assert.That(plan.WorldUpdateAllocatorOwnerCount, Is.EqualTo(1));
            Assert.That(plan.RewindableAllocatorCandidateCount, Is.EqualTo(1));

            Assert.That(
                plan.TryFind(EGasRuntimeFrameBudgetEntryId.RuntimeCoreFramePrepare, out var framePrepare),
                Is.True);
            Assert.That(framePrepare.Phase, Is.EqualTo(EGasRuntimeCoreFramePhase.FramePrepare));
            Assert.That(framePrepare.QueryBudget, Is.EqualTo(0));
            Assert.That(framePrepare.AllocatorOwner, Is.EqualTo(EGasRuntimeFrameAllocatorOwner.WorldUpdateAllocator));
            Assert.That(framePrepare.DependencyBudget, Is.EqualTo(EGasRuntimeFrameDependencyBudget.None));
            Assert.That(framePrepare.HasRisk(EGasRuntimeFrameBudgetRisk.FrameArenaOwner), Is.True);
            Assert.That(framePrepare.HasRisk(EGasRuntimeFrameBudgetRisk.WorldUpdateAllocatorCandidate), Is.True);
            Assert.That(framePrepare.HasRisk(EGasRuntimeFrameBudgetRisk.RewindableAllocatorCandidate), Is.True);
        }

        [Test]
        public void RuntimeKnownOwnerHelpersNoLongerOwnHelperTempQueryRisk()
        {
            var plan = GASRuntimeFrameBudgetPlanner.CreateCurrent();

            Assert.That(
                plan.TryFind(EGasRuntimeFrameBudgetEntryId.EffectCommandSpecStreamSingleton, out var streamEntry),
                Is.True);
            Assert.That(streamEntry.QueryBudget, Is.EqualTo(0));
            Assert.That(streamEntry.SyncQueryBudget, Is.EqualTo(0));
            Assert.That(streamEntry.AllocatorOwner, Is.EqualTo(EGasRuntimeFrameAllocatorOwner.NoNativeAllocation));
            Assert.That(streamEntry.DependencyBudget, Is.EqualTo(EGasRuntimeFrameDependencyBudget.ReadOnlyMainThread));
            Assert.That(streamEntry.HasRisk(EGasRuntimeFrameBudgetRisk.HelperTempQueryRisk), Is.False);
            Assert.That(streamEntry.HasRisk(EGasRuntimeFrameBudgetRisk.SyncQuery), Is.False);
            Assert.That(streamEntry.HasRisk(EGasRuntimeFrameBudgetRisk.ToEntityArrayTemp), Is.False);
            Assert.That(streamEntry.HasRisk(EGasRuntimeFrameBudgetRisk.MainThreadOnly), Is.True);
            Assert.That(streamEntry.HasRisk(EGasRuntimeFrameBudgetRisk.DependencyWaitRisk), Is.True);

            Assert.That(
                plan.TryFind(EGasRuntimeFrameBudgetEntryId.RuntimeFrameContextCurrentFrame, out var frameEntry),
                Is.True);
            Assert.That(frameEntry.QueryBudget, Is.EqualTo(0));
            Assert.That(frameEntry.SyncQueryBudget, Is.EqualTo(0));
            Assert.That(frameEntry.AllocatorOwner, Is.EqualTo(EGasRuntimeFrameAllocatorOwner.NoNativeAllocation));
            Assert.That(frameEntry.DependencyBudget, Is.EqualTo(EGasRuntimeFrameDependencyBudget.ReadOnlyMainThread));
            Assert.That(frameEntry.HasRisk(EGasRuntimeFrameBudgetRisk.HelperTempQueryRisk), Is.False);
            Assert.That(frameEntry.HasRisk(EGasRuntimeFrameBudgetRisk.SyncQuery), Is.False);
            Assert.That(frameEntry.HasRisk(EGasRuntimeFrameBudgetRisk.ToEntityArrayTemp), Is.False);
            Assert.That(frameEntry.HasRisk(EGasRuntimeFrameBudgetRisk.MainThreadOnly), Is.True);
            Assert.That(frameEntry.HasRisk(EGasRuntimeFrameBudgetRisk.DependencyWaitRisk), Is.True);

            Assert.That(
                plan.TryFind(EGasRuntimeFrameBudgetEntryId.RuntimeDebuggerCurrentFrame, out _),
                Is.False);
            Assert.That(
                plan.TryFind(EGasRuntimeFrameBudgetEntryId.PresentationOutboxProjectionCurrentFrame, out _),
                Is.False);
            Assert.That(
                plan.TryFind(EGasRuntimeFrameBudgetEntryId.DebugReplayLogProjectionCurrentFrame, out _),
                Is.False);
        }

        [Test]
        public void FrameBudgetEntriesMapToRuntimeCoreBackbonePhases()
        {
            var phaseSet = new HashSet<EGasRuntimeCoreFramePhase>();
            var phases = GASSystemScheduleContract.RuntimeCoreFramePhases;
            for (var i = 0; i < phases.Count; i++)
                phaseSet.Add(phases[i].Phase);

            var plan = GASRuntimeFrameBudgetPlanner.CreateCurrent();
            for (var i = 0; i < plan.Entries.Count; i++)
            {
                Assert.That(
                    phaseSet.Contains(plan.Entries[i].Phase),
                    Is.True,
                    plan.Entries[i].EntryId + " must belong to a declared Runtime Core frame phase.");
            }

            Assert.That(
                plan.CountEntriesForPhase(EGasRuntimeCoreFramePhase.FramePrepare),
                Is.GreaterThanOrEqualTo(3));
            Assert.That(
                plan.CountEntriesForPhase(EGasRuntimeCoreFramePhase.ObservationProjection),
                Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void RuntimeFrameContextDoesNotCreateFallbackQueryForIsolatedWorld()
        {
            using var world = new World("RuntimeFrameContextDoesNotCreateFallbackQueryForIsolatedWorld");
            var em = world.EntityManager;
            var timer = em.CreateEntity();
            em.AddComponentData(timer, new GlobalTimer { Frame = 23 });

            Assert.That(GASRuntimeFrameContext.TryResolveCurrentFrame(em, out var frame), Is.False);
            Assert.That(frame, Is.EqualTo(0));
            Assert.That(GASRuntimeFrameContext.ResolveCurrentFrame(em), Is.EqualTo(0));
        }

        [Test]
        public void RuntimeFrameBudgetContractDoesNotExposeUnityRuntimeHandles()
        {
            var checkedTypes = new[]
            {
                typeof(EGasRuntimeFrameBudgetEntryId),
                typeof(EGasRuntimeFrameAllocatorOwner),
                typeof(EGasRuntimeFrameDependencyBudget),
                typeof(EGasRuntimeFrameBudgetRisk),
                typeof(GASRuntimeFrameBudgetEntry),
                typeof(GASRuntimeFrameBudgetPlan),
                typeof(GASRuntimeFrameBudgetPlanner),
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
