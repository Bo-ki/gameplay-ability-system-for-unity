# 20 GAS Runtime Core API 选型基线

## 定位

本文件是 EX-GAS Runtime Core 的 **项目级 API selection checkpoint**，不是 Unity 官方规范。所有结论必须先引用对应主题中的官方机制，再说明 EX-GAS 为什么采用或拒绝某个方案。

**类型：** EX-GAS 项目治理规则

**证据等级：** 项目推导；具体 API 机制以关联主题的精确 PackageCache 来源为准

**适用版本：** Unity 6000.3.14f1；Entities 1.4.6；Collections 2.6.6；Burst 1.8.29

## 选型流程

1. 明确数据是否参与 gameplay 权威结果、确定性 hash 或 replay。
2. 明确 owner、生命周期、最大并发写入者、容量边界和销毁责任。
3. 比较候选 API 的数据布局、同步、结构变化、Burst/AOT 和确定性语义。
4. 写明采用、拒绝和暂不相关的理由；不能用“官方要求”替代项目判断。
5. 定义可观测指标和重新选型条件。固定数量或耗时只有附基准证据时才能成为阈值。

## 数据性质与默认候选

| 数据性质 | 生命周期 | 默认候选 | 关键验证 |
|---|---|---|---|
| Gameplay 权威状态 | 跨帧 | owner component / DynamicBuffer / Blob 引用 | owner、容量、确定性 |
| 本帧确定性命令 | 帧内 | owner-local buffer、按逻辑分区的 NativeStream、Job scratch | merge key、tie-break、allocator |
| 结构变化请求 | 延迟提交 | 明确 phase 的 ECB；主线程批量同类变化可评估 EntityManager bulk API | playback 位置、同步点、命令量 |
| 静态定义 | 长生命周期只读 | BlobAsset 或生成的只读数据 | 构建时机、释放责任 |
| 高频状态 | 跨帧 | enum/bit field、Enableable 或按 query 分离的数据布局 | idle 分布、查询等待、chunk 利用率 |
| Presentation/Telemetry | 可丢弃或帧内 | Boundary outbox、截断容器 | 不反向影响 Core、独立计时 |

## API 选型原则

### 遍历

- `SystemAPI.Query` 是可 Burst 的主线程遍历 API，并会完成必要依赖；它不是仅供 debug 的 API。
- `IJobEntity`、`IJobChunk` 或主线程遍历的选择依据工作量、依赖链、批处理机会和 Profiler，不使用固定 entity 数量分界。
- 随机 Lookup 有额外定位和缓存成本，但不把它描述为固定算法或固定倍率。

### 并行写入与确定性

- 普通 `ParallelWriter` 只保证并发安全，不保证调度顺序。
- `NativeStream` 的 bufferCount 是逻辑分区数量，不等于线程数。若逻辑分区和段内写入稳定，可按分区顺序读取；若业务需要全序，使用稳定 total key 和 tie-breaker 排序。
- 并行 ECB 使用 `EntityCommandBuffer.ParallelWriter` 和调度无关 sort key；跨 job 的业务全序不能只依赖 chunk index。

### 结构变化

- worker job 中不能直接执行结构变化，通常通过 ECB 延迟。
- “单一 playback phase”是 EX-GAS 可选的架构纪律，不是 Unity 唯一合法模型。
- 多个相同类型的主线程结构变化可以评估 EntityManager 的 query/bulk API；以实际同步和拷贝成本决定。

### Burst 与资源

- Editor Burst 使用 JIT 语境，Player 构建使用 AOT；性能报告必须记录运行环境。
- FunctionPointer 用 delegate，适合批处理粒度的动态分派；优先比较直接调用、Job、静态 switch 等更简单方案。
- `UnityObjectRef<T>` 与 `WeakObjectReference<T>` 生命周期不同；Runtime Core 不在 Burst hot path 解引用托管 Unity 对象。

## SEL 规则

| 编号 | EX-GAS 项目规则 |
|---|---|
| `SEL-01` | 先按 gameplay、transient、telemetry、presentation 分类，再选 API。 |
| `SEL-02` | Proof 方案必须显式标记，不自动升级为 scale-ready 方案。 |
| `SEL-03` | 每个 NativeContainer 声明 allocator、owner、释放或 rewind 时机。 |
| `SEL-04` | 重新选型条件使用可观测指标；固定阈值必须有项目基准证据。 |
| `SEL-05` | Singleton、Query 和 EntityManager API 按真实依赖完成语义选择，不使用“永不同步/必然同步”的绝对表述。 |

## 任务交还模板

```markdown
| 业务链路 | 数据性质 | 采用 API | 拒绝方案 | 官方机制依据 | 项目理由 | 指标/重选型条件 |
|---|---|---|---|---|---|---|
| ... | ... | ... | ... | QRY-xx / SC-xx | ... | ... |
```

## 关联主题

- [Query 与 Job](Query-Job-遍历/_index.md)
- [结构变化与 ECB](结构变化-ECB/_index.md)
- [DynamicBuffer 与 Chunk](DynamicBuffer-Chunk-Archetype/_index.md)
- [Enableable](Enableable-Component选型/_index.md)
- [NativeContainer 与 Allocator](NativeContainer-Allocator/_index.md)
- [Burst 与 AOT](Burst-AOT/_index.md)
- [确定性](Mathematics-确定性/_index.md)

## 验收

1. 每个 Runtime Core 任务交还 API 选型表。
2. 所有官方结论可追溯到精确版本来源；所有项目策略明确标注。
3. 性能结论分离 core、physics、render 和 runner 成本。
4. 固定阈值附目标平台、场景、测量方法和日期；否则只保留定性重选型条件。
