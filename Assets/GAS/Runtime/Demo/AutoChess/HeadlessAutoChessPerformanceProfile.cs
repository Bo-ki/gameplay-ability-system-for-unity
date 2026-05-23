using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GAS.Runtime
{
    public readonly struct HeadlessAutoChessPerformanceProfileOptions
    {
        public readonly int RunCountPerVariant;
        public readonly bool ExportLogs;
        public readonly bool ExportScaleRunLogs;
        public readonly string ExportDirectory;
        public readonly double MaxAverageTickMilliseconds;
        public readonly double MaxP95RunAverageTickMilliseconds;
        public readonly double MaxRunAverageTickMilliseconds;
        public readonly double MinTicksPerSecond;
        public readonly HeadlessAutoChessOptions ScenarioOptions;
        public readonly HeadlessAutoChessScenarioVariant[] Variants;

        public HeadlessAutoChessPerformanceProfileOptions(
            int runCountPerVariant,
            bool exportLogs = false,
            bool exportScaleRunLogs = false,
            string exportDirectory = null,
            double maxAverageTickMilliseconds = 0d,
            double maxP95RunAverageTickMilliseconds = 0d,
            double maxRunAverageTickMilliseconds = 0d,
            double minTicksPerSecond = 0d,
            HeadlessAutoChessOptions scenarioOptions = default,
            HeadlessAutoChessScenarioVariant[] variants = null)
        {
            RunCountPerVariant = runCountPerVariant;
            ExportLogs = exportLogs;
            ExportScaleRunLogs = exportScaleRunLogs;
            ExportDirectory = exportDirectory;
            MaxAverageTickMilliseconds = maxAverageTickMilliseconds;
            MaxP95RunAverageTickMilliseconds = maxP95RunAverageTickMilliseconds;
            MaxRunAverageTickMilliseconds = maxRunAverageTickMilliseconds;
            MinTicksPerSecond = minTicksPerSecond;
            ScenarioOptions = scenarioOptions;
            Variants = CopyVariants(variants);
        }

        public HeadlessAutoChessPerformanceProfileOptions Normalize()
        {
            var scenario = ScenarioOptions.Normalize();
            var thresholds = scenario.ValidationThresholds.Normalize();
            var maxAverageTickMilliseconds = PickPositive(
                MaxAverageTickMilliseconds,
                thresholds.MaxAverageTickMilliseconds);
            return new HeadlessAutoChessPerformanceProfileOptions(
                RunCountPerVariant > 0 ? RunCountPerVariant : 2,
                ExportLogs,
                ExportScaleRunLogs,
                ExportDirectory,
                maxAverageTickMilliseconds,
                PickPositive(MaxP95RunAverageTickMilliseconds, maxAverageTickMilliseconds),
                PickPositive(MaxRunAverageTickMilliseconds, maxAverageTickMilliseconds),
                PickPositive(MinTicksPerSecond, 1000d / maxAverageTickMilliseconds),
                scenario,
                NormalizeVariants(Variants));
        }

        private static HeadlessAutoChessScenarioVariant[] NormalizeVariants(
            HeadlessAutoChessScenarioVariant[] variants)
        {
            if (variants == null || variants.Length == 0)
            {
                return new[]
                {
                    HeadlessAutoChessScenarioVariant.DefaultBalanced,
                    HeadlessAutoChessScenarioVariant.PlayerAdvantage,
                    HeadlessAutoChessScenarioVariant.EnemyPressure,
                    HeadlessAutoChessScenarioVariant.LargeBoard,
                };
            }

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

        private static double PickPositive(double value, double fallback)
        {
            return value > 0d ? value : fallback;
        }
    }

    public readonly struct HeadlessAutoChessPerformanceRunProfile
    {
        public readonly int RunIndex;
        public readonly int VariantRunIndex;
        public readonly HeadlessAutoChessScenarioVariant Variant;
        public readonly string VariantName;
        public readonly bool Passed;
        public readonly bool Completed;
        public readonly HeadlessAutoChessTeam Winner;
        public readonly int UnitCount;
        public readonly int TotalTicks;
        public readonly int ReplayEvents;
        public readonly int StructuredLogEntries;
        public readonly int PresentationMarkers;
        public readonly double ElapsedMilliseconds;
        public readonly double AverageTickMilliseconds;
        public readonly double TicksPerSecond;
        public readonly double ReplayEventsPerTick;
        public readonly double PresentationMarkersPerTick;
        public readonly string DeterminismSignature;

        public HeadlessAutoChessPerformanceRunProfile(
            int runIndex,
            int variantRunIndex,
            HeadlessAutoChessScenarioVariant variant,
            string variantName,
            bool passed,
            bool completed,
            HeadlessAutoChessTeam winner,
            int unitCount,
            int totalTicks,
            int replayEvents,
            int structuredLogEntries,
            int presentationMarkers,
            double elapsedMilliseconds,
            double averageTickMilliseconds,
            string determinismSignature)
        {
            RunIndex = runIndex;
            VariantRunIndex = variantRunIndex;
            Variant = variant;
            VariantName = variantName ?? string.Empty;
            Passed = passed;
            Completed = completed;
            Winner = winner;
            UnitCount = unitCount;
            TotalTicks = totalTicks;
            ReplayEvents = replayEvents;
            StructuredLogEntries = structuredLogEntries;
            PresentationMarkers = presentationMarkers;
            ElapsedMilliseconds = elapsedMilliseconds;
            AverageTickMilliseconds = averageTickMilliseconds;
            TicksPerSecond = elapsedMilliseconds > 0d ? totalTicks * 1000d / elapsedMilliseconds : 0d;
            ReplayEventsPerTick = totalTicks > 0 ? (double)replayEvents / totalTicks : 0d;
            PresentationMarkersPerTick = totalTicks > 0 ? (double)presentationMarkers / totalTicks : 0d;
            DeterminismSignature = determinismSignature ?? string.Empty;
        }
    }

    public readonly struct HeadlessAutoChessPerformanceVariantProfile
    {
        public readonly HeadlessAutoChessScenarioVariant Variant;
        public readonly string VariantName;
        public readonly int RunCount;
        public readonly int PassedRunCount;
        public readonly bool Deterministic;
        public readonly int TotalTicks;
        public readonly int TotalReplayEvents;
        public readonly int TotalStructuredLogEntries;
        public readonly int TotalPresentationMarkers;
        public readonly double TotalElapsedMilliseconds;
        public readonly double AverageTickMilliseconds;
        public readonly double P50RunAverageTickMilliseconds;
        public readonly double P95RunAverageTickMilliseconds;
        public readonly double MaxRunAverageTickMilliseconds;
        public readonly double TicksPerSecond;
        public readonly double ReplayEventsPerTick;
        public readonly double PresentationMarkersPerTick;
        public readonly string ReferenceDeterminismSignature;

        public HeadlessAutoChessPerformanceVariantProfile(
            HeadlessAutoChessScenarioVariant variant,
            string variantName,
            int runCount,
            int passedRunCount,
            bool deterministic,
            int totalTicks,
            int totalReplayEvents,
            int totalStructuredLogEntries,
            int totalPresentationMarkers,
            double totalElapsedMilliseconds,
            double p50RunAverageTickMilliseconds,
            double p95RunAverageTickMilliseconds,
            double maxRunAverageTickMilliseconds,
            string referenceDeterminismSignature)
        {
            Variant = variant;
            VariantName = variantName ?? string.Empty;
            RunCount = runCount;
            PassedRunCount = passedRunCount;
            Deterministic = deterministic;
            TotalTicks = totalTicks;
            TotalReplayEvents = totalReplayEvents;
            TotalStructuredLogEntries = totalStructuredLogEntries;
            TotalPresentationMarkers = totalPresentationMarkers;
            TotalElapsedMilliseconds = totalElapsedMilliseconds;
            AverageTickMilliseconds = totalTicks > 0 ? totalElapsedMilliseconds / totalTicks : 0d;
            P50RunAverageTickMilliseconds = p50RunAverageTickMilliseconds;
            P95RunAverageTickMilliseconds = p95RunAverageTickMilliseconds;
            MaxRunAverageTickMilliseconds = maxRunAverageTickMilliseconds;
            TicksPerSecond = totalElapsedMilliseconds > 0d ? totalTicks * 1000d / totalElapsedMilliseconds : 0d;
            ReplayEventsPerTick = totalTicks > 0 ? (double)totalReplayEvents / totalTicks : 0d;
            PresentationMarkersPerTick = totalTicks > 0 ? (double)totalPresentationMarkers / totalTicks : 0d;
            ReferenceDeterminismSignature = referenceDeterminismSignature ?? string.Empty;
        }
    }

    public readonly struct HeadlessAutoChessPerformanceProfileReport
    {
        public readonly bool Passed;
        public readonly int FailureCount;
        public readonly string SummaryText;
        public readonly string[] FailureMessages;
        public readonly string SummaryPath;
        public readonly long SummaryByteCount;

        public HeadlessAutoChessPerformanceProfileReport(
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

    public readonly struct HeadlessAutoChessPerformanceProfileResult
    {
        public readonly HeadlessAutoChessScaleValidationResult ScaleValidation;
        public readonly HeadlessAutoChessPerformanceVariantProfile[] Variants;
        public readonly HeadlessAutoChessPerformanceRunProfile[] Runs;
        public readonly int RunCount;
        public readonly int VariantCount;
        public readonly bool Deterministic;
        public readonly int TotalTicks;
        public readonly int TotalReplayEvents;
        public readonly int TotalStructuredLogEntries;
        public readonly int TotalPresentationMarkers;
        public readonly double TotalElapsedMilliseconds;
        public readonly double AverageTickMilliseconds;
        public readonly double P50RunAverageTickMilliseconds;
        public readonly double P95RunAverageTickMilliseconds;
        public readonly double MaxRunAverageTickMilliseconds;
        public readonly double TicksPerSecond;
        public readonly double ReplayEventsPerTick;
        public readonly double PresentationMarkersPerTick;
        public readonly HeadlessAutoChessPerformanceProfileReport Report;

        public HeadlessAutoChessPerformanceProfileResult(
            HeadlessAutoChessScaleValidationResult scaleValidation,
            HeadlessAutoChessPerformanceVariantProfile[] variants,
            HeadlessAutoChessPerformanceRunProfile[] runs,
            int totalTicks,
            int totalReplayEvents,
            int totalStructuredLogEntries,
            int totalPresentationMarkers,
            double totalElapsedMilliseconds,
            double p50RunAverageTickMilliseconds,
            double p95RunAverageTickMilliseconds,
            double maxRunAverageTickMilliseconds,
            HeadlessAutoChessPerformanceProfileReport report)
        {
            ScaleValidation = scaleValidation;
            Variants = variants ?? Array.Empty<HeadlessAutoChessPerformanceVariantProfile>();
            Runs = runs ?? Array.Empty<HeadlessAutoChessPerformanceRunProfile>();
            RunCount = Runs.Length;
            VariantCount = Variants.Length;
            Deterministic = scaleValidation.Deterministic;
            TotalTicks = totalTicks;
            TotalReplayEvents = totalReplayEvents;
            TotalStructuredLogEntries = totalStructuredLogEntries;
            TotalPresentationMarkers = totalPresentationMarkers;
            TotalElapsedMilliseconds = totalElapsedMilliseconds;
            AverageTickMilliseconds = totalTicks > 0 ? totalElapsedMilliseconds / totalTicks : 0d;
            P50RunAverageTickMilliseconds = p50RunAverageTickMilliseconds;
            P95RunAverageTickMilliseconds = p95RunAverageTickMilliseconds;
            MaxRunAverageTickMilliseconds = maxRunAverageTickMilliseconds;
            TicksPerSecond = totalElapsedMilliseconds > 0d ? totalTicks * 1000d / totalElapsedMilliseconds : 0d;
            ReplayEventsPerTick = totalTicks > 0 ? (double)totalReplayEvents / totalTicks : 0d;
            PresentationMarkersPerTick = totalTicks > 0 ? (double)totalPresentationMarkers / totalTicks : 0d;
            Report = report;
        }
    }

    public static class HeadlessAutoChessPerformanceProfile
    {
        public static HeadlessAutoChessPerformanceProfileResult Run(
            HeadlessAutoChessPerformanceProfileOptions options = default)
        {
            var normalized = options.Normalize();
            var exportDirectory = ResolveExportDirectory(normalized.ExportDirectory);
            var scaleExportDirectory = normalized.ExportScaleRunLogs
                ? Path.Combine(exportDirectory, "scale-runs")
                : string.Empty;
            var scaleValidation = HeadlessAutoChessScaleValidation.Run(
                new HeadlessAutoChessScaleValidationOptions(
                    normalized.RunCountPerVariant,
                    normalized.ExportScaleRunLogs,
                    scaleExportDirectory,
                    true,
                    normalized.MaxAverageTickMilliseconds,
                    normalized.MaxRunAverageTickMilliseconds,
                    normalized.ScenarioOptions,
                    normalized.Variants));

            var runs = BuildRunProfiles(scaleValidation.Runs);
            var variants = BuildVariantProfiles(runs, scaleValidation.Variants);
            return BuildResult(scaleValidation, variants, runs, normalized, exportDirectory);
        }

        private static HeadlessAutoChessPerformanceRunProfile[] BuildRunProfiles(
            HeadlessAutoChessScaleRunResult[] runs)
        {
            var profiles = new HeadlessAutoChessPerformanceRunProfile[runs.Length];
            for (var i = 0; i < runs.Length; i++)
            {
                var run = runs[i];
                var result = run.Result;
                profiles[i] = new HeadlessAutoChessPerformanceRunProfile(
                    run.RunIndex,
                    run.VariantRunIndex,
                    run.Variant,
                    run.VariantName,
                    result.ValidationReport.Passed,
                    result.Completed,
                    result.Winner,
                    result.Units.Length,
                    result.TotalTicks,
                    result.EventCounts.ReplayEvents,
                    result.EventCounts.StructuredLogEntries,
                    CountPresentationMarkers(result.EventCounts),
                    result.ElapsedMilliseconds,
                    result.AverageTickMilliseconds,
                    run.DeterminismSignature);
            }

            return profiles;
        }

        private static HeadlessAutoChessPerformanceVariantProfile[] BuildVariantProfiles(
            HeadlessAutoChessPerformanceRunProfile[] runs,
            HeadlessAutoChessScaleVariantResult[] scaleVariants)
        {
            var profiles = new HeadlessAutoChessPerformanceVariantProfile[scaleVariants.Length];
            for (var i = 0; i < scaleVariants.Length; i++)
            {
                var variant = scaleVariants[i];
                profiles[i] = BuildVariantProfile(runs, variant);
            }

            return profiles;
        }

        private static HeadlessAutoChessPerformanceVariantProfile BuildVariantProfile(
            HeadlessAutoChessPerformanceRunProfile[] runs,
            in HeadlessAutoChessScaleVariantResult scaleVariant)
        {
            var averages = new List<double>();
            var runCount = 0;
            var passedRunCount = 0;
            var totalTicks = 0;
            var totalReplayEvents = 0;
            var totalStructuredLogEntries = 0;
            var totalPresentationMarkers = 0;
            var totalElapsedMilliseconds = 0d;
            var maxRunAverageTickMilliseconds = 0d;

            for (var i = 0; i < runs.Length; i++)
            {
                var run = runs[i];
                if (run.Variant != scaleVariant.Variant)
                    continue;

                runCount++;
                if (run.Passed)
                    passedRunCount++;

                totalTicks += run.TotalTicks;
                totalReplayEvents += run.ReplayEvents;
                totalStructuredLogEntries += run.StructuredLogEntries;
                totalPresentationMarkers += run.PresentationMarkers;
                totalElapsedMilliseconds += run.ElapsedMilliseconds;
                averages.Add(run.AverageTickMilliseconds);
                if (run.AverageTickMilliseconds > maxRunAverageTickMilliseconds)
                    maxRunAverageTickMilliseconds = run.AverageTickMilliseconds;
            }

            return new HeadlessAutoChessPerformanceVariantProfile(
                scaleVariant.Variant,
                scaleVariant.VariantName,
                runCount,
                passedRunCount,
                scaleVariant.Deterministic,
                totalTicks,
                totalReplayEvents,
                totalStructuredLogEntries,
                totalPresentationMarkers,
                totalElapsedMilliseconds,
                Percentile(averages, 50d),
                Percentile(averages, 95d),
                maxRunAverageTickMilliseconds,
                scaleVariant.ReferenceDeterminismSignature);
        }

        private static HeadlessAutoChessPerformanceProfileResult BuildResult(
            in HeadlessAutoChessScaleValidationResult scaleValidation,
            HeadlessAutoChessPerformanceVariantProfile[] variants,
            HeadlessAutoChessPerformanceRunProfile[] runs,
            in HeadlessAutoChessPerformanceProfileOptions options,
            string exportDirectory)
        {
            var totalTicks = 0;
            var totalReplayEvents = 0;
            var totalStructuredLogEntries = 0;
            var totalPresentationMarkers = 0;
            var totalElapsedMilliseconds = 0d;
            var maxRunAverageTickMilliseconds = 0d;
            var runAverages = new List<double>(runs.Length);
            var failures = new List<string>();

            if (!scaleValidation.Report.Passed)
                failures.Add("scale validation failed");
            if (!scaleValidation.Deterministic)
                failures.Add("scale validation was not deterministic");

            for (var i = 0; i < runs.Length; i++)
            {
                var run = runs[i];
                totalTicks += run.TotalTicks;
                totalReplayEvents += run.ReplayEvents;
                totalStructuredLogEntries += run.StructuredLogEntries;
                totalPresentationMarkers += run.PresentationMarkers;
                totalElapsedMilliseconds += run.ElapsedMilliseconds;
                runAverages.Add(run.AverageTickMilliseconds);
                if (run.AverageTickMilliseconds > maxRunAverageTickMilliseconds)
                    maxRunAverageTickMilliseconds = run.AverageTickMilliseconds;
                if (run.StructuredLogEntries != run.ReplayEvents)
                    failures.Add("run " + run.RunIndex + " structured log entries did not match replay events");
                if (run.PresentationMarkers <= 0)
                    failures.Add("run " + run.RunIndex + " presentation markers expected > 0");
            }

            var averageTickMilliseconds = totalTicks > 0 ? totalElapsedMilliseconds / totalTicks : 0d;
            var p50RunAverageTickMilliseconds = Percentile(runAverages, 50d);
            var p95RunAverageTickMilliseconds = Percentile(runAverages, 95d);
            var ticksPerSecond = totalElapsedMilliseconds > 0d ? totalTicks * 1000d / totalElapsedMilliseconds : 0d;

            if (averageTickMilliseconds > options.MaxAverageTickMilliseconds)
            {
                failures.Add(
                    "profile averageTickMilliseconds expected <= "
                    + FormatDouble(options.MaxAverageTickMilliseconds)
                    + " but was "
                    + FormatDouble(averageTickMilliseconds));
            }

            if (p95RunAverageTickMilliseconds > options.MaxP95RunAverageTickMilliseconds)
            {
                failures.Add(
                    "profile p95RunAverageTickMilliseconds expected <= "
                    + FormatDouble(options.MaxP95RunAverageTickMilliseconds)
                    + " but was "
                    + FormatDouble(p95RunAverageTickMilliseconds));
            }

            if (maxRunAverageTickMilliseconds > options.MaxRunAverageTickMilliseconds)
            {
                failures.Add(
                    "profile maxRunAverageTickMilliseconds expected <= "
                    + FormatDouble(options.MaxRunAverageTickMilliseconds)
                    + " but was "
                    + FormatDouble(maxRunAverageTickMilliseconds));
            }

            if (ticksPerSecond < options.MinTicksPerSecond)
            {
                failures.Add(
                    "profile ticksPerSecond expected >= "
                    + FormatDouble(options.MinTicksPerSecond)
                    + " but was "
                    + FormatDouble(ticksPerSecond));
            }

            for (var i = 0; i < variants.Length; i++)
            {
                var variant = variants[i];
                if (!variant.Deterministic)
                    failures.Add("variant " + variant.VariantName + " was not deterministic");
                if (variant.PassedRunCount != variant.RunCount)
                    failures.Add("variant " + variant.VariantName + " had failed runs");
                if (variant.P95RunAverageTickMilliseconds > options.MaxP95RunAverageTickMilliseconds)
                    failures.Add("variant " + variant.VariantName + " p95 run average exceeded threshold");
                if (variant.TotalPresentationMarkers <= 0)
                    failures.Add("variant " + variant.VariantName + " presentation markers expected > 0");
            }

            var report = BuildReport(
                failures,
                scaleValidation,
                variants,
                runs,
                options,
                exportDirectory,
                totalTicks,
                totalReplayEvents,
                totalStructuredLogEntries,
                totalPresentationMarkers,
                totalElapsedMilliseconds,
                averageTickMilliseconds,
                p50RunAverageTickMilliseconds,
                p95RunAverageTickMilliseconds,
                maxRunAverageTickMilliseconds,
                ticksPerSecond);

            return new HeadlessAutoChessPerformanceProfileResult(
                scaleValidation,
                variants,
                runs,
                totalTicks,
                totalReplayEvents,
                totalStructuredLogEntries,
                totalPresentationMarkers,
                totalElapsedMilliseconds,
                p50RunAverageTickMilliseconds,
                p95RunAverageTickMilliseconds,
                maxRunAverageTickMilliseconds,
                report);
        }

        private static HeadlessAutoChessPerformanceProfileReport BuildReport(
            List<string> failures,
            in HeadlessAutoChessScaleValidationResult scaleValidation,
            HeadlessAutoChessPerformanceVariantProfile[] variants,
            HeadlessAutoChessPerformanceRunProfile[] runs,
            in HeadlessAutoChessPerformanceProfileOptions options,
            string exportDirectory,
            int totalTicks,
            int totalReplayEvents,
            int totalStructuredLogEntries,
            int totalPresentationMarkers,
            double totalElapsedMilliseconds,
            double averageTickMilliseconds,
            double p50RunAverageTickMilliseconds,
            double p95RunAverageTickMilliseconds,
            double maxRunAverageTickMilliseconds,
            double ticksPerSecond)
        {
            var summaryPath = options.ExportLogs
                ? Path.Combine(exportDirectory, "headless-autochess.performance.profile.txt")
                : string.Empty;
            var summaryText = BuildSummary(
                failures,
                scaleValidation,
                variants,
                runs,
                options,
                summaryPath,
                totalTicks,
                totalReplayEvents,
                totalStructuredLogEntries,
                totalPresentationMarkers,
                totalElapsedMilliseconds,
                averageTickMilliseconds,
                p50RunAverageTickMilliseconds,
                p95RunAverageTickMilliseconds,
                maxRunAverageTickMilliseconds,
                ticksPerSecond);
            var summaryByteCount = 0L;

            if (options.ExportLogs)
            {
                Directory.CreateDirectory(exportDirectory);
                File.WriteAllText(summaryPath, summaryText, Encoding.UTF8);
                summaryByteCount = Encoding.UTF8.GetByteCount(summaryText);
            }

            return new HeadlessAutoChessPerformanceProfileReport(
                failures.Count == 0,
                failures.Count,
                summaryText,
                failures.ToArray(),
                summaryPath,
                summaryByteCount);
        }

        private static string BuildSummary(
            List<string> failures,
            in HeadlessAutoChessScaleValidationResult scaleValidation,
            HeadlessAutoChessPerformanceVariantProfile[] variants,
            HeadlessAutoChessPerformanceRunProfile[] runs,
            in HeadlessAutoChessPerformanceProfileOptions options,
            string summaryPath,
            int totalTicks,
            int totalReplayEvents,
            int totalStructuredLogEntries,
            int totalPresentationMarkers,
            double totalElapsedMilliseconds,
            double averageTickMilliseconds,
            double p50RunAverageTickMilliseconds,
            double p95RunAverageTickMilliseconds,
            double maxRunAverageTickMilliseconds,
            double ticksPerSecond)
        {
            var builder = new StringBuilder(4096);
            builder.AppendLine("HeadlessAutoChessPerformanceProfileReport");
            builder.Append("passed=").AppendLine(failures.Count == 0 ? "true" : "false");
            builder.Append("failureCount=").AppendLine(failures.Count.ToString(CultureInfo.InvariantCulture));
            builder.Append("profile|runs=")
                .Append(runs.Length)
                .Append("|variants=")
                .Append(variants.Length)
                .Append("|deterministic=")
                .Append(scaleValidation.Deterministic ? "true" : "false")
                .Append("|scalePassed=")
                .Append(scaleValidation.Report.Passed ? "true" : "false")
                .AppendLine();
            builder.Append("runtime|totalTicks=")
                .Append(totalTicks)
                .Append("|elapsedMs=")
                .Append(FormatDouble(totalElapsedMilliseconds))
                .Append("|avgTickMs=")
                .Append(FormatDouble(averageTickMilliseconds))
                .Append("|maxRunAvgTickMs=")
                .Append(FormatDouble(maxRunAverageTickMilliseconds))
                .AppendLine();
            builder.Append("percentiles|p50RunAvgTickMs=")
                .Append(FormatDouble(p50RunAverageTickMilliseconds))
                .Append("|p95RunAvgTickMs=")
                .Append(FormatDouble(p95RunAverageTickMilliseconds))
                .AppendLine();
            builder.Append("throughput|ticksPerSecond=")
                .Append(FormatDouble(ticksPerSecond))
                .Append("|replayEventsPerTick=")
                .Append(FormatDouble(totalTicks > 0 ? (double)totalReplayEvents / totalTicks : 0d))
                .Append("|presentationMarkersPerTick=")
                .Append(FormatDouble(totalTicks > 0 ? (double)totalPresentationMarkers / totalTicks : 0d))
                .AppendLine();
            builder.Append("events|replayTotal=")
                .Append(totalReplayEvents)
                .Append("|structuredTotal=")
                .Append(totalStructuredLogEntries)
                .Append("|eventsPerMs=")
                .Append(FormatDouble(totalElapsedMilliseconds > 0d ? totalReplayEvents / totalElapsedMilliseconds : 0d))
                .AppendLine();
            builder.Append("presentation|markersTotal=")
                .Append(totalPresentationMarkers)
                .Append("|markersPerMs=")
                .Append(FormatDouble(totalElapsedMilliseconds > 0d ? totalPresentationMarkers / totalElapsedMilliseconds : 0d))
                .AppendLine();
            builder.Append("thresholds|maxAvgTickMs=")
                .Append(FormatDouble(options.MaxAverageTickMilliseconds))
                .Append("|maxP95RunAvgTickMs=")
                .Append(FormatDouble(options.MaxP95RunAverageTickMilliseconds))
                .Append("|maxRunAvgTickMs=")
                .Append(FormatDouble(options.MaxRunAverageTickMilliseconds))
                .Append("|minTicksPerSecond=")
                .Append(FormatDouble(options.MinTicksPerSecond))
                .AppendLine();
            builder.Append("exports|summary=")
                .Append(summaryPath ?? string.Empty)
                .Append("|scaleSummary=")
                .Append(scaleValidation.Report.SummaryPath ?? string.Empty)
                .AppendLine();

            for (var i = 0; i < variants.Length; i++)
                AppendVariantProfile(builder, variants[i]);

            for (var i = 0; i < runs.Length; i++)
                AppendRunProfile(builder, runs[i]);

            for (var i = 0; i < failures.Count; i++)
            {
                builder.Append("failure|index=")
                    .Append(i)
                    .Append("|message=")
                    .AppendLine(failures[i]);
            }

            return builder.ToString();
        }

        private static void AppendVariantProfile(
            StringBuilder builder,
            in HeadlessAutoChessPerformanceVariantProfile variant)
        {
            builder.Append("variantProfile|name=")
                .Append(variant.VariantName)
                .Append("|runs=")
                .Append(variant.RunCount)
                .Append("|passed=")
                .Append(variant.PassedRunCount)
                .Append("|deterministic=")
                .Append(variant.Deterministic ? "true" : "false")
                .Append("|totalTicks=")
                .Append(variant.TotalTicks)
                .Append("|avgTickMs=")
                .Append(FormatDouble(variant.AverageTickMilliseconds))
                .Append("|p50RunAvgTickMs=")
                .Append(FormatDouble(variant.P50RunAverageTickMilliseconds))
                .Append("|p95RunAvgTickMs=")
                .Append(FormatDouble(variant.P95RunAverageTickMilliseconds))
                .Append("|maxRunAvgTickMs=")
                .Append(FormatDouble(variant.MaxRunAverageTickMilliseconds))
                .Append("|ticksPerSecond=")
                .Append(FormatDouble(variant.TicksPerSecond))
                .Append("|eventsPerTick=")
                .Append(FormatDouble(variant.ReplayEventsPerTick))
                .Append("|markersPerTick=")
                .Append(FormatDouble(variant.PresentationMarkersPerTick))
                .Append("|replay=")
                .Append(variant.TotalReplayEvents)
                .Append("|structured=")
                .Append(variant.TotalStructuredLogEntries)
                .Append("|markers=")
                .Append(variant.TotalPresentationMarkers)
                .Append("|signature=")
                .Append(variant.ReferenceDeterminismSignature)
                .AppendLine();
        }

        private static void AppendRunProfile(
            StringBuilder builder,
            in HeadlessAutoChessPerformanceRunProfile run)
        {
            builder.Append("runProfile|index=")
                .Append(run.RunIndex)
                .Append("|variant=")
                .Append(run.VariantName)
                .Append("|variantRun=")
                .Append(run.VariantRunIndex)
                .Append("|passed=")
                .Append(run.Passed ? "true" : "false")
                .Append("|completed=")
                .Append(run.Completed ? "true" : "false")
                .Append("|winner=")
                .Append(run.Winner)
                .Append("|units=")
                .Append(run.UnitCount)
                .Append("|totalTicks=")
                .Append(run.TotalTicks)
                .Append("|elapsedMs=")
                .Append(FormatDouble(run.ElapsedMilliseconds))
                .Append("|avgTickMs=")
                .Append(FormatDouble(run.AverageTickMilliseconds))
                .Append("|ticksPerSecond=")
                .Append(FormatDouble(run.TicksPerSecond))
                .Append("|eventsPerTick=")
                .Append(FormatDouble(run.ReplayEventsPerTick))
                .Append("|markersPerTick=")
                .Append(FormatDouble(run.PresentationMarkersPerTick))
                .Append("|signature=")
                .Append(run.DeterminismSignature)
                .AppendLine();
        }

        private static int CountPresentationMarkers(in HeadlessAutoChessEventCounts counts)
        {
            return counts.PresentationUiMarkers
                   + counts.PresentationVfxMarkers
                   + counts.PresentationSfxMarkers
                   + counts.PresentationFloatingTextMarkers
                   + counts.PresentationCueMarkers
                   + counts.PresentationSettlementMarkers;
        }

        private static double Percentile(List<double> values, double percentile)
        {
            if (values == null || values.Count == 0)
                return 0d;

            var sorted = values.ToArray();
            Array.Sort(sorted);
            var rank = (int)Math.Ceiling(percentile / 100d * sorted.Length) - 1;
            if (rank < 0)
                rank = 0;
            if (rank >= sorted.Length)
                rank = sorted.Length - 1;
            return sorted[rank];
        }

        private static string ResolveExportDirectory(string exportDirectory)
        {
            return string.IsNullOrWhiteSpace(exportDirectory)
                ? Path.Combine("TestResults", "AutoChess", "T6-CHESS-P")
                : exportDirectory;
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("G9", CultureInfo.InvariantCulture);
        }
    }
}
