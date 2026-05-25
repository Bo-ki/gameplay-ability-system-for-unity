using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace GAS.Runtime.Tests.Event
{
    public sealed class RuntimeStructuralChangePlanTests
    {
        [Test]
        public void StructuralPlanConsumesAllLayoutStructuralHotspots()
        {
            var layout = GASRuntimeQueryLayoutPlanner.CreateCurrent();
            var structural = GASRuntimeStructuralChangePlanner.CreateCurrent(layout);

            Assert.That(structural.EntryCount, Is.GreaterThanOrEqualTo(10));
            Assert.That(structural.RequiresEcbMigrationBeforeEnableableRollout, Is.True);
            Assert.That(structural.EcbFirstCount, Is.GreaterThanOrEqualTo(6));

            var missing = new List<GASRuntimeQueryLayoutEntryId>();
            for (var i = 0; i < layout.Entries.Count; i++)
            {
                var layoutEntry = layout.Entries[i];
                if (!layoutEntry.HasBoundary(GASRuntimeLayoutBoundary.StructuralEntityManagerHotspot))
                    continue;

                if (!structural.TryFindFirstByLayoutEntry(layoutEntry.EntryId, out var structuralEntry)
                    || !structuralEntry.HasEligibility(GASRuntimeStructuralEligibility.MirrorsLayoutStructuralHotspot))
                {
                    missing.Add(layoutEntry.EntryId);
                }
            }

            Assert.That(missing, Is.Empty);
        }

        [Test]
        public void AscCreateRequestIsAlreadyEcbAndEnableableMarkerExample()
        {
            var plan = GASRuntimeStructuralChangePlanner.CreateCurrent();

            Assert.That(plan.TryFind(GASRuntimeStructuralChangeEntryId.AscCreateRequest, out var entry), Is.True);
            Assert.That(entry.LayoutEntryId, Is.EqualTo(GASRuntimeQueryLayoutEntryId.AscStableState));
            Assert.That(entry.HasMigrationStep(GASRuntimeStructuralMigrationStep.AlreadyEcb), Is.True);
            Assert.That(entry.HasEligibility(GASRuntimeStructuralEligibility.AlreadyUsesEcb), Is.True);
            Assert.That(entry.HasOperation(GASRuntimeStructuralOperation.CreateEntity), Is.True);
            Assert.That(entry.HasOperation(GASRuntimeStructuralOperation.SetComponentEnabled), Is.True);
            Assert.That(entry.HasEnableableScope(GASRuntimeEnableableScope.ExistingRequestMarker), Is.True);
            Assert.That(ContainsSystem(entry.SystemTypes, typeof(SASCCreate)), Is.True);
        }

        [Test]
        public void AbilityCommitUsesEcbBeforeEnableableAndRequiresSemanticDecision()
        {
            var plan = GASRuntimeStructuralChangePlanner.CreateCurrent();

            Assert.That(plan.TryFind(GASRuntimeStructuralChangeEntryId.AbilityCommitGateMutation, out var entry), Is.True);
            Assert.That(entry.LayoutEntryId, Is.EqualTo(GASRuntimeQueryLayoutEntryId.AbilityCommitGate));
            Assert.That(entry.HasMigrationStep(GASRuntimeStructuralMigrationStep.EcbFirst), Is.True);
            Assert.That(entry.HasMigrationStep(GASRuntimeStructuralMigrationStep.EnableableAfterEcb), Is.True);
            Assert.That(entry.HasMigrationStep(GASRuntimeStructuralMigrationStep.AlreadyEcb), Is.False);
            Assert.That(entry.HasEligibility(GASRuntimeStructuralEligibility.CanUseEcb), Is.True);
            Assert.That(entry.HasEligibility(GASRuntimeStructuralEligibility.RequiresSemanticDecision), Is.True);
            Assert.That(entry.HasEligibility(GASRuntimeStructuralEligibility.CanUseDirtyPipeline), Is.False);
            Assert.That(entry.HasEnableableScope(GASRuntimeEnableableScope.TransientCommandMarker), Is.True);
            Assert.That(entry.HasEnableableScope(GASRuntimeEnableableScope.RuntimeSemanticStateRequiresDecision), Is.True);
            Assert.That(entry.HasAffectedSlot(GASRuntimeLayoutComponentSlot.AbilityActive), Is.True);
            Assert.That(entry.HasAffectedSlot(GASRuntimeLayoutComponentSlot.AbilityCommitRequest), Is.True);
        }

        [Test]
        public void DirtyPipelineCandidatesAreLimitedToSimulationDirtyMarkers()
        {
            var plan = GASRuntimeStructuralChangePlanner.CreateCurrent();

            Assert.That(plan.HasDirtyPipelineCandidates, Is.True);
            Assert.That(plan.DirtyPipelineCandidateCount, Is.GreaterThanOrEqualTo(5));
            AssertDirtyCandidate(
                plan,
                GASRuntimeStructuralChangeEntryId.AttributeDirtyRecalculate,
                GASRuntimeDirtyPipelineSignal.AttributeBaseValueDirty);
            AssertDirtyCandidate(
                plan,
                GASRuntimeStructuralChangeEntryId.TagMaskDirtySync,
                GASRuntimeDirtyPipelineSignal.TagMaskDirty);
            AssertDirtyCandidate(
                plan,
                GASRuntimeStructuralChangeEntryId.ExecutionCalculationOutputMutation,
                GASRuntimeDirtyPipelineSignal.ExecutionCalculationOutputDirty);
            AssertDirtyCandidate(
                plan,
                GASRuntimeStructuralChangeEntryId.GameplayEffectActiveRuntimeMutation,
                GASRuntimeDirtyPipelineSignal.GameplayEffectModifierDirty);

            Assert.That(
                plan.TryFind(GASRuntimeStructuralChangeEntryId.AbilityCommitGateMutation, out var commit),
                Is.True);
            Assert.That(commit.HasEligibility(GASRuntimeStructuralEligibility.CanUseDirtyPipeline), Is.False);
            Assert.That(commit.HasDirtySignal(GASRuntimeDirtyPipelineSignal.AttributeBaseValueDirty), Is.False);
        }

        [Test]
        public void ManagedCuePresentationStaysOutOfSimulationMigrationContract()
        {
            var plan = GASRuntimeStructuralChangePlanner.CreateCurrent();

            Assert.That(
                plan.TryFind(GASRuntimeStructuralChangeEntryId.ManagedCuePresentationBoundary, out var entry),
                Is.True);
            Assert.That(entry.LayoutEntryId, Is.EqualTo(GASRuntimeQueryLayoutEntryId.ManagedCuePresentation));
            Assert.That(entry.IsSimulationMigrationCandidate, Is.False);
            Assert.That(entry.HasMigrationStep(GASRuntimeStructuralMigrationStep.ManagedBoundaryOnly), Is.True);
            Assert.That(entry.HasMigrationStep(GASRuntimeStructuralMigrationStep.KeepMainThread), Is.True);
            Assert.That(entry.HasEligibility(GASRuntimeStructuralEligibility.RequiresManagedBoundary), Is.True);
            Assert.That(entry.HasEligibility(GASRuntimeStructuralEligibility.CanUseEcb), Is.False);
            Assert.That(entry.HasEligibility(GASRuntimeStructuralEligibility.CanUseDirtyPipeline), Is.False);
            Assert.That(entry.HasEnableableScope(GASRuntimeEnableableScope.ManagedPresentationState), Is.True);
        }

        [Test]
        public void StructuralPlanReferencesOnlyExistingLayoutEntries()
        {
            var layout = GASRuntimeQueryLayoutPlanner.CreateCurrent();
            var structural = GASRuntimeStructuralChangePlanner.CreateCurrent(layout);
            var missing = new List<GASRuntimeQueryLayoutEntryId>();

            for (var i = 0; i < structural.Entries.Count; i++)
            {
                var entry = structural.Entries[i];
                if (!layout.TryFind(entry.LayoutEntryId, out _))
                    missing.Add(entry.LayoutEntryId);
            }

            Assert.That(missing, Is.Empty);
        }

        [Test]
        public void StructuralPlaybackGateDeclaresUniqueContractOnlyGate()
        {
            var plan = GASRuntimeStructuralPlaybackGatePlanner.CreateCurrent();

            Assert.That(plan.Gate.GateId, Is.EqualTo(GASRuntimeStructuralPlaybackGateId.RuntimeCoreHotPath));
            Assert.That(plan.Gate.GroupType, Is.EqualTo(typeof(GasStructuralPlaybackSystemGroup)));
            Assert.That(plan.Gate.EndEcbSystemType, Is.EqualTo(typeof(GasEndStructuralEcbSystem)));
            Assert.That(plan.Gate.Phase, Is.EqualTo(EGasRuntimeCoreFramePhase.StructuralPlayback));
            Assert.That(
                plan.Gate.StructuralPermission,
                Is.EqualTo(EGasRuntimeCoreStructuralPermission.PlaybackOnly));
            Assert.That(plan.Gate.UniqueHotPathGate, Is.True);
            Assert.That(plan.Gate.ContractOnly, Is.True);
            Assert.That(plan.Gate.HasEvidence(GASRuntimeStructuralPlaybackEvidence.UniqueHotPathGate), Is.True);
            Assert.That(plan.Gate.HasEvidence(GASRuntimeStructuralPlaybackEvidence.EcbPlaybackCount), Is.True);
            Assert.That(plan.Gate.HasEvidence(GASRuntimeStructuralPlaybackEvidence.EcbCommandCount), Is.True);
            Assert.That(plan.RequiredStructuralPlaybackCount, Is.GreaterThanOrEqualTo(6));
            Assert.That(plan.DebuggerGateEvidenceCount, Is.EqualTo(plan.RequiredStructuralPlaybackCount));

            var phase = GetPhaseContract(EGasRuntimeCoreFramePhase.StructuralPlayback);
            Assert.That(phase.CurrentGroupType, Is.EqualTo(typeof(GasStructuralPlaybackSystemGroup)));
            Assert.That(phase.StructuralPermission, Is.EqualTo(EGasRuntimeCoreStructuralPermission.PlaybackOnly));
        }

        [Test]
        public void StructuralPlaybackGateRoutesSimulationStructuralEntriesToSinglePlaybackPhase()
        {
            var structural = GASRuntimeStructuralChangePlanner.CreateCurrent();
            var playback = GASRuntimeStructuralPlaybackGatePlanner.CreateCurrent(structural);

            for (var i = 0; i < structural.Entries.Count; i++)
            {
                var entry = structural.Entries[i];
                if (!entry.IsSimulationMigrationCandidate || !HasStructuralPlaybackOperation(entry))
                    continue;

                Assert.That(playback.TryFind(entry.EntryId, out var route), Is.True);
                Assert.That(route.RequiresStructuralPlayback, Is.True, entry.EntryId.ToString());
                Assert.That(route.RoutesToStructuralPlaybackGate, Is.True, entry.EntryId.ToString());
                Assert.That(route.RecordPhase, Is.Not.EqualTo(EGasRuntimeCoreFramePhase.StructuralPlayback));
                Assert.That(route.PlaybackPhase, Is.EqualTo(EGasRuntimeCoreFramePhase.StructuralPlayback));
                Assert.That(route.DirectEntityManagerAllowed, Is.False);
                Assert.That(route.HasPolicy(GASRuntimeStructuralPlaybackPolicy.EcbCommandBuffer), Is.True);
                Assert.That(route.HasEvidence(GASRuntimeStructuralPlaybackEvidence.RecordOnlySourcePhase), Is.True);
                Assert.That(route.HasEvidence(GASRuntimeStructuralPlaybackEvidence.PlaybackOnlyGatePhase), Is.True);
                Assert.That(route.HasEvidence(GASRuntimeStructuralPlaybackEvidence.DebuggerGateTag), Is.True);
                Assert.That(route.HasEvidence(GASRuntimeStructuralPlaybackEvidence.LocalPlaybackMigration), Is.True);
            }
        }

        [Test]
        public void StructuralPlaybackGateProvidesBulkCleanupAndDirtyPolicies()
        {
            var plan = GASRuntimeStructuralPlaybackGatePlanner.CreateCurrent();

            Assert.That(
                plan.TryFind(GASRuntimeStructuralChangeEntryId.GameplayEffectActiveRuntimeMutation, out var active),
                Is.True);
            Assert.That(active.HasPolicy(GASRuntimeStructuralPlaybackPolicy.EcbCommandBuffer), Is.True);
            Assert.That(active.HasPolicy(GASRuntimeStructuralPlaybackPolicy.EntityQueryBulkCandidate), Is.True);
            Assert.That(active.HasPolicy(GASRuntimeStructuralPlaybackPolicy.ComponentTypeSetBulkCandidate), Is.True);
            Assert.That(active.HasPolicy(GASRuntimeStructuralPlaybackPolicy.CleanupComponentCandidate), Is.True);
            Assert.That(active.HasPolicy(GASRuntimeStructuralPlaybackPolicy.EnableablePreferred), Is.True);
            Assert.That(active.HasEvidence(GASRuntimeStructuralPlaybackEvidence.BulkQueryPolicy), Is.True);
            Assert.That(active.HasEvidence(GASRuntimeStructuralPlaybackEvidence.CleanupPolicy), Is.True);

            Assert.That(
                plan.TryFind(GASRuntimeStructuralChangeEntryId.GameplayEffectApplyRequestConsumption, out var apply),
                Is.True);
            Assert.That(apply.HasPolicy(GASRuntimeStructuralPlaybackPolicy.EntityQueryBulkCandidate), Is.True);
            Assert.That(apply.HasPolicy(GASRuntimeStructuralPlaybackPolicy.ComponentTypeSetBulkCandidate), Is.True);
            Assert.That(apply.HasPolicy(GASRuntimeStructuralPlaybackPolicy.CleanupComponentCandidate), Is.True);

            Assert.That(
                plan.TryFind(GASRuntimeStructuralChangeEntryId.AbilityLifecycleCleanup, out var abilityCleanup),
                Is.True);
            Assert.That(abilityCleanup.HasPolicy(GASRuntimeStructuralPlaybackPolicy.CleanupComponentCandidate), Is.True);

            Assert.That(
                plan.TryFind(GASRuntimeStructuralChangeEntryId.AttributeDirtyRecalculate, out var attributeDirty),
                Is.True);
            Assert.That(attributeDirty.RequiresStructuralPlayback, Is.False);
            Assert.That(
                attributeDirty.Status,
                Is.EqualTo(GASRuntimeStructuralPlaybackRouteStatus.DirtyPipelineNoPlayback));
            Assert.That(
                attributeDirty.HasPolicy(GASRuntimeStructuralPlaybackPolicy.DirtyPipelineNoStructuralChange),
                Is.True);
        }

        [Test]
        public void StructuralPlaybackGateKeepsObservationAndManagedPresentationOutOfHotPathGate()
        {
            var plan = GASRuntimeStructuralPlaybackGatePlanner.CreateCurrent();

            Assert.That(
                plan.TryFind(GASRuntimeStructuralChangeEntryId.ObservationProjectionBoundary, out var observation),
                Is.True);
            Assert.That(observation.RequiresStructuralPlayback, Is.False);
            Assert.That(observation.GateId, Is.EqualTo(GASRuntimeStructuralPlaybackGateId.None));
            Assert.That(
                observation.Status,
                Is.EqualTo(GASRuntimeStructuralPlaybackRouteStatus.ObservationBoundaryNoPlayback));
            Assert.That(observation.HasPolicy(GASRuntimeStructuralPlaybackPolicy.ObservationNoPlayback), Is.True);

            Assert.That(
                plan.TryFind(GASRuntimeStructuralChangeEntryId.ManagedCuePresentationBoundary, out var managed),
                Is.True);
            Assert.That(managed.RequiresStructuralPlayback, Is.False);
            Assert.That(managed.GateId, Is.EqualTo(GASRuntimeStructuralPlaybackGateId.None));
            Assert.That(
                managed.Status,
                Is.EqualTo(GASRuntimeStructuralPlaybackRouteStatus.ManagedBoundaryNoPlayback));
            Assert.That(managed.HasPolicy(GASRuntimeStructuralPlaybackPolicy.ManagedBoundaryOnly), Is.True);
        }

        [Test]
        public void EcbFirstRuntimeSystemsUseLocalPlaybackForSameTickSemantics()
        {
            AssertUsesLocalEcb(
                "Assets/GAS/Runtime/System/Ability/STryActivateAbility.cs",
                "ecb.AddComponent<CAbilityCommitRequest>",
                "ecb.RemoveComponent<CAbilityInTryActivate>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Ability/STryActivateAbility.cs",
                "em.AddComponent<CAbilityCommitRequest>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Ability/STryActivateAbility.cs",
                "em.RemoveComponent<CAbilityInTryActivate>");

            AssertUsesLocalEcb(
                "Assets/GAS/Runtime/System/Ability/SAbilityCommit.cs",
                "ecb.AddComponent<CAbilityActive>",
                "ecb.RemoveComponent<CAbilityCommitRequest>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Ability/SAbilityCommit.cs",
                "em.AddComponent<CAbilityActive>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Ability/SAbilityCommit.cs",
                "em.RemoveComponent<CAbilityCommitRequest>");

            AssertUsesLocalEcb(
                "Assets/GAS/Runtime/System/Effect/SRemoveGameplayEffectRequest.cs",
                "ecb.AddComponent<CEffectDestroy>",
                "ecb.DestroyEntity(requestEntity)");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SRemoveGameplayEffectRequest.cs",
                "em.AddComponent<CEffectDestroy>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SRemoveGameplayEffectRequest.cs",
                "em.DestroyEntity(requestEntity)");

            AssertUsesLocalEcb(
                "Assets/GAS/Runtime/System/Effect/SExecutionCalculation.cs",
                "ecb.AddBuffer<BExecutionCalculationValue>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SExecutionCalculation.cs",
                "em.AddBuffer<BExecutionCalculationValue>");
            AssertUsesLocalEcb(
                "Assets/GAS/Runtime/System/Effect/EffectMagnitudeResolver.cs",
                "ecb.AddBuffer<BResolvedModifier>",
                "ecb.AddBuffer<BAttributeCaptureValue>",
                "PlaybackAndReset(ref ecb, em)");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/EffectMagnitudeResolver.cs",
                "em.AddBuffer<BResolvedModifier>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/EffectMagnitudeResolver.cs",
                "em.AddBuffer<BAttributeCaptureValue>");
            AssertUsesLocalEcb(
                "Assets/GAS/Runtime/System/Effect/SExecutionCalculationOutputModifier.cs",
                "EffectMagnitudeResolver.ResolveModifiers(em, ref ecb",
                "ecb.AddBuffer<BResolvedModifier>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SExecutionCalculationOutputModifier.cs",
                "em.AddBuffer<BResolvedModifier>");

            AssertUsesLocalEcb(
                "Assets/GAS/Runtime/System/Effect/SApplyGameplayEffectRequest.cs",
                "SetOrAdd(em, ref ecb, ge, context)",
                "SetOrAdd(em, ref ecb, ge, spec)",
                "CopySetByCallerValuesToEffect(em, ref ecb",
                "CopyTargetDataSummaryToEffect(em, ref ecb",
                "EffectMagnitudeResolver.ResolveModifiers(em, ref ecb",
                "ecb.DestroyEntity(requestEntity)",
                "PlaybackAndReset(ref ecb, em)");
            Assert.That(
                ReadProjectFile("Assets/GAS/Runtime/System/Effect/SApplyGameplayEffectRequest.cs")
                    .Contains("GameplayEffectConfigRegistry.CreateRuntimeEffectInstance("),
                Is.True,
                "SApplyGameplayEffectRequest must keep registry/prototype creation as an explicit immediate owner boundary.");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SApplyGameplayEffectRequest.cs",
                "em.DestroyEntity(requestEntity)");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SApplyGameplayEffectRequest.cs",
                "em.AddComponentData");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SApplyGameplayEffectRequest.cs",
                "em.AddBuffer<BSetByCallerValue>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SApplyGameplayEffectRequest.cs",
                "em.AddBuffer<BEffectTargetPoint>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SApplyGameplayEffectRequest.cs",
                "em.AddBuffer<BEffectTargetDirection>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SApplyGameplayEffectRequest.cs",
                "em.AddBuffer<BEffectTargetHit>");

            AssertUsesLocalEcb(
                "Assets/GAS/Runtime/System/Effect/SEffectApply.cs",
                "EffectRuntimeUtility.EnsureLifecycle(",
                "EffectRuntimeUtility.DestroyEffectEntity(em, ref ecb",
                "EffectRuntimeUtility.RemoveActiveGameplayEffectsWithTags(em, ref ecb",
                "EffectRuntimeUtility.TryMergeStackingApplication(em, ref ecb",
                "EffectRuntimeUtility.ActivateDurationEffect(em, ref ecb",
                "EffectRuntimeUtility.ApplyInactiveDurationEffect(em, ref ecb",
                "EffectRuntimeUtility.PlaybackAndReset(ref ecb, em)",
                "ecb.SetComponent(ge, duration)");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SEffectApply.cs",
                "EffectRuntimeUtility.DestroyEffectEntity(em, ge)");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SEffectApply.cs",
                "EffectRuntimeUtility.ActivateDurationEffect(em, ge");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SEffectApply.cs",
                "em.SetComponentData(ge, duration)");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SEffectApply.cs",
                "em.AddComponentData(ge, runtime)");

            AssertUsesLocalEcb(
                "Assets/GAS/Runtime/System/Effect/SOngoingTagRequirements.cs",
                "EffectRuntimeUtility.DeactivateOngoingEffect(em, ref ecb",
                "EffectRuntimeUtility.ReactivateOngoingEffect(em, ref ecb");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SOngoingTagRequirements.cs",
                "EffectRuntimeUtility.DeactivateOngoingEffect(em, ge");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SOngoingTagRequirements.cs",
                "EffectRuntimeUtility.ReactivateOngoingEffect(em, ge");

            AssertUsesLocalEcb(
                "Assets/GAS/Runtime/System/Effect/SEffectRemove.cs",
                "EffectRuntimeUtility.CleanupActiveEffect(em, ref ecb",
                "EffectRuntimeUtility.DestroyEffectEntity(em, ref ecb");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SEffectRemove.cs",
                "EffectRuntimeUtility.CleanupActiveEffect(em, ge)");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SEffectRemove.cs",
                "EffectRuntimeUtility.DestroyEffectEntity(em, ge)");

            AssertUsesLocalEcb(
                "Assets/GAS/Runtime/System/Effect/SEffectTick.cs",
                "EffectRuntimeUtility.HandleDurationExpired(em, ref ecb",
                "EffectRuntimeUtility.CreateDerivedApplyRequest(",
                "ecb.AddComponent(ge, new CPeriodRuntime",
                "ecb.SetComponent(ge, runtime)");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SEffectTick.cs",
                "EffectRuntimeUtility.HandleDurationExpired(em, ge");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SEffectTick.cs",
                "em.AddComponentData(ge, new CPeriodRuntime");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/SEffectTick.cs",
                "em.SetComponentData(ge, runtime)");

            AssertUsesLocalEcb(
                "Assets/GAS/Runtime/System/Ability/SAbilityStateCleanup.cs",
                "CleanupAbilityCreatedEffects(em, ref ecb",
                "CleanupGrantedAbilityIfNeeded(em, ref ecb",
                "RemoveComponentIfPresent<CAbilityActive>(em, ref ecb",
                "ecb.SetComponent(ability, runtime)",
                "DestroyAbilityEntity(em, ref ecb");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Ability/SAbilityStateCleanup.cs",
                "em.AddComponent<CEffectDestroy>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Ability/SAbilityStateCleanup.cs",
                "em.AddComponent<CAbilityDestroyOnCleanup>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Ability/SAbilityStateCleanup.cs",
                "em.RemoveComponent<");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Ability/SAbilityStateCleanup.cs",
                "em.DestroyEntity(ability)");

            AssertUsesLocalEcb(
                "Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs",
                "public static void PlaybackAndReset",
                "public static void DestroyEffectEntity(EntityManager em, ref EntityCommandBuffer ecb",
                "private static void SetPeriodStartTime(",
                "private static void AddGrantedAbilities(",
                "private static void CancelOrDestroyAbility(");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs",
                "em.AddComponent<CEffectDestroy>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs",
                "em.DestroyEntity(ge)");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs",
                "em.AddBuffer<BGrantedAbilityRuntime>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs",
                "em.AddComponentData(abilityEntity, new CGrantedByEffect");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs",
                "em.AddComponent<CAbilityInTryActivate>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs",
                "em.AddComponent<CAbilityDestroyOnCleanup>");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs",
                "em.DestroyEntity(abilityEntity)");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs",
                "em.AddComponentData(ge, new CPeriodRuntime");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs",
                "em.SetComponentData(ge, duration)");
            AssertNoSourceToken(
                "Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs",
                "em.SetComponentData(target, tags)");
        }

        [Test]
        public void StructuralContractDoesNotExposeEditorGeneratedOrRuntimeHandles()
        {
            var checkedTypes = new[]
            {
                typeof(GASRuntimeStructuralChangeEntryId),
                typeof(GASRuntimeStructuralOperation),
                typeof(GASRuntimeStructuralMigrationStep),
                typeof(GASRuntimeStructuralEligibility),
                typeof(GASRuntimeStructuralBoundary),
                typeof(GASRuntimeDirtyPipelineSignal),
                typeof(GASRuntimeEnableableScope),
                typeof(GASRuntimeStructuralPlaybackGateId),
                typeof(GASRuntimeStructuralPlaybackRouteStatus),
                typeof(GASRuntimeStructuralPlaybackPolicy),
                typeof(GASRuntimeStructuralPlaybackEvidence),
                typeof(GASRuntimeStructuralChangeEntry),
                typeof(GASRuntimeStructuralChangePlan),
                typeof(GASRuntimeStructuralChangePlanner),
                typeof(GASRuntimeStructuralPlaybackGateContract),
                typeof(GASRuntimeStructuralPlaybackRouteEntry),
                typeof(GASRuntimeStructuralPlaybackGatePlan),
                typeof(GASRuntimeStructuralPlaybackGatePlanner),
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

        private static GASRuntimeCoreFramePhaseContract GetPhaseContract(EGasRuntimeCoreFramePhase phase)
        {
            var phases = GASSystemScheduleContract.RuntimeCoreFramePhases;
            for (var i = 0; i < phases.Count; i++)
            {
                if (phases[i].Phase == phase)
                    return phases[i];
            }

            Assert.Fail("Missing phase contract " + phase);
            return default;
        }

        private static bool HasStructuralPlaybackOperation(GASRuntimeStructuralChangeEntry entry)
        {
            return entry.HasOperation(GASRuntimeStructuralOperation.CreateEntity)
                   || entry.HasOperation(GASRuntimeStructuralOperation.DestroyEntity)
                   || entry.HasOperation(GASRuntimeStructuralOperation.AddComponent)
                   || entry.HasOperation(GASRuntimeStructuralOperation.RemoveComponent)
                   || entry.HasOperation(GASRuntimeStructuralOperation.AddBuffer);
        }

        private static void AssertDirtyCandidate(
            GASRuntimeStructuralChangePlan plan,
            GASRuntimeStructuralChangeEntryId entryId,
            GASRuntimeDirtyPipelineSignal signal)
        {
            Assert.That(plan.TryFind(entryId, out var entry), Is.True);
            Assert.That(entry.HasMigrationStep(GASRuntimeStructuralMigrationStep.DirtyTrackingCandidate), Is.True);
            Assert.That(entry.HasEligibility(GASRuntimeStructuralEligibility.CanUseDirtyPipeline), Is.True);
            Assert.That(entry.HasEnableableScope(GASRuntimeEnableableScope.DirtyMarker), Is.True);
            Assert.That(entry.HasDirtySignal(signal), Is.True);
        }

        private static bool ContainsSystem(IReadOnlyList<Type> systemTypes, Type expected)
        {
            for (var i = 0; i < systemTypes.Count; i++)
            {
                if (systemTypes[i] == expected)
                    return true;
            }

            return false;
        }

        private static void AssertUsesLocalEcb(
            string relativePath,
            params string[] requiredTokens)
        {
            var source = ReadProjectFile(relativePath);
            Assert.That(source.Contains("new EntityCommandBuffer"), Is.True, relativePath);
            Assert.That(source.Contains(".Playback(em)"), Is.True, relativePath);
            for (var i = 0; i < requiredTokens.Length; i++)
                Assert.That(source.Contains(requiredTokens[i]), Is.True, relativePath + " missing " + requiredTokens[i]);
        }

        private static void AssertNoSourceToken(string relativePath, string forbiddenToken)
        {
            var source = ReadProjectFile(relativePath);
            Assert.That(source.Contains(forbiddenToken), Is.False, relativePath + " still contains " + forbiddenToken);
        }

        private static string ReadProjectFile(string relativePath)
        {
            var root = FindProjectRoot();
            var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.ReadAllText(fullPath);
        }

        private static string FindProjectRoot()
        {
            var current = new DirectoryInfo(Environment.CurrentDirectory);
            while (current != null)
            {
                var runtimePath = Path.Combine(current.FullName, "Assets", "GAS", "Runtime");
                if (Directory.Exists(runtimePath))
                    return current.FullName;

                current = current.Parent;
            }

            Assert.Fail("Could not locate Unity project root from " + Environment.CurrentDirectory);
            return string.Empty;
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
