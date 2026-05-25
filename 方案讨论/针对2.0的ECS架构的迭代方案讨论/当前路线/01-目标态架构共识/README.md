# 01 目标态架构共识

本目录是 EX-GAS 2.0 当前迭代的主 Spec 体系。当前路线根入口统一为 `../README.md`。

## 主 Spec 定位

`01-目标态架构共识/` 是最终目标和实现约束的唯一设计 owner。历史方案参考只能先被吸收到这里，形成明确的目标态设计、非目标、契约、验收方式和行号定位；随后 `02-主线任务树/` 再把这些 Spec 拆成主线、支线和任务。

```text
历史方案讨论 -> 目标态架构共识 -> 主线任务树 -> Goal 循环推进
```

任何实现任务都不能直接以历史方案原文为第一性依据。历史方案中的优秀设计必须先落到本目录对应 Spec 的 `历史方案定位` 章节，再进入任务树。

## Spec 索引

| 文件 | 职责 |
|---|---|
| [00-总览Spec](00-总览Spec.md) | 目标态总览、四层工程模型和主约束 |
| [01-GAS概念模型Spec](01-GAS概念模型Spec.md) | GAS 概念到 Unity ECS 的映射 |
| [02-四层架构Spec](02-四层架构Spec.md) | Application Shell / Runtime Boundary / GAS Runtime Core / Definition & Generation |
| [03-RuntimeCore管线Spec](03-RuntimeCore管线Spec.md) | Runtime Core phase、stream 和 SystemGroup 目标 |
| [04-EffectCommand-SpecStream-AttributeDeltaSpec](04-EffectCommand-SpecStream-AttributeDeltaSpec.md) | Effect Command、Instant Spec、Attribute Delta |
| [05-ActiveEffectStoreSpec](05-ActiveEffectStoreSpec.md) | Duration / Stack / Period / Granted state |
| [06-Observation-Presentation-ReplaySpec](06-Observation-Presentation-ReplaySpec.md) | Observation、Presentation、Replay 分层 |
| [07-RuntimeCoreDebuggerSpec](07-RuntimeCoreDebuggerSpec.md) | Runtime Core Debugger 诊断事实与图表 |
| [08-Luban-SourceGenerator配置生成链路Spec](08-Luban-SourceGenerator配置生成链路Spec.md) | Luban / SourceGenerator / Definition / Bake 链路 |
| [09-Authoring-EditorSpec](09-Authoring-EditorSpec.md) | UI Toolkit authoring 与 Editor 边界 |
| [10-AutoChess无头验收Spec](10-AutoChess无头验收Spec.md) | AutoChessDemo 验收 Demo 目标态、目录架构、业务链路和自动验收 |
| [10B-AutoChess完整业务案例设计Spec](10B-AutoChess完整业务案例设计Spec.md) | 完整 GAS 设计预演：具名棋子、具体 Excel 配置、C# System 实现、业务流程走查和交互矩阵 |
| [11-AutoChessDemo-Luban配置方案Spec](11-AutoChessDemo-Luban配置方案Spec.md) | AutoChessDemo Luban 表、SourceGenerator 输出、ScaleProfile 和自动验收配置 |
| [12-命名规范Spec](12-命名规范Spec.md) | 四层职责命名、后缀语义、限制词和任务命名规范 |
| [13-EntityComponent物理布局Spec](13-EntityComponent物理布局Spec.md) | Entity/Component 物理布局、Archetype 审计、Buffer 容量策略 |
| [90-目标态不变量](90-目标态不变量.md) | 全局不变量 |
| [91-术语表](91-术语表.md) | 术语和缩写 |

## 官方依据入口

Unity DOTS 官方依据不再维护在本目录内。所有 Runtime Core、Debugger、Luban、AutoChessDemo 相关 Spec 必须按需引用当前路线级入口：

| 入口 | 职责 |
|---|---|
| [UnityDOTS官方文档参考](../UnityDOTS官方文档参考/README.md) | PackageCache 版本、单主题索引、DOTS API 规则、官方案例、流程闭环 |
| [规则编号索引](../UnityDOTS官方文档参考/主题/90-规则编号索引.md) | `SYS/JOB/QRY/SC/ECB/BUF/SEL/CASE/ODF` 等规则族入口 |
| [GAS Runtime Core API 选型基线](../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md) | Runtime Core 任务执行前的 API selection checkpoint |

## 维护规则

本目录遵守 `Spec规范.md`（Spec结构、图表要求、Unity Entities校准要求、API选型章节、官方文档覆盖维护规则），不在此重复。

本目录特有规则：

1. 本目录只维护目标态设计，不记录实现流水。
2. 已实现事实回写 `../00-当前架构事实/`，不在本目录追加完成清单。
3. 历史方案吸收关系维护在对应 Spec 的 `历史方案定位` 章节，使用 `../历史方案参考/方案N.md:line-line` 形式。
4. 任务树节点必须引用本目录中的具体 Spec；找不到对应 Spec 时先补目标态设计再拆任务。
5. Runtime Core 相关 Spec 必须先引用 `../UnityDOTS官方文档参考/README.md`。
6. 新增 Unity 官方文档结论时，先通过 `../UnityDOTS官方文档参考/README.md` 判断归属，再反哺对应业务 Spec。

