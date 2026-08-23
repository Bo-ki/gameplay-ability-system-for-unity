# CONTENT-02：按生命周期区分 UnityObjectRef 与 WeakObjectReference

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities 1.4.6
**严重度**：P0
**Primary Owner**：Prefab-Content管理
**来源**：`reference-unity-objects.md`、`content-management-intro.md`、`content-management-get-a-weak-reference.md`、`content-management-load-an-object.md`、`UnityObjectRef<T>` API remarks

## 规则声明

- 常驻、直接引用的 UnityEngine.Object 使用 `UnityObjectRef<T>`，使 ECS component 保持 unmanaged。
- 需要显式异步加载/释放的 content-archive 资源使用 `WeakObjectReference<T>`，每次 `LoadAsync` 必须对应一次 `Release`。
- 两者不得表述为跨 World 的 `Entity` 引用方案。

## 关键区别

`UnityObjectRef<T>` 存储 object instance ID，具有直接/强引用语义；官方 API 明确说明该引用会阻止 `Resources.UnloadUnusedAssets` 回收对应资产。其存储类型是 unmanaged，但 `Value`/隐式转换返回 managed UnityEngine.Object，不能在 Burst job 中解引用。

`WeakObjectReference<T>` 包装 `UntypedWeakReferenceId`；对象未加载时引用仍有效，调用者负责加载状态和引用计数。

## 检查方法

审查每个资产字段的加载/释放需求；搜索对 `UnityObjectRef<T>` 的“weak、不会阻止卸载、Burst 中取 Value”描述，以及没有成对 Release 的 WeakObjectReference 加载。
