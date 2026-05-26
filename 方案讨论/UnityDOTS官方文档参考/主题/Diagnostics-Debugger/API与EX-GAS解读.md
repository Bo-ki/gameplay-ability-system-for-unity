# Diagnostics-Debugger: API 与 EX-GAS 解读

## 核心概念

### 三层诊断体系

| 层级 | 工具 | 使用场景 | 对 Runtime 的影响 |
|---|---|---|---|
| **ECS 内置** | Entities Journaling | 排查 entity/chunk/component 生命周期 bug | 有开销，默认关闭 |
| **Unity Profiler** | Structural Changes Profiler module | 定位结构变化热点 | Profiler 开销，应在 profile build 中 |
| **手写 Debugger** | Runtime Core counters + structured log | 架构级性能监控和自动验收 | 极低（只记录计数） |

### Entities Journaling

记录 ECS 操作流水，用于回放排查 bug：
- 记录内容：entity create/destroy、component add/remove、system update
- 输出：可导出为 JSON，支持 frame-by-frame 回放
- **有显著的运行时开销，不应在性能测试中开启**
- 与手写 Debugger counters 互补：Journaling 用于深度排查，counters 用于日常监控

### Structural Changes Profiler Module

专门监控结构变化的 Profiler 模块：
- 每帧 entity create/destroy 数量
- Add/Remove Component 数量
- 按 system 归类结构变化来源

### 手写 Runtime Core Debugger 的最小指标集

```
System 层级:     SystemTimingMs, SystemEntityCount
Query 层级:      ActiveQueryCount, FilteredQueryCount, UnfilteredQueryCount
结构变化:        EntityCreateCount, EntityDestroyCount, StructuralChangeCount, EcbCommandCount
Buffer:          BufferSpillCount, BufferPressureRatios
Allocator:       TempJobAllocCount, PersistentAllocCount
Burst:           BurstWarmedUp, BurstCompiledSystemCount
```

### 性能报告的口径拆分

必须拆分的 cost 分组：

| 分组 | 含义 |
|---|---|
| `coreSimulationTickMs` | GASRuntimeCore 各 phase |
| `physicsStepMs` | 如有 Physics |
| `renderMs` | 如有渲染 |
| `runnerBootstrapMs` | world 创建、初始化 |
| `observationTickMs` | presentation outbox、replay sink |

**禁止**把 `physicsStepMs` 或 `renderMs` 混入 `coreTickMs` 后归因 GAS 性能问题。

---

## EX-GAS 项目解读

### 当前诊断流程的不足

- 能知道哪个 system 慢，但不知道为什么慢
- 不知道每帧结构变化来自哪里
- 不知道 buffer 是否接近溢出
- 不知道 lookup 是否成为瓶颈

### 目标态诊断流程

```
AutoChess 运行
    → Debugger 收集 counters（system timing + structural + buffer + query）
    → validation summary 自动输出
    → 异常阈值触发告警
    → 如果需要深度排查 → Profiler + Journaling
```

### RuntimeDiagnostics IComponentData

```csharp
public struct RuntimeDiagnostics : IComponentData
{
    // System 层级
    public FixedList512Bytes<float> SystemTimingMs;
    public FixedList512Bytes<int> SystemEntityCount;

    // Query 层级
    public int ActiveQueryCount;
    public int FilteredQueryCount;
    public int UnfilteredQueryCount;

    // 结构变化
    public int EntityCreateCount;
    public int EntityDestroyCount;
    public int StructuralChangeCount;
    public int EcbCommandCount;

    // Buffer
    public int BufferSpillCount;
    public FixedList128Bytes<float> BufferPressureRatios;

    // Allocator
    public int TempJobAllocCount;
    public int PersistentAllocCount;

    // Burst
    public bool BurstWarmedUp;
    public int BurstCompiledSystemCount;
}
```

### Debugger 自身防止性能污染

手写 Debugger 只能记录计数，不做字符串拼接、不分配托管内存。所有计数器走 struct 级字段，嵌入 `RuntimeDiagnostics : IComponentData`，以 singleton 方式挂到 Debugger World。

---

## 常见陷阱

1. **Debugger 自身成为性能问题**：手写 Debugger 只能记录计数，不做字符串拼接、不分配托管内存
2. **Journaling 开启时跑性能测试**：Journaling 有巨大开销，性能数据无意义
3. **`avgTickMs` 掩盖问题**：单帧 spike（如 ECB playback）被平均后看不出
4. **Burst warmup 污染首帧数据**：Player 性能报告必须排除前 N 帧
5. **用 Journaling 替代 Debugger counters**：职责混淆，Journaling 只能用于深度排查
6. **Debugger 输出影响 simulation**：如 Debugger 触发 sync point
7. **在 hot path 拼接人读日志字符串**：GC 压力和非确定性暂停

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则编号 |
|---|---|---|
| `entities-journaling.md` | Journaling 记录 ECS 操作流水，有显著开销，默认关闭 | DBG-01, DBG-02 |
| `profiler-module-structural-changes.md` | Profiler 可观察结构变化来源，按 system 归类 | DBG-01, DBG-03 |
| `performance-sync-points.md` | sync point 是性能诊断核心对象 | DBG-03, DBG-04 |
| `systems-optimizing.md` | system / type handle / lookup 数量是成本源 | DBG-05 |
