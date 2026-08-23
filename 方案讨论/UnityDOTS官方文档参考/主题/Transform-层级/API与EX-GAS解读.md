# Transform-层级：API 与 EX-GAS 解读

## 结论

`LocalToWorld` 在 Simulation 阶段可以作为允许延迟时的快速近似，但不能用于要求当前精确值的 gameplay 计算。精确层级世界矩阵使用 `TransformHelpers.ComputeWorldTransformMatrix`；无 parent 的根实体可直接使用最新 `LocalTransform`。TransformUsageFlags 是多个 Baker 合并后的“需求声明”，不能按单个 flag 机械推导固定组件集合。

**适用版本**：Entities 1.4.6 / Unity.Transforms（项目安装版本）

## LocalToWorld 的时效性

默认 `LocalToWorldSystem` 在 `TransformSystemGroup` 中更新 `LocalToWorld`。在 `SimulationSystemGroup` 运行期间，它可能仍是上一轮 transform 更新的值，也可能包含图形平滑偏移。

官方给出的选择是：

1. 延迟可接受：直接读取 `LocalToWorld`，把它当快速近似。
2. 无 parent 且只需要当前 position/rotation/uniform scale：读取刚写入的 `LocalTransform`。
3. 层级内且要求当前精确世界矩阵：调用 `ComputeWorldTransformMatrix`。

```csharp
ComponentLookup<LocalTransform> localTransformLookup;
ComponentLookup<Parent> parentLookup;
ComponentLookup<PostTransformMatrix> postTransformLookup;

// 每次 OnUpdate 调度/调用前更新缓存 lookup。
localTransformLookup.Update(ref state);
parentLookup.Update(ref state);
postTransformLookup.Update(ref state);

TransformHelpers.ComputeWorldTransformMatrix(
    entity,
    out float4x4 worldMatrix,
    ref localTransformLookup,
    ref parentLookup,
    ref postTransformLookup);
```

该 helper 会沿层级遍历，官方示例明确提示其成本较高，应只在需要精确值的路径使用。

来源：`transforms-concepts.md` > `The LocalToWorld component`；`transforms-helpers.md` > `ComputeWorldTransformMatrix`。

## Parent / Child 层级

应用代码通过 child entity 的 `Parent` 声明关系。`ParentSystem` 自动维护：

- parent 上的 `DynamicBuffer<Child>`；
- child 上的 `PreviousParent`。

应用代码不能直接添加、移除或修改 `Child` / `PreviousParent`。修改 `Parent` 后，直到下一次 `ParentSystem` 更新前，`Child` buffer 仍可能反映旧关系。

`Child` 的顺序任意，没有 GameObject sibling index 语义。需要业务顺序时，另存稳定排序键。

来源：`transforms-concepts.md`、`transforms-using.md`、`transforms-comparison.md`。

## TransformUsageFlags

Flags 会在同一 GameObject 的所有 Bakers 之间合并：

| Flag | 准确语义 |
|---|---|
| `None` | 当前 Baker 没有特定 transform 需求；不能阻止其他 Baker 请求组件 |
| `Renderable` | 需要渲染所需 transform，但不要求运行时移动 |
| `Dynamic` | 需要运行时移动所需 transform |
| `WorldSpace` | 即使 authoring parent 是 Dynamic，也要求 runtime 保持 world-space |
| `NonUniformScale` | 需要表示非均匀缩放 |
| `ManualOverride` | 忽略同 GameObject 其他 Baker 的 flags，并且不自动添加任何 transform component |

实际组件集合取决于合并后的 flags、authoring hierarchy、static 状态等。例如静态 Renderable 层级通常可烘焙为 world-space `LocalToWorld`；跟随 Dynamic parent 的 Renderable child 仍可能得到 `LocalTransform`、`Parent`、`LocalToWorld`。Entity Prefab 自动标记为 Dynamic。

来源：`transforms-usage-flags.md`。

## Custom Transform

`ManualOverride` 不会自动添加 `LocalToWorld`。自定义方案必须显式添加它，并用带 `[WriteGroup(typeof(LocalToWorld))]` 的自定义 component 让内置 `LocalToWorldSystem` 排除这些 entity：

```csharp
[WriteGroup(typeof(LocalToWorld))]
public struct CGridTransform : IComponentData
{
    public int2 Cell;
}

public override void Bake(GridAuthoring authoring)
{
    Entity entity = GetEntity(TransformUsageFlags.ManualOverride);
    AddComponent(entity, new CGridTransform { Cell = authoring.Cell });
    AddComponent(entity, new LocalToWorld { Value = float4x4.identity });
}
```

如果自定义层级仍需要 `Parent`，也必须由 Baker 显式添加。仅做小幅标准变换时不要引入整套 custom transform。

来源：`transforms-custom.md`、官方 `TransformsCustom.cs` 示例。

## EX-GAS 项目策略

1. Battle Core 优先使用网格/逻辑坐标作为权威状态，不让表现平滑偏移进入规则裁决。
2. 目标获取若只需根 entity 当前坐标，直接读 `LocalTransform`；只有层级精确世界值才使用 helper。
3. 允许延迟的 UI/VFX 可直接读 `LocalToWorld`。
4. 每个 Baker 声明满足功能所需的最小 flag，但最终组件集合以 Baking Preview/Entity Inspector 为准，不以静态表猜测。

## 常见错误

- 把 `ComputeWorldTransformMatrix` 写成返回 `float4x4`；1.4.6 签名通过 `out` 返回。
- 在一个 Baker 中多次 `GetEntity` 误以为创建了多个 entity；实际返回同一 primary entity并合并 flags。
- 认为 `TransformUsageFlags.None` 能禁止其他 Bakers 添加 transform。
- 使用 `ManualOverride` 后忘记显式添加 `LocalToWorld`。
- 把所有 Simulation 阶段的 `LocalToWorld` 读取都判成错误，忽略官方允许的近似用途。
