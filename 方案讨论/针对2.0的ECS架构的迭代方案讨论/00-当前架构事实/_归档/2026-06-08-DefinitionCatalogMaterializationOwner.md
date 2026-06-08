# DefinitionCatalog materialization owner 收口

> 日期：2026-06-08
> 范围：`Assets/GAS/Generated/CodeGen/Runtime/DefinitionCatalog.gen.cs`

## 本轮结论

`GASGeneratedDefinitionCatalogBuilder.BuildCatalog()` 不应继续作为 runtime-visible generated artifact 暴露。Generated Runtime 的职责应收敛为 immutable catalog 数据填充和 lookup glue；`BlobBuilder` materialization、allocator 与 dispose 责任必须由明确的 Baking / bootstrap owner 持有。

本轮将 generated runtime 输出收窄为：

- `GASGeneratedDefinitionCatalogInfo`
- `GASGeneratedDefinitionCatalogLookup`
- `GASGeneratedDefinitionCatalogData.Populate(ref BlobBuilder, ref GASDefinitionCatalogBlob)`

Editor/Baking 的 `GASGeneratedDefinitionCatalogBaker` 在 Baker 边界内构建 Blob 并调用 `AddBlobAsset(...)`。AutoChess 的 `AutoChessBattleDefinitionCatalogBuilder` 在 demo bootstrap / catalog install owner 内构建 runtime-created catalog Blob，并在 uninstall 时 dispose。

## 本轮改动

1. `DefinitionCatalog.gen.cs` 删除 generated runtime `BuildCatalog(...)` API，改为 `Populate(...)` 数据填充 glue。
2. `GasGlueCodeGenPhases` 的 DefinitionCatalog phase 同步生成 `GASGeneratedDefinitionCatalogData`。
3. Generated Editor Baker 改为在 `Bake(...)` 内局部拥有 `BlobBuilder` materialization。
4. AutoChess catalog install 改为在 `AutoChessBattleDefinitionCatalogBuilder` 内局部拥有 bootstrap materialization，不再调用 generated runtime builder。
5. `GasCodeGenValidationReport` gate 增加 generated runtime `new BlobBuilder(` / `CreateBlobAssetReference<` 回流阻断，旧 builder 回流会变成 boundary hit。
6. `ISSUE-005` 更新为 generated catalog / pure glue 可保留、Blob materialization owner 不得回流 generated runtime 的风险口径。

## 不能推出的结论

1. 这不代表 `DefinitionCatalogLifetime` 已完成目标态 capability；AutoChess 仍在 demo adapter bootstrap 内直接创建 catalog entity 并负责 Blob dispose。
2. 这不代表 generated pure glue 与 hand-written resolver 的职责已经对账完成。
3. 这不代表 SourceGenerator release-ready gate 已完成；system budget、缺失 artifact/type mismatch/assembly unavailable 负例和全流程 sourcegen 重跑仍未闭合。
4. 这不代表 AutoChess unit/scenario/scale/validation expectation 已配置化。

## 验收要求

1. `Assets/GAS/Generated/CodeGen/Runtime` 不应出现 `GASGeneratedDefinitionCatalogBuilder`、`new BlobBuilder(` 或 `CreateBlobAssetReference<`。
2. `GasCodeGenValidationReport.md` 应保持 `GeneratedRuntimeBoundaryHits: 0`、`GeneratedRuntimeOwnershipHits: 0`、`GeneratedRuntimeUnclassifiedBoundaryHits: 0`。
3. `com.exhard.exgas.generated.runtime.csproj`、`com.exhard.exgas.runtime.csproj`、`com.exhard.exgas.generated.editor.csproj`、`com.exhard.exgas.editor.csproj` 必须能在当前切片下编译。
4. 后续若恢复 generated runtime builder，必须先给出 Baking / bootstrap owner 不适用的官方规则证据；否则默认阻断。
