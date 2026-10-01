# Runtime V1 当前实现与 UE-GAS 语义综合审查

> 审查日期：2026-08-29
> 审查快照：`HEAD 10cbd25668ecb4650cae53b30d30885b54105742` + dirty worktree
> 快照规模：61 个 tracked modified + 11 个 untracked
> Unity：`6000.3.14f1`
> UE-GAS 对照源：`E:/Unity/UnityProjects/_Git/Y_GameFramework/Tools/Github/UE-GAS/Source`
> 状态：只读静态审查事实；不代表问题已修复，不代表任何 V0-V7 阶段已退出
> 历史裁决：第六轮多 Agent 当时将下一轮收敛为 `RuntimeV1-Runnable-ClosedWorld`；对应计划见[第六轮交叉评审与单轮可运行计划](RuntimeV1-第六轮多Agent交叉评审与单轮可运行计划.md)
> 2026-08-30 状态增量：Development ClosedWorld 已完成；本文的旧 8 个 P0 表不得再原样解释为当前状态。scoped 完成度与 V1.1 第一门以[完成后停顿审查与 D0-M2F 单轮计划](RuntimeV1-完成后停顿审查与V1.1-D0-M2F单轮计划.md)为准。

## 1. 文档用途

本文档将连续四轮审查结论按语义领域去重，作为其他 Agent 可直接领取的问题账本。它同时对照：

1. 当前 Runtime V1 代码和测试。
2. `01-目标态架构共识/` 中的目标 Spec。
3. 本地 UE-GAS 源码中可迁移的 Gameplay Ability System 核心语义。
4. Luban、sidecar、CodeGen、Catalog、AutoChess 和 Tier-B 证据链。

本文档不要求复制 UE 的 UObject、delegate、prediction、replication 或 AbilityTask 对象模型。对照的是以下可迁移语义：权威输入、准入、Commit/End、Tag/Block/Cancel、Effect/Stack/Period、事实可观测性、稳定身份、容量证明和失败原子性。

证据路径均相对于项目根目录；表格中只写文件名时，使用 `rg --files | rg '<file-name>'` 解析当前唯一路径。UE 证据路径相对于页首的 UE-GAS 对照源。由于共享工作树在审查后仍可变化，修复 Agent 必须以符号和代码语义重新定位，不得仅依赖旧行号。

### 1.1 目标契约主要索引

- Ability/Effect 业务语义：[01B-GAS 业务语义链路](../../方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/01B-GAS业务语义链路概念设计Spec.md)、[24-GAS 官方概念对照](../../方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/24-GAS官方概念对照复核Spec.md)。
- Runtime DAG/Boundary：[03A-执行域与数据流](../../方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/03-RuntimeCore管线/03A-执行域与数据流Spec.md)、[03E-02-ActiveEffectStore](../../方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-Fact/03E-02-StateEvaluateActiveEffectStoreSpec.md)、[03F-StructuralCommit 与 BoundaryProjection](../../方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/03-RuntimeCore管线/03F-StructuralCommit与BoundaryProjectionSpec.md)。
- ActiveEffect：[05-ActiveEffectStore](../../方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/05-ActiveEffectStoreSpec.md)。
- 配置与生成：[08-Luban/SourceGenerator](../../方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md)、[14-Definition CodeGen](../../方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/14-DefinitionCodeGen目标链路Spec.md)、[25-配置语义编译与 CapacityProof](../../方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/25-配置语义编译契约与CapacityProof统一裁决Spec.md)。
- AutoChess 与证据：[10-AutoChess 无头验收](../../方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/10-AutoChess无头验收Spec.md)、[10B-06-业务流程走查](../../方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/10B-AutoChess完整业务案例/10B-06-业务流程走查Spec.md)、[21-AutoChessDemo 策划配置验收](../../方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/21-AutoChessDemo策划配置验收样例Spec.md)。

## 2. 总结论

**当前实现不通过完整性审查，不能声明 Runtime V1 已完成，也不能声明已等价实现 UE-GAS 的可迁移核心语义。**

四轮结论在当前树重新核对后，本文档登记 **65 个 canonical 问题 ID（63 个生产问题 + `EVD-03/06` 两个独立证据治理问题）+ 6 个 EVD 证据交叉引用 ID**，其中 **8 个 P0 发布阻断 ID**。EVD 交叉引用仅指向生产问题的假绿证据，不在阻断统计中二次计数。这些 ID 是可领取账本，不是完成度 KPI。

当前已有的单 Tick DAG、ASC slab、稳定身份、Owner/Target shadow、统一 final-publish token、Boundary ring 和 Tier-B runner 是正向基础，但仍有五类系统性断裂：

1. **配置权威断裂**：Luban、sidecar、normalized row、手写 Catalog、测试 Catalog 和 AutoChess 运行 Catalog 不是同一事实源。
2. **Ability 业务链断裂**：Tag/Block/Cancel、自然 End、Wait、GameplayEvent/OwnedTag Trigger 有数据结构或内部事务，但没有真实 Ability 生产链。
3. **Effect 安装与运行断裂**：Catalog 可接受的组合会在 apply/merge/lifecycle 确定性拒绝、fault 或静默不生效。
4. **Boundary/容量断裂**：Accepted 不总能得到唯一终态，FaultClose/Dispose 与 Core lifecycle 不闭合，多 Battle 和全局 maxima 可造成假 capacity fault。
5. **证据 oracle 断裂**：多个 Green 只证明内部事务算术、count 非零或同一错误实现重复可现，没有证明配置精确语义和真实 Boundary 端到端链路。

当前 `Tools/Tests/RuntimeV1TierBManifest.json` 登记为 **5 Green / 12 Pending**。当前仓库内没有可由这些历史 EvidenceId 独立解析的完整 artifact，因此这只是当前登记状态，不是本快照的可重验执行结果，也不是 release evidence。

## 3. 严重度与消费规则

| 等级 | 含义 | 处理规则 |
|---|---|---|
| P0 | 破坏唯一权威、Accepted/terminal 承诺、原子发布、核心数值权威或可重放身份 | 修复前不得声明 Runtime V1 或对应阶段完成 |
| P1 | 真实业务向量缺失、安装/运行契约不一致、可观测性丢失、合法请求被假拒绝 | 对应 Tier-B/目标 Spec 保持 Pending，不得以局部单测绕过 |
| P2 | 编译期可前置的错误、证据治理、假预算或潜在技术债 | 进入发布门或下一阶段前闭合 |
| Decision | 当前实现与目标 Spec 不同，但可能是有意缩减 | 必须修改 Spec 并 bake-fail 非支持输入，或完整实现；不得保持模糊 |

本文档中“完成”表示同时满足：代码链接通、Catalog/Runtime 契约一致、真实 Boundary 向量通过、typed fact 精确且证据绑定当前快照。

问题 ID 在 Agent 间交接后保持稳定；合并或撤下的旧结论保留编号空洞，不对后续 ID 重新编号。P0 仅在第 4 节登记一次，后续领域表不重复建账。

## 4. P0 发布阻断项

| ID | 结论 | 直接后果 | 核心证据 | 最小退出门 |
|---|---|---|---|---|
| CFG-01 | AutoChess 运行 Catalog 由手写 Builder 构造，绕开 Luban/CodeGen | 重新生成配置不会改变实际战斗；9201/9202/9203/9207、Cue 和 Attribute 已发生语义分叉 | `Assets/AutoChessDemo/Battle/Ecs/AutoChessBattleDefinitionCatalogBuilder.cs:13-20,52-68,79-115,328-398`；`Assets/DataGenerated/Luban/Json/GAS/exgas_tbgameplayeffect.json:333-439` | 删除手写 gameplay 定义；运行只安装一个由当代 Luban 输入生成的 immutable Catalog |
| CFG-02 | Catalog 哈希为同源固定常量，Validator 不重算内容，也缺 `LayoutHash/ArtifactManifestHash` | 错误或过期 Catalog 可携带“正确”常量通过，replay/跨版本身份无法信任 | `AutoChessBattleDefinitionCatalogBuilder.cs:13-16`；`AutoChessGasCatalogSession.cs:17-23`；`GasDefinitionCatalogValidator.cs:64-87`；`GasDefinitionCatalogSchema.cs:650-672` | 从 candidate bytes 独立计算四 hash；Runtime install 比对外部 package descriptor，alternate Catalog 必须失败 |
| SYS-01 | Boundary Accepted 请求没有全量唯一 terminal outcome 账本 | Tick 成功后只标记 transport `Consumed`；Core 业务拒绝、fault 或 teardown 后不总有按 RequestKey 可关联的终态 | `GasBoundaryCommandProtocol.cs:20-25,505-525`；`GasTickJobs.cs:8252-8262`；`SessionIngressGate.cs:210-237,764-793`；`GasBoundaryLayout.cs:84-105` | 每个 Accepted RequestKey 恰好一个 typed terminal envelope；normal/fault/retry/GC 不重复、不丢失 |
| SYS-02 | Battle 终局发布与 Gate 权限刷新之间存在可接受且可执行的 tail 窗口 | Core 已 durable 写 `OutcomeFrozen/IngressClosed`，managed Gate 仍读旧 authority；tail 下 Tick 还可修改胜方 ASC | `GasTickJobs.cs:6860-6869,7245-7279,8049-8051`；`GasRuntimeWorldOwner.cs:417-433`；`SessionIngressGate.cs:373-382` | terminal publish/cutoff/Gate authority 单点线性化；tail 同步拒绝或 Core typed reject 且 Battle 零写 |
| ABL-08 | Commit 不是 End，但生产路径没有 normal End | 一次性 Ability Commit 后永久存活，child count 不下降，`RemoveWhenAllActivationsEnd` 无法完成 | `GasBoundaryCommandProtocol.cs:5-15`；`GasCommandPort.cs:23-112`；`GasAbilityOwnerTransaction.cs:279-284`；`GasAbilityRuntimeTypes.cs:49-57` | Activate→Commit 后独立 normal End 冻结 `Completed/WasCancelled=0`，Ending→Ended 且 child 只减一次 |
| ABL-12 | ASC 死亡后被 OwnerWave 排除，`OwnerTerminal` End 永远不发生 | live Activation、Continuation、Subscription 和 grant child count 永久滞留 | `GasTickJobs.cs:4164-4209,4258-4268,4868-4875`；`GasAbilityRuntimeTypes.cs:49-57` | 未 Commit/已 Commit/本地 timer/跨 ASC wait 四向量致死，下一确定性 cleanup 全部 `OwnerTerminal` 且 committed work 保留 |
| GE-11 | Public `RequestRemoveEffect` 被 Gate 接受，Kernel 忽略后仍标记 `Consumed` | caller 得到 Accepted，ActiveEffect 不变，且无业务拒绝可观测 | `GasCommandPort.cs:99-112`；`SessionIngressGate.cs:558-570,630-639`；`GasAbilityOwnerPlanUtility.cs:28-52`；`GasTickJobs.cs:8252-8261` | Apply→获得 handle→Remove，精确撤销 application/contributor；stale/重复 remove 均输出唯一 typed terminal |
| NUM-01 | Ability Health cost 可绕过统一 Attribute/death/fact 权威链 | Health 可被 cost 扣到零，ASC 仍 Alive，无 DeathTransition/Death fact，AliveOnly 工作继续 | `GasDefinitionCatalogValidator.cs:429-450`；`GasAbilityOwnerTransaction.cs:287-367`；`GasTickJobs.cs:4860-4880,5923-5999` | 若允许 Health cost，必须同 Tick 进入唯一死亡事务；否则 Catalog bake-fail |

后文提到这些 ID 时均引用本表，不再复制第二份问题描述。

## 5. 配置、CodeGen 与 Catalog 审查

### 5.1 权威源与语义编译

| ID | 等级 | 问题 | 触发/后果 | 证据 | 退出门 |
|---|---|---|---|---|---|
| CFG-03 | P1 | `exgas.sourcegen.json` 可追加 modifier、覆盖 refresh/expiry，是隐藏第二 gameplay authoring 源 | sidecar 缺失时静默按空继续；匹配 code 或 name；多个 override 后写覆盖 | `EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/exgas.sourcegen.json:2-24`；`LubanNormalizedRowBootstrap.cs:641-648,789-860,1012-1045` | 唯一 authoring schema；override/append 冲突按 `CFG1001` fatal；不存在静默合并 |
| CFG-04 | P1 | normalized row 是有损且未类型化的中间表 | 丢失 Cue 多 phase、GrantedAbility、Period.FirstTrigger、Duration TimeUnit/reset、AssetTags 和多值 range；又保留源 enum 整数 | `LubanNormalizedRowBootstrap.cs:80-127,641-704`；`LubanNormalizedRows.gen.cs:139-151` | 生成 typed semantic graph；任何不支持字段或 enum 映射失败必须 bake-fail |
| CFG-05 | P1 | CodeGen 直接覆盖 active files，错误时可留下混合代际 | normalized 先写，phase 各自写 active，orphan 在最终验证前删除；core 成功/demo 失败已改变活动产物 | `Assets/GAS/Editor/CodeGen/GasCodeGenPipeline.cs:32-40,56-117,119-161`；`GasCodeGenManifest.cs:251-265` | candidate 独立目录生成、全量验证、单点 atomic promotion；失败后 active bytes 不变 |
| CFG-06 | P1 | 同次 CodeGen 写出 row N 后，立即从当前 AppDomain 反射读取 compiled row N-1 | 第一次生成的 hash/report/artifact 可基于上一代；“再跑一次收敛”不符合一次运行一致性 | `GasCodeGenPipeline.cs:59-60`；`GasRowScanner.cs:14-45`；`RowMetadataFactory.cs:169-220` | 一次 CLI/CI 运行从同一 candidate input 得到同一套 output/hash；不依赖旧 AppDomain factory |
| CFG-07 | P1 | Manifest 和 validation report 无法表达目标必需产物 | 没有四 hash、artifact byte hash、TypedContract、LayoutProof、CapacityProof；missing-required 基本只检查 asmdef | `GasCodeGenManifest.cs:13-28,378-403`；`GasGlueCodeGenPhases.cs:514-548`；`Assets/GAS/Generated/CodeGen/GasCodeGenValidationReport.md:7-19` | manifest 覆盖全部 required kinds/bytes/hash/provenance；删任一 required artifact 必须失败 |
| CFG-08 | P1 | AutoChess sidecar/formula/单位不参与 InputHash | 修改 `autochess.sourcegen.json` 可产生不同代码但 manifest identity 不变；Core/AutoChess manifest 当前 InputHash 相同 | `GasCodeGenContext.cs:323-370`；`AutoChessDemoConfigModel.cs:369-480`；`AutoChessDemoCodeGenPhase.cs:17-38` | 所有影响 artifact bytes 的输入都进 content/input hash；单字节修改导致 descriptor 变化 |

### 5.2 AutoChess 具体分叉

| 定义 | Luban/sidecar 事实 | 手写运行 Catalog | 审查结论 |
|---|---|---|---|
| 9201 | `Subtract 12`，Cue 9301 | `Add -44` | 伤害值和 Cue 都不一致 |
| 9202 | `Subtract 8`，Cue 9301 | `Add -44` | 伤害值和 Cue 都不一致 |
| 9203 | 主表无 Modifier；sidecar 追加 Attribute 2；Duration 8/Period 2/Stack 3 | 目标 Health，来源为手写 Attribute 3/Attack，Cue 9403 | 三个输入源彼此不一致，且没有单一 Attack authoring authority |
| 9207 | 无 Modifier，Cue 9301 | 手写 formula，Cue 9404 | generated evaluator 存在但运行零调用方 |
| AttributeSet 9001 | 只有 Attribute 1/2，Max 99999 | 新增 Attribute 3，Health Max 1000 | layout/hash/默认值不可信 |

AutoChess 当前 validation 只要求 Completed、winner、count 达到最小值、有非零 fact 且重复运行自洽；没有断言 9201=12、9202=8、9203 精确公式、Cue=9301 或 Catalog 四 hash。证据：`Assets/AutoChessDemo/Battle/Validation/AutoChessBattleValidationRun.cs:543-566,664-701,756-796`。

## 6. Ability、Grant、Wait 与 Trigger 审查

| ID | 等级 | 问题 | 触发/后果 | 核心证据 | 最小退出门 |
|---|---|---|---|---|---|
| ABL-01 | P1 | AbilityTags、ActivationOwnedTags、BlockAbilitiesWithTag、CancelAbilitiesWithTag 只有布局空壳 | A 激活后 B 仍可激活，运行中 C 不会被 cancel，同 OwnerWave 的 requirement 看不到 A | `GasDefinitionCatalogSchema.cs:539-550`；`GasAbilityRuntimeTypes.cs:87-94`；`GasAbilityOwnerPlanUtility.cs:101-157`；`GasAbilityOwnerTransaction.cs:187-220` | 同 wave `Activate(A)→Activate(B)` + 预置 C，断言 owned-tag refcount、B blocked、C Ending 和 contribution 归属 |
| ABL-02 | P1 | 普通 Cancel 没有 CancelPolicy/NotCancelable | 不可打断施法窗口也会被玩家 Cancel 或 cancel-by-tag 无条件终止 | `GasDefinitionCatalogSchema.cs:539-550`；`GasAbilityRuntimeTypes.cs:62-82`；`GasAbilityOwnerPlanUtility.cs:224-260` | 普通 Cancel 返回 typed not-cancelable 且 phase 不变；GrantRemoved/OwnerTerminal 强制终止仍可穿越 |
| ABL-03 | P1 | Cancel→Activate 的 concurrency shadow 过早释放 child 名额 | 旧 activation 带跨 ASC wait 时仍在 Ending，同 wave 新 activation 却被放行，真实 child count 超限 | `GasAbilityOwnerPlanUtility.cs:600-623`；`GasAbilityOwnerTransaction.cs:432-443`；`GasAbilityLifecycleMaintenance.cs:88-96,290-307` | 有远端 subscription 时 Cancel→Activate 必须拒绝；ack/finalize 后下一 Tick 才放行 |
| ABL-04 | P1 | GrantedSpec.Level 是假权威，InputBindingId 无生产者 | 同 Definition 以不同 Level grant，cost/cooldown/DirectEffect 完全相同；InputBindingId 也不参与输入路由 | `GasAscLayout.cs:264-265`；`GasStageBSpawnFinalizeJob.cs:1992-2008`；`GasAbilityOwnerPlanUtility.cs:287-304,366-375` | Level 实际进 evaluator，InputBindingId 由输入路由生产并冻结；或从 Simulation 删除两个假字段并修改 Spec |
| ABL-05 | P1 | 真实 Ability 执行路径无法进入 Wait/Continuation | `TryBeginWait` 全 Runtime 无生产调用方；TB-08 通过手工 seed activation 和直调内部 transaction 变 Green | `GasAbilityWaitSlabTransaction.cs:33-68`；`GasDefinitionCatalogSchema.cs:539-550`；`RuntimeV1AbilityWaitDagPlayModeTests.cs:86-121` | 从 Boundary Activate/Commit 和 authored Ability program 到 timer/event/tag wait，禁止测试手工 seed |
| ABL-06 | P1 | GameplayEvent/OwnedTag 触发 Ability 的生产链缺失 | 没有 Boundary event 命令、trigger range、grant register/remove unregister、deferred reaction activation | `GasBoundaryCommandProtocol.cs:5-15`；`GasDefinitionCatalogSchema.cs:539-550`；`GasAbilityRuntimeTypes.cs:108-118` | T 发 child event，T+1 按 exact/parent 稳定顺序激活；OwnedTag 0→1 激活、1→0 按策略取消；remove grant 后不触发 |
| ABL-07 | P1 | Edge/Event 水位和 payload handoff/ownership 只由测试伪造 | baseline 用 registration CommandSequence；Wait 只转发 handle，没有 payload byte reader、Owner/Kind/live-record/Generation 校验、retain/handoff 或终态释放 | `GasAbilityWaitObservationUtility.cs:13-41`；`GasAbilityWaitSlabTransaction.cs:445-456,1028-1058,1175-1204,1550-1556`；`GasPayloadRangeAllocator.cs:263-315` | 真实 publisher 产生 payload，subscriber T+1 通过受校验 handoff 读取完整 bytes；错 owner/kind/generation 拒绝，终态恰好释放一次 |
| ABL-11 | P1 | 已有 activation-owned/emitted ledger 缺 End 清理与 ack/handoff 契约 | ABL-01 接通贡献生产后，当前 End 仅明确等待 live Continuation，没有对 owned Tag/block/cue 和 AuditOnly/CleanupRight emitted ref 的清理规则 | `GasAscLayout.cs:296-299,375-405`；`GasDefinitionCatalogSchema.cs:539-550`；`GasAbilityLifecycleMaintenance.cs:82-98,290-310` | 接通 ABL-01 后，End 撤销自身贡献，保留普通 GE，等待 remove terminal Ack/audit handoff 后仅 tombstone 一次 |
| ABL-13 | P1 | AbilityRequest payload 没有冻结进 Activation 输入 | Activate 后的 EventData/TargetData/Context/SourceAvatar generation 丢失；跨 Tick Commit 与 Avatar rebind 不可重放 | `GasBoundaryCommandProtocol.cs:163-168`；`GasAbilityOwnerPlanUtility.cs:14-26`；`GasAbilityOwnerTransaction.cs:203-216`；`GasAscLayout.cs:291-294` | 携带非空 payload 激活，Commit 前更换 Avatar generation；按冻结策略使用或 typed stale，retry identity 一致 |
| ABL-14 | P2 | `MaxConcurrentActivations` 被当成完整 ActivationPolicy | 无法区分“拒绝重入”与“结束旧实例后 retrigger” | `GasDefinitionCatalogSchema.cs:539-550`；`GasAbilityOwnerPlanUtility.cs:602-623` | max=1 下 retrigger on/off 分别得到“确定性结束旧+创建新”和 `ConcurrencyBlocked` |
| ABL-15 | P1 | `GrantedRemovalPolicy` 在 remove 时被 command 改写 | grant 创建时冻结的 CancelImmediately/RemoveWhenAll/LeaveGranted 可被任意 cleanup caller 改义 | `GasAbilityLifecycleMaintenance.cs:13-55,233-259` | 命令 policy 与 grant policy 不同必须拒绝/fault 且原 policy 不变；匹配时才执行 |
| ABL-16 | P1 | 未通过 authority/provenance 校验的 removal 提前污染同 Tick shadow | 错 battle/source/provenance 的 removal 先使合法 Activate/Commit 返回 PendingRemove/OwnerEnding，后续真实 removal 又被忽略 | `GasAbilityOwnerPlanUtility.cs:121-125,183-195,247-254,668-688`；`GasAbilityLifecycleMaintenance.cs:233-246` | 匹配 handle 但错 provenance 的 removal 与合法 Commit 同 Tick，removal 零影响且 Commit 成功 |
| ABL-17 | P1 | `GasAbilityGrantSourceKind.Command` 只有枚举/合法性判断，没有 grant-command producer | 目标 Spec 的 bootstrap/command/ActiveEffect 三种 grant 中，当前只有 bootstrap 能真实生产 | `GasAbilityRuntimeTypes.cs:8-13`；`GasStageBSpawnFinalizeJob.cs:1992-2008`；`GasDefinitionCatalogSchema.cs:539-550` | 公开 grant command 创建带 command/source provenance 的 GrantedSpec，exact retry 不重复 grant，remove 精确命中该 source |
| ABL-18 | P1 | CanActivate 未执行 cost/cooldown 基础门禁 | 资源不足或 cooldown gate 已存在时仍创建 `RunningUncommitted` 并增 child，直到 Commit 才失败 | `GasAbilityOwnerPlanUtility.cs:115-156,208-217,381-438` | 资源不足/gate 存活时 Activate 不分配槽；两 Activate 竞争同资源时 Commit 按 canonical order 唯一成功 |
| ABL-10 | Decision | Cooldown owned-tag 只有单个 `OwnedTagIndex`，目标 Spec 为 contribution range | 无法同时表达 ability-specific 和 shared-category cooldown Tag | `GasDefinitionCatalogSchema.cs:525-534`；`GasAbilityOwnerTransaction.cs:29-68,383-405` | 实现 range；或修改 Spec 并对多 Tag Definition bake-fail |

### 6.1 UE-GAS 对照边界

需迁移的核心是：

- Ability 分类 Tag、激活期 owned contribution、block refcount 和 cancel selection。
- `CanBeCanceled` 对普通 Cancel 的门控，以及强制终止的独立通道。
- ActiveCount 在真正 End 时才减少。
- Spec Level 参与 cost/cooldown/evaluator。
- Wait/Event payload 在延迟消费前有明确所有权。

不要迁移 UE 的 UObject 实例生命周期、delegate 调用栈、prediction key 或 replication 管线。本项目的 T+1 Event/Wait 可保留为 intentional deviation，但必须有同等的稳定顺序、payload ownership 和 unregister 语义。

### 6.2 UE-GAS 源码语义锚点

| 可迁移语义 | UE-GAS 源码证据 | 本项目消费方式 |
|---|---|---|
| AbilityTags、ActivationOwnedTags、Block/Cancel by Tag | `GameplayAbilities/Public/Abilities/GameplayAbility.h:485-487,730-740`；`GameplayAbilities/Private/Abilities/GameplayAbility.cpp:199-230,793-820`；`GameplayAbilities/Private/AbilitySystemComponent_Abilities.cpp:1236-1266` | 转换为 Blob range + activation-owned refcount contribution + OwnerWave read-your-writes，不迁移 UObject |
| CanActivate 检查 cost/cooldown | `GameplayAbilities/Private/Abilities/GameplayAbility.cpp:298-356` | Activate 基础门禁不分配明知无法 Commit 的 activation；Commit 仍按 canonical order 重检 |
| 普通 Cancel 受 CanBeCanceled 门控 | `GameplayAbilities/Private/Abilities/GameplayAbility.cpp:507-528,557-565` | 生成 CancelPolicy，普通 Cancel 与 OwnerTerminal/GrantRemoved 强制终止分流 |
| ActiveCount 在 PreActivate 增加、真正 End 后减少 | `GameplayAbilities/Public/GameplayAbilitySpec.h:210-212`；`GameplayAbilities/Private/Abilities/GameplayAbility.cpp:829-831`；`GameplayAbilities/Private/AbilitySystemComponent_Abilities.cpp:1041-1065` | child count 不得在 Cancel plan 被接受时提前释放 |
| Spec Level 进入 cost/cooldown | `GameplayAbilities/Public/GameplayAbilitySpec.h:198-204`；`GameplayAbilities/Private/Abilities/GameplayAbility.cpp:913-943,1543-1564` | GrantedSpec.Level 必须是 evaluator 真实输入，或从 Simulation 删除 |
| Commit 重检 cost/cooldown，Commit 不等于 End | `GameplayAbilities/Private/Abilities/GameplayAbility.cpp:408-424,464-505` | 保留当前 Commit 冻结/原子写入的正向部分，新增独立 normal End |
| normal End 撤销 owned tags/tasks/block 并通知 ASC | `GameplayAbilities/Private/Abilities/GameplayAbility.cpp:618-715` | 生成 End policy/cleanup ledger，分离 committed work 与 activation-owned resource，恰好清理一次 |
| GameplayEvent/OwnedTag trigger 的注册、路由与注销 | `GameplayAbilities/Public/Abilities/GameplayAbility.h:718-720`；`GameplayAbilities/Private/AbilitySystemComponent_Abilities.cpp:522-585,2220-2251,2288-2309,2370-2414` | 生成 trigger index，T 收集事件、T+1 稳定顺序 reaction，grant remove 后 unregister |
| 事件输入在 activation 时复制 | `GameplayAbilities/Private/Abilities/GameplayAbility.cpp:797-800`；`GameplayAbilities/Private/AbilitySystemComponent_Abilities.cpp:2220-2251` | EventData/TargetData/Context/avatar generation 在 activation 输入中冻结，跨 Tick Commit 可重放 |
| WaitGameplayEvent 同步回调接收时复制 payload，销毁时注销 | `GameplayAbilities/Private/Abilities/Tasks/AbilityTask_WaitGameplayEvent.cpp:17-62,84-100` | UE 证据不证明 T+1 retention；本项目的跨 Tick retain/handoff/release 由目标 Spec 和自身验收证明 |
| GE grant 与 removal policy 随 Spec/Effect 冻结 | `GameplayAbilities/Public/GameplayAbilitySpec.h:44-84`；`GameplayAbilities/Private/GameplayEffect.cpp:3982-4001,4275-4303` | bootstrap/command/ActiveEffect grant 分别保留 provenance，remove 不得用 caller policy 改写 grant policy |
| Retrigger 与 block 新 activation 是不同策略 | `GameplayAbilities/Public/Abilities/GameplayAbility.h:686-696`；`GameplayAbilities/Private/AbilitySystemComponent_Abilities.cpp:1604-1619` | 生成明确 ActivationPolicy，不用单一 MaxConcurrentActivations 暗示全部语义 |
| Period 执行以 Modifier 为单位，inhibited 时不执行 | `GameplayAbilities/Private/GameplayEffect.cpp:4065-4088` | 生产 transaction 逐 Modifier 执行；Catalog 不接受与冻结契约相反的 ContinueExecution |
| Stack old/new 与 removal 可观测 | `GameplayAbilities/Private/GameplayEffect.cpp:3037-3053,4238-4240` | 输出带稳定 Effect 身份、old/new stack、reason 的 typed facts，不迁移 delegate |
| Cooldown 是 TagContainer 查询 | `GameplayAbilities/Private/Abilities/GameplayAbility.cpp:883-905,1019-1023` | 实现 contribution range，或官方裁剪为单 Tag 并 bake-fail 多 Tag |

## 7. GameplayEffect、Attribute、Tag、Capture 与 Stack 审查

### 7.1 定义支持面与安装契约

| ID | 等级 | 问题 | 触发/后果 | 核心证据 | 最小退出门 |
|---|---|---|---|---|---|
| GE-01 | P1 | Duration/Infinite Modifier 没有 persistent Contribution/Aggregator 生命周期 | `ExecuteOnApplication=0` 时 modifier 完全不生效；设为 1 时直接永久改 Base/Current，expiry/inhibit/remove 不会撤销 | `GasGameplayEffectTransaction.cs:314-358`；`GasAscLayout.cs:408-465`；`GasRuntimeV1Archetypes.cs:221-225,477-481` | Duration、Period=0、Add +20；active 时 Current +20，inhibit/expiry 后恢复，Base 不被污染 |
| GE-02 | P1 | RemovalRequirement 可进 Catalog 但 lifecycle 从不消费 | 条件满足后 Effect 仍存活、period、stack，配置被静默忽略 | `GasDefinitionCatalogValidator.cs:603-623`；`GasGameplayEffectLifecycleUtility.cs:470-535` | 授予命中 RemovalRequirement 的 Tag 后，同 Tick stabilization 中 slot 移除并输出 reason/fact |
| GE-03 | P1 | GE `DirectEffectProgramRange` 可安装但完全不执行 | 父 GE apply 成功，静态 child program 没有 child application、mutation 或 fact | `GasDefinitionCatalogValidator.cs:578-597,731-749`；`GasTickJobs.cs:1338-1385` 只展开 Ability DirectEffectProgram | Parent GE 的 child Instant GE 产生独立 ApplicationId、伤害和 causality；未支持前 bake-fail |
| GE-04 | P1 | Catalog 接受 Live、OwnerPlanBuild 和高阶 ValueView，生产只支持 Snapshot+三种 view | install 成功后才 post-admission failure；Final 还静默退化为 Current | `GasDefinitionCatalogValidator.cs:1400-1468`；`GasTickJobs.cs:4404-4462,5121-5208` | 每个允许组合都有 install→production apply 向量；未实现 mode/phase/view 在 validator 稳定拒绝 |
| GE-05 | P1 | Catalog/runtime policy 闭包不一致 | 非 stack Duration + `RemoveOneStackAndRefreshDuration` 首次 maintenance 失败；Infinite stack + duration refresh 第二次 apply 必然拒绝 | `GasDefinitionCatalogValidator.cs:533-564,1302-1341`；`GasGameplayEffectLifecycleUtility.cs:400-405`；`GasGameplayEffectTransaction.cs:463-469,1025-1032` | 所有 lifetime×stack×refresh×expiry 组合做表驱动 Catalog→apply→merge→maintenance 契约测试 |
| GE-06 | P1 | 多 Modifier 父 evaluator range 被误当成单一 postfix program | 两个各自合法的 Modifier 因父 range 最终栈深 2 被 Catalog 错误拒绝 | `GasDefinitionCatalogValidator.cs:743-749,786-823,935-970`；`GasGameplayEffectTransaction.cs:572-582,731-747` | Catalog 按 Modifier 子程序验证，生产 Tick 实际产生两条 Attribute mutation |
| GE-07 | P2 | 常量可证明非法的 Divide0/Clamp 在运行期才失败 | 静态必失败程序可安装，application 才 `RejectedDefinition/EvaluatorFailure` | `GasDefinitionCatalogValidator.cs:935-1022`；`GasGameplayEffectEvaluator.cs:138-186`；`GasGameplayEffectTransaction.cs:842-846` | `1/0` 与 Clamp `min=10,max=1` 均在 Catalog validation 阶段失败 |
| GE-25 | P1 | GE GrantedTag/GrantedAbility 没有生产链，现有结果由测试手工 seed | GE 无法改变后序 requirement/immunity，也无法证明 GE→grant→remove 三态 | `GasDefinitionCatalogSchema.cs:556-592`；`GasGameplayEffectTransaction.cs:111-162,203-251,281-397`；`RuntimeV1AbilityLifecycleTests.cs:317-337`；`RuntimeV1TickDagPlayModeTests.cs:1006-1023` | 正式 apply 授予 Tag+Ability 的 Duration GE，后序 command 可见；expiry 精确验证三种 removal policy |

### 7.2 Application、Capture、Stack 与 Target 语义

| ID | 等级 | 问题 | 触发/后果 | 核心证据 | 退出门 |
|---|---|---|---|---|---|
| GE-08 | P1 | immunity ownership 反向，rejection 又丢失 blocker provenance | incoming Definition 自己的 `ImmunityRequirementRange` 被用来匹配 target tags；target 上既有 immunity GE 无法阻止 incoming | `GasGameplayEffectTransaction.cs:220-239`；`GasAscLayout.cs:412-444`；`GasGameplayEffectRuntimeTypes.cs:82-105` | X 先建 immunity，Y 后到并匹配；Y `RejectedImmunity`，fact 含 X Definition/Handle/Contributor/matched rule |
| GE-09 | P2 | TargetResolve 提前裁决 TargetLife，并把正常业务拒绝伪装成 stale binding | tick-start 已 Dead 的 AliveOnly application 得到 Session-scope `RejectedStaleBinding/InvalidIdentity`，正确 `RejectedTargetLife` 不可达 | `GasTickJobs.cs:1300-1321,1383-1405,1592-1645`；`GasGameplayEffectTransaction.cs:197-201` | tick-start dead target 必须进 target writer 并产生 target-owned `RejectedTargetLife` |
| GE-12 | P1 | Required SetByCaller/EffectContext 可进 Catalog，Spec/application/evaluator 没有 carrier/consumer | `Required=1` 不检查缺值，payload 不进 magnitude，Level/ParentContext/ActivationId 成为孤儿 | `GasDefinitionCatalogValidator.cs:1024-1075,1181-1224`；`GasTickScratch.cs:360-386`；`GasGameplayEffectRuntimeTypes.cs:47-76`；`GasDefinitionCatalogSchema.cs:246-264` | required `Damage=7`：缺值 typed reject，带值精确写 7，period/reapply 保留 Spec provenance |
| GE-13 | P1 | Modifier `CaptureRange.Start` 被忽略 | 子 range 不在 Definition capture 首段时，`PushCapture 0` 静默读到另一属性 | `GasDefinitionCatalogValidator.cs:786-820`；`GasGameplayEffectTransaction.cs:731-747`；`GasGameplayEffectEvaluator.cs:59-70` | captures `[Attack=10,Armor=3]`，Modifier 仅指 Armor，`PushCapture 0` 必须得 3 |
| GE-14 | P1 | `AggregateByTarget` 被强制带 SourceAsc，实际退化为 AggregateBySource | Source A/B 对同 Target 得到两个 slot，而不是一个共享 stack | `GasDefinitionCatalogSchema.cs:381-398`；`GasDefinitionCatalogValidator.cs:1347-1357`；`GasGameplayEffectTransaction.cs:526-552` | A/B 对 T 各 apply 一次：AggregateByTarget 单槽 stack=2，AggregateBySource 双槽 |

### 7.3 Period、Expiry、Inhibition 与 Attribute

| ID | 等级 | 问题 | 触发/后果 | 核心证据 | 退出门 |
|---|---|---|---|---|---|
| GE-16 | P1 | 首次 application 不评估 OngoingRequirement，会先短暂 Active/执行 body | 新槽直接 `Inhibited=0`；ongoing 初始不满足时 `ExecuteOnApplication` 仍可先造成一次伤害 | `GasGameplayEffectTransaction.cs:197-239,314-397`；`GasGameplayEffectLifecycleUtility.cs:470-535` | 目标缺 ongoing Tag 时直接建 Inhibited，零 modifier/contribution/OnActive |
| GE-17 | P1 | `InhibitedPeriodPolicy.ContinueExecution` 仍被接受和执行 | inhibited Effect 仍修改 Attribute，与目标和 UE 的“抑制时不执行 modifier”语义不符 | `GasDefinitionCatalogValidator.cs:1324-1325`；`GasGameplayEffectLifecycleUtility.cs:222-227`；`GasTickJobs.cs:5447-5455` | 不支持则 bake-fail；支持新语义则先修改 Spec，不得默认当 UE 等价 |
| GE-18 | P1 | Period skip 被发布为 Executed，每次 period 又错增 `ActiveCycleOrdinal` | 消费者无法区分 skip/executed；Cue lifecycle key 每次 period 变化，还可因不相关 ActiveCycle overflow 阻断 period | `GasTickJobs.cs:5747-5765,5808-5843`；`GasGameplayEffectLifecycleUtility.cs:537-557`；`RuntimeV1GameplayEffectLifecycleTests.cs:15-38,75-93` | 两次 period 后 ActiveCycle=0/PeriodOrdinal=2；inhibit→reactivate 才增 cycle；skip 使用独立 outcome |
| GE-19 | P1 | `Stop + RemoveOneStackAndRefreshDuration` 留下下一 Tick 必然非法的 live slot | 首次 expiry 保留 stack 但清零 NextPeriodTick，下次 lifecycle 要求其非零并 fault | `GasGameplayEffectLifecycleUtility.cs:350-362,563-582`；`GasDefinitionCatalogValidator.cs:1302-1330` | 该组合 bake-fail，或定义可被 lifecycle 接受的明确 stopped-live 状态 |
| GE-20 | P1 | Instant Modifier 对 Base 和 Current 同时套用同一 op，绕过 Aggregator 重算 | Base=100、Current=120(+20 buff)，Multiply×2 得 200/240；正确重算应为 200/220 | `GasGameplayEffectTransaction.cs:791-855` | 持久 Add +20 下分别施加 Instant Multiply/Override，Current 保留 contribution 语义 |
| GE-21 | P1 | cost affordability 用原始 delta，提交却经过 clamp，可少付费用 | Current=5、minimum=1、cost=-5 会 Commit 成功但最终 Current=1，只支付 4 | `GasAbilityOwnerPlanUtility.cs:378-407`；`GasAbilityOwnerTransaction.cs:303-367`；`GasAttributeTransactionUtility.cs:266-288,335-344` | 上述向量必须拒绝或全额支付，不得由 clamp 改写费用 |
| GE-22 | P2/Decision | 动态除数为 0 的项目契约未冻结 | GE-07 已覆盖静态 Divide0 bake-fail；当除数来自 capture/runtime 时，目标还需在“typed reject、fault 或定义值”中选定唯一契约 | `GasGameplayEffectEvaluator.cs:138-165`；`GasGameplayEffectTransaction.cs:842-846` | Spec 冻结动态 Divide0 唯一 typed outcome，所有 evaluator/modifier 路径一致且 replay 稳定 |
| GE-26 | P1 | CueRange 被压成一个无 Definition/phase/lifecycle key 的泛化 marker | 不遍历多 Cue，CreatedActive/MergedStack 同 marker，缺 OnActive/WhileActive/Removed、CueDefinitionOrdinal、Handle/Cycle；`InstantExecution` 在 CueRange=0 时也无条件发 marker | `GasTickJobs.cs:6504-6592` | 两 Cue Duration GE 覆盖 apply/stack/inhibit/reactivate/remove，逐 Cue 断言 phase 和完整 CueLifecycleKey |

### 7.4 9203 与 lifecycle fact 特定问题

| ID | 等级 | 问题 | 证据 | 退出门 |
|---|---|---|---|---|
| GE-23 | P1 | 被 Catalog 测试验收的 9203 `ModifierRange.Count == 0`，周期毒伤恒为零 | `RuntimeV1CatalogTests.cs:120-135,400-425,723-751`；`GasGameplayEffectTransaction.cs:558-626`；TickDAG 测试另造一个带 Modifier 的 ad-hoc Catalog | 同一 production Catalog 经 Boundary Apply→首次 period due，Health.Base 精确减少 `SnapshotAttack*0.3*StackCount` |
| GE-24 | P1 | lifecycle transition 先被压成无身份 aggregate count，随后又被生产链 `out _` 丢弃 | `GasGameplayEffectLifecycleUtility.cs:24-35,71-105`；`GasTickJobs.cs:5280-5289,6413-6466`；`GasGameplayEffectRuntimeTypes.cs:79-105` | lifecycle 直接产生每槽 typed transition，Boundary 覆盖 merge/cap reapply/expiry decrement/final remove/inhibit/reactivate，带 handle、old/new stack、reason；仅接回 aggregate 返回值不算完成 |

### 7.5 已从当前问题账本撤下的旧结论

- 旧的 `ExecuteOnApplication` modifier mutation/fact 容量低估在当前树已改为对 EffectOperation 的完整 `ModifierRange.Count` 做保守预留，并叠加 period demand，证据为 `GasTickJobs.cs:2076-2129`。除非找到新的下界反例，不应继续把该旧结论交给修复 Agent。
- 本次对当前树重新核对后，不再将“source-less ApplyEffect 默认 self/fail-open”和“full slab 一定误拒 merge”作为独立 finding；如后续重现，需以当前快照新增问题 ID，不得直接恢复旧口径。

## 8. DOTS DAG、Admission、Boundary 与 Teardown 审查

| ID | 等级 | 问题 | 触发/后果 | 核心证据 | 最小退出门 |
|---|---|---|---|---|---|
| SYS-03 | P1 | Boundary staging 背压会阻断 FaultClose | Core 已 Faulted，outer fence 因 staging rejected 提前抛异常，Gate 仍接受新请求 | `GasTickJobs.cs:3568-3590`；`GasRuntimeWorldOwner.cs:422-433`；`SessionIngressGate.cs:147-172` | 同批 staging failure + admission fault 后，再 accept 必须 typed `IngressClosed`；FinalDrain 失败不阻塞 Gate close |
| SYS-04 | P1 | `Dispose()` 未停止/卸载 gameplay execution domain | caller-owned World 继续 update 时旧 Session 仍跑 gameplay；同 World 重新 install 因旧 Ingress Gate bind 失败 | `GasRuntimeWorldOwner.cs:378-412,469-485`；`GasFixedTickSystems.cs:37-154`；`GasCommandIngressSystem.cs:72-78` | Dispose 后推进 World，Tick/属性/period 全不变；同 World 可重新 install，或明确一次性且彻底销毁 World |
| SYS-05 | P1 | Provision 使用“全 Catalog 最大 maxima × 命令数” | 一个从未使用的 high-max Definition 可使实际仅 1 个 operation 的合法 Tick 永久 fault | `GasTickJobs.cs:343-381,401-474`；`GasStageBSpawnFinalize.cs:447-469` | 按 tick-start handle→Definition lookup 和 per-definition maxima 建 envelope；无关定义不影响结果 |
| SYS-06 | P1 | 任一 Effect/Period 工作无区分地为所有 Running Battle 计提终局 fact | 只处理 Battle A 的非致死 Effect，加入一个无 terminal candidate 且未触碰的 Battle B 后却可超 `MaxCoreFactCount` | `GasTickJobs.cs:2110-2155,6825-6872,6939-7040` | demand 精确覆盖“全量 tick-start terminal candidates + 本 Tick 可能改变成员生命状态的 distinct Battles”，不包含其他无关 Battle |
| SYS-07 | P2 | `MaxOwnerReservationCount/MaxTargetReservationCount` 是未生效逻辑预算 | 两值设 0 时 Commit/ApplyEffect 仍可进 admission 并执行 | `GasRuntimeV1LayoutTypes.cs:151-170`；`GasTickScratch.cs:638-639,743-744`；`GasTickJobs.cs:1996-1999,2512-2610` | 0/边界/超限向量真实门控 owner/target reservation；或删除该假契约 |
| SYS-08 | P1 | Request 幂等与 canonical source order 缺 producer domain | 两个 producer 都从 local sequence 1 开始时，第二个可被误判 duplicate/conflict；caller 可伪造较小 SourceSequence 抢顺序 | `GasBoundaryCommandProtocol.cs:452-499`；`GasSessionLayout.cs:527-552`；`SessionIngressGate.cs:81-84,161-162,304-326`；`GasTickScratch.cs:83-103` | P1/P2 各以 local request/source sequence 1 提交，均接受并按完整 producer tuple 全序；generation 重连可区分 |
| SYS-09 | P1 | Gate ledger 永久保留 full payload，ACK 查询退化为 O(N²) | 即使 outstanding 上限 1，accept→freeze→ack N 次仍保留全部 N 份 payload，每次 ACK 从头线性扫描 | `SessionIngressGate.cs:79-84,719-725,736-758`；`GasBoundaryCommandProtocol.cs:611-662` | 循环 10 万次大 payload，retained full record 受硬上界，ACK 延迟不随累计历史线性增长，过期 key 返回 history-expired |
| SYS-10 | P1 | 当前所谓 Stabilization 只是一次 lifecycle sweep + fact projection，没有 fixed-point 机制 | 当前 GE 尚无 GrantedTag/contribution 生产链，完整两轮/振荡向量暂不可编码；GE-25 接通后，Tag→ongoing→inhibition→contribution→Tag 将确定超出单 pass 能力 | `GasGameplayEffectLifecycleUtility.cs:71-106,503-534`；`GasTickJobs.cs:4665-4685,6347-6501`；`GasDefinitionCatalogSchema.cs:556-591` | 先接通 GE-25；正向两轮链同 Tick 收敛，振荡环产生固定 FaultCandidate 且 target shadow 零 publish |
| SYS-11 | P1 | EffectSpec/Application/ActiveEffect 身份链不完整 | 无 EffectSpecId；bootstrap/runtime ApplicationId 使用两个无 namespace 发号器；EffectLifecycle fact 缺 SlotIndex，无法 round-trip handle | `GasTickScratch.cs:322-385`；`GasTickJobs.cs:1271-1274,1831-1840,6611-6629`；`GasStageBSpawnFinalizeJob.cs:1361-1374,1534-1548`；`GasBoundaryLayout.cs:84-105` | 一 Spec 派生两 target applications，共享 SpecId/不同 ApplicationId；initial/runtime 无碰撞；fact 无损还原完整 ActiveEffectHandle |
| SYS-12 | P1 | AvailableTick 由 caller 控制，不是 Gate 线性化分配 | 同 wall-time 的请求可由外部选 0 或远未来 Tick，制造 pending 孔洞并影响 replay；receipt 无 AssignedAvailableTick | `GasBoundaryCommandProtocol.cs:452-499,505-525,638-662`；`GasCommandIngressSystem.cs:206-234`；`GasTickJobs.cs:97-145` | accept-before-freeze/freeze-before-accept barrier；caller tick hint 不影响结果，receipt 返回 Gate 冻结 tick/hash，retry 复用 |
| SYS-13 | P1 | Payload capacity proof 同时累计漏算与 exact-free reuse 误拒 | 同 target 多 operation 各自用相同 durable high-water 检查，总需求超限仍通过；high-water 已满但有 exact free range 时又 fault | `GasTickJobs.cs:2573-2643,4816-4825`；`GasPayloadRangeAllocator.cs:190-226` | 剩 1 credit 时同 target 两 capture 必须 admission fail；high-water 满+可复用 exact range 必须 admission 成功 |

### 8.1 已复核的正向基础

以下链路在本轮未发现新的独立问题，但不能用来抵消上述缺口：

1. `GasTickDag.cs:114-558` 的 DOTS JobHandle 依赖当前严格串接，Kernel 内未发现新的 `Complete()`。
2. Target preflight→global decision→Target/Terminal/Route/Boundary publish 未发现新的局部 durable publish 绕过。
3. accepted-shell prepass 和 SpawnFinalize ECB producer dependency 已接回 Kernel。
4. Boundary InFlight BatchId/watermark 复用、accept-before-clear 和 late-tail 保留未发现新问题。
5. Tag ancestor closure、Exact/Inclusive count、refcount overflow/underflow 和 presence rebuild 在已实现支持面内未发现新问题。
6. Commit 的 cost/cooldown 重检、contract 冻结与原子 phase/cost/gate 写入与 UE 可迁移核心基本一致。

## 9. 证据、测试与假绿审查

| ID | 等级 | 证据问题 | 为什么不能证明完成 | Canonical 问题 | 退出门 |
|---|---|---|---|---|---|
| EVD-01 | P2 | TB-08 通过手工 seed grant/activation 和内部 `TryBeginWait` | 只证明 slab/protocol 算术，不证明真实 Ability 可到达 Wait | ABL-05/06/07 | 从公开 Boundary 与生产 Catalog 起步，禁止 seed 内部状态 |
| EVD-02 | P2 | 自然 End/Completed 由测试手工写入 | 只证明 cleanup 在预制状态上能跑，不证明 Ability program/lifecycle 会产生 Completed | ABL-08/11/12 | Boundary Activate→Commit→自然 Ended→cleanup 端到端 |
| EVD-03 | P2 | Effect transaction 测试直调 transaction，绕过 Catalog validator/admission/publish | 可用 Runtime 无法安装的 ad-hoc definition 获得绿测 | 独立证据缺口 | 同一 Blob 依次通过 Catalog install、Boundary、admission、production Tick 和 typed facts |
| EVD-04 | P2 | 9203 Catalog fixture 断言零 Modifier，TickDAG fixture 另造有 Modifier Catalog | 两组测试对“9203 是什么”没有共享事实 | CFG-01/GE-23 | 测试只消费 active production package descriptor 对应的唯一 Catalog |
| EVD-05 | P2 | Cue 只断言 generic/nonzero count，AutoChess 只断言最小 count 与重复自洽 | 错 Cue ID、错 phase、错 sequence 或错伤害仍可 Green | CFG-01/08、GE-26 | 按 request/application/effect identity 断言 OnApply/OnActive/Executed/Removed 精确序列和 payload |
| EVD-06 | P2 | 历史 `LastRunEvidenceId` 只校验非空，无法解析到 artifact/hash | 历史 Green 无法独立验证所属快照；当前 runner 重跑结果仍有价值，但不能为历史记录背书 | 独立证据缺口 | EvidenceId 必须解析到 immutable evidence JSON/XML/log/provenance，并重算 hash |
| EVD-07 | P2 | CodeGen validation 与 AutoChess validation 主要是自洽/count oracle | 同一错误 Catalog 重复运行仍可 deterministic；不能证明与策划输入一致 | CFG-05/06/07/08 | 每个关键配置生成精确 golden semantic bytes；运行结果与 package descriptor 绑定 |
| EVD-08 | P2 | 现有测试把裸 SourceSequence、caller AvailableTick、`Consumed=终态`、终身 ledger 写成绿断言 | 测试通过只证明当前实现自洽；证据：`RuntimeV1IngressGateTests.cs:92-104,330-377`、`RuntimeV1TickDagPlayModeTests.cs:91-109` | SYS-01/08/09/12 | 改为 producer domain、Gate-assigned tick、RequestTerminalOutcome 和 bounded history 的 conformance 测试 |

`EVD-01/02/04/05/07/08` 是 canonical 生产问题的证据交叉引用，不重复计入问题或阻断数；`EVD-03/06` 是独立证据治理缺口。

### 9.1 当前证据状态的正确解读

- `TB-01/02/06/07/08` 当前登记为 Green。
- `TB-03/04/05/09/10/11/12/13/14/15/16/17` 为 Pending。
- Green 只在 manifest 登记的 fixture 和当次 provenance 范围内有效。
- TB-08 的 Wait 生产链、payload lifetime 和真实 Ability 可达性未被 Green 覆盖，应拆分向量或降级。
- 未执行的 AutoChess headless、Luban/CodeGen 全链、Profiler、Journaling、Player build 和规模门不得被文档或历史日志代替。

## 10. 关键 intentional deviation 裁决

| 项目 | 建议裁决 | 必须保留的契约 |
|---|---|---|
| Commit 与 End | 保留 `Commit != End` | Commit 只冻结并交付 cost/cooldown/direct work；Ability 以独立 end policy 终止 |
| TargetData | 不作为 Commit 的临时参数 | 必须在 activation/spec/target authority 中冻结并可重放 |
| committed-work-wins | 可保留为项目有意契约 | source 后续死亡/取消不撤销已 Commit 工作，但每个工作仍有唯一 terminal outcome |
| Event/Wait T+1 | 可保留 | 稳定顺序、水位、payload ownership、unsubscribe/ack 必须完整 |
| UE prediction/replication/UObject task | 不迁移 | 不能因为不迁移对象模型，同时丢失 cancel、active count、tag contribution 和 payload lifetime 语义 |
| Cooldown 单 Tag | 需决策 | 选单 Tag 必须修改 Spec 且对多 Tag bake-fail；否则实现 contribution range |
| Divide0 | 需决策 | 生成期和运行期必须只有一个契约 |

## 11. 建议修复分包与依赖顺序

### W0：冻结快照与问题账本

1. 为当前要修复的 dirty snapshot 生成独立 fingerprint。
2. 每个 Agent 只领取本文档的一组 ID，避免同时修改 Catalog、Runtime 和测试 oracle 却产生新双事实源。
3. 修复后必须在本账本记录 replaced/superseded 证据，不得只写“已处理”。

### W1：配置唯一权威与原子发布

覆盖：`CFG-01..08`。

顺序：

1. 先定义 typed semantic graph/support matrix。
2. 删除 gameplay sidecar 静默 override 和手写 Runtime Catalog。
3. 生成 Catalog/lookup/contract/layout/capacity/provenance 全部 candidate artifacts。
4. 重算四 hash 和 artifact byte hash。
5. 通过全量 negative/positive gate 后 atomic promotion。

不要先改 AutoChess 测试期望去迎合当前手写 `-44/9403/9404`，这会继续固化错误权威。

### W2：Catalog/Runtime 契约闭包

覆盖：`GE-02..07`、`GE-13/14/19/25`。

要求生成 lifetime×stack×period×expiry×inhibition×capture 支持矩阵。所有 Catalog 可安装定义都必须能完成其全部合法生命周期；不支持组合必须在 bake/install 前失败。

### W3：Ability 真实执行图

覆盖：`ABL-01..18` 中本文已登记的所有 ID。

建议依赖：

```text
GrantedSpec authority
  -> Ability program/end policy
  -> Activation-owned contribution
  -> Block/Cancel selection
  -> Wait node allocation
  -> Event/OwnedTag deferred reaction
  -> Ending/ack/cleanup ledger
  -> typed lifecycle facts
```

不允许用测试手工 seed activation、continuation 或 Completed 代替生产链。

### W4：Effect/Attribute/Tag 业务闭环

覆盖：`GE-01`、`GE-08..14`、`GE-16..26`、`NUM-01`。

先完成 persistent contributor、application/ongoing/immunity/removal 独立 phase 和 exact typed facts，再闭合 9203。否则“9203 有周期 fact”仍可能只是零伤害假绿。

### W5：Boundary、FaultClose、Teardown 与容量

覆盖：`SYS-01..13`。

重点退出门：

1. 每个 Accepted Request 恰好一个 terminal outcome。
2. FaultClose 不被 Boundary staging 失败阻断。
3. Dispose 后 caller-owned World 零 gameplay 更新。
4. Catalog 中无关 high-max Definition 不影响当前 Tick。
5. 未触达 Battle 不增加当前 Tick 业务 demand。

### W6：真实证据门

覆盖：`EVD-01..08`。

新 Tier-B/master manifest 至少需绑定：

- TestId 和精确 source hash。
- HEAD + tracked/untracked fingerprint。
- package descriptor 四 hash。
- NUnit XML、Unity log、log-health 结果。
- 精确 CommandTrace/SlotLifecycle/FinalState/Fact/Cue sequence。
- AutoChess 配置数值 golden，不是 `count > 0`。
- 失败 candidate active bytes 不变的 negative publish 证据。

## 12. 最小验收向量集

| 向量 | 必须证明 | 关联问题 |
|---|---|---|
| V-CFG-01 | 修改 9201 伤害值后，一次 CodeGen 产生新 ContentHash/artifact hash，Runtime 读到新值 | CFG-01..08 |
| V-CFG-02 | 生成失败或删任一 required artifact 后，active generation bytes/hash 完全不变 | CFG-05..07 |
| V-ABL-01 | 同 wave A 贡献 Tag/block/cancel，B 被 block，C 进入 Ending | ABL-01..03 |
| V-ABL-02 | Boundary Activate→Commit→自然 End→cleanup，无 Cancel/手工 Completed | ABL-08、ABL-11、ABL-12，EVD-02 |
| V-ABL-03 | 真实 Ability program 创建 Wait，跨 ASC event T+1 唤醒，payload 原记录已释放仍保真 | ABL-05..07，EVD-01 |
| V-ABL-04 | 资源不足/cooldown 存活时 CanActivate 零分配；激活 payload 跨 Tick/Avatar rebind 保真 | ABL-13、ABL-18 |
| V-ABL-05 | ASC 在未 Commit/已 Commit/本地 wait/跨 ASC wait 状态下死亡，OwnerTerminal 精确清理且 committed work 保留 | ABL-11、ABL-12 |
| V-GE-01 | 安装每个支持 policy 组合，连续 apply/merge/period/expiry/remove 均不产生 Runtime definition rejection | GE-04..07，GE-19 |
| V-GE-02 | 9203 按生产 Catalog 完成 stack 1→N、period damage、inhibit/reactivate、expiry N→0，数值和 typed facts 精确 | GE-01、GE-16..24 中已登记项 |
| V-GE-03 | 不同 source 对同 target 的 AggregateByTarget 为单槽 stack=2，AggregateBySource 为双槽 | GE-14 |
| V-GE-04 | grant command 与 Duration GE 分别生产带 provenance 的 grant；GE 授予 Tag+Ability 影响后序 requirement/immunity，expiry 按冻结 policy 清理 | ABL-15/16/17、GE-25 |
| V-SYS-01 | staging failure + 同批 Core fault 后 Gate 立即 fail-closed | SYS-01..03 |
| V-SYS-02 | Dispose 后继续 update World，Tick/Attribute/Period 零变化，随后可重新 install | SYS-04 |
| V-SYS-03 | 无关 high-max Definition 和无关 Battle 不改变当前请求 admission 结果 | SYS-05..07 |
| V-SYS-04 | 两 producer 同 local sequence 可共存；AvailableTick 由 Gate 分配；10 万次 ACK 后 payload/history 有界 | SYS-08、SYS-09、SYS-12 |
| V-SYS-05 | 同 target 多 capture 累计预留正确，exact-free range 可复用；Spec/Application/Handle 公开 fact 可无损还原 | SYS-11、SYS-13 |
| V-EVD-01 | 17 个 Tier-B 向量在同一 package descriptor 下全部非 Pending，且无 seed/直调绕过生产链 | EVD-01..08 |

## 13. 审查边界与未执行项

1. 本轮是只读静态审查，没有修改 C#、Unity 资源、Catalog 或测试。
2. 本轮未运行 Unity EditMode/PlayMode、AutoChess headless、Luban、CodeGen、Profiler、Journaling 或 Player build。
3. `git diff --check` 在审查快照下通过，仅有已存在的 LF→CRLF 提示。
4. 工作树在审查期间由其他 Agent 持续修改。处理任一 ID 前，必须先在当前快照重现并重新定位行号，不得根据本文档盲目覆盖新修复。
5. 本文档将“未发现新问题”与“已完成”严格区分。静态审查无法证明没有其他缺陷。

## 14. 交付结论

修复的正确顺序是：

```text
唯一配置权威 / candidate 原子发布
  -> Catalog/Runtime 支持面闭包
  -> Ability 真实执行图
  -> Effect/Attribute/Tag 业务闭环
  -> Boundary/FaultClose/Teardown/容量
  -> 精确 Tier-B + master release evidence
```

在上述 8 个 P0 以及其直接依赖 `ABL-11` 关闭之前，不应进入“只补测试并把 Pending 改 Green”的路线。否则新测试很可能继续为手写 Catalog、不可达内部事务和 count oracle 背书。
