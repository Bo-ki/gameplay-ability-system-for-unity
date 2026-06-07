using Unity.Entities;

namespace GAS.Runtime
{
    public static class GASRuntimeDefinitionResolver
    {
        public static bool TryBuildAbilityActivationPlan(
            ref GASDefinitionCatalogBlob catalog,
            int abilityCode,
            Entity sourceAsc,
            Entity targetAsc,
            Entity sourceAbility,
            int frame,
            int requestedLevel,
            out AbilityActivationPlanRecord plan)
        {
            plan = default;
            if (!GASDefinitionCatalogLookup.TryGetAbilityIndex(ref catalog, abilityCode, out var abilityIndex))
            {
                plan.FailureReasonCode = GASFailureReasonCodes.AbilityNotFound;
                return false;
            }

            ref readonly var ability = ref GASDefinitionCatalogLookup.GetAbility(ref catalog, abilityIndex);
            plan = new AbilityActivationPlanRecord
            {
                Frame = frame,
                AbilityCode = ability.AbilityCode,
                AbilityDefinitionIndex = abilityIndex,
                Level = requestedLevel > 0 ? requestedLevel : ability.Level,
                SourceAsc = sourceAsc,
                TargetAsc = targetAsc,
                SourceAbility = sourceAbility,
                PrimaryGameplayEffectCode = ability.PrimaryGameplayEffectCode,
                SecondaryGameplayEffectCode = ability.SecondaryGameplayEffectCode,
                CostGameplayEffectCode = ability.CostGameplayEffectCode,
                CooldownGameplayEffectCode = ability.CooldownGameplayEffectCode,
                CooldownFrames = ability.CooldownFrames,
                TargetRuleCode = ability.TargetRuleCode,
                FailureReasonCode = GASFailureReasonCodes.None,
            };
            return true;
        }

        public static bool TryBuildGECommandSeed(
            ref GASDefinitionCatalogBlob catalog,
            in AbilityActivationPlanRecord plan,
            int seedKind,
            GEEffectCommandSource source,
            int gameplayEffectCode,
            int contextId,
            int parentContextId,
            out GECommandSeedRecord seed)
        {
            seed = default;
            if (!plan.Succeeded || gameplayEffectCode <= 0)
                return false;

            var failureReason = GASFailureReasonCodes.None;
            var durationFrameOverride = 0;
            var gameplayEffectIndex = -1;
            var flags = GASGECommandSeedFlags.None;
            if (!GASDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, gameplayEffectCode, out gameplayEffectIndex))
            {
                failureReason = GASFailureReasonCodes.GameplayEffectNotFound;
            }
            else
            {
                ref readonly var gameplayEffect = ref GASDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                durationFrameOverride = seedKind == GASGESeedKind.Cooldown && plan.CooldownFrames > 0
                    ? plan.CooldownFrames
                    : gameplayEffect.DurationFrames;
                if (RequiresActiveMutationLane(in gameplayEffect, durationFrameOverride))
                    flags |= GASGECommandSeedFlags.ActiveMutation;
            }

            seed = new GECommandSeedRecord
            {
                Frame = plan.Frame,
                SeedKind = seedKind,
                Source = source,
                SourceAsc = plan.SourceAsc,
                TargetAsc = plan.TargetAsc,
                SourceAbility = plan.SourceAbility,
                GameplayEffectCode = gameplayEffectCode,
                GameplayEffectDefinitionIndex = gameplayEffectIndex,
                Level = plan.Level,
                ContextId = contextId,
                ParentContextId = parentContextId,
                DurationFrameOverride = durationFrameOverride,
                FailureReasonCode = failureReason,
                Flags = flags,
            };
            return failureReason == GASFailureReasonCodes.None;
        }

        private static bool RequiresActiveMutationLane(
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int durationFrameOverride)
        {
            return durationFrameOverride > 0
                   || gameplayEffect.PeriodFrames > 0
                   || gameplayEffect.StackLimitCount > 0
                   || gameplayEffect.GrantedTagMaskIndex >= 0
                   || !gameplayEffect.RemoveGameplayEffectTagQuery.IsEmpty
                   || gameplayEffect.GrantedAbilityCount > 0;
        }
    }

    public static class GASRuntimeRequirementEvaluator
    {
        public static bool EvaluateAbilityRequirements(
            ref GASDefinitionCatalogBlob catalog,
            int abilityDefinitionIndex,
            in TagMaskComponent ownerTags,
            out int failureReasonCode)
        {
            failureReasonCode = GASFailureReasonCodes.None;
            if ((uint)abilityDefinitionIndex >= (uint)catalog.Abilities.Length)
            {
                failureReasonCode = GASFailureReasonCodes.AbilityNotFound;
                return false;
            }

            ref readonly var ability = ref GASDefinitionCatalogLookup.GetAbility(ref catalog, abilityDefinitionIndex);
            return EvaluateRange(ref catalog, ability.RequirementStart, ability.RequirementCount, in ownerTags, out failureReasonCode);
        }

        private static bool EvaluateRange(
            ref GASDefinitionCatalogBlob catalog,
            int start,
            int count,
            in TagMaskComponent ownerTags,
            out int failureReasonCode)
        {
            failureReasonCode = GASFailureReasonCodes.None;
            for (var i = 0; i < count; i++)
            {
                var index = start + i;
                if ((uint)index >= (uint)catalog.Requirements.Length)
                {
                    failureReasonCode = GASFailureReasonCodes.RequirementFailed;
                    return false;
                }

                var requirement = catalog.Requirements[index];
                if (!EvaluateRequirement(in requirement, in ownerTags))
                {
                    failureReasonCode = GASFailureReasonCodes.RequirementFailed;
                    return false;
                }
            }

            return true;
        }

        private static bool EvaluateRequirement(
            in GASCatalogRequirementDefinitionBlob requirement,
            in TagMaskComponent ownerTags)
        {
            if (requirement.RequirementKind == GASRequirementKind.None)
                return true;

            if (requirement.TagQuery.IsEmpty)
                return false;

            var matches = requirement.TagQuery.Evaluate(ownerTags);
            if (requirement.RequirementKind == GASRequirementKind.RequiredTags)
                return matches;
            if (requirement.RequirementKind == GASRequirementKind.BlockedTags)
                return !matches;
            return false;
        }
    }
}
