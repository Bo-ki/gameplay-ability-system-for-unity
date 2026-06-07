# TagRequirement Query / Definition Glue 事实

> Owner：`00-当前架构事实/架构重划分审查事实` | 状态：当前事实专题 | 最近整理：2026-06-08

本文件是 TagRequirement query 从 Luban / SourceGenerator 到 Runtime consumer 的唯一当前事实页。其他事实文档只保留摘要入口，不再重复维护完整证据链。

## 事实边界

本页只回答当前代码已经做到什么、证据在哪里、仍然违反哪些 DOTS / SourceGenerator 边界。不在本页设计目标态 API，也不写执行任务正文。

目标态约束归入：

1. `../../01-目标态架构共识/15-SourceGenerator职责边界Spec.md`
2. `../../01-目标态架构共识/90-目标态不变量.md`

任务消费归入：

1. `../../02-主线任务树/R0-R8/R3-SingletonCarrier与FanIn选型替换.md`
2. `../../02-主线任务树/R0-R8/R5-SourceGeneratorPureGlue收权.md`
3. `../../02-主线任务树/R0-R8/R7-ActiveEffectStoreStoreOnlyLifecycle.md`

短期验证摘要归入 `../../04-当前进度状态/最近验证摘要.md`。

## 当前正向事实

1. Definition catalog 已承载 requirement 数据。
   - `Assets/GAS/Runtime/Definition/GASDefinitionCatalogRuntimeTypes.cs` 定义 `GASDefinitionCatalogBlob.Requirements`、`GASCatalogGameplayEffectDefinitionBlob.RemoveGameplayEffectTagQuery`、`GASCatalogRequirementDefinitionBlob.TagQuery` 和 `GASRequirementKind`。
   - `Assets/GAS/Generated/CodeGen/Runtime/DefinitionCatalog.gen.cs` materialize `GASCatalogRequirementDefinitionBlob` 数组、`TagRequirementMask` literal 和 GE `RemoveGameplayEffectTagQuery`。
2. CodeGen 模板已经把 all / any / none query 纳入生成链。
   - `GasGlueCodeGenPhases.cs` 中的 `BuildTagRequirementMask(...)`、`AddTagRequirement(...)` 和 `TagRequirementMaskLiteral(...)` 负责从 normalized row 生成 `TagRequirementMask`。
   - Ability 侧覆盖 `ActivationRequiredTags` / `ActivationBlockedTags`；GE 侧覆盖 `ApplicationRequiredTags` / `OngoingRequiredTags` / `RemoveGameplayEffectsWithTags` / `ImmunityTags`。
3. Pure evaluator 已集中在 generated definition glue。
   - `RuntimeDefinitionGlue.gen.cs` 中 `GASGeneratedRequirementEvaluator` 提供 `EvaluateAbilityRequirements(...)` 和 `EvaluateGameplayEffectRequirements(...)`。
   - evaluator 只消费 immutable catalog definition / requirement range 和调用方传入的 tag snapshot，不拥有 query、ECB、NativeContainer 或 runtime lifecycle。
4. 当前三个 Runtime 入口已消费同一个 evaluator。
   - Ability commit 路径调用 `EvaluateAbilityRequirements(...)`。
   - Active effect mutation apply 路径调用 `EvaluateGameplayEffectRequirements(...)`。
   - Instant GE spec build 路径已补 target tag evaluator，避免 modifier/cue instant GE 绕过 `ApplicationRequiredTags`。

## 当前剩余风险

1. `RuntimeEffectInstant.gen.cs` 仍通过 `SystemAPI.GetComponentLookup<TagMaskComponent>(isReadOnly: true)` 读取 target tag。该行为发生在 manifest 分类为 `RuntimeLifecycleMigration` 的 generated artifact 内，只能作为 `MigrationProofOnly`，不能写成 SourceGenerator pure glue 完成。
2. `ActiveEffectLifecycleOwnerSystems.cs` 已退出 SourceGenerator 输出、manifest 和 generated boundary report，但仍是 generated runtime 物理 asmdef 内的手写 companion owner，包含 lookup owner、ECB / structural owner 和 active effect remove / pre-tick / mutation apply 调度逻辑；`RuntimeActiveEffect.gen.cs` 继续承载 helper/job 和 snapshot key。即使其中部分 tag requirement 已消费 owner-local resource 或 evaluator，也不能把整个 active effect 输出写成目标态 Runtime lane owner。
3. `GasCodeGenValidationReport.md` 当前仍显示 generated runtime boundary hits 非 0：`GeneratedRuntimeBoundaryHits=63`、`GeneratedRuntimeLifecycleMigrationArtifacts=3`、`GeneratedRuntimeLifecycleHits=9`、`GeneratedRuntimeStructuralChangeHits=5`、`GeneratedRuntimeOwnershipHits=1`、`GeneratedRuntimeRandomWriteLookupHits=48`、`GeneratedRuntimeBoundaryGateMode=blocking-unclassified-lifecycle-migration`、`GeneratedRuntimeUnclassifiedBoundaryHits=0`。这说明未分类回流已阻断，但已分类 `MigrationProofOnly` 仍未退出。
4. Run5 业务样本仍偏低量，且 tag query 组合覆盖不足。当前生成物里存在非空 `TagQuery` literal，但不能据此证明所有 Ability / GE / removal / immunity 组合已被规模化验证。
5. `TagMaskComponent` live lookup 若在高频 generated lifecycle 中继续扩散，仍触发 `QRY-04` / `PRF-06` / `PRF-19` 风险。目标态应由 handwritten Runtime Core lane 传入 owner-local tag snapshot、target-grouped tag record 或 frame-local snapshot record。

## Run5 验证边界

有效日志：`../_归档/2026-06-08-AutoChessBattleValidation-TagRequirementQuery-Run5.log`

Run5 可证明：

1. AutoChess x50 validation 未被 TagRequirement catalog / evaluator 贯通破坏。
2. `AutoChessDemoValidationRunResult` 输出 `passed=True`、`thresholdsPassed=True`、`runtimeChainPassed=True`、`repeatRunPassed=True`。
3. `AutoChessDemoRuntimeRunner` 输出 `blockingDebugErrors=0`、`periodTickDamageFacts=150`、`activeEffectSlots=100`、`pendingAttributeAppliedDeltas=250`、`performancePassObservationPollutionRisks=0`、`factsHash=0xA4A93C35`、`summaryHash=0xB7198D87`。
4. 同轮日志没有命中本轮已知阻断症状：`Exception`、`SnapshotLaneCounters`、`Use CollectionHelper`、`error CS`。

Run5 不能证明：

1. DOTS 性能优秀或 Profiler enabled 闭环。日志仍显示 `profiler disabled; Entities profiler modules collect no data`。
2. x100 / x1000 scale profile 完成。
3. SourceAttribute / TargetAttribute 非零业务样本覆盖。`magnitudeSource*` 字段仍为 0。
4. generated lifecycle owner 退出。manifest 中 `RuntimeAbilityActivation.gen.cs` / `RuntimeEffectInstant.gen.cs` / `RuntimeActiveEffect.gen.cs` 仍是 `RuntimeLifecycleMigration`；`ActiveEffectLifecycleOwnerSystems.cs` 已退出 manifest，但仍是 generated runtime 物理 asmdef 内的手写 companion owner。
5. 所有 tag requirement 组合、GE removal query、immunity query 和 tag taxonomy 分支都已覆盖。

## DOTS 判定

| 事实项 | 规则 | 判定 |
|---|---|---|
| Requirement 数据进入 immutable catalog / blob range | `BLOB-01`、`BLOB-02`、`SEL-01` | 正向事实；适合作为 Runtime Core 只读输入 |
| Pure evaluator 不拥有 ECS lifecycle | `BUR-01`、`SYS-01`、`ODF-18` | 正向事实；属于 SourceGenerator 允许输出 |
| generated lifecycle 内读取 `ComponentLookup<TagMaskComponent>` | `QRY-04`、`PRF-06`、`PRF-19` | 迁移期 proof；目标态应由 hand-written lane 传入 snapshot |
| generated lifecycle / ECB / lookup owner 仍在 report 中分类存在 | `SYS-03`、`SC-01`、`ECB-03`、`NAT-01` | `MigrationProofOnly`；R5 必须给退出门或 release-ready fail gate |
| x50 Run5 跑通 | `DBG-01..05`、`ODF-09` | 业务链路证据；不能替代 Profiler / scale / API health 结论 |

## 消费规则

1. `Runtime主链事实.md`、`CodeGen链路复审事实.md`、`SourceGenerator链路复审事实.md`、`AutoChessDemo事实.md` 只保留摘要和本页链接，避免重复维护完整 TagRequirement 事实正文。
2. `01-目标态架构共识` 只能写抽象目标：immutable tag query catalog、pure evaluator、owner-local / snapshot tag input、generated lifecycle 禁止方向；不得写 Run5 数字、当前 `.gen.cs` 行号或 report 当前命中数。
3. R3/R5/R7 触达 tag requirement、target tag read、generated requirement evaluator 或 active effect removal query 时，必须先消费本页。
4. 任何交还若仍依赖 generated lifecycle live `ComponentLookup<TagMaskComponent>`，只能交还为 `MigrationProofOnly` 或退出任务，不能交还为目标态完成。
