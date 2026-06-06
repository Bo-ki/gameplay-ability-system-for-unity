# CodeGen 到 Runtime 新链路重构计划

## 目标

以 `08-Luban-SourceGenerator配置生成链路Spec.md`、`15-Luban-SourceGenerator链路复审与目标重划.md` 和 `90-目标态不变量.md` 为准，收敛 Luban / SourceGenerator 到 Definition & Generation Layer。Runtime Core 只消费 generated id、`GASDefinitionCatalogBlob`、code->index lookup、pure Generated Runtime Glue、component type set 和 validation hint；Runtime lifecycle、query ownership、dependency ownership、allocator ownership、ECB ownership 和 NativeContainer ownership 必须由手写 ECS System 拥有。

## 官方规则前提

| 规则 | 对本计划的硬约束 |
|---|---|
| `BLOB-01` | 静态 Ability / GE / Modifier / Tag 定义进入 immutable Blob，Runtime 只读 |
| `BLOB-02` | `BlobBuilder` 只能属于 Baking / Bootstrap / initialization owner，不进入 Runtime hot path |
| `BAKE-01` / `BAKE-02` / `BAKE-03` | Baker glue 必须无状态、只添加输出，Baking System 需声明依赖和增量还原 |
| `SYS-01` | gameplay 权威计算落在 ECS System / Job 数据流，不能由 generated manager 或 hidden lifecycle 驱动 |
| `SYS-03` | system 数量是成本源，SourceGenerator 不得按表或字段批量制造 system |
| `QRY-01` / `QRY-04` | hot path 必须 job 化，并避免高频 `ComponentLookup` / `BufferLookup` random lookup |
| `SC-01` / `ECB-03` | hot path 不直接结构变化；ECB playback 必须归属明确 SystemGroup phase |
| `NAT-03` | NativeStream fan-in 必须由 owner system 定义 deterministic merge 和预算 |
| `BUR-01` | hot path system / job 必须 Burst 且无托管依赖 |

## 主链路

```text
Excel / Luban row
  -> GasCodeGenContext / RowMetadata
  -> generated ids + Blob schema + static lookup
  -> Baker / Bootstrap builds GASDefinitionCatalogBlob
  -> Runtime Core hand-written System reads catalog by ref readonly
  -> Generated Runtime Glue returns frame-local records
  -> hand-written Runtime lane owns query / job / ECB / NativeContainer
```

## 2026-06-06 复审后的当前状态

已确认方向正确：

1. `GenerateAllCode()` 接入 `BeanUpdater + GasCodeGenPipeline`，Luban process gate、manifest、validation report、generated asmdef 分层方向正确。
2. `DefinitionCatalog.gen.cs` 已出现 `GASDefinitionCatalogBlob`、sorted code lookup、`ref readonly` definition 访问和 `GASGeneratedDefinitionCatalogLookup`，符合 `BLOB-01`。
3. `RuntimeDefinitionGlue.gen.cs` 中的 `GASGeneratedRuntimeDefinitionResolver`、Requirement / Magnitude evaluator、record glue 是应保留资产。
4. Luban C# 留在 Unity 编译域、generated runtime 不反向依赖 `cfg.*` / `Luban.Runtime` / `SimpleJSON` 的边界应保留。

当前 P0 违约：

1. `GasCodeGenPipeline.s_corePhases` 仍包含 `AutoChessDemoConfigPhase`；Core pipeline 仍被 Demo phase 污染。
2. `RuntimeDefinitionGluePhase` 过厚：除了 pure glue，还生成 `RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs`、`RuntimeActiveEffect.gen.cs`、`RuntimeSystemRegistration.gen.cs`。
3. generated lifecycle 文件包含 `: ISystem`、`OnUpdate(ref SystemState)`、`state.EntityManager`、`SystemAPI.GetComponentLookup`、`SystemAPI.GetBufferLookup`、`EntityCommandBuffer`、`CreateSystem()`、`AddSystemToUpdateList()`；这些只能是 `MigrationProofOnly`，不是目标态 Runtime Core。
4. `GASGeneratedDefinitionCatalogBuilder.BuildCatalog()` 在 Runtime-visible generated 文件中调用 `BlobBuilder`；目标态必须迁入 Baking / Bootstrap / initialization owner。
5. `RuntimeForbiddenDependencyHits = 0` 只能证明没有 managed config 泄露，不能证明 SourceGenerator 没有越权拥有 lifecycle / query / ECB / NativeContainer。

## 重构切片

| 顺序 | 切片 | 验收 |
|---|---|---|
| 1 | Core pipeline 去 Demo 污染 | `RunAll()` 默认 phase 不包含任何 `AutoChess*Phase` |
| 2 | SourceGenerator 收权 | `RuntimeDefinitionGluePhase` 只生成 pure glue / unmanaged record |
| 3 | Generated lifecycle 迁移 | 删除、迁移或标记 `RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs`、`RuntimeActiveEffect.gen.cs`、`RuntimeSystemRegistration.gen.cs` 为 `MigrationProofOnly` |
| 4 | Catalog owner 收口 | `BuildCatalog()` / `BlobBuilder` 归属 Baking / Bootstrap / initialization owner，Runtime hot path 只读 catalog |
| 5 | Validation hard gate | 输出 generated lifecycle / ownership / random lookup / NativeContainer owner / structural change hits |
| 6 | Runtime 消费闭环 | 手写 Runtime System 调用 pure glue，拥有 query / dependency / ECB / NativeContainer |

## 下一轮目标

1. 先收权 SourceGenerator：`RunAll()` 去除 Demo phase；`RuntimeDefinitionGluePhase` 只输出 pure glue；generated lifecycle 文件删除、迁移或标记 `MigrationProofOnly`。
2. 再重新运行完整 `GenerateAllCode()`，证明 `BeanUpdater -> Luban CLI -> GasCodeGenPipeline -> Unity compile` 主链闭环。
3. 集中静态审查 generated runtime，确认 forbidden dependency、generated naming debt 和 generated lifecycle / ownership hits 均为 0。
4. 转入手写 GAS Runtime System 消费 generated catalog / lookup / pure glue，而不是继续补 generated lifecycle system。

## 验收点

1. `GasCodeGenPipeline.RunAll()` 默认 phase 数不包含任何 `AutoChess*Phase`。
2. Runtime-visible generated 文件默认不得出现 `: ISystem`、`OnUpdate(ref SystemState)`、`CreateSystem(`、`AddSystemToUpdateList(`、`state.EntityManager`、`EntityCommandBuffer`、`SystemAPI.GetComponentLookup`、`SystemAPI.GetBufferLookup`、`ComponentLookup<`、`BufferLookup<`、`NativeList<`、`NativeStream`。
3. 如上述 token 临时存在，文件必须标记 `MigrationProofOnly`，validation report 必须列出违反的 DOTS 规则、保留原因和移除任务。
4. `RuntimeDefinitionGlue.gen.cs` 只生成 resolver / evaluator / target rule table / unmanaged record，不生成 lifecycle system、不注册 system、不隐藏 ECB/query。
5. `GASGeneratedDefinitionCatalogBuilder.BuildCatalog()` 不出现在 Runtime Core hot path；catalog 构建 owner 必须是 Baking / Bootstrap / initialization。
6. Runtime 消费链必须证明：手写 System 拥有 lifecycle 和 query，generated pure glue 只输出 frame-local record。
