# FSM-03: 大量 entity 长期 idle → Enableable 整 chunk 跳过

**严重度**: P1
**Primary Owner**: 状态机策略
**来源**: `state-machine.md` Per-State Data Branching 策略；`components-enableable-use.md`

## 规则声明
当大量 entity 的某状态为"空闲/idle"且该状态不需要执行任何逻辑时，使用 IEnableableComponent 标记是否激活，使 EntityQuery 在 chunk 级别跳过全部 disabled entity。

## 为什么
Enableable component 的 disabled 状态在 chunk 级别过滤——chunk 内全部 entity 均为 disabled 时，整 chunk 不进入 job Execute 方法。相比 enum/bit field 方案（全量 entity 进入 job 后每个 entity 要检查分支），Enableable 方案在 idle 占绝大多数时节省全部 job 执行时间。切换本身不产生结构变化（O(1) SetComponentEnabled）。

## EX-GAS 诊断
Ability 激活状态——大量 ASC entity 的 Ability 长期处于 Idle（非战斗状态），只有少量被激活。当前架构未使用 Enableable 表达，应迁移。

## 检查方法
Profiler 检查存在"大量 entity 进入 job 后立即 if (state == Idle) return"的模式 → 应改用 Enableable。
