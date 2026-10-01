# ADR-0001：CodeGen 单 Generation、唯一 Selector 与 Install Envelope

- 状态：已接受（D0-M2S 已通过；`StableGraphSourceGenerator` 为唯一 production 路线；D1 已授权）
- 日期：2026-08-31
- 决策 owner：N2-G0-D
- 关联规范：`08-Luban-SourceGenerator配置生成链路Spec.md`、`14-DefinitionCodeGen目标链路Spec.md`、`15-SourceGenerator职责边界Spec.md`、`25-配置语义编译契约与CapacityProof统一裁决Spec.md`

## 结论

D0-M2R 当前有效裁决绑定正式 RunId `D0M2S-20260830T180951Z-fe75df12b9a2` 及 terminal `Passed / None`（terminal SHA-256 `0578fa2afee26b6c024f37607db4c6ff06302ff212dc72974d31ff6a97a7149b`）。同一 fresh run 中 `FullFaultStatus=Passed`、`SourceGeneratorSpecificXStatus=Passed`、SG-01～SG-08 全部通过且 evidence closure 为 74/74，因此 `StableGraphSourceGenerator` 被接受为唯一 production 路线。

唯一 Unity-consumed generation selector 冻结为 `Assets/GAS/CodeGen/Selector/Generation.GasCodeGenSourceGenerator.additionalfile`，immutable analyzer authority 冻结为 `Assets/GAS/CodeGen/Analyzers/GasCodeGenSourceGenerator.dll`。本 ADR 再次确认后 `D1Authorized=true`、`ProductionMigrationAuthorized=true`；`ProductionInstallAdmission=NotEvaluated`、`DeclaredFullSemanticEligibility=false` 与 `RuntimeV1-Runnable-ClosedWorld` 状态保持不变。

精确 production layout、selector wire、Authority/Derived/Cache、candidate/promotion/recovery/migration 与 D1 写集以 [SourceGenerator 路线接受与 D1 授权裁决](../reviews/RuntimeV1.1-D0-M2R-SourceGenerator路线接受与D1授权裁决.md) 为准。实验中观察到的 `Assets/Selector/Generation.D0M2SCanaryGenerator.additionalfile` 是 disposable canary 路径，不得安装进 production；历史 terminal 内 `AcceptedProductionRoute=None / D1Authorized=false` 是实验不得自我授权的冻结字段，不回写。

以下 7 点仅记录 D0-M2F Run5 当时冻结的 immutable tarball 候选合同，已被上述 D0-M2T/D0-M2R 当前裁决 supersede，不再表示 production 路线已选中：

1. generation A/B 以 content-addressed immutable `.tgz` 作为 Authority payload。
2. `Packages/manifest.json` 是 Authority / mutable `SoleUnitySelector`；单文件原子 replace 是冻结的唯一 Unity-consumed active generation 候选线性化操作，其 crash-atomic 性仍须完整故障实验证明。
3. `ProjectSettings/GasCodeGen/ActiveGenerationRef.json` 降为 Derived `AuditOnly` promotion record，对 Unity active generation 选择权为零。
4. `Packages/packages-lock.json` 是 Derived；D0-M2T 当时要求 manifest 临时/备份、publish intent、owner sentinel 等事务/恢复对象同样为 Derived 且零 Unity 选择权。PackageCache、解包目录与 Bee/RSP 当时被归为 Cache / compiled projection，不得成为第二或 fallback selector；正式实验已经证明 Unity 的 warm cache 行为违反该 production 候选要求。
5. Core、Editor/Baking 与 AutoChess generated code 必须来自 manifest 选中的同一 generation；禁止继续维护两个 active 根或任何 stable alias/materializer 旁路。
6. `GasInstallEnvelope-v2` 是由已封存 descriptor v3 byte snapshot 派生的 runtime-safe build input；它不进入 package tree 或 `ArtifactManifestHash`，避免自哈希闭环。D1 直接升级版本而不保留 v1/v2 双写；`FullSemanticEligibility` 仍保持 `false`，Semantics、Proofs 和 Runtime admission consumer 不属于 D 的修改范围。
7. SourceGenerator 路线当时虽然通过 Probe S，但按当时冻结优先级未被选中。

上述历史候选合同绑定 [RuntimeV1.1 D0-M2F 选路裁决](../reviews/RuntimeV1.1-D0-M2F-选路裁决.md) 与 RunId `D0M2F-20260830T124005Z-c661f64e4c81`；它必须保留为“当时为何进入 tarball 完整故障实验”的历史事实，不能再作为当前 production 选路依据。

本文后文出现的 `D0-M2T` 均指已完成的 fresh `ImmutableTarballFullFaultExperiment`；所有 tarball 时序只作为历史实验合同解释，当前门一律以上述 D0-M2R 裁决为准。

## 背景与问题

当前控制面把以下两个目录作为 active 输出：

- `Assets/GAS/Generated/CodeGen`
- `Assets/AutoChessDemo/Generated`

即使发布器先统一备份再依次移动，两次目录安装之间仍存在 Unity 可观察的混代状态。当前 candidate compile gate 还依赖 active Bee/Roslyn response file，再替换部分源码；它只能证明“近似 active 图”，不能证明 candidate 自己的 `.asmdef/.asmref/define/platform/reference/source` 图。

D0-M2F Probe X 以 `ActiveGenerationRef=A`、`Packages/manifest.json=B` 制造真实冲突，cold、计划 kill 前与 restart 后三次观察中 Runtime、Editor、AutoChess 程序集都只消费 B。因此 `ActiveGenerationRef` 不是该 feasibility fixture 的 Unity selector。D0-M2T 随后证明 manifest 指向的 Authority 缺失时 warm `PackageCache` 仍可被消费，故“manifest + immutable tarball 可直接成为 production 唯一 selector”的推论已被否决。

## D0-M1 实验裁决：两条候选均已否决

单根是必要的布局收敛，但不是目录事务。Windows/.NET 不能以一次 replace 把一个非空目录原子替换为另一个非空目录；发布器仍需先移走旧根、再把新根移入 active 路径：

```text
Old -> Missing -> New
```

这与目标规范的两条硬门冲突：promotion 自身失败时 active ref 必须保持旧值且 active 文件不得部分来自 candidate；任一 promotion 故障点后 active generation 必须 byte-for-byte 不变。`AssetDatabase.StartAssetEditing`、`DisallowAutoRefresh` 与 `LockReloadAssemblies` 只能约束仍存活的 Editor 进程，不是进程崩溃后的持久屏障。

`D0-M1` 已完成正式串行双跑。退出码 1 是候选被否决后的预期结果；两轮 case 集、拒绝决策与 deterministic evidence fingerprint 完全一致，证据见 `Tools/Tests/GasCodeGen/N2-G0-D0-M1-evidence.md`。

| 候选 | 决定性证据 | 当前裁决 |
|---|---|---|
| 单根 + 启动前 materializer | `M-01` 证明 old move 后强杀留下 Missing；`M-02` 证明直接 `Unity.exe` 可绕过 materializer 并观察该状态 | 否决：`RejectedByDirectUnityBypass` |
| Directory-local UPM slots + 单 `Packages/manifest.json` selector | selector replace、UPM resolve、域重载与三程序集同代成立，但 `U-08` 两轮均捕获 80 条 generation 目录瞬态写事件 | 否决：`RejectedByUnityDirectoryMonitorMutation` |
| Immutable tarball selector | D0-M2F Probe T/X 曾通过并进入完整故障实验；正式 D0-M2T 证明 missing Authority 时 warm `PackageCache` 仍消费 B | production 路线否决：`MissingOrCorruptAuthorityAccepted` |
| 稳定 assembly 图 + 单 self-contained additional-file selector + SourceGenerator | D0-M2S full-fault、SG-specific X、SG-01～SG-08 与 74/74 closure 均通过 | `AcceptedProductionRoute=StableGraphSourceGenerator`；D1 已授权 |

目录最终 hash 恢复一致不能覆盖事件级写入事实；generation 一旦被 Unity 写入，就不再满足不可变发布单元契约。团队约定、README、Hub/IDE 包装器或 Editor `InitializeOnLoad` 也不能修复 materializer 的直接启动旁路。

历史 tarball 候选会向 Unity 暴露物理源码，因此其 package 内 `.asmdef/.asmref/.meta` 与所有实际路由 bytes 必须进入实验身份边界。该约束继续解释 D0-M2T 的证据设计，但 immutable tarball 已无 production 资格；当前 SourceGenerator 路线的精确身份边界已经由 fresh SG full-fault + SG-specific X 后的 D0-M2R 冻结。

## 决策一：逻辑 Generation 与唯一 Selector（D0-M2F 历史候选，已 superseded）

本节保留 D0-M2F 当时冻结的 immutable tarball 候选物理职责，只用于解释历史实验输入；它不再声明当前 production selector。下面的 `Package/` 是该候选 generation 内的完整逻辑产物，封存后由 content-addressed immutable `.tgz` 承载。

### Canonical 角色（精确 production layout 延后）

```text
<ContentAddressedArchiveAuthority>/<sha256>.tgz # Authority；精确 root 由 D0-M2R 冻结
Packages/manifest.json                          # Authority / mutable SoleUnitySelector
Packages/packages-lock.json                    # Derived；无选择权
ProjectSettings/GasCodeGen/ActiveGenerationRef.json
                                                # Derived audit/promotion record；不是 selector
<ControlPlaneTransactionArtifacts>              # Derived；intent/temp/backup/sentinel，精确布局延后
<GenerationMetadata>                            # Derived；descriptor/envelope/compile plan/record
Library/PackageCache/** + unpack + Bee/RSP       # Cache / compiled projection；never selector
```

在该历史候选中，Run5 未证明 production archive root、generation metadata root、transaction artifact 文件名或 candidate workspace 的精确路径；D0-M2T 随后否决了整条 tarball production 路线。当前 SourceGenerator 路线已把 production selector/analyzer 路径冻结，candidate workspace 继续与 production checkout 隔离，精确 transaction/recovery 对象按新的 selector intent 协议实现。

禁止再次出现 `CoreCandidateRoot + AutoChessCandidateRoot`、`CoreTreeSha256 + AutoChessTreeSha256` 或两个 active root 的生产协议。candidate workspace 可暂存 emitter 的多目录输出，但 descriptor vNext 只记录一个 `SelectorSha256`、一个 `AnalyzerSha256`、一个 `RouteScaffoldSha256`、一个覆盖完整受管 candidate artifact 集的 `ArtifactManifestHash` 与一个只覆盖 bundle C# subset 的 `SourceArtifactInventoryHash`；后者不得替代四元 install identity。

### selector、audit record 与投影的职责

在 D0-M2F 历史候选中，`ActiveGenerationRef.json` 是不参与 Unity 选择的 audit/promotion record。当前 SourceGenerator 路线继续把它冻结为 `DerivedAudit / UnityConsumerAuthority=0`：它绑定 selector、analyzer、scaffold、compile-plan 与四元 identity，但只能在 selector commit 后写入；缺失或漂移不得反向选择、回滚或修复 selector。现有 `RefSha256` / `RefFileSha256` 只作为 legacy migration/evidence 输入，由 D1 升级为 route-specific audit schema。

历史候选要求 `Packages/manifest.json` 直接绑定精确 content-addressed archive，并作为 Unity 唯一 active generation selector；D0-M2T 已证明该要求在 Authority 缺失后的 warm `PackageCache` 场景不成立，因此不得进入 production。`ActiveGenerationRef` 在 Probe X fixture 中仍保持 `UnityConsumerAuthority=0`，但这不挽救 tarball 路线，也不能转化为 SourceGenerator 路线授权。

该历史候选曾要求同代重复 publish 在 Authority/manifest/identity 全部复核后收敛为 no-op；当前 SourceGenerator 路线固定以相同 selector bytes 为 no-op，已有 selector 使用单文件原子 replace、首次发布使用 no-overwrite atomic create-new/move，并在 commit 后写零选择权 audit；两种提交都只以 canonical selector 出现完整 target bytes 为唯一线性化点。

## 决策二：程序集边界与迁移输入

D0-M2F 首先证明历史 tarball fixture 内三类 generated source 被 Runtime、Editor 与 AutoChess 三个既有程序集同代消费；D0-M2T 否决了该物理路线。D0-M2S 随后证明稳定 assembly 图、共同 additional-file 与固定 analyzer 可以在这三个 owner 中同代生成；D0-M2R 现冻结下表的 production 归属。

production 程序集 owner 与 scaffold 约束为：

| 逻辑 artifact | Unity 程序集归属 | 路由约束 |
|---|---|---|
| Runtime generated source | `com.exhard.exgas.generated.runtime` | stable runtime `.asmdef/.meta` 与 required anchor 固定；SourceGenerator 按 assembly name 发射 |
| Editor/Baking generated source | `com.exhard.exgas.generated.editor` | stable editor `.asmdef/.meta` 与 required anchor 固定；SourceGenerator 按 assembly name 发射 |
| AutoChess generated source | `com.exhard.exgas.autochessdemo` | required anchor 加入既有 AutoChess assembly；不得新建第四个 generated asmdef/asmref |

AutoChess generated 类型与手写 AutoChess 代码保持同程序集 `internal` 可见性。当前 SourceGenerator 直接在 `com.exhard.exgas.autochessdemo` compilation 内 `AddSource`，不复制物理 source、不创建第四程序集；三个 anchor 共同提供 analyzer 未运行时的硬编译失败。

selector bundle 内所有 generated source bytes 都进入 artifact inventory；selector、analyzer、`.meta`、stable `.asmdef`、anchor 与实际路由 bytes 进入 route scaffold/candidate compile identity。缺失、重复、case collision、GUID 漂移或程序集解析错误时，candidate 不得获得 selector promotion 资格。

## 决策三：Install Envelope

### 角色边界

`GasPackageDescriptor.json` 在 D1 升级为 descriptor v3，继续作为 Editor/CI 的完整描述符，包含 required-set contract、完整 artifact inventory、source-subset bijection、proof/evidence 引用与 route binding。`GasInstallEnvelope-v2` 是它的最小、不可变、runtime-safe 投影，用于后续 C1/Z 在 Build/Bootstrap 边界注入 Runtime admission consumer。

Player 不读取 `ProjectSettings`，Runtime consumer 也不解析 Editor/Newtonsoft descriptor。D1 只生成和封存 envelope bytes；将 bytes 注入 Player/Editor bootstrap、解码、admission 以及生产入口接线属于 C1/Z。

### 唯一 wire format

envelope 使用 `EX-GAS-InstallEnvelope-v2` domain 的定长顺序 binary codec；`EnvelopeVersion=2`、`DescriptorVersion=3`，旧版本直接拒绝，不做兼容读取：

- 整数：little-endian 固定宽度。
- 布尔：单字节 `0` 或 `1`。
- 字符串：`uint32` UTF-8 byte length + 原始 UTF-8 bytes。
- SHA-256：64 个小写 ASCII 十六进制字符；未闭合 proof 使用零长度字段，不使用伪 hash。
- 禁止 culture-sensitive string、反射字段顺序、JSON property 顺序和默认 `ToString()` 参与编码。

字段顺序固定为：

1. `EnvelopeVersion`
2. `EncodingDomain`
3. `DescriptorVersion`
4. `DescriptorSha256`
5. `SemanticIdentityAlgorithm`
6. `ArtifactIdentityAlgorithm`
7. `ArtifactManifestHashDomain`
8. `RequiredArtifactSetId`
9. `RequiredArtifactSetContractHash`
10. `SourceInputHash`
11. `SchemaHash`
12. `ContentHash`
13. `LayoutHash`
14. `ArtifactManifestHash`
15. `SourceArtifactInventoryHash`
16. `SelectorSha256`
17. `AnalyzerSha256`
18. `RouteScaffoldSha256`
19. `CandidateCompilePlanSha256`
20. `TypedContractHash`
21. `LayoutProofHash`
22. `CapacityProofHash`
23. `FullSemanticEligibility`

四元 install identity 仍然且只由以下字段组成：

```text
{ SchemaHash, ContentHash, LayoutHash, ArtifactManifestHash }
```

`RequiredArtifactSetContractHash`、`SourceInputHash`、`SourceArtifactInventoryHash`、`SelectorSha256`、`AnalyzerSha256`、`RouteScaffoldSha256`、`CandidateCompilePlanSha256` 与 `DescriptorSha256` 是 provenance/物理安装证据，不能替代四元 identity。D1 将 required set 从包含动态 asmdef 的 v1 升级为 D0-M2R 冻结的 `EX-GAS-RuntimeV1-RequiredArtifacts-v2`；stable asmdef 改由 scaffold hash 绑定。`PromotionId` 不进入 envelope；同一 generation 的显式 rollback 会产生新 PromotionId，但不能改变 selector、analyzer、scaffold 或 envelope bytes。

### 无自哈希绑定

`GasInstallEnvelope.bin`、`GasPackageDescriptor.json`、`CandidateCompilePlan.bin` 和 `GenerationRecord.json` 位于 canonical selector 之外，不进入 `SelectorSha256` 或 `ArtifactManifestHash`。canonical selector 与 candidate manifest 文件自身也不进入 `ArtifactManifestHash`；该 hash 只覆盖 manifest 列出的完整受管 artifact 集。任何参与 `ArtifactManifestHash` 的 artifact bytes 都禁止反向嵌入 `ArtifactManifestHash` 或 `SelectorSha256`；C# source 还禁止嵌入自身 `SourceSha256` 或 `SourceArtifactInventoryHash` 的 raw/hex 表示，否则会形成自哈希闭环。

这不是未封存旁路：

1. descriptor 绑定完整 candidate manifest bytes、`ArtifactManifestHash`、`RequiredArtifactSetContractHash`、bundle `SourceArtifactInventoryHash`、required C# set 与 bundle entries 的双向 bijection、analyzer、route scaffold 与全部 artifact bytes。
2. envelope 绑定 descriptor byte snapshot、四元 identity、`RequiredArtifactSetContractHash`、`SourceArtifactInventoryHash`、selector/analyzer/scaffold 与 compile plan。
3. generation record、selector transaction 与 audit record 必须绑定同一 descriptor、envelope、compile plan、`PreviousSelectorState`、target selector 完整 bytes，以及仅在 previous present 时存在的 previous selector 完整 bytes 和 analyzer/scaffold identity。
4. Store 从 Stage 到 selector transaction 与 audit binding 全程只使用第一次读取的 descriptor/envelope/compile-plan/selector/analyzer/scaffold byte snapshot；任何落盘 bytes 与 snapshot 不同都拒绝。selector commit 必须先于零选择权 audit write。

`InstallEnvelopeSha256` 是 envelope bytes 的外部哈希，写入 generation record/ref，不写回 envelope 本身。

D1 只能输出 `FullSemanticEligibility=false`，且 `TypedContractHash`、`LayoutProofHash`、`CapacityProofHash` 未由 A/B/Z 提供时保持空字段。不得用当前过渡 semantic snapshot 或默认值伪造 proof hash。

## 决策四：Candidate 编译图

SourceGenerator promotion compile gate 已由 D0-M2S 与 D0-M2R 冻结：

1. candidate 先完成 semantic/layout/artifact 等 pre-seal validation，并把现有 emitter 的精确 C# bytes 封装进唯一 self-contained selector bundle。
2. selector 封存后的任何 byte 变化必须产生新 `SelectorSha256` 与新 candidate，禁止原地修补；analyzer 与 stable scaffold 在 ordinary generation promotion 中不可变。
3. candidate 必须在独立临时 Unity project 中使用精确 production selector/analyzer/scaffold 相对路径，完成 isolated Unity compile 与 route-specific fault gate 后才获得 selector promotion 资格。
4. 临时项目必须创建自己的 `Library/Bee`；禁止复制本 checkout 或其他 worktree 的 Bee 图。
5. 使用项目锁定的 Unity `6000.3.14f1` 串行 import/compile。
6. 从 Unity `CompilationPipeline` 导出实际 assembly/source/additional-file/define/platform/reference/compiler-option 图，并形成 canonical `CandidateCompilePlan.bin`。
7. 明确断言 AutoChess、Runtime 与 Editor compilation 只消费同一 production selector bytes、同一 analyzer bytes，并各自生成 required marker 与预期 artifacts。
8. selector、analyzer、scaffold、source、define、platform、reference 或 toolchain 任一变化必须改变相应身份并触发真实重编译；stale Bee/RSP、duplicate selector 与 legacy active generated source 都是硬失败。

直接 Roslyn/RSP 重写入口可保留为不具 promotion 权限的诊断工具，也可删除；它不能再产生 promotion token。

## 决策五：物理消费路线与实施门

D0-M1 已否决启动前 materializer 和 directory-local UPM slots。它们原有的发布、恢复、启动拦截与双根迁移时序只保留在实验记录中，不再构成本 ADR 的规范或 D1 实施授权。

D0-M2 整体属于 V1.1 release-hardening，不进入 `RuntimeV1-Runnable-ClosedWorld` 的关键路径。当前严格顺序为：

1. 已完成（历史）：D0-M2F 冻结 `(ImmutableTarball, Packages/manifest.json, Authority/Derived/Cache table)` 并把 `ActiveGenerationRef` 降为 audit/promotion record。
2. 已完成（正式）：D0-M2T 覆盖 missing/corrupt authority、selector/lock drift、stale cache/Bee/RSP、hardlink/reparse、强杀点、事件 watcher、cleanup 与真实项目协议保护；terminal 为 `RouteRejected / MissingOrCorruptAuthorityAccepted`。
3. 已完成（正式）：D0-M2S 在同一 fresh run 中通过 full-fault、SG-specific X、SG-01～SG-08 与 74/74 closure。
4. 当前裁决：`AcceptedProductionRoute=StableGraphSourceGenerator`；production selector/analyzer 路径、self-contained bundle、单文件 promotion 与恢复边界已冻结。
5. 下一任务为 `D1-SourceGeneratorProductionMigration`；D1 迁移 legacy active `.gen.cs`、实现 exact selector/analyzer/scaffold 与 route-specific E1，但不得设置 `FullSemanticEligibility=true`。

当前固定为 `D1Authorized=true`、`ProductionMigrationAuthorized=true`、`ProductionInstallAdmission=NotEvaluated`、`DeclaredFullSemanticEligibility=false`。

## 决策六：Token、旁路、幂等与崩溃清理不变量

下列是不随物理路线变化的不变量；D1 已获授权，涉及 `PreviousSelectorState=Missing|Present`、intent 与 scavenger 的精确对象必须按当前 SourceGenerator 裁决实现。

1. `CandidateWorkspace` 创建不可伪造的 sealed workspace token；token 绑定 workspace id、权威 payload snapshot、candidate compile plan 和进程内 nonce。
2. `StageGeneration` 只接受该 workspace 产生且已被 descriptor/envelope/compile gate 封存的 token；公开路径字符串不能获得 Stage/Promote 权限。
3. Store 是唯一 mutation owner。删除或降权所有 `PromoteAll`、`PromoteDirectory`、无 token `StageGeneration` 等旧旁路；legacy `CommitActiveRef` 只能被删除或降为零选择权 audit write，不能获得 selector commit 权限。
4. Store 必须先读取 canonical selector 的完整 previous snapshot，再以 `FileMode.CreateNew` 把 `CommitAttemptState=NotStarted`、`PreviousSelectorState=Missing|Present`（previous bytes 仅在 `Present` 时存在）与其余字段一次性写入并 flush fixed `PublishIntent.json`，由此获得单 active mutation claim；禁止先创建不含 previous 的 claim 再补写。intent 同时绑定 `ExclusiveClaimId`、owner sentinel、target selector 完整 bytes、`DescriptorSha256`、`InstallEnvelopeSha256`、required-contract/manifest/inventory/selector/analyzer/scaffold/compile-plan identity、generation identity 和 mutation 时的 `PromotionId`。取得 claim 后必须重读 selector 与 intent previous 做 CAS 前置复核，不相等时 durable 记为 `Indeterminate`。已有 intent 只能 recovery 或 fail closed，不按时间/lease 抢占；token、intent 或 snapshot 不可跨 workspace/进程复用，recovery 必须重算全部 snapshot SHA。
5. commit state 固定为 `NotStarted|Armed|Committed|CompetitionFailed|Indeterminate|NoOp`。只有 durable committed receipt + valid exclusive claim + selector==target 才可补 audit；`CompetitionFailed|Indeterminate` 永不因相同 target 出现而冒领 PromotionId，`NotStarted|Armed` 下 target-equal 也 fail closed。`Missing+selector missing` 与 `Present+selector==previous` 仅在 `NotStarted|Armed` 下判定未线性化；同代 `NoOp` 不产生新 promotion/audit identity。
6. 若存在 selector temp/backup、previous audit state 或派生缓存回收，删除前必须重新计算 Authority/selector 身份并与 intent 绑定的 previous state 匹配。首次发布只能以 no-overwrite atomic create-new/move 提交，已有 selector 只能 `File.Replace`；竞争/未知结果必须 durable 分类，无法证明 intent 独占或 commit 归属时保留证据并 fail closed，不猜测“最近对象”。selector commit 和 committed receipt 必须先于零选择权 audit write。
7. scavenger 只删除固定 work root 下、名称/marker/token/年龄/非 reparse-point 全部通过的协议自有对象；任何超出边界、符号链接、junction、hardlink 异常或身份不明对象都拒绝。
8. 文本 writer 统一 UTF-8 no-BOM + LF；`.gitattributes` 与跨 OS golden 固定 artifact bytes。

### 当前保证边界

- A1-R 只确认逻辑身份与职责边界，不证明 Unity 物理可见性的原子性。
- D0-M1 已证明 materializer 存在直接启动旁路，directory-local UPM generation 也不是事件级不可变目录。
- D0-M2F Run5 只在隔离 fixture 中证明 `ActiveGenerationRef=A`、`manifest=B` 时三程序集全部消费 B；D0-M2T 已进一步证明该 tuple 不能阻止 warm `PackageCache` fallback，故 immutable tarball production 路线已否决。
- 当前只承诺 D0-M2S 和后续 D1/E1 直接验证的进程边界；机器断电、目录 fsync 与同主体并发替换语义仍显式未验证，不能由 `Flush(true)` 推断。

## 冻结的 D1 精确修改边界（已授权）

D0-M2S 已通过且本 ADR 已再次确认，以下 owner 路径现由唯一 D1 owner 领取：

- `Assets/GAS/Editor/CodeGen/Core/GasCodeGenCandidateWorkspace.cs`
- `Assets/GAS/Editor/CodeGen/Core/GasCodeGenCandidateCompileGate.cs`
- `Assets/GAS/Editor/CodeGen/Core/GasCodeGenGenerationStore.cs`
- `Assets/GAS/Editor/CodeGen/Core/GasCodeGenManifest.cs`
- `Assets/GAS/Editor/CodeGen/Core/GasCodeGenPackageDescriptor.cs`
- 新 `Assets/GAS/Editor/CodeGen/Core/GasCodeGenInstallEnvelope.cs` 与 `.meta`
- `Assets/GAS/Editor/CodeGen/GasCodeGenPipeline.cs`
- `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs`
- `Assets/GAS/Editor/CodeGen/Phases/AutoChessDemoCodeGenPhase.cs`
- `Assets/GAS/CodeGen/Analyzers/**`、`Assets/GAS/CodeGen/Selector/**`、stable asmdef/anchors 与迁移所需 `.meta`
- `Tools/GasCodeGenSourceGenerator/**`
- `Tools/GasCodeGenCli/**`
- `Tools/Tests/GasCodeGen/D1/**`
- `TestResults/GasCodeGen/D1/**`
- `Tools/CodeGen/README.md`

`RuntimeV1-Runnable-ClosedWorld` 阶段曾有一个不授权 D1 的窄源码例外，用于把 per-unit Attack 与 Luban 9203 Source Snapshot 收敛到 development generated output；该例外已经完成并只保留为历史。当前 D1 以本节新授权为准，不得据该旧例外保留 legacy development output 作为第二 active 实现。

该例外必须按 producer-first ABI 交接执行：producer 完成后，在旧 `AutoChessGameRoomDefinition` 仍可编译时执行唯一正式生成；确认 generated Attack ABI 后关闭 Unity，再修改 GameRoom/Kernel/test consumer，且不再次生成。禁止以兼容字段、reflection 或双 ABI 绕过这个顺序。

D1 不修改：

- `Assets/GAS/Editor/CodeGen/Semantics/**`
- `Assets/GAS/Editor/CodeGen/Proofs/**`
- `Assets/GAS/Runtime/V1/Install/**`
- `AutoChessGasRuntimeAccess`、`AutoChessBattleDefinitionCatalogBuilder` 等 Runtime consumer/生产接线
- `FullSemanticEligibility` 的计算或 true 值

## 验收条件

第 1、2 项保留历史否决；D0-M2S 与 D0-M2R 已关闭路线门，当前验收对象更新为 D1 的精确 SourceGenerator production migration。

1. D0-M1 的 materializer 与 directory-local UPM slots 否决结论已写入 ADR；D1 不得重新采用这两条路线。
2. D0-M2F 历史 tuple 已被 D0-M2T `RouteRejected` supersede；D0-M2S 随后接受 `StableGraphSourceGenerator` 并授权 D1。
3. SourceGenerator candidate payload、assembly/source/additional-file/define/reference 改动必须由隔离 Unity candidate 图真实编译；active Bee/RSP 不参与 promotion 资格。
4. AutoChess、Runtime 与 Editor generated output 的实际 assembly owner 必须绑定同一 canonical selector bytes；精确路径、analyzer 与 stable scaffold 已由 D0-M2R 冻结。
5. descriptor 单次 byte snapshot、完整 manifest/source-subset binding、selector/analyzer/scaffold binding，以及 `PreviousSelectorState`/target selector 复核在故障注入中保持有效。
6. 路线所需的 scaffold/selector/archive/blob bytes 全部进入可复核 generation 身份；缺失、重复、漂移或不受信输入 fail closed。
7. 旧双根 promotion、公开 path promotion、未 sealed token Stage/Commit 均不可达。
8. E1 覆盖 Missing 成功/中断、Present replace/中断、完整 NotStarted intent CreateNew+flush 后而 claim 后重读前的中断、claim 后 CAS previous 漂移、committed receipt 后 audit 失败、same-target no-op、Present missing、corrupt/unknown、相同/不同 target 竞争、indeterminate target-equal 与 repeated recovery；scaffold missing/byte drift 必须在 direct Unity 下硬失败且不改 selector。
9. crash cleanup 只清理协议精确拥有的目录；机器断电保证边界在日志和 README 中与实现一致。
10. `FullSemanticEligibility` 仍为 `false`，proof hash 未闭合时为空；没有 Semantics/Proofs/Runtime consumer diff。
11. 所有 SourceGen、shadow Unity compile、Unity import、EditMode/PlayMode 和 CLI fault 验证串行执行。

## 路线裁决汇总

### Stable assembly graph + SourceGenerator

已接受。正式 D0-M2S RunId `D0M2S-20260830T180951Z-fe75df12b9a2` 已通过 full-fault、SG-specific X、全部八个 case 与证据闭包；D1 按本 ADR 的 exact selector/analyzer/scaffold 协议实施。

### Immutable tarball selector

production 路线否决。正式 D0-M2T 在 warm B 后移除 Authority archive，Unity 仍从 `PackageCache` 成功消费 B，terminal 为 `RouteRejected / MissingOrCorruptAuthorityAccepted`。

### 启动前 materializer

否决。直接 `Unity.exe` 可以绕过启动门，并在目录交换强杀后观察 Missing 根；无法满足 active generation byte-for-byte 不变。

### Directory-local immutable UPM slots

否决。Unity `DirectoryMonitor` 在 resolve、compile 与 domain reload 期间会对 generation 目录产生瞬态写事件；首尾 tree hash 相同不能证明 generation 不可变。

### 继续双根并依次 rename

否决。文件系统无法把两个目录 rename 组成一个 Unity 可见原子操作，进程崩溃与 AssetDatabase watcher 都可能观察到混代。

### 为 AutoChess generated 新建 asmdef

否决。会改变 `internal` 可见性和依赖方向，迫使修改 D 无权触碰的 Runtime consumer，并产生第四个 generated assembly。

### 只替换 active Bee/RSP 中的 generated source

否决。无法证明 candidate 自己的 `.asmdef/.asmref`、define、platform、reference 与 source membership，且可消费 stale Bee 图。

### 把 envelope 生成为参与 ArtifactManifestHash 的 `.gen.cs`

否决。envelope 必须携带最终 `ArtifactManifestHash`；若自身 bytes 又进入该 hash，会形成自哈希闭环。envelope 必须作为 generation 控制面封存物，由 Build/Bootstrap 边界注入。

### 让 Runtime 读取 ProjectSettings 或 active 目录猜测当前代

否决。Player 不应依赖 Editor 控制面路径，且这会恢复 selector/目录双事实源与 Runtime fallback。

## 影响与风险

- 已确认：generation identity、程序集归属目标与 envelope 逻辑是已接受 SourceGenerator production 路线必须满足的不变量；production 路线已经选定，install admission 尚未评估。
- 已否决：materializer 不是不可绕过的启动屏障；directory-local UPM slots 不是事件级不可变存储；immutable tarball + manifest 不能阻止 warm `PackageCache` fallback。
- 当前影响：D1、active source migration 与 production selector promotion 已获授权；immutable tarball 和双 active root 路线继续禁止。
- 下一风险：D1 必须为 generator DLL load failure 增加 stable hard-guard anchor，并证明 legacy `.gen.cs`、duplicate additional-file 与 stale compile/cache 均不能成为旁路。
- 验证成本：物理路线实验与最终 candidate gate 都必须使用独立 Bee 图并串行执行，速度低于 RSP 近似编译，但不能以速度换取错误资格。

## 官方依据

- Unity 6：Assembly Definition Reference 可将一个目录中的脚本显式加入既有程序集：<https://docs.unity3d.com/cn/6000.0/Manual/assembly-definitions-creating.html>
- Unity Assembly Definition 文件格式：`.asmref` 可用 assembly name 或 asmdef asset GUID 引用；当前已接受路线的精确 assembly graph 与 scaffold bytes 由 D0-M2R 冻结：<https://docs.unity3d.com/kr/2022.3/Manual/AssemblyDefinitionFileFormat.html>
- Unity AssetDatabase refresh 会发现文件变化，并导入/编译 `.asmdef`、`.asmref`、`.rsp` 与 `.cs`：<https://docs.unity3d.com/kr/current/Manual/AssetDatabaseRefreshing.html>
- `AssetDatabase.DisallowAutoRefresh` 使用成对计数，且不能阻止显式 `AssetDatabase.Refresh`：<https://docs.unity3d.com/ja/current/ScriptReference/AssetDatabase.DisallowAutoRefresh.html>
- `EditorApplication.LockReloadAssemblies`/`UnlockReloadAssemblies` 必须成对使用：<https://docs.unity3d.com/cn/2023.2/ScriptReference/EditorApplication.LockReloadAssemblies.html>
- Windows `MoveFileEx(MOVEFILE_REPLACE_EXISTING)` 在目标是既有目录时明确报错，目录移动要求目标不存在，因此单根替换仍是两次操作：<https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexa>
- Windows `ReplaceFile` 只替换普通文件，不能把非空目录交换当成一次 file replace：<https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilea>

## 确认门

D0-M2F tuple 是历史候选裁决；D0-M2T 已技术否决 tarball；D0-M2S 与本次 D0-M2R 已完成恢复路线确认。当前门更新为：

1. `AcceptedProductionRoute=StableGraphSourceGenerator`；immutable tarball、materializer、directory-local UPM slots 与双 active root 继续禁止。
2. `AcceptedSoleUnityConsumedSelector=Assets/GAS/CodeGen/Selector/Generation.GasCodeGenSourceGenerator.additionalfile`；ordinary promotion 只原子替换这一普通文件。
3. `D1Authorized=true`、`ProductionMigrationAuthorized=true`；下一门为 D1 production migration + route-specific E1，不再重复 D0-M2S。
4. `ProductionInstallAdmission=NotEvaluated`、`DeclaredFullSemanticEligibility=false`；D1 不修改 Semantics、Proofs、Runtime admission 或 eligibility true 值。
5. `RuntimeV1-Runnable-ClosedWorld` 状态不变；只承诺实验和 D1/E1 直接验证的进程边界，不承诺机器断电、目录 fsync 或同主体并发路径替换语义。
