# CASE-44: Custom Section Metadata — 烘焙阶段向 Section Meta Entity 附加自定义元数据

**Primary Owner**: Baking-BlobAsset
**来源**: `streaming-meta-entities.md`
**关联规则**: BAKE-03

## 使用场景
Section meta entity 在场景内容加载**之前**就已可用。可在 Baking System（非 Baker）中通过 `SerializeUtility.GetSceneSectionEntity` 获取 section meta entity 并附加自定义 ECS component 作为元数据（如包围盒、PVS 信息、加载条件）。

## 模式描述
```csharp
[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
public partial struct SectionMetadataBakingSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (sectionData, entity) in
            SystemAPI.Query<SceneSectionData>().WithEntityAccess())
        {
            // 获取 section meta entity
            var sectionEntity = SerializeUtility.GetSceneSectionEntity(entity,
                state.EntityManager, sceneGUID);

            // 附加自定义元数据
            ecb.AddComponent(sectionEntity, new CStreamingBounds
            {
                Bounds = new AABB { Center = ..., Extents = ... }
            });
            ecb.AddComponent(sectionEntity, new CLoadCondition
            {
                Priority = sectionData.Priority,
                PreloadRadius = 50f
            });
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
```

## 注意事项
- 必须在 Baking System 中执行，不能在 Baker 中
- Section meta entity 在场景加载前即可访问
- Runtime 通过 `ResolvedSectionEntity` buffer 查询各 section meta entity

## EX-GAS 适用点
- AutoChess 大战场按空间分区流式加载
- Section meta entity 存储包围盒，runtime 根据玩家位置决定加载／卸载
