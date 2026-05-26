# CASE-31: DependsOn() 在 Early-Out 之前

**Primary Owner**: Baking-BlobAsset
**来源**: 官方案例模式-高级
**关联规则**: BAKE-03

## 使用场景
确保外部引用恢复时 Baker 被重新触发。即使引用为 null，`DependsOn` 也建立跟踪依赖。

## 模式描述
```csharp
public override void Bake(AbilityAuthoring authoring)
{
    // 正确：DependsOn 必须在所有 early-out 之前调用
    DependsOn(authoring.EffectConfig);
    DependsOn(authoring.CueConfig);

    // 然后再做 early-out 检查
    if (authoring.EffectConfig == null)
        return;

    // 正常 baking 逻辑
    var entity = GetEntity(TransformUsageFlags.Dynamic);
    // ...
}
```

## 注意事项
- `DependsOn(authoring.Reference)` 必须在所有 `if (ref == null) return` 之前调用
- 违反此模式：当 external reference 为 null 时 `DependsOn` 未执行，之后引用被赋值时 Baker 不会重新触发
- 适用于 Baker 有外部依赖的所有场景

## EX-GAS 适用点
- Ability Baker 中 DependsOn 外部配置引用
- GE Baker 中 DependsOn 外部 modifier 配置
