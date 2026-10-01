using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 标识 RuntimeV1-Runnable 支持面在 raw 或 Blob 阶段拒绝的语义族。
    /// </summary>
    public enum GasRuntimeV1SupportProfileError : byte
    {
        None = 0,
        RawDocumentInvalid = 1,
        RawAbilityUnsupported = 2,
        RawEffectUnsupported = 3,
        RawAttributeUnsupported = 4,
        RawScenarioUnsupported = 5,
        ClosedWorldShapeMismatch = 6,
        AbilityUnsupported = 7,
        EffectUnsupported = 8,
        CaptureUnsupported = 9,
        CueUnsupported = 10,
        RangeUnsupported = 11,
    }

    /// <summary>
    /// 返回同一 RuleId 下的 typed 支持面校验结果，便于 raw 与 Blob reject 统一归因。
    /// </summary>
    public readonly struct GasRuntimeV1SupportProfileResult
    {
        public readonly GasRuntimeV1SupportProfileError Error;
        public readonly int DefinitionId;
        public readonly int ElementIndex;

        /// <summary>
        /// 创建一个支持面校验结果。
        /// </summary>
        public GasRuntimeV1SupportProfileResult(
            GasRuntimeV1SupportProfileError error,
            int definitionId = 0,
            int elementIndex = -1)
        {
            Error = error;
            DefinitionId = definitionId;
            ElementIndex = elementIndex;
        }

        public bool Succeeded => Error == GasRuntimeV1SupportProfileError.None;
        public string RuleId => GasRuntimeV1SupportProfile.RuleId;
    }

    /// <summary>
    /// 冻结 AutoChess RuntimeV1-Runnable 闭世界；只判定支持性，不修补或默认化 authoring。
    /// </summary>
    public static class GasRuntimeV1SupportProfile
    {
        public const string RuleId = "RuntimeV1SupportProfile-v1";

        /// <summary>
        /// 在基础 Catalog contract 通过后，校验当前 Runnable 闭世界的精确 Blob 支持面。
        /// </summary>
        public static GasRuntimeV1SupportProfileResult ValidateBlob(ref GasDefinitionCatalogBlob catalog)
        {
            var shape = ValidateRootShape(ref catalog);
            if (!shape.Succeeded)
                return shape;

            var attributes = ValidateAttributes(
                ref catalog,
                out var healthLayoutIndex,
                out var attackLayoutIndex);
            if (!attributes.Succeeded)
                return attributes;

            var abilities = ValidateAbilities(ref catalog);
            if (!abilities.Succeeded)
                return abilities;

            return ValidateEffects(ref catalog, healthLayoutIndex, attackLayoutIndex);
        }

        /// <summary>
        /// 校验所有 root array 数量，防止未声明 payload 混入受限 Profile。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateRootShape(ref GasDefinitionCatalogBlob catalog)
        {
            if (catalog.AttributeLayout.Entries.Length != 3
                || catalog.TagCatalog.Entries.Length != 0
                || catalog.TagCatalog.AncestorIndices.Length != 0
                || catalog.AbilityIndex.Length != 4 || catalog.Abilities.Length != 4
                || catalog.GameplayEffectIndex.Length != 4 || catalog.GameplayEffects.Length != 4
                || catalog.Requirements.Length != 0 || catalog.RequirementTagIndices.Length != 0
                || catalog.CaptureDescriptors.Length != 1 || catalog.Modifiers.Length != 4
                || catalog.DirectEffectProgramNodes.Length != 4 || catalog.CueReferences.Length != 4
                || catalog.ValueViews.Length != 3 || catalog.EvaluatorInstructions.Length != 21
                || catalog.SetByCallerDescriptors.Length != 0
                || catalog.TargetDataDescriptors.Length != 4
                || catalog.EffectContextFieldDescriptors.Length != 12)
            {
                return Failure(GasRuntimeV1SupportProfileError.ClosedWorldShapeMismatch);
            }
            return Success();
        }

        /// <summary>
        /// 校验 Health、Energy、Attack 三属性布局，并返回唯一 Health layout index。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateAttributes(
            ref GasDefinitionCatalogBlob catalog,
            out int healthLayoutIndex,
            out int attackLayoutIndex)
        {
            healthLayoutIndex = -1;
            attackLayoutIndex = -1;
            for (var index = 0; index < catalog.AttributeLayout.Entries.Length; index++)
            {
                var attribute = catalog.AttributeLayout.Entries[index];
                var expectedId = ExpectedAttributeId(index);
                var isHealth = index == 0;
                if (attribute.AttributeId != expectedId || attribute.LayoutIndex != index
                    || attribute.MinimumValue != 0f || attribute.MaximumValue != 99999f
                    || attribute.ClampMinimum != 1 || attribute.ClampMaximum != 1
                    || attribute.DomainRole != (isHealth ? GasAttributeDomainRole.Health : GasAttributeDomainRole.None)
                    || attribute.DefaultValue != (isHealth ? 100f : 0f))
                {
                    return Failure(GasRuntimeV1SupportProfileError.ClosedWorldShapeMismatch, elementIndex: index);
                }
                if (isHealth)
                    healthLayoutIndex = index;
                if (attribute.AttributeId == 4)
                    attackLayoutIndex = index;
            }
            return healthLayoutIndex == 0 && attackLayoutIndex == 2
                ? Success()
                : Failure(GasRuntimeV1SupportProfileError.ClosedWorldShapeMismatch);
        }

        /// <summary>
        /// 校验四个 cost/cooldown-disabled one-shot Ability 与一对一 direct node。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateAbilities(ref GasDefinitionCatalogBlob catalog)
        {
            for (var index = 0; index < catalog.Abilities.Length; index++)
            {
                var ability = catalog.Abilities[index];
                var expectedAbilityId = ExpectedAbilityId(index);
                var expectedEffectId = ExpectedAbilityEffectId(index);
                var indexEntry = catalog.AbilityIndex[index];
                if (ability.DefinitionId != expectedAbilityId
                    || indexEntry.DefinitionId != expectedAbilityId || indexEntry.DefinitionIndex != index
                    || ability.Level != 1 || ability.MaxConcurrentActivations != 1
                    || ability.ActivationRequirementRange.Count != 0 || ability.CueRange.Count != 0
                    || ability.CostMutationContract.Enabled != 0 || ability.CooldownGateContract.Enabled != 0
                    || ability.DirectEffectProgramRange.Start != index || ability.DirectEffectProgramRange.Count != 1
                    || !IsFrozenAlive(in ability.TargetPolicy)
                    || !IsAbilityMaxima(in ability.Maxima))
                {
                    return Failure(GasRuntimeV1SupportProfileError.AbilityUnsupported, ability.DefinitionId, index);
                }

                var node = catalog.DirectEffectProgramNodes[index];
                if (node.NodeOrdinal != 0 || node.EffectDefinitionId != expectedEffectId
                    || node.MaximumTargetCount != 1 || node.MaximumOutputCount != 1)
                {
                    return Failure(GasRuntimeV1SupportProfileError.AbilityUnsupported, ability.DefinitionId, index);
                }
            }
            return Success();
        }

        /// <summary>
        /// 校验四个 Effect 的 canonical ranges，并按 payload 形状验证两类 instant、9203 与 finisher。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateEffects(
            ref GasDefinitionCatalogBlob catalog,
            int healthLayoutIndex,
            int attackLayoutIndex)
        {
            var cursors = new PayloadCursors();
            var instantCount = 0;
            var durationCount = 0;
            var executionCount = 0;
            for (var index = 0; index < catalog.GameplayEffects.Length; index++)
            {
                var effect = catalog.GameplayEffects[index];
                var expectedEffectId = ExpectedEffectId(index);
                var indexEntry = catalog.GameplayEffectIndex[index];
                if (effect.DefinitionId != expectedEffectId
                    || indexEntry.DefinitionId != expectedEffectId || indexEntry.DefinitionIndex != index)
                {
                    return Failure(GasRuntimeV1SupportProfileError.EffectUnsupported, effect.DefinitionId, index);
                }
                var ranges = ValidateEffectEnvelope(ref catalog, in effect, ref cursors, index);
                if (!ranges.Succeeded)
                    return ranges;

                GasRuntimeV1SupportProfileResult result;
                if (effect.Lifetime == GasEffectLifetimePolicy.Instant)
                {
                    instantCount++;
                    result = ValidateFixedInstant(ref catalog, in effect, healthLayoutIndex);
                }
                else if (effect.Lifetime == GasEffectLifetimePolicy.Duration)
                {
                    durationCount++;
                    result = ValidatePoison(ref catalog, in effect, healthLayoutIndex, attackLayoutIndex);
                }
                else if (effect.Lifetime == GasEffectLifetimePolicy.InstantExecution)
                {
                    executionCount++;
                    result = ValidateFinisher(ref catalog, in effect, healthLayoutIndex);
                }
                else
                {
                    result = Failure(GasRuntimeV1SupportProfileError.EffectUnsupported, effect.DefinitionId, index);
                }
                if (!result.Succeeded)
                    return result;
            }

            return instantCount == 2 && durationCount == 1 && executionCount == 1 && cursors.AllConsumed(ref catalog)
                ? Success()
                : Failure(GasRuntimeV1SupportProfileError.ClosedWorldShapeMismatch);
        }

        /// <summary>
        /// 校验一个 Effect 无隐藏 requirement/child/SetByCaller，且所有 payload range 连续不重叠。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateEffectEnvelope(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob effect,
            ref PayloadCursors cursors,
            int elementIndex)
        {
            if (effect.DefinitionId <= 0 || !IsFrozenAlive(in effect.TargetPolicy)
                || effect.ApplicationRequirementRange.Count != 0 || effect.OngoingRequirementRange.Count != 0
                || effect.RemovalRequirementRange.Count != 0 || effect.ImmunityRequirementRange.Count != 0
                || effect.DirectEffectProgramRange.Count != 0 || effect.SetByCallerRange.Count != 0
                || effect.TargetDataRange.Start != cursors.TargetData || effect.TargetDataRange.Count != 1
                || effect.EffectContextFieldRange.Start != cursors.Context || effect.EffectContextFieldRange.Count != 3
                || effect.CaptureRange.Start != cursors.Capture
                || effect.ModifierRange.Start != cursors.Modifier
                || effect.CueRange.Start != cursors.Cue
                || effect.ValueViewRange.Start != cursors.ValueView
                || effect.EvaluatorProgramRange.Start != cursors.Evaluator)
            {
                return Failure(GasRuntimeV1SupportProfileError.RangeUnsupported, effect.DefinitionId, elementIndex);
            }

            cursors.Advance(in effect);
            return ValidateSpecEnvelope(ref catalog, in effect, elementIndex);
        }

        /// <summary>
        /// 校验每个 Effect 的 StableAsc TargetData 与三个必需 context 字段。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateSpecEnvelope(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob effect,
            int elementIndex)
        {
            var target = catalog.TargetDataDescriptors[effect.TargetDataRange.Start];
            if (target.Variant != GasTargetDataVariant.StableAsc || target.FieldOrdinal != 0 || target.Required != 1)
                return Failure(GasRuntimeV1SupportProfileError.EffectUnsupported, effect.DefinitionId, elementIndex);

            var start = effect.EffectContextFieldRange.Start;
            if (!IsContext(ref catalog, start, GasEffectContextFieldKind.CausalityId, 0)
                || !IsContext(ref catalog, start + 1, GasEffectContextFieldKind.SourceAvatarBindingGeneration, 1)
                || !IsContext(ref catalog, start + 2, GasEffectContextFieldKind.TargetAvatarBindingGeneration, 2))
            {
                return Failure(GasRuntimeV1SupportProfileError.EffectUnsupported, effect.DefinitionId, elementIndex);
            }
            return Success();
        }

        /// <summary>
        /// 校验固定负数伤害 instant 与受限 Executed Cue。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateFixedInstant(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob effect,
            int healthLayoutIndex)
        {
            if (effect.CaptureRange.Count != 0 || effect.ModifierRange.Count != 1
                || effect.CueRange.Count != 1 || effect.ValueViewRange.Count != 0
                || effect.EvaluatorProgramRange.Count != 1 || effect.RequiredValueViews != GasAttributeValueViewMask.None
                || !IsNonPeriodic(in effect) || !IsEffectMaxima(in effect, 0, 1, 1, 0))
            {
                return Failure(GasRuntimeV1SupportProfileError.EffectUnsupported, effect.DefinitionId);
            }
            var modifier = catalog.Modifiers[effect.ModifierRange.Start];
            var instruction = catalog.EvaluatorInstructions[effect.EvaluatorProgramRange.Start];
            if (!IsHealthModifier(in modifier, healthLayoutIndex, effect.EvaluatorProgramRange, effect.CaptureRange)
                || instruction.Opcode != GasEvaluatorOpcode.PushConstant || instruction.ConstantValue >= 0f)
            {
                return Failure(GasRuntimeV1SupportProfileError.EffectUnsupported, effect.DefinitionId);
            }
            return ValidateCue(ref catalog, in effect, GasCuePhaseFlags.Executed);
        }

        /// <summary>
        /// 校验 9203 的 Source Attack Snapshot、六指令 evaluator 与 stack/period/expiry 策略。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidatePoison(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob effect,
            int healthLayoutIndex,
            int attackLayoutIndex)
        {
            if (effect.CaptureRange.Count != 1 || effect.ModifierRange.Count != 1
                || effect.CueRange.Count != 1 || effect.ValueViewRange.Count != 0
                || effect.EvaluatorProgramRange.Count != 6 || effect.RequiredValueViews != GasAttributeValueViewMask.None
                || effect.DurationTicks != 8 || effect.PeriodTicks != 2 || effect.StackLimit != 3
                || !IsPoisonPolicies(in effect) || !IsEffectMaxima(in effect, 1, 1, 6, 0))
            {
                return Failure(GasRuntimeV1SupportProfileError.EffectUnsupported, effect.DefinitionId);
            }
            var capture = catalog.CaptureDescriptors[effect.CaptureRange.Start];
            if (!IsAttackCapture(in capture, attackLayoutIndex))
                return Failure(GasRuntimeV1SupportProfileError.CaptureUnsupported, effect.DefinitionId);

            var modifier = catalog.Modifiers[effect.ModifierRange.Start];
            if (!IsHealthModifier(in modifier, healthLayoutIndex, effect.EvaluatorProgramRange, effect.CaptureRange)
                || !IsPoisonProgram(ref catalog, effect.EvaluatorProgramRange))
            {
                return Failure(GasRuntimeV1SupportProfileError.EffectUnsupported, effect.DefinitionId);
            }
            return ValidateCue(ref catalog, in effect, GasCuePhaseFlags.OnActive);
        }

        /// <summary>
        /// 校验 missing-health finisher 的三 ValueView 与十三指令负伤害公式。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateFinisher(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob effect,
            int healthLayoutIndex)
        {
            var expectedViews = GasAttributeValueViewMask.Base
                | GasAttributeValueViewMask.Current
                | GasAttributeValueViewMask.DefinitionMaxValue;
            if (effect.CaptureRange.Count != 0 || effect.ModifierRange.Count != 1
                || effect.CueRange.Count != 1 || effect.ValueViewRange.Count != 3
                || effect.EvaluatorProgramRange.Count != 13 || effect.RequiredValueViews != expectedViews
                || !IsNonPeriodic(in effect) || !IsEffectMaxima(in effect, 0, 1, 13, 3))
            {
                return Failure(GasRuntimeV1SupportProfileError.EffectUnsupported, effect.DefinitionId);
            }
            var modifier = catalog.Modifiers[effect.ModifierRange.Start];
            if (!IsHealthModifier(in modifier, healthLayoutIndex, effect.EvaluatorProgramRange, effect.CaptureRange)
                || !IsFinisherViews(ref catalog, in effect, healthLayoutIndex)
                || !IsFinisherProgram(ref catalog, effect.EvaluatorProgramRange))
            {
                return Failure(GasRuntimeV1SupportProfileError.EffectUnsupported, effect.DefinitionId);
            }
            return ValidateCue(ref catalog, in effect, GasCuePhaseFlags.Executed);
        }

        /// <summary>
        /// 校验 9203 capture 只读取 layout 2 的 Attack Source Base Snapshot。
        /// </summary>
        private static bool IsAttackCapture(in GasCaptureDescriptorBlob capture, int attackLayoutIndex)
        {
            return capture.CaptureOrdinal == 0
                && capture.Owner == GasCaptureOwner.Source
                && capture.Binding == GasCaptureBinding.Snapshot
                && capture.Phase == GasCapturePhase.SourceSpecProjection
                && capture.LiveScope == GasLiveCaptureScope.None
                && capture.GonePolicy == GasCaptureGonePolicy.RejectApplication
                && capture.ValueView == GasAttributeValueView.Base
                && capture.AttributeLayoutIndex == attackLayoutIndex
                && capture.ConsumerNodeOrdinal == 0
                && capture.ConsumerFieldOrdinal == 0;
        }

        /// <summary>
        /// 校验 modifier 的 Health Add、evaluator 与 capture range 均与所属 Effect 完全一致。
        /// </summary>
        private static bool IsHealthModifier(
            in GasModifierDefinitionBlob modifier,
            int healthLayoutIndex,
            GasCatalogRange evaluatorRange,
            GasCatalogRange captureRange)
        {
            return modifier.AttributeLayoutIndex == healthLayoutIndex
                && modifier.Operation == GasModifierOperation.Add
                && SameRange(modifier.EvaluatorProgramRange, evaluatorRange)
                && SameRange(modifier.CaptureRange, captureRange);
        }

        /// <summary>
        /// 校验 9203 postfix program 的精确运算顺序与 0.3 系数。
        /// </summary>
        private static bool IsPoisonProgram(ref GasDefinitionCatalogBlob catalog, GasCatalogRange range)
        {
            return IsInstruction(ref catalog, range.Start, GasEvaluatorOpcode.PushCapture, 0)
                && IsConstant(ref catalog, range.Start + 1, 0.3f)
                && IsInstruction(ref catalog, range.Start + 2, GasEvaluatorOpcode.Multiply)
                && IsInstruction(ref catalog, range.Start + 3, GasEvaluatorOpcode.PushStackCount)
                && IsInstruction(ref catalog, range.Start + 4, GasEvaluatorOpcode.Multiply)
                && IsInstruction(ref catalog, range.Start + 5, GasEvaluatorOpcode.Negate);
        }

        /// <summary>
        /// 校验 finisher 三个 ValueView 均绑定 Health，且顺序为 Base/Current/DefinitionMaxValue。
        /// </summary>
        private static bool IsFinisherViews(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob effect,
            int healthLayoutIndex)
        {
            var start = effect.ValueViewRange.Start;
            return IsValueView(ref catalog, start, healthLayoutIndex, GasAttributeValueView.Base)
                && IsValueView(ref catalog, start + 1, healthLayoutIndex, GasAttributeValueView.Current)
                && IsValueView(ref catalog, start + 2, healthLayoutIndex, GasAttributeValueView.DefinitionMaxValue);
        }

        /// <summary>
        /// 校验 finisher 的 canonical missing-health postfix 形状；数值 golden 由 production Catalog test 负责。
        /// </summary>
        private static bool IsFinisherProgram(ref GasDefinitionCatalogBlob catalog, GasCatalogRange range)
        {
            return IsInstruction(ref catalog, range.Start, GasEvaluatorOpcode.PushValueView, 2)
                && IsInstruction(ref catalog, range.Start + 1, GasEvaluatorOpcode.PushValueView, 0)
                && IsInstruction(ref catalog, range.Start + 2, GasEvaluatorOpcode.Subtract)
                && IsConstant(ref catalog, range.Start + 3, 0f)
                && IsInstruction(ref catalog, range.Start + 4, GasEvaluatorOpcode.Maximum)
                && IsPositiveConstant(ref catalog, range.Start + 5)
                && IsInstruction(ref catalog, range.Start + 6, GasEvaluatorOpcode.Multiply)
                && IsPositiveConstant(ref catalog, range.Start + 7)
                && IsInstruction(ref catalog, range.Start + 8, GasEvaluatorOpcode.Add)
                && IsNonNegativeConstant(ref catalog, range.Start + 9)
                && IsPositiveConstant(ref catalog, range.Start + 10)
                && IsInstruction(ref catalog, range.Start + 11, GasEvaluatorOpcode.Clamp)
                && IsInstruction(ref catalog, range.Start + 12, GasEvaluatorOpcode.Negate);
        }

        /// <summary>
        /// 校验受限 Cue 只有单一阶段，所有 Effect 共用同一个正 ID。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateCue(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob effect,
            GasCuePhaseFlags phase)
        {
            var cue = catalog.CueReferences[effect.CueRange.Start];
            return cue.CueDefinitionId == 9301
                && cue.CueDefinitionOrdinal == 0 && cue.Phases == phase
                ? Success()
                : Failure(GasRuntimeV1SupportProfileError.CueUnsupported, effect.DefinitionId);
        }

        /// <summary>
        /// 校验非 period Effect 的零生命周期与零 stack 策略。
        /// </summary>
        private static bool IsNonPeriodic(in GasGameplayEffectDefinitionBlob effect)
        {
            return effect.DurationTicks == 0 && effect.PeriodTicks == 0 && effect.StackLimit == 0
                && effect.StackKey == GasStackKeyFields.None && effect.StackPolicy == GasStackPolicy.None
                && effect.StackPayloadPolicy == GasStackPayloadPolicy.None
                && effect.StackLimitApplicationPolicy == GasStackLimitApplicationPolicy.None
                && effect.DurationRefreshPolicy == GasDurationRefreshPolicy.Never
                && effect.PeriodResetPolicy == GasPeriodResetPolicy.Never
                && effect.ExpiryPolicy == GasExpiryPolicy.Remove
                && effect.ExpiryPeriodPolicy == GasExpiryPeriodPolicy.Stop
                && effect.ExpirySameTickPolicy == GasExpirySameTickPolicy.ExpiryBeforePeriodDue
                && effect.InhibitTimePolicy == GasInhibitTimePolicy.DurationContinues
                && effect.InhibitedPeriodPolicy == GasInhibitedPeriodPolicy.None
                && effect.MissedPeriodPolicy == GasMissedPeriodPolicy.SkipNoCatchUp
                && effect.ExecuteOnApplication == 0;
        }

        /// <summary>
        /// 校验 9203 接受的 stack、period、expiry 与 inhibition 时间策略。
        /// </summary>
        private static bool IsPoisonPolicies(in GasGameplayEffectDefinitionBlob effect)
        {
            var expectedKey = GasStackKeyFields.Definition | GasStackKeyFields.TargetAsc | GasStackKeyFields.SourceAsc;
            return effect.StackKey == expectedKey
                && effect.StackPolicy == GasStackPolicy.AggregateBySource
                && effect.StackPayloadPolicy == GasStackPayloadPolicy.ReplaceLatestPayloadAndProvenance
                && effect.StackLimitApplicationPolicy == GasStackLimitApplicationPolicy.AcceptAndKeepLimit
                && effect.DurationRefreshPolicy == GasDurationRefreshPolicy.Never
                && effect.PeriodResetPolicy == GasPeriodResetPolicy.OnSuccessfulApplication
                && effect.ExpiryPolicy == GasExpiryPolicy.RemoveOneStackAndRefreshDuration
                && effect.ExpiryPeriodPolicy == GasExpiryPeriodPolicy.Reset
                && effect.ExpirySameTickPolicy == GasExpirySameTickPolicy.PeriodDueBeforeExpiry
                && effect.InhibitTimePolicy == GasInhibitTimePolicy.DurationContinues
                && effect.InhibitedPeriodPolicy == GasInhibitedPeriodPolicy.SkipExecution
                && effect.MissedPeriodPolicy == GasMissedPeriodPolicy.SkipNoCatchUp
                && effect.ExecuteOnApplication == 0;
        }

        /// <summary>
        /// 校验四维 target policy 为本轮唯一 FrozenAsc alive 支持值。
        /// </summary>
        private static bool IsFrozenAlive(in GasTargetPolicyBlob policy)
        {
            return policy.LogicalTarget == GasLogicalTargetPolicy.FrozenAsc
                && policy.Avatar == GasAvatarTargetPolicy.RequireSameAvatar
                && policy.Spatial == GasSpatialTargetPolicy.None
                && policy.Life == GasTargetLifePolicy.AliveOnly;
        }

        /// <summary>
        /// 返回 Health、Energy、Attack dense layout 的精确 Attribute ID。
        /// </summary>
        private static int ExpectedAttributeId(int index)
        {
            return index switch
            {
                0 => 1,
                1 => 2,
                2 => 4,
                _ => 0,
            };
        }

        /// <summary>
        /// 返回 Ability dense array 的精确 Definition ID。
        /// </summary>
        private static int ExpectedAbilityId(int index)
        {
            return index switch
            {
                0 => 9101,
                1 => 9102,
                2 => 9103,
                3 => 9104,
                _ => 0,
            };
        }

        /// <summary>
        /// 返回每个 Ability 唯一 direct Effect 的精确 ID。
        /// </summary>
        private static int ExpectedAbilityEffectId(int index)
        {
            return index switch
            {
                0 => 9201,
                1 => 9202,
                2 => 9207,
                3 => 9203,
                _ => 0,
            };
        }

        /// <summary>
        /// 返回按 ID 排序的 Effect dense array 的精确 Definition ID。
        /// </summary>
        private static int ExpectedEffectId(int index)
        {
            return index switch
            {
                0 => 9201,
                1 => 9202,
                2 => 9203,
                3 => 9207,
                _ => 0,
            };
        }

        /// <summary>
        /// 校验 Ability maxima 不声明本轮关闭的额外容量。
        /// </summary>
        private static bool IsAbilityMaxima(in GasDefinitionMaxima maxima)
        {
            return maxima.MaximumTargetCount == 1 && maxima.MaximumPlannedApplicationCount == 1
                && maxima.MaximumRequirementCount == 0 && maxima.MaximumCaptureDescriptorCount == 0
                && maxima.MaximumModifierCount == 0 && maxima.MaximumDirectProgramNodeCount == 1
                && maxima.MaximumDirectProgramOutputCount == 1 && maxima.MaximumCueCount == 0
                && maxima.MaximumValueViewCount == 0 && maxima.MaximumEvaluatorInstructionCount == 0
                && maxima.MaximumSetByCallerCount == 0 && maxima.MaximumTargetDataCount == 0
                && maxima.MaximumEffectContextFieldCount == 0;
        }

        /// <summary>
        /// 校验 Effect maxima 与其实际 capture/modifier/cue/evaluator/value-view 数量精确一致。
        /// </summary>
        private static bool IsEffectMaxima(
            in GasGameplayEffectDefinitionBlob effect,
            int captureCount,
            int cueCount,
            int evaluatorCount,
            int valueViewCount)
        {
            var maxima = effect.Maxima;
            return maxima.MaximumTargetCount == 1 && maxima.MaximumPlannedApplicationCount == 1
                && maxima.MaximumRequirementCount == 0 && maxima.MaximumCaptureDescriptorCount == captureCount
                && maxima.MaximumModifierCount == 1 && maxima.MaximumDirectProgramNodeCount == 0
                && maxima.MaximumDirectProgramOutputCount == 0 && maxima.MaximumCueCount == cueCount
                && maxima.MaximumValueViewCount == valueViewCount
                && maxima.MaximumEvaluatorInstructionCount == evaluatorCount
                && maxima.MaximumSetByCallerCount == 0 && maxima.MaximumTargetDataCount == 1
                && maxima.MaximumEffectContextFieldCount == 3;
        }

        /// <summary>
        /// 校验一条 context descriptor 的 kind、ordinal 与 required 位。
        /// </summary>
        private static bool IsContext(
            ref GasDefinitionCatalogBlob catalog,
            int index,
            GasEffectContextFieldKind kind,
            int ordinal)
        {
            var context = catalog.EffectContextFieldDescriptors[index];
            return context.Field == kind && context.FieldOrdinal == ordinal && context.Required == 1;
        }

        /// <summary>
        /// 校验一条 ValueView descriptor。
        /// </summary>
        private static bool IsValueView(
            ref GasDefinitionCatalogBlob catalog,
            int index,
            int layoutIndex,
            GasAttributeValueView view)
        {
            var descriptor = catalog.ValueViews[index];
            return descriptor.AttributeLayoutIndex == layoutIndex && descriptor.ValueView == view;
        }

        /// <summary>
        /// 校验一条无常量 evaluator 指令及其 operand。
        /// </summary>
        private static bool IsInstruction(
            ref GasDefinitionCatalogBlob catalog,
            int index,
            GasEvaluatorOpcode opcode,
            int operandIndex = 0)
        {
            var instruction = catalog.EvaluatorInstructions[index];
            return instruction.Opcode == opcode && instruction.OperandIndex == operandIndex
                && instruction.ConstantValue == 0f;
        }

        /// <summary>
        /// 校验一条精确 PushConstant 指令。
        /// </summary>
        private static bool IsConstant(ref GasDefinitionCatalogBlob catalog, int index, float value)
        {
            var instruction = catalog.EvaluatorInstructions[index];
            return instruction.Opcode == GasEvaluatorOpcode.PushConstant
                && instruction.OperandIndex == 0 && instruction.ConstantValue == value;
        }

        /// <summary>
        /// 校验正 PushConstant 指令。
        /// </summary>
        private static bool IsPositiveConstant(ref GasDefinitionCatalogBlob catalog, int index)
        {
            var instruction = catalog.EvaluatorInstructions[index];
            return instruction.Opcode == GasEvaluatorOpcode.PushConstant
                && instruction.OperandIndex == 0 && instruction.ConstantValue > 0f;
        }

        /// <summary>
        /// 校验非负 PushConstant 指令。
        /// </summary>
        private static bool IsNonNegativeConstant(ref GasDefinitionCatalogBlob catalog, int index)
        {
            var instruction = catalog.EvaluatorInstructions[index];
            return instruction.Opcode == GasEvaluatorOpcode.PushConstant
                && instruction.OperandIndex == 0 && instruction.ConstantValue >= 0f;
        }

        /// <summary>
        /// 比较两个 root array range 是否完全一致。
        /// </summary>
        private static bool SameRange(GasCatalogRange left, GasCatalogRange right)
        {
            return left.Start == right.Start && left.Count == right.Count;
        }

        /// <summary>
        /// 创建成功结果。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult Success()
        {
            return new GasRuntimeV1SupportProfileResult(GasRuntimeV1SupportProfileError.None);
        }

        /// <summary>
        /// 创建带 Definition 与元素位置的 typed reject。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult Failure(
            GasRuntimeV1SupportProfileError error,
            int definitionId = 0,
            int elementIndex = -1)
        {
            return new GasRuntimeV1SupportProfileResult(error, definitionId, elementIndex);
        }

        /// <summary>
        /// 追踪 Effect flat payload 的 canonical 连续区间，防止遗漏或重叠。
        /// </summary>
        private struct PayloadCursors
        {
            public int Capture;
            public int Modifier;
            public int Cue;
            public int ValueView;
            public int Evaluator;
            public int TargetData;
            public int Context;

            /// <summary>
            /// 按一个 Effect 的 range 数量推进全部 cursor。
            /// </summary>
            public void Advance(in GasGameplayEffectDefinitionBlob effect)
            {
                Capture += effect.CaptureRange.Count;
                Modifier += effect.ModifierRange.Count;
                Cue += effect.CueRange.Count;
                ValueView += effect.ValueViewRange.Count;
                Evaluator += effect.EvaluatorProgramRange.Count;
                TargetData += effect.TargetDataRange.Count;
                Context += effect.EffectContextFieldRange.Count;
            }

            /// <summary>
            /// 判断全部 payload root array 是否被 definition ranges 精确消费。
            /// </summary>
            public bool AllConsumed(ref GasDefinitionCatalogBlob catalog)
            {
                return Capture == catalog.CaptureDescriptors.Length
                    && Modifier == catalog.Modifiers.Length
                    && Cue == catalog.CueReferences.Length
                    && ValueView == catalog.ValueViews.Length
                    && Evaluator == catalog.EvaluatorInstructions.Length
                    && TargetData == catalog.TargetDataDescriptors.Length
                    && Context == catalog.EffectContextFieldDescriptors.Length;
            }
        }
    }
}
