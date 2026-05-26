# CASE-29: Custom Transform via WriteGroup + ManualOverride

**Primary Owner**: Transform-层级
**来源**: `transforms-custom.md` / `TransformsCustom.cs`
**关联规则**: TRF-05

## 使用场景
完全替代标准 transform 系统的自定义 transform。WriteGroup 使标准 `LocalToWorldSystem` 跳过持有自定义 transform 的 entity；`ManualOverride` 阻止 Baker 添加标准 transform component。

## 模式描述
```csharp
// 1. 自定义 transform component
[WriteGroup(typeof(LocalToWorld))]
public struct CGridPosition : IComponentData
{
    public int X;
    public int Y;
}

// 2. Baker 中使用 ManualOverride
public class GridUnitBaker : Baker<GridUnitAuthoring>
{
    public override void Bake(GridUnitAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.ManualOverride);
        AddComponent(entity, new CGridPosition { X = authoring.GridX, Y = authoring.GridY });
        // 不会生成 LocalTransform / Parent / LocalToWorld
    }
}

// 3. 自定义 transform system
[UpdateInGroup(typeof(TransformSystemGroup))]
public partial struct GridToWorldSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (gridPos, entity) in
            SystemAPI.Query<CGridPosition>().WithEntityAccess())
        {
            SystemAPI.SetComponent(entity, new LocalToWorld
            {
                Value = float4x4.Translate(new float3(gridPos.X * 2f, 0, gridPos.Y * 2f))
            });
        }
    }
}
```

## 注意事项
- WriteGroup 使标准 `LocalToWorldSystem` 跳过持有自定义 transform 的 entity
- `ManualOverride` 阻止 Baker 添加标准 transform component
- 适用于 2D 网格坐标、固定轴旋转、非标准空间变换
- 仅需微小调整的标准 transform 直接用 `LocalTransform` + `Parent`

## EX-GAS 适用点
- 2D 网格坐标
- 固定轴旋转
- 非标准空间变换
