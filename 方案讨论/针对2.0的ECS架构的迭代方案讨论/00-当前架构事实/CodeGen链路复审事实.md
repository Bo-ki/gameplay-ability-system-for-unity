# CodeGen 链路复审事实

> 归属：本文件只记录 CodeGen 到 Runtime 的当前事实、历史计划与现实偏差、P0/P1 违约证据，以及按 Unity DOTS 官方规则得出的事实判断。纯目标态链路见 `../01-目标态架构共识/14-DefinitionCodeGen目标链路Spec.md` 和 `../01-目标态架构共识/15-SourceGenerator职责边界Spec.md`；可执行拆分写入任务树，不写在本文件内。

## 2026-06-07 目录归位说明

旧的 `01-目标态架构共识/14-CodeGen到Runtime新链路重构计划.md` 混合了承诺链路、当前违约、下一轮目标和验收点，不适合继续作为目标态 Spec。当前归位规则如下：

| 信息类型 | Owner | 说明 |
|---|---|---|
| 当前 CodeGen pipeline 事实、generated artifact 清单、违约点 | 本文件 | 作为事实和诊断证据保留 |
| Definition CodeGen 目标数据流、允许/禁止 artifact、Runtime 消费契约 | `../01-目标态架构共识/14-DefinitionCodeGen目标链路Spec.md` | 作为框架设计 Spec |
| SourceGenerator 权限边界、generated lifecycle 禁止项、validation gate 目标值 | `../01-目标态架构共识/15-SourceGenerator职责边界Spec.md` | 作为框架设计 Spec |
| Core pipeline 默认化、validation gate 扩展、catalog bootstrap 收口等执行项 | `../02-主线任务树/` 或当前进度目录 | 作为任务，不在事实文档展开 |

## 审查边界

本文件以 `../01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`、`../01-目标态架构共识/14-DefinitionCodeGen目标链路Spec.md`、`../01-目标态架构共识/15-SourceGenerator职责边界Spec.md` 和 `../../UnityDOTS官方文档参考/主题/90-规则编号索引.md` 作为判定标准，审查当前生成链路是否满足：

1. Core pipeline 不被 Demo phase 污染。
2. Runtime-visible generated artifact 不引用 managed row、JSON、`cfg.*` 或 Editor registry。
3. SourceGenerator 不生成 Runtime Core lifecycle、system registration、hidden query、hidden ECB、NativeContainer owner。
4. Runtime Core 只消费 generated id、Blob schema、`GASDefinitionCatalogBlob`、lookup、pure Runtime definition glue 和 validation artifact。

本文件中的“必须”“不得”“后续约束”都是从当前事实导出的整改约束，不表示目标态设计正文已经在本文件内成立。

## 当前正向事实

1. `GenerateAllCode()` 已接入 `BeanUpdater + GasCodeGenPipeline`，并通过 process gate 统一驱动 Luban JSON/C# export 与 GAS CodeGen。
2. Luban C# 保留在 Unity 编译域，generated Runtime 不反向依赖 `cfg.*` / `Luban.Runtime` / `SimpleJSON`。
3. `DefinitionCatalog.gen.cs` 已生成 `GASDefinitionCatalogBlob`、sorted code lookup、`TryGetAbilityIndex()` / `TryGetGameplayEffectIndex()` 和 `ref readonly` definition 访问。
4. `RuntimeDefinitionGluePhase` 当前只输出 `RuntimeDefinitionGlue.gen.cs`，其中 `GASGeneratedRuntimeDefinitionResolver`、Requirement / Magnitude evaluator、record glue 是正向资产：它们把 definition index/range 转成 frame-local record，不需要 managed row、JSON 或 `Dictionary`。
5. `GasCodeGenPipeline.s_corePhases` 已不再包含 `AutoChessDemoConfigPhase`；Demo 生成改由 `s_autoChessDemoPhases` standalone 写入 `Assets/AutoChessDemo/Generated`。
6. `RuntimeLifecycleMigrationPhase` 当前不再保留 manifest `RuntimeLifecycleMigration` artifact；`RuntimeActiveEffect.gen.cs`、`RuntimeAbilityActivation.gen.cs` 与 `RuntimeEffectInstant.gen.cs` 均已退为 12 行级 marker / pure glue artifact，manifest 以 `ArtifactCategory=RuntimePureGlue` 分类。真实 active-effect helper/job/snapshot/mutation/tick/remove 代码已迁到手写 `Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs`，并由 `GEActiveEffectLifecycleSystems.cs` 的 Runtime systems 调度。Instant effect 的真实 runtime owner 已转到手写 `Assets/GAS/Runtime/System/Effect/GEEffectInstantSystems.cs`。
7. `ActiveEffectLifecycleOwnerSystems.cs` 当前已从磁盘文件列表、SourceGenerator 输出、manifest、validation report 和 schedule type-name 列表退出；`rg --files Assets/GAS/Generated/CodeGen/Runtime` 当前只剩 `RuntimeActiveEffect.gen.cs` marker，不再有该 companion 文件。它仍应保留为防回流扫描项，而不是当前残留事实。
8. `GasCodeGenValidationReport.md` 当前不仅输出 forbidden dependency 和 naming debt 命中数，也输出 generated runtime boundary hits；其中 `GeneratedRuntimeSystemRegistrationHits = 0` 是 SourceGenerator 不自注册的正向事实。当前 report 已收敛到 `GeneratedRuntimeBoundaryHits = 0`、`GeneratedRuntimeOwnershipHits = 0`、`GeneratedRuntimeStructuralChangeHits = 0`、`GeneratedRuntimeRandomWriteLookupHits = 0`、`GeneratedRuntimeManagedConfigHits = 0`、`GeneratedRuntimeUnclassifiedBoundaryHits = 0`。手写 `GASSystemScheduleContract.AddSystemsByTypeName()` 当前对缺失 generated type 已 fail-fast 抛错，不再静默跳过。剩余证据缺口转为 catalog lifetime / dispose owner、缺失 artifact / type mismatch / assembly unavailable 的负例验证、system 数量、phase budget、manifest/report/file/schedule 对账和 release-ready gate。
9. TagRequirement all-any-none 已进入 CodeGen 正向链路，并形成 generated catalog / pure evaluator 正向证据；`RuntimeEffectInstant.gen.cs` 已退为 `RuntimePureGlue` marker，instant target tag lookup / spec build / reduce 当前由手写 `GEEffectInstantSystems.cs` 承接。完整证据链、Run5 验证边界和重复维护裁决见 [架构重划分审查事实/08-TagRequirementQueryDefinitionGlue事实.md](架构重划分审查事实/08-TagRequirementQueryDefinitionGlue事实.md)。

## 2026-06-08 CodeGen Module depth / artifact responsibility 续审事实

本轮用 codedb 复核 CodeGen pipeline、manifest、validation gate、generated catalog、pure glue 消费面、hand-written instant owner 和 active-effect Runtime owner。当前 codedb 截面为 434 files / 434 outlines / 766 chunks / graph 448 nodes / 1831 edges / 34 communities / scan ready；Runtime module-map 主社群为 146 files / 2227 indexed symbols，dependency edges internal 604 / boundary 133 / incoming 101 / outgoing 32。该数字只代表本轮复核输入，后续领取必须重跑。

| 审查面 | 当前证据 | 事实判定 |
|---|---|---|
| CodeGen phase 聚合度 | `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs` 当前为 5556 行级大聚合，单文件内同时包含 asmdef、definition index、Blob schema、static lookup、catalog、runtime definition glue、runtime lifecycle migration marker、Baker glue、component type set、query layout、validation report 和多类 boundary collector | CodeGen 已有统一 pipeline 的正向价值，且 runtime lifecycle 模板体量已明显收缩；但 `GasGlueCodeGenPhases` 仍是浅 Interface / 厚 Implementation 大聚合。后续重构不应拆成多个重复扫描器，而应按 artifact responsibility 抽出 phase owner、template owner 和 validation rule owner |
| manifest 分类能力 | `GasCodeGenManifest.AddGeneratedFile(...)` 已支持 `ArtifactCategory`；`GasGlueCodeGenPhases` 的 phase base 已有 `AddRuntimePureGlueManifest(...)` 与 `AddRuntimeLifecycleMigrationManifest(...)`；manifest JSON 中 `RuntimeDefinitionGlue.gen.cs`、`RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs` 与 `RuntimeActiveEffect.gen.cs` 均为 `RuntimePureGlue`，当前 `RuntimeLifecycleMigration` artifact 为 0 | manifest 已成为 generated artifact 责任分类事实源。后续 R5 必须把 manifest category、validation report、模板输出和 runtime consumer 四者对账；不能只看文件名或目录 |
| validation report 分类能力 | `ValidationReportPhase` 输出 `GeneratedRuntimeBoundaryHits`、pure glue / lifecycle migration artifact 数、lifecycle / registration / structural / ownership / random lookup / managed config hit 数，并在 unclassified hit 大于 0 时抛异常；当前 generated boundary / lifecycle / registration / structural / ownership / random lookup / managed config / unclassified 分项均为 0 | gate 已从 forbidden dependency 扩展到职责边界；当前数字是正向事实，但 release-ready 仍要补 catalog lifetime / dispose owner、负例验证、system budget、file/schedule 对账和防回流扫描 |
| validation report 可读性 | report 的 Manifest Entries 表当前已列出 Phase / File / Layer / RuntimeVisible / `ArtifactCategory` / VersionControlled；manifest JSON 仍是 category 的机器可读事实源 | R5 交还应同时贴 report 与 manifest JSON，对账 marker、pure glue、lifecycle migration 和 bootstrap materialization；不能只看文件名或目录 |
| pure glue 消费面 | `GASGeneratedRuntimeDefinitionResolver` 当前没有 runtime caller；`GASGeneratedRequirementEvaluator` 当前 0 caller；`GASGeneratedMagnitudeEvaluator` 只被 generated definition resolver 内部调用。ability activation、instant effect 和 active-effect runtime 的实际 consumer 已转向手写 `GASRuntimeDefinitionResolver`、`GASRuntimeRequirementEvaluator` 与 `GASRuntimeMagnitudeEvaluator`，调用点覆盖 `AbilityCommitSystem`、`GEEffectInstantSystems.cs` 和 `GASActiveEffectRuntime.cs` | pure glue 本身是可保留资产，但当前更像未调度 / 内部自用 glue；R5 必须决定保留接口、删除死胶水还是收窄为 generated definition record helper，不能把 0 caller 写成完成证明 |
| catalog materialization 消费面 | `GASGeneratedDefinitionCatalogBuilder.BuildCatalog(...)` 被 `AutoChessBattleDefinitionCatalogBuilder.Install(...)` 用于 runtime/bootstrap install，并被 generated Editor Baker 的 `Bake(...)` 调用后 `AddBlobAsset(...)` | catalog builder 有 Baking / Bootstrap 低频消费证据，符合 `BLOB-02` 下限；但 runtime-visible builder 仍要绑定 lifetime / dispose owner，不能成为 hot path 可调用能力 |
| generated catalog runtime 消费面 | `DefinitionCatalog.gen.cs` 以文件依赖被手写 `GEEffectInstantSystems.cs`、`GASActiveEffectRuntime.cs` 和 Runtime active-effect lifecycle systems 消费；generated runtime 目录当前不再有 active-effect companion 文件 | immutable catalog / lookup 已进入真实 runtime 执行链；同时也说明 generated pure glue 与手写 Runtime owner 必须继续按 Runtime Core 规则对账 |
| Luban normalized row 边界 | `LubanNormalizedRowBootstrap` 生成 Editor-only row struct / row factory literal；validation report 的 normalized row boundary hit 当前为 0 | normalized row 作为 Editor / CodeGen 输入方向正确；不能把 row factory 或 managed row 直接作为 Runtime Core lookup |
| Demo phase 分离 | `GasCodeGenPipeline.s_corePhases` 不含 `AutoChessDemoConfigPhase`，`TryRunAll()` 先跑 Core 再跑 Demo standalone；`AutoChessDemoConfigPhase` 写入 `Assets/AutoChessDemo/Generated` 并属于 Demo sourcegen pass | Core / Demo phase 旧 P0 已缓解；但 `TryRunAll()` 是复合入口，交还时仍要分 Core validation evidence 与 Demo scenario/config evidence |

本轮 CodeGen 结论：当前链路的正确方向是“统一 pipeline + manifest 分类 + validation gate + generated immutable catalog / pure glue”。当前正向变化是 ability activation、instant effect 与 active-effect runtime 代码均已从 generated lifecycle 输出退向手写 Runtime owner，`RuntimeActiveEffect.gen.cs` 也已成为 `RuntimePureGlue` marker，`ActiveEffectLifecycleOwnerSystems.cs` 已从 generated runtime 物理目录退出；当前不足转为大模板 Module 深度不够、generated pure glue 与手写 resolver 职责需要对账、catalog lifetime / dispose owner 证据、system budget 与 release-ready 负例验证缺失。因此 R5 不能以 `RuntimeForbiddenDependencyHits=0`、`GeneratedRuntimeBoundaryHits=0`、`GeneratedRuntimeUnclassifiedBoundaryHits=0`、`RuntimePureGlueArtifacts=4` 或 `RuntimeActiveEffect.gen.cs` 已成 marker 交还完成态。

## 当前 P0 / P1 违约事实

### 已缓解：Core pipeline 不再直接包含 Demo phase

旧复审中的 `s_corePhases` 包含 `AutoChessDemoConfigPhase` 已不是当前事实。当前 Core phase set 只包含 Core phases，Demo phase 改为 standalone phase 和 standalone output root。

剩余约束是消费链路要分 owner：`TryRunAll()` 仍然先跑 Core 再跑 AutoChessDemo standalone，所以 Core validation report 只消费为 Runtime Core 生成链证据；AutoChessDemo generated manifest / scenario artifact 只能消费为业务验收或 Demo 证据。

### 已缓解但未完成：RuntimeDefinitionGluePhase 不再生成 lifecycle artifact

`RuntimeDefinitionGluePhase` 当前只生成：

```text
Runtime/RuntimeDefinitionGlue.gen.cs
```

迁移期 lifecycle artifact 在 manifest 中当前已归零；这意味着 `RuntimeLifecycleMigrationPhase` 不再向 manifest 交还 active gameplay lifecycle artifact。

`RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs` 与 `RuntimeActiveEffect.gen.cs` 当前都是 12 行级 marker，并全部以 `RuntimePureGlue` 分类。ability activation 的实际 runtime owner 已转向手写 `GASRuntimeDefinitionResolver`，instant effect spec build / attribute reduce 的实际 runtime owner 已转向手写 `GEEffectInstantSystems.cs`，active-effect mutation / pre-tick / remove / magnitude helper 已转向手写 `GASActiveEffectRuntime.cs` 与 `GEActiveEffectLifecycleSystems.cs`。`ActiveEffectLifecycleOwnerSystems.cs` 当前已不在 generated runtime 物理目录；`RuntimeSystemRegistration.gen.cs` 当前已不在 output list，且 report 中 `GeneratedRuntimeSystemRegistrationHits = 0`。手写 registry 当前已把缺失 generated type 从 silent skip 改为 fail-fast，但 `GASSystemScheduleContract` 的 generated type-name 列表当前已为空，后续重点应是 system 数量、phase budget、type mismatch / assembly unavailable 负例、marker / hand-written owner 对账和 stale generated lifecycle 防回流。

判定依据：

1. `SYS-01`：权威 gameplay 计算必须落在 ECS System / Job 数据流，但生命周期 owner 应由 Runtime Core 架构显式拥有。
2. `SYS-03`：system 数量是成本源；当前 registration hit 为 0，但新增 generated registration 一旦回流，仍不能绕过 system budget。
3. `QRY-01` / `QRY-04`：hot path query 与高频 random lookup 必须有 owner-local / chunk-local 选型论证。
4. `SC-01` / `ECB-03`：结构变化必须归属明确 phase，不能由 generated glue 隐藏 owner。
5. `BUR-01`：hot path system/job 必须 Burst 且无托管依赖，generated output 与模板同等受审。

### P1：Catalog builder runtime-visible

`GASGeneratedDefinitionCatalogBuilder.BuildCatalog()` 当前在 Runtime-visible generated 文件中调用 `BlobBuilder`。如果只在 bootstrap / initialization 调用，它符合 `BLOB-02` 下限；但当前层级边界仍偏软，因为 runtime-visible API 暴露了 catalog materialization 和 dispose owner 责任。

判定依据：`BLOB-01` / `BLOB-02` / `BAKE-01` 要求静态 definition 进入 immutable Blob，`BlobBuilder` 只在 Baking 或初始化期使用。Runtime Core hot path 只能只读 catalog。

### P0：Validation report 已进入 blocking-unclassified gate，当前 generated lifecycle hit 已归零

`RuntimeForbiddenDependencyHits = 0`、`RuntimeGeneratedNamingDebtHits = 0` 只能证明没有 managed config 泄漏和命名债。当前 report 已经能输出 generated runtime boundary 分类，并且 `GeneratedRuntimeBoundaryGateMode` / `CurrentMode` 已是 `blocking-unclassified-lifecycle-migration`。这说明未分类 boundary hit 会被阻断；当前 `GeneratedRuntimeBoundaryHits = 0`，且 lifecycle / structural / ownership / random lookup / registration / managed config 分项均为 0，但它仍不是 SourceGenerator 完成证明。

当前必须消费为剩余风险的字段是：

```text
GeneratedRuntimeLifecycleHits
GeneratedRuntimeSystemRegistrationHits
GeneratedRuntimeOwnershipHits
GeneratedRuntimeStructuralChangeHits
GeneratedRuntimeRandomWriteLookupHits
GeneratedRuntimeManagedConfigHits
```

## 当前事实对照快照

| 事实项 | 当前证据 | 官方规则 | 判定 |
|---|---|---|---|
| Core / Demo phase 已拆分 | `GasCodeGenPipeline.cs` 中 `s_corePhases` 不含 `AutoChessDemoConfigPhase`；Demo standalone 写入 `Assets/AutoChessDemo/Generated` | `ODF-06` | 旧 P0 已缓解；消费链仍需分 owner |
| RuntimeDefinitionGluePhase 只输出 pure glue | `RuntimeDefinitionGluePhase` 输出 `RuntimeDefinitionGlue.gen.cs`，manifest 分类为 `RuntimePureGlue` | `BUR-01`、`ODF-18` | pure glue phase ownership 已收窄 |
| RuntimeLifecycleMigrationPhase / manifest 已收口 | `RuntimeLifecycleMigrationPhase` 当前没有 manifest lifecycle artifact；`RuntimeActiveEffect.gen.cs`、`RuntimeAbilityActivation.gen.cs` 与 `RuntimeEffectInstant.gen.cs` 均为 `RuntimePureGlue` marker；active-effect helper/job 已迁到手写 `GASActiveEffectRuntime.cs` / `GEActiveEffectLifecycleSystems.cs`；`ActiveEffectLifecycleOwnerSystems.cs` 当前磁盘缺失 | `SYS-01`、`SYS-03` | 迁移期 proof 已大幅缩小；active-effect lifecycle owner 不再由 SourceGenerator 生成主链代码，后续转为 marker / hand-written owner 对账、system budget、负例验证和 generated lifecycle 防回流 |
| generated runtime registration 当前为 0 | validation report 输出 `GeneratedRuntimeSystemRegistrationHits: 0`；手写 registry 对缺失 generated type 当前 fail-fast 抛错 | `SYS-03` | SourceGenerator 不自注册是正向事实；缺失 type 静默漏注册风险已缓解，但主链注册仍需 system 数量、phase budget、type mismatch / assembly unavailable 负例验证；新增 registration 默认失败 |
| generated runtime boundary gate 已阻断未分类命中 | validation report 输出 `GeneratedRuntimeBoundaryHits: 0`、`GeneratedRuntimePureGlueArtifacts: 4`、`GeneratedRuntimeLifecycleMigrationArtifacts: 0`、`GeneratedRuntimeLifecycleHits: 0`、`GeneratedRuntimeStructuralChangeHits: 0`、`GeneratedRuntimeOwnershipHits: 0`、`GeneratedRuntimeRandomWriteLookupHits: 0`、`GeneratedRuntimeManagedConfigHits: 0`、`GeneratedRuntimeBoundaryGateMode: blocking-unclassified-lifecycle-migration`、`GeneratedRuntimeUnclassifiedBoundaryHits: 0` | `ODF-18`、`SYS-01`、`QRY-04` | gate 数字不能单独视为当前事实完成证明；R5 仍要补 release-ready gate、system budget、负例验证、catalog lifetime / dispose owner 证据和 manifest/report/file/schedule 对账 |
| generated runtime random lookup | 当前 active-effect runtime lookup owner 已迁到手写 `GASActiveEffectRuntime.cs` / Runtime systems；generated runtime 目录当前不再保留 active-effect companion lookup / ECB / NativeContainer 文件；validation report random lookup hit 为 0 | `QRY-04` | 主链迁移方向正向，但仍需按手写 Runtime Core lookup owner、scale evidence 和 generated lifecycle 防回流分别验收 |
| catalog blob 已出现 | `DefinitionCatalog.gen.cs` 有 sorted lookup / `ref readonly` access | `BLOB-01` | 正向事实 |
| `BlobBuilder` runtime-visible | `DefinitionCatalog.gen.cs` 暴露 `BuildCatalog()` | `BLOB-02`、`BAKE-01` | 初始化可接受，层级 owner 需收口 |
| forbidden dependency gate 通过 | report 输出 forbidden dependency hit 为 0 | `ODF-18` | 只能证明局部边界 |

## 后续事实约束

以下不是本文件的任务计划，而是后续实现必须回填证据的事实约束：

1. `GasCodeGenPipeline.RunAll()` 或默认 Core 入口必须持续证明 Core phase set 不包含 `AutoChessDemoConfigPhase` 或任何 Demo 专用 phase；Demo standalone evidence 不能混写为 Core validation evidence。
2. `RuntimeDefinitionGluePhase` 必须持续只输出 pure glue / unmanaged record；`RuntimeLifecycleMigrationPhase` 中的 generated lifecycle artifact 必须迁出 Runtime-visible 层、由手写 Runtime Core owner 接管，或在 release-ready mode 中变为 blocking。
3. `BuildCatalog()` / `BlobBuilder` 必须有 Baking / Bootstrap / initialization owner 和 dispose owner 证据。
4. Validation report 已新增 generated lifecycle / ownership / random lookup / NativeContainer / structural change gate，并已进入 `blocking-unclassified-lifecycle-migration`；当前 generated boundary / lifecycle / ownership / structural / random lookup / registration / managed config / unclassified 分项均为 0。后续必须补 release-ready gate、catalog lifetime / dispose owner、负例验证、manifest/report/file/schedule 对账和防回流扫描，不能只停留在“本轮数字为 0”。
5. Runtime 消费链必须证明至少一条 Ability / GE 链路只读 `GASDefinitionCatalogBlob` / lookup / pure glue，不反查 per-definition entity、managed row、JSON 或 `Dictionary`。

## 当前红线

1. 不再把“generated code 已存在”写成架构完成；generated code 进入主链后必须按 Runtime Core 规则审查。
2. 不再把 `RuntimeForbiddenDependencyHits = 0` 写成 SourceGenerator 完全合规。
3. 不再把 generated lifecycle system 当作目标态 Runtime Core。
4. 不再把 AutoChessDemo phase、scenario、validation artifact 混入 Core CodeGen 默认路径。
5. 不手改 `.gen.cs` 修复架构问题；修复必须落回 codegen phase、manifest、validation report、离线 sourcegen bat/CLI 或 Unity batchmode 生成链路。
