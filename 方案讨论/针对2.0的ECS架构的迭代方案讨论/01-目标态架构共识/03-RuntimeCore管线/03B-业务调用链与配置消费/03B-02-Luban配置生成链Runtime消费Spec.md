# Luban 配置生成链 Runtime 消费 Spec

## 结论

Luban rows 是策划源；Runtime 唯一消费物是已验证且 Session 内不可变的 Catalog Blob、Layout Blob 和生成 pure glue。配置链不得生成第二 Runtime。

## 生成产物

- Ability/Effect/Attribute/Tag/Cue Definition Blob。
- AttributeLayout 与 TagCatalog 的 dense index/ancestor chain。
- Application/Ongoing/Removal/Immunity requirement 独立 ranges。
- CaptureProjectionContract、Source/Target × Snapshot/Live 描述与 phase。
- DirectEffectProgram 拓扑、节点/输出上限。
- ongoing/tag/live dependency graph 与 cycle validation。
- static code→index lookup、pure requirement/magnitude/target evaluator。
- schema/content hash、ScaleProfile memory report、Editor metadata 和 validation diagnostics。

## Runtime resolve

grant 阶段将 AbilityDefinitionId 写入 `GrantedAbilitySlot`；apply 阶段以 DefinitionIndex 构造 `EffectApplicationSpec`；hot path 只使用预解析 index/range，不进行字符串、Dictionary、JSON 或 Luban managed row 查询。

一个源 Spec 对多个目标可共享 source-frozen 数据，但每个目标拥有独立 target capture/application record。跨 tick Spec 由 Continuation 的不可变 payload 或独立 PendingEffectSpec slot 持有，不能引用 tick scratch。

## Session 规则

install 时校验 Unity/package/schema/content/layout/tick-rate/scale-profile hash；失败不得启动 simulation。Catalog/Layout 变化通过新 Session/World 生效，不原地修改存活 ASC。

## Bake failure

- 未声明的动态 Capture/Execution API。
- 无法证明 ScalarSnapshot 安全却强制 scalar。
- 未实现的跨 ASC Live Capture 或潜在跨 ASC live cycle。
- DirectEffectProgram 非闭合、非静态有界或动态回边。
- Tag/Attribute index、requirement phase、provenance 或容量报告缺失。
- 请求 Prediction/replication schema。
