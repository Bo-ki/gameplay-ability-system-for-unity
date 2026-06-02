# Definition / 配置事实

> 上次更新：2026-06-02 | 事实源：`Assets/GAS/Runtime/Definition` + `Assets/GAS/Generated/CodeGen/Runtime` + 当前 `Assets/AutoChessDemo`

本文件只记录当前代码事实。旧文档中引用的 `Assets/AutoChessDemo/Config/Generated/HeadlessAutoChessGeneratedDefinitionRows.cs`、`HeadlessAutoChessDefinitionSource.cs`、`Assets/GAS/Runtime/DotsBaking/GASGeneratedDefinitionBakingSystem.cs` 当前不在代码树内，不能继续作为当前事实。

## 当前 Definition 链路

```mermaid
flowchart LR
    Source["managed config / generated source / demo builder"] --> Table["GASDefinitionTable\nmanaged summary table"]
    Source --> CatalogBuilder["GASDefinitionCatalogBlob builder"]
    CatalogBuilder --> Catalog["GASDefinitionCatalogComponent\nBlobAssetReference"]
    Catalog --> GeneratedRuntime["Generated runtime systems"]
    GeneratedRuntime --> CommandSpecDelta["Ability command / GE command / spec / delta / active effect"]
```

当前实际上并存两套层次：

1. **Managed summary / diagnostics 层**：`GASDefinitionTable`、`GASDefinitionGeneratedAdapter`、ConfigRegistry、BakePlan/Contract/Pipeline/IntegrationPlan。
2. **Runtime catalog blob 层**：`GASDefinitionCatalogBlob`、generated lookup/glue、generated runtime systems。

第一层适合初始化、诊断、过渡期生成链规划；第二层才是当前 generated runtime hot path 读取的事实源。

## 已成立事实

1. `GASDefinitionCatalogRuntimeTypes.cs` 定义 runtime catalog singleton：
   - `GASDefinitionCatalogComponent`
   - `GASDefinitionCatalogBlob`
   - ability / GE / modifier / requirement / tag mask / granted ability blob record
2. generated runtime systems 已通过 `state.RequireForUpdate<GASDefinitionCatalogComponent>()` 依赖 catalog：
   - `AbilityCatalogCommitSystem`
   - `GEEffectCommandCatalogNormalizeSystem`
   - `GEEffectSpecBuildSystem`
   - `GASAttributeSetReduceApplySystem`
   - `GASActiveEffectMutationApplySystem`
   - `GASActiveEffectPreTickSystem`
   - `GASActiveEffectRemoveSystem`
3. `DefinitionCatalog.gen.cs` 提供 generated catalog lookup：`TryGetAbilityIndex()`、`TryGetGameplayEffectIndex()`、`GetAbility()`、`GetGameplayEffect()`。
4. `RuntimeDefinitionGlue.gen.cs` 提供 runtime 对 catalog 的 ability / GE / requirement / modifier glue。
5. `GASDefinitionGeneratedAdapter` 仍存在，用 managed source 构建 `GASDefinitionTable` 并生成 diagnostics。
6. `GASDefinitionTable` 仍是 managed summary table，内部 summary / lookup 不是 blob/chunk/job 友好结构。
7. `GameplayEffectConfigRegistry`、`AbilityConfigRegistry`、`CueConfig` 等 registry 仍是迁移期 managed provider 入口。
8. `GameplayEffectConfigRegistry` 仍维护 prototype entity cache 和 `GEStaticDefinitionBlob` cache，适合初始化/prototype 路径，不应进入 hot path。
9. 当前 AutoChessDemo 没有依赖旧 Headless generated rows，而是由 `AutoChessBattleDefinitionCatalogBuilder` 在代码中安装最小 `GASDefinitionCatalogBlob`。

## 当前实现文件

| 文件 | 当前职责 | 风险/备注 |
|---|---|---|
| `Assets/GAS/Runtime/Definition/GASDefinitionCatalogRuntimeTypes.cs` | runtime catalog blob 类型 | generated runtime hot path 读取入口 |
| `Assets/GAS/Generated/CodeGen/Runtime/DefinitionCatalog.gen.cs` | generated blob lookup | 当前 lookup 需要纳入生成代码审查 |
| `Assets/GAS/Generated/CodeGen/Runtime/RuntimeDefinitionGlue.gen.cs` | generated runtime glue | command/spec/active effect 依赖 |
| `Assets/GAS/Runtime/Definition/GASDefinitionTable.cs` | managed definition summary / lookup | O(n) / managed array / diagnostics 层 |
| `Assets/GAS/Runtime/Definition/GASDefinitionGeneratedAdapter.cs` | generated source -> table + diagnostics | 初始化/验证层，不是 hot path |
| `Assets/GAS/Runtime/Definition/GASGeneratedDefinitionBakingPlan.cs` | baking plan contract | contract-only，当前不等于实际 Unity Baker |
| `Assets/GAS/Runtime/Definition/GASGeneratedDefinitionBakeContract.cs` | bake writes/archetype contract | contract-only |
| `Assets/GAS/Runtime/Definition/GASGeneratedDefinitionBakePipeline.cs` | artifact materialization plan | contract-only / warmup plan |
| `Assets/GAS/Runtime/Definition/GASGeneratedDefinitionRuntimeIntegrationPlan.cs` | runtime integration plan | contract-only |
| `Assets/GAS/Runtime/Effect/GameplayEffectConfigRegistry.cs` | GE config registry、prototype、static blob cache、diagnostics | managed cache，生命周期需治理 |
| `Assets/AutoChessDemo/Battle/Ecs/AutoChessBattleDefinitionCatalogBuilder.cs` | demo 最小 catalog 安装器 | 低频初始化路径，含 `ToEntityArray` singleton 查找 |

## 已失效的旧事实

| 旧口径 | 当前状态 |
|---|---|
| `HeadlessAutoChessGeneratedDefinitionRows.cs` 是当前 AutoChess generated source | 文件当前不存在 |
| `HeadlessAutoChessDefinitionSource.cs` 是当前 AutoChess Definition source | 文件当前不存在 |
| `GASGeneratedDefinitionBakingSystem` 位于 `Assets/GAS/Runtime/DotsBaking` | 当前 Runtime/DotsBaking 下没有该 `.cs` 实现 |
| 代码库中已有实际 `Baker<T>` runtime/baking 实现 | 当前 `Baker<T>` 只出现在 Editor CodeGen 模板字符串里，没有生成到当前 Runtime/AutoChess 代码树 |
| AutoChess 当前由 Luban generated rows 驱动 | 当前 demo 由代码内最小 catalog builder 驱动 |

## 当前缺口

### DEF-01：Managed summary table 与 runtime catalog blob 并存

`GASDefinitionTable` / `GASDefinitionGeneratedAdapter` 仍是有价值的诊断/过渡层，但它不是 generated runtime 的 hot path 数据结构。文档和任务拆分必须区分：

- 初始化/诊断事实：managed table、registry、diagnostics、plan。
- Runtime 执行事实：`GASDefinitionCatalogBlob` + generated lookup/glue。

### DEF-02：Baking contract 不是实际 Baker 集成

BakePlan / BakeContract / BakePipeline / RuntimeIntegrationPlan 目前是 contract/plan 代码。它们不能证明已经符合 Unity Entities Baker 机制。当前实际 `Baker<T>` 类未生成到代码树，`BlobAssetStore` 生命周期治理也未落地。

### DEF-03：ConfigRegistry 仍是全局 managed 状态

`ConfigRegistryDiagnostics` 使用静态 `List` / `HashSet`，`GameplayEffectConfigRegistry` 使用静态 `Dictionary<int, Entity>` 和 `Dictionary<int, BlobAssetReference<GEStaticDefinitionBlob>>`。这可以作为初始化/Editor/诊断路径，但不适合 Burst/job/runtime scale path。

### DEF-04：AutoChess catalog 是最小手写样本

`AutoChessBattleDefinitionCatalogBuilder` 能让 demo 跑通，但它没有证明 Spec 11 的配置表、SourceGenerator、Baker、ValidationExpectation、ScaleProfile 链路已经落地。

### DEF-05：generated runtime 必须纳入同等审查

generated runtime 已读取 catalog 并修改 runtime state，因此生成代码要接受与 handwritten Runtime 一样的 DOTS 检查：

1. 主线程 `SystemAPI.Query` 是否可接受。
2. buffer for loop 是否有规模上限和 capacity 证据。
3. `state.Dependency.Complete()` 是否可以拆成 job chain。
4. blob lookup 是否 deterministic 且 revision/lifecycle 明确。

## 与目标态的当前差距

| 目标项 | 当前事实 | 严重度 |
|---|---|---|
| Unity `Baker<T>` 实际接入 | 当前只有 Editor CodeGen 模板字符串，没有落地生成物 | P1 |
| BlobAsset 生命周期 owner | runtime/prototype 仍有静态 Dictionary 管理 | P1 |
| Generated catalog 完整配置源 | AutoChess 当前是手写最小 catalog，不是完整 Luban/SourceGenerator 链 | P1 |
| ScaleProfile / ValidationExpectation | 当前没有配置驱动验收链 | P1 |
| Physics/Render profile | 当前无资源表现链和 profile plan | P2 |
| Managed summary table hot path 隔离 | 文档需明确 `GASDefinitionTable` 不是 runtime hot path owner | P1 |

## 当前结论

Definition 层相比旧代码已经有重要进展：`GASDefinitionCatalogBlob` 和 generated runtime glue 已经进入真实执行链，Runtime 不再只能依赖 managed config/prototype 路径。

但当前不能宣称 Definition / Baking / SourceGenerator 目标态已完成。实际状态是：

1. Runtime hot path 使用 catalog blob。
2. Managed table/registry/plan 仍是初始化、诊断、迁移层。
3. AutoChess 只安装最小手写 catalog。
4. 实际 Baker / BlobAssetStore / 配置驱动验收链尚未闭合。
