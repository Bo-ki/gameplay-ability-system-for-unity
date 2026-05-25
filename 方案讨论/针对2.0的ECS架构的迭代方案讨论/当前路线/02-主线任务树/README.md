# 02 主线任务树

本目录维护 EX-GAS 2.0 的任务树，并承担原 `迭代路线总览` 的职责。当前路线根入口统一为 `../README.md`。

任务树的每个节点（不论处于树的哪一层）都必须能作为 Agent prompt 上下文。分支节点提供子树索引和验收门槛，叶子节点提供可直接领取的执行单元。节点文档不是目录索引或任务列表——必须写清当前问题、目标态参考、历史方案参考、目标/非目标、执行范围、验收标准和测试链路。

任务树承接目标态主 Spec，不重新定义目标态。它的工作是把 `01-目标态架构共识/` 的总目标拆成主线、支线和任务，并把每个节点写成 Agent 可以直接领取、循环推进、验证交还的任务提示词。

```text
历史方案讨论 -> 目标态架构共识 -> 主线任务树 -> Goal 循环推进
```

## 当前路线看板

本看板就是当前迭代路线总览，不再拆出独立 `路线总览/` 文件夹。路线阶段必须能落到具体分支节点和可领取叶子节点；如果阶段无法落到任务节点，先补任务树上下文。

| 顺序 | 任务名 | 状态 | 所属主线 | 路线作用 |
|---|---|---|---|---|
| 1 | GAS ECS Runtime - Runtime Core 重构 - Freeze Safety Gate | 契约已确立 | T1 | AM-0，冻结旧 pipeline 扩张 |
| 2 | Observation / Presentation / Debugger - Runtime Core Debugger - Diagnostics Counters Baseline | 契约已确立 | T4 | AM-1，补 counters 和诊断出口 |
| 3 | 文档治理与目标态共识 - Unity DOTS 官方文档全覆盖系列（AM-1B~AM-2G，25轮） | 已完成 | T0/T1 | ~175 个 PackageCache 文档全部读完，CASE 47 / PRF 34 / FSM 6 / SEL 5 / ODF 18 规则体系建立。完整历史见 [已完成文档校准日志](T0-文档治理与目标态共识/已完成文档校准日志.md) |
| 4 | GAS ECS Runtime - Runtime Core 重构 - EffectCommand 与 SpecStream 契约 | 契约已确立 | T1 | AM-2，定义新 effect 入口和 phase，command/spec/delta/fact 数据契约落地 |
| 5 | GAS ECS Runtime - Runtime Core Frame Backbone - Schedule / Phase Contract | 契约已确立 | T1 | AM-2.5A，SystemGroup / phase contract |
| 6 | GAS ECS Runtime - Runtime Core Frame Backbone - Frame Arena 与 Query Budget | 契约已确立 | T1 | AM-2.5B，query / lookup / allocator / dependency budget |
| 7 | GAS ECS Runtime - Runtime Core Frame Backbone - Stream Owner 与 Deterministic Merge | 契约已确立 | T1 | AM-2.5C，command/spec/delta/fact owner、clear/read/write/merge |
| 8 | GAS ECS Runtime - Runtime Core Frame Backbone - Structural Playback Gate | 契约已确立 | T1 | AM-2.5D，唯一结构变化屏障 |
| 9 | GAS ECS Runtime - Runtime Core Frame Backbone - Debugger Evidence Gate | 契约已确立 | T1/T4 | AM-2.5E，frame backbone counters 和 Profiler / Journaling 对照 |
| 10 | GAS ECS Runtime - Runtime Core Frame Backbone - AM3 / AM5 Rebind 与交还验证 | 契约已确立 | T1/T4 | AM-2.5F，AM3 / AM5 绑定到 backbone |
| 11 | GAS ECS Runtime - Runtime Core 重构 - Instant Spec Evaluation 迁移 | 推荐优先 | T1 | AM-3，simple instant GE 默认不创建 runtime GE entity。已完成：激活/cost/Timeline ApplyEffects single+multi target producer，attribute/cue/generic/damage typed fact native consumer。AM3 完成条件见下方 AM3 完成边界 |
| 12 | GAS ECS Runtime - Runtime Core 重构 - Active Effect Store 重建 | 进行中 | T1 | AM-5，duration/stack/period/granted state 稳定存储。owner-local store + Debugger slot baseline + period/overflow derived command proof 已落地 |
| 13 | Runtime Validation Demo - AutoChess 无头验收 - AutoChessDemo 目录迁移与分层重构 | 暂停 | T6 | DEMO-0，基础拆迁已做，等 Runtime Core 形成可验收链路后恢复 |
| 14 | Observation / Presentation / Debugger - Observation Split - Core 与表现分离 | 后置 | T4 | AM-7，Core simulation tick 与 observation 分离 |
| 15 | Burst / Generated 后置优化 - Generated Runtime Glue | 后置 | T5 | AM-8，Jobify 和 generated static lookup |
| 16 | Runtime Validation Demo - AutoChess 无头验收 - 规模压力测试 | 后置 | T6 | AM-9，x50/x100/x1000/x10w/x100w scale gates |

### AM3 完成边界

AM3 的"完成"不要求迁移所有旧 producer。以下条件全部满足后 AM3 可标为已完成：

1. activation / cost / Timeline ApplyEffects single+multi target simple instant producer 全部走 `BEffectCommand` 主链（已满足）
2. attribute / cue / generic gameplay / damage typed fact 的 native Presentation / Replay consumer 全部就位（已满足）
3. EventBus legacy callsite writer 迁移完成，无静态 `EventBusHelper.Enqueue*` 裸调用残留在热路径
4. parallel fan-in / deterministic merge 在 AM2B backbone 约束下可演示（至少一个多 job producer fan-in 场景通过 contract test）
5. AM3 路径不再产生临时 `CApplyGameplayEffectRequest` 或 runtime GE entity（已满足 simple instant 路径；复杂 GE fallback 明确标记为 Boundary request 并由 AM5 承接）

### 看板变更说明（本轮文档治理）

相比上轮看板：
- **AM-1B~AM-2G 25 条文档校准条目压缩为 1 条归档摘要**（条目 3），完整历史迁入 [已完成文档校准日志](T0-文档治理与目标态共识/已完成文档校准日志.md)
- **AM-4（Attribute Delta / Typed Facts）移除**：AM3 已完成 attribute/cue/generic/damage typed fact 的 native consumer，AM-4 独立存在的价值已被 AM3 覆盖
- **AM-6（AutoChess Read Model）移除**：Runtime Core 重构完成前 AutoChess 细节无法确定，届时重新评估
- **状态标签统一**：所有"已完成（Unity验证待补跑）"和"已完成（文档校准）"替换为规范的"契约已确立"或"已完成"

当前状态：`AM-0` 静态收口 + `AM-1` Debugger counters baseline 已完成。Unity DOTS 官方文档全覆盖系列（AM-1B~AM-2G，25轮）已读完 ~175 个 PackageCache 文档，CASE 47 / PRF 34 / FSM 6 / SEL 5 / ODF 18 规则体系建立。`AM-2 EffectCommand / SpecStream 契约` 已落地 ECS 数据契约。`AM-2.5A -> AM-2.5F` Frame Backbone 连续任务链完成 contract-first：phase contract、frame budget、stream owner / deterministic merge、structural playback gate、Debugger evidence gate、AM3 / AM5 rebind handoff。`AM-3` 已完成 activation/cost/Timeline ApplyEffects single+multi target simple instant producer 迁移，attribute/cue/generic/damage typed fact native consumer 全部就位。`AM-5` 已完成 owner-local store + Debugger slot baseline + period/overflow derived command proof。下一步继续推荐 AM3 剩余 simple instant producer 迁移和 parallel fan-in / deterministic merge 在 AM2B backbone 约束下的落地。

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

| 主线 | 目录 | 当前重点 |
|---|---|---|
| T0 文档治理与目标态共识 | [T0-文档治理与目标态共识](T0-文档治理与目标态共识/README.md) | 当前文档拆分和 Spec 规范 |
| T1 GAS ECS Runtime | [T1-GAS_ECS_Runtime](T1-GAS_ECS_Runtime/README.md) | Runtime Core rebuild |
| T2 Definition / Luban 配置权威 | [T2-Definition_Luban配置权威](T2-Definition_Luban配置权威/README.md) | generated / bake / integration contract |
| T3 Editor / UI Toolkit Authoring | [T3-Editor_UI_Toolkit_Authoring](T3-Editor_UI_Toolkit_Authoring/README.md) | authoring follows Definition & Generation Layer |
| T4 Observation / Presentation / Debugger | [T4-Observation_Presentation_Debugger](T4-Observation_Presentation_Debugger/README.md) | Runtime Core Debugger and observation split |
| T5 Burst / Generated 后置优化 | [T5-Burst_Generated后置优化](T5-Burst_Generated后置优化/README.md) | jobify / generated glue after semantics |
| T6 Runtime Validation Demo | [T6-RuntimeValidationDemo](T6-RuntimeValidationDemo/README.md) | 10B 完整业务设计已就绪；AutoChess headless validation and scale gates |

## 任务命名

任务显示名采用职责路径拼接：

```text
主线名 - 子节点名 - 叶子节点名
```

短 ID 只用于检索，不作为对外主标题。节点命名遵守 `01-目标态架构共识/12-命名规范Spec.md`。

## 节点模型

任务树不预设固定层级。节点只有两种类型：

- **叶子节点**：无子节点，Agent 直接领取执行
- **分支节点**：有子节点，只提供上下文索引，不可直接领取

树结构通过 [任务树规范](任务树规范.md) 中的拆分触发条件（轮次≥5、行数>200、多关注点、依赖链）自动生长。深度不受硬性限制，通过合并触发（所有子节点完成→父节点自动完成、单子合并）自调节。

主干索引见上方「主线索引」。具体任务上下文在各节点的文档中。

## 路线维护规则

1. 主线任务树就是当前迭代路线 owner。
2. 路线原则、阶段路线和当前优先级直接维护在本 README 的 `当前路线看板`。
3. 具体执行上下文维护在各主线、支线和叶子节点中。
4. 不再维护独立 `路线总览/` 文件夹。
5. 任务节点必须引用 `01` 中的目标态 Spec；历史方案只能作为设计素材和行号定位出现。
6. Goal 模式循环推进时，以叶子节点为 prompt 单元；若上下文不足先补节点或补 Spec，不直接进入实现。
7. DOTS 相关任务节点必须引用 `UnityDOTS官方文档参考/README.md`，再引用 `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`，并说明相关 `ODF-*` 规则和官方文档覆盖主题。
8. DOTS 相关任务节点必须显式处理 `ODF-09..18` 是否相关：PackageCache hash 路径 / 版本差异、Burst AOT / Player 口径、managed boundary、Baking world、EntityPrefabReference / prefab load、allocator aliasing、Unity Physics、Physics 配置、Entities Graphics、render evidence。相关时要进入验收字段，不相关时要写明原因。
