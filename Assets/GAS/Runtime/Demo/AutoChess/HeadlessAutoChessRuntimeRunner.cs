using System;
using System.Globalization;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GAS.Runtime
{
    public static class HeadlessAutoChessRuntimeRunner
    {
        private const string RunArgument = "-gasAutoChessHeadless";
        private const string ExportDirectoryArgument = "-gasAutoChessExportDirectory";
        private const string MaxTicksArgument = "-gasAutoChessMaxTicks";
        private const string SystemTimingArgument = "-gasAutoChessSystemTiming";
        private const string UnitScaleArgument = "-gasAutoChessUnitScale";
        private const string NoAssertionLogArgument = "-gasAutoChessNoAssertionLog";
        private const string NoTextLogsArgument = "-gasAutoChessNoTextLogs";
        private const string ProfileOnlyArgument = "-gasAutoChessProfileOnly";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunOnPlayerLaunch()
        {
            if (!HasArgument(RunArgument))
                return;

            RunFromCommandLine();
        }

        public static void RunFromCommandLine()
        {
            var exitCode = 0;
            try
            {
                var result = Run();
                exitCode = HasArgument(ProfileOnlyArgument)
                    ? result.MeasuredTicks > 0 ? 0 : 1
                    : result.Completed && result.ValidationReport.Passed ? 0 : 1;
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Debug.LogException(ex);
                Console.Error.WriteLine(ex);
            }
            finally
            {
                Quit(exitCode);
            }
        }

        public static HeadlessAutoChessResult Run()
        {
            var args = Environment.GetCommandLineArgs();
            var exportDirectory = ReadArgumentValue(
                args,
                ExportDirectoryArgument,
                Path.Combine("TestResults", "AutoChess", "T6-CHESS-AF-RuntimePlayer"));
            var maxTicks = ReadPositiveInt(args, MaxTicksArgument, 128);
            var collectSystemTimings = HasArgument(args, SystemTimingArgument);
            var unitScale = ReadPositiveInt(args, UnitScaleArgument, 1);
            var profileOnly = HasArgument(args, ProfileOnlyArgument);
            var captureAssertionLog = !profileOnly && !HasArgument(args, NoAssertionLogArgument);
            var exportTextLogs = !profileOnly && !HasArgument(args, NoTextLogsArgument);

            var result = HeadlessAutoChessScenario.RunDefault(
                new HeadlessAutoChessOptions(
                    maxTicks,
                    postVictoryFlushTicks: 4,
                    exportLogs: true,
                    exportDirectory: Path.GetFullPath(exportDirectory),
                    collectSystemTimings: collectSystemTimings,
                    unitScale: unitScale,
                    captureAssertionLog: captureAssertionLog,
                    exportTextLogs: exportTextLogs));

            var summary = string.Concat(
                "HeadlessAutoChessRuntimeRunner|passed=",
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

            Debug.Log(summary);
            Console.WriteLine(summary);
            return result;
        }

        private static bool HasArgument(string argument)
        {
            return HasArgument(Environment.GetCommandLineArgs(), argument);
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

        private static string ReadArgumentValue(string[] args, string argument, string fallback)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return fallback;
        }

        private static int ReadPositiveInt(string[] args, string argument, int fallback)
        {
            var value = ReadArgumentValue(args, argument, string.Empty);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                   && parsed > 0
                ? parsed
                : fallback;
        }

        private static void Quit(int exitCode)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.Exit(exitCode);
                return;
            }
#endif

            Application.Quit(exitCode);
        }
    }
}
