# 任务树规范 — 如何合理的规划 Task

本文件定义**规划 Agent** 编写叶子任务文档的完整方法论。规划 Agent 的唯一交付物是一个 `.md` 叶子文件——该文件必须是执行 Agent 的完整上下文，不依赖任何跨文件查找。

## 核心原则

**规划 Agent 承担上下文整合成本，执行 Agent 只读一个 .md 文件。**

```
规划Agent                              执行Agent
  读 ISSUE →                             读叶子文件.md →
  读 Spec →     整合写入     →            执行任务 →
  读 DOTS规则 →  叶子文件                 交还报告
  读代码事实 →
```

规划 Agent 的工作是：从 00（当前事实）、01（目标态）、UnityDOTS 三个知识库中提取与本任务相关的约束，解码为执行 Agent 能直接理解的工程语言，按 Why→What→How→Done 梯度写入一个文件。

## Part A: 五阶段规划流程

### Phase 1 — 定位：确定要读什么

规划 Agent 不应通读全部三个知识库。三个知识库各自有 README 索引文件，规划 Agent 通过索引定位当前版本的实际文件。

**定位方法：先确定任务的关注领域，再通过各知识库的索引找到当前文件。**

```
步骤：
1. 确定任务的关注领域（例如: "Runtime Core 即时 GE 评估"）
2. 查下面的领域→文档类型映射表，确定应该在每个知识库中找什么类型的文档
3. 通过各知识库的 README 索引找到当前版本的具体文件
   - 00: 查看 核心问题诊断.md 的问题看板
   - 01: 查看 01-目标态架构共识/README.md 的 Spec 索引
   - DOTS: 查看 UnityDOTS官方文档参考/README.md 的"按技术领域"和"按任务类型"表
4. 打开定位到的文件，执行 Phase 2 提取
```

#### 领域→文档类型映射

本表描述每种任务类型应该在三个知识库中寻找**什么领域/主题的文档**。具体文件名通过各知识库的 README 索引查找，不在此硬编码。

| 任务领域 | 在 00 中寻找... | 在 01 中寻找... | 在 UnityDOTS 中寻找... |
|---|---|---|---|
| Runtime Core — 管线/契约/评估 | GE 生命周期、结构变化边界、EventBus、Frame Backbone 相关的问题诊断 | Runtime Core 管线 Spec、EffectCommand/SpecStream Spec、命名规范、物理布局规范、全局不变量 | System/World/SystemGroup 组织、Query/Job 遍历模式、结构变化/ECB/Enableable 规则、Buffer/Chunk/Store 承载策略、Collections/Allocator 选型、编写规范与性能陷阱、API 选型基线、规则编号索引 |
| Runtime Core — Store/状态 | GE 生命周期、结构变化边界、Frame Backbone 相关的问题诊断 | Runtime Core 管线 Spec、EffectCommand Spec、ActiveEffectStore Spec、命名规范、物理布局规范、全局不变量 | 同上 + 状态机与数据分支策略、数据流与系统生命周期规范 |
| Definition / 配置生成 | 配置生成链路、Definition 配置事实相关的问题诊断 | 四层架构 Spec、Luban 配置链路 Spec、AutoChessDemo 配置方案 Spec、命名规范、物理布局规范 | Baking/Blob/Prefab 机制、Burst 编译条件、编写规范与性能陷阱、官方案例（Baker/Baking System 模式）、API 选型基线、版本与 PackageCache 证据 |
| Debugger / Diagnostics | Observation 耦合、Debugger 证据不足相关的问题诊断 | Runtime Core 管线 Spec、Observation/Presentation/Replay Spec、Debugger Spec、命名规范 | System/World、Query/Job、ECB/Enableable、Buffer/Chunk/Store、Diagnostics/Profiler/Journaling、Collections/Allocator、编写规范与性能陷阱、数据流/生命周期、API 选型基线 |
| Demo / AutoChess 验收 | AutoChess 边界混入 Runtime Core 相关的问题诊断 | Runtime Core 管线 Spec、Observation Spec、Debugger Spec、AutoChess 无头验收 Spec、完整业务案例设计 Spec、配置方案 Spec、命名规范、物理布局规范 | System/World、Query/Job、ECB/Enableable、Buffer/Chunk/Store、Unity Physics、Entities Graphics、Collections/Allocator、Mathematics 确定性计算、编写规范与性能陷阱、状态机策略、数据流/生命周期、官方案例高级、API 选型基线 |
| Editor / Authoring | —（Editor 类任务通常无直接 ISSUE） | 四层架构 Spec、Luban 配置链路 Spec、Authoring/Editor Spec、命名规范 | Baking/Blob/Prefab 机制、API 选型基线 |
| Burst / Generated 后置 | 配置生成链路相关的问题诊断 | Runtime Core 管线 Spec、EffectCommand Spec、Luban 配置链路 Spec、命名规范、物理布局规范 | Burst 编译/向量化/AOT、Collections/Allocator、编写规范与性能陷阱、数据流/生命周期、官方案例高级、API 选型基线 |

#### 补充阅读

上述映射覆盖的是**任务领域的基础必读**。如果任务的 `执行范围` 中包含了映射未覆盖的 API 或模块（如 Physics、Entities Graphics），规划 Agent 必须额外在 UnityDOTS 知识库中查找对应技术领域的主题文档。

### Phase 2 — 提取：从三个来源提取约束

#### 从 00-当前架构事实 提取 → 目标输出：`当前事实约束` 节

提取规则：只提取**本任务执行范围内的代码/模块**直接关联的 ISSUE 事实。

```
步骤：
1. 打开必读 ISSUE 文件
2. 找到"当前事实"或"症状"章节
3. 筛选：这条事实涉及的代码文件是否在本任务的执行范围内？
   - 是 → 提取
   - 否 → 跳过
4. 将提取的事实改写为对本任务的具体约束
   改写前: "旧 GE lifecycle 的 per-hit entity create/destroy 是核心热点"
   改写后: "本任务的 direct command 主链不得做 per-hit entity create/destroy"
5. 附来源标注: "> 来源：ISSUE-001、ISSUE-004"
```

输出数量上限：≤3 条。

#### 从 01-目标态架构共识 提取 → 目标输出：`目标态约束摘要` 节

提取规则：从 Spec 中提取**会被本任务的代码决策直接引用的**硬约束。不要提取"整个 GAS 架构都适用"的通用原则（通用原则应由规划 Agent 在理解后融入执行细则，不需要作为约束条目列出）。

```
步骤：
1. 打开必读 Spec 文件
2. 扫描关键章节（通常是"目标态"、"约束"、"契约"、"验收"）
3. 筛选：这条约束是否直接影响本任务执行范围内的代码编写决策？
   - 是（例如"四阶段主链不创建 runtime GE entity"直接决定 AM3 的代码怎么写）→ 提取
   - 否（例如"Observation 分层消费 facts"对 AM3 的 instant command 写入路径无直接影响）→ 跳过
4. 用工程语言改写
   改写前: "simple instant GE evaluation SHALL NOT create CApplyGameplayEffectRequest"
   改写后: "simple instant GE 走四阶段主链，不创建 CApplyGameplayEffectRequest 或 runtime GE entity"
5. 附来源标注: "> 来源：04-EffectCommand-SpecStream-AttributeDeltaSpec.md"
```

输出数量上限：≤5 条。超过说明任务执行范围过大，应触发 S3（多关注点）拆分。

#### 从 UnityDOTS官方文档参考 提取 → 目标输出：`适用规则摘要` 节

提取规则：根据本任务**涉及的具体 API 和操作**来筛选适用规则。

```
步骤：
1. 列出本任务执行范围内涉及的所有 DOTS API 操作
   示例: DynamicBuffer 写入、ISystem 主线程、定义数据只读、Query 组件访问
2. 查 90-规则编号索引.md，找到每类操作对应的规则族
   示例: DynamicBuffer → BUF-*, ISystem → SYS-*, 定义只读 → DEF-*, Query → QRY-*
3. 从对应主题文档中读取规则的具体约束内容
4. 筛选：这条规则是否在本任务的执行场景下会被触发？
   - 是 → 收录，用一行中文写出约束
   - 否 → 跳过（例如 BUF-04 关于跨 job buffer 共享，本任务用主线程 ISystem，不适用）
5. 用表格式输出: | 规则编号 | 本任务约束（一行中文） |
```

不需要覆盖所有规则编号。只选本任务确实会触发的。一条规则一行，不做长段落解释。

### Phase 3 — 合成：去重、检测冲突、控量

三个来源提取完毕后，做最终合成：

1. **去重**：同一条约束可能同时出现在 Spec 和 DOTS 规则中（例如"不在 hot path 做结构变化"同时出现在 `04-EffectCommand` 和 `SC-01`）。合并为一条，来源标注两者。
2. **检测冲突**：Spec 要求的目标态和 ISSUE 报告的当前状态可能矛盾。如果冲突直接阻碍本任务执行，标记为"本轮需解决"并写入 `当前问题`。
3. **控量**：约束摘要总计（目标态 + 当前事实 + 适用规则）不超过 20 条。超过说明任务范围过大，应触发拆分。

### Phase 4 — 写入：按上下文梯度组织

叶子文件的节按 Why→What→How→Done 梯度组织。每个节的写入来源和规则如下：

| 梯度 | 节 | 信息来源 | 写入规则 |
|---|---|---|---|
| Why | `节点定位` | 规划Agent综合判断 | 写清承接谁、输出给谁，2-3行 |
| Why | `兄弟关系` | 父节点看板 + 任务树结构 | sequential/parallel + 前置是谁 + 后继是谁 |
| Why | `当前问题` | 00-ISSUE + 代码扫描 | 具体到代码文件，不以"完善/优化"开头 |
| Why | `本轮目标` | 状态 + 当前进展 + 未闭合点 | 按状态区分写法（见下方） |
| What | `目标态约束摘要` | Phase 2 提取自 01-Spec | ≤5条，每条 1-2行 + 来源标注 |
| What | `当前事实约束` | Phase 2 提取自 00-ISSUE | ≤3条，每条 1-2行 + 来源标注 |
| What | `适用规则摘要` | Phase 2 提取自 DOTS规则 | 表格式，编号 + 一行中文约束 |
| What | `非目标` | 边界判断 | 具体到"不做什么模块/不改什么文件" |
| How | `执行范围` | 代码目录扫描 | 逐文件列出，附简短职责说明 |
| How | `执行细则` | DOTS规则 + Spec约束 + 代码现实 | 每条 DO/DON'T，粗体关键词开头 |
| Done | `验收标准` | Spec验收门槛 + DOTS规则指标 | 可客观判断，可自动/半自动验证 |
| Done | `测试链路` | Spec测试链路 + 代码目录 | 具体命令（git/dotnet/rg），不写"运行相关测试" |
| Done | `交还内容` | 交还规则 | 逐文件列出需更新的文档 |

### Phase 5 — 验证：模拟执行 Agent 自检

规划 Agent 写完叶子文件后，必须执行自检——模拟一个执行 Agent 只读这个文件，判断是否可以开始工作。

#### 自检流程

```
步骤 1: 自上而下通读文件
  → 检查: Why→What→How→Done 梯度是否完整？跳着读是否还能理解？

步骤 2: 检查每个节的可行动性
  → 执行细则每一条是否都是具体的 DO/DON'T？
  → 验收标准每一条是否可客观判断？
  → 测试链路每一条是否可直接复制执行？

步骤 3: 检查约束是否被解码
  → 是否存在裸文件路径（"详见 xx.md"）作为约束的唯一定义？
  → 是否存在裸规则编号（"SYS-01"）而未解释含义？
  → 如果存在 → 该约束未内联，需补充

步骤 4: 检查无用信息
  → 是否存在本任务执行范围内不会触发的 DOTS 规则？
  → 是否存在本任务不涉及的 Spec 约束？
  → 如果存在 → 删除，减少噪音

步骤 5: 检查当前进展和本轮目标的一致性
  → 当前进展是否 ≤10 行？
  → 本轮目标是否匹配任务状态？
  → 本轮目标是否具体到"本轮做到什么程度"？
```

#### 自检清单（逐项打勾）

| # | 检查项 | 不合格时 |
|---|---|---|
| 1 | 执行 Agent 不打开其他文件就能理解要做什么 | 补充约束内联 |
| 2 | 执行 Agent 不打开其他文件就能理解受什么 DOTS 规则约束 | 解码规则编号 |
| 3 | 执行 Agent 不打开其他文件就能理解当前做到哪了 | 重写当前进展 |
| 4 | 执行细则每一条都是可行动的 DO/DON'T | 去掉模糊表述 |
| 5 | 验收标准每一条都可客观判断 | 加上具体指标 |
| 6 | 测试链路每一条都是可直接执行的命令 | 替换"运行相关测试" |
| 7 | 约束摘要没有裸文件路径作为约束的唯一定义 | 内联约束内容 |
| 8 | 适用规则表没有裸编号（每行都有中文约束） | 补充中文约束 |
| 9 | 没有本任务不会触发的多余规则 | 删除冗余 |
| 10 | 当前进展 ≤10 行 | 迁移到迭代记录 |

## Part B: 叶子文件字段规范

### 所有节点必填字段

| 字段 | 要求 | 规划Agent写入来源 |
|---|---|---|
| 节点名 | `父节点路径 - 本节点职责名` | 命名规则 |
| 节点定位 | 本节点在树中的角色：为什么存在、承接什么上游、输出给什么下游 | 规划Agent综合判断 |
| 当前问题 | 本节点直接解决的架构/实现问题（具体到文件/模块） | Phase 2 从 ISSUE 提取 |
| 目标态参考 | 关联的 Spec 文件列表（溯源用指针） | Phase 1 阅读矩阵 |
| 目标 | 本轮要改变的架构事实（完成后的状态描述） | Phase 2 从 Spec 提取 |
| 非目标 | 明确不做什么（具体到文件/模块） | 边界判断 |
| 执行范围 | 涉及目录、System、模块的文件路径列表，每项附简短职责说明 | 代码目录扫描 |
| 执行细则 | 具体的 DO/DON'T 约束，每条以粗体关键词开头 | Phase 2 从 DOTS+Spec 提取 |
| 验收标准 | 可客观判断"完成"的结果列表 | Phase 2 从 Spec 验收门槛提取 |
| 测试链路 | 可直接复制执行的命令（git/dotnet/rg/profile） | Spec测试链路 + 代码目录 |
| 交还内容 | 需回写的文档和证据列表（逐文件列出） | 交还规则 |
| 拆分历史 | 本节点何时从哪个父节点拆分、触发条件 | 任务树生长记录 |

### 叶子节点额外必填字段

| 字段 | 要求 | 规划Agent写入来源 |
|---|---|---|
| 任务ID | 唯一短标识 | 命名规则 |
| 状态 | 候选/就绪/进行中/待验证/契约已确立/已完成/阻塞/已归档 | 当前实际状态 |
| 兄弟关系 | `sequential` 或 `parallel`，说明前置和后继节点的具体任务ID | 父节点看板 |
| 领取轮次 | 本轮是第几次领取、累计已执行几轮 | 历史记录 |
| 当前进展 | 最近一轮的执行结果摘要，**≤10行** | 上一轮交还报告（摘要化） |
| 本轮目标 | 本轮要推进到的具体状态，按状态区分写法 | 当前进展 + 未闭合点 |

### 约束内联字段（规划Agent核心交付物）

| 字段 | 要求 | 规划Agent提取来源 |
|---|---|---|
| 目标态约束摘要 | ≤5条，每条1-2行+来源标注 | Phase 2: 从 Spec 提取 |
| 当前事实约束 | ≤3条，每条1-2行+来源标注 | Phase 2: 从 ISSUE 提取 |
| 适用规则摘要 | 表格式，编号+一行中文约束 | Phase 2: 从 DOTS规则解码 |

### 按状态区分"本轮目标"写法

| 状态 | 本轮目标写法 | 示例 |
|---|---|---|
| 就绪（首次领取） | 完整目标 + 完成后的状态 | "完成 simple instant GE activation 的 ECS command 写入路径，使 SAbilityCommit 不创建 CApplyGameplayEffectRequest。完成后激活/cost/Timeline 三类 producer 全部走 ECS 主链" |
| 进行中 | 本轮增量 + 当前卡点 | "基于上一轮已完成的 activation command 写入，本轮推进 cost GE 的 command 路径。当前卡点是 SetByCaller range 跨 command 传递" |
| 契约已确立 | 明确本轮是补验证还是只读 | "本轮只补 `dotnet build` 验证和 runtime test 通过证据，不新增功能代码。如 LicensingClient 阻塞则记录环境阻塞" |
| 阻塞 | 阻塞原因 + 等待什么 | "等待 AM2B-D structural playback gate 完成。阻塞项是 cleanup 路径的结构变化需收口到 playback gate" |
| 推荐优先 | 为什么优先 + 卡住什么下游 | "优先因为 AM3 闭合后 AM5 的 store-driven lifecycle 才有稳定上游。当前卡住 parallel fan-in contract test 缺失和 EventBus callsite 残留" |

## Part C: 拆分与合并（树自动生长）

### 拆分触发

一个叶子节点满足**任一**条件时，规划 Agent 必须将其拆分为分支节点：

| # | 条件 | 阈值 | 拆分动作 |
|---|---|---|---|
| S1 | 轮次累计 | 累计 ≥5 轮未闭合 | 进行中的关注点独立为一个子节点，已完成的各归档为一个子节点 |
| S2 | 行数膨胀 | 正文 >200 行 | 按关注点边界拆分子节点 |
| S3 | 多关注点 | 同时触及 ≥3 个独立模块/SystemGroup/数据契约 | 每个关注点独立为一个子节点 |
| S4 | 依赖链 | 步骤 B 依赖 A 的输出、C 依赖 B | 每个步骤独立为一个子节点，标记 sequential |

触发后原节点自动变为分支节点（不可直接领取），原内容中的共享上下文保留在分支 README 中作为规划 Agent 编写子节点时的素材。

### 合并触发

| # | 条件 | 合并动作 |
|---|---|---|
| M1 | 所有子节点已完成或已归档 | 分支节点自动已完成 |
| M2 | 只剩 1 个非归档子节点 | 子节点合并回父节点，父节点恢复为叶子 |
| M3 | 子节点 >4 轮无领取且无进行中 | 归档或合并回父节点 |

## Part D: 持续校准

本规范描述的是**规划方法**（如何定位、如何提取、如何验证），不依赖具体文件名。需要校准的是以下两项：

### 领域→文档类型映射校准

映射表中的行描述的是"某类任务应该关注哪些知识领域"。以下情况触发映射更新：

1. 新增任务领域（如新增一条主线或支线方向）→ 评估是否需要新增行
2. 某知识库新增了文档类型/主题 → 补充到受影响的任务领域行
3. 任务执行中发现"某类知识对该领域实际上没有参考价值" → 从对应行中移除

每次更新在映射表下方记录一行变更日志（日期 + 变更内容）。

### 知识库索引校准

各知识库的 README 索引（`核心问题诊断.md`、`01-目标态架构共识/README.md`、`UnityDOTS官方文档参考/README.md`）是规划 Agent 定位当前文件的入口。这些索引的准确性由各知识库的维护规范保证，不在本文件中重复定义。
