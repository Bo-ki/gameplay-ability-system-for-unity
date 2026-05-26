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
| **跨概念基础设施** | `GAS` | — | Frame Arena、Stream Owner、Structural ECB 等框架级设施 |

## DOTS 类型后缀表

| DOTS 机制 | 后缀 | 说明 |
|---|---|---|
| `IComponentData` | `Component` | unmanaged 数据组件：`ASCIdentityComponent`、`AttributeComponent` |
| `IBufferElementData` | `Buffer` | 动态数组元素：`GEEffectSpecBuffer`、`AttributeModifierBuffer` |
| `IEnableableComponent` | `Tag` | 高频状态开关（语义接近 UE 的 tag 查询）：`AbilityActiveTag`、`PeriodDueTag` |
| `IChunkComponent` | `ChunkComponent` | chunk 级共享数据：`AllIdleChunkComponent`、`NoActiveEffectsChunkComponent` |
| `ISystem` | `System` | 系统实现：`GEEffectCommandIngestSystem`、`AttributeModifierApplySystem` |
| `ComponentSystemGroup` | `SystemGroup` | 系统组：`GASCommandIngestSystemGroup` |
| `BlobAsset` / static data def | `Definition` | 不可变定义数据：`GameplayEffectDefinition`、`AbilityDefinition` |
| `EntityCommandBuffer` singleton | `ECBSystem` | 自定义 ECB 播放系统：`BeginGASStructuralECBSystem`、`EndGASStructuralECBSystem` |
| Singleton Entity | `Singleton` | 全局唯一实体：`FrameArenaSingleton` |
| Static lookup / table | `Registry` / `Lookup` | 静态查找表：`GEConfigRegistry`、`AbilityConfigRegistry` |
| Request Entity | `Request` | 边界请求实体上的 component：`AbilityCommandRequest`、`GEApplyRequest` |
| Presentation outbox | `Event`（表现用） | 表现层事件：`PresentationEvent`（区别于 Core 内部 `GameplayEvent`） |
| `IJobEntity` / `IJobChunk` | `Job` | Job 结构体：`BuildEffectSpecsJob`、`TickActiveEffectsJob` |

## GAS Runtime Core Layer 完整命名体系

### IComponentData（`[GAS前缀][职责]Component`）

| 目标命名 | 旧命名 | GAS 概念 | 挂载 Entity | 职责 |
|---|---|---|---|---|
| `ASCIdentityComponent` | `CAscOwner` | ASC | ASC Entity | 标记”此 Entity 是 GAS 权威中枢” |
| `AttributeComponent`（占位统称） | `CAttribute` | Attribute | ASC Entity | 每种属性一个独立 type（如 `HealthAttribute` 直接作为 struct 名，不再用统称） |
| `TagMaskComponent` | `CTagMask` | Tag | ASC Entity | 运行时 granted tag 的 dense bitmask |
| `ASCActiveEffectsComponent` | `CActiveEffectStore` | ASC | ASC Entity | active effect store 的版本标记和统计 |
| `AbilityStateComponent` | `CAbilityRuntimeState` | Ability | Ability Entity | ability code、当前状态、激活帧 |
| `TargetAcquisitionComponent` | `CTargetDataRequest` | Target | Ability Entity | 目标获取参数（mode/range/filter） |
| `GEContextComponent` | （新增） | EffectContext | Request/Spec | GE 施加时的完整上下文（Source/Instigator/AbilityRef/OptionalObject） |
| `FrameArenaStateComponent` | `CFrameArenaState` | GAS | FrameArenaSingleton | RewindableAllocator handle、frame index |
| `FrameArenaOwnerComponent` | `CFrameArenaOwner` | GAS | FrameArenaSingleton | 标记”此 Entity 是 Frame Arena owner” |
| `GEStreamOwnerComponent` | `CEffectCommandStreamOwner` | GE | StreamOwnerSingleton | stream 元数据（version/sequence） |

### IBufferElementData（`[GAS前缀][职责]Buffer`）

| 目标命名 | 旧命名 | GAS 概念 | 帧内/跨帧 | 职责 |
|---|---|---|---|---|
| `GEEffectCommandBuffer` | `BEffectCommand` | GE | 帧内 | 本帧施加意图（等效 GAS 的 Apply 请求） |
| `GESetByCallerValueBuffer` | `BEffectCommandSetByCallerValue` | GE | 帧内 | SetByCaller magnitude 数据 |
| `GEEffectSpecBuffer` | `BInstantEffectSpec` | GE | 帧内 | 运行时 GE 规格数据（等效 `FGameplayEffectSpec`），所有 GE 类型共用 |
| `AttributeModifierBuffer` | `BAttributeDelta` | Attribute | 帧内 | 已解析的属性修改器结果（等效 resolved modifier） |
| `ActiveGameplayEffectBuffer` | `BActiveEffectSlot` | GE | **跨帧** | 目标 ASC 上的 active effect（等效 `FActiveGameplayEffect`） |
| `ActiveEffectMutationBuffer` | `BActiveEffectMutation` | GE | 帧内 | duration/stack/period 状态变更 |
| `GameplayEventBuffer` | `BTypedSimulationFact` | GameplayEvent | 帧内 | Core 内部 gameplay 事件（驱动 Ability trigger / GE reaction） |
| `PresentationEventBuffer` | `BPresentationEvent` | GAS | 帧内 | Boundary 表现层事件（UI/Cue/VFX/SFX 消费） |
| `TargetDataBuffer` | `BTargetDataResult` | Target | 帧内 | 目标解析结果列表 |

### IEnableableComponent（`[GAS前缀][职责]Tag`）

| 目标命名 | 旧命名 | GAS 概念 | Toggle 频率 | 职责 |
|---|---|---|---|---|
| `AbilityActiveTag` | `CAbilityActive` | Ability | 低频（grant/revoke） | Ability 当前是否可用 |
| `AbilityActivatingTag` | `CAbilityInTryActivate` | Ability | 中频（激活期间） | Ability 正在激活流程中 |
| `PeriodDueTag` | `CPeriodDue` | GE | 每帧 | period tick 到期，需派生 EffectCommand |

### IChunkComponent

| 目标命名 | 旧命名 | 职责 |
|---|---|---|
| `AllIdleChunkComponent` | `ChunkAllIdle` | chunk 内所有 ASC 的所有 effect slot 都是 idle |
| `NoActiveEffectsChunkComponent` | `ChunkNoActiveEffects` | chunk 内所有 ASC 都没有 active effect |

### ISystem（`[GAS前缀][职责]System`）

| 目标命名 | 旧命名 | Phase |
|---|---|---|
| `GASFrameArenaSetupSystem` | `SFrameArenaSetup` | FramePrepare |
| `GEEffectCommandIngestSystem` | `SEffectCommandIngest` | CommandIngest |
| `AbilityCommandIngestSystem` | —（新增） | CommandIngest |
| `GEEffectSpecBuildSystem` | `SInstantEffectSpecBuild` | SpecEval |
| `ActiveEffectMutationApplySystem` | `SActiveEffectMutationApply` | SpecEval |
| `AttributeModifierApplySystem` | `SAttributeDeltaApply` | DeltaApply |
| `ActiveEffectTickSystem` | `SEffectTick` | ActiveLifecycle |
| `ActiveEffectSlotSyncSystem` | `SActiveEffectSlotSync` | ActiveLifecycle |
| `ChunkComponentMaintainSystem` | `SChunkComponentMaintain` | ActiveLifecycle |
| `GameplayEventProjectionSystem` | `STypedSimulationFactProjection` | TypedFact |
| `CueRequestProjectionSystem` | `SInstantEffectCueRequestProjection` | TypedFact |
| `GameplayEventLegacyBridgeSystem` | `STypedSimulationFactEventBridge` | TypedFact（迁移期） |
| `AbilityCommitSystem` | `SAbilityCommit` | CommandIngest / SpecEval |
| `PresentationOutboxSystem` | `SPresentationOutboxProjection` | Observation |
| `ReplayLogSystem` | `SDebugReplayLogProjection` | Observation |
| `DiagnosticsSnapshotSystem` | `SDiagnosticsSnapshotExport` | Observation |

### ComponentSystemGroup（`GAS[Phase]SystemGroup`）

SystemGroup 命名保持原有模式，统一使用 `GAS` 大写前缀：

| 目标命名 | 旧命名 |
|---|---|
| `GASFramePrepareSystemGroup` | `GasRuntimeFramePrepareSystemGroup` |
| `GASCommandIngestSystemGroup` | `GasCommandIngestSystemGroup` |
| `GASSpecEvaluationSystemGroup` | `GasSpecEvaluationSystemGroup` |
| `GASActiveEffectLifecycleSystemGroup` | `GasActiveEffectLifecycleSystemGroup` |
| `GASDeltaApplySystemGroup` | `GasDeltaApplySystemGroup` |
| `GASGameplayEventProjectionSystemGroup` | `GasTypedFactProjectionSystemGroup` |
| `GASStructuralPlaybackSystemGroup` | `GasStructuralPlaybackSystemGroup` |
| `GASObservationProjectionSystemGroup` | `GasObservationProjectionSystemGroup` |

### BlobAsset Definition（`[GAS前缀]Definition`）

| 目标命名 | 旧命名 | 职责 |
|---|---|---|
| `GameplayEffectDefinition` | （Blob 统称） | GE 静态定义（Duration/Period/Stack/Modifier/Tag/Cue 配置） |
| `AbilityDefinition` | （Blob 统称） | Ability 静态定义（tag requirement/cooldown/cost/effect 引用） |
| `AttributeSetDefinition` | （新增） | 属性集定义（属性列表、初始值、Min/Max/曲线引用） |

### Singleton Entity 命名

| 目标命名 | 旧命名 | 理由 |
|---|---|---|
| `FrameArenaSingleton` | `FrameArenaSingleton`（不变） | 全局唯一，跨帧持久 |
| `GEStreamOwnerSingleton` | `EffectCommandStreamOwner` | 全局唯一 GE 命令流 owner（proof-only） |

## 层级命名约束（更新）

| 层级 | 推荐命名 | 禁止误用 |
|---|---|---|
| Application Shell Layer | `*Shell`、`*Presenter`、`*Controller`、`*Runner`、`*SceneInstaller`、`*ResourceBinding` | 不使用 `*System` 表示 OOP 业务对象，不暴露 `EntityManager` |
| Runtime Boundary Layer | `*CommandGateway`、`*ReadModel`、`*OutboxBridge`、`*DiagnosticsSink`、`*ReplaySink`、`*BoundarySystem` | 不用泛化 `*Adapter` 承载业务计算、缓存、日志和事件分发 |
| GAS Runtime Core Layer | 见上：`[GAS前缀][职责][DOTS后缀]` | 不使用 `Manager`、`Facade`、泛化 `Helper`、真实 UI/VFX/SFX 资源名；不使用旧 `C*`/`B*`/`S*` 单字母前缀 |
| Definition & Generation Layer | `*Definition`、`*Registry`、`*Lookup`、`*BakePlan`、`*ValidationReport`、`*GeneratedIds` | 不使用 `Runtime` 命名承载生成产物，不生成 `*LifecycleSystem` |

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
| `Resolver` | 纯计算解析，输入明确，输出明确 | 不执行结构变化，不调用外部资源 | `ModifierMagnitudeResolver` |
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
| 嵌套命名 | 子 SystemGroup 使用 `GAS<Phase><SubPhase>SystemGroup` 模式 |

示例：`GASFramePrepareSystemGroup`、`GASSpecEvaluationSystemGroup`、`GASStructuralPlaybackSystemGroup`。

### ISystem 命名

| 规则 | 说明 |
|---|---|
| 格式 `[GAS前缀][职责]System` | 所有 `ISystem` 实现使用 GAS 概念前缀 + 职责描述 + `System` 后缀 |
| 语义完整名 | 使用完整语义名，不使用缩写：`GEEffectCommandIngestSystem`，非 `GECIS` |
| Phase 归属 | System 名应表达所在 phase：`[GAS前缀]` + Phase + 具体职责 + `System` |

示例：`GEEffectCommandIngestSystem`、`GEEffectSpecBuildSystem`、`AttributeModifierApplySystem`、`GameplayEventProjectionSystem`。

### Job 结构体命名

| 规则 | 说明 |
|---|---|
| 后缀 `Job` | 所有 `IJobEntity` / `IJobChunk` / `IJobParallelFor` 结构体使用 `*Job` 后缀 |
| 语义前缀 | Job 名应表达输入→输出的转换：`BuildInstantSpecsJob`、`TickActiveSlotsJob` |
| Burst 标注 | Burst 编译的 Job 名不额外加前缀；是否 Burst 由 `[BurstCompile]` 属性表达 |

### BlobAsset 命名

| 规则 | 说明 |
|---|---|
| 引用字段 | `BlobAssetReference<T>` 字段名以 `Blob` 结尾：`AbilityDefinitionBlob`、`EffectConfigBlob` |
| Blob 类型 | Blob 根类型以 `*Blob` 或 `*DefinitionBlob` 结尾 |
| 不要 BlobData | 不使用泛化 `BlobData` 后缀，必须表达数据内容 |

### Singleton Entity 命名

| 规则 | 说明 |
|---|---|
| 后缀 `Singleton` | 全局唯一的 entity 使用 `*Singleton` 后缀：`FrameArenaSingleton` |
| 后缀 `Owner` | 拥有 frame-local stream 的 singleton entity 使用 `*Owner` 后缀：`EffectCommandStreamOwner` |
| 区分标准 | `Singleton` = 跨帧持久且全局唯一；`Owner` = 持有可变 buffer 的帧级单例 |

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

## 当前命名问题样本

1. `AbilitySystemFacade` 位于 `Assets/GAS/Runtime/AbilitySystem/AbilitySystemFacade.cs:7-27`，同时暴露 Entity、GameObject 和 Observation，后续应拆成应用壳层 API、CommandGateway 和 ReadModel。
2. `EventBusHelper` 位于 `Assets/GAS/Runtime/Event/EventBusHelper.cs:7-45`，实际职责是 fact append / sequence / batch capability，不应继续以 Helper 掩盖边界写入语义。
3. `GASManager` 位于 `Assets/GAS/Runtime/General/GASManager.cs:6-36`，承担 world host、entity manager、global singleton、bootstrap 多种职责，命名过宽。
4. `HeadlessAutoChessScenario` 位于 `Assets/GAS/Runtime/Demo/AutoChess/HeadlessAutoChessScenario.cs:12-45`，当前文件还承担配置、执行、报告、压力参数等职责，后续 AutoChessDemo 重构应拆为 ScenarioDefinition、Runner、ValidationReport、ScaleProfile。
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
