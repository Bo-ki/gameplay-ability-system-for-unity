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
        public const int Count = 6;

        public static bool TryGetAt(int index, out GASGeneratedDefinitionIndexEntry entry)
        {
            switch (index)
            {
                case 0:
                    entry = new GASGeneratedDefinitionIndexEntry(0, GASDefinitionKind.Ability, -7087823, 1943544664, -1181086457, 8, 10);
                    return true;
                case 1:
                    entry = new GASGeneratedDefinitionIndexEntry(1, GASDefinitionKind.Attribute, 1660590223, -898914946, -626649207, 13, 7);
                    return true;
                case 2:
                    entry = new GASGeneratedDefinitionIndexEntry(2, GASDefinitionKind.AttributeSet, -677498379, 22454532, 1412038275, 3, 1);
                    return true;
                case 3:
                    entry = new GASGeneratedDefinitionIndexEntry(3, GASDefinitionKind.GameplayCue, 2027271664, 1809634853, 1972995510, 4, 2);
                    return true;
                case 4:
                    entry = new GASGeneratedDefinitionIndexEntry(4, GASDefinitionKind.GameplayEffect, -917533084, 2050744081, -479655726, 15, 33);
                    return true;
                case 5:
                    entry = new GASGeneratedDefinitionIndexEntry(5, GASDefinitionKind.GameplayTag, -1940843005, -174959718, 1944373125, 33, 3);
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
                    entry = new GASGeneratedDefinitionIndexEntry(0, GASDefinitionKind.Ability, -7087823, 1943544664, -1181086457, 8, 10);
                    return true;
                case GASDefinitionKind.Attribute:
                    entry = new GASGeneratedDefinitionIndexEntry(1, GASDefinitionKind.Attribute, 1660590223, -898914946, -626649207, 13, 7);
                    return true;
                case GASDefinitionKind.AttributeSet:
                    entry = new GASGeneratedDefinitionIndexEntry(2, GASDefinitionKind.AttributeSet, -677498379, 22454532, 1412038275, 3, 1);
                    return true;
                case GASDefinitionKind.GameplayCue:
                    entry = new GASGeneratedDefinitionIndexEntry(3, GASDefinitionKind.GameplayCue, 2027271664, 1809634853, 1972995510, 4, 2);
                    return true;
                case GASDefinitionKind.GameplayEffect:
                    entry = new GASGeneratedDefinitionIndexEntry(4, GASDefinitionKind.GameplayEffect, -917533084, 2050744081, -479655726, 15, 33);
                    return true;
                case GASDefinitionKind.GameplayTag:
                    entry = new GASGeneratedDefinitionIndexEntry(5, GASDefinitionKind.GameplayTag, -1940843005, -174959718, 1944373125, 33, 3);
                    return true;
                default:
                    entry = default;
                    return false;
            }
        }
    }
}
