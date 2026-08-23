# V6 AutoChess 迁移与旧链删除

> 状态：V3-V5 通过后领取 | 前置：V3、V4、V5

## Owner 输入

- [当前 AutoChess/旧链基线](../../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)
- [真实业务链事实](../../00-当前架构事实/AutoChess真实业务链二轮审查事实.md)
- [目标删除门](../../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md)
- [AutoChess 业务 Owner](../../01-目标态架构共识/10B-AutoChess完整业务案例设计Spec.md)
- [无头验收 Owner](../../01-目标态架构共识/10-AutoChess无头验收Spec.md)
- [配置语义编译/删除门 Owner](../../01-目标态架构共识/25-配置语义编译契约与CapacityProof统一裁决Spec.md)

## 目标

把 AutoChess command、execution、tick、observation 和 report迁入 v1 seam，并让所有旧 Runtime authority/调度达到零运行命中。

## 执行范围

1. command drive 改为 Kernel 前 immutable ingress producer。
2. damage calculation 改为 v1 pure evaluator dispatch。
3. runner 只调用 session `TickBatch`，不逐组 Update。
4. validation/report 改为 Kernel phase、immutable batch和stable identity。
5. 删除旧五组、自定义 ECB、Ability Entity、legacy GE、global index双写、旧 Cue bridge/EventBus consumer。
6. 实现一 World 一 active Session、多 BattleInstance；SpawnBatch `Pending -> Ready` 全有或全无，Initialize/Grant 失败有 request identity 与 typed outcome。
7. AI 迁移到 `ActiveDue > Finisher > Primary`、BattleLocalTick、ScenarioUnitId tie-break；Frozen ASC + AliveOnly，删除 invalid-target self fallback。
8. 区分 UnitDeath/BattleTerminal/SessionTerminal：单战局终局只封自身 ingress；全部战局终局/显式 stop 才 Session Terminalizing。
9. 终局 tick 完成完整 DAG -> managed gameplay FinalDrain -> 冻结 BattleOutcome；teardown audit 后返回 ValidationResult，teardown 不改 battle hash。
10. 删除 gameplay warmup、固定 postVictory 四 tick flush 与 Result-before-Close/cleanup；setup warmup、FinalDrain、teardown scope 分开。
11. 删除 sourcegen sidecar/override/append/dummy modifier、generated normalized row C# -> 当前 AppDomain factory 反扫、managed row/JSON/ScriptableObject Runtime authority。
12. 删除 generator 逐 phase 直写 active `.gen.cs`/manifest、失败后继续保存、candidate gate 前 orphan cleanup；配置只从 content-addressed candidate 经 `08` 原子 promotion。
13. 删除 TagMask 固定容量/unknown no-op authority、Attribute 线性扫描与 runner/ASC factory 硬编码容量作为目标实现；切换到 promoted LayoutProof/CapacityProof 与 admission consumer。
14. 删除 `InputHash`/整数 SchemaVersion/revision 单独 install 判定及旧 Catalog Runtime fallback；Session 只接受四元 install identity 精确匹配。

## 验收

- Tier A 9101–9104 legacy characterization 与 Tier B target semantic conformance 分开通过；有意 breaking 项使用新批准 golden。
- Ready 前 ingress 不可见；Spawn/Grant 任一失败无半初始化单位、无部分 mutation。
- 同 Session 多战局终局隔离、FinalDrain、BattleOutcome freeze、corpse ASC retention 与 teardown cleanup audit 全绿。
- validation不再硬编码旧 system/group/data-flow 名。
- static/runtime deletion gate逐项为零，且没有 compatibility flag/fallback。
- AutoChess、Editor watcher、binding不取得 raw Runtime ECS capability。
- Runtime/Generated/AutoChess编译和 Unity headless smoke通过。
- static deletion gate 对 sidecar semantic keys、AppDomain row factory authority、active-root direct write、partial manifest save、pre-gate orphan cleanup、managed config Runtime lookup、fixed TagMask authority、hard-coded capacity authority、Runtime Catalog fallback 逐项为零。
- 第一次/第二次生成、不同 project path/culture 的 canonical graph、四 hash 与 artifact bytes 相同；任一 candidate/promotion 故障时 active generation byte-for-byte 不变。
- AutoChess typed support RuleId、provenance、Layout/CapacityProof、consumer map 与 candidate/promotion evidence 均进入 validation report；只有 hint/high-water 不算完成。

## 交还

完整删除表、配置 candidate/promotion/四 hash/byte proof、业务 golden diff、adapter capability map、仍未跑的 scale/Profiler 项并转交 V7。
