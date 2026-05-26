# PRF-14: NativeContainer 必须明确 Allocator 归属和生命周期

**严重度**: P1
**Primary Owner**: NativeContainer-Allocator
**来源**: `13-DOTS编写规范与性能陷阱.md` — 官方证据索引

## 规则声明
所有 NativeContainer 的 allocator 选择和生命周期必须在任务交还时说明，接受 code review 检查。

## 为什么
这是编码规范级别的要求，与 NAT-01 互为补充。NAT-01 要求代码注释标注，PRF-14 要求任务交还时审查。

## EX-GAS 诊断
每条带有 NativeContainer 分配的 PR/任务必须审查 allocator 选择是否合适。

## 检查方法
Code review checklist 包含 NativeContainer 归属确认项。
