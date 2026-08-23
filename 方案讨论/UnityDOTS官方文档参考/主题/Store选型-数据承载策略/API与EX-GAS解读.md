# Store 选型 / 数据承载策略：API 与 EX-GAS 解读

**适用版本**：Unity `6000.3.14f1`；Entities `1.4.6`；Collections `2.6.6`

## 定位

Gameplay / transient / telemetry / presentation 是 **EX-GAS 的数据分类框架**，不是 Unity 官方定义的四种 Store。Unity 官方文档只裁决各容器、组件和生命周期机制；项目必须根据 owner、生命周期、并发模型、顺序语义和访问方式完成选型。

## 分类框架

### Gameplay state

跨帧、影响 gameplay/replay 的可变权威状态。常见承载是 per-entity `IComponentData`、owner-local DynamicBuffer、singleton component/buffer，或有明确 Persistent owner 的 NativeContainer。不是“必须直接在 chunk 内”；选择取决于访问模式和生命周期。

### Gameplay transient

帧内或 phase 内生产/消费、但会影响 gameplay 结算的记录，例如 EffectCommand、AttributeDelta。它们虽然短命，仍必须满足确定性、依赖和丢失策略，不能写成“不需要确定性”。

并行 fan-in 可使用 NativeStream 或其他并行容器；单 writer 可使用 DynamicBuffer/NativeList。容器不自动赋予确定性，消费顺序必须由稳定逻辑 buffer 映射、stable ranges 或 total-key sort 定义。

### Telemetry

不反馈 gameplay 的诊断/统计。可使用有容量上限的 NativeContainer 或 managed storage；allocator 由真实生命周期决定，不强制所有 telemetry 都使用 Persistent NativeList。采样和导出不能给热路径引入不可接受的同步。

### Presentation

只从 gameplay 向表现层输出、不会反向影响权威状态的请求。可以丢弃/合并/降频到什么程度必须由具体产品语义声明，不能一概写“允许丢一帧”。承载可为 DynamicBuffer、NativeStream、ECB 命令或边界 managed queue。

只读定义数据（BlobAssetReference/generated table）是横跨上述分类的共享输入，不应硬塞入某个运行时 Store 类别。Blob lifetime 由创建/烘焙方式与 owner 管理，不统一等于 World 生命周期。

## 选型维度

选型必须依次回答：

1. 谁拥有数据，谁负责 Dispose/Clear/Destroy？
2. 数据有效到何时：job、phase、frame、entity、World 还是进程？
3. 单 writer、多个独立 writer，还是共享并行 append？
4. 按 owner 连续访问、按 Entity 随机查找、按 key 索引，还是全量扫描？
5. 输出顺序是否影响 gameplay/replay？若影响，稳定全序键是什么？
6. 数据是否需要结构变化、是否必须本帧立即可见？

| 承载方式 | 主要生命周期 | 典型访问 | 关键限制 |
|---|---|---|---|
| `IComponentData` | entity | query/chunk、lookup | 组件类型变化是结构变化 |
| `DynamicBuffer<T>` | entity | owner-local 变长集合 | 容量外部化；具体引用在结构变化后失效 |
| `NativeStream` | 显式 allocator | 多逻辑 buffer append/read | 固定 ForEachCount；一 buffer 一 writer；先写后读 |
| `NativeList/Map` | 显式 allocator | 临时集合/索引 | ParallelWriter 不保证物理写入顺序 |
| ECB | playback 前 | 延迟实体命令 | 不是通用数据 Store；sort key 只定义 playback 排序 |
| BlobAssetReference | 由创建/烘焙 owner 决定 | 只读共享 | 不承载可变 gameplay state |
| Chunk component | chunk | per-chunk 元数据 | 添加/移除是结构变化；chunk identity 不稳定 |

## 确定性 merge

影响 battle hash/replay 的顺序敏感输出必须建立稳定全序，不能只按 target 排序。

```text
(Phase,
 TargetStableId,
 SourceStableId,
 CommandKind,
 ProducerSequence)
```

键必须唯一或定义所有相等项的 tie-breaker。`Entity.Index`、chunk index、worker index 和 NativeStream buffer index 不是天然的跨运行业务稳定 ID。若操作可交换且可结合，也可以通过数学证明消除排序；浮点加法通常不能直接满足该条件。

## NativeStream 消费示例

```csharp
NativeStream.Reader reader = commandStream.AsReader();
for (int bufferIndex = 0; bufferIndex < reader.ForEachCount; bufferIndex++)
{
    int itemCount = reader.BeginForEachIndex(bufferIndex);
    for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
    {
        sortedCommands.Add(reader.Read<EffectCommand>());
    }
    reader.EndForEachIndex();
}

sortedCommands.Sort(new EffectCommandTotalOrderComparer());
```

旧示例中的 `NativeStream ParallelWriter` 与 `commandStream.AsReader(i)` 不是 Collections 2.6.6 API。NativeStream 使用 `Writer`/`Reader`，并通过 `BeginForEachIndex` 选择逻辑 buffer。

## EX-GAS 当前方向

- ActiveEffect：owner-local 跨帧状态，可用 DynamicBuffer slot；容量由数据分布决定。
- EffectCommand/AttributeDelta/Fact：gameplay transient；并行生产时使用稳定 NativeStream 映射或并行容器，消费前建立业务顺序。
- Telemetry：与 gameplay 隔离、有明确容量/采样预算，不写入 battle hash。
- Cue/Presentation：单向输出；丢弃、合并和时延策略由具体 cue 语义声明。
- 所有 Store 在类型文档或邻近 owner 文档中声明 owner、lifetime、access path、ordering 和 teardown；不要求把全部信息硬塞进类型名。

## 官方证据边界

| 官方文档 | 能支持的结论 |
|---|---|
| Entities `components-buffer-introducing.md` | DynamicBuffer 的 entity 归属、容量和结构变化失效 |
| Entities `components-nativecontainers.md` | component 嵌套 NativeContainer 的 job 调度限制 |
| Entities `systems-entity-command-buffer-playback.md` | ECB sort key 定义 playback 顺序；并行录制需 ParallelWriter |
| Entities `performance-chunk-allocations.md` | 临时 Add/Remove、archetype/chunk 碎片化风险 |
| Collections `parallel-readers.md` | 普通 ParallelWriter 顺序不确定；NativeStream 可隔离并行 buffer |

数据四分类、battle total key、Store 命名和具体容器映射均为 EX-GAS 设计规范。
