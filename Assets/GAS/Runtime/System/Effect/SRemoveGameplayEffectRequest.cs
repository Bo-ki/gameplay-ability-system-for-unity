using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateBefore(typeof(SApplyGameplayEffectRequest))]
    public partial struct SRemoveGameplayEffectRequest : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CRemoveGameplayEffectRequest>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var requests = _query.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (var i = 0; i < requests.Length; i++)
            {
                var requestEntity = requests[i];
                var request = em.GetComponentData<CRemoveGameplayEffectRequest>(requestEntity);
                if (request.GameplayEffect != Entity.Null
                    && em.Exists(request.GameplayEffect)
                    && em.HasComponent<CEffectContext>(request.GameplayEffect)
                    && !em.HasComponent<CEffectDestroy>(request.GameplayEffect))
                {
                    ecb.AddComponent<CEffectDestroy>(request.GameplayEffect);
                }

                ecb.DestroyEntity(requestEntity);
            }

            ecb.Playback(em);
            GasRuntimeDebugger.RecordRuntimeCoreEcbPlayback(
                em,
                GasRuntimeDebugger.ResolveCurrentFrame(em),
                EGasRuntimeDiagnosticModule.Effect);
            ecb.Dispose();
            requests.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
