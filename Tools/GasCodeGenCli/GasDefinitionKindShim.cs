namespace GAS.Runtime
{
    public enum GASDefinitionKind
    {
        None = 0,
        Ability = 1,
        GameplayEffect = 2,
        AttributeSet = 3,
        Attribute = 4,
        GameplayTag = 5,
        GameplayCue = 6,
    }

    public static class GASRequirementKind
    {
        public const int None = 0;
        public const int RequiredTags = 1;
        public const int BlockedTags = 2;
        public const int AttributeCompare = 3;
    }

    public struct TagRequirementMask
    {
        public TagMaskComponent All;
        public TagMaskComponent Any;
        public TagMaskComponent None;

        public readonly bool IsEmpty => All.IsEmpty && Any.IsEmpty && None.IsEmpty;
    }

    public struct TagMaskComponent
    {
        public const int Capacity = 256;

        public ulong Mask0;
        public ulong Mask1;
        public ulong Mask2;
        public ulong Mask3;

        public readonly bool IsEmpty => Mask0 == 0 && Mask1 == 0 && Mask2 == 0 && Mask3 == 0;

        public void AddTag(int index)
        {
            if ((uint)index >= Capacity)
                return;

            var bit = 1ul << (index & 63);
            if (index < 64)
                Mask0 |= bit;
            else if (index < 128)
                Mask1 |= bit;
            else if (index < 192)
                Mask2 |= bit;
            else
                Mask3 |= bit;
        }
    }
}
