using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 标识 ActiveEffect due maintenance 在结构、身份或时序上的确定性失败原因。
    /// </summary>
    internal enum GasActiveEffectLifecycleFailure : byte
    {
        None,
        InvalidBufferShape,
        InvalidIdentity,
        InvalidDefinition,
        InvalidTiming,
        RequirementMalformed,
        PeriodOrdinalOverflow,
        SlabMetadata,
        GenerationOverflow,
    }

    /// <summary>
    /// 汇总一次 target-owned ActiveEffect maintenance 的 due claim、inhibition 与 expiry 结果。
    /// </summary>
    internal struct GasActiveEffectLifecycleResult
    {
        public GasActiveEffectLifecycleFailure Failure;
        public int ProcessedCount;
        public int PeriodClaimCount;
        public int PeriodSkipCount;
        public int ExpiredCount;
        public int StackRefreshCount;
        public int InhibitedCount;
        public int ReactivatedCount;
        public int TombstoneCount;
        public int RecycledCount;
    }

    /// <summary>
    /// 在目标 ASC single-writer lane 中推进 ActiveEffect 的 due、ongoing inhibition 与 expiry。
    /// </summary>
    internal static class GasGameplayEffectLifecycleUtility
    {
        /// <summary>
        /// 预检全部 live ActiveEffect 后一次性推进 candidate tick，失败时不写入任何 authority。
        /// </summary>
        internal static bool TryProcessDue(
            ref GasDefinitionCatalogBlob catalog,
            ulong simulationEpoch,
            ulong candidateTick,
            DynamicBuffer<ActiveEffectSlot> activeEffects,
            DynamicBuffer<TagCountSlot> tagCounts,
            ref GasSlabHead activeEffectHead,
            int activeEffectCapacity,
            out GasActiveEffectLifecycleResult result)
        {
            result = default;
            var storage = new GasActiveEffectSlabStorage { Buffer = activeEffects };
            var validation = GasNonCompactingSlabAllocator.Validate(
                in activeEffectHead, ref storage, activeEffectCapacity);
            if (validation != GasSlabStorageFailure.None)
                return Fail(ref result, GasActiveEffectLifecycleFailure.SlabMetadata);
            if (!Preflight(
                    ref catalog,
                    simulationEpoch,
                    candidateTick,
                    activeEffects,
                    tagCounts,
                    out var preflightFailure))
                return Fail(ref result, preflightFailure);

            for (var index = 0; index < activeEffects.Length; index++)
            {
                var slot = activeEffects[index];
                if (slot.Header.StorageState != GasSlabSlotState.Live ||
                    slot.State != GasSlotBusinessState.Active)
                    continue;
                result.ProcessedCount++;
                var definition = catalog.GameplayEffects[slot.DefinitionIndex];
                ApplyOngoingTransition(
                    ref catalog,
                    in definition,
                    tagCounts,
                    ref slot,
                    ref result);
                ApplyPeriodTransition(
                    in definition,
                    candidateTick,
                    ref slot,
                    out _,
                    ref result);
                if (ApplyExpiryTransition(
                        in definition,
                        candidateTick,
                        ref slot,
                        ref result))
                {
                    if (GasNonCompactingSlabAllocator.TryMarkTombstone(
                            in activeEffectHead, ref storage, index) != GasSlabStorageFailure.None)
                        return Fail(ref result, GasActiveEffectLifecycleFailure.SlabMetadata);
                    slot.Header = storage.ReadHeader(index);
                    slot.State = GasSlotBusinessState.Terminal;
                    activeEffects[index] = slot;
                    continue;
                }
                activeEffects[index] = slot;
            }
            return true;
        }

        /// <summary>
        /// 只读估算当前 ASC 的 period claim 与 modifier 需求，供 WholeTick admission 预留事实和 Attribute scratch。
        /// </summary>
        internal static bool TryEstimateDue(
            ref GasDefinitionCatalogBlob catalog,
            ulong simulationEpoch,
            ulong candidateTick,
            DynamicBuffer<ActiveEffectSlot> activeEffects,
            DynamicBuffer<TagCountSlot> tagCounts,
            int activeEffectCapacity,
            out int claimCount,
            out int mutationCount,
            out GasActiveEffectLifecycleFailure failure)
        {
            claimCount = 0;
            mutationCount = 0;
            failure = GasActiveEffectLifecycleFailure.None;
            if (!Preflight(
                    ref catalog,
                    simulationEpoch,
                    candidateTick,
                    activeEffects,
                    tagCounts,
                    out failure))
                return false;

            for (var index = 0; index < activeEffects.Length; index++)
            {
                var slot = activeEffects[index];
                if (slot.Header.StorageState != GasSlabSlotState.Live ||
                    slot.State != GasSlotBusinessState.Active)
                    continue;
                var definition = catalog.GameplayEffects[slot.DefinitionIndex];
                if (!TryGetPeriodDecision(
                        ref catalog,
                        in definition,
                        in slot,
                        tagCounts,
                        candidateTick,
                        out var claim,
                        out var execute,
                        out _,
                        out failure))
                    return false;
                if (claim == 0)
                    continue;
                if (slot.ActiveCycleOrdinal == uint.MaxValue ||
                    slot.PeriodExecutionOrdinal >= int.MaxValue / 1024)
                {
                    failure = GasActiveEffectLifecycleFailure.PeriodOrdinalOverflow;
                    return false;
                }
                claimCount++;
                if (execute != 0)
                {
                    mutationCount += definition.ModifierRange.Count;
                    if (mutationCount < 0)
                    {
                        failure = GasActiveEffectLifecycleFailure.PeriodOrdinalOverflow;
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 按 ongoing requirement、抑制策略与 expiry 仲裁返回单槽 period claim 决策，不写入任何 authority。
        /// </summary>
        internal static bool TryGetPeriodDecision(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob definition,
            in ActiveEffectSlot slot,
            DynamicBuffer<TagCountSlot> tagCounts,
            ulong candidateTick,
            out byte claim,
            out byte execute,
            out uint nextOrdinal,
            out GasActiveEffectLifecycleFailure failure)
        {
            claim = 0;
            execute = 0;
            nextOrdinal = slot.PeriodExecutionOrdinal;
            failure = GasActiveEffectLifecycleFailure.None;
            var projected = slot;
            if (!TryEvaluateInhibition(
                    ref catalog,
                    in definition,
                    tagCounts,
                    slot.Inhibited,
                    out projected.Inhibited,
                    out failure))
                return false;
            if (!ShouldAdvancePeriod(in definition, in projected, candidateTick))
                return true;
            if (slot.ActiveCycleOrdinal == uint.MaxValue ||
                slot.PeriodExecutionOrdinal >= int.MaxValue / 1024)
            {
                failure = GasActiveEffectLifecycleFailure.PeriodOrdinalOverflow;
                return false;
            }
            claim = 1;
            nextOrdinal = slot.PeriodExecutionOrdinal + 1;
            execute = projected.Inhibited != 0 &&
                      definition.InhibitedPeriodPolicy == GasInhibitedPeriodPolicy.SkipExecution
                ? (byte)0
                : (byte)1;
            return true;
        }

        /// <summary>
        /// 在引用交接完成后回收所有 ActiveEffect tombstone，保留稳定槽索引并递增 generation。
        /// </summary>
        internal static bool TryRecycleTombstones(
            DynamicBuffer<ActiveEffectSlot> activeEffects,
            ref GasSlabHead activeEffectHead,
            int activeEffectCapacity,
            out int recycled,
            out GasActiveEffectLifecycleFailure failure)
        {
            recycled = 0;
            failure = GasActiveEffectLifecycleFailure.None;
            var storage = new GasActiveEffectSlabStorage { Buffer = activeEffects };
            if (GasNonCompactingSlabAllocator.Validate(
                    in activeEffectHead, ref storage, activeEffectCapacity) != GasSlabStorageFailure.None)
                return SetFailure(out failure, GasActiveEffectLifecycleFailure.SlabMetadata);
            for (var index = 0; index < activeEffects.Length; index++)
            {
                if (activeEffects[index].Header.StorageState != GasSlabSlotState.Tombstone)
                    continue;
                if (activeEffects[index].Header.Generation == uint.MaxValue)
                    return SetFailure(out failure, GasActiveEffectLifecycleFailure.GenerationOverflow);
            }
            for (var index = 0; index < activeEffects.Length; index++)
            {
                if (activeEffects[index].Header.StorageState != GasSlabSlotState.Tombstone)
                    continue;
                if (GasNonCompactingSlabAllocator.TryRecycleTombstone(
                        ref activeEffectHead, ref storage, index) != GasSlabStorageFailure.None)
                    return SetFailure(out failure, GasActiveEffectLifecycleFailure.SlabMetadata);
                recycled++;
            }
            return true;
        }

        /// <summary>
        /// 在任何写入前验证所有 live slot 的身份、definition、requirements 与可能的 tick 运算。
        /// </summary>
        private static bool Preflight(
            ref GasDefinitionCatalogBlob catalog,
            ulong simulationEpoch,
            ulong candidateTick,
            DynamicBuffer<ActiveEffectSlot> activeEffects,
            DynamicBuffer<TagCountSlot> tagCounts,
            out GasActiveEffectLifecycleFailure failure)
        {
            failure = GasActiveEffectLifecycleFailure.None;
            for (var index = 0; index < activeEffects.Length; index++)
            {
                var slot = activeEffects[index];
                if (slot.Header.StorageState != GasSlabSlotState.Live)
                    continue;
                if (slot.State != GasSlotBusinessState.Active)
                    return SetFailure(out failure, GasActiveEffectLifecycleFailure.InvalidIdentity);
                if (!ValidateSlotIdentity(in slot, index, simulationEpoch))
                    return SetFailure(out failure, GasActiveEffectLifecycleFailure.InvalidIdentity);
                if (slot.DefinitionIndex < 0 || slot.DefinitionIndex >= catalog.GameplayEffects.Length)
                    return SetFailure(out failure, GasActiveEffectLifecycleFailure.InvalidDefinition);
                var definition = catalog.GameplayEffects[slot.DefinitionIndex];
                if (!ValidateDefinition(in definition, in slot))
                    return SetFailure(out failure, GasActiveEffectLifecycleFailure.InvalidDefinition);
                if (definition.OngoingRequirementRange.Start < 0 ||
                    definition.OngoingRequirementRange.Start > catalog.Requirements.Length)
                    return SetFailure(out failure, GasActiveEffectLifecycleFailure.RequirementMalformed);
                GasGameplayEffectRequirements.Evaluate(
                    ref catalog,
                    definition.OngoingRequirementRange,
                    tagCounts,
                    out _,
                    out var malformed);
                if (malformed)
                    return SetFailure(out failure, GasActiveEffectLifecycleFailure.RequirementMalformed);
                if (!ValidateDueArithmetic(in definition, in slot, candidateTick))
                    return SetFailure(out failure, GasActiveEffectLifecycleFailure.InvalidTiming);
                if (ShouldAdvancePeriod(in definition, in slot, candidateTick) &&
                    (slot.ActiveCycleOrdinal == uint.MaxValue ||
                     slot.PeriodExecutionOrdinal == uint.MaxValue))
                    return SetFailure(out failure, GasActiveEffectLifecycleFailure.PeriodOrdinalOverflow);
            }
            return true;
        }

        /// <summary>
        /// 校验槽位 handle 与物理索引代际一致，拒绝 stale/reused ActiveEffect 引用。
        /// </summary>
        private static bool ValidateSlotIdentity(
            in ActiveEffectSlot slot,
            int index,
            ulong simulationEpoch)
        {
            return slot.Handle.IsValid &&
                   slot.Handle.SimulationEpoch == simulationEpoch &&
                   slot.Handle.SlotIndex == index &&
                   slot.Handle.SlotGeneration == slot.Header.Generation &&
                   slot.Handle.OwnerAsc.IsValid &&
                   slot.SourceAsc.IsValid;
        }

        /// <summary>
        /// 校验 ActiveEffect lifetime、stack、period 与当前槽的闭合定义契约。
        /// </summary>
        private static bool ValidateDefinition(
            in GasGameplayEffectDefinitionBlob definition,
            in ActiveEffectSlot slot)
        {
            if (definition.Lifetime != GasEffectLifetimePolicy.Duration &&
                definition.Lifetime != GasEffectLifetimePolicy.Infinite)
                return false;
            if (definition.DurationTicks < 0 || definition.PeriodTicks < 0 ||
                definition.StackLimit < 0 || slot.StackCount <= 0 || slot.Inhibited > 1)
                return false;
            if (slot.StartTick > slot.EndTick ||
                (slot.NextPeriodTick != 0 && slot.NextPeriodTick < slot.StartTick))
                return false;
            if (definition.StackLimit > 0 && slot.StackCount > definition.StackLimit)
                return false;
            if (definition.StackPolicy == GasStackPolicy.None && slot.StackCount != 1)
                return false;
            if (definition.StackPolicy != GasStackPolicy.None &&
                (definition.StackKey & GasStackKeyFields.StackingId) != 0)
                return false;
            if (definition.Lifetime == GasEffectLifetimePolicy.Infinite &&
                (definition.DurationTicks != 0 || slot.EndTick != ulong.MaxValue))
                return false;
            if (definition.Lifetime == GasEffectLifetimePolicy.Duration &&
                (definition.DurationTicks <= 0 || slot.EndTick <= slot.StartTick))
                return false;
            if (definition.PeriodTicks == 0)
                return slot.NextPeriodTick == 0 && ValidatePolicies(in definition);
            return slot.NextPeriodTick != 0 &&
                   definition.InhibitedPeriodPolicy != GasInhibitedPeriodPolicy.None &&
                   ValidatePolicies(in definition);
        }

        /// <summary>
        /// 校验 lifecycle policy 枚举闭集与 stack key 必要维度，避免 Runtime 猜测缺省语义。
        /// </summary>
        private static bool ValidatePolicies(in GasGameplayEffectDefinitionBlob definition)
        {
            if (definition.StackPolicy < GasStackPolicy.None ||
                definition.StackPolicy > GasStackPolicy.AggregateByTarget ||
                definition.StackPayloadPolicy < GasStackPayloadPolicy.None ||
                definition.StackPayloadPolicy > GasStackPayloadPolicy.ReplaceLatestPayloadAndProvenance ||
                definition.StackLimitApplicationPolicy < GasStackLimitApplicationPolicy.None ||
                definition.StackLimitApplicationPolicy > GasStackLimitApplicationPolicy.AcceptAndKeepLimit ||
                definition.DurationRefreshPolicy < GasDurationRefreshPolicy.Never ||
                definition.DurationRefreshPolicy > GasDurationRefreshPolicy.OnSuccessfulApplication ||
                definition.PeriodResetPolicy < GasPeriodResetPolicy.Never ||
                definition.PeriodResetPolicy > GasPeriodResetPolicy.OnSuccessfulApplication)
                return false;
            if (definition.ExpiryPolicy < GasExpiryPolicy.Remove ||
                definition.ExpiryPolicy > GasExpiryPolicy.RemoveOneStackAndRefreshDuration ||
                definition.ExpiryPeriodPolicy < GasExpiryPeriodPolicy.Stop ||
                definition.ExpiryPeriodPolicy > GasExpiryPeriodPolicy.Reset ||
                definition.ExpirySameTickPolicy < GasExpirySameTickPolicy.ExpiryBeforePeriodDue ||
                definition.ExpirySameTickPolicy > GasExpirySameTickPolicy.PeriodDueBeforeExpiry ||
                definition.InhibitTimePolicy < GasInhibitTimePolicy.PauseDuration ||
                definition.InhibitTimePolicy > GasInhibitTimePolicy.DurationContinues ||
                definition.InhibitedPeriodPolicy < GasInhibitedPeriodPolicy.None ||
                definition.InhibitedPeriodPolicy > GasInhibitedPeriodPolicy.ContinueExecution ||
                definition.MissedPeriodPolicy < GasMissedPeriodPolicy.SkipNoCatchUp ||
                definition.MissedPeriodPolicy > GasMissedPeriodPolicy.CatchUpBounded ||
                definition.ExecuteOnApplication > 1)
                return false;
            if (definition.InhibitTimePolicy == GasInhibitTimePolicy.PauseDuration)
                return false;
            // v1 只物化单次 due claim，未实现 ExecuteOnce/CatchUpBounded 的追赶预算。
            if (definition.MissedPeriodPolicy != GasMissedPeriodPolicy.SkipNoCatchUp)
                return false;
            if (definition.StackPolicy == GasStackPolicy.None)
                return definition.StackLimit == 0 &&
                       definition.StackKey == GasStackKeyFields.None &&
                       definition.StackPayloadPolicy == GasStackPayloadPolicy.None &&
                       definition.StackLimitApplicationPolicy == GasStackLimitApplicationPolicy.None &&
                       definition.ExpiryPolicy == GasExpiryPolicy.Remove;
            if (definition.StackLimit <= 0)
                return false;
            const GasStackKeyFields required = GasStackKeyFields.Definition |
                                                GasStackKeyFields.TargetAsc |
                                                GasStackKeyFields.SourceAsc;
            const GasStackKeyFields known = required | GasStackKeyFields.StackingId;
            return (definition.StackKey & required) == required &&
                   (definition.StackKey & ~known) == 0 &&
                   (definition.StackKey & GasStackKeyFields.StackingId) == 0 &&
                   definition.StackPayloadPolicy != GasStackPayloadPolicy.None &&
                   definition.StackLimitApplicationPolicy != GasStackLimitApplicationPolicy.None;
        }

        /// <summary>
        /// 预验证 period refresh 与 duration refresh 可能使用的 candidate tick 加法不会回绕。
        /// </summary>
        private static bool ValidateDueArithmetic(
            in GasGameplayEffectDefinitionBlob definition,
            in ActiveEffectSlot slot,
            ulong candidateTick)
        {
            if (slot.EndTick <= candidateTick &&
                definition.ExpiryPolicy == GasExpiryPolicy.RemoveOneStackAndRefreshDuration &&
                slot.StackCount > 1 &&
                !TryAddTick(candidateTick, (ulong)definition.DurationTicks, out _))
                return false;
            if (slot.EndTick <= candidateTick &&
                definition.ExpiryPolicy == GasExpiryPolicy.RemoveOneStackAndRefreshDuration &&
                slot.StackCount > 1 &&
                definition.ExpiryPeriodPolicy == GasExpiryPeriodPolicy.Reset &&
                definition.PeriodTicks > 0 &&
                !TryAddTick(candidateTick, (ulong)definition.PeriodTicks, out _))
                return false;
            if (ShouldAdvancePeriod(in definition, in slot, candidateTick) &&
                !TryAddTick(candidateTick, (ulong)definition.PeriodTicks, out _))
                return false;
            return true;
        }

        /// <summary>
        /// 判断 period due 是否应推进 next due，处理抑制与 expiry 同 tick 仲裁。
        /// </summary>
        internal static bool ShouldAdvancePeriod(
            in GasGameplayEffectDefinitionBlob definition,
            in ActiveEffectSlot slot,
            ulong candidateTick)
        {
            if (definition.PeriodTicks <= 0 || slot.NextPeriodTick == 0 ||
                slot.NextPeriodTick > candidateTick)
                return false;
            if (slot.Inhibited != 0 &&
                definition.InhibitedPeriodPolicy == GasInhibitedPeriodPolicy.PauseSchedule)
                return false;
            if (slot.EndTick != ulong.MaxValue && slot.EndTick <= candidateTick &&
                slot.NextPeriodTick > slot.EndTick)
                return false;
            return !(slot.EndTick != ulong.MaxValue &&
                     slot.NextPeriodTick == slot.EndTick &&
                     definition.ExpirySameTickPolicy == GasExpirySameTickPolicy.ExpiryBeforePeriodDue);
        }

        /// <summary>
        /// 评估 ongoing requirement 对当前槽的有效 inhibition 状态，供 admission 与 target writer 共用。
        /// </summary>
        internal static bool TryEvaluateInhibition(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob definition,
            DynamicBuffer<TagCountSlot> tagCounts,
            byte currentInhibited,
            out byte inhibited,
            out GasActiveEffectLifecycleFailure failure)
        {
            inhibited = currentInhibited;
            failure = GasActiveEffectLifecycleFailure.None;
            if (definition.OngoingRequirementRange.Count == 0)
            {
                inhibited = 0;
                return true;
            }
            var satisfied = GasGameplayEffectRequirements.Evaluate(
                ref catalog,
                definition.OngoingRequirementRange,
                tagCounts,
                out _,
                out var malformed);
            if (malformed)
            {
                failure = GasActiveEffectLifecycleFailure.RequirementMalformed;
                return false;
            }
            inhibited = satisfied ? (byte)0 : (byte)1;
            return true;
        }

        /// <summary>
        /// 按 ongoing requirement 在 Active 与 Inhibited 之间切换，保留槽身份和 stack/context。
        /// </summary>
        private static void ApplyOngoingTransition(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob definition,
            DynamicBuffer<TagCountSlot> tagCounts,
            ref ActiveEffectSlot slot,
            ref GasActiveEffectLifecycleResult result)
        {
            if (definition.OngoingRequirementRange.Count == 0)
            {
                if (slot.Inhibited != 0)
                {
                    slot.Inhibited = 0;
                    result.ReactivatedCount++;
                }
                return;
            }
            var satisfied = GasGameplayEffectRequirements.Evaluate(
                ref catalog,
                definition.OngoingRequirementRange,
                tagCounts,
                out _,
                out _);
            if (!satisfied && slot.Inhibited == 0)
            {
                slot.Inhibited = 1;
                result.InhibitedCount++;
            }
            else if (satisfied && slot.Inhibited != 0)
            {
                slot.Inhibited = 0;
                result.ReactivatedCount++;
            }
        }

        /// <summary>
        /// claim 当前 tick 的 period due 一次并推进 next due，抑制策略决定是否执行或跳过。
        /// </summary>
        private static void ApplyPeriodTransition(
            in GasGameplayEffectDefinitionBlob definition,
            ulong candidateTick,
            ref ActiveEffectSlot slot,
            out bool periodAdvanced,
            ref GasActiveEffectLifecycleResult result)
        {
            periodAdvanced = ShouldAdvancePeriod(in definition, in slot, candidateTick);
            if (!periodAdvanced)
                return;
            var inhibited = slot.Inhibited != 0;
            if (inhibited && definition.InhibitedPeriodPolicy == GasInhibitedPeriodPolicy.SkipExecution)
                result.PeriodSkipCount++;
            else
                result.PeriodClaimCount++;
            slot.ActiveCycleOrdinal++;
            slot.PeriodExecutionOrdinal++;
            TryAddTick(candidateTick, (ulong)definition.PeriodTicks, out slot.NextPeriodTick);
        }

        /// <summary>
        /// 按 same-tick 与 stack expiry policy 决定保留一层刷新还是终止槽位。
        /// </summary>
        private static bool ApplyExpiryTransition(
            in GasGameplayEffectDefinitionBlob definition,
            ulong candidateTick,
            ref ActiveEffectSlot slot,
            ref GasActiveEffectLifecycleResult result)
        {
            if (slot.EndTick == ulong.MaxValue || slot.EndTick > candidateTick)
                return false;
            result.ExpiredCount++;
            if (definition.ExpiryPolicy == GasExpiryPolicy.RemoveOneStackAndRefreshDuration &&
                slot.StackCount > 1)
            {
                slot.StackCount--;
                TryAddTick(candidateTick, (ulong)definition.DurationTicks, out slot.EndTick);
                if (definition.ExpiryPeriodPolicy == GasExpiryPeriodPolicy.Stop)
                    slot.NextPeriodTick = 0;
                else if (definition.PeriodTicks > 0)
                    TryAddTick(candidateTick, (ulong)definition.PeriodTicks, out slot.NextPeriodTick);
                result.StackRefreshCount++;
                return false;
            }
            result.TombstoneCount++;
            return true;
        }

        /// <summary>
        /// 写入失败结果并返回 false，统一保持调用方可判断的 typed failure。
        /// </summary>
        private static bool Fail(
            ref GasActiveEffectLifecycleResult result,
            GasActiveEffectLifecycleFailure failure)
        {
            result.Failure = failure;
            return false;
        }

        /// <summary>
        /// 设置独立输出 failure 并返回 false，供 tombstone 回收预检复用。
        /// </summary>
        private static bool SetFailure(
            out GasActiveEffectLifecycleFailure failure,
            GasActiveEffectLifecycleFailure value)
        {
            failure = value;
            return false;
        }

        /// <summary>
        /// 以减法检查 tick 加法，避免周期或刷新时间回绕。
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
    }
}
