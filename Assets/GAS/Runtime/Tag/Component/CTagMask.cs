using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 4×ulong Bitmask Tag 组件，存储由 TagHelper 压缩后的 0..255 DenseIndex。
    /// </summary>
    public struct CTagMask : IComponentData
    {
        public const int Capacity = 256;

        public ulong Mask0;
        public ulong Mask1;
        public ulong Mask2;
        public ulong Mask3;

        public readonly bool IsEmpty => Mask0 == 0 && Mask1 == 0 && Mask2 == 0 && Mask3 == 0;

        public void Clear()
        {
            Mask0 = 0;
            Mask1 = 0;
            Mask2 = 0;
            Mask3 = 0;
        }

        public readonly bool HasAnyTag(in CTagMask other)
        {
            return (Mask0 & other.Mask0) != 0
                || (Mask1 & other.Mask1) != 0
                || (Mask2 & other.Mask2) != 0
                || (Mask3 & other.Mask3) != 0;
        }

        public readonly bool HasAllTags(in CTagMask other)
        {
            return (Mask0 & other.Mask0) == other.Mask0
                && (Mask1 & other.Mask1) == other.Mask1
                && (Mask2 & other.Mask2) == other.Mask2
                && (Mask3 & other.Mask3) == other.Mask3;
        }

        public static bool IsValidIndex(int index)
        {
            return (uint)index < 256u;
        }

        public readonly ulong GetBlock(int index) => index switch
        {
            < 64 => Mask0,
            < 128 => Mask1,
            < 192 => Mask2,
            < 256 => Mask3,
            _ => 0,
        };

        private ulong GetBlockRef(int index)
        {
            if (index < 64) return Mask0;
            if (index < 128) return Mask1;
            if (index < 192) return Mask2;
            if (index < 256) return Mask3;
            return 0;
        }

        private void SetBlockRef(int index, ulong value)
        {
            if (index < 64) Mask0 = value;
            else if (index < 128) Mask1 = value;
            else if (index < 192) Mask2 = value;
            else if (index < 256) Mask3 = value;
        }

        public readonly bool HasTag(int index)
        {
            if (!IsValidIndex(index)) return false;
            return (GetBlock(index) & (1ul << (index & 63))) != 0;
        }

        public void AddTag(int index)
        {
            if (!IsValidIndex(index)) return;
            SetBlockRef(index, GetBlockRef(index) | (1ul << (index & 63)));
        }

        public void RemoveTag(int index)
        {
            if (!IsValidIndex(index)) return;
            SetBlockRef(index, GetBlockRef(index) & ~(1ul << (index & 63)));
        }
    }
}
