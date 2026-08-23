# JOB-02: 区分 job scheduling overhead、main-thread sync、Burst warmup

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6；Burst 1.8.29
**来源**: `performance-sync-points.md`、`systems-systemapi-query.md`、`components-enableable-use.md`、Burst `getting-started.md`

## 规则声明
性能分析时必须区分三种成本来源：job scheduling overhead（调度延迟）、main-thread sync point（主线程等待）、Burst warmup（首次编译）。禁止笼统归因为"job 慢"或"Burst 问题"，必须分别归因。

## 为什么
三种成本的特征和优化方向完全不同：
- **Scheduling overhead**：由 system 数量和 job 粒度决定。过小粒度的 job 导致调度开销 > 执行开销。
- **Dependency wait / sync point**：`Run()`、idiomatic foreach 或同步 Query API 在所需依赖尚未完成时会让主线程等待；等待范围取决于 API 和相关依赖。
- **Burst 首次成本**：Editor 中可能包含异步 JIT 编译；Player 使用 AOT，但首次调用仍可能混入加载、缓存等一次性成本，应与稳态样本分开。

将三者混淆会导致错误的优化方向——例如为了减少 sync point 而增加 system 数量，反而增加了 scheduling overhead。

**依赖链与调度：**
```csharp
public partial struct MySystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var job1 = new Job1 { ... };
        state.Dependency = job1.ScheduleParallel(state.Dependency);

        var job2 = new Job2 { ... };
        state.Dependency = job2.ScheduleParallel(state.Dependency);
    }
}
```
`state.Dependency` 记录本 system 已调度、需要传递给后续系统的 job。Entities 会基于组件访问和该句柄计算后续依赖；自定义 NativeContainer 的生产者/消费者关系仍需显式串联。

**Sync Point 触发操作：**
- `CalculateEntityCount()` — 有 enableable 过滤且写 job 未完成时触发 sync
- `ToEntityArray()` / `ToComponentDataArray()` — 同步版本触发 sync
- Singleton 便捷 API **不会替调用方自动完成依赖**；访问前是否需要 `CompleteDependencyBeforeRO/RW<T>()` 取决于是否存在冲突 job

## EX-GAS 诊断
EventBus 中同步 query 操作 → 注意 sync 触发。当前诊断报告应分别列示 scheduling overhead、sync point 耗时、Burst warmup 成本，禁止混为一谈。

## 检查方法
- 审查 `OnUpdate` 中 job 调度代码，统计调度开销 vs 执行开销
- 使用 Profiler 标记区分 Scheduling、Sync、Execution 三个阶段
- 性能报告中必须包含三种成本的单独数据
