# CASE-19：TransformUsageFlags 声明最小运行时需求

**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities 1.4.6
**Primary Owner**：Transform-层级
**来源**：`transforms-usage-flags.md`
**关联规则**：TRF-04

## 模式描述

```csharp
public sealed class UnitBaker : Baker<UnitAuthoring>
{
    public override void Bake(UnitAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new CUnit());
    }
}

public sealed class AbilityDefinitionBaker : Baker<AbilityDefinitionAuthoring>
{
    public override void Bake(AbilityDefinitionAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new CAbilityDefinition());
    }
}
```

`GetEntity` 对同一 authoring GameObject 返回同一个 primary entity。不要在一个 `Bake` 中多次调用它并误以为得到 static/dynamic/logic 三个 entity；不同调用的 flags 会合并。需要额外 entity 时使用 `CreateAdditionalEntity`。

## 注意事项

- `None` 只表示本 Baker 无需求，其他 Baker 仍可添加 flags。
- `Renderable` 的实际组件取决于 hierarchy 与其他 flags，不应硬编码为“永远只有 LocalToWorld”。
- Entity Prefab 自动标记为 Dynamic。
- 最终结果用 Baking Preview/Entity Inspector 验证。
