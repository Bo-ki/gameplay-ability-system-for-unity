# CASE-32: TemporaryBakingType + Baking System Burst 计算

**Primary Owner**: Baking-BlobAsset
**来源**: 官方案例模式-高级
**关联规则**: BAKE-01, BAKE-03

## 使用场景
Baker 写入 `[TemporaryBakingType]` 临时数据 → `[WorldSystemFilter(BakingSystem)]` + `[BurstCompile]` ISystem 做 Burst 计算 → 写入持久化 component。适用于需要跨 entity 聚合或复杂计算的场景（如 modifier 计算、定义关联）。

## 模式描述
```csharp
// 1. Baker 写入临时数据
[TemporaryBakingType]
public struct CRawModifierData : IComponentData
{
    public float Value;
    public int OperationType;
}

public class GEBaker : Baker<GEAuthoring>
{
    public override void Bake(GEAuthoring authoring)
    {
        DependsOn(authoring.ModifierConfig);
        if (authoring.ModifierConfig == null) return;

        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new CRawModifierData
        {
            Value = authoring.ModifierConfig.Value,
            OperationType = (int)authoring.ModifierConfig.Operation
        });
    }
}

// 2. Baking System 做 Burst 计算
[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
[BurstCompile]
public partial struct GEModifierBakingSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (raw, entity) in
            SystemAPI.Query<CRawModifierData>().WithEntityAccess())
        {
            // 复杂计算：modifier 解析、条件过滤、定义关联
            var processed = ProcessModifier(raw);

            // 写入持久化 component
            state.EntityManager.AddComponent(entity, processed);
        }
    }
}
```

## 注意事项
- Temporary 数据在当前 bake pass 结束后自动清除
- Baking System 必须手动管理依赖和增量还原
- Baker 只做轻量数据搬运，复杂计算放入 Baking System

## EX-GAS 适用点
- GE modifier 解析（GE Baker → RawModifierData → Baking System → ProcessedModifier）
- Ability 定义关联处理
- 跨 entity 数据聚合场景
