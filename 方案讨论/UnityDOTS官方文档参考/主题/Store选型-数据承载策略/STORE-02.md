# STORE-02：影响 battle hash 的输出必须定义稳定全序或顺序无关归约

**严重度**：P0
**Primary Owner**：Store选型-数据承载策略
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`；Collections `2.6.6`
**官方来源**：Collections `parallel-readers.md`；Entities `systems-entity-command-buffer-playback.md`

## 规则声明
顺序会影响结果的 gameplay 输出必须按稳定、唯一的业务全序消费；或证明归约在数学和表示层面与顺序无关。只按 target、ForEachCount、chunk index 或物理 append 顺序不构成完整证明。

## 为什么
并行 writer 的完成/追加顺序不确定，同 target 下多个命令仍需 tie-breaker。ECB 的 `ChunkIndexInQuery` sort key 解决一次录制内 playback 与调度完成顺序的关系，不保证跨 World/replay 的业务 identity。

## EX-GAS 诊断
EffectCommand/AttributeDelta/TypedFact 分别声明 total key，例如 `(Phase, TargetStableId, SourceStableId, Kind, ProducerSequence)`，并定义重复键与浮点归约策略。

## 检查方法
列出 hash 输入的规范化顺序和稳定 ID 来源；改变 worker 数、batch size 和调度时机后结果仍一致。
