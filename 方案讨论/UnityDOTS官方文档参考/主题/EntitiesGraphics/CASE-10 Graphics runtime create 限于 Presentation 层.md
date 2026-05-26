# CASE-10: Graphics runtime create 限于 Presentation 层

**Primary Owner**: EntitiesGraphics
**来源**: 官方案例模式
**关联规则**: GFX-01, GFX-02

## 使用场景
`RenderMeshUtility.AddComponents` 用于 Presentation 层真实渲染创建，Core 不得直接添加 graphics component。

## 模式描述
```csharp
// 正确：在 Presentation 层使用 RenderMeshUtility.AddComponents
public partial struct VFXPresentationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (vfxRequest, entity) in
            SystemAPI.Query<CVFXRequest>().WithEntityAccess())
        {
            var renderEntity = ecb.CreateEntity();
            RenderMeshUtility.AddComponents(renderEntity, ecb,
                new RenderMeshDescription(vfxRequest.Mesh, vfxRequest.Material));

            ecb.AddComponent(renderEntity, new LocalTransform
            {
                Position = vfxRequest.WorldPosition,
                Rotation = quaternion.identity,
                Scale = 1f
            });
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

// 错误：Core simulation 中直接操作 graphics component
// RenderMesh 操作应通过 Presentation Outbox + BindingSystem
```

## 注意事项
- 只应在 Presentation/Boundary 层使用
- Core simulation 通过 Presentation Outbox 传递表现请求
- 无头模式验证表现正确性无需真实渲染

## EX-GAS 适用点
- VFX/SFX entity 创建
- UI element entity 创建
- PresentationBindingSystem 中处理 Outbox buffer
