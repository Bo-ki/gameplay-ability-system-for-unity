using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// ASC 边界命令类型。只用于低频 Request Entity，不进入高频帧内 fan-in record。
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
    /// ASC 边界命令请求。系统消费后应销毁请求实体，避免跨帧堆积。
    /// </summary>
    public struct ASCCommandRequestComponent : IComponentData
    {
        public Entity ASC;
        public ASCCommandType CommandType;

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
