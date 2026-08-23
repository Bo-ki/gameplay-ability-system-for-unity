# 10B-03 羁绊与 Runtime 基础设施

本页是导航：

- [10B-03A 羁绊系统](10B-03A-羁绊系统Spec.md) 定义阵容变化如何产生稳定 GE 应用/移除意图。
- [10B-03B Runtime 基础设施](10B-03B-Runtime基础设施Spec.md) 定义 Session runner、ingress 和 immutable observation。

羁绊不是另一个 GAS Runtime，AutoChess runner 也不拥有内部 SystemGroup。二者只能通过通用 Definition/Command/Boundary contract 集成。

分层边界：`03B` 是 Tier A/B 共用的真实战局控制主线；`03A` 的羁绊是 Tier C 扩展样例，只验证公共契约的可扩展性，不得写成当前 9101–9104 主战已覆盖能力。
