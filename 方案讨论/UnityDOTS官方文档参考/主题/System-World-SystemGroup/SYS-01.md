# SYS-01: 权威计算落在 ECS System/Job 数据流，禁止托管 manager 驱动

**严重度**: P0
**Primary Owner**: System-World-SystemGroup
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: EX-GAS 2.0；Unity 6000.3 / Entities 1.4.6
**官方来源**: `systems-isystem.md`、`job-overhead.md`

## 规则声明
所有 gameplay 权威状态变更（属性、效果、冷却）由 ECS System 拥有和编排。托管 MonoBehaviour/ScriptableObject 只作为配置或边界输入，不直接成为另一套权威状态机。工作量和依赖适合时由 system 调度 job；少量主线程工作仍是官方支持路径，应以 Profiler 决定，不强制“每次都必须调度 job”。

## 为什么
把权威状态同时放在托管 manager 与 ECS 会形成双事实源，并绕开 ECS 的 query、依赖和变更追踪。托管代码不等于必然慢，job 也有调度成本；性能结论必须来自目标规模、目标设备上的 Profiler，而不是预设操作次数或固定阈值。

## EX-GAS 诊断
当前 `GASManager.cs` 可保留边界路由职责，但不得持有与 ECS 重复的权威 gameplay 状态。Debugger 分别报告 ECS system/job、托管 boundary 与等待成本；是否迁移某条路径依据正确性归属和 Profiler 证据，不设“System/Job 占比 > 95%”这类无官方依据的通过线。

## 检查方法
- 搜索 `OnUpdate` 中直接调用托管 manager 方法的模式
- 统计 Runtime Core 中 `ISystem.OnUpdate` 与托管 `Update()` 的执行时间占比
