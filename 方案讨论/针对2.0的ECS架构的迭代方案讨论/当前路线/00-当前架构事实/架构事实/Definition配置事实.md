# Definition / 配置事实

> 上次更新：2026-05-26 | 基于代码审查 + 01-目标态架构共识 Spec 对照

## 已成立事实

1. `GASDefinitionTable` 已统一 Ability、GameplayEffect、AttributeSet、Attribute、GameplayTag、GameplayCue summary / lookup contract。
2. `GASDefinitionGeneratedAdapter` 已把 generated/Luban source 映射到 unified definition table。
3. `GameplayEffectConfigRegistry` 是 GE definition cache lifecycle owner（BlobAsset + prototype entity 管理）。
   - 通过 `TryGetOrCreateStaticDefinitionBlob` 提供 GE 静态定义查询。
   - 通过 `CreateRuntimeEffectInstance` 提供 prototype → instantiate 或 config → 直接创建的双路径。
   - BlobAsset 引用通过静态 `Dictionary` 管理生命周期（存在 `BLOB-02` 合规缺陷）。
4. `GASGeneratedDefinitionBakingPlan / BakeContract / BakePipeline / RuntimeIntegrationPlan` 已形成完整的 generated → bake → runtime integration contract 链：
   - **BakingPlan**：从 `GASGeneratedDefinitionBuildResult` + `GameplayEffectDefinitionCacheState` 创建，包含 6 类 Definition 的 entry（Ability/GE/AttributeSet/Attribute/Tag/Cue），每 entry 标记 capability（GeneratedCarrier/BakerInput/StaticDefinitionBlob）和 deferred boundary。
   - **BakeContract**：从 BakingPlan 创建，包含 `BakeWrite[]`（5 种 WriteKind）和 `ArchetypeTemplate[]`（6 种 TemplateKind），标记每个 write 的 phase/target/eligibility。
   - **BakePipeline**：从 BakeContract 创建 `BakeResult`，将 writes 物化为 5 类 artifact（Carrier/BakerInput/StaticBlobCacheRequest/RuntimeArchetype/DeferredBoundary），并提供 `WarmupStaticDefinitionBlobCache` 预热入口。
   - **RuntimeIntegrationPlan**：从 BakeResult + LayoutPlan + StructuralPlan 创建，将 artifacts 映射到 4 种 integration target（UnityBakerInput/StaticDefinitionBlobCache/RuntimeStaticArchetype/DeferredBoundary），解析 layout/structural boundary 和 dirty pipeline signal。
5. AutoChess 已有 generated package、SourceGenerator output/export/toolchain/authoring snapshot 和真实 Luban process gate 样板。

## 当前实现文件

| 文件 | 职责 |
|------|------|
| `GASDefinitionTable.cs` (935行) | 统一 Definition 查询入口，6 种 Summary 类型 |
| `GASDefinitionGeneratedAdapter.cs` (372行) | Generated → Definition Table 映射 + BuildResult |
| `GASGeneratedDefinitionBakeContract.cs` (442行) | Bake 契约：5 种 WriteKind、6 种 TemplateKind、9 种 ArchetypeSlot |
| `GASGeneratedDefinitionBakingPlan.cs` (361行) | Bake 计划：6 类 Definition entry + capability/boundary 标记 |
| `GASGeneratedDefinitionBakePipeline.cs` (391行) | Bake 流水线：5 类 artifact materialization + BlobCache warmup |
| `GASGeneratedDefinitionRuntimeIntegrationPlan.cs` (527行) | Runtime 集成计划：4 种 target + 12 种 IntegrationBoundary + layout/structural 映射 |
| `GASGeneratedDefinitionBakingSystem.cs` (207行) | DOTS Baking System 实现（`Assets/GAS/Runtime/DotsBaking/`），SystemBase，单 ECB + 单次 Playback，8 种定义类型 |
| `GameplayEffectConfigRegistry.cs` (934行) | GE BlobAsset cache + prototype entity cache + 诊断报告 |
| `AbilityConfigRegistry.cs` (28行) | Ability config registry（委托注入模式） |
| `TimelineAbilityConfigRegistry.cs` | Timeline ability config registry |
| `GameplayCueConfigRegistry.cs` | GameplayCue config registry |
| `ConfigRegistryDiagnostics` (in `GameplayEffectConfigRegistry.cs`) | 配置引用图诊断：missing config 检测、去重、严重度策略、图验证器 |
| `Ability/AbilityConfig.cs` | Ability config (BlobAsset) |
| `Effect/GameplayEffectConfig.cs` | GE config |

## Config Registry 架构

4 个 Registry（Ability / GameplayEffect / TimelineAbility / GameplayCue）均遵循统一模式：
- `static Func<int, T> _getConfigByID` 委托字段（依赖注入）
- `RegisterGetConfigByIDFunc(Func<int, T>)` 注册入口
- `GetConfigByID(int, ConfigRegistryReferenceContext)` 查询 + 缺失诊断

`ConfigRegistryGraphValidator` 提供完整的跨表引用图验证：
- 验证 Ability → Cost/Cooldown/ActivationEffect GE 引用、Timeline 引用
- 验证 GameplayEffect → Period/Overflow/GrantedAbility 引用
- 验证 Timeline → ApplyEffect/CuePreset 引用
- 去重诊断 + 可配置严重度策略

## AutoChess Generated 链路

| 组件 | 定位 |
|------|------|
| Generated C# source | `Assets/AutoChessDemo/Config/Generated/HeadlessAutoChessGeneratedDefinitionRows.cs` |
| Definition source | `Assets/AutoChessDemo/Config/HeadlessAutoChessDefinitionSource.cs` (583行) |
| Luban 配置源 | `EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/` (8 个 xlsx) |
| Luban Defines | `EX_GAS_Config/ProjectConfigTable/exgas_config/Defines/builtin.xml` |
| Luban 入口 | `EX_GAS_Config/ProjectConfigTable/exgas_config/gen.bat` / `gen.sh` / `luban.conf` |

### AutoChess 当前数据表

| Excel 文件 | 对应 Row 类型 |
|-----------|-------------|
| `#exgas.ability.xlsx` | `HeadlessAutoChessAbilityDefinitionRow` |
| `#exgas.gameplayEffect.xlsx` | `HeadlessAutoChessGameplayEffectDefinitionRow` |
| `#exgas.attributeSet.xlsx` | `HeadlessAutoChessAttributeSetDefinitionRow` |
| `#exgas.attribute.xlsx` | `HeadlessAutoChessAttributeDefinitionRow` |
| `#exgas.gameplayTags.xlsx` | `HeadlessAutoChessGameplayTagDefinitionRow` |
| `#exgas.gameplayCue.xlsx` | `HeadlessAutoChessGameplayCueDefinitionRow` |
| `#exgas.timelineAbility.xlsx` | `HeadlessAutoChessTimelineDefinitionRow` |
| `#exgas.asc.xlsx` | ASC 配置 |

### AutoChess DefinitionSource 能力

`HeadlessAutoChessDefinitionSource` (583行) 提供：
- `CreateGeneratedDefinitionPackage()` → `HeadlessAutoChessGeneratedDefinitionPackage`
- `CreateGeneratedRegistrySnapshot()` → 8 种 Row 的聚合快照
- `CreateGeneratedSource()` → `GASGeneratedDefinitionSource`
- `RegisterRuntimeProviders()` → 注入 4 个 Registry 的 `GetConfigByID` 委托
- `CreateCombatAttributeSet()` → 程序化创建战斗属性集
- `WarmupGameplayEffectPrototypes()` → 预热所有 GE prototype entity
- 内联的 Config 类（`HeadlessAutoChessShieldDamageExecutionConfig` 等 7 个内部类）

### DOTS Baking System

`GASGeneratedDefinitionBakingSystem` (207行, `SystemBase`)：
- 冷启动一次性执行，完成后 `Enabled = false`
- 处理 8 种定义类型：Ability / GameplayEffect / AttributeSet / Attribute / GameplayTag / GameplayCue / Timeline / Summon
- 每种类型：构建 `Blob*DefinitionLookup` + `BlobDefinitionBuilder.Build*` + `GasGeneratedBakers.Bake*`
- 单 ECB + 单次 Playback（正面模式）
- 使用 `Allocator.Persistent` 分配 BlobAsset

## 与 01-目标态架构共识 Spec 对照

### 已对齐

| Spec 要求 | 当前实现 | 状态 |
|----------|---------|------|
| 数据流: Excel→Luban→Package→Adapter→Table→BakePlan→BakeContract→BakePipeline→Integration (08-Spec §数据流图) | 完整链路已实现 | 对齐 |
| Generated adapter 不持有 Entity/BlobAssetReference/Editor/spec/context/runtime state (08-Spec §核心契约) | `GASDefinitionGeneratedAdapter` 仅做映射，不持有上述类型 | 对齐 |
| Bake plan/contract/pipeline 不生成 gameplay lifecycle (08-Spec §核心契约) | 所有 Bake 层 struct 仅描述 artifact 和 boundary | 对齐 |
| Runtime integration plan 只描述落点和 deferred boundary (08-Spec §核心契约) | `RuntimeIntegrationPlan` 描述 target/status/boundary，不执行 lifecycle | 对齐 |
| Luban 和 SourceGenerator 只生成 Definition & Generation Layer 输入和 static lookup glue (08-Spec §核心契约) | `HeadlessAutoChessGeneratedDefinitionRows` 提供 Row → 代码常量/lookup | 对齐 |
| 真实 Luban process gate (08-Spec §验收) | `gen.bat` / `gen.sh` / `luban.conf` 已配置 | 对齐 |
| Config registry 委托注入模式 | 4 个 Registry 均以 `Func<int, T>` 委托实现 | 对齐 |
| CASE-39/40/41 意图 (Baker 无状态/只添加/单 ECB) | BakingSystem doc comment 声明合规意图 | 部分对齐 |

### 缺口（Spec vs 实现）

| Spec 要求 | 当前状态 | 严重度 |
|----------|---------|--------|
| **生成产物扩展 (14 种 artifact)** (08-Spec §DOTS深读后的生成产物扩展) | 当前 BakeResult 仅 5 种 artifact；缺失：Query layout hint、Calculation registry、Baking world/phase report、Content reference plan、Entity prefab plan、Scene/Scale plan、Physics profile plan、Render binding plan、Burst AOT evidence plan、Official DOTS coverage report、Validation graph | P1 |
| **AutoChess 子目录表结构** (11-Spec §目标目录) | Spec 定义 `Datas/AutoChessDemo/` 下 12 张表；当前 8 张表直接放在 `Datas/` 下，无 `AutoChessDemo/` 子目录 | P2 |
| **缺失配置表** (11-Spec §配置表职责) | 缺失：`autochess.unit.xlsx`、`autochess.physics_profile.xlsx`、`autochess.render_profile.xlsx`、`autochess.scenario.xlsx`、`autochess.scale_profile.xlsx`、`autochess.validation_expectation.xlsx` | P1 |
| **生成物拆分** (11-Spec §生成物职责) | Spec 定义 6 个独立 `.g.cs` 文件；当前为单体 `HeadlessAutoChessGeneratedDefinitionRows.cs` | P2 |
| **ScaleProfile 系统** (11-Spec §ScaleProfile设计) | 完整的 x1/x50/x100/x1000/x10w/x100w profile 系统（33 个字段）未实现 | P1 |
| **DiagnosticsThreshold 系统** (11-Spec §DiagnosticsThreshold设计) | GoalStopEligible + tier 自动验证阈值系统未实现 | P1 |
| **ValidationExpectation 系统** (11-Spec §ValidationExpectation设计) | 自动验收 expectation（RequiredFactKinds, RequiredCueMarkers, SummaryHashPolicy）未实现 | P1 |
| **Physics profile / Render profile** (08-Spec §验收10) | 两个 profile plan 均未生成；无 `PhysicsCollider`/`CollisionFilter`/`RenderMeshArray` 相关生成物 | P1 |
| **ODF-* 官方文档覆盖报告** (08-Spec §验收8) | 未输出任何 ODF 覆盖检查结果 | P2 |
| **生成报告** (08-Spec §验收6-10) | 未输出 query layout hint、buffer capacity hint、TransformUsageFlags、WeakObjectReference load plan、Baking dependency summary | P1 |

### 架构层面缺口

| 缺口 | 详情 |
|------|------|
| **AutoChess 表结构 ≠ Spec** | Spec 11 定义了 12 张业务表（含 unit/ability/GE/cue/physics_profile/render_profile/scenario/scale_profile/validation_expectation），当前仅 8 张基础表。缺失的 4 张表（unit/scenario/scale_profile/validation_expectation）是验收链路的必要输入。Physics/Render profile 表是 DOTS 官方机制对齐的必要证据。 |
| **生成物未拆分** | Spec 11 要求 6 个独立 `.g.cs` 文件按职责拆分（Ids/Lookups/BlobBuilders/ScenarioBuildPlan/ValidationExpectations/ConfigDiagnostics），当前全在单一文件中。这影响编译依赖最小化和 Editor/CI 诊断独立分发。 |
| **ScaleProfile → DiagnosticsThreshold → ValidationExpectation 三层验收体系缺失** | 这是 Spec 11 的核心自动验收链路，当前完全未实现。AutoChess 目前无法通过配置驱动性能验收。 |
| **Summon 类型的 Baking 支持超出 Spec 范围** | `GASGeneratedDefinitionBakingSystem` 包含 `BakeSummons`（第 8 种类型），但 Spec 08/11 均未定义 Summon 表或 Summon Row 类型。这是实现超前于 Spec 的情况。 |

## 合规缺陷

| 缺陷 | 编号 | 详情 |
|------|------|------|
| Definition 层 6 个 DTO struct 含托管数组 | `U` (P1) | 违反 `BAKE-01` |
| BlobAsset 通过静态 Dictionary 管理生命周期 | `V` (P1) | 违反 `BLOB-02`，`GameplayEffectConfigRegistry` 中 `StaticDefinitionBlobByCode` 和 `PrototypeByCode` 均为 `static Dictionary` |
| 代码库中零 `Baker<T>` 实现 | `W` (P1) | 违反 `CASE-39/40/41`——缺少 Baker 使用案例。现有 `GASGeneratedDefinitionBakingSystem` 是 SystemBase 而非 Baker |
| BakePlan/BakeContract/BakePipeline/RuntimeIntegrationPlan 所有 struct 内部均使用托管 `T[]` 数组 | **新增** (P1) | 同 `BAKE-01` 违规模式——`_entries`、`_writes`、`_carriers` 等字段均为 managed array |
| `ConfigRegistryDiagnostics` 使用全局可变静态状态 | **新增** (P2) | `static List<ConfigRegistryDiagnostic>` + `static HashSet<string>` — 非线程安全，非 Burst 友好 |
| `GASDefinitionTable` 查询为 O(n) 线性搜索 + managed array | **新增** (P1) | 6 种 Summary 均为 `IReadOnlyList<T>` 遍历查找，无 blob lookup 或二分搜索 |
| `GASGeneratedDefinitionBakingSystem` 使用 `SystemBase` 而非 `ISystem` | **新增** (P2) | Baking system 使用 class-based SystemBase 可接受（需持有 managed Snapshot property），但与 repo 整体 ISystem 迁移方向不一致 |
| AutoChess 配置目录结构与 Spec 11 不一致 | **新增** (P2) | 表文件缺少 `AutoChessDemo/` 子目录层级 |
| 6 个 Spec 11 要求的配置表缺失 | **新增** (P1) | unit / physics_profile / render_profile / scenario / scale_profile / validation_expectation |

## 边界

1. Definition summary 不携带 spec/context/runtime state。
2. Generated pipeline 不生成 Ability / GE lifecycle。
3. 自动生成目录不纳入版本控制。
4. Bake contract chain 的所有 struct 为 readonly struct，不持有 Entity/World/System引用。
5. Config registry 委托注入模式将 Luban 数据源与 Runtime Core 解耦——Registry 不依赖 Luban 程序集。
6. `ConfigRegistryGraphValidator` 的验证逻辑仅在 warmup/诊断阶段执行，不进入 hot path。
7. AutoChess DefinitionSource 内部的 7 个 Config 子类是 Demo 专用胶水代码，不属于 Runtime Core 通用契约。
