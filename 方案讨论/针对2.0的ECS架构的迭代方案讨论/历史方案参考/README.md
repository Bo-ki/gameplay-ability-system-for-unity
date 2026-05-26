# 历史方案参考

本目录保存 2.0 ECS 架构迭代讨论中的预研讨论设计方案。它们是设计素材，不是当前路线入口。
用户会根据情况添加参考, 但这些参考都是基于2.0初版进行的延伸讨论, 仅供思路和设计参考.

## 使用口径

1. 只参考其中的目标态信号、代码块、图示和边界思路。
2. 任何“当前问题诊断”都必须先对照当前代码和 [当前架构事实](../00-当前架构事实/README.md)。
3. 方案10/11已瘦身，保留 OOP Shell、Luban/Blob、Burst-friendly、事件/日志、调度集中化等目标态信号；旧主链诊断不再作为当前事实。
4. 需要追溯某条目标态设计吸收了哪些方案时，优先读 [目标态架构共识](../01-目标态架构共识/README.md) 中对应 Spec 的 `历史方案定位`。

## 参考重点

|方案|当前参考重点|
|-|-|
|方案1-6|早期 ECS 数据化、Tag、Attribute、GE、Ability command 思路|
|方案7-9|ECS-first 目标态、Luban/Blob、outbox、SystemGroup、presentation boundary|
|方案10|OOP Shell、结构化日志、Luban -> Blob、Burst-friendly、显式调度信号|
|方案11|OOP facade、事件 buffer、Tag/Luban/GE Blob、目标态收益信号|
|方案12-13|当前 demo 业务预演、验收链路和业务形态压力测试信号|
|方案14-15|AutoChess 无头验收、四层工程模型、Debugger、Luban SourceGenerator 和 Spec 图表表达|

