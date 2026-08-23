# QRY-02: Query contract 写清 All/Any/None/Disabled/ChangeFilter

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `systems-entityquery-create.md`、`iterating-data-ijobentity.md`、`components-enableable-use.md`

## 规则声明
显式构造 `EntityQuery` 或为 `IJobEntity` 添加 query 属性时，必须让代码清楚表达实际使用的 All/Any/None/Absent/Disabled/Present、Options、shared/change filter 等语义。只声明业务确实需要的类别；`IJobEntity.Execute` 参数自动形成的必需组件是正式 query contract，不属于应被重复声明的“隐式条件”。

## 为什么
遗漏业务过滤条件会改变匹配集合；重复或矛盾声明则增加维护风险。`IgnoreComponentEnabledState` 决定是否考虑 enabled 状态，但“未设置该选项”本身不等于必然同步；只有执行遵守过滤的同步操作且相关 enableable 写 job 未完成时才会等待。

## EX-GAS 诊断
每个 `IJobEntity`/`IJobChunk` 旁注释列出对应的 EntityQuery 条件。Code review 时交叉验证。当前各 IJobEntity 缺少 EntityQuery 注释 → 增加 PRF-20 风险。

## 检查方法
- Grep 搜索 `IJobEntity` 和 `IJobChunk` 在 Runtime Core 目录中的使用
- 检查 query 属性（`[WithAll]`/`[WithNone]`/`[WithAny]`/`[WithOptions]`）是否完整
- Code review 交叉验证 query 属性和 Execute 参数是否一一对应
