using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 执行 Ability tombstone 回收、grant removal 三态与 Ending 收尾的 ASC-local maintenance。
    /// </summary>
    internal static class GasAbilityLifecycleMaintenance
    {
        /// <summary>
        /// 由 owner writer 创建下一 Tick 才可处理的 GrantedAbility removal 命令。
        /// </summary>
        internal static bool TryEnqueueGrantedRemoval(
            in BattleInstanceHandle battle,
            in OwnerAscHandle owner,
            in GrantedAbilityHandle grant,
            DynamicBuffer<GrantedAbilitySlot> grants,
            GasGrantedAbilityRemovalPolicy policy,
            ulong availableTick,
            ulong commandSequence,
            int hardCapacity,
            DynamicBuffer<PendingCommand> commands,
            ref GasSlabHead commandHead)
        {
            if (!battle.IsValid || !owner.IsValid || !grant.IsValid ||
                !grant.OwnerAsc.Equals(owner) || grant.SimulationEpoch != battle.SimulationEpoch ||
                availableTick == 0 || commandSequence == 0 || hardCapacity <= 0 ||
                !IsKnownRemovalPolicy(policy) ||
                !TryResolveGrant(in grant, grants, out var liveGrant) ||
                liveGrant.RemovalState != GasGrantedAbilityRemovalState.None ||
                !IsValidGrantProvenance(in liveGrant))
                return false;
            var storage = new GasPendingCommandSlabStorage { Buffer = commands };
            if (GasNonCompactingSlabAllocator.TryAllocate(
                    ref commandHead, ref storage, hardCapacity, out var allocation) !=
                GasSlabStorageFailure.None)
                return false;
            commands[allocation.SlotIndex] = new PendingCommand
            {
                Header = allocation.LiveHeader,
                BattleInstance = battle,
                SourceAsc = owner,
                TargetAsc = owner,
                GrantedAbility = grant,
                AvailableTick = availableTick,
                CommandSequence = commandSequence,
                CommandKind = (int)GasAbilityPendingCommandKind.RequestGrantedRemoval,
                GrantedRemovalPolicy = policy,
                GrantedRemovalSourceKind = liveGrant.GrantSourceKind,
                GrantedRemovalSourceStableId = liveGrant.GrantSourceStableId,
                GrantedRemovalActiveEffect = liveGrant.GrantingActiveEffect,
                GrantedRemovalApplicationId = liveGrant.GrantApplicationId,
                GrantedRemovalContextId = liveGrant.GrantContextId,
                State = GasSlotBusinessState.Pending,
            };
            return true;
        }

        /// <summary>
        /// 在 owner command 前处理 due removal，并回收更早 Tick 已完成交接的 tombstones。
        /// </summary>
        internal static bool RunPreCommand(
            ulong candidateTick,
            in BattleInstanceHandle battle,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            DynamicBuffer<PendingCommand> commands,
            ref AscSlabHeads heads)
        {
            if (!RecycleTombstones(
                    grants, activations, continuations, subscriptions, commands, ref heads))
                return false;
            return ProcessRemovalCommands(
                candidateTick, in battle, grants, activations, commands, ref heads);
        }

        /// <summary>
        /// 在 canonical owner plans 后完成无 continuation 的 Ending Activation 与待移除 grant。
        /// </summary>
        internal static bool RunPostCommand(
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations,
            ref AscSlabHeads heads)
        {
            for (var index = 0; index < activations.Length; index++)
            {
                var activation = activations[index];
                if (activation.Header.StorageState != GasSlabSlotState.Live ||
                    activation.Phase != GasAbilityActivationPhase.Ending ||
                    HasLiveContinuation(activation.Handle, continuations))
                    continue;
                if (!FinalizeActivation(index, grants, activations, ref heads.AbilityActivation))
                    return false;
            }
            return FinalizePendingGrants(grants, ref heads.GrantedAbility);
        }

        /// <summary>
        /// 回收前一 Tick 留下的 tombstones，使 late generation 永远不能命中新对象。
        /// </summary>
        private static bool RecycleTombstones(
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            DynamicBuffer<PendingCommand> commands,
            ref AscSlabHeads heads)
        {
            var grantStorage = new GasGrantedAbilitySlabStorage { Buffer = grants };
            var activationStorage = new GasAbilityActivationSlabStorage { Buffer = activations };
            var continuationStorage = new GasAbilityContinuationSlabStorage { Buffer = continuations };
            var subscriptionStorage = new GasAbilitySubscriptionSlabStorage { Buffer = subscriptions };
            var commandStorage = new GasPendingCommandSlabStorage { Buffer = commands };
            return RecycleAll(ref heads.GrantedAbility, ref grantStorage) &&
                   RecycleAll(ref heads.AbilityActivation, ref activationStorage) &&
                   RecycleAll(ref heads.AbilityContinuation, ref continuationStorage) &&
                   RecycleAll(ref heads.AbilitySubscription, ref subscriptionStorage) &&
                   RecycleAll(ref heads.PendingCommand, ref commandStorage);
        }

        /// <summary>
        /// 将 storage 中全部 tombstone 递增 generation 后接入 free-list。
        /// </summary>
        private static bool RecycleAll<TStorage>(
            ref GasSlabHead head,
            ref TStorage storage)
            where TStorage : struct, IGasSlabHeaderStorage
        {
            for (var index = 0; index < storage.Count; index++)
            {
                if (storage.ReadHeader(index).StorageState != GasSlabSlotState.Tombstone)
                    continue;
                if (GasNonCompactingSlabAllocator.TryRecycleTombstone(
                        ref head, ref storage, index) != GasSlabStorageFailure.None)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 按冻结 AvailableTick/CommandSequence 全序处理 due grant removal，并把已接管命令转 tombstone。
        /// </summary>
        private static bool ProcessRemovalCommands(
            ulong candidateTick,
            in BattleInstanceHandle battle,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<PendingCommand> commands,
            ref AscSlabHeads heads)
        {
            var storage = new GasPendingCommandSlabStorage { Buffer = commands };
            while (TryFindNextRemovalCommand(candidateTick, commands, out var index))
            {
                var command = commands[index];
                if (GasNonCompactingSlabAllocator.TryMarkTombstone(
                        in heads.PendingCommand, ref storage, index) != GasSlabStorageFailure.None)
                    return false;
                ApplyRemoval(in command, in battle, grants, activations);
                command = commands[index];
                command.State = GasSlotBusinessState.Terminal;
                commands[index] = command;
            }
            return true;
        }

        /// <summary>
        /// 在未消费的 due removal 中选择最小冻结命令键，避免业务顺序依赖 slab 遍历位置。
        /// </summary>
        private static bool TryFindNextRemovalCommand(
            ulong candidateTick,
            DynamicBuffer<PendingCommand> commands,
            out int selectedIndex)
        {
            selectedIndex = -1;
            for (var index = 0; index < commands.Length; index++)
            {
                var candidate = commands[index];
                if (!IsDueRemoval(in candidate, candidateTick))
                    continue;
                if (selectedIndex < 0)
                {
                    selectedIndex = index;
                    continue;
                }
                var selected = commands[selectedIndex];
                if (CompareRemovalCommand(
                        in candidate, index, in selected, selectedIndex) < 0)
                    selectedIndex = index;
            }
            return selectedIndex >= 0;
        }

        /// <summary>
        /// 判断 PendingCommand 是否为当前 Tick 可消费的 grant removal。
        /// </summary>
        private static bool IsDueRemoval(in PendingCommand command, ulong candidateTick)
        {
            return command.Header.StorageState == GasSlabSlotState.Live &&
                   command.State == GasSlotBusinessState.Pending &&
                   command.AvailableTick <= candidateTick &&
                   command.CommandKind == (int)GasAbilityPendingCommandKind.RequestGrantedRemoval;
        }

        /// <summary>
        /// 比较 removal 的正式命令键；slot 仅为完全等价记录提供确定性末级裁决。
        /// </summary>
        private static int CompareRemovalCommand(
            in PendingCommand left,
            int leftIndex,
            in PendingCommand right,
            int rightIndex)
        {
            var comparison = left.AvailableTick.CompareTo(right.AvailableTick);
            if (comparison != 0)
                return comparison;
            comparison = left.CommandSequence.CompareTo(right.CommandSequence);
            if (comparison != 0)
                return comparison;
            comparison = left.GrantedAbility.SlotIndex.CompareTo(right.GrantedAbility.SlotIndex);
            if (comparison != 0)
                return comparison;
            comparison = left.GrantedAbility.SlotGeneration.CompareTo(
                right.GrantedAbility.SlotGeneration);
            return comparison != 0 ? comparison : leftIndex.CompareTo(rightIndex);
        }

        /// <summary>
        /// 应用 CancelImmediately、RemoveWhenAllActivationsEnd 或 LeaveGranted 的唯一状态转换。
        /// </summary>
        private static void ApplyRemoval(
            in PendingCommand command,
            in BattleInstanceHandle battle,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations)
        {
            if (!IsValidRemovalCommand(in command, in battle) ||
                !TryResolveGrant(command.GrantedAbility, grants, out var grant) ||
                !grant.Handle.OwnerAsc.Equals(command.SourceAsc) ||
                !grant.Handle.OwnerAsc.Equals(command.TargetAsc) ||
                grant.Handle.SimulationEpoch != battle.SimulationEpoch ||
                !MatchesRemovalProvenance(in command, in grant) ||
                grant.RemovalState != GasGrantedAbilityRemovalState.None)
                return;
            grant.RemovalPolicy = command.GrantedRemovalPolicy;
            if (command.GrantedRemovalPolicy == GasGrantedAbilityRemovalPolicy.LeaveGranted)
            {
                grant.RemovalState = GasGrantedAbilityRemovalState.ProvenanceDetached;
                grant.ProvenanceDetached = 1;
                grant.GrantingActiveEffect = default;
                grants[grant.Handle.SlotIndex] = grant;
                return;
            }
            grant.RemovalState = GasGrantedAbilityRemovalState.PendingRemove;
            grants[grant.Handle.SlotIndex] = grant;
            if (command.GrantedRemovalPolicy != GasGrantedAbilityRemovalPolicy.CancelImmediately)
                return;
            for (var index = 0; index < activations.Length; index++)
            {
                var activation = activations[index];
                if (activation.Header.StorageState != GasSlabSlotState.Live ||
                    !activation.GrantedAbility.Equals(grant.Handle))
                    continue;
                BeginGrantRemovalEnding(index, activations);
            }
        }

        /// <summary>
        /// 首次把 child Activation 与全部 Continuation 置 Ending，并冻结 GrantRemoved 原因。
        /// </summary>
        private static void BeginGrantRemovalEnding(
            int activationIndex,
            DynamicBuffer<AbilityActivationSlot> activations)
        {
            var activation = activations[activationIndex];
            if (activation.Phase == GasAbilityActivationPhase.Ending ||
                activation.Phase == GasAbilityActivationPhase.Ended)
                return;
            activation.Phase = GasAbilityActivationPhase.Ending;
            activation.EndReason = GasAbilityEndReason.GrantRemoved;
            activation.WasCancelled = 1;
            activations[activationIndex] = activation;
        }

        /// <summary>
        /// 将 Ending Activation 变为 Ended tombstone 并递减其 grant child count。
        /// </summary>
        private static bool FinalizeActivation(
            int activationIndex,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            ref GasSlabHead activationHead)
        {
            var activation = activations[activationIndex];
            var storage = new GasAbilityActivationSlabStorage { Buffer = activations };
            if (GasNonCompactingSlabAllocator.TryMarkTombstone(
                    in activationHead, ref storage, activationIndex) != GasSlabStorageFailure.None)
                return false;
            activation = activations[activationIndex];
            activation.Phase = GasAbilityActivationPhase.Ended;
            activations[activationIndex] = activation;
            if (TryResolveGrant(activation.GrantedAbility, grants, out var grant))
            {
                if (grant.ChildActivationCount > 0)
                    grant.ChildActivationCount--;
                grants[grant.Handle.SlotIndex] = grant;
            }
            return true;
        }

        /// <summary>
        /// 将已无 child 的 PendingRemove grant 转 tombstone；LeaveGranted 永不进入本路径。
        /// </summary>
        private static bool FinalizePendingGrants(
            DynamicBuffer<GrantedAbilitySlot> grants,
            ref GasSlabHead grantHead)
        {
            var storage = new GasGrantedAbilitySlabStorage { Buffer = grants };
            for (var index = 0; index < grants.Length; index++)
            {
                var grant = grants[index];
                if (grant.Header.StorageState != GasSlabSlotState.Live ||
                    grant.RemovalState != GasGrantedAbilityRemovalState.PendingRemove ||
                    grant.ChildActivationCount != 0)
                    continue;
                if (GasNonCompactingSlabAllocator.TryMarkTombstone(
                        in grantHead, ref storage, index) != GasSlabStorageFailure.None)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 判断 Activation 是否仍有尚未完成交接的 live Continuation。
        /// </summary>
        private static bool HasLiveContinuation(
            in AbilityActivationHandle activation,
            DynamicBuffer<AbilityContinuationSlot> continuations)
        {
            for (var index = 0; index < continuations.Length; index++)
            {
                var slot = continuations[index];
                if (slot.Header.StorageState == GasSlabSlotState.Live &&
                    slot.Activation.Equals(activation))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 解析同 owner generation 的 live grant，不接受 tombstone 或 free 槽。
        /// </summary>
        private static bool TryResolveGrant(
            in GrantedAbilityHandle handle,
            DynamicBuffer<GrantedAbilitySlot> grants,
            out GrantedAbilitySlot grant)
        {
            grant = default;
            if (!handle.IsValid || handle.SlotIndex < 0 || handle.SlotIndex >= grants.Length)
                return false;
            grant = grants[handle.SlotIndex];
            return grant.Header.StorageState == GasSlabSlotState.Live &&
                   grant.Header.Generation == handle.SlotGeneration && grant.Handle.Equals(handle);
        }

        /// <summary>
        /// 校验 removal 命令的 owner、BattleInstance、Epoch、provenance token 与三态 policy。
        /// </summary>
        private static bool IsValidRemovalCommand(
            in PendingCommand command,
            in BattleInstanceHandle expectedBattle)
        {
            return command.CommandKind == (int)GasAbilityPendingCommandKind.RequestGrantedRemoval &&
                   command.CommandSequence != 0 && expectedBattle.IsValid &&
                   command.BattleInstance.Equals(expectedBattle) &&
                   command.SourceAsc.IsValid && command.TargetAsc.IsValid &&
                   command.SourceAsc.Equals(command.TargetAsc) &&
                   command.GrantedAbility.IsValid &&
                   command.GrantedAbility.SimulationEpoch == expectedBattle.SimulationEpoch &&
                   IsKnownRemovalPolicy(command.GrantedRemovalPolicy) &&
                   IsValidRemovalProvenance(in command);
        }

        /// <summary>
        /// 限定唯一允许进入 removal 仲裁的三种策略，未知枚举一律拒绝。
        /// </summary>
        private static bool IsKnownRemovalPolicy(GasGrantedAbilityRemovalPolicy policy)
        {
            return policy == GasGrantedAbilityRemovalPolicy.CancelImmediately ||
                   policy == GasGrantedAbilityRemovalPolicy.RemoveWhenAllActivationsEnd ||
                   policy == GasGrantedAbilityRemovalPolicy.LeaveGranted;
        }

        /// <summary>
        /// 限定 grant provenance 来源，并要求 ActiveEffect 来源携带完整 effect owner 句柄。
        /// </summary>
        private static bool IsValidGrantProvenance(in GrantedAbilitySlot grant)
        {
            if (!IsKnownGrantSourceKind(grant.GrantSourceKind))
                return false;
            if (grant.GrantSourceKind != GasAbilityGrantSourceKind.ActiveEffect)
                return true;
            return grant.GrantingActiveEffect.IsValid &&
                   grant.GrantingActiveEffect.SimulationEpoch == grant.Handle.SimulationEpoch &&
                   grant.GrantingActiveEffect.OwnerAsc.Equals(grant.Handle.OwnerAsc);
        }

        /// <summary>
        /// 限定命令携带的 provenance token 具有可解释的来源类型，避免默认值伪造。
        /// </summary>
        private static bool IsValidRemovalProvenance(in PendingCommand command)
        {
            if (!IsKnownGrantSourceKind(command.GrantedRemovalSourceKind))
                return false;
            return command.GrantedRemovalSourceKind != GasAbilityGrantSourceKind.ActiveEffect ||
                   command.GrantedRemovalActiveEffect.IsValid;
        }

        /// <summary>
        /// 比较命令入队时冻结的 provenance 与当前 live grant，拒绝 stale cleanup 写入新 provenance。
        /// </summary>
        private static bool MatchesRemovalProvenance(
            in PendingCommand command,
            in GrantedAbilitySlot grant)
        {
            return command.GrantedRemovalSourceKind == grant.GrantSourceKind &&
                   command.GrantedRemovalSourceStableId == grant.GrantSourceStableId &&
                   command.GrantedRemovalActiveEffect.Equals(grant.GrantingActiveEffect) &&
                   command.GrantedRemovalApplicationId == grant.GrantApplicationId &&
                   command.GrantedRemovalContextId == grant.GrantContextId;
        }

        /// <summary>
        /// 判断 grant provenance 类型是否属于冻结协议定义的三类来源。
        /// </summary>
        private static bool IsKnownGrantSourceKind(GasAbilityGrantSourceKind sourceKind)
        {
            return sourceKind == GasAbilityGrantSourceKind.Bootstrap ||
                   sourceKind == GasAbilityGrantSourceKind.Command ||
                   sourceKind == GasAbilityGrantSourceKind.ActiveEffect;
        }
    }
}
