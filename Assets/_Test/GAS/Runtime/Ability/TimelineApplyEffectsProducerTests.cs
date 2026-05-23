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
