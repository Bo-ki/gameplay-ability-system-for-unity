# CodeGen 到 Runtime 新链路重构计划

## 目标

以 `08-Luban-SourceGenerator配置生成链路Spec.md` 为准，直接用 `GasCodeGenPipeline + Context + RowMetadata + Phase + Manifest` 替换旧的分散生成器链路。当前阶段不维护旧链路兼容，不新增 wrapper，不让 Runtime Core 反查 Luban managed row、JSON、ScriptableObject 或 Editor registry。

## 主链路

1. `BeanUpdater` 只负责确保 Luban / bean 输入存在。
2. `GasCodeGenContext` 单次扫描 `*DefinitionRow`，生成统一 `RowMetadata`、输入 hash、输出根目录和命名空间。
3. `IGasCodeGenPhase` 只消费 context，不自行扫描程序集，不自行推断全局命名。
4. `GasCodeGenManifest` 记录输入 hash、输出文件、层级、runtime 可见性、版本控制归属，并清理 orphan artifact。
5. Runtime Core 只消费 generated id、Blob schema、`GASDefinitionCatalogBlob`、unmanaged / generated code->index lookup、Runtime definition glue、component type set、query layout 和 static switch。

## 2026-05-27 实现审查结论

本轮审查以 `08-Luban-SourceGenerator配置生成链路Spec.md`、`90-目标态不变量.md`、`13-EntityComponent物理布局Spec.md` 和 DOTS 规则索引为准，结论是当前计划必须从 AutoChess 业务闭环退回到通用 Luban / SourceGenerator 核心生成链路。

本文件中出现的 `HeadlessAutoChess` 只表示 2026-05-27 审查时的历史旧口径或禁止回流命名，不表示当前 AutoChessDemo 的业务域名称。当前 AutoChessDemo 的 unit / scenario / scale / validation 配置链应由 `11-AutoChessDemo-Luban配置方案Spec.md` 约束，并作为 Demo 专用链路接回通用 Core generated catalog，而不是重新污染 `GasCodeGenPipeline.RunAll()`。

> 2026-06-06 复审更新：本文件的“Core generated 主链收口”已被 `15-Luban-SourceGenerator链路复审与目标重划.md` 进一步收窄。`RuntimeDefinitionGluePhase` 不能再以“待补 generated runtime system”的方式推进；它只能输出 pure glue / unmanaged record。已经生成的 `RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs`、`RuntimeActiveEffect.gen.cs`、`RuntimeSystemRegistration.gen.cs` 属于职责越界或迁移期 proof，不是目标态完成物。

### 重定位前偏差

1. `GasCodeGenPipeline.RunAll()` 仍默认执行 `AutoChessAttributeComponentPhase`（目标态应迁为 AttributeSet family phase）、`AutoChessTagMaskPhase`、`AutoChessUnitConfigPhase`、`AutoChessMmcEvaluatorPhase`、`AutoChessScenarioBuildPlanPhase`。这违反 `08` 的通用 `Context -> RowMetadata -> Phase -> Manifest` 主链定位，也把 demo 旧架构混进了核心生成链。
2. `RowMetadataFactory` 仍在代码里硬编码 `HeadlessAutoChess` 前缀。根据 `08` 的 `GasCodeGenSettings` 要求，项目名前缀只能来自配置，不能存在于生成器实现。
3. `ValidationReportPhase` 的报告内容仍以 AutoChess runtime artifact 为中心，无法作为 Core CodeGen 的验收报告。
4. Runtime 侧已出现若干 proof-only 实现，例如主线程遍历、`ToEntityArray` 和直接 `EntityManager` 写入路径。根据 `QRY-01`、`JOB-01`、`PRF-05`、`SC-01`、`PRF-02`，这些不能扩展为目标态 hot path；当前轮只允许保留为迁移期证据，不继续围绕 AutoChess 补业务。
5. AutoChessDemo 中的 generated row adapter、scenario driver、runtime projection 仍混有旧结构。它们不是当前轮验收对象，继续补这些文件会稀释 Luban / SourceGenerator 主链收敛。
6. 按 `12-命名规范Spec.md` 审查，当前生成模板会产出 `BlobAbilityDefinition`、`GasGeneratedDefinitionIndex`、`GeneratedDefinitionBlobComponent<T>`、`DefinitionCodeComponent` 等命名，未遵循 `GAS` 框架前缀、`*DefinitionBlob` Blob 根类型后缀和 `[GAS前缀][职责][DOTS后缀]` 规则。

### 重新定位

当前两到三轮目标只验收 **通用 CodeGen Core**：

1. `GenerateAllCode()` 默认只运行 `BeanUpdater + CoreGasCodeGenPipeline`。
2. Core phase 固定为：`AssemblyDefinition`、`DefinitionIndex`、`BlobSchema`、`StaticLookup`、`RuntimeDefinitionGlue`、`BakerGlue`、`ComponentTypeSet`、`QueryLayout`、`ValidationReport`。
3. AutoChess 专用 phase 当前轮直接后置，不保留在 Core pipeline 中；后续若重启 Demo 生成链路，必须另起 Demo 专用入口且不得污染 `RunAll()`。
4. Core generated 输出根目录固定收敛到 `Assets/GAS/Generated/CodeGen`；AutoChessDemo 不再作为默认 glue 输出位置。
5. `RowMetadataFactory` 的项目名前缀剥离、生成 namespace、输出目录必须由 `GasCodeGenSettings` 或 `GASSettingAsset` 承载。
6. Runtime-visible generated artifact 不得出现 `DefinitionRow`、row factory、`IReadOnlyList<*DefinitionRow>`、`cfg.*`、`XLuban`、`SimpleJSON`、`JsonReader` 或 managed row snapshot。
7. Editor / Baking 侧允许 row-based builder，但必须处于 `#if UNITY_EDITOR`、Baker glue 或 Baking artifact 层，且 manifest 标记为非 runtime-visible。
8. Validation report 必须输出 DOTS 规则对照、manifest layer 表、runtime forbidden dependency scan、naming debt scan、Baker/Blob/lookup 边界，不输出 AutoChess 业务完成度。
9. 新增 generated Runtime Core 类型必须遵守 `12`：Blob 根类型使用 `{Domain}DefinitionBlob`，静态 lookup 使用 `{Domain}DefinitionLookup`，通用 generated component 使用 `GASGeneratedDefinitionBlobComponent<T>` 与 `GASDefinitionCodeComponent`，Runtime glue 使用 `GASGeneratedRuntimeDefinitionResolver` / `GASGeneratedRequirementEvaluator` / `GASGeneratedMagnitudeEvaluator` / `GASGeneratedTargetRuleTable`，框架产物统一使用 `GASGenerated*` 前缀。

## 破坏性替换切片

| 顺序 | 切片 | 验收 |
| --- | --- | --- |
| 1 | Core pipeline 默认化 | `生成所有` 只运行 core phases；AutoChess phase 从默认主链移出 |
| 2 | Output root / asmdef 收敛 | 默认输出到 `Assets/GAS/Generated/CodeGen`，并由 generated runtime/editor asmdef 分层；Runtime asmdef 不引用 row source，Editor asmdef 自动引用实际 row source assembly |
| 3 | Settings 去硬编码 | generated namespace、输出根目录、row prefix strip 来自配置；生成器代码中不出现项目名前缀 |
| 4 | Naming gate | Core generated names 满足 `12`：`GAS*` 框架前缀、`*DefinitionBlob`、`*Lookup`、`*Component`、`*BakePlan` |
| 5 | DefinitionIndex | 生成 row-free unmanaged metadata index，Runtime 可按 `GASDefinitionKind` 查询定义规模和 schema hash |
| 6 | Runtime / Baking 分层 | manifest 区分 Runtime、Baking、Editor/CI；runtime-visible 文件无 managed row / JSON / cfg 引用 |
| 7 | Blob / Static lookup | runtime 默认生成 `GASDefinitionCatalogBlob` + code->index lookup；迁移期 per-domain lookup 可持有 `NativeArray<int>` 与 `NativeArray<BlobAssetReference<T>>`，row-based builder 只在 Editor / Baking |
| 8 | Runtime definition glue | 生成 `AbilityDefinitionIndex -> AbilityActivationPlanRecord -> GECommandSeedRecord -> ResolvedModifierRecord` 的静态纯函数链；不生成 lifecycle system、不隐藏 ECB/query |
| 9 | Baker glue | 生成 stateless `Baker<TAuthoring>`，调用 `AddBlobAsset()`，只添加 component，不读取其他 Baker 输出 |
| 10 | Report gate | `GasCodeGenValidationReport.md` 输出 DOTS 规则、命名规则、layer、manifest、forbidden dependency、naming debt 和 orphan 清理证据 |

## 2026-06-06 复审后的当前推进状态

本轮状态必须按真实代码重新锚定，而不是沿用“接近收口”的旧口径。

已经具备且应保留的基础：

- 主入口 `GenerateAllCode()` 已接入 `BeanUpdater + GasCodeGenPipeline`，Luban process gate、manifest、validation report、generated asmdef 分层的方向仍然正确。
- `DefinitionCatalog.gen.cs` 已出现 `GASDefinitionCatalogBlob`、sorted code lookup、`ref readonly` definition 访问和 `GASGeneratedDefinitionCatalogLookup`。这符合 `BLOB-01`：静态定义进入 immutable Blob，Runtime 只读。
- `RuntimeDefinitionGlue.gen.cs` 中的 `GASGeneratedRuntimeDefinitionResolver`、Requirement / Magnitude evaluator、record glue 是目标态资产；它们把 definition index/range 转成 frame-local record，不需要 managed row / JSON / `Dictionary`。
- Luban C# 保留在 Unity 编译域、generated runtime 不反向依赖 `cfg.*` / `Luban.Runtime` / `SimpleJSON` 的边界仍应保留。这能让配置事实错误在真实编译链路暴露。

当前 P0 违约：

- `GasCodeGenPipeline.s_corePhases` 当前仍包含 `AutoChessDemoConfigPhase`。所以“`RunAll()` 已默认只执行 core phases；AutoChess 专用 phase 已移除”不成立。
- `RuntimeDefinitionGluePhase` 已实现，但实现过厚：它除了生成 pure glue，还生成 `RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs`、`RuntimeActiveEffect.gen.cs`、`RuntimeSystemRegistration.gen.cs`。
- 上述 generated lifecycle 文件包含 `: ISystem`、`OnUpdate(ref SystemState)`、`state.EntityManager`、`SystemAPI.GetComponentLookup`、`SystemAPI.GetBufferLookup`、`EntityCommandBuffer`、`world.CreateSystem()`、`AddSystemToUpdateList()`。对照 `SYS-01`、`SYS-03`、`QRY-01`、`QRY-04`、`SC-01`、`ECB-03`、`BUR-01`，它们只能作为迁移期 proof，不能作为目标态 Runtime Core。
- `GASGeneratedDefinitionCatalogBuilder.BuildCatalog()` 在 Runtime-visible generated 文件中调用 `BlobBuilder`。对照 `BLOB-02`，它必须归属 Baking / Bootstrap / initialization owner；Runtime Core hot path 只能只读 catalog。
- `RuntimeForbiddenDependencyHits = 0`、`RuntimeGeneratedNamingDebtHits = 0` 只证明没有 managed config 泄露和命名债，不能证明 generated lifecycle、hidden query、hidden ECB、random lookup、NativeContainer owner 合规。

剩余 Core 缺口：

- `GasCodeGenPipeline.RunAll()` 必须去 Demo 污染：默认 phase 不得包含 `AutoChessDemoConfigPhase` 或任何 Demo 专用 phase。
- `RuntimeDefinitionGluePhase` 必须收权：只允许生成 `GASGeneratedRuntimeDefinitionResolver`、`GASGeneratedRequirementEvaluator`、`GASGeneratedMagnitudeEvaluator`、`GASGeneratedTargetRuleTable` 这类 pure glue / unmanaged record。
- `RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs`、`RuntimeActiveEffect.gen.cs`、`RuntimeSystemRegistration.gen.cs` 必须删除、迁移到手写 Runtime Core System，或临时标记 `MigrationProofOnly` 并绑定移除任务。
- `BuildCatalog()` / `BlobBuilder` 必须迁入 Baking / Bootstrap / initialization owner，并明确 `BlobAssetReference` dispose owner。
- Validation report 必须新增 generated lifecycle / ownership gate：`GeneratedRuntimeLifecycleHits = 0`、`GeneratedRuntimeOwnershipHits = 0`、`GeneratedRuntimeRandomLookupHits = 0`、`GeneratedRuntimeNativeContainerOwnerHits = 0`、`GeneratedRuntimeStructuralChangeHits = 0`，或把命中 artifact 标为 `MigrationProofOnly` 并绑定移除任务。
- Player/AOT / Burst Inspector 证据仍是后续 CI artifact，不作为当前 Editor Core 编译完成条件；但 `BUR-01` 必须进入设计验收，不得等到性能测试阶段才补。

## 下一轮目标

1. 重新运行完整 `GenerateAllCode()`，以 Unity batchmode 证明 `BeanUpdater -> Luban CLI -> GasCodeGenPipeline -> Unity compile` 主链闭环。
2. 保持集中静态审查：扫描 `Assets/GAS/Runtime` 和 generated runtime 目录，确认 forbidden dependency、generated naming debt 和 generated lifecycle / ownership hits 均为 0；扫描全 generated artifact，确认旧命名债为 0。
3. 若完整链路通过，下一步转入手写 GAS Runtime System 消费 generated `GASDefinitionCatalogBlob` / lookup / pure Runtime definition glue，而不是继续补 AutoChessDemo 旧业务链路或 generated lifecycle system。

## 本轮验收点

- `GasCodeGenPipeline.RunAll()` 的默认 phase 数不再包含任何 `AutoChess*Phase`。
- `DefinitionIndex.gen.cs` 进入 manifest，标记为 `RuntimeVisible = true`。
- `RowMetadataFactory` 源码不再出现 `HeadlessAutoChess`。
- `GasCodeGenValidationReport.md` 不再输出 AutoChess 业务完成度，改为输出 `BAKE-*`、`BLOB-*`、`QRY-*`、`JOB-*`、`SC-*`、`BUR-*`、`PRF-*`、`ODF-*` 采用/拒绝/暂缓理由。
- `GasCodeGenValidationReport.md` 新增 `GeneratedRuntimeLifecycleHits`、`GeneratedRuntimeOwnershipHits`、`GeneratedRuntimeRandomLookupHits`、`GeneratedRuntimeNativeContainerOwnerHits`、`GeneratedRuntimeStructuralChangeHits`，用于阻断 generated lifecycle / hidden query / hidden ECB 回流。
- `GasCodeGenValidationReport.md` 输出 `RuntimeGeneratedNamingDebtHits`，用于阻断 `Blob*Definition`、`GasGenerated*` 等旧命名回流。
- 新生成的 Blob 根类型形如 `AbilityDefinitionBlob` / `GameplayEffectDefinitionBlob`，不再使用 `BlobAbilityDefinition`。
- 新生成的通用框架类型使用 `GASGeneratedDefinitionIndex`、`GASGeneratedDefinitionBlobComponent<T>`、`GASDefinitionCodeComponent`、`GASGeneratedDefinitionBakePlan`。
- Editor/Baking lookup builder 使用 `GASGeneratedDefinitionLookupBuilder`，不得回流 `BlobDefinitionLookupBuilder`。
- Luban C# 输出保留在 `Assets/DataGenerated/Luban/CSharp` 并由 Unity 编译；GAS generated Runtime 不引用 `cfg.*` / `Luban.Runtime` / `SimpleJSON`。
- Runtime-visible generated 文件不得引入 `DefinitionRow`、row factory、`cfg.*`、`XLuban`、`SimpleJSON`、`JsonReader`。
- AutoChessDemo 旧业务链路不纳入本轮提交范围；当前 Core 收口不得新增或补全 AutoChessDemo generated glue、scenario driver、runtime projection。
- Runtime 消费链新增验收：至少一条 Ability / GE 链路必须证明 Core 只读 Catalog Blob，且不在 activation / fan-in / magnitude hot path 反查 per-definition entity、managed row、JSON 或 `Dictionary`。
- Runtime glue 新增验收：至少一条 Ability / GE 链路必须证明 generated static glue 只输出 frame-local record，不拥有 NativeContainer、不调度 job、不执行结构变化。
