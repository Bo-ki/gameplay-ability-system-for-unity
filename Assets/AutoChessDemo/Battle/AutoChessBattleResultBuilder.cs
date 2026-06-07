using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessBattleResultBuilder
    {
        public static AutoChessBattleResult Build(
            AutoChessBattleSession session,
            in AutoChessGasCoreObservationSnapshot coreObservation,
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
            var units = session.CreateUnitResults(coreObservation.StructuredLog);
            var reportFacts = session.CreateReportFacts(coreObservation.StructuredLog);
            var battleReport = AutoChessBattleReportBuilder.Build(
                units,
                reportFacts);
            var battleLog = AutoChessBattleLogBuilder.Build(
                session.Room,
                battleReport,
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
                battleReport,
                battleLog,
                coreObservation.AssertionLog);
        }
    }
}
