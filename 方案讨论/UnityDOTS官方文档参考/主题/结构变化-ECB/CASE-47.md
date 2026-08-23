# CASE-47: ECB AppendToBuffer + sortKey 多源并行 fan-in

**Primary Owner**: 结构变化-ECB
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `components-buffer-command-buffer.md`、`systems-entity-command-buffer-use.md`、`systems-entity-command-buffer-playback.md`、`systems-entity-command-buffer-automatic-playback.md`
**关联规则**: PRF-13, PRF-25, CASE-35, ECB-02

## 使用场景

当多个并行 job（来自不同 system）需要向同一 entity 的 `DynamicBuffer` 追加元素，且需要保证最终 buffer 内容确定性有序时。

## 模式描述

多个并行 job 各自创建独立 ECB，并使用各自的 `ParallelWriter` 向已存在的 buffer 录制 append 命令。每个 ECB 内以调度无关的 sort key 排序；多个 ECB 之间按 ECB System 中的创建顺序 playback。需要跨 World/构建确定性时，仍要按稳定业务键归并。

```csharp
// Phase 1: 各 system 独立 ECB，向同一 entity 追加
// System A: Attribute system
var ecbA = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
var handleA = new AttributeJob { ECB = ecbA.AsParallelWriter() }
    .ScheduleParallel(state.Dependency);

// System B: GE system
var ecbB = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
var handleB = new GEJob { ECB = ecbB.AsParallelWriter() }
    .ScheduleParallel(state.Dependency);

state.Dependency = JobHandle.CombineDependencies(handleA, handleB);

// Job 实现
[BurstCompile]
public partial struct AttributeJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;
    public Entity TargetEntity;

    public void Execute([ChunkIndexInQuery] int sortKey, in CAttributeDelta delta)
    {
        // sortKey 保证本 job 内命令的确定性顺序
        ECB.AppendToBuffer(sortKey, TargetEntity, new CEffectCommand { ... });
    }
}

// Phase 2: ECB playback
// 各 ECB 独立 playback，各自按 sortKey 排序后追加
// 最终 buffer 内容 = jobA 的有序序列 + jobB 的有序序列
```

## 注意事项

- 最稳妥的做法是在录制前让目标 entity 已静态持有 buffer；若用 ECB `AddBuffer<T>()` 创建，必须保证该命令所在 ECB 的 playback/排序先于任何 `AppendToBuffer`
- 各 job 必须使用独立 ECB 实例，禁止复用（PRF-25）
- 不要为“统一 sortKey 域”复用同一 ECB 给多个 job；官方建议每个 distinct job 使用独立 ECB。若需要全局总序，应先写带稳定业务键的记录，再显式排序归并
- 最终 buffer 的顺序是：（jobA 的有序序列）||（jobB 的有序序列）

## EX-GAS 适用点

- EffectCommand 从多个并行 job（Attribute system、GE system、Tag system）向同一 ASC entity 的 outbox buffer 追加输出
- 多效果并行评估的 ECB 写入
- TypedFact projection 的 fan-in 合并
