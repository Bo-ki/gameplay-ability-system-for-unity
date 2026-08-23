# DynamicBuffer-Chunk-Archetype：API 与 EX-GAS 解读

**适用版本**：Unity `6000.3.14f1`；Entities `1.4.6`

## Archetype 与 chunk

同一 World 中组件类型组合完全相同的 entity 共享一个 archetype。Archetype 在 World 销毁时才销毁；其 chunk 按需创建，并在最后一个 entity 离开后销毁。

Entities 1.4.6 的普通 archetype chunk 为 16 KiB，每个 chunk 最多 128 个 entity，实际容量由 Entity ID、各组件数组、buffer header/inline data 和 chunk 元数据共同决定。entity 离开 chunk 时，最后一个 entity 会被移动来填补空位，因此不能依赖 chunk 内 index 持久稳定。

碎片化需要通过 Archetypes Window/Profiler 判断，常见来源是：

- 大型 entity 导致 chunk capacity 很低；
- shared component 值过多，把同 archetype entity 分散到许多 chunk；
- 临时 Add/Remove 或大量 tag 组合制造过多 archetype；
- 加载大量不同 archetype 的 prefab，每个 prefab archetype 可能占据自己的 16 KiB chunk。

“100,000 个 entity 各有唯一 archetype会超过 1.5 GB”是官方用于说明极端碎片化的例子，不能拿来证明所有 transient entity 都会创建独立 archetype。

## DynamicBuffer 容量

DynamicBuffer 的默认内部容量是能放入 128 字节的元素数量；`[InternalBufferCapacity(N)]` 用于覆盖默认值。默认策略本身合法，不要求每个 `IBufferElementData` 都机械添加属性。

当 Length 超过内部容量时，Unity 在 chunk 外分配数组并复制数据。此后即使 Length 缩小，数据也不会自动迁回 chunk；`TrimExcess` 可以缩减外部 capacity，但不会恢复 inline 存储。容量频繁剧烈变化时，官方建议考虑 `InternalBufferCapacity(0)`，避免为很少使用的 inline 区域降低 chunk capacity。

容量决策需要同时权衡：

- 内联命中率与外部间接访问；
- 为所有 entity 预留 inline bytes 导致的 chunk capacity 降低；
- 典型/峰值长度、增长频率和访问热度。

不存在官方“externalized ratio 超过 30% 必须告警”的阈值。EX-GAS 可以设置项目告警，但必须由具体 buffer、平台和规模基准校准。

## 结构变化后的失效范围

官方明确要求：任何结构变化后，先前取得的 `DynamicBuffer<T>` 以及由它取得的 ref/pointer 都可能失效，必须重新获取。

`BufferLookup<T>` 和 `BufferTypeHandle<T>` 是不同层次的缓存对象：可以存为 system 字段，并在调度前调用 `.Update(ref state)` 刷新。不要把它们写成“每次结构变化都必须 new/重新 Get”；也不要继续使用结构变化前已经从 lookup/accessor 取出的 `DynamicBuffer<T>`。

## Job 访问

DynamicBuffer 由 ECS safety/dependency 系统管理，不需要像独立 NativeContainer 那样由业务代码 Dispose。但它仍受读写依赖、并行写限制和结构变化失效规则约束，并非“没有 NativeContainer 的任何 job 限制”。

`IJobChunk` 通过 `BufferTypeHandle<T>` 和 `ArchetypeChunk.GetBufferAccessorRO/RW` 批量访问本 chunk 的 buffers；跨 entity 随机访问使用 `BufferLookup<T>`。

## Chunk Component

Chunk component 每个 chunk 存一份值。**设置已有 chunk component 的值**不移动普通 entity；但**添加或移除 chunk component**会改变 entity archetype，是结构变化。它不会像 shared component 那样因每个不同值自动拆分 chunk，但 component 类型组合仍参与 archetype/查询设计，不能说“永远不增加 archetype 排列”。

Chunk component 的值跟随物理 chunk，不适合保存需要跨 chunk 重排稳定、逐 entity 精确归属或参与跨运行 battle hash 的状态。

## ComponentTypeSet 与批量创建

已知最终组件集合时，创建 archetype 后批量 `CreateEntity`，避免逐 entity、逐 component 经过多个中间 archetype。运行时需要同时 Add/Remove 多个组件时，`ComponentTypeSet` 可在一次 API 调用中处理多个类型，减少结构变化和冗余 archetype。

```csharp
var types = new ComponentTypeSet(typeof(A), typeof(B), typeof(C));
entityManager.AddComponent(entity, types);
```

是否用 EntityManager、query bulk API 或 ECB，仍取决于是否需要立即生效、调用线程和现有 sync point。

## DynamicBuffer.Reinterpret

`Reinterpret<U>()` 只检查源/目标元素大小相同，不检查语义兼容。不同大小不是“编译失败”，而是在运行时检查中失败。reinterpret 后的 buffer 与原 buffer 别名同一内存并共享 safety handle。

不要把 `DynamicBuffer<byte>` 随意 reinterpret 为多字节 struct；元素大小必须相同。若要把 byte 序列解析成 struct，需要另一套明确对齐、边界和序列化方案。

## EX-GAS 项目策略

- ActiveEffect slot 作为 owner-local DynamicBuffer 是候选物理形态，最终 `InternalBufferCapacity` 由实测长度分布与 chunk capacity 决定，不预设 16/64 为官方值。
- 高并发 fan-in 不直接并行写同一个 singleton DynamicBuffer；使用 NativeStream、可并行容器后确定性 merge，或单 writer 分发。
- Instant GE 仅在没有独立 identity/跨帧 lifecycle/query 需求时走 command/value 路径；不能把“entity 表示瞬时状态”一概判错。
- Debugger 观察关键 buffer 的 Length/Capacity 分布、外部化估计、chunk capacity/unused bytes；告警阈值全部标注为项目基准。

## 官方证据

| 官方文档（Entities 1.4.6） | 可裁决结论 |
|---|---|
| `concepts-archetypes.md` | archetype/chunk 生命周期、16 KiB、数组布局、swap-back |
| `performance-chunk-allocations.md` | 最大 128 entity、三类碎片化、prefab/tag/临时 Add-Remove 风险 |
| `components-buffer-introducing.md` | 默认内部容量、DynamicBuffer 引用在结构变化后失效 |
| `components-buffer-set-capacity.md` | 外部化不自动迁回、`InternalBufferCapacity(0)` 场景 |
| `components-buffer-jobs.md` | BufferLookup 缓存与每次更新前 `.Update` |
| `components-chunk-use.md` | 添加/移除 chunk component 是结构变化；值读写 API |
| `components-buffer-reinterpret.md` | 仅按元素大小检查、内存别名和共享 safety handle |
| `optimize-structural-changes.md` | archetype 批量创建、ComponentTypeSet 与 Profiler 决策 |
