using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Converts try-activate markers into explicit commit requests.
    /// The gameplay commit gate lives in SAbilityCommit.
    /// </summary>
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateBefore(typeof(SAbilityCommit))]
    [BurstCompile]
    public partial struct STryActivateAbility : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CAbilityInTryActivate, CAbilityBaseInfo, CAbilityRuntimeState, CAbilityConfig>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var abilities = _query.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var ability in abilities)
            {
                if (!em.HasComponent<CAbilityCommitRequest>(ability))
                    ecb.AddComponent<CAbilityCommitRequest>(ability);

                if (em.HasComponent<CAbilityInTryActivate>(ability))
                    ecb.RemoveComponent<CAbilityInTryActivate>(ability);
            }

            ecb.Playback(em);
            GasRuntimeDebugger.RecordRuntimeCoreEcbPlayback(
                em,
                GasRuntimeDebugger.ResolveCurrentFrame(em),
                EGasRuntimeDiagnosticModule.Ability);
            ecb.Dispose();
            abilities.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
