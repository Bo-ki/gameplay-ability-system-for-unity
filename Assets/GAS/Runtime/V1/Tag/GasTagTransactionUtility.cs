using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 表示 Tag exact/inclusive 计数事务的确定性失败原因。
    /// </summary>
    internal enum GasTagMutationFailure : byte
    {
        None = 0,
        InvalidBufferShape = 1,
        InvalidTagIndex = 2,
        InvalidCatalogRange = 3,
        ExistingCountInvalid = 4,
        Underflow = 5,
        Overflow = 6,
    }

    /// <summary>
    /// 记录一次 Tag 叶节点及其 ancestor inclusive count 的变更。
    /// </summary>
    internal struct GasTagMutationRecord
    {
        public int TagIndex;
        public int Delta;
        public int PreviousExactCount;
        public int PreviousInclusiveCount;
        public int ExactCount;
        public int InclusiveCount;
        public byte Changed;
    }

    /// <summary>
    /// 统一维护 Tag exact/inclusive authority 与 presence 派生位图。
    /// </summary>
    internal static class GasTagTransactionUtility
    {
        /// <summary>
        /// 只读验证 Tag delta，供 slab 回收等复合事务在任何权威写前完成预检。
        /// </summary>
        internal static bool TryValidateDelta(
            ref GasDefinitionCatalogBlob catalog,
            int tagIndex,
            int delta,
            DynamicBuffer<TagCountSlot> counts,
            DynamicBuffer<TagPresenceWord> presence,
            out GasTagMutationFailure failure)
        {
            failure = ValidateShapeAndRange(
                ref catalog, tagIndex, counts, presence, out var range);
            if (failure != GasTagMutationFailure.None)
                return false;
            failure = ValidateExistingCounts(counts);
            if (failure != GasTagMutationFailure.None || delta == 0)
                return failure == GasTagMutationFailure.None;
            failure = ValidateDelta(ref catalog, tagIndex, delta, counts, in range);
            return failure == GasTagMutationFailure.None;
        }

        /// <summary>
        /// 在单 writer 内原子应用 Tag delta，并同步完整 ancestor chain 与 presence cache。
        /// </summary>
        internal static bool TryApplyDelta(
            ref GasDefinitionCatalogBlob catalog,
            int tagIndex,
            int delta,
            DynamicBuffer<TagCountSlot> counts,
            DynamicBuffer<TagPresenceWord> presence,
            out GasTagMutationRecord mutation,
            out GasTagMutationFailure failure)
        {
            mutation = new GasTagMutationRecord
            {
                TagIndex = tagIndex,
                Delta = delta,
            };
            failure = ValidateShapeAndRange(
                ref catalog, tagIndex, counts, presence, out var range);
            if (failure != GasTagMutationFailure.None)
                return false;
            failure = ValidateExistingCounts(counts);
            if (failure != GasTagMutationFailure.None)
                return false;
            var target = counts[tagIndex];
            mutation.PreviousExactCount = target.ExactCount;
            mutation.PreviousInclusiveCount = target.InclusiveCount;
            if (delta == 0)
            {
                mutation.ExactCount = target.ExactCount;
                mutation.InclusiveCount = target.InclusiveCount;
                return true;
            }
            failure = ValidateDelta(ref catalog, tagIndex, delta, counts, in range);
            if (failure != GasTagMutationFailure.None)
                return false;

            for (var offset = 0; offset < range.Count; offset++)
            {
                var ancestor = catalog.TagCatalog.AncestorIndices[range.Start + offset];
                var value = counts[ancestor];
                value.InclusiveCount = (int)((long)value.InclusiveCount + delta);
                if (ancestor == tagIndex)
                    value.ExactCount = (int)((long)value.ExactCount + delta);
                counts[ancestor] = value;
            }
            RebuildPresence(counts, presence);
            target = counts[tagIndex];
            mutation.ExactCount = target.ExactCount;
            mutation.InclusiveCount = target.InclusiveCount;
            mutation.Changed = 1;
            return true;
        }

        /// <summary>
        /// 应用 Tag delta 的简化入口，供不需要审计记录的内部调用使用。
        /// </summary>
        internal static bool TryApplyDelta(
            ref GasDefinitionCatalogBlob catalog,
            int tagIndex,
            int delta,
            DynamicBuffer<TagCountSlot> counts,
            DynamicBuffer<TagPresenceWord> presence)
        {
            return TryApplyDelta(
                ref catalog,
                tagIndex,
                delta,
                counts,
                presence,
                out _,
                out _);
        }

        /// <summary>
        /// 验证 Tag 固定 buffer、ancestor range 与 dense 索引形状。
        /// </summary>
        private static GasTagMutationFailure ValidateShapeAndRange(
            ref GasDefinitionCatalogBlob catalog,
            int tagIndex,
            DynamicBuffer<TagCountSlot> counts,
            DynamicBuffer<TagPresenceWord> presence,
            out GasCatalogRange range)
        {
            range = default;
            var tagCount = catalog.TagCatalog.Entries.Length;
            if (counts.Length != tagCount || presence.Length != WordCount(tagCount))
                return GasTagMutationFailure.InvalidBufferShape;
            if (tagIndex < 0 || tagIndex >= tagCount)
                return GasTagMutationFailure.InvalidTagIndex;
            var entry = catalog.TagCatalog.Entries[tagIndex];
            if (entry.TagIndex != tagIndex)
                return GasTagMutationFailure.InvalidTagIndex;
            range = entry.AncestorIndexRange;
            if (range.Start < 0 || range.Count <= 0 ||
                range.Start > catalog.TagCatalog.AncestorIndices.Length - range.Count)
                return GasTagMutationFailure.InvalidCatalogRange;
            var previous = -1;
            var containsSelf = false;
            for (var offset = 0; offset < range.Count; offset++)
            {
                var ancestor = catalog.TagCatalog.AncestorIndices[range.Start + offset];
                if (ancestor <= previous || ancestor < 0 || ancestor >= tagCount)
                    return GasTagMutationFailure.InvalidCatalogRange;
                containsSelf |= ancestor == tagIndex;
                previous = ancestor;
            }
            return containsSelf
                ? GasTagMutationFailure.None
                : GasTagMutationFailure.InvalidCatalogRange;
        }

        /// <summary>
        /// 拒绝已有 exact/inclusive 负数，避免在损坏计数上继续累积。
        /// </summary>
        private static GasTagMutationFailure ValidateExistingCounts(
            DynamicBuffer<TagCountSlot> counts)
        {
            for (var index = 0; index < counts.Length; index++)
            {
                var value = counts[index];
                if (value.ExactCount < 0 || value.InclusiveCount < 0 ||
                    value.ExactCount > value.InclusiveCount)
                    return GasTagMutationFailure.ExistingCountInvalid;
            }
            return GasTagMutationFailure.None;
        }

        /// <summary>
        /// 预检目标叶与全部 ancestor 的 checked 加法，保证失败时没有半写入。
        /// </summary>
        private static GasTagMutationFailure ValidateDelta(
            ref GasDefinitionCatalogBlob catalog,
            int tagIndex,
            int delta,
            DynamicBuffer<TagCountSlot> counts,
            in GasCatalogRange range)
        {
            for (var offset = 0; offset < range.Count; offset++)
            {
                var ancestor = catalog.TagCatalog.AncestorIndices[range.Start + offset];
                var value = counts[ancestor];
                var nextInclusive = (long)value.InclusiveCount + delta;
                if (nextInclusive < 0)
                    return GasTagMutationFailure.Underflow;
                if (nextInclusive > int.MaxValue)
                    return GasTagMutationFailure.Overflow;
                if (ancestor == tagIndex)
                {
                    var nextExact = (long)value.ExactCount + delta;
                    if (nextExact < 0)
                        return GasTagMutationFailure.Underflow;
                    if (nextExact > int.MaxValue)
                        return GasTagMutationFailure.Overflow;
                }
            }
            return GasTagMutationFailure.None;
        }

        /// <summary>
        /// 从 inclusive authority 重建固定长度 presence cache，禁止其成为第二事实源。
        /// </summary>
        private static void RebuildPresence(
            DynamicBuffer<TagCountSlot> counts,
            DynamicBuffer<TagPresenceWord> presence)
        {
            for (var index = 0; index < presence.Length; index++)
                presence[index] = default;
            for (var index = 0; index < counts.Length; index++)
            {
                if (counts[index].InclusiveCount <= 0)
                    continue;
                var wordIndex = index / 64;
                var word = presence[wordIndex];
                word.Value |= 1UL << (index % 64);
                presence[wordIndex] = word;
            }
        }

        /// <summary>
        /// 计算固定 64 位 presence 位图长度。
        /// </summary>
        private static int WordCount(int count)
        {
            return count <= 0 ? 0 : ((count - 1) / 64) + 1;
        }
    }
}
