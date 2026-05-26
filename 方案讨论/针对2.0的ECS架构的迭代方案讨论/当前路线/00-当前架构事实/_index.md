# 00 当前架构事实 — 总看板

## 职责边界

本看板覆盖当前版本的架构事实摘要、核心问题诊断和合规缺陷总览。它回答"当前版本到底有什么问题、哪些事实已成立、哪些缺陷待修"。

**覆盖**：Runtime 主链事实、配置链事实、架构图、模块索引、核心问题看板、合规缺陷。
**不覆盖**：目标态设计（见 `../01-目标态架构共识/`）、任务拆解和进度（见 `../02-主线任务树/`）、DOTS 规则参考（见 `../UnityDOTS官方文档参考/`）。

## 先读关系

1. **维护规范** → `维护规范.md`（必读，理解问题进入/归档流程和 ISSUE 模板）
2. **架构事实** → `架构事实/_index.md`（当前版本 Runtime/Definition/Sytem 模块事实）
3. **核心问题看板** → 本文（所有 Active 问题的索引和总诊断）
4. **合规审查** → `合规审查/_index.md`（DOTS 代码合规缺陷清单）

## 当前架构事实摘要

### Runtime 主链
1. 外部接入以 `AbilitySystemBinding + AbilitySystemFacade` 为主；写操作通过 request entity 进入 ECS。
2. AM2 已落地 `EffectCommandSpecStream` 数据契约；AM3 已完成 simple instant 局部 proof；AM5 已落地 owner-local `ActiveEffectStore` mirror。
3. Presentation outbox、Replay sink、GameplayEventBus 已经分层，但 Observation 仍需要继续从 hot path 拆离。
4. 当前 Runtime Core 仍缺少统一 frame backbone——contracts 已有（frame budget / stream owner / deterministic merge / structural playback gate / Debugger evidence gate），但真实 SystemGroup 搬迁未完成。
5. 旧 GE lifecycle pipeline（request entity → runtime GE entity → lifecycle → eventbus）仍是待迁移对象。

### Definition / 配置链
6. `GASDefinitionTable` 已统一 Ability/GE/Attribute/Tag/Cue summary contract；generated → bake → runtime integration contract 链已形成。
7. AutoChess 已有 generated package 和 Luban process gate 样板。

### 架构边界
8. Demo 业务仍需迁移到 `Assets/AutoChessDemo`——当前部分仍位于 `Assets/GAS/Runtime/Demo`。
9. 当前 6 个旧 SystemGroup（GASCommand/GASEffect/GASAttribute 等）与目标态 8 个新 SystemGroup 完全不匹配。

## 核心问题看板

| ID | 问题 | 状态 | 严重度 | 完整诊断 | 应查看 Spec | 应领取任务 |
|----|------|------|--------|----------|-------------|-----------|
| ISSUE-001 | GE 生命周期管线过重 | Active | P0 | [ISSUE-001](核心问题诊断/ISSUE-001-GE生命周期管线过重.md) | `03-RuntimeCore管线Spec`, `04-EffectCommand-SpecStream-AttributeDeltaSpec` | `T1-GAS_ECS_Runtime/RuntimeCore重构/` |
| ISSUE-002 | Observation 与 Runtime Core 热路径耦合 | Active | P0 | [ISSUE-002](核心问题诊断/ISSUE-002-Observation与RuntimeCore热路径耦合.md) | `06-Observation-Presentation-ReplaySpec` | `T4-Observation_Presentation_Debugger/` |
| ISSUE-003 | Runtime Core Debugger 证据不足 | Active | P1 | [ISSUE-003](核心问题诊断/ISSUE-003-RuntimeCoreDebugger证据不足.md) | `07-RuntimeCoreDebuggerSpec` | `T4-Observation_Presentation_Debugger/RuntimeCoreDebugger/` |
| ISSUE-004 | 结构变化边界脆弱 | Mitigated | P0 | [ISSUE-004](核心问题诊断/ISSUE-004-结构变化边界脆弱.md) | `03-RuntimeCore管线Spec`, `05-ActiveEffectStoreSpec` | `T1-GAS_ECS_Runtime/RuntimeCore重构/` |
| ISSUE-005 | Generated 链路未反哺 Runtime Core | Active | P1 | [ISSUE-005](核心问题诊断/ISSUE-005-Generated链路未反哺RuntimeCore.md) | `08-Luban-SourceGenerator配置生成链路Spec` | `T2-Definition_Luban配置权威/` |
| ISSUE-006 | AutoChess Demo 边界混入 Runtime Core | Active | P1 | [ISSUE-006](核心问题诊断/ISSUE-006-AutoChessDemo边界混入RuntimeCore.md) | `10-AutoChess无头验收Spec` | `T6-RuntimeValidationDemo/AutoChess无头验收/` |
| ISSUE-007 | 任务上下文与目标态 Spec 断链 | Active | P1 | [ISSUE-007](核心问题诊断/ISSUE-007-任务上下文与目标态Spec断链.md) | `12-命名规范Spec` | `T0-文档治理与目标态共识/` |
| ISSUE-008 | 目标态 Spec 尚未充分 Unity Entities 机制化 | Mitigated | P1 | [ISSUE-008](核心问题诊断/ISSUE-008-目标态Spec尚未充分UnityEntities机制化.md) | `README.md`, `90-规则编号索引.md` | `T1-GAS_ECS_Runtime/RuntimeCore重构/` |
| ISSUE-009 | Runtime Core Frame Backbone 缺失 | Resolved (P0→P1) | — | [ISSUE-009](核心问题诊断/ISSUE-009-RuntimeCoreFrameBackbone缺失.md) | `03-RuntimeCore管线Spec` | `T1-GAS_ECS_Runtime/RuntimeCore重构/AM2B-FrameBackbone/` |
| ISSUE-010 | 代码执行范式未切换到 DOTS | Active | P0 | [ISSUE-010](核心问题诊断/ISSUE-010-代码执行范式未切换到DOTS.md) | `03-RuntimeCore管线Spec` | `T1-GAS_ECS_Runtime/RuntimeCore重构/` |
| ISSUE-011 | 临时 EntityQuery 泛滥与 API 承载选型错误 | Active | P0 | [ISSUE-011](核心问题诊断/ISSUE-011-临时EntityQuery泛滥与API承载选型错误.md) | `04-EffectCommand-SpecStream-AttributeDeltaSpec` | `T1-GAS_ECS_Runtime/RuntimeCore重构/` |

### 开放点摘要（按 ISSUE 归组）

以下摘要来自 `当前未闭合点.md`，详细信息在对应 ISSUE 文档。每条后标注原开放点编号（OP-x）。

**ISSUE-001 (GE 生命周期管线过重)**
- AM3 已完成 direct simple instant evaluation 主链落点，并迁入 ability activation / cost / Timeline single-target 与 multi-target simple instant producer (OP-3)
- AM5 period / overflow simple instant child GE 已能写入 EffectCommand 并复用 AM3 主链 (OP-3)
- AM5 ASC owner-local slot 已镜像 duration lifecycle；granted tag/ability cleanup、store-driven lifecycle、slot compact 仍未闭合 (OP-4)
- Cooldown、复杂 cost fallback、duration/stack/tag requirements/granted state ApplyEffects fallback、剩余 producer、业务 reaction typed fact consumer 和 parallel fan-in / deterministic merge 未闭合 (OP-3, OP-6, OP-11, OP-12, OP-17)

**ISSUE-002 (Observation 热路径耦合)**
- attribute/cue/generic/damage typed fact 已绕过旧 EventBus 直接投影 Presentation/Replay (OP-7)
- 业务 reaction、Replay sampling 和 projection tick 独立统计未闭合 (OP-7)

**ISSUE-003 (Debugger 证据不足)**
- Debugger 已有 AM-1 baseline + AM5 slot pressure/state distribution baseline (OP-2)
- 逐系统结构变化预算、cleanup/compact/chunk skip 证据待补 (OP-2)

**ISSUE-009 (Frame Backbone)**
- AM2B-A~F 全部 contract 已确立，但真实 SystemGroup 搬迁、Profiler/Journaling 采样和 AutoChess profile 未闭合 (OP-5)

**跨 ISSUE 流程要求**
- EffectCommand 承载 API 选型复核（singleton DynamicBuffer vs per-owner vs NativeStream vs ECB → 写入行动报告）(OP-11)
- ActiveEffectStore API 选型复核（stable entity vs enableable vs enum state vs Cleanup vs Chunk vs LinkedEntityGroup）(OP-12)
- AutoChess validation summary 补齐 API 选型健康指标 (OP-13)
- 后续行动报告必须补 DOTS 官方案例对照（CASE-01/04/05/07/08/10/11）和官方文档覆盖检查（ODF-* 规则）(OP-18, OP-19)

## 当前总诊断

当前不应继续把问题描述为"缺少某个业务机制"。真正的当前问题是 Runtime Core 主流程尚未从旧 lifecycle / global observation stream 迁移到 phase + stream 驱动模型；同时当前实现还缺少 Unity DOTS 意义上的 Runtime Core Frame Backbone，无法统一归因 query / lookup / allocator / dependency / structural playback / deterministic stream / Debugger evidence。

**2026-05-26 审查更新**: 代码库已全面 ECS 化（222 个文件，38 个 ISystem），GASSystemScheduleContract(447行) 已完整定义 8-phase 管线契约，Stream 处理系统（SEffectCommandSpecStreamPhases, 619行）使用基于游标的 for 循环——这是新代码的正面范例。但核心热路径（SEffectApply、SEffectTick、SAbilityCommit、SApplyGameplayEffectRequest）仍全主线程 foreach + ToEntityArray，EffectRuntimeUtility(2125行) 仍有 8+ 处 ECB 碎片化 PlaybackAndReset。Typed Simulation Fact 桥接模式（BTypedSimulationFact → Projection → EventBridge → 双出口）已清晰分离模拟事实与表现投影。详见 `审查更新-2026-05-26.md`。

AM2 已先固定 ECS 数据契约；AM3 已完成 direct simple instant evaluation 主链落点；AM5 已开始把 duration lifecycle 镜像到 ActiveEffectStore；AM2B contract-first backbone 已设立 frame budget / stream owner / structural playback gate / Debugger evidence gate 契约体系。下一步是真实 SystemGroup 搬迁、剩余 producer 迁移、parallel fan-in / deterministic merge 落地和 Profiler/Journaling 证据采样。

后续领取 Runtime 任务前，必须先读本看板和对应 ISSUE 文档。如果任务执行中发现新的系统性问题，先按 `维护规范.md` 补问题诊断，再拆任务或改目标态 Spec。

## 拓展阅读路径

| 去 | 看什么 |
|----|--------|
| `../01-目标态架构共识/` | 所有 Spec 文档，目标态设计 |
| `../02-主线任务树/` | 任务拆解、进度、依赖 |
| `../UnityDOTS官方文档参考/主题/` | DOTS 规范、模式、API 解读 |
| `../UnityDOTS官方文档参考/元信息/90-规则编号索引.md` | 规则族索引和编号速查 |
| `架构事实/` | 当前版本 Runtime/Definition/模块事实文档 |
| `合规审查/` | 代码 DOTS 合规缺陷清单 |
| `审查更新-2026-05-26.md` | 最近一次代码审查的变更摘要 |

## 验收指标

- [ ] 看板中每个 Active ISSUE 都有对应的 `.md` 文件且状态与看板一致
- [ ] 每个 ISSUE 的 `最近复核` 日期在 2 个迭代以内
- [ ] 新发现的系统性问题按照 `维护规范.md` 进入流程
- [ ] 合规审查缺陷数只减不增（新代码不引入同类违规）
