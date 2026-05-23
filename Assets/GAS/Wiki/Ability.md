# EX-GAS Wiki - Ability

更新时间：2026-05-23

Ability 表示游戏中可以被触发的行为或技能。当前 2.0 架构中，Ability 不是托管生命周期对象，而是 ECS Entity 上的一组 definition、runtime state、request marker、buffer 和系统行为。

## 当前职责

Ability 负责“产生意图”：

- 通过 tag / cost / cooldown / block relation 判断是否可以激活。
- 激活成功后输出 facts，并创建 cost / cooldown / activation GE request。
- Timeline action 或专用 Ability system 可继续创建 GE request、Cue request 或 lifecycle request。
- Ability 本身不直接修改 Attribute；属性变化必须通过 GE / ExecutionCalculation 链路进入。

## 激活链路

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

`TryActivateAbility(...)` 只是创建 command request。实际结果要等 ECS system 消费后，通过 observation / fact / runtime state 读取。

## Commit Gate

`SAbilityCommit` 是当前 Ability 激活判定中心，负责读取：

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

## End / Cancel

End / Cancel 不应由任意 system 手写 marker。统一入口是：

- `AbilityRuntimeActions.RequestAbilityEnd(...)`
- `AbilityRuntimeActions.RequestAbilityCancel(...)`

该入口会同步写 lifecycle request component 和 request fact。`SAbilityStateCleanup` 消费 `CAbilityInTryEnd` / `CAbilityInTryCancel` 后做最终清理，并输出 `AbilityEnded` / `AbilityCanceled`。

当前已统一的 producer 包括：

- 外部显式 End / Cancel。
- Remove Ability。
- ASC destroy。
- Timeline completion。
- Attribute threshold lifecycle request。
- granted GE deactivation / removal。
- cancel matched ability。

## Timeline

Timeline 不再是托管 task 自由执行链。当前原则：

- Timeline 表只保存 action clip 和参数。
- `SAbilityTimelineAction` 负责 action dispatch。
- ApplyEffects / ApplyCost / ApplyCooldown / PlayCue / PlayCuePreset 都输出 ECS request / fact。
- 非 manual end 的 timeline completion 由 lifecycle request system 归一化。

## 新增 Ability 行为

新增行为的推荐步骤：

1. 新增参数 `XParam`，用 `[BeanField]` / `[BeanPolymorphicField]` 暴露需要入表的字段。
2. 在 `EditorAbilityHelper.GetAbilityExecutionSchemas()` 注册 AbilityExecution schema。
3. 更新 `CodeGeneratorLubanPart.GetAbilityConfig(...)`，把 Luban Bean 映射为 `AbilityComponentConfig`。
4. 新增 runtime component / buffer。
5. 新增 `ISystem`，读取 ECS state 和 facts，输出 request / fact。
6. 把 system 加入 `GASSystemScheduleContract`。
7. 补测试证明 commit、request、runtime state、fact、presentation 需要的链路闭合。

不要新增 `AbilityLogicBase`、`AbilitySpec` 或托管 Ability 生命周期对象。

## 标签条件

Ability 激活相关标签条件统一为 `TagRequirementData`：

- `ActivationRequiredTags`：required query 必须满足。
- `ActivationBlockedTags`：blocked query 命中即失败。
- active ability block relation 命中时输出 `AbilityActivationBlockedByAbility` fact。

失败原因会进入 `AbilityActivationResult` 和 tag requirement failure code，供 replay / log / test 读取。
