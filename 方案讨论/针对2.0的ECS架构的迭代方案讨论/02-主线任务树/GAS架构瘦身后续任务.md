# GAS 架构瘦身后续任务

> Owner：`02-主线任务树` | 最近归位：2026-08-29 | 状态：历史兼容索引，不可领取

当前唯一可领取路线是 [Runtime v1 不可兼容迁移](RuntimeV1不可兼容迁移/README.md)。[R0-R8](R0-R8/README.md) 与本文件只保留已完成瘦身批次、防回流检查和旧 proof 到当前任务树的历史映射，不再维护独立批次执行计划。

当前代码事实见 `../00-当前架构事实/架构瘦身事实约束.md`、`../00-当前架构事实/Runtime主链事实.md`、`../00-当前架构事实/架构重划分审查事实.md` 和 `../00-当前架构事实/SourceGenerator链路复审事实.md`。每轮真实进度写入 `../04-当前进度状态/`；本文件不接收逐轮验证摘要。

## 与已归档 R 切片的历史映射

| 旧瘦身批次 / 主题 | 当前归属 | 当前领取口径 |
|---|---|---|
| Damage / Attribute / Cue / Tag helper 兼容入口删除 | 已关闭，作为防回流素材 | 不再开新批次；新增回流命中写 `00` 并转 R3/R4/R5 |
| Presentation / Replay 双读瘦身 | 已关闭，作为防回流素材 | Presentation / Replay 只能消费 typed fact / boundary projection；新增问题转 R4 |
| generated active mutation store 瘦身 | R2 + R3 | 只改模板、manifest/report gate 和生成链，不手改 `.gen.cs` |
| global facade ownership 收缩 | R1 + R5 + R6 | Shell / Debugger / Demo / Bootstrap 分类，Core hot path 禁止白名单外 facade |
| structural evidence / Debugger 热点归因 | R4 | 需要 official diff、Profiler/Journaling state、TopN 对账和 pass 分离 |
| ActiveEffectStore cleanup/tick/finalize 剩余迁移 | R7 | store-only lifecycle、candidate collection、action classification、structural owner |
| scale profile / 性能预算 | R8 | 基于 R1-R7 evidence 后置推进，不用 x1/x50 功能通过替代规模证据 |

## 官方依据

旧瘦身批次继续受以下 DOTS 规则约束；执行时必须回到 R 任务的交还包填写 API 选型表和官方覆盖检查：

1. `../../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
2. `../../../UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`
3. `../../../UnityDOTS官方文档参考/主题/03-结构变化-ECB-Enableable.md`
4. `../../../UnityDOTS官方文档参考/主题/04-数据承载-Buffer-Chunk-Store.md`
5. `../../../UnityDOTS官方文档参考/主题/06-Diagnostics-Profiler-Journaling.md`
6. `../../../UnityDOTS官方文档参考/主题/09-Burst-编译-向量化-AOT.md`

## 防回流检查

这些检查用于确认旧瘦身成果没有回退；它们不是独立任务完成证明。

```powershell
rg "DamageEventBuffer|EnqueueDamageEvent|EnqueueAttributeChangeEvent|EnqueueTagChangeEvent" Assets/GAS/Runtime Assets/GAS/Editor/CodeGen/Phases Assets/GAS/Generated/CodeGen/Runtime
rg "state\.Dependency\.Complete\(|\.Run\(" Assets/GAS/Runtime Assets/GAS/Generated/CodeGen/Runtime
rg "GASManager\.EntityManager" Assets/GAS/Runtime Assets/GAS/Generated/CodeGen/Runtime
rg "GeneratedRuntimeBoundaryHits|GeneratedRuntimeLifecycleHits|GeneratedRuntimeRandomWriteLookupHits|CurrentMode" Assets/GAS Tools
```

命中解释必须按 owner 分类：

1. Runtime Core hot path：默认阻断，转 R1/R2/R3/R5/R7。
2. Boundary / Shell / Demo adapter：必须说明 proof-only compatibility、opaque handle / snapshot 替代路径和退出任务，转 R1/R6。
3. Debugger / Observation：必须与 performance pass 分离，转 R4/R8。
4. Bootstrap / Authoring / Prototype：可保留但必须列白名单和不进入 hot path 的证据。
5. generated/template：不能只改输出，必须改模板、manifest/report gate 和生成链，转 R5。

## 交还规则

1. 本文件不再新增 Batch F/G 或更新批次状态。
2. 旧瘦身 proof 的长期事实写入 `../00-当前架构事实/`；短期验证写入 `../04-当前进度状态/`；新任务只写入 Runtime v1 V0-V7 路线。
3. 不把“已 job 化”“已删除旧 helper”“x50 跑通”写成架构完成证明；必须给 owner-local / carrier / structural / Debugger / scale evidence。
4. 不把 Debugger、Presentation、Replay 或 AutoChess runner 的格式化输出当机器验收源；它们必须从 structured evidence 派生。
5. N0/N1/N2 只是 Runtime v1 路线的跨阶段实施检查点，不改变 V0-V7 owner、依赖顺序与退出门。

## 风险接受

1. 允许破坏旧 API 和旧观察缓冲，不做兼容层。
2. 允许中间阶段编译失败，但不能提交半迁移状态作为完成。
3. 生成产物失败时修模板和生成链，不手修 `.gen.cs`。
4. 保留 proof-only carrier / MigrationProofOnly artifact 时必须写清规模上限、reselect trigger 和下一条 R 任务。
