using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GAS.Runtime
{
    public static class HeadlessAutoChessRuntimeRunner
    {
        private const string RunArgument = "-gasAutoChessHeadless";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunOnPlayerLaunch()
        {
            if (!HasArgument(RunArgument))
                return;

            var request = HeadlessAutoChessRuntimeSystemBootstrap.RequestDefaultScenario();
            Debug.Log(
                "HeadlessAutoChessRuntimeRunner: queued generated AutoChess scenario bootstrap "
                + $"request={request.Index}, scenario={HeadlessAutoChessScenario.ScenarioDefaultDuel}");
        }

        private static bool HasArgument(string argument)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
