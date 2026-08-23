# CASE-43：PostLoadCommandBuffer + ProcessAfterLoadGroup 定制场景实例

**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities 1.4.6
**Primary Owner**：Prefab-Content管理
**来源**：`streaming-scene-instancing.md`
**关联规则**：CONTENT-01

## 使用场景

使用 `SceneLoadFlags.NewInstance` 多次加载同一 entity scene，并在每个 section 的 streaming world 中应用实例专属偏移或配置。

## 模式描述

```csharp
var loadParameters = new SceneSystem.LoadParameters
{
    Flags = SceneLoadFlags.NewInstance
};
Entity sceneEntity = SceneSystem.LoadSceneAsync(
    state.WorldUnmanaged,
    sceneReference,
    loadParameters);

var postLoadEcb = new EntityCommandBuffer(
    Allocator.Persistent,
    PlaybackPolicy.MultiPlayback);
Entity instanceData = postLoadEcb.CreateEntity();
postLoadEcb.AddComponent(instanceData, new PostLoadOffset
{
    Offset = sceneOffset
});

state.EntityManager.AddComponentData(sceneEntity, new PostLoadCommandBuffer
{
    CommandBuffer = postLoadEcb
});
// 不要在这里 Dispose：streaming system 接管并回放该 ECB。
```

`PostLoadCommandBuffer` 在 `ProcessAfterLoadGroup` 之前回放。后者运行在每个 section 的 streaming world，内容随后才移入 main world。

## 注意事项

- `PostLoadCommandBuffer` 是 managed component，包含普通 ECB。
- Scene meta entity 上的 buffer 应用到全部 sections；section meta entity 上的 buffer 只应用到该 section。
- 不要把 main-world `Entity` 值当作 streaming-world entity 传递；Entity 身份是 World-local。应传递偏移、ID 等可解释的值。
- Custom section metadata 存在 entity scene 文件中，因此同一 scene 的不同实例共享同一份烘焙 metadata。
