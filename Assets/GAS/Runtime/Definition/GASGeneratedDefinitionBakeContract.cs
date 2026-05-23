using System;
using System.Collections.Generic;

namespace GAS.Runtime
{
    public enum GASGeneratedDefinitionBakeWriteKind
    {
        None = 0,
        GeneratedCarrier = 1,
        UnityBakerInput = 2,
        StaticDefinitionBlobCache = 3,
        RuntimeArchetypeTemplate = 4,
        DeferredBoundary = 5,
    }

    public enum GASGeneratedDefinitionBakeWritePhase
    {
        None = 0,
        GeneratedDefinitionBuild = 1,
        UnityEntitiesBaker = 2,
        RuntimeDefinitionCache = 3,
        RuntimeArchetypeBuilder = 4,
        RuntimeSystem = 5,
        ManagedPresentation = 6,
    }

    public enum GASGeneratedDefinitionBakeWriteTarget
    {
        None = 0,
        GeneratedDefinitionCarrier = 1,
        UnityEntitiesBakerInput = 2,
        GameplayEffectStaticDefinitionBlobCache = 3,
        RuntimeArchetypeTemplate = 4,
        RuntimeLifecycleSystems = 5,
        RuntimeTimelineSystems = 6,
        ManagedPresentationBridge = 7,
        GameplayEffectCacheLifecycleOwner = 8,
    }

    public enum GASGeneratedDefinitionArchetypeTemplateKind
    {
        None = 0,
        AbilityDefinition = 1,
        GameplayEffectDefinition = 2,
        AttributeSetDefinition = 3,
        AttributeDefinition = 4,
        GameplayTagDefinition = 5,
        GameplayCueDefinition = 6,
    }

    [Flags]
    public enum GASGeneratedDefinitionArchetypeSlot
    {
        None = 0,
        DefinitionKey = 1 << 0,
        StaticComponentData = 1 << 1,
        StaticBufferData = 1 << 2,
        TagRequirementData = 1 << 3,
        AttributeDefaultData = 1 << 4,
        DefinitionReferenceEdges = 1 << 5,
        TimelineReferenceKey = 1 << 6,
        StaticDefinitionBlobReferenceSlot = 1 << 7,
        PresentationCueKey = 1 << 8,
    }

    public readonly struct GASGeneratedDefinitionBakeWrite
    {
        public readonly GASDefinitionKind DefinitionKind;
        public readonly int DefinitionCode;
        public readonly GASGeneratedDefinitionBakeWriteKind WriteKind;
        public readonly GASGeneratedDefinitionBakeWritePhase WritePhase;
        public readonly GASGeneratedDefinitionBakeWriteTarget WriteTarget;
        public readonly GASGeneratedDefinitionBakingBoundary Boundary;
        public readonly bool IsEligible;

        public GASGeneratedDefinitionBakeWrite(
            GASDefinitionKind definitionKind,
            int definitionCode,
            GASGeneratedDefinitionBakeWriteKind writeKind,
            GASGeneratedDefinitionBakeWritePhase writePhase,
            GASGeneratedDefinitionBakeWriteTarget writeTarget,
            GASGeneratedDefinitionBakingBoundary boundary,
            bool isEligible)
        {
            DefinitionKind = definitionKind;
            DefinitionCode = definitionCode;
            WriteKind = writeKind;
            WritePhase = writePhase;
            WriteTarget = writeTarget;
            Boundary = boundary;
            IsEligible = isEligible;
        }
    }

    public readonly struct GASGeneratedDefinitionArchetypeTemplate
    {
        public readonly GASDefinitionKind DefinitionKind;
        public readonly int DefinitionCode;
        public readonly GASGeneratedDefinitionArchetypeTemplateKind TemplateKind;
        public readonly GASGeneratedDefinitionArchetypeSlot Slots;
        public readonly GASGeneratedDefinitionBakingBoundary DeferredBoundaries;
        public readonly bool IsEligible;

        public GASGeneratedDefinitionArchetypeTemplate(
            GASDefinitionKind definitionKind,
            int definitionCode,
            GASGeneratedDefinitionArchetypeTemplateKind templateKind,
            GASGeneratedDefinitionArchetypeSlot slots,
            GASGeneratedDefinitionBakingBoundary deferredBoundaries,
            bool isEligible)
        {
            DefinitionKind = definitionKind;
            DefinitionCode = definitionCode;
            TemplateKind = templateKind;
            Slots = slots;
            DeferredBoundaries = deferredBoundaries;
            IsEligible = isEligible;
        }

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
    }

    public readonly struct GASGeneratedDefinitionBakeContract
    {
        private readonly GASGeneratedDefinitionBakeWrite[] _writes;
        private readonly GASGeneratedDefinitionArchetypeTemplate[] _archetypeTemplates;

        public readonly GASGeneratedDefinitionBakingPlan SourcePlan;

        public GASGeneratedDefinitionBakeContract(
            GASGeneratedDefinitionBakingPlan sourcePlan,
            IEnumerable<GASGeneratedDefinitionBakeWrite> writes,
            IEnumerable<GASGeneratedDefinitionArchetypeTemplate> archetypeTemplates)
        {
            SourcePlan = sourcePlan;
            _writes = writes is GASGeneratedDefinitionBakeWrite[] writeArray
                ? (GASGeneratedDefinitionBakeWrite[])writeArray.Clone()
                : new List<GASGeneratedDefinitionBakeWrite>(writes ?? Array.Empty<GASGeneratedDefinitionBakeWrite>()).ToArray();
            _archetypeTemplates = archetypeTemplates is GASGeneratedDefinitionArchetypeTemplate[] templateArray
                ? (GASGeneratedDefinitionArchetypeTemplate[])templateArray.Clone()
                : new List<GASGeneratedDefinitionArchetypeTemplate>(
                    archetypeTemplates ?? Array.Empty<GASGeneratedDefinitionArchetypeTemplate>()).ToArray();
        }

        public static GASGeneratedDefinitionBakeContract Empty =>
            new(
                GASGeneratedDefinitionBakingPlan.Empty,
                Array.Empty<GASGeneratedDefinitionBakeWrite>(),
                Array.Empty<GASGeneratedDefinitionArchetypeTemplate>());

        public IReadOnlyList<GASGeneratedDefinitionBakeWrite> Writes =>
            _writes ?? Array.Empty<GASGeneratedDefinitionBakeWrite>();

        public IReadOnlyList<GASGeneratedDefinitionArchetypeTemplate> ArchetypeTemplates =>
            _archetypeTemplates ?? Array.Empty<GASGeneratedDefinitionArchetypeTemplate>();

        public int WriteCount => Writes.Count;
        public int ArchetypeTemplateCount => ArchetypeTemplates.Count;
        public int GeneratedCarrierWriteCount => CountWrites(GASGeneratedDefinitionBakeWriteKind.GeneratedCarrier, eligibleOnly: false);
        public int UnityBakerInputWriteCount => CountWrites(GASGeneratedDefinitionBakeWriteKind.UnityBakerInput, eligibleOnly: false);
        public int EligibleUnityBakerInputWriteCount => CountWrites(GASGeneratedDefinitionBakeWriteKind.UnityBakerInput, eligibleOnly: true);
        public int StaticDefinitionBlobCacheWriteCount => CountWrites(GASGeneratedDefinitionBakeWriteKind.StaticDefinitionBlobCache, eligibleOnly: false);
        public int EligibleStaticDefinitionBlobCacheWriteCount => CountWrites(GASGeneratedDefinitionBakeWriteKind.StaticDefinitionBlobCache, eligibleOnly: true);
        public int DeferredBoundaryWriteCount => CountWrites(GASGeneratedDefinitionBakeWriteKind.DeferredBoundary, eligibleOnly: false);
        public int EligibleArchetypeTemplateCount => CountArchetypeTemplates(eligibleOnly: true);
        public bool CanRunUnityBaker => SourcePlan.CanBake && EligibleUnityBakerInputWriteCount == SourcePlan.EligibleBakerInputCount;

        private int CountWrites(GASGeneratedDefinitionBakeWriteKind writeKind, bool eligibleOnly)
        {
            var count = 0;
            var writes = Writes;
            for (var i = 0; i < writes.Count; i++)
            {
                if (writes[i].WriteKind != writeKind)
                    continue;
                if (eligibleOnly && !writes[i].IsEligible)
                    continue;
                count++;
            }

            return count;
        }

        private int CountArchetypeTemplates(bool eligibleOnly)
        {
            var count = 0;
            var templates = ArchetypeTemplates;
            for (var i = 0; i < templates.Count; i++)
            {
                if (eligibleOnly && !templates[i].IsEligible)
                    continue;
                count++;
            }

            return count;
        }
    }

    public static class GASGeneratedDefinitionBakeContractPlanner
    {
        public static GASGeneratedDefinitionBakeContract Create(GASGeneratedDefinitionBakingPlan plan)
        {
            var writes = new List<GASGeneratedDefinitionBakeWrite>();
            var archetypeTemplates = new List<GASGeneratedDefinitionArchetypeTemplate>();
            var entries = plan.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                AppendCarrierWrites(plan, entry, writes);
                AppendArchetypeTemplate(plan, entry, archetypeTemplates, writes);
                AppendDeferredBoundaryWrites(entry, writes);
            }

            if (plan.TimelineDefinitionCount > 0)
            {
                writes.Add(new GASGeneratedDefinitionBakeWrite(
                    GASDefinitionKind.TimelineAbility,
                    0,
                    GASGeneratedDefinitionBakeWriteKind.DeferredBoundary,
                    GASGeneratedDefinitionBakeWritePhase.RuntimeSystem,
                    GASGeneratedDefinitionBakeWriteTarget.RuntimeTimelineSystems,
                    GASGeneratedDefinitionBakingBoundary.RuntimeTimeline,
                    false));
            }

            if (plan.HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary.MissingGameplayEffectConfigProvider))
            {
                writes.Add(new GASGeneratedDefinitionBakeWrite(
                    GASDefinitionKind.None,
                    0,
                    GASGeneratedDefinitionBakeWriteKind.DeferredBoundary,
                    GASGeneratedDefinitionBakeWritePhase.RuntimeDefinitionCache,
                    GASGeneratedDefinitionBakeWriteTarget.GameplayEffectCacheLifecycleOwner,
                    GASGeneratedDefinitionBakingBoundary.MissingGameplayEffectConfigProvider,
                    false));
            }

            return new GASGeneratedDefinitionBakeContract(plan, writes, archetypeTemplates);
        }

        private static void AppendCarrierWrites(
            GASGeneratedDefinitionBakingPlan plan,
            GASGeneratedDefinitionBakingEntry entry,
            List<GASGeneratedDefinitionBakeWrite> writes)
        {
            if (entry.HasCapability(GASGeneratedDefinitionBakingCapability.GeneratedCarrier))
            {
                writes.Add(new GASGeneratedDefinitionBakeWrite(
                    entry.DefinitionKind,
                    entry.DefinitionCode,
                    GASGeneratedDefinitionBakeWriteKind.GeneratedCarrier,
                    GASGeneratedDefinitionBakeWritePhase.GeneratedDefinitionBuild,
                    GASGeneratedDefinitionBakeWriteTarget.GeneratedDefinitionCarrier,
                    GASGeneratedDefinitionBakingBoundary.None,
                    plan.CanBake));
            }

            if (entry.HasCapability(GASGeneratedDefinitionBakingCapability.BakerInput))
            {
                writes.Add(new GASGeneratedDefinitionBakeWrite(
                    entry.DefinitionKind,
                    entry.DefinitionCode,
                    GASGeneratedDefinitionBakeWriteKind.UnityBakerInput,
                    GASGeneratedDefinitionBakeWritePhase.UnityEntitiesBaker,
                    GASGeneratedDefinitionBakeWriteTarget.UnityEntitiesBakerInput,
                    GASGeneratedDefinitionBakingBoundary.None,
                    plan.CanBake));
            }

            if (entry.HasCapability(GASGeneratedDefinitionBakingCapability.StaticDefinitionBlob))
            {
                writes.Add(new GASGeneratedDefinitionBakeWrite(
                    entry.DefinitionKind,
                    entry.DefinitionCode,
                    GASGeneratedDefinitionBakeWriteKind.StaticDefinitionBlobCache,
                    GASGeneratedDefinitionBakeWritePhase.RuntimeDefinitionCache,
                    GASGeneratedDefinitionBakeWriteTarget.GameplayEffectStaticDefinitionBlobCache,
                    GASGeneratedDefinitionBakingBoundary.GameplayEffectCacheLifecycleOwner,
                    plan.CanBake && plan.HasGameplayEffectConfigProvider));
            }
        }

        private static void AppendArchetypeTemplate(
            GASGeneratedDefinitionBakingPlan plan,
            GASGeneratedDefinitionBakingEntry entry,
            List<GASGeneratedDefinitionArchetypeTemplate> templates,
            List<GASGeneratedDefinitionBakeWrite> writes)
        {
            if (!entry.HasCapability(GASGeneratedDefinitionBakingCapability.BakerInput))
                return;

            var templateKind = ToTemplateKind(entry.DefinitionKind);
            if (templateKind == GASGeneratedDefinitionArchetypeTemplateKind.None)
                return;

            templates.Add(new GASGeneratedDefinitionArchetypeTemplate(
                entry.DefinitionKind,
                entry.DefinitionCode,
                templateKind,
                GetStaticSlots(entry),
                entry.DeferredBoundaries,
                plan.CanBake));

            writes.Add(new GASGeneratedDefinitionBakeWrite(
                entry.DefinitionKind,
                entry.DefinitionCode,
                GASGeneratedDefinitionBakeWriteKind.RuntimeArchetypeTemplate,
                GASGeneratedDefinitionBakeWritePhase.RuntimeArchetypeBuilder,
                GASGeneratedDefinitionBakeWriteTarget.RuntimeArchetypeTemplate,
                GASGeneratedDefinitionBakingBoundary.None,
                plan.CanBake));
        }

        private static GASGeneratedDefinitionArchetypeTemplateKind ToTemplateKind(GASDefinitionKind definitionKind)
        {
            switch (definitionKind)
            {
                case GASDefinitionKind.Ability:
                    return GASGeneratedDefinitionArchetypeTemplateKind.AbilityDefinition;
                case GASDefinitionKind.GameplayEffect:
                    return GASGeneratedDefinitionArchetypeTemplateKind.GameplayEffectDefinition;
                case GASDefinitionKind.AttributeSet:
                    return GASGeneratedDefinitionArchetypeTemplateKind.AttributeSetDefinition;
                case GASDefinitionKind.Attribute:
                    return GASGeneratedDefinitionArchetypeTemplateKind.AttributeDefinition;
                case GASDefinitionKind.GameplayTag:
                    return GASGeneratedDefinitionArchetypeTemplateKind.GameplayTagDefinition;
                case GASDefinitionKind.GameplayCue:
                    return GASGeneratedDefinitionArchetypeTemplateKind.GameplayCueDefinition;
                default:
                    return GASGeneratedDefinitionArchetypeTemplateKind.None;
            }
        }

        private static GASGeneratedDefinitionArchetypeSlot GetStaticSlots(GASGeneratedDefinitionBakingEntry entry)
        {
            var slots =
                GASGeneratedDefinitionArchetypeSlot.DefinitionKey
                | GASGeneratedDefinitionArchetypeSlot.StaticComponentData;

            switch (entry.DefinitionKind)
            {
                case GASDefinitionKind.Ability:
                    slots |=
                        GASGeneratedDefinitionArchetypeSlot.StaticBufferData
                        | GASGeneratedDefinitionArchetypeSlot.TagRequirementData
                        | GASGeneratedDefinitionArchetypeSlot.DefinitionReferenceEdges;
                    if (entry.HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary.RuntimeTimeline))
                        slots |= GASGeneratedDefinitionArchetypeSlot.TimelineReferenceKey;
                    break;
                case GASDefinitionKind.GameplayEffect:
                    slots |=
                        GASGeneratedDefinitionArchetypeSlot.StaticBufferData
                        | GASGeneratedDefinitionArchetypeSlot.TagRequirementData
                        | GASGeneratedDefinitionArchetypeSlot.DefinitionReferenceEdges
                        | GASGeneratedDefinitionArchetypeSlot.StaticDefinitionBlobReferenceSlot;
                    if (entry.HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary.ManagedPresentation))
                        slots |= GASGeneratedDefinitionArchetypeSlot.PresentationCueKey;
                    break;
                case GASDefinitionKind.AttributeSet:
                    slots |=
                        GASGeneratedDefinitionArchetypeSlot.StaticBufferData
                        | GASGeneratedDefinitionArchetypeSlot.AttributeDefaultData;
                    break;
                case GASDefinitionKind.Attribute:
                    slots |= GASGeneratedDefinitionArchetypeSlot.AttributeDefaultData;
                    break;
                case GASDefinitionKind.GameplayTag:
                    slots |= GASGeneratedDefinitionArchetypeSlot.DefinitionReferenceEdges;
                    break;
                case GASDefinitionKind.GameplayCue:
                    slots |=
                        GASGeneratedDefinitionArchetypeSlot.TagRequirementData
                        | GASGeneratedDefinitionArchetypeSlot.PresentationCueKey;
                    break;
            }

            return slots;
        }

        private static void AppendDeferredBoundaryWrites(
            GASGeneratedDefinitionBakingEntry entry,
            List<GASGeneratedDefinitionBakeWrite> writes)
        {
            AppendDeferredBoundaryWrite(
                entry,
                GASGeneratedDefinitionBakingBoundary.RuntimeLifecycle,
                GASGeneratedDefinitionBakeWritePhase.RuntimeSystem,
                GASGeneratedDefinitionBakeWriteTarget.RuntimeLifecycleSystems,
                writes);
            AppendDeferredBoundaryWrite(
                entry,
                GASGeneratedDefinitionBakingBoundary.RuntimeTimeline,
                GASGeneratedDefinitionBakeWritePhase.RuntimeSystem,
                GASGeneratedDefinitionBakeWriteTarget.RuntimeTimelineSystems,
                writes);
            AppendDeferredBoundaryWrite(
                entry,
                GASGeneratedDefinitionBakingBoundary.ManagedPresentation,
                GASGeneratedDefinitionBakeWritePhase.ManagedPresentation,
                GASGeneratedDefinitionBakeWriteTarget.ManagedPresentationBridge,
                writes);
            AppendDeferredBoundaryWrite(
                entry,
                GASGeneratedDefinitionBakingBoundary.GameplayEffectCacheLifecycleOwner,
                GASGeneratedDefinitionBakeWritePhase.RuntimeDefinitionCache,
                GASGeneratedDefinitionBakeWriteTarget.GameplayEffectCacheLifecycleOwner,
                writes);
        }

        private static void AppendDeferredBoundaryWrite(
            GASGeneratedDefinitionBakingEntry entry,
            GASGeneratedDefinitionBakingBoundary boundary,
            GASGeneratedDefinitionBakeWritePhase writePhase,
            GASGeneratedDefinitionBakeWriteTarget writeTarget,
            List<GASGeneratedDefinitionBakeWrite> writes)
        {
            if (!entry.HasDeferredBoundary(boundary))
                return;

            writes.Add(new GASGeneratedDefinitionBakeWrite(
                entry.DefinitionKind,
                entry.DefinitionCode,
                GASGeneratedDefinitionBakeWriteKind.DeferredBoundary,
                writePhase,
                writeTarget,
                boundary,
                false));
        }
    }
}
