using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace GAS.Runtime.Tests.Event
{
    public sealed class RuntimeQueryLayoutPlanTests
    {
        [Test]
        public void CurrentLayoutSeparatesSimulationObservationAndManagedPresentation()
        {
            var plan = GASRuntimeQueryLayoutPlanner.CreateCurrent();

            Assert.That(plan.EntryCount, Is.GreaterThanOrEqualTo(12));
            Assert.That(
                plan.TryFind(GASRuntimeQueryLayoutEntryId.AscStableState, out var asc),
                Is.True);
            Assert.That(asc.EntityKind, Is.EqualTo(GASRuntimeEntityKind.AbilitySystemComponent));
            Assert.That(asc.HasCapability(GASRuntimeLayoutCapability.GeneratedArchetypeCandidate), Is.True);
            Assert.That(asc.HasCapability(GASRuntimeLayoutCapability.BurstCandidate), Is.True);
            Assert.That(asc.HasRequiredSlot(GASRuntimeLayoutComponentSlot.AttributeBuffer), Is.True);
            Assert.That(asc.HasBoundary(GASRuntimeLayoutBoundary.ManagedPresentationBoundary), Is.False);

            Assert.That(
                plan.TryFind(GASRuntimeQueryLayoutEntryId.ObservationReplayAndOutbox, out var observation),
                Is.True);
            Assert.That(observation.HasCapability(GASRuntimeLayoutCapability.ObservationOnly), Is.True);
            Assert.That(observation.HasCapability(GASRuntimeLayoutCapability.WritesSimulationState), Is.False);
            Assert.That(observation.HasBoundary(GASRuntimeLayoutBoundary.ObservationReadBoundary), Is.True);

            Assert.That(
                plan.TryFind(GASRuntimeQueryLayoutEntryId.ManagedCuePresentation, out var cue),
                Is.True);
            Assert.That(cue.HasCapability(GASRuntimeLayoutCapability.ManagedPresentationBoundary), Is.True);
            Assert.That(cue.HasCapability(GASRuntimeLayoutCapability.BurstCandidate), Is.False);
            Assert.That(cue.HasRequiredSlot(GASRuntimeLayoutComponentSlot.ManagedCueComponent), Is.True);
        }

        [Test]
        public void CurrentLayoutMarksStructuralHotspotsBeforeFullGeneratedIntegration()
        {
            var plan = GASRuntimeQueryLayoutPlanner.CreateCurrent();

            Assert.That(plan.RequiresEcbMigrationBeforeFullGeneratedRuntimeIntegration, Is.True);
            Assert.That(plan.EcbMigrationCandidateCount, Is.GreaterThanOrEqualTo(5));
            Assert.That(
                plan.CountEntriesWithBoundary(GASRuntimeLayoutBoundary.StructuralEntityManagerHotspot),
                Is.EqualTo(plan.EcbMigrationCandidateCount));

            AssertNeedsEcb(plan, GASRuntimeQueryLayoutEntryId.AbilityCommitGate);
            AssertNeedsEcb(plan, GASRuntimeQueryLayoutEntryId.GameplayEffectApplyRequest);
            AssertNeedsEcb(plan, GASRuntimeQueryLayoutEntryId.GameplayEffectActiveRuntime);
            AssertNeedsEcb(plan, GASRuntimeQueryLayoutEntryId.ExecutionCalculationPipeline);
        }

        [Test]
        public void CurrentLayoutKeepsJobFriendlyEntriesOutOfMainThreadHotspotSet()
        {
            var plan = GASRuntimeQueryLayoutPlanner.CreateCurrent();

            Assert.That(plan.JobCandidateCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(plan.BurstCandidateCount, Is.GreaterThanOrEqualTo(4));

            AssertJobFriendly(plan, GASRuntimeQueryLayoutEntryId.AbilityTickLifecycle);
            AssertJobFriendly(plan, GASRuntimeQueryLayoutEntryId.AttributeRecalculate);
            AssertJobFriendly(plan, GASRuntimeQueryLayoutEntryId.TagMaskRuntime);
        }

        [Test]
        public void CurrentLayoutMarksActiveEffectStoreAsOwnerLocalTargetContract()
        {
            var plan = GASRuntimeQueryLayoutPlanner.CreateCurrent();

            Assert.That(
                plan.TryFind(GASRuntimeQueryLayoutEntryId.ActiveEffectStore, out var store),
                Is.True);
            Assert.That(store.EntityKind, Is.EqualTo(GASRuntimeEntityKind.AbilitySystemComponent));
            Assert.That(store.Decision, Is.EqualTo(GASRuntimeLayoutDecision.TargetContract));
            Assert.That(store.HasRequiredSlot(GASRuntimeLayoutComponentSlot.ActiveEffectStore), Is.True);
            Assert.That(store.HasRequiredSlot(GASRuntimeLayoutComponentSlot.ActiveEffectSlotBuffer), Is.True);
            Assert.That(store.HasOptionalSlot(GASRuntimeLayoutComponentSlot.ActiveEffectGlobalIndexStore), Is.True);
            Assert.That(store.HasOptionalSlot(GASRuntimeLayoutComponentSlot.ActiveEffectGlobalIndexBuffer), Is.True);
            Assert.That(store.HasCapability(GASRuntimeLayoutCapability.NoPerHitStructuralChange), Is.True);
            Assert.That(store.HasBoundary(GASRuntimeLayoutBoundary.DynamicBufferMutation), Is.True);
            Assert.That(store.HasBoundary(GASRuntimeLayoutBoundary.StructuralEntityManagerHotspot), Is.False);
        }

        [Test]
        public void GameplayEffectActiveRuntimeDeclaresCleanupAndFinalDestroyMarkers()
        {
            var plan = GASRuntimeQueryLayoutPlanner.CreateCurrent();

            Assert.That(
                plan.TryFind(GASRuntimeQueryLayoutEntryId.GameplayEffectActiveRuntime, out var active),
                Is.True);
            Assert.That(active.HasOptionalSlot(GASRuntimeLayoutComponentSlot.EffectCleanup), Is.True);
            Assert.That(active.HasOptionalSlot(GASRuntimeLayoutComponentSlot.EffectDestroy), Is.True);
            Assert.That(active.HasOptionalSlot(GASRuntimeLayoutComponentSlot.EffectFinalDestroy), Is.True);
            Assert.That(active.HasOptionalSlot(GASRuntimeLayoutComponentSlot.ActiveEffectGlobalIndexStableRow), Is.True);
        }

        [Test]
        public void LayoutPlanSystemTypesArePartOfExplicitGasSchedule()
        {
            var scheduled = new HashSet<Type>();
            Add(scheduled, GASSystemScheduleContract.CommandSystems);
            Add(scheduled, GASSystemScheduleContract.ExecutionCalculationExtensionSystems);
            Add(scheduled, GASSystemScheduleContract.ResetDirtySystems);
            Add(scheduled, GASSystemScheduleContract.TagSystems);
            Add(scheduled, GASSystemScheduleContract.EffectSystems);
            Add(scheduled, GASSystemScheduleContract.AttributeSystems);
            Add(scheduled, GASSystemScheduleContract.AbilitySystems);
            Add(scheduled, GASSystemScheduleContract.CueSystems);

            var offenders = new List<string>();
            var plan = GASRuntimeQueryLayoutPlanner.CreateCurrent();
            for (var i = 0; i < plan.Entries.Count; i++)
            {
                var entry = plan.Entries[i];
                for (var j = 0; j < entry.SystemTypes.Count; j++)
                {
                    var systemType = entry.SystemTypes[j];
                    if (!scheduled.Contains(systemType))
                        offenders.Add(entry.EntryId + " -> " + systemType.Name);
                }
            }

            Assert.That(
                offenders,
                Is.Empty,
                "Runtime layout plan must reference systems owned by GASSystemScheduleContract.");
        }

        [Test]
        public void RuntimeLayoutContractDoesNotExposeEditorGeneratedOrRuntimeHandles()
        {
            var checkedTypes = new[]
            {
                typeof(GASRuntimeQueryLayoutEntryId),
                typeof(GASRuntimeLayoutDomain),
                typeof(GASRuntimeEntityKind),
                typeof(GASRuntimeLayoutDecision),
                typeof(GASRuntimeLayoutCapability),
                typeof(GASRuntimeLayoutBoundary),
                typeof(GASRuntimeLayoutComponentSlot),
                typeof(GASRuntimeQueryLayoutEntry),
                typeof(GASRuntimeQueryLayoutPlan),
                typeof(GASRuntimeQueryLayoutPlanner),
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

        private static void AssertNeedsEcb(
            GASRuntimeQueryLayoutPlan plan,
            GASRuntimeQueryLayoutEntryId entryId)
        {
            Assert.That(plan.TryFind(entryId, out var entry), Is.True);
            Assert.That(entry.Decision, Is.EqualTo(GASRuntimeLayoutDecision.NeedsEcbMigration));
            Assert.That(entry.HasCapability(GASRuntimeLayoutCapability.EcbMigrationCandidate), Is.True);
            Assert.That(entry.HasCapability(GASRuntimeLayoutCapability.RequiresMainThreadEntityManager), Is.True);
            Assert.That(entry.HasBoundary(GASRuntimeLayoutBoundary.StructuralEntityManagerHotspot), Is.True);
        }

        private static void AssertJobFriendly(
            GASRuntimeQueryLayoutPlan plan,
            GASRuntimeQueryLayoutEntryId entryId)
        {
            Assert.That(plan.TryFind(entryId, out var entry), Is.True);
            Assert.That(entry.Decision, Is.EqualTo(GASRuntimeLayoutDecision.StableQueryLayout));
            Assert.That(entry.HasCapability(GASRuntimeLayoutCapability.JobCandidate), Is.True);
            Assert.That(entry.HasCapability(GASRuntimeLayoutCapability.BurstCandidate), Is.True);
            Assert.That(entry.HasCapability(GASRuntimeLayoutCapability.RequiresMainThreadEntityManager), Is.False);
            Assert.That(entry.HasBoundary(GASRuntimeLayoutBoundary.StructuralEntityManagerHotspot), Is.False);
        }

        private static void Add(HashSet<Type> set, IReadOnlyList<Type> values)
        {
            for (var i = 0; i < values.Count; i++)
                set.Add(values[i]);
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
