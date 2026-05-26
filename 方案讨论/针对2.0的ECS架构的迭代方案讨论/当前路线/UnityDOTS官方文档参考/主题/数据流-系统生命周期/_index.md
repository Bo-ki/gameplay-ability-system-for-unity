# 数据流-系统生命周期

## 职责边界

覆盖 Unity Entities 官方关于数据流确定性、Job 依赖管理、System 生命周期交互的 P2 注意级规范。包括：并行写入确定性、Enableable Mask 安全处理、ECB 独立实例化、读写数据分离、System Update() 调用禁令、Singleton 竞态风险、DynamicBuffer 默认读写陷阱、EntityQuery 创建约束等。

本主题不覆盖 P0/P1 hot path 致命级规范（见 `System-World-SystemGroup/_index.md` 和 `结构变化-ECB/_index.md`），不覆盖 Query/Job 调度细节（见 `Query-Job-遍历/_index.md`）。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解数据流确定性三层保障和系统生命周期契约）
2. **核心规范**（按严重度）
   - `PRF-13: 确定性输出不得依赖无序写入.md` — P0: 确定性输出不得依赖无序写入
   - `PRF-19: ComponentLookup_BufferLookup 随机访问与 Job 数据重叠导致竞态.md` — P1: ComponentLookup/BufferLookup 随机访问竞态
   - `PRF-22: IJobChunk 中 Enableable Mask 处理.md` — P1: IJobChunk 中 Enableable Mask 处理
   - `PRF-25: 每个并行 Job 使用独立 ECB.md` — P1: 每个并行 Job 使用独立 ECB
   - `PRF-26: 读写数据分离到不同 Component.md` — P1: 读写数据分离到不同 Component
   - `PRF-27: 禁止手动调用其他数据处理 System 的 Update().md` — P1: 禁止手动调用其他 System 的 Update()
   - `PRF-29: Singleton API 不自动完成 Job 依赖.md` — P1: Singleton API 不自动完成 Job 依赖
   - `PRF-32: 主线程数据操作禁用 .Run() Job.md` — P1: 主线程数据操作禁用 .Run() Job
   - `PRF-33: EntityQuery 必须通过 SystemState.GetEntityQuery 创建.md` — P1: EntityQuery 必须通过 SystemState.GetEntityQuery 创建
   - `PRF-30: SystemAPI.Query 中 DynamicBuffer<T> 默认读写.md` — P2: SystemAPI.Query 中 DynamicBuffer 默认读写
3. **模式与案例**
   - `CASE-14: Write Group 写保护.md` — Write Group 写保护
   - `CASE-15: Cleanup Component 生命周期.md` — Cleanup Component 生命周期
   - `CASE-27: chunk.Has<T>() Chunk 级可选组件检查.md` — chunk.Has<T>() Chunk 级可选组件检查
   - `CASE-31: DependsOn() 在 Early-Out 之前.md` — DependsOn() 在 Early-Out 之前
   - `CASE-38: IJobEntityChunkBeginEnd Chunk 预评估.md` — IJobEntityChunkBeginEnd Chunk 预评估
   - `CASE-45: System-Associated Entity Data.md` — System-Associated Entity Data（Primary Owner）
   - `CASE-46: SystemAPI.Query 不可存储复用.md` — SystemAPI.Query 不可存储复用
4. **拓展阅读**（按需）
   - ECB 结构变化集中化 → `结构变化-ECB/_index.md`
   - Query/Job 调度细节 → `Query-Job-遍历/_index.md`
   - System 类型与排序 → `System-World-SystemGroup/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | 数据流确定性 + 系统生命周期契约详解 |
| `PRF-13: 确定性输出不得依赖无序写入.md` | 规范 P0 | 确定性输出不得依赖无序写入 |
| `PRF-19: ComponentLookup_BufferLookup 随机访问与 Job 数据重叠导致竞态.md` | 规范 P1 | ComponentLookup/BufferLookup 随机访问竞态 |
| `PRF-22: IJobChunk 中 Enableable Mask 处理.md` | 规范 P1 | IJobChunk 中 Enableable Mask 处理 |
| `PRF-25: 每个并行 Job 使用独立 ECB.md` | 规范 P1 | 每个并行 Job 使用独立 ECB |
| `PRF-26: 读写数据分离到不同 Component.md` | 规范 P1 | 读写数据分离到不同 Component |
| `PRF-27: 禁止手动调用其他数据处理 System 的 Update().md` | 规范 P1 | 禁止手动调用其他 System 的 Update() |
| `PRF-29: Singleton API 不自动完成 Job 依赖.md` | 规范 P1 | Singleton API 不自动完成 Job 依赖 |
| `PRF-30: SystemAPI.Query 中 DynamicBuffer<T> 默认读写.md` | 规范 P2 | SystemAPI.Query 中 DynamicBuffer 默认读写 |
| `PRF-32: 主线程数据操作禁用 .Run() Job.md` | 规范 P1 | 主线程数据操作禁用 .Run() Job |
| `PRF-33: EntityQuery 必须通过 SystemState.GetEntityQuery 创建.md` | 规范 P1 | EntityQuery 必须通过 SystemState.GetEntityQuery 创建 |
| `CASE-14: Write Group 写保护.md` | 模式 | Write Group 写保护 |
| `CASE-15: Cleanup Component 生命周期.md` | 模式 | Cleanup Component 生命周期 |
| `CASE-27: chunk.Has<T>() Chunk 级可选组件检查.md` | 模式 | chunk.Has<T>() Chunk 级可选组件检查 |
| `CASE-31: DependsOn() 在 Early-Out 之前.md` | 模式 | DependsOn() 在 Early-Out 之前 |
| `CASE-38: IJobEntityChunkBeginEnd Chunk 预评估.md` | 模式 | IJobEntityChunkBeginEnd Chunk 预评估 |
| `CASE-45: System-Associated Entity Data.md` | 模式 | System-Associated Entity Data（Primary Owner） |
| `CASE-46: SystemAPI.Query 不可存储复用.md` | 模式 | SystemAPI.Query 不可存储复用 |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `PRF-13` | P0 | 确定性输出不得依赖无序写入 | 搜索 ParallelWriter 输出路径→确认是否影响 replay/hash |
| `PRF-19` | P1 | ComponentLookup 随机访问竞态 | Grep ComponentLookup/BufferLookup 在 job 中的使用，检查 entity 集合重叠 |
| `PRF-22` | P1 | IJobChunk Enableable Mask 处理 | Grep IJobChunk.Execute：无 ChunkEntityEnumerator 且无 Assert.IsFalse → bug |
| `PRF-25` | P1 | 每个并行 Job 使用独立 ECB | 审计 OnUpdate 中 CreateCommandBuffer 调用次数与 job 数量的对应关系 |
| `PRF-26` | P1 | 读写数据分离到不同 Component | 审计 IComponentData 中运行时变与不变字段 |
| `PRF-27` | P1 | 禁止手动调用其他 System Update() | Grep .Update() 调用在 OnUpdate 方法内 |
| `PRF-29` | P1 | Singleton API 不自动完成 Job 依赖 | 搜索 GetSingletonRW 调用，检查前置 CompleteDependencyBeforeRW |
| `PRF-30` | P2 | DynamicBuffer 在 Query 中默认读写 | 搜索 SystemAPI.Query<DynamicBuffer< 判断 buffer 是否只读 |
| `PRF-32` | P1 | 主线程操作禁用 .Run() Job | 搜索 .Run() 在 Runtime Core 中的使用 |
| `PRF-33` | P1 | EntityQuery 必须通过 GetEntityQuery | Grep EntityManager.CreateEntityQuery → 每个都是违规 |

## 跨主题引用

本主题规则被以下主题引用：

| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `PRF-13` | 结构变化-ECB | ECB sortKey 确定性回放 |
| `PRF-25` | 结构变化-ECB | 独立 ECB 实例化 |
| `PRF-26` | Query-Job-遍历 | 读写分离与 chunk 变更标记 |
| `PRF-27` | System-World-SystemGroup | 禁止手动 Update() |

其他主题拥有的规则（本主题引用但不拥有）：

| 规则 | 拥有者 | 使用场景 |
|------|--------|----------|
| `SYS-01` | System-World-SystemGroup | 权威计算落在 ECS System |
| `SYS-02` | System-World-SystemGroup | SystemGroup phase owner |
| `SC-01` | 结构变化-ECB | Hot path 禁止结构变化 |
| `PRF-02` | 结构变化-ECB | 禁止直接结构变化 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| 结构变化和 ECB 的集中化设计 | `结构变化-ECB/_index.md` |
| Query/Job 调度细节 | `Query-Job-遍历/_index.md` |
| System 类型选择与排序 | `System-World-SystemGroup/_index.md` |
| Enableable Component 选型 | `Enableable-Component选型/_index.md` |

## 验收指标

1. 所有 IJobChunk 实现已审计：有 enableable 的用 `ChunkEntityEnumerator`，无 enableable 的用 `Assert.IsFalse(useEnabledMask)`（PRF-22）
2. 每个并行 job 使用独立 ECB 实例；无复用 ECB 的代码（PRF-25）
3. IComponentData 中只读字段与读写字段已分离到不同 component（PRF-26）
4. 零处手动调用其他数据处理 System 的 Update()（PRF-27）
5. 每处 `GetSingletonRW` 调用有明确的依赖完成声明或 NativeContainer 包装（PRF-29）
6. 只读 `DynamicBuffer<T>` 场景未使用默认的 `SystemAPI.Query<DynamicBuffer<T>>`（PRF-30）
7. 零处使用 `.Run()` 在主线程处理 entity 数据操作（PRF-32）
8. 零处使用 `EntityManager.CreateEntityQuery`（PRF-33）
9. 确定性输出路径（EffectCommand fan-in、TypedFact projection）使用 sortKey 确定性 merge（PRF-13）
10. `ComponentLookup` / `BufferLookup` 随机访问与遍历 entity 集合已确认无重叠或正确标记（PRF-19）
