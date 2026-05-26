# CASE-45: System-Associated Entity Data

**Primary Owner**: 数据流-系统生命周期
**来源**: `systems-data.md`
**关联规则**: PRF-29

## 使用场景

当 system 需要跨帧/跨 system 共享数据（如全局配置、累积统计数据、调试状态），且不能使用 static 字段（static 不参与 safety system、不考虑 threading）时。

## 模式描述

每个 ISystem 在 World 中关联一个唯一的 entity（`state.SystemHandle` 对应一个 entity）。通过在该 entity 上添加 component 来存储 system 级的公开数据。其他 system 通过 `EntityQuery` + `SystemHandle` 或 `SystemAPI.GetComponent` 可正常查询。

```csharp
// System 级数据定义
public struct SDebugState : IComponentData
{
    public int TotalEffectsApplied;
    public int LastFrameStructuralChangeCount;
}

// 写入方 system
[BurstCompile]
public partial struct SDebugMonitorSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // 将调试数据存入 system-associated entity
        SystemAPI.SetComponent(state.SystemHandle, new SDebugState
        {
            TotalEffectsApplied = count,
            LastFrameStructuralChangeCount = structuralChanges
        });
    }
}

// 读取方 system
[BurstCompile]
public partial struct SDebugReaderSystem : ISystem
{
    public EntityQuery DebugStateQuery;

    public void OnCreate(ref SystemState state)
    {
        DebugStateQuery = state.GetEntityQuery(
            ComponentType.ReadOnly<SDebugState>()
        );
    }

    public void OnUpdate(ref SystemState state)
    {
        if (DebugStateQuery.TryGetSingleton<SDebugState>(out var debugState))
        {
            // 读取调试数据
            UnityEngine.Debug.Log($"Effects applied: {debugState.TotalEffectsApplied}");
        }
    }
}
```

## 注意事项

- System-associated entity 在 system 创建时自动生成，在 system 销毁时自动清理（cleanup component 机制）
- 数据通过 component 存储，参与 safety system——线程安全
- 不需要手动管理生命周期
- 优于 static 字段：static 字段不参与 safety system、不跟随 system 生命周期、不兼容 Burst

## EX-GAS 适用点

- Debugger 系统通过 system-associated entity 存储每帧统计数据
- 全局配置使用 Singleton component（更简单）而非 system-associated entity
- System 级累积计数器（如 `TotalEffectsApplied`）
