# CASE-35: [ChunkIndexInQuery] sortKey 确定性 ECB 回放

**Primary Owner**: 结构变化-ECB
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `systems-entity-command-buffer-use.md`、`systems-entity-command-buffer-playback.md`、`iterating-data-ijobentity.md`
**关联规则**: PRF-13, CASE-47, PRF-25

## 使用场景

当并行 job 中录制 ECB 命令的顺序受调度影响，而 playback 需要在同一 World/query 布局下获得调度无关的顺序时。跨构建 battle replay/hash 还需要稳定业务键，不能只依赖 chunk 顺序。

## 模式描述

将 `[ChunkIndexInQuery] int sortKey` 作为 ECB 方法的第一个参数传递。playback 前按 sortKey 排序，大者后执行。

```csharp
[BurstCompile]
public partial struct EffectEvaluationJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;

    public void Execute([ChunkIndexInQuery] int sortKey, Entity e, in CEffectRequest request)
    {
        // sortKey 作为第一个参数，保证 playback 确定性顺序
        ECB.AddComponent(sortKey, e, new CEffectEvaluated { ... });
    }
}
```

排序规则：
- sortKey 小的命令先执行
- sortKey 相同的命令保留录制序；跨线程共享同一 key 时录制序本身可能不确定
- 多 thread 间 sortKey 不同时，按 sortKey 排序

## 注意事项

- `ChunkIndexInQuery` 是官方推荐的高效 ECB sort key，不需要 `CalculateBaseEntityIndexArrayAsync`；该额外准备步骤属于 `EntityIndexInQuery`
- `ParallelWriter` 的 ECB 命令必须传 sort key；选择能与调度无关且尽量减少排序工作量的 key
- 不同 job 使用独立 ECB 实例，避免 sortKey 域重叠导致的命令交错
- 若确定性要求跨不同 World 构建、不同 archetype/chunk 布局或不同平台，先输出稳定业务 ID + tie-breaker，再做显式排序/归并

## EX-GAS 适用点

- EffectCommand fan-in 中多个并行 job 向同一 buffer 追加输出
- TypedFact projection 的 reduce 阶段确定性 merge
- 确定性 replay 调试
