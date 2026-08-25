namespace GAS.Runtime
{
    /// <summary>
    /// 表示 non-compacting slab 状态转换失败的确定性原因。
    /// </summary>
    internal enum GasSlabStorageFailure : byte
    {
        None = 0,
        InvalidCapacity = 1,
        MetadataCorrupt = 2,
        FreeListCorrupt = 3,
        CapacityExceeded = 4,
        SlotIndexOutOfRange = 5,
        SlotNotLive = 6,
        SlotNotTombstone = 7,
        GenerationOverflow = 8,
    }

    /// <summary>
    /// 返回 slab 分配所确定的稳定索引、代际和待写入槽头。
    /// </summary>
    internal readonly struct GasSlabAllocation
    {
        public readonly int SlotIndex;
        public readonly uint Generation;
        public readonly GasSlabSlotHeader LiveHeader;
        public readonly bool Reused;

        /// <summary>
        /// 创建一次完整且尚未写入业务字段的 slab 分配结果。
        /// </summary>
        public GasSlabAllocation(
            int slotIndex,
            uint generation,
            in GasSlabSlotHeader liveHeader,
            bool reused)
        {
            SlotIndex = slotIndex;
            Generation = generation;
            LiveHeader = liveHeader;
            Reused = reused;
        }
    }

    /// <summary>
    /// 抽象 caller-owned slab 的槽头读写，使同一状态机可适配不同领域槽 Buffer。
    /// </summary>
    internal interface IGasSlabHeaderStorage
    {
        int Count { get; }

        /// <summary>
        /// 读取指定稳定索引的槽头副本。
        /// </summary>
        GasSlabSlotHeader ReadHeader(int slotIndex);

        /// <summary>
        /// 覆盖指定稳定索引的槽头且不移动其他槽。
        /// </summary>
        void WriteHeader(int slotIndex, in GasSlabSlotHeader header);

        /// <summary>
        /// 在 high-water 尾部追加一个新槽头。
        /// </summary>
        void AppendHeader(in GasSlabSlotHeader header);
    }

    /// <summary>
    /// 实现 owner-local slab 的非压缩分配、tombstone 与代际回收状态机。
    /// </summary>
    internal static class GasNonCompactingSlabAllocator
    {
        private const uint FirstGeneration = 1;

        /// <summary>
        /// 优先复用 free-head，否则在 hard capacity 内扩展 high-water。
        /// </summary>
        public static GasSlabStorageFailure TryAllocate<TStorage>(
            ref GasSlabHead head,
            ref TStorage storage,
            int hardCapacity,
            out GasSlabAllocation allocation)
            where TStorage : struct, IGasSlabHeaderStorage
        {
            allocation = default;
            var validation = ValidateMetadata(in head, ref storage, hardCapacity);
            if (validation != GasSlabStorageFailure.None)
                return validation;

            if (head.FreeHeadIndex >= 0)
                return ReuseFreeHead(ref head, ref storage, out allocation);

            if (head.HighWater >= hardCapacity)
                return GasSlabStorageFailure.CapacityExceeded;

            var slotIndex = head.HighWater;
            var liveHeader = GasSlabSlotHeader.CreateLive(FirstGeneration);
            storage.AppendHeader(in liveHeader);
            head.HighWater++;
            allocation = new GasSlabAllocation(slotIndex, FirstGeneration, in liveHeader, false);
            return GasSlabStorageFailure.None;
        }

        /// <summary>
        /// 将 live 槽转为仍属于原代际的 tombstone，等待引用交接完成。
        /// </summary>
        public static GasSlabStorageFailure TryMarkTombstone<TStorage>(
            in GasSlabHead head,
            ref TStorage storage,
            int slotIndex)
            where TStorage : struct, IGasSlabHeaderStorage
        {
            var validation = ValidateMetadata(in head, ref storage, head.HighWater);
            if (validation != GasSlabStorageFailure.None)
                return validation;

            if (slotIndex < 0 || slotIndex >= head.HighWater)
                return GasSlabStorageFailure.SlotIndexOutOfRange;

            var header = storage.ReadHeader(slotIndex);
            if (header.StorageState != GasSlabSlotState.Live)
                return GasSlabStorageFailure.SlotNotLive;

            header.MarkTombstone();
            storage.WriteHeader(slotIndex, in header);
            return GasSlabStorageFailure.None;
        }

        /// <summary>
        /// 将 tombstone 递增一个代际后接入 free-list，溢出时保持全部状态不变。
        /// </summary>
        public static GasSlabStorageFailure TryRecycleTombstone<TStorage>(
            ref GasSlabHead head,
            ref TStorage storage,
            int slotIndex)
            where TStorage : struct, IGasSlabHeaderStorage
        {
            var validation = ValidateMetadata(in head, ref storage, head.HighWater);
            if (validation != GasSlabStorageFailure.None)
                return validation;

            if (slotIndex < 0 || slotIndex >= head.HighWater)
                return GasSlabStorageFailure.SlotIndexOutOfRange;

            var header = storage.ReadHeader(slotIndex);
            if (header.StorageState != GasSlabSlotState.Tombstone)
                return GasSlabStorageFailure.SlotNotTombstone;

            if (header.Generation == uint.MaxValue)
                return GasSlabStorageFailure.GenerationOverflow;

            header.LinkFree(header.Generation + 1, head.FreeHeadIndex);
            storage.WriteHeader(slotIndex, in header);
            head.FreeHeadIndex = slotIndex;
            head.FreeCount++;
            return GasSlabStorageFailure.None;
        }

        /// <summary>
        /// 校验 high-water、容量和可达 free-list，且不修改 caller-owned 状态。
        /// </summary>
        private static GasSlabStorageFailure ValidateMetadata<TStorage>(
            in GasSlabHead head,
            ref TStorage storage,
            int hardCapacity)
            where TStorage : struct, IGasSlabHeaderStorage
        {
            if (hardCapacity < 0 || head.HighWater > hardCapacity)
                return GasSlabStorageFailure.InvalidCapacity;

            if (head.HighWater < 0 || head.FreeCount < 0 || head.FreeCount > head.HighWater ||
                storage.Count != head.HighWater)
                return GasSlabStorageFailure.MetadataCorrupt;

            if (head.FreeHeadIndex < -1 || head.FreeHeadIndex >= head.HighWater)
                return GasSlabStorageFailure.FreeListCorrupt;

            var freeCount = 0;
            for (var index = 0; index < head.HighWater; index++)
            {
                var header = storage.ReadHeader(index);
                if (header.Generation == 0 || !IsKnownState(header.StorageState))
                    return GasSlabStorageFailure.MetadataCorrupt;
                if (header.StorageState == GasSlabSlotState.Free)
                    freeCount++;
                else if (header.NextFreeIndex != -1)
                    return GasSlabStorageFailure.FreeListCorrupt;
            }

            var currentIndex = head.FreeHeadIndex;
            var visitedCount = 0;
            while (currentIndex >= 0)
            {
                if (visitedCount++ >= head.HighWater)
                    return GasSlabStorageFailure.FreeListCorrupt;

                var header = storage.ReadHeader(currentIndex);
                if (header.StorageState != GasSlabSlotState.Free || header.Generation == 0)
                    return GasSlabStorageFailure.FreeListCorrupt;

                currentIndex = header.NextFreeIndex;
                if (currentIndex < -1 || currentIndex >= head.HighWater)
                    return GasSlabStorageFailure.FreeListCorrupt;
            }

            return visitedCount == freeCount && freeCount == head.FreeCount
                ? GasSlabStorageFailure.None
                : GasSlabStorageFailure.FreeListCorrupt;
        }

        /// <summary>
        /// 判断槽头状态是否属于冻结的 Free、Live、Tombstone 三态。
        /// </summary>
        private static bool IsKnownState(GasSlabSlotState state)
        {
            return state == GasSlabSlotState.Free ||
                   state == GasSlabSlotState.Live ||
                   state == GasSlabSlotState.Tombstone;
        }

        /// <summary>
        /// 从已验证 free-head 弹出一个槽，并保持回收阶段写入的 generation 不变。
        /// </summary>
        private static GasSlabStorageFailure ReuseFreeHead<TStorage>(
            ref GasSlabHead head,
            ref TStorage storage,
            out GasSlabAllocation allocation)
            where TStorage : struct, IGasSlabHeaderStorage
        {
            var slotIndex = head.FreeHeadIndex;
            var freeHeader = storage.ReadHeader(slotIndex);
            var liveHeader = GasSlabSlotHeader.CreateLive(freeHeader.Generation);
            storage.WriteHeader(slotIndex, in liveHeader);
            head.FreeHeadIndex = freeHeader.NextFreeIndex;
            head.FreeCount--;
            allocation = new GasSlabAllocation(
                slotIndex,
                freeHeader.Generation,
                in liveHeader,
                true);
            return GasSlabStorageFailure.None;
        }
    }
}
