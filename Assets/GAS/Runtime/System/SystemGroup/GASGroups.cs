using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 命令处理组：清理上一 tick 事件流，然后消费 Facade / Ability / GE request。
    /// 当前 tick 产生的事实事件必须留给后续 Tag/Effect/Attribute/Ability/Cue 组读取。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class GASCommandGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// ExecutionCalculation 扩展插槽：自定义 ECS system 在这里写 BExecutionCalculationValue。
    /// 统一 output modifier consumer 会在本组之后消费输出值。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SExecutionCalculation))]
    [UpdateBefore(typeof(SExecutionCalculationOutputModifier))]
    public partial class GASExecutionCalculationExtensionGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 重置脏标记组：重置属性 Dirty、Tag 变更标记等。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GASCommandGroup))]
    public partial class GASResetDirtyGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// Tag 计算组：Tag 变化传播，如 GE 授权/移除 Tag。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GASResetDirtyGroup))]
    public partial class GASTagGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// GE 计算组：Duration Tick, Period 触发, Stacking 处理。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GASTagGroup))]
    public partial class GASEffectGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 属性计算组：Modifier → CurrentValue 重算。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GASEffectGroup))]
    public partial class GASAttributeGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// Ability 逻辑组：Activate, Tick, End。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GASAttributeGroup))]
    public partial class GASAbilityGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 表现层组：消费当前 tick 的 ECS 事实事件，驱动 Cue / GameObject / UI 边界。
    /// Simulation runtime 不应反向依赖本组的托管表现对象。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GASAbilityGroup))]
    public partial class GASCueGroup : ComponentSystemGroup
    {
    }
}
