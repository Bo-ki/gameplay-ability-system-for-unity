# V1 Definition Catalog 与稳定身份

> 状态：V0 通过后领取 | 前置：V0

## Owner 输入

- [当前迁移基线](../../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)
- [目标 Spec：Definition、Capture、Identity](../../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md)
- [Luban/SourceGenerator Owner](../../01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md)
- [AutoChess 配置 Owner](../../01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md)
- [配置语义编译/CapacityProof Owner](../../01-目标态架构共识/25-配置语义编译契约与CapacityProof统一裁决Spec.md)

## 目标

建立 v1 唯一 canonical semantic graph、typed Catalog schema、layout/proof 与四元 install identity，使后续 slab/kernel 不依赖 raw Entity、被压平的 requirement range、已编译 row factory 或混代 generated artifact。

## 执行范围

1. Application/Ongoing/Removal/Immunity 独立 schema、range 与 validation。
2. Definition -> Spec 所需 SetByCaller、target data、capture descriptor 与 context 字段。
3. ASC session/stable id/generation、Owner/Avatar binding generation。
4. Ability/Activation/Effect handle 的 owner + slot + generation 协议。
5. 同步 Luban/CodeGen/template/generated output/catalog builder；generated 只保留 pure glue。
6. 冻结 target policy 数据：FrozenASC/RequireSameAvatar/FollowASC/FrozenSpatial 与 TargetLifePolicy；缺失策略不得 fallback self。
7. Luban 作为 authoring 语义唯一权威，Runtime compiled Blob 作为 Session immutable snapshot；同字段冲突、非法 enum/range、缺失 ValueView 必须携 provenance bake fail。
8. 单次从 Luban schema/rows 构建 `CanonicalNormalizedSemanticGraph`；generated normalized row C# 只作派生证据，不得扫描当前 AppDomain factory 作为 graph/hash 输入。
9. 生成 Cost/Cooldown/Capture/Stack/DirectEffect/Spawn typed schema、逐字段 support result/RuleId/provenance、LayoutProof、dependency graph 与 CapacityProof；未知字段默认 deny。
10. 生成 content-addressed candidate、四元 install identity与逐 artifact byte hash；semantic/layout/capacity/compile/AOT/negative/scenario 全绿后才原子 promotion。
11. Catalog install 以 `{SchemaHash, ContentHash, LayoutHash, ArtifactManifestHash}` 精确匹配；`SourceInputHash` 只作复现，不承担 Runtime 兼容。

## 验收

- phase round-trip 测试证明各 requirement 不再互相压平。
- Source/Target × Snapshot/Live descriptor 四组合可生成和解析。
- public/boundary protocol 无 raw Entity。
- generated artifact 无 System/query/ECB/lifecycle owner。
- sourcegen 无字段 override、dummy modifier 或 sidecar semantic overlay；Luban 9203 非法 enum 负例可定位到 workbook/table/row/column。
- 9203 与 9207 的 target schema 全字段可进入 compiled Blob；9204 legacy child 不进入目标 catalog。
- Catalog 四元 install identity mismatch 显式失败，不走旧 schema、旧 Catalog、managed row、sidecar 或 ScriptableObject fallback。
- 同输入第一次/第二次生成、不同 project path/culture 的 graph、四 hash 与 artifact bytes 相同；不依赖 Unity 再编译一次。
- Cost/Cooldown 对 cue/application/ongoing/removal/grant/live/execution/dynamic-child 的 allow/deny、RuleId 与 provenance 固定且负例全绿。
- Cross-ASC Live 缺 FrozenProjectionPayload、revision/value 原子配对、bytes/fanout/coalesce 任一项时 candidate 失败。
- manifest 每个 artifact 都有 canonical path/kind/owner/byte hash；任一 phase/gate/promotion 故障时 active generation byte-for-byte 不变。
- 回滚通过精确历史 `ArtifactManifestHash` 的新原子 promotion 完成；Runtime 没有自动 fallback 分支。

## 交还

Schema/contract/layout/proof diff、candidate/artifact bytes 对账、promotion/identity protocol tests 与 V2/V3 consumer说明；不创建新 runtime backend selector。
