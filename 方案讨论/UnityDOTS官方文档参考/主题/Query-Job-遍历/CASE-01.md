# CASE-01: SystemAPI.Query 遍历

**Primary Owner**: Query-Job-遍历
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `systems-systemapi-query.md`、`performance-sync-points.md`
**关联规则**: QRY-01, PRF-05

## 使用场景
适用于需要在主线程直接遍历组件、逻辑简单且不需要 worker-thread 并行的场景。是否用于 hot path 由目标设备上的 Profiler 数据、依赖等待和单实体工作量决定，不以实体数量设置统一阈值。

## 模式描述
`SystemAPI.Query` 是 ECS 的主线程遍历方式。底层机制为 source generator 创建并缓存 EntityQuery，并在 foreach 前自动完成必要 read/write 依赖；若相关 job 未完成，会表现为主线程等待。

```csharp
// 主线程直接遍历；可在合适的 ISystem/Burst 上下文中编译
foreach (var (health, translation) in SystemAPI.Query<RefRO<Health>, RefRW<Translation>>())
{
    translation.ValueRW.Value += health.ValueRO.Value;
}
```

**关键事实：**
- Source generator 为每个 `SystemAPI.Query` 调用自动创建并缓存 `EntityQuery`
- `foreach` 前会自动完成必要 read/write 依赖；若相关 job 未完成，会表现为主线程等待
- 只有相关依赖尚未完成时才会发生主线程等待；无关 job 仍可继续运行
- 可在合适 `ISystem` / Burst 上下文被 Burst 编译，但仍是主线程 idiomatic foreach，不提供 worker-thread 并行

## 注意事项
- 不要把“主线程执行”直接等同于“每次必然产生有成本的等待”；应在 Profiler 中检查依赖完成与遍历本体耗时
- 当遍历工作量足以摊薄调度开销，且存在可利用的并行度时，评估改用 `IJobEntity` / `IJobChunk`
- Debugger、Editor 工具和原型验证是常见用途，但不是唯一合法用途

## EX-GAS 适用点
- Debugger 快照生成（`GasRuntimeDebugger`）
- Editor 工具中的数据预览
- Proof-of-concept 阶段的快速验证
