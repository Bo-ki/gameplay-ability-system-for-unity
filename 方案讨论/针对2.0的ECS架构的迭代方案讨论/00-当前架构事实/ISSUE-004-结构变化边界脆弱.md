# ISSUE-004 结构变化边界脆弱

> 最近复核：2026-06-02 | 状态：Active | 严重度：P0

## 当前结论

结构变化边界已经比旧代码更清晰：`GASStructuralCommitSystemGroup`、`BeginGASStructuralCommitECBSystem`、`EndGASStructuralCommitECBSystem` 是真实物理 gate。问题是 gate 存在不等于所有结构变化都已进入 gate。

## 已缓解部分

1. 5 段主链中有独立 `GASStructuralCommitSystemGroup`。
2. 多个 runtime systems 已通过 `EndGASStructuralCommitECBSystem.Singleton` 记录结构变化。
3. generated active effect mutation/remove 使用 structural ECB。

## 仍成立风险

1. `ASCCommandGateway` 仍直接创建 request entity。
2. `GASManager.Initialize()`、config/prototype/cache、AutoChess bridge 仍有直接 `CreateEntity` / `DestroyEntity`。
3. 这些低频/边界路径需要明确分类，否则容易回流到 hot path。
4. 缺少 Journaling 证明结构变化来源和 playback phase。

## 代码证据

| 事实 | 文件 |
|---|---|
| structural commit group | `GASGroups.cs` |
| schedule registration | `GASSystemScheduleContract.cs` |
| boundary direct request creation | `ASCCommandGateway.cs` |
| generated structural ECB | `RuntimeActiveEffect.gen.cs` |
| AutoChess bridge direct EM | `AutoChessGasCoreBridge.cs` |

## 退出条件

1. 所有 runtime hot path 结构变化通过 Begin/End structural ECB 记录。
2. Boundary facade 创建 request 的路径被统一 command sink 接管，或明确证明为低频外部入口。
3. Official diff 能证明结构变化 phase 与数量符合预期。
