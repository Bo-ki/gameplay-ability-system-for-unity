# SYS-05: World 边界：Debugger/Demo/Presentation 只能通过 Boundary 观察 Core

**严重度**: P1
**Primary Owner**: System-World-SystemGroup
**来源**: `concepts-worlds.md`

## 规则声明
Debugger、AutoChess Demo Runner、Presentation 层不允许直接修改 Core World 的 ECS 数据。它们必须通过只读观察通道（如 `ComponentLookup`、`SystemAPI.Query` 只读、Shared Read Model）或预定义的 Boundary API 获取 Core 状态。

## 为什么
允许 Debugger 直接写入 Core 数据会引入非确定性行为——Debugger 的一次写入可能在 release build 中不存在，导致 Core 行为在 debug/release 间不一致。Demo Runner 的写入可能绕过 Core 的业务规则。

## EX-GAS 诊断
当前 `GasRuntimeDebugger.cs` 持有部分观察者职责。需要明确区分 read-only 诊断和 read-write 调试命令。

## 检查方法
- 搜索 Debugger/Presentation/Demo 目录中对 Core component 的写入操作
- 确认跨 World 访问是否通过只读通道
