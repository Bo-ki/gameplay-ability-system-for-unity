# CASE-28：Chunk Component

**Primary Owner**：DynamicBuffer-Chunk-Archetype
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `components-chunk-introducing.md`、`components-chunk-use.md`

## 使用场景
数据确实属于当前物理 chunk，允许 entity 因结构变化/搬迁而与该值解除关联，并且系统希望按 chunk 读写一份元数据。

## 模式描述
Chunk component 每个 chunk 存一份。设置已有值不移动普通 entity；添加或移除 chunk component 会改变 archetype，是结构变化。

```csharp
/// <summary>
/// 记录当前 chunk 的批处理元数据。
/// </summary>
public struct ChunkProcessingState : IComponentData
{
    public int Phase;
}

entityManager.AddChunkComponentData<ChunkProcessingState>(chunk);
entityManager.SetChunkComponentData(
    chunk,
    new ChunkProcessingState { Phase = 1 });
```

## 注意事项
- 不用 chunk component 表达需要逐 entity 精确归属的 Alive/Dead、tag grant 等 gameplay state。
- 不使用 chunk identity/order 参与跨运行 battle hash。
- 不同值不会像 shared component 那样自动把 entity 重分组；添加/移除 component 类型仍会造成结构变化。

## EX-GAS 适用点
短生命周期的 per-chunk 处理元数据或统计缓存；使用前必须证明 chunk 重排后的语义仍正确。
