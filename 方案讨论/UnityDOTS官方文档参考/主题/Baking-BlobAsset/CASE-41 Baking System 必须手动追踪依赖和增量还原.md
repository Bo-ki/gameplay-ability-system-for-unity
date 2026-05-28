# CASE-41: Baking System 必须手动追踪依赖和增量还原

**Primary Owner**: Baking-BlobAsset
**来源**: `baking-baking-systems-overview.md`
**关联规则**: BAKE-03

## 使用场景
Baking System 不自动追踪依赖和结构变化；需显式 `DependsOn()` 并在添加组件时手动追踪／撤销变更。Baking System 中创建的 entity **不会**出现在 baked entity scene 中（仅用于系统间数据传递）。

## 模式描述
```csharp
[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
public partial struct GEDefinitionBakingSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // 1. 显式 DependsOn 所有外部引用
        SystemAPI.TryGetSingleton<GEDefinitionBakingInput>(out var input);
        // 不需要对 singleton 调用 DependsOn，Baking System 需要手动管理

        // 2. 处理数据
        foreach (var (rawData, entity) in
            SystemAPI.Query<CRawModifierData>().WithEntityAccess())
        {
            var processed = ProcessModifier(rawData, input);
            state.EntityManager.AddComponent(entity, processed);
        }
    }

    // 增量还原：需要手动追踪添加的 component
    // 在 Baking System 中，通过回调或标记追踪已添加的组件
}
```

## 注意事项
- Baking System 不会自动记录依赖或产出
- 对所有外部数据引用显式调用 `DependsOn()`
- 当向 entity 添加 component 时手动追踪并实现撤销逻辑
- Baking System 中创建的 entity 不进入 baked scene
- 要输出到 scene 的 entity 必须在 Baker 中通过 `CreateAdditionalEntity` 创建

## EX-GAS 适用点
- 批量 GE 属性计算
- Definition 数据关联与过滤
- 跨 entity 数据聚合（modifier 批量处理）
