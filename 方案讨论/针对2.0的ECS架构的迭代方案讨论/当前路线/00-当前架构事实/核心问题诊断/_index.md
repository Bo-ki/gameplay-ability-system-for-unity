# 核心问题诊断明细

本目录按”一个核心问题一个文档”维护当前架构问题。`../_index.md` 为总看板，保留索引、状态、总诊断和开放点摘要。

## 文件索引

| 文件 | 问题 |
|---|---|
| [ISSUE-001-GE生命周期管线过重](ISSUE-001-GE生命周期管线过重.md) | simple instant GE 仍容易落入 request entity / runtime GE entity / lifecycle / eventbus 全链路 |
| [ISSUE-002-Observation与RuntimeCore热路径耦合](ISSUE-002-Observation与RuntimeCore热路径耦合.md) | EventBus / presentation / replay / reaction 共享热路径 |
| [ISSUE-003-RuntimeCoreDebugger证据不足](ISSUE-003-RuntimeCoreDebugger证据不足.md) | Debugger 还不能直接解释 request/spec/delta/fact/entity lifecycle 成本 |
| [ISSUE-004-结构变化边界脆弱](ISSUE-004-结构变化边界脆弱.md) | 边读 buffer 边结构变化容易引发句柄失效和隐性 sync point |
| [ISSUE-005-Generated链路未反哺RuntimeCore](ISSUE-005-Generated链路未反哺RuntimeCore.md) | Definition / generated / bake contract 还未成为 Runtime hot path 主输入 |
| [ISSUE-006-AutoChessDemo边界混入RuntimeCore](ISSUE-006-AutoChessDemo边界混入RuntimeCore.md) | 验收 Demo 仍位于 Runtime Core 目录并存在巨类混杂 |
| [ISSUE-007-任务上下文与目标态Spec断链](ISSUE-007-任务上下文与目标态Spec断链.md) | 任务 prompt 上下文不足会导致执行偏离目标态 |
| [ISSUE-008-目标态Spec尚未充分UnityEntities机制化](ISSUE-008-目标态Spec尚未充分UnityEntities机制化.md) | 目标态还需要落到 Unity Entities 1.4.6 的具体机制 |
| [ISSUE-009-RuntimeCoreFrameBackbone缺失](ISSUE-009-RuntimeCoreFrameBackbone缺失.md) | 当前 Runtime Core 缺少统一 SystemGroup / query / allocator / structural playback / Debugger evidence 的 DOTS frame backbone |
| [ISSUE-010-代码执行范式未切换到DOTS](ISSUE-010-代码执行范式未切换到DOTS.md) | 全系统主线 foreach + EntityManager，零 IJobEntity/IJobChunk 使用 |
| [ISSUE-011-临时EntityQuery泛滥与API承载选型错误](ISSUE-011-临时EntityQuery泛滥与API承载选型错误.md) | 每帧临时 EntityQuery、ThreadStatic 批量状态、IComponentData 挂 NativeArray |
| [_归档](./_归档/README.md) | 已解决或合并的问题诊断 |

## 单问题文档必备结构

1. `状态`：Active / Monitoring / Resolved / Archived，含严重度、最近复核、所属层。
2. `DOTS规则引用`：关联的规则编号、摘要、原因（必填，连接 UnityDOTS 规范）。
3. `问题陈述`：一句话说明问题本质，不写解决方案口号。
4. `当前证据`：当前代码、当前 profile、当前验证记录或当前文档事实。
5. `执行路径`：从入口到后果的链路，能画图就画图。
6. `影响`：性能、结构安全、边界污染、Agent 上下文或验收风险。
7. `根因反推`：这个问题说明目标架构哪条约束还没兑现。
8. `目标态入口`：指向 `../../01-目标态架构共识/` 的具体 Spec。
9. `任务入口`：指向 `../../02-主线任务树/` 的具体主线或支线。
10. `退出条件`：怎样才算已解决或可归档。

完整模板和维护流程见 `../维护规范.md` Part E。
