# CASE-20: 批量状态切换用 EnabledMask

**Primary Owner**: Enableable-Component选型
**来源**: Enableable-Component选型.md
**关联规则**: EN-03

## 使用场景
在 IJobChunk 中需要对 chunk 内所有或大部分 entity 批量切换 enableable 状态。

## 模式描述
使用 `EnabledMask` 提供 chunk 级 enabled bit 数组的直接索引，比逐个 `SetComponentEnabled` 快 10-100x。

```csharp
var mask = chunk.GetEnabledMask(ref abilityActiveHandle);
for (int i = 0; i < chunk.Count; i++)
    if (ShouldDeactivate(i))
        mask[i] = false;
```

## 注意事项
- 仅在 IJobChunk 中可用
- 批量操作，不适合随机访问
- 需要获得对应 component 的 `ComponentTypeHandle`

## EX-GAS 适用点
- 批量复活/批量击杀场景中的状态切换
- Effect 批量到期处理
- 同时大量触发 cooldown 重置
