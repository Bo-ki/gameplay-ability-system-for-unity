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
4. `RuntimeDefinitionGlue.gen.cs` 中的 `GASGeneratedRuntimeDefinitionResolver`、Requirement / Magnitude evaluator、record glue 是正向资产：它们把 definition index/range 转成 frame-local record，不需要 managed row、JSON 或 `Dictionary`。
5. `GasCodeGenPipeline.s_corePhases` 已不再包含 `AutoChessDemoConfigPhase`；Demo 生成改由 `s_autoChessDemoPhases` standalone 写入 `Assets/AutoChessDemo/Generated`。
6. `GasCodeGenValidationReport.md` 当前不仅输出 forbidden dependency 和 naming debt 命中数，也输出 generated runtime boundary hits；其中 `GeneratedRuntimeSystemRegistrationHits = 0` 是 SourceGenerator 不自注册的正向事实。手写 `GASSystemScheduleContract.AddSystemsByTypeName()` 当前对缺失 generated type 已 fail-fast 抛错，不再静默跳过；剩余证据缺口转为缺失 artifact / type mismatch / assembly unavailable 的负例验证、system 数量、phase budget 和 validation gate 对账。

## 当前 P0 / P1 违约事实

### 已缓解：Core pipeline 不再直接包含 Demo phase

旧复审中的 `s_corePhases` 包含 `AutoChessDemoConfigPhase` 已不是当前事实。当前 Core phase set 只包含 Core phases，Demo phase 改为 standalone phase 和 standalone output root。

剩余约束是消费链路要分 owner：`TryRunAll()` 仍然先跑 Core 再跑 AutoChessDemo standalone，所以 Core validation report 只消费为 Runtime Core 生成链证据；AutoChessDemo generated manifest / scenario artifact 只能消费为业务验收或 Demo 证据。

### P0：RuntimeDefinitionGluePhase 生成 lifecycle artifact

`RuntimeDefinitionGluePhase` 当前除了 pure glue，还生成：

```text
Runtime/RuntimeAbilityActivation.gen.cs
Runtime/RuntimeEffectInstant.gen.cs
Runtime/RuntimeActiveEffect.gen.cs
```

这些 generated 文件包含 `ISystem`、`OnUpdate(ref SystemState)`、`SystemAPI.GetComponentLookup`、`SystemAPI.GetBufferLookup`、`EntityCommandBuffer`、structural owner 和大量 random lookup。`RuntimeSystemRegistration.gen.cs` 当前已不在 output list，且 report 中 `GeneratedRuntimeSystemRegistrationHits = 0`；手写 registry 当前已把缺失 generated type 从 silent skip 改为 fail-fast。这个修复只关闭“缺失 type 静默漏注册”风险，不等于主链注册整体合规；新增 registration helper 仍必须按目标态禁止项处理，手写 registry 还必须补负例验证、system 数量、phase budget 和 generated assembly 可用性证据。

判定依据：

1. `SYS-01`：权威 gameplay 计算必须落在 ECS System / Job 数据流，但生命周期 owner 应由 Runtime Core 架构显式拥有。
2. `SYS-03`：system 数量是成本源；当前 registration hit 为 0，但新增 generated registration 一旦回流，仍不能绕过 system budget。
3. `QRY-01` / `QRY-04`：hot path query 与高频 random lookup 必须有 owner-local / chunk-local 选型论证。
4. `SC-01` / `ECB-03`：结构变化必须归属明确 phase，不能由 generated glue 隐藏 owner。
5. `BUR-01`：hot path system/job 必须 Burst 且无托管依赖，generated output 与模板同等受审。

### P1：Catalog builder runtime-visible

`GASGeneratedDefinitionCatalogBuilder.BuildCatalog()` 当前在 Runtime-visible generated 文件中调用 `BlobBuilder`。如果只在 bootstrap / initialization 调用，它符合 `BLOB-02` 下限；但当前层级边界仍偏软，因为 runtime-visible API 暴露了 catalog materialization 和 dispose owner 责任。

判定依据：`BLOB-01` / `BLOB-02` / `BAKE-01` 要求静态 definition 进入 immutable Blob，`BlobBuilder` 只在 Baking 或初始化期使用。Runtime Core hot path 只能只读 catalog。

### P0：Validation report 已进入 blocking-unclassified gate，但仍允许 `MigrationProofOnly`

`RuntimeForbiddenDependencyHits = 0`、`RuntimeGeneratedNamingDebtHits = 0` 只能证明没有 managed config 泄漏和命名债。当前 report 已经能输出 generated runtime boundary 分类，并且 `GeneratedRuntimeBoundaryGateMode` / `CurrentMode` 已是 `blocking-unclassified-migration-proof`。这说明未分类 boundary hit 会被阻断；但 `GeneratedRuntimeBoundaryHits = 135` 仍以 `MigrationProofOnly` / `BootstrapDefinitionOwner` 等分类留在生成产物中，所以它不是 SourceGenerator 完成证明。

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
| RuntimeDefinitionGluePhase 过厚 | `GasGlueCodeGenPhases.cs` 写出 runtime activation / instant / active lifecycle artifact | `SYS-01`、`SYS-03` | SourceGenerator 越权生成 lifecycle |
| generated runtime registration 当前为 0 | validation report 输出 `GeneratedRuntimeSystemRegistrationHits: 0`；手写 registry 对缺失 generated type 当前 fail-fast 抛错 | `SYS-03` | SourceGenerator 不自注册是正向事实；缺失 type 静默漏注册风险已缓解，但主链注册仍需 system 数量、phase budget、type mismatch / assembly unavailable 负例验证；新增 registration 默认失败 |
| generated runtime boundary gate 已阻断未分类命中 | validation report 输出 `GeneratedRuntimeBoundaryHits: 135`、`GeneratedRuntimeBoundaryGateMode: blocking-unclassified-migration-proof`、`GeneratedRuntimeUnclassifiedBoundaryHits: 0` | `ODF-18`、`SYS-01`、`QRY-04` | 未分类回流已进入 blocking；已分类 `MigrationProofOnly` 仍需 R2/R3/R5 退出 |
| generated runtime random lookup | generated lifecycle 文件使用 `ComponentLookup<T>` / `BufferLookup<T>` | `QRY-04` | 迁移期 proof，非 scale-ready 终局 |
| catalog blob 已出现 | `DefinitionCatalog.gen.cs` 有 sorted lookup / `ref readonly` access | `BLOB-01` | 正向事实 |
| `BlobBuilder` runtime-visible | `DefinitionCatalog.gen.cs` 暴露 `BuildCatalog()` | `BLOB-02`、`BAKE-01` | 初始化可接受，层级 owner 需收口 |
| forbidden dependency gate 通过 | report 输出 forbidden dependency hit 为 0 | `ODF-18` | 只能证明局部边界 |

## 后续事实约束

以下不是本文件的任务计划，而是后续实现必须回填证据的事实约束：

1. `GasCodeGenPipeline.RunAll()` 或默认 Core 入口必须持续证明 Core phase set 不包含 `AutoChessDemoConfigPhase` 或任何 Demo 专用 phase；Demo standalone evidence 不能混写为 Core validation evidence。
2. `RuntimeDefinitionGluePhase` 必须能证明只输出 pure glue / unmanaged record；任何 generated lifecycle artifact 必须迁出 Runtime-visible 层，或标记为迁移期 proof 并绑定移除任务。
3. `BuildCatalog()` / `BlobBuilder` 必须有 Baking / Bootstrap / initialization owner 和 dispose owner 证据。
4. Validation report 已新增 generated lifecycle / ownership / random lookup / NativeContainer / structural change gate，并已进入 `blocking-unclassified-migration-proof`；后续必须把已分类 `MigrationProofOnly` 逐项迁出、阈值化或失败化，不能只停留在“允许的 `MigrationProofOnly`”。
5. Runtime 消费链必须证明至少一条 Ability / GE 链路只读 `GASDefinitionCatalogBlob` / lookup / pure glue，不反查 per-definition entity、managed row、JSON 或 `Dictionary`。

## 当前红线

1. 不再把“generated code 已存在”写成架构完成；generated code 进入主链后必须按 Runtime Core 规则审查。
2. 不再把 `RuntimeForbiddenDependencyHits = 0` 写成 SourceGenerator 完全合规。
3. 不再把 generated lifecycle system 当作目标态 Runtime Core。
4. 不再把 AutoChessDemo phase、scenario、validation artifact 混入 Core CodeGen 默认路径。
5. 不手改 `.gen.cs` 修复架构问题；修复必须落回 codegen phase、manifest、validation report、离线 sourcegen bat/CLI 或 Unity batchmode 生成链路。
