# FSM-05：Boolean 状态按查询语义选择 bit field、enum 或 enableable

**严重度**：P1
**Primary Owner**：状态机策略
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `state-machine.md`、`performance-chunk-allocations.md`、`components-enableable-use.md`

## 规则声明
多个标记经常一起读取/写入且不需要 EntityQuery 单独过滤时，优先评估一个 bit field；互斥状态用 enum；需要独立 query 过滤或 worker 切换的状态评估 enableable component。不存在“>3 必须合并”的官方门槛。

## 为什么
普通 tag 类型会产生潜在 archetype 组合；enableable component 类型预先存在时，启停不会创建新 archetype，但会增加 mask/filter与组件依赖。bit field 避免这些类型/掩码，却让独立 bit 无法直接用于 query，并可能扩大写冲突。

## EX-GAS 诊断
Stunned/Silenced 等若总是一起读取可用 `CStatusFlags`；若系统只想高效查询某一稀疏状态，单独 enableable 可能更合适。

## 检查方法
按每个状态的查询频率、共读写比例、切换频率和依赖冲突比较，不按 boolean 数量直接判定。
