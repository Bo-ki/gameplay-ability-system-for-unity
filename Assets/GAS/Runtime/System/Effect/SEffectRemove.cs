using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// GE Remove 入口。消费 CEffectDestroy，清理目标 ASC 上的运行时状态。
    /// </summary>
    [UpdateInGroup(typeof(GASEffectGroup))]
    [UpdateAfter(typeof(SEffectApply))]
    [UpdateBefore(typeof(SEffectTick))]
    public partial struct SEffectRemove : ISystem
    {
        private EntityQuery _removeQuery;

        public void OnCreate(ref SystemState state)
        {
            _removeQuery = SystemAPI.QueryBuilder()
                .WithAll<CEffectDestroy>()
                .Build();
            state.RequireForUpdate(_removeQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var effects = _removeQuery.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            using var gameplayEventBatch = EventBusHelper.BeginGameplayEventBatch(em, GASManager.EntityEventBus);

            foreach (var ge in effects)
            {
                if (!em.Exists(ge))
                    continue;

                if (em.HasComponent<CEffectContext>(ge))
                    EffectRuntimeUtility.CleanupActiveEffect(em, ref ecb, ge);
                else
                    EffectRuntimeUtility.DestroyEffectEntity(em, ref ecb, ge);
            }

            ecb.Playback(em);
            ecb.Dispose();
            effects.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
