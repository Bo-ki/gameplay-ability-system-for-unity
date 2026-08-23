# BAKE-01：Baker 只添加 ECS 组件；Authoring 读取必须走 Baker API

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities 1.4.6
**严重度**：P0
**Primary Owner**：Baking-BlobAsset
**来源**：`baking-baker-overview.md` > `Accessing other data sources in a baker`；`baking-phases.md` > `Baker phase`

## 规则声明

Baker 可以读取 Authoring 数据：

- 读取 `authoring` 自身字段会自动跟踪依赖。
- 读取其他 UnityEngine components/GameObjects 必须使用 Baker 的 `GetComponent<T>` 等 API，以记录增量依赖。
- 读取外部 asset 内容时按需调用 `DependsOn`。

Baker 不能读取或修改 entity 上已有的 ECS components，也不能修改其他 Baker 管理的 entity。它只能给自己的 primary entity 和自己创建的 additional entities 添加新 ECS components。

## 纠错

“Baker 内 `GetComponent<T>()` 不可用”是错误表述。不可用的是把它当成 ECS `GetComponent`；`Baker.GetComponent<TAuthoringComponent>` 是官方推荐的 Authoring 读取与依赖跟踪 API。

## 检查方法

区分调用目标：允许 `Baker.GetComponent<Transform/MonoBehaviour>`；禁止试图读取/`SetComponent` 已烘焙 ECS data。跨 GameObject/asset 读取必须能找到对应 Baker API 或 `DependsOn`。
