using System.Diagnostics;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    internal sealed class AutoChessBattleFlow : System.IDisposable
    {
        private const int WarmupRuntimeTicks = 3;

        private readonly AutoChessBattleOptions _options;
        private readonly AutoChessBattleProfileHooks _profileHooks;
        private readonly IAutoChessBattleRuntime _runtime;
        private readonly AutoChessBattleSession _session;
        private AutoChessGasCoreOfficialToolDiffCapture _officialToolDiffCapture;
        private GasRuntimeOfficialToolDiffSnapshot _officialToolDiff;
        private AutoChessBattleRuntimeTiming _runtimeTiming;
        private Stopwatch _stopwatch;
        private long _measuredElapsedTicks;
        private int _measuredTicks;
        private int _droppedWarmupTicks;
        private int _battleTicks;
        private int _totalTicks;
        private int _victoryTick;
        private AutoChessTeam _winner;
        private bool _officialToolDiffStarted;
        private bool _officialToolDiffClosed;
        private bool _measuredWindowOpened;
        private bool _measuredWindowClosed;
        private bool _opened;
        private bool _completed;
        private bool _disposed;

        private AutoChessBattleFlow(
            AutoChessBattleOptions options,
            AutoChessBattleProfileHooks profileHooks,
            IAutoChessBattleRuntime runtime)
        {
            _options = options.Normalize();
            _profileHooks = profileHooks;
            _runtime = runtime ?? AutoChessBattleRuntime.Default;
            _session = new AutoChessBattleSession(AutoChessGameRoomFactory.CreateDefaultRoom(
                _options.Scale,
                _options.HealthMultiplier));
            _officialToolDiff = _runtime.UnavailableOfficialToolDiff;
            _winner = AutoChessTeam.None;
            _victoryTick = -1;
        }

        public static AutoChessBattleFlow Open(
            AutoChessBattleOptions options,
            AutoChessBattleProfileHooks profileHooks)
        {
            var normalized = options.Normalize();
            var runtime = AutoChessBattleRuntime.Default;
            runtime.Open(normalized);

            var flow = new AutoChessBattleFlow(normalized, profileHooks, runtime);
            flow.OpenSession();
            return flow;
        }

        public bool CanAdvance => _opened && !_completed && _battleTicks < _options.MaxTicks;

        public bool AdvanceTick(bool stopWhenMinimumBattleSecondsReachedWithoutWinner)
        {
            if (!CanAdvance)
                return false;

            var shouldRecordTiming = _droppedWarmupTicks >= WarmupRuntimeTicks;
            OpenMeasuredWindowIfNeeded(shouldRecordTiming);

            var tickStart = shouldRecordTiming ? Stopwatch.GetTimestamp() : 0L;
            _runtime.AdvanceFixedTick(shouldRecordTiming, ref _runtimeTiming);

            if (shouldRecordTiming)
            {
                _measuredElapsedTicks += Stopwatch.GetTimestamp() - tickStart;
                _measuredTicks++;
            }
            else
            {
                _droppedWarmupTicks++;
            }

            _totalTicks++;
            _battleTicks++;
            UpdateVictoryState();

            if (HasResolvedVictoryAndFlushed())
                return false;

            return !stopWhenMinimumBattleSecondsReachedWithoutWinner
                   || _victoryTick >= 0
                   || !HasReachedMinimumBattleSeconds();
        }

        public AutoChessBattleResult Complete()
        {
            if (_completed)
                throw new System.InvalidOperationException("AutoChessBattleFlow has already completed.");

            CloseMeasuredWindow();
            CloseOfficialToolDiffCapture();

            if (_winner == AutoChessTeam.None)
                _session.TryResolveWinner(out _winner);

            _stopwatch.Stop();
            var driverStats = _session.GetDriverStats();
            var diagnosticsStart = Stopwatch.GetTimestamp();
            var coreObservation = _runtime.ExportDiagnostics();
            _runtimeTiming.AddDebuggerExport(Stopwatch.GetTimestamp() - diagnosticsStart);
            _completed = true;
            return AutoChessBattleResultBuilder.Build(
                _session,
                coreObservation,
                IsCompleted(),
                _winner,
                _options.Scale,
                _battleTicks,
                _totalTicks,
                _droppedWarmupTicks,
                _measuredTicks,
                driverStats,
                _stopwatch.ElapsedTicks,
                _stopwatch.Elapsed.TotalMilliseconds,
                _measuredElapsedTicks,
                _runtime.ToMilliseconds(_measuredElapsedTicks),
                _runtimeTiming,
                _officialToolDiff);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (!_completed)
            {
                CloseMeasuredWindow();
                CloseOfficialToolDiffCapture();
            }

            _session.Close();
            FlushRuntimeCleanup();
            _disposed = true;
        }

        private void OpenSession()
        {
            _stopwatch = Stopwatch.StartNew();
            _session.Open();
            _runtime.AdvanceFixedTick(recordTiming: false, ref _runtimeTiming);
            _droppedWarmupTicks++;
            _totalTicks++;
            _opened = true;
        }

        private void OpenMeasuredWindowIfNeeded(bool shouldRecordTiming)
        {
            if (!shouldRecordTiming || _measuredWindowOpened)
                return;

            if (_options.CaptureOfficialToolDiff)
            {
                _officialToolDiffCapture = _runtime.BeginOfficialToolDiffCapture();
                _officialToolDiffStarted = true;
            }

            _profileHooks.BeforeMeasuredWindow?.Invoke();
            _measuredWindowOpened = true;
        }

        private void CloseMeasuredWindow()
        {
            if (!_measuredWindowOpened || _measuredWindowClosed)
                return;

            _profileHooks.AfterMeasuredWindow?.Invoke();
            _measuredWindowClosed = true;
        }

        private void CloseOfficialToolDiffCapture()
        {
            if (!_officialToolDiffStarted || _officialToolDiffClosed)
                return;

            _officialToolDiff = _officialToolDiffCapture.End();
            _officialToolDiffClosed = true;
        }

        private void UpdateVictoryState()
        {
            if (_victoryTick >= 0)
                return;

            if (_session.TryResolveWinner(out _winner))
                _victoryTick = _battleTicks;
        }

        private bool HasResolvedVictoryAndFlushed()
        {
            return _victoryTick >= 0
                   && _battleTicks - _victoryTick >= _options.PostVictoryFlushTicks
                   && HasReachedMinimumBattleSeconds();
        }

        private bool HasReachedMinimumBattleSeconds()
        {
            return _options.MinimumBattleSeconds <= 0d
                   || _stopwatch.Elapsed.TotalSeconds >= _options.MinimumBattleSeconds;
        }

        private bool IsCompleted()
        {
            if (_winner != AutoChessTeam.None && _winner != AutoChessTeam.Draw)
                return true;

            return _options.MinimumBattleSeconds > 0d
                   && _stopwatch.Elapsed.TotalSeconds >= _options.MinimumBattleSeconds;
        }

        private void FlushRuntimeCleanup()
        {
            _runtime.AdvanceFixedTick(recordTiming: false, ref _runtimeTiming);
            _runtime.AdvanceFixedTick(recordTiming: false, ref _runtimeTiming);
        }
    }
}
