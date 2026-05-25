using Unity.Entities;

namespace GAS.Runtime
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCueGroup))]
    [UpdateAfter(typeof(SDebugReplayLogProjection))]
    public partial struct SRuntimeCoreDebuggerCounters : ISystem
    {
        private GasRuntimeCoreCounterQueries _queries;

        public void OnCreate(ref SystemState state)
        {
            _queries = GasRuntimeCoreCounterQueries.Create(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CGasRuntimeDebugger>(out var debuggerEntity))
                return;

            var em = state.EntityManager;
            var debugger = em.GetComponentData<CGasRuntimeDebugger>(debuggerEntity);
            if (debugger.Enabled == 0)
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBus)
                ? eventBus
                : Entity.Null;
            var eventLogSinkEntity = SystemAPI.TryGetSingletonEntity<CGameplayEventLogSink>(out var eventLogSink)
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
            GasRuntimeDebugger.RecordCurrentRuntimeCoreFrameBackboneEvidence(
                em,
                debuggerEntity,
                currentFrame);
        }
    }
}
