using System;
using System.Collections.Generic;

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
            Variants = variants ?? Array.Empty<HeadlessAutoChessScenarioVariant>();
        }

        public HeadlessAutoChessScaleValidationOptions Normalize()
        {
            var scenario = ScenarioOptions.Normalize();
            var scenarioThresholds = scenario.ValidationThresholds.Normalize();
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
                Variants.Length > 0 ? Variants : new[] { HeadlessAutoChessScenarioVariant.DefaultBalanced });
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
            throw new NotImplementedException(
                "ScaleValidation.Run() removed pending destructive refactor of AutoChessDemo scenario runtime. "
                + "Type definitions preserved. See AutoChessDemo事实.md.");
        }
    }
}
