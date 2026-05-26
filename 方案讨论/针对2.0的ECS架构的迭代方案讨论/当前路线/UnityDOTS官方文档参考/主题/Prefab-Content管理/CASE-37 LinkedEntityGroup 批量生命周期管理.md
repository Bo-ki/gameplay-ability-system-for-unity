# CASE-37: LinkedEntityGroup 批量生命周期管理

**Primary Owner**: Prefab-Content管理
**来源**: `linked-entity-group.md`
**关联规则**: CONTENT-01

## 使用场景
root entity 持有 `DynamicBuffer<LinkedEntityGroup>`，`DestroyEntity(root)` 自动销毁 buffer 中所有关联 entity。

## 模式描述
```csharp
// 初始化 LinkedEntityGroup
var linkedGroup = ecb.AddBuffer<LinkedEntityGroup>(ascEntity);
linkedGroup.Add(new LinkedEntityGroup { Value = ascEntity });  // 第一个元素必须是根自身
linkedGroup.Add(new LinkedEntityGroup { Value = abilityEntityA });
linkedGroup.Add(new LinkedEntityGroup { Value = abilityEntityB });
linkedGroup.Add(new LinkedEntityGroup { Value = activeEffectEntity });

// 销毁根 entity → 自动销毁所有 linked entity
ecb.DestroyEntity(ascEntity);
// 等价于手动销毁：ascEntity + abilityEntityA + abilityEntityB + activeEffectEntity
```

## 注意事项
- 第一个元素必须是根 entity 自身
- 不与 Transform hierarchy 递归（独立概念）
- 不可嵌套（LinkedEntityGroup A 不包含 B 的内容）
- Instantiate/Destroy/SetEnabled 操作根 entity 自动递归到 buffer 中所有 entity

## EX-GAS 适用点
- ASC entity + granted ability/effect entity 统一生命周期
- 销毁 ASC → 自动清理所有关联 entity
