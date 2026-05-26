# CASE-35: [ChunkIndexInQuery] sortKey 确定性 ECB 回放

**Primary Owner**: 结构变化-ECB
**来源**: `systems-entity-command-buffer-playback.md`
**关联规则**: PRF-13, CASE-47, PRF-25

## 使用场景

当并行 job 中录制 ECB 命令的顺序不确定（多线程竞争），但 playback 需要确定性顺序时（如 battle replay、hash 验证）。

## 模式描述

将 `[ChunkIndexInQuery] int sortKey` 作为 ECB 方法的第一个参数传递。playback 前按 sortKey 排序，大者后执行。

```csharp
[BurstCompile]
public partial struct EffectEvaluationJob : IJobEntity
{
    public EntityCommandBuffer ECB;

    public void Execute([ChunkIndexInQuery] int sortKey, Entity e, in CEffectRequest request)
    {
        // sortKey 作为第一个参数，保证 playback 确定性顺序
        ECB.AddComponent(sortKey, e, new CEffectEvaluated { ... });
    }
}
```

排序规则：
- sortKey 小的命令先执行
- sortKey 相同的命令按录制顺序执行（同一 thread 内确定）
- 多 thread 间 sortKey 不同时，按 sortKey 排序

## 注意事项

- sortKey 有微开销——`[ChunkIndexInQuery]` 在并行 job 中需要使用 `CalculateBaseEntityIndexArrayAsync`，不是完全免费的
- 仅在需要确定性 playback 时使用；不需要确定性时可以省略 sortKey 以获得更好性能
- 不同 job 使用独立 ECB 实例，避免 sortKey 域重叠导致的命令交错

## EX-GAS 适用点

- EffectCommand fan-in 中多个并行 job 向同一 buffer 追加输出
- TypedFact projection 的 reduce 阶段确定性 merge
- 确定性 replay 调试
