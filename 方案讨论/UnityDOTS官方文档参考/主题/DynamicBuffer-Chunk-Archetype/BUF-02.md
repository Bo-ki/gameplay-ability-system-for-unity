# BUF-02：多 writer fan-in 不直接并行写单一全局 DynamicBuffer

**严重度**：P1
**Primary Owner**：DynamicBuffer-Chunk-Archetype
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`；Collections `2.6.6`
**官方来源**：Entities `components-buffer-jobs.md`；Collections `parallel-readers.md`

## 规则声明
多个 worker 产生数据时，不把同一个 singleton DynamicBuffer 作为无同步并行 append 目标。根据语义使用 NativeStream、可并行写容器后 merge、ECB ParallelWriter（仅实体命令），或让单 writer 串行汇总。

## 为什么
ECS 仍会保护同一 buffer 的写依赖，DynamicBuffer 并非无约束并行容器。问题取决于 writer 数、数据量和同步方式，不存在官方“超过 100 writer”或“百万实体”阈值。

## EX-GAS 诊断
EffectCommand 并行收集优先 NativeStream + 明确 merge；低频主线程/single-job 的 singleton buffer 可保留，并由 Profiler 验证。

## 检查方法
审查 singleton buffer 的 writer 数、JobHandle、顺序语义和 Profiler 成本，不按固定 entity 数直接判定。
