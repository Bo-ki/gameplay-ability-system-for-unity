# Runtime V1 第六轮多 Agent 交叉评审与单轮可运行计划

> 日期：2026-08-30  
> 盘点快照：`HEAD 10cbd25668ecb4650cae53b30d30885b54105742` + dirty worktree（86 tracked dirty / 208 untracked，含本计划）；该计数仅描述盘点时点，不作为并行会话停机条件  
> 状态：已执行完成；最终结果见 [Runtime V1 第七轮收口执行结果](RuntimeV1-第七轮收口执行结果.md)，历史计划与证据继续保留  
> 交付名：`RuntimeV1-Runnable-ClosedWorld`（Development Player 可运行功能基线）  
> 输入：[当前实现与 UE-GAS 语义综合审查](RuntimeV1-当前实现与UE-GAS语义综合审查.md)、[10B-02 技能与 GE Spec](../../方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/10B-AutoChess完整业务案例/10B-02-技能与GESpec.md)、[ADR-0001](../adr/0001-codegen-single-install-root-and-install-envelope.md)；UE 对照源码根为 `E:/Unity/UnityProjects/_Git/Y_GameFramework/Tools/Github/UE-GAS`

## 结论

下一轮只做一件事：**在一个迭代内交付可实际启动、可跑完 AutoChess、且不会接受未实现语义的 Runtime V1 闭世界功能基线。**

为保证这轮真的结束，计划作出以下裁决：

1. 本轮冻结 A/B/C/D 发布硬化链，不做 A1-S、B1-I/B2、C1、D0-M2/D1、E1/E2 或 `FullSemanticEligibility=true`。
2. 本轮只关闭会破坏当前开发运行链的 Runtime 阻断：`SYS-01`、`SYS-02`、最小 `SYS-03`、`ABL-08`、`ABL-12`、当前支持面的 `ABL-11`，以及 `GE-11` 的错误受理。
3. 新增唯一、不可选择的 `RuntimeV1SupportProfile` 规则集，并以两个只读 adapter 消费：normalization 前 raw-authoring scan 与 development bootstrap Blob scan。它不生成默认值、不覆盖 Luban、不充当 backend selector，也不冒充 canonical proof 或 production install admission。
4. `Cancel`、`RemoveEffect` 及超出 one-shot profile 的 Activate/Commit payload 在 Gate 接受前返回稳定 typed `UnsupportedByRuntimeV1Profile`；不分配 RequestKey，不写 journal/inbox/RequestTerminal ledger，gameplay 状态零变化。
5. 9203 本轮实现目标表的数值、stack、period 与 expiry 子集：Target Health `AttributeDelta=-(SnapshotAttack * 0.3 * CurrentStackCount)`、成功 reapply 重置 period 但不刷新 duration、逐层 expiry。当前 generated `-1`、Luban child 9204 和旧测试夹具不是三个可并存的事实源；Tag-driven inhibition 与完整 Cue lifecycle 继续 Pending。
6. 最终硬门是一次 Development Windows Player build + 自动启动；只通过 Editor 编译或 PlayMode 不算“跑起来”。

本轮完成后可声明：**Runtime V1 的受限功能基线在 Development Player 中可运行。** 不得声明正式 Release V1、N2-G0、Player ABI、crash-atomic 物理发布或完整 UE-GAS 语义已经完成。

## 多轮交叉评审记录

### 第一轮：独立审查

三路 Agent 分别从架构、实现和执行计划审查当前状态，形成共同结论：

- ADR 将 `ActiveGenerationRef` 冻结为唯一 selector，但旧计划又要求 tarball 路线使用唯一 `Packages/manifest.json` selector；D0-M2T 可能验证出违反架构合同的“假成功”。
- A/B/C/D 的主体实现、测试和证据大量仍为 untracked；从当前 HEAD 新建 worktree 会丢失领取所需基线。
- A/B 仍为 Red；C0 success token 不可达；D0-M2 未裁决。把它们全部放进下一轮会引入至少六个串行门，无法满足“快、单轮、跑起来”。
- Runtime 侧仍有可直接影响实际运行的 P0：Accepted 无唯一业务终态、终局 cutoff 非线性化、Ability 无 normal End、dead owner 不收口、RemoveEffect 被接受后忽略。

### 第二轮：Fast-Runtime 与 Strict-OneRoute 对抗

三路 Agent 一致否决把 Strict-OneRoute 作为本轮关键路径：它必须经过 selector 可行性、tarball 实验、路线裁决、D1、Z 和故障证据，失败时还会展开 SourceGenerator 分支。

一致选择 Fast-Runtime，并增加诚实边界：

- `FullSemanticEligibility=false` 保持不变。
- development runtime 继续走当前 generated Catalog/bootstrap，不新增 bypass，也不伪造 proof hash。
- D0-M2、完整 Layout/CapacityProof、InstallEnvelope positive admission 和 release fault matrix 整体移动到 V1.1 release-hardening。
- 所有移出的语义必须在运行前稳定拒绝，不能继续“Catalog 可安装、Kernel 再忽略或 fault”。

### 第三轮：红队反例与修正

红队只按“是否会假完成或无法派发”复查，并收口以下十类问题：

1. 当前 AutoChess 战斗直接调用 `RequestApplyEffect`，不经过 Ability。最终门拆成“AutoChess GE/Boundary 黄金链”和独立 public Ability `grant resolver -> Activate RequestTerminal(handle) -> Commit -> normal End/OwnerTerminal` 链。
2. 现有 accept receipt 不足以继续 public Ability 链。J0 新增唯一 request key、RequestTerminal drain/read、bootstrap grant resolver，以及 Activate/ApplyEffect success handle 合同。
3. 只扫描 normalized Blob 会漏掉已被取首项、默认化或丢弃的 authoring 字段。SupportProfile 改为同一规则集的 raw-authoring 与 Blob 两个只读 adapter，并要求真实 raw mutation 反例。
4. 9203 公式依赖的 Source Attack 尚未进入 generated Attribute、unit model 与 Stage-B。Luban xlsx 只权威定义 Attack Attribute identity/layout/约束和 9203 evaluator；每个 unit 的 Attack 初值只权威来自版本化 `autochess.sourcegen.json`，两者经 generator 汇合到 spawn，不能由测试、Runtime 常量或第二默认值补出。
5. 当前 production generated 9203、Luban child 9204 和旧测试夹具语义不同。最终裁决以目标 Spec 的数值/stack/period/expiry 表为权威，用一个紧凑生命周期向量覆盖全部被接受策略；Tag-driven inhibition 与完整 Cue lifecycle 不在本轮完成声明内。
6. R3/R4 原先会争用 generated output、AutoChess validation 与 Unity。现改为 J1 单次转交 generated-output lease，R4 是唯一中央/Unity owner，并用 RunId、build hash 与冻结 source fingerprint 防止旧结果假绿。
7. `AutoChessDemoCodeGenPhase.cs` 只开放 unit scenario/spawn-plan、Catalog plan/Compile 与 capture 专属 writer plumbing；selector/manifest/promotion/install 控制面代码相对 J0 snapshot 无新增变化。Development Player 使用独立 `-gasRuntimeV1Runnable` 入口顺序跑三向量，不与现有 `-gasAutoChessDemo` runner 竞争。
8. 现有 generated `AutoChessGeneratedUnitSpawnPlan` 尚无 Attack；若 R3 先改 `AutoChessGameRoomDefinition` 消费 `plan.Attack`，AutoChess 编译会先失败，Editor CodeGen 也无法启动。最终顺序改为 producer-first：R3 只交付 producer；J1 后 R4 先在旧 consumer 仍可编译时执行唯一一次正式生成，确认新 ABI，再关闭 Unity、修改 consumer 与中央接线，之后禁止再次生成。
9. Attack 的真实 generator 接线点包含 `UnitFields` 与 `WriteStableConstants`，官方入口还会由 `BeanUpdater` 重写 `Datas/__beans__.xlsx`。allowlist 与 R4 process-output lease 均改为精确授权这些 hunk/文件，避免正确实现被误判越界，也不扩大为整个 CodeGen/Datas 写权限。
10. 仅写 RunId/source hash 名称仍可能复用旧结果。J0 冻结确定性 `RuntimeV1RunnableRunManifest-v1`，J2 将唯一生成日志和三类 identity 落入新 run root，J3 的 Edit/Play/Player sidecar 与 PlayerResult 全部绑定同一 manifest，且不新增测试批次。

## 当前实现盘点

### 已有可复用底座

| 领域 | 当前事实 | 本轮处理 |
|---|---|---|
| Catalog 权威 | AutoChess 已改为 generated Catalog；9201=12 golden 已通过 | 保留，不重做 CFG-01 |
| Catalog identity minimum | FNV64 重算与 Resource-only cost gate 已落地 | 保留，不重做 CFG-02/NUM-01 |
| Runtime DAG | Owner/Target shadow、统一 final-publish token、Boundary ring、committed-work-wins 已存在 | 只补缺失 hook，不重构 DAG |
| Ability | Activate/Commit 分离、direct-effect program、cooldown 独立生命周期已有基础 | 增加 one-shot normal End 与窄 OwnerTerminal |
| Effect | Instant、Duration/Period/Stack 基础事务已存在 | 只闭合目标 9203 语义和 production golden |
| Player | `AutoChessRuntimeRunner` 已证明底层 AutoChess battle 可被自动调用，但只跑 scale=50 validation 战斗且无确定性退出 | 只复用底层 battle service，不复用旧 flag/runner/`RunGeneratedScenario(ValidationScenario)`；新增固定 Scale=1 的三向量 orchestrator |

当前仍没有完整 public Ability 消费链：`RequestActivate` 需要 bootstrap grant handle，`RequestCommit` 需要 activation handle，而现有 accept receipt 只返回 request identity。9203 目标公式所需的 Source Attack 也尚未进入 generated Attribute、unit scenario model 和 Stage-B 初始化。两者均是本轮实现项，不能由测试常量或内部状态 seed 代替。

### 本轮必须关闭

| ID | 当前断点 | 本轮最小退出门 |
|---|---|---|
| `SYS-01` | Accepted 只有 transport `Consumed`，没有每 RequestKey 唯一业务终态或公开消费 API | 冻结唯一 request key、RequestTerminal drain/read API；normal/reject/fault/teardown 各恰好一个 typed RequestTerminal，retry 不重复 |
| `SYS-02` | Core 已冻结 Battle 终局，Gate 仍可按旧 authority 接受 tail | `BattleTerminal` 或 `SessionFault` transition 与 Gate cutoff 同一收敛点；普通 RequestTerminal 不关闭 ingress；tail 零 Battle 写 |
| `SYS-03` | staging/drain 失败可在 Gate fault-close 前抛出 | 先进入 `SessionFault` 并 close authority，再报告 drain 错误 |
| `ABL-08` | successful Commit 后 activation 永久存活，public receipt 也拿不到下一步所需 activation handle | public bootstrap-grant resolver/receipt；Activate RequestTerminal 返回 ActivationHandle；one-shot Ability 独立进入 `Completed -> Ending -> Ended`，`WasCancelled=0` |
| `ABL-12` + 窄 `ABL-11` | dead owner 排除后 activation/child 不收口 | 无 wait 的未 Commit/已 Commit 两向量均 `OwnerTerminal`；committed work 保留；child 恰好释放一次 |
| `GE-11` | public RemoveEffect 被接受后 Kernel 忽略 | 本轮不实现 public remove；Gate 前 typed Unsupported、RequestSequence=0、零写 |
| `GE-23` | 9203 三套语义分叉，Source Attack 尚未进入 unit/spawn，当前 generated 仍不是目标公式 | Luban 唯一权威 9203/Attribute 语义，`autochess.sourcegen.json` 唯一权威 per-unit Attack 初值；generator 汇合后进入 layout/spawn；production Catalog 紧凑生命周期 golden |

### 明确移出本轮

- A1-S、B1-I/B2、C1、D0-M2/D1、E1/E2、Z 与 `FullSemanticEligibility=true`。
- Player/IL2CPP ABI proof、AOT stripping、Release Player、跨 OS、断电/fsync、Profiler、Journaling、目标规模长跑。
- Wait/Event/OwnedTag Trigger、public Cancel、activation-owned Tag/block/cancel、granted ability、SetByCaller、Live capture、AggregateByTarget、多 Battle 通用容量。
- 9203 的 Tag-driven ongoing/inhibition/no-catch-up 可达链与完整 Cue lifecycle；`GE-18/24/26`、`TB-05/11` 继续 Pending。
- 完整 Tier-B/master release evidence。未在本轮支持面的向量保持 Pending，不以改状态代替实现。

## RuntimeV1SupportProfile v1

该 Profile 是本轮唯一支持面规则集。实现必须按结构字段判断，不得用 DefinitionId 特判 gameplay 结果；AutoChess 的 ID、数值和公式仍由 Luban/generated Catalog 唯一提供。

同一规则集必须有两个只读 adapter，且共享稳定 RuleId：

1. **Raw-authoring scan**：在 normalization 前读取真实 Luban JSON/typed table 与 sidecar，拒绝会被 normalized row 丢失、取首项或静默默认的字段族。
2. **Bootstrap Blob scan**：对最终 development Catalog 的实际 ranges、enum、maxima 和引用闭包做结构校验。

两者不是两套 Profile。raw scan 不编译 gameplay 语义，Blob scan 不修补 authoring。`RV1-SUPPORT` 必须把当前权威 raw document 复制到仓库外临时目录，在 normalization 前对该真实格式副本施加 mutation 并证明 typed reject；不得原地改写 production 输入，单纯构造 Blob mutation 也不足以证明 ClosedWorld。

Bootstrap Blob scan 不是静态 helper 自证：R4 必须在 `GasStageBBootstrapRecorder.Record` 中，现有 `GasStageBSpawnContract.ValidateBootstrapRequest` 成功之后、`endFixed.CreateCommandBuffer` 之前直接调用同 RuleId 的 `ValidateBlob`。失败映射既有 `GasStageBSpawnFaultReason.ProfileInvalid`，`deferredSession=Entity.Null`、record gate 不提交、零 ECB/registry/gameplay 写。Player 的 `SupportProfileAdmission=Passed` 只能来自这条真实 bootstrap 返回成功，不能由测试或 orchestrator 自行赋值。

Raw-authoring scan 的最低拒绝族为：多 Cue/非本轮 Cue phase、GrantedTag/GrantedAbility、application/ongoing/removal/immunity requirement、Wait/Trigger、enabled cost/cooldown、非 9203 SourceSnapshot capture、SetByCaller/Live、dynamic child/overflow effect、sidecar GE/Ability gameplay semantic override 与未知 enum/range。必须单列检查 `AbilityExecution.Param.IDs`：不是数组、`Count != 1` 或唯一 ID 非正数均在 `FirstPositive` normalization 前 typed reject，不能让多 direct-effect ID 静默取首项。`autochess.sourcegen.json` 的 `units[*].attack` 是本轮明确允许的版本化 scenario spawn stat，不属于 gameplay semantic override。

### Session 与 Boundary

- 单 Session、单 Battle、固定 `Scale=1`、四个 base unit/四 ASC 与当前单 producer。
- `autochess.sourcegen.json` 的现有 `validation.scale=50` 是本轮外 perf 场景，保持原值且不计闭合证据；PlayMode/Player 不得调用 `RunGeneratedScenario(ValidationScenario)` 或旧 scale=50 runner。
- 只有 `Activate`、`Commit`、`ApplyEffect` 可进入 Accepted。
- `Cancel`、`RemoveEffect` 以及非空/非规范 one-shot Ability payload 返回 `UnsupportedByRuntimeV1Profile`。
- `RequestTerminal` 是单请求业务结果；`BattleTerminal` 是战斗终局；`SessionFault`/`OwnerDisposal` 是 Session 级关闭原因。三者不得共用一个含糊的 `terminal` 状态或触发器。
- request key 冻结为 `GasRequestKey(SimulationEpoch, RequestId, RequestSequence)` 公开值；RequestTerminal drain/read API 是 consumer 的单请求结果唯一入口。
- Activate 成功 RequestTerminal 携带 `AbilityActivationHandle`；ApplyEffect 成功 RequestTerminal 携带 `ApplicationId`，仅 active lifetime 携带可选 `ActiveEffectHandle`；OwnerDisposal 为所有已 Accepted 且未终态请求各发布一次 teardown RequestTerminal。
- 每个 Accepted request key 恰好一个 typed RequestTerminal；普通 success/reject RequestTerminal 不关闭 ingress。只有 `BattleTerminal`、`SessionFault` 或 `OwnerDisposal` transition 与 Gate cutoff 同一收敛点；若 RequestTerminal 记录 fault，它只是该 SessionFault 的单请求结果，不是第二个 cutoff owner。

### Ability

- 只接受当前 9101-9104 形状：bootstrap grant、Level=1、MaxConcurrent=1、单 direct-effect node。
- bootstrap 完成后必须通过公开只读 resolver/receipt 取得 `GrantedAbilityHandle`；测试不得扫描 ECS Buffer 或 seed handle。
- 当前实例的 cost/cooldown disabled，requirements/cues empty；这不代表 schema 永久删除这些字段。
- 目标策略只接受 Frozen ASC + RequireSameAvatar + NoSpatial + AliveOnly。
- 无 Ability payload、Wait/Trigger、owned contribution、grant contribution 或 emitted cleanup right。
- successful Commit 后走独立 one-shot normal End；Ability `OwnerTerminal` 只覆盖无 wait 的未 Commit/已 Commit activation 收口，它不是 Session `OwnerDisposal`，不会自行关闭整个 ingress。

### Attribute 与 scenario input

- Attribute layout 只接受 Health、Energy 与本轮新增的 Attack；AttributeId/LayoutIndex 在 J0 冻结。
- Luban xlsx 只定义 Attack 的 AttributeId/LayoutIndex/约束与 9203 evaluator，不保存 per-unit Attack 默认或 override。
- 每个 unit 的 Attack 初值只来自版本化 `EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/AutoChessDemo/autochess.sourcegen.json` 的 `units[*].attack`，经 generated unit model 与 Stage-B bootstrap 初始化；Runtime 不得按 DefinitionId、Luban 默认或常量补出 Attack。
- 9203 application 捕获当次 Source Attack；之后直接修改 source Attack 不影响旧 period，只有下一次成功 reapply 才替换 snapshot。

### GameplayEffect

- 9201：Instant Health `-12`；9202：Instant Health `-8`。
- 9207：Instant pure evaluator，严格按目标 Spec 的 missing-health 公式和 ValueView 执行。
- 9203：
  - `StackKey=(Definition,TargetASC,SourceASC)`，`AggregateBySource`，limit=3，ReplaceLatest。
  - 每次成功 application 在 `GasCapturePhase.SourceSpecProjection` 捕获 Source Attack Snapshot。
  - root inline modifier 的输出固定为 Target Health；Luban authoring operation=`Subtract`（当前 code=3），generated Runtime 表达为 `GasModifierOperation.Add` 加 evaluator 末尾 `Negate`。
  - evaluator 输入固定为 Attack capture，period `AttributeDelta = -(SnapshotAttack * 0.3 * CurrentStackCount)`；不得把唯一 Attribute 同时当 capture 输入和 modifier 输出，也不得产生加血。
  - Duration=8、Period=2、ExecuteOnApply=false。
  - reapply 不刷新 duration；成功 reapply 与 cap reapply 都替换 payload/provenance 并重置 period。
  - Due=End 时 period 先于 expiry。
  - expiry 为 `GasExpiryPolicy.RemoveOneStackAndRefreshDuration`，并使用独立 `GasExpiryPeriodPolicy.Reset`；剩余 stack 刷新 duration 并重置 period，最后一层 Remove。
  - period kill 归因 latest successful application。
- legacy 9204 从目标 authoring/generated Catalog 删除，不保留运行 fallback。
- 除 9203 的 Source/Snapshot capture 外，不接受其他 Capture；不接受 SetByCaller、Live、GE direct-child、requirement/immunity/granted Tag/Ability。
- 仅接受 required StableAsc TargetData 与冻结的 Causality/SourceAvatarGeneration/TargetAvatarGeneration context。

### Cue 与 Fact

- 本轮 Cue 只验证当前受限支持：9201/9202/9207 的 9301 `Executed` 与 9203 首次 active 的 9301 `OnActive`。
- 9203 `WhileActive/period Executed/Removed` 及完整 lifecycle key 不在本轮完成声明内，继续由 `GE-18/24/26`、`TB-11` 跟踪。
- 测试必须断言 DefinitionId、ApplicationId/ActiveEffectHandle、受支持 phase、AttributeDelta、RequestTerminal result 与 causality；禁止以 `count > 0` 代替语义。

## 单轮执行计划

### J0：冻结接口与写集

J0 只做一次短会合，不建设新框架：冻结 SupportProfile v1 raw/blob RuleId 与 bootstrap `ProfileInvalid` 映射、`GasRequestKey`、RequestTerminal DTO/drain API、`BattleTerminal/SessionFault/OwnerDisposal` cutoff 术语、bootstrap grant resolver、Activate/ApplyEffect RequestTerminal handle、Ability normal-end/OwnerTerminal hook、Attack AttributeId/LayoutIndex 与 unit spawn-plan 字段、9203 的 Attack capture/Health output/CaptureDescriptor 字段及含 `Scale/AscCount` 的 Player result schema。J0 同时生成 `BaselineId`，记录每条独占写集与所有冻结路径的文件存在性、byte SHA；后续只与该 snapshot 比较，不用全局 dirty 数量或 HEAD diff 判断越界。之后四条链按下表执行。

`EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/__beans__.xlsx` 是官方入口会重写的 process output：J0 记录其初始 SHA，R1-R3 禁止修改；J2 将它纳入 `ProducerFingerprint` 并记录 official pipeline 的 pre/post SHA。该授权只覆盖这个精确文件，不扩展到其它 `Datas/**`。

J0 同时冻结最小 `RuntimeV1RunnableRunManifest-v1`，不建设通用 evidence 框架。三类 identity 共用 `InventorySha256-v1`：展开冻结的精确路径/递归目录，repo 文件编码为 `F<TAB>/<repo-relative-path><TAB>exists<TAB>lowercase-sha256-or-dash<LF>`；外部实际工具编码为 `T<TAB>role<TAB>normalized-absolute-path<TAB>file-version<TAB>lowercase-sha256<LF>`。全部 `/` 规范化并按整行 ordinal 排序，整体 UTF-8 no-BOM 后再取 SHA-256。目录只纳入 regular files，符号链接直接拒绝。

- `ProducerFingerprint` 在唯一正式生成前计算：`Assets/**` 下全部 `.cs/.asmdef/.asmref/.dll` 及其现存 `.meta`，`EX_GAS_Config/ProjectConfigTable/exgas_config/Defines/**`、`Datas/**`，`ProjectSettings/GASSettingAsset.asset`、`ProjectSettings/ProjectVersion.txt`、`Packages/manifest.json`、`Packages/packages-lock.json`，以及实际启动的 Unity/CLI/wrapper 的规范化路径、file version 与 binary/script SHA-256。
- `GeneratedArtifactIdentity` 在唯一正式生成后计算：精确 `Datas/__beans__.xlsx`、`Assets/DataGenerated/Luban/**`、`Assets/GAS/Generated/**`、`Assets/AutoChessDemo/Generated/**`、`ProjectSettings/GasCodeGen/**` 的独立 byte inventory；不得只信任生成物内自报的 manifest/ref hash。
- `FinalSourceFingerprint` 在 post-generation consumer 接线完成后计算：按 Producer 的 repo inventory 规则重算最终 bytes，再加入本次 Player 的精确 build scene 列表、对应 scene/meta、`ProjectSettings/EditorBuildSettings.asset`，并绑定 `ProducerFingerprint` 与 `GeneratedArtifactIdentity`。明确排除 `Library/Temp/Logs/obj/TestResults` 和 Player build output。

精确公式为：`ProducerFingerprint=SHA256(producer-lines)`；`GeneratedArtifactIdentity=SHA256(generated-lines)`；`FinalSourceFingerprint=SHA256("P<TAB>"+ProducerFingerprint+"<LF>G<TAB>"+GeneratedArtifactIdentity+"<LF>"+final-repo-lines)`。所有 SHA-256 digest 使用完整 64 个 lowercase hex 字符（256-bit，禁止截断）；`RunManifestSha256` 则直接对最后一次原子写入的 manifest bytes 取 SHA-256，不依赖 JSON 属性顺序。

R4 只实现一个共享的确定性 inventory/provenance helper，不为该合同增加 NUnit 用例或抽象框架。

“冻结 A/B/C/D”特指 proof/install/selector/promotion release 控制面代码与合同。R3 对 `AutoChessDemoCodeGenPhase.cs` 的临时例外只限三组：unit scenario/spawn-plan 的 `UnitFields`、`WriteGeneratedUnitSpawnPlan`、`WriteBaseUnitSpawnPlans`、`WriteUnit`，以及 `WriteStableConstants` 中仅新增 `AttributeAttack` 的 hunk；`AutoChessRuntimeV1CatalogModel` 对应的 `CatalogEmissionPlan/Compile*` 及 emission-plan carrier；capture 专属的 `WriteCatalogSourceFactory`、`WriteEffectFactory`、`WritePayloadFactories`、`WriteModifierFactory` 和新 `WriteCaptureFactory` 中仅 `CaptureDescriptors/CaptureRange/MaximumCaptureDescriptorCount` 相关 hunk。相对 J0 snapshot 的新增 hunk 必须全部落在这些授权模型/方法内，`Execute`、output root、manifest、descriptor、candidate、generation、promotion、selector 与 install 相关代码不得产生新变化。盘点前已有 dirty 内容原样保留，不回滚也不冒充本轮 diff。R4 只执行现有官方 pipeline，由此产生的 development generation-store 状态变化属于输出 lease，不授权修改控制面代码或领取 D0-M2/D1。

| 链 | Owner 与目标 | 独占写集 | 禁止事项 |
|---|---|---|---|
| `R1-Boundary` | `SYS-01 -> SYS-02/SYS-03 -> profile command/payload reject` | `Assets/GAS/Runtime/V1/Boundary/**`、`Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs`、`Assets/GAS/Runtime/V1/Layout/GasBoundaryLayout.cs`、`Assets/_Test/RuntimeV1/EditMode/RuntimeV1RunnableBoundaryContractTests.cs` | 不改 `GasTickJobs.cs`、Ability、Catalog |
| `R2-Ability` | `ABL-08 -> ABL-12 + 窄 ABL-11` | `Assets/GAS/Runtime/V1/Ability/**`、`Assets/_Test/RuntimeV1/EditMode/RuntimeV1RunnableAbilityContractTests.cs` | 不改 Gate、Catalog、`GasTickJobs.cs` |
| `R3-Definition-Effect` | raw/blob SupportProfile、Luban Attribute/9203、scenario Attack、unit/Catalog/capture semantic emission 与 effect helper | `EX_GAS_Config/ProjectConfigTable/exgas_config/Defines/builtin.xml`、`EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/#exgas.attributeSet.xlsx`、`EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/#exgas.gameplayEffect.xlsx`、`EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/AutoChessDemo/autochess.sourcegen.json`、`Assets/GAS/Editor/CodeGen/Core/AutoChessDemoConfigModel.cs`、新建 public `Assets/GAS/Editor/CodeGen/Core/RuntimeV1RawAuthoringSupportProfileAdapter.cs`、`Assets/GAS/Editor/CodeGen/Phases/AutoChessDemoCodeGenPhase.cs` 的 `UnitFields`、unit spawn-plan writers、`WriteStableConstants` 单个 `AttributeAttack` hunk、Catalog plan/Compile、emission-plan carrier 与 capture 专属 writer 授权方法、`Assets/GAS/Runtime/V1/Definition/GasDefinitionCatalogValidator.cs`、新建 `Assets/GAS/Runtime/V1/Definition/GasRuntimeV1SupportProfile.cs`、`Assets/GAS/Runtime/V1/Effect/**`、新建 public `Assets/AutoChessDemo/Integration/GasCore/AutoChessRuntimeV1CatalogAccess.cs`、`Assets/_Test/RuntimeV1/EditMode/RuntimeV1CatalogTests.cs`、新建 `RuntimeV1RunnableSupportProfileTests.cs`/`RuntimeV1RunnableCatalogTests.cs`、`Assets/_Test/RuntimeV1/EditMode/com.exhard.exgas.runtimev1.tests.editmode.asmdef` | EditMode asmdef 只新增 `com.exhard.exgas.editor`/`com.exhard.exgas.autochessdemo` 直接引用；禁止 reflection；production Catalog test 必须经独立 public access 消费实际 generated Blob；R3 的 consumer/test 不得静态引用尚未生成的 Attack ABI；generated output、GameRoom consumer、runner 和中央 Kernel 不属于本链 |
| `R4-Kernel-Player` | generation-first ABI 交接、唯一中央集成、Player orchestrator 与最终验证 | `Assets/GAS/Runtime/V1/System/GasTickJobs.cs`、`Assets/GAS/Runtime/V1/System/GasTickDag.cs`、`Assets/GAS/Runtime/V1/System/GasTickScratch.cs`、`Assets/GAS/Runtime/V1/System/GasStageBBootstrapRecorder.cs`、`Assets/GAS/Runtime/V1/System/GasStageBSpawnFinalizeJob.cs`、`Assets/AutoChessDemo/GameRoom/AutoChessGameRoomDefinition.cs`、`Assets/AutoChessDemo/Integration/GasCore/AutoChessGasBattleEntityLifecycle.cs`、`Assets/AutoChessDemo/Presentation/AutoChessDemoSceneRunner.cs`、official Luban/CodeGen process-output lease（精确文件 `EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/__beans__.xlsx` 及 `Assets/DataGenerated/Luban/**`、`Assets/GAS/Generated/**`、`Assets/AutoChessDemo/Generated/**`、`ProjectSettings/GasCodeGen/**`）、`Assets/_Test/RuntimeV1/PlayMode/RuntimeV1Runnable*`、`Assets/_Test/RuntimeV1/PlayMode/com.exhard.exgas.runtimev1.tests.playmode.asmdef`、`Assets/AutoChessDemo/AutoRunner/RuntimeV1RunnablePlayerOrchestrator.cs`、`Assets/AutoChessDemo/Battle/Validation/RuntimeV1Runnable*`、`Assets/AutoChessDemo/Editor/RuntimeV1RunnablePlayerBuilder.cs` | 先正式生成并确认 Attack ABI，关闭 Unity 后才可写 consumer；`__beans__.xlsx` 禁止手改，只允许唯一官方 pipeline 重写并记录前后 SHA；PlayMode asmdef 只新增 `com.exhard.exgas.autochessdemo` 直接引用；其他链不得修改中央/生成/runner 文件；不改 generation-store/promotion/selector 实现或合同，不把运行输出计作 D0-M2/D1 证据 |

### J1：三组 helper API 冻结

前三条链先完成各自纯合同、utility 与隔离测试。J1 只检查：

1. API 是否足以让 Kernel 接线，且没有第二份状态机。
2. 写集是否互斥，是否误改 frozen A/B/C/D 或中央文件。
3. 9203 是否由同一 Luban row 一路生成，是否仍残留 9204 fallback；四个 unit 的 Attack 是否均为显式非默认值，并可追溯到 `autochess.sourcegen.json`。
4. 相对 J0 snapshot，`GasCodeGenPipeline`、Candidate/GenerationStore/PackageDescriptor、CLI 不得有本轮新增源码变化；`AutoChessDemoCodeGenPhase` 的新增 hunk 必须全部位于 `UnitFields`、unit spawn-plan writers、`WriteStableConstants` 的单个 `AttributeAttack`、Catalog plan/Compile/emission carrier 或 capture 专属 writer 授权方法，capture writer hunk 只能增加 Captures/CaptureRange/maxima 接线。禁止按 HEAD diff 回滚盘点前已有变化。
5. R3 停止写入后，将精确 `Datas/__beans__.xlsx`、Luban/DataGenerated/AutoChess/`ProjectSettings/GasCodeGen` process-output lease 一次性交给 R4；Player 结果完成前不得交回。该 lease 只授权现有官方入口的运行输出，`__beans__.xlsx` 禁止手改且必须记录 pre/post SHA，不授权编辑 D 控制面源码或合同。
6. EditMode test asmdef 直接引用 Editor 与 AutoChess；raw adapter 可直接调用且未用 reflection；production Catalog test 通过独立 public access 读取实际 generated Blob，而不是复制 fixture，且 R3 代码/测试没有静态引用尚未生成的 Attack 成员。

J1 通过后只允许 `R4-Kernel-Player` 执行正式生成、修改 generated-ABI consumer 与中央 Kernel。

R4 可在 J1 前并行准备不启动 Unity 的 result DTO、精确 test filter 与 Player launcher，但这些准备不得静态引用尚未生成的 Attack ABI；J1 门控 generation-first ABI 交接、中央 Kernel 接线和 Unity lease。

### J2：generation-first ABI 交接与一次中央集成

J2 开始先创建唯一 `RunId` 与全新的 `TestResults/RuntimeV1Runnable/<RunId>/`；目录已存在即失败。在 R1-R3 冻结后，R4 原子写入初始 `RunManifest.json`：schema、`BaselineId`、canonical inventory specs/records、`ProducerFingerprint`、实际 Unity/tool identity、唯一正式生成的完整 argv 与 `Generation.log` 路径。`R4` 必须按以下顺序执行，不得互换：

1. 保持旧 `AutoChessGameRoomDefinition` consumer 不变，冻结全部源码，通过现有官方入口执行本轮唯一一次 Luban + AutoChess CodeGen，并把 stdout/stderr/Unity log、exit code 写入该 RunId；只允许 pipeline 重写精确 `Datas/__beans__.xlsx` 和既定生成/output lease，并更新 `ProjectSettings/GasCodeGen/**` generation/ref/intent 运行状态，不得手改这些输出、修改控制面代码或把结果计作 D0-M2/D1 证据。
2. 记录 `__beans__.xlsx` pre/post SHA，确认 generated unit spawn-plan 已具备 Attack ABI、四个 unit Attack 可追溯、9203 目标子集与 capture 已进入实际 Catalog、9204 缺席；独立计算 `GeneratedArtifactIdentity`，把官方 manifest/ref 值仅作为旁证，并原子更新 `RunManifest.json` 的 generation argv/exit/log SHA 与 output inventory。然后等待生成进程完全退出并关闭 Unity；失败时停机，不得先改 consumer 绕过生成。
3. 由 R4 独占修改 `AutoChessGameRoomDefinition`、Stage-B、Boundary/Ability/Effect 三组 Tick DAG hook、PlayMode/Player consumer；不重排现有 phase，不顺手重构 8k 行 `GasTickJobs.cs`，且不得再次执行生成。
4. 在 `GasStageBBootstrapRecorder.Record` 的现有 contract validation 成功后、创建 ECB 前执行 `ValidateBlob`，profile reject 返回 `ProfileInvalid` 且零写。
5. PlayMode test asmdef 直接新增 AutoChess 引用，不用 reflection 或复制 battle 实现；`AutoChessDemoSceneRunner` 在 `-gasRuntimeV1Runnable` 模式下必须在其 `Start` 发起旧 `runOnStart` 战斗前直接返回，新 orchestrator 是该进程唯一三向量/`Application.Quit` owner，普通无 flag 场景行为不变。

任何需要新增通用框架的请求先退回最小 helper。

### J3：最小验证与交付

只执行以下四个批次；不做双跑、全历史套件或额外停顿审查。J3 开始时计算 post-generation consumer 已接线后的 `FinalSourceFingerprint` 与 R1-R4 写集 SHA，并原子更新同一个 `RunManifest.json`，绑定 J2 的 `RunId`、`ProducerFingerprint`、`GeneratedArtifactIdentity`；禁止复用旧 XML/log/result。每个批次前后必须重算 `FinalSourceFingerprint` 与 `GeneratedArtifactIdentity` 并保持不变：

1. **静态门（不再生成）**：确认 `RunManifest.json` 的 generation log/exit/hash 完整、J2 `GeneratedArtifactIdentity` 未变化、generated Attack ABI 与四 unit Attack 可追溯、9203 目标子集、9204 缺席、raw mutation 在 normalization 前拒绝、最终 Blob profile 通过；禁止再次执行 Luban/CodeGen。
2. **EditMode 单批**：唯一 category=`RuntimeV1Runnable`，输出 `EditMode.xml`/`EditMode.log`；SupportProfile raw/blob、Cancel/Remove/payload Unsupported、typed RequestTerminal contract、9201/9202/9207 evaluator。XML 必须 fresh、`tests > 0`、0 failed/0 skipped，并包含 J0 冻结的全部 required TestId。批次结束原子写 `EditMode.provenance.json`：同一 RunId/三 identity、完整 filter/argv/exit code、XML/log SHA。
3. **PlayMode/headless 单批**：同一 category，输出 `PlayMode.xml`/`PlayMode.log`；真实 bootstrap Blob profile pass/reject（reject=`ProfileInvalid`、零 ECB）、public Ability normal End/OwnerTerminal、SYS-02/03 cutoff、9203 数值/stack/period/expiry 子集、AutoChess `Scale=1/AscCount=4` 的四 base-unit `GasRuntimeSession` 终局。不得调用旧 `RunGeneratedScenario(ValidationScenario)`。XML 使用与 EditMode 相同的非零/required-TestId 门，并原子写同字段的 `PlayMode.provenance.json`。
   `BuildHash` 使用同一 inventory 算法，以 `/` 形式 build-root-relative path 编码 Player output 全部 regular files 后取 SHA-256。
4. **Development Player 单次**：StandaloneWindows64，沿用当前 scripting backend；`BuildHash` 是 Player output regular files 的 canonical inventory SHA-256，build 完成后原子写入 `RunManifest.json`。新增非 NUnit `[RuntimeInitializeOnLoadMethod]` Player orchestrator，以独立 `-gasRuntimeV1Runnable` 开关顺序执行 public Ability、production 9203、AutoChess `Scale=1/AscCount=4` 三向量，原子写 `PlayerResult.json`，顶层捕获异常并 `Application.Quit(0/1)`。现有场景 `AutoChessDemoSceneRunner` 必须因该 flag 在 `Start` 旧战斗前返回，不能与 orchestrator 并发或抢先退出。launcher 先确认 run root 下不存在旧 `Player.log/PlayerResult.json`，再以 `-batchmode -nographics -logFile <runRoot>/Player.log -gasRuntimeV1Runnable -gasRuntimeV1RunnableRunId <RunId> -gasRuntimeV1RunnableManifest <absolute RunManifest.json> -gasRuntimeV1RunnableManifestSha256 <sha256>` 启动；不传现有 `-gasAutoChessDemo`，不调用旧 scale=50 runner，只持有并在超时后终止自己创建的 PID。Player 从指定 manifest 读取身份字段；launcher 在退出后独立重算 manifest、build、final source 与 generated identity。exit 0、fresh 日志健康、result 上述全部身份字段匹配才通过，并原子写 `Player.provenance.json`。

建议上限：EditMode 20 分钟、PlayMode 30 分钟、Player build 45 分钟、Player 运行 10 分钟；超时为失败，不自动扩大范围。Player 模块或许可证缺失属于真实阻断；不得用 Editor 结果冒充通过。该门只证明 Development Player 可运行，不证明 IL2CPP/AOT/Release。

`PlayerResult.json` 必须分别记录：`RunId`、`ProducerFingerprint`、`GeneratedArtifactIdentity`、`FinalSourceFingerprint`、`BuildHash`、实际读取的 `RunManifestSha256`、来自真实 `GasStageBBootstrapRecorder.Record` 成功返回的 `SupportProfileAdmission=Passed`、`ProductionInstallAdmission=NotEvaluated`、`DeclaredFullSemanticEligibility=false`、`Scale=1`、`AscCount=4`、Ability/9203/AutoChess 结果与最终 semantic/state hash。不得由 orchestrator 静态写死 profile pass，也不得只输出一个容易被误读为已执行 install admission 的 `FullSemanticEligibility=false`。

J0 冻结的 required TestId 至少包含以下七项；实现不得以改名漏跑来减少集合：

- `GAS.RuntimeV1.Tests.EditMode.RuntimeV1RunnableSupportProfileTests.RawAndBlobProfile_RejectUnsupported`
- `GAS.RuntimeV1.Tests.EditMode.RuntimeV1RunnableBoundaryContractTests.UnsupportedCommandsAndPayloads_DoNotAccept`
- `GAS.RuntimeV1.Tests.EditMode.RuntimeV1RunnableCatalogTests.ProductionCatalog_MatchesClosedWorld`
- `GAS.RuntimeV1.Tests.PlayMode.RuntimeV1RunnableBoundaryPlayModeTests.AcceptedRequests_PublishOneRequestTerminalAndCloseOnSessionFault`
- `GAS.RuntimeV1.Tests.PlayMode.RuntimeV1RunnableAbilityPlayModeTests.PublicActivateCommit_NormalEndAndOwnerTerminal`
- `GAS.RuntimeV1.Tests.PlayMode.RuntimeV1RunnableGameplayEffectPlayModeTests.Production9203_NumericStackPeriodExpiry`
- `GAS.RuntimeV1.Tests.PlayMode.RuntimeV1RunnableAutoChessPlayModeTests.StandardSession_ReachesExactBattleTerminal`

## 最小验收向量

| Vector | 必须证明 |
|---|---|
| `RV1-SUPPORT` | 同一 RuleId 矩阵的 raw-authoring 与 Blob adapter 均通过；每个排除字段族至少一个权威 raw document 临时副本 mutation 在 normalization 前 typed reject，必须包含 `AbilityExecution.Param.IDs` 的非数组/0/多项/非正 ID；production 输入 bytes 不变；真实 Stage-B bootstrap 对 unsupported Blob 返回 `ProfileInvalid` 且零 ECB |
| `RV1-BOUNDARY` | invalid Definition 的 Accepted request 恰好一个 RequestTerminal reject 且 ingress 保持可用；SessionFault/staging fault、BattleTerminal 或 OwnerDisposal 后 Gate 立即关闭；Cancel/Remove/非法 payload 从不 Accepted；dispose 补齐未终态 teardown RequestTerminal |
| `RV1-ABILITY` | public grant resolver -> `Activate RequestTerminal(handle) -> Commit -> Completed -> Ended`，无 Cancel/seed/ECS 扫描；dead owner 的未 Commit/已 Commit 两向量收口，committed work 保留 |
| `RV1-9203` | scenario Attack 初始化；capture=Source Attack、modifier output=Target Health、每次 period `AttributeDelta=-(SnapshotAttack*0.3*StackCount)`；同/异 source、1/2/3/cap、latest snapshot、旧 snapshot 不受 source 后改值影响、成功 reapply 才换 snapshot、period reset/no duration refresh、period-before-expiry、逐层 expiry、kill provenance 全精确 |
| `RV1-AUTOCHESS` | 固定 Scale=1、四 base unit/AscCount=4；production Catalog 下 9201=12、9202=8、9207 精确公式、当前受支持 9301 phase/identity、战斗 winner/result/state hash 精确；scale=50 perf 场景不执行、不计证据 |
| `RV1-PLAYER` | Development Player 自动启动以上链，输出机器结果并 exit 0 |

## 并行执行规则

盘点时工作树包含 294 项变化，且 A/B/C/D 关键基线大量未跟踪。下一轮默认所有会话使用当前 checkout，不新建 worktree；否则必须先由用户授权做依赖闭合 checkpoint。该总数只用于说明不能从 HEAD 重建现状，不是会话开始/继续条件。

- 所有会话以 J0 `BaselineId` 为共同起点，只校验自己独占写集和冻结路径的存在性/byte SHA；允许其他链的独占写集变化，不得因全局 dirty 计数变化停机或回滚。
- 每条链同时拥有其新增 Unity 文件对应的 `.meta`，必须在 J1 前一并交付；不得等待 R4 启动 Unity 后由导入器跨写集补齐。
- root 只调度；唯一命名的 `R4` owner 持有 Unity lease 与 J1 后的 official-generation output lease。R4 启动 Unity 前，R1-R3 必须全部 idle、写集冻结且确认没有既有 Editor/Unity 进程；正式生成期间全部源码冻结。生成进程退出且 Unity 关闭后，只允许 R4 应用计划内的 post-generation consumer/中央 hunks，R1-R3 持续 idle，生成物不得再写；J3 冻结 `FinalSourceFingerprint` 后直至 Player 结果完成，任何会话都不得写源码或生成物。
- 前三链只做非竞争的静态/独立 .NET 检查；R3 如需试生成，只能写仓库外临时目录。
- 任一链需要中央文件改动时只提交 hook 需求，不直接修改。
- 不创建兼容 fallback、feature flag、第二 SupportProfile 或第二 gameplay authoring 源。
- 不因测试失败扩大实现范围；先判断输入是否应由 SupportProfile 拒绝。

## 可复制任务提示词

### R1-Boundary

> 在绝对项目根 `E:/Unity/UnityProjects/_Git/gameplay-ability-system-for-unity` 的 saved-project local/current checkout 执行，禁止新建 worktree；先读取本权威计划，以 J0 `BaselineId` 核对本链独占写集与冻结路径 SHA，不按全局 dirty 计数判断，校验不符立即停止。执行 `RuntimeV1-Runnable R1-Boundary`：只修改 Boundary/Session 独占写集，冻结 `GasRequestKey(SimulationEpoch,RequestId,RequestSequence)`、RequestTerminal drain/read API、bootstrap grant resolver/receipt、Activate/ApplyEffect success handles 与 teardown RequestTerminal；关闭 SYS-01、SYS-02 和最小 SYS-03。普通 RequestTerminal 不关闭 ingress，只有 BattleTerminal/SessionFault/OwnerDisposal transition 与 Gate cutoff 同一收敛点。Cancel/RemoveEffect/非法 one-shot payload 在 Gate 分配 key 前返回 `UnsupportedByRuntimeV1Profile`，保证 seq=0、零 journal/inbox/RequestTerminal/gameplay 写。不得修改 GasTickJobs、Ability、Catalog 或 release 控制面。交付 helper API、针对性测试、写集 diff 与 Kernel hook 需求，不启动 Unity。

### R2-Ability

> 在绝对项目根 `E:/Unity/UnityProjects/_Git/gameplay-ability-system-for-unity` 的 saved-project local/current checkout 执行，禁止新建 worktree；先读取本权威计划，以 J0 `BaselineId` 核对本链独占写集与冻结路径 SHA，不按全局 dirty 计数判断，校验不符立即停止。执行 `RuntimeV1-Runnable R2-Ability`：只修改 Runtime/V1/Ability 与专属合同测试，实现当前 cost/cooldown-disabled one-shot 支持面的 normal End、OwnerTerminal 和窄 ABL-11。successful Commit 的工作先冻结，再独立 Completed->Ended，WasCancelled=0；dead owner 未 Commit/已 Commit 均只收口一次，committed direct work 保留。Wait/Trigger/owned contribution 不实现，由 SupportProfile 拒绝。不得修改 Gate、Catalog、GasTickJobs 或 release 控制面；交付 helper API 与 Kernel hook 需求，不启动 Unity。

### R3-Definition-Effect

> 在绝对项目根 `E:/Unity/UnityProjects/_Git/gameplay-ability-system-for-unity` 的 saved-project local/current checkout 执行，禁止新建 worktree；先读取本权威计划，以 J0 `BaselineId` 核对本链独占写集与冻结路径 SHA，不按全局 dirty 计数判断，校验不符立即停止。执行 `RuntimeV1-Runnable R3-Definition-Effect`：只修改 authoritative xlsx/builtin、`Datas/AutoChessDemo/autochess.sourcegen.json`、AutoChess config model、`AutoChessDemoCodeGenPhase` 的 `UnitFields`/unit spawn-plan writers、`WriteStableConstants` 单个 `AttributeAttack` hunk、Catalog plan/Compile/emission carrier 与 capture 专属 writer 授权方法、Definition profile/validator、Effect helper、独立 public `AutoChessRuntimeV1CatalogAccess`、RuntimeV1 EditMode tests/asmdef；capture writer 只接 `CaptureDescriptors/CaptureRange/MaximumCaptureDescriptorCount`。用同一 SupportProfile RuleId 做 normalization 前 raw scan 与 bootstrap Blob scan；raw adapter 必须可被加了 Editor/AutoChess 直接引用的 test asmdef 调用，禁止 reflection，production golden 必须经独立 access 读取实际 generated Blob。Luban xlsx 只权威定义 Attack Attribute identity/layout/约束和 9203 evaluator，四个 unit 的 Attack 初值只权威来自 `autochess.sourcegen.json` 中的显式非默认 `units[*].attack`，禁止第二默认/override。9203 root modifier 输出 Target Health，authoring Subtract/code3 编译为 Add+evaluator Negate；输入为 Source Attack Snapshot，最终 period `AttributeDelta=-(SnapshotAttack*0.3*StackCount)`，并实现 Never duration refresh、successful reapply period reset、`RemoveOneStackAndRefreshDuration + Reset`，删除 legacy 9204；不按 DefinitionId 在 Runtime 特判。R3 不得修改 `AutoChessGameRoomDefinition`，其 access/测试也不得静态引用尚未生成的 Attack 成员；不得修改 production generated output、AutoChess runner/validation、GasTickJobs 或 D selector/manifest/promotion/install 区域；试生成只写仓库外临时目录，不启动 Unity。交付 producer、唯一正式生成需求、public profile/helper API 与 Kernel hook 需求。

### R4-Kernel-Player

> 在绝对项目根 `E:/Unity/UnityProjects/_Git/gameplay-ability-system-for-unity` 的 saved-project local/current checkout 执行，禁止新建 worktree；先读取本权威计划，以 J0 `BaselineId` 核对本链独占写集与冻结路径 SHA，不按全局 dirty 计数判断；启动时确认 official-generation output lease 仍属 R3，J1 后再次确认精确 `EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/__beans__.xlsx`、`Assets/DataGenerated/Luban/**`、两处 active generated root 与 `ProjectSettings/GasCodeGen/**` lease 已一次性转交 R4，任一校验不符立即停止。执行 `RuntimeV1-Runnable R4-Kernel-Player`：作为唯一中央/Unity owner，J1 前只可准备不静态引用新 Attack ABI 的 result DTO、category/filter 与 launcher。J1 后创建新 RunId/run root，按 `RuntimeV1RunnableRunManifest-v1` 原子记录含 `__beans__.xlsx` 初始 SHA 的 `ProducerFingerprint`、tool identity、唯一生成 argv/log；确认 R1-R3 idle、全部源码冻结且无既有 Unity 进程，保持旧 GameRoom consumer 可编译，通过现有官方入口执行本轮唯一一次正式生成，只允许 pipeline 重写精确 `__beans__.xlsx` 与既定输出并记录 pre/post SHA，独立计算 `GeneratedArtifactIdentity`，确认 generated Attack ABI/Catalog 后等待进程退出并关闭 Unity。随后且仅随后，由 R4 修改 `AutoChessGameRoomDefinition`、GasTickJobs/GasTickDag/GasTickScratch、Stage-B Attack 初始化、PlayMode/Player consumer；不得再次生成。`GasStageBBootstrapRecorder.Record` 必须在现有 contract validation 成功后、创建 ECB 前调用同 RuleId `ValidateBlob`，reject=`ProfileInvalid` 且零写；PlayMode asmdef 直接引用 AutoChess；`AutoChessDemoSceneRunner` 在新 flag 下必须于旧 `runOnStart` 战斗前返回。只做最小接线，不重构 Tick DAG。冻结 `FinalSourceFingerprint` 后，执行静态门（不再生成）、一次 EditMode、一次 PlayMode/headless、一次 Development Windows Player build+auto launch；Edit/Play/Player 各写绑定同一 RunId/三 identity 的 provenance sidecar。Player launcher 使用 `-batchmode -nographics -logFile <runRoot>/Player.log -gasRuntimeV1Runnable -gasRuntimeV1RunnableRunId <RunId> -gasRuntimeV1RunnableManifest <absolute RunManifest.json> -gasRuntimeV1RunnableManifestSha256 <sha256>`，启动前拒绝旧 log/result。同一 Player 顺序运行 public Ability、production 9203 与固定 `Scale=1/AscCount=4` 的四 base-unit AutoChess，并原子写 result/退出；不复用 `-gasAutoChessDemo`，不调用 `RunGeneratedScenario(ValidationScenario)` 或旧 scale=50 runner。`SupportProfileAdmission=Passed` 必须来自真实 bootstrap 成功；精确 `__beans__.xlsx` 与 `ProjectSettings/GasCodeGen/**` 只允许唯一正式生成产生的运行状态变化，不修改控制面代码/合同，不计 D0-M2/D1 证据。输出 fresh 机器结果、日志健康和 release-hardening 边界；保持 `ProductionInstallAdmission=NotEvaluated`、`DeclaredFullSemanticEligibility=false`，不接 A/B/C/D。

## 后续 V1.1 Release-Hardening 起点

本轮交付后，发布硬化从 `D0-M2F` 开始，而不是直接执行旧 M2T。`D0-M2F` 只是 time-boxed 选路/决策门，**通过不等于 selector P0 已关闭**：

1. 先用最多三个 probe 对候选路线观察 Unity 实际读取的可变指针、直接启动行为和切换/强杀窗口。
2. 再冻结三元组 `(selected route, sole Unity-consumed selector, Authority/Derived/Cache table)`，而不是在选路前假定所有路线共享同一 selector。
3. 若选中的 sole selector 是 manifest/additional-file，必须先把 `ActiveGenerationRef` 从 selector 降为 audit/promotion record，提交 Spec08/ADR delta 并人工确认；不得保留两个 selector。
4. 只有选中路线完成完整 fault evidence、D0-M2R 回写并再次人工确认，selector P0 才能关闭。

因此，旧的 `B1-R ∥ D0-M2T ∥ D0-M2-O` 不再是下一轮领取顺序；它们是 V1.1 release-hardening backlog。
