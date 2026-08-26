using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;

namespace GAS.Editor
{
    /// <summary>
    /// EX-GAS Runtime v1 的编辑器生成入口，仅负责 Luban 导表与统一 CodeGen Pipeline 调度。
    /// 旧版按 Tag、Ability、Cue 或 Launcher 分散生成的入口已移除，避免重新产生第二套运行时事实源。
    /// </summary>
    public static class CodeGenerator
    {
        /// <summary>
        /// 执行 Luban 导表并刷新编辑器资源数据库。
        /// </summary>
        [MenuItem("EXTool/EX-GAS/生成脚本/GAS表配置", priority = 0)]
        public static void GenerateGasConfigTables()
        {
            TryGenerateGasConfigTables();
        }

        /// <summary>
        /// 执行一次 Luban 导表，供 Runtime v1 Pipeline 和 GAS Center 共用。
        /// </summary>
        internal static bool TryGenerateGasConfigTables()
        {
            var settings = GasCodeGenEnvironment.ActiveSettings
                           ?? (GasCodeGenEnvironment.IsOffline
                               ? GasCodeGenSettings.CreateDefault()
                               : GasCodeGenSettings.From(GASSettingAsset.LoadOrCreate()));
            GasCodeGenEnvironment.SetActiveSettings(settings);

            var configProjectPath = GasCodeGenEnvironment.ResolveProjectPath(settings.ConfigProjectPath);
            var outputPath = GasCodeGenEnvironment.ResolveProjectPath(settings.LubanDataOutputPath);
            var codeOutputPath = GasCodeGenEnvironment.ResolveProjectPath(settings.LubanCodeOutputPath);
            var lubanDllPath = Path.GetFullPath(Path.Combine(configProjectPath, "..", "Tools", "Luban", "Luban.dll"));
            var lubanConfigPath = Path.Combine(configProjectPath, "luban.conf");

            if (!ValidateLubanInputs(configProjectPath, lubanDllPath, lubanConfigPath))
                return false;

            Directory.CreateDirectory(outputPath);
            Directory.CreateDirectory(codeOutputPath);
            return RunLuban(configProjectPath, lubanDllPath, lubanConfigPath, outputPath, codeOutputPath);
        }

        /// <summary>
        /// 执行 Runtime v1 的统一生成流水线。
        /// </summary>
        [MenuItem("EXTool/EX-GAS/生成脚本/Runtime v1", priority = 10)]
        public static void GenerateAllCode()
        {
            TryGenerateAllCode();
        }

        /// <summary>
        /// 先通过输入处理门，再运行统一 Core 与 AutoChess 生成阶段。
        /// </summary>
        public static bool TryGenerateAllCode()
        {
            if (!GasCodeGenProcessGate.RunDefault())
                return false;

            return GasCodeGenPipeline.TryRunAll();
        }

        /// <summary>
        /// 检查 Luban 工程、配置文件和运行时程序集是否齐全。
        /// </summary>
        private static bool ValidateLubanInputs(
            string configProjectPath,
            string lubanDllPath,
            string lubanConfigPath)
        {
            if (!Directory.Exists(configProjectPath))
            {
                GasCodeGenEnvironment.LogError($"配置表工程路径不存在: {configProjectPath}");
                return false;
            }

            if (!File.Exists(lubanDllPath))
            {
                GasCodeGenEnvironment.LogError($"Luban.dll 不存在: {lubanDllPath}");
                return false;
            }

            if (!File.Exists(lubanConfigPath))
            {
                GasCodeGenEnvironment.LogError($"luban.conf 不存在: {lubanConfigPath}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 启动 Luban 子进程并将标准输出转发到统一日志入口。
        /// </summary>
        private static bool RunLuban(
            string configProjectPath,
            string lubanDllPath,
            string lubanConfigPath,
            string outputPath,
            string codeOutputPath)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    WorkingDirectory = configProjectPath,
                    Arguments = $"{Quote(lubanDllPath)} -t client -c cs-simple-json -d json " +
                                $"--conf {Quote(lubanConfigPath)} " +
                                $"-x outputCodeDir={Quote(codeOutputPath)} " +
                                $"-x outputDataDir={Quote(outputPath)}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.OutputDataReceived += (_, args) => LogOutput(args.Data, false);
            process.ErrorDataReceived += (_, args) => LogOutput(args.Data, true);

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    GasCodeGenEnvironment.LogError($"Luban 执行失败，退出代码: {process.ExitCode}");
                    return false;
                }

                GasCodeGenEnvironment.Log("Luban 执行完成。");
                return true;
            }
            catch (Exception ex)
            {
                GasCodeGenEnvironment.LogException(ex);
                return false;
            }
            finally
            {
                GasCodeGenEnvironment.RefreshAssetDatabase();
            }
        }

        /// <summary>
        /// 将 Luban 的一行输出路由到普通或错误日志。
        /// </summary>
        private static void LogOutput(string message, bool isError)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (isError)
                GasCodeGenEnvironment.LogError(message);
            else
                GasCodeGenEnvironment.Log(message);
        }

        /// <summary>
        /// 为 Luban 命令行参数添加安全引号。
        /// </summary>
        private static string Quote(string value)
        {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }
    }
}
