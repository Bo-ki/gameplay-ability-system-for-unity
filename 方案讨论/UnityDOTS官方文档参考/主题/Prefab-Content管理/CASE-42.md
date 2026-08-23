# CASE-42：Scene Section 引用只能指向同 Section 或 Section 0

**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities 1.4.6
**Primary Owner**：Prefab-Content管理
**来源**：`streaming-scene-sections.md`
**关联规则**：CONTENT-01

## 官方约束

SubScene 中 ECS component 的 `Entity` 引用只能指向同一 section 或 section 0。指向其他非零 section 的引用加载时变为 `Entity.Null`。

打开的 SubScene 在 Editor 中会把所有 entity 视为 section 0；真实 section 分配只在关闭 SubScene 后应用，因此必须在 closed-SubScene/Player 路径验证。

Entity prefab 实例携带 `SceneSection` 时，卸载对应 section 会销毁实例。若要让实例脱离该生命周期，应显式移除 `SceneSection`。

## EX-GAS 策略

把跨区域共享且需要常驻的 entity 放入 section 0 是项目方案，不是 Unity 要求所有 ASC/Ability 都放入 section 0。需结合常驻内存预算、引用方向和卸载生命周期决定。
