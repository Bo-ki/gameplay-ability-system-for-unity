# CASE-30：CreateAdditionalEntity 产出同一 Baker 管理的附加实体

**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities 1.4.6
**Primary Owner**：Prefab-Content管理
**来源**：`baking-baker-overview.md`；`baking-baking-systems-overview.md`；`IBaker.CreateAdditionalEntity` API
**关联规则**：BAKE-01、BAKE-02

## 使用场景

一个 authoring component 确实需要产出多个具有独立 runtime 身份的 entity。Baker 只能修改自己的 primary entity 和由自己创建的 additional entities。

## 模式描述

```csharp
public override void Bake(UnitAuthoring authoring)
{
    var root = GetEntity(TransformUsageFlags.Dynamic);
    AddComponent(root, new CUnit());

    foreach (var socket in authoring.RuntimeSockets)
    {
        Entity socketEntity = CreateAdditionalEntity(
            TransformUsageFlags.Dynamic,
            $"Socket_{socket.name}");
        AddComponent(socketEntity, new CSocket
        {
            Owner = root,
            Id = socket.Id
        });
    }
}
```

## 注意事项

- 多次 `GetEntity(...)` 仍返回该 authoring GameObject 的 primary entity，并会合并 TransformUsageFlags；它不会创建多个 entity。
- 只有 `CreateAdditionalEntity` 返回新的附加实体，并为 baking/live baking 配置归属和还原。
- 不要为了拆分纯静态 Ability/GE 配置而机械创建大量 entity；此类数据优先 BlobAsset。
