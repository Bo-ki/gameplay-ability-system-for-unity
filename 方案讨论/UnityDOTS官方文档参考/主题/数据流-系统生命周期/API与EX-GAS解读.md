# 数据流-系统生命周期: API 与 EX-GAS 解读

## 核心概念

### 数据流确定性的三层保障

在 ECS 中，数据流确定性（deterministic replay / battle hash）需要三层保障：

1. **架构层**：明确 SystemGroup、约束 system 先后关系，并选择明确的 ECB playback phase
2. **写入层**：并行 job 使用 `EntityCommandBuffer.ParallelWriter` 与 sort key；跨 query/跨 World 或战斗回放需要稳定业务键与显式 merge，不能只依赖 chunk index
3. **依赖层**：正确管理 JobHandle 依赖链，避免 Singleton API 绕过依赖、避免手动 Update() 破坏变更版本号

### 系统生命周期契约

每个 ISystem 在 ECS 架构下遵循以下隐含契约：

- **OnCreate**：创建并缓存 system-owned 查询和资源；不要假设运行时 entity 数据已存在
- **OnUpdate**：把当前调用调度的所有 JobHandle 发布到 `state.Dependency`，并通过 group 排序协作；禁止手动调用其他 system 的 `Update()`
- **OnDestroy**：清理 Persistent 资源；System-associated entity component 由 ECS 自动清理

### 数据读写粒度决策树

```
Component 中有只读字段 + 读写字段？
  是 → 分离到不同 component（PRF-26）
  否 → 检查 IJobEntity 参数
    用 `ref` 声明的参数默认 write access
    用 `in` 声明的参数为 read-only access
    确保只读参数用 `in`，避免不必要标记 chunk 为已变更
```

### 推荐模式汇总

**模式 A：确定性 ECB fan-in（符合 PRF-13 + PRF-25 + CASE-47）**
```
多个并行 job 各自创建独立 ECB/ParallelWriter → 各自向同一 entity 的 buffer append 元素
→ query 内可用 [ChunkIndexInQuery] 作为 sortKey，消除 worker 执行时序造成的随机顺序
→ 需要跨 query 或跨运行稳定的战斗顺序时，记录稳定业务键并显式排序/merge
```

**模式 B：只读/读写分离（符合 PRF-26 + CASE-38）**
```csharp
struct CHealthCurrent : IComponentData { public float Value; }    // 每帧读写
struct CHealthConfig : IComponentData { public float Max; }       // 极少写
// IJobEntity 中: void Execute(ref CHealthCurrent health, in CHealthConfig config)
```

**模式 C：IJobChunk Enableable 安全模板（符合 PRF-22）**
```csharp
public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
    bool useEnabledMask, in v128 chunkEnabledMask)
{
    if (!useEnabledMask)
    {
        for (var i = 0; i < chunk.Count; i++)
            ExecuteEntity(i);
        return;
    }

    var enumerator = new ChunkEntityEnumerator(true, chunkEnabledMask, chunk.Count);
    while (enumerator.NextEntityIndex(out var i))
    {
        // 只处理 enabled entity
        ExecuteEntity(i);
    }
}
```

**模式 D：Singleton 写安全模式（符合 PRF-29）**
```csharp
state.EntityManager.CompleteDependencyBeforeRW<CGlobalConfig>();
var config = SystemAPI.GetSingletonRW<CGlobalConfig>();
config.ValueRW.someField = newValue;
```

**模式 E：System 级数据公开（CASE-45）**
```csharp
// 系统级共享数据存为 component 而非 system 字段
SystemAPI.SetComponent(state.SystemHandle, new SDebugState { ... });
// 其他系统通过 EntityQuery + SystemHandle 可正常查询
```

### 反模式汇总

**反模式 A：复用 ECB（违反 PRF-25）**
```csharp
var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
// 无依赖地让 Job1/Job2 访问同一 ECB 会触发 safety error；
// 即使串行依赖安全，重叠 sortKey 仍可能使两组命令交错。
```

**反模式 B：混合只读/读写字段（违反 PRF-26）**
```csharp
struct CUnitState : IComponentData {
    public float Health;      // 读写
    public float MaxHealth;   // 只读 — 写访问触发不必要的变更通知
    public int Level;         // 只读 — 同上
}
```

**反模式 C：手动 Update()（违反 PRF-27）**
```csharp
protected override void OnUpdate() {
    otherSystem.Update();  // 破坏当前 system 的 EntityQuery 版本号！
}
```

**反模式 D：GetSingletonRW 直接写（违反 PRF-29）**
```csharp
var config = SystemAPI.GetSingletonRW<CConfig>();
config.ValueRW.someField = newValue;  // 若有 job 正在读 CConfig → 竞态！
```

**反模式 E：无序 ParallelWriter 写入确定性输出（违反 PRF-13）**
```csharp
var parallelWriter = outputStream.AsParallelWriter();
// 各线程无序写入 → output 顺序不确定 → replay 不一致
```

---

## EX-GAS 项目解读

### 数据流设计约束

GAS Runtime Core 中以下数据流路径受到本领域规范的直接影响：

| 数据流路径 | 关联规范 | 当前状态 | 目标态 |
|-----------|---------|---------|--------|
| EffectCommand fan-in（多 system -> outbox buffer） | PRF-13, PRF-25, CASE-47 | 多个 Attribute/GE/Tag system 向同一 ASC entity 追加输出命令 | 各 job 独立 ECB；战斗顺序使用稳定业务键 merge，chunk sortKey 只负责 query 内录制顺序 |
| ActiveEffect store 遍历 | PRF-22 | 需审计 IJobChunk 中 enableable mask 处理 | 统一使用 ChunkEntityEnumerator 或 Assert.IsFalse |
| Attribute 计算 | PRF-26 | CAttribute 中 CurrentValue + BaseValue 混合 | 拆分为 CAttributeCurrent + CAttributeConfig |
| SystemGroup 执行顺序 | PRF-27 | 可能存在手动 Update 调用 | 全部通过 SystemGroup 排序和 job dependency 链 |
| 全局配置访问 | PRF-29 | GetSingletonRW 使用需审计 | 访问前 CompleteDependencyBeforeRW 或 NativeContainer 包装 |

### 状态规范参考

本主题状态规范引用以下主题文件提供更详细的上下文：
- `13-DOTS编写规范与性能陷阱.md`：P0/P1 致命/严重级规范的完整矩阵
- `15-数据流-系统生命周期规范.md`：P2 规范的完整声明、代码示例和检查清单
- 相关模式通过本主题 `_index.md` 和全局规则索引进入；early-out 依赖规则为 CASE-48，Baker 的 DependsOn 规则为 CASE-31。

---

## 常见陷阱

1. **"多个 Job 共用一个 ECB 方便"** — 无依赖共享 ECB 会触发 safety error；串行复用时重叠 sort key 仍可能让命令交错。默认每个 distinct job 独立 ECB。（违反 PRF-25）
2. **"一个 Component 包含所有属性最方便"** — 读写字段与只读字段共用 component 导致每个写入触发所有监听该 component 的响应式系统，即使只读字段从未变化。（违反 PRF-26）
3. **"手动调用其他 System 的 Update() 就能复用逻辑"** — 被调用方的 entity 数据处理会破坏调用方 EntityQuery 的变更版本号，导致响应式系统漏触发或误触发。（违反 PRF-27）
4. **"GetSingletonRW 在主线程改个配置很方便"** — Singleton API 不等待 job 完成。若有 job 正在读写该 component，返回引用后直接修改 -> 数据竞态。（违反 PRF-29）
5. **"IJobChunk 的 for 循环遍历所有 entity 就行"** — 若 query 包含 enableable component 但未使用 ChunkEntityEnumerator，会静默处理已禁用的 entity。（违反 PRF-22）
6. **"禁用 ComponentLookup 并行限制就安全"** — `[NativeDisableParallelForRestriction]` 只关闭检查；只有已证明各 worker 访问集合不重叠时才能使用。（违反 PRF-19）
7. **"调度 Job 的 system 用 EntityManager.CreateEntityQuery 也一样"** — 这类 query 没有注册为该 system 自己的访问，safety system 可能无法推导其 component 依赖。应在 `OnCreate` 用 system-owned API 创建并缓存；结构变化后不需要重建 query。（违反 PRF-33）
8. **"SystemAPI.Query 可以存为变量复用"** — `SystemAPI.Query<T>` 依赖 source generator，无法存储复用。必须内联 foreach（CASE-46）。
9. **"System 级数据放个 static 字段就行"** — static 字段在 ECS 中不参与 safety system、不考虑 threading、生命周期不跟随 system。官方推荐 System-associated entity data（CASE-45）。

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则编号 |
|------------|---------|-------------|
| `systems-entity-command-buffers.md` | 每个并行 job 独立 ECB 避免命令交错 | PRF-25 |
| `systems-data-granularity.md` | 读写分离避免响应式系统误触发 | PRF-26, CASE-38 |
| `systems-version-numbers.md` | 手动 Update() 破坏变更版本号 | PRF-27 |
| `components-singleton.md` | Singleton API 不自动完成 Job 依赖 | PRF-29 |
| `systems-systemapi-query.md` | DynamicBuffer<T> 默认读写、Query 不可存储 | PRF-30, CASE-46 |
| `job-overhead.md` | .Run() Job 有额外 job dep system 开销 | PRF-32 |
| `common-errors.md` / `systems-entityquery-create.md` | 调度 job 的 system 必须使用自身注册的 EntityQuery；query 可缓存 | PRF-33 |
| `systems-looking-up-data.md` | ComponentLookup 随机访问竞态风险 | PRF-19 |
| `iterating-data-ijobchunk.md` / `JobChunkExamples.cs` | ChunkEntityEnumerator 标准用法 | PRF-22, CASE-27 |
| `systems-data.md` | System-Associated Entity Data（component 存公开数据） | CASE-45 |
| `components-buffer-command-buffer.md` | ECB AppendToBuffer + sortKey 多源 fan-in | CASE-47 |
| `systems-systemapi.md` | SystemAPI.GetSingleton 不 sync 的明确说明 | PRF-29 补充 |
| `scheduling-jobs-dependencies.md` | `state.Dependency` 在入口包含传入依赖；显式调度返回的 handle 必须发布 | CASE-48 |
| `performance-sync-points.md` | `.Run()` 与 idiomatic foreach 都可能等待必要依赖 | PRF-32 |
