# 10B-03A：羁绊系统 Spec

> Owner：`01-目标态架构共识/10B-AutoChess完整业务案例` | 状态：目标态子 Spec | 来源入口：[10B-03](10B-03-羁绊与Runtime基础设施Spec.md)

## 目的

定义 AutoChess 业务案例中的羁绊配置、羁绊检测和 typed fact 输出方式。羁绊系统是业务机制 Spec，不拥有 Runtime Core 通用基础设施，也不直接定义 buffer/catalog 的全局形态。

## 非目标

1. 不把当前实现类、验证日志或迁移状态写成本 Spec 的完成证明。
2. 不通过 OOP event bus、GameObject 查找或表现层事件驱动羁绊状态。
3. 不在羁绊检测 System 中直接做结构变化、表现播放或属性最终写回。

## 配置形态

羁绊配置由 Luban 表表达，SourceGenerator 生成稳定 id、tag bit 映射、阈值数组和 GE / modifier 引用。表行只描述业务规则，不描述 Runtime 执行顺序。

| 字段 | 目标含义 | 约束 |
|---|---|---|
| `SynergyId` | 稳定羁绊 id | 只增不复用 |
| `Name` | 策划可读名称 | 不进入 hot path |
| `TagQuery` | Race / Class / Camp / 自定义 tag 条件 | 生成 bitset 或 blob query |
| `Thresholds` | 阈值列表 | 允许多段阈值，升序保存 |
| `EffectGE` | 阈值满足后产生的 GE 或 modifier seed | 只引用 definition id |
| `Description` | 编辑器展示文案 | 不参与 Runtime 判定 |

示例：

| SynergyId | Name | TagQuery | Thresholds | EffectGE | Description |
|---:|---|---|---|---:|---|
| 1 | 冰系羁绊 | `Race=Frost` | `3` | 4002 | 3 个以上冰系单位在场时生成减速类 fact/command seed |
| 2 | 牧师羁绊 | `Class=Priest` | `2` | 0 | 2 个以上牧师在场时生成治疗倍率 fact |

## Runtime 数据流

```text
Alive ASC chunks
  -> read TagMask / Camp / Alive enableable state
  -> count synergy tags by chunk
  -> write SynergyFactRecord into NativeStream
  -> deterministic merge by SynergyId + Camp + threshold
  -> write GameplayFactRecord
  -> next-frame command seed or fact reaction
```

核心约束：

1. 羁绊检测必须在 GAS Runtime Core 的 simulation lane 中运行，默认使用 `ISystem + IJobChunk`。
2. 每个 job 只读取 chunk-local unit tag/camp/alive state，不访问 managed collection。
3. 并行输出写入 frame-local `NativeStream`；merge job 负责稳定排序、去重和阈值跨越判定。
4. 输出是 typed gameplay fact，不是全局事件字符串，也不是直接属性写入。
5. fact reaction 默认写 next-frame command seed，避免同帧无限连锁；允许只读 fact 被 Debugger / validation 消费。

## 目标代码骨架

```csharp
[UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
[BurstCompile]
public partial struct SynergyDetectSystem : ISystem
{
    private EntityQuery _aliveAscQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _aliveAscQuery = state.GetEntityQuery(
            ComponentType.ReadOnly<ASCIdentityComponent>(),
            ComponentType.ReadOnly<TagMaskComponent>(),
            ComponentType.ReadOnly<ASCAliveTag>());

        state.RequireForUpdate(_aliveAscQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var tagMaskType = SystemAPI.GetComponentTypeHandle<TagMaskComponent>(true);
        var identityType = SystemAPI.GetComponentTypeHandle<ASCIdentityComponent>(true);
        var chunkCount = _aliveAscQuery.CalculateChunkCount();
        var facts = new NativeStream(chunkCount, Allocator.TempJob);

        state.Dependency = new CountSynergyJob
        {
            TagMaskType = tagMaskType,
            IdentityType = identityType,
            Writer = facts.AsWriter(),
            Frame = GASFrameClock.ResolveFrame(ref state),
        }.ScheduleParallel(_aliveAscQuery, state.Dependency);

        state.Dependency = new MergeSynergyFactsJob
        {
            Reader = facts.AsReader(),
            FactWriter = GASGameplayFactFrame.Resolve(ref state).FactWriter,
        }.Schedule(state.Dependency);

        state.Dependency = facts.Dispose(state.Dependency);
    }
}

[BurstCompile]
public struct CountSynergyJob : IJobChunk
{
    [ReadOnly] public ComponentTypeHandle<TagMaskComponent> TagMaskType;
    [ReadOnly] public ComponentTypeHandle<ASCIdentityComponent> IdentityType;
    public NativeStream.Writer Writer;
    public int Frame;

    public void Execute(
        in ArchetypeChunk chunk,
        int unfilteredChunkIndex,
        bool useEnabledMask,
        in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
    {
        var tags = chunk.GetNativeArray(ref TagMaskType);
        var identities = chunk.GetNativeArray(ref IdentityType);
        var accumulator = default(SynergyAccumulator);

        var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
        while (enumerator.NextEntityIndex(out var i))
            accumulator.Add(tags[i], identities[i]);

        Writer.BeginForEachIndex(unfilteredChunkIndex);
        accumulator.WriteFacts(Frame, unfilteredChunkIndex, ref Writer);
        Writer.EndForEachIndex();
    }
}
```

## Fact 合约

| Fact | 产生条件 | 消费方 | 禁止 |
|---|---|---|---|
| `SynergyThresholdEntered` | 某 camp 的 tag count 从低于阈值变为达到阈值 | Fact reaction / Debugger / validation | 直接改 AttributeSet |
| `SynergyThresholdExited` | 某 camp 的 tag count 从达到阈值变为低于阈值 | Fact reaction / Debugger / validation | 直接销毁 GE entity |
| `SynergyStateSnapshot` | diagnostic pass 需要完整解释羁绊状态 | Debugger / diagram export | 作为 gameplay truth 回写 |

## DOTS 约束

1. `IJobChunk` 是默认遍历方式；`SystemAPI.Query` 只能用于低频调试或 authoring，不用于羁绊 hot path。
2. `NativeStream` 生命周期由 frame arena / system update 持有，业务 job 不泄漏 writer 到 OOP Shell。
3. merge 顺序必须稳定；排序键至少包含 `CampId`、`SynergyId`、`Threshold` 和 chunk sort key。
4. 阈值状态应 owner-local 保存，避免每帧只靠 fact 反推上一状态。
5. 所有配置 lookup 使用 generated static lookup / blob，不在检测 job 中查 managed table。

## 验收门槛

1. 给定一组单位 tag/camp/alive state，羁绊检测能稳定输出进入/退出 fact。
2. 同一输入多次运行输出顺序一致。
3. fact reaction 与 attribute/effect apply 解耦；羁绊检测本身不直接写属性。
4. Debugger 能解释 count、threshold、fact 和 command seed 的来源。
5. 规模放大时，成本归因到 SynergyDetect / merge / fact reaction，而不是 OOP event bus 或 managed query。
