# Runtime V1.1 D1 SourceGenerator 单轮迁移与可运行收口计划

> 日期：2026-08-31  
> 目标：一轮完成 D1 production migration，并让既有 Runtime V1 Development Player 再次跑通  
> 原则：只做唯一 selector 迁移、route-specific E1 和受影响回归；不扩功能、不重跑无关全量门
> 状态：已完成；最终结果见[生产迁移与可运行验收结果](RuntimeV1.1-D1-SourceGenerator生产迁移与可运行验收结果.md)

## 1. 结论

Runtime V1 功能版已经完成，不需要再开一轮补玩法：权威 RunId `RV1-20260830T070718Z-ab754759ec68` 已通过 RuntimeV1Runnable EditMode 11/11、PlayMode 5/5 和 StandaloneWindows64 Development Player，Ability、production GE 9203、AutoChess 三向量均为绿色。

当前唯一必须完成的工程债是 D1：production 仍以两个物理 generated root 和 active `.gen.cs` 消费 generation，尚未迁移到 D0-M2R 已接受的 `StableGraphSourceGenerator`。下一轮只完成这一迁移，并用一次 production-path E1、一次主工程 targeted Edit/Play 和一次 Player build/run 验收。完成后可声明：

```text
RuntimeV1-Runnable-ClosedWorld = Passed（保持）
AcceptedProductionRoute = StableGraphSourceGenerator
D1-SourceGeneratorProductionMigration = Passed
ProductionInstallAdmission = NotEvaluated
DeclaredFullSemanticEligibility = false
```

这不是“正式 Release V1 全部完成”。13 个 Tier-B backlog、B1-R/C1/Z、Release/IL2CPP、Profiler/Journaling、完整 UE-GAS 语义与 `FullSemanticEligibility=true` 继续后置；把它们塞进本轮会再次拖慢“先完成、先跑起来”的目标。

## 2. 当前实现盘点

| 领域 | 当前事实 | 本轮动作 |
|---|---|---|
| Runtime V1 功能 | `RuntimeV1-Runnable-ClosedWorld` 已交付；Edit 11/11、Play 5/5、Player exit 0 | 不新增功能，只在 migration 后重跑同一最小门 |
| CodeGen emitter | 现有 pipeline 已能从 Luban/normalized rows 生成 Runtime、Editor、AutoChess 精确 C# bytes 与 manifest | 直接复用，不重写业务生成算法 |
| Candidate/descriptor/store | workspace、compile gate、descriptor、generation store、intent/audit 已存在 | 收敛为 single selector bundle；删除双 root promotion 与 active RSP 近似资格 |
| 物理 route | D0-M1 两路线与 D0-M2T tarball 已否决；D0-M2S fresh Run5 全绿 | 不再做候选选路或重复 D0-M2S |
| production selector | 尚不存在；当前 active `.gen.cs` 仍被 Unity 直接消费 | 新增 exact selector/analyzer/scaffold 并完成一次性迁移 |
| SourceGenerator | 只有 disposable D0-M2F/D0-M2S canary | 实现最小 bundle validator/dispatcher，不在 generator 内重写 semantic compiler |
| E1 | 当前 E0 只覆盖旧 generation/ref/intent synthetic boundary | 新增 selector/analyzer/duplicate/legacy/stale fault slice |
| 完整语义与 admission | A1-R 有冻结输入；B/C/Z 未闭合，eligibility 为 false | 本轮不碰，明确保持 false/NotEvaluated |

## 3. 单轮范围

### 必做

1. 实现 `GasSourceBundle-v1` canonical codec、bundle validator 与 `GasCodeGenSourceGenerator`，同时绑定 exact `RequiredArtifactSetContractHash`、完整 `ArtifactManifestHash` 和 C# subset 的 `SourceArtifactInventoryHash`。
2. 让现有 emitter 的完整 C# bytes 打包进唯一 canonical `.additionalfile`，descriptor 封存完整 candidate manifest bytes，并证明 required C# items 与 bundle entries 双向 bijection，不再复制为 production active `.gen.cs`。
3. 把 candidate compile gate 改为 disposable Unity project 的 exact production selector/analyzer/scaffold 图，不再改写 active Bee/RSP 近似 candidate。
4. 把 Store 的线性化点改为 canonical selector 单文件提交：fixed intent 以 `FileMode.CreateNew` 取得单 active claim，已有 selector 使用 `File.Replace`，首次发布使用 no-overwrite atomic create-new/move；只有 committed receipt + valid claim + selector==target 才补 audit/ref。
5. 加入 Runtime、Editor、AutoChess 三个 required anchors；generator 未运行时普通编译必须硬失败。
6. 删除或降权双 active root rename、公开 path promotion、legacy active `.gen.cs` 与 fallback 入口；不保留双实现兼容期。
7. 执行 route-specific E1、主工程 targeted Edit/Play 和一次 Development Player build/run。

### 明确不做

- 不再运行 D0-M2F、D0-M2T 或 D0-M2S 正式矩阵。
- 不新增 Ability、GE、Tag、Cue、Prediction 或 AutoChess 功能。
- 不领取 B1-R、C1、A1-S、Z 或 E2。
- 不修改 `Semantics/**`、`Proofs/**`、`Runtime/V1/Install/**` 或 eligibility true 逻辑。
- 不跑完整 Tier-B、全量 EditMode/PlayMode、Release/IL2CPP、Profiler 或 Journaling。
- 不为 legacy `.gen.cs`、tarball、manifest selector、双 root 或 stable alias 保留兼容 fallback。

## 4. 并行任务链

### D1-A：Bundle 与 Generator

独占写集：

```text
Tools/GasCodeGenSourceGenerator/**
Tools/Tests/GasCodeGen/D1/Codec/**
```

交付：

- C# 9 / netstandard2.0、Microsoft.CodeAnalysis.CSharp 4.3 compatible generator project；
- `GasSourceBundle-v1` 精确 wire reader/validator，验证 `EX-GAS-RuntimeV1-RequiredArtifacts-v2` contract hash，并区分完整 `ArtifactManifestHash` 与 `SourceArtifactInventoryHash`；
- 按 assembly name 分发 entries、canonical `AddSource` 与冻结的 `GAS.Generated.CodeGen.GasCodeGenSourceGeneratorMarker` ABI；保留 marker hint 不得被 bundle 占用；
- 从 selector path 反算工程根并在 `AddSource` 前重算 analyzer/scaffold identity；analyzer 完全未加载时由 anchor 硬失败；
- missing/corrupt/noncanonical Base64、重复 target/hint、hash/length/order、required source missing/extra/target-hint drift、source self/inventory hash 负例；
- deterministic DLL、source 与测试 SHA 清单。

禁止：修改 production pipeline/store/descriptor、复制 DLL 到 `Assets`、启动 Unity、读取 legacy active `.gen.cs` 作为 fallback。

可复制提示词：

> 执行 `D1-A Bundle 与 Generator`。先读 ADR-0001 与 `RuntimeV1.1-D0-M2R-SourceGenerator路线接受与D1授权裁决.md`。只写 `Tools/GasCodeGenSourceGenerator/**` 和 `Tools/Tests/GasCodeGen/D1/Codec/**`；逐字段实现已冻结的 C# 9/netstandard2.0、Microsoft.CodeAnalysis.CSharp 4.3 compatible `GasSourceBundle-v1` wire reader/validator与 `GasCodeGenSourceGenerator`，验证 `EX-GAS-RuntimeV1-RequiredArtifacts-v2` exact contract/hash，严格区分完整 `ArtifactManifestHash` 和 C# subset `SourceArtifactInventoryHash`，按三目标 assembly 分发 exact source entries 并生成 required marker；从 selector path 反算工程根，在 `AddSource` 前重算 analyzer/scaffold identity。覆盖 required source missing/extra/target-hint drift 与 source self-hash 等 canonical/negative unit tests；完整 manifest↔source 双向 mapping 由 D1-B descriptor tests 负责。禁止修改 production 控制面、Assets、Semantics/Proofs/Runtime Install，禁止启动 Unity。完成后交付文件/SHA、测试命令和 P0/P1，停止等待集成。

### D1-B：Selector 控制面

独占写集：

```text
Assets/GAS/Editor/CodeGen/Core/GasCodeGenCandidateWorkspace.cs
Assets/GAS/Editor/CodeGen/Core/GasCodeGenCandidateCompileGate.cs
Assets/GAS/Editor/CodeGen/Core/GasCodeGenGenerationStore.cs
Assets/GAS/Editor/CodeGen/Core/GasCodeGenManifest.cs
Assets/GAS/Editor/CodeGen/Core/GasCodeGenPackageDescriptor.cs
Assets/GAS/Editor/CodeGen/Core/GasCodeGenInstallEnvelope.cs
Assets/GAS/Editor/CodeGen/Core/GasCodeGenInstallEnvelope.cs.meta
Assets/GAS/Editor/CodeGen/GasCodeGenPipeline.cs
Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs
Assets/GAS/Editor/CodeGen/Phases/AutoChessDemoCodeGenPhase.cs
Tools/GasCodeGenCli/**
Tools/CodeGen/README.md
```

交付：

- emitter source bytes → one bundle candidate；
- descriptor v3 / envelope v2 的 exact required-set contract、完整 manifest、`SourceArtifactInventoryHash`、required C#↔bundle bijection 与 selector/analyzer/scaffold/compile-plan binding；旧版本直接拒绝，不双写；
- claim 前读取 canonical selector 完整 previous snapshot，并把 `CommitAttemptState=NotStarted`、`PreviousSelectorState=Missing|Present`、Present-only previous bytes、`DescriptorSha256 / InstallEnvelopeSha256` 与全部 route snapshot 通过 fixed intent `FileMode.CreateNew` 一次性写入并 flush；claim 后重读 selector 做 CAS 前置复核、single-file atomic commit、durable committed receipt、post-commit audit；
- `NotStarted|Armed|Committed|CompetitionFailed|Indeterminate|NoOp` recovery matrix 与 explicit rollback；
- descriptor/manifest negative tests：required-set 缩窄/扩张、6 个 required item 任一 managed `.meta` 缺失或 byte-length/SHA 不一致、required C#↔bundle 任一方向漏映射、length/hash/category/target/hint 不一致；
- 删除/降权 `PromoteAll`、`PromoteDirectory`、public path Stage/Commit 和 active RSP promotion token。

禁止：新增 production analyzer/selector/anchor 文件、修改 generator 工具、启动 Unity、保留双事实源兼容层。

可复制提示词：

> 执行 `D1-B Selector 控制面`。先读 ADR-0001 与 D0-M2R SourceGenerator 接受裁决。只写列出的 CandidateWorkspace/CompileGate/GenerationStore/PackageDescriptor/Pipeline/AutoChess phase/CLI/README 路径。复用现有 emitter，把 exact source bytes 封装为一个 selector candidate；升级并验证 `EX-GAS-RuntimeV1-RequiredArtifacts-v2` contract hash，保留完整 `ArtifactManifestHash` 语义，新增 `SourceArtifactInventoryHash` 与 required C#↔bundle bijection。Store 必须先读取 canonical selector 完整 previous snapshot，再以 fixed intent `FileMode.CreateNew` 将 `CommitAttemptState=NotStarted`、`PreviousSelectorState=Missing|Present`、Present-only previous bytes、`DescriptorSha256 / InstallEnvelopeSha256` 与全部 route snapshot 一次性写入并 flush 以获取 exclusive claim；claim 后重读 selector 做 CAS 前置复核，漂移即 durable `Indeterminate`，禁止先建空 previous claim 再补写。随后驱动首次 create-new 或已有 selector replace；实现六态 commit/recovery，competition/indeterminate 或无 committed receipt 不得冒领 PromotionId，audit 选择权为零。删除双 root/public path/active RSP promotion 旁路；保持 `FullSemanticEligibility=false`，不修改 Semantics/Proofs/Runtime Install，不启动 Unity。用静态和非 Unity 单元门覆盖 required-set 缩窄/扩张、6 个 required item 任一 managed `.meta` 缺失/byte-length/SHA 错配、双向漏映射、length/hash/target/hint/category 错配、snapshot 漂移与六态 recovery，交付 diff、P0/P1 与待集成 ABI。

### D1-C：E1 Oracle 与 Production Scaffold 模板

独占写集：

```text
Tools/Tests/GasCodeGen/D1/E1/**
Tools/Tests/GasCodeGen/D1/Scaffold/**
```

交付：

- exact production relative paths 的 disposable fixture/scaffold；
- transaction cases：Missing success/interrupted、Present replace/interrupted、完整 NotStarted intent CreateNew+flush 后而 claim 后重读前的中断、claim 后 CAS previous 漂移、committed receipt 后 audit 失败、same-target NoOp、Present missing、corrupt/unknown、相同/不同 target competing create、indeterminate target-equal 与 repeated recovery；
- direct-Unity cases：scaffold missing/byte drift、selector missing/corrupt、analyzer missing/corrupt、duplicate selector、legacy active `.gen.cs`、stale A Bee/RSP、三程序集共同 selector/marker、cleanup/closure；所有负例断言 invocation 非零、无 `AddSource`/promotion 且 selector 不变；
- generator load warning + missing marker 必须使外层 invocation 非零的 oracle；
- 只读 production protected-path oracle 和 typed terminal schema。

禁止：修改 D0-M2S 历史 harness/evidence、写 production Assets/控制面、启动正式 Unity；Unity 执行由 root 在集成阶段持有唯一 lease。

可复制提示词：

> 执行 `D1-C E1 Oracle 与 Scaffold`。只写 `Tools/Tests/GasCodeGen/D1/E1/**` 与 `Tools/Tests/GasCodeGen/D1/Scaffold/**`。按 D0-M2R exact production selector/analyzer/scaffold 路径定义 disposable fixture、transaction matrix、direct-Unity fixed cases、typed terminal、protected/tool/cleanup/evidence closure oracle；覆盖 Missing/Present/六态 recovery、完整 NotStarted intent CreateNew+flush 后而 claim 后重读前的中断、claim 后 CAS previous 漂移、相同与不同 target competing create、unknown/repeated recovery、scaffold missing/byte drift，并特别覆盖 analyzer corrupt 只产生 CS8034 时 required marker 仍让外层 invocation 硬失败。所有负例断言非零、无 AddSource/promotion、selector 不变。不得修改 D0-M2S 历史、production Assets/控制面，不启动 Unity。交付 AST/static 结果、P0/P1、正式执行入口和预期 case 表。

### D1-J：唯一集成与 Unity Lease

仅 root/唯一集成 owner 执行，等待 A/B/C 冻结 SHA 后开始。独占写集：

```text
Assets/GAS/CodeGen/Analyzers/**
Assets/GAS/CodeGen/Selector/**
Assets/GAS/Generated/CodeGen/{Runtime,Editor}/** 的 migration hunk
Assets/AutoChessDemo/Generated/** 的 migration hunk
TestResults/GasCodeGen/D1/** 的正式 evidence/provenance
```

职责：先把 analyzer DLL/meta、canonical selector、三 anchors 与控制面集成为 disposable staged production bytes，串行执行 E1；只有 E1 全绿后才把同一冻结 bytes 迁入 production Assets、删除 legacy active `.gen.cs`，随后执行主工程 Unity/Player 并形成最终 evidence。

## 5. Join 与执行顺序

```text
J0 已完成：D0-M2S Passed + D0-M2R/ADR 冻结
  ├─ D1-A Bundle/Generator ─┐
  ├─ D1-B Selector Control ├─> J1 ABI/SHA 冻结
  └─ D1-C E1 Oracle ───────┘
                                -> D1-J staged integration candidate
                                -> E1 disposable Unity
                                -> D1-J production migration（复用同一冻结 bytes）
                                -> 主工程 compile
                                -> RuntimeV1Runnable Edit/Play
                                -> Development Player build/run
                                -> J2 停顿审查与结果落盘
```

所有并行链都不得启动 Unity；只有 D1-J 持有 Unity lease。A/B/C 如果需要共同类型，只先提交最小 ABI 文档或接口文件，由 root 在 J1 一次冻结，禁止互相改对方写集。

## 6. 最小验证

| 门 | 必须结果 | 不做的扩展 |
|---|---|---|
| generator/codec | build 0 error；wire、exact required-set contract、6 个 required managed `.meta` 完整性、完整 manifest/source subset bijection、self-hash negative tests 全绿 | 不测业务玩法 |
| E1 disposable Unity | 完整 transaction matrix 与 scaffold missing/drift 等 direct-Unity cases 全绿；三程序集共同 selector/marker；负例非零、无 AddSource/promotion、selector 不变；cleanup/closure 通过 | 不重跑 D0-M2S |
| 主工程 compile | Unity `6000.3.14f1` exit 0；无 compiler error、analyzer load warning、duplicate type | 不跑全量 EditMode |
| RuntimeV1Runnable EditMode | 11/11、0 failed、0 skipped | 不扩 filter |
| RuntimeV1Runnable PlayMode | 5/5、0 failed、0 skipped | 不扩 filter |
| Development Player | fresh build + launch exit 0；Ability、GE 9203、AutoChess 均 passed | 不做 Release/IL2CPP |
| 身份/清理 | selector/analyzer/scaffold/descriptor/compile-plan/provenance 闭包；无 Unity/Bee/temp residue | 不制造全仓库新 fingerprint 体系 |

不得把 `-quit` 与 Unity `-runTests` 同用。已绿色的 D0-M2S、历史 E0、全量 conformance 和无关测试不重复运行。

## 7. 验收标准

D1 只有同时满足以下条件才完成：

1. production exact selector/analyzer 路径存在且命名匹配 Unity 规则；
2. Runtime、Editor、AutoChess 三 compilation 都只消费同一 selector bytes；
3. analyzer 未运行时 required anchor 使编译硬失败；
4. generation-dependent active `.gen.cs` 为零，legacy 双 root promotion 和 fallback API 不可达；
5. ordinary promotion 先取得 fixed intent exclusive claim；已有 selector 只原子替换，首次发布只做 no-overwrite atomic create-new/move，只有 committed receipt 可补 audit；competition/indeterminate 不冒领且 audit/ref/cache 不改变选择结果；
6. E1 transaction/scaffold/fault fixed cases、cleanup 与 evidence closure 全绿；
7. RuntimeV1Runnable Edit/Play 与 Development Player 三向量保持绿色；
8. `ProductionInstallAdmission=NotEvaluated`、`DeclaredFullSemanticEligibility=false`，没有越权修改。

出现以下任一条件立即停止，不做兼容补丁：需要第二个 Unity-consumed selector、必须同时保留 legacy `.gen.cs` 才能编译、需要为 AutoChess 新建第四程序集、required set/source subset 无法双向闭合、竞争 intent 可以冒领 PromotionId、audit/cache 可以恢复 generation、或无法让 analyzer/scaffold load failure 硬失败。

## 8. 交付结果文档

本轮结束只新增一份结果：`RuntimeV1.1-D1-SourceGenerator生产迁移与可运行验收结果.md`。它必须绑定 A/B/C/J 冻结 SHA、E1 terminal、主工程 compile、Edit/Play XML/provenance、Player result 与 cleanup，不重复撰写新的候选选路讨论。
