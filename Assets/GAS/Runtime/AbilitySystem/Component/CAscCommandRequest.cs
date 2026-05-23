using Unity.Entities;

namespace GAS.Runtime
{
    public enum EAscCommandType : byte
    {
        SetLevel,
        AddFixedTag,
        RemoveFixedTag,
        SetAttributeBaseValue,
        AddAttribute,
    }

    public struct CAscCommandRequest : IComponentData
    {
        public Entity ASC;
        public EAscCommandType CommandType;

        public int Level;
        public int TagCode;

        public int AttrSetCode;
        public int AttributeCode;
        public float AttributeValue;
        public bool IsClampMin;
        public bool IsClampMax;
        public float MinValue;
        public float MaxValue;
    }
}
