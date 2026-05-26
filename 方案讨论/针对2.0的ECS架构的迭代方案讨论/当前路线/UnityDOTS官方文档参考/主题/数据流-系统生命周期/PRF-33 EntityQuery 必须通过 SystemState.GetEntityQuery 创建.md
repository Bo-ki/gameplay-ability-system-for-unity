# PRF-33: EntityQuery 必须通过 SystemState.GetEntityQuery 创建

**严重度**: P1
**Primary Owner**: 数据流-系统生命周期
**来源**: `common-errors.md`

## 规则声明

所有 `EntityQuery` 实例必须通过 `SystemState.GetEntityQuery`（`state.GetEntityQuery` 或 `this.GetEntityQuery`）创建，禁止使用 `EntityManager.CreateEntityQuery`。

## 为什么

通过 `EntityManager.CreateEntityQuery` 创建的 query 不会被 ECS 安全系统正确注册——当 system 调度 job 时，依赖链无法正确追踪该 query 的读写访问。这可能导致：a) 不正确的 safety handle → 数据竞态；b) query 持有的 type handle 在结构变化时未正确刷新；c) type handle 更新遗漏。

## EX-GAS 诊断

所有 Runtime Core system 中，`EntityQuery` 必须在 system 字段中声明，通过 `state.GetEntityQuery` 在 `OnCreate` 中初始化，通过 `state.GetEntityQuery` 在 `OnUpdate` 中获取最新 version。

## 检查方法

Grep 搜索 `EntityManager.CreateEntityQuery` → 每个出现点都是违规，必须改为 `state.GetEntityQuery`。
