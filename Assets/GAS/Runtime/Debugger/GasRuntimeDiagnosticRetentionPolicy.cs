using Unity.Entities;

namespace GAS.Runtime
{
    public static class GasRuntimeDiagnosticRetentionPolicy
    {
        public static void Apply(
            DynamicBuffer<GASRuntimeDiagnosticEventBuffer> log,
            ref GASRuntimeDebuggerComponent state)
        {
            if (state.MaxRetainedEvents <= 0 || log.Length <= state.MaxRetainedEvents)
                return;

            var removeCount = log.Length - state.MaxRetainedEvents;
            log.RemoveRange(0, removeCount);
            state.FirstRetainedSequence = log.Length > 0 ? log[0].Sequence : state.NextSequence;
            state.DroppedEventCount += removeCount;
        }
    }
}
