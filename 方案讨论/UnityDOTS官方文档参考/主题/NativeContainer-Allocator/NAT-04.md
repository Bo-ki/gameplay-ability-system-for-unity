# NAT-04：Persistent 容器必须有唯一 owner 和 teardown

**严重度**：P0
**Primary Owner**：NativeContainer-Allocator
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Collections `2.6.6`；Entities `1.4.6`
**官方来源**：Collections `allocator-overview.md`；Entities `systems-data.md`

## 规则声明
`Allocator.Persistent` 分配必须绑定唯一 owner，并在 owner 的 `OnDestroy`、Dispose 或等价 teardown 中释放。禁止全局无主分配和多个 owner 都可能 Dispose 的设计。

## 为什么
Persistent 可无限期存在，安全检查无法判断它是否超过业务生命周期。是否使用 system-associated entity 由访问方式决定；它是可选承载手段，不替代 NativeContainer 的显式 Dispose。

## EX-GAS 诊断
长期缓存、Debugger ring buffer 和 singleton container 分别列出 owner、读写系统、最后 JobHandle 与 teardown。效果热管线“Persistent 应为 0”只能作为当前架构目标，不能冒充官方规则。

## 检查方法
审计全部 `Allocator.Persistent`；模拟 World/system 销毁并检查 leak、未完成 job 和重复释放。
