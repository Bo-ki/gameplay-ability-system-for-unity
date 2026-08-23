# CASE-41：Baking System 显式维护增量还原

**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities 1.4.6
**Primary Owner**：Baking-BlobAsset
**来源**：`baking-baking-systems-overview.md`；官方 `BakingExamples.cs#BakingSystem`
**关联规则**：BAKE-03

## 正确模式

- Authoring 依赖由 Baker 记录，并通过 ECS baking components 输入。
- Baker 能预先添加 output 时，Baking System 只修改值。
- 必须由 system 添加 tag/component 时，同时实现逆向清理：

```csharp
var addQuery = SystemAPI.QueryBuilder()
    .WithAll<CInput>()
    .WithNone<COutputTag>()
    .Build();
state.EntityManager.AddComponent<COutputTag>(addQuery);

var removeQuery = SystemAPI.QueryBuilder()
    .WithAll<COutputTag>()
    .WithNone<CInput>()
    .Build();
state.EntityManager.RemoveComponent<COutputTag>(removeQuery);
```

Baking System 不应调用 Baker `DependsOn`；两者不是同一 API/生命周期。System 新建 entity 只适合 baking systems 之间传递数据，不会进入 baked entity scene。
