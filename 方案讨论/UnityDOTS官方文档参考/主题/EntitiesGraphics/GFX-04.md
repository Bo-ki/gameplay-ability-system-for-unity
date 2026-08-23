# GFX-04: 无头模式与有头模式的 Core simulation hash 必须相同

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: EX-GAS 当前无头/有头验收架构；Entities Graphics 1.4.19
**严重度**: P1
**Primary Owner**: EntitiesGraphics
**来源**: EX-GAS AutoChess 无头验收设计（非 Unity 官方通用要求）

## 规则声明
无论是否启用 Entities Graphics，Core simulation 的 battle hash 必须一致。表现层（Entities Graphics binding）不能反向修改 Core state。无头 Demo 通过 Presentation Outbox -> Log Marker 链路验证表现正确性。

## 为什么
1) 无头模式是自动验收的基础，必须与有头模式共享相同 Core simulation；2) 表现 entity 创建/销毁如果影响 Core random state 或 Attribute 值，破坏无头验证；3) Presentation Outbox 确保表现请求被记录但不会反馈修改 Core。

## EX-GAS 诊断
压测报告对比有头/无头模式下的 battle hash；Debugger 输出 outbox count 和 cue count 用于验证。

## 检查方法
检查表现层 system 是否对 Core component 有写访问；确认 Core simulation 在 Graphics 启用/禁用时输出相同 hash。
