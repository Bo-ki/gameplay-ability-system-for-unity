using System;
using System.Collections.Generic;
using GAS.Runtime.Generated;
using Unity.Entities;

namespace GAS.Runtime
{
    public static class HeadlessAutoChessDefinitionSource
    {
        public const string TargetCatcherName = "HeadlessAutoChess.Target";
        public const int GameplayCueNoopOnApply = 9641;

        private static readonly HeadlessAutoChessGeneratedDefinitionPackage RuntimeProviderPackage =
            HeadlessAutoChessGeneratedDefinitionRows.CreateLubanSourceGeneratorPackage();

        private static readonly HeadlessAutoChessGeneratedRegistrySnapshot RuntimeProviderSnapshot =
            RuntimeProviderPackage.Snapshot;

        public static HeadlessAutoChessGeneratedDefinitionPackage CreateGeneratedDefinitionPackage()
        {
            return HeadlessAutoChessGeneratedDefinitionRows.CreateLubanSourceGeneratorPackage();
        }

        public static HeadlessAutoChessGeneratedRegistrySnapshot CreateGeneratedRegistrySnapshot()
        {
            return CreateGeneratedDefinitionPackage().Snapshot;
        }

        public static GASGeneratedDefinitionSource CreateGeneratedSource()
        {
            return CreateGeneratedDefinitionPackage().CreateDefinitionSource();
        }

        public static int[] CreateAbilityCodes()
        {
            return CreateGeneratedRegistrySnapshot().CreateAbilityCodes();
        }

        public static int[] CreateGameplayEffectCodes()
        {
            return CreateGeneratedRegistrySnapshot().CreateGameplayEffectCodes();
        }

        public static int[] CreateTimelineIds()
        {
            return CreateGeneratedRegistrySnapshot().CreateTimelineIds();
        }

        public static int[] CreateGameplayCueCodes()
        {
            return CreateGeneratedRegistrySnapshot().CreateGameplayCueCodes();
        }

        public static GameplayTag[] CreateGameplayTags()
        {
            return CreateGeneratedRegistrySnapshot().CreateGameplayTags();
        }

        public static AttrSetConfig CreateCombatAttributeSet(
            float health,
            float mana,
            float maxHealth,
            float maxMana,
            float arcaneResistance = 0f,
            float counterDamage = 0f,
            float lifeStealRatio = 0f)
        {
            var attributeRows = HeadlessAutoChessGeneratedDefinitionRows.CreateAttributeRows(
                health,
                mana,
                maxHealth,
                maxMana,
                shield: 0f,
                maxShield: HeadlessAutoChessScenario.MaxShieldAmount,
                arcaneResistance: arcaneResistance,
                maxArcaneResistance: HeadlessAutoChessScenario.MaxArcaneResistance,
                counterDamage: counterDamage,
                maxCounterDamage: HeadlessAutoChessScenario.MaxCounterDamageAmount,
                lifeStealRatio: lifeStealRatio,
                maxLifeStealRatio: HeadlessAutoChessScenario.MaxLifeStealRatio);
            var settings = new AttributeBaseSetting[attributeRows.Length];
            for (var i = 0; i < attributeRows.Length; i++)
                settings[i] = attributeRows[i].ToAttributeSetting();

            return new AttrSetConfig(
                HeadlessAutoChessScenario.AttributeSetCombat,
                settings);
        }

        public static void RegisterRuntimeProviders()
        {
            AbilityConfigRegistry.RegisterGetConfigByIDFunc(CreateAbilityConfig);
            TimelineAbilityConfigRegistry.RegisterGetConfigByIDFunc(CreateTimelineConfig);
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(CreateGameplayEffectConfig);
            GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(CreateGameplayCueConfig);
            WarmupGameplayEffectPrototypes();
        }

        public static void ClearRuntimeProviders()
        {
            AbilityConfigRegistry.RegisterGetConfigByIDFunc(null);
            TimelineAbilityConfigRegistry.RegisterGetConfigByIDFunc(null);
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
            GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(null);
        }

        public static AbilityConfig CreateAbilityConfig(int abilityCode)
        {
            return RuntimeProviderSnapshot.TryFindAbilityRow(abilityCode, out var row)
                ? CreateAbilityConfig(row)
                : null;
        }

        public static XParamTimeline CreateTimelineConfig(int timelineId)
        {
            return RuntimeProviderSnapshot.TryFindTimelineRow(timelineId, out var row)
                ? CreateApplyEffectTimeline(row)
                : null;
        }

        public static GameplayEffectConfig CreateGameplayEffectConfig(int gameplayEffectCode)
        {
            return RuntimeProviderSnapshot.TryFindGameplayEffectRow(gameplayEffectCode, out var row)
                ? CreateGameplayEffectConfig(row)
                : null;
        }

        public static void WarmupGameplayEffectPrototypes()
        {
            if (!GASManager.IsInitialized)
                return;

            var em = GASManager.EntityManager;
            var codes = RuntimeProviderSnapshot.CreateGameplayEffectCodes();
            for (var i = 0; i < codes.Length; i++)
            {
                GameplayEffectConfigRegistry.TryWarmupRuntimePrototype(
                    em,
                    codes[i],
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.GameplayEffect,
                        codes[i],
                        ConfigRegistryReferenceKind.Direct));
            }
        }

        public static GameplayCueConfig CreateGameplayCueConfig(int gameplayCueCode)
        {
            return RuntimeProviderSnapshot.TryFindGameplayCueRow(gameplayCueCode, out var row)
                ? CreateGameplayCueConfig(row)
                : null;
        }

        private static AbilityConfig CreateAbilityConfig(in HeadlessAutoChessAbilityDefinitionRow row)
        {
            var components = new List<AbilityComponentConfig>
            {
                new ConfAbilityBaseInfo
                {
                    Code = row.AbilityCode,
                    Level = row.Level,
                },
            };

            if (row.HasActivationOwnedTag)
                components.Add(new HeadlessAutoChessDirectMaskAbilityTagsConfig(new[] { row.ActivationOwnedTagCode }));

            if (row.HasCost)
            {
                components.Add(new ConfAbilityCost
                {
                    GameplayEffectCode = row.CostGameplayEffectCode,
                });
            }

            if (row.HasCooldown)
            {
                components.Add(new ConfAbilityCooldown
                {
                    Cooldown = row.CooldownFrames,
                    GameplayEffectCode = row.CooldownGameplayEffectCode,
                });
            }

            if (row.HasTimeline)
            {
                if (RuntimeProviderSnapshot.TryFindTimelineRow(row.TimelineId, out var timelineRow))
                {
                    components.Add(new ConfAbilityTargetEffectsOnActivate
                    {
                        EffectCodes = timelineRow.CreateGameplayEffectCodes(),
                        AutoEndOnCommit = true,
                    });
                }
            }

            return new AbilityConfig(components.ToArray());
        }

        private static XParamTimeline CreateApplyEffectTimeline(in HeadlessAutoChessTimelineDefinitionRow row)
        {
            var applyEffects = new XParamApplyEffects(row.CreateGameplayEffectCodes());
            applyEffects.SetCatcherType(row.TargetCatcherName);
            applyEffects.SetParam(new XParamNone());

            return new XParamTimeline(
                row.TimelineId,
                row.Name,
                1,
                false,
                new List<Track>
                {
                    new Track
                    {
                        Name = "ApplyTargetEffect",
                        ActionClips = new List<TimelineActionClipData>
                        {
                            new TimelineActionClipData
                            {
                                Name = "ApplyEffect",
                                StartTime = 0,
                                EndTime = 1,
                                ActionType = "ApplyEffects",
                                Parameter = applyEffects,
                            },
                        },
                    },
                });
        }

        private static GameplayEffectConfig CreateGameplayEffectConfig(
            in HeadlessAutoChessGameplayEffectDefinitionRow row)
        {
            var components = new List<GameplayEffectComponentConfig>
            {
                new ConfEffectBasicInfo
                {
                    Name = row.Name,
                },
            };

            if (row.HasDuration)
            {
                components.Add(new ConfDuration
                {
                    duration = row.DurationFrames,
                    timeUnit = TimeUnit.Frame,
                    ResetStartTimeWhenActivated = true,
                    StopTickWhenDeactivated = false,
                });
            }

            if (row.HasPeriod)
            {
                components.Add(new ConfPeriod
                {
                    Period = row.PeriodFrames,
                    ResetTimeCountWhenDeactivated = true,
                    GameplayEffectCodes = row.PeriodGameplayEffectCode > 0
                        ? new[] { row.PeriodGameplayEffectCode }
                        : Array.Empty<int>(),
                });
            }

            if (row.HasStacking)
            {
                components.Add(new ConfStacking
                {
                    StackType = row.StackType,
                    StackingCode = row.StackingCode,
                    LimitCount = row.StackLimitCount,
                    EffectDurationRefreshPolicy = row.EffectDurationRefreshPolicy,
                    EffectPeriodResetPolicy = row.EffectPeriodResetPolicy,
                    EffectExpirationPolicy = row.EffectExpirationPolicy,
                    denyOverflowApplication = row.DenyOverflowApplication,
                    clearStackOnOverflow = row.ClearStackOnOverflow,
                    OverflowEffectCodes = row.HasOverflowGameplayEffect
                        ? new[] { row.OverflowGameplayEffectCode }
                        : Array.Empty<int>(),
                });
            }

            if (row.HasModifier)
            {
                if (IsShieldRoutedDamage(row))
                {
                    components.Add(new HeadlessAutoChessShieldDamageExecutionConfig(
                        row.GameplayEffectCode,
                        row.ModifierMagnitude,
                        row.DamageTypeCode,
                        row.ResistanceAttributeSetCode,
                        row.ResistanceAttributeCode,
                        row.ResistanceCap));
                }
                else
                {
                    components.Add(new ConfModifierConfig
                    {
                        ModifierSettings = new[]
                        {
                            new ModifierDefinitionSetting
                            {
                                AttrSetCode = row.ModifierAttributeSetCode,
                                AttrCode = row.ModifierAttributeCode,
                                Operation = row.ModifierOperation,
                                Magnitude = row.ModifierMagnitude,
                            },
                        },
                    });

                    if (row.ModifierMagnitudeSource != EMagnitudeSource.Constant)
                        components.Add(new HeadlessAutoChessMagnitudeDefinitionConfig(row));
                }
            }

            if (RuntimeProviderSnapshot.TryFindSummonRow(row.GameplayEffectCode, out var summonRow))
                components.Add(new HeadlessAutoChessSummonRequestConfig(summonRow));

            if (row.HasRemoveGameplayEffectsWithTags)
                components.Add(new HeadlessAutoChessDirectMaskRemoveEffectWithTagsConfig(
                    new[] { row.RemoveGameplayEffectTagCode }));

            if (row.HasGrantedTag)
                components.Add(new HeadlessAutoChessDirectMaskGrantedTagsConfig(new[] { row.GrantedTagCode }));

            if (row.HasGameplayCue)
            {
                components.Add(new ConfGameplayEffectCueRequestOnApply
                {
                    CueCode = row.GameplayCueCode,
                });
            }

            return new GameplayEffectConfig(components.ToArray());
        }

        private static bool IsShieldRoutedDamage(in HeadlessAutoChessGameplayEffectDefinitionRow row)
        {
            return row.ModifierAttributeSetCode == HeadlessAutoChessScenario.AttributeSetCombat
                   && row.ModifierAttributeCode == HeadlessAutoChessScenario.AttributeHealth
                   && row.ModifierOperation == EModifierOp.Subtract
                   && row.ModifierMagnitude > 0f;
        }

        private static GameplayCueConfig CreateGameplayCueConfig(in HeadlessAutoChessGameplayCueDefinitionRow row)
        {
            if (row.PresentationKey != HeadlessAutoChessGeneratedDefinitionRows.NoopCuePresentationKey)
                return null;

            return new GameplayCueConfig(typeof(HeadlessAutoChessNoopCue), new XParamNone());
        }

        private static CTagMask CreateDenseMask(IEnumerable<int> tagCodes)
        {
            var mask = new CTagMask();
            if (tagCodes == null)
                return mask;

            foreach (var tagCode in tagCodes)
            {
                if (AutoChessTagMaskTable.TryGetDenseIndex(tagCode, out var denseIndex))
                    mask.AddTag(denseIndex);
            }

            return mask;
        }

        private sealed class HeadlessAutoChessDirectMaskAbilityTagsConfig :
            AbilityComponentConfig,
            IGASDefinitionAbilityActivationOwnedTagsProvider
        {
            private readonly CTagMask _tags;

            public HeadlessAutoChessDirectMaskAbilityTagsConfig(IEnumerable<int> tagIndices)
            {
                _tags = CreateDenseMask(tagIndices);
            }

            public override void LoadToGameplayAbilityEntity(Entity ability)
            {
                _entityManager.AddComponentData(ability, new CAbilityActivationOwnedTags
                {
                    Tags = _tags,
                });
            }

            public bool TryGetDefinitionActivationOwnedTags(out CTagMask tags)
            {
                tags = _tags;
                return !tags.IsEmpty;
            }
        }

        private sealed class HeadlessAutoChessDirectMaskGrantedTagsConfig :
            GameplayEffectComponentConfig,
            IGASDefinitionGameplayEffectGrantedTagsProvider
        {
            private readonly CTagMask _tags;

            public HeadlessAutoChessDirectMaskGrantedTagsConfig(IEnumerable<int> tagIndices)
            {
                _tags = CreateDenseMask(tagIndices);
            }

            public override void LoadToGameplayEffectEntity(Entity ge)
            {
                _entityManager.AddComponentData(ge, new CEffectGrantedTags
                {
                    Tags = _tags,
                });
            }

            public bool TryGetDefinitionGrantedTags(out CTagMask tags)
            {
                tags = _tags;
                return !tags.IsEmpty;
            }
        }

        private sealed class HeadlessAutoChessDirectMaskRemoveEffectWithTagsConfig :
            GameplayEffectComponentConfig,
            IGASDefinitionGameplayEffectRemoveTagsProvider
        {
            private readonly CTagMask _tags;

            public HeadlessAutoChessDirectMaskRemoveEffectWithTagsConfig(IEnumerable<int> tagIndices)
            {
                _tags = CreateDenseMask(tagIndices);
            }

            public override void LoadToGameplayEffectEntity(Entity ge)
            {
                _entityManager.AddComponentData(ge, new CRemoveEffectWithTags
                {
                    requirement = new TagRequirementMask
                    {
                        Any = _tags,
                    },
                });
            }

            public bool TryGetDefinitionRemoveGameplayEffectsWithTags(out TagRequirementMask requirement)
            {
                requirement = new TagRequirementMask
                {
                    Any = _tags,
                };
                return !requirement.IsEmpty;
            }
        }

        private sealed class HeadlessAutoChessShieldDamageExecutionConfig : GameplayEffectComponentConfig
        {
            private readonly int _gameplayEffectCode;
            private readonly float _baseDamage;
            private readonly int _damageTypeCode;
            private readonly int _resistanceAttributeSetCode;
            private readonly int _resistanceAttributeCode;
            private readonly float _resistanceCap;

            public HeadlessAutoChessShieldDamageExecutionConfig(
                int gameplayEffectCode,
                float baseDamage,
                int damageTypeCode,
                int resistanceAttributeSetCode,
                int resistanceAttributeCode,
                float resistanceCap)
            {
                _gameplayEffectCode = gameplayEffectCode;
                _baseDamage = baseDamage;
                _damageTypeCode = damageTypeCode;
                _resistanceAttributeSetCode = resistanceAttributeSetCode;
                _resistanceAttributeCode = resistanceAttributeCode;
                _resistanceCap = resistanceCap;
            }

            public override void LoadToGameplayEffectEntity(Entity ge)
            {
                _entityManager.AddComponentData(ge, new CHeadlessAutoChessShieldDamageCalculation
                {
                    AttributeSetCode = HeadlessAutoChessScenario.AttributeSetCombat,
                    HealthAttrCode = HeadlessAutoChessScenario.AttributeHealth,
                    ShieldAttrCode = HeadlessAutoChessScenario.AttributeShield,
                    ShieldDamageOutputKey = HeadlessAutoChessScenario.ExecutionCalculationShieldDamageOutput,
                    HealthDamageOutputKey = HeadlessAutoChessScenario.ExecutionCalculationHealthDamageOutput,
                    ResistedDamageOutputKey = HeadlessAutoChessScenario.ExecutionCalculationResistedDamageOutput,
                    DamageTypeCode = _damageTypeCode,
                    ResistanceAttrSetCode = _resistanceAttributeSetCode,
                    ResistanceAttrCode = _resistanceAttributeCode,
                    ResistanceCap = _resistanceCap,
                    BaseDamage = _baseDamage,
                });

                var definitions = _entityManager.HasBuffer<BExecutionCalculationOutputModifierDefinition>(ge)
                    ? _entityManager.GetBuffer<BExecutionCalculationOutputModifierDefinition>(ge)
                    : _entityManager.AddBuffer<BExecutionCalculationOutputModifierDefinition>(ge);
                definitions.Add(new BExecutionCalculationOutputModifierDefinition
                {
                    CalculationCode = _gameplayEffectCode,
                    OutputKey = HeadlessAutoChessScenario.ExecutionCalculationShieldDamageOutput,
                    AttrSetCode = HeadlessAutoChessScenario.AttributeSetCombat,
                    AttributeCode = HeadlessAutoChessScenario.AttributeShield,
                    Op = EModifierOp.Subtract,
                    FallbackMagnitude = 0f,
                    Coefficient = 1f,
                });
                definitions.Add(new BExecutionCalculationOutputModifierDefinition
                {
                    CalculationCode = _gameplayEffectCode,
                    OutputKey = HeadlessAutoChessScenario.ExecutionCalculationHealthDamageOutput,
                    AttrSetCode = HeadlessAutoChessScenario.AttributeSetCombat,
                    AttributeCode = HeadlessAutoChessScenario.AttributeHealth,
                    Op = EModifierOp.Subtract,
                    FallbackMagnitude = _baseDamage,
                    Coefficient = 1f,
                });
            }
        }

        private sealed class HeadlessAutoChessMagnitudeDefinitionConfig : GameplayEffectComponentConfig
        {
            private readonly HeadlessAutoChessGameplayEffectDefinitionRow _row;

            public HeadlessAutoChessMagnitudeDefinitionConfig(HeadlessAutoChessGameplayEffectDefinitionRow row)
            {
                _row = row;
            }

            public override void LoadToGameplayEffectEntity(Entity ge)
            {
                var definitions = _entityManager.HasBuffer<BMagnitudeDefinition>(ge)
                    ? _entityManager.GetBuffer<BMagnitudeDefinition>(ge)
                    : _entityManager.AddBuffer<BMagnitudeDefinition>(ge);
                definitions.Add(new BMagnitudeDefinition
                {
                    ModifierIndex = 0,
                    Source = _row.ModifierMagnitudeSource,
                    Key = _row.ModifierMagnitudeKey,
                    FallbackMagnitude = _row.ModifierMagnitude,
                    Coefficient = 1f,
                });
            }
        }

        private sealed class HeadlessAutoChessSummonRequestConfig : GameplayEffectComponentConfig
        {
            private readonly HeadlessAutoChessSummonDefinitionRow _row;

            public HeadlessAutoChessSummonRequestConfig(HeadlessAutoChessSummonDefinitionRow row)
            {
                _row = row;
            }

            public override void LoadToGameplayEffectEntity(Entity ge)
            {
                _entityManager.AddComponentData(ge, new CHeadlessAutoChessSummonRequest
                {
                    SummonedUnitCode = _row.SummonedUnitCode,
                    HealthAttrSetCode = HeadlessAutoChessScenario.AttributeSetCombat,
                    HealthAttrCode = HeadlessAutoChessScenario.AttributeHealth,
                    ManaAttrSetCode = HeadlessAutoChessScenario.AttributeSetCombat,
                    ManaAttrCode = HeadlessAutoChessScenario.AttributeMana,
                    ShieldAttrSetCode = HeadlessAutoChessScenario.AttributeSetCombat,
                    ShieldAttrCode = HeadlessAutoChessScenario.AttributeShield,
                    ArcaneResistanceAttrSetCode = HeadlessAutoChessScenario.AttributeSetCombat,
                    ArcaneResistanceAttrCode = HeadlessAutoChessScenario.AttributeArcaneResistance,
                    FixedTagCode = _row.FixedTagCode,
                    PrimaryAbilityCode = _row.PrimaryAbilityCode,
                    PrimaryCooldownTagIndex = -1,
                    LifetimeTurns = _row.LifetimeTurns,
                    SlotOffset = _row.SlotOffset,
                    BoardXOffset = _row.BoardXOffset,
                    BoardYOffset = _row.BoardYOffset,
                    TurnOrderOffset = _row.TurnOrderOffset,
                    Health = _row.Health,
                    Mana = _row.Mana,
                    Shield = _row.Shield,
                    ArcaneResistance = 0f,
                    MaxHealth = _row.MaxHealth,
                    MaxMana = _row.MaxMana,
                    MaxShield = _row.MaxShield,
                    MaxArcaneResistance = HeadlessAutoChessScenario.MaxArcaneResistance,
                    PrimaryTargetPolicy = _row.PrimaryTargetPolicy,
                });
            }
        }
    }
}
