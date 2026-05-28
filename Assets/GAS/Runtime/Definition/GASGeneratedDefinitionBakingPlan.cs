using System;
using System.Collections.Generic;

namespace GAS.Runtime
{
    [Flags]
    public enum GASGeneratedDefinitionBakingCapability
    {
        None = 0,
        GeneratedCarrier = 1 << 0,
        BakerInput = 1 << 1,
        StaticDefinitionBlob = 1 << 2,
    }

    [Flags]
    public enum GASGeneratedDefinitionBakingBoundary
    {
        None = 0,
        SourceHasErrors = 1 << 0,
        RuntimeLifecycle = 1 << 1,
        ManagedPresentation = 1 << 3,
        GameplayEffectCacheLifecycleOwner = 1 << 4,
        MissingGameplayEffectConfigProvider = 1 << 5,
    }

    public readonly struct GASGeneratedDefinitionBakingEntry
    {
        public readonly GASDefinitionKind DefinitionKind;
        public readonly int DefinitionCode;
        public readonly GASGeneratedDefinitionBakingCapability Capabilities;
        public readonly GASGeneratedDefinitionBakingBoundary DeferredBoundaries;
        public readonly int ReferencedDefinitionCount;

        public GASGeneratedDefinitionBakingEntry(
            GASDefinitionKind definitionKind,
            int definitionCode,
            GASGeneratedDefinitionBakingCapability capabilities,
            GASGeneratedDefinitionBakingBoundary deferredBoundaries,
            int referencedDefinitionCount)
        {
            DefinitionKind = definitionKind;
            DefinitionCode = definitionCode;
            Capabilities = capabilities;
            DeferredBoundaries = deferredBoundaries;
            ReferencedDefinitionCount = referencedDefinitionCount;
        }

        public bool HasCapability(GASGeneratedDefinitionBakingCapability capability)
        {
            return capability != GASGeneratedDefinitionBakingCapability.None
                   && (Capabilities & capability) == capability;
        }

        public bool HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary boundary)
        {
            return boundary != GASGeneratedDefinitionBakingBoundary.None
                   && (DeferredBoundaries & boundary) == boundary;
        }

        public bool HasDeferredBoundaries => DeferredBoundaries != GASGeneratedDefinitionBakingBoundary.None;
    }

    public readonly struct GASGeneratedDefinitionBakingPlan
    {
        private readonly GASGeneratedDefinitionBakingEntry[] _entries;

        public readonly GASGeneratedDefinitionBakingBoundary BlockingBoundaries;
        public readonly GASGeneratedDefinitionBakingBoundary DeferredBoundaries;
        public readonly int RegistryDiagnosticCount;
        public readonly int ValidationDiagnosticCount;
        public readonly GameplayEffectDefinitionLifecycleOwnerKind GameplayEffectLifecycleOwnerKind;
        public readonly bool HasGameplayEffectConfigProvider;
        public readonly int GameplayEffectCacheGeneration;
        public readonly int CachedGameplayEffectPrototypeCount;
        public readonly int CachedGameplayEffectStaticDefinitionBlobCount;

        public GASGeneratedDefinitionBakingPlan(
            IEnumerable<GASGeneratedDefinitionBakingEntry> entries,
            GASGeneratedDefinitionBakingBoundary blockingBoundaries,
            GASGeneratedDefinitionBakingBoundary deferredBoundaries,
            int registryDiagnosticCount,
            int validationDiagnosticCount,
            GameplayEffectDefinitionLifecycleOwnerKind gameplayEffectLifecycleOwnerKind,
            bool hasGameplayEffectConfigProvider,
            int gameplayEffectCacheGeneration,
            int cachedGameplayEffectPrototypeCount,
            int cachedGameplayEffectStaticDefinitionBlobCount)
        {
            _entries = entries is GASGeneratedDefinitionBakingEntry[] array
                ? (GASGeneratedDefinitionBakingEntry[])array.Clone()
                : new List<GASGeneratedDefinitionBakingEntry>(entries ?? Array.Empty<GASGeneratedDefinitionBakingEntry>()).ToArray();
            BlockingBoundaries = blockingBoundaries;
            DeferredBoundaries = deferredBoundaries;
            RegistryDiagnosticCount = registryDiagnosticCount;
            ValidationDiagnosticCount = validationDiagnosticCount;
            GameplayEffectLifecycleOwnerKind = gameplayEffectLifecycleOwnerKind;
            HasGameplayEffectConfigProvider = hasGameplayEffectConfigProvider;
            GameplayEffectCacheGeneration = gameplayEffectCacheGeneration;
            CachedGameplayEffectPrototypeCount = cachedGameplayEffectPrototypeCount;
            CachedGameplayEffectStaticDefinitionBlobCount = cachedGameplayEffectStaticDefinitionBlobCount;
        }

        public static GASGeneratedDefinitionBakingPlan Empty =>
            new(
                Array.Empty<GASGeneratedDefinitionBakingEntry>(),
                GASGeneratedDefinitionBakingBoundary.None,
                GASGeneratedDefinitionBakingBoundary.None,
                0,
                0,
                default,
                false,
                0,
                0,
                0);

        public IReadOnlyList<GASGeneratedDefinitionBakingEntry> Entries => _entries ?? Array.Empty<GASGeneratedDefinitionBakingEntry>();
        public int EntryCount => Entries.Count;
        public int TotalDiagnosticCount => RegistryDiagnosticCount + ValidationDiagnosticCount;
        public bool HasBlockingErrors => BlockingBoundaries != GASGeneratedDefinitionBakingBoundary.None;
        public bool CanBake => !HasBlockingErrors;
        public GASGeneratedDefinitionBakingBoundary AllBoundaries => BlockingBoundaries | DeferredBoundaries;

        public int GeneratedCarrierCandidateCount => CountEntriesWithCapability(GASGeneratedDefinitionBakingCapability.GeneratedCarrier);
        public int StaticDefinitionBlobCandidateCount => CountEntriesWithCapability(GASGeneratedDefinitionBakingCapability.StaticDefinitionBlob);
        public int BakerInputCandidateCount => CountEntriesWithCapability(GASGeneratedDefinitionBakingCapability.BakerInput);
        public int EligibleBakerInputCount => CanBake ? BakerInputCandidateCount : 0;
        public int RuntimeLifecycleDeferredEntryCount => CountEntriesWithBoundary(GASGeneratedDefinitionBakingBoundary.RuntimeLifecycle);
        public int ManagedPresentationDeferredEntryCount => CountEntriesWithBoundary(GASGeneratedDefinitionBakingBoundary.ManagedPresentation);

        public bool HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary boundary)
        {
            return boundary != GASGeneratedDefinitionBakingBoundary.None
                   && (DeferredBoundaries & boundary) == boundary;
        }

        public bool HasBlockingBoundary(GASGeneratedDefinitionBakingBoundary boundary)
        {
            return boundary != GASGeneratedDefinitionBakingBoundary.None
                   && (BlockingBoundaries & boundary) == boundary;
        }

        private int CountEntriesWithCapability(GASGeneratedDefinitionBakingCapability capability)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
                if (entries[i].HasCapability(capability))
                    count++;
            return count;
        }

        private int CountEntriesWithBoundary(GASGeneratedDefinitionBakingBoundary boundary)
        {
            var count = 0;
            var entries = Entries;
            for (var i = 0; i < entries.Count; i++)
                if (entries[i].HasDeferredBoundary(boundary))
                    count++;
            return count;
        }
    }

    public static class GASGeneratedDefinitionBakingPlanner
    {
        public static GASGeneratedDefinitionBakingPlan Create(
            GASGeneratedDefinitionBuildResult buildResult,
            GameplayEffectDefinitionCacheState gameplayEffectCacheState)
        {
            var entries = new List<GASGeneratedDefinitionBakingEntry>();
            var deferredBoundaries = GASGeneratedDefinitionBakingBoundary.None;
            var blockingBoundaries = buildResult.HasErrors
                ? GASGeneratedDefinitionBakingBoundary.SourceHasErrors
                : GASGeneratedDefinitionBakingBoundary.None;

            var table = buildResult.DefinitionTable;
            AppendAbilities(table.Abilities, entries, ref deferredBoundaries);
            AppendGameplayEffects(table.GameplayEffects, entries, ref deferredBoundaries);
            AppendAttributeSets(table.AttributeSets, entries);
            AppendAttributes(table.Attributes, entries);
            AppendGameplayTags(table.GameplayTags, entries);
            AppendGameplayCues(table.GameplayCues, entries, ref deferredBoundaries);

            if (table.GameplayEffects.Count > 0)
            {
                deferredBoundaries |= GASGeneratedDefinitionBakingBoundary.GameplayEffectCacheLifecycleOwner;
                if (!gameplayEffectCacheState.HasConfigProvider)
                    deferredBoundaries |= GASGeneratedDefinitionBakingBoundary.MissingGameplayEffectConfigProvider;
            }

            return new GASGeneratedDefinitionBakingPlan(
                entries,
                blockingBoundaries,
                deferredBoundaries,
                buildResult.RegistryDiagnosticCount,
                buildResult.ValidationDiagnosticCount,
                gameplayEffectCacheState.OwnerKind,
                gameplayEffectCacheState.HasConfigProvider,
                gameplayEffectCacheState.Generation,
                gameplayEffectCacheState.CachedPrototypeCount,
                gameplayEffectCacheState.CachedStaticDefinitionBlobCount);
        }

        private static void AppendAbilities(
            IReadOnlyList<AbilityDefinitionSummary> abilities,
            List<GASGeneratedDefinitionBakingEntry> entries,
            ref GASGeneratedDefinitionBakingBoundary planDeferredBoundaries)
        {
            for (var i = 0; i < abilities.Count; i++)
            {
                var ability = abilities[i];
                var deferredBoundaries = GASGeneratedDefinitionBakingBoundary.RuntimeLifecycle;

                planDeferredBoundaries |= deferredBoundaries;
                entries.Add(new GASGeneratedDefinitionBakingEntry(
                    GASDefinitionKind.Ability,
                    ability.AbilityCode,
                    GASGeneratedDefinitionBakingCapability.GeneratedCarrier
                    | GASGeneratedDefinitionBakingCapability.BakerInput,
                    deferredBoundaries,
                    CountAbilityReferences(ability)));
            }
        }

        private static void AppendGameplayEffects(
            IReadOnlyList<GameplayEffectDefinitionSummary> gameplayEffects,
            List<GASGeneratedDefinitionBakingEntry> entries,
            ref GASGeneratedDefinitionBakingBoundary planDeferredBoundaries)
        {
            for (var i = 0; i < gameplayEffects.Count; i++)
            {
                var gameplayEffect = gameplayEffects[i];
                var deferredBoundaries =
                    GASGeneratedDefinitionBakingBoundary.RuntimeLifecycle
                    | GASGeneratedDefinitionBakingBoundary.GameplayEffectCacheLifecycleOwner;
                if (gameplayEffect.HasManagedCueTriggers)
                    deferredBoundaries |= GASGeneratedDefinitionBakingBoundary.ManagedPresentation;

                planDeferredBoundaries |= deferredBoundaries;
                entries.Add(new GASGeneratedDefinitionBakingEntry(
                    GASDefinitionKind.GameplayEffect,
                    gameplayEffect.GameplayEffectCode,
                    GASGeneratedDefinitionBakingCapability.GeneratedCarrier
                    | GASGeneratedDefinitionBakingCapability.BakerInput
                    | GASGeneratedDefinitionBakingCapability.StaticDefinitionBlob,
                    deferredBoundaries,
                    gameplayEffect.PeriodEffectCount
                    + gameplayEffect.OverflowEffectCount
                    + gameplayEffect.GrantedAbilityCount
                    + gameplayEffect.CueTriggerCount));
            }
        }

        private static void AppendAttributeSets(
            IReadOnlyList<AttributeSetDefinitionSummary> attributeSets,
            List<GASGeneratedDefinitionBakingEntry> entries)
        {
            for (var i = 0; i < attributeSets.Count; i++)
            {
                entries.Add(new GASGeneratedDefinitionBakingEntry(
                    GASDefinitionKind.AttributeSet,
                    attributeSets[i].AttrSetCode,
                    GASGeneratedDefinitionBakingCapability.GeneratedCarrier
                    | GASGeneratedDefinitionBakingCapability.BakerInput,
                    GASGeneratedDefinitionBakingBoundary.None,
                    attributeSets[i].AttributeCount));
            }
        }

        private static void AppendAttributes(
            IReadOnlyList<AttributeDefinitionSummary> attributes,
            List<GASGeneratedDefinitionBakingEntry> entries)
        {
            for (var i = 0; i < attributes.Count; i++)
            {
                entries.Add(new GASGeneratedDefinitionBakingEntry(
                    GASDefinitionKind.Attribute,
                    attributes[i].AttributeCode,
                    GASGeneratedDefinitionBakingCapability.GeneratedCarrier
                    | GASGeneratedDefinitionBakingCapability.BakerInput,
                    GASGeneratedDefinitionBakingBoundary.None,
                    attributes[i].HasClamp ? 1 : 0));
            }
        }

        private static void AppendGameplayTags(
            IReadOnlyList<GameplayTagDefinitionSummary> gameplayTags,
            List<GASGeneratedDefinitionBakingEntry> entries)
        {
            for (var i = 0; i < gameplayTags.Count; i++)
            {
                entries.Add(new GASGeneratedDefinitionBakingEntry(
                    GASDefinitionKind.GameplayTag,
                    gameplayTags[i].TagCode,
                    GASGeneratedDefinitionBakingCapability.GeneratedCarrier
                    | GASGeneratedDefinitionBakingCapability.BakerInput,
                    GASGeneratedDefinitionBakingBoundary.None,
                    gameplayTags[i].ParentCount + gameplayTags[i].ChildCount));
            }
        }

        private static void AppendGameplayCues(
            IReadOnlyList<GameplayCueDefinitionSummary> gameplayCues,
            List<GASGeneratedDefinitionBakingEntry> entries,
            ref GASGeneratedDefinitionBakingBoundary planDeferredBoundaries)
        {
            for (var i = 0; i < gameplayCues.Count; i++)
            {
                var cue = gameplayCues[i];
                var deferredBoundaries = cue.UsesManagedPresentationFactory
                    ? GASGeneratedDefinitionBakingBoundary.ManagedPresentation
                    : GASGeneratedDefinitionBakingBoundary.None;

                planDeferredBoundaries |= deferredBoundaries;
                entries.Add(new GASGeneratedDefinitionBakingEntry(
                    GASDefinitionKind.GameplayCue,
                    cue.CueCode,
                    GASGeneratedDefinitionBakingCapability.GeneratedCarrier
                    | GASGeneratedDefinitionBakingCapability.BakerInput,
                    deferredBoundaries,
                    0));
            }
        }

        private static int CountAbilityReferences(AbilityDefinitionSummary ability)
        {
            var count = ability.ActivationEffectCount;
            if (ability.HasCost)
                count++;
            if (ability.HasCooldown)
                count++;
            return count;
        }
    }
}
