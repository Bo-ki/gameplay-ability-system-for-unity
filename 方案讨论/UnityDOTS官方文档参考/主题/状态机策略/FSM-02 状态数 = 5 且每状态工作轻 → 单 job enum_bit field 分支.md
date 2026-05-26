# FSM-02: 状态数 <= 5 且每状态工作轻 → 单 job enum/bit field 分支

**严重度**: P1
**Primary Owner**: 状态机策略
**来源**: `state-machine.md` Per-FSM Data Branching 策略定义

## 规则声明
状态数不超过 5 且每个状态内的计算工作量较轻时（读取/写入少量字段、无复杂循环），默认使用单 job 内 enum switch 或 bit field 分支（Per-FSM Data Branching）。

## 为什么
单 job 内 switch 避免多 system 拆分的固定开销（TypeHandle 刷新 + JobHandle 依赖链），且无需结构变化。状态数 <= 5 时，分支预测失败代价可忽略。当状态数增长到 6+ 或某状态内计算复杂时，才应考虑拆分为独立 job/system。

## EX-GAS 诊断
ActiveEffect 生命周期 5 个状态 + 每状态工作轻（更新 remaining time、period cursor、stack count）→ 正确使用 enum switch。若某状态（如 Active）需要遍历所有 modifier 执行复杂计算 → 评估是否独立为 Enableable-driven job。

## 检查方法
Code review 发现状态机实现：若状态数 <= 5 且每状态 body < 20 行 → 必须是单 job switch；否则需附带性能证明说明为何需要多 system。
