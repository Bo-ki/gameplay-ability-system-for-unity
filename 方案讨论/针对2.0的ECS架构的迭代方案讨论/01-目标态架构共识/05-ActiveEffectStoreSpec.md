# Active Effect Store Spec

## 目的

定义 duration、stack、period、granted tag、granted ability 等跨帧状态的目标存储方式。

## 状态图

```mermaid
stateDiagram-v2
    [*] --> PendingApply
    PendingApply --> Active
    Active --> Inhibited: ongoing requirement failed
    Inhibited --> Active: requirement restored
    Active --> PendingRemove: duration expired / remove request
    Inhibited --> PendingRemove
    PendingRemove --> Removed
    Removed --> [*]
```

## 核心契约

1. Active effect store 只承载跨帧状态。
2. Period / Overflow 只派生 EffectCommand，不直接写 Attribute。
3. Granted tag / ability 必须有明确 owner 和 cleanup path。
4. StackCount 影响 magnitude 时，必须通过 spec/active mutation 同步。

## 官方依据与设计论证

| 目标态选择 | 官方规则依据 | 为什么更优秀 | 为什么有必要 |
|---|---|---|---|
| Active effect 默认先进入 ASC owner-local slot，而不是每个 effect 默认实体化 | `BUF-01`、`PRF-10`、`QRY-04`、`SC-01` | owner-local buffer / slot 让 duration、stack、period tick 可按 ASC 分组处理，减少跨 entity random lookup | 大量 buff/debuff 若默认独立 entity，会增加 query 数、archetype 数和 cleanup 结构变化 |
| Inhibited / PendingRemove / PeriodDue 等轻量状态默认 enum / bit flags | `FSM-02`、`FSM-05`、`PRF-03`、`EN-01` | 状态切换不触发 archetype 迁移，多个轻量状态可在同一 job 中分支处理 | 高频状态如果通过 tag component add/remove 表达，会造成结构变化热点和 archetype 爆炸 |
| Period / Overflow 只派生 EffectCommand，Attribute 写入仍归 Attribute Reduce/Apply | `SC-01`、`CASE-12`、`NAT-03`、`PRF-26` | store lane 只负责生命周期推进，数值修改继续走 target-grouped reduce/apply，可保持写集合清晰 | Period effect 若直接写 Attribute，会绕开 modifier merge、dirty mask 和 Debugger 归因 |
| Granted tag / ability cleanup 进入 Structural Commit 或 CleanupStore | `ECB-03`、`SC-03`、`PRF-04`、`SYS-05` | grant/remove/destroy 的结构变化集中 playback，Profiler/Journaling 能定位来源 | owner 死亡、effect remove 和 ability revoke 需要 deterministic cleanup；散落在 tick job 中会形成隐式 sync point |

## Unity Entities 存储校准

ActiveEffectStore 的目标不是“把每个 active effect 都实体化”，而是用 Unity Entities 机制表达跨帧状态：

| 状态类型 | 推荐承载 | 说明 |
|---|---|---|
| active effect slot | ASC entity 上的 DynamicBuffer slot / stable child entity | 高频读写优先稳定布局 |
| inhibited / ready / expired | slot enum / bit flags | 默认不为每个轻量状态建 enableable component |
| period due | slot flag / frame command；enableable 仅作 profiler 证明后的 chunk skip 优化 | 到期时派生 `EffectCommand` |
| stack count / remaining time | unmanaged component / buffer element | 不混入 definition |
| granted tag / granted ability owner | owner id + cleanup command | cleanup 进入 `GASStructuralCommitSystemGroup` |

结构变化只允许发生在明确 lifecycle 边界，例如首次创建 owner state、最终 cleanup、grant ability 等；普通 tick / inhibited 切换 / period due 不应 add / remove component。

## DOTS 深读后的 Store 分层

ActiveEffectStore 不再讨论“buffer 还是 entity”二选一，而是拆成四个可组合存储面：

| Store 面 | 职责 | 候选 API | 适用判断 | 必须指标 |
|---|---|---|---|---|
| OwnerLocalStore | ASC owner 上少量 active effect slot、stack、remaining time、owner-local period | ASC `DynamicBuffer` slot、small unmanaged component、FixedList-like generated small state | 每 owner active effect 数量可控、主要按 owner 顺序处理 | slot count、capacity、spill、compact count |
| GlobalIndexedStore | 需要跨 owner 查询、统一 period bucket、全局生命周期扫描的 active effect | stable active effect entity、chunk component bucket、shared low-frequency group | effect 数量大或跨 owner query 多，且创建/销毁是低频边界 | active entity count、archetype count、chunk utilization |
| LifecycleCleanupStore | owner destroyed / effect removed 后仍需释放 granted tag / ability / cue / spawned bundle | Cleanup Component、Cleanup Shared Component、LinkedEntityGroup boundary | destroy 后仍需上下文；同类清理信息可共享 | cleanup retained count、cleanup latency、shared unique value |
| ChunkSkipIndex | 大量 idle / not due / inhibited effect 的 chunk 级跳过索引 | Chunk Component、enableable mask、`IJobChunk` precheck、change version | 可按 chunk 判断整批无需处理 | matched chunks、skipped chunks、unused entities、skip reason |

执行含义：

1. `OwnerLocalStore` 的 DynamicBuffer 必须明确 `InternalBufferCapacity`。超过 capacity 外置后不会自动回到 chunk，若 active effect 数量波动大，应考虑 stable entity 或 `InternalBufferCapacity(0)`。**当 `InternalBufferCapacity(0)` 时**：spill 监控改为 "total external buffer element count"（非百分比），告警阈值：> 32 slots → 考虑 GlobalIndexedStore，> 64 slots → 架构告警。此约束与 `13-EntityComponent物理布局Spec.md` 行255-258 的 buffer 容量策略一致。
2. `GlobalIndexedStore` 不是“每 tick 创建 GE entity”。它只适合生命周期稳定、需要全局 query 的 effect；period due / inhibited / ready 不通过 add/remove component 表达。
3. `CleanupStore` 只处理 destroy 后清理，不替代 Active 生命周期状态机；cleanup component 不能 baked 到 definition。
4. `ChunkSkipIndex` 的目标是让 idle effect 在 chunk 级跳过，而不是给每个 entity 重复写相同 fact。
5. 状态机选择顺序：enableable 适合高频开关；enum / bit field 适合状态多且同 job 轻分支；tag / archetype 只适合低频且数据差异大的生命周期阶段。
6. **`FSM-04`：单 entity 上多 FSM 叠加时拆分 Job，而非拆分 Entity。** ASC entity 的 `ActiveGameplayEffectBuffer` buffer 同时承载多套独立 FSM（inhibit/tick/expire、stack push/pop、grant tag/ability cleanup 等）。`FSM-04` 的官方判断标准是：**同一 entity 上 > 3 个独立的生命期决策，且各决策依赖不同的 component 读取集**。`ActiveGameplayEffectBuffer` 完全命中这个条件。

   **修正策略 —— 按 FSM 职责拆分 Job（不增加 Archetype）：**
   ```
   Job A: Duration lifecycle FSM（读 RemainingDuration，写 State/Flags）
   Job B: Period cursor FSM（读 PeriodAccumulator，写 slot PeriodDue flag / 派生命令候选）
   Job C: Stack overflow check（读 StackCount，写 ActiveEffectMutationBuffer）
   Job D: Granted tag/ability cleanup（读 Flags.PendingRemove，写 ECB）

   Job 依赖链：
   A → B（Inhibited 状态不应 tick period）
   A → D（需要知道哪些 effect 进入 PendingRemove）
   C 与 A 并行（不依赖 duration state）
   ```

   **不拆分 Entity 的原因**：拆分为独立 Entity 会增加 archetype 数量和 entity 数量，与 Archetype < 10 目标矛盾。拆分 Job 保持同一 Entity/Archetype，仅通过 Job 依赖链管理 FSM 间的数据依赖。每个 Job 只读写自己关心的字段，依赖链清晰，可独立 Profile。
7. ActiveEffect 的默认状态表达是 slot 内 enum / bit flags；禁止为 `PendingApply / Active / Inhibited / PendingRemove / Removed` 等轻量状态各建一个 enableable component。
8. Status / Buff / Debuff 类标记由 granted tag / active effect store 聚合到 bitmask 或 status flags；禁止每种 status 一个 tag component 或 enableable component。

## ActiveEffectStore API 选型矩阵

ActiveEffectStore 不能预设为“每个 active effect 一个 entity”或“全部塞进 ASC buffer”。任一 ActiveEffect 小闭环前都必须按状态类型选型：

| 候选承载 | 适用 | 风险 | 验收指标 |
|---|---|---|---|
| ASC `DynamicBuffer` slot | 每个 ASC active effect 数量有限、按 owner 顺序批处理 | buffer spill、slot compact、并行写冲突 | slot count、capacity、spill、compact count |
| stable active effect entity | 每个 effect 状态复杂、需要独立 query / timer / stack | entity 数量增加、query 数增加、生命周期 cleanup | active entity count、archetype count、query match |
| enableable marker（可选） | 大量 idle / not-due entity 需要整 chunk/entity skip，且 profiler 证明收益 | query enabled state 成本、同步等待、状态语义不足；禁止替代 slot enum/bit flags | enabled count、ignore-filter count、wait ms |
| enum state / bit field | 状态很多但每状态工作轻、适合同一 job 分支 | 分支影响 vectorization、可读性下降 | branch distribution、job cost |
| Cleanup Component | owner destroy 后仍需释放 granted tag / ability / cue | cleanup 生命周期必须明确移除 | cleanup retained count、cleanup latency |
| Chunk Component / chunk counters | 大量 idle/no-op effect 可整体跳过 | 只适合 per-chunk 优化事实，不替代 per-entity state | chunk skip count、matched chunk reduction |
| LinkedEntityGroup | prefab-like group、表现/authoring 组合实例化和销毁 | 不进入 Core hot path 决策 | group instantiate/destroy count |

### `CASE-37` LinkedEntityGroup 在 ASC→Ability Entity 生命周期中的应用

**评估结论**：`LinkedEntityGroup` 适合用于 ASC → Ability Entity 的**生命周期级联销毁**，但不适合用于**迭代顺序依赖**的场景。

**适用场景 —— ASC Entity 销毁时的级联清理：**
- 当 ASC Entity 被销毁时，其所有 Ability Entity 也应当被销毁
- 使用 `LinkedEntityGroup` 后，`GASStructuralCommitSystemGroup` 中通过 ECB / bulk destroy 销毁 `ascEntity` 时可自动级联销毁所有 Ability Entity
- 替代全局扫描并手动查找子 Ability Entity 的 O(N_abilities_global) 清理路径

**不适用场景：**
- 需要按特定顺序逐 Ability Entity 执行清理逻辑（如先 revoke 后 destroy）
- `PRF-31`：Child Buffer 迭代顺序不保证确定，不应依赖 sibling index 做排序

目标态建议在 ASC Entity 创建时将 Ability Entity 加入 `LinkedEntityGroup`，销毁时利用级联机制减少手动清理代码。此选项应在 `GASStructuralCommitSystemGroup` 中实现，不影响 hot path 性能。

ActiveEffectStore 验收必须能证明：普通 tick、period due、inhibit 切换不触发 archetype churn；grant/remove/cleanup 进入 `GASStructuralCommitSystemGroup`；Debugger 能解释 slot pressure、optional enableable state、cleanup 和 chunk skip。

## OwnerLocalStore Baseline 约束

目标态默认 baseline 是 `OwnerLocalStore`：ASC owner 持有 `ASCActiveEffectsComponent` 和 `ActiveGameplayEffectBuffer`，用 slot 承载 duration active effect 的跨帧状态。它是 baseline，不是唯一终局；当 slot pressure、跨 owner query 或 chunk skip 证据触发时，必须重新评估 `GlobalIndexedStore`、Cleanup Component 或 ChunkSkipIndex。

约束：

1. Store 创建只发生在 ASC factory / bootstrap 等低频结构阶段；helper 在 hot path 发现缺少 store 时必须返回失败，不允许隐式 add component / add buffer。
2. Slot 可以记录 `Active / Inhibited / PendingRemove` 等 enum state、duration、remaining、period cursor、stack count、source / target / context 和 legacy entity 引用。
3. slot 可以保留外部 lifecycle reference 字段用于 proof 或互操作，但该字段不得成为目标态 active effect lifecycle 的权威。
4. Slot buffer 不能把逻辑上限直接等同为 chunk 内联容量；heavy slot element 应使用小 `InternalBufferCapacity`，逻辑上限通过 store helper / validation gate 控制。
5. period due / overflow simple instant 派生命令必须进入 EffectCommand 主链；复杂 child GE 需要明确是否仍属于 active store lifecycle 或 structural request。
6. Debugger 必须输出 slot pressure、state distribution、externalized owner count、compact count、cleanup retained count 和 chunk skip count，避免把 slot mirror 误判为 scale-ready store。

## Period / Overflow 派生输出约束

period / overflow 的 simple instant child GE 必须作为 ActiveEffectStore 的派生输出进入 EffectCommand 主链：

1. period due 时，simple instant child GE 不默认创建 request/runtime child entity，而是写入 `EffectCommand(Source=Period)`。
2. stack overflow 派生 simple instant child GE 同样写入 `EffectCommand(Source=Overflow)`；复杂 child GE 必须明确 fallback 条件和重选型触发。
3. 派生命令复制 child GE 的 SetByCaller values，保持 command/spec/delta/fact 的 magnitude 输入连续性。
4. `GEPeriodStateComponent.StartTime` 与 owner-local `ActiveGameplayEffectBuffer.LastPeriodFrame` 必须同步刷新；store cursor 是 store-driven lifecycle 和 Debugger 证据的一部分。
5. 若派生命令仍落在 singleton command stream，validation evidence 必须标记 proof-only、规模上限、重选型触发条件和移除任务。

## 禁止方向

1. active effect 直接持有子 effect runtime entity。
2. 把 instant GE 和 duration GE 都塞进同一重 lifecycle。
3. 把 definition 字段和 runtime timer / stack state 混写。
4. 用 add / remove component 表达每帧 inhibited、period due、ready 等高频状态切换。
5. 不定义 DynamicBuffer 容量、清空策略和 buffer pressure 计数就把 ActiveEffectStore 作为高规模目标结构。
6. 未复核 Cleanup Component、Chunk Component、enum state、stable entity 等候选 API 就固定单一存储形态。
## 历史方案定位

1. GameplayEffectConfigBlob 中 Duration / Period / Stack 字段的设计信号来自 `../历史方案参考/方案11.md:393-406`。
2. GE Blob 构建和 `CEffectConfigRef` 参考来自 `../历史方案参考/方案11.md:510-522`。
3. Duration GE、GrantedTags 和 modifier blob 的生成例子来自 `../历史方案参考/方案14.md:611-624`。
4. unmanaged modifier buffer 替代托管 modifier 的信号来自 `../历史方案参考/方案15.md:451-466`。
