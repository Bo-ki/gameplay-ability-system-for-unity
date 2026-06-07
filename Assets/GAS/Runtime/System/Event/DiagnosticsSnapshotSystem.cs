using Unity.Entities;

namespace GAS.Runtime
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup))]
    [UpdateAfter(typeof(ReplayLogSystem))]
    public partial struct DiagnosticsSnapshotSystem : ISystem
    {
        private GasRuntimeCoreCounterQueries _queries;

        public void OnCreate(ref SystemState state)
        {
            _queries = GasRuntimeCoreCounterQueries.Create(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<GASRuntimeDebuggerComponent>(out var debuggerEntity))
                return;

            var em = state.EntityManager;
            var debugger = em.GetComponentData<GASRuntimeDebuggerComponent>(debuggerEntity);
            if (debugger.Enabled == 0)
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var eventBus)
                ? eventBus
                : Entity.Null;
            var eventLogSinkEntity = SystemAPI.TryGetSingletonEntity<GameplayEventLogSinkComponent>(out var eventLogSink)
                ? eventLogSink
                : Entity.Null;
            var currentFrame = GASRuntimeFrameContext.ResolveCurrentFrame(em);

            GasRuntimeDebugger.CollectAndRecordRuntimeCoreCounters(
                em,
                debuggerEntity,
                currentFrame,
                eventBusEntity,
                eventLogSinkEntity,
                _queries);
            GasRuntimeDebugger.RecordEffectCommandSpecStreamPressure(
                em,
                debuggerEntity,
                currentFrame);
            GasRuntimeDebugger.RecordCurrentRuntimeCoreFrameBackboneEvidence(
                em,
                debuggerEntity,
                currentFrame);
        }
    }
}
