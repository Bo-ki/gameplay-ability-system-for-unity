using System.Collections.Generic;
using GAS.Runtime;
using NUnit.Framework;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Runtime v1 slab 与 payload range 在回收、复用及失败路径上保持稳定身份。
    /// </summary>
    [TestFixture]
    public class RuntimeV1NonCompactingStorageTests
    {
        private const ulong Epoch = 3107;
        private static readonly OwnerAscHandle OwnerAsc = new OwnerAscHandle(7001, 3);

        /// <summary>
        /// 验证反复回收只复用目标槽，其他 live 槽的索引与 generation 始终不变。
        /// </summary>
        [Test]
        public void Slab持续Churn_复用固定索引且拒绝陈旧句柄()
        {
            var head = GasSlabHead.CreateEmpty();
            var storage = new SlabHeaderStorage();
            AssertAllocateSlab(ref head, ref storage, 2, out var churnedAllocation);
            AssertAllocateSlab(ref head, ref storage, 2, out var stableAllocation);
            var stableHandle = CreateGrantedHandle(in stableAllocation);

            for (var iteration = 0; iteration < 64; iteration++)
            {
                var oldHandle = CreateGrantedHandle(in churnedAllocation);
                var oldGeneration = churnedAllocation.Generation;

                Assert.That(
                    GasNonCompactingSlabAllocator.TryMarkTombstone(
                        in head,
                        ref storage,
                        churnedAllocation.SlotIndex),
                    Is.EqualTo(GasSlabStorageFailure.None));
                Assert.That(storage.GetHeader(churnedAllocation.SlotIndex).Generation, Is.EqualTo(oldGeneration));

                Assert.That(
                    GasNonCompactingSlabAllocator.TryRecycleTombstone(
                        ref head,
                        ref storage,
                        churnedAllocation.SlotIndex),
                    Is.EqualTo(GasSlabStorageFailure.None));
                AssertAllocateSlab(ref head, ref storage, 2, out churnedAllocation);

                Assert.That(churnedAllocation.SlotIndex, Is.EqualTo(0));
                Assert.That(churnedAllocation.Generation, Is.EqualTo(oldGeneration + 1));
                Assert.That(churnedAllocation.Reused, Is.True);
                AssertStaleGrantedHandle(in oldHandle, in head, ref storage);
                AssertGrantedHandleLive(in stableHandle, in head, ref storage);
            }

            Assert.That(head.HighWater, Is.EqualTo(2));
            Assert.That(storage.Count, Is.EqualTo(2));
            Assert.That(stableAllocation.SlotIndex, Is.EqualTo(1));
        }

        /// <summary>
        /// 验证 slab hard capacity 拒绝扩展且不会改变 high-water、free-head 或既有槽头。
        /// </summary>
        [Test]
        public void Slab容量耗尽_失败时状态保持不变()
        {
            var head = GasSlabHead.CreateEmpty();
            var storage = new SlabHeaderStorage();
            AssertAllocateSlab(ref head, ref storage, 1, out _);
            var expectedHead = head;
            var expectedHeader = storage.GetHeader(0);

            var failure = GasNonCompactingSlabAllocator.TryAllocate(
                ref head,
                ref storage,
                1,
                out var rejectedAllocation);

            Assert.That(failure, Is.EqualTo(GasSlabStorageFailure.CapacityExceeded));
            Assert.That(rejectedAllocation.Generation, Is.Zero);
            AssertSlabHead(in head, in expectedHead);
            var actualHeader = storage.GetHeader(0);
            AssertSlabHeader(in actualHeader, in expectedHeader);
            Assert.That(storage.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// 验证 tombstone generation 达到 uint 上限时显式失败并保持未入 free-list。
        /// </summary>
        [Test]
        public void SlabGeneration溢出_显式失败且不回绕()
        {
            var head = GasSlabHead.CreateEmpty();
            var storage = new SlabHeaderStorage();
            AssertAllocateSlab(ref head, ref storage, 1, out var allocation);
            var header = storage.GetHeader(allocation.SlotIndex);
            header.Generation = uint.MaxValue;
            storage.SetHeader(allocation.SlotIndex, in header);
            Assert.That(
                GasNonCompactingSlabAllocator.TryMarkTombstone(in head, ref storage, allocation.SlotIndex),
                Is.EqualTo(GasSlabStorageFailure.None));
            var expectedHead = head;
            var expectedHeader = storage.GetHeader(allocation.SlotIndex);

            var failure = GasNonCompactingSlabAllocator.TryRecycleTombstone(
                ref head,
                ref storage,
                allocation.SlotIndex);

            Assert.That(failure, Is.EqualTo(GasSlabStorageFailure.GenerationOverflow));
            AssertSlabHead(in head, in expectedHead);
            var actualHeader = storage.GetHeader(allocation.SlotIndex);
            AssertSlabHeader(in actualHeader, in expectedHeader);
            Assert.That(storage.GetHeader(allocation.SlotIndex).StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
        }

        /// <summary>
        /// 验证孤立 free 槽与 live 槽伪造 free 指针都会作为 free-list 损坏拒绝。
        /// </summary>
        [Test]
        public void SlabFreeList不完整或污染Live槽_显式拒绝()
        {
            var orphanHead = GasSlabHead.CreateEmpty();
            var orphanStorage = new SlabHeaderStorage();
            AssertAllocateSlab(ref orphanHead, ref orphanStorage, 1, out var allocation);
            Assert.That(GasNonCompactingSlabAllocator.TryMarkTombstone(
                in orphanHead, ref orphanStorage, allocation.SlotIndex), Is.EqualTo(GasSlabStorageFailure.None));
            Assert.That(GasNonCompactingSlabAllocator.TryRecycleTombstone(
                ref orphanHead, ref orphanStorage, allocation.SlotIndex), Is.EqualTo(GasSlabStorageFailure.None));
            orphanHead.FreeHeadIndex = -1;

            Assert.That(GasNonCompactingSlabAllocator.TryAllocate(
                ref orphanHead, ref orphanStorage, 1, out _), Is.EqualTo(GasSlabStorageFailure.FreeListCorrupt));

            var pollutedHead = GasSlabHead.CreateEmpty();
            var pollutedStorage = new SlabHeaderStorage();
            AssertAllocateSlab(ref pollutedHead, ref pollutedStorage, 2, out var live);
            var liveHeader = pollutedStorage.GetHeader(live.SlotIndex);
            liveHeader.NextFreeIndex = live.SlotIndex;
            pollutedStorage.SetHeader(live.SlotIndex, in liveHeader);
            Assert.That(GasNonCompactingSlabAllocator.TryAllocate(
                ref pollutedHead, ref pollutedStorage, 2, out _), Is.EqualTo(GasSlabStorageFailure.FreeListCorrupt));
        }

        /// <summary>
        /// 验证 exact-size range 在原 Offset 复用，新代际拒绝旧句柄且相邻 live range 不移动。
        /// </summary>
        [Test]
        public void PayloadRange精确复用_保持相邻Live范围并拒绝旧代际()
        {
            var state = GasPayloadRangeAllocatorState.Create(Epoch, in OwnerAsc);
            var storage = new PayloadRangeRecordStorage();
            AssertAllocateRange(ref state, ref storage, PayloadKind.Capture, 2, 4, 8, out var first);
            AssertAllocateRange(ref state, ref storage, PayloadKind.Capture, 3, 4, 8, out var stable);
            var stableRecord = storage.GetRecord(stable.RecordIndex);

            Assert.That(
                GasPayloadRangeAllocator.TryMarkTombstone(
                    in state,
                    ref storage,
                    in first.Handle,
                    PayloadKind.Capture),
                Is.EqualTo(GasPayloadRangeStorageFailure.None));
            Assert.That(
                GasPayloadRangeAllocator.TryRecycleTombstone(
                    ref state,
                    ref storage,
                    in first.Handle,
                    PayloadKind.Capture),
                Is.EqualTo(GasPayloadRangeStorageFailure.None));
            AssertAllocateRange(ref state, ref storage, PayloadKind.Capture, 2, 4, 8, out var reused);

            Assert.That(reused.Reused, Is.True);
            Assert.That(reused.RecordIndex, Is.EqualTo(first.RecordIndex));
            Assert.That(reused.Handle.Offset, Is.EqualTo(first.Handle.Offset));
            Assert.That(reused.Handle.RangeGeneration, Is.EqualTo(first.Handle.RangeGeneration + 1));
            Assert.That(
                GasPayloadRangeAllocator.Validate(in first.Handle, in state, ref storage, PayloadKind.Capture),
                Is.EqualTo(PayloadRangeValidationFailure.GenerationMismatch));
            Assert.That(
                GasPayloadRangeAllocator.Validate(in stable.Handle, in state, ref storage, PayloadKind.Capture),
                Is.EqualTo(PayloadRangeValidationFailure.None));
            var actualStableRecord = storage.GetRecord(stable.RecordIndex);
            AssertRangeRecord(in actualStableRecord, in stableRecord);
            Assert.That(state.ValueHighWater, Is.EqualTo(5));
        }

        /// <summary>
        /// 验证不同 kind 或不同长度不会占用 free range，之后的 exact 请求仍可原位复用。
        /// </summary>
        [Test]
        public void PayloadRange非精确请求_追加而不拆分或压缩Free范围()
        {
            var state = GasPayloadRangeAllocatorState.Create(Epoch, in OwnerAsc);
            var storage = new PayloadRangeRecordStorage();
            AssertAllocateRange(ref state, ref storage, PayloadKind.Capture, 2, 3, 8, out var capture);
            Assert.That(
                GasPayloadRangeAllocator.TryMarkTombstone(
                    in state,
                    ref storage,
                    in capture.Handle,
                    PayloadKind.Capture),
                Is.EqualTo(GasPayloadRangeStorageFailure.None));
            Assert.That(
                GasPayloadRangeAllocator.TryRecycleTombstone(
                    ref state,
                    ref storage,
                    in capture.Handle,
                    PayloadKind.Capture),
                Is.EqualTo(GasPayloadRangeStorageFailure.None));

            AssertAllocateRange(ref state, ref storage, PayloadKind.EffectSpec, 2, 3, 8, out var otherKind);
            Assert.That(otherKind.Reused, Is.False);
            Assert.That(otherKind.Handle.Offset, Is.EqualTo(2));
            AssertAllocateRange(ref state, ref storage, PayloadKind.Capture, 2, 3, 8, out var exact);

            Assert.That(exact.Reused, Is.True);
            Assert.That(exact.Handle.Offset, Is.EqualTo(0));
            Assert.That(otherKind.Handle.Offset, Is.EqualTo(2));
            Assert.That(state.ValueHighWater, Is.EqualTo(4));
            Assert.That(state.Records.HighWater, Is.EqualTo(2));
        }

        /// <summary>
        /// 验证 record 与 value 两类容量失败均不改变 allocator 或既有 record。
        /// </summary>
        [Test]
        public void PayloadRange容量耗尽_失败时状态保持不变()
        {
            var state = GasPayloadRangeAllocatorState.Create(Epoch, in OwnerAsc);
            var storage = new PayloadRangeRecordStorage();
            AssertAllocateRange(ref state, ref storage, PayloadKind.EffectSpec, 2, 1, 3, out _);
            var expectedState = state;
            var expectedRecord = storage.GetRecord(0);

            var recordFailure = GasPayloadRangeAllocator.TryAllocate(
                ref state,
                ref storage,
                PayloadKind.EffectSpec,
                1,
                1,
                3,
                out _);
            Assert.That(recordFailure, Is.EqualTo(GasPayloadRangeStorageFailure.RangeRecordCapacityExceeded));
            AssertRangeState(in state, in expectedState);
            var actualRecord = storage.GetRecord(0);
            AssertRangeRecord(in actualRecord, in expectedRecord);

            var valueFailure = GasPayloadRangeAllocator.TryAllocate(
                ref state,
                ref storage,
                PayloadKind.EffectSpec,
                2,
                2,
                3,
                out _);
            Assert.That(valueFailure, Is.EqualTo(GasPayloadRangeStorageFailure.PayloadValueCapacityExceeded));
            AssertRangeState(in state, in expectedState);
            actualRecord = storage.GetRecord(0);
            AssertRangeRecord(in actualRecord, in expectedRecord);
            Assert.That(storage.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// 验证 range free-list 遗漏 free record 时 fail-closed，不把孤儿范围静默泄漏为容量耗尽。
        /// </summary>
        [Test]
        public void PayloadRangeFreeList遗漏FreeRecord_显式拒绝()
        {
            var state = GasPayloadRangeAllocatorState.Create(Epoch, in OwnerAsc);
            var storage = new PayloadRangeRecordStorage();
            AssertAllocateRange(ref state, ref storage, PayloadKind.Capture, 2, 1, 2, out var allocation);
            Assert.That(GasPayloadRangeAllocator.TryMarkTombstone(
                in state, ref storage, in allocation.Handle, PayloadKind.Capture),
                Is.EqualTo(GasPayloadRangeStorageFailure.None));
            Assert.That(GasPayloadRangeAllocator.TryRecycleTombstone(
                ref state, ref storage, in allocation.Handle, PayloadKind.Capture),
                Is.EqualTo(GasPayloadRangeStorageFailure.None));
            state.Records.FreeHeadIndex = -1;

            Assert.That(GasPayloadRangeAllocator.TryAllocate(
                ref state, ref storage, PayloadKind.Capture, 2, 1, 2, out _),
                Is.EqualTo(GasPayloadRangeStorageFailure.FreeListCorrupt));
        }

        /// <summary>
        /// 验证两个 record 的物理区间发生部分重叠时 metadata 校验拒绝双重所有权。
        /// </summary>
        [Test]
        public void PayloadRangeRecord部分重叠_显式拒绝()
        {
            var state = GasPayloadRangeAllocatorState.Create(Epoch, in OwnerAsc);
            var storage = new PayloadRangeRecordStorage();
            AssertAllocateRange(ref state, ref storage, PayloadKind.Capture, 2, 3, 6, out _);
            AssertAllocateRange(ref state, ref storage, PayloadKind.EffectSpec, 2, 3, 6, out var second);
            var overlapping = storage.GetRecord(second.RecordIndex);
            overlapping.Offset = 1;
            storage.SetRecord(second.RecordIndex, in overlapping);

            Assert.That(GasPayloadRangeAllocator.TryAllocate(
                ref state, ref storage, PayloadKind.Capture, 1, 3, 6, out _),
                Is.EqualTo(GasPayloadRangeStorageFailure.MetadataCorrupt));
        }

        /// <summary>
        /// 验证永久 range record 表出现未归属 gap 时拒绝继续分配，防止静默泄漏 value 容量。
        /// </summary>
        [Test]
        public void PayloadRangeRecord存在Gap_显式拒绝()
        {
            var state = GasPayloadRangeAllocatorState.Create(Epoch, in OwnerAsc);
            var storage = new PayloadRangeRecordStorage();
            AssertAllocateRange(ref state, ref storage, PayloadKind.Capture, 2, 3, 8, out _);
            AssertAllocateRange(ref state, ref storage, PayloadKind.EffectSpec, 2, 3, 8, out var second);
            var gapped = storage.GetRecord(second.RecordIndex);
            gapped.Offset = 3;
            storage.SetRecord(second.RecordIndex, in gapped);
            state.ValueHighWater = 5;

            Assert.That(GasPayloadRangeAllocator.TryAllocate(
                ref state, ref storage, PayloadKind.Capture, 1, 3, 8, out _),
                Is.EqualTo(GasPayloadRangeStorageFailure.MetadataCorrupt));
        }

        /// <summary>
        /// 验证永久 range record 的 offset 顺序被交换时拒绝非规范元数据。
        /// </summary>
        [Test]
        public void PayloadRangeRecordOffset乱序_显式拒绝()
        {
            var state = GasPayloadRangeAllocatorState.Create(Epoch, in OwnerAsc);
            var storage = new PayloadRangeRecordStorage();
            AssertAllocateRange(ref state, ref storage, PayloadKind.Capture, 2, 3, 6, out var first);
            AssertAllocateRange(ref state, ref storage, PayloadKind.EffectSpec, 2, 3, 6, out var second);
            var firstRecord = storage.GetRecord(first.RecordIndex);
            var secondRecord = storage.GetRecord(second.RecordIndex);
            firstRecord.Offset = 2;
            secondRecord.Offset = 0;
            storage.SetRecord(first.RecordIndex, in firstRecord);
            storage.SetRecord(second.RecordIndex, in secondRecord);

            Assert.That(GasPayloadRangeAllocator.TryAllocate(
                ref state, ref storage, PayloadKind.Capture, 1, 3, 6, out _),
                Is.EqualTo(GasPayloadRangeStorageFailure.MetadataCorrupt));
        }

        /// <summary>
        /// 验证 Epoch、Owner、Kind、Offset 与 Length 任一不匹配都会被明确拒绝。
        /// </summary>
        [Test]
        public void PayloadRange句柄_完整校验所有权用途与物理范围()
        {
            var state = GasPayloadRangeAllocatorState.Create(Epoch, in OwnerAsc);
            var storage = new PayloadRangeRecordStorage();
            AssertAllocateRange(ref state, ref storage, PayloadKind.Capture, 2, 2, 4, out var allocation);
            var handle = allocation.Handle;
            var wrongOwner = new PayloadRangeHandle(
                Epoch,
                new OwnerAscHandle(9999, 1),
                handle.Offset,
                handle.Length,
                handle.RangeGeneration,
                handle.Kind);
            var wrongLength = new PayloadRangeHandle(
                Epoch,
                OwnerAsc,
                handle.Offset,
                1,
                handle.RangeGeneration,
                handle.Kind);
            var wrongOffset = new PayloadRangeHandle(
                Epoch,
                OwnerAsc,
                1,
                1,
                handle.RangeGeneration,
                handle.Kind);
            var wrongKindAndOwner = new PayloadRangeHandle(
                Epoch,
                new OwnerAscHandle(9999, 1),
                handle.Offset,
                handle.Length,
                handle.RangeGeneration,
                PayloadKind.EffectSpec);

            AssertRangeValidation(in handle, in state, ref storage, PayloadKind.Capture, PayloadRangeValidationFailure.None);
            AssertRangeValidation(in wrongOwner, in state, ref storage, PayloadKind.Capture, PayloadRangeValidationFailure.OwnerMismatch);
            AssertRangeValidation(in handle, in state, ref storage, PayloadKind.EffectSpec, PayloadRangeValidationFailure.KindMismatch);
            AssertRangeValidation(in wrongKindAndOwner, in state, ref storage, PayloadKind.Capture, PayloadRangeValidationFailure.KindMismatch);
            AssertRangeValidation(in wrongLength, in state, ref storage, PayloadKind.Capture, PayloadRangeValidationFailure.LengthMismatch);
            AssertRangeValidation(in wrongOffset, in state, ref storage, PayloadKind.Capture, PayloadRangeValidationFailure.RangeNotFound);
        }

        /// <summary>
        /// 验证 Epoch 与 PayloadKind 在 record store 访问前完成路由拒绝，且 Epoch 优先于 Kind。
        /// </summary>
        [Test]
        public void PayloadRange路由校验_错误Epoch或Kind时不读取Store()
        {
            var state = GasPayloadRangeAllocatorState.Create(Epoch, in OwnerAsc);
            state.ValueHighWater = 2;
            var forbiddenStorage = new ForbiddenPayloadRangeRecordStorage();
            var wrongKindAndOwner = new PayloadRangeHandle(
                Epoch,
                new OwnerAscHandle(9999, 1),
                0,
                2,
                1,
                PayloadKind.EffectSpec);
            var wrongEpochAndKind = new PayloadRangeHandle(
                Epoch + 1,
                OwnerAsc,
                0,
                2,
                1,
                PayloadKind.EffectSpec);

            var kindFailure = GasPayloadRangeAllocator.Validate(
                in wrongKindAndOwner,
                in state,
                ref forbiddenStorage,
                PayloadKind.Capture);
            var epochFailure = GasPayloadRangeAllocator.Validate(
                in wrongEpochAndKind,
                in state,
                ref forbiddenStorage,
                PayloadKind.Capture);

            Assert.That(kindFailure, Is.EqualTo(PayloadRangeValidationFailure.KindMismatch));
            Assert.That(epochFailure, Is.EqualTo(PayloadRangeValidationFailure.EpochMismatch));
        }

        /// <summary>
        /// 验证越界 PayloadKind 不能被分配、不能绕过路由校验，也不能作为 record 元数据继续运行。
        /// </summary>
        [Test]
        public void PayloadRange越界Kind_全路径FailClosed()
        {
            var unknownKind = (PayloadKind)byte.MaxValue;
            var state = GasPayloadRangeAllocatorState.Create(Epoch, in OwnerAsc);
            var emptyStorage = new PayloadRangeRecordStorage();
            Assert.That(GasPayloadRangeAllocator.TryAllocate(
                ref state, ref emptyStorage, unknownKind, 1, 1, 1, out _),
                Is.EqualTo(GasPayloadRangeStorageFailure.InvalidArgument));
            Assert.That(emptyStorage.Count, Is.Zero);

            var unknownHandle = new PayloadRangeHandle(Epoch, OwnerAsc, 0, 1, 1, unknownKind);
            var forbiddenStorage = new ForbiddenPayloadRangeRecordStorage();
            Assert.That(GasPayloadRangeAllocator.Validate(
                in unknownHandle, in state, ref forbiddenStorage, unknownKind),
                Is.EqualTo(PayloadRangeValidationFailure.KindMismatch));

            AssertAllocateRange(ref state, ref emptyStorage, PayloadKind.Capture, 1, 2, 2, out var allocation);
            var corruptRecord = emptyStorage.GetRecord(allocation.RecordIndex);
            corruptRecord.Kind = unknownKind;
            emptyStorage.SetRecord(allocation.RecordIndex, in corruptRecord);
            Assert.That(GasPayloadRangeAllocator.TryAllocate(
                ref state, ref emptyStorage, PayloadKind.Capture, 1, 2, 2, out _),
                Is.EqualTo(GasPayloadRangeStorageFailure.MetadataCorrupt));
        }

        /// <summary>
        /// 验证 range generation 达到 uint 上限时保持 tombstone 与 free-list 原状。
        /// </summary>
        [Test]
        public void PayloadRangeGeneration溢出_显式失败且不回绕()
        {
            var state = GasPayloadRangeAllocatorState.Create(Epoch, in OwnerAsc);
            var storage = new PayloadRangeRecordStorage();
            AssertAllocateRange(ref state, ref storage, PayloadKind.ActiveEffectPayload, 2, 2, 4, out var allocation);
            Assert.That(
                GasPayloadRangeAllocator.TryMarkTombstone(
                    in state,
                    ref storage,
                    in allocation.Handle,
                    PayloadKind.ActiveEffectPayload),
                Is.EqualTo(GasPayloadRangeStorageFailure.None));
            var record = storage.GetRecord(allocation.RecordIndex);
            record.Header.Generation = uint.MaxValue;
            storage.SetRecord(allocation.RecordIndex, in record);
            var maxHandle = new PayloadRangeHandle(
                Epoch,
                OwnerAsc,
                allocation.Handle.Offset,
                allocation.Handle.Length,
                uint.MaxValue,
                allocation.Handle.Kind);
            var expectedState = state;
            var expectedRecord = storage.GetRecord(allocation.RecordIndex);

            var failure = GasPayloadRangeAllocator.TryRecycleTombstone(
                ref state,
                ref storage,
                in maxHandle,
                PayloadKind.ActiveEffectPayload);

            Assert.That(failure, Is.EqualTo(GasPayloadRangeStorageFailure.GenerationOverflow));
            AssertRangeState(in state, in expectedState);
            var actualRecord = storage.GetRecord(allocation.RecordIndex);
            AssertRangeRecord(in actualRecord, in expectedRecord);
        }

        /// <summary>
        /// 分配一个 slab 槽并断言成功，减少测试对成功路径的重复样板。
        /// </summary>
        private static void AssertAllocateSlab(
            ref GasSlabHead head,
            ref SlabHeaderStorage storage,
            int hardCapacity,
            out GasSlabAllocation allocation)
        {
            var failure = GasNonCompactingSlabAllocator.TryAllocate(
                ref head,
                ref storage,
                hardCapacity,
                out allocation);
            Assert.That(failure, Is.EqualTo(GasSlabStorageFailure.None));
        }

        /// <summary>
        /// 从 slab 分配结果构造旧或当前 GrantedAbilityHandle。
        /// </summary>
        private static GrantedAbilityHandle CreateGrantedHandle(in GasSlabAllocation allocation)
        {
            return new GrantedAbilityHandle(
                Epoch,
                OwnerAsc,
                allocation.SlotIndex,
                allocation.Generation);
        }

        /// <summary>
        /// 断言已复用槽拒绝旧 generation 的 GrantedAbilityHandle。
        /// </summary>
        private static void AssertStaleGrantedHandle(
            in GrantedAbilityHandle handle,
            in GasSlabHead head,
            ref SlabHeaderStorage storage)
        {
            var header = storage.GetHeader(handle.SlotIndex);
            var failure = StableHandleValidator.Validate(
                handle.ToDiagnosticCarrier(),
                Epoch,
                OwnerAsc,
                head.HighWater,
                header.StorageState == GasSlabSlotState.Live,
                header.Generation,
                HandleKind.GrantedAbility);
            Assert.That(failure, Is.EqualTo(HandleValidationFailure.GenerationMismatch));
        }

        /// <summary>
        /// 断言未参与 churn 的 GrantedAbilityHandle 始终保持 live。
        /// </summary>
        private static void AssertGrantedHandleLive(
            in GrantedAbilityHandle handle,
            in GasSlabHead head,
            ref SlabHeaderStorage storage)
        {
            var header = storage.GetHeader(handle.SlotIndex);
            var failure = StableHandleValidator.Validate(
                handle.ToDiagnosticCarrier(),
                Epoch,
                OwnerAsc,
                head.HighWater,
                header.StorageState == GasSlabSlotState.Live,
                header.Generation,
                HandleKind.GrantedAbility);
            Assert.That(failure, Is.EqualTo(HandleValidationFailure.None));
        }

        /// <summary>
        /// 分配一个 payload range 并断言成功。
        /// </summary>
        private static void AssertAllocateRange(
            ref GasPayloadRangeAllocatorState state,
            ref PayloadRangeRecordStorage storage,
            PayloadKind kind,
            int length,
            int hardRangeRecordCapacity,
            int hardPayloadValueCapacity,
            out GasPayloadRangeAllocation allocation)
        {
            var failure = GasPayloadRangeAllocator.TryAllocate(
                ref state,
                ref storage,
                kind,
                length,
                hardRangeRecordCapacity,
                hardPayloadValueCapacity,
                out allocation);
            Assert.That(failure, Is.EqualTo(GasPayloadRangeStorageFailure.None));
        }

        /// <summary>
        /// 断言 payload range 校验返回指定的首个失败阶段。
        /// </summary>
        private static void AssertRangeValidation(
            in PayloadRangeHandle handle,
            in GasPayloadRangeAllocatorState state,
            ref PayloadRangeRecordStorage storage,
            PayloadKind expectedKind,
            PayloadRangeValidationFailure expectedFailure)
        {
            var failure = GasPayloadRangeAllocator.Validate(
                in handle,
                in state,
                ref storage,
                expectedKind);
            Assert.That(failure, Is.EqualTo(expectedFailure));
        }

        /// <summary>
        /// 断言两个 slab 头的 free-head 与 high-water 完全一致。
        /// </summary>
        private static void AssertSlabHead(in GasSlabHead actual, in GasSlabHead expected)
        {
            Assert.That(actual.FreeHeadIndex, Is.EqualTo(expected.FreeHeadIndex));
            Assert.That(actual.HighWater, Is.EqualTo(expected.HighWater));
            Assert.That(actual.FreeCount, Is.EqualTo(expected.FreeCount));
        }

        /// <summary>
        /// 断言两个槽头的代际、free 链与物理状态完全一致。
        /// </summary>
        private static void AssertSlabHeader(
            in GasSlabSlotHeader actual,
            in GasSlabSlotHeader expected)
        {
            Assert.That(actual.Generation, Is.EqualTo(expected.Generation));
            Assert.That(actual.NextFreeIndex, Is.EqualTo(expected.NextFreeIndex));
            Assert.That(actual.StorageState, Is.EqualTo(expected.StorageState));
        }

        /// <summary>
        /// 断言两个 range allocator 状态的 owner 与双 high-water 完全一致。
        /// </summary>
        private static void AssertRangeState(
            in GasPayloadRangeAllocatorState actual,
            in GasPayloadRangeAllocatorState expected)
        {
            Assert.That(actual.SimulationEpoch, Is.EqualTo(expected.SimulationEpoch));
            Assert.That(actual.OwnerAsc, Is.EqualTo(expected.OwnerAsc));
            AssertSlabHead(in actual.Records, in expected.Records);
            Assert.That(actual.ValueHighWater, Is.EqualTo(expected.ValueHighWater));
        }

        /// <summary>
        /// 断言两个 payload range record 的完整持久字段一致。
        /// </summary>
        private static void AssertRangeRecord(
            in GasPayloadRangeRecord actual,
            in GasPayloadRangeRecord expected)
        {
            AssertSlabHeader(in actual.Header, in expected.Header);
            Assert.That(actual.SimulationEpoch, Is.EqualTo(expected.SimulationEpoch));
            Assert.That(actual.OwnerAsc, Is.EqualTo(expected.OwnerAsc));
            Assert.That(actual.Offset, Is.EqualTo(expected.Offset));
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            Assert.That(actual.Kind, Is.EqualTo(expected.Kind));
        }

        /// <summary>
        /// 用 List 承载测试槽头，并按稳定索引实现生产 allocator 适配契约。
        /// </summary>
        private struct SlabHeaderStorage : IGasSlabHeaderStorage
        {
            private List<GasSlabSlotHeader> _headers;

            public int Count => _headers == null ? 0 : _headers.Count;

            /// <summary>
            /// 读取指定测试槽头。
            /// </summary>
            public GasSlabSlotHeader ReadHeader(int slotIndex)
            {
                return _headers[slotIndex];
            }

            /// <summary>
            /// 覆盖指定测试槽头。
            /// </summary>
            public void WriteHeader(int slotIndex, in GasSlabSlotHeader header)
            {
                _headers[slotIndex] = header;
            }

            /// <summary>
            /// 在测试 high-water 尾部追加槽头。
            /// </summary>
            public void AppendHeader(in GasSlabSlotHeader header)
            {
                EnsureCreated();
                _headers.Add(header);
            }

            /// <summary>
            /// 为断言读取指定槽头。
            /// </summary>
            public GasSlabSlotHeader GetHeader(int slotIndex)
            {
                return _headers[slotIndex];
            }

            /// <summary>
            /// 为溢出向量设置指定槽头。
            /// </summary>
            public void SetHeader(int slotIndex, in GasSlabSlotHeader header)
            {
                _headers[slotIndex] = header;
            }

            /// <summary>
            /// 首次 append 时创建测试 backing list。
            /// </summary>
            private void EnsureCreated()
            {
                if (_headers == null)
                    _headers = new List<GasSlabSlotHeader>();
            }
        }

        /// <summary>
        /// 用 List 承载测试 range record，并按稳定索引实现 allocator 适配契约。
        /// </summary>
        private struct PayloadRangeRecordStorage : IGasPayloadRangeRecordStorage
        {
            private List<GasPayloadRangeRecord> _records;

            public int Count => _records == null ? 0 : _records.Count;

            /// <summary>
            /// 读取指定测试 record。
            /// </summary>
            public GasPayloadRangeRecord ReadRecord(int recordIndex)
            {
                return _records[recordIndex];
            }

            /// <summary>
            /// 覆盖指定测试 record。
            /// </summary>
            public void WriteRecord(int recordIndex, in GasPayloadRangeRecord record)
            {
                _records[recordIndex] = record;
            }

            /// <summary>
            /// 在测试 record high-water 尾部追加元数据。
            /// </summary>
            public void AppendRecord(in GasPayloadRangeRecord record)
            {
                EnsureCreated();
                _records.Add(record);
            }

            /// <summary>
            /// 为断言读取指定 record。
            /// </summary>
            public GasPayloadRangeRecord GetRecord(int recordIndex)
            {
                return _records[recordIndex];
            }

            /// <summary>
            /// 为溢出向量覆盖指定 record。
            /// </summary>
            public void SetRecord(int recordIndex, in GasPayloadRangeRecord record)
            {
                _records[recordIndex] = record;
            }

            /// <summary>
            /// 首次 append 时创建测试 backing list。
            /// </summary>
            private void EnsureCreated()
            {
                if (_records == null)
                    _records = new List<GasPayloadRangeRecord>();
            }
        }

        /// <summary>
        /// 在任一 record store 访问时抛错，用于证明错误路由在物理读取前被拒绝。
        /// </summary>
        private struct ForbiddenPayloadRangeRecordStorage : IGasPayloadRangeRecordStorage
        {
            public int Count => throw new AssertionException("错误路由不应读取 payload range store。");

            /// <summary>
            /// 禁止错误路由读取 record。
            /// </summary>
            public GasPayloadRangeRecord ReadRecord(int recordIndex)
            {
                throw new AssertionException("错误路由不应读取 payload range record。");
            }

            /// <summary>
            /// 禁止错误路由写入 record。
            /// </summary>
            public void WriteRecord(int recordIndex, in GasPayloadRangeRecord record)
            {
                throw new AssertionException("错误路由不应写入 payload range record。");
            }

            /// <summary>
            /// 禁止错误路由追加 record。
            /// </summary>
            public void AppendRecord(in GasPayloadRangeRecord record)
            {
                throw new AssertionException("错误路由不应追加 payload range record。");
            }
        }
    }
}
