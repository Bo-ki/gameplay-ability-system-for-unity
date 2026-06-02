using System;
using System.Collections;
using System.Diagnostics;

namespace GAS.AutoChessDemo
{
    public static class AutoChessBattleManager
    {
        private const int WarmupRuntimeTicks = 3;

        public static AutoChessBattleResult RunDefault(
            AutoChessBattleOptions options = default,
            AutoChessBattleProfileHooks profileHooks = default)
        {
            var normalized = options.Normalize();
            AutoChessGasCoreBridge.EnsureRuntimeInitialized();
            AutoChessGasCoreBridge.ResetObservationState(normalized);

            var officialToolDiffCapture = default(AutoChessGasCoreOfficialToolDiffCapture);
            var officialToolDiffClosed = false;
            var officialToolDiffStarted = false;
            var officialToolDiff = AutoChessGasCoreBridge.UnavailableOfficialToolDiff;
            var measuredWindowOpened = false;
            var measuredWindowClosed = false;
            var stopwatch = Stopwatch.StartNew();
            var measuredElapsedTicks = 0L;
            var measuredTicks = 0;
            var droppedWarmupTicks = 0;
            var runtimeTiming = new AutoChessBattleRuntimeTiming();
            var room = AutoChessGameRoomFactory.CreateDefaultRoom(
                normalized.Scale,
                normalized.HealthMultiplier);
            var state = new AutoChessBattleSession(room);

            try
            {
                state.Open();
                AutoChessGasCoreBridge.TickRuntime(recordTiming: false, ref runtimeTiming);
                state.CacheGrantedAbilityEntities();
                droppedWarmupTicks++;

                var battleTicks = 0;
                var totalTicks = 1;
                var winner = AutoChessTeam.None;
                var victoryTick = -1;

                for (var i = 0; i < normalized.MaxTicks; i++)
                {
                    var shouldRecordTiming = droppedWarmupTicks >= WarmupRuntimeTicks;
                    if (shouldRecordTiming && !measuredWindowOpened)
                    {
                        if (normalized.CaptureOfficialToolDiff)
                        {
                            officialToolDiffCapture = AutoChessGasCoreBridge.BeginOfficialToolDiffCapture();
                            officialToolDiffStarted = true;
                        }

                        profileHooks.BeforeMeasuredWindow?.Invoke();
                        measuredWindowOpened = true;
                    }

                    var tickStart = shouldRecordTiming ? Stopwatch.GetTimestamp() : 0L;
                    AutoChessGasCoreBridge.TickRuntime(shouldRecordTiming, ref runtimeTiming);
                    if (!shouldRecordTiming)
                        state.CacheGrantedAbilityEntities();
                    if (shouldRecordTiming)
                    {
                        measuredElapsedTicks += Stopwatch.GetTimestamp() - tickStart;
                        measuredTicks++;
                    }
                    else
                    {
                        droppedWarmupTicks++;
                    }

                    totalTicks++;
                    battleTicks++;

                    if (victoryTick < 0 && state.TryResolveWinner(out winner))
                    {
                        victoryTick = battleTicks;
                    }

                    if (victoryTick >= 0
                        && battleTicks - victoryTick >= normalized.PostVictoryFlushTicks
                        && HasReachedMinimumBattleSeconds(normalized, stopwatch))
                    {
                        break;
                    }
                }

                if (measuredWindowOpened)
                {
                    profileHooks.AfterMeasuredWindow?.Invoke();
                    measuredWindowClosed = true;
                }

                if (officialToolDiffStarted)
                {
                    officialToolDiff = officialToolDiffCapture.End();
                    officialToolDiffClosed = true;
                }

                if (winner == AutoChessTeam.None)
                    state.TryResolveWinner(out winner);

                stopwatch.Stop();
                var driverStats = state.GetDriverStats();
                return AutoChessBattleResultBuilder.Build(
                    state,
                    IsCompleted(normalized, stopwatch, winner),
                    winner,
                    normalized.Scale,
                    battleTicks,
                    totalTicks,
                    droppedWarmupTicks,
                    measuredTicks,
                    driverStats,
                    stopwatch.ElapsedTicks,
                    stopwatch.Elapsed.TotalMilliseconds,
                    measuredElapsedTicks,
                    AutoChessGasCoreBridge.ToMilliseconds(measuredElapsedTicks),
                    runtimeTiming,
                    officialToolDiff);
            }
            finally
            {
                if (measuredWindowOpened && !measuredWindowClosed)
                    profileHooks.AfterMeasuredWindow?.Invoke();

                if (officialToolDiffStarted && !officialToolDiffClosed)
                    officialToolDiffCapture.End();

                state.Close();
            }
        }

        public static IEnumerator RunDefaultStepped(
            AutoChessBattleOptions options,
            AutoChessBattleProfileHooks profileHooks,
            Action<AutoChessBattleResult> completed)
        {
            var normalized = options.Normalize();
            AutoChessGasCoreBridge.EnsureRuntimeInitialized();
            AutoChessGasCoreBridge.ResetObservationState(normalized);

            var officialToolDiffCapture = default(AutoChessGasCoreOfficialToolDiffCapture);
            var officialToolDiffClosed = false;
            var officialToolDiffStarted = false;
            var officialToolDiff = AutoChessGasCoreBridge.UnavailableOfficialToolDiff;
            var measuredWindowOpened = false;
            var measuredWindowClosed = false;
            var stopwatch = Stopwatch.StartNew();
            var measuredElapsedTicks = 0L;
            var measuredTicks = 0;
            var droppedWarmupTicks = 0;
            var runtimeTiming = new AutoChessBattleRuntimeTiming();
            var room = AutoChessGameRoomFactory.CreateDefaultRoom(
                normalized.Scale,
                normalized.HealthMultiplier);
            var state = new AutoChessBattleSession(room);
            var result = default(AutoChessBattleResult);

            try
            {
                state.Open();
                AutoChessGasCoreBridge.TickRuntime(recordTiming: false, ref runtimeTiming);
                state.CacheGrantedAbilityEntities();
                droppedWarmupTicks++;
                yield return null;

                var battleTicks = 0;
                var totalTicks = 1;
                var winner = AutoChessTeam.None;
                var victoryTick = -1;

                for (var i = 0; i < normalized.MaxTicks; i++)
                {
                    var shouldRecordTiming = droppedWarmupTicks >= WarmupRuntimeTicks;
                    if (shouldRecordTiming && !measuredWindowOpened)
                    {
                        if (normalized.CaptureOfficialToolDiff)
                        {
                            officialToolDiffCapture = AutoChessGasCoreBridge.BeginOfficialToolDiffCapture();
                            officialToolDiffStarted = true;
                        }

                        profileHooks.BeforeMeasuredWindow?.Invoke();
                        measuredWindowOpened = true;
                    }

                    var tickStart = shouldRecordTiming ? Stopwatch.GetTimestamp() : 0L;
                    AutoChessGasCoreBridge.TickRuntime(shouldRecordTiming, ref runtimeTiming);
                    if (!shouldRecordTiming)
                        state.CacheGrantedAbilityEntities();
                    if (shouldRecordTiming)
                    {
                        measuredElapsedTicks += Stopwatch.GetTimestamp() - tickStart;
                        measuredTicks++;
                    }
                    else
                    {
                        droppedWarmupTicks++;
                    }

                    totalTicks++;
                    battleTicks++;

                    if (victoryTick < 0 && state.TryResolveWinner(out winner))
                        victoryTick = battleTicks;

                    if (victoryTick >= 0
                        && battleTicks - victoryTick >= normalized.PostVictoryFlushTicks
                        && HasReachedMinimumBattleSeconds(normalized, stopwatch))
                    {
                        break;
                    }

                    if (victoryTick < 0 && HasReachedMinimumBattleSeconds(normalized, stopwatch))
                        break;

                    yield return null;
                }

                if (measuredWindowOpened)
                {
                    profileHooks.AfterMeasuredWindow?.Invoke();
                    measuredWindowClosed = true;
                }

                if (officialToolDiffStarted)
                {
                    officialToolDiff = officialToolDiffCapture.End();
                    officialToolDiffClosed = true;
                }

                if (winner == AutoChessTeam.None)
                    state.TryResolveWinner(out winner);

                stopwatch.Stop();
                var driverStats = state.GetDriverStats();
                result = AutoChessBattleResultBuilder.Build(
                    state,
                    IsCompleted(normalized, stopwatch, winner),
                    winner,
                    normalized.Scale,
                    battleTicks,
                    totalTicks,
                    droppedWarmupTicks,
                    measuredTicks,
                    driverStats,
                    stopwatch.ElapsedTicks,
                    stopwatch.Elapsed.TotalMilliseconds,
                    measuredElapsedTicks,
                    AutoChessGasCoreBridge.ToMilliseconds(measuredElapsedTicks),
                    runtimeTiming,
                    officialToolDiff);
            }
            finally
            {
                if (measuredWindowOpened && !measuredWindowClosed)
                    profileHooks.AfterMeasuredWindow?.Invoke();

                if (officialToolDiffStarted && !officialToolDiffClosed)
                    officialToolDiffCapture.End();

                state.Close();
            }

            completed?.Invoke(result);
        }

        public static void ShutdownRuntime()
        {
            AutoChessGasCoreBridge.ShutdownRuntime();
        }

        private static bool HasReachedMinimumBattleSeconds(
            in AutoChessBattleOptions options,
            Stopwatch stopwatch)
        {
            return options.MinimumBattleSeconds <= 0d
                   || stopwatch.Elapsed.TotalSeconds >= options.MinimumBattleSeconds;
        }

        private static bool IsCompleted(
            in AutoChessBattleOptions options,
            Stopwatch stopwatch,
            AutoChessTeam winner)
        {
            if (winner != AutoChessTeam.None && winner != AutoChessTeam.Draw)
                return true;

            return options.MinimumBattleSeconds > 0d
                   && stopwatch.Elapsed.TotalSeconds >= options.MinimumBattleSeconds;
        }

    }
}
