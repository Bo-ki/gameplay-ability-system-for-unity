using UnityEditor;
using UnityEngine;

namespace GAS.Editor
{
    public static class GasCodeGenBatchRunner
    {
        public static void GenerateAllAndExit()
        {
            var success = false;
            try
            {
                success = CodeGenerator.TryGenerateAllCode();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                success = false;
            }

            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
