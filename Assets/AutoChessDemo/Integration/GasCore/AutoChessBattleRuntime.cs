using System;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    internal interface IAutoChessBattleRuntime
    {
        GasRuntimeOfficialToolDiffSnapshot UnavailableOfficialToolDiff { get; }

        void Open(in AutoChessBattleOptions options);

        bool AdvanceFixedTick(
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
            if (!AutoChessGasRuntimeHost.EnsureRuntimeInitialized())
                throw new InvalidOperationException("Runtime v1 AutoChess host 初始化失败。");
            AutoChessGasObservationGateway.ResetObservationState(options.Normalize());
        }

        public bool AdvanceFixedTick(
            bool recordTiming,
            ref AutoChessBattleRuntimeTiming runtimeTiming)
        {
            return AutoChessGasRuntimeTicker.TickRuntime(recordTiming, ref runtimeTiming);
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
