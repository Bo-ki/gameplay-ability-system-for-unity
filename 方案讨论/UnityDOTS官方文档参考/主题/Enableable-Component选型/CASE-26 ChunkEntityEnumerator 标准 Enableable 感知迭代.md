# CASE-26: ChunkEntityEnumerator 标准 Enableable 感知迭代

**Primary Owner**: Enableable-Component选型
**来源**: Enableable-Component选型.md / `iterating-data-ijobchunk.md`
**关联规则**: EN-01, EN-02

## 使用场景
在 IJobChunk 中遍历 entity 时，chunk 中存在 enableable component，需要跳过 disabled entity。

## 模式描述
当 `useEnabledMask == true` 时，使用 `ChunkEntityEnumerator` 自动跳过 disabled entity，避免使用简单 `for` 循环处理所有 entity（包括 disabled）。当确认没有 enableable filter 时，使用普通 `for` 循环并断言 `useEnabledMask == false`，这是官方 `iterating-data-ijobchunk-implement.md` 允许的快路径。

```csharp
if (!useEnabledMask)
{
    for (var i = 0; i < chunk.Count; i++)
        ExecuteEntity(i);
    return;
}

var enumerator = new ChunkEntityEnumerator(true, chunkEnabledMask, chunk.Count);
while (enumerator.NextEntityIndex(out var i))
{
    // i 始终是 enabled entity 的索引
    ExecuteEntity(i);
}
```

若该 query 设计上永远不含 enableable component，也可直接 `Assert.IsFalse(useEnabledMask)` 后使用简单 `for`。

## 注意事项
- 在 `useEnabledMask == true` 时，简单 for 循环会处理 disabled entity，结果错误
- 在 `useEnabledMask == false` 时，普通 for 循环是更直接的快路径；不要无条件为所有 chunk 构造 enumerator
- 与 `EnabledMask` 配合使用

## EX-GAS 适用点
- 所有 IJobChunk 中涉及 enableable component 的迭代
- ActiveEffect 的批量处理
- Ability 状态遍历
