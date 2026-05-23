using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GAS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace GAS.Editor
{
    [InitializeOnLoad]
    internal static class HeadlessAutoChessEditorSceneRunRequest
    {
        private const string ScenePath = "Assets/GAS/Runtime/Demo/AutoChess/HeadlessAutoChessDemo.unity";
        private const string FlagPath = "Temp/HeadlessAutoChessEditorSceneRun.flag";
        private const string OptionsPath = "Temp/HeadlessAutoChessEditorSceneRun.options";

        static HeadlessAutoChessEditorSceneRunRequest()
        {
            EditorApplication.update += TryConsumeRunRequest;
        }

        [MenuItem("GAS/AutoChess/Run Headless Demo Scene")]
        private static void RequestRunFromMenu()
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(FlagPath, "run");
        }

        private static void TryConsumeRunRequest()
        {
            if (!File.Exists(FlagPath))
                return;

            if (EditorApplication.isCompiling
                || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string request;
            try
            {
                request = File.ReadAllText(FlagPath);
                File.Delete(FlagPath);
            }
            catch (IOException)
            {
                return;
            }

            Directory.CreateDirectory("Temp");
            File.WriteAllText(OptionsPath, request ?? string.Empty);

            var scene = FindLoadedScene();
            if (!scene.IsValid())
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            if (scene.IsValid())
                EditorSceneManager.SetActiveScene(scene);

            EditorApplication.EnterPlaymode();
        }

        private static Scene FindLoadedScene()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.path == ScenePath)
                    return scene;
            }

            return default;
        }
    }

    public static class HeadlessAutoChessEditorValidationCommand
    {
        private const string OutputPathArgument = "-gasAutoChessOutput";
        private const string MaxTicksArgument = "-gasAutoChessMaxTicks";
        private const string ProfileWarmupTicksArgument = "-gasAutoChessProfileWarmupTicks";
        private const string SystemTimingArgument = "-gasAutoChessSystemTiming";
        private const string UnitScaleArgument = "-gasAutoChessUnitScale";
        private const string UnitScalesArgument = "-gasAutoChessUnitScales";
        private const string NoAssertionLogArgument = "-gasAutoChessNoAssertionLog";
        private const string NoTextLogsArgument = "-gasAutoChessNoTextLogs";
        private const string ProfileOnlyArgument = "-gasAutoChessProfileOnly";

        public static void RunDefaultFromCommandLine()
        {
            var exitCode = 0;
            try
            {
                var args = Environment.GetCommandLineArgs();
                var exportDirectory = ReadArgumentValue(
                    args,
                    OutputPathArgument,
                    Path.Combine("TestResults", "AutoChess", "T6-CHESS-AH-TypedFacts"));
                var collectSystemTimings = HasArgument(args, SystemTimingArgument);
                var maxTicks = ReadPositiveInt(args, MaxTicksArgument, 128);
                var unitScale = ReadPositiveInt(args, UnitScaleArgument, 1);
                var profileOnly = HasArgument(args, ProfileOnlyArgument);
                var captureAssertionLog = !profileOnly && !HasArgument(args, NoAssertionLogArgument);
                var exportTextLogs = !profileOnly && !HasArgument(args, NoTextLogsArgument);

                var result = HeadlessAutoChessScenario.RunDefault(
                    new HeadlessAutoChessOptions(
                        maxTicks: maxTicks,
                        postVictoryFlushTicks: 4,
                        exportLogs: true,
                        exportDirectory: Path.GetFullPath(exportDirectory),
                        collectSystemTimings: collectSystemTimings,
                        unitScale: unitScale,
                        captureAssertionLog: captureAssertionLog,
                        exportTextLogs: exportTextLogs));

                var summary = string.Concat(
                    "HeadlessAutoChessEditorValidation|passed=",
                    result.ValidationReport.Passed ? "true" : "false",
                    "|profileOnly=",
                    profileOnly ? "true" : "false",
                    "|completed=",
                    result.Completed ? "true" : "false",
                    "|units=",
                    result.Units.Length.ToString(CultureInfo.InvariantCulture),
                    "|unitScale=",
                    unitScale.ToString(CultureInfo.InvariantCulture),
                    "|exportTextLogs=",
                    exportTextLogs ? "true" : "false",
                    "|battleTicks=",
                    result.BattleTicks.ToString(CultureInfo.InvariantCulture),
                    "|measuredTicks=",
                    result.MeasuredTicks.ToString(CultureInfo.InvariantCulture),
                    "|avgTickMs=",
                    result.AverageTickMilliseconds.ToString("0.########", CultureInfo.InvariantCulture),
                    "|summary=",
                    result.ValidationReport.SummaryPath ?? string.Empty);
                UnityEngine.Debug.Log(summary);
                Console.WriteLine(summary);

                exitCode = profileOnly
                    ? result.MeasuredTicks > 0 ? 0 : 1
                    : result.Completed && result.ValidationReport.Passed ? 0 : 1;
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

        public static void RunScaleProfileFromCommandLine()
        {
            var exitCode = 0;
            try
            {
                var args = Environment.GetCommandLineArgs();
                var exportDirectory = Path.GetFullPath(ReadArgumentValue(
                    args,
                    OutputPathArgument,
                    Path.Combine("TestResults", "AutoChess", "T6-CHESS-AT-ScaleProfileBatch")));
                var collectSystemTimings = HasArgument(args, SystemTimingArgument);
                var maxTicks = ReadPositiveInt(args, MaxTicksArgument, 16);
                var scales = ReadScaleList(args);
                var warmupTicks = ReadPositiveInt(args, ProfileWarmupTicksArgument, 8);

                Directory.CreateDirectory(exportDirectory);
                var aggregatePath = Path.Combine(exportDirectory, "headless-autochess.scale-profile.txt");
                var builder = new StringBuilder(4096);
                builder.Append("HeadlessAutoChessScaleProfile")
                    .Append("|maxTicks=")
                    .Append(maxTicks.ToString(CultureInfo.InvariantCulture))
                    .Append("|warmupTicks=")
                    .Append(warmupTicks.ToString(CultureInfo.InvariantCulture))
                    .Append("|collectSystemTimings=")
                    .Append(collectSystemTimings ? "true" : "false")
                    .Append("|captureAssertionLog=false|exportTextLogs=false")
                    .AppendLine();

                HeadlessAutoChessScenario.RunDefault(
                    new HeadlessAutoChessOptions(
                        maxTicks: warmupTicks,
                        postVictoryFlushTicks: 0,
                        collectSystemTimings: collectSystemTimings,
                        unitScale: 1,
                        captureAssertionLog: false,
                        exportTextLogs: false));

                for (var i = 0; i < scales.Length; i++)
                {
                    var scale = scales[i];
                    var runExportDirectory = Path.Combine(
                        exportDirectory,
                        "x" + scale.ToString(CultureInfo.InvariantCulture));
                    var result = HeadlessAutoChessScenario.RunDefault(
                        new HeadlessAutoChessOptions(
                            maxTicks: maxTicks,
                            postVictoryFlushTicks: 4,
                            exportLogs: true,
                            exportDirectory: runExportDirectory,
                            collectSystemTimings: collectSystemTimings,
                            unitScale: scale,
                            captureAssertionLog: false,
                            exportTextLogs: false));

                    AppendScaleProfileRun(builder, scale, result);
                    AppendScaleProfileTopSystems(builder, scale, result.SystemTimings, 8);

                    if (result.MeasuredTicks <= 0)
                        exitCode = 1;
                }

                File.WriteAllText(aggregatePath, builder.ToString(), Encoding.UTF8);
                var summary = string.Concat(
                    "HeadlessAutoChessEditorScaleProfile|profileOnly=true|scales=",
                    JoinScales(scales),
                    "|maxTicks=",
                    maxTicks.ToString(CultureInfo.InvariantCulture),
                    "|collectSystemTimings=",
                    collectSystemTimings ? "true" : "false",
                    "|summary=",
                    aggregatePath);
                UnityEngine.Debug.Log(summary);
                Console.WriteLine(summary);
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

        private static string ReadArgumentValue(string[] args, string argument, string fallback)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return fallback;
        }

        private static bool HasArgument(string[] args, string argument)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static int ReadPositiveInt(string[] args, string argument, int fallback)
        {
            var value = ReadArgumentValue(args, argument, string.Empty);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                   && parsed > 0
                ? parsed
                : fallback;
        }

        private static int[] ReadScaleList(string[] args)
        {
            var fallback = HasArgument(args, UnitScaleArgument)
                ? new[] { ReadPositiveInt(args, UnitScaleArgument, 1) }
                : new[] { 1, 2, 5, 10 };
            var value = ReadArgumentValue(args, UnitScalesArgument, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            var parts = value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var scales = new List<int>(parts.Length);
            for (var i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var scale)
                    && scale > 0)
                {
                    scales.Add(scale);
                }
            }

            return scales.Count > 0 ? scales.ToArray() : fallback;
        }

        private static void AppendScaleProfileRun(
            StringBuilder builder,
            int scale,
            HeadlessAutoChessResult result)
        {
            var timing = result.RuntimeTiming;
            builder.Append("scaleProfile|scale=")
                .Append(scale.ToString(CultureInfo.InvariantCulture))
                .Append("|variant=")
                .Append(result.VariantName)
                .Append("|units=")
                .Append(result.Units.Length.ToString(CultureInfo.InvariantCulture))
                .Append("|completed=")
                .Append(result.Completed ? "true" : "false")
                .Append("|passed=")
                .Append(result.ValidationReport.Passed ? "true" : "false")
                .Append("|battleTicks=")
                .Append(result.BattleTicks.ToString(CultureInfo.InvariantCulture))
                .Append("|measuredTicks=")
                .Append(result.MeasuredTicks.ToString(CultureInfo.InvariantCulture))
                .Append("|avgTickMs=")
                .Append(FormatDouble(result.AverageTickMilliseconds))
                .Append("|commandAvgMs=")
                .Append(FormatDouble(timing.AverageCommandMilliseconds))
                .Append("|effectAvgMs=")
                .Append(FormatDouble(timing.AverageEffectMilliseconds))
                .Append("|attributeAvgMs=")
                .Append(FormatDouble(timing.AverageAttributeMilliseconds))
                .Append("|abilityAvgMs=")
                .Append(FormatDouble(timing.AverageAbilityMilliseconds))
                .Append("|cueAvgMs=")
                .Append(FormatDouble(timing.AverageCueMilliseconds))
                .Append("|replayEvents=")
                .Append(result.EventCounts.ReplayEvents.ToString(CultureInfo.InvariantCulture))
                .Append("|presentationOutboxEvents=")
                .Append(result.PresentationOutboxCounts.TotalEvents.ToString(CultureInfo.InvariantCulture))
                .Append("|summary=")
                .Append(result.ValidationReport.SummaryPath ?? string.Empty)
                .AppendLine();
        }

        private static void AppendScaleProfileTopSystems(
            StringBuilder builder,
            int scale,
            HeadlessAutoChessSystemTiming[] systemTimings,
            int maxRows)
        {
            if (systemTimings == null || systemTimings.Length == 0)
                return;

            var count = systemTimings.Length < maxRows ? systemTimings.Length : maxRows;
            for (var i = 0; i < count; i++)
            {
                var timing = systemTimings[i];
                builder.Append("scaleProfileSystem|scale=")
                    .Append(scale.ToString(CultureInfo.InvariantCulture))
                    .Append("|rank=")
                    .Append((i + 1).ToString(CultureInfo.InvariantCulture))
                    .Append("|group=")
                    .Append(timing.GroupName)
                    .Append("|system=")
                    .Append(timing.SystemName)
                    .Append("|calls=")
                    .Append(timing.CallCount.ToString(CultureInfo.InvariantCulture))
                    .Append("|avgMs=")
                    .Append(FormatDouble(timing.AverageMilliseconds))
                    .Append("|totalMs=")
                    .Append(FormatDouble(timing.TotalMilliseconds))
                    .AppendLine();
            }
        }

        private static string JoinScales(int[] scales)
        {
            if (scales == null || scales.Length == 0)
                return string.Empty;

            var builder = new StringBuilder();
            for (var i = 0; i < scales.Length; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append(scales[i].ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("0.########", CultureInfo.InvariantCulture);
        }
    }
}
