# MAT-05：顺序敏感的确定性 fan-in 必须使用稳定全序

**严重度**：P0
**Primary Owner**：Mathematics-确定性
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`；Collections `2.6.6`；Mathematics `1.3.3`
**官方来源**：Collections `parallel-readers.md`（并行写入顺序不确定）；Entities `systems-entity-command-buffer-playback.md`（ECB sort key 语义）

## 规则声明
影响 battle hash/replay 且结果对顺序敏感的并行 fan-in，必须在消费前建立稳定全序，或证明其归约在数学和数据表示上与顺序无关。全序键必须包含稳定业务 ID 与处理所有相等项的 tie-breaker。

## 为什么
并行调度顺序不稳定；只按 target 排序会保留同 target 项的不确定相对顺序。`ChunkIndexInQuery` 适合让一次查询录制的 ECB playback 不依赖 job 调度完成顺序，但 chunk 编排不是跨 World/跨 replay 的业务稳定标识。

## EX-GAS 诊断
EffectCommand 建议键为 `(Phase, TargetStableId, SourceStableId, CommandKind, ProducerSequence)`。若同键仍可重复，继续增加稳定序号或定义交换/合并规则。浮点 reduce 还需固定归约顺序或使用量化/定点表示。

## 检查方法
审计 `ParallelWriter`、NativeStream、ECB 和并行 reduce：列出完整键、稳定 ID 来源、tie-breaker 和浮点策略；仅写“按 target ASC”不通过。
