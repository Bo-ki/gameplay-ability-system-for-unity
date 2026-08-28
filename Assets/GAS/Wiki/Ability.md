# EX-GAS Wiki - Ability

更新时间：2026-05-23

> 迁移提示：本文保留旧 Ability ECS 组件和 system 名称作为历史记录；这些类型已从 Runtime v1 删除。当前 Ability 行为由 `GasRuntimeWorldOwner` 持有的 definition/catalog、`GasCommandPort` 接收 intent，并由 `GasTickDag` 推进；请以 `Assets/GAS/Runtime/V1` 及 Runtime v1 测试为准。

Ability 表示游戏中可以被触发的行为或技能。Runtime v1 中，Ability 是由 owner 管理的不可变 definition、运行时槽位与 command/fact 事务，不再通过旧 ECS request component 或兼容 facade 暴露。

## 当前职责

Ability 负责“产生意图”：

- 通过 tag / cost / cooldown / block relation 判断是否可以激活。
- 激活成功后在 OwnerWave 提交 owner-local commit plan，更新 activation state，并输出 Boundary facts。
- Timeline action 只通过 `GasCommandPort` 提交后续 command；Cue 与生命周期结果都从 transaction 产生的 fact 观察。
- Ability 本身不直接修改 Attribute；属性变化必须通过 GE / ExecutionCalculation 链路进入。

## 激活链路（Runtime v1）

Runtime v1 的唯一流程是：

```text
外部 intent / AI
  -> GasCommandPort
  -> GasTickDag
  -> owner Ability transaction
  -> Boundary fact / read model
```

下方旧组件/system 流程仅用于迁移对照。

```text
外部输入 / AI / ECS driver
  -> CAbilityCommandRequest
  -> SAbilityCommandRequest
  -> CAbilityInTryActivate
  -> STryActivateAbility
  -> CAbilityCommitRequest
  -> SAbilityCommit
  -> AbilityCommitSucceeded / AbilityCommitFailed fact
  -> cost / cooldown / activation GE request
  -> CAbilityRuntimeState phase 更新
```

旧版 `TryActivateAbility(...)` 只是创建 command request；Runtime v1 改为向 `GasCommandPort` 提交 intent，实际结果通过 Boundary fact / read model 读取。

## Commit Gate（历史流程示意）

旧版 `SAbilityCommit` 曾是 Ability 激活判定中心；Runtime v1 改由 owner transaction 负责：

- `CAbilityCommitRequest`
- `CAbilityBaseInfo`
- `CAbilityRuntimeState`
- `CAbilityConfig`
- owner ASC 的 Attribute / Tag 数据
- activation required / blocked tags
- cooldown tag / cost attribute
- active ability block relation

成功时：

- 输出 `AbilityCommitSucceeded`。
- 创建 cost / cooldown / activation GE request。
- 写 activation owned tags。
- 按配置请求 cancel matched ability。
- 将 Ability 置为 active phase。

失败时：

- 输出 `AbilityCommitFailed`。
- 移除 commit request。
- 不创建 GE request。
- 不添加 active state。

## End / Cancel（历史流程示意）

旧版 End / Cancel 通过生命周期 request component 和 cleanup system 清理；这些类型已删除。Runtime v1 由 owner transaction 统一处理 End / Cancel，并输出 Boundary fact。

旧版 producer 包括（仅作迁移对照）：

- 外部显式 End / Cancel。
- Remove Ability。
- ASC destroy。
- Timeline completion。
- Attribute threshold lifecycle request。
- granted GE deactivation / removal。
- cancel matched ability。

## Timeline（Runtime v1 迁移规则）

Timeline 不再是托管 task 自由执行链。当前原则：

- Timeline 表只保存 action clip 和参数。
- `GasCommandPort` 负责 action dispatch。
- ApplyEffects / ApplyCost / ApplyCooldown / PlayCue / PlayCuePreset 都输出 command / fact。
- 非 manual end 的 timeline completion 由 owner transaction 归一化。

## 新增 Ability 行为（Runtime v1）

新增行为的推荐步骤：

1. 新增参数 `XParam`，用 `[BeanField]` / `[BeanPolymorphicField]` 暴露需要入表的字段。
2. 在编辑器 schema 注册 Ability definition。
3. 更新 CodeGen normalized row 映射。
4. 新增 V1 definition / transaction 数据。
5. 让 `GasTickDag` 读取 command 并输出 fact。
6. 更新 V1 tick 顺序并补验证。
7. 补测试证明 intent、runtime state、fact、presentation 链路闭合。

不要新增 `AbilityLogicBase`、`AbilitySpec` 或托管 Ability 生命周期对象。

## 标签条件

Ability 激活相关标签条件统一编码在 definition catalog 的 `GasRequirementBlob`：

- `ActivationRequiredTags`：required query 必须满足。
- `ActivationBlockedTags`：blocked query 命中即失败。
- active ability block relation 命中时输出 `AbilityActivationBlockedByAbility` fact。

标签条件失败由 Runtime v1 transaction 的纯 evaluator 判定，并以稳定 failure code 写入 fact；当前不再维护独立的激活失败枚举。
