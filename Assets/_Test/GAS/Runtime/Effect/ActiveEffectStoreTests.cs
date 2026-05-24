using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.Runtime.Tests.Effect
{
    public sealed class ActiveEffectStoreTests
    {
        private readonly List<Entity> _entities = new();
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
        }

        [TearDown]
        public void TearDown()
        {
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
            for (var i = _entities.Count - 1; i >= 0; i--)
            {
                var entity = _entities[i];
                if (_em.Exists(entity))
                    _em.DestroyEntity(entity);
            }

            _entities.Clear();
        }

        [Test]
        public void AbilitySystemFactoryCreatesActiveEffectStore()
        {
            var asc = AbilitySystemEntityFactory.Create(_em);
            _entities.Add(asc);

            Assert.That(_em.HasComponent<CActiveEffectStore>(asc), Is.True);
            Assert.That(_em.HasBuffer<BActiveEffectSlot>(asc), Is.True);

            var store = _em.GetComponentData<CActiveEffectStore>(asc);
            Assert.That(store.Version, Is.EqualTo(ActiveEffectStore.CurrentVersion));
            Assert.That(store.NextSequence, Is.EqualTo(1));
            Assert.That(_em.GetBuffer<BActiveEffectSlot>(asc).Length, Is.EqualTo(0));
        }

        [Test]
        public void ActiveEffectStoreDoesNotCreateMissingStoreInHotPath()
        {
            var source = CreateEntity();
            var target = CreateEntity();
            var effect = CreateDurationEffect(source, target, effectCode: 94101);
            var context = _em.GetComponentData<CEffectContext>(effect);
            var duration = _em.GetComponentData<CDurationRuntime>(effect);

            var upserted = ActiveEffectStore.TryUpsertDurationEffect(
                _em,
                effect,
                context,
                duration,
                EActiveEffectSlotState.Active,
                currentFrame: 10);

            Assert.That(upserted, Is.False);
            Assert.That(_em.HasComponent<CActiveEffectStore>(target), Is.False);
            Assert.That(_em.HasBuffer<BActiveEffectSlot>(target), Is.False);
        }

        [Test]
        public void DurationLifecycleMirrorsIntoOwnerActiveEffectSlot()
        {
            var source = CreateEntity();
            var target = AbilitySystemEntityFactory.Create(_em);
            _entities.Add(target);
            var effect = CreateDurationEffect(source, target, effectCode: 94102);
            var context = _em.GetComponentData<CEffectContext>(effect);
            var duration = _em.GetComponentData<CDurationRuntime>(effect);

            EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
            _em.SetComponentData(effect, duration);

            var slot = GetSingleSlot(target);
            Assert.That(slot.Sequence, Is.EqualTo(1));
            Assert.That(slot.ActiveEffectEntity, Is.EqualTo(effect));
            Assert.That(slot.State, Is.EqualTo(EActiveEffectSlotState.Active));
            Assert.That(slot.PreviousState, Is.EqualTo(EActiveEffectSlotState.Active));
            Assert.That(slot.GameplayEffectCode, Is.EqualTo(94102));
            Assert.That(slot.Level, Is.EqualTo(3));
            Assert.That(slot.StackCount, Is.EqualTo(2));
            Assert.That(slot.StartFrame, Is.EqualTo(10));
            Assert.That(slot.StateStartFrame, Is.EqualTo(10));
            Assert.That(slot.DurationFrame, Is.EqualTo(100));
            Assert.That(slot.RemainingFrame, Is.EqualTo(100));
            Assert.That(slot.PeriodFrame, Is.EqualTo(5));
            Assert.That(slot.LastPeriodFrame, Is.EqualTo(10));
            Assert.That(HasFlag(slot, EActiveEffectSlotFlags.LegacyEntityBacked), Is.True);
            Assert.That(HasFlag(slot, EActiveEffectSlotFlags.HasDuration), Is.True);
            Assert.That(HasFlag(slot, EActiveEffectSlotFlags.HasPeriod), Is.True);
            Assert.That(HasFlag(slot, EActiveEffectSlotFlags.HasStacking), Is.True);

            EffectRuntimeUtility.DeactivateOngoingEffect(_em, effect, currentFrame: 15);
            slot = GetSingleSlot(target);
            Assert.That(slot.State, Is.EqualTo(EActiveEffectSlotState.Inhibited));
            Assert.That(slot.PreviousState, Is.EqualTo(EActiveEffectSlotState.Active));
            Assert.That(slot.StateStartFrame, Is.EqualTo(15));
            Assert.That(slot.RemainingFrame, Is.EqualTo(95));

            EffectRuntimeUtility.ReactivateOngoingEffect(_em, effect, currentFrame: 20);
            slot = GetSingleSlot(target);
            Assert.That(slot.State, Is.EqualTo(EActiveEffectSlotState.Active));
            Assert.That(slot.PreviousState, Is.EqualTo(EActiveEffectSlotState.Inhibited));
            Assert.That(slot.StateStartFrame, Is.EqualTo(20));

            EffectRuntimeUtility.MarkEffectForRemoval(_em, effect, currentFrame: 30);
            slot = GetSingleSlot(target);
            Assert.That(slot.State, Is.EqualTo(EActiveEffectSlotState.PendingRemove));
            Assert.That(slot.PreviousState, Is.EqualTo(EActiveEffectSlotState.Active));
            Assert.That(slot.StateStartFrame, Is.EqualTo(30));

            EffectRuntimeUtility.CleanupActiveEffect(_em, effect);
            Assert.That(_em.GetBuffer<BActiveEffectSlot>(target).Length, Is.EqualTo(0));
            Assert.That(_em.Exists(effect), Is.False);
        }

        private Entity CreateDurationEffect(Entity source, Entity target, int effectCode)
        {
            var effect = CreateEntity();
            _em.AddComponentData(effect, new CEffectContext
            {
                SourceAsc = source,
                TargetAsc = target,
                Instigator = source,
                Causer = source,
                ContextId = effectCode,
            });
            _em.AddComponentData(effect, new CEffectSpecData
            {
                GameplayEffectCode = effectCode,
                Level = 3,
                StackCount = 2,
            });
            _em.AddComponentData(effect, new CDurationDefinition
            {
                Duration = 100,
                TimeUnit = TimeUnit.Frame,
                StopTickWhenDeactivated = true,
            });
            _em.AddComponentData(effect, new CDurationRuntime
            {
                ResolvedDuration = 100,
                ResolvedTimeUnit = TimeUnit.Frame,
                Active = false,
                ActiveTime = 0,
                RemainingTime = 100,
            });
            _em.AddComponentData(effect, new CPeriodDefinition
            {
                Period = 5,
            });
            _em.AddComponentData(effect, new CStackingDefinition
            {
                StackingCode = 77,
                LimitCount = 4,
            });
            _em.AddComponentData(effect, new CStackingRuntime
            {
                StackCount = 2,
            });
            return effect;
        }

        private Entity CreateEntity()
        {
            var entity = _em.CreateEntity();
            _entities.Add(entity);
            return entity;
        }

        private BActiveEffectSlot GetSingleSlot(Entity target)
        {
            var slots = _em.GetBuffer<BActiveEffectSlot>(target);
            Assert.That(slots.Length, Is.EqualTo(1));
            return slots[0];
        }

        private static bool HasFlag(BActiveEffectSlot slot, EActiveEffectSlotFlags flag)
        {
            return (slot.Flags & (int)flag) != 0;
        }
    }
}
