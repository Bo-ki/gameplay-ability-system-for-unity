# PRF-14：任务交还必须说明 NativeContainer 所有权

**严重度**：P1
**Primary Owner**：NativeContainer-Allocator
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Collections `2.6.6`
**官方来源**：Collections `allocator-overview.md`、`allocator-rewindable.md`
**权威细则**：NAT-01、NAT-04

## 规则声明
新增或改变 NativeContainer 的任务交还必须列出 allocator、owner、最后使用的依赖与 Dispose/Rewind 边界；评审按 NAT-01/NAT-04 核验。

## 为什么
这是交付层检查，不重复定义 allocator API。缺少依赖链的“帧末 Dispose”可能发生在 job 仍使用容器时。

## EX-GAS 诊断
PR/任务说明包含所有权表；没有新增 allocation 时明确写“无 NativeContainer 生命周期变化”。

## 检查方法
从代码中的分配点抽查到 teardown，并与交付说明一致。
