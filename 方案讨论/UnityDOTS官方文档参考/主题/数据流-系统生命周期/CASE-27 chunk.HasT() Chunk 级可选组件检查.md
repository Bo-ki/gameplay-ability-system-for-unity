# CASE-27: chunk.Has<T>() Chunk 级可选组件检查

**Primary Owner**: 数据流-系统生命周期
**来源**: `iterating-data-ijobchunk.md` / `JobChunkExamples.cs`
**关联规则**: PRF-22

## 使用场景

在 IJobChunk 中需要高效判断当前 chunk 是否包含某个可选（opt-in）组件时，使用 `ArchetypeChunk.Has<T>()` 方法。

## 模式描述

`ArchetypeChunk.Has<T>()` 在 chunk 级别判断组件是否存在（所有 entity 在同一个 chunk 中要么都有该组件，要么都没有），避免了逐 entity 检查的开销。适用于**chunk 级均匀分布**的可选组件。

```csharp
[BurstCompile]
public struct SProcessJob : IJobChunk
{
    [ReadOnly] public ComponentTypeHandle<CAttribute> AttributeTypeHandle;
    [ReadOnly] public ComponentTypeHandle<CBuffModifier> BuffModifierHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
        bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var hasBuff = chunk.Has<CBuffModifier>();
        var attributes = chunk.GetNativeArray(ref AttributeTypeHandle);

        if (hasBuff)
        {
            var buffModifiers = chunk.GetNativeArray(ref BuffModifierHandle);
            for (int i = 0; i < chunk.Count; i++)
            {
                // 处理带 buff 的 entity
                ProcessWithBuff(attributes[i], buffModifiers[i]);
            }
        }
        else
        {
            for (int i = 0; i < chunk.Count; i++)
            {
                // 处理无 buff 的 entity
                ProcessWithoutBuff(attributes[i]);
            }
        }
    }
}
```

## 注意事项

- `chunk.Has<T>()` 仅在**组件存在/不存在于整个 chunk**时才有意义。若组件通过 `AddComponent` 逐 entity 添加，默认情况下同一 chunk 中分布不均匀，此时 chunk.Has<T> 可能返回混合结果
- 不能替代 `ChunkEntityEnumerator` 的 enableable 检查——`Has<T>` 检查组件类型存在性，不检查 enable/disable 状态

## EX-GAS 适用点

- 在 IJobChunk 中检查 chunk 是否包含 `CBuffModifier` 或 `CTagEffect` 等可选组件，分叉处理逻辑
