# PHY-03: Collision/Trigger Events 不能跨 frame 保存

**严重度**: P1
**Primary Owner**: UnityPhysics
**来源**: `simulation-results.md`

## 规则声明
`SimulationSingleton` 提供的 collision / trigger event iterator 只在当前 physics step 有效。禁止将 event iterator 或其引用保存到下一帧或跨 frame 使用。

## 为什么
Physics simulation 每帧重建 event 数据结构。跨 frame 保存的 iterator 指向已释放或复写内存，读取产生未定义行为。

## EX-GAS 诊断
搜索将 `SimulationSingleton`、collision event types 或 trigger event types 存储在 `IComponentData` 或 `NativeContainer` 中跨 frame 保留的模式。

## 检查方法
搜索 `SimulationSingleton` 存储为 system 字段或 component 字段；搜索 collision/trigger event 类型的跨 frame 引用。
