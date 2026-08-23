# Runtime v1 第三轮多 Agent 架构与性能审查事实

> Owner：`00-当前架构事实` | 审查日期：2026-08-24 | 基线：`b12889eb` | 状态：当前代码与文档事实

## 结论

第二轮已经闭合 Runtime v1 的单 Kernel、ASC slab、稳定身份、标准 EndFixed、单 Drain、committed-work-wins、Spawn 整批 Ready 与 Tier B 业务语义主干；第三轮不撤回这些方向。

重新读取 `b12889eb` 后，仍有七组会导致不同实现者产生不同 Runtime 的 P0 文档缺口：配置 candidate 原子发布、typed semantic graph 与 CapacityProof、target stabilization 半写、Accepted Request 终态账本、per-Battle 关闸、`0..N` Runner/Drain/fence、FinalDrain/Disposed/ValidationResult 与真实发布证据链。另有 Snapshot/Cue reconcile、managed Cue 异步资源、hot-target 容量和策划 Impact/CI orchestration 等 P1。

本页只记录审查基线、当前实现证据和目标文档冲突；唯一目标裁决仍由 `01` 目录维护，实施切片仍由 `02` 目录维护。没有实现 Runtime v1，也没有运行 Unity、Luban、SourceGenerator、Profiler 或 Journaling。

## 审查基线与隔离

- 第三轮只读审查起点为 `EX-GAS-2.0@564fe711` 加第二轮未提交文档，随后第二轮以 `b12889eb` 提交并清洁工作区。
- 第三轮写入从确认的 `b12889eb` 创建独立 `codex/ex-gas-third-round-docs` worktree；没有覆盖第二轮共享工作区。
- UE GAS 实际源码位于 `E:/Unity/UnityProjects/_Git/Y_GameFramework/Tools/Github/UE-GAS`；用户初始路径中的 `Y/_GameFramework` 并不存在。
- 四个方向各有唯一审查 owner：Kernel/业务壳、UE 语义×DOTS、Luban/SourceGenerator、Headless/证据链；第二轮按 A→B→C→D→A 交叉质询，第三轮由主 Agent 去重裁决。

## 当前配置与生成链事实

| 事实 | 当前证据 | 不能推出的结论 |
|---|---|---|
| Luban row 不是 gameplay authoring 的唯一输入 | `EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/exgas.sourcegen.json:2-25` 可追加 modifier、覆盖 refresh/expiration；`Assets/GAS/Editor/CodeGen/Core/LubanNormalizedRowBootstrap.cs:637-648,789-862,1022-1071` 合并这些值 | Generated Catalog 可运行不等于单一事实源成立 |
| 9203 当前输入本身非法且被 sidecar 改写 | `Assets/DataGenerated/Luban/Json/GAS/exgas_tbgameplayeffect.json:403-440` 输出空 modifier 与 `StackingType=9203`；`DefinitionCatalog.gen.cs:684-710` 保留非法值并注入 overlay 语义 | Validation report 的零错误不能证明 enum、owner 或内容合法 |
| 同一轮生成可能读取旧 compiled normalized factory | pipeline 先写 `LubanNormalizedRows.gen.cs`，随后从当前 AppDomain/反射读取 factory；新源码尚未编译时 catalog/hash 可仍来自旧程序集 | “第二次运行收敛”不能代替一次运行一致性 |
| publish 不是 immutable candidate 的原子 promotion | 生成阶段直接写最终路径、错误后仍可继续、完整验证前可删 orphan并保存 manifest | 最终目录存在不能证明失败 candidate 未改变 active bundle |
| hash 链不能覆盖完整语义和 artifact bytes | Blob 主要暴露 `SchemaVersion`；反射 hash 依赖类型/字段枚举，manifest 未绑定全部 artifact bytes；AutoChess sidecar 未进入同一输入域 | 单一 InputHash 不能证明 schema、content、layout 与 artifact 一致 |
| Tag/Attribute layout 与 contract/capacity 仍不完整 | 当前 Tag mask 固定四个 `ulong`，生成循环有 256 上限；Catalog 无完整 Cost/Cooldown/Capture/Spawn/Target transaction contract 与 per Definition CapacityProof | 超上限不报错或 Runtime fallback 不能成为 v1 设计 |

## 当前 Runtime 与 Boundary 事实

1. 当前 Runtime 仍是五段 physical group、Ability Entity、owner-local/legacy GE 迁移态；`RuntimeV1不可兼容迁移基线事实.md` 已记录该差距，第三轮 Spec 不能反写成“已实现”。
2. 目标文档仍把 `AscOwnerCommandWave`、`AscTargetStateWave`、`Stabilize/Death`写成 durable mutation；Stabilization fatal 却在这些写入后才可能发现。只丢弃未发布 Fact 不能撤销已写 Attribute/Tag/ActiveEffect/Grant 权威。
3. `SessionIngressGate` 已定义 Accept/FaultClose，但仍缺 Producer namespace、payload conflict、AssignedAvailableTick receipt、exact sparse membership、normal terminal、BoundaryAccepted、retire/GC 与 history-expired 合同。
4. 一个 Session 可承载多个 BattleInstance，但 Boundary 只有 Session gate；Core terminal 到 Gate 关闭之间的 Accepted tail、per-Battle close cutoff 与独立 BattleOutcome seal 尚无完整载体。
5. 目标 Runner 写 `TickBatch -> 0..N FixedStep -> Drain`，但未固定 Entities 首次 `t=0` update、absolute elapsed、debt、actual physical/gameplay count和 0 FixedStep 时 Drain 的精确位置。
6. 当前 `10-AutoChess无头验收Spec.md` 仍写 `teardown + cleanup audit -> freeze ValidationResult -> dispose Session`；`03F/06` 又要求资源释放和 Disposed audit。最终 Result 与 Disposed 的顺序存在冲突。

## 当前验证链假阳性

| 假阳性 | 当前实现事实 | 缺失的证明 |
|---|---|---|
| `HasRequiredRuntimeChain()` | 主要检查若干 count `> 0` | 每 Request/Application 的精确 typed outcome、状态和 hash |
| `CalculateFactsHash()` | 混入 Frame、report key、raw SourceAbility/GameplayEffect Entity index | 与 batch/Entity/worker 无关的 canonical semantic bytes |
| Repeat run evidence | 主要比较 issued command、attribute、execution、period、Cue counts | CommandTrace、SlotLifecycle、FinalState、Fact/Cue sequence 与 BattleHash |
| Runner/summary 存在 | `AutoChessRuntimeRunner` 只是启动入口；当前没有真实 GAS/AutoChess Unity Test source | TestId、assembly/source/build hash、实际运行状态和 golden |
| x50 | 50 个隔离的四单位 BattleGroup | hot target、period burst、mass death、Boundary burst、wait fanout 或 cross-ASC live dirty |

因此 Headless 只能证明 Runtime 安装并消费某个已发布 package，不能单独证明非法 candidate 被生成器拒绝、atomic promotion 成功或 active package bytes 未改变。后一组证据必须由 Definition CodeGen/CI Publish Controller 产生，再与 Runtime evidence 通过同一 package descriptor 连接。

## 交叉质询后的发现变化

| 方向 | 撤回 | 保留/升级 |
|---|---|---|
| Kernel/业务壳 | 撤回“BattleOutcome 就是最终 ValidationResult”和“单 Session watermark足以封印某 Battle” | Request ledger、per-Battle双切面、t=0/Drain/fence继续为P0；FinalDrain/Disposed升为P0 |
| UE×DOTS | 撤回“v1 fault 域可暂不裁”和“Cue 已端到端闭合” | v1 Session-fatal；TargetPrepare/FaultReduce/Publish、Request terminal、Cue reconcile与Fault hash升级 |
| 配置链 | 无整项撤回 | typed support matrix、dense layout、CapacityProof、Live/Grant/EmittedRef与target transaction生成物升级为P0 |
| Headless | 撤回“Headless 可单独证明 generator/publish negative gate” | ResultSeal、同步点、0/1/N、ScaleProfile与exact package evidence继续保留并升级 |

## 重新基线后的 P0/P1

| 等级 | 当前缺口 | 对文档冻结的影响 |
|---|---|---|
| P0 | immutable candidate、四 hash、atomic promotion、exact install | 失败内容可能被发布或 Runtime 读取另一事实源 |
| P0 | normalized typed contract、dense layout、CapacityProof/consumer map | Cost/Cooldown/Capture/Spawn/Grant/Live/Target transaction不可完整生成或拒绝 |
| P0 | TargetPrepare → FaultReduce → TargetPublish | Stabilization fatal无法证明 target durable零半写 |
| P0 | Accepted Request ledger与唯一terminal envelope | Accepted承诺可能无终态、重复终态或无法安全回收 |
| P0 | per-Battle Gate close与双切面BattleOutcome seal | 单Battle终局会误伤其他Battle或在Result后遗留请求 |
| P0 | t=0 prime、0..N、Drain和合法fence | Scene/Headless tick数、容量和同步证据不可复现 |
| P0 | FinalDrain/Disposed/ResultSeal、versioned VectorManifest和六类证据pass | count、假测试或未运行证据仍可冒充发布通过 |
| P1 | Snapshot/ReadModel/Cue reconcile、managed资源异步生命周期 | overflow、destroy、late callback可能破坏表现闭环 |
| P1 | hot-target work unit、durable高水位、策划Impact/CI orchestration | 规模或策划发布链仍不可复现 |

## 本页不能推导的结论

1. 第三轮已经形成裁决，不等于对应 Runtime、生成器、测试或CI已经实现。
2. 文档静态验证通过，不等于 Unity compile、Burst、Player、Headless、Profiler或Journaling通过。
3. `P0/P1` 写入任务树，不等于任务已领取或已完成。
4. ADR 未选择前，不能宣称跨平台 bit-identical determinism；v1 默认推荐同 Build+Platform 的 float authority，但当前仍是待决项。
