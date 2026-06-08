using Unity.Entities;

namespace GAS.Runtime
{
    public static class GASRuntimeDefinitionResolver
    {
        public static GASRuntimeConceptCoverageSnapshot CreateConceptCoverageSnapshot(
            ref GASDefinitionCatalogBlob catalog)
        {
            var abilityCount = catalog.Abilities.Length;
            var gameplayEffectCount = catalog.GameplayEffects.Length;
            var modifierCount = catalog.Modifiers.Length;
            var requirementCount = catalog.Requirements.Length;
            var tagMaskCount = catalog.TagMasks.Length;
            var gameplayCueCount = 0;
            var activeGameplayEffectDefinitionCount = 0;
            var setByCallerModifierCount = 0;
            var executionCalculationModifierCount = 0;
            for (var i = 0; i < gameplayEffectCount; i++)
            {
                var gameplayEffect = catalog.GameplayEffects[i];
                if (gameplayEffect.GameplayCueCode > 0)
                    gameplayCueCount++;
                if (RequiresActiveMutationLane(in gameplayEffect, gameplayEffect.DurationFrames))
                    activeGameplayEffectDefinitionCount++;
            }

            for (var i = 0; i < modifierCount; i++)
            {
                var modifier = catalog.Modifiers[i];
                if (modifier.MagnitudeSource == EMagnitudeSource.SetByCaller)
                    setByCallerModifierCount++;
                if (modifier.MagnitudeSource == EMagnitudeSource.ExecutionCalculation)
                    executionCalculationModifierCount++;
            }

            var covered = EGASOfficialConceptCoverageFlags.None;
            if (abilityCount > 0)
                covered |= EGASOfficialConceptCoverageFlags.AbilityLifecycle;
            if (gameplayEffectCount > 0)
                covered |= EGASOfficialConceptCoverageFlags.GameplayEffectSpec;
            if (modifierCount > 0)
            {
                covered |= EGASOfficialConceptCoverageFlags.AttributeModifier
                           | EGASOfficialConceptCoverageFlags.MagnitudeEvaluation;
            }

            if (tagMaskCount > 0)
                covered |= EGASOfficialConceptCoverageFlags.TagTaxonomy;
            if (requirementCount > 0)
                covered |= EGASOfficialConceptCoverageFlags.TagRequirement;
            if (gameplayCueCount > 0)
                covered |= EGASOfficialConceptCoverageFlags.GameplayCue;
            if (abilityCount > 0 || gameplayEffectCount > 0)
                covered |= EGASOfficialConceptCoverageFlags.AbilitySystemComponentBinding;
            if (activeGameplayEffectDefinitionCount > 0)
                covered |= EGASOfficialConceptCoverageFlags.ActiveGameplayEffect;
            if (setByCallerModifierCount > 0)
                covered |= EGASOfficialConceptCoverageFlags.SetByCallerMagnitude;
            if (executionCalculationModifierCount > 0)
                covered |= EGASOfficialConceptCoverageFlags.ExecutionCalculation;
            if (catalog.GrantedAbilities.Length > 0)
                covered |= EGASOfficialConceptCoverageFlags.GrantedAbility;

            return new GASRuntimeConceptCoverageSnapshot(
                catalog.SchemaVersion,
                abilityCount,
                gameplayEffectCount,
                modifierCount,
                requirementCount,
                tagMaskCount,
                gameplayCueCount,
                catalog.GrantedAbilities.Length,
                activeGameplayEffectDefinitionCount,
                setByCallerModifierCount,
                executionCalculationModifierCount,
                covered,
                GASRuntimeConceptCoverage.OfficialConceptMatrix & ~covered);
        }

        public static bool TryBuildRuntimeTracePreview(
            ref GASDefinitionCatalogBlob catalog,
            int abilityCode,
            Entity sourceAsc,
            Entity targetAsc,
            Entity sourceAbility,
            int frame,
            int requestedLevel,
            out GASRuntimeTracePreview trace)
        {
            var coverage = CreateConceptCoverageSnapshot(ref catalog);
            if (!TryBuildAbilityActivationPlan(
                    ref catalog,
                    abilityCode,
                    sourceAsc,
                    targetAsc,
                    sourceAbility,
                    frame,
                    requestedLevel,
                    out var plan))
            {
                trace = new GASRuntimeTracePreview(
                    frame,
                    abilityCode,
                    requestedLevel,
                    sourceAsc,
                    targetAsc,
                    sourceAbility,
                    plan.FailureReasonCode,
                    seedCount: 0,
                    modifierCount: 0,
                    factCount: 0,
                    cueCount: 0,
                    activeMutationSeedCount: 0,
                    executionCalculationModifierCount: 0,
                    EGASRuntimeTraceStageFlags.None,
                    GASRuntimeConceptCoverage.RequiredTraceStages,
                    coverage.CoveredConcepts,
                    coverage.MissingConcepts);
                return false;
            }

            var stageMask = EGASRuntimeTraceStageFlags.AbilityActivationPlan;
            if (HasAbilityRequirementQuery(ref catalog, plan.AbilityDefinitionIndex))
                stageMask |= EGASRuntimeTraceStageFlags.AbilityRequirementQuery;

            var seedCount = 0;
            var modifierCount = 0;
            var factCount = 0;
            var cueCount = 0;
            var activeMutationSeedCount = 0;
            var executionCalculationModifierCount = 0;
            var nextPreviewContext = 1;
            AppendGameplayEffectTrace(
                ref catalog,
                in plan,
                GASGESeedKind.Primary,
                GEEffectCommandSource.Ability,
                plan.PrimaryGameplayEffectCode,
                ref nextPreviewContext,
                ref seedCount,
                ref modifierCount,
                ref factCount,
                ref cueCount,
                ref activeMutationSeedCount,
                ref executionCalculationModifierCount);
            AppendGameplayEffectTrace(
                ref catalog,
                in plan,
                GASGESeedKind.Secondary,
                GEEffectCommandSource.Ability,
                plan.SecondaryGameplayEffectCode,
                ref nextPreviewContext,
                ref seedCount,
                ref modifierCount,
                ref factCount,
                ref cueCount,
                ref activeMutationSeedCount,
                ref executionCalculationModifierCount);
            AppendGameplayEffectTrace(
                ref catalog,
                in plan,
                GASGESeedKind.Cost,
                GEEffectCommandSource.Ability,
                plan.CostGameplayEffectCode,
                ref nextPreviewContext,
                ref seedCount,
                ref modifierCount,
                ref factCount,
                ref cueCount,
                ref activeMutationSeedCount,
                ref executionCalculationModifierCount);
            AppendGameplayEffectTrace(
                ref catalog,
                in plan,
                GASGESeedKind.Cooldown,
                GEEffectCommandSource.Ability,
                plan.CooldownGameplayEffectCode,
                ref nextPreviewContext,
                ref seedCount,
                ref modifierCount,
                ref factCount,
                ref cueCount,
                ref activeMutationSeedCount,
                ref executionCalculationModifierCount);

            if (seedCount > 0)
            {
                stageMask |= EGASRuntimeTraceStageFlags.GECommandSeed
                             | EGASRuntimeTraceStageFlags.GESpecShape;
            }

            if (modifierCount > 0)
            {
                stageMask |= EGASRuntimeTraceStageFlags.ModifierMagnitude
                             | EGASRuntimeTraceStageFlags.AttributeDelta
                             | EGASRuntimeTraceStageFlags.GameplayFact
                             | EGASRuntimeTraceStageFlags.BoundaryProjection;
            }

            if (cueCount > 0)
            {
                stageMask |= EGASRuntimeTraceStageFlags.GameplayCue
                             | EGASRuntimeTraceStageFlags.GameplayFact
                             | EGASRuntimeTraceStageFlags.BoundaryProjection;
            }

            if (activeMutationSeedCount > 0)
            {
                stageMask |= EGASRuntimeTraceStageFlags.ActiveEffectMutation
                             | EGASRuntimeTraceStageFlags.GameplayFact;
            }

            var missingStageMask = GASRuntimeConceptCoverage.RequiredTraceStages & ~stageMask;
            trace = new GASRuntimeTracePreview(
                frame,
                plan.AbilityCode,
                plan.Level,
                sourceAsc,
                targetAsc,
                sourceAbility,
                plan.FailureReasonCode,
                seedCount,
                modifierCount,
                factCount,
                cueCount,
                activeMutationSeedCount,
                executionCalculationModifierCount,
                stageMask,
                missingStageMask,
                coverage.CoveredConcepts,
                coverage.MissingConcepts);
            return seedCount > 0;
        }

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

        public static bool TryNormalizeGameplayEffectCommand(
            ref GASDefinitionCatalogBlob catalog,
            ref GEEffectCommandBuffer command)
        {
            if (command.GameplayEffectCode <= 0
                || !GASDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                    ref catalog,
                    command.GameplayEffectCode,
                    out var gameplayEffectIndex))
            {
                return false;
            }

            ref readonly var gameplayEffect = ref GASDefinitionCatalogLookup.GetGameplayEffect(
                ref catalog,
                gameplayEffectIndex);
            var durationFrame = ResolveDurationFrame(in command, in gameplayEffect);
            var isActiveMutation = RequiresActiveMutationLane(in gameplayEffect, durationFrame);
            command.Kind = isActiveMutation ? GEEffectCommandKind.ActiveMutation : GEEffectCommandKind.Instant;
            command.DurationFrameOverride = durationFrame;
            if (isActiveMutation)
                command.Flags |= GASGECommandSeedFlags.ActiveMutation;
            else
                command.Flags &= ~GASGECommandSeedFlags.ActiveMutation;
            return true;
        }

        private static int ResolveDurationFrame(
            in GEEffectCommandBuffer command,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect)
        {
            return command.DurationFrameOverride > 0
                ? command.DurationFrameOverride
                : gameplayEffect.DurationFrames;
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

        private static bool HasAbilityRequirementQuery(
            ref GASDefinitionCatalogBlob catalog,
            int abilityDefinitionIndex)
        {
            if ((uint)abilityDefinitionIndex >= (uint)catalog.Abilities.Length)
                return false;

            var ability = catalog.Abilities[abilityDefinitionIndex];
            return ability.RequirementCount > 0
                   || ability.ActivationOwnedTagMaskIndex >= 0;
        }

        private static void AppendGameplayEffectTrace(
            ref GASDefinitionCatalogBlob catalog,
            in AbilityActivationPlanRecord plan,
            int seedKind,
            GEEffectCommandSource source,
            int gameplayEffectCode,
            ref int nextPreviewContext,
            ref int seedCount,
            ref int modifierCount,
            ref int factCount,
            ref int cueCount,
            ref int activeMutationSeedCount,
            ref int executionCalculationModifierCount)
        {
            if (!TryBuildGECommandSeed(
                    ref catalog,
                    in plan,
                    seedKind,
                    source,
                    gameplayEffectCode,
                    nextPreviewContext,
                    parentContextId: 0,
                    out var seed))
            {
                return;
            }

            nextPreviewContext++;
            seedCount++;
            if ((seed.Flags & GASGECommandSeedFlags.ActiveMutation) != 0)
                activeMutationSeedCount++;

            if ((uint)seed.GameplayEffectDefinitionIndex >= (uint)catalog.GameplayEffects.Length)
                return;

            var gameplayEffect = catalog.GameplayEffects[seed.GameplayEffectDefinitionIndex];
            modifierCount += gameplayEffect.ModifierCount;
            if (gameplayEffect.ModifierCount > 0)
                factCount += gameplayEffect.ModifierCount;
            if (gameplayEffect.GameplayCueCode > 0)
            {
                cueCount++;
                factCount++;
            }

            if ((seed.Flags & GASGECommandSeedFlags.ActiveMutation) != 0)
                factCount++;

            for (var i = 0; i < gameplayEffect.ModifierCount; i++)
            {
                var modifierIndex = gameplayEffect.ModifierStart + i;
                if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)
                    continue;

                if (catalog.Modifiers[modifierIndex].MagnitudeSource == EMagnitudeSource.ExecutionCalculation)
                    executionCalculationModifierCount++;
            }
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

        public static bool EvaluateGameplayEffectRequirements(
            ref GASDefinitionCatalogBlob catalog,
            int gameplayEffectDefinitionIndex,
            in TagMaskComponent targetTags,
            out int failureReasonCode)
        {
            failureReasonCode = GASFailureReasonCodes.None;
            if ((uint)gameplayEffectDefinitionIndex >= (uint)catalog.GameplayEffects.Length)
            {
                failureReasonCode = GASFailureReasonCodes.GameplayEffectNotFound;
                return false;
            }

            ref readonly var gameplayEffect = ref GASDefinitionCatalogLookup.GetGameplayEffect(
                ref catalog,
                gameplayEffectDefinitionIndex);
            return EvaluateGameplayEffectRequirements(ref catalog, in gameplayEffect, in targetTags, out failureReasonCode);
        }

        public static bool EvaluateGameplayEffectRequirements(
            ref GASDefinitionCatalogBlob catalog,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            in TagMaskComponent targetTags,
            out int failureReasonCode)
        {
            return EvaluateRange(
                ref catalog,
                gameplayEffect.RequirementStart,
                gameplayEffect.RequirementCount,
                in targetTags,
                out failureReasonCode);
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

    public static class GASRuntimeMagnitudeEvaluator
    {
        public static bool TryResolveMagnitude(
            in GASCatalogModifierDefinitionBlob modifier,
            in MagnitudeEvalContext context,
            out float magnitude)
        {
            var rawMagnitude = modifier.MagnitudeSource switch
            {
                EMagnitudeSource.Constant => modifier.BaseMagnitude,
                EMagnitudeSource.SetByCaller => context.HasSetByCallerValue != 0
                    && context.SetByCallerKey == modifier.MagnitudeKey
                        ? context.SetByCallerValue
                        : modifier.FallbackMagnitude,
                EMagnitudeSource.SourceAttribute => context.HasSourceAttributeValue != 0
                    ? context.SourceAttributeValue
                    : modifier.FallbackMagnitude,
                EMagnitudeSource.TargetAttribute => context.HasTargetAttributeValue != 0
                    ? context.TargetAttributeValue
                    : modifier.FallbackMagnitude,
                EMagnitudeSource.ExecutionCalculation => context.HasExecutionValue != 0
                    ? context.ExecutionValue
                    : modifier.FallbackMagnitude,
                EMagnitudeSource.StackCount => context.StackCount,
                _ => modifier.BaseMagnitude,
            };

            var coefficient = modifier.Coefficient == 0f ? 1f : modifier.Coefficient;
            magnitude = ((rawMagnitude + modifier.PreAdd) * coefficient) + modifier.PostAdd;
            return true;
        }
    }
}
