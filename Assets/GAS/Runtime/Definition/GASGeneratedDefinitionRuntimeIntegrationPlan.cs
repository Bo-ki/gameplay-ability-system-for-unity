using System;
using System.Collections.Generic;

namespace GAS.Runtime
{
    public enum GASGeneratedDefinitionRuntimeIntegrationTarget
    {
        None = 0,
        UnityBakerInput = 1,
        StaticDefinitionBlobCache = 2,
        RuntimeStaticArchetype = 3,
        DeferredBoundary = 4,
    }

    public enum GASGeneratedDefinitionRuntimeIntegrationStatus
    {
        None = 0,
        Ready = 1,
        ReadyWithDeferredRuntimeBoundary = 2,
        Deferred = 3,
        Blocked = 4,
        ObservationOnly = 5,
        ManagedPresentationBoundary = 6,
    }

    [Flags]
    public enum GASGeneratedDefinitionRuntimeIntegrationBoundary
    {
        None = 0,
        BakePipelineNotReady = 1 << 0,
        MissingRuntimeLayoutEntry = 1 << 1,
        MissingStructuralChangeEntry = 1 << 2,
        RuntimeLifecycleDeferred = 1 << 3,
        ManagedPresentationDeferred = 1 << 5,
        GameplayEffectCacheLifecycleOwner = 1 << 6,
        MissingGameplayEffectConfigProvider = 1 << 7,
        StructuralSemanticDecision = 1 << 8,
        StructuralDirtyPipelineCandidate = 1 << 9,
        ObservationOnly = 1 << 10,
        ManagedPresentation = 1 << 11,
    }

    public readonly struct GASGeneratedDefinitionRuntimeIntegrationEntry
    {
        public readonly GASDefinitionKind DefinitionKind;
        public readonly int DefinitionCode;
        public readonly GASGeneratedDefinitionRuntimeIntegrationTarget Target;
        public readonly GASGeneratedDefinitionRuntimeIntegrationStatus Status;
        public readonly GASRuntimeQueryLayoutEntryId LayoutEntryId;
        public readonly GASRuntimeStructuralChangeEntryId StructuralEntryId;
        public readonly GASGeneratedDefinitionArchetypeTemplateKind TemplateKind;
        public readonly GASGeneratedDefinitionArchetypeSlot Slots;
        public readonly GASGeneratedDefinitionBakingBoundary DeferredBoundaries;
        public readonly GASGeneratedDefinitionRuntimeIntegrationBoundary IntegrationBoundaries;
        public readonly GASRuntimeLayoutCapability RuntimeLayoutCapabilities;
        public readonly GASRuntimeStructuralEligibility StructuralEligibility;
        public readonly GASRuntimeDirtyPipelineSignal DirtySignals;

        public GASGeneratedDefinitionRuntimeIntegrationEntry(
            GASDefinitionKind definitionKind,
            int definitionCode,
            GASGeneratedDefinitionRuntimeIntegrationTarget target,
            GASGeneratedDefinitionRuntimeIntegrationStatus status,
            GASRuntimeQueryLayoutEntryId layoutEntryId,
            GASRuntimeStructuralChangeEntryId structuralEntryId,
            GASGeneratedDefinitionArchetypeTemplateKind templateKind,
            GASGeneratedDefinitionArchetypeSlot slots,
            GASGeneratedDefinitionBakingBoundary deferredBoundaries,
            GASGeneratedDefinitionRuntimeIntegrationBoundary integrationBoundaries,
            GASRuntimeLayoutCapability runtimeLayoutCapabilities,
            GASRuntimeStructuralEligibility structuralEligibility,
            GASRuntimeDirtyPipelineSignal dirtySignals)
        {
            DefinitionKind = definitionKind;
            DefinitionCode = definitionCode;
            Target = target;
            Status = status;
            LayoutEntryId = layoutEntryId;
            StructuralEntryId = structuralEntryId;
            TemplateKind = templateKind;
            Slots = slots;
            DeferredBoundaries = deferredBoundaries;
            IntegrationBoundaries = integrationBoundaries;
            RuntimeLayoutCapabilities = runtimeLayoutCapabilities;
            StructuralEligibility = structuralEligibility;
            DirtySignals = dirtySignals;
        }

        public bool IsReady =>
            Status == GASGeneratedDefinitionRuntimeIntegrationStatus.Ready
            || Status == GASGeneratedDefinitionRuntimeIntegrationStatus.ReadyWithDeferredRuntimeBoundary;

        public bool IsBlocked => Status == GASGeneratedDefinitionRuntimeIntegrationStatus.Blocked;

        public bool IsRuntimeStaticArchetype =>
            Target == GASGeneratedDefinitionRuntimeIntegrationTarget.RuntimeStaticArchetype;

        public bool HasSlot(GASGeneratedDefinitionArchetypeSlot slot)
        {
            return slot != GASGeneratedDefinitionArchetypeSlot.None
                   && (Slots & slot) == slot;
        }

        public bool HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary boundary)
        {
            return boundary != GASGeneratedDefinitionBakingBoundary.None
                   && (DeferredBoundaries & boundary) == boundary;
        }

        public bool HasIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary boundary)
        {
            return boundary != GASGeneratedDefinitionRuntimeIntegrationBoundary.None
                   && (IntegrationBoundaries & boundary) == boundary;
        }
    }

    public readonly struct GASGeneratedDefinitionRuntimeIntegrationPlan
    {
        private readonly GASGeneratedDefinitionRuntimeIntegrationEntry[] _entries;

        public readonly GASGeneratedDefinitionBakeResult SourceBakeResult;
        public readonly GASRuntimeQueryLayoutPlan SourceLayoutPlan;
        public readonly GASRuntimeStructuralChangePlan SourceStructuralPlan;

        public GASGeneratedDefinitionRuntimeIntegrationPlan(
            GASGeneratedDefinitionBakeResult sourceBakeResult,
            GASRuntimeQueryLayoutPlan sourceLayoutPlan,
            GASRuntimeStructuralChangePlan sourceStructuralPlan,
            IEnumerable<GASGeneratedDefinitionRuntimeIntegrationEntry> entries)
        {
            SourceBakeResult = sourceBakeResult;
            SourceLayoutPlan = sourceLayoutPlan;
            SourceStructuralPlan = sourceStructuralPlan;
            _entries = entries is GASGeneratedDefinitionRuntimeIntegrationEntry[] array
                ? (GASGeneratedDefinitionRuntimeIntegrationEntry[])array.Clone()
                : new List<GASGeneratedDefinitionRuntimeIntegrationEntry>(
                    entries ?? Array.Empty<GASGeneratedDefinitionRuntimeIntegrationEntry>()).ToArray();
        }

        public static GASGeneratedDefinitionRuntimeIntegrationPlan Empty =>
            new(
                GASGeneratedDefinitionBakeResult.Empty,
                GASRuntimeQueryLayoutPlan.Empty,
                GASRuntimeStructuralChangePlan.Empty,
                Array.Empty<GASGeneratedDefinitionRuntimeIntegrationEntry>());

        public IReadOnlyList<GASGeneratedDefinitionRuntimeIntegrationEntry> Entries =>
            _entries ?? Array.Empty<GASGeneratedDefinitionRuntimeIntegrationEntry>();

        public int EntryCount => Entries.Count;
        public bool CanRunUnityBaker => SourceBakeResult.CanRunUnityBaker;
        public int UnityBakerInputEntryCount => CountEntriesByTarget(GASGeneratedDefinitionRuntimeIntegrationTarget.UnityBakerInput);
        public int StaticBlobCacheEntryCount => CountEntriesByTarget(GASGeneratedDefinitionRuntimeIntegrationTarget.StaticDefinitionBlobCache);
        public int RuntimeArchetypeEntryCount => CountEntriesByTarget(GASGeneratedDefinitionRuntimeIntegrationTarget.RuntimeStaticArchetype);
        public int DeferredBoundaryEntryCount => CountEntriesByTarget(GASGeneratedDefinitionRuntimeIntegrationTarget.DeferredBoundary);
        public int ReadyRuntimeArchetypeCount => CountRuntimeArchetypeEntries(readyOnly: true, blockedOnly: false);
        public int BlockedRuntimeArchetypeCount => CountRuntimeArchetypeEntries(readyOnly: false, blockedOnly: true);
        public int BlockedEntryCount => CountEntriesByStatus(GASGeneratedDefinitionRuntimeIntegrationStatus.Blocked);
        public int ManagedPresentationBoundaryCount =>
            CountEntriesWithIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary.ManagedPresentation);
        public int ObservationOnlyEntryCount =>
            CountEntriesWithIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary.ObservationOnly);
        public int StructuralSemanticDecisionCount =>
            CountEntriesWithIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary.StructuralSemanticDecision);
        public int DirtyPipelineCandidateCount =>
            CountEntriesWithIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary.StructuralDirtyPipelineCandidate);
        public bool HasRuntimeLifecycleDeferredBoundaries =>
            CountEntriesWithIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary.RuntimeLifecycleDeferred) > 0;
        public bool CanIntegrateRuntimeStaticArchetypes =>
            CanRunUnityBaker
            && RuntimeArchetypeEntryCount > 0
            && ReadyRuntimeArchetypeCount > 0
            && BlockedRuntimeArchetypeCount == 0;

        public bool TryFindEntry(
            GASGeneratedDefinitionRuntimeIntegrationTarget target,
            GASDefinitionKind definitionKind,
            int definitionCode,
            out GASGeneratedDefinitionRuntimeIntegrationEntry entry)
        {
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Target != target
                    || entries[i].DefinitionKind != definitionKind
                    || entries[i].DefinitionCode != definitionCode)
                    continue;

                entry = entries[i];
                return true;
            }

            entry = default;
            return false;
        }

        public int CountEntriesByTarget(GASGeneratedDefinitionRuntimeIntegrationTarget target)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Target == target)
                    count++;
            }

            return count;
        }

        public int CountEntriesByStatus(GASGeneratedDefinitionRuntimeIntegrationStatus status)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Status == status)
                    count++;
            }

            return count;
        }

        public int CountEntriesWithIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary boundary)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].HasIntegrationBoundary(boundary))
                    count++;
            }

            return count;
        }

        private int CountRuntimeArchetypeEntries(bool readyOnly, bool blockedOnly)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (!entries[i].IsRuntimeStaticArchetype)
                    continue;
                if (readyOnly && !entries[i].IsReady)
                    continue;
                if (blockedOnly && !entries[i].IsBlocked)
                    continue;
                count++;
            }

            return count;
        }
    }

    public static class GASGeneratedDefinitionRuntimeIntegrationPlanner
    {
        public static GASGeneratedDefinitionRuntimeIntegrationPlan Create(
            GASGeneratedDefinitionBakeResult bakeResult,
            GASRuntimeQueryLayoutPlan layoutPlan,
            GASRuntimeStructuralChangePlan structuralPlan)
        {
            var entries = new List<GASGeneratedDefinitionRuntimeIntegrationEntry>();

            AppendBakerInputs(bakeResult, layoutPlan, structuralPlan, entries);
            AppendStaticBlobCacheRequests(bakeResult, layoutPlan, structuralPlan, entries);
            AppendRuntimeArchetypes(bakeResult, layoutPlan, structuralPlan, entries);
            AppendDeferredBoundaries(bakeResult, layoutPlan, structuralPlan, entries);

            return new GASGeneratedDefinitionRuntimeIntegrationPlan(
                bakeResult,
                layoutPlan,
                structuralPlan,
                entries);
        }

        public static GASGeneratedDefinitionRuntimeIntegrationPlan CreateCurrent(
            GASGeneratedDefinitionBakeResult bakeResult)
        {
            var layoutPlan = GASRuntimeQueryLayoutPlanner.CreateCurrent();
            return Create(
                bakeResult,
                layoutPlan,
                GASRuntimeStructuralChangePlanner.CreateCurrent(layoutPlan));
        }

        private static void AppendBakerInputs(
            GASGeneratedDefinitionBakeResult bakeResult,
            GASRuntimeQueryLayoutPlan layoutPlan,
            GASRuntimeStructuralChangePlan structuralPlan,
            List<GASGeneratedDefinitionRuntimeIntegrationEntry> entries)
        {
            var artifacts = bakeResult.BakerInputs;
            for (var i = 0; i < artifacts.Count; i++)
            {
                var artifact = artifacts[i];
                entries.Add(CreateEntry(
                    bakeResult,
                    layoutPlan,
                    structuralPlan,
                    artifact.DefinitionKind,
                    artifact.DefinitionCode,
                    GASGeneratedDefinitionRuntimeIntegrationTarget.UnityBakerInput,
                    GASGeneratedDefinitionArchetypeTemplateKind.None,
                    GASGeneratedDefinitionArchetypeSlot.None,
                    GASGeneratedDefinitionBakingBoundary.None,
                    includeStructuralBoundaries: false));
            }
        }

        private static void AppendStaticBlobCacheRequests(
            GASGeneratedDefinitionBakeResult bakeResult,
            GASRuntimeQueryLayoutPlan layoutPlan,
            GASRuntimeStructuralChangePlan structuralPlan,
            List<GASGeneratedDefinitionRuntimeIntegrationEntry> entries)
        {
            var requests = bakeResult.StaticBlobCacheRequests;
            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                entries.Add(CreateEntry(
                    bakeResult,
                    layoutPlan,
                    structuralPlan,
                    request.DefinitionKind,
                    request.DefinitionCode,
                    GASGeneratedDefinitionRuntimeIntegrationTarget.StaticDefinitionBlobCache,
                    GASGeneratedDefinitionArchetypeTemplateKind.None,
                    GASGeneratedDefinitionArchetypeSlot.StaticDefinitionBlobReferenceSlot,
                    GASGeneratedDefinitionBakingBoundary.GameplayEffectCacheLifecycleOwner,
                    includeStructuralBoundaries: false));
            }
        }

        private static void AppendRuntimeArchetypes(
            GASGeneratedDefinitionBakeResult bakeResult,
            GASRuntimeQueryLayoutPlan layoutPlan,
            GASRuntimeStructuralChangePlan structuralPlan,
            List<GASGeneratedDefinitionRuntimeIntegrationEntry> entries)
        {
            var artifacts = bakeResult.RuntimeArchetypes;
            for (var i = 0; i < artifacts.Count; i++)
            {
                var artifact = artifacts[i];
                entries.Add(CreateEntry(
                    bakeResult,
                    layoutPlan,
                    structuralPlan,
                    artifact.DefinitionKind,
                    artifact.DefinitionCode,
                    GASGeneratedDefinitionRuntimeIntegrationTarget.RuntimeStaticArchetype,
                    artifact.TemplateKind,
                    artifact.Slots,
                    artifact.DeferredBoundaries,
                    includeStructuralBoundaries: true));
            }
        }

        private static void AppendDeferredBoundaries(
            GASGeneratedDefinitionBakeResult bakeResult,
            GASRuntimeQueryLayoutPlan layoutPlan,
            GASRuntimeStructuralChangePlan structuralPlan,
            List<GASGeneratedDefinitionRuntimeIntegrationEntry> entries)
        {
            var artifacts = bakeResult.DeferredBoundaries;
            for (var i = 0; i < artifacts.Count; i++)
            {
                var artifact = artifacts[i];
                entries.Add(CreateEntry(
                    bakeResult,
                    layoutPlan,
                    structuralPlan,
                    artifact.DefinitionKind,
                    artifact.DefinitionCode,
                    GASGeneratedDefinitionRuntimeIntegrationTarget.DeferredBoundary,
                    GASGeneratedDefinitionArchetypeTemplateKind.None,
                    GASGeneratedDefinitionArchetypeSlot.None,
                    artifact.Boundary,
                    includeStructuralBoundaries: false));
            }
        }

        private static GASGeneratedDefinitionRuntimeIntegrationEntry CreateEntry(
            GASGeneratedDefinitionBakeResult bakeResult,
            GASRuntimeQueryLayoutPlan layoutPlan,
            GASRuntimeStructuralChangePlan structuralPlan,
            GASDefinitionKind definitionKind,
            int definitionCode,
            GASGeneratedDefinitionRuntimeIntegrationTarget target,
            GASGeneratedDefinitionArchetypeTemplateKind templateKind,
            GASGeneratedDefinitionArchetypeSlot slots,
            GASGeneratedDefinitionBakingBoundary deferredBoundaries,
            bool includeStructuralBoundaries)
        {
            var layoutEntryId = ToLayoutEntryId(definitionKind, target);
            var integrationBoundaries = ToIntegrationBoundaries(deferredBoundaries);
            var layoutCapabilities = GASRuntimeLayoutCapability.None;
            var structuralEntryId = GASRuntimeStructuralChangeEntryId.None;
            var structuralEligibility = GASRuntimeStructuralEligibility.None;
            var dirtySignals = GASRuntimeDirtyPipelineSignal.None;

            if (!bakeResult.CanRunUnityBaker)
                integrationBoundaries |= GASGeneratedDefinitionRuntimeIntegrationBoundary.BakePipelineNotReady;

            if (layoutEntryId == GASRuntimeQueryLayoutEntryId.None
                || !layoutPlan.TryFind(layoutEntryId, out var layoutEntry))
            {
                integrationBoundaries |= GASGeneratedDefinitionRuntimeIntegrationBoundary.MissingRuntimeLayoutEntry;
            }
            else
            {
                layoutCapabilities = layoutEntry.Capabilities;
                if (layoutEntry.HasCapability(GASRuntimeLayoutCapability.ObservationOnly))
                    integrationBoundaries |= GASGeneratedDefinitionRuntimeIntegrationBoundary.ObservationOnly;
                if (layoutEntry.HasCapability(GASRuntimeLayoutCapability.ManagedPresentationBoundary))
                    integrationBoundaries |= GASGeneratedDefinitionRuntimeIntegrationBoundary.ManagedPresentation;

                if (includeStructuralBoundaries)
                {
                    if (structuralPlan.TryFindFirstByLayoutEntry(layoutEntryId, out var structuralEntry))
                    {
                        structuralEntryId = structuralEntry.EntryId;
                        structuralEligibility = structuralEntry.Eligibility;
                        dirtySignals = structuralEntry.DirtySignals;
                        if (structuralEntry.HasEligibility(GASRuntimeStructuralEligibility.RequiresSemanticDecision))
                            integrationBoundaries |= GASGeneratedDefinitionRuntimeIntegrationBoundary.StructuralSemanticDecision;
                        if (structuralEntry.HasEligibility(GASRuntimeStructuralEligibility.CanUseDirtyPipeline))
                            integrationBoundaries |= GASGeneratedDefinitionRuntimeIntegrationBoundary.StructuralDirtyPipelineCandidate;
                        if (structuralEntry.HasBoundary(GASRuntimeStructuralBoundary.ManagedPresentation))
                            integrationBoundaries |= GASGeneratedDefinitionRuntimeIntegrationBoundary.ManagedPresentation;
                    }
                    else
                    {
                        integrationBoundaries |= GASGeneratedDefinitionRuntimeIntegrationBoundary.MissingStructuralChangeEntry;
                    }
                }
            }

            return new GASGeneratedDefinitionRuntimeIntegrationEntry(
                definitionKind,
                definitionCode,
                target,
                ResolveStatus(target, integrationBoundaries),
                layoutEntryId,
                structuralEntryId,
                templateKind,
                slots,
                deferredBoundaries,
                integrationBoundaries,
                layoutCapabilities,
                structuralEligibility,
                dirtySignals);
        }

        private static GASRuntimeQueryLayoutEntryId ToLayoutEntryId(
            GASDefinitionKind definitionKind,
            GASGeneratedDefinitionRuntimeIntegrationTarget target)
        {
            switch (definitionKind)
            {
                case GASDefinitionKind.Ability:
                    return GASRuntimeQueryLayoutEntryId.AbilityTickLifecycle;
                case GASDefinitionKind.GameplayEffect:
                    return GASRuntimeQueryLayoutEntryId.GameplayEffectActiveRuntime;
                case GASDefinitionKind.AttributeSet:
                    return GASRuntimeQueryLayoutEntryId.AscStableState;
                case GASDefinitionKind.Attribute:
                    return GASRuntimeQueryLayoutEntryId.AttributeRecalculate;
                case GASDefinitionKind.GameplayTag:
                    return GASRuntimeQueryLayoutEntryId.TagMaskRuntime;
                case GASDefinitionKind.GameplayCue:
                    return GASRuntimeQueryLayoutEntryId.ObservationReplayAndOutbox;
                default:
                    return GASRuntimeQueryLayoutEntryId.None;
            }
        }

        private static GASGeneratedDefinitionRuntimeIntegrationBoundary ToIntegrationBoundaries(
            GASGeneratedDefinitionBakingBoundary deferredBoundaries)
        {
            var boundaries = GASGeneratedDefinitionRuntimeIntegrationBoundary.None;
            if ((deferredBoundaries & GASGeneratedDefinitionBakingBoundary.SourceHasErrors) != 0)
                boundaries |= GASGeneratedDefinitionRuntimeIntegrationBoundary.BakePipelineNotReady;
            if ((deferredBoundaries & GASGeneratedDefinitionBakingBoundary.RuntimeLifecycle) != 0)
                boundaries |= GASGeneratedDefinitionRuntimeIntegrationBoundary.RuntimeLifecycleDeferred;
            if ((deferredBoundaries & GASGeneratedDefinitionBakingBoundary.ManagedPresentation) != 0)
                boundaries |= GASGeneratedDefinitionRuntimeIntegrationBoundary.ManagedPresentationDeferred;
            if ((deferredBoundaries & GASGeneratedDefinitionBakingBoundary.GameplayEffectCacheLifecycleOwner) != 0)
                boundaries |= GASGeneratedDefinitionRuntimeIntegrationBoundary.GameplayEffectCacheLifecycleOwner;
            if ((deferredBoundaries & GASGeneratedDefinitionBakingBoundary.MissingGameplayEffectConfigProvider) != 0)
                boundaries |= GASGeneratedDefinitionRuntimeIntegrationBoundary.MissingGameplayEffectConfigProvider;

            return boundaries;
        }

        private static GASGeneratedDefinitionRuntimeIntegrationStatus ResolveStatus(
            GASGeneratedDefinitionRuntimeIntegrationTarget target,
            GASGeneratedDefinitionRuntimeIntegrationBoundary boundaries)
        {
            if ((boundaries & (
                    GASGeneratedDefinitionRuntimeIntegrationBoundary.BakePipelineNotReady
                    | GASGeneratedDefinitionRuntimeIntegrationBoundary.MissingRuntimeLayoutEntry
                    | GASGeneratedDefinitionRuntimeIntegrationBoundary.MissingStructuralChangeEntry
                    | GASGeneratedDefinitionRuntimeIntegrationBoundary.MissingGameplayEffectConfigProvider)) != 0)
            {
                return GASGeneratedDefinitionRuntimeIntegrationStatus.Blocked;
            }

            if ((boundaries & GASGeneratedDefinitionRuntimeIntegrationBoundary.ManagedPresentation) != 0)
                return GASGeneratedDefinitionRuntimeIntegrationStatus.ManagedPresentationBoundary;
            if ((boundaries & GASGeneratedDefinitionRuntimeIntegrationBoundary.ObservationOnly) != 0)
                return GASGeneratedDefinitionRuntimeIntegrationStatus.ObservationOnly;
            if (target == GASGeneratedDefinitionRuntimeIntegrationTarget.DeferredBoundary)
                return GASGeneratedDefinitionRuntimeIntegrationStatus.Deferred;
            if (boundaries != GASGeneratedDefinitionRuntimeIntegrationBoundary.None)
                return GASGeneratedDefinitionRuntimeIntegrationStatus.ReadyWithDeferredRuntimeBoundary;

            return GASGeneratedDefinitionRuntimeIntegrationStatus.Ready;
        }
    }
}
