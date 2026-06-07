using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    internal interface IAutoChessBattleRuntime
    {
        GasRuntimeOfficialToolDiffSnapshot UnavailableOfficialToolDiff { get; }

        void Open(in AutoChessBattleOptions options);

        void AdvanceFixedTick(
            bool recordTiming,
            ref AutoChessBattleRuntimeTiming runtimeTiming);

        AutoChessGasCoreOfficialToolDiffCapture BeginOfficialToolDiffCapture();

        AutoChessGasCoreObservationSnapshot ExportDiagnostics();

        double ToMilliseconds(long stopwatchTicks);

        void Shutdown();
    }

    internal sealed class AutoChessBattleRuntime : IAutoChessBattleRuntime
    {
        public static readonly IAutoChessBattleRuntime Default = new AutoChessBattleRuntime();

        private AutoChessBattleRuntime()
        {
        }

        public GasRuntimeOfficialToolDiffSnapshot UnavailableOfficialToolDiff =>
            AutoChessGasObservationGateway.UnavailableOfficialToolDiff;

        public void Open(in AutoChessBattleOptions options)
        {
            AutoChessGasRuntimeHost.EnsureRuntimeInitialized();
            AutoChessGasObservationGateway.ResetObservationState(options.Normalize());
        }

        public void AdvanceFixedTick(
            bool recordTiming,
            ref AutoChessBattleRuntimeTiming runtimeTiming)
        {
            AutoChessGasRuntimeTicker.TickRuntime(recordTiming, ref runtimeTiming);
        }

        public AutoChessGasCoreOfficialToolDiffCapture BeginOfficialToolDiffCapture()
        {
            return AutoChessGasObservationGateway.BeginOfficialToolDiffCapture();
        }

        public AutoChessGasCoreObservationSnapshot ExportDiagnostics()
        {
            return AutoChessGasObservationGateway.CreateObservationSnapshot();
        }

        public double ToMilliseconds(long stopwatchTicks)
        {
            return AutoChessGasRuntimeTicker.ToMilliseconds(stopwatchTicks);
        }

        public void Shutdown()
        {
            AutoChessGasRuntimeHost.ShutdownRuntime();
        }
    }
}
