# CASE-31：Baker 的 DependsOn 在引用 Early-Out 之前

**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities 1.4.6
**Primary Owner**：Baking-BlobAsset
**来源**：`baking-baker-overview.md`；官方 `BakingExamples.cs#DependenciesBaker`
**关联规则**：BAKE-01、BAKE-02

## 模式描述

```csharp
public override void Bake(AbilityAuthoring authoring)
{
    DependsOn(authoring.EffectConfig);
    DependsOn(authoring.CueConfig);

    if (authoring.EffectConfig == null)
        return;

    Entity entity = GetEntity(TransformUsageFlags.None);
    AddComponent(entity, new CAbilityInput
    {
        EffectCode = authoring.EffectConfig.Code
    });
}
```

Unity 引用可能表现为 fake-null。先声明依赖，才能在对象恢复时重新触发 Baker。若只原样携带引用、没有读取引用对象的数据，是否需要 `DependsOn` 应按官方 dependency 语义判断，不机械添加。

本规则只适用于 Baker；Baking System 没有该 Authoring `DependsOn` API。
