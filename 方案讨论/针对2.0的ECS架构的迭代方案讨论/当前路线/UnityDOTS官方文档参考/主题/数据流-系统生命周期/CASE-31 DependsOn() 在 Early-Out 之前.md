# CASE-31: DependsOn() 在 Early-Out 之前

**Primary Owner**: 数据流-系统生命周期
**来源**: `systems-dependencies.md`
**关联规则**: PRF-29

## 使用场景

当 system 的 `OnUpdate` 需要在某些条件下提前退出（early-out），但在退出前必须确保已调度的 job 依赖链不被中断时。

## 模式描述

在 `OnUpdate` 中如果进行 early-out 判断（如 `RequireForUpdate` 不满足），必须在返回前调用 `state.DependsOn()` 或 `state.CompleteDependency()` 将已调度的 job 依赖传递下去，防止依赖链断裂。

```csharp
[BurstCompile]
public partial struct SEffectCleanupSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // 如果当前没有需要处理的 effect，提前退出
        if (SystemAPI.QueryBuilder().WithAll<CExpiredEffect>().Build().IsEmpty)
        {
            // 错误：直接 return 可能中断依赖链
            // return;

            // 正确：先传递依赖再 return
            state.Dependency = new JobHandle();  // 传递空依赖
            return;
        }

        // 正常处理逻辑
        var job = new CleanupJob { ... };
        state.Dependency = job.Schedule(state.Dependency);
    }
}
```

更精确的写法：
```csharp
public void OnUpdate(ref SystemState state)
{
    var query = SystemAPI.QueryBuilder().WithAll<CExpiredEffect>().Build();

    if (query.IsEmpty)
    {
        // DependsOn(JobHandle) 更新 state.Dependency 但不会强制 complete
        state.Dependency = state.Dependency;  // 或 state.CompleteDependency();
        return;
    }

    // 处理非空 query
    // ...
}
```

## 注意事项

- Early-Out 前不处理依赖链不会导致编译错误或运行时异常——这是**静默 bug**：后续 system 可能看到未完成的 job
- 最安全的做法：early-out 路径上调用 `state.CompleteDependency()` 强制等待所有 job 完成
- ISystem 结构中，`state.Dependency` 在每次 `OnUpdate` 入口被重置，所以需要显式设置

## EX-GAS 适用点

- 所有包含 early-out 逻辑的 system OnUpdate 方法
- 条件性的 effect cleanup system
- Debugger 中 conditional projection system
