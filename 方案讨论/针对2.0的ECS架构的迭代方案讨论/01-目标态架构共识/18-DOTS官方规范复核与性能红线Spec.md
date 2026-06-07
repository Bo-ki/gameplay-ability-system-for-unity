# DOTS 官方规范复核与性能红线 Spec

## 目的

本 Spec 用 `../../UnityDOTS官方文档参考/主题/` 下的官方规则重新审查 EX-GAS 2.0 新划分设计。结论不是重画分层图，而是把分层图压成可执行、可审查、可拒绝的 DOTS 目标态硬约束。

本文件只讨论 GAS Runtime 架构，不讨论 Editor authoring 实现、不记录当前代码命中、不写迁移进度。

## 复核结论

新的四层划分方向成立，但成立条件必须收紧：

1. 四层是工程职责边界，不是自定义 runtime 抽象；Layer 3 必须直接落在 Unity Entities `World / SystemGroup / ISystem / Job / EntityQuery / ECB / Blob / DynamicBuffer` 上。
2. OOP Shell 和 Runtime Boundary 可以存在，但只能作为 capability 外壳；它们不能成为 gameplay 计算中间层，也不能暴露 `World`、`EntityManager`、`Entity`、`EntityQuery` 或可写 `DynamicBuffer`。
3. Runtime Core 不能为了 GAS 概念完整性牺牲 DOTS 物理规则。Ability、GameplayEffect、Attribute、Tag、Cue 必须先按数据性质分类，再选择承载。
4. Luban + SourceGenerator 的价值是生成 immutable catalog、unmanaged lookup、static pure glue 和 validation artifact；不是生成 lifecycle system、hidden query、hidden ECB 或 NativeContainer owner。
5. Debugger 的目标是性能证据和 DOTS API 健康度归因；字符串日志只是导出格式，不能替代 Profiler / Journaling / counters / API health evidence。

## 官方依据矩阵

| 官方主题 | 规则族 | 对新划分的强制结论 |
|---|---|---|
| `01-Entities系统与World.md` | `SYS-*` | Runtime Core 权威计算必须在 ECS System / Job；SystemGroup 只表达物理 phase，不按业务目录膨胀 |
| `02-查询遍历与Job.md` | `QRY-*` `JOB-*` | hot path 默认 `IJobEntity` / `IJobChunk`；`SystemAPI.Query` 主线程遍历只能用于 Debugger、Boundary 小规模读取或 proof-only |
| `03-结构变化-ECB-Enableable.md` | `SC-*` `ECB-*` `EN-*` | 结构变化只进 `GASStructuralCommitSystemGroup`；Enableable 只作整 entity / chunk skip cache，不替代局部 slot 状态 |
| `04-数据承载-Buffer-Chunk-Store.md` | `BUF-*` `STORE-*` | Buffer 必须有 owner、容量、清空 phase、pressure counter；全局 singleton buffer 只能 proof-only |
| `05-Baking-Blob-Prefab-Content.md` | `BAKE-*` `BLOB-*` `CONTENT-*` | 静态 definition 默认 Blob / Baker / Bootstrap；Runtime hot path 不反查 managed row / JSON / ScriptableObject |
| `06-Diagnostics-Profiler-Journaling.md` | `DBG-*` | Debugger 只能采集 evidence；不得成为 runtime 控制层或 gameplay event bus |
| `09-Burst-编译-向量化-AOT.md` | `BUR-*` | Runtime hot path 必须能 Burst；托管 delegate、managed registry、SystemBase 中间层不进入 Core |
| `10-Collections-Allocator-NativeStream.md` | `NAT-*` | NativeContainer owner、allocator、dispose / rewind 和 merge order 必须显式声明 |
| `13-DOTS编写规范与性能陷阱.md` | `PRF-*` | 禁止高频结构变化、随机 lookup 写、tag component 状态爆炸、临时 query、未归因 sync point |
| `20-GASRuntimeCore-API选型基线.md` | `SEL-*` | 每个 Runtime Core 任务必须先交 API 选型表、拒绝理由和重新选型触发条件 |

## 四层划分复核

| 层级 | 复核结论 | 必须保持的 DOTS 边界 | 违规则退回 |
|---|---|---|---|
| Application Shell | 保留是合理的 | 只表达业务动作、UI/AI/Network/Demo 意图和观察消费 | Shell 直接持有 ECS 可写句柄、计算 damage/cooldown/requirement |
| Runtime Boundary | 保留是必要的 | command capability、snapshot capability、diagnostics capability、bootstrap/definition capability 分离 | 万能 Adapter 混合 command、snapshot、EntityManager、Debugger、Catalog 生命周期 |
| GAS Runtime Core | 必须是唯一 gameplay 权威 | 少量 physical SystemGroup + kernel lane；`ISystem` / Burst job / owner-local data / deterministic stream | OOP facade、SystemBase manager、每个 GAS 概念一个 group、每帧 runtime GE entity churn |
| Definition & Generation | 必须收权 | Blob catalog、generated lookup、static pure glue、Baker / Bootstrap、validation report | generated lifecycle system、hidden query、hidden ECB、managed config lookup、runtime `BlobBuilder` |

## Runtime Core 性能红线

以下红线命中时，目标态不成立，不能用“兼容旧链路”“先跑通业务”或“后续优化”豁免：

1. Runtime Core hot path 依赖 managed component、managed shared component、managed config row、JSON reader、`Dictionary`、delegate registry 或 `ScriptableObject`。
2. Ability / GE / Cue / Tag 的瞬时状态默认创建 entity，并在本帧销毁，导致 entity churn。
3. Target Resolve、Effect Fan-In、State Evaluate、Attribute Apply、Gameplay Fact、Boundary Projection 中直接执行 `EntityManager` 结构变化。
4. SourceGenerator 生成 `ISystem`、`SystemBase`、`OnUpdate`、system registration、`SystemAPI.Query` owner、ECB owner、`EntityManager` write 或 NativeContainer owner。
5. Debugger 在 Runtime hot loop 中拼字符串、装箱、写托管日志对象，或把日志总线作为 simulation 路由。
6. 大容量 fan-in 固化为全局 singleton DynamicBuffer，且没有 `NativeStream` / per-owner range 的拒绝理由和重新选型触发条件。
7. Attribute Apply 通过高频跨 entity `ComponentLookup` / `BufferLookup` 随机写实现，且没有唯一写证明或 target-grouped 替代方案。
8. Enableable 被当成任意 boolean 状态开关，而不是 profiler 证明后的 query skip cache。
9. `SystemAPI.Query` 主线程遍历被用于可规模化 Runtime Core hot path。
10. 新增 SystemGroup 只是为了业务目录清晰，而不是因为存在新的同步、结构变化、固定步或投影物理边界。
11. Runtime Core 新增或保留 `IAspect` 包装作为目标态 API；Entities 1.4.6 下统一改为直接 component 访问和明确 query contract。

## GAS 业务链路复核

| 业务链路 | 目标态承载 | 为什么这样设计 | 拒绝的旧做法 |
|---|---|---|---|
| Ability 激活 | Boundary request / command record -> Core activation lane | Shell 只提交意图；cost、cooldown、tag requirement 在 Core 数据流中统一判定 | OOP Ability facade 直接调用 effect / attribute service |
| TargetData | request-owned buffer / frame-local target record / deterministic sort key | target resolve 是可并行、可复现的 Core lane，不是托管回调 | 托管 TargetCatcher、GameObject list、delegate callback |
| Instant GE | `GECommandSeedRecord` -> spec / modifier / attribute delta / fact | 瞬时效果不需要跨帧 identity，避免 entity churn 和结构变化 sync | instant GE runtime entity create/destroy |
| Duration GE | owner-local active slot buffer + lifecycle flags + cleanup path | 跨帧状态属于 ASC owner，周期、堆叠、过期可线性遍历 | 每个 active effect 一个托管对象或不受控 entity |
| Attribute Apply | target-grouped delta reduce + authority write | 写入集中到目标 ASC，减少 random lookup 和竞态 | 每个 spec 随机写目标 component |
| Gameplay Tag / Status | bit field / enum / generated tag mask；Enableable 只作 skip cache | 高频 tag/status 不做 Add/Remove component | 每个状态一个 tag component 或 enableable component |
| Cue / Presentation | Boundary outbox / observation fact | 表现是派生 side effect，不决定 gameplay | Cue entity 反向持有 GameObject / VFX 资源并参与 Core |
| Debugger | sampled counters / Native evidence / official diff | 诊断输出性能热点、API health 和事实链 | Runtime 中央 Logger / EventBus |

## API 选型门槛

每个 Runtime Core 任务开始前必须交付以下表格；缺一项则不得实现：

| 字段 | 要求 |
|---|---|
| 数据性质 | 明确属于 Gameplay 权威、Transient command、Telemetry、Presentation、Structural mutation、Definition 输入中的哪一类 |
| 候选 API | 至少评估 `DynamicBuffer`、`NativeStream`、owner-local component/buffer、Blob lookup、ECB、EntityQuery bulk、Enableable / Chunk Component 中相关候选 |
| 采用 API | 写出 owner、生命周期、读写集合、clear / dispose phase、deterministic ordering |
| 拒绝 API | 不得只写“不需要”；必须写拒绝场景和代价 |
| 官方依据 | 引用 `SYS/QRY/JOB/SC/ECB/EN/BUF/BLOB/NAT/BUR/PRF/SEL/CASE/ODF` 规则编号 |
| 重选型触发 | 写出规模、buffer spill、merge cost、sync point、random lookup、enableable wait 等阈值 |
| Proof-only 标记 | 若是 proof-only，必须写可接受范围和退出任务 |

## 目标代码形态复核

### Runtime Core System

目标态手写 Runtime System 必须像下面这样拥有 query、lookup、dependency 和 evidence 归因：

```csharp
[BurstCompile]
public partial struct GASEffectFanInSystem : ISystem
{
    private EntityQuery _commandQuery;
    private ComponentTypeHandle<GASAbilityActivationCommandRecord> _commandType;
    private BufferTypeHandle<GASResolvedModifierRecord> _modifierBufferType;

    public void OnCreate(ref SystemState state)
    {
        _commandQuery = state.GetEntityQuery(ComponentType.ReadOnly<GASAbilityActivationCommandRecord>());
        state.RequireForUpdate(_commandQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _commandType = state.GetComponentTypeHandle<GASAbilityActivationCommandRecord>(true);
        _modifierBufferType = state.GetBufferTypeHandle<GASResolvedModifierRecord>(false);

        var job = new FanInJob
        {
            CommandType = _commandType,
            ModifierBufferType = _modifierBufferType
        };

        state.Dependency = job.ScheduleParallel(_commandQuery, state.Dependency);
    }
}
```

关键不是类型名，而是 owner：query 由 `ISystem.OnCreate` 创建，type handle 在 `OnUpdate` 刷新，job 由 system 调度，dependency 回写 `state.Dependency`，结构变化不在此处执行。

### Generated Runtime Glue

目标态 generated glue 必须是静态纯解析，不持有 ECS owner：

```csharp
public static partial class GASGeneratedRuntimeDefinitionResolver
{
    public static bool TryBuildEffectSeed(
        ref readonly GASDefinitionCatalogBlob catalog,
        AbilityDefinitionIndex abilityIndex,
        out GECommandSeedRecord seed)
    {
        ref readonly var ability = ref catalog.Abilities[abilityIndex.Value];
        seed = new GECommandSeedRecord
        {
            GameplayEffectIndex = ability.PrimaryGameplayEffectIndex,
            RequirementSetIndex = ability.RequirementSetIndex,
            MagnitudeSetIndex = ability.MagnitudeSetIndex
        };
        return seed.GameplayEffectIndex.IsValid;
    }
}
```

generated glue 不得创建 query、写 component、写 ECB、调度 job、持有 NativeContainer 或做 runtime lifecycle 注册。

## Debugger 复核

Debugger 目标态必须输出以下 evidence，才称得上能支撑 DOTS 性能优化：

| Evidence | 用途 |
|---|---|
| `systemGroupTickMs` / `laneTickMs` / `jobScheduleCount` | 找出物理 phase 和 kernel lane 热点 |
| `queryCount` / `typeHandleRefreshCount` / `lookupRefreshCount` | 发现 system 拆分、query 过多、lookup 滥用 |
| `nativeStreamSegmentCount` / `bufferExternalizedRatio` | 发现 fan-in 压力和 DynamicBuffer spill |
| `structuralChangeCount` / `ecbCommandCount` / `playbackMs` | 证明结构变化是否集中且成本可控 |
| `enableableToggleCount` / `enableableWaitMs` | 判断 Enableable skip 是否值得 |
| `randomLookupReadCount` / `randomLookupWriteCount` | 发现跨 entity 随机访问热点 |
| `managedAllocationBytes` / `gcAllocCount` | 防止 Debugger / Boundary 污染 Runtime Core 性能结论 |
| `officialDiffCoverage` | 对照 Profiler、Entities Journaling、Burst / AOT、PackageCache 规则覆盖 |

## 验收

1. 目标态任务引用本文件时，必须同时引用 `20-GASRuntimeCore-API选型基线.md` 和 `90-规则编号索引.md`。
2. 每个 Runtime Core System 都有 query contract、读写集合、dependency contract、structural permission 和 Debugger counters。
3. 每个 generated runtime-visible artifact 都通过 SourceGenerator Ownership Gate，不含 lifecycle / query / ECB / EntityManager / NativeContainer owner 越权。
4. 每个 Buffer / NativeContainer / Blob 都有 owner、生命周期、容量或 dispose 规则。
5. 每条 GAS 业务链路都能从 Shell intent 追踪到 Core command/spec/delta/fact，再追踪到 Boundary projection；中间没有 OOP gameplay 计算层。
6. 任意性能结论必须按 owner 分类：Runtime Core、Runtime Boundary、Debugger / Observation、Definition / Generation、Demo Adapter，不得用平均 tick 或字符串日志掩盖。

## 禁止方向

1. 不把“纯 ECS”解释成在 OOP manager 下面包一层 entity helper。
2. 不把“适配层”解释成可以读写任意 ECS 句柄的万能 runtime access。
3. 不把“SourceGenerator 相性好”解释成批量生成 System 或生命周期。
4. 不把“Debugger 包含日志”解释成 Runtime Core 需要托管 logger。
5. 不把“兼容旧链路”作为保留 OOP facade、EventBus、SpecStream singleton、runtime GE entity churn 或 generated lifecycle 的理由。
