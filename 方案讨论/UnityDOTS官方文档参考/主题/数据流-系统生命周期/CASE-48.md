# CASE-48: Early-Out 不得丢失已调度 JobHandle

**Primary Owner**: 数据流-系统生命周期
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**: Unity 6000.3 / Entities 1.4.6
**官方来源**: `scheduling-jobs-dependencies.md`、`common-errors.md`
**关联规则**: PRF-29

## 使用场景

当 system 的 `OnUpdate` 存在 early-out，且某些分支可能已经调度 job 时。

## 模式描述

`state.Dependency` 在 `OnUpdate` 入口已经包含前序 system 的传入依赖。若当前调用尚未调度新 job，可以直接 `return`，不得把它清成默认值。若已经调度新 job，则必须在所有返回路径上把返回的 `JobHandle` 赋给或合并进 `state.Dependency`。

```csharp
[BurstCompile]
public partial struct SEffectCleanupSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var query = SystemAPI.QueryBuilder().WithAll<CExpiredEffect>().Build();

        // 尚未调度当前 system 的 job：保留传入的 state.Dependency 即可。
        if (query.IsEmpty)
            return;

        var cleanupHandle = new CleanupJob().ScheduleParallel(query, state.Dependency);

        // 已调度 job 的 early-out：先发布新 handle，再返回。
        if (!SystemAPI.HasSingleton<CleanupSettings>())
        {
            state.Dependency = cleanupHandle;
            return;
        }

        state.Dependency = new FinalizeCleanupJob().Schedule(cleanupHandle);
    }
}
```

## 注意事项

- `SystemState` 没有这里所称的无参 `DependsOn()`；依赖通过 `state.Dependency` 与各调度 API 返回的 `JobHandle` 表达。
- 不要为了 early-out 调用 `CompleteDependency()`；这会无必要地阻塞主线程。只有后续主线程代码必须立即访问受保护数据时才完成依赖。
- 不要执行 `state.Dependency = default` 或 `new JobHandle()`，这会丢失传入依赖。
- `RequireForUpdate` 不满足时，Unity 根本不会调用该 system 的 `OnUpdate`，不是 `OnUpdate` 内需要手动处理的 early-out 分支。

## EX-GAS 适用点

- 所有包含 early-out 逻辑的 system OnUpdate 方法
- 条件性的 effect cleanup system
- Debugger 中 conditional projection system
