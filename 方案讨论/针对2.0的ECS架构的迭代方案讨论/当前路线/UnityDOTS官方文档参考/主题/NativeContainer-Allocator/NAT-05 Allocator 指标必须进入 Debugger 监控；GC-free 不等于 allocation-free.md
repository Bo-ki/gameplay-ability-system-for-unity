# NAT-05: Allocator 指标必须进入 Debugger 监控；GC-free 不等于 allocation-free

**严重度**: P1
**Primary Owner**: NativeContainer-Allocator
**来源**: `10-Collections-Allocator-NativeStream.md` — EX-GAS 项目解读

## 规则声明
Debugger 必须报告 TempJob、Persistent 的分配计数和 RewindableAllocator capacity 峰值。团队必须理解"GC-free"只意味着无托管内存分配（无 GC 暂停），NativeContainer 的 Native 内存分配仍然存在且有成本。

## 为什么
NativeContainer 分配虽然在非托管堆上（无 GC 暂停），但每次分配仍然有 Native 内存分配器调用、边界检查、类型初始化等开销。大规模频繁的 NativeContainer 分配/释放会影响帧率。

## EX-GAS 诊断
RuntimeDiagnostics 包含：`TempJobAllocCount`（不应线性增长）、`PersistentAllocCount`（应为 0 或恒定）、`RewindableCapacity`（rewind 前峰值）、`NativeStreamSegmentCount`（/帧）。

## 检查方法
Debugger validation summary 包含 allocator 指标表格；性能任务交还时确认 NativeContainer 分配次数与 entity 数的关系。
