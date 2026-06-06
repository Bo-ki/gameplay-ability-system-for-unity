# ISSUE-001 GE 生命周期管线过重

> 最近复核：2026-06-06 | 状态：Active | 严重度：P0

## 当前结论

旧问题没有完全消失，但问题形态已经变化。当前 Runtime 不再只是旧 request entity lifecycle；代码已经形成 `GEEffectCommandBuffer -> GEEffectSpecBuffer -> AttributeModifierBuffer -> GameplayEventBuffer` 的迁移链，并且 generated active effect systems 已进入主链。

剩余 P0 是：legacy fallback、active effect store、generated mutation/remove、singleton stream owner 与 ExecutionCalculation/Attribute/Fact projection 之间仍未形成完整 deterministic、store-driven lifecycle。注意：这里的 generated mutation 风险不是旧主线程 `EntityManager` helper，而是 singleton serial `IJob`、Buffer/ComponentLookup random access 和 store/ordering 证据不足。

## 官方文档推导

本轮按当前项目包内 `com.unity.entities@e90944159b94/Documentation~` 复核，GE lifecycle 的风险不应再用旧“是否用了 EntityManager helper”粗粒度判断，而应按 data owner、query owner 和写入方式拆开：

| 官方文档约束 | 对当前 GE lifecycle 的诊断 |
|---|---|
| `components-enableable-use.md`：高频 enableable 写入在同 chunk 内优先使用 `EnabledMask`；random `ComponentLookup.SetComponentEnabled` 可用但有额外 lookup 成本和竞态风险 | ability commit、ability lifecycle、attribute owner marker、execution output applied marker 已收口到 owner chunk applicator；这些不再是当前 GE lifecycle P0 事实 |
| `components-buffer-jobs.md`：`BufferLookup` 是 job 内随机访问 buffer 的工具，不等同于 scale-ready store | generated active mutation 虽已 scheduled，但仍围绕 singleton stream 和 target `BufferLookup` 做大量随机 store 写入，必须继续作为 lifecycle store 风险 |
| `systems-entityquery-create.md`：query 要由 system 拥有并明确 enabled-state 语义；同步 gather 只能作为有意选择 | 当前风险不是 `SystemAPI.Query` 数量，而是 active mutation / stream / fact projection 的 query 和 carrier 仍缺少容量、排序、依赖证据 |
| `systems-entity-command-buffer-use.md`：job 内结构变化应记录到 ECB，集中 playback 降低 sync point | generated active mutation/remove 和 cleanup 已走 ECB gate 的方向正确，但 `GasRuntimeOfficialToolDiff` / Profiler 还没有证明所有结构变化来源与 phase |

因此，本 issue 的当前核心不是“GE 管线还没 job 化”，而是：GE lifecycle 已进入 job 化迁移期，但数据承载仍浅，singleton stream 同时承担 command/spec/delta/fact/mutation，多处 target random lookup 没有 target-grouped merge 或 owner-local store 证明。

## 已缓解部分

1. generated `GEEffectSpecBuildSystem` 已从 command 构建 spec。
2. generated `GASAttributeSetReduceApplySystem` 已从 spec 写 attribute delta。
3. `GameplayFactProjectionSystem` 已能把 delta 投影成 typed facts。
4. generated `GASActiveEffectMutationApplySystem` / `PreTick` / `Remove` 已接入 CoreSimulation。
5. `GASDefinitionCatalogBlob` 已进入 runtime catalog 查找链。

## 仍成立风险

1. `GEExecutionCalculationSystem` 与 `GEExecutionCalculationOutputModifierSystem` 已切到 scheduled job 路径；当前风险转为 singleton stream / serial merge 的容量、ordering 和 profile 证据。
2. generated active effect pre-tick 已从 scan + `Complete()` 迁出；generated mutation/remove 已迁入 scheduled job，当前风险转为 mutation singleton serial job、lookup random access 和 store capacity/ordering 证据。
3. command/spec/delta/fact 仍集中在 singleton stream owner DynamicBuffer。
4. period/overflow/granted cleanup 等复杂 active lifecycle 需要继续核对是否完全脱离 legacy runtime GE entity。
5. `ASCCommandGateway` 的 transient request entity 链路已退场；剩余风险是 bridge/helper 继续绕过 owner-local command sink 或直接触碰 `EntityManager`。
6. `GameplayEventBuffer` typed fact 已替代 legacy `GameplayEventBusEventBuffer`；当前事实源分裂风险转移为剩余 Attribute/Cue/Tag/Damage 边界缓冲是否继续绕开 typed fact。

## 代码证据

| 事实 | 文件 |
|---|---|
| generated runtime 注册 | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeSystemRegistration.gen.cs` |
| spec build / delta apply | `RuntimeEffectInstant.gen.cs` |
| active effect mutation/tick/remove | `RuntimeActiveEffect.gen.cs` |
| handwritten execution calculation | `GEExecutionCalculationSystem.cs`, `GEExecutionCalculationOutputModifierSystem.cs` |
| command/spec/fact stream | `GEEffectCommandSpecStream.cs`, `GEEffectCommandSpecStreamPhases.cs` |

## 退出条件

1. simple instant、duration、period、overflow、granted cleanup 都有明确 command/store 路径。
2. active effect tick/remove 不再依赖无解释的主线程 `Complete()`；mutation 的 singleton serial job 也迁入可解释的 store chain 或 deterministic merge 路径。
3. singleton stream owner 有容量、ordering、merge 证据，或已迁出到更合适 carrier。
4. legacy request fallback 被限制到低频边界，并有调用点清单。
5. active mutation 的 `BufferLookup` / `ComponentLookup` 写入被 owner-local store、target-grouped merge 或可量化 profile 证明覆盖。
6. gameplay event 已走 typed fact，Core gameplay reaction 不再依赖 observation bus；剩余边界缓冲必须继续保持只读/投影职责。
