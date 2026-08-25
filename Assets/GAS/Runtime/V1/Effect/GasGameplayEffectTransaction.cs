using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 在目标 ASC single-writer lane 内完成 GameplayEffect 的 requirement、stack、modifier 与 ActiveEffect 提交。
    /// </summary>
    internal static class GasGameplayEffectTransaction
    {
        /// <summary>
        /// 以已冻结的 application intent 执行一条 target-owned effect transaction。
        /// </summary>
        internal static bool TryApply(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectApplicationRequest request,
            DynamicBuffer<ActiveEffectSlot> activeEffects,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<AttributeDirtyWord> dirtyWords,
            DynamicBuffer<TagCountSlot> tagCounts,
            DynamicBuffer<TagPresenceWord> tagPresence,
            ref GasSlabHead activeEffectHead,
            int activeEffectCapacity,
            NativeArray<float> captures,
            NativeArray<float> valueViews,
            NativeArray<float> evaluatorStack,
            out GasGameplayEffectApplicationResult result)
        {
            result = new GasGameplayEffectApplicationResult
            {
                ApplicationId = request.ApplicationId,
            };
            if (!ValidateRequest(
                    ref catalog,
                    in request,
                    activeEffects,
                    attributes,
                    dirtyWords,
                    tagCounts,
                    tagPresence,
                    activeEffectCapacity,
                    captures,
                    valueViews,
                    evaluatorStack))
            {
                result.Outcome = GasGameplayEffectApplicationOutcome.RejectedStaleBinding;
                result.Failure = GasGameplayEffectTransactionFailure.InvalidBufferShape;
                return false;
            }

            var definition = catalog.GameplayEffects[request.DefinitionIndex];
            if (!MatchesLifePolicy(definition.TargetPolicy.Life, request.TargetIsAlive))
            {
                result.Outcome = GasGameplayEffectApplicationOutcome.RejectedTargetLife;
                result.Failure = GasGameplayEffectTransactionFailure.InvalidTargetLife;
                return false;
            }
            var applicationRequirementSatisfied = GasGameplayEffectRequirements.Evaluate(
                ref catalog,
                definition.ApplicationRequirementRange,
                tagCounts,
                out _,
                out var applicationRequirementMalformed);
            if (applicationRequirementMalformed)
            {
                SetDefinitionFailure(ref result);
                return false;
            }
            if (!applicationRequirementSatisfied)
            {
                result.Outcome = GasGameplayEffectApplicationOutcome.RejectedRequirement;
                result.Failure = GasGameplayEffectTransactionFailure.RequirementRejected;
                return false;
            }
            if (definition.ImmunityRequirementRange.Count > 0)
            {
                var immunitySatisfied = GasGameplayEffectRequirements.Evaluate(
                    ref catalog,
                    definition.ImmunityRequirementRange,
                    tagCounts,
                    out _,
                    out var immunityMalformed);
                if (immunityMalformed)
                {
                    SetDefinitionFailure(ref result);
                    return false;
                }
                if (immunitySatisfied)
                {
                    result.Outcome = GasGameplayEffectApplicationOutcome.RejectedImmunity;
                    result.Failure = GasGameplayEffectTransactionFailure.ImmunityRejected;
                    return false;
                }
            }

            if (!TryFindStack(in definition, in request, activeEffects, out var existingIndex))
            {
                return ApplyNew(
                    ref catalog,
                    in definition,
                    in request,
                    activeEffects,
                    attributes,
                    dirtyWords,
                    tagCounts,
                    tagPresence,
                    ref activeEffectHead,
                    activeEffectCapacity,
                    captures,
                    valueViews,
                    evaluatorStack,
                    ref result);
            }

            return ApplyExistingStack(
                ref catalog,
                in definition,
                in request,
                existingIndex,
                activeEffects,
                attributes,
                dirtyWords,
                captures,
                valueViews,
                evaluatorStack,
                ref result);
        }

        /// <summary>
        /// 创建 Instant 或新的 Duration/Infinite ActiveEffect，并在同一事务应用 modifiers。
        /// </summary>
        private static bool ApplyNew(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob definition,
            in GasGameplayEffectApplicationRequest request,
            DynamicBuffer<ActiveEffectSlot> activeEffects,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<AttributeDirtyWord> dirtyWords,
            DynamicBuffer<TagCountSlot> tagCounts,
            DynamicBuffer<TagPresenceWord> tagPresence,
            ref GasSlabHead activeEffectHead,
            int activeEffectCapacity,
            NativeArray<float> captures,
            NativeArray<float> valueViews,
            NativeArray<float> evaluatorStack,
            ref GasGameplayEffectApplicationResult result)
        {
            var isInstant = definition.Lifetime == GasEffectLifetimePolicy.Instant ||
                            definition.Lifetime == GasEffectLifetimePolicy.InstantExecution;
            var endTick = 0UL;
            var periodTick = 0UL;
            if (!isInstant && !TryBuildTiming(
                    in definition,
                    request.StartTick,
                    out endTick,
                    out periodTick))
            {
                result.Outcome = GasGameplayEffectApplicationOutcome.RejectedDefinition;
                result.Failure = GasGameplayEffectTransactionFailure.InvalidDefinition;
                return false;
            }

            if (!PreflightModifiers(
                    ref catalog,
                    in definition,
                    captures,
                    valueViews,
                    evaluatorStack,
                    attributes,
                    ref result))
                return false;

            var allocation = default(GasSlabAllocation);
            if (!isInstant)
            {
                var storage = new GasActiveEffectSlabStorage { Buffer = activeEffects };
                if (GasNonCompactingSlabAllocator.TryAllocate(
                        ref activeEffectHead,
                        ref storage,
                        activeEffectCapacity,
                        out allocation) != GasSlabStorageFailure.None)
                {
                    result.Outcome = GasGameplayEffectApplicationOutcome.InfrastructureFault;
                    result.Failure = GasGameplayEffectTransactionFailure.ActiveEffectCapacity;
                    return false;
                }
            }

            if (!ApplyModifierWrites(
                    ref catalog,
                    in definition,
                    in request,
                    attributes,
                    dirtyWords,
                    captures,
                    valueViews,
                    evaluatorStack,
                    ref result))
            {
                if (!isInstant)
                    RollbackAllocation(in allocation, activeEffects, ref activeEffectHead);
                return false;
            }

            if (isInstant)
            {
                result.Outcome = GasGameplayEffectApplicationOutcome.AppliedInstant;
                return true;
            }

            var handle = new ActiveEffectHandle(
                request.SimulationEpoch,
                request.TargetAsc,
                allocation.SlotIndex,
                allocation.Generation);
            activeEffects[allocation.SlotIndex] = new ActiveEffectSlot
            {
                Header = allocation.LiveHeader,
                Handle = handle,
                SourceAsc = request.SourceAsc,
                DefinitionIndex = request.DefinitionIndex,
                StartTick = request.StartTick,
                EndTick = endTick,
                NextPeriodTick = periodTick,
                StackCount = 1,
                ActiveCycleOrdinal = 0,
                State = GasSlotBusinessState.Active,
                Inhibited = 0,
            };
            result.ActiveEffect = handle;
            result.Outcome = GasGameplayEffectApplicationOutcome.CreatedActive;
            return true;
        }

        /// <summary>
        /// 按冻结 stack policy 合并已有槽，禁止超过上限或重复累加 modifier。
        /// </summary>
        private static bool ApplyExistingStack(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob definition,
            in GasGameplayEffectApplicationRequest request,
            int existingIndex,
            DynamicBuffer<ActiveEffectSlot> activeEffects,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<AttributeDirtyWord> dirtyWords,
            NativeArray<float> captures,
            NativeArray<float> valueViews,
            NativeArray<float> evaluatorStack,
            ref GasGameplayEffectApplicationResult result)
        {
            if (definition.StackLimit <= 0 ||
                definition.StackLimitApplicationPolicy == GasStackLimitApplicationPolicy.None)
            {
                result.Outcome = GasGameplayEffectApplicationOutcome.RejectedStackPolicy;
                result.Failure = GasGameplayEffectTransactionFailure.StackRejected;
                return false;
            }
            var slot = activeEffects[existingIndex];
            if (slot.Header.StorageState != GasSlabSlotState.Live ||
                slot.State != GasSlotBusinessState.Active)
            {
                result.Outcome = GasGameplayEffectApplicationOutcome.RejectedStaleBinding;
                result.Failure = GasGameplayEffectTransactionFailure.InvalidIdentity;
                return false;
            }
            if (slot.StackCount >= definition.StackLimit &&
                definition.StackLimitApplicationPolicy == GasStackLimitApplicationPolicy.RejectAtLimit)
            {
                result.Outcome = GasGameplayEffectApplicationOutcome.RejectedStackPolicy;
                result.Failure = GasGameplayEffectTransactionFailure.StackRejected;
                return false;
            }
            if (slot.StackCount < definition.StackLimit)
                slot.StackCount++;
            if (definition.DurationRefreshPolicy == GasDurationRefreshPolicy.OnSuccessfulApplication &&
                !TryBuildEndTick(in definition, request.StartTick, out slot.EndTick))
            {
                result.Outcome = GasGameplayEffectApplicationOutcome.RejectedDefinition;
                result.Failure = GasGameplayEffectTransactionFailure.InvalidDefinition;
                return false;
            }
            if (definition.PeriodResetPolicy == GasPeriodResetPolicy.OnSuccessfulApplication &&
                definition.PeriodTicks > 0)
            {
                if (!TryAddTick(request.StartTick, (ulong)definition.PeriodTicks, out slot.NextPeriodTick))
                {
                    result.Outcome = GasGameplayEffectApplicationOutcome.RejectedDefinition;
                    result.Failure = GasGameplayEffectTransactionFailure.InvalidDefinition;
                    return false;
                }
            }
            activeEffects[existingIndex] = slot;
            result.ActiveEffect = slot.Handle;
            result.Outcome = GasGameplayEffectApplicationOutcome.MergedStack;
            return true;
        }

        /// <summary>
        /// 以 definition/target/source 三元身份查找可合并的 ActiveEffect 槽。
        /// </summary>
        private static bool TryFindStack(
            in GasGameplayEffectDefinitionBlob definition,
            in GasGameplayEffectApplicationRequest request,
            DynamicBuffer<ActiveEffectSlot> activeEffects,
            out int slotIndex)
        {
            slotIndex = -1;
            if (definition.StackPolicy == GasStackPolicy.None)
                return false;
            for (var index = 0; index < activeEffects.Length; index++)
            {
                var slot = activeEffects[index];
                if (slot.Header.StorageState != GasSlabSlotState.Live ||
                    slot.State != GasSlotBusinessState.Active ||
                    slot.DefinitionIndex != request.DefinitionIndex ||
                    !slot.Handle.OwnerAsc.Equals(request.TargetAsc))
                    continue;
                if ((definition.StackKey & GasStackKeyFields.SourceAsc) != 0 &&
                    !slot.SourceAsc.Equals(request.SourceAsc))
                    continue;
                slotIndex = index;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 按 modifier evaluator 结果计算 Base/Current delta，并保证所有 evaluator 先成功再写 authority。
        /// </summary>
        private static bool ApplyModifierWrites(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob definition,
            in GasGameplayEffectApplicationRequest request,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<AttributeDirtyWord> dirtyWords,
            NativeArray<float> captures,
            NativeArray<float> valueViews,
            NativeArray<float> evaluatorStack,
            ref GasGameplayEffectApplicationResult result)
        {
            for (var offset = 0; offset < definition.ModifierRange.Count; offset++)
            {
                var modifier = catalog.Modifiers[definition.ModifierRange.Start + offset];
                var failure = GasGameplayEffectEvaluator.TryEvaluate(
                    ref catalog,
                    modifier.EvaluatorProgramRange,
                    captures,
                    valueViews,
                    1,
                    evaluatorStack,
                    out var magnitude);
                if (failure != GasEvaluatorFailure.None ||
                    !TryGetDeltas(in modifier, magnitude, attributes, out var baseDelta, out var currentDelta))
                {
                    result.Outcome = GasGameplayEffectApplicationOutcome.RejectedDefinition;
                    result.Failure = GasGameplayEffectTransactionFailure.EvaluatorFailure;
                    return false;
                }
                if (!GasAttributeTransactionUtility.TryApplyDelta(
                        ref catalog,
                        modifier.AttributeLayoutIndex,
                        baseDelta,
                        currentDelta,
                        attributes,
                        dirtyWords,
                        out _,
                        out _))
                {
                    result.Outcome = GasGameplayEffectApplicationOutcome.InfrastructureFault;
                    result.Failure = GasGameplayEffectTransactionFailure.AttributeMutationFailure;
                    return false;
                }
                result.AppliedModifierCount++;
            }
            return true;
        }

        /// <summary>
        /// 在第一条 Attribute 写入前验证全部 evaluator、delta、buffer 与 revision 预算。
        /// </summary>
        private static bool PreflightModifiers(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob definition,
            NativeArray<float> captures,
            NativeArray<float> valueViews,
            NativeArray<float> evaluatorStack,
            DynamicBuffer<AttributeValueSlot> attributes,
            ref GasGameplayEffectApplicationResult result)
        {
            if (definition.ModifierRange.Count == 0)
                return true;
            if (!IsRangeValid(in definition.ModifierRange, catalog.Modifiers.Length))
                return FailEvaluator(ref result);
            for (var offset = 0; offset < definition.ModifierRange.Count; offset++)
            {
                var modifier = catalog.Modifiers[definition.ModifierRange.Start + offset];
                if (!TryBuildShadowValue(
                        ref catalog,
                        in definition,
                        offset,
                        captures,
                        valueViews,
                        evaluatorStack,
                        attributes,
                        out var shadow,
                        out var attributeFailure))
                    return attributeFailure ? FailAttribute(ref result) : FailEvaluator(ref result);

                if (!TryEvaluateMagnitude(
                        ref catalog,
                        in modifier,
                        captures,
                        valueViews,
                        evaluatorStack,
                        out var magnitude) ||
                    !TryGetDeltas(in modifier, magnitude, in shadow, out var baseDelta, out var currentDelta))
                    return FailEvaluator(ref result);
                if (!GasAttributeTransactionUtility.TrySimulateDelta(
                        ref catalog,
                        modifier.AttributeLayoutIndex,
                        baseDelta,
                        currentDelta,
                        in shadow,
                        out _,
                        out var simulationFailure))
                    return IsDefinitionMutationFailure(simulationFailure)
                        ? FailEvaluator(ref result)
                        : FailAttribute(ref result);
            }
            return true;
        }

        /// <summary>
        /// 重放当前 modifier 之前的同属性序列，得到不写 authority 的顺序 shadow 值。
        /// </summary>
        private static bool TryBuildShadowValue(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob definition,
            int currentOffset,
            NativeArray<float> captures,
            NativeArray<float> valueViews,
            NativeArray<float> evaluatorStack,
            DynamicBuffer<AttributeValueSlot> attributes,
            out AttributeValueSlot shadow,
            out bool attributeFailure)
        {
            shadow = default;
            attributeFailure = false;
            var targetModifier = catalog.Modifiers[definition.ModifierRange.Start + currentOffset];
            var attributeLayoutIndex = targetModifier.AttributeLayoutIndex;
            if (attributeLayoutIndex < 0 || attributeLayoutIndex >= attributes.Length)
            {
                // modifier 目标索引属于 definition 结构错误，不应伪装成运行时 Attribute authority 故障。
                attributeFailure = false;
                return false;
            }
            shadow = attributes[attributeLayoutIndex];
            for (var offset = 0; offset < currentOffset; offset++)
            {
                var prior = catalog.Modifiers[definition.ModifierRange.Start + offset];
                if (prior.AttributeLayoutIndex != attributeLayoutIndex)
                    continue;
                if (!TryEvaluateMagnitude(
                        ref catalog,
                        in prior,
                        captures,
                        valueViews,
                        evaluatorStack,
                        out var magnitude) ||
                    !TryGetDeltas(in prior, magnitude, in shadow, out var baseDelta, out var currentDelta))
                    return false;
                if (!GasAttributeTransactionUtility.TrySimulateDelta(
                        ref catalog,
                        attributeLayoutIndex,
                        baseDelta,
                        currentDelta,
                        in shadow,
                        out var nextShadow,
                        out var simulationFailure))
                {
                    attributeFailure = !IsDefinitionMutationFailure(simulationFailure);
                    return false;
                }
                shadow = nextShadow;
            }
            return true;
        }

        /// <summary>
        /// 在冻结 evaluator 输入上计算一个 modifier magnitude，不访问 ECS authority。
        /// </summary>
        private static bool TryEvaluateMagnitude(
            ref GasDefinitionCatalogBlob catalog,
            in GasModifierDefinitionBlob modifier,
            NativeArray<float> captures,
            NativeArray<float> valueViews,
            NativeArray<float> evaluatorStack,
            out float magnitude)
        {
            return GasGameplayEffectEvaluator.TryEvaluate(
                       ref catalog,
                       modifier.EvaluatorProgramRange,
                       captures,
                       valueViews,
                       1,
                       evaluatorStack,
                       out magnitude) == GasEvaluatorFailure.None;
        }

        /// <summary>
        /// 区分 evaluator 产生的数值/索引错误与目标 Attribute authority 的运行时故障。
        /// </summary>
        private static bool IsDefinitionMutationFailure(GasAttributeMutationFailure failure)
        {
            return failure == GasAttributeMutationFailure.InvalidAttributeIndex ||
                   failure == GasAttributeMutationFailure.InvalidCatalogEntry ||
                   failure == GasAttributeMutationFailure.NonFiniteInput ||
                   failure == GasAttributeMutationFailure.NonFiniteResult;
        }

        /// <summary>
        /// 设置 catalog/definition 结构拒绝结果，避免与业务 requirement 拒绝混淆。
        /// </summary>
        private static bool SetDefinitionFailure(ref GasGameplayEffectApplicationResult result)
        {
            result.Outcome = GasGameplayEffectApplicationOutcome.RejectedDefinition;
            result.Failure = GasGameplayEffectTransactionFailure.InvalidDefinition;
            return false;
        }

        /// <summary>
        /// 设置 evaluator 拒绝结果并保持 target authority 不变。
        /// </summary>
        private static bool FailEvaluator(ref GasGameplayEffectApplicationResult result)
        {
            result.Outcome = GasGameplayEffectApplicationOutcome.RejectedDefinition;
            result.Failure = GasGameplayEffectTransactionFailure.EvaluatorFailure;
            return false;
        }

        /// <summary>
        /// 设置 Attribute 预检拒绝结果并保持 target authority 不变。
        /// </summary>
        private static bool FailAttribute(ref GasGameplayEffectApplicationResult result)
        {
            result.Outcome = GasGameplayEffectApplicationOutcome.InfrastructureFault;
            result.Failure = GasGameplayEffectTransactionFailure.AttributeMutationFailure;
            return false;
        }

        /// <summary>
        /// 将 Add/Multiply/Divide/Override 统一转换为当前 Attribute 的 Base/Current delta。
        /// </summary>
        private static bool TryGetDeltas(
            in GasModifierDefinitionBlob modifier,
            float magnitude,
            DynamicBuffer<AttributeValueSlot> attributes,
            out float baseDelta,
            out float currentDelta)
        {
            if (modifier.AttributeLayoutIndex < 0 ||
                modifier.AttributeLayoutIndex >= attributes.Length ||
                !IsFinite(magnitude))
            {
                baseDelta = 0f;
                currentDelta = 0f;
                return false;
            }
            var value = attributes[modifier.AttributeLayoutIndex];
            return TryGetDeltas(
                in modifier,
                magnitude,
                in value,
                out baseDelta,
                out currentDelta);
        }

        /// <summary>
        /// 以 shadow Attribute 值计算 modifier delta，避免预演读取已写入 authority 的旧快照。
        /// </summary>
        private static bool TryGetDeltas(
            in GasModifierDefinitionBlob modifier,
            float magnitude,
            in AttributeValueSlot value,
            out float baseDelta,
            out float currentDelta)
        {
            baseDelta = 0f;
            currentDelta = 0f;
            if (!IsFinite(magnitude) || !IsFinite(value.Base) || !IsFinite(value.Current))
                return false;
            switch (modifier.Operation)
            {
                case GasModifierOperation.Add:
                    baseDelta = magnitude;
                    currentDelta = magnitude;
                    break;
                case GasModifierOperation.Multiply:
                    baseDelta = value.Base * magnitude - value.Base;
                    currentDelta = value.Current * magnitude - value.Current;
                    break;
                case GasModifierOperation.Divide:
                    if (magnitude == 0f)
                        return false;
                    baseDelta = value.Base / magnitude - value.Base;
                    currentDelta = value.Current / magnitude - value.Current;
                    break;
                case GasModifierOperation.Override:
                    baseDelta = magnitude - value.Base;
                    currentDelta = magnitude - value.Current;
                    break;
                default:
                    return false;
            }
            return IsFinite(baseDelta) && IsFinite(currentDelta);
        }

        /// <summary>
        /// 校验 request 的 Epoch、owner、definition 与输入数组形状。
        /// </summary>
        private static bool ValidateRequest(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectApplicationRequest request,
            DynamicBuffer<ActiveEffectSlot> activeEffects,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<AttributeDirtyWord> dirtyWords,
            DynamicBuffer<TagCountSlot> tagCounts,
            DynamicBuffer<TagPresenceWord> tagPresence,
            int activeEffectCapacity,
            NativeArray<float> captures,
            NativeArray<float> valueViews,
            NativeArray<float> evaluatorStack)
        {
            if (request.SimulationEpoch == 0 ||
                !request.SourceAsc.IsValid ||
                !request.TargetAsc.IsValid ||
                request.ApplicationId == 0 ||
                request.DefinitionIndex < 0 ||
                request.DefinitionIndex >= catalog.GameplayEffects.Length ||
                request.TargetIsAlive > 1)
                return false;
            var attributeCount = catalog.AttributeLayout.Entries.Length;
            var tagCount = catalog.TagCatalog.Entries.Length;
            return activeEffectCapacity >= 0 && activeEffects.Length <= activeEffectCapacity &&
                   attributes.Length == attributeCount &&
                   dirtyWords.Length == WordCount(attributeCount) &&
                   tagCounts.Length == tagCount && tagPresence.Length == WordCount(tagCount) &&
                   request.CaptureValueCount == captures.Length &&
                   request.ValueViewCount == valueViews.Length &&
                   AreFinite(captures) && AreFinite(valueViews) &&
                   (catalog.GameplayEffects[request.DefinitionIndex].ModifierRange.Count == 0 ||
                    evaluatorStack.IsCreated && evaluatorStack.Length > 0);
        }

        /// <summary>
        /// 验证 evaluator 输入数组全部为有限值，避免把数值污染带入 target authority。
        /// </summary>
        private static bool AreFinite(NativeArray<float> values)
        {
            for (var index = 0; index < values.Length; index++)
                if (!IsFinite(values[index]))
                    return false;
            return true;
        }

        /// <summary>
        /// 验证固定长度位图所需的 64 位字数。
        /// </summary>
        private static int WordCount(int count)
        {
            return count <= 0 ? 0 : ((count - 1) / 64) + 1;
        }

        /// <summary>
        /// 以减法验证 Catalog range，避免起点与长度相加回绕。
        /// </summary>
        private static bool IsRangeValid(in GasCatalogRange range, int length)
        {
            return range.Start >= 0 && range.Count > 0 && range.Start <= length &&
                   range.Count <= length - range.Start;
        }

        /// <summary>
        /// 判断目标生命策略是否允许当前 target snapshot。
        /// </summary>
        private static bool MatchesLifePolicy(GasTargetLifePolicy policy, byte targetIsAlive)
        {
            switch (policy)
            {
                case GasTargetLifePolicy.AliveOnly:
                    return targetIsAlive != 0;
                case GasTargetLifePolicy.RequireDead:
                    return targetIsAlive == 0;
                case GasTargetLifePolicy.AnyLifeState:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 计算新 ActiveEffect 的结束与下一次 period tick，并拒绝整数回绕。
        /// </summary>
        private static bool TryBuildTiming(
            in GasGameplayEffectDefinitionBlob definition,
            ulong startTick,
            out ulong endTick,
            out ulong periodTick)
        {
            periodTick = 0;
            if (definition.Lifetime == GasEffectLifetimePolicy.Infinite)
                endTick = ulong.MaxValue;
            else if (!TryBuildEndTick(in definition, startTick, out endTick))
                return false;
            if (definition.PeriodTicks > 0 &&
                !TryAddTick(startTick, (ulong)definition.PeriodTicks, out periodTick))
                return false;
            return true;
        }

        /// <summary>
        /// 计算有限 duration 的结束 tick。
        /// </summary>
        private static bool TryBuildEndTick(
            in GasGameplayEffectDefinitionBlob definition,
            ulong startTick,
            out ulong endTick)
        {
            endTick = 0;
            return definition.DurationTicks > 0 &&
                   TryAddTick(startTick, (ulong)definition.DurationTicks, out endTick);
        }

        /// <summary>
        /// 使用减法检测 tick 加法溢出。
        /// </summary>
        private static bool TryAddTick(ulong left, ulong right, out ulong value)
        {
            if (right > ulong.MaxValue - left)
            {
                value = 0;
                return false;
            }
            value = left + right;
            return true;
        }

        /// <summary>
        /// 回滚尚未发布业务身份的 ActiveEffect 分配，并立即回收到 free-list。
        /// </summary>
        private static bool RollbackAllocation(
            in GasSlabAllocation allocation,
            DynamicBuffer<ActiveEffectSlot> activeEffects,
            ref GasSlabHead activeEffectHead)
        {
            var storage = new GasActiveEffectSlabStorage { Buffer = activeEffects };
            if (GasNonCompactingSlabAllocator.TryMarkTombstone(
                    in activeEffectHead, ref storage, allocation.SlotIndex) != GasSlabStorageFailure.None)
                return false;
            return GasNonCompactingSlabAllocator.TryRecycleTombstone(
                       ref activeEffectHead,
                       ref storage,
                       allocation.SlotIndex) == GasSlabStorageFailure.None;
        }

        /// <summary>
        /// 在不依赖高版本 runtime API 的前提下拒绝 NaN 与无穷值。
        /// </summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
