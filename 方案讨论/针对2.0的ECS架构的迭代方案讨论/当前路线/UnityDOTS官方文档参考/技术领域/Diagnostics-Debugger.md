# Diagnostics-Debugger

## 职责

维护 Unity ECS 三层诊断体系如何在 EX-GAS Runtime Core Debugger 中落地的规则。覆盖 Entities Journaling、Structural Changes Profiler Module 和手写 Runtime Core Debugger 的职责边界、使用时机和产出物要求。不覆盖 Unity Profiler 通用使用指南或非 ECS 相关的诊断工具。

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
- 是排查"哪个 system 做了不该做的结构变化"的终极工具
- 与手写 Debugger counters 互补：Journaling 用于深度排查，counters 用于日常监控

### Structural Changes Profiler Module

专门监控结构变化的 Profiler 模块：
- 每帧 entity create/destroy 数量
- Add/Remove Component 数量
- 按 system 归类结构变化来源

使用流程：
1. 开启 Profiler (Window > Analysis > Profiler)
2. 添加 Entities Structural Changes module
3. 运行场景，观察结构变化的时间和来源 system
4. 目标：hot path system 的结构变化为 0

### 手写 Runtime Core Debugger 的最小指标集

Profiler 和 Journaling 是工具链，但不能替代自动验收。EX-GAS Debugger 必须输出以下机器可读指标：

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

## 编写规范

### DBG-01: 三层诊断体系各有明确职责边界，不可互相替代

**声明：** 日常开发用手写 Debugger counters，热点定位用 Profiler + Structural Changes module，疑难排查用 Journaling 逐帧回放。不允许用 Journaling 替代 Debugger counters。

**来源：** 06-Diagnostics-Profiler-Journaling.md — 核心概念详解 / 使用模式与反模式

**为什么：** Journaling 有巨大运行时开销，不适合持续运行；Profiler 需要人工观察，不适合自动验收。三层工具链设计上各有不可替代的用途。

**EX-GAS 诊断：** GasRuntimeDebugger 当前已实现 counters 但缺少与 Journaling/Profiler 的切换引导机制。不应把 journaling 当作"实时日志"使用。

**检查方法：** Debugger 启动时检查 Journaling 是否开启；若开启且非排查模式则告警。

### DBG-02: Entities Journaling 必须在性能测试中关闭

**声明：** 执行性能测试或 benchmark 前，必须确认 Entities Journaling 已关闭（Window > Entities > Journaling）。

**来源：** 06-Diagnostics-Profiler-Journaling.md — Entities Journaling / 常见陷阱

**为什么：** Journaling 记录所有 ECS 操作流水，开销巨大，开启时跑性能测试得到的数据毫无意义。

**EX-GAS 诊断：** AutoChess performance run 前自动检测 Journaling 状态，开启则阻止测试并提示关闭。

**检查方法：** 性能测试启动脚本检查 journaling 状态；Debugger 输出 `JournalingActive = false`。

### DBG-03: Structural Changes Profiler 是热点定位工具，不能替代自动验收

**声明：** Structural Changes Profiler Module 用于人工分析 heat path 的结构变化来源。自动验收必须依赖手写 Debugger 的结构变化计数器。

**来源：** 06-Diagnostics-Profiler-Journaling.md — Structural Changes Profiler Module / 使用模式与反模式

**为什么：** Profiler 需要人工操作和观察，不能嵌入 CI/自动测试流程。自动验收要求机器可读的计数指标。

**EX-GAS 诊断：** Debugger 已输出 `EntityCreateCount`/`EntityDestroyCount`/`StructuralChangeCount`，但尚未按 system 归类结构变化来源。应扩展为记录各 system 的 `StructuralChangeCountBySystem`。

**检查方法：** Debugger 报告中包含按 system 分组的结构变化计数；目标：每个 hot path system 的结构变化为 0。

### DBG-04: 手写 Debugger 只能记录计数，不做字符串拼接、不分配托管内存

**声明：** Runtime Core Debugger 的输出仅限于 struct 级计数器和预分配的固定缓冲区。禁止在 hot path 拼接人读日志字符串、分配托管内存或触发 sync point。

**来源：** 06-Diagnostics-Profiler-Journaling.md — 常见陷阱 / 反模式总结

**为什么：** Debugger 自身的开销如果进入主线程关键路径，会污染性能数据。字符串拼接和托管分配会产生 GC 压力和非确定性暂停。

**EX-GAS 诊断：** 所有 Debugger 输出走预分配 `FixedList`/`NativeArray` 和 struct 级计数器。GasRuntimeDebugger 中的字符串应全部采用预格式化或结构化数据，不做运行时 `string.Format`/`$""`。

**检查方法：** 代码审查搜索 Debugger hot path 中的 `$""`、`string.Format`、`new string`、`new object[]`、`Debug.Log`。

### DBG-05: 性能报告必须按 cost 分组统计，禁止混合归因

**声明：** 性能报告必须按 `coreSimulationTickMs`、`physicsStepMs`、`renderMs`、`runnerBootstrapMs`、`observationTickMs` 分组统计。禁止将不同分组的 cost 混合后归因于单一系统。

**来源：** 06-Diagnostics-Profiler-Journaling.md — 性能报告的口径拆分

**为什么：** 混入 physics 或 rendering 耗时后归因 GAS 性能问题，掩盖真实瓶颈，导致优化方向错误。

**EX-GAS 诊断：** GasRuntimeFrameBudgetContract 已声明分组预算，但 Debugger 输出尚未按分组聚合。应实现 `ReportGroupedCost()` 输出各分组占比。

**检查方法：** Debugger validation summary 包含 cost 分组表格，各分组占比不为 0 且总和不超过帧预算。

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
| `entities-journaling.md` | Journaling 记录 ECS 操作流水，可回放排查；有显著开销，默认关闭 | DBG-01, DBG-02 |
| `profiler-module-structural-changes.md` | Profiler 可观察结构变化来源，按 system 归类 | DBG-01, DBG-03 |
| `performance-sync-points.md` | sync point 是性能诊断核心对象 | DBG-03, DBG-04 |
| `systems-optimizing.md` | system / type handle / lookup 数量是成本源 | DBG-05 |

## 验收指标

1. 每轮性能测试输出 core / physics / render / runner 分组
2. 能定位 top N system、query count、lookup count、structural changes、buffer spill
3. 发现架构问题时能反哺 `00-当前架构事实`
4. Debugger 不增加可观测的主线程阻塞
5. Journaling 在性能测试中自动检测并阻止
