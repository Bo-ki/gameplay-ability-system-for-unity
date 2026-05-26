# FSM-04: 单 entity 上多 FSM 叠加时评估拆分 entity

**严重度**: P1
**Primary Owner**: 状态机策略
**来源**: `state-machine.md` implementation issues 章节

## 规则声明
同一 entity 上存在多个独立 FSM 时（如 Ability 状态机 + Buff 状态机 + 生存状态），若 FSM 之间的状态排列组合导致数据碎片化或查询复杂度增加，应评估拆分 entity。

## 为什么
多 FSM 叠加时状态可能不互斥 → archetype 排列数相乘增长。例如：2 个独立 FSM 各有 3 个状态 → 最多 9 种状态组合。同一 entity 承载过多状态 → per-entity 数据结构膨胀 → 缓存局部性下降。拆分 entity 使每个 entity 只承载一个 FSM，chunk 内 entity 状态更一致。

## EX-GAS 诊断
ASC entity 当前承载 ActiveEffect 生命周期状态 + Ability 激活状态 + 生存状态 + Status Effect 标记。应评估拆分：ActiveEffect 状态由 effect store entity 承载；Ability 状态由 ability entity 承载；ASC 只保留生存状态和 status flag bit field。

## 检查方法
审计 entity 上 component 集合中状态相关 component 的数量 > 3 时 → 标记为拆分候选。
