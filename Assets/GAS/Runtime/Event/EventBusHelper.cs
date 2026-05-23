using System;
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
        [ThreadStatic] private static bool _gameplayEventBatchActive;
        [ThreadStatic] private static EntityManager _gameplayEventBatchEntityManager;
        [ThreadStatic] private static Entity _gameplayEventBatchEventBusEntity;
        [ThreadStatic] private static CGameplayEventBus _gameplayEventBatchEventBus;
        [ThreadStatic] private static int _gameplayEventBatchFrame;
        [ThreadStatic] private static bool _gameplayEventBatchCanAppend;
        [ThreadStatic] private static bool _attributeChangeEventBatchCanAppend;
        [ThreadStatic] private static bool _cueRequestBatchCanAppend;
        [ThreadStatic] private static bool _tagChangeEventBatchCanAppend;
        [ThreadStatic] private static bool _damageEventBatchCanAppend;

        public static void EnqueueGameplayEvent(
            EntityManager entityManager,
            Entity eventBusEntity,
            BGameplayEvent evt)
        {
            if (_gameplayEventBatchActive
                && _gameplayEventBatchEventBusEntity == eventBusEntity
                && _gameplayEventBatchEntityManager.Equals(entityManager))
            {
                evt.Frame = _gameplayEventBatchFrame;
                evt.Sequence = _gameplayEventBatchEventBus.NextSequence;
                _gameplayEventBatchEventBus.NextSequence++;
                if (_gameplayEventBatchCanAppend)
                    entityManager.GetBuffer<BGameplayEvent>(eventBusEntity).Add(evt);
                return;
            }

            if (!CanAppend<BGameplayEvent>(entityManager, eventBusEntity))
                return;

            evt.Frame = ResolveCurrentFrame(entityManager);
            evt.Sequence = AssignGameplayEventSequence(entityManager, eventBusEntity);
            entityManager.GetBuffer<BGameplayEvent>(eventBusEntity).Add(evt);
        }

        public static GameplayEventBatch BeginGameplayEventBatch(
            EntityManager entityManager,
            Entity eventBusEntity)
        {
            var previous = GameplayEventBatchState.CaptureCurrent();
            if (!CanAppend<BGameplayEvent>(entityManager, eventBusEntity))
                return new GameplayEventBatch(false, previous);

            _gameplayEventBatchActive = true;
            _gameplayEventBatchEntityManager = entityManager;
            _gameplayEventBatchEventBusEntity = eventBusEntity;
            _gameplayEventBatchCanAppend = true;
            _attributeChangeEventBatchCanAppend = entityManager.HasBuffer<BAttributeChangeEvent>(eventBusEntity);
            _cueRequestBatchCanAppend = entityManager.HasBuffer<BCueRequest>(eventBusEntity);
            _tagChangeEventBatchCanAppend = entityManager.HasBuffer<BTagChangeEvent>(eventBusEntity);
            _damageEventBatchCanAppend = entityManager.HasBuffer<BDamageEvent>(eventBusEntity);
            _gameplayEventBatchEventBus = entityManager.HasComponent<CGameplayEventBus>(eventBusEntity)
                ? entityManager.GetComponentData<CGameplayEventBus>(eventBusEntity)
                : default;
            _gameplayEventBatchFrame = ResolveCurrentFrame(entityManager);
            return new GameplayEventBatch(true, previous);
        }

        public static int AllocateGameplayEffectContextId(
            EntityManager entityManager,
            Entity eventBusEntity)
        {
            if (_gameplayEventBatchActive
                && _gameplayEventBatchEventBusEntity == eventBusEntity
                && _gameplayEventBatchEntityManager.Equals(entityManager))
            {
                return AllocateContextId(ref _gameplayEventBatchEventBus);
            }

            if (eventBusEntity == Entity.Null
                || !entityManager.Exists(eventBusEntity)
                || !entityManager.HasComponent<CGameplayEventBus>(eventBusEntity))
            {
                return 0;
            }

            var eventBus = entityManager.GetComponentData<CGameplayEventBus>(eventBusEntity);
            var contextId = AllocateContextId(ref eventBus);
            entityManager.SetComponentData(eventBusEntity, eventBus);
            return contextId;
        }

        public static void EnqueueAttributeChangeEvent(
            EntityManager entityManager,
            Entity eventBusEntity,
            BAttributeChangeEvent evt)
        {
            if (_gameplayEventBatchActive
                && _attributeChangeEventBatchCanAppend
                && _gameplayEventBatchEventBusEntity == eventBusEntity
                && _gameplayEventBatchEntityManager.Equals(entityManager))
            {
                entityManager.GetBuffer<BAttributeChangeEvent>(eventBusEntity).Add(evt);
                return;
            }

            if (!CanAppend<BAttributeChangeEvent>(entityManager, eventBusEntity))
                return;

            entityManager.GetBuffer<BAttributeChangeEvent>(eventBusEntity).Add(evt);
        }

        public static void EnqueueCueRequest(
            EntityManager entityManager,
            Entity eventBusEntity,
            BCueRequest evt)
        {
            if (_gameplayEventBatchActive
                && _cueRequestBatchCanAppend
                && _gameplayEventBatchEventBusEntity == eventBusEntity
                && _gameplayEventBatchEntityManager.Equals(entityManager))
            {
                entityManager.GetBuffer<BCueRequest>(eventBusEntity).Add(evt);
                return;
            }

            if (!CanAppend<BCueRequest>(entityManager, eventBusEntity))
                return;

            entityManager.GetBuffer<BCueRequest>(eventBusEntity).Add(evt);
        }

        public static void EnqueueTagChangeEvent(
            EntityManager entityManager,
            Entity eventBusEntity,
            BTagChangeEvent evt)
        {
            if (_gameplayEventBatchActive
                && _tagChangeEventBatchCanAppend
                && _gameplayEventBatchEventBusEntity == eventBusEntity
                && _gameplayEventBatchEntityManager.Equals(entityManager))
            {
                entityManager.GetBuffer<BTagChangeEvent>(eventBusEntity).Add(evt);
                return;
            }

            if (!CanAppend<BTagChangeEvent>(entityManager, eventBusEntity))
                return;

            entityManager.GetBuffer<BTagChangeEvent>(eventBusEntity).Add(evt);
        }

        public static void EnqueueDamageEvent(
            EntityManager entityManager,
            Entity eventBusEntity,
            BDamageEvent evt)
        {
            if (_gameplayEventBatchActive
                && _damageEventBatchCanAppend
                && _gameplayEventBatchEventBusEntity == eventBusEntity
                && _gameplayEventBatchEntityManager.Equals(entityManager))
            {
                entityManager.GetBuffer<BDamageEvent>(eventBusEntity).Add(evt);
                return;
            }

            if (!CanAppend<BDamageEvent>(entityManager, eventBusEntity))
                return;

            entityManager.GetBuffer<BDamageEvent>(eventBusEntity).Add(evt);
        }

        public static void AppendPresentationEvent(
            EntityManager entityManager,
            Entity eventBusEntity,
            Entity asc,
            in BPresentationEvent evt)
        {
            if (asc == Entity.Null
                || !entityManager.Exists(asc)
                || !entityManager.HasBuffer<BPresentationEvent>(asc))
            {
                return;
            }

            var outbox = entityManager.GetBuffer<BPresentationEvent>(asc);
            var wasEmpty = outbox.Length == 0;
            outbox.Add(evt);

            if (wasEmpty
                && eventBusEntity != Entity.Null
                && entityManager.Exists(eventBusEntity)
                && entityManager.HasBuffer<BPresentationOutboxOwner>(eventBusEntity))
            {
                entityManager.GetBuffer<BPresentationOutboxOwner>(eventBusEntity).Add(new BPresentationOutboxOwner
                {
                    ASC = asc,
                });
            }
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

        private static int AllocateContextId(ref CGameplayEventBus eventBus)
        {
            if (eventBus.NextContextId <= 0)
                eventBus.NextContextId = 1;

            var contextId = eventBus.NextContextId;
            eventBus.NextContextId++;
            return contextId;
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

        public readonly struct GameplayEventBatch : IDisposable
        {
            private readonly bool _active;
            private readonly GameplayEventBatchState _previous;

            internal GameplayEventBatch(
                bool active,
                GameplayEventBatchState previous)
            {
                _active = active;
                _previous = previous;
            }

            public void Dispose()
            {
                if (_active
                    && _gameplayEventBatchActive
                    && _gameplayEventBatchEventBusEntity != Entity.Null
                    && _gameplayEventBatchEntityManager.Exists(_gameplayEventBatchEventBusEntity)
                    && _gameplayEventBatchEntityManager.HasComponent<CGameplayEventBus>(_gameplayEventBatchEventBusEntity))
                {
                    _gameplayEventBatchEntityManager.SetComponentData(
                        _gameplayEventBatchEventBusEntity,
                        _gameplayEventBatchEventBus);
                }

                _previous.Restore();
            }
        }

        internal readonly struct GameplayEventBatchState
        {
            private readonly bool _active;
            private readonly EntityManager _entityManager;
            private readonly Entity _eventBusEntity;
            private readonly CGameplayEventBus _eventBus;
            private readonly int _frame;
            private readonly bool _gameplayEventCanAppend;
            private readonly bool _attributeChangeEventCanAppend;
            private readonly bool _cueRequestCanAppend;
            private readonly bool _tagChangeEventCanAppend;
            private readonly bool _damageEventCanAppend;

            private GameplayEventBatchState(
                bool active,
                EntityManager entityManager,
                Entity eventBusEntity,
                CGameplayEventBus eventBus,
                int frame,
                bool gameplayEventCanAppend,
                bool attributeChangeEventCanAppend,
                bool cueRequestCanAppend,
                bool tagChangeEventCanAppend,
                bool damageEventCanAppend)
            {
                _active = active;
                _entityManager = entityManager;
                _eventBusEntity = eventBusEntity;
                _eventBus = eventBus;
                _frame = frame;
                _gameplayEventCanAppend = gameplayEventCanAppend;
                _attributeChangeEventCanAppend = attributeChangeEventCanAppend;
                _cueRequestCanAppend = cueRequestCanAppend;
                _tagChangeEventCanAppend = tagChangeEventCanAppend;
                _damageEventCanAppend = damageEventCanAppend;
            }

            public static GameplayEventBatchState CaptureCurrent()
            {
                return new GameplayEventBatchState(
                    _gameplayEventBatchActive,
                    _gameplayEventBatchEntityManager,
                    _gameplayEventBatchEventBusEntity,
                    _gameplayEventBatchEventBus,
                    _gameplayEventBatchFrame,
                    _gameplayEventBatchCanAppend,
                    _attributeChangeEventBatchCanAppend,
                    _cueRequestBatchCanAppend,
                    _tagChangeEventBatchCanAppend,
                    _damageEventBatchCanAppend);
            }

            public void Restore()
            {
                _gameplayEventBatchActive = _active;
                _gameplayEventBatchEntityManager = _entityManager;
                _gameplayEventBatchEventBusEntity = _eventBusEntity;
                _gameplayEventBatchEventBus = _eventBus;
                _gameplayEventBatchFrame = _frame;
                _gameplayEventBatchCanAppend = _gameplayEventCanAppend;
                _attributeChangeEventBatchCanAppend = _attributeChangeEventCanAppend;
                _cueRequestBatchCanAppend = _cueRequestCanAppend;
                _tagChangeEventBatchCanAppend = _tagChangeEventCanAppend;
                _damageEventBatchCanAppend = _damageEventCanAppend;
            }
        }
    }
}
