using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// GE Entity 上的 Modifier 配置 Buffer。
    /// 每个元素描述一个属性修改器。
    /// </summary>
    public struct BModifierConfig : IBufferElementData
    {
        public int AttrSetCode;
        public int AttributeCode;
        public EModifierOp Op;
        public float Magnitude;
    }
}
