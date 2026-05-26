# CONTENT-02: WeakObjectReference／UnityObjectRef 用于跨 World 资源引用

**严重度**: P1
**Primary Owner**: Prefab-Content管理
**来源**: `systems-data.md` (CASE-45 上下文)

## 规则声明
Managed Unity 对象引用必须使用 `UnityObjectRef<T>`（Burst-compatible）或 `WeakObjectReference`，不能使用常规 C# 引用字段。

## 为什么
ECS component 中的常规 C# object 引用会导致 archetype 包含托管 component，chunk 失去 Burst-compatible 特性且增加 GC 压力。`UnityObjectRef<T>` 是 Burst-compatible 的间接引用，在内部使用 `WeakObjectReference` 机制跟踪 Unity 对象生命周期，不会阻止对象被卸载。

## EX-GAS 诊断
所有跨 World 的资源引用（Material、Texture、AudioClip、GameObject Prefab）必须在 ECS component 中使用 `UnityObjectRef<T>`。非托管资源引用（如 entity、BlobAssetReference）使用原生类型。

## 检查方法
搜索 ECS component 中 `IComponentData` 或 `IBufferElementData` 的 `Object` 类型字段或泛型 object 引用。确认替换为 `UnityObjectRef<T>`。
