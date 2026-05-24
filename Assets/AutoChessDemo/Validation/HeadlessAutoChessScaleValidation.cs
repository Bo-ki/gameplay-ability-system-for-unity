using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GAS.Runtime
{
    public readonly struct HeadlessAutoChessScaleValidationOptions
    {
        public readonly int RunCount;
        public readonly bool ExportLogs;
        public readonly string ExportDirectory;
        public readonly bool RequireDeterministicOutcome;
        public readonly double MaxAverageTickMilliseconds;
        public readonly double MaxRunAverageTickMilliseconds;
        public readonly HeadlessAutoChessOptions ScenarioOptions;
        public readonly HeadlessAutoChessScenarioVariant[] Variants;

        public HeadlessAutoChessScaleValidationOptions(
            int runCount,
            bool exportLogs = false,
            string exportDirectory = null,
            bool requireDeterministicOutcome = true,
            double maxAverageTickMilliseconds = 0d,
            double maxRunAverageTickMilliseconds = 0d,
            HeadlessAutoChessOptions scenarioOptions = default,
            HeadlessAutoChessScenarioVariant[] variants = null)
        {
            RunCount = runCount;
            ExportLogs = exportLogs;
            ExportDirectory = exportDirectory;
            RequireDeterministicOutcome = requireDeterministicOutcome;
            MaxAverageTickMilliseconds = maxAverageTickMilliseconds;
            MaxRunAverageTickMilliseconds = maxRunAverageTickMilliseconds;
            ScenarioOptions = scenarioOptions;
            Variants = CopyVariants(variants);
        }

        public HeadlessAutoChessScaleValidationOptions Normalize()
        {
            var scenario = ScenarioOptions.Normalize();
            var scenarioThresholds = scenario.ValidationThresholds.Normalize();
            var variants = NormalizeVariants(Variants);
            return new HeadlessAutoChessScaleValidationOptions(
                RunCount > 0 ? RunCount : 8,
                ExportLogs,
                ExportDirectory,
                RequireDeterministicOutcome,
                MaxAverageTickMilliseconds > 0d
                    ? MaxAverageTickMilliseconds
                    : scenarioThresholds.MaxAverageTickMilliseconds,
                MaxRunAverageTickMilliseconds > 0d
                    ? MaxRunAverageTickMilliseconds
                    : scenarioThresholds.MaxAverageTickMilliseconds,
                scenario,
                variants);
        }

        private static HeadlessAutoChessScenarioVariant[] NormalizeVariants(
            HeadlessAutoChessScenarioVariant[] variants)
        {
            if (variants == null || variants.Length == 0)
                return new[] { HeadlessAutoChessScenarioVariant.DefaultBalanced };

            var normalized = new List<HeadlessAutoChessScenarioVariant>(variants.Length);
            for (var i = 0; i < variants.Length; i++)
            {
                var variant = HeadlessAutoChessScenario.GetVariantDefinition(variants[i]).Variant;
                if (!ContainsVariant(normalized, variant))
                    normalized.Add(variant);
            }

            return normalized.Count == 0
                ? new[] { HeadlessAutoChessScenarioVariant.DefaultBalanced }
                : normalized.ToArray();
        }

        private static bool ContainsVariant(
            List<HeadlessAutoChessScenarioVariant> variants,
            HeadlessAutoChessScenarioVariant candidate)
        {
            for (var i = 0; i < variants.Count; i++)
            {
                if (variants[i] == candidate)
                    return true;
            }

            return false;
        }

        private static HeadlessAutoChessScenarioVariant[] CopyVariants(
            HeadlessAutoChessScenarioVariant[] variants)
        {
            if (variants == null || variants.Length == 0)
                return Array.Empty<HeadlessAutoChessScenarioVariant>();

            var copy = new HeadlessAutoChessScenarioVariant[variants.Length];
            Array.Copy(variants, copy, variants.Length);
            return copy;
        }
    }

    public readonly struct HeadlessAutoChessScaleRunResult
    {
        public readonly int RunIndex;
        public readonly int VariantRunIndex;
        public readonly HeadlessAutoChessScenarioVariant Variant;
        public readonly string VariantName;
        public readonly int DeterministicSeed;
        public readonly string DeterminismSignature;
        public readonly string ExportDirectory;
        public readonly HeadlessAutoChessResult Result;

        public HeadlessAutoChessScaleRunResult(
            int runIndex,
            int variantRunIndex,
            HeadlessAutoChessScenarioVariant variant,
            string variantName,
            int deterministicSeed,
            string determinismSignature,
            string exportDirectory,
            HeadlessAutoChessResult result)
        {
            RunIndex = runIndex;
            VariantRunIndex = variantRunIndex;
            Variant = variant;
            VariantName = variantName ?? string.Empty;
            DeterministicSeed = deterministicSeed;
            DeterminismSignature = determinismSignature ?? string.Empty;
            ExportDirectory = exportDirectory ?? string.Empty;
            Result = result;
        }
    }

    public readonly struct HeadlessAutoChessScaleVariantResult
    {
        public readonly HeadlessAutoChessScenarioVariant Variant;
        public readonly string VariantName;
        public readonly int DeterministicSeed;
        public readonly int PlayerUnitCount;
        public readonly int EnemyUnitCount;
        public readonly HeadlessAutoChessTeam ExpectedWinner;
        public readonly int RunCount;
        public readonly int PassedRunCount;
        public readonly int FailedRunCount;
        public readonly bool Deterministic;
        public readonly string ReferenceDeterminismSignature;
        public readonly int TotalReplayEvents;
        public readonly int TotalStructuredLogEntries;

        public HeadlessAutoChessScaleVariantResult(
            HeadlessAutoChessScenarioVariant variant,
            string variantName,
            int deterministicSeed,
            int playerUnitCount,
            int enemyUnitCount,
            HeadlessAutoChessTeam expectedWinner,
            int runCount,
            int passedRunCount,
            int failedRunCount,
            bool deterministic,
            string referenceDeterminismSignature,
            int totalReplayEvents,
            int totalStructuredLogEntries)
        {
            Variant = variant;
            VariantName = variantName ?? string.Empty;
            DeterministicSeed = deterministicSeed;
            PlayerUnitCount = playerUnitCount;
            EnemyUnitCount = enemyUnitCount;
            ExpectedWinner = expectedWinner;
            RunCount = runCount;
            PassedRunCount = passedRunCount;
            FailedRunCount = failedRunCount;
            Deterministic = deterministic;
            ReferenceDeterminismSignature = referenceDeterminismSignature ?? string.Empty;
            TotalReplayEvents = totalReplayEvents;
            TotalStructuredLogEntries = totalStructuredLogEntries;
        }
    }

    public readonly struct HeadlessAutoChessScaleValidationReport
    {
        public readonly bool Passed;
        public readonly int FailureCount;
        public readonly string SummaryText;
        public readonly string[] FailureMessages;
        public readonly string SummaryPath;
        public readonly long SummaryByteCount;

        public HeadlessAutoChessScaleValidationReport(
            bool passed,
            int failureCount,
            string summaryText,
            string[] failureMessages,
            string summaryPath,
            long summaryByteCount)
        {
            Passed = passed;
            FailureCount = failureCount;
            SummaryText = summaryText ?? string.Empty;
            FailureMessages = failureMessages ?? Array.Empty<string>();
            SummaryPath = summaryPath ?? string.Empty;
            SummaryByteCount = summaryByteCount;
        }
    }

    public readonly struct HeadlessAutoChessScaleValidationResult
    {
        public readonly int RunCount;
        public readonly int PassedRunCount;
        public readonly int FailedRunCount;
        public readonly bool Deterministic;
        public readonly string ReferenceDeterminismSignature;
        public readonly int TotalBattleTicks;
        public readonly int TotalTicks;
        public readonly int TotalMeasuredTicks;
        public readonly int TotalReplayEvents;
        public readonly int TotalStructuredLogEntries;
        public readonly double TotalElapsedMilliseconds;
        public readonly double AverageTickMilliseconds;
        public readonly double MaxRunAverageTickMilliseconds;
        public readonly HeadlessAutoChessRuntimeTickTiming RuntimeTiming;
        public readonly HeadlessAutoChessScaleVariantResult[] Variants;
        public readonly HeadlessAutoChessScaleRunResult[] Runs;
        public readonly HeadlessAutoChessScaleValidationReport Report;

        public HeadlessAutoChessScaleValidationResult(
            int runCount,
            int passedRunCount,
            int failedRunCount,
            bool deterministic,
            string referenceDeterminismSignature,
            int totalBattleTicks,
            int totalTicks,
            int totalMeasuredTicks,
            int totalReplayEvents,
            int totalStructuredLogEntries,
            double totalElapsedMilliseconds,
            double maxRunAverageTickMilliseconds,
            in HeadlessAutoChessRuntimeTickTiming runtimeTiming,
            HeadlessAutoChessScaleVariantResult[] variants,
            HeadlessAutoChessScaleRunResult[] runs,
            HeadlessAutoChessScaleValidationReport report)
        {
            RunCount = runCount;
            PassedRunCount = passedRunCount;
            FailedRunCount = failedRunCount;
            Deterministic = deterministic;
            ReferenceDeterminismSignature = referenceDeterminismSignature ?? string.Empty;
            TotalBattleTicks = totalBattleTicks;
            TotalTicks = totalTicks;
            TotalMeasuredTicks = totalMeasuredTicks;
            TotalReplayEvents = totalReplayEvents;
            TotalStructuredLogEntries = totalStructuredLogEntries;
            TotalElapsedMilliseconds = totalElapsedMilliseconds;
            AverageTickMilliseconds = totalMeasuredTicks > 0
                ? totalElapsedMilliseconds / totalMeasuredTicks
                : totalBattleTicks > 0
                    ? totalElapsedMilliseconds / totalBattleTicks
                    : totalTicks > 0
                        ? totalElapsedMilliseconds / totalTicks
                        : 0d;
            MaxRunAverageTickMilliseconds = maxRunAverageTickMilliseconds;
            RuntimeTiming = runtimeTiming;
            Variants = variants ?? Array.Empty<HeadlessAutoChessScaleVariantResult>();
            Runs = runs ?? Array.Empty<HeadlessAutoChessScaleRunResult>();
            Report = report;
        }
    }

    public static class HeadlessAutoChessScaleValidation
    {
        public static HeadlessAutoChessScaleValidationResult Run(
            HeadlessAutoChessScaleValidationOptions options = default)
        {
            var normalized = options.Normalize();
            var variants = normalized.Variants;
            var runs = new HeadlessAutoChessScaleRunResult[normalized.RunCount * variants.Length];
            var exportDirectory = ResolveScaleExportDirectory(normalized.ExportDirectory);
            var runIndex = 0;

            for (var variantIndex = 0; variantIndex < variants.Length; variantIndex++)
            {
                var variant = variants[variantIndex];
                var variantDefinition = HeadlessAutoChessScenario.GetVariantDefinition(variant);
                var variantExportToken = CreateExportToken(variantDefinition.Name);

                for (var variantRunIndex = 0; variantRunIndex < normalized.RunCount; variantRunIndex++)
                {
                    var runExportDirectory = normalized.ExportLogs
                        ? Path.Combine(
                            exportDirectory,
                            variantExportToken,
                            "run-" + variantRunIndex.ToString("D3", CultureInfo.InvariantCulture))
                        : string.Empty;
                    var runOptions = CreateRunOptions(normalized, runExportDirectory);
                    var result = HeadlessAutoChessScenario.RunVariant(variantDefinition.Variant, runOptions);
                    var signature = CreateDeterminismSignature(result);
                    runs[runIndex] = new HeadlessAutoChessScaleRunResult(
                        runIndex,
                        variantRunIndex,
                        variantDefinition.Variant,
                        variantDefinition.Name,
                        variantDefinition.DeterministicSeed,
                        signature,
                        runExportDirectory,
                        result);
                    runIndex++;
                }
            }

            return BuildResult(runs, normalized, exportDirectory);
        }

        private static HeadlessAutoChessOptions CreateRunOptions(
            in HeadlessAutoChessScaleValidationOptions options,
            string runExportDirectory)
        {
            var scenario = options.ScenarioOptions.Normalize();
            return new HeadlessAutoChessOptions(
                scenario.MaxTicks,
                scenario.PostVictoryFlushTicks,
                options.ExportLogs,
                runExportDirectory,
                scenario.ValidationThresholds,
                scenario.CollectSystemTimings,
                scenario.UnitScale,
                scenario.CaptureAssertionLog,
                scenario.ExportTextLogs);
        }

        private static HeadlessAutoChessScaleValidationResult BuildResult(
            HeadlessAutoChessScaleRunResult[] runs,
            in HeadlessAutoChessScaleValidationOptions options,
            string exportDirectory)
        {
            var passedRunCount = 0;
            var totalBattleTicks = 0;
            var totalTicks = 0;
            var totalMeasuredTicks = 0;
            var totalReplayEvents = 0;
            var totalStructuredLogEntries = 0;
            var totalElapsedMilliseconds = 0d;
            var maxRunAverageTickMilliseconds = 0d;
            var runtimeTiming = new HeadlessAutoChessRuntimeTickTiming();
            var failures = new List<string>();

            if (runs.Length == 0)
                failures.Add("runCount expected > 0 but was 0");

            for (var i = 0; i < runs.Length; i++)
            {
                var run = runs[i];
                var result = run.Result;
                if (result.ValidationReport.Passed)
                    passedRunCount++;
                else
                    failures.Add("run " + i + " validation failed: " + result.ValidationReport.SummaryText);

                var variantDefinition = HeadlessAutoChessScenario.GetVariantDefinition(run.Variant);
                if (result.Winner != variantDefinition.ExpectedWinner)
                {
                    failures.Add(
                        "run "
                        + i
                        + " winner expected "
                        + variantDefinition.ExpectedWinner
                        + " but was "
                        + result.Winner);
                }

                if (!result.Completed)
                    failures.Add("run " + i + " battle did not complete");
                if (result.EventCounts.StructuredLogEntries != result.EventCounts.ReplayEvents)
                    failures.Add("run " + i + " structured log entries did not match replay events");
                if (result.AverageTickMilliseconds > options.MaxRunAverageTickMilliseconds)
                {
                    failures.Add(
                        "run "
                        + i
                        + " averageTickMilliseconds expected <= "
                        + FormatDouble(options.MaxRunAverageTickMilliseconds)
                        + " but was "
                        + FormatDouble(result.AverageTickMilliseconds));
                }

                totalBattleTicks += result.BattleTicks;
                totalTicks += result.TotalTicks;
                totalMeasuredTicks += result.MeasuredTicks;
                totalReplayEvents += result.EventCounts.ReplayEvents;
                totalStructuredLogEntries += result.EventCounts.StructuredLogEntries;
                totalElapsedMilliseconds += result.ElapsedMilliseconds;
                runtimeTiming.Accumulate(result.RuntimeTiming);
                if (result.AverageTickMilliseconds > maxRunAverageTickMilliseconds)
                    maxRunAverageTickMilliseconds = result.AverageTickMilliseconds;
            }

            var variantResults = BuildVariantResults(runs);
            var deterministic = true;
            for (var i = 0; i < variantResults.Length; i++)
            {
                if (!variantResults[i].Deterministic)
                {
                    deterministic = false;
                    break;
                }
            }

            var referenceSignature = variantResults.Length > 0
                ? variantResults[0].ReferenceDeterminismSignature
                : string.Empty;
            var averageTickMilliseconds = totalMeasuredTicks > 0
                ? totalElapsedMilliseconds / totalMeasuredTicks
                : totalBattleTicks > 0
                    ? totalElapsedMilliseconds / totalBattleTicks
                    : totalTicks > 0
                        ? totalElapsedMilliseconds / totalTicks
                        : 0d;
            if (options.RequireDeterministicOutcome && !deterministic)
                failures.Add("variant determinism signatures diverged");
            if (averageTickMilliseconds > options.MaxAverageTickMilliseconds)
            {
                failures.Add(
                    "batch averageTickMilliseconds expected <= "
                    + FormatDouble(options.MaxAverageTickMilliseconds)
                    + " but was "
                    + FormatDouble(averageTickMilliseconds));
            }

            var failedRunCount = runs.Length - passedRunCount;
            var report = BuildReport(
                failures,
                variantResults,
                runs,
                options,
                exportDirectory,
                passedRunCount,
                failedRunCount,
                deterministic,
                referenceSignature,
                totalBattleTicks,
                totalTicks,
                totalMeasuredTicks,
                totalReplayEvents,
                totalStructuredLogEntries,
                totalElapsedMilliseconds,
                averageTickMilliseconds,
                maxRunAverageTickMilliseconds,
                runtimeTiming);

            return new HeadlessAutoChessScaleValidationResult(
                runs.Length,
                passedRunCount,
                failedRunCount,
                deterministic,
                referenceSignature,
                totalBattleTicks,
                totalTicks,
                totalMeasuredTicks,
                totalReplayEvents,
                totalStructuredLogEntries,
                totalElapsedMilliseconds,
                maxRunAverageTickMilliseconds,
                runtimeTiming,
                variantResults,
                runs,
                report);
        }

        private static HeadlessAutoChessScaleVariantResult[] BuildVariantResults(
            HeadlessAutoChessScaleRunResult[] runs)
        {
            var results = new List<HeadlessAutoChessScaleVariantResult>();
            for (var i = 0; i < runs.Length; i++)
            {
                var variant = runs[i].Variant;
                if (ContainsVariantResult(results, variant))
                    continue;

                results.Add(CreateVariantResult(runs, variant));
            }

            return results.ToArray();
        }

        private static bool ContainsVariantResult(
            List<HeadlessAutoChessScaleVariantResult> results,
            HeadlessAutoChessScenarioVariant variant)
        {
            for (var i = 0; i < results.Count; i++)
            {
                if (results[i].Variant == variant)
                    return true;
            }

            return false;
        }

        private static HeadlessAutoChessScaleVariantResult CreateVariantResult(
            HeadlessAutoChessScaleRunResult[] runs,
            HeadlessAutoChessScenarioVariant variant)
        {
            var definition = HeadlessAutoChessScenario.GetVariantDefinition(variant);
            var runCount = 0;
            var passedRunCount = 0;
            var totalReplayEvents = 0;
            var totalStructuredLogEntries = 0;
            var deterministic = true;
            var referenceSignature = string.Empty;

            for (var i = 0; i < runs.Length; i++)
            {
                var run = runs[i];
                if (run.Variant != definition.Variant)
                    continue;

                if (runCount == 0)
                    referenceSignature = run.DeterminismSignature;
                else if (!string.Equals(referenceSignature, run.DeterminismSignature, StringComparison.Ordinal))
                    deterministic = false;

                runCount++;
                if (run.Result.ValidationReport.Passed)
                    passedRunCount++;

                totalReplayEvents += run.Result.EventCounts.ReplayEvents;
                totalStructuredLogEntries += run.Result.EventCounts.StructuredLogEntries;
            }

            return new HeadlessAutoChessScaleVariantResult(
                definition.Variant,
                definition.Name,
                definition.DeterministicSeed,
                definition.PlayerUnitCount,
                definition.EnemyUnitCount,
                definition.ExpectedWinner,
                runCount,
                passedRunCount,
                runCount - passedRunCount,
                deterministic,
                referenceSignature,
                totalReplayEvents,
                totalStructuredLogEntries);
        }

        private static HeadlessAutoChessScaleValidationReport BuildReport(
            List<string> failures,
            HeadlessAutoChessScaleVariantResult[] variants,
            HeadlessAutoChessScaleRunResult[] runs,
            in HeadlessAutoChessScaleValidationOptions options,
            string exportDirectory,
            int passedRunCount,
            int failedRunCount,
            bool deterministic,
            string referenceSignature,
            int totalBattleTicks,
            int totalTicks,
            int totalMeasuredTicks,
            int totalReplayEvents,
            int totalStructuredLogEntries,
            double totalElapsedMilliseconds,
            double averageTickMilliseconds,
            double maxRunAverageTickMilliseconds,
            in HeadlessAutoChessRuntimeTickTiming runtimeTiming)
        {
            var summaryPath = options.ExportLogs
                ? Path.Combine(exportDirectory, "headless-autochess.scale.validation.txt")
                : string.Empty;
            var summaryText = BuildSummary(
                failures,
                variants,
                runs,
                options,
                passedRunCount,
                failedRunCount,
                deterministic,
                referenceSignature,
                totalBattleTicks,
                totalTicks,
                totalMeasuredTicks,
                totalReplayEvents,
                totalStructuredLogEntries,
                totalElapsedMilliseconds,
                averageTickMilliseconds,
                maxRunAverageTickMilliseconds,
                runtimeTiming,
                summaryPath);
            var summaryByteCount = 0L;

            if (options.ExportLogs)
            {
                Directory.CreateDirectory(exportDirectory);
                File.WriteAllText(summaryPath, summaryText, Encoding.UTF8);
                summaryByteCount = Encoding.UTF8.GetByteCount(summaryText);
            }

            return new HeadlessAutoChessScaleValidationReport(
                failures.Count == 0,
                failures.Count,
                summaryText,
                failures.ToArray(),
                summaryPath,
                summaryByteCount);
        }

        private static string BuildSummary(
            List<string> failures,
            HeadlessAutoChessScaleVariantResult[] variants,
            HeadlessAutoChessScaleRunResult[] runs,
            in HeadlessAutoChessScaleValidationOptions options,
            int passedRunCount,
            int failedRunCount,
            bool deterministic,
            string referenceSignature,
            int totalBattleTicks,
            int totalTicks,
            int totalMeasuredTicks,
            int totalReplayEvents,
            int totalStructuredLogEntries,
            double totalElapsedMilliseconds,
            double averageTickMilliseconds,
            double maxRunAverageTickMilliseconds,
            in HeadlessAutoChessRuntimeTickTiming runtimeTiming,
            string summaryPath)
        {
            var builder = new StringBuilder(4096);
            builder.AppendLine("HeadlessAutoChessScaleValidationReport");
            builder.Append("passed=").AppendLine(failures.Count == 0 ? "true" : "false");
            builder.Append("failureCount=").AppendLine(failures.Count.ToString(CultureInfo.InvariantCulture));
            builder.Append("runs|count=")
                .Append(runs.Length)
                .Append("|passed=")
                .Append(passedRunCount)
                .Append("|failed=")
                .Append(failedRunCount)
                .Append("|deterministic=")
                .Append(deterministic ? "true" : "false")
                .AppendLine();
            builder.Append("ticks|battleTotal=")
                .Append(totalBattleTicks)
                .Append("|total=")
                .Append(totalTicks)
                .Append("|measuredTotal=")
                .Append(totalMeasuredTicks)
                .Append("|elapsedMs=")
                .Append(FormatDouble(totalElapsedMilliseconds))
                .Append("|avgTickMs=")
                .Append(FormatDouble(averageTickMilliseconds))
                .Append("|maxRunAvgTickMs=")
                .Append(FormatDouble(maxRunAverageTickMilliseconds))
                .AppendLine();
            builder.Append("events|replayTotal=")
                .Append(totalReplayEvents)
                .Append("|structuredTotal=")
                .Append(totalStructuredLogEntries)
                .AppendLine();
            builder.Append("variants|count=")
                .Append(variants.Length)
                .AppendLine();
            builder.Append("signature|reference=")
                .Append(referenceSignature ?? string.Empty)
                .AppendLine();
            builder.Append("thresholds|maxBatchAvgTickMs=")
                .Append(FormatDouble(options.MaxAverageTickMilliseconds))
                .Append("|maxRunAvgTickMs=")
                .Append(FormatDouble(options.MaxRunAverageTickMilliseconds))
                .AppendLine();
            builder.Append("measurement|scope=ecsRuntimeTickOnly|warmupTickExcluded=")
                .Append(HeadlessAutoChessScenario.PerformanceWarmupBattleTicks)
                .AppendLine("|excluded=bootstrap,presentationOutbox,validationExport");
            builder.Append("groupTiming|ticks=")
                .Append(runtimeTiming.TickCount)
                .Append("|totalAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageTotalMilliseconds))
                .Append("|commandAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageCommandMilliseconds))
                .Append("|resetDirtyAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageResetDirtyMilliseconds))
                .Append("|tagAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageTagMilliseconds))
                .Append("|effectAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageEffectMilliseconds))
                .Append("|attributeAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageAttributeMilliseconds))
                .Append("|abilityAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageAbilityMilliseconds))
                .Append("|cueAvgMs=")
                .Append(FormatDouble(runtimeTiming.AverageCueMilliseconds))
                .AppendLine();
            builder.Append("exports|summary=")
                .Append(summaryPath ?? string.Empty)
                .AppendLine();

            for (var i = 0; i < variants.Length; i++)
                AppendVariantSummary(builder, variants[i]);

            for (var i = 0; i < runs.Length; i++)
                AppendRunSummary(builder, runs[i]);

            for (var i = 0; i < failures.Count; i++)
            {
                builder.Append("failure|index=")
                    .Append(i)
                    .Append("|message=")
                    .AppendLine(failures[i]);
            }

            return builder.ToString();
        }

        private static void AppendVariantSummary(
            StringBuilder builder,
            in HeadlessAutoChessScaleVariantResult variant)
        {
            builder.Append("variant|name=")
                .Append(variant.VariantName)
                .Append("|runs=")
                .Append(variant.RunCount)
                .Append("|passed=")
                .Append(variant.PassedRunCount)
                .Append("|failed=")
                .Append(variant.FailedRunCount)
                .Append("|deterministic=")
                .Append(variant.Deterministic ? "true" : "false")
                .Append("|seed=")
                .Append(variant.DeterministicSeed)
                .Append("|players=")
                .Append(variant.PlayerUnitCount)
                .Append("|enemies=")
                .Append(variant.EnemyUnitCount)
                .Append("|expectedWinner=")
                .Append(variant.ExpectedWinner)
                .Append("|replay=")
                .Append(variant.TotalReplayEvents)
                .Append("|structured=")
                .Append(variant.TotalStructuredLogEntries)
                .Append("|signature=")
                .Append(variant.ReferenceDeterminismSignature)
                .AppendLine();
        }

        private static void AppendRunSummary(StringBuilder builder, in HeadlessAutoChessScaleRunResult run)
        {
            var result = run.Result;
            builder.Append("run|index=")
                .Append(run.RunIndex)
                .Append("|passed=")
                .Append(result.ValidationReport.Passed ? "true" : "false")
                .Append("|winner=")
                .Append(result.Winner)
                .Append("|variant=")
                .Append(run.VariantName)
                .Append("|variantRun=")
                .Append(run.VariantRunIndex)
                .Append("|seed=")
                .Append(run.DeterministicSeed)
                .Append("|units=")
                .Append(result.Units.Length)
                .Append("|players=")
                .Append(result.PlayerUnitCount)
                .Append("|enemies=")
                .Append(result.EnemyUnitCount)
                .Append("|battle=")
                .Append(result.BattleTicks)
                .Append("|total=")
                .Append(result.TotalTicks)
                .Append("|measured=")
                .Append(result.MeasuredTicks)
                .Append("|avgTickMs=")
                .Append(FormatDouble(result.AverageTickMilliseconds))
                .Append("|replay=")
                .Append(result.EventCounts.ReplayEvents)
                .Append("|structured=")
                .Append(result.EventCounts.StructuredLogEntries)
                .Append("|uiMarkers=")
                .Append(result.EventCounts.PresentationUiMarkers)
                .Append("|vfxMarkers=")
                .Append(result.EventCounts.PresentationVfxMarkers)
                .Append("|sfxMarkers=")
                .Append(result.EventCounts.PresentationSfxMarkers)
                .Append("|floatingTextMarkers=")
                .Append(result.EventCounts.PresentationFloatingTextMarkers)
                .Append("|cueMarkers=")
                .Append(result.EventCounts.PresentationCueMarkers)
                .Append("|settlementMarkers=")
                .Append(result.EventCounts.PresentationSettlementMarkers)
                .Append("|signature=")
                .Append(run.DeterminismSignature)
                .Append("|export=")
                .Append(run.ExportDirectory)
                .AppendLine();
        }

        private static string ResolveScaleExportDirectory(string exportDirectory)
        {
            return string.IsNullOrWhiteSpace(exportDirectory)
                ? Path.Combine("TestResults", "AutoChess", "T6-CHESS-P")
                : exportDirectory;
        }

        private static string CreateExportToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Variant";

            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                builder.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '-');
            }

            return builder.Length == 0 ? "Variant" : builder.ToString();
        }

        private static string CreateDeterminismSignature(in HeadlessAutoChessResult result)
        {
            var hash = FnvOffsetBasis;
            AddInt(ref hash, (int)result.Variant);
            AddString(ref hash, result.VariantName);
            AddInt(ref hash, result.DeterministicSeed);
            AddInt(ref hash, result.PlayerUnitCount);
            AddInt(ref hash, result.EnemyUnitCount);
            AddBool(ref hash, result.Completed);
            AddInt(ref hash, (int)result.Winner);
            AddInt(ref hash, result.BoardWidth);
            AddInt(ref hash, result.BoardHeight);
            AddInt(ref hash, result.BattleTicks);
            AddInt(ref hash, result.TotalTicks);
            AddInt(ref hash, result.Round);
            AddInt(ref hash, result.TurnCount);
            AddInt(ref hash, result.DriverIssuedCommands);
            AddInt(ref hash, result.DriverIssuedPrimaryCommands);
            AddInt(ref hash, result.DriverIssuedManaAbilityCommands);
            AddInt(ref hash, result.DriverIssuedControlAbilityCommands);
            AddInt(ref hash, result.DriverIssuedSupportAbilityCommands);
            AddInt(ref hash, result.DriverIssuedSummonAbilityCommands);
            AddInt(ref hash, result.DriverFrontlineTargetSelections);
            AddInt(ref hash, result.DriverLowestHealthTargetSelections);
            AddInt(ref hash, result.PlayerDefeatedCount);
            AddInt(ref hash, result.EnemyDefeatedCount);
            AddInt(ref hash, result.FirstDefeatFrame);
            AddInt(ref hash, result.LastDefeatFrame);
            AddInt(ref hash, result.BattleResolvedFrame);
            AddInt(ref hash, result.PassiveTriggeredCount);
            AddInt(ref hash, result.KillManaGrantedCount);
            AddInt(ref hash, result.ReviveRequestedCount);
            AddInt(ref hash, result.ReviveAppliedCount);
            AddInt(ref hash, result.SynergyActivatedCount);
            AddInt(ref hash, result.SynergyExpiredCount);
            AddInt(ref hash, result.SynergyAllyBuffRequestedCount);
            AddInt(ref hash, result.SynergyEnemyDebuffRequestedCount);
            AddInt(ref hash, result.SynergyPeriodicTickCount);
            AddInt(ref hash, result.ShieldAppliedCount);
            AddInt(ref hash, result.ShieldAbsorbedCount);
            AddInt(ref hash, result.ShieldBrokenCount);
            AddInt(ref hash, result.CleanseRequestedCount);
            AddInt(ref hash, result.CleanseAppliedCount);
            AddInt(ref hash, result.CleanseEffectRemovedCount);
            AddInt(ref hash, result.CleanseRallyRequestedCount);
            AddInt(ref hash, result.CleanseRallyAppliedCount);
            AddInt(ref hash, result.RallyComboTriggeredCount);
            AddInt(ref hash, result.RallyComboDamageAppliedCount);
            AddInt(ref hash, result.LifeStealTriggeredCount);
            AddInt(ref hash, result.LifeStealHealedCount);
            AddInt(ref hash, result.PoisonStackRequestedCount);
            AddInt(ref hash, result.PoisonStackChangedCount);
            AddInt(ref hash, result.PoisonOverflowTriggeredCount);
            AddInt(ref hash, result.PoisonOverflowDamageAppliedCount);
            AddInt(ref hash, result.PoisonPeriodDamageAppliedCount);
            AddInt(ref hash, result.ExecuteTriggeredCount);
            AddInt(ref hash, result.ExecuteDamageAppliedCount);
            AddInt(ref hash, result.DeathBurstTriggeredCount);
            AddInt(ref hash, result.DeathBurstDamageAppliedCount);
            AddInt(ref hash, result.EnrageTriggeredCount);
            AddInt(ref hash, result.EnrageAppliedCount);
            AddInt(ref hash, result.SummonRequestedCount);
            AddInt(ref hash, result.SummonSpawnedCount);
            AddInt(ref hash, result.SummonExpiredCount);
            AddInt(ref hash, result.SummonDespawnedCount);
            AddEventCounts(ref hash, result.EventCounts);
            AddInt(ref hash, result.Units.Length);
            for (var i = 0; i < result.Units.Length; i++)
                AddUnit(ref hash, result.Units[i]);

            return "0x" + hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static void AddEventCounts(ref ulong hash, in HeadlessAutoChessEventCounts counts)
        {
            AddInt(ref hash, counts.ReplayEvents);
            AddInt(ref hash, counts.StructuredLogEntries);
            AddInt(ref hash, counts.AbilityCommitSucceeded);
            AddInt(ref hash, counts.AbilityCommitFailed);
            AddInt(ref hash, counts.GameplayEffectInstanced);
            AddInt(ref hash, counts.GameplayEffectApplied);
            AddInt(ref hash, counts.GameplayEffectRemoved);
            AddInt(ref hash, counts.AttributeChanges);
            AddInt(ref hash, counts.HealthDamageAttributeChanges);
            AddInt(ref hash, counts.TagChanges);
            AddInt(ref hash, counts.CueRequests);
            AddInt(ref hash, counts.DamageEvents);
            AddInt(ref hash, counts.UnitDefeated);
            AddInt(ref hash, counts.BattleResolved);
            AddInt(ref hash, counts.PassiveTriggered);
            AddInt(ref hash, counts.KillManaGranted);
            AddInt(ref hash, counts.ReviveRequested);
            AddInt(ref hash, counts.ReviveApplied);
            AddInt(ref hash, counts.SynergyActivated);
            AddInt(ref hash, counts.SynergyExpired);
            AddInt(ref hash, counts.SynergyAllyBuffRequested);
            AddInt(ref hash, counts.SynergyEnemyDebuffRequested);
            AddInt(ref hash, counts.SynergyPeriodicTicked);
            AddInt(ref hash, counts.ControlTurnSkipped);
            AddInt(ref hash, counts.ShieldApplied);
            AddInt(ref hash, counts.ShieldAbsorbed);
            AddInt(ref hash, counts.ShieldBroken);
            AddInt(ref hash, counts.DamageTypeResolved);
            AddInt(ref hash, counts.DamageResisted);
            AddInt(ref hash, counts.EquipmentApplied);
            AddInt(ref hash, counts.CounterTriggered);
            AddInt(ref hash, counts.CounterDamageApplied);
            AddInt(ref hash, counts.CleanseRequested);
            AddInt(ref hash, counts.CleanseApplied);
            AddInt(ref hash, counts.CleanseEffectRemoved);
            AddInt(ref hash, counts.CleanseRallyRequested);
            AddInt(ref hash, counts.CleanseRallyApplied);
            AddInt(ref hash, counts.RallyComboTriggered);
            AddInt(ref hash, counts.RallyComboDamageApplied);
            AddInt(ref hash, counts.LifeStealTriggered);
            AddInt(ref hash, counts.LifeStealHealed);
            AddInt(ref hash, counts.PoisonStackRequested);
            AddInt(ref hash, counts.PoisonStackChanged);
            AddInt(ref hash, counts.PoisonOverflowTriggered);
            AddInt(ref hash, counts.PoisonOverflowDamageApplied);
            AddInt(ref hash, counts.PoisonPeriodDamageApplied);
            AddInt(ref hash, counts.ExecuteTriggered);
            AddInt(ref hash, counts.ExecuteDamageApplied);
            AddInt(ref hash, counts.DeathBurstTriggered);
            AddInt(ref hash, counts.DeathBurstDamageApplied);
            AddInt(ref hash, counts.EnrageTriggered);
            AddInt(ref hash, counts.EnrageApplied);
            AddInt(ref hash, counts.SummonRequested);
            AddInt(ref hash, counts.SummonSpawned);
            AddInt(ref hash, counts.SummonExpired);
            AddInt(ref hash, counts.SummonDespawned);
            AddInt(ref hash, counts.PresentationUiMarkers);
            AddInt(ref hash, counts.PresentationVfxMarkers);
            AddInt(ref hash, counts.PresentationSfxMarkers);
            AddInt(ref hash, counts.PresentationFloatingTextMarkers);
            AddInt(ref hash, counts.PresentationCueMarkers);
            AddInt(ref hash, counts.PresentationSettlementMarkers);
        }

        private static void AddUnit(ref ulong hash, in HeadlessAutoChessUnitResult unit)
        {
            AddString(ref hash, unit.Id);
            AddInt(ref hash, (int)unit.Team);
            AddInt(ref hash, unit.Slot);
            AddInt(ref hash, unit.BoardX);
            AddInt(ref hash, unit.BoardY);
            AddFloat(ref hash, unit.Health);
            AddFloat(ref hash, unit.Mana);
            AddFloat(ref hash, unit.Shield);
            AddFloat(ref hash, unit.ArcaneResistance);
            AddBool(ref hash, unit.Alive);
        }

        private static void AddString(ref ulong hash, string value)
        {
            value ??= string.Empty;
            AddInt(ref hash, value.Length);
            for (var i = 0; i < value.Length; i++)
                AddInt(ref hash, value[i]);
        }

        private static void AddBool(ref ulong hash, bool value)
        {
            AddInt(ref hash, value ? 1 : 0);
        }

        private static void AddFloat(ref ulong hash, float value)
        {
            AddInt(ref hash, (int)Math.Round(value * 1000f, MidpointRounding.AwayFromZero));
        }

        private static void AddInt(ref ulong hash, int value)
        {
            unchecked
            {
                var unsigned = (uint)value;
                for (var i = 0; i < 4; i++)
                {
                    hash ^= (byte)(unsigned >> (i * 8));
                    hash *= FnvPrime;
                }
            }
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("G9", CultureInfo.InvariantCulture);
        }

        private const ulong FnvOffsetBasis = 1469598103934665603UL;
        private const ulong FnvPrime = 1099511628211UL;
    }
}
