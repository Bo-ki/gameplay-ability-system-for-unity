# CASE-12: NativeStream 并行 Fan-In + Deterministic Merge

**Primary Owner**: NativeContainer-Allocator
**来源**: NativeContainer-Allocator.md / DocCodeSamples
**关联规则**: NAT-02, NAT-03

## 使用场景
大量实体并行写入同一数据管道，且输出需要确定性顺序（影响 battle hash）。

## 模式描述
使用 NativeStream 实现无锁并行 fan-in。每个 worker thread 写自己的 segment，无锁无竞争。Merge 阶段按固定 key 排序保证确定性。

```csharp
// 1. 创建 stream
var stream = new NativeStream(threadCount, Allocator.TempJob);

// 2. 并行写入
[BurstCompile]
public struct FanInJob : IJobFor
{
    public NativeStream.Writer StreamWriter;
    public void Execute(int index)
    {
        StreamWriter.BeginForEachIndex(index);
        StreamWriter.Write(new EffectCommand { ... });
        StreamWriter.EndForEachIndex(index);
    }
}

// 3. 确定性 merge
var reader = stream.AsReader();
// collect + sort by fixed key → dispatch
allCommands.Sort(new EffectCommandComparer());  // 按 target ASC id 排序
for (int i = 0; i < allCommands.Length; i++) { /* dispatch */ }
```

## 注意事项
- forEachCount 遍历顺序不可控，必须排序
- ParallelWriter 写入顺序取决于线程调度
- 内存预算需根据最大 entity 数量预先估算

## EX-GAS 适用点
- EffectCommand 并行收集阶段
- SimulationFact 并行生产
- AttributeDelta 并行计算
