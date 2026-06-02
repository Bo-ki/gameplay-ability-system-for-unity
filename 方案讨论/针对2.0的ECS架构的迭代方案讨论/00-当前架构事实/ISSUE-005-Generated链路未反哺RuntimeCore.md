# ISSUE-005 Generated 链路已反哺但缺同等审查

> 最近复核：2026-06-02 | 状态：Active | 严重度：P0

## 当前结论

旧标题已经不准确：generated 链路现在已经反哺 Runtime Core。当前问题是 generated runtime 已经成为主链事实，但还没有被同等 DOTS 审查覆盖。

## 已缓解部分

1. `RuntimeSystemRegistration.gen.cs` 注册 7 个 generated systems。
2. `GASDefinitionCatalogBlob` 被 generated runtime 读取。
3. Ability catalog commit、GE command normalize、spec build、attribute reduce、active effect mutation/tick/remove 都已进入执行链。

## 新风险

1. generated systems 修改 runtime state，不能被视为“只读生成物”。
2. generated code 中存在主线程 query、buffer for loop、`Complete()`。
3. generated active effect lifecycle 与 handwritten ExecutionCalculation/Attribute/Fact projection 的 ordering 需要证据。
4. generated catalog lookup 的 revision/lifecycle owner 需要明确。

## 代码证据

| 事实 | 文件 |
|---|---|
| generated registration | `RuntimeSystemRegistration.gen.cs` |
| catalog lookup | `DefinitionCatalog.gen.cs` |
| runtime glue | `RuntimeDefinitionGlue.gen.cs` |
| ability activation | `RuntimeAbilityActivation.gen.cs` |
| instant effect | `RuntimeEffectInstant.gen.cs` |
| active effect | `RuntimeActiveEffect.gen.cs` |

## 退出条件

1. Generated runtime 纳入每次 Runtime 审查范围。
2. 每个 generated system 标注 phase、reads/writes、query pattern、dependency policy。
3. generated output 有 static validation 或 codegen report，证明不会生成违反当前 DOTS 规则的 hot path。
