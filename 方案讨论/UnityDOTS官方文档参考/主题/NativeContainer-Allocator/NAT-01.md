# NAT-01：每个 NativeContainer 必须说明 allocator、owner 和释放依赖

**严重度**：P0
**Primary Owner**：NativeContainer-Allocator
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Collections `2.6.6`
**官方来源**：Collections `allocator-overview.md`、`allocator-rewindable.md`

## 规则声明
Runtime Core 的 NativeContainer 分配点必须能追踪 allocator、唯一 owner、最后使用它的 JobHandle，以及 Dispose/Rewind 的位置。若生命周期由 World/System Group allocator 管理，应注明官方失效窗口，不能自行延长。

## 为什么
TempJob 必须在 4 帧内释放，Persistent 必须显式释放，Rewind 会一次失效所有 child allocation。容器为 struct，副本的 `IsCreated` 不能证明底层内存仍有效。

## EX-GAS 诊断
任务交还列出新增长期 container 和 teardown；短期 container 优先用 `Dispose(lastUseHandle)` 串联，不以主线程 `Complete` 代替所有权设计。

## 检查方法
从每个分配点追踪所有 job 使用者到最终 Dispose/Rewind；验证没有 use-after-free、双重 owner 或 TempJob 超过 4 帧。
