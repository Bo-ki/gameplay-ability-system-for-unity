using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 在线性化点从 observed ASC 权威状态构造五类 wait 的确定性注册样本。
    /// </summary>
    internal static class GasAbilityWaitObservationUtility
    {
        /// <summary>
        /// Level 读取固定 Tag query，Edge/Event 冻结注册序列水位，HandleLifecycle 解析 typed handle。
        /// </summary>
        internal static bool TrySampleRegistration(
            in OwnerAscHandle observedOwner,
            in PendingCommand registration,
            DynamicBuffer<TagCountSlot> tagCounts,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            DynamicBuffer<CooldownGateSlot> cooldowns,
            DynamicBuffer<ActiveEffectSlot> activeEffects,
            out GasAbilityWaitRegistrationSample sample)
        {
            sample = default;
            switch (registration.WaitSemantic)
            {
                case GasAbilityWaitSemantic.Level:
                    if (registration.QueryKey < 0 || registration.QueryKey >= tagCounts.Length)
                        return false;
                    sample = GasAbilityWaitRegistrationSample.ForLevel(
                        tagCounts[registration.QueryKey].InclusiveCount > 0);
                    return true;
                case GasAbilityWaitSemantic.Edge:
                    sample = GasAbilityWaitRegistrationSample.ForEdge(
                        registration.CommandSequence);
                    return registration.CommandSequence != 0;
                case GasAbilityWaitSemantic.Event:
                    sample = GasAbilityWaitRegistrationSample.ForEvent(
                        registration.CommandSequence);
                    return registration.CommandSequence != 0;
                case GasAbilityWaitSemantic.HandleLifecycle:
                    sample = GasAbilityWaitRegistrationSample.ForHandle(ResolveHandleState(
                        in observedOwner, in registration.ObservedHandle, grants, activations,
                        continuations, subscriptions, cooldowns, activeEffects));
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 将 typed observed handle 区分为 live、无效输入或曾存在但已移除的代际终态。
        /// </summary>
        private static GasAbilityObservedHandleState ResolveHandleState(
            in OwnerAscHandle observedOwner,
            in GasAbilityObservedHandle handle,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            DynamicBuffer<CooldownGateSlot> cooldowns,
            DynamicBuffer<ActiveEffectSlot> activeEffects)
        {
            if (!handle.IsValid || !handle.OwnerAsc.Equals(observedOwner) ||
                !TryReadHeader(in handle, grants, activations, continuations, subscriptions,
                    cooldowns, activeEffects, out var header))
                return GasAbilityObservedHandleState.Invalid;
            return header.StorageState == GasSlabSlotState.Live &&
                   header.Generation == handle.SlotGeneration
                ? GasAbilityObservedHandleState.Live
                : GasAbilityObservedHandleState.AlreadyRemoved;
        }

        /// <summary>
        /// 按联合体 Kind 从对应 owner-local slab 读取 header，禁止跨类型索引碰撞。
        /// </summary>
        private static bool TryReadHeader(
            in GasAbilityObservedHandle handle,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            DynamicBuffer<CooldownGateSlot> cooldowns,
            DynamicBuffer<ActiveEffectSlot> activeEffects,
            out GasSlabSlotHeader header)
        {
            header = default;
            if (handle.SlotIndex < 0)
                return false;
            switch (handle.Kind)
            {
                case HandleKind.GrantedAbility:
                    return TryReadHeader(handle.SlotIndex, grants, out header);
                case HandleKind.AbilityActivation:
                    return TryReadHeader(handle.SlotIndex, activations, out header);
                case HandleKind.AbilityContinuation:
                    return TryReadHeader(handle.SlotIndex, continuations, out header);
                case HandleKind.AbilitySubscription:
                    return TryReadHeader(handle.SlotIndex, subscriptions, out header);
                case HandleKind.CooldownGate:
                    return TryReadHeader(handle.SlotIndex, cooldowns, out header);
                case HandleKind.ActiveEffect:
                    return TryReadHeader(handle.SlotIndex, activeEffects, out header);
                default:
                    return false;
            }
        }

        /// <summary>
        /// 从 GrantedAbility slab 读取指定稳定槽 header。
        /// </summary>
        private static bool TryReadHeader(
            int index,
            DynamicBuffer<GrantedAbilitySlot> slots,
            out GasSlabSlotHeader header)
        {
            header = default;
            if (index >= slots.Length)
                return false;
            header = slots[index].Header;
            return true;
        }

        /// <summary>
        /// 从 Activation slab 读取指定稳定槽 header。
        /// </summary>
        private static bool TryReadHeader(
            int index,
            DynamicBuffer<AbilityActivationSlot> slots,
            out GasSlabSlotHeader header)
        {
            header = default;
            if (index >= slots.Length)
                return false;
            header = slots[index].Header;
            return true;
        }

        /// <summary>
        /// 从 Continuation slab 读取指定稳定槽 header。
        /// </summary>
        private static bool TryReadHeader(
            int index,
            DynamicBuffer<AbilityContinuationSlot> slots,
            out GasSlabSlotHeader header)
        {
            header = default;
            if (index >= slots.Length)
                return false;
            header = slots[index].Header;
            return true;
        }

        /// <summary>
        /// 从 Subscription slab 读取指定稳定槽 header。
        /// </summary>
        private static bool TryReadHeader(
            int index,
            DynamicBuffer<AbilitySubscriptionSlot> slots,
            out GasSlabSlotHeader header)
        {
            header = default;
            if (index >= slots.Length)
                return false;
            header = slots[index].Header;
            return true;
        }

        /// <summary>
        /// 从 CooldownGate slab 读取指定稳定槽 header。
        /// </summary>
        private static bool TryReadHeader(
            int index,
            DynamicBuffer<CooldownGateSlot> slots,
            out GasSlabSlotHeader header)
        {
            header = default;
            if (index >= slots.Length)
                return false;
            header = slots[index].Header;
            return true;
        }

        /// <summary>
        /// 从 ActiveEffect slab 读取指定稳定槽 header。
        /// </summary>
        private static bool TryReadHeader(
            int index,
            DynamicBuffer<ActiveEffectSlot> slots,
            out GasSlabSlotHeader header)
        {
            header = default;
            if (index >= slots.Length)
                return false;
            header = slots[index].Header;
            return true;
        }
    }
}
