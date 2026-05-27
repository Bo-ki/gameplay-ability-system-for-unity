# CodeGen 到 Runtime 新链路重构计划

## 目标

以 `08-Luban-SourceGenerator配置生成链路Spec.md` 为准，直接用 `GasCodeGenPipeline + Context + RowMetadata + Phase + Manifest` 替换旧的分散生成器链路。当前阶段不维护旧链路兼容，不新增 wrapper，不让 Runtime Core 反查 Luban managed row、JSON、ScriptableObject 或 Editor registry。

## 主链路

1. `BeanUpdater` 只负责确保 Luban / bean 输入存在。
2. `GasCodeGenContext` 单次扫描 `*DefinitionRow`，生成统一 `RowMetadata`、输入 hash、输出根目录和命名空间。
3. `IGasCodeGenPhase` 只消费 context，不自行扫描程序集，不自行推断全局命名。
4. `GasCodeGenManifest` 记录输入 hash、输出文件、层级、runtime 可见性、版本控制归属，并清理 orphan artifact。
5. Runtime Core 只消费 generated id、Blob schema、unmanaged lookup、component type set、query layout 和 static switch。

## 2026-05-27 实现审查结论

本轮审查以 `08-Luban-SourceGenerator配置生成链路Spec.md`、`90-目标态不变量.md`、`13-EntityComponent物理布局Spec.md` 和 DOTS 规则索引为准，结论是当前计划必须从 AutoChess 业务闭环退回到通用 Luban / SourceGenerator 核心生成链路。

### 当前偏差

1. `GasCodeGenPipeline.RunAll()` 仍默认执行 `AutoChessAttributeComponentPhase`、`AutoChessTagMaskPhase`、`AutoChessUnitConfigPhase`、`AutoChessMmcEvaluatorPhase`、`AutoChessScenarioBuildPlanPhase`。这违反 `08` 的通用 `Context -> RowMetadata -> Phase -> Manifest` 主链定位，也把 demo 旧架构混进了核心生成链。
2. `RowMetadataFactory` 仍在代码里硬编码 `HeadlessAutoChess` 前缀。根据 `08` 的 `GasCodeGenSettings` 要求，项目名前缀只能来自配置，不能存在于生成器实现。
3. `ValidationReportPhase` 的报告内容仍以 AutoChess runtime artifact 为中心，无法作为 Core CodeGen 的验收报告。
4. Runtime 侧已出现若干 proof-only 实现，例如主线程遍历、`ToEntityArray` 和直接 `EntityManager` 写入路径。根据 `QRY-01`、`JOB-01`、`PRF-05`、`SC-01`、`PRF-02`，这些不能扩展为目标态 hot path；当前轮只允许保留为迁移期证据，不继续围绕 AutoChess 补业务。
5. AutoChessDemo 中的 generated row adapter、scenario driver、runtime projection 仍混有旧结构。它们不是当前轮验收对象，继续补这些文件会稀释 Luban / SourceGenerator 主链收敛。
6. 按 `12-命名规范Spec.md` 审查，当前生成模板会产出 `BlobAbilityDefinition`、`GasGeneratedDefinitionIndex`、`GeneratedDefinitionBlobComponent<T>`、`DefinitionCodeComponent` 等命名，未遵循 `GAS` 框架前缀、`*DefinitionBlob` Blob 根类型后缀和 `[GAS前缀][职责][DOTS后缀]` 规则。

### 重新定位

当前两到三轮目标只验收 **通用 CodeGen Core**：

1. `GenerateAllCode()` 默认只运行 `BeanUpdater + CoreGasCodeGenPipeline`。
2. Core phase 固定为：`AssemblyDefinition`、`DefinitionIndex`、`BlobSchema`、`StaticLookup`、`BakerGlue`、`ComponentTypeSet`、`QueryLayout`、`ValidationReport`。
3. AutoChess 专用 phase 只能作为显式 demo phase 存在，不进入默认 `RunAll()`，不作为当前验收目标。
4. Core generated 输出根目录固定收敛到 `Assets/GAS/Generated/CodeGen`；AutoChessDemo 不再作为默认 glue 输出位置。
5. `RowMetadataFactory` 的项目名前缀剥离、生成 namespace、输出目录必须由 `GasCodeGenSettings` 或 `GASSettingAsset` 承载。
6. Runtime-visible generated artifact 不得出现 `DefinitionRow`、row factory、`IReadOnlyList<*DefinitionRow>`、`cfg.*`、`XLuban`、`SimpleJSON`、`JsonReader` 或 managed row snapshot。
7. Editor / Baking 侧允许 row-based builder，但必须处于 `#if UNITY_EDITOR`、Baker glue 或 Baking artifact 层，且 manifest 标记为非 runtime-visible。
8. Validation report 必须输出 DOTS 规则对照、manifest layer 表、runtime forbidden dependency scan、naming debt scan、Baker/Blob/lookup 边界，不输出 AutoChess 业务完成度。
9. 新增 generated Runtime Core 类型必须遵守 `12`：Blob 根类型使用 `{Domain}DefinitionBlob`，静态 lookup 使用 `{Domain}DefinitionLookup`，通用 generated component 使用 `GASGeneratedDefinitionBlobComponent<T>` 与 `GASDefinitionCodeComponent`，框架产物统一使用 `GASGenerated*` 前缀。

## 破坏性替换切片

| 顺序 | 切片 | 验收 |
| --- | --- | --- |
| 1 | Core pipeline 默认化 | `生成所有` 只运行 core phases；AutoChess phase 从默认主链移出 |
| 2 | Output root / asmdef 收敛 | 默认输出到 `Assets/GAS/Generated/CodeGen`，并由 generated runtime/editor asmdef 分层；Runtime asmdef 不引用 row source，Editor asmdef 自动引用实际 row source assembly |
| 3 | Settings 去硬编码 | generated namespace、输出根目录、row prefix strip 来自配置；生成器代码中不出现项目名前缀 |
| 4 | Naming gate | Core generated names 满足 `12`：`GAS*` 框架前缀、`*DefinitionBlob`、`*Lookup`、`*Component`、`*BakePlan` |
| 5 | DefinitionIndex | 生成 row-free unmanaged metadata index，Runtime 可按 `GASDefinitionKind` 查询定义规模和 schema hash |
| 6 | Runtime / Baking 分层 | manifest 区分 Runtime、Baking、Editor/CI；runtime-visible 文件无 managed row / JSON / cfg 引用 |
| 7 | Blob / Static lookup | runtime lookup 只持有 `NativeArray<int>` 与 `NativeArray<BlobAssetReference<T>>`，row-based builder 只在 Editor / Baking |
| 8 | Baker glue | 生成 stateless `Baker<TAuthoring>`，调用 `AddBlobAsset()`，只添加 component，不读取其他 Baker 输出 |
| 9 | Report gate | `GasCodeGenValidationReport.md` 输出 DOTS 规则、命名规则、layer、manifest、forbidden dependency、naming debt 和 orphan 清理证据 |

## 当前推进状态

已经具备的 Core 基础：

- 主入口 `GenerateAllCode()` 已切到 `BeanUpdater + GasCodeGenPipeline`。
- `GasCodeGenPipeline.RunAll()` 已默认只执行 core phases；AutoChess phase 已拆到显式 `RunAutoChessDemo()`。
- `AssemblyDefinitionPhase` 已进入 core phases，generated runtime/editor asmdef 由管线按当前 row source assembly 自动生成。
- `DefinitionIndexPhase` 已进入 core phases。
- `GASSettingAsset` / `GasCodeGenSettings` 已承载 generated namespace、输出根目录、row prefix strip。
- 默认输出根目录已从 AutoChessDemo 切到 `Assets/GAS/Generated/CodeGen`，并补充 generated runtime/editor asmdef 分层。
- `GasCodeGenContext` 已能单次扫描 `*DefinitionRow`，计算 `InputHash`。
- `RowMetadata` 已包含 `DefinitionKind`、code field、Blob schema、lookup、Baker、component set、query layout、row factory 和 `RowValueSnapshot`。
- `RowMetadataFactory` 已按 `12` 改为 `{Domain}DefinitionBlob` / `{Domain}DefinitionLookup` 生成命名，且不再硬编码 `HeadlessAutoChess`。
- `StaticLookupPhase` 已拆分 Runtime lookup 与 Editor/Baking row builder。
- `BakerGluePhase` 已生成 `Baker<TAuthoring>`、`AddBlobAsset()` 和 `GASGeneratedDefinitionBlobComponent<T>`。
- `GasCodeGenManifest` 已记录输出文件、层级、runtime 可见性、版本控制归属，并能按旧 manifest 清理 orphan。
- `ValidationReportPhase` 已输出 manifest layer 表、DOTS 规则表、runtime forbidden dependency scan 和 runtime generated naming debt scan。
- `ValidationReportPhase` 已输出 Luban C# / JSON 输出边界，明确 Luban C# 必须留在 Unity 编译域内，同时 GAS generated Runtime 禁止反向依赖 `cfg.*` / `Luban.Runtime` / `SimpleJSON`。
- `QueryLayoutPhase` 已降级为 `Editor/CI` query layout hint，不再把 managed `EntityQueryDesc` 标记为 runtime-visible artifact。
- `StaticLookupPhase` 生成的 lookup 已带 `OwnsMemory` 契约，调用方可区分拥有型与借用型 `NativeArray` / `BlobAssetReference` 生命周期。
- `StaticLookupPhase` 的 Editor/Baking lookup builder 已按命名规范收敛为 `GASGeneratedDefinitionLookupBuilder`。
- `GenerateAllCode()` 已接入 `BeanUpdater.TryUpdateBeans()` 与 `CodeGenerator.TryGenerateGasConfigTables()` process gate；Bean / Luban 任一失败都会阻断 Core artifact 导出。
- `CodeGenerator.TryGenerateGasConfigTables()` 已直接执行 Luban CLI：`dotnet Tools/Luban/Luban.dll -t client -c cs-simple-json -d json --conf luban.conf`，输出 JSON 到 `Assets/DataGenerated/Luban/Json/GAS`，输出 C# 到 `Assets/DataGenerated/Luban/CSharp`。
- `Assets/DataGenerated/Luban/CSharp` 已恢复为 Unity 编译域内的 Luban C# 输出路径；`Assets/Plugins/LubanRuntime/Luban.Runtime.dll` 和 `SimpleJSON.dll` 作为显式 Unity 编译依赖存在。
- manifest orphan 清理已完成负例验证：旧 manifest 中不存在于当前 phase 输出的探针 `.gen.cs` 与 `.meta` 会被删除，并在 `GasCodeGenValidationReport.md` 输出 `OrphansDeleted`。
- Unity batchmode Core generation 已能输出 `Assets/GAS/Generated/CodeGen` 下的 runtime/editor asmdef、`DefinitionIndex.gen.cs`、`BlobSchemas.gen.cs`、`StaticLookups.gen.cs`、`DefinitionComponents.gen.cs`、`ComponentTypeSets.gen.cs`、Editor/Baking builder/glue、manifest 和 validation report。
- 生成后的 `com.exhard.exgas.generated.runtime` / `com.exhard.exgas.generated.editor` assembly 已通过 Unity 脚本编译；当前 report 中 `RuntimeForbiddenDependencyHits = 0`、`RuntimeGeneratedNamingDebtHits = 0`。
- generated runtime asmdef 已审查：只引用 `com.exhard.exgas.runtime`、`Unity.Collections`、`Unity.Entities`；实际 row source assembly 只出现在 generated editor asmdef。

剩余 Core 缺口：

- 最后一轮只保留集中验证与收口：重新运行 `GenerateAllCode()`，确认 Luban C# 仍在 Unity 编译域内通过，`GasCodeGenValidationReport.md` 中 `RuntimeForbiddenDependencyHits = 0`、`RuntimeGeneratedNamingDebtHits = 0`、`GeneratedNamingDebtHits = 0`。
- Player/AOT / Burst Inspector 证据仍是后续 CI artifact，不作为当前 Editor Core 编译完成条件。

暂停推进的范围：

- AutoChessScenario bootstrap、driver、summon projection、fact projection、presentation marker。
- `HeadlessAutoChessDefinitionSource` managed row adapter 的业务补全。
- AutoChess 专用 Attribute / Tag / Unit / MMC / Scenario phase 深化。
- 基于 AutoChess 的 runtime facts、settlement、scale profile、validation expectation。

## 下一轮目标

1. 重新运行完整 `GenerateAllCode()`，以 Unity batchmode 证明 `BeanUpdater -> Luban CLI -> GasCodeGenPipeline -> Unity compile` 主链闭环。
2. 保持集中静态审查：扫描 `Assets/GAS/Runtime` 和 generated runtime 目录，确认 forbidden dependency 与 generated naming debt 均为 0；扫描全 generated artifact，确认旧命名债为 0。
3. 若完整链路通过，当前 Luban / SourceGenerator Core 重构计划进入收尾态；后续再转入 GAS Runtime 消费 generated Blob / lookup，而不是继续补 AutoChessDemo 旧业务链路。

## 本轮验收点

- `GasCodeGenPipeline.RunAll()` 的默认 phase 数不再包含任何 `AutoChess*Phase`。
- `DefinitionIndex.gen.cs` 进入 manifest，标记为 `RuntimeVisible = true`。
- `RowMetadataFactory` 源码不再出现 `HeadlessAutoChess`。
- `GasCodeGenValidationReport.md` 不再输出 AutoChess 业务完成度，改为输出 `BAKE-*`、`BLOB-*`、`QRY-*`、`JOB-*`、`SC-*`、`BUR-*`、`PRF-*`、`ODF-*` 采用/拒绝/暂缓理由。
- `GasCodeGenValidationReport.md` 输出 `RuntimeGeneratedNamingDebtHits`，用于阻断 `Blob*Definition`、`GasGenerated*` 等旧命名回流。
- 新生成的 Blob 根类型形如 `AbilityDefinitionBlob` / `GameplayEffectDefinitionBlob`，不再使用 `BlobAbilityDefinition`。
- 新生成的通用框架类型使用 `GASGeneratedDefinitionIndex`、`GASGeneratedDefinitionBlobComponent<T>`、`GASDefinitionCodeComponent`、`GASGeneratedDefinitionBakePlan`。
- Editor/Baking lookup builder 使用 `GASGeneratedDefinitionLookupBuilder`，不得回流 `BlobDefinitionLookupBuilder`。
- Luban C# 输出保留在 `Assets/DataGenerated/Luban/CSharp` 并由 Unity 编译；GAS generated Runtime 不引用 `cfg.*` / `Luban.Runtime` / `SimpleJSON`。
- Runtime-visible generated 文件不得引入 `DefinitionRow`、row factory、`cfg.*`、`XLuban`、`SimpleJSON`、`JsonReader`。
- AutoChessDemo 当前改动只作为迁移期工作树状态保留，不纳入本轮完成度判断。
