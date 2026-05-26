# CASE-11: Burst FunctionPointer 用于动态算法选择 + 批处理粒度

**Primary Owner**: Burst-AOT
**来源**: Burst-AOT.md / DocCodeSamples
**关联规则**: BUR-02

## 使用场景
需要动态选择不同算法执行路径（如 ExecutionCalculation 的多种 MMC 类型），且同类型 entity 可批处理。

## 模式描述
使用 `BurstCompiler.CompileFunctionPointer<T>()` 注册静态方法，在 job 内以 FunctionPointer 调用。必须确保调用粒度是 chunk 级批处理，而非 per-entity invoke。

```csharp
// 注册
var fnPtr = BurstCompiler.CompileFunctionPointer<ExecutionCalculationFunction>(CalculateDamage);

// 在 job 中使用（chunk 级批处理）
[BurstCompile]
public struct ExecutionJob : IJobChunk
{
    public FunctionPointer<ExecutionCalculationFunction> Calculate;
    public void Execute(in ArchetypeChunk chunk, ...)
    {
        // 统一调用，不 per-entity invoke
        var resultCtx = new ExecutionContext { ... };
        Calculate.Invoke(ref resultCtx);
    }
}
```

## 注意事项
- Per-entity invoke 失去自动向量化机会
- 批处理 job 往往比 FunctionPointer 更简单更快
- 只适用于需要动态算法选择 + 批处理粒度足够大的场景

## EX-GAS 适用点
- ExecutionCalculation 的 MMC 计算（一类计算方式一个 FunctionPointer）
- Ability 的伤害计算公式选择
- Effect 的 Modifier 计算路由
