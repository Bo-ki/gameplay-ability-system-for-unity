# CASE-15: Cleanup Component 生命周期

**Primary Owner**: 数据流-系统生命周期
**来源**: `components-cleanup.md`
**关联规则**: 无直接关联

## 使用场景

当 entity 销毁后需要执行清理逻辑（释放资源、记录日志、触发响应），或需要在 entity 销毁时保留部分数据以供观察时。

## 模式描述

Cleanup Component 是一种特殊的 component，它在 entity 被销毁时不会被立即清除。ECS 将已销毁 entity 的数据复制到一份临时的"cleanup entity"中，其上仍保留所有 cleanup component，供 system 在后续帧中处理。

```csharp
// 定义 cleanup component（实现 ICleanupComponentData 接口）
public struct CDeathEvent : ICleanupComponentData
{
    public Entity OriginalEntity;
    public float DeathTime;
    public float3 Position;
}

// 监听 cleanup entity 的系统
[UpdateInGroup(typeof(GameplaySystemGroup))]
public partial struct SDeathEventCleanup : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (deathEvent, entity) in SystemAPI.Query<CDeathEvent>().WithEntityAccess())
        {
            // 处理死亡事件（播放 VFX、分配经验等）
            // 处理完毕后必须销毁 cleanup entity
            state.EntityManager.DestroyEntity(entity);
        }
    }
}
```

## 注意事项

- Cleanup Component 增加 entity 销毁成本（数据复制到 cleanup entity）
- 必须由 system 显式销毁 cleanup entity，否则会累积
- 同一 entity 的多个 cleanup component 会合并到同一个 cleanup entity 上
- 滥用 cleanup component 会导致 per-frame entity 泄漏

## EX-GAS 适用点

- 单位死亡事件的响应处理
- 效果到期后的资源释放通知
- Debugger 中观察已销毁 entity 的最后状态
