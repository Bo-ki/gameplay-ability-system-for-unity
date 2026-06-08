using System;
using System.Collections.Generic;

namespace GAS.Runtime
{
    public enum EGasRuntimeFrameStreamId
    {
        None = 0,
        EffectCommand = 1,
        EffectCommandSetByCaller = 2,
        InstantEffectSpec = 3,
        ActiveEffectMutation = 4,
        AttributeDelta = 5,
        TypedSimulationFact = 6,
        ActiveEffectNextFrameMutation = 7,
        OwnerLocalInstantNextFrame = 8,
    }

    public enum EGasRuntimeFrameStreamAuthority
    {
        None = 0,
        GameplayDeterministic = 1,
        GameplayAuxiliaryDeterministic = 2,
        ObservationOnly = 3,
        DebugTelemetry = 4,
    }

    public enum EGasRuntimeFrameStreamCarrier
    {
        None = 0,
        SingletonDynamicBuffer = 1,
        CommandRangeAuxiliaryBuffer = 2,
        OwnerLocalDynamicBuffer = 3,
        PerThreadNativeStream = 4,
        EcbAppendToBuffer = 5,
        SampledDebuggerBuffer = 6,
        BoundaryObservationBuffer = 7,
    }

    public enum EGasRuntimeFrameStreamScaleStatus
    {
        None = 0,
        ProofOnly = 1,
        MigrationCarrier = 2,
        ScaleReady = 3,
    }

    public enum EGasRuntimeFrameStreamMergePolicy
    {
        None = 0,
        SingleWriterSequenceAppend = 1,
        StableSortByCommandSequence = 2,
        StableSortByTargetThenSequence = 3,
        StableSortByTargetAttributeThenSequence = 4,
        ExcludedFromGameplayMerge = 5,
    }

    public enum EGasRuntimeFrameStreamSortKey
    {
        None = 0,
        CommandSequence = 1,
        SpecSequence = 2,
        DeltaSequence = 3,
        TargetAscThenCommandSequence = 4,
        TargetAscThenSpecSequence = 5,
        TargetAscThenDeltaSequence = 6,
        TargetAscThenFactSequence = 7,
        TargetAscThenAttributeThenDeltaSequence = 8,
    }

    public enum EGasRuntimeSequenceKind
    {
        None = 0,
        ContextId = 1,
        CommandSequence = 2,
        SpecSequence = 3,
        DeltaSequence = 4,
        FactSequence = 5,
    }

    public enum EGasRuntimeSequenceOwnerKind
    {
        None = 0,
        EffectCommandStreamSingleton = 1,
        AttributeDeltaOwnerLocalStream = 2,
        TypedSimulationFactOwnerLocalStream = 3,
    }

    public readonly struct GASRuntimeSequenceOwnerEntry
    {
        public readonly EGasRuntimeSequenceKind SequenceKind;
        public readonly EGasRuntimeSequenceOwnerKind OwnerKind;
        public readonly EGasRuntimeFrameStreamId StreamId;
        public readonly EGasRuntimeFrameStreamSortKey SortKey;
        public readonly bool AffectsBattleHash;

        public GASRuntimeSequenceOwnerEntry(
            EGasRuntimeSequenceKind sequenceKind,
            EGasRuntimeSequenceOwnerKind ownerKind,
            EGasRuntimeFrameStreamId streamId,
            EGasRuntimeFrameStreamSortKey sortKey,
            bool affectsBattleHash)
        {
            SequenceKind = sequenceKind;
            OwnerKind = ownerKind;
            StreamId = streamId;
            SortKey = sortKey;
            AffectsBattleHash = affectsBattleHash;
        }
    }

    public static class GASRuntimeSequenceOwnerContract
    {
        public static GASRuntimeSequenceOwnerEntry Resolve(EGasRuntimeSequenceKind sequenceKind)
        {
            switch (sequenceKind)
            {
                case EGasRuntimeSequenceKind.ContextId:
                    return new GASRuntimeSequenceOwnerEntry(
                        sequenceKind,
                        EGasRuntimeSequenceOwnerKind.EffectCommandStreamSingleton,
                        EGasRuntimeFrameStreamId.EffectCommand,
                        EGasRuntimeFrameStreamSortKey.CommandSequence,
                        affectsBattleHash: true);
                case EGasRuntimeSequenceKind.CommandSequence:
                    return new GASRuntimeSequenceOwnerEntry(
                        sequenceKind,
                        EGasRuntimeSequenceOwnerKind.EffectCommandStreamSingleton,
                        EGasRuntimeFrameStreamId.EffectCommand,
                        EGasRuntimeFrameStreamSortKey.CommandSequence,
                        affectsBattleHash: true);
                case EGasRuntimeSequenceKind.SpecSequence:
                    return new GASRuntimeSequenceOwnerEntry(
                        sequenceKind,
                        EGasRuntimeSequenceOwnerKind.EffectCommandStreamSingleton,
                        EGasRuntimeFrameStreamId.InstantEffectSpec,
                        EGasRuntimeFrameStreamSortKey.TargetAscThenSpecSequence,
                        affectsBattleHash: true);
                case EGasRuntimeSequenceKind.DeltaSequence:
                    return new GASRuntimeSequenceOwnerEntry(
                        sequenceKind,
                        EGasRuntimeSequenceOwnerKind.AttributeDeltaOwnerLocalStream,
                        EGasRuntimeFrameStreamId.AttributeDelta,
                        EGasRuntimeFrameStreamSortKey.TargetAscThenAttributeThenDeltaSequence,
                        affectsBattleHash: true);
                case EGasRuntimeSequenceKind.FactSequence:
                    return new GASRuntimeSequenceOwnerEntry(
                        sequenceKind,
                        EGasRuntimeSequenceOwnerKind.TypedSimulationFactOwnerLocalStream,
                        EGasRuntimeFrameStreamId.TypedSimulationFact,
                        EGasRuntimeFrameStreamSortKey.TargetAscThenFactSequence,
                        affectsBattleHash: true);
                default:
                    return default;
            }
        }
    }

    public static class GASRuntimeSequenceAllocator
    {
        public static int AllocateContextId(ref GEEffectCommandStreamComponent stream)
        {
            return Allocate(ref stream.NextContextId);
        }

        public static int AllocateCommandSequence(ref GEEffectCommandStreamComponent stream)
        {
            return Allocate(ref stream.NextCommandSequence);
        }

        public static int AllocateSpecSequence(ref GEEffectCommandStreamComponent stream)
        {
            return Allocate(ref stream.NextSpecSequence);
        }

        public static int AllocateDeltaSequence(ref GEEffectCommandStreamComponent stream)
        {
            return Allocate(ref stream.NextDeltaSequence);
        }

        public static int AllocateFactSequence(ref GEEffectCommandStreamComponent stream)
        {
            return Allocate(ref stream.NextFactSequence);
        }

        public static int Allocate(
            ref GEEffectCommandStreamComponent stream,
            EGasRuntimeSequenceKind sequenceKind)
        {
            switch (sequenceKind)
            {
                case EGasRuntimeSequenceKind.ContextId:
                    return AllocateContextId(ref stream);
                case EGasRuntimeSequenceKind.CommandSequence:
                    return AllocateCommandSequence(ref stream);
                case EGasRuntimeSequenceKind.SpecSequence:
                    return AllocateSpecSequence(ref stream);
                case EGasRuntimeSequenceKind.DeltaSequence:
                    return AllocateDeltaSequence(ref stream);
                case EGasRuntimeSequenceKind.FactSequence:
                    return AllocateFactSequence(ref stream);
                default:
                    return 0;
            }
        }

        private static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;

            return next++;
        }
    }

    [Flags]
    public enum EGasRuntimeFrameStreamReselectTrigger
    {
        None = 0,
        X50BufferPressure = 1 << 0,
        X1000ScaleGate = 1 << 1,
        BufferExternalized = 1 << 2,
        ParallelProducerIntroduced = 1 << 3,
        MergeCostDominatesSpecEvaluation = 1 << 4,
        BattleHashInstability = 1 << 5,
        RandomLookupDominatesGroupedApply = 1 << 6,
    }

    [Flags]
    public enum EGasRuntimeFrameStreamEvidence
    {
        None = 0,
        BufferPressure = 1 << 0,
        ClearPhase = 1 << 1,
        WritePhase = 1 << 2,
        ReadPhase = 1 << 3,
        DeterministicSortKey = 1 << 4,
        BattleHashInput = 1 << 5,
        NativeStreamCandidate = 1 << 6,
        PerOwnerBufferCandidate = 1 << 7,
        ProofOrMigrationMarker = 1 << 8,
        OwnerRangeCounters = 1 << 9,
        RandomLookupCounters = 1 << 10,
    }

    public readonly struct GASRuntimeFrameStreamOwnerEntry
    {
        public GASRuntimeFrameStreamOwnerEntry(
            EGasRuntimeFrameStreamId streamId,
            GASRuntimeQueryLayoutEntryId layoutEntryId,
            GASRuntimeLayoutComponentSlot componentSlot,
            EGasRuntimeCoreFramePhase clearPhase,
            EGasRuntimeCoreFramePhase writePhase,
            EGasRuntimeCoreFramePhase readPhase,
            EGasRuntimeCoreFramePhase mergePhase,
            EGasRuntimeFrameStreamAuthority authority,
            EGasRuntimeFrameStreamCarrier currentCarrier,
            EGasRuntimeFrameStreamCarrier targetCarrier,
            EGasRuntimeFrameStreamScaleStatus scaleStatus,
            EGasRuntimeFrameStreamMergePolicy mergePolicy,
            EGasRuntimeFrameStreamSortKey sortKey,
            int internalBufferCapacity,
            bool affectsBattleHash,
            EGasRuntimeFrameStreamReselectTrigger reselectTriggers,
            EGasRuntimeFrameStreamEvidence evidence)
        {
            StreamId = streamId;
            LayoutEntryId = layoutEntryId;
            ComponentSlot = componentSlot;
            ClearPhase = clearPhase;
            WritePhase = writePhase;
            ReadPhase = readPhase;
            MergePhase = mergePhase;
            Authority = authority;
            CurrentCarrier = currentCarrier;
            TargetCarrier = targetCarrier;
            ScaleStatus = scaleStatus;
            MergePolicy = mergePolicy;
            SortKey = sortKey;
            InternalBufferCapacity = internalBufferCapacity;
            AffectsBattleHash = affectsBattleHash;
            ReselectTriggers = reselectTriggers;
            Evidence = evidence;
        }

        public EGasRuntimeFrameStreamId StreamId { get; }

        public GASRuntimeQueryLayoutEntryId LayoutEntryId { get; }

        public GASRuntimeLayoutComponentSlot ComponentSlot { get; }

        public EGasRuntimeCoreFramePhase ClearPhase { get; }

        public EGasRuntimeCoreFramePhase WritePhase { get; }

        public EGasRuntimeCoreFramePhase ReadPhase { get; }

        public EGasRuntimeCoreFramePhase MergePhase { get; }

        public EGasRuntimeFrameStreamAuthority Authority { get; }

        public EGasRuntimeFrameStreamCarrier CurrentCarrier { get; }

        public EGasRuntimeFrameStreamCarrier TargetCarrier { get; }

        public EGasRuntimeFrameStreamScaleStatus ScaleStatus { get; }

        public EGasRuntimeFrameStreamMergePolicy MergePolicy { get; }

        public EGasRuntimeFrameStreamSortKey SortKey { get; }

        public int InternalBufferCapacity { get; }

        public bool AffectsBattleHash { get; }

        public EGasRuntimeFrameStreamReselectTrigger ReselectTriggers { get; }

        public EGasRuntimeFrameStreamEvidence Evidence { get; }

        public bool HasReselectTrigger(EGasRuntimeFrameStreamReselectTrigger trigger)
        {
            return trigger != EGasRuntimeFrameStreamReselectTrigger.None
                   && (ReselectTriggers & trigger) == trigger;
        }

        public bool HasEvidence(EGasRuntimeFrameStreamEvidence evidence)
        {
            return evidence != EGasRuntimeFrameStreamEvidence.None
                   && (Evidence & evidence) == evidence;
        }
    }

    public readonly struct GASRuntimeFrameStreamOwnerPlan
    {
        private readonly GASRuntimeFrameStreamOwnerEntry[] _entries;

        public GASRuntimeFrameStreamOwnerPlan(IEnumerable<GASRuntimeFrameStreamOwnerEntry> entries)
        {
            _entries = entries is GASRuntimeFrameStreamOwnerEntry[] array
                ? (GASRuntimeFrameStreamOwnerEntry[])array.Clone()
                : new List<GASRuntimeFrameStreamOwnerEntry>(
                    entries ?? Array.Empty<GASRuntimeFrameStreamOwnerEntry>()).ToArray();
        }

        public static GASRuntimeFrameStreamOwnerPlan Empty =>
            new(Array.Empty<GASRuntimeFrameStreamOwnerEntry>());

        public IReadOnlyList<GASRuntimeFrameStreamOwnerEntry> Entries =>
            _entries ?? Array.Empty<GASRuntimeFrameStreamOwnerEntry>();

        public int EntryCount => Entries.Count;

        public int SingletonDynamicBufferCount =>
            CountEntriesWithCurrentCarrier(EGasRuntimeFrameStreamCarrier.SingletonDynamicBuffer);

        public int MigrationCarrierCount =>
            CountEntriesWithScaleStatus(EGasRuntimeFrameStreamScaleStatus.MigrationCarrier);

        public int ScaleReadyCount =>
            CountEntriesWithScaleStatus(EGasRuntimeFrameStreamScaleStatus.ScaleReady);

        public int NativeStreamCandidateCount =>
            CountEntriesWithTargetCarrier(EGasRuntimeFrameStreamCarrier.PerThreadNativeStream);

        public int OwnerLocalBufferCandidateCount =>
            CountEntriesWithTargetCarrier(EGasRuntimeFrameStreamCarrier.OwnerLocalDynamicBuffer);

        public int BattleHashStreamCount
        {
            get
            {
                var count = 0;
                var entries = Entries;
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i].AffectsBattleHash)
                        count++;
                }

                return count;
            }
        }

        public bool TryFind(
            EGasRuntimeFrameStreamId streamId,
            out GASRuntimeFrameStreamOwnerEntry entry)
        {
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].StreamId == streamId)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public int CountEntriesWithWritePhase(EGasRuntimeCoreFramePhase phase)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].WritePhase == phase)
                    count++;
            }

            return count;
        }

        public int CountEntriesWithCurrentCarrier(EGasRuntimeFrameStreamCarrier carrier)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].CurrentCarrier == carrier)
                    count++;
            }

            return count;
        }

        public int CountEntriesWithTargetCarrier(EGasRuntimeFrameStreamCarrier carrier)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].TargetCarrier == carrier)
                    count++;
            }

            return count;
        }

        public int CountEntriesWithScaleStatus(EGasRuntimeFrameStreamScaleStatus scaleStatus)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].ScaleStatus == scaleStatus)
                    count++;
            }

            return count;
        }
    }

    public static class GASRuntimeFrameStreamOwnerPlanner
    {
        public static GASRuntimeFrameStreamOwnerPlan CreateCurrent()
        {
            return new GASRuntimeFrameStreamOwnerPlan(new[]
            {
                Entry(
                    EGasRuntimeFrameStreamId.EffectCommand,
                    GASRuntimeLayoutComponentSlot.EffectCommandBuffer,
                    EGasRuntimeCoreFramePhase.FramePrepare,
                    EGasRuntimeCoreFramePhase.CommandIngest,
                    EGasRuntimeCoreFramePhase.SpecEvaluation,
                    EGasRuntimeCoreFramePhase.CommandIngest,
                    EGasRuntimeFrameStreamAuthority.GameplayDeterministic,
                    EGasRuntimeFrameStreamCarrier.SingletonDynamicBuffer,
                    EGasRuntimeFrameStreamCarrier.PerThreadNativeStream,
                    EGasRuntimeFrameStreamMergePolicy.StableSortByTargetThenSequence,
                    EGasRuntimeFrameStreamSortKey.TargetAscThenCommandSequence,
                    internalBufferCapacity: 64),
                Entry(
                    EGasRuntimeFrameStreamId.EffectCommandSetByCaller,
                    GASRuntimeLayoutComponentSlot.EffectCommandSetByCallerBuffer,
                    EGasRuntimeCoreFramePhase.FramePrepare,
                    EGasRuntimeCoreFramePhase.CommandIngest,
                    EGasRuntimeCoreFramePhase.SpecEvaluation,
                    EGasRuntimeCoreFramePhase.SpecEvaluation,
                    EGasRuntimeFrameStreamAuthority.GameplayAuxiliaryDeterministic,
                    EGasRuntimeFrameStreamCarrier.SingletonDynamicBuffer,
                    EGasRuntimeFrameStreamCarrier.CommandRangeAuxiliaryBuffer,
                    EGasRuntimeFrameStreamMergePolicy.StableSortByCommandSequence,
                    EGasRuntimeFrameStreamSortKey.CommandSequence,
                    internalBufferCapacity: 16),
                Entry(
                    EGasRuntimeFrameStreamId.InstantEffectSpec,
                    GASRuntimeLayoutComponentSlot.InstantEffectSpecBuffer,
                    EGasRuntimeCoreFramePhase.FramePrepare,
                    EGasRuntimeCoreFramePhase.SpecEvaluation,
                    EGasRuntimeCoreFramePhase.DeltaApply,
                    EGasRuntimeCoreFramePhase.SpecEvaluation,
                    EGasRuntimeFrameStreamAuthority.GameplayDeterministic,
                    EGasRuntimeFrameStreamCarrier.OwnerLocalDynamicBuffer,
                    EGasRuntimeFrameStreamCarrier.OwnerLocalDynamicBuffer,
                    EGasRuntimeFrameStreamMergePolicy.StableSortByTargetThenSequence,
                    EGasRuntimeFrameStreamSortKey.TargetAscThenSpecSequence,
                    internalBufferCapacity: 64),
                Entry(
                    EGasRuntimeFrameStreamId.ActiveEffectMutation,
                    GASRuntimeLayoutComponentSlot.ActiveEffectMutationBuffer,
                    EGasRuntimeCoreFramePhase.FramePrepare,
                    EGasRuntimeCoreFramePhase.ActiveEffectLifecycle,
                    EGasRuntimeCoreFramePhase.DeltaApply,
                    EGasRuntimeCoreFramePhase.ActiveEffectLifecycle,
                    EGasRuntimeFrameStreamAuthority.GameplayDeterministic,
                    EGasRuntimeFrameStreamCarrier.OwnerLocalDynamicBuffer,
                    EGasRuntimeFrameStreamCarrier.OwnerLocalDynamicBuffer,
                    EGasRuntimeFrameStreamMergePolicy.StableSortByTargetThenSequence,
                    EGasRuntimeFrameStreamSortKey.TargetAscThenCommandSequence,
                    internalBufferCapacity: 32),
                Entry(
                    EGasRuntimeFrameStreamId.ActiveEffectNextFrameMutation,
                    GASRuntimeLayoutComponentSlot.ActiveEffectNextFrameMutationCommandBuffer,
                    EGasRuntimeCoreFramePhase.FramePrepare,
                    EGasRuntimeCoreFramePhase.ActiveEffectLifecycle,
                    EGasRuntimeCoreFramePhase.FramePrepare,
                    EGasRuntimeCoreFramePhase.FramePrepare,
                    EGasRuntimeFrameStreamAuthority.GameplayDeterministic,
                    EGasRuntimeFrameStreamCarrier.OwnerLocalDynamicBuffer,
                    EGasRuntimeFrameStreamCarrier.OwnerLocalDynamicBuffer,
                    EGasRuntimeFrameStreamMergePolicy.StableSortByTargetThenSequence,
                    EGasRuntimeFrameStreamSortKey.TargetAscThenCommandSequence,
                    internalBufferCapacity: 4),
                Entry(
                    EGasRuntimeFrameStreamId.OwnerLocalInstantNextFrame,
                    GASRuntimeLayoutComponentSlot.OwnerLocalInstantNextFrameCommandBuffer,
                    EGasRuntimeCoreFramePhase.FramePrepare,
                    EGasRuntimeCoreFramePhase.ActiveEffectLifecycle,
                    EGasRuntimeCoreFramePhase.FramePrepare,
                    EGasRuntimeCoreFramePhase.FramePrepare,
                    EGasRuntimeFrameStreamAuthority.GameplayDeterministic,
                    EGasRuntimeFrameStreamCarrier.OwnerLocalDynamicBuffer,
                    EGasRuntimeFrameStreamCarrier.OwnerLocalDynamicBuffer,
                    EGasRuntimeFrameStreamMergePolicy.StableSortByTargetThenSequence,
                    EGasRuntimeFrameStreamSortKey.TargetAscThenCommandSequence,
                    internalBufferCapacity: 4),
                Entry(
                    EGasRuntimeFrameStreamId.AttributeDelta,
                    GASRuntimeLayoutComponentSlot.AttributeDeltaBuffer,
                    EGasRuntimeCoreFramePhase.FramePrepare,
                    EGasRuntimeCoreFramePhase.DeltaApply,
                    EGasRuntimeCoreFramePhase.TypedFactProjection,
                    EGasRuntimeCoreFramePhase.DeltaApply,
                    EGasRuntimeFrameStreamAuthority.GameplayDeterministic,
                    EGasRuntimeFrameStreamCarrier.OwnerLocalDynamicBuffer,
                    EGasRuntimeFrameStreamCarrier.OwnerLocalDynamicBuffer,
                    EGasRuntimeFrameStreamMergePolicy.StableSortByTargetAttributeThenSequence,
                    EGasRuntimeFrameStreamSortKey.TargetAscThenAttributeThenDeltaSequence,
                    internalBufferCapacity: 128),
                Entry(
                    EGasRuntimeFrameStreamId.TypedSimulationFact,
                    GASRuntimeLayoutComponentSlot.BoundaryObservationFactBuffer,
                    EGasRuntimeCoreFramePhase.FramePrepare,
                    EGasRuntimeCoreFramePhase.ObservationProjection,
                    EGasRuntimeCoreFramePhase.ObservationProjection,
                    EGasRuntimeCoreFramePhase.TypedFactProjection,
                    EGasRuntimeFrameStreamAuthority.GameplayDeterministic,
                    EGasRuntimeFrameStreamCarrier.OwnerLocalDynamicBuffer,
                    EGasRuntimeFrameStreamCarrier.BoundaryObservationBuffer,
                    EGasRuntimeFrameStreamMergePolicy.StableSortByTargetThenSequence,
                    EGasRuntimeFrameStreamSortKey.TargetAscThenFactSequence,
                    internalBufferCapacity: 128),
            });
        }

        private static GASRuntimeFrameStreamOwnerEntry Entry(
            EGasRuntimeFrameStreamId streamId,
            GASRuntimeLayoutComponentSlot componentSlot,
            EGasRuntimeCoreFramePhase clearPhase,
            EGasRuntimeCoreFramePhase writePhase,
            EGasRuntimeCoreFramePhase readPhase,
            EGasRuntimeCoreFramePhase mergePhase,
            EGasRuntimeFrameStreamAuthority authority,
            EGasRuntimeFrameStreamCarrier currentCarrier,
            EGasRuntimeFrameStreamCarrier targetCarrier,
            EGasRuntimeFrameStreamMergePolicy mergePolicy,
            EGasRuntimeFrameStreamSortKey sortKey,
            int internalBufferCapacity)
        {
            var evidence = EGasRuntimeFrameStreamEvidence.BufferPressure
                           | EGasRuntimeFrameStreamEvidence.ClearPhase
                           | EGasRuntimeFrameStreamEvidence.WritePhase
                           | EGasRuntimeFrameStreamEvidence.ReadPhase
                           | EGasRuntimeFrameStreamEvidence.DeterministicSortKey
                           | EGasRuntimeFrameStreamEvidence.BattleHashInput
                           | EGasRuntimeFrameStreamEvidence.ProofOrMigrationMarker;
            if (targetCarrier == EGasRuntimeFrameStreamCarrier.PerThreadNativeStream)
                evidence |= EGasRuntimeFrameStreamEvidence.NativeStreamCandidate;
            if (targetCarrier == EGasRuntimeFrameStreamCarrier.OwnerLocalDynamicBuffer)
                evidence |= EGasRuntimeFrameStreamEvidence.PerOwnerBufferCandidate;
            if (streamId == EGasRuntimeFrameStreamId.ActiveEffectMutation
                || streamId == EGasRuntimeFrameStreamId.AttributeDelta)
            {
                evidence |= EGasRuntimeFrameStreamEvidence.OwnerRangeCounters
                            | EGasRuntimeFrameStreamEvidence.RandomLookupCounters;
            }

            return new GASRuntimeFrameStreamOwnerEntry(
                streamId,
                GASRuntimeQueryLayoutEntryId.GameplayEffectCommandSpecStream,
                componentSlot,
                clearPhase,
                writePhase,
                readPhase,
                mergePhase,
                authority,
                currentCarrier,
                targetCarrier,
                EGasRuntimeFrameStreamScaleStatus.MigrationCarrier,
                mergePolicy,
                sortKey,
                internalBufferCapacity,
                affectsBattleHash: true,
                EGasRuntimeFrameStreamReselectTrigger.X50BufferPressure
                | EGasRuntimeFrameStreamReselectTrigger.X1000ScaleGate
                | EGasRuntimeFrameStreamReselectTrigger.BufferExternalized
                | EGasRuntimeFrameStreamReselectTrigger.ParallelProducerIntroduced
                | EGasRuntimeFrameStreamReselectTrigger.MergeCostDominatesSpecEvaluation
                | EGasRuntimeFrameStreamReselectTrigger.BattleHashInstability,
                evidence);
        }
    }
}
