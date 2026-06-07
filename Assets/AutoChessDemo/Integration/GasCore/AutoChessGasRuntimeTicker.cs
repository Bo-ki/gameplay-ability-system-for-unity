using System.Diagnostics;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasRuntimeTicker
    {
        public static void TickRuntime(
            bool recordTiming,
            ref AutoChessBattleRuntimeTiming runtimeTiming)
        {
            if (!AutoChessGasRuntimeHost.TryGetRuntimeTickGroups(out var groups))
                return;

            var framePrepareTicks = UpdateTimed(groups.FramePrepare);
            var commandResolveTicks = UpdateTimed(groups.CommandResolve);
            var coreSimulationTicks = UpdateTimed(groups.CoreSimulation);
            var structuralCommitTicks = UpdateTimed(groups.StructuralCommit);
            var boundaryProjectionTicks = UpdateTimed(groups.BoundaryProjection);

            if (!recordTiming)
                return;

            var dependencyDrainTicks = CompleteRuntimeJobsTimed();
            runtimeTiming.Add(
                framePrepareTicks,
                commandResolveTicks,
                coreSimulationTicks,
                structuralCommitTicks,
                boundaryProjectionTicks,
                dependencyDrainTicks);
            RecordRuntimeTickTiming(
                framePrepareTicks,
                commandResolveTicks,
                coreSimulationTicks,
                structuralCommitTicks,
                boundaryProjectionTicks,
                dependencyDrainTicks);
        }

        public static double ToMilliseconds(long stopwatchTicks)
        {
            return stopwatchTicks * 1000d / Stopwatch.Frequency;
        }

        private static long UpdateTimed(ComponentSystemGroup group)
        {
            var start = Stopwatch.GetTimestamp();
            group.Update();
            return Stopwatch.GetTimestamp() - start;
        }

        private static long CompleteRuntimeJobsTimed()
        {
            var start = Stopwatch.GetTimestamp();
            AutoChessGasRuntimeHost.TryCompleteRuntimeJobs();
            return Stopwatch.GetTimestamp() - start;
        }

        private static void RecordRuntimeTickTiming(
            long framePrepareTicks,
            long commandResolveTicks,
            long coreSimulationTicks,
            long structuralCommitTicks,
            long boundaryProjectionTicks,
            long dependencyDrainTicks)
        {
            AutoChessGasObservationGateway.RecordRuntimeTickTiming(
                framePrepareTicks,
                commandResolveTicks,
                coreSimulationTicks,
                structuralCommitTicks,
                boundaryProjectionTicks,
                dependencyDrainTicks,
                Stopwatch.Frequency);
        }
    }
}
