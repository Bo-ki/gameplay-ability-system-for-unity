using System;
using UnityEngine;

namespace GAS.AutoChessDemo
{
    public static class AutoChessRuntimeRunner
    {
        private const string RunArgument = "-gasAutoChessDemo";
        private static AutoChessGeneratedScenarioProfile ValidationScenario =>
            AutoChessGeneratedConfig.ValidationScenario;

        public static void RunAutoChessBattleOnce()
        {
            var runResult = RunAutoChessBattleForValidation();
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

            RunAutoChessBattleForValidation();
        }

        private static AutoChessValidationRunResult RunAutoChessBattleForValidation()
        {
            try
            {
                var runResult = AutoChessBattleValidationRun.RunHeadlessValidation(
                    ValidationScenario,
                    AutoChessLogPresentationOutboxBridge.Instance,
                    int.MaxValue);
                LogRunResult(runResult);
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
    }
}
