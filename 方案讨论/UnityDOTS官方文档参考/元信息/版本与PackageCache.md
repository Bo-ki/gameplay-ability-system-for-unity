# 版本与 PackageCache 证据

## 职责

本文件维护 Unity DOTS 官方依据的版本口径、本地证据路径和查找方法。回答"当前项目实际使用哪一版"以及"怎么在 PackageCache 中找到对应的官方文档"。

## 当前版本表

| 包 | 实际版本 | PackageCache hash | 本地文档路径 | 在线入口 |
|---|---:|---|---|---|
| Entities | `1.4.6` | `com.unity.entities@e90944159b94` | `Library/PackageCache/com.unity.entities@e90944159b94/Documentation~` | https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/ |
| Unity Physics | `1.4.6` | `com.unity.physics@22d10f355559` | `Library/PackageCache/com.unity.physics@22d10f355559/Documentation~` | https://docs.unity3d.com/Packages/com.unity.physics@1.4/manual/ |
| Entities Graphics | `1.4.19` | `com.unity.entities.graphics@1e91bb5cdef3` | `Library/PackageCache/com.unity.entities.graphics@1e91bb5cdef3/Documentation~` | https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.4/manual/ |
| Burst | `1.8.29` | `com.unity.burst@6bb9aca3ef38` | `Library/PackageCache/com.unity.burst@6bb9aca3ef38/Documentation~` | https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/ |
| Collections | `2.6.6` | `com.unity.collections@9796e5ee0d9e` | `Library/PackageCache/com.unity.collections@9796e5ee0d9e/Documentation~` | https://docs.unity3d.com/Packages/com.unity.collections@2.6/manual/ |
| Mathematics | `1.3.3` | `com.unity.mathematics@19a9377c4ffa` | `Library/PackageCache/com.unity.mathematics@19a9377c4ffa/Documentation~` | https://docs.unity3d.com/Packages/com.unity.mathematics@1.3/manual/ |

**禁止修改 `Library/PackageCache` 中的任何文件。** 该目录变更会被 Unity 自动还原。

## 如何查找官方文档

```
方法1: 直接浏览
  Library/PackageCache/<package-name>@<hash>/Documentation~/

方法2: 搜索特定主题
  rg "sync point" Library/PackageCache/com.unity.entities@e90944159b94/Documentation~/

方法3: 查看代码示例
  Library/PackageCache/<package-name>@<hash>/DocCodeSamples.Tests/*.cs
```

## 常用文档页面与本地路径对照

| 主题 | 本地路径 | 在线 URL |
|---|---|---|
| World 概念 | `concepts-worlds.md` | `manual/concepts-worlds.html` |
| System 介绍 | `systems-intro.md` | `manual/systems-intro.html` |
| ISystem | `systems-isystem.md` | `manual/systems-isystem.html` |
| SystemGroup/UpdateOrder | `systems-update-order.html` | `manual/systems-update-order.html` |
| System 优化 | `systems-optimizing.html` | `manual/systems-optimizing.html` |
| IJobEntity | `iterating-data-ijobentity.html` | `manual/iterating-data-ijobentity.html` |
| IJobChunk | `iterating-data-ijobchunk.html` | `manual/iterating-data-ijobchunk.html` |
| SystemAPI.Query | `systems-systemapi-query.md` | `manual/systems-systemapi-query.html` |
| Archetype 概念 | `concepts-archetypes.html` | `manual/concepts-archetypes.html` |
| Chunk 分配 | `performance-chunk-allocations.html` | `manual/performance-chunk-allocations.html` |
| Sync Points | `performance-sync-points.md` | `manual/performance-sync-points.html` |
| ECB | `systems-entity-command-buffers.md` | `manual/systems-entity-command-buffers.html` |
| Enableable | `components-enableable-use.html` | `manual/components-enableable-use.html` |
| DynamicBuffer | `components-buffer-introducing.html` | `manual/components-buffer-introducing.html` |
| Baking | `baking-overview.html` | `manual/baking-overview.html` |
| BlobAsset | `components-blobasset.html` | `manual/components-blobasset.html` |
| Transform | `transforms-concepts.md` | `manual/transforms-concepts.html` |
| Physics | `physics-concepts.html` | `manual/physics-concepts.html` |
| Entities Graphics | `entities-graphics.html` | `manual/entities-graphics.html` |
| Burst | `burst-overview.html` | `manual/burst-overview.html` |
| Collections | `collections-overview.html` | `manual/collections-overview.html` |
| Mathematics | `mathematics-overview.html` | `manual/mathematics-overview.html` |
| Safety | `concepts-safety.md` | `manual/concepts-safety.html` |
| Common Errors | `common-errors.md` | `manual/common-errors.html` |
| 结构变化优化 | `optimize-structural-changes.md` | `manual/optimize-structural-changes.html` |
| 状态机 | `state-machine.md` | `manual/state-machine.html` |

## 维护规则

1. PackageCache hash 变化时立即更新版本表
2. 版本变更后，记录受影响的领域文档列表
3. 不修改 `Library/PackageCache` 中的任何文件
