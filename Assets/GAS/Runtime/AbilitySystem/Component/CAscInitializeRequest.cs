using Unity.Entities;

namespace GAS.Runtime
{
    public struct CAscInitializeRequest : IComponentData
    {
        public Entity ASC;
        public int Level;
    }

    public struct BAscInitFixedTag : IBufferElementData
    {
        public int TagCode;
    }

    public struct BAscInitAttribute : IBufferElementData
    {
        public int AttrSetCode;
        public int AttributeCode;
        public float BaseValue;
        public bool IsClampMin;
        public bool IsClampMax;
        public float MinValue;
        public float MaxValue;
    }

    public struct BAscInitAbility : IBufferElementData
    {
        public int AbilityCode;
    }
}
