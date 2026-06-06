using Unity.Burst;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Burst 兼容的 Ability 状态推进系统。
    /// 本 System 只负责通用逻辑：运行时状态更新、计时推进。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [BurstCompile]
    public partial struct AbilityStateTickSystem : ISystem
    {
        private EntityQuery _activeQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _activeQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<AbilityStateComponent>(),
                },
            });
            state.RequireForUpdate(_activeQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new AbilityStateTickJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(_activeQuery, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private partial struct AbilityStateTickJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(ref AbilityStateComponent runtime)
            {
                if (runtime.Phase != EAbilityPhase.Active && runtime.Phase != EAbilityPhase.Activating)
                    return;

                runtime.Timer += DeltaTime;

                // 激活阶段转为活跃
                if (runtime.Phase == EAbilityPhase.Activating)
                    runtime.Phase = EAbilityPhase.Active;

                if (runtime.RemainingFrame > 0)
                {
                    runtime.RemainingFrame--;
                    if (runtime.RemainingFrame == 0)
                        runtime.Phase = EAbilityPhase.Ending;
                }
            }
        }
    }
}
