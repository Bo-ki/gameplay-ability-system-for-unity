///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;

namespace GAS.Runtime.Generated
{
    public readonly struct GASGeneratedDefinitionIndexEntry
    {
        public readonly int Index;
        public readonly GASDefinitionKind DefinitionKind;
        public readonly int DomainHash;
        public readonly int CodeFieldHash;
        public readonly int BlobSchemaHash;
        public readonly int SourceRowCount;
        public readonly int BlobMemberCount;

        public GASGeneratedDefinitionIndexEntry(
            int index,
            GASDefinitionKind definitionKind,
            int domainHash,
            int codeFieldHash,
            int blobSchemaHash,
            int sourceRowCount,
            int blobMemberCount)
        {
            Index = index;
            DefinitionKind = definitionKind;
            DomainHash = domainHash;
            CodeFieldHash = codeFieldHash;
            BlobSchemaHash = blobSchemaHash;
            SourceRowCount = sourceRowCount;
            BlobMemberCount = blobMemberCount;
        }
    }

    public static class GASGeneratedDefinitionIndex
    {
        public const int Count = 10;

        public static bool TryGetAt(int index, out GASGeneratedDefinitionIndexEntry entry)
        {
            switch (index)
            {
                case 0:
                    entry = new GASGeneratedDefinitionIndexEntry(0, GASDefinitionKind.Ability, -7087823, 1943544664, -1181086457, 7, 7);
                    return true;
                case 1:
                    entry = new GASGeneratedDefinitionIndexEntry(1, GASDefinitionKind.Attribute, 1660590223, -898914946, -626649207, 6, 7);
                    return true;
                case 2:
                    entry = new GASGeneratedDefinitionIndexEntry(2, GASDefinitionKind.AttributeSet, -677498379, 22454532, 1412038275, 1, 1);
                    return true;
                case 3:
                    entry = new GASGeneratedDefinitionIndexEntry(3, GASDefinitionKind.GameplayCue, 2027271664, 1809634853, 1972995510, 1, 2);
                    return true;
                case 4:
                    entry = new GASGeneratedDefinitionIndexEntry(4, GASDefinitionKind.GameplayEffect, -917533084, 2050744081, -479655726, 33, 27);
                    return true;
                case 5:
                    entry = new GASGeneratedDefinitionIndexEntry(5, GASDefinitionKind.GameplayTag, -1940843005, -174959718, 1944373125, 19, 3);
                    return true;
                case 6:
                    entry = new GASGeneratedDefinitionIndexEntry(6, GASDefinitionKind.None, -1178083798, 137200775, 10808612, 4, 7);
                    return true;
                case 7:
                    entry = new GASGeneratedDefinitionIndexEntry(7, GASDefinitionKind.None, 1188892602, 878040070, -779313708, 1, 16);
                    return true;
                case 8:
                    entry = new GASGeneratedDefinitionIndexEntry(8, GASDefinitionKind.TimelineAbility, 819430390, 984365931, 1285909208, 7, 5);
                    return true;
                case 9:
                    entry = new GASGeneratedDefinitionIndexEntry(9, GASDefinitionKind.None, 1759602215, 1863546534, 1386546657, 5, 20);
                    return true;
                default:
                    entry = default;
                    return false;
            }
        }

        public static bool TryGetByDefinitionKind(GASDefinitionKind definitionKind, out GASGeneratedDefinitionIndexEntry entry)
        {
            switch (definitionKind)
            {
                case GASDefinitionKind.Ability:
                    entry = new GASGeneratedDefinitionIndexEntry(0, GASDefinitionKind.Ability, -7087823, 1943544664, -1181086457, 7, 7);
                    return true;
                case GASDefinitionKind.Attribute:
                    entry = new GASGeneratedDefinitionIndexEntry(1, GASDefinitionKind.Attribute, 1660590223, -898914946, -626649207, 6, 7);
                    return true;
                case GASDefinitionKind.AttributeSet:
                    entry = new GASGeneratedDefinitionIndexEntry(2, GASDefinitionKind.AttributeSet, -677498379, 22454532, 1412038275, 1, 1);
                    return true;
                case GASDefinitionKind.GameplayCue:
                    entry = new GASGeneratedDefinitionIndexEntry(3, GASDefinitionKind.GameplayCue, 2027271664, 1809634853, 1972995510, 1, 2);
                    return true;
                case GASDefinitionKind.GameplayEffect:
                    entry = new GASGeneratedDefinitionIndexEntry(4, GASDefinitionKind.GameplayEffect, -917533084, 2050744081, -479655726, 33, 27);
                    return true;
                case GASDefinitionKind.GameplayTag:
                    entry = new GASGeneratedDefinitionIndexEntry(5, GASDefinitionKind.GameplayTag, -1940843005, -174959718, 1944373125, 19, 3);
                    return true;
                case GASDefinitionKind.None:
                    entry = new GASGeneratedDefinitionIndexEntry(6, GASDefinitionKind.None, -1178083798, 137200775, 10808612, 4, 7);
                    return true;
                case GASDefinitionKind.TimelineAbility:
                    entry = new GASGeneratedDefinitionIndexEntry(8, GASDefinitionKind.TimelineAbility, 819430390, 984365931, 1285909208, 7, 5);
                    return true;
                default:
                    entry = default;
                    return false;
            }
        }
    }
}
