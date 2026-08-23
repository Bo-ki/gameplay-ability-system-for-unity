# CASE-12：NativeStream 并行 Fan-In 与确定性合并

**Primary Owner**：NativeContainer-Allocator
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Collections `2.6.6`
**官方来源**：Collections `parallel-readers.md`、`NativeStream.cs` API 注释
**关联规则**：NAT-02、NAT-03、MAT-05

## 使用场景
多个逻辑输入并行生成零到多个结果，需要避免共享 append 竞争，并在生产完成后合并。

## 模式描述
为每个稳定逻辑输入索引分配一个 NativeStream buffer，而不是把输入 index 写进只按 worker thread 数创建的 stream。生产、合并和释放通过 JobHandle 串联。

```csharp
/// <summary>
/// 每个输入索引独占一个 NativeStream buffer。
/// </summary>
[BurstCompile]
public struct ProduceCommandsJob : IJobFor
{
    [ReadOnly] public NativeArray<Input> Inputs;
    public NativeStream.Writer Writer;

    /// <summary>
    /// 生成当前输入对应的命令。
    /// </summary>
    public void Execute(int index)
    {
        Writer.BeginForEachIndex(index);
        Writer.Write(new EffectCommand
        {
            ProducerIndex = index,
            TargetStableId = Inputs[index].TargetStableId
        });
        Writer.EndForEachIndex();
    }
}

/// <summary>
/// 在生产完成后按固定 buffer index 合并命令。
/// </summary>
[BurstCompile]
public struct MergeCommandsJob : IJob
{
    [ReadOnly] public NativeStream Stream;
    public NativeList<EffectCommand> Output;

    /// <summary>
    /// 逐 buffer 读取全部项目并建立业务要求的最终顺序。
    /// </summary>
    public void Execute()
    {
        NativeStream.Reader reader = Stream.AsReader();
        for (int index = 0; index < reader.ForEachCount; index++)
        {
            int itemCount = reader.BeginForEachIndex(index);
            for (int item = 0; item < itemCount; item++)
            {
                Output.Add(reader.Read<EffectCommand>());
            }
            reader.EndForEachIndex();
        }

        // 仅当业务顺序不等同于稳定的 buffer/index 顺序时排序；
        // comparer 必须实现含 tie-breaker 的稳定全序。
        Output.Sort(new EffectCommandTotalOrderComparer());
    }
}
```

调度边界：

1. `inputCount == 0` 时直接跳过；否则 `stream = new NativeStream(inputCount, Allocator.TempJob)`；
2. Produce job 使用 `ScheduleParallel`；
3. Merge job 依赖 Produce handle；
4. 所有消费者依赖 Merge handle；
5. `stream.Dispose(lastConsumerHandle)`，不得提前 Dispose/Rewind。

## 注意事项
- `ForEachCount` 是构造的正数 buffer count，不随 worker 数波动。
- 每个 buffer 只能 Begin 一次，并由一个 writer/thread 写入。
- 所有写入完成后才能开始读取。
- NativeStream 可消除 append 调度顺序竞争，但跨运行业务顺序仍取决于稳定映射和 total-key/tie-breaker。
- NativeStream 使用链式块扩容；项目应监控总 item/byte 高水位，而不是声称存在官方“每 segment 固定容量上限”。

## EX-GAS 适用点
EffectCommand、SimulationFact、AttributeDelta 的并行生产；是否排序由其业务顺序和 battle hash 规则决定。
