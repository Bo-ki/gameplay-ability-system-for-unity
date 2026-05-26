# 状态机策略: API 与 EX-GAS 解读

## 核心概念

### 三种实现策略

官方 `state-machine.md` 定义了三种在 ECS 中实现 FSM 的方法，按"状态切换频率"和"状态间数据差异度"区分：

| 策略 | 机制 | ECS 承载方式 | 适用场景 |
|---|---|---|---|
| **Per-State Data Clustering** | 按状态将 entity 分入不同 archetype | Tag Component / Shared Component | 低频状态切换（创建/销毁级别）、状态间数据差异大 |
| **Per-State Data Branching** | 同 archetype 内按状态过滤 | Enableable Component / Job per state | 高频状态开关、大量 idle entity 需要整 chunk 跳过 |
| **Per-FSM Data Branching** | 同 archetype 内单 job 内分支 | enum/bit field in single job | 状态数少（<= 5）、每个状态工作量轻 |

### 策略选择决策树

```
状态切换频率？
  低频（创建/销毁级别）→ Per-State Data Clustering
  高频（每帧可能切换）→ 看 entity 数量
    大量 entity 长期 idle → Per-State Data Branching (Enableable 整 chunk 跳过)
    状态数 ≤ 4 且每状态工作轻 → Per-FSM Data Branching (单 job enum switch)
```

### 性能影响矩阵

| 实现问题 | 影响策略 | 监控工具 |
|---|---|---|
| 结构变化 | Per-State Clustering（高频切换时） | CPU Profiler / Structural Changes Profiler |
| 数据碎片化 | Per-State Clustering（状态多、entity 少时） | Archetype Window |
| 不必要的数据获取 | 全部策略（idle entity 多时） | VTune / Instruments cache miss |
| 重复数据获取 | Per-State Branching（多状态时） | VTune / Instruments cache miss |
| Job 开销 | Per-State Clustering + Branching（多状态时） | Profiler job scheduling |
| 复杂依赖 | 全部策略（跨状态访问时） | Profiler job dependencies |
| 触发响应式系统 | Per-State Branching + Per-FSM Branching | Systems Window / Journaling |

---

## EX-GAS 项目解读

### 当前 GAS 状态机对应关系

| GAS 状态机 | 当前表达 | 推荐目标态 | 理由 |
|---|---|---|---|
| ActiveEffect 生命周期 (5 states) | `BActiveEffectSlot.State` enum | 保持 enum + 单 job switch | 已是最佳实践，符合 FSM-02 |
| Ability 激活/冷却 | 独立 component + manual tracking | `IEnableableComponent` | 大量 idle，需整 chunk 跳过，符合 FSM-03 |
| Unit Alive/Dead | 未统一 | `IEnableableComponent` | 大量可能同时死亡，符合 FSM-03 |
| Status Effect 标记 | 各自独立 component | bit field in `CStatusFlags` | 避免 archetype 爆炸，符合 FSM-05 |
| Tag granted by effect | 各自独立 tag | 评估 bit field 或 Chunk Component | 取决于查询模式 |

### 推荐模式

**模式 A：ActiveEffect 生命周期（Per-FSM Data Branching）**
```
BActiveEffectSlot.State: enum { PendingApply, Active, Inhibited, PendingRemove, Removed }
单 job 内 switch(State) { case Active: ... case Inhibited: ... }
```

**模式 B：Ability 激活（Per-State Branching via IEnableableComponent）**
```
CAbilityActive : IEnableableComponent  // enabled = 正在激活/冷却
CActivatedAbilityTag : IComponentData  // 只在非 Idle 时存在
```

**模式 C：Buff/Debuff 标记（Bit Field 变体）**
```
struct CStatusFlags : IComponentData {
    BitField32 flags;  // Stunned=1, Silenced=2, Disarmed=4, Slowed=8, Burning=16
}
```

### 反模式

- **反模式 A**：为每个状态创建独立 System（5 个状态 = 5 个 System 固定开销）
- **反模式 B**：用 Tag Component 做高频状态标记（每次切换 = archetype 迁移）
- **反模式 C**：盲目使用 Enableable 替代 Add/Remove（低频且数据差异大时反而更差）
- **反模式 D**：不考虑 Enableable 查询成本（全部 entity 均 enabled 时纯开销）

### 多 FSM 叠加处理方针

- ASC entity 只承载少量核心 FSM（ActiveEffect life cycle、Ability commit）
- 大量临时状态标记（buff/debuff）统一用 bit field 表达
- 拆分的 entity 通过 owner reference 关联，互不重叠

---

## 常见陷阱

1. **"我这个状态机只有两个状态，用 bool Tag Component 没事"** — 高频 toggle 每次仍是结构变化
2. **"Enableable Component 可以不加区分地替代所有 Tag Component"** — 全部 enabled 时过滤成本是纯开销
3. **"Per-FSM Data Branching 就是普通 switch，不够 ECS 原生"** — 官方将其列为三种正式策略之一
4. **"我把所有状态标记合并到一个巨大的 bit field 里"** — 超过 64 标记或分组语义差异大时应拆分
5. **"多 FSM 放在同一 entity 上方便管理"** — 多 FSM 叠加使 archetype 排列数相乘增长

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则编号 |
|---|---|---|
| `state-machine.md` | 三种 FSM 策略完整论述、问题矩阵、监控工具 | FSM-01, FSM-02, FSM-03, FSM-04, FSM-06 |
| `performance-chunk-allocations.md` | Archetype 爆炸和数据碎片化详解 | FSM-05 |
| `optimize-structural-changes.md` | 结构变化性能对比表（enableable 0.03ms vs IJobEntity 170ms） | FSM-01 |
| `components-enableable-use.md` | Enableable 查询 cost 和 sync point 风险 | FSM-01, FSM-03 |
