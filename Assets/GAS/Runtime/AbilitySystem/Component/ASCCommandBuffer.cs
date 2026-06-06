using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// ASC owner-local 命令类型。边界入口只追加 ASCCommandBuffer，不创建 request entity。
    /// </summary>
    public enum ASCCommandType : byte
    {
        SetLevel,
        AddFixedTag,
        RemoveFixedTag,
        SetAttributeBaseValue,
        AddAttribute,
    }

    /// <summary>
    /// ASC owner-local 命令 payload。Transient command 由 ASCCommandBuffer 承载。
    /// </summary>
    public struct ASCCommand
    {
        public ASCCommandType CommandType;

        public int Level;
        public int TagSourceIndex;
        public TagMaskComponent TagMask;

        public int AttrSetCode;
        public int AttributeCode;
        public float AttributeValue;
        public bool IsClampMin;
        public bool IsClampMax;
        public float MinValue;
        public float MaxValue;
    }

    [InternalBufferCapacity(4)]
    public struct ASCCommandBuffer : IBufferElementData
    {
        public ASCCommand Command;
    }
}
