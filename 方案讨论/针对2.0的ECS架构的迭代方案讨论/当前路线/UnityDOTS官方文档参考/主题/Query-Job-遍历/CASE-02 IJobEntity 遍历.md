# CASE-02: IJobEntity 遍历

**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — IJobEntity 节、CASE-02
**关联规则**: QRY-01, JOB-01, JOB-04, PRF-05

## 使用场景
Runtime Core hot path 主力。用于普通 per-entity 计算、简单 per-entity 变换、stateless 计算。当需要 chunk 级条件跳过时改用 IJobChunk。

## 模式描述
IJobEntity 是 per-entity 遍历的首选。只需定义 `Execute` 方法，source generator 自动生成 IJobChunk 实现。

```csharp
[BurstCompile]
public partial struct AttributeDeltaApplyJob : IJobEntity
{
    [ReadOnly] public NativeHashMap<int, float> DeltaMap;

    public void Execute(ref BAttribute attribute, in CAttributeDeltaConsumer consumer)
    {
        if (DeltaMap.TryGetValue(consumer.AttributeCode, out var delta))
            attribute.CurrentValue += delta;
    }
}

// 调度
var job = new AttributeDeltaApplyJob { DeltaMap = deltaMap };
state.Dependency = job.ScheduleParallel(state.Dependency);
```

**IJobEntity 的 query 定制属性：**

```csharp
[WithAll(typeof(BTargetable))]
[WithNone(typeof(CDead))]
[WithChangeFilter(typeof(BAttribute))]
[BurstCompile]
public partial struct MyJob : IJobEntity
{
    public void Execute(ref BAttribute attr) { ... }
}
```

## 注意事项
- IJobEntity 底层生成 IJobChunk，自动享受未来 source gen 优化
- 需要 `IJobEntityChunkBeginEnd` 接口做 chunk 级前后处理
- Execute 参数必须精确使用 `ref`/`in` 修饰符
- IJobEntity 不验证 Execute 参数与 EntityQuery 的匹配性，需手动交叉验证

## EX-GAS 适用点
- Attribute Delta 归并（`AttributeDeltaApplyJob`）
- ActiveEffectStore 周期 tick（配合 enableable 标记只遍历 active slot）
- 各类 per-entity 变换计算
