using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.Runtime.Tests.Definition
{
    public sealed class GASDefinitionTableTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (!GASManager.IsInitialized)
                GASManager.Initialize();
        }

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
        public void BuildFromRegistriesCollectsAbilityEffectAttributeTagAndCueSummaries()
        {
            const int abilityCode = 1001;
            const int costEffectCode = 2001;
            const int cooldownEffectCode = 2002;
            const int activationEffectCode = 2003;
            const int timelineId = 3001;
            const int effectCode = 4001;
            const int cueCode = 5001;
            const int attrSetCode = 6001;
            const int attributeCode = 6002;
            const int tagCode = 7001;

            var abilityConfig = new AbilityConfig(new AbilityComponentConfig[]
            {
                new ConfAbilityBaseInfo { Code = abilityCode, Level = 3 },
                new ConfAbilityCost { GameplayEffectCode = costEffectCode },
                new ConfAbilityCooldown { Cooldown = 45, GameplayEffectCode = cooldownEffectCode },
                new ConfAbilityEffectsOnActivate { EffectCodes = new[] { activationEffectCode, 0 } },
                new ConfAbilityTimelineRef { TimelineId = timelineId },
                new ConfAbilityActivationRequiredTags { tags = new[] { tagCode } },
                new ConfAbilityActivationBlockedTags { tags = new[] { tagCode + 1 } },
                new ConfCancelAbilityWithTags { tags = new[] { tagCode + 2 } },
                new ConfBlockAbilityWithTags { tags = new[] { tagCode + 3 } },
            });
            var cueConfig = new GameplayCueConfig(
                typeof(CueLog),
                new XParamString("definition"),
                requiredTags: new[] { tagCode },
                immunityTags: new[] { tagCode + 1 });
            var effectConfig = new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfDuration { duration = 60, timeUnit = TimeUnit.Frame },
                new ConfPeriod { Period = 5, GameplayEffectCodes = new[] { effectCode + 1, effectCode + 2 } },
                new ConfStacking
                {
                    StackType = EffectStackType.AggregateByTarget,
                    StackingCode = effectCode,
                    LimitCount = 4,
                    OverflowEffectCodes = new[] { effectCode + 3 },
                },
                new ConfModifierConfig
                {
                    ModifierSettings = new[]
                    {
                        new ModifierDefinitionSetting
                        {
                            AttrSetCode = attrSetCode,
                            AttrCode = attributeCode,
                            Operation = EModifierOp.Add,
                            Magnitude = 10f,
                        },
                        new ModifierDefinitionSetting
                        {
                            AttrSetCode = attrSetCode,
                            AttrCode = attributeCode + 1,
                            Operation = EModifierOp.Subtract,
                            Magnitude = 2f,
                        },
                    },
                },
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
                new ConfApplicationRequiredTags { tags = new[] { tagCode } },
                new ConfCueOnApply { cues = new[] { cueConfig } },
            });
            var attrSet = new AttrSetConfig(
                attrSetCode,
                new[]
                {
                    new AttributeBaseSetting(attributeCode, 100f, true, true, 0f, 200f),
                    new AttributeBaseSetting(attributeCode + 1, 10f, false, false, 0f, 0f),
                });
            var tag = new GameplayTag(tagCode, new[] { tagCode - 1 }, new[] { tagCode + 1 });

            AbilityConfigRegistry.RegisterGetConfigByIDFunc(id => id == abilityCode ? abilityConfig : null);
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id => id == effectCode ? effectConfig : null);
            GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(id => id == cueCode ? cueConfig : null);

            var table = GASDefinitionSummaryBuilder.BuildFromRegistries(
                new[] { abilityCode },
                new[] { effectCode },
                new[] { attrSet },
                new[] { tag },
                new[] { cueCode });

            Assert.That(table.TotalDefinitionCount, Is.EqualTo(7));
            Assert.That(table.TryGetAbility(abilityCode, out var ability), Is.True);
            Assert.That(ability.Level, Is.EqualTo(3));
            Assert.That(ability.CostGameplayEffectCode, Is.EqualTo(costEffectCode));
            Assert.That(ability.CooldownGameplayEffectCode, Is.EqualTo(cooldownEffectCode));
            Assert.That(ability.Cooldown, Is.EqualTo(45));
            Assert.That(ability.ActivationEffectCount, Is.EqualTo(1));
            Assert.That(ability.TimelineId, Is.EqualTo(timelineId));
            Assert.That(ability.HasActivationRequiredTags, Is.True);
            Assert.That(ability.HasActivationBlockedTags, Is.True);
            Assert.That(ability.HasCancelAbilityTags, Is.True);
            Assert.That(ability.HasBlockAbilityTags, Is.True);

            Assert.That(table.TryGetGameplayEffect(effectCode, out var effect), Is.True);
            Assert.That(effect.HasDuration, Is.True);
            Assert.That(effect.Duration.Duration, Is.EqualTo(60));
            Assert.That(effect.HasPeriod, Is.True);
            Assert.That(effect.PeriodEffectCount, Is.EqualTo(2));
            Assert.That(effect.HasStacking, Is.True);
            Assert.That(effect.Stacking.LimitCount, Is.EqualTo(4));
            Assert.That(effect.OverflowEffectCount, Is.EqualTo(1));
            Assert.That(effect.ModifierCount, Is.EqualTo(2));
            Assert.That(effect.GrantedAbilityCount, Is.EqualTo(1));
            Assert.That(effect.HasApplicationRequiredTags, Is.True);
            Assert.That(effect.CueTriggerCount, Is.EqualTo(1));
            Assert.That(effect.HasManagedCueTriggers, Is.True);

            Assert.That(table.TryGetAttributeSet(attrSetCode, out var attributeSet), Is.True);
            Assert.That(attributeSet.AttributeCount, Is.EqualTo(2));
            Assert.That(attributeSet.HasClampedAttributes, Is.True);
            Assert.That(table.TryGetAttribute(attrSetCode, attributeCode, out var attribute), Is.True);
            Assert.That(attribute.InitialValue, Is.EqualTo(100f));
            Assert.That(attribute.IsClampMin, Is.True);
            Assert.That(attribute.IsClampMax, Is.True);

            Assert.That(table.TryGetGameplayTag(tagCode, out var tagSummary), Is.True);
            Assert.That(tagSummary.ParentCount, Is.EqualTo(1));
            Assert.That(tagSummary.ChildCount, Is.EqualTo(1));
            Assert.That(tagSummary.IsRoot, Is.False);
            Assert.That(tagSummary.HasChildren, Is.True);

            Assert.That(table.TryGetGameplayCue(cueCode, out var cue), Is.True);
            Assert.That(cue.HasRequiredTags, Is.True);
            Assert.That(cue.HasImmunityTags, Is.True);
            Assert.That(cue.UsesManagedPresentationFactory, Is.True);
        }

        [Test]
        public void GameplayEffectSummaryMatchesStaticDefinitionBlobBoundary()
        {
            const int effectCode = 8001;
            const int periodEffectCode = 8002;
            const int overflowEffectCode = 8003;
            const int abilityCode = 8004;
            const int attrSetCode = 8005;
            const int attributeCode = 8006;
            var effectConfig = new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfDuration { duration = 120, timeUnit = TimeUnit.Frame },
                new ConfPeriod { Period = 8, GameplayEffectCodes = new[] { periodEffectCode } },
                new ConfStacking
                {
                    StackType = EffectStackType.AggregateBySource,
                    StackingCode = effectCode,
                    LimitCount = 2,
                    OverflowEffectCodes = new[] { overflowEffectCode },
                },
                new ConfModifierConfig
                {
                    ModifierSettings = new[]
                    {
                        new ModifierDefinitionSetting
                        {
                            AttrSetCode = attrSetCode,
                            AttrCode = attributeCode,
                            Operation = EModifierOp.Add,
                            Magnitude = 5f,
                        },
                    },
                },
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
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id => id == effectCode ? effectConfig : null);

            Assert.That(
                GameplayEffectConfigRegistry.TryGetOrCreateStaticDefinitionBlob(
                    GASManager.EntityManager,
                    effectCode,
                    out var blob),
                Is.True);

            var summary = GASDefinitionSummaryBuilder.FromGameplayEffectStaticDefinition(blob);

            Assert.That(summary.GameplayEffectCode, Is.EqualTo(effectCode));
            Assert.That(summary.HasDuration, Is.True);
            Assert.That(summary.Duration.Duration, Is.EqualTo(120));
            Assert.That(summary.HasPeriod, Is.True);
            Assert.That(summary.PeriodEffectCount, Is.EqualTo(1));
            Assert.That(summary.HasStacking, Is.True);
            Assert.That(summary.OverflowEffectCount, Is.EqualTo(1));
            Assert.That(summary.ModifierCount, Is.EqualTo(1));
            Assert.That(summary.GrantedAbilityCount, Is.EqualTo(1));
            Assert.That(summary.CueTriggerCount, Is.EqualTo(0));
            Assert.That(summary.HasManagedCueTriggers, Is.False);
        }

        [Test]
        public void BuildFromRegistriesKeepsMissingConfigDiagnostics()
        {
            const int missingAbilityCode = 9101;
            const int missingEffectCode = 9102;
            const int missingCueCode = 9103;
            AbilityConfigRegistry.RegisterGetConfigByIDFunc(_ => null);
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(_ => null);
            GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(_ => null);
            ConfigRegistryDiagnostics.Clear();

            var table = GASDefinitionSummaryBuilder.BuildFromRegistries(
                new[] { missingAbilityCode },
                new[] { missingEffectCode },
                Array.Empty<AttrSetConfig>(),
                Array.Empty<GameplayTag>(),
                new[] { missingCueCode });

            Assert.That(table.TotalDefinitionCount, Is.EqualTo(0));
            Assert.That(ConfigRegistryDiagnostics.Count, Is.EqualTo(3));
            AssertHasMissingDiagnostic(ConfigRegistryConfigKind.Ability, missingAbilityCode);
            AssertHasMissingDiagnostic(ConfigRegistryConfigKind.GameplayEffect, missingEffectCode);
            AssertHasMissingDiagnostic(ConfigRegistryConfigKind.GameplayCue, missingCueCode);
        }

        [Test]
        public void GameplayEffectDefinitionLifecycleReloadOwnsPrototypeAndStaticBlobCaches()
        {
            const int effectCode = 9201;
            var entityManager = GASManager.EntityManager;
            var duration = 30;
            var firstEffect = Entity.Null;
            var secondEffect = Entity.Null;

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode ? CreateDurationDefinitionConfig(duration) : null);

            try
            {
                firstEffect = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(entityManager, effectCode);

                Assert.That(firstEffect, Is.Not.EqualTo(Entity.Null));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out var firstPrototype),
                    Is.True);
                Assert.That(entityManager.Exists(firstPrototype), Is.True);
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(effectCode, out var firstBlob),
                    Is.True);
                Assert.That(firstBlob.Value.Duration.Duration, Is.EqualTo(30));

                duration = 60;
                var reloadResult = GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                    id == effectCode ? CreateDurationDefinitionConfig(duration) : null);

                Assert.That(reloadResult.Before.OwnerKind,
                    Is.EqualTo(GameplayEffectDefinitionLifecycleOwnerKind.GameplayEffectConfigRegistry));
                Assert.That(reloadResult.Before.HasConfigProvider, Is.True);
                Assert.That(reloadResult.Before.CachedPrototypeCount, Is.EqualTo(1));
                Assert.That(reloadResult.Before.CachedStaticDefinitionBlobCount, Is.EqualTo(1));
                Assert.That(reloadResult.After.HasConfigProvider, Is.True);
                Assert.That(reloadResult.After.CachedPrototypeCount, Is.EqualTo(0));
                Assert.That(reloadResult.After.CachedStaticDefinitionBlobCount, Is.EqualTo(0));
                Assert.That(reloadResult.After.Generation, Is.EqualTo(reloadResult.Before.Generation + 1));
                Assert.That(reloadResult.UsedEntityManagerForPrototypeDisposal, Is.True);
                Assert.That(reloadResult.RemovedPrototypeEntryCount, Is.EqualTo(1));
                Assert.That(reloadResult.DestroyedPrototypeEntityCount, Is.EqualTo(1));
                Assert.That(reloadResult.RemovedStaticDefinitionBlobEntryCount, Is.EqualTo(1));
                Assert.That(reloadResult.DisposedStaticDefinitionBlobCount, Is.EqualTo(1));
                Assert.That(entityManager.Exists(firstPrototype), Is.False);

                secondEffect = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(entityManager, effectCode);

                Assert.That(secondEffect, Is.Not.EqualTo(Entity.Null));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out var rebuiltPrototype),
                    Is.True);
                Assert.That(rebuiltPrototype, Is.Not.EqualTo(firstPrototype));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(effectCode, out var rebuiltBlob),
                    Is.True);
                Assert.That(rebuiltBlob.Value.Duration.Duration, Is.EqualTo(60));
            }
            finally
            {
                DestroyIfExists(entityManager, secondEffect);
                DestroyIfExists(entityManager, firstEffect);
            }
        }

        [Test]
        public void GameplayEffectDefinitionLifecycleReloadControlsGameplayEffectDiagnostics()
        {
            const int missingEffectCode = 9202;
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(_ => null);
            ConfigRegistryDiagnostics.Clear();

            GameplayEffectConfigRegistry.GetConfigByID(missingEffectCode);

            Assert.That(ConfigRegistryDiagnostics.Count, Is.EqualTo(1));

            var keepDiagnostics = GameplayEffectConfigRegistry.ReloadDefinitionCaches(
                clearGameplayEffectDiagnostics: false);

            Assert.That(keepDiagnostics.ClearedGameplayEffectDiagnosticCount, Is.EqualTo(0));
            Assert.That(ConfigRegistryDiagnostics.Count, Is.EqualTo(1));

            var clearDiagnostics = GameplayEffectConfigRegistry.ReloadDefinitionCaches(
                clearGameplayEffectDiagnostics: true);

            Assert.That(clearDiagnostics.ClearedGameplayEffectDiagnosticCount, Is.EqualTo(1));
            Assert.That(ConfigRegistryDiagnostics.Count, Is.EqualTo(0));
            Assert.That(clearDiagnostics.After.Generation, Is.EqualTo(clearDiagnostics.Before.Generation + 1));
        }

        [Test]
        public void GameplayEffectDefinitionLifecycleContractDoesNotExposeRuntimeState()
        {
            var disallowedTypes = new HashSet<Type>
            {
                typeof(Entity),
                typeof(BlobAssetReference<GEStaticDefinitionBlob>),
                typeof(CEffectSpecData),
                typeof(CEffectContext),
                typeof(CDurationRuntime),
                typeof(CPeriodRuntime),
                typeof(CStackingRuntime),
                typeof(CAbilityRuntimeState),
                typeof(BAttribute),
            };
            var lifecycleTypes = new[]
            {
                typeof(GameplayEffectDefinitionCacheState),
                typeof(GameplayEffectDefinitionCacheReloadResult),
            };

            for (var i = 0; i < lifecycleTypes.Length; i++)
            {
                var fields = lifecycleTypes[i].GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (var fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    var field = fields[fieldIndex];
                    Assert.That(
                        IsDefinitionLifecycleRuntimeType(field.FieldType, disallowedTypes),
                        Is.False,
                        $"{lifecycleTypes[i].Name}.{field.Name} must not expose runtime state type {field.FieldType.Name}.");
                }
            }
        }

        [Test]
        public void DefinitionSummariesDoNotCarrySpecContextOrRuntimeStateFields()
        {
            var disallowedFieldNames = new HashSet<string>
            {
                nameof(CEffectSpecData.StackCount),
                nameof(CEffectSpecData.DurationFrameOverride),
                nameof(CEffectContext.SourceAsc),
                nameof(CEffectContext.TargetAsc),
                nameof(CEffectContext.SourceEffect),
                nameof(CEffectContext.Instigator),
                nameof(CEffectContext.Causer),
                nameof(CEffectContext.ContextId),
                nameof(CEffectContext.TargetDataKind),
                nameof(CDurationRuntime.ActiveTime),
                nameof(CDurationRuntime.LastActiveTime),
                nameof(CDurationRuntime.RemainingTime),
                nameof(CPeriodRuntime.StartTime),
                nameof(CStackingRuntime.StackCount),
                nameof(CAbilityRuntimeState.Phase),
                nameof(CAbilityRuntimeState.RemainingFrame),
                nameof(CAbilityRuntimeState.Timer),
                nameof(BAttribute.BaseValue),
                nameof(BAttribute.CurrentValue),
                nameof(BAttribute.PreviousCurrentValue),
                nameof(BAttribute.Dirty),
                nameof(BAttribute.CurrentValueChangePending),
            };
            var disallowedTypes = new HashSet<Type>
            {
                typeof(Entity),
                typeof(CEffectSpecData),
                typeof(CEffectContext),
                typeof(CDurationRuntime),
                typeof(CPeriodRuntime),
                typeof(CStackingRuntime),
                typeof(CAbilityRuntimeState),
                typeof(BAttribute),
            };
            var summaryTypes = new[]
            {
                typeof(AbilityDefinitionSummary),
                typeof(GameplayEffectDefinitionSummary),
                typeof(AttributeSetDefinitionSummary),
                typeof(AttributeDefinitionSummary),
                typeof(GameplayTagDefinitionSummary),
                typeof(GameplayCueDefinitionSummary),
                typeof(GASDefinitionTable),
            };

            for (var i = 0; i < summaryTypes.Length; i++)
            {
                var fields = summaryTypes[i].GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (var fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    var field = fields[fieldIndex];
                    Assert.That(
                        disallowedFieldNames.Contains(field.Name),
                        Is.False,
                        $"{summaryTypes[i].Name}.{field.Name} must not be owned by Definition.");
                    Assert.That(
                        disallowedTypes.Contains(field.FieldType),
                        Is.False,
                        $"{summaryTypes[i].Name}.{field.Name} must not carry runtime-only type {field.FieldType.Name}.");
                }
            }
        }

        private static GameplayEffectConfig CreateDurationDefinitionConfig(int duration)
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfDuration
                {
                    duration = duration,
                    timeUnit = TimeUnit.Frame,
                },
            });
        }

        private static bool IsDefinitionLifecycleRuntimeType(
            Type fieldType,
            HashSet<Type> disallowedTypes)
        {
            if (disallowedTypes.Contains(fieldType))
                return true;

            return fieldType.IsGenericType
                   && fieldType.GetGenericTypeDefinition() == typeof(BlobAssetReference<>);
        }

        private static void DestroyIfExists(EntityManager entityManager, Entity entity)
        {
            if (entity != Entity.Null && entityManager.Exists(entity))
                entityManager.DestroyEntity(entity);
        }

        private static void AssertHasMissingDiagnostic(
            ConfigRegistryConfigKind missingKind,
            int missingCode)
        {
            var diagnostics = ConfigRegistryDiagnostics.Snapshot();
            for (var i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].MissingConfigKind == missingKind
                    && diagnostics[i].MissingConfigCode == missingCode
                    && diagnostics[i].Code == ConfigRegistryDiagnosticCode.MissingConfig)
                    return;
            }

            Assert.Fail($"Missing diagnostic {missingKind}:{missingCode} was not found.");
        }
    }
}
