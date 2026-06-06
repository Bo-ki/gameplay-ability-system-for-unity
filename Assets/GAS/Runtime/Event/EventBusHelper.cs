using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Boundary helpers for context allocation, presentation outbox appends, and tooling snapshots.
    /// Gameplay fact writes must go through typed fact producers instead of this facade.
    /// </summary>
    public static class EventBusHelper
    {
        public static int AllocateGameplayEffectContextId(
            EntityManager entityManager,
            Entity eventBusEntity)
        {
            if (eventBusEntity == Entity.Null
                || !entityManager.Exists(eventBusEntity)
                || !entityManager.HasComponent<GameplayEventBusComponent>(eventBusEntity))
            {
                return 0;
            }

            var eventBus = entityManager.GetComponentData<GameplayEventBusComponent>(eventBusEntity);
            var contextId = AllocateContextId(ref eventBus);
            entityManager.SetComponentData(eventBusEntity, eventBus);
            return contextId;
        }

        public static void AppendPresentationEvent(
            EntityManager entityManager,
            Entity eventBusEntity,
            Entity asc,
            PresentationEventBuffer evt)
        {
            if (asc == Entity.Null
                || !entityManager.Exists(asc)
                || !entityManager.HasBuffer<PresentationEventBuffer>(asc))
            {
                return;
            }

            var outbox = entityManager.GetBuffer<PresentationEventBuffer>(asc);
            if (outbox.Length == 0
                && eventBusEntity != Entity.Null
                && entityManager.Exists(eventBusEntity)
                && entityManager.HasBuffer<PresentationOutboxOwnerBuffer>(eventBusEntity))
            {
                entityManager.GetBuffer<PresentationOutboxOwnerBuffer>(eventBusEntity)
                    .Add(new PresentationOutboxOwnerBuffer { ASC = asc });
            }

            outbox.Add(evt);
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

        public static NativeArray<T> SnapshotBufferRange<T>(
            EntityManager entityManager,
            Entity eventBusEntity,
            int processedCount,
            Allocator allocator,
            out int eventCount)
            where T : unmanaged, IBufferElementData
        {
            eventCount = 0;
            if (eventBusEntity == Entity.Null
                || !entityManager.Exists(eventBusEntity)
                || !entityManager.HasBuffer<T>(eventBusEntity))
            {
                return new NativeArray<T>(0, allocator);
            }

            var source = entityManager.GetBuffer<T>(eventBusEntity);
            eventCount = source.Length;
            var start = ClampProcessedCount(source, processedCount);
            return CopyBufferRange(source, start, eventCount, allocator);
        }

        public static int ClampProcessedCount<T>(DynamicBuffer<T> source, int processedCount)
            where T : unmanaged, IBufferElementData
        {
            if (processedCount < 0)
                return 0;

            return processedCount > source.Length ? 0 : processedCount;
        }

        public static T ReadBufferElement<T>(
            EntityManager entityManager,
            Entity eventBusEntity,
            int index)
            where T : unmanaged, IBufferElementData
        {
            return entityManager.GetBuffer<T>(eventBusEntity)[index];
        }

        private static int AllocateContextId(ref GameplayEventBusComponent eventBus)
        {
            if (eventBus.NextContextId <= 0)
                eventBus.NextContextId = 1;

            var contextId = eventBus.NextContextId;
            eventBus.NextContextId++;
            return contextId;
        }
    }
}
