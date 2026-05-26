using System;
using System.Collections.Generic;

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
            Variants = variants ?? Array.Empty<HeadlessAutoChessScenarioVariant>();
        }

        public HeadlessAutoChessPerformanceProfileOptions Normalize()
        {
            var scenario = ScenarioOptions.Normalize();
            var thresholds = scenario.ValidationThresholds.Normalize();
            var maxAvg = MaxAverageTickMilliseconds > 0d
                ? MaxAverageTickMilliseconds
                : thresholds.MaxAverageTickMilliseconds;
            return new HeadlessAutoChessPerformanceProfileOptions(
                RunCountPerVariant > 0 ? RunCountPerVariant : 2,
                ExportLogs,
                ExportScaleRunLogs,
                ExportDirectory,
                maxAvg,
                MaxP95RunAverageTickMilliseconds > 0d ? MaxP95RunAverageTickMilliseconds : maxAvg,
                MaxRunAverageTickMilliseconds > 0d ? MaxRunAverageTickMilliseconds : maxAvg,
                MinTicksPerSecond > 0d ? MinTicksPerSecond : 1000d / maxAvg,
                scenario,
                Variants.Length > 0 ? Variants : new[] { HeadlessAutoChessScenarioVariant.DefaultBalanced });
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
            throw new NotImplementedException(
                "PerformanceProfile.Run() removed pending destructive refactor of AutoChessDemo scenario runtime. "
                + "Type definitions preserved. See AutoChessDemo事实.md.");
        }
    }
}
