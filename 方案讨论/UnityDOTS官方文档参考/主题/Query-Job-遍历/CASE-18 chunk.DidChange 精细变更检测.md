# CASE-18: chunk.DidChange 精细变更检测

**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — ChangeFilter 与 chunk.DidChange 节、CASE-18
**关联规则**: QRY-04

## 使用场景
在 IJobChunk 内按 component type 逐一检查 chunk 是否变更，比 query 级 filter 更灵活。用于属性脏标记检测、effect 重评估跳过。

## 模式描述
`chunk.DidChange` 在 IJobChunk 内部按 component type 逐一检查，允许精确判断某种 component 是否发生了变更，而不是整个 query 级的粗粒度过滤。

**ChangeFilter 与 chunk.DidChange 对比：**

| 机制 | 作用层级 | 判断依据 | 适用场景 |
|------|----------|----------|----------|
| `[WithChangeFilter]` | Query 级 | chunk 中**任意** entity 的**任意**指定 component 变更 → 整个 chunk 通过 | 粗粒度：只想处理"有变化"的 chunk |
| `chunk.DidChange` | Chunk 内逐 component | **每种** component type 独立判断 | 精细：只重新计算变更的输入源 |
| `chunk.DidOrderChange` | Chunk 内 | entity 在 chunk 中的顺序是否变化 | entity 重排检测 |

```csharp
[BurstCompile]
public struct TagQueryJob : IJobChunk
{
    [ReadOnly] public ComponentTypeHandle<BAttribute> AttributeHandle;
    [ReadOnly] public ComponentTypeHandle<CTagMask> TagHandle;
    public ComponentTypeHandle<BTagCache> CacheHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                        bool useEnabledMask, in v128 chunkEnabledMask)
    {
        // 精细检测：只重新计算 Attribute 变更的 chunk
        bool attrChanged = chunk.DidChange(ref AttributeHandle, lastSystemVersion);
        bool tagChanged = chunk.DidChange(ref TagHandle, lastSystemVersion);

        if (!attrChanged && !tagChanged)
            return;  // 跳过整个 chunk，不处理

        var attributes = chunk.GetNativeArray(ref AttributeHandle);
        var tags = chunk.GetNativeArray(ref TagHandle);
        var cache = chunk.GetNativeArray(ref CacheHandle);

        for (int i = 0; i < chunk.Count; i++)
        {
            if (attrChanged)
                cache[i] = RecalculateCache(attributes[i], tags[i]);
        }
    }
}
```

## 注意事项
- `chunk.DidChange` 仍然是 chunk 级判断，不是 entity 级变更检测
- `DidChange` 返回 true 表示 chunk 中至少一个 entity 变更，不承诺哪个 entity
- 比 query 级 filter 更灵活（可逐 component type 判断），但仍不是 per-entity 级
- 需要 `lastSystemVersion`（从 SystemState 获取当前版本号）

## EX-GAS 适用点
- Tag Query / 目标扫描：使用 `chunk.DidChange` 做精细变更检测
- Effect 重评估跳过：只处理输入源有变更的 entity 组
- 属性脏标记检测：减少不必要的全量重计算
