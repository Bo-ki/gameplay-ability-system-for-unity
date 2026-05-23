using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// GE Tick。处理 Duration 计时、Period 子效果触发和自然过期。
    /// </summary>
    [UpdateInGroup(typeof(GASEffectGroup))]
    [UpdateAfter(typeof(SEffectRemove))]
    public partial struct SEffectTick : ISystem
    {
        private EntityQuery _activeDurationQuery;

        public void OnCreate(ref SystemState state)
        {
            _activeDurationQuery = SystemAPI.QueryBuilder()
                .WithAll<CEffectContext, CDurationDefinition, CDurationRuntime>()
                .WithNone<CEffectDestroy>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var currentFrame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            var effects = _activeDurationQuery.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var ge in effects)
            {
                if (!em.Exists(ge)
                    || !em.HasComponent<CEffectContext>(ge)
                    || !em.HasComponent<CDurationDefinition>(ge)
                    || !em.HasComponent<CDurationRuntime>(ge))
                    continue;

                var durationDefinition = em.GetComponentData<CDurationDefinition>(ge);
                var durationRuntime = em.GetComponentData<CDurationRuntime>(ge);
                var context = em.GetComponentData<CEffectContext>(ge);
                if (!EffectRuntimeUtility.IsAppliedDurationEffect(em, ge, context))
                    continue;

                if (!durationRuntime.Active)
                {
                    if (!durationDefinition.StopTickWhenDeactivated)
                        TickInactiveDuration(em, ref ecb, ge, context, ref durationRuntime, currentFrame);
                    continue;
                }

                TickPeriodEffects(em, ref ecb, ge, context, currentFrame);

                var elapsed = currentFrame - durationRuntime.ActiveTime;
                if (durationRuntime.ResolvedDuration > 0 && elapsed >= durationRuntime.ResolvedDuration)
                    EffectRuntimeUtility.HandleDurationExpired(em, ref ecb, ge, context, ref durationRuntime, currentFrame);
            }

            ecb.Playback(em);
            ecb.Dispose();
            effects.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void TickPeriodEffects(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            in CEffectContext context,
            int currentFrame)
        {
            if (!TryGetPeriodDefinition(em, ge, out var periodDefinition))
                return;

            if (periodDefinition.Period <= 0)
                return;

            var runtime = GetPeriodRuntime(em, ge);
            if (currentFrame - runtime.StartTime < periodDefinition.Period)
                return;

            if (!EffectRuntimeUtility.TryGetStaticDefinitionBlob(em, ge, out var blob))
                return;

            ref var staticDefinition = ref blob.Value;
            if (!staticDefinition.HasPeriod)
                return;

            for (var i = 0; i < staticDefinition.PeriodEffectCodes.Length; i++)
                EffectRuntimeUtility.CreateDerivedApplyRequest(
                    em,
                    ref ecb,
                    ge,
                    context,
                    staticDefinition.PeriodEffectCodes[i]);

            SetPeriodStartTime(em, ref ecb, ge, currentFrame);
        }

        private static bool TryGetPeriodDefinition(
            EntityManager em,
            Entity ge,
            out CPeriodDefinition definition)
        {
            if (em.HasComponent<CPeriodDefinition>(ge))
            {
                definition = em.GetComponentData<CPeriodDefinition>(ge);
                return true;
            }

            definition = default;
            return false;
        }

        private static CPeriodRuntime GetPeriodRuntime(EntityManager em, Entity ge)
        {
            if (em.HasComponent<CPeriodRuntime>(ge))
                return em.GetComponentData<CPeriodRuntime>(ge);

            return default;
        }

        private static void SetPeriodStartTime(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            int currentFrame)
        {
            if (em.HasComponent<CPeriodRuntime>(ge))
            {
                var runtime = em.GetComponentData<CPeriodRuntime>(ge);
                runtime.StartTime = currentFrame;
                ecb.SetComponent(ge, runtime);
            }
            else
            {
                ecb.AddComponent(ge, new CPeriodRuntime { StartTime = currentFrame });
            }
        }

        private static void TickInactiveDuration(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            in CEffectContext context,
            ref CDurationRuntime durationRuntime,
            int currentFrame)
        {
            if (durationRuntime.ResolvedDuration <= 0)
                return;

            var elapsed = currentFrame - durationRuntime.ActiveTime;
            if (elapsed >= durationRuntime.ResolvedDuration)
                EffectRuntimeUtility.HandleDurationExpired(em, ref ecb, ge, context, ref durationRuntime, currentFrame);
        }
    }
}
