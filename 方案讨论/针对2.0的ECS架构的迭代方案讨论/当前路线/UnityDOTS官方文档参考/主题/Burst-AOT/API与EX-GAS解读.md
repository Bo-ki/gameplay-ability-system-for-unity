# Burst-AOT: API 与 EX-GAS 解读

## 核心概念

### Burst 编译器原理

Burst 是 LLVM-based 的 C# 子集编译器，将 HPC#（High Performance C#）代码编译为高度优化的原生代码。

**Burst 能编译什么：**
- `struct` 类型的 job（`IJobEntity`、`IJobChunk`、`IJob`）
- unmanaged `ISystem` 的 `OnCreate`/`OnUpdate`/`OnDestroy`
- 使用 `FunctionPointer<T>` 的特定静态方法

**Burst 不能编译什么：**
- 任何引用类型（class、delegate、managed array）
- 托管对象访问（`GameObject`、`MonoBehaviour`）
- `SystemBase` 的回调方法
- 虚方法调用

### FunctionPointer

`FunctionPointer<T>` 是 Burst 中实现动态算法选择的唯一方式。

```csharp
// 注册
var fnPtr = BurstCompiler.CompileFunctionPointer<ExecutionCalculationFunction>(CalculateDamage);

// 在 job 中使用
[BurstCompile]
public struct ExecutionJob : IJobEntity
{
    public FunctionPointer<ExecutionCalculationFunction> Calculate;
    public void Execute(ref BAttribute attr, in CEffectContext ctx)
    {
        var resultCtx = new ExecutionContext { ... };
        attr.CurrentValue += Calculate.Invoke(ref resultCtx);
    }
}
```

**FunctionPointer 的代价：**
- 标量调用（per-entity invoke）可能失去自动向量化
- 批处理 job 往往比 FunctionPointer 更简单更快
- 只适用于需要动态算法选择 + 批处理粒度足够大的场景

### Burst AOT 与 Player

Player build 的 Burst 设置独立于 Editor：

| 设置 | 说明 |
|---|---|
| `OptimizeFor` | `Performance`（默认）/ `Size` / `FastCompilation` |
| `Safety Checks` | Editor 可开启；Player 应关闭（性能） |
| AOT compilation | iOS/consoles 需要 AOT；desktop 默认 JIT |

**Player 性能报告必须记录：**
- Burst AOT 是否启用
- `OptimizeFor` 设置
- Safety Checks 状态
- CPU 架构（x64/ARM64）
- Burst warmup 帧数

### 向量化与别名分析

Burst 的自动向量化需要编译器能证明代码无别名冲突。使用 `[NoAlias]` 标记无别名关系，帮助编译器向量化：

```csharp
[BurstCompile]
public struct VectorizedJob : IJobEntity
{
    [NoAlias] public NativeArray<float> Results;
    public void Execute([NoAlias] ref BAttribute attr, [NoAlias] in CModifier mod)
    {
        attr.CurrentValue += mod.Value;  // Burst 能向量化此循环
    }
}
```

---

## EX-GAS 项目解读

### 目标态 Ability/Effect 逻辑的 Burst 约束

| 旧模式 | 目标态替代 |
|---|---|
| `AbilityLogicBase` 虚方法 | 数据驱动规则 + Blob config + job 批处理 |
| `MMC` 虚方法计算 | generated static function table + FunctionPointer（批处理粒度） |
| managed delegate 回调 | ECS command/event stream |
| `Activator.CreateInstance` 动态创建 | config-driven + Blob definition |

### FunctionPointer 在 ExecutionCalculation 的定位

```
不要：每个 Ability 一个 FunctionPointer，per-entity invoke
应该：一类计算方式一个 FunctionPointer，同类的 entity chunk 批处理
```

### Player 性能报告的 Burst 上下文

Player 性能报告必须自动附加 Burst 上下文：
- Burst AOT enabled
- OptimizeFor = Performance
- Safety Checks = Disabled
- CPU arch: x64 / ARM64
- Warmup frames: N frames excluded

---

## 常见陷阱

1. **FunctionPointer 的 per-entity invoke**：和虚方法调用一样糟，会丢失向量化
2. **Editor Burst 通过不等于 Player Burst 通过**：AOT 平台的限制更多
3. **`[BurstCompile]` 标记了但内部调用托管方法**：编译静默降级为解释模式
4. **Burst warmup 误解**：首帧执行时 JIT 编译，成本极高。延迟初始化或用 `Warmup` API 控制时机
5. **把 FunctionPointer 当 OOP 虚方法链替代品**：逐 entity invoke 失去批处理意义
6. **Managed component 替代 Blob/struct config**：Burst 无法编译托管对象
7. **用 Burst 通过率证明架构正确**：Burst 不覆盖 query/allocator/structure 问题

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则编号 |
|---|---|---|
| `burst/building-aot-settings.md` | Player/AOT 有独立 Burst 设置；OptimizeFor/Safety Checks 差异 | BUR-03, BUR-05 |
| `burst/csharp-function-pointers.md` | FunctionPointer 标量调用可能失去向量化；批处理 job 优先 | BUR-02 |
| `burst/optimization-loop-vectorization.md` | loop vectorization 需要可分析循环和明确数据布局 | BUR-04 |
| `burst/aliasing.md` | aliasing 影响 Burst 优化和正确性；[NoAlias] 辅助 | BUR-04 |
| DocCodeSamples (CASE-11) | FunctionPointer + BurstCompiler.CompileFunctionPointer 动态算法选择 | BUR-02 |
