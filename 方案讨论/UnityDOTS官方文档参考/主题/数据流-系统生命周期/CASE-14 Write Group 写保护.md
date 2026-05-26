# CASE-14: Write Group 写保护

**Primary Owner**: 数据流-系统生命周期
**来源**: `systems-writing-group.md`
**关联规则**: PRF-26

## 使用场景

当需要对某个 component 的写入进行拦截或覆盖，让特定 system 拥有独占写入权，其他 system 只能读取时。

## 模式描述

Write Group 允许通过 `[WriteGroup]` 属性标记一组 component，只有声明了相同 `[WriteGroup]` 的 system 才能写入。普通 system 遍历时，标记了 `[WriteGroup]` 的 component 不会被包含在读写访问中，除非 system 显式声明该 write group。

```csharp
// 定义基础 component 和 write group
[WriteGroup(typeof(CAttributeModifier))]
public struct CBaseAttribute : IComponentData
{
    public float Value;
}

public struct CAttributeModifier : IComponentData
{
    public float Delta;
}

// 只有声明 write group 的 system 可以写入 CBaseAttribute
[UpdateInGroup(typeof(GameplaySystemGroup))]
public partial struct SAttributeWriteGroup : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // 该 system 可以读写 CBaseAttribute
        // 其他未声明 write group 的 system 只能读取 CBaseAttribute
    }
}
```

## 注意事项

- Write Group 会增加 query 配置复杂度
- 多数情况下，通过 PRF-26（读写分离）即可满足需求，Write Group 是更严格的约束机制
- Write Group 在 ECS 1.0 中通过 `EntityQueryDesc` 声明

## EX-GAS 适用点

- 限制只有特定 system 能写入属性值，其他 system 通过 modifier 间接变更
