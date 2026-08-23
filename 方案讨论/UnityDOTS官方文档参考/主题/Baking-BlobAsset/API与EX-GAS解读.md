# Baking-BlobAsset：API 与 EX-GAS 解读

## 结论

Baker 可以读取 Authoring 数据，但必须通过 Baker API 建立增量依赖；它不能读取或修改已经烘焙到 entity 上的 ECS components，只能给自己的 primary/additional entities 添加新组件。Baking System 没有 Baker 的 `DependsOn` authoring API；正确链路是 Baker 跟踪 Authoring 依赖并产出临时/输出组件，Baking System 查询 ECS 数据并自行保证结构变化可还原。

**适用版本**：Entities 1.4.6（项目安装版本）

## Baker 的读取与写入边界

Baker 实例只创建一次，`Bake` 会以不确定顺序调用多次，因此不能在实例/static 字段缓存跨调用状态。

可读取：

- `authoring` 自身字段：自动成为依赖。
- 其他 GameObject/Component：使用 Baker 的 `GetComponent<T>` 等方法；这些方法会记录依赖，目标 component 缺失时也能记录其存在性依赖。
- 外部 asset/object 的内容：先调用 `DependsOn`；若对象可能是 Unity fake-null，在 early-out 前声明依赖。

不可读取/修改：

- 另一个 Baker 已写入的 ECS component。
- 其他 Baker 管理的 entity。
- 自己 entity 上的 ECS component 值再做 `Get/Set`。

Baker 只能向当前 authoring 的 primary entity，以及同一 Baker 用 `CreateAdditionalEntity` 创建的 entities 添加新 ECS components。

来源：`baking-baker-overview.md`、`baking-phases.md`。

```csharp
public override void Bake(AbilityAuthoring authoring)
{
    // 外部引用在 early-out 前建立依赖。
    DependsOn(authoring.Config);
    if (authoring.Config == null)
        return;

    // 这是 Baker.GetComponent：读取 UnityEngine authoring component，并自动跟踪依赖。
    Transform sourceTransform = GetComponent<Transform>();

    Entity entity = GetEntity(TransformUsageFlags.None);
    AddComponent(entity, new CRawAbility
    {
        Code = authoring.Config.Code,
        Position = sourceTransform.position
    });
}
```

## Baking System 的增量正确性

Baking System 在每个 baking pass 更新，适合批量 ECS 处理、Jobs 和 Burst。它不自动跟踪 authoring dependencies，也不自动撤销自己造成的结构变化。

必须区分两类“依赖”：

1. Authoring/asset 变更依赖：只能由 Baker 的访问 API/`DependsOn` 记录，再通过 Baking/TemporaryBakingType component 把数据交给 system。
2. ECS job dependency：像普通 system 一样通过 `state.Dependency` 等调度；它不是 Baker `DependsOn` 的替代品。

推荐 Baker 预先添加最终 output component，Baking System 只写其值：

```csharp
[TemporaryBakingType]
public struct CRawAbility : IComponentData
{
    public float Value;
}

public struct CProcessedAbility : IComponentData
{
    public float Value;
}

// Baker 同时声明临时输入和最终输出，Unity 可以自动还原 Baker 产出。
AddComponent(entity, new CRawAbility { Value = authoring.Value });
AddComponent(entity, new CProcessedAbility());

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
[BurstCompile]
public partial struct AbilityBakingSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (raw, output) in
                 SystemAPI.Query<RefRO<CRawAbility>, RefRW<CProcessedAbility>>())
        {
            output.ValueRW.Value = raw.ValueRO.Value * 2f;
        }
    }
}
```

若 Baking System 自己添加组件，必须同时处理“输入消失时移除旧输出”的逆向 query；否则 live/incremental baking 会残留。Baking System 创建的新 entity 不会进入 baked entity scene；持久输出 entity 必须由 Baker 创建。

来源：`baking-baking-systems-overview.md`、官方 `BakingExamples.cs` 的 `BakingSystem`、`TemporaryBakingType` 示例。

## BlobAsset 边界

- BlobAsset 只包含 unmanaged、immutable 数据。
- 含 `BlobArray` / `BlobString` / `BlobPtr` 的内容必须通过 `ref` 或 `BlobAssetReference<T>` 访问。
- Baker 创建后必须调用 `AddBlobAsset`（或 custom hash 版本）注册，供 `BlobAssetStore` 去重、引用计数和增量还原。
- Runtime 用 `CreateBlobAssetReference` 创建的 blob 由创建者手动 `Dispose`；从 entity scene 加载的 blob 由场景引用计数管理。
- `BlobBuilder` 适合 Baking/初始化，不在 per-frame hot path 构建。

来源：`blob-assets-concept.md`、`blob-assets-create.md`。

## EX-GAS 项目策略

1. Baker 只做 Authoring 读取、依赖声明和 ECS 输入/输出形状声明。
2. 可并行的批量派生计算放 Baking System；不要把“复杂”机械等同于必须拆 system。
3. Ability/GE/Tag 的 immutable definition 输出 BlobAsset/generated table。
4. 任何需要确定顺序的 Baking 输出使用稳定业务键和 tie-breaker；`ChunkIndexInQuery` 不能保证跨 full/incremental baking 布局稳定。
