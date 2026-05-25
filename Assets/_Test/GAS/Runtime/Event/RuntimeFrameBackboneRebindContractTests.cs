using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace GAS.Runtime.Tests.Event
{
    public sealed class RuntimeFrameBackboneRebindContractTests
    {
        [Test]
        public void CurrentRebindPlanClosesAm2bAndKeepsAutoChessAsLaterValidation()
        {
            var plan = GASRuntimeFrameBackboneRebindPlanner.CreateCurrent();

            Assert.That(plan.EntryCount, Is.EqualTo(2));
            Assert.That(plan.AM2BBackboneEvidenceComplete, Is.True);
            Assert.That(plan.PhaseCount, Is.GreaterThanOrEqualTo(8));
            Assert.That(plan.FrameBudgetEntryCount, Is.GreaterThan(0));
            Assert.That(plan.StreamOwnerEntryCount, Is.GreaterThan(0));
            Assert.That(plan.DeterministicMergePolicyCount, Is.EqualTo(plan.StreamOwnerEntryCount));
            Assert.That(plan.StructuralPlaybackRouteCount, Is.GreaterThan(0));
            Assert.That(plan.DebuggerEvidenceCoverageCount, Is.GreaterThanOrEqualTo(10));
            Assert.That(plan.AutoChessValidationAllowed, Is.False);
            Assert.That(
                plan.RecommendedNextTask,
                Is.EqualTo(GASRuntimeFrameBackboneNextTaskId.RuntimeCoreInstantSpecEvaluation));
        }

        [Test]
        public void Am3RebindRequiresFrameBackboneEvidenceAndCommandClassification()
        {
            var plan = GASRuntimeFrameBackboneRebindPlanner.CreateCurrent();

            Assert.That(
                plan.TryFind(GASRuntimeFrameBackboneRebindTaskId.InstantSpecEvaluation, out var am3),
                Is.True);
            Assert.That(am3.TaskTreeId, Is.EqualTo("T1-RuntimeCore-AM3"));
            Assert.That(am3.ContainsPhaseAnchor(EGasRuntimeCoreFramePhase.CommandIngest), Is.True);
            Assert.That(am3.ContainsPhaseAnchor(EGasRuntimeCoreFramePhase.SpecEvaluation), Is.True);
            Assert.That(am3.ContainsPhaseAnchor(EGasRuntimeCoreFramePhase.DeltaApply), Is.True);
            Assert.That(am3.ContainsPhaseAnchor(EGasRuntimeCoreFramePhase.TypedFactProjection), Is.True);
            Assert.That(am3.ContainsStream(EGasRuntimeFrameStreamId.EffectCommand), Is.True);
            Assert.That(am3.ContainsStream(EGasRuntimeFrameStreamId.InstantEffectSpec), Is.True);
            Assert.That(am3.ContainsStream(EGasRuntimeFrameStreamId.AttributeDelta), Is.True);
            Assert.That(am3.ContainsStream(EGasRuntimeFrameStreamId.TypedSimulationFact), Is.True);
            Assert.That(
                am3.HasCommandClassification(GASRuntimeFrameBackboneCommandClassification.BoundaryRequest),
                Is.True);
            Assert.That(
                am3.HasCommandClassification(GASRuntimeFrameBackboneCommandClassification.CoreFrameCommand),
                Is.True);
            Assert.That(
                am3.HasCommandClassification(GASRuntimeFrameBackboneCommandClassification.ParallelFanInStream),
                Is.True);
            Assert.That(
                am3.HasCommandClassification(GASRuntimeFrameBackboneCommandClassification.StructuralMutationRequest),
                Is.True);
            AssertRequiresAm2bEvidence(am3);
            Assert.That(
                am3.HasPolicy(GASRuntimeFrameBackboneRebindPolicy.RejectSingletonDynamicBufferAsScaleReady),
                Is.True);
            Assert.That(am3.HasPolicy(GASRuntimeFrameBackboneRebindPolicy.ReuseFrameStreamOwner), Is.True);
            Assert.That(am3.HasPolicy(GASRuntimeFrameBackboneRebindPolicy.RequireApiSelectionTable), Is.True);
            Assert.That(am3.StructuralGateId, Is.EqualTo(GASRuntimeStructuralPlaybackGateId.RuntimeCoreHotPath));
        }

        [Test]
        public void Am5RebindRequiresStoreSurfacesAndStructuralPlaybackGate()
        {
            var plan = GASRuntimeFrameBackboneRebindPlanner.CreateCurrent();

            Assert.That(
                plan.TryFind(GASRuntimeFrameBackboneRebindTaskId.ActiveEffectStore, out var am5),
                Is.True);
            Assert.That(am5.TaskTreeId, Is.EqualTo("T1-RuntimeCore-AM5"));
            Assert.That(am5.ContainsPhaseAnchor(EGasRuntimeCoreFramePhase.ActiveEffectLifecycle), Is.True);
            Assert.That(am5.ContainsPhaseAnchor(EGasRuntimeCoreFramePhase.DeltaApply), Is.True);
            Assert.That(am5.ContainsPhaseAnchor(EGasRuntimeCoreFramePhase.StructuralPlayback), Is.True);
            Assert.That(am5.ContainsStream(EGasRuntimeFrameStreamId.ActiveEffectMutation), Is.True);
            Assert.That(am5.ContainsStream(EGasRuntimeFrameStreamId.AttributeDelta), Is.True);
            Assert.That(am5.HasStoreSurface(GASRuntimeFrameBackboneStoreSurface.OwnerLocalStore), Is.True);
            Assert.That(am5.HasStoreSurface(GASRuntimeFrameBackboneStoreSurface.GlobalIndexedStore), Is.True);
            Assert.That(am5.HasStoreSurface(GASRuntimeFrameBackboneStoreSurface.LifecycleCleanupStore), Is.True);
            Assert.That(am5.HasStoreSurface(GASRuntimeFrameBackboneStoreSurface.ChunkSkipIndex), Is.True);
            Assert.That(
                am5.HasCommandClassification(GASRuntimeFrameBackboneCommandClassification.StructuralMutationRequest),
                Is.True);
            AssertRequiresAm2bEvidence(am5);
            Assert.That(
                am5.HasPolicy(GASRuntimeFrameBackboneRebindPolicy.RejectLegacyBackedMirrorAsScaleReady),
                Is.True);
            Assert.That(am5.HasPolicy(GASRuntimeFrameBackboneRebindPolicy.ReuseStructuralPlaybackGate), Is.True);
            Assert.That(am5.StructuralGateId, Is.EqualTo(GASRuntimeStructuralPlaybackGateId.RuntimeCoreHotPath));
        }

        [Test]
        public void RebindEntriesRequireDocsDebuggerAndNoAutoChessBusinessChange()
        {
            var plan = GASRuntimeFrameBackboneRebindPlanner.CreateCurrent();

            Assert.That(
                plan.CountEntriesRequiringEvidence(GASRuntimeFrameBackboneRequiredEvidence.ApiSelectionTable),
                Is.EqualTo(plan.EntryCount));
            Assert.That(
                plan.CountEntriesRequiringEvidence(GASRuntimeFrameBackboneRequiredEvidence.OfficialDocCoverage),
                Is.EqualTo(plan.EntryCount));
            Assert.That(
                plan.CountEntriesRequiringEvidence(GASRuntimeFrameBackboneRequiredEvidence.DebuggerEvidenceGate),
                Is.EqualTo(plan.EntryCount));
            Assert.That(
                plan.CountEntriesRequiringEvidence(GASRuntimeFrameBackboneRequiredEvidence.NoAutoChessBusinessChange),
                Is.EqualTo(plan.EntryCount));
            Assert.That(
                plan.CountEntriesWithPolicy(GASRuntimeFrameBackboneRebindPolicy.PreferRuntimeCoreBeforeAutoChessValidation),
                Is.EqualTo(plan.EntryCount));
            Assert.That(
                plan.CountEntriesWithPolicy(GASRuntimeFrameBackboneRebindPolicy.NoAutoChessBusinessChange),
                Is.EqualTo(plan.EntryCount));
        }

        [Test]
        public void RuntimeFrameBackboneRebindContractDoesNotExposeUnityRuntimeHandles()
        {
            var checkedTypes = new[]
            {
                typeof(GASRuntimeFrameBackboneRebindTaskId),
                typeof(GASRuntimeFrameBackboneNextTaskId),
                typeof(GASRuntimeFrameBackboneRequiredEvidence),
                typeof(GASRuntimeFrameBackboneCommandClassification),
                typeof(GASRuntimeFrameBackboneStoreSurface),
                typeof(GASRuntimeFrameBackboneRebindPolicy),
                typeof(GASRuntimeFrameBackboneRebindEntry),
                typeof(GASRuntimeFrameBackboneRebindPlan),
                typeof(GASRuntimeFrameBackboneRebindPlanner),
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

        private static void AssertRequiresAm2bEvidence(GASRuntimeFrameBackboneRebindEntry entry)
        {
            Assert.That(entry.HasRequiredEvidence(GASRuntimeFrameBackboneRequiredEvidence.PhaseContract), Is.True);
            Assert.That(entry.HasRequiredEvidence(GASRuntimeFrameBackboneRequiredEvidence.FrameBudget), Is.True);
            Assert.That(entry.HasRequiredEvidence(GASRuntimeFrameBackboneRequiredEvidence.StreamOwner), Is.True);
            Assert.That(entry.HasRequiredEvidence(GASRuntimeFrameBackboneRequiredEvidence.DeterministicMerge), Is.True);
            Assert.That(entry.HasRequiredEvidence(GASRuntimeFrameBackboneRequiredEvidence.StructuralPlaybackGate), Is.True);
            Assert.That(entry.HasRequiredEvidence(GASRuntimeFrameBackboneRequiredEvidence.DebuggerEvidenceGate), Is.True);
            Assert.That(entry.HasRequiredEvidence(GASRuntimeFrameBackboneRequiredEvidence.ApiSelectionTable), Is.True);
            Assert.That(entry.HasRequiredEvidence(GASRuntimeFrameBackboneRequiredEvidence.OfficialDocCoverage), Is.True);
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
