# SourceGenerator 链路复审事实

> 归属：本文件只记录 Luban / SourceGenerator 当前链路事实、越权证据、静态门禁缺口，以及这些事实为什么按 Unity DOTS 官方规则构成架构风险。目标态设计见 `../01-目标态架构共识/14-DefinitionCodeGen目标链路Spec.md` 和 `../01-目标态架构共识/15-SourceGenerator职责边界Spec.md`；任务拆分不写在本文件内。

## 2026-06-07 目录归位说明

`01-目标态架构共识/` 只保留目标态 Spec，不再承载当前复审流水、当前文件清单、P0/P1 命中或下一轮计划。

因此，原先混入目标态目录的 CodeGen / Luban-SourceGenerator 复审信息按以下方式归位：

| 信息类型 | Owner | 说明 |
|---|---|---|
| 当前生成链路事实、generated artifact 清单、当前 P0/P1 违约 | 本文件与 `CodeGen链路复审事实.md` | 作为事实和诊断证据保留 |
| 目标态 Definition CodeGen 数据流、允许/禁止 artifact、验收门槛 | `../01-目标态架构共识/14-DefinitionCodeGen目标链路Spec.md` | 作为框架设计 Spec |
| SourceGenerator 权限边界、官方规则论证、generated glue 调用形态 | `../01-目标态架构共识/15-SourceGenerator职责边界Spec.md` | 作为框架设计 Spec |
| 可执行整改切片、下一轮目标、CI gate 落地任务 | `../02-主线任务树/` 或当前进度目录 | 不在事实文档中展开 |

本文件后续只允许补充“现实代码证据”和“事实判定”。如果需要写目标态分层、目标数据流、示例代码或最终不变量，应更新 `01` 下对应 Spec。

## 结论

当前链路已经走到了正确方向，且旧复审中的 Core / Demo phase 混入问题已经被第一刀缓解；`RuntimeDefinitionGluePhase` 也已从 lifecycle 输出中收窄为 pure glue 输出。当前更进一步的是：manifest 中 `RuntimeLifecycleMigration` artifact 已归零，ability / instant / active effect 生成物都退为 marker / pure glue，`ActiveEffectLifecycleOwnerSystems.cs` 当前也已从 generated runtime 物理目录退出。SourceGenerator 的权限边界仍未完成，但当前风险已经从“generated lifecycle artifact 仍生成 / stale lifecycle 文件仍编译”转为“marker / pure glue / hand-written owner 职责对账、system budget、release-ready gate、负例验证和 generated lifecycle 防回流仍未闭合”。

正确方向包括：

1. Luban 输出保留在 Unity 编译域内。
2. GAS generated Runtime 不直接引用 `cfg.*` / `SimpleJSON` / JSON reader。
3. `GASDefinitionCatalogBlob`、sorted code lookup、`ref readonly` definition 访问已经出现。
4. `RuntimeForbiddenDependencyHits = 0` 能证明 managed config 没有直接泄漏到 generated Runtime。
5. `GasCodeGenPipeline.s_corePhases` 已不再包含 `AutoChessDemoConfigPhase`，Demo 产物改由 standalone phase 写入 `Assets/AutoChessDemo/Generated`。
6. validation report 已新增 generated lifecycle / structural / ownership / random lookup / managed config boundary 计数，并输出 `GeneratedRuntimeBoundaryGateMode: blocking-unclassified-lifecycle-migration`。
7. `GeneratedRuntimeSystemRegistrationHits = 0` 只说明 SourceGenerator 当前没有输出自注册 helper；当前 `GASSystemScheduleContract` 的 generated type-name 列表已经为空，主链 active-effect / instant / attribute systems 由手写 Runtime 类型直接注册。`AddSystemsByTypeName()` 的 fail-fast 仍是防回流机制，但不再能被写成当前 generated system 调度来源。后续仍需补缺失 artifact / type mismatch / assembly unavailable 的负例验证、system 数量和 phase budget 证据。
8. `GasCodeGen.manifest.json` 当前把 `RuntimeDefinitionGlue.gen.cs`、`RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs` 与 `RuntimeActiveEffect.gen.cs` 都标为 `ArtifactCategory=RuntimePureGlue`；`RuntimeLifecycleMigration` artifact 当前为 0。当前磁盘上的三个 runtime lifecycle 入口文件实际都是 12 行 `HandwrittenRuntimeOwner` marker，active-effect helper/job/snapshot/mutation/tick/remove 已迁到手写 `GASActiveEffectRuntime.cs` / `GEActiveEffectLifecycleSystems.cs`。这使 SourceGenerator 输出职责明显收窄，但 marker、generated pure glue 和 hand-written owner 之间仍需要 R5 对账，而不是完成证明。
9. `ActiveEffectLifecycleOwnerSystems.cs` 当前已退出 SourceGenerator 输出、manifest、validation report、schedule type-name 列表和磁盘文件列表；它现在是防回流扫描项，不是当前 Runtime 主链，也不是 generated runtime 物理 asmdef 残留。
10. TagRequirement all-any-none query 已形成 generated catalog / pure evaluator 正向事实；instant target tag live lookup 已随 instant owner 转入手写 `GEEffectInstantSystems.cs`，active effect requirement / magnitude 消费当前也转向手写 `GASActiveEffectRuntime.cs` 调用 `GASRuntimeRequirementEvaluator` / `GASRuntimeMagnitudeEvaluator`。该专题事实的唯一正文见 [架构重划分审查事实/08-TagRequirementQueryDefinitionGlue事实.md](架构重划分审查事实/08-TagRequirementQueryDefinitionGlue事实.md)。本文件只保留 SourceGenerator 权限边界摘要。

必须修正的是：生成器不应继续生成 Runtime Core lifecycle system、隐藏结构变化 owner 或大量 runtime random lookup 驱动逻辑；新增 boundary hit 不能长期只报告不阻断。SourceGenerator 当前已经不只是“配置到代码”的胶水，而是在部分路径上替 Runtime Core 拥有 gameplay 时序。

## 2026-06-08 artifact responsibility 续审事实

本轮继续用 codedb 复核 SourceGenerator 输出、manifest 分类、validation classifier 与 runtime consumer。新增事实只记录当前实现，不写目标态设计。

| 复核项 | 当前事实 | 判定 |
|---|---|---|
| `RuntimePureGlue` 当前含义 | manifest 把 `RuntimeDefinitionGlue.gen.cs`、`RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs` 与 `RuntimeActiveEffect.gen.cs` 标为 `RuntimePureGlue`；前者包含 definition resolver、requirement evaluator、magnitude evaluator 和 target rule table，后三者当前是 handwritten runtime owner marker / pure glue artifact，均没有 `ISystem` / `OnUpdate` / ECB / `EntityManager` 写入 | 这是 SourceGenerator 可以保留和继续加深的正向资产，但 marker 不等于业务 glue 已完整生成 |
| pure glue 当前消费面 | `GASGeneratedRuntimeDefinitionResolver` 当前没有 runtime caller；`GASGeneratedRequirementEvaluator` 当前 0 caller；`GASGeneratedMagnitudeEvaluator` 只被 generated resolver 内部调用。ability activation 实际调用手写 `GASRuntimeDefinitionResolver` / `GASRuntimeRequirementEvaluator`；instant effect 与 active-effect runtime 实际调用手写 `GASRuntimeRequirementEvaluator` / `GASRuntimeMagnitudeEvaluator` | ability activation、instant effect 和 active effect 都已出现手写 Runtime Core owner 接管的正向事实；但 generated pure glue 与手写 resolver 的职责重复、0 caller glue 是否保留、marker 是否仍应入 manifest，仍需 R5 对账，不能写成 SourceGenerator 收权完成 |
| `RuntimeLifecycleMigration` 当前含义 | manifest 当前没有 `RuntimeLifecycleMigration` artifact；`RuntimeActiveEffect.gen.cs` 已与 ability / instant marker 一样归类为 `RuntimePureGlue`，且 `ActiveEffectLifecycleOwnerSystems.cs` 当前磁盘缺失 | 迁移期 generated lifecycle artifact 已退出 manifest 与物理文件面；release-ready 前仍要确认 future generated lifecycle 回流会被 blocking，并持续扫描 stale companion 文件名 / 改名回流 |
| 不允许迁移豁免的 hit | classifier 对 `system-registration` 与 `managed-config` 返回 blocking，不允许走 `MigrationProofOnly` | SourceGenerator 自注册和 managed config 泄漏已被设为硬边界；后续新增 helper / `cfg.*` / JSON / row runtime 引用应直接失败 |
| Bootstrap catalog owner | 当前 report 已没有 `BootstrapDefinitionOwner` boundary hit；`DefinitionCatalog.gen.cs` 的 catalog builder 仍被 AutoChess 初始化 install/dispose 与 Editor Baker `AddBlobAsset` 消费 | `BuildCatalog()` 当前可作为 Baking / Bootstrap / initialization materialization 事实；虽然不再是 generated boundary hit，runtime-visible builder 仍需 lifetime / dispose owner 证据，不能写成 hot path lookup |
| report / manifest 对账 | validation report 的 Manifest Entries 表当前已逐行显示 `ArtifactCategory`；manifest JSON 仍有完整 category 字段 | SourceGenerator 交还必须同时附 report 和 manifest category 对账；否则仍可能把 marker、pure glue、lifecycle migration artifact 或 bootstrap materialization owner 混读 |
| 大模板 owner 风险 | `GasGlueCodeGenPhases.cs` 同时拥有 pure glue 生成、lifecycle migration 生成、Baker glue、query layout、validation collector 和 gate policy | 当前风险不是“生成器缺功能”，而是生成器 Module 太宽，容易继续把 Runtime Core lifecycle、validation rule 和 demo/config glue 混在同一实现中推进 |

本轮 SourceGenerator 判断：当前实现已经具备“正确的生成资产”和“正确的阻断方向”，但尚未完成目标态收权。ability activation、instant effect 和 active effect 均已出现 hand-written Runtime Core owner 接管；完成态还必须看到 generated / hand-written resolver 职责对账、`RuntimeActiveEffect.gen.cs` marker 与 `RuntimePureGlue` manifest category 的持续防回流、catalog lifetime / dispose owner 证据、catalog materialization owner 独立、manifest/report/category/file 四方可直接对账，以及 generated lifecycle 回流负例验证。

## 官方依据与判定标准

本复审不是“少生成代码更干净”的审美判断，而是由 Unity Entities 的运行时机制反推出来的边界判断。后续修改本链路时，必须先回答三件事：

1. 该 artifact 属于配置事实、Baking / Bootstrap，还是每帧 Runtime lifecycle。
2. 它是否拥有 query、dependency、NativeContainer、ECB 或结构变化。
3. 它是否会进入 Runtime Core hot path，是否能被 Debugger / Profiler / Journaling 归因。

| 当前事实判定 | 官方 / 本地规则依据 | 为什么是风险 | 为什么必须处理 |
|---|---|---|---|
| generated Runtime lifecycle 不应作为目标态 | `SYS-01`、`SYS-03`、`PRF-07`；`System-World-SystemGroup/API与EX-GAS解读.md` 记录每个 system 都有 TypeHandle、Lookup、Dependency 成本 | lifecycle owner、query owner、dependency owner 被模板隐藏，Debugger 只能看到 generated system，难以归因到业务 lane | GAS 的热路径要按 phase / lane 优化，generated system 数量和 update order 不能绕过架构预算 |
| generated glue 不应拥有 query / lookup refresh | `QRY-01`、`QRY-04`、`PRF-05`、`PRF-06`、`PRF-19` | `ComponentLookup` / `BufferLookup` random access 会被模板固化，x1000 规模时只能改大模板 | 高频 lookup 应由手写 Runtime System 按 owner-local / chunk-local / target-grouped merge 重新选型 |
| generated glue 不应拥有 ECB / 结构变化 | `SC-01`、`ECB-03`、`PRF-02`、`PRF-04` | ECB owner 和 playback phase 隐藏后，StructuralCommit gate 失去唯一事实源 | 结构变化需要 Journaling / Profiler 能证明来源和相位，否则无法控制 sync point |
| Catalog 构建应属于 Baking / Bootstrap / initialization owner | `BLOB-01`、`BLOB-02`、`BAKE-01`~`BAKE-03`、`CASE-07`、`CASE-24` | `BlobBuilder`、Dispose owner、row adapter 如果留在 runtime-visible 代码中，会把配置构建能力暴露给 gameplay 层 | Runtime Core hot path 应只读 `BlobAssetReference<GASDefinitionCatalogBlob>` 和 generated lookup |
| validation gate 不能只查 forbidden dependency | `ODF-06`、`ODF-18`、`20-GASRuntimeCore-API选型基线.md` | `RuntimeForbiddenDependencyHits = 0` 仍可能同时存在 generated `ISystem`、`EntityManager`、system registration | Gate 必须证明职责边界，不只是证明没有 `cfg.*` 字符串 |

## 审查范围

本复审只覆盖 Runtime 相关配置链路：

```text
Excel / Luban
  -> Luban JSON / C#
  -> LubanNormalizedRows / RowMetadata
  -> GasCodeGenPipeline phases
  -> generated Runtime / Baking / Editor artifacts
  -> Runtime Core consumption
```

不讨论 Editor UI 操作体验，不讨论 GAS Center 页面逻辑。`Assets/GAS/Editor/CodeGen/**` 属于生成链路实现，纳入审查；`Assets/GAS/Generated/CodeGen/Runtime/**` 已被注册进主链时，按 Runtime Core 规则受审。

## 当前链路事实

### 1. Luban process gate 已接入主入口

`CodeGenerator.TryGenerateAllCode()` 当前先执行 `GasCodeGenProcessGate.RunDefault()`，再执行 `GasCodeGenPipeline.TryRunAll()`。

证据：

- `Assets/GAS/Editor/CodeGen/CodeGenerator.cs`
- `Assets/GAS/Editor/CodeGen/Core/GasCodeGenProcessGate.cs`

这条链路的价值是：Bean / Luban 任一失败会阻断 Core artifact 导出，避免旧生成物继续污染 Runtime。

### 2. Luban 输出边界方向正确

当前默认输出：

```text
Assets/DataGenerated/Luban/CSharp
Assets/DataGenerated/Luban/Json/GAS
```

这符合 `../01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md` 的约束：Luban C# 是 Unity 编译域内的配置事实边界，编译错误是真实 gate failure，不应通过挪出 `Assets` 隐藏。

### 3. Normalized Rows 是 Editor-only 输入转换层

`LubanNormalizedRowBootstrap` 从 Luban JSON 读取 `ability`、`gameplayEffect`、`attributeSet`、`gameplayTags` 等表，生成 `Assets/GAS/Generated/CodeGen/Editor/LubanNormalizedRows.gen.cs`。

这比 Runtime 直接读取 `cfg.*` 或 JSON 更合理。它把 Luban 表事实折叠成后续 `RowMetadata` 可扫描的 row literal factory，同时保持 Editor-only。

### 4. Catalog / Blob 方向已经出现

`Assets/GAS/Generated/CodeGen/Runtime/DefinitionCatalog.gen.cs` 已经生成：

- `GASDefinitionCatalogBlob`
- sorted `AbilityCodes` / `GameplayEffectCodes`
- `TryGetAbilityIndex()`
- `TryGetGameplayEffectIndex()`
- `GetAbility()` / `GetGameplayEffect()` 的 `ref readonly` 访问

这是目标方向上的关键收益：Runtime Core 不再把 Luban row 当表 API，而是读取 immutable catalog。

### 5. Validation report 已扩展职责边界扫描，并阻断未分类命中

`GasCodeGenValidationReport.md` 当前显示：

```text
RuntimeForbiddenDependencyHits: 0
RuntimeGeneratedNamingDebtHits: 0
GeneratedNamingDebtHits: 0
GeneratedRuntimeBoundaryHits: 0
GeneratedRuntimePureGlueArtifacts: 4
GeneratedRuntimeLifecycleMigrationArtifacts: 0
GeneratedRuntimeLifecycleHits: 0
GeneratedRuntimeSystemRegistrationHits: 0
GeneratedRuntimeStructuralChangeHits: 0
GeneratedRuntimeOwnershipHits: 0
GeneratedRuntimeRandomWriteLookupHits: 0
GeneratedRuntimeManagedConfigHits: 0
GeneratedRuntimeBoundaryGateMode: blocking-unclassified-lifecycle-migration
GeneratedRuntimeUnclassifiedBoundaryHits: 0
```

这说明扫描能力已经从 forbidden dependency 扩展到 generated runtime 职责边界，且未分类 boundary hit 已会阻断生成。当前 generated runtime lifecycle、structural owner、ownership owner 和 random lookup owner 已退出 report 分类面。R5 的任务不再是“迁出 classified lifecycle artifact”，而是确认 release-ready gate、补 catalog lifetime / dispose owner、system budget、manifest/report/file/schedule 对账和 generated lifecycle 防回流门。

### 6. Core phase 与 Demo phase 已拆分，但默认 all 入口仍是复合消费链

`GasCodeGenPipeline.s_corePhases` 当前只包含 Core phases：

```csharp
new AssemblyDefinitionPhase(),
new DefinitionIndexPhase(),
new BlobSchemaPhase(),
new StaticLookupPhase(),
new DefinitionCatalogPhase(),
new RuntimeDefinitionGluePhase(),
new RuntimeLifecycleMigrationPhase(),
new BakerGluePhase(),
new ComponentTypeSetPhase(),
new QueryLayoutPhase(),
new ValidationReportPhase(),
```

`AutoChessDemoConfigPhase` 已移动到 `s_autoChessDemoPhases`，并通过 `TryRunAutoChessDemo()` standalone 写入 `Assets/AutoChessDemo/Generated`。这是正向修复，后续任务不得继续把“Core phase 仍包含 Demo phase”当作当前 P0 执行。

需要保留的事实边界是：`TryRunAll()` 仍然先 `TryRunCore()` 再 `TryRunAutoChessDemo()`，所以“生成 -> 消费 -> 归档”链路必须区分 Core validation report 与 Demo standalone manifest / scenario 产物，不能把 Demo 证据混写成 Runtime Core 完成证明。

## 主要架构问题

### 已缓解：Core pipeline 不再直接混入 Demo phase

旧复审中的 `s_corePhases` 包含 `AutoChessDemoConfigPhase` 已不是当前事实。当前 Core phase set 已拆分，Demo 生成改为 standalone phase 和 standalone output root。

剩余约束不是继续拆 `s_corePhases`，而是保持 Core report / manifest 与 Demo generated manifest 的 owner 分离：Core validation report 只能证明 Runtime generated artifact；AutoChessDemo standalone 产物只能作为业务验收 / scenario evidence，不能消费为通用 Core 合规证明。

### 已缓解但未完成：RuntimeDefinitionGluePhase 不再生成 lifecycle system

`RuntimeDefinitionGluePhase` 当前输出：

```text
Runtime/RuntimeDefinitionGlue.gen.cs
```

迁移期 lifecycle artifact 在 manifest 中当前已经归零；三个旧 lifecycle 入口文件都已改为 `RuntimePureGlue` marker：

```text
Runtime/RuntimeAbilityActivation.gen.cs
Runtime/RuntimeEffectInstant.gen.cs
Runtime/RuntimeActiveEffect.gen.cs
```

`RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs` 与 `RuntimeActiveEffect.gen.cs` 当前都已退为 marker，并全部归类为 `RuntimePureGlue`。ability activation 的 plan / requirement / seed 构建已由手写 `GASRuntimeDefinitionResolver` 和 `GASRuntimeRequirementEvaluator` 承接，instant effect 的 spec build / attribute reduce 已由手写 `GEEffectInstantSystems.cs` 承接，active-effect helper/job/snapshot/mutation/tick/remove 已由手写 `GASActiveEffectRuntime.cs` 和 `GEActiveEffectLifecycleSystems.cs` 承接。当前 `ActiveEffectLifecycleOwnerSystems.cs` 已从 `RuntimeLifecycleMigrationPhase` 输出、manifest 分类、validation report 和 generated runtime 磁盘目录中退出。`RuntimeSystemRegistration.gen.cs` 当前已不在 output list，且 report 中 `GeneratedRuntimeSystemRegistrationHits = 0`；但这只证明生成器没有自注册 helper，不能证明 schedule registry、manifest category、validation report 和 stale generated lifecycle 防回流整体达标。当前 `GASSystemScheduleContract` 的 generated type-name 列表为空；后续仍要补缺失 artifact / type mismatch / assembly unavailable 负例、system 数量和 phase budget。

官方判定：

1. `SYS-01` 要求权威 gameplay 计算落在 ECS System / Job 数据流，但 owner 必须可审计；generated glue 输出 `ISystem` 后，glue 就变成 lifecycle owner。
2. `SYS-03` 认为 system 数量是成本源；当前 registration hit 为 0，但新增 generated registration 一旦回流，系统数量和 update order 仍会绕过 Runtime Core 架构预算。
3. `QRY-01` / `QRY-04` 要求 hot path query 和 random lookup 有 job / owner-local / chunk-local 论证；模板生成 lookup 会绕过每条业务 lane 的 API 选型表。
4. `SC-01` / `ECB-03` 要求结构变化集中在明确 playback phase；generated lifecycle system 如果创建 ECB 或直接写 `EntityManager`，会制造隐藏结构变化 owner。

### P0：Validation gate 已让 generated lifecycle hit 归零，但 release-ready 证据仍不足

当前 validation report 已把以下内容作为 generated runtime boundary hits 记录：

```text
: ISystem
OnUpdate(ref SystemState state)
SystemAPI.GetComponentLookup(...)
SystemAPI.GetBufferLookup(...)
state.EntityManager
EntityCommandBuffer
NativeList<T>
```

`Generated Runtime Boundary Gate` 的 `CurrentMode` 当前是 `blocking-unclassified-lifecycle-migration`，未分类命中会被阻断；当前 `GeneratedRuntimeBoundaryHits = 0`，runtime-visible generated lifecycle / lookup / structural / ownership owner hit 已归零。因此报告可以写 `RuntimeForbiddenDependencyHits = 0`、`GeneratedRuntimeUnclassifiedBoundaryHits = 0`，但不能写成 SourceGenerator 完成。当前剩余风险是 system budget 未闭合、catalog lifetime / dispose owner、manifest/report/file/schedule 对账和 release-ready 防回流门不足。

### P1：Catalog builder 的层级边界偏软

`GASGeneratedDefinitionCatalogBuilder.BuildCatalog()` 当前在 Runtime-visible generated 文件中调用 `BlobBuilder`。

如果它只在 world/bootstrap 初始化期调用，符合 `BLOB-02` 的“初始化期可用”下限；但当前 runtime-visible 暴露方式仍然偏软。事实风险是：配置 materialization、Blob dispose owner、catalog install owner 与 gameplay runtime 可见 API 混在一起，后续 hot path 审查必须持续证明没有每帧构建和没有 owner 泄漏。

### P1：generated runtime hot path 仍有大量 random lookup

手写 `GEEffectInstantSystems.cs`、`GASActiveEffectRuntime.cs` / `GEActiveEffectLifecycleSystems.cs` 中仍存在：

```text
ComponentLookup<T>
BufferLookup<T>
GetComponentLookup<T>()
GetBufferLookup<T>()
```

这些 API 可以作为迁移期 proof 使用，但不能被写成 scale-ready store 终局。对照 `QRY-04`，高频路径中的跨 entity random lookup 需要重构为 owner-local、chunk-local 或 frame-local record merge，并给出 capacity / ordering / x50 / x1000 profile 证据。

### P1：SourceGenerator 生成内容太厚

当前 SourceGenerator / generated runtime 链路既生成 Blob / lookup / pure glue，也仍保留 active marker；active effect mutation、pre tick、remove 已转到手写 Runtime System，且 `ActiveEffectLifecycleOwnerSystems.cs` 当前磁盘缺失，但仍需要按 Runtime Core lookup owner 和防回流规则审查。这会造成三个长期问题：

1. Runtime Core 的职责 owner 变成模板文件，而不是清晰的手写 System。
2. 性能热点难以定位：Debugger 看见的是 generated system，不知道业务 owner 是谁。
3. 每次调整 DOTS API 选型，都要改大模板，变成高风险全链重生成。

这不是“模板大不好维护”的普通工程偏好，而是和 Unity DOTS 的调优方式冲突：DOTS 性能问题通常落在 query filter、chunk layout、lookup 刷新、NativeContainer 生命周期、ECB playback、enableable wait、buffer spill 上。这些都需要由具体 system owner 输出证据。

## 目标态内容归位

本文件旧版本曾包含 `新架构分层`、`目标数据流`、`真实业务流`、`目标代码形态`、`重构路线`、`最终不变量` 等章节。这些内容不属于当前事实文档，已按下表归位：

| 旧章节类型 | 新 Owner | 保留方式 |
|---|---|---|
| Luban Fact / Definition CodeGen / Baking / Runtime Core 四层目标分工 | `../01-目标态架构共识/14-DefinitionCodeGen目标链路Spec.md`、`../01-目标态架构共识/15-SourceGenerator职责边界Spec.md` | 以目标态 Spec 表述 |
| 目标数据流图、Generated Glue 调用形态、禁止生成项 | `../01-目标态架构共识/15-SourceGenerator职责边界Spec.md` | 以权限边界和验收门槛表述 |
| AutoChess Ability 激活的理想业务链路 | `../01-目标态架构共识/10B-AutoChess完整业务案例设计Spec.md`、`../01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md` | 以业务案例 Spec 表述 |
| P0/P1/P2 重构路线 | `../02-主线任务树/` 或 `../04-当前进度状态/` | 以任务和进度表述 |
| 最终不变量 | `../01-目标态架构共识/90-目标态不变量.md` | 以全局不变量表述 |

本文件只保留这些目标态设计对应的当前事实：哪些代码正在违反边界、为什么违反、证据在哪里。

## 当前事实对照快照

| 事实项 | 当前证据 | 官方规则 | 当前判定 |
|---|---|---|---|
| Core / Demo phase 已拆分 | `GasCodeGenPipeline.s_corePhases` 不含 `AutoChessDemoConfigPhase`，Demo 写入 standalone output root | `ODF-06`、`20-GASRuntimeCore-API选型基线.md` | 旧 P0 已缓解；继续保持 Core report 与 Demo evidence 分 owner |
| RuntimeDefinitionGluePhase 已收窄 | `RuntimeDefinitionGluePhase` 当前只输出 `RuntimeDefinitionGlue.gen.cs`，manifest 分类为 `RuntimePureGlue`；runtime lifecycle marker 已归入 pure glue，`RuntimeLifecycleMigration` artifact 为 0 | `SYS-01`、`SYS-03`、`QRY-01` | phase ownership 已缓解；后续重点转为 pure glue consumer 对账、release-ready 防回流和 stale generated lifecycle 扫描 |
| generated runtime system registration 当前为 0 | report 输出 `GeneratedRuntimeSystemRegistrationHits: 0`，output list 不含 `RuntimeSystemRegistration.gen.cs`；手写 `GASSystemScheduleContract.AddSystemsByTypeName()` 对缺失 generated type 当前 fail-fast | `SYS-01`、`SYS-03` | SourceGenerator 不自注册是正向事实；缺失 type 静默漏注册风险已缓解，但手写 registry 仍需负例验证、system 数量和 phase budget；新增 registration helper 必须默认失败 |
| generated runtime boundary gate 阻断未分类命中 | report 输出 `GeneratedRuntimeBoundaryHits: 0`、`GeneratedRuntimePureGlueArtifacts: 4`、`GeneratedRuntimeLifecycleMigrationArtifacts: 0`、`GeneratedRuntimeLifecycleHits: 0`、`GeneratedRuntimeStructuralChangeHits: 0`、`GeneratedRuntimeOwnershipHits: 0`、`GeneratedRuntimeRandomWriteLookupHits: 0`、`GeneratedRuntimeBoundaryGateMode: blocking-unclassified-lifecycle-migration`、`GeneratedRuntimeUnclassifiedBoundaryHits: 0` | `ODF-18`、`SYS-01`、`QRY-04`、`SC-01` | 未分类回流已被阻断，generated lifecycle / structural / ownership / random lookup hit 已退出 report 分类面；但 release-ready 仍需负例验证、system budget、file/schedule 对账和防回流 |
| generated runtime random lookup 已退出 report 面 | active-effect runtime random lookup owner 已迁到手写 `GASActiveEffectRuntime.cs` / Runtime systems，generated report 不再记录 generated random lookup hit，generated runtime 目录也不再保留 active-effect companion 文件 | `QRY-04`、`PRF-06`、`PRF-19` | 主链迁移方向正向，但仍需按手写 Runtime Core lookup owner、scale evidence 和 stale generated lifecycle 防回流分别验收 |
| Catalog builder runtime-visible | `DefinitionCatalog.gen.cs` 暴露 `BuildCatalog()` / `BlobBuilder` | `BLOB-01`、`BLOB-02`、`BAKE-01` | 初始化可接受，但层级边界和 dispose owner 需继续证明 |
| Forbidden dependency gate 通过 | `RuntimeForbiddenDependencyHits: 0` | `ODF-18` | 只证明 managed config 未泄漏，不能证明 lifecycle / query / ECB 合规 |

## 后续事实约束

以下不是本文件的任务计划，而是后续实现必须回填证据的事实约束：

1. Core generation 默认入口必须持续证明 Core phase set 不包含 `AutoChessDemoConfigPhase` 或任何 Demo 专用 phase；Demo standalone manifest 不能反哺为 Core validation evidence。
2. Runtime-visible generated artifact 必须能归类为 definition、Blob、lookup、pure glue、validation、Baker glue 或 Bootstrap glue；否则必须标记为迁移期 proof 并绑定移除任务。
3. validation report 已新增 generated lifecycle / ownership / random lookup / NativeContainer / structural change gate，并已进入 `blocking-unclassified-lifecycle-migration`；当前 generated boundary / lifecycle / ownership / structural / random lookup hit 已归零，后续必须把 release-ready gate、system budget、manifest/report/file/schedule 对账和防回流扫描补齐。
4. `BlobBuilder` 与 catalog dispose owner 必须有 Baking / Bootstrap / initialization 证据；Runtime Core hot path 只能只读 catalog。
5. generated `.gen.cs` 的修复必须落回 `GasGlueCodeGenPhases`、manifest、validation report、离线 sourcegen bat/CLI 或 Unity batchmode 生成链路；不能手改 generated output 当作架构修复。

## 当前红线

1. 不再把“没有 `cfg.*` / JSON / managed row”写成 SourceGenerator 完全合规。
2. 不再把 generated lifecycle system 写成目标态 Runtime Core。
3. 不再把 `ComponentLookup` / `BufferLookup` job 化迁移写成最终性能优化完成。
4. 不再把 AutoChessDemo catalog install 当作通用 Baking / Bootstrap contract 完成证明。
5. 不再把目标态分层、目标数据流和示例代码放入本文件；这些内容归属 `01-目标态架构共识`。
