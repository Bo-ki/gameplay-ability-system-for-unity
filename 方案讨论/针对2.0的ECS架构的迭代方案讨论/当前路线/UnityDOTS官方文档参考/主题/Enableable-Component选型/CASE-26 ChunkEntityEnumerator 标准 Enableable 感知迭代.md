# CASE-26: ChunkEntityEnumerator 标准 Enableable 感知迭代

**Primary Owner**: Enableable-Component选型
**来源**: Enableable-Component选型.md / `iterating-data-ijobchunk.md`
**关联规则**: EN-01, EN-02

## 使用场景
在 IJobChunk 中遍历 entity 时，chunk 中存在 enableable component，需要跳过 disabled entity。

## 模式描述
使用 `ChunkEntityEnumerator` 自动跳过 disabled entity，避免使用简单 `for` 循环处理所有 entity（包括 disabled）。

```csharp
var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
while (enumerator.NextEntityIndex(out var i))
{
    // i 始终是 enabled entity 的索引
}
```

无 enableable 时可简单 `for` 并用 `Assert.IsFalse(useEnabledMask)` 断言。

## 注意事项
- 简单 for 循环会处理 disabled entity，结果错误
- 必须在确认有 enableable 时使用，否则增加开销
- 与 `EnabledMask` 配合使用

## EX-GAS 适用点
- 所有 IJobChunk 中涉及 enableable component 的迭代
- ActiveEffect 的批量处理
- Ability 状态遍历
