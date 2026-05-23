using Unity.Burst;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Burst 兼容的 Ability Tick System。按 Ability 类型分派到各自的 ISystem 处理。
    /// 本 System 只负责通用逻辑：运行时状态更新、计时推进。
    /// </summary>
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [BurstCompile]
    public partial struct SAbilityTick : ISystem
    {
        private EntityQuery _activeQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _activeQuery = SystemAPI.QueryBuilder()
                .WithAll<CAbilityActive, CAbilityBaseInfo, CAbilityRuntimeState>()
                .Build();
            state.RequireForUpdate(_activeQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new TickJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(_activeQuery, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private partial struct TickJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(ref CAbilityRuntimeState runtime)
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
