# GFX-02: Runtime 创建渲染 entity 必须使用 RenderMeshUtility.AddComponents

**严重度**: P0
**Primary Owner**: EntitiesGraphics
**来源**: `runtime-entity-creation.md`、`overview.md`

## 规则声明
Runtime 路径下创建具有渲染表现的 entity 时，必须使用 `RenderMeshUtility.AddComponents` API。禁止手动拼装 Entities Graphics 内部组件。

## 为什么
`RenderMeshUtility.AddComponents` 自动添加所有必需内部组件（`RenderMesh`、`RenderBounds`、`RenderFilterSettings`、`ChunkWorldRenderBounds`、`WorldRenderBounds`）。手动拼装遗漏任何必需的内部组件导致渲染静默失败（entity 存在但不渲染），无编译错误或运行时异常。

## EX-GAS 诊断
搜索 `AddComponent<RenderMesh` / `AddComponent<RenderBounds` 等模式，标记为潜在违规。

## 检查方法
搜索 `AddComponent<RenderMesh` / `AddComponent<MaterialMeshInfo` 按组件名手动拼装的模式；标记要求改为 `RenderMeshUtility.AddComponents`。
