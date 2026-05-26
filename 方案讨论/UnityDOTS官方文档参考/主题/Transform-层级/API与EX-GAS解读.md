# Transform-层级: API 与 EX-GAS 解读

## 核心概念

### ECS Transform 系统的分层架构

```
TransformSystemGroup
  ├── ParentSystem         ← 维护 Child/Parent 层级关系
  ├── LocalToWorldSystem   ← 计算 LocalToWorld 矩阵
  ├── ... 其他 transform 相关 system
  └── PostTransformSystem
```

**关键特征：**
- Transform 更新只发生在 `TransformSystemGroup` 运行期间
- `SimulationSystemGroup` 运行期间 `LocalToWorld` 可能过期
- `Child` / `PreviousParent` 由 `ParentSystem` 自动管理，应用代码只读

### LocalToWorld 的时效性陷阱

```csharp
// LocalToWorld 的值在 SimulationSystemGroup 运行期间可能过期或无效
// 原因 1：TransformSystemGroup 在 SimulationSystemGroup 之外的时机运行
// 原因 2：LocalToWorld 可能包含图形平滑目的的额外偏移

// 错误：在 SimulationSystemGroup 中直接读 LocalToWorld 做 gameplay 决策
float3 targetPos = localToWorld.Position;   // 可能过期或包含图形偏移

// 正确：需要精确世界坐标时使用 TransformHelpers.ComputeWorldTransformMatrix
var worldMatrix = TransformHelpers.ComputeWorldTransformMatrix(
    entity, ref localTransformLookup, ref parentLookup, ref postTransformLookup);
float3 targetPos = worldMatrix.Translation();
```

### Child / Parent 层级管理

层级关系通过 `Parent` component 建立，`Child`（`DynamicBuffer<Child>`）和 `PreviousParent` 由 `ParentSystem` 自动维护。

```csharp
// 正确：通过 Parent component 建立层级
SystemAPI.SetComponent(childEntity, new Parent { Value = parentEntity });

// 错误：直接修改 Child buffer
var children = SystemAPI.GetBuffer<Child>(parentEntity);
children.Add(new Child { Value = childEntity });  // 禁止！被 ParentSystem 覆盖
```

### TransformUsageFlags 烘焙优化

| Flag | 生成的 Component | 适用场景 |
|---|---|---|
| `Renderable` | `LocalToWorld` 仅 | 静态装饰、建筑 |
| `Dynamic` | `LocalTransform` + `Parent` + `LocalToWorld` | 动态移动的单位 |
| `WorldSpace` | `LocalTransform`（无 Parent） | UI、World-space 标记 |
| `ManualOverride` | 无 | 自定义 transform |
| `None` | 无 | 纯逻辑 entity |

---

## EX-GAS 项目解读

### AutoChess 的层级约束

AutoChess 中 Transform 的典型场景：

1. **棋子（动态单位）**：`TransformUsageFlags.Dynamic` — 需要运行时移动、旋转
2. **棋盘格子（静态装饰）**：`TransformUsageFlags.Renderable` — 只需渲染，不移动
3. **纯逻辑 ASC entity**：`TransformUsageFlags.None` — 无表现需求
4. **目标获取**：在 `SimulationSystemGroup` 中执行，必须使用 `ComputeWorldTransformMatrix`（TRF-01）

### Gameplay 坐标获取的规范路径

```csharp
partial struct TargetAcquisitionJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
    [ReadOnly] public ComponentLookup<Parent> ParentLookup;
    [ReadOnly] public ComponentLookup<PostTransformMatrix> PostTransformLookup;

    void Execute(in CUnitState unit, ref CUnitTarget target)
    {
        // 正确：在 SimulationSystemGroup 中使用 ComputeWorldTransformMatrix
        var worldMatrix = TransformHelpers.ComputeWorldTransformMatrix(
            unit.SelfEntity,
            ref LocalTransformLookup,
            ref ParentLookup,
            ref PostTransformLookup
        );
        target.WorldPosition = worldMatrix.Translation();
    }
}
```

---

## 常见陷阱

1. **"LocalToWorld 就是实体世界坐标"** — 在 `SimulationSystemGroup` 中可能过期或包含图形平滑偏移
2. **"我把这个 entity 加到 parent 的 Child buffer 就行"** — 直接修改会被下次覆盖，应通过 `Parent` component
3. **"children 的顺序就是 Baker 添加的顺序"** — ECS 不保证 `Child` buffer 迭代顺序
4. **"所有 entity 都用 Dynamic flag 省心"** — 静态 entity 用 `Dynamic` 浪费 24+ 字节 per entity
5. **"自定义 Transform 手动算好 LocalToWorld 就行"** — 标准 `LocalToWorldSystem` 会覆盖

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则编号 |
|---|---|---|
| `transforms-concepts.md` | `LocalToWorld` 在 `SimulationSystemGroup` 运行期间可能过期或无效 | TRF-01 |
| `transforms-using.md` | `Child` 和 `PreviousParent` 始终由 `ParentSystem` 管理 | TRF-02 |
| `transforms-comparison.md` | Child Buffer 迭代顺序不确定 | TRF-03 |
| `transforms-usage-flags.md` | TransformUsageFlags 控制 Baker 生成哪些 transform component | TRF-04 |
| `transforms-custom.md` / `TransformsCustom.cs` | WriteGroup + ManualOverride 自定义 transform | TRF-05 |
