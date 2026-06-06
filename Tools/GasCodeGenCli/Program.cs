using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GAS.Editor;

namespace GasCodeGenCliHost
{
    internal static class Program
    {
        private const string UnityMethod = "GAS.Editor.GasCodeGenBatchRunner.GenerateAllAndExit";

        private static int Main(string[] args)
        {
            try
            {
                var projectRoot = ResolveProjectRoot(args);
                var mode = ResolveMode(args);

                if (string.Equals(mode, "unity", StringComparison.OrdinalIgnoreCase))
                    return RunUnityBatchmode(projectRoot, args);

                GasCodeGenEnvironment.UseOfflineProjectRoot(projectRoot);
                Console.WriteLine("Mode:    sourcegen");
                Console.WriteLine($"Project: {projectRoot}");
                Console.WriteLine("Input:   Luban JSON tables");

                return GasCodeGenPipeline.TryRunAll(refreshAssetDatabase: false)
                    ? 0
                    : 3;
            }
            catch (Exception ex)
            {
                WriteException(ex);
                return 1;
            }
        }

        private static int RunUnityBatchmode(string projectRoot, string[] args)
        {
            var unityExe = ResolveUnityExecutable(projectRoot, args);
            var logFile = ResolveLogFile(projectRoot, args);

            Directory.CreateDirectory(Path.GetDirectoryName(logFile) ?? projectRoot);
            Console.WriteLine("Mode:    unity");
            Console.WriteLine($"Unity:   {unityExe}");
            Console.WriteLine($"Project: {projectRoot}");
            Console.WriteLine($"Log:     {logFile}");

            var exitCode = RunUnity(unityExe, projectRoot, logFile);
            if (exitCode != 0)
                PrintLogTail(logFile, 160);

            return exitCode;
        }

        private static int RunUnity(string unityExe, string projectRoot, string logFile)
        {
            if (!File.Exists(unityExe) && unityExe.IndexOf(Path.DirectorySeparatorChar) >= 0)
            {
                Console.Error.WriteLine($"Unity executable not found: {unityExe}");
                return 2;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = unityExe,
                    Arguments = "-batchmode -nographics -quit " +
                                $"-projectPath {Quote(projectRoot)} " +
                                $"-executeMethod {UnityMethod} " +
                                $"-logFile {Quote(logFile)}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    Console.WriteLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    Console.Error.WriteLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            process.WaitForExit();
            return process.ExitCode;
        }

        private static string ResolveProjectRoot(string[] args)
        {
            var explicitRoot = FindArgumentValue(args, "--projectRoot");
            return Path.GetFullPath(string.IsNullOrWhiteSpace(explicitRoot)
                ? Directory.GetCurrentDirectory()
                : explicitRoot);
        }

        private static string ResolveMode(string[] args)
        {
            var explicitMode = FindArgumentValue(args, "--mode");
            return string.IsNullOrWhiteSpace(explicitMode)
                ? "sourcegen"
                : explicitMode;
        }

        private static string ResolveUnityExecutable(string projectRoot, string[] args)
        {
            var explicitUnity = FindArgumentValue(args, "--unity");
            if (!string.IsNullOrWhiteSpace(explicitUnity))
                return explicitUnity;

            var versionFile = Path.Combine(projectRoot, "ProjectSettings", "ProjectVersion.txt");
            if (File.Exists(versionFile))
            {
                foreach (var line in File.ReadLines(versionFile))
                {
                    const string prefix = "m_EditorVersion:";
                    if (!line.StartsWith(prefix, StringComparison.Ordinal))
                        continue;

                    var version = line.Substring(prefix.Length).Trim();
                    var localUnity = Path.Combine("E:\\Unity\\UnityEditor", version, "Editor", "Unity.exe");
                    if (File.Exists(localUnity))
                        return localUnity;
                }
            }

            return "Unity.exe";
        }

        private static string ResolveLogFile(string projectRoot, string[] args)
        {
            var explicitLog = FindArgumentValue(args, "--logFile");
            return Path.GetFullPath(string.IsNullOrWhiteSpace(explicitLog)
                ? Path.Combine(projectRoot, "Logs", "GASCodeGenCli.log")
                : explicitLog);
        }

        private static string FindArgumentValue(string[] args, string name)
        {
            if (args == null)
                return string.Empty;

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == name && i + 1 < args.Length)
                    return args[i + 1];

                var prefix = name + "=";
                if (arg != null && arg.StartsWith(prefix, StringComparison.Ordinal))
                    return arg.Substring(prefix.Length);
            }

            return string.Empty;
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void PrintLogTail(string logFile, int lineCount)
        {
            if (!File.Exists(logFile))
                return;

            Console.Error.WriteLine($"--- {logFile} tail ---");
            string[] lines;
            using (var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
                lines = reader.ReadToEnd()
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in lines.Reverse().Take(lineCount).Reverse())
                Console.Error.WriteLine(line);
        }

        private static void WriteException(Exception ex)
        {
            var current = ex;
            var depth = 0;
            while (current != null)
            {
                Console.Error.WriteLine($"[{depth}] {current.GetType().FullName}: {SafeString(() => current.Message)}");
                var stackTrace = SafeString(() => current.StackTrace);
                if (!string.IsNullOrWhiteSpace(stackTrace))
                    Console.Error.WriteLine(stackTrace);

                current = current.InnerException;
                depth++;
            }
        }

        private static string SafeString(Func<string> read)
        {
            try
            {
                return read() ?? string.Empty;
            }
            catch (Exception nested)
            {
                return $"<failed to read exception text: {nested.GetType().FullName}>";
            }
        }
    }
}
