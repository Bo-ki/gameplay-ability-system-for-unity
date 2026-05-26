# Burst-AOT

## 职责

维护 Burst 编译器在 EX-GAS Runtime Core 热路径、Player 验收、FunctionPointer、向量化加速和 AOT 配置中的使用规则。覆盖 `[BurstCompile]` 标记规范、FunctionPointer 的合理使用边界、Player 与 Editor 的 Burst 差异、AOT 编译配置、以及 `[NoAlias]` 向量化辅助。不覆盖 IL2CPP 配置或非 ECS 相关的 burst 使用。

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

## 编写规范

### BUR-01: Hot path system/job 必须标注 `[BurstCompile]` 且无托管依赖

**声明：** Runtime Core 中所有每帧执行的 system 和 job 必须标注 `[BurstCompile]`，其内部执行的代码必须满足 Burst 编译约束（无引用类型、无虚方法调用、无托管对象访问）。

**来源：** 09-Burst-编译-向量化-AOT.md — Burst 编译器原理

**为什么：** 未 Burst 编译的代码在托管环境下运行，性能比 Burst 编译后慢 10-100x。标注 `[BurstCompile]` 但内部调用了托管方法会导致静默降级为解释模式，无编译错误提示。

**EX-GAS 诊断：** 所有 `IJobEntity`/`IJobChunk`/`ISystem.OnUpdate` 在 Runtime Core 中都必须有 `[BurstCompile]`。GasRuntimeDebugger 应能检测和报告未 Burst 编译的 system。

**检查方法：** Burst Inspector 查看编译状态；Debugger 输出 `BurstCompiledSystemCount` 和 `NonBurstSystemCount`；代码审查搜索 hot path system 的 `[BurstCompile]` 缺失。

### BUR-02: FunctionPointer 用于批处理粒度的动态算法选择，禁止 per-entity invoke

**声明：** FunctionPointer 只能用于同类型 entity 的 chunk 级批处理调用。禁止在 IJobEntity 的 per-entity Execute 中逐 entity invoke FunctionPointer。

**来源：** 09-Burst-编译-向量化-AOT.md — FunctionPointer / 使用模式与反模式。CASE-11 (12-官方案例模式.md) — Burst FunctionPointer 用于动态算法选择 + 批处理粒度。

**为什么：** Per-entity invoke 造成标量调用，失去自动向量化机会，性能退化为与虚方法调用同级别。批处理 job 往往比 FunctionPointer 更简单更快。

**EX-GAS 诊断：** ExecutionCalculation 的 FunctionPointer 使用必须遵循"一类计算方式一个 FunctionPointer，同类的 entity chunk 批处理"。不能每个 Ability 一个 FunctionPointer + per-entity invoke。

**检查方法：** 审查 ExecutionCalculation job 中 FunctionPointer.Invoke 的调用位置（IJobChunk.Execute 内批量优于 IJobEntity.Execute 内逐个）。

### BUR-03: Player 性能报告必须包含 AOT/Safety/架构/warmup 上下文

**声明：** 所有提交的 Player 性能报告必须包含：Burst AOT 是否启用、`OptimizeFor` 设置、Safety Checks 状态、CPU 架构（x64/ARM64）、Burst warmup 帧数。

**来源：** 09-Burst-编译-向量化-AOT.md — Burst AOT 与 Player

**为什么：** 缺少 AOT 上下文的性能数据无法跨平台比较。Safety Checks 在 Player 中关闭带来的性能收益应被量化记录。Burst warmup 帧（首帧 JIT 编译）的成本极高，必须从稳定帧数据中排除。

**EX-GAS 诊断：** AutoChess Player 性能测试脚本自动采集 Burst 配置参数并附加到性能报告中。Debugger 输出 `BurstWarmedUp` 标志，warmup 帧不计入稳定帧统计。

**检查方法：** 性能报告头部检查强制字段；Player 启动后首 N 帧的 timing 数据标记为 warmup 阶段。

### BUR-04: 使用 `[NoAlias]` 辅助 Burst 向量化优化

**声明：** 在 Burst 编译的 job 中对 `NativeArray`/`NativeSlice` 参数和 `ref`/`in` struct 参数标注 `[NoAlias]`，帮助编译器证明无内存别名冲突，启用自动向量化。

**来源：** 09-Burst-编译-向量化-AOT.md — 向量化与别名分析

**为什么：** Burst 的自动向量化要求编译器能证明访问无别名冲突。`[NoAlias]` 显式消除编译器保守推断，生成 SIMD 向量化代码。

**EX-GAS 诊断：** EffectDeltaApply、AttributeAggregate 等大规模 per-entity 循环计算的 job，对 `NativeArray` 参数和 `ref BAttribute`/`in CModifier` 参数标注 `[NoAlias]`。

**检查方法：** Burst Inspector 检查循环是否已向量化；代码审查确认 hot path job 中 `[NoAlias]` 的标注是否合理。

### BUR-05: Editor Burst 通过不等于 Player Burst 通过，AOT 平台需单独验证

**声明：** Editor 中 Burst 编译通过不代表 Player 中能正确运行。iOS、consoles 等 AOT 平台有更多限制（如泛型特化、指针操作、FunctionPointer 可用性差异），必须在目标平台进行验证。

**来源：** 09-Burst-编译-向量化-AOT.md — 常见陷阱

**为什么：** Editor 使用 JIT 模式，Burst 可动态生成代码；AOT 平台在编译时就需要完成所有代码生成，限制更严格。泛型特化、FunctionPointer 引用等在 AOT 平台可能失败。

**EX-GAS 诊断：** AutoChess 目标平台列表中必须包含至少一个 AOT 平台的构建验证步骤。iOS/Android Player build 加入 CI 流水线。

**检查方法：** CI 配置中包含 AOT 平台（如 iOS）的 Build + Burst 编译验证。Burst Inspector 的 AOT 编译日志无 Error/Warning。

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

## 验收指标

1. 热路径 system 可标注 `[BurstCompile]` 且无托管依赖
2. Player 跑完 warmup 后再采样稳定成本
3. Burst Inspector / 编译日志能作为性能交还证据
4. ExecutionCalculation 不走 OOP delegate 链
5. Player 性能报告包含完整的 Burst AOT 上下文
