using System;
using UnityEditor;
using UnityEngine;

namespace GAS.AutoChessDemo.Editor
{
    public static class AutoChessDemoBatchRunner
    {
        private const string RunValidationArgument = "-runAutoChessValidation";
        private static bool s_RunScheduled;

        [InitializeOnLoadMethod]
        private static void RunFromCommandLineArgument()
        {
            if (!Application.isBatchMode
                || !HasArgument(RunValidationArgument)
                || s_RunScheduled)
            {
                return;
            }

            s_RunScheduled = true;
            EditorApplication.delayCall += RunAutoChessBattleOnceAndExit;
        }

        public static void RunAutoChessBattleOnceAndExit()
        {
            var success = false;
            try
            {
                AutoChessRuntimeRunner.RunAutoChessBattleOnce();
                success = true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                EditorApplication.Exit(success ? 0 : 1);
            }
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
