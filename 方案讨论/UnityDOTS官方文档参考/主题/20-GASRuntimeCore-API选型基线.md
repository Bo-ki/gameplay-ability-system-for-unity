# 20 GAS Runtime Core API 选型基线

## 职责

本主题把官方 DOTS API 反推到 EX-GAS Runtime Core 设计。它不是新目标态 Spec，而是执行任务前的 **API selection checkpoint**。每个 Runtime Core 任务必须先通过此 checkpoint 才能开始实现。

## 核心概念详解

### API Selection Ladder

在执行任何 Runtime Core 任务前，必须按以下流程完成 API 选型：

```
Step 1: 明确数据性质
  ├── deterministic gameplay result? (需要确定性和可复现性)
  ├── transient command? (本帧消费即可)
  ├── telemetry / debug? (可丢弃)
  ├── presentation marker? (Boundary 层消费)
  └── structural mutation? (需要 ECB)

Step 2: 评估候选 API
  ├── 并行写入 → NativeStream vs ParallelWriter vs ECB
  ├── per-owner 存储 → DynamicBuffer vs Enableable+Component
  ├── 全局查询 → Singleton vs ComponentLookup vs EntityQuery
  ├── 只读定义 → BlobAsset vs generated static array
  └── 状态切换 → Enableable vs Add/Remove Component

Step 3: 声明拒绝理由
  └── 不得只写"不需要"，必须写"X 场景下 Y 的问题，Z 更适合"

Step 4: 设定重新选型触发条件
  └── 规模/性能/确定性阈值
```

### 数据性质分类

| 数据性质 | 确定性要求 | 生命周期 | 容量上限 | 首选承载 |
|---|---|---|---|---|
| Gameplay 权威状态 | 是 | 跨帧 | 可预测 | Component + Buffer on entity |
| EffectCommand | 是 | 本帧 | 可预测 | NativeStream + deterministic sort |
| InstantSpec | 是 | 本帧 | 可预测 | IJobChunk scratch |
| AttributeDelta | 是 | 本帧 | 可预测 | per-owner buffer + reduce |
| TypedFacts | 是 | 本帧 | 可预测 | singleton buffer + 排序 |
| ActiveEffectStore | 是 | 跨帧 | 固定 slot 上限 | owner-local Buffer + Enableable |
| Cue/Presentation outbox | 否 | 本帧 | 可变 | NativeStream → boundary queue |
| Debug telemetry | 否 | Persistent | 截断 | sampled NativeList + periodic export |

### API 选型决策表（按 EX-GAS 模块）

| 模块 | 默认候选 | 当前目标态倾向 | 必须监控 | 重新选型触发 |
|---|---|---|---|---|
| **Ability command ingest** | request entity / owner buffer / NativeStream | Boundary request → Core command；禁止 OOP callback | command count、structural mutation | command count > 1000/frame |
| **Effect command fan-in** | NativeStream + deterministic sort | 高规模并行 stream + 按 target ASC 排序 merge | stream segment count、merge cost | merge cost > spec eval cost |
| **Instant effect apply** | IJobChunk / IJobEntity + chunk-local scratch | chunk 批处理，禁止 per-entity managed dispatch | chunk utilization、skip rate | utilization < 50% |
| **Active effect lifecycle** | owner-local DynamicBuffer + Enableable slot | 固定容量 slot（如 64），enableable 标记 active | buffer externalized ratio、expired slot ratio | externalized > 30% |
| **Granted tag/ability** | Enableable / owner-local bitset / delta stream | 高频状态优先 enableable；低频用 tag delta | enableable wait time、tag delta count | enableable sync > 1ms |
| **Attribute delta** | per-target buffer + deterministic reduce | 按 target 分组后线性 reduce；禁止无序 writer | delta count per frame、merge order | random lookup 成本 > 顺序 merge |
| **Cue / Presentation** | presentation outbox → log marker / Entities Graphics | Boundary 派生，Core 不依赖资源 | outbox count per frame | outbox count > fact count (异常重复) |
| **Debug telemetry** | sampled counter buffer + periodic export | 可关闭、低侵入、分组归因 | overhead (ns per counter) | overhead > 1% coreTickMs |
| **Execution calculation** | generated static switch / FunctionPointer + batch | 批处理粒度（同 calculation type 一批），避免 per-entity invoke | invoke count、batch size | 非批处理 invoke > 1000/frame |

## 官方证据

所有选型必须追溯到 `01-12` 主题文档中的具体规则编号和官方文档路径。

## 使用模式与反模式

**正确模式：**
- 先分类数据性质，再选 API
- 每个 API 选择附带监控指标和重新选型阈值
- proof 可以用简单 API（Singleton DynamicBuffer），但必须标为 proof-only

**反模式：**
- 把 DynamicBuffer / ECB / Enableable 固化为唯一答案
- 以 proof API 作为 scale-ready 方案
- 拒绝某 API 时不写具体原因
- 用 `avgTickMs` 替代 API 选型证据
- **使用 `IAspect` 包装组件访问**：API 已在 Entities 1.4.6 标记为 deprecated，禁止新代码使用。直接 component 访问是官方推荐替代方案（参见 `CASE-13`、`P1-09`）

## EX-GAS 项目解读

### 当前 API 偏差

当前实现中的典型 API 选型偏差：

| 当前实现 | 问题 | 目标态替代 |
|---|---|---|
| Instant GE → runtime entity → destroy | entity churn；结构变化散落 | EffectCommand → Spec + Delta (无 entity) |
| `ToEntityArray` 全量 scan (Driver) | 每 tick O(n) 全扫描 | stable read model + chunk/jobified cursor |
| `CGameplayEventBus` singleton buffer | 全局串行瓶颈 | typed facts per-owner buffer |
| managed `AbilityConfig` lookup | hot path 托管分配 | BlobAsset / generated static lookup |
| `EntityHelper` 全局 ECB | 隐式结构变化，不可追踪 | 显式 ECB playback phase |

### 选型表必须作为任务交还条件

每个 Runtime Core 任务交还 API 选型表时必须包含：

```markdown
## API 选型表
| 业务链路 | 数据性质 | 采用 API | 拒绝 API | 官方依据 | 重新选型触发 | Proof-only? |
|---|---|---|---|---|---|---|
| Instant GE apply | deterministic result | IJobChunk + Delta buffer | request entity (entity churn) | QRY-01, SC-01 | buffer spill > 50% | 否 |
| Effect fan-in | deterministic command | NativeStream + sort merge | singleton buffer (串行) | NAT-02, NAT-03 | merge > 1ms | 否 |
```

## 常见陷阱

1. **Proof API 被当成最终方案**：Singleton buffer 用于 proof 但不可规模化
2. **Allocator 选择未明确定义**：Temp 还是 TempJob 还是 Persistent
3. **确定性假设未验证**：假设 foreach 顺序是稳定的
4. **重新选型条件缺失**：不知道什么时候该从简单方案切换到复杂方案
5. **EntityManager.GetComponentData 误用于热路径读取单例**：`EntityManager.GetComponentData<T>()` 触发 sync point（等待所有写 T 的 job 完成），而 `SystemAPI.GetSingleton<T>()` 不触发 sync。主线程读取单例配置时优先使用 SystemAPI 避免不必要同步（参见 `SEL-05`）

## 验收指标

1. 每个 Runtime Core 任务交还 API 选型表
2. 压测报告能说明哪些 API 仍是 proof-only，哪些已经 scale-ready
3. 触发重新选型阈值时，任务树能新增或重排对应任务
