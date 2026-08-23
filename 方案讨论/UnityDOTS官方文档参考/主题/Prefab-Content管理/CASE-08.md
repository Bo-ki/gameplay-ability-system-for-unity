# CASE-08：EntityPrefabReference 请求加载后实例化

**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities 1.4.6
**Primary Owner**：Prefab-Content管理
**来源**：`baking-prefabs.md` > `Create and register an Entity prefab`、`Instantiate prefabs`
**关联规则**：CONTENT-01

## 使用场景

把 prefab 内容存入独立 entity scene 文件，避免在每个引用它的 SubScene 中重复序列化完整 prefab。

## 模式描述

```csharp
public struct CEntityPrefabReference : IComponentData
{
    public EntityPrefabReference Value;
}

public sealed class PrefabReferenceBaker : Baker<PrefabReferenceAuthoring>
{
    public override void Bake(PrefabReferenceAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new CEntityPrefabReference
        {
            Value = new EntityPrefabReference(authoring.Prefab)
        });
    }
}

// Runtime：先给请求实体添加 RequestEntityPrefabLoaded。
entityManager.AddComponentData(requestEntity, new RequestEntityPrefabLoaded
{
    Prefab = prefabReference
});

// PrefabLoadResult 出现后再实例化真正的 prefab root。
Entity instance = ecb.Instantiate(prefabLoadResult.PrefabRoot);
```

加载可能跨多个 update。消费后是否移除 `RequestEntityPrefabLoaded` / `PrefabLoadResult` 取决于是否继续保留加载请求；若像官方一次性示例那样移除两者，可避免重复实例化并释放请求关系。

## EX-GAS 适用点

适用于角色表现、VFX 等真实实体模板。纯配置定义仍按 CONTENT-01 使用 BlobAsset/generated table。
