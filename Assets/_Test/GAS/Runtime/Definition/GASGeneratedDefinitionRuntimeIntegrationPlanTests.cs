using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace GAS.Runtime.Tests.Definition
{
    public sealed class GASGeneratedDefinitionRuntimeIntegrationPlanTests
    {
        private const int AbilityCode = 31001;
        private const int GameplayEffectCode = 31002;
        private const int AttrSetCode = 31003;
        private const int AttributeCode = 31004;
        private const int TagCode = 31005;
        private const int CueCode = 31006;
        private const int TimelineId = 31007;

        [Test]
        public void RuntimeIntegrationPlanConsumesBakeResultLayoutAndStructuralPlans()
        {
            var bakeResult = CreateBakeResultWithFullStaticSurface();
            var layoutPlan = GASRuntimeQueryLayoutPlanner.CreateCurrent();
            var structuralPlan = GASRuntimeStructuralChangePlanner.CreateCurrent(layoutPlan);

            var plan = GASGeneratedDefinitionRuntimeIntegrationPlanner.Create(
                bakeResult,
                layoutPlan,
                structuralPlan);

            Assert.That(plan.SourceBakeResult.BakerInputCount, Is.EqualTo(bakeResult.BakerInputCount));
            Assert.That(plan.SourceLayoutPlan.EntryCount, Is.EqualTo(layoutPlan.EntryCount));
            Assert.That(plan.SourceStructuralPlan.EntryCount, Is.EqualTo(structuralPlan.EntryCount));
            Assert.That(plan.CanRunUnityBaker, Is.True);
            Assert.That(plan.UnityBakerInputEntryCount, Is.EqualTo(bakeResult.BakerInputCount));
            Assert.That(plan.StaticBlobCacheEntryCount, Is.EqualTo(bakeResult.StaticBlobCacheRequestCount));
            Assert.That(plan.RuntimeArchetypeEntryCount, Is.EqualTo(bakeResult.RuntimeArchetypeCount));
            Assert.That(plan.DeferredBoundaryEntryCount, Is.EqualTo(bakeResult.DeferredBoundaryCount));
            Assert.That(
                plan.EntryCount,
                Is.EqualTo(
                    bakeResult.BakerInputCount
                    + bakeResult.StaticBlobCacheRequestCount
                    + bakeResult.RuntimeArchetypeCount
                    + bakeResult.DeferredBoundaryCount));
            Assert.That(plan.CanIntegrateRuntimeStaticArchetypes, Is.True);
            Assert.That(plan.HasRuntimeLifecycleDeferredBoundaries, Is.True);
            Assert.That(plan.StructuralSemanticDecisionCount, Is.GreaterThan(0));
            Assert.That(plan.DirtyPipelineCandidateCount, Is.GreaterThan(0));
            Assert.That(plan.ManagedPresentationBoundaryCount, Is.GreaterThan(0));

            Assert.That(
                plan.TryFindEntry(
                    GASGeneratedDefinitionRuntimeIntegrationTarget.RuntimeStaticArchetype,
                    GASDefinitionKind.Ability,
                    AbilityCode,
                    out var ability),
                Is.True);
            Assert.That(ability.Status, Is.EqualTo(GASGeneratedDefinitionRuntimeIntegrationStatus.ReadyWithDeferredRuntimeBoundary));
            Assert.That(ability.LayoutEntryId, Is.EqualTo(GASRuntimeQueryLayoutEntryId.AbilityTickLifecycle));
            Assert.That(ability.StructuralEntryId, Is.EqualTo(GASRuntimeStructuralChangeEntryId.AbilityLifecycleCleanup));
            Assert.That(
                ability.HasIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary.RuntimeLifecycleDeferred),
                Is.True);
            Assert.That(
                ability.HasIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary.StructuralSemanticDecision),
                Is.True);

            Assert.That(
                plan.TryFindEntry(
                    GASGeneratedDefinitionRuntimeIntegrationTarget.RuntimeStaticArchetype,
                    GASDefinitionKind.GameplayEffect,
                    GameplayEffectCode,
                    out var effect),
                Is.True);
            Assert.That(effect.Status, Is.EqualTo(GASGeneratedDefinitionRuntimeIntegrationStatus.ReadyWithDeferredRuntimeBoundary));
            Assert.That(effect.LayoutEntryId, Is.EqualTo(GASRuntimeQueryLayoutEntryId.GameplayEffectActiveRuntime));
            Assert.That(effect.StructuralEntryId, Is.EqualTo(GASRuntimeStructuralChangeEntryId.GameplayEffectActiveRuntimeMutation));
            Assert.That(effect.HasSlot(GASGeneratedDefinitionArchetypeSlot.StaticDefinitionBlobReferenceSlot), Is.True);
            Assert.That(
                effect.HasIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary.GameplayEffectCacheLifecycleOwner),
                Is.True);
            Assert.That(
                effect.HasIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary.ManagedPresentationDeferred),
                Is.True);

            Assert.That(
                plan.TryFindEntry(
                    GASGeneratedDefinitionRuntimeIntegrationTarget.StaticDefinitionBlobCache,
                    GASDefinitionKind.GameplayEffect,
                    GameplayEffectCode,
                    out var staticBlob),
                Is.True);
            Assert.That(staticBlob.Status, Is.EqualTo(GASGeneratedDefinitionRuntimeIntegrationStatus.ReadyWithDeferredRuntimeBoundary));
            Assert.That(staticBlob.LayoutEntryId, Is.EqualTo(GASRuntimeQueryLayoutEntryId.GameplayEffectActiveRuntime));
            Assert.That(staticBlob.HasSlot(GASGeneratedDefinitionArchetypeSlot.StaticDefinitionBlobReferenceSlot), Is.True);
            Assert.That(
                staticBlob.HasIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary.GameplayEffectCacheLifecycleOwner),
                Is.True);

            Assert.That(
                plan.TryFindEntry(
                    GASGeneratedDefinitionRuntimeIntegrationTarget.RuntimeStaticArchetype,
                    GASDefinitionKind.Attribute,
                    AttributeCode,
                    out var attribute),
                Is.True);
            Assert.That(attribute.Status, Is.EqualTo(GASGeneratedDefinitionRuntimeIntegrationStatus.Ready));
            Assert.That(attribute.LayoutEntryId, Is.EqualTo(GASRuntimeQueryLayoutEntryId.AttributeRecalculate));
            Assert.That(attribute.StructuralEntryId, Is.EqualTo(GASRuntimeStructuralChangeEntryId.AttributeDirtyRecalculate));

            Assert.That(
                plan.TryFindEntry(
                    GASGeneratedDefinitionRuntimeIntegrationTarget.RuntimeStaticArchetype,
                    GASDefinitionKind.GameplayCue,
                    CueCode,
                    out var cue),
                Is.True);
            Assert.That(cue.Status, Is.EqualTo(GASGeneratedDefinitionRuntimeIntegrationStatus.ManagedPresentationBoundary));
            Assert.That(cue.LayoutEntryId, Is.EqualTo(GASRuntimeQueryLayoutEntryId.ManagedCuePresentation));
            Assert.That(
                cue.HasIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary.ManagedPresentation),
                Is.True);
        }

        [Test]
        public void RuntimeIntegrationPlanKeepsBakeErrorsAsBlockedDiagnostics()
        {
            var table = new GASDefinitionTable(
                new[]
                {
                    CreateAbilitySummary(32001, 0),
                },
                Array.Empty<GameplayEffectDefinitionSummary>(),
                Array.Empty<AttributeSetDefinitionSummary>(),
                Array.Empty<AttributeDefinitionSummary>(),
                Array.Empty<GameplayTagDefinitionSummary>(),
                Array.Empty<GameplayCueDefinitionSummary>());
            var buildResult = new GASGeneratedDefinitionBuildResult(
                table,
                ConfigRegistryGraphWarmupResult.Empty,
                new[]
                {
                    new ConfigRegistryDiagnostic(
                        ConfigRegistryDiagnosticSeverity.Error,
                        ConfigRegistryDiagnosticCode.MissingConfig,
                        ConfigRegistryConfigKind.Ability,
                        32001,
                        default,
                        "Ability source error."),
                },
                Array.Empty<GASDefinitionValidationDiagnostic>());
            var bakingPlan = GASGeneratedDefinitionBakingPlanner.Create(buildResult, default);
            var contract = GASGeneratedDefinitionBakeContractPlanner.Create(bakingPlan);
            var bakeResult = GASGeneratedDefinitionBakePipeline.Create(contract);

            var plan = GASGeneratedDefinitionRuntimeIntegrationPlanner.CreateCurrent(bakeResult);

            Assert.That(bakeResult.CanRunUnityBaker, Is.False);
            Assert.That(plan.CanRunUnityBaker, Is.False);
            Assert.That(plan.UnityBakerInputEntryCount, Is.EqualTo(0));
            Assert.That(plan.RuntimeArchetypeEntryCount, Is.EqualTo(0));
            Assert.That(plan.EntryCount, Is.EqualTo(bakeResult.DeferredBoundaryCount));
            Assert.That(plan.BlockedEntryCount, Is.EqualTo(plan.EntryCount));
            Assert.That(plan.CanIntegrateRuntimeStaticArchetypes, Is.False);
            Assert.That(
                plan.CountEntriesWithIntegrationBoundary(
                    GASGeneratedDefinitionRuntimeIntegrationBoundary.BakePipelineNotReady),
                Is.EqualTo(plan.EntryCount));
        }

        [Test]
        public void RuntimeIntegrationContractDoesNotExposeEditorGeneratedOrRuntimeHandles()
        {
            var checkedTypes = new[]
            {
                typeof(GASGeneratedDefinitionRuntimeIntegrationTarget),
                typeof(GASGeneratedDefinitionRuntimeIntegrationStatus),
                typeof(GASGeneratedDefinitionRuntimeIntegrationBoundary),
                typeof(GASGeneratedDefinitionRuntimeIntegrationEntry),
                typeof(GASGeneratedDefinitionRuntimeIntegrationPlan),
                typeof(GASGeneratedDefinitionRuntimeIntegrationPlanner),
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
                "Unity.Entities.Entity",
                "EntityManager",
                "EntityQuery",
                nameof(CEffectSpecData),
                nameof(CEffectContext),
                nameof(CDurationRuntime),
                nameof(CPeriodRuntime),
                nameof(CStackingRuntime),
                nameof(CAbilityRuntimeState),
                nameof(BAttribute),
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

        private static GASGeneratedDefinitionBakeResult CreateBakeResultWithFullStaticSurface()
        {
            var table = new GASDefinitionTable(
                new[]
                {
                    CreateAbilitySummary(AbilityCode, TimelineId),
                },
                new[]
                {
                    new GameplayEffectDefinitionSummary(
                        GameplayEffectCode,
                        true,
                        default,
                        false,
                        default,
                        0,
                        false,
                        default,
                        0,
                        default,
                        default,
                        false,
                        false,
                        false,
                        default,
                        false,
                        default,
                        false,
                        default,
                        false,
                        default,
                        1,
                        0,
                        1,
                        true),
                },
                new[]
                {
                    new AttributeSetDefinitionSummary(AttrSetCode, 1, true),
                },
                new[]
                {
                    new AttributeDefinitionSummary(
                        AttrSetCode,
                        AttributeCode,
                        10f,
                        true,
                        true,
                        0f,
                        100f),
                },
                new[]
                {
                    new GameplayTagDefinitionSummary(TagCode, Array.Empty<int>(), Array.Empty<int>()),
                },
                new[]
                {
                    new GameplayCueDefinitionSummary(
                        CueCode,
                        default,
                        default,
                        true,
                        false,
                        true),
                });
            var buildResult = new GASGeneratedDefinitionBuildResult(
                table,
                new ConfigRegistryGraphWarmupResult(
                    abilityConfigCount: 1,
                    gameplayEffectConfigCount: 1,
                    timelineConfigCount: 1,
                    abilityDiagnosticCount: 0,
                    gameplayEffectDiagnosticCount: 0,
                    timelineDiagnosticCount: 0,
                    newDiagnosticCount: 0,
                    diagnostics: Array.Empty<ConfigRegistryDiagnostic>()),
                Array.Empty<ConfigRegistryDiagnostic>(),
                Array.Empty<GASDefinitionValidationDiagnostic>());
            var cacheState = new GameplayEffectDefinitionCacheState(
                GameplayEffectDefinitionLifecycleOwnerKind.GameplayEffectConfigRegistry,
                hasConfigProvider: true,
                generation: 1,
                cachedPrototypeCount: 0,
                cachedStaticDefinitionBlobCount: 0);
            var bakingPlan = GASGeneratedDefinitionBakingPlanner.Create(buildResult, cacheState);
            var contract = GASGeneratedDefinitionBakeContractPlanner.Create(bakingPlan);
            return GASGeneratedDefinitionBakePipeline.Create(contract);
        }

        private static AbilityDefinitionSummary CreateAbilitySummary(int abilityCode, int timelineId)
        {
            return new AbilityDefinitionSummary(
                abilityCode,
                1,
                default,
                default,
                default,
                default,
                default,
                default,
                0,
                0,
                0,
                0,
                timelineId,
                false,
                false,
                false,
                false,
                false,
                false);
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
