using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public enum EActiveEffectSlotState : byte
    {
        PendingApply = 0,
        Active = 1,
        Inhibited = 2,
        PendingRemove = 3,
    }

    public enum EActiveEffectSlotFlags : int
    {
        None = 0,
        LegacyEntityBacked = 1 << 0,
        HasDuration = 1 << 1,
        HasPeriod = 1 << 2,
        HasStacking = 1 << 3,
        HasGrantedTags = 1 << 4,
        HasGrantedAbilities = 1 << 5,
        HasOngoingRequirements = 1 << 6,
        TicksWhenInactive = 1 << 7,
    }

    [System.Flags]
    public enum EActiveEffectTickActionFlags : int
    {
        None = 0,
        Period = 1 << 0,
        DurationExpire = 1 << 1,
    }

    [System.Flags]
    public enum EActiveEffectCleanupWorkFlags : int
    {
        None = 0,
        OwnerLocalSlot = 1 << 0,
        LegacyTargetBuffer = 1 << 1,
        RuntimeModifiers = 1 << 2,
        GrantedTags = 1 << 3,
        GrantedAbilities = 1 << 4,
        DurationRuntime = 1 << 5,
        EntityDestroy = 1 << 6,
    }

    [System.Flags]
    public enum EActiveEffectChunkSkipReasonFlags : int
    {
        None = 0,
        EmptyOwner = 1 << 0,
        NoActiveSlots = 1 << 1,
        NoPeriodDue = 1 << 2,
        AllSlotsSkippable = 1 << 3,
        HasInhibitedSlots = 1 << 4,
        HasPendingRemoveSlots = 1 << 5,
    }

    [System.Flags]
    public enum EActiveEffectGlobalIndexFlags : int
    {
        None = 0,
        PeriodDue = 1 << 0,
        DurationDue = 1 << 1,
        Inhibited = 1 << 2,
        PendingRemove = 1 << 3,
        OwnerLocalBacked = 1 << 4,
        LegacyEntityBacked = 1 << 5,
        StableRowBacked = 1 << 6,
    }

    public struct CActiveEffectStore : IComponentData
    {
        public int Version;
        public int NextSequence;
        public int LastCompactedFrame;
        public int LastCleanupFrame;
        public int CleanupRecordCount;
        public int LastChunkSkipIndexFrame;
        public int ChunkSkipMatchedSlotCount;
        public int ChunkSkipSkippedSlotCount;
        public int ChunkSkipDuePeriodSlotCount;
        public int ChunkSkipNoopSlotCount;
        public int ChunkSkipReasonFlags;
    }

    public struct CActiveEffectGlobalIndexStore : IComponentData
    {
        public int Version;
        public int LastSyncedFrame;
        public int LastCompactedFrame;
        public int IndexedEffectCount;
        public int ActiveCount;
        public int InhibitedCount;
        public int PendingRemoveCount;
        public int PeriodDueCount;
        public int DurationDueCount;
        public int StaleCount;
        public int IndexedStableRowCount;
        public int StaleStableRowCount;
    }

    public struct CActiveEffectGlobalIndexBucket : IComponentData
    {
        public int Version;
        public Entity RootOwner;
        public int BucketIndex;
        public int BucketCount;
        public int LastSyncedFrame;
        public int LastCompactedFrame;
        public int IndexedEffectCount;
        public int ActiveCount;
        public int InhibitedCount;
        public int PendingRemoveCount;
        public int PeriodDueCount;
        public int DurationDueCount;
        public int StaleCount;
        public int IndexedStableRowCount;
        public int StaleStableRowCount;
    }

    public struct CActiveEffectGlobalIndexStableRow : IComponentData
    {
        public int Version;
        public int IsIndexed;
        public Entity ActiveEffectEntity;
        public Entity RootOwner;
        public Entity BucketOwner;
        public int BucketIndex;
        public int Sequence;
        public Entity OwnerAsc;
        public Entity SourceAsc;
        public Entity SourceAbility;
        public Entity SourceEffect;
        public Entity Instigator;
        public Entity Causer;
        public int GameplayEffectCode;
        public int Level;
        public int StackCount;
        public EActiveEffectSlotState State;
        public EActiveEffectSlotState PreviousState;
        public int StartFrame;
        public int StateStartFrame;
        public int DurationFrame;
        public int RemainingFrame;
        public int DurationDueFrame;
        public int PeriodFrame;
        public int LastPeriodFrame;
        public int PeriodDueFrame;
        public int ActiveGrantedTagCount;
        public int ActiveGrantedAbilityCount;
        public int SlotFlags;
        public int IndexFlags;
        public int LastSyncedFrame;
        public int LastCompactedFrame;
    }

    [InternalBufferCapacity(ActiveEffectStore.InlineSlotCapacity)]
    public struct BActiveEffectSlot : IBufferElementData
    {
        public int Sequence;
        public EActiveEffectSlotState State;
        public EActiveEffectSlotState PreviousState;
        public Entity ActiveEffectEntity;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public Entity SourceEffect;
        public Entity Instigator;
        public Entity Causer;
        public int GameplayEffectCode;
        public int Level;
        public int StackCount;
        public int ContextId;
        public int ParentContextId;
        public int StartFrame;
        public int StateStartFrame;
        public int DurationFrame;
        public int RemainingFrame;
        public int PeriodFrame;
        public int LastPeriodFrame;
        public int ActiveGrantedTagCount;
        public int ActiveGrantedAbilityCount;
        public int Flags;
    }

    [InternalBufferCapacity(ActiveEffectStore.InlineGlobalIndexCapacity)]
    public struct BGlobalActiveEffectIndex : IBufferElementData
    {
        public int Sequence;
        public Entity ActiveEffectEntity;
        public Entity OwnerAsc;
        public Entity SourceAsc;
        public Entity SourceAbility;
        public Entity SourceEffect;
        public Entity Instigator;
        public Entity Causer;
        public int GameplayEffectCode;
        public int Level;
        public int StackCount;
        public EActiveEffectSlotState State;
        public EActiveEffectSlotState PreviousState;
        public int StartFrame;
        public int StateStartFrame;
        public int DurationFrame;
        public int RemainingFrame;
        public int DurationDueFrame;
        public int PeriodFrame;
        public int LastPeriodFrame;
        public int PeriodDueFrame;
        public int ActiveGrantedTagCount;
        public int ActiveGrantedAbilityCount;
        public int SlotFlags;
        public int IndexFlags;
        public int LastSyncedFrame;
    }

    [InternalBufferCapacity(ActiveEffectStore.GlobalIndexBucketCount)]
    public struct BGlobalActiveEffectIndexBucketOwner : IBufferElementData
    {
        public int BucketIndex;
        public Entity BucketOwner;
    }

    [InternalBufferCapacity(ActiveEffectStore.InlineCleanupRecordCapacity)]
    public struct BActiveEffectCleanupRecord : IBufferElementData
    {
        public int Sequence;
        public Entity ActiveEffectEntity;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public Entity SourceEffect;
        public Entity Instigator;
        public Entity Causer;
        public int GameplayEffectCode;
        public int Level;
        public int StackCount;
        public int ContextId;
        public int ParentContextId;
        public int CleanupFrame;
        public EGameplayEffectLifecycleState CleanupState;
        public EActiveEffectSlotState SlotState;
        public EActiveEffectSlotState PreviousSlotState;
        public int DurationFrame;
        public int RemainingFrame;
        public int PeriodFrame;
        public int LastPeriodFrame;
        public int ActiveGrantedTagCount;
        public int ActiveGrantedAbilityCount;
        public int ActiveModifierCount;
        public int RequestedCleanupWorkFlags;
        public int ResolvedCleanupWorkFlags;
        public int CleanupResolvedFrame;
        public int Flags;
    }

    public readonly struct ActiveEffectChunkSkipIndexSnapshot
    {
        public readonly int MatchedSlotCount;
        public readonly int SkippedSlotCount;
        public readonly int DuePeriodSlotCount;
        public readonly int NoopSlotCount;
        public readonly int ReasonFlags;

        public bool CanSkipOwner => MatchedSlotCount == 0
                                    || (MatchedSlotCount > 0 && SkippedSlotCount == MatchedSlotCount);

        public ActiveEffectChunkSkipIndexSnapshot(
            int matchedSlotCount,
            int skippedSlotCount,
            int duePeriodSlotCount,
            int noopSlotCount,
            int reasonFlags)
        {
            MatchedSlotCount = matchedSlotCount;
            SkippedSlotCount = skippedSlotCount;
            DuePeriodSlotCount = duePeriodSlotCount;
            NoopSlotCount = noopSlotCount;
            ReasonFlags = reasonFlags;
        }
    }

    public static class ActiveEffectStore
    {
        public const int CurrentVersion = 1;
        public const int InlineSlotCapacity = 8;
        public const int InlineGlobalIndexCapacity = 64;
        public const int GlobalIndexBucketCount = 16;
        public const int InlineCleanupRecordCapacity = 4;
        public const int MaxCleanupRecordCount = 16;

        private static EntityManager _cachedGlobalIndexEntityManager;
        private static Entity _cachedGlobalIndexEntity;
        private static bool _hasCachedGlobalIndexOwner;

        public static CActiveEffectStore CreateDefault()
        {
            return new CActiveEffectStore
            {
                Version = CurrentVersion,
                NextSequence = 1,
            };
        }

        public static CActiveEffectGlobalIndexStore CreateGlobalIndexDefault()
        {
            return new CActiveEffectGlobalIndexStore
            {
                Version = CurrentVersion,
            };
        }

        public static CActiveEffectGlobalIndexStableRow CreateGlobalIndexStableRowDefault(Entity effectEntity)
        {
            return new CActiveEffectGlobalIndexStableRow
            {
                Version = CurrentVersion,
                ActiveEffectEntity = effectEntity,
                BucketIndex = -1,
            };
        }

        public static Entity EnsureGlobalIndexStore(EntityManager em)
        {
            if (TryGetGlobalIndexStore(em, out var indexOwner))
            {
                EnsureGlobalIndexBuffers(em, indexOwner);
                EnsureGlobalIndexBucketOwners(em, indexOwner);
                return indexOwner;
            }

            indexOwner = em.CreateEntity();
            em.SetName(indexOwner, "ActiveEffectGlobalIndexStore");
            em.AddComponentData(indexOwner, CreateGlobalIndexDefault());
            em.AddBuffer<BGlobalActiveEffectIndex>(indexOwner).EnsureCapacity(InlineGlobalIndexCapacity);
            em.AddBuffer<BGlobalActiveEffectIndexBucketOwner>(indexOwner).EnsureCapacity(GlobalIndexBucketCount);
            RegisterKnownGlobalIndexStore(em, indexOwner);
            EnsureGlobalIndexBucketOwners(em, indexOwner);
            return indexOwner;
        }

        public static bool TryGetGlobalIndexStore(EntityManager em, out Entity indexOwner)
        {
            return TryResolveKnownGlobalIndexStore(em, out indexOwner);
        }

        public static void RegisterKnownGlobalIndexStore(EntityManager em, Entity indexOwner)
        {
            if (!IsValidGlobalIndexStore(em, indexOwner))
                return;

            _cachedGlobalIndexEntityManager = em;
            _cachedGlobalIndexEntity = indexOwner;
            _hasCachedGlobalIndexOwner = true;
        }

        public static void EnsureGlobalIndexBuffers(EntityManager em, Entity indexOwner)
        {
            if (indexOwner == Entity.Null || !em.Exists(indexOwner))
                return;

            if (!em.HasBuffer<BGlobalActiveEffectIndex>(indexOwner))
                em.AddBuffer<BGlobalActiveEffectIndex>(indexOwner);
            if (!em.HasBuffer<BGlobalActiveEffectIndexBucketOwner>(indexOwner))
                em.AddBuffer<BGlobalActiveEffectIndexBucketOwner>(indexOwner);
        }

        public static bool TryUpsertDurationEffect(
            EntityManager em,
            Entity effectEntity,
            in CEffectContext context,
            in CDurationRuntime duration,
            EActiveEffectSlotState state,
            int currentFrame)
        {
            if (!TryGetStore(em, context.TargetAsc, out var store, out var slots))
                return false;

            var index = FindSlot(slots, effectEntity);
            if (index < 0 && slots.Length >= GASParameterSetting.ASC_MAX_GAMEPLAY_EFFECT_COUNT)
                return false;

            var slot = index >= 0
                ? slots[index]
                : new BActiveEffectSlot
                {
                    Sequence = Allocate(ref store.NextSequence),
                    ActiveEffectEntity = effectEntity,
                    StartFrame = duration.ActiveTime,
                    PreviousState = state,
                };

            var previousState = slot.State;
            if (index < 0 || previousState != state)
            {
                slot.PreviousState = index < 0 ? state : previousState;
                slot.StateStartFrame = currentFrame;
            }

            slot.State = state;
            slot.ActiveEffectEntity = effectEntity;
            slot.SourceAsc = context.SourceAsc;
            slot.TargetAsc = context.TargetAsc;
            slot.SourceAbility = context.SourceAbility;
            slot.SourceEffect = context.SourceEffect;
            slot.Instigator = context.Instigator;
            slot.Causer = context.Causer;
            slot.ContextId = context.ContextId;
            slot.ParentContextId = context.ParentContextId;
            slot.StartFrame = ResolveDurationStartFrame(duration, currentFrame, slot.StartFrame);
            slot.DurationFrame = duration.ResolvedDuration;
            slot.RemainingFrame = duration.RemainingTime;
            slot.StackCount = ResolveStackCount(em, effectEntity);
            slot.PeriodFrame = ResolvePeriodFrame(em, effectEntity);
            slot.LastPeriodFrame = ResolveLastPeriodFrame(em, effectEntity);
            if (slot.PeriodFrame > 0 && slot.LastPeriodFrame <= 0)
                slot.LastPeriodFrame = currentFrame;
            slot.ActiveGrantedTagCount = CountActiveGrantedTags(em, context.TargetAsc, effectEntity);
            slot.ActiveGrantedAbilityCount = CountActiveGrantedAbilities(em, effectEntity);
            slot.Flags = ResolveFlags(em, effectEntity);

            if (TryGetSpec(em, effectEntity, out var spec))
            {
                slot.GameplayEffectCode = spec.GameplayEffectCode;
                slot.Level = spec.Level;
                if (slot.StackCount <= 0)
                    slot.StackCount = spec.StackCount > 0 ? spec.StackCount : 1;
            }

            if (slot.StackCount <= 0)
                slot.StackCount = 1;

            if (index >= 0)
                slots[index] = slot;
            else
                slots.Add(slot);

            RefreshChunkSkipIndex(ref store, slots, currentFrame);
            em.SetComponentData(context.TargetAsc, store);
            TrySyncGlobalIndex(em, slot, currentFrame);
            return true;
        }

        public static bool TryRefreshDuration(
            EntityManager em,
            Entity effectEntity,
            in CEffectContext context,
            in CDurationRuntime duration)
        {
            if (!TryGetStore(em, context.TargetAsc, out var store, out var slots))
                return false;

            var index = FindSlot(slots, effectEntity);
            if (index < 0)
                return false;

            var slot = slots[index];
            slot.StartFrame = ResolveDurationStartFrame(duration, duration.ActiveTime, slot.StartFrame);
            slot.DurationFrame = duration.ResolvedDuration;
            slot.RemainingFrame = duration.RemainingTime;
            slot.LastPeriodFrame = ResolveLastPeriodFrame(em, effectEntity);
            slots[index] = slot;
            RefreshChunkSkipIndex(ref store, slots, store.LastChunkSkipIndexFrame);
            em.SetComponentData(context.TargetAsc, store);
            TrySyncGlobalIndex(em, slot, ResolveGlobalIndexSyncFrame(em, store));
            return true;
        }

        public static bool TryRefreshStackCount(
            EntityManager em,
            Entity effectEntity,
            in CEffectContext context,
            int stackCount)
        {
            if (!TryGetStore(em, context.TargetAsc, out var store, out var slots))
                return false;

            var index = FindSlot(slots, effectEntity);
            if (index < 0)
                return false;

            var slot = slots[index];
            slot.StackCount = stackCount > 0 ? stackCount : 1;
            slots[index] = slot;
            RefreshChunkSkipIndex(ref store, slots, store.LastChunkSkipIndexFrame);
            em.SetComponentData(context.TargetAsc, store);
            TrySyncGlobalIndex(em, slot, ResolveGlobalIndexSyncFrame(em, store));
            return true;
        }

        public static bool TryRefreshPeriodFrame(
            EntityManager em,
            Entity effectEntity,
            in CEffectContext context,
            int currentFrame)
        {
            if (!TryGetStore(em, context.TargetAsc, out var store, out var slots))
                return false;

            var index = FindSlot(slots, effectEntity);
            if (index < 0)
                return false;

            var slot = slots[index];
            slot.PeriodFrame = ResolvePeriodFrame(em, effectEntity);
            slot.LastPeriodFrame = currentFrame;
            slots[index] = slot;
            RefreshChunkSkipIndex(ref store, slots, currentFrame);
            em.SetComponentData(context.TargetAsc, store);
            TrySyncGlobalIndex(em, slot, currentFrame);
            return true;
        }

        public static bool TryRefreshGrantedAbilityState(
            EntityManager em,
            Entity effectEntity,
            in CEffectContext context)
        {
            if (!TryGetStore(em, context.TargetAsc, out var store, out var slots))
                return false;

            var index = FindSlot(slots, effectEntity);
            if (index < 0)
                return false;

            var slot = slots[index];
            slot.ActiveGrantedTagCount = CountActiveGrantedTags(em, context.TargetAsc, effectEntity);
            slot.ActiveGrantedAbilityCount = CountActiveGrantedAbilities(em, effectEntity);
            slot.Flags = ResolveFlags(em, effectEntity);
            slots[index] = slot;
            RefreshChunkSkipIndex(ref store, slots, store.LastChunkSkipIndexFrame);
            em.SetComponentData(context.TargetAsc, store);
            TrySyncGlobalIndex(em, slot, ResolveGlobalIndexSyncFrame(em, store));
            return true;
        }

        public static bool TryRefreshGrantedTagState(
            EntityManager em,
            Entity effectEntity,
            Entity owner)
        {
            if (!TryGetStore(em, owner, out var store, out var slots))
                return false;

            var index = FindSlot(slots, effectEntity);
            if (index < 0)
                return false;

            var slot = slots[index];
            slot.ActiveGrantedTagCount = CountActiveGrantedTags(em, owner, effectEntity);
            slot.Flags = ResolveFlags(em, effectEntity);
            slots[index] = slot;
            RefreshChunkSkipIndex(ref store, slots, store.LastChunkSkipIndexFrame);
            em.SetComponentData(owner, store);
            TrySyncGlobalIndex(em, slot, ResolveGlobalIndexSyncFrame(em, store));
            return true;
        }

        public static bool TryMarkPendingRemove(
            EntityManager em,
            Entity effectEntity,
            in CEffectContext context,
            int currentFrame)
        {
            if (!TryGetStore(em, context.TargetAsc, out var store, out var slots))
                return false;

            var index = FindSlot(slots, effectEntity);
            if (index < 0)
                return false;

            var slot = slots[index];
            var previousState = slot.State;
            if (previousState != EActiveEffectSlotState.PendingRemove)
            {
                slot.PreviousState = previousState;
                slot.StateStartFrame = currentFrame;
            }

            slot.State = EActiveEffectSlotState.PendingRemove;
            slot.ActiveEffectEntity = effectEntity;
            slot.SourceAsc = context.SourceAsc;
            slot.TargetAsc = context.TargetAsc;
            slot.SourceAbility = context.SourceAbility;
            slot.SourceEffect = context.SourceEffect;
            slot.Instigator = context.Instigator;
            slot.Causer = context.Causer;
            slot.ContextId = context.ContextId;
            slot.ParentContextId = context.ParentContextId;
            slots[index] = slot;

            RefreshChunkSkipIndex(ref store, slots, currentFrame);
            em.SetComponentData(context.TargetAsc, store);
            TrySyncGlobalIndex(em, slot, currentFrame);
            return true;
        }

        public static bool TryRemove(
            EntityManager em,
            Entity effectEntity,
            in CEffectContext context,
            int currentFrame)
        {
            if (!TryGetStore(em, context.TargetAsc, out var store, out var slots))
                return false;

            var removed = false;
            for (var i = slots.Length - 1; i >= 0; i--)
            {
                if (slots[i].ActiveEffectEntity != effectEntity)
                    continue;

                slots.RemoveAt(i);
                removed = true;
            }

            if (removed)
            {
                store.LastCompactedFrame = currentFrame;
                RefreshChunkSkipIndex(ref store, slots, currentFrame);
                em.SetComponentData(context.TargetAsc, store);
                TryRemoveGlobalIndex(em, effectEntity, currentFrame);
            }

            return removed;
        }

        public static bool TrySyncGlobalIndex(
            EntityManager em,
            in BActiveEffectSlot slot,
            int currentFrame)
        {
            if (slot.ActiveEffectEntity == Entity.Null || slot.TargetAsc == Entity.Null)
                return false;

            var entry = ToGlobalIndexEntry(slot, currentFrame);
            if (!TryGetGlobalIndexBucket(em, entry, out var indexOwner, out var bucketOwner, out var bucket, out var indices))
                return false;

            var indexedEntry = ResolveGlobalIndexStableRowFlag(em, entry);
            UpsertGlobalIndexEntry(indices, indexedEntry);
            SortGlobalIndexEntries(indices);
            TrySyncGlobalIndexStableRow(em, indexedEntry, indexOwner, bucketOwner, ResolveGlobalIndexBucket(indexedEntry), currentFrame);
            RefreshGlobalIndexBucketCounters(em, ref bucket, indices, currentFrame, compactFrame: false);
            em.SetComponentData(bucketOwner, bucket);
            RefreshGlobalIndexRootFromBuckets(em, indexOwner, currentFrame, compactFrame: false);
            return true;
        }

        public static BGlobalActiveEffectIndex CreateGlobalIndexEntry(in BActiveEffectSlot slot, int currentFrame)
        {
            return ToGlobalIndexEntry(slot, currentFrame);
        }

        public static int ResolveGlobalIndexBucket(in BGlobalActiveEffectIndex entry)
        {
            return ResolveGlobalIndexBucket(entry.OwnerAsc, entry.ActiveEffectEntity);
        }

        public static int ResolveGlobalIndexBucket(in BActiveEffectSlot slot)
        {
            return ResolveGlobalIndexBucket(slot.TargetAsc, slot.ActiveEffectEntity);
        }

        public static int ResolveGlobalIndexBucket(Entity ownerAsc, Entity activeEffectEntity)
        {
            unchecked
            {
                var hash = ownerAsc.Index;
                hash = (hash * 397) ^ ownerAsc.Version;
                hash = (hash * 397) ^ activeEffectEntity.Index;
                hash = (hash * 397) ^ activeEffectEntity.Version;
                var bucket = hash % GlobalIndexBucketCount;
                return bucket < 0 ? bucket + GlobalIndexBucketCount : bucket;
            }
        }

        public static bool TryGetGlobalIndexBucketOwner(
            EntityManager em,
            int bucketIndex,
            out Entity bucketOwner)
        {
            bucketOwner = Entity.Null;
            if (bucketIndex < 0
                || bucketIndex >= GlobalIndexBucketCount
                || !TryGetGlobalIndexStore(em, out var indexOwner)
                || !em.HasBuffer<BGlobalActiveEffectIndexBucketOwner>(indexOwner))
            {
                return false;
            }

            var bucketOwners = em.GetBuffer<BGlobalActiveEffectIndexBucketOwner>(indexOwner);
            for (var i = 0; i < bucketOwners.Length; i++)
            {
                var candidate = bucketOwners[i];
                if (candidate.BucketIndex != bucketIndex
                    || !IsValidGlobalIndexBucket(em, indexOwner, candidate.BucketOwner, bucketIndex))
                {
                    continue;
                }

                bucketOwner = candidate.BucketOwner;
                return true;
            }

            return false;
        }

        public static bool TryMergeGlobalIndexEntries(
            EntityManager em,
            NativeList<BGlobalActiveEffectIndex> entries,
            int currentFrame)
        {
            if (!TryGetGlobalIndexStore(em, out var indexOwner))
                return false;

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.ActiveEffectEntity == Entity.Null || entry.OwnerAsc == Entity.Null)
                    continue;

                if (!TryGetGlobalIndexBucket(em, indexOwner, entry, out var bucketOwner, out var bucket, out var indices))
                    return false;

                var indexedEntry = ResolveGlobalIndexStableRowFlag(em, entry);
                UpsertGlobalIndexEntry(indices, indexedEntry);
                SortGlobalIndexEntries(indices);
                TrySyncGlobalIndexStableRow(em, indexedEntry, indexOwner, bucketOwner, ResolveGlobalIndexBucket(indexedEntry), currentFrame);
                RefreshGlobalIndexBucketCounters(em, ref bucket, indices, currentFrame, compactFrame: false);
                em.SetComponentData(bucketOwner, bucket);
            }

            RefreshAllGlobalIndexBuckets(em, indexOwner, currentFrame, compactFrame: false);
            RefreshGlobalIndexRootFromBuckets(em, indexOwner, currentFrame, compactFrame: false);
            return true;
        }

        public static bool TryRemoveGlobalIndex(
            EntityManager em,
            Entity effectEntity,
            int currentFrame)
        {
            if (!TryGetGlobalIndexStore(em, out var indexOwner)
                || !TryGetGlobalIndexBucketOwners(em, indexOwner, out var bucketOwners))
            {
                return false;
            }

            var removed = false;
            for (var bucketIndex = 0; bucketIndex < bucketOwners.Length; bucketIndex++)
            {
                var bucketOwner = bucketOwners[bucketIndex].BucketOwner;
                if (!IsValidGlobalIndexBucket(em, indexOwner, bucketOwner, bucketOwners[bucketIndex].BucketIndex))
                    continue;

                var indices = em.GetBuffer<BGlobalActiveEffectIndex>(bucketOwner);
                for (var i = indices.Length - 1; i >= 0; i--)
                {
                    if (indices[i].ActiveEffectEntity != effectEntity)
                        continue;

                    indices.RemoveAt(i);
                    TryMarkGlobalIndexStableRowRemoved(em, effectEntity, currentFrame);
                    removed = true;
                }
            }

            if (!removed)
                return false;

            RefreshAllGlobalIndexBuckets(em, indexOwner, currentFrame, compactFrame: true);
            RefreshGlobalIndexRootFromBuckets(em, indexOwner, currentFrame, compactFrame: true);
            return true;
        }

        public static bool TryRefreshGlobalIndexCounters(EntityManager em, int currentFrame)
        {
            if (!TryGetGlobalIndexStore(em, out var indexOwner))
                return false;

            RefreshAllGlobalIndexBuckets(em, indexOwner, currentFrame, compactFrame: false);
            RefreshGlobalIndexRootFromBuckets(em, indexOwner, currentFrame, compactFrame: false);
            return true;
        }

        public static bool TrySyncGlobalIndexOwner(EntityManager em, Entity owner, int currentFrame)
        {
            if (!TryGetStore(em, owner, out _, out var slots)
                || !TryGetGlobalIndexStore(em, out var indexOwner))
            {
                return false;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.ActiveEffectEntity == Entity.Null || slot.TargetAsc == Entity.Null)
                    continue;

                var entry = ToGlobalIndexEntry(slot, currentFrame);
                if (!TryGetGlobalIndexBucket(em, indexOwner, entry, out var bucketOwner, out var bucket, out var indices))
                    return false;

                var indexedEntry = ResolveGlobalIndexStableRowFlag(em, entry);
                UpsertGlobalIndexEntry(indices, indexedEntry);
                SortGlobalIndexEntries(indices);
                TrySyncGlobalIndexStableRow(em, indexedEntry, indexOwner, bucketOwner, ResolveGlobalIndexBucket(indexedEntry), currentFrame);
                RefreshGlobalIndexBucketCounters(em, ref bucket, indices, currentFrame, compactFrame: false);
                em.SetComponentData(bucketOwner, bucket);
            }

            RefreshGlobalIndexRootFromBuckets(em, indexOwner, currentFrame, compactFrame: false);
            return true;
        }

        public static bool TryRefreshChunkSkipIndex(EntityManager em, Entity owner, int currentFrame)
        {
            if (!TryGetStore(em, owner, out var store, out var slots))
                return false;

            RefreshChunkSkipIndex(ref store, slots, currentFrame);
            em.SetComponentData(owner, store);
            TrySyncGlobalIndexOwner(em, owner, currentFrame);
            return true;
        }

        public static bool TryGetSlot(EntityManager em, Entity owner, Entity effectEntity, out BActiveEffectSlot slot)
        {
            slot = default;
            if (!TryGetStore(em, owner, out _, out var slots))
                return false;

            var index = FindSlot(slots, effectEntity);
            if (index < 0)
                return false;

            slot = slots[index];
            return true;
        }

        public static bool HasSlot(EntityManager em, Entity owner, Entity effectEntity)
        {
            return TryGetSlot(em, owner, effectEntity, out _);
        }

        public static bool IsTickDue(in BActiveEffectSlot slot, int currentFrame)
        {
            return CreateTickActionFlags(slot, currentFrame) != (int)EActiveEffectTickActionFlags.None;
        }

        public static int CreateTickActionFlags(in BActiveEffectSlot slot, int currentFrame)
        {
            var flags = EActiveEffectTickActionFlags.None;
            if (IsPeriodDue(slot, currentFrame))
                flags |= EActiveEffectTickActionFlags.Period;
            if (IsDurationDue(slot, currentFrame))
                flags |= EActiveEffectTickActionFlags.DurationExpire;

            return (int)flags;
        }

        public static int CreateLegacyDurationTickActionFlags(
            in CDurationDefinition durationDefinition,
            in CDurationRuntime durationRuntime,
            bool isAppliedDurationEffect,
            bool hasPeriodDefinition,
            int periodFrame,
            int periodStartFrame,
            int currentFrame)
        {
            var flags = EActiveEffectTickActionFlags.None;
            if (!durationRuntime.Active)
            {
                if (!isAppliedDurationEffect || durationDefinition.StopTickWhenDeactivated)
                    return (int)flags;

                if (durationRuntime.ResolvedDuration > 0
                    && currentFrame - durationRuntime.ActiveTime >= durationRuntime.ResolvedDuration)
                {
                    flags |= EActiveEffectTickActionFlags.DurationExpire;
                }

                return (int)flags;
            }

            if (hasPeriodDefinition
                && periodFrame > 0
                && currentFrame - periodStartFrame >= periodFrame)
            {
                flags |= EActiveEffectTickActionFlags.Period;
            }

            if (durationRuntime.ResolvedDuration > 0
                && currentFrame - durationRuntime.ActiveTime >= durationRuntime.ResolvedDuration)
            {
                flags |= EActiveEffectTickActionFlags.DurationExpire;
            }

            return (int)flags;
        }

        public static ActiveEffectChunkSkipIndexSnapshot CreateChunkSkipIndexSnapshot(
            DynamicBuffer<BActiveEffectSlot> slots,
            int currentFrame)
        {
            var matchedSlotCount = slots.Length;
            var skippedSlotCount = 0;
            var duePeriodSlotCount = 0;
            var noopSlotCount = 0;
            var activeSlotCount = 0;
            var hasInhibitedSlots = false;
            var hasPendingRemoveSlots = false;

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.State == EActiveEffectSlotState.Active)
                    activeSlotCount++;
                if (slot.State == EActiveEffectSlotState.Inhibited)
                    hasInhibitedSlots = true;
                if (slot.State == EActiveEffectSlotState.PendingRemove)
                    hasPendingRemoveSlots = true;

                var periodDue = IsPeriodDue(slot, currentFrame);
                if (periodDue)
                {
                    duePeriodSlotCount++;
                    continue;
                }

                if (IsNoopForChunkSkip(slot, currentFrame))
                {
                    skippedSlotCount++;
                    noopSlotCount++;
                }
            }

            var reasonFlags = EActiveEffectChunkSkipReasonFlags.None;
            if (matchedSlotCount == 0)
                reasonFlags |= EActiveEffectChunkSkipReasonFlags.EmptyOwner;
            if (activeSlotCount == 0)
                reasonFlags |= EActiveEffectChunkSkipReasonFlags.NoActiveSlots;
            if (duePeriodSlotCount == 0)
                reasonFlags |= EActiveEffectChunkSkipReasonFlags.NoPeriodDue;
            if (matchedSlotCount > 0 && skippedSlotCount == matchedSlotCount)
                reasonFlags |= EActiveEffectChunkSkipReasonFlags.AllSlotsSkippable;
            if (hasInhibitedSlots)
                reasonFlags |= EActiveEffectChunkSkipReasonFlags.HasInhibitedSlots;
            if (hasPendingRemoveSlots)
                reasonFlags |= EActiveEffectChunkSkipReasonFlags.HasPendingRemoveSlots;

            return new ActiveEffectChunkSkipIndexSnapshot(
                matchedSlotCount,
                skippedSlotCount,
                duePeriodSlotCount,
                noopSlotCount,
                (int)reasonFlags);
        }

        public static ActiveEffectChunkSkipIndexSnapshot RefreshChunkSkipIndexCounters(
            ref CActiveEffectStore store,
            DynamicBuffer<BActiveEffectSlot> slots,
            int currentFrame)
        {
            return RefreshChunkSkipIndex(ref store, slots, currentFrame);
        }

        private static ActiveEffectChunkSkipIndexSnapshot RefreshChunkSkipIndex(
            ref CActiveEffectStore store,
            DynamicBuffer<BActiveEffectSlot> slots,
            int currentFrame)
        {
            var snapshot = CreateChunkSkipIndexSnapshot(slots, currentFrame);
            store.LastChunkSkipIndexFrame = currentFrame;
            store.ChunkSkipMatchedSlotCount = snapshot.MatchedSlotCount;
            store.ChunkSkipSkippedSlotCount = snapshot.SkippedSlotCount;
            store.ChunkSkipDuePeriodSlotCount = snapshot.DuePeriodSlotCount;
            store.ChunkSkipNoopSlotCount = snapshot.NoopSlotCount;
            store.ChunkSkipReasonFlags = snapshot.ReasonFlags;
            return snapshot;
        }

        private static bool IsNoopForChunkSkip(BActiveEffectSlot slot, int currentFrame)
        {
            if (slot.State != EActiveEffectSlotState.Active
                && slot.State != EActiveEffectSlotState.Inhibited)
            {
                return true;
            }

            return !IsPeriodDue(slot, currentFrame) && !IsDurationDue(slot, currentFrame);
        }

        private static bool IsPeriodDue(BActiveEffectSlot slot, int currentFrame)
        {
            return slot.State == EActiveEffectSlotState.Active
                   && slot.PeriodFrame > 0
                   && currentFrame - slot.LastPeriodFrame >= slot.PeriodFrame;
        }

        private static bool IsDurationDue(BActiveEffectSlot slot, int currentFrame)
        {
            if (slot.DurationFrame <= 0
                || slot.RemainingFrame <= 0)
            {
                return false;
            }

            if (slot.State != EActiveEffectSlotState.Active
                && (slot.State != EActiveEffectSlotState.Inhibited
                    || !HasFlag(slot, EActiveEffectSlotFlags.TicksWhenInactive)))
            {
                return false;
            }

            return currentFrame - slot.StartFrame >= slot.RemainingFrame;
        }

        private static int ResolveDurationStartFrame(
            in CDurationRuntime duration,
            int currentFrame,
            int fallbackFrame)
        {
            if (duration.ActiveTime >= 0)
                return duration.ActiveTime;

            if (fallbackFrame >= 0)
                return fallbackFrame;

            return currentFrame;
        }

        public static bool TryRecordLifecycleCleanup(
            EntityManager em,
            Entity effectEntity,
            in CEffectContext context,
            EGameplayEffectLifecycleState cleanupState,
            int currentFrame)
        {
            return TryRecordLifecycleCleanup(
                em,
                effectEntity,
                context,
                cleanupState,
                currentFrame,
                out _);
        }

        public static bool TryRecordLifecycleCleanup(
            EntityManager em,
            Entity effectEntity,
            in CEffectContext context,
            EGameplayEffectLifecycleState cleanupState,
            int currentFrame,
            out int cleanupSequence)
        {
            return TryRecordLifecycleCleanup(
                em,
                effectEntity,
                context,
                cleanupState,
                currentFrame,
                requestedCleanupWorkFlags: 0,
                out cleanupSequence);
        }

        public static bool TryRecordLifecycleCleanup(
            EntityManager em,
            Entity effectEntity,
            in CEffectContext context,
            EGameplayEffectLifecycleState cleanupState,
            int currentFrame,
            int requestedCleanupWorkFlags,
            out int cleanupSequence)
        {
            if (!TryGetStore(em, context.TargetAsc, out var store, out var slots)
                || !em.HasBuffer<BActiveEffectCleanupRecord>(context.TargetAsc))
            {
                cleanupSequence = 0;
                return false;
            }

            var records = em.GetBuffer<BActiveEffectCleanupRecord>(context.TargetAsc);
            while (records.Length >= MaxCleanupRecordCount)
                records.RemoveAt(0);

            var slotIndex = FindSlot(slots, effectEntity);
            var hasSlot = slotIndex >= 0;
            var slot = hasSlot ? slots[slotIndex] : default;
            var activeGrantedTagCount = hasSlot ? slot.ActiveGrantedTagCount : CountActiveGrantedTags(em, context.TargetAsc, effectEntity);
            var activeGrantedAbilityCount = hasSlot ? slot.ActiveGrantedAbilityCount : CountActiveGrantedAbilities(em, effectEntity);
            var activeModifierCount = CountActiveModifiers(em, context.TargetAsc, effectEntity);
            var cleanupWorkFlags = requestedCleanupWorkFlags != 0
                ? requestedCleanupWorkFlags
                : CreateCleanupWorkFlags(
                    em,
                    effectEntity,
                    context,
                    cleanupState,
                    hasSlot,
                    activeGrantedTagCount,
                    activeGrantedAbilityCount,
                    activeModifierCount);
            var record = new BActiveEffectCleanupRecord
            {
                Sequence = Allocate(ref store.NextSequence),
                ActiveEffectEntity = effectEntity,
                SourceAsc = context.SourceAsc,
                TargetAsc = context.TargetAsc,
                SourceAbility = context.SourceAbility,
                SourceEffect = context.SourceEffect,
                Instigator = context.Instigator,
                Causer = context.Causer,
                ContextId = context.ContextId,
                ParentContextId = context.ParentContextId,
                CleanupFrame = currentFrame,
                CleanupState = cleanupState,
                SlotState = hasSlot ? slot.State : ToSlotState(cleanupState),
                PreviousSlotState = hasSlot ? slot.PreviousState : ToSlotState(cleanupState),
                DurationFrame = hasSlot ? slot.DurationFrame : ResolveDurationFrame(em, effectEntity),
                RemainingFrame = hasSlot ? slot.RemainingFrame : ResolveRemainingFrame(em, effectEntity),
                PeriodFrame = hasSlot ? slot.PeriodFrame : ResolvePeriodFrame(em, effectEntity),
                LastPeriodFrame = hasSlot ? slot.LastPeriodFrame : ResolveLastPeriodFrame(em, effectEntity),
                ActiveGrantedTagCount = activeGrantedTagCount,
                ActiveGrantedAbilityCount = activeGrantedAbilityCount,
                ActiveModifierCount = activeModifierCount,
                RequestedCleanupWorkFlags = cleanupWorkFlags,
                Flags = hasSlot ? slot.Flags : ResolveFlags(em, effectEntity),
            };

            if (TryGetSpec(em, effectEntity, out var spec))
            {
                record.GameplayEffectCode = spec.GameplayEffectCode;
                record.Level = spec.Level;
                record.StackCount = spec.StackCount > 0 ? spec.StackCount : 1;
            }

            if (hasSlot)
            {
                record.GameplayEffectCode = slot.GameplayEffectCode;
                record.Level = slot.Level;
                record.StackCount = slot.StackCount;
            }

            if (record.StackCount <= 0)
                record.StackCount = 1;

            records.Add(record);
            store.LastCleanupFrame = currentFrame;
            store.CleanupRecordCount = records.Length;
            em.SetComponentData(context.TargetAsc, store);
            cleanupSequence = record.Sequence;
            return true;
        }

        public static int CreateCleanupWorkFlags(
            EntityManager em,
            Entity effectEntity,
            in CEffectContext context,
            EGameplayEffectLifecycleState cleanupState)
        {
            var hasSlot = false;
            var activeGrantedTagCount = CountActiveGrantedTags(em, context.TargetAsc, effectEntity);
            var activeGrantedAbilityCount = CountActiveGrantedAbilities(em, effectEntity);
            if (TryGetStore(em, context.TargetAsc, out _, out var slots))
            {
                var slotIndex = FindSlot(slots, effectEntity);
                if (slotIndex >= 0)
                {
                    var slot = slots[slotIndex];
                    hasSlot = true;
                    activeGrantedTagCount = slot.ActiveGrantedTagCount;
                    activeGrantedAbilityCount = slot.ActiveGrantedAbilityCount;
                }
            }

            return CreateCleanupWorkFlags(
                em,
                effectEntity,
                context,
                cleanupState,
                hasSlot,
                activeGrantedTagCount,
                activeGrantedAbilityCount,
                CountActiveModifiers(em, context.TargetAsc, effectEntity));
        }

        public static int CreateCleanupWorkFlags(
            EntityManager em,
            Entity effectEntity,
            in CEffectContext context,
            EGameplayEffectLifecycleState cleanupState,
            bool hasSlot,
            int activeGrantedTagCount,
            int activeGrantedAbilityCount,
            int activeModifierCount)
        {
            var flags = EActiveEffectCleanupWorkFlags.None;
            if (hasSlot && (cleanupState == EGameplayEffectLifecycleState.Active
                || cleanupState == EGameplayEffectLifecycleState.Inhibited))
            {
                flags |= EActiveEffectCleanupWorkFlags.OwnerLocalSlot;
            }

            if (HasLegacyTargetBufferEntry(em, context.TargetAsc, effectEntity)
                && (cleanupState == EGameplayEffectLifecycleState.Active
                    || cleanupState == EGameplayEffectLifecycleState.Inhibited))
            {
                flags |= EActiveEffectCleanupWorkFlags.LegacyTargetBuffer;
            }

            if (cleanupState == EGameplayEffectLifecycleState.Active)
            {
                if (activeModifierCount > 0)
                    flags |= EActiveEffectCleanupWorkFlags.RuntimeModifiers;
                if (activeGrantedTagCount > 0)
                    flags |= EActiveEffectCleanupWorkFlags.GrantedTags;
                if (activeGrantedAbilityCount > 0)
                    flags |= EActiveEffectCleanupWorkFlags.GrantedAbilities;
            }

            if (em.Exists(effectEntity) && em.HasComponent<CDurationRuntime>(effectEntity))
                flags |= EActiveEffectCleanupWorkFlags.DurationRuntime;
            if (em.Exists(effectEntity))
                flags |= EActiveEffectCleanupWorkFlags.EntityDestroy;

            return (int)flags;
        }

        public static bool TryResolveLifecycleCleanup(
            EntityManager em,
            Entity owner,
            int cleanupSequence,
            int currentFrame)
        {
            if (owner == Entity.Null
                || cleanupSequence <= 0
                || !em.Exists(owner)
                || !em.HasComponent<CActiveEffectStore>(owner)
                || !em.HasBuffer<BActiveEffectCleanupRecord>(owner))
            {
                return false;
            }

            var records = em.GetBuffer<BActiveEffectCleanupRecord>(owner);
            for (var i = records.Length - 1; i >= 0; i--)
            {
                var record = records[i];
                if (record.Sequence != cleanupSequence)
                    continue;

                return TryResolveLifecycleCleanup(
                    em,
                    owner,
                    cleanupSequence,
                    record.RequestedCleanupWorkFlags,
                    currentFrame);
            }

            return false;
        }

        public static bool TryResolveLifecycleCleanupBeforeEntityDestroy(
            EntityManager em,
            Entity owner,
            int cleanupSequence,
            int currentFrame)
        {
            if (owner == Entity.Null
                || cleanupSequence <= 0
                || !em.Exists(owner)
                || !em.HasComponent<CActiveEffectStore>(owner)
                || !em.HasBuffer<BActiveEffectCleanupRecord>(owner))
            {
                return false;
            }

            var records = em.GetBuffer<BActiveEffectCleanupRecord>(owner);
            for (var i = records.Length - 1; i >= 0; i--)
            {
                var record = records[i];
                if (record.Sequence != cleanupSequence)
                    continue;

                var resolvedFlags = record.RequestedCleanupWorkFlags
                                    & ~(int)EActiveEffectCleanupWorkFlags.EntityDestroy;
                return TryResolveLifecycleCleanup(
                    em,
                    owner,
                    cleanupSequence,
                    resolvedFlags,
                    currentFrame);
            }

            return false;
        }

        public static bool TryResolveLifecycleCleanup(
            EntityManager em,
            Entity owner,
            int cleanupSequence,
            int resolvedCleanupWorkFlags,
            int currentFrame)
        {
            if (owner == Entity.Null
                || cleanupSequence <= 0
                || !em.Exists(owner)
                || !em.HasComponent<CActiveEffectStore>(owner)
                || !em.HasBuffer<BActiveEffectCleanupRecord>(owner))
            {
                return false;
            }

            var store = em.GetComponentData<CActiveEffectStore>(owner);
            var records = em.GetBuffer<BActiveEffectCleanupRecord>(owner);
            for (var i = records.Length - 1; i >= 0; i--)
            {
                var record = records[i];
                if (record.Sequence != cleanupSequence)
                    continue;

                record.ResolvedCleanupWorkFlags = resolvedCleanupWorkFlags;
                record.CleanupResolvedFrame = currentFrame;
                records[i] = record;
                store.CleanupRecordCount = records.Length;
                em.SetComponentData(owner, store);
                return true;
            }

            return false;
        }

        private static bool TryGetStore(
            EntityManager em,
            Entity owner,
            out CActiveEffectStore store,
            out DynamicBuffer<BActiveEffectSlot> slots)
        {
            store = default;
            slots = default;

            if (owner == Entity.Null
                || !em.Exists(owner)
                || !em.HasComponent<CActiveEffectStore>(owner)
                || !em.HasBuffer<BActiveEffectSlot>(owner))
            {
                return false;
            }

            store = em.GetComponentData<CActiveEffectStore>(owner);
            if (store.Version <= 0)
                store.Version = CurrentVersion;
            if (store.NextSequence <= 0)
                store.NextSequence = 1;

            slots = em.GetBuffer<BActiveEffectSlot>(owner);
            return true;
        }

        private static int FindSlot(DynamicBuffer<BActiveEffectSlot> slots, Entity effectEntity)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i].ActiveEffectEntity == effectEntity)
                    return i;
            }

            return -1;
        }

        private static bool TryResolveKnownGlobalIndexStore(EntityManager em, out Entity indexOwner)
        {
            if (TryResolveCachedGlobalIndexStore(em, out indexOwner))
                return true;

            if (TryResolveGasManagerGlobalIndexStore(em, out indexOwner))
                return true;

            indexOwner = Entity.Null;
            return false;
        }

        private static bool TryResolveCachedGlobalIndexStore(EntityManager em, out Entity indexOwner)
        {
            if (!_hasCachedGlobalIndexOwner || !_cachedGlobalIndexEntityManager.Equals(em))
            {
                indexOwner = Entity.Null;
                return false;
            }

            if (IsValidGlobalIndexStore(em, _cachedGlobalIndexEntity))
            {
                indexOwner = _cachedGlobalIndexEntity;
                return true;
            }

            _hasCachedGlobalIndexOwner = false;
            _cachedGlobalIndexEntity = Entity.Null;
            indexOwner = Entity.Null;
            return false;
        }

        private static bool TryResolveGasManagerGlobalIndexStore(EntityManager em, out Entity indexOwner)
        {
            if (!GASManager.IsInitialized || !GASManager.EntityManager.Equals(em))
            {
                indexOwner = Entity.Null;
                return false;
            }

            indexOwner = GASManager.EntityActiveEffectGlobalIndex;
            if (!IsValidGlobalIndexStore(em, indexOwner))
            {
                indexOwner = Entity.Null;
                return false;
            }

            RegisterKnownGlobalIndexStore(em, indexOwner);
            return true;
        }

        private static bool IsValidGlobalIndexStore(EntityManager em, Entity indexOwner)
        {
            return indexOwner != Entity.Null
                   && em.Exists(indexOwner)
                   && em.HasComponent<CActiveEffectGlobalIndexStore>(indexOwner)
                   && em.HasBuffer<BGlobalActiveEffectIndex>(indexOwner);
        }

        private static CActiveEffectGlobalIndexBucket CreateGlobalIndexBucketDefault(
            Entity rootOwner,
            int bucketIndex)
        {
            return new CActiveEffectGlobalIndexBucket
            {
                Version = CurrentVersion,
                RootOwner = rootOwner,
                BucketIndex = bucketIndex,
                BucketCount = GlobalIndexBucketCount,
            };
        }

        private static void EnsureGlobalIndexBucketOwners(EntityManager em, Entity indexOwner)
        {
            if (!IsValidGlobalIndexStore(em, indexOwner))
                return;

            EnsureGlobalIndexBuffers(em, indexOwner);
            var bucketOwners = em.GetBuffer<BGlobalActiveEffectIndexBucketOwner>(indexOwner);
            var resolvedBucketOwners = new NativeArray<Entity>(GlobalIndexBucketCount, Allocator.Temp);
            try
            {
                for (var bucketIndex = 0; bucketIndex < GlobalIndexBucketCount; bucketIndex++)
                {
                    var bucketOwner = FindValidGlobalIndexBucketOwner(em, indexOwner, bucketOwners, bucketIndex);
                    if (bucketOwner == Entity.Null)
                        bucketOwner = CreateGlobalIndexBucketOwner(em, indexOwner, bucketIndex);

                    resolvedBucketOwners[bucketIndex] = bucketOwner;
                }

                bucketOwners.Clear();
                for (var bucketIndex = 0; bucketIndex < GlobalIndexBucketCount; bucketIndex++)
                {
                    bucketOwners.Add(new BGlobalActiveEffectIndexBucketOwner
                    {
                        BucketIndex = bucketIndex,
                        BucketOwner = resolvedBucketOwners[bucketIndex],
                    });
                }
            }
            finally
            {
                resolvedBucketOwners.Dispose();
            }
        }

        private static Entity CreateGlobalIndexBucketOwner(
            EntityManager em,
            Entity indexOwner,
            int bucketIndex)
        {
            var bucketOwner = em.CreateEntity();
            em.SetName(bucketOwner, $"ActiveEffectGlobalIndexBucket_{bucketIndex}");
            em.AddComponentData(bucketOwner, CreateGlobalIndexBucketDefault(indexOwner, bucketIndex));
            em.AddBuffer<BGlobalActiveEffectIndex>(bucketOwner).EnsureCapacity(InlineGlobalIndexCapacity);
            return bucketOwner;
        }

        private static Entity FindValidGlobalIndexBucketOwner(
            EntityManager em,
            Entity indexOwner,
            DynamicBuffer<BGlobalActiveEffectIndexBucketOwner> bucketOwners,
            int bucketIndex)
        {
            for (var i = 0; i < bucketOwners.Length; i++)
            {
                var bucketOwner = bucketOwners[i];
                if (bucketOwner.BucketIndex == bucketIndex
                    && IsValidGlobalIndexBucket(em, indexOwner, bucketOwner.BucketOwner, bucketIndex))
                {
                    return bucketOwner.BucketOwner;
                }
            }

            return Entity.Null;
        }

        private static bool IsValidGlobalIndexBucket(
            EntityManager em,
            Entity indexOwner,
            Entity bucketOwner,
            int bucketIndex)
        {
            if (bucketOwner == Entity.Null
                || !em.Exists(bucketOwner)
                || !em.HasComponent<CActiveEffectGlobalIndexBucket>(bucketOwner)
                || !em.HasBuffer<BGlobalActiveEffectIndex>(bucketOwner))
            {
                return false;
            }

            var bucket = em.GetComponentData<CActiveEffectGlobalIndexBucket>(bucketOwner);
            return bucket.Version == CurrentVersion
                   && bucket.RootOwner == indexOwner
                   && bucket.BucketIndex == bucketIndex
                   && bucket.BucketCount == GlobalIndexBucketCount;
        }

        private static bool TryGetGlobalIndexBucketOwners(
            EntityManager em,
            Entity indexOwner,
            out DynamicBuffer<BGlobalActiveEffectIndexBucketOwner> bucketOwners)
        {
            bucketOwners = default;
            if (!IsValidGlobalIndexStore(em, indexOwner)
                || !em.HasBuffer<BGlobalActiveEffectIndexBucketOwner>(indexOwner))
            {
                return false;
            }

            bucketOwners = em.GetBuffer<BGlobalActiveEffectIndexBucketOwner>(indexOwner);
            return bucketOwners.Length >= GlobalIndexBucketCount;
        }

        private static bool TryGetGlobalIndexBucketOwner(
            EntityManager em,
            Entity indexOwner,
            int bucketIndex,
            out Entity bucketOwner)
        {
            bucketOwner = Entity.Null;
            if (bucketIndex < 0
                || bucketIndex >= GlobalIndexBucketCount
                || !TryGetGlobalIndexBucketOwners(em, indexOwner, out var bucketOwners))
            {
                return false;
            }

            bucketOwner = FindValidGlobalIndexBucketOwner(em, indexOwner, bucketOwners, bucketIndex);
            return bucketOwner != Entity.Null;
        }

        private static bool TryGetGlobalIndexBucket(
            EntityManager em,
            in BGlobalActiveEffectIndex entry,
            out Entity indexOwner,
            out Entity bucketOwner,
            out CActiveEffectGlobalIndexBucket bucket,
            out DynamicBuffer<BGlobalActiveEffectIndex> indices)
        {
            bucketOwner = Entity.Null;
            bucket = default;
            indices = default;
            if (!TryGetGlobalIndexStore(em, out indexOwner))
                return false;

            return TryGetGlobalIndexBucket(
                em,
                indexOwner,
                entry,
                out bucketOwner,
                out bucket,
                out indices);
        }

        private static bool TryGetGlobalIndexBucket(
            EntityManager em,
            Entity indexOwner,
            in BGlobalActiveEffectIndex entry,
            out Entity bucketOwner,
            out CActiveEffectGlobalIndexBucket bucket,
            out DynamicBuffer<BGlobalActiveEffectIndex> indices)
        {
            bucketOwner = Entity.Null;
            bucket = default;
            indices = default;

            var bucketIndex = ResolveGlobalIndexBucket(entry);
            if (!TryGetGlobalIndexBucketOwner(em, indexOwner, bucketIndex, out bucketOwner))
                return false;

            bucket = em.GetComponentData<CActiveEffectGlobalIndexBucket>(bucketOwner);
            indices = em.GetBuffer<BGlobalActiveEffectIndex>(bucketOwner);
            return true;
        }

        private static bool TryGetGlobalIndex(
            EntityManager em,
            out Entity indexOwner,
            out CActiveEffectGlobalIndexStore store,
            out DynamicBuffer<BGlobalActiveEffectIndex> indices)
        {
            store = default;
            indices = default;

            if (!TryGetGlobalIndexStore(em, out indexOwner))
                return false;

            store = em.GetComponentData<CActiveEffectGlobalIndexStore>(indexOwner);
            if (store.Version <= 0)
                store.Version = CurrentVersion;
            indices = em.GetBuffer<BGlobalActiveEffectIndex>(indexOwner);
            return true;
        }

        private static bool TrySyncGlobalIndexStableRow(
            EntityManager em,
            in BGlobalActiveEffectIndex entry,
            Entity indexOwner,
            Entity bucketOwner,
            int bucketIndex,
            int currentFrame)
        {
            var effectEntity = entry.ActiveEffectEntity;
            if (effectEntity == Entity.Null
                || !em.Exists(effectEntity)
                || !em.HasComponent<CActiveEffectGlobalIndexStableRow>(effectEntity))
            {
                return false;
            }

            var row = ToGlobalIndexStableRow(entry, indexOwner, bucketOwner, bucketIndex, currentFrame);
            em.SetComponentData(effectEntity, row);
            return true;
        }

        private static bool TryMarkGlobalIndexStableRowRemoved(
            EntityManager em,
            Entity effectEntity,
            int currentFrame)
        {
            if (effectEntity == Entity.Null
                || !em.Exists(effectEntity)
                || !em.HasComponent<CActiveEffectGlobalIndexStableRow>(effectEntity))
            {
                return false;
            }

            var row = em.GetComponentData<CActiveEffectGlobalIndexStableRow>(effectEntity);
            row.Version = CurrentVersion;
            row.IsIndexed = 0;
            row.LastCompactedFrame = currentFrame;
            row.IndexFlags &= ~(int)EActiveEffectGlobalIndexFlags.StableRowBacked;
            em.SetComponentData(effectEntity, row);
            return true;
        }

        private static CActiveEffectGlobalIndexStableRow ToGlobalIndexStableRow(
            in BGlobalActiveEffectIndex entry,
            Entity indexOwner,
            Entity bucketOwner,
            int bucketIndex,
            int currentFrame)
        {
            return new CActiveEffectGlobalIndexStableRow
            {
                Version = CurrentVersion,
                IsIndexed = 1,
                ActiveEffectEntity = entry.ActiveEffectEntity,
                RootOwner = indexOwner,
                BucketOwner = bucketOwner,
                BucketIndex = bucketIndex,
                Sequence = entry.Sequence,
                OwnerAsc = entry.OwnerAsc,
                SourceAsc = entry.SourceAsc,
                SourceAbility = entry.SourceAbility,
                SourceEffect = entry.SourceEffect,
                Instigator = entry.Instigator,
                Causer = entry.Causer,
                GameplayEffectCode = entry.GameplayEffectCode,
                Level = entry.Level,
                StackCount = entry.StackCount,
                State = entry.State,
                PreviousState = entry.PreviousState,
                StartFrame = entry.StartFrame,
                StateStartFrame = entry.StateStartFrame,
                DurationFrame = entry.DurationFrame,
                RemainingFrame = entry.RemainingFrame,
                DurationDueFrame = entry.DurationDueFrame,
                PeriodFrame = entry.PeriodFrame,
                LastPeriodFrame = entry.LastPeriodFrame,
                PeriodDueFrame = entry.PeriodDueFrame,
                ActiveGrantedTagCount = entry.ActiveGrantedTagCount,
                ActiveGrantedAbilityCount = entry.ActiveGrantedAbilityCount,
                SlotFlags = entry.SlotFlags,
                IndexFlags = entry.IndexFlags | (int)EActiveEffectGlobalIndexFlags.StableRowBacked,
                LastSyncedFrame = currentFrame,
            };
        }

        private static BGlobalActiveEffectIndex ResolveGlobalIndexStableRowFlag(
            EntityManager em,
            in BGlobalActiveEffectIndex entry)
        {
            if (entry.ActiveEffectEntity == Entity.Null
                || !em.Exists(entry.ActiveEffectEntity)
                || !em.HasComponent<CActiveEffectGlobalIndexStableRow>(entry.ActiveEffectEntity))
            {
                return entry;
            }

            var result = entry;
            result.IndexFlags |= (int)EActiveEffectGlobalIndexFlags.StableRowBacked;
            return result;
        }

        private static bool IsIndexedGlobalIndexStableRow(
            EntityManager em,
            in BGlobalActiveEffectIndex entry,
            Entity expectedRootOwner,
            int expectedBucketIndex)
        {
            if (entry.ActiveEffectEntity == Entity.Null
                || !em.Exists(entry.ActiveEffectEntity)
                || !em.HasComponent<CActiveEffectGlobalIndexStableRow>(entry.ActiveEffectEntity))
            {
                return false;
            }

            var row = em.GetComponentData<CActiveEffectGlobalIndexStableRow>(entry.ActiveEffectEntity);
            return row.Version == CurrentVersion
                   && row.IsIndexed != 0
                   && row.ActiveEffectEntity == entry.ActiveEffectEntity
                   && row.RootOwner == expectedRootOwner
                   && row.BucketIndex == expectedBucketIndex
                   && row.OwnerAsc == entry.OwnerAsc;
        }

        private static bool IsStaleGlobalIndexStableRow(
            EntityManager em,
            in BGlobalActiveEffectIndex entry,
            Entity expectedRootOwner,
            int expectedBucketIndex)
        {
            if (entry.ActiveEffectEntity == Entity.Null
                || !em.Exists(entry.ActiveEffectEntity)
                || !em.HasComponent<CActiveEffectGlobalIndexStableRow>(entry.ActiveEffectEntity))
            {
                return false;
            }

            var row = em.GetComponentData<CActiveEffectGlobalIndexStableRow>(entry.ActiveEffectEntity);
            if (row.IsIndexed == 0)
                return false;

            return row.Version != CurrentVersion
                   || row.ActiveEffectEntity != entry.ActiveEffectEntity
                   || row.RootOwner != expectedRootOwner
                   || row.BucketIndex != expectedBucketIndex
                   || row.OwnerAsc != entry.OwnerAsc
                   || row.OwnerAsc == Entity.Null
                   || !em.Exists(row.OwnerAsc);
        }

        private static int FindGlobalIndex(DynamicBuffer<BGlobalActiveEffectIndex> indices, Entity effectEntity)
        {
            for (var i = 0; i < indices.Length; i++)
            {
                if (indices[i].ActiveEffectEntity == effectEntity)
                    return i;
            }

            return -1;
        }

        private static void UpsertGlobalIndexEntry(
            DynamicBuffer<BGlobalActiveEffectIndex> indices,
            in BGlobalActiveEffectIndex entry)
        {
            var index = FindGlobalIndex(indices, entry.ActiveEffectEntity);
            if (index >= 0)
                indices[index] = entry;
            else
                indices.Add(entry);
        }

        private static void SortGlobalIndexEntries(DynamicBuffer<BGlobalActiveEffectIndex> indices)
        {
            for (var i = 1; i < indices.Length; i++)
            {
                var current = indices[i];
                var j = i - 1;
                while (j >= 0 && CompareGlobalIndexEntries(indices[j], current) > 0)
                {
                    indices[j + 1] = indices[j];
                    j--;
                }

                indices[j + 1] = current;
            }
        }

        private static int CompareGlobalIndexEntries(
            in BGlobalActiveEffectIndex left,
            in BGlobalActiveEffectIndex right)
        {
            var result = left.OwnerAsc.Index.CompareTo(right.OwnerAsc.Index);
            if (result != 0)
                return result;

            result = left.OwnerAsc.Version.CompareTo(right.OwnerAsc.Version);
            if (result != 0)
                return result;

            result = left.Sequence.CompareTo(right.Sequence);
            if (result != 0)
                return result;

            result = left.ActiveEffectEntity.Index.CompareTo(right.ActiveEffectEntity.Index);
            if (result != 0)
                return result;

            return left.ActiveEffectEntity.Version.CompareTo(right.ActiveEffectEntity.Version);
        }

        private static BGlobalActiveEffectIndex ToGlobalIndexEntry(in BActiveEffectSlot slot, int currentFrame)
        {
            return new BGlobalActiveEffectIndex
            {
                Sequence = slot.Sequence,
                ActiveEffectEntity = slot.ActiveEffectEntity,
                OwnerAsc = slot.TargetAsc,
                SourceAsc = slot.SourceAsc,
                SourceAbility = slot.SourceAbility,
                SourceEffect = slot.SourceEffect,
                Instigator = slot.Instigator,
                Causer = slot.Causer,
                GameplayEffectCode = slot.GameplayEffectCode,
                Level = slot.Level,
                StackCount = slot.StackCount,
                State = slot.State,
                PreviousState = slot.PreviousState,
                StartFrame = slot.StartFrame,
                StateStartFrame = slot.StateStartFrame,
                DurationFrame = slot.DurationFrame,
                RemainingFrame = slot.RemainingFrame,
                DurationDueFrame = ResolveDurationDueFrame(slot),
                PeriodFrame = slot.PeriodFrame,
                LastPeriodFrame = slot.LastPeriodFrame,
                PeriodDueFrame = ResolvePeriodDueFrame(slot),
                ActiveGrantedTagCount = slot.ActiveGrantedTagCount,
                ActiveGrantedAbilityCount = slot.ActiveGrantedAbilityCount,
                SlotFlags = slot.Flags,
                IndexFlags = ResolveGlobalIndexFlags(slot, currentFrame),
                LastSyncedFrame = currentFrame,
            };
        }

        private static int ResolveGlobalIndexFlags(in BActiveEffectSlot slot, int currentFrame)
        {
            var flags = EActiveEffectGlobalIndexFlags.OwnerLocalBacked;
            if (IsPeriodDue(slot, currentFrame))
                flags |= EActiveEffectGlobalIndexFlags.PeriodDue;
            if (IsDurationDue(slot, currentFrame))
                flags |= EActiveEffectGlobalIndexFlags.DurationDue;
            if (slot.State == EActiveEffectSlotState.Inhibited)
                flags |= EActiveEffectGlobalIndexFlags.Inhibited;
            if (slot.State == EActiveEffectSlotState.PendingRemove)
                flags |= EActiveEffectGlobalIndexFlags.PendingRemove;
            if (HasFlag(slot, EActiveEffectSlotFlags.LegacyEntityBacked))
                flags |= EActiveEffectGlobalIndexFlags.LegacyEntityBacked;

            return (int)flags;
        }

        private static int ResolveDurationDueFrame(in BActiveEffectSlot slot)
        {
            return slot.DurationFrame > 0 && slot.RemainingFrame > 0
                ? slot.StartFrame + slot.RemainingFrame
                : 0;
        }

        private static int ResolvePeriodDueFrame(in BActiveEffectSlot slot)
        {
            return slot.PeriodFrame > 0 && slot.LastPeriodFrame >= 0
                ? slot.LastPeriodFrame + slot.PeriodFrame
                : 0;
        }

        private static int ResolveGlobalIndexSyncFrame(EntityManager em, in CActiveEffectStore store)
        {
            return store.LastChunkSkipIndexFrame > 0
                ? store.LastChunkSkipIndexFrame
                : GASRuntimeFrameContext.ResolveCurrentFrame(em);
        }

        private static void RefreshAllGlobalIndexBuckets(
            EntityManager em,
            Entity indexOwner,
            int currentFrame,
            bool compactFrame)
        {
            if (!TryGetGlobalIndexBucketOwners(em, indexOwner, out var bucketOwners))
                return;

            for (var i = 0; i < bucketOwners.Length; i++)
            {
                var bucketOwner = bucketOwners[i].BucketOwner;
                if (!IsValidGlobalIndexBucket(em, indexOwner, bucketOwner, bucketOwners[i].BucketIndex))
                    continue;

                var bucket = em.GetComponentData<CActiveEffectGlobalIndexBucket>(bucketOwner);
                var indices = em.GetBuffer<BGlobalActiveEffectIndex>(bucketOwner);
                SortGlobalIndexEntries(indices);
                RefreshGlobalIndexBucketCounters(em, ref bucket, indices, currentFrame, compactFrame);
                em.SetComponentData(bucketOwner, bucket);
            }
        }

        private static void RefreshGlobalIndexRootFromBuckets(
            EntityManager em,
            Entity indexOwner,
            int currentFrame,
            bool compactFrame)
        {
            if (!TryGetGlobalIndexBucketOwners(em, indexOwner, out var bucketOwners)
                || !TryGetGlobalIndex(em, out _, out var store, out var rootIndices))
            {
                return;
            }

            rootIndices.Clear();
            for (var i = 0; i < bucketOwners.Length; i++)
            {
                var bucketOwner = bucketOwners[i].BucketOwner;
                if (!IsValidGlobalIndexBucket(em, indexOwner, bucketOwner, bucketOwners[i].BucketIndex))
                    continue;

                var bucketIndices = em.GetBuffer<BGlobalActiveEffectIndex>(bucketOwner);
                for (var j = 0; j < bucketIndices.Length; j++)
                    rootIndices.Add(bucketIndices[j]);
            }

            SortGlobalIndexEntries(rootIndices);
            RefreshGlobalIndexCounters(em, indexOwner, ref store, rootIndices, currentFrame, compactFrame);
            em.SetComponentData(indexOwner, store);
        }

        private static void RefreshGlobalIndexBucketCounters(
            EntityManager em,
            ref CActiveEffectGlobalIndexBucket bucket,
            DynamicBuffer<BGlobalActiveEffectIndex> indices,
            int currentFrame,
            bool compactFrame)
        {
            var activeCount = 0;
            var inhibitedCount = 0;
            var pendingRemoveCount = 0;
            var periodDueCount = 0;
            var durationDueCount = 0;
            var staleCount = 0;
            var indexedStableRowCount = 0;
            var staleStableRowCount = 0;

            for (var i = 0; i < indices.Length; i++)
            {
                var entry = indices[i];
                if (entry.ActiveEffectEntity == Entity.Null
                    || entry.OwnerAsc == Entity.Null
                    || !em.Exists(entry.ActiveEffectEntity)
                    || !em.Exists(entry.OwnerAsc))
                {
                    staleCount++;
                }

                switch (entry.State)
                {
                    case EActiveEffectSlotState.Active:
                        activeCount++;
                        break;
                    case EActiveEffectSlotState.Inhibited:
                        inhibitedCount++;
                        break;
                    case EActiveEffectSlotState.PendingRemove:
                        pendingRemoveCount++;
                        break;
                }

                if ((entry.IndexFlags & (int)EActiveEffectGlobalIndexFlags.PeriodDue) != 0)
                    periodDueCount++;
                if ((entry.IndexFlags & (int)EActiveEffectGlobalIndexFlags.DurationDue) != 0)
                    durationDueCount++;
                if (IsIndexedGlobalIndexStableRow(em, entry, bucket.RootOwner, bucket.BucketIndex))
                    indexedStableRowCount++;
                if (IsStaleGlobalIndexStableRow(em, entry, bucket.RootOwner, bucket.BucketIndex))
                    staleStableRowCount++;
            }

            bucket.Version = CurrentVersion;
            bucket.BucketCount = GlobalIndexBucketCount;
            bucket.LastSyncedFrame = currentFrame;
            if (compactFrame)
                bucket.LastCompactedFrame = currentFrame;
            bucket.IndexedEffectCount = indices.Length;
            bucket.ActiveCount = activeCount;
            bucket.InhibitedCount = inhibitedCount;
            bucket.PendingRemoveCount = pendingRemoveCount;
            bucket.PeriodDueCount = periodDueCount;
            bucket.DurationDueCount = durationDueCount;
            bucket.StaleCount = staleCount;
            bucket.IndexedStableRowCount = indexedStableRowCount;
            bucket.StaleStableRowCount = staleStableRowCount;
        }

        private static void RefreshGlobalIndexCounters(
            EntityManager em,
            Entity indexOwner,
            ref CActiveEffectGlobalIndexStore store,
            DynamicBuffer<BGlobalActiveEffectIndex> indices,
            int currentFrame,
            bool compactFrame)
        {
            var activeCount = 0;
            var inhibitedCount = 0;
            var pendingRemoveCount = 0;
            var periodDueCount = 0;
            var durationDueCount = 0;
            var staleCount = 0;
            var indexedStableRowCount = 0;
            var staleStableRowCount = 0;

            for (var i = 0; i < indices.Length; i++)
            {
                var entry = indices[i];
                if (entry.ActiveEffectEntity == Entity.Null
                    || entry.OwnerAsc == Entity.Null
                    || !em.Exists(entry.ActiveEffectEntity)
                    || !em.Exists(entry.OwnerAsc))
                {
                    staleCount++;
                }

                switch (entry.State)
                {
                    case EActiveEffectSlotState.Active:
                        activeCount++;
                        break;
                    case EActiveEffectSlotState.Inhibited:
                        inhibitedCount++;
                        break;
                    case EActiveEffectSlotState.PendingRemove:
                        pendingRemoveCount++;
                        break;
                }

                if ((entry.IndexFlags & (int)EActiveEffectGlobalIndexFlags.PeriodDue) != 0)
                    periodDueCount++;
                if ((entry.IndexFlags & (int)EActiveEffectGlobalIndexFlags.DurationDue) != 0)
                    durationDueCount++;
                if (IsIndexedGlobalIndexStableRow(em, entry, indexOwner, ResolveGlobalIndexBucket(entry)))
                    indexedStableRowCount++;
                if (IsStaleGlobalIndexStableRow(em, entry, indexOwner, ResolveGlobalIndexBucket(entry)))
                    staleStableRowCount++;
            }

            store.Version = CurrentVersion;
            store.LastSyncedFrame = currentFrame;
            if (compactFrame)
                store.LastCompactedFrame = currentFrame;
            store.IndexedEffectCount = indices.Length;
            store.ActiveCount = activeCount;
            store.InhibitedCount = inhibitedCount;
            store.PendingRemoveCount = pendingRemoveCount;
            store.PeriodDueCount = periodDueCount;
            store.DurationDueCount = durationDueCount;
            store.StaleCount = staleCount;
            store.IndexedStableRowCount = indexedStableRowCount;
            store.StaleStableRowCount = staleStableRowCount;
        }

        private static bool TryGetSpec(
            EntityManager em,
            Entity effectEntity,
            out CEffectSpecData spec)
        {
            if (em.Exists(effectEntity) && em.HasComponent<CEffectSpecData>(effectEntity))
            {
                spec = em.GetComponentData<CEffectSpecData>(effectEntity);
                return true;
            }

            spec = default;
            return false;
        }

        private static int ResolveStackCount(EntityManager em, Entity effectEntity)
        {
            if (em.Exists(effectEntity) && em.HasComponent<CStackingRuntime>(effectEntity))
                return em.GetComponentData<CStackingRuntime>(effectEntity).StackCount;

            return TryGetSpec(em, effectEntity, out var spec) && spec.StackCount > 0
                ? spec.StackCount
                : 1;
        }

        private static int ResolvePeriodFrame(EntityManager em, Entity effectEntity)
        {
            return em.Exists(effectEntity) && em.HasComponent<CPeriodDefinition>(effectEntity)
                ? em.GetComponentData<CPeriodDefinition>(effectEntity).Period
                : 0;
        }

        private static int ResolveDurationFrame(EntityManager em, Entity effectEntity)
        {
            return em.Exists(effectEntity) && em.HasComponent<CDurationRuntime>(effectEntity)
                ? em.GetComponentData<CDurationRuntime>(effectEntity).ResolvedDuration
                : 0;
        }

        private static int ResolveRemainingFrame(EntityManager em, Entity effectEntity)
        {
            return em.Exists(effectEntity) && em.HasComponent<CDurationRuntime>(effectEntity)
                ? em.GetComponentData<CDurationRuntime>(effectEntity).RemainingTime
                : 0;
        }

        private static int ResolveLastPeriodFrame(EntityManager em, Entity effectEntity)
        {
            return em.Exists(effectEntity) && em.HasComponent<CPeriodRuntime>(effectEntity)
                ? em.GetComponentData<CPeriodRuntime>(effectEntity).StartTime
                : 0;
        }

        private static int CountActiveGrantedAbilities(EntityManager em, Entity effectEntity)
        {
            if (!em.Exists(effectEntity) || !em.HasBuffer<BGrantedAbilityRuntime>(effectEntity))
                return 0;

            var runtimeAbilities = em.GetBuffer<BGrantedAbilityRuntime>(effectEntity);
            var count = 0;
            for (var i = 0; i < runtimeAbilities.Length; i++)
            {
                var ability = runtimeAbilities[i].AbilityEntity;
                if (ability != Entity.Null && em.Exists(ability))
                    count++;
            }

            return count;
        }

        private static int CountActiveGrantedTags(EntityManager em, Entity owner, Entity effectEntity)
        {
            if (owner == Entity.Null
                || effectEntity == Entity.Null
                || !em.Exists(owner)
                || !em.HasBuffer<BTempTagSource>(owner))
            {
                return 0;
            }

            var sources = em.GetBuffer<BTempTagSource>(owner);
            var seen = new CTagMask();
            var count = 0;
            for (var i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source.Source != effectEntity
                    || !CTagMask.IsValidIndex(source.TagIndex)
                    || seen.HasTag(source.TagIndex))
                {
                    continue;
                }

                seen.AddTag(source.TagIndex);
                count++;
            }

            return count;
        }

        private static int CountActiveModifiers(EntityManager em, Entity owner, Entity effectEntity)
        {
            if (owner == Entity.Null
                || effectEntity == Entity.Null
                || !em.Exists(owner)
                || !em.HasBuffer<BActiveModifier>(owner))
            {
                return 0;
            }

            var modifiers = em.GetBuffer<BActiveModifier>(owner);
            var count = 0;
            for (var i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i].SourceEntity == effectEntity)
                    count++;
            }

            return count;
        }

        private static bool HasLegacyTargetBufferEntry(EntityManager em, Entity owner, Entity effectEntity)
        {
            if (owner == Entity.Null
                || effectEntity == Entity.Null
                || !em.Exists(owner)
                || !em.HasBuffer<BGameplayEffect>(owner))
            {
                return false;
            }

            var effects = em.GetBuffer<BGameplayEffect>(owner);
            for (var i = 0; i < effects.Length; i++)
            {
                if (effects[i].GameplayEffect == effectEntity)
                    return true;
            }

            return false;
        }

        private static int ResolveFlags(EntityManager em, Entity effectEntity)
        {
            var flags = EActiveEffectSlotFlags.LegacyEntityBacked;

            if (!em.Exists(effectEntity))
                return (int)flags;

            if (em.HasComponent<CDurationDefinition>(effectEntity))
            {
                flags |= EActiveEffectSlotFlags.HasDuration;
                var durationDefinition = em.GetComponentData<CDurationDefinition>(effectEntity);
                if (!durationDefinition.StopTickWhenDeactivated)
                    flags |= EActiveEffectSlotFlags.TicksWhenInactive;
            }
            if (em.HasComponent<CPeriodDefinition>(effectEntity))
                flags |= EActiveEffectSlotFlags.HasPeriod;
            if (em.HasComponent<CStackingDefinition>(effectEntity))
                flags |= EActiveEffectSlotFlags.HasStacking;
            if (EffectRuntimeUtility.TryGetStaticDefinitionBlob(em, effectEntity, out var blob))
            {
                ref var definition = ref blob.Value;
                if (!definition.GrantedTags.IsEmpty)
                    flags |= EActiveEffectSlotFlags.HasGrantedTags;
                if (definition.GrantedAbilities.Length > 0)
                    flags |= EActiveEffectSlotFlags.HasGrantedAbilities;
                if (definition.HasOngoingRequiredTags)
                    flags |= EActiveEffectSlotFlags.HasOngoingRequirements;
            }

            return (int)flags;
        }

        private static bool HasFlag(in BActiveEffectSlot slot, EActiveEffectSlotFlags flag)
        {
            return (slot.Flags & (int)flag) != 0;
        }

        private static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;

            return next++;
        }

        private static EActiveEffectSlotState ToSlotState(EGameplayEffectLifecycleState state)
        {
            return state switch
            {
                EGameplayEffectLifecycleState.Active => EActiveEffectSlotState.Active,
                EGameplayEffectLifecycleState.Inhibited => EActiveEffectSlotState.Inhibited,
                EGameplayEffectLifecycleState.PendingRemove => EActiveEffectSlotState.PendingRemove,
                _ => EActiveEffectSlotState.PendingApply,
            };
        }
    }
}
