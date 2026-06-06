using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 扁平属性 Buffer 元素。
    /// 每个 ASC Entity 持有此 Buffer，每个元素代表一个属性值。
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct AttributeValueBuffer : IBufferElementData
    {
        public int AttrSetCode;
        public int Code;
        public float BaseValue;
        public float CurrentValue;
        public float PreviousCurrentValue;
        public bool IsClampMin;
        public bool IsClampMax;
        public float MinValue;
        public float MaxValue;
        public bool Dirty;
        public bool CurrentValueChangePending;
    }

    public enum EAttributeOwnerMarkerRequestKind : byte
    {
        MarkDirty = 1,
        SetActiveModifierPresent = 2,
    }

    [InternalBufferCapacity(0)]
    public struct AttributeOwnerMarkerRequestBuffer : IBufferElementData
    {
        public int Sequence;
        public Entity ASC;
        public EAttributeOwnerMarkerRequestKind RequestKind;
        public byte Value;
    }
}
