# ISSUE-008 目标态 Spec 尚未充分 Unity Entities 机制化

> 最近复核：2026-06-06 | 状态：Active | 严重度：P1

## 当前结论

当前代码已经吸收了一部分 Entities 机制：FixedStep group、ComponentSystemGroup、ECB System singleton、BlobAssetReference、`SystemAPI.QueryBuilder`、scheduled `IJob` / `IJobChunk`、EntitiesJournaling、以及不依赖 Unity Editor UI 的 sourcegen 驱动。但目标态仍需要继续把官方规则转成可执行的 query/job/baking/blob/structural evidence。

当前缺口不是“有没有引用官方术语”，而是官方机制还没有全部变成可验证的 runtime gate。文档、Contract 和模板只能定义约束；只有注册进当前 group 的 system、生成模板输出、Unity 编译域、Journaling/Profiler/Debugger evidence 和业务 runner hash 才能证明机制化完成。

## 官方文档到当前 Gate 的映射

| 官方文档 | 当前已落地 Gate | 仍缺的 Gate |
|---|---|---|
| `components-enableable-use.md` | chunk `EnabledMask` 已覆盖 ASC pending/destroying/dirty、ability commit/auto-end、ability lifecycle request、attribute owner marker、execution output applied marker；generated hot path gate 阻断旧 random enableable 回流 | static gate 还需要继续覆盖新的 generated active mutation store 模式，避免未来模板绕回 target random marker 写入 |
| `structural-changes-enableable-components.md` | `ASCDestroyingComponent` 已按 enableable bit 判定，不再用 `HasComponent` 误判 disabled destroying | 对所有销毁态/挂起态 marker 建立统一的 enabled-state 读写清单 |
| `iterating-data-ijobchunk.md` | 多条 owner applicator 已用 `IJobChunk` + `ChunkEntityEnumerator` / `EnabledMask` | 所有 `IJobChunk` 需要有 query ownership、enabled-mask 语义和 dependency 证据；不能只凭类型名认定合规 |
| `components-buffer-jobs.md` | `BufferLookup` 已把部分主线程 helper 迁到 job 内 | 需要把“随机访问工具”与“目标态 store”区分开，尤其是 active mutation、cleanup、fact bridge 的多 target 写入 |
| `systems-entity-command-buffer-use.md` | Begin/End GAS ECB gate 已存在，部分 generated/remove/cleanup 写入 ECB | 需要用 Journaling/Profiler 证明 playback phase、数量、来源和是否存在绕过 gate 的 direct structural write |
| `systems-entityquery-create.md` | stored query / `QueryBuilder` 已用于部分系统，`SystemAPI.Query` 热路径已收窄 | 需要把 boundary managed query、debug gather、low-frequency init 和 Core hot path query 分开计数和预算 |

## 已机制化部分

1. `FixedStepSimulationSystemGroup` 下 5 段 GAS group。
2. `Begin/EndGASStructuralCommitECBSystem` 作为 structural commit gate。
3. `GASDefinitionCatalogBlob` 作为 runtime catalog。
4. `SystemAPI.QueryBuilder()` 用于部分 query 预创建。
5. `GasRuntimeOfficialToolDiff` 使用 `EntitiesJournaling`。
6. `Tools/CodeGen/Generate-GAS-SourceGen.bat` / `Tools/GasCodeGenCli` 可离线驱动 Luban + GAS CodeGen，不再强制依赖 Unity Editor UI。
7. `GasCodeGenValidationReport.md` 已有 generated hot path static gate，当前 `GeneratedHotPathRegressionHits: 0`。

## 仍不足部分

1. `SystemAPI.Query` 主线程 foreach 在 `Assets/GAS` 当前只剩 Cue managed boundary；后续不足不是数量，而是必须防止它重新进入 Core hot path。
2. `Complete()` 已清零，并已进入 generated hot path gate；仍需作为 codegen/static validation 防回流项，而不是当前 runtime 事实。
3. 实际 runtime authoring/Baker / `BlobAssetStore` 还没有落到当前业务验收链。
4. NativeStream / deterministic merge 已在 OutputModifier 局部落地，但 singleton stream owner 与 generated active mutation serial job 的容量、ordering、budget 证据仍不足。
5. Contract-only 文件未和 runtime evidence 强绑定。
6. 离线 sourcegen 与 Unity batchmode 验证需要双轨保留：前者支持迭代速度，后者证明 Unity 编译域、asmdef import、`BeanUpdater` 和 `AssetDatabase` 边界。
7. generated hot path report 当前能证明旧回流模式为 0，但还不能证明所有 generated store 选型、capacity 和 deterministic ordering 已达标。
8. AutoChess runner 已提供业务级 hash/counter 证据，但仍需把 `syncQueryBudget`、`dependencyWaitRisks`、`requiredStructuralPlaybacks`、`recordedStructuralPlaybacks` 这类 evidence gap 转成固定验收门禁。

## 退出条件

1. 每条 DOTS 规则都能映射到当前代码检查项。
2. Contract 文件只作为约束，完成度由 runtime registration、Profiler、Journaling、battle hash 证明。
3. Baking/Blob/Query/Job/Structural 规则有独立 evidence gate。
4. SourceGenerator 输出的 `.gen.cs` 只作为生成产物受审；任何修复必须落回模板、manifest、report 或离线/Unity batchmode 生成链路。
5. Core、Boundary、Demo、Observation 四类成本在报告和 gate 中分开，不再混用一个“通过/失败”结论。
