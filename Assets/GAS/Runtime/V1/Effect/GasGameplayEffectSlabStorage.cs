using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 将 ActiveEffect DynamicBuffer 接入统一 non-compacting slab allocator。
    /// </summary>
    internal struct GasActiveEffectSlabStorage : IGasSlabHeaderStorage
    {
        internal DynamicBuffer<ActiveEffectSlot> Buffer;

        public int Count => Buffer.Length;

        /// <summary>
        /// 读取指定 ActiveEffect 槽头。
        /// </summary>
        public GasSlabSlotHeader ReadHeader(int slotIndex)
        {
            return Buffer[slotIndex].Header;
        }

        /// <summary>
        /// 仅覆盖指定 ActiveEffect 槽头，不移动任何相邻槽。
        /// </summary>
        public void WriteHeader(int slotIndex, in GasSlabSlotHeader header)
        {
            var slot = Buffer[slotIndex];
            slot.Header = header;
            Buffer[slotIndex] = slot;
        }

        /// <summary>
        /// 在 high-water 尾部追加尚未写入业务字段的 ActiveEffect 槽。
        /// </summary>
        public void AppendHeader(in GasSlabSlotHeader header)
        {
            Buffer.Add(new ActiveEffectSlot { Header = header });
        }
    }
}
