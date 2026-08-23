# PRF-13: 确定性输出不得依赖无序写入

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6；Collections 2.6.6
**严重度**: P0
**Primary Owner**: 数据流-系统生命周期
**来源**: Collections `parallel-readers.md`；Entities `systems-entity-command-buffer-playback.md`；EX-GAS battle hash/replay 约束

## 规则声明

影响 battle hash / replay 的结果（EffectCommand fan-in、TypedFact projection、AttributeDelta reduce）不得把线程调度或未定义的 chunk/容器迭代顺序当作业务顺序。输出必须使用稳定逻辑分区，或按稳定业务 total key（含 tie-breaker）归并。

## 为什么

无序写入在不同帧或不同硬件线程调度下产生不同输出顺序，导致 battle hash 不一致、replay 失败。即使单次执行看起来正确，多线程调度差异会在下次运行产生不同结果。

## EX-GAS 诊断

EffectCommand 来自多个并行 job 时，各 job 使用独立 ECB/输出容器。`EntityCommandBuffer.ParallelWriter.AppendToBuffer(chunkSortKey, ...)` 只能稳定单个 query 的录制域，不能独自保证跨 job 的业务全序；最终 merge 必须使用稳定业务 key 和 tie-breaker。TypedFact projection 同样遵循该规则。

## 检查方法

搜索所有 `ParallelWriter`、NativeStream 和并行 ECB 输出路径，确认其是否影响 replay/battle hash。若影响，验证逻辑分区映射或 total-key merge，并加入重复 key、跨 job 和不同 worker 数的确定性测试。
