# V1 Definition Catalog 与稳定身份

> 状态：V0 通过后领取 | 前置：V0

## Owner 输入

- [当前迁移基线](../../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)
- [目标 Spec：Definition、Capture、Identity](../../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md)
- [Luban/SourceGenerator Owner](../../01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md)
- [AutoChess 配置 Owner](../../01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md)

## 目标

建立 v1 唯一 Catalog schema 与 stable identity，使后续 slab/kernel 不依赖 raw Entity 或被压平的 requirement range。

## 执行范围

1. Application/Ongoing/Removal/Immunity 独立 schema、range 与 validation。
2. Definition -> Spec 所需 SetByCaller、target data、capture descriptor 与 context 字段。
3. ASC session/stable id/generation、Owner/Avatar binding generation。
4. Ability/Activation/Effect handle 的 owner + slot + generation 协议。
5. 同步 Luban/CodeGen/template/generated output/catalog builder；generated 只保留 pure glue。
6. 冻结 target policy 数据：FrozenASC/RequireSameAvatar/FollowASC/FrozenSpatial 与 TargetLifePolicy；缺失策略不得 fallback self。
7. Luban 作为 authoring 语义唯一权威，Runtime compiled Blob 作为 Session immutable snapshot；同字段冲突、非法 enum/range、缺失 ValueView 必须携 provenance bake fail。

## 验收

- phase round-trip 测试证明各 requirement 不再互相压平。
- Source/Target × Snapshot/Live descriptor 四组合可生成和解析。
- public/boundary protocol 无 raw Entity。
- generated artifact 无 System/query/ECB/lifecycle owner。
- sourcegen 无字段 override、dummy modifier 或 sidecar semantic overlay；Luban 9203 非法 enum 负例可定位到 workbook/table/row/column。
- 9203 与 9207 的 target schema 全字段可进入 compiled Blob；9204 legacy child 不进入目标 catalog。
- Catalog version mismatch 显式失败，不走旧 schema fallback。

## 交还

Schema diff、生成物对账、identity protocol tests 与 V2/V3 使用说明；不创建新 runtime backend selector。
