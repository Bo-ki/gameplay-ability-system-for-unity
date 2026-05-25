# ISSUE-005 Generated 链路未反哺 Runtime Core

## 状态

| 字段 | 内容 |
|---|---|
| 状态 | Active |
| 严重度 | P1 |
| 最近复核 | 2026-05-24 |
| 所属层 | Definition & Generation Layer / GAS Runtime Core Layer |

## DOTS规则引用

| 规则编号 | 规则摘要 | 关联原因 |
|---|---|---|
| `BAKE-01` | Definition 不携带 runtime state | generated artifact 应止于 static lookup/blob/bake plan |
| `BAKE-02` | Baker 只添加不读取；禁止读取已有 component | SourceGenerator 不能越权生成 gameplay lifecycle |
| `BAKE-03` | Baking System 手动追踪依赖和增量还原 | generated 链路尚未对接 Unity baking pipeline |
| `BLOB-01` | BlobAsset 承载 immutable 静态定义 | GE/Ability 定义应转为 BlobAsset 而非 runtime component |
| `BLOB-02` | BlobBuilder 标准构建模式 | Luban 输出可对接 BlobBuilder.ConstructRoot/Allocate |
| `CONTENT-01` | Prefab 限于真实资源 entity 模板 | GE 定义不用 prefab，用 BlobAsset + static table |
| `CONTENT-02` | WeakObjectReference/UnityObjectRef 管理跨 World 资源引用 | generated 链路的资源引用边界 |
| `CASE-07` | Baker + Blob — Definition & Generation Layer 的 definition 承载 | generated artifact 应落到 Baker/Blob |
| `CASE-39` | Baker 只添加不读取 | generated code 注入 Baker 的约束 |
| `CASE-40` | Baker 必须无状态 | generated Baker 代码的安全约束 |
| `CASE-41` | Baking System 必须手动追踪依赖和增量还原 | generated 链路进入 baking pipeline 的要求 |
| `PRF-11` | 控制 Prefab 数量；静态定义用 BlobAsset | generated 链路不应输出 prefab |

## 问题陈述

Definition / Luban / SourceGenerator / Bake / RuntimeIntegration contract 已有基础，但它还没有充分转化为 Runtime Core hot path 的静态查表、query layout、archetype plan 和 generated glue 优势。配置链目前更多证明“能生成和校验”，还没有成为 Core pipeline 的主输入。

## 当前证据

代码证据：

1. `GASDefinitionGeneratedAdapter` 已有 generated source 到 DefinitionTable 的 adapter surface：`Assets/GAS/Runtime/Definition/GASDefinitionGeneratedAdapter.cs:50-70`, `Assets/GAS/Runtime/Definition/GASDefinitionGeneratedAdapter.cs:154`。
2. `GASDefinitionTable` 已有 definition summary 结构：`Assets/GAS/Runtime/Definition/GASDefinitionTable.cs:48-70`, `Assets/GAS/Runtime/Definition/GASDefinitionTable.cs:294`。
3. Runtime integration plan 仍包含 `DeferredBoundary`、`RuntimeLifecycleDeferred`、`ManagedPresentationDeferred`、`ObservationOnly` 等边界状态，说明 generated 链路尚未完全进入 runtime 主干：`Assets/GAS/Runtime/Definition/GASGeneratedDefinitionRuntimeIntegrationPlan.cs:6-42`。
4. AutoChess generated definition rows 仍在 `Assets/GAS/Runtime/Demo/AutoChess` 下，且单文件超过 3600 行；它具备 package/output/manifest 路径，但仍混在 Runtime Demo 边界中：`Assets/GAS/Runtime/Demo/AutoChess/HeadlessAutoChessGeneratedDefinitionRows.cs:1-70`, `Assets/GAS/Runtime/Demo/AutoChess/HeadlessAutoChessGeneratedDefinitionRows.cs:2908-2948`。

记录证据：

1. 路线反思指出当前吸收了 Luban / SourceGenerator 样板，但没有同步兑现 generated runtime glue：`../../../迭代记录/92-T6-CHESS-AL-RouteReflectionFromPlans12To15.md:51-58`。
2. 方案14/15 的配置链方向被保留，但必须限制为 Definition & Generation Layer 输入，不能生成 runtime lifecycle：`../../../迭代记录/92-T6-CHESS-AL-RouteReflectionFromPlans12To15.md:29-49`。

## 执行路径

```text
Luban / generated source
-> generated package / manifest / validation
-> GASDefinitionGeneratedAdapter
-> GASDefinitionTable / BakePlan / RuntimeIntegrationPlan
-> 当前 Runtime Core 仍大量使用手写 registry、EntityManager 查询、runtime request/entity lifecycle
```

## 影响

1. 配置链完整性没有转化为 ECS hot path 的布局优势。
2. Runtime Core 重构时仍需要手工维护 lookup、archetype、structural plan，容易偏离 Definition 权威。
3. AutoChess Demo 生成链混在 Runtime Core 目录，会继续放大边界问题。

## 根因反推

Generated 链路的目标不是“生成更多 C# 文件”，而是把配置权威转化为：

1. static lookup。
2. blob / bake plan。
3. query layout plan。
4. structural change plan。
5. validation graph。

SourceGenerator 不能越权生成 gameplay lifecycle，但必须给 Runtime Core 提供可 Burst 消费的静态输入。

## 目标态入口

1. `../../01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`
2. `../../01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`
3. `../../01-目标态架构共识/02-四层架构Spec.md`

## 任务入口

1. `../../02-主线任务树/T2-Definition_Luban配置权威/LubanSourceGenerator配置链.md`
2. `../../02-主线任务树/T5-Burst_Generated后置优化/README.md`

## 退出条件

1. Runtime Core 主链至少有一条关键链路消费 generated static lookup / bake plan / query layout。
2. AutoChessDemo generated output 从 Runtime Core Demo 目录迁出。
3. Definition validation 能阻止 runtime state、EntityManager、lifecycle system 进入 generated artifact。
