# PRF-12: 审核 SharedComponent 使用

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `components-shared-introducing.md`、`components-shared-optimize.md`、`iterating-data-ijobentity.md`

## 规则声明
Shared component 用于把相同值的实体聚集到同一批 chunk，并支持按 shared value 过滤。`IJobEntity.Execute` 中以 `in` 只读访问；unmanaged shared component 可由 Burst/unmanaged API 使用，managed shared component 不能 Burst 编译或 schedule，相关 `IJobEntity` 必须 `.Run()`。不要把它当作高频变化的逐实体状态。

## 为什么
修改 shared component 值是结构变化，会把实体移动到具有目标 shared value 的 chunk；大量唯一值会产生许多低占用 chunk。managed 与 unmanaged shared component 的执行能力不同，不能用统一“托管回退倍率”描述。对于稳定分类且同值实体很多的场景，shared component 正是官方设计用途。

## EX-GAS 诊断
审查值的变化频率、唯一值基数、chunk 占用率，以及是否确实需要按值过滤。稳定分类不应仅因位于 hot path 就替换为 enableable/tag。

## 检查方法
- 搜索 `ISharedComponent` 实现和 `GetSharedComponentManaged` 调用
- 评估 SharedComponent 在 hot path 中使用的影响
