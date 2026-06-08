# ActiveEffect Runtime Owner 手写化目标态归档

日期: 2026-06-08

## 决策

Active-effect 生命周期不再属于 SourceGenerator 的 runtime lifecycle migration 责任域。SourceGenerator 只能生成定义索引、blob、静态查找、builder、纯 glue 或 ownership marker；active-effect 的调度系统、hot-path jobs、结构变更 owner、random lookup owner、SourceAttribute snapshot lane 必须由手写 Runtime Core 拥有。

## 目标态边界

- `GASActiveEffectRuntime` 是 active-effect lifecycle jobs/helper 的唯一 owner。
- `GEActiveEffectLifecycleSystems` 是 active-effect pre-tick、显式 remove、mutation apply 的调度 owner。
- `GEEffectCommandCatalogNormalizeSystem` 保持手写 owner，并位于 remove 之后、spec build 之前。
- `GASSystemScheduleContract` 只注册 Runtime Core 中的手写类型，不再通过 generated assembly type-name 字符串注册 active-effect lifecycle systems。
- `RuntimeActiveEffect.gen.cs` 只能保留 marker，不允许重新生成 `IJob`、`IJobChunk`、`ISystem`、`ComponentLookup`、`BufferLookup`、`EntityCommandBuffer` 等运行时 owner 代码。

## 后续推进

- 删除或迁出 `Assets/GAS/Generated/CodeGen/Runtime/ActiveEffectLifecycleOwnerSystems.cs` 的残留 wrapper，避免 generated 路径继续承载未调度 runtime 系统。
- 将当前 active-effect runtime helper 的机械抽取结果继续整理为更清晰的 owner 内聚结构，例如 command collect、slot tick、remove cleanup、grant ability、fact emit 分区。
- 在无头 AutoChess demo 中补 active-effect duration、period、stack、remove、granted tag/ability、SourceAttribute snapshot 的真实业务链路验收。
- 建立 profile 基线，确认 active mutation chunk-local apply 和 SourceAttribute snapshot lane 在 x100/x1000 场景下没有随机 lookup 或结构变更热点回退。
