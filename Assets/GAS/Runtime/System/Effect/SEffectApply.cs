using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// GE Apply 入口。处理申请条件、Instant 修改，以及 Duration 激活。
    /// </summary>
    [UpdateInGroup(typeof(GASEffectGroup))]
    public partial struct SEffectApply : ISystem
    {
        private EntityQuery _effectQuery;

        public void OnCreate(ref SystemState state)
        {
            _effectQuery = SystemAPI.QueryBuilder()
                .WithAll<CEffectPendingApply, CEffectContext>()
                .WithNone<CEffectDestroy>()
                .WithNone<CEffectCleanup>()
                .WithNone<CEffectFinalDestroy>()
                .Build();
            state.RequireForUpdate(_effectQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var currentFrame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            var effects = _effectQuery.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var hasPendingStructuralChanges = false;

            foreach (var ge in effects)
            {
                if (!em.Exists(ge) || !em.HasComponent<CEffectContext>(ge))
                    continue;

                var context = em.GetComponentData<CEffectContext>(ge);
                if (em.HasComponent<CDurationDefinition>(ge))
                {
                    var hadDurationRuntime = em.HasComponent<CDurationRuntime>(ge);
                    var duration = GetOrCreateDurationRuntime(em, ref ecb, ge);
                    if (!hadDurationRuntime)
                        hasPendingStructuralChanges = true;

                    if (duration.Active || EffectRuntimeUtility.IsAppliedDurationEffect(em, ge, context))
                    {
                        hasPendingStructuralChanges |= RemovePendingApply(em, ref ecb, ge);
                        continue;
                    }

                    if (!EffectRuntimeUtility.IsPendingApply(em, ge))
                    {
                        hasPendingStructuralChanges |= RemovePendingApply(em, ref ecb, ge);
                        continue;
                    }

                    if (EffectRuntimeUtility.ShouldReject(em, context.TargetAsc, ge, out var durationRejection))
                    {
                        EffectRuntimeUtility.EnqueueApplicationRejectedEvent(em, ge, context, durationRejection);
                        EffectRuntimeUtility.DestroyEffectEntity(em, ref ecb, ge);
                        hasPendingStructuralChanges = true;
                        continue;
                    }

                    var removedActiveEffects = EffectRuntimeUtility.RemoveActiveGameplayEffectsWithTags(
                        em,
                        ref ecb,
                        ge,
                        context,
                        currentFrame);
                    if (removedActiveEffects || hasPendingStructuralChanges)
                    {
                        EffectRuntimeUtility.PlaybackAndReset(ref ecb, em);
                        hasPendingStructuralChanges = false;
                    }

                    if (EffectRuntimeUtility.TryMergeStackingApplication(em, ref ecb, ge, context, ref duration, currentFrame))
                    {
                        EffectRuntimeUtility.PlaybackAndReset(ref ecb, em);
                        hasPendingStructuralChanges = false;
                        continue;
                    }

                    if (EffectRuntimeUtility.MeetsOngoingRequirements(em, ge, context))
                        EffectRuntimeUtility.ActivateDurationEffect(em, ref ecb, ge, context, ref duration, currentFrame);
                    else
                        EffectRuntimeUtility.ApplyInactiveDurationEffect(em, ref ecb, ge, context, ref duration, currentFrame);

                    hasPendingStructuralChanges = true;
                    hasPendingStructuralChanges |= RemovePendingApply(em, ref ecb, ge);
                    ecb.SetComponent(ge, duration);
                    continue;
                }

                if (!EffectRuntimeUtility.IsPendingApply(em, ge))
                {
                    hasPendingStructuralChanges |= RemovePendingApply(em, ref ecb, ge);
                    continue;
                }

                if (EffectRuntimeUtility.ShouldReject(em, context.TargetAsc, ge, out var instantRejection))
                {
                    EffectRuntimeUtility.EnqueueApplicationRejectedEvent(em, ge, context, instantRejection);
                    EffectRuntimeUtility.DestroyEffectEntity(em, ref ecb, ge);
                    hasPendingStructuralChanges = true;
                    continue;
                }

                var removedEffects = EffectRuntimeUtility.RemoveActiveGameplayEffectsWithTags(
                    em,
                    ref ecb,
                    ge,
                    context,
                    currentFrame);
                if (removedEffects || hasPendingStructuralChanges)
                {
                    EffectRuntimeUtility.PlaybackAndReset(ref ecb, em);
                    hasPendingStructuralChanges = false;
                }

                EffectRuntimeUtility.ApplyInstantEffect(em, ge, context);
                EffectRuntimeUtility.DestroyEffectEntity(em, ref ecb, ge);
                hasPendingStructuralChanges = true;
            }

            ecb.Playback(em);
            GasRuntimeDebugger.RecordRuntimeCoreEcbPlayback(
                em,
                currentFrame,
                EGasRuntimeDiagnosticModule.Effect);
            ecb.Dispose();
            effects.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static CDurationRuntime GetOrCreateDurationRuntime(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge)
        {
            if (em.HasComponent<CDurationRuntime>(ge))
                return em.GetComponentData<CDurationRuntime>(ge);

            var definition = em.GetComponentData<CDurationDefinition>(ge);
            var runtime = new CDurationRuntime
            {
                ResolvedDuration = definition.Duration,
                ResolvedTimeUnit = definition.TimeUnit,
            };
            ecb.AddComponent(ge, runtime);
            return runtime;
        }

        private static bool RemovePendingApply(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge)
        {
            if (em.Exists(ge) && em.HasComponent<CEffectPendingApply>(ge))
            {
                ecb.RemoveComponent<CEffectPendingApply>(ge);
                return true;
            }

            return false;
        }

    }

}
