# AutoChess 完整业务案例设计 Spec

## 结论

AutoChess 是 Runtime v1 的端到端业务验收投影，不拥有第二套 GAS 调度或数据模型。设计顺序必须先保住真实 9101-9104 攻击/斩杀/毒业务链，再用固定微场景证明 Runtime v1 语义，最后才把盾击、冰霜新星、羁绊、治疗等扩展计入覆盖。

详细设计见 [10B 子目录](10B-AutoChess完整业务案例/README.md)。Runtime 通用 contract 仍以 01/03/04/05/06/13/16/18/24 为准，本案例只绑定业务字段和验收结果。

## 场景目标

- 同一 Session 跑 Scene 与 Headless，使用相同 FixedStep/Physics/GAS/EndFixed/Drain。
- 一 World 只有一个 active Session，但允许多个 BattleInstance；单战局终局不停止其他战局。
- SpawnBatch 在 Pending 期间完成所有 ASC/初始属性/Tag/Grant 与容量验证，整批可见或整批失败；只在 Ready 后发布稳定 handle。
- 多单位、多目标与多来源 stack 验证 target single writer/canonical order。
- 生命值归零在当前 target transaction 立刻禁止行动，同时 Death reaction/表现按正式时序导出。
- Cue 资源缺失不影响 gameplay，Headless 仍输出完整 marker。
- replicated groups、hot target、period burst、mass death+teardown、Boundary burst/retry、wait fanout/cancel 与 cross-ASC live dirty 使用同一 ScaleProfile/evidence schema。

## 业务分层

| Tier | 内容 | 验收边界 |
|---|---|---|
| A legacy real characterization | 9101 己方攻击、9102 敌方攻击、9103 斩杀、9104 毒；AI 优先级 `ActiveDue > Finisher > Primary` | 使用 `BattleLocalTick` 和稳定 `ScenarioUnitId` 归一当前 wallclock/raw Entity 差异；不声称当前已覆盖 cost/cooldown/wait/Tag/Cue/cleanup |
| B target semantic conformance | Spawn/Ready、Commit、Frozen target、9203 精确毒、Death、wait/cancel、immunity/inhibition、Tag count、LeaveGranted、Cue/FinalDrain | 每个机制用独立固定微场景精确断言，不用 count > 0 替代 |
| C extension | 盾击、冰霜新星、羁绊、治疗、资源表现 | 只是目标扩展样例；未有真实场景 evidence 时不计入当前覆盖 |

## 非目标

不以 AutoChess Adapter、Damage System、旧五组 timing 或自定义 GE Entity 作为 Runtime extension seam；不借 Demo 恢复同步 GameplayEvent call-stack、Prediction 或 raw Entity public API。不保留 9203 非法 StackType/sourcegen override/`-1` period 或 9207 无限 marker 的兼容支路。
