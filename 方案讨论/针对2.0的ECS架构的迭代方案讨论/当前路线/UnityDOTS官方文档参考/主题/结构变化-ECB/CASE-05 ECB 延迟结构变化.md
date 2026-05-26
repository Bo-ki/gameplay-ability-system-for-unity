# CASE-05: ECB 延迟结构变化

**Primary Owner**: 结构变化-ECB
**来源**: `systems-entity-command-buffer.md`、`performance-sync-points.md`
**关联规则**: SC-01, PRF-02, PRF-04, ECB-03

## 使用场景

当需要在 job 线程中安全地执行结构变化（创建/销毁 entity、添加/移除 component）、或将多个结构变化合并为一次 sync point 时。

## 模式描述

所有需要创建/销毁 entity、添加/移除 component 的操作，录制到 ECB 中，在指定的 playabck phase 统一提交。ECB 在 job 线程中安全地"录制"结构变化命令，在 playback phase 由主线程统一提交，将多次结构变化合并为一次 sync point。

```csharp
// 从专属 ECB System 获取 ECB
var ecb = gasStructuralECBSystem.CreateCommandBuffer(state.WorldUnmanaged);

// Job 中录制结构变化
[BurstCompile]
public partial struct CreateEffectJob : IJobEntity
{
    public EntityCommandBuffer ECB;

    public void Execute(Entity e, in CApplyEffectRequest request)
    {
        var effectEntity = ECB.CreateEntity();
        ECB.AddComponent(effectEntity, new CEffectInUsage { ... });
    }
}

var job = new CreateEffectJob { ECB = ecb };
state.Dependency = job.ScheduleParallel(state.Dependency);
```

## 注意事项

- ECB 仍然产生结构变化，只是延迟合并。优先考虑 enableable component 或 DynamicBuffer 替代高频 entity 创建
- ECB 必须在 `OnUpdate` 中通过 `CreateCommandBuffer` 获取，不能跨帧复用
- Playback 后 ECB allocator rewind，所有内存自动释放

## EX-GAS 适用点

- `GasStructuralPlaybackSystemGroup` 中的 ECB playback phase
- `SEffectApply` 中通过 ECB 录制效果应用的结构变化
- `GameplayEffectRequestWriter` 中通过 ECB 延迟效果请求写入
