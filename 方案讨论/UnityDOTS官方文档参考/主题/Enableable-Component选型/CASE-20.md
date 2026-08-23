# CASE-20: 批量状态切换用 EnabledMask

**Primary Owner**: Enableable-Component选型
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**: Unity 6000.3 / Entities 1.4.6
**官方来源**: `components-enableable-use.md`
**关联规则**: EN-03

## 使用场景
在 IJobChunk 中需要对 chunk 内所有或大部分 entity 批量切换 enableable 状态。

## 模式描述
使用 `ArchetypeChunk.GetEnabledMask(ref handle)` 获取 `EnabledMask`，再按当前 chunk 内的 entity index 读写启用状态。官方建议性能敏感时优先使用这种迭代式访问，但没有给出固定倍率。

```csharp
var mask = chunk.GetEnabledMask(ref abilityActiveHandle);
for (int i = 0; i < chunk.Count; i++)
    if (ShouldDeactivate(i))
        mask[i] = false;
```

## 注意事项
- 面向 `ArchetypeChunk` 的 chunk 级访问，典型用途是 IJobChunk
- 批量操作，不适合随机访问
- 需要获得对应 component 的 `ComponentTypeHandle`
- 是否优于重构后的 IJobEntity / `EnabledRefRW<T>`，应在目标设备上用 Profiler 验证

## EX-GAS 适用点
- 批量复活/批量击杀场景中的状态切换
- Effect 批量到期处理
- 同时大量触发 cooldown 重置
