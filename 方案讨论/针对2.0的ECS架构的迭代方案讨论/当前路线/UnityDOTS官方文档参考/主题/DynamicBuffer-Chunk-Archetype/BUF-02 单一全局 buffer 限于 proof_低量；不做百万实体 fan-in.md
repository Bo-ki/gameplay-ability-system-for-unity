# BUF-02: 单一全局 buffer 限于 proof/低量；不做百万实体 fan-in

**严重度**: P1
**Primary Owner**: DynamicBuffer-Chunk-Archetype
**来源**: `components-buffer-introducing.html`

## 规则声明
单一全局 `DynamicBuffer` 不能作为百万实体并行 fan-in 的唯一通道。必须使用 `NativeStream`（并行 writer）或 per-thread ECB 替代。

## 为什么
单一全局 buffer 作为所有 entity 的并行写入目标时，写入顺序不确定、内存写入互斥、失去并行意义。DynamicBuffer 无 NativeContainer 调度限制但不等于全局百万级 fan-in 无瓶颈。

## EX-GAS 诊断
EffectCommand fan-in 使用 `NativeStream` + deterministic merge，而非全局 `DynamicBuffer`。

## 检查方法
审查所有使用 `SystemAPI.GetSingletonBuffer<...>()` 或全局 entity 上 DynamicBuffer 的写入路径，超过 100 并行 writer 则违规。
