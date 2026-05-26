# CASE-40: Baker 必须无状态 — 禁止在 Baker 实例中缓存数据

**Primary Owner**: Baking-BlobAsset
**来源**: `baking-baker-overview.md`
**关联规则**: BAKE-02

## 使用场景
Baker 是单例实例（每个 Baker 类型一个实例），其 `Bake()` 方法在非确定性顺序下被多次调用（增量烘焙跨长时间运行）。不能在字段中缓存状态。

## 模式描述
```csharp
// 错误：Baker 持有状态
public class WrongAbilityBaker : Baker<AbilityAuthoring>
{
    private int _bakeCount;           // 错误！实例状态
    private static int _totalBaked;   // 错误！static 跨会话残留
    private List<int> _cachedCodes;   // 错误！托管缓存

    public override void Bake(AbilityAuthoring authoring)
    {
        _bakeCount++;
        _totalBaked++;
        _cachedCodes.Add(authoring.AbilityCode);
        // ...
    }
}

// 正确：Baker 无状态，所有数据通过 Baker 方法产出
public class CorrectAbilityBaker : Baker<AbilityAuthoring>
{
    public override void Bake(AbilityAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new CAbilityConfig { AbilityCode = authoring.AbilityCode });
        AddComponent(entity, new CAbilityCooldown { Duration = authoring.Cooldown });
        // 复杂计算 → 通过 TemporaryBakingType 传递给 Baking System
    }
}
```

## 注意事项
- Baker 中缓存任何值违反不变式，导致烘焙行为异常
- 所有数据访问必须通过 Baker 方法（自动记录依赖和产出以支持增量烘焙的 undo/redo）
- static 字段同样禁止，会跨 Baking 会话残留

## EX-GAS 适用点
- GE/Ability definition Baker 的 `Bake()` 方法必须无状态
- 定义数据通过 `BlobBuilder` 或 component 产出
