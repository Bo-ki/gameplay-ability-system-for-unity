# 20：GAS Runtime Core API 选型基线

> 精确版本：Unity 6000.3 / Entities 1.4.6
> 定位：把各主题官方机制组合成 EX-GAS v1 的项目选择；不是新增 Unity 官方规则

## 1. 最终选择

| 关注点 | EX-GAS v1 | 官方机制提供什么 | 项目额外承担什么 |
|---|---|---|---|
| Tick | FixedStep，`0..N` Tick/render frame | rate-managed group/update order | 整数 SimulationTick、Tick Rate/hash |
| Physics 顺序 | FixedStep 直接子组，After `PhysicsSystemGroup` | update attributes/physics groups | PostPhysics 语义 |
| Core 系统 | 一个 `GasFixedTickSystemGroup` + 一个 `GasTickKernelSystem` | ISystem/SystemGroup/Job 调度 | lane 划分、单 Kernel 所有权 |
| 临时内存 | `SystemState.WorldUpdateAllocator` | world/group allocator | 项目可用期仅当前 Tick |
| 持久 ASC 数据 | Component/DynamicBuffer | chunk/component/buffer | Layout/Catalog/slab 唯一模型 |
| 结构变化 | 标准 `EndFixedStepSimulationEntityCommandBufferSystem` | 延迟 playback | 哪些操作属于结构变化 |
| 销毁事实 | per-ASC `ICleanupBufferElementData` outbox | cleanup shell 生命周期 | 单 drain、幂等 key、retention 边界 |
| 性能门 | ScaleProfile | Profiler/容器与 Job API | 场景、门槛与回归判定 |

## 2. 执行域

```csharp
/// <summary>
/// EX-GAS v1 的唯一固定步进域；选择主 Physics 后更新，而不是注册到会随 custom PhysicsWorld 复制的 AfterPhysicsSystemGroup。
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial class GasFixedTickSystemGroup : ComponentSystemGroup
{
}
```

- 这是 EX-GAS 项目裁决，不是 Entities 对 GAS 的官方要求。
- 一渲染帧固定组可以更新 `0..N` 次；Duration/Period/Cooldown 使用整数 Tick。
- Session Tick Rate 建立后不可变，并参与规则哈希。
- GAS 默认 PostPhysics；当前 Tick 的 GAS 物理写入在下一 Physics Tick 生效。
- manual/独立 World 必须更新完整 FixedStep 父链，不能手工更新 GAS phase 清单。

参考：[System / World / SystemGroup 解读](./System-World-SystemGroup/API与EX-GAS解读.md)。

## 3. System 与 Job

v1 只允许：

- 可选 `GasCommandIngressSystem`：规范化外部输入，不执行 GAS 语义；
- `GasTickKernelSystem`：拥有整 Tick Job DAG 与 scratch；
- Unity 标准 EndFixed ECB system；
- 固定步进批次后的单 `GasBoundaryDrainSystem`。

Kernel 的逻辑 lane 是具名 Job/纯函数：freeze/sort/resolve、ability/continuation、pre-apply expansion、target bucket、apply/stabilize、fact merge、boundary projection、ECB record。禁止为了 phase 排序把它们重新拆成多个 SystemGroup，也禁止 phase `Complete()`。

## 4. 数据 API

| 数据 | API/布局 | 硬契约 |
|---|---|---|
| 静态定义/Catalog/Layout | BlobAsset | build 时验证；运行时只读共享 |
| Attribute | fixed logical length DynamicBuffer | Layout id→index；Base/Current 唯一权威 |
| Tag | fixed logical length count Buffer | Exact/Inclusive；bitset 仅派生 |
| Granted Ability | ASC-local slab Buffer | slot+generation；不压缩 |
| Continuation | ASC-local slab Buffer | 跨 Tick 持久，不用 scratch |
| Active Effect | target-local slab Buffer | capture/stack/period/inhibition/channel |
| Tick work | WorldUpdateAllocator NativeContainer | 单 Kernel/单 Tick项目所有权 |
| Boundary | ASC Cleanup Buffer | 自包含 id/tick/key；单 managed drain |

v1 不允许 definition-time slot→Entity promotion。projectile/hitbox 可作为独立 Entity，但只引用权威 slot handle。

## 5. 并行与确定性

- Command 输入生成 canonical stable key。
- target bucket 之间并行，同 target 单 writer。
- ongoing/inhibition/tag grant/remove 在 target-local stabilization 中求稳定态。
- Definition build 检查带符号依赖与 closed/finite/bounded program。
- 运行时不收敛为 deterministic fatal；不固定 pass 静默截断。
- Ability direct output 与 verified pre-apply program 可 same-tick；apply 后 Fact reaction 默认下一 Tick。
- Fact/ECB 排序使用 stable key，不依赖 worker 完成时序。

## 6. 结构与边界

Ability/Effect 槽写、Attribute/Tag 更新、outbox append 都不是结构变化。创建/销毁 Entity、增删 Component/Buffer 才进入标准 EndFixed。

ASC spawn 必须显式添加 cleanup outbox，因为 cleanup component 不从 prefab instance 自动继承。Destroy 后 shell 只保留 cleanup 数据；drain 接管后清空，并在下一标准 EndFixed 移除 buffer。

Runtime Core 只提供一个 managed drain。UI/Audio/Cue/Log/Network 的多消费者与 retention 在托管 dispatch 层处理，不在 ECS 内维护多游标。

参考：[ECB 解读](./结构变化-ECB/API与EX-GAS解读.md) 与 [数据流/生命周期解读](./数据流-系统生命周期/API与EX-GAS解读.md)。

## 7. Allocator

官方 world/group allocator 可能在物理上保留两次更新；EX-GAS 仍把 scratch 的项目可用期限制到当前 Tick DAG。尚未 rewind 不等于允许跨 Tick引用。

禁止：

- 自定义 FrameArena Singleton/手工 rewind；
- scratch 写入 Component/static/managed field；
- 跨 System传 NativeContainer；
- 长期 continuation/effect 使用临时 allocator；
- 仅为 lane 边界调用 `Complete()`。

参考：[NativeContainer / Allocator 解读](./NativeContainer-Allocator/API与EX-GAS解读.md)。

## 8. ScaleProfile

任何 IBC、初始容量、batch size、算法与数值门槛必须引用版本化 ScaleProfile，包含环境、输入分布、场景生成、采样方法、基线 commit 与验收阈值。

通用架构只写形态红线：无 phase sync、无双事实源、无热路径结构变化/resize、无同 target 竞态写、无越生命周期 scratch、无未稳定事实、无遗留 cleanup shell。

## 9. 任务交还模板

涉及 Runtime Core 的设计/实现必须回答：

1. 运行在哪个 Tick/Group，`0..N` Tick 是否成立？
2. 权威 owner、读写者与 stable key 是什么？
3. 数据为何选择 Component/Buffer/Blob/NativeContainer/Cleanup？
4. allocator、项目可用期与最终 JobHandle 是什么？
5. 是否结构变化，在哪个标准 playback 点可见？
6. same-tick 是否证明 closed/finite/bounded，否则如何推迟？
7. Destroy/shutdown 后谁 drain/清理？
8. 哪个 ScaleProfile/验收场景支持容量与性能选择？

缺少任一项不得称为可实施设计。
