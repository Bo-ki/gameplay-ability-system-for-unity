# CASE-04：Owner-local DynamicBuffer

**Primary Owner**：DynamicBuffer-Chunk-Archetype
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `components-buffer-introducing.md`、`components-buffer-set-capacity.md`
**关联规则**：BUF-01、BUF-03、BUF-04

## 使用场景
每个 entity 自身拥有同类型元素的可变集合，集合应随 entity 生命周期管理并通过 ECS query/job 访问。

## 模式描述
DynamicBuffer 在未超过内部容量时内联在 chunk；超过后外部化。ECS 管理其依赖和释放，但调用者仍需遵守读写依赖与结构变化后的引用失效。

```csharp
[InternalBufferCapacity(16)]
public struct BActiveModifier : IBufferElementData
{
    public int AttributeCode;
    public float Magnitude;
    public int SourceEffectCode;
}

DynamicBuffer<BActiveModifier> buffer =
    SystemAPI.GetBuffer<BActiveModifier>(entity);
buffer.Add(new BActiveModifier());
```

## 注意事项
- `16` 仅为示例，不是官方推荐值；按 BUF-04 基准选择。
- 外部化后不会因缩容自动迁回 chunk。
- 任何结构变化后重新获取具体 DynamicBuffer 引用。
- singleton DynamicBuffer 可用于单 writer/低并发数据；多 writer fan-in 按 BUF-02 设计。

## EX-GAS 适用点
ActiveEffect slot 和 per-owner delta/fact 候选；是否跨帧、是否每帧 Clear 必须在 store 生命周期中单独声明。
