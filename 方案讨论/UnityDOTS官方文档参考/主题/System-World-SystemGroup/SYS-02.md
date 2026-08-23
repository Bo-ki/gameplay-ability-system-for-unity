# SYS-02: SystemGroup 是 phase owner，禁止手写 Tick 顺序

**严重度**: P0
**Primary Owner**: System-World-SystemGroup
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3 / Entities 1.4.6
**官方来源**: `systems-update-order.md`

## 规则声明
EX-GAS 的物理执行边界由显式 `SystemGroup` 与同级 `UpdateBefore` / `UpdateAfter` 约束定义。禁止由业务 system 手动调用其他 system 的 `Update()`；若需与 MonoBehaviour PlayerLoop 建立边界，使用官方 PlayerLoop 注入机制并形成明确 contract。

## 为什么
SystemGroup 负责对子 system 排序并通过标准 system update 流程推进生命周期、版本与依赖。手动调用会绕开既定 group contract，其具体后果取决于被调用 API，不应缩写为“必然导致 change filter 失效”。

## EX-GAS 诊断
只为确有独立时序、更新率、同步/结构变化或边界投影语义的物理 phase 创建 `ComponentSystemGroup`。普通 GAS 语义步骤可留在同一 group 内，以 system 排序和依赖表达，避免一语义一步一 group。

## 检查方法
- 审计所有 `ISystem.OnUpdate` 或 `SystemBase.OnUpdate` 中的 `.Update()` 调用
- 确认每个 phase 首尾有明确的 SystemGroup 边界
