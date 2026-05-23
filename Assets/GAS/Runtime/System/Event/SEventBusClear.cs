using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 清理上一帧事件总线数据。事件总线只保留最近一次 GAS tick 产生的事实事件。
    /// </summary>
    [UpdateInGroup(typeof(GASCommandGroup), OrderFirst = true)]
    public partial struct SEventBusClear : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CGameplayEventBus>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBus))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBus))
                return;

            if (em.HasBuffer<BPresentationOutboxOwner>(eventBus))
                ClearDirtyPresentationOutboxes(em, em.GetBuffer<BPresentationOutboxOwner>(eventBus));

            if (em.HasBuffer<BDamageEvent>(eventBus))
                em.GetBuffer<BDamageEvent>(eventBus).Clear();
            if (em.HasBuffer<BTagChangeEvent>(eventBus))
                em.GetBuffer<BTagChangeEvent>(eventBus).Clear();
            if (em.HasBuffer<BGameplayEvent>(eventBus))
                em.GetBuffer<BGameplayEvent>(eventBus).Clear();
            if (em.HasBuffer<BAttributeChangeEvent>(eventBus))
                em.GetBuffer<BAttributeChangeEvent>(eventBus).Clear();
            if (em.HasBuffer<BCueRequest>(eventBus))
                em.GetBuffer<BCueRequest>(eventBus).Clear();
        }

        private static void ClearDirtyPresentationOutboxes(
            EntityManager em,
            DynamicBuffer<BPresentationOutboxOwner> dirtyOutboxes)
        {
            for (var i = 0; i < dirtyOutboxes.Length; i++)
            {
                var asc = dirtyOutboxes[i].ASC;
                if (asc != Entity.Null && em.Exists(asc) && em.HasBuffer<BPresentationEvent>(asc))
                    em.GetBuffer<BPresentationEvent>(asc).Clear();
            }

            dirtyOutboxes.Clear();
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
