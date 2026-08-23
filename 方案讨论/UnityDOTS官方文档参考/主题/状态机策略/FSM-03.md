# FSM-03：大量 idle entity 时评估 Enableable 查询过滤

**严重度**：P1
**Primary Owner**：状态机策略
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `state-machine.md`、`components-enableable-use.md`

## 规则声明
若大量 entity 长期 idle/no-op，且 active 状态可由一个独立 component 语义表达，评估 IEnableableComponent 以让 query 跳过 disabled entity；采用前必须比较 mask/filter、依赖等待和额外组件成本。

## 为什么
Enableable query 对每个 entity 检查 enable bit，并可在 chunk 内没有匹配项时跳过整个 chunk。它不是“所有 disabled 都自动整 chunk 跳过”，也不是无成本 O(1) 查询。切换本身不产生结构变化，且可在 worker thread 执行。

## EX-GAS 诊断
Ability active/cooldown 是候选。不要同时 Add/Remove 普通 `CActivatedAbilityTag`，否则抵消避免结构变化的目标。

## 检查方法
比较 enum early-return 与 enableable query 的执行 entity 数、chunk 跳过、主线程等待和总系统耗时。
