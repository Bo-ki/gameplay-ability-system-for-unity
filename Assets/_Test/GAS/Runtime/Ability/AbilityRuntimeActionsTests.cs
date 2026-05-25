using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Tests.Ability
{
    public sealed class AbilityRuntimeActionsTests
    {
        private EntityManager _em;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (!GASManager.IsInitialized)
                GASManager.Initialize();
        }

        [SetUp]
        public void SetUp()
        {
            _em = GASManager.EntityManager;
            ConfigRegistryDiagnostics.Clear();
            ClearTransientEventBuffers();
            ClearEffectCommandStream();
        }

        [TearDown]
        public void TearDown()
        {
            ClearTransientEventBuffers();
            ClearEffectCommandStream();
        }

        [Test]
        public void RequestCostGameplayEffectWritesSimpleInstantSelfCommand()
        {
            const int costEffectCode = 3002;
            const int attrSetCode = 10;
            const int attributeCode = 20;

            var owner = CreateStandardAsc();
            var ability = CreateAbility(owner, level: 4);
            var request = Entity.Null;

            try
            {
                _em.GetBuffer<BAttribute>(owner).Add(new BAttribute
                {
                    AttrSetCode = attrSetCode,
                    Code = attributeCode,
                    BaseValue = 100,
                    CurrentValue = 100,
                });
                _em.AddComponentData(ability, new CAbilityCost
                {
                    GameplayEffectCode = costEffectCode,
                });

                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                    id == costEffectCode
                        ? new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                        {
                            new ConfModifierConfig
                            {
                                ModifierSettings = new[]
                                {
                                    new ModifierDefinitionSetting
                                    {
                                        AttrSetCode = attrSetCode,
                                        AttrCode = attributeCode,
                                        Operation = EModifierOp.Subtract,
                                        Magnitude = 25,
                                    },
                                },
                            },
                        })
                        : null);

                request = AbilityRuntimeActions.RequestCostGameplayEffect(ability, _em);
                Assert.That(request, Is.EqualTo(Entity.Null));
                Assert.That(CountApplyRequestsByCode(costEffectCode), Is.EqualTo(0));
                Assert.That(FindEffectByCode(costEffectCode), Is.EqualTo(Entity.Null));

                var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
                var commands = _em.GetBuffer<BEffectCommand>(streamEntity);
                Assert.That(commands.Length, Is.EqualTo(1));

                var command = commands[0];
                Assert.That(command.Kind, Is.EqualTo(EEffectCommandKind.Instant));
                Assert.That(command.Source, Is.EqualTo(EEffectCommandSource.Ability));
                Assert.That(command.GameplayEffectCode, Is.EqualTo(costEffectCode));
                Assert.That(command.SourceAsc, Is.EqualTo(owner));
                Assert.That(command.TargetAsc, Is.EqualTo(owner));
                Assert.That(command.SourceAbility, Is.EqualTo(ability));
                Assert.That(command.Instigator, Is.EqualTo(owner));
                Assert.That(command.Causer, Is.EqualTo(ability));
                Assert.That(command.Level, Is.EqualTo(4));
                Assert.That(command.DurationFrameOverride, Is.EqualTo(0));
                Assert.That(command.TargetDataKind, Is.EqualTo(ETargetDataKind.Self));

                var attribute = _em.GetBuffer<BAttribute>(owner)[0];
                Assert.That(attribute.BaseValue, Is.EqualTo(100));
                Assert.That(attribute.CurrentValue, Is.EqualTo(100));
            }
            finally
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
                DestroyIfExists(request);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void RequestCostGameplayEffectWritesSelfCommandAndProjectsCueOnApply()
        {
            const int costEffectCode = 3003;
            const int attrSetCode = 10;
            const int attributeCode = 21;
            const int cueCode = 31;

            var owner = CreateStandardAsc();
            var ability = CreateAbility(owner, level: 4);
            var request = Entity.Null;

            try
            {
                _em.GetBuffer<BAttribute>(owner).Add(new BAttribute
                {
                    AttrSetCode = attrSetCode,
                    Code = attributeCode,
                    BaseValue = 100,
                    CurrentValue = 100,
                });
                _em.AddComponentData(ability, new CAbilityCost
                {
                    GameplayEffectCode = costEffectCode,
                });

                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                    id == costEffectCode
                        ? new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                        {
                            new ConfModifierConfig
                            {
                                ModifierSettings = new[]
                                {
                                    new ModifierDefinitionSetting
                                    {
                                        AttrSetCode = attrSetCode,
                                        AttrCode = attributeCode,
                                        Operation = EModifierOp.Subtract,
                                        Magnitude = 25,
                                    },
                                },
                            },
                            new ConfGameplayEffectCueRequestOnApply
                            {
                                CueCode = cueCode,
                            },
                        })
                        : null);

                request = AbilityRuntimeActions.RequestCostGameplayEffect(ability, _em);
                Assert.That(request, Is.EqualTo(Entity.Null));
                Assert.That(CountApplyRequestsByCode(costEffectCode), Is.EqualTo(0));
                Assert.That(FindEffectByCode(costEffectCode), Is.EqualTo(Entity.Null));

                var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
                var commands = _em.GetBuffer<BEffectCommand>(streamEntity);
                Assert.That(commands.Length, Is.EqualTo(1));
                Assert.That(commands[0].GameplayEffectCode, Is.EqualTo(costEffectCode));
                Assert.That(commands[0].SourceAsc, Is.EqualTo(owner));
                Assert.That(commands[0].TargetAsc, Is.EqualTo(owner));
                Assert.That(commands[0].SourceAbility, Is.EqualTo(ability));
                Assert.That(commands[0].TargetDataKind, Is.EqualTo(ETargetDataKind.Self));

                RunCommandGroup();

                var specs = _em.GetBuffer<BInstantEffectSpec>(streamEntity);
                var deltas = _em.GetBuffer<BAttributeDelta>(streamEntity);
                var facts = _em.GetBuffer<BTypedSimulationFact>(streamEntity);
                var cueRequests = _em.GetBuffer<BCueRequest>(GASManager.EntityEventBus);
                var gameplayEvents = _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus);
                var attribute = _em.GetBuffer<BAttribute>(owner)[0];

                Assert.That(specs.Length, Is.EqualTo(1));
                Assert.That(specs[0].CueRequestOnApplyCode, Is.EqualTo(cueCode));
                Assert.That(deltas.Length, Is.EqualTo(1));
                Assert.That(facts.Length, Is.EqualTo(1));
                Assert.That(attribute.BaseValue, Is.EqualTo(75));
                Assert.That(attribute.CurrentValue, Is.EqualTo(75));
                Assert.That(cueRequests.Length, Is.EqualTo(1));
                Assert.That(cueRequests[0].TargetAsc, Is.EqualTo(owner));
                Assert.That(cueRequests[0].SourceAsc, Is.EqualTo(owner));
                Assert.That(cueRequests[0].SourceAbility, Is.EqualTo(ability));
                Assert.That(cueRequests[0].CueEvent, Is.EqualTo(EGameplayCueEvent.OnApply));
                Assert.That(
                    ContainsGameplayEvent(
                        gameplayEvents,
                        EGameplayEventType.CueRequested,
                        (int)EGameplayCueEvent.OnApply,
                        cueCode,
                        specs[0].ContextId),
                    Is.True);
                Assert.That(CountApplyRequestsByCode(costEffectCode), Is.EqualTo(0));
                Assert.That(FindEffectByCode(costEffectCode), Is.EqualTo(Entity.Null));
            }
            finally
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
                DestroyIfExists(request);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void RequestCooldownGameplayEffectWritesSelfApplyRequestWithDurationOverride()
        {
            var owner = _em.CreateEntity();
            var ability = CreateAbility(owner, level: 2);
            var request = Entity.Null;

            try
            {
                _em.AddComponentData(ability, new CAbilityCooldown
                {
                    GameplayEffectCode = 3001,
                    Cooldown = 120,
                });

                request = AbilityRuntimeActions.RequestCooldownGameplayEffect(ability, _em);
                Assert.That(request, Is.Not.EqualTo(Entity.Null));

                var effectRequest = _em.GetComponentData<CApplyGameplayEffectRequest>(request);
                Assert.That(effectRequest.GameplayEffectCode, Is.EqualTo(3001));
                Assert.That(effectRequest.SourceAsc, Is.EqualTo(owner));
                Assert.That(effectRequest.SourceAbility, Is.EqualTo(ability));
                Assert.That(effectRequest.Instigator, Is.EqualTo(owner));
                Assert.That(effectRequest.Causer, Is.EqualTo(ability));
                Assert.That(effectRequest.Level, Is.EqualTo(2));
                Assert.That(effectRequest.DurationFrameOverride, Is.EqualTo(120));

                AssertSelfTarget(request, owner, ability);
            }
            finally
            {
                DestroyIfExists(request);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void CooldownRequestDurationOverrideIsAppliedWhenRequestIsConsumed()
        {
            const int cooldownEffectCode = 93001;

            var owner = _em.CreateEntity();
            var ability = CreateAbility(owner, level: 1);
            Entity request = Entity.Null;
            Entity createdEffect = Entity.Null;

            try
            {
                _em.AddComponentData(owner, new CTagMask());
                _em.AddBuffer<BGameplayEffect>(owner);
                _em.AddComponentData(ability, new CAbilityCooldown
                {
                    GameplayEffectCode = cooldownEffectCode,
                    Cooldown = 45,
                });

                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                    id == cooldownEffectCode
                        ? new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                        {
                            new ConfDuration
                            {
                                duration = 10,
                                timeUnit = TimeUnit.Frame,
                            },
                        })
                        : null);

                request = AbilityRuntimeActions.RequestCooldownGameplayEffect(ability, _em);
                Assert.That(request, Is.Not.EqualTo(Entity.Null));

                GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>().Update();

                createdEffect = FindEffectByCode(cooldownEffectCode);
                Assert.That(createdEffect, Is.Not.EqualTo(Entity.Null));
                Assert.That(_em.Exists(request), Is.False);

                var definition = _em.GetComponentData<CDurationDefinition>(createdEffect);
                Assert.That(definition.Duration, Is.EqualTo(10));
                Assert.That(definition.TimeUnit, Is.EqualTo(TimeUnit.Frame));

                var runtime = _em.GetComponentData<CDurationRuntime>(createdEffect);
                Assert.That(runtime.ResolvedDuration, Is.EqualTo(45));
                Assert.That(runtime.ResolvedTimeUnit, Is.EqualTo(TimeUnit.Frame));

                var spec = _em.GetComponentData<CEffectSpecData>(createdEffect);
                Assert.That(spec.DurationFrameOverride, Is.EqualTo(45));

                var context = _em.GetComponentData<CEffectContext>(createdEffect);
                Assert.That(context.SourceAsc, Is.EqualTo(owner));
                Assert.That(context.TargetAsc, Is.EqualTo(owner));
                Assert.That(context.SourceAbility, Is.EqualTo(ability));
                Assert.That(context.TargetDataKind, Is.EqualTo(ETargetDataKind.Self));
            }
            finally
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
                DestroyIfExists(createdEffect);
                DestroyIfExists(request);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void RequestCostGameplayEffectAppliesSimpleInstantThroughEffectCommandStream()
        {
            const int costEffectCode = 94001;
            const int attrSetCode = 10;
            const int attributeCode = 20;

            var owner = CreateStandardAsc();
            var ability = CreateAbility(owner, level: 1);
            Entity request = Entity.Null;

            try
            {
                _em.GetBuffer<BAttribute>(owner).Add(new BAttribute
                {
                    AttrSetCode = attrSetCode,
                    Code = attributeCode,
                    BaseValue = 100,
                    CurrentValue = 100,
                });

                _em.AddComponentData(ability, new CAbilityCost
                {
                    GameplayEffectCode = costEffectCode,
                });

                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                    id == costEffectCode
                        ? new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                        {
                            new ConfModifierConfig
                            {
                                ModifierSettings = new[]
                                {
                                    new ModifierDefinitionSetting
                                    {
                                        AttrSetCode = attrSetCode,
                                        AttrCode = attributeCode,
                                        Operation = EModifierOp.Subtract,
                                        Magnitude = 25,
                                    },
                                },
                            },
                        })
                        : null);

                request = AbilityRuntimeActions.RequestCostGameplayEffect(ability, _em);
                Assert.That(request, Is.EqualTo(Entity.Null));
                Assert.That(CountApplyRequestsByCode(costEffectCode), Is.EqualTo(0));

                RunCommandGroup();

                Assert.That(CountApplyRequestsByCode(costEffectCode), Is.EqualTo(0));
                Assert.That(FindEffectByCode(costEffectCode), Is.EqualTo(Entity.Null));

                var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
                var commands = _em.GetBuffer<BEffectCommand>(streamEntity);
                var specs = _em.GetBuffer<BInstantEffectSpec>(streamEntity);
                var deltas = _em.GetBuffer<BAttributeDelta>(streamEntity);
                var facts = _em.GetBuffer<BTypedSimulationFact>(streamEntity);
                var attribute = _em.GetBuffer<BAttribute>(owner)[0];

                Assert.That(commands.Length, Is.EqualTo(1));
                Assert.That(specs.Length, Is.EqualTo(1));
                Assert.That(deltas.Length, Is.EqualTo(1));
                Assert.That(facts.Length, Is.EqualTo(1));
                Assert.That(attribute.BaseValue, Is.EqualTo(75));
                Assert.That(attribute.CurrentValue, Is.EqualTo(75));
                Assert.That(attribute.Dirty, Is.True);

                var spec = specs[0];
                var delta = deltas[0];
                var fact = facts[0];
                Assert.That(spec.SourceCommandSequence, Is.EqualTo(commands[0].Sequence));
                Assert.That(spec.GameplayEffectCode, Is.EqualTo(costEffectCode));
                Assert.That(spec.SourceAsc, Is.EqualTo(owner));
                Assert.That(spec.TargetAsc, Is.EqualTo(owner));
                Assert.That(spec.SourceAbility, Is.EqualTo(ability));
                Assert.That(delta.SourceCommandSequence, Is.EqualTo(commands[0].Sequence));
                Assert.That(delta.SourceSpecSequence, Is.EqualTo(spec.Sequence));
                Assert.That(delta.GameplayEffectCode, Is.EqualTo(costEffectCode));
                Assert.That(delta.TargetAsc, Is.EqualTo(owner));
                Assert.That(delta.SourceAbility, Is.EqualTo(ability));
                Assert.That(delta.AttrSetCode, Is.EqualTo(attrSetCode));
                Assert.That(delta.AttributeCode, Is.EqualTo(attributeCode));
                Assert.That(delta.Op, Is.EqualTo(EModifierOp.Subtract));
                Assert.That(delta.ValueKind, Is.EqualTo(EAttributeDeltaValueKind.BaseValue));
                Assert.That(delta.Magnitude, Is.EqualTo(25));
                Assert.That(delta.OldValue, Is.EqualTo(100));
                Assert.That(delta.NewValue, Is.EqualTo(75));
                Assert.That(fact.SourceCommandSequence, Is.EqualTo(commands[0].Sequence));
                Assert.That(fact.SourceSpecSequence, Is.EqualTo(spec.Sequence));
                Assert.That(fact.SourceDeltaSequence, Is.EqualTo(delta.Sequence));
                Assert.That(fact.EventType, Is.EqualTo(EGameplayEventType.AttributeBaseValueChanged));
                Assert.That(fact.Domain, Is.EqualTo(EGameplayFactDomain.Attribute));
                Assert.That(fact.Category, Is.EqualTo(EGameplayFactCategory.StateChange));
                Assert.That(fact.GameplayEffectCode, Is.EqualTo(costEffectCode));
                Assert.That(fact.OldValue, Is.EqualTo(100));
                Assert.That(fact.NewValue, Is.EqualTo(75));
            }
            finally
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
                DestroyIfExists(request);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void CooldownGameplayEffectRequestGrantsAndRemovesTagThroughEffectLifecycle()
        {
            const int cooldownEffectCode = 94002;
            const int cooldownTagIndex = 12;

            var owner = CreateStandardAsc();
            var ability = CreateAbility(owner, level: 1);
            Entity request = Entity.Null;
            Entity createdEffect = Entity.Null;

            try
            {
                _em.AddComponentData(ability, new CAbilityCooldown
                {
                    GameplayEffectCode = cooldownEffectCode,
                    Cooldown = 2,
                });

                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                    id == cooldownEffectCode
                        ? new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                        {
                            new ConfDuration
                            {
                                duration = 10,
                                timeUnit = TimeUnit.Frame,
                            },
                            new ConfGrantedTagIndex(cooldownTagIndex),
                        })
                        : null);

                request = AbilityRuntimeActions.RequestCooldownGameplayEffect(ability, _em);
                Assert.That(request, Is.Not.EqualTo(Entity.Null));

                RunCommandGroup();
                RunEffectGroup();

                createdEffect = FindEffectByCode(cooldownEffectCode);
                Assert.That(createdEffect, Is.Not.EqualTo(Entity.Null));
                Assert.That(_em.Exists(request), Is.False);
                Assert.That(_em.GetComponentData<CTagMask>(owner).HasTag(cooldownTagIndex), Is.True);
                Assert.That(HasTagChangeEvent(owner, cooldownTagIndex, added: true), Is.True);

                RunFullFrame();
                Assert.That(_em.GetComponentData<CTagMask>(owner).HasTag(cooldownTagIndex), Is.True);

                RunFullFrame();
                Assert.That(_em.HasComponent<CEffectDestroy>(createdEffect), Is.True);
                Assert.That(_em.GetComponentData<CTagMask>(owner).HasTag(cooldownTagIndex), Is.True);

                RunFullFrame();
                Assert.That(_em.Exists(createdEffect), Is.False);
                Assert.That(_em.GetComponentData<CTagMask>(owner).HasTag(cooldownTagIndex), Is.False);
                Assert.That(HasTagChangeEvent(owner, cooldownTagIndex, added: false), Is.True);
            }
            finally
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
                DestroyIfExists(createdEffect);
                DestroyIfExists(request);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void TryActivateAbilityInstantiatesCostCooldownAndActivationEffectsWithoutDirectStateMutation()
        {
            const int costEffectCode = 95001;
            const int cooldownEffectCode = 95002;
            const int activationEffectCode = 95003;
            const int attrSetCode = 30;
            const int attributeCode = 40;
            const int cooldownTagIndex = 14;

            var owner = CreateStandardAsc();
            var ability = _em.CreateEntity();

            try
            {
                _em.GetBuffer<BAttribute>(owner).Add(new BAttribute
                {
                    AttrSetCode = attrSetCode,
                    Code = attributeCode,
                    BaseValue = 100,
                    CurrentValue = 100,
                });

                _em.AddComponentData(ability, new CAbilityBaseInfo
                {
                    Code = 10002,
                    Level = 3,
                    Owner = owner,
                });
                _em.AddComponentData(ability, new CAbilityRuntimeState
                {
                    Phase = EAbilityPhase.Ready,
                });
                _em.AddComponentData(ability, new CAbilityCost
                {
                    GameplayEffectCode = costEffectCode,
                });
                var cooldownTags = new CTagMask();
                cooldownTags.AddTag(cooldownTagIndex);
                _em.AddComponentData(ability, new CAbilityCooldown
                {
                    GameplayEffectCode = cooldownEffectCode,
                    Cooldown = 30,
                });
                _em.AddBuffer<BAbilityEffectOnActivate>(ability)
                    .Add(new BAbilityEffectOnActivate { EffectCode = activationEffectCode });
                _em.AddComponentData(ability, new CAbilityConfig
                {
                    Config = CreateAbilityConfigBlob(
                        cooldownTags,
                        new[]
                        {
                            new BlobAbilityCostModifier
                            {
                                AttrSetCode = attrSetCode,
                                AttributeCode = attributeCode,
                                Op = EModifierOp.Subtract,
                                Magnitude = 25,
                            },
                        }),
                });
                _em.AddComponent<CAbilityInTryActivate>(ability);

                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                    id switch
                    {
                        costEffectCode => new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                        {
                            new ConfModifierConfig
                            {
                                ModifierSettings = new[]
                                {
                                    new ModifierDefinitionSetting
                                    {
                                        AttrSetCode = attrSetCode,
                                        AttrCode = attributeCode,
                                        Operation = EModifierOp.Subtract,
                                        Magnitude = 25,
                                    },
                                },
                            },
                        }),
                        cooldownEffectCode => new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                        {
                            new ConfDuration
                            {
                                duration = 10,
                                timeUnit = TimeUnit.Frame,
                            },
                            new ConfGrantedTagIndex(cooldownTagIndex),
                        }),
                        activationEffectCode => new GameplayEffectConfig(System.Array.Empty<GameplayEffectComponentConfig>()),
                        _ => null,
                    });

                RunCommandGroup();

                Assert.That(_em.HasComponent<CAbilityInTryActivate>(ability), Is.False);
                Assert.That(_em.HasComponent<CAbilityCommitRequest>(ability), Is.False);
                Assert.That(_em.HasComponent<CAbilityActive>(ability), Is.True);
                Assert.That(_em.GetComponentData<CAbilityRuntimeState>(ability).Phase, Is.EqualTo(EAbilityPhase.Active));
                AssertAbilityCommitEvent(
                    EGameplayEventType.AbilityCommitSucceeded,
                    ability,
                    10002,
                    AbilityActivationResult.Success);

                var attribute = _em.GetBuffer<BAttribute>(owner)[0];
                Assert.That(attribute.BaseValue, Is.EqualTo(75));
                Assert.That(attribute.CurrentValue, Is.EqualTo(75));
                Assert.That(_em.GetComponentData<CTagMask>(owner).HasTag(cooldownTagIndex), Is.False);

                Assert.That(FindEffectByCode(costEffectCode), Is.EqualTo(Entity.Null));
                Assert.That(CountApplyRequestsByCode(costEffectCode), Is.EqualTo(0));
                var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
                Assert.That(_em.GetBuffer<BEffectCommand>(streamEntity).Length, Is.EqualTo(1));
                Assert.That(_em.GetBuffer<BInstantEffectSpec>(streamEntity).Length, Is.EqualTo(1));
                Assert.That(_em.GetBuffer<BAttributeDelta>(streamEntity).Length, Is.EqualTo(1));
                Assert.That(_em.GetBuffer<BTypedSimulationFact>(streamEntity).Length, Is.EqualTo(1));
                AssertPendingEffect(cooldownEffectCode, owner, ability, expectedDuration: 30);
                AssertPendingEffect(activationEffectCode, owner, ability, expectedDuration: null);
            }
            finally
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
                DestroyEffectsByCodes(costEffectCode, cooldownEffectCode, activationEffectCode);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void TryActivateAbilityFailsCommitWhenCostIsInsufficientAndEmitsFact()
        {
            const int costEffectCode = 95011;
            const int cooldownEffectCode = 95012;
            const int attrSetCode = 31;
            const int attributeCode = 41;

            var owner = CreateStandardAsc();
            var ability = _em.CreateEntity();

            try
            {
                _em.GetBuffer<BAttribute>(owner).Add(new BAttribute
                {
                    AttrSetCode = attrSetCode,
                    Code = attributeCode,
                    BaseValue = 10,
                    CurrentValue = 10,
                });

                _em.AddComponentData(ability, new CAbilityBaseInfo
                {
                    Code = 10011,
                    Level = 1,
                    Owner = owner,
                });
                _em.AddComponentData(ability, new CAbilityRuntimeState
                {
                    Phase = EAbilityPhase.Ready,
                });
                _em.AddComponentData(ability, new CAbilityCost
                {
                    GameplayEffectCode = costEffectCode,
                });
                _em.AddComponentData(ability, new CAbilityCooldown
                {
                    GameplayEffectCode = cooldownEffectCode,
                    Cooldown = 30,
                });
                _em.AddComponentData(ability, new CAbilityConfig
                {
                    Config = CreateAbilityConfigBlob(
                        default,
                        new[]
                        {
                            new BlobAbilityCostModifier
                            {
                                AttrSetCode = attrSetCode,
                                AttributeCode = attributeCode,
                                Op = EModifierOp.Subtract,
                                Magnitude = 25,
                            },
                        }),
                });
                _em.AddComponent<CAbilityInTryActivate>(ability);

                RunCommandGroup();

                Assert.That(_em.HasComponent<CAbilityInTryActivate>(ability), Is.False);
                Assert.That(_em.HasComponent<CAbilityCommitRequest>(ability), Is.False);
                Assert.That(_em.HasComponent<CAbilityActive>(ability), Is.False);
                Assert.That(_em.GetComponentData<CAbilityRuntimeState>(ability).Phase, Is.EqualTo(EAbilityPhase.Ready));
                Assert.That(FindEffectByCode(costEffectCode), Is.EqualTo(Entity.Null));
                Assert.That(FindEffectByCode(cooldownEffectCode), Is.EqualTo(Entity.Null));
                AssertAbilityCommitEvent(
                    EGameplayEventType.AbilityCommitFailed,
                    ability,
                    10011,
                    AbilityActivationResult.FailCost);
            }
            finally
            {
                DestroyEffectsByCodes(costEffectCode, cooldownEffectCode);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void TryActivateAbilityFailsCommitWhenCooldownTagIsPresentAndEmitsFact()
        {
            const int costEffectCode = 95021;
            const int cooldownEffectCode = 95022;
            const int attrSetCode = 32;
            const int attributeCode = 42;
            const int cooldownTagIndex = 15;

            var owner = CreateStandardAsc();
            var ability = _em.CreateEntity();

            try
            {
                _em.GetBuffer<BAttribute>(owner).Add(new BAttribute
                {
                    AttrSetCode = attrSetCode,
                    Code = attributeCode,
                    BaseValue = 100,
                    CurrentValue = 100,
                });

                var ownerTags = _em.GetComponentData<CTagMask>(owner);
                ownerTags.AddTag(cooldownTagIndex);
                _em.SetComponentData(owner, ownerTags);

                _em.AddComponentData(ability, new CAbilityBaseInfo
                {
                    Code = 10021,
                    Level = 1,
                    Owner = owner,
                });
                _em.AddComponentData(ability, new CAbilityRuntimeState
                {
                    Phase = EAbilityPhase.Ready,
                });
                _em.AddComponentData(ability, new CAbilityCost
                {
                    GameplayEffectCode = costEffectCode,
                });
                _em.AddComponentData(ability, new CAbilityCooldown
                {
                    GameplayEffectCode = cooldownEffectCode,
                    Cooldown = 30,
                });

                var cooldownTags = new CTagMask();
                cooldownTags.AddTag(cooldownTagIndex);
                _em.AddComponentData(ability, new CAbilityConfig
                {
                    Config = CreateAbilityConfigBlob(
                        cooldownTags,
                        new[]
                        {
                            new BlobAbilityCostModifier
                            {
                                AttrSetCode = attrSetCode,
                                AttributeCode = attributeCode,
                                Op = EModifierOp.Subtract,
                                Magnitude = 25,
                            },
                        }),
                });
                _em.AddComponent<CAbilityInTryActivate>(ability);

                RunCommandGroup();

                Assert.That(_em.HasComponent<CAbilityInTryActivate>(ability), Is.False);
                Assert.That(_em.HasComponent<CAbilityActive>(ability), Is.False);
                Assert.That(_em.GetComponentData<CAbilityRuntimeState>(ability).Phase, Is.EqualTo(EAbilityPhase.Ready));
                Assert.That(FindEffectByCode(costEffectCode), Is.EqualTo(Entity.Null));
                Assert.That(FindEffectByCode(cooldownEffectCode), Is.EqualTo(Entity.Null));
                AssertAbilityCommitEvent(
                    EGameplayEventType.AbilityCommitFailed,
                    ability,
                    10021,
                    AbilityActivationResult.FailCooldown);
            }
            finally
            {
                DestroyEffectsByCodes(costEffectCode, cooldownEffectCode);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void TryActivateAbilityFailsCommitWhenActivationRequiredTagsAreMissingAndEmitsSpecificFact()
        {
            const int requiredTagIndex = 20;
            const int abilityCode = 10031;

            var owner = CreateStandardAsc();
            var requiredTags = new CTagMask();
            requiredTags.AddTag(requiredTagIndex);
            var ability = CreateRuntimeAbility(
                owner,
                abilityCode,
                CreateAbilityConfigBlob(
                    default,
                    System.Array.Empty<BlobAbilityCostModifier>(),
                    code: abilityCode,
                    activationRequiredTags: new TagRequirementMask { All = requiredTags }));

            try
            {
                _em.AddComponent<CAbilityInTryActivate>(ability);

                RunCommandGroup();

                Assert.That(_em.HasComponent<CAbilityInTryActivate>(ability), Is.False);
                Assert.That(_em.HasComponent<CAbilityActive>(ability), Is.False);
                Assert.That(_em.GetComponentData<CAbilityRuntimeState>(ability).Phase, Is.EqualTo(EAbilityPhase.Ready));
                AssertAbilityCommitEvent(
                    EGameplayEventType.AbilityCommitFailed,
                    ability,
                    abilityCode,
                    AbilityActivationResult.FailActivationRequiredTags);
            }
            finally
            {
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void TryActivateAbilityFailsCommitWhenActivationBlockedTagIsPresentAndEmitsSpecificFact()
        {
            const int blockedTagIndex = 21;
            const int abilityCode = 10032;

            var owner = CreateStandardAsc();
            var ownerTags = _em.GetComponentData<CTagMask>(owner);
            ownerTags.AddTag(blockedTagIndex);
            _em.SetComponentData(owner, ownerTags);

            var blockedTags = new CTagMask();
            blockedTags.AddTag(blockedTagIndex);
            var ability = CreateRuntimeAbility(
                owner,
                abilityCode,
                CreateAbilityConfigBlob(
                    default,
                    System.Array.Empty<BlobAbilityCostModifier>(),
                    code: abilityCode,
                    activationBlockedTags: new TagRequirementMask { None = blockedTags }));

            try
            {
                _em.AddComponent<CAbilityInTryActivate>(ability);

                RunCommandGroup();

                Assert.That(_em.HasComponent<CAbilityInTryActivate>(ability), Is.False);
                Assert.That(_em.HasComponent<CAbilityActive>(ability), Is.False);
                Assert.That(_em.GetComponentData<CAbilityRuntimeState>(ability).Phase, Is.EqualTo(EAbilityPhase.Ready));
                AssertAbilityCommitEvent(
                    EGameplayEventType.AbilityCommitFailed,
                    ability,
                    abilityCode,
                    AbilityActivationResult.FailActivationBlockedTags);
            }
            finally
            {
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void TryActivateAbilityFailsCommitWhenBlockedByActiveAbilityAndEmitsBlockFact()
        {
            const int blockedAssetTagIndex = 22;
            const int attemptedAbilityCode = 10033;
            const int blockerAbilityCode = 10034;

            var owner = CreateStandardAsc();
            var blockedAssetTags = new CTagMask();
            blockedAssetTags.AddTag(blockedAssetTagIndex);
            var attempted = CreateRuntimeAbility(
                owner,
                attemptedAbilityCode,
                CreateAbilityConfigBlob(
                    default,
                    System.Array.Empty<BlobAbilityCostModifier>(),
                    code: attemptedAbilityCode,
                    assetTags: blockedAssetTags));

            var blockTags = new CTagMask();
            blockTags.AddTag(blockedAssetTagIndex);
            var blocker = CreateRuntimeAbility(
                owner,
                blockerAbilityCode,
                CreateAbilityConfigBlob(
                    default,
                    System.Array.Empty<BlobAbilityCostModifier>(),
                    code: blockerAbilityCode,
                    blockAbilityTags: blockTags),
                EAbilityPhase.Active);

            try
            {
                _em.AddComponent<CAbilityActive>(blocker);
                var granted = _em.GetBuffer<BGrantedAbility>(owner);
                granted.Add(new BGrantedAbility { AbilityEntity = attempted });
                granted.Add(new BGrantedAbility { AbilityEntity = blocker });
                _em.AddComponent<CAbilityInTryActivate>(attempted);

                RunCommandGroup();

                Assert.That(_em.HasComponent<CAbilityInTryActivate>(attempted), Is.False);
                Assert.That(_em.HasComponent<CAbilityActive>(attempted), Is.False);
                Assert.That(_em.GetComponentData<CAbilityRuntimeState>(attempted).Phase, Is.EqualTo(EAbilityPhase.Ready));
                AssertAbilityCommitEvent(
                    EGameplayEventType.AbilityCommitFailed,
                    attempted,
                    attemptedAbilityCode,
                    AbilityActivationResult.FailBlockedByActiveAbility);
                AssertGameplayEvent(
                    EGameplayEventType.AbilityActivationBlockedByAbility,
                    attempted,
                    attemptedAbilityCode,
                    blockerAbilityCode);
            }
            finally
            {
                DestroyIfExists(attempted);
                DestroyIfExists(blocker);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void TryActivateAbilityCancelsMatchedActiveAbilitiesAndEmitsRequestFact()
        {
            const int cancelTagIndex = 23;
            const int triggerAbilityCode = 10035;
            const int canceledAbilityCode = 10036;

            var owner = CreateStandardAsc();
            var cancelTags = new CTagMask();
            cancelTags.AddTag(cancelTagIndex);
            var trigger = CreateRuntimeAbility(
                owner,
                triggerAbilityCode,
                CreateAbilityConfigBlob(
                    default,
                    System.Array.Empty<BlobAbilityCostModifier>(),
                    code: triggerAbilityCode,
                    cancelAbilityTags: cancelTags));

            var canceledAssetTags = new CTagMask();
            canceledAssetTags.AddTag(cancelTagIndex);
            var canceled = CreateRuntimeAbility(
                owner,
                canceledAbilityCode,
                CreateAbilityConfigBlob(
                    default,
                    System.Array.Empty<BlobAbilityCostModifier>(),
                    code: canceledAbilityCode,
                    assetTags: canceledAssetTags),
                EAbilityPhase.Active);

            try
            {
                _em.AddComponent<CAbilityActive>(canceled);
                var granted = _em.GetBuffer<BGrantedAbility>(owner);
                granted.Add(new BGrantedAbility { AbilityEntity = trigger });
                granted.Add(new BGrantedAbility { AbilityEntity = canceled });
                _em.AddComponent<CAbilityInTryActivate>(trigger);

                RunCommandGroup();

                Assert.That(_em.HasComponent<CAbilityActive>(trigger), Is.True);
                Assert.That(_em.HasComponent<CAbilityInTryCancel>(canceled), Is.True);
                var cancelRequest = _em.GetComponentData<CAbilityInTryCancel>(canceled);
                Assert.That(cancelRequest.Reason, Is.EqualTo(EAbilityLifecycleReason.CancelMatchedAbility));
                Assert.That(cancelRequest.SourceAbility, Is.EqualTo(trigger));
                Assert.That(cancelRequest.SourceAbilityCode, Is.EqualTo(triggerAbilityCode));
                AssertAbilityCommitEvent(
                    EGameplayEventType.AbilityCommitSucceeded,
                    trigger,
                    triggerAbilityCode,
                    AbilityActivationResult.Success);
                var requestEvent = AssertGameplayEvent(
                    EGameplayEventType.AbilityCancelRequested,
                    canceled,
                    canceledAbilityCode,
                    triggerAbilityCode);
                Assert.That(requestEvent.ReasonCode, Is.EqualTo((int)EAbilityLifecycleReason.CancelMatchedAbility));
                Assert.That(requestEvent.RelatedAbility, Is.EqualTo(trigger));
                Assert.That(requestEvent.RelatedAbilityCode, Is.EqualTo(triggerAbilityCode));

                RunAbilityGroup();

                Assert.That(_em.HasComponent<CAbilityActive>(canceled), Is.False);
                Assert.That(_em.GetComponentData<CAbilityRuntimeState>(canceled).Phase, Is.EqualTo(EAbilityPhase.Ready));
                var canceledEvent = AssertGameplayEvent(
                    EGameplayEventType.AbilityCanceled,
                    canceled,
                    canceledAbilityCode,
                    triggerAbilityCode);
                Assert.That(canceledEvent.ReasonCode, Is.EqualTo((int)EAbilityLifecycleReason.CancelMatchedAbility));
                Assert.That(canceledEvent.RelatedAbility, Is.EqualTo(trigger));
                Assert.That(canceledEvent.RelatedAbilityCode, Is.EqualTo(triggerAbilityCode));
            }
            finally
            {
                DestroyIfExists(trigger);
                DestroyIfExists(canceled);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void ExplicitEndCleanupEmitsAbilityCodeAndReason()
        {
            const int abilityCode = 10037;

            var owner = CreateStandardAsc();
            var ability = CreateRuntimeAbility(
                owner,
                abilityCode,
                CreateAbilityConfigBlob(default, System.Array.Empty<BlobAbilityCostModifier>(), code: abilityCode),
                EAbilityPhase.Active);

            try
            {
                _em.AddComponent<CAbilityActive>(ability);
                AbilityRuntimeActions.RequestAbilityEnd(ability, _em, EAbilityLifecycleReason.ExplicitEnd);

                var requestEvent = AssertGameplayEvent(
                    EGameplayEventType.AbilityEndRequested,
                    ability,
                    abilityCode,
                    0);
                Assert.That(requestEvent.ReasonCode, Is.EqualTo((int)EAbilityLifecycleReason.ExplicitEnd));
                Assert.That(requestEvent.RelatedAbility, Is.EqualTo(Entity.Null));

                RunAbilityGroup();

                Assert.That(_em.HasComponent<CAbilityActive>(ability), Is.False);
                Assert.That(_em.GetComponentData<CAbilityRuntimeState>(ability).Phase, Is.EqualTo(EAbilityPhase.Ready));

                var endedEvent = AssertGameplayEvent(
                    EGameplayEventType.AbilityEnded,
                    ability,
                    abilityCode,
                    0);
                Assert.That(endedEvent.TargetAsc, Is.EqualTo(owner));
                Assert.That(endedEvent.ReasonCode, Is.EqualTo((int)EAbilityLifecycleReason.ExplicitEnd));
                Assert.That(endedEvent.RelatedAbility, Is.EqualTo(Entity.Null));
                Assert.That(endedEvent.RelatedAbilityCode, Is.EqualTo(0));
            }
            finally
            {
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void ExplicitCancelCleanupEmitsAbilityCodeAndReason()
        {
            const int abilityCode = 10038;

            var owner = CreateStandardAsc();
            var ability = CreateRuntimeAbility(
                owner,
                abilityCode,
                CreateAbilityConfigBlob(default, System.Array.Empty<BlobAbilityCostModifier>(), code: abilityCode),
                EAbilityPhase.Active);

            try
            {
                _em.AddComponent<CAbilityActive>(ability);
                AbilityRuntimeActions.RequestAbilityCancel(ability, _em, EAbilityLifecycleReason.ExplicitCancel);

                var requestEvent = AssertGameplayEvent(
                    EGameplayEventType.AbilityCancelRequested,
                    ability,
                    abilityCode,
                    0);
                Assert.That(requestEvent.ReasonCode, Is.EqualTo((int)EAbilityLifecycleReason.ExplicitCancel));
                Assert.That(requestEvent.RelatedAbility, Is.EqualTo(Entity.Null));

                RunAbilityGroup();

                Assert.That(_em.HasComponent<CAbilityActive>(ability), Is.False);
                Assert.That(_em.GetComponentData<CAbilityRuntimeState>(ability).Phase, Is.EqualTo(EAbilityPhase.Ready));

                var canceledEvent = AssertGameplayEvent(
                    EGameplayEventType.AbilityCanceled,
                    ability,
                    abilityCode,
                    0);
                Assert.That(canceledEvent.TargetAsc, Is.EqualTo(owner));
                Assert.That(canceledEvent.ReasonCode, Is.EqualTo((int)EAbilityLifecycleReason.ExplicitCancel));
                Assert.That(canceledEvent.RelatedAbility, Is.EqualTo(Entity.Null));
                Assert.That(canceledEvent.RelatedAbilityCode, Is.EqualTo(0));
            }
            finally
            {
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void EndingPhaseLifecycleRequestEmitsLifetimeExpiredReason()
        {
            const int abilityCode = 10039;

            var owner = CreateStandardAsc();
            var ability = CreateRuntimeAbility(
                owner,
                abilityCode,
                CreateAbilityConfigBlob(default, System.Array.Empty<BlobAbilityCostModifier>(), code: abilityCode),
                EAbilityPhase.Ending);

            try
            {
                _em.AddComponent<CAbilityActive>(ability);

                RunAbilityGroup();

                Assert.That(_em.HasComponent<CAbilityInTryEnd>(ability), Is.False);
                Assert.That(_em.HasComponent<CAbilityActive>(ability), Is.False);
                Assert.That(_em.GetComponentData<CAbilityRuntimeState>(ability).Phase, Is.EqualTo(EAbilityPhase.Ready));

                var endedEvent = AssertGameplayEvent(
                    EGameplayEventType.AbilityEnded,
                    ability,
                    abilityCode,
                    abilityCode);
                Assert.That(endedEvent.ReasonCode, Is.EqualTo((int)EAbilityLifecycleReason.LifetimeExpired));
                Assert.That(endedEvent.RelatedAbility, Is.EqualTo(ability));
                Assert.That(endedEvent.RelatedAbilityCode, Is.EqualTo(abilityCode));
            }
            finally
            {
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void AbilityFactoryBuildsCostAndCooldownCacheFromGameplayEffectDefinitionsWithoutProtoEffects()
        {
            const int abilityCode = 96000;
            const int costEffectCode = 96001;
            const int cooldownEffectCode = 96002;
            const int attrSetCode = 60;
            const int attributeCode = 70;
            const int cooldownTagCode = 96003;

            var ability = Entity.Null;

            try
            {
                TagHelper.InitTagMap(
                    new Dictionary<int, GameplayTag>
                    {
                        [cooldownTagCode] = new GameplayTag(cooldownTagCode, System.Array.Empty<int>(), System.Array.Empty<int>()),
                    },
                    new Dictionary<int, string>
                    {
                        [cooldownTagCode] = "Cooldown.Test",
                    });

                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                    id switch
                    {
                        costEffectCode => new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                        {
                            new DirectModifierComponentConfig(
                                attrSetCode,
                                attributeCode,
                                EModifierOp.Subtract,
                                magnitude: 15),
                        }),
                        cooldownEffectCode => new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                        {
                            new ConfEffectGrantedTags
                            {
                                tags = new[] { cooldownTagCode },
                            },
                        }),
                        _ => null,
                    });

                var modifierEntityCountBefore = CountEntitiesWith(ComponentType.ReadOnly<BModifierConfig>());
                var prototypeEntityCountBefore = CountEntitiesWith(ComponentType.ReadOnly<CGameplayEffectPrototype>());
                ability = AbilityEntityFactory.CreateAbilityEntity(new AbilityConfig(new AbilityComponentConfig[]
                {
                    new ConfAbilityBaseInfo
                    {
                        Code = abilityCode,
                        Level = 1,
                    },
                    new ConfAbilityCost
                    {
                        GameplayEffectCode = costEffectCode,
                    },
                    new ConfAbilityCooldown
                    {
                        Cooldown = 45,
                        GameplayEffectCode = cooldownEffectCode,
                    },
                }));
                var modifierEntityCountAfter = CountEntitiesWith(ComponentType.ReadOnly<BModifierConfig>());
                var prototypeEntityCountAfter = CountEntitiesWith(ComponentType.ReadOnly<CGameplayEffectPrototype>());

                Assert.That(modifierEntityCountAfter, Is.EqualTo(modifierEntityCountBefore));
                Assert.That(prototypeEntityCountAfter, Is.EqualTo(prototypeEntityCountBefore));
                Assert.That(GameplayEffectConfigRegistry.CachedPrototypeCount, Is.EqualTo(0));
                Assert.That(GameplayEffectConfigRegistry.CachedStaticDefinitionBlobCount, Is.EqualTo(2));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(costEffectCode, out _),
                    Is.True);
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(cooldownEffectCode, out _),
                    Is.True);

                var cost = _em.GetComponentData<CAbilityCost>(ability);
                Assert.That(cost.GameplayEffectCode, Is.EqualTo(costEffectCode));

                var cooldown = _em.GetComponentData<CAbilityCooldown>(ability);
                Assert.That(cooldown.GameplayEffectCode, Is.EqualTo(cooldownEffectCode));
                Assert.That(cooldown.Cooldown, Is.EqualTo(45));

                var config = _em.GetComponentData<CAbilityConfig>(ability).Config;
                Assert.That(config.IsCreated, Is.True);
                ref var blob = ref config.Value;
                Assert.That(blob.CostModifiers.Length, Is.EqualTo(1));
                Assert.That(blob.CostModifiers[0].AttrSetCode, Is.EqualTo(attrSetCode));
                Assert.That(blob.CostModifiers[0].AttributeCode, Is.EqualTo(attributeCode));
                Assert.That(blob.CostModifiers[0].Op, Is.EqualTo(EModifierOp.Subtract));
                Assert.That(blob.CostModifiers[0].Magnitude, Is.EqualTo(15));
                Assert.That(blob.CooldownTags.HasTag(0), Is.True);
                Assert.That(blob.Cooldown, Is.EqualTo(45));
            }
            finally
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
                DestroyIfExists(ability);
            }
        }

        [Test]
        public void AbilityFactoryFallsBackToEmptyCostAndCooldownSummaryWhenReferencedGameplayEffectsAreMissing()
        {
            const int abilityCode = 96010;
            const int missingCostEffectCode = 96011;
            const int missingCooldownEffectCode = 96012;

            var ability = Entity.Null;

            try
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(_ => null);

                ability = AbilityEntityFactory.CreateAbilityEntity(new AbilityConfig(new AbilityComponentConfig[]
                {
                    new ConfAbilityBaseInfo
                    {
                        Code = abilityCode,
                        Level = 1,
                    },
                    new ConfAbilityCost
                    {
                        GameplayEffectCode = missingCostEffectCode,
                    },
                    new ConfAbilityCooldown
                    {
                        Cooldown = 30,
                        GameplayEffectCode = missingCooldownEffectCode,
                    },
                }));

                Assert.That(GameplayEffectConfigRegistry.CachedPrototypeCount, Is.EqualTo(0));
                Assert.That(GameplayEffectConfigRegistry.CachedStaticDefinitionBlobCount, Is.EqualTo(0));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(missingCostEffectCode, out _),
                    Is.False);
                Assert.That(
                    GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(missingCooldownEffectCode, out _),
                    Is.False);

                var cost = _em.GetComponentData<CAbilityCost>(ability);
                Assert.That(cost.GameplayEffectCode, Is.EqualTo(missingCostEffectCode));

                var cooldown = _em.GetComponentData<CAbilityCooldown>(ability);
                Assert.That(cooldown.GameplayEffectCode, Is.EqualTo(missingCooldownEffectCode));
                Assert.That(cooldown.Cooldown, Is.EqualTo(30));

                var config = _em.GetComponentData<CAbilityConfig>(ability).Config;
                Assert.That(config.IsCreated, Is.True);
                ref var blob = ref config.Value;
                Assert.That(blob.CostModifiers.Length, Is.EqualTo(0));
                Assert.That(blob.CooldownTags.IsEmpty, Is.True);
                Assert.That(blob.Cooldown, Is.EqualTo(30));

                Assert.That(ConfigRegistryDiagnostics.Count, Is.EqualTo(2));
                AssertMissingConfigDiagnostic(
                    ConfigRegistryDiagnostics.Entries[0],
                    ConfigRegistryConfigKind.GameplayEffect,
                    missingCooldownEffectCode,
                    ConfigRegistryConfigKind.Ability,
                    abilityCode,
                    ConfigRegistryReferenceKind.AbilityCooldown);
                AssertMissingConfigDiagnostic(
                    ConfigRegistryDiagnostics.Entries[1],
                    ConfigRegistryConfigKind.GameplayEffect,
                    missingCostEffectCode,
                    ConfigRegistryConfigKind.Ability,
                    abilityCode,
                    ConfigRegistryReferenceKind.AbilityCost);
            }
            finally
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
                DestroyIfExists(ability);
            }
        }

        [Test]
        public void DiagnosticsSnapshotIsStableAndRegistryScopedClearPreservesUnrelatedEntries()
        {
            const int abilityCode = 96020;
            const int timelineId = 96021;
            const int missingEffectCode = 96022;
            const int missingCueCode = 96023;

            try
            {
                ConfigRegistryDiagnostics.SetMissingConfigSeverityPolicy(ConfigRegistryDiagnosticSeverity.Error);
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(_ => null);
                GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(_ => null);

                GameplayEffectConfigRegistry.GetConfigByID(
                    missingEffectCode,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.Ability,
                        abilityCode,
                        ConfigRegistryReferenceKind.AbilityCost));
                GameplayCueConfigRegistry.GetConfigByID(
                    missingCueCode,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.TimelineAbility,
                        timelineId,
                        ConfigRegistryReferenceKind.TimelineCuePreset));

                var snapshot = ConfigRegistryDiagnostics.Snapshot();
                Assert.That(snapshot.Length, Is.EqualTo(2));
                AssertHasMissingConfigDiagnostic(
                    snapshot,
                    ConfigRegistryConfigKind.GameplayEffect,
                    missingEffectCode,
                    ConfigRegistryConfigKind.Ability,
                    abilityCode,
                    ConfigRegistryReferenceKind.AbilityCost,
                    ConfigRegistryDiagnosticSeverity.Error);
                AssertHasMissingConfigDiagnostic(
                    snapshot,
                    ConfigRegistryConfigKind.GameplayCue,
                    missingCueCode,
                    ConfigRegistryConfigKind.TimelineAbility,
                    timelineId,
                    ConfigRegistryReferenceKind.TimelineCuePreset,
                    ConfigRegistryDiagnosticSeverity.Error);

                ConfigRegistryDiagnostics.ClearForConfigKind(ConfigRegistryConfigKind.GameplayEffect);

                Assert.That(ConfigRegistryDiagnostics.Count, Is.EqualTo(1));
                AssertHasMissingConfigDiagnostic(
                    ConfigRegistryDiagnostics.Snapshot(),
                    ConfigRegistryConfigKind.GameplayCue,
                    missingCueCode,
                    ConfigRegistryConfigKind.TimelineAbility,
                    timelineId,
                    ConfigRegistryReferenceKind.TimelineCuePreset,
                    ConfigRegistryDiagnosticSeverity.Error);
                Assert.That(snapshot.Length, Is.EqualTo(2));
            }
            finally
            {
                ConfigRegistryDiagnostics.ResetSeverityPolicy();
                ConfigRegistryDiagnostics.Clear();
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
                GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(null);
            }
        }

        [Test]
        public void ConfigGraphValidatorRecordsMissingCrossConfigReferences()
        {
            const int abilityCode = 96030;
            const int effectCode = 96031;
            const int timelineId = 96032;
            const int missingCostEffectCode = 96033;
            const int missingCooldownEffectCode = 96034;
            const int missingActivationEffectCode = 96035;
            const int missingTimelineEffectCode = 96036;
            const int missingCueCode = 96037;
            const int missingGrantedAbilityCode = 96038;
            const int missingPeriodEffectCode = 96039;
            const int missingOverflowEffectCode = 96040;

            var timeline = new XParamTimeline(
                timelineId,
                "Diagnostics Timeline",
                lifeTime: 10,
                manualEndAbility: false,
                tracks: new List<Track>
                {
                    new()
                    {
                        Name = "Main",
                        ActionClips = new List<TimelineActionClipData>
                        {
                            new()
                            {
                                Name = "ApplyMissingEffect",
                                StartTime = 1,
                                EndTime = 2,
                                ActionType = "ApplyEffects",
                                Parameter = new XParamApplyEffects(new[] { missingTimelineEffectCode }),
                            },
                            new()
                            {
                                Name = "PlayMissingCue",
                                StartTime = 2,
                                EndTime = 3,
                                ActionType = "PlayCuePreset",
                                Parameter = new XParamCueList(new[] { missingCueCode }),
                            },
                        },
                    },
                });

            try
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(_ => null);
                AbilityConfigRegistry.RegisterGetConfigByIDFunc(_ => null);
                GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(_ => null);
                TimelineAbilityConfigRegistry.RegisterGetConfigByIDFunc(id => id == timelineId ? timeline : null);
                ConfigRegistryDiagnostics.Clear();

                var abilityDiagnostics = ConfigRegistryGraphValidator.ValidateAbilityConfig(
                    abilityCode,
                    new AbilityConfig(new AbilityComponentConfig[]
                    {
                        new ConfAbilityBaseInfo
                        {
                            Code = abilityCode,
                            Level = 1,
                        },
                        new ConfAbilityCost
                        {
                            GameplayEffectCode = missingCostEffectCode,
                        },
                        new ConfAbilityCooldown
                        {
                            Cooldown = 30,
                            GameplayEffectCode = missingCooldownEffectCode,
                        },
                        new ConfAbilityEffectsOnActivate
                        {
                            EffectCodes = new[] { missingActivationEffectCode },
                        },
                        new ConfAbilityTimelineRef
                        {
                            TimelineId = timelineId,
                        },
                    }));
                var effectDiagnostics = ConfigRegistryGraphValidator.ValidateGameplayEffectConfig(
                    effectCode,
                    new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                    {
                        new ConfGrantedAbilityConfig
                        {
                            GrantedAbilities = new[]
                            {
                                new GrantedAbilityConfigSetting
                                {
                                    AbilityCode = missingGrantedAbilityCode,
                                    Level = 1,
                                },
                            },
                        },
                        new ConfPeriod
                        {
                            Period = 1,
                            GameplayEffectCodes = new[] { missingPeriodEffectCode },
                        },
                        new ConfStacking
                        {
                            LimitCount = 1,
                            OverflowEffectCodes = new[] { missingOverflowEffectCode },
                        },
                    }));

                var snapshot = ConfigRegistryDiagnostics.Snapshot();
                Assert.That(abilityDiagnostics, Is.EqualTo(5));
                Assert.That(effectDiagnostics, Is.EqualTo(3));
                Assert.That(snapshot.Length, Is.EqualTo(8));
                AssertHasMissingConfigDiagnostic(
                    snapshot,
                    ConfigRegistryConfigKind.GameplayEffect,
                    missingCostEffectCode,
                    ConfigRegistryConfigKind.Ability,
                    abilityCode,
                    ConfigRegistryReferenceKind.AbilityCost);
                AssertHasMissingConfigDiagnostic(
                    snapshot,
                    ConfigRegistryConfigKind.GameplayEffect,
                    missingCooldownEffectCode,
                    ConfigRegistryConfigKind.Ability,
                    abilityCode,
                    ConfigRegistryReferenceKind.AbilityCooldown);
                AssertHasMissingConfigDiagnostic(
                    snapshot,
                    ConfigRegistryConfigKind.GameplayEffect,
                    missingActivationEffectCode,
                    ConfigRegistryConfigKind.Ability,
                    abilityCode,
                    ConfigRegistryReferenceKind.AbilityActivationEffect);
                AssertHasMissingConfigDiagnostic(
                    snapshot,
                    ConfigRegistryConfigKind.GameplayEffect,
                    missingTimelineEffectCode,
                    ConfigRegistryConfigKind.TimelineAbility,
                    timelineId,
                    ConfigRegistryReferenceKind.TimelineApplyEffect);
                AssertHasMissingConfigDiagnostic(
                    snapshot,
                    ConfigRegistryConfigKind.GameplayCue,
                    missingCueCode,
                    ConfigRegistryConfigKind.TimelineAbility,
                    timelineId,
                    ConfigRegistryReferenceKind.TimelineCuePreset);
                AssertHasMissingConfigDiagnostic(
                    snapshot,
                    ConfigRegistryConfigKind.Ability,
                    missingGrantedAbilityCode,
                    ConfigRegistryConfigKind.GameplayEffect,
                    effectCode,
                    ConfigRegistryReferenceKind.GameplayEffectGrantedAbility);
                AssertHasMissingConfigDiagnostic(
                    snapshot,
                    ConfigRegistryConfigKind.GameplayEffect,
                    missingPeriodEffectCode,
                    ConfigRegistryConfigKind.GameplayEffect,
                    effectCode,
                    ConfigRegistryReferenceKind.GameplayEffectPeriodEffect);
                AssertHasMissingConfigDiagnostic(
                    snapshot,
                    ConfigRegistryConfigKind.GameplayEffect,
                    missingOverflowEffectCode,
                    ConfigRegistryConfigKind.GameplayEffect,
                    effectCode,
                    ConfigRegistryReferenceKind.GameplayEffectOverflowEffect);
            }
            finally
            {
                ConfigRegistryDiagnostics.Clear();
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
                AbilityConfigRegistry.RegisterGetConfigByIDFunc(null);
                GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(null);
                TimelineAbilityConfigRegistry.RegisterGetConfigByIDFunc(null);
            }
        }

        [Test]
        public void ConfigGraphWarmupValidatesBatchedConfigIdsAndClearsStaleDiagnostics()
        {
            const int abilityCode = 96050;
            const int effectCode = 96051;
            const int timelineId = 96052;
            const int staleCueCode = 96053;
            const int missingAbilityCode = 96054;
            const int missingCostEffectCode = 96055;
            const int missingCooldownEffectCode = 96056;
            const int missingTimelineEffectCode = 96057;
            const int missingCueCode = 96058;
            const int missingGrantedAbilityCode = 96059;
            const int missingPeriodEffectCode = 96060;

            var timeline = new XParamTimeline(
                timelineId,
                "Warmup Timeline",
                lifeTime: 10,
                manualEndAbility: false,
                tracks: new List<Track>
                {
                    new()
                    {
                        Name = "WarmupTrack",
                        ActionClips = new List<TimelineActionClipData>
                        {
                            new()
                            {
                                Name = "WarmupApplyMissingEffect",
                                StartTime = 1,
                                EndTime = 2,
                                ActionType = "ApplyEffects",
                                Parameter = new XParamApplyEffects(new[] { missingTimelineEffectCode }),
                            },
                            new()
                            {
                                Name = "WarmupPlayMissingCue",
                                StartTime = 2,
                                EndTime = 3,
                                ActionType = "PlayCuePreset",
                                Parameter = new XParamCueList(new[] { missingCueCode }),
                            },
                        },
                    },
                });

            var abilityConfig = new AbilityConfig(new AbilityComponentConfig[]
            {
                new ConfAbilityBaseInfo
                {
                    Code = abilityCode,
                    Level = 1,
                },
                new ConfAbilityCost
                {
                    GameplayEffectCode = missingCostEffectCode,
                },
                new ConfAbilityCooldown
                {
                    Cooldown = 30,
                    GameplayEffectCode = missingCooldownEffectCode,
                },
                new ConfAbilityTimelineRef
                {
                    TimelineId = timelineId,
                },
            });

            var effectConfig = new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfGrantedAbilityConfig
                {
                    GrantedAbilities = new[]
                    {
                        new GrantedAbilityConfigSetting
                        {
                            AbilityCode = missingGrantedAbilityCode,
                            Level = 1,
                        },
                    },
                },
                new ConfPeriod
                {
                    Period = 1,
                    GameplayEffectCodes = new[] { missingPeriodEffectCode },
                },
            });

            try
            {
                AbilityConfigRegistry.RegisterGetConfigByIDFunc(id => id == abilityCode ? abilityConfig : null);
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id => id == effectCode ? effectConfig : null);
                GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(_ => null);
                TimelineAbilityConfigRegistry.RegisterGetConfigByIDFunc(id => id == timelineId ? timeline : null);

                GameplayCueConfigRegistry.GetConfigByID(
                    staleCueCode,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.Ability,
                        abilityCode,
                        ConfigRegistryReferenceKind.AbilityCuePreset));
                Assert.That(ConfigRegistryDiagnostics.Count, Is.EqualTo(1));

                var result = ConfigRegistryGraphValidator.Warmup(
                    new[] { abilityCode, abilityCode, missingAbilityCode, 0 },
                    new[] { effectCode, effectCode },
                    new[] { timelineId, timelineId },
                    clearDiagnostics: true);

                Assert.That(result.AbilityConfigCount, Is.EqualTo(2));
                Assert.That(result.GameplayEffectConfigCount, Is.EqualTo(1));
                Assert.That(result.TimelineConfigCount, Is.EqualTo(1));
                Assert.That(result.TotalConfigCount, Is.EqualTo(4));
                Assert.That(result.AbilityDiagnosticCount, Is.EqualTo(5));
                Assert.That(result.GameplayEffectDiagnosticCount, Is.EqualTo(2));
                Assert.That(result.TimelineDiagnosticCount, Is.EqualTo(0));
                Assert.That(result.NewDiagnosticCount, Is.EqualTo(7));
                Assert.That(result.Diagnostics.Length, Is.EqualTo(7));
                Assert.That(ConfigRegistryDiagnostics.Count, Is.EqualTo(7));
                Assert.That(result.Diagnostics, Is.Not.SameAs(ConfigRegistryDiagnostics.Entries));

                AssertHasMissingConfigDiagnostic(
                    result.Diagnostics,
                    ConfigRegistryConfigKind.Ability,
                    missingAbilityCode,
                    ConfigRegistryConfigKind.None,
                    0,
                    ConfigRegistryReferenceKind.Direct);
                AssertHasMissingConfigDiagnostic(
                    result.Diagnostics,
                    ConfigRegistryConfigKind.GameplayEffect,
                    missingCostEffectCode,
                    ConfigRegistryConfigKind.Ability,
                    abilityCode,
                    ConfigRegistryReferenceKind.AbilityCost);
                AssertHasMissingConfigDiagnostic(
                    result.Diagnostics,
                    ConfigRegistryConfigKind.GameplayEffect,
                    missingCooldownEffectCode,
                    ConfigRegistryConfigKind.Ability,
                    abilityCode,
                    ConfigRegistryReferenceKind.AbilityCooldown);
                AssertHasMissingConfigDiagnostic(
                    result.Diagnostics,
                    ConfigRegistryConfigKind.GameplayEffect,
                    missingTimelineEffectCode,
                    ConfigRegistryConfigKind.TimelineAbility,
                    timelineId,
                    ConfigRegistryReferenceKind.TimelineApplyEffect);
                AssertHasMissingConfigDiagnostic(
                    result.Diagnostics,
                    ConfigRegistryConfigKind.GameplayCue,
                    missingCueCode,
                    ConfigRegistryConfigKind.TimelineAbility,
                    timelineId,
                    ConfigRegistryReferenceKind.TimelineCuePreset);
                AssertHasMissingConfigDiagnostic(
                    result.Diagnostics,
                    ConfigRegistryConfigKind.Ability,
                    missingGrantedAbilityCode,
                    ConfigRegistryConfigKind.GameplayEffect,
                    effectCode,
                    ConfigRegistryReferenceKind.GameplayEffectGrantedAbility);
                AssertHasMissingConfigDiagnostic(
                    result.Diagnostics,
                    ConfigRegistryConfigKind.GameplayEffect,
                    missingPeriodEffectCode,
                    ConfigRegistryConfigKind.GameplayEffect,
                    effectCode,
                    ConfigRegistryReferenceKind.GameplayEffectPeriodEffect);
            }
            finally
            {
                ConfigRegistryDiagnostics.Clear();
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
                AbilityConfigRegistry.RegisterGetConfigByIDFunc(null);
                GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(null);
                TimelineAbilityConfigRegistry.RegisterGetConfigByIDFunc(null);
            }
        }

        private static void AssertMissingConfigDiagnostic(
            ConfigRegistryDiagnostic diagnostic,
            ConfigRegistryConfigKind missingKind,
            int missingCode,
            ConfigRegistryConfigKind sourceKind,
            int sourceCode,
            ConfigRegistryReferenceKind referenceKind)
        {
            Assert.That(diagnostic.Severity, Is.EqualTo(ConfigRegistryDiagnosticSeverity.Warning));
            Assert.That(diagnostic.Code, Is.EqualTo(ConfigRegistryDiagnosticCode.MissingConfig));
            Assert.That(diagnostic.MissingConfigKind, Is.EqualTo(missingKind));
            Assert.That(diagnostic.MissingConfigCode, Is.EqualTo(missingCode));
            Assert.That(diagnostic.SourceConfigKind, Is.EqualTo(sourceKind));
            Assert.That(diagnostic.SourceConfigCode, Is.EqualTo(sourceCode));
            Assert.That(diagnostic.ReferenceKind, Is.EqualTo(referenceKind));
            Assert.That(diagnostic.Message, Does.Contain(missingKind.ToString()));
            Assert.That(diagnostic.Message, Does.Contain(missingCode.ToString()));
        }

        private static void AssertHasMissingConfigDiagnostic(
            IReadOnlyList<ConfigRegistryDiagnostic> diagnostics,
            ConfigRegistryConfigKind missingKind,
            int missingCode,
            ConfigRegistryConfigKind sourceKind,
            int sourceCode,
            ConfigRegistryReferenceKind referenceKind,
            ConfigRegistryDiagnosticSeverity severity = ConfigRegistryDiagnosticSeverity.Warning)
        {
            for (var i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                if (diagnostic.Severity == severity
                    && diagnostic.Code == ConfigRegistryDiagnosticCode.MissingConfig
                    && diagnostic.MissingConfigKind == missingKind
                    && diagnostic.MissingConfigCode == missingCode
                    && diagnostic.SourceConfigKind == sourceKind
                    && diagnostic.SourceConfigCode == sourceCode
                    && diagnostic.ReferenceKind == referenceKind)
                {
                    Assert.That(diagnostic.Message, Does.Contain(missingKind.ToString()));
                    Assert.That(diagnostic.Message, Does.Contain(missingCode.ToString()));
                    return;
                }
            }

            Assert.Fail(
                $"Missing diagnostic not found: missing={missingKind}:{missingCode}, source={sourceKind}:{sourceCode}, reference={referenceKind}, severity={severity}.");
        }

        private Entity CreateAbility(Entity owner, int level)
        {
            var ability = _em.CreateEntity();
            _em.AddComponentData(ability, new CAbilityBaseInfo
            {
                Code = 10001,
                Level = level,
                Owner = owner,
            });
            return ability;
        }

        private Entity CreateRuntimeAbility(
            Entity owner,
            int abilityCode,
            BlobAssetReference<BlobAbilityConfig> config,
            EAbilityPhase phase = EAbilityPhase.Ready)
        {
            var ability = _em.CreateEntity();
            _em.AddComponentData(ability, new CAbilityBaseInfo
            {
                Code = abilityCode,
                Level = 1,
                Owner = owner,
            });
            _em.AddComponentData(ability, new CAbilityRuntimeState
            {
                Phase = phase,
            });
            _em.AddComponentData(ability, new CAbilityConfig
            {
                Config = config,
            });
            return ability;
        }

        private Entity CreateStandardAsc()
        {
            var asc = _em.CreateEntity();
            _em.AddComponentData(asc, new CTagMask());
            _em.AddComponentData(asc, new CFixedTagMask());
            _em.AddBuffer<BAttribute>(asc);
            _em.AddBuffer<BActiveModifier>(asc);
            _em.AddBuffer<BGrantedAbility>(asc);
            _em.AddBuffer<BFixedTagSource>(asc);
            _em.AddBuffer<BTempTagSource>(asc);
            _em.AddBuffer<BGameplayEffect>(asc);
            return asc;
        }

        private void AssertSelfTarget(Entity request, Entity owner, Entity ability)
        {
            var header = _em.GetComponentData<CTargetDataHeader>(request);
            Assert.That(header.SourceAsc, Is.EqualTo(owner));
            Assert.That(header.SourceAbility, Is.EqualTo(ability));
            Assert.That(header.Kind, Is.EqualTo(ETargetDataKind.Self));

            var targets = _em.GetBuffer<BTargetEntity>(request);
            Assert.That(targets.Length, Is.EqualTo(1));
            Assert.That(targets[0].TargetAsc, Is.EqualTo(owner));
        }

        private Entity FindEffectByCode(int gameplayEffectCode)
        {
            using var query = _em.CreateEntityQuery(
                ComponentType.ReadOnly<CEffectSpecData>(),
                ComponentType.ReadOnly<CEffectContext>());
            using var effects = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            for (var i = 0; i < effects.Length; i++)
            {
                var spec = _em.GetComponentData<CEffectSpecData>(effects[i]);
                if (spec.GameplayEffectCode == gameplayEffectCode)
                    return effects[i];
            }

            return Entity.Null;
        }

        private void AssertPendingEffect(int gameplayEffectCode, Entity owner, Entity ability, int? expectedDuration)
        {
            var effect = FindEffectByCode(gameplayEffectCode);
            Assert.That(effect, Is.Not.EqualTo(Entity.Null));

            var context = _em.GetComponentData<CEffectContext>(effect);
            Assert.That(context.SourceAsc, Is.EqualTo(owner));
            Assert.That(context.TargetAsc, Is.EqualTo(owner));
            Assert.That(context.SourceAbility, Is.EqualTo(ability));
            Assert.That(context.Instigator, Is.EqualTo(owner));
            Assert.That(context.Causer, Is.EqualTo(ability));
            Assert.That(context.TargetDataKind, Is.EqualTo(ETargetDataKind.Self));
            Assert.That(_em.GetComponentData<CEffectSpecData>(effect).Level, Is.EqualTo(3));

            var lifecycle = _em.GetComponentData<CEffectLifecycle>(effect);
            Assert.That(lifecycle.State, Is.EqualTo(EGameplayEffectLifecycleState.PendingApply));

            if (expectedDuration.HasValue)
            {
                Assert.That(_em.HasComponent<CDurationDefinition>(effect), Is.True);
                Assert.That(_em.HasComponent<CDurationRuntime>(effect), Is.True);
                Assert.That(_em.GetComponentData<CDurationDefinition>(effect).Duration, Is.EqualTo(10));
                Assert.That(_em.GetComponentData<CDurationRuntime>(effect).ResolvedDuration, Is.EqualTo(expectedDuration.Value));
            }
            else
            {
                Assert.That(_em.HasComponent<CDurationDefinition>(effect), Is.False);
                Assert.That(_em.HasComponent<CDurationRuntime>(effect), Is.False);
            }
        }

        private BlobAssetReference<BlobAbilityConfig> CreateAbilityConfigBlob(
            CTagMask cooldownTags,
            BlobAbilityCostModifier[] costModifiers,
            int code = 10002,
            TagRequirementMask activationRequiredTags = default,
            TagRequirementMask activationBlockedTags = default,
            CTagMask assetTags = default,
            CTagMask cancelAbilityTags = default,
            CTagMask blockAbilityTags = default)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BlobAbilityConfig>();
            root.Code = code;
            root.MaxLevel = 3;
            root.AssetTags = assetTags;
            root.ActivationRequiredTags = activationRequiredTags;
            root.ActivationBlockedTags = activationBlockedTags;
            root.CancelAbilityTags = cancelAbilityTags;
            root.BlockAbilityTags = blockAbilityTags;
            root.CooldownTags = cooldownTags;
            var costs = builder.Allocate(ref root.CostModifiers, costModifiers.Length);
            for (var i = 0; i < costModifiers.Length; i++)
                costs[i] = costModifiers[i];

            var blob = builder.CreateBlobAssetReference<BlobAbilityConfig>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        private void DestroyEffectsByCodes(params int[] gameplayEffectCodes)
        {
            using var query = _em.CreateEntityQuery(
                ComponentType.ReadOnly<CEffectSpecData>());
            using var effects = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < effects.Length; i++)
            {
                var spec = _em.GetComponentData<CEffectSpecData>(effects[i]);
                for (var j = 0; j < gameplayEffectCodes.Length; j++)
                {
                    if (spec.GameplayEffectCode != gameplayEffectCodes[j])
                        continue;

                    DestroyIfExists(effects[i]);
                    break;
                }
            }
        }

        private void AssertAbilityCommitEvent(
            EGameplayEventType type,
            Entity ability,
            int abilityCode,
            AbilityActivationResult result)
        {
            var evt = FindGameplayEvent(type, ability, abilityCode);
            Assert.That(evt.SourceAbility, Is.EqualTo(ability));
            Assert.That(evt.EventCode, Is.EqualTo(abilityCode));
            Assert.That(evt.Value, Is.EqualTo((float)(int)result));
        }

        private BGameplayEvent AssertGameplayEvent(
            EGameplayEventType type,
            Entity ability,
            int eventCode,
            float value)
        {
            var evt = FindGameplayEvent(type, ability, eventCode);
            Assert.That(evt.SourceAbility, Is.EqualTo(ability));
            Assert.That(evt.EventCode, Is.EqualTo(eventCode));
            Assert.That(evt.Value, Is.EqualTo(value));
            return evt;
        }

        private BGameplayEvent FindGameplayEvent(EGameplayEventType type, Entity ability, int eventCode)
        {
            var events = _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus);
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Type == type
                    && evt.SourceAbility == ability
                    && evt.EventCode == eventCode)
                {
                    return evt;
                }
            }

            Assert.Fail($"Gameplay event {type} for ability {eventCode} was not found.");
            return default;
        }

        private static bool ContainsGameplayEvent(
            DynamicBuffer<BGameplayEvent> events,
            EGameplayEventType type,
            int eventCode,
            int reasonCode,
            int contextId)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Type == type
                    && evt.EventCode == eventCode
                    && evt.ReasonCode == reasonCode
                    && evt.ContextId == contextId)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearTransientEventBuffers()
        {
            if (!_em.Exists(GASManager.EntityEventBus))
                return;

            if (_em.HasBuffer<BDamageEvent>(GASManager.EntityEventBus))
                _em.GetBuffer<BDamageEvent>(GASManager.EntityEventBus).Clear();
            if (_em.HasBuffer<BTagChangeEvent>(GASManager.EntityEventBus))
                _em.GetBuffer<BTagChangeEvent>(GASManager.EntityEventBus).Clear();
            if (_em.HasBuffer<BGameplayEvent>(GASManager.EntityEventBus))
                _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus).Clear();
            if (_em.HasBuffer<BAttributeChangeEvent>(GASManager.EntityEventBus))
                _em.GetBuffer<BAttributeChangeEvent>(GASManager.EntityEventBus).Clear();
            if (_em.HasBuffer<BCueRequest>(GASManager.EntityEventBus))
                _em.GetBuffer<BCueRequest>(GASManager.EntityEventBus).Clear();
        }

        private void ClearEffectCommandStream()
        {
            if (_em == default)
                return;

            if (EffectCommandSpecStream.TryGetSingleton(_em, out var streamEntity))
                EffectCommandSpecStream.ClearFrameLocalData(_em, streamEntity, 0);
        }

        private bool HasTagChangeEvent(Entity asc, int tagIndex, bool added)
        {
            var events = _em.GetBuffer<BTagChangeEvent>(GASManager.EntityEventBus);
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.ASC == asc && evt.TagIndex == tagIndex && evt.Added == added)
                    return true;
            }

            return false;
        }

        private void RunFullFrame()
        {
            RunCommandGroup();
            RunEffectGroup();
        }

        private int CountEntitiesWith(ComponentType componentType)
        {
            using var query = _em.CreateEntityQuery(componentType);
            return query.CalculateEntityCount();
        }

        private int CountApplyRequestsByCode(int gameplayEffectCode)
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<CApplyGameplayEffectRequest>());
            using var requests = query.ToEntityArray(Allocator.Temp);
            var count = 0;
            for (var i = 0; i < requests.Length; i++)
            {
                if (_em.GetComponentData<CApplyGameplayEffectRequest>(requests[i]).GameplayEffectCode == gameplayEffectCode)
                    count++;
            }

            return count;
        }

        private void RunCommandGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>().Update();
        }

        private void RunEffectGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASEffectGroup>().Update();
        }

        private void RunAbilityGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASAbilityGroup>().Update();
        }

        private void DestroyIfExists(Entity entity)
        {
            if (entity == Entity.Null || !_em.Exists(entity))
                return;

            if (_em.HasComponent<CAbilityConfig>(entity))
            {
                var config = _em.GetComponentData<CAbilityConfig>(entity).Config;
                if (config.IsCreated)
                    config.Dispose();
            }

            _em.DestroyEntity(entity);
        }

        private sealed class ConfGrantedTagIndex : GameplayEffectComponentConfig
        {
            private readonly int _tagIndex;

            public ConfGrantedTagIndex(int tagIndex)
            {
                _tagIndex = tagIndex;
            }

            public override void LoadToGameplayEffectEntity(Entity ge)
            {
                var buffer = _entityManager.HasBuffer<BGrantedTagConfig>(ge)
                    ? _entityManager.GetBuffer<BGrantedTagConfig>(ge)
                    : _entityManager.AddBuffer<BGrantedTagConfig>(ge);

                buffer.Add(new BGrantedTagConfig { TagIndex = _tagIndex });
            }
        }

        private sealed class DirectModifierComponentConfig : GameplayEffectComponentConfig
        {
            private readonly int _attrSetCode;
            private readonly int _attributeCode;
            private readonly EModifierOp _op;
            private readonly float _magnitude;

            public DirectModifierComponentConfig(
                int attrSetCode,
                int attributeCode,
                EModifierOp op,
                float magnitude)
            {
                _attrSetCode = attrSetCode;
                _attributeCode = attributeCode;
                _op = op;
                _magnitude = magnitude;
            }

            public override void LoadToGameplayEffectEntity(Entity ge)
            {
                var buffer = _entityManager.HasBuffer<BModifierConfig>(ge)
                    ? _entityManager.GetBuffer<BModifierConfig>(ge)
                    : _entityManager.AddBuffer<BModifierConfig>(ge);

                buffer.Add(new BModifierConfig
                {
                    AttrSetCode = _attrSetCode,
                    AttributeCode = _attributeCode,
                    Op = _op,
                    Magnitude = _magnitude,
                });
            }
        }
    }
}
