# CASE-25: IECBSingleton 自定义 ECB System

**Primary Owner**: 结构变化-ECB
**来源**: `systems-entity-command-buffer.md`
**关联规则**: ECB-03, PRF-04

## 使用场景

当需要将 ECB playback 放置在 frame backbone 的特定 phase，而不是使用默认的 SimulationSystemGroup ECB System 时。

## 模式描述

继承 `EntityCommandBufferSystem` + 实现自定义 `IECBSingleton` + `RegisterSingleton<Singleton>`，在 frame backbone 的特定 phase 放置专用的 ECB playback。

```csharp
// 定义自定义 ECB System
[UpdateInGroup(typeof(GasStructuralPlaybackSystemGroup), OrderLast = true)]
public partial class GasEndStructuralECBSystem : EntityCommandBufferSystem
{
    // 可选：定义 Singleton 供其他 system 访问
    public struct Singleton : IComponentData
    {
        public Entity ECBSystemEntity;
    }
}

// 在 World 创建时注册
var ecbSystem = world.GetOrCreateSystem<GasEndStructuralECBSystem>();
world.EntityManager.AddComponentData(ecbSystem, new GasEndStructuralECBSystem.Singleton
{
    ECBSystemEntity = ecbSystem
});

// 其他 system 通过 Singleton 获取 ECB
var ecbSingleton = SystemAPI.GetSingleton<GasEndStructuralECBSystem.Singleton>();
var ecb = state.EntityManager.GetSystemHandle<GasEndStructuralECBSystem>()
    .CreateCommandBuffer(state.WorldUnmanaged);
```

## 注意事项

- 自定义 ECB System 必须通过 `UpdateInGroup` 放到正确的 SystemGroup 和排序位置
- 如果不注册 Singleton，其他 system 需要直接通过 `SystemHandle` 获取 ECB
- 同一 group 内可以有多个 ECB System（begin/end 模式）

## EX-GAS 适用点

- `GasStructuralPlaybackSystemGroup` 中定义 `BeginGasStructuralECBSystem` 和 `EndGasStructuralECBSystem`
- 所有需要结构变化的 system 通过此自定义 ECB System 获取 ECB，而非默认的 Simulation ECB
