using System;
using System.Collections.Generic;

namespace GAS.Runtime
{
    public enum EGasRuntimeFrameBudgetEntryId
    {
        None = 0,
        RuntimeCoreFramePrepare = 1,
        EffectCommandSpecStreamSingleton = 2,
        RuntimeFrameContextCurrentFrame = 3,
        RuntimeDebuggerCurrentFrame = 4,
        RuntimeDebuggerEntityCounterQueries = 5,
        RuntimeDebuggerActiveEffectStoreCounters = 6,
        RuntimeDebuggerPresentationFallback = 7,
        PresentationOutboxProjectionCurrentFrame = 8,
        DebugReplayLogProjectionCurrentFrame = 9,
        AttributeRecalculateModifierLookup = 10,
    }

    public enum EGasRuntimeFrameAllocatorOwner
    {
        None = 0,
        NoNativeAllocation = 1,
        TempMainThreadScratch = 2,
        WorldUpdateAllocator = 3,
        SystemGroupRewindableAllocator = 4,
        PersistentOwnerEntity = 5,
    }

    public enum EGasRuntimeFrameDependencyBudget
    {
        None = 0,
        ReadOnlyMainThread = 1,
        ScheduledJob = 2,
        ManualComplete = 3,
        SyncQueryMayWait = 4,
        DebuggerObservationOnly = 5,
    }

    [Flags]
    public enum EGasRuntimeFrameBudgetRisk
    {
        None = 0,
        FrameArenaOwner = 1 << 0,
        QueryContract = 1 << 1,
        HelperTempQueryRisk = 1 << 2,
        SyncQuery = 1 << 3,
        ToEntityArrayTemp = 1 << 4,
        LookupUpdate = 1 << 5,
        RandomLookup = 1 << 6,
        MainThreadOnly = 1 << 7,
        DebuggerObservation = 1 << 8,
        WorldUpdateAllocatorCandidate = 1 << 9,
        RewindableAllocatorCandidate = 1 << 10,
        DependencyWaitRisk = 1 << 11,
    }

    public readonly struct GASRuntimeFrameBudgetEntry
    {
        public GASRuntimeFrameBudgetEntry(
            EGasRuntimeFrameBudgetEntryId entryId,
            EGasRuntimeCoreFramePhase phase,
            GASRuntimeQueryLayoutEntryId layoutEntryId,
            int queryBudget,
            int filteredQueryBudget,
            int unfilteredQueryBudget,
            int lookupUpdateBudget,
            int randomLookupBudget,
            int syncQueryBudget,
            EGasRuntimeFrameAllocatorOwner allocatorOwner,
            EGasRuntimeFrameDependencyBudget dependencyBudget,
            EGasRuntimeFrameBudgetRisk risks)
        {
            EntryId = entryId;
            Phase = phase;
            LayoutEntryId = layoutEntryId;
            QueryBudget = queryBudget;
            FilteredQueryBudget = filteredQueryBudget;
            UnfilteredQueryBudget = unfilteredQueryBudget;
            LookupUpdateBudget = lookupUpdateBudget;
            RandomLookupBudget = randomLookupBudget;
            SyncQueryBudget = syncQueryBudget;
            AllocatorOwner = allocatorOwner;
            DependencyBudget = dependencyBudget;
            Risks = risks;
        }

        public EGasRuntimeFrameBudgetEntryId EntryId { get; }

        public EGasRuntimeCoreFramePhase Phase { get; }

        public GASRuntimeQueryLayoutEntryId LayoutEntryId { get; }

        public int QueryBudget { get; }

        public int FilteredQueryBudget { get; }

        public int UnfilteredQueryBudget { get; }

        public int LookupUpdateBudget { get; }

        public int RandomLookupBudget { get; }

        public int SyncQueryBudget { get; }

        public EGasRuntimeFrameAllocatorOwner AllocatorOwner { get; }

        public EGasRuntimeFrameDependencyBudget DependencyBudget { get; }

        public EGasRuntimeFrameBudgetRisk Risks { get; }

        public bool HasRisk(EGasRuntimeFrameBudgetRisk risk)
        {
            return risk != EGasRuntimeFrameBudgetRisk.None
                   && (Risks & risk) == risk;
        }
    }

    public readonly struct GASRuntimeFrameBudgetPlan
    {
        private readonly GASRuntimeFrameBudgetEntry[] _entries;

        public GASRuntimeFrameBudgetPlan(IEnumerable<GASRuntimeFrameBudgetEntry> entries)
        {
            _entries = entries is GASRuntimeFrameBudgetEntry[] array
                ? (GASRuntimeFrameBudgetEntry[])array.Clone()
                : new List<GASRuntimeFrameBudgetEntry>(
                    entries ?? Array.Empty<GASRuntimeFrameBudgetEntry>()).ToArray();
        }

        public static GASRuntimeFrameBudgetPlan Empty =>
            new(Array.Empty<GASRuntimeFrameBudgetEntry>());

        public IReadOnlyList<GASRuntimeFrameBudgetEntry> Entries =>
            _entries ?? Array.Empty<GASRuntimeFrameBudgetEntry>();

        public int EntryCount => Entries.Count;

        public int TotalQueryBudget => Sum(entry => entry.QueryBudget);

        public int TotalFilteredQueryBudget => Sum(entry => entry.FilteredQueryBudget);

        public int TotalUnfilteredQueryBudget => Sum(entry => entry.UnfilteredQueryBudget);

        public int TotalLookupUpdateBudget => Sum(entry => entry.LookupUpdateBudget);

        public int TotalRandomLookupBudget => Sum(entry => entry.RandomLookupBudget);

        public int TotalSyncQueryBudget => Sum(entry => entry.SyncQueryBudget);

        public int HelperTempQueryRiskCount =>
            CountEntriesWithRisk(EGasRuntimeFrameBudgetRisk.HelperTempQueryRisk);

        public int DependencyWaitRiskCount =>
            CountEntriesWithRisk(EGasRuntimeFrameBudgetRisk.DependencyWaitRisk);

        public int WorldUpdateAllocatorOwnerCount =>
            CountEntriesWithAllocatorOwner(EGasRuntimeFrameAllocatorOwner.WorldUpdateAllocator);

        public int RewindableAllocatorCandidateCount =>
            CountEntriesWithRisk(EGasRuntimeFrameBudgetRisk.RewindableAllocatorCandidate);

        public bool TryFind(
            EGasRuntimeFrameBudgetEntryId entryId,
            out GASRuntimeFrameBudgetEntry entry)
        {
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].EntryId == entryId)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public int CountEntriesForPhase(EGasRuntimeCoreFramePhase phase)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Phase == phase)
                    count++;
            }

            return count;
        }

        public int CountEntriesWithRisk(EGasRuntimeFrameBudgetRisk risk)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].HasRisk(risk))
                    count++;
            }

            return count;
        }

        public int CountEntriesWithAllocatorOwner(EGasRuntimeFrameAllocatorOwner allocatorOwner)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].AllocatorOwner == allocatorOwner)
                    count++;
            }

            return count;
        }

        public int CountEntriesWithDependencyBudget(EGasRuntimeFrameDependencyBudget dependencyBudget)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].DependencyBudget == dependencyBudget)
                    count++;
            }

            return count;
        }

        private int Sum(Func<GASRuntimeFrameBudgetEntry, int> selector)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
                count += selector(entries[i]);

            return count;
        }
    }

    public static class GASRuntimeFrameBudgetPlanner
    {
        public static GASRuntimeFrameBudgetPlan CreateCurrent()
        {
            return new GASRuntimeFrameBudgetPlan(new[]
            {
                Entry(
                    EGasRuntimeFrameBudgetEntryId.RuntimeCoreFramePrepare,
                    EGasRuntimeCoreFramePhase.FramePrepare,
                    GASRuntimeQueryLayoutEntryId.None,
                    queryBudget: 0,
                    filteredQueryBudget: 0,
                    unfilteredQueryBudget: 0,
                    lookupUpdateBudget: 0,
                    randomLookupBudget: 0,
                    syncQueryBudget: 0,
                    EGasRuntimeFrameAllocatorOwner.WorldUpdateAllocator,
                    EGasRuntimeFrameDependencyBudget.None,
                    EGasRuntimeFrameBudgetRisk.FrameArenaOwner
                    | EGasRuntimeFrameBudgetRisk.WorldUpdateAllocatorCandidate
                    | EGasRuntimeFrameBudgetRisk.RewindableAllocatorCandidate),
                Entry(
                    EGasRuntimeFrameBudgetEntryId.EffectCommandSpecStreamSingleton,
                    EGasRuntimeCoreFramePhase.FramePrepare,
                    GASRuntimeQueryLayoutEntryId.GameplayEffectCommandSpecStream,
                    queryBudget: 0,
                    filteredQueryBudget: 0,
                    unfilteredQueryBudget: 0,
                    lookupUpdateBudget: 0,
                    randomLookupBudget: 0,
                    syncQueryBudget: 0,
                    EGasRuntimeFrameAllocatorOwner.NoNativeAllocation,
                    EGasRuntimeFrameDependencyBudget.ReadOnlyMainThread,
                    EGasRuntimeFrameBudgetRisk.MainThreadOnly
                    | EGasRuntimeFrameBudgetRisk.DependencyWaitRisk),
                Entry(
                    EGasRuntimeFrameBudgetEntryId.RuntimeFrameContextCurrentFrame,
                    EGasRuntimeCoreFramePhase.FramePrepare,
                    GASRuntimeQueryLayoutEntryId.None,
                    queryBudget: 0,
                    filteredQueryBudget: 0,
                    unfilteredQueryBudget: 0,
                    lookupUpdateBudget: 0,
                    randomLookupBudget: 0,
                    syncQueryBudget: 0,
                    EGasRuntimeFrameAllocatorOwner.NoNativeAllocation,
                    EGasRuntimeFrameDependencyBudget.ReadOnlyMainThread,
                    EGasRuntimeFrameBudgetRisk.MainThreadOnly
                    | EGasRuntimeFrameBudgetRisk.DependencyWaitRisk),
                Entry(
                    EGasRuntimeFrameBudgetEntryId.RuntimeDebuggerEntityCounterQueries,
                    EGasRuntimeCoreFramePhase.ObservationProjection,
                    GASRuntimeQueryLayoutEntryId.ObservationReplayAndOutbox,
                    queryBudget: 12,
                    filteredQueryBudget: 0,
                    unfilteredQueryBudget: 12,
                    lookupUpdateBudget: 0,
                    randomLookupBudget: 0,
                    syncQueryBudget: 12,
                    EGasRuntimeFrameAllocatorOwner.NoNativeAllocation,
                    EGasRuntimeFrameDependencyBudget.DebuggerObservationOnly,
                    EGasRuntimeFrameBudgetRisk.QueryContract
                    | EGasRuntimeFrameBudgetRisk.SyncQuery
                    | EGasRuntimeFrameBudgetRisk.MainThreadOnly
                    | EGasRuntimeFrameBudgetRisk.DebuggerObservation
                    | EGasRuntimeFrameBudgetRisk.DependencyWaitRisk),
                Entry(
                    EGasRuntimeFrameBudgetEntryId.RuntimeDebuggerActiveEffectStoreCounters,
                    EGasRuntimeCoreFramePhase.ObservationProjection,
                    GASRuntimeQueryLayoutEntryId.ActiveEffectStore,
                    queryBudget: 1,
                    filteredQueryBudget: 0,
                    unfilteredQueryBudget: 1,
                    lookupUpdateBudget: 0,
                    randomLookupBudget: 0,
                    syncQueryBudget: 1,
                    EGasRuntimeFrameAllocatorOwner.TempMainThreadScratch,
                    EGasRuntimeFrameDependencyBudget.DebuggerObservationOnly,
                    EGasRuntimeFrameBudgetRisk.QueryContract
                    | EGasRuntimeFrameBudgetRisk.SyncQuery
                    | EGasRuntimeFrameBudgetRisk.ToEntityArrayTemp
                    | EGasRuntimeFrameBudgetRisk.MainThreadOnly
                    | EGasRuntimeFrameBudgetRisk.DebuggerObservation
                    | EGasRuntimeFrameBudgetRisk.DependencyWaitRisk),
                Entry(
                    EGasRuntimeFrameBudgetEntryId.RuntimeDebuggerPresentationFallback,
                    EGasRuntimeCoreFramePhase.ObservationProjection,
                    GASRuntimeQueryLayoutEntryId.ManagedCuePresentation,
                    queryBudget: 1,
                    filteredQueryBudget: 0,
                    unfilteredQueryBudget: 1,
                    lookupUpdateBudget: 0,
                    randomLookupBudget: 0,
                    syncQueryBudget: 1,
                    EGasRuntimeFrameAllocatorOwner.TempMainThreadScratch,
                    EGasRuntimeFrameDependencyBudget.DebuggerObservationOnly,
                    EGasRuntimeFrameBudgetRisk.QueryContract
                    | EGasRuntimeFrameBudgetRisk.SyncQuery
                    | EGasRuntimeFrameBudgetRisk.ToEntityArrayTemp
                    | EGasRuntimeFrameBudgetRisk.MainThreadOnly
                    | EGasRuntimeFrameBudgetRisk.DebuggerObservation
                    | EGasRuntimeFrameBudgetRisk.DependencyWaitRisk),
                Entry(
                    EGasRuntimeFrameBudgetEntryId.AttributeRecalculateModifierLookup,
                    EGasRuntimeCoreFramePhase.DeltaApply,
                    GASRuntimeQueryLayoutEntryId.AttributeRecalculate,
                    queryBudget: 0,
                    filteredQueryBudget: 0,
                    unfilteredQueryBudget: 0,
                    lookupUpdateBudget: 1,
                    randomLookupBudget: 1,
                    syncQueryBudget: 0,
                    EGasRuntimeFrameAllocatorOwner.NoNativeAllocation,
                    EGasRuntimeFrameDependencyBudget.ScheduledJob,
                    EGasRuntimeFrameBudgetRisk.LookupUpdate
                    | EGasRuntimeFrameBudgetRisk.RandomLookup),
            });
        }

        private static GASRuntimeFrameBudgetEntry Entry(
            EGasRuntimeFrameBudgetEntryId entryId,
            EGasRuntimeCoreFramePhase phase,
            GASRuntimeQueryLayoutEntryId layoutEntryId,
            int queryBudget,
            int filteredQueryBudget,
            int unfilteredQueryBudget,
            int lookupUpdateBudget,
            int randomLookupBudget,
            int syncQueryBudget,
            EGasRuntimeFrameAllocatorOwner allocatorOwner,
            EGasRuntimeFrameDependencyBudget dependencyBudget,
            EGasRuntimeFrameBudgetRisk risks)
        {
            return new GASRuntimeFrameBudgetEntry(
                entryId,
                phase,
                layoutEntryId,
                queryBudget,
                filteredQueryBudget,
                unfilteredQueryBudget,
                lookupUpdateBudget,
                randomLookupBudget,
                syncQueryBudget,
                allocatorOwner,
                dependencyBudget,
                risks);
        }
    }
}
