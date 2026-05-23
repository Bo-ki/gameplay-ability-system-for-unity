using NUnit.Framework;
using Unity.Entities;

namespace GAS.Runtime.Tests
{
    public sealed class AbilitySystemFacadeTests
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
        public void RequestGameplayEffectToSelfWritesSetByCallerValuesToRequest()
        {
            var asc = AbilitySystemFacade.Create();
            var request = Entity.Null;

            try
            {
                request = asc.RequestGameplayEffectToSelf(
                    10001,
                    new[]
                    {
                        new BSetByCallerValue { Key = 3001, Value = 25f },
                        new BSetByCallerValue { Key = 3002, Value = 7f },
                    },
                    level: 2);

                Assert.That(request, Is.Not.EqualTo(Entity.Null));
                Assert.That(_em.HasComponent<CApplyGameplayEffectRequest>(request), Is.True);
                Assert.That(_em.HasBuffer<BSetByCallerValue>(request), Is.True);

                var effectRequest = _em.GetComponentData<CApplyGameplayEffectRequest>(request);
                Assert.That(effectRequest.GameplayEffectCode, Is.EqualTo(10001));
                Assert.That(effectRequest.Level, Is.EqualTo(2));

                var values = _em.GetBuffer<BSetByCallerValue>(request);
                Assert.That(values.Length, Is.EqualTo(2));
                Assert.That(values[0].Key, Is.EqualTo(3001));
                Assert.That(values[0].Value, Is.EqualTo(25f));
                Assert.That(values[1].Key, Is.EqualTo(3002));
                Assert.That(values[1].Value, Is.EqualTo(7f));
            }
            finally
            {
                if (request != Entity.Null && _em.Exists(request))
                    _em.DestroyEntity(request);

                if (asc.Entity != Entity.Null && _em.Exists(asc.Entity))
                    _em.DestroyEntity(asc.Entity);
            }
        }

        [Test]
        public void RequestGameplayEffectToWritesSetByCallerValuesToRequest()
        {
            var source = AbilitySystemFacade.Create();
            var target = AbilitySystemFacade.Create();
            var request = Entity.Null;

            try
            {
                request = source.RequestGameplayEffectTo(
                    10002,
                    target,
                    new[]
                    {
                        new BSetByCallerValue { Key = 4001, Value = 13f },
                    });

                Assert.That(request, Is.Not.EqualTo(Entity.Null));
                Assert.That(_em.HasBuffer<BSetByCallerValue>(request), Is.True);

                var targets = _em.GetBuffer<BTargetEntity>(request);
                Assert.That(targets.Length, Is.EqualTo(1));
                Assert.That(targets[0].TargetAsc, Is.EqualTo(target.Entity));

                var values = _em.GetBuffer<BSetByCallerValue>(request);
                Assert.That(values.Length, Is.EqualTo(1));
                Assert.That(values[0].Key, Is.EqualTo(4001));
                Assert.That(values[0].Value, Is.EqualTo(13f));
            }
            finally
            {
                if (request != Entity.Null && _em.Exists(request))
                    _em.DestroyEntity(request);

                if (source.Entity != Entity.Null && _em.Exists(source.Entity))
                    _em.DestroyEntity(source.Entity);

                if (target.Entity != Entity.Null && _em.Exists(target.Entity))
                    _em.DestroyEntity(target.Entity);
            }
        }

        [Test]
        public void AbilityCommandFacadeMethodsReturnRequestEntities()
        {
            var asc = AbilitySystemFacade.Create();
            var activateRequest = Entity.Null;
            var endRequest = Entity.Null;
            var cancelRequest = Entity.Null;
            var removeRequest = Entity.Null;

            try
            {
                activateRequest = asc.TryActivateAbility(2001);
                endRequest = asc.TryEndAbility(2002);
                cancelRequest = asc.TryCancelAbility(2003);
                removeRequest = asc.RemoveAbility(2004);

                AssertAbilityCommandRequest(activateRequest, EAbilityCommandType.Activate, 2001, asc.Entity);
                AssertAbilityCommandRequest(endRequest, EAbilityCommandType.End, 2002, asc.Entity);
                AssertAbilityCommandRequest(cancelRequest, EAbilityCommandType.Cancel, 2003, asc.Entity);
                AssertAbilityCommandRequest(removeRequest, EAbilityCommandType.Remove, 2004, asc.Entity);
            }
            finally
            {
                DestroyIfExists(activateRequest);
                DestroyIfExists(endRequest);
                DestroyIfExists(cancelRequest);
                DestroyIfExists(removeRequest);
                DestroyIfExists(asc.Entity);
            }
        }

        [Test]
        public void TryActivateAbilityWithTargetWritesTargetAscToRequest()
        {
            var source = AbilitySystemFacade.Create();
            var target = AbilitySystemFacade.Create();
            var requestByFacade = Entity.Null;
            var requestByEntity = Entity.Null;

            try
            {
                requestByFacade = source.TryActivateAbility(2101, target);
                requestByEntity = source.TryActivateAbility(2102, target.Entity);

                AssertAbilityCommandRequest(requestByFacade, EAbilityCommandType.Activate, 2101, source.Entity);
                AssertAbilityCommandRequest(requestByEntity, EAbilityCommandType.Activate, 2102, source.Entity);
                Assert.That(_em.GetComponentData<CAbilityCommandRequest>(requestByFacade).TargetAsc, Is.EqualTo(target.Entity));
                Assert.That(_em.GetComponentData<CAbilityCommandRequest>(requestByEntity).TargetAsc, Is.EqualTo(target.Entity));
            }
            finally
            {
                DestroyIfExists(requestByFacade);
                DestroyIfExists(requestByEntity);
                DestroyIfExists(source.Entity);
                DestroyIfExists(target.Entity);
            }
        }

        [Test]
        public void ObservationReadsStableMirrorWithoutFacadeCache()
        {
            var asc = AbilitySystemFacade.Create();

            try
            {
                _em.AddComponentData(asc.Entity, new CAscBasicData { Level = 7 });
                var attributes = _em.GetBuffer<BAttribute>(asc.Entity);
                attributes.Add(new BAttribute
                {
                    AttrSetCode = 10,
                    Code = 20,
                    BaseValue = 100f,
                    CurrentValue = 75f,
                });

                Assert.That(asc.TryGetLevel(out var level), Is.True);
                Assert.That(level, Is.EqualTo(7));
                Assert.That(asc.GetLevel(), Is.EqualTo(7));

                Assert.That(asc.TryGetAttributeCurrentValue(10, 20, out var currentValue), Is.True);
                Assert.That(currentValue, Is.EqualTo(75f));
                Assert.That(asc.GetAttrCurrentValue(10, 20), Is.EqualTo(75f));

                Assert.That(asc.TryGetAttributeBaseValue(10, 20, out var baseValue), Is.True);
                Assert.That(baseValue, Is.EqualTo(100f));
                Assert.That(asc.GetAttrBaseValue(10, 20), Is.EqualTo(100f));

                attributes[0] = new BAttribute
                {
                    AttrSetCode = 10,
                    Code = 20,
                    BaseValue = 100f,
                    CurrentValue = 64f,
                };

                Assert.That(asc.Observation.TryGetAttributeCurrentValue(10, 20, out var updatedValue), Is.True);
                Assert.That(updatedValue, Is.EqualTo(64f));
            }
            finally
            {
                DestroyIfExists(asc.Entity);
            }
        }

        [Test]
        public void PeekPresentationEventsCopiesCurrentOutboxWithoutDraining()
        {
            var asc = AbilitySystemFacade.Create();

            try
            {
                var events = _em.GetBuffer<BPresentationEvent>(asc.Entity);
                events.Add(new BPresentationEvent
                {
                    Frame = 12,
                    Sequence = 34,
                    Kind = EPresentationEventKind.GameplayEvent,
                    GameplayEventType = EGameplayEventType.GameplayEffectApplied,
                    TargetAsc = asc.Entity,
                    EventCode = 1001,
                    Value = 5f,
                });
                events.Add(new BPresentationEvent
                {
                    Frame = 12,
                    Kind = EPresentationEventKind.AttributeChange,
                    TargetAsc = asc.Entity,
                    AttrSetCode = 10,
                    AttributeCode = 20,
                    OldValue = 8f,
                    NewValue = 9f,
                });

                var output = new GasPresentationEventView[4];
                var copied = asc.PeekPresentationEvents(output);

                Assert.That(copied, Is.EqualTo(2));
                Assert.That(asc.PresentationEventCount, Is.EqualTo(2));
                Assert.That(_em.GetBuffer<BPresentationEvent>(asc.Entity).Length, Is.EqualTo(2));
                Assert.That(output[0].Kind, Is.EqualTo(EPresentationEventKind.GameplayEvent));
                Assert.That(output[0].GameplayEventType, Is.EqualTo(EGameplayEventType.GameplayEffectApplied));
                Assert.That(output[0].Sequence, Is.EqualTo(34));
                Assert.That(output[0].Value, Is.EqualTo(5f));
                Assert.That(output[1].Kind, Is.EqualTo(EPresentationEventKind.AttributeChange));
                Assert.That(output[1].AttrSetCode, Is.EqualTo(10));
                Assert.That(output[1].AttributeCode, Is.EqualTo(20));
                Assert.That(output[1].OldValue, Is.EqualTo(8f));
                Assert.That(output[1].NewValue, Is.EqualTo(9f));
            }
            finally
            {
                DestroyIfExists(asc.Entity);
            }
        }

        private void AssertAbilityCommandRequest(
            Entity request,
            EAbilityCommandType commandType,
            int abilityCode,
            Entity owner)
        {
            Assert.That(request, Is.Not.EqualTo(Entity.Null));
            Assert.That(_em.HasComponent<CAbilityCommandRequest>(request), Is.True);

            var command = _em.GetComponentData<CAbilityCommandRequest>(request);
            Assert.That(command.Owner, Is.EqualTo(owner));
            Assert.That(command.CommandType, Is.EqualTo(commandType));
            Assert.That(command.AbilityCode, Is.EqualTo(abilityCode));
        }

        private void DestroyIfExists(Entity entity)
        {
            if (entity != Entity.Null && _em.Exists(entity))
                _em.DestroyEntity(entity);
        }
    }
}
