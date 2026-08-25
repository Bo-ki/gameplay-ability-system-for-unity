using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
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
        private EntityQuery _gameplaySessionQuery;
        private EntityQuery _spawnBatchMarkerQuery;
        private EntityQuery _acceptedBoundaryShellQuery;
        private GasStageBSpawnFinalizeJob _spawnFinalizeTemplate;

        /// <summary>
        /// 创建当前 World-local Session query，不建立静态或跨 World 缓存。
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            _sessionQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<GasActiveSessionAuthority>());
            _gameplaySessionQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<GasActiveSessionAuthority>(),
                ComponentType.ReadOnly<GasSessionIdentity>(),
                ComponentType.ReadOnly<GasScaleProfile>(),
                ComponentType.ReadOnly<GasDefinitionRegistry>(),
                ComponentType.ReadOnly<SimulationTickState>(),
                ComponentType.ReadOnly<GasSessionLifecycle>(),
                ComponentType.ReadOnly<SessionFaultLatch>(),
                ComponentType.ReadOnly<GasTickDiagnostics>(),
                ComponentType.ReadOnly<BoundaryCommandInbox>(),
                ComponentType.ReadOnly<BoundaryCommandFrozenPayload>());
            _spawnBatchMarkerQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<GasSpawnBatchMarker>());
            _acceptedBoundaryShellQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<BoundaryDrainState>(),
                    ComponentType.ReadOnly<BoundaryFactBuffer>(),
                },
                None = new[]
                {
                    ComponentType.ReadOnly<GasAscIdentity>(),
                    ComponentType.ReadOnly<GasSessionIdentity>(),
                },
            });
            _spawnFinalizeTemplate.InitializeLookups(ref state);
            state.RequireForUpdate<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        }

        /// <summary>
        /// 先排 maintenance，再对唯一 Session 串联 SpawnFinalize 与完整 gameplay DAG。
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            var endFixed = SystemAPI.GetSingleton<
                EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            var commandBuffer = endFixed.CreateCommandBuffer(state.WorldUnmanaged);
            var acceptedShells = _acceptedBoundaryShellQuery.ToEntityListAsync(
                Allocator.TempJob,
                out var shellGatherDependency);
            var prepassDependency = JobHandle.CombineDependencies(
                state.Dependency,
                shellGatherDependency);
            var dependency = GasTickDag.ScheduleCleanupAcceptedPrepass(
                ref state,
                acceptedShells,
                commandBuffer,
                prepassDependency);
            var sessionCount = _sessionQuery.CalculateEntityCount();
            if (sessionCount == 0)
            {
                state.Dependency = dependency;
                return;
            }

            _spawnFinalizeTemplate.UpdateLookups(ref state);
            if (sessionCount != 1)
            {
                var faultBatchMarkedAscs = _spawnBatchMarkerQuery.ToEntityArray(Allocator.TempJob);
                var sessions = _sessionQuery.ToEntityArray(Allocator.TempJob);
                state.Dependency = GasStageBSpawnFinalize.FailSessionCardinality(
                    ref _spawnFinalizeTemplate,
                    sessions,
                    faultBatchMarkedAscs,
                    commandBuffer,
                    dependency);
                return;
            }

            var session = _sessionQuery.GetSingletonEntity();
            var batchMarkedAscs = _spawnBatchMarkerQuery.ToEntityListAsync(
                Allocator.TempJob,
                out var gatherDependency);
            if (_gameplaySessionQuery.CalculateEntityCount() != 1)
            {
                state.Dependency = GasStageBSpawnFinalize.Run(
                    ref _spawnFinalizeTemplate,
                    session,
                    batchMarkedAscs,
                    0,
                    gatherDependency,
                    commandBuffer,
                    dependency);
                return;
            }

            var identity = state.EntityManager.GetComponentData<GasSessionIdentity>(session);
            var profile = state.EntityManager.GetComponentData<GasScaleProfile>(session);
            var definitions = state.EntityManager.GetComponentData<GasDefinitionRegistry>(session);
            var scratch = GasTickDag.CreateScratch(ref state, in profile);
            dependency = GasTickDag.ScheduleModeCapture(
                ref state,
                session,
                in scratch,
                dependency);
            dependency = GasStageBSpawnFinalize.Run(
                ref _spawnFinalizeTemplate,
                session,
                batchMarkedAscs,
                profile.MaxSpawnBatchSize,
                gatherDependency,
                commandBuffer,
                dependency);
            state.Dependency = GasTickDag.ScheduleGameplay(
                ref state,
                session,
                in identity,
                in profile,
                in definitions,
                in scratch,
                dependency);
        }
    }
}
