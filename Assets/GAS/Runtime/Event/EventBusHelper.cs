using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 事件总线入队工具。通过 ECB 写入事件总线 Singleton 的 Buffer。
    /// 在 Burst 并行 Job 中使用 AsParallelWriter 安全入队。
    /// </summary>
    public static class EventBusHelper
    {
        public static void EnqueueGameplayEvent(
            EntityManager entityManager,
            Entity eventBusEntity,
            BGameplayEvent evt)
        {
            if (!CanAppend<BGameplayEvent>(entityManager, eventBusEntity))
                return;

            evt.Frame = ResolveCurrentFrame(entityManager);
            evt.Sequence = AssignGameplayEventSequence(entityManager, eventBusEntity);
            entityManager.GetBuffer<BGameplayEvent>(eventBusEntity).Add(evt);
        }

        public static void EnqueueAttributeChangeEvent(
            EntityManager entityManager,
            Entity eventBusEntity,
            BAttributeChangeEvent evt)
        {
            if (!CanAppend<BAttributeChangeEvent>(entityManager, eventBusEntity))
                return;

            entityManager.GetBuffer<BAttributeChangeEvent>(eventBusEntity).Add(evt);
        }

        public static void EnqueueCueRequest(
            EntityManager entityManager,
            Entity eventBusEntity,
            BCueRequest evt)
        {
            if (!CanAppend<BCueRequest>(entityManager, eventBusEntity))
                return;

            entityManager.GetBuffer<BCueRequest>(eventBusEntity).Add(evt);
        }

        public static void EnqueueTagChangeEvent(
            EntityManager entityManager,
            Entity eventBusEntity,
            BTagChangeEvent evt)
        {
            if (!CanAppend<BTagChangeEvent>(entityManager, eventBusEntity))
                return;

            entityManager.GetBuffer<BTagChangeEvent>(eventBusEntity).Add(evt);
        }

        public static void EnqueueDamageEvent(
            EntityManager entityManager,
            Entity eventBusEntity,
            BDamageEvent evt)
        {
            if (!CanAppend<BDamageEvent>(entityManager, eventBusEntity))
                return;

            entityManager.GetBuffer<BDamageEvent>(eventBusEntity).Add(evt);
        }

        public static NativeArray<T> CopyBufferRange<T>(
            DynamicBuffer<T> source,
            int startInclusive,
            int endExclusive,
            Allocator allocator)
            where T : unmanaged, IBufferElementData
        {
            var start = startInclusive;
            if (start < 0)
                start = 0;
            else if (start > source.Length)
                start = source.Length;

            var end = endExclusive;
            if (end < start)
                end = start;
            else if (end > source.Length)
                end = source.Length;

            var snapshot = new NativeArray<T>(end - start, allocator);
            for (var i = 0; i < snapshot.Length; i++)
                snapshot[i] = source[start + i];

            return snapshot;
        }

        /// <summary>
        /// 通过 ECB 入队伤害事件到事件总线。
        /// </summary>
        public static void EnqueueDamageEvent(
            this EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity eventBusEntity,
            Entity target,
            Entity source,
            float amount)
        {
            ecb.AppendToBuffer(sortKey, eventBusEntity, new BDamageEvent
            {
                Target = target,
                Source = source,
                Amount = amount,
            });
        }

        /// <summary>
        /// 通过 ECB 入队 Tag 变更事件到事件总线。
        /// </summary>
        public static void EnqueueTagChangeEvent(
            this EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity eventBusEntity,
            Entity asc,
            int tagIndex,
            bool added)
        {
            ecb.AppendToBuffer(sortKey, eventBusEntity, new BTagChangeEvent
            {
                ASC = asc,
                TagIndex = tagIndex,
                Added = added,
            });
        }

        private static bool CanAppend<T>(EntityManager entityManager, Entity eventBusEntity)
            where T : unmanaged, IBufferElementData
        {
            return eventBusEntity != Entity.Null
                   && entityManager.Exists(eventBusEntity)
                   && entityManager.HasBuffer<T>(eventBusEntity);
        }

        private static int AssignGameplayEventSequence(EntityManager entityManager, Entity eventBusEntity)
        {
            if (!entityManager.HasComponent<CGameplayEventBus>(eventBusEntity))
                return 0;

            var eventBus = entityManager.GetComponentData<CGameplayEventBus>(eventBusEntity);
            var sequence = eventBus.NextSequence;
            eventBus.NextSequence++;
            entityManager.SetComponentData(eventBusEntity, eventBus);
            return sequence;
        }

        private static int ResolveCurrentFrame(EntityManager entityManager)
        {
            var globalTimer = GASManager.EntityGlobalTimer;
            if (globalTimer != Entity.Null
                && entityManager.Exists(globalTimer)
                && entityManager.HasComponent<GlobalTimer>(globalTimer))
            {
                return entityManager.GetComponentData<GlobalTimer>(globalTimer).Frame;
            }

            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GlobalTimer>());
            if (!query.IsEmptyIgnoreFilter)
                return query.GetSingleton<GlobalTimer>().Frame;

            return 0;
        }
    }
}
