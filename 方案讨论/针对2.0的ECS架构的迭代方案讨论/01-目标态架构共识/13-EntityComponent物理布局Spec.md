# Entity/Component 物理布局 Spec

> Owner：`01-目标态架构共识` | 状态：目标态 Spec 索引 | 最近拆分：2026-06-08

本文件只保留目标态 Entity / Component 物理布局的总览、阅读路径和 owner 边界。拆分前全文已归档到 [`_归档/2026-06-08-13-EntityComponent物理布局Spec拆分前.md`](_归档/2026-06-08-13-EntityComponent物理布局Spec拆分前.md)。

## 目的

定义 Runtime Core 目标态的 Entity 和 Component 物理布局，确保 Archetype 数量稳定、Component 选型正确、Buffer 容量策略可审计，并使物理布局能服务纯 ECS Runtime Core 的 SystemGroup / Job / Debugger evidence。

## 阅读路径

1. 先读 [13-01 Entity 清单与运行时布局](13-EntityComponent物理布局/13-01-Entity清单与运行时布局Spec.md)，确认 FrameArena、DefinitionCatalog、ASC、Ability、Request 和 Active Effect Query 的实体布局。
2. 再读 [13-02 Archetype 与 Component 分类](13-EntityComponent物理布局/13-02-Archetype与Component分类Spec.md)，确认批量创建、Archetype 审计和 Component 类型分类。
3. 最后读 [13-03 Buffer 容量与 Phase 映射](13-EntityComponent物理布局/13-03-Buffer容量与Phase映射Spec.md)，确认 InternalBufferCapacity、spill 监控、phase 对应、不变量和验收。

## Owner 边界

| 相邻 Spec | 职责分工 |
|---|---|
| [03G Component矩阵 / FrameArena / Job 拓扑](03-RuntimeCore管线/03G-Component矩阵-FrameArena-Job拓扑Spec.md) | 维护 Runtime Core component matrix、job topology 和查询拓扑；引用 `13` 的物理布局，不重复维护容量正文。 |
| [05 ActiveEffectStore](05-ActiveEffectStoreSpec.md) | 维护 ActiveEffectStore 语义、Duration / Stack / Period / Granted state；引用 `13` 的 buffer capacity 约束，不重复维护布局表。 |
| [16 纯 ECS 内核与边界重划分](16-纯ECS内核与边界重划分Spec.md) | 维护 Shell / Boundary / Core / Debugger / SourceGenerator 的目标态分层和端到端代码骨架；引用 `13` 的实体物理布局。 |

## 禁止写入

1. 不记录当前代码事实、当前文件行号、generated report 数字、验证日志或任务计划。
2. 不把 current proof-only buffer / singleton carrier 写成目标态完成。
3. 不在本文件恢复长正文；新增正文进入子目录对应 owner。

## 验收

1. 子页能分别回答实体布局、Component 分类、容量 / phase 映射三个问题。
2. `03G`、`05`、`16` 只引用本 Spec，不维护第二份物理布局正文。
3. 目标态实现任务能从本 Spec 找到 Archetype、Component type、capacity 和 phase mapping 的验收入口。
