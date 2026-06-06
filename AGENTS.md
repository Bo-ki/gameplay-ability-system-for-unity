# Repository Guidelines

## Project Structure & Module Organization
This repository is a Unity project (2022.3 LTS) centered on EX-GAS 2.0.

- `Assets/GAS/Runtime`: core gameplay ability system runtime (ECS/DOTS, tags, effects, abilities).
- `Assets/GAS/Editor`: custom tools (GAS Center, timeline editor, web editors).
- `Assets/GAS/General`: shared utilities/constants used by runtime and editor code.
- `Assets/DemoForESC`, `Assets/EXUI`, `Assets/_EXProceduralMachine`: demo/gameplay integration content.
- `EX_GAS_Config/ProjectConfigTable/exgas_config`: Luban Excel-to-JSON config source (`Datas/`, `gen.bat`, `gen.sh`).
- `Packages/`, `ProjectSettings/`: Unity package and project config.

Do not commit generated caches/build folders like `Library/`, `Temp/`, `Logs/`, or `obj/`.

## Agent skills

### Issue tracker

Use GitHub issues for issue/PRD workflows in this repo. Prefer the `origin` repository unless the user explicitly asks to target `upstream`. See `docs/agents/issue-tracker.md`.

### Triage labels

Use the default mattpocock/skills triage label vocabulary (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Use a single-context domain documentation layout. Read `CONTEXT.md` and `docs/adr/` when present; also treat the existing `方案讨论/针对2.0的ECS架构的迭代方案讨论/当前路线/` docs as supporting architecture context. See `docs/agents/domain.md`.

## Build, Test, and Development Commands
- Open project with Unity `2022.3.62f3` (see `ProjectSettings/ProjectVersion.txt`).
- Regenerate config JSON from Excel:
  - Windows: `EX_GAS_Config\ProjectConfigTable\exgas_config\gen.bat`
  - macOS/Linux: `bash EX_GAS_Config/ProjectConfigTable/exgas_config/gen.sh`
- Run EditMode tests (batchmode):
  - `Unity.exe -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml`
- Run PlayMode tests (batchmode):
  - `Unity.exe -batchmode -quit -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/PlayMode.xml`

## Coding Style & Naming Conventions
- Language: C# with 4-space indentation, braces on new lines (match existing files).
- Keep namespaces consistent with folder intent (for GAS code, typically `GAS.Runtime`, `GAS.Editor`, `GAS.General`).
- Type naming follows project patterns:
  - `C*` for ECS components, `B*` for buffer elements, `S*` for systems, `Conf*` for config loaders.
- Preserve Unity `.meta` files for any moved/added assets.

## Testing Guidelines
- Primary framework: Unity Test Framework (`com.unity.test-framework`).
- Keep project-specific tests under `Assets/_Test` or dedicated test asmdefs; avoid mixing with plugin vendor tests.
- Name test classes by behavior and suffix with `Tests` where possible (for example, `GameplayCueTests`).

## Commit & Pull Request Guidelines
- Recent history shows short, task-focused messages in both Chinese and English (for example: `docs: ...`, `Update ...`, `修复...`).
- Prefer format: `<scope>: <imperative summary>` (examples: `runtime: fix effect disposal`, `docs: update GAS workflow`).
- PRs should include:
  - What changed and why.
  - Affected modules/paths (for example `Assets/GAS/Runtime/Effect`).
  - Validation evidence (test run, editor verification, or screenshots for tooling/UI changes).

## codedb-mcp 检索约定

- 当需要按自然语言语义、业务概念或模糊描述查找代码时，优先使用 `codedb_search`，不要先大范围读取源码树。
- 当需要精确文本、正则或字符串搜索时，使用 `codedb_search`，必要时传入 `regex=true`；如果结果看起来不完整，先用 `codedb_status` 检查索引范围和扫描状态。
- 当需要查找符号定义、文件大纲或读取局部代码上下文时，优先使用 `codedb_symbol`、`codedb_outline`、`codedb_read`，并用行号范围控制上下文大小。
- 当需要查找符号引用、调用方或“哪里用了这个类/方法”时，优先使用 `codedb_callers`；如果已知定义位置，传入 `definition_path` 和 `definition_line`。
- 当需要分析文件依赖、反向依赖或跨模块关系时，优先使用 `codedb_deps`。
- 当一次任务需要多个搜索、outline、read 或依赖查询时，优先使用 `codedb_bundle`、`codedb_query` 或工具自带的 batch 参数，减少 MCP 往返和 token 消耗。
- 当怀疑索引不新鲜或监听未生效时，先调用 `codedb_status`、`codedb_changes` 或 `codedb_hot` 检查状态。
