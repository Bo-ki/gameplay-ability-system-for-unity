using System;
using System.Collections.Generic;

namespace GAS.Runtime
{
    public static class GASRuntimeStructuralPlaybackGateNames
    {
        public const string GroupName = "GASStructuralCommitSystemGroup";
        public const string DebuggerGroupName = "StructuralPlayback";
    }

    public enum GASRuntimeStructuralPlaybackGateId
    {
        None = 0,
        RuntimeCoreHotPath = 1,
    }

    public enum GASRuntimeStructuralPlaybackRouteStatus
    {
        None = 0,
        StructuralCommitPlayback = 1,
        LocalPlaybackMigration = 2,
        DirtyPipelineNoPlayback = 3,
        ObservationBoundaryNoPlayback = 4,
        ManagedBoundaryNoPlayback = 5,
    }

    [Flags]
    public enum GASRuntimeStructuralPlaybackPolicy
    {
        None = 0,
        EcbCommandBuffer = 1 << 0,
        EntityQueryBulkCandidate = 1 << 1,
        ComponentTypeSetBulkCandidate = 1 << 2,
        CleanupComponentCandidate = 1 << 3,
        EnableablePreferred = 1 << 4,
        DirtyPipelineNoStructuralChange = 1 << 5,
        ObservationNoPlayback = 1 << 6,
        ManagedBoundaryOnly = 1 << 7,
    }

    [Flags]
    public enum GASRuntimeStructuralPlaybackEvidence
    {
        None = 0,
        UniqueHotPathGate = 1 << 0,
        RecordOnlySourcePhase = 1 << 1,
        PlaybackOnlyGatePhase = 1 << 2,
        EcbPlaybackCount = 1 << 3,
        EcbCommandCount = 1 << 4,
        BulkQueryPolicy = 1 << 5,
        CleanupPolicy = 1 << 6,
        SourceSystemAttribution = 1 << 7,
        HandleInvalidationBoundary = 1 << 8,
        LocalPlaybackMigration = 1 << 9,
        DebuggerGateTag = 1 << 10,
    }

    public readonly struct GASRuntimeStructuralPlaybackGateContract
    {
        public GASRuntimeStructuralPlaybackGateContract(
            GASRuntimeStructuralPlaybackGateId gateId,
            Type groupType,
            Type endEcbSystemType,
            EGasRuntimeCoreFramePhase phase,
            EGasRuntimeCoreStructuralPermission structuralPermission,
            bool uniqueHotPathGate,
            bool contractOnly,
            GASRuntimeStructuralPlaybackEvidence evidence)
        {
            GateId = gateId;
            GroupType = groupType;
            EndEcbSystemType = endEcbSystemType;
            Phase = phase;
            StructuralPermission = structuralPermission;
            UniqueHotPathGate = uniqueHotPathGate;
            ContractOnly = contractOnly;
            Evidence = evidence;
        }

        public GASRuntimeStructuralPlaybackGateId GateId { get; }

        public Type GroupType { get; }

        public Type EndEcbSystemType { get; }

        public EGasRuntimeCoreFramePhase Phase { get; }

        public EGasRuntimeCoreStructuralPermission StructuralPermission { get; }

        public bool UniqueHotPathGate { get; }

        public bool ContractOnly { get; }

        public GASRuntimeStructuralPlaybackEvidence Evidence { get; }

        public bool HasEvidence(GASRuntimeStructuralPlaybackEvidence evidence)
        {
            return evidence != GASRuntimeStructuralPlaybackEvidence.None
                   && (Evidence & evidence) == evidence;
        }
    }

    public readonly struct GASRuntimeStructuralPlaybackRouteEntry
    {
        public GASRuntimeStructuralPlaybackRouteEntry(
            GASRuntimeStructuralChangeEntryId structuralEntryId,
            GASRuntimeQueryLayoutEntryId layoutEntryId,
            EGasRuntimeCoreFramePhase recordPhase,
            EGasRuntimeCoreFramePhase playbackPhase,
            GASRuntimeStructuralPlaybackGateId gateId,
            GASRuntimeStructuralPlaybackRouteStatus status,
            GASRuntimeStructuralPlaybackPolicy policy,
            GASRuntimeStructuralPlaybackEvidence evidence,
            bool requiresStructuralPlayback,
            bool directEntityManagerAllowed)
        {
            StructuralEntryId = structuralEntryId;
            LayoutEntryId = layoutEntryId;
            RecordPhase = recordPhase;
            PlaybackPhase = playbackPhase;
            GateId = gateId;
            Status = status;
            Policy = policy;
            Evidence = evidence;
            RequiresStructuralPlayback = requiresStructuralPlayback;
            DirectEntityManagerAllowed = directEntityManagerAllowed;
        }

        public GASRuntimeStructuralChangeEntryId StructuralEntryId { get; }

        public GASRuntimeQueryLayoutEntryId LayoutEntryId { get; }

        public EGasRuntimeCoreFramePhase RecordPhase { get; }

        public EGasRuntimeCoreFramePhase PlaybackPhase { get; }

        public GASRuntimeStructuralPlaybackGateId GateId { get; }

        public GASRuntimeStructuralPlaybackRouteStatus Status { get; }

        public GASRuntimeStructuralPlaybackPolicy Policy { get; }

        public GASRuntimeStructuralPlaybackEvidence Evidence { get; }

        public bool RequiresStructuralPlayback { get; }

        public bool DirectEntityManagerAllowed { get; }

        public bool RoutesToStructuralPlaybackGate =>
            RequiresStructuralPlayback
            && GateId == GASRuntimeStructuralPlaybackGateId.RuntimeCoreHotPath
            && PlaybackPhase == EGasRuntimeCoreFramePhase.StructuralPlayback;

        public bool HasPolicy(GASRuntimeStructuralPlaybackPolicy policy)
        {
            return policy != GASRuntimeStructuralPlaybackPolicy.None
                   && (Policy & policy) == policy;
        }

        public bool HasEvidence(GASRuntimeStructuralPlaybackEvidence evidence)
        {
            return evidence != GASRuntimeStructuralPlaybackEvidence.None
                   && (Evidence & evidence) == evidence;
        }
    }

    public readonly struct GASRuntimeStructuralPlaybackGatePlan
    {
        private readonly GASRuntimeStructuralPlaybackRouteEntry[] _routes;

        public GASRuntimeStructuralPlaybackGatePlan(
            GASRuntimeStructuralPlaybackGateContract gate,
            IEnumerable<GASRuntimeStructuralPlaybackRouteEntry> routes)
        {
            Gate = gate;
            _routes = routes is GASRuntimeStructuralPlaybackRouteEntry[] array
                ? (GASRuntimeStructuralPlaybackRouteEntry[])array.Clone()
                : new List<GASRuntimeStructuralPlaybackRouteEntry>(
                    routes ?? Array.Empty<GASRuntimeStructuralPlaybackRouteEntry>()).ToArray();
        }

        public GASRuntimeStructuralPlaybackGateContract Gate { get; }

        public IReadOnlyList<GASRuntimeStructuralPlaybackRouteEntry> Routes =>
            _routes ?? Array.Empty<GASRuntimeStructuralPlaybackRouteEntry>();

        public int RouteCount => Routes.Count;

        public int RequiredStructuralPlaybackCount => CountRoutesRequiringStructuralPlayback();

        public int LocalPlaybackMigrationCount =>
            CountRoutesWithStatus(GASRuntimeStructuralPlaybackRouteStatus.LocalPlaybackMigration);

        public int BulkQueryCandidateCount =>
            CountRoutesWithPolicy(GASRuntimeStructuralPlaybackPolicy.EntityQueryBulkCandidate);

        public int CleanupPolicyCandidateCount =>
            CountRoutesWithPolicy(GASRuntimeStructuralPlaybackPolicy.CleanupComponentCandidate);

        public int DebuggerGateEvidenceCount =>
            CountRoutesWithEvidence(GASRuntimeStructuralPlaybackEvidence.DebuggerGateTag);

        public bool TryFind(
            GASRuntimeStructuralChangeEntryId structuralEntryId,
            out GASRuntimeStructuralPlaybackRouteEntry route)
        {
            var routes = Routes;
            for (var i = 0; i < routes.Count; i++)
            {
                if (routes[i].StructuralEntryId == structuralEntryId)
                {
                    route = routes[i];
                    return true;
                }
            }

            route = default;
            return false;
        }

        public int CountRoutesWithPolicy(GASRuntimeStructuralPlaybackPolicy policy)
        {
            var count = 0;
            var routes = Routes;
            for (var i = 0; i < routes.Count; i++)
            {
                if (routes[i].HasPolicy(policy))
                    count++;
            }

            return count;
        }

        public int CountRoutesWithStatus(GASRuntimeStructuralPlaybackRouteStatus status)
        {
            var count = 0;
            var routes = Routes;
            for (var i = 0; i < routes.Count; i++)
            {
                if (routes[i].Status == status)
                    count++;
            }

            return count;
        }

        public int CountRoutesWithEvidence(GASRuntimeStructuralPlaybackEvidence evidence)
        {
            var count = 0;
            var routes = Routes;
            for (var i = 0; i < routes.Count; i++)
            {
                if (routes[i].HasEvidence(evidence))
                    count++;
            }

            return count;
        }

        private int CountRoutesRequiringStructuralPlayback()
        {
            var count = 0;
            var routes = Routes;
            for (var i = 0; i < routes.Count; i++)
            {
                if (routes[i].RequiresStructuralPlayback)
                    count++;
            }

            return count;
        }
    }

    public static class GASRuntimeStructuralPlaybackGatePlanner
    {
        public static GASRuntimeStructuralPlaybackGatePlan CreateCurrent()
        {
            return CreateCurrent(GASRuntimeStructuralChangePlanner.CreateCurrent());
        }

        public static GASRuntimeStructuralPlaybackGatePlan CreateCurrent(
            GASRuntimeStructuralChangePlan structuralPlan)
        {
            var routes = new List<GASRuntimeStructuralPlaybackRouteEntry>();
            var entries = structuralPlan.Entries;
            for (var i = 0; i < entries.Count; i++)
                routes.Add(CreateRoute(entries[i]));

            return new GASRuntimeStructuralPlaybackGatePlan(CreateGate(), routes);
        }

        private static GASRuntimeStructuralPlaybackGateContract CreateGate()
        {
            return new GASRuntimeStructuralPlaybackGateContract(
                GASRuntimeStructuralPlaybackGateId.RuntimeCoreHotPath,
                typeof(GASStructuralCommitSystemGroup),
                typeof(EndGASStructuralCommitECBSystem),
                EGasRuntimeCoreFramePhase.StructuralPlayback,
                EGasRuntimeCoreStructuralPermission.PlaybackOnly,
                uniqueHotPathGate: true,
                contractOnly: true,
                GASRuntimeStructuralPlaybackEvidence.UniqueHotPathGate
                | GASRuntimeStructuralPlaybackEvidence.PlaybackOnlyGatePhase
                | GASRuntimeStructuralPlaybackEvidence.EcbPlaybackCount
                | GASRuntimeStructuralPlaybackEvidence.EcbCommandCount
                | GASRuntimeStructuralPlaybackEvidence.BulkQueryPolicy
                | GASRuntimeStructuralPlaybackEvidence.DebuggerGateTag);
        }

        private static GASRuntimeStructuralPlaybackRouteEntry CreateRoute(
            GASRuntimeStructuralChangeEntry entry)
        {
            var requiresPlayback = RequiresStructuralPlayback(entry);
            var status = ResolveStatus(entry, requiresPlayback);
            var recordPhase = ResolveRecordPhase(entry);
            var playbackPhase = requiresPlayback
                ? EGasRuntimeCoreFramePhase.StructuralPlayback
                : recordPhase;
            var gateId = requiresPlayback
                ? GASRuntimeStructuralPlaybackGateId.RuntimeCoreHotPath
                : GASRuntimeStructuralPlaybackGateId.None;
            var policy = ResolvePolicy(entry, requiresPlayback);
            var evidence = ResolveEvidence(policy, requiresPlayback, status);

            return new GASRuntimeStructuralPlaybackRouteEntry(
                entry.EntryId,
                entry.LayoutEntryId,
                recordPhase,
                playbackPhase,
                gateId,
                status,
                policy,
                evidence,
                requiresPlayback,
                directEntityManagerAllowed: false);
        }

        private static bool RequiresStructuralPlayback(GASRuntimeStructuralChangeEntry entry)
        {
            return entry.IsSimulationMigrationCandidate
                   && (entry.HasOperation(GASRuntimeStructuralOperation.CreateEntity)
                       || entry.HasOperation(GASRuntimeStructuralOperation.DestroyEntity)
                       || entry.HasOperation(GASRuntimeStructuralOperation.AddComponent)
                       || entry.HasOperation(GASRuntimeStructuralOperation.RemoveComponent)
                       || entry.HasOperation(GASRuntimeStructuralOperation.AddBuffer));
        }

        private static GASRuntimeStructuralPlaybackRouteStatus ResolveStatus(
            GASRuntimeStructuralChangeEntry entry,
            bool requiresPlayback)
        {
            if (entry.HasMigrationStep(GASRuntimeStructuralMigrationStep.ManagedBoundaryOnly))
                return GASRuntimeStructuralPlaybackRouteStatus.ManagedBoundaryNoPlayback;
            if (entry.HasBoundary(GASRuntimeStructuralBoundary.ObservationProjectionOnly))
                return GASRuntimeStructuralPlaybackRouteStatus.ObservationBoundaryNoPlayback;
            if (!requiresPlayback)
                return GASRuntimeStructuralPlaybackRouteStatus.DirtyPipelineNoPlayback;
            if (entry.HasMigrationStep(GASRuntimeStructuralMigrationStep.AlreadyEcb))
                return GASRuntimeStructuralPlaybackRouteStatus.StructuralCommitPlayback;

            return GASRuntimeStructuralPlaybackRouteStatus.LocalPlaybackMigration;
        }

        private static EGasRuntimeCoreFramePhase ResolveRecordPhase(
            GASRuntimeStructuralChangeEntry entry)
        {
            return entry.EntryId switch
            {
                GASRuntimeStructuralChangeEntryId.GameplayEffectActiveRuntimeMutation =>
                    EGasRuntimeCoreFramePhase.ActiveEffectLifecycle,
                GASRuntimeStructuralChangeEntryId.AbilityLifecycleCleanup =>
                    EGasRuntimeCoreFramePhase.ActiveEffectLifecycle,
                GASRuntimeStructuralChangeEntryId.AttributeDirtyRecalculate =>
                    EGasRuntimeCoreFramePhase.DeltaApply,
                GASRuntimeStructuralChangeEntryId.TagMaskDirtySync =>
                    EGasRuntimeCoreFramePhase.DeltaApply,
                GASRuntimeStructuralChangeEntryId.ObservationProjectionBoundary =>
                    EGasRuntimeCoreFramePhase.ObservationProjection,
                GASRuntimeStructuralChangeEntryId.ExecutionCalculationOutputMutation =>
                    EGasRuntimeCoreFramePhase.SpecEvaluation,
                GASRuntimeStructuralChangeEntryId.GameplayEffectApplyRequestConsumption =>
                    EGasRuntimeCoreFramePhase.SpecEvaluation,
                _ => EGasRuntimeCoreFramePhase.CommandIngest,
            };
        }

        private static GASRuntimeStructuralPlaybackPolicy ResolvePolicy(
            GASRuntimeStructuralChangeEntry entry,
            bool requiresPlayback)
        {
            if (!requiresPlayback)
            {
                if (entry.HasMigrationStep(GASRuntimeStructuralMigrationStep.ManagedBoundaryOnly))
                    return GASRuntimeStructuralPlaybackPolicy.ManagedBoundaryOnly;
                if (entry.HasBoundary(GASRuntimeStructuralBoundary.ObservationProjectionOnly))
                    return GASRuntimeStructuralPlaybackPolicy.ObservationNoPlayback;
                if (entry.HasEligibility(GASRuntimeStructuralEligibility.CanUseDirtyPipeline))
                    return GASRuntimeStructuralPlaybackPolicy.DirtyPipelineNoStructuralChange;

                return GASRuntimeStructuralPlaybackPolicy.None;
            }

            var policy = GASRuntimeStructuralPlaybackPolicy.EcbCommandBuffer;
            if (entry.HasOperation(GASRuntimeStructuralOperation.DestroyEntity)
                || entry.HasOperation(GASRuntimeStructuralOperation.AddComponent)
                || entry.HasOperation(GASRuntimeStructuralOperation.RemoveComponent))
            {
                policy |= GASRuntimeStructuralPlaybackPolicy.EntityQueryBulkCandidate;
            }

            if (entry.HasOperation(GASRuntimeStructuralOperation.AddComponent)
                || entry.HasOperation(GASRuntimeStructuralOperation.RemoveComponent)
                || entry.HasOperation(GASRuntimeStructuralOperation.AddBuffer))
            {
                policy |= GASRuntimeStructuralPlaybackPolicy.ComponentTypeSetBulkCandidate;
            }

            if (entry.HasBoundary(GASRuntimeStructuralBoundary.RuntimeEntityLifecycle)
                || entry.HasBoundary(GASRuntimeStructuralBoundary.GameplayEffectInstanceLifecycle)
                || entry.HasBoundary(GASRuntimeStructuralBoundary.AbilityLifecycleMarker))
            {
                policy |= GASRuntimeStructuralPlaybackPolicy.CleanupComponentCandidate;
            }

            if (entry.HasEligibility(GASRuntimeStructuralEligibility.CanUseEnableableTransientMarker))
                policy |= GASRuntimeStructuralPlaybackPolicy.EnableablePreferred;

            return policy;
        }

        private static GASRuntimeStructuralPlaybackEvidence ResolveEvidence(
            GASRuntimeStructuralPlaybackPolicy policy,
            bool requiresPlayback,
            GASRuntimeStructuralPlaybackRouteStatus status)
        {
            var evidence = GASRuntimeStructuralPlaybackEvidence.SourceSystemAttribution
                           | GASRuntimeStructuralPlaybackEvidence.HandleInvalidationBoundary;
            if (!requiresPlayback)
                return evidence;

            evidence |= GASRuntimeStructuralPlaybackEvidence.UniqueHotPathGate
                        | GASRuntimeStructuralPlaybackEvidence.RecordOnlySourcePhase
                        | GASRuntimeStructuralPlaybackEvidence.PlaybackOnlyGatePhase
                        | GASRuntimeStructuralPlaybackEvidence.EcbPlaybackCount
                        | GASRuntimeStructuralPlaybackEvidence.EcbCommandCount
                        | GASRuntimeStructuralPlaybackEvidence.DebuggerGateTag;
            if ((policy & GASRuntimeStructuralPlaybackPolicy.EntityQueryBulkCandidate) != 0
                || (policy & GASRuntimeStructuralPlaybackPolicy.ComponentTypeSetBulkCandidate) != 0)
            {
                evidence |= GASRuntimeStructuralPlaybackEvidence.BulkQueryPolicy;
            }

            if ((policy & GASRuntimeStructuralPlaybackPolicy.CleanupComponentCandidate) != 0)
                evidence |= GASRuntimeStructuralPlaybackEvidence.CleanupPolicy;
            if (status == GASRuntimeStructuralPlaybackRouteStatus.LocalPlaybackMigration)
            {
                evidence |= GASRuntimeStructuralPlaybackEvidence.LocalPlaybackMigration;
            }

            return evidence;
        }
    }
}
