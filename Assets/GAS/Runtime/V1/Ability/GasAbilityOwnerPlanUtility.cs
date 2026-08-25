using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 只读解释一个 ASC 的 Ability command，并用先前计划模拟 canonical read-your-writes。
    /// </summary>
    internal static class GasAbilityOwnerPlanUtility
    {
        /// <summary>
        /// 将一条 sealed command 转为无权威写的 owner-local 计划或确定性业务拒绝。
        /// </summary>
        internal static GasOwnerPlanRecord Build(
            in BoundaryCommandInbox command,
            ulong simulationEpoch,
            ulong candidateTick,
            ref GasDefinitionCatalogBlob catalog,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<CooldownGateSlot> cooldowns,
            DynamicBuffer<PendingCommand> pendingCommands,
            NativeArray<GasOwnerPlanRecord> previousPlans,
            int previousPlanCount)
        {
            var plan = CreatePlan(in command);
            switch (command.CommandKind)
            {
                case GasBoundaryCommandKind.ApplyEffect:
                    PlanApplyEffect(ref plan, ref catalog, command.DefinitionId);
                    break;
                case GasBoundaryCommandKind.Activate:
                    PlanActivate(ref plan, simulationEpoch, candidateTick, ref catalog,
                        grants, activations, attributes, cooldowns, pendingCommands,
                        previousPlans, previousPlanCount);
                    break;
                case GasBoundaryCommandKind.Commit:
                    PlanCommit(ref plan, simulationEpoch, candidateTick, ref catalog,
                        grants, activations, attributes, cooldowns, pendingCommands,
                        previousPlans, previousPlanCount);
                    break;
                case GasBoundaryCommandKind.Cancel:
                    PlanCancel(ref plan, simulationEpoch, candidateTick, grants, activations,
                        pendingCommands,
                        previousPlans, previousPlanCount);
                    break;
                default:
                    plan.Result = GasAbilityCommandResult.NotAbilityCommand;
                    break;
            }
            return plan;
        }

        /// <summary>
        /// 为直接 GameplayEffect 请求建立 source-local committed-work 计划；正式 application identity 由 OwnerPlan 分配。
        /// </summary>
        private static void PlanApplyEffect(
            ref GasOwnerPlanRecord plan,
            ref GasDefinitionCatalogBlob catalog,
            int definitionId)
        {
            if (plan.OwnerAsc.IsValid == false ||
                plan.Target.Kind != GasBoundaryTargetKind.Asc ||
                !plan.Target.TargetAsc.IsValid ||
                !GasDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                    ref catalog, definitionId, out var definitionIndex))
            {
                plan.Result = GasAbilityCommandResult.DefinitionInvalid;
                return;
            }

            plan.DefinitionIndex = definitionIndex;
            plan.Result = GasAbilityCommandResult.None;
            plan.ProducesCommittedWork = 1;
            plan.BusinessAccepted = 1;
        }

        /// <summary>
        /// 构造保留原 canonical 身份但尚未接受的计划头。
        /// </summary>
        private static GasOwnerPlanRecord CreatePlan(in BoundaryCommandInbox command)
        {
            return new GasOwnerPlanRecord
            {
                OwnerAsc = command.SourceAsc,
                SourceSequence = command.SourceSequence,
                SubjectHandle = command.SubjectHandle,
                Target = command.Target,
                HasTarget = 1,
                CommandKind = command.CommandKind,
                CostAttributeLayoutIndex = -1,
                CooldownOwnedTagIndex = -1,
            };
        }

        /// <summary>
        /// 执行 CanActivate，只分配未来 Activation 需求且不消费费用或冷却。
        /// </summary>
        private static void PlanActivate(
            ref GasOwnerPlanRecord plan,
            ulong simulationEpoch,
            ulong candidateTick,
            ref GasDefinitionCatalogBlob catalog,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<CooldownGateSlot> cooldowns,
            DynamicBuffer<PendingCommand> pendingCommands,
            NativeArray<GasOwnerPlanRecord> previousPlans,
            int previousPlanCount)
        {
            if (!TryResolveGrant(in plan.SubjectHandle, simulationEpoch, plan.OwnerAsc, grants, out var grant))
            {
                plan.Result = GasAbilityCommandResult.HandleInvalid;
                return;
            }
            plan.GrantedAbility = grant.Handle;
            if (grant.RemovalState == GasGrantedAbilityRemovalState.PendingRemove ||
                HasDueRemoval(grant.Handle, candidateTick, pendingCommands, false))
            {
                plan.Result = GasAbilityCommandResult.GrantPendingRemove;
                return;
            }
            if (!TryGetDefinition(in grant, ref catalog, out var definition))
            {
                plan.Result = GasAbilityCommandResult.DefinitionInvalid;
                return;
            }
            if (IsConcurrencyBlocked(in grant, in definition, activations, previousPlans, previousPlanCount))
            {
                plan.Result = GasAbilityCommandResult.ConcurrencyBlocked;
                return;
            }
            plan.DefinitionIndex = grant.DefinitionIndex;
            plan.Result = GasAbilityCommandResult.Activated;
            plan.RequiresActivationSlot = 1;
            plan.BusinessAccepted = 1;
        }

        /// <summary>
        /// 全量预检 source-local CommitPlan，成功时冻结 no-fail mutation 参数。
        /// </summary>
        private static void PlanCommit(
            ref GasOwnerPlanRecord plan,
            ulong simulationEpoch,
            ulong candidateTick,
            ref GasDefinitionCatalogBlob catalog,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<CooldownGateSlot> cooldowns,
            DynamicBuffer<PendingCommand> pendingCommands,
            NativeArray<GasOwnerPlanRecord> previousPlans,
            int previousPlanCount)
        {
            if (!TryResolveActivation(in plan.SubjectHandle, simulationEpoch, plan.OwnerAsc,
                    activations, out var activation))
            {
                plan.Result = GasAbilityCommandResult.HandleInvalid;
                return;
            }
            plan.Activation = activation.Handle;
            plan.GrantedAbility = activation.GrantedAbility;
            var preCommandEnding = HasDueRemoval(
                activation.GrantedAbility, candidateTick, pendingCommands, true);
            var phase = GetShadowPhase(
                in activation, preCommandEnding, previousPlans, previousPlanCount);
            if (phase == GasAbilityActivationPhase.Committed)
            {
                plan.Result = GasAbilityCommandResult.AlreadyCommitted;
                return;
            }
            if (phase == GasAbilityActivationPhase.Ending || phase == GasAbilityActivationPhase.Ended)
            {
                plan.Result = GasAbilityCommandResult.OwnerEnding;
                return;
            }
            if (!TryResolveGrant(in activation.GrantedAbility, simulationEpoch, plan.OwnerAsc,
                    grants, out var grant) || !TryGetDefinition(in grant, ref catalog, out var definition))
            {
                plan.Result = GasAbilityCommandResult.DefinitionInvalid;
                return;
            }
            if (!CanPayCost(in definition, attributes, previousPlans, previousPlanCount, plan.OwnerAsc))
            {
                plan.Result = GasAbilityCommandResult.CostUnavailable;
                return;
            }
            if (HasCooldown(in definition, candidateTick, cooldowns, previousPlans, previousPlanCount, plan.OwnerAsc))
            {
                plan.Result = GasAbilityCommandResult.CooldownActive;
                return;
            }
            FreezeCommitContract(ref plan, in grant, in definition);
        }

        /// <summary>
        /// 只允许首个 canonical End/Cancel 穿越 Ending 屏障并冻结结束原因。
        /// </summary>
        private static void PlanCancel(
            ref GasOwnerPlanRecord plan,
            ulong simulationEpoch,
            ulong candidateTick,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<PendingCommand> pendingCommands,
            NativeArray<GasOwnerPlanRecord> previousPlans,
            int previousPlanCount)
        {
            if (!TryResolveActivation(in plan.SubjectHandle, simulationEpoch, plan.OwnerAsc,
                    activations, out var activation))
            {
                plan.Result = GasAbilityCommandResult.HandleInvalid;
                return;
            }
            plan.Activation = activation.Handle;
            plan.GrantedAbility = activation.GrantedAbility;
            if (!TryResolveGrant(in activation.GrantedAbility, simulationEpoch, plan.OwnerAsc, grants, out _))
            {
                plan.Result = GasAbilityCommandResult.HandleInvalid;
                return;
            }
            var preCommandEnding = HasDueRemoval(
                activation.GrantedAbility, candidateTick, pendingCommands, true);
            var phase = GetShadowPhase(
                in activation, preCommandEnding, previousPlans, previousPlanCount);
            if (phase == GasAbilityActivationPhase.Ending || phase == GasAbilityActivationPhase.Ended)
            {
                plan.Result = GasAbilityCommandResult.DuplicateEndNoOp;
                return;
            }
            plan.EndReason = GasAbilityEndReason.Cancelled;
            plan.WasCancelled = 1;
            plan.Result = GasAbilityCommandResult.Ended;
            plan.BusinessAccepted = 1;
        }

        /// <summary>
        /// 把 immutable cost/cooldown contract 复制进计划，后续 OwnerWave 不再查询可变条件。
        /// </summary>
        private static void FreezeCommitContract(
            ref GasOwnerPlanRecord plan,
            in GrantedAbilitySlot grant,
            in GasAbilityDefinitionBlob definition)
        {
            plan.DefinitionIndex = grant.DefinitionIndex;
            if (definition.CostMutationContract.Enabled != 0)
            {
                plan.CostAttributeLayoutIndex = definition.CostMutationContract.AttributeLayoutIndex;
                plan.CostBaseDelta = definition.CostMutationContract.BaseDelta;
                plan.CostCurrentDelta = definition.CostMutationContract.CurrentDelta;
            }
            if (definition.CooldownGateContract.Enabled != 0)
            {
                plan.CooldownGateKey = definition.CooldownGateContract.GateKey;
                plan.CooldownDurationTicks = definition.CooldownGateContract.DurationTicks;
                plan.CooldownOwnedTagIndex = definition.CooldownGateContract.OwnedTagIndex;
                plan.RequiresCooldownSlot = 1;
            }
            plan.Result = GasAbilityCommandResult.Committed;
            plan.ProducesCommittedWork = 1;
            plan.BusinessAccepted = 1;
        }

        /// <summary>
        /// 按稳定 typed carrier 校验并返回一个 live GrantedAbility 槽。
        /// </summary>
        private static bool TryResolveGrant(
            in StableHandleDiagnosticCarrier handle,
            ulong simulationEpoch,
            in OwnerAscHandle owner,
            DynamicBuffer<GrantedAbilitySlot> grants,
            out GrantedAbilitySlot grant)
        {
            grant = default;
            if (handle.SlotIndex < 0 || handle.SlotIndex >= grants.Length)
                return false;
            grant = grants[handle.SlotIndex];
            return StableHandleValidator.Validate(in handle, simulationEpoch, in owner, grants.Length,
                grant.Header.StorageState == GasSlabSlotState.Live, grant.Header.Generation,
                HandleKind.GrantedAbility) == HandleValidationFailure.None;
        }

        /// <summary>
        /// 以强类型句柄载体校验并返回一个 live GrantedAbility 槽。
        /// </summary>
        private static bool TryResolveGrant(
            in GrantedAbilityHandle handle,
            ulong simulationEpoch,
            in OwnerAscHandle owner,
            DynamicBuffer<GrantedAbilitySlot> grants,
            out GrantedAbilitySlot grant)
        {
            var carrier = handle.ToDiagnosticCarrier();
            return TryResolveGrant(in carrier, simulationEpoch, in owner, grants, out grant);
        }

        /// <summary>
        /// 按稳定 typed carrier 校验并返回一个 live Activation 槽。
        /// </summary>
        private static bool TryResolveActivation(
            in StableHandleDiagnosticCarrier handle,
            ulong simulationEpoch,
            in OwnerAscHandle owner,
            DynamicBuffer<AbilityActivationSlot> activations,
            out AbilityActivationSlot activation)
        {
            activation = default;
            if (handle.SlotIndex < 0 || handle.SlotIndex >= activations.Length)
                return false;
            activation = activations[handle.SlotIndex];
            return StableHandleValidator.Validate(in handle, simulationEpoch, in owner, activations.Length,
                activation.Header.StorageState == GasSlabSlotState.Live, activation.Header.Generation,
                HandleKind.AbilityActivation) == HandleValidationFailure.None;
        }

        /// <summary>
        /// 返回 grant 当前 Definition，拒绝索引或稳定 DefinitionId 错配。
        /// </summary>
        private static bool TryGetDefinition(
            in GrantedAbilitySlot grant,
            ref GasDefinitionCatalogBlob catalog,
            out GasAbilityDefinitionBlob definition)
        {
            definition = default;
            if (grant.DefinitionIndex < 0 || grant.DefinitionIndex >= catalog.Abilities.Length)
                return false;
            definition = catalog.Abilities[grant.DefinitionIndex];
            return definition.DefinitionId == grant.DefinitionId;
        }

        /// <summary>
        /// 依据已提交成本计划计算 owner attribute shadow，并验证封闭 clamp 范围。
        /// </summary>
        private static bool CanPayCost(
            in GasAbilityDefinitionBlob definition,
            DynamicBuffer<AttributeValueSlot> attributes,
            NativeArray<GasOwnerPlanRecord> plans,
            int planCount,
            in OwnerAscHandle owner)
        {
            var contract = definition.CostMutationContract;
            if (contract.Enabled == 0)
                return true;
            if (contract.AttributeLayoutIndex < 0 || contract.AttributeLayoutIndex >= attributes.Length)
                return false;
            var value = attributes[contract.AttributeLayoutIndex];
            for (var index = 0; index < planCount; index++)
            {
                var plan = plans[index];
                if (plan.BusinessAccepted == 0 || plan.CommandKind != GasBoundaryCommandKind.Commit ||
                    !plan.OwnerAsc.Equals(owner) || plan.CostAttributeLayoutIndex != contract.AttributeLayoutIndex)
                    continue;
                value.Base += plan.CostBaseDelta;
                value.Current += plan.CostCurrentDelta;
            }
            var projectedBase = value.Base + contract.BaseDelta;
            var projectedCurrent = value.Current + contract.CurrentDelta;
            return IsFinite(projectedBase) && IsFinite(projectedCurrent) &&
                   projectedBase >= 0f && projectedCurrent >= 0f;
        }

        /// <summary>
        /// 判断现有未到期 gate 或本 Tick 更早 CommitPlan 是否已经占用相同 gate key。
        /// </summary>
        private static bool HasCooldown(
            in GasAbilityDefinitionBlob definition,
            ulong candidateTick,
            DynamicBuffer<CooldownGateSlot> cooldowns,
            NativeArray<GasOwnerPlanRecord> plans,
            int planCount,
            in OwnerAscHandle owner)
        {
            var contract = definition.CooldownGateContract;
            if (contract.Enabled == 0)
                return false;
            for (var index = 0; index < cooldowns.Length; index++)
            {
                var slot = cooldowns[index];
                if (slot.Header.StorageState == GasSlabSlotState.Live &&
                    slot.State == GasSlotBusinessState.Active && slot.GateKey == contract.GateKey &&
                    slot.EndTick > candidateTick)
                    return true;
            }
            for (var index = 0; index < planCount; index++)
            {
                var plan = plans[index];
                if (plan.BusinessAccepted != 0 && plan.CommandKind == GasBoundaryCommandKind.Commit &&
                    plan.OwnerAsc.Equals(owner) && plan.CooldownGateKey == contract.GateKey)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 以 grant child count 加本 Tick前序 Activate/Cancel 计划判断并发上限。
        /// </summary>
        private static bool IsConcurrencyBlocked(
            in GrantedAbilitySlot grant,
            in GasAbilityDefinitionBlob definition,
            DynamicBuffer<AbilityActivationSlot> activations,
            NativeArray<GasOwnerPlanRecord> plans,
            int planCount)
        {
            if (definition.MaxConcurrentActivations == 0)
                return false;
            var childCount = grant.ChildActivationCount;
            for (var index = 0; index < planCount; index++)
            {
                var plan = plans[index];
                if (plan.BusinessAccepted == 0 || !plan.GrantedAbility.Equals(grant.Handle))
                    continue;
                if (plan.CommandKind == GasBoundaryCommandKind.Activate)
                    childCount++;
                else if (plan.CommandKind == GasBoundaryCommandKind.Cancel &&
                         HasLiveActivation(plan.Activation, activations))
                    childCount--;
            }
            return childCount >= definition.MaxConcurrentActivations;
        }

        /// <summary>
        /// 判断句柄是否仍对应当前 Activation buffer 的 live 槽。
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

        /// <summary>
        /// 将前序 Commit/Cancel 计划叠加为当前 Activation shadow phase。
        /// </summary>
        private static GasAbilityActivationPhase GetShadowPhase(
            in AbilityActivationSlot activation,
            bool preCommandEnding,
            NativeArray<GasOwnerPlanRecord> plans,
            int planCount)
        {
            if (preCommandEnding)
                return GasAbilityActivationPhase.Ending;
            var phase = activation.Phase;
            for (var index = 0; index < planCount; index++)
            {
                var plan = plans[index];
                if (plan.BusinessAccepted == 0 || !plan.Activation.Equals(activation.Handle))
                    continue;
                if (plan.CommandKind == GasBoundaryCommandKind.Commit)
                    phase = GasAbilityActivationPhase.Committed;
                else if (plan.CommandKind == GasBoundaryCommandKind.Cancel)
                    phase = GasAbilityActivationPhase.Ending;
            }
            return phase;
        }

        /// <summary>
        /// 把 tick-start due grant removal 折叠进 owner shadow；CancelImmediately 还形成 Activation Ending 屏障。
        /// </summary>
        private static bool HasDueRemoval(
            in GrantedAbilityHandle grant,
            ulong candidateTick,
            DynamicBuffer<PendingCommand> commands,
            bool cancelImmediatelyOnly)
        {
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (command.Header.StorageState != GasSlabSlotState.Live ||
                    command.State != GasSlotBusinessState.Pending ||
                    command.CommandKind != (int)GasAbilityPendingCommandKind.RequestGrantedRemoval ||
                    command.AvailableTick > candidateTick || !command.GrantedAbility.Equals(grant))
                    continue;
                if (command.GrantedRemovalPolicy == GasGrantedAbilityRemovalPolicy.CancelImmediately)
                    return true;
                if (!cancelImmediatelyOnly &&
                    command.GrantedRemovalPolicy != GasGrantedAbilityRemovalPolicy.LeaveGranted)
                    return true;
            }
            return false;
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
