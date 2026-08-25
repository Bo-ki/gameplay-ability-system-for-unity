using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 表示 Attribute 单写事务在索引、数值、revision 或 dirty 位图上的确定性失败原因。
    /// </summary>
    internal enum GasAttributeMutationFailure : byte
    {
        None = 0,
        InvalidBufferShape = 1,
        InvalidAttributeIndex = 2,
        InvalidCatalogEntry = 3,
        NonFiniteInput = 4,
        NonFiniteResult = 5,
        RevisionOverflow = 6,
    }

    /// <summary>
    /// 记录一次已经通过校验的 Attribute Base/Current 变更，供事实与审计层复用。
    /// </summary>
    internal struct GasAttributeMutationRecord
    {
        public int AttributeLayoutIndex;
        public float RequestedBaseDelta;
        public float RequestedCurrentDelta;
        public float PreviousBase;
        public float PreviousCurrent;
        public float UnclampedBase;
        public float UnclampedCurrent;
        public float AppliedBase;
        public float AppliedCurrent;
        public uint PreviousRevision;
        public uint Revision;
        public byte Changed;
    }

    /// <summary>
    /// 统一执行 ASC Attribute 的 Base/Current、clamp、revision 与 dirty 位图写入。
    /// </summary>
    internal static class GasAttributeTransactionUtility
    {
        /// <summary>
        /// 在固定 Attribute 布局上原子应用 Base/Current delta，并更新 revision 与 dirty 位。
        /// </summary>
        internal static bool TryApplyDelta(
            ref GasDefinitionCatalogBlob catalog,
            int attributeLayoutIndex,
            float baseDelta,
            float currentDelta,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<AttributeDirtyWord> dirtyWords,
            out GasAttributeMutationRecord mutation,
            out GasAttributeMutationFailure failure)
        {
            return TryApplyDeltaCore(
                ref catalog,
                attributeLayoutIndex,
                baseDelta,
                currentDelta,
                attributes,
                dirtyWords,
                true,
                out mutation,
                out failure);
        }

        /// <summary>
        /// 只读验证 Attribute delta，供复合事务在任何 authority 写入前完成 no-fail 预检。
        /// </summary>
        internal static bool TryValidateDelta(
            ref GasDefinitionCatalogBlob catalog,
            int attributeLayoutIndex,
            float baseDelta,
            float currentDelta,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<AttributeDirtyWord> dirtyWords,
            out GasAttributeMutationRecord mutation,
            out GasAttributeMutationFailure failure)
        {
            return TryPrepareMutation(
                ref catalog,
                attributeLayoutIndex,
                baseDelta,
                currentDelta,
                attributes,
                dirtyWords,
                true,
                out mutation,
                out failure);
        }

        /// <summary>
        /// 在不写入 ASC buffer 的 shadow 值上模拟一次 Attribute delta，供复合 transaction 预演顺序结果。
        /// </summary>
        internal static bool TrySimulateDelta(
            ref GasDefinitionCatalogBlob catalog,
            int attributeLayoutIndex,
            float baseDelta,
            float currentDelta,
            in AttributeValueSlot current,
            out AttributeValueSlot next,
            out GasAttributeMutationFailure failure)
        {
            next = current;
            failure = ValidateCatalogEntry(ref catalog, attributeLayoutIndex, out var entry);
            if (failure != GasAttributeMutationFailure.None)
                return false;
            if (!IsFinite(baseDelta) || !IsFinite(currentDelta) ||
                !IsFinite(current.Base) || !IsFinite(current.Current))
            {
                failure = GasAttributeMutationFailure.NonFiniteInput;
                return false;
            }

            var unclampedBase = current.Base + baseDelta;
            var unclampedCurrent = current.Current + currentDelta;
            if (!IsFinite(unclampedBase) || !IsFinite(unclampedCurrent))
            {
                failure = GasAttributeMutationFailure.NonFiniteResult;
                return false;
            }

            var appliedBase = Clamp(unclampedBase, in entry);
            var appliedCurrent = Clamp(unclampedCurrent, in entry);
            if (appliedBase == current.Base && appliedCurrent == current.Current)
                return true;
            if (current.Revision == uint.MaxValue)
            {
                failure = GasAttributeMutationFailure.RevisionOverflow;
                return false;
            }

            next.Base = appliedBase;
            next.Current = appliedCurrent;
            next.Revision++;
            return true;
        }

        /// <summary>
        /// 只读验证不携带 dirty 位图的内部 Attribute 变更。
        /// </summary>
        internal static bool TryValidateDelta(
            ref GasDefinitionCatalogBlob catalog,
            int attributeLayoutIndex,
            float baseDelta,
            float currentDelta,
            DynamicBuffer<AttributeValueSlot> attributes,
            out GasAttributeMutationRecord mutation,
            out GasAttributeMutationFailure failure)
        {
            return TryPrepareMutation(
                ref catalog,
                attributeLayoutIndex,
                baseDelta,
                currentDelta,
                attributes,
                default(DynamicBuffer<AttributeDirtyWord>),
                false,
                out mutation,
                out failure);
        }

        /// <summary>
        /// 保留无 dirty buffer 的内部调用形状，仅复用同一数值与 revision 校验核心。
        /// </summary>
        internal static bool TryApplyDelta(
            ref GasDefinitionCatalogBlob catalog,
            int attributeLayoutIndex,
            float baseDelta,
            float currentDelta,
            DynamicBuffer<AttributeValueSlot> attributes,
            out GasAttributeMutationRecord mutation,
            out GasAttributeMutationFailure failure)
        {
            return TryApplyDeltaCore(
                ref catalog,
                attributeLayoutIndex,
                baseDelta,
                currentDelta,
                attributes,
                default(DynamicBuffer<AttributeDirtyWord>),
                false,
                out mutation,
                out failure);
        }

        /// <summary>
        /// 执行 Attribute 事务的统一前置校验、计算与最终写入，失败时保持两个 buffer 不变。
        /// </summary>
        private static bool TryApplyDeltaCore(
            ref GasDefinitionCatalogBlob catalog,
            int attributeLayoutIndex,
            float baseDelta,
            float currentDelta,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<AttributeDirtyWord> dirtyWords,
            bool writeDirty,
            out GasAttributeMutationRecord mutation,
            out GasAttributeMutationFailure failure)
        {
            if (!TryPrepareMutation(
                    ref catalog,
                    attributeLayoutIndex,
                    baseDelta,
                    currentDelta,
                    attributes,
                    dirtyWords,
                    writeDirty,
                    out mutation,
                    out failure))
                return false;
            if (mutation.Changed == 0)
                return true;
            var current = attributes[attributeLayoutIndex];
            current.Base = mutation.AppliedBase;
            current.Current = mutation.AppliedCurrent;
            current.Revision = mutation.Revision;
            attributes[attributeLayoutIndex] = current;
            if (writeDirty)
                SetDirtyBit(dirtyWords, attributeLayoutIndex);
            return true;
        }

        /// <summary>
        /// 计算一次 Attribute mutation；该方法不写 buffer，保证调用方可先完成整批预检。
        /// </summary>
        private static bool TryPrepareMutation(
            ref GasDefinitionCatalogBlob catalog,
            int attributeLayoutIndex,
            float baseDelta,
            float currentDelta,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<AttributeDirtyWord> dirtyWords,
            bool writeDirty,
            out GasAttributeMutationRecord mutation,
            out GasAttributeMutationFailure failure)
        {
            mutation = new GasAttributeMutationRecord
            {
                AttributeLayoutIndex = attributeLayoutIndex,
                RequestedBaseDelta = baseDelta,
                RequestedCurrentDelta = currentDelta,
            };
            failure = ValidateShapeAndEntry(
                ref catalog, attributeLayoutIndex, attributes, dirtyWords, writeDirty,
                out var entry);
            if (failure != GasAttributeMutationFailure.None)
                return false;
            if (!IsFinite(baseDelta) || !IsFinite(currentDelta))
            {
                failure = GasAttributeMutationFailure.NonFiniteInput;
                return false;
            }

            var current = attributes[attributeLayoutIndex];
            mutation.PreviousBase = current.Base;
            mutation.PreviousCurrent = current.Current;
            mutation.PreviousRevision = current.Revision;
            if (!IsFinite(current.Base) || !IsFinite(current.Current))
            {
                failure = GasAttributeMutationFailure.NonFiniteInput;
                return false;
            }

            var unclampedBase = current.Base + baseDelta;
            var unclampedCurrent = current.Current + currentDelta;
            if (!IsFinite(unclampedBase) || !IsFinite(unclampedCurrent))
            {
                failure = GasAttributeMutationFailure.NonFiniteResult;
                return false;
            }
            var appliedBase = Clamp(unclampedBase, in entry);
            var appliedCurrent = Clamp(unclampedCurrent, in entry);
            mutation.UnclampedBase = unclampedBase;
            mutation.UnclampedCurrent = unclampedCurrent;
            mutation.AppliedBase = appliedBase;
            mutation.AppliedCurrent = appliedCurrent;
            mutation.Revision = current.Revision;
            if (appliedBase == current.Base && appliedCurrent == current.Current)
                return true;
            if (current.Revision == uint.MaxValue)
            {
                failure = GasAttributeMutationFailure.RevisionOverflow;
                return false;
            }
            mutation.Revision++;
            mutation.Changed = 1;
            return true;
        }

        /// <summary>
        /// 只验证 catalog 中一个 Attribute entry，供没有真实 DynamicBuffer 的 shadow 预演复用。
        /// </summary>
        private static GasAttributeMutationFailure ValidateCatalogEntry(
            ref GasDefinitionCatalogBlob catalog,
            int attributeLayoutIndex,
            out GasAttributeLayoutEntryBlob entry)
        {
            entry = default;
            var attributeCount = catalog.AttributeLayout.Entries.Length;
            if (attributeLayoutIndex < 0 || attributeLayoutIndex >= attributeCount)
                return GasAttributeMutationFailure.InvalidAttributeIndex;
            entry = catalog.AttributeLayout.Entries[attributeLayoutIndex];
            if (entry.LayoutIndex != attributeLayoutIndex ||
                entry.ClampMinimum > 1 || entry.ClampMaximum > 1 ||
                !IsFinite(entry.MinimumValue) || !IsFinite(entry.MaximumValue) ||
                entry.MinimumValue > entry.MaximumValue)
                return GasAttributeMutationFailure.InvalidCatalogEntry;
            return GasAttributeMutationFailure.None;
        }

        /// <summary>
        /// 验证 Attribute 固定长度、catalog dense index、clamp 元数据与 dirty 位图长度。
        /// </summary>
        private static GasAttributeMutationFailure ValidateShapeAndEntry(
            ref GasDefinitionCatalogBlob catalog,
            int attributeLayoutIndex,
            DynamicBuffer<AttributeValueSlot> attributes,
            DynamicBuffer<AttributeDirtyWord> dirtyWords,
            bool writeDirty,
            out GasAttributeLayoutEntryBlob entry)
        {
            entry = default;
            var attributeCount = catalog.AttributeLayout.Entries.Length;
            if (attributes.Length != attributeCount)
                return GasAttributeMutationFailure.InvalidBufferShape;
            if (attributeLayoutIndex < 0 || attributeLayoutIndex >= attributeCount)
                return GasAttributeMutationFailure.InvalidAttributeIndex;
            if (writeDirty && dirtyWords.Length != WordCount(attributeCount))
                return GasAttributeMutationFailure.InvalidBufferShape;
            return ValidateCatalogEntry(ref catalog, attributeLayoutIndex, out entry);
        }

        /// <summary>
        /// 按 Catalog 的开关对结果执行最小值与最大值裁剪。
        /// </summary>
        private static float Clamp(float value, in GasAttributeLayoutEntryBlob entry)
        {
            if (entry.ClampMinimum != 0 && value < entry.MinimumValue)
                value = entry.MinimumValue;
            if (entry.ClampMaximum != 0 && value > entry.MaximumValue)
                value = entry.MaximumValue;
            return value;
        }

        /// <summary>
        /// 设置 dense Attribute 对应的固定 dirty 位。
        /// </summary>
        private static void SetDirtyBit(
            DynamicBuffer<AttributeDirtyWord> dirtyWords,
            int attributeLayoutIndex)
        {
            var wordIndex = attributeLayoutIndex / 64;
            var word = dirtyWords[wordIndex];
            word.Value |= 1UL << (attributeLayoutIndex % 64);
            dirtyWords[wordIndex] = word;
        }

        /// <summary>
        /// 计算固定 64 位 dirty 位图长度。
        /// </summary>
        private static int WordCount(int count)
        {
            return count <= 0 ? 0 : ((count - 1) / 64) + 1;
        }

        /// <summary>
        /// 拒绝 NaN 与无穷值，避免数值污染 authority。
        /// </summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
