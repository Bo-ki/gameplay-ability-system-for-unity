# TagRequirement Query / Definition Glue 事实

> Owner：`00-当前架构事实/架构重划分审查事实` | 最近复核：2026-08-24 | 状态：当前事实

本页只回答当前 Definition/CodeGen/Runtime 如何保存和消费 requirement。目标 phase contract 由 `../../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md` 维护。

## 结论

当前 Catalog/Blob、`TagRequirementMask` 与 pure evaluator 是正向基础，但 CodeGen 把 Application、Ongoing 和 Immunity 压入同一 requirement range，Runtime 已无法恢复经典 GAS 的 phase 语义。因此“字段已生成”不能写成“ongoing/immunity 已实现”。

## 当前正向事实

1. `GASDefinitionCatalogBlob.Requirements` 保存 immutable requirement records。
2. `TagRequirementMask` 支持 All/Any/None 位掩码。
3. Ability activation required/blocked tag 能生成 requirement records。
4. GE application/ongoing/immunity tag 配置都会进入 Catalog。
5. generated lookup 与 magnitude/requirement pure glue 不拥有 query、ECB 或 lifecycle。

## 当前 phase flatten

`GasGlueCodeGenPhases` 当前按以下方式生成 GE requirements：

| 配置来源 | 生成 kind | 保存范围 |
|---|---|---|
| Application requirements | `RequiredTags` | GE 的唯一 `RequirementStart/Count` |
| Ongoing requirements | `RequiredTags` | 同一个 `RequirementStart/Count` |
| Immunity | `BlockedTags` | 同一个 `RequirementStart/Count` |

`GASRuntimeRequirementEvaluator.EvaluateGameplayEffectRequirements(...)` 遍历整个共享 range，只区分 RequiredTags/BlockedTags；没有 application/ongoing/immunity phase 参数。

证据：

- `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs:1207-1260`
- `Assets/GAS/Runtime/Definition/GASRuntimeDefinitionResolver.cs:465-541`

## 当前 Runtime 消费校准

- Ability commit 调用手写 `GASRuntimeRequirementEvaluator.EvaluateAbilityRequirements(...)`。
- instant GE spec build 调用手写 `GASRuntimeRequirementEvaluator.EvaluateGameplayEffectRequirements(...)`。
- active effect mutation apply 当前也调用手写 `GASRuntimeRequirementEvaluator`；旧文档中“仍调用 generated `GASGeneratedRequirementEvaluator`”的描述已过期。
- evaluator 当前只执行 tag match/not-match；Blob 中的 attribute compare 字段没有进入该分支。

证据：

- `Assets/GAS/Runtime/System/Ability/AbilityCommitSystem.cs`
- `Assets/GAS/Runtime/System/Effect/GEEffectInstantSystems.cs`
- `Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs:650-672`

## 当前语义后果

1. Ongoing requirement 在 apply gate 被一次性检查，没有基于 tag/attribute version 的持续重评。
2. `ActiveEffectSlotState.Inhibited` 和 `HasOngoingRequirements` 字段存在，但当前 owner-local apply 没有形成完整 inhibit/reactivate 主链。
3. Immunity 被折叠为普通 blocked application gate，无法表达独立 immunity 查询和原因。
4. Application、Ongoing、Removal、Immunity 没有各自的 failure/result fact。
5. 当前业务样本不能证明所有 All/Any/None、removal、immunity 与 attribute compare 组合。

## 当前 Tag 数据风险

- `TagMaskComponent` 固定为 256-bit。
- `TagHelper` 依赖 managed static dictionary 与 dense index 初始化；当前 Runtime 内没有可确认的通用 `InitTagMap` bootstrap 调用。
- `GameplayTagChangeProcessSystem.OnUpdate` 当前为空。
- bit presence 不能独立表达多个 Effect 同时 grant 同一 tag 的引用计数。

## 不能推出的结论

1. generated Catalog 非空不等于 requirement phase 完整。
2. pure evaluator 可 Burst 不等于 active effect ongoing lifecycle 已实现。
3. x50 历史样本通过不等于 immunity/ongoing/tag count 已覆盖。
4. `ActiveEffectSlotState.Inhibited` 字段存在不等于 runtime 会进入或退出 Inhibited。

## 消费规则

1. Runtime v1 必须从本页读取 phase flatten 事实，不得继续扩展共享 range。
2. 目标 schema、TagCount、stabilization 和 inhibition 规则只写入 `01/17`。
3. 执行任务只写入 `02/RuntimeV1不可兼容迁移/V1` 与 `V4`。
4. 触达 requirement generator 时，模板、generated output、Catalog validation 和 runtime tests 必须同一交还包对账。
