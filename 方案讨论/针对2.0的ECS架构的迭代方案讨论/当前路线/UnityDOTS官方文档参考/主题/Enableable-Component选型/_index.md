# Enableable-Component选型

## 职责边界

本主题覆盖 `IEnableableComponent` 的选型决策、操作方式、性能特征与约束。阐述为什么 Enableable 是高频状态切换的首选机制、何时仍应使用 Add/Remove Component、以及 Enableable 在实际使用中容易被忽视的成本（查询同步、random-access 开销、竞态条件）。

不覆盖 ECB 或结构变化的通用规则（参见《结构变化-ECB》），不覆盖非 ECS 的状态管理方式。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解 Enableable 三种操作方式和查询变体）
2. **核心规范**（按严重度）
   - `EN-01: 高频开关优先 Enableable，低频生命周期再考虑 Add_Remove.md` — P0: 高频开关优先 Enableable，低频生命周期再考虑 Add/Remove
   - `PRF-03: 禁止用 Tag Component 做高频状态标记.md` — P0: 禁止用 Tag Component 做高频状态标记
   - `EN-02: Enableable 查询成本和同步等待进入性能诊断.md` — P1: Enableable 查询成本和同步等待进入性能诊断
   - `EN-03: Random-Access Enableable 方法有额外开销；迭代优先.md` — P1: Random-Access Enableable 方法有额外开销；迭代优先
3. **模式与案例**
   - `CASE-06: 高频状态切换用 EnabledRefRW.md` — 高频状态切换用 EnabledRefRW
   - `CASE-20: 批量状态切换用 EnabledMask.md` — 批量状态切换用 EnabledMask
   - `CASE-22: EntityQueryMask O(1) 实体-Query 匹配检查.md` — EntityQueryMask O(1) 实体-query 匹配检查
   - `CASE-26: ChunkEntityEnumerator 标准 Enableable 感知迭代.md` — ChunkEntityEnumerator 标准 enableable 感知迭代
4. **拓展阅读**（按需）
   - 结构变化与 Enableable 的关系 → `结构变化-ECB/_index.md`
   - DynamicBuffer 数据承载 → `Store选型-数据承载策略/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | Enableable 概念、操作方式、EX-GAS 角色、选型决策树 |
| `EN-01: 高频开关优先 Enableable，低频生命周期再考虑 Add_Remove.md` | 规范 P0 | 高频开关优先 Enableable，低频再考虑 Add/Remove |
| `EN-02: Enableable 查询成本和同步等待进入性能诊断.md` | 规范 P1 | Enableable 查询成本和同步等待进入诊断 |
| `EN-03: Random-Access Enableable 方法有额外开销；迭代优先.md` | 规范 P1 | Random-Access Enableable 有额外开销；迭代优先 |
| `PRF-03: 禁止用 Tag Component 做高频状态标记.md` | 规范 P0 | 禁止用 Tag Component 做高频状态标记 |
| `CASE-06: 高频状态切换用 EnabledRefRW.md` | 模式 | 高频状态切换用 EnabledRefRW |
| `CASE-20: 批量状态切换用 EnabledMask.md` | 模式 | 批量状态切换用 EnabledMask |
| `CASE-22: EntityQueryMask O(1) 实体-Query 匹配检查.md` | 模式 | EntityQueryMask O(1) 匹配检查 |
| `CASE-26: ChunkEntityEnumerator 标准 Enableable 感知迭代.md` | 模式 | ChunkEntityEnumerator 感知迭代 |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `EN-01` | P0 | 高频开关优先 Enableable，低频再考虑 Add/Remove | 搜索高频路径中的 AddComponent/RemoveComponent |
| `EN-02` | P1 | Enableable 查询成本和同步等待进入诊断 | 检查同步 query 是否可使用 IgnoreFilter 或 Async 变体 |
| `EN-03` | P1 | Random-Access Enableable 有额外开销 | Grep SetComponentEnabled 在 job 中的调用 |
| `PRF-03` | P0 | 禁止用 Tag Component 做高频状态标记 | Archetype 窗口检查；审计无数据字段 IComponentData |

## 跨主题引用

其他主题引用了本主题的规则：
| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `EN-01` | 结构变化-ECB | Enableable 与结构变化的选型边界 |
| `EN-02` | Diagnostics-Debugger | 同步 query 触发的 sync point 归因 |
| `PRF-03` | DynamicBuffer-Chunk-Archetype | Tag component 排列爆炸诊断 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| ECB 与结构变化通用规则 | `结构变化-ECB/_index.md` |
| DynamicBuffer 数据承载 | `Store选型-数据承载策略/_index.md` |
| Archetype 碎片化诊断 | `DynamicBuffer-Chunk-Archetype/_index.md` |
| Enableable 的 Debugger 监控 | `Diagnostics-Debugger/_index.md` |

## 验收指标

1. 所有高频（每帧 > 1 次）状态切换使用 `IEnableableComponent` 而非 Add/Remove Component
2. Debugger 输出 `enableableToggleCount`、`enableableQuerySyncCount`、`archetypeCount`、`tagComponentCount`
3. Archetype 总数远小于 entity 总数（无 archetype 排列爆炸）
4. Runtime Core hot path 零 Add/Remove Component 用于高频状态 toggle
5. 所有 IJobChunk 中涉及 enableable component 的迭代使用 `ChunkEntityEnumerator`
6. Debugger 能区分 sync point 中由 enableable query 触发的比例
7. 不存在无数据字段的 `IComponentData` 被用于高频标记（使用 `IEnableableComponent` 替代）
