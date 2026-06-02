using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessBattleResultBuilder
    {
        public static AutoChessBattleResult Build(
            AutoChessBattleSession session,
            bool completed,
            AutoChessTeam winner,
            int scenarioScale,
            int battleTicks,
            int totalTicks,
            int warmupDroppedTicks,
            int measuredTicks,
            AutoChessBattleDriverComponent driverStats,
            long elapsedTicks,
            double elapsedMilliseconds,
            long measuredElapsedTicks,
            double measuredElapsedMilliseconds,
            AutoChessBattleRuntimeTiming runtimeTiming,
            GasRuntimeOfficialToolDiffSnapshot officialToolDiff)
        {
            var units = session.CreateUnitResults();
            var coreObservation = AutoChessGasCoreBridge.CreateObservationSnapshot();
            var battleLog = AutoChessBattleLogBuilder.Build(
                session.Room,
                coreObservation.StructuredLog,
                units,
                winner,
                battleTicks);

            return new AutoChessBattleResult(
                session.Room.RoomId,
                completed,
                winner,
                scenarioScale,
                battleTicks,
                totalTicks,
                warmupDroppedTicks,
                measuredTicks,
                driverStats.IssuedCommandCount,
                driverStats.IssuedPrimaryCommandCount,
                driverStats.IssuedFinisherCommandCount,
                driverStats.LowestHealthTargetCount,
                elapsedTicks,
                elapsedMilliseconds,
                measuredElapsedTicks,
                measuredElapsedMilliseconds,
                runtimeTiming,
                units,
                coreObservation.EventCounts,
                coreObservation.RuntimeDiagnostics,
                coreObservation.RuntimeDiagnosticsLog,
                officialToolDiff,
                coreObservation.StructuredLog,
                battleLog,
                coreObservation.AssertionLog);
        }
    }
}
