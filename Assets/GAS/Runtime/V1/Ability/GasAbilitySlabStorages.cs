using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 将 GrantedAbility DynamicBuffer 适配到统一 non-compacting slab allocator。
    /// </summary>
    internal struct GasGrantedAbilitySlabStorage : IGasSlabHeaderStorage
    {
        internal DynamicBuffer<GrantedAbilitySlot> Buffer;
        public int Count => Buffer.Length;

        /// <summary>
        /// 读取指定 grant 槽的物理头。
        /// </summary>
        public GasSlabSlotHeader ReadHeader(int slotIndex) => Buffer[slotIndex].Header;

        /// <summary>
        /// 只覆盖指定 grant 槽的物理头。
        /// </summary>
        public void WriteHeader(int slotIndex, in GasSlabSlotHeader header)
        {
            var slot = Buffer[slotIndex];
            slot.Header = header;
            Buffer[slotIndex] = slot;
        }

        /// <summary>
        /// 追加尚未写业务字段的新 grant 槽。
        /// </summary>
        public void AppendHeader(in GasSlabSlotHeader header)
        {
            Buffer.Add(new GrantedAbilitySlot { Header = header });
        }
    }

    /// <summary>
    /// 将 AbilityActivation DynamicBuffer 适配到统一 non-compacting slab allocator。
    /// </summary>
    internal struct GasAbilityActivationSlabStorage : IGasSlabHeaderStorage
    {
        internal DynamicBuffer<AbilityActivationSlot> Buffer;
        public int Count => Buffer.Length;

        /// <summary>
        /// 读取指定 activation 槽的物理头。
        /// </summary>
        public GasSlabSlotHeader ReadHeader(int slotIndex) => Buffer[slotIndex].Header;

        /// <summary>
        /// 只覆盖指定 activation 槽的物理头。
        /// </summary>
        public void WriteHeader(int slotIndex, in GasSlabSlotHeader header)
        {
            var slot = Buffer[slotIndex];
            slot.Header = header;
            Buffer[slotIndex] = slot;
        }

        /// <summary>
        /// 追加尚未写业务字段的新 activation 槽。
        /// </summary>
        public void AppendHeader(in GasSlabSlotHeader header)
        {
            Buffer.Add(new AbilityActivationSlot { Header = header });
        }
    }

    /// <summary>
    /// 将 AbilityContinuation DynamicBuffer 适配到统一 non-compacting slab allocator。
    /// </summary>
    internal struct GasAbilityContinuationSlabStorage : IGasSlabHeaderStorage
    {
        internal DynamicBuffer<AbilityContinuationSlot> Buffer;
        public int Count => Buffer.Length;

        /// <summary>
        /// 读取指定 continuation 槽的物理头。
        /// </summary>
        public GasSlabSlotHeader ReadHeader(int slotIndex) => Buffer[slotIndex].Header;

        /// <summary>
        /// 只覆盖指定 continuation 槽的物理头。
        /// </summary>
        public void WriteHeader(int slotIndex, in GasSlabSlotHeader header)
        {
            var slot = Buffer[slotIndex];
            slot.Header = header;
            Buffer[slotIndex] = slot;
        }

        /// <summary>
        /// 追加尚未写业务字段的新 continuation 槽。
        /// </summary>
        public void AppendHeader(in GasSlabSlotHeader header)
        {
            Buffer.Add(new AbilityContinuationSlot { Header = header });
        }
    }

    /// <summary>
    /// 将 AbilitySubscription DynamicBuffer 适配到统一 non-compacting slab allocator。
    /// </summary>
    internal struct GasAbilitySubscriptionSlabStorage : IGasSlabHeaderStorage
    {
        internal DynamicBuffer<AbilitySubscriptionSlot> Buffer;
        public int Count => Buffer.Length;

        /// <summary>
        /// 读取指定 subscription 槽的物理头。
        /// </summary>
        public GasSlabSlotHeader ReadHeader(int slotIndex) => Buffer[slotIndex].Header;

        /// <summary>
        /// 只覆盖指定 subscription 槽的物理头。
        /// </summary>
        public void WriteHeader(int slotIndex, in GasSlabSlotHeader header)
        {
            var slot = Buffer[slotIndex];
            slot.Header = header;
            Buffer[slotIndex] = slot;
        }

        /// <summary>
        /// 追加尚未写业务字段的新 subscription 槽。
        /// </summary>
        public void AppendHeader(in GasSlabSlotHeader header)
        {
            Buffer.Add(new AbilitySubscriptionSlot { Header = header });
        }
    }

    /// <summary>
    /// 将 CooldownGate DynamicBuffer 适配到统一 non-compacting slab allocator。
    /// </summary>
    internal struct GasCooldownGateSlabStorage : IGasSlabHeaderStorage
    {
        internal DynamicBuffer<CooldownGateSlot> Buffer;
        public int Count => Buffer.Length;

        /// <summary>
        /// 读取指定 cooldown 槽的物理头。
        /// </summary>
        public GasSlabSlotHeader ReadHeader(int slotIndex) => Buffer[slotIndex].Header;

        /// <summary>
        /// 只覆盖指定 cooldown 槽的物理头。
        /// </summary>
        public void WriteHeader(int slotIndex, in GasSlabSlotHeader header)
        {
            var slot = Buffer[slotIndex];
            slot.Header = header;
            Buffer[slotIndex] = slot;
        }

        /// <summary>
        /// 追加尚未写业务字段的新 cooldown 槽。
        /// </summary>
        public void AppendHeader(in GasSlabSlotHeader header)
        {
            Buffer.Add(new CooldownGateSlot { Header = header });
        }
    }

    /// <summary>
    /// 将 ActivationOwnedContribution DynamicBuffer 适配到统一 non-compacting slab allocator。
    /// </summary>
    internal struct GasAbilityContributionSlabStorage : IGasSlabHeaderStorage
    {
        internal DynamicBuffer<ActivationOwnedContributionSlot> Buffer;
        public int Count => Buffer.Length;

        /// <summary>
        /// 读取指定 contribution 槽的物理头。
        /// </summary>
        public GasSlabSlotHeader ReadHeader(int slotIndex) => Buffer[slotIndex].Header;

        /// <summary>
        /// 只覆盖指定 contribution 槽的物理头。
        /// </summary>
        public void WriteHeader(int slotIndex, in GasSlabSlotHeader header)
        {
            var slot = Buffer[slotIndex];
            slot.Header = header;
            Buffer[slotIndex] = slot;
        }

        /// <summary>
        /// 追加尚未写业务字段的新 contribution 槽。
        /// </summary>
        public void AppendHeader(in GasSlabSlotHeader header)
        {
            Buffer.Add(new ActivationOwnedContributionSlot { Header = header });
        }
    }

    /// <summary>
    /// 将 PendingCommand DynamicBuffer 适配到统一 non-compacting slab allocator。
    /// </summary>
    internal struct GasPendingCommandSlabStorage : IGasSlabHeaderStorage
    {
        internal DynamicBuffer<PendingCommand> Buffer;
        public int Count => Buffer.Length;

        /// <summary>
        /// 读取指定 pending command 槽的物理头。
        /// </summary>
        public GasSlabSlotHeader ReadHeader(int slotIndex) => Buffer[slotIndex].Header;

        /// <summary>
        /// 只覆盖指定 pending command 槽的物理头。
        /// </summary>
        public void WriteHeader(int slotIndex, in GasSlabSlotHeader header)
        {
            var slot = Buffer[slotIndex];
            slot.Header = header;
            Buffer[slotIndex] = slot;
        }

        /// <summary>
        /// 追加尚未写业务字段的新 pending command 槽。
        /// </summary>
        public void AppendHeader(in GasSlabSlotHeader header)
        {
            Buffer.Add(new PendingCommand { Header = header });
        }
    }
}
