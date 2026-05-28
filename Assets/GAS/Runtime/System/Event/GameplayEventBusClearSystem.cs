using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 清理上一帧事件总线数据。事件总线只保留最近一次 GAS tick 产生的事实事件。
    /// </summary>
    [UpdateInGroup(typeof(GASFramePrepareSystemGroup), OrderFirst = true)]
    public partial struct GameplayEventBusClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayEventBusComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var eventBus))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBus))
                return;

            if (em.HasBuffer<PresentationOutboxOwnerBuffer>(eventBus))
                ClearDirtyPresentationOutboxes(em, em.GetBuffer<PresentationOutboxOwnerBuffer>(eventBus));

            if (em.HasBuffer<DamageEventBuffer>(eventBus))
                em.GetBuffer<DamageEventBuffer>(eventBus).Clear();
            if (em.HasBuffer<TagChangeEventBuffer>(eventBus))
                em.GetBuffer<TagChangeEventBuffer>(eventBus).Clear();
            if (em.HasBuffer<GameplayEventBusEventBuffer>(eventBus))
                em.GetBuffer<GameplayEventBusEventBuffer>(eventBus).Clear();
            if (em.HasBuffer<AttributeChangeEventBuffer>(eventBus))
                em.GetBuffer<AttributeChangeEventBuffer>(eventBus).Clear();
            if (em.HasBuffer<CueRequestBuffer>(eventBus))
                em.GetBuffer<CueRequestBuffer>(eventBus).Clear();
        }

        private static void ClearDirtyPresentationOutboxes(
            EntityManager em,
            DynamicBuffer<PresentationOutboxOwnerBuffer> dirtyOutboxes)
        {
            for (var i = 0; i < dirtyOutboxes.Length; i++)
            {
                var asc = dirtyOutboxes[i].ASC;
                if (asc != Entity.Null && em.Exists(asc) && em.HasBuffer<PresentationEventBuffer>(asc))
                    em.GetBuffer<PresentationEventBuffer>(asc).Clear();
            }

            dirtyOutboxes.Clear();
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
