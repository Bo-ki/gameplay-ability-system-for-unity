# FSM-02：状态逻辑轻且共享数据时优先单 job enum 分支

**严重度**：P1
**Primary Owner**：状态机策略
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `state-machine.md`（Per-FSM data branching）

## 规则声明
当状态逻辑共享主要数据、没有大量 idle/no-op entity、单 job 依赖不会阻塞其他工作时，优先用 enum/switch 的 Per-FSM branching。状态数量和代码行数仅是评审线索，不设官方或全局硬阈值。

## 为什么
单 job 减少调度与重复遍历，但状态数不是决定分支预测或性能的充分条件。“<=5”“6+ 才拆”“body <20 行”均没有官方依据，必须用真实状态分布和目标 Player 基准裁决。

## EX-GAS 诊断
ActiveEffect lifecycle 可从 enum/switch 起步；若 Active 状态工作远重于其他状态或 idle 比例高，再比较 enableable/per-state jobs。

## 检查方法
记录各状态 entity 占比、每状态成本、job/依赖时间和 cache miss；不以源码行数判定实现。
