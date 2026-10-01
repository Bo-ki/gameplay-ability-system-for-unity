using System;
using GAS.Editor;

namespace LubanNormalizedRowBootstrapHost
{
    /// <summary>
    /// 在主 CodeGen CLI 编译前生成隔离的 normalized row 编译输入。
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// 解析项目与候选输出根，执行 bootstrap 并用退出码报告结果。
        /// </summary>
        private static int Main(string[] args)
        {
            try
            {
                var projectRoot = GasCodeGenEnvironment.ResolveProjectRootArgument(args);
                GasCodeGenEnvironment.UseOfflineProjectRoot(projectRoot);
                var outputRoot = ResolveArgument(args, "--outputRoot");
                var outputPath = LubanNormalizedRowBootstrap.Generate(
                    GasCodeGenSettings.CreateDefault(),
                    outputRoot);
                Console.WriteLine(outputPath);
                return 0;
            }
            catch (Exception ex)
            {
                GasCodeGenEnvironment.LogException(ex);
                return 1;
            }
        }

        /// <summary>
        /// 读取支持“--name value”与“--name=value”的可选命令行参数。
        /// </summary>
        private static string ResolveArgument(string[] args, string name)
        {
            if (args == null)
                return string.Empty;

            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal) && i + 1 < args.Length)
                    return args[i + 1];

                var prefix = name + "=";
                if (args[i] != null && args[i].StartsWith(prefix, StringComparison.Ordinal))
                    return args[i].Substring(prefix.Length);
            }

            return string.Empty;
        }
    }
}
