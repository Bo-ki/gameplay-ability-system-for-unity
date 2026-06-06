# ISSUE-001 GE 生命周期管线过重

> 最近复核：2026-06-06 | 状态：Active | 严重度：P0

## 当前结论

旧问题没有完全消失，但问题形态已经变化。当前 Runtime 不再只是旧 request entity lifecycle；代码已经形成 `GEEffectCommandBuffer -> GEEffectSpecBuffer -> AttributeModifierBuffer -> GameplayEventBuffer` 的迁移链，并且 generated active effect systems 已进入主链。

剩余 P0 是：legacy fallback、active effect store、generated mutation/remove、singleton stream owner 与 ExecutionCalculation/Attribute/Fact projection 之间仍未形成完整 deterministic、store-driven lifecycle。

## 已缓解部分

1. generated `GEEffectSpecBuildSystem` 已从 command 构建 spec。
2. generated `GASAttributeSetReduceApplySystem` 已从 spec 写 attribute delta。
3. `GameplayFactProjectionSystem` 已能把 delta 投影成 typed facts。
4. generated `GASActiveEffectMutationApplySystem` / `PreTick` / `Remove` 已接入 CoreSimulation。
5. `GASDefinitionCatalogBlob` 已进入 runtime catalog 查找链。

## 仍成立风险

1. `GEExecutionCalculationSystem` 与 `GEExecutionCalculationOutputModifierSystem` 已切到 scheduled job 路径；当前风险转为 singleton stream / serial merge 的容量、ordering 和 profile 证据。
2. generated active effect pre-tick 已从 scan + `Complete()` 迁出；generated mutation/remove 仍有主线程 buffer loop / `EntityManager` 过渡实现。
3. command/spec/delta/fact 仍集中在 singleton stream owner DynamicBuffer。
4. period/overflow/granted cleanup 等复杂 active lifecycle 需要继续核对是否完全脱离 legacy runtime GE entity。
5. `ASCCommandGateway` 的 transient request entity 链路已退场；剩余风险是 bridge/helper 继续绕过 owner-local command sink 或直接触碰 `EntityManager`。

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
2. active effect tick/remove 不再依赖无解释的主线程 `Complete()`；remove/mutation 的 serial `EntityManager` loop 也迁入可解释 job/store 路径。
3. singleton stream owner 有容量、ordering、merge 证据，或已迁出到更合适 carrier。
4. legacy request fallback 被限制到低频边界，并有调用点清单。
