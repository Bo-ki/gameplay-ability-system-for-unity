# 91 Agent 行动报告模板

## 使用时机

Agent 领取 DOTS 相关任务，完成上下文搜寻后、正式执行前，必须输出行动报告。报告不等待用户批准，但必须让用户看见 Agent 掌握的上下文和将要采取的动作。

## 模板

```md
行动报告：

任务：<任务树节点名>
分支：<主线 / 支线 / 三级任务>

已读取上下文：
- 目标态 Spec：<01-目标态架构共识/...>
- 任务树：<02-主线任务树/...>
- 当前事实：<00-当前架构事实/...>
- Unity DOTS 官方主题：<UnityDOTS官方文档参考/主题/...>

官方文档覆盖检查：
- PackageCache 版本：<Entities 1.4.6 / Physics 1.4.6 / ...>
- 适用主题：<01 / 02 / 03 / 04 / 13 / ...>
- 适用规则：<SYS-*, QRY-*, SEL-*, PRF-*, ODF-*>
- 采用：<采用哪些 API / 案例>
- 拒绝：<拒绝哪些 API / 案例及原因>
- 暂不相关：<Physics / Graphics / Baking 等为什么不相关>
- 规范检查：<PRF-01~PRF-16 相关项的检查结果>

执行计划：
- <将修改哪些模块 / 文档 / 测试>
- <如何控制结构变化、query、allocator、Burst、determinism>

验收方式：
- <测试链路>
- <性能 / Debugger / Profiler / battle hash 证据>
- <需要反哺的 owner>
```

## 交还检查

1. 报告中必须出现具体主题文档，不允许只写”读取 DOTS 官方文档”。
2. 报告中必须说明 API selection，而不是只声明使用 ECS。
3. 若任务涉及性能，必须说明 core / physics / render / runner 拆分。
4. 若任务涉及 AutoChessDemo，必须说明 battle hash、ScaleProfile 和无头 / rendered profile 关系。
5. **新增：** 所有 Runtime Core 任务必须附 `13-DOTS编写规范与性能陷阱`（P0/P1）的检查清单，至少覆盖：
   - PRF-01（禁止 Entity 表示临时状态）
   - PRF-02（禁止 Hot Path 直接结构变化）
   - PRF-03（禁止 Tag Component 高频标记）
   - PRF-05（禁止 Hot Path 主线程遍历）
   - PRF-06（禁止高频 Random Access Lookup）
   - PRF-10（Buffer 溢出监控）
   P2 级检查项（Allocator/ECB/Singleton/System 生命周期）参见 `15-数据流-系统生命周期规范`。
