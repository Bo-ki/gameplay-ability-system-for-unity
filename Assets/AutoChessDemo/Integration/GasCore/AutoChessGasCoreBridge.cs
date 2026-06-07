using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasCoreBridge
    {
        public static AutoChessGasBattleUnitHandle CreateBattleUnit(AutoChessUnitDefinition definition)
        {
            return AutoChessGasBattleEntityLifecycle.CreateBattleUnit(definition);
        }

        public static AutoChessGasBattleDriverHandle CreateBattleDriver()
        {
            return AutoChessGasBattleEntityLifecycle.CreateBattleDriver();
        }

        public static AutoChessBattleReportFact[] CreateReportFacts(
            in GasStructuredLogExportSnapshot structuredLog,
            AutoChessGasBattleUnitHandle[] handles)
        {
            return AutoChessGasBattleEntityLifecycle.CreateReportFacts(structuredLog, handles);
        }

        public static AutoChessBattleDriverComponent GetBattleDriverStats(
            AutoChessGasBattleDriverHandle driverHandle)
        {
            return AutoChessGasBattleEntityLifecycle.GetBattleDriverStats(driverHandle);
        }

        public static void CloseBattleDriver(AutoChessGasBattleDriverHandle driverHandle)
        {
            AutoChessGasBattleEntityLifecycle.CloseBattleDriver(driverHandle);
        }

        public static void DestroyBattleUnit(AutoChessGasBattleUnitHandle handle)
        {
            AutoChessGasBattleEntityLifecycle.DestroyBattleUnit(handle);
        }

    }
}
