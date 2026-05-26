# CASE-01: SystemAPI.Query 遍历

**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — SystemAPI.Query 节、CASE-01
**关联规则**: QRY-01, PRF-05

## 使用场景
仅用于 Debugger 快照、Editor 工具、<100 entity 的 proof 验证。任何 >100 entity 的 hot path 拒绝使用。

## 模式描述
`SystemAPI.Query` 是 ECS 的主线程遍历方式。底层机制为 source generator 创建并缓存 EntityQuery，但每次 foreach 触发主线程 sync point——等待所有相关 job 完成。

```csharp
// 仅限 Debugger/Editor/proof 场景
foreach (var (health, translation) in SystemAPI.Query<RefRO<Health>, RefRW<Translation>>())
{
    translation.ValueRW.Value += health.ValueRO.Value;
}
```

**关键事实：**
- Source generator 为每个 `SystemAPI.Query` 调用自动创建并缓存 `EntityQuery`
- `foreach` 触发主线程 sync point —— 等待所有写入相关 component 的 job 完成
- 主线程遍历导致所有 worker 线程闲置等待
- 无法 Burst 编译

## 注意事项
- 不可以用于 Runtime Core hot path
- 每次遍历触发 sync point，不适用于高频场景
- 适合快速原型验证、Editor 工具、debug 可视化

## EX-GAS 适用点
- Debugger 快照生成（`GasRuntimeDebugger`）
- Editor 工具中的数据预览
- Proof-of-concept 阶段的快速验证
