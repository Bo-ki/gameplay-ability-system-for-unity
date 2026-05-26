# NativeContainer-Allocator: API 与 EX-GAS 解读

## 核心概念

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
var rew = new RewindableAllocator(Allocator.Persistent, 1024 * 1024); // 1MB 初始
var list = new NativeList<int>(rew.ToAllocator);
// ... 使用 ...
rew.Rewind();  // 帧末重置（内存不释放，只回退指针），下帧重新使用
```

适用场景：
- `WorldUpdateAllocator`（每帧自动 rewind）
- Frame Arena scratch buffer
- ECB 内部使用 RewindableAllocator

### NativeStream

并行 fan-in 的关键工具。每个 worker thread 写自己的 segment，无锁、无竞争：

```csharp
var stream = new NativeStream(threadCount, Allocator.TempJob);

// 并行写入
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

// Merge —— 必须 deterministic
var reader = stream.AsReader();
// collect + sort by fixed key → dispatch
```

### Deterministic Merge 要求

影响 battle hash 的输出必须可复现。NativeStream 的 forEachCount 顺序不可控：

```csharp
// 错误：依赖 forEachCount 顺序（不可控）
// 正确：收集所有 stream 内容 → 按固定 key 排序 → 再 dispatch
allCommands.Sort(new EffectCommandComparer());  // 按 target ASC id 排序
for (int i = 0; i < allCommands.Length; i++) { /* dispatch */ }
```

### ParallelWriter 的排序问题

`ParallelWriter`（如 `NativeList.AsParallelWriter()`）写入顺序不确定。只能用于结果不依赖顺序的场景（如 telemetry、presentation outbox），不能用于 battle-deterministic 场景。

---

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

### SystemGroup Allocator（CASE-16）

Frame Prepare SystemGroup 使用 `SetRateManagerCreateAllocator` + `DoubleRewindableAllocators` + `SetGroupAllocator` 建立 per-group scratch allocator。`GasRuntimeFramePrepareSystemGroup` 应配置独立的 RewindableAllocator，避免与其他 phase 的 allocator 冲突。

### NativeContainer 作为 IComponentData 的约束（PRF-34）

对于必须在 IComponentData 中持有 NativeContainer 的场景（如 EffectStore）：

```csharp
// 错误：直接对包含 NativeContainer 的 component 调度并行 job
// 正确：主线程提取 NativeContainer 数据到临时 NativeArray，再调度独立的 job 处理
```

---

## 常见陷阱

1. **Temp 传入 job**：安全检查抛异常
2. **TempJob 忘记 dispose**：4 帧后 leak detection 报警
3. **Persistent 无 teardown**：World dispose 时报 leak
4. **NativeStream segment 数量波动**：线程数变化影响 segment 布局，merge 时不能依赖顺序
5. **Rewindable 容量不够**：触发分配新块，失去 rewind 的意义
6. **GC-free 当成 allocation-free**：NativeContainer 分配仍在 Native 堆上有开销
7. **NativeContainer 当 IComponentData 字段直接参与并行 job**：safety handle 无法正确处理读写依赖
8. **NativeStream 的 forEach 顺序被当作确定性的**：线程调度影响 segment 布局

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则编号 |
|---|---|---|
| `allocator-overview.md` | Temp/TempJob/Persistent 生命周期差异；Temp 不能传入 job | NAT-01, NAT-04 |
| `allocator-rewindable.md` | RewindableAllocator 批量 rewind；帧级 scratch 用法 | NAT-01, NAT-05 |
| `parallel-readers.md` | ParallelWriter 顺序不确定；NativeStream 按线程分段 | NAT-02, NAT-03 |
| `collection-types.md` | NativeStream 等集合类型概览 | NAT-03 |
| `components-nativecontainers.md` | NativeContainer 在 IComponentData 上禁止调度并行 job | PRF-34 |
| `systems-data.md` (CASE-45) | System-Associated Entity Data — 系统级数据存为 component | NAT-04 |
| DocCodeSamples (CASE-12) | NativeStream 并行 fan-in + deterministic merge | NAT-03 |
| DocCodeSamples (CASE-16) | SystemGroup Allocator — per-group scratch allocator | NAT-01, NAT-05 |
