# BAKE-03: Baking System 必须手动追踪依赖和增量还原

**严重度**: P0
**Primary Owner**: Baking-BlobAsset
**来源**: `baking-baking-systems-overview.md`

## 规则声明
Baking System 不自动追踪依赖和结构变化；需显式 `DependsOn()` 并在添加组件时手动追踪／撤销变更。Baking System 中创建的 entity **不会**出现在 baked entity scene 中（仅用于系统间数据传递）。

## 为什么
Baking System 与 Baker 不同，不会自动记录依赖或产出。必须：1) 对所有外部数据引用显式调用 `DependsOn()`；2) 当向 entity 添加 component 时手动追踪并实现撤销逻辑以支持增量烘焙。要输出到 scene 的 entity 必须在 Baker 中通过 `CreateAdditionalEntity` 创建。

## EX-GAS 诊断
批量 GE 属性计算、Definition 数据关联与过滤等场景必须通过 Baking System 实现，并手动管理增量还原。

## 检查方法
审查所有 `[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]` 标记的 ISystem，确认 `DependsOn()` 调用覆盖所有外部依赖，且添加 component 时有匹配的撤销逻辑。
