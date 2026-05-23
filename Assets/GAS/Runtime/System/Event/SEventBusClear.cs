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
            foreach (var presentationEvents in SystemAPI.Query<DynamicBuffer<BPresentationEvent>>())
                presentationEvents.Clear();

            foreach (var (_, damageEvents, tagEvents, gameplayEvents, attributeEvents, cueRequests) in SystemAPI
                         .Query<
                             RefRO<CGameplayEventBus>,
                             DynamicBuffer<BDamageEvent>,
                             DynamicBuffer<BTagChangeEvent>,
                             DynamicBuffer<BGameplayEvent>,
                             DynamicBuffer<BAttributeChangeEvent>,
                             DynamicBuffer<BCueRequest>>())
            {
                damageEvents.Clear();
                tagEvents.Clear();
                gameplayEvents.Clear();
                attributeEvents.Clear();
                cueRequests.Clear();
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
