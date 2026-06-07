using System;
using System.Collections.Generic;

namespace GAS.Runtime
{
    public enum GASRuntimeFrameBackboneRebindTaskId
    {
        None = 0,
        InstantSpecEvaluation = 1,
        ActiveEffectStore = 2,
    }

    public enum GASRuntimeFrameBackboneNextTaskId
    {
        None = 0,
        RuntimeCoreInstantSpecEvaluation = 1,
        RuntimeCoreActiveEffectStore = 2,
        RuntimeBoundaryFullChainValidation = 3,
    }

    [Flags]
    public enum GASRuntimeFrameBackboneRequiredEvidence
    {
        None = 0,
        PhaseContract = 1 << 0,
        FrameBudget = 1 << 1,
        StreamOwner = 1 << 2,
        DeterministicMerge = 1 << 3,
        StructuralPlaybackGate = 1 << 4,
        DebuggerEvidenceGate = 1 << 5,
        ApiSelectionTable = 1 << 6,
        OfficialDocCoverage = 1 << 7,
        NoRuntimeBoundaryBusinessChange = 1 << 8,
    }

    [Flags]
    public enum GASRuntimeFrameBackboneCommandClassification
    {
        None = 0,
        BoundaryRequest = 1 << 0,
        CoreFrameCommand = 1 << 1,
        ParallelFanInStream = 1 << 2,
        StructuralMutationRequest = 1 << 3,
    }

    [Flags]
    public enum GASRuntimeFrameBackboneStoreSurface
    {
        None = 0,
        OwnerLocalStore = 1 << 0,
        GlobalIndexedStore = 1 << 1,
        LifecycleCleanupStore = 1 << 2,
        ChunkSkipIndex = 1 << 3,
    }

    [Flags]
    public enum GASRuntimeFrameBackboneRebindPolicy
    {
        None = 0,
        ContractFirstHandoff = 1 << 0,
        RejectSingletonDynamicBufferAsScaleReady = 1 << 1,
        RejectLegacyBackedMirrorAsScaleReady = 1 << 2,
        ReuseFrameStreamOwner = 1 << 3,
        ReuseStructuralPlaybackGate = 1 << 4,
        ReuseDebuggerEvidenceGate = 1 << 5,
        RequireApiSelectionTable = 1 << 6,
        RequireOfficialDocCoverage = 1 << 7,
        NoRuntimeBoundaryBusinessChange = 1 << 8,
        PreferRuntimeCoreBeforeBoundaryValidation = 1 << 9,
    }

    public readonly struct GASRuntimeFrameBackboneRebindEntry
    {
        private readonly EGasRuntimeCoreFramePhase[] _phaseAnchors;
        private readonly EGasRuntimeFrameStreamId[] _streamIds;

        public GASRuntimeFrameBackboneRebindEntry(
            GASRuntimeFrameBackboneRebindTaskId taskId,
            string taskTreeId,
            string taskName,
            IEnumerable<EGasRuntimeCoreFramePhase> phaseAnchors,
            IEnumerable<EGasRuntimeFrameStreamId> streamIds,
            GASRuntimeFrameBackboneCommandClassification commandClassification,
            GASRuntimeFrameBackboneStoreSurface storeSurfaces,
            GASRuntimeStructuralPlaybackGateId structuralGateId,
            GASRuntimeFrameBackboneRequiredEvidence requiredEvidence,
            GASRuntimeFrameBackboneRebindPolicy policy)
        {
            TaskId = taskId;
            TaskTreeId = taskTreeId ?? string.Empty;
            TaskName = taskName ?? string.Empty;
            _phaseAnchors = phaseAnchors is EGasRuntimeCoreFramePhase[] phaseArray
                ? (EGasRuntimeCoreFramePhase[])phaseArray.Clone()
                : new List<EGasRuntimeCoreFramePhase>(
                    phaseAnchors ?? Array.Empty<EGasRuntimeCoreFramePhase>()).ToArray();
            _streamIds = streamIds is EGasRuntimeFrameStreamId[] streamArray
                ? (EGasRuntimeFrameStreamId[])streamArray.Clone()
                : new List<EGasRuntimeFrameStreamId>(
                    streamIds ?? Array.Empty<EGasRuntimeFrameStreamId>()).ToArray();
            CommandClassification = commandClassification;
            StoreSurfaces = storeSurfaces;
            StructuralGateId = structuralGateId;
            RequiredEvidence = requiredEvidence;
            Policy = policy;
        }

        public GASRuntimeFrameBackboneRebindTaskId TaskId { get; }

        public string TaskTreeId { get; }

        public string TaskName { get; }

        public IReadOnlyList<EGasRuntimeCoreFramePhase> PhaseAnchors =>
            _phaseAnchors ?? Array.Empty<EGasRuntimeCoreFramePhase>();

        public IReadOnlyList<EGasRuntimeFrameStreamId> StreamIds =>
            _streamIds ?? Array.Empty<EGasRuntimeFrameStreamId>();

        public GASRuntimeFrameBackboneCommandClassification CommandClassification { get; }

        public GASRuntimeFrameBackboneStoreSurface StoreSurfaces { get; }

        public GASRuntimeStructuralPlaybackGateId StructuralGateId { get; }

        public GASRuntimeFrameBackboneRequiredEvidence RequiredEvidence { get; }

        public GASRuntimeFrameBackboneRebindPolicy Policy { get; }

        public bool HasRequiredEvidence(GASRuntimeFrameBackboneRequiredEvidence evidence)
        {
            return evidence != GASRuntimeFrameBackboneRequiredEvidence.None
                   && (RequiredEvidence & evidence) == evidence;
        }

        public bool HasCommandClassification(GASRuntimeFrameBackboneCommandClassification classification)
        {
            return classification != GASRuntimeFrameBackboneCommandClassification.None
                   && (CommandClassification & classification) == classification;
        }

        public bool HasStoreSurface(GASRuntimeFrameBackboneStoreSurface surface)
        {
            return surface != GASRuntimeFrameBackboneStoreSurface.None
                   && (StoreSurfaces & surface) == surface;
        }

        public bool HasPolicy(GASRuntimeFrameBackboneRebindPolicy policy)
        {
            return policy != GASRuntimeFrameBackboneRebindPolicy.None
                   && (Policy & policy) == policy;
        }

        public bool ContainsPhaseAnchor(EGasRuntimeCoreFramePhase phase)
        {
            var phases = PhaseAnchors;
            for (var i = 0; i < phases.Count; i++)
            {
                if (phases[i] == phase)
                    return true;
            }

            return false;
        }

        public bool ContainsStream(EGasRuntimeFrameStreamId streamId)
        {
            var streams = StreamIds;
            for (var i = 0; i < streams.Count; i++)
            {
                if (streams[i] == streamId)
                    return true;
            }

            return false;
        }
    }

    public readonly struct GASRuntimeFrameBackboneRebindPlan
    {
        private readonly GASRuntimeFrameBackboneRebindEntry[] _entries;

        public GASRuntimeFrameBackboneRebindPlan(
            IEnumerable<GASRuntimeFrameBackboneRebindEntry> entries,
            GASRuntimeFrameBackboneNextTaskId recommendedNextTask,
            bool boundaryValidationAllowed,
            bool am2bBackboneEvidenceComplete,
            int phaseCount,
            int frameBudgetEntryCount,
            int streamOwnerEntryCount,
            int deterministicMergePolicyCount,
            int structuralPlaybackRouteCount,
            int debuggerEvidenceCoverageCount)
        {
            _entries = entries is GASRuntimeFrameBackboneRebindEntry[] array
                ? (GASRuntimeFrameBackboneRebindEntry[])array.Clone()
                : new List<GASRuntimeFrameBackboneRebindEntry>(
                    entries ?? Array.Empty<GASRuntimeFrameBackboneRebindEntry>()).ToArray();
            RecommendedNextTask = recommendedNextTask;
            BoundaryValidationAllowed = boundaryValidationAllowed;
            AM2BBackboneEvidenceComplete = am2bBackboneEvidenceComplete;
            PhaseCount = phaseCount;
            FrameBudgetEntryCount = frameBudgetEntryCount;
            StreamOwnerEntryCount = streamOwnerEntryCount;
            DeterministicMergePolicyCount = deterministicMergePolicyCount;
            StructuralPlaybackRouteCount = structuralPlaybackRouteCount;
            DebuggerEvidenceCoverageCount = debuggerEvidenceCoverageCount;
        }

        public IReadOnlyList<GASRuntimeFrameBackboneRebindEntry> Entries =>
            _entries ?? Array.Empty<GASRuntimeFrameBackboneRebindEntry>();

        public int EntryCount => Entries.Count;

        public GASRuntimeFrameBackboneNextTaskId RecommendedNextTask { get; }

        public bool BoundaryValidationAllowed { get; }

        public bool AM2BBackboneEvidenceComplete { get; }

        public int PhaseCount { get; }

        public int FrameBudgetEntryCount { get; }

        public int StreamOwnerEntryCount { get; }

        public int DeterministicMergePolicyCount { get; }

        public int StructuralPlaybackRouteCount { get; }

        public int DebuggerEvidenceCoverageCount { get; }

        public bool TryFind(
            GASRuntimeFrameBackboneRebindTaskId taskId,
            out GASRuntimeFrameBackboneRebindEntry entry)
        {
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].TaskId == taskId)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public int CountEntriesRequiringEvidence(GASRuntimeFrameBackboneRequiredEvidence evidence)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].HasRequiredEvidence(evidence))
                    count++;
            }

            return count;
        }

        public int CountEntriesWithPolicy(GASRuntimeFrameBackboneRebindPolicy policy)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].HasPolicy(policy))
                    count++;
            }

            return count;
        }
    }

    public static class GASRuntimeFrameBackboneRebindPlanner
    {
        public static GASRuntimeFrameBackboneRebindPlan CreateCurrent()
        {
            var phases = GASSystemScheduleContract.RuntimeCoreFramePhases;
            var frameBudget = GASRuntimeFrameBudgetPlanner.CreateCurrent();
            var streams = GASRuntimeFrameStreamOwnerPlanner.CreateCurrent();
            var structural = GASRuntimeStructuralPlaybackGatePlanner.CreateCurrent();
            var debuggerEvidence = GASRuntimeDebuggerEvidenceGatePlanner.CreateCurrent();
            var deterministicMergePolicyCount = CountDeterministicMergePolicies(streams);
            var coverageCount = CountDebuggerEvidenceCoverage(debuggerEvidence.Gate.Coverage);
            var evidenceComplete = phases.Count >= 8
                                   && frameBudget.EntryCount > 0
                                   && streams.EntryCount > 0
                                   && deterministicMergePolicyCount == streams.EntryCount
                                   && structural.Gate.GateId == GASRuntimeStructuralPlaybackGateId.RuntimeCoreHotPath
                                   && debuggerEvidence.Gate.GateId
                                   == GASRuntimeDebuggerEvidenceGateId.RuntimeCoreFrameBackbone;

            return new GASRuntimeFrameBackboneRebindPlan(
                new[]
                {
                    CreateInstantSpecEvaluationEntry(),
                    CreateActiveEffectStoreEntry(),
                },
                GASRuntimeFrameBackboneNextTaskId.RuntimeCoreInstantSpecEvaluation,
                boundaryValidationAllowed: false,
                evidenceComplete,
                phases.Count,
                frameBudget.EntryCount,
                streams.EntryCount,
                deterministicMergePolicyCount,
                structural.RouteCount,
                coverageCount);
        }

        private static GASRuntimeFrameBackboneRebindEntry CreateInstantSpecEvaluationEntry()
        {
            return new GASRuntimeFrameBackboneRebindEntry(
                GASRuntimeFrameBackboneRebindTaskId.InstantSpecEvaluation,
                "T1-RuntimeCore-AM3",
                "GAS ECS Runtime - Runtime Core Refactor - Instant Spec Evaluation Migration",
                new[]
                {
                    EGasRuntimeCoreFramePhase.CommandIngest,
                    EGasRuntimeCoreFramePhase.SpecEvaluation,
                    EGasRuntimeCoreFramePhase.DeltaApply,
                    EGasRuntimeCoreFramePhase.TypedFactProjection,
                },
                new[]
                {
                    EGasRuntimeFrameStreamId.EffectCommand,
                    EGasRuntimeFrameStreamId.EffectCommandSetByCaller,
                    EGasRuntimeFrameStreamId.InstantEffectSpec,
                    EGasRuntimeFrameStreamId.AttributeDelta,
                    EGasRuntimeFrameStreamId.TypedSimulationFact,
                },
                GASRuntimeFrameBackboneCommandClassification.BoundaryRequest
                | GASRuntimeFrameBackboneCommandClassification.CoreFrameCommand
                | GASRuntimeFrameBackboneCommandClassification.ParallelFanInStream
                | GASRuntimeFrameBackboneCommandClassification.StructuralMutationRequest,
                GASRuntimeFrameBackboneStoreSurface.None,
                GASRuntimeStructuralPlaybackGateId.RuntimeCoreHotPath,
                RequiredAM2BEvidence(),
                GASRuntimeFrameBackboneRebindPolicy.ContractFirstHandoff
                | GASRuntimeFrameBackboneRebindPolicy.RejectSingletonDynamicBufferAsScaleReady
                | GASRuntimeFrameBackboneRebindPolicy.ReuseFrameStreamOwner
                | GASRuntimeFrameBackboneRebindPolicy.ReuseStructuralPlaybackGate
                | GASRuntimeFrameBackboneRebindPolicy.ReuseDebuggerEvidenceGate
                | GASRuntimeFrameBackboneRebindPolicy.RequireApiSelectionTable
                | GASRuntimeFrameBackboneRebindPolicy.RequireOfficialDocCoverage
                | GASRuntimeFrameBackboneRebindPolicy.NoRuntimeBoundaryBusinessChange
                | GASRuntimeFrameBackboneRebindPolicy.PreferRuntimeCoreBeforeBoundaryValidation);
        }

        private static GASRuntimeFrameBackboneRebindEntry CreateActiveEffectStoreEntry()
        {
            return new GASRuntimeFrameBackboneRebindEntry(
                GASRuntimeFrameBackboneRebindTaskId.ActiveEffectStore,
                "T1-RuntimeCore-AM5",
                "GAS ECS Runtime - Runtime Core Refactor - Active Effect Store Rebuild",
                new[]
                {
                    EGasRuntimeCoreFramePhase.ActiveEffectLifecycle,
                    EGasRuntimeCoreFramePhase.DeltaApply,
                    EGasRuntimeCoreFramePhase.StructuralPlayback,
                },
                new[]
                {
                    EGasRuntimeFrameStreamId.EffectCommand,
                    EGasRuntimeFrameStreamId.ActiveEffectMutation,
                    EGasRuntimeFrameStreamId.AttributeDelta,
                    EGasRuntimeFrameStreamId.TypedSimulationFact,
                },
                GASRuntimeFrameBackboneCommandClassification.CoreFrameCommand
                | GASRuntimeFrameBackboneCommandClassification.StructuralMutationRequest,
                GASRuntimeFrameBackboneStoreSurface.OwnerLocalStore
                | GASRuntimeFrameBackboneStoreSurface.GlobalIndexedStore
                | GASRuntimeFrameBackboneStoreSurface.LifecycleCleanupStore
                | GASRuntimeFrameBackboneStoreSurface.ChunkSkipIndex,
                GASRuntimeStructuralPlaybackGateId.RuntimeCoreHotPath,
                RequiredAM2BEvidence(),
                GASRuntimeFrameBackboneRebindPolicy.ContractFirstHandoff
                | GASRuntimeFrameBackboneRebindPolicy.RejectLegacyBackedMirrorAsScaleReady
                | GASRuntimeFrameBackboneRebindPolicy.ReuseFrameStreamOwner
                | GASRuntimeFrameBackboneRebindPolicy.ReuseStructuralPlaybackGate
                | GASRuntimeFrameBackboneRebindPolicy.ReuseDebuggerEvidenceGate
                | GASRuntimeFrameBackboneRebindPolicy.RequireApiSelectionTable
                | GASRuntimeFrameBackboneRebindPolicy.RequireOfficialDocCoverage
                | GASRuntimeFrameBackboneRebindPolicy.NoRuntimeBoundaryBusinessChange
                | GASRuntimeFrameBackboneRebindPolicy.PreferRuntimeCoreBeforeBoundaryValidation);
        }

        private static GASRuntimeFrameBackboneRequiredEvidence RequiredAM2BEvidence()
        {
            return GASRuntimeFrameBackboneRequiredEvidence.PhaseContract
                   | GASRuntimeFrameBackboneRequiredEvidence.FrameBudget
                   | GASRuntimeFrameBackboneRequiredEvidence.StreamOwner
                   | GASRuntimeFrameBackboneRequiredEvidence.DeterministicMerge
                   | GASRuntimeFrameBackboneRequiredEvidence.StructuralPlaybackGate
                   | GASRuntimeFrameBackboneRequiredEvidence.DebuggerEvidenceGate
                   | GASRuntimeFrameBackboneRequiredEvidence.ApiSelectionTable
                   | GASRuntimeFrameBackboneRequiredEvidence.OfficialDocCoverage
                   | GASRuntimeFrameBackboneRequiredEvidence.NoRuntimeBoundaryBusinessChange;
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

        private static int CountDebuggerEvidenceCoverage(GASRuntimeDebuggerEvidenceGateCoverage coverage)
        {
            var count = 0;
            var value = (int)coverage;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }

            return count;
        }
    }
}
