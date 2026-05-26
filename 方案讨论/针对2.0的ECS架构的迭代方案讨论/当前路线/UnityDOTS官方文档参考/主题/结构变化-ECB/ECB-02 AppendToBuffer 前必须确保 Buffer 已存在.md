# ECB-02: AppendToBuffer 前必须确保 Buffer 已存在

**严重度**: P0
**Primary Owner**: 结构变化-ECB
**来源**: `components-buffer-command-buffer.md`

## 规则声明

使用 `ECB.AppendToBuffer<T>(entity, element)` 前，必须确保目标 entity 已拥有 `DynamicBuffer<T>` 组件。如果 buffer 可能不存在，必须先通过 `ECB.AddComponent<T>(entity, new T())` 添加。

## 为什么

`AppendToBuffer` 在 playback 时不会自动创建 buffer。如果 buffer 组件不存在，playback 执行 `AppendToBuffer` 会失败。`SetBuffer` 同理——它设置整个 buffer，但也不自动创建组件。

## EX-GAS 诊断

EffectCommand fan-in 中多个并行 job 通过 ECB 向同一 ASC entity 的 outbox buffer 追加元素时，必须确保 buffer 组件在首个 `AppendToBuffer` 前已存在。

## 检查方法

搜索 `ECB.AppendToBuffer` / `ecb.AppendToBuffer` 的出现，逐处确认上游是否有 `ECB.AddComponent<TBuffer>` 或 playback 前 entity 已静态持有该 buffer 组件。
