# NAT-04: Persistent 容器必须有明确的 owner entity/system 和 teardown 规则

**严重度**: P0
**Primary Owner**: NativeContainer-Allocator
**来源**: `10-Collections-Allocator-NativeStream.md` — Allocator 三种生命周期

## 规则声明
使用 `Allocator.Persistent` 的 NativeContainer 必须关联到一个明确的 owner（具体 system 或 entity），并在 owner 销毁时自动或手动释放。不允许"全局无主"的 Persistent 分配。

## 为什么
Persistent 内存不会自动回收。无主分配导致内存泄漏，World dispose 时触发 leak 报警。明确的 owner 规则使得 teardown 路径可追踪。推荐利用 System-Associated Entity 机制管理 Persistent 生命周期。

## EX-GAS 诊断
Debugger 的 Persistent 分配应集中且数量恒定（如 Debugger buffer 的 Persistent 分配）。效果管线中 Persistent 分配应为 0，全部使用 TempJob 或 RewindableAllocator。

## 检查方法
审查所有 `Allocator.Persistent` 使用点，确认其 owner 和释放路径；Debugger 报告 PersistentAllocCount 每帧稳定。
