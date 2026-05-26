# CASE-39: Baker 只添加不读取 — Baker 间无依赖

**Primary Owner**: Baking-BlobAsset
**来源**: `baking-phases.md`
**关联规则**: BAKE-01

## 使用场景
Baker 内 `GetComponent<T>()`／`SetComponent<T>()` 不可用；只能 `AddComponent<T>()`。Baker 之间无依赖关系：Baker 只能向当前 entity **添加**新 component，不能读取已有 component 的值，也不能访问／修改其他 entity。

## 模式描述
```csharp
public class AbilityAuthoringBaker : Baker<AbilityAuthoring>
{
    public override void Bake(AbilityAuthoring authoring)
    {
        // 正确：只添加
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new CAbilityConfig { AbilityCode = authoring.AbilityCode });
        AddComponent(entity, new CAbilityCooldown { Duration = authoring.Cooldown });

        // 错误：不能 GetComponent/SetComponent
        // var existing = GetComponent<CAbilityConfig>(entity); // 禁止！
        // SetComponent(entity, new CAbilityConfig { ... });   // 禁止！
    }
}
```

## 注意事项
- Baker 间无依赖 —— 一个 Baker 的输出不能作为另一个 Baker 的输入
- 需要读取已有数据做复杂计算时，在 Baking System（`[WorldSystemFilter(BakingSystem)]` + `[BurstCompile]` ISystem）中用 Burst 完成（参考 CASE-32）
- 若在 Baker 中访问其他 entity 会导致未定义行为

## EX-GAS 适用点
- Ability/GE/Buff definition 的 Baker 只负责烘焙期数据搬运
- GE 的 modifier 计算逻辑放在 Baking System
