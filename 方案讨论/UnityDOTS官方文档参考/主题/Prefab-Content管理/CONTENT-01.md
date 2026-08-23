# CONTENT-01：Prefab 用于实体原型；静态定义优先共享不可变数据

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities 1.4.6；EX-GAS 当前 Definition 架构
**严重度**：P1
**Primary Owner**：Prefab-Content管理
**来源**：`performance-chunk-allocations.md` > `Prefabs and chunk fragmentation`；`blob-assets-intro.md`

## 规则声明

EX-GAS 的 Ability、GameplayEffect、Tag 等纯静态定义优先使用 BlobAsset 或 generated table；只有需要实体身份、完整组件集合、实例化和生命周期语义的数据才使用 Entity Prefab。

Unity 官方没有规定“每个 prefab 必然拥有唯一 archetype/独占 16 KiB chunk”。只有当 prefab 落入不同 archetype 或 shared-component 分区，并导致低占用 chunk 时，才形成相应碎片。

## 为什么

ECS chunk 固定为 16 KiB。大量不同 archetype、分区或低占用 prefab chunk 会浪费空间；BlobAsset 能让多个 entity 共享不可变数据。间接访问是否值得仍应以 Profiler 和 Archetypes window 证据裁决。

## 检查方法

在 Archetypes window 记录 prefab 相关 archetype、chunk count、allocated/unused bytes 和 capacity。审查 `EntityPrefabReference`：若只承载不可变定义而不需要实例语义，改用共享数据方案。
