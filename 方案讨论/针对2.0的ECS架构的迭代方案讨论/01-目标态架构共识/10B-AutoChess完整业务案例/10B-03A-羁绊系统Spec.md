# 10B-03A 羁绊系统 Spec

## 结论

羁绊是 Tier C 扩展样例，也是 Shell/业务规则生成的低频 intent：阵容稳定后计算 tier diff，再向各目标 ASC 请求应用/移除预定义 GameplayEffect。羁绊不直接写 Attribute/Tag/ActiveEffect；本页不构成当前 AutoChess 主战覆盖证据。

## 配置

```text
SynergyDefinition
  TraitTag
  Tier thresholds
  Tier EffectDefinitionId
  Eligible unit query policy
  Source/stack key policy
```

生成期校验 tier 单调、Effect 引用、source key 和移除可逆性。每个羁绊 tier application 使用稳定 `SynergyInstanceId + UnitAscId` 作为 provenance，保证升/降 tier 精确移除旧贡献。

## 数据流

```text
Roster stable snapshot
 -> pure synergy count/tier calculation
 -> diff old/new tier
 -> CommandPort Apply/Remove requests
 -> next eligible SimulationTick target-owned Effect transaction
 -> immutable facts/read model
```

阵容在 Kernel 执行中发生变化时不直接重入当前 target transaction；新的羁绊命令按 Boundary ingress 规则进入后续 tick。

## 验收

- 未启用羁绊时仍可独立完成 Tier A characterization 与 Tier B semantic conformance。
- 同一 roster/order 产生相同 tier diff 和 request order。
- tier 升降/单位离场只撤销相应 source contribution。
- 多羁绊同时影响同一属性时由 target single writer/canonical order 处理。
- 羁绊模块关闭后 Core 通用类型与调度不变化。
