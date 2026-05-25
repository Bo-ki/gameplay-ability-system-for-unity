using System;

namespace GAS.Runtime
{
    public static class GASRuntimeDebuggerEvidenceGateNames
    {
        public const string GateName = "RuntimeCoreFrameBackbone";
        public const string DebuggerGroupName = "FrameBackbone";
    }

    public enum GASRuntimeDebuggerEvidenceGateId
    {
        None = 0,
        RuntimeCoreFrameBackbone = 1,
    }

    [Flags]
    public enum GASRuntimeDebuggerEvidenceGatePolicy
    {
        None = 0,
        ContractOnly = 1 << 0,
        NumericCountersOnly = 1 << 1,
        BoundaryTextExportOnly = 1 << 2,
        NoSimulationInput = 1 << 3,
        CorePhysicsRenderRunnerSplit = 1 << 4,
        ProfilerJournalingCorrelation = 1 << 5,
        ManualDisablePolicy = 1 << 6,
        SampleEveryFrame = 1 << 7,
        NoManagedStringHotPath = 1 << 8,
        BurstWarmupSeparated = 1 << 9,
    }

    [Flags]
    public enum GASRuntimeDebuggerEvidenceGateCoverage
    {
        None = 0,
        PhaseContract = 1 << 0,
        QueryBudget = 1 << 1,
        LookupBudget = 1 << 2,
        AllocatorOwner = 1 << 3,
        DependencyWait = 1 << 4,
        StreamOwner = 1 << 5,
        StreamMergePolicy = 1 << 6,
        StreamMergeCost = 1 << 7,
        StructuralPlayback = 1 << 8,
        EcbCommandCount = 1 << 9,
        BulkQueryCount = 1 << 10,
        ProfilerCorrelation = 1 << 11,
        JournalingCorrelation = 1 << 12,
        CostGroupSplit = 1 << 13,
        DebuggerOverhead = 1 << 14,
        DisablePolicy = 1 << 15,
        BurstWarmupPolicy = 1 << 16,
    }

    public readonly struct GASRuntimeDebuggerEvidenceGateContract
    {
        public GASRuntimeDebuggerEvidenceGateContract(
            GASRuntimeDebuggerEvidenceGateId gateId,
            string gateName,
            GASRuntimeDebuggerEvidenceGatePolicy policy,
            GASRuntimeDebuggerEvidenceGateCoverage coverage,
            int samplingInterval,
            int overheadBudgetMicroseconds)
        {
            GateId = gateId;
            GateName = gateName ?? string.Empty;
            Policy = policy;
            Coverage = coverage;
            SamplingInterval = samplingInterval;
            OverheadBudgetMicroseconds = overheadBudgetMicroseconds;
        }

        public GASRuntimeDebuggerEvidenceGateId GateId { get; }

        public string GateName { get; }

        public GASRuntimeDebuggerEvidenceGatePolicy Policy { get; }

        public GASRuntimeDebuggerEvidenceGateCoverage Coverage { get; }

        public int SamplingInterval { get; }

        public int OverheadBudgetMicroseconds { get; }

        public bool HasPolicy(GASRuntimeDebuggerEvidenceGatePolicy policy)
        {
            return policy != GASRuntimeDebuggerEvidenceGatePolicy.None
                   && (Policy & policy) == policy;
        }

        public bool HasCoverage(GASRuntimeDebuggerEvidenceGateCoverage coverage)
        {
            return coverage != GASRuntimeDebuggerEvidenceGateCoverage.None
                   && (Coverage & coverage) == coverage;
        }
    }

    public readonly struct GASRuntimeDebuggerEvidenceGatePlan
    {
        public GASRuntimeDebuggerEvidenceGatePlan(
            GASRuntimeDebuggerEvidenceGateContract gate,
            int phaseCount,
            int contractOnlyPhaseCount,
            int queryBudget,
            int filteredQueryBudget,
            int unfilteredQueryBudget,
            int lookupUpdateBudget,
            int randomLookupBudget,
            int syncQueryBudget,
            int dependencyWaitRiskCount,
            int worldUpdateAllocatorOwnerCount,
            int rewindableAllocatorCandidateCount,
            int streamCount,
            int migrationCarrierCount,
            int nativeStreamCandidateCount,
            int ownerLocalBufferCandidateCount,
            int battleHashStreamCount,
            int deterministicMergePolicyCount,
            int mergeCostMeasuredCount,
            int mergeCostMicroseconds,
            int requiredStructuralPlaybackCount,
            int structuralRouteCount,
            int structuralLocalPlaybackMigrationCount,
            int ecbCommandPolicyCount,
            int bulkQueryPolicyCount,
            int cleanupPolicyCount,
            int profilerMarkerCount,
            int journalingMarkerCount,
            int coreCostGroupCount,
            int physicsCostGroupCount,
            int renderCostGroupCount,
            int runnerCostGroupCount,
            int physicsDisabledReasonCount,
            int renderDisabledReasonCount,
            int disablePolicyCount,
            int burstWarmupPolicyCount,
            int hotPathManagedStringCount)
        {
            Gate = gate;
            PhaseCount = phaseCount;
            ContractOnlyPhaseCount = contractOnlyPhaseCount;
            QueryBudget = queryBudget;
            FilteredQueryBudget = filteredQueryBudget;
            UnfilteredQueryBudget = unfilteredQueryBudget;
            LookupUpdateBudget = lookupUpdateBudget;
            RandomLookupBudget = randomLookupBudget;
            SyncQueryBudget = syncQueryBudget;
            DependencyWaitRiskCount = dependencyWaitRiskCount;
            WorldUpdateAllocatorOwnerCount = worldUpdateAllocatorOwnerCount;
            RewindableAllocatorCandidateCount = rewindableAllocatorCandidateCount;
            StreamCount = streamCount;
            MigrationCarrierCount = migrationCarrierCount;
            NativeStreamCandidateCount = nativeStreamCandidateCount;
            OwnerLocalBufferCandidateCount = ownerLocalBufferCandidateCount;
            BattleHashStreamCount = battleHashStreamCount;
            DeterministicMergePolicyCount = deterministicMergePolicyCount;
            MergeCostMeasuredCount = mergeCostMeasuredCount;
            MergeCostMicroseconds = mergeCostMicroseconds;
            RequiredStructuralPlaybackCount = requiredStructuralPlaybackCount;
            StructuralRouteCount = structuralRouteCount;
            StructuralLocalPlaybackMigrationCount = structuralLocalPlaybackMigrationCount;
            EcbCommandPolicyCount = ecbCommandPolicyCount;
            BulkQueryPolicyCount = bulkQueryPolicyCount;
            CleanupPolicyCount = cleanupPolicyCount;
            ProfilerMarkerCount = profilerMarkerCount;
            JournalingMarkerCount = journalingMarkerCount;
            CoreCostGroupCount = coreCostGroupCount;
            PhysicsCostGroupCount = physicsCostGroupCount;
            RenderCostGroupCount = renderCostGroupCount;
            RunnerCostGroupCount = runnerCostGroupCount;
            PhysicsDisabledReasonCount = physicsDisabledReasonCount;
            RenderDisabledReasonCount = renderDisabledReasonCount;
            DisablePolicyCount = disablePolicyCount;
            BurstWarmupPolicyCount = burstWarmupPolicyCount;
            HotPathManagedStringCount = hotPathManagedStringCount;
        }

        public GASRuntimeDebuggerEvidenceGateContract Gate { get; }

        public int PhaseCount { get; }

        public int ContractOnlyPhaseCount { get; }

        public int QueryBudget { get; }

        public int FilteredQueryBudget { get; }

        public int UnfilteredQueryBudget { get; }

        public int LookupUpdateBudget { get; }

        public int RandomLookupBudget { get; }

        public int SyncQueryBudget { get; }

        public int DependencyWaitRiskCount { get; }

        public int WorldUpdateAllocatorOwnerCount { get; }

        public int RewindableAllocatorCandidateCount { get; }

        public int StreamCount { get; }

        public int MigrationCarrierCount { get; }

        public int NativeStreamCandidateCount { get; }

        public int OwnerLocalBufferCandidateCount { get; }

        public int BattleHashStreamCount { get; }

        public int DeterministicMergePolicyCount { get; }

        public int MergeCostMeasuredCount { get; }

        public int MergeCostMicroseconds { get; }

        public int RequiredStructuralPlaybackCount { get; }

        public int StructuralRouteCount { get; }

        public int StructuralLocalPlaybackMigrationCount { get; }

        public int EcbCommandPolicyCount { get; }

        public int BulkQueryPolicyCount { get; }

        public int CleanupPolicyCount { get; }

        public int ProfilerMarkerCount { get; }

        public int JournalingMarkerCount { get; }

        public int CoreCostGroupCount { get; }

        public int PhysicsCostGroupCount { get; }

        public int RenderCostGroupCount { get; }

        public int RunnerCostGroupCount { get; }

        public int PhysicsDisabledReasonCount { get; }

        public int RenderDisabledReasonCount { get; }

        public int DisablePolicyCount { get; }

        public int BurstWarmupPolicyCount { get; }

        public int HotPathManagedStringCount { get; }
    }

    public static class GASRuntimeDebuggerEvidenceGatePlanner
    {
        public const int DefaultSamplingInterval = 1;
        public const int DefaultOverheadBudgetMicroseconds = 50;

        public static GASRuntimeDebuggerEvidenceGatePlan CreateCurrent()
        {
            var phases = GASSystemScheduleContract.RuntimeCoreFramePhases;
            var frameBudget = GASRuntimeFrameBudgetPlanner.CreateCurrent();
            var streams = GASRuntimeFrameStreamOwnerPlanner.CreateCurrent();
            var structural = GASRuntimeStructuralPlaybackGatePlanner.CreateCurrent();

            return new GASRuntimeDebuggerEvidenceGatePlan(
                CreateGate(),
                phases.Count,
                CountContractOnlyPhases(phases),
                frameBudget.TotalQueryBudget,
                frameBudget.TotalFilteredQueryBudget,
                frameBudget.TotalUnfilteredQueryBudget,
                frameBudget.TotalLookupUpdateBudget,
                frameBudget.TotalRandomLookupBudget,
                frameBudget.TotalSyncQueryBudget,
                frameBudget.DependencyWaitRiskCount,
                frameBudget.WorldUpdateAllocatorOwnerCount,
                frameBudget.RewindableAllocatorCandidateCount,
                streams.EntryCount,
                streams.MigrationCarrierCount,
                streams.NativeStreamCandidateCount,
                streams.OwnerLocalBufferCandidateCount,
                streams.BattleHashStreamCount,
                CountDeterministicMergePolicies(streams),
                mergeCostMeasuredCount: 0,
                mergeCostMicroseconds: 0,
                structural.RequiredStructuralPlaybackCount,
                structural.Routes.Count,
                structural.LocalPlaybackMigrationCount,
                structural.CountRoutesWithPolicy(GASRuntimeStructuralPlaybackPolicy.EcbCommandBuffer),
                structural.BulkQueryCandidateCount,
                structural.CleanupPolicyCandidateCount,
                profilerMarkerCount: 4,
                journalingMarkerCount: 1,
                coreCostGroupCount: 1,
                physicsCostGroupCount: 1,
                renderCostGroupCount: 1,
                runnerCostGroupCount: 1,
                physicsDisabledReasonCount: 1,
                renderDisabledReasonCount: 1,
                disablePolicyCount: 1,
                burstWarmupPolicyCount: 1,
                hotPathManagedStringCount: 0);
        }

        private static GASRuntimeDebuggerEvidenceGateContract CreateGate()
        {
            return new GASRuntimeDebuggerEvidenceGateContract(
                GASRuntimeDebuggerEvidenceGateId.RuntimeCoreFrameBackbone,
                GASRuntimeDebuggerEvidenceGateNames.GateName,
                GASRuntimeDebuggerEvidenceGatePolicy.ContractOnly
                | GASRuntimeDebuggerEvidenceGatePolicy.NumericCountersOnly
                | GASRuntimeDebuggerEvidenceGatePolicy.BoundaryTextExportOnly
                | GASRuntimeDebuggerEvidenceGatePolicy.NoSimulationInput
                | GASRuntimeDebuggerEvidenceGatePolicy.CorePhysicsRenderRunnerSplit
                | GASRuntimeDebuggerEvidenceGatePolicy.ProfilerJournalingCorrelation
                | GASRuntimeDebuggerEvidenceGatePolicy.ManualDisablePolicy
                | GASRuntimeDebuggerEvidenceGatePolicy.SampleEveryFrame
                | GASRuntimeDebuggerEvidenceGatePolicy.NoManagedStringHotPath
                | GASRuntimeDebuggerEvidenceGatePolicy.BurstWarmupSeparated,
                GASRuntimeDebuggerEvidenceGateCoverage.PhaseContract
                | GASRuntimeDebuggerEvidenceGateCoverage.QueryBudget
                | GASRuntimeDebuggerEvidenceGateCoverage.LookupBudget
                | GASRuntimeDebuggerEvidenceGateCoverage.AllocatorOwner
                | GASRuntimeDebuggerEvidenceGateCoverage.DependencyWait
                | GASRuntimeDebuggerEvidenceGateCoverage.StreamOwner
                | GASRuntimeDebuggerEvidenceGateCoverage.StreamMergePolicy
                | GASRuntimeDebuggerEvidenceGateCoverage.StreamMergeCost
                | GASRuntimeDebuggerEvidenceGateCoverage.StructuralPlayback
                | GASRuntimeDebuggerEvidenceGateCoverage.EcbCommandCount
                | GASRuntimeDebuggerEvidenceGateCoverage.BulkQueryCount
                | GASRuntimeDebuggerEvidenceGateCoverage.ProfilerCorrelation
                | GASRuntimeDebuggerEvidenceGateCoverage.JournalingCorrelation
                | GASRuntimeDebuggerEvidenceGateCoverage.CostGroupSplit
                | GASRuntimeDebuggerEvidenceGateCoverage.DebuggerOverhead
                | GASRuntimeDebuggerEvidenceGateCoverage.DisablePolicy
                | GASRuntimeDebuggerEvidenceGateCoverage.BurstWarmupPolicy,
                DefaultSamplingInterval,
                DefaultOverheadBudgetMicroseconds);
        }

        private static int CountContractOnlyPhases(
            System.Collections.Generic.IReadOnlyList<GASRuntimeCoreFramePhaseContract> phases)
        {
            var count = 0;
            for (var i = 0; i < phases.Count; i++)
            {
                if (phases[i].ContractOnly)
                    count++;
            }

            return count;
        }

        private static int CountDeterministicMergePolicies(GASRuntimeFrameStreamOwnerPlan streams)
        {
            var count = 0;
            var entries = streams.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var policy = entries[i].MergePolicy;
                if (policy != EGasRuntimeFrameStreamMergePolicy.None
                    && policy != EGasRuntimeFrameStreamMergePolicy.ExcludedFromGameplayMerge)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
