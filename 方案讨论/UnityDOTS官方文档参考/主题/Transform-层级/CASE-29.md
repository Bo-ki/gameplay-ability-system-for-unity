# CASE-29：Custom Transform via WriteGroup + ManualOverride

**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities 1.4.6
**Primary Owner**：Transform-层级
**来源**：`transforms-custom.md`、官方 `TransformsCustom.cs` 示例
**关联规则**：TRF-05

## 模式描述

```csharp
[WriteGroup(typeof(LocalToWorld))]
public struct CGridPosition : IComponentData
{
    public int2 Cell;
}

public sealed class GridUnitBaker : Baker<GridUnitAuthoring>
{
    public override void Bake(GridUnitAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.ManualOverride);
        AddComponent(entity, new CGridPosition { Cell = authoring.Cell });

        // ManualOverride 不会自动添加任何 transform component。
        AddComponent(entity, new LocalToWorld { Value = float4x4.identity });
    }
}

[UpdateInGroup(typeof(TransformSystemGroup))]
public partial struct GridToWorldSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (grid, localToWorld) in
                 SystemAPI.Query<RefRO<CGridPosition>, RefRW<LocalToWorld>>())
        {
            float3 position = new float3(
                grid.ValueRO.Cell.x * 2f,
                0f,
                grid.ValueRO.Cell.y * 2f);
            localToWorld.ValueRW.Value = float4x4.Translate(position);
        }
    }
}
```

## 注意事项

- `[WriteGroup(typeof(LocalToWorld))]` 让使用 `FilterWriteGroup` 的内置 transform query 排除这些 entity。
- `ManualOverride` 忽略同 GameObject 的其他 TransformUsageFlags，并阻止自动 transform 组件生成。
- 自定义层级仍需显式添加 `Parent` 并正确维护世界矩阵。
- 小幅标准变换直接使用 `LocalTransform` / `PostTransformMatrix`，不引入 custom transform。
