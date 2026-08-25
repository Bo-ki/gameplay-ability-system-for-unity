using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Systems;

namespace GAS.Runtime
{
    /// <summary>
    /// 定义 Runtime v1 在主 Physics 后、标准 EndFixed 前运行的唯一固定步执行域；由 World owner 显式安装。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(EndFixedStepSimulationEntityCommandBufferSystem))]
    public partial class GasFixedTickSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 拥有 Runtime v1 唯一 Tick DAG；Stage B 仅实现 SpawnFinalize maintenance，gameplay lanes 保持显式空操作。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GasFixedTickSystemGroup))]
    public partial struct GasTickKernelSystem : ISystem
    {
        private EntityQuery _sessionQuery;
        private EntityQuery _spawnBatchMarkerQuery;
        private GasStageBSpawnFinalizeJob _spawnFinalizeTemplate;

        /// <summary>
        /// 创建当前 World-local Session query，不建立静态或跨 World 缓存。
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            _sessionQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<GasActiveSessionAuthority>());
            _spawnBatchMarkerQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<GasSpawnBatchMarker>());
            _spawnFinalizeTemplate.InitializeLookups(ref state);
            state.RequireForUpdate<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        }

        /// <summary>
        /// 先执行单 Session fail-closed，再按 lifecycle 仅分派 SpawnFinalize 或 Stage-B no-op lane。
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            var sessionCount = _sessionQuery.CalculateEntityCount();
            if (sessionCount == 0)
                return;

            _spawnFinalizeTemplate.UpdateLookups(ref state);
            var endFixed = SystemAPI.GetSingleton<
                EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            var commandBuffer = endFixed.CreateCommandBuffer(state.WorldUnmanaged);
            var batchMarkedAscs = _spawnBatchMarkerQuery.ToEntityArray(Allocator.TempJob);
            if (sessionCount != 1)
            {
                var sessions = _sessionQuery.ToEntityArray(Allocator.TempJob);
                state.Dependency = GasStageBSpawnFinalize.FailSessionCardinality(
                    ref _spawnFinalizeTemplate,
                    sessions,
                    batchMarkedAscs,
                    commandBuffer,
                    state.Dependency);
                return;
            }

            var session = _sessionQuery.GetSingletonEntity();
            state.Dependency = GasStageBSpawnFinalize.Run(
                ref _spawnFinalizeTemplate,
                session,
                batchMarkedAscs,
                commandBuffer,
                state.Dependency);

            // Stage B 的 Ready/Running admission 与 gameplay DAG 尚未实现，当前不递增 Tick、不消费 inbox。
        }
    }
}
