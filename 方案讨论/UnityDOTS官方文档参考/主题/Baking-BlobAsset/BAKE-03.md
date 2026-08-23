# BAKE-03：Baking System 必须显式保证输入链与结构变化可还原

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities 1.4.6
**严重度**：P0
**Primary Owner**：Baking-BlobAsset
**来源**：`baking-baking-systems-overview.md`；官方 `BakingExamples.cs`

## 规则声明

Baking System 不自动跟踪 authoring dependencies，也不自动撤销它造成的结构变化：

1. Authoring/asset 依赖由 Baker 的访问 API/`DependsOn` 跟踪，Baker 再用 BakingType/TemporaryBakingType component 把输入交给 system。
2. Baking System 没有 Baker 的 `DependsOn` API；普通 job dependencies 通过 `state.Dependency` 管理。
3. 推荐由 Baker 添加最终 output component，Baking System 只写值。
4. 若 system 自行添加 component，必须提供输入消失时移除旧 component 的逆向 query。
5. Baking System 创建的新 entity 不会被序列化到 baked entity scene；持久输出 entity 用 Baker `CreateAdditionalEntity` 创建。

## 检查方法

搜索 Baking System 中虚构/误用的 `DependsOn`、单向 `AddComponent` 和 `CreateEntity`。每个结构输出都必须能说明 full/live baking 下的还原路径。
