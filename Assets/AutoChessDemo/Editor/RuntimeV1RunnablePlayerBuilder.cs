using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GAS.AutoChessDemo.Editor
{
    /// <summary>
    /// 通过显式命令行输入构建唯一 Runtime V1 Development Windows Player，不负责生成或测试调度。
    /// </summary>
    public static class RuntimeV1RunnablePlayerBuilder
    {
        private const string BuildOutputArgument = "-gasRuntimeV1RunnableBuildOutput";
        private const string BuildScenesArgument = "-gasRuntimeV1RunnableBuildScenes";

        /// <summary>
        /// 校验冻结的场景和输出路径后执行一次 Development Windows Player 构建。
        /// </summary>
        public static void BuildFromCommandLine()
        {
            var arguments = Environment.GetCommandLineArgs();
            var outputPath = Path.GetFullPath(GetRequiredValue(arguments, BuildOutputArgument));
            var scenes = GetRequiredValue(arguments, BuildScenesArgument)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            ValidateInputs(outputPath, scenes);

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Runtime V1 Development Player build failed: " + report.summary.result);
            }
        }

        /// <summary>
        /// 拒绝旧 Player 输出、空场景和不存在的场景，防止复用历史构建。
        /// </summary>
        private static void ValidateInputs(string outputPath, string[] scenes)
        {
            if (scenes.Length == 0)
                throw new InvalidOperationException("Runtime V1 build scenes are missing.");
            if (File.Exists(outputPath))
                throw new IOException("Runtime V1 Player output already exists: " + outputPath);

            var dataPath = Path.Combine(
                Path.GetDirectoryName(outputPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(outputPath) + "_Data");
            if (Directory.Exists(dataPath))
                throw new IOException("Runtime V1 Player data output already exists: " + dataPath);

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            for (var index = 0; index < scenes.Length; index++)
            {
                var scenePath = scenes[index].Replace('\\', '/');
                if (!scenePath.StartsWith("Assets/", StringComparison.Ordinal)
                    || !File.Exists(Path.Combine(projectRoot, scenePath)))
                {
                    throw new FileNotFoundException("Runtime V1 build scene was not found.", scenes[index]);
                }
            }
        }

        /// <summary>
        /// 读取 `-name value` 或 `-name=value` 参数，缺失时立即失败。
        /// </summary>
        private static string GetRequiredValue(string[] arguments, string argument)
        {
            var prefix = argument + "=";
            for (var index = 0; index < arguments.Length; index++)
            {
                var value = arguments[index];
                if (string.Equals(value, argument, StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 < arguments.Length
                        && !string.IsNullOrWhiteSpace(arguments[index + 1]))
                    {
                        return arguments[index + 1];
                    }

                    break;
                }

                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return value.Substring(prefix.Length);
            }

            throw new InvalidOperationException("Missing required argument: " + argument);
        }
    }
}
