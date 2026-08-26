using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasCoreBridge
    {
        public static AutoChessGasBattleUnitHandle CreateBattleUnit(AutoChessUnitDefinition definition)
        {
            return AutoChessGasBattleEntityLifecycle.CreateBattleUnit(definition);
        }

        public static AutoChessBattleReportFact[] CreateReportFacts(
            in GasStructuredLogExportSnapshot structuredLog,
            AutoChessGasBattleUnitHandle[] handles)
        {
            return AutoChessGasBattleEntityLifecycle.CreateReportFacts(structuredLog, handles);
        }

        public static void DestroyBattleUnit(AutoChessGasBattleUnitHandle handle)
        {
            AutoChessGasBattleEntityLifecycle.DestroyBattleUnit(handle);
        }

    }
}
