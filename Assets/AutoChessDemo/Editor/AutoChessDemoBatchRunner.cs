using System;
using UnityEditor;
using UnityEngine;

namespace GAS.AutoChessDemo.Editor
{
    public static class AutoChessDemoBatchRunner
    {
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
    }
}
