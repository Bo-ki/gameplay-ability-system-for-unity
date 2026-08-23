# PRF-33: 调度 Job 的 System 必须持有自身注册的 EntityQuery

**严重度**: P1
**Primary Owner**: 数据流-系统生命周期
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3 / Entities 1.4.6
**官方来源**: `common-errors.md`、`systems-entityquery-create.md`、`systems-systemapi-query.md`

## 规则声明

会使用 query 调度 job 的 system，必须使用该 system 自己注册的 query：`state.GetEntityQuery(...)`、`EntityQueryBuilder.Build(ref state)`，或由 `SystemAPI.Query` / `SystemAPI.QueryBuilder().Build()` 的 source generator 创建。不要在此类 system 中使用 `EntityManager.CreateEntityQuery`，也不要借用其他 system 创建的 query。

## 为什么

Entities 根据 system 自己创建的 query 推导该 system 读写的 component 类型并维护依赖。官方 `common-errors.md` 明确指出：使用未通过 system 的 `GetEntityQuery` 创建的 query，可能使 safety system 无法确定 system 使用了哪些类型，并报出 job 未写回 `Dependency` 的错误。

这条约束针对 system 的依赖注册，不表示 query 会因结构变化而失效。`EntityQuery` 可以在 `OnCreate` 创建并缓存；后续新建的匹配 archetype 会自动纳入同一个 query。需要每次更新的是显式缓存的 `ComponentTypeHandle` / `ComponentLookup` 等访问句柄，而不是重新创建 query。

## EX-GAS 诊断

Runtime Core system 中，长期使用的 `EntityQuery` 在 `OnCreate` 通过 system-owned API 初始化并缓存即可。`SystemAPI.Query` 与 `SystemAPI.QueryBuilder()` 生成的 query/handle 缓存由 source generator 维护。

## 检查方法

搜索 Runtime system 内的 `EntityManager.CreateEntityQuery`，逐处确认是否会用于该 system 的 job 调度。Editor 工具、测试或明确独立拥有 query 的非 system 代码不应被此规则机械判错。
