using Unity.Entities;

namespace GAS.Runtime
{
    public enum EMagnitudeSource : byte
    {
        Constant = 0,
        SetByCaller = 1,
        SourceAttribute = 2,
        TargetAttribute = 3,
        ExecutionCalculation = 4,
        StackCount = 5,
    }

    public enum EAttributeCaptureTiming : byte
    {
        Snapshot = 0,
        CurrentValue = 1,
    }

    public enum EExecutionCalculationInputSource : byte
    {
        Constant = 0,
        SetByCaller = 1,
        SourceAttribute = 2,
        TargetAttribute = 3,
        SpecLevel = 4,
        StackCount = 5,
    }

    /// <summary>
    /// GE Modifier 的 Magnitude 定义。用于把 SetByCaller / Attribute Capture / Execution output / StackCount 接入 ECS resolver。
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct GEMagnitudeDefinitionBuffer : IBufferElementData, IEnableableComponent
    {
        public int ModifierIndex;
        public EMagnitudeSource Source;
        public int AttributeSetCode;
        public int AttributeCode;
        public int Key;
        public EAttributeCaptureTiming CaptureTiming;
        public float FallbackMagnitude;
        public float Coefficient;
        public float PreAdd;
        public float PostAdd;
    }

    /// <summary>
    /// Snapshot Attribute Capture 的运行时缓存。第一次解析后固定，后续 StackCount 等重算不会重新捕获属性。
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct GEAttributeCaptureValueBuffer : IBufferElementData
    {
        public int ModifierIndex;
        public EMagnitudeSource Source;
        public int AttributeSetCode;
        public int AttributeCode;
        public float Value;
    }

    /// <summary>
    /// ExecutionCalculation 的 ECS 输出值。后续专用 execution system 写入，resolver 只按 Key 消费。
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct GEExecutionCalculationValueBuffer : IBufferElementData
    {
        public int Key;
        public float Value;
    }

    /// <summary>
    /// ExecutionCalculation 的数据化输出定义。无 InputDefinition 时兼容单输入线性计算。
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct GEExecutionCalculationDefinitionBuffer : IBufferElementData, IEnableableComponent
    {
        public int CalculationCode;
        public int OutputKey;
        public EExecutionCalculationInputSource Source;
        public int AttributeSetCode;
        public int AttributeCode;
        public int Key;
        public EAttributeCaptureTiming CaptureTiming;
        public float ConstantValue;
        public float FallbackValue;
        public float Coefficient;
        public float PreAdd;
        public float PostAdd;
    }

    /// <summary>
    /// ExecutionCalculation 的多输入定义。多个输入按同一 CalculationCode/OutputKey 聚合后写入输出值。
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct GEExecutionCalculationInputDefinitionBuffer : IBufferElementData, IEnableableComponent
    {
        public int CalculationCode;
        public int OutputKey;
        public int InputIndex;
        public EExecutionCalculationInputSource Source;
        public int AttributeSetCode;
        public int AttributeCode;
        public int Key;
        public EAttributeCaptureTiming CaptureTiming;
        public float ConstantValue;
        public float FallbackValue;
        public float Coefficient;
        public float PreAdd;
        public float PostAdd;
    }

    /// <summary>
    /// ExecutionCalculation 的输出 Modifier 定义。把输出值接入标准 GE Modifier 管线，不直接写 Attribute。
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct GEExecutionCalculationOutputModifierDefinitionBuffer : IBufferElementData, IEnableableComponent
    {
        public int CalculationCode;
        public int OutputKey;
        public int AttrSetCode;
        public int AttributeCode;
        public EModifierOp Op;
        public float FallbackMagnitude;
        public float Coefficient;
        public float PreAdd;
        public float PostAdd;
    }

    /// <summary>
    /// Marks a transient GE whose ExecutionCalculation outputs have already been converted to attribute deltas.
    /// </summary>
    public struct GEExecutionCalculationOutputModifierAppliedComponent : IComponentData, IEnableableComponent
    {
        public int Frame;
        public int DeltaCount;
    }
}
