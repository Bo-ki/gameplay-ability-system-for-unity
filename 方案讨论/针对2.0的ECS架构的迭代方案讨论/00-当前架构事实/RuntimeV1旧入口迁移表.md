# Runtime v1 旧入口迁移表

> Owner：`00-当前架构事实` | 盘点冻结 SHA：`b12889eb5c45f01362813e57ab0cfe269b2b8337` | 状态：阶段 A 当前代码事实，不代表目标实现或删除授权

本文只回答“当前哪条代码路径拥有权威、谁在调用、应迁到哪个 v1 owner/lane、何时才可删除”。目标语义以 [17-GAS 业务链路破坏性重划分 Spec](../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md) 为唯一正文，执行顺序以 [Runtime v1 不可兼容迁移任务](../02-主线任务树/RuntimeV1不可兼容迁移/README.md) 为准。

盘点以冻结 SHA 下的受跟踪源码为准，使用精确符号与路径 `rg` 统计定义和调用。以下“无引用”均指仓内静态引用为零；反射、Unity 序列化 GUID、生成器输入以及 `autoReferenced` 公共 API 可能绕过词法引用，因此分别列为风险，不能据此越过对应删除门。

## 基线事实

| 项目 | 冻结事实 | 对迁移的约束 |
|---|---|---|
| Unity | `ProjectSettings/ProjectVersion.txt`：`6000.3.14f1`，revision `d68c3f99a318` | 验收必须在该编辑器版本执行 |
| DOTS 包 | `Packages/manifest.json`：Entities `1.4.6`、Entities Graphics `1.4.19`、Physics `1.4.6`；`packages-lock.json`：Burst `1.8.29` 为传递依赖 | v1 API 与调度实现按该版本编译，不以更高版本能力为前提 |
| Runtime asmdef | `Assets/GAS/Runtime/com.exhard.exgas.runtime.asmdef`：`autoReferenced: true`、`allowUnsafeCode: true`，引用 General 与 Unity DOTS 包 | 公共类型可能有仓外调用方；删除前仍须编译、序列化与 API 清单门 |
| Generated asmdef | `Assets/GAS/Generated/CodeGen/Runtime/com.exhard.exgas.generated.runtime.asmdef`：依赖 Runtime、Collections、Entities、Burst | 依赖方向是 Generated → Runtime；手写 Runtime 不得反向依赖生成程序集 |
| Demo asmdef | `Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef`：同时依赖 General、Runtime、Generated Runtime 与 DOTS 包 | Demo 迁移必须在 V6 删除门前完成，不能以兼容层保留旧入口 |
| 测试 | 受跟踪 `Assets` 下不存在 `Assets/_Test`、`*Tests.cs` 或测试 asmdef | 当前没有可执行 GAS 语义回归基线；V0 是删除任何活跃权威路径前的硬门 |

## 旧入口与权威路径迁移表

阶段含义：V0 测试基线；V1 Definition/稳定身份；V2 ASC slab/handle；V3 单 Tick Kernel/Ability；V4 Effect/Attribute/Tag；V5 Boundary/Cue；V6 Demo 迁移与旧链删除；V7 确定性规模门。

| 旧类型、入口与调用方证据 | 当前权威事实 | v1 目标 owner / lane | 建立阶段 | 最早删除阶段与门 |
|---|---|---|---|---|
| `GASFramePrepareSystemGroup`、`GASCommandResolveSystemGroup`、`GASCoreSimulationSystemGroup`、`GEExecutionCalculationExtensionSystemGroup`、`GASStructuralCommitSystemGroup`、`GASBoundaryProjectionSystemGroup`；定义于 `Assets/GAS/Runtime/System/SystemGroup/GASGroups.cs`；由 `GASSystemScheduleContract` 创建、注册和排序；`GASManager.Initialize` 创建 FixedStep 与旧组 | 五段自定义 physical group 与自定义 structural commit ECB 是当前实际调度骨架 | `GasFixedTickSystemGroup`；`GasCommandIngressSystem` → `GasTickKernelSystem` → Unity `EndFixedStepSimulationEntityCommandBufferSystem`；catch-up 后由 managed `GasBoundaryDrainSystem` 单独 Drain | V3 建立单 Kernel/标准 EndFixed；V5 建立 Drain | V6；单 Tick writer、标准 EndFixed、V0 语义向量及 Demo ticker 全部切换后整链删除 |
| `BeginGASStructuralCommitECBSystem`、`EndGASStructuralCommitECBSystem`；`AbilityStateCleanupSystem` 明确取用后者 | 结构变更提交点由 GAS 自定义 ECB 占有 | Unity 标准 `EndFixedStepSimulationEntityCommandBufferSystem` | V3 | V6；所有 producer 已改写且无旧 ECB 查询/更新命中 |
| `AbilitySlotBuffer.AbilityEntity`；`AbilityStateComponent`；`GASRuntimeEntityArchetypes` 的 Ability/GrantedAbility archetype；`ASCCommandBufferResolveSystem` grant 时创建 Ability Entity；`AbilityTryActivateSystem`、`AbilityCommitSystem`、`AbilityLifecycleRequestSystem`、`AbilityStateTickSystem`、`AbilityStateCleanupSystem` 查询 Ability Entity | Ability 身份、phase、activation count 与生命周期分散在 per-Ability Entity 上，是现有 Ability 权威链 | ASC Entity 上 non-compacting `GrantedAbility`、`Activation`、`Continuation`、`Subscription` slab；`OwnerPlanBuildJob` 规划，`AscOwnerCommandWaveJob` 提交 | V2 建 slab/handle；V3 建 Ability transaction | V6；V2-V4 集成窗口完成、handle 代际校验与 V0 Activate/Commit/Cancel/Wait 向量全绿后删除 Entity 链 |
| `LegacyGameplayEffectEntityBuffer`、`ASCActiveEffectsComponent`、`ActiveGameplayEffectBuffer`、`GASRuntimeEntityArchetypes` 的 GameplayEffect Runtime/Prototype archetype；`ActiveGameplayEffectBuffer.ActiveEffectEntity`；`GASActiveEffectRuntime` 两处 `Slots.RemoveAt(slotIndex)` | ASC owner-local slot 与 legacy GE Entity/全局索引并存，且紧凑删除使 slot 不稳定，构成双权威 | target ASC 上 non-compacting Effect/Payload/Capture/Aggregator slab；`SourceSpecProjection` 密封 source snapshot，`AscTargetStateWaveJob` 单写 target | V4 | V6；Effect/Attribute/Tag 语义向量全绿、无 Entity promotion、无 compacting slot、全局索引零命中 |
| `GASManager` 创建 effect command singleton 与 ActiveEffect global index；`GEExecutionCalculationSystem`、`GEExecutionCalculationOutputModifierSystem` 查询 legacy GE components；`AutoChessExecuteDamageCalculationSystem` 另行消费 owner-local `GEEffectSpecBuffer` | GE execution 同时走 legacy Entity 查询与 Demo 自定义 owner-local spec 计算 | `GasTickKernelSystem` 内的 `AscTargetStateWaveJob`/Attribute apply lane；计算 evaluator 为纯函数，无独立 system owner | V4 | V6；AutoChess evaluator 已迁移且所有 legacy query/system 从 schedule 消失 |
| `GameplayEventBusComponent` singleton；`BoundaryObservationComponent`、Attribute/Cue/Replay buffers；`GameplayEventBuffer`、`OwnerLocalGameplayFactBuffer`；`GameplayBoundaryFactExportSystem`、`GameplayFactBoundaryProjectionSystem`、`PresentationOutboxProjectionSystem`、`ReplayLogSystem`、`DiagnosticsSnapshotSystem`、`ASCDestroyFinalizeSystem` | singleton/EventBus 与多个 Boundary consumer 同时解释、复制或清理事实 | ASC/Session scoped `ICleanupBufferElementData` outbox；`BoundaryDrainState`；恰一个 managed `GasBoundaryDrainSystem` 按 immutable batch 接管 | V5 | V6；retry/ack/watermark、terminal destroy handoff 与“Result 后零事实”向量全绿后删除 singleton/多 consumer 链 |
| `AutoChessGasObservationGateway` 直接读取/解释/清空 EventBus、Replay 与 debug buffers；`GASWatcher` 通过 `GASRuntimeShell` 读取 singleton/buffer/legacy GE | Demo 与 Debugger 绕过单一 Drain，直接拥有 ECS 边界消费行为 | 只读 `RuntimeSnapshot`/diagnostics 与 `GasBoundaryDrainSystem` 已接管的 managed staging；调用方不持有 buffer 清理权 | V5 | V6；Demo/Debugger 均只消费 snapshot/staging，旧 buffer 访问零命中 |
| `CueRequestBridgeSystem`、`CueManagedLifecycleSystem` 均 `[DisableAutoCreation]`；前者仅被后者的排序 attribute 提及，二者未进入 `GASSystemScheduleContract.BoundaryProjectionSystemTypes`；bridge 对 `CueEntity.Null` 直接返回，而现有 projection 产生 Null cue entity | Cue bridge 在当前手工注册模型下不可达；即使被注册，Null cue 也使主要投影无法进入 managed lifecycle | `GasBoundaryDrainSystem` 驱动 Cue `Requested → Active → Removed → Destroyed` 四阶段，按 stable Avatar binding 路由 | V5 | V5/V6；先以 Cue active-cycle、rebind 和 teardown 向量替代，随后删除 bridge/entity lifecycle，不保留兼容 facade |
| `GASManager.Initialize/Shutdown`；仓内实际 host 为 `AutoChessGasRuntimeHost`；`ASCCommandPort` 保存 raw `EntityManager/Entity` 并经 `ASCBoundaryCommandWriter` 写命令；`GASRuntimeShell` 暴露 World、EntityManager、singleton、groups 与 job drain | Runtime session、命令入口、调度控制和调试读取通过多个 raw ECS facade 暴露 | `RuntimeSession`；stable ASC handle + immutable `CommandPort` ingress；snapshot/diagnostics；单 Drain；外部不持有 gameplay writer 或 group update 权 | V1 建 session/identity；V3 建 ingress/kernel；V5 建 snapshot/drain | V6；host、Demo、Editor 调用方全部迁移，旧 public facade 零仓内命中并完成公共 API/序列化审计 |
| `AbilitySystemBinding` MonoBehaviour；仓内无 prefab/scene/asset GUID 命中，但 `CatchAreaBox3D` 通过 `GetComponent<AbilitySystemBinding>()` 动态获取 | 未发现序列化引用不等于无运行时依赖；仍是 public Runtime API | stable ASC handle 的场景绑定/adapter，不携带 ASC 权威副本 | V1/V2 | V6；动态调用方迁移、全项目 GUID/反射/API 审计通过后删除 |
| `AutoChessGasRuntimeAccess` 代理 World、EntityManager、旧 groups、singleton diagnostics、command port 与 job drain；`AutoChessGasRuntimeTicker` 手工更新五组；`AutoChessRuntimeSystemBootstrap` 向 command/core 组注入两个 Demo system | AutoChess 当前既是 runtime host，又部分拥有 schedule 与 evaluator | RuntimeSession runner 只更新完整 FixedStep；Demo 只提交 immutable ingress 并消费 Drain/snapshot，不注入 Core system | V3/V5 | V6；完整 Demo 迁移与旧组/旧 access 零命中后一次性删除 |
| `AutoChessBattleDefinitionCatalogBuilder.BuildCatalog()` 调用 `GASGeneratedDefinitionCatalogData.Populate` 并安装 `GASDefinitionCatalogComponent`；实际 Runtime 解析仍使用手写 `GASRuntimeDefinitionResolver` / `GASRuntimeRequirementEvaluator`；`GASGeneratedRuntimeDefinitionResolver` / `GASGeneratedRequirementEvaluator` 无 Runtime 调用方 | 生成 catalog 数据是活路径，但生成 runtime resolver/evaluator 与手写 resolver 并未形成单一执行权威 | phase-aware immutable Blob Catalog；生成代码只做纯数据/纯 evaluator glue，不拥有 query、lifecycle、ECB 或 runtime state | V1 | V6；V1 明确生成边界、Runtime 无反向 asmdef 依赖、所有 definition lookup 只有一条路径后删除旧 resolver/glue |

## 可证明无仓内引用的死代码候选

这些候选可进入阶段 A 删除清单，但“词法零引用”不是单独的删除授权。每批删除至少要同时更新硬编码工具规则，执行 Unity 编译，并复查反射、生成输入、序列化 GUID 和 public API 风险。

| 候选 | `rg` 证据范围 | 建议阶段 | 风险与删除条件 |
|---|---|---|---|
| `Assets/GAS/Runtime/Effect/Component/GEConfigRefComponent.cs`：`GEConfigRefComponent` | 类型名只有定义 1 次；`BlobGEConfig`、`ModifierDef` 只在同文件内部出现 | 阶段 A 可直接候选 | Runtime asmdef 为 `autoReferenced`；先过公共 API 清单与 Unity 编译，整文件连同 `.meta` 成对处理 |
| `Assets/GAS/Runtime/Ability/AbilityRuntimeActions.cs`：`AbilityRuntimeActions` 及其 cost/cooldown/cancel/cleanup helpers | 仓内源码无外部调用 | 阶段 A 可直接候选 | `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1` 对该文件/文本有硬编码检查；必须与工具规则同批更新，否则会制造假失败 |
| `AbilityActivationResult` | 代码零引用；仅 Wiki 文本提及 | 阶段 A 条件候选 | public API 风险高于内部类型；先确认仓外/包消费者契约 |
| `TargetDataHeaderComponent`、`TargetEntityBuffer`、`TargetPointBuffer`、`TargetDirectionBuffer`、`TargetHitBuffer` | 各类型只有声明；同文件 `ETargetDataKind` 仍有活引用 | 阶段 A 条件候选 | 只能做文件内精准删除，不能删除整个 `TargetDataComponents.cs` |
| `GETargetPointBuffer`、`GETargetDirectionBuffer`、`GETargetHitBuffer` | 各类型只有声明；同文件 `GEContextComponent` 仍有活引用 | 阶段 A 条件候选 | 只能精准删除三个 buffer，保留 live context |
| `GEEffectPendingApplyComponent`、`GEEffectCleanupComponent`、`GECreatedByAbilityComponent` | 各类型只有声明；同文件其他 legacy lifecycle/remove 类型仍有活引用 | 阶段 A 条件候选 | 只能精准删除；legacy GE 全链仍应等 V6 删除门 |
| Definition planning graph 五文件：`GASDefinitionGeneratedAdapter.cs`、`GASGeneratedDefinitionBakingPlan.cs`、`GASGeneratedDefinitionBakeContract.cs`、`GASGeneratedDefinitionBakePipeline.cs`、`GASGeneratedDefinitionRuntimeIntegrationPlan.cs` | 34 个 public 类型在 `Assets`、`Tools`、`AutoChessDemo` 中无五文件外命中 | V1 决策、最迟 V6 删除 | public/generated/reflection 风险；需先冻结 SourceGenerator 边界，确认不是外部生成流程约定 |
| `GameplayEffectRuntimePipelineContract.cs` | 文件内声明类型均无外部命中 | 阶段 A 条件候选 | 名为 contract 且 public，需公共 API/文档/工具规则审计 |
| `GASRuntimeFrameBackboneRebindContract.cs` | 文件内声明类型均无外部命中 | 阶段 A 条件候选 | 同上；确认未被反射或仓外测试消费 |
| `GASRuntimeSequenceOwnerContract`、`GASRuntimeSequenceOwnerEntry`、`EGasRuntimeSequenceOwnerKind` | 三个符号无文件外引用 | 阶段 A 条件候选 | 与活跃的 `GASRuntimeSequenceAllocator`、`EGasRuntimeSequenceKind` 同文件；禁止整文件删除 |
| `CueRequestBridgeSystem`、`CueManagedLifecycleSystem`、`GameplayCueUnit` | 前两者无手工 schedule 注册；`GameplayCueUnit` 无仓内 caller 且标记 obsolete | V5 建立替代，V5/V6 删除 | 这是“运行不可达/公共 facade”而非纯私有死代码；Cue 四阶段与边界向量未建立前不得抢删 |
| `GameplayEffectConfigRegistry.CreateRuntimeEffectInstance` 及 `GameplayEffectEntityFactory` legacy runtime factory 路径 | 前者无 caller；后者只由 registry/static-blob warmup planning 路径触达 | V1 明确替代，V6 删除 | 配置反射、authoring、warmup 与迁移依赖存在，不能在阶段 A 仅凭引用数整链删除 |

## 不得误判为死代码

1. `CueLog`、`CuePlayAnimator`、`ConfCueOn*` 等符号存在 Editor 反射扫描、Luban 或 CodeGen 消费，不能按普通 C# caller 数判断。
2. TargetCatcher 类型由 `EditorTargetCatcherHelper`、`BeanUpdater` 与生成配置行驱动，存在反射/数据入口。
3. `AbilitySystemBinding` 虽无序列化 GUID 命中，仍有 `GetComponent<T>()` 动态调用方。
4. Runtime asmdef `autoReferenced: true`；任何 public 类型删除都必须显式承认可能破坏仓外消费者，这是 v1 破坏性切换的已知风险，不是跳过审计的理由。

## 阶段 A 删除门

1. 先完成 V0 可执行测试 asmdef 与 red/green manifest；没有真实测试时，只允许删除“零仓内引用且无反射/序列化/工具命中”的独立候选。
2. 每个候选重新执行定义名、GUID、字符串、反射注册、生成输入与工具规则搜索；共享文件只做符号级删除。
3. 删除 C# 文件时同步处理 `.meta`，删除 public 类型时记录明确破坏面；禁止新增 shim、obsolete 转发或配置开关。
4. 每批通过 Unity 编译和相关 EditMode/PlayMode 语义向量后再扩大范围；调度、Ability、GE、Boundary、Demo 活跃链严格等到表中阶段。
5. V6 删除门要求旧 schedule、Ability Entity、legacy GE Entity/global index、singleton EventBus、多 Boundary consumer、raw Runtime facade 与 Demo 注入系统均为零运行命中，而不只是零词法命中。
