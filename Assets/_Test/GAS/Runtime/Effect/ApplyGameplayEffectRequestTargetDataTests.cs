using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime.Tests.Effect
{
    public sealed class ApplyGameplayEffectRequestTargetDataTests
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
        }

        [TearDown]
        public void TearDown()
        {
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
        }

        [Test]
        public void ApplyRequestCopiesPointDirectionAndHitSummaryToEffectInstance()
        {
            const int effectCode = 97001;

            var source = _em.CreateEntity();
            var target = _em.CreateEntity();
            var hitEntity = _em.CreateEntity();
            var request = Entity.Null;
            var effect = Entity.Null;

            try
            {
                RegisterEmptyEffect(effectCode);
                request = CreateRequest(effectCode, source, ETargetDataKind.Entity);
                GameplayEffectRequestWriter.AddTarget(_em, request, target);
                AddTargetDataSummary(request, hitEntity);

                RunCommandGroup();

                Assert.That(_em.Exists(request), Is.False);
                effect = FindSingleEffectByCode(effectCode);
                Assert.That(effect, Is.Not.EqualTo(Entity.Null));

                var context = _em.GetComponentData<CEffectContext>(effect);
                Assert.That(context.SourceAsc, Is.EqualTo(source));
                Assert.That(context.TargetAsc, Is.EqualTo(target));
                Assert.That(context.TargetDataKind, Is.EqualTo(ETargetDataKind.Entity));

                AssertTargetDataSummary(effect, hitEntity);
            }
            finally
            {
                DestroyIfExists(effect);
                DestroyIfExists(request);
                DestroyIfExists(hitEntity);
                DestroyIfExists(target);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void EntityListRequestCopiesTargetDataSummaryToEachEffectInstance()
        {
            const int effectCode = 97002;

            var source = _em.CreateEntity();
            var targetA = _em.CreateEntity();
            var targetB = _em.CreateEntity();
            var hitEntity = _em.CreateEntity();
            var request = Entity.Null;

            try
            {
                RegisterEmptyEffect(effectCode);
                request = CreateRequest(effectCode, source, ETargetDataKind.EntityList);
                GameplayEffectRequestWriter.AddTarget(_em, request, targetA);
                GameplayEffectRequestWriter.AddTarget(_em, request, targetB);
                AddTargetDataSummary(request, hitEntity);

                RunCommandGroup();

                Assert.That(_em.Exists(request), Is.False);
                using var effects = FindEffectsByCode(effectCode);
                Assert.That(effects.Length, Is.EqualTo(2));

                AssertEffectForTarget(effects, targetA, hitEntity);
                AssertEffectForTarget(effects, targetB, hitEntity);
            }
            finally
            {
                DestroyEffectsByCode(effectCode);
                DestroyIfExists(request);
                DestroyIfExists(hitEntity);
                DestroyIfExists(targetB);
                DestroyIfExists(targetA);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void ApplyRequestStoresLevelInSpecAndKeepsContextLevelFree()
        {
            const int effectCode = 97004;

            var source = _em.CreateEntity();
            var target = _em.CreateEntity();
            var request = Entity.Null;
            var effect = Entity.Null;

            try
            {
                RegisterEmptyEffect(effectCode);
                request = CreateRequest(effectCode, source, ETargetDataKind.Entity);
                GameplayEffectRequestWriter.AddTarget(_em, request, target);

                RunCommandGroup();

                effect = FindSingleEffectByCode(effectCode);
                var context = _em.GetComponentData<CEffectContext>(effect);
                var spec = _em.GetComponentData<CEffectSpecData>(effect);

                Assert.That(spec.GameplayEffectCode, Is.EqualTo(effectCode));
                Assert.That(spec.Level, Is.EqualTo(3));
                Assert.That(context.SourceAsc, Is.EqualTo(source));
                Assert.That(context.TargetAsc, Is.EqualTo(target));
                var contextLevelField = typeof(CEffectContext)
                    .GetField(nameof(CEffectSpecData.Level));
                Assert.That(contextLevelField, Is.Null);
            }
            finally
            {
                DestroyIfExists(effect);
                DestroyIfExists(request);
                DestroyIfExists(target);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void MissingGameplayEffectConfigRecordsApplyRequestDiagnostic()
        {
            const int effectCode = 97091;

            var source = _em.CreateEntity();
            var target = _em.CreateEntity();
            var request = Entity.Null;

            try
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(_ => null);
                request = CreateRequest(effectCode, source, ETargetDataKind.Entity);
                GameplayEffectRequestWriter.AddTarget(_em, request, target);

                RunCommandGroup();

                Assert.That(_em.Exists(request), Is.False);
                using var effects = FindEffectsByCode(effectCode);
                Assert.That(effects.Length, Is.EqualTo(0));

                Assert.That(ConfigRegistryDiagnostics.Count, Is.EqualTo(1));
                AssertMissingConfigDiagnostic(
                    ConfigRegistryDiagnostics.Entries[0],
                    ConfigRegistryConfigKind.GameplayEffect,
                    effectCode,
                    ConfigRegistryConfigKind.None,
                    0,
                    ConfigRegistryReferenceKind.ApplyGameplayEffectRequest);
            }
            finally
            {
                DestroyEffectsByCode(effectCode);
                DestroyIfExists(request);
                DestroyIfExists(target);
                DestroyIfExists(source);
            }
        }


        [Test]
        public void GameplayEventAssignsFrameAndMonotonicSequence()
        {
            var eventBus = GASManager.EntityEventBus;
            var previousEventBus = _em.GetComponentData<CGameplayEventBus>(eventBus);
            var previousTimer = _em.GetComponentData<GlobalTimer>(GASManager.EntityGlobalTimer);

            try
            {
                _em.GetBuffer<BGameplayEvent>(eventBus).Clear();
                _em.SetComponentData(eventBus, new CGameplayEventBus
                {
                    NextSequence = 0,
                });
                _em.SetComponentData(GASManager.EntityGlobalTimer, new GlobalTimer
                {
                    Frame = 123,
                    Turn = previousTimer.Turn,
                });

                EventBusHelper.EnqueueGameplayEvent(_em, eventBus, new BGameplayEvent
                {
                    Type = EGameplayEventType.GameplayEffectInstanced,
                    EventCode = 97003,
                });
                EventBusHelper.EnqueueGameplayEvent(_em, eventBus, new BGameplayEvent
                {
                    Type = EGameplayEventType.GameplayEffectApplied,
                    EventCode = 97003,
                });

                var events = _em.GetBuffer<BGameplayEvent>(eventBus);
                Assert.That(events.Length, Is.EqualTo(2));
                Assert.That(events[0].Frame, Is.EqualTo(123));
                Assert.That(events[0].Sequence, Is.EqualTo(0));
                Assert.That(events[1].Frame, Is.EqualTo(123));
                Assert.That(events[1].Sequence, Is.EqualTo(1));

                var eventBusState = _em.GetComponentData<CGameplayEventBus>(eventBus);
                Assert.That(eventBusState.NextSequence, Is.EqualTo(2));
            }
            finally
            {
                _em.GetBuffer<BGameplayEvent>(eventBus).Clear();
                _em.SetComponentData(eventBus, previousEventBus);
                _em.SetComponentData(GASManager.EntityGlobalTimer, previousTimer);
            }
        }

        [Test]
        public void AttributeRecalculateEmitsCurrentValueChangeEvent()
        {
            const int attrSetCode = 97004;
            const int attributeCode = 97005;

            var asc = _em.CreateEntity();
            var sourceEffect = _em.CreateEntity();

            try
            {
                CreateAttributeProjectionFixture(asc, sourceEffect, attrSetCode, attributeCode);
                ClearAttributeChangeEvents();

                RunAttributeGroup();

                var attribute = _em.GetBuffer<BAttribute>(asc)[0];
                Assert.That(attribute.CurrentValue, Is.EqualTo(125f));
                Assert.That(attribute.PreviousCurrentValue, Is.EqualTo(125f));
                Assert.That(attribute.Dirty, Is.False);
                Assert.That(attribute.CurrentValueChangePending, Is.False);

                var evt = FindAttributeChangeEvent(asc, attrSetCode, attributeCode);
                Assert.That(evt.OldValue, Is.EqualTo(100f));
                Assert.That(evt.NewValue, Is.EqualTo(125f));
                Assert.That(evt.IsBaseValue, Is.False);
            }
            finally
            {
                ClearAttributeChangeEvents();
                DestroyIfExists(sourceEffect);
                DestroyIfExists(asc);
            }
        }

        [Test]
        public void AttributeProjectionDoesNotEmitDuplicateEventWithoutNewChange()
        {
            const int attrSetCode = 97006;
            const int attributeCode = 97007;

            var asc = _em.CreateEntity();
            var sourceEffect = _em.CreateEntity();

            try
            {
                CreateAttributeProjectionFixture(asc, sourceEffect, attrSetCode, attributeCode);
                ClearAttributeChangeEvents();

                RunAttributeGroup();
                ClearAttributeChangeEvents();
                RunAttributeGroup();

                var events = _em.GetBuffer<BAttributeChangeEvent>(GASManager.EntityEventBus);
                Assert.That(events.Length, Is.EqualTo(0));
            }
            finally
            {
                ClearAttributeChangeEvents();
                DestroyIfExists(sourceEffect);
                DestroyIfExists(asc);
            }
        }

        [Test]
        public void AttributeRecalculatePreservesPendingOldValueAcrossRecalculate()
        {
            const int attrSetCode = 97008;
            const int attributeCode = 97009;

            var asc = _em.CreateEntity();
            var sourceEffect = _em.CreateEntity();

            try
            {
                _em.AddComponentData(asc, new CTagMask());
                _em.AddBuffer<BAttribute>(asc).Add(new BAttribute
                {
                    AttrSetCode = attrSetCode,
                    Code = attributeCode,
                    BaseValue = 50f,
                    CurrentValue = 50f,
                    PreviousCurrentValue = 110f,
                    Dirty = true,
                    CurrentValueChangePending = true,
                });
                _em.AddBuffer<BActiveModifier>(asc).Add(new BActiveModifier
                {
                    AttrSetCode = attrSetCode,
                    AttributeCode = attributeCode,
                    SourceEntity = sourceEffect,
                    Magnitude = 10f,
                    Op = EModifierOp.Add,
                });
                ClearAttributeChangeEvents();

                RunAttributeGroup();

                var attribute = _em.GetBuffer<BAttribute>(asc)[0];
                Assert.That(attribute.CurrentValue, Is.EqualTo(60f));
                Assert.That(attribute.PreviousCurrentValue, Is.EqualTo(60f));
                Assert.That(attribute.CurrentValueChangePending, Is.False);

                var evt = FindAttributeChangeEvent(asc, attrSetCode, attributeCode);
                Assert.That(evt.OldValue, Is.EqualTo(110f));
                Assert.That(evt.NewValue, Is.EqualTo(60f));
                Assert.That(evt.IsBaseValue, Is.False);
            }
            finally
            {
                ClearAttributeChangeEvents();
                DestroyIfExists(sourceEffect);
                DestroyIfExists(asc);
            }
        }

        private Entity CreateRequest(int effectCode, Entity source, ETargetDataKind kind)
        {
            return GameplayEffectRequestWriter.Create(
                _em,
                new CApplyGameplayEffectRequest
                {
                    SourceAsc = source,
                    Instigator = source,
                    Causer = source,
                    GameplayEffectCode = effectCode,
                    Level = 3,
                },
                new CTargetDataHeader
                {
                    SourceAsc = source,
                    Kind = kind,
                },
                "TargetDataSummaryRequest");
        }

        private void CreateAttributeProjectionFixture(
            Entity asc,
            Entity sourceEffect,
            int attrSetCode,
            int attributeCode)
        {
            _em.AddComponentData(asc, new CTagMask());
            _em.AddBuffer<BAttribute>(asc).Add(new BAttribute
            {
                AttrSetCode = attrSetCode,
                Code = attributeCode,
                BaseValue = 100f,
                CurrentValue = 100f,
                PreviousCurrentValue = 100f,
                Dirty = true,
            });
            _em.AddBuffer<BActiveModifier>(asc).Add(new BActiveModifier
            {
                AttrSetCode = attrSetCode,
                AttributeCode = attributeCode,
                SourceEntity = sourceEffect,
                Magnitude = 25f,
                Op = EModifierOp.Add,
            });
        }

        private void AddTargetDataSummary(Entity request, Entity hitEntity)
        {
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
                HitEntity = hitEntity,
                Position = new float3(4f, 5f, 6f),
                Normal = new float3(0f, 1f, 0f),
                SurfaceCode = 7,
            });
        }

        private void AssertEffectForTarget(NativeArray<Entity> effects, Entity target, Entity hitEntity)
        {
            var effect = Entity.Null;
            for (var i = 0; i < effects.Length; i++)
            {
                var context = _em.GetComponentData<CEffectContext>(effects[i]);
                if (context.TargetAsc == target)
                {
                    Assert.That(context.TargetDataKind, Is.EqualTo(ETargetDataKind.EntityList));
                    effect = effects[i];
                    break;
                }
            }

            Assert.That(effect, Is.Not.EqualTo(Entity.Null));
            AssertTargetDataSummary(effect, hitEntity);
        }

        private void AssertTargetDataSummary(Entity effect, Entity hitEntity)
        {
            Assert.That(_em.HasBuffer<BEffectTargetPoint>(effect), Is.True);
            Assert.That(_em.HasBuffer<BEffectTargetDirection>(effect), Is.True);
            Assert.That(_em.HasBuffer<BEffectTargetHit>(effect), Is.True);

            var points = _em.GetBuffer<BEffectTargetPoint>(effect);
            Assert.That(points.Length, Is.EqualTo(1));
            Assert.That(points[0].Position, Is.EqualTo(new float3(1f, 2f, 3f)));

            var directions = _em.GetBuffer<BEffectTargetDirection>(effect);
            Assert.That(directions.Length, Is.EqualTo(1));
            Assert.That(directions[0].Direction, Is.EqualTo(new float3(0f, 0f, 1f)));

            var hits = _em.GetBuffer<BEffectTargetHit>(effect);
            Assert.That(hits.Length, Is.EqualTo(1));
            Assert.That(hits[0].HitEntity, Is.EqualTo(hitEntity));
            Assert.That(hits[0].Position, Is.EqualTo(new float3(4f, 5f, 6f)));
            Assert.That(hits[0].Normal, Is.EqualTo(new float3(0f, 1f, 0f)));
            Assert.That(hits[0].SurfaceCode, Is.EqualTo(7));
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

        private void RegisterEmptyEffect(int effectCode)
        {
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode
                    ? new GameplayEffectConfig(System.Array.Empty<GameplayEffectComponentConfig>())
                    : null);
        }

        private Entity FindSingleEffectByCode(int effectCode)
        {
            using var effects = FindEffectsByCode(effectCode);
            Assert.That(effects.Length, Is.EqualTo(1));
            return effects[0];
        }

        private NativeArray<Entity> FindEffectsByCode(int effectCode)
        {
            using var query = _em.CreateEntityQuery(
                ComponentType.ReadOnly<CEffectSpecData>(),
                ComponentType.ReadOnly<CEffectContext>());
            using var candidates = query.ToEntityArray(Allocator.Temp);
            var matches = new NativeList<Entity>(Allocator.Temp);

            for (var i = 0; i < candidates.Length; i++)
            {
                var spec = _em.GetComponentData<CEffectSpecData>(candidates[i]);
                if (spec.GameplayEffectCode == effectCode)
                    matches.Add(candidates[i]);
            }

            return matches.ToArray(Allocator.Temp);
        }

        private void DestroyEffectsByCode(int effectCode)
        {
            using var effects = FindEffectsByCode(effectCode);
            for (var i = 0; i < effects.Length; i++)
                DestroyIfExists(effects[i]);
        }

        private void RunCommandGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>().Update();
        }

        private void RunAttributeGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASAttributeGroup>().Update();
        }

        private void ClearAttributeChangeEvents()
        {
            if (_em.Exists(GASManager.EntityEventBus)
                && _em.HasBuffer<BAttributeChangeEvent>(GASManager.EntityEventBus))
            {
                _em.GetBuffer<BAttributeChangeEvent>(GASManager.EntityEventBus).Clear();
            }
        }

        private BAttributeChangeEvent FindAttributeChangeEvent(Entity asc, int attrSetCode, int attributeCode)
        {
            var events = _em.GetBuffer<BAttributeChangeEvent>(GASManager.EntityEventBus);
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.ASC == asc
                    && evt.AttrSetCode == attrSetCode
                    && evt.AttributeCode == attributeCode)
                {
                    return evt;
                }
            }

            Assert.Fail("Expected attribute change event was not found.");
            return default;
        }

        private void DestroyIfExists(Entity entity)
        {
            if (entity != Entity.Null && _em.Exists(entity))
                _em.DestroyEntity(entity);
        }
    }

    public sealed class GameplayEffectPrototypeRegistryTests
    {
        private readonly List<Entity> _created = new();
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
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
            AbilityConfigRegistry.RegisterGetConfigByIDFunc(null);
            GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(null);
            TimelineAbilityConfigRegistry.RegisterGetConfigByIDFunc(null);
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                var entity = _created[i];
                if (_em.Exists(entity))
                    _em.DestroyEntity(entity);
            }

            _created.Clear();
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
            AbilityConfigRegistry.RegisterGetConfigByIDFunc(null);
            GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(null);
            TimelineAbilityConfigRegistry.RegisterGetConfigByIDFunc(null);
        }

        [Test]
        public void ApplyRequestUsesCachedPrototypeForRepeatedEffectCode()
        {
            const int effectCode = 98001;
            var configLookupCount = 0;
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
            {
                configLookupCount++;
                return id == effectCode ? CreateDurationModifierConfig() : null;
            });

            var source = CreateEntity();
            var targetA = CreateEntity();
            var targetB = CreateEntity();

            try
            {
                CreateRequest(effectCode, source, targetA, level: 1);
                RunCommandGroup();
                CreateRequest(effectCode, source, targetB, level: 2);
                RunCommandGroup();

                Assert.That(configLookupCount, Is.EqualTo(1));
                Assert.That(GameplayEffectConfigRegistry.CachedPrototypeCount, Is.EqualTo(1));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out var prototype), Is.True);
                Assert.That(_em.Exists(prototype), Is.True);
                Assert.That(_em.GetComponentData<CGameplayEffectPrototype>(prototype).GameplayEffectCode,
                    Is.EqualTo(effectCode));

                using var effects = FindEffectsByCode(effectCode);
                Assert.That(effects.Length, Is.EqualTo(2));
                for (var i = 0; i < effects.Length; i++)
                {
                    Assert.That(_em.HasComponent<CGameplayEffectPrototype>(effects[i]), Is.False);
                    Assert.That(_em.HasComponent<CEffectContext>(effects[i]), Is.True);
                    Assert.That(_em.HasComponent<CEffectSpecData>(effects[i]), Is.True);
                    Assert.That(_em.HasComponent<CEffectLifecycle>(effects[i]), Is.True);
                    Assert.That(_em.HasComponent<CDurationDefinition>(effects[i]), Is.True);
                    Assert.That(_em.HasComponent<CDurationRuntime>(effects[i]), Is.True);
                    Assert.That(_em.HasBuffer<BModifierConfig>(effects[i]), Is.True);
                }
            }
            finally
            {
                DestroyEffectsByCode(effectCode);
            }
        }

        [Test]
        public void ApplyRequestCachesStaticDefinitionBlobWithPrototype()
        {
            const int effectCode = 98010;
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode ? CreateDurationModifierConfig() : null);

            var source = CreateEntity();
            var target = CreateEntity();

            try
            {
                CreateRequest(effectCode, source, target, level: 1);
                RunCommandGroup();

                Assert.That(GameplayEffectConfigRegistry.CachedPrototypeCount, Is.EqualTo(1));
                Assert.That(GameplayEffectConfigRegistry.CachedStaticDefinitionBlobCount, Is.EqualTo(1));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(effectCode, out var blob),
                    Is.True);
                Assert.That(blob.IsCreated, Is.True);

                ref var definition = ref blob.Value;
                Assert.That(definition.GameplayEffectCode, Is.EqualTo(effectCode));
                Assert.That(definition.HasDuration, Is.True);
                Assert.That(definition.Duration.Duration, Is.EqualTo(30));
                Assert.That(definition.Modifiers.Length, Is.EqualTo(1));
                Assert.That(definition.Modifiers[0].MagnitudeSource, Is.EqualTo(EMagnitudeSource.Constant));
            }
            finally
            {
                DestroyEffectsByCode(effectCode);
            }
        }

        [Test]
        public void RegisterGetConfigClearsStaticDefinitionBlobCache()
        {
            const int effectCode = 98011;
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode ? CreateDurationModifierConfig() : null);

            var source = CreateEntity();
            var target = CreateEntity();

            try
            {
                CreateRequest(effectCode, source, target, level: 1);
                RunCommandGroup();

                Assert.That(GameplayEffectConfigRegistry.CachedPrototypeCount, Is.EqualTo(1));
                Assert.That(GameplayEffectConfigRegistry.CachedStaticDefinitionBlobCount, Is.EqualTo(1));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(effectCode, out _),
                    Is.True);

                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);

                Assert.That(GameplayEffectConfigRegistry.CachedPrototypeCount, Is.EqualTo(0));
                Assert.That(GameplayEffectConfigRegistry.CachedStaticDefinitionBlobCount, Is.EqualTo(0));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(effectCode, out _),
                    Is.False);
            }
            finally
            {
                DestroyEffectsByCode(effectCode);
            }
        }

        [Test]
        public void RegisterGetConfigClearsPrototypeEntityAndRebuildsDefinitionFromNewConfig()
        {
            const int effectCode = 98019;
            var duration = 30;
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode ? CreateDurationModifierConfig(duration) : null);

            var source = CreateEntity();
            var targetA = CreateEntity();
            var targetB = CreateEntity();

            try
            {
                CreateRequest(effectCode, source, targetA, level: 1);
                RunCommandGroup();

                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out var firstPrototype),
                    Is.True);
                Assert.That(_em.Exists(firstPrototype), Is.True);
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(effectCode, out var firstBlob),
                    Is.True);
                Assert.That(firstBlob.Value.Duration.Duration, Is.EqualTo(30));

                duration = 60;
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                    id == effectCode ? CreateDurationModifierConfig(duration) : null);

                Assert.That(_em.Exists(firstPrototype), Is.False);
                Assert.That(GameplayEffectConfigRegistry.CachedPrototypeCount, Is.EqualTo(0));
                Assert.That(GameplayEffectConfigRegistry.CachedStaticDefinitionBlobCount, Is.EqualTo(0));

                CreateRequest(effectCode, source, targetB, level: 1);
                RunCommandGroup();

                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out var rebuiltPrototype),
                    Is.True);
                Assert.That(_em.Exists(rebuiltPrototype), Is.True);
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(effectCode, out var rebuiltBlob),
                    Is.True);
                Assert.That(rebuiltBlob.Value.Duration.Duration, Is.EqualTo(60));

                var effectB = FindEffectForTarget(effectCode, targetB);
                Assert.That(_em.GetComponentData<CDurationDefinition>(effectB).Duration, Is.EqualTo(60));
            }
            finally
            {
                DestroyEffectsByCode(effectCode);
            }
        }

        [Test]
        public void DestroyedCachedPrototypeIsEvictedWithStaticDefinitionBlob()
        {
            const int effectCode = 98020;
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode ? CreateDurationModifierConfig() : null);

            var source = CreateEntity();
            var target = CreateEntity();

            try
            {
                CreateRequest(effectCode, source, target, level: 1);
                RunCommandGroup();

                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out var prototype),
                    Is.True);
                Assert.That(_em.Exists(prototype), Is.True);
                Assert.That(GameplayEffectConfigRegistry.CachedStaticDefinitionBlobCount, Is.EqualTo(1));

                _em.DestroyEntity(prototype);

                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out var stalePrototype),
                    Is.False);
                Assert.That(stalePrototype, Is.EqualTo(Entity.Null));
                Assert.That(GameplayEffectConfigRegistry.CachedPrototypeCount, Is.EqualTo(0));
                Assert.That(GameplayEffectConfigRegistry.CachedStaticDefinitionBlobCount, Is.EqualTo(0));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(effectCode, out _),
                    Is.False);
            }
            finally
            {
                DestroyEffectsByCode(effectCode);
            }
        }

        [Test]
        public void ApplyRequestRebuildsStaticDefinitionBlobWhenCachedPrototypeWasDestroyed()
        {
            const int effectCode = 98012;
            var configLookupCount = 0;
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
            {
                if (id != effectCode)
                    return null;

                configLookupCount++;
                return CreateDurationModifierConfig();
            });

            var source = CreateEntity();
            var targetA = CreateEntity();
            var targetB = CreateEntity();

            try
            {
                CreateRequest(effectCode, source, targetA, level: 1);
                RunCommandGroup();

                Assert.That(configLookupCount, Is.EqualTo(1));
                Assert.That(GameplayEffectConfigRegistry.CachedPrototypeCount, Is.EqualTo(1));
                Assert.That(GameplayEffectConfigRegistry.CachedStaticDefinitionBlobCount, Is.EqualTo(1));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out var firstPrototype),
                    Is.True);
                Assert.That(_em.Exists(firstPrototype), Is.True);
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(effectCode, out var firstBlob),
                    Is.True);
                Assert.That(firstBlob.IsCreated, Is.True);

                _em.DestroyEntity(firstPrototype);

                CreateRequest(effectCode, source, targetB, level: 1);
                RunCommandGroup();

                Assert.That(configLookupCount, Is.EqualTo(2));
                Assert.That(GameplayEffectConfigRegistry.CachedPrototypeCount, Is.EqualTo(1));
                Assert.That(GameplayEffectConfigRegistry.CachedStaticDefinitionBlobCount, Is.EqualTo(1));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out var rebuiltPrototype),
                    Is.True);
                Assert.That(rebuiltPrototype, Is.Not.EqualTo(firstPrototype));
                Assert.That(_em.Exists(rebuiltPrototype), Is.True);
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(effectCode, out var rebuiltBlob),
                    Is.True);
                Assert.That(rebuiltBlob.IsCreated, Is.True);
                Assert.That(rebuiltBlob.Value.GameplayEffectCode, Is.EqualTo(effectCode));

                using var effects = FindEffectsByCode(effectCode);
                Assert.That(effects.Length, Is.EqualTo(2));
            }
            finally
            {
                DestroyEffectsByCode(effectCode);
            }
        }

        [Test]
        public void ShouldRejectReadsApplicationAndImmunityRequirementsFromStaticDefinitionBlob()
        {
            const int effectCode = 98013;
            const int requiredTagCode = 9801301;
            const int immunityTagCode = 9801302;
            InitTagMap(requiredTagCode, immunityTagCode);
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode ? CreateApplicationAndImmunityRequirementConfig(requiredTagCode, immunityTagCode) : null);

            var effect = Entity.Null;
            var target = CreateEntity();
            _em.AddComponentData(target, new CTagMask());
            AddTagsToAsc(target, requiredTagCode);

            try
            {
                effect = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(_em, effectCode);
                _created.Add(effect);
                Assert.That(effect, Is.Not.EqualTo(Entity.Null));
                Assert.That(_em.HasComponent<CApplicationRequiredTags>(effect), Is.True);
                Assert.That(_em.HasComponent<CEffectImmunityTags>(effect), Is.True);

                _em.RemoveComponent<CApplicationRequiredTags>(effect);
                _em.RemoveComponent<CEffectImmunityTags>(effect);
                Assert.That(_em.HasComponent<CApplicationRequiredTags>(effect), Is.False);
                Assert.That(_em.HasComponent<CEffectImmunityTags>(effect), Is.False);

                _em.AddComponentData(effect, new CEffectSpecData
                {
                    GameplayEffectCode = effectCode,
                    Level = 1,
                    StackCount = 1,
                });

                Assert.That(EffectRuntimeUtility.TryGetStaticDefinitionBlob(_em, effect, out var blob), Is.True);
                Assert.That(blob.Value.HasApplicationRequiredTags, Is.True);
                Assert.That(blob.Value.HasImmunityTags, Is.True);

                Assert.That(EffectRuntimeUtility.ShouldReject(_em, target, effect, out var allowedEvaluation), Is.False);
                Assert.That(allowedEvaluation.Passed, Is.True);

                AddTagsToAsc(target, requiredTagCode, immunityTagCode);
                Assert.That(EffectRuntimeUtility.ShouldReject(_em, target, effect, out var immunityEvaluation), Is.True);
                Assert.That(
                    immunityEvaluation.Failure,
                    Is.EqualTo(ETagRequirementFailure.ImmunityTagsMatched));

                _em.SetComponentData(target, new CTagMask());
                Assert.That(EffectRuntimeUtility.ShouldReject(_em, target, effect, out var requiredEvaluation), Is.True);
                Assert.That(
                    requiredEvaluation.Failure,
                    Is.EqualTo(ETagRequirementFailure.RequiredTagsNotMet));
            }
            finally
            {
                if (effect != Entity.Null && _em.Exists(effect))
                    _em.DestroyEntity(effect);
            }
        }

        [Test]
        public void RemoveGameplayEffectsWithTagsReadsRequirementFromStaticDefinitionBlob()
        {
            const int activeEffectCode = 9801402;
            const int incomingEffectCode = 98014;
            const int removableTagCode = 9801401;
            InitTagMap(removableTagCode);
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == incomingEffectCode ? CreateRemoveGameplayEffectsWithTagsConfig(removableTagCode) :
                id == activeEffectCode ? CreateAssetTagsConfig(removableTagCode) :
                null);

            var source = CreateEntity();
            var target = CreateTargetAsc();
            var activeEffect = Entity.Null;
            var incoming = Entity.Null;

            try
            {
                activeEffect = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(_em, activeEffectCode);
                _created.Add(activeEffect);
                Assert.That(activeEffect, Is.Not.EqualTo(Entity.Null));
                _em.AddComponentData(activeEffect, new CEffectSpecData
                {
                    GameplayEffectCode = activeEffectCode,
                    Level = 1,
                    StackCount = 1,
                });
                Assert.That(EffectRuntimeUtility.TryGetStaticDefinitionBlob(_em, activeEffect, out var activeBlob),
                    Is.True);
                AssertMaskContainsOnlyTagCode(activeBlob.Value.AssetTags, removableTagCode);

                _em.GetBuffer<BGameplayEffect>(target).Add(new BGameplayEffect
                {
                    GameplayEffect = activeEffect,
                });

                incoming = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(_em, incomingEffectCode);
                _created.Add(incoming);
                Assert.That(incoming, Is.Not.EqualTo(Entity.Null));
                Assert.That(_em.HasComponent<CRemoveEffectWithTags>(incoming), Is.True);

                _em.RemoveComponent<CRemoveEffectWithTags>(incoming);
                Assert.That(_em.HasComponent<CRemoveEffectWithTags>(incoming), Is.False);

                _em.AddComponentData(incoming, new CEffectSpecData
                {
                    GameplayEffectCode = incomingEffectCode,
                    Level = 1,
                    StackCount = 1,
                });

                Assert.That(EffectRuntimeUtility.TryGetStaticDefinitionBlob(_em, incoming, out var blob), Is.True);
                Assert.That(blob.Value.HasRemoveGameplayEffectsWithTags, Is.True);

                EffectRuntimeUtility.RemoveActiveGameplayEffectsWithTags(
                    _em,
                    incoming,
                    new CEffectContext
                    {
                        SourceAsc = source,
                        TargetAsc = target,
                        Instigator = source,
                    },
                    currentFrame: 321);

                Assert.That(_em.HasComponent<CEffectDestroy>(activeEffect), Is.True);
                var lifecycle = _em.GetComponentData<CEffectLifecycle>(activeEffect);
                Assert.That(lifecycle.State, Is.EqualTo(EGameplayEffectLifecycleState.PendingRemove));
                Assert.That(lifecycle.StateStartFrame, Is.EqualTo(321));
            }
            finally
            {
                if (incoming != Entity.Null && _em.Exists(incoming))
                    _em.DestroyEntity(incoming);
            }
        }

        [Test]
        public void GrantedAbilitiesReadDefinitionFromStaticDefinitionBlob()
        {
            const int effectCode = 98015;
            const int abilityCode = 990151;
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode ? CreateDurationGrantedAbilityDefinitionConfig(abilityCode) : null);
            AbilityConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == abilityCode ? CreateGrantedAbilityConfig(abilityCode) : null);

            var source = CreateEntity();
            var target = CreateTargetAsc();
            var effect = Entity.Null;
            var ability = Entity.Null;

            try
            {
                effect = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(_em, effectCode);
                _created.Add(effect);
                Assert.That(effect, Is.Not.EqualTo(Entity.Null));
                Assert.That(_em.HasBuffer<BGrantedAbilityConfig>(effect), Is.True);
                Assert.That(_em.HasBuffer<BGrantedAbilityRuntime>(effect), Is.True);
                Assert.That(_em.GetBuffer<BGrantedAbilityRuntime>(effect).Length, Is.EqualTo(0));

                _em.RemoveComponent<BGrantedAbilityConfig>(effect);
                Assert.That(_em.HasBuffer<BGrantedAbilityConfig>(effect), Is.False);
                Assert.That(_em.HasBuffer<BGrantedAbilityRuntime>(effect), Is.True);

                var context = new CEffectContext
                {
                    SourceAsc = source,
                    TargetAsc = target,
                    Instigator = source,
                };
                _em.AddComponentData(effect, context);
                _em.AddComponentData(effect, new CEffectSpecData
                {
                    GameplayEffectCode = effectCode,
                    Level = 1,
                    StackCount = 1,
                });

                Assert.That(EffectRuntimeUtility.TryGetStaticDefinitionBlob(_em, effect, out var blob), Is.True);
                Assert.That(blob.Value.GrantedAbilities.Length, Is.EqualTo(1));

                var duration = _em.GetComponentData<CDurationRuntime>(effect);
                EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 432);
                _em.SetComponentData(effect, duration);

                var grantedAbilities = _em.GetBuffer<BGrantedAbility>(target);
                Assert.That(grantedAbilities.Length, Is.EqualTo(1));
                ability = grantedAbilities[0].AbilityEntity;
                Assert.That(_em.Exists(ability), Is.True);

                var baseInfo = _em.GetComponentData<CAbilityBaseInfo>(ability);
                Assert.That(baseInfo.Code, Is.EqualTo(abilityCode));
                Assert.That(baseInfo.Level, Is.EqualTo(5));
                Assert.That(baseInfo.Owner, Is.EqualTo(target));

                var grantedByEffect = _em.GetComponentData<CGrantedByEffect>(ability);
                Assert.That(grantedByEffect.SourceEffect, Is.EqualTo(effect));
                Assert.That(grantedByEffect.ActivationPolicy, Is.EqualTo(GrantedAbilityActivationPolicy.None));
                Assert.That(grantedByEffect.DeactivationPolicy,
                    Is.EqualTo(GrantedAbilityDeactivationPolicy.SyncWithEffect));
                Assert.That(grantedByEffect.RemovePolicy, Is.EqualTo(GrantedAbilityRemovePolicy.SyncWithEffect));

                var runtimeAbilities = _em.GetBuffer<BGrantedAbilityRuntime>(effect);
                Assert.That(runtimeAbilities.Length, Is.EqualTo(1));
                Assert.That(runtimeAbilities[0].ConfigIndex, Is.EqualTo(0));
                Assert.That(runtimeAbilities[0].AbilityEntity, Is.EqualTo(ability));

                EffectRuntimeUtility.CleanupActiveEffect(_em, effect);

                Assert.That(_em.Exists(effect), Is.False);
                Assert.That(_em.Exists(ability), Is.False);
                Assert.That(_em.GetBuffer<BGrantedAbility>(target).Length, Is.EqualTo(0));
            }
            finally
            {
                if (ability != Entity.Null && _em.Exists(ability))
                    _em.DestroyEntity(ability);
                if (effect != Entity.Null && _em.Exists(effect))
                    _em.DestroyEntity(effect);
            }
        }

        [Test]
        public void MissingGrantedAbilityConfigDoesNotCreateRuntimeAbilityAndKeepsEffectActive()
        {
            const int effectCode = 98017;
            const int missingAbilityCode = 990171;

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode ? CreateDurationGrantedAbilityDefinitionConfig(missingAbilityCode) : null);
            AbilityConfigRegistry.RegisterGetConfigByIDFunc(_ => null);

            var source = CreateEntity();
            var target = CreateTargetAsc();
            var effect = Entity.Null;

            try
            {
                effect = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(_em, effectCode);
                _created.Add(effect);
                Assert.That(effect, Is.Not.EqualTo(Entity.Null));

                var context = new CEffectContext
                {
                    SourceAsc = source,
                    TargetAsc = target,
                    Instigator = source,
                };
                _em.AddComponentData(effect, context);
                _em.AddComponentData(effect, new CEffectSpecData
                {
                    GameplayEffectCode = effectCode,
                    Level = 1,
                    StackCount = 1,
                });

                Assert.That(EffectRuntimeUtility.TryGetStaticDefinitionBlob(_em, effect, out var blob), Is.True);
                Assert.That(blob.Value.GrantedAbilities.Length, Is.EqualTo(1));

                var duration = _em.GetComponentData<CDurationRuntime>(effect);
                EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 533);
                _em.SetComponentData(effect, duration);

                Assert.That(_em.Exists(effect), Is.True);
                Assert.That(_em.GetComponentData<CEffectLifecycle>(effect).State,
                    Is.EqualTo(EGameplayEffectLifecycleState.Active));
                Assert.That(_em.GetBuffer<BGameplayEffect>(target).Length, Is.EqualTo(1));
                Assert.That(_em.GetBuffer<BGameplayEffect>(target)[0].GameplayEffect, Is.EqualTo(effect));
                Assert.That(_em.GetBuffer<BGrantedAbility>(target).Length, Is.EqualTo(0));
                if (_em.HasBuffer<BGrantedAbilityRuntime>(effect))
                    Assert.That(_em.GetBuffer<BGrantedAbilityRuntime>(effect).Length, Is.EqualTo(0));

                Assert.That(ConfigRegistryDiagnostics.Count, Is.EqualTo(1));
                AssertMissingConfigDiagnostic(
                    ConfigRegistryDiagnostics.Entries[0],
                    ConfigRegistryConfigKind.Ability,
                    missingAbilityCode,
                    ConfigRegistryConfigKind.GameplayEffect,
                    effectCode,
                    ConfigRegistryReferenceKind.GameplayEffectGrantedAbility);
            }
            finally
            {
                if (effect != Entity.Null && _em.Exists(effect))
                    _em.DestroyEntity(effect);
            }
        }

        [Test]
        public void RuntimeInstanceDataDoesNotPolluteCachedPrototype()
        {
            const int effectCode = 98002;
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode ? CreateDurationModifierConfig() : null);

            var source = CreateEntity();
            var targetA = CreateEntity();
            var targetB = CreateEntity();

            try
            {
                var requestA = CreateRequest(effectCode, source, targetA, level: 3);
                GameplayEffectRequestWriter.AddSetByCallerValues(_em, requestA, new[]
                {
                    new BSetByCallerValue { Key = 11, Value = 111f },
                });
                AddTargetPoint(requestA, new float3(1f, 2f, 3f));

                var requestB = CreateRequest(effectCode, source, targetB, level: 7);
                GameplayEffectRequestWriter.AddSetByCallerValues(_em, requestB, new[]
                {
                    new BSetByCallerValue { Key = 22, Value = 222f },
                });
                AddTargetPoint(requestB, new float3(4f, 5f, 6f));

                RunCommandGroup();

                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out var prototype), Is.True);
                Assert.That(_em.HasComponent<CGameplayEffectPrototype>(prototype), Is.True);
                Assert.That(_em.HasComponent<CEffectContext>(prototype), Is.False);
                Assert.That(_em.HasComponent<CEffectSpecData>(prototype), Is.False);
                Assert.That(_em.HasComponent<CEffectLifecycle>(prototype), Is.False);
                Assert.That(_em.HasBuffer<BSetByCallerValue>(prototype), Is.False);
                Assert.That(_em.HasBuffer<BEffectTargetPoint>(prototype), Is.False);
                Assert.That(_em.HasBuffer<BResolvedModifier>(prototype), Is.False);

                var effectA = FindEffectForTarget(effectCode, targetA);
                var effectB = FindEffectForTarget(effectCode, targetB);
                Assert.That(effectA, Is.Not.EqualTo(effectB));

                AssertRuntimeInstance(effectA, targetA, level: 3, setByCallerKey: 11, setByCallerValue: 111f,
                    point: new float3(1f, 2f, 3f));
                AssertRuntimeInstance(effectB, targetB, level: 7, setByCallerKey: 22, setByCallerValue: 222f,
                    point: new float3(4f, 5f, 6f));
            }
            finally
            {
                DestroyEffectsByCode(effectCode);
            }
        }

        [Test]
        public void RuntimeStateAndCaptureDataDoNotPolluteCachedPrototype()
        {
            const int effectCode = 98004;
            const int grantedAbilityCode = 99004;
            const int attrSetCode = 1201;
            const int attributeCode = 2201;
            const int assetTagCode = 9800411;
            const int grantedTagCode = 9800412;
            const int applicationAllTagCode = 9800413;
            const int applicationAnyTagCode = 9800414;
            const int applicationNoneTagCode = 9800415;
            const int ongoingAllTagCode = 9800416;
            const int ongoingAnyTagCode = 9800417;
            const int ongoingNoneTagCode = 9800418;
            const int removeAllTagCode = 9800419;
            const int removeAnyTagCode = 9800420;
            const int removeNoneTagCode = 9800421;
            const int immunityAllTagCode = 9800422;
            const int immunityAnyTagCode = 9800423;
            const int immunityNoneTagCode = 9800424;
            const int periodEffectCode = 9800425;
            const int overflowEffectCode = 9800426;

            InitTagMap(
                assetTagCode,
                grantedTagCode,
                applicationAllTagCode,
                applicationAnyTagCode,
                applicationNoneTagCode,
                ongoingAllTagCode,
                ongoingAnyTagCode,
                ongoingNoneTagCode,
                removeAllTagCode,
                removeAnyTagCode,
                removeNoneTagCode,
                immunityAllTagCode,
                immunityAnyTagCode,
                immunityNoneTagCode);

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode
                    ? CreateDefinitionContractConfig(grantedAbilityCode, attrSetCode, attributeCode)
                    : null);
            AbilityConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == grantedAbilityCode
                    ? CreateGrantedAbilityConfig(grantedAbilityCode)
                    : null);

            var source = CreateAscWithAttribute(attrSetCode, attributeCode, currentValue: 42f);
            var target = CreateTargetAsc();
            AddTagsToAsc(
                target,
                applicationAllTagCode,
                applicationAnyTagCode,
                ongoingAllTagCode,
                ongoingAnyTagCode,
                immunityNoneTagCode);

            try
            {
                CreateRequest(effectCode, source, target, level: 5);
                RunCommandGroup();
                RunEffectGroup();

                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out var prototype), Is.True);
                AssertPrototypeHasOnlyDefinitionState(
                    prototype,
                    effectCode,
                    attrSetCode,
                    attributeCode,
                    grantedAbilityCode,
                    assetTagCode,
                    grantedTagCode,
                    applicationAllTagCode,
                    applicationAnyTagCode,
                    applicationNoneTagCode,
                    ongoingAllTagCode,
                    ongoingAnyTagCode,
                    ongoingNoneTagCode,
                    removeAllTagCode,
                    removeAnyTagCode,
                    removeNoneTagCode,
                    immunityAllTagCode,
                    immunityAnyTagCode,
                    immunityNoneTagCode,
                    periodEffectCode,
                    overflowEffectCode);

                var effect = FindEffectForTarget(effectCode, target);
                AssertRuntimeInstanceHasRuntimeState(effect, target, grantedAbilityCode);
            }
            finally
            {
                DestroyGrantedAbilities(target);
                DestroyEffectsByCode(effectCode);
            }
        }

        [Test]
        public void StaticDefinitionBlobBuilderCopiesPrototypeDefinitionFields()
        {
            const int effectCode = 98005;
            const int grantedAbilityCode = 99005;
            const int attrSetCode = 1201;
            const int attributeCode = 2201;
            const int assetTagCode = 9800411;
            const int grantedTagCode = 9800412;
            const int applicationAllTagCode = 9800413;
            const int applicationAnyTagCode = 9800414;
            const int applicationNoneTagCode = 9800415;
            const int ongoingAllTagCode = 9800416;
            const int ongoingAnyTagCode = 9800417;
            const int ongoingNoneTagCode = 9800418;
            const int removeAllTagCode = 9800419;
            const int removeAnyTagCode = 9800420;
            const int removeNoneTagCode = 9800421;
            const int immunityAllTagCode = 9800422;
            const int immunityAnyTagCode = 9800423;
            const int immunityNoneTagCode = 9800424;
            const int periodEffectCode = 9800425;
            const int overflowEffectCode = 9800426;

            InitTagMap(
                assetTagCode,
                grantedTagCode,
                applicationAllTagCode,
                applicationAnyTagCode,
                applicationNoneTagCode,
                ongoingAllTagCode,
                ongoingAnyTagCode,
                ongoingNoneTagCode,
                removeAllTagCode,
                removeAnyTagCode,
                removeNoneTagCode,
                immunityAllTagCode,
                immunityAnyTagCode,
                immunityNoneTagCode);

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode
                    ? CreateDefinitionContractConfig(grantedAbilityCode, attrSetCode, attributeCode)
                    : null);

            var source = CreateAscWithAttribute(attrSetCode, attributeCode, currentValue: 42f);
            var target = CreateTargetAsc();
            AddTagsToAsc(
                target,
                applicationAllTagCode,
                applicationAnyTagCode,
                ongoingAllTagCode,
                ongoingAnyTagCode,
                immunityNoneTagCode);

            BlobAssetReference<GEStaticDefinitionBlob> blob = default;
            try
            {
                CreateRequest(effectCode, source, target, level: 5);
                RunCommandGroup();

                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out var prototype), Is.True);
                blob = GEStaticDefinitionBlobBuilder.BuildFromPrototype(_em, prototype);

                Assert.That(blob.IsCreated, Is.True);
                ref var definition = ref blob.Value;
                AssertStaticDefinitionBlob(
                    ref definition,
                    effectCode,
                    attrSetCode,
                    attributeCode,
                    grantedAbilityCode,
                    assetTagCode,
                    grantedTagCode,
                    applicationAllTagCode,
                    applicationAnyTagCode,
                    applicationNoneTagCode,
                    ongoingAllTagCode,
                    ongoingAnyTagCode,
                    ongoingNoneTagCode,
                    removeAllTagCode,
                    removeAnyTagCode,
                    removeNoneTagCode,
                    immunityAllTagCode,
                    immunityAnyTagCode,
                    immunityNoneTagCode,
                    periodEffectCode,
                    overflowEffectCode);
            }
            finally
            {
                if (blob.IsCreated)
                    blob.Dispose();
                DestroyEffectsByCode(effectCode);
            }
        }

        [Test]
        public void ConfigAuthorityMapPreservesManagedConfigPrototypeAndCachedBlobDefinitionFields()
        {
            const int effectCode = 98016;
            const int grantedAbilityCode = 99016;
            const int attrSetCode = 1216;
            const int attributeCode = 2216;
            const int assetTagCode = 9800411;
            const int grantedTagCode = 9800412;
            const int applicationAllTagCode = 9800413;
            const int applicationAnyTagCode = 9800414;
            const int applicationNoneTagCode = 9800415;
            const int ongoingAllTagCode = 9800416;
            const int ongoingAnyTagCode = 9800417;
            const int ongoingNoneTagCode = 9800418;
            const int removeAllTagCode = 9800419;
            const int removeAnyTagCode = 9800420;
            const int removeNoneTagCode = 9800421;
            const int immunityAllTagCode = 9800422;
            const int immunityAnyTagCode = 9800423;
            const int immunityNoneTagCode = 9800424;
            const int periodEffectCode = 9800425;
            const int overflowEffectCode = 9800426;

            InitTagMap(
                assetTagCode,
                grantedTagCode,
                applicationAllTagCode,
                applicationAnyTagCode,
                applicationNoneTagCode,
                ongoingAllTagCode,
                ongoingAnyTagCode,
                ongoingNoneTagCode,
                removeAllTagCode,
                removeAnyTagCode,
                removeNoneTagCode,
                immunityAllTagCode,
                immunityAnyTagCode,
                immunityNoneTagCode);

            var config = CreateDefinitionContractConfig(grantedAbilityCode, attrSetCode, attributeCode);
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode ? config : null);

            var source = CreateAscWithAttribute(attrSetCode, attributeCode, currentValue: 42f);
            var target = CreateTargetAsc();
            AddTagsToAsc(
                target,
                applicationAllTagCode,
                applicationAnyTagCode,
                ongoingAllTagCode,
                ongoingAnyTagCode,
                immunityNoneTagCode);

            try
            {
                Assert.That(GameplayEffectConfigRegistry.GetConfigByID(effectCode), Is.SameAs(config));
                AssertManagedGameplayEffectConfigDefinition(
                    config,
                    attrSetCode,
                    attributeCode,
                    grantedAbilityCode,
                    assetTagCode,
                    grantedTagCode,
                    applicationAllTagCode,
                    applicationAnyTagCode,
                    applicationNoneTagCode,
                    ongoingAllTagCode,
                    ongoingAnyTagCode,
                    ongoingNoneTagCode,
                    removeAllTagCode,
                    removeAnyTagCode,
                    removeNoneTagCode,
                    immunityAllTagCode,
                    immunityAnyTagCode,
                    immunityNoneTagCode,
                    periodEffectCode,
                    overflowEffectCode);

                CreateRequest(effectCode, source, target, level: 5);
                RunCommandGroup();

                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out var prototype), Is.True);
                AssertPrototypeHasOnlyDefinitionState(
                    prototype,
                    effectCode,
                    attrSetCode,
                    attributeCode,
                    grantedAbilityCode,
                    assetTagCode,
                    grantedTagCode,
                    applicationAllTagCode,
                    applicationAnyTagCode,
                    applicationNoneTagCode,
                    ongoingAllTagCode,
                    ongoingAnyTagCode,
                    ongoingNoneTagCode,
                    removeAllTagCode,
                    removeAnyTagCode,
                    removeNoneTagCode,
                    immunityAllTagCode,
                    immunityAnyTagCode,
                    immunityNoneTagCode,
                    periodEffectCode,
                    overflowEffectCode);

                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(effectCode, out var blob),
                    Is.True);
                ref var definition = ref blob.Value;
                AssertStaticDefinitionBlob(
                    ref definition,
                    effectCode,
                    attrSetCode,
                    attributeCode,
                    grantedAbilityCode,
                    assetTagCode,
                    grantedTagCode,
                    applicationAllTagCode,
                    applicationAnyTagCode,
                    applicationNoneTagCode,
                    ongoingAllTagCode,
                    ongoingAnyTagCode,
                    ongoingNoneTagCode,
                    removeAllTagCode,
                    removeAnyTagCode,
                    removeNoneTagCode,
                    immunityAllTagCode,
                    immunityAnyTagCode,
                    immunityNoneTagCode,
                    periodEffectCode,
                    overflowEffectCode);
            }
            finally
            {
                DestroyEffectsByCode(effectCode);
            }
        }

        [Test]
        public void GeneratedGameplayEffectAdapterPreservesFieldsIntoPrototypeAndStaticDefinitionBlob()
        {
            const int effectCode = 98021;
            const int grantedAbilityCode = 99021;
            const int attrSetCode = 1221;
            const int attributeCode = 2221;
            const int assetTagCode = 9802111;
            const int grantedTagCode = 9802112;
            const int applicationAllTagCode = 9802113;
            const int applicationAnyTagCode = 9802114;
            const int applicationNoneTagCode = 9802115;
            const int ongoingAllTagCode = 9802116;
            const int ongoingAnyTagCode = 9802117;
            const int ongoingNoneTagCode = 9802118;
            const int removeAllTagCode = 9802119;
            const int removeAnyTagCode = 9802120;
            const int removeNoneTagCode = 9802121;
            const int immunityAllTagCode = 9802122;
            const int immunityAnyTagCode = 9802123;
            const int immunityNoneTagCode = 9802124;
            const int periodEffectCode = 9802125;
            const int overflowEffectCode = 9802126;

            InitTagMap(
                assetTagCode,
                grantedTagCode,
                applicationAllTagCode,
                applicationAnyTagCode,
                applicationNoneTagCode,
                ongoingAllTagCode,
                ongoingAnyTagCode,
                ongoingNoneTagCode,
                removeAllTagCode,
                removeAnyTagCode,
                removeNoneTagCode,
                immunityAllTagCode,
                immunityAnyTagCode,
                immunityNoneTagCode);

            var generatedConfig = CreateConfigFromGeneratedRow(new GeneratedGameplayEffectRow
            {
                AssetTags = new List<int> { assetTagCode },
                GrantedTags = new List<int> { grantedTagCode },
                ApplicationRequiredTags = new GeneratedTagRequirementSpec
                {
                    All = new List<int> { applicationAllTagCode, 0 },
                    Any = new List<int> { applicationAnyTagCode },
                    None = new List<int> { applicationNoneTagCode },
                },
                OngoingRequiredTags = new GeneratedTagRequirementSpec
                {
                    All = new List<int> { ongoingAllTagCode },
                    Any = new List<int> { ongoingAnyTagCode, 0 },
                    None = new List<int> { ongoingNoneTagCode },
                },
                RemoveGameplayEffectsWithTags = new GeneratedTagRequirementSpec
                {
                    All = new List<int> { removeAllTagCode },
                    Any = new List<int> { removeAnyTagCode },
                    None = new List<int> { removeNoneTagCode, 0 },
                },
                ImmunityTags = new GeneratedTagRequirementSpec
                {
                    All = new List<int> { immunityAllTagCode },
                    Any = new List<int> { immunityAnyTagCode },
                    None = new List<int> { immunityNoneTagCode },
                },
                Duration = new GeneratedDurationRow
                {
                    TimeUnit = (int)TimeUnit.Frame,
                    Time = 30,
                    ResetStartTimeWhenActivated = true,
                },
                Period = new GeneratedPeriodRow
                {
                    Time = 5,
                    Effects = new[] { periodEffectCode },
                    FirstTrigger = true,
                },
                Modifiers = new List<GeneratedModifierRow>
                {
                    new()
                    {
                        AttrSet = attrSetCode,
                        Attribute = attributeCode,
                        Magnitude = 1f,
                        Operation = (int)EModifierOp.Add,
                    },
                },
                GrantedAbility = new List<GeneratedGrantedAbilityRow>
                {
                    new()
                    {
                        ID = grantedAbilityCode,
                        Level = 2,
                        ActivationPolicy = (int)GrantedAbilityActivationPolicy.None,
                        DeactivationPolicy = (int)GrantedAbilityDeactivationPolicy.None,
                        RemovePolicy = (int)GrantedAbilityRemovePolicy.SyncWithEffect,
                    },
                },
                Stacking = new GeneratedStackingRow
                {
                    StackingType = (int)EffectStackType.AggregateByTarget,
                    StackCode = 9800402,
                    LimitCount = 3,
                    DurationRefreshPolicy = (int)EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication,
                    PeriodResetPolicy = (int)EffectPeriodResetPolicy.ResetOnSuccessfulApplication,
                    ExpirationPolicy = (int)EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration,
                    DenyOverflowApplication = false,
                    ClearStackOnOverflow = false,
                    OverflowEffects = new[] { overflowEffectCode },
                },
            });
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id => id == effectCode ? generatedConfig : null);

            var source = CreateAscWithAttribute(attrSetCode, attributeCode, currentValue: 42f);
            var target = CreateTargetAsc();
            AddTagsToAsc(
                target,
                applicationAllTagCode,
                applicationAnyTagCode,
                ongoingAllTagCode,
                ongoingAnyTagCode,
                immunityNoneTagCode);

            try
            {
                var config = GameplayEffectConfigRegistry.GetConfigByID(effectCode);
                AssertManagedGameplayEffectConfigDefinition(
                    config,
                    attrSetCode,
                    attributeCode,
                    grantedAbilityCode,
                    assetTagCode,
                    grantedTagCode,
                    applicationAllTagCode,
                    applicationAnyTagCode,
                    applicationNoneTagCode,
                    ongoingAllTagCode,
                    ongoingAnyTagCode,
                    ongoingNoneTagCode,
                    removeAllTagCode,
                    removeAnyTagCode,
                    removeNoneTagCode,
                    immunityAllTagCode,
                    immunityAnyTagCode,
                    immunityNoneTagCode,
                    periodEffectCode,
                    overflowEffectCode,
                    expectsMagnitudeDefinition: false,
                    stopTickWhenDeactivated: false);

                CreateRequest(effectCode, source, target, level: 5);
                RunCommandGroup();

                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out var prototype), Is.True);
                AssertPrototypeHasOnlyDefinitionState(
                    prototype,
                    effectCode,
                    attrSetCode,
                    attributeCode,
                    grantedAbilityCode,
                    assetTagCode,
                    grantedTagCode,
                    applicationAllTagCode,
                    applicationAnyTagCode,
                    applicationNoneTagCode,
                    ongoingAllTagCode,
                    ongoingAnyTagCode,
                    ongoingNoneTagCode,
                    removeAllTagCode,
                    removeAnyTagCode,
                    removeNoneTagCode,
                    immunityAllTagCode,
                    immunityAnyTagCode,
                    immunityNoneTagCode,
                    periodEffectCode,
                    overflowEffectCode,
                    expectsMagnitudeDefinition: false,
                    stopTickWhenDeactivated: false);

                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(effectCode, out var blob),
                    Is.True);
                ref var definition = ref blob.Value;
                AssertStaticDefinitionBlob(
                    ref definition,
                    effectCode,
                    attrSetCode,
                    attributeCode,
                    grantedAbilityCode,
                    assetTagCode,
                    grantedTagCode,
                    applicationAllTagCode,
                    applicationAnyTagCode,
                    applicationNoneTagCode,
                    ongoingAllTagCode,
                    ongoingAnyTagCode,
                    ongoingNoneTagCode,
                    removeAllTagCode,
                    removeAnyTagCode,
                    removeNoneTagCode,
                    immunityAllTagCode,
                    immunityAnyTagCode,
                    immunityNoneTagCode,
                    periodEffectCode,
                    overflowEffectCode,
                    expectsSourceAttributeMagnitude: false,
                    stopTickWhenDeactivated: false);
            }
            finally
            {
                DestroyEffectsByCode(effectCode);
            }
        }

        [Test]
        public void StaticDefinitionBlobBuilderRequiresPrototypeMarker()
        {
            const int effectCode = 98006;

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode ? CreateDurationModifierConfig() : null);

            var source = CreateEntity();
            var target = CreateEntity();
            var ordinaryEntity = CreateEntity();
            _em.AddComponentData(ordinaryEntity, new CDurationDefinition
            {
                Duration = 99,
                TimeUnit = TimeUnit.Frame,
            });

            BlobAssetReference<GEStaticDefinitionBlob> runtimeBlob = default;
            BlobAssetReference<GEStaticDefinitionBlob> ordinaryBlob = default;
            BlobAssetReference<GEStaticDefinitionBlob> nullBlob = default;
            try
            {
                CreateRequest(effectCode, source, target, level: 1);
                RunCommandGroup();

                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out var prototype), Is.True);
                Assert.That(_em.HasComponent<CGameplayEffectPrototype>(prototype), Is.True);

                using var effects = FindEffectsByCode(effectCode);
                Assert.That(effects.Length, Is.EqualTo(1));
                Assert.That(_em.HasComponent<CGameplayEffectPrototype>(effects[0]), Is.False);

                runtimeBlob = GEStaticDefinitionBlobBuilder.BuildFromPrototype(_em, effects[0]);
                ordinaryBlob = GEStaticDefinitionBlobBuilder.BuildFromPrototype(_em, ordinaryEntity);
                nullBlob = GEStaticDefinitionBlobBuilder.BuildFromPrototype(_em, Entity.Null);

                Assert.That(runtimeBlob.IsCreated, Is.False);
                Assert.That(ordinaryBlob.IsCreated, Is.False);
                Assert.That(nullBlob.IsCreated, Is.False);
            }
            finally
            {
                if (runtimeBlob.IsCreated)
                    runtimeBlob.Dispose();
                if (ordinaryBlob.IsCreated)
                    ordinaryBlob.Dispose();
                if (nullBlob.IsCreated)
                    nullBlob.Dispose();
                DestroyEffectsByCode(effectCode);
            }
        }

        [Test]
        public void StaticDefinitionBlobBuilderHandlesPrototypeWithoutOptionalDefinitions()
        {
            const int effectCode = 98007;

            var prototype = CreateEntity();
            _em.AddComponentData(prototype, new CGameplayEffectPrototype
            {
                GameplayEffectCode = effectCode,
            });

            BlobAssetReference<GEStaticDefinitionBlob> blob = default;
            try
            {
                blob = GEStaticDefinitionBlobBuilder.BuildFromPrototype(_em, prototype);

                Assert.That(blob.IsCreated, Is.True);
                ref var definition = ref blob.Value;
                Assert.That(definition.GameplayEffectCode, Is.EqualTo(effectCode));
                AssertBlobHasNoOptionalDefinitions(ref definition);
            }
            finally
            {
                if (blob.IsCreated)
                    blob.Dispose();
            }
        }

        [Test]
        public void StaticDefinitionBlobBuilderUsesConstantModifierWhenMagnitudeDefinitionIsMissing()
        {
            const int effectCode = 98008;
            const int attrSetCode = 1301;
            const int attributeCode = 2301;

            var prototype = CreateEntity();
            _em.AddComponentData(prototype, new CGameplayEffectPrototype
            {
                GameplayEffectCode = effectCode,
            });
            _em.AddBuffer<BModifierConfig>(prototype).Add(new BModifierConfig
            {
                AttrSetCode = attrSetCode,
                AttributeCode = attributeCode,
                Op = EModifierOp.Add,
                Magnitude = 7f,
            });

            BlobAssetReference<GEStaticDefinitionBlob> blob = default;
            try
            {
                blob = GEStaticDefinitionBlobBuilder.BuildFromPrototype(_em, prototype);

                Assert.That(blob.IsCreated, Is.True);
                ref var definition = ref blob.Value;
                Assert.That(definition.GameplayEffectCode, Is.EqualTo(effectCode));
                AssertBlobHasConstantModifierDefinition(ref definition.Modifiers, attrSetCode, attributeCode);
                Assert.That(definition.GrantedAbilities.Length, Is.EqualTo(0));
            }
            finally
            {
                if (blob.IsCreated)
                    blob.Dispose();
            }
        }

        [Test]
        public void StaticDefinitionBlobBuilderPreservesMultiValueDefinitions()
        {
            const int effectCode = 98009;
            const int assetTagA = 9800901;
            const int assetTagB = 9800902;
            const int grantedTagA = 9800903;
            const int grantedTagB = 9800904;
            const int requirementAllA = 9800905;
            const int requirementAllB = 9800906;
            const int requirementAnyA = 9800907;
            const int requirementAnyB = 9800908;
            const int requirementNoneA = 9800909;
            const int requirementNoneB = 9800910;
            const int periodEffectA = 9800911;
            const int periodEffectB = 9800912;
            const int overflowEffectA = 9800913;
            const int overflowEffectB = 9800914;
            const int grantedAbilityA = 990091;
            const int grantedAbilityB = 990092;

            InitTagMap(
                assetTagA,
                assetTagB,
                grantedTagA,
                grantedTagB,
                requirementAllA,
                requirementAllB,
                requirementAnyA,
                requirementAnyB,
                requirementNoneA,
                requirementNoneB);

            var prototype = CreateEntity();
            _em.AddComponentData(prototype, new CGameplayEffectPrototype
            {
                GameplayEffectCode = effectCode,
            });
            _em.AddComponentData(prototype, new CEffectAssetTags
            {
                Tags = CreateMaskFromTagCodes(assetTagA, assetTagB),
            });
            var grantedTags = _em.AddBuffer<BGrantedTagConfig>(prototype);
            AddGrantedTagConfig(grantedTags, grantedTagA);
            AddGrantedTagConfig(grantedTags, grantedTagB);

            var requirement = CreateRequirementMask(
                new[] { requirementAllA, requirementAllB },
                new[] { requirementAnyA, requirementAnyB },
                new[] { requirementNoneA, requirementNoneB });
            _em.AddComponentData(prototype, new CApplicationRequiredTags { requirement = requirement });
            _em.AddComponentData(prototype, new COngoingRequiredTags { requirement = requirement });
            _em.AddComponentData(prototype, new CRemoveEffectWithTags { requirement = requirement });
            _em.AddComponentData(prototype, new CEffectImmunityTags { requirement = requirement });

            _em.AddComponentData(prototype, new CPeriodDefinition
            {
                Period = 11,
                ResetTimeCountWhenDeactivated = true,
            });
            var periodEffects = _em.AddBuffer<BPeriodGEConfig>(prototype);
            periodEffects.Add(new BPeriodGEConfig { GameplayEffectCode = periodEffectA });
            periodEffects.Add(new BPeriodGEConfig { GameplayEffectCode = periodEffectB });

            _em.AddComponentData(prototype, new CStackingDefinition
            {
                StackType = EffectStackType.AggregateBySource,
                StackingCode = 9800900,
                LimitCount = 5,
                EffectDurationRefreshPolicy = EffectDurationRefreshPolicy.NeverRefresh,
                EffectPeriodResetPolicy = EffectPeriodResetPolicy.NeverRefresh,
                EffectExpirationPolicy = EffectExpirationPolicy.ClearEntireStack,
                DenyOverflowApplication = true,
                ClearStackOnOverflow = true,
            });
            var overflowEffects = _em.AddBuffer<BOverflowGEConfig>(prototype);
            overflowEffects.Add(new BOverflowGEConfig { GameplayEffectCode = overflowEffectA });
            overflowEffects.Add(new BOverflowGEConfig { GameplayEffectCode = overflowEffectB });

            var grantedAbilities = _em.AddBuffer<BGrantedAbilityConfig>(prototype);
            grantedAbilities.Add(new BGrantedAbilityConfig
            {
                AbilityCode = grantedAbilityA,
                Level = 1,
                ActivationPolicy = GrantedAbilityActivationPolicy.WhenAdded,
                DeactivationPolicy = GrantedAbilityDeactivationPolicy.None,
                RemovePolicy = GrantedAbilityRemovePolicy.WhenEnd,
            });
            grantedAbilities.Add(new BGrantedAbilityConfig
            {
                AbilityCode = grantedAbilityB,
                Level = 3,
                ActivationPolicy = GrantedAbilityActivationPolicy.SyncWithEffect,
                DeactivationPolicy = GrantedAbilityDeactivationPolicy.SyncWithEffect,
                RemovePolicy = GrantedAbilityRemovePolicy.WhenCancelOrEnd,
            });

            BlobAssetReference<GEStaticDefinitionBlob> blob = default;
            try
            {
                blob = GEStaticDefinitionBlobBuilder.BuildFromPrototype(_em, prototype);

                Assert.That(blob.IsCreated, Is.True);
                ref var definition = ref blob.Value;
                Assert.That(definition.GameplayEffectCode, Is.EqualTo(effectCode));
                AssertMaskContainsOnlyTagCodes(definition.AssetTags, assetTagA, assetTagB);
                AssertMaskContainsOnlyTagCodes(definition.GrantedTags, grantedTagA, grantedTagB);
                AssertRequirementContainsOnlyTagCodes(
                    definition.ApplicationRequiredTags,
                    new[] { requirementAllA, requirementAllB },
                    new[] { requirementAnyA, requirementAnyB },
                    new[] { requirementNoneA, requirementNoneB });
                AssertRequirementContainsOnlyTagCodes(
                    definition.OngoingRequiredTags,
                    new[] { requirementAllA, requirementAllB },
                    new[] { requirementAnyA, requirementAnyB },
                    new[] { requirementNoneA, requirementNoneB });
                AssertRequirementContainsOnlyTagCodes(
                    definition.RemoveGameplayEffectsWithTags,
                    new[] { requirementAllA, requirementAllB },
                    new[] { requirementAnyA, requirementAnyB },
                    new[] { requirementNoneA, requirementNoneB });
                AssertRequirementContainsOnlyTagCodes(
                    definition.ImmunityTags,
                    new[] { requirementAllA, requirementAllB },
                    new[] { requirementAnyA, requirementAnyB },
                    new[] { requirementNoneA, requirementNoneB });

                Assert.That(definition.HasPeriod, Is.True);
                Assert.That(definition.Period.Period, Is.EqualTo(11));
                Assert.That(definition.Period.ResetTimeCountWhenDeactivated, Is.True);
                AssertBlobIntArray(ref definition.PeriodEffectCodes, periodEffectA, periodEffectB);

                Assert.That(definition.HasStacking, Is.True);
                Assert.That(definition.Stacking.StackType, Is.EqualTo(EffectStackType.AggregateBySource));
                Assert.That(definition.Stacking.StackingCode, Is.EqualTo(9800900));
                Assert.That(definition.Stacking.LimitCount, Is.EqualTo(5));
                Assert.That(definition.Stacking.EffectDurationRefreshPolicy,
                    Is.EqualTo(EffectDurationRefreshPolicy.NeverRefresh));
                Assert.That(definition.Stacking.EffectPeriodResetPolicy,
                    Is.EqualTo(EffectPeriodResetPolicy.NeverRefresh));
                Assert.That(definition.Stacking.EffectExpirationPolicy,
                    Is.EqualTo(EffectExpirationPolicy.ClearEntireStack));
                Assert.That(definition.Stacking.DenyOverflowApplication, Is.True);
                Assert.That(definition.Stacking.ClearStackOnOverflow, Is.True);
                AssertBlobIntArray(ref definition.OverflowEffectCodes, overflowEffectA, overflowEffectB);

                Assert.That(definition.GrantedAbilities.Length, Is.EqualTo(2));
                AssertGrantedAbilityDefinition(
                    definition.GrantedAbilities[0],
                    grantedAbilityA,
                    1,
                    GrantedAbilityActivationPolicy.WhenAdded,
                    GrantedAbilityDeactivationPolicy.None,
                    GrantedAbilityRemovePolicy.WhenEnd);
                AssertGrantedAbilityDefinition(
                    definition.GrantedAbilities[1],
                    grantedAbilityB,
                    3,
                    GrantedAbilityActivationPolicy.SyncWithEffect,
                    GrantedAbilityDeactivationPolicy.SyncWithEffect,
                    GrantedAbilityRemovePolicy.WhenCancelOrEnd);
            }
            finally
            {
                if (blob.IsCreated)
                    blob.Dispose();
            }
        }

        [Test]
        public void ConfigContainingCueDoesNotEnterPrototypeCache()
        {
            const int effectCode = 98003;
            var configLookupCount = 0;
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
            {
                configLookupCount++;
                return id == effectCode ? CreateDurationModifierConfigWithCueBoundary() : null;
            });

            var source = CreateEntity();
            var targetA = CreateEntity();
            var targetB = CreateEntity();

            try
            {
                CreateRequest(effectCode, source, targetA, level: 1);
                RunCommandGroup();
                CreateRequest(effectCode, source, targetB, level: 1);
                RunCommandGroup();

                Assert.That(configLookupCount, Is.EqualTo(2));
                Assert.That(GameplayEffectConfigRegistry.CachedPrototypeCount, Is.EqualTo(0));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out _), Is.False);
                Assert.That(GameplayEffectConfigRegistry.CachedStaticDefinitionBlobCount, Is.EqualTo(1));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedStaticDefinitionBlob(effectCode, out var blob),
                    Is.True);
                Assert.That(blob.Value.HasDuration, Is.True);
                Assert.That(blob.Value.Duration.Duration, Is.EqualTo(30));
                Assert.That(blob.Value.Modifiers.Length, Is.EqualTo(1));

                using var effects = FindEffectsByCode(effectCode);
                Assert.That(effects.Length, Is.EqualTo(2));
                for (var i = 0; i < effects.Length; i++)
                {
                    Assert.That(_em.HasComponent<CGameplayEffectPrototype>(effects[i]), Is.False);
                    Assert.That(_em.HasComponent<CEffectContext>(effects[i]), Is.True);
                    Assert.That(_em.HasComponent<CEffectSpecData>(effects[i]), Is.True);
                }
            }
            finally
            {
                DestroyEffectsByCode(effectCode);
            }
        }

        [Test]
        public void TryGetOrCreateStaticDefinitionBlobFiltersCueConfigsAndDoesNotCreatePrototypeCache()
        {
            const int effectCode = 98018;
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode ? CreateDurationModifierConfigWithCueBoundary() : null);

            var prototypeCountBefore = CountEntitiesWith(ComponentType.ReadOnly<CGameplayEffectPrototype>());
            try
            {
                Assert.That(
                    GameplayEffectConfigRegistry.TryGetOrCreateStaticDefinitionBlob(_em, effectCode, out var blob),
                    Is.True);

                Assert.That(blob.IsCreated, Is.True);
                Assert.That(GameplayEffectConfigRegistry.CachedPrototypeCount, Is.EqualTo(0));
                Assert.That(GameplayEffectConfigRegistry.TryGetCachedPrototype(effectCode, out _), Is.False);
                Assert.That(GameplayEffectConfigRegistry.CachedStaticDefinitionBlobCount, Is.EqualTo(1));
                Assert.That(CountEntitiesWith(ComponentType.ReadOnly<CGameplayEffectPrototype>()),
                    Is.EqualTo(prototypeCountBefore));

                ref var definition = ref blob.Value;
                Assert.That(definition.GameplayEffectCode, Is.EqualTo(effectCode));
                Assert.That(definition.HasDuration, Is.True);
                Assert.That(definition.Duration.Duration, Is.EqualTo(30));
                Assert.That(definition.Modifiers.Length, Is.EqualTo(1));
            }
            finally
            {
                DestroyEffectsByCode(effectCode);
            }
        }

        private GameplayEffectConfig CreateDurationModifierConfig(int duration = 30)
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfDuration
                {
                    duration = duration,
                    timeUnit = TimeUnit.Frame,
                },
                new ConfModifierConfig
                {
                    ModifierSettings = new[]
                    {
                        new ModifierDefinitionSetting
                        {
                            AttrSetCode = 1001,
                            AttrCode = 2001,
                            Operation = EModifierOp.Add,
                            Magnitude = 5f,
                        },
                    },
                },
            });
        }

        private GameplayEffectConfig CreateDurationModifierConfigWithCueBoundary()
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new NoOpCueConfig(),
                new ConfDuration
                {
                    duration = 30,
                    timeUnit = TimeUnit.Frame,
                },
                new ConfModifierConfig
                {
                    ModifierSettings = new[]
                    {
                        new ModifierDefinitionSetting
                        {
                            AttrSetCode = 1001,
                            AttrCode = 2001,
                            Operation = EModifierOp.Add,
                            Magnitude = 5f,
                        },
                    },
                },
            });
        }

        private GameplayEffectConfig CreateApplicationAndImmunityRequirementConfig(
            int requiredTagCode,
            int immunityTagCode)
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfApplicationRequiredTags
                {
                    all = new[] { requiredTagCode },
                },
                new ConfEffectImmunityTags
                {
                    any = new[] { immunityTagCode },
                },
            });
        }

        private GameplayEffectConfig CreateRemoveGameplayEffectsWithTagsConfig(int removableTagCode)
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfRemoveEffectWithTags
                {
                    any = new[] { removableTagCode },
                },
            });
        }

        private GameplayEffectConfig CreateAssetTagsConfig(int assetTagCode)
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfAssetTags
                {
                    tags = new[] { assetTagCode },
                },
            });
        }

        private GameplayEffectConfig CreateDurationGrantedAbilityDefinitionConfig(int abilityCode)
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfDuration
                {
                    duration = 60,
                    timeUnit = TimeUnit.Frame,
                },
                new ConfGrantedAbilityConfig
                {
                    GrantedAbilities = new[]
                    {
                        new GrantedAbilityConfigSetting
                        {
                            AbilityCode = abilityCode,
                            Level = 5,
                            ActivationPolicy = GrantedAbilityActivationPolicy.None,
                            DeactivationPolicy = GrantedAbilityDeactivationPolicy.SyncWithEffect,
                            RemovePolicy = GrantedAbilityRemovePolicy.SyncWithEffect,
                        },
                    },
                },
            });
        }

        private GameplayEffectConfig CreateDefinitionContractConfig(
            int grantedAbilityCode,
            int attrSetCode,
            int attributeCode)
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfDuration
                {
                    duration = 30,
                    timeUnit = TimeUnit.Frame,
                    ResetStartTimeWhenActivated = true,
                    StopTickWhenDeactivated = true,
                },
                new ConfAssetTags
                {
                    tags = new[] { 9800411 },
                },
                new ConfEffectGrantedTags
                {
                    tags = new[] { 9800412 },
                },
                new ConfApplicationRequiredTags
                {
                    all = new[] { 9800413 },
                    any = new[] { 9800414 },
                    none = new[] { 9800415 },
                },
                new ConfOngoingRequiredTags
                {
                    all = new[] { 9800416 },
                    any = new[] { 9800417 },
                    none = new[] { 9800418 },
                },
                new ConfRemoveEffectWithTags
                {
                    all = new[] { 9800419 },
                    any = new[] { 9800420 },
                    none = new[] { 9800421 },
                },
                new ConfEffectImmunityTags
                {
                    all = new[] { 9800422 },
                    any = new[] { 9800423 },
                    none = new[] { 9800424 },
                },
                new ConfPeriod
                {
                    Period = 5,
                    ResetTimeCountWhenDeactivated = true,
                    GameplayEffectCodes = new[] { 9800425 },
                },
                new ConfStacking
                {
                    StackType = EffectStackType.AggregateByTarget,
                    StackingCode = 9800402,
                    LimitCount = 3,
                    EffectDurationRefreshPolicy = EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication,
                    EffectPeriodResetPolicy = EffectPeriodResetPolicy.ResetOnSuccessfulApplication,
                    EffectExpirationPolicy = EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration,
                    OverflowEffectCodes = new[] { 9800426 },
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
                            Magnitude = 1f,
                        },
                    },
                },
                new MagnitudeDefinitionConfig
                {
                    Definitions = new[]
                    {
                        new BMagnitudeDefinition
                        {
                            ModifierIndex = 0,
                            Source = EMagnitudeSource.SourceAttribute,
                            AttributeSetCode = attrSetCode,
                            AttributeCode = attributeCode,
                        },
                    },
                },
                new ConfGrantedAbilityConfig
                {
                    GrantedAbilities = new[]
                    {
                        new GrantedAbilityConfigSetting
                        {
                            AbilityCode = grantedAbilityCode,
                            Level = 2,
                            ActivationPolicy = GrantedAbilityActivationPolicy.None,
                            DeactivationPolicy = GrantedAbilityDeactivationPolicy.None,
                            RemovePolicy = GrantedAbilityRemovePolicy.SyncWithEffect,
                        },
                    },
                },
            });
        }

        private void AssertManagedGameplayEffectConfigDefinition(
            GameplayEffectConfig config,
            int attrSetCode,
            int attributeCode,
            int grantedAbilityCode,
            int assetTagCode,
            int grantedTagCode,
            int applicationAllTagCode,
            int applicationAnyTagCode,
            int applicationNoneTagCode,
            int ongoingAllTagCode,
            int ongoingAnyTagCode,
            int ongoingNoneTagCode,
            int removeAllTagCode,
            int removeAnyTagCode,
            int removeNoneTagCode,
            int immunityAllTagCode,
            int immunityAnyTagCode,
            int immunityNoneTagCode,
            int periodEffectCode,
            int overflowEffectCode,
            bool expectsMagnitudeDefinition = true,
            bool stopTickWhenDeactivated = true)
        {
            Assert.That(config, Is.Not.Null);
            Assert.That(config.ComponentConfigs, Is.Not.Null);
            Assert.That(config.ComponentConfigs.Length, Is.EqualTo(expectsMagnitudeDefinition ? 12 : 11));

            ConfDuration duration = null;
            ConfAssetTags assetTags = null;
            ConfEffectGrantedTags grantedTags = null;
            ConfApplicationRequiredTags applicationRequiredTags = null;
            ConfOngoingRequiredTags ongoingRequiredTags = null;
            ConfRemoveEffectWithTags removeEffectWithTags = null;
            ConfEffectImmunityTags immunityTags = null;
            ConfPeriod period = null;
            ConfStacking stacking = null;
            ConfModifierConfig modifiers = null;
            MagnitudeDefinitionConfig magnitudeDefinitions = null;
            ConfGrantedAbilityConfig grantedAbilities = null;

            for (var i = 0; i < config.ComponentConfigs.Length; i++)
            {
                switch (config.ComponentConfigs[i])
                {
                    case ConfDuration value:
                        duration = value;
                        break;
                    case ConfAssetTags value:
                        assetTags = value;
                        break;
                    case ConfEffectGrantedTags value:
                        grantedTags = value;
                        break;
                    case ConfApplicationRequiredTags value:
                        applicationRequiredTags = value;
                        break;
                    case ConfOngoingRequiredTags value:
                        ongoingRequiredTags = value;
                        break;
                    case ConfRemoveEffectWithTags value:
                        removeEffectWithTags = value;
                        break;
                    case ConfEffectImmunityTags value:
                        immunityTags = value;
                        break;
                    case ConfPeriod value:
                        period = value;
                        break;
                    case ConfStacking value:
                        stacking = value;
                        break;
                    case ConfModifierConfig value:
                        modifiers = value;
                        break;
                    case MagnitudeDefinitionConfig value:
                        magnitudeDefinitions = value;
                        break;
                    case ConfGrantedAbilityConfig value:
                        grantedAbilities = value;
                        break;
                }
            }

            Assert.That(duration, Is.Not.Null);
            Assert.That(duration.duration, Is.EqualTo(30));
            Assert.That(duration.timeUnit, Is.EqualTo(TimeUnit.Frame));
            Assert.That(duration.ResetStartTimeWhenActivated, Is.True);
            Assert.That(duration.StopTickWhenDeactivated, Is.EqualTo(stopTickWhenDeactivated));

            Assert.That(assetTags, Is.Not.Null);
            AssertIntArray(assetTags.tags, assetTagCode);
            Assert.That(grantedTags, Is.Not.Null);
            AssertIntArray(grantedTags.tags, grantedTagCode);

            Assert.That(applicationRequiredTags, Is.Not.Null);
            AssertIntArray(applicationRequiredTags.all, applicationAllTagCode);
            AssertIntArray(applicationRequiredTags.any, applicationAnyTagCode);
            AssertIntArray(applicationRequiredTags.none, applicationNoneTagCode);

            Assert.That(ongoingRequiredTags, Is.Not.Null);
            AssertIntArray(ongoingRequiredTags.all, ongoingAllTagCode);
            AssertIntArray(ongoingRequiredTags.any, ongoingAnyTagCode);
            AssertIntArray(ongoingRequiredTags.none, ongoingNoneTagCode);

            Assert.That(removeEffectWithTags, Is.Not.Null);
            AssertIntArray(removeEffectWithTags.all, removeAllTagCode);
            AssertIntArray(removeEffectWithTags.any, removeAnyTagCode);
            AssertIntArray(removeEffectWithTags.none, removeNoneTagCode);

            Assert.That(immunityTags, Is.Not.Null);
            AssertIntArray(immunityTags.all, immunityAllTagCode);
            AssertIntArray(immunityTags.any, immunityAnyTagCode);
            AssertIntArray(immunityTags.none, immunityNoneTagCode);

            Assert.That(period, Is.Not.Null);
            Assert.That(period.Period, Is.EqualTo(5));
            Assert.That(period.ResetTimeCountWhenDeactivated, Is.True);
            AssertIntArray(period.GameplayEffectCodes, periodEffectCode);

            Assert.That(stacking, Is.Not.Null);
            Assert.That(stacking.StackType, Is.EqualTo(EffectStackType.AggregateByTarget));
            Assert.That(stacking.StackingCode, Is.EqualTo(9800402));
            Assert.That(stacking.LimitCount, Is.EqualTo(3));
            Assert.That(stacking.EffectDurationRefreshPolicy,
                Is.EqualTo(EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication));
            Assert.That(stacking.EffectPeriodResetPolicy,
                Is.EqualTo(EffectPeriodResetPolicy.ResetOnSuccessfulApplication));
            Assert.That(stacking.EffectExpirationPolicy,
                Is.EqualTo(EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration));
            Assert.That(stacking.denyOverflowApplication, Is.False);
            Assert.That(stacking.clearStackOnOverflow, Is.False);
            AssertIntArray(stacking.OverflowEffectCodes, overflowEffectCode);

            Assert.That(modifiers, Is.Not.Null);
            Assert.That(modifiers.ModifierSettings, Is.Not.Null);
            Assert.That(modifiers.ModifierSettings.Length, Is.EqualTo(1));
            Assert.That(modifiers.ModifierSettings[0].AttrSetCode, Is.EqualTo(attrSetCode));
            Assert.That(modifiers.ModifierSettings[0].AttrCode, Is.EqualTo(attributeCode));
            Assert.That(modifiers.ModifierSettings[0].Operation, Is.EqualTo(EModifierOp.Add));
            Assert.That(modifiers.ModifierSettings[0].Magnitude, Is.EqualTo(1f));

            if (expectsMagnitudeDefinition)
            {
                Assert.That(magnitudeDefinitions, Is.Not.Null);
                Assert.That(magnitudeDefinitions.Definitions, Is.Not.Null);
                Assert.That(magnitudeDefinitions.Definitions.Length, Is.EqualTo(1));
                Assert.That(magnitudeDefinitions.Definitions[0].ModifierIndex, Is.EqualTo(0));
                Assert.That(magnitudeDefinitions.Definitions[0].Source, Is.EqualTo(EMagnitudeSource.SourceAttribute));
                Assert.That(magnitudeDefinitions.Definitions[0].AttributeSetCode, Is.EqualTo(attrSetCode));
                Assert.That(magnitudeDefinitions.Definitions[0].AttributeCode, Is.EqualTo(attributeCode));
            }
            else
            {
                Assert.That(magnitudeDefinitions, Is.Null);
            }

            Assert.That(grantedAbilities, Is.Not.Null);
            Assert.That(grantedAbilities.GrantedAbilities, Is.Not.Null);
            Assert.That(grantedAbilities.GrantedAbilities.Length, Is.EqualTo(1));
            Assert.That(grantedAbilities.GrantedAbilities[0].AbilityCode, Is.EqualTo(grantedAbilityCode));
            Assert.That(grantedAbilities.GrantedAbilities[0].Level, Is.EqualTo(2));
            Assert.That(grantedAbilities.GrantedAbilities[0].ActivationPolicy,
                Is.EqualTo(GrantedAbilityActivationPolicy.None));
            Assert.That(grantedAbilities.GrantedAbilities[0].DeactivationPolicy,
                Is.EqualTo(GrantedAbilityDeactivationPolicy.None));
            Assert.That(grantedAbilities.GrantedAbilities[0].RemovePolicy,
                Is.EqualTo(GrantedAbilityRemovePolicy.SyncWithEffect));
        }

        private void InitTagMap(params int[] tagCodes)
        {
            var tagMap = new Dictionary<int, GameplayTag>();
            var tagNames = new Dictionary<int, string>();
            for (var i = 0; i < tagCodes.Length; i++)
            {
                tagMap[tagCodes[i]] = new GameplayTag(tagCodes[i], System.Array.Empty<int>(), System.Array.Empty<int>());
                tagNames[tagCodes[i]] = $"Test.Tag.{tagCodes[i]}";
            }

            TagHelper.InitTagMap(tagMap, tagNames);
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

        private static GameplayEffectConfig CreateConfigFromGeneratedRow(GeneratedGameplayEffectRow data)
        {
            var configs = new List<GameplayEffectComponentConfig>();

            if (data.AssetTags is { Count: > 0 })
                configs.Add(new ConfAssetTags { tags = data.AssetTags.ToArray() });
            if (data.GrantedTags is { Count: > 0 })
                configs.Add(new ConfEffectGrantedTags { tags = data.GrantedTags.ToArray() });
            if (TryParseGeneratedTagRequirement(
                    data.ApplicationRequiredTags,
                    out var applicationAll,
                    out var applicationAny,
                    out var applicationNone))
                configs.Add(new ConfApplicationRequiredTags
                {
                    all = applicationAll,
                    any = applicationAny,
                    none = applicationNone,
                });
            if (TryParseGeneratedTagRequirement(
                    data.OngoingRequiredTags,
                    out var ongoingAll,
                    out var ongoingAny,
                    out var ongoingNone))
                configs.Add(new ConfOngoingRequiredTags
                {
                    all = ongoingAll,
                    any = ongoingAny,
                    none = ongoingNone,
                });
            if (TryParseGeneratedTagRequirement(
                    data.RemoveGameplayEffectsWithTags,
                    out var removeAll,
                    out var removeAny,
                    out var removeNone))
                configs.Add(new ConfRemoveEffectWithTags
                {
                    all = removeAll,
                    any = removeAny,
                    none = removeNone,
                });
            if (TryParseGeneratedTagRequirement(
                    data.ImmunityTags,
                    out var immunityAll,
                    out var immunityAny,
                    out var immunityNone))
                configs.Add(new ConfEffectImmunityTags
                {
                    all = immunityAll,
                    any = immunityAny,
                    none = immunityNone,
                });

            if (data.Duration != null && data.Duration.Time != 0)
                configs.Add(new ConfDuration
                {
                    duration = data.Duration.Time,
                    timeUnit = (TimeUnit)data.Duration.TimeUnit,
                    ResetStartTimeWhenActivated = data.Duration.ResetStartTimeWhenActivated,
                });
            if (data.Period is { Time: > 0 })
                configs.Add(new ConfPeriod
                {
                    Period = data.Period.Time,
                    ResetTimeCountWhenDeactivated = data.Period.FirstTrigger,
                    GameplayEffectCodes = data.Period.Effects,
                });
            if (data.Modifiers is { Count: > 0 })
            {
                var modifierSettings = new ModifierDefinitionSetting[data.Modifiers.Count];
                for (var i = 0; i < data.Modifiers.Count; i++)
                {
                    var info = data.Modifiers[i];
                    modifierSettings[i] = new ModifierDefinitionSetting
                    {
                        AttrSetCode = info.AttrSet,
                        AttrCode = info.Attribute,
                        Magnitude = info.Magnitude,
                        Operation = (EModifierOp)info.Operation,
                    };
                }

                configs.Add(new ConfModifierConfig { ModifierSettings = modifierSettings });
            }
            if (data.GrantedAbility is { Count: > 0 })
            {
                var grantedAbilities = new GrantedAbilityConfigSetting[data.GrantedAbility.Count];
                for (var i = 0; i < data.GrantedAbility.Count; i++)
                {
                    var info = data.GrantedAbility[i];
                    grantedAbilities[i] = new GrantedAbilityConfigSetting
                    {
                        AbilityCode = info.ID,
                        ActivationPolicy = (GrantedAbilityActivationPolicy)info.ActivationPolicy,
                        DeactivationPolicy = (GrantedAbilityDeactivationPolicy)info.DeactivationPolicy,
                        Level = info.Level,
                        RemovePolicy = (GrantedAbilityRemovePolicy)info.RemovePolicy,
                    };
                }

                configs.Add(new ConfGrantedAbilityConfig { GrantedAbilities = grantedAbilities });
            }
            if (data.Stacking != null && data.Stacking.StackCode != 0)
                configs.Add(new ConfStacking
                {
                    StackingCode = data.Stacking.StackCode,
                    StackType = (EffectStackType)data.Stacking.StackingType,
                    LimitCount = data.Stacking.LimitCount,
                    EffectDurationRefreshPolicy =
                        (EffectDurationRefreshPolicy)data.Stacking.DurationRefreshPolicy,
                    EffectPeriodResetPolicy = (EffectPeriodResetPolicy)data.Stacking.PeriodResetPolicy,
                    EffectExpirationPolicy = (EffectExpirationPolicy)data.Stacking.ExpirationPolicy,
                    denyOverflowApplication = data.Stacking.DenyOverflowApplication,
                    clearStackOnOverflow = data.Stacking.ClearStackOnOverflow,
                    OverflowEffectCodes = data.Stacking.OverflowEffects,
                });

            return new GameplayEffectConfig(configs.ToArray());
        }

        private static bool TryParseGeneratedTagRequirement(
            GeneratedTagRequirementSpec requirement,
            out int[] all,
            out int[] any,
            out int[] none)
        {
            all = FilterPositiveCodes(requirement?.All);
            any = FilterPositiveCodes(requirement?.Any);
            none = FilterPositiveCodes(requirement?.None);
            return all != null || any != null || none != null;
        }

        private static int[] FilterPositiveCodes(List<int> codes)
        {
            if (codes == null || codes.Count == 0)
                return null;

            var filtered = new List<int>();
            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i] > 0)
                    filtered.Add(codes[i]);
            }

            return filtered.Count > 0 ? filtered.ToArray() : null;
        }

        private void AddTagsToAsc(Entity asc, params int[] tagCodes)
        {
            var tags = _em.GetComponentData<CTagMask>(asc);
            for (var i = 0; i < tagCodes.Length; i++)
            {
                if (TagHelper.TryGetDenseIndex(tagCodes[i], out var tagIndex))
                    tags.AddTag(tagIndex);
            }

            _em.SetComponentData(asc, tags);
        }

        private AbilityConfig CreateGrantedAbilityConfig(int abilityCode)
        {
            return new AbilityConfig(new AbilityComponentConfig[]
            {
                new ConfAbilityBaseInfo
                {
                    Code = abilityCode,
                    Level = 1,
                },
            });
        }

        private Entity CreateRequest(int effectCode, Entity source, Entity target, int level)
        {
            var request = GameplayEffectRequestWriter.Create(
                _em,
                new CApplyGameplayEffectRequest
                {
                    SourceAsc = source,
                    Instigator = source,
                    Causer = source,
                    GameplayEffectCode = effectCode,
                    Level = level,
                },
                new CTargetDataHeader
                {
                    SourceAsc = source,
                    Kind = ETargetDataKind.Entity,
                },
                "PrototypeCacheRequest");

            GameplayEffectRequestWriter.AddTarget(_em, request, target);
            return request;
        }

        private Entity CreateAscWithAttribute(int attrSetCode, int attributeCode, float currentValue)
        {
            var asc = CreateEntity();
            _em.AddComponentData(asc, new CTagMask());
            _em.AddBuffer<BAttribute>(asc).Add(new BAttribute
            {
                AttrSetCode = attrSetCode,
                Code = attributeCode,
                BaseValue = currentValue,
                CurrentValue = currentValue,
            });
            return asc;
        }

        private Entity CreateTargetAsc()
        {
            var asc = CreateEntity();
            _em.AddComponentData(asc, new CTagMask());
            _em.AddBuffer<BAttribute>(asc);
            _em.AddBuffer<BActiveModifier>(asc);
            _em.AddBuffer<BGameplayEffect>(asc);
            _em.AddBuffer<BGrantedAbility>(asc);
            return asc;
        }

        private void AddTargetPoint(Entity request, float3 point)
        {
            _em.AddBuffer<BTargetPoint>(request).Add(new BTargetPoint
            {
                Position = point,
            });
        }

        private void AssertRuntimeInstance(
            Entity effect,
            Entity target,
            int level,
            int setByCallerKey,
            float setByCallerValue,
            float3 point)
        {
            Assert.That(_em.HasComponent<CGameplayEffectPrototype>(effect), Is.False);

            var context = _em.GetComponentData<CEffectContext>(effect);
            Assert.That(context.TargetAsc, Is.EqualTo(target));
            Assert.That(context.TargetDataKind, Is.EqualTo(ETargetDataKind.Entity));

            var spec = _em.GetComponentData<CEffectSpecData>(effect);
            Assert.That(spec.Level, Is.EqualTo(level));

            var setByCallers = _em.GetBuffer<BSetByCallerValue>(effect);
            Assert.That(setByCallers.Length, Is.EqualTo(1));
            Assert.That(setByCallers[0].Key, Is.EqualTo(setByCallerKey));
            Assert.That(setByCallers[0].Value, Is.EqualTo(setByCallerValue));

            var points = _em.GetBuffer<BEffectTargetPoint>(effect);
            Assert.That(points.Length, Is.EqualTo(1));
            Assert.That(points[0].Position, Is.EqualTo(point));
        }

        private void AssertPrototypeHasOnlyDefinitionState(
            Entity prototype,
            int effectCode,
            int attrSetCode,
            int attributeCode,
            int grantedAbilityCode,
            int assetTagCode,
            int grantedTagCode,
            int applicationAllTagCode,
            int applicationAnyTagCode,
            int applicationNoneTagCode,
            int ongoingAllTagCode,
            int ongoingAnyTagCode,
            int ongoingNoneTagCode,
            int removeAllTagCode,
            int removeAnyTagCode,
            int removeNoneTagCode,
            int immunityAllTagCode,
            int immunityAnyTagCode,
            int immunityNoneTagCode,
            int periodEffectCode,
            int overflowEffectCode,
            bool expectsMagnitudeDefinition = true,
            bool stopTickWhenDeactivated = true)
        {
            Assert.That(_em.HasComponent<CGameplayEffectPrototype>(prototype), Is.True);
            Assert.That(_em.GetComponentData<CGameplayEffectPrototype>(prototype).GameplayEffectCode,
                Is.EqualTo(effectCode));
            Assert.That(_em.HasComponent<CEffectContext>(prototype), Is.False);
            Assert.That(_em.HasComponent<CEffectSpecData>(prototype), Is.False);
            Assert.That(_em.HasComponent<CEffectLifecycle>(prototype), Is.False);
            Assert.That(_em.HasBuffer<BSetByCallerValue>(prototype), Is.False);
            Assert.That(_em.HasBuffer<BEffectTargetPoint>(prototype), Is.False);
            Assert.That(_em.HasBuffer<BEffectTargetDirection>(prototype), Is.False);
            Assert.That(_em.HasBuffer<BEffectTargetHit>(prototype), Is.False);
            Assert.That(_em.HasBuffer<BResolvedModifier>(prototype), Is.False);
            Assert.That(_em.HasBuffer<BAttributeCaptureValue>(prototype), Is.False);
            Assert.That(_em.HasBuffer<BGrantedAbilityRuntime>(prototype), Is.False);

            var durationDefinition = _em.GetComponentData<CDurationDefinition>(prototype);
            Assert.That(durationDefinition.Duration, Is.EqualTo(30));
            Assert.That(durationDefinition.TimeUnit, Is.EqualTo(TimeUnit.Frame));
            Assert.That(durationDefinition.ResetStartTimeWhenActivated, Is.True);
            Assert.That(durationDefinition.StopTickWhenDeactivated, Is.EqualTo(stopTickWhenDeactivated));
            Assert.That(_em.HasComponent<CDurationRuntime>(prototype), Is.False);

            AssertMaskContainsOnlyTagCode(_em.GetComponentData<CEffectAssetTags>(prototype).Tags, assetTagCode);
            AssertMaskContainsOnlyTagCode(_em.GetComponentData<CEffectGrantedTags>(prototype).Tags, grantedTagCode);
            AssertRequirementContainsOnlyTagCodes(
                _em.GetComponentData<CApplicationRequiredTags>(prototype).requirement,
                applicationAllTagCode,
                applicationAnyTagCode,
                applicationNoneTagCode);
            AssertRequirementContainsOnlyTagCodes(
                _em.GetComponentData<COngoingRequiredTags>(prototype).requirement,
                ongoingAllTagCode,
                ongoingAnyTagCode,
                ongoingNoneTagCode);
            AssertRequirementContainsOnlyTagCodes(
                _em.GetComponentData<CRemoveEffectWithTags>(prototype).requirement,
                removeAllTagCode,
                removeAnyTagCode,
                removeNoneTagCode);
            AssertRequirementContainsOnlyTagCodes(
                _em.GetComponentData<CEffectImmunityTags>(prototype).requirement,
                immunityAllTagCode,
                immunityAnyTagCode,
                immunityNoneTagCode);

            var periodDefinition = _em.GetComponentData<CPeriodDefinition>(prototype);
            Assert.That(periodDefinition.Period, Is.EqualTo(5));
            Assert.That(periodDefinition.ResetTimeCountWhenDeactivated, Is.True);
            Assert.That(_em.HasComponent<CPeriodRuntime>(prototype), Is.False);

            var periodEffects = _em.GetBuffer<BPeriodGEConfig>(prototype);
            Assert.That(periodEffects.Length, Is.EqualTo(1));
            Assert.That(periodEffects[0].GameplayEffectCode, Is.EqualTo(periodEffectCode));

            var stacking = _em.GetComponentData<CStackingDefinition>(prototype);
            Assert.That(stacking.StackType, Is.EqualTo(EffectStackType.AggregateByTarget));
            Assert.That(stacking.StackingCode, Is.EqualTo(9800402));
            Assert.That(stacking.LimitCount, Is.EqualTo(3));
            Assert.That(stacking.EffectDurationRefreshPolicy,
                Is.EqualTo(EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication));
            Assert.That(stacking.EffectPeriodResetPolicy,
                Is.EqualTo(EffectPeriodResetPolicy.ResetOnSuccessfulApplication));
            Assert.That(stacking.EffectExpirationPolicy,
                Is.EqualTo(EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration));
            Assert.That(_em.HasComponent<CStackingRuntime>(prototype), Is.False);
            var overflowEffects = _em.GetBuffer<BOverflowGEConfig>(prototype);
            Assert.That(overflowEffects.Length, Is.EqualTo(1));
            Assert.That(overflowEffects[0].GameplayEffectCode, Is.EqualTo(overflowEffectCode));

            var modifiers = _em.GetBuffer<BModifierConfig>(prototype);
            Assert.That(modifiers.Length, Is.EqualTo(1));
            Assert.That(modifiers[0].AttrSetCode, Is.EqualTo(attrSetCode));
            Assert.That(modifiers[0].AttributeCode, Is.EqualTo(attributeCode));
            Assert.That(modifiers[0].Op, Is.EqualTo(EModifierOp.Add));
            Assert.That(modifiers[0].Magnitude, Is.EqualTo(1f));

            if (expectsMagnitudeDefinition)
            {
                var magnitudeDefinitions = _em.GetBuffer<BMagnitudeDefinition>(prototype);
                Assert.That(magnitudeDefinitions.Length, Is.EqualTo(1));
                Assert.That(magnitudeDefinitions[0].ModifierIndex, Is.EqualTo(0));
                Assert.That(magnitudeDefinitions[0].Source, Is.EqualTo(EMagnitudeSource.SourceAttribute));
                Assert.That(magnitudeDefinitions[0].AttributeSetCode, Is.EqualTo(attrSetCode));
                Assert.That(magnitudeDefinitions[0].AttributeCode, Is.EqualTo(attributeCode));
            }
            else
            {
                Assert.That(_em.HasBuffer<BMagnitudeDefinition>(prototype), Is.False);
            }

            var grantedTags = _em.GetBuffer<BGrantedTagConfig>(prototype);
            Assert.That(grantedTags.Length, Is.EqualTo(1));
            Assert.That(TagHelper.TryGetDenseIndex(grantedTagCode, out var grantedTagIndex), Is.True);
            Assert.That(grantedTags[0].TagIndex, Is.EqualTo(grantedTagIndex));

            var grantedAbilities = _em.GetBuffer<BGrantedAbilityConfig>(prototype);
            Assert.That(grantedAbilities.Length, Is.EqualTo(1));
            Assert.That(grantedAbilities[0].AbilityCode, Is.EqualTo(grantedAbilityCode));
            Assert.That(grantedAbilities[0].Level, Is.EqualTo(2));
            Assert.That(grantedAbilities[0].ActivationPolicy, Is.EqualTo(GrantedAbilityActivationPolicy.None));
            Assert.That(grantedAbilities[0].DeactivationPolicy, Is.EqualTo(GrantedAbilityDeactivationPolicy.None));
            Assert.That(grantedAbilities[0].RemovePolicy, Is.EqualTo(GrantedAbilityRemovePolicy.SyncWithEffect));
        }

        private static void AssertStaticDefinitionBlob(
            ref GEStaticDefinitionBlob definition,
            int effectCode,
            int attrSetCode,
            int attributeCode,
            int grantedAbilityCode,
            int assetTagCode,
            int grantedTagCode,
            int applicationAllTagCode,
            int applicationAnyTagCode,
            int applicationNoneTagCode,
            int ongoingAllTagCode,
            int ongoingAnyTagCode,
            int ongoingNoneTagCode,
            int removeAllTagCode,
            int removeAnyTagCode,
            int removeNoneTagCode,
            int immunityAllTagCode,
            int immunityAnyTagCode,
            int immunityNoneTagCode,
            int periodEffectCode,
            int overflowEffectCode,
            bool expectsSourceAttributeMagnitude = true,
            bool stopTickWhenDeactivated = true)
        {
            Assert.That(definition.GameplayEffectCode, Is.EqualTo(effectCode));
            AssertBlobDurationDefinition(ref definition, stopTickWhenDeactivated);
            AssertBlobPeriodDefinition(ref definition, periodEffectCode);
            AssertBlobStackingDefinition(ref definition, overflowEffectCode);
            AssertBlobTagDefinitions(
                ref definition,
                assetTagCode,
                grantedTagCode,
                applicationAllTagCode,
                applicationAnyTagCode,
                applicationNoneTagCode,
                ongoingAllTagCode,
                ongoingAnyTagCode,
                ongoingNoneTagCode,
                removeAllTagCode,
                removeAnyTagCode,
                removeNoneTagCode,
                immunityAllTagCode,
                immunityAnyTagCode,
                immunityNoneTagCode);
            if (expectsSourceAttributeMagnitude)
                AssertBlobModifierDefinitions(ref definition.Modifiers, attrSetCode, attributeCode);
            else
                AssertBlobHasConstantModifierDefinition(ref definition.Modifiers, attrSetCode, attributeCode, 1f);
            AssertBlobGrantedAbilityDefinitions(ref definition.GrantedAbilities, grantedAbilityCode);
        }

        private static void AssertBlobDurationDefinition(
            ref GEStaticDefinitionBlob definition,
            bool stopTickWhenDeactivated = true)
        {
            Assert.That(definition.HasDuration, Is.True);
            Assert.That(definition.Duration.Duration, Is.EqualTo(30));
            Assert.That(definition.Duration.TimeUnit, Is.EqualTo(TimeUnit.Frame));
            Assert.That(definition.Duration.ResetStartTimeWhenActivated, Is.True);
            Assert.That(definition.Duration.StopTickWhenDeactivated, Is.EqualTo(stopTickWhenDeactivated));
        }

        private static void AssertBlobPeriodDefinition(
            ref GEStaticDefinitionBlob definition,
            int periodEffectCode)
        {
            Assert.That(definition.HasPeriod, Is.True);
            Assert.That(definition.Period.Period, Is.EqualTo(5));
            Assert.That(definition.Period.ResetTimeCountWhenDeactivated, Is.True);
            Assert.That(definition.PeriodEffectCodes.Length, Is.EqualTo(1));
            Assert.That(definition.PeriodEffectCodes[0], Is.EqualTo(periodEffectCode));
        }

        private static void AssertBlobStackingDefinition(
            ref GEStaticDefinitionBlob definition,
            int overflowEffectCode)
        {
            Assert.That(definition.HasStacking, Is.True);
            Assert.That(definition.Stacking.StackType, Is.EqualTo(EffectStackType.AggregateByTarget));
            Assert.That(definition.Stacking.StackingCode, Is.EqualTo(9800402));
            Assert.That(definition.Stacking.LimitCount, Is.EqualTo(3));
            Assert.That(definition.Stacking.EffectDurationRefreshPolicy,
                Is.EqualTo(EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication));
            Assert.That(definition.Stacking.EffectPeriodResetPolicy,
                Is.EqualTo(EffectPeriodResetPolicy.ResetOnSuccessfulApplication));
            Assert.That(definition.Stacking.EffectExpirationPolicy,
                Is.EqualTo(EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration));
            Assert.That(definition.Stacking.DenyOverflowApplication, Is.False);
            Assert.That(definition.Stacking.ClearStackOnOverflow, Is.False);
            Assert.That(definition.OverflowEffectCodes.Length, Is.EqualTo(1));
            Assert.That(definition.OverflowEffectCodes[0], Is.EqualTo(overflowEffectCode));
        }

        private static void AssertBlobTagDefinitions(
            ref GEStaticDefinitionBlob definition,
            int assetTagCode,
            int grantedTagCode,
            int applicationAllTagCode,
            int applicationAnyTagCode,
            int applicationNoneTagCode,
            int ongoingAllTagCode,
            int ongoingAnyTagCode,
            int ongoingNoneTagCode,
            int removeAllTagCode,
            int removeAnyTagCode,
            int removeNoneTagCode,
            int immunityAllTagCode,
            int immunityAnyTagCode,
            int immunityNoneTagCode)
        {
            AssertMaskContainsOnlyTagCode(definition.AssetTags, assetTagCode);
            AssertMaskContainsOnlyTagCode(definition.GrantedTags, grantedTagCode);
            Assert.That(definition.HasApplicationRequiredTags, Is.True);
            AssertRequirementContainsOnlyTagCodes(
                definition.ApplicationRequiredTags,
                applicationAllTagCode,
                applicationAnyTagCode,
                applicationNoneTagCode);
            Assert.That(definition.HasOngoingRequiredTags, Is.True);
            AssertRequirementContainsOnlyTagCodes(
                definition.OngoingRequiredTags,
                ongoingAllTagCode,
                ongoingAnyTagCode,
                ongoingNoneTagCode);
            Assert.That(definition.HasRemoveGameplayEffectsWithTags, Is.True);
            AssertRequirementContainsOnlyTagCodes(
                definition.RemoveGameplayEffectsWithTags,
                removeAllTagCode,
                removeAnyTagCode,
                removeNoneTagCode);
            Assert.That(definition.HasImmunityTags, Is.True);
            AssertRequirementContainsOnlyTagCodes(
                definition.ImmunityTags,
                immunityAllTagCode,
                immunityAnyTagCode,
                immunityNoneTagCode);
        }

        private static void AssertBlobModifierDefinitions(
            ref BlobArray<GEModifierDefinition> modifiers,
            int attrSetCode,
            int attributeCode)
        {
            Assert.That(modifiers.Length, Is.EqualTo(1));
            Assert.That(modifiers[0].ModifierIndex, Is.EqualTo(0));
            Assert.That(modifiers[0].AttrSetCode, Is.EqualTo(attrSetCode));
            Assert.That(modifiers[0].AttributeCode, Is.EqualTo(attributeCode));
            Assert.That(modifiers[0].Operation, Is.EqualTo(EModifierOp.Add));
            Assert.That(modifiers[0].Magnitude, Is.EqualTo(1f));
            Assert.That(modifiers[0].MagnitudeSource, Is.EqualTo(EMagnitudeSource.SourceAttribute));
            Assert.That(modifiers[0].MagnitudeAttributeSetCode, Is.EqualTo(attrSetCode));
            Assert.That(modifiers[0].MagnitudeAttributeCode, Is.EqualTo(attributeCode));
            Assert.That(modifiers[0].MagnitudeKey, Is.EqualTo(0));
            Assert.That(modifiers[0].MagnitudeCaptureTiming, Is.EqualTo(EAttributeCaptureTiming.Snapshot));
            Assert.That(modifiers[0].FallbackMagnitude, Is.EqualTo(0f));
            Assert.That(modifiers[0].Coefficient, Is.EqualTo(1f));
            Assert.That(modifiers[0].PreAdd, Is.EqualTo(0f));
            Assert.That(modifiers[0].PostAdd, Is.EqualTo(0f));
        }

        private static void AssertBlobGrantedAbilityDefinitions(
            ref BlobArray<GEGrantedAbilityDefinition> grantedAbilities,
            int grantedAbilityCode)
        {
            Assert.That(grantedAbilities.Length, Is.EqualTo(1));
            Assert.That(grantedAbilities[0].AbilityCode, Is.EqualTo(grantedAbilityCode));
            Assert.That(grantedAbilities[0].Level, Is.EqualTo(2));
            Assert.That(grantedAbilities[0].ActivationPolicy, Is.EqualTo(GrantedAbilityActivationPolicy.None));
            Assert.That(grantedAbilities[0].DeactivationPolicy, Is.EqualTo(GrantedAbilityDeactivationPolicy.None));
            Assert.That(grantedAbilities[0].RemovePolicy, Is.EqualTo(GrantedAbilityRemovePolicy.SyncWithEffect));
        }

        private static void AssertGrantedAbilityDefinition(
            GEGrantedAbilityDefinition grantedAbility,
            int abilityCode,
            int level,
            GrantedAbilityActivationPolicy activationPolicy,
            GrantedAbilityDeactivationPolicy deactivationPolicy,
            GrantedAbilityRemovePolicy removePolicy)
        {
            Assert.That(grantedAbility.AbilityCode, Is.EqualTo(abilityCode));
            Assert.That(grantedAbility.Level, Is.EqualTo(level));
            Assert.That(grantedAbility.ActivationPolicy, Is.EqualTo(activationPolicy));
            Assert.That(grantedAbility.DeactivationPolicy, Is.EqualTo(deactivationPolicy));
            Assert.That(grantedAbility.RemovePolicy, Is.EqualTo(removePolicy));
        }

        private static void AssertBlobHasNoOptionalDefinitions(ref GEStaticDefinitionBlob definition)
        {
            Assert.That(definition.HasDuration, Is.False);
            Assert.That(definition.HasPeriod, Is.False);
            Assert.That(definition.PeriodEffectCodes.Length, Is.EqualTo(0));
            Assert.That(definition.HasStacking, Is.False);
            Assert.That(definition.OverflowEffectCodes.Length, Is.EqualTo(0));
            AssertMaskIsEmpty(definition.AssetTags);
            AssertMaskIsEmpty(definition.GrantedTags);
            Assert.That(definition.HasApplicationRequiredTags, Is.False);
            AssertRequirementIsEmpty(definition.ApplicationRequiredTags);
            Assert.That(definition.HasOngoingRequiredTags, Is.False);
            AssertRequirementIsEmpty(definition.OngoingRequiredTags);
            Assert.That(definition.HasRemoveGameplayEffectsWithTags, Is.False);
            AssertRequirementIsEmpty(definition.RemoveGameplayEffectsWithTags);
            Assert.That(definition.HasImmunityTags, Is.False);
            AssertRequirementIsEmpty(definition.ImmunityTags);
            Assert.That(definition.Modifiers.Length, Is.EqualTo(0));
            Assert.That(definition.GrantedAbilities.Length, Is.EqualTo(0));
        }

        private static void AssertBlobHasConstantModifierDefinition(
            ref BlobArray<GEModifierDefinition> modifiers,
            int attrSetCode,
            int attributeCode,
            float expectedMagnitude = 7f)
        {
            Assert.That(modifiers.Length, Is.EqualTo(1));
            Assert.That(modifiers[0].ModifierIndex, Is.EqualTo(0));
            Assert.That(modifiers[0].AttrSetCode, Is.EqualTo(attrSetCode));
            Assert.That(modifiers[0].AttributeCode, Is.EqualTo(attributeCode));
            Assert.That(modifiers[0].Operation, Is.EqualTo(EModifierOp.Add));
            Assert.That(modifiers[0].Magnitude, Is.EqualTo(expectedMagnitude));
            Assert.That(modifiers[0].MagnitudeSource, Is.EqualTo(EMagnitudeSource.Constant));
            Assert.That(modifiers[0].MagnitudeAttributeSetCode, Is.EqualTo(0));
            Assert.That(modifiers[0].MagnitudeAttributeCode, Is.EqualTo(0));
            Assert.That(modifiers[0].MagnitudeKey, Is.EqualTo(0));
            Assert.That(modifiers[0].FallbackMagnitude, Is.EqualTo(expectedMagnitude));
            Assert.That(modifiers[0].Coefficient, Is.EqualTo(1f));
            Assert.That(modifiers[0].PreAdd, Is.EqualTo(0f));
            Assert.That(modifiers[0].PostAdd, Is.EqualTo(0f));
        }

        private static void AssertRequirementIsEmpty(TagRequirementMask requirement)
        {
            AssertMaskIsEmpty(requirement.All);
            AssertMaskIsEmpty(requirement.Any);
            AssertMaskIsEmpty(requirement.None);
        }

        private static void AssertMaskIsEmpty(CTagMask mask)
        {
            Assert.That(CountTags(mask), Is.EqualTo(0));
        }

        private static void AssertBlobIntArray(ref BlobArray<int> values, params int[] expectedValues)
        {
            Assert.That(values.Length, Is.EqualTo(expectedValues.Length));
            for (var i = 0; i < expectedValues.Length; i++)
                Assert.That(values[i], Is.EqualTo(expectedValues[i]));
        }

        private static void AssertIntArray(int[] values, params int[] expectedValues)
        {
            Assert.That(values, Is.Not.Null);
            Assert.That(values.Length, Is.EqualTo(expectedValues.Length));
            for (var i = 0; i < expectedValues.Length; i++)
                Assert.That(values[i], Is.EqualTo(expectedValues[i]));
        }

        private static void AssertRequirementContainsOnlyTagCodes(
            TagRequirementMask requirement,
            int allTagCode,
            int anyTagCode,
            int noneTagCode)
        {
            AssertMaskContainsOnlyTagCode(requirement.All, allTagCode);
            AssertMaskContainsOnlyTagCode(requirement.Any, anyTagCode);
            AssertMaskContainsOnlyTagCode(requirement.None, noneTagCode);
        }

        private static void AssertRequirementContainsOnlyTagCodes(
            TagRequirementMask requirement,
            int[] allTagCodes,
            int[] anyTagCodes,
            int[] noneTagCodes)
        {
            AssertMaskContainsOnlyTagCodes(requirement.All, allTagCodes);
            AssertMaskContainsOnlyTagCodes(requirement.Any, anyTagCodes);
            AssertMaskContainsOnlyTagCodes(requirement.None, noneTagCodes);
        }

        private static void AssertMaskContainsOnlyTagCode(CTagMask mask, int tagCode)
        {
            Assert.That(TagHelper.TryGetDenseIndex(tagCode, out var tagIndex), Is.True);
            Assert.That(mask.HasTag(tagIndex), Is.True);
            Assert.That(CountTags(mask), Is.EqualTo(1));
        }

        private static void AssertMaskContainsOnlyTagCodes(CTagMask mask, params int[] tagCodes)
        {
            for (var i = 0; i < tagCodes.Length; i++)
            {
                Assert.That(TagHelper.TryGetDenseIndex(tagCodes[i], out var tagIndex), Is.True);
                Assert.That(mask.HasTag(tagIndex), Is.True);
            }

            Assert.That(CountTags(mask), Is.EqualTo(tagCodes.Length));
        }

        private static TagRequirementMask CreateRequirementMask(
            int[] allTagCodes,
            int[] anyTagCodes,
            int[] noneTagCodes)
        {
            return new TagRequirementMask
            {
                All = CreateMaskFromTagCodes(allTagCodes),
                Any = CreateMaskFromTagCodes(anyTagCodes),
                None = CreateMaskFromTagCodes(noneTagCodes),
            };
        }

        private static CTagMask CreateMaskFromTagCodes(params int[] tagCodes)
        {
            var mask = new CTagMask();
            if (tagCodes == null)
                return mask;

            for (var i = 0; i < tagCodes.Length; i++)
            {
                Assert.That(TagHelper.TryGetDenseIndex(tagCodes[i], out var tagIndex), Is.True);
                mask.AddTag(tagIndex);
            }

            return mask;
        }

        private static void AddGrantedTagConfig(DynamicBuffer<BGrantedTagConfig> grantedTags, int tagCode)
        {
            Assert.That(TagHelper.TryGetDenseIndex(tagCode, out var tagIndex), Is.True);
            grantedTags.Add(new BGrantedTagConfig { TagIndex = tagIndex });
        }

        private static int CountTags(CTagMask mask)
        {
            var count = 0;
            for (var i = 0; i < CTagMask.Capacity; i++)
            {
                if (mask.HasTag(i))
                    count++;
            }

            return count;
        }

        private void AssertRuntimeInstanceHasRuntimeState(
            Entity effect,
            Entity target,
            int grantedAbilityCode)
        {
            Assert.That(_em.HasComponent<CGameplayEffectPrototype>(effect), Is.False);
            Assert.That(_em.HasComponent<CEffectContext>(effect), Is.True);
            Assert.That(_em.HasComponent<CEffectSpecData>(effect), Is.True);
            Assert.That(_em.HasComponent<CEffectLifecycle>(effect), Is.True);
            Assert.That(_em.HasBuffer<BResolvedModifier>(effect), Is.True);
            Assert.That(_em.HasBuffer<BAttributeCaptureValue>(effect), Is.True);
            Assert.That(_em.HasBuffer<BGrantedAbilityRuntime>(effect), Is.True);

            var context = _em.GetComponentData<CEffectContext>(effect);
            Assert.That(context.TargetAsc, Is.EqualTo(target));

            var spec = _em.GetComponentData<CEffectSpecData>(effect);
            Assert.That(spec.StackCount, Is.EqualTo(1));

            var captures = _em.GetBuffer<BAttributeCaptureValue>(effect);
            Assert.That(captures.Length, Is.EqualTo(1));
            Assert.That(captures[0].Value, Is.EqualTo(42f));

            var resolved = _em.GetBuffer<BResolvedModifier>(effect);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(42f));

            var runtimeAbilities = _em.GetBuffer<BGrantedAbilityRuntime>(effect);
            Assert.That(runtimeAbilities.Length, Is.EqualTo(1));
            Assert.That(runtimeAbilities[0].ConfigIndex, Is.EqualTo(0));
            Assert.That(_em.Exists(runtimeAbilities[0].AbilityEntity), Is.True);
            Assert.That(_em.GetComponentData<CAbilityBaseInfo>(runtimeAbilities[0].AbilityEntity).Code,
                Is.EqualTo(grantedAbilityCode));

            var durationDefinition = _em.GetComponentData<CDurationDefinition>(effect);
            Assert.That(durationDefinition.Duration, Is.EqualTo(30));
            Assert.That(durationDefinition.TimeUnit, Is.EqualTo(TimeUnit.Frame));

            var durationRuntime = _em.GetComponentData<CDurationRuntime>(effect);
            Assert.That(durationRuntime.Active, Is.True);
            Assert.That(durationRuntime.ActiveTime, Is.GreaterThanOrEqualTo(0));
            Assert.That(durationRuntime.RemainingTime, Is.EqualTo(durationRuntime.ResolvedDuration));

            var periodDefinition = _em.GetComponentData<CPeriodDefinition>(effect);
            Assert.That(periodDefinition.Period, Is.EqualTo(5));
            Assert.That(periodDefinition.ResetTimeCountWhenDeactivated, Is.True);

            var periodRuntime = _em.GetComponentData<CPeriodRuntime>(effect);
            Assert.That(periodRuntime.StartTime, Is.EqualTo(durationRuntime.ActiveTime));

            var stacking = _em.GetComponentData<CStackingRuntime>(effect);
            Assert.That(stacking.StackCount, Is.EqualTo(1));
        }

        private Entity FindEffectForTarget(int effectCode, Entity target)
        {
            using var effects = FindEffectsByCode(effectCode);
            for (var i = 0; i < effects.Length; i++)
            {
                var context = _em.GetComponentData<CEffectContext>(effects[i]);
                if (context.TargetAsc == target)
                    return effects[i];
            }

            Assert.Fail("Expected gameplay effect instance was not found.");
            return Entity.Null;
        }

        private NativeArray<Entity> FindEffectsByCode(int effectCode)
        {
            using var query = _em.CreateEntityQuery(
                ComponentType.ReadOnly<CEffectSpecData>(),
                ComponentType.ReadOnly<CEffectContext>());
            using var candidates = query.ToEntityArray(Allocator.Temp);
            using var matches = new NativeList<Entity>(Allocator.Temp);

            for (var i = 0; i < candidates.Length; i++)
            {
                var spec = _em.GetComponentData<CEffectSpecData>(candidates[i]);
                if (spec.GameplayEffectCode == effectCode)
                    matches.Add(candidates[i]);
            }

            return matches.ToArray(Allocator.Temp);
        }

        private void DestroyEffectsByCode(int effectCode)
        {
            using var effects = FindEffectsByCode(effectCode);
            for (var i = 0; i < effects.Length; i++)
            {
                if (_em.Exists(effects[i]))
                    _em.DestroyEntity(effects[i]);
            }
        }

        private void DestroyGrantedAbilities(Entity target)
        {
            if (target == Entity.Null || !_em.Exists(target) || !_em.HasBuffer<BGrantedAbility>(target))
                return;

            var abilities = _em.GetBuffer<BGrantedAbility>(target);
            for (var i = abilities.Length - 1; i >= 0; i--)
            {
                var ability = abilities[i].AbilityEntity;
                if (ability != Entity.Null && _em.Exists(ability))
                    _em.DestroyEntity(ability);
            }

            abilities.Clear();
        }

        private Entity CreateEntity()
        {
            var entity = _em.CreateEntity();
            _created.Add(entity);
            return entity;
        }

        private void RunCommandGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>().Update();
        }

        private void RunEffectGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASEffectGroup>().Update();
        }

        private int CountEntitiesWith(ComponentType componentType)
        {
            using var query = _em.CreateEntityQuery(componentType);
            return query.CalculateEntityCount();
        }

        private sealed class NoOpCueConfig : ConfCueBase
        {
            public override void LoadToGameplayEffectEntity(Entity ge)
            {
            }
        }

        private sealed class GeneratedGameplayEffectRow
        {
            public List<int> AssetTags;
            public List<int> GrantedTags;
            public GeneratedTagRequirementSpec ApplicationRequiredTags;
            public GeneratedTagRequirementSpec OngoingRequiredTags;
            public GeneratedTagRequirementSpec RemoveGameplayEffectsWithTags;
            public GeneratedTagRequirementSpec ImmunityTags;
            public GeneratedDurationRow Duration;
            public GeneratedPeriodRow Period;
            public List<GeneratedModifierRow> Modifiers;
            public List<GeneratedGrantedAbilityRow> GrantedAbility;
            public GeneratedStackingRow Stacking;
        }

        private sealed class GeneratedTagRequirementSpec
        {
            public List<int> All;
            public List<int> Any;
            public List<int> None;
        }

        private sealed class GeneratedDurationRow
        {
            public int TimeUnit;
            public int Time;
            public bool ResetStartTimeWhenActivated;
        }

        private sealed class GeneratedPeriodRow
        {
            public int Time;
            public int[] Effects;
            public bool FirstTrigger;
        }

        private sealed class GeneratedModifierRow
        {
            public int AttrSet;
            public int Attribute;
            public float Magnitude;
            public int Operation;
        }

        private sealed class GeneratedGrantedAbilityRow
        {
            public int ID;
            public int Level;
            public int ActivationPolicy;
            public int DeactivationPolicy;
            public int RemovePolicy;
        }

        private sealed class GeneratedStackingRow
        {
            public int StackingType;
            public int StackCode;
            public int LimitCount;
            public int DurationRefreshPolicy;
            public int PeriodResetPolicy;
            public int ExpirationPolicy;
            public bool DenyOverflowApplication;
            public bool ClearStackOnOverflow;
            public int[] OverflowEffects;
        }

        private sealed class MagnitudeDefinitionConfig : GameplayEffectComponentConfig
        {
            public BMagnitudeDefinition[] Definitions;

            public override void LoadToGameplayEffectEntity(Entity ge)
            {
                var buffer = _entityManager.AddBuffer<BMagnitudeDefinition>(ge);
                if (Definitions == null)
                    return;

                for (var i = 0; i < Definitions.Length; i++)
                    buffer.Add(Definitions[i]);
            }
        }
    }
}
