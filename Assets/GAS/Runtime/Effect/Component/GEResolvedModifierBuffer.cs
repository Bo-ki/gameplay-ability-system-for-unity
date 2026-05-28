using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// GE instance 上已经解析完成的 Modifier。Attribute 热路径只消费解析结果，不回查配置和上下文。
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct GEResolvedModifierBuffer : IBufferElementData
    {
        public int AttrSetCode;
        public int AttributeCode;
        public EModifierOp Op;
        public float Magnitude;
        public Entity SourceEffect;
    }
}
