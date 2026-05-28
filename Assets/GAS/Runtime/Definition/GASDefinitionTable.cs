using System;
using System.Collections.Generic;
using Unity.Entities;

namespace GAS.Runtime
{
    public enum GASDefinitionKind
    {
        None = 0,
        Ability = 1,
        GameplayEffect = 2,
        AttributeSet = 3,
        Attribute = 4,
        GameplayTag = 5,
        GameplayCue = 6,
    }

    public readonly struct GASDefinitionKey
    {
        public readonly GASDefinitionKind Kind;
        public readonly int Code;

        public GASDefinitionKey(GASDefinitionKind kind, int code)
        {
            Kind = kind;
            Code = code;
        }

        public bool IsValid => Kind != GASDefinitionKind.None && Code > 0;
    }

    public interface IGASDefinitionAbilityActivationOwnedTagsProvider
    {
        bool TryGetDefinitionActivationOwnedTags(out TagMaskComponent tags);
    }

    public interface IGASDefinitionGameplayEffectGrantedTagsProvider
    {
        bool TryGetDefinitionGrantedTags(out TagMaskComponent tags);
    }

    public interface IGASDefinitionGameplayEffectRemoveTagsProvider
    {
        bool TryGetDefinitionRemoveGameplayEffectsWithTags(out TagRequirementMask requirement);
    }

    public readonly struct AbilityDefinitionSummary
    {
        public readonly int AbilityCode;
        public readonly int Level;
        public readonly TagMaskComponent AssetTags;
        public readonly TagRequirementMask ActivationRequiredTags;
        public readonly TagRequirementMask ActivationBlockedTags;
        public readonly TagMaskComponent ActivationOwnedTags;
        public readonly TagMaskComponent CancelAbilityTags;
        public readonly TagMaskComponent BlockAbilityTags;
        public readonly int CostGameplayEffectCode;
        public readonly int CooldownGameplayEffectCode;
        public readonly int Cooldown;
        public readonly int ActivationEffectCount;
        public readonly bool HasAssetTags;
        public readonly bool HasActivationRequiredTags;
        public readonly bool HasActivationBlockedTags;
        public readonly bool HasActivationOwnedTags;
        public readonly bool HasCancelAbilityTags;
        public readonly bool HasBlockAbilityTags;

        public AbilityDefinitionSummary(
            int abilityCode,
            int level,
            TagMaskComponent assetTags,
            TagRequirementMask activationRequiredTags,
            TagRequirementMask activationBlockedTags,
            TagMaskComponent activationOwnedTags,
            TagMaskComponent cancelAbilityTags,
            TagMaskComponent blockAbilityTags,
            int costGameplayEffectCode,
            int cooldownGameplayEffectCode,
            int cooldown,
            int activationEffectCount,
            bool hasAssetTags,
            bool hasActivationRequiredTags,
            bool hasActivationBlockedTags,
            bool hasActivationOwnedTags,
            bool hasCancelAbilityTags,
            bool hasBlockAbilityTags)
        {
            AbilityCode = abilityCode;
            Level = level;
            AssetTags = assetTags;
            ActivationRequiredTags = activationRequiredTags;
            ActivationBlockedTags = activationBlockedTags;
            ActivationOwnedTags = activationOwnedTags;
            CancelAbilityTags = cancelAbilityTags;
            BlockAbilityTags = blockAbilityTags;
            CostGameplayEffectCode = costGameplayEffectCode;
            CooldownGameplayEffectCode = cooldownGameplayEffectCode;
            Cooldown = cooldown;
            ActivationEffectCount = activationEffectCount;
            HasAssetTags = hasAssetTags;
            HasActivationRequiredTags = hasActivationRequiredTags;
            HasActivationBlockedTags = hasActivationBlockedTags;
            HasActivationOwnedTags = hasActivationOwnedTags;
            HasCancelAbilityTags = hasCancelAbilityTags;
            HasBlockAbilityTags = hasBlockAbilityTags;
        }

        public bool HasCost => CostGameplayEffectCode > 0;
        public bool HasCooldown => CooldownGameplayEffectCode > 0 || Cooldown > 0;
        public bool HasActivationEffects => ActivationEffectCount > 0;
    }

    public readonly struct GameplayEffectDefinitionSummary
    {
        public readonly int GameplayEffectCode;
        public readonly bool HasDuration;
        public readonly GEDurationDefinition Duration;
        public readonly bool HasPeriod;
        public readonly GEPeriodDefinition Period;
        public readonly int PeriodEffectCount;
        public readonly bool HasStacking;
        public readonly GEStackingDefinition Stacking;
        public readonly int OverflowEffectCount;
        public readonly TagMaskComponent AssetTags;
        public readonly TagMaskComponent GrantedTags;
        public readonly bool HasAssetTags;
        public readonly bool HasGrantedTags;
        public readonly bool HasApplicationRequiredTags;
        public readonly TagRequirementMask ApplicationRequiredTags;
        public readonly bool HasOngoingRequiredTags;
        public readonly TagRequirementMask OngoingRequiredTags;
        public readonly bool HasRemoveGameplayEffectsWithTags;
        public readonly TagRequirementMask RemoveGameplayEffectsWithTags;
        public readonly bool HasImmunityTags;
        public readonly TagRequirementMask ImmunityTags;
        public readonly int ModifierCount;
        public readonly int GrantedAbilityCount;
        public readonly int CueTriggerCount;
        public readonly bool HasManagedCueTriggers;

        public GameplayEffectDefinitionSummary(
            int gameplayEffectCode,
            bool hasDuration,
            GEDurationDefinition duration,
            bool hasPeriod,
            GEPeriodDefinition period,
            int periodEffectCount,
            bool hasStacking,
            GEStackingDefinition stacking,
            int overflowEffectCount,
            TagMaskComponent assetTags,
            TagMaskComponent grantedTags,
            bool hasAssetTags,
            bool hasGrantedTags,
            bool hasApplicationRequiredTags,
            TagRequirementMask applicationRequiredTags,
            bool hasOngoingRequiredTags,
            TagRequirementMask ongoingRequiredTags,
            bool hasRemoveGameplayEffectsWithTags,
            TagRequirementMask removeGameplayEffectsWithTags,
            bool hasImmunityTags,
            TagRequirementMask immunityTags,
            int modifierCount,
            int grantedAbilityCount,
            int cueTriggerCount,
            bool hasManagedCueTriggers)
        {
            GameplayEffectCode = gameplayEffectCode;
            HasDuration = hasDuration;
            Duration = duration;
            HasPeriod = hasPeriod;
            Period = period;
            PeriodEffectCount = periodEffectCount;
            HasStacking = hasStacking;
            Stacking = stacking;
            OverflowEffectCount = overflowEffectCount;
            AssetTags = assetTags;
            GrantedTags = grantedTags;
            HasAssetTags = hasAssetTags;
            HasGrantedTags = hasGrantedTags;
            HasApplicationRequiredTags = hasApplicationRequiredTags;
            ApplicationRequiredTags = applicationRequiredTags;
            HasOngoingRequiredTags = hasOngoingRequiredTags;
            OngoingRequiredTags = ongoingRequiredTags;
            HasRemoveGameplayEffectsWithTags = hasRemoveGameplayEffectsWithTags;
            RemoveGameplayEffectsWithTags = removeGameplayEffectsWithTags;
            HasImmunityTags = hasImmunityTags;
            ImmunityTags = immunityTags;
            ModifierCount = modifierCount;
            GrantedAbilityCount = grantedAbilityCount;
            CueTriggerCount = cueTriggerCount;
            HasManagedCueTriggers = hasManagedCueTriggers;
        }
    }

    public readonly struct AttributeDefinitionSummary
    {
        public readonly int AttrSetCode;
        public readonly int AttributeCode;
        public readonly float InitialValue;
        public readonly float MinValue;
        public readonly float MaxValue;
        public readonly bool IsClampMin;
        public readonly bool IsClampMax;

        public AttributeDefinitionSummary(
            int attrSetCode,
            int attributeCode,
            float initialValue,
            bool isClampMin,
            bool isClampMax,
            float minValue,
            float maxValue)
        {
            AttrSetCode = attrSetCode;
            AttributeCode = attributeCode;
            InitialValue = initialValue;
            IsClampMin = isClampMin;
            IsClampMax = isClampMax;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public bool HasClamp => IsClampMin || IsClampMax;
    }

    public readonly struct AttributeSetDefinitionSummary
    {
        public readonly int AttrSetCode;
        public readonly int AttributeCount;
        public readonly bool HasClampedAttributes;

        public AttributeSetDefinitionSummary(
            int attrSetCode,
            int attributeCount,
            bool hasClampedAttributes)
        {
            AttrSetCode = attrSetCode;
            AttributeCount = attributeCount;
            HasClampedAttributes = hasClampedAttributes;
        }
    }

    public readonly struct GameplayTagDefinitionSummary
    {
        public readonly int TagCode;
        public readonly int[] ParentCodes;
        public readonly int[] ChildCodes;

        public GameplayTagDefinitionSummary(int tagCode, int[] parentCodes, int[] childCodes)
        {
            TagCode = tagCode;
            ParentCodes = parentCodes ?? Array.Empty<int>();
            ChildCodes = childCodes ?? Array.Empty<int>();
        }

        public int ParentCount => ParentCodes.Length;
        public int ChildCount => ChildCodes.Length;
        public bool IsRoot => ParentCount == 0;
        public bool HasChildren => ChildCount > 0;
    }

    public readonly struct GameplayCueDefinitionSummary
    {
        public readonly int CueCode;
        public readonly TagRequirementMask RequiredTags;
        public readonly TagRequirementMask ImmunityTags;
        public readonly bool HasRequiredTags;
        public readonly bool HasImmunityTags;
        public readonly bool UsesManagedPresentationFactory;

        public GameplayCueDefinitionSummary(
            int cueCode,
            TagRequirementMask requiredTags,
            TagRequirementMask immunityTags,
            bool hasRequiredTags,
            bool hasImmunityTags,
            bool usesManagedPresentationFactory)
        {
            CueCode = cueCode;
            RequiredTags = requiredTags;
            ImmunityTags = immunityTags;
            HasRequiredTags = hasRequiredTags;
            HasImmunityTags = hasImmunityTags;
            UsesManagedPresentationFactory = usesManagedPresentationFactory;
        }
    }

    public readonly struct GASDefinitionTable
    {
        private readonly AbilityDefinitionSummary[] _abilities;
        private readonly GameplayEffectDefinitionSummary[] _gameplayEffects;
        private readonly AttributeSetDefinitionSummary[] _attributeSets;
        private readonly AttributeDefinitionSummary[] _attributes;
        private readonly GameplayTagDefinitionSummary[] _gameplayTags;
        private readonly GameplayCueDefinitionSummary[] _gameplayCues;

        public GASDefinitionTable(
            AbilityDefinitionSummary[] abilities,
            GameplayEffectDefinitionSummary[] gameplayEffects,
            AttributeSetDefinitionSummary[] attributeSets,
            AttributeDefinitionSummary[] attributes,
            GameplayTagDefinitionSummary[] gameplayTags,
            GameplayCueDefinitionSummary[] gameplayCues)
        {
            _abilities = abilities ?? Array.Empty<AbilityDefinitionSummary>();
            _gameplayEffects = gameplayEffects ?? Array.Empty<GameplayEffectDefinitionSummary>();
            _attributeSets = attributeSets ?? Array.Empty<AttributeSetDefinitionSummary>();
            _attributes = attributes ?? Array.Empty<AttributeDefinitionSummary>();
            _gameplayTags = gameplayTags ?? Array.Empty<GameplayTagDefinitionSummary>();
            _gameplayCues = gameplayCues ?? Array.Empty<GameplayCueDefinitionSummary>();
        }

        public static GASDefinitionTable Empty =>
            new(
                Array.Empty<AbilityDefinitionSummary>(),
                Array.Empty<GameplayEffectDefinitionSummary>(),
                Array.Empty<AttributeSetDefinitionSummary>(),
                Array.Empty<AttributeDefinitionSummary>(),
                Array.Empty<GameplayTagDefinitionSummary>(),
                Array.Empty<GameplayCueDefinitionSummary>());

        public IReadOnlyList<AbilityDefinitionSummary> Abilities => _abilities;
        public IReadOnlyList<GameplayEffectDefinitionSummary> GameplayEffects => _gameplayEffects;
        public IReadOnlyList<AttributeSetDefinitionSummary> AttributeSets => _attributeSets;
        public IReadOnlyList<AttributeDefinitionSummary> Attributes => _attributes;
        public IReadOnlyList<GameplayTagDefinitionSummary> GameplayTags => _gameplayTags;
        public IReadOnlyList<GameplayCueDefinitionSummary> GameplayCues => _gameplayCues;

        public int TotalDefinitionCount =>
            _abilities.Length
            + _gameplayEffects.Length
            + _attributeSets.Length
            + _attributes.Length
            + _gameplayTags.Length
            + _gameplayCues.Length;

        public bool TryGetAbility(int abilityCode, out AbilityDefinitionSummary summary)
        {
            for (var i = 0; i < _abilities.Length; i++)
            {
                if (_abilities[i].AbilityCode != abilityCode)
                    continue;

                summary = _abilities[i];
                return true;
            }

            summary = default;
            return false;
        }

        public bool TryGetGameplayEffect(int gameplayEffectCode, out GameplayEffectDefinitionSummary summary)
        {
            for (var i = 0; i < _gameplayEffects.Length; i++)
            {
                if (_gameplayEffects[i].GameplayEffectCode != gameplayEffectCode)
                    continue;

                summary = _gameplayEffects[i];
                return true;
            }

            summary = default;
            return false;
        }

        public bool TryGetAttributeSet(int attrSetCode, out AttributeSetDefinitionSummary summary)
        {
            for (var i = 0; i < _attributeSets.Length; i++)
            {
                if (_attributeSets[i].AttrSetCode != attrSetCode)
                    continue;

                summary = _attributeSets[i];
                return true;
            }

            summary = default;
            return false;
        }

        public bool TryGetAttribute(
            int attrSetCode,
            int attributeCode,
            out AttributeDefinitionSummary summary)
        {
            for (var i = 0; i < _attributes.Length; i++)
            {
                if (_attributes[i].AttrSetCode != attrSetCode
                    || _attributes[i].AttributeCode != attributeCode)
                    continue;

                summary = _attributes[i];
                return true;
            }

            summary = default;
            return false;
        }

        public bool TryGetGameplayTag(int tagCode, out GameplayTagDefinitionSummary summary)
        {
            for (var i = 0; i < _gameplayTags.Length; i++)
            {
                if (_gameplayTags[i].TagCode != tagCode)
                    continue;

                summary = _gameplayTags[i];
                return true;
            }

            summary = default;
            return false;
        }

        public bool TryGetGameplayCue(int cueCode, out GameplayCueDefinitionSummary summary)
        {
            for (var i = 0; i < _gameplayCues.Length; i++)
            {
                if (_gameplayCues[i].CueCode != cueCode)
                    continue;

                summary = _gameplayCues[i];
                return true;
            }

            summary = default;
            return false;
        }
    }

    public static class GASDefinitionSummaryBuilder
    {
        public static AbilityDefinitionSummary FromAbilityConfig(int abilityCode, AbilityConfig config)
        {
            var resolvedCode = abilityCode;
            var level = 0;
            var assetTags = default(TagMaskComponent);
            var activationRequiredTags = default(TagRequirementMask);
            var activationBlockedTags = default(TagRequirementMask);
            var activationOwnedTags = default(TagMaskComponent);
            var cancelAbilityTags = default(TagMaskComponent);
            var blockAbilityTags = default(TagMaskComponent);
            var costGameplayEffectCode = 0;
            var cooldownGameplayEffectCode = 0;
            var cooldown = 0;
            var activationEffectCount = 0;
            var hasAssetTags = false;
            var hasActivationRequiredTags = false;
            var hasActivationBlockedTags = false;
            var hasActivationOwnedTags = false;
            var hasCancelAbilityTags = false;
            var hasBlockAbilityTags = false;

            var configs = config?.ComponentConfigs ?? Array.Empty<AbilityComponentConfig>();
            for (var i = 0; i < configs.Length; i++)
            {
                switch (configs[i])
                {
                    case ConfAbilityBaseInfo baseInfo:
                        if (resolvedCode <= 0)
                            resolvedCode = baseInfo.Code;
                        level = baseInfo.Level;
                        break;
                    case ConfAbilityAssetTags tags:
                        hasAssetTags = HasAny(tags.tags);
                        assetTags = TagHelper.BuildMask(tags.tags, includeParents: true);
                        break;
                    case ConfAbilityActivationRequiredTags requiredTags:
                        hasActivationRequiredTags = HasAnyRequirement(requiredTags.all ?? requiredTags.tags, requiredTags.any, requiredTags.none);
                        activationRequiredTags = TagHelper.BuildRequirementMask(
                            requiredTags.all ?? requiredTags.tags,
                            requiredTags.any,
                            requiredTags.none);
                        break;
                    case ConfAbilityActivationBlockedTags blockedTags:
                        hasActivationBlockedTags = HasAnyRequirement(blockedTags.all, blockedTags.any, blockedTags.none ?? blockedTags.tags);
                        activationBlockedTags = TagHelper.BuildRequirementMask(
                            blockedTags.all,
                            blockedTags.any,
                            blockedTags.none ?? blockedTags.tags);
                        break;
                    case ConfAbilityActivationOwnedTags ownedTags:
                        hasActivationOwnedTags = HasAny(ownedTags.tags);
                        activationOwnedTags = TagHelper.BuildMask(ownedTags.tags, includeParents: true);
                        break;
                    case IGASDefinitionAbilityActivationOwnedTagsProvider ownedTagsProvider:
                        if (ownedTagsProvider.TryGetDefinitionActivationOwnedTags(out var providedOwnedTags))
                        {
                            hasActivationOwnedTags = !providedOwnedTags.IsEmpty;
                            activationOwnedTags = providedOwnedTags;
                        }
                        break;
                    case ConfCancelAbilityWithTags cancelTags:
                        hasCancelAbilityTags = HasAny(cancelTags.tags);
                        cancelAbilityTags = TagHelper.BuildMask(cancelTags.tags, includeParents: false);
                        break;
                    case ConfBlockAbilityWithTags blockTags:
                        hasBlockAbilityTags = HasAny(blockTags.tags);
                        blockAbilityTags = TagHelper.BuildMask(blockTags.tags, includeParents: false);
                        break;
                    case ConfAbilityCost cost:
                        costGameplayEffectCode = cost.GameplayEffectCode;
                        break;
                    case ConfAbilityCooldown cooldownConfig:
                        cooldown = cooldownConfig.Cooldown;
                        cooldownGameplayEffectCode = cooldownConfig.GameplayEffectCode;
                        break;
                    case ConfAbilityEffectsOnActivate activationEffects:
                        activationEffectCount += CountPositive(activationEffects.EffectCodes);
                        break;
                }
            }

            return new AbilityDefinitionSummary(
                resolvedCode,
                level,
                assetTags,
                activationRequiredTags,
                activationBlockedTags,
                activationOwnedTags,
                cancelAbilityTags,
                blockAbilityTags,
                costGameplayEffectCode,
                cooldownGameplayEffectCode,
                cooldown,
                activationEffectCount,
                hasAssetTags,
                hasActivationRequiredTags,
                hasActivationBlockedTags,
                hasActivationOwnedTags,
                hasCancelAbilityTags,
                hasBlockAbilityTags);
        }

        public static GameplayEffectDefinitionSummary FromGameplayEffectConfig(
            int gameplayEffectCode,
            GameplayEffectConfig config)
        {
            var hasDuration = false;
            var duration = default(GEDurationDefinition);
            var hasPeriod = false;
            var period = default(GEPeriodDefinition);
            var periodEffectCount = 0;
            var hasStacking = false;
            var stacking = default(GEStackingDefinition);
            var overflowEffectCount = 0;
            var assetTags = default(TagMaskComponent);
            var grantedTags = default(TagMaskComponent);
            var hasAssetTags = false;
            var hasGrantedTags = false;
            var hasApplicationRequiredTags = false;
            var applicationRequiredTags = default(TagRequirementMask);
            var hasOngoingRequiredTags = false;
            var ongoingRequiredTags = default(TagRequirementMask);
            var hasRemoveGameplayEffectsWithTags = false;
            var removeGameplayEffectsWithTags = default(TagRequirementMask);
            var hasImmunityTags = false;
            var immunityTags = default(TagRequirementMask);
            var modifierCount = 0;
            var grantedAbilityCount = 0;
            var cueTriggerCount = 0;
            var hasManagedCueTriggers = false;

            var configs = config?.ComponentConfigs ?? Array.Empty<GameplayEffectComponentConfig>();
            for (var i = 0; i < configs.Length; i++)
            {
                switch (configs[i])
                {
                    case ConfDuration durationConfig:
                        hasDuration = true;
                        duration = new GEDurationDefinition
                        {
                            Duration = durationConfig.duration,
                            TimeUnit = durationConfig.timeUnit,
                            ResetStartTimeWhenActivated = durationConfig.ResetStartTimeWhenActivated,
                            StopTickWhenDeactivated = durationConfig.StopTickWhenDeactivated,
                        };
                        break;
                    case ConfPeriod periodConfig:
                        hasPeriod = true;
                        period = new GEPeriodDefinition
                        {
                            Period = periodConfig.Period,
                            ResetTimeCountWhenDeactivated = periodConfig.ResetTimeCountWhenDeactivated,
                        };
                        periodEffectCount += CountPositive(periodConfig.GameplayEffectCodes);
                        break;
                    case ConfStacking stackingConfig:
                        hasStacking = true;
                        stacking = new GEStackingDefinition
                        {
                            StackType = stackingConfig.StackType,
                            StackingCode = stackingConfig.StackingCode,
                            LimitCount = stackingConfig.LimitCount,
                            EffectDurationRefreshPolicy = stackingConfig.EffectDurationRefreshPolicy,
                            EffectPeriodResetPolicy = stackingConfig.EffectPeriodResetPolicy,
                            EffectExpirationPolicy = stackingConfig.EffectExpirationPolicy,
                            DenyOverflowApplication = stackingConfig.denyOverflowApplication,
                            ClearStackOnOverflow = stackingConfig.clearStackOnOverflow,
                        };
                        overflowEffectCount += CountPositive(stackingConfig.OverflowEffectCodes);
                        break;
                    case ConfAssetTags tags:
                        hasAssetTags = HasAny(tags.tags);
                        assetTags = TagHelper.BuildMask(tags.tags, includeParents: true);
                        break;
                    case ConfEffectGrantedTags tags:
                        hasGrantedTags = HasAny(tags.tags);
                        grantedTags = TagHelper.BuildMask(tags.tags, includeParents: true);
                        break;
                    case IGASDefinitionGameplayEffectGrantedTagsProvider grantedTagsProvider:
                        if (grantedTagsProvider.TryGetDefinitionGrantedTags(out var providedGrantedTags))
                        {
                            hasGrantedTags = !providedGrantedTags.IsEmpty;
                            grantedTags = providedGrantedTags;
                        }
                        break;
                    case ConfApplicationRequiredTags requiredTags:
                        hasApplicationRequiredTags = HasAnyRequirement(requiredTags.all ?? requiredTags.tags, requiredTags.any, requiredTags.none);
                        applicationRequiredTags = TagHelper.BuildRequirementMask(
                            requiredTags.all ?? requiredTags.tags,
                            requiredTags.any,
                            requiredTags.none);
                        break;
                    case ConfOngoingRequiredTags requiredTags:
                        hasOngoingRequiredTags = HasAnyRequirement(requiredTags.all ?? requiredTags.tags, requiredTags.any, requiredTags.none);
                        ongoingRequiredTags = TagHelper.BuildRequirementMask(
                            requiredTags.all ?? requiredTags.tags,
                            requiredTags.any,
                            requiredTags.none);
                        break;
                    case ConfRemoveEffectWithTags removeTags:
                        hasRemoveGameplayEffectsWithTags = HasAnyRequirement(removeTags.all, removeTags.any ?? removeTags.tags, removeTags.none);
                        removeGameplayEffectsWithTags = TagHelper.BuildRequirementMask(
                            removeTags.all,
                            removeTags.any ?? removeTags.tags,
                            removeTags.none);
                        break;
                    case IGASDefinitionGameplayEffectRemoveTagsProvider removeTagsProvider:
                        if (removeTagsProvider.TryGetDefinitionRemoveGameplayEffectsWithTags(out var providedRemoveTags))
                        {
                            hasRemoveGameplayEffectsWithTags = !providedRemoveTags.IsEmpty;
                            removeGameplayEffectsWithTags = providedRemoveTags;
                        }
                        break;
                    case ConfEffectImmunityTags immunity:
                        hasImmunityTags = HasAnyRequirement(immunity.all, immunity.any ?? immunity.tags, immunity.none);
                        immunityTags = TagHelper.BuildRequirementMask(
                            immunity.all,
                            immunity.any ?? immunity.tags,
                            immunity.none);
                        break;
                    case ConfModifierConfig modifiers:
                        modifierCount += modifiers.ModifierSettings?.Length ?? 0;
                        break;
                    case ConfGrantedAbilityConfig grantedAbilities:
                        grantedAbilityCount += grantedAbilities.GrantedAbilities?.Length ?? 0;
                        break;
                    case ConfCueBase cueConfig:
                        var count = cueConfig.cues?.Length ?? 0;
                        cueTriggerCount += count;
                        hasManagedCueTriggers |= count > 0;
                        break;
                }
            }

            return new GameplayEffectDefinitionSummary(
                gameplayEffectCode,
                hasDuration,
                duration,
                hasPeriod,
                period,
                periodEffectCount,
                hasStacking,
                stacking,
                overflowEffectCount,
                assetTags,
                grantedTags,
                hasAssetTags,
                hasGrantedTags,
                hasApplicationRequiredTags,
                applicationRequiredTags,
                hasOngoingRequiredTags,
                ongoingRequiredTags,
                hasRemoveGameplayEffectsWithTags,
                removeGameplayEffectsWithTags,
                hasImmunityTags,
                immunityTags,
                modifierCount,
                grantedAbilityCount,
                cueTriggerCount,
                hasManagedCueTriggers);
        }

        public static GameplayEffectDefinitionSummary FromGameplayEffectStaticDefinition(
            BlobAssetReference<GEStaticDefinitionBlob> blob)
        {
            if (!blob.IsCreated)
                return default;

            ref var definition = ref blob.Value;
            return FromGameplayEffectStaticDefinition(ref definition);
        }

        public static GameplayEffectDefinitionSummary FromGameplayEffectStaticDefinition(
            ref GEStaticDefinitionBlob definition)
        {
            return new GameplayEffectDefinitionSummary(
                definition.GameplayEffectCode,
                definition.HasDuration,
                definition.Duration,
                definition.HasPeriod,
                definition.Period,
                definition.PeriodEffectCodes.Length,
                definition.HasStacking,
                definition.Stacking,
                definition.OverflowEffectCodes.Length,
                definition.AssetTags,
                definition.GrantedTags,
                !definition.AssetTags.IsEmpty,
                !definition.GrantedTags.IsEmpty,
                definition.HasApplicationRequiredTags,
                definition.ApplicationRequiredTags,
                definition.HasOngoingRequiredTags,
                definition.OngoingRequiredTags,
                definition.HasRemoveGameplayEffectsWithTags,
                definition.RemoveGameplayEffectsWithTags,
                definition.HasImmunityTags,
                definition.ImmunityTags,
                definition.Modifiers.Length,
                definition.GrantedAbilities.Length,
                0,
                false);
        }

        public static AttributeSetDefinitionSummary FromAttributeSetConfig(AttrSetConfig config)
        {
            var settings = config.Settings ?? Array.Empty<AttributeBaseSetting>();
            var hasClampedAttributes = false;
            for (var i = 0; i < settings.Length; i++)
                hasClampedAttributes |= settings[i].IsClampMin || settings[i].IsClampMax;

            return new AttributeSetDefinitionSummary(
                config.Code,
                settings.Length,
                hasClampedAttributes);
        }

        public static AttributeDefinitionSummary[] FromAttributeSetAttributes(AttrSetConfig config)
        {
            var settings = config.Settings ?? Array.Empty<AttributeBaseSetting>();
            var attributes = new AttributeDefinitionSummary[settings.Length];
            for (var i = 0; i < settings.Length; i++)
            {
                var setting = settings[i];
                attributes[i] = new AttributeDefinitionSummary(
                    config.Code,
                    setting.Code,
                    setting.InitValue,
                    setting.IsClampMin,
                    setting.IsClampMax,
                    setting.Min,
                    setting.Max);
            }

            return attributes;
        }

        public static GameplayTagDefinitionSummary FromGameplayTag(GameplayTag tag)
        {
            return new GameplayTagDefinitionSummary(
                tag.Code,
                tag.Parents ?? Array.Empty<int>(),
                tag.Children ?? Array.Empty<int>());
        }

        public static GameplayCueDefinitionSummary FromGameplayCueConfig(int cueCode, GameplayCueConfig config)
        {
            var requiredTags = TagHelper.BuildRequirementMask(
                config?.RequiredAllTags,
                config?.RequiredAnyTags,
                config?.RequiredNoneTags);
            var immunityTags = TagHelper.BuildRequirementMask(
                config?.ImmunityAllTags,
                config?.ImmunityAnyTags,
                config?.ImmunityNoneTags);

            return new GameplayCueDefinitionSummary(
                cueCode,
                requiredTags,
                immunityTags,
                HasAnyRequirement(config?.RequiredAllTags, config?.RequiredAnyTags, config?.RequiredNoneTags),
                HasAnyRequirement(config?.ImmunityAllTags, config?.ImmunityAnyTags, config?.ImmunityNoneTags),
                config?.CueType != null || config?.Param != null);
        }

        public static GASDefinitionTable BuildFromRegistries(
            IEnumerable<int> abilityCodes,
            IEnumerable<int> gameplayEffectCodes,
            IEnumerable<AttrSetConfig> attributeSetConfigs,
            IEnumerable<GameplayTag> gameplayTags,
            IEnumerable<int> gameplayCueCodes)
        {
            var abilities = new List<AbilityDefinitionSummary>();
            var effects = new List<GameplayEffectDefinitionSummary>();
            var attributeSets = new List<AttributeSetDefinitionSummary>();
            var attributes = new List<AttributeDefinitionSummary>();
            var tags = new List<GameplayTagDefinitionSummary>();
            var cues = new List<GameplayCueDefinitionSummary>();

            foreach (var abilityCode in EnumerateUniquePositiveCodes(abilityCodes))
            {
                var config = AbilityConfigRegistry.GetConfigByID(
                    abilityCode,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.None,
                        0,
                        ConfigRegistryReferenceKind.Direct));
                if (config != null)
                    abilities.Add(FromAbilityConfig(abilityCode, config));
            }

            foreach (var gameplayEffectCode in EnumerateUniquePositiveCodes(gameplayEffectCodes))
            {
                var config = GameplayEffectConfigRegistry.GetConfigByID(
                    gameplayEffectCode,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.None,
                        0,
                        ConfigRegistryReferenceKind.Direct));
                if (config != null)
                    effects.Add(FromGameplayEffectConfig(gameplayEffectCode, config));
            }

            if (attributeSetConfigs != null)
            {
                foreach (var config in attributeSetConfigs)
                {
                    if (config.Code <= 0)
                        continue;

                    attributeSets.Add(FromAttributeSetConfig(config));
                    attributes.AddRange(FromAttributeSetAttributes(config));
                }
            }

            if (gameplayTags != null)
            {
                var seenTags = new HashSet<int>();
                foreach (var tag in gameplayTags)
                {
                    if (tag.Code <= 0 || !seenTags.Add(tag.Code))
                        continue;

                    tags.Add(FromGameplayTag(tag));
                }
            }

            foreach (var cueCode in EnumerateUniquePositiveCodes(gameplayCueCodes))
            {
                var config = GameplayCueConfigRegistry.GetConfigByID(
                    cueCode,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.None,
                        0,
                        ConfigRegistryReferenceKind.Direct));
                if (config != null)
                    cues.Add(FromGameplayCueConfig(cueCode, config));
            }

            return new GASDefinitionTable(
                abilities.ToArray(),
                effects.ToArray(),
                attributeSets.ToArray(),
                attributes.ToArray(),
                tags.ToArray(),
                cues.ToArray());
        }

        private static IEnumerable<int> EnumerateUniquePositiveCodes(IEnumerable<int> codes)
        {
            if (codes == null)
                yield break;

            var seen = new HashSet<int>();
            foreach (var code in codes)
            {
                if (code <= 0 || !seen.Add(code))
                    continue;

                yield return code;
            }
        }

        private static int CountPositive(int[] values)
        {
            if (values == null)
                return 0;

            var count = 0;
            for (var i = 0; i < values.Length; i++)
                if (values[i] > 0)
                    count++;
            return count;
        }

        private static bool HasAny(int[] values)
        {
            if (values == null)
                return false;

            for (var i = 0; i < values.Length; i++)
                if (values[i] > 0)
                    return true;
            return false;
        }

        private static bool HasAnyRequirement(int[] all, int[] any, int[] none)
        {
            return HasAny(all) || HasAny(any) || HasAny(none);
        }
    }
}
