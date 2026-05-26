# 00 版本与 PackageCache 证据

## 职责

本主题维护 Unity DOTS 官方依据的版本口径、本地证据路径和**如何查找官方文档**。它回答"当前项目实际使用哪一版"以及"怎么在 PackageCache 中找到对应的官方文档"。

## 当前版本表

| 包 | 实际版本 | PackageCache hash | 本地文档路径 | 在线入口 |
|---|---:|---|---|---|
| Entities | `1.4.6` | `com.unity.entities@e90944159b94` | `Library/PackageCache/com.unity.entities@e90944159b94/Documentation~` | https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/ |
| Unity Physics | `1.4.6` | `com.unity.physics@22d10f355559` | `Library/PackageCache/com.unity.physics@22d10f355559/Documentation~` | https://docs.unity3d.com/Packages/com.unity.physics@1.4/manual/ |
| Entities Graphics | `1.4.19` | `com.unity.entities.graphics@1e91bb5cdef3` | `Library/PackageCache/com.unity.entities.graphics@1e91bb5cdef3/Documentation~` | https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.4/manual/ |
| Burst | `1.8.29` | `com.unity.burst@6bb9aca3ef38` | `Library/PackageCache/com.unity.burst@6bb9aca3ef38/Documentation~` | https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/ |
| Collections | `2.6.6` | `com.unity.collections@9796e5ee0d9e` | `Library/PackageCache/com.unity.collections@9796e5ee0d9e/Documentation~` | https://docs.unity3d.com/Packages/com.unity.collections@2.6/manual/ |
| Mathematics | `1.3.3` | `com.unity.mathematics@19a9377c4ffa` | `Library/PackageCache/com.unity.mathematics@19a9377c4ffa/Documentation~` | https://docs.unity3d.com/Packages/com.unity.mathematics@1.3/manual/ |

## 如何在 PackageCache 中查找官方文档

PackageCache 中的 `Documentation~` 目录包含 markdown 格式的官方文档。查找路径：

```
方法1: 直接浏览文件
  Library/PackageCache/<package-name>@<hash>/Documentation~/
  例如: Library/PackageCache/com.unity.entities@e90944159b94/Documentation~/

方法2: 搜索特定主题
  rg "sync point" Library/PackageCache/com.unity.entities@e90944159b94/Documentation~/
  
方法3: 查看 API 文档
  Library/PackageCache/<package-name>@<hash>/Documentation~/api_*

方法4: 查看代码示例（DocCodeSamples）
  Library/PackageCache/com.unity.entities@e90944159b94/DocCodeSamples.Tests/*.cs
  
方法5: 查看性能测试（PerformanceTests）
  Library/PackageCache/com.unity.entities@e90944159b94/Unity.Entities.PerformanceTests/
```

### 常用文档页面与本地路径对照

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
| Sync Point | `performance-sync-points.md` | `manual/performance-sync-points.html` |
| Enableable | `components-enableable-use.html` | `manual/components-enableable-use.html` |
| DynamicBuffer | `components-buffer-introducing.html` | `manual/components-buffer-introducing.html` |
| Baking 概述 | `baking-overview.html` | `manual/baking-overview.html` |
| Baker | `baking-baker-overview.md` | `manual/baking-baker-overview.html` |
| Journaling | `entities-journaling.md` | `manual/entities-journaling.html` |

**注意：** `.md` vs `.html` 后缀取决于包的打包方式，实际文件以后缀为准。

## 版本解析规则

1. **PackageCache 实际版本优先于在线 latest 文档**。在线文档可能比本地新，API 可能不同。
2. `Packages/manifest.json` 表示直接依赖意图；`Packages/packages-lock.json` 表示解析结果。与 PackageCache 不一致时记录差异。
3. 文档引用必须写真实 hash 路径，如 `com.unity.entities@e90944159b94`。
4. Unity Editor 升级不等于 DOTS package 文档升级，必须逐包确认。
5. PackageCache 是只读证据源，不能改包目录。

## EX-GAS 项目解读

### 为什么需要版本证据

EX-GAS 目标态不能只说"遵守 Unity ECS"。每条 DOTS 约束必须能追溯到**当前 PackageCache 的官方文档**。同一个设计在不同 Entities/Physics/Graphics/Burst 版本下可能语义不同。

### 版本变更的触发条件

以下情况需要检查版本是否变更：
- Unity Editor 版本升级
- `Packages/manifest.json` 修改
- 从 Package Manager 更新包
- 新建/切换分支（可能使用不同的 manifest）

## 验收指标

1. DOTS 相关行动报告列出具体 PackageCache hash 版本
2. 性能或架构结论不引用 `latest` 作为唯一依据
3. 发现 hash 变化 → 先更新本文件 → 检查所有主题文档中的路径
4. 任务执行者能在 PackageCache 中定位到相关官方文档原文
