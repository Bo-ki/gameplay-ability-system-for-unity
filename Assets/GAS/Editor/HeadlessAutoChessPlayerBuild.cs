using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace GAS.Editor
{
    public static class HeadlessAutoChessPlayerBuild
    {
        private const string BuildPathArgument = "-gasAutoChessBuildPath";

        public static void BuildWindowsPlayerFromCommandLine()
        {
            var exitCode = 0;
            try
            {
                var report = BuildWindowsPlayer();
                exitCode = report.summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception ex)
            {
                exitCode = 1;
                UnityEngine.Debug.LogException(ex);
                Console.Error.WriteLine(ex);
            }
            finally
            {
                EditorApplication.Exit(exitCode);
            }
        }

        public static BuildReport BuildWindowsPlayer()
        {
            var args = Environment.GetCommandLineArgs();
            var buildPath = ReadArgumentValue(
                args,
                BuildPathArgument,
                Path.Combine("Builds", "AutoChessHeadless", "AutoChessHeadless.exe"));
            buildPath = Path.GetFullPath(buildPath);

            var buildDirectory = Path.GetDirectoryName(buildPath);
            if (!string.IsNullOrEmpty(buildDirectory))
                Directory.CreateDirectory(buildDirectory);

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scene found in EditorBuildSettings.");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            UnityEngine.Debug.Log(
                "HeadlessAutoChessPlayerBuild|result="
                + summary.result
                + "|errors="
                + summary.totalErrors
                + "|warnings="
                + summary.totalWarnings
                + "|path="
                + buildPath);

            return report;
        }

        private static string ReadArgumentValue(string[] args, string argument, string fallback)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return fallback;
        }
    }
}
