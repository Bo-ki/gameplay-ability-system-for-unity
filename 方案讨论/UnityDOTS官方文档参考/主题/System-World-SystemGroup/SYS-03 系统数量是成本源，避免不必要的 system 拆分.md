# SYS-03: 系统数量是成本源，避免不必要的 system 拆分

**严重度**: P1
**Primary Owner**: System-World-SystemGroup
**来源**: `systems-optimizing.html`

## 规则声明
每个活跃 System 有 TypeHandle 刷新、Lookup 创建、Dependency 链三种固定开销。功能相近且共享 query 的 system 应评估合并，而非为每个细小职责创建一个新 system。

## 为什么
N 个 system = N 次 type handle 操作 + N 次 lookup 更新 + N 个 JobHandle 链节点。在 100+ system 规模下，这些开销累计可达毫秒级。

## EX-GAS 诊断
Debugger 应输出 `ActiveSystemCount` 分组统计。Runtime Core 每 phase 超过 5 个 system 时触发合并评估。

## 检查方法
- 在 Debugger 中查看按 `core/diagnostics/presentation/demo` 分组的 System 计数
- Code review 时注意为单一职责创建新 system 的冲动
