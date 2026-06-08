using System;
using System.Globalization;
using System.IO;
using System.Text;
using GAS.Runtime;
using UnityEngine;

namespace GAS.AutoChessDemo
{
    public static class AutoChessRuntimeRunner
    {
        private const string RunArgument = "-gasAutoChessDemo";
        private const string DefaultBatchReportPath =
            "TestResults/AutoChess/Headless/AutoChessHeadlessValidationSummary.txt";
        private static AutoChessGeneratedScenarioProfile ValidationScenario =>
            AutoChessGeneratedConfig.ValidationScenario;

        public static void RunAutoChessBattleOnce()
        {
            var runResult = RunAutoChessBattleForValidation(
                AutoChessRuntimeRunnerOptions.FromCommandLine(
                    Application.isBatchMode ? DefaultBatchReportPath : string.Empty));
            if (runResult.Passed)
                return;

            throw new InvalidOperationException(
                "AutoChessDemo validation failed: "
                + AutoChessBattleValidationReport.CreateRunResultSummary(runResult)
                + " | "
                + AutoChessBattleValidationReport.CreateSummary(runResult.Evidence));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunOnPlayerLaunch()
        {
            if (!HasArgument(RunArgument))
                return;

            var runResult = RunAutoChessBattleForValidation(
                AutoChessRuntimeRunnerOptions.FromCommandLine(string.Empty));
            if (!runResult.Passed)
            {
                throw new InvalidOperationException(
                    "AutoChessDemo player validation failed: "
                    + AutoChessBattleValidationReport.CreateRunResultSummary(runResult)
                    + " | "
                    + AutoChessBattleValidationReport.CreateSummary(runResult.Evidence));
            }
        }

        private static AutoChessValidationRunResult RunAutoChessBattleForValidation(
            AutoChessRuntimeRunnerOptions options)
        {
            try
            {
                var scenario = options.ApplyTo(ValidationScenario);
                var runResult = AutoChessBattleValidationRun.RunHeadlessValidation(
                    scenario,
                    AutoChessLogPresentationOutboxBridge.Instance,
                    options.MaxPresentationLines);
                LogRunResult(runResult);
                WriteRunReport(runResult, options.ReportPath);
                return runResult;
            }
            finally
            {
                AutoChessBattleManager.ShutdownRuntime();
            }
        }

        private static void LogRunResult(in AutoChessValidationRunResult runResult)
        {
            var result = runResult.PerformanceResult;
            var diagnosticResult = runResult.DiagnosticResult;

            Debug.Log("AutoChessDemoRuntimeRunner: "
                      + AutoChessBattleValidationReport.CreateSummary(runResult.Evidence));
            Debug.Log("AutoChessDemoValidationRunResult: "
                      + AutoChessBattleValidationReport.CreateRunResultSummary(runResult));
            Debug.Log("AutoChessDemoRepeatRunEvidence: "
                      + AutoChessBattleValidationRun.CreateRepeatRunEvidenceSummary(runResult.RepeatRunEvidence));
            Debug.Log("AutoChessDemoHeadlessLogicBudget: "
                      + AutoChessBattleValidationReport.CreateHeadlessLogicBudgetSummary(
                          runResult.HeadlessLogicBudget));
            Debug.Log("AutoChessDemoRuntimeDataOrientedScorecard:\n"
                      + GasRuntimeDebugger.ExportDataOrientedScorecardToText(
                          runResult.HeadlessLogicBudget.RuntimeScorecard));
            Debug.Log("AutoChessDemoRuntimeHotspots: "
                      + AutoChessBattleValidationReport.CreateHotspotSummary(result, diagnosticResult));
            Debug.Log("AutoChessDemoBoundaryOwners: "
                      + AutoChessBattleValidationReport.CreateBoundaryOwnerSummary(result));
            Debug.Log("AutoChessDemoBoundaryReportKeys: "
                      + AutoChessBattleValidationReport.CreateBoundaryReportKeyCoverageSummary(
                          result.StructuredLogSnapshot));
            Debug.Log("AutoChessDemoBattlePresentation: "
                      + AutoChessBattleValidationReport.CreatePresentationSummary(runResult.Presentation));
            Debug.Log("AutoChessDemoRuntimeBattleLog:\n" + runResult.Presentation.ToText());
            Debug.Log("AutoChessDemoRuntimeDebugger: "
                      + AutoChessBattleValidationReport.CreateDebuggerSummary(diagnosticResult));
            Debug.Log("AutoChessDemoRuntimeTiming: "
                      + AutoChessBattleValidationReport.CreateTimingSummary(result, runResult.Evidence));
            Debug.Log("AutoChessDemoOfficialToolDiff: "
                      + AutoChessBattleValidationReport.CreateOfficialToolDiffSummary(result, runResult.Evidence));
            Debug.Log("AutoChessDemoRuntimeDataFlow:\n"
                      + AutoChessBattleValidationReport.CreateDataFlowDiagram(result));
            Debug.Log("AutoChessDemoRuntimeSequence:\n"
                      + AutoChessBattleValidationReport.CreateSequenceDiagram(result));
            Debug.Log("AutoChessDemoRuntimeDiagnostics:\n" + diagnosticResult.RuntimeDiagnosticsLog);
        }

        private static void WriteRunReport(
            in AutoChessValidationRunResult runResult,
            string reportPath)
        {
            if (string.IsNullOrWhiteSpace(reportPath))
                return;

            var fullPath = Path.GetFullPath(reportPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(
                fullPath,
                CreateRunReport(runResult),
                new UTF8Encoding(false));
            Debug.Log("AutoChessDemoRuntimeReport: path=" + fullPath);
        }

        private static string CreateRunReport(in AutoChessValidationRunResult runResult)
        {
            var result = runResult.PerformanceResult;
            var diagnosticResult = runResult.DiagnosticResult;
            var builder = new StringBuilder(2048);
            builder.AppendLine("AutoChessDemoHeadlessRunnerPerformance: "
                               + AutoChessBattleValidationReport.CreateSummary(runResult.Evidence));
            builder.AppendLine("AutoChessDemoHeadlessValidationRunResult: "
                               + AutoChessBattleValidationReport.CreateRunResultSummary(runResult));
            builder.AppendLine("AutoChessDemoHeadlessRepeatRunEvidence: "
                               + AutoChessBattleValidationRun.CreateRepeatRunEvidenceSummary(
                                   runResult.RepeatRunEvidence));
            builder.AppendLine("AutoChessDemoHeadlessLogicBudget: "
                               + AutoChessBattleValidationReport.CreateHeadlessLogicBudgetSummary(
                                   runResult.HeadlessLogicBudget));
            builder.AppendLine("AutoChessDemoHeadlessRuntimeDataOrientedScorecard:");
            builder.Append(
                GasRuntimeDebugger.ExportDataOrientedScorecardToText(
                    runResult.HeadlessLogicBudget.RuntimeScorecard));
            builder.AppendLine("AutoChessDemoHeadlessRuntimeHotspots: "
                               + AutoChessBattleValidationReport.CreateHotspotSummary(
                                   result,
                                   diagnosticResult));
            builder.AppendLine("AutoChessDemoHeadlessBoundaryOwners: "
                               + AutoChessBattleValidationReport.CreateBoundaryOwnerSummary(result));
            builder.AppendLine("AutoChessDemoHeadlessBoundaryReportKeys: "
                               + AutoChessBattleValidationReport.CreateBoundaryReportKeyCoverageSummary(
                                   result.StructuredLogSnapshot));
            builder.AppendLine("AutoChessDemoHeadlessBattlePresentation: "
                               + AutoChessBattleValidationReport.CreatePresentationSummary(
                                   runResult.Presentation));
            builder.AppendLine("AutoChessDemoHeadlessRuntimeDebugger: "
                               + AutoChessBattleValidationReport.CreateDebuggerSummary(diagnosticResult));
            builder.AppendLine("AutoChessDemoHeadlessRuntimeTiming: "
                               + AutoChessBattleValidationReport.CreateTimingSummary(
                                   result,
                                   runResult.Evidence));
            builder.AppendLine("AutoChessDemoHeadlessOfficialToolDiff: "
                               + AutoChessBattleValidationReport.CreateOfficialToolDiffSummary(
                                   result,
                                   runResult.Evidence));
            builder.AppendLine("AutoChessDemoHeadlessRuntimeBattleLog:");
            builder.Append(runResult.Presentation.ToText());
            return builder.ToString();
        }

        private static bool HasArgument(string argument)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private readonly struct AutoChessRuntimeRunnerOptions
        {
            private const string ScaleArgument = "-autoChessScale";
            private const string MaxTicksArgument = "-autoChessMaxTicks";
            private const string PostVictoryFlushTicksArgument = "-autoChessPostVictoryFlushTicks";
            private const string ProcessWarmupRunsArgument = "-autoChessProcessWarmupRuns";
            private const string HealthMultiplierArgument = "-autoChessHealthMultiplier";
            private const string MaxPresentationLinesArgument = "-autoChessMaxPresentationLines";
            private const string ReportPathArgument = "-autoChessReportPath";

            private readonly int m_scale;
            private readonly int m_maxTicks;
            private readonly int m_postVictoryFlushTicks;
            private readonly int m_processWarmupRuns;
            private readonly float m_healthMultiplier;

            public readonly int MaxPresentationLines;
            public readonly string ReportPath;

            private AutoChessRuntimeRunnerOptions(
                int scale,
                int maxTicks,
                int postVictoryFlushTicks,
                int processWarmupRuns,
                float healthMultiplier,
                int maxPresentationLines,
                string reportPath)
            {
                m_scale = scale;
                m_maxTicks = maxTicks;
                m_postVictoryFlushTicks = postVictoryFlushTicks;
                m_processWarmupRuns = processWarmupRuns;
                m_healthMultiplier = healthMultiplier;
                MaxPresentationLines = maxPresentationLines > 0 ? maxPresentationLines : int.MaxValue;
                ReportPath = reportPath ?? string.Empty;
            }

            public static AutoChessRuntimeRunnerOptions FromCommandLine(string defaultReportPath)
            {
                var args = Environment.GetCommandLineArgs();
                return new AutoChessRuntimeRunnerOptions(
                    GetInt(args, ScaleArgument, 0),
                    GetInt(args, MaxTicksArgument, 0),
                    GetInt(args, PostVictoryFlushTicksArgument, -1),
                    GetInt(args, ProcessWarmupRunsArgument, -1),
                    GetFloat(args, HealthMultiplierArgument, 0f),
                    GetInt(args, MaxPresentationLinesArgument, int.MaxValue),
                    GetString(args, ReportPathArgument, defaultReportPath));
            }

            public AutoChessGeneratedScenarioProfile ApplyTo(AutoChessGeneratedScenarioProfile scenario)
            {
                return new AutoChessGeneratedScenarioProfile(
                    m_scale > 0 ? m_scale : scenario.Scale,
                    m_maxTicks > 0 ? m_maxTicks : scenario.MaxTicks,
                    m_postVictoryFlushTicks >= 0
                        ? m_postVictoryFlushTicks
                        : scenario.PostVictoryFlushTicks,
                    m_processWarmupRuns >= 0 ? m_processWarmupRuns : scenario.ProcessWarmupRuns,
                    m_healthMultiplier > 0f ? m_healthMultiplier : scenario.HealthMultiplier,
                    scenario.ExpectedWinner,
                    scenario.MinDriverIssuedCommands,
                    scenario.MinAttributeChanges,
                    scenario.MinExecutionOutputs,
                    scenario.MinCueRequests,
                    scenario.MinActiveEffectSlots,
                    scenario.MinPeriodTickDamageFacts,
                    scenario.MinActiveMutationCommands,
                    scenario.MinActiveMutationOwnerGroups,
                    scenario.MaxActiveMutationEstimatedRandomLookups,
                    scenario.MaxActiveMutationOwnerResourceLookups,
                    scenario.MaxActiveMutationMigrationCarriers);
            }

            private static int GetInt(string[] args, string argument, int defaultValue)
            {
                var value = GetString(args, argument, string.Empty);
                if (string.IsNullOrWhiteSpace(value))
                    return defaultValue;

                return int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                    ? parsed
                    : defaultValue;
            }

            private static float GetFloat(string[] args, string argument, float defaultValue)
            {
                var value = GetString(args, argument, string.Empty);
                if (string.IsNullOrWhiteSpace(value))
                    return defaultValue;

                return float.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                    ? parsed
                    : defaultValue;
            }

            private static string GetString(string[] args, string argument, string defaultValue)
            {
                var prefix = argument + "=";
                for (var i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    if (string.Equals(arg, argument, StringComparison.OrdinalIgnoreCase))
                    {
                        return i + 1 < args.Length ? args[i + 1] : defaultValue;
                    }

                    if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return arg.Substring(prefix.Length);
                }

                return defaultValue ?? string.Empty;
            }
        }
    }
}
