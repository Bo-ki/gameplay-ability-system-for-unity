using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 在 WholeTick admission 后执行已冻结的 Ability owner-local no-fail 事务。
    /// </summary>
    internal static class GasAbilityOwnerTransaction
    {
        /// <summary>
        /// 先释放当前 Tick 已 due 的 cooldown gate，再允许解释 canonical owner plans。
        /// </summary>
        internal static bool ReleaseDueCooldowns(
            ulong candidateTick,
            ref GasDefinitionCatalogBlob catalog,
            DynamicBuffer<CooldownGateSlot> cooldowns,
            DynamicBuffer<TagCountSlot> tagCounts,
            DynamicBuffer<TagPresenceWord> tagPresence,
            ref GasSlabHead cooldownHead)
        {
            var storage = new GasCooldownGateSlabStorage { Buffer = cooldowns };
            for (var index = 0; index < cooldowns.Length; index++)
            {
                var slot = cooldowns[index];
                if (slot.Header.StorageState != GasSlabSlotState.Live ||
                    slot.State != GasSlotBusinessState.Active || slot.EndTick > candidateTick)
                    continue;
                if (slot.Header.Generation == uint.MaxValue ||
                    (slot.OwnedTagIndex >= 0 &&
                     !GasTagTransactionUtility.TryValidateDelta(
                         ref catalog,
                         slot.OwnedTagIndex,
                         -1,
                         tagCounts,
                         tagPresence,
                         out _)))
                    return false;
            }
            for (var index = 0; index < cooldowns.Length; index++)
            {
                var slot = cooldowns[index];
                if (slot.Header.StorageState != GasSlabSlotState.Live ||
                    slot.State != GasSlotBusinessState.Active || slot.EndTick > candidateTick)
                    continue;
                if (slot.OwnedTagIndex >= 0 &&
                    !GasTagTransactionUtility.TryValidateDelta(
                        ref catalog,
                        slot.OwnedTagIndex,
                        -1,
                        tagCounts,
                        tagPresence,
                        out _))
                    return false;
                if (GasNonCompactingSlabAllocator.TryMarkTombstone(
                        in cooldownHead, ref storage, index) != GasSlabStorageFailure.None ||
                    GasNonCompactingSlabAllocator.TryRecycleTombstone(
                        ref cooldownHead, ref storage, index) != GasSlabStorageFailure.None)
                    return false;
                if (slot.OwnedTagIndex >= 0)
                {
                    GasTagTransactionUtility.TryApplyDelta(
                        ref catalog,
                        slot.OwnedTagIndex,
                        -1,
                        tagCounts,
                        tagPresence,
                        out _,
                        out _);
                }
                slot = cooldowns[index];
                slot.State = GasSlotBusinessState.Terminal;
                cooldowns[index] = slot;
            }
            return true;
        }

        /// <summary>
        /// 按计划种类执行 Activation 分配、Commit 原子事务或 Ending 屏障。
        /// </summary>
        internal static bool ApplyPlan(
            ref GasOwnerPlanRecord plan,
            ulong simulationEpoch,
            ulong candidateTick,
            in GasScaleProfile profile,
            ref GasDefinitionCatalogBlob catalog,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<CooldownGateSlot> cooldowns,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<TagCountSlot> tagCounts,
            DynamicBuffer<TagPresenceWord> tagPresence,
            ref AscSlabHeads heads)
        {
            return ApplyPlanCore(
                ref plan,
                simulationEpoch,
                candidateTick,
                in profile,
                ref catalog,
                grants,
                activations,
                cooldowns,
                attributes,
                default(DynamicBuffer<AttributeDirtyWord>),
                false,
                tagCounts,
                tagPresence,
                ref heads);
        }

        /// <summary>
        /// 执行带 Attribute dirty 位图的 owner-local no-fail 事务入口。
        /// </summary>
        internal static bool ApplyPlan(
            ref GasOwnerPlanRecord plan,
            ulong simulationEpoch,
            ulong candidateTick,
            in GasScaleProfile profile,
            ref GasDefinitionCatalogBlob catalog,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<CooldownGateSlot> cooldowns,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<AttributeDirtyWord> attributeDirtyWords,
            DynamicBuffer<TagCountSlot> tagCounts,
            DynamicBuffer<TagPresenceWord> tagPresence,
            ref AscSlabHeads heads)
        {
            return ApplyPlanCore(
                ref plan,
                simulationEpoch,
                candidateTick,
                in profile,
                ref catalog,
                grants,
                activations,
                cooldowns,
                attributes,
                attributeDirtyWords,
                true,
                tagCounts,
                tagPresence,
                ref heads);
        }

        /// <summary>
        /// 按计划种类执行 owner-local 事务，并在主路径统一接入 Attribute/Tag utility。
        /// </summary>
        private static bool ApplyPlanCore(
            ref GasOwnerPlanRecord plan,
            ulong simulationEpoch,
            ulong candidateTick,
            in GasScaleProfile profile,
            ref GasDefinitionCatalogBlob catalog,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<CooldownGateSlot> cooldowns,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<AttributeDirtyWord> attributeDirtyWords,
            bool writeAttributeDirty,
            DynamicBuffer<TagCountSlot> tagCounts,
            DynamicBuffer<TagPresenceWord> tagPresence,
            ref AscSlabHeads heads)
        {
            if (plan.BusinessAccepted == 0)
                return true;
            switch (plan.CommandKind)
            {
                case GasBoundaryCommandKind.Activate:
                    return ApplyActivate(ref plan, simulationEpoch, candidateTick, profile.MaxAbilityActivationCount,
                        grants, activations, ref heads.AbilityActivation);
                case GasBoundaryCommandKind.Commit:
                    return ApplyCommit(ref plan, simulationEpoch, candidateTick, profile.MaxCooldownGateCount,
                        ref catalog, activations, cooldowns, attributes, attributeDirtyWords,
                        writeAttributeDirty, tagCounts, tagPresence,
                        ref heads.CooldownGate);
                case GasBoundaryCommandKind.Cancel:
                    return BeginEnding(in plan, activations);
                default:
                    return true;
            }
        }

        /// <summary>
        /// 分配一个 RunningUncommitted Activation 并接入 grant child 链。
        /// </summary>
        private static bool ApplyActivate(
            ref GasOwnerPlanRecord plan,
            ulong simulationEpoch,
            ulong candidateTick,
            int hardCapacity,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            ref GasSlabHead activationHead)
        {
            if (plan.GrantedAbility.SlotIndex < 0 || plan.GrantedAbility.SlotIndex >= grants.Length)
                return false;
            var storage = new GasAbilityActivationSlabStorage { Buffer = activations };
            var failure = GasNonCompactingSlabAllocator.TryAllocate(
                ref activationHead, ref storage, hardCapacity, out var allocation);
            if (failure != GasSlabStorageFailure.None)
                return false;
            var handle = new AbilityActivationHandle(
                simulationEpoch, plan.OwnerAsc, allocation.SlotIndex, allocation.Generation);
            var grant = grants[plan.GrantedAbility.SlotIndex];
            activations[allocation.SlotIndex] = new AbilityActivationSlot
            {
                Header = allocation.LiveHeader,
                Handle = handle,
                GrantedAbility = grant.Handle,
                CausalityId = plan.StableSequence,
                ActivationSequence = plan.StableSequence,
                StartTick = candidateTick,
                Phase = GasAbilityActivationPhase.RunningUncommitted,
                LastCommandResult = GasAbilityCommandResult.Activated,
            };
            grant.ChildActivationCount++;
            grants[grant.Handle.SlotIndex] = grant;
            plan.Activation = handle;
            return true;
        }

        /// <summary>
        /// 原子提交 phase、cost、cooldown gate 与 cooldown-owned Tag contribution。
        /// </summary>
        private static bool ApplyCommit(
            ref GasOwnerPlanRecord plan,
            ulong simulationEpoch,
            ulong candidateTick,
            int cooldownCapacity,
            ref GasDefinitionCatalogBlob catalog,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<CooldownGateSlot> cooldowns,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<AttributeDirtyWord> attributeDirtyWords,
            bool writeAttributeDirty,
            DynamicBuffer<TagCountSlot> tagCounts,
            DynamicBuffer<TagPresenceWord> tagPresence,
            ref GasSlabHead cooldownHead)
        {
            if (!HasLiveActivation(plan.Activation, activations))
                return false;
            if (!ValidateCommitMutations(
                    in plan,
                    ref catalog,
                    attributes,
                    attributeDirtyWords,
                    writeAttributeDirty,
                    tagCounts,
                    tagPresence,
                    candidateTick))
                return false;
            var allocation = default(GasSlabAllocation);
            if (plan.RequiresCooldownSlot != 0)
            {
                var storage = new GasCooldownGateSlabStorage { Buffer = cooldowns };
                if (GasNonCompactingSlabAllocator.TryAllocate(
                        ref cooldownHead, ref storage, cooldownCapacity, out allocation) !=
                    GasSlabStorageFailure.None)
                    return false;
            }
            if (!ApplyCost(
                    in plan,
                    ref catalog,
                    attributes,
                    attributeDirtyWords,
                    writeAttributeDirty))
            {
                return plan.RequiresCooldownSlot == 0 ||
                       RollbackCooldownAllocation(cooldowns, in allocation, ref cooldownHead);
            }
            if (plan.RequiresCooldownSlot != 0 &&
                !WriteCooldown(in plan, simulationEpoch, candidateTick, in allocation,
                    ref catalog, cooldowns, tagCounts, tagPresence))
            {
                RollbackCooldownAllocation(cooldowns, in allocation, ref cooldownHead);
                return false;
            }
            var activation = activations[plan.Activation.SlotIndex];
            activation.Phase = GasAbilityActivationPhase.Committed;
            activation.CommitSequence = plan.StableSequence;
            activation.LastCommandResult = GasAbilityCommandResult.Committed;
            activations[plan.Activation.SlotIndex] = activation;
            return true;
        }

        /// <summary>
        /// 在 Commit 首次写入前验证 cost、owned Tag 与 tick 加法，建立复合事务 no-fail 前提。
        /// </summary>
        private static bool ValidateCommitMutations(
            in GasOwnerPlanRecord plan,
            ref GasDefinitionCatalogBlob catalog,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<AttributeDirtyWord> attributeDirtyWords,
            bool writeAttributeDirty,
            DynamicBuffer<TagCountSlot> tagCounts,
            DynamicBuffer<TagPresenceWord> tagPresence,
            ulong candidateTick)
        {
            if (plan.CooldownDurationTicks > 0 &&
                candidateTick > ulong.MaxValue - (ulong)plan.CooldownDurationTicks)
                return false;
            if (plan.CostAttributeLayoutIndex >= 0)
            {
                var valid = writeAttributeDirty
                    ? GasAttributeTransactionUtility.TryValidateDelta(
                        ref catalog,
                        plan.CostAttributeLayoutIndex,
                        plan.CostBaseDelta,
                        plan.CostCurrentDelta,
                        attributes,
                        attributeDirtyWords,
                        out _,
                        out _)
                    : GasAttributeTransactionUtility.TryValidateDelta(
                        ref catalog,
                        plan.CostAttributeLayoutIndex,
                        plan.CostBaseDelta,
                        plan.CostCurrentDelta,
                        attributes,
                        out _,
                        out _);
                if (!valid)
                    return false;
            }
            return plan.CooldownOwnedTagIndex < 0 ||
                   GasTagTransactionUtility.TryValidateDelta(
                       ref catalog,
                       plan.CooldownOwnedTagIndex,
                       1,
                       tagCounts,
                       tagPresence,
                       out _);
        }

        /// <summary>
        /// 直接修改同一 ASC 的 Attribute authority，不创建 cost Effect 或镜像。
        /// </summary>
        private static bool ApplyCost(
            in GasOwnerPlanRecord plan,
            ref GasDefinitionCatalogBlob catalog,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<AttributeDirtyWord> attributeDirtyWords,
            bool writeAttributeDirty)
        {
            if (plan.CostAttributeLayoutIndex < 0)
                return true;
            if (writeAttributeDirty)
            {
                return GasAttributeTransactionUtility.TryApplyDelta(
                    ref catalog,
                    plan.CostAttributeLayoutIndex,
                    plan.CostBaseDelta,
                    plan.CostCurrentDelta,
                    attributes,
                    attributeDirtyWords,
                    out _,
                    out _);
            }
            return GasAttributeTransactionUtility.TryApplyDelta(
                ref catalog,
                plan.CostAttributeLayoutIndex,
                plan.CostBaseDelta,
                plan.CostCurrentDelta,
                attributes,
                out _,
                out _);
        }

        /// <summary>
        /// 写入独立于 Activation 生命周期的 cooldown gate，并应用其可选 Tag contribution。
        /// </summary>
        private static bool WriteCooldown(
            in GasOwnerPlanRecord plan,
            ulong simulationEpoch,
            ulong candidateTick,
            in GasSlabAllocation allocation,
            ref GasDefinitionCatalogBlob catalog,
            DynamicBuffer<CooldownGateSlot> cooldowns,
            DynamicBuffer<TagCountSlot> tagCounts,
            DynamicBuffer<TagPresenceWord> tagPresence)
        {
            if (plan.CooldownOwnedTagIndex >= 0 &&
                !GasTagTransactionUtility.TryApplyDelta(
                    ref catalog,
                    plan.CooldownOwnedTagIndex,
                    1,
                    tagCounts,
                    tagPresence,
                    out _,
                    out _))
                return false;
            cooldowns[allocation.SlotIndex] = new CooldownGateSlot
            {
                Header = allocation.LiveHeader,
                Handle = new CooldownGateHandle(
                    simulationEpoch, plan.OwnerAsc, allocation.SlotIndex, allocation.Generation),
                GrantedAbility = plan.GrantedAbility,
                SourceCommitActivation = plan.Activation,
                GateKey = plan.CooldownGateKey,
                AbilityDefinitionId = catalog.Abilities[plan.DefinitionIndex].DefinitionId,
                OwnedTagIndex = plan.CooldownOwnedTagIndex,
                StartTick = candidateTick,
                EndTick = candidateTick + (ulong)plan.CooldownDurationTicks,
                State = GasSlotBusinessState.Active,
            };
            return true;
        }

        /// <summary>
        /// 回滚本次 Commit 尚未写入业务字段的 cooldown 分配，保持 slab 与 Attribute/Tag 原子性。
        /// </summary>
        private static bool RollbackCooldownAllocation(
            DynamicBuffer<CooldownGateSlot> cooldowns,
            in GasSlabAllocation allocation,
            ref GasSlabHead cooldownHead)
        {
            var storage = new GasCooldownGateSlabStorage { Buffer = cooldowns };
            return GasNonCompactingSlabAllocator.TryMarkTombstone(
                       in cooldownHead, ref storage, allocation.SlotIndex) == GasSlabStorageFailure.None &&
                   GasNonCompactingSlabAllocator.TryRecycleTombstone(
                       ref cooldownHead, ref storage, allocation.SlotIndex) == GasSlabStorageFailure.None;
        }

        /// <summary>
        /// 首次 Cancel 将 Activation 与全部 child Continuation 置入 Ending，暂不撤销 cooldown 或远端 work。
        /// </summary>
        private static bool BeginEnding(
            in GasOwnerPlanRecord plan,
            DynamicBuffer<AbilityActivationSlot> activations)
        {
            if (!HasLiveActivation(plan.Activation, activations))
                return false;
            var activation = activations[plan.Activation.SlotIndex];
            if (activation.Phase == GasAbilityActivationPhase.Ending ||
                activation.Phase == GasAbilityActivationPhase.Ended)
                return true;
            activation.Phase = GasAbilityActivationPhase.Ending;
            activation.EndReason = plan.EndReason;
            activation.WasCancelled = plan.WasCancelled;
            activation.LastCommandResult = GasAbilityCommandResult.Ended;
            activations[plan.Activation.SlotIndex] = activation;
            return true;
        }

        /// <summary>
        /// 判断 Activation handle 是否仍命中原 generation 的 live 槽。
        /// </summary>
        private static bool HasLiveActivation(
            in AbilityActivationHandle handle,
            DynamicBuffer<AbilityActivationSlot> activations)
        {
            if (!handle.IsValid || handle.SlotIndex < 0 || handle.SlotIndex >= activations.Length)
                return false;
            var slot = activations[handle.SlotIndex];
            return slot.Header.StorageState == GasSlabSlotState.Live &&
                   slot.Header.Generation == handle.SlotGeneration;
        }

    }
}
