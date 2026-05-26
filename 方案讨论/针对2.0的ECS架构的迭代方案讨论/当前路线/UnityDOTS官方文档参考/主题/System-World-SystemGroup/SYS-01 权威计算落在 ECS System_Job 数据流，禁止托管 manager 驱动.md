# SYS-01: 权威计算落在 ECS System/Job 数据流，禁止托管 manager 驱动

**严重度**: P0
**Primary Owner**: System-World-SystemGroup
**来源**: `concepts-worlds.md`、`systems-intro.md`

## 规则声明
所有 gameplay 权威计算（属性变化、效果执行、冷却管理）必须经由 ECS System 调度 job 完成，禁止由托管 C# manager 类（MonoBehaviour、ScriptableObject 等）在主线程驱动计算逻辑。

## 为什么
托管 manager 驱动意味着计算在主线程顺序执行、无法 Burst、无法利用 worker 线程并行度、且与 ECS 数据的安全句柄系统隔离。当 x50 规模下每秒数万次 GE 操作时，托管 manager 成为中心瓶颈。

## EX-GAS 诊断
当前 `GASManager.cs` 仍持有一部分托管路由职责。Debugger 应报告 System/Job 执行的运算量 vs 托管路径运算量的比例，目标是 System/Job 占比 > 95%。

## 检查方法
- 搜索 `OnUpdate` 中直接调用托管 manager 方法的模式
- 统计 Runtime Core 中 `ISystem.OnUpdate` 与托管 `Update()` 的执行时间占比
