using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.Runtime.Tests.Definition
{
    public sealed class GASDefinitionGeneratedAdapterTests
    {
        [SetUp]
        public void SetUp()
        {
            ConfigRegistryDiagnostics.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            AbilityConfigRegistry.RegisterGetConfigByIDFunc(null);
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
            GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(null);
            TimelineAbilityConfigRegistry.RegisterGetConfigByIDFunc(null);
            ConfigRegistryDiagnostics.Clear();
        }

        [Test]
        public void BuildCreatesDefinitionTableAndValidationGraphFromGeneratedSource()
        {
            const int abilityCode = 11001;
            const int costEffectCode = 11002;
            const int effectCode = 11003;
            const int cueCode = 11004;
            const int timelineId = 11005;
            const int attrSetCode = 11006;
            const int attributeCode = 11007;
            const int tagCode = 11008;

            var abilityConfig = new AbilityConfig(new AbilityComponentConfig[]
            {
                new ConfAbilityBaseInfo { Code = abilityCode, Level = 2 },
                new ConfAbilityCost { GameplayEffectCode = costEffectCode },
                new ConfAbilityTimelineRef { TimelineId = timelineId },
            });
            var emptyEffectConfig = new GameplayEffectConfig(Array.Empty<GameplayEffectComponentConfig>());
            var effectConfig = new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfGrantedAbilityConfig
                {
                    GrantedAbilities = new[]
                    {
                        new GrantedAbilityConfigSetting
                        {
                            AbilityCode = abilityCode,
                            Level = 1,
                        },
                    },
                },
            });
            var cueConfig = new GameplayCueConfig(typeof(CueLog), new XParamString("generated"));
            var timeline = new XParamTimeline(
                timelineId,
                "GeneratedTimeline",
                lifeTime: 10,
                manualEndAbility: false,
                tracks: new List<Track>());
            var attrSetConfig = new AttrSetConfig(
                attrSetCode,
                new[]
                {
                    new AttributeBaseSetting(
                        attributeCode,
                        initValue: 9f,
                        isClampMin: true,
                        isClampMax: true,
                        min: 0f,
                        max: 99f),
                });
            var source = new GASGeneratedDefinitionSource(
                new[] { abilityCode },
                new[] { costEffectCode, effectCode },
                new[] { attrSetConfig },
                new[] { new GameplayTag(tagCode, Array.Empty<int>(), Array.Empty<int>()) },
                new[] { cueCode },
                new[] { timelineId });

            AbilityConfigRegistry.RegisterGetConfigByIDFunc(id => id == abilityCode ? abilityConfig : null);
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == costEffectCode ? emptyEffectConfig :
                id == effectCode ? effectConfig :
                null);
            GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(id => id == cueCode ? cueConfig : null);
            TimelineAbilityConfigRegistry.RegisterGetConfigByIDFunc(id => id == timelineId ? timeline : null);

            var result = GASDefinitionGeneratedAdapter.Build(source);

            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.RegistryDiagnosticCount, Is.EqualTo(0));
            Assert.That(result.ValidationDiagnosticCount, Is.EqualTo(0));
            Assert.That(result.WarmupResult.AbilityConfigCount, Is.EqualTo(1));
            Assert.That(result.WarmupResult.GameplayEffectConfigCount, Is.EqualTo(2));
            Assert.That(result.WarmupResult.TimelineConfigCount, Is.EqualTo(1));
            Assert.That(result.DefinitionTable.TotalDefinitionCount, Is.EqualTo(7));
            Assert.That(result.DefinitionTable.TryGetAbility(abilityCode, out var ability), Is.True);
            Assert.That(ability.CostGameplayEffectCode, Is.EqualTo(costEffectCode));
            Assert.That(ability.TimelineId, Is.EqualTo(timelineId));
            Assert.That(result.DefinitionTable.TryGetGameplayEffect(effectCode, out var effect), Is.True);
            Assert.That(effect.GrantedAbilityCount, Is.EqualTo(1));
            Assert.That(result.DefinitionTable.TryGetAttribute(attrSetCode, attributeCode, out var attribute), Is.True);
            Assert.That(attribute.InitialValue, Is.EqualTo(9f));
            Assert.That(result.DefinitionTable.TryGetGameplayTag(tagCode, out _), Is.True);
            Assert.That(result.DefinitionTable.TryGetGameplayCue(cueCode, out var cue), Is.True);
            Assert.That(cue.UsesManagedPresentationFactory, Is.True);
        }

        [Test]
        public void BuildAggregatesWarmupAndDirectDefinitionDiagnostics()
        {
            const int abilityCode = 12001;
            const int missingCostEffectCode = 12002;
            const int missingDirectEffectCode = 12003;
            const int missingCueCode = 12004;

            var abilityConfig = new AbilityConfig(new AbilityComponentConfig[]
            {
                new ConfAbilityBaseInfo { Code = abilityCode, Level = 1 },
                new ConfAbilityCost { GameplayEffectCode = missingCostEffectCode },
            });
            var source = new GASGeneratedDefinitionSource(
                new[] { abilityCode },
                new[] { missingDirectEffectCode },
                Array.Empty<AttrSetConfig>(),
                Array.Empty<GameplayTag>(),
                new[] { missingCueCode },
                Array.Empty<int>());

            AbilityConfigRegistry.RegisterGetConfigByIDFunc(id => id == abilityCode ? abilityConfig : null);
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(_ => null);
            GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(_ => null);

            var result = GASDefinitionGeneratedAdapter.Build(source);

            Assert.That(result.ValidationDiagnosticCount, Is.EqualTo(0));
            Assert.That(result.WarmupResult.NewDiagnosticCount, Is.EqualTo(2));
            Assert.That(result.RegistryDiagnosticCount, Is.EqualTo(3));
            Assert.That(result.DefinitionTable.TotalDefinitionCount, Is.EqualTo(1));
            AssertHasMissingConfigDiagnostic(
                result.RegistryDiagnostics,
                ConfigRegistryConfigKind.GameplayEffect,
                missingCostEffectCode,
                ConfigRegistryConfigKind.Ability,
                abilityCode,
                ConfigRegistryReferenceKind.AbilityCost);
            AssertHasMissingConfigDiagnostic(
                result.RegistryDiagnostics,
                ConfigRegistryConfigKind.GameplayEffect,
                missingDirectEffectCode,
                ConfigRegistryConfigKind.None,
                0,
                ConfigRegistryReferenceKind.Direct);
            AssertHasMissingConfigDiagnostic(
                result.RegistryDiagnostics,
                ConfigRegistryConfigKind.GameplayCue,
                missingCueCode,
                ConfigRegistryConfigKind.None,
                0,
                ConfigRegistryReferenceKind.Direct);
        }

        [Test]
        public void BuildReportsInvalidAndDuplicateGeneratedDefinitionCodes()
        {
            const int abilityCode = 13001;
            const int effectCode = 13002;
            const int cueCode = 13003;
            const int timelineId = 13004;
            const int attrSetCode = 13005;
            const int attributeCode = 13006;
            const int tagCode = 13007;

            var abilityConfig = new AbilityConfig(new AbilityComponentConfig[]
            {
                new ConfAbilityBaseInfo { Code = abilityCode, Level = 1 },
            });
            var effectConfig = new GameplayEffectConfig(Array.Empty<GameplayEffectComponentConfig>());
            var cueConfig = new GameplayCueConfig(typeof(CueLog), new XParamString("duplicate"));
            var timeline = new XParamTimeline(
                timelineId,
                "DuplicateTimeline",
                lifeTime: 1,
                manualEndAbility: false,
                tracks: new List<Track>());
            var source = new GASGeneratedDefinitionSource(
                new[] { abilityCode, abilityCode, 0 },
                new[] { effectCode },
                new[]
                {
                    new AttrSetConfig(
                        attrSetCode,
                        new[]
                        {
                            new AttributeBaseSetting(attributeCode, 1f, false, false, 0f, 0f),
                            new AttributeBaseSetting(attributeCode, 2f, false, false, 0f, 0f),
                            new AttributeBaseSetting(0, 0f, false, false, 0f, 0f),
                        }),
                    new AttrSetConfig(attrSetCode, Array.Empty<AttributeBaseSetting>()),
                },
                new[]
                {
                    new GameplayTag(tagCode, Array.Empty<int>(), Array.Empty<int>()),
                    new GameplayTag(tagCode, Array.Empty<int>(), Array.Empty<int>()),
                    new GameplayTag(0, Array.Empty<int>(), Array.Empty<int>()),
                },
                new[] { cueCode, cueCode },
                new[] { timelineId });

            AbilityConfigRegistry.RegisterGetConfigByIDFunc(id => id == abilityCode ? abilityConfig : null);
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id => id == effectCode ? effectConfig : null);
            GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(id => id == cueCode ? cueConfig : null);
            TimelineAbilityConfigRegistry.RegisterGetConfigByIDFunc(id => id == timelineId ? timeline : null);

            var result = GASDefinitionGeneratedAdapter.Build(source);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.RegistryDiagnosticCount, Is.EqualTo(0));
            Assert.That(result.ValidationDiagnosticCount, Is.EqualTo(8));
            Assert.That(result.DefinitionTable.TryGetAbility(abilityCode, out _), Is.True);
            Assert.That(result.DefinitionTable.TryGetGameplayCue(cueCode, out _), Is.True);
            AssertHasValidationDiagnostic(
                result.ValidationDiagnostics,
                GASDefinitionValidationCode.DuplicateDefinitionCode,
                GASDefinitionKind.Ability,
                abilityCode,
                GASDefinitionKind.None,
                0,
                2);
            AssertHasValidationDiagnostic(
                result.ValidationDiagnostics,
                GASDefinitionValidationCode.InvalidDefinitionCode,
                GASDefinitionKind.Ability,
                0,
                GASDefinitionKind.None,
                0,
                1);
            AssertHasValidationDiagnostic(
                result.ValidationDiagnostics,
                GASDefinitionValidationCode.DuplicateDefinitionCode,
                GASDefinitionKind.GameplayCue,
                cueCode,
                GASDefinitionKind.None,
                0,
                2);
            AssertHasValidationDiagnostic(
                result.ValidationDiagnostics,
                GASDefinitionValidationCode.DuplicateDefinitionCode,
                GASDefinitionKind.AttributeSet,
                attrSetCode,
                GASDefinitionKind.None,
                0,
                2);
            AssertHasValidationDiagnostic(
                result.ValidationDiagnostics,
                GASDefinitionValidationCode.DuplicateDefinitionCode,
                GASDefinitionKind.Attribute,
                attributeCode,
                GASDefinitionKind.AttributeSet,
                attrSetCode,
                2);
            AssertHasValidationDiagnostic(
                result.ValidationDiagnostics,
                GASDefinitionValidationCode.InvalidDefinitionCode,
                GASDefinitionKind.Attribute,
                0,
                GASDefinitionKind.AttributeSet,
                attrSetCode,
                1);
            AssertHasValidationDiagnostic(
                result.ValidationDiagnostics,
                GASDefinitionValidationCode.DuplicateDefinitionCode,
                GASDefinitionKind.GameplayTag,
                tagCode,
                GASDefinitionKind.None,
                0,
                2);
            AssertHasValidationDiagnostic(
                result.ValidationDiagnostics,
                GASDefinitionValidationCode.InvalidDefinitionCode,
                GASDefinitionKind.GameplayTag,
                0,
                GASDefinitionKind.None,
                0,
                1);
        }

        [Test]
        public void GeneratedAdapterContractDoesNotOwnGeneratedOrRuntimeStateTypes()
        {
            var checkedTypes = new[]
            {
                typeof(GASGeneratedDefinitionSource),
                typeof(GASGeneratedDefinitionBuildResult),
                typeof(GASDefinitionValidationDiagnostic),
                typeof(GASDefinitionGeneratedAdapter),
            };
            var forbiddenTypeNames = new[]
            {
                "cfg.",
                "SimpleJSON",
                "XLuban",
                "Unity.Entities.Entity",
                nameof(CEffectSpecData),
                nameof(CEffectContext),
                nameof(CAbilityRuntimeState),
                nameof(BAttribute),
            };

            for (var typeIndex = 0; typeIndex < checkedTypes.Length; typeIndex++)
            {
                var fields = checkedTypes[typeIndex].GetFields(
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (var fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    var fieldTypeName = fields[fieldIndex].FieldType.FullName ?? fields[fieldIndex].FieldType.Name;
                    for (var forbiddenIndex = 0; forbiddenIndex < forbiddenTypeNames.Length; forbiddenIndex++)
                    {
                        Assert.That(
                            fieldTypeName,
                            Does.Not.Contain(forbiddenTypeNames[forbiddenIndex]),
                            $"{checkedTypes[typeIndex].Name}.{fields[fieldIndex].Name} must not own generated-specific or runtime state type {forbiddenTypeNames[forbiddenIndex]}.");
                    }
                }
            }
        }

        [Test]
        public void BakingPlanDerivesCarrierBlobAndDeferredBoundariesFromDefinitionPlane()
        {
            const int abilityCode = 21001;
            const int gameplayEffectCode = 21002;
            const int attrSetCode = 21003;
            const int firstAttributeCode = 21004;
            const int secondAttributeCode = 21005;
            const int tagCode = 21006;
            const int cueCode = 21007;
            const int timelineId = 21008;

            var table = new GASDefinitionTable(
                new[]
                {
                    new AbilityDefinitionSummary(
                        abilityCode,
                        1,
                        default,
                        default,
                        default,
                        default,
                        default,
                        default,
                        gameplayEffectCode,
                        0,
                        0,
                        1,
                        timelineId,
                        false,
                        false,
                        false,
                        false,
                        false,
                        false),
                },
                new[]
                {
                    new GameplayEffectDefinitionSummary(
                        gameplayEffectCode,
                        true,
                        default,
                        true,
                        default,
                        2,
                        false,
                        default,
                        1,
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
                        2,
                        1,
                        1,
                        true),
                },
                new[]
                {
                    new AttributeSetDefinitionSummary(attrSetCode, 2, true),
                },
                new[]
                {
                    new AttributeDefinitionSummary(attrSetCode, firstAttributeCode, 10f, true, false, 0f, 0f),
                    new AttributeDefinitionSummary(attrSetCode, secondAttributeCode, 20f, false, false, 0f, 0f),
                },
                new[]
                {
                    new GameplayTagDefinitionSummary(tagCode, new[] { tagCode - 1 }, new[] { tagCode + 1 }),
                },
                new[]
                {
                    new GameplayCueDefinitionSummary(
                        cueCode,
                        default,
                        default,
                        false,
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
                generation: 7,
                cachedPrototypeCount: 1,
                cachedStaticDefinitionBlobCount: 2);

            var plan = GASGeneratedDefinitionBakingPlanner.Create(buildResult, cacheState);

            Assert.That(plan.CanBake, Is.True);
            Assert.That(plan.EntryCount, Is.EqualTo(7));
            Assert.That(plan.GeneratedCarrierCandidateCount, Is.EqualTo(7));
            Assert.That(plan.BakerInputCandidateCount, Is.EqualTo(7));
            Assert.That(plan.EligibleBakerInputCount, Is.EqualTo(7));
            Assert.That(plan.StaticDefinitionBlobCandidateCount, Is.EqualTo(1));
            Assert.That(plan.TimelineDefinitionCount, Is.EqualTo(1));
            Assert.That(plan.AbilityTimelineReferenceCount, Is.EqualTo(1));
            Assert.That(plan.RuntimeLifecycleDeferredEntryCount, Is.EqualTo(2));
            Assert.That(plan.RuntimeTimelineDeferredCount, Is.EqualTo(2));
            Assert.That(plan.ManagedPresentationDeferredEntryCount, Is.EqualTo(2));
            Assert.That(plan.GameplayEffectLifecycleOwnerKind,
                Is.EqualTo(GameplayEffectDefinitionLifecycleOwnerKind.GameplayEffectConfigRegistry));
            Assert.That(plan.HasGameplayEffectConfigProvider, Is.True);
            Assert.That(plan.GameplayEffectCacheGeneration, Is.EqualTo(7));
            Assert.That(plan.CachedGameplayEffectPrototypeCount, Is.EqualTo(1));
            Assert.That(plan.CachedGameplayEffectStaticDefinitionBlobCount, Is.EqualTo(2));
            Assert.That(plan.HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary.RuntimeLifecycle), Is.True);
            Assert.That(plan.HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary.RuntimeTimeline), Is.True);
            Assert.That(plan.HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary.ManagedPresentation), Is.True);
            Assert.That(plan.HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary.GameplayEffectCacheLifecycleOwner), Is.True);
            Assert.That(TryFindBakingEntry(plan, GASDefinitionKind.GameplayEffect, gameplayEffectCode, out var effectEntry),
                Is.True);
            Assert.That(effectEntry.HasCapability(GASGeneratedDefinitionBakingCapability.StaticDefinitionBlob), Is.True);
            Assert.That(effectEntry.HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary.ManagedPresentation), Is.True);
            Assert.That(effectEntry.HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary.GameplayEffectCacheLifecycleOwner), Is.True);
            Assert.That(TryFindBakingEntry(plan, GASDefinitionKind.GameplayCue, cueCode, out var cueEntry), Is.True);
            Assert.That(cueEntry.HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary.ManagedPresentation), Is.True);
        }

        [Test]
        public void BakingPlanBlocksBakerEligibilityOnRegistryOrValidationErrors()
        {
            const int abilityCode = 22001;
            var table = new GASDefinitionTable(
                new[]
                {
                    new AbilityDefinitionSummary(
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
                        0,
                        false,
                        false,
                        false,
                        false,
                        false,
                        false),
                },
                Array.Empty<GameplayEffectDefinitionSummary>(),
                Array.Empty<AttributeSetDefinitionSummary>(),
                Array.Empty<AttributeDefinitionSummary>(),
                Array.Empty<GameplayTagDefinitionSummary>(),
                Array.Empty<GameplayCueDefinitionSummary>());
            var registryDiagnostics = new[]
            {
                new ConfigRegistryDiagnostic(
                    ConfigRegistryDiagnosticSeverity.Error,
                    ConfigRegistryDiagnosticCode.MissingConfig,
                    ConfigRegistryConfigKind.GameplayEffect,
                    22002,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.Ability,
                        abilityCode,
                        ConfigRegistryReferenceKind.AbilityCost),
                    "Missing cost effect."),
            };
            var validationDiagnostics = new[]
            {
                new GASDefinitionValidationDiagnostic(
                    GASDefinitionValidationSeverity.Error,
                    GASDefinitionValidationCode.InvalidDefinitionCode,
                    GASDefinitionKind.GameplayCue,
                    0,
                    GASDefinitionKind.None,
                    0,
                    1,
                    "Invalid cue code."),
            };
            var buildResult = new GASGeneratedDefinitionBuildResult(
                table,
                ConfigRegistryGraphWarmupResult.Empty,
                registryDiagnostics,
                validationDiagnostics);

            var plan = GASGeneratedDefinitionBakingPlanner.Create(
                buildResult,
                default);

            Assert.That(plan.HasBlockingErrors, Is.True);
            Assert.That(plan.CanBake, Is.False);
            Assert.That(plan.HasBlockingBoundary(GASGeneratedDefinitionBakingBoundary.SourceHasErrors), Is.True);
            Assert.That(plan.RegistryDiagnosticCount, Is.EqualTo(1));
            Assert.That(plan.ValidationDiagnosticCount, Is.EqualTo(1));
            Assert.That(plan.TotalDiagnosticCount, Is.EqualTo(2));
            Assert.That(plan.GeneratedCarrierCandidateCount, Is.EqualTo(1));
            Assert.That(plan.BakerInputCandidateCount, Is.EqualTo(1));
            Assert.That(plan.EligibleBakerInputCount, Is.EqualTo(0));
        }

        [Test]
        public void BakeContractMapsPlanToBakerCacheAndArchetypeBoundaries()
        {
            const int abilityCode = 23001;
            const int gameplayEffectCode = 23002;
            const int cueCode = 23003;
            const int timelineId = 23004;
            var table = new GASDefinitionTable(
                new[]
                {
                    new AbilityDefinitionSummary(
                        abilityCode,
                        1,
                        default,
                        default,
                        default,
                        default,
                        default,
                        default,
                        gameplayEffectCode,
                        0,
                        0,
                        1,
                        timelineId,
                        false,
                        false,
                        false,
                        false,
                        false,
                        false),
                },
                new[]
                {
                    new GameplayEffectDefinitionSummary(
                        gameplayEffectCode,
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
                        true,
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
                Array.Empty<AttributeSetDefinitionSummary>(),
                Array.Empty<AttributeDefinitionSummary>(),
                Array.Empty<GameplayTagDefinitionSummary>(),
                new[]
                {
                    new GameplayCueDefinitionSummary(
                        cueCode,
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
                generation: 3,
                cachedPrototypeCount: 1,
                cachedStaticDefinitionBlobCount: 1);
            var plan = GASGeneratedDefinitionBakingPlanner.Create(buildResult, cacheState);

            var contract = GASGeneratedDefinitionBakeContractPlanner.Create(plan);

            Assert.That(contract.CanRunUnityBaker, Is.True);
            Assert.That(contract.GeneratedCarrierWriteCount, Is.EqualTo(plan.GeneratedCarrierCandidateCount));
            Assert.That(contract.UnityBakerInputWriteCount, Is.EqualTo(plan.BakerInputCandidateCount));
            Assert.That(contract.EligibleUnityBakerInputWriteCount, Is.EqualTo(plan.EligibleBakerInputCount));
            Assert.That(contract.ArchetypeTemplateCount, Is.EqualTo(plan.BakerInputCandidateCount));
            Assert.That(contract.EligibleArchetypeTemplateCount, Is.EqualTo(plan.EligibleBakerInputCount));
            Assert.That(contract.StaticDefinitionBlobCacheWriteCount, Is.EqualTo(1));
            Assert.That(contract.EligibleStaticDefinitionBlobCacheWriteCount, Is.EqualTo(1));
            Assert.That(contract.DeferredBoundaryWriteCount, Is.GreaterThanOrEqualTo(6));
            Assert.That(TryFindBakeWrite(
                    contract,
                    GASDefinitionKind.Ability,
                    abilityCode,
                    GASGeneratedDefinitionBakeWriteKind.UnityBakerInput,
                    out var abilityBakerWrite),
                Is.True);
            Assert.That(abilityBakerWrite.WritePhase, Is.EqualTo(GASGeneratedDefinitionBakeWritePhase.UnityEntitiesBaker));
            Assert.That(abilityBakerWrite.WriteTarget, Is.EqualTo(GASGeneratedDefinitionBakeWriteTarget.UnityEntitiesBakerInput));
            Assert.That(abilityBakerWrite.IsEligible, Is.True);
            Assert.That(TryFindBakeWrite(
                    contract,
                    GASDefinitionKind.GameplayEffect,
                    gameplayEffectCode,
                    GASGeneratedDefinitionBakeWriteKind.StaticDefinitionBlobCache,
                    out var staticBlobWrite),
                Is.True);
            Assert.That(staticBlobWrite.WritePhase, Is.EqualTo(GASGeneratedDefinitionBakeWritePhase.RuntimeDefinitionCache));
            Assert.That(staticBlobWrite.WriteTarget,
                Is.EqualTo(GASGeneratedDefinitionBakeWriteTarget.GameplayEffectStaticDefinitionBlobCache));
            Assert.That(staticBlobWrite.IsEligible, Is.True);
            Assert.That(TryFindArchetypeTemplate(
                    contract,
                    GASDefinitionKind.Ability,
                    abilityCode,
                    out var abilityTemplate),
                Is.True);
            Assert.That(abilityTemplate.TemplateKind,
                Is.EqualTo(GASGeneratedDefinitionArchetypeTemplateKind.AbilityDefinition));
            Assert.That(abilityTemplate.HasSlot(GASGeneratedDefinitionArchetypeSlot.DefinitionKey), Is.True);
            Assert.That(abilityTemplate.HasSlot(GASGeneratedDefinitionArchetypeSlot.TimelineReferenceKey), Is.True);
            Assert.That(abilityTemplate.HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary.RuntimeLifecycle), Is.True);
            Assert.That(TryFindArchetypeTemplate(
                    contract,
                    GASDefinitionKind.GameplayEffect,
                    gameplayEffectCode,
                    out var effectTemplate),
                Is.True);
            Assert.That(effectTemplate.HasSlot(GASGeneratedDefinitionArchetypeSlot.StaticDefinitionBlobReferenceSlot),
                Is.True);
            Assert.That(effectTemplate.HasSlot(GASGeneratedDefinitionArchetypeSlot.PresentationCueKey), Is.True);
            Assert.That(effectTemplate.HasDeferredBoundary(
                    GASGeneratedDefinitionBakingBoundary.GameplayEffectCacheLifecycleOwner),
                Is.True);
        }

        [Test]
        public void BakeContractKeepsBakerAndArchetypeIneligibleWhenPlanHasErrors()
        {
            const int abilityCode = 24001;
            var table = new GASDefinitionTable(
                new[]
                {
                    new AbilityDefinitionSummary(
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
                        0,
                        false,
                        false,
                        false,
                        false,
                        false,
                        false),
                },
                Array.Empty<GameplayEffectDefinitionSummary>(),
                Array.Empty<AttributeSetDefinitionSummary>(),
                Array.Empty<AttributeDefinitionSummary>(),
                Array.Empty<GameplayTagDefinitionSummary>(),
                Array.Empty<GameplayCueDefinitionSummary>());
            var registryDiagnostics = new[]
            {
                new ConfigRegistryDiagnostic(
                    ConfigRegistryDiagnosticSeverity.Error,
                    ConfigRegistryDiagnosticCode.MissingConfig,
                    ConfigRegistryConfigKind.Ability,
                    abilityCode,
                    default,
                    "Ability source error."),
            };
            var buildResult = new GASGeneratedDefinitionBuildResult(
                table,
                ConfigRegistryGraphWarmupResult.Empty,
                registryDiagnostics,
                Array.Empty<GASDefinitionValidationDiagnostic>());
            var plan = GASGeneratedDefinitionBakingPlanner.Create(buildResult, default);

            var contract = GASGeneratedDefinitionBakeContractPlanner.Create(plan);

            Assert.That(plan.CanBake, Is.False);
            Assert.That(contract.CanRunUnityBaker, Is.False);
            Assert.That(contract.GeneratedCarrierWriteCount, Is.EqualTo(1));
            Assert.That(contract.UnityBakerInputWriteCount, Is.EqualTo(1));
            Assert.That(contract.EligibleUnityBakerInputWriteCount, Is.EqualTo(0));
            Assert.That(contract.ArchetypeTemplateCount, Is.EqualTo(1));
            Assert.That(contract.EligibleArchetypeTemplateCount, Is.EqualTo(0));
            Assert.That(TryFindBakeWrite(
                    contract,
                    GASDefinitionKind.Ability,
                    abilityCode,
                    GASGeneratedDefinitionBakeWriteKind.UnityBakerInput,
                    out var bakerWrite),
                Is.True);
            Assert.That(bakerWrite.IsEligible, Is.False);
            Assert.That(TryFindArchetypeTemplate(
                    contract,
                    GASDefinitionKind.Ability,
                    abilityCode,
                    out var template),
                Is.True);
            Assert.That(template.IsEligible, Is.False);
        }

        [Test]
        public void BakeContractRoutesMissingGameplayEffectProviderToCacheOwnerBoundary()
        {
            const int gameplayEffectCode = 25001;
            var table = new GASDefinitionTable(
                Array.Empty<AbilityDefinitionSummary>(),
                new[]
                {
                    new GameplayEffectDefinitionSummary(
                        gameplayEffectCode,
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
                        0,
                        0,
                        0,
                        false),
                },
                Array.Empty<AttributeSetDefinitionSummary>(),
                Array.Empty<AttributeDefinitionSummary>(),
                Array.Empty<GameplayTagDefinitionSummary>(),
                Array.Empty<GameplayCueDefinitionSummary>());
            var buildResult = new GASGeneratedDefinitionBuildResult(
                table,
                ConfigRegistryGraphWarmupResult.Empty,
                Array.Empty<ConfigRegistryDiagnostic>(),
                Array.Empty<GASDefinitionValidationDiagnostic>());
            var plan = GASGeneratedDefinitionBakingPlanner.Create(buildResult, default);

            var contract = GASGeneratedDefinitionBakeContractPlanner.Create(plan);

            Assert.That(plan.CanBake, Is.True);
            Assert.That(plan.HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary.MissingGameplayEffectConfigProvider),
                Is.True);
            Assert.That(contract.CanRunUnityBaker, Is.True);
            Assert.That(contract.StaticDefinitionBlobCacheWriteCount, Is.EqualTo(1));
            Assert.That(contract.EligibleStaticDefinitionBlobCacheWriteCount, Is.EqualTo(0));
            Assert.That(TryFindBakeWrite(
                    contract,
                    GASDefinitionKind.GameplayEffect,
                    gameplayEffectCode,
                    GASGeneratedDefinitionBakeWriteKind.StaticDefinitionBlobCache,
                    out var staticBlobWrite),
                Is.True);
            Assert.That(staticBlobWrite.IsEligible, Is.False);
            Assert.That(TryFindBakeWrite(
                    contract,
                    GASDefinitionKind.None,
                    0,
                    GASGeneratedDefinitionBakeWriteKind.DeferredBoundary,
                    GASGeneratedDefinitionBakingBoundary.MissingGameplayEffectConfigProvider,
                    out var missingProviderWrite),
                Is.True);
            Assert.That(missingProviderWrite.WriteTarget,
                Is.EqualTo(GASGeneratedDefinitionBakeWriteTarget.GameplayEffectCacheLifecycleOwner));
        }

        [Test]
        public void BakePipelineMaterializesEligibleCarrierBakerCacheAndArchetypeArtifacts()
        {
            const int abilityCode = 26001;
            const int gameplayEffectCode = 26002;
            const int cueCode = 26003;
            const int timelineId = 26004;
            var table = new GASDefinitionTable(
                new[]
                {
                    new AbilityDefinitionSummary(
                        abilityCode,
                        1,
                        default,
                        default,
                        default,
                        default,
                        default,
                        default,
                        gameplayEffectCode,
                        0,
                        0,
                        1,
                        timelineId,
                        false,
                        false,
                        false,
                        false,
                        false,
                        false),
                },
                new[]
                {
                    new GameplayEffectDefinitionSummary(
                        gameplayEffectCode,
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
                        true,
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
                Array.Empty<AttributeSetDefinitionSummary>(),
                Array.Empty<AttributeDefinitionSummary>(),
                Array.Empty<GameplayTagDefinitionSummary>(),
                new[]
                {
                    new GameplayCueDefinitionSummary(
                        cueCode,
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
            var plan = GASGeneratedDefinitionBakingPlanner.Create(buildResult, cacheState);
            var contract = GASGeneratedDefinitionBakeContractPlanner.Create(plan);

            var result = GASGeneratedDefinitionBakePipeline.Create(contract);

            Assert.That(result.CanRunUnityBaker, Is.True);
            Assert.That(result.CandidateWriteCount, Is.EqualTo(contract.WriteCount));
            Assert.That(result.CarrierCount, Is.EqualTo(plan.GeneratedCarrierCandidateCount));
            Assert.That(result.BakerInputCount, Is.EqualTo(plan.EligibleBakerInputCount));
            Assert.That(result.StaticBlobCacheRequestCount, Is.EqualTo(1));
            Assert.That(result.RuntimeArchetypeCount, Is.EqualTo(plan.EligibleBakerInputCount));
            Assert.That(result.DeferredBoundaryCount, Is.EqualTo(contract.DeferredBoundaryWriteCount));
            Assert.That(result.HasSkippedMaterialization, Is.False);
            Assert.That(TryFindCarrierArtifact(result, GASDefinitionKind.Ability, abilityCode, out var carrier),
                Is.True);
            Assert.That(carrier.SourceTarget, Is.EqualTo(GASGeneratedDefinitionBakeWriteTarget.GeneratedDefinitionCarrier));
            Assert.That(TryFindBakerInputArtifact(result, GASDefinitionKind.Ability, abilityCode, out var bakerInput),
                Is.True);
            Assert.That(bakerInput.SourceTarget, Is.EqualTo(GASGeneratedDefinitionBakeWriteTarget.UnityEntitiesBakerInput));
            Assert.That(TryFindStaticBlobCacheRequest(result, gameplayEffectCode, out var staticBlobRequest),
                Is.True);
            Assert.That(staticBlobRequest.SourceTarget,
                Is.EqualTo(GASGeneratedDefinitionBakeWriteTarget.GameplayEffectStaticDefinitionBlobCache));
            Assert.That(TryFindRuntimeArchetypeArtifact(
                    result,
                    GASDefinitionKind.GameplayEffect,
                    gameplayEffectCode,
                    out var effectArchetype),
                Is.True);
            Assert.That(effectArchetype.HasSlot(GASGeneratedDefinitionArchetypeSlot.StaticDefinitionBlobReferenceSlot),
                Is.True);
            Assert.That(effectArchetype.HasSlot(GASGeneratedDefinitionArchetypeSlot.PresentationCueKey), Is.True);
            Assert.That(effectArchetype.HasDeferredBoundary(
                    GASGeneratedDefinitionBakingBoundary.GameplayEffectCacheLifecycleOwner),
                Is.True);
        }

        [Test]
        public void BakePipelineSkipsMaterializationWhenContractHasErrors()
        {
            const int abilityCode = 27001;
            var table = new GASDefinitionTable(
                new[]
                {
                    new AbilityDefinitionSummary(
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
                        0,
                        false,
                        false,
                        false,
                        false,
                        false,
                        false),
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
                        abilityCode,
                        default,
                        "Ability source error."),
                },
                Array.Empty<GASDefinitionValidationDiagnostic>());
            var plan = GASGeneratedDefinitionBakingPlanner.Create(buildResult, default);
            var contract = GASGeneratedDefinitionBakeContractPlanner.Create(plan);

            var result = GASGeneratedDefinitionBakePipeline.Create(contract);

            Assert.That(result.CanRunUnityBaker, Is.False);
            Assert.That(result.CarrierCount, Is.EqualTo(0));
            Assert.That(result.BakerInputCount, Is.EqualTo(0));
            Assert.That(result.StaticBlobCacheRequestCount, Is.EqualTo(0));
            Assert.That(result.RuntimeArchetypeCount, Is.EqualTo(0));
            Assert.That(result.DeferredBoundaryCount, Is.EqualTo(1));
            Assert.That(result.SkippedIneligibleWriteCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(result.SkippedIneligibleArchetypeTemplateCount, Is.EqualTo(1));
            Assert.That(result.HasSkippedMaterialization, Is.True);
        }

        [Test]
        public void BakePipelineWarmsGameplayEffectStaticBlobCacheThroughRegistryOwner()
        {
            const int gameplayEffectCode = 28001;
            var world = new World(nameof(BakePipelineWarmsGameplayEffectStaticBlobCacheThroughRegistryOwner));
            var entityManager = world.EntityManager;

            try
            {
                var effectConfig = new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                {
                    new ConfDuration
                    {
                        duration = 6,
                        timeUnit = TimeUnit.Turn,
                    },
                });
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                    id == gameplayEffectCode ? effectConfig : null);
                var table = new GASDefinitionTable(
                    Array.Empty<AbilityDefinitionSummary>(),
                    new[]
                    {
                        GASDefinitionSummaryBuilder.FromGameplayEffectConfig(gameplayEffectCode, effectConfig),
                    },
                    Array.Empty<AttributeSetDefinitionSummary>(),
                    Array.Empty<AttributeDefinitionSummary>(),
                    Array.Empty<GameplayTagDefinitionSummary>(),
                    Array.Empty<GameplayCueDefinitionSummary>());
                var buildResult = new GASGeneratedDefinitionBuildResult(
                    table,
                    ConfigRegistryGraphWarmupResult.Empty,
                    Array.Empty<ConfigRegistryDiagnostic>(),
                    Array.Empty<GASDefinitionValidationDiagnostic>());
                var plan = GASGeneratedDefinitionBakingPlanner.Create(
                    buildResult,
                    GameplayEffectConfigRegistry.GetDefinitionCacheState());
                var contract = GASGeneratedDefinitionBakeContractPlanner.Create(plan);
                var result = GASGeneratedDefinitionBakePipeline.Create(contract);

                var warmup = GASGeneratedDefinitionBakePipeline.WarmupStaticDefinitionBlobCache(
                    entityManager,
                    result);

                Assert.That(result.StaticBlobCacheRequestCount, Is.EqualTo(1));
                Assert.That(warmup.RequestCount, Is.EqualTo(1));
                Assert.That(warmup.MaterializedCount, Is.EqualTo(1));
                Assert.That(warmup.FailedCount, Is.EqualTo(0));
                Assert.That(warmup.SkippedCount, Is.EqualTo(0));
                Assert.That(warmup.AllRequestsMaterialized, Is.True);
                Assert.That(GameplayEffectConfigRegistry.CachedPrototypeCount, Is.EqualTo(0));
                Assert.That(GameplayEffectConfigRegistry.CachedStaticDefinitionBlobCount, Is.EqualTo(1));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(
                        gameplayEffectCode,
                        out var blob),
                    Is.True);
                Assert.That(blob.Value.GameplayEffectCode, Is.EqualTo(gameplayEffectCode));
                Assert.That(blob.Value.HasDuration, Is.True);
                Assert.That(blob.Value.Duration.Duration, Is.EqualTo(6));
                Assert.That(blob.Value.Duration.TimeUnit, Is.EqualTo(TimeUnit.Turn));
            }
            finally
            {
                world.Dispose();
            }
        }

        [Test]
        public void BakePipelineDoesNotWarmStaticBlobCacheWhenProviderIsMissing()
        {
            const int gameplayEffectCode = 29001;
            var world = new World(nameof(BakePipelineDoesNotWarmStaticBlobCacheWhenProviderIsMissing));
            var entityManager = world.EntityManager;

            try
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
                var table = new GASDefinitionTable(
                    Array.Empty<AbilityDefinitionSummary>(),
                    new[]
                    {
                        new GameplayEffectDefinitionSummary(
                            gameplayEffectCode,
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
                            0,
                            0,
                            0,
                            false),
                    },
                    Array.Empty<AttributeSetDefinitionSummary>(),
                    Array.Empty<AttributeDefinitionSummary>(),
                    Array.Empty<GameplayTagDefinitionSummary>(),
                    Array.Empty<GameplayCueDefinitionSummary>());
                var buildResult = new GASGeneratedDefinitionBuildResult(
                    table,
                    ConfigRegistryGraphWarmupResult.Empty,
                    Array.Empty<ConfigRegistryDiagnostic>(),
                    Array.Empty<GASDefinitionValidationDiagnostic>());
                var plan = GASGeneratedDefinitionBakingPlanner.Create(
                    buildResult,
                    GameplayEffectConfigRegistry.GetDefinitionCacheState());
                var contract = GASGeneratedDefinitionBakeContractPlanner.Create(plan);
                var result = GASGeneratedDefinitionBakePipeline.Create(contract);

                var warmup = GASGeneratedDefinitionBakePipeline.WarmupStaticDefinitionBlobCache(
                    entityManager,
                    result);

                Assert.That(result.StaticBlobCacheRequestCount, Is.EqualTo(0));
                Assert.That(warmup.RequestCount, Is.EqualTo(0));
                Assert.That(warmup.MaterializedCount, Is.EqualTo(0));
                Assert.That(warmup.FailedCount, Is.EqualTo(0));
                Assert.That(warmup.AllRequestsMaterialized, Is.True);
                Assert.That(GameplayEffectConfigRegistry.CachedStaticDefinitionBlobCount, Is.EqualTo(0));
            }
            finally
            {
                world.Dispose();
            }
        }

        [Test]
        public void BakingPlanContractDoesNotExposeRuntimeEditorOrGeneratedSpecificTypes()
        {
            var checkedTypes = new[]
            {
                typeof(GASGeneratedDefinitionBakingEntry),
                typeof(GASGeneratedDefinitionBakingPlan),
                typeof(GASGeneratedDefinitionBakingPlanner),
                typeof(GASGeneratedDefinitionBakeWrite),
                typeof(GASGeneratedDefinitionArchetypeTemplate),
                typeof(GASGeneratedDefinitionBakeContract),
                typeof(GASGeneratedDefinitionBakeContractPlanner),
                typeof(GASGeneratedDefinitionCarrierArtifact),
                typeof(GASGeneratedDefinitionBakerInputArtifact),
                typeof(GASGeneratedDefinitionStaticBlobCacheRequest),
                typeof(GASGeneratedDefinitionRuntimeArchetypeArtifact),
                typeof(GASGeneratedDefinitionDeferredBoundaryArtifact),
                typeof(GASGeneratedDefinitionBakeResult),
                typeof(GASGeneratedDefinitionStaticBlobCacheWarmupResult),
                typeof(GASGeneratedDefinitionBakePipeline),
            };
            var forbiddenTypeNames = new[]
            {
                "cfg.",
                "SimpleJSON",
                "XLuban",
                "UnityEditor",
                "Unity.Entities.Entity",
                "BlobAssetReference",
                nameof(CEffectSpecData),
                nameof(CEffectContext),
                nameof(CDurationRuntime),
                nameof(CPeriodRuntime),
                nameof(CStackingRuntime),
                nameof(CAbilityRuntimeState),
                nameof(BAttribute),
            };

            for (var typeIndex = 0; typeIndex < checkedTypes.Length; typeIndex++)
            {
                var fields = checkedTypes[typeIndex].GetFields(
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (var fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    var fieldTypeName = fields[fieldIndex].FieldType.FullName ?? fields[fieldIndex].FieldType.Name;
                    for (var forbiddenIndex = 0; forbiddenIndex < forbiddenTypeNames.Length; forbiddenIndex++)
                    {
                        Assert.That(
                            fieldTypeName,
                            Does.Not.Contain(forbiddenTypeNames[forbiddenIndex]),
                            $"{checkedTypes[typeIndex].Name}.{fields[fieldIndex].Name} must not expose runtime, editor, or generated-specific type {forbiddenTypeNames[forbiddenIndex]}.");
                    }
                }
            }
        }

        private static void AssertHasMissingConfigDiagnostic(
            IReadOnlyList<ConfigRegistryDiagnostic> diagnostics,
            ConfigRegistryConfigKind missingKind,
            int missingCode,
            ConfigRegistryConfigKind sourceKind,
            int sourceCode,
            ConfigRegistryReferenceKind referenceKind)
        {
            for (var i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                if (diagnostic.Code == ConfigRegistryDiagnosticCode.MissingConfig
                    && diagnostic.MissingConfigKind == missingKind
                    && diagnostic.MissingConfigCode == missingCode
                    && diagnostic.SourceConfigKind == sourceKind
                    && diagnostic.SourceConfigCode == sourceCode
                    && diagnostic.ReferenceKind == referenceKind)
                {
                    return;
                }
            }

            Assert.Fail(
                $"Missing registry diagnostic not found: missing={missingKind}:{missingCode}, source={sourceKind}:{sourceCode}, reference={referenceKind}.");
        }

        private static void AssertHasValidationDiagnostic(
            IReadOnlyList<GASDefinitionValidationDiagnostic> diagnostics,
            GASDefinitionValidationCode code,
            GASDefinitionKind definitionKind,
            int definitionCode,
            GASDefinitionKind ownerKind,
            int ownerCode,
            int occurrenceCount)
        {
            for (var i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                if (diagnostic.Code == code
                    && diagnostic.DefinitionKind == definitionKind
                    && diagnostic.DefinitionCode == definitionCode
                    && diagnostic.OwnerKind == ownerKind
                    && diagnostic.OwnerCode == ownerCode
                    && diagnostic.OccurrenceCount == occurrenceCount)
                {
                    Assert.That(diagnostic.Severity, Is.EqualTo(GASDefinitionValidationSeverity.Error));
                    Assert.That(diagnostic.Message, Does.Contain(definitionKind.ToString()));
                    return;
                }
            }

            Assert.Fail(
                $"Missing validation diagnostic not found: code={code}, definition={definitionKind}:{definitionCode}, owner={ownerKind}:{ownerCode}, count={occurrenceCount}.");
        }

        private static bool TryFindBakingEntry(
            GASGeneratedDefinitionBakingPlan plan,
            GASDefinitionKind definitionKind,
            int definitionCode,
            out GASGeneratedDefinitionBakingEntry entry)
        {
            var entries = plan.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].DefinitionKind != definitionKind || entries[i].DefinitionCode != definitionCode)
                    continue;

                entry = entries[i];
                return true;
            }

            entry = default;
            return false;
        }

        private static bool TryFindBakeWrite(
            GASGeneratedDefinitionBakeContract contract,
            GASDefinitionKind definitionKind,
            int definitionCode,
            GASGeneratedDefinitionBakeWriteKind writeKind,
            out GASGeneratedDefinitionBakeWrite write)
        {
            var writes = contract.Writes;
            for (var i = 0; i < writes.Count; i++)
            {
                if (writes[i].DefinitionKind != definitionKind
                    || writes[i].DefinitionCode != definitionCode
                    || writes[i].WriteKind != writeKind)
                    continue;

                write = writes[i];
                return true;
            }

            write = default;
            return false;
        }

        private static bool TryFindBakeWrite(
            GASGeneratedDefinitionBakeContract contract,
            GASDefinitionKind definitionKind,
            int definitionCode,
            GASGeneratedDefinitionBakeWriteKind writeKind,
            GASGeneratedDefinitionBakingBoundary boundary,
            out GASGeneratedDefinitionBakeWrite write)
        {
            var writes = contract.Writes;
            for (var i = 0; i < writes.Count; i++)
            {
                if (writes[i].DefinitionKind != definitionKind
                    || writes[i].DefinitionCode != definitionCode
                    || writes[i].WriteKind != writeKind
                    || writes[i].Boundary != boundary)
                    continue;

                write = writes[i];
                return true;
            }

            write = default;
            return false;
        }

        private static bool TryFindArchetypeTemplate(
            GASGeneratedDefinitionBakeContract contract,
            GASDefinitionKind definitionKind,
            int definitionCode,
            out GASGeneratedDefinitionArchetypeTemplate template)
        {
            var templates = contract.ArchetypeTemplates;
            for (var i = 0; i < templates.Count; i++)
            {
                if (templates[i].DefinitionKind != definitionKind
                    || templates[i].DefinitionCode != definitionCode)
                    continue;

                template = templates[i];
                return true;
            }

            template = default;
            return false;
        }

        private static bool TryFindCarrierArtifact(
            GASGeneratedDefinitionBakeResult result,
            GASDefinitionKind definitionKind,
            int definitionCode,
            out GASGeneratedDefinitionCarrierArtifact artifact)
        {
            var artifacts = result.Carriers;
            for (var i = 0; i < artifacts.Count; i++)
            {
                if (artifacts[i].DefinitionKind != definitionKind || artifacts[i].DefinitionCode != definitionCode)
                    continue;

                artifact = artifacts[i];
                return true;
            }

            artifact = default;
            return false;
        }

        private static bool TryFindBakerInputArtifact(
            GASGeneratedDefinitionBakeResult result,
            GASDefinitionKind definitionKind,
            int definitionCode,
            out GASGeneratedDefinitionBakerInputArtifact artifact)
        {
            var artifacts = result.BakerInputs;
            for (var i = 0; i < artifacts.Count; i++)
            {
                if (artifacts[i].DefinitionKind != definitionKind || artifacts[i].DefinitionCode != definitionCode)
                    continue;

                artifact = artifacts[i];
                return true;
            }

            artifact = default;
            return false;
        }

        private static bool TryFindStaticBlobCacheRequest(
            GASGeneratedDefinitionBakeResult result,
            int gameplayEffectCode,
            out GASGeneratedDefinitionStaticBlobCacheRequest request)
        {
            var requests = result.StaticBlobCacheRequests;
            for (var i = 0; i < requests.Count; i++)
            {
                if (requests[i].DefinitionKind != GASDefinitionKind.GameplayEffect
                    || requests[i].DefinitionCode != gameplayEffectCode)
                    continue;

                request = requests[i];
                return true;
            }

            request = default;
            return false;
        }

        private static bool TryFindRuntimeArchetypeArtifact(
            GASGeneratedDefinitionBakeResult result,
            GASDefinitionKind definitionKind,
            int definitionCode,
            out GASGeneratedDefinitionRuntimeArchetypeArtifact artifact)
        {
            var artifacts = result.RuntimeArchetypes;
            for (var i = 0; i < artifacts.Count; i++)
            {
                if (artifacts[i].DefinitionKind != definitionKind || artifacts[i].DefinitionCode != definitionCode)
                    continue;

                artifact = artifacts[i];
                return true;
            }

            artifact = default;
            return false;
        }
    }
}
