# CASE-11：Burst FunctionPointer 用于动态算法选择与批处理

**Primary Owner**：Burst-AOT
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Burst `1.8.29`
**官方来源**：Burst `csharp-function-pointers.md`
**关联规则**：BUR-02、BUR-05

## 使用场景
算法必须在运行时选择、不能用数据分组或简单 `switch` 清晰表达，并且同一算法能一次处理一段连续数据。

## 模式描述
`FunctionPointer<T>` 的 `T` 是非泛型 delegate；目标是带 `[BurstCompile]` 的静态方法。注册发生在托管初始化边界，job 中保存编译后的 FunctionPointer，并按批次调用。

```csharp
using AOT;
using Unity.Burst;

/// <summary>
/// 定义批量计算函数的非托管签名。
/// </summary>
public unsafe delegate void ExecutionCalculationDelegate(float* values, int count);

/// <summary>
/// 提供可动态选择的 Burst 批量算法。
/// </summary>
[BurstCompile]
public static class ExecutionCalculations
{
    /// <summary>
    /// 批量处理连续数据。
    /// </summary>
    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExecutionCalculationDelegate))]
    public static unsafe void CalculateDamage(float* values, int count)
    {
        for (int i = 0; i < count; i++)
        {
            values[i] *= 2f;
        }
    }
}

/// <summary>
/// 在托管初始化边界创建并缓存函数指针。
/// </summary>
public static class ExecutionCalculationBootstrap
{
    /// <summary>
    /// 编译并返回批量计算函数指针。
    /// </summary>
    public static FunctionPointer<ExecutionCalculationDelegate> Create()
    {
        return BurstCompiler.CompileFunctionPointer<ExecutionCalculationDelegate>(
            ExecutionCalculations.CalculateDamage);
    }
}
```

完整的 `IJobParallelForBatch` 调用示例见同目录 `API与EX-GAS解读.md`。

## 注意事项
- 先比较直接 job、按算法分组 job 和 `switch`；FunctionPointer 不是唯一动态分派方式。
- 禁止在每帧或逐 entity 路径重复 `CompileFunctionPointer`。
- 逐元素调用不一定错误，但通常损失批处理、向量化和别名分析机会；Runtime Core 采用前必须有目标 Player 基准。
- delegate 不得是泛型 delegate；目标 Player/IL2CPP 必须验证。

## EX-GAS 适用点
ExecutionCalculation 只有在算法类型需运行时选择且能按类型成批处理时才采用；Ability/Effect 的小型公式优先使用数据驱动分支或独立 job。
