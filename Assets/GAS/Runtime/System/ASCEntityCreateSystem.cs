using Unity.Burst;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// ASC Entity 创建 System。处理 ASCCreateRequestComponent 请求，
    /// 创建标准 ASC Entity（含 TagMaskComponent / TagFixedMaskComponent / AttributeValueBuffer / AttributeActiveModifierBuffer / AbilitySlotBuffer / TagTemporarySourceBuffer）。
    /// 在 GASCommandResolveSystemGroup 中运行。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [BurstCompile]
    public partial struct ASCEntityCreateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ASCCreateRequestComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (request, entity) in SystemAPI
                         .Query<ASCCreateRequestComponent>()
                         .WithEntityAccess())
            {
                var asc = ASCEntityFactory.Create(ecb, state.EntityManager);

                // 将结果 ASC Entity 写回请求
                ecb.SetComponent(entity, new ASCCreateRequestComponent
                {
                    ResultEntity = asc,
                });
                ecb.SetComponentEnabled<ASCCreateRequestComponent>(entity, false);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
