# Generated Runtime boundary 归零目标态归档

日期：2026-06-08

## 目标态裁决

Runtime-visible generated artifact 可以提供 immutable catalog、lookup 和 pure data populator，但不能拥有 allocator、dispose、lifecycle、query、ECB、system registration 或 runtime structural owner。Blob materialization 的 owner 必须位于 Baking / Bootstrap / hand-written 初始化路径。

## 已落地边界

- `DefinitionCatalog.gen.cs` 的 generated runtime 职责收敛为 catalog metadata、lookup 和 `GASGeneratedDefinitionCatalogData.Populate(...)`。
- Baker 侧 `DefinitionCatalogBuilder.gen.cs` 是 Baking owner，可在 Editor/Baking 路径创建并注册 Blob。
- AutoChess headless demo 的 `AutoChessBattleDefinitionCatalogBuilder` 是 runtime bootstrap owner，可在真实业务测试前安装/卸载 catalog。
- `GasCodeGenValidationReport.md` 的 generated runtime 边界字段已归零：
  - `GeneratedRuntimeBoundaryHits = 0`
  - `GeneratedRuntimeOwnershipHits = 0`
  - `GeneratedRuntimeUnclassifiedBoundaryHits = 0`

## 防回流要求

- SourceGenerator 不得重新在 runtime-visible generated 文件输出 `BuildCatalog(Allocator...)`、`new BlobBuilder(...)` 或 hidden dispose owner。
- 如果未来新增 generated runtime-visible artifact，必须继续由 validation report 证明 lifecycle / structural / native-container / random-lookup / managed-config hit 为 0。
- AutoChess headless bootstrap 必须继续显式拥有 catalog install / uninstall 与 Blob dispose，不得把这些生命周期回流到 generated runtime。

## 下一步

Generated Runtime boundary 已从“允许 BootstrapDefinitionOwner 豁免”推进到 release-ready 的 0-hit 口径。后续应继续沿 AutoChess 全链路推进实际业务验证和性能证据，而不是再依赖 generated boundary 豁免来证明架构达标。
