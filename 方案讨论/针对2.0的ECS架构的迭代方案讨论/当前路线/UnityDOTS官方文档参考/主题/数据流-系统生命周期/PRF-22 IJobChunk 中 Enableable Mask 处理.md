# PRF-22: IJobChunk 中 Enableable Mask 处理

**严重度**: P1
**Primary Owner**: 数据流-系统生命周期
**来源**: `ChunkIterationJob.cs` / `JobChunkExamples.cs`

## 规则声明

IJobChunk.Execute 的 `useEnabledMask` 和 `chunkEnabledMask` 参数指示当前 chunk 是否包含 enableable component。若 query 包含 enableable 但 job 使用简单 `for (int i = 0; i < chunk.Count; i++)` 循环，会处理已禁用的 entity（静默逻辑错误）。必须二选一：1) 有 enableable → 使用 `ChunkEntityEnumerator`；2) 确认无 enableable → 使用 `Assert.IsFalse(useEnabledMask)`。

## 为什么

引入 enableable component 后，未使用 `ChunkEntityEnumerator` 的 IJobChunk 会静默处理已禁用的 entity——没有编译错误或运行时异常。在 GAS 中表现为：停用的 effect 仍在计算、已死的 unit 仍在移动。

## EX-GAS 诊断

所有 IJobChunk 实现必须审计。例如：`SEffectApply` 若使用 IJobChunk 且 future 可能引入 enableable → 必须先用 `ChunkEntityEnumerator` 保护。

## 检查方法

Grep IJobChunk.Execute 实现：无 `ChunkEntityEnumerator` 且无 `Assert.IsFalse(useEnabledMask)` → 标记为潜在 bug。
