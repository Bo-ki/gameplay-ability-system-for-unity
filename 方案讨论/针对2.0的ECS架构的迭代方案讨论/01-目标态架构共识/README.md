# 01 目标态架构共识

本目录是 EX-GAS 2.0 的目标态 Spec 体系。迭代方案根入口统一为 `../README.md`。

## 主 Spec 定位

`01-目标态架构共识/` 是最终目标和实现约束的唯一设计 owner。这里回答“理想的 GAS 架构应该如何设计、为什么更优、为什么必须这样做、如何验收”。历史方案参考只能先被吸收到这里，形成明确的目标态设计、非目标、契约、验收方式和来源定位；随后 `02-主线任务树/` 再把这些 Spec 拆成主线、支线和任务。

```text
历史方案讨论 -> 目标态架构共识 -> 主线任务树 -> Goal 循环推进
```

任何实现任务都不能直接以历史方案原文为第一性依据。历史方案中的优秀设计必须先落到本目录对应 Spec 的 `历史方案定位` 章节，再进入任务树。

## 纯粹性边界

目录名已经限定职责：`01-目标态架构共识/` 必须能被当成 EX-GAS 2.0 框架设计 Spec 读取。读者不应在这里判断“现实代码做到哪里”，而应只看到“理想目标态应该是什么、为什么必须如此、如何验收”。

本目录不记录实现事实、过渡落点、实现流水、已生成文件清单、profile 结果或下一步计划。需要这些内容时写入：

| 内容 | Owner |
|---|---|
| 实现代码事实、缺陷诊断、`MigrationProofOnly` 实现证据、生成链路复审事实 | [00-当前架构事实](../00-当前架构事实/README.md) |
| 可执行任务、路线拆分、下一步推进 | [02-主线任务树](../02-主线任务树/README.md) |
| 跨轮 handoff、最近验证摘要、短期窗口 | [04-当前进度状态](../04-当前进度状态/README.md) |

目标态 Spec 可以引用事实 owner，但正文只能保留目标约束、官方依据、API 选型、禁止方向和验收门槛。

## Spec 索引

| 文件 | 职责 |
|---|---|
| [00-总览Spec](00-总览Spec.md) | 目标态总览、四层工程模型和主约束 |
| [01-GAS概念模型Spec](01-GAS概念模型Spec.md) | GAS 概念到 Unity ECS 的映射 |
| [01B-GAS业务语义链路概念设计Spec](01B-GAS业务语义链路概念设计Spec.md) | GAS 概念设计审查结论：权威状态、frame-local record、Boundary 投影和 OOP Shell 边界 |
| [02-四层架构Spec](02-四层架构Spec.md) | Application Shell / Runtime Boundary / GAS Runtime Core / Definition & Generation |
| [03-RuntimeCore管线Spec](03-RuntimeCore管线Spec.md) / [03 子 Spec 索引](03-RuntimeCore管线/README.md) | Runtime Core phase、stream、SystemGroup、lane、Frame Arena、DOTS API 策略和 System/Lane Catalog 目标 |
| [04-EffectCommand-SpecStream-AttributeDeltaSpec](04-EffectCommand-SpecStream-AttributeDeltaSpec.md) | Effect Command、Spec/Delta/Fact 语义链、目标态 fan-in 承载 |
| [05-ActiveEffectStoreSpec](05-ActiveEffectStoreSpec.md) | Duration / Stack / Period / Granted state |
| [06-Observation-Presentation-ReplaySpec](06-Observation-Presentation-ReplaySpec.md) | Observation、Presentation、Replay 分层 |
| [07-RuntimeCoreDebuggerSpec](07-RuntimeCoreDebuggerSpec.md) | Runtime Core Debugger 诊断事实、ValidationEvidence 与图表 |
| [08-Luban-SourceGenerator配置生成链路Spec](08-Luban-SourceGenerator配置生成链路Spec.md) | Luban / SourceGenerator / Definition / Bake 链路 |
| [09-DefinitionAuthoring边界Spec](09-DefinitionAuthoring边界Spec.md) | Definition Authoring 输入面、Baker/Bootstrap 和 Runtime Core 隔离边界 |
| [10-AutoChess无头验收Spec](10-AutoChess无头验收Spec.md) | AutoChessDemo 验收 Demo 目标态、Battle Runtime Adapter、业务链路和自动验收 |
| [10B-AutoChess完整业务案例设计Spec](10B-AutoChess完整业务案例设计Spec.md) / [10B 子 Spec 索引](10B-AutoChess完整业务案例/README.md) | 完整 GAS 设计预演总览：具名棋子、具体 Excel 配置、羁绊机制、Runtime 基础设施、目标代码样例、业务流程走查和交互矩阵 |
| [11-AutoChessDemo-Luban配置方案Spec](11-AutoChessDemo-Luban配置方案Spec.md) | AutoChessDemo Luban 表、SourceGenerator 输出、ScaleProfile 和自动验收配置 |
| [12-命名规范Spec](12-命名规范Spec.md) | 四层职责命名、后缀语义、限制词和任务命名规范 |
| [13-EntityComponent物理布局Spec](13-EntityComponent物理布局Spec.md) | Entity/Component 物理布局、Archetype 审计、Buffer 容量策略 |
| [14-DefinitionCodeGen目标链路Spec](14-DefinitionCodeGen目标链路Spec.md) | Definition CodeGen 到 Runtime catalog / lookup / pure glue 的目标链路 |
| [15-SourceGenerator职责边界Spec](15-SourceGenerator职责边界Spec.md) | SourceGenerator 权限边界、允许/禁止生成物、官方规则论证 |
| [16-纯ECS内核与边界重划分Spec](16-纯ECS内核与边界重划分Spec.md) / [16 子 Spec 索引](16-纯ECS内核与边界重划分/README.md) | 纯 ECS Runtime Core、OOP Shell、Runtime Boundary、Debugger evidence、SourceGenerator pure glue 的目标态总览、接口和验收门 |
| [17-GAS业务链路破坏性重划分Spec](17-GAS业务链路破坏性重划分Spec.md) | GAS 业务链路破坏性收权、旧链路退出门槛、设计理由和目标态验收口径 |
| [18-DOTS官方规范复核与性能红线Spec](18-DOTS官方规范复核与性能红线Spec.md) | 用 Unity DOTS 官方规则复核新划分设计，固化 Runtime Core API 选型、性能红线和验收门槛 |
| [19-GAS业务编辑路径与配置链职责Spec](19-GAS业务编辑路径与配置链职责Spec.md) | 以真实业务场景定义更短 GAS 编辑路径，并划分 Luban、Editor、SourceGenerator 的配置链职责 |
| [20-策划配置能力交叉审查Spec](20-策划配置能力交叉审查Spec.md) | 从引用图、发布门禁、协作、平衡、场景验证、表现绑定和 SourceGenerator Editor Binding 交叉约束策划配置能力 |
| [21-AutoChessDemo策划配置验收样例Spec](21-AutoChessDemo策划配置验收样例Spec.md) | 以 AutoChessDemo 为验收场，审查 10/10B/11 并定义核心测试能力包、row projection、trace preview 和 scenario validation 样例 |
| [22-新增能力业务推进流程Spec](22-新增能力业务推进流程Spec.md) | 从策划和程序协作视角定义新增能力三档分类、最短路径、程序介入门槛和 DOTS Official Review Gate |
| [23-能力配置链条与分析步骤Spec](23-能力配置链条与分析步骤Spec.md) | 细化 Editor / Authoring 配置链，定义能力配置分析协议、row projection plan、trace、impact、scenario 和 publish snapshot |
| [24-GAS官方概念对照复核Spec](24-GAS官方概念对照复核Spec.md) | 用 Epic 官方 GAS 概念复核 19/20/22/23，固化 Ability lifecycle、GE spec、Tag taxonomy、Cue parameters、AbilityTask 映射和 ASC binding |
| [90-目标态不变量](90-目标态不变量.md) | 全局不变量 |
| [91-术语表](91-术语表.md) | 术语和缩写 |

## 官方依据入口

Unity DOTS 官方依据不再维护在本目录内。所有 Runtime Core、Debugger、Luban、AutoChessDemo 相关 Spec 必须按需引用下列官方依据入口：

| 入口 | 职责 |
|---|---|
| [UnityDOTS官方文档参考](../../UnityDOTS官方文档参考/README.md) | PackageCache 版本、单主题索引、DOTS API 规则、官方案例、流程闭环 |
| [规则编号索引](../../UnityDOTS官方文档参考/主题/90-规则编号索引.md) | `SYS/JOB/QRY/SC/ECB/BUF/SEL/CASE/ODF` 等规则族入口 |
| [GAS Runtime Core API 选型基线](../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md) | Runtime Core 任务执行前的 API selection checkpoint |
| [Luban / SourceGenerator 收权论证](15-SourceGenerator职责边界Spec.md) | 将官方 DOTS 规则映射到生成器职责边界，说明为什么 generated lifecycle 不是目标态 |
| [DOTS 官方规范复核与性能红线](18-DOTS官方规范复核与性能红线Spec.md) | 将官方 DOTS 规则映射到四层划分、GAS 业务链路、API 选型门槛和性能红线 |

## 整体重划分代码入口

需要理解目标态 GAS 架构“应该如何重新划分、为什么这样划分更合理、完整代码骨架如何阅读”时，默认从 [16-01 目标分层、官方依据与不变量](16-纯ECS内核与边界重划分/16-01-目标分层官方依据与不变量Spec.md) 开始。该页给出 Shell capability、Boundary command、Core `IJobChunk`、`NativeStream` deterministic fan-in、Diagnostics evidence、GeneratedDefinitionGlue 和 StructuralCommit 的目标态完整代码骨架与代码级说明。

阅读顺序：

1. 先读 `目标态模块 Owner Map`，确认哪些 Module 是目标态 owner。
2. 再读 `目标态职责重划分验收表`，确认每个 owner 必须拥有和不得拥有的内容。
3. 最后读 `目标态最小完整代码骨架`、`代码解读` 和 `重新划分合理性的代码级说明`，确认接口为什么不泄露 ECS handle、query、ECB、NativeContainer owner 或 generated lifecycle。

## 维护规则

本目录遵守 [规范手册](../规范手册.md) 第3节（目标态 Spec 规范），不在此重复。

本目录特有规则：

1. 本目录只维护目标态设计，不记录实现流水。
2. 已实现事实、当前落点、`MigrationProofOnly` 实现证据和 profile 结果回写 `../00-当前架构事实/`，不在本目录追加完成清单。
3. 历史方案吸收关系维护在对应 Spec 的 `历史方案定位` 章节，使用 `../历史方案参考/方案N.md:line-line` 形式。
4. 任务树节点必须引用本目录中的具体 Spec；找不到对应 Spec 时先补目标态设计再拆任务。
5. Runtime Core 相关 Spec 必须先引用 `../../UnityDOTS官方文档参考/README.md`。
6. 新增 Unity 官方文档结论时，先通过 `../../UnityDOTS官方文档参考/README.md` 判断归属，再反哺对应业务 Spec。
7. 文件标题含 `计划`、`复审`、`当前`、`事实`、`落点`、`进度` 的文档默认不应放在本目录；若确需保留，必须改写为纯目标态 Spec。
8. 任何章节标题含“历史证据”“当前状态”“本轮实现”“已落地”“计划中”的内容，默认判定为 owner 归位错误；除非改写成目标态 evidence model、API gate、禁止方向或官方依据，否则必须迁出本目录。
9. 写入本目录前必须先做 owner 判定：如果句子回答“现实代码现在是什么、哪些文件命中、哪次验证通过、下一轮做什么”，它不属于 `01`；如果句子回答“理想架构必须怎样设计、为什么这样设计、怎样验收”，才允许留在 `01`。
