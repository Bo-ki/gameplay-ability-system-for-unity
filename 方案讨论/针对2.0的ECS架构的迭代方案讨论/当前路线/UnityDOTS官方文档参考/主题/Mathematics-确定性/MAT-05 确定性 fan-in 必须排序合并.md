# MAT-05: 确定性 fan-in 必须排序合并

**严重度**: P1
**Primary Owner**: Mathematics-确定性
**来源**: `15-数据流-系统生命周期规范.md` P2-01

## 规则声明
影响 battle hash / replay 的结果（EffectCommand fan-in、TypedFact projection、AttributeDelta reduce）不得使用无序 `ParallelWriter` 或依赖未排序的 foreach chunk 顺序。并行 fan-in 后必须按确定键（如 `[ChunkIndexInQuery]` int sortKey）排序 merge。

## 为什么
并行 job 的执行顺序在不同帧和不同 CPU 架构上不可预测。依赖顺序的 fan-in 导致 battle hash 不稳定。

## EX-GAS 诊断
Debugger 应报告每个 fan-in 点的排序策略；无序 fan-in 输出告警。

## 检查方法
审计所有 `ParallelWriter` / `NativeStream` 使用，确认消费端在 merge 时有确定性排序；搜索 `AppendToBuffer` + `sortKey` 模式。
