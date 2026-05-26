# CASE-03: IJobChunk 遍历

**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — IJobChunk 节、CASE-03
**关联规则**: QRY-01, QRY-03, JOB-01

## 使用场景
批量统计、enableable 过滤、非标准遍历、chunk 级条件跳过、optional component 判断、需要异常遍历顺序的场景。

## 模式描述
当需要 chunk 级跳过、optional component 判断、或非标准遍历顺序时使用 IJobChunk。

```csharp
[BurstCompile]
public struct EffectSpecChunkJob : IJobChunk
{
    [ReadOnly] public ComponentTypeHandle<CSpecRequest> SpecRequestHandle;
    public ComponentTypeHandle<BAttribute> AttributeHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                        bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var specs = chunk.GetNativeArray(ref SpecRequestHandle);
        var attrs = chunk.GetNativeArray(ref AttributeHandle);

        var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
        while (enumerator.NextEntityIndex(out var i))
        {
            attrs[i] = ApplySpec(attrs[i], specs[i]);
        }
    }
}
```

**IJobChunk 适用场景：**
- 不遍历 entity（如收集 chunk 统计信息）
- 多次遍历同一 chunk 的 entity
- 需要异常遍历顺序
- 需要 chunk 级条件跳过
- Optional component 使用 `chunk.Has(ref TypeHandle)` 判断

**IJobEntity vs IJobChunk 选择：**
- 大多数 per-entity 遍历应使用 IJobEntity（CASE-02）
- IJobEntity 底层生成 IJobChunk，自动享受未来 source gen 优化
- 简单 per-entity 变换用 IJobEntity，chunk 级操作用 IJobChunk

## 注意事项
- `ScheduleParallel` 并行度由 chunk 数量决定，chunk 太少时并行度不足
- 使用 `ChunkEntityEnumerator` 处理 enableable component 过滤
- 不忽略 enableable 时，for 循环可能处理已禁用的 entity

## EX-GAS 适用点
- EffectCommand 批处理（chunk 级效果分类）
- 批量统计和 chunk 信息收集
- 需要 optional component 分支判断的计算
- `chunk.DidChange` 精细变更检测（CASE-18）
