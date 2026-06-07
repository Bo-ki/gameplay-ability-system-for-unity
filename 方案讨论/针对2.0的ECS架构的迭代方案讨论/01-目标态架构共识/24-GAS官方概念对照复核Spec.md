# GAS 官方概念对照复核 Spec

## 目的

本 Spec 用 Epic Unreal Engine 5.7 官方 Gameplay Ability System 文档复核近期更新的配置链与新增能力文档：

1. `19-GAS业务编辑路径与配置链职责Spec.md`
2. `20-策划配置能力交叉审查Spec.md`
3. `22-新增能力业务推进流程Spec.md`
4. `23-能力配置链条与分析步骤Spec.md`

复核目标不是把 UE 的 OOP 类层级搬进 Unity，而是确认目标态短编辑路径、Luban / SourceGenerator 生成链和 pure ECS Runtime Core 没有丢失 GAS 的核心业务语义。

本文件只记录目标态概念映射、偏差修正、禁止方向和验收口径；不讨论 Editor UI 控件实现，不记录当前代码完成度。

## 复核结论

近期更新的四份文档方向成立，但必须补上 **GAS Official Concept Mapping** 作为配置链 guardrail。

当前设计做对的部分：

1. `Ability Package / Effect Package` 被定义为 Authoring 聚合，不进入 Runtime Core。
2. Luban rows 仍是长期权威，Editor draft 只能投影为 row diff 和发布快照。
3. SourceGenerator 只生成 Blob / lookup / pure glue / validation / Editor Binding，不生成 Runtime lifecycle。
4. Runtime Trace Preview、Impact Analysis、Scenario Validation Binding 被前置到保存 / 发布前。
5. Cue / UI / VFX / SFX 被限定为 Boundary / Presentation 派生，不反向决定 gameplay。

仍需补强的概念边界：

1. Ability 不能只被理解为“模板 + rows”，必须保留 grant、activation、can activate、commit、cancel、end、block / cancel tags、failure reason 的 lifecycle contract。
2. GameplayEffect 必须区分 immutable definition 与 runtime GameplayEffectSpec shape；instant、duration、periodic、stacking、execution、immunity、requirement、granted state 和 cue 触发不能被压平成同一种 row。
3. GameplayTag 不是普通字符串引用，而是激活、阻断、取消、应用、持续、移除、免疫、Cue 路由和事件触发的共同条件语言。
4. AbilityTask 的官方语义是 Ability 执行期间的异步 / 等待 / 目标收集 / 事件流控制；DOTS 目标态不能保留 OOP AbilityTask，但必须显式映射为 ECS target / async / fact / boundary lane。
5. ASC 是 GAS owner / aggregator。Unit 默认能力授予、AttributeSet、Owned Tags、Active GE slot 与 runtime ASC state 的关系必须在配置链中显式建模。

## 官方 GAS 概念基线

| 官方概念 | 官方语义基线 | Unity DOTS 目标映射 | 禁止误译 |
|---|---|---|---|
| Ability System Component | Actor 与 GAS 的桥接和聚合点，Actor 需要自己的 ASC 或访问 Pawn / PlayerState 上的 ASC | ASC Entity + AttributeSet family + tag mask + ability grant slots + active effect store | 把 ASC 包成 Runtime OOP manager，或把 Unit 配置 row 当 runtime ASC state |
| Gameplay Ability | 主动或被动能力；通过 CanActivate / Activate / Commit / Cancel / End 形成生命周期；cost / cooldown 通常在 Commit 阶段生效；tags 可取消、阻断或限制激活 | Ability definition blob + granted ability state + frame-local activation plan record + generated requirement / commit glue | 只生成 Ability row，却不表达 activation / commit / cancel / end 语义 |
| Ability Task | Ability 执行期间的异步任务，可等待事件、目标、动画、延迟等，并影响 Ability 执行流；任务应随 Ability 结束而终止 | ECS target resolve lane、cross-frame async state slot、Boundary command / fact record、bounded reaction pass | 在 Runtime Core 引入 OOP AbilityTask、delegate、协程或托管状态机 |
| GameplayEffect Definition | 配置资产 / definition，描述 duration policy、modifier、execution、tag requirement、cue、granted tag / ability、stacking、immunity 等 | Luban row + normalized definition + `GASDefinitionCatalogBlob` range | Runtime hot path 读取 managed row / JSON，或把每个 GE definition 实体化为 runtime object |
| GameplayEffect Spec | GameplayEffect 的 runtime instance wrapper，携带 source / target context、等级、动态参数、捕获值、duration / period / stack 等运行期上下文 | `GECommandSeedRecord` / `ResolvedModifierRecord` / active effect slot seed / context record | 把 GE Spec 与 GE definition 混同，或把所有 instant GE 都创建 runtime entity |
| Attribute / AttributeSet | Attribute 保存 current / base value；AttributeSet 管理 Attribute 与系统交互、clamp、计算和变化反应，并注册到 ASC | generated AttributeSet family，Current 高频读写与 Base / Config 低频数据拆分，dirty mask / fact 派生 | 一属性一 component / 一属性一 system 作为默认方案，或用表现事件替代 attribute fact |
| GameplayTag | 层级标签、Tag Container、Tag Query；用于对象状态、事件、能力交互、阻断、取消和条件判断 | generated dense tag id、tag mask、tag query evaluator、tag taxonomy metadata | 只当字符串 / int ID 引用，缺少 taxonomy、query 和条件语义 |
| GameplayCue | 由 Ability / GE 触发表现反馈；通常通过 GameplayCue tag 路由，表达 OnActive / WhileActive / Removed / Executed 等事件 | Boundary cue outbox + `CueParameterContract` + headless marker | Cue 写 gameplay state，或无头验收删除 Cue 链路 |

## 当前文档对照矩阵

| 文档 | 已对齐部分 | 概念风险 | 修正要求 |
|---|---|---|---|
| `19-GAS业务编辑路径与配置链职责Spec.md` | 将默认编辑对象改为业务能力包；Ability / GE / Tag / Cue / Attribute 被聚合投影；Runtime Core 只消费 catalog | `Ability Package` 容易被误解为官方 Ability 的替代概念，导致 lifecycle、GE Spec、Cue Parameters 缺失 | 在业务包 projection 中强制输出 `AbilityLifecycleContract`、`GameplayEffectSpecShape`、`CueParameterContract` 和 `ASCBindingContract` |
| `20-策划配置能力交叉审查Spec.md` | 引用图、影响分析、发布门禁、表现绑定、SourceGenerator Editor Binding 的方向正确 | 引用图如果只表达 Ability -> GE -> Cue，会漏掉 official tag taxonomy、ASC owner、granted ability、immunity 和 GE requirement 边 | Config Reference Graph 必须包含 tag taxonomy edge、ASC grant edge、GE spec context edge、cue parameter edge 和 ability lifecycle edge |
| `22-新增能力业务推进流程Spec.md` | 三档分类能正确区分配置、胶水扩展和 Runtime 语义扩展；程序介入门槛合理 | 新异步等待 / 目标收集 / 事件驱动能力可能被错误归入 Configuration-Only，只因为 row 能保存字段 | 任何 AbilityTask-like 语义必须先判定是否已有 ECS async / target / fact lane；没有承载时升级为 Runtime Semantic Extension |
| `23-能力配置链条与分析步骤Spec.md` | 12 步分析协议覆盖业务意图、激活、目标、效果、数值、Requirement、持续、表现、投影、trace、影响和发布 | 分析步骤还缺官方 GE Spec shape、Ability lifecycle closure、Tag Query taxonomy、Cue parameter payload 和 ASC binding 的显式产物 | `Ability Configuration Analysis Report` 必须新增官方概念覆盖字段，不能只输出 row projection 与 trace shape |

## 必须新增的目标契约

### AbilityLifecycleContract

`AbilityLifecycleContract` 是业务包投影到 Ability definition 时必须输出的机器可读契约。

| 字段 | 目的 |
|---|---|
| `GrantedBy` | 能力由 Unit 默认配置、GE grant、装备、事件还是外部 shell 授予 |
| `ActivationPolicy` | 主动、被动、事件触发、on granted、periodic reaction 等 |
| `CanActivateRequirements` | activation required / blocked tags、attribute threshold、resource availability |
| `CommitPlan` | cost GE、cooldown GE、commit timing、失败回滚策略 |
| `CancelPolicy` | cancel tags、external cancel、self cancel、uncancelable window |
| `BlockPolicy` | block abilities with tag、activation owned tags、同类互斥 |
| `EndPolicy` | instant end、task completion、duration end、manual end、cancel cleanup |
| `FailureReason` | generated diagnostics code，供 Editor / CI / Debugger 统一解释 |
| `TraceShape` | activation plan 到 command / target / GE seed / fact / cue 的预期链路 |

设计理由：官方 Ability 的业务价值不在“有一行 Ability 数据”，而在生命周期完整闭合。短路径编辑必须隐藏表行复杂度，但不能删除生命周期语义。

### GameplayEffectSpecShape

`GameplayEffectSpecShape` 是 GE definition 进入 Runtime record 前必须保持的 spec 形状。

| 字段 | 目的 |
|---|---|
| `DefinitionIndex` | 指向 immutable GE definition / Blob range |
| `SourceASC / TargetASC` | 记录施加者、目标和上下文，不用裸 Entity 暴露给外部业务 |
| `Level / ScaleProfile` | 支持等级、曲线和场景规模输入 |
| `ContextPayload` | 命中、来源、target data、事件、随机种子等 effect context |
| `MagnitudeInputs` | modifier、execution、动态参数、公式 token 和 captured attribute refs |
| `DurationPolicy` | instant、infinite、has duration |
| `PeriodPolicy` | tick 周期、首 tick 策略、period cue |
| `StackPolicy` | stack count、refresh、independent timer、overflow policy |
| `RequirementPolicy` | application、ongoing、remove、immunity、custom can apply |
| `GrantedStatePolicy` | granted tags、granted abilities、cleanup / revoke policy |
| `CuePolicy` | on apply / active / tick / remove / executed 的 cue 参数和路由 |

设计理由：官方文档明确 GE definition 与 GameplayEffectSpec 不同。Unity 目标态可以不用 UE 的 UObject / FGameplayEffectSpec 类型，但不能把 runtime spec 上下文丢掉。

### GameplayTagTaxonomy

`GameplayTagTaxonomy` 是 Tag 配置链必须输出的分类表。

| Tag 类别 | 用途 | Runtime 承载 |
|---|---|---|
| `OwnedTag` | ASC 当前拥有的 gameplay 状态 | tag mask / status flags |
| `AssetTag` | Ability / GE definition 自身分类 | definition metadata |
| `ActivationRequiredTag` | Ability 激活前置条件 | generated requirement evaluator |
| `ActivationBlockedTag` | Ability 激活阻断条件 | generated requirement evaluator |
| `CancelAbilityTag` | 新能力激活时取消既有能力 | state evaluate / structural commit plan |
| `BlockAbilityTag` | 新能力激活时阻止其他能力 | granted ability state / tag mask |
| `ApplicationRequirementTag` | GE 应用前置条件 | GE application evaluator |
| `OngoingRequirementTag` | Duration GE 持续条件 | active effect store evaluator |
| `RemovalTag` | GE 移除条件 | active effect cleanup policy |
| `ImmunityTag` | 阻断 GE spec 应用 | immunity evaluator |
| `CueTag` | GameplayCue 路由 | Boundary cue outbox |
| `GameplayEventTag` | 事件触发 / reaction | typed fact / event lane |

设计理由：官方 GameplayTag 是层级标签、Tag Container 和 Tag Query 的条件语言。目标态可以把它压缩成 dense id / bitset / generated evaluator，但不能退化成裸 int 引用。

### CueParameterContract

`CueParameterContract` 是 Boundary Projection 必须承载的表现参数。

| 字段 | 目的 |
|---|---|
| `CueTag` | 对齐 GameplayCue 路由，通常属于 `GameplayCue.*` 命名域 |
| `CueEvent` | OnActive、WhileActive、Removed、Executed、Tick 或项目等价事件 |
| `SourceHandle / TargetHandle` | Boundary 可见的 source / target opaque handle |
| `EffectContext` | 命中点、方向、来源、target data、触发原因 |
| `MagnitudeSnapshot` | 表现需要的伤害、治疗、stack、period tick、crit 等只读数值 |
| `PresentationBindingKey` | VFX / SFX / UI / floating text / headless marker 绑定 |
| `TraceId` | 与 Runtime Trace Preview / Debugger evidence 关联 |

设计理由：Cue 是表现反馈，不是 gameplay state。但表现链路是业务体验的一部分，无头验收只能替换最终 side effect，不能删除参数和路由。

### AbilityTaskSemanticMapping

目标态不保留 UE `UAbilityTask` OOP 类型，但必须保留其异步语义。

| 官方 AbilityTask 类语义 | DOTS 目标映射 | 升级条件 |
|---|---|---|
| wait delay / wait event | cross-frame async state slot + Gameplay Fact cursor | 无现有 slot 或 fact lane 时升级为 Runtime Semantic Extension |
| wait target data | Target Resolve lane + request-owned target result buffer / NativeStream | 目标链、跳转链或高规模 fan-out 无法表达时升级 |
| play montage and wait | Boundary command + completion fact，不写 Core gameplay state | 表现完成决定 gameplay 时必须拆成 explicit gameplay fact |
| wait gameplay tag / attribute change | generated tag / attribute watch fact + bounded reaction policy | 高频监听导致 query / lookup 风险时走 DOTS gate |
| custom task | 先拆成 target、fact、duration、boundary、structural mutation 五类 | 任何需要新 lane / store / phase 的任务都不是配置型能力 |

设计理由：官方 AbilityTask 解决的是 Ability 跨帧执行和异步事件问题。DOTS 目标态要把该语义拆进 ECS 数据流，而不是用 OOP task 做 Runtime 中间层。

### ASCBindingContract

`ASCBindingContract` 连接 Unit / Actor 业务配置和 Runtime ASC state。

| 字段 | 目的 |
|---|---|
| `AscOwnerKey` | Unit / battle unit / pawn 的稳定业务 key |
| `AttributeSetFamily` | 该 ASC 拥有哪些 generated AttributeSet family |
| `InitialOwnedTags` | 初始 tag mask / status flags |
| `GrantedAbilityRefs` | 默认授予能力、GE grant 能力、装备授予能力 |
| `InitialActiveEffects` | 开局已有 duration / passive / aura effect |
| `GrantSource` | Unit row、equipment row、GE row、scenario setup 或 external command |
| `RuntimeInstallPolicy` | bootstrap 安装、Structural Commit 授予、cleanup / revoke 策略 |

设计理由：官方 ASC 是 GAS 的 owner / aggregator。Unity 目标态中 ASC 是 Entity，但配置链仍必须说明能力、属性、tag、active effect 如何绑定到 ASC，而不是让 Demo 常量或手写 bootstrap 代码隐式决定。

## 对配置链的修正要求

`Ability Configuration Analysis Report` 在现有 12 步基础上，必须增加官方概念覆盖字段：

| 字段 | 来源步骤 | 发布门禁 |
|---|---|---|
| `AbilityLifecycleContract` | 激活语义分析、Requirement 分析、Runtime Trace 分析 | 缺失时不得发布 Ability Package |
| `GameplayEffectSpecShape` | 效果拆解、数值公式、Duration / Period / Stack 分析 | GE definition 与 runtime spec 无法区分时不得发布 |
| `GameplayTagTaxonomy` | Requirement / Immunity 分析 | Tag 仅为裸字符串 / int 引用时不得发布 |
| `CueParameterContract` | 表现绑定分析 | Cue 缺 source / target / event / payload 时阻断完整业务发布 |
| `AbilityTaskSemanticMapping` | 激活、目标、Duration、表现绑定、trace 分析 | 发现 wait / async / target data 语义但无 ECS 承载时升级分类 |
| `ASCBindingContract` | Row Projection、Scenario Binding、Impact Analysis | Unit / Scenario / GE grant 无法反查 ASC 影响面时不得发布 |

## 对新增能力三档分类的修正

| 新能力情况 | 原分类倾向 | 官方 GAS 复核后的门槛 |
|---|---|---|
| 只改伤害、冷却、消耗、duration、period、stack、cue 绑定 | Configuration-Only Ability | 必须已有 lifecycle、GE spec、tag taxonomy、cue parameter、ASC binding 的模板覆盖 |
| 新增公式 token、target rule、requirement evaluator、Tag Query evaluator | Generated Glue Extension | 只能生成 pure glue / Editor Binding / validation，不得生成 lifecycle 或 Runtime System |
| 新增 wait target data、wait event、跨帧任务、链式目标、持续引导、表现完成驱动 gameplay | Runtime Semantic Extension 候选 | 若现有 ECS async / target / fact lane 已能表达，可降为 Generated Glue；否则必须走 DOTS Official Review Gate |
| 新增网络预测、客户端回滚、UE 风格 replicated AbilityTask | 非当前目标或后续扩展 | 当前目标态必须显式标注不支持，不能用 OOP 兼容层偷渡 |

## DOTS 规范复核

| GAS 官方语义 | DOTS 规则约束 | 目标态落点 |
|---|---|---|
| Ability lifecycle | `SYS-01`、`SYS-02`、`SYS-03`、`SEL-01` | 生命周期由 Runtime Core System / Job 数据流拥有，definition / trace / validation 只提供输入和证据 |
| GE Spec runtime payload | `BLOB-01`、`BLOB-02`、`NAT-03`、`BUF-01`、`STORE-03` | definition 进 Blob，spec / command / modifier 为 frame-local record 或 active slot seed |
| Instant GE executed but not active | `PRF-01`、`SC-01`、`CASE-12` | instant 默认走 Effect Fan-In / Attribute Reduce，不创建 runtime GE entity |
| Duration / Period / Stack | `BUF-01`~`BUF-04`、`FSM-02`、`FSM-05`、`PRF-03` | owner-local active effect slot、enum / bit field、capacity / overflow evidence |
| Tag conditions and queries | `PRF-03`、`FSM-05`、`BUR-01` | dense tag mask、generated evaluator、tag query metadata |
| AbilityTask async flow | `SYS-02`、`SC-01`、`ECB-03`、`NAT-01`、`SEL-04` | explicit lane / store / phase / allocator owner，不生成 OOP task |
| GameplayCue cosmetic route | `SYS-05`、`CONTENT-01`、`CONTENT-02`、`DBG-01` | Boundary outbox、Presentation binding、headless marker，不参与 Core 决策 |

## 非目标明确化

1. 不实现 UE UObject 版 `UAbilitySystemComponent`、`UGameplayAbility`、`UAbilityTask` 或 `UGameplayEffect` 的 OOP 继承树。
2. 不把 AbilityTask 做成 Runtime Core 托管 task / coroutine / delegate。
3. 不把 UE 网络 prediction / replication 作为当前目标态默认能力；需要时必须另开 Network / Prediction Spec，并通过 DOTS gate。
4. 不为兼容旧链路保留 OOP gameplay 中间层；旧链路只能作为迁移事实或 proof-only，不得进入目标态。
5. 不让 Business Package 替代官方 GAS 概念。Business Package 只是 Authoring 聚合，必须能投影出 Ability lifecycle、GE spec、Tag taxonomy、Cue parameters 和 ASC binding。

## 验收

1. `19/20/22/23` 中任何新增业务能力示例，都必须能映射到 `AbilityLifecycleContract`、`GameplayEffectSpecShape`、`GameplayTagTaxonomy`、`CueParameterContract` 和 `ASCBindingContract`。
2. 任何 AbilityTask-like 需求必须输出 `AbilityTaskSemanticMapping`，并说明是已有 ECS lane、Generated Glue Extension 还是 Runtime Semantic Extension。
3. Config Reference Graph 必须能表达 Ability lifecycle edge、GE spec context edge、tag taxonomy edge、cue parameter edge、ASC grant edge 和 scenario validation edge。
4. Runtime Trace Preview 必须区分 definition 输入、frame-local record、active effect slot、Core fact 和 Boundary cue；不得把它们合并成单一日志线。
5. SourceGenerator validation 必须拒绝 runtime-visible generated lifecycle、OOP task、hidden query、hidden ECB、managed delegate、managed config lookup。
6. DOTS Official Review Gate 必须把官方 GAS 概念缺口和 Unity DOTS API 选型一起记录；不能只说明“这是 GAS 语义”，也不能只说明“这是 ECS 性能优化”。

## 官方文档来源

1. Epic Developer Community / Unreal Engine 5.7 Documentation: [Gameplay Ability System](https://dev.epicgames.com/documentation/unreal-engine/gameplay-ability-system-for-unreal-engine?lang=en-US)
2. Epic Developer Community / Unreal Engine 5.7 Documentation: [Using Gameplay Abilities](https://dev.epicgames.com/documentation/unreal-engine/using-gameplay-abilities-in-unreal-engine?lang=en-US)
3. Epic Developer Community / Unreal Engine 5.7 Documentation: [Gameplay Ability Tasks](https://dev.epicgames.com/documentation/unreal-engine/gameplay-ability-tasks-in-unreal-engine?lang=en-US)
4. Epic Developer Community / Unreal Engine 5.7 Documentation: [Gameplay Effects for the Gameplay Ability System](https://dev.epicgames.com/documentation/unreal-engine/gameplay-effects-for-the-gameplay-ability-system-in-unreal-engine?lang=en-US)
5. Epic Developer Community / Unreal Engine 5.7 Documentation: [Gameplay Ability System Component and Gameplay Attributes](https://dev.epicgames.com/documentation/unreal-engine/gameplay-ability-system-component-and-gameplay-attributes-in-unreal-engine?lang=en-US)
6. Epic Developer Community / Unreal Engine 5.7 Documentation: [Gameplay Attributes and Attribute Sets](https://dev.epicgames.com/documentation/unreal-engine/gameplay-attributes-and-attribute-sets-for-the-gameplay-ability-system-in-unreal-engine?lang=en-US)
7. Epic Developer Community / Unreal Engine 5.7 Documentation: [Using Gameplay Tags](https://dev.epicgames.com/documentation/unreal-engine/using-gameplay-tags-in-unreal-engine?lang=en-US)

## 历史方案定位

1. 本文件复核 `19/20/22/23` 的配置链目标态，不替代这些文档。
2. DOTS 侧约束仍以 `18-DOTS官方规范复核与性能红线Spec.md`、`../../UnityDOTS官方文档参考/主题/90-规则编号索引.md` 和 `../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md` 为第一性技术依据。
