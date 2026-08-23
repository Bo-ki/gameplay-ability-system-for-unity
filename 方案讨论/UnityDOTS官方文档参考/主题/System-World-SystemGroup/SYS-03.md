# SYS-03: 系统数量是成本源，避免不必要的 system 拆分

**严重度**: P1
**Primary Owner**: System-World-SystemGroup
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3 / Entities 1.4.6
**官方来源**: `systems-optimizing.md`

## 规则声明
每个活跃 System 都有固定 overhead，包括 system 自身的 entity/type handle 访问、所用 lookup 的更新，以及 `Dependency` 计算。功能相近且共享数据遍历的 system 应评估合并，但不要仅为减少数量破坏清晰的依赖或更新频率边界。

## 为什么
官方只说明 overhead 随活跃 system 数量增长，没有给出“100+ system 即毫秒级”的通用阈值。成本还取决于匹配检查、调度方式、平台和每个 system 实际使用的 handle/lookup，应在 Profiler 中分辨 system overhead 与 job 工作量。

## EX-GAS 诊断
Debugger 输出 `ActiveSystemCount` 与各 system 主线程 marker。合并评估由 Profiler 证据、共享遍历机会和依赖关系触发，不用“每 phase 超过 5 个”作为硬门槛。

## 检查方法
- 在 Debugger 中查看按 `core/diagnostics/presentation/demo` 分组的 System 计数
- Code review 时注意为单一职责创建新 system 的冲动
