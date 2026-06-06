# 命名规范 Spec

## 目的

建立 EX-GAS 2.0 的职责命名规范，避免类名、文件名、任务名继续使用”Facade / Adapter / Helper / Manager / EventBus / Scenario”等模糊词承载多重职责。命名必须让 Agent 在领取任务时直接判断对象所属层级、GAS 概念归属、DOTS 承载机制和读写方向。

## 核心原则

1. **GAS 概念作前缀，DOTS 机制作后缀**。名称从两端标注：前缀告诉读者”这是什么 GAS 概念”，后缀告诉读者”用什么 DOTS 机制承载”。
2. 一个类型名只能对应一个边界职责；如果名称需要用 `And`、`Or`、`Manager`、`Helper` 才能解释，通常说明需要拆分。
3. 类型名必须能回答三件事：属于哪个 GAS 概念、用什么 DOTS 机制承载、所属哪一层。
4. Runtime Core 热路径命名：`[GAS前缀]` + `[职责描述]` + `[DOTS后缀]`。
5. 边界层命名优先表达方向：`Gateway` 写入 Core，`ReadModel` 读取 Core，`Bridge` 做跨边界执行，`Sink` 只写外部导出。

## GAS 概念前缀表

| GAS 概念 | 前缀 | 来源（UE GAS / tranek） | 覆盖范围 |
|---|---|---|---|
| **AbilitySystemComponent** | `ASC` | `UAbilitySystemComponent` | ASC 实体身份标记、ASC 上的状态容器 |
| **GameplayEffect** | `GE` | `UGameplayEffect` / `FGameplayEffectSpec` / `FActiveGameplayEffect` | Effect Command、Spec、Modifier、Active Effect 实例 |
| **Ability** | `Ability` | `UGameplayAbility` | Ability 运行时状态、激活请求、目标获取 |
| **Attribute** | `Attribute` | `UAttributeSet` / `FGameplayAttributeData` | 属性值 component、属性修改器 buffer |
| **GameplayTag** | `Tag` | `FGameplayTag` / `FGameplayTagContainer` | Tag mask、Tag requirement、Tag change event |
| **TargetData** | `Target` | `FGameplayAbilityTargetData` | 目标获取参数、目标结果数据 |
| **GameplayCue** | `Cue` | `FGameplayCueTag` / `UGameplayCueManager` | Cue 触发标记、Cue 参数 |
| **GameplayEvent** | `GameplayEvent` | `FGameplayEventData` | Core 内部事件（驱动 Ability trigger 和 GE reaction） |
| **EffectContext** | `GEContext` | `FGameplayEffectContext` | GE 施加上下文（Source/Instigator/AbilityRef/HitResult） |
| **Modifier/Magnitude** | `Modifier` | `FGameplayModifierInfo` / `FGameplayEffectModifierMagnitude` | 已解析的 modifier 结果、magnitude 计算 |
| **跨概念基础设施** | `GAS` | — | Frame Prepare、Effect Fan-In、Structural Commit、Boundary Projection 等 Runtime Core kernel |

## DOTS 类型后缀表

| DOTS 机制 | 后缀 | 说明 |
|---|---|---|
| `IComponentData` | `Component` | unmanaged 数据组件：`ASCIdentityComponent`、`CombatAttributeCurrentSetComponent` |
| `IBufferElementData` | `Buffer` | 动态数组元素：`GEEffectSpecBuffer`、`AttributeModifierBuffer` |
| `IEnableableComponent` | `Tag` | 高频、不可预测、需要 query 过滤的开关：`PeriodDueTag`、`AbilityExecutableTag` 均仅在 profiler 证明 chunk/entity skip 收益时可选 |
| `IChunkComponent` | `ChunkComponent` | chunk 级共享数据：`AllIdleChunkComponent`、`NoActiveEffectsChunkComponent` |
| `ISystem` | `System` | 系统实现：`GASEffectFanInSystem`、`GASAttributeSetReduceApplySystem` |
| `ComponentSystemGroup` | `SystemGroup` | 物理执行域：`GASCommandResolveSystemGroup`、`GASCoreSimulationSystemGroup` |
| `BlobAsset` / static data def | `DefinitionBlob` | 不可变定义数据：`GameplayEffectDefinitionBlob`、`AbilityDefinitionBlob`、`GASDefinitionCatalogBlob` |
| `EntityCommandBuffer` singleton | `ECBSystem` | 自定义 ECB 播放系统：`BeginGASStructuralCommitECBSystem`、`EndGASStructuralCommitECBSystem` |
| Singleton Entity | `Singleton` | 全局唯一实体：`FrameArenaSingleton` |
| Static lookup / table | `Lookup` | 静态查找表：`GameplayEffectDefinitionLookup`、`AbilityDefinitionLookup`、`GASGeneratedDefinitionLookup` |
| Request Entity | `Request` | 边界请求实体上的 component：`AbilityActivationRequestComponent`、`AbilityCommandComponent`、`GEApplyRequest` |
| Frame-local command record | `Record` | NativeContainer 中的帧内记录：`AbilityActivationCommandRecord`、`AbilityTargetRecord`、`GEEffectCommandRecord` |
| Presentation outbox | `Event`（表现用） | 表现层事件：`PresentationEvent`（区别于 Core 内部 `GameplayEvent`） |
| `IJobEntity` / `IJobChunk` | `Job` | Job 结构体：`BuildEffectSpecsJob`、`TickActiveEffectsJob` |

## GAS Runtime Core Layer 完整命名体系

### IComponentData（`[GAS前缀][职责]Component`）

| 目标命名 | 旧命名 | GAS 概念 | 挂载 Entity | 职责 |
|---|---|---|---|---|
| `ASCIdentityComponent` | `CAscOwner` | ASC | ASC Entity | 标记”此 Entity 是 GAS 权威中枢” |
| `CombatAttributeCurrentSetComponent` / `CombatAttributeBaseSetComponent`（属性集家族） | `CAttribute` / `HealthAttribute` 等 | Attribute | ASC Entity | 默认按热路径和变更频率生成 AttributeSet component family；不再默认每种属性一个 component type |
| `AttributeDirtyMaskComponent` | （新增） | Attribute | ASC Entity | 标记本帧哪些 AttributeCode 发生变化，用于 fact/projection 精确过滤 |
| `TagMaskComponent` | `CTagMask` | Tag | ASC Entity | 运行时 granted tag 的 dense bitmask |
| `ASCActiveEffectsComponent` | `CActiveEffectStore` | ASC | ASC Entity | active effect store 的版本标记和统计 |
| `AbilityStateComponent` | `CAbilityRuntimeState` | Ability | Ability Entity | ability code、当前状态、激活帧 |
| `AbilityActivationRequestComponent` | `AbilityCommandRequest` | Ability | Request Entity | Boundary 输入意图：source、ability、显式目标、input sequence、target mode |
| `AbilityCommandComponent` | （新增） | Ability | Request Entity | Ingest 后的归一化激活命令：primary GE、level、target params、status |
| `GEContextComponent` | （新增） | EffectContext | Request/Spec | GE 施加时的完整上下文（Source/Instigator/AbilityRef/OptionalObject） |
| `GASDefinitionCatalogComponent` | （新增） | Definition | DefinitionCatalogSingleton | 只读 Definition Catalog Blob 入口；world/bootstrap 后不可写 |
| `FrameArenaStateComponent` | `CFrameArenaState` | GAS | FrameArenaSingleton | frame index、allocator budget、lookup refresh counters；禁止承载 query registry |
| `FrameArenaOwnerComponent` | `CFrameArenaOwner` | GAS | FrameArenaSingleton | 标记此 Entity 是 Frame Prepare owner |
| `GEStreamOwnerComponent` | `CEffectCommandStreamOwner` | GE | `GEStreamOwnerSingleton`（迁移期） | proof-only stream 元数据（version/sequence）；scale-ready 不作为默认 owner |
| `EffectFanInScratchComponent` | （新增） | GAS | System-associated / Scratch Owner（可选） | fan-in scratch 的 owner 与预算标记；不得作为 query registry |

**Attribute 命名规则（PackageCache 原文交叉审查后更新）：**

1. 默认不生成 `HealthAttribute` / `ManaAttribute` 这类“一属性一 component type”。Entities 官方 `systems-data-granularity.md` 同时说明小 component 有利于精细 query，也警告过度 component 粒度会增加 query、archetype 和内部流程开销；GAS 热路径中大多数属性在同一 Apply / MMC / Fact lane 一起参与计算，因此默认按 AttributeSet family 生成。
2. 当前值与低频基准值必须拆分：`CombatAttributeCurrentSetComponent` / `CombatAttributeBaseSetComponent`、`ResourceAttributeCurrentSetComponent` / `ResourceAttributeBaseSetComponent`。这对应官方 `systems-data-granularity.md` 的 read-only / read-write 分离，避免写 Current 时让 Base 的 reactive consumer 被误触发。
3. AttributeSet family 的划分依据是真实 DOTS 查询共现和写入频率，而不是 OOP 属性类层级。示例：`CombatAttributeCurrentSetComponent`（Health/Shield/Attack/Defense/MagicPower）、`ResourceAttributeCurrentSetComponent`（Mana/Energy/Rage）。
4. 只有 profiler 或 query contract 证明某个属性长期稀有、查询隔离收益大于额外 type/system/handle 成本时，才允许生成独立 `[AttributeName]AttributeComponent`，并必须在 `13-EntityComponent物理布局Spec.md` 的 archetype 审计中记录理由。

### IBufferElementData（`[GAS前缀][职责]Buffer`）

| 目标命名 | 旧命名 | GAS 概念 | 帧内/跨帧 | 职责 |
|---|---|---|---|---|
| `GEEffectCommandBuffer` | `BEffectCommand` | GE | 帧内 | 本帧施加意图；目标态是 compact owner-local range 或迁移期 proof buffer，不代表全局总线 |
| `GESetByCallerValueBuffer` | `BEffectCommandSetByCallerValue` | GE | 帧内 | SetByCaller magnitude range；必须随 command/spec range 传递 |
| `GEEffectSpecBuffer` | `BInstantEffectSpec` | GE | 帧内 | 迁移期 instant spec buffer；目标态可由 `NativeStream` / NativeList record 承载 |
| `AttributeModifierBuffer` | `BAttributeDelta` | Attribute | 帧内 | 已解析的属性修改器结果；目标态按 target grouped range apply |
| `ActiveGameplayEffectBuffer` | `BActiveEffectSlot` | GE | **跨帧** | 目标 ASC 上的 active effect（等效 `FActiveGameplayEffect`） |
| `ActiveEffectMutationBuffer` | `BActiveEffectMutation` | GE | 帧内 | duration/stack/period 状态变更 |
| `GameplayEventBuffer` | `BTypedSimulationFact` | GameplayFact | 帧内 | Core fact / Boundary observation 的迁移期承载 |
| `PresentationEventBuffer` | `BPresentationEvent` | GAS | 帧内 | Boundary 表现层事件（UI/Cue/VFX/SFX 消费） |
| `TargetDataBuffer` | `BTargetDataResult` | Target | 帧内 | Request/Command Entity 上的目标解析结果列表 |

### NativeContainer Record（`[GAS概念][职责]Record`）

| 目标命名 | GAS 概念 | 载体 | 生命周期 | 职责 |
|---|---|---|---|---|
| `AbilityActivationCommandRecord` | Ability | `NativeStream` / `NativeList` | 帧内 | Core 内部高频 ability 激活命令；AI autocast、passive、period、reaction 默认写它，不创建 request entity |
| `AbilityTargetRecord` | Target | `NativeStream` / `NativeList` | 帧内 | Target Resolve 输出的扁平 target record；包含 `Sequence`、`TargetIndex`、`TargetSortKey` |
| `GEEffectCommandRecord` | GE | `NativeStream` / `NativeList` | 帧内 | Effect Fan-In 的统一 GE apply/period/passive/reaction command record |

### IEnableableComponent（`[GAS前缀][职责]Tag`）

| 目标命名 | 旧命名 | GAS 概念 | Toggle 频率 | 职责 |
|---|---|---|---|---|
| `AbilityExecutableTag`（可选） | `CAbilityActive` | Ability | 仅 profiler 证明大量不可执行 ability 需要 skip 时 | ability 可执行 query skip cache；grant/revoke 默认不使用 enableable |
| `PeriodDueTag`（可选） | `CPeriodDue` | GE | 仅 profiler 证明大量 idle 可跳过时 | period tick chunk/entity skip cache；默认用 slot flag |

> `AbilityActiveTag` / `AbilityActivatingTag` 不作为默认目标态命名保留。PackageCache `components-enableable-intro.md` 明确低频且持续多帧的状态更适合 Add/Remove Component 或普通状态字段；因此 ability grant/revoke、activating/cooldown/blocked 默认进入 `AbilityStateComponent.State/Flags` 与 Structural Commit 链路。只有 query skip 收益被 profiler 证明后，才生成 `AbilityExecutableTag` 这类可选 enableable。
>
> `TargetAcquisitionComponent` 不再作为挂在 Ability Entity 上的目标态命名保留。Boundary 低频单次激活的目标参数属于 `AbilityActivationRequestComponent` / `AbilityCommandComponent`，解析结果属于同一 request/command entity 的 `TargetDataBuffer`；Core 高频单次激活的目标参数和解析结果属于 `AbilityActivationCommandRecord` / `AbilityTargetRecord`；Ability Entity 只保留 granted ability 的跨帧 `AbilityStateComponent`。
>
> `Request Entity` 命名只用于 Boundary 低频外部意图。Core 内部高频命令不得命名为 `Request`，应使用 `Record`，避免把 frame-local fan-in 误设计成 entity create/destroy 链路。

### IChunkComponent

| 目标命名 | 旧命名 | 职责 |
|---|---|---|
| `AllIdleChunkComponent` | `ChunkAllIdle` | chunk 内所有 ASC 的所有 effect slot 都是 idle |
| `NoActiveEffectsChunkComponent` | `ChunkNoActiveEffects` | chunk 内所有 ASC 都没有 active effect |

### ISystem（`[GAS前缀][职责]System`）

| 目标命名 | 旧命名 | Physical Group / Lane |
|---|---|---|
| `GASFrameArenaSetupSystem` | `SFrameArenaSetup` | FramePrepare / FramePrepare |
| `AbilityCommandIngestSystem` | —（新增） | CommandResolve / BoundaryCommandIngest |
| `AbilityTargetResolveSystem` | —（新增） | CommandResolve / TargetResolve |
| `GASEffectFanInSystem` | —（新增） | CoreSimulation / EffectFanIn |
| `GEEffectSpecBuildSystem` | `SInstantEffectSpecBuild` | CoreSimulation / EffectFanIn（迁移期可独立 system，不独立 group） |
| `GASActiveEffectPreTickSystem`（或 `GASEffectFanInSystem` 内 producer job） | `SEffectTick`（period/expire seed 部分） | CoreSimulation / StateEvaluate.PreTick |
| `GASActiveEffectPostApplySystem` | `SEffectTick`（slot mutation 部分） | CoreSimulation / StateEvaluate.PostApply |
| `AbilityStateEvaluateSystem` | `SAbilityTick` | CoreSimulation / StateEvaluate |
| `ChunkComponentMaintainSystem` | `SChunkComponentMaintain` | CoreSimulation / StateEvaluate |
| `GASAttributeSetReduceApplySystem` | `SAttributeDeltaApply` / 旧 Health-only apply sample | CoreSimulation / AttributeReduceApply |
| `GameplayReactionSystem` | —（新增） | CoreSimulation / GameplayFact |
| `GameplayFactProjectionSystem` | `STypedSimulationFactProjection` / `GameplayEventProjectionSystem` | CoreSimulation / GameplayFact（迁移期命名收敛） |
| `GASAbilityDestroyCommitSystem` | —（新增） | StructuralCommit / StructuralCommit |
| `PresentationOutboxSystem` | `SPresentationOutboxProjection` | BoundaryProjection / BoundaryProjection |
| `ReplayLogSystem` | `SDebugReplayLogProjection` | BoundaryProjection / BoundaryProjection |
| `DiagnosticsSnapshotSystem` | `SDiagnosticsSnapshotExport` | BoundaryProjection / BoundaryProjection |

### ComponentSystemGroup（`GAS[PhysicalDomain]SystemGroup`）

SystemGroup 命名统一使用 `GAS` 大写前缀，但只表达 DOTS 物理执行域，不表达每个业务 kernel。`SYS-03` / `PRF-07` 要求避免把每个职责都拆成独立 group。

| 目标命名 | 旧命名 |
|---|---|
| `GASFramePrepareSystemGroup` | `GasRuntimeFramePrepareSystemGroup` |
| `GASCommandResolveSystemGroup` | `GasCommandIngestSystemGroup` / `GASTargetResolveSystemGroup`（合并） |
| `GASCoreSimulationSystemGroup` | `GASEffectFanInSystemGroup` / `GASStateEvaluateSystemGroup` / `GASAttributeReduceApplySystemGroup` / `GASGameplayFactSystemGroup`（合并为 lane systems） |
| `GASStructuralCommitSystemGroup` | `GasStructuralPlaybackSystemGroup` |
| `GASBoundaryProjectionSystemGroup` | `GasObservationProjectionSystemGroup` |

### BlobAsset Definition（`[GAS前缀]DefinitionBlob`）

| 目标命名 | 旧命名 | 职责 |
|---|---|---|
| `GASDefinitionCatalogBlob` | （新增） | Definition Catalog 根 Blob；聚合 Ability / GE / Tag / Attribute 等只读定义、排序 code、schema/content hash |
| `GameplayEffectDefinitionBlob` | `GameplayEffectDefinition` | GE 静态定义（Duration/Period/Stack/Modifier/Tag/Cue 配置） |
| `AbilityDefinitionBlob` | `AbilityDefinition` | Ability 静态定义（tag requirement/cooldown/cost/effect 引用） |
| `AttributeSetDefinitionBlob` | `AttributeSetDefinition` | 属性集定义（属性列表、初始值、Min/Max/曲线引用、生成到哪个 AttributeSet family） |

### Singleton Entity 命名

| 目标命名 | 旧命名 | 理由 |
|---|---|---|
| `FrameArenaSingleton` | `FrameArenaSingleton`（不变） | 全局唯一，跨帧持久 |
| `DefinitionCatalogSingleton` | （新增） | 每个 World / battle config set 一个，只持有 `GASDefinitionCatalogComponent`；不是 registry manager |
| `GEStreamOwnerSingleton` | `EffectCommandStreamOwner` | 全局唯一 GE 命令流 owner（proof-only；scale-ready 不新增依赖） |

## 层级命名约束（更新）

| 层级 | 推荐命名 | 禁止误用 |
|---|---|---|
| Application Shell Layer | `*Shell`、`*Presenter`、`*Controller`、`*Runner`、`*SceneInstaller`、`*ResourceBinding` | 不使用 `*System` 表示 OOP 业务对象，不暴露 `EntityManager` |
| Runtime Boundary Layer | `*CommandGateway`、`*ReadModel`、`*OutboxBridge`、`*DiagnosticsSink`、`*ReplaySink`、`*BoundarySystem` | 不用泛化 `*Adapter` 承载业务计算、缓存、日志和事件分发 |
| GAS Runtime Core Layer | 见上：`[GAS前缀][职责][DOTS后缀]` | 不使用 `Manager`、`Facade`、泛化 `Helper`、真实 UI/VFX/SFX 资源名；不使用旧 `C*`/`B*`/`S*` 单字母前缀 |
| Definition & Generation Layer | `*Definition`、`*Registry`、`*Lookup`、`*BakePlan`、`*ValidationReport`、`*GeneratedIds` | 不使用 `Runtime` 命名承载生成产物，不生成 `*LifecycleSystem` |

## Definition & Generation Layer 生成产物命名

Luban / SourceGenerator 生成的 Runtime-visible 类型也必须遵守职责命名，不使用 `Blob*Definition`、`Gas*`/`GAS*` 混用或无机制后缀的短名。

| 产物 | 命名格式 | 示例 | 说明 |
|---|---|---|---|
| Catalog Blob 根类型 | `GASDefinitionCatalogBlob` | `GASDefinitionCatalogBlob` | 聚合多 domain 静态定义；只保存 immutable definition，不保存 runtime state |
| Catalog Component | `GASDefinitionCatalogComponent` | `GASDefinitionCatalogComponent` | Runtime-visible singleton component，字段名用 `DefinitionCatalogBlob` |
| Blob 根类型 | `{Domain}DefinitionBlob` | `AbilityDefinitionBlob`、`GameplayEffectDefinitionBlob` | `Blob` 是 DOTS 承载机制，必须放在后缀 |
| Static lookup | `{Domain}DefinitionLookup` / `GASGeneratedDefinitionLookup` | `AbilityDefinitionLookup`、`GASGeneratedDefinitionLookup` | lookup 是 code -> index / BlobRef 查找表，不承载 runtime state；Blob 元素必须通过 index + `ref` 读取 |
| Runtime definition glue | `GASGeneratedRuntimeDefinitionResolver` | `GASGeneratedRuntimeDefinitionResolver` | generated static pure functions：Ability definition -> activation plan、GE definition -> command/modifier records；不是 service locator |
| Requirement evaluator | `GASGeneratedRequirementEvaluator` | `GASGeneratedRequirementEvaluator` | tag mask / attribute threshold / failure reason 的 generated static switch；不访问 entity |
| Magnitude evaluator | `GASGeneratedMagnitudeEvaluator` | `GASGeneratedMagnitudeEvaluator` | MMC / modifier magnitude static switch；默认不使用托管 delegate |
| Target rule table | `GASGeneratedTargetRuleTable` | `GASGeneratedTargetRuleTable` | target rule code -> unmanaged params / sort policy；不生成 strategy class |
| Runtime glue record | `{Domain}{Intent}Record` | `AbilityActivationPlanRecord`、`GECommandSeedRecord`、`ResolvedModifierRecord` | frame-local NativeContainer record，不是 `IComponentData`，不进入 EntityComponent 布局 |
| Generated index | `GASGeneratedDefinitionIndex` / `GASGeneratedDefinitionIndexEntry` | `GASGeneratedDefinitionIndexEntry` | GAS 框架级生成元数据，统一 `GASGenerated*` |
| Blob component | `GASGeneratedDefinitionBlobComponent<T>` | `GASGeneratedDefinitionBlobComponent<AbilityDefinitionBlob>` | Runtime-visible `IComponentData`，后缀必须是 `Component` |
| Code component | `GASDefinitionCodeComponent` | `GASDefinitionCodeComponent` | Runtime-visible `IComponentData`，只保存 stable code |
| Baker authoring | `{Domain}DefinitionBlobAuthoring` / `{Domain}DefinitionBlobBaker` | `AbilityDefinitionBlobBaker` | Baker 命名必须能看出输入 Blob 类型 |
| Blob builder | `GASGeneratedDefinitionBlobBuilder` | `BuildAbilityDefinitionBlob()` | Builder 只构建 Blob，不拥有生命周期 |
| Catalog builder | `GASGeneratedDefinitionCatalogBuilder` | `BuildDefinitionCatalogBlob()` | 聚合各 domain definition，生成排序 code、schema/content hash 和去重信息 |
| Lookup builder | `GASGeneratedDefinitionLookupBuilder` | `BuildAbilityDefinitionLookupFromRows()` | Editor/Baking 侧从 row 构建 lookup；不得使用 `BlobDefinitionLookupBuilder` 这类机制前置命名 |
| Component type set | `GASGeneratedDefinitionComponentTypeSets` | `AbilityDefinitionComponentTypes` | 只输出 `ComponentTypeSet` 常量，不隐藏结构变化 |
| Query layout | `GASGeneratedDefinitionQueryLayouts` | `AbilityDefinitionQuery` | 只描述 query shape，不生成 runtime lifecycle system |
| Validation report | `GASCodeGenValidationReport` 或文件 `GasCodeGenValidationReport.md` | `GasCodeGenValidationReport.md` | Editor/CI 诊断，不进入 Runtime Core |

禁止样例：

1. `BlobAbilityDefinition`：机制前缀压过 GAS 概念，违反“GAS 概念作前缀，DOTS 机制作后缀”。
2. `GeneratedDefinitionBlobComponent<T>`：缺少 `GAS` 框架归属前缀。
3. `GasGeneratedDefinitionIndex`：框架缩写大小写不一致，新生成类型统一使用 `GAS`。
4. `BlobDefinitionLookupBuilder`：机制前置且缺少 `GASGenerated` 框架归属；应改为 `GASGeneratedDefinitionLookupBuilder`。

## 旧 C/B/S 前缀的迁移

旧惯例 `C*` = IComponentData、`B*` = IBufferElementData、`S*` = ISystem 自本 Spec v2 起废弃。迁移规则：

- `C*` → `[GAS前缀][职责]Component`（IComponentData）或 `[GAS前缀][职责]Tag`（IEnableableComponent）
- `B*` → `[GAS前缀][职责]Buffer`（IBufferElementData）
- `S*` → `[GAS前缀][职责]System`（ISystem）
- 旧命名的 `C`/`B`/`S` 前缀不再出现在新代码中

已有代码中的旧命名在重构窗口内逐步迁移，Spec 文档即刻起使用新命名。

## 后缀职责字典

| 后缀 | 允许职责 | 禁止职责 | 示例 |
|---|---|---|---|
| `Facade` | 面向应用壳层的窄 API 聚合，只做语义便捷入口 | 不持有权威状态，不做 gameplay 计算，不混合 read/write/cache/log | `AbilitySystemFacade` 后续应收缩为壳层 API 或拆成 `GASCommandGateway` + `GASReadModel` |
| `Gateway` | 跨层写入入口，把外部意图转换为 command/request | 不读事实、不分发表现、不缓存业务状态 | `GASCommandGateway` |
| `ReadModel` | 面向外层的只读镜像 | 不写 ECS，不返回可写 buffer，不暴露 `EntityManager` | `GASUnitReadModel` |
| `Bridge` | 跨技术边界执行副作用，如 ECS facts 到 UI/Cue/VFX/SFX marker | 不计算 gameplay 结果 | `GameplayCueOutboxBridge` |
| `Sink` | 只写外部导出目标，如日志、Replay、诊断文件 | 不作为 Runtime Core 输入 | `RuntimeDiagnosticsSink` |
| `Projector` | 把 Core 数据投影为 fact、read model 或 outbox | 不拥有生命周期，不做 command 写入 | `AttributeDeltaProjector` |
| `Store` | Runtime Core 内稳定状态容器 | 不表示静态注册表，不访问 OOP | `ActiveEffectStore` |
| `Registry` | 静态定义注册和查找 | 不保存 runtime state | `GameplayEffectDefinitionRegistry` |
| `Resolver` | 纯计算解析，输入明确，输出明确；generated `GASGeneratedRuntimeDefinitionResolver` 只表示静态纯函数集合 | 不执行结构变化，不调用外部资源，不保存 runtime state，不作为 service locator | `ModifierMagnitudeResolver`、`GASGeneratedRuntimeDefinitionResolver` |
| `Builder` | 构建 command、spec、blob、report | 不拥有运行时生命周期 | `GameplayEffectSpecBuilder` |
| `Runner` | 测试或场景执行入口 | 不承载业务规则巨类 | `AutoChessHeadlessRunner` |
| `Scenario` | 场景数据或测试编排描述 | 不成为配置、逻辑、表现、报告混合容器 | `AutoChessScenarioDefinition` |

## ECS 特定命名约束

以下规则已统一到 `[GAS前缀][职责][DOTS后缀]` 体系（见上文「GAS Runtime Core Layer 完整命名体系」各表），此处仅保留机制层面的约束。

### SystemGroup 命名

| 规则 | 说明 |
|---|---|
| 后缀 `SystemGroup` | 所有 Unity `ComponentSystemGroup` 子类使用 `*SystemGroup` 后缀 |
| 前缀 `GAS` | Runtime Core 的 SystemGroup 统一使用大写 `GAS` 前缀 |
| 物理域命名 | 子 SystemGroup 只表达物理执行域：FramePrepare、CommandResolve、CoreSimulation、StructuralCommit、BoundaryProjection |

示例：`GASFramePrepareSystemGroup`、`GASCommandResolveSystemGroup`、`GASCoreSimulationSystemGroup`、`GASStructuralCommitSystemGroup`。

### ISystem 命名

| 规则 | 说明 |
|---|---|
| 格式 `[GAS前缀][职责]System` | 所有 `ISystem` 实现使用 GAS 概念前缀 + 职责描述 + `System` 后缀 |
| 语义完整名 | 使用完整语义名，不使用缩写：`GEEffectCommandIngestSystem`，非 `GECIS` |
| 归属声明 | System 名表达业务职责；物理执行域和 kernel lane 通过 `[UpdateInGroup]`、`[UpdateBefore]` / `[UpdateAfter]` 与文档表声明，不再把每个 lane 升格为 SystemGroup |

示例：`AbilityCommandIngestSystem`、`GASEffectFanInSystem`、`GASAttributeSetReduceApplySystem`、`GameplayFactProjectionSystem`。

### Job 结构体命名

| 规则 | 说明 |
|---|---|
| 后缀 `Job` | 所有 `IJobEntity` / `IJobChunk` / `IJobParallelFor` 结构体使用 `*Job` 后缀 |
| 语义前缀 | Job 名应表达输入→输出的转换：`BuildInstantSpecsJob`、`TickActiveSlotsJob` |
| Burst 标注 | Burst 编译的 Job 名不额外加前缀；是否 Burst 由 `[BurstCompile]` 属性表达 |

### BlobAsset 命名

| 规则 | 说明 |
|---|---|
| 引用字段 | `BlobAssetReference<T>` 字段名以 `Blob` 结尾：`DefinitionCatalogBlob`、`AbilityDefinitionBlob`、`EffectConfigBlob` |
| Blob 类型 | Blob 根类型以 `*Blob` 或 `*DefinitionBlob` 结尾 |
| 不要 BlobData | 不使用泛化 `BlobData` 后缀，必须表达数据内容 |
| 返回形态 | lookup 不返回含 `BlobArray` / `BlobString` / `BlobPtr` 的定义值副本；命名为 `TryGet*Index` + `Get*` 表达 index + `ref readonly` 访问 |

### Singleton Entity 命名

| 规则 | 说明 |
|---|---|
| 后缀 `Singleton` | 全局唯一的 entity 使用 `*Singleton` 后缀：`FrameArenaSingleton` |
| 后缀 `Owner` | 仅迁移期 proof-only 的 frame-local stream singleton 使用 `*Owner` 后缀；新 scale-ready 设计优先 owner system + NativeContainer |
| Definition Catalog | `DefinitionCatalogSingleton` 只持有只读 `GASDefinitionCatalogComponent`，不是 service locator；Runtime system 通过 `RequireForUpdate` 和只读 `GetSingleton` 获取 BlobRef |
| 区分标准 | `Singleton` = 跨帧持久且全局唯一；`Owner` = 持有可变 buffer 的帧级单例，不得升级为 manager/service locator |

## 限制词

### `Manager`

默认禁止。只有管理 Unity 资源生命周期、Editor window 生命周期或进程级服务生命周期时才允许使用。Runtime Core 内不得出现 `Manager` 作为业务权威对象。

当前 `GASManager` 更接近 `GASRuntimeWorldHost` / `GASRuntimeBootstrap` / `GASRuntimeServiceLocator` 的混合职责；后续需要按职责拆分，而不是继续扩大 `Manager`。

### `Helper`

默认禁止作为新增 Runtime 类型后缀。允许保留纯函数、小范围、无状态、无结构变化、无外部副作用的工具，但名称应尽量替换成具体职责。

示例重命名方向：

| 当前命名 | 问题 | 目标命名方向 |
|---|---|---|
| `EventBusHelper` | 同时表达 metadata、sequence、append、batch capability，`Helper` 掩盖写入方向 | `GameplayFactStreamWriter` / `GameplayFactAppendService` |
| `AttributeHelper` | 包含属性查找、重算、modifier 应用，容易进入 Core 热路径 | `AttributeValueResolver` + `AttributeDeltaProjector` |
| `CueHelper` | 真实职责是 Cue 类型注册、实例创建和 presentation bridge 执行细节 | `GameplayCueFactory` + `GameplayCueOutboxBridge` |
| `EntityHelper` | 当前主要是 GameObject presentation binding 和调试取名 | `PresentationEntityBindingRegistry` |

### `Adapter`

只允许表示协议或数据形态转换。不得同时负责 command 写入、结果分发、read model、debug log 和业务计算。历史方案中的 `Thin Adapter` 在本路线中统一收敛为 `Runtime Boundary Layer`，具体类型优先使用 `Gateway / ReadModel / Bridge / Sink`。

### `EventBus`

不允许作为实时 simulation 业务中心。Core 内可以存在 fact stream / transient event buffer，但命名必须说明流向和用途。

推荐：

1. Core 输出：`GameplayFactStream`、`AttributeDeltaStream`、`PresentationMarkerOutbox`。
2. 边界读取：`GameplayFactReadCursor`、`PresentationOutboxBridge`。
3. 外部分发：`ApplicationEventDispatcher`，仅属于 Application Shell Layer。

### `Debugger` / `Logger`

只能观察、采样、导出和可视化。不得参与 gameplay routing，不得作为 Core 输入源。Runtime Core Debugger 的内部对象应优先命名为 `DiagnosticsCounter`、`TraceEvent`、`TimingSample`、`BufferPressureSample`、`ReplayFrame`。

### AutoChessDemo / Debugger 分层命名

1. `Assets/AutoChessDemo` 属于 **Layer 1 Application Shell Layer / 业务验收层**，命名空间必须使用 `GAS.AutoChessDemo`，不能使用 `GAS.Runtime`。
2. AutoChessDemo 可以出现 `Runner`、`Scenario`、`CommandDriveSystem`、`ValidationSummary` 等业务验收命名，但不能声明 Runtime Core 权威状态或私有 Debugger 模块。
3. Runtime Debugger 的采样、snapshot、official tool diff、replay export 属于 **Layer 2 Runtime Boundary Layer**，类型优先使用 `DiagnosticsSink`、`RuntimeDiagnosticsSnapshot`、`OfficialToolDiff`、`ReplaySink`、`TraceEvent`。
4. Editor Debugger Window 属于 **Layer 1 Editor Extension**，命名应带 `EditorWindow` / `EditorView` / `EditorPresenter` 等后缀，并只读消费 Layer 2 snapshot / export API。
5. 无头 runner 属于 Layer 1 业务验收入口，允许输出 Debugger summary、耗时、数据流图和时序图；它不是 Debugger 数据源。

## 当前命名问题样本

1. `AbilitySystemFacade` 位于 `Assets/GAS/Runtime/AbilitySystem/AbilitySystemFacade.cs:7-27`，同时暴露 Entity、GameObject 和 Observation，后续应拆成应用壳层 API、CommandGateway 和 ReadModel。
2. `EventBusHelper` 位于 `Assets/GAS/Runtime/Event/EventBusHelper.cs:7-45`，实际职责是 fact append / sequence / batch capability，不应继续以 Helper 掩盖边界写入语义。
3. `GASManager` 位于 `Assets/GAS/Runtime/General/GASManager.cs:6-36`，承担 world host、entity manager、global singleton、bootstrap 多种职责，命名过宽。
4. 旧 `HeadlessAutoChessScenario` 已从 `Assets/GAS/Runtime/Demo/AutoChess` 迁出；当前 `Assets/AutoChessDemo` 类型必须保持 `GAS.AutoChessDemo` 命名空间，并按真实业务 owner 拆分为 `AutoChessGameRoomDefinition`、`AutoChessBattleManager`、`AutoChessBattleSession`、`AutoChessRuntimeRunner`、`AutoChessRuntimeSystemBootstrap`、`AutoChessBattleCommandDriveSystem`、`AutoChessBattleDefinitionCatalogBuilder`、`AutoChessDemoSceneRunner` 等可定位职责。禁止把新链路重新命名为 `AutoBattle*` 或 `HeadlessAutoChess*`。
5. 历史方案中 `ShadowMark` “实为 Debuff 却放 Buff 命名空间”是配置命名与职责不符的典型反例：`../历史方案参考/方案14.md:1251-1258`。

## 任务命名规范

任务名采用“主线名 - 支线名 - 任务名”的中文职责拼接，不使用抽象代号作为主名称。代号只允许作为附加追踪字段。

示例：

| 旧风格 | 新风格 |
|---|---|
| `T6-REAL-B-DemoMigration` | `Runtime验收Demo - AutoChessDemo架构迁移 - Runtime目录迁出` |
| `T4-Debugger-AM1` | `Observation与Debugger - RuntimeCore诊断基线 - SystemTiming与BufferPressure采样` |
| `T1-RuntimeCore-AM0` | `GAS Runtime Core - 结构变化边界 - 禁止跨结构变化持有DynamicBuffer` |

## 验收方式

1. 新增类型必须能从名称判断所属四层和读写方向。
2. 新增 Runtime Core 类型不得使用 `Manager`、`Facade`、泛化 `Helper`、真实 UI/VFX/SFX 资源名。
3. 新增边界层类型不得用 `Adapter` 作为万能名，必须在 `Gateway / ReadModel / Bridge / Sink / Projector` 中选择或补充等价精确后缀。
4. 任务树主线、支线、任务节点都必须带目标、Spec 引用、历史方案定位、验收标准和测试链路。

## 历史方案定位

1. 方案15 中 `UnitFacade / GASAdapter / GASEventBus / GASDebugger` 的示例提供了四层原始雏形，但本 Spec 将其拆成更精确的 Gateway、ReadModel、Bridge、Sink：`../历史方案参考/方案15.md:53-88`。
2. 方案15 对适配层“翻译而非计算”的描述是 Runtime Boundary 命名约束的来源：`../历史方案参考/方案15.md:473-490`。
3. 方案15 对 `AbilityLogicBase` 与 `AbilityBehaviorSystem` 的对比说明命名应反映 Burst ECS 行为归属：`../历史方案参考/方案15.md:1302-1315`。
4. 方案14 的职责最终定义强调 Core 禁止 OOP 反查，支持本 Spec 对 Core 命名和边界命名的约束：`../历史方案参考/方案14.md:1038-1058`。
