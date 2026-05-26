# CASE-23: BufferAccessor in IJobChunk

**Primary Owner**: DynamicBuffer-Chunk-Archetype
**来源**: DynamicBuffer-Chunk-Archetype.md / `iterating-data-ijobchunk.md`
**关联规则**: BUF-01, BUF-03

## 使用场景
在 IJobChunk 中需要批量访问 chunk 内所有 entity 的 DynamicBuffer。

## 模式描述
使用 `chunk.GetBufferAccessorRO<T>()` 或 `chunk.GetBufferAccessorRW<T>()` 获取 BufferAccessor，按 chunk 内 index 访问每个 entity 的 buffer。

```csharp
var accessor = chunk.GetBufferAccessorRO<BMyData>(ref bufferHandle);
for (int i = 0; i < chunk.Count; i++)
{
    var buffer = accessor[i];
    // 操作该 entity 的 buffer
}
```

`BufferTypeHandle<T>` 专用于 IJobChunk 的 chunk 级批量访问，与 `BufferLookup<T>`（随机访问）不同。

## 注意事项
- 需要对应 component 的 `BufferTypeHandle`，在 `OnUpdate` 中通过 `state.GetBufferTypeHandle<T>()` 获取
- chunk 内按 index 顺序访问，cache locality 良好
- 不适合随机 entity 访问

## EX-GAS 适用点
- ActiveEffectStore 的批量处理
- Attribute 聚合计算的批量 buffer 访问
- Effect 状态同步
