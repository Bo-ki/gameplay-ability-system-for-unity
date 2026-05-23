namespace GAS.Runtime
{
    public struct AbilitySystemConfig
    {
        public AbilitySystemConfig(int[] baseTags, AttrSetConfig[] attrSets, int[] baseAbilityCodes, int level = 1)
        {
            BaseTags = baseTags;
            AttrSets = attrSets;
            BaseAbilityCodes = baseAbilityCodes;
            Level = level;
        }

        public int[] BaseTags { get; private set; }

        public AttrSetConfig[] AttrSets { get; private set; }

        public int[] BaseAbilityCodes { get; private set; }

        public int Level { get; private set; }

        public void SetBaseTags(int[] baseTags)
        {
            BaseTags = baseTags;
        }

        public void SetAttrSets(AttrSetConfig[] attrSets)
        {
            AttrSets = attrSets;
        }

        public void SetBaseAbilityCodes(int[] baseAbilityCodes)
        {
            BaseAbilityCodes = baseAbilityCodes;
        }

        public void SetLevel(int level)
        {
            Level = level;
        }
    }
}
