# Prefab-Content 管理：API 与 EX-GAS 解读

## 结论

Entity Prefab 适合复用完整实体结构，BlobAsset 适合共享不可变数据，但 Unity 没有规定“一个 prefab 必然对应一个独立 archetype/独占 16 KiB chunk”。内存风险来自不同 archetype、shared-component 分区和低占用 chunk 的组合，必须用 Archetypes window/Profiler 实测。`UnityObjectRef<T>` 与 `WeakObjectReference<T>` 是两种不同的资产引用模型，也都不是“跨 World entity 引用”。

**适用版本**：Entities 1.4.6（项目安装版本）

## Entity Prefab

Baking 生成的 entity prefab root 带：

- `Prefab` tag：默认查询排除 prefab。
- `LinkedEntityGroup`：扁平记录 prefab hierarchy，支持整体 instantiate/destroy/enable。

实例化时 `Prefab` tag 会从副本移除。多个 prefab 只有在组件类型集合、shared component 分区等条件不同而落入不同 chunk 时才会增加碎片；仅凭“prefab 数量”不能推出 `N × 16 KiB`。

官方建议：

- 需要完整实体原型时使用 entity prefab。
- 需要在多个 SubScene 复用且避免把内容复制进每个 entity scene 时，使用 `EntityPrefabReference`。
- 共享、不可变、无需独立实体身份的数据可考虑 BlobAsset。
- 用 Archetypes window 检查 allocated/unused memory、archetype 和 chunk 数量。

来源：`baking-prefabs.md`、`linked-entity-group.md`、`performance-chunk-allocations.md`。

### EntityPrefabReference 加载流程

`EntityPrefabReference` 指向独立 entity scene 文件；它不是可直接传给 `Instantiate` 的 `Entity`。运行时必须先请求加载，再使用 `PrefabLoadResult.PrefabRoot`：

```csharp
// Baker：把 GameObject prefab 转成 EntityPrefabReference。
var prefabReference = new EntityPrefabReference(authoring.Prefab);
AddComponent(entity, new CPrefabReference { Value = prefabReference });

// Runtime：请求异步加载。
entityManager.AddComponentData(requestEntity, new RequestEntityPrefabLoaded
{
    Prefab = prefabReference
});

// PrefabLoadResult 出现后才能实例化。
var instance = ecb.Instantiate(prefabLoadResult.PrefabRoot);
```

来源：`baking-prefabs.md` > `Create and register an Entity prefab`、`Instantiate prefabs`。

## UnityObjectRef 与 WeakObjectReference

| 类型 | 引用/加载语义 | 生命周期 | 关键限制 |
|---|---|---|---|
| `UnityObjectRef<T>` | 在 unmanaged component 中保存 UnityEngine.Object 的直接引用（内部为 instance ID） | 随 entity scene/直接引用自动加载；引用会阻止 `Resources.UnloadUnusedAssets` 回收资产 | component 仍是 unmanaged，但读取 `Value` 得到 managed object，不可据此声称解引用可在 Burst job 中执行 |
| `WeakObjectReference<T>` | 包装 `UntypedWeakReferenceId` 的 content-archive 弱引用 | 调用者显式 `LoadAsync`，每次 Load 对应一次 `Release`；引用计数归零后 archive 才可卸载 | 使用前检查 loading status/结果；同步等待可能造成性能问题 |

两者都可用于资产引用，而不是 World 间的 `Entity` 引用。`Entity` 只在所属 World 的 entity store 中有意义；不能用这两个资产引用类型解决跨 World entity 身份问题。

来源：`reference-unity-objects.md`、`content-management-intro.md`、`content-management-get-a-weak-reference.md`、`content-management-load-an-object.md`，以及 `UnityObjectRef<T>` API remarks。

## Scene Section

SubScene 中的 `Entity` 字段只能引用：

- 同一 section 的 entity；
- section 0 的 entity。

指向其他非零 section 的引用加载时变为 `Entity.Null`。Entity prefab 实例带 `SceneSection` 时，卸载对应 section 会一起销毁实例；如果不希望绑定该生命周期，应在实例上移除 `SceneSection`。

来源：`streaming-scene-sections.md`。

## EX-GAS 项目策略

以下不是 Unity 官方硬规则：

1. Ability、GameplayEffect、Tag 等不可变定义优先使用 BlobAsset/generated table。
2. Entity Prefab 只用于确有实体身份、组件集合和实例生命周期的对象。
3. Prefab 数量不设置脱离数据的固定阈值；以 archetype/chunk/unused memory 预算和 Profile 结果裁决。
4. 直接常驻资源用 `UnityObjectRef<T>`；需要显式异步加载/卸载的 Presentation 资源用 `WeakObjectReference<T>`。
5. 跨 section 共享 ECS 实体放在 section 0 只是项目布局方案，需结合实际流式加载生命周期验证。

## 常见错误

1. 把 `EntityPrefabReference` 当成可直接实例化的 `Entity`。
2. 声称每个 prefab 天生拥有唯一 archetype 和专属 chunk。
3. 声称 `UnityObjectRef<T>` 内部使用 `WeakObjectReference<T>` 或不会阻止卸载。
4. 把 asset reference 描述为跨 World entity reference。
5. 忘记 `LinkedEntityGroup` 不递归且只能包含有效 entity。
6. 保存 `PostLoadCommandBuffer` 后立即 dispose 其中 ECB；官方流程要求把所有权交给 streaming system。

## 官方证据

| 官方文档 | 可裁决结论 |
|---|---|
| `baking-prefabs.md` | Prefab 组成、注册、EntityPrefabReference 加载和实例化 |
| `performance-chunk-allocations.md` | 16 KiB chunk、prefab fragmentation 的成立条件和测量方式 |
| `linked-entity-group.md` | 整组操作、首元素、有效 entity 与非递归语义 |
| `reference-unity-objects.md` | UnityObjectRef 直接引用 |
| `content-management-intro.md` | strong/weak content 生命周期差异 |
| `streaming-scene-sections.md` | section 引用和 prefab instance 生命周期 |
| `streaming-scene-instancing.md` | PostLoadCommandBuffer 与 ProcessAfterLoadGroup |
