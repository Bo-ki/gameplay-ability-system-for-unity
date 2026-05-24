# 02 主线任务树

本目录维护 EX-GAS 2.0 的任务树，并承担原 `迭代路线总览` 的职责。当前路线根入口统一为 `../README.md`。

任务树的一级主线、二级支线、三级任务都必须作为 Agent prompt 上下文维护。一级 README 不能只是目录索引，二级支线文档不能只是任务列表；它们必须写清当前问题、目标态参考、历史方案参考、目标 / 非目标、执行范围、验收标准和测试链路。

任务树承接目标态主 Spec，不重新定义目标态。它的工作是把 `01-目标态架构共识/` 的总目标拆成主线、支线和任务，并把每个节点写成 Agent 可以直接领取、循环推进、验证交还的任务提示词。

```text
历史方案讨论 -> 目标态架构共识 -> 主线任务树 -> Goal 循环推进
```

## 当前路线看板

本看板就是当前迭代路线总览，不再拆出独立 `路线总览/` 文件夹。路线阶段必须能落到具体一级主线、二级支线和可领取任务；如果阶段无法落到任务节点，先补任务树上下文。

| 顺序 | 任务名 | 状态 | 所属主线 | 路线作用 |
|---|---|---|---|---|
| 1 | GAS ECS Runtime - Runtime Core 重构 - Freeze Safety Gate | 已完成（Unity验证待补跑） | T1 | `AM-0`，冻结旧 pipeline 扩张 |
| 2 | Observation / Presentation / Debugger - Runtime Core Debugger - Diagnostics Counters Baseline | 已完成（Unity验证待补跑） | T4 | `AM-1`，补 counters 和诊断出口 |
| 3 | GAS ECS Runtime - Runtime Core 重构 - Unity DOTS API 选型修正 | 已完成（文档校准） | T1 | `AM-1B`，把 NativeStream / Chunk Component / Cleanup Component / Bulk Query 等官方 API 纳入目标态选型 |
| 4 | GAS ECS Runtime - Runtime Core 重构 - DOTS API 深读反推 Runtime Core | 已完成（文档校准） | T1 | `AM-1C`，把 Query / Filter / Allocator / Dependency / Chunk layout / Burst calculation 反推回主 Spec |
| 5 | GAS ECS Runtime - Runtime Core 重构 - Unity DOTS 官方案例反推 Runtime Core | 已完成（文档校准） | T1 | `AM-1D`，把 DocCodeSamples / Tests / PerformanceTests 的实际写法转为 `CASE-*` 规则和任务对照 |
| 6 | GAS ECS Runtime - Runtime Core 重构 - Unity DOTS 官方文档参考体系主题化与流程闭环 | 已完成（文档校准） | T1/T0 | `AM-1E`，把官方文档主题入口、覆盖矩阵、`ODF-*` 规则和反哺流程接入目标态与任务执行 |
| 6.5 | 文档治理与目标态共识 - 官方文档查缺补漏与流程闭环 - PackageCache 版本路径与二次覆盖主题补漏 | 已完成（文档校准） | T0/T1 | `AM-1F`，补真实 hash 路径、版本差异、Managed boundary、Baking world、EntityPrefabReference、Burst AOT、allocator aliasing 到 `ODF-09..14` |
| 6.75 | 文档治理与目标态共识 - 官方文档查缺补漏与流程闭环 - Unity Physics / Entities Graphics 新包覆盖 | 已完成（文档校准） | T0/T1/T2/T4/T5/T6 | `AM-1G`，把 Unity Physics 和 Entities Graphics 加入官方文档参考体系、`ODF-15..18`、Debugger、Luban、Generated Glue 和 AutoChess 验收 |
| 6.85 | 文档治理与目标态共识 - 官方文档查缺补漏与流程闭环 - Unity DOTS 官方文档参考顶层拆分 | 已完成（文档校准） | T0/T1 | `AM-1H`，把官方依据库提升到当前路线级 `UnityDOTS官方文档参考/`，目标态 Spec 只作为引用方 |
| 6.95 | 文档治理与目标态共识 - Unity DOTS 官方参考反推目标态与主线审查 | 已完成（文档校准） | T0/T1 | `AM-1I`，确认四层模型保留，但主线推荐先补 Runtime Core Frame Backbone |
| 7 | GAS ECS Runtime - Runtime Core 重构 - EffectCommand 与 SpecStream 契约 | 已完成（Unity验证待补跑） | T1 | `AM-2`，定义新 effect 入口和 phase |
| 7.5.1 | GAS ECS Runtime - Runtime Core Frame Backbone - Schedule / Phase Contract | 已完成（contract-first，Unity验证待补跑） | T1 | `AM-2.5A`，先固定 Runtime Core frame backbone 的 SystemGroup / phase contract |
| 7.5.2 | GAS ECS Runtime - Runtime Core Frame Backbone - Frame Arena 与 Query Budget | 推荐优先 | T1 | `AM-2.5B`，建立 query / lookup / allocator / dependency budget |
| 7.5.3 | GAS ECS Runtime - Runtime Core Frame Backbone - Stream Owner 与 Deterministic Merge | 候选 | T1 | `AM-2.5C`，定义 command/spec/delta/fact owner、clear/read/write/merge 与 deterministic policy |
| 7.5.4 | GAS ECS Runtime - Runtime Core Frame Backbone - Structural Playback Gate | 候选 | T1 | `AM-2.5D`，建立唯一结构变化屏障与 ECB / bulk query policy |
| 7.5.5 | GAS ECS Runtime - Runtime Core Frame Backbone - Debugger Evidence Gate | 候选 | T1/T4 | `AM-2.5E`，补 frame backbone counters 和 Profiler / Journaling 对照口径 |
| 7.5.6 | GAS ECS Runtime - Runtime Core Frame Backbone - AM3 / AM5 Rebind 与交还验证 | 候选 | T1/T4 | `AM-2.5F`，把 AM3 / AM5 绑定到 frame backbone 并决定下一推荐任务 |
| 8 | GAS ECS Runtime - Runtime Core 重构 - Instant Spec Evaluation 迁移 | 进行中（activation simple single-target producer 已接入 stream，继续扩张前需完成 AM-2.5） | T1 | `AM-3`，simple instant GE 默认不创建 runtime GE entity |
| 9 | GAS ECS Runtime - Runtime Core 重构 - Attribute Delta / Typed Facts | 候选 | T1/T4 | `AM-4`，typed facts 成为 reaction 主输入 |
| 10 | GAS ECS Runtime - Runtime Core 重构 - Active Effect Store 重建 | 进行中（owner-local store 第一刀与 Debugger slot pressure baseline 已落地，继续扩张前需完成 AM-2.5） | T1 | `AM-5`，duration/stack/period/granted state 稳定存储 |
| 11 | Runtime Validation Demo - AutoChess 无头验收 - AutoChessDemo 目录迁移与分层重构 | 暂停 | T6/T2/T4 | `DEMO-0`，基础拆迁已做，Runtime 重构完成前不继续推进 |
| 12 | Runtime Validation Demo - AutoChess 无头验收 - AutoChess Read Model 重建 | 后置 | T6 | `AM-6`，Driver / reaction read model 重建 |
| 13 | Observation / Presentation / Debugger - Observation Split - Core 与表现分离 | 后置 | T4 | `AM-7`，Core simulation tick 与 observation 分离 |
| 14 | Burst / Generated 后置优化 - Generated Runtime Glue - Jobify 与静态查表 | 后置 | T5 | `AM-8`，Jobify 和 generated static lookup |
| 15 | Runtime Validation Demo - AutoChess 无头验收 - x50/x100/x1000/x10w/x100w Scale Gates | 后置 | T6/T5 | `AM-9`，规模门槛、压力设计和性能曲线 |

当前第一刀：`AM-0` 已完成静态收口；`AM-1` 已建立 RuntimeCoreDebugger counters baseline；`AM-1B/AM-1C/AM-1D/AM-1E/AM-1F/AM-1G/AM-1H/AM-1I` 已把 Unity DOTS API 选型、Query / Filter / Allocator / Dependency / Chunk layout / Burst calculation、官方案例实际写法、官方文档参考体系主题入口、覆盖矩阵、真实 PackageCache hash 路径、Burst AOT、Managed boundary、Baking world、EntityPrefabReference、allocator aliasing、Unity Physics、Entities Graphics、官方依据库顶层拆分和 DOTS-first 路线重排反推写入目标态和任务树；`AM-2 EffectCommand / SpecStream 契约` 已落地 ECS 数据契约、显式 phase 和 query layout；`AM-3` 和 `AM-5` 已完成局部 proof 小闭环，但继续扩张前必须按 `AM-2.5A -> AM-2.5F` 补齐 Runtime Core Frame Backbone。`GAS ECS Runtime - Runtime Core Frame Backbone - Schedule / Phase Contract` 已完成 contract-first 落点；下一步仍走 GAS Runtime 重构任务线，不回到 AutoChess 业务拆分。当前推荐领取 `GAS ECS Runtime - Runtime Core Frame Backbone - Frame Arena 与 Query Budget`（任务ID：`T1-RuntimeCore-AM2B-B`），把 query / lookup / allocator / dependency budget 接到 frame backbone。`DEMO-0` 已完成基础拆迁并暂停，必须等 GAS Runtime 架构重构形成可验收链路后，再回到 AutoChess 做全链路验证、性能日志分析和对应路线分流。

## 路线原则

1. 先稳 GAS 语义边界，再做性能化和生成化。
2. 先稳 ECS 数据流、实体关系、SystemGroup 调度和配置图，再讨论 API 兼容。
3. OOP 只能位于 Application Shell Layer 或 Runtime Boundary Layer，不能成为 GAS Runtime Core 中间层。
4. 日志、表现 outbox、debug/replay sink 属于 Runtime Boundary Layer 的只读派生，不反向喂给 GAS Runtime Core Layer。
5. 方案12/13/14/15作为验收和架构信号来源；其中 Instant GE 实体化、EventBus 中心化示例只作为风险证据。
6. Unity DOTS 官方文档参考体系作为 Runtime Core 落地规则来源；任务必须先读 `UnityDOTS官方文档参考/README.md`，再让 `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md` 的覆盖矩阵和 `ODF-*` 规则进入行动报告、任务交还和验收摘要。
7. Unity Physics / Entities Graphics 只作为物理输入层和表现边界层进入任务树；相关任务必须引用 `ODF-15..18`，并把 core / physics / render 计时拆分。
8. Runtime Core 遵守 `DOTS Backbone First`：先完成 SystemGroup / Frame Arena / Query / Lookup / Allocator / Dependency / Structural Playback / Debugger evidence，再扩展 AM3 / AM5 的功能迁移。

## 当前不进入

1. 不恢复旧 OOP runtime 主链。
2. 不引入托管 EventBus 作为实时主事件中心。
3. 不让 SourceGenerator 生成 gameplay lifecycle。
4. 不继续给旧 pipeline 堆 fast path 作为主路线。
5. 不在缺少 Runtime Core Frame Backbone 的情况下继续扩大 AM3 / AM5 的旧 lifecycle mirror 或 proof-only stream。

## 主线索引

| 一级主线 | 目录 | 当前重点 |
|---|---|---|
| T0 文档治理与目标态共识 | [T0-文档治理与目标态共识](T0-文档治理与目标态共识/README.md) | 当前文档拆分和 Spec 规范 |
| T1 GAS ECS Runtime | [T1-GAS_ECS_Runtime](T1-GAS_ECS_Runtime/README.md) | Runtime Core rebuild |
| T2 Definition / Luban 配置权威 | [T2-Definition_Luban配置权威](T2-Definition_Luban配置权威/README.md) | generated / bake / integration contract |
| T3 Editor / UI Toolkit Authoring | [T3-Editor_UI_Toolkit_Authoring](T3-Editor_UI_Toolkit_Authoring/README.md) | authoring follows Definition & Generation Layer |
| T4 Observation / Presentation / Debugger | [T4-Observation_Presentation_Debugger](T4-Observation_Presentation_Debugger/README.md) | Runtime Core Debugger and observation split |
| T5 Burst / Generated 后置优化 | [T5-Burst_Generated后置优化](T5-Burst_Generated后置优化/README.md) | jobify / generated glue after semantics |
| T6 Runtime Validation Demo | [T6-RuntimeValidationDemo](T6-RuntimeValidationDemo/README.md) | AutoChess headless validation and scale gates |

## 任务命名

任务显示名采用：

```text
主任务线名 - 副任务线名 - 任务名
```

短 ID 只用于检索，不作为对外主标题。

任务命名和节点上下文必须遵守 `01-目标态架构共识/12-命名规范Spec.md`：主线、支线、任务都要用职责名拼接，不能只用抽象代号。

## 节点规范

| 层级 | 文件形态 | Prompt 职责 |
|---|---|---|
| 一级主线 | `Tn-*/README.md` | 主线边界、当前问题、目标态参考、支线索引、主线验收门槛 |
| 二级支线 | 独立 `.md` | 功能链路上下文、阶段目标、任务看板、支线验收门槛 |
| 三级任务 | 二级文档内章节 | 可领取执行单元、具体范围、验收标准、测试链路、交还内容 |

## 路线维护规则

1. 主线任务树就是当前迭代路线 owner。
2. 路线原则、阶段路线和当前优先级直接维护在本 README 的 `当前路线看板`。
3. 具体执行上下文维护在各一级主线、二级支线和三级任务节点中。
4. 不再维护独立 `路线总览/` 文件夹。
5. 任务节点必须引用 `01` 中的目标态 Spec；历史方案只能作为设计素材和行号定位出现。
6. Goal 模式循环推进时，以任务树节点为 prompt 单元；若节点上下文不足，先补节点或补 Spec，不直接进入实现。
7. DOTS 相关任务节点必须引用 `UnityDOTS官方文档参考/README.md`，再引用 `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`，并说明相关 `ODF-*` 规则和官方文档覆盖主题。
8. DOTS 相关任务节点必须显式处理 `ODF-09..18` 是否相关：PackageCache hash 路径 / 版本差异、Burst AOT / Player 口径、managed boundary、Baking world、EntityPrefabReference / prefab load、allocator aliasing、Unity Physics、Physics 配置、Entities Graphics、render evidence。相关时要进入验收字段，不相关时要写明原因。


