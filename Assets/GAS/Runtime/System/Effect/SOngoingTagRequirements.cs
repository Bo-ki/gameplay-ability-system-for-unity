using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 持续评估 active GE 的 OngoingRequiredTags。
    /// 条件不满足时只关闭 GE 的运行时贡献，不从 active effect 容器移除。
    /// </summary>
    [UpdateInGroup(typeof(GASEffectGroup))]
    [UpdateAfter(typeof(SEffectApply))]
    [UpdateBefore(typeof(SEffectRemove))]
    public partial struct SOngoingTagRequirements : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CEffectContext, CDurationDefinition, CDurationRuntime>()
                .WithNone<CEffectDestroy>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var currentFrame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            var effects = _query.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            using var gameplayEventBatch = EventBusHelper.BeginGameplayEventBatch(em, GASManager.EntityEventBus);

            for (var i = 0; i < effects.Length; i++)
            {
                var ge = effects[i];
                if (!em.Exists(ge)
                    || !em.HasComponent<CEffectContext>(ge)
                    || !em.HasComponent<CDurationDefinition>(ge)
                    || !em.HasComponent<CDurationRuntime>(ge))
                    continue;

                if (!EffectRuntimeUtility.HasOngoingRequirements(em, ge))
                    continue;

                var context = em.GetComponentData<CEffectContext>(ge);
                if (!EffectRuntimeUtility.IsAppliedDurationEffect(em, ge, context))
                    continue;

                var duration = em.GetComponentData<CDurationRuntime>(ge);
                var meetsRequirements = EffectRuntimeUtility.MeetsOngoingRequirements(em, ge, context);
                if (duration.Active && !meetsRequirements)
                    EffectRuntimeUtility.DeactivateOngoingEffect(em, ref ecb, ge, currentFrame);
                else if (!duration.Active && meetsRequirements)
                    EffectRuntimeUtility.ReactivateOngoingEffect(em, ref ecb, ge, currentFrame);
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
