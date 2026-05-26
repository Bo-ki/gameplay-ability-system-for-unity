# JOB-02: 区分 job scheduling overhead、main-thread sync、Burst warmup

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — Job 调度与依赖管理节、PRF-09 节、QRY-02 节

## 规则声明
性能分析时必须区分三种成本来源：job scheduling overhead（调度延迟）、main-thread sync point（主线程等待）、Burst warmup（首次编译）。禁止笼统归因为"job 慢"或"Burst 问题"，必须分别归因。

## 为什么
三种成本的特征和优化方向完全不同：
- **Scheduling overhead**：由 system 数量和 job 粒度决定。过小粒度的 job 导致调度开销 > 执行开销。
- **Sync point**：由主线程 query 操作或 `Run()` 触发。主线程阻塞等待所有 worker 线程闲置。
- **Burst warmup**：首次编译产生一次性延迟。Burst 编译后的性能稳定可预测。

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
`state.Dependency` 是当前 system 的"等待门"——下一个 system 的 job 自动等待它。依赖链长度和复杂度直接影响调度性能。

**Sync Point 触发操作：**
- `CalculateEntityCount()` — 有 enableable 过滤且写 job 未完成时触发 sync
- `ToEntityArray()` / `ToComponentDataArray()` — 同步版本触发 sync
- `GetSingleton<T>()` — 触发 sync，建议用异步变体或 `GetSingletonRW`

## EX-GAS 诊断
EventBus 中同步 query 操作 → 注意 sync 触发。当前诊断报告应分别列示 scheduling overhead、sync point 耗时、Burst warmup 成本，禁止混为一谈。

## 检查方法
- 审查 `OnUpdate` 中 job 调度代码，统计调度开销 vs 执行开销
- 使用 Profiler 标记区分 Scheduling、Sync、Execution 三个阶段
- 性能报告中必须包含三种成本的单独数据
