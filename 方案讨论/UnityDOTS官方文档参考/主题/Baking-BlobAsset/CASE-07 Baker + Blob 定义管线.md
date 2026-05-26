# CASE-07: Baker + Blob 定义管线

**Primary Owner**: Baking-BlobAsset
**来源**: 官方案例模式
**关联规则**: BAKE-01, BAKE-02, BLOB-01, BLOB-02

## 使用场景
Baker 从 authoring 读取配置数据，通过 `BlobBuilder` 创建 `BlobAssetReference<T>`，通过 `AddBlobAsset<T>` 向 Baker 注册后写入 entity component。适用于 Ability 定义、GE 定义、Tag 配置表等静态数据。

## 模式描述
```csharp
public class AbilityDefinitionBaker : Baker<AbilityDefinitionAuthoring>
{
    public override void Bake(AbilityDefinitionAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<AbilityDefinitionBlob>();
        root.AbilityCode = authoring.AbilityCode;
        root.CooldownDuration = authoring.Cooldown;

        var tags = builder.Allocate(ref root.GrantedTagCodes, authoring.TagCodes.Length);
        for (int i = 0; i < authoring.TagCodes.Length; i++)
            tags[i] = authoring.TagCodes[i];

        var blobRef = builder.CreateBlobAssetReference<AbilityDefinitionBlob>(Allocator.Persistent);
        builder.Dispose();

        AddBlobAsset(ref blobRef, out var hash);
        AddComponent(entity, new CAbilityConfigBlob { Definition = blobRef });
    }
}
```

## 注意事项
- 必须调用 `AddBlobAsset<T>()` 向 Baker 注册 blob，自动处理去重和引用计数
- `TryGetBlobAssetReference` 可在同一 Baker 会话中复用已注册的 blob
- Blob 是 immutable，创建后不可修改

## EX-GAS 适用点
- Ability 定义数据管线
- GameplayEffect 定义数据管线
- Attribute 元数据管线
- Tag 查找表管线
