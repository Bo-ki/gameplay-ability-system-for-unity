# FSM-01: 高频状态切换禁用 Tag Component add/remove

**严重度**: P0
**Primary Owner**: 状态机策略
**来源**: `state-machine.md`（Per-State Data Branching 策略）；`PRF-03`

## 规则声明
每帧可能发生切换的状态标记不得使用 Tag Component 的 AddComponent / RemoveComponent 实现。必须使用 IEnableableComponent 或 enum/bit field 替代。

## 为什么
每次 Add/Remove Tag Component = 1 次 archetype 迁移 = 1 次结构变化。高频切换时，每帧多次结构变化累积为 P0-级 sync point 阻塞。N 个独立 tag → 最多 2^N 种 archetype 排列，archetype 爆炸使 EntityQuery 遍历退化到 O(archetype_count)。

## EX-GAS 诊断
ActiveEffect 生命周期切换（PendingApply/Active/Inhibited/PendingRemove/Removed）若用 5 个独立 tag → 32 种 archetype 排列。当前已使用 `BActiveEffectSlot.State` enum，正确避免此问题。

## 检查方法
Grep 搜索 Runtime Core 中 `AddComponent<` / `RemoveComponent<` 在每帧可能执行的路径；Archetype Window 检查 archetype 总数是否接近 entity 总数。
