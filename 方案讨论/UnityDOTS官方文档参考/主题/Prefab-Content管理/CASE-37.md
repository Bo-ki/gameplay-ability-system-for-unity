# CASE-37：LinkedEntityGroup 批量生命周期管理

**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities 1.4.6
**Primary Owner**：Prefab-Content管理
**来源**：`linked-entity-group.md`
**关联规则**：CONTENT-01

## 使用场景

Root entity 的 `DynamicBuffer<LinkedEntityGroup>` 把多个 entity 绑定到 instantiate、destroy 和 `SetEnabled` 操作。

## 规则与模式

```csharp
var linked = ecb.AddBuffer<LinkedEntityGroup>(root);
linked.Add(new LinkedEntityGroup { Value = root }); // 首元素必须是 root 自身。
linked.Add(new LinkedEntityGroup { Value = abilityEntity });
linked.Add(new LinkedEntityGroup { Value = effectEntity });

ecb.DestroyEntity(root); // 整组销毁。
```

## 必须满足

- 首元素始终是 root 自身。
- Buffer 只能包含有效 entity；若单独销毁成员，必须先把它从 group 移除。
- LinkedEntityGroup 不递归处理嵌套 group，应避免嵌套。
- 用 EntityQuery 销毁时，group 内要么全部匹配、要么全部不匹配。
- 它与 Transform hierarchy 相互独立；加入 `Child` 不会自动加入 linked group。
- Scene 中的 linked members 还必须具有正确 `SceneTag`，否则卸载 query 可能只匹配 group 的一部分。
