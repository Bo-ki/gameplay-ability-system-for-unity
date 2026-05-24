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
    }

    public struct CActiveEffectStore : IComponentData
    {
        public int Version;
        public int NextSequence;
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
        public int Flags;
    }

    public static class ActiveEffectStore
    {
        public const int CurrentVersion = 1;
        public const int InlineSlotCapacity = 8;

        public static CActiveEffectStore CreateDefault()
        {
            return new CActiveEffectStore
            {
                Version = CurrentVersion,
                NextSequence = 1,
            };
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
                    StartFrame = currentFrame,
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
            slot.DurationFrame = duration.ResolvedDuration;
            slot.RemainingFrame = duration.RemainingTime;
            slot.StackCount = ResolveStackCount(em, effectEntity);
            slot.PeriodFrame = ResolvePeriodFrame(em, effectEntity);
            slot.LastPeriodFrame = ResolveLastPeriodFrame(em, effectEntity);
            if (slot.PeriodFrame > 0 && slot.LastPeriodFrame <= 0)
                slot.LastPeriodFrame = currentFrame;
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

            em.SetComponentData(context.TargetAsc, store);
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
            slot.DurationFrame = duration.ResolvedDuration;
            slot.RemainingFrame = duration.RemainingTime;
            slot.LastPeriodFrame = ResolveLastPeriodFrame(em, effectEntity);
            slots[index] = slot;
            em.SetComponentData(context.TargetAsc, store);
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
            em.SetComponentData(context.TargetAsc, store);
            return true;
        }

        public static bool TryRemove(
            EntityManager em,
            Entity effectEntity,
            in CEffectContext context)
        {
            if (!TryGetStore(em, context.TargetAsc, out _, out var slots))
                return false;

            var removed = false;
            for (var i = slots.Length - 1; i >= 0; i--)
            {
                if (slots[i].ActiveEffectEntity != effectEntity)
                    continue;

                slots.RemoveAt(i);
                removed = true;
            }

            return removed;
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

        private static int ResolveLastPeriodFrame(EntityManager em, Entity effectEntity)
        {
            return em.Exists(effectEntity) && em.HasComponent<CPeriodRuntime>(effectEntity)
                ? em.GetComponentData<CPeriodRuntime>(effectEntity).StartTime
                : 0;
        }

        private static int ResolveFlags(EntityManager em, Entity effectEntity)
        {
            var flags = EActiveEffectSlotFlags.LegacyEntityBacked;

            if (!em.Exists(effectEntity))
                return (int)flags;

            if (em.HasComponent<CDurationDefinition>(effectEntity))
                flags |= EActiveEffectSlotFlags.HasDuration;
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

        private static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;

            return next++;
        }
    }
}
