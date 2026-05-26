# ECB-01: ECB 是延迟结构变化工具，不是 Gameplay Event Bus

**严重度**: P0
**Primary Owner**: 结构变化-ECB
**来源**: `systems-entity-command-buffer.md`

## 规则声明

ECB 的设计目标是延迟录制和批量提交结构变化。不应将 ECB 用作 Gameplay Event Bus、消息队列或跨 system 通信通道。

## 为什么

ECB playback 本身就是结构变化，带来 sync point。用它传递非结构变化的消息会引入不必要的 sync point，同时掩盖原本应在数据流中清晰表达的事件传递。

## EX-GAS 诊断

EventBus 在当前实现中同时承载 gameplay event 和结构变化边界，职责不单一。目标态应将结构变化使用 ECB，gameplay 事件使用 TypedFact/NativeStream。

## 检查方法

ECB command 的语义审查：如果录制的命令不是 CreateEntity/AddComponent/RemoveComponent/DestroyEntity 等结构变化操作，而是单纯的数据消息传递 → 违规。
