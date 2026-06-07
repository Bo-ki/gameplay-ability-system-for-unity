# 03E：Effect Fan-In / State / Attribute / Fact

> Owner：`01-目标态架构共识/03-RuntimeCore管线` | 状态：目标态子 Spec 索引 | 拆分来源：`../03-RuntimeCore管线Spec.md` | 最近拆分：2026-06-08

本文件只保留 03E 目标态主题索引。正文已拆入同名子目录；禁止在此追加当前代码事实、迁移流水、验证数字或下一步任务。

## 职责

03E 定义 Runtime Core 中 effect fan-in、active effect store 状态推进、attribute reduce/apply 和 gameplay fact projection 的目标态数据流。

这些主题属于同一个 Runtime Core simulation 段，但各自的输入、输出、carrier、并行策略和验收关注点不同，因此根页只保留阅读路径，正文由子页分别维护。

## 阅读顺序

| 子页 | 职责 |
|---|---|
| [03E-01 Effect Fan-In](03E-EffectFanIn-State-Attribute-Fact/03E-01-EffectFanInSpec.md) | 目标态 GE command producer、NativeStream fan-in、deterministic merge 和 target ASC command buffer 写入。 |
| [03E-02 State Evaluate / ActiveEffect Store](03E-EffectFanIn-State-Attribute-Fact/03E-02-StateEvaluateActiveEffectStoreSpec.md) | 目标态 active effect slot 状态推进、period due owner 和 PostApply store 变更边界。 |
| [03E-03 Attribute Reduce / Apply](03E-EffectFanIn-State-Attribute-Fact/03E-03-AttributeReduceApplySpec.md) | 目标态 AttributeSet target grouped 写入、dirty mask、modifier buffer 和 attribute fact 输出。 |
| [03E-04 Gameplay Fact](03E-EffectFanIn-State-Attribute-Fact/03E-04-GameplayFactSpec.md) | 目标态 Core reaction fact 投影、death fact 示例和 Boundary Projection 分离规则。 |

## Owner 边界

1. 当前代码事实、文件行号、运行日志、x50 / x100 / x1000 数字和迁移 proof 写入 `../../00-当前架构事实/`。
2. 可领取任务、退出门、执行顺序和测试链路写入 `../../02-主线任务树/`。
3. 短期接力、未跑项和最新验证摘要写入 `../../04-当前进度状态/`。
4. 本索引和子页只维护目标态 Spec、禁止方向、DOTS 官方依据和验收门槛。

## 与相邻 Spec 的关系

1. `03D` 负责 command ingest 与 target resolve；`03E-01` 只消费已解析的 command / target record，不重新定义输入层。
2. `03F` 负责 structural intent 和 Boundary Projection；`03E-04` 只产出 Core reaction fact，不执行结构变化或表现层投影。
3. `03G` 负责 component 矩阵、Frame Arena 和 Job 拓扑；03E 子页只描述本 lane 的最小 carrier 与读写约束。
4. `03H` 负责 DOTS API 选型与验收预算；03E 子页引用其策略，不复制 API 目录正文。
5. `03I` 负责 lane catalog 与禁止方向；03E 子页只维护 Fan-In / State / Attribute / Fact 的局部执行契约。
6. [05 ActiveEffectStore](../05-ActiveEffectStoreSpec.md) 是 active effect store 的总体目标规则；`03E-02` 只维护 State Evaluate lane 中 PostApply store 变更代码骨架，不复制 store 总体规范。
7. [13 EntityComponent 物理布局](../13-EntityComponent物理布局Spec.md) 是组件物理布局 owner；03E 子页不重复维护全局 component catalog。

## 归档

拆分前全文快照见 [2026-06-08-03E-EffectFanIn-State-Attribute-FactSpec拆分前](../_归档/2026-06-08-03E-EffectFanIn-State-Attribute-FactSpec拆分前.md)。

## 反向入口

- 子页索引：[03E 子 Spec 索引](03E-EffectFanIn-State-Attribute-Fact/README.md)
- 03 Runtime Core 管线索引：[README.md](README.md)
- 03 总览：[../03-RuntimeCore管线Spec.md](../03-RuntimeCore管线Spec.md)
- 01 总入口：[../README.md](../README.md)
