# CASE-32：TemporaryBakingType + Baking System Burst 计算

**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities 1.4.6
**Primary Owner**：Baking-BlobAsset
**来源**：`baking-baking-systems-overview.md`；官方 `BakingExamples.cs#TemporaryBakingType`
**关联规则**：BAKE-01、BAKE-03

## 模式描述

```csharp
[TemporaryBakingType]
public struct CRawModifierData : IComponentData
{
    public float Value;
}

public struct CProcessedModifier : IComponentData
{
    public float Value;
}

public sealed class GEBaker : Baker<GEAuthoring>
{
    public override void Bake(GEAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);

        // Baker 声明临时输入和最终输出，保证增量还原归属清晰。
        AddComponent(entity, new CRawModifierData { Value = authoring.Value });
        AddComponent(entity, new CProcessedModifier());
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
[BurstCompile]
public partial struct GEModifierBakingSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (raw, output) in
                 SystemAPI.Query<RefRO<CRawModifierData>, RefRW<CProcessedModifier>>())
        {
            output.ValueRW.Value = Process(raw.ValueRO.Value);
        }
    }
}
```

Baking System 不在这里 `AddComponent`，只写 Baker 已添加的 output。`TemporaryBakingType` 只出现在相关 baking pass，不进入 runtime 输出。
