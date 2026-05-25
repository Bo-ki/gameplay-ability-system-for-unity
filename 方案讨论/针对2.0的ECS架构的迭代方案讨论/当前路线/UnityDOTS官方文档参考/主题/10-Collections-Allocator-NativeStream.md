# 10 Collections、Allocator 与 NativeStream

## 职责

本主题维护 NativeContainer、allocator 生命周期、ParallelWriter、NativeStream、deterministic merge 和 dispose 规则。这直接决定 EX-GAS 的并行 fan-in 数据流形态。

## 核心概念详解

### Allocator 三种生命周期

| Allocator | 生命周期 | 释放方式 | 典型场景 |
|---|---|---|---|
| `Allocator.Temp` | 当前帧 | 自动（frame 结束） | 单帧 scratch、临时排序 |
| `Allocator.TempJob` | Job 或手动 dispose | `Dispose()` | Job 内临时分配 |
| `Allocator.Persistent` | 手动 dispose | `Dispose()` | 跨帧缓存、Debugger buffer |

**关键规则：**
- `Allocator.Temp` 内存**不能**传入 job（会触发 Safety Checks 错误）
- `Allocator.TempJob` 必须在 4 帧内 dispose，否则 leak detection 报警
- `Allocator.Persistent` 必须有明确的 owner system/entity 和 teardown 规则

### RewindableAllocator

```csharp
// 每帧分配、rewind 重置——最适合 frame scratch
var rew = new RewindableAllocator(Allocator.Persistent, 1024 * 1024); // 1MB

// 使用
var list = new NativeList<int>(rew.ToAllocator);
// ... 使用 ...

// 帧末重置（内存不释放，只回退指针）
rew.Rewind();
// 下帧重新使用同一块内存
```

**适用场景：**
- `WorldUpdateAllocator`（每帧自动 rewind）
- Frame Arena scratch buffer
- ECB 内部使用 RewindableAllocator

### NativeStream

并行 fan-in 的关键工具。每个 worker thread 写自己的 segment，无锁、无竞争：

```csharp
// 创建 NativeStream
var stream = new NativeStream(threadCount, Allocator.TempJob);

// 在 job 中并行写入
[BurstCompile]
public struct FanInJob : IJobFor
{
    public NativeStream.Writer StreamWriter;

    public void Execute(int index)
    {
        StreamWriter.BeginForEachIndex(index);
        StreamWriter.Write(new EffectCommand { ... });
        StreamWriter.EndForEachIndex(index);
    }
}

// Merge 阶段 —— 必须 deterministic
var reader = stream.AsReader();
for (int i = 0; i < reader.ForEachCount; i++)
{
    reader.BeginForEachIndex(i);
    while (reader.RemainingItemCount > 0)
    {
        var cmd = reader.Read<EffectCommand>();
        sortedList.Add(cmd);  // 收集后按 target 排序
    }
    reader.EndForEachIndex();
}
stream.Dispose();
```

### Deterministic Merge 要求

影响 battle hash 的输出必须可复现：

```csharp
// 错误：依赖 NativeStream 的 forEachCount 顺序（不可控）
// 正确：收集所有 stream 内容 → 按固定 key 排序 → 再 dispatch
var allCommands = new NativeList<EffectCommand>(Allocator.TempJob);
// 收集...
allCommands.Sort(new EffectCommandComparer());  // 按 target ASC id 排序
for (int i = 0; i < allCommands.Length; i++) { /* dispatch */ }
```

### ParallelWriter 的排序问题

`ParallelWriter`（如 `NativeList.AsParallelWriter()`）写入顺序不确定。只能用于结果不依赖顺序的场景（如 telemetry、presentation outbox），不能用于 battle-deterministic 场景。

## 官方证据

| 证据 | 结论 |
|---|---|
| `allocator-overview.md` | Temp/TempJob/Persistent 生命周期差异 |
| `allocator-rewindable.md` | RewindableAllocator 批量 rewind |
| `parallel-readers.md` | ParallelWriter 顺序不确定；NativeStream 按线程分段 |
| `collection-types.md` | NativeStream 等集合类型 |

## 使用模式与反模式

**正确模式：**
- Frame scratch → Temp 或 RewindableAllocator
- Job 内分配 → TempJob
- 跨帧数据 → Persistent + 明确 owner
- 并行 fan-in → NativeStream + deterministic merge
- 日志/遥测 → ParallelWriter（不参与 battle hash）

**反模式：**
- Temp 内存传入 job
- 无 owner 的 Persistent 分配
- NativeStream 的 forEach 顺序被当作确定性的
- GC-free 当成 allocation-free（NativeContainer 分配仍存在）

## EX-GAS 项目解读

### EffectCommand fan-in 的 allocator 设计

```
Command Ingest phase:
    Boundary request → 按 target ASC 分类
    并行写入：NativeStream（per-thread segment）
    
Spec Evaluation phase:
    Deterministic merge：collect + sort by target ASC
    RewindableAllocator scratch（每帧 rewind）
    
Delta Apply phase:
    Per-owner DynamicBuffer（属性聚合）
    TempJob allocator（临时排序）
```

### Debugger 的 allocator 指标

```csharp
Debugger counters:
    - TempJob alloc count / 帧
    - Persistent alloc count（应该为 0 或恒定）
    - Rewindable capacity rewind 前峰值
    - NativeStream segment count / 帧
```

## 常见陷阱

1. **Temp 传入 job**：安全检查抛异常
2. **TempJob 忘记 dispose**：4 帧后 leak detection 报警
3. **Persistent 无 teardown**：World dispose 时报 leak
4. **NativeStream segment 数量波动**：线程数变化影响 segment 布局
5. **Rewindable 容量不够**：触发分配新块，失去 rewind 的意义

## 验收指标

1. API 选型表包含 allocator 和 deterministic output policy
2. Debugger 报告 NativeContainer 分配、dispose、stream segment、merge cost
3. 十万/百万实体压测下 fan-in 不因单一写入点串行化
4. Battle hash 稳定（确定性 merge）
