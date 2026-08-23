# 状态机策略：API 与 EX-GAS 解读

**适用版本**：Unity `6000.3.14f1`；Entities `1.4.6`

## 官方三种实现方法

| 方法 | 官方定义 | 常见实现 | 主要代价 |
|---|---|---|---|
| Per-state data clustering | 按状态把 entity 数据聚集到不同 archetype/chunk | tag component、shared component | 状态切换结构变化、碎片化、archetype/chunk 数 |
| Per-state data branching | 不按状态改变 archetype，通过状态过滤每个状态要处理的 entity | enableable component、每状态一个 job 并在 job 中过滤 | 重复遍历、job 调度、稀疏匹配的数据抓取 |
| Per-FSM data branching | 不同状态留在同一 archetype，一个 job 内按值分支 | enum/switch | idle 数据抓取、复杂分支、较宽读写依赖 |

Unity 官方没有给出“状态数 <= 5”“body < 20 行”“bool > 3”“耗时占比 5%”等阈值。官方结论是根据结构变化、chunk 使用、数据抓取、job overhead 和依赖用 Profiler 选择。

## 选择原则

### Per-FSM data branching

通常是实现最简单、job 数最少的起点，适合状态逻辑共享大部分数据、所有状态都需经常处理的场景。但不是所有 FSM 的强制默认；大量 idle entity 会让无效数据抓取显著。

### Enableable / per-state branching

Enableable component 的切换不改变 archetype，可从 worker thread 安全切换。查询会跳过 disabled entity；若一个 chunk 中没有匹配 entity，可以跳过整个 chunk。它仍有 mask/filter 成本，主线程同步查询还可能等待写 enable-state 的 job。

不要为了 Enableable 又在非 Idle 时 Add/Remove 一个普通 tag；这会重新引入结构变化。需要查询过滤的状态直接用 enableable component，互斥状态也可以用 enum 加按需查询策略。

### Tag/shared clustering

适合切换很少、同状态 entity 足够多、且聚集后能显著改善数据局部性的情况。Tag Add/Remove 是结构变化；shared component 值改变也会把 entity 移到不同 chunk。不能只因为“ECS 原生”就选聚类。

### Bit field

bit field 能避免多个 tag 类型组合和结构变化，适合一起读取、一起更新的标记。但它不能让 EntityQuery 原生过滤某一 bit，会让所有消费者依赖同一 component，可能增加无效数据抓取和写冲突。独立查询频繁的标记可能更适合 enableable component。

## 多 FSM

多个 FSM 放在同一 entity 上并不必然使 archetype 组合相乘：只有状态通过 tag/shared/component 类型组合表达时才产生相应 archetype/chunk 组合。若使用 enum/bit field，主要风险是 entity 变大、重复抓取、复杂依赖和状态语义耦合。

拆分 entity 也有 Entity/lookup 间接访问与生命周期协调成本。因此“状态 component 数 > 3 就拆”不是官方规则；应按访问共现性和 Profiler 数据决定。

## EX-GAS 当前策略

- ActiveEffect lifecycle：先保留 slot enum + 单 job 分支；若大量 idle slot 或某状态逻辑独立成为瓶颈，再评估布局/作业拆分。
- Ability active/cooldown：enableable component 是候选，而非无条件迁移结论；需要先确认 query 过滤、写 enable-state 依赖和数据布局收益。
- Status flags：经常一起读取、无需 query 单独过滤的标记可合并 bit field；频繁独立查询的状态保留 enableable 候选。
- 高频状态切换不使用普通 tag Add/Remove，除非 Profiler/结构语义给出明确例外。
- Chunk component 不用于逐 entity 状态；它属于物理 chunk 元数据。

## 监控与验收

- Structural Changes Profiler：切换导致的结构变化与 sync point；
- Archetypes Window：archetype 数、chunk 使用和碎片化；
- CPU Profiler/Jobs：job 数、调度开销、依赖空洞；
- 原生 Profiler：cache miss、无效/重复数据抓取；
- Systems Window/Journaling：写版本变化与 reactive system 触发。

所有数值阈值必须标注为“EX-GAS 待基准策略”，附场景、平台、实体规模与版本；未经基准不得作为硬性审查门槛。

## 官方证据

| 官方文档（Entities 1.4.6） | 可裁决结论 |
|---|---|
| `state-machine.md` | 三种 FSM 方法、七类问题、监控工具、复杂方案需 profile |
| `structural-changes-enableable-components.md` | enableable 切换不产生结构变化 |
| `components-enableable-use.md` | query filtering、worker thread 切换和同步注意事项 |
| `performance-chunk-allocations.md` | tag/archetype 组合和 chunk 碎片化 |
| `optimize-structural-changes.md` | 结构变化方式与 Profiler；示例数字不是跨项目阈值 |
