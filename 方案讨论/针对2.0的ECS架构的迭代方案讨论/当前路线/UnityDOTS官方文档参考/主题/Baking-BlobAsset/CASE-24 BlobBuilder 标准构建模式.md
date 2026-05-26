# CASE-24: BlobBuilder 标准构建模式

**Primary Owner**: Baking-BlobAsset
**来源**: `blob-assets-create.md`
**关联规则**: BLOB-01, BLOB-02

## 使用场景
Definition 层 BlobAsset 构建：GE 定义、Ability 定义等静态数据。`ISystem` 在 `OnCreate` 中通过 `BlobBuilder` 创建 `BlobAssetReference<T>` 并存入 singleton component，`OnDestroy` 中 `Dispose()`。

## 模式描述
```csharp
// ISystem OnCreate 中一次性构建
public void OnCreate(ref SystemState state)
{
    var builder = new BlobBuilder(Allocator.Temp);
    ref var root = ref builder.ConstructRoot<GameplayEffectDefinitionBlob>();
    root.Duration = 5.0f;

    var modifiers = builder.Allocate(ref root.Modifiers, 3);
    modifiers[0] = new ModifierBlob { ... };

    var blobRef = builder.CreateBlobAssetReference<GameplayEffectDefinitionBlob>(Allocator.Persistent);
    builder.Dispose();

    state.EntityManager.AddComponentData(state.SystemHandle, new CEffectDefinitionRef { Value = blobRef });
}

// OnDestroy 中释放
public void OnDestroy(ref SystemState state)
{
    var refComp = SystemAPI.GetComponent<CEffectDefinitionRef>(state.SystemHandle);
    refComp.Value.Dispose();
}
```

## 注意事项
- `BlobArray` 通过 `Allocate` 返回的 `BlobBuilderArray` 逐元素初始化
- `BlobString` 通过 `AllocateString` 分配
- `BlobPtr` 通过 `SetPointer` 设置
- 禁止在 runtime hot path 中使用 BlobBuilder

## EX-GAS 适用点
- Definition 层 BlobAsset 构建
- GE 定义、Ability 定义等静态数据
- 仅在 Baking 期或初始化期执行
