# InstantSpecBuild OwnerLocal Consumer 目标态归档

> 归档日期：2026-06-08
> 当前入口：`../../00-当前架构事实/P0-致命缺陷.md`、`../03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-FactSpec.md`、`../16-纯ECS内核与边界重划分/16-01-目标分层官方依据与不变量/16-01C-目标态消息流协议Spec.md`

## 目标态结论

Instant GE 的 command consumer 不应依赖 singleton command stream。ASC owner-local command/payload 已经是更接近目标态的 Core lane owner，spec build 应在 Core implementation 内直接声明 query、收集 owner-local command、完成 deterministic merge 和 spec sequence 分配。

本轮落地后，`OwnerLocalInstantCommandFlushSystem` 作为迁移期中间层退场；`GEEffectSpecBuildSystem` 直接消费 ASC owner-local instant command，并由 CodeGen 模板生成同样结构。

## 当前代码骨架

```csharp
[UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
[UpdateAfter(typeof(GEEffectCommandCatalogNormalizeSystem))]
public partial struct GEEffectSpecBuildSystem : ISystem
{
    private EntityQuery _ownerInstantCommandQuery;

    public void OnCreate(ref SystemState state)
    {
        _ownerInstantCommandQuery = state.GetEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadOnly<ASCIdentityComponent>(),
                ComponentType.ReadOnly<GEEffectCommandBuffer>(),
                ComponentType.ReadOnly<GESetByCallerValueBuffer>(),
            },
        });
    }

    public void OnUpdate(ref SystemState state)
    {
        // CollectOwnerLocalInstantSpecCommandsJob -> BuildOwnerLocalInstantSpecsJob
        // command input comes from ASC owner-local buffers, not the singleton stream.
    }
}
```

## 保留约束

1. Spec build 可以暂时写 singleton `GEEffectSpecBuffer`，因为下游 attribute reduce / cue projection 仍以 spec stream 为消费面。
2. Spec-local set-by-caller payload 可以暂时写 stream `GESetByCallerValueBuffer`，但只能作为 spec payload，不得重新作为 command flush carrier。
3. FramePrepare 必须在 singleton command buffer 为空时清掉 stream `GESetByCallerValueBuffer`，避免 spec-local payload 因不再经过 command compaction 而跨帧残留。
4. `SpecBuildCommandCursor` 不再是 instant command 输入 cursor；重新引入应视为 P0-D 回流。
5. 下一个目标态切片应迁移 spec carrier / spec payload，使 `GEEffectSpecBuffer` 退出 singleton stream owner。
