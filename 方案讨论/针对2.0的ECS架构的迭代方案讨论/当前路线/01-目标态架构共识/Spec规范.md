# 目标态 Spec 规范

## 目标

`01-目标态架构共识` 是当前迭代设计的主 Spec 体系，不再作为讨论纪要或完成清单。它必须能直接指导后续实现、评审和验收。

## Spec 必备结构

每个目标态 Spec 文档使用同一模板：

```text
# Spec 名称

## 目的
## 范围
## 非目标
## 术语
## 架构视图
## 核心契约
## 数据所有权
## SystemGroup / Phase 顺序
## 禁止方向
## 验收方式
## 关联任务
## 关联 ADR / 方案来源
```

## 图表要求

目标态 Spec 必须提供软件工程常用图表。图表源码优先使用 Mermaid 写入 Markdown。

| 图表 | Mermaid 类型 | 用途 |
|---|---|---|
| 分层架构图 | `flowchart` | 说明系统边界和依赖方向 |
| UML 类图 | `classDiagram` | 说明核心数据结构、接口和契约关系 |
| 时序图 | `sequenceDiagram` | 说明一次业务链路或工具链链路 |
| 状态图 | `stateDiagram-v2` | 说明 Ability、Effect、Debugger 等生命周期 |
| 数据流图 | `flowchart` | 说明 command/spec/delta/fact/outbox 流 |
| 调度表 | Markdown table | 说明 SystemGroup 顺序和 phase barrier |

Runtime Core、Runtime Core Debugger、Luban SourceGenerator 三类 Spec 必须至少包含：数据流图、UML 类图、时序图。

## Unity Entities 校准要求

Runtime Core 相关 Spec 还必须显式回答 Unity Entities 1.4.6 的机制落点：

| 机制 | Spec 必须说明 |
|---|---|
| SystemGroup / update order | phase 对应哪个 SystemGroup，在哪个顺序执行 |
| ISystem / Job | hot path 使用 `ISystem`、`IJobEntity`、`IJobChunk` 或其他迭代方式 |
| ECB / structural change | 哪些 phase 允许 ECB playback，哪些 phase 禁止结构变化 |
| DynamicBuffer | buffer 生命周期、容量、清空时机、pressure counters |
| Enableable / stable archetype | 高频状态开关是否避免 add / remove component |
| Blob / Baker / generated lookup | Definition 如何进入 Burst-friendly runtime 输入 |
| EntityQuery filter | 是否使用 change filter；若使用，必须说明 chunk 粒度限制 |
| Profiler / Journaling | Debugger counters 如何对齐 Unity 外部证据 |
| API selection | 是否评估 `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md` 中的候选 API；当前选择、拒绝理由和重新选型触发条件是什么 |
| Query / Filter / Allocator / Dependency | query/filter 口径、frame scratch allocator、lookup update、dependency wait 是否进入 phase contract |
| Chunk / Buffer layout | archetype / chunk 利用率、DynamicBuffer internal capacity / spill / externalized 状态如何验收 |
| Burst calculation | ExecutionCalculation / MagnitudeResolver 是否用 Burst job、generated static switch 或经证明的 FunctionPointer |
| Official DOTS coverage | 是否先读取 `UnityDOTS官方文档参考/README.md`，再按 `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md` 检查官方文档覆盖矩阵、`ODF-*` 规则和缺口反哺路径 |

缺少上述机制落点的 Runtime Core Spec 不能作为实现任务的唯一依据；必须先补 `UnityDOTS官方文档参考/主题/01-Entities系统与World.md` 或对应 Spec。

Runtime Core 相关 Spec 还必须引用 `UnityDOTS官方文档参考/主题/90-规则编号索引.md` 中的具体规则编号。示例：

```text
适用规则：SYS-01, JOB-01, SC-01, ECB-02, BUF-04, DBG-02
```

不能只写“遵守 Unity ECS 规范”。

Runtime Core 相关 Spec 还必须包含 `DOTS API 选型` 章节，至少覆盖：

1. 数据承载：component / DynamicBuffer / Blob / NativeContainer / cleanup / chunk component。
2. 遍历方式：`IJobEntity` / `IJobChunk` / `SystemAPI.Query` / `ComponentLookup` / `BufferLookup`。
3. 结构变化：Enableable / EntityQuery bulk / ECB / `EntityManager` / `ComponentTypeSet`。
4. 并行输出：NativeStream / NativeQueue / ParallelWriter / deterministic merge。
5. 证据指标：buffer pressure、lookup count、chunk skip、structural count、sync point、GC、Profiler / Journaling 对照。
6. frame arena：`WorldUpdateAllocator` / group allocator / TempJob / Persistent / Rewindable allocator 的生命周期。
7. query 口径：filtered / unfiltered、enableable wait、change filter chunk 粒度、shared filter 是否低频稳定。
8. Burst 口径：Burst target、warmup、static readonly / Blob lookup、FunctionPointer overhead。
9. 官方文档覆盖：官方文档参考体系主题、相关 `ODF-*` 规则、PackageCache 证据、采用 / 拒绝 / 暂不相关理由、反哺 owner 和验收指标。

缺少上述 API 选型章节时，Spec 只能作为概念草稿，不能作为实现任务主依据。

## 官方文档覆盖维护规则

1. Runtime Core、Debugger、Luban、Demo 相关 Spec 必须先把 `UnityDOTS官方文档参考/README.md` 作为官方 DOTS 文档主题入口，再引用具体单主题文档。
2. `UnityDOTS官方文档参考` 维护主题定位、第一性版本、阅读顺序、规则层关系和反哺路径。
3. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md` 记录覆盖矩阵和缺口路由；它不是任务状态文档。
4. 如果新增官方文档结论改变目标态，必须修改对应业务 Spec，而不能只在官方参考主题里追加摘要。
5. 如果新增官方文档结论影响任务拆分，必须同步更新 `02-主线任务树` 对应节点。
6. 如果新增官方文档结论暴露当前实现问题，必须同步更新 `00-当前架构事实` 或核心问题诊断。
7. Spec 中引用官方文档时优先使用本地 PackageCache 路径和行号；在线文档链接作为版本对照或阅读入口。

## 图表维护规则

1. 每个图节点必须能映射到当前代码实体、目标契约或明确未来模块。
2. 每条箭头必须标注命令、数据、事实、状态或生命周期动作。
3. 图中出现的模块必须在“核心契约”中解释。
4. 任务交还时若改变节点或箭头，必须同步更新图表。
5. PNG 只能作为导出物，不能替代 Mermaid 源。

## 与方案15的关系

可吸收：

1. 四层架构表达方式。
2. Luban -> SourceGenerator -> ECS 胶水链路视角。
3. GASDebugger 时序图导出视角。
4. 自走棋复杂业务链验证架构的写法。
5. 架构质量对比和责任边界表。

不吸收：

1. 托管 EventBus / Debugger 作为 simulation 路由。
2. SourceGenerator 直接生成 gameplay lifecycle。
3. 自动 System 发现替代显式 `GASSystemScheduleContract`。
4. 示例类名和代码结构直接变成当前实现蓝图。


