# CASE-05: ECB 延迟结构变化

**Primary Owner**: 结构变化-ECB
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `systems-entity-command-buffer-use.md`、`systems-entity-command-buffer-automatic-playback.md`、`performance-sync-points.md`
**关联规则**: SC-01, PRF-02, PRF-04, ECB-03

## 使用场景

当需要在 job 中记录未来的结构变化（创建/销毁 entity、添加/移除 component），或希望把分散的结构变化集中到明确的 playback 位置时。job 本身不执行结构变化。

## 模式描述

ECB 在 job 中只录制命令，之后在主线程 playback。并行 job 必须使用 `EntityCommandBuffer.ParallelWriter`；集中 playback 通常能减少结构变化造成的 sync point 数量。

```csharp
// 从专属 ECB System 获取 ECB
var ecb = gasStructuralECBSystem.CreateCommandBuffer(state.WorldUnmanaged);

// Job 中录制结构变化
[BurstCompile]
public partial struct CreateEffectJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;

    public void Execute([ChunkIndexInQuery] int sortKey, Entity e, in CApplyEffectRequest request)
    {
        var effectEntity = ECB.CreateEntity(sortKey);
        ECB.AddComponent(sortKey, effectEntity, new CEffectInUsage { ... });
    }
}

var job = new CreateEffectJob { ECB = ecb.AsParallelWriter() };
state.Dependency = job.ScheduleParallel(state.Dependency);
```

## 注意事项

- ECB 仍然产生结构变化，只是延迟合并。优先考虑 enableable component 或 DynamicBuffer 替代高频 entity 创建
- 通过 `EntityCommandBufferSystem.CreateCommandBuffer` 获取的 ECB 由该系统自动 playback/dispose，不要手工 playback 或 dispose
- 手工 `new EntityCommandBuffer(...)` 创建的 ECB 由调用方负责 playback/dispose，并可按 `PlaybackPolicy` 选择单次或多次播放

## EX-GAS 适用点

- `GasStructuralPlaybackSystemGroup` 中的 ECB playback phase
- `SEffectApply` 中通过 ECB 录制效果应用的结构变化
- `GameplayEffectRequestWriter` 中通过 ECB 延迟效果请求写入
