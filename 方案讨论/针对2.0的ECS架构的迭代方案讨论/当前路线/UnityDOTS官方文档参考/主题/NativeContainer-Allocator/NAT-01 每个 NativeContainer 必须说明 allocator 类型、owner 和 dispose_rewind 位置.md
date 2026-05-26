# NAT-01: 每个 NativeContainer 必须说明 allocator 类型、owner 和 dispose/rewind 位置

**严重度**: P0
**Primary Owner**: NativeContainer-Allocator
**来源**: `10-Collections-Allocator-NativeStream.md` — Allocator 三种生命周期

## 规则声明
所有在 Runtime Core 中分配的 NativeContainer（NativeList、NativeArray、NativeStream、NativeHashMap 等）必须在代码注释或文档中明确标注：allocator 类型（Temp/TempJob/Persistent）、owner（哪个 system 或 entity 负责释放）、dispose 或 rewind 的发生位置和时机。

## 为什么
缺少归属声明的 Persistent 分配导致内存泄漏。TempJob 忘记 dispose 在 4 帧后触发 leak detection。明确的 owner 规则是防止内存泄漏的第一道防线。

## EX-GAS 诊断
Debugger 报告中包含 `TempJobAllocCount`、`PersistentAllocCount`（应为 0 或恒定）、`RewindableCapacity`。所有 NativeContainer 声明遵循 `new NativeList<T>(Allocator.TempJob)` 并在同一 system 的帧末 `Dispose`。

## 检查方法
代码审查每个 NativeContainer 分配点，确认 allocator 选择和 dispose/rewind 位置；Debugger 报告 Persistent alloc count 是否超出预期。
