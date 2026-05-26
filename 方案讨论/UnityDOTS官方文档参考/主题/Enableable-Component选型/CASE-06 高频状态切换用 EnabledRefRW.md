# CASE-06: 高频状态切换用 EnabledRefRW

**Primary Owner**: Enableable-Component选型
**来源**: Enableable-Component选型.md / `components-enableable-use.html`
**关联规则**: EN-01, EN-03

## 使用场景
在 IJobEntity 中需要高频切换 entity 的 enableable 状态，且访问模式为顺序遍历。

## 模式描述
使用 `EnabledRefRW<T>` 作为 IJobEntity 参数，编译器自动将其编译为 chunk 内 enabled mask 的批量位操作，访问模式完全可预测，零额外开销。

```csharp
[BurstCompile]
public partial struct DisableFinishedAbilityJob : IJobEntity
{
    void Execute(EnabledRefRW<CAbilityActive> active, in CAbilityRuntimeState state)
    {
        if (state.RemainingTime <= 0)
            active.ValueRW = false;  // 无结构变化，无 sync point
    }
}
```

## 注意事项
- 仅在 IJobEntity 中可用
- 需要 component 实现了 `IEnableableComponent`
- 读写在同一位置完成，无需额外的 ComponentLookup

## EX-GAS 适用点
- ActiveEffectStore 中 duration 到期的 slot 禁用
- Ability cooldown 到期后的状态切换
- GrantedTag 的开关操作
