# CASE-38: IJobEntityChunkBeginEnd Chunk 预评估

**Primary Owner**: 数据流-系统生命周期
**来源**: `systems-data-granularity.md`
**关联规则**: PRF-26

## 使用场景

在 IJobEntity 或 IJobChunk 中需要在遍历 entity 前对 chunk 级数据做一次性预评估（检查 chunk 是否有只读数据变化、是否需要跳过整个 chunk），以减少不必要的逐 entity 操作。

## 模式描述

`IJobEntityChunkBeginEnd` 接口提供 `OnChunkBegin` 和 `OnChunkEnd` 回调，允许在 chunk 级别进行预评估和后处理。如果 `OnChunkBegin` 返回 `false`，跳过该 chunk 的所有 entity。

```csharp
[BurstCompile]
public partial struct SChunkPreEvalJob : IJobEntity, IJobEntityChunkBeginEnd
{
    [ReadOnly] public ComponentTypeHandle<CAttributeConfig> ConfigTypeHandle;
    public bool ChunkHasChanged;

    public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex)
    {
        // 预评估：只处理 config 变更的 chunk
        var configs = chunk.GetNativeArray(ref ConfigTypeHandle);
        ChunkHasChanged = chunk.DidChange(ref ConfigTypeHandle, 1);

        // 返回 false 跳过整个 chunk
        return ChunkHasChanged;
    }

    public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex) { }

    public void Execute(Entity e, ref CAttributeCurrent current, in CAttributeConfig config)
    {
        // 只在 chunk 有变更时执行
        current.Value = ApplyConfig(current.Value, config);
    }
}
```

## 注意事项

- `OnChunkBegin` 的返回值控制该 chunk 是否被遍历：`false` = 跳过
- 在实现 `IJobEntityChunkBeginEnd` 时，job struct 中的字段在 `OnChunkBegin` 中修改后对同一 chunk 的所有 entity 可见，但对不同 chunk 不可见
- 结合 PRF-26 的读写分离使用效果最佳——只读字段拆到独立 component，用 `DidChange` 检测变更
- Burst 支持 `IJobEntityChunkBeginEnd`

## EX-GAS 适用点

- Attribute recalculate 中仅处理 config 变更的 chunk
- 按 chunk 跳过的 effect 条件评估
- 需要 chunk 级预评估的批量数据操作
