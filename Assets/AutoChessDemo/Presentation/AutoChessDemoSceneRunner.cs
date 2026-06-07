using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GAS.AutoChessDemo
{
    public sealed class AutoChessDemoSceneRunner : MonoBehaviour
    {
        private const int WarmupMaxTicks = 32;

        [SerializeField] private bool runOnStart = true;
        [SerializeField] private int maxTicks = 100000;
        [SerializeField] private int postVictoryFlushTicks = 4;
        [SerializeField] private int scenarioScale = 50;
        [SerializeField] private float scenarioHealthMultiplier = 2048f;
        [SerializeField] private float minimumProfileSeconds = 30f;
        [SerializeField] private bool captureOfficialToolDiff = true;
        [SerializeField] private bool exportLogs = true;
        [SerializeField] private string exportDirectory = "TestResults/AutoChess/T6-CHESS-AF-SceneRuntime";
        [SerializeField] private bool profilePlayMode = true;
        [SerializeField] private string profileCapturePath = "Temp/AutoChessDemo-PlayMode-X50-RuntimeProfile.data";
        [SerializeField] private int profilerPreRunFrames = 1;
        [SerializeField] private int profilerPostRunFrames = 1;
        [SerializeField] private bool quitPlayerOnComplete = true;
        [SerializeField] private bool stopEditorPlayModeOnComplete = true;
        [SerializeField] private bool showLogScreen = true;
        [SerializeField] private int logScreenMaxLines = 80;

        public bool HasResult { get; private set; }
        public AutoChessBattleResult LastResult { get; private set; }
        public AutoChessBattleResult LastDiagnosticResult { get; private set; }
        public AutoChessPresentationSnapshot LastPresentation { get; private set; }
        public AutoChessValidationEvidence LastValidationEvidence { get; private set; }

        private IAutoChessPresentationOutboxBridge presentationOutboxBridge =
            AutoChessLogPresentationOutboxBridge.Instance;
        private Vector2 logScrollPosition;
        private string logScreenStatus = "等待 AutoChess Demo 启动";
        private string[] logScreenLines = Array.Empty<string>();
        private GUIStyle logPanelStyle;
        private GUIStyle logTitleStyle;
        private GUIStyle logMetaStyle;
        private GUIStyle logLineStyle;

#if UNITY_EDITOR
        private bool hasPreviousProfilerState;
        private object previousProfileEditor;
        private object previousProfileGpu;
        private object previousDeepProfiling;
        private object previousProfilingEnabled;
        private bool previousProfilerEnabled;
        private bool previousBinaryLogEnabled;
        private string previousProfilerLogFile;
#endif

        private IEnumerator Start()
        {
            if (!runOnStart)
                yield break;

            yield return RunPlayModeProfile();
        }

        public IEnumerator RunPlayModeProfile()
        {
            var profilerStarted = false;
            var profilerCaptureCompleted = false;
            var profilerSummary = "profilerStarted=False";

            try
            {
                HasResult = false;
                SetLogScreenStatus("正在创建对局房间...");
                yield return null;

                AutoChessBattleValidationRun.RunWarmupPass(CreateWarmupOptions());
                SetLogScreenStatus("预热完成，正在运行正式战斗...");
                for (var i = 0; i < profilerPreRunFrames; i++)
                    yield return null;

                var profileHooks = profilePlayMode
                    ? new AutoChessBattleProfileHooks(
                        () =>
                        {
                            profilerStarted = TryBeginProfilerCapture();
                        },
                        () =>
                        {
                            if (!profilerStarted)
                                return;

                            profilerCaptureCompleted = true;
                        })
                    : default;

                var performanceResult = default(AutoChessBattleResult);
                yield return AutoChessBattleValidationRun.RunScenarioStepped(
                    CreatePerformanceOptions(),
                    profileHooks,
                    result => performanceResult = result);

                if (profilerCaptureCompleted)
                {
                    for (var i = 0; i < profilerPostRunFrames; i++)
                        yield return null;

                    StopProfilerCapture();
                    profilerStarted = false;
                    profilerSummary = SaveProfilerCapture();
                }

                var replayOptions = CreateReplayOptions();
                var diagnosticResult = AutoChessBattleValidationRun.RunDiagnosticPass(
                    performanceResult,
                    replayOptions);
                LastResult = captureOfficialToolDiff
                    ? AutoChessBattleValidationRun.RunOfficialDiffPass(
                        performanceResult,
                        replayOptions)
                    : performanceResult;
                LastDiagnosticResult = diagnosticResult;
                var validationRun = AutoChessBattleValidationRun.CreateRunResult(
                    AutoChessGeneratedConfig.ValidationScenario,
                    LastResult,
                    LastDiagnosticResult,
                    default,
                    false,
                    captureOfficialToolDiff,
                    presentationOutboxBridge,
                    logScreenMaxLines);
                LastPresentation = validationRun.Presentation;
                LastValidationEvidence = validationRun.Evidence;
                HasResult = true;
                UpdateLogScreen(LastPresentation);

                ValidateResult(validationRun);
                LogResult(validationRun, profilerSummary);
                if (exportLogs)
                    ExportResult(validationRun, profilerSummary);
            }
            finally
            {
                if (profilerStarted)
                    StopProfilerCapture();

                AutoChessBattleManager.ShutdownRuntime();
                RestoreProfilerState();
                CompletePlayMode();
            }

            yield break;
        }

        private AutoChessBattleOptions CreateWarmupOptions()
        {
            return new AutoChessBattleOptions(
                WarmupMaxTicks,
                postVictoryFlushTicks,
                scenarioScale,
                captureOfficialToolDiff: false,
                debuggerEnabled: false,
                captureSystemTimings: false,
                captureBufferPressure: false,
                healthMultiplier: 1f);
        }

        private AutoChessBattleOptions CreatePerformanceOptions()
        {
            return new AutoChessBattleOptions(
                maxTicks,
                postVictoryFlushTicks,
                scenarioScale,
                captureOfficialToolDiff: false,
                debuggerEnabled: false,
                captureSystemTimings: false,
                captureBufferPressure: false,
                healthMultiplier: scenarioHealthMultiplier,
                minimumBattleSeconds: minimumProfileSeconds);
        }

        private AutoChessBattleOptions CreateReplayOptions()
        {
            return new AutoChessBattleOptions(
                maxTicks,
                postVictoryFlushTicks,
                scenarioScale,
                captureOfficialToolDiff: false,
                debuggerEnabled: true,
                captureSystemTimings: true,
                captureBufferPressure: true,
                healthMultiplier: scenarioHealthMultiplier);
        }

        private static void ValidateResult(in AutoChessValidationRunResult runResult)
        {
            if (runResult.Passed)
            {
                return;
            }

            throw new InvalidOperationException(
                "AutoChessBattle PlayMode validation failed: "
                + AutoChessBattleValidationReport.CreateRunResultSummary(runResult)
                + " | "
                + AutoChessBattleValidationReport.CreateSummary(runResult.Evidence));
        }

        private static void LogResult(
            in AutoChessValidationRunResult runResult,
            string profilerSummary)
        {
            var result = runResult.PerformanceResult;
            var diagnosticResult = runResult.DiagnosticResult;
            var diagnosticWithOfficialDiff = diagnosticResult.WithOfficialToolDiff(result.OfficialToolDiff);
            Debug.Log("AutoChessDemoPlayModeRunnerPerformance: "
                      + AutoChessBattleValidationReport.CreateSummary(runResult.Evidence));
            Debug.Log("AutoChessDemoPlayModeValidationRunResult: "
                      + AutoChessBattleValidationReport.CreateRunResultSummary(runResult));
            Debug.Log("AutoChessDemoBattlePresentation: markers="
                      + runResult.Presentation.RuntimeMarkerCount
                      + ", displayedLines="
                      + runResult.Presentation.DisplayLineCount
                      + ", droppedLines="
                      + runResult.Presentation.DroppedLineCount
                      + ", disabledReason="
                      + runResult.Presentation.DisabledReason);
            Debug.Log("AutoChessDemoBattleLog:\n" + runResult.Presentation.ToText());
            Debug.Log("AutoChessDemoPlayModeTiming: "
                      + AutoChessBattleValidationReport.CreateTimingSummary(result, runResult.Evidence));
            Debug.Log("AutoChessDemoPlayModeDebugger: "
                      + AutoChessBattleValidationReport.CreateDebuggerSummary(diagnosticResult));
            Debug.Log("AutoChessDemoPlayModeOfficialToolDiff: "
                      + AutoChessBattleValidationReport.CreateOfficialToolDiffSummary(
                          result,
                          runResult.Evidence));
            Debug.Log("AutoChessDemoPlayModeProfiler: " + profilerSummary);
            Debug.Log("AutoChessDemoPlayModeDiagnosticRunner: "
                      + AutoChessBattleValidationReport.CreateSummary(
                          AutoChessBattleValidationReport.CreateEvidence(
                              AutoChessGeneratedConfig.ValidationScenario,
                              diagnosticWithOfficialDiff,
                              runResult.Presentation,
                              runResult.RequireOfficialToolDiff)));
            Debug.Log("AutoChessDemoPlayModeDataFlow:\n"
                      + AutoChessBattleValidationReport.CreateDataFlowDiagram(diagnosticWithOfficialDiff));
            Debug.Log("AutoChessDemoPlayModeSequence:\n"
                      + AutoChessBattleValidationReport.CreateSequenceDiagram(diagnosticWithOfficialDiff));
        }

        private void ExportResult(
            in AutoChessValidationRunResult runResult,
            string profilerSummary)
        {
            if (string.IsNullOrWhiteSpace(exportDirectory))
                return;

            Directory.CreateDirectory(exportDirectory);
            var result = runResult.PerformanceResult;
            var diagnosticResult = runResult.DiagnosticResult;
            var diagnosticWithOfficialDiff = diagnosticResult.WithOfficialToolDiff(result.OfficialToolDiff);
            var builder = new StringBuilder(1024);
            builder.AppendLine("AutoChessDemoPlayModeRunnerPerformance: "
                               + AutoChessBattleValidationReport.CreateSummary(runResult.Evidence));
            builder.AppendLine("AutoChessDemoPlayModeValidationRunResult: "
                               + AutoChessBattleValidationReport.CreateRunResultSummary(runResult));
            builder.AppendLine("AutoChessDemoPlayModeTiming: "
                               + AutoChessBattleValidationReport.CreateTimingSummary(result, runResult.Evidence));
            builder.AppendLine("AutoChessDemoPlayModeDebugger: "
                               + AutoChessBattleValidationReport.CreateDebuggerSummary(diagnosticResult));
            builder.AppendLine("AutoChessDemoPlayModeOfficialToolDiff: "
                               + AutoChessBattleValidationReport.CreateOfficialToolDiffSummary(
                                   result,
                                   runResult.Evidence));
            builder.AppendLine("AutoChessDemoBattlePresentation: markers="
                               + runResult.Presentation.RuntimeMarkerCount
                               + ", displayedLines="
                               + runResult.Presentation.DisplayLineCount
                               + ", droppedLines="
                               + runResult.Presentation.DroppedLineCount
                               + ", disabledReason="
                               + runResult.Presentation.DisabledReason);
            builder.AppendLine("AutoChessDemoPlayModeProfiler: " + profilerSummary);
            builder.AppendLine("AutoChessDemoPlayModeDiagnosticRunner: "
                               + AutoChessBattleValidationReport.CreateSummary(
                                   AutoChessBattleValidationReport.CreateEvidence(
                                       AutoChessGeneratedConfig.ValidationScenario,
                                       diagnosticWithOfficialDiff,
                                       runResult.Presentation,
                                       runResult.RequireOfficialToolDiff)));
            File.WriteAllText(
                Path.Combine(exportDirectory, "AutoChessPlayModeProfileSummary.txt"),
                builder.ToString());
            File.WriteAllText(
                Path.Combine(exportDirectory, "AutoChessBattleLog.txt"),
                runResult.Presentation.ToText());
        }

        private void OnGUI()
        {
            if (!showLogScreen)
                return;

            EnsureLogStyles();

            var margin = 18f;
            var panelRect = new Rect(
                margin,
                margin,
                Mathf.Max(320f, Screen.width - margin * 2f),
                Mathf.Max(220f, Screen.height - margin * 2f));

            GUILayout.BeginArea(panelRect, logPanelStyle);
            GUILayout.Label("AutoChess Demo 战斗日志", logTitleStyle);
            GUILayout.Label(logScreenStatus, logMetaStyle);
            logScrollPosition = GUILayout.BeginScrollView(logScrollPosition);
            for (var i = 0; i < logScreenLines.Length; i++)
                GUILayout.Label(logScreenLines[i], logLineStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void SetLogScreenStatus(string status)
        {
            logScreenStatus = status ?? string.Empty;
        }

        private void UpdateLogScreen(in AutoChessPresentationSnapshot presentation)
        {
            logScreenStatus = presentation.Status;
            var sourceLines = presentation.Lines ?? Array.Empty<AutoChessBattleLogLine>();
            logScreenLines = new string[sourceLines.Length];
            for (var i = 0; i < sourceLines.Length; i++)
                logScreenLines[i] = sourceLines[i].ToString();
            logScrollPosition = Vector2.zero;
        }

        private void EnsureLogStyles()
        {
            if (logPanelStyle != null)
                return;

            logPanelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(16, 16, 14, 14),
                alignment = TextAnchor.UpperLeft,
            };
            logTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            logMetaStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.78f, 0.86f, 1f, 1f) },
                wordWrap = true,
            };
            logLineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.92f, 0.94f, 0.96f, 1f) },
                wordWrap = true,
            };
        }

        private void CompletePlayMode()
        {
#if UNITY_EDITOR
            if (stopEditorPlayModeOnComplete)
                EditorApplication.isPlaying = false;
            else if (quitPlayerOnComplete && !Application.isEditor)
                Application.Quit();
#else
            if (quitPlayerOnComplete)
                Application.Quit();
#endif
        }

        private bool TryBeginProfilerCapture()
        {
#if UNITY_EDITOR
            try
            {
                CaptureProfilerState();
                var path = Path.GetFullPath(profileCapturePath);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                CallProfiler("SetMaxFrameHistoryLength", 512);
                CallProfiler("ClearAllFrames");
                SetProfilerProperty("profileEditor", false);
                SetProfilerProperty("profileGPU", false);
                SetProfilerProperty("deepProfiling", false);
                Profiler.logFile = path;
                Profiler.enableBinaryLog = true;
                Profiler.enabled = true;
                CallProfiler("SetProfilingEnabled", true);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("AutoChessDemoPlayModeProfiler: failed to start profiler capture: " + ex.Message);
                return false;
            }
#else
            return false;
#endif
        }

        private string SaveProfilerCapture()
        {
#if UNITY_EDITOR
            try
            {
                var path = Path.GetFullPath(profileCapturePath);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var firstFrame = GetProfilerIntProperty("firstFrameIndex");
                var lastFrame = GetProfilerIntProperty("lastFrameIndex");
                var driverSaved = firstFrame >= 0
                                  && lastFrame >= firstFrame
                                  && (bool)CallProfiler("SaveProfile", path);
                var binaryLogExists = File.Exists(path);
                var binaryLogBytes = binaryLogExists ? new FileInfo(path).Length : 0L;
                var saved = driverSaved || binaryLogBytes > 0L;
                return "saved=" + saved
                       + ", driverSaved=" + driverSaved
                       + ", binaryLogBytes=" + binaryLogBytes
                       + ", path=" + path
                       + ", firstFrameIndex=" + firstFrame
                       + ", lastFrameIndex=" + lastFrame
                       + ", profileEditor=" + GetProfilerProperty("profileEditor")
                       + ", profilingEnabled=" + CallProfiler("IsProfilingEnabled");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("AutoChessDemoPlayModeProfiler: failed to save profiler capture: " + ex.Message);
                return "saved=False, error=" + ex.Message;
            }
#else
            return "saved=False, reason=UnityEditor ProfilerDriver unavailable";
#endif
        }

        private static void StopProfilerCapture()
        {
#if UNITY_EDITOR
            try
            {
                CallProfiler("SetProfilingEnabled", false);
                Profiler.enabled = false;
                Profiler.enableBinaryLog = false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("AutoChessDemoPlayModeProfiler: failed to stop profiler capture: " + ex.Message);
            }
#endif
        }

#if UNITY_EDITOR
        private void CaptureProfilerState()
        {
            previousProfileEditor = GetProfilerProperty("profileEditor");
            previousProfileGpu = GetProfilerProperty("profileGPU");
            previousDeepProfiling = GetProfilerProperty("deepProfiling");
            previousProfilingEnabled = CallProfiler("IsProfilingEnabled");
            previousProfilerEnabled = Profiler.enabled;
            previousBinaryLogEnabled = Profiler.enableBinaryLog;
            previousProfilerLogFile = Profiler.logFile;
            hasPreviousProfilerState = true;
        }

        private void RestoreProfilerState()
        {
            if (!hasPreviousProfilerState)
                return;

            TryRestoreProfilerProperty("profileEditor", previousProfileEditor);
            TryRestoreProfilerProperty("profileGPU", previousProfileGpu);
            TryRestoreProfilerProperty("deepProfiling", previousDeepProfiling);
            Profiler.logFile = previousProfilerLogFile;
            Profiler.enableBinaryLog = previousBinaryLogEnabled;
            Profiler.enabled = previousProfilerEnabled;
            if (previousProfilingEnabled is bool wasProfiling)
            {
                try
                {
                    CallProfiler("SetProfilingEnabled", wasProfiling);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("AutoChessDemoPlayModeProfiler: failed to restore profiling state: " + ex.Message);
                }
            }

            hasPreviousProfilerState = false;
        }

        private static void TryRestoreProfilerProperty(string name, object value)
        {
            if (value == null)
                return;

            try
            {
                SetProfilerProperty(name, value);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("AutoChessDemoPlayModeProfiler: failed to restore profiler property " + name + ": " + ex.Message);
            }
        }

        private static object CallProfiler(string name, params object[] args)
        {
            var profilerType = typeof(EditorWindow).Assembly.GetType("UnityEditorInternal.ProfilerDriver");
            if (profilerType == null)
                throw new InvalidOperationException("UnityEditorInternal.ProfilerDriver not found.");

            var method = profilerType.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
                throw new MissingMethodException(profilerType.FullName, name);

            return method.Invoke(null, args);
        }

        private static object GetProfilerProperty(string name)
        {
            var profilerType = typeof(EditorWindow).Assembly.GetType("UnityEditorInternal.ProfilerDriver");
            if (profilerType == null)
                return null;

            var property = profilerType.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return property != null && property.CanRead ? property.GetValue(null) : null;
        }

        private static int GetProfilerIntProperty(string name)
        {
            var value = GetProfilerProperty(name);
            return value is int intValue ? intValue : -1;
        }

        private static void SetProfilerProperty(string name, object value)
        {
            var profilerType = typeof(EditorWindow).Assembly.GetType("UnityEditorInternal.ProfilerDriver");
            if (profilerType == null)
                return;

            var property = profilerType.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (property != null && property.CanWrite)
                property.SetValue(null, value);
        }
#else
        private void RestoreProfilerState()
        {
        }
#endif
    }
}
