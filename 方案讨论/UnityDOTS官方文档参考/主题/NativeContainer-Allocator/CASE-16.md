# CASE-16：System Group Allocator

**Primary Owner**：NativeContainer-Allocator
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities `1.4.6`；Collections `2.6.6`
**官方来源**：Entities `allocators-system-group.md`、`SystemGroupAllocatorExample.cs`
**关联规则**：NAT-01、NAT-04

## 使用场景
一个按 rate manager 更新的 `ComponentSystemGroup` 内需要共享只在该 group 更新窗口有效的 scratch allocation。

## 模式描述
在 system group 构造函数中调用 `SetRateManagerCreateAllocator(IRateManager)`；Entities 会为 group 创建并切换 double rewindable allocator。旧示例中的 `RateUtils.RateManagerCreateAllocator` 类型不存在。

```csharp
/// <summary>
/// 以固定步长更新并拥有 group allocator 的系统组。
/// </summary>
public partial class GasFixedStepGroup : ComponentSystemGroup
{
    /// <summary>
    /// 设置 fixed-rate manager，并让 Entities 创建 group allocator。
    /// </summary>
    public GasFixedStepGroup()
    {
        SetRateManagerCreateAllocator(new RateUtils.FixedRateSimpleManager(1f / 60f));
    }
}
```

## 注意事项
- 直接设置 `RateManager` 属性不会创建 system group allocator。
- 使用 group/update allocator 创建的 allocation 只能存活在官方定义的更新窗口；不得保存在跨更新 component/system 字段中。
- 业务代码通常不自行调用 `World.SetGroupAllocator`/`RestoreGroupAllocator`；自定义 `IRateManager` 才需要严格按官方模式成对切换。
- 采用前确认 group 确实需要独立 rate manager；不要只为获得 allocator 引入多余更新语义。

## EX-GAS 适用点
需要固定步长或可变 rate 更新的独立 phase。普通每帧 system 的临时分配优先使用 World update allocator，不额外创建 group。
