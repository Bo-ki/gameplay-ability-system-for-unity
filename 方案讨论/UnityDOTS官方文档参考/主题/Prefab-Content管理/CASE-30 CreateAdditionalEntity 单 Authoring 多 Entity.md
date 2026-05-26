# CASE-30: CreateAdditionalEntity 单 Authoring 多 Entity

**Primary Owner**: Prefab-Content管理
**来源**: 官方案例模式-高级
**关联规则**: BAKE-01, BAKE-02

## 使用场景
Baker 中使用 `CreateAdditionalEntity(TransformUsageFlags, entityName)` 从一个 authoring 产出多个 runtime entity。

## 模式描述
```csharp
public class AbilityDefinitionBaker : Baker<AbilityDefinitionAuthoring>
{
    public override void Bake(AbilityDefinitionAuthoring authoring)
    {
        var abilityEntity = GetEntity(TransformUsageFlags.None);
        AddComponent(abilityEntity, new CAbilityConfig { ... });

        // 为每个 GE 创建独立的 entity
        foreach (var geConfig in authoring.GameplayEffects)
        {
            var geEntity = CreateAdditionalEntity(TransformUsageFlags.None, "GE_Effect");
            AddComponent(geEntity, new CGameplayEffectConfig { ... });
        }

        // 为 Cue 创建独立的 entity
        var cueEntity = CreateAdditionalEntity(TransformUsageFlags.Dynamic, "Cue_VFX");
        AddComponent(cueEntity, new CCueConfig { ... });
    }
}
```

## 注意事项
- `CreateAdditionalEntity` 创建的 entity 在同一个 Baking 会话中
- 适用于 1:N 的 authoring → runtime entity 映射
- 简单 1:1 映射不需要此模式

## EX-GAS 适用点
- 一个 AbilityDefinition authoring → ability entity + 多个 GE entity + cue entity
- 复杂 Authoring 组件需要拆分到多个 runtime entity
