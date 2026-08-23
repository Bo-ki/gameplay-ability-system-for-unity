# CASE-06: 高频状态切换用 EnabledRefRW

**Primary Owner**: Enableable-Component选型
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**: Unity 6000.3 / Entities 1.4.6
**官方来源**: `components-enableable-use.md`、`systems-systemapi-query.md`
**关联规则**: EN-01, EN-03

## 使用场景
在 IJobEntity 中需要高频切换 entity 的 enableable 状态，且访问模式为顺序遍历。

## 模式描述
使用 `EnabledRefRW<T>` 作为 IJobEntity 参数或 `SystemAPI.Query` 元素，直接访问当前迭代 entity 的启用状态。官方将其列为最高效的 enableable 操作方式，因为它利用可预测的线性访问模式；这不等于“零开销”。

```csharp
[BurstCompile]
public partial struct DisableFinishedAbilityJob : IJobEntity
{
    void Execute(EnabledRefRW<CAbilityActive> active, in CAbilityRuntimeState state)
    {
        if (state.RemainingTime <= 0)
            active.ValueRW = false;  // 不改变 archetype
    }
}
```

## 注意事项
- 可用于 IJobEntity 与 idiomatic `SystemAPI.Query` foreach
- 这里的 `T` 必须同时实现 `IComponentData` 与 `IEnableableComponent`；enableable buffer 使用 buffer/chunk/lookup 对应 API
- 读写在同一位置完成，无需额外的 ComponentLookup
- 写 enable bit 本身不产生结构变化；后续尊重 enabled 状态的同步 query 仍可能等待相关写 job

## EX-GAS 适用点
- ActiveEffectStore 中 duration 到期的 slot 禁用
- Ability cooldown 到期后的状态切换
- GrantedTag 的开关操作
