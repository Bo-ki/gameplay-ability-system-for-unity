using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 保存一个 payload range 的稳定物理范围、所有权、用途与 free-list 槽头。
    /// </summary>
    [InternalBufferCapacity(0)]
    internal struct GasPayloadRangeRecord : IBufferElementData
    {
        public GasSlabSlotHeader Header;
        public ulong SimulationEpoch;
        public OwnerAscHandle OwnerAsc;
        public int Offset;
        public int Length;
        public PayloadKind Kind;
    }

    /// <summary>
    /// 保存单个 ASC payload range 表的稳定 owner、record slab 与只增 value high-water。
    /// </summary>
    internal struct GasPayloadRangeAllocatorState : IComponentData
    {
        public ulong SimulationEpoch;
        public OwnerAscHandle OwnerAsc;
        public GasSlabHead Records;
        public int ValueHighWater;

        /// <summary>
        /// 为一个确定的 Session Epoch 与 ASC owner 创建空 range allocator。
        /// </summary>
        public static GasPayloadRangeAllocatorState Create(
            ulong simulationEpoch,
            in OwnerAscHandle ownerAsc)
        {
            return new GasPayloadRangeAllocatorState
            {
                SimulationEpoch = simulationEpoch,
                OwnerAsc = ownerAsc,
                Records = GasSlabHead.CreateEmpty(),
                ValueHighWater = 0,
            };
        }
    }

    /// <summary>
    /// 保存全部持久 payload/capture range 的唯一 64-bit word store，字段解释由 PayloadKind 与生成 schema 决定。
    /// </summary>
    [InternalBufferCapacity(0)]
    internal struct GasPayloadValueSlot : IBufferElementData
    {
        public ulong ValueBits;
    }

    /// <summary>
    /// 抽象 caller-owned payload range record 表，算法只通过稳定索引读写元数据。
    /// </summary>
    internal interface IGasPayloadRangeRecordStorage
    {
        int Count { get; }

        /// <summary>
        /// 读取指定稳定 record 索引的元数据副本。
        /// </summary>
        GasPayloadRangeRecord ReadRecord(int recordIndex);

        /// <summary>
        /// 覆盖指定稳定 record，禁止移动任何相邻 live range。
        /// </summary>
        void WriteRecord(int recordIndex, in GasPayloadRangeRecord record);

        /// <summary>
        /// 在 record high-water 尾部追加新范围元数据。
        /// </summary>
        void AppendRecord(in GasPayloadRangeRecord record);
    }

    /// <summary>
    /// 将预安装的 DynamicBuffer 适配为 payload range allocator 的稳定 record 存储。
    /// </summary>
    internal struct GasPayloadRangeDynamicBufferStorage : IGasPayloadRangeRecordStorage
    {
        private DynamicBuffer<GasPayloadRangeRecord> _records;

        public int Count => _records.Length;

        /// <summary>
        /// 绑定当前 mutation lane 独占写入的 range record Buffer。
        /// </summary>
        public GasPayloadRangeDynamicBufferStorage(DynamicBuffer<GasPayloadRangeRecord> records)
        {
            _records = records;
        }

        /// <summary>
        /// 按稳定 record 索引读取 Buffer 元素。
        /// </summary>
        public GasPayloadRangeRecord ReadRecord(int recordIndex)
        {
            return _records[recordIndex];
        }

        /// <summary>
        /// 按稳定 record 索引覆盖 Buffer 元素且不改变物理长度。
        /// </summary>
        public void WriteRecord(int recordIndex, in GasPayloadRangeRecord record)
        {
            _records[recordIndex] = record;
        }

        /// <summary>
        /// 仅在 allocator 扩展 high-water 时向 Buffer 尾部追加 record。
        /// </summary>
        public void AppendRecord(in GasPayloadRangeRecord record)
        {
            _records.Add(record);
        }
    }

    /// <summary>
    /// 表示 payload range 分配或回收失败的确定性原因。
    /// </summary>
    internal enum GasPayloadRangeStorageFailure : byte
    {
        None = 0,
        InvalidArgument = 1,
        MetadataCorrupt = 2,
        FreeListCorrupt = 3,
        RangeRecordCapacityExceeded = 4,
        PayloadValueCapacityExceeded = 5,
        RangeNotFound = 6,
        RangeIdentityMismatch = 7,
        RangeNotLive = 8,
        RangeNotTombstone = 9,
        GenerationMismatch = 10,
        GenerationOverflow = 11,
    }

    /// <summary>
    /// 表示 payload range 句柄按 Epoch、PayloadKind、Owner、范围、Live 与代际校验遇到的首个失败。
    /// </summary>
    internal enum PayloadRangeValidationFailure : byte
    {
        None = 0,
        AllocatorMetadataCorrupt = 1,
        EpochMismatch = 2,
        KindMismatch = 3,
        OwnerMismatch = 4,
        RangeOutOfBounds = 5,
        RangeNotFound = 6,
        RecordIdentityMismatch = 7,
        LengthMismatch = 8,
        AllocationNotLive = 9,
        GenerationMismatch = 10,
    }

    /// <summary>
    /// 返回 range 分配的稳定句柄、record 索引及是否来自 exact-size 复用。
    /// </summary>
    internal readonly struct GasPayloadRangeAllocation
    {
        public readonly PayloadRangeHandle Handle;
        public readonly int RecordIndex;
        public readonly bool Reused;

        /// <summary>
        /// 创建一次已经完整占有 record 与 value range 的分配结果。
        /// </summary>
        public GasPayloadRangeAllocation(
            in PayloadRangeHandle handle,
            int recordIndex,
            bool reused)
        {
            Handle = handle;
            RecordIndex = recordIndex;
            Reused = reused;
        }
    }

    /// <summary>
    /// 实现 ASC-local payload range 的 exact-size free reuse 与完整句柄校验。
    /// </summary>
    internal static class GasPayloadRangeAllocator
    {
        private const uint FirstGeneration = 1;

        /// <summary>
        /// 按 free-list 顺序复用同 kind、同长度范围，否则在 value high-water 尾部扩展。
        /// </summary>
        public static GasPayloadRangeStorageFailure TryAllocate<TStorage>(
            ref GasPayloadRangeAllocatorState state,
            ref TStorage storage,
            PayloadKind kind,
            int length,
            int hardRangeRecordCapacity,
            int hardPayloadValueCapacity,
            out GasPayloadRangeAllocation allocation)
            where TStorage : struct, IGasPayloadRangeRecordStorage
        {
            allocation = default;
            if (!GasPayloadKindContract.IsKnown(kind) || length <= 0)
                return GasPayloadRangeStorageFailure.InvalidArgument;

            var validation = ValidateMetadata(
                in state,
                ref storage,
                hardRangeRecordCapacity,
                hardPayloadValueCapacity);
            if (validation != GasPayloadRangeStorageFailure.None)
                return validation;

            var search = FindExactFreeRange(in state, ref storage, kind, length, out var recordIndex, out var previousIndex);
            if (search != GasPayloadRangeStorageFailure.None)
                return search;

            if (recordIndex >= 0)
                return ReuseExactRange(ref state, ref storage, recordIndex, previousIndex, out allocation);

            return AppendRange(
                ref state,
                ref storage,
                kind,
                length,
                hardRangeRecordCapacity,
                hardPayloadValueCapacity,
                out allocation);
        }

        /// <summary>
        /// 按 Epoch、PayloadKind、Owner、范围、Live、Generation 验证句柄且先拒绝错误路由。
        /// </summary>
        public static PayloadRangeValidationFailure Validate<TStorage>(
            in PayloadRangeHandle handle,
            in GasPayloadRangeAllocatorState state,
            ref TStorage storage,
            PayloadKind expectedKind)
            where TStorage : struct, IGasPayloadRangeRecordStorage
        {
            // 先完成不触碰 store 的身份路由校验，避免用错 PayloadKind 时读取错误物理表。
            var identity = ValidateHandleIdentity(in handle, in state, expectedKind);
            if (identity != PayloadRangeValidationFailure.None)
                return identity;

            var metadata = ValidateMetadata(
                in state,
                ref storage,
                int.MaxValue,
                int.MaxValue);
            if (metadata != GasPayloadRangeStorageFailure.None)
                return PayloadRangeValidationFailure.AllocatorMetadataCorrupt;

            var recordIndex = FindRecordByOffset(in state, ref storage, handle.Offset);
            if (recordIndex < 0)
                return PayloadRangeValidationFailure.RangeNotFound;

            var record = storage.ReadRecord(recordIndex);
            return ValidateLiveRecord(in handle, in state, in record);
        }

        /// <summary>
        /// 将合法 live range 转为同代际 tombstone，保留范围供交接阶段解析。
        /// </summary>
        public static GasPayloadRangeStorageFailure TryMarkTombstone<TStorage>(
            in GasPayloadRangeAllocatorState state,
            ref TStorage storage,
            in PayloadRangeHandle handle,
            PayloadKind expectedKind)
            where TStorage : struct, IGasPayloadRangeRecordStorage
        {
            var validation = Validate(in handle, in state, ref storage, expectedKind);
            if (validation != PayloadRangeValidationFailure.None)
                return MapValidationFailure(validation);

            var recordIndex = FindRecordByOffset(in state, ref storage, handle.Offset);
            var record = storage.ReadRecord(recordIndex);
            record.Header.MarkTombstone();
            storage.WriteRecord(recordIndex, in record);
            return GasPayloadRangeStorageFailure.None;
        }

        /// <summary>
        /// 校验 tombstone 仍匹配旧句柄，递增 generation 后将 record 接入 free-list。
        /// </summary>
        public static GasPayloadRangeStorageFailure TryRecycleTombstone<TStorage>(
            ref GasPayloadRangeAllocatorState state,
            ref TStorage storage,
            in PayloadRangeHandle handle,
            PayloadKind expectedKind)
            where TStorage : struct, IGasPayloadRangeRecordStorage
        {
            // 回收同样先拒绝错 Epoch/Kind/Owner，随后才允许解析 record。
            var identity = ValidateHandleIdentity(in handle, in state, expectedKind);
            if (identity != PayloadRangeValidationFailure.None)
                return MapValidationFailure(identity);

            var metadata = ValidateMetadata(in state, ref storage, int.MaxValue, int.MaxValue);
            if (metadata != GasPayloadRangeStorageFailure.None)
                return metadata;

            var recordIndex = FindRecordByOffset(in state, ref storage, handle.Offset);
            if (recordIndex < 0)
                return GasPayloadRangeStorageFailure.RangeNotFound;

            var record = storage.ReadRecord(recordIndex);
            var matchFailure = ValidateTombstoneRecord(in handle, in state, in record);
            if (matchFailure != GasPayloadRangeStorageFailure.None)
                return matchFailure;

            if (record.Header.Generation == uint.MaxValue)
                return GasPayloadRangeStorageFailure.GenerationOverflow;

            record.Header.LinkFree(record.Header.Generation + 1, state.Records.FreeHeadIndex);
            storage.WriteRecord(recordIndex, in record);
            state.Records.FreeHeadIndex = recordIndex;
            return GasPayloadRangeStorageFailure.None;
        }

        /// <summary>
        /// 校验 allocator owner、high-water、record 范围与完整可达 free-list。
        /// </summary>
        private static GasPayloadRangeStorageFailure ValidateMetadata<TStorage>(
            in GasPayloadRangeAllocatorState state,
            ref TStorage storage,
            int hardRangeRecordCapacity,
            int hardPayloadValueCapacity)
            where TStorage : struct, IGasPayloadRangeRecordStorage
        {
            if (state.SimulationEpoch == 0 || !state.OwnerAsc.IsValid ||
                hardRangeRecordCapacity < 0 || hardPayloadValueCapacity < 0)
                return GasPayloadRangeStorageFailure.InvalidArgument;

            if (state.Records.HighWater < 0 || state.ValueHighWater < 0 ||
                storage.Count != state.Records.HighWater ||
                state.Records.HighWater > hardRangeRecordCapacity ||
                state.ValueHighWater > hardPayloadValueCapacity)
                return GasPayloadRangeStorageFailure.MetadataCorrupt;

            if (state.Records.FreeHeadIndex < -1 ||
                state.Records.FreeHeadIndex >= state.Records.HighWater)
                return GasPayloadRangeStorageFailure.FreeListCorrupt;

            return ValidateRecordsAndFreeList(in state, ref storage);
        }

        /// <summary>
        /// 校验所有 record 的固定身份和物理范围，再验证 free 链不存在断链或循环。
        /// </summary>
        private static GasPayloadRangeStorageFailure ValidateRecordsAndFreeList<TStorage>(
            in GasPayloadRangeAllocatorState state,
            ref TStorage storage)
            where TStorage : struct, IGasPayloadRangeRecordStorage
        {
            var freeCount = 0;
            var expectedOffset = 0;
            for (var index = 0; index < state.Records.HighWater; index++)
            {
                var record = storage.ReadRecord(index);
                if (!RecordBelongsToAllocator(in state, in record) ||
                    record.Header.Generation == 0 ||
                    !IsKnownState(record.Header.StorageState) ||
                    record.Offset != expectedOffset ||
                    !RangeFits(record.Offset, record.Length, state.ValueHighWater))
                    return GasPayloadRangeStorageFailure.MetadataCorrupt;
                if (record.Header.StorageState == GasSlabSlotState.Free)
                    freeCount++;
                else if (record.Header.NextFreeIndex != -1)
                    return GasPayloadRangeStorageFailure.FreeListCorrupt;
                expectedOffset += record.Length;
            }

            if (expectedOffset != state.ValueHighWater)
                return GasPayloadRangeStorageFailure.MetadataCorrupt;

            var currentIndex = state.Records.FreeHeadIndex;
            var visitedCount = 0;
            while (currentIndex >= 0)
            {
                if (visitedCount++ >= state.Records.HighWater)
                    return GasPayloadRangeStorageFailure.FreeListCorrupt;

                var record = storage.ReadRecord(currentIndex);
                if (record.Header.StorageState != GasSlabSlotState.Free)
                    return GasPayloadRangeStorageFailure.FreeListCorrupt;

                currentIndex = record.Header.NextFreeIndex;
                if (currentIndex < -1 || currentIndex >= state.Records.HighWater)
                    return GasPayloadRangeStorageFailure.FreeListCorrupt;
            }

            return visitedCount == freeCount
                ? GasPayloadRangeStorageFailure.None
                : GasPayloadRangeStorageFailure.FreeListCorrupt;
        }

        /// <summary>
        /// 判断 range record 状态是否属于冻结的 Free、Live、Tombstone 三态。
        /// </summary>
        private static bool IsKnownState(GasSlabSlotState state)
        {
            return state == GasSlabSlotState.Free ||
                   state == GasSlabSlotState.Live ||
                   state == GasSlabSlotState.Tombstone;
        }

        /// <summary>
        /// 在已验证 free-list 中查找第一个同 kind、同长度的确定性复用候选。
        /// </summary>
        private static GasPayloadRangeStorageFailure FindExactFreeRange<TStorage>(
            in GasPayloadRangeAllocatorState state,
            ref TStorage storage,
            PayloadKind kind,
            int length,
            out int recordIndex,
            out int previousIndex)
            where TStorage : struct, IGasPayloadRangeRecordStorage
        {
            recordIndex = -1;
            previousIndex = -1;
            var currentIndex = state.Records.FreeHeadIndex;
            while (currentIndex >= 0)
            {
                var record = storage.ReadRecord(currentIndex);
                if (record.Kind == kind && record.Length == length)
                {
                    recordIndex = currentIndex;
                    return GasPayloadRangeStorageFailure.None;
                }

                previousIndex = currentIndex;
                currentIndex = record.Header.NextFreeIndex;
            }

            previousIndex = -1;
            return GasPayloadRangeStorageFailure.None;
        }

        /// <summary>
        /// 从 free 链中摘除 exact-size record，并以原物理 Offset 创建新代际句柄。
        /// </summary>
        private static GasPayloadRangeStorageFailure ReuseExactRange<TStorage>(
            ref GasPayloadRangeAllocatorState state,
            ref TStorage storage,
            int recordIndex,
            int previousIndex,
            out GasPayloadRangeAllocation allocation)
            where TStorage : struct, IGasPayloadRangeRecordStorage
        {
            var record = storage.ReadRecord(recordIndex);
            var nextFreeIndex = record.Header.NextFreeIndex;
            if (previousIndex < 0)
            {
                state.Records.FreeHeadIndex = nextFreeIndex;
            }
            else
            {
                var previous = storage.ReadRecord(previousIndex);
                previous.Header.NextFreeIndex = nextFreeIndex;
                storage.WriteRecord(previousIndex, in previous);
            }

            record.Header = GasSlabSlotHeader.CreateLive(record.Header.Generation);
            storage.WriteRecord(recordIndex, in record);
            var handle = CreateHandle(in state, in record);
            allocation = new GasPayloadRangeAllocation(in handle, recordIndex, true);
            return GasPayloadRangeStorageFailure.None;
        }

        /// <summary>
        /// 在两个 hard capacity 均允许时追加 record 与 value high-water。
        /// </summary>
        private static GasPayloadRangeStorageFailure AppendRange<TStorage>(
            ref GasPayloadRangeAllocatorState state,
            ref TStorage storage,
            PayloadKind kind,
            int length,
            int hardRangeRecordCapacity,
            int hardPayloadValueCapacity,
            out GasPayloadRangeAllocation allocation)
            where TStorage : struct, IGasPayloadRangeRecordStorage
        {
            allocation = default;
            if (state.Records.HighWater >= hardRangeRecordCapacity)
                return GasPayloadRangeStorageFailure.RangeRecordCapacityExceeded;

            if (length > hardPayloadValueCapacity - state.ValueHighWater)
                return GasPayloadRangeStorageFailure.PayloadValueCapacityExceeded;

            var recordIndex = state.Records.HighWater;
            var record = new GasPayloadRangeRecord
            {
                Header = GasSlabSlotHeader.CreateLive(FirstGeneration),
                SimulationEpoch = state.SimulationEpoch,
                OwnerAsc = state.OwnerAsc,
                Offset = state.ValueHighWater,
                Length = length,
                Kind = kind,
            };
            storage.AppendRecord(in record);
            state.Records.HighWater++;
            state.ValueHighWater += length;
            var handle = CreateHandle(in state, in record);
            allocation = new GasPayloadRangeAllocation(in handle, recordIndex, false);
            return GasPayloadRangeStorageFailure.None;
        }

        /// <summary>
        /// 按 Offset 定位唯一 record；重复 Offset 被视为 allocator 元数据损坏。
        /// </summary>
        private static int FindRecordByOffset<TStorage>(
            in GasPayloadRangeAllocatorState state,
            ref TStorage storage,
            int offset)
            where TStorage : struct, IGasPayloadRangeRecordStorage
        {
            var foundIndex = -1;
            for (var index = 0; index < state.Records.HighWater; index++)
            {
                if (storage.ReadRecord(index).Offset != offset)
                    continue;

                if (foundIndex >= 0)
                    return -1;

                foundIndex = index;
            }

            return foundIndex;
        }

        /// <summary>
        /// 按 Epoch、PayloadKind、Owner 与 allocator value high-water 校验句柄外壳。
        /// </summary>
        private static PayloadRangeValidationFailure ValidateHandleIdentity(
            in PayloadRangeHandle handle,
            in GasPayloadRangeAllocatorState state,
            PayloadKind expectedKind)
        {
            if (handle.SimulationEpoch != state.SimulationEpoch)
                return PayloadRangeValidationFailure.EpochMismatch;

            if (!GasPayloadKindContract.IsKnown(expectedKind) ||
                !GasPayloadKindContract.IsKnown(handle.Kind) || handle.Kind != expectedKind)
                return PayloadRangeValidationFailure.KindMismatch;

            if (!handle.OwnerAsc.Equals(state.OwnerAsc))
                return PayloadRangeValidationFailure.OwnerMismatch;

            if (!RangeFits(handle.Offset, handle.Length, state.ValueHighWater))
                return PayloadRangeValidationFailure.RangeOutOfBounds;

            return PayloadRangeValidationFailure.None;
        }

        /// <summary>
        /// 校验已定位 record 的持久身份、长度、Live 状态和 generation。
        /// </summary>
        private static PayloadRangeValidationFailure ValidateLiveRecord(
            in PayloadRangeHandle handle,
            in GasPayloadRangeAllocatorState state,
            in GasPayloadRangeRecord record)
        {
            if (!RecordBelongsToAllocator(in state, in record) || record.Kind != handle.Kind)
                return PayloadRangeValidationFailure.RecordIdentityMismatch;

            if (record.Length != handle.Length)
                return PayloadRangeValidationFailure.LengthMismatch;

            if (record.Header.StorageState != GasSlabSlotState.Live)
                return PayloadRangeValidationFailure.AllocationNotLive;

            if (record.Header.Generation != handle.RangeGeneration)
                return PayloadRangeValidationFailure.GenerationMismatch;

            return PayloadRangeValidationFailure.None;
        }

        /// <summary>
        /// 校验旧句柄精确指向当前 tombstone 且仍属于回收前 generation。
        /// </summary>
        private static GasPayloadRangeStorageFailure ValidateTombstoneRecord(
            in PayloadRangeHandle handle,
            in GasPayloadRangeAllocatorState state,
            in GasPayloadRangeRecord record)
        {
            if (!RecordBelongsToAllocator(in state, in record) ||
                record.Kind != handle.Kind || record.Length != handle.Length)
                return GasPayloadRangeStorageFailure.RangeIdentityMismatch;

            if (record.Header.StorageState != GasSlabSlotState.Tombstone)
                return GasPayloadRangeStorageFailure.RangeNotTombstone;

            if (record.Header.Generation != handle.RangeGeneration)
                return GasPayloadRangeStorageFailure.GenerationMismatch;

            return GasPayloadRangeStorageFailure.None;
        }

        /// <summary>
        /// 判断 record 是否严格属于 allocator 冻结的 Epoch 与 Owner。
        /// </summary>
        private static bool RecordBelongsToAllocator(
            in GasPayloadRangeAllocatorState state,
            in GasPayloadRangeRecord record)
        {
            return record.SimulationEpoch == state.SimulationEpoch &&
                   record.OwnerAsc.Equals(state.OwnerAsc) &&
                   GasPayloadKindContract.IsKnown(record.Kind);
        }

        /// <summary>
        /// 以减法检查范围，避免 Offset + Length 的整数回绕。
        /// </summary>
        private static bool RangeFits(int offset, int length, int valueHighWater)
        {
            return offset >= 0 && length > 0 &&
                   offset <= valueHighWater &&
                   length <= valueHighWater - offset;
        }

        /// <summary>
        /// 从 allocator 与 record 的冻结字段创建完整 payload range 句柄。
        /// </summary>
        private static PayloadRangeHandle CreateHandle(
            in GasPayloadRangeAllocatorState state,
            in GasPayloadRangeRecord record)
        {
            return new PayloadRangeHandle(
                state.SimulationEpoch,
                state.OwnerAsc,
                record.Offset,
                record.Length,
                record.Header.Generation,
                record.Kind);
        }

        /// <summary>
        /// 将句柄校验失败收敛为写操作可返回的存储失败原因。
        /// </summary>
        private static GasPayloadRangeStorageFailure MapValidationFailure(
            PayloadRangeValidationFailure failure)
        {
            switch (failure)
            {
                case PayloadRangeValidationFailure.AllocatorMetadataCorrupt:
                    return GasPayloadRangeStorageFailure.MetadataCorrupt;
                case PayloadRangeValidationFailure.RangeNotFound:
                    return GasPayloadRangeStorageFailure.RangeNotFound;
                case PayloadRangeValidationFailure.AllocationNotLive:
                    return GasPayloadRangeStorageFailure.RangeNotLive;
                case PayloadRangeValidationFailure.GenerationMismatch:
                    return GasPayloadRangeStorageFailure.GenerationMismatch;
                default:
                    return GasPayloadRangeStorageFailure.RangeIdentityMismatch;
            }
        }
    }
}
