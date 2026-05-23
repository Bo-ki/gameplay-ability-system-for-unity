using System;
using System.Collections.Generic;

namespace GAS.Runtime
{
    public enum GASRuntimeQueryLayoutEntryId
    {
        None = 0,
        AscStableState = 1,
        AbilityCommandRequest = 2,
        AbilityCommitGate = 3,
        AbilityTickLifecycle = 4,
        AbilityTimelineRuntime = 5,
        GameplayEffectApplyRequest = 6,
        GameplayEffectActiveRuntime = 7,
        ExecutionCalculationPipeline = 8,
        AttributeRecalculate = 9,
        AttributeFactProjection = 10,
        TagMaskRuntime = 11,
        ObservationReplayAndOutbox = 12,
        ManagedCuePresentation = 13,
    }

    public enum GASRuntimeLayoutDomain
    {
        None = 0,
        AbilitySystem = 1,
        Ability = 2,
        GameplayEffect = 3,
        Attribute = 4,
        GameplayTag = 5,
        Observation = 6,
        GameplayCue = 7,
    }

    public enum GASRuntimeEntityKind
    {
        None = 0,
        AbilitySystemComponent = 1,
        AbilityRuntime = 2,
        GameplayEffectRuntime = 3,
        Request = 4,
        EventBus = 5,
        ManagedPresentation = 6,
    }

    public enum GASRuntimeLayoutDecision
    {
        None = 0,
        StableQueryLayout = 1,
        NeedsEcbMigration = 2,
        ObservationOnly = 3,
        ManagedPresentationBoundary = 4,
    }

    [Flags]
    public enum GASRuntimeLayoutCapability
    {
        None = 0,
        QueryBased = 1 << 0,
        JobCandidate = 1 << 1,
        BurstCandidate = 1 << 2,
        GeneratedArchetypeCandidate = 1 << 3,
        EcbMigrationCandidate = 1 << 4,
        EnableableCandidate = 1 << 5,
        ObservationOnly = 1 << 6,
        ManagedPresentationBoundary = 1 << 7,
        RequiresMainThreadEntityManager = 1 << 8,
        StructuralChanges = 1 << 9,
        ReadsDefinitionData = 1 << 10,
        WritesSimulationState = 1 << 11,
    }

    [Flags]
    public enum GASRuntimeLayoutBoundary
    {
        None = 0,
        RequestEntityWriteBoundary = 1 << 0,
        ObservationReadBoundary = 1 << 1,
        ManagedPresentationBoundary = 1 << 2,
        StructuralEntityManagerHotspot = 1 << 3,
        CrossEntityLookup = 1 << 4,
        DynamicBufferMutation = 1 << 5,
        DefinitionRuntimeBoundary = 1 << 6,
    }

    public enum GASRuntimeLayoutComponentSlot
    {
        None = 0,
        AscBasicData = 1,
        TagMask = 2,
        AttributeBuffer = 3,
        ActiveModifierBuffer = 4,
        GrantedAbilityBuffer = 5,
        GameplayEffectBuffer = 6,
        AbilityCommandRequest = 7,
        AbilityCommitRequest = 8,
        AbilityBaseInfo = 9,
        AbilityConfig = 10,
        AbilityRuntimeState = 11,
        AbilityActive = 12,
        AbilityTimelineRef = 13,
        AbilityTimelineRuntime = 14,
        ApplyGameplayEffectRequest = 15,
        TargetData = 16,
        EffectContext = 17,
        EffectSpecData = 18,
        EffectLifecycle = 19,
        EffectDestroy = 20,
        DurationDefinition = 21,
        DurationRuntime = 22,
        PeriodDefinition = 23,
        PeriodRuntime = 24,
        StackingRuntime = 25,
        MagnitudeDefinitionBuffer = 26,
        ExecutionCalculationDefinitionBuffer = 27,
        ExecutionCalculationValueBuffer = 28,
        ResolvedModifierBuffer = 29,
        GameplayEventBus = 30,
        PresentationOutbox = 31,
        DebugReplayLog = 32,
        ManagedCueComponent = 33,
        CueEnableableState = 34,
    }

    public readonly struct GASRuntimeQueryLayoutEntry
    {
        private readonly GASRuntimeLayoutComponentSlot[] _requiredSlots;
        private readonly GASRuntimeLayoutComponentSlot[] _optionalSlots;
        private readonly Type[] _systemTypes;

        public readonly GASRuntimeQueryLayoutEntryId EntryId;
        public readonly GASRuntimeLayoutDomain Domain;
        public readonly GASRuntimeEntityKind EntityKind;
        public readonly GASRuntimeLayoutCapability Capabilities;
        public readonly GASRuntimeLayoutBoundary Boundaries;
        public readonly GASRuntimeLayoutDecision Decision;

        public GASRuntimeQueryLayoutEntry(
            GASRuntimeQueryLayoutEntryId entryId,
            GASRuntimeLayoutDomain domain,
            GASRuntimeEntityKind entityKind,
            GASRuntimeLayoutCapability capabilities,
            GASRuntimeLayoutBoundary boundaries,
            GASRuntimeLayoutDecision decision,
            IEnumerable<GASRuntimeLayoutComponentSlot> requiredSlots,
            IEnumerable<GASRuntimeLayoutComponentSlot> optionalSlots,
            IEnumerable<Type> systemTypes)
        {
            EntryId = entryId;
            Domain = domain;
            EntityKind = entityKind;
            Capabilities = capabilities;
            Boundaries = boundaries;
            Decision = decision;
            _requiredSlots = Materialize(requiredSlots);
            _optionalSlots = Materialize(optionalSlots);
            _systemTypes = Materialize(systemTypes);
        }

        public IReadOnlyList<GASRuntimeLayoutComponentSlot> RequiredSlots =>
            _requiredSlots ?? Array.Empty<GASRuntimeLayoutComponentSlot>();

        public IReadOnlyList<GASRuntimeLayoutComponentSlot> OptionalSlots =>
            _optionalSlots ?? Array.Empty<GASRuntimeLayoutComponentSlot>();

        public IReadOnlyList<Type> SystemTypes => _systemTypes ?? Array.Empty<Type>();

        public bool HasCapability(GASRuntimeLayoutCapability capability)
        {
            return capability != GASRuntimeLayoutCapability.None
                   && (Capabilities & capability) == capability;
        }

        public bool HasBoundary(GASRuntimeLayoutBoundary boundary)
        {
            return boundary != GASRuntimeLayoutBoundary.None
                   && (Boundaries & boundary) == boundary;
        }

        public bool HasRequiredSlot(GASRuntimeLayoutComponentSlot slot)
        {
            return ContainsSlot(RequiredSlots, slot);
        }

        public bool HasOptionalSlot(GASRuntimeLayoutComponentSlot slot)
        {
            return ContainsSlot(OptionalSlots, slot);
        }

        private static bool ContainsSlot(
            IReadOnlyList<GASRuntimeLayoutComponentSlot> slots,
            GASRuntimeLayoutComponentSlot slot)
        {
            if (slot == GASRuntimeLayoutComponentSlot.None)
                return false;

            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] == slot)
                    return true;
            }

            return false;
        }

        private static T[] Materialize<T>(IEnumerable<T> values)
        {
            if (values == null)
                return Array.Empty<T>();

            return values is T[] array ? (T[])array.Clone() : new List<T>(values).ToArray();
        }
    }

    public readonly struct GASRuntimeQueryLayoutPlan
    {
        private readonly GASRuntimeQueryLayoutEntry[] _entries;

        public GASRuntimeQueryLayoutPlan(IEnumerable<GASRuntimeQueryLayoutEntry> entries)
        {
            _entries = entries is GASRuntimeQueryLayoutEntry[] array
                ? (GASRuntimeQueryLayoutEntry[])array.Clone()
                : new List<GASRuntimeQueryLayoutEntry>(
                    entries ?? Array.Empty<GASRuntimeQueryLayoutEntry>()).ToArray();
        }

        public static GASRuntimeQueryLayoutPlan Empty =>
            new(Array.Empty<GASRuntimeQueryLayoutEntry>());

        public IReadOnlyList<GASRuntimeQueryLayoutEntry> Entries =>
            _entries ?? Array.Empty<GASRuntimeQueryLayoutEntry>();

        public int EntryCount => Entries.Count;

        public int JobCandidateCount => CountEntriesWithCapability(GASRuntimeLayoutCapability.JobCandidate);

        public int BurstCandidateCount => CountEntriesWithCapability(GASRuntimeLayoutCapability.BurstCandidate);

        public int EcbMigrationCandidateCount =>
            CountEntriesWithCapability(GASRuntimeLayoutCapability.EcbMigrationCandidate);

        public int GeneratedArchetypeCandidateCount =>
            CountEntriesWithCapability(GASRuntimeLayoutCapability.GeneratedArchetypeCandidate);

        public int ManagedPresentationBoundaryCount =>
            CountEntriesWithCapability(GASRuntimeLayoutCapability.ManagedPresentationBoundary);

        public bool RequiresEcbMigrationBeforeFullGeneratedRuntimeIntegration =>
            EcbMigrationCandidateCount > 0;

        public bool TryFind(
            GASRuntimeQueryLayoutEntryId entryId,
            out GASRuntimeQueryLayoutEntry entry)
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

        public int CountEntriesWithCapability(GASRuntimeLayoutCapability capability)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].HasCapability(capability))
                    count++;
            }

            return count;
        }

        public int CountEntriesWithBoundary(GASRuntimeLayoutBoundary boundary)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].HasBoundary(boundary))
                    count++;
            }

            return count;
        }

        public int CountEntriesWithDecision(GASRuntimeLayoutDecision decision)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Decision == decision)
                    count++;
            }

            return count;
        }
    }

    public static class GASRuntimeQueryLayoutPlanner
    {
        public static GASRuntimeQueryLayoutPlan CreateCurrent()
        {
            return new GASRuntimeQueryLayoutPlan(new[]
            {
                Entry(
                    GASRuntimeQueryLayoutEntryId.AscStableState,
                    GASRuntimeLayoutDomain.AbilitySystem,
                    GASRuntimeEntityKind.AbilitySystemComponent,
                    GASRuntimeLayoutCapability.QueryBased
                    | GASRuntimeLayoutCapability.JobCandidate
                    | GASRuntimeLayoutCapability.BurstCandidate
                    | GASRuntimeLayoutCapability.GeneratedArchetypeCandidate
                    | GASRuntimeLayoutCapability.WritesSimulationState,
                    GASRuntimeLayoutBoundary.DynamicBufferMutation,
                    GASRuntimeLayoutDecision.StableQueryLayout,
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.AscBasicData,
                        GASRuntimeLayoutComponentSlot.TagMask,
                        GASRuntimeLayoutComponentSlot.AttributeBuffer,
                    },
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.ActiveModifierBuffer,
                        GASRuntimeLayoutComponentSlot.GrantedAbilityBuffer,
                        GASRuntimeLayoutComponentSlot.GameplayEffectBuffer,
                    },
                    typeof(SASCCreate),
                    typeof(SAscInitializeRequest),
                    typeof(SAscDestroyRequest),
                    typeof(SAscDestroyFinalize)),
                Entry(
                    GASRuntimeQueryLayoutEntryId.AbilityCommandRequest,
                    GASRuntimeLayoutDomain.Ability,
                    GASRuntimeEntityKind.Request,
                    GASRuntimeLayoutCapability.QueryBased
                    | GASRuntimeLayoutCapability.EcbMigrationCandidate
                    | GASRuntimeLayoutCapability.RequiresMainThreadEntityManager
                    | GASRuntimeLayoutCapability.StructuralChanges
                    | GASRuntimeLayoutCapability.WritesSimulationState,
                    GASRuntimeLayoutBoundary.RequestEntityWriteBoundary
                    | GASRuntimeLayoutBoundary.StructuralEntityManagerHotspot,
                    GASRuntimeLayoutDecision.NeedsEcbMigration,
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.AbilityCommandRequest,
                    },
                    Array.Empty<GASRuntimeLayoutComponentSlot>(),
                    typeof(SAbilityCommandRequest),
                    typeof(STryActivateAbility)),
                Entry(
                    GASRuntimeQueryLayoutEntryId.AbilityCommitGate,
                    GASRuntimeLayoutDomain.Ability,
                    GASRuntimeEntityKind.AbilityRuntime,
                    GASRuntimeLayoutCapability.QueryBased
                    | GASRuntimeLayoutCapability.GeneratedArchetypeCandidate
                    | GASRuntimeLayoutCapability.EcbMigrationCandidate
                    | GASRuntimeLayoutCapability.EnableableCandidate
                    | GASRuntimeLayoutCapability.RequiresMainThreadEntityManager
                    | GASRuntimeLayoutCapability.StructuralChanges
                    | GASRuntimeLayoutCapability.ReadsDefinitionData
                    | GASRuntimeLayoutCapability.WritesSimulationState,
                    GASRuntimeLayoutBoundary.CrossEntityLookup
                    | GASRuntimeLayoutBoundary.DynamicBufferMutation
                    | GASRuntimeLayoutBoundary.DefinitionRuntimeBoundary
                    | GASRuntimeLayoutBoundary.StructuralEntityManagerHotspot,
                    GASRuntimeLayoutDecision.NeedsEcbMigration,
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.AbilityCommitRequest,
                        GASRuntimeLayoutComponentSlot.AbilityBaseInfo,
                        GASRuntimeLayoutComponentSlot.AbilityRuntimeState,
                        GASRuntimeLayoutComponentSlot.AbilityConfig,
                    },
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.AbilityActive,
                        GASRuntimeLayoutComponentSlot.AttributeBuffer,
                        GASRuntimeLayoutComponentSlot.TagMask,
                    },
                    typeof(SAbilityCommit)),
                Entry(
                    GASRuntimeQueryLayoutEntryId.AbilityTickLifecycle,
                    GASRuntimeLayoutDomain.Ability,
                    GASRuntimeEntityKind.AbilityRuntime,
                    GASRuntimeLayoutCapability.QueryBased
                    | GASRuntimeLayoutCapability.JobCandidate
                    | GASRuntimeLayoutCapability.BurstCandidate
                    | GASRuntimeLayoutCapability.GeneratedArchetypeCandidate
                    | GASRuntimeLayoutCapability.EnableableCandidate
                    | GASRuntimeLayoutCapability.WritesSimulationState,
                    GASRuntimeLayoutBoundary.None,
                    GASRuntimeLayoutDecision.StableQueryLayout,
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.AbilityBaseInfo,
                        GASRuntimeLayoutComponentSlot.AbilityRuntimeState,
                    },
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.AbilityActive,
                    },
                    typeof(SAbilityTick),
                    typeof(SAbilityLifecycleRequest),
                    typeof(SAbilityStateCleanup)),
                Entry(
                    GASRuntimeQueryLayoutEntryId.AbilityTimelineRuntime,
                    GASRuntimeLayoutDomain.Ability,
                    GASRuntimeEntityKind.AbilityRuntime,
                    GASRuntimeLayoutCapability.QueryBased
                    | GASRuntimeLayoutCapability.EcbMigrationCandidate
                    | GASRuntimeLayoutCapability.RequiresMainThreadEntityManager
                    | GASRuntimeLayoutCapability.StructuralChanges
                    | GASRuntimeLayoutCapability.ReadsDefinitionData
                    | GASRuntimeLayoutCapability.WritesSimulationState,
                    GASRuntimeLayoutBoundary.CrossEntityLookup
                    | GASRuntimeLayoutBoundary.DefinitionRuntimeBoundary
                    | GASRuntimeLayoutBoundary.StructuralEntityManagerHotspot,
                    GASRuntimeLayoutDecision.NeedsEcbMigration,
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.AbilityActive,
                        GASRuntimeLayoutComponentSlot.AbilityBaseInfo,
                        GASRuntimeLayoutComponentSlot.AbilityRuntimeState,
                        GASRuntimeLayoutComponentSlot.AbilityTimelineRef,
                    },
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.AbilityTimelineRuntime,
                    },
                    typeof(SAbilityTimelineAction),
                    typeof(SAbilityTimelineLifecycleRequest)),
                Entry(
                    GASRuntimeQueryLayoutEntryId.GameplayEffectApplyRequest,
                    GASRuntimeLayoutDomain.GameplayEffect,
                    GASRuntimeEntityKind.Request,
                    GASRuntimeLayoutCapability.QueryBased
                    | GASRuntimeLayoutCapability.EcbMigrationCandidate
                    | GASRuntimeLayoutCapability.RequiresMainThreadEntityManager
                    | GASRuntimeLayoutCapability.StructuralChanges
                    | GASRuntimeLayoutCapability.ReadsDefinitionData
                    | GASRuntimeLayoutCapability.WritesSimulationState,
                    GASRuntimeLayoutBoundary.RequestEntityWriteBoundary
                    | GASRuntimeLayoutBoundary.CrossEntityLookup
                    | GASRuntimeLayoutBoundary.DynamicBufferMutation
                    | GASRuntimeLayoutBoundary.DefinitionRuntimeBoundary
                    | GASRuntimeLayoutBoundary.StructuralEntityManagerHotspot,
                    GASRuntimeLayoutDecision.NeedsEcbMigration,
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.ApplyGameplayEffectRequest,
                    },
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.TargetData,
                    },
                    typeof(SApplyGameplayEffectRequest)),
                Entry(
                    GASRuntimeQueryLayoutEntryId.GameplayEffectActiveRuntime,
                    GASRuntimeLayoutDomain.GameplayEffect,
                    GASRuntimeEntityKind.GameplayEffectRuntime,
                    GASRuntimeLayoutCapability.QueryBased
                    | GASRuntimeLayoutCapability.GeneratedArchetypeCandidate
                    | GASRuntimeLayoutCapability.EcbMigrationCandidate
                    | GASRuntimeLayoutCapability.EnableableCandidate
                    | GASRuntimeLayoutCapability.RequiresMainThreadEntityManager
                    | GASRuntimeLayoutCapability.StructuralChanges
                    | GASRuntimeLayoutCapability.ReadsDefinitionData
                    | GASRuntimeLayoutCapability.WritesSimulationState,
                    GASRuntimeLayoutBoundary.CrossEntityLookup
                    | GASRuntimeLayoutBoundary.DynamicBufferMutation
                    | GASRuntimeLayoutBoundary.DefinitionRuntimeBoundary
                    | GASRuntimeLayoutBoundary.StructuralEntityManagerHotspot,
                    GASRuntimeLayoutDecision.NeedsEcbMigration,
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.EffectContext,
                        GASRuntimeLayoutComponentSlot.EffectSpecData,
                        GASRuntimeLayoutComponentSlot.EffectLifecycle,
                    },
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.DurationDefinition,
                        GASRuntimeLayoutComponentSlot.DurationRuntime,
                        GASRuntimeLayoutComponentSlot.PeriodDefinition,
                        GASRuntimeLayoutComponentSlot.PeriodRuntime,
                        GASRuntimeLayoutComponentSlot.StackingRuntime,
                        GASRuntimeLayoutComponentSlot.EffectDestroy,
                    },
                    typeof(SEffectApply),
                    typeof(SOngoingTagRequirements),
                    typeof(SEffectRemove),
                    typeof(SEffectTick)),
                Entry(
                    GASRuntimeQueryLayoutEntryId.ExecutionCalculationPipeline,
                    GASRuntimeLayoutDomain.GameplayEffect,
                    GASRuntimeEntityKind.GameplayEffectRuntime,
                    GASRuntimeLayoutCapability.QueryBased
                    | GASRuntimeLayoutCapability.GeneratedArchetypeCandidate
                    | GASRuntimeLayoutCapability.EcbMigrationCandidate
                    | GASRuntimeLayoutCapability.RequiresMainThreadEntityManager
                    | GASRuntimeLayoutCapability.StructuralChanges
                    | GASRuntimeLayoutCapability.ReadsDefinitionData
                    | GASRuntimeLayoutCapability.WritesSimulationState,
                    GASRuntimeLayoutBoundary.CrossEntityLookup
                    | GASRuntimeLayoutBoundary.DynamicBufferMutation
                    | GASRuntimeLayoutBoundary.DefinitionRuntimeBoundary
                    | GASRuntimeLayoutBoundary.StructuralEntityManagerHotspot,
                    GASRuntimeLayoutDecision.NeedsEcbMigration,
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.EffectContext,
                        GASRuntimeLayoutComponentSlot.EffectSpecData,
                        GASRuntimeLayoutComponentSlot.ExecutionCalculationDefinitionBuffer,
                    },
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.ExecutionCalculationValueBuffer,
                        GASRuntimeLayoutComponentSlot.ResolvedModifierBuffer,
                    },
                    typeof(SExecutionCalculation),
                    typeof(GASExecutionCalculationExtensionGroup),
                    typeof(SExecutionCalculationOutputModifier)),
                Entry(
                    GASRuntimeQueryLayoutEntryId.AttributeRecalculate,
                    GASRuntimeLayoutDomain.Attribute,
                    GASRuntimeEntityKind.AbilitySystemComponent,
                    GASRuntimeLayoutCapability.QueryBased
                    | GASRuntimeLayoutCapability.JobCandidate
                    | GASRuntimeLayoutCapability.BurstCandidate
                    | GASRuntimeLayoutCapability.GeneratedArchetypeCandidate
                    | GASRuntimeLayoutCapability.WritesSimulationState,
                    GASRuntimeLayoutBoundary.DynamicBufferMutation,
                    GASRuntimeLayoutDecision.StableQueryLayout,
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.TagMask,
                        GASRuntimeLayoutComponentSlot.AttributeBuffer,
                    },
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.ActiveModifierBuffer,
                    },
                    typeof(SAttributeRecalculate)),
                Entry(
                    GASRuntimeQueryLayoutEntryId.AttributeFactProjection,
                    GASRuntimeLayoutDomain.Attribute,
                    GASRuntimeEntityKind.EventBus,
                    GASRuntimeLayoutCapability.QueryBased
                    | GASRuntimeLayoutCapability.ObservationOnly
                    | GASRuntimeLayoutCapability.RequiresMainThreadEntityManager,
                    GASRuntimeLayoutBoundary.ObservationReadBoundary
                    | GASRuntimeLayoutBoundary.CrossEntityLookup
                    | GASRuntimeLayoutBoundary.DynamicBufferMutation,
                    GASRuntimeLayoutDecision.ObservationOnly,
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.AttributeBuffer,
                        GASRuntimeLayoutComponentSlot.GameplayEventBus,
                    },
                    Array.Empty<GASRuntimeLayoutComponentSlot>(),
                    typeof(SAttributeChangeEventProjection)),
                Entry(
                    GASRuntimeQueryLayoutEntryId.TagMaskRuntime,
                    GASRuntimeLayoutDomain.GameplayTag,
                    GASRuntimeEntityKind.AbilitySystemComponent,
                    GASRuntimeLayoutCapability.QueryBased
                    | GASRuntimeLayoutCapability.JobCandidate
                    | GASRuntimeLayoutCapability.BurstCandidate
                    | GASRuntimeLayoutCapability.GeneratedArchetypeCandidate
                    | GASRuntimeLayoutCapability.WritesSimulationState,
                    GASRuntimeLayoutBoundary.DynamicBufferMutation,
                    GASRuntimeLayoutDecision.StableQueryLayout,
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.TagMask,
                    },
                    Array.Empty<GASRuntimeLayoutComponentSlot>(),
                    typeof(STagChangeProcess)),
                Entry(
                    GASRuntimeQueryLayoutEntryId.ObservationReplayAndOutbox,
                    GASRuntimeLayoutDomain.Observation,
                    GASRuntimeEntityKind.EventBus,
                    GASRuntimeLayoutCapability.QueryBased
                    | GASRuntimeLayoutCapability.ObservationOnly
                    | GASRuntimeLayoutCapability.RequiresMainThreadEntityManager,
                    GASRuntimeLayoutBoundary.ObservationReadBoundary
                    | GASRuntimeLayoutBoundary.DynamicBufferMutation,
                    GASRuntimeLayoutDecision.ObservationOnly,
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.GameplayEventBus,
                        GASRuntimeLayoutComponentSlot.PresentationOutbox,
                        GASRuntimeLayoutComponentSlot.DebugReplayLog,
                    },
                    Array.Empty<GASRuntimeLayoutComponentSlot>(),
                    typeof(SEventBusClear),
                    typeof(SHeadlessAutoChessPresentationCueMarkerProjection),
                    typeof(SPresentationOutboxProjection),
                    typeof(SDebugReplayLogProjection)),
                Entry(
                    GASRuntimeQueryLayoutEntryId.ManagedCuePresentation,
                    GASRuntimeLayoutDomain.GameplayCue,
                    GASRuntimeEntityKind.ManagedPresentation,
                    GASRuntimeLayoutCapability.QueryBased
                    | GASRuntimeLayoutCapability.ManagedPresentationBoundary
                    | GASRuntimeLayoutCapability.ObservationOnly
                    | GASRuntimeLayoutCapability.RequiresMainThreadEntityManager,
                    GASRuntimeLayoutBoundary.ManagedPresentationBoundary
                    | GASRuntimeLayoutBoundary.ObservationReadBoundary,
                    GASRuntimeLayoutDecision.ManagedPresentationBoundary,
                    new[]
                    {
                        GASRuntimeLayoutComponentSlot.ManagedCueComponent,
                        GASRuntimeLayoutComponentSlot.CueEnableableState,
                    },
                    Array.Empty<GASRuntimeLayoutComponentSlot>(),
                    typeof(SCueRequestBridge),
                    typeof(SCueStart),
                    typeof(SCueTick),
                    typeof(SCueEnd),
                    typeof(SCueDestroy)),
            });
        }

        private static GASRuntimeQueryLayoutEntry Entry(
            GASRuntimeQueryLayoutEntryId entryId,
            GASRuntimeLayoutDomain domain,
            GASRuntimeEntityKind entityKind,
            GASRuntimeLayoutCapability capabilities,
            GASRuntimeLayoutBoundary boundaries,
            GASRuntimeLayoutDecision decision,
            IEnumerable<GASRuntimeLayoutComponentSlot> requiredSlots,
            IEnumerable<GASRuntimeLayoutComponentSlot> optionalSlots,
            params Type[] systemTypes)
        {
            return new GASRuntimeQueryLayoutEntry(
                entryId,
                domain,
                entityKind,
                capabilities,
                boundaries,
                decision,
                requiredSlots,
                optionalSlots,
                systemTypes);
        }
    }
}
