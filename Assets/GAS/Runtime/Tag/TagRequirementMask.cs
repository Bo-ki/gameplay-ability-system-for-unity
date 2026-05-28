namespace GAS.Runtime
{
    public struct TagRequirementMask
    {
        public TagMaskComponent All;
        public TagMaskComponent Any;
        public TagMaskComponent None;

        public readonly bool IsEmpty => All.IsEmpty && Any.IsEmpty && None.IsEmpty;

        public readonly bool Evaluate(in TagMaskComponent owner)
        {
            if (!owner.HasAllTags(All))
                return false;

            if (!Any.IsEmpty && !owner.HasAnyTag(Any))
                return false;

            if (!None.IsEmpty && owner.HasAnyTag(None))
                return false;

            return true;
        }
    }
}
