# CASE-23：在 IJobChunk 中使用 BufferAccessor

**Primary Owner**：DynamicBuffer-Chunk-Archetype
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `iterating-data-ijobchunk.md`、`components-buffer-jobs.md`
**关联规则**：BUF-03

## 使用场景
`IJobChunk` 需要按 chunk 批量访问每个 entity 的 DynamicBuffer。

## 模式描述
system 缓存 `BufferTypeHandle<T>`，每次调度前 `.Update(ref state)`；job 在 `Execute` 中通过 `GetBufferAccessorRO/RW` 获取 accessor。

```csharp
BufferAccessor<BMyData> buffers =
    chunk.GetBufferAccessorRO(ref bufferTypeHandle);

for (int index = 0; index < chunk.Count; index++)
{
    DynamicBuffer<BMyData> buffer = buffers[index];
    // 读取当前 entity 的 buffer。
}
```

## 注意事项
- RO/RW 必须匹配真实访问，避免不必要的写依赖。
- `BufferLookup<T>` 用于按 Entity 随机访问，不是 BufferAccessor 的同义词。
- job 执行期间不能进行结构变化；结构变化后旧 DynamicBuffer 引用失效。

## EX-GAS 适用点
ActiveEffect、Attribute 聚合等按 chunk 遍历 owner-local buffer 的系统。
