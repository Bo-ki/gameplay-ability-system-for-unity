using System;
using System.Collections;

namespace GAS.AutoChessDemo
{
    public static class AutoChessBattleManager
    {
        public static AutoChessBattleResult RunDefault(
            AutoChessBattleOptions options = default,
            AutoChessBattleProfileHooks profileHooks = default)
        {
            var flow = AutoChessBattleFlow.Open(options, profileHooks);
            try
            {
                while (flow.CanAdvance)
                {
                    if (!flow.AdvanceTick(stopWhenMinimumBattleSecondsReachedWithoutWinner: false))
                        break;
                }

                return flow.Complete();
            }
            finally
            {
                flow.Dispose();
            }
        }

        public static IEnumerator RunDefaultStepped(
            AutoChessBattleOptions options,
            AutoChessBattleProfileHooks profileHooks,
            Action<AutoChessBattleResult> completed)
        {
            var flow = AutoChessBattleFlow.Open(options, profileHooks);
            var result = default(AutoChessBattleResult);

            try
            {
                yield return null;

                while (flow.CanAdvance)
                {
                    if (!flow.AdvanceTick(stopWhenMinimumBattleSecondsReachedWithoutWinner: true))
                        break;

                    yield return null;
                }

                result = flow.Complete();
            }
            finally
            {
                flow.Dispose();
            }

            completed?.Invoke(result);
        }

        public static void ShutdownRuntime()
        {
            AutoChessBattleRuntime.Default.Shutdown();
        }
    }
}
