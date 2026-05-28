using Unity.Entities;

namespace GAS.Runtime
{
    public enum EModifierOp : byte
    {
        Add = 0,
        Subtract = 3,
        Multiply = 1,
        Divide = 4,
        Override = 2,
    }

    /// <summary>
    /// ASC Entity 上的活跃 Modifier Buffer，由 GE 应用时写入、GE 过期时移除。
    /// 平坦化存储，无托管对象嵌套。
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct AttributeActiveModifierBuffer : IBufferElementData
    {
        public int AttrSetCode;
        public int AttributeCode;
        public Entity SourceEntity;
        public int SourceSequence;
        public int SourceGameplayEffectCode;
        public float Magnitude;
        public EModifierOp Op;
    }
}
