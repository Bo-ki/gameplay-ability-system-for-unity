# 13 Entity / Component 物理布局 Spec 索引

> Owner：`01-目标态架构共识/13-EntityComponent物理布局` | 状态：目标态 Spec 子目录 | 最近拆分：2026-06-08

本目录从 `../13-EntityComponent物理布局Spec.md` 拆出目标态正文。根文件只保留总览、阅读路径和 owner 边界；本目录内文件才是具体正文 owner。

## 边界

1. 本目录只回答目标态 Entity / Component 物理布局应该如何设计、为什么这样设计、如何验收。
2. 不记录当前代码事实、generated report 数字、执行流水、迁移进度或下一步任务。
3. 当前事实回到 `../../00-当前架构事实/`；任务消费回到 `../../02-主线任务树/`；短期接力回到 `../../04-当前进度状态/`。
4. 与 Runtime Core 管线相邻 Spec 的关系：`03G` 维护 component matrix / job topology，`05` 维护 ActiveEffectStore 语义，`13` 维护物理布局和容量策略。

## 子页索引

| 文件 | 职责 |
|---|---|
| [13-01 Entity 清单与运行时布局](13-01-Entity清单与运行时布局Spec.md) | FrameArena、DefinitionCatalog、Frame fan-in scratch、ASC、Ability、Request、Active Effect Query 的目标态物理布局。 |
| [13-02 Archetype 与 Component 分类](13-02-Archetype与Component分类Spec.md) | ASC 批量创建、Archetype 审计、IComponentData / IBufferElementData / IEnableable / ChunkComponent / 禁止类型分类。 |
| [13-03 Buffer 容量与 Phase 映射](13-03-Buffer容量与Phase映射Spec.md) | Buffer capacity、物理布局与 phase 对应、不变量和验收门。 |

## 反向入口

- 目标态总入口：[../README.md](../README.md)
- Runtime Core 管线：[../03-RuntimeCore管线Spec.md](../03-RuntimeCore管线Spec.md)
- ActiveEffectStore：[../05-ActiveEffectStoreSpec.md](../05-ActiveEffectStoreSpec.md)
