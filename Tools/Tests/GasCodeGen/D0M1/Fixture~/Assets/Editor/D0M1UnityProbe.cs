using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEngine;

namespace GAS.Tests.D0M1
{
    /// <summary>
    /// D0-M1 隔离 Unity 工程入口，用于证明 selector 切换、跨域重载及三个程序集的物理来源保持同代。
    /// </summary>
    public static class D0M1UnityProbe
    {
        private const string OperationArgument = "-d0m1Operation";
        private const string OutputArgument = "-d0m1Output";
        private const string ExpectedGenerationArgument = "-d0m1ExpectedGeneration";
        private const string NextManifestArgument = "-d0m1NextManifest";
        private const string StateArgument = "-d0m1State";
        private const string ObserveRootOperation = "observe-root";
        private const string VerifySlotOperation = "verify-slot";
        private const string LiveSwitchOperation = "live-switch";
        private const string PackageName = "com.exhard.exgas.d0m1-slot";
        private const string RuntimeAssemblyName = "com.exhard.exgas.generated.runtime";
        private const string EditorAssemblyName = "com.exhard.exgas.generated.editor";
        private const string AutoChessAssemblyName = "com.exhard.exgas.autochessdemo";
        private const string RuntimeMarkerPath = "Runtime/GenerationMarker.cs";
        private const string EditorMarkerPath = "Editor/GenerationMarker.cs";
        private const string AutoChessMarkerPath = "AutoChessGenerated/GenerationMarker.cs";
        private const string RuntimeMarkerType = "D0M1.Generated.Runtime.GenerationMarker, com.exhard.exgas.generated.runtime";
        private const string EditorMarkerType = "D0M1.Generated.Editor.GenerationMarker, com.exhard.exgas.generated.editor";
        private const string AutoChessMarkerType = "D0M1.Generated.AutoChess.GenerationMarker, com.exhard.exgas.autochessdemo";
        private const string ReloadEpochKey = "GAS.Tests.D0M1.ReloadEpoch";
        private static readonly TimeSpan LiveSwitchTimeout = TimeSpan.FromMinutes(3);
        private static bool liveSwitchCompleting;

        /// <summary>
        /// 解析命令行并执行一次性探针；live-switch 会返回事件循环并由跨域回调完成。
        /// </summary>
        public static void Run()
        {
            string outputPath = GetRequiredArgument(OutputArgument);
            try
            {
                string operation = GetRequiredArgument(OperationArgument);
                if (operation == LiveSwitchOperation)
                {
                    StartLiveSwitch(outputPath);
                    return;
                }

                ProbeResult result = operation == ObserveRootOperation
                    ? ObserveInstallRoot()
                    : VerifySlot(operation);
                CompleteProbe(outputPath, result);
            }
            catch (Exception exception)
            {
                CompleteProbe(outputPath, ProbeResult.CreateFailure(exception.ToString()));
            }
        }

        /// <summary>
        /// 每次脚本域重载后推进持久 epoch，并仅在 live-switch 状态存在时恢复轮询。
        /// </summary>
        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            int reloadEpoch = SessionState.GetInt(ReloadEpochKey, 0) + 1;
            SessionState.SetInt(ReloadEpochKey, reloadEpoch);
            if (IsLiveSwitchCommand() && HasLiveSwitchState())
            {
                try
                {
                    LiveSwitchState state = ReadLiveSwitchState();
                    AppendTimeline(state, "ScriptsReloaded");
                    WriteLiveSwitchState(GetRequiredArgument(StateArgument), state);
                    ScheduleLiveSwitchTick();
                }
                catch (Exception exception)
                {
                    CompleteLiveSwitch(GetLiveSwitchOutputPath(), CreateLiveSwitchFailure(exception));
                }
            }
        }

        /// <summary>
        /// 在替换 selector 前落盘恢复状态，再原子替换 manifest 并请求 UPM 重新解析。
        /// </summary>
        private static void StartLiveSwitch(string outputPath)
        {
            LiveSwitchState state = new LiveSwitchState
            {
                OutputPath = outputPath,
                ExpectedGeneration = GetRequiredArgument(ExpectedGenerationArgument),
                StartedUtcTicks = DateTime.UtcNow.Ticks,
                ReloadEpochBefore = SessionState.GetInt(ReloadEpochKey, 0),
                Timeline = new[] { CaptureTimelineEntry("BeforeSelectorReplace") }
            };
            string statePath = GetRequiredArgument(StateArgument);
            WriteLiveSwitchState(statePath, state);
            ReplaceProjectManifest(GetRequiredArgument(NextManifestArgument));
            AppendTimeline(state, "SelectorReplaced");
            WriteLiveSwitchState(statePath, state);
            ScheduleLiveSwitchTick();
            Client.Resolve();
            AppendTimeline(state, "ResolveRequested");
            WriteLiveSwitchState(statePath, state);
        }

        /// <summary>
        /// 在 Editor 更新循环中等待真实域重载和 UPM/编译稳定，超时则显式失败退出。
        /// </summary>
        private static void TickLiveSwitch()
        {
            if (liveSwitchCompleting)
            {
                return;
            }

            try
            {
                LiveSwitchState state = ReadLiveSwitchState();
                if (DateTime.UtcNow - new DateTime(state.StartedUtcTicks, DateTimeKind.Utc) > LiveSwitchTimeout)
                {
                    throw new TimeoutException("Live selector switch timed out before a verified domain reload.");
                }

                int reloadEpochAfter = SessionState.GetInt(ReloadEpochKey, 0);
                if (reloadEpochAfter <= state.ReloadEpochBefore
                    || EditorApplication.isCompiling
                    || EditorApplication.isUpdating)
                {
                    return;
                }

                ProbeResult result = VerifySlot(LiveSwitchOperation);
                AppendTimeline(state, "Verified");
                WriteLiveSwitchState(GetRequiredArgument(StateArgument), state);
                result.DomainReloadObserved = true;
                result.ReloadEpochBefore = state.ReloadEpochBefore;
                result.ReloadEpochAfter = reloadEpochAfter;
                result.Timeline = state.Timeline;
                CompleteLiveSwitch(state.OutputPath, result);
            }
            catch (Exception exception)
            {
                CompleteLiveSwitch(GetLiveSwitchOutputPath(), CreateLiveSwitchFailure(exception));
            }
        }

        /// <summary>
        /// 记录 Unity 未经 materializer 直接打开工程后能否观察到缺失的唯一安装根。
        /// </summary>
        private static ProbeResult ObserveInstallRoot()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string installRoot = Path.Combine(projectRoot, "Assets", "GAS", "Generated", "CodeGen");
            bool rootExists = Directory.Exists(installRoot);
            return new ProbeResult
            {
                Operation = ObserveRootOperation,
                Passed = !rootExists,
                InstallRootExists = rootExists,
                Detail = rootExists
                    ? "直接 Unity 启动未观察到缺失根，不能形成 materializer 绕过反例。"
                    : "直接 Unity 启动已在未执行 materializer 的情况下观察到缺失根。"
            };
        }

        /// <summary>
        /// 核对三个程序集 marker、UPM 解析根及 marker 源文件均来自 selector 指向的同一 generation。
        /// </summary>
        private static ProbeResult VerifySlot(string operation)
        {
            if (operation != VerifySlotOperation && operation != LiveSwitchOperation)
            {
                throw new InvalidOperationException("Unsupported D0-M1 operation: " + operation);
            }

            string expectedGeneration = GetRequiredArgument(ExpectedGenerationArgument);
            string runtimeGeneration = ReadGenerationMarker(RuntimeMarkerType);
            string editorGeneration = ReadGenerationMarker(EditorMarkerType);
            string autoChessGeneration = ReadGenerationMarker(AutoChessMarkerType);
            SourceEvidence sourceEvidence = CollectSourceEvidence();
            bool passed = runtimeGeneration == expectedGeneration
                && editorGeneration == expectedGeneration
                && autoChessGeneration == expectedGeneration;

            return CreateVerificationResult(
                operation,
                expectedGeneration,
                runtimeGeneration,
                editorGeneration,
                autoChessGeneration,
                sourceEvidence,
                passed);
        }

        /// <summary>
        /// 构造包含 generation 与物理来源的验证结果，确保外部 harness 可独立复核。
        /// </summary>
        private static ProbeResult CreateVerificationResult(
            string operation,
            string expectedGeneration,
            string runtimeGeneration,
            string editorGeneration,
            string autoChessGeneration,
            SourceEvidence evidence,
            bool passed)
        {
            return new ProbeResult
            {
                Operation = operation,
                Passed = passed,
                ExpectedGeneration = expectedGeneration,
                RuntimeGeneration = runtimeGeneration,
                EditorGeneration = editorGeneration,
                AutoChessGeneration = autoChessGeneration,
                ResolvedPackagePath = evidence.ResolvedPackagePath,
                RuntimeSourceFiles = evidence.RuntimeSourceFiles,
                EditorSourceFiles = evidence.EditorSourceFiles,
                AutoChessSourceFiles = evidence.AutoChessSourceFiles,
                Timeline = new[] { CaptureTimelineEntry("Verified") },
                Detail = passed
                    ? "Runtime、Editor 与 AutoChess 三程序集消费同一 generation，且 marker 源文件来自选中 immutable slot。"
                    : "至少一个程序集未消费 selector 指向的 generation。"
            };
        }

        /// <summary>
        /// 从 PackageManager 与 CompilationPipeline 收集三程序集的实际物理来源并执行 fail-closed 校验。
        /// </summary>
        private static SourceEvidence CollectSourceEvidence()
        {
            string resolvedPackagePath = GetResolvedPackagePath();
            if (!Directory.Exists(resolvedPackagePath))
            {
                throw new InvalidOperationException("Resolved package path does not exist: " + resolvedPackagePath);
            }

            string[] runtimeSources = GetAssemblySourceFiles(RuntimeAssemblyName, resolvedPackagePath);
            string[] editorSources = GetAssemblySourceFiles(EditorAssemblyName, resolvedPackagePath);
            string[] autoChessSources = GetAssemblySourceFiles(AutoChessAssemblyName, resolvedPackagePath);
            EnsureMarkerSource(resolvedPackagePath, RuntimeMarkerPath, runtimeSources);
            EnsureMarkerSource(resolvedPackagePath, EditorMarkerPath, editorSources);
            EnsureMarkerSource(resolvedPackagePath, AutoChessMarkerPath, autoChessSources);
            return new SourceEvidence
            {
                ResolvedPackagePath = resolvedPackagePath,
                RuntimeSourceFiles = runtimeSources,
                EditorSourceFiles = editorSources,
                AutoChessSourceFiles = autoChessSources
            };
        }

        /// <summary>
        /// 查找 D0-M1 测试包的 PackageManager 实际解析根并规范化为绝对路径。
        /// </summary>
        private static string GetResolvedPackagePath()
        {
            UnityEditor.PackageManager.PackageInfo[] packages =
                UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            foreach (UnityEditor.PackageManager.PackageInfo package in packages)
            {
                if (package != null && string.Equals(package.name, PackageName, StringComparison.Ordinal))
                {
                    return Path.GetFullPath(package.resolvedPath);
                }
            }

            throw new InvalidOperationException("Resolved package was not registered: " + PackageName);
        }

        /// <summary>
        /// 读取指定程序集的全部源文件，并将 Unity 虚拟 package 路径映射为真实 slot 绝对路径。
        /// </summary>
        private static string[] GetAssemblySourceFiles(string assemblyName, string resolvedPackagePath)
        {
            UnityEditor.Compilation.Assembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            foreach (UnityEditor.Compilation.Assembly assembly in assemblies)
            {
                if (string.Equals(assembly.name, assemblyName, StringComparison.Ordinal))
                {
                    string[] sourceFiles = assembly.sourceFiles ?? Array.Empty<string>();
                    string[] normalizedFiles = new string[sourceFiles.Length];
                    for (int index = 0; index < sourceFiles.Length; index++)
                    {
                        normalizedFiles[index] = NormalizeSourcePath(sourceFiles[index], resolvedPackagePath);
                    }

                    return normalizedFiles;
                }
            }

            throw new InvalidOperationException("Compilation assembly was not found: " + assemblyName);
        }

        /// <summary>
        /// 将 CompilationPipeline 返回的相对或虚拟路径映射到可验证的绝对物理路径。
        /// </summary>
        private static string NormalizeSourcePath(string sourcePath, string resolvedPackagePath)
        {
            string normalized = sourcePath.Replace('\\', '/');
            string packagePrefix = "Packages/" + PackageName + "/";
            if (normalized.StartsWith(packagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                string relativePath = normalized.Substring(packagePrefix.Length)
                    .Replace('/', Path.DirectorySeparatorChar);
                return Path.GetFullPath(Path.Combine(resolvedPackagePath, relativePath));
            }

            if (Path.IsPathRooted(sourcePath))
            {
                return Path.GetFullPath(sourcePath);
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, sourcePath));
        }

        /// <summary>
        /// 强制 marker 文件存在于解析根内且确实被目标程序集编译，阻断缺失 slot 或陈旧 Library 旁路。
        /// </summary>
        private static void EnsureMarkerSource(string resolvedPackagePath, string markerRelativePath, string[] sourceFiles)
        {
            string markerPath = Path.GetFullPath(Path.Combine(
                resolvedPackagePath,
                markerRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            string rootPrefix = resolvedPackagePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!markerPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Generation marker source file escaped resolved package path: " + markerPath);
            }

            if (!File.Exists(markerPath))
            {
                throw new InvalidOperationException("Generation marker source file does not exist: " + markerPath);
            }

            foreach (string sourceFile in sourceFiles)
            {
                if (string.Equals(sourceFile, markerPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            throw new InvalidOperationException("Generation marker source file was not compiled from resolved package path: " + markerPath);
        }

        /// <summary>
        /// 将当前可观察 marker 与 UPM 解析根追加到跨域时间线，不让瞬态缺失中断证据采集。
        /// </summary>
        private static void AppendTimeline(LiveSwitchState state, string phase)
        {
            TimelineEntry entry = CaptureTimelineEntry(phase);
            TimelineEntry[] current = state.Timeline ?? Array.Empty<TimelineEntry>();
            TimelineEntry[] expanded = new TimelineEntry[current.Length + 1];
            Array.Copy(current, expanded, current.Length);
            expanded[current.Length] = entry;
            state.Timeline = expanded;
        }

        /// <summary>
        /// 捕获单个时间点的 marker triple 与绝对解析根；不可见字段以空字符串表示。
        /// </summary>
        private static TimelineEntry CaptureTimelineEntry(string phase)
        {
            return new TimelineEntry
            {
                Phase = phase,
                RuntimeGeneration = TryReadGenerationMarker(RuntimeMarkerType),
                EditorGeneration = TryReadGenerationMarker(EditorMarkerType),
                AutoChessGeneration = TryReadGenerationMarker(AutoChessMarkerType),
                ResolvedPackagePath = TryGetResolvedPackagePath()
            };
        }

        /// <summary>
        /// 尝试读取时间线 marker，类型在域切换瞬态不可见时返回空值而不伪造 generation。
        /// </summary>
        private static string TryReadGenerationMarker(string assemblyQualifiedTypeName)
        {
            try
            {
                return ReadGenerationMarker(assemblyQualifiedTypeName);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 尝试读取时间线解析根，并确保成功返回时始终是绝对路径。
        /// </summary>
        private static string TryGetResolvedPackagePath()
        {
            try
            {
                return GetResolvedPackagePath();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 通过程序集限定类型名读取固定 generation 字段，避免探针自身建立编译期依赖。
        /// </summary>
        private static string ReadGenerationMarker(string assemblyQualifiedTypeName)
        {
            Type markerType = Type.GetType(assemblyQualifiedTypeName, false);
            if (markerType == null)
            {
                throw new InvalidOperationException("Generation marker type was not loaded: " + assemblyQualifiedTypeName);
            }

            FieldInfo generationField = markerType.GetField("GenerationId", BindingFlags.Public | BindingFlags.Static);
            if (generationField == null)
            {
                throw new InvalidOperationException("Generation marker field is missing: " + assemblyQualifiedTypeName);
            }

            return generationField.GetValue(null) as string ?? string.Empty;
        }

        /// <summary>
        /// 将 next manifest 复制到目标同目录后用 File.Replace 原子切换 selector，并保留旧 selector 证据。
        /// </summary>
        private static void ReplaceProjectManifest(string nextManifestPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
            string stagedPath = manifestPath + ".d0m1.next";
            string backupPath = manifestPath + ".d0m1.backup";
            if (!File.Exists(nextManifestPath) || !File.Exists(manifestPath))
            {
                throw new FileNotFoundException("Selector manifest input is missing.");
            }

            if (File.Exists(stagedPath) || File.Exists(backupPath))
            {
                throw new IOException("Selector transaction residue already exists: " + stagedPath);
            }

            File.Copy(nextManifestPath, stagedPath, false);
            File.Replace(stagedPath, manifestPath, backupPath, true);
        }

        /// <summary>
        /// 注册唯一 Editor 更新回调，避免同一域内重复轮询 live-switch。
        /// </summary>
        private static void ScheduleLiveSwitchTick()
        {
            EditorApplication.update -= TickLiveSwitch;
            EditorApplication.update += TickLiveSwitch;
        }

        /// <summary>
        /// 读取 live-switch 的磁盘恢复状态，作为跨域后的唯一流程事实源。
        /// </summary>
        private static LiveSwitchState ReadLiveSwitchState()
        {
            string statePath = GetRequiredArgument(StateArgument);
            if (!File.Exists(statePath))
            {
                throw new FileNotFoundException("Live selector switch state file is missing.", statePath);
            }

            LiveSwitchState state = JsonUtility.FromJson<LiveSwitchState>(File.ReadAllText(statePath, Encoding.UTF8));
            if (state == null || string.IsNullOrWhiteSpace(state.OutputPath))
            {
                throw new InvalidOperationException("Live selector switch state file is invalid: " + statePath);
            }

            return state;
        }

        /// <summary>
        /// 以稳定 UTF-8 JSON 写出跨域恢复状态，保证 selector 切换前已具备恢复依据。
        /// </summary>
        private static void WriteLiveSwitchState(string statePath, LiveSwitchState state)
        {
            string directory = Path.GetDirectoryName(statePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Live selector switch state path must have a parent directory.");
            }

            Directory.CreateDirectory(directory);
            string json = JsonUtility.ToJson(state, true).Replace("\r\n", "\n") + "\n";
            File.WriteAllText(statePath, json, new UTF8Encoding(false));
        }

        /// <summary>
        /// 判断当前 Unity 进程是否由 live-switch 命令启动，防止普通 probe 被初始化回调误退出。
        /// </summary>
        private static bool IsLiveSwitchCommand()
        {
            string operation;
            return TryGetArgument(OperationArgument, out operation)
                && string.Equals(operation, LiveSwitchOperation, StringComparison.Ordinal);
        }

        /// <summary>
        /// 判断命令行指定的 live-switch 恢复文件是否已经落盘。
        /// </summary>
        private static bool HasLiveSwitchState()
        {
            string statePath;
            return TryGetArgument(StateArgument, out statePath) && File.Exists(statePath);
        }

        /// <summary>
        /// 尽力从恢复状态取得输出路径，使状态损坏时仍能写出显式失败结果。
        /// </summary>
        private static string GetLiveSwitchOutputPath()
        {
            try
            {
                return ReadLiveSwitchState().OutputPath;
            }
            catch
            {
                return GetRequiredArgument(OutputArgument);
            }
        }

        /// <summary>
        /// 构造携带已有时间线和 reload epoch 的 live-switch 失败结果，保留故障前证据。
        /// </summary>
        private static ProbeResult CreateLiveSwitchFailure(Exception exception)
        {
            ProbeResult result = ProbeResult.CreateFailure(exception.ToString());
            try
            {
                LiveSwitchState state = ReadLiveSwitchState();
                AppendTimeline(state, "Failed");
                WriteLiveSwitchState(GetRequiredArgument(StateArgument), state);
                result.Timeline = state.Timeline;
                result.ReloadEpochBefore = state.ReloadEpochBefore;
                result.ReloadEpochAfter = SessionState.GetInt(ReloadEpochKey, 0);
                result.DomainReloadObserved = result.ReloadEpochAfter > result.ReloadEpochBefore;
            }
            catch
            {
                result.Timeline = Array.Empty<TimelineEntry>();
            }

            return result;
        }

        /// <summary>
        /// 完成 live-switch 并注销轮询，确保最终结果只写一次。
        /// </summary>
        private static void CompleteLiveSwitch(string outputPath, ProbeResult result)
        {
            liveSwitchCompleting = true;
            EditorApplication.update -= TickLiveSwitch;
            CompleteProbe(outputPath, result);
        }

        /// <summary>
        /// 写出一次探针结果并以 Passed 映射进程退出码。
        /// </summary>
        private static void CompleteProbe(string outputPath, ProbeResult result)
        {
            WriteResult(outputPath, result);
            Debug.Log("D0-M1 Unity probe completed: " + result.Operation + ", Passed=" + result.Passed);
            EditorApplication.Exit(result.Passed ? 0 : 1);
        }

        /// <summary>
        /// 读取紧随指定名称后的命令行值，缺失或为空时显式失败。
        /// </summary>
        private static string GetRequiredArgument(string argumentName)
        {
            string value;
            if (TryGetArgument(argumentName, out value))
            {
                return value;
            }

            throw new ArgumentException("Required command-line argument is missing: " + argumentName);
        }

        /// <summary>
        /// 尝试读取紧随指定名称后的非空命令行值，供初始化回调安全判定当前 operation。
        /// </summary>
        private static bool TryGetArgument(string argumentName, out string value)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], argumentName, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(arguments[index + 1]))
                {
                    value = arguments[index + 1];
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        /// <summary>
        /// 以 UTF-8 无 BOM 和 LF 写出稳定 JSON 证据。
        /// </summary>
        private static void WriteResult(string outputPath, ProbeResult result)
        {
            string directory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Probe output path must have a parent directory.");
            }

            Directory.CreateDirectory(directory);
            string json = JsonUtility.ToJson(result, true).Replace("\r\n", "\n") + "\n";
            File.WriteAllText(outputPath, json, new UTF8Encoding(false));
        }
    }

    /// <summary>
    /// Unity 探针的可序列化结果，供外部 harness 复核 generation、域重载与程序集物理来源。
    /// </summary>
    [Serializable]
    internal sealed class ProbeResult
    {
        public string Operation;
        public bool Passed;
        public bool InstallRootExists;
        public string ExpectedGeneration;
        public string RuntimeGeneration;
        public string EditorGeneration;
        public string AutoChessGeneration;
        public string ResolvedPackagePath;
        public string[] RuntimeSourceFiles;
        public string[] EditorSourceFiles;
        public string[] AutoChessSourceFiles;
        public TimelineEntry[] Timeline;
        public bool DomainReloadObserved;
        public int ReloadEpochBefore;
        public int ReloadEpochAfter;
        public string Detail;

        /// <summary>
        /// 为入口异常创建显式失败结果，禁止静默吞掉 Unity 侧错误。
        /// </summary>
        public static ProbeResult CreateFailure(string detail)
        {
            return new ProbeResult
            {
                Operation = "failed",
                Passed = false,
                Detail = detail
            };
        }
    }

    /// <summary>
    /// 跨域切换的最小磁盘状态，避免依赖会随 AppDomain 销毁的静态字段。
    /// </summary>
    [Serializable]
    internal sealed class LiveSwitchState
    {
        public string OutputPath;
        public string ExpectedGeneration;
        public long StartedUtcTicks;
        public int ReloadEpochBefore;
        public TimelineEntry[] Timeline;
    }

    /// <summary>
    /// selector 切换期间单个可观察阶段的 marker triple 与 UPM 解析根快照。
    /// </summary>
    [Serializable]
    internal sealed class TimelineEntry
    {
        public string Phase;
        public string RuntimeGeneration;
        public string EditorGeneration;
        public string AutoChessGeneration;
        public string ResolvedPackagePath;
    }

    /// <summary>
    /// PackageManager 与 CompilationPipeline 的物理来源快照，用于构造外部可审计结果。
    /// </summary>
    internal sealed class SourceEvidence
    {
        public string ResolvedPackagePath;
        public string[] RuntimeSourceFiles;
        public string[] EditorSourceFiles;
        public string[] AutoChessSourceFiles;
    }
}
