using System.Diagnostics;
using GAS.Runtime;
using Unity.Core;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasRuntimeTicker
    {
        /// <summary>
        /// 通过唯一 Runtime v1 owner 推进一次完整 Simulation batch，禁止手动更新物理 phase group。
        /// </summary>
        public static bool TickRuntime(
            bool recordTiming,
            ref AutoChessBattleRuntimeTiming runtimeTiming)
        {
            var owner = AutoChessGasRuntimeHost.RuntimeOwner;
            if (owner == null || !AutoChessGasRuntimeHost.TryResolveWorld(out var world))
                return false;

            SetDeterministicBatchTime(world);

            var start = Stopwatch.GetTimestamp();
            var succeeded = owner.TickBatch();
            var elapsedTicks = Stopwatch.GetTimestamp() - start;
            if (succeeded)
                succeeded = AutoChessGasObservationGateway.DrainRuntimeV1BoundaryBatches();
            if (!recordTiming)
                return succeeded;

            runtimeTiming.Add(
                0,
                0,
                elapsedTicks,
                0,
                0,
                0);
            return succeeded;
        }

        /// <summary>
        /// 为 headless 显式 batch 提供一个真实 fixed delta，确保 FixedStepSimulationSystemGroup 每次只推进一个 tick。
        /// </summary>
        private static void SetDeterministicBatchTime(World world)
        {
            var timestep = 1f / 20f;
            using var query = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GasSessionConfig>());
            if (query.CalculateEntityCount() == 1)
            {
                var config = query.GetSingleton<GasSessionConfig>();
                if (config.TickRate > 0)
                    timestep = 1f / config.TickRate;
            }

            var elapsed = world.Time.ElapsedTime + timestep;
            world.SetTime(new TimeData(elapsed, timestep));
        }

        public static double ToMilliseconds(long stopwatchTicks)
        {
            return stopwatchTicks * 1000d / Stopwatch.Frequency;
        }

    }
}
