# PRF-13: 确定性输出不得依赖无序写入

**严重度**: P0
**Primary Owner**: 数据流-系统生命周期
**来源**: `systems-version-numbers.md` + 确定性 replay 架构要求

## 规则声明

影响 battle hash / replay 的结果（EffectCommand fan-in、TypedFact projection、AttributeDelta reduce）不得使用无序 `ParallelWriter` 或未排序的 foreach chunk 顺序输出。并行 fan-in 后必须按确定键（如 sortKey + entity index）排序 merge。

## 为什么

无序写入在不同帧或不同硬件线程调度下产生不同输出顺序，导致 battle hash 不一致、replay 失败。即使单次执行看起来正确，多线程调度差异会在下次运行产生不同结果。

## EX-GAS 诊断

EffectCommand 从多个并行 job（Attribute system、GE system、Tag system）向同一 ASC entity 的 outbox buffer 追加输出时，必须使用 `ECB.AppendToBuffer(sortKey, entity, element)`（CASE-47）保证确定性排序。TypedFact projection 的 reduce 阶段也需确定性 merge 策略。

## 检查方法

搜索所有 `NativeList.ParallelWriter` / `NativeHashMap.ParallelWriter` / `Concurrent` 输出路径 → 确认其写入是否影响确定性输出（replay/battle hash）。若是 → 必须改为 sortKey 排序 merge。
