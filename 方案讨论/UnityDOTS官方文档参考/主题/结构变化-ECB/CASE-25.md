# CASE-25: IECBSingleton 自定义 ECB System

**Primary Owner**: 结构变化-ECB
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `systems-entity-command-buffer-automatic-playback.md`、`allocators-entity-command-buffer.md`；官方示例 `DocCodeSamples.Tests/EntityCommandBuffers.cs`
**关联规则**: ECB-03, PRF-04

## 使用场景

当需要将 ECB playback 放置在 frame backbone 的特定 phase，而不是使用默认的 SimulationSystemGroup ECB System 时。

## 模式描述

继承 `EntityCommandBufferSystem`，在内嵌 singleton 上完整实现 `IECBSingleton`，并在 `OnCreate` 中用 `RegisterSingleton` 绑定 `PendingBuffers` 与 ECB allocator。生产者通过该 singleton 创建 ECB，ECB System 会完成注册的生产 job、按 ECB 创建顺序 playback 并 dispose。

```csharp
/// <summary>
/// 在 GAS 结构提交阶段统一播放生产者录制的 ECB。
/// </summary>
[UpdateInGroup(typeof(GasStructuralPlaybackSystemGroup), OrderLast = true)]
public partial class GasEndStructuralECBSystem : EntityCommandBufferSystem
{
    public unsafe struct Singleton : IComponentData, IECBSingleton
    {
        private UnsafeList<EntityCommandBuffer>* pendingBuffers;
        private AllocatorManager.AllocatorHandle allocator;

        public EntityCommandBuffer CreateCommandBuffer(WorldUnmanaged world)
        {
            return EntityCommandBufferSystem.CreateCommandBuffer(
                ref *pendingBuffers,
                allocator,
                world);
        }

        public void SetPendingBufferList(ref UnsafeList<EntityCommandBuffer> buffers)
        {
            pendingBuffers = (UnsafeList<EntityCommandBuffer>*)UnsafeUtility.AddressOf(ref buffers);
        }

        public void SetAllocator(Allocator allocatorIn)
        {
            allocator = allocatorIn;
        }

        public void SetAllocator(AllocatorManager.AllocatorHandle allocatorIn)
        {
            allocator = allocatorIn;
        }
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        this.RegisterSingleton<Singleton>(ref PendingBuffers, World.Unmanaged);
    }
}

// 其他 system 通过 Singleton 获取 ECB
var ecbSingleton = SystemAPI.GetSingleton<GasEndStructuralECBSystem.Singleton>();
var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
```

## 注意事项

- 自定义 ECB System 必须通过 `UpdateInGroup` 放到正确的 SystemGroup 和排序位置
- 如果不使用 singleton，只能在 managed 侧取得 ECB System 实例并负责 `AddJobHandleForProducer`；Runtime Core 优先 singleton 模式
- 同一 group 内可以有多个 ECB System（begin/end 模式）

## EX-GAS 适用点

- `GasStructuralPlaybackSystemGroup` 中定义 `BeginGasStructuralECBSystem` 和 `EndGasStructuralECBSystem`
- 所有需要结构变化的 system 通过此自定义 ECB System 获取 ECB，而非默认的 Simulation ECB
