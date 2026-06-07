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

当前链路已经走到了正确方向，且旧复审中的 Core / Demo phase 混入问题已经被第一刀缓解；`RuntimeDefinitionGluePhase` 也已从 lifecycle 输出中收窄为 pure glue 输出。但 SourceGenerator 的权限边界仍未完成，generated runtime boundary gate 目前只阻断未分类命中，显式登记为 `RuntimeLifecycleMigration` 的 `MigrationProofOnly` artifact 仍会继续生成。

正确方向包括：

1. Luban 输出保留在 Unity 编译域内。
2. GAS generated Runtime 不直接引用 `cfg.*` / `SimpleJSON` / JSON reader。
3. `GASDefinitionCatalogBlob`、sorted code lookup、`ref readonly` definition 访问已经出现。
4. `RuntimeForbiddenDependencyHits = 0` 能证明 managed config 没有直接泄漏到 generated Runtime。
5. `GasCodeGenPipeline.s_corePhases` 已不再包含 `AutoChessDemoConfigPhase`，Demo 产物改由 standalone phase 写入 `Assets/AutoChessDemo/Generated`。
6. validation report 已新增 generated lifecycle / structural / ownership / random lookup / managed config boundary 计数，并输出 `GeneratedRuntimeBoundaryGateMode: blocking-unclassified-lifecycle-migration`。
7. `GeneratedRuntimeSystemRegistrationHits = 0` 只说明 SourceGenerator 当前没有输出自注册 helper；手写 `GASSystemScheduleContract.AddSystemsByTypeName()` 仍通过 `Type.GetType(...)` 反射解析 generated systems，但解析失败时当前已抛 `InvalidOperationException`，不再静默跳过。该事实具备 fail-fast 正向证据，仍需补缺失 artifact / type mismatch / assembly unavailable 的负例验证、system 数量和 phase budget 证据。
8. `GasCodeGen.manifest.json` 当前把 `RuntimeDefinitionGlue.gen.cs` 标为 `ArtifactCategory=RuntimePureGlue`，把 `RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs`、`RuntimeActiveEffect.gen.cs` 标为 `ArtifactCategory=RuntimeLifecycleMigration`；validation 的 `MigrationProofOnly` 分类必须依赖该 manifest contract，而不是文件名白名单。

必须修正的是：生成器不应继续生成 Runtime Core lifecycle system、隐藏结构变化 owner 或大量 runtime random lookup 驱动逻辑；新增 boundary hit 不能长期只报告不阻断。SourceGenerator 当前已经不只是“配置到代码”的胶水，而是在部分路径上替 Runtime Core 拥有 gameplay 时序。

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
GeneratedRuntimeBoundaryHits: 121
GeneratedRuntimePureGlueArtifacts: 1
GeneratedRuntimeLifecycleMigrationArtifacts: 3
GeneratedRuntimeLifecycleHits: 21
GeneratedRuntimeSystemRegistrationHits: 0
GeneratedRuntimeStructuralChangeHits: 8
GeneratedRuntimeOwnershipHits: 6
GeneratedRuntimeRandomWriteLookupHits: 86
GeneratedRuntimeManagedConfigHits: 0
GeneratedRuntimeBoundaryGateMode: blocking-unclassified-lifecycle-migration
GeneratedRuntimeUnclassifiedBoundaryHits: 0
CurrentMode: blocking-unclassified-lifecycle-migration
```

这说明扫描能力已经从 forbidden dependency 扩展到 generated runtime 职责边界，且未分类 boundary hit 已会阻断生成。剩余风险是 lifecycle、structural owner、NativeContainer owner 和 random lookup owner 仍被分类为 `MigrationProofOnly` / `BootstrapDefinitionOwner` 后允许存在；R5 的任务不再是“从 0 做 blocking”，而是把这些分类迁移证明逐项迁出、阈值化或失败化。当前比旧快照更进一步的是：`MigrationProofOnly` 不再只靠文件名白名单，而是必须来自 manifest 中的 `RuntimeLifecycleMigration` 分类。

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

迁移期 lifecycle system 由 `RuntimeLifecycleMigrationPhase` 输出：

```text
Runtime/RuntimeAbilityActivation.gen.cs
Runtime/RuntimeEffectInstant.gen.cs
Runtime/RuntimeActiveEffect.gen.cs
```

后三类不再挂在 definition glue phase 下，但仍是 Runtime Core 执行管线。它们包含 `ISystem`、`OnUpdate()`、`ComponentLookup`、`BufferLookup`、`Schedule()`、ECB 和 structural owner。`RuntimeSystemRegistration.gen.cs` 当前已不在 output list，且 report 中 `GeneratedRuntimeSystemRegistrationHits = 0`，这是正向事实；但这只证明生成器没有自注册 helper，不能证明手写 schedule registry 整体达标。当前 `GASSystemScheduleContract.AddSystemsByTypeName()` 在 generated type 缺失时已 fail-fast，新增 registration helper 必须默认失败，手写 registry 还必须补缺失 artifact / type mismatch / assembly unavailable 的负例验证、system 数量和 phase budget。

官方判定：

1. `SYS-01` 要求权威 gameplay 计算落在 ECS System / Job 数据流，但 owner 必须可审计；generated glue 输出 `ISystem` 后，glue 就变成 lifecycle owner。
2. `SYS-03` 认为 system 数量是成本源；当前 registration hit 为 0，但新增 generated registration 一旦回流，系统数量和 update order 仍会绕过 Runtime Core 架构预算。
3. `QRY-01` / `QRY-04` 要求 hot path query 和 random lookup 有 job / owner-local / chunk-local 论证；模板生成 lookup 会绕过每条业务 lane 的 API 选型表。
4. `SC-01` / `ECB-03` 要求结构变化集中在明确 playback phase；generated lifecycle system 如果创建 ECB 或直接写 `EntityManager`，会制造隐藏结构变化 owner。

### P0：Validation gate 仍允许已分类 generated lifecycle `MigrationProofOnly`

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

`Generated Runtime Boundary Gate` 的 `CurrentMode` 当前是 `blocking-unclassified-lifecycle-migration`，未分类命中会被阻断；但 `GeneratedRuntimeBoundaryHits = 121` 仍被分类为迁移证明或 bootstrap owner 后允许生成。因此报告可以写 `RuntimeForbiddenDependencyHits = 0`、`GeneratedRuntimeUnclassifiedBoundaryHits = 0`，同时 generated Runtime 仍然生成实际 gameplay lifecycle。这已经从“自检盲区 / 纯报告”升级为“已阻断未知回流，但仍保留已分类迁移证明”的门禁缺口。当前 gate 的改进是：只有 manifest 分类为 `RuntimeLifecycleMigration` 的 artifact 才能承载 lifecycle / lookup / structural owner hit。

### P1：Catalog builder 的层级边界偏软

`GASGeneratedDefinitionCatalogBuilder.BuildCatalog()` 当前在 Runtime-visible generated 文件中调用 `BlobBuilder`。

如果它只在 world/bootstrap 初始化期调用，符合 `BLOB-02` 的“初始化期可用”下限；但当前 runtime-visible 暴露方式仍然偏软。事实风险是：配置 materialization、Blob dispose owner、catalog install owner 与 gameplay runtime 可见 API 混在一起，后续 hot path 审查必须持续证明没有每帧构建和没有 owner 泄漏。

### P1：generated runtime hot path 仍有大量 random lookup

`RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs`、`RuntimeActiveEffect.gen.cs` 里大量使用：

```text
ComponentLookup<T>
BufferLookup<T>
GetComponentLookup<T>()
GetBufferLookup<T>()
```

这些 API 可以作为迁移期 proof 使用，但不能被写成 scale-ready store 终局。对照 `QRY-04`，高频路径中的跨 entity random lookup 需要重构为 owner-local、chunk-local 或 frame-local record merge，并给出 capacity / ordering / x50 / x1000 profile 证据。

### P1：SourceGenerator 生成内容太厚

当前 SourceGenerator 既生成 Blob / lookup，又生成 runtime command commit、instant spec build、active effect mutation、pre tick、remove system。这会造成三个长期问题：

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
| RuntimeDefinitionGluePhase 已收窄 | `RuntimeDefinitionGluePhase` 当前只输出 `RuntimeDefinitionGlue.gen.cs`，manifest 分类为 `RuntimePureGlue`；lifecycle artifact 已拆到 `RuntimeLifecycleMigrationPhase` | `SYS-01`、`SYS-03`、`QRY-01` | phase ownership 已缓解，但 SourceGenerator 仍保留迁移期 lifecycle artifact |
| generated runtime system registration 当前为 0 | report 输出 `GeneratedRuntimeSystemRegistrationHits: 0`，output list 不含 `RuntimeSystemRegistration.gen.cs`；手写 `GASSystemScheduleContract.AddSystemsByTypeName()` 对缺失 generated type 当前 fail-fast | `SYS-01`、`SYS-03` | SourceGenerator 不自注册是正向事实；缺失 type 静默漏注册风险已缓解，但手写 registry 仍需负例验证、system 数量和 phase budget；新增 registration helper 必须默认失败 |
| generated runtime boundary gate 阻断未分类命中 | report 输出 `GeneratedRuntimeBoundaryHits: 121`、`GeneratedRuntimePureGlueArtifacts: 1`、`GeneratedRuntimeLifecycleMigrationArtifacts: 3`、`GeneratedRuntimeBoundaryGateMode: blocking-unclassified-lifecycle-migration`、`GeneratedRuntimeUnclassifiedBoundaryHits: 0` | `ODF-18`、`SYS-01`、`QRY-04`、`SC-01` | 未分类回流已被阻断；只有 manifest 分类为 `RuntimeLifecycleMigration` 的 artifact 可保留 `MigrationProofOnly` |
| generated runtime 使用 random lookup | generated lifecycle 文件内使用 `ComponentLookup<T>` / `BufferLookup<T>` | `QRY-04`、`PRF-06`、`PRF-19` | 只能作为迁移期 proof，不是 scale-ready 终局 |
| Catalog builder runtime-visible | `DefinitionCatalog.gen.cs` 暴露 `BuildCatalog()` / `BlobBuilder` | `BLOB-01`、`BLOB-02`、`BAKE-01` | 初始化可接受，但层级边界和 dispose owner 需继续证明 |
| Forbidden dependency gate 通过 | `RuntimeForbiddenDependencyHits: 0` | `ODF-18` | 只证明 managed config 未泄漏，不能证明 lifecycle / query / ECB 合规 |

## 后续事实约束

以下不是本文件的任务计划，而是后续实现必须回填证据的事实约束：

1. Core generation 默认入口必须持续证明 Core phase set 不包含 `AutoChessDemoConfigPhase` 或任何 Demo 专用 phase；Demo standalone manifest 不能反哺为 Core validation evidence。
2. Runtime-visible generated artifact 必须能归类为 definition、Blob、lookup、pure glue、validation、Baker glue 或 Bootstrap glue；否则必须标记为迁移期 proof 并绑定移除任务。
3. validation report 已新增 generated lifecycle / ownership / random lookup / NativeContainer / structural change gate，并已进入 `blocking-unclassified-lifecycle-migration`；后续必须把允许存在的 `MigrationProofOnly` 迁出 Runtime-visible lifecycle、收紧阈值或转为失败条件。
4. `BlobBuilder` 与 catalog dispose owner 必须有 Baking / Bootstrap / initialization 证据；Runtime Core hot path 只能只读 catalog。
5. generated `.gen.cs` 的修复必须落回 `GasGlueCodeGenPhases`、manifest、validation report、离线 sourcegen bat/CLI 或 Unity batchmode 生成链路；不能手改 generated output 当作架构修复。

## 当前红线

1. 不再把“没有 `cfg.*` / JSON / managed row”写成 SourceGenerator 完全合规。
2. 不再把 generated lifecycle system 写成目标态 Runtime Core。
3. 不再把 `ComponentLookup` / `BufferLookup` job 化迁移写成最终性能优化完成。
4. 不再把 AutoChessDemo catalog install 当作通用 Baking / Bootstrap contract 完成证明。
5. 不再把目标态分层、目标数据流和示例代码放入本文件；这些内容归属 `01-目标态架构共识`。
