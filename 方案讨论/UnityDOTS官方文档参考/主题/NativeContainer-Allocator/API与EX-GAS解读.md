# NativeContainer-Allocator：API 与 EX-GAS 解读

**适用版本**：Unity `6000.3.14f1`；Collections `2.6.6`；Entities `1.4.6`

## Allocator 生命周期

| Allocator | 官方生命周期约束 | 释放/失效 |
|---|---|---|
| `Allocator.Temp` | 只在创建它的线程与作用域内安全；主线程创建的 Temp allocation 不能传入 job；job 内可使用该 worker 的 Temp allocator | 主线程 Temp 在帧末整体回收；job 的线程 Temp 在 job 结束时整体回收；单独 Dispose 不释放该块 |
| `Allocator.TempJob` | 可以传入 job；必须在创建后 4 帧内释放 | `Dispose()` 或 `Dispose(JobHandle)` |
| `Allocator.Persistent` | 可长期存在，安全系统无法判断是否超出业务生命周期 | owner 在 teardown 显式 `Dispose` |

`Dispose(JobHandle)` 会调度一个依赖输入 handle 的释放 job。容器是 struct；对一个副本调用 Dispose 不会把其他副本的 `IsCreated` 自动改为 false，因此所有权不能靠 `IsCreated` 猜测。

## RewindableAllocator 2.6.6 API

`RewindableAllocator` 不是 `new RewindableAllocator(Allocator.Persistent, size)`。Collections 2.6.6 的官方创建方式是 `AllocatorHelper<RewindableAllocator>` + `Initialize`。调用 `Rewind()` 会一次失效所有子 allocation 和 child safety handle；任何由它创建的容器都不得再访问。

```csharp
using System;
using Unity.Collections;

/// <summary>
/// 拥有一块可整批回收的帧级临时内存。
/// </summary>
public struct FrameArena : IDisposable
{
    private AllocatorHelper<RewindableAllocator> allocatorHelper;

    private ref RewindableAllocator Allocator => ref allocatorHelper.Allocator;

    /// <summary>
    /// 创建并注册 RewindableAllocator。
    /// </summary>
    public void Initialize(int initialBlockSize)
    {
        allocatorHelper =
            new AllocatorHelper<RewindableAllocator>(Unity.Collections.Allocator.Persistent);
        Allocator.Initialize(initialBlockSize, false);
    }

    /// <summary>
    /// 使用当前 arena 创建 NativeList。
    /// </summary>
    public NativeList<int> CreateList()
    {
        return new NativeList<int>(Allocator.Handle);
    }

    /// <summary>
    /// 一次失效并回收当前 arena 的全部子 allocation。
    /// </summary>
    public void Rewind()
    {
        Allocator.Rewind();
    }

    /// <summary>
    /// 注销并释放 allocator 自身及其内存块。
    /// </summary>
    public void Dispose()
    {
        Allocator.Dispose();
        allocatorHelper.Dispose();
    }
}
```

不要在仍有读取/写入 job 使用 arena allocation 时 Rewind。Entities 的 World Update Allocator 和 System Group Allocator 已管理自己的 rewind 周期；使用者只应把 allocation 保留在官方声明的更新窗口内。

## NativeStream 的真实模型

`new NativeStream(bufferCount, allocator)` 创建的是**固定数量的逻辑 buffer**，`ForEachCount == bufferCount`，且 `bufferCount` 必须大于 0。它不会自动读取 worker thread 数，也不会在调度后改变 segment 数。构造参数通常可以按线程数设计，但更稳妥的确定性模式是让逻辑工作索引映射到固定 buffer。

每个 buffer 只能由一个 writer 使用；一个 writer 对该 buffer 调用一次 `BeginForEachIndex(index)`，完成后调用 `EndForEachIndex()`。所有写入必须先于第一次读取完成。

```csharp
/// <summary>
/// 每个逻辑输入索引独占一个 NativeStream buffer。
/// </summary>
[BurstCompile]
public struct FanInJob : IJobFor
{
    public NativeStream.Writer Writer;

    /// <summary>
    /// 把当前逻辑输入的输出写入同索引 buffer。
    /// </summary>
    public void Execute(int index)
    {
        Writer.BeginForEachIndex(index);
        Writer.Write(new EffectCommand
        {
            ProducerIndex = index
        });
        Writer.EndForEachIndex();
    }
}

int inputCount = commands.Length; // 为 0 时由外层直接跳过，不创建 NativeStream。
var stream = new NativeStream(inputCount, Allocator.TempJob);
JobHandle produceHandle = new FanInJob
{
    Writer = stream.AsWriter()
}.ScheduleParallel(inputCount, 64, dependency);
```

如果构造时传 `threadCount`，却在 `IJobFor.Execute(index)` 中把任意输入 index 传给 `BeginForEachIndex`，当 input count 大于 buffer count 时会越界；同一 buffer 被多个 Execute 重复 Begin 也违反 writer 契约。

## NativeStream 与确定性

Collections 官方文档指出普通 ParallelWriter 的追加顺序依赖线程调度，而 NativeStream 通过独立 buffer 避免这种调度顺序竞争。NativeStream 本身并不自动提供“跨运行业务顺序”，确定性取决于：

1. buffer index 到逻辑输入的映射是否稳定；
2. 单个 buffer 内的生产顺序是否稳定；
3. 消费端是否固定按 `0..ForEachCount-1` 与 buffer 内写入顺序读取；
4. 若业务要求与上述顺序不同，是否按稳定全序键排序并处理 tie-breaker。

因此，“所有 NativeStream 结果必须再排序”和“NativeStream 的 ForEachCount 顺序不可控”都不正确。若一索引一 buffer 且映射稳定，按 index 合并可以确定；多个来源或同键命令需要统一业务顺序时，才进行 total-key sort。

## System Group Allocator

`ComponentSystemGroup.SetRateManagerCreateAllocator(IRateManager)` 在设置 rate manager 的同时创建 group allocator。官方示例在 system group 构造函数中调用它。不是传入不存在的 `RateUtils.RateManagerCreateAllocator` 工厂，也不需要业务代码自行构造 `DoubleRewindableAllocators`。

```csharp
/// <summary>
/// 以固定步长更新并拥有 group allocator 的系统组。
/// </summary>
public partial class GasFixedStepGroup : ComponentSystemGroup
{
    /// <summary>
    /// 创建 fixed-rate manager 和对应的 group allocator。
    /// </summary>
    public GasFixedStepGroup()
    {
        SetRateManagerCreateAllocator(new RateUtils.FixedRateSimpleManager(1f / 60f));
    }
}
```

## EX-GAS 项目策略

- 每个 NativeContainer 分配点说明 allocator、owner、最后使用它的 JobHandle，以及 Dispose/Rewind 边界。
- 影响 battle hash 的无序并行输出必须在消费前建立稳定顺序；不禁止 ParallelWriter 本身，禁止的是把其物理写入顺序当业务顺序。
- Debugger 记录项目能可靠采集的 allocation count/bytes、NativeStream buffer/item count、arena 高水位；指标及告警阈值必须由基准校准。
- 将 NativeContainer 嵌入 component 时，禁止对该 component 调度 `IJobChunk`/`IJobEntity`；主线程获取 component、提取 container，再把 container 本身传给 job，并正确串联依赖。

## 官方证据

| 官方文档 | 可裁决结论 |
|---|---|
| Collections `allocator-overview.md` | Temp/TempJob/Persistent 生命周期、4 帧限制、`Dispose(JobHandle)`、`IsCreated` 限制 |
| Collections `allocator-rewindable.md` | `AllocatorHelper<RewindableAllocator>`、Initialize/Rewind/Dispose 顺序和失效语义 |
| Collections `parallel-readers.md` | ParallelWriter 顺序不确定；NativeStream/UnsafeStream 可隔离并行读写 |
| Collections `NativeStream.cs` API 注释 | bufferCount/ForEachCount、单 buffer writer、Begin/End 和先写后读契约 |
| Entities `allocators-system-group.md` | `SetRateManagerCreateAllocator` 与 group allocator 生命周期 |
| Entities `components-nativecontainers.md` | 禁止对含嵌套 NativeContainer 的 component 调度 IJobChunk/IJobEntity；允许提取 container 后调度 |
