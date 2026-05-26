# NativeContainer-Allocator

## 职责

维护 NativeContainer（NativeList、NativeArray、NativeStream 等）在 EX-GAS 中的 allocator 生命周期管理、确定性写入规则和 dispose 规范。覆盖 Allocator.Temp/TempJob/Persistent 三类型使用边界、RewindableAllocator 帧级一次性分配、NativeStream 并行 fan-in 与 deterministic merge、以及容器归属的监控要求。不覆盖托管内存分配或 UnityEngine.Object 的资源管理。

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

## 编写规范

### NAT-01: 每个 NativeContainer 必须说明 allocator 类型、owner 和 dispose/rewind 位置

**声明：** 所有在 Runtime Core 中分配的 NativeContainer（NativeList、NativeArray、NativeStream、NativeHashMap 等）必须在代码注释或文档中明确标注：allocator 类型（Temp/TempJob/Persistent）、owner（哪个 system 或 entity 负责释放）、dispose 或 rewind 的发生位置和时机。

**来源：** 10-Collections-Allocator-NativeStream.md — Allocator 三种生命周期 / EX-GAS 项目解读。PRF-14 (13-DOTS编写规范与性能陷阱.md) — NativeContainer 必须明确 Allocator 归属和生命周期。

**为什么：** 缺少归属声明的 Persistent 分配导致内存泄漏。TempJob 忘记 dispose 在 4 帧后触发 leak detection。明确的 owner 规则是防止内存泄漏的第一道防线。

**EX-GAS 诊断：** Debugger 报告中包含 `TempJobAllocCount`、`PersistentAllocCount`（应为 0 或恒定）、`RewindableCapacity`。所有 NativeContainer 声明遵循 `new NativeList<T>(Allocator.TempJob)` 并在同一 system 的帧末 `Dispose`。

**检查方法：** 代码审查每个 NativeContainer 分配点，确认 allocator 选择和 dispose/rewind 位置；Debugger 报告 Persistent alloc count 是否超出预期。

### NAT-02: 影响 battle hash 的结果禁止使用无序 ParallelWriter

**声明：** 任何影响 battle 同步、replay、或确定性验证的输出路径，禁止使用 `NativeList.AsParallelWriter()`、`NativeHashMap.ParallelWriter` 等无序并行写入器。必须使用 NativeStream + deterministic merge 或顺序写入。

**来源：** 10-Collections-Allocator-NativeStream.md — ParallelWriter 的排序问题 / 使用模式与反模式

**为什么：** ParallelWriter 的写入顺序取决于线程调度，每次运行可能不同。battle-deterministic 场景要求相同输入产生完全相同的输出序列。

**EX-GAS 诊断：** EffectCommand fan-in 使用 NativeStream + deterministic merge（collect + sort by target ASC），不使用 ParallelWriter。Telemetry/presentation outbox 可使用 ParallelWriter（不参与 battle hash）。

**检查方法：** Grep 搜索 `AsParallelWriter` 在 Runtime Core 中位于 battle-deterministic 路径的使用；若找到且无确定性保证注释则违规。

### NAT-03: NativeStream 并行 fan-in 必须定义 merge 顺序和内存预算

**声明：** 使用 NativeStream 进行并行 fan-in 时，必须：1) 定义明确的 deterministic merge 策略（collect → sort by 固定 key → dispatch）；2) 设定 segment 数量和每个 segment 的内存预算上限；3) merge 结果必须经过排序后再写入目标 store。

**来源：** 10-Collections-Allocator-NativeStream.md — NativeStream / Deterministic Merge 要求 / 验收指标。CASE-12 (12-官方案例模式.md) — NativeStream 并行 fan-in + deterministic merge。

**为什么：** NativeStream 的 segment 数量随线程数变化而波动。forEachCount 遍历顺序不是确定性的。不排序直接 merge 会产生非确定性输出，破坏 battle hash。

**EX-GAS 诊断：** EffectCommand fan-in 的管线设计：Command Ingest phase 并行写入 NativeStream → Spec Evaluation phase 收集排序 → Delta Apply phase 顺序应用。Debugger 报告 NativeStream segment count / 帧、merge cost。

**检查方法：** 每个 NativeStream 使用点检查 merge 阶段是否有排序步骤；Debugger 报告 segment 数量和 merge 耗时。

### NAT-04: Persistent 容器必须有明确的 owner entity/system 和 teardown 规则

**声明：** 使用 `Allocator.Persistent` 的 NativeContainer 必须关联到一个明确的 owner（具体 system 或 entity），并在 owner 销毁时自动或手动释放。不允许"全局无主"的 Persistent 分配。

**来源：** 10-Collections-Allocator-NativeStream.md — Allocator 三种生命周期 / 常见陷阱。CASE-45 (16-官方案例模式-高级.md) — System-Associated Entity Data：系统级数据存为 component 而非 system 字段，生命周期自动跟随系统。

**为什么：** Persistent 内存不会自动回收。无主分配导致内存泄漏，World dispose 时触发 leak 报警。明确的 owner 规则使得 teardown 路径可追踪。推荐利用 System-Associated Entity 机制管理 Persistent 生命周期。

**EX-GAS 诊断：** Debugger 的 Persistent 分配应集中且数量恒定（如 Debugger buffer 的 Persistent 分配）。效果管线中 Persistent 分配应为 0，全部使用 TempJob 或 RewindableAllocator。

**检查方法：** 审查所有 `Allocator.Persistent` 使用点，确认其 owner 和释放路径；Debugger 报告 PersistentAllocCount 每帧稳定。

### NAT-05: Allocator 指标必须进入 Debugger 监控；GC-free 不等于 allocation-free

**声明：** Debugger 必须报告 TempJob、Persistent 的分配计数和 RewindableAllocator capacity 峰值。团队必须理解"GC-free"只意味着无托管内存分配（无 GC 暂停），NativeContainer 的 Native 内存分配仍然存在且有成本。

**来源：** 10-Collections-Allocator-NativeStream.md — EX-GAS 项目解读 / 常见陷阱

**为什么：** NativeContainer 分配虽然在非托管堆上（无 GC 暂停），但每次分配仍然有 Native 内存分配器调用、边界检查、类型初始化等开销。大规模频繁的 NativeContainer 分配/释放会影响帧率。

**EX-GAS 诊断：** RuntimeDiagnostics 包含：`TempJobAllocCount`（不应线性增长）、`PersistentAllocCount`（应为 0 或恒定）、`RewindableCapacity`（rewind 前峰值）、`NativeStreamSegmentCount`（/帧）。

**检查方法：** Debugger validation summary 包含 allocator 指标表格；性能任务交还时确认 NativeContainer 分配次数与 entity 数的关系。

### PRF-14（关联引入）: NativeContainer 必须明确 Allocator 归属和生命周期

**声明：** 所有 NativeContainer 的 allocator 选择和生命周期必须在任务交还时说明，接受 code review 检查。

**来源：** 13-DOTS编写规范与性能陷阱.md — 官方证据索引。90-规则编号索引.md — PRF-14。

**为什么：** 这是编码规范级别的要求，与 NAT-01 互为补充。NAT-01 要求代码注释标注，PRF-14 要求任务交还时审查。

**EX-GAS 诊断：** 每条带有 NativeContainer 分配的 PR/任务必须审查 allocator 选择是否合适。

**检查方法：** Code review checklist 包含 NativeContainer 归属确认项。

### PRF-21（关联引入）: 工作线程结构变化用 ExclusiveEntityTransaction

**声明：** 需要在工作线程执行结构变化时（而非 job 中录制 ECB），使用 `ExclusiveEntityTransaction` 而不是直接 `EntityManager` 调用。

**来源：** 90-规则编号索引.md — PRF-21

**为什么：** `ExclusiveEntityTransaction` 允许在主线程外的单独线程进行结构变化操作，且不会触发 sync point。适用于 World 初始化、battle 加载等批量结构变化场景。

**EX-GAS 诊断：** AutoChess battle init、Scene loading 中的批量 entity 创建应考虑 `ExclusiveEntityTransaction`。这些场景不属于 hot path 但需要快速完成大量结构变化。

**检查方法：** 搜索批量 entity 创建场景，确认使用 `ExclusiveEntityTransaction` 而非逐 entity 的 `EntityManager` 调用。

### PRF-34（关联引入）: NativeContainer 放在 IComponentData 上时禁止调度 IJobChunk/IJobEntity

**声明：** 禁止将 `NativeContainer`（如 `NativeList<T>`、`NativeArray<T>`）直接作为 `IComponentData` 字段并对该 component 调度 `IJobChunk` 或 `IJobEntity`。

**来源：** 13-DOTS编写规范与性能陷阱.md — 官方证据索引（`components-nativecontainers.md` → PRF-34）。90-规则编号索引.md — PRF-34。

**为什么：** ECS 安全系统会对 NativeContainer 字段自动建立 safety handle。当对具有 NativeContainer 字段的 component 调度并行 job 时，safety 系统无法正确处理 job 间的读写依赖，导致未定义行为。正确的做法是在主线程提取 NativeContainer 内容后单独调度 job。

**EX-GAS 诊断：** 如果 EffectStore、ActiveEffectSlot 等 store 使用 NativeContainer 作为 IComponentData 的字段，必须先在主线程提取数据再做 job 调度。

**检查方法：** Grep 搜索 IComponentData struct 定义中的 NativeContainer 类型字段（`NativeList`、`NativeArray`、`NativeHashMap` 等）；对每个匹配项检查是否存在对该 component 的 IJobChunk/IJobEntity 调度。

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

## 验收指标

1. API 选型表包含 allocator 和 deterministic output policy
2. Debugger 报告 NativeContainer 分配、dispose、stream segment、merge cost
3. 十万/百万实体压测下 fan-in 不因单一写入点串行化
4. Battle hash 稳定（确定性 merge）
5. 所有 Persistent 分配有明确 owner 和 teardown 路径
6. 零 NativeContainer 作为 IComponentData 字段直接参与并行 job 调度
