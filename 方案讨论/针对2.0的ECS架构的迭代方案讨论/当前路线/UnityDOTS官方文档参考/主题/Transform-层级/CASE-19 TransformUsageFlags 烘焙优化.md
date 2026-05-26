# CASE-19: TransformUsageFlags 烘焙优化

**Primary Owner**: Transform-层级
**来源**: `transforms-usage-flags.md`
**关联规则**: TRF-04

## 使用场景
Baker 中使用 `TransformUsageFlags.Dynamic` / `Renderable` / `WorldSpace` / `ManualOverride` 控制 Baker 生成哪些 transform component，避免冗余。

## 模式描述
```csharp
public class UnitBaker : Baker<UnitAuthoring>
{
    public override void Bake(UnitAuthoring authoring)
    {
        // 静态建筑：只需渲染，不需要运行时修改 transform
        var staticEntity = GetEntity(TransformUsageFlags.Renderable);
        AddComponent(staticEntity, new CStaticDecoration { ... });

        // 动态单位：需要运行时移动、旋转
        var dynamicEntity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(dynamicEntity, new CUnitConfig { ... });

        // 纯逻辑 entity：无表现需求
        var logicEntity = GetEntity(TransformUsageFlags.None);
        AddComponent(logicEntity, new CAbilityConfig { ... });
    }
}
```

## 注意事项
- `Renderable` 只生成 `LocalToWorld`，适合静态装饰、建筑
- `Dynamic` 生成完整 transform hierarchy（`LocalTransform` + `Parent` + `LocalToWorld`）
- `None` 阻止 Baker 添加任何 transform component
- `Dynamic` 必须附带注释说明运行时修改 transform 的理由

## EX-GAS 适用点
- AutoChess Baking 优化
- 静态棋子/建筑用 `Renderable`
- 动态单位用 `Dynamic`
- 纯逻辑 ASC entity 用 `None`
