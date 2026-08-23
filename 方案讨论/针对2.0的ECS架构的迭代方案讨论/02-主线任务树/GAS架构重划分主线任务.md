# GAS 架构重划分主线任务

> Owner：`02-主线任务树` | 状态：当前路由别名 | 最近更新：2026-08-24

当前 GAS 架构重划分已从“在五段 physical group 上渐进瘦身”切换为 Runtime v1 不可兼容重构。

唯一可领取入口：

- [Runtime v1 不可兼容迁移](RuntimeV1不可兼容迁移/README.md)

必读 owner：

- 当前事实：[Runtime v1 不可兼容迁移基线事实](../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)
- 目标规范：[17-GAS业务链路破坏性重划分 Spec](../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md)
- 当前接力：[当前窗口](../04-当前进度状态/当前窗口.md)

## 当前主线目标

以 V0-V7 完成一次性权威切换：真实测试基线 -> Definition/Identity -> ASC slab -> 单 Kernel -> Effect/Attribute/Tag 语义 -> Boundary Drain/Cue -> AutoChess 与旧链删除 -> 确定性/规模/Profiler release gate。

## 禁止领取

1. 不再从 R0-R8 继续实现渐进 carrier/五组优化。
2. 不在旧 AM/Chain/Export 文件续写新轮次。
3. 不先实现 compatibility facade、backend selector 或双 Runtime。
4. 不把目标设计写回 `00`，不把当前完成度写进 `01`。

旧 R0-R8 只读索引：[R0-R8 历史输入](R0-R8/README.md)。
