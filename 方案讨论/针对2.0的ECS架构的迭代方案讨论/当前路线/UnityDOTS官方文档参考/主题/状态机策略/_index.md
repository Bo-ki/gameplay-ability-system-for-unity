# 状态机策略

## 职责边界

覆盖 Unity Entities 官方 `state-machine.md` 定义的三种 FSM（有限状态机）实现策略：Per-State Data Clustering、Per-State Data Branching、Per-FSM Data Branching。本领域不覆盖通用业务逻辑分支（if/else、switch），仅关注 ECS 架构下由状态切换引发的 archetype 变化、chunk 遍历和数据结构设计决策。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解三种 FSM 策略、选择决策树、性能影响矩阵）
2. **核心规范**（按严重度）
   - `FSM-01: 高频状态切换禁用 Tag Component add_remove.md` — P0: 高频状态切换禁用 Tag Component add/remove
   - `FSM-02: 状态数 <= 5 且每状态工作轻 → 单 job enum_bit field 分支.md` — P1: 状态数 <= 5 且每状态工作轻 → 单 job enum/bit field 分支
   - `FSM-03: 大量 entity 长期 idle → Enableable 整 chunk 跳过.md` — P1: 大量 entity 长期 idle → Enableable 整 chunk 跳过
   - `FSM-04: 单 entity 上多 FSM 叠加时评估拆分 entity.md` — P1: 单 entity 上多 FSM 叠加时评估拆分 entity
   - `FSM-05: 同 entity 上 > 3 个独立 boolean 状态标记 → 合并为 bit field _ enum.md` — P1: 同 entity 上 > 3 个独立 boolean 状态标记 → 合并为 bit field / enum
   - `FSM-06: 默认使用单 job enum 分支；仅 profiler 证明必要时切换更复杂方案.md` — P2: 默认使用单 job enum 分支；仅 profiler 证明必要时切换更复杂方案
3. **模式与反模式**（在 API 解读中详细说明）
   - 模式 A: ActiveEffect 生命周期（Per-FSM Data Branching）
   - 模式 B: Ability 激活（Per-State Branching via IEnableableComponent）
   - 模式 C: Buff/Debuff 标记（Bit Field 变体）
4. **拓展阅读**（按需）
   - Enableable component 选型 → `Enableable-Component选型/_index.md`
   - 结构变化与 archetype 管理 → `结构变化-ECB/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | 三种 FSM 策略详解 + EX-GAS 状态机对应关系 + 推荐模式/反模式 |
| `FSM-01: 高频状态切换禁用 Tag Component add_remove.md` | 规范 P0 | 高频状态切换禁用 Tag Component add/remove |
| `FSM-02: 状态数 <= 5 且每状态工作轻 → 单 job enum_bit field 分支.md` | 规范 P1 | 状态数 <= 5 且每状态工作轻 → 单 job enum/bit field 分支 |
| `FSM-03: 大量 entity 长期 idle → Enableable 整 chunk 跳过.md` | 规范 P1 | 大量 entity 长期 idle → Enableable 整 chunk 跳过 |
| `FSM-04: 单 entity 上多 FSM 叠加时评估拆分 entity.md` | 规范 P1 | 单 entity 上多 FSM 叠加时评估拆分 entity |
| `FSM-05: 同 entity 上 > 3 个独立 boolean 状态标记 → 合并为 bit field _ enum.md` | 规范 P1 | > 3 个独立 boolean 状态标记 → 合并为 bit field / enum |
| `FSM-06: 默认使用单 job enum 分支；仅 profiler 证明必要时切换更复杂方案.md` | 规范 P2 | 默认使用单 job enum 分支；profiler 驱动切换 |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `FSM-01` | P0 | 高频状态切换禁用 Tag Component add/remove | Grep `AddComponent<`/`RemoveComponent<` 在每帧可能执行的路径 |
| `FSM-02` | P1 | 状态数 <= 5 且工作轻 → 单 job enum switch | Code review；若状态数 <= 5 且 body < 20 行必须是单 job switch |
| `FSM-03` | P1 | 大量 idle entity → Enableable 整 chunk 跳过 | Profiler 检查存在大量"进入 job 后立即 if (state == Idle) return" |
| `FSM-04` | P1 | 多 FSM 叠加评估拆分 entity | 审计 entity 上状态相关 component 数量 > 3 → 拆分候选 |
| `FSM-05` | P1 | > 3 个独立 boolean 标记 → 合并 bit field | 搜索独立 boolean 语义 IComponentData 超过 3 个 |
| `FSM-06` | P2 | 默认单 job enum 分支；profiler 驱动切换 | PR review 若非 Per-FSM Data Branching 需附带 profiler 证据 |

## 跨主题引用

| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `FSM-01` | Enableable-Component选型 | 高频状态开关选型 |
| `FSM-05` | DynamicBuffer-Chunk-Archetype | Archetype 爆炸防范 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| Enableable Component 详细机制 | `Enableable-Component选型/_index.md` |
| 结构变化与 archetype 管理 | `结构变化-ECB/_index.md` |
| DynamicBuffer 数据承载 | `DynamicBuffer-Chunk-Archetype/_index.md` |

## 验收指标

1. GAS Runtime Core 中高频（> 1 次/帧）状态切换不使用 Tag Component add/remove。
2. ActiveEffect 生命周期保持 enum + 单 job 模式（符合 FSM-02）。
3. Status Effect 类标记（> 3 种）合并为 bit field 或 enum（符合 FSM-05）。
4. 大量 idle entity 的场景（如 Ability 激活）使用 Enableable 整 chunk 跳过（符合 FSM-03）。
5. Debugger 输出 archetype count 和 enableable wait time，用于 FSM 方案决策验证。
6. 新引入的状态机实现默认从 Per-FSM Data Branching 开始；非默认方案附带 profiler 证据（符合 FSM-06）。
7. 单 entity 上 FSM 相关 component 数量不超过 3 个（符合 FSM-04）。
