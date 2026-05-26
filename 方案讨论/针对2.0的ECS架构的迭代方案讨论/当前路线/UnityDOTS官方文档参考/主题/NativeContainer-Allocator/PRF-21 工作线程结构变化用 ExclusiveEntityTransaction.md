# PRF-21: 工作线程结构变化用 ExclusiveEntityTransaction

**严重度**: P1
**Primary Owner**: NativeContainer-Allocator
**来源**: `90-规则编号索引.md` — PRF-21

## 规则声明
需要在工作线程执行结构变化时（而非 job 中录制 ECB），使用 `ExclusiveEntityTransaction` 而不是直接 `EntityManager` 调用。

## 为什么
`ExclusiveEntityTransaction` 允许在主线程外的单独线程进行结构变化操作，且不会触发 sync point。适用于 World 初始化、battle 加载等批量结构变化场景。

## EX-GAS 诊断
AutoChess battle init、Scene loading 中的批量 entity 创建应考虑 `ExclusiveEntityTransaction`。这些场景不属于 hot path 但需要快速完成大量结构变化。

## 检查方法
搜索批量 entity 创建场景，确认使用 `ExclusiveEntityTransaction` 而非逐 entity 的 `EntityManager` 调用。
