# 最小完整代码骨架 Spec

## 结论

下面只固定 owner 关系，不固定具体 NativeContainer 或 Job 数量。

```csharp
/// <summary>
/// 固定步 GAS 物理执行域；只定义主 Physics 后的项目级更新位置。
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial class GasFixedTickSystemGroup : ComponentSystemGroup
{
}

/// <summary>
/// 单个 SimulationTick 的查询、临时内存、Job DAG 与最终依赖 owner。
/// </summary>
[UpdateInGroup(typeof(GasFixedTickSystemGroup))]
public partial struct GasTickKernelSystem : ISystem
{
    // OnUpdate 内密封输入、调度命名 Jobs，并只返回一个完整 dependency。
}

/// <summary>
/// 固定步 catch-up 后唯一复制并清理 ECS Boundary facts 的 managed owner。
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
public partial class GasBoundaryDrainSystem : SystemBase
{
}
```

独立 World bootstrap 必须显式创建 `FixedStepSimulationSystemGroup`、Physics、`GasFixedTickSystemGroup`、标准 Begin/End FixedStep ECB 和 Drain，并排序完整父链。任何 Demo/测试 runner 只调用 Session `TickBatch`，不持有上述子 System 引用。
