# System / World / SystemGroup：API 与 EX-GAS 解读

> 精确基线：Unity 6000.3 / Entities 1.4.6
> 边界：先陈述官方机制，再给 EX-GAS v1 项目裁决

## 1. 官方机制

### 1.1 World 与 System

- World 是 EntityManager、System 与 ECS 数据的生命周期边界。
- `ISystem` 是 unmanaged system 形态；`SystemBase` 支持托管字段与托管边界，但不因此自动适合热点 Core。
- System 的数据访问声明与 `JobHandle` 依赖共同决定安全调度；调度 Job 的 system 必须把最终依赖交还框架。
- System 有固定 update/调度成本；官方性能文档说明不必要的 system 拆分可能增加开销，但没有规定“项目最多几个 System”。

官方快照：

- [World 概念](../../官方文档原件/com.unity.entities/Documentation~/concepts-worlds.md)
- [System 概念](../../官方文档原件/com.unity.entities/Documentation~/concepts-systems.md)
- [ISystem](../../官方文档原件/com.unity.entities/Documentation~/systems-isystem.md)
- [SystemBase](../../官方文档原件/com.unity.entities/Documentation~/systems-systembase.md)
- [System 优化](../../官方文档原件/com.unity.entities/Documentation~/systems-optimizing.md)

### 1.2 SystemGroup 与更新顺序

- `UpdateInGroup` 把 System/Group 放进父组；`UpdateBefore`/`UpdateAfter` 表达同组内约束。
- 属性是排序约束，不替代数据 `JobHandle`。
- `FixedStepSimulationSystemGroup` 使用 fixed-rate manager；一次外层更新可能执行零次或多次 fixed update。
- 通过 `SetRateManagerCreateAllocator` 设置 rate manager 的组可建立自己的 double rewindable allocator，并在组更新期间替换当前 world update allocator。

官方快照：

- [System 更新顺序](../../官方文档原件/com.unity.entities/Documentation~/systems-update-order.md)
- [System 时间](../../官方文档原件/com.unity.entities/Documentation~/systems-time.md)
- [System group allocator](../../官方文档原件/com.unity.entities/Documentation~/allocators-system-group.md)

### 1.3 Physics group

Unity Physics 的 custom physics group 会为指定 PhysicsWorld 运行自己的 PhysicsSystemGroup；挂在 `BeforePhysicsSystemGroup`/`AfterPhysicsSystemGroup` 的用户系统也会随该 custom group 运行。

官方快照：[Physics system groups](../../官方文档原件/com.unity.physics/Documentation~/group-body.md)。

因此，“把一个系统放 AfterPhysicsSystemGroup”不仅表示主 Physics 后运行，还可能让它在多个 custom PhysicsWorld group 中被复制调用。这是机制；系统是否应该复制是项目语义。

## 2. EX-GAS v1 裁决

### 2.1 唯一固定步进域

```csharp
/// <summary>
/// EX-GAS 的主固定步进域；作为 FixedStep 直接子组只跟随主 Physics 顺序，不随 custom PhysicsWorld 的 AfterPhysics 组复制。
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial class GasFixedTickSystemGroup : ComponentSystemGroup
{
}
```

这是项目决策：

- GAS 使用 FixedStep，默认 PostPhysics。
- 一渲染帧可 `0..N` GAS Tick。
- 持续时间/周期/冷却使用整数 `SimulationTick`。
- Session Tick Rate 建立后不可变并进入规则哈希。
- 当前 Tick 的 Physics 结果可供 GAS；GAS 写入 Physics/Transform 的结果在下一 Physics Tick 生效。

不选择 `AfterPhysicsSystemGroup`，是为了避免全局 GAS 随 custom PhysicsWorld 复制。若某项逻辑确实属于每个 PhysicsWorld，应建立专用局部 integration system，不改变 GAS authority owner。

### 2.2 一个 Group + 一个 Kernel

`GasFixedTickSystemGroup` 默认包含：

1. 可选 `GasCommandIngressSystem`：只做外部请求规范化；
2. `GasTickKernelSystem`：拥有全部 Runtime Core Job DAG。

Resolve、Ability、Effect、Stabilization、Fact merge 等是 Kernel 内具名 lane/Job，不是多个 SystemGroup。该选择减少固定调度面并让 Tick scratch/依赖有唯一 owner；官方文档并未要求 GAS 必须如此。

如果未来新增 Core System，必须证明其需要不同生命周期/频率/托管边界，而不是仅为了源文件拆分或 phase 排序。

### 2.3 Managed 边界

`GasBoundaryDrainSystem` 可以使用 `SystemBase`/托管 service，因为它明确处于 Core 之外并需要对接 Cue/UI/Audio/日志。它在固定步进批次后单次接管 outbox，不把 UnityEngine.Object 送入 Burst Job。

## 3. manual / 独立 World

官方允许自定义 bootstrap/world，但手动调用某个 system 的 Update 不能自动复现默认父组的 rate manager、allocator、Physics 和 ECB 顺序。

EX-GAS runner 合约：

- TickBatch owner 更新完整 `FixedStepSimulationSystemGroup` 父链；
- 不直接 Update Kernel，不维护旧 GAS phase 清单；
- 父链自然执行 Physics → GAS → 标准 EndFixed；
- 固定批次后执行单 managed drain；
- 标准与 manual World 使用相同 Session Tick Rate/规则 hash。

参考：[ICustomBootstrap](../../官方文档原件/com.unity.entities/Documentation~/systems-icustombootstrap.md) 与本主题 [CASE-17](./CASE-17.md)。

## 4. 依赖与可观测性

- Update ordering 只表达 system 顺序；Kernel lane 之间使用 JobHandle。
- Kernel 不在 phase 边界 `Complete()`。
- 单 Kernel 内用具名 Job、ProfilerMarker、稳定 trace id 保持可调试性。
- Debug/Presentation 只能读 Boundary/快照，不能从托管 manager 驱动 Core。

关联规则：[SYS-01](./SYS-01.md)、[SYS-03](./SYS-03.md)、[SYS-05](./SYS-05.md)。

## 5. 不应写死

以下由 ScaleProfile/ADR 决定：

- 具体 Job 类型、batch size 与排序算法；
- 是否真的需要 ingress 独立 System；
- managed drain 的实际父组/宿主（但必须在完整固定批次后且保持单消费者）；
- 多 World 的构建方式；
- System 数量与耗时数值门槛。

不能变化的 v1 契约是 FixedStep/PostPhysics、完整父链、单 Kernel ownership、整数 Tick 与标准 EndFixed。
