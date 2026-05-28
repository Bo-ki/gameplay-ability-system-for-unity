using System;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Legacy EventBus helpers. The singleton buffers are migration carriers;
    /// batched writes must use an explicit writer instead of ambient thread state.
    /// </summary>
    public static class EventBusHelper
    {
        public struct GameplayEventBusWriter : IDisposable
        {
            private EntityManager _entityManager;
            private Entity _eventBusEntity;
            private GameplayEventBusComponent _eventBus;
            private int _frame;
            private bool _isCreated;
            private bool _canWriteEventBus;
            private bool _eventBusDirty;
            private bool _gameplayEventCanAppend;
            private bool _attributeChangeEventCanAppend;
            private bool _cueRequestCanAppend;
            private bool _tagChangeEventCanAppend;
            private bool _damageEventCanAppend;

            private GameplayEventBusWriter(
                EntityManager entityManager,
                Entity eventBusEntity,
                GameplayEventBusComponent eventBus,
                int frame,
                bool canWriteEventBus,
                bool gameplayEventCanAppend,
                bool attributeChangeEventCanAppend,
                bool cueRequestCanAppend,
                bool tagChangeEventCanAppend,
                bool damageEventCanAppend)
            {
                _entityManager = entityManager;
                _eventBusEntity = eventBusEntity;
                _eventBus = eventBus;
                _frame = frame;
                _isCreated = true;
                _canWriteEventBus = canWriteEventBus;
                _eventBusDirty = false;
                _gameplayEventCanAppend = gameplayEventCanAppend;
                _attributeChangeEventCanAppend = attributeChangeEventCanAppend;
                _cueRequestCanAppend = cueRequestCanAppend;
                _tagChangeEventCanAppend = tagChangeEventCanAppend;
                _damageEventCanAppend = damageEventCanAppend;
            }

            public bool IsCreated => _isCreated;

            public int Frame => _frame;

            public static GameplayEventBusWriter Create(EntityManager entityManager, Entity eventBusEntity)
            {
                if (eventBusEntity == Entity.Null || !entityManager.Exists(eventBusEntity))
                    return default;

                var canWriteEventBus = entityManager.HasComponent<GameplayEventBusComponent>(eventBusEntity);
                var eventBus = canWriteEventBus
                    ? entityManager.GetComponentData<GameplayEventBusComponent>(eventBusEntity)
                    : default;
                var frame = GASRuntimeFrameContext.ResolveCurrentFrame(entityManager);

                return new GameplayEventBusWriter(
                    entityManager,
                    eventBusEntity,
                    eventBus,
                    frame,
                    canWriteEventBus,
                    entityManager.HasBuffer<GameplayEventBusEventBuffer>(eventBusEntity),
                    entityManager.HasBuffer<AttributeChangeEventBuffer>(eventBusEntity),
                    entityManager.HasBuffer<CueRequestBuffer>(eventBusEntity),
                    entityManager.HasBuffer<TagChangeEventBuffer>(eventBusEntity),
                    entityManager.HasBuffer<DamageEventBuffer>(eventBusEntity));
            }

            public int AllocateGameplayEffectContextId()
            {
                if (!_canWriteEventBus)
                    return 0;

                _eventBusDirty = true;
                return AllocateContextId(ref _eventBus);
            }

            public void EnqueueGameplayEvent(GameplayEventBusEventBuffer evt)
            {
                if (!_gameplayEventCanAppend)
                    return;

                evt.Frame = _frame;
                if (_canWriteEventBus)
                {
                    evt.Sequence = _eventBus.NextSequence;
                    _eventBus.NextSequence++;
                    _eventBusDirty = true;
                }
                else
                {
                    evt.Sequence = 0;
                }

                _entityManager.GetBuffer<GameplayEventBusEventBuffer>(_eventBusEntity).Add(evt);
            }

            public void EnqueueAttributeChangeEvent(AttributeChangeEventBuffer evt)
            {
                if (_attributeChangeEventCanAppend)
                    _entityManager.GetBuffer<AttributeChangeEventBuffer>(_eventBusEntity).Add(evt);
            }

            public void EnqueueCueRequest(CueRequestBuffer evt)
            {
                if (_cueRequestCanAppend)
                    _entityManager.GetBuffer<CueRequestBuffer>(_eventBusEntity).Add(evt);
            }

            public void EnqueueTagChangeEvent(TagChangeEventBuffer evt)
            {
                if (_tagChangeEventCanAppend)
                    _entityManager.GetBuffer<TagChangeEventBuffer>(_eventBusEntity).Add(evt);
            }

            public void EnqueueDamageEvent(DamageEventBuffer evt)
            {
                if (_damageEventCanAppend)
                    _entityManager.GetBuffer<DamageEventBuffer>(_eventBusEntity).Add(evt);
            }

            public void Flush()
            {
                if (!_isCreated || !_eventBusDirty || !_canWriteEventBus)
                    return;

                if (_eventBusEntity != Entity.Null
                    && _entityManager.Exists(_eventBusEntity)
                    && _entityManager.HasComponent<GameplayEventBusComponent>(_eventBusEntity))
                {
                    _entityManager.SetComponentData(_eventBusEntity, _eventBus);
                }

                _eventBusDirty = false;
            }

            public void Dispose()
            {
                Flush();
            }
        }

        public static GameplayEventBusWriter BeginGameplayEventBatch(
            EntityManager entityManager,
            Entity eventBusEntity)
        {
            return GameplayEventBusWriter.Create(entityManager, eventBusEntity);
        }

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

        public static int AllocateGameplayEffectContextId(ref GameplayEventBusWriter writer)
        {
            return writer.AllocateGameplayEffectContextId();
        }

        public static void EnqueueGameplayEvent(
            EntityManager entityManager,
            Entity eventBusEntity,
            GameplayEventBusEventBuffer evt)
        {
            if (!CanAppend<GameplayEventBusEventBuffer>(entityManager, eventBusEntity))
                return;

            evt.Frame = GASRuntimeFrameContext.ResolveCurrentFrame(entityManager);
            evt.Sequence = AssignGameplayEventSequence(entityManager, eventBusEntity);
            entityManager.GetBuffer<GameplayEventBusEventBuffer>(eventBusEntity).Add(evt);
        }

        public static void EnqueueAttributeChangeEvent(
            EntityManager entityManager,
            Entity eventBusEntity,
            AttributeChangeEventBuffer evt)
        {
            if (CanAppend<AttributeChangeEventBuffer>(entityManager, eventBusEntity))
                entityManager.GetBuffer<AttributeChangeEventBuffer>(eventBusEntity).Add(evt);
        }

        public static void EnqueueCueRequest(
            EntityManager entityManager,
            Entity eventBusEntity,
            CueRequestBuffer evt)
        {
            if (CanAppend<CueRequestBuffer>(entityManager, eventBusEntity))
                entityManager.GetBuffer<CueRequestBuffer>(eventBusEntity).Add(evt);
        }

        public static void EnqueueTagChangeEvent(
            EntityManager entityManager,
            Entity eventBusEntity,
            TagChangeEventBuffer evt)
        {
            if (CanAppend<TagChangeEventBuffer>(entityManager, eventBusEntity))
                entityManager.GetBuffer<TagChangeEventBuffer>(eventBusEntity).Add(evt);
        }

        public static void EnqueueDamageEvent(
            EntityManager entityManager,
            Entity eventBusEntity,
            DamageEventBuffer evt)
        {
            if (CanAppend<DamageEventBuffer>(entityManager, eventBusEntity))
                entityManager.GetBuffer<DamageEventBuffer>(eventBusEntity).Add(evt);
        }

        public static void AppendPresentationEvent(
            EntityManager entityManager,
            Entity eventBusEntity,
            Entity asc,
            PresentationEventBuffer evt)
        {
            if (eventBusEntity != Entity.Null
                && entityManager.Exists(eventBusEntity)
                && entityManager.HasBuffer<PresentationOutboxOwnerBuffer>(eventBusEntity))
            {
                var owners = entityManager.GetBuffer<PresentationOutboxOwnerBuffer>(eventBusEntity);
                var alreadyOwned = false;
                for (var i = 0; i < owners.Length; i++)
                {
                    if (owners[i].ASC != asc)
                        continue;

                    alreadyOwned = true;
                    break;
                }

                if (!alreadyOwned)
                    owners.Add(new PresentationOutboxOwnerBuffer { ASC = asc });
            }

            if (asc != Entity.Null && entityManager.Exists(asc) && entityManager.HasBuffer<PresentationEventBuffer>(asc))
                entityManager.GetBuffer<PresentationEventBuffer>(asc).Add(evt);
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

        public static void EnqueueDamageEvent(
            this EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity eventBusEntity,
            Entity target,
            Entity source,
            float amount)
        {
            ecb.AppendToBuffer(sortKey, eventBusEntity, new DamageEventBuffer
            {
                Target = target,
                Source = source,
                Amount = amount,
            });
        }

        public static void EnqueueTagChangeEvent(
            this EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity eventBusEntity,
            Entity asc,
            int tagIndex,
            bool added)
        {
            ecb.AppendToBuffer(sortKey, eventBusEntity, new TagChangeEventBuffer
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
            if (!entityManager.HasComponent<GameplayEventBusComponent>(eventBusEntity))
                return 0;

            var eventBus = entityManager.GetComponentData<GameplayEventBusComponent>(eventBusEntity);
            var sequence = eventBus.NextSequence;
            eventBus.NextSequence++;
            entityManager.SetComponentData(eventBusEntity, eventBus);
            return sequence;
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
