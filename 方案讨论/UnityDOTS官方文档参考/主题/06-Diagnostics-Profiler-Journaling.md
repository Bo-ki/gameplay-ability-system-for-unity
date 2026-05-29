# 06 Diagnostics、Profiler 与 Journaling

## 职责

本主题维护 Unity 官方诊断工具如何进入 EX-GAS Runtime Core Debugger、性能验收和问题反哺流程。

## 核心概念详解

### 三层诊断体系

| 层级 | 工具 | 使用场景 | 对 Runtime 的影响 |
|---|---|---|---|
| **ECS 内置** | Entities Journaling | 排查 entity/chunk/component 生命周期 bug | 有开销，默认关闭 |
| **Unity Profiler** | Structural Changes Profiler module | 定位结构变化热点 | Profiler 开销，应在 profile build 中 |
| **手写 Debugger** | Runtime Core counters + structured log | 架构级性能监控和自动验收 | 极低（只记录计数） |

### Entities Journaling

记录 ECS 操作流水，用于回放排查 bug：

```csharp
// 开启方式：Window > Entities > Journaling
// 记录内容：world/entity create/destroy、system add/remove、component add/remove、RW component access
// 输出：Journaling window 或 Unity.Entities.EntitiesJournaling API
```

**关键事实：**
- 有显著的运行时开销，不应在性能测试中开启
- 记录使用 FIFO 内存；Preferences > Entities > Journaling 的 Total Memory MB 会影响记录保留窗口
- 可记录 `GetComponentDataRW` / `GetBufferRW` 访问，但官方文档说明它不是字段级 diff；Unity 会根据相邻记录推断可能的 setter
- 记录包含执行 system、ECB origin system、frame index、record index、record type、world、entity 和 component type 等信息
- 是排查"哪个 system 做了不该做的结构变化或 RW 访问"的官方工具
- 与手写 Debugger counters 互补：Journaling 用于深度排查，counters 用于日常监控

### Structural Changes Profiler Module

专门监控结构变化的 Profiler 模块：
- 每帧 entity create/destroy 数量
- Add/Remove Component 数量
- 按 system 归类结构变化来源
- Profiler module 未启用时不会收集数据；profiling 后再启用 module 也不会回填历史数据

**使用流程：**
```
1. 开启 Profiler (Window > Analysis > Profiler)
2. 添加 Entities Structural Changes module
3. 运行场景 → 观察结构变化的时间和来源 system
4. 目标：hot path system 的结构变化为 0
```

### 手写 Runtime Core Debugger 的最小指标集

Profiler 和 Journaling 是工具链，但不能替代自动验收。EX-GAS Debugger 必须输出以下机器可读指标：

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
    public int BufferSpillCount;       // 本帧溢出次数
    public FixedList128Bytes<float> BufferPressureRatios;  // 各主要 buffer 的 length/capacity

    // Allocator
    public int TempJobAllocCount;
    public int PersistentAllocCount;

    // Burst
    public bool BurstWarmedUp;
    public int BurstCompiledSystemCount;
}
```

## 官方证据

| 证据 | 结论 |
|---|---|
| `entities-journaling.md` | Journaling 记录 world/entity/system/component/RW access 操作，可通过窗口或 API 检查；记录使用 FIFO 内存 |
| `profiler-modules-entities-introduction.md` | Entities Structural Changes / Entities Memory 是 Profiler modules；module 未启用时不采集数据 |
| `profiler-module-structural-changes.md` | Profiler 可观察结构变化来源 |
| `performance-sync-points.md` | sync point 是性能诊断核心对象 |
| `systems-optimizing.md` | system / type handle / lookup 数量是成本源 |

## 使用模式与反模式

**正确模式：**
- 日常开发：手写 Debugger counters 挂 singleton
- 热点定位：Profiler + Structural Changes module
- 疑难排查：Journaling 逐帧回放
- 自动验收：counters → validation summary 自动报告

**反模式：**
- 用 Journaling 替代 Debugger counters
- 只靠 `avgTickMs` 归因性能问题
- Debugger 输出影响 simulation（如 Debugger 触发 sync point）
- 在 hot path 拼接人读日志字符串

## EX-GAS 项目解读

### 当前诊断流程的不足

当前热点查找依赖 "systemTiming + validation summary + 人工日志对照"：
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

### Headless official diff 口径

1. Functional x1 可以临时启用 Entities Journaling 做 official diff，但这不是性能 benchmark。
2. 临时采样必须恢复原 `EntitiesJournaling.Enabled` 状态；batchmode 中还要清理本次 official diff 产生的 Journaling 持久状态，否则 Native leak trace 会把官方工具缓存误判为 Runtime Core 泄漏。
3. Profiler modules 在无头命令中未启用时，只能输出 `profilerEnabled=false`、category 状态和 disabled reason，不能声明 profiler captured。
4. Debugger 数据、Journaling 数据和 Profiler modules 数据必须并列展示差分，不得互相替代。

### 性能报告的口径拆分

**必须拆分的 cost 分组：**
```
coreSimulationTickMs    (GASRuntimeCore 各 phase)
physicsStepMs           (如有 Physics)
renderMs                (如有渲染)
runnerBootStrapMs       (world 创建、初始化)
observationTickMs       (presentation outbox、replay sink)
```

禁止把 `physicsStepMs` 或 `renderMs` 混入 `coreTickMs` 后归因 GAS 性能问题。

## 常见陷阱

1. **Debugger 自身成为性能问题**：手写 Debugger 只能记录计数，不做字符串拼接、不分配托管内存
2. **Journaling 开启时跑性能测试**：Journaling 有巨大开销，性能数据无意义
3. **`avgTickMs` 掩盖问题**：单帧 spike（如 ECB playback）被平均后看不出
4. **Burst warmup 污染首帧数据**：Player 性能报告必须排除前 N 帧

## 验收指标

1. 每轮性能测试输出 core / physics / render / runner 分组
2. 能定位 top N system、query count、lookup count、structural changes、buffer spill
3. 发现架构问题时能反哺 `00-当前架构事实`
4. Debugger 不增加可观测的主线程阻塞
