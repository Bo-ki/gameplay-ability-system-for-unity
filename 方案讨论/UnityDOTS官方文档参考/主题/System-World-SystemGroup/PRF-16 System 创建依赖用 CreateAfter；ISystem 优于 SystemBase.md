# PRF-16: System 创建依赖用 CreateAfter；ISystem 优于 SystemBase

**严重度**: P2
**Primary Owner**: System-World-SystemGroup
**来源**: `systems-update-order.html`（CreateAfter）、`systems-optimizing.html`（ISystem 开销更低）

## 规则声明
- 当 System A 的 `OnCreate` 依赖 System B 创建的 singleton 或资源时，必须加 `[CreateAfter(typeof(SystemB))]`
- 新 system 优先使用 `ISystem`（unmanaged struct）而非 `SystemBase`（managed class），特别是需要 Burst 编译时

## 为什么
没有 `CreateAfter` 的情况下，`OnCreate` 中访问其他系统的 singleton 可能因创建顺序不确定而访问空引用。`ISystem` 无托管对象、虚调用、GC 分配，Burst 开销更低。

## EX-GAS 诊断
Runtime Core 所有新 system 必须为 `ISystem`。当前 `SAbilityCommit`/`SAbilityStateCleanup` 等 system 已使用 `ISystem` 结构。

## 检查方法
- Code review 检查新 system 类型选择
- 搜索 `SystemBase` 在 Runtime Core 目录的出现（仅允许旧 system）
