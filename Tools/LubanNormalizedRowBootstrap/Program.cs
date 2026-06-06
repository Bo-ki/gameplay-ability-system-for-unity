using System;
using GAS.Editor;

namespace LubanNormalizedRowBootstrapHost
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                var projectRoot = GasCodeGenEnvironment.ResolveProjectRootArgument(args);
                GasCodeGenEnvironment.UseOfflineProjectRoot(projectRoot);
                var outputPath = LubanNormalizedRowBootstrap.Generate(GasCodeGenSettings.CreateDefault());
                Console.WriteLine(outputPath);
                return 0;
            }
            catch (Exception ex)
            {
                GasCodeGenEnvironment.LogException(ex);
                return 1;
            }
        }
    }
}
