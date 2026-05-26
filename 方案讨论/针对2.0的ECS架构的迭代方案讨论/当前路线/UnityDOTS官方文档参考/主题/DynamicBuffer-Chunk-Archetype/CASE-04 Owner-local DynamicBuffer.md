# CASE-04: Owner-local DynamicBuffer

**Primary Owner**: DynamicBuffer-Chunk-Archetype
**来源**: DynamicBuffer-Chunk-Archetype.md / `components-buffer-introducing.html`
**关联规则**: BUF-01, BUF-04

## 使用场景
Entity 自身需要持有同类型元素的可变集合，且集合数据应随 entity 生命周期管理。

## 模式描述
每个 entity 持有一个 DynamicBuffer 作为其 owner-local 可变集合。数据内联存储在 chunk 中（前提是不溢出），ECS 安全系统原生管理读写依赖，无 NativeContainer 的 job 调度限制。

```csharp
[InternalBufferCapacity(16)]
public struct BActiveModifier : IBufferElementData
{
    public int AttributeCode;
    public float Magnitude;
    public int SourceEffectCode;
}

var buffer = SystemAPI.GetBuffer<BActiveModifier>(entity);
buffer.Add(new BActiveModifier { ... });
buffer.RemoveAt(0);
```

## 注意事项
- 必须设定合理的 `InternalBufferCapacity`，默认 128 字节的容量容易溢出
- 溢出后数据永不自动迁回 chunk
- 结构变化后 buffer handle 失效，必须重新获取

## EX-GAS 适用点
- `BActiveEffectSlot` 作为 ASC entity 上的 DynamicBuffer
- `BAttributeDelta` 作为 per-target DynamicBuffer（帧内 clear）
- `BTypedFact` 作为 singleton DynamicBuffer
