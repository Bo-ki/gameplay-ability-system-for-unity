# PRF-20: IJobEntity 显式 EntityQuery 的校验边界

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `iterating-data-ijobentity.md`；安装包源码 `Unity.Entities/Iterators/EntityQuery.cs` 的 `HasComponentsRequiredForExecuteMethodToRun`

## 规则声明
不传显式 query 时，`IJobEntity` source generator 根据 `Execute` 参数和 job 属性生成 query。传入显式 `EntityQuery` 调度时，Entities 1.4.6 会在运行时校验该 query 是否包含 `Execute` 所需组件及兼容的读写模式；缺失时抛出可读异常。该检查不覆盖 `WithDisabled`、`WithPresent`、`IgnoreComponentEnabledState` 等全部语义组合，仍需人工交叉验证。

## 为什么
显式 query 可以比 job 自动生成的约束更宽松，这是官方保留的灵活性；但它至少必须提供 `Execute` 访问的组件和权限。运行时校验只覆盖可机械验证的最低条件，业务筛选语义仍可能因 query 与 job 属性不一致而改变。

## EX-GAS 诊断
仅对调用 `.Schedule(query, ...)` / `.ScheduleParallel(query, ...)` / `.Run(query)` 的显式 query 路径执行该审查；使用自动生成 query 的路径不需要重复声明 `Execute` 已表达的必需组件。

## 检查方法
- Code review 时交叉验证 query 属性（`[WithAll]`/`[WithNone]`）和 Execute 参数
- 检查显式 query 至少包含 `Execute` 所需组件和兼容访问模式
- 另外检查 enableable 状态、filter、WithAbsent/WithDisabled/WithPresent 等未被最低校验覆盖的语义
