# SYS-02: SystemGroup 是 phase owner，禁止手写 Tick 顺序

**严重度**: P0
**Primary Owner**: System-World-SystemGroup
**来源**: `systems-update-order.html`

## 规则声明
每个 phase 边界必须通过显式 `SystemGroup` 定义。禁止通过手动调用其他 System 的 `Update()` 或依赖 `MonoBehaviour.Update()` 顺序控制 phase 执行时机。

## 为什么
手动 Update 调用会破坏 EntityQuery 的变更版本号，导致 `SetChangedVersionFilter` 失效。手写 Tick 顺序也无法被 ECS 安全系统跟踪，丢失自动依赖管理。

## EX-GAS 诊断
`GasStructuralPlaybackSystemGroup` 设计已存在但尚未完全落地。每个 phase 必须有对应的 `ComponentSystemGroup` 子类和明确的 `[UpdateInGroup]` 归属。

## 检查方法
- 审计所有 `ISystem.OnUpdate` 或 `SystemBase.OnUpdate` 中的 `.Update()` 调用
- 确认每个 phase 首尾有明确的 SystemGroup 边界
