# PRF-16: System 创建依赖用 CreateAfter；ISystem 优于 SystemBase

**严重度**: P2
**Primary Owner**: System-World-SystemGroup
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3 / Entities 1.4.6
**官方来源**: `systems-update-order.md`、`systems-optimizing.md`、`systems-isystem.md`

## 规则声明
- 当 System A 的 `OnCreate` 必须读取 System B 在 `OnCreate` 创建的 system、singleton 或资源时，加 `[CreateAfter(typeof(SystemB))]`；若直接引用 B，再通过 `GetExistingSystem` 获取，避免在 A 内隐式创建 B
- 需要 Burst 编译且不依赖托管状态的新 Runtime Core system 优先使用 `ISystem`；确需托管字段或继承式 API 时使用 `SystemBase`

## 为什么
默认 system 创建顺序不保证满足 system group 的更新顺序，只保证显式 `CreateAfter` / `CreateBefore` 约束。官方说明在使用 Burst 时 `ISystem` 的 system overhead 低于 `SystemBase`；这不表示每个 `SystemBase` 每帧都会产生 GC，也不表示 `ISystem` 对所有场景都更快。

## EX-GAS 诊断
EX-GAS Runtime Core 新 system 默认选 `ISystem`；若必须使用托管对象或 `SystemBase` 专属能力，在设计说明中记录理由，并在 hot path 用 Profiler 验证。当前 `SAbilityCommit` / `SAbilityStateCleanup` 等已使用 `ISystem`。

## 检查方法
- Code review 检查新 system 类型选择
- 搜索 `SystemBase` 在 Runtime Core 的使用，确认每处都有托管/API 需求，而不是按类型一律判错
