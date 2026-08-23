# NativeContainer / Allocator：API 与 EX-GAS 解读

> 精确基线：Unity 6000.3 / Entities 1.4.6
> 边界：allocator 的物理寿命来自官方；EX-GAS 另设更短的业务所有权

## 1. 官方机制

### 1.1 Allocator 类型

官方区分 Temp、TempJob、Persistent、rewindable、world update、system group 与 ECB allocator。选择要同时考虑 lifetime、Job 可用性、释放责任与线程安全，不能只看分配速度。

官方快照：

- [Allocator overview](../../官方文档原件/com.unity.entities/Documentation~/allocators-overview.md)
- [World update allocator](../../官方文档原件/com.unity.entities/Documentation~/allocators-world-update.md)
- [System group allocator](../../官方文档原件/com.unity.entities/Documentation~/allocators-system-group.md)
- [ECB allocator](../../官方文档原件/com.unity.entities/Documentation~/allocators-entity-command-buffer.md)

### 1.2 World/group allocator 生命周期

- World update allocator 是 double rewindable；官方说明分配物理寿命跨两次 world update。
- 调用 `SetRateManagerCreateAllocator` 的 ComponentSystemGroup 可建立 double rewindable group allocator。
- 组更新时 group allocator 被设为当前 world update allocator；通过 `SystemState.WorldUpdateAllocator` 创建的容器会使用当前上下文 allocator。
- group allocator 的分配物理寿命跨两次该组更新。

“内存还没被 rewind”只说明物理存储暂时存在，不自动授予另一个 Tick/System 读取权，也不替代 JobHandle 依赖。

### 1.3 NativeContainer 与并行写

- NativeContainer 的 safety/依赖规则要求明确读写者。
- ParallelWriter 允许并发追加，不保证物理写入顺序等于游戏语义顺序。
- `NativeStream` 适合按 logical foreach index 分区写、之后读取的 fan-in 场景，但需要定义分区、读取和稳定 merge；并非所有 fan-in 的默认最优容器。
- Persistent 容器必须有唯一 owner、释放点和 shutdown 路径。

关联规则：[NAT-01](./NAT-01.md)、[NAT-02](./NAT-02.md)、[NAT-03](./NAT-03.md)、[NAT-04](./NAT-04.md)。

## 2. EX-GAS v1 裁决

### 2.1 Kernel-owned Tick scratch

`GasTickKernelSystem` 使用 `state.WorldUpdateAllocator` 创建：

- canonical command keys/ranges；
- resolve/validation partitions；
- bounded pre-apply operation list；
- target buckets/ranges；
- target-local worklist；
- Core Fact partitions 与 stable merge indices；
- Boundary routing indices。

虽然 allocator 物理寿命可能覆盖两次组更新，EX-GAS 把这些容器的项目可用期严格限制到当前 `SimulationTick` 的 Kernel Job DAG。Tick 结束后不再保存/读取。

### 2.2 单 Job DAG

Kernel 从 `state.Dependency` 建立连续依赖，把所有 container producer/consumer 串成 DAG，并将最终 handle 交回 `state.Dependency`。真实 ECB producer 也登记完整依赖。

禁止：

- lane/phase 之间 `Complete()`；
- 把 scratch 写入 Component/Buffer/Singleton/static/managed field；
- 一个 System 分配、另一个 System 消费当前 Tick scratch；
- 手工 `Rewind` 或自建 `FrameArenaSingleton`；
- 用 WorldUpdateAllocator 保存跨 Tick continuation/effect/pending request。

长期状态进入 ASC DynamicBuffer；托管 Boundary retention 进入 drain 之后的 managed queue。

## 3. 容器选择原则

| 工作集 | 候选 | 选择条件 |
|---|---|---|
| 追加并可预估长度 | `NativeList` + 分区/prefix sum | 单/分区 writer，后续稳定排序 |
| per-target ranges | key array + sort/radix + range scan | 需要 canonical target order |
| 并行可变 fan-in | `NativeStream` | logical index 映射自然、两段读取可接受 |
| key→value 临时表 | Native hash map/multi-map | 查找收益高于 hash/内存成本 |
| bit/dirty work | Native bit array/word array | Catalog-indexed 固定空间 |

Spec 不指定一种容器覆盖所有 lane。选择必须记录：

- owner 与 allocator；
- 写入/读取 Job；
- logical ordering；
- capacity/overflow 策略；
- 最终依赖与失效点；
- ScaleProfile 数据。

## 4. 确定性

ParallelWriter 的 append 顺序、Job worker 和 hash iteration 都不能成为 Command/Fact 顺序。结果必须按显式 stable key canonicalize，例如：

```text
(SimulationTick, TargetStableId, SourceStableId, SourceSequence, Kind, LocalSequence)
```

是否需要全排序、radix、bucket-local sort 或 prefix sum 由 ScaleProfile 决定；语义 key 不得随算法改变。

## 5. ScaleProfile

必须采集：

- 每个 Tick 临时容器峰值、初始容量、扩容/overflow；
- allocator 分配次数与字节；
- target bucket 长度/偏斜；
- sort/build/merge 各 Job 耗时；
- Job DAG 空洞与主线程 sync 原因；
- container 选择对 cache/chunk/worker 的影响；
- 0/1/N fixed Tick 的峰值内存。

不在通用文档写死容器容量、ASC 数、毫秒或分配次数阈值。profile 需包含硬件、版本、输入分布、采样方法与基线 commit。

## 6. 生命周期验收

- 一个渲染帧连续 N 个 FixedStep update 时，每 Tick scratch 不被下一 Tick 读取。
- safety checks/Burst 下无 use-after-rewind、race 或 leaked Persistent allocation。
- phase profiler 中无非必要 `Complete`。
- manual World 通过完整 FixedStep 父链获得相同 group allocator 上下文。
- shutdown 完成所有 producer 依赖后才释放 Persistent/Blob/managed owner。

## 7. 不能由官方直接推出

官方只提供 allocator/container 能力；以下是项目决策：

- 一个 Kernel 拥有全部 Tick scratch；
- 项目可用期比 allocator 物理寿命更短；
- target-local bucket/single writer；
- same-tick closed bounded DAG；
- 不自建 FrameArena；
- 具体 stable key 与 ScaleProfile 门。
