# Definition / 配置事实

## 已成立事实

1. `GASDefinitionTable` 已统一 Ability、GameplayEffect、AttributeSet、Attribute、GameplayTag、GameplayCue summary / lookup contract。
2. `GASDefinitionGeneratedAdapter` 已把 generated/Luban source 映射到 unified definition table。
3. `GameplayEffectConfigRegistry` 是 GE definition cache lifecycle owner。
4. `GASGeneratedDefinitionBakingPlan / BakeContract / BakePipeline / RuntimeIntegrationPlan` 已形成 generated -> bake -> runtime integration 的 contract 链。
5. AutoChess 已有 generated package、SourceGenerator output/export/toolchain/authoring snapshot 和真实 Luban process gate 样板。

## 边界

1. Definition summary 不携带 spec/context/runtime state。
2. generated pipeline 不生成 Ability / GE lifecycle。
3. 自动生成目录不纳入版本控制。
