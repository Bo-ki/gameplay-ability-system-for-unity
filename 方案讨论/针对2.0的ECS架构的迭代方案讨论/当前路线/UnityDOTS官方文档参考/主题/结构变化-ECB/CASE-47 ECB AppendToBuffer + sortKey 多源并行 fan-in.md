# CASE-47: ECB AppendToBuffer + sortKey 多源并行 fan-in

**Primary Owner**: 结构变化-ECB
**来源**: `components-buffer-command-buffer.md`
**关联规则**: PRF-13, PRF-25, CASE-35, ECB-02

## 使用场景

当多个并行 job（来自不同 system）需要向同一 entity 的 `DynamicBuffer` 追加元素，且需要保证最终 buffer 内容确定性有序时。

## 模式描述

多个并行 job 各自创建独立 ECB → 各自向同一 entity 的 buffer append 元素 → sortKey = `[ChunkIndexInQuery]` 保证集合内确定性顺序 → 各 ECB 独立 playback，不交错。

```csharp
// Phase 1: 各 system 独立 ECB，向同一 entity 追加
// System A: Attribute system
var ecbA = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
var jobA = new AttributeJob { ECB = ecbA }.Schedule(state.Dependency);

// System B: GE system
var ecbB = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
var jobB = new GEJob { ECB = ecbB }.Schedule(state.Dependency);

// Job 实现
[BurstCompile]
public partial struct AttributeJob : IJobEntity
{
    public EntityCommandBuffer ECB;
    public Entity TargetEntity;

    public void Execute([ChunkIndexInQuery] int sortKey, in CAttributeDelta delta)
    {
        // sortKey 保证本 job 内命令的确定性顺序
        ECB.AppendToBuffer(sortKey, TargetEntity, new CEffectCommand { ... });
    }
}

// Phase 2: ECB playback
// 各 ECB 独立 playback，各自按 sortKey 排序后追加
// 最终 buffer 内容 = jobA 的有序序列 + jobB 的有序序列
```

## 注意事项

- 必须先通过 `ECB.AddComponent<TBuffer>()` 确保目标 entity 的 buffer 组件已存在（ECB-02）
- 各 job 必须使用独立 ECB 实例，禁止复用（PRF-25）
- 如果只关心总顺序而非"各来源内部有序后再拼接"，可以用统一的 sortKey 域 + 同一 ECB
- 最终 buffer 的顺序是：（jobA 的有序序列）||（jobB 的有序序列）

## EX-GAS 适用点

- EffectCommand 从多个并行 job（Attribute system、GE system、Tag system）向同一 ASC entity 的 outbox buffer 追加输出
- 多效果并行评估的 ECB 写入
- TypedFact projection 的 fan-in 合并
