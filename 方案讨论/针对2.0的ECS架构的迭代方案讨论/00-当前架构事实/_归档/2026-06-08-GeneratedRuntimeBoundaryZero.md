# Generated Runtime boundary 归零归档

日期：2026-06-08

## 背景

删除 active-effect generated wrapper 后，`GasCodeGenValidationReport.md` 仍保留一条 generated runtime 边界命中：

- `GeneratedRuntimeBoundaryHits = 1`
- `GeneratedRuntimeOwnershipHits = 1`
- 命中项为 `DefinitionCatalog.gen.cs` 中的 `GASGeneratedDefinitionCatalogBuilder.BuildCatalog(Allocator allocator = Allocator.Persistent)`，分类为 `BootstrapDefinitionOwner`

该命中不是每帧 hot path lifecycle system，但它仍让 runtime-visible generated artifact 拥有 `BlobBuilder` / allocator / dispose 生命周期，不符合 release-ready 的 SourceGenerator 职责边界。

## 本轮变更

- `DefinitionCatalog.gen.cs` 不再生成 `BuildCatalog(...)`，改为生成 `GASGeneratedDefinitionCatalogData.Populate(ref BlobBuilder builder, ref GASDefinitionCatalogBlob root)`。
- `DefinitionCatalogBuilder.gen.cs` 的 Baker 在 Baking 初始化路径中拥有 `BlobBuilder`、`Allocator.Persistent` 与 `Dispose`。
- `AutoChessBattleDefinitionCatalogBuilder` 在 AutoChess headless bootstrap 路径中拥有 `BlobBuilder`、`Allocator.Persistent` 与 `Dispose`，再调用 generated data populator 填充不可变 catalog。
- `GasGlueCodeGenPhases.cs` 模板同步调整，避免下次 codegen 回流 generated runtime allocator owner。

## 验证

- `dotnet run --project Tools\GasCodeGenCli\GasCodeGenCli.csproj -- core`：通过。
- `dotnet build .\com.exhard.exgas.generated.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`：通过；仅既有 `MSB3277` 警告。
- `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`：通过；仅既有 `MSB3277` 警告。
- `dotnet build .\com.exhard.exgas.generated.editor.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`：通过；仅既有 `MSB3277` 与 Editor API obsolete 警告。
- `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1`：当前工作树通过；该脚本仍有并行脏改，结论只作为当前工作树门禁。
- `GasCodeGenValidationReport.md`：`GeneratedRuntimeBoundaryHits = 0`，`GeneratedRuntimeOwnershipHits = 0`，`GeneratedRuntimeUnclassifiedBoundaryHits = 0`。

## 结论

Generated runtime-visible artifact 当前不再拥有 lifecycle、registration、structural change、NativeContainer owner、random write lookup 或 managed config 命中。Definition catalog 的 allocator / dispose owner 已落到 Baker 初始化和 AutoChess headless bootstrap 这两个明确调用点。
