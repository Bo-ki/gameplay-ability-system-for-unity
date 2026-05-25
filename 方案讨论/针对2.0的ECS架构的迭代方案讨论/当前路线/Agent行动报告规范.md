# Agent 行动报告规范

## 目的

本规范约束 Agent 在领取任务并完成必要上下文搜寻后、正式执行文件修改 / 代码实现 / 大规模命令前，必须先向用户输出一轮行动报告。

行动报告的目的不是请求批准，而是暴露 Agent 当前掌握的任务上下文、执行意图、边界判断和将遵守的规范，方便用户判断流程是否按预期运行，并继续反哺项目规范。

## 适用范围

适用于以下任务：

1. 领取 `02-主线任务树/` 中的一级 / 二级 / 三级任务。
2. 进入 Goal 循环推进 Runtime、Demo、配置链、Debugger、文档治理等主线任务。
3. 执行会改变代码、文档、目录结构、配置、测试链路或架构事实的工作。
4. 从用户临时指令中推导出一个新的可执行工作块。

以下情况可简化或省略：

1. 用户只要求查询一个事实、运行一个只读命令或回答一个简单问题。
2. 修复明显打字错误、格式错误且不影响流程规范。
3. 用户明确要求只输出最终结果或暂停沟通。

## 触发时机

行动报告必须发生在：

```text
任务领取 -> 阅读任务提示词 -> 搜寻必要上下文 -> 行动报告 -> 直接执行
```

行动报告之后不需要等待用户批准。Agent 应继续执行；如果用户在执行前或执行中提出修正，再以最新用户输入为准调整。

## 字段分层

行动报告字段按三级分层。Tier 1 始终必填，Tier 2 按任务类型触发，Tier 3 按领域触发（不触发时完全省略该小节，不需要写"不相关"）。

### Tier 1 -- 始终必填

| 字段 | 要求 |
|---|---|
| 当前领取任务 | 任务名、任务 ID 或临时工作块名称 |
| 所属分支 | 一级主线、二级支线、三级任务路径；临时任务需说明归属 owner |
| 已读取上下文 | 已读取的任务树、目标态 Spec、当前事实 ISSUE、历史方案片段、代码路径或验证摘要 |
| 当前理解 | 本轮要解决的问题、目标和非目标 |
| 执行边界 | 预计触碰的目录、模块、System、配置或文档；明确不触碰的范围 |
| 行动计划 | 将采取的主要步骤，保持 3-7 条，避免展开成长期计划 |
| 遵守规范 | 本轮会遵守的关键规范 |
| 验收方式 | 计划运行的测试、编译、Demo、profile 或文档检查；如不运行需说明原因 |
| 风险 / 假设 | 当前仍未确认但不阻塞执行的假设，或可能影响结果的风险 |

### Tier 2 -- Runtime Core / Debugger / Luban / Demo 任务必填

| 字段 | 要求 |
|---|---|
| API 选型 | 列出候选 DOTS API、最终选择、拒绝理由、健康指标和重新选型触发条件；并说明 Query / Filter / Allocator / Dependency / Chunk layout / Burst calculation 是否受影响 |
| 官方文档覆盖检查 | 说明本轮涉及 `UnityDOTS官方文档参考/README.md` 的哪些主题，并落到 `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md` 的覆盖主题、`ODF-*` 规则、PackageCache 证据、采用/拒绝/暂不相关理由和反哺 owner |
| 官方版本与证据口径 | 记录 `officialPackageVersion`、`packageCachePath`、manifest/lock/PackageCache 是否存在差异；禁止引用不存在的 PackageCache `@version` 路径 |
| Burst / Player 口径 | 说明 Editor/Player、Burst AOT、OptimizeFor、Safety Checks、CPU architecture、warning policy 和 warmup 口径 |

触发条件：任务属于 T1(GAS ECS Runtime)、T2(Definition/Luban)、T4(Debugger)、T5(Burst/Generated)、T6(Demo) 主线时必填。纯文档治理(T0)和纯 Editor Authoring(T3)任务不触发 Tier 2。

### Tier 3 -- 按领域触发（不触发时省略该小节）

| 字段 | 要求 | 触发条件 |
|---|---|---|
| Authoring / Baking / Prefab 口径 | 说明 conversion/shadow/main world、Baker phase、Baking System phase、EntityPrefabReference/PrefabLoadResult/IncludePrefab 是否相关 | Luban、Demo、Presentation 任务 |
| Managed / Allocator 安全口径 | 说明 managed component clone/dispose、GC、allocator owner、alias/reinterpret/ECB allocator lifetime 是否相关 | Boundary、Presentation、Debugger、NativeContainer 任务 |
| Unity Physics 口径 | 说明 `PhysicsSystemGroup`/FixedStep、`PhysicsWorldSingleton`/`SimulationSingleton`、query broadphase、collision/trigger event 生命周期和 `ODF-15/16` | 涉及物理目标获取、命中确认、范围检测、碰撞、触发器、击退或 Physics profile 时 |
| Entities Graphics 口径 | 说明 URP/HDRP、single render world、`RenderMeshArray`/`MaterialMeshInfo`、`RenderMeshUtility.AddComponents` 边界、render evidence 和 `ODF-17/18` | 涉及真实表现、渲染资源、UI/VFX/SFX 接入、rendered profile 时 |

## 推荐模板

```text
行动报告：

当前领取任务：
- ...

所属分支：
- 一级主线：...
- 二级支线：...
- 三级任务：...

已获取上下文：
- 任务树：...
- 目标态 Spec：...
- 当前事实 / ISSUE：...
- 代码 / 验证摘要：...

当前理解：
- 目标：...
- 非目标：...

执行边界：
- 会触碰：...
- 不触碰：...

行动计划：
1. ...
2. ...
3. ...

遵守规范：
- ...

验收方式：
- ...

风险 / 假设：
- ...

--- 以下 Tier 2，仅 Runtime Core / Debugger / Luban / Demo 任务填写 ---

API 选型：
- 使用情形：...
- 候选 API：...
- 当前选择：...
- 拒绝理由：...
- Query / Filter：...
- Allocator / Dependency：...
- Chunk / Buffer：...
- Burst / Calculation：...
- 健康指标：...
- 重新选型触发：...

官方文档覆盖检查：
- 相关主题：...
- 官方证据：...
- 适用 ODF 规则：...
- 本任务取舍：采用 / 拒绝 / 暂不相关，原因...
- 影响 owner：...
- 验收指标：...
- Burst / Player 口径：...

--- 以下 Tier 3，仅对应领域触发时填写；不触发则省略该小节 ---
--- Authoring / Baking / Prefab：仅 Luban / Demo / Presentation 任务 ---
--- Managed / Allocator：仅 Boundary / Presentation / Debugger / NativeContainer 任务 ---
--- Unity Physics：仅涉及物理目标获取 / 碰撞 / 触发器 / Physics profile 时 ---
--- Entities Graphics：仅涉及真实渲染资源 / Rendered Profile 时 ---

说明：本报告仅用于暴露执行意图，不等待批准；如用户提出修正，将按最新输入调整。
```

## 与其他文档的关系

1. 行动报告是执行前的即时沟通，不是长期 owner。
2. 行动报告中暴露出的流程缺口，应在本轮 `04-当前进度状态/迭代摘要.md` 中记录，再按 `04-当前进度状态/迭代摘要反哺流程规范.md` 反哺到对应 owner。
3. 如果行动报告发现任务树上下文不足，必须更新或标记 `02-主线任务树/` 中对应节点，不能直接靠 Agent 自行补脑执行。
4. 如果行动报告发现目标态 Spec 缺失或冲突，必须先修订 `01-目标态架构共识/`，再继续拆任务或实现。
5. 行动报告不替代最终交还；最终交还仍需说明变更、验证和后续影响。
6. Runtime Core 相关行动报告必须引用 `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`，并列出 `SEL-*` 规则；如果只是文档治理任务，也要说明本轮是否改变 API 选型规范。
7. AM3 相关行动报告必须区分 Boundary request、Core frame command、parallel fan-in stream、structural mutation request；AM5 相关行动报告必须区分 OwnerLocalStore、GlobalIndexedStore、LifecycleCleanupStore、ChunkSkipIndex。
8. DOTS 相关行动报告必须先引用 `UnityDOTS官方文档参考/README.md`，再列出相关单主题文档和 `ODF-*` 规则；纯文档治理(T0)任务只需说明本轮是否改变官方文档主题入口、覆盖矩阵或反哺流程。
9. DOTS 相关行动报告必须显式覆盖 `ODF-09` 到 `ODF-18` 是否相关：PackageCache hash path、Burst AOT、managed boundary、Baking world、EntityPrefabReference、allocator aliasing、Unity Physics、Physics 配置、Entities Graphics 和 render evidence。暂不相关必须给出一句理由。纯文档治理(T0)和 Editor Authoring(T3)任务不触发本条。

## 禁止事项

1. 禁止把行动报告写成请求批准的阻塞流程。
2. 禁止在没有读取任务树 / Spec / 当前事实的情况下伪造”已获取上下文”。
3. 禁止行动报告只写”我将修改文档 / 代码”，必须说明任务归属和执行边界。
4. 禁止把行动报告作为长期事实存档；长期结论必须进入 `00/01/02/04` 对应 owner。
5. 禁止用行动报告绕过验收；行动报告只说明计划，不能替代测试和交还证据。
6. 禁止 Runtime Core 任务行动报告只写”使用 DynamicBuffer / ECB / Enableable”，必须说明为什么不使用其他候选 DOTS API。
7. 禁止 Runtime Core 任务行动报告只写”会跑性能测试”，必须说明将输出哪些 DOTS 机制指标和外部证据对照。
8. 禁止 DOTS 相关任务只引用 Unity 官方文档标题，必须说明证据如何影响当前任务取舍、owner 反哺和验收指标。
9. 禁止把 Player / AOT 性能问题、PackageCache 版本差异、managed resource 生命周期、Baking world / prefab load 和 allocator aliasing 只写成”后续注意”；相关时必须进入行动计划或验收指标。
10. 禁止纯文档治理(T0)、任务重组、命名规范类任务填写 Tier 2/Tier 3 字段；这些任务只需要 Tier 1。

## 迁移规则

1. 新增任务树节点时，`交还规则` 或 `测试链路` 中应提醒执行者在动手前输出行动报告。
2. 已存在任务节点不要求立即批量重写，但下一次维护该节点时应补充行动报告要求。
3. Goal 模式启动新一轮任务时，行动报告应成为第一条正式执行前的用户可见更新。


