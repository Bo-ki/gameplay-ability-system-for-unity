using System;

namespace GAS.Runtime
{
    public enum GasRuntimeDiagnosticsPassMode : byte
    {
        Off = 0,
        PerfCounter = 1,
        DiagnosticSample = 2,
        OfficialCapture = 3,
        DerivedExport = 4,
    }

    public enum GasRuntimeDiagnosticsCostDomain : byte
    {
        Core = 0,
        Boundary = 1,
        Debugger = 2,
        Runner = 3,
        Physics = 4,
        Render = 5,
        Presentation = 6,
    }

    public enum GasRuntimeDiagnosticsEvidenceTier : byte
    {
        RuntimeCounter = 0,
        DiagnosticMaterialization = 1,
        OfficialCapture = 2,
        ValidationEvidence = 3,
        DerivedExport = 4,
    }

    [Flags]
    public enum GasRuntimeDiagnosticsMetricFamilyMask : int
    {
        None = 0,
        Workload = 1 << 0,
        GasConcept = 1 << 1,
        DataShape = 1 << 2,
        ApiHealth = 1 << 3,
        Timing = 1 << 4,
        Overhead = 1 << 5,
    }

    public enum GasRuntimeDataOrientedDominantRisk : byte
    {
        None = 0,
        MissingPerformanceTiming = 1,
        ObservationPollution = 2,
        DependencyWait = 3,
        SyncQuery = 4,
        RandomLookup = 5,
        OwnerLocality = 6,
        BufferCapacityPressure = 7,
        MissingProfilerEvidence = 8,
    }

    public readonly struct GasRuntimeDataOrientedScorecardInput
    {
        public readonly int UnitCount;
        public readonly int MeasuredTicks;
        public readonly int CommandCount;
        public readonly bool PerformanceTimingAvailable;
        public readonly bool ProfilerEvidencePassed;
        public readonly double MeasuredAverageTickMilliseconds;
        public readonly double GasTickAverageMilliseconds;
        public readonly double GasTickMaxMilliseconds;
        public readonly double CoreRuntimeAverageMilliseconds;
        public readonly double CoreSimulationAverageMilliseconds;
        public readonly double BoundaryAverageMilliseconds;
        public readonly double RunnerAverageMilliseconds;
        public readonly string ProfilerCaptureState;

        public GasRuntimeDataOrientedScorecardInput(
            int unitCount,
            int measuredTicks,
            int commandCount,
            bool performanceTimingAvailable,
            bool profilerEvidencePassed,
            double measuredAverageTickMilliseconds,
            double gasTickAverageMilliseconds,
            double gasTickMaxMilliseconds,
            double coreRuntimeAverageMilliseconds,
            double coreSimulationAverageMilliseconds,
            double boundaryAverageMilliseconds,
            double runnerAverageMilliseconds,
            string profilerCaptureState)
        {
            UnitCount = unitCount;
            MeasuredTicks = measuredTicks;
            CommandCount = commandCount;
            PerformanceTimingAvailable = performanceTimingAvailable;
            ProfilerEvidencePassed = profilerEvidencePassed;
            MeasuredAverageTickMilliseconds = measuredAverageTickMilliseconds;
            GasTickAverageMilliseconds = gasTickAverageMilliseconds;
            GasTickMaxMilliseconds = gasTickMaxMilliseconds;
            CoreRuntimeAverageMilliseconds = coreRuntimeAverageMilliseconds;
            CoreSimulationAverageMilliseconds = coreSimulationAverageMilliseconds;
            BoundaryAverageMilliseconds = boundaryAverageMilliseconds;
            RunnerAverageMilliseconds = runnerAverageMilliseconds;
            ProfilerCaptureState = profilerCaptureState ?? string.Empty;
        }
    }

    public readonly struct GasRuntimeDataOrientedScorecard
    {
        public readonly int UnitCount;
        public readonly int MeasuredTicks;
        public readonly int CommandCount;
        public readonly int CoreFactCount;
        public readonly int ActiveMutationCommandCount;
        public readonly int ActiveMutationOwnerGroupCount;
        public readonly int ActiveMutationMaxOwnerRange;
        public readonly int ActiveMutationEstimatedRandomLookupCount;
        public readonly int PendingAttributeDeltaCount;
        public readonly int PendingAttributeTargetGroupCount;
        public readonly int PendingAttributeMaxTargetRange;
        public readonly int PendingAttributeEstimatedRandomLookupCount;
        public readonly int OwnerLocalFactCount;
        public readonly int OwnerLocalFactOwnerGroupCount;
        public readonly int OwnerLocalFactMaxOwnerRange;
        public readonly int OwnerLocalFactFlushCount;
        public readonly int ActiveEffectSlotCount;
        public readonly int ActiveEffectSlotCapacity;
        public readonly int ActiveEffectChunkSkipDuePeriodSlotCount;
        public readonly int QueryBudget;
        public readonly int LookupUpdateBudget;
        public readonly int RandomLookupBudget;
        public readonly int SyncQueryBudget;
        public readonly int DependencyWaitRiskCount;
        public readonly int PerformanceObservationPollutionRiskCount;
        public readonly GasRuntimeDiagnosticsMetricFamilyMask MetricFamilyMask;
        public readonly GasRuntimeDataOrientedDominantRisk DominantRisk;
        public readonly bool PerformanceTimingAvailable;
        public readonly bool ProfilerEvidencePassed;
        public readonly double MeasuredAverageTickMilliseconds;
        public readonly double GasTickAverageMilliseconds;
        public readonly double GasTickMaxMilliseconds;
        public readonly double CoreRuntimeAverageMilliseconds;
        public readonly double CoreSimulationAverageMilliseconds;
        public readonly double BoundaryAverageMilliseconds;
        public readonly double RunnerAverageMilliseconds;
        public readonly double CommandsPerMeasuredTick;
        public readonly double CoreFactsPerMeasuredTick;
        public readonly double MeasuredMicrosecondsPerUnit;
        public readonly double GasTickMicrosecondsPerUnit;
        public readonly double CoreSimulationMicrosecondsPerUnit;
        public readonly double GasTickMicrosecondsPerCommand;
        public readonly double CoreSimulationMicrosecondsPerCoreFact;
        public readonly string ProfilerCaptureState;

        private GasRuntimeDataOrientedScorecard(
            in GasRuntimeDataOrientedScorecardInput input,
            in GasRuntimeCoreDiagnosticCounters counters,
            in GasRuntimeObservationMaterializationCounters observation)
        {
            UnitCount = input.UnitCount;
            MeasuredTicks = input.MeasuredTicks;
            CommandCount = input.CommandCount;
            CoreFactCount = counters.FactCount;
            ActiveMutationCommandCount = counters.ActiveMutationCommandCount;
            ActiveMutationOwnerGroupCount = counters.ActiveMutationOwnerGroupCount;
            ActiveMutationMaxOwnerRange = counters.ActiveMutationMaxOwnerRange;
            ActiveMutationEstimatedRandomLookupCount =
                counters.ActiveMutationEstimatedRandomLookupCount;
            PendingAttributeDeltaCount = counters.PendingAttributeDeltaCount;
            PendingAttributeTargetGroupCount = counters.PendingAttributeTargetGroupCount;
            PendingAttributeMaxTargetRange = counters.PendingAttributeMaxTargetRange;
            PendingAttributeEstimatedRandomLookupCount =
                counters.PendingAttributeEstimatedRandomLookupCount;
            OwnerLocalFactCount = counters.OwnerLocalFactCount;
            OwnerLocalFactOwnerGroupCount = counters.OwnerLocalFactOwnerGroupCount;
            OwnerLocalFactMaxOwnerRange = counters.OwnerLocalFactMaxOwnerRange;
            OwnerLocalFactFlushCount = counters.OwnerLocalFactFlushCount;
            ActiveEffectSlotCount = counters.ActiveEffectSlotCount;
            ActiveEffectSlotCapacity = counters.ActiveEffectSlotCapacity;
            ActiveEffectChunkSkipDuePeriodSlotCount =
                counters.ActiveEffectChunkSkipDuePeriodSlotCount;
            QueryBudget = counters.QueryBudget;
            LookupUpdateBudget = counters.LookupUpdateBudget;
            RandomLookupBudget = counters.RandomLookupBudget;
            SyncQueryBudget = counters.SyncQueryBudget;
            DependencyWaitRiskCount = counters.DependencyWaitRiskCount;
            PerformanceObservationPollutionRiskCount =
                observation.PerformancePollutionRiskCount;
            MetricFamilyMask = ComputeMetricFamilyMask(input, counters, observation);
            DominantRisk = ComputeDominantRisk(input, counters, observation);
            PerformanceTimingAvailable = input.PerformanceTimingAvailable;
            ProfilerEvidencePassed = input.ProfilerEvidencePassed;
            MeasuredAverageTickMilliseconds = input.MeasuredAverageTickMilliseconds;
            GasTickAverageMilliseconds = input.GasTickAverageMilliseconds;
            GasTickMaxMilliseconds = input.GasTickMaxMilliseconds;
            CoreRuntimeAverageMilliseconds = input.CoreRuntimeAverageMilliseconds;
            CoreSimulationAverageMilliseconds = input.CoreSimulationAverageMilliseconds;
            BoundaryAverageMilliseconds = input.BoundaryAverageMilliseconds;
            RunnerAverageMilliseconds = input.RunnerAverageMilliseconds;
            CommandsPerMeasuredTick = Divide(input.CommandCount, input.MeasuredTicks);
            CoreFactsPerMeasuredTick = Divide(counters.FactCount, input.MeasuredTicks);
            MeasuredMicrosecondsPerUnit =
                Divide(input.MeasuredAverageTickMilliseconds * 1000d, input.UnitCount);
            GasTickMicrosecondsPerUnit =
                Divide(input.GasTickAverageMilliseconds * 1000d, input.UnitCount);
            CoreSimulationMicrosecondsPerUnit =
                Divide(input.CoreSimulationAverageMilliseconds * 1000d, input.UnitCount);
            GasTickMicrosecondsPerCommand =
                Divide(input.GasTickAverageMilliseconds * 1000d, CommandsPerMeasuredTick);
            CoreSimulationMicrosecondsPerCoreFact =
                Divide(input.CoreSimulationAverageMilliseconds * 1000d, CoreFactsPerMeasuredTick);
            ProfilerCaptureState = input.ProfilerCaptureState ?? string.Empty;
        }

        public static GasRuntimeDataOrientedScorecard Create(
            in GasRuntimeDataOrientedScorecardInput input,
            in GasRuntimeDiagnosticSnapshot diagnostics)
        {
            return new GasRuntimeDataOrientedScorecard(
                input,
                diagnostics.CoreCounters,
                diagnostics.ObservationMaterializationCounters);
        }

        private static double Divide(double numerator, double denominator)
        {
            return denominator > 0d ? numerator / denominator : 0d;
        }

        private static GasRuntimeDiagnosticsMetricFamilyMask ComputeMetricFamilyMask(
            in GasRuntimeDataOrientedScorecardInput input,
            in GasRuntimeCoreDiagnosticCounters counters,
            in GasRuntimeObservationMaterializationCounters observation)
        {
            var mask = GasRuntimeDiagnosticsMetricFamilyMask.None;

            if (input.UnitCount > 0
                || input.MeasuredTicks > 0
                || input.CommandCount > 0
                || counters.FactCount > 0)
            {
                mask |= GasRuntimeDiagnosticsMetricFamilyMask.Workload;
            }

            if (input.CommandCount > 0
                || counters.SpecCount > 0
                || counters.DeltaCount > 0
                || counters.FactCount > 0
                || counters.CueCount > 0
                || counters.ActiveEffectSlotCount > 0
                || counters.PendingAttributeDeltaCount > 0)
            {
                mask |= GasRuntimeDiagnosticsMetricFamilyMask.GasConcept;
            }

            if (counters.ActiveMutationOwnerGroupCount > 0
                || counters.ActiveMutationMaxOwnerRange > 0
                || counters.PendingAttributeTargetGroupCount > 0
                || counters.PendingAttributeMaxTargetRange > 0
                || counters.OwnerLocalFactOwnerGroupCount > 0
                || counters.OwnerLocalFactMaxOwnerRange > 0
                || counters.ActiveEffectSlotCapacity > 0)
            {
                mask |= GasRuntimeDiagnosticsMetricFamilyMask.DataShape;
            }

            if (counters.QueryBudget > 0
                || counters.LookupUpdateBudget > 0
                || counters.RandomLookupBudget > 0
                || counters.SyncQueryBudget > 0
                || counters.DependencyWaitRiskCount > 0)
            {
                mask |= GasRuntimeDiagnosticsMetricFamilyMask.ApiHealth;
            }

            if (input.PerformanceTimingAvailable
                || input.MeasuredAverageTickMilliseconds > 0d
                || input.GasTickAverageMilliseconds > 0d
                || input.CoreSimulationAverageMilliseconds > 0d)
            {
                mask |= GasRuntimeDiagnosticsMetricFamilyMask.Timing;
            }

            if (observation.PerformancePollutionRiskCount > 0
                || !input.ProfilerEvidencePassed
                || !string.IsNullOrEmpty(input.ProfilerCaptureState))
            {
                mask |= GasRuntimeDiagnosticsMetricFamilyMask.Overhead;
            }

            return mask;
        }

        private static GasRuntimeDataOrientedDominantRisk ComputeDominantRisk(
            in GasRuntimeDataOrientedScorecardInput input,
            in GasRuntimeCoreDiagnosticCounters counters,
            in GasRuntimeObservationMaterializationCounters observation)
        {
            if (!input.PerformanceTimingAvailable)
                return GasRuntimeDataOrientedDominantRisk.MissingPerformanceTiming;
            if (observation.PerformancePollutionRiskCount > 0)
                return GasRuntimeDataOrientedDominantRisk.ObservationPollution;
            if (counters.DependencyWaitRiskCount > 0)
                return GasRuntimeDataOrientedDominantRisk.DependencyWait;
            if (counters.SyncQueryBudget > 0)
                return GasRuntimeDataOrientedDominantRisk.SyncQuery;
            if (counters.RandomLookupBudget > 0
                || counters.ActiveMutationEstimatedRandomLookupCount > 0
                || counters.PendingAttributeEstimatedRandomLookupCount > 0)
            {
                return GasRuntimeDataOrientedDominantRisk.RandomLookup;
            }
            if (counters.ActiveMutationMaxOwnerRange > 1
                || counters.PendingAttributeMaxTargetRange > 1
                || counters.OwnerLocalFactMaxOwnerRange > 1)
            {
                return GasRuntimeDataOrientedDominantRisk.OwnerLocality;
            }
            if (counters.ActiveEffectSlotCapacity > 0
                && counters.ActiveEffectSlotCount * 100 >= counters.ActiveEffectSlotCapacity * 70)
            {
                return GasRuntimeDataOrientedDominantRisk.BufferCapacityPressure;
            }
            if (!input.ProfilerEvidencePassed)
                return GasRuntimeDataOrientedDominantRisk.MissingProfilerEvidence;

            return GasRuntimeDataOrientedDominantRisk.None;
        }
    }
}
