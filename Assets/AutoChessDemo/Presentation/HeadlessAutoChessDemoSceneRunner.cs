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
    public sealed class HeadlessAutoChessDemoSceneRunner : MonoBehaviour
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

        public bool HasResult { get; private set; }
        public HeadlessAutoBattleResult LastResult { get; private set; }
        public HeadlessAutoBattleResult LastDiagnosticResult { get; private set; }

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
                yield return null;

                RunWarmupPass();

                var profileHooks = profilePlayMode
                    ? new HeadlessAutoBattleProfileHooks(
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

                var performanceResult = default(HeadlessAutoBattleResult);
                yield return HeadlessAutoBattleScenario.RunDefaultStepped(
                    new HeadlessAutoBattleOptions(
                        maxTicks,
                        postVictoryFlushTicks,
                        scenarioScale,
                        captureOfficialToolDiff: false,
                        debuggerEnabled: false,
                        captureSystemTimings: false,
                        captureBufferPressure: false,
                        healthMultiplier: scenarioHealthMultiplier,
                        minimumBattleSeconds: minimumProfileSeconds),
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

                var diagnosticResult = RunDiagnosticPass(performanceResult);
                LastResult = captureOfficialToolDiff
                    ? RunOfficialDiffPass(performanceResult)
                    : performanceResult;
                LastDiagnosticResult = diagnosticResult;
                HasResult = true;

                ValidateResult(LastResult, LastDiagnosticResult, captureOfficialToolDiff);
                LogResult(LastResult, LastDiagnosticResult, profilerSummary);
                if (exportLogs)
                    ExportResult(LastResult, LastDiagnosticResult, profilerSummary);
            }
            finally
            {
                if (profilerStarted)
                    StopProfilerCapture();

                HeadlessAutoBattleScenario.ShutdownRuntime();
                RestoreProfilerState();
                CompletePlayMode();
            }

            yield break;
        }

        private void RunWarmupPass()
        {
            try
            {
                HeadlessAutoBattleScenario.RunDefault(
                    new HeadlessAutoBattleOptions(
                        WarmupMaxTicks,
                        postVictoryFlushTicks,
                        scenarioScale,
                        captureOfficialToolDiff: false,
                        debuggerEnabled: false,
                        captureSystemTimings: false,
                        captureBufferPressure: false,
                        healthMultiplier: 1f));
            }
            finally
            {
                HeadlessAutoBattleScenario.ShutdownRuntime();
            }
        }

        private HeadlessAutoBattleResult RunDiagnosticPass(
            in HeadlessAutoBattleResult performanceResult)
        {
            HeadlessAutoBattleScenario.ShutdownRuntime();
            var replayTicks = Mathf.Max(1, performanceResult.BattleTicks);
            var diagnosticResult = HeadlessAutoBattleScenario.RunDefault(
                new HeadlessAutoBattleOptions(
                    replayTicks,
                    postVictoryFlushTicks,
                    scenarioScale,
                    captureOfficialToolDiff: false,
                    debuggerEnabled: true,
                    captureSystemTimings: true,
                    captureBufferPressure: true,
                    healthMultiplier: scenarioHealthMultiplier));
            HeadlessAutoChessRuntimeRunner.ValidateOfficialDiffRun(performanceResult, diagnosticResult);
            return diagnosticResult;
        }

        private HeadlessAutoBattleResult RunOfficialDiffPass(
            in HeadlessAutoBattleResult performanceResult)
        {
            HeadlessAutoBattleScenario.ShutdownRuntime();
            var replayTicks = Mathf.Max(1, performanceResult.BattleTicks);
            var officialDiffResult = HeadlessAutoBattleScenario.RunDefault(
                new HeadlessAutoBattleOptions(
                    replayTicks,
                    postVictoryFlushTicks,
                    scenarioScale,
                    captureOfficialToolDiff: true,
                    debuggerEnabled: false,
                    captureSystemTimings: false,
                    captureBufferPressure: false,
                    healthMultiplier: scenarioHealthMultiplier));
            HeadlessAutoChessRuntimeRunner.ValidateOfficialDiffRun(performanceResult, officialDiffResult);
            return performanceResult.WithOfficialToolDiff(officialDiffResult.OfficialToolDiff);
        }

        private static void ValidateResult(
            in HeadlessAutoBattleResult result,
            in HeadlessAutoBattleResult diagnosticResult,
            bool requireOfficialToolDiff)
        {
            if (result.Completed
                && result.DriverIssuedCommands > 0
                && result.EventCounts.AttributeChanges > 0
                && result.EventCounts.ExecutionCalculationOutputUpdated > 0
                && result.EventCounts.CueRequests > 0
                && diagnosticResult.RuntimeDiagnostics.EventCount > 0
                && !HeadlessAutoChessRuntimeRunner.HasBlockingDiagnosticErrors(diagnosticResult.RuntimeDiagnostics)
                && (!requireOfficialToolDiff || result.OfficialToolDiff.JournalingCaptured))
            {
                return;
            }

            throw new InvalidOperationException(
                "AutoBattle PlayMode validation failed: "
                + HeadlessAutoChessRuntimeRunner.CreateSummary(result));
        }

        private static void LogResult(
            in HeadlessAutoBattleResult result,
            in HeadlessAutoBattleResult diagnosticResult,
            string profilerSummary)
        {
            Debug.Log("HeadlessAutoChessPlayModeRunnerPerformance: "
                      + HeadlessAutoChessRuntimeRunner.CreateSummary(result));
            Debug.Log("HeadlessAutoChessPlayModeTiming: "
                      + HeadlessAutoChessRuntimeRunner.CreateTimingSummary(result));
            Debug.Log("HeadlessAutoChessPlayModeDebugger: "
                      + HeadlessAutoChessRuntimeRunner.CreateDebuggerSummary(diagnosticResult));
            Debug.Log("HeadlessAutoChessPlayModeOfficialToolDiff: "
                      + HeadlessAutoChessRuntimeRunner.CreateOfficialToolDiffSummary(result));
            Debug.Log("HeadlessAutoChessPlayModeProfiler: " + profilerSummary);
            Debug.Log("HeadlessAutoChessPlayModeDiagnosticRunner: "
                      + HeadlessAutoChessRuntimeRunner.CreateSummary(diagnosticResult));
            var diagnosticWithOfficialDiff = diagnosticResult.WithOfficialToolDiff(result.OfficialToolDiff);
            Debug.Log("HeadlessAutoChessPlayModeDataFlow:\n"
                      + HeadlessAutoChessRuntimeRunner.CreateDataFlowDiagram(diagnosticWithOfficialDiff));
            Debug.Log("HeadlessAutoChessPlayModeSequence:\n"
                      + HeadlessAutoChessRuntimeRunner.CreateSequenceDiagram(diagnosticWithOfficialDiff));
        }

        private void ExportResult(
            in HeadlessAutoBattleResult result,
            in HeadlessAutoBattleResult diagnosticResult,
            string profilerSummary)
        {
            if (string.IsNullOrWhiteSpace(exportDirectory))
                return;

            Directory.CreateDirectory(exportDirectory);
            var builder = new StringBuilder(1024);
            builder.AppendLine("HeadlessAutoChessPlayModeRunnerPerformance: "
                               + HeadlessAutoChessRuntimeRunner.CreateSummary(result));
            builder.AppendLine("HeadlessAutoChessPlayModeTiming: "
                               + HeadlessAutoChessRuntimeRunner.CreateTimingSummary(result));
            builder.AppendLine("HeadlessAutoChessPlayModeDebugger: "
                               + HeadlessAutoChessRuntimeRunner.CreateDebuggerSummary(diagnosticResult));
            builder.AppendLine("HeadlessAutoChessPlayModeOfficialToolDiff: "
                               + HeadlessAutoChessRuntimeRunner.CreateOfficialToolDiffSummary(result));
            builder.AppendLine("HeadlessAutoChessPlayModeProfiler: " + profilerSummary);
            builder.AppendLine("HeadlessAutoChessPlayModeDiagnosticRunner: "
                               + HeadlessAutoChessRuntimeRunner.CreateSummary(diagnosticResult));
            File.WriteAllText(
                Path.Combine(exportDirectory, "AutoChessPlayModeProfileSummary.txt"),
                builder.ToString());
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
                Debug.LogWarning("HeadlessAutoChessPlayModeProfiler: failed to start profiler capture: " + ex.Message);
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
                Debug.LogWarning("HeadlessAutoChessPlayModeProfiler: failed to save profiler capture: " + ex.Message);
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
                Debug.LogWarning("HeadlessAutoChessPlayModeProfiler: failed to stop profiler capture: " + ex.Message);
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
                    Debug.LogWarning("HeadlessAutoChessPlayModeProfiler: failed to restore profiling state: " + ex.Message);
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
                Debug.LogWarning("HeadlessAutoChessPlayModeProfiler: failed to restore profiler property " + name + ": " + ex.Message);
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
