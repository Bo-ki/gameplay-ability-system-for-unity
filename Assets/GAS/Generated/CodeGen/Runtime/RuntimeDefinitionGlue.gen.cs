///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Generated
{
    public static class GASGeneratedRuntimeDefinitionResolver
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
            if (!GASGeneratedDefinitionCatalogLookup.TryGetAbilityIndex(ref catalog, abilityCode, out var abilityIndex))
            {
                plan.FailureReasonCode = GASFailureReasonCodes.AbilityNotFound;
                return false;
            }

            ref readonly var ability = ref GASGeneratedDefinitionCatalogLookup.GetAbility(ref catalog, abilityIndex);
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

        public static int WriteGECommandSeeds(
            ref GASDefinitionCatalogBlob catalog,
            in AbilityActivationPlanRecord plan,
            int contextId,
            int parentContextId,
            ref NativeList<GECommandSeedRecord> seeds)
        {
            if (!plan.Succeeded)
                return 0;

            var count = 0;
            count += TryAppendSeed(ref catalog, in plan, GASGESeedKind.Cost, GEEffectCommandSource.Ability, plan.CostGameplayEffectCode, contextId, parentContextId, ref seeds) ? 1 : 0;
            count += TryAppendSeed(ref catalog, in plan, GASGESeedKind.Cooldown, GEEffectCommandSource.Ability, plan.CooldownGameplayEffectCode, contextId, parentContextId, ref seeds) ? 1 : 0;
            count += TryAppendSeed(ref catalog, in plan, GASGESeedKind.Primary, GEEffectCommandSource.Ability, plan.PrimaryGameplayEffectCode, contextId, parentContextId, ref seeds) ? 1 : 0;
            count += TryAppendSeed(ref catalog, in plan, GASGESeedKind.Secondary, GEEffectCommandSource.Ability, plan.SecondaryGameplayEffectCode, contextId, parentContextId, ref seeds) ? 1 : 0;
            return count;
        }

        public static int AppendModifierRecords(
            ref GASDefinitionCatalogBlob catalog,
            int gameplayEffectDefinitionIndex,
            in MagnitudeEvalContext context,
            ref NativeList<ResolvedModifierRecord> modifiers)
        {
            if ((uint)gameplayEffectDefinitionIndex >= (uint)catalog.GameplayEffects.Length)
                return 0;

            ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectDefinitionIndex);
            var appended = 0;
            for (var i = 0; i < gameplayEffect.ModifierCount; i++)
            {
                var modifierIndex = gameplayEffect.ModifierStart + i;
                if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)
                    continue;

                var modifier = catalog.Modifiers[modifierIndex];
                if (!GASGeneratedMagnitudeEvaluator.TryResolveMagnitude(in modifier, in context, out var magnitude))
                    continue;

                modifiers.Add(new ResolvedModifierRecord
                {
                    GameplayEffectCode = gameplayEffect.GameplayEffectCode,
                    ModifierIndex = modifier.ModifierIndex,
                    AttributeSetCode = modifier.AttributeSetCode,
                    AttributeCode = modifier.AttributeCode,
                    Operation = modifier.Operation,
                    Magnitude = magnitude,
                    MagnitudeSource = modifier.MagnitudeSource,
                    MagnitudeKey = modifier.MagnitudeKey,
                    SourceAsc = context.SourceAsc,
                    TargetAsc = context.TargetAsc,
                });
                appended++;
            }

            return appended;
        }

        private static bool TryAppendSeed(
            ref GASDefinitionCatalogBlob catalog,
            in AbilityActivationPlanRecord plan,
            int seedKind,
            GEEffectCommandSource source,
            int gameplayEffectCode,
            int contextId,
            int parentContextId,
            ref NativeList<GECommandSeedRecord> seeds)
        {
            if (gameplayEffectCode <= 0)
                return false;

            var failureReason = GASFailureReasonCodes.None;
            var durationFrameOverride = 0;
            var flags = GASGECommandSeedFlags.None;
            if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, gameplayEffectCode, out var gameplayEffectIndex))
                failureReason = GASFailureReasonCodes.GameplayEffectNotFound;
            else
            {
                ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                durationFrameOverride = seedKind == GASGESeedKind.Cooldown && plan.CooldownFrames > 0
                    ? plan.CooldownFrames
                    : gameplayEffect.DurationFrames;
                if (durationFrameOverride > 0
                    || gameplayEffect.PeriodFrames > 0
                    || gameplayEffect.StackLimitCount > 0
                    || gameplayEffect.GrantedTagMaskIndex >= 0
                    || gameplayEffect.RemoveGameplayEffectTagMaskIndex >= 0
                    || gameplayEffect.GrantedAbilityCount > 0
                    || gameplayEffect.ModifierCount == 0)
                    flags |= GASGECommandSeedFlags.ActiveMutation;
            }

            seeds.Add(new GECommandSeedRecord
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
            });
            return failureReason == GASFailureReasonCodes.None;
        }
    }

    public static class GASGeneratedRequirementEvaluator
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

            ref readonly var ability = ref GASGeneratedDefinitionCatalogLookup.GetAbility(ref catalog, abilityDefinitionIndex);
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
                if (!EvaluateRequirement(ref catalog, in requirement, in ownerTags))
                {
                    failureReasonCode = GASFailureReasonCodes.RequirementFailed;
                    return false;
                }
            }

            return true;
        }

        private static bool EvaluateRequirement(ref GASDefinitionCatalogBlob catalog, in GASCatalogRequirementDefinitionBlob requirement, in TagMaskComponent ownerTags)
        {
            if (requirement.RequirementKind == GASRequirementKind.None)
                return true;

            if (requirement.TagMaskIndex < 0 || requirement.TagMaskIndex >= catalog.TagMasks.Length)
                return true;

            var mask = catalog.TagMasks[requirement.TagMaskIndex].Mask;
            if (requirement.RequirementKind == GASRequirementKind.RequiredTags)
                return ownerTags.HasAllTags(mask);
            if (requirement.RequirementKind == GASRequirementKind.BlockedTags)
                return !ownerTags.HasAnyTag(mask);
            return true;
        }
    }

    public static class GASGeneratedMagnitudeEvaluator
    {
        public static bool TryResolveMagnitude(
            in GASCatalogModifierDefinitionBlob modifier,
            in MagnitudeEvalContext context,
            out float magnitude)
        {
            var rawMagnitude = modifier.MagnitudeSource switch
            {
                EMagnitudeSource.Constant => modifier.BaseMagnitude,
                EMagnitudeSource.SetByCaller => context.HasSetByCallerValue != 0 && context.SetByCallerKey == modifier.MagnitudeKey ? context.SetByCallerValue : modifier.FallbackMagnitude,
                EMagnitudeSource.SourceAttribute => context.HasSourceAttributeValue != 0 ? context.SourceAttributeValue : modifier.FallbackMagnitude,
                EMagnitudeSource.TargetAttribute => context.HasTargetAttributeValue != 0 ? context.TargetAttributeValue : modifier.FallbackMagnitude,
                EMagnitudeSource.ExecutionCalculation => context.HasExecutionValue != 0 ? context.ExecutionValue : modifier.FallbackMagnitude,
                EMagnitudeSource.StackCount => context.StackCount,
                _ => modifier.BaseMagnitude,
            };

            var coefficient = modifier.Coefficient == 0f ? 1f : modifier.Coefficient;
            magnitude = ((rawMagnitude + modifier.PreAdd) * coefficient) + modifier.PostAdd;
            return true;
        }
    }

    public static class GASGeneratedTargetRuleTable
    {
        public static bool TryResolveSingleTarget(
            in AbilityActivationPlanRecord plan,
            Entity fallbackTarget,
            out AbilityTargetRecord target)
        {
            target = new AbilityTargetRecord
            {
                SourceAsc = plan.SourceAsc,
                TargetAsc = plan.TargetAsc != Entity.Null ? plan.TargetAsc : fallbackTarget,
                TargetRuleCode = plan.TargetRuleCode,
            };
            return target.TargetAsc != Entity.Null;
        }
    }
}
