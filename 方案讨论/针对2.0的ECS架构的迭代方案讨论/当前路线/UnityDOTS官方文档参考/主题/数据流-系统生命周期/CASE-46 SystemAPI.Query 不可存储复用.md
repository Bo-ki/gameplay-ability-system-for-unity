# CASE-46: SystemAPI.Query 不可存储复用

**Primary Owner**: 数据流-系统生命周期
**来源**: `systems-systemapi-query.md`
**关联规则**: PRF-30

## 使用场景

当需要使用 `SystemAPI.Query<T>` 遍历 entity 数据时。

## 模式描述

`SystemAPI.Query<T>` 依赖 C# source generator 在编译时展开，返回的 `QueryEnumerable` 是 ref struct，无法存储为字段、无法跨方法传递、无法在 lambda 中捕获。必须在 `OnUpdate` 中直接使用内联 foreach。

```csharp
// 正确：内联 foreach
public void OnUpdate(ref SystemState state)
{
    foreach (var (health, config) in SystemAPI.Query<RefRW<CHealthCurrent>, RefRO<CHealthConfig>>())
    {
        // 直接处理
    }
}

// 错误：尝试存储查询
// SystemAPI.Query<...> cachedQuery;  // 编译错误：QueryEnumerable 是 ref struct

// 错误：尝试返回查询
// var query = SystemAPI.Query<...>();  // 无法存储到 var 之外
```

## 注意事项

- `SystemAPI.Query<T>` 是 source generator 驱动的语法糖，适合简单遍历
- 如果需要对查询进行更精细的控制（只读/读写分离、filter 配置、排序），应使用 `EntityQuery` + `state.GetEntityQuery`
- `SystemAPI.Query` 的每次调用都会重新评估，没有缓存
- 性能特征与 `EntityQuery` + 手动遍历相似，但语法更简洁

## EX-GAS 适用点

- 简单只读遍历（attribute 显示、状态检查）
- Debugger 中的数据展示查询
- 任何不需要精细控制查询配置的遍历场景
