# Transform 层级

## 职责

本领域维护 ECS Transform 系统在 EX-GAS 中的正确使用边界：层级关系管理（Child/Parent）、世界坐标获取时效性（LocalToWorld）、烘焙优化（TransformUsageFlags）、自定义变换（WriteGroup）。不覆盖标准 transform 操作（LocalTransform 的 Position/Rotation/Scale 读写——这些按常规 ECS component 读写即可）。

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
// ParentSystem 在下次更新时自动维护 Child buffer 和 PreviousParent

// 错误：直接修改 Child buffer
var children = SystemAPI.GetBuffer<Child>(parentEntity);
children.Add(new Child { Value = childEntity });  // 禁止！被 ParentSystem 覆盖
```

### TransformUsageFlags 烘焙优化

Baker 中通过 `TransformUsageFlags` 控制烘焙期生成哪些 transform component：

| Flag | 生成的 Component | 适用场景 |
|---|---|---|
| `Renderable` | `LocalToWorld` 仅 | 静态装饰、建筑 |
| `Dynamic` | `LocalTransform` + `Parent` + `LocalToWorld` | 动态移动的单位 |
| `WorldSpace` | `LocalTransform`（无 Parent） | UI、World-space 标记 |
| `ManualOverride` | 无（阻止 Baker 添加任何 transform component） | 自定义 transform |
| `None` | 无 | 纯逻辑 entity（无表现需求） |

## 编写规范

### TRF-01: SimulationSystemGroup 中禁止直接读 LocalToWorld 做 gameplay 决策

**声明：** 在 `SimulationSystemGroup` 运行期间（即所有 Runtime Core system 的执行期），禁止直接读取 `LocalToWorld` 的 `.Position` / `.Rotation` 用于 gameplay 决策（目标获取、距离判定、范围检测等）。必须使用 `TransformHelpers.ComputeWorldTransformMatrix` 递归计算精确世界矩阵。

- **来源：** `transforms-concepts.md` — "The `LocalToWorld` component value might be out of date or invalid while the `SimulationSystemGroup` is running"
- **为什么：** Transform 更新只在 `TransformSystemGroup` 运行期间发生。`SimulationSystemGroup` 中的 `LocalToWorld` 可能包含上一帧数据或图形平滑偏移。基于过期坐标的 gameplay 决策（如技能范围判定）产生错误结果
- **EX-GAS 诊断：** Debugger 标记所有 `SimulationSystemGroup` 内 system 对 `LocalToWorld` 的直接 `Position` / `Rotation` 读取；Grep 工具应能检测此类违规
- **检查方法：** Grep `LocalToWorld` 在 `Assets/GAS/Runtime/` 下的 `.Position` / `.Rotation` 读取；判断所在 system group；若是 `SimulationSystemGroup` sub-group 则标记

### TRF-02: 禁止直接修改 Child / PreviousParent

**声明：** `Child`（`DynamicBuffer<Child>`）和 `PreviousParent` component 始终由 `ParentSystem` 内部管理。应用代码禁止直接添加、移除或修改这两个组件的值。修改层级关系只能通过设置 `Parent` component 的值完成。

- **来源：** `transforms-using.md` — "The `Child` and `PreviousParent` components are always managed by the `ParentSystem`. Application code should never directly add, remove, or change the values of these components."
- **为什么：** 1) 直接修改 `Child` buffer -> `ParentSystem` 下次更新时覆盖为正确值，修改被静默丢弃；2) 修改 `Parent` 后到下一次 `ParentSystem` 更新前层级关系处于不一致状态；3) 手动操作可能破坏 `Child` / `PreviousParent` 的内部一致性
- **EX-GAS 诊断：** Grep `Buffer<Child>` / `DynamicBuffer<Child>` 上的 `Add` / `Remove` / `Insert` / `Clear` 调用；`PreviousParent` 的 `SetComponent` / `AddComponent`
- **检查方法：** 搜索 `Buffer<Child>` 的 Add/Remove/Clear 操作在应用代码中；搜索 `PreviousParent` 的直接写入

### TRF-03: Child Buffer 迭代顺序不确定，禁止依赖 sibling index

**声明：** `DynamicBuffer<Child>` 中 children 的迭代顺序在不同 scene 加载、不同 platform、不同 ECS 版本下不确定。禁止依赖 sibling index（buffer 中的位置）用于确定性 gameplay 逻辑（如"第一个 child 是主手武器"）。

- **来源：** `transforms-comparison.md` — Child Buffer 迭代顺序不确定
- **为什么：** ECS 不保证 `Child` buffer 的存储顺序与 Baker 中添加顺序一致，scene deserialization 可能重排。依赖 sibling index 做确定性排序在 replay 中不可复现
- **EX-GAS 诊断：** 搜索以 `children[0]` / `children[i]` 等索引方式访问 `Child` buffer 的模式，要求插入明确排序逻辑
- **检查方法：** 搜索 `Buffer<Child>` 或 `DynamicBuffer<Child>` 的索引访问 `[`；若用于确定性输出则违规

### TRF-04: 使用正确 TransformUsageFlags 避免冗余 transform component

**声明：** Baker 中必须根据实体的运行时需求选择最小 `TransformUsageFlags`。纯逻辑 entity（无表现、无 world-space 坐标需求）使用 `None`；静态表现 entity 使用 `Renderable`；仅在需要运行时修改 transform 时使用 `Dynamic`。

- **来源：** `transforms-usage-flags.md` (CASE-19) — TransformUsageFlags 控制 Baker 生成哪些 transform component
- **为什么：** `Dynamic` 生成完整 transform hierarchy（`LocalTransform` + `Parent` + `LocalToWorld`），每多一个 component 增加 chunk 内存占用和 archetype 排列数。静态 entity 使用 `Renderable` 只生成 `LocalToWorld`，减少 2/3 的 transform component 存储
- **EX-GAS 诊断：** 检查每个 Baker 的 `TransformUsageFlags` 声明；`Dynamic` 必须附带注释说明运行时修改 transform 的理由
- **检查方法：** 审计所有 Baker 文件中 `bakingType` / `GetEntity` / `CreateAdditionalEntity` 的 TransformUsageFlags 参数

### TRF-05: Custom Transform 使用 WriteGroup + ManualOverride

**声明：** 当实体需要完全替代标准 transform 系统（如 2D 网格坐标、固定轴旋转、非标准空间变换）时，通过 `[WriteGroup(typeof(LocalToWorld))]` 声明自定义 transform component，并在 Baker 中使用 `TransformUsageFlags.ManualOverride` 阻止标准 transform component 生成。

- **来源：** `transforms-custom.md` / `TransformsCustom.cs` (CASE-29) — WriteGroup + ManualOverride 自定义 transform
- **为什么：** WriteGroup 使标准 `LocalToWorldSystem` 跳过持有自定义 transform 的 entity；`ManualOverride` 阻止 Baker 添加标准 transform component。两者结合确保自定义变换不被标准系统覆盖
- **EX-GAS 诊断：** 自定义 transform 必须有对应的 `[WriteGroup]` 声明；Baker 中 `TransformUsageFlags.ManualOverride`
- **检查方法：** 搜索 `[WriteGroup(typeof(LocalToWorld))]` 确认与 `ManualOverride` 配对。检查自定义 transform system 是否在 `TransformSystemGroup` 中正确排序

## EX-GAS 项目解读

### AutoChess 的层级约束

AutoChess 中 Transform 的典型场景：

1. **棋子（动态单位）**：`TransformUsageFlags.Dynamic` — 需要运行时移动、旋转
2. **棋盘格子（静态装饰）**：`TransformUsageFlags.Renderable` — 只需渲染，不移动
3. **纯逻辑 ASC entity**：`TransformUsageFlags.None` — 无表现需求
4. **目标获取**：在 `SimulationSystemGroup` 中执行，必须使用 `ComputeWorldTransformMatrix`（TRF-01）

### Gameplay 坐标获取的规范路径

```csharp
// 目标获取 system —— 在 SimulationSystemGroup 中运行
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

## 常见陷阱

1. **"LocalToWorld 就是实体世界坐标"** — 在 `SimulationSystemGroup` 中可能过期或包含图形平滑偏移。需要精确 gameplay 坐标时必须用 `ComputeWorldTransformMatrix` 递归计算
2. **"我把这个 entity 加到 parent 的 Child buffer 就行"** — `Child` buffer 由 `ParentSystem` 自动管理，直接修改会被下次更新覆盖。应通过设置 `Parent` component 建立层级
3. **"children 的顺序就是 Baker 添加的顺序"** — ECS 不保证 `Child` buffer 迭代顺序。依赖 sibling index 的确定性逻辑在 replay 中不可复现
4. **"所有 entity 都用 Dynamic flag 省心"** — 静态 entity 用 `Dynamic` 生成不必要的 `LocalTransform` + `Parent`，浪费 24+ 字节 per entity。纯逻辑 entity 用 `None` 避免任何 transform 负担
5. **"自定义 Transform 手动算好 LocalToWorld 就行"** — 标准 `LocalToWorldSystem` 会覆盖自定义计算。必须用 `WriteGroup` 注册自定义 component 并使用 `ManualOverride`

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则编号 |
|---|---|---|
| `transforms-concepts.md` | `LocalToWorld` 在 `SimulationSystemGroup` 运行期间可能过期或无效；可能包含图形平滑偏移 | TRF-01 |
| `transforms-using.md` | `Child` 和 `PreviousParent` 始终由 `ParentSystem` 管理，应用代码禁止直接修改 | TRF-02 |
| `transforms-comparison.md` | Child Buffer 迭代顺序不确定；`PostTransformMatrix` 非均匀缩放 | TRF-03 |
| `transforms-usage-flags.md` | TransformUsageFlags 控制 Baker 生成哪些 transform component（CASE-19） | TRF-04 |
| `transforms-custom.md` / `TransformsCustom.cs` | WriteGroup + ManualOverride 自定义 transform 系统（CASE-29） | TRF-05 |

## 验收指标

1. Runtime Core 中 `SimulationSystemGroup` 路径零直接 `LocalToWorld.Position` / `.Rotation` 读取
2. 应用代码中零 `Buffer<Child>` / `DynamicBuffer<Child>` 的 Add/Remove/Set 操作
3. 无依赖 `Child` buffer index 确定性的 gameplay 逻辑
4. 所有 Baker 的 `TransformUsageFlags` 通过 Code Review 确认最小化
5. 自定义 transform 路径均有 `[WriteGroup]` + `ManualOverride` 配对
