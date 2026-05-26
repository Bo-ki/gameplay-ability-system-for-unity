# PRF-32: 主线程数据操作禁用 .Run() Job

**严重度**: P1
**Primary Owner**: 数据流-系统生命周期
**来源**: `job-overhead.md`、`performance-sync-points.md`

## 规则声明

主线程需要同步执行 entity 数据操作时，应直接使用 `SystemAPI.Query` foreach（idiomatic foreach），而非 `.Run()` 调度同步 job。`.Run()` 通过 job 调度系统执行，包含了额外的 job dependency system 开销（sync point + type handle 刷新 + 依赖链管理）；主线程 foreach 无此开销。

## 为什么

主线程 foreach 直接遍历 chunk，不经过 job 调度系统，避免 job system 的开销。`.Run()` 虽然同步执行，但 ECS 将其作为"main thread job"处理——仍会创建 job handle、检查依赖、触发可能的 sync point。官方明确建议："主线程优先 foreach，仅在需要 Burst 编译时才使用 `.Run()`。"

## EX-GAS 诊断

Debugger 路径、一次性的 initialization 路径、entity 数量很少的查询中，若有 `query.Run()` 或 `.Schedule().Complete()` → 评估是否替换为 `SystemAPI.Query` foreach。

## 检查方法

搜索 `.Run()` 在 Runtime Core 中的使用。若为 hot path → 必须改为 IJobEntity 的 `.ScheduleParallel()`；若为主线程一次性的小数据操作 → 优先 `SystemAPI.Query` foreach。
