using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Tests.Ability
{
    public sealed class TimelineApplyEffectsProducerTests
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
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
            ClearTransientEventBuffers();
            ClearEffectCommandStream();
        }

        [Test]
        public void RequestApplyEffectsWithCatchSelfWritesSelfTargetRequest()
        {
            var owner = _em.CreateEntity();
            var ability = CreateAbility(owner, level: 5);
            var requests = new List<Entity>();

            try
            {
                var count = TimelineApplyEffectsProducer.RequestApplyEffects(
                    _em,
                    ability,
                    Entity.Null,
                    new[] { 51001 },
                    new CatchSelf(),
                    new XParamNone(),
                    requests);

                Assert.That(count, Is.EqualTo(1));
                Assert.That(requests.Count, Is.EqualTo(1));

                var request = requests[0];
                var effectRequest = _em.GetComponentData<CApplyGameplayEffectRequest>(request);
                Assert.That(effectRequest.SourceAsc, Is.EqualTo(owner));
                Assert.That(effectRequest.SourceAbility, Is.EqualTo(ability));
                Assert.That(effectRequest.Instigator, Is.EqualTo(owner));
                Assert.That(effectRequest.Causer, Is.EqualTo(ability));
                Assert.That(effectRequest.GameplayEffectCode, Is.EqualTo(51001));
                Assert.That(effectRequest.Level, Is.EqualTo(5));

                AssertTargetData(request, owner, ability, ETargetDataKind.Self, owner);
                AssertNoGameplayEffectInstance(51001);
            }
            finally
            {
                DestroyAll(requests);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void RequestApplyEffectsWithCatchSelfSimpleInstantWritesEffectCommandStream()
        {
            const int effectCode = 51101;
            const int attrSetCode = 10;
            const int attributeCode = 20;

            var owner = CreateAscWithAttribute(attrSetCode, attributeCode, 100f);
            var ability = CreateAbility(owner, level: 5);
            var requests = new List<Entity>();

            try
            {
                RegisterSimpleModifierEffect(effectCode, attrSetCode, attributeCode, EModifierOp.Subtract, 12f);

                var count = TimelineApplyEffectsProducer.RequestApplyEffects(
                    _em,
                    ability,
                    Entity.Null,
                    new[] { effectCode },
                    new CatchSelf(),
                    new XParamNone(),
                    requests);

                Assert.That(count, Is.EqualTo(1));
                Assert.That(requests.Count, Is.EqualTo(0));
                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));
                AssertNoGameplayEffectInstance(effectCode);

                var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
                var commands = _em.GetBuffer<BEffectCommand>(streamEntity);
                Assert.That(commands.Length, Is.EqualTo(1));

                var command = commands[0];
                Assert.That(command.Kind, Is.EqualTo(EEffectCommandKind.Instant));
                Assert.That(command.Source, Is.EqualTo(EEffectCommandSource.Ability));
                Assert.That(command.SourceAsc, Is.EqualTo(owner));
                Assert.That(command.TargetAsc, Is.EqualTo(owner));
                Assert.That(command.SourceAbility, Is.EqualTo(ability));
                Assert.That(command.GameplayEffectCode, Is.EqualTo(effectCode));
                Assert.That(command.Level, Is.EqualTo(5));
                Assert.That(command.TargetDataKind, Is.EqualTo(ETargetDataKind.Self));

                var attribute = _em.GetBuffer<BAttribute>(owner)[0];
                Assert.That(attribute.BaseValue, Is.EqualTo(100f));
                Assert.That(attribute.CurrentValue, Is.EqualTo(100f));
            }
            finally
            {
                DestroyAll(requests);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void RequestApplyEffectsWithCatchTargetSimpleInstantWritesEffectCommandStream()
        {
            const int effectCode = 51102;
            const int attrSetCode = 11;
            const int attributeCode = 21;

            var owner = _em.CreateEntity();
            var target = CreateAscWithAttribute(attrSetCode, attributeCode, 100f);
            var ability = CreateAbility(owner, level: 2);
            var requests = new List<Entity>();

            try
            {
                RegisterSimpleModifierEffect(effectCode, attrSetCode, attributeCode, EModifierOp.Subtract, 15f);

                var count = TimelineApplyEffectsProducer.RequestApplyEffects(
                    _em,
                    ability,
                    target,
                    new[] { effectCode },
                    new CatchTarget(),
                    new XParamNone(),
                    requests);

                Assert.That(count, Is.EqualTo(1));
                Assert.That(requests.Count, Is.EqualTo(0));
                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));

                var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
                var commands = _em.GetBuffer<BEffectCommand>(streamEntity);
                Assert.That(commands.Length, Is.EqualTo(1));
                Assert.That(commands[0].TargetAsc, Is.EqualTo(target));
                Assert.That(commands[0].TargetDataKind, Is.EqualTo(ETargetDataKind.Entity));
                Assert.That(commands[0].GameplayEffectCode, Is.EqualTo(effectCode));
            }
            finally
            {
                DestroyAll(requests);
                DestroyIfExists(ability);
                DestroyIfExists(target);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void RequestApplyEffectsWithCueOnApplyWritesStreamAndProjectsCue()
        {
            const int effectCode = 51103;
            const int attrSetCode = 12;
            const int attributeCode = 22;
            const int cueCode = 3001;

            var owner = CreateAscWithAttribute(attrSetCode, attributeCode, 100f);
            var ability = CreateAbility(owner, level: 3);
            var requests = new List<Entity>();

            try
            {
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

                var count = TimelineApplyEffectsProducer.RequestApplyEffects(
                    _em,
                    ability,
                    Entity.Null,
                    new[] { effectCode },
                    new CatchSelf(),
                    new XParamNone(),
                    requests);

                Assert.That(count, Is.EqualTo(1));
                Assert.That(requests.Count, Is.EqualTo(0));
                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));

                var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
                var commands = _em.GetBuffer<BEffectCommand>(streamEntity);
                Assert.That(commands.Length, Is.EqualTo(1));
                Assert.That(commands[0].GameplayEffectCode, Is.EqualTo(effectCode));
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
                Assert.That(attribute.BaseValue, Is.EqualTo(88f));
                Assert.That(attribute.CurrentValue, Is.EqualTo(88f));
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
                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));
                AssertNoGameplayEffectInstance(effectCode);
            }
            finally
            {
                DestroyAll(requests);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void RequestApplyEffectsWithCatchTargetWritesOneRequestPerEffect()
        {
            var owner = _em.CreateEntity();
            var target = _em.CreateEntity();
            var ability = CreateAbility(owner, level: 2);
            var requests = new List<Entity>();

            try
            {
                var count = TimelineApplyEffectsProducer.RequestApplyEffects(
                    _em,
                    ability,
                    target,
                    new[] { 51002, 51003 },
                    new CatchTarget(),
                    new XParamNone(),
                    requests);

                Assert.That(count, Is.EqualTo(2));
                Assert.That(requests.Count, Is.EqualTo(2));

                AssertRequest(requests[0], owner, ability, 51002, 2);
                AssertRequest(requests[1], owner, ability, 51003, 2);
                AssertTargetData(requests[0], owner, ability, ETargetDataKind.Entity, target);
                AssertTargetData(requests[1], owner, ability, ETargetDataKind.Entity, target);
                AssertNoGameplayEffectInstance(51002);
                AssertNoGameplayEffectInstance(51003);
            }
            finally
            {
                DestroyAll(requests);
                DestroyIfExists(ability);
                DestroyIfExists(target);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void RequestApplyEffectsWithMultipleTargetsWritesEntityListRequest()
        {
            var owner = _em.CreateEntity();
            var targetA = _em.CreateEntity();
            var targetB = _em.CreateEntity();
            var ability = CreateAbility(owner, level: 3);
            var requests = new List<Entity>();

            try
            {
                var count = TimelineApplyEffectsProducer.RequestApplyEffects(
                    _em,
                    ability,
                    Entity.Null,
                    new[] { 51004 },
                    new FixedTargetsCatcher(targetA, targetB),
                    null,
                    requests);

                Assert.That(count, Is.EqualTo(1));
                Assert.That(requests.Count, Is.EqualTo(1));

                AssertRequest(requests[0], owner, ability, 51004, 3);
                AssertTargetData(requests[0], owner, ability, ETargetDataKind.EntityList, targetA, targetB);
                AssertNoGameplayEffectInstance(51004);
            }
            finally
            {
                DestroyAll(requests);
                DestroyIfExists(ability);
                DestroyIfExists(targetA);
                DestroyIfExists(targetB);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void RequestApplyEffectsWithMultipleSimpleInstantTargetsWritesCommandPerTarget()
        {
            const int effectCode = 51104;
            const int attrSetCode = 13;
            const int attributeCode = 23;

            var owner = _em.CreateEntity();
            var targetA = CreateAscWithAttribute(attrSetCode, attributeCode, 100f);
            var targetB = CreateAscWithAttribute(attrSetCode, attributeCode, 60f);
            var ability = CreateAbility(owner, level: 3);
            var requests = new List<Entity>();

            try
            {
                RegisterSimpleModifierEffect(effectCode, attrSetCode, attributeCode, EModifierOp.Subtract, 11f);

                var count = TimelineApplyEffectsProducer.RequestApplyEffects(
                    _em,
                    ability,
                    Entity.Null,
                    new[] { effectCode },
                    new FixedTargetsCatcher(targetA, targetB),
                    null,
                    requests);

                Assert.That(count, Is.EqualTo(1));
                Assert.That(requests.Count, Is.EqualTo(0));
                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));
                AssertNoGameplayEffectInstance(effectCode);

                var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
                var commands = _em.GetBuffer<BEffectCommand>(streamEntity);
                Assert.That(commands.Length, Is.EqualTo(2));
                Assert.That(commands[0].TargetAsc, Is.EqualTo(targetA));
                Assert.That(commands[1].TargetAsc, Is.EqualTo(targetB));
                Assert.That(commands[0].TargetDataKind, Is.EqualTo(ETargetDataKind.EntityList));
                Assert.That(commands[1].TargetDataKind, Is.EqualTo(ETargetDataKind.EntityList));
                Assert.That(commands[0].GameplayEffectCode, Is.EqualTo(effectCode));
                Assert.That(commands[1].GameplayEffectCode, Is.EqualTo(effectCode));

                RunCommandGroup();

                var specs = _em.GetBuffer<BInstantEffectSpec>(streamEntity);
                var deltas = _em.GetBuffer<BAttributeDelta>(streamEntity);
                var facts = _em.GetBuffer<BTypedSimulationFact>(streamEntity);

                Assert.That(specs.Length, Is.EqualTo(2));
                Assert.That(deltas.Length, Is.EqualTo(2));
                Assert.That(facts.Length, Is.EqualTo(2));
                Assert.That(_em.GetBuffer<BAttribute>(targetA)[0].BaseValue, Is.EqualTo(89f));
                Assert.That(_em.GetBuffer<BAttribute>(targetB)[0].BaseValue, Is.EqualTo(49f));
                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));
                AssertNoGameplayEffectInstance(effectCode);
            }
            finally
            {
                DestroyAll(requests);
                DestroyIfExists(ability);
                DestroyIfExists(targetA);
                DestroyIfExists(targetB);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void RequestApplyEffectsSkipsInvalidEffectsAndTargets()
        {
            var owner = _em.CreateEntity();
            var destroyedTarget = _em.CreateEntity();
            var ability = CreateAbility(owner, level: 1);
            var requests = new List<Entity>();

            try
            {
                _em.DestroyEntity(destroyedTarget);

                var count = TimelineApplyEffectsProducer.RequestApplyEffects(
                    _em,
                    ability,
                    Entity.Null,
                    new[] { 0, -1, 51005 },
                    new FixedTargetsCatcher(Entity.Null, destroyedTarget),
                    null,
                    requests);

                Assert.That(count, Is.EqualTo(0));
                Assert.That(requests.Count, Is.EqualTo(0));
                AssertNoGameplayEffectInstance(51005);
            }
            finally
            {
                DestroyAll(requests);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
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

        private Entity CreateAscWithAttribute(int attrSetCode, int attributeCode, float value)
        {
            var asc = _em.CreateEntity();
            _em.AddBuffer<BAttribute>(asc).Add(new BAttribute
            {
                AttrSetCode = attrSetCode,
                Code = attributeCode,
                BaseValue = value,
                CurrentValue = value,
            });
            return asc;
        }

        private static void RegisterSimpleModifierEffect(
            int effectCode,
            int attrSetCode,
            int attributeCode,
            EModifierOp op,
            float magnitude)
        {
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
                                    Operation = op,
                                    Magnitude = magnitude,
                                },
                            },
                        },
                    })
                    : null);
        }

        private void AssertRequest(Entity request, Entity owner, Entity ability, int effectCode, int level)
        {
            var effectRequest = _em.GetComponentData<CApplyGameplayEffectRequest>(request);
            Assert.That(effectRequest.SourceAsc, Is.EqualTo(owner));
            Assert.That(effectRequest.SourceAbility, Is.EqualTo(ability));
            Assert.That(effectRequest.Instigator, Is.EqualTo(owner));
            Assert.That(effectRequest.Causer, Is.EqualTo(ability));
            Assert.That(effectRequest.GameplayEffectCode, Is.EqualTo(effectCode));
            Assert.That(effectRequest.Level, Is.EqualTo(level));
        }

        private void AssertTargetData(
            Entity request,
            Entity owner,
            Entity ability,
            ETargetDataKind kind,
            params Entity[] expectedTargets)
        {
            var header = _em.GetComponentData<CTargetDataHeader>(request);
            Assert.That(header.SourceAsc, Is.EqualTo(owner));
            Assert.That(header.SourceAbility, Is.EqualTo(ability));
            Assert.That(header.Kind, Is.EqualTo(kind));

            var targets = _em.GetBuffer<BTargetEntity>(request);
            Assert.That(targets.Length, Is.EqualTo(expectedTargets.Length));
            for (var i = 0; i < expectedTargets.Length; i++)
                Assert.That(targets[i].TargetAsc, Is.EqualTo(expectedTargets[i]));
        }

        private void AssertNoGameplayEffectInstance(int effectCode)
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<CEffectSpecData>());
            using var effects = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < effects.Length; i++)
            {
                var spec = _em.GetComponentData<CEffectSpecData>(effects[i]);
                Assert.That(spec.GameplayEffectCode, Is.Not.EqualTo(effectCode));
            }
        }

        private int CountApplyRequestsByCode(int effectCode)
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<CApplyGameplayEffectRequest>());
            using var requests = query.ToEntityArray(Allocator.Temp);
            var count = 0;
            for (var i = 0; i < requests.Length; i++)
            {
                if (_em.GetComponentData<CApplyGameplayEffectRequest>(requests[i]).GameplayEffectCode == effectCode)
                    count++;
            }

            return count;
        }

        private void ClearEffectCommandStream()
        {
            if (_em == default)
                return;

            if (EffectCommandSpecStream.TryGetSingleton(_em, out var streamEntity))
                EffectCommandSpecStream.ClearFrameLocalData(_em, streamEntity, 0);
        }

        private void ClearTransientEventBuffers()
        {
            if (_em == default
                || !GASManager.IsInitialized
                || GASManager.EntityEventBus == Entity.Null
                || !_em.Exists(GASManager.EntityEventBus))
            {
                return;
            }

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

        private static void RunCommandGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>().Update();
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

        private void DestroyAll(List<Entity> entities)
        {
            for (var i = 0; i < entities.Count; i++)
                DestroyIfExists(entities[i]);
        }

        private void DestroyIfExists(Entity entity)
        {
            if (entity != Entity.Null && _em.Exists(entity))
                _em.DestroyEntity(entity);
        }

        private sealed class FixedTargetsCatcher : TargetCatcherBase
        {
            private readonly Entity[] _targets;

            public FixedTargetsCatcher(params Entity[] targets)
            {
                _targets = targets;
            }

            protected override void CatchTargetsNonAlloc(Entity mainTarget, List<Entity> results)
            {
                for (var i = 0; i < _targets.Length; i++)
                    results.Add(_targets[i]);
            }
        }
    }
}
