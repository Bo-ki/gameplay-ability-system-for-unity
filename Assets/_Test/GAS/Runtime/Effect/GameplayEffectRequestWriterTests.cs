using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime.Tests.Effect
{
    public sealed class GameplayEffectRequestWriterTests
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
            ClearTransientEventBuffers();
            ClearEffectCommandStream();
        }

        [TearDown]
        public void TearDown()
        {
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(_ => null);
            ClearTransientEventBuffers();
            ClearEffectCommandStream();
        }

        [Test]
        public void CreateSelfRequestWritesHeaderTargetAndSetByCaller()
        {
            var source = _em.CreateEntity();
            var request = Entity.Null;

            try
            {
                request = GameplayEffectRequestWriter.Create(
                    _em,
                    new CApplyGameplayEffectRequest
                    {
                        SourceAsc = source,
                        Instigator = source,
                        Causer = source,
                        GameplayEffectCode = 10001,
                        Level = 2,
                    },
                    new CTargetDataHeader
                    {
                        SourceAsc = source,
                        Kind = ETargetDataKind.Self,
                    });

                GameplayEffectRequestWriter.AddTarget(_em, request, source);
                GameplayEffectRequestWriter.AddSetByCallerValues(
                    _em,
                    request,
                    new[]
                    {
                        new BSetByCallerValue { Key = 3001, Value = 12f },
                        new BSetByCallerValue { Key = 3002, Value = 34f },
                    });

                Assert.That(_em.HasComponent<CApplyGameplayEffectRequest>(request), Is.True);
                Assert.That(_em.HasComponent<CTargetDataHeader>(request), Is.True);

                var header = _em.GetComponentData<CTargetDataHeader>(request);
                Assert.That(header.SourceAsc, Is.EqualTo(source));
                Assert.That(header.Kind, Is.EqualTo(ETargetDataKind.Self));

                var targets = _em.GetBuffer<BTargetEntity>(request);
                Assert.That(targets.Length, Is.EqualTo(1));
                Assert.That(targets[0].TargetAsc, Is.EqualTo(source));

                var values = _em.GetBuffer<BSetByCallerValue>(request);
                Assert.That(values.Length, Is.EqualTo(2));
                Assert.That(values[0].Key, Is.EqualTo(3001));
                Assert.That(values[0].Value, Is.EqualTo(12f));
                Assert.That(values[1].Key, Is.EqualTo(3002));
                Assert.That(values[1].Value, Is.EqualTo(34f));
            }
            finally
            {
                DestroyIfExists(request);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void AddTargetAllowsEntityListRequests()
        {
            var source = _em.CreateEntity();
            var targetA = _em.CreateEntity();
            var targetB = _em.CreateEntity();
            var request = Entity.Null;

            try
            {
                request = GameplayEffectRequestWriter.Create(
                    _em,
                    new CApplyGameplayEffectRequest
                    {
                        SourceAsc = source,
                        GameplayEffectCode = 10002,
                    },
                    new CTargetDataHeader
                    {
                        SourceAsc = source,
                        Kind = ETargetDataKind.EntityList,
                    });

                GameplayEffectRequestWriter.AddTarget(_em, request, targetA);
                GameplayEffectRequestWriter.AddTarget(_em, request, targetB);

                var header = _em.GetComponentData<CTargetDataHeader>(request);
                Assert.That(header.Kind, Is.EqualTo(ETargetDataKind.EntityList));

                var targets = _em.GetBuffer<BTargetEntity>(request);
                Assert.That(targets.Length, Is.EqualTo(2));
                Assert.That(targets[0].TargetAsc, Is.EqualTo(targetA));
                Assert.That(targets[1].TargetAsc, Is.EqualTo(targetB));
            }
            finally
            {
                DestroyIfExists(request);
                DestroyIfExists(source);
                DestroyIfExists(targetA);
                DestroyIfExists(targetB);
            }
        }

        [Test]
        public void RequestEntityCanCarryOptionalPointDirectionAndHitBuffers()
        {
            var source = _em.CreateEntity();
            var target = _em.CreateEntity();
            var request = Entity.Null;

            try
            {
                request = GameplayEffectRequestWriter.Create(
                    _em,
                    new CApplyGameplayEffectRequest
                    {
                        SourceAsc = source,
                        GameplayEffectCode = 10003,
                    },
                    new CTargetDataHeader
                    {
                        SourceAsc = source,
                        Kind = ETargetDataKind.Entity,
                    });

                GameplayEffectRequestWriter.AddTarget(_em, request, target);
                _em.AddBuffer<BTargetPoint>(request).Add(new BTargetPoint
                {
                    Position = new float3(1f, 2f, 3f),
                });
                _em.AddBuffer<BTargetDirection>(request).Add(new BTargetDirection
                {
                    Direction = new float3(0f, 0f, 1f),
                });
                _em.AddBuffer<BTargetHit>(request).Add(new BTargetHit
                {
                    HitEntity = target,
                    Position = new float3(4f, 5f, 6f),
                    Normal = new float3(0f, 1f, 0f),
                    SurfaceCode = 7,
                });

                Assert.That(_em.GetBuffer<BTargetPoint>(request)[0].Position, Is.EqualTo(new float3(1f, 2f, 3f)));
                Assert.That(_em.GetBuffer<BTargetDirection>(request)[0].Direction, Is.EqualTo(new float3(0f, 0f, 1f)));

                var hit = _em.GetBuffer<BTargetHit>(request)[0];
                Assert.That(hit.HitEntity, Is.EqualTo(target));
                Assert.That(hit.Position, Is.EqualTo(new float3(4f, 5f, 6f)));
                Assert.That(hit.Normal, Is.EqualTo(new float3(0f, 1f, 0f)));
                Assert.That(hit.SurfaceCode, Is.EqualTo(7));
            }
            finally
            {
                DestroyIfExists(request);
                DestroyIfExists(source);
                DestroyIfExists(target);
            }
        }

        [Test]
        public void AppendSimpleInstantCommandOrCreateSingleTargetRequestWritesEffectCommandStream()
        {
            const int effectCode = 12011;
            const int attrSetCode = 10;
            const int attributeCode = 21;
            var source = _em.CreateEntity();
            var target = _em.CreateEntity();

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode
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
                                    Magnitude = 12f,
                                },
                            },
                        },
                    })
                    : null);

            try
            {
                _em.AddBuffer<BAttribute>(target).Add(new BAttribute
                {
                    AttrSetCode = attrSetCode,
                    Code = attributeCode,
                    BaseValue = 100f,
                    CurrentValue = 100f,
                    PreviousCurrentValue = 100f,
                });

                var request = GameplayEffectRequestWriter.AppendSimpleInstantCommandOrCreateSingleTargetRequest(
                    _em,
                    new CApplyGameplayEffectRequest
                    {
                        SourceAsc = source,
                        Instigator = source,
                        Causer = source,
                        GameplayEffectCode = effectCode,
                        Level = 1,
                    },
                    target,
                    ETargetDataKind.Entity,
                    "EffectCommandProducer");

                Assert.That(request, Is.EqualTo(Entity.Null));
                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));
                Assert.That(CountRuntimeEffectSpecsByCode(effectCode), Is.EqualTo(0));

                var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
                Assert.That(_em.GetBuffer<BEffectCommand>(streamEntity).Length, Is.EqualTo(1));
                Assert.That(_em.GetBuffer<BAttribute>(target)[0].BaseValue, Is.EqualTo(100f));

                RunCommandGroup();

                var specs = _em.GetBuffer<BInstantEffectSpec>(streamEntity);
                var deltas = _em.GetBuffer<BAttributeDelta>(streamEntity);
                var facts = _em.GetBuffer<BTypedSimulationFact>(streamEntity);
                var attribute = _em.GetBuffer<BAttribute>(target)[0];

                Assert.That(specs.Length, Is.EqualTo(1));
                Assert.That(deltas.Length, Is.EqualTo(1));
                Assert.That(facts.Length, Is.EqualTo(1));
                Assert.That(attribute.BaseValue, Is.EqualTo(88f));
                Assert.That(attribute.CurrentValue, Is.EqualTo(88f));
                Assert.That(deltas[0].GameplayEffectCode, Is.EqualTo(effectCode));
                Assert.That(facts[0].SourceDeltaSequence, Is.EqualTo(deltas[0].Sequence));
                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));
                Assert.That(CountRuntimeEffectSpecsByCode(effectCode), Is.EqualTo(0));
            }
            finally
            {
                DestroyIfExists(target);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void AppendSimpleInstantCommandsOrCreateTargetListRequestWritesCommandPerTarget()
        {
            const int effectCode = 12013;
            const int attrSetCode = 10;
            const int attributeCode = 23;
            var source = _em.CreateEntity();
            var targetA = _em.CreateEntity();
            var targetB = _em.CreateEntity();

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode
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
                                    Magnitude = 9f,
                                },
                            },
                        },
                    })
                    : null);

            try
            {
                AddAttribute(targetA, attrSetCode, attributeCode, 100f);
                AddAttribute(targetB, attrSetCode, attributeCode, 80f);

                var request = GameplayEffectRequestWriter.AppendSimpleInstantCommandsOrCreateTargetListRequest(
                    _em,
                    new CApplyGameplayEffectRequest
                    {
                        SourceAsc = source,
                        Instigator = source,
                        Causer = source,
                        GameplayEffectCode = effectCode,
                        Level = 2,
                    },
                    new[] { targetA, targetB },
                    ETargetDataKind.EntityList,
                    "EffectCommandMultiTarget");

                Assert.That(request, Is.EqualTo(Entity.Null));
                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));
                Assert.That(CountRuntimeEffectSpecsByCode(effectCode), Is.EqualTo(0));

                var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
                var commands = _em.GetBuffer<BEffectCommand>(streamEntity);
                Assert.That(commands.Length, Is.EqualTo(2));
                Assert.That(commands[0].TargetAsc, Is.EqualTo(targetA));
                Assert.That(commands[1].TargetAsc, Is.EqualTo(targetB));
                Assert.That(commands[0].TargetDataKind, Is.EqualTo(ETargetDataKind.EntityList));
                Assert.That(commands[1].TargetDataKind, Is.EqualTo(ETargetDataKind.EntityList));

                RunCommandGroup();

                var specs = _em.GetBuffer<BInstantEffectSpec>(streamEntity);
                var deltas = _em.GetBuffer<BAttributeDelta>(streamEntity);
                var facts = _em.GetBuffer<BTypedSimulationFact>(streamEntity);

                Assert.That(specs.Length, Is.EqualTo(2));
                Assert.That(deltas.Length, Is.EqualTo(2));
                Assert.That(facts.Length, Is.EqualTo(2));
                Assert.That(_em.GetBuffer<BAttribute>(targetA)[0].BaseValue, Is.EqualTo(91f));
                Assert.That(_em.GetBuffer<BAttribute>(targetB)[0].BaseValue, Is.EqualTo(71f));
                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));
                Assert.That(CountRuntimeEffectSpecsByCode(effectCode), Is.EqualTo(0));
            }
            finally
            {
                DestroyIfExists(targetB);
                DestroyIfExists(targetA);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void AppendSimpleInstantCommandsOrCreateTargetListRequestFallsBackWithoutPartialCommands()
        {
            const int effectCode = 12014;
            var source = _em.CreateEntity();
            var targetA = _em.CreateEntity();
            var targetB = _em.CreateEntity();
            var request = Entity.Null;

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode
                    ? new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                    {
                        new ConfDuration
                        {
                            duration = 2,
                            timeUnit = TimeUnit.Frame,
                        },
                    })
                    : null);

            try
            {
                request = GameplayEffectRequestWriter.AppendSimpleInstantCommandsOrCreateTargetListRequest(
                    _em,
                    new CApplyGameplayEffectRequest
                    {
                        SourceAsc = source,
                        GameplayEffectCode = effectCode,
                        Level = 1,
                    },
                    new[] { targetA, targetB },
                    ETargetDataKind.EntityList,
                    "EffectCommandMultiTargetFallback");

                Assert.That(request, Is.Not.EqualTo(Entity.Null));
                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(1));

                var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
                Assert.That(_em.GetBuffer<BEffectCommand>(streamEntity).Length, Is.EqualTo(0));

                var header = _em.GetComponentData<CTargetDataHeader>(request);
                Assert.That(header.SourceAsc, Is.EqualTo(source));
                Assert.That(header.Kind, Is.EqualTo(ETargetDataKind.EntityList));

                var targets = _em.GetBuffer<BTargetEntity>(request);
                Assert.That(targets.Length, Is.EqualTo(2));
                Assert.That(targets[0].TargetAsc, Is.EqualTo(targetA));
                Assert.That(targets[1].TargetAsc, Is.EqualTo(targetB));
            }
            finally
            {
                DestroyIfExists(request);
                DestroyIfExists(targetB);
                DestroyIfExists(targetA);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void AppendSimpleInstantCommandOrCreateSingleTargetRequestProjectsCueOnApplyFromStream()
        {
            const int effectCode = 12012;
            const int attrSetCode = 10;
            const int attributeCode = 22;
            const int cueCode = 3002;
            var source = _em.CreateEntity();
            var target = _em.CreateEntity();

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode
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
                                    Magnitude = 12f,
                                },
                            },
                        },
                        new ConfGameplayEffectCueRequestOnApply
                        {
                            CueCode = cueCode,
                        },
                    })
                    : null);

            try
            {
                _em.AddBuffer<BAttribute>(target).Add(new BAttribute
                {
                    AttrSetCode = attrSetCode,
                    Code = attributeCode,
                    BaseValue = 100f,
                    CurrentValue = 100f,
                });

                var request = GameplayEffectRequestWriter.AppendSimpleInstantCommandOrCreateSingleTargetRequest(
                    _em,
                    new CApplyGameplayEffectRequest
                    {
                        SourceAsc = source,
                        Instigator = source,
                        Causer = source,
                        GameplayEffectCode = effectCode,
                        Level = 1,
                    },
                    target,
                    ETargetDataKind.Entity,
                    "CueProjection");

                Assert.That(request, Is.EqualTo(Entity.Null));
                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));
                Assert.That(CountRuntimeEffectSpecsByCode(effectCode), Is.EqualTo(0));

                var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
                var commands = _em.GetBuffer<BEffectCommand>(streamEntity);
                Assert.That(commands.Length, Is.EqualTo(1));
                Assert.That(commands[0].GameplayEffectCode, Is.EqualTo(effectCode));
                Assert.That(commands[0].TargetAsc, Is.EqualTo(target));

                RunCommandGroup();

                var specs = _em.GetBuffer<BInstantEffectSpec>(streamEntity);
                var deltas = _em.GetBuffer<BAttributeDelta>(streamEntity);
                var facts = _em.GetBuffer<BTypedSimulationFact>(streamEntity);
                var cueRequests = _em.GetBuffer<BCueRequest>(GASManager.EntityEventBus);
                var gameplayEvents = _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus);
                var attribute = _em.GetBuffer<BAttribute>(target)[0];

                Assert.That(specs.Length, Is.EqualTo(1));
                Assert.That(specs[0].CueRequestOnApplyCode, Is.EqualTo(cueCode));
                Assert.That(deltas.Length, Is.EqualTo(1));
                Assert.That(facts.Length, Is.EqualTo(1));
                Assert.That(attribute.BaseValue, Is.EqualTo(88f));
                Assert.That(cueRequests.Length, Is.EqualTo(1));
                Assert.That(cueRequests[0].TargetAsc, Is.EqualTo(target));
                Assert.That(cueRequests[0].SourceAsc, Is.EqualTo(source));
                Assert.That(cueRequests[0].CueEvent, Is.EqualTo(EGameplayCueEvent.OnApply));
                Assert.That(
                    ContainsGameplayEvent(
                        gameplayEvents,
                        EGameplayEventType.CueRequested,
                        (int)EGameplayCueEvent.OnApply,
                        cueCode,
                        specs[0].ContextId),
                    Is.True);
                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));
                Assert.That(CountRuntimeEffectSpecsByCode(effectCode), Is.EqualTo(0));
            }
            finally
            {
                DestroyIfExists(target);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void ApplyLegacyInstantBypassOrCreateSingleTargetRequestAppliesSimpleInstantWithoutRequestEntity()
        {
            const int effectCode = 12001;
            const int attrSetCode = 10;
            const int attributeCode = 20;
            const int cueCode = 3001;
            var eventBus = GASManager.EntityEventBus;
            var previousEventBus = _em.GetComponentData<CGameplayEventBus>(eventBus);
            var source = _em.CreateEntity();
            var target = _em.CreateEntity();

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode
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
                                    Magnitude = 12f,
                                },
                            },
                        },
                        new ConfGameplayEffectCueRequestOnApply
                        {
                            CueCode = cueCode,
                        },
                    })
                    : null);

            try
            {
                _em.SetComponentData(eventBus, new CGameplayEventBus
                {
                    NextSequence = 30,
                    NextContextId = 7,
                });
                _em.AddBuffer<BAttribute>(target).Add(new BAttribute
                {
                    AttrSetCode = attrSetCode,
                    Code = attributeCode,
                    BaseValue = 100f,
                    CurrentValue = 100f,
                });

                var request = GameplayEffectRequestWriter.ApplyLegacyInstantBypassOrCreateSingleTargetRequest(
                    _em,
                    new CApplyGameplayEffectRequest
                    {
                        SourceAsc = source,
                        Instigator = source,
                        Causer = source,
                        GameplayEffectCode = effectCode,
                        Level = 1,
                    },
                    target,
                    ETargetDataKind.Entity,
                    "LegacyDirect");

                Assert.That(request, Is.EqualTo(Entity.Null));
                var attribute = _em.GetBuffer<BAttribute>(target)[0];
                Assert.That(attribute.BaseValue, Is.EqualTo(88f));
                Assert.That(attribute.CurrentValue, Is.EqualTo(88f));

                var attributeEvents = _em.GetBuffer<BAttributeChangeEvent>(eventBus);
                Assert.That(attributeEvents.Length, Is.EqualTo(1));
                Assert.That(attributeEvents[0].ASC, Is.EqualTo(target));
                Assert.That(attributeEvents[0].ContextId, Is.EqualTo(7));
                Assert.That(attributeEvents[0].EventCode, Is.EqualTo(effectCode));

                var cueRequests = _em.GetBuffer<BCueRequest>(eventBus);
                Assert.That(cueRequests.Length, Is.EqualTo(1));
                Assert.That(cueRequests[0].TargetAsc, Is.EqualTo(target));
                Assert.That(cueRequests[0].ContextId, Is.EqualTo(7));
                Assert.That(cueRequests[0].CueEvent, Is.EqualTo(EGameplayCueEvent.OnApply));

                var gameplayEvents = _em.GetBuffer<BGameplayEvent>(eventBus);
                Assert.That(gameplayEvents.Length, Is.EqualTo(3));
                Assert.That(gameplayEvents[0].Sequence, Is.EqualTo(30));
                Assert.That(gameplayEvents[1].Sequence, Is.EqualTo(31));
                Assert.That(gameplayEvents[2].Sequence, Is.EqualTo(32));
                Assert.That(ContainsGameplayEvent(gameplayEvents, EGameplayEventType.GameplayEffectInstanced, effectCode, 7), Is.True);
                Assert.That(ContainsGameplayEvent(gameplayEvents, EGameplayEventType.CueRequested, (int)EGameplayCueEvent.OnApply, 7), Is.True);
                Assert.That(ContainsGameplayEvent(gameplayEvents, EGameplayEventType.GameplayEffectApplied, effectCode, 7), Is.True);
                var eventBusState = _em.GetComponentData<CGameplayEventBus>(eventBus);
                Assert.That(eventBusState.NextContextId, Is.EqualTo(8));
                Assert.That(eventBusState.NextSequence, Is.EqualTo(33));
            }
            finally
            {
                _em.SetComponentData(eventBus, previousEventBus);
                DestroyIfExists(target);
                DestroyIfExists(source);
            }
        }

        private void DestroyIfExists(Entity entity)
        {
            if (entity != Entity.Null && _em.Exists(entity))
                _em.DestroyEntity(entity);
        }

        private void AddAttribute(Entity target, int attrSetCode, int attributeCode, float value)
        {
            _em.AddBuffer<BAttribute>(target).Add(new BAttribute
            {
                AttrSetCode = attrSetCode,
                Code = attributeCode,
                BaseValue = value,
                CurrentValue = value,
                PreviousCurrentValue = value,
            });
        }

        private void ClearTransientEventBuffers()
        {
            if (!GASManager.IsInitialized || GASManager.EntityEventBus == Entity.Null)
                return;

            var eventBus = GASManager.EntityEventBus;
            if (!_em.Exists(eventBus))
                return;

            if (_em.HasBuffer<BDamageEvent>(eventBus))
                _em.GetBuffer<BDamageEvent>(eventBus).Clear();
            if (_em.HasBuffer<BTagChangeEvent>(eventBus))
                _em.GetBuffer<BTagChangeEvent>(eventBus).Clear();
            if (_em.HasBuffer<BGameplayEvent>(eventBus))
                _em.GetBuffer<BGameplayEvent>(eventBus).Clear();
            if (_em.HasBuffer<BAttributeChangeEvent>(eventBus))
                _em.GetBuffer<BAttributeChangeEvent>(eventBus).Clear();
            if (_em.HasBuffer<BCueRequest>(eventBus))
                _em.GetBuffer<BCueRequest>(eventBus).Clear();
        }

        private void ClearEffectCommandStream()
        {
            if (_em == default)
                return;

            if (EffectCommandSpecStream.TryGetSingleton(_em, out var streamEntity))
                EffectCommandSpecStream.ClearFrameLocalData(_em, streamEntity, 0);
        }

        private static void RunCommandGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>().Update();
        }

        private int CountApplyRequestsByCode(int effectCode)
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<CApplyGameplayEffectRequest>());
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var count = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                if (_em.GetComponentData<CApplyGameplayEffectRequest>(entities[i]).GameplayEffectCode == effectCode)
                    count++;
            }

            return count;
        }

        private int CountRuntimeEffectSpecsByCode(int effectCode)
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<CEffectSpecData>());
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var count = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                if (_em.GetComponentData<CEffectSpecData>(entities[i]).GameplayEffectCode == effectCode)
                    count++;
            }

            return count;
        }

        private static bool ContainsGameplayEvent(
            DynamicBuffer<BGameplayEvent> events,
            EGameplayEventType type,
            int eventCode,
            int contextId)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Type == type
                    && evt.EventCode == eventCode
                    && evt.ContextId == contextId)
                {
                    return true;
                }
            }

            return false;
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
    }
}
