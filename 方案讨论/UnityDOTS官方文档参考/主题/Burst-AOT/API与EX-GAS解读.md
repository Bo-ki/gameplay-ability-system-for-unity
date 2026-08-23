# Burst-AOT：API 与 EX-GAS 解读

**适用版本**：Unity `6000.3.14f1`；Burst `1.8.29`

## 结论

- Editor Play Mode 中 Burst 使用 JIT 编译；Player 中 Burst 支持的入口由构建流程 AOT 编译为原生库，桌面 Player 也不是“默认 JIT”。
- `FunctionPointer<T>` 的 `T` 必须是 delegate 类型。它不是动态算法选择的唯一方案；数据分组、`switch` 和不同 job 往往更简单、更快。
- job struct 与其中的 NativeContainer 字段通常已由 Burst/Job System 推断 no-alias 信息。`[NoAlias]` 只用于编译器无法推断且调用方确实满足契约的复杂指针或 struct；错误标注可能产生未定义行为。
- `[BurstCompile]` 不是“尽力解释执行”的开关。Burst 不支持的代码应以编译诊断、Burst Inspector 和目标 Player 构建结果处理，不能假设会静默降级为解释模式。

## Burst 编译边界

Burst 编译 HPC# 子集。常见入口包括 job、unmanaged `ISystem` 回调，以及通过 `BurstCompiler.CompileFunctionPointer<T>()` 编译的静态方法。Burst 编译后的代码不能直接操作托管对象；`SystemBase` 自身的回调在托管侧运行，但它调度的 job 仍可由 Burst 编译。

Editor 默认可异步编译 Burst 入口：等待编译期间，入口可能暂时运行托管的 .NET JIT 版本。Player 构建则把支持的 Burst 入口 AOT 编译并随应用发布。

## FunctionPointer 的正确类型和批处理方式

下面示例展示 FunctionPointer 的关键编译边界。delegate 是编译期签名；Burst job 中保存和调用的是 `FunctionPointer<ExecutionCalculationDelegate>`，不是托管 delegate 实例。

```csharp
using System.Runtime.InteropServices;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

/// <summary>
/// 定义批量数值计算的非托管函数签名。
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void ExecutionCalculationDelegate(float* values, int count);

/// <summary>
/// 提供可由 Burst 编译并通过函数指针调用的静态算法。
/// </summary>
[BurstCompile]
public static class ExecutionCalculationFunctions
{
    /// <summary>
    /// 对一段连续数据执行批量计算。
    /// </summary>
    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExecutionCalculationDelegate))]
    public static unsafe void Scale(float* values, int count)
    {
        for (int i = 0; i < count; i++)
        {
            values[i] *= 2f;
        }
    }
}

/// <summary>
/// 以批次为粒度调用动态选择的 Burst 算法。
/// </summary>
[BurstCompile]
public unsafe struct ExecutionBatchJob : IJobParallelForBatch
{
    public NativeArray<float> Values;
    public FunctionPointer<ExecutionCalculationDelegate> Calculate;

    /// <summary>
    /// 将当前批次的连续内存交给函数指针处理。
    /// </summary>
    public void Execute(int startIndex, int count)
    {
        float* values = (float*)Values.GetUnsafePtr() + startIndex;
        Calculate.Invoke(values, count);
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
            ExecutionCalculationFunctions.Scale);
    }
}
```

官方要求包含：静态方法和包含类型标注 `[BurstCompile]`、用 delegate 声明签名，以及 IL2CPP 回调所需的 `MonoPInvokeCallback`。`CompileFunctionPointer` 会为 delegate 补充 Cdecl 互操作属性；示例显式标出该约束以便审查。

FunctionPointer 有间接调用、P/Invoke 边界和较弱别名分析成本。不要逐 entity 调用很小的函数；先尝试数据驱动 `switch`、按算法分组后分别调度 job，确需运行时函数指针时再批处理调用，并用 Profiler/Burst Inspector 验证。

## Player AOT 与性能报告

Burst 1.8.29 的 `Optimize For` 包括 `Balanced`（默认）、`Performance`、`Size` 和 `Fast Compilation`。Player 性能报告至少记录：

- Unity、Entities、Burst 的精确版本；
- 目标平台、构建后端、Development/Release、CPU 架构；
- Burst AOT 是否启用、`Optimize For`、优化和安全检查相关配置；
- 采样场景、实体规模、采样帧范围，以及为排除场景加载、缓存填充等一次性成本而丢弃的预热帧。

Player 中不存在“首帧 Burst JIT warmup”。预热帧仍可用于排除场景初始化、资源加载和缓存冷启动，但必须称为工作负载预热，而不是 Burst JIT。Editor 若要控制首次 Burst 编译时机，可对确有必要的入口使用同步编译配置；Burst 1.8.29 没有通用 `Warmup` API。

## `[NoAlias]` 的安全边界

大多数 job/NativeContainer 场景不需要手写 `[NoAlias]`。只有同时满足以下条件才评估使用：

1. Burst Inspector 显示别名阻止了目标优化；
2. Job System/NativeContainer 规则没有自动提供等价信息；
3. 能证明相关指针、引用或 struct 在所有调用路径上绝不别名；
4. 有覆盖别名契约的测试和目标平台性能数据。

不能把 `[NoAlias]` 当作普通“优化提示”。它是正确性契约；若实际内存存在别名，Burst 可基于错误前提重排读写，结果属于未定义行为。

## EX-GAS 项目策略

- Runtime Core 的热路径优先保持 unmanaged、可 Burst 编译；托管边界放在初始化、配置加载和 Presentation。
- ExecutionCalculation 默认使用数据分组或明确 `switch`；FunctionPointer 仅用于算法需运行时选择、同算法数据可成批处理且基准证明有价值的路径。
- 每个 FunctionPointer 在初始化阶段注册并缓存，禁止每帧或逐 entity 调用 `CompileFunctionPointer`。
- 性能结论必须来自目标 Player，不用 Editor 结果替代 AOT 构建验证。

## 官方证据

| 官方文档（Burst 1.8.29） | 可裁决结论 |
|---|---|
| `getting-started.md` | Editor Play Mode 使用 JIT；Player 使用 AOT |
| `compilation-synchronous.md` | Editor 可异步或同步编译；Player 构建 AOT 编译支持的代码 |
| `building-aot-settings.md` | Player AOT 设置、目标架构和 `Optimize For` 选项 |
| `csharp-function-pointers.md` | `T` 是 delegate；静态方法/包含类型标注；IL2CPP 回调约束；job 和批处理通常优先 |
| `aliasing-job-system.md` | job 和 NativeContainer 常见场景会自动获得 no-alias 信息 |
| `aliasing-noalias.md` | 多数场景无需 `[NoAlias]`；错误标注可能导致未定义行为 |
