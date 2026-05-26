using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
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
            ResetGlobalIndexStore();
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
            ResetGlobalIndexStore();
        }

        [Test]
        public void AbilitySystemFactoryCreatesActiveEffectStore()
        {
            var asc = AbilitySystemEntityFactory.Create(_em);
            _entities.Add(asc);

            Assert.That(_em.HasComponent<CActiveEffectStore>(asc), Is.True);
            Assert.That(_em.HasBuffer<BActiveEffectSlot>(asc), Is.True);
            Assert.That(_em.HasBuffer<BActiveEffectCleanupRecord>(asc), Is.True);

            var store = _em.GetComponentData<CActiveEffectStore>(asc);
            Assert.That(store.Version, Is.EqualTo(ActiveEffectStore.CurrentVersion));
            Assert.That(store.NextSequence, Is.EqualTo(1));
            Assert.That(store.LastCleanupFrame, Is.EqualTo(0));
            Assert.That(store.CleanupRecordCount, Is.EqualTo(0));
            Assert.That(_em.GetBuffer<BActiveEffectSlot>(asc).Length, Is.EqualTo(0));
            Assert.That(_em.GetBuffer<BActiveEffectCleanupRecord>(asc).Length, Is.EqualTo(0));
        }

        [Test]
        public void GameplayEffectFactoryCreatesGlobalIndexStableRow()
        {
            var effect = GameplayEffectEntityFactory.CreateFromConfig(
                _em,
                new GameplayEffectComponentConfig[0]);
            _entities.Add(effect);

            Assert.That(_em.HasComponent<CActiveEffectGlobalIndexStableRow>(effect), Is.True);
            var row = _em.GetComponentData<CActiveEffectGlobalIndexStableRow>(effect);
            Assert.That(row.Version, Is.EqualTo(ActiveEffectStore.CurrentVersion));
            Assert.That(row.IsIndexed, Is.EqualTo(0));
            Assert.That(row.ActiveEffectEntity, Is.EqualTo(effect));
            Assert.That(row.BucketIndex, Is.EqualTo(-1));
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
        public void EnsureGlobalIndexStoreCreatesExplicitOwnerAndHotPathDoesNotCreateMissingOwner()
        {
            using var world = new World("ActiveEffectGlobalIndexMissingOwnerTest");
            var em = world.EntityManager;
            var source = em.CreateEntity();
            var target = em.CreateEntity();
            em.AddComponentData(target, ActiveEffectStore.CreateDefault());
            em.AddBuffer<BActiveEffectSlot>(target);
            em.AddBuffer<BActiveEffectCleanupRecord>(target);
            var effect = em.CreateEntity();
            em.AddComponentData(effect, new CEffectContext
            {
                SourceAsc = source,
                TargetAsc = target,
                Instigator = source,
                Causer = source,
                ContextId = 94116,
            });
            em.AddComponentData(effect, new CEffectSpecData
            {
                GameplayEffectCode = 94116,
                Level = 3,
                StackCount = 2,
            });
            em.AddComponentData(effect, new CDurationRuntime
            {
                ResolvedDuration = 100,
                ResolvedTimeUnit = TimeUnit.Frame,
                Active = true,
                ActiveTime = 10,
                RemainingTime = 100,
            });
            var context = em.GetComponentData<CEffectContext>(effect);
            var duration = em.GetComponentData<CDurationRuntime>(effect);

            var upserted = ActiveEffectStore.TryUpsertDurationEffect(
                em,
                effect,
                context,
                duration,
                EActiveEffectSlotState.Active,
                currentFrame: 10);

            Assert.That(upserted, Is.True);
            Assert.That(ActiveEffectStore.TryGetGlobalIndexStore(em, out _), Is.False);

            var indexOwner = ActiveEffectStore.EnsureGlobalIndexStore(em);
            Assert.That(em.Exists(indexOwner), Is.True);
            Assert.That(em.HasComponent<CActiveEffectGlobalIndexStore>(indexOwner), Is.True);
            Assert.That(em.HasBuffer<BGlobalActiveEffectIndex>(indexOwner), Is.True);
            Assert.That(em.GetBuffer<BGlobalActiveEffectIndex>(indexOwner).Capacity, Is.GreaterThanOrEqualTo(ActiveEffectStore.InlineGlobalIndexCapacity));
            AssertGlobalIndexBucketOwners(em, indexOwner);

            Assert.That(ActiveEffectStore.TrySyncGlobalIndexOwner(em, target, currentFrame: 10), Is.True);
            Assert.That(em.GetBuffer<BGlobalActiveEffectIndex>(indexOwner).Length, Is.EqualTo(1));
            AssertGlobalIndexEntryInResolvedBucket(em, effect, target);
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
            Assert.That(slot.ActiveGrantedTagCount, Is.EqualTo(0));
            Assert.That(slot.ActiveGrantedAbilityCount, Is.EqualTo(0));
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

            EffectRuntimeUtility.CleanupActiveEffect(_em, effect, currentFrame: 35);
            Assert.That(_em.GetBuffer<BActiveEffectSlot>(target).Length, Is.EqualTo(0));
            Assert.That(_em.GetComponentData<CActiveEffectStore>(target).LastCompactedFrame, Is.EqualTo(35));
            Assert.That(_em.Exists(effect), Is.False);
        }

        [Test]
        public void DurationLifecycleMirrorsIntoGlobalIndexedStore()
        {
            var indexOwner = ActiveEffectStore.EnsureGlobalIndexStore(_em);
            var source = CreateEntity();
            var target = AbilitySystemEntityFactory.Create(_em);
            _entities.Add(target);
            var effect = CreateDurationEffect(source, target, effectCode: 94117);
            AddGlobalIndexStableRow(effect);
            var context = _em.GetComponentData<CEffectContext>(effect);
            var duration = _em.GetComponentData<CDurationRuntime>(effect);

            EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
            _em.SetComponentData(effect, duration);

            var index = GetSingleGlobalIndex(indexOwner);
            Assert.That(index.ActiveEffectEntity, Is.EqualTo(effect));
            Assert.That(index.OwnerAsc, Is.EqualTo(target));
            Assert.That(index.GameplayEffectCode, Is.EqualTo(94117));
            Assert.That(index.State, Is.EqualTo(EActiveEffectSlotState.Active));
            Assert.That(index.StackCount, Is.EqualTo(2));
            Assert.That(index.PeriodFrame, Is.EqualTo(5));
            Assert.That(index.LastPeriodFrame, Is.EqualTo(10));
            Assert.That(index.PeriodDueFrame, Is.EqualTo(15));
            Assert.That(index.DurationDueFrame, Is.EqualTo(110));
            Assert.That(HasFlag(index.IndexFlags, EActiveEffectGlobalIndexFlags.LegacyEntityBacked), Is.True);
            Assert.That(HasFlag(index.IndexFlags, EActiveEffectGlobalIndexFlags.StableRowBacked), Is.True);
            AssertStableRowMatchesIndex(effect, indexOwner, index, currentFrame: 10);
            var globalStore = _em.GetComponentData<CActiveEffectGlobalIndexStore>(indexOwner);
            Assert.That(globalStore.ActiveCount, Is.EqualTo(1));
            Assert.That(globalStore.IndexedStableRowCount, Is.EqualTo(1));
            Assert.That(globalStore.StaleStableRowCount, Is.EqualTo(0));

            EffectRuntimeUtility.DeactivateOngoingEffect(_em, effect, currentFrame: 15);
            index = GetSingleGlobalIndex(indexOwner);
            Assert.That(index.State, Is.EqualTo(EActiveEffectSlotState.Inhibited));
            Assert.That(HasFlag(index.IndexFlags, EActiveEffectGlobalIndexFlags.Inhibited), Is.True);
            Assert.That(HasFlag(index.IndexFlags, EActiveEffectGlobalIndexFlags.StableRowBacked), Is.True);
            AssertStableRowMatchesIndex(effect, indexOwner, index, currentFrame: 15);
            Assert.That(_em.GetComponentData<CActiveEffectGlobalIndexStore>(indexOwner).InhibitedCount, Is.EqualTo(1));

            EffectRuntimeUtility.MarkEffectForRemoval(_em, effect, currentFrame: 30);
            index = GetSingleGlobalIndex(indexOwner);
            Assert.That(index.State, Is.EqualTo(EActiveEffectSlotState.PendingRemove));
            Assert.That(HasFlag(index.IndexFlags, EActiveEffectGlobalIndexFlags.PendingRemove), Is.True);
            Assert.That(HasFlag(index.IndexFlags, EActiveEffectGlobalIndexFlags.StableRowBacked), Is.True);
            AssertStableRowMatchesIndex(effect, indexOwner, index, currentFrame: 30);
            Assert.That(_em.GetComponentData<CActiveEffectGlobalIndexStore>(indexOwner).PendingRemoveCount, Is.EqualTo(1));

            EffectRuntimeUtility.CleanupActiveEffect(_em, effect, currentFrame: 35);
            Assert.That(_em.GetBuffer<BGlobalActiveEffectIndex>(indexOwner).Length, Is.EqualTo(0));
            var store = _em.GetComponentData<CActiveEffectGlobalIndexStore>(indexOwner);
            Assert.That(store.IndexedEffectCount, Is.EqualTo(0));
            Assert.That(store.IndexedStableRowCount, Is.EqualTo(0));
            Assert.That(store.LastCompactedFrame, Is.EqualTo(35));
        }

        [Test]
        public void GlobalIndexedStoreRemoveMarksStableRowBeforeEntityDestroy()
        {
            var indexOwner = ActiveEffectStore.EnsureGlobalIndexStore(_em);
            var source = CreateEntity();
            var target = AbilitySystemEntityFactory.Create(_em);
            _entities.Add(target);
            var effect = CreateDurationEffect(source, target, effectCode: 94122);
            AddGlobalIndexStableRow(effect);
            var context = _em.GetComponentData<CEffectContext>(effect);
            var duration = _em.GetComponentData<CDurationRuntime>(effect);

            EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
            _em.SetComponentData(effect, duration);
            Assert.That(GetSingleGlobalIndex(indexOwner).ActiveEffectEntity, Is.EqualTo(effect));

            Assert.That(ActiveEffectStore.TryRemoveGlobalIndex(_em, effect, currentFrame: 35), Is.True);

            Assert.That(_em.Exists(effect), Is.True);
            Assert.That(_em.GetBuffer<BGlobalActiveEffectIndex>(indexOwner).Length, Is.EqualTo(0));
            var row = _em.GetComponentData<CActiveEffectGlobalIndexStableRow>(effect);
            Assert.That(row.Version, Is.EqualTo(ActiveEffectStore.CurrentVersion));
            Assert.That(row.IsIndexed, Is.EqualTo(0));
            Assert.That(row.LastCompactedFrame, Is.EqualTo(35));
            Assert.That(HasFlag(row.IndexFlags, EActiveEffectGlobalIndexFlags.StableRowBacked), Is.False);
            var store = _em.GetComponentData<CActiveEffectGlobalIndexStore>(indexOwner);
            Assert.That(store.IndexedEffectCount, Is.EqualTo(0));
            Assert.That(store.IndexedStableRowCount, Is.EqualTo(0));
            Assert.That(store.LastCompactedFrame, Is.EqualTo(35));
        }

        [Test]
        public void GlobalIndexedStoreOwnerRefreshUpdatesPeriodAndDurationDueCounters()
        {
            var indexOwner = ActiveEffectStore.EnsureGlobalIndexStore(_em);
            var source = CreateEntity();
            var target = CreateOwnerLocalStoreTarget();
            var periodEffect = CreateDurationEffect(source, target, effectCode: 94118);
            var durationEffect = CreateDurationEffect(source, target, effectCode: 94119);
            var slots = _em.GetBuffer<BActiveEffectSlot>(target);
            slots.Add(new BActiveEffectSlot
            {
                Sequence = 1,
                ActiveEffectEntity = periodEffect,
                SourceAsc = source,
                TargetAsc = target,
                GameplayEffectCode = 94118,
                State = EActiveEffectSlotState.Active,
                StartFrame = 0,
                DurationFrame = 100,
                RemainingFrame = 100,
                PeriodFrame = 5,
                LastPeriodFrame = 10,
                Flags = (int)(EActiveEffectSlotFlags.HasDuration | EActiveEffectSlotFlags.HasPeriod),
            });
            slots.Add(new BActiveEffectSlot
            {
                Sequence = 2,
                ActiveEffectEntity = durationEffect,
                SourceAsc = source,
                TargetAsc = target,
                GameplayEffectCode = 94119,
                State = EActiveEffectSlotState.Active,
                StartFrame = 0,
                DurationFrame = 10,
                RemainingFrame = 10,
                Flags = (int)EActiveEffectSlotFlags.HasDuration,
            });

            Assert.That(ActiveEffectStore.TryRefreshChunkSkipIndex(_em, target, currentFrame: 15), Is.True);

            var globalStore = _em.GetComponentData<CActiveEffectGlobalIndexStore>(indexOwner);
            Assert.That(globalStore.IndexedEffectCount, Is.EqualTo(2));
            Assert.That(globalStore.ActiveCount, Is.EqualTo(2));
            Assert.That(globalStore.PeriodDueCount, Is.EqualTo(1));
            Assert.That(globalStore.DurationDueCount, Is.EqualTo(1));
            AssertGlobalIndexBucketOwners(_em, indexOwner);
            AssertGlobalIndexEntryInResolvedBucket(_em, periodEffect, target);
            AssertGlobalIndexEntryInResolvedBucket(_em, durationEffect, target);
        }

        [Test]
        public void GlobalIndexedStoreBatchMergeSortsCandidatesAndUpdatesCounters()
        {
            var indexOwner = ActiveEffectStore.EnsureGlobalIndexStore(_em);
            var source = CreateEntity();
            var firstTarget = CreateOwnerLocalStoreTarget();
            var secondTarget = CreateOwnerLocalStoreTarget();
            var periodEffect = CreateDurationEffect(source, firstTarget, effectCode: 94120);
            var durationEffect = CreateDurationEffect(source, secondTarget, effectCode: 94121);
            var entries = new NativeList<BGlobalActiveEffectIndex>(Allocator.Temp);
            try
            {
                entries.Add(ActiveEffectStore.CreateGlobalIndexEntry(
                    new BActiveEffectSlot
                    {
                        Sequence = 2,
                        ActiveEffectEntity = durationEffect,
                        SourceAsc = source,
                        TargetAsc = secondTarget,
                        GameplayEffectCode = 94121,
                        State = EActiveEffectSlotState.Active,
                        StartFrame = 0,
                        DurationFrame = 10,
                        RemainingFrame = 10,
                        Flags = (int)EActiveEffectSlotFlags.HasDuration,
                    },
                    currentFrame: 15));
                entries.Add(ActiveEffectStore.CreateGlobalIndexEntry(
                    new BActiveEffectSlot
                    {
                        Sequence = 1,
                        ActiveEffectEntity = periodEffect,
                        SourceAsc = source,
                        TargetAsc = firstTarget,
                        GameplayEffectCode = 94120,
                        State = EActiveEffectSlotState.Active,
                        StartFrame = 0,
                        DurationFrame = 100,
                        RemainingFrame = 100,
                        PeriodFrame = 5,
                        LastPeriodFrame = 10,
                        Flags = (int)(EActiveEffectSlotFlags.HasDuration | EActiveEffectSlotFlags.HasPeriod),
                    },
                    currentFrame: 15));

                Assert.That(ActiveEffectStore.TryMergeGlobalIndexEntries(_em, entries, currentFrame: 15), Is.True);
            }
            finally
            {
                entries.Dispose();
            }

            var indices = _em.GetBuffer<BGlobalActiveEffectIndex>(indexOwner);
            Assert.That(indices.Length, Is.EqualTo(2));
            Assert.That(indices[0].OwnerAsc, Is.EqualTo(firstTarget));
            Assert.That(indices[0].ActiveEffectEntity, Is.EqualTo(periodEffect));
            Assert.That(indices[1].OwnerAsc, Is.EqualTo(secondTarget));
            Assert.That(indices[1].ActiveEffectEntity, Is.EqualTo(durationEffect));

            var globalStore = _em.GetComponentData<CActiveEffectGlobalIndexStore>(indexOwner);
            Assert.That(globalStore.IndexedEffectCount, Is.EqualTo(2));
            Assert.That(globalStore.ActiveCount, Is.EqualTo(2));
            Assert.That(globalStore.PeriodDueCount, Is.EqualTo(1));
            Assert.That(globalStore.DurationDueCount, Is.EqualTo(1));
            AssertGlobalIndexBucketOwners(_em, indexOwner);
            AssertGlobalIndexEntryInResolvedBucket(_em, periodEffect, firstTarget);
            AssertGlobalIndexEntryInResolvedBucket(_em, durationEffect, secondTarget);
        }

        [Test]
        public void CleanupActiveEffectRecordsLifecycleCleanupStoreBeforeDestroy()
        {
            var source = CreateEntity();
            var target = AbilitySystemEntityFactory.Create(_em);
            _entities.Add(target);
            var effect = CreateDurationEffect(source, target, effectCode: 94110);
            var context = _em.GetComponentData<CEffectContext>(effect);
            var duration = _em.GetComponentData<CDurationRuntime>(effect);

            EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
            _em.SetComponentData(effect, duration);
            Assert.That(GetSingleSlot(target).State, Is.EqualTo(EActiveEffectSlotState.Active));

            EffectRuntimeUtility.MarkEffectForRemoval(_em, effect, currentFrame: 20);
            EffectRuntimeUtility.CleanupActiveEffect(_em, effect, currentFrame: 25);

            Assert.That(_em.Exists(effect), Is.False);
            Assert.That(_em.GetBuffer<BActiveEffectSlot>(target).Length, Is.EqualTo(0));

            var store = _em.GetComponentData<CActiveEffectStore>(target);
            Assert.That(store.LastCompactedFrame, Is.EqualTo(25));
            Assert.That(store.LastCleanupFrame, Is.EqualTo(25));
            Assert.That(store.CleanupRecordCount, Is.EqualTo(1));

            var records = _em.GetBuffer<BActiveEffectCleanupRecord>(target);
            Assert.That(records.Length, Is.EqualTo(1));
            var record = records[0];
            Assert.That(record.ActiveEffectEntity, Is.EqualTo(effect));
            Assert.That(record.SourceAsc, Is.EqualTo(source));
            Assert.That(record.TargetAsc, Is.EqualTo(target));
            Assert.That(record.GameplayEffectCode, Is.EqualTo(94110));
            Assert.That(record.Level, Is.EqualTo(3));
            Assert.That(record.StackCount, Is.EqualTo(2));
            Assert.That(record.ContextId, Is.EqualTo(94110));
            Assert.That(record.CleanupFrame, Is.EqualTo(25));
            Assert.That(record.CleanupState, Is.EqualTo(EGameplayEffectLifecycleState.Active));
            Assert.That(record.SlotState, Is.EqualTo(EActiveEffectSlotState.PendingRemove));
            Assert.That(record.PreviousSlotState, Is.EqualTo(EActiveEffectSlotState.Active));
            Assert.That(record.DurationFrame, Is.EqualTo(100));
            Assert.That(record.RemainingFrame, Is.EqualTo(100));
            Assert.That(record.PeriodFrame, Is.EqualTo(5));
            Assert.That(record.LastPeriodFrame, Is.EqualTo(10));
            Assert.That(record.ActiveGrantedTagCount, Is.EqualTo(0));
            Assert.That(record.ActiveGrantedAbilityCount, Is.EqualTo(0));
            Assert.That(HasFlag(record.Flags, EActiveEffectSlotFlags.LegacyEntityBacked), Is.True);
            Assert.That(HasFlag(record.Flags, EActiveEffectSlotFlags.HasDuration), Is.True);
            Assert.That(HasFlag(record.Flags, EActiveEffectSlotFlags.HasPeriod), Is.True);
            Assert.That(HasFlag(record.Flags, EActiveEffectSlotFlags.HasStacking), Is.True);
        }

        [Test]
        public void CleanupActiveEffectRecordsRequestedAndResolvedCleanupWork()
        {
            const int effectCode = 94111;
            const int tagIndex = 12;

            var source = CreateEntity();
            var target = AbilitySystemEntityFactory.Create(_em);
            _entities.Add(target);
            var effect = CreateDurationEffect(source, target, effectCode);
            var grantedTags = CreateMask(tagIndex);
            _em.AddComponentData(effect, new CEffectGrantedTags { Tags = grantedTags });
            _em.AddBuffer<BResolvedModifier>(effect).Add(new BResolvedModifier
            {
                AttrSetCode = 31,
                AttributeCode = 41,
                Op = EModifierOp.Add,
                Magnitude = 7,
                SourceEffect = effect,
            });

            var ability = CreateGrantedAbility(
                target,
                effect,
                abilityCode: 95111,
                active: false,
                deactivationPolicy: GrantedAbilityDeactivationPolicy.SyncWithEffect,
                removePolicy: GrantedAbilityRemovePolicy.SyncWithEffect);
            _em.AddBuffer<BGrantedAbilityRuntime>(effect).Add(new BGrantedAbilityRuntime
            {
                ConfigIndex = 0,
                AbilityEntity = ability,
            });
            _em.GetBuffer<BGrantedAbility>(target).Add(new BGrantedAbility
            {
                AbilityEntity = ability,
            });

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode
                    ? new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                    {
                        new GrantedTagDefinitionConfig { Tags = grantedTags },
                    })
                    : null);

            var context = _em.GetComponentData<CEffectContext>(effect);
            var duration = _em.GetComponentData<CDurationRuntime>(effect);
            EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
            _em.SetComponentData(effect, duration);

            Assert.That(GetSingleSlot(target).ActiveGrantedTagCount, Is.EqualTo(1));
            Assert.That(GetSingleSlot(target).ActiveGrantedAbilityCount, Is.EqualTo(1));
            Assert.That(_em.GetBuffer<BActiveModifier>(target).Length, Is.EqualTo(1));
            Assert.That(_em.GetComponentData<CTagMask>(target).HasTag(tagIndex), Is.True);
            Assert.That(_em.GetBuffer<BGameplayEffect>(target).Length, Is.EqualTo(1));
            Assert.That(_em.GetBuffer<BGrantedAbility>(target).Length, Is.EqualTo(1));

            EffectRuntimeUtility.MarkEffectForRemoval(_em, effect, currentFrame: 20);
            EffectRuntimeUtility.CleanupActiveEffect(_em, effect, currentFrame: 25);

            Assert.That(_em.Exists(effect), Is.False);
            Assert.That(_em.Exists(ability), Is.False);
            Assert.That(_em.GetBuffer<BActiveModifier>(target).Length, Is.EqualTo(0));
            Assert.That(_em.GetComponentData<CTagMask>(target).HasTag(tagIndex), Is.False);
            Assert.That(_em.GetBuffer<BGameplayEffect>(target).Length, Is.EqualTo(0));
            Assert.That(_em.GetBuffer<BGrantedAbility>(target).Length, Is.EqualTo(0));

            var records = _em.GetBuffer<BActiveEffectCleanupRecord>(target);
            Assert.That(records.Length, Is.EqualTo(1));
            var record = records[0];
            Assert.That(record.ActiveGrantedTagCount, Is.EqualTo(1));
            Assert.That(record.ActiveGrantedAbilityCount, Is.EqualTo(1));
            Assert.That(record.ActiveModifierCount, Is.EqualTo(1));
            Assert.That(record.CleanupResolvedFrame, Is.EqualTo(25));
            Assert.That(record.ResolvedCleanupWorkFlags, Is.EqualTo(record.RequestedCleanupWorkFlags));
            Assert.That(HasFlag(record.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.OwnerLocalSlot), Is.True);
            Assert.That(HasFlag(record.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.LegacyTargetBuffer), Is.True);
            Assert.That(HasFlag(record.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.RuntimeModifiers), Is.True);
            Assert.That(HasFlag(record.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.GrantedTags), Is.True);
            Assert.That(HasFlag(record.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.GrantedAbilities), Is.True);
            Assert.That(HasFlag(record.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.DurationRuntime), Is.True);
            Assert.That(HasFlag(record.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.EntityDestroy), Is.True);
        }

        [Test]
        public void CleanupActiveEffectEcbPathDefersEntityDestroyUntilFinalDestroySystem()
        {
            var source = CreateEntity();
            var target = AbilitySystemEntityFactory.Create(_em);
            _entities.Add(target);
            var effect = CreateDurationEffect(source, target, effectCode: 94115);
            var context = _em.GetComponentData<CEffectContext>(effect);
            var duration = _em.GetComponentData<CDurationRuntime>(effect);

            EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
            _em.SetComponentData(effect, duration);
            EffectRuntimeUtility.MarkEffectForRemoval(_em, effect, currentFrame: 20);

            var cleanupEcb = new EntityCommandBuffer(Allocator.Temp);
            EffectRuntimeUtility.CleanupActiveEffect(_em, ref cleanupEcb, effect, currentFrame: 25);
            cleanupEcb.Playback(_em);
            cleanupEcb.Dispose();

            Assert.That(_em.Exists(effect), Is.True);
            Assert.That(_em.HasComponent<CEffectFinalDestroy>(effect), Is.True);
            Assert.That(_em.GetBuffer<BActiveEffectSlot>(target).Length, Is.EqualTo(0));

            var records = _em.GetBuffer<BActiveEffectCleanupRecord>(target);
            Assert.That(records.Length, Is.EqualTo(1));
            var record = records[0];
            Assert.That(record.ActiveEffectEntity, Is.EqualTo(effect));
            Assert.That(record.CleanupFrame, Is.EqualTo(25));
            Assert.That(record.CleanupResolvedFrame, Is.EqualTo(25));
            Assert.That(HasFlag(record.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.EntityDestroy), Is.True);
            Assert.That(HasFlag(record.ResolvedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.EntityDestroy), Is.False);

            var finalDestroy = _em.GetComponentData<CEffectFinalDestroy>(effect);
            Assert.That(finalDestroy.RequestedFrame, Is.EqualTo(25));
            Assert.That(finalDestroy.CleanupSequence, Is.EqualTo(record.Sequence));
            Assert.That(finalDestroy.TargetAsc, Is.EqualTo(target));

            var destroyEcb = new EntityCommandBuffer(Allocator.Temp);
            EffectRuntimeUtility.FinalizeEffectDestroy(_em, ref destroyEcb, effect, currentFrame: 25);
            destroyEcb.Playback(_em);
            destroyEcb.Dispose();

            Assert.That(_em.Exists(effect), Is.False);
            records = _em.GetBuffer<BActiveEffectCleanupRecord>(target);
            record = records[0];
            Assert.That(record.ResolvedCleanupWorkFlags, Is.EqualTo(record.RequestedCleanupWorkFlags));
            Assert.That(record.CleanupResolvedFrame, Is.EqualTo(25));
        }

        [Test]
        public void SEffectFinalDestroyConsumesDeferredFinalDestroyCandidates()
        {
            var previousTimer = SetFrame(25);
            try
            {
                var source = CreateEntity();
                var target = AbilitySystemEntityFactory.Create(_em);
                _entities.Add(target);
                var effect = CreateDurationEffect(source, target, effectCode: 94123);
                var context = _em.GetComponentData<CEffectContext>(effect);
                var duration = _em.GetComponentData<CDurationRuntime>(effect);

                EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
                _em.SetComponentData(effect, duration);
                EffectRuntimeUtility.MarkEffectForRemoval(_em, effect, currentFrame: 20);

                var cleanupEcb = new EntityCommandBuffer(Allocator.Temp);
                EffectRuntimeUtility.CleanupActiveEffect(_em, ref cleanupEcb, effect, currentFrame: 25);
                cleanupEcb.Playback(_em);
                cleanupEcb.Dispose();

                Assert.That(_em.Exists(effect), Is.True);
                Assert.That(_em.HasComponent<CEffectFinalDestroy>(effect), Is.True);

                RunEffectGroup();

                Assert.That(_em.Exists(effect), Is.False);
                var records = _em.GetBuffer<BActiveEffectCleanupRecord>(target);
                Assert.That(records.Length, Is.EqualTo(1));
                var record = records[0];
                Assert.That(record.ActiveEffectEntity, Is.EqualTo(effect));
                Assert.That(record.ResolvedCleanupWorkFlags, Is.EqualTo(record.RequestedCleanupWorkFlags));
                Assert.That(record.CleanupResolvedFrame, Is.EqualTo(25));
            }
            finally
            {
                RestoreFrame(previousTimer);
            }
        }

        [Test]
        public void GrantedAbilitySelfCleanupRefreshesOwnerActiveEffectSlotCount()
        {
            var source = CreateEntity();
            var target = AbilitySystemEntityFactory.Create(_em);
            _entities.Add(target);
            var effect = CreateDurationEffect(source, target, effectCode: 94103);
            var ability = CreateGrantedAbility(target, effect, abilityCode: 95103);
            var runtimeAbilities = _em.AddBuffer<BGrantedAbilityRuntime>(effect);
            runtimeAbilities.Add(new BGrantedAbilityRuntime
            {
                ConfigIndex = 0,
                AbilityEntity = ability,
            });
            _em.GetBuffer<BGrantedAbility>(target).Add(new BGrantedAbility
            {
                AbilityEntity = ability,
            });

            var context = _em.GetComponentData<CEffectContext>(effect);
            var duration = _em.GetComponentData<CDurationRuntime>(effect);
            Assert.That(
                ActiveEffectStore.TryUpsertDurationEffect(
                    _em,
                    effect,
                    context,
                    duration,
                    EActiveEffectSlotState.Active,
                    currentFrame: 10),
                Is.True);
            Assert.That(GetSingleSlot(target).ActiveGrantedAbilityCount, Is.EqualTo(1));

            _em.AddComponentData(ability, new CAbilityInTryEnd
            {
                Reason = EAbilityLifecycleReason.ExplicitEnd,
                SourceEffect = effect,
                SourceAbility = ability,
                SourceAbilityCode = 95103,
            });

            RunAbilityGroup();

            Assert.That(_em.Exists(ability), Is.False);
            Assert.That(_em.GetBuffer<BGrantedAbility>(target).Length, Is.EqualTo(0));
            Assert.That(_em.GetBuffer<BGrantedAbilityRuntime>(effect)[0].AbilityEntity, Is.EqualTo(Entity.Null));

            var slot = GetSingleSlot(target);
            Assert.That(slot.ActiveEffectEntity, Is.EqualTo(effect));
            Assert.That(slot.ActiveGrantedAbilityCount, Is.EqualTo(0));
        }

        [Test]
        public void GrantedTagLifecycleMirrorsIntoOwnerActiveEffectSlot()
        {
            const int effectCode = 94104;
            const int tagIndex = 11;

            var source = CreateEntity();
            var target = AbilitySystemEntityFactory.Create(_em);
            _entities.Add(target);
            var effect = CreateDurationEffect(source, target, effectCode);
            var grantedTags = CreateMask(tagIndex);
            _em.AddComponentData(effect, new CEffectGrantedTags { Tags = grantedTags });
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode
                    ? new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                    {
                        new GrantedTagDefinitionConfig { Tags = grantedTags },
                    })
                    : null);

            var context = _em.GetComponentData<CEffectContext>(effect);
            var duration = _em.GetComponentData<CDurationRuntime>(effect);
            EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
            _em.SetComponentData(effect, duration);

            var slot = GetSingleSlot(target);
            Assert.That(slot.ActiveGrantedTagCount, Is.EqualTo(1));
            Assert.That(slot.ActiveGrantedAbilityCount, Is.EqualTo(0));
            Assert.That(HasFlag(slot, EActiveEffectSlotFlags.HasGrantedTags), Is.True);
            Assert.That(_em.GetComponentData<CTagMask>(target).HasTag(tagIndex), Is.True);
            Assert.That(HasTempTagSource(target, effect, tagIndex), Is.True);

            EffectRuntimeUtility.DeactivateOngoingEffect(_em, effect, currentFrame: 20);

            slot = GetSingleSlot(target);
            Assert.That(slot.State, Is.EqualTo(EActiveEffectSlotState.Inhibited));
            Assert.That(slot.ActiveGrantedTagCount, Is.EqualTo(0));
            Assert.That(HasFlag(slot, EActiveEffectSlotFlags.HasGrantedTags), Is.True);
            Assert.That(_em.GetComponentData<CTagMask>(target).HasTag(tagIndex), Is.False);
            Assert.That(HasTempTagSource(target, effect, tagIndex), Is.False);

            EffectRuntimeUtility.ReactivateOngoingEffect(_em, effect, currentFrame: 30);

            slot = GetSingleSlot(target);
            Assert.That(slot.State, Is.EqualTo(EActiveEffectSlotState.Active));
            Assert.That(slot.ActiveGrantedTagCount, Is.EqualTo(1));
            Assert.That(HasFlag(slot, EActiveEffectSlotFlags.HasGrantedTags), Is.True);
            Assert.That(_em.GetComponentData<CTagMask>(target).HasTag(tagIndex), Is.True);
            Assert.That(HasTempTagSource(target, effect, tagIndex), Is.True);
        }

        [Test]
        public void RemoveGameplayEffectRequestMirrorsPendingRemoveIntoOwnerActiveEffectSlot()
        {
            var previousTimer = SetFrame(39);
            try
            {
                var source = CreateEntity();
                var target = AbilitySystemEntityFactory.Create(_em);
                _entities.Add(target);
                var effect = CreateDurationEffect(source, target, effectCode: 94105);
                var context = _em.GetComponentData<CEffectContext>(effect);
                var duration = _em.GetComponentData<CDurationRuntime>(effect);

                EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
                _em.SetComponentData(effect, duration);
                Assert.That(GetSingleSlot(target).State, Is.EqualTo(EActiveEffectSlotState.Active));

                var request = CreateEntity();
                _em.AddComponentData(request, new CRemoveGameplayEffectRequest
                {
                    GameplayEffect = effect,
                });

                RunCommandGroup();

                Assert.That(_em.Exists(request), Is.False);
                Assert.That(_em.HasComponent<CEffectDestroy>(effect), Is.True);
                var slot = GetSingleSlot(target);
                Assert.That(slot.State, Is.EqualTo(EActiveEffectSlotState.PendingRemove));
                Assert.That(slot.PreviousState, Is.EqualTo(EActiveEffectSlotState.Active));
                Assert.That(slot.StateStartFrame, Is.EqualTo(40));

                EffectRuntimeUtility.CleanupActiveEffect(_em, effect, currentFrame: 45);
                Assert.That(_em.GetBuffer<BActiveEffectSlot>(target).Length, Is.EqualTo(0));
                Assert.That(_em.GetComponentData<CActiveEffectStore>(target).LastCompactedFrame, Is.EqualTo(45));
                Assert.That(_em.Exists(effect), Is.False);
            }
            finally
            {
                RestoreFrame(previousTimer);
            }
        }

        [Test]
        public void AbilityCleanupMirrorsCreatedEffectPendingRemoveIntoOwnerActiveEffectSlot()
        {
            var previousTimer = SetFrame(50);
            try
            {
                var source = CreateEntity();
                var target = AbilitySystemEntityFactory.Create(_em);
                _entities.Add(target);
                var ability = CreateActiveAbility(target, abilityCode: 95106);
                var effect = CreateDurationEffect(source, target, effectCode: 94106);
                _em.AddComponentData(effect, new CCreatedByAbility
                {
                    sourceAbility = ability,
                });
                _em.GetBuffer<BGameplayEffect>(target).Add(new BGameplayEffect
                {
                    GameplayEffect = effect,
                });

                var context = _em.GetComponentData<CEffectContext>(effect);
                var duration = _em.GetComponentData<CDurationRuntime>(effect);
                EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 20);
                _em.SetComponentData(effect, duration);
                Assert.That(GetSingleSlot(target).State, Is.EqualTo(EActiveEffectSlotState.Active));

                _em.AddComponentData(ability, new CAbilityInTryEnd
                {
                    Reason = EAbilityLifecycleReason.ExplicitEnd,
                    SourceAbility = ability,
                    SourceAbilityCode = 95106,
                });

                RunAbilityGroup();

                Assert.That(_em.HasComponent<CEffectDestroy>(effect), Is.True);
                var slot = GetSingleSlot(target);
                Assert.That(slot.State, Is.EqualTo(EActiveEffectSlotState.PendingRemove));
                Assert.That(slot.PreviousState, Is.EqualTo(EActiveEffectSlotState.Active));
                Assert.That(slot.StateStartFrame, Is.EqualTo(50));

                EffectRuntimeUtility.CleanupActiveEffect(_em, effect, currentFrame: 55);
                Assert.That(_em.GetBuffer<BActiveEffectSlot>(target).Length, Is.EqualTo(0));
                Assert.That(_em.GetComponentData<CActiveEffectStore>(target).LastCompactedFrame, Is.EqualTo(55));
                Assert.That(_em.Exists(effect), Is.False);
            }
            finally
            {
                RestoreFrame(previousTimer);
            }
        }

        [Test]
        public void CleanupActiveEffectCompactsOwnerStoreWithoutLegacyGameplayEffectBuffer()
        {
            var source = CreateEntity();
            var target = CreateOwnerLocalStoreTarget();
            var effect = CreateDurationEffect(source, target, effectCode: 94107);
            var context = _em.GetComponentData<CEffectContext>(effect);
            var duration = _em.GetComponentData<CDurationRuntime>(effect);

            Assert.That(_em.HasBuffer<BGameplayEffect>(target), Is.False);

            EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
            _em.SetComponentData(effect, duration);

            Assert.That(_em.HasBuffer<BGameplayEffect>(target), Is.False);
            Assert.That(GetSingleSlot(target).State, Is.EqualTo(EActiveEffectSlotState.Active));

            EffectRuntimeUtility.MarkEffectForRemoval(_em, effect, currentFrame: 20);
            Assert.That(GetSingleSlot(target).State, Is.EqualTo(EActiveEffectSlotState.PendingRemove));

            EffectRuntimeUtility.CleanupActiveEffect(_em, effect, currentFrame: 25);

            Assert.That(_em.GetBuffer<BActiveEffectSlot>(target).Length, Is.EqualTo(0));
            Assert.That(_em.GetComponentData<CActiveEffectStore>(target).LastCompactedFrame, Is.EqualTo(25));
            Assert.That(_em.Exists(effect), Is.False);
        }

        [Test]
        public void MarkEffectForRemovalWritesCleanupShellBeforeLegacyDestroyMarker()
        {
            var source = CreateEntity();
            var target = AbilitySystemEntityFactory.Create(_em);
            _entities.Add(target);
            var effect = CreateDurationEffect(source, target, effectCode: 94113);
            var context = _em.GetComponentData<CEffectContext>(effect);
            var duration = _em.GetComponentData<CDurationRuntime>(effect);

            EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
            _em.SetComponentData(effect, duration);

            EffectRuntimeUtility.MarkEffectForRemoval(_em, effect, currentFrame: 20);

            Assert.That(_em.HasComponent<CEffectCleanup>(effect), Is.True);
            Assert.That(_em.HasComponent<CEffectDestroy>(effect), Is.True);
            var cleanup = _em.GetComponentData<CEffectCleanup>(effect);
            Assert.That(cleanup.RequestedFrame, Is.EqualTo(20));
            Assert.That(cleanup.CleanupState, Is.EqualTo(EGameplayEffectLifecycleState.Active));
            Assert.That(cleanup.SourceAsc, Is.EqualTo(source));
            Assert.That(cleanup.TargetAsc, Is.EqualTo(target));
            Assert.That(HasFlag(cleanup.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.OwnerLocalSlot), Is.True);
            Assert.That(HasFlag(cleanup.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.LegacyTargetBuffer), Is.True);
            Assert.That(HasFlag(cleanup.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.DurationRuntime), Is.True);
            Assert.That(HasFlag(cleanup.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.EntityDestroy), Is.True);

            var slot = GetSingleSlot(target);
            Assert.That(slot.State, Is.EqualTo(EActiveEffectSlotState.PendingRemove));
            Assert.That(slot.PreviousState, Is.EqualTo(EActiveEffectSlotState.Active));
        }

        [Test]
        public void CleanupActiveEffectConsumesCleanupShellExecutionPlan()
        {
            var source = CreateEntity();
            var target = AbilitySystemEntityFactory.Create(_em);
            _entities.Add(target);
            var effect = CreateDurationEffect(source, target, effectCode: 94124);
            var context = _em.GetComponentData<CEffectContext>(effect);
            var duration = _em.GetComponentData<CDurationRuntime>(effect);

            EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
            _em.SetComponentData(effect, duration);
            EffectRuntimeUtility.MarkEffectForRemoval(_em, effect, currentFrame: 20);

            var shellPlan = _em.GetComponentData<CEffectCleanup>(effect).RequestedCleanupWorkFlags;
            Assert.That(HasFlag(shellPlan, EActiveEffectCleanupWorkFlags.LegacyTargetBuffer), Is.True);

            _em.GetBuffer<BGameplayEffect>(target).Clear();

            EffectRuntimeUtility.CleanupActiveEffect(_em, effect, currentFrame: 25);

            Assert.That(_em.Exists(effect), Is.False);
            var records = _em.GetBuffer<BActiveEffectCleanupRecord>(target);
            Assert.That(records.Length, Is.EqualTo(1));
            var record = records[0];
            Assert.That(record.RequestedCleanupWorkFlags, Is.EqualTo(shellPlan));
            Assert.That(HasFlag(record.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.LegacyTargetBuffer), Is.True);
            Assert.That(record.ResolvedCleanupWorkFlags, Is.EqualTo(record.RequestedCleanupWorkFlags));
        }

        [Test]
        public void SEffectRemoveCleansPendingRemoveOwnerLocalSlotWithoutEffectDestroyMarker()
        {
            var previousTimer = SetFrame(24);
            try
            {
                var source = CreateEntity();
                var target = AbilitySystemEntityFactory.Create(_em);
                _entities.Add(target);
                var effect = CreateDurationEffect(source, target, effectCode: 94112);
                var context = _em.GetComponentData<CEffectContext>(effect);
                var duration = _em.GetComponentData<CDurationRuntime>(effect);

                EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
                _em.SetComponentData(effect, duration);
                Assert.That(GetSingleSlot(target).State, Is.EqualTo(EActiveEffectSlotState.Active));

                Assert.That(
                    ActiveEffectStore.TryUpsertDurationEffect(
                        _em,
                        effect,
                        context,
                        duration,
                        EActiveEffectSlotState.PendingRemove,
                        currentFrame: 20),
                    Is.True);
                Assert.That(_em.HasComponent<CEffectDestroy>(effect), Is.False);
                Assert.That(GetSingleSlot(target).State, Is.EqualTo(EActiveEffectSlotState.PendingRemove));

                RunEffectGroup();

                Assert.That(_em.Exists(effect), Is.False);
                Assert.That(_em.GetBuffer<BActiveEffectSlot>(target).Length, Is.EqualTo(0));

                var store = _em.GetComponentData<CActiveEffectStore>(target);
                Assert.That(store.LastCompactedFrame, Is.EqualTo(24));
                Assert.That(store.LastCleanupFrame, Is.EqualTo(24));
                Assert.That(store.CleanupRecordCount, Is.EqualTo(1));

                var records = _em.GetBuffer<BActiveEffectCleanupRecord>(target);
                Assert.That(records.Length, Is.EqualTo(1));
                var record = records[0];
                Assert.That(record.ActiveEffectEntity, Is.EqualTo(effect));
                Assert.That(record.CleanupFrame, Is.EqualTo(24));
                Assert.That(record.CleanupState, Is.EqualTo(EGameplayEffectLifecycleState.Active));
                Assert.That(record.SlotState, Is.EqualTo(EActiveEffectSlotState.PendingRemove));
                Assert.That(record.PreviousSlotState, Is.EqualTo(EActiveEffectSlotState.Active));
                Assert.That(record.CleanupResolvedFrame, Is.EqualTo(24));
                Assert.That(record.ResolvedCleanupWorkFlags, Is.EqualTo(record.RequestedCleanupWorkFlags));
                Assert.That(HasFlag(record.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.OwnerLocalSlot), Is.True);
                Assert.That(HasFlag(record.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.EntityDestroy), Is.True);
            }
            finally
            {
                RestoreFrame(previousTimer);
            }
        }

        [Test]
        public void SEffectRemoveCleansCleanupShellWithoutEffectDestroyMarker()
        {
            var previousTimer = SetFrame(26);
            try
            {
                var source = CreateEntity();
                var target = AbilitySystemEntityFactory.Create(_em);
                _entities.Add(target);
                var effect = CreateDurationEffect(source, target, effectCode: 94114);
                var context = _em.GetComponentData<CEffectContext>(effect);
                var duration = _em.GetComponentData<CDurationRuntime>(effect);

                EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
                _em.SetComponentData(effect, duration);
                Assert.That(GetSingleSlot(target).State, Is.EqualTo(EActiveEffectSlotState.Active));

                _em.AddComponentData(effect, new CEffectCleanup
                {
                    RequestedFrame = 20,
                    CleanupState = EGameplayEffectLifecycleState.Active,
                    SourceAsc = source,
                    TargetAsc = target,
                });
                Assert.That(_em.HasComponent<CEffectDestroy>(effect), Is.False);

                RunEffectGroup();

                Assert.That(_em.Exists(effect), Is.False);
                Assert.That(_em.GetBuffer<BActiveEffectSlot>(target).Length, Is.EqualTo(0));

                var store = _em.GetComponentData<CActiveEffectStore>(target);
                Assert.That(store.LastCompactedFrame, Is.EqualTo(26));
                Assert.That(store.LastCleanupFrame, Is.EqualTo(26));
                Assert.That(store.CleanupRecordCount, Is.EqualTo(1));

                var records = _em.GetBuffer<BActiveEffectCleanupRecord>(target);
                Assert.That(records.Length, Is.EqualTo(1));
                var record = records[0];
                Assert.That(record.ActiveEffectEntity, Is.EqualTo(effect));
                Assert.That(record.CleanupFrame, Is.EqualTo(26));
                Assert.That(record.CleanupState, Is.EqualTo(EGameplayEffectLifecycleState.Active));
                Assert.That(record.SlotState, Is.EqualTo(EActiveEffectSlotState.Active));
                Assert.That(record.CleanupResolvedFrame, Is.EqualTo(26));
                Assert.That(record.ResolvedCleanupWorkFlags, Is.EqualTo(record.RequestedCleanupWorkFlags));
                Assert.That(HasFlag(record.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.OwnerLocalSlot), Is.True);
                Assert.That(HasFlag(record.RequestedCleanupWorkFlags, EActiveEffectCleanupWorkFlags.EntityDestroy), Is.True);
            }
            finally
            {
                RestoreFrame(previousTimer);
            }
        }

        [Test]
        public void SEffectRemoveDestroysOrphanCleanupCandidatesWithoutEffectContext()
        {
            var previousTimer = SetFrame(27);
            try
            {
                var cleanupOnlyEffect = CreateEntity();
                _em.AddComponentData(cleanupOnlyEffect, new CEffectCleanup
                {
                    RequestedFrame = 20,
                    CleanupState = EGameplayEffectLifecycleState.PendingApply,
                });

                var legacyDestroyEffect = CreateEntity();
                _em.AddComponent<CEffectDestroy>(legacyDestroyEffect);

                RunEffectGroup();

                Assert.That(_em.Exists(cleanupOnlyEffect), Is.False);
                Assert.That(_em.Exists(legacyDestroyEffect), Is.False);
            }
            finally
            {
                RestoreFrame(previousTimer);
            }
        }

        [Test]
        public void CleanupActiveEffectRemovesRuntimeGrantedAbilityWithoutStaticDefinitionBlob()
        {
            var source = CreateEntity();
            var target = AbilitySystemEntityFactory.Create(_em);
            _entities.Add(target);
            var effect = CreateDurationEffect(source, target, effectCode: 94108);
            var ability = CreateGrantedAbility(
                target,
                effect,
                abilityCode: 95108,
                active: false,
                deactivationPolicy: GrantedAbilityDeactivationPolicy.SyncWithEffect,
                removePolicy: GrantedAbilityRemovePolicy.SyncWithEffect);
            _em.AddBuffer<BGrantedAbilityRuntime>(effect).Add(new BGrantedAbilityRuntime
            {
                ConfigIndex = 0,
                AbilityEntity = ability,
            });
            _em.GetBuffer<BGrantedAbility>(target).Add(new BGrantedAbility
            {
                AbilityEntity = ability,
            });

            var context = _em.GetComponentData<CEffectContext>(effect);
            var duration = _em.GetComponentData<CDurationRuntime>(effect);
            EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
            _em.SetComponentData(effect, duration);
            Assert.That(EffectRuntimeUtility.TryGetStaticDefinitionBlob(_em, effect, out _), Is.False);
            Assert.That(ActiveEffectStore.TryRefreshGrantedAbilityState(_em, effect, context), Is.True);
            Assert.That(GetSingleSlot(target).ActiveGrantedAbilityCount, Is.EqualTo(1));

            EffectRuntimeUtility.MarkEffectForRemoval(_em, effect, currentFrame: 20);
            EffectRuntimeUtility.CleanupActiveEffect(_em, effect, currentFrame: 25);

            Assert.That(_em.Exists(effect), Is.False);
            Assert.That(_em.Exists(ability), Is.False);
            Assert.That(_em.GetBuffer<BGrantedAbility>(target).Length, Is.EqualTo(0));
            Assert.That(_em.GetBuffer<BActiveEffectSlot>(target).Length, Is.EqualTo(0));
            Assert.That(_em.GetComponentData<CActiveEffectStore>(target).LastCompactedFrame, Is.EqualTo(25));
        }

        [Test]
        public void CleanupActiveEffectCancelsAndDestroysActiveRuntimeGrantedAbility()
        {
            var source = CreateEntity();
            var target = AbilitySystemEntityFactory.Create(_em);
            _entities.Add(target);
            var effect = CreateDurationEffect(source, target, effectCode: 94109);
            var ability = CreateGrantedAbility(
                target,
                effect,
                abilityCode: 95109,
                active: true,
                deactivationPolicy: GrantedAbilityDeactivationPolicy.SyncWithEffect,
                removePolicy: GrantedAbilityRemovePolicy.SyncWithEffect);
            _em.AddBuffer<BGrantedAbilityRuntime>(effect).Add(new BGrantedAbilityRuntime
            {
                ConfigIndex = 0,
                AbilityEntity = ability,
            });
            _em.GetBuffer<BGrantedAbility>(target).Add(new BGrantedAbility
            {
                AbilityEntity = ability,
            });

            var context = _em.GetComponentData<CEffectContext>(effect);
            var duration = _em.GetComponentData<CDurationRuntime>(effect);
            EffectRuntimeUtility.ActivateDurationEffect(_em, effect, context, ref duration, currentFrame: 10);
            _em.SetComponentData(effect, duration);
            Assert.That(EffectRuntimeUtility.TryGetStaticDefinitionBlob(_em, effect, out _), Is.False);
            Assert.That(ActiveEffectStore.TryRefreshGrantedAbilityState(_em, effect, context), Is.True);
            Assert.That(GetSingleSlot(target).ActiveGrantedAbilityCount, Is.EqualTo(1));

            EffectRuntimeUtility.MarkEffectForRemoval(_em, effect, currentFrame: 20);
            EffectRuntimeUtility.CleanupActiveEffect(_em, effect, currentFrame: 25);

            Assert.That(_em.Exists(effect), Is.False);
            Assert.That(_em.Exists(ability), Is.True);
            Assert.That(_em.GetBuffer<BGrantedAbility>(target).Length, Is.EqualTo(0));
            Assert.That(_em.HasComponent<CAbilityInTryCancel>(ability), Is.True);
            Assert.That(_em.HasComponent<CAbilityDestroyOnCleanup>(ability), Is.True);
            var cancel = _em.GetComponentData<CAbilityInTryCancel>(ability);
            Assert.That(cancel.Reason, Is.EqualTo(EAbilityLifecycleReason.GrantedEffectRemoved));
            Assert.That(cancel.SourceEffect, Is.EqualTo(effect));
            Assert.That(_em.GetBuffer<BActiveEffectSlot>(target).Length, Is.EqualTo(0));
            Assert.That(_em.GetComponentData<CActiveEffectStore>(target).LastCompactedFrame, Is.EqualTo(25));

            RunAbilityGroup();

            Assert.That(_em.Exists(ability), Is.False);
            Assert.That(_em.GetBuffer<BGrantedAbility>(target).Length, Is.EqualTo(0));
            Assert.That(_em.GetBuffer<BActiveEffectSlot>(target).Length, Is.EqualTo(0));
            Assert.That(_em.GetComponentData<CActiveEffectStore>(target).LastCompactedFrame, Is.EqualTo(25));
        }

        [Test]
        public void ChunkSkipIndexRefreshesOwnerLocalNoopAndDuePeriodFacts()
        {
            var target = CreateOwnerLocalStoreTarget();
            var slots = _em.GetBuffer<BActiveEffectSlot>(target);
            slots.Add(new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.Active,
                StartFrame = 10,
                StateStartFrame = 10,
                DurationFrame = 100,
                RemainingFrame = 100,
                PeriodFrame = 10,
                LastPeriodFrame = 15,
            });
            slots.Add(new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.Active,
                StartFrame = 10,
                StateStartFrame = 10,
                DurationFrame = 100,
                RemainingFrame = 100,
                PeriodFrame = 5,
                LastPeriodFrame = 10,
            });
            slots.Add(new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.Inhibited,
            });
            slots.Add(new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.PendingRemove,
            });

            Assert.That(ActiveEffectStore.TryRefreshChunkSkipIndex(_em, target, currentFrame: 20), Is.True);

            var store = _em.GetComponentData<CActiveEffectStore>(target);
            Assert.That(store.LastChunkSkipIndexFrame, Is.EqualTo(20));
            Assert.That(store.ChunkSkipMatchedSlotCount, Is.EqualTo(4));
            Assert.That(store.ChunkSkipSkippedSlotCount, Is.EqualTo(3));
            Assert.That(store.ChunkSkipDuePeriodSlotCount, Is.EqualTo(1));
            Assert.That(store.ChunkSkipNoopSlotCount, Is.EqualTo(3));
            Assert.That(HasFlag(store.ChunkSkipReasonFlags, EActiveEffectChunkSkipReasonFlags.NoPeriodDue), Is.False);
            Assert.That(HasFlag(store.ChunkSkipReasonFlags, EActiveEffectChunkSkipReasonFlags.AllSlotsSkippable), Is.False);
            Assert.That(HasFlag(store.ChunkSkipReasonFlags, EActiveEffectChunkSkipReasonFlags.HasInhibitedSlots), Is.True);
            Assert.That(HasFlag(store.ChunkSkipReasonFlags, EActiveEffectChunkSkipReasonFlags.HasPendingRemoveSlots), Is.True);
        }

        [Test]
        public void ChunkSkipIndexKeepsInactiveTickingDurationAsDueWork()
        {
            var target = CreateOwnerLocalStoreTarget();
            var slots = _em.GetBuffer<BActiveEffectSlot>(target);
            slots.Add(new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.Inhibited,
                StartFrame = 0,
                StateStartFrame = 5,
                DurationFrame = 10,
                RemainingFrame = 10,
                Flags = (int)(EActiveEffectSlotFlags.HasDuration | EActiveEffectSlotFlags.TicksWhenInactive),
            });

            Assert.That(ActiveEffectStore.IsTickDue(slots[0], currentFrame: 10), Is.True);
            Assert.That(ActiveEffectStore.TryRefreshChunkSkipIndex(_em, target, currentFrame: 10), Is.True);

            var store = _em.GetComponentData<CActiveEffectStore>(target);
            Assert.That(store.ChunkSkipMatchedSlotCount, Is.EqualTo(1));
            Assert.That(store.ChunkSkipSkippedSlotCount, Is.EqualTo(0));
            Assert.That(store.ChunkSkipNoopSlotCount, Is.EqualTo(0));
            Assert.That(store.ChunkSkipDuePeriodSlotCount, Is.EqualTo(0));
            Assert.That(HasFlag(store.ChunkSkipReasonFlags, EActiveEffectChunkSkipReasonFlags.AllSlotsSkippable), Is.False);
            Assert.That(HasFlag(store.ChunkSkipReasonFlags, EActiveEffectChunkSkipReasonFlags.HasInhibitedSlots), Is.True);
        }

        [Test]
        public void TickActionFlagsClassifyOwnerLocalPeriodAndDurationWork()
        {
            var noop = new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.Active,
                StartFrame = 10,
                DurationFrame = 100,
                RemainingFrame = 100,
                PeriodFrame = 10,
                LastPeriodFrame = 15,
            };
            var periodOnly = new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.Active,
                StartFrame = 10,
                DurationFrame = 100,
                RemainingFrame = 100,
                PeriodFrame = 5,
                LastPeriodFrame = 10,
            };
            var durationOnly = new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.Active,
                StartFrame = 0,
                DurationFrame = 10,
                RemainingFrame = 10,
            };
            var both = new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.Active,
                StartFrame = 0,
                DurationFrame = 10,
                RemainingFrame = 10,
                PeriodFrame = 5,
                LastPeriodFrame = 0,
            };
            var inactiveTickingDuration = new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.Inhibited,
                StartFrame = 0,
                DurationFrame = 10,
                RemainingFrame = 10,
                Flags = (int)(EActiveEffectSlotFlags.HasDuration | EActiveEffectSlotFlags.TicksWhenInactive),
            };
            var inactiveNoop = inactiveTickingDuration;
            inactiveNoop.Flags = (int)EActiveEffectSlotFlags.HasDuration;

            Assert.That(ActiveEffectStore.CreateTickActionFlags(noop, currentFrame: 20),
                Is.EqualTo((int)EActiveEffectTickActionFlags.None));

            var periodFlags = ActiveEffectStore.CreateTickActionFlags(periodOnly, currentFrame: 20);
            Assert.That(HasFlag(periodFlags, EActiveEffectTickActionFlags.Period), Is.True);
            Assert.That(HasFlag(periodFlags, EActiveEffectTickActionFlags.DurationExpire), Is.False);
            Assert.That(ActiveEffectStore.IsTickDue(periodOnly, currentFrame: 20), Is.True);

            var durationFlags = ActiveEffectStore.CreateTickActionFlags(durationOnly, currentFrame: 10);
            Assert.That(HasFlag(durationFlags, EActiveEffectTickActionFlags.Period), Is.False);
            Assert.That(HasFlag(durationFlags, EActiveEffectTickActionFlags.DurationExpire), Is.True);

            var bothFlags = ActiveEffectStore.CreateTickActionFlags(both, currentFrame: 10);
            Assert.That(HasFlag(bothFlags, EActiveEffectTickActionFlags.Period), Is.True);
            Assert.That(HasFlag(bothFlags, EActiveEffectTickActionFlags.DurationExpire), Is.True);

            var inactiveFlags = ActiveEffectStore.CreateTickActionFlags(inactiveTickingDuration, currentFrame: 10);
            Assert.That(HasFlag(inactiveFlags, EActiveEffectTickActionFlags.Period), Is.False);
            Assert.That(HasFlag(inactiveFlags, EActiveEffectTickActionFlags.DurationExpire), Is.True);

            Assert.That(ActiveEffectStore.CreateTickActionFlags(inactiveNoop, currentFrame: 10),
                Is.EqualTo((int)EActiveEffectTickActionFlags.None));
        }

        [Test]
        public void TickActionFlagsClassifyLegacyDurationPeriodAndDurationWork()
        {
            var activeDurationDefinition = new CDurationDefinition
            {
                Duration = 100,
                TimeUnit = TimeUnit.Frame,
                StopTickWhenDeactivated = true,
            };
            var activeDurationRuntime = new CDurationRuntime
            {
                Active = true,
                ActiveTime = 0,
                ResolvedDuration = 100,
                ResolvedTimeUnit = TimeUnit.Frame,
            };
            var activeNoop = ActiveEffectStore.CreateLegacyDurationTickActionFlags(
                activeDurationDefinition,
                activeDurationRuntime,
                isAppliedDurationEffect: true,
                hasPeriodDefinition: true,
                periodFrame: 10,
                periodStartFrame: 15,
                currentFrame: 20);
            Assert.That(activeNoop, Is.EqualTo((int)EActiveEffectTickActionFlags.None));

            var periodOnly = ActiveEffectStore.CreateLegacyDurationTickActionFlags(
                activeDurationDefinition,
                activeDurationRuntime,
                isAppliedDurationEffect: true,
                hasPeriodDefinition: true,
                periodFrame: 5,
                periodStartFrame: 10,
                currentFrame: 20);
            Assert.That(HasFlag(periodOnly, EActiveEffectTickActionFlags.Period), Is.True);
            Assert.That(HasFlag(periodOnly, EActiveEffectTickActionFlags.DurationExpire), Is.False);

            var durationOnly = ActiveEffectStore.CreateLegacyDurationTickActionFlags(
                activeDurationDefinition,
                activeDurationRuntime,
                isAppliedDurationEffect: true,
                hasPeriodDefinition: false,
                periodFrame: 0,
                periodStartFrame: 0,
                currentFrame: 100);
            Assert.That(HasFlag(durationOnly, EActiveEffectTickActionFlags.Period), Is.False);
            Assert.That(HasFlag(durationOnly, EActiveEffectTickActionFlags.DurationExpire), Is.True);

            var both = ActiveEffectStore.CreateLegacyDurationTickActionFlags(
                activeDurationDefinition,
                activeDurationRuntime,
                isAppliedDurationEffect: true,
                hasPeriodDefinition: true,
                periodFrame: 5,
                periodStartFrame: 10,
                currentFrame: 100);
            Assert.That(HasFlag(both, EActiveEffectTickActionFlags.Period), Is.True);
            Assert.That(HasFlag(both, EActiveEffectTickActionFlags.DurationExpire), Is.True);

            var inactiveTickingDefinition = activeDurationDefinition;
            inactiveTickingDefinition.StopTickWhenDeactivated = false;
            var inactiveRuntime = activeDurationRuntime;
            inactiveRuntime.Active = false;
            var inactiveExpire = ActiveEffectStore.CreateLegacyDurationTickActionFlags(
                inactiveTickingDefinition,
                inactiveRuntime,
                isAppliedDurationEffect: true,
                hasPeriodDefinition: true,
                periodFrame: 1,
                periodStartFrame: 0,
                currentFrame: 100);
            Assert.That(HasFlag(inactiveExpire, EActiveEffectTickActionFlags.Period), Is.False);
            Assert.That(HasFlag(inactiveExpire, EActiveEffectTickActionFlags.DurationExpire), Is.True);

            Assert.That(ActiveEffectStore.CreateLegacyDurationTickActionFlags(
                    inactiveTickingDefinition,
                    inactiveRuntime,
                    isAppliedDurationEffect: false,
                    hasPeriodDefinition: true,
                    periodFrame: 1,
                    periodStartFrame: 0,
                    currentFrame: 100),
                Is.EqualTo((int)EActiveEffectTickActionFlags.None));

            var inactiveStoppedDefinition = inactiveTickingDefinition;
            inactiveStoppedDefinition.StopTickWhenDeactivated = true;
            Assert.That(ActiveEffectStore.CreateLegacyDurationTickActionFlags(
                    inactiveStoppedDefinition,
                    inactiveRuntime,
                    isAppliedDurationEffect: true,
                    hasPeriodDefinition: true,
                    periodFrame: 1,
                    periodStartFrame: 0,
                    currentFrame: 100),
                Is.EqualTo((int)EActiveEffectTickActionFlags.None));
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

        private Entity CreateActiveAbility(Entity target, int abilityCode)
        {
            var ability = CreateEntity();
            _em.AddComponentData(ability, new CAbilityBaseInfo
            {
                Code = abilityCode,
                Level = 1,
                Owner = target,
            });
            _em.AddComponentData(ability, new CAbilityRuntimeState
            {
                Phase = EAbilityPhase.Active,
            });
            _em.AddComponent<CAbilityActive>(ability);
            return ability;
        }

        private Entity CreateGrantedAbility(
            Entity target,
            Entity effect,
            int abilityCode,
            bool active = true,
            GrantedAbilityDeactivationPolicy deactivationPolicy = GrantedAbilityDeactivationPolicy.None,
            GrantedAbilityRemovePolicy removePolicy = GrantedAbilityRemovePolicy.WhenEnd)
        {
            var ability = CreateEntity();
            _em.AddComponentData(ability, new CAbilityBaseInfo
            {
                Code = abilityCode,
                Level = 1,
                Owner = target,
            });
            _em.AddComponentData(ability, new CAbilityRuntimeState
            {
                Phase = active ? EAbilityPhase.Active : EAbilityPhase.Ready,
            });
            if (active)
                _em.AddComponent<CAbilityActive>(ability);
            _em.AddComponentData(ability, new CGrantedByEffect
            {
                SourceEffect = effect,
                ActivationPolicy = GrantedAbilityActivationPolicy.None,
                DeactivationPolicy = deactivationPolicy,
                RemovePolicy = removePolicy,
            });
            return ability;
        }

        private static void RunAbilityGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASAbilityGroup>().Update();
        }

        private static void RunCommandGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>().Update();
        }

        private static void RunEffectGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASEffectGroup>().Update();
        }

        private GlobalTimer SetFrame(int frame)
        {
            var previousTimer = _em.GetComponentData<GlobalTimer>(GASManager.EntityGlobalTimer);
            _em.SetComponentData(GASManager.EntityGlobalTimer, new GlobalTimer
            {
                Frame = frame,
                Turn = previousTimer.Turn,
            });
            return previousTimer;
        }

        private void RestoreFrame(GlobalTimer timer)
        {
            _em.SetComponentData(GASManager.EntityGlobalTimer, timer);
        }

        private Entity CreateEntity()
        {
            var entity = _em.CreateEntity();
            _entities.Add(entity);
            return entity;
        }

        private Entity CreateOwnerLocalStoreTarget()
        {
            var target = CreateEntity();
            _em.AddComponentData(target, ActiveEffectStore.CreateDefault());
            _em.AddBuffer<BActiveEffectSlot>(target);
            _em.AddBuffer<BActiveEffectCleanupRecord>(target);
            return target;
        }

        private BActiveEffectSlot GetSingleSlot(Entity target)
        {
            var slots = _em.GetBuffer<BActiveEffectSlot>(target);
            Assert.That(slots.Length, Is.EqualTo(1));
            return slots[0];
        }

        private BGlobalActiveEffectIndex GetSingleGlobalIndex(Entity indexOwner)
        {
            var indices = _em.GetBuffer<BGlobalActiveEffectIndex>(indexOwner);
            Assert.That(indices.Length, Is.EqualTo(1));
            return indices[0];
        }

        private void AddGlobalIndexStableRow(Entity effect)
        {
            if (!_em.HasComponent<CActiveEffectGlobalIndexStableRow>(effect))
                _em.AddComponentData(effect, ActiveEffectStore.CreateGlobalIndexStableRowDefault(effect));
        }

        private void AssertStableRowMatchesIndex(
            Entity effect,
            Entity indexOwner,
            BGlobalActiveEffectIndex index,
            int currentFrame)
        {
            Assert.That(_em.HasComponent<CActiveEffectGlobalIndexStableRow>(effect), Is.True);
            var row = _em.GetComponentData<CActiveEffectGlobalIndexStableRow>(effect);
            var bucketIndex = ActiveEffectStore.ResolveGlobalIndexBucket(index.OwnerAsc, effect);
            Assert.That(ActiveEffectStore.TryGetGlobalIndexBucketOwner(_em, bucketIndex, out var bucketOwner), Is.True);

            Assert.That(row.Version, Is.EqualTo(ActiveEffectStore.CurrentVersion));
            Assert.That(row.IsIndexed, Is.EqualTo(1));
            Assert.That(row.ActiveEffectEntity, Is.EqualTo(effect));
            Assert.That(row.RootOwner, Is.EqualTo(indexOwner));
            Assert.That(row.BucketOwner, Is.EqualTo(bucketOwner));
            Assert.That(row.BucketIndex, Is.EqualTo(bucketIndex));
            Assert.That(row.Sequence, Is.EqualTo(index.Sequence));
            Assert.That(row.OwnerAsc, Is.EqualTo(index.OwnerAsc));
            Assert.That(row.GameplayEffectCode, Is.EqualTo(index.GameplayEffectCode));
            Assert.That(row.State, Is.EqualTo(index.State));
            Assert.That(row.PeriodDueFrame, Is.EqualTo(index.PeriodDueFrame));
            Assert.That(row.DurationDueFrame, Is.EqualTo(index.DurationDueFrame));
            Assert.That(row.LastSyncedFrame, Is.EqualTo(currentFrame));
            Assert.That(HasFlag(row.IndexFlags, EActiveEffectGlobalIndexFlags.StableRowBacked), Is.True);
        }

        private static void AssertGlobalIndexBucketOwners(EntityManager em, Entity indexOwner)
        {
            Assert.That(em.HasBuffer<BGlobalActiveEffectIndexBucketOwner>(indexOwner), Is.True);

            var bucketOwners = em.GetBuffer<BGlobalActiveEffectIndexBucketOwner>(indexOwner);
            Assert.That(bucketOwners.Length, Is.EqualTo(ActiveEffectStore.GlobalIndexBucketCount));
            var seen = new bool[ActiveEffectStore.GlobalIndexBucketCount];

            for (var i = 0; i < bucketOwners.Length; i++)
            {
                var bucketOwnerRef = bucketOwners[i];
                Assert.That(bucketOwnerRef.BucketIndex, Is.InRange(0, ActiveEffectStore.GlobalIndexBucketCount - 1));
                Assert.That(seen[bucketOwnerRef.BucketIndex], Is.False);
                seen[bucketOwnerRef.BucketIndex] = true;

                var bucketOwner = bucketOwnerRef.BucketOwner;
                Assert.That(bucketOwner, Is.Not.EqualTo(Entity.Null));
                Assert.That(em.Exists(bucketOwner), Is.True);
                Assert.That(em.HasComponent<CActiveEffectGlobalIndexBucket>(bucketOwner), Is.True);
                Assert.That(em.HasBuffer<BGlobalActiveEffectIndex>(bucketOwner), Is.True);

                var bucket = em.GetComponentData<CActiveEffectGlobalIndexBucket>(bucketOwner);
                Assert.That(bucket.Version, Is.EqualTo(ActiveEffectStore.CurrentVersion));
                Assert.That(bucket.RootOwner, Is.EqualTo(indexOwner));
                Assert.That(bucket.BucketIndex, Is.EqualTo(bucketOwnerRef.BucketIndex));
                Assert.That(bucket.BucketCount, Is.EqualTo(ActiveEffectStore.GlobalIndexBucketCount));
                Assert.That(bucket.IndexedEffectCount, Is.EqualTo(em.GetBuffer<BGlobalActiveEffectIndex>(bucketOwner).Length));
            }

            for (var i = 0; i < seen.Length; i++)
                Assert.That(seen[i], Is.True, "missing global index bucket " + i);
        }

        private static void AssertGlobalIndexEntryInResolvedBucket(
            EntityManager em,
            Entity effect,
            Entity ownerAsc)
        {
            var bucketIndex = ActiveEffectStore.ResolveGlobalIndexBucket(ownerAsc, effect);
            Assert.That(ActiveEffectStore.TryGetGlobalIndexBucketOwner(em, bucketIndex, out var bucketOwner), Is.True);

            var bucket = em.GetComponentData<CActiveEffectGlobalIndexBucket>(bucketOwner);
            Assert.That(bucket.BucketIndex, Is.EqualTo(bucketIndex));

            var indices = em.GetBuffer<BGlobalActiveEffectIndex>(bucketOwner);
            var found = false;
            for (var i = 0; i < indices.Length; i++)
            {
                var index = indices[i];
                if (index.ActiveEffectEntity == effect && index.OwnerAsc == ownerAsc)
                {
                    found = true;
                    break;
                }
            }

            Assert.That(found, Is.True);
        }

        private void ResetGlobalIndexStore()
        {
            if (!ActiveEffectStore.TryGetGlobalIndexStore(_em, out var indexOwner)
                || indexOwner == Entity.Null
                || !_em.Exists(indexOwner))
            {
                return;
            }

            _em.SetComponentData(indexOwner, ActiveEffectStore.CreateGlobalIndexDefault());
            _em.GetBuffer<BGlobalActiveEffectIndex>(indexOwner).Clear();
            if (!_em.HasBuffer<BGlobalActiveEffectIndexBucketOwner>(indexOwner))
                return;

            var bucketOwners = _em.GetBuffer<BGlobalActiveEffectIndexBucketOwner>(indexOwner);
            for (var i = 0; i < bucketOwners.Length; i++)
            {
                var bucketOwner = bucketOwners[i].BucketOwner;
                if (bucketOwner == Entity.Null || !_em.Exists(bucketOwner))
                    continue;

                if (_em.HasBuffer<BGlobalActiveEffectIndex>(bucketOwner))
                    _em.GetBuffer<BGlobalActiveEffectIndex>(bucketOwner).Clear();
                if (_em.HasComponent<CActiveEffectGlobalIndexBucket>(bucketOwner))
                {
                    var bucket = _em.GetComponentData<CActiveEffectGlobalIndexBucket>(bucketOwner);
                    bucket.IndexedEffectCount = 0;
                    bucket.ActiveCount = 0;
                    bucket.InhibitedCount = 0;
                    bucket.PendingRemoveCount = 0;
                    bucket.PeriodDueCount = 0;
                    bucket.DurationDueCount = 0;
                    bucket.StaleCount = 0;
                    bucket.IndexedStableRowCount = 0;
                    bucket.StaleStableRowCount = 0;
                    _em.SetComponentData(bucketOwner, bucket);
                }
            }
        }

        private static CTagMask CreateMask(int tagIndex)
        {
            var mask = new CTagMask();
            mask.AddTag(tagIndex);
            return mask;
        }

        private bool HasTempTagSource(Entity target, Entity source, int tagIndex)
        {
            if (!_em.HasBuffer<BTempTagSource>(target))
                return false;

            var sources = _em.GetBuffer<BTempTagSource>(target);
            for (var i = 0; i < sources.Length; i++)
            {
                if (sources[i].Source == source && sources[i].TagIndex == tagIndex)
                    return true;
            }

            return false;
        }

        private static bool HasFlag(BActiveEffectSlot slot, EActiveEffectSlotFlags flag)
        {
            return (slot.Flags & (int)flag) != 0;
        }

        private static bool HasFlag(int flags, EActiveEffectSlotFlags flag)
        {
            return (flags & (int)flag) != 0;
        }

        private static bool HasFlag(int flags, EActiveEffectTickActionFlags flag)
        {
            return (flags & (int)flag) != 0;
        }

        private static bool HasFlag(int flags, EActiveEffectCleanupWorkFlags flag)
        {
            return (flags & (int)flag) != 0;
        }

        private static bool HasFlag(int flags, EActiveEffectChunkSkipReasonFlags flag)
        {
            return (flags & (int)flag) != 0;
        }

        private static bool HasFlag(int flags, EActiveEffectGlobalIndexFlags flag)
        {
            return (flags & (int)flag) != 0;
        }

        private sealed class GrantedTagDefinitionConfig : GameplayEffectComponentConfig
        {
            public CTagMask Tags;

            public override void LoadToGameplayEffectEntity(Entity ge)
            {
                GASManager.EntityManager.AddComponentData(ge, new CEffectGrantedTags
                {
                    Tags = Tags,
                });
            }
        }
    }
}
