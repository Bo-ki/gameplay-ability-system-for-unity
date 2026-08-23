# CASE-39：Baker 无 ECS 输出依赖，但可读取 Authoring 数据

**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities 1.4.6
**Primary Owner**：Baking-BlobAsset
**来源**：`baking-phases.md`、`baking-baker-overview.md`
**关联规则**：BAKE-01

Baker 运行顺序不保证，因此不能读取或修改其他 Baker 已添加的 ECS components。Baker 可以并且应该通过 `Baker.GetComponent<T>`、`GetComponents`、`DependsOn` 等 API 读取 Authoring 数据并记录依赖。

需要消费多个 Baker 的 ECS 输出时，在所有 Bakers 之后运行的 Baking System 中查询；输出 component 最好由拥有它的 Baker 预先添加，system 只写值。
