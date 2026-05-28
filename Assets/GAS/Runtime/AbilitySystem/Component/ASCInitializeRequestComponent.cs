using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// ASC 初始化请求。承载外部创建流程的初始等级，初始化明细通过同一请求实体上的 Buffer 提供。
    /// </summary>
    public struct ASCInitializeRequestComponent : IComponentData
    {
        public Entity ASC;
        public int Level;
    }

    [InternalBufferCapacity(4)]
    public struct ASCInitializeFixedTagBuffer : IBufferElementData
    {
        public int TagCode;
    }

    [InternalBufferCapacity(8)]
    public struct ASCInitializeAttributeBuffer : IBufferElementData
    {
        public int AttrSetCode;
        public int AttributeCode;
        public float BaseValue;
        public bool IsClampMin;
        public bool IsClampMax;
        public float MinValue;
        public float MaxValue;
    }

    [InternalBufferCapacity(4)]
    public struct ASCInitializeAbilityBuffer : IBufferElementData
    {
        public int AbilityCode;
    }
}
