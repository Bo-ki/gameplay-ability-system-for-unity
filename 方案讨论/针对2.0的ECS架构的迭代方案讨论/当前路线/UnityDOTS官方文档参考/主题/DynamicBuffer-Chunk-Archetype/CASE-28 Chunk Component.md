# CASE-28: Chunk Component

**Primary Owner**: DynamicBuffer-Chunk-Archetype
**来源**: DynamicBuffer-Chunk-Archetype.md / `components-chunk-use.md`
**关联规则**: BUF-01

## 使用场景
需要给整个 chunk 的 entity 附加一个共享标签或元数据，且该数据不随 entity 数量线性增长。

## 模式描述
Chunk Component 是每个 chunk 存储一份的 component，极低存储成本。修改不触发结构变化、不移动 entity、不翻倍 archetype 排列。

```csharp
public struct ChunkAllDead : IComponentData { }
```

```csharp
// 添加 Chunk Component
EntityManager.AddChunkComponentData<ChunkAllDead>(chunk);
```

**Chunk Component vs SharedComponent：**

| | Chunk Component | SharedComponent |
|---|---|---|
| 创建新 chunk | 手动添加时 | 每次值改变强制 entity 移动 |
| 存储 | 每个 chunk 一份 | 每个 chunk 一份 |
| 适用 | 同 chunk entity 的 "标签" | 分组 + 跨 chunk 相同值共享 |
| Chunk 分裂 | 不会 | 值改变时强制分裂 |

## 注意事项
- Chunk Component 的修改不触发结构变化
- 适用于同 chunk entity 的分组标记
- 不会因值改变导致 chunk 分裂

## EX-GAS 适用点
- Chunk 级状态标记（如 "chunk 内所有 entity 的 effect 已处理完毕"）
- Chunk 级管线阶段标记
- 批量处理场景中的 per-chunk 元数据
