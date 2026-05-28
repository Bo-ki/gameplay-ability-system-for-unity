# 13 DOTS 编写规范与性能陷阱

## 职责

本主题从 Unity DOTS 官方文档中提取系统性编码规范，按性能影响严重度排序，每条规范附带：触发条件、为什么违规会导致性能退化、对应的 EX-GAS ISSUE 诊断证据、以及修复方向。

**P2 注意级规范（数据流、系统生命周期）见 [15-数据流-系统生命周期规范](15-数据流-系统生命周期规范.md)。**

## 严重度定义

| 严重度 | 含义 | 量化标准 | 修复优先级 |
|---|---|---|---|
| **P0-致命** | 违反会导致 O(n) 结构变化、archetype 爆炸、或主线程阻塞 | frame time 占比 > 20% 或 scale 时非线性暴涨 | 阻塞 AM 迁移，必须立即修复 |
| **P1-严重** | 违反会导致显著的 cache miss、sync point 堆积、或 job 并行度不足 | frame time 占比 5-20% | 当前迭代窗口修复 |
| **P2-注意** | 违反在当前规模下可控但 scale 时会恶化为 P1 | frame time 占比 < 5% | 达到重选型阈值前修复 |

---

## Part A: 结构与原型 — P0 致命级

### P0-01: 禁止用 Entity 表示临时/瞬时状态

**规则：** 每帧创建并随后销毁的 entity 表示临时状态（如 instant GameplayEffect、单帧 command、transient event），必须用 `DynamicBuffer`、`NativeStream` 或 `Enableable` 替代。

**来自官方文档：**
> `performance-chunk-allocations.html`: "Temporary addition and removal of components... store it in a dynamic buffer so you can add and remove them without changing the entity archetype"

**为什么致命：**
1. 每次 `CreateEntity` + `DestroyEntity` = 2 次结构变化 + 1 次 archetype 迁移
2. 不同的临时 entity 可能创建不同的 archetype（component 组合不同）
3. 每帧高频 create/destroy → entity churn → chunk 碎片化
4. 100,000 entity 各有独特 archetype → >1.5 GB chunk 浪费 + 每个 entity 间 cache miss

**EX-GAS 直接命中：ISSUE-001**
- Instant GE 走 `CApplyGameplayEffectRequest → runtime GE entity → lifecycle → destroy`
- x50 下 `GameplayEffectApplied=3479`、`AttributeChanges=2730`、`CueRequests=3466`
- `SEffectApply`、`SApplyGameplayEffectRequest`、`SEffectTick` 均为 top 热点

**修复方向：**
```
Instant GE → EffectCommand → InstantEffectSpec → AttributeDelta → TypedFact
（全程无 entity 创建）
```

**检查方法：**
- Debugger 输出 `entityCreated - entityDestroyed ≈ 0` 表示无 churn
- 若 `entityCreated` 随 game event 数量线性增长 → 违规

---

### P0-02: 禁止在 Hot Path 直接执行结构变化

**规则：** `ISystem.OnUpdate` 或 job 内直接调用 `EntityManager.CreateEntity`、`AddComponent`、`RemoveComponent`、`DestroyEntity` 是 P0 违规。所有结构变化必须通过 ECB 延迟到 playback phase。

**来自官方文档：**
> `performance-sync-points.md`: "Structural changes to the data in ECS are the primary cause of sync points"

**为什么致命：**
1. 每个结构变化触发 sync point → 主线程等待所有 worker 线程
2. 所有 TypeHandle/ComponentLookup/BufferLookup/DynamicBuffer 失效
3. 散落的结构变化使 ECB 合并优化失效

**EX-GAS 直接命中：ISSUE-004**
- 真实 Scene 曾暴露三类 `BufferTypeHandle invalidated by structural change` 错误
- `SApplyGameplayEffectRequest` 在同一系统中读 buffer 后创建/销毁 entity

**正确做法：**
```csharp
// 错误：hot path 直接结构变化
var buffer = SystemAPI.GetBuffer<BData>(entity);
EntityManager.CreateEntity();    // SYNC POINT + handle 失效
buffer.Add(...);                  // 安全系统抛异常！

// 正确：通过 ECB 延迟
var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
    .CreateCommandBuffer(state.WorldUnmanaged);
// ... in job:
ecb.CreateEntity();
ecb.AddComponent(...);
// ECB playback 在主线程统一执行，只产生一个 sync point
```

**检查方法：**
- 搜索代码中 `EntityManager.CreateEntity` / `DestroyEntity` / `AddComponent` / `RemoveComponent`
- 在 Debugger 中向违规 system 告警

---

### P0-03: 禁止用 Tag Component 做高频状态标记

**规则：** 每个 tag component（无论多小）使 archetype 排列数翻倍。高频 toggle 的状态标记必须用 `IEnableableComponent` 或 owner-local bitset。

**来自官方文档：**
> `performance-chunk-allocations.html`: "Each tag component multiplies the number of archetypes permutations you have by two. You can usually replace a tag component with an enableable component."

**为什么致命：**
- N 个独立 tag component → 最多 2^N 种 archetype 排列
- 10 个 tag → 最多 1024 种 archetype → 1024 × 16 KiB = 16 MB 仅 chunk header
- 50 个 tag → 天文数字
- 即使实际未达上限，每次 entity 获得/失去 tag = archetype 迁移

**检查方法：**
- Archetype 窗口检查：archetype 总数是否接近 entity 总数
- 审计所有 `IComponentData` struct 中无数据字段的 "marker" component
- 按 `ISSUE-004` 规范要求改为 enableable

---

### P0-04: 禁止 Sync Point 散落 —— 结构变化必须集中

**规则：** Frame backbone 中只允许一个（或一组连续排列的）ECB playback phase 做结构变化。所有需要结构变化的 system 必须使用该 phase 的 ECB。

**来自官方文档：**
> `performance-sync-points.md`: "Two systems that both make structural changes only create one sync point if they update sequentially, unless the first one also schedules jobs"

**为什么致命：**
- 每增加一个独立的 sync point = 增加 0.1-1.0ms 主线程阻塞
- 10 个分散 sync point = 1-10ms → 在 16.67ms 帧预算中致命
- EX-GAS 当前有多个散落的 entity create/destroy 来源

**EX-GAS 直接命中：ISSUE-009**
- 缺少统一 frame backbone
- `GasStructuralPlaybackSystemGroup` 设计已存在但尚未完全落地
- AM2B-A/B contract 已声明但 system 尚未全部迁移

**目标态：**
```
GasStructuralPlaybackSystemGroup（唯一结构变化点）
  ├── BeginGasStructuralECBSystem  (playback before core phases)
  └── EndGasStructuralECBSystem    (playback after core phases)
```

---

## Part B: Job 与查询 — P1 严重级

### P1-01: Hot Path 禁止主线程遍历

**规则：** Hot path（每帧执行、entity 数 > 100）禁止使用 `SystemAPI.Query` foreach 或 `Run()`。必须使用 `IJobEntity` 或 `IJobChunk` + Burst。

**来自官方文档：**
> `performance-sync-points.md`: "Sync points can also happen when you use Run to run a job, or when you use idiomatic foreach"
> `upgrade-guide.md`: `SystemAPI.Query` 可在不需要 job 的遍历中使用，并可在合适上下文 Burst 编译。

**为什么严重：**
- 主线程 foreach 前自动完成必要依赖（相关 job 未完成时等待）→ 再串行遍历
- 同时间内所有 worker 线程空闲
- 即使 `SystemAPI.Query` 被 Burst 编译，仍没有 worker-thread 并行度；hot path 主要损失是 dependency completion + 主线程串行遍历

**检查方法：**
- Grep 搜索 `SystemAPI.Query` 在 Runtime Core system 目录中的出现
- 标记为 proof-only 或要求迁移到 IJobEntity

---

### P1-02: 禁止高频 Random Access Lookup

**规则：** 高频路径中使用 `ComponentLookup.TryGetComponent` 或 `BufferLookup.TryGetBuffer` 做跨 entity 随机访问，其成本在规模下显著。应重构为 owner-local data 或顺序遍历。

**来自官方文档：**
> `components-enableable-use.html`: "Random-access methods have some additional overhead because they need to look up the target entity's data. When performance is a priority, use the iteration-based methods where possible."

**为什么严重：**
- 每次 lookup 都是哈希查找 + 内存随机访问
- 在 IJobEntity 的紧密循环中，一个 random lookup 的 cache miss 成本 ≈ 10-20 个顺序 entity 处理成本
- 百万 entity 遍历中 10% 做 random lookup ≈ 10 万次 cache miss

**修复方向：**
```
反模式: IJobEntity 遍历 source entity → ComponentLookup 查 target entity 属性
正确:   提前将 target 属性写入 source 的 owner-local buffer → 顺序读取
```

---

### P1-03: 避免不必要的 System 拆分

**规则：** 每个 System 有固定性能开销（TypeHandle 刷新 + Lookup 创建 + Dependency 链）。功能相近且共享 query 的系统应评估合并。

**来自官方文档：**
> `systems-optimizing.html`: "Every system has a fixed performance overhead... the CPU overhead of these accesses grows linearly with the number of active systems."

**三种固定开销：**
1. **EntityTypeHandle 副本获取**：每个 system 每次 OnUpdate 前获取。随 system 数量线性增长。
2. **ComponentLookup/BufferLookup 创建**：不同 system 可能重复创建相同 lookup。每帧需要更新。
3. **JobHandle Dependency 链**：更多 system → 更多 job → 更复杂的依赖计算链。

**EX-GAS 应用：**
- Debugger 应输出 `ActiveSystemCount`，按 `core/diagnostics/presentation/demo` 分组
- Runtime Core 每 phase 超过 5 个 system 时触发合并评估

---

### P1-04: 禁止 EntityIndexInQuery 在 Hot Path 使用

**规则：** `[EntityIndexInQuery]` 的内部实现调用 `CalculateBaseEntityIndexArray`，不是简单索引访问。高频遍历中避免使用。

**为什么严重：**
- 需要额外的内部数组构建步骤
- 不是 O(1) 简单索引
- 在 IJobEntity 的每次 Execute 中调用会显著增加开销

**替代方案：**
- 需要 per-entity 索引 → owner-local buffer 或 chunk-local counter
- 需要 array 对应 → 通过 entity 上的 DynamicBuffer 代替 parallel array

---

### P1-05: 注意 Query 操作的 Sync 触发

**规则：** 以下操作在有 enableable component 且写 job 未完成时触发 sync point：
- `CalculateEntityCount()`
- `ToEntityArray()` / `ToComponentDataArray()`
- `GetSingleton<T>()`（当 T 是 enableable 且有写 job 未完成）

**规避方法：**
- 使用 `IgnoreFilter` 结尾的变体 → 忽略 enableable 过滤 → 不需要 sync point
- 使用 `Async` 结尾的变体 → 调度异步 job → 不阻塞主线程
- 使用 `EntityQueryOptions.IgnoreComponentEnabledState` 构建 query

---

## Part C: 内存与 Chunk — P1 严重级

### P1-06: 监控 DynamicBuffer 溢出

**规则：** `DynamicBuffer` 默认容量 128 字节。溢出时数据外部化到 chunk 外。如果不监控，外部化比例可能随着 entity 数量线性上升而无法发现。

**为什么严重：**
- 外部化 buffer 每次访问多一次间接内存跳转
- 大量 entity 的外部化 buffer 导致缓存局部性丧失
- 静默性能退化——不会报错，但速度越来越慢

**来自官方文档：**
> `components-buffer-introducing.html`: "The pointer is initially null to signify that the data is in the chunk with the entity, and if Unity moves the data outside the chunk, the pointer is set to point to the new array."
> `components-buffer-set-capacity.md`: **"If Unity moves dynamic buffer data outside of a chunk, it never moves the data back into the chunk."** 即使后续 buffer 缩容到 capacity 以内，数据也不会迁回 chunk。浪费的 inline 空间在该 entity 生命周期内永久存在。可通过 `InternalBufferCapacity = 0` 始终外部化以避免迁移开销；`TrimExcess` 可减少 padded capacity 但不恢复 inline 存储。

**EX-GAS 要求：**
- 为每个关键 buffer 类型设定 `InternalBufferCapacity`
- Debugger 报告 `bufferExternalizedRatio`
- `externalizedCount / totalBufferCount > 30%` 触发告警

---

### P1-07: 控制 Prefab 数量 —— Prefab ≠ 定义数据

**规则：** 每个 prefab 占用至少 16 KiB 的 chunk（因为 Prefab component 使它们成为独立 archetype）。大量不同的 prefab 导致显著的内存浪费。静态定义数据应用 BlobAsset 或 generated static table 代替。

**来自官方文档：**
> `performance-chunk-allocations.html`: "each prefab occupies its own 16 KiB chunk... The memory overheads of these prefabs can add up quickly"

**EX-GAS 应用：**
- GE 定义不应是 prefab，而应是 BlobAsset 或 static data table
- 仅需要实体原型（如角色模型）的 entity 使用 prefab
- Debugger 应输出 prefab archetype 数量和总 chunk 内存

---

### P1-08: 审核 SharedComponent 使用

**规则：** `ISharedComponent` 值改变触发 entity 移动到新 chunk（结构变化）。使用前必须满足三条件：1) system 按 subgroup 操作有用 2) subgroup 数量少 3) 节省内存 > chunk 分裂浪费。

**来自官方文档：**
> `performance-chunk-allocations.html`: "As a rule, shared components are only useful if all the following statements are true..."

**替代：** Chunk Component 共享同值但不强制 chunk 分裂。

---

### P1-09: 禁止使用 IAspect（已废弃 API）

**规则：** `IAspect` 在 Entities 1.4.6 中已被官方标记为 deprecated，将在未来版本中移除。禁止在任何新代码中使用 `IAspect` 或 `readonly partial struct ... : IAspect`。已有使用 Aspect 的代码需在重构窗口替换为直接 component 访问和 query 方法。

**来自官方文档：**
> `aspects-intro.md` / `aspects-concepts.md`: "Aspects are deprecated and will be removed in a future release. Use component access and query methods directly instead."

**为什么严重：**
- 废弃 API 将在未来版本移除，代码将无法编译
- IAspect 生成的源代码依赖特定的代码生成器，跨版本兼容性不可靠
- 直接 component 访问是官方推荐的替代方案，不需要迁移负担
- 即使当前项目可能不更新 Entities 版本，让团队成员养成使用废弃 API 的习惯会增加未来升级成本

**EX-GAS 替换指南：**

```csharp
// 废弃方式（禁止）：
readonly partial struct CannonBallAspect : IAspect
{
    public readonly Entity Self;
    readonly RefRW<LocalTransform> Transform;
    readonly RefRW<CannonBall> CannonBall;
    public float3 Position { get => Transform.ValueRO.Position; set => Transform.ValueRW.Position = value; }
}
foreach (var cannonball in SystemAPI.Query<CannonBallAspect>()) { }

// 推荐替代（直接 component 访问）：
[BurstCompile]
partial struct CannonBallJob : IJobEntity
{
    void Execute(ref LocalTransform transform, ref CannonBall cannonBall)
    {
        var position = transform.Position;
        // ...
    }
}
```

**检查方法：**
- Grep 搜索 `IAspect`、`: IAspect`、`partial struct.*Aspect` 在 Runtime Core 和 AutoChess 代码中
- 新增代码 PR 必须零 `IAspect` 出现
- 已有 Aspect 使用在 `CASE-13` 中登记为待迁移

---

### P1-10: SimulationSystemGroup 期间 `LocalToWorld` 可能过期

**规则：** `LocalToWorld` component 的值在 `SimulationSystemGroup` 运行期间可能过期或无效。需要精确世界坐标用于 gameplay 计算时，必须使用 `TransformHelpers.ComputeWorldTransformMatrix`——不能直接读 `LocalToWorld`。

**来自官方文档：**
> `transforms-concepts.md`: "The `LocalToWorld` component value might be out of date or invalid while the `SimulationSystemGroup` is running. This is because the transform system only updates the component value when the `TransformSystemGroup` runs... you shouldn't rely on it if you need an accurate, up-to-date world transform for simulation purposes. In those cases, use the `ComputeWorldTransformMatrix` method."
>
> 同一文档还警告：`LocalToWorld` 可能包含 **"additional offsets applied for graphical smoothing purposes"**（图形平滑目的的额外偏移），即使 `TransformSystemGroup` 已运行过，存储的值仍可能不是实体真实世界坐标。

**为什么严重：**
1. `TransformSystemGroup` 的更新时机与 `SimulationSystemGroup` 不同步
2. 读取过期 `LocalToWorld` 导致目标位置、距离计算、范围判断等基于错误坐标
3. 额外保留 `LocalTransform` + `Parent` 信息才能正确递归计算世界坐标

**EX-GAS 直接关联：**
- AutoChess 目标获取（target acquisition）需要精确世界坐标
- 范围技能的距离判定（distance check）
- 物理查询的 input position

**修复方向：**
```csharp
// 错误：直接读可能过期的 LocalToWorld
float3 targetPos = localToWorld.Position;

// 正确：需要精确值时用 ComputeWorldTransformMatrix
var worldMatrix = TransformHelpers.ComputeWorldTransformMatrix(
    entity, ref localTransformLookup, ref parentLookup, ref postTransformLookup);
float3 targetPos = worldMatrix.Translation();
```

**检查方法：**
- Grep `LocalToWorld` 在 Runtime Core 或 Simulation system 中的直接 `.Position` / `.Rotation` 读取
- 判断该读取是否在 `SimulationSystemGroup`（非 `TransformSystemGroup`）中
- 若是 gameplay 决策依据 → 违规

---

### P1-11: `ComponentLookup` / `BufferLookup` 随机访问与 Job 数据重叠导致竞态

**规则：** 在 IJobEntity / IJobChunk 中使用 `ComponentLookup` 或 `BufferLookup` 随机访问其他 entity 的 component 时，若被访问数据与 job 直接遍历的 component type 重叠，会导致 race condition。必须确保随机访问的 entity 集合与直接遍历的 entity 集合无交集，否则标记 `[NativeDisableParallelForRestriction]` 并承担正确性责任。

**来自官方文档：**
> `systems-looking-up-data.md`: "If the data you look up overlaps the data you want to read and write to in the job, then random access might lead to race conditions."
> `common-errors.md`: "If you can guarantee that your access is safe, you can use the `NativeDisableParallelForRestriction` attribute to silence the error."

**为什么严重：**
1. 两个 worker 线程可能同时通过 lookup 和直接遍历访问同一 entity 的同一 component
2. ECS 安全系统能检测并行写入冲突，但不能检测"lookup 读 + 遍历写"的微妙竞态
3. 高频随机 lookup 成本远高于顺序遍历（cache miss × N）

**EX-GAS 直接关联：**
- Effect application 中 `ComponentLookup<BAttribute>` 跨 entity 读取 source ASC 属性
- ActiveEffect MMC 计算中 lookup source/target 属性
- Cue 投影中 lookup 表现状态

**修复方向：**
```
// 优先方案：owner-local buffer 替代跨 entity lookup
// 备选方案：确保 lookup entity 集合与遍历 entity 集合不同组
// 最后手段：[NativeDisableParallelForRestriction] + 注释证明无重叠
```

**检查方法：**
- Grep `ComponentLookup` / `BufferLookup` 在 `IJobEntity` / `IJobChunk` 中的使用
- 检查 lookup 的 entity 来源是否与 job 的 query 条件可能重叠
- 若无 `[NativeDisableParallelForRestriction]` 且 ECS 安全系统报警 → 确认竞态风险

---

### P1-12: IJobEntity Execute 参数与 EntityQuery 不匹配的未保护安全风险

**规则：** `IJobEntity` 不会验证 `Execute` 方法的参数是否与关联的 `EntityQuery` 匹配。参数不匹配不会产生编译错误或运行时警告，但会导致静默错误（访问错误数据或遗漏 entity）。必须手动保持两者同步，每次修改 query 或 Execute 签名后交叉验证。

**来自官方文档：**
> `concepts-safety.md`: "IJobEntity does not verify that the Execute method's parameters match the EntityQuery. You must manually keep them in sync."

**为什么严重：**
1. 编译器零检查 — 纯运行时静默错误，可能长时间不被发现
2. Execute 参数比 query 多 → job 不调度任何 entity（query 不含该 component 的 entity 被排除）
3. Execute 参数比 query 少 → 遗漏 component 写入，数据不一致
4. 重构 query 忘记同步 Execute 参数 → 引入隐蔽 bug，无任何警告

**EX-GAS 直接关联：**
- Runtime Core 中所有 `IJobEntity` 使用（参数复杂，重构风险高）
- Effect application job（多 component 读写）
- AutoChess demo 的 gameplay job

**正确做法：**
```csharp
// 错误：query 含 RefRO<BAttribute> 但 Execute 遗漏了该参数
// → 静默：job 不调度任何 entity（因为 Execute 参数要求 BAttribute 可写？不——
//   实际上如果 Execute 参数比 query "多"，query 不匹配的 entity 被静默跳过）
[BurstCompile]
partial struct MyJob : IJobEntity
{
    void Execute(ref BHealth health) // 遗漏了 in BAttribute
    {
        // BAttribute 永远不会被读取，且只有同时有 BHealth 的 entity 才被调度
        // 如果 query 是 WithAll<BHealth, BAttribute>，所有 entity 都被跳过
    }
}

// 正确：Query 和 Execute 参数一一对应
// Query: WithAll<BHealth, BAttribute>()
[BurstCompile]
partial struct MyJob : IJobEntity
{
    void Execute(ref BHealth health, in BAttribute attribute)
    {
        // 参数与 query 完全匹配
    }
}
```

**检查方法：**
- 每个 `IJobEntity` 旁注释列出对应的 `EntityQuery` 条件
- Code review 时交叉验证 query 和 Execute 参数
- 新增/删除 component 参数时同步检查 query 声明

---

### P1-13: 禁止从 Job 内部启动新 Job

**规则：** 禁止在任意 job 的执行上下文中（包括 `IJobEntity.Execute`、`IJobChunk.Execute`、`IJobParallelFor.Execute`、`Entities.ForEach` lambda）启动新的 job（`.Schedule()` 或 `.Run()`）。ECS 安全系统不会为嵌套 job 建立正确的 safety handle。

**来自官方文档：**
> `common-errors.md`: "Launching jobs from jobs is not currently supported, and the resulting safety handles won't be set up correctly. This includes obscure cases like using `.job.Run()` from inside of an `Entities.ForEach().Run()` lambda function, which is itself implemented as a main-thread job."

**为什么严重：**
1. 嵌套 job 的 safety handle 未正确建立 → 竞态条件无法被检测
2. 主线程 `.Run()` 内部再 `.Run()` 同样违规（`ForEach().Run()` 本身实现为 main-thread job）
3. 编译器不报错，运行时可能偶然正确 → 隐蔽的竞态 bug

**正确做法：**
```csharp
// 错误：在 job 内部启动另一个 job
Entities.ForEach((ref SomeData data) =>
{
    var result = new NativeArray<float>(1, Allocator.Temp);
    new MyOtherJob { ... }.Run();  // 违规！
}).Run();

// 正确：将两个 job 的顺序依赖显式管理
var handle1 = new MyFirstJob { ... }.ScheduleParallel(query, Dependency);
var handle2 = new MyOtherJob { ... }.Schedule(handle1);
Dependency = JobHandle.CombineDependencies(handle1, handle2);
```

**检查方法：**
- Grep `\.Schedule\(\)` / `\.Run\(\)` 出现在 `void Execute(` 或 lambda `() =>` 内部
- 特别注意 `Entities.ForEach().Run()` 内部的 `.Run()` 调用

---

### P1-14: 禁止逐 Component 构建 Entity Archetype —— 预创建 Archetype 批量创建

**规则：** 使用 `EntityManager.CreateEntity()` 后逐次调用 `AddComponent<T>()` 会在每次调用时创建新的中间 archetype。这些中间 archetype 在应用剩余生命周期内持续存在并增加所有 `EntityQuery` 的计算开销。必须预先用 `CreateArchetype()` 构建完整 archetype，再用 `CreateEntity(archetype, entities)` 批量创建。

**来自官方文档：**
> `optimize-structural-changes.md`: "Avoid adding components one at a time to construct entities at runtime. Calling EntityManager.AddComponent() creates a new archetype and moves the entity into a whole new chunk. The archetype exists for the rest of the runtime... You should create the archetype that describes the entity you want to end up with and then create an entity directly from that archetype."

**为什么严重：**
1. 每个 `AddComponent` = 一次结构变化 + 一个新 archetype
2. 中间 archetype 永久存在，每次 query 创建/更新都要遍历所有 archetype
3. N 个 component 逐个添加 = N-1 个冗余中间 archetype
4. 批量创建场景（AutoChess battle init）中，10000 entity × 3 component 逐个添加 = 20000 次结构变化 + 2 个冗余 archetype

**对比数据（1M entity 添加一个 component）：**
| 方法 | 耗时 |
|---|---|
| Enableable toggle | 0.03 ms |
| EntityManager.AddComponent(query) | 3.5 ms |
| ECB + IJobChunk per-entity | 17 ms |
| EntityManager.AddComponent(NativeArray) | 35 ms |
| ECB + IJobEntity per-entity | 170 ms |

**正确做法：**
```csharp
// 错误：逐 component 添加
for (int i = 0; i < 10000; i++)
{
    var entity = EntityManager.CreateEntity();
    EntityManager.AddComponent<A>(entity);  // archetype {A} 创建
    EntityManager.AddComponent<B>(entity);  // archetype {A,B} 创建, {A} 成为冗余
    EntityManager.AddComponent<C>(entity);  // archetype {A,B,C} 创建, {A,B} 成为冗余
}

// 正确：预建 archetype 批量创建
var archetype = EntityManager.CreateArchetype(typeof(A), typeof(B), typeof(C));
var entities = new NativeArray<Entity>(10000, Allocator.Temp);
EntityManager.CreateEntity(archetype, entities);
```

**EX-GAS 直接关联：**
- AutoChess battle 初始化（批量棋子创建）
- Effect 批量应用时的 entity 创建
- Definition 加载期的 entity 预分配

**检查方法：**
- 搜索 `CreateEntity()` 后紧跟 `AddComponent` 的模式
- Archetype 窗口检查冗余 archetype 数量

## EX-GAS 问题 → 规范映射矩阵

| EX-GAS ISSUE | 对应的规范 | 严重度 | 当前状态 |
|---|---|---|---|
| **ISSUE-001** GE 生命周期管线过重 | P0-01 禁止 Entity 表示临时状态, P0-02 禁止 Hot Path 结构变化 | P0 | AM3 已部分迁移，AM5 进行中 |
| **ISSUE-004** 结构变化边界脆弱 | P0-02 禁止 Hot Path 结构变化, P0-04 Sync Point 集中化 | P0 | Phase contract 已声明，system 未完全搬迁 |
| **ISSUE-002** Observation 热路径耦合 | P1-01 Hot Path 主线程遍历, P1-02 高频 Random Access | P0 | EventBus 承担多职责，需分离 |
| **ISSUE-009** Frame Backbone 缺失 | P0-04 Sync Point 集中化, P1-03 避免不必要 System 拆分 | P0 | AM2B-A/B contract 已编写，backbone 未形成 |

---

## 检查清单

每个 Runtime Core 任务交还前必须通过以下检查：

### 结构变化（P0）
- [ ] 无直接 `EntityManager.CreateEntity/DestroyEntity/AddComponent/RemoveComponent` 在 hot path
- [ ] 所有结构变化通过 `GasStructuralPlaybackSystemGroup` 的 ECB
- [ ] `entityCreated` 和 `entityDestroyed` 计数不随事件数线性增长
- [ ] Instant GE 路径不创建 runtime entity

### 查询与 Job（P1）
- [ ] Hot path 无 `SystemAPI.Query` foreach / `Run()`
- [ ] 使用 `IJobEntity` 或 `IJobChunk` + Burst
- [ ] Query contract 写清 All/Any/None/Disabled/ChangeFilter
- [ ] 无 `EntityIndexInQuery` 在高频路径
- [ ] 高频 random lookup 已替换为 owner-local buffer 或顺序遍历
- [ ] 零 `IAspect` 使用；已有 Aspect 已登记为待迁移
- [ ] `LocalToWorld` 在 `SimulationSystemGroup` 中不直接读 `.Position`/`.Rotation`；精确坐标用 `ComputeWorldTransformMatrix`
- [ ] `ComponentLookup`/`BufferLookup` 随机访问与遍历 entity 集合无重叠，或有 `[NativeDisableParallelForRestriction]` + 安全证明
- [ ] 每个 `IJobEntity` 的 Execute 参数与 EntityQuery 条件手动交叉验证通过（P1-12）
- [ ] 无 Job 内启动 Job（含 `.Run()` 内部调用 `.Schedule()`/`.Run()`）（P1-13）

### Chunk 与内存（P1）
- [ ] Buffer capacity 声明 + externalized 监控到位
- [ ] 无高频 Add/Remove tag component（改用 enableable）
- [ ] Prefab 数量审计（避免用于数据定义）
- [ ] SharedComponent 使用有明确必要性说明
- [ ] Entity 创建前预建 Archetype；禁止逐 component 添加构建 entity（P1-14）
- [ ] 批量添加/移除多个 component 用 `ComponentTypeSet` 一次完成（CASE-33）

> **P2 级检查项（Allocator/ECB/Job/Singleton）见 [15-数据流-系统生命周期规范](15-数据流-系统生命周期规范.md)**

---

## 官方证据索引

| 官方文档 | 关联规范 |
|---|---|
| `performance-chunk-allocations.html` | P0-01, P0-03, P1-06, P1-07, P1-08 |
| `performance-sync-points.md` | P0-02, P0-04, P1-01, P1-05 |
| `concepts-archetypes.html` | P0-01, P0-03 |
| `systems-optimizing.html` | P1-03, P2-05, P2-06 |
| `systems-update-order.html` | P2-04 |
| `components-enableable-use.html` | P0-03, P1-02, P1-05 |
| `components-buffer-introducing.html` | P1-06 |
| `transforms-concepts.md` | P1-10 |
| `systems-looking-up-data.md` | P1-11 |
| `systems-version-numbers.md` | CASE-18 |
| `transforms-usage-flags.md` | CASE-19 |
| `concepts-safety.md` | P1-12, P2-07 |
| `iterating-data-ijobchunk.md` / `JobChunkExamples.cs` | CASE-26, CASE-27, P2-08 |
| `components-chunk-use.md` / `ChunkComponentExamples.cs` | CASE-28 |
| `transforms-custom.md` / `TransformsCustom.cs` | CASE-29 |
| `common-errors.md` | P1-12, P1-13 |
| `optimize-structural-changes.md` | P1-14, CASE-33, CASE-34 |
| `state-machine.md` | FSM-01..06 |
| `systems-entity-command-buffers.md` | P2-09 |
| `systems-data-granularity.md` | P2-10 |
| `systems-version-numbers.md` | PRF-27 |
| `baking-phases.md` | CASE-39 |
| `transforms-using.md` | PRF-28 |
| `baking-baker-overview.md` | CASE-40 |
| `components-singleton.md` | PRF-29 |
| `baking-baking-systems-overview.md` | CASE-41 |

| `systems-systemapi.md` | SEL-05（SystemAPI.GetSingleton 不 sync vs EntityManager.GetComponentData 触发 sync） |
| `streaming-meta-entities.md` | CASE-44（Section meta entity 自定义元数据） |
| `systems-optimizing.md` | `[RequireMatchingQueriesForUpdate]` vs 手动 `if` 开销对比 |
| `systems-data.md` | CASE-45（系统级数据存为 component 而非 system 字段）、`UnityObjectRef<T>` 托管对象引用 |
| `systems-systemapi-query.md` | CASE-46（`SystemAPI.Query` 不可存储复用）、PRF-30（`DynamicBuffer<T>` 默认读写限制） |
| `systems-entitymanager.md` | EntityManager 三种方法可用在 SystemAPI.Query foreach（CreateEntity/CreateArchetype/Instantiate） |
| `systems-entityquery-filters.md` | Change filter chunk 级触发于 write access 声明（非实际数据变更）—— PRF-26 关键依据 |
| `transforms-comparison.md` | PRF-31（Child Buffer 迭代顺序不确定）、`PostTransformMatrix` 非均匀缩放、`eulerAngles`/`hasChanged`/sibling order 无 ECS 等效 |
| `job-overhead.md` | PRF-32（主线程 `.Run()` Job 有额外 job dep system 开销 — 主线程优先 foreach） |
| `iterating-data-ijobchunk-implement.md` | `CalculateBaseEntityIndexArrayAsync` per-entity 索引、type handle 每帧更新、`[DeallocateOnJobCompletion]` |
| `common-errors.md` | PRF-33（EntityQuery 必须通过 SystemState.GetEntityQuery 创建，禁止 EntityManager 创建）、`partial` 关键字必要（system 和 IJobEntity）、`[NativeDisableContainerSafetyRestriction]` 安全抑制场景 |
| `components-nativecontainers.md` | PRF-34（NativeContainer 放在 component 上时禁止对该 component 调度 IJobChunk/IJobEntity） |

---

## 常见陷阱速查

1. **"我用 ECB 就能随便创建 entity"** — ECB 仍然产生结构变化，只是延迟合并。Instant GE 创建 entity 即使通过 ECB 也不应成为默认路径。
2. **"System 多了没关系"** — 每个 system 有三种固定开销。100 个 system = 100 × 三种开销。不需要的拆分就是成本。
3. **"Enableable 替换 Add/Remove 就没问题了"** — Enableable 查询仍有成本（enableable 过滤需要 sync point）。高频 enableable 写 job + 同步 query = 仍有 sync point。
4. **"Buffer 反正能用，溢出无所谓"** — 溢出是静默退化。从 cache-friendly inline 变为 pointer chase，但不会报错。必须监控。
5. **"Prefab 就是配置数据"** — Prefab 在 ECS 中是重型对象（16 KiB chunk）。静态数据应该是 BlobAsset。
6. **"LocalToWorld 就是实体世界坐标"** — 在 `SimulationSystemGroup` 运行期间 `LocalToWorld` 可能过期或包含图形平滑偏移。需要精确 gameplay 坐标时必须用 `ComputeWorldTransformMatrix` 递归计算。
7. **"ComponentLookup 随机访问没问题"** — 若 lookup 目标 entity 与 job 直接遍历的 entity 重叠，会产生竞态条件。ECS 安全系统不总能检测此竞态（读-读模式不会报警）。
8. **"IJobEntity 参数对了就行"** — IJobEntity 不验证 Execute 参数与 EntityQuery 匹配。重构 query 后忘记同步 Execute 参数 → 静默错误，不会产生任何编译或运行时警告。

> **P2 级常见陷阱（Enableable Mask / ECB 复用 / 数据分离 / Update() 调用 / Child/Parent / Singleton 竞态）见 [15-数据流-系统生命周期规范](15-数据流-系统生命周期规范.md)**

---

## 验收指标

1. 所有 P0 规范在 Runtime Core hot path 中零违规
2. Debugger 输出按规范分类的性能指标（结构变化、sync point、buffer externalized、archetype count）
3. AM 迁移任务交还前通过本检查清单
4. 每类规范违规有明确的修复任务和 deadline
5. 新加入的 system/feature 自动经过规范检查（CI/lint 或 Debugger 验证）
