using Unity.Burst;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// ASC Entity 创建 System。处理 CRequestASCCreate 请求，
    /// 创建标准 ASC Entity（含 CTagMask / CFixedTagMask / BAttribute / BActiveModifier / BGrantedAbility / BTempTagSource）。
    /// 在 GASCommandGroup 中运行。
    /// </summary>
    [UpdateInGroup(typeof(GASCommandGroup))]
    [BurstCompile]
    public partial struct SASCCreate : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CRequestASCCreate>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (request, entity) in SystemAPI
                         .Query<CRequestASCCreate>()
                         .WithEntityAccess())
            {
                var asc = AbilitySystemEntityFactory.Create(ecb);

                // 将结果 ASC Entity 写回请求
                ecb.SetComponent(entity, new CRequestASCCreate
                {
                    ResultEntity = asc,
                });
                ecb.SetComponentEnabled<CRequestASCCreate>(entity, false);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
