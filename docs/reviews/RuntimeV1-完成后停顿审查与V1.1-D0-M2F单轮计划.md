# Runtime V1 完成后停顿审查与 V1.1 D0-M2F 单轮计划

> 日期：2026-08-30  
> 状态：审查完成，计划可执行  
> 当前交付：`RuntimeV1-Runnable-ClosedWorld` Development Player  
> 下一轮唯一目标：`V1.1 / D0-M2F — sole Unity selector 选路裁决`

## 1. 结论

Runtime V1 的 **Development ClosedWorld 可运行基线已经完成**，不再追加 Runtime 功能或回归测试。下一轮只执行 `D0-M2F`：在隔离 Unity fixture 中，以最多三个 time-boxed probe 冻结：

```text
(selected route, sole Unity-consumed selector, Authority/Derived/Cache table)
```

本轮禁止夹带 `SYS-04`、`SYS-13 + GE-24` 或其他 Runtime P1。它们不会缩短 Release 关键路径，却会改变已冻结的源码身份 `F`，迫使 PlayMode、Player 与证据链重跑。三条独立审查线经两轮交叉质询后对此形成 3/3 一致结论。

`D0-M2F` 通过也 **不等于 selector P0 已关闭**。它只完成选路合同；随后必须停顿并由人工确认。选中路线的完整故障实验、`D0-M2R`、production migration 与 `D1` 均另立任务。

## 2. V1 最终交付边界

权威结果见[第七轮收口执行结果](RuntimeV1-第七轮收口执行结果.md)。本次停顿审查复核后，以下事实保持成立：

| 项目 | 最终值 |
|---|---|
| RunId | `RV1-20260830T070718Z-ab754759ec68` |
| ProducerFingerprint `P` | `3cd42fde517c3d7f8b217846dd3798c980634438a149eedb9dc3782694ae79fb` |
| GeneratedArtifactIdentity `G` | `18fe1344cf0754728c55ced657919e939a6d0ea382bc0b78df74a4daa267d3af` |
| FinalSourceFingerprint `F` | `85078d0a12e7b54fecdd38b040c0538b55fe14a2422e645bd9411891eab685db` |
| BuildHash | `6d2164ad3024f034760532379fea8128b42b2203b45cd8fbba2259cfbb1fd89c` |
| EditMode | `RuntimeV1Runnable` 11/11 |
| PlayMode | `RuntimeV1Runnable` 5/5 |
| Development Player | exit 0；Ability / production 9203 / AutoChess 全部通过 |
| SupportProfileAdmission | `Passed` |
| ProductionInstallAdmission | `NotEvaluated` |
| DeclaredFullSemanticEligibility | `false` |

因此，允许声明的是“Development Player 受限闭世界基线已可运行”；不允许外推为 Release/IL2CPP、production install admission、crash-atomic 发布或完整 UE-GAS 语义完成。

## 3. 旧 8 个 P0 的当前 scoped 状态

[综合审查](RuntimeV1-当前实现与UE-GAS语义综合审查.md)形成于 V1 收口前，不能原样当作当前完成度。按当前代码与最终 Player 证据重新盘点如下：

| ID | 当前状态 | 当前已证明 | 未证明 / 延期 |
|---|---|---|---|
| `CFG-01` | `Closed-In-Runnable-Profile` | Runtime Catalog 由 `AutoChessGeneratedDefinitionCatalog.Build` 安装，不再由手写 gameplay 定义构造 | 不外推为任意 alternate package/install 合格 |
| `CFG-02` | `Open-Release` | Validator 独立重算 content、layout/attribute、tag 身份 | production install envelope 未在 Player 接入；`ProductionInstallAdmission=NotEvaluated` |
| `SYS-01` | `Closed-In-Accepted-Profile` | Accepted request 具有 `GasRequestKey`、唯一 RequestTerminal publish/read/drain | 不外推为未支持命令或完整多 producer 历史策略 |
| `SYS-02` | `Closed-In-Runnable-Profile` | 当前 host 的 Player 终局记录 `IngressClosed`；isolated Gate 合同证明 BattleTerminal 后同步拒绝后续请求 | 尚未证明 production Battle terminal 后再提交 tail 且 Battle/ASC 零写；多 Session/多 Battle 目标态未声明 |
| `ABL-08` | `Closed-OneShot-Profile` | Activate → Commit → normal End 已跑通 | 完整 AbilityTask/Wait program 不在支持面 |
| `ABL-12` | `Partial-Open-FullSemantic` | 无 Wait one-shot 的 OwnerTerminal 已关闭 | Wait/Continuation/Subscription cleanup 仍为 P1 |
| `GE-11` | `Closed-By-Profile-Rejection` | 不再“Accepted 后忽略”；RemoveEffect 在 RequestKey 前以 `UnsupportedByRuntimeV1Profile` 拒绝 | public RemoveEffect 语义仍为 P1 |
| `NUM-01` | `Closed-By-Catalog-Rejection` | Health cost 在 Catalog validation / Stage-B record 前 fail closed | 若未来允许 Health cost，仍须接入唯一死亡事务 |

补充确认：`GE-23` production 9203 与当前 scoped `SYS-03` 已关闭。`SYS-04`、`SYS-13`、`GE-24` 是有效 P1 backlog，但都不推翻本次 Development V1，也不是 `D0-M2F` 前置。

## 4. Release / CodeGen 当前实现盘点

当前实现尚不能直接进入完整 production physical experiment：

1. `ActiveGenerationRef` 是控制面记录，但 Unity 当前实际编译两个 `Assets/**/Generated` 活跃目录；Unity 启动不直接消费该 ref。
2. [GasCodeGenPipeline](../../Assets/GAS/Editor/CodeGen/GasCodeGenPipeline.cs)仍生成 `CoreCandidateRoot + AutoChessCandidateRoot`，[GasCodeGenGenerationStore](../../Assets/GAS/Editor/CodeGen/Core/GasCodeGenGenerationStore.cs)仍顺序备份、晋升两个 active root。
3. descriptor/ref 仍绑定 Core/AutoChess 双 tree；这与 ADR vNext 的单 generation、单 selector 目标存在待裁决差异。
4. [Candidate compile gate](../../Assets/GAS/Editor/CodeGen/Core/GasCodeGenCandidateCompileGate.cs)仍基于 active `Library/Bee/*.rsp` 做 source replacement 近似验证，不能冒充 candidate 自身的 production qualification。
5. [`D0-M1` 正式证据](../../Tools/Tests/GasCodeGen/N2-G0-D0-M1-evidence.md)已否决 pre-start materializer 与 directory-local UPM slot；后者会被 Unity `DirectoryMonitor` 写入瞬态文件。
6. 仓库已有 D0-M1 fixture 与证据，但尚无 `D0-M2F` harness。

两条候选路线只具有“值得探测”的平台能力，不代表已经合格：Unity 支持在项目 manifest 中声明直接 package dependency，也支持从本地 `.tgz` 添加 package；Unity Source Generator 可以通过位于 `Assets` 下、按 analyzer 名称命名的 `.additionalfile` 接收附加输入。参考 Unity 官方文档：[依赖与 manifest](https://docs.unity3d.com/cn/current/Manual/upm-dependencies.html)、[本地 tarball](https://docs.unity3d.com/cn/6000.0/Manual/upm-ui-tarball.html)、[创建 Source Generator](https://docs.unity3d.com/cn/current/Manual/create-source-generator.html)、[additional file](https://docs.unity3d.com/kr/current/Manual/roslyn-analyzers-additional-files.html)。

## 5. 两轮多 Agent 交叉审查记录

### 5.1 第一轮独立审查

| 审查线 | 初始结论 |
|---|---|
| Release / CodeGen | 下一门必须是纯 `D0-M2F`；不能提前并入选中路线完整实验 |
| Runtime / UE-GAS delta | 旧 8 个 P0 需按支持面重新标注；建议将 `SYS-13 + GE-24` 作为独立 Runtime 小轮，不与 `D0-M2F` 混合 |
| 验证成本 / DAG | 初始建议 `D0-M2F` 同轮并行一个 generated-independent `SYS-04 Dispose` 切片 |

### 5.2 第二轮交叉质询

三方按“是否当前 P0、是否缩短可交付时间、是否污染唯一 Unity 证据”重新审查，最终 3/3 选择 **仅 D0-M2F**：

- `SYS-04` 是 P1，且修改 World/Ingress teardown 后必须新增精确 PlayMode 与现有 5/5 回归。
- `SYS-13 + GE-24` 会改中央 Tick、allocator、lifecycle 与 fact 语义，测试与证据成本更高。
- 任一 Runtime 改动都会改变当前 `F`，让 V1 Player 证据只剩历史快照，并争抢唯一 Unity lease。
- 纯 `D0-M2F` 可保持单一归因，候选失败或不确定时也能立即停顿，不扩大范围。

## 6. 下一轮目标与完成定义

### 6.1 唯一目标

在与 production checkout 隔离、无 hardlink/reparse 的 disposable fixture 中，对以下两条候选路线执行可行性裁决：

1. immutable tarball + manifest selector；
2. stable assembly graph + SourceGenerator/additional-file selector。

最多运行三个 probe suite：两个候选各一个；只要能够选出 provisional candidate，第三个 sole-selector 冲突 probe 就是输出 `CandidateSelected` 前的必跑门。两条候选都没有形成 provisional candidate 时才跳过第三个 probe。

### 6.2 合法结果

| 结果 | 定义 | 后续 |
|---|---|---|
| `CandidateSelected` | provisional candidate 已通过强制冲突 probe，路线、唯一 Unity selector 与无重叠角色表全部确定 | `HumanConfirmationRequired`，立即停止 |
| `NoViableRoute` | 两条路线均以明确 hard-reject reason 否决 | D0-M2F blocked，立即停止；不得发明第三路线 |
| `Inconclusive` | 任一关键结论因超时、缓存依赖或证据不足无法确定 | D0-M2F blocked，立即停止；不得自动延长 time-box |

无论哪种结果，本轮均保持：

```text
D1Authorized=false
ProductionInstallAdmission=NotEvaluated
DeclaredFullSemanticEligibility=false
```

### 6.3 明确非目标

- 不修改 production Runtime、Semantics、Proofs、Install consumer。
- 不修改 production CodeGen pipeline、generation store、promotion、candidate compile gate 或 CLI。
- 不修改 production `Packages/manifest.json` / `packages-lock.json`。
- 不修改两个 active generated root 或 `ProjectSettings/GasCodeGen/**`。
- 不运行 official CodeGen、Runtime Edit/Play、Development Player、Release/IL2CPP 或长跑。
- 不执行选中路线完整 fault matrix、`D0-M2R`、production migration 或 `D1`。
- 不直接修改 Spec08/ADR 正文；只允许在裁决结果中写 delta 草案。

## 7. 三条并行任务链

### 7.1 执行拓扑

```text
J0 root：冻结 V1 证据、工具身份、保护路径 fingerprint
  ├─ R1 immutable tarball harness（禁止启动 Unity）
  ├─ R2 SourceGenerator harness（禁止启动 Unity）
  └─ R3 threat oracle / evidence contract（禁止启动 Unity）
                  ↓
J1 root：三线 idle；写集、AST、fixture、保护路径交叉检查
                  ↓
U0 root：唯一 Unity lease，串行 T → S → 强制 X（存在 provisional candidate 时）
                  ↓
J2 root：发布 tuple / NoViableRoute / Inconclusive 与证据 SHA
                  ↓
人工确认门：停止，不自动进入完整实验
```

### 7.2 独占写集与验收

| 链 | 独占写集 | 必须交付 | 禁止 |
|---|---|---|---|
| `R1-Tarball` | `Tools/Tests/GasCodeGen/D0M2F/Tarball/**` | A/B 内容寻址 `.tgz` fixture、manifest 切换脚本、三程序集 token 与事件采集、typed reject | 启动 Unity；写 production manifest、PackageCache、generated、Runtime |
| `R2-SourceGenerator` | `Tools/Tests/GasCodeGen/D0M2F/SourceGenerator/**` | 最小 canary generator、单 semantic blob/additional-file fixture、三程序集 token 与 Bee 参数采集 | 移植 production generator；写 production pipeline/generated/RSP |
| `R3-Contract` | `Tools/Tests/GasCodeGen/D0M2F/Contracts/**` | 机器可读结果 schema、protected-path oracle、Authority/Derived/Cache 模板、case-ID/static checks | 选择路线；写 R1/R2 或中央 runner；启动 Unity |
| `R0-root` | `Tools/Tests/GasCodeGen/Run-N2-G0-D0-M2F.ps1`、`Tools/Tests/GasCodeGen/README.md`、fresh `TestResults/GasCodeGen/N2-G0-D0-M2F/<RunId>/**`、`docs/reviews/RuntimeV1.1-D0-M2F-选路裁决.md` | J0/J1、中央整合、唯一 Unity 执行、结果确认与人工门 | 在各链未 idle 时启动 Unity；覆盖旧证据；把 probe 结果自动升级为 ADR/D1 |

所有新增 fixture 模板必须保留必要 `.meta`。运行时 fixture 必须深拷贝到 OS 临时目录，不得使用 hardlink、junction 或 reparse point；递归清理前必须验证精确临时根、owner sentinel 与全部后代边界，不满足即保留现场并 fail closed。

### 7.3 J0 / J1 冻结与 Unity lease

root 在任何并行线开始前生成唯一 `RunId=D0M2F-<UTC yyyyMMddTHHmmssZ>-<12 lowercase hex>`。`TestResults/GasCodeGen/N2-G0-D0-M2F/<RunId>` 必须不存在；目录或任一目标证据叶已存在即 fail closed，禁止删除、清空或覆盖旧 run。

J0/J1 的 protected fingerprint 必须精确覆盖：

- `Packages/manifest.json`、`Packages/packages-lock.json`；
- `ProjectSettings/ProjectVersion.txt`、`ProjectSettings/GasCodeGen/**`；
- `Assets/GAS/Runtime/V1/**`、`Assets/GAS/Editor/CodeGen/**`；
- `Assets/GAS/Generated/CodeGen/**`、`Assets/AutoChessDemo/Generated/**`；
- `Tools/GasCodeGenCli/**`；
- production `Library/Bee/**` 与实际 response files；
- `docs/adr/**`、`方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`、本权威计划、`Tools/Tests/GasCodeGen/N2-G0-D0-M1-evidence.md` 与 V1 权威证据根 `TestResults/RuntimeV1Runnable/RV1-20260830T070718Z-ab754759ec68/**`。

授权排除项只包括 R1/R2/R3 的三个独占工具写集、root 中央 runner/README、fresh run root 与 `docs/reviews/RuntimeV1.1-D0-M2F-选路裁决.md`。本计划在派发后冻结；其他 production 漂移一律使当前 probe 证据作废。

J0 必须单独记录 ADR-0001、Spec08、本计划与 D0-M1 evidence 的 raw-file SHA-256；J2 逐项复算并要求完全相等。任一输入 SHA 漂移都输出 `Inconclusive`，不得用新旧合同混合解释本轮证据。

U0 开始前，R1-R3 必须全部 idle；root 必须枚举现存 `Unity.exe`/batchmode 进程并确认没有活动 Editor 或其他 Unity job 占用 production project/fixture。来源不明时停止，不自动终止用户进程。每个 suite 后必须等待本 suite 启动的 Unity 及其受管 child 全部退出并确认 fixture 写入稳定，再进入下一 suite；超时或残留进程使本轮输出 `Inconclusive`。Probe X 只由 root 中央 runner 执行。

## 8. Probe 合同

### 8.1 Probe T：immutable tarball

时间盒：90 分钟。

最少观察：

1. 由内容寻址 A/B archive 构建 disposable fixture，只改变 fixture `Packages/manifest.json`。
2. direct cold Unity 启动消费 A；A → B 后观察三程序集 generation token。
3. 覆盖一个最小切换/强杀恢复窗口。
4. 记录 manifest、archive、lock、PackageCache、解包目录的读取与写入角色。

通过：恰好一个 Unity 实际消费的可变 selector；无 wrapper/materializer；Runtime、Editor、AutoChess 同代；archive authority 与 derived/cache 无重叠。

立即否决：需要 pre-start materializer；存在多个可变 pointer；Unity 写 authority/archive；三程序集混代；cache/lock/解包目录可成为 fallback selector。

### 8.2 Probe S：stable graph + SourceGenerator

时间盒：120 分钟。

最少观察：

1. 只在 disposable fixture 中创建最小 canary generator，不复用或迁移 production generator。
2. 单 semantic blob/additional-file 由 A 原子切换到 B。
3. 记录三次真实 compilation 的 generator 输入、generation token 与 Bee 参数。
4. 冷启动后检查 stale Bee/RSP 不得决定最终 generation。

通过：一个 semantic selector 可靠触发 Runtime、Editor、AutoChess 三程序集重新生成且全部绑定 B；不依赖 active `.gen.cs`、旧 RSP 或 stale Bee 输出。

立即否决：需要每程序集独立 selector/token；任一程序集未失效或绑定旧 token；SourceGenerator output 必须与 active generated source 并存；结果依赖 stale cache。

### 8.3 Probe X：sole-selector 强制冲突门

时间盒：60 分钟。T/S 完成并按第 8.4 节选出 provisional candidate 后必跑；未通过 X 不得输出 `CandidateSelected`。只有无法选出 provisional candidate 时才跳过。

构造 `ActiveGenerationRef=A`、`实际候选 selector=B` 的冲突，执行 direct cold Unity 与一个 kill/restart 窗口。只有当一个输入确定控制全部三程序集、另一个对 Unity 消费权限为零并可降为 audit/promotion record 时通过。

如不同程序集/启动阶段读取不同输入，或结果受缓存/启动顺序影响，立即输出 `Inconclusive`。

### 8.4 路线裁决

- tarball 合格：将 tarball 作为 provisional candidate；即使 SourceGenerator 也合格，仍优先 tarball，因为它复用 D0-M1 已验证的 package graph/三程序集基础，生产迁移面更小。
- tarball 明确否决且 SourceGenerator 合格：将 SourceGenerator 作为 provisional candidate。
- 两者均明确否决：`NoViableRoute`，跳过 X。
- tarball 为 timeout/unknown 且只有 SourceGenerator 合格，或其他状态无法按上述规则形成 provisional candidate：`Inconclusive`，跳过 X。
- provisional candidate 必须再通过 X，才升级为 `CandidateSelected`；X 失败或不确定则为 `Inconclusive`。

## 9. 最小验证矩阵

| 门 | 唯一必要验证 |
|---|---|
| J0 静态门 | PowerShell AST parse；固定 case ID/schema；tool/fixture SHA；protected-path before fingerprint |
| Candidate | Probe T 一次 + Probe S 一次 |
| Sole selector | 任何 provisional candidate 在被选择前必须执行 Probe X 一次 |
| J2 收口 | protected-path after fingerprint；raw/aggregate JSON 可解析；case-ID 精确；harness/fixture/Unity 版本与证据 SHA 绑定 |
| 明确不跑 | official CodeGen、完整 EditMode/PlayMode、`RuntimeV1Runnable` 5/5、Player、Release/IL2CPP、双跑、长跑 |

这里的“一次”指一个有界 probe suite；suite 内只保留合同要求的 cold start、selector switch 与最小 kill/restart，不做重复稳定性双跑。路线自身的预期 hard-reject 必须记录 typed reason，并继续执行另一候选；它不是 harness failure。只有 harness、schema/case-ID、protected-path、Unity 进程或证据完整性失败才全局停止并输出 `Inconclusive`，且不自动扩大测试集。

## 10. 人工确认门

`CandidateSelected` 后必须把以下内容交给人工确认：

1. 选中与被否决路线的原始证据及 SHA；
2. 唯一 Unity-consumed selector；
3. 完整、无重叠的 Authority/Derived/Cache 表；
4. 双 selector 冲突 probe 的实际消费结果；
5. Spec08/ADR delta 草案；若 selector 是 manifest/additional-file，草案必须把 `ActiveGenerationRef` 降为 audit/promotion record；
6. 人工确认裁决与 delta 后，先由独立规范变更把已批准 delta 正式写入并冻结 Spec08/ADR；规范变更完成前不得启动完整实验；
7. 规范 delta 完成后，下一执行任务才允许是“选中路线完整实验”，不得直接开始 production migration。

以下任一情况均按 P0 阻断：`ActiveGenerationRef + manifest` 或 `ActiveGenerationRef + additional-file` 同时声称 active authority；stable alias/materializer 与另一 selector 并存；tarball/SourceGenerator 与现有 active generated source 同时参与编译；Core/AutoChess 双独立晋升点继续存在；lock、PackageCache、解包目录或 Bee/RSP 被当成 fallback selector。

## 11. 可复制给并行会话的任务提示词

### R1-Tarball

> 在 `E:/Unity/UnityProjects/_Git/gameplay-ability-system-for-unity` 的当前 local checkout 执行 `V1.1 D0-M2F R1-Tarball`。先读 `docs/reviews/RuntimeV1-完成后停顿审查与V1.1-D0-M2F单轮计划.md` 与 ADR-0001。只写 `Tools/Tests/GasCodeGen/D0M2F/Tarball/**`，构建 disposable immutable `.tgz` A/B fixture、fixture manifest selector 切换、三程序集 generation token 和事件采集。禁止启动 Unity，禁止修改 production `Packages/**`、Runtime、CodeGen、generated、ProjectSettings 或 Library/Bee。fixture 只能深拷贝，不得 hardlink/reparse；清理必须 sentinel + 边界复核。交付脚本、fixture、固定 case ID、预期 typed result、文件清单与 SHA；完成后停止并通知 root 获取唯一 Unity lease。

### R2-SourceGenerator

> 在 `E:/Unity/UnityProjects/_Git/gameplay-ability-system-for-unity` 的当前 local checkout 执行 `V1.1 D0-M2F R2-SourceGenerator`。先读权威计划与 ADR-0001。只写 `Tools/Tests/GasCodeGen/D0M2F/SourceGenerator/**`，在 disposable fixture 中实现最小 canary SourceGenerator、单 semantic blob/additional-file A/B 切换、Runtime/Editor/AutoChess 三程序集 token 与真实 Bee 参数采集。禁止启动 Unity，禁止移植或修改 production generator/pipeline，禁止依赖现有 active `.gen.cs` 或 stale RSP。交付脚本、fixture、固定 case ID、预期 typed result、文件清单与 SHA；完成后停止并通知 root。

### R3-Contract

> 在 `E:/Unity/UnityProjects/_Git/gameplay-ability-system-for-unity` 的当前 local checkout 执行 `V1.1 D0-M2F R3-Contract`。先读权威计划、ADR-0001 与 D0-M1 evidence。只写 `Tools/Tests/GasCodeGen/D0M2F/Contracts/**`，产出机器可读 aggregate schema、固定 case-ID 合同、protected production path before/after oracle、Authority/Derived/Cache 空模板、hard-reject/timeout/inconclusive 枚举与静态校验。不得替 R1/R2 选路线，不得写中央 runner，不得启动 Unity 或修改任何 production 文件。交付 schema/checker、威胁清单、文件清单与 SHA；完成后停止并通知 root。

## 12. 延期账本

以下内容明确不属于下一轮；本节是关键延期项，不是 65 个 canonical ID 的穷尽清单：

- 选中路线完整 fault matrix、`D0-M2R`、D1、production promotion/install admission；
- `SYS-04 Dispose`；
- `SYS-13` capture reservation/exact-free accounting；
- `GE-24` per-slot typed lifecycle transition；
- `SYS-09` 有界 payload/terminal history 与 lookup；
- `SYS-12` Gate-assigned AvailableTick；
- public `GE-11 RemoveEffect`、完整 `ABL-12` Wait cleanup、EffectSpec 全身份链；
- Tag/Block/Cancel、contribution/inhibition/fixed-point；
- Release/IL2CPP/AOT、跨 OS、断电/fsync、Profiler、目标规模长跑；
- `FullSemanticEligibility=true`。

下一轮结束条件不是“把所有 backlog 做完”，而是尽快给出一个可人工裁决、不可双解的物理路线结论，然后停顿。
