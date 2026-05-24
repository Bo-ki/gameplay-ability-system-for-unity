using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GAS.Runtime
{
    public sealed class HeadlessAutoChessDemoSceneRunner : MonoBehaviour
    {
#if UNITY_EDITOR
        private const string EditorRunOptionsPath = "Temp/HeadlessAutoChessEditorSceneRun.options";
#endif

        [SerializeField] private bool runOnStart = true;
        [SerializeField] private int maxTicks = 128;
        [SerializeField] private int postVictoryFlushTicks = 4;
        [SerializeField] private bool exportLogs = true;
        [SerializeField] private string exportDirectory = "TestResults/AutoChess/T6-CHESS-AF-SceneRuntime";
        [SerializeField] private bool collectSystemTimings;
        [SerializeField] private bool quitPlayerOnComplete = true;
        [SerializeField] private bool stopEditorPlayModeOnComplete;

        public HeadlessAutoChessResult LastResult { get; private set; }

        private IEnumerator Start()
        {
            if (!runOnStart)
                yield break;

            yield return null;
            RunScenario();
        }

        [ContextMenu("Run Headless Auto Chess")]
        public void RunScenario()
        {
            var normalizedMaxTicks = maxTicks > 0 ? maxTicks : 128;
            var normalizedPostVictoryFlushTicks = postVictoryFlushTicks >= 0 ? postVictoryFlushTicks : 4;
            var normalizedExportDirectory = string.IsNullOrWhiteSpace(exportDirectory)
                ? Path.Combine("TestResults", "AutoChess", "T6-CHESS-AF-SceneRuntime")
                : exportDirectory;

            LastResult = HeadlessAutoChessScenario.RunDefault(
                new HeadlessAutoChessOptions(
                    normalizedMaxTicks,
                    normalizedPostVictoryFlushTicks,
                    exportLogs,
                    Path.GetFullPath(normalizedExportDirectory),
                    collectSystemTimings: ResolveCollectSystemTimings()));

            var exitCode = LastResult.Completed && LastResult.ValidationReport.Passed ? 0 : 1;
            var summary = string.Concat(
                "HeadlessAutoChessDemoSceneRunner|passed=",
                LastResult.ValidationReport.Passed ? "true" : "false",
                "|completed=",
                LastResult.Completed ? "true" : "false",
                "|battleTicks=",
                LastResult.BattleTicks.ToString(CultureInfo.InvariantCulture),
                "|measuredTicks=",
                LastResult.MeasuredTicks.ToString(CultureInfo.InvariantCulture),
                "|avgTickMs=",
                LastResult.AverageTickMilliseconds.ToString("0.########", CultureInfo.InvariantCulture),
                "|summary=",
                LastResult.ValidationReport.SummaryPath ?? string.Empty);

            Debug.Log(summary);
            Console.WriteLine(summary);

#if UNITY_EDITOR
            if (Application.isEditor && stopEditorPlayModeOnComplete && EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
                return;
            }
#endif

            if (!Application.isEditor && quitPlayerOnComplete)
                Application.Quit(exitCode);
        }

        private bool ResolveCollectSystemTimings()
        {
#if UNITY_EDITOR
            if (File.Exists(EditorRunOptionsPath))
            {
                var request = File.ReadAllText(EditorRunOptionsPath);
                File.Delete(EditorRunOptionsPath);
                if (request != null
                    && request.IndexOf("systemTiming", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
#endif

            return collectSystemTimings;
        }
    }
}
