# CASE-17: ICustomBootstrap 多世界

**Primary Owner**: System-World-SystemGroup
**来源**: `concepts-worlds.md`, `systems-update-order.html`
**关联规则**: SYS-05

## 使用场景
AutoChess 无头验收 Demo 需要隔离 World 运行，不依赖 Editor frame delta。通过 ICustomBootstrap 创建独立 World。

## 模式描述
```csharp
public class AutoChessHeadlessBootstrap : ICustomBootstrap
{
    public bool Initialize(string defaultWorldName)
    {
        // 创建无头 World，固定时间步长
        var world = new World("AutoChessHeadlessWorld", WorldFlags.Game);
        world.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
        // 添加 Core systems
        var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
        // 设置固定时间步长
        world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>().Timestep = 1.0f / 60f;
        ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
        return true; // 阻止默认 World
    }
}
```

## 注意事项
- 不隐式依赖 Editor `World.Time` 或 `VariableStepTime`
- 各 SystemGroup 通过 `[UpdateInGroup]` 分发到正确 World
- Core simulation hash 在无头/有头模式下必须一致（验证 SYS-04/GFX-04）

## EX-GAS 适用点
- AutoChess 无头批量验证
- Battle hash 确定性验证
- CI/CD 自动化测试管线
