# 03H：DOTS API 策略与 Backbone 验收

> Owner：`01-目标态架构共识/03-RuntimeCore管线` | 状态：目标态子 Spec | 拆分来源：`../03-RuntimeCore管线Spec.md` | 最近拆分：2026-06-07

本文件只描述理想 Runtime Core 目标态。禁止写入当前代码事实、迁移流水、验证数字或下一步任务；现实证据必须回到 `../../00-当前架构事实/`，任务拆分必须回到 `../../02-主线任务树/`。

定位：目标态 DOTS API 选型修正、Enableable 策略、Chunk Component 策略、DOTS Backbone First 顺序和 API 预算。

## DOTS API 选型修正

Runtime Core phase 不等于固定 API。任一实现前必须按下表完成 API selection ladder：

| Kernel | 首选问题 | 候选 DOTS API | 适用规则 | 必须输出的证据 |
|---|---|---|---|---|
| Frame Prepare | 本帧是否需要 frame clock / scratch / budget，生命周期多长 | `WorldUpdateAllocator`、system group allocator、Rewindable allocator、system-associated entity data、budget counter | `SYS-02` `SYS-03` `CASE-16` `CASE-45` `NAT-01` `NAT-04` `NAT-05` `PRF-14` | allocator owner、rewind 生命周期、lookup update count、query count、dependency wait |
| Definition Consumption | 静态配置是低频 bootstrap 数据还是每帧可变状态 | `GASDefinitionCatalogComponent` singleton + `BlobAssetReference<GASDefinitionCatalogBlob>`、generated code -> index lookup、`ref readonly` Blob access、optional perfect hash | `BLOB-01` `BLOB-02` `CASE-07` `CASE-24` `CASE-46` `PRF-34` | forbidden dependency scan、schema/content hash、lookup complexity、singleton write count = 0、Blob dispose owner |
| Boundary Command Ingest | 命令来自低频边界还是高频 fan-in | request entity、Boundary buffer、owner DynamicBuffer、`NativeStream` | `SEL-01` `SEL-02` `CASE-04` `CASE-12` `BUF-01` `NAT-03` `PRF-13` | command count、request churn、fan-in source、determinism |
| Target Resolve | target 是否能按 command/request 顺序解析 | `IJobChunk`、`IJobParallelFor`、`AbilityTargetRecord` NativeStream、request-owned `TargetDataBuffer`（低量物化）、Physics query input snapshot、deterministic sort key | `QRY-01` `QRY-03` `QRY-04` `PHY-02` `MAT-05` `CASE-03` | target count、physics query cost、sort key、buffer spill、random lookup count |
| Effect Fan-In | 多 producer 是否需要并行写入和确定性归并 | `NativeStream`、per-thread stream、chunk-local scratch、compact owner-local command range | `CASE-12` `NAT-02` `NAT-03` `MAT-05` `BUF-02` `SEL-02` | stream segment count、merge cost、buffer pressure、battle hash |
| State Evaluate | 状态切换是高频开关还是生命周期变化 | enum / bit field、DynamicBuffer slot、enableable（仅高频 query skip）、Chunk Component、stable effect entity | `EN-01` `EN-02` `FSM-01`~`FSM-06` `CASE-06` `CASE-20` `CASE-28` `PRF-01` `PRF-03` | slot count、state distribution、enableable wait、chunk skip count |
| Attribute Reduce / Apply | 是否需要随机访问或可按 target 分组 | sorted stream、per-target buffer、chunk-local apply、只读 Blob/static lookup | `QRY-03` `QRY-04` `PRF-06` `PRF-19` `PRF-26` `CASE-04` | random lookup count、write component set、change version risk |
| Gameplay Fact | fact 是 Core reaction 还是 Boundary observation | typed fact buffer、reaction cursor、`NativeStream` + deterministic merge、sampled sink | `SYS-05` `CASE-12` `CASE-18` `CASE-38` `DBG-03` `STORE-03` | fact count、consumer count、reaction source、projection lag |
| Structural Commit | 是否可批量而非逐实体 | EntityQuery bulk、`ComponentTypeSet`、`EntityQueryCaptureMode.AtPlayback`、custom ECB playback；`ExclusiveEntityTransaction` 仅限 secondary/streaming World | `SC-01`~`SC-03` `ECB-01`~`ECB-04` `CASE-05` `CASE-21` `CASE-33`~`CASE-35` `PRF-02` `PRF-04` `PRF-21` `PRF-25` | structural count、sync point、origin system、是否 secondary/streaming World |
| Boundary Projection | 是否进入表现/Replay/Debugger 边界 | read-only projection job、outbox buffer、sampled sink、WeakObjectReference / UnityObjectRef boundary | `SYS-05` `DBG-01` `DBG-02` `GFX-01` `CONTENT-01` `CASE-10` `CASE-11` `PRF-32` | presentation marker、replay sink、GC alloc |

执行规则：

1. EffectCommand / instant GE 主链实现前，`EffectCommand` 承载必须复核 `NativeStream` / per-owner stream / DynamicBuffer / request entity / ECB 的取舍（`CASE-04` `CASE-05` `CASE-12` `CASE-47` `SEL-01` `SEL-02`）。
2. ActiveEffectStore 每个小闭环前，必须复核 slot、stable entity、cleanup component、enableable、enum state、chunk component 的取舍（`CASE-06` `CASE-15` `CASE-20` `CASE-28` `CASE-37` `FSM-01`~`FSM-06`）。
3. Debugger 必须能解释 API 选型是否健康，而不是只输出 `avgTickMs`（`DBG-01`~`DBG-05`）。
4. 每个 phase 的实现写法必须对照具体 `CASE-*`：hot path 遍历对照 `CASE-02`/`CASE-03`（禁止 `CASE-01` 主线程 foreach），结构变化对照 `CASE-05`/`CASE-21`/`CASE-33`~`CASE-35`，buffer 对照 `CASE-04`/`CASE-36`/`CASE-47`，bake/resource 对照 `CASE-07`/`CASE-39`~`CASE-44`，chunk 优化对照 `CASE-18`/`CASE-26`~`CASE-28`/`CASE-38`。
5. Structural Commit 必须声明 `sortKey` 策略（`CASE-35` `[ChunkIndexInQuery]`）和独立 ECB per job 策略（`PRF-25`）。
6. `IJobChunk` 迭代必须按 `useEnabledMask` 分支：无 mask 走普通 `for` 快路径；有 mask 使用 `ChunkEntityEnumerator`（`CASE-26`），批量 enable/disable 操作使用 `EnabledMask`（`CASE-20`）。

---

## Enableable 全局策略

### 为什么需要全局策略

`IEnableableComponent` 是 Unity ECS 避免高频状态结构变化和 archetype 爆炸的工具（参见 `PRF-03`），但它不是生命周期状态的默认表达。PackageCache `components-enableable-intro.md` 将它限定在频繁、不可预测、高排列状态上；enableable 的同步查询也不是免费的，写 job 未完成时会等待依赖。因此需要全局策略：**哪些 component 是 enableable、哪些 query 用 IgnoreFilter、在哪个 phase 做 toggle。**

### Enableable Component 清单

| Enableable Component | 挂载 Entity | Toggle Phase | Toggle 方式 | 消费 Query 的 IgnoreFilter? |
|---|---|---|---|---|
| `PeriodDueTag`（optional） | ASC entity | `GASCoreSimulationSystemGroup` / State lane | `EnabledRefRW` | 仅当 profiler 证明 skip 收益时启用 |
| `AbilityExecutableTag`（optional） | Ability entity | `GASCoreSimulationSystemGroup` / State lane | `EnabledRefRW` | 仅当 profiler 证明 query skip 收益时启用 |
| `FactReadyTag`（optional） | fact stream | `GASCoreSimulationSystemGroup` / Fact lane | `EnabledRefRW` | 消费者用 `IgnoreFilter` 或异步 query |

### Query IgnoreFilter 策略

| Query 用途 | 是否 IgnoreFilter | 原因 |
|---|---|---|
| Attribute Reduce / Apply 读取 Attribute（非 enableable） | N/A | Attribute 本身不是 enableable |
| State Evaluate 遍历 active slot | 默认 N/A；只有启用 `PeriodDueTag` 时才评估过滤 | ActiveEffect 默认用 slot enum / bit flags，不为每个 slot 创建 enableable；大量 idle 且 profiler 证明收益时才引入 enableable skip |
| Gameplay Fact 消费 modifier / fact（非 enableable） | N/A | Modifier / fact buffer 不是 enableable |
| Debugger 快照 | **是** — IgnoreFilter | Debug 用途不做 enableable 过滤，避免等待写 job |
| Boundary Projection outbox 投影 | **是** — IgnoreFilter 或用异步 query | Boundary 不阻塞 Core |

### Enableable Wait 监控

Debugger 必须报告：
- `enableableWriteJobCount` per phase
- `enableableSyncQueryCount` — 使用了同步 query + 未用 IgnoreFilter + 有未完成的 enableable 写 job
- **若 `enableableSyncQueryCount > 0` 告警** —— 表示本帧有额外的 sync point

---

## Chunk Component 策略

### 使用场景

| Chunk Component | 目的 | 设置 Phase | 设置条件 | 消费 Phase |
|---|---|---|---|---|
| `AllIdleChunkComponent` | 整个 chunk 的 ASC 的所有 effect slot 都是 idle | `GASCoreSimulationSystemGroup` / State lane | 遍历 chunk 确认所有 slot 状态 | CoreSimulation 内 Fan-In / Attribute lane 跳过整个 chunk |
| `NoActiveEffectsChunkComponent` | 整个 chunk 的 ASC 无 active effect | `GASCoreSimulationSystemGroup` / State lane | 遍历 chunk 确认无 active | CoreSimulation 内 Attribute / Fact lane |
| `PeriodDueChunkComponent`（可选） | 整个 chunk 的 ASC 的 period due 状态统一 | `GASCoreSimulationSystemGroup` / State lane | 按 period 时长分组 ASC 到不同 chunk | CoreSimulation 内 Fan-In lane |

### Chunk Skip 实现模式

```csharp
// IJobChunk.Execute 开头：
public void Execute(in ArchetypeChunk chunk, ...)
{
    // 检查 chunk component — 如果整个 chunk idle 则跳过
    if (chunk.Has<AllIdleChunkComponent>())
        return;  // 零 entity 遍历成本

    // ... 正常遍历
}
```

**注意：** Chunk Component 是优化，不是正确性依赖。如果 Attribute Reduce/Apply 依赖 `NoActiveEffectsChunkComponent` 跳过，但 State Evaluate 忘记更新该标记，会导致逻辑错误而非 crash。因此 Chunk Component 的使用必须有 Debugger 验证。

### Chunk Component 一致性验证算法

Debugger 在 `GASBoundaryProjectionSystemGroup` 中执行轻量级采样验证（不阻塞 hot path）：

1. **采样策略**：每 N 帧（N=60，约 1 秒一次）随机选取 10% 的 chunk，对其中的全部 entity 做全量状态扫描。
2. **验证逻辑**：
   ```
   for each sampled chunk:
       if chunk.Has<AllIdleChunkComponent>():
           // 验证：该 chunk 中不应有任何 active slot
           for each entity in chunk:
               for each slot in ActiveGameplayEffectBuffer:
                   if slot.Flags & Active: → 报告 "AllIdleChunkComponent 错误标记"
       if chunk.Has<NoActiveEffectsChunkComponent>():
           // 验证：该 chunk 中不应有任何 active effect
           for each entity in chunk:
               if HasAnyActiveSlot(entity): → 报告 "NoActiveEffectsChunkComponent 错误标记"
   ```
3. **输出指标**：
   - `chunkComponentMismatchCount` — 标记与实际状态不一致的 chunk 数
   - **若 > 0 → P0 告警**（逻辑错误，可能导致 entity 被错误跳过）
   - `chunkSkipSavings` — chunk skip 实际节省的处理量（skipped entities / total entities）
4. **降级策略**：若 `chunkComponentMismatchCount > 0`，消费者 phase 应在该帧自动 fallback 到 per-entity 检查（忽略 Chunk Component），并向 Debugger 输出降级事件。

---

## DOTS Backbone First 目标顺序

`Frame Prepare` 不是可选优化阶段，而是 Runtime Core 功能扩展的前置骨架。扩展 `Effect Fan-In`、`State Evaluate`、`Attribute Reduce/Apply`、`Gameplay Fact` 或 Debugger 之前，必须先完成 `Runtime Core Frame Backbone`：

1. SystemGroup：建立 `GASFramePrepareSystemGroup`、`GASCommandResolveSystemGroup`、`GASCoreSimulationSystemGroup`、`GASStructuralCommitSystemGroup`、`GASBoundaryProjectionSystemGroup` 的显式顺序；业务 kernel 作为 lane system 排序。
2. Frame owner：每类 command / spec / delta / fact / active mutation stream 都必须声明 owner、clear phase、writer phase、reader phase 和 merge phase。
3. Query / lookup budget：每帧 query 数、lookup update 数、random lookup 数、filtered / unfiltered query 数和 enableable wait 必须可统计。
4. Allocator / dependency budget：每帧 scratch allocator、`WorldUpdateAllocator` / `RewindableAllocator` 使用者、job dependency wait 和 manual NativeContainer dependency 必须可归因。
5. Determinism：并行 fan-in 必须声明 sort key、partition、merge order、battle hash 或等价 deterministic output policy。
6. Structural commit：hot path 结构变化只允许进入 `GASStructuralCommitSystemGroup`，并输出 playback count、ECB command count、bulk query count 和 origin system。
7. Debug evidence：Debugger 至少输出 frame backbone counters；性能结论必须能和 Profiler / Entities Journaling / Burst Inspector 证据对照。

该顺序不新增架构层级，只规定 Runtime Core 的实现前置条件。任何扩展目标只能在该 backbone 上扩展，不能把 proof-only 小闭环扩写成新的目标骨架。

## Runtime Core API 预算

每个 phase 除了功能验收，还必须给出 API 预算和重新选型触发条件：

| 预算项 | 必须记录 | 重新选型触发 |
|---|---|---|
| buffer pressure | length / capacity / peak / spill / clear phase | spill 或 x50 起峰值持续增长 |
| lookup pressure | lookup update count / random lookup count / read-write lookup count | random lookup 成为 TopN 热点或随实体数线性爆炸 |
| chunk efficiency | matched chunks / skipped chunks / utilization / enabled-aware count | 大量 idle/no-op 仍全量扫描 |
| structural cost | query bulk count / ECB command count / playback count / sync point | 大批量变化表现为 per-entity ECB |
| output determinism | sort key / partition / post-sort / hash | battle hash 不稳定或无序 ParallelWriter 影响 gameplay |
| native allocation | allocator / lifetime / dispose / merge cost | TempJob 越界、Persistent 无 owner、merge cost 高于主计算 |
| proof-only API | proof marker / scale-ready marker / reselect trigger | proof API 被用于 x1000 以上却无替代方案 |
| dependency budget | read/write component set / enableable wait / SystemAPI foreach sync / manual NativeContainer dependency | 无意义等待成为 TopN 或 query/filter 触发主线程阻塞 |

## DOTS 深读后的管线修正

1. Runtime Core 每帧必须显式归因 query / lookup / allocator / dependency 成本；EntityQuery 和 Lookup 由 owner system 自己创建/刷新，Frame Prepare 负责预算计数和 allocator 生命周期。
2. `Command Ingest` 只负责把 Boundary request 翻译为 Core command，不负责 spec 计算、不负责表现 projection、不直接结构变化。
3. `Effect Fan-In` 和 `Attribute Reduce / Apply` 的目标形态是 target-grouped 顺序 pass；若必须 random lookup，必须解释为什么不能按 target 分组或使用 owner-local buffer。
4. `Structural Commit` 是唯一热路径结构变化语义屏障；大批量同类结构变化优先 EntityQuery bulk / `ComponentTypeSet`，job 内发现的少量变化才进入 ECB。
5. `Boundary Projection` 不再承担 Debugger 全量日志；只投影必要 outbox / replay / sampled sink，性能分析由 Debugger counters 与 Unity Profiler / Journaling 对照完成。
6. AutoChess 无头验收若使用隔离 world 或固定 tick，应明确 world time / `ICustomBootstrap` / manual runner，不隐式依赖 Editor frame delta。
7. **更新：** Enableable toggle 只能发生在明确拥有状态的 lane：`GASCoreSimulationSystemGroup` 的 State lane（高频状态）或 `GASStructuralCommitSystemGroup`（grant/revoke/destroy）；Target / Fan-In / Attribute Apply 只读 enableable。
8. **更新：** Chunk Component 由 CoreSimulation 的 State lane 维护；消费者 lane 的 `IJobChunk` 在 `Execute` 开头检查 Chunk Component 决定是否跳过整个 chunk。

---
