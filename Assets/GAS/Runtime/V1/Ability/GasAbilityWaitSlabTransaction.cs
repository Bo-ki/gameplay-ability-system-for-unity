using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 冻结 owner writer 创建一个 Continuation 所需的输入，sample 必须留给 observed writer。
    /// </summary>
    internal struct GasAbilityContinuationCreateRequest
    {
        public AbilityActivationHandle Activation;
        public OwnerAscHandle ObservedAsc;
        public GasAbilityObservedHandle ObservedHandle;
        public GasAbilityWaitSemantic Semantic;
        public GasAbilityWaitPolicy Policy;
        public ulong CurrentTick;
        public ulong DueTick;
        public ulong CommandSequence;
        public int ProgramCounter;
        public int InstanceNameId;
        public int QueryKey;
    }

    /// <summary>
    /// 把纯 wait 协议映射到 owner Continuation 与 observed Subscription 两类 non-compacting slabs。
    /// </summary>
    internal static class GasAbilityWaitSlabTransaction
    {
        private const uint FirstRegistrationGeneration = 1;

        /// <summary>
        /// 在 owner ASC 分配 Continuation；跨 ASC wait 只生成注册消息，不读取 observed state。
        /// </summary>
        internal static bool TryBeginWait(
            in GasAbilityContinuationCreateRequest request,
            int hardCapacity,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations,
            ref GasSlabHead continuationHead,
            out AbilityContinuationHandle handle,
            out PendingCommand registration)
        {
            handle = default;
            registration = default;
            if (!IsValidCreateRequest(in request) ||
                !TryResolveActivation(request.Activation, activations, out var activation) ||
                !IsRunnableActivationPhase(activation.Phase))
                return false;
            var storage = new GasAbilityContinuationSlabStorage { Buffer = continuations };
            if (GasNonCompactingSlabAllocator.TryAllocate(
                    ref continuationHead, ref storage, hardCapacity, out var allocation) !=
                GasSlabStorageFailure.None)
                return false;
            handle = new AbilityContinuationHandle(
                request.Activation.SimulationEpoch,
                request.Activation.OwnerAsc,
                allocation.SlotIndex,
                allocation.Generation);
            var slot = CreateContinuation(in request, in handle, in allocation);
            if (request.Semantic == GasAbilityWaitSemantic.Timer)
            {
                if (!InitializeTimer(ref slot, in request))
                    return RollbackAllocation(allocation.SlotIndex, continuations, ref continuationHead);
            }
            else if (!TryCreateRegistration(in request, in handle, out registration))
                return RollbackAllocation(allocation.SlotIndex, continuations, ref continuationHead);
            continuations[allocation.SlotIndex] = slot;
            IncrementActivationChild(ref activation, activations);
            return true;
        }

        /// <summary>
        /// 在 observed ASC 的单 writer 线性化 sample+register，并返回 Ack 或即时 Completion。
        /// </summary>
        internal static GasAbilityWaitProtocolStatus SampleAndRegisterObserved(
            in OwnerAscHandle observedOwner,
            in PendingCommand registration,
            in GasAbilityWaitRegistrationSample sample,
            ulong currentTick,
            ulong registrationSequence,
            int hardCapacity,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            ref GasSlabHead subscriptionHead,
            out PendingCommand response)
        {
            var status = PlanSampleAndRegisterObservedCore(
                in observedOwner,
                in registration,
                in sample,
                currentTick,
                subscriptions,
                out response,
                out var requiresSubscriptionAllocation,
                out var record);
            if (!requiresSubscriptionAllocation)
                return status;
            var availableTick = response.AvailableTick;
            return AllocateObservedSubscription(
                in observedOwner,
                in registration,
                in record,
                availableTick,
                registrationSequence,
                hardCapacity,
                subscriptions,
                ref subscriptionHead,
                out response);
        }

        /// <summary>
        /// Admission 前只读规划 sample+register；新订阅以 default handle Ack 表示待分配需求。
        /// </summary>
        internal static GasAbilityWaitProtocolStatus PlanSampleAndRegisterObserved(
            in OwnerAscHandle observedOwner,
            in PendingCommand registration,
            in GasAbilityWaitRegistrationSample sample,
            ulong currentTick,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            out PendingCommand responseTemplate,
            out bool requiresSubscriptionAllocation)
        {
            return PlanSampleAndRegisterObservedCore(
                in observedOwner,
                in registration,
                in sample,
                currentTick,
                subscriptions,
                out responseTemplate,
                out requiresSubscriptionAllocation,
                out _);
        }

        /// <summary>
        /// 在 subscriber owner writer 应用 Ack/Completion，并复验 activation、continuation 与握手 generation。
        /// </summary>
        internal static bool ApplyOwnerResponse(
            in OwnerAscHandle owner,
            in PendingCommand response,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations)
        {
            if (!owner.Equals(response.TargetAsc) ||
                !TryResolveActivation(response.Activation, activations, out _) ||
                !TryResolveContinuation(response.Continuation, continuations, out var slot) ||
                slot.RegistrationGeneration != response.RegistrationGeneration ||
                !slot.Activation.Equals(response.Activation) ||
                !slot.ObservedAsc.Equals(response.SourceAsc) ||
                slot.WaitSemantic != response.WaitSemantic ||
                slot.WaitPolicy != response.WaitPolicy)
                return false;
            if (response.CommandKind == (int)GasAbilityPendingCommandKind.WaitRegistrationAck)
                return ApplyRegistrationAck(in response, ref slot, continuations);
            if (response.CommandKind == (int)GasAbilityPendingCommandKind.WaitCompletion)
                return ApplyCompletion(in response, ref slot, continuations);
            return false;
        }

        /// <summary>
        /// 将已成功应用的 live WaitCompletion 从 Pending 提升为 durable runnable resume。
        /// </summary>
        internal static bool TryMarkOwnerResponseRunnable(
            in OwnerAscHandle owner,
            int commandIndex,
            ulong currentTick,
            DynamicBuffer<PendingCommand> commands)
        {
            if (commandIndex < 0 || commandIndex >= commands.Length)
                return false;
            var command = commands[commandIndex];
            if (!IsValidRunnableOwnerResponse(in owner, in command) ||
                command.State != GasSlotBusinessState.Pending ||
                command.ResumeTick != currentTick)
                return false;
            command.State = GasSlotBusinessState.Active;
            commands[commandIndex] = command;
            return true;
        }

        /// <summary>
        /// 严格消费 durable resume 并 tombstone 命令槽，不替 ability program 结束 Continuation。
        /// </summary>
        internal static bool TryConsumeRunnableOwnerResponse(
            in OwnerAscHandle owner,
            int commandIndex,
            in AbilityActivationHandle activation,
            in AbilityContinuationHandle continuation,
            uint registrationGeneration,
            ulong resumeTick,
            DynamicBuffer<PendingCommand> commands,
            ref GasSlabHead commandHead,
            out PendingCommand consumedResponse)
        {
            consumedResponse = default;
            if (commandIndex < 0 || commandIndex >= commands.Length)
                return false;
            var command = commands[commandIndex];
            if (!IsValidRunnableOwnerResponse(in owner, in command) ||
                command.State != GasSlotBusinessState.Active ||
                !command.Activation.Equals(activation) ||
                !command.Continuation.Equals(continuation) ||
                command.RegistrationGeneration != registrationGeneration ||
                command.ResumeTick != resumeTick)
                return false;
            var storage = new GasPendingCommandSlabStorage { Buffer = commands };
            if (GasNonCompactingSlabAllocator.TryMarkTombstone(
                    in commandHead, ref storage, commandIndex) != GasSlabStorageFailure.None)
                return false;
            consumedResponse = command;
            command = commands[commandIndex];
            command.State = GasSlotBusinessState.Terminal;
            commands[commandIndex] = command;
            return true;
        }

        /// <summary>
        /// 在 observed ASC 匹配一个 Registered subscription，one-shot 消费且 persistent 分配 WakeOrdinal。
        /// </summary>
        internal static GasAbilityWaitProtocolStatus WakeObserved(
            in OwnerAscHandle observedOwner,
            in AbilitySubscriptionHandle subscriptionHandle,
            GasAbilityWaitSignalKind signalKind,
            ulong currentTick,
            ulong observedOrdinal,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            out PendingCommand response)
        {
            response = default;
            if (!TryResolveSubscription(subscriptionHandle, subscriptions, out var subscription))
                return GasAbilityWaitProtocolStatus.NoOpStale;
            var waitSignal = CreateWaitSignalCommand(
                in observedOwner, in subscription, signalKind, currentTick, observedOrdinal);
            return WakeObserved(
                in observedOwner, in waitSignal, currentTick, subscriptions, out response);
        }

        /// <summary>
        /// 在 observed writer 严格消费完整 WaitSignal 命令，并仅在匹配成功后改写 Subscription。
        /// </summary>
        internal static GasAbilityWaitProtocolStatus WakeObserved(
            in OwnerAscHandle observedOwner,
            in PendingCommand waitSignal,
            ulong currentTick,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            out PendingCommand response)
        {
            response = default;
            if (!TryResolveSubscription(waitSignal.Subscription, subscriptions, out var subscription))
                return GasAbilityWaitProtocolStatus.NoOpStale;
            var status = EvaluateWakeObserved(
                in observedOwner,
                in subscription,
                in waitSignal,
                currentTick,
                out var updatedSubscription,
                out response);
            if (status == GasAbilityWaitProtocolStatus.Completed ||
                status == GasAbilityWaitProtocolStatus.PersistentWake)
            {
                subscriptions[waitSignal.Subscription.SlotIndex] = updatedSubscription;
            }
            return status;
        }

        /// <summary>
        /// Admission 前在虚拟 Subscription 上只读规划 WaitSignal 响应，不改写真实 slab。
        /// </summary>
        internal static GasAbilityWaitProtocolStatus PlanWakeObserved(
            in OwnerAscHandle observedOwner,
            in AbilitySubscriptionSlot virtualSubscription,
            in PendingCommand waitSignal,
            ulong currentTick,
            out PendingCommand response)
        {
            return EvaluateWakeObserved(
                in observedOwner,
                in virtualSubscription,
                in waitSignal,
                currentTick,
                out _,
                out response);
        }

        /// <summary>
        /// 验证 WaitSignal 的目标、完整订阅身份、冻结 tick、语义与 ordinal 载荷形状。
        /// </summary>
        internal static bool IsValidWaitSignal(
            in OwnerAscHandle observedOwner,
            in AbilitySubscriptionSlot subscription,
            in PendingCommand waitSignal,
            ulong currentTick)
        {
            if (!IsRegisteredSubscriptionForOwner(in observedOwner, in subscription) ||
                waitSignal.CommandKind != (int)GasAbilityPendingCommandKind.WaitSignal ||
                !waitSignal.SourceAsc.IsValid ||
                !waitSignal.TargetAsc.Equals(observedOwner) ||
                !waitSignal.Subscription.Equals(subscription.Handle) ||
                !waitSignal.Activation.Equals(subscription.Activation) ||
                !waitSignal.Continuation.Equals(subscription.Continuation) ||
                waitSignal.AvailableTick != currentTick ||
                waitSignal.CommandSequence == 0 ||
                waitSignal.RegistrationGeneration != subscription.RegistrationGeneration ||
                waitSignal.WaitSemantic != subscription.WaitSemantic ||
                waitSignal.WaitPolicy != subscription.WaitPolicy ||
                waitSignal.QueryKey != subscription.QueryKey ||
                waitSignal.State != GasSlotBusinessState.Pending)
                return false;
            return IsWaitSignalShapeValid(in subscription, in waitSignal);
        }

        /// <summary>
        /// owner 先推进握手 generation 并置 Continuation Ending，再创建 unsubscribe 命令。
        /// </summary>
        internal static bool TryCancelOwnerWait(
            in AbilityContinuationHandle continuationHandle,
            ulong currentTick,
            ulong commandSequence,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations,
            out PendingCommand unsubscribe)
        {
            unsubscribe = default;
            if (!TryResolveContinuation(continuationHandle, continuations, out var slot) ||
                !TryResolveActivation(slot.Activation, activations, out _) ||
                slot.WaitSemantic == GasAbilityWaitSemantic.Timer ||
                !slot.ObservedAsc.IsValid ||
                commandSequence == 0 ||
                slot.RegistrationGeneration == uint.MaxValue ||
                !TryNextTick(currentTick, out var availableTick))
                return false;
            if (slot.WaitState != GasAbilityWaitState.PendingRegistration &&
                slot.WaitState != GasAbilityWaitState.Registered)
                return false;
            slot.RegistrationGeneration++;
            slot.WaitState = GasAbilityWaitState.Ending;
            continuations[continuationHandle.SlotIndex] = slot;
            unsubscribe = CreateUnsubscribe(in slot, availableTick, commandSequence);
            return true;
        }

        /// <summary>
        /// OwnerPlan 只读冻结 activation end 将产生的 unsubscribe，不提前改写 Continuation authority。
        /// </summary>
        internal static bool TryPlanOwnerCancellation(
            in AbilityContinuationSlot slot,
            ulong currentTick,
            out PendingCommand unsubscribe)
        {
            unsubscribe = default;
            if (slot.Header.StorageState != GasSlabSlotState.Live ||
                !slot.Handle.IsValid || !slot.Activation.IsValid || !slot.ObservedAsc.IsValid ||
                slot.WaitSemantic == GasAbilityWaitSemantic.Timer ||
                (slot.WaitState != GasAbilityWaitState.PendingRegistration &&
                 slot.WaitState != GasAbilityWaitState.Registered) ||
                slot.RegistrationGeneration == uint.MaxValue ||
                !TryNextTick(currentTick, out var availableTick))
                return false;
            var ending = slot;
            ending.RegistrationGeneration++;
            unsubscribe = CreateUnsubscribe(in ending, availableTick, 0);
            return true;
        }

        /// <summary>
        /// 判断 Ending barrier 是否必须为该 live 跨 ASC Continuation 生成 unsubscribe。
        /// </summary>
        internal static bool RequiresOwnerCancellation(in AbilityContinuationSlot slot)
        {
            return slot.Header.StorageState == GasSlabSlotState.Live &&
                   slot.WaitSemantic != GasAbilityWaitSemantic.Timer &&
                   (slot.WaitState == GasAbilityWaitState.PendingRegistration ||
                    slot.WaitState == GasAbilityWaitState.Registered);
        }

        /// <summary>
        /// OwnerWave 只接受 admission 前已冻结且精确推进一个 generation 的取消计划。
        /// </summary>
        internal static bool ApplyPlannedOwnerCancellation(
            in PendingCommand unsubscribe,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations)
        {
            if (!TryResolveContinuation(unsubscribe.Continuation, continuations, out var slot) ||
                !TryResolveActivation(slot.Activation, activations, out var activation) ||
                activation.Phase != GasAbilityActivationPhase.Ending ||
                slot.WaitSemantic == GasAbilityWaitSemantic.Timer ||
                slot.WaitState != GasAbilityWaitState.PendingRegistration &&
                slot.WaitState != GasAbilityWaitState.Registered ||
                !slot.Activation.Equals(unsubscribe.Activation) ||
                !slot.ObservedAsc.Equals(unsubscribe.TargetAsc) ||
                !slot.Handle.OwnerAsc.Equals(unsubscribe.SourceAsc) ||
                slot.RegistrationGeneration == uint.MaxValue ||
                unsubscribe.RegistrationGeneration != slot.RegistrationGeneration + 1)
                return false;
            slot.RegistrationGeneration++;
            slot.WaitState = GasAbilityWaitState.Ending;
            continuations[slot.Handle.SlotIndex] = slot;
            return true;
        }

        /// <summary>
        /// 本地 Timer 不参与跨 ASC unsubscribe；取消时直接结束并 tombstone Continuation。
        /// </summary>
        internal static bool TryCancelLocalTimerWait(
            in AbilityContinuationHandle continuationHandle,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations,
            ref GasSlabHead continuationHead)
        {
            if (!TryResolveContinuation(continuationHandle, continuations, out var slot) ||
                slot.WaitSemantic != GasAbilityWaitSemantic.Timer ||
                !TryResolveActivation(slot.Activation, activations, out _))
                return false;
            return EndAndTombstoneContinuation(
                continuationHandle.SlotIndex, in slot, activations, continuations, ref continuationHead);
        }

        /// <summary>
        /// 在 owner writer 内只按冻结 DueTick 评估 Timer，并统一写入最早 T+1 的恢复时点。
        /// </summary>
        internal static GasAbilityWaitProtocolStatus EvaluateTimer(
            in AbilityContinuationHandle continuationHandle,
            ulong currentTick,
            DynamicBuffer<AbilityContinuationSlot> continuations)
        {
            if (!TryResolveContinuation(continuationHandle, continuations, out var slot) ||
                slot.WaitSemantic != GasAbilityWaitSemantic.Timer ||
                slot.WaitPolicy != GasAbilityWaitPolicy.OneShot ||
                slot.WaitState != GasAbilityWaitState.Registered)
                return GasAbilityWaitProtocolStatus.NoOpStale;
            var record = CreateProtocolRecord(in slot);
            var identity = GasAbilityWaitMessageIdentity.From(in record);
            var signal = new GasAbilityWaitSignal(
                in identity, GasAbilityWaitSignalKind.TimerDueEvaluation, currentTick, 0);
            var result = GasAbilityWaitProtocol.Wake(ref record, in signal);
            if (result.Status == GasAbilityWaitProtocolStatus.Completed)
            {
                ApplyProtocolRecord(ref slot, in record);
                continuations[continuationHandle.SlotIndex] = slot;
            }
            return result.Status;
        }

        /// <summary>
        /// ability program 消费一次性 completion 后结束并 tombstone owner Continuation。
        /// </summary>
        internal static bool TryConsumeCompletedOwnerWait(
            in AbilityContinuationHandle continuationHandle,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations,
            ref GasSlabHead continuationHead)
        {
            if (!TryResolveContinuation(continuationHandle, continuations, out var slot) ||
                slot.WaitState != GasAbilityWaitState.Completed ||
                !TryResolveActivation(slot.Activation, activations, out _))
                return false;
            return EndAndTombstoneContinuation(
                continuationHandle.SlotIndex, in slot, activations, continuations, ref continuationHead);
        }

        /// <summary>
        /// observed writer 按 Subscription 或 registration tuple 结束订阅，并始终返回 cancel Ack。
        /// </summary>
        internal static bool UnsubscribeObserved(
            in OwnerAscHandle observedOwner,
            in PendingCommand unsubscribe,
            ulong currentTick,
            int hardCapacity,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            ref GasSlabHead subscriptionHead,
            out PendingCommand response)
        {
            response = default;
            if (!IsValidUnsubscribe(in unsubscribe, in observedOwner) ||
                unsubscribe.AvailableTick != currentTick ||
                !TryNextTick(currentTick, out var availableTick))
                return false;
            var slotIndex = FindCancelledSubscription(in unsubscribe, subscriptions);
            if (slotIndex >= 0 && !ApplyObservedCancellation(
                    slotIndex, unsubscribe.RegistrationGeneration, subscriptions, ref subscriptionHead))
                return false;
            if (slotIndex < 0 && !AllocateCancellationFence(
                    in observedOwner,
                    in unsubscribe,
                    hardCapacity,
                    subscriptions,
                    ref subscriptionHead))
                return false;
            response = CreateUnsubscribeAck(in unsubscribe, availableTick);
            return true;
        }

        /// <summary>
        /// Admission 前为 due unsubscribe 冻结必然产生的 T+1 Ack，不读取或修改 observed slab。
        /// </summary>
        internal static bool TryPlanUnsubscribeAck(
            in OwnerAscHandle observedOwner,
            in PendingCommand unsubscribe,
            ulong currentTick,
            out PendingCommand response)
        {
            response = default;
            if (!IsValidUnsubscribe(in unsubscribe, in observedOwner) ||
                unsubscribe.AvailableTick != currentTick ||
                !TryNextTick(currentTick, out var availableTick))
                return false;
            response = CreateUnsubscribeAck(in unsubscribe, availableTick);
            return true;
        }

        /// <summary>
        /// Admission 判断 due unsubscribe 是否必须临时分配 cancellation generation fence。
        /// </summary>
        internal static bool RequiresCancellationFence(
            in OwnerAscHandle observedOwner,
            in PendingCommand unsubscribe,
            ulong currentTick,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions)
        {
            if (!IsValidUnsubscribe(in unsubscribe, in observedOwner) ||
                unsubscribe.AvailableTick != currentTick)
                return false;
            var slotIndex = FindCancelledSubscription(in unsubscribe, subscriptions);
            return slotIndex < 0 ||
                   subscriptions[slotIndex].Header.StorageState == GasSlabSlotState.Tombstone;
        }

        /// <summary>
        /// 判断 outbound kind 是否定义了 destination 消失后的类型化 owner-gone 回执。
        /// </summary>
        internal static bool SupportsObservedOwnerGoneResponse(int commandKind)
        {
            return commandKind == (int)GasAbilityPendingCommandKind.WaitUnsubscribe ||
                   commandKind == (int)GasAbilityPendingCommandKind.WaitRegistration;
        }

        /// <summary>
        /// route destination 已消失时把 registration/unsubscribe 转为 owner 可消费的类型化终态消息。
        /// </summary>
        internal static bool TryCreateObservedOwnerGoneResponse(
            in PendingCommand outbound,
            ulong currentTick,
            out PendingCommand response)
        {
            response = default;
            if (!TryNextTick(currentTick, out var availableTick) ||
                outbound.AvailableTick != availableTick || !outbound.SourceAsc.IsValid ||
                !outbound.TargetAsc.IsValid || !outbound.Activation.IsValid ||
                !outbound.Continuation.IsValid || outbound.CommandSequence == 0)
                return false;
            if (outbound.CommandKind == (int)GasAbilityPendingCommandKind.WaitUnsubscribe)
            {
                response = CreateUnsubscribeAck(in outbound, availableTick);
                return true;
            }
            if (outbound.CommandKind != (int)GasAbilityPendingCommandKind.WaitRegistration ||
                outbound.WaitSemantic == GasAbilityWaitSemantic.Timer)
                return false;
            response = new PendingCommand
            {
                SourceAsc = outbound.TargetAsc,
                TargetAsc = outbound.SourceAsc,
                Activation = outbound.Activation,
                Continuation = outbound.Continuation,
                AvailableTick = availableTick,
                CommandSequence = outbound.CommandSequence,
                RegistrationSequence = outbound.RegistrationSequence,
                CompletionTick = currentTick,
                ResumeTick = availableTick,
                MatchedTagDepth = outbound.MatchedTagDepth,
                RegistrationGeneration = outbound.RegistrationGeneration,
                RecipientKindPriority = outbound.RecipientKindPriority,
                CommandKind = (int)GasAbilityPendingCommandKind.WaitCompletion,
                WaitSemantic = outbound.WaitSemantic,
                WaitPolicy = outbound.WaitPolicy,
                CompletionReason = GasAbilityWaitCompletionReason.ObservedOwnerGone,
                State = GasSlotBusinessState.Pending,
            };
            return true;
        }

        /// <summary>
        /// observed ASC 已退出 Ready/Alive 时冻结 owner 可在 T+1 消费的确定性 completion。
        /// </summary>
        internal static bool TryPlanObservedOwnerGoneCompletion(
            in AbilityContinuationSlot slot,
            ulong currentTick,
            out PendingCommand completion)
        {
            completion = default;
            if (slot.Header.StorageState != GasSlabSlotState.Live || !slot.Handle.IsValid ||
                !slot.Activation.IsValid || !slot.ObservedAsc.IsValid ||
                slot.WaitSemantic == GasAbilityWaitSemantic.Timer ||
                (slot.WaitState != GasAbilityWaitState.PendingRegistration &&
                 slot.WaitState != GasAbilityWaitState.Registered) ||
                !TryNextTick(currentTick, out var availableTick))
                return false;
            completion = new PendingCommand
            {
                SourceAsc = slot.ObservedAsc,
                TargetAsc = slot.Handle.OwnerAsc,
                Activation = slot.Activation,
                Continuation = slot.Handle,
                AvailableTick = availableTick,
                CompletionTick = currentTick,
                ResumeTick = availableTick,
                RegistrationGeneration = slot.RegistrationGeneration,
                CommandKind = (int)GasAbilityPendingCommandKind.WaitCompletion,
                WaitSemantic = slot.WaitSemantic,
                WaitPolicy = slot.WaitPolicy,
                CompletionReason = GasAbilityWaitCompletionReason.ObservedOwnerGone,
                State = GasSlotBusinessState.Pending,
            };
            return true;
        }

        /// <summary>
        /// subscriber owner 已消失时直接结束 observed Subscription，不创建无法投递的孤儿 Ack。
        /// </summary>
        internal static bool TryTombstoneOrphanedSubscription(
            in OwnerAscHandle observedOwner,
            in AbilitySubscriptionHandle subscriptionHandle,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            ref GasSlabHead subscriptionHead)
        {
            if (!TryResolveSubscription(subscriptionHandle, subscriptions, out var slot) ||
                !slot.ObservedAsc.Equals(observedOwner) ||
                (slot.State != GasAbilitySubscriptionState.PendingRegistration &&
                 slot.State != GasAbilitySubscriptionState.Registered &&
                 slot.State != GasAbilitySubscriptionState.Completed &&
                 slot.State != GasAbilitySubscriptionState.Ending))
                return false;
            var storage = new GasAbilitySubscriptionSlabStorage { Buffer = subscriptions };
            if (GasNonCompactingSlabAllocator.TryMarkTombstone(
                    in subscriptionHead, ref storage, subscriptionHandle.SlotIndex) !=
                GasSlabStorageFailure.None)
                return false;
            slot = subscriptions[subscriptionHandle.SlotIndex];
            slot.State = GasAbilitySubscriptionState.Ended;
            subscriptions[subscriptionHandle.SlotIndex] = slot;
            return true;
        }

        /// <summary>
        /// subscriber 收到 cancel Ack 后结束并 tombstone Continuation，迟到旧 generation Ack 保持 no-op。
        /// </summary>
        internal static bool ApplyCancelAckOwner(
            in OwnerAscHandle owner,
            in PendingCommand response,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations,
            ref GasSlabHead continuationHead)
        {
            if (response.CommandKind != (int)GasAbilityPendingCommandKind.WaitUnsubscribeAck ||
                !response.TargetAsc.Equals(owner) ||
                !TryResolveContinuation(response.Continuation, continuations, out var slot) ||
                !TryResolveActivation(slot.Activation, activations, out _) ||
                !response.SourceAsc.Equals(slot.ObservedAsc) ||
                !response.Activation.Equals(slot.Activation) ||
                !MatchesOptionalSubscription(response.Subscription, slot.Subscription) ||
                response.WaitSemantic != slot.WaitSemantic ||
                response.WaitPolicy != slot.WaitPolicy ||
                slot.WaitState != GasAbilityWaitState.Ending ||
                slot.RegistrationGeneration != response.RegistrationGeneration)
                return false;
            return EndAndTombstoneContinuation(
                response.Continuation.SlotIndex,
                in slot,
                activations,
                continuations,
                ref continuationHead);
        }

        /// <summary>
        /// one-shot completion 已形成持久 route 后回收 observed Subscription，旧 handle 立即失效。
        /// </summary>
        internal static bool FinalizeObservedCompletion(
            in AbilitySubscriptionHandle subscriptionHandle,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            ref GasSlabHead subscriptionHead)
        {
            if (!TryResolveSubscription(subscriptionHandle, subscriptions, out var slot) ||
                slot.State != GasAbilitySubscriptionState.Completed)
                return false;
            var storage = new GasAbilitySubscriptionSlabStorage { Buffer = subscriptions };
            if (GasNonCompactingSlabAllocator.TryMarkTombstone(
                    in subscriptionHead, ref storage, subscriptionHandle.SlotIndex) !=
                GasSlabStorageFailure.None)
                return false;
            slot = subscriptions[subscriptionHandle.SlotIndex];
            slot.State = GasAbilitySubscriptionState.Ended;
            subscriptions[subscriptionHandle.SlotIndex] = slot;
            return true;
        }

        /// <summary>
        /// 共享 sample+register 的只读裁决，使 admission 规划与 writer 落盘保持同一事实源。
        /// </summary>
        private static GasAbilityWaitProtocolStatus PlanSampleAndRegisterObservedCore(
            in OwnerAscHandle observedOwner,
            in PendingCommand registration,
            in GasAbilityWaitRegistrationSample sample,
            ulong currentTick,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            out PendingCommand response,
            out bool requiresSubscriptionAllocation,
            out GasAbilityWaitProtocolRecord record)
        {
            response = default;
            requiresSubscriptionAllocation = false;
            record = default;
            if (!IsRegistrationForOwner(in registration, in observedOwner) ||
                registration.AvailableTick != currentTick ||
                !TryNextTick(currentTick, out var availableTick))
                return GasAbilityWaitProtocolStatus.InvalidRequest;
            if (TryHandleExistingRegistration(
                    in registration, availableTick, subscriptions, out var existingStatus, out response))
                return existingStatus;
            var request = CreateRegistrationRequest(in observedOwner, in registration);
            var result = GasAbilityWaitProtocol.SampleAndRegister(
                in request, in sample, currentTick, out record);
            if (result.Status == GasAbilityWaitProtocolStatus.Completed)
            {
                response = CreateCompletionResponse(
                    in registration, in result, availableTick, default);
                return result.Status;
            }
            if (result.Status != GasAbilityWaitProtocolStatus.PendingRegistration)
                return result.Status;
            response = CreateRegistrationAck(in registration, default, availableTick);
            requiresSubscriptionAllocation = true;
            return GasAbilityWaitProtocolStatus.Registered;
        }

        /// <summary>
        /// 从冻结 registration 创建纯协议注册请求。
        /// </summary>
        private static GasAbilityWaitRegistrationRequest CreateRegistrationRequest(
            in OwnerAscHandle observedOwner,
            in PendingCommand registration)
        {
            return new GasAbilityWaitRegistrationRequest(
                registration.Activation,
                registration.Continuation,
                observedOwner,
                registration.WaitSemantic,
                registration.WaitPolicy,
                registration.DueTick);
        }

        /// <summary>
        /// 在 Subscription 副本上执行协议唤醒，成功时同时返回待写副本与 T+1 response。
        /// </summary>
        private static GasAbilityWaitProtocolStatus EvaluateWakeObserved(
            in OwnerAscHandle observedOwner,
            in AbilitySubscriptionSlot subscription,
            in PendingCommand waitSignal,
            ulong currentTick,
            out AbilitySubscriptionSlot updatedSubscription,
            out PendingCommand response)
        {
            updatedSubscription = subscription;
            response = default;
            if (!IsRegisteredSubscriptionForOwner(in observedOwner, in subscription))
                return GasAbilityWaitProtocolStatus.NoOpStale;
            if (!IsValidWaitSignal(in observedOwner, in subscription, in waitSignal, currentTick))
                return GasAbilityWaitProtocolStatus.InvalidRequest;
            var record = CreateProtocolRecord(in subscription);
            var identity = GasAbilityWaitMessageIdentity.From(in record);
            var signal = new GasAbilityWaitSignal(
                in identity, waitSignal.WaitSignalKind, currentTick, waitSignal.ObservedRevision);
            var result = GasAbilityWaitProtocol.Wake(ref record, in signal);
            if (result.Status != GasAbilityWaitProtocolStatus.Completed &&
                result.Status != GasAbilityWaitProtocolStatus.PersistentWake)
                return result.Status;
            ApplyWakeToSubscription(ref updatedSubscription, in record, in result);
            response = CreateWakeResponse(
                in updatedSubscription, in result, result.ResumeTick, in waitSignal);
            return result.Status;
        }

        /// <summary>
        /// 旧测试入口构造与生产命令相同的完整 WaitSignal，避免保留第二套唤醒语义。
        /// </summary>
        private static PendingCommand CreateWaitSignalCommand(
            in OwnerAscHandle observedOwner,
            in AbilitySubscriptionSlot subscription,
            GasAbilityWaitSignalKind signalKind,
            ulong currentTick,
            ulong observedOrdinal)
        {
            return new PendingCommand
            {
                SourceAsc = observedOwner,
                TargetAsc = observedOwner,
                Activation = subscription.Activation,
                Continuation = subscription.Continuation,
                Subscription = subscription.Handle,
                CommandSequence = subscription.RegistrationSequence != 0
                    ? subscription.RegistrationSequence
                    : (ulong)subscription.Handle.SlotIndex + 1,
                ObservedHandle = subscription.ObservedHandle,
                AvailableTick = currentTick,
                ObservedRevision = observedOrdinal,
                QueryKey = subscription.QueryKey,
                RegistrationGeneration = subscription.RegistrationGeneration,
                CommandKind = (int)GasAbilityPendingCommandKind.WaitSignal,
                WaitSemantic = subscription.WaitSemantic,
                WaitPolicy = subscription.WaitPolicy,
                WaitSignalKind = signalKind,
                State = GasSlotBusinessState.Pending,
            };
        }

        /// <summary>
        /// 创建带 child linkage 与冻结 wait 描述的 owner Continuation 槽。
        /// </summary>
        private static AbilityContinuationSlot CreateContinuation(
            in GasAbilityContinuationCreateRequest request,
            in AbilityContinuationHandle handle,
            in GasSlabAllocation allocation)
        {
            return new AbilityContinuationSlot
            {
                Header = allocation.LiveHeader,
                Handle = handle,
                Activation = request.Activation,
                ObservedAsc = request.ObservedAsc,
                ObservedHandle = request.ObservedHandle,
                DueTick = request.DueTick,
                ProgramCounter = request.ProgramCounter,
                InstanceNameId = request.InstanceNameId,
                QueryKey = request.QueryKey,
                RegistrationGeneration = FirstRegistrationGeneration,
                WaitSemantic = request.Semantic,
                WaitPolicy = request.Policy,
                WaitState = GasAbilityWaitState.PendingRegistration,
            };
        }

        /// <summary>
        /// Timer 不创建 observed Subscription，只按 DueTick 决定本地 Registered 或 Completed。
        /// </summary>
        private static bool InitializeTimer(
            ref AbilityContinuationSlot slot,
            in GasAbilityContinuationCreateRequest request)
        {
            var protocolRequest = new GasAbilityWaitRegistrationRequest(
                request.Activation,
                slot.Handle,
                default,
                GasAbilityWaitSemantic.Timer,
                request.Policy,
                request.DueTick);
            var sample = GasAbilityWaitRegistrationSample.ForTimer();
            var result = GasAbilityWaitProtocol.SampleAndRegister(
                in protocolRequest, in sample, request.CurrentTick, out var record);
            if (result.Status != GasAbilityWaitProtocolStatus.Registered &&
                result.Status != GasAbilityWaitProtocolStatus.Completed)
                return false;
            ApplyProtocolRecord(ref slot, in record);
            return true;
        }

        /// <summary>
        /// 创建 T+1 WaitRegistration 消息，ObservedHandle 与 query 均在此冻结。
        /// </summary>
        private static bool TryCreateRegistration(
            in GasAbilityContinuationCreateRequest request,
            in AbilityContinuationHandle handle,
            out PendingCommand registration)
        {
            registration = default;
            if (!request.ObservedAsc.IsValid || !TryNextTick(request.CurrentTick, out var availableTick))
                return false;
            registration = new PendingCommand
            {
                SourceAsc = request.Activation.OwnerAsc,
                TargetAsc = request.ObservedAsc,
                Activation = request.Activation,
                Continuation = handle,
                ObservedHandle = request.ObservedHandle,
                AvailableTick = availableTick,
                CommandSequence = request.CommandSequence,
                DueTick = request.DueTick,
                QueryKey = request.QueryKey,
                RegistrationGeneration = FirstRegistrationGeneration,
                CommandKind = (int)GasAbilityPendingCommandKind.WaitRegistration,
                WaitSemantic = request.Semantic,
                WaitPolicy = request.Policy,
                State = GasSlotBusinessState.Pending,
            };
            return true;
        }

        /// <summary>
        /// 分配 observed Subscription，并创建携带完整三句柄身份的注册 Ack。
        /// </summary>
        private static GasAbilityWaitProtocolStatus AllocateObservedSubscription(
            in OwnerAscHandle observedOwner,
            in PendingCommand registration,
            in GasAbilityWaitProtocolRecord record,
            ulong availableTick,
            ulong registrationSequence,
            int hardCapacity,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            ref GasSlabHead subscriptionHead,
            out PendingCommand response)
        {
            response = default;
            var storage = new GasAbilitySubscriptionSlabStorage { Buffer = subscriptions };
            if (GasNonCompactingSlabAllocator.TryAllocate(
                    ref subscriptionHead, ref storage, hardCapacity, out var allocation) !=
                GasSlabStorageFailure.None)
                return GasAbilityWaitProtocolStatus.InvalidRequest;
            var handle = new AbilitySubscriptionHandle(
                registration.Activation.SimulationEpoch,
                observedOwner,
                allocation.SlotIndex,
                allocation.Generation);
            subscriptions[allocation.SlotIndex] = CreateSubscription(
                in registration, in record, in handle, in allocation, registrationSequence);
            response = CreateRegistrationAck(in registration, in handle, availableTick);
            return GasAbilityWaitProtocolStatus.Registered;
        }

        /// <summary>
        /// 将注册协议 record 投影为 observed ASC 的 live Subscription 槽。
        /// </summary>
        private static AbilitySubscriptionSlot CreateSubscription(
            in PendingCommand registration,
            in GasAbilityWaitProtocolRecord record,
            in AbilitySubscriptionHandle handle,
            in GasSlabAllocation allocation,
            ulong registrationSequence)
        {
            return new AbilitySubscriptionSlot
            {
                Header = allocation.LiveHeader,
                Handle = handle,
                ObservedAsc = registration.TargetAsc,
                SubscriberAsc = registration.SourceAsc,
                Activation = registration.Activation,
                Continuation = registration.Continuation,
                ObservedHandle = registration.ObservedHandle,
                ObservedRevisionAtRegister = record.ObservedOrdinal,
                RegistrationSequence = registrationSequence,
                QueryKey = registration.QueryKey,
                RegistrationGeneration = registration.RegistrationGeneration,
                WaitSemantic = registration.WaitSemantic,
                WaitPolicy = registration.WaitPolicy,
                State = GasAbilitySubscriptionState.Registered,
            };
        }

        /// <summary>
        /// 重复 registration 返回原 Ack；已消费或已取消 tuple 作为 generation fence 拒绝重建。
        /// </summary>
        private static bool TryHandleExistingRegistration(
            in PendingCommand registration,
            ulong availableTick,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            out GasAbilityWaitProtocolStatus status,
            out PendingCommand response)
        {
            status = GasAbilityWaitProtocolStatus.InvalidRequest;
            response = default;
            for (var index = 0; index < subscriptions.Length; index++)
            {
                var slot = subscriptions[index];
                if (slot.Header.StorageState == GasSlabSlotState.Free ||
                    !MatchesRegistrationTuple(in registration, in slot) ||
                    slot.RegistrationGeneration < registration.RegistrationGeneration)
                    continue;
                if (slot.Header.StorageState == GasSlabSlotState.Live &&
                    slot.State == GasAbilitySubscriptionState.Registered &&
                    slot.RegistrationGeneration == registration.RegistrationGeneration)
                {
                    response = CreateRegistrationAck(in registration, in slot.Handle, availableTick);
                    status = GasAbilityWaitProtocolStatus.Registered;
                }
                else
                {
                    status = GasAbilityWaitProtocolStatus.NoOpStale;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// registration 去重键固定为两端身份、wait 描述与观察句柄，避免冲突请求复用旧 Ack。
        /// </summary>
        private static bool MatchesRegistrationTuple(
            in PendingCommand registration,
            in AbilitySubscriptionSlot subscription)
        {
            return subscription.ObservedAsc.Equals(registration.TargetAsc) &&
                   subscription.SubscriberAsc.Equals(registration.SourceAsc) &&
                   subscription.Activation.Equals(registration.Activation) &&
                   subscription.Continuation.Equals(registration.Continuation) &&
                   subscription.ObservedHandle.Equals(registration.ObservedHandle) &&
                   subscription.QueryKey == registration.QueryKey &&
                   subscription.WaitSemantic == registration.WaitSemantic &&
                   subscription.WaitPolicy == registration.WaitPolicy;
        }

        /// <summary>
        /// 应用 observed 注册 Ack，Subscription owner 必须等于原冻结 observed ASC。
        /// </summary>
        private static bool ApplyRegistrationAck(
            in PendingCommand response,
            ref AbilityContinuationSlot slot,
            DynamicBuffer<AbilityContinuationSlot> continuations)
        {
            if (!response.Subscription.IsValid ||
                !response.Subscription.OwnerAsc.Equals(slot.ObservedAsc) ||
                response.Subscription.SimulationEpoch != slot.Activation.SimulationEpoch ||
                slot.WaitState != GasAbilityWaitState.PendingRegistration)
                return false;
            slot.Subscription = response.Subscription;
            slot.WaitState = GasAbilityWaitState.Registered;
            continuations[slot.Handle.SlotIndex] = slot;
            return true;
        }

        /// <summary>
        /// 应用 T 时 completion 的冻结结果，ResumeTick 必须精确为 T+1。
        /// </summary>
        private static bool ApplyCompletion(
            in PendingCommand response,
            ref AbilityContinuationSlot slot,
            DynamicBuffer<AbilityContinuationSlot> continuations)
        {
            if (response.CompletionTick == ulong.MaxValue ||
                response.ResumeTick != response.CompletionTick + 1 ||
                response.AvailableTick != response.ResumeTick ||
                response.CompletionReason == GasAbilityWaitCompletionReason.None ||
                !IsCompletionIdentityValid(in response, in slot) ||
                !IsCompletionReasonValid(slot.WaitSemantic, response.CompletionReason) ||
                (slot.WaitState != GasAbilityWaitState.PendingRegistration &&
                 slot.WaitState != GasAbilityWaitState.Registered))
                return false;
            if (slot.WaitPolicy == GasAbilityWaitPolicy.Persistent &&
                response.Subscription.IsValid && !IsNextWakeOrdinal(slot.WakeOrdinal, response.WakeOrdinal))
                return false;
            var observedOwnerGone =
                response.CompletionReason == GasAbilityWaitCompletionReason.ObservedOwnerGone;
            slot.Subscription = observedOwnerGone ? default : response.Subscription;
            slot.PayloadRange = observedOwnerGone ? default : response.PayloadRange;
            slot.CompletionReason = response.CompletionReason;
            slot.ResumeTick = response.ResumeTick;
            slot.WakeOrdinal = response.WakeOrdinal;
            slot.ObservedBaseline = observedOwnerGone ? 0 : response.ObservedRevision;
            slot.WaitState = !observedOwnerGone &&
                             slot.WaitPolicy == GasAbilityWaitPolicy.Persistent &&
                             response.Subscription.IsValid
                ? GasAbilityWaitState.Registered
                : GasAbilityWaitState.Completed;
            continuations[slot.Handle.SlotIndex] = slot;
            return true;
        }

        /// <summary>
        /// 从 observed Subscription 创建纯协议 record，保持 WakeOrdinal 与 baseline 连续。
        /// </summary>
        private static GasAbilityWaitProtocolRecord CreateProtocolRecord(
            in AbilitySubscriptionSlot subscription)
        {
            return new GasAbilityWaitProtocolRecord
            {
                Activation = subscription.Activation,
                Continuation = subscription.Continuation,
                Subscription = subscription.Handle,
                ObservedAsc = subscription.ObservedAsc,
                Semantic = subscription.WaitSemantic,
                Mode = subscription.WaitPolicy,
                LifecycleState = GasAbilityWaitState.Registered,
                ObservedOrdinal = subscription.ObservedRevisionAtRegister,
                WakeOrdinal = subscription.WakeOrdinal,
            };
        }

        /// <summary>
        /// 从 owner Continuation 创建 Timer 协议 record，不伪造 observed Subscription。
        /// </summary>
        private static GasAbilityWaitProtocolRecord CreateProtocolRecord(
            in AbilityContinuationSlot continuation)
        {
            return new GasAbilityWaitProtocolRecord
            {
                Activation = continuation.Activation,
                Continuation = continuation.Handle,
                ObservedAsc = continuation.ObservedAsc,
                Semantic = continuation.WaitSemantic,
                Mode = continuation.WaitPolicy,
                LifecycleState = continuation.WaitState,
                DueTick = continuation.DueTick,
                WakeOrdinal = continuation.WakeOrdinal,
                ResumeTick = continuation.ResumeTick,
                CompletionReason = continuation.CompletionReason,
            };
        }

        /// <summary>
        /// 将纯协议 wake 结果写回 observed Subscription，one-shot 首次匹配即 Completed。
        /// </summary>
        private static void ApplyWakeToSubscription(
            ref AbilitySubscriptionSlot subscription,
            in GasAbilityWaitProtocolRecord record,
            in GasAbilityWaitProtocolResult result)
        {
            subscription.WakeOrdinal = result.WakeOrdinal;
            subscription.ObservedRevisionAtRegister = record.ObservedOrdinal;
            if (result.Status == GasAbilityWaitProtocolStatus.Completed)
                subscription.State = GasAbilitySubscriptionState.Completed;
        }

        /// <summary>
        /// 创建包含完整身份、generation 与冻结 T+1 时序的 completion 消息。
        /// </summary>
        private static PendingCommand CreateCompletionResponse(
            in PendingCommand registration,
            in GasAbilityWaitProtocolResult result,
            ulong availableTick,
            in AbilitySubscriptionHandle subscription)
        {
            return new PendingCommand
            {
                SourceAsc = registration.TargetAsc,
                TargetAsc = registration.SourceAsc,
                Activation = registration.Activation,
                Continuation = registration.Continuation,
                Subscription = subscription,
                AvailableTick = availableTick,
                RegistrationSequence = registration.RegistrationSequence,
                CompletionTick = result.CompletionTick,
                ResumeTick = result.ResumeTick,
                WakeOrdinal = result.WakeOrdinal,
                RegistrationGeneration = registration.RegistrationGeneration,
                CommandKind = (int)GasAbilityPendingCommandKind.WaitCompletion,
                WaitSemantic = registration.WaitSemantic,
                WaitPolicy = registration.WaitPolicy,
                CompletionReason = result.CompletionReason,
                State = GasSlotBusinessState.Pending,
            };
        }

        /// <summary>
        /// 创建 observed writer 返回的 registration Ack。
        /// </summary>
        private static PendingCommand CreateRegistrationAck(
            in PendingCommand registration,
            in AbilitySubscriptionHandle subscription,
            ulong availableTick)
        {
            return new PendingCommand
            {
                SourceAsc = registration.TargetAsc,
                TargetAsc = registration.SourceAsc,
                Activation = registration.Activation,
                Continuation = registration.Continuation,
                Subscription = subscription,
                AvailableTick = availableTick,
                RegistrationSequence = registration.RegistrationSequence,
                RegistrationGeneration = registration.RegistrationGeneration,
                CommandKind = (int)GasAbilityPendingCommandKind.WaitRegistrationAck,
                WaitSemantic = registration.WaitSemantic,
                WaitPolicy = registration.WaitPolicy,
                State = GasSlotBusinessState.Pending,
            };
        }

        /// <summary>
        /// 创建 observed match 返回的 completion/wake 消息。
        /// </summary>
        private static PendingCommand CreateWakeResponse(
            in AbilitySubscriptionSlot subscription,
            in GasAbilityWaitProtocolResult result,
            ulong availableTick,
            in PendingCommand waitSignal)
        {
            return new PendingCommand
            {
                SourceAsc = subscription.ObservedAsc,
                TargetAsc = subscription.SubscriberAsc,
                Activation = subscription.Activation,
                Continuation = subscription.Continuation,
                Subscription = subscription.Handle,
                PayloadRange = waitSignal.PayloadRange,
                AvailableTick = availableTick,
                CommandSequence = waitSignal.CommandSequence,
                RegistrationSequence = subscription.RegistrationSequence,
                CompletionTick = result.CompletionTick,
                ResumeTick = result.ResumeTick,
                WakeOrdinal = result.WakeOrdinal,
                ObservedRevision = subscription.ObservedRevisionAtRegister,
                MatchedTagDepth = waitSignal.MatchedTagDepth,
                RegistrationGeneration = subscription.RegistrationGeneration,
                RecipientKindPriority = waitSignal.RecipientKindPriority,
                CommandKind = (int)GasAbilityPendingCommandKind.WaitCompletion,
                WaitSemantic = subscription.WaitSemantic,
                WaitPolicy = subscription.WaitPolicy,
                CompletionReason = result.CompletionReason,
                State = GasSlotBusinessState.Pending,
            };
        }

        /// <summary>
        /// 创建携带推进后 handshake generation 的 unsubscribe 命令。
        /// </summary>
        private static PendingCommand CreateUnsubscribe(
            in AbilityContinuationSlot slot,
            ulong availableTick,
            ulong commandSequence)
        {
            return new PendingCommand
            {
                SourceAsc = slot.Handle.OwnerAsc,
                TargetAsc = slot.ObservedAsc,
                Activation = slot.Activation,
                Continuation = slot.Handle,
                Subscription = slot.Subscription,
                ObservedHandle = slot.ObservedHandle,
                AvailableTick = availableTick,
                CommandSequence = commandSequence,
                QueryKey = slot.QueryKey,
                RegistrationGeneration = slot.RegistrationGeneration,
                CommandKind = (int)GasAbilityPendingCommandKind.WaitUnsubscribe,
                WaitSemantic = slot.WaitSemantic,
                WaitPolicy = slot.WaitPolicy,
                State = GasSlotBusinessState.Pending,
            };
        }

        /// <summary>
        /// 创建 observed writer 的 unsubscribe Ack，即使订阅尚未建立也完成取消握手。
        /// </summary>
        private static PendingCommand CreateUnsubscribeAck(
            in PendingCommand unsubscribe,
            ulong availableTick)
        {
            return new PendingCommand
            {
                SourceAsc = unsubscribe.TargetAsc,
                TargetAsc = unsubscribe.SourceAsc,
                Activation = unsubscribe.Activation,
                Continuation = unsubscribe.Continuation,
                Subscription = unsubscribe.Subscription,
                AvailableTick = availableTick,
                CommandSequence = unsubscribe.CommandSequence,
                RegistrationSequence = unsubscribe.RegistrationSequence,
                MatchedTagDepth = unsubscribe.MatchedTagDepth,
                RegistrationGeneration = unsubscribe.RegistrationGeneration,
                RecipientKindPriority = unsubscribe.RecipientKindPriority,
                CommandKind = (int)GasAbilityPendingCommandKind.WaitUnsubscribeAck,
                WaitSemantic = unsubscribe.WaitSemantic,
                WaitPolicy = unsubscribe.WaitPolicy,
                State = GasSlotBusinessState.Pending,
            };
        }

        /// <summary>
        /// 按显式 Subscription handle 或 activation/continuation tuple 查找被取消槽。
        /// </summary>
        private static int FindCancelledSubscription(
            in PendingCommand unsubscribe,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions)
        {
            if (unsubscribe.Subscription.IsValid &&
                unsubscribe.Subscription.SlotIndex >= 0 &&
                unsubscribe.Subscription.SlotIndex < subscriptions.Length)
            {
                var explicitSlot = subscriptions[unsubscribe.Subscription.SlotIndex];
                if (explicitSlot.Handle.Equals(unsubscribe.Subscription) &&
                    MatchesCancellationRecord(in unsubscribe, in explicitSlot))
                    return unsubscribe.Subscription.SlotIndex;
            }
            for (var index = 0; index < subscriptions.Length; index++)
            {
                var slot = subscriptions[index];
                if (slot.Header.StorageState != GasSlabSlotState.Free &&
                    MatchesCancellationRecord(in unsubscribe, in slot))
                    return index;
            }
            return -1;
        }

        /// <summary>
        /// cancellation 必须同时匹配两端 owner、三句柄身份与推进一个单位的握手 generation。
        /// </summary>
        private static bool MatchesCancellationRecord(
            in PendingCommand unsubscribe,
            in AbilitySubscriptionSlot subscription)
        {
            if (!subscription.ObservedAsc.Equals(unsubscribe.TargetAsc) ||
                !subscription.SubscriberAsc.Equals(unsubscribe.SourceAsc) ||
                !subscription.Activation.Equals(unsubscribe.Activation) ||
                !subscription.Continuation.Equals(unsubscribe.Continuation))
                return false;
            if (subscription.RegistrationGeneration == unsubscribe.RegistrationGeneration)
                return subscription.Header.StorageState == GasSlabSlotState.Tombstone;
            return subscription.RegistrationGeneration < uint.MaxValue &&
                   unsubscribe.RegistrationGeneration == subscription.RegistrationGeneration + 1;
        }

        /// <summary>
        /// 将 live Subscription 转 cancellation fence；现有 tombstone 只推进业务 generation。
        /// </summary>
        private static bool ApplyObservedCancellation(
            int slotIndex,
            uint cancellationGeneration,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            ref GasSlabHead subscriptionHead)
        {
            var slot = subscriptions[slotIndex];
            if (slot.Header.StorageState == GasSlabSlotState.Tombstone)
            {
                slot.RegistrationGeneration = cancellationGeneration;
                slot.State = GasAbilitySubscriptionState.Ending;
                subscriptions[slotIndex] = slot;
                return true;
            }
            var storage = new GasAbilitySubscriptionSlabStorage { Buffer = subscriptions };
            if (GasNonCompactingSlabAllocator.TryMarkTombstone(
                    in subscriptionHead, ref storage, slotIndex) != GasSlabStorageFailure.None)
                return false;
            slot = subscriptions[slotIndex];
            slot.RegistrationGeneration = cancellationGeneration;
            slot.State = GasAbilitySubscriptionState.Ending;
            subscriptions[slotIndex] = slot;
            return true;
        }

        /// <summary>
        /// unsubscribe 先到且尚无 Subscription 时建立同 Tick 可见的 tombstone generation fence。
        /// </summary>
        private static bool AllocateCancellationFence(
            in OwnerAscHandle observedOwner,
            in PendingCommand unsubscribe,
            int hardCapacity,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            ref GasSlabHead subscriptionHead)
        {
            var storage = new GasAbilitySubscriptionSlabStorage { Buffer = subscriptions };
            if (GasNonCompactingSlabAllocator.TryAllocate(
                    ref subscriptionHead, ref storage, hardCapacity, out var allocation) !=
                GasSlabStorageFailure.None)
                return false;
            var handle = new AbilitySubscriptionHandle(
                unsubscribe.Activation.SimulationEpoch,
                observedOwner,
                allocation.SlotIndex,
                allocation.Generation);
            subscriptions[allocation.SlotIndex] = new AbilitySubscriptionSlot
            {
                Header = allocation.LiveHeader,
                Handle = handle,
                ObservedAsc = observedOwner,
                SubscriberAsc = unsubscribe.SourceAsc,
                Activation = unsubscribe.Activation,
                Continuation = unsubscribe.Continuation,
                RegistrationGeneration = unsubscribe.RegistrationGeneration,
                ObservedHandle = unsubscribe.ObservedHandle,
                QueryKey = unsubscribe.QueryKey,
                WaitSemantic = unsubscribe.WaitSemantic,
                WaitPolicy = unsubscribe.WaitPolicy,
                State = GasAbilitySubscriptionState.Ending,
            };
            return ApplyObservedCancellation(
                allocation.SlotIndex,
                unsubscribe.RegistrationGeneration,
                subscriptions,
                ref subscriptionHead);
        }

        /// <summary>
        /// 将纯协议 record 的时序与 lifecycle 写回 Continuation 槽。
        /// </summary>
        private static void ApplyProtocolRecord(
            ref AbilityContinuationSlot slot,
            in GasAbilityWaitProtocolRecord record)
        {
            slot.Subscription = record.Subscription;
            slot.ResumeTick = record.ResumeTick;
            slot.WakeOrdinal = record.WakeOrdinal;
            slot.ObservedBaseline = record.ObservedOrdinal;
            slot.WaitState = record.LifecycleState;
            slot.CompletionReason = record.CompletionReason;
        }

        /// <summary>
        /// 将新 Continuation 接入 Activation child list 并递增 child count。
        /// </summary>
        private static void IncrementActivationChild(
            ref AbilityActivationSlot activation,
            DynamicBuffer<AbilityActivationSlot> activations)
        {
            activation.ContinuationCount++;
            activations[activation.Handle.SlotIndex] = activation;
        }

        /// <summary>
        /// 结束本地 owner Continuation、递减 child count，并保留 tombstone 到后续 maintenance。
        /// </summary>
        private static bool EndAndTombstoneContinuation(
            int slotIndex,
            in AbilityContinuationSlot current,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations,
            ref GasSlabHead continuationHead)
        {
            if (!TryResolveActivation(current.Activation, activations, out var activation) ||
                activation.ContinuationCount <= 0)
                return false;
            var storage = new GasAbilityContinuationSlabStorage { Buffer = continuations };
            if (GasNonCompactingSlabAllocator.TryMarkTombstone(
                    in continuationHead, ref storage, slotIndex) != GasSlabStorageFailure.None)
                return false;
            var slot = continuations[slotIndex];
            slot.WaitState = GasAbilityWaitState.Ended;
            continuations[slotIndex] = slot;
            activation.ContinuationCount--;
            activations[activation.Handle.SlotIndex] = activation;
            return true;
        }

        /// <summary>
        /// 回滚尚未发布的 Continuation 分配，保持 owner child list 零写。
        /// </summary>
        private static bool RollbackAllocation(
            int slotIndex,
            DynamicBuffer<AbilityContinuationSlot> continuations,
            ref GasSlabHead head)
        {
            var storage = new GasAbilityContinuationSlabStorage { Buffer = continuations };
            if (GasNonCompactingSlabAllocator.TryMarkTombstone(
                    in head, ref storage, slotIndex) != GasSlabStorageFailure.None)
                return false;
            GasNonCompactingSlabAllocator.TryRecycleTombstone(ref head, ref storage, slotIndex);
            return false;
        }

        /// <summary>
        /// 验证虚拟或真实 Subscription 的 live header、两端身份与冻结 wait 描述。
        /// </summary>
        private static bool IsRegisteredSubscriptionForOwner(
            in OwnerAscHandle observedOwner,
            in AbilitySubscriptionSlot subscription)
        {
            var knownSemantic = subscription.WaitSemantic >= GasAbilityWaitSemantic.Level &&
                                subscription.WaitSemantic <= GasAbilityWaitSemantic.HandleLifecycle;
            var knownPolicy = subscription.WaitPolicy == GasAbilityWaitPolicy.OneShot ||
                              subscription.WaitPolicy == GasAbilityWaitPolicy.Persistent;
            if (!observedOwner.IsValid || !knownSemantic || !knownPolicy ||
                subscription.Header.StorageState != GasSlabSlotState.Live ||
                !subscription.Handle.IsValid || !subscription.ObservedAsc.Equals(observedOwner) ||
                !subscription.Handle.OwnerAsc.Equals(observedOwner) ||
                subscription.Header.Generation != subscription.Handle.SlotGeneration ||
                !subscription.SubscriberAsc.IsValid || !subscription.Activation.IsValid ||
                !subscription.Continuation.IsValid || subscription.RegistrationGeneration == 0 ||
                subscription.State != GasAbilitySubscriptionState.Registered)
                return false;
            return subscription.Activation.OwnerAsc.Equals(subscription.SubscriberAsc) &&
                   subscription.Continuation.OwnerAsc.Equals(subscription.SubscriberAsc) &&
                   subscription.Handle.SimulationEpoch == subscription.Activation.SimulationEpoch &&
                   subscription.Continuation.SimulationEpoch == subscription.Activation.SimulationEpoch &&
                   IsSubscriptionObservedHandleShapeValid(in subscription);
        }

        /// <summary>
        /// 验证 durable resume 的 live command、owner 身份、握手代际与冻结 T+1 时序。
        /// </summary>
        private static bool IsValidRunnableOwnerResponse(
            in OwnerAscHandle owner,
            in PendingCommand command)
        {
            var knownSemantic = command.WaitSemantic >= GasAbilityWaitSemantic.Level &&
                                command.WaitSemantic <= GasAbilityWaitSemantic.Timer;
            var knownPolicy = command.WaitPolicy == GasAbilityWaitPolicy.OneShot ||
                              command.WaitPolicy == GasAbilityWaitPolicy.Persistent;
            if (!owner.IsValid || !knownSemantic || !knownPolicy ||
                (command.WaitSemantic == GasAbilityWaitSemantic.Timer &&
                 command.WaitPolicy != GasAbilityWaitPolicy.OneShot) ||
                command.Header.StorageState != GasSlabSlotState.Live ||
                command.Header.Generation == 0 ||
                command.CommandKind != (int)GasAbilityPendingCommandKind.WaitCompletion ||
                !command.TargetAsc.Equals(owner) || !command.SourceAsc.IsValid ||
                !command.Activation.IsValid || !command.Continuation.IsValid ||
                !command.Activation.OwnerAsc.Equals(owner) ||
                !command.Continuation.OwnerAsc.Equals(owner) ||
                command.Activation.SimulationEpoch != command.Continuation.SimulationEpoch ||
                command.RegistrationGeneration == 0 || command.CompletionTick == ulong.MaxValue ||
                command.CommandSequence == 0 ||
                command.ResumeTick != command.CompletionTick + 1 ||
                command.AvailableTick != command.ResumeTick ||
                !IsCompletionReasonValid(command.WaitSemantic, command.CompletionReason))
                return false;
            return command.Subscription.Equals(default) ||
                   command.Subscription.IsValid &&
                   command.Subscription.OwnerAsc.Equals(command.SourceAsc) &&
                   command.Subscription.SimulationEpoch == command.Activation.SimulationEpoch;
        }

        /// <summary>
        /// 校验 signal kind 与 semantic 的一一映射、输入 ordinal 及未决响应字段。
        /// </summary>
        private static bool IsWaitSignalShapeValid(
            in AbilitySubscriptionSlot subscription,
            in PendingCommand waitSignal)
        {
            if (waitSignal.CompletionTick != 0 || waitSignal.ResumeTick != 0 ||
                waitSignal.WakeOrdinal != 0 || waitSignal.DueTick != 0 ||
                waitSignal.CompletionReason != GasAbilityWaitCompletionReason.None ||
                !IsWaitSignalObservedHandleValid(in subscription, in waitSignal) ||
                !IsWaitSignalPayloadRangeValid(in subscription, in waitSignal))
                return false;
            switch (subscription.WaitSemantic)
            {
                case GasAbilityWaitSemantic.Level:
                    return waitSignal.WaitSignalKind == GasAbilityWaitSignalKind.LevelSatisfied &&
                           waitSignal.ObservedRevision == 0;
                case GasAbilityWaitSemantic.Edge:
                    return waitSignal.WaitSignalKind == GasAbilityWaitSignalKind.EdgeObserved &&
                           waitSignal.ObservedRevision != 0;
                case GasAbilityWaitSemantic.Event:
                    return waitSignal.WaitSignalKind == GasAbilityWaitSignalKind.EventObserved &&
                           waitSignal.ObservedRevision != 0;
                case GasAbilityWaitSemantic.HandleLifecycle:
                    return waitSignal.WaitSignalKind == GasAbilityWaitSignalKind.HandleRemoved &&
                           waitSignal.ObservedRevision == 0;
                default:
                    return false;
            }
        }

        /// <summary>
        /// HandleLifecycle signal 必须精确携带冻结 typed handle，其他语义必须保持 default。
        /// </summary>
        private static bool IsWaitSignalObservedHandleValid(
            in AbilitySubscriptionSlot subscription,
            in PendingCommand waitSignal)
        {
            return subscription.WaitSemantic == GasAbilityWaitSemantic.HandleLifecycle
                ? waitSignal.ObservedHandle.Equals(subscription.ObservedHandle)
                : waitSignal.ObservedHandle.Equals(default);
        }

        /// <summary>
        /// 可选 signal payload 必须是当前 simulation epoch 的稳定 range。
        /// </summary>
        private static bool IsWaitSignalPayloadRangeValid(
            in AbilitySubscriptionSlot subscription,
            in PendingCommand waitSignal)
        {
            return waitSignal.PayloadRange.Equals(default) ||
                   waitSignal.PayloadRange.IsValid &&
                   waitSignal.PayloadRange.SimulationEpoch == subscription.Activation.SimulationEpoch;
        }

        /// <summary>
        /// HandleLifecycle Subscription 必须冻结 typed handle，其余语义不得残留无关句柄。
        /// </summary>
        private static bool IsSubscriptionObservedHandleShapeValid(
            in AbilitySubscriptionSlot subscription)
        {
            if (subscription.WaitSemantic == GasAbilityWaitSemantic.HandleLifecycle)
            {
                return subscription.ObservedHandle.IsValid &&
                       subscription.ObservedHandle.OwnerAsc.Equals(subscription.ObservedAsc) &&
                       subscription.ObservedHandle.SimulationEpoch ==
                       subscription.Activation.SimulationEpoch;
            }
            return subscription.ObservedHandle.Equals(default);
        }

        /// <summary>
        /// 验证 WaitRegistration 的 destination、语义与稳定句柄形状。
        /// </summary>
        private static bool IsRegistrationForOwner(
            in PendingCommand registration,
            in OwnerAscHandle observedOwner)
        {
            var knownSemantic = registration.WaitSemantic >= GasAbilityWaitSemantic.Level &&
                                registration.WaitSemantic <= GasAbilityWaitSemantic.HandleLifecycle;
            var knownPolicy = registration.WaitPolicy == GasAbilityWaitPolicy.OneShot ||
                              registration.WaitPolicy == GasAbilityWaitPolicy.Persistent;
            return observedOwner.IsValid && knownSemantic && knownPolicy &&
                   registration.CommandKind == (int)GasAbilityPendingCommandKind.WaitRegistration &&
                   registration.CommandSequence != 0 &&
                   registration.TargetAsc.Equals(observedOwner) &&
                   registration.SourceAsc.IsValid && registration.Activation.IsValid &&
                   registration.Continuation.IsValid && registration.RegistrationGeneration != 0 &&
                   registration.Activation.OwnerAsc.Equals(registration.SourceAsc) &&
                   registration.Continuation.OwnerAsc.Equals(registration.SourceAsc) &&
                   registration.Activation.SimulationEpoch == registration.Continuation.SimulationEpoch &&
                   IsObservedHandleShapeValid(in registration);
        }

        /// <summary>
        /// HandleLifecycle 必须携带同 observed owner 的 typed 联合体，其余 wait 必须保持严格 default。
        /// </summary>
        private static bool IsObservedHandleShapeValid(in PendingCommand registration)
        {
            if (registration.WaitSemantic == GasAbilityWaitSemantic.HandleLifecycle)
            {
                return registration.ObservedHandle.IsValid &&
                       registration.ObservedHandle.OwnerAsc.Equals(registration.TargetAsc) &&
                       registration.ObservedHandle.SimulationEpoch ==
                       registration.Activation.SimulationEpoch;
            }
            return registration.ObservedHandle.Equals(default);
        }

        /// <summary>
        /// unsubscribe 必须保持 subscriber 三句柄同源，并由目标 observed ASC 精确接管。
        /// </summary>
        private static bool IsValidUnsubscribe(
            in PendingCommand unsubscribe,
            in OwnerAscHandle observedOwner)
        {
            if (unsubscribe.CommandKind != (int)GasAbilityPendingCommandKind.WaitUnsubscribe ||
                !unsubscribe.TargetAsc.Equals(observedOwner) || !unsubscribe.SourceAsc.IsValid ||
                !unsubscribe.Activation.IsValid || !unsubscribe.Continuation.IsValid ||
                unsubscribe.RegistrationGeneration < 2 || unsubscribe.CommandSequence == 0)
                return false;
            if (!unsubscribe.Activation.OwnerAsc.Equals(unsubscribe.SourceAsc) ||
                !unsubscribe.Continuation.OwnerAsc.Equals(unsubscribe.SourceAsc) ||
                unsubscribe.Activation.SimulationEpoch != unsubscribe.Continuation.SimulationEpoch)
                return false;
            var knownSemantic = unsubscribe.WaitSemantic >= GasAbilityWaitSemantic.Level &&
                                unsubscribe.WaitSemantic <= GasAbilityWaitSemantic.HandleLifecycle;
            var knownPolicy = unsubscribe.WaitPolicy == GasAbilityWaitPolicy.OneShot ||
                              unsubscribe.WaitPolicy == GasAbilityWaitPolicy.Persistent;
            return knownSemantic && knownPolicy &&
                   (unsubscribe.Subscription.Equals(default) ||
                   (unsubscribe.Subscription.OwnerAsc.Equals(observedOwner) &&
                     unsubscribe.Subscription.SimulationEpoch == unsubscribe.Activation.SimulationEpoch &&
                     unsubscribe.Subscription.IsValid));
        }

        /// <summary>
        /// 创建前拒绝未知 enum、Persistent Timer、无 observed owner 与无法形成 T+1 的 tick。
        /// </summary>
        private static bool IsValidCreateRequest(in GasAbilityContinuationCreateRequest request)
        {
            var knownSemantic = request.Semantic >= GasAbilityWaitSemantic.Level &&
                                request.Semantic <= GasAbilityWaitSemantic.Timer;
            var knownPolicy = request.Policy == GasAbilityWaitPolicy.OneShot ||
                              request.Policy == GasAbilityWaitPolicy.Persistent;
            if (!knownSemantic || !knownPolicy || request.CurrentTick == ulong.MaxValue)
                return false;
            if (request.Activation.SimulationEpoch == 0 || !request.Activation.OwnerAsc.IsValid)
                return false;
            if (request.Semantic == GasAbilityWaitSemantic.Timer)
                return request.Policy == GasAbilityWaitPolicy.OneShot && !request.ObservedAsc.IsValid &&
                       request.ObservedHandle.Equals(default);
            if (request.CommandSequence == 0)
                return false;
            if (!request.ObservedAsc.IsValid)
                return false;
            return request.Semantic == GasAbilityWaitSemantic.HandleLifecycle
                ? request.ObservedHandle.IsValid &&
                  request.ObservedHandle.OwnerAsc.Equals(request.ObservedAsc) &&
                  request.ObservedHandle.SimulationEpoch == request.Activation.SimulationEpoch
                : request.ObservedHandle.Equals(default);
        }

        /// <summary>
        /// completion 在 PendingRegistration 时无 Subscription，Registered 时必须精确命中当前句柄。
        /// </summary>
        private static bool IsCompletionIdentityValid(
            in PendingCommand response,
            in AbilityContinuationSlot slot)
        {
            if (response.CompletionReason == GasAbilityWaitCompletionReason.ObservedOwnerGone)
                return response.Subscription.Equals(default);
            if (slot.WaitState == GasAbilityWaitState.PendingRegistration)
                return response.Subscription.Equals(default);
            return slot.Subscription.IsValid && slot.Subscription.Equals(response.Subscription);
        }

        /// <summary>
        /// 复验 completion reason 与冻结 wait semantic，ObservedOwnerGone 是唯一跨语义终止原因。
        /// </summary>
        private static bool IsCompletionReasonValid(
            GasAbilityWaitSemantic semantic,
            GasAbilityWaitCompletionReason reason)
        {
            if (reason == GasAbilityWaitCompletionReason.ObservedOwnerGone)
                return semantic != GasAbilityWaitSemantic.Timer;
            switch (semantic)
            {
                case GasAbilityWaitSemantic.Level:
                    return reason == GasAbilityWaitCompletionReason.LevelSatisfied;
                case GasAbilityWaitSemantic.Edge:
                    return reason == GasAbilityWaitCompletionReason.EdgeObserved;
                case GasAbilityWaitSemantic.Event:
                    return reason == GasAbilityWaitCompletionReason.EventObserved;
                case GasAbilityWaitSemantic.HandleLifecycle:
                    return reason == GasAbilityWaitCompletionReason.HandleInvalid ||
                           reason == GasAbilityWaitCompletionReason.HandleAlreadyRemoved ||
                           reason == GasAbilityWaitCompletionReason.HandleRemoved;
                case GasAbilityWaitSemantic.Timer:
                    return reason == GasAbilityWaitCompletionReason.TimerDue;
                default:
                    return false;
            }
        }

        /// <summary>
        /// persistent wake 必须逐个消费，禁止重复、跳号、回退或 ulong 回绕。
        /// </summary>
        private static bool IsNextWakeOrdinal(ulong current, ulong candidate)
        {
            return current < ulong.MaxValue && candidate == current + 1;
        }

        /// <summary>
        /// Continuation 只能挂在明确可运行的未提交或已提交 Activation 上。
        /// </summary>
        private static bool IsRunnableActivationPhase(GasAbilityActivationPhase phase)
        {
            return phase == GasAbilityActivationPhase.RunningUncommitted ||
                   phase == GasAbilityActivationPhase.Committed;
        }

        /// <summary>
        /// cancel Ack 在双方都尚未分配 Subscription 时接受 default，否则必须精确匹配当前句柄。
        /// </summary>
        private static bool MatchesOptionalSubscription(
            in AbilitySubscriptionHandle response,
            in AbilitySubscriptionHandle current)
        {
            return current.IsValid ? current.Equals(response) : response.Equals(default);
        }

        /// <summary>
        /// 解析 live Activation 并复验完整 typed handle。
        /// </summary>
        private static bool TryResolveActivation(
            in AbilityActivationHandle handle,
            DynamicBuffer<AbilityActivationSlot> activations,
            out AbilityActivationSlot slot)
        {
            slot = default;
            if (!handle.IsValid || handle.SlotIndex < 0 || handle.SlotIndex >= activations.Length)
                return false;
            slot = activations[handle.SlotIndex];
            return slot.Header.StorageState == GasSlabSlotState.Live &&
                   slot.Header.Generation == handle.SlotGeneration && slot.Handle.Equals(handle);
        }

        /// <summary>
        /// 解析 live Continuation 并复验完整 typed handle。
        /// </summary>
        private static bool TryResolveContinuation(
            in AbilityContinuationHandle handle,
            DynamicBuffer<AbilityContinuationSlot> continuations,
            out AbilityContinuationSlot slot)
        {
            slot = default;
            if (!handle.IsValid || handle.SlotIndex < 0 || handle.SlotIndex >= continuations.Length)
                return false;
            slot = continuations[handle.SlotIndex];
            return slot.Header.StorageState == GasSlabSlotState.Live &&
                   slot.Header.Generation == handle.SlotGeneration && slot.Handle.Equals(handle);
        }

        /// <summary>
        /// 解析 live Subscription 并复验完整 typed handle。
        /// </summary>
        private static bool TryResolveSubscription(
            in AbilitySubscriptionHandle handle,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            out AbilitySubscriptionSlot slot)
        {
            slot = default;
            if (!handle.IsValid || handle.SlotIndex < 0 || handle.SlotIndex >= subscriptions.Length)
                return false;
            slot = subscriptions[handle.SlotIndex];
            return slot.Header.StorageState == GasSlabSlotState.Live &&
                   slot.Header.Generation == handle.SlotGeneration && slot.Handle.Equals(handle);
        }

        /// <summary>
        /// 计算内部 route 的最早 T+1 deliver tick，显式拒绝回绕。
        /// </summary>
        private static bool TryNextTick(ulong currentTick, out ulong nextTick)
        {
            if (currentTick == ulong.MaxValue)
            {
                nextTick = 0;
                return false;
            }
            nextTick = currentTick + 1;
            return true;
        }
    }
}
