using System.Globalization;
using System.Text;

namespace GAS.Runtime
{
    public static class GasRuntimeDerivedExportSink
    {
        public static string ExportToText(
            in GasRuntimeDiagnosticSnapshot snapshot,
            in GasRuntimeDataOrientedScorecard scorecard,
            int maxEvents = 0)
        {
            var diagnosticsText = GasRuntimeDebugger.ExportToText(snapshot, maxEvents);
            var builder = new StringBuilder(diagnosticsText.Length + 1024);
            builder.Append(diagnosticsText);
            AppendDataOrientedScorecard(builder, scorecard);
            return builder.ToString();
        }

        public static string ExportDataOrientedScorecardToText(
            in GasRuntimeDataOrientedScorecard scorecard)
        {
            var builder = new StringBuilder(1024);
            AppendDataOrientedScorecard(builder, scorecard);
            return builder.ToString();
        }

        private static void AppendDataOrientedScorecard(
            StringBuilder builder,
            in GasRuntimeDataOrientedScorecard scorecard)
        {
            builder.Append("runtimeDataOrientedScorecard|source=GasRuntimeDataOrientedScorecard")
                .Append("|passMode=")
                .Append(nameof(GasRuntimeDiagnosticsPassMode.PerfCounter))
                .Append("|costDomain=")
                .Append(nameof(GasRuntimeDiagnosticsCostDomain.Core))
                .Append("|evidenceTier=")
                .Append(nameof(GasRuntimeDiagnosticsEvidenceTier.ValidationEvidence))
                .Append("|units=")
                .Append(scorecard.UnitCount)
                .Append("|measuredTicks=")
                .Append(scorecard.MeasuredTicks)
                .Append("|commands=")
                .Append(scorecard.CommandCount)
                .Append("|commandsPerMeasuredTick=");
            AppendInvariantDouble(builder, scorecard.CommandsPerMeasuredTick);
            builder.Append("|coreFacts=")
                .Append(scorecard.CoreFactCount)
                .Append("|coreFactsPerMeasuredTick=");
            AppendInvariantDouble(builder, scorecard.CoreFactsPerMeasuredTick);
            builder.Append("|activeMutationCommands=")
                .Append(scorecard.ActiveMutationCommandCount)
                .Append("|activeMutationOwnerGroups=")
                .Append(scorecard.ActiveMutationOwnerGroupCount)
                .Append("|activeMutationMaxOwnerRange=")
                .Append(scorecard.ActiveMutationMaxOwnerRange)
                .Append("|activeMutationEstimatedRandomLookups=")
                .Append(scorecard.ActiveMutationEstimatedRandomLookupCount)
                .Append("|pendingAttributeDeltas=")
                .Append(scorecard.PendingAttributeDeltaCount)
                .Append("|pendingAttributeTargetGroups=")
                .Append(scorecard.PendingAttributeTargetGroupCount)
                .Append("|pendingAttributeMaxTargetRange=")
                .Append(scorecard.PendingAttributeMaxTargetRange)
                .Append("|pendingAttributeEstimatedRandomLookups=")
                .Append(scorecard.PendingAttributeEstimatedRandomLookupCount)
                .Append("|ownerLocalFacts=")
                .Append(scorecard.OwnerLocalFactCount)
                .Append("|ownerLocalFactOwnerGroups=")
                .Append(scorecard.OwnerLocalFactOwnerGroupCount)
                .Append("|ownerLocalFactMaxOwnerRange=")
                .Append(scorecard.OwnerLocalFactMaxOwnerRange)
                .Append("|ownerLocalFactFlushes=")
                .Append(scorecard.OwnerLocalFactFlushCount)
                .Append("|activeEffectSlots=")
                .Append(scorecard.ActiveEffectSlotCount)
                .Append("|activeEffectSlotCapacity=")
                .Append(scorecard.ActiveEffectSlotCapacity)
                .Append("|activeEffectDuePeriodSlots=")
                .Append(scorecard.ActiveEffectChunkSkipDuePeriodSlotCount)
                .Append("|queryBudget=")
                .Append(scorecard.QueryBudget)
                .Append("|lookupUpdateBudget=")
                .Append(scorecard.LookupUpdateBudget)
                .Append("|randomLookupBudget=")
                .Append(scorecard.RandomLookupBudget)
                .Append("|syncQueryBudget=")
                .Append(scorecard.SyncQueryBudget)
                .Append("|dependencyWaitRisks=")
                .Append(scorecard.DependencyWaitRiskCount)
                .Append("|performanceObservationPollutionRisks=")
                .Append(scorecard.PerformanceObservationPollutionRiskCount)
                .Append("|metricFamilyMask=0x")
                .Append(((int)scorecard.MetricFamilyMask).ToString("X", CultureInfo.InvariantCulture))
                .Append("|dominantRisk=")
                .Append(scorecard.DominantRisk)
                .Append("|performanceTimingAvailable=")
                .Append(scorecard.PerformanceTimingAvailable)
                .Append("|profilerEvidencePassed=")
                .Append(scorecard.ProfilerEvidencePassed)
                .Append("|measuredAvgTickMs=");
            AppendInvariantDouble(builder, scorecard.MeasuredAverageTickMilliseconds);
            builder.Append("|gasTickAvgMs=");
            AppendInvariantDouble(builder, scorecard.GasTickAverageMilliseconds);
            builder.Append("|gasTickMaxMs=");
            AppendInvariantDouble(builder, scorecard.GasTickMaxMilliseconds);
            builder.Append("|coreRuntimeAvgMs=");
            AppendInvariantDouble(builder, scorecard.CoreRuntimeAverageMilliseconds);
            builder.Append("|coreSimulationAvgMs=");
            AppendInvariantDouble(builder, scorecard.CoreSimulationAverageMilliseconds);
            builder.Append("|boundaryAvgMs=");
            AppendInvariantDouble(builder, scorecard.BoundaryAverageMilliseconds);
            builder.Append("|runnerAvgMs=");
            AppendInvariantDouble(builder, scorecard.RunnerAverageMilliseconds);
            builder.Append("|measuredUsPerUnit=");
            AppendInvariantDouble(builder, scorecard.MeasuredMicrosecondsPerUnit);
            builder.Append("|gasTickUsPerUnit=");
            AppendInvariantDouble(builder, scorecard.GasTickMicrosecondsPerUnit);
            builder.Append("|coreSimulationUsPerUnit=");
            AppendInvariantDouble(builder, scorecard.CoreSimulationMicrosecondsPerUnit);
            builder.Append("|gasTickUsPerCommand=");
            AppendInvariantDouble(builder, scorecard.GasTickMicrosecondsPerCommand);
            builder.Append("|coreSimulationUsPerCoreFact=");
            AppendInvariantDouble(builder, scorecard.CoreSimulationMicrosecondsPerCoreFact);
            builder.Append("|profilerCaptureState=")
                .Append(scorecard.ProfilerCaptureState)
                .AppendLine();
        }

        private static void AppendInvariantDouble(StringBuilder builder, double value)
        {
            builder.Append(value.ToString("G9", CultureInfo.InvariantCulture));
        }
    }
}
