# Unity DOTS 官方文档参考

## 定位

本目录是 EX-GAS 2.0 架构路线的 **Unity DOTS 版本化依据库与项目规则库**。它不属于目标态 Spec，也不属于任务树。

本目录严格区分三类内容：

1. **官方事实/官方建议**：来自当前项目精确 PackageCache 的 `Documentation~` 或包源码。
2. **EX-GAS 项目规则**：基于官方机制形成的项目选型，不代表 Unity 官方强制要求。
3. **项目基准阈值**：必须附目标平台、场景、测量方法和日期；没有基准证据时只能作为待验证假设。

为避免在同一文件混合规范层级，当前原子文件采用固定职责：非 `CASE-*` 文件属于“EX-GAS 项目规则/项目推导”，`CASE-*` 文件属于“模式/案例”。Unity 官方事实与建议由各主题的 `API与EX-GAS解读.md` 承载；原子文件中的“来源”只说明项目规则所依据的官方机制，不能把该规则整体引用成 Unity 强制要求。

### 这个目录解决什么问题

当你在 EX-GAS 开发中遇到以下问题时，本目录提供有据可查的答案：

- "这个 DOTS API 到底应该怎么用？选 IJobEntity 还是 IJobChunk？"
- "为什么不能在 hot path 直接做结构变化？官方是怎么说的？"
- "ECB 和 Enableable 各适合什么场景？EX-GAS 项目里应该用哪个？"
- "官方文档说 DynamicBuffer 默认 128 字节容量，溢出了会怎样？"
- "NativeStream 做并行 fan-in 时，怎么保证输出是确定性的？"
- "Entity Graphics 的无头 Demo 等价性怎么保证？"

### 使用方式

1. **先读本 README**，定位你关心的问题属于哪个技术领域
2. **打开对应的技术领域文档**，读"核心概念"章节获取 API 机制
3. **读"EX-GAS 项目解读"章节**，获取该机制在本项目中的具体应用约束
4. **行动报告中引用具体规则编号**，如 `SC-01`、`BUF-02`、`CASE-03`

### 与本项目其他目录的关系

```mermaid
flowchart LR
    Official["Unity DOTS 官方文档参考"] --> Target["01 目标态架构共识"]
    Official --> Task["02 主线任务树"]
    Target --> Task
    Task --> Agent["Agent 行动报告"]
    Agent --> Code["代码 / Demo / 验证"]
    Code --> Facts["00 当前架构事实"]
    Facts --> Target
```

目标态架构共识回答“EX-GAS 应该怎么设计”；本目录同时回答“当前精确版本的 Unity DOTS/ECS 机制是什么”和“EX-GAS 选择怎样约束自己”。Runtime Core、Debugger、Luban、AutoChessDemo 相关任务在执行前必须先从本目录读取相关技术领域文档，并区分官方依据与项目策略。

## 目录结构

```
UnityDOTS官方文档参考/
├── README.md                 ← 本文件（速查入口）
├── 维护规范.md                ← 提取/维护/模板/自检规范
├── 主题/                      ← 技术领域主题与当前权威流程
│   ├── System-World-SystemGroup/
│   │   ├── _index.md          ← 主题门户（核心概念 + 跨主题链接）
│   │   ├── API与EX-GAS解读.md  ← API 机制详解 + EX-GAS 项目解读
│   │   ├── SYS-01.md          ← EX-GAS 规范规则文件（编号是唯一标识）
│   │   └── CASE-17.md         ← 模式/反模式案例
│   ├── Query-Job-遍历/
│   ├── 结构变化-ECB/
│   ├── Enableable-Component选型/
│   ├── DynamicBuffer-Chunk-Archetype/
│   ├── Store选型-数据承载策略/
│   ├── Baking-BlobAsset/
│   ├── Prefab-Content管理/
│   ├── Diagnostics-Debugger/
│   ├── UnityPhysics/
│   ├── EntitiesGraphics/
│   ├── Burst-AOT/
│   ├── NativeContainer-Allocator/
│   ├── Mathematics-确定性/
│   ├── Transform-层级/
│   ├── 状态机策略/
│   ├── 数据流-系统生命周期/
│   ├── 20-GASRuntimeCore-API选型基线.md      ← API selection checkpoint 唯一正文
│   ├── 21-官方文档覆盖与流程闭环.md          ← 官方依据生成-消费-归档唯一正文
│   ├── 00-*.md ... 16-*.md    ← 旧路径兼容跳转，不维护正文
│   ├── 90-规则编号索引.md      ← 旧路径兼容跳转
│   └── 91-Agent行动报告模板.md ← 旧路径兼容跳转
└── 元信息/                    ← 版本、模板和旧路径兼容入口
    ├── 版本与PackageCache.md
    ├── 行动报告模板.md
    ├── 90-规则编号索引.md       ← 全局编号与 Primary Owner 唯一索引
    ├── API选型基线.md          ← 兼容跳转，不维护正文
    └── 文档覆盖流程.md          ← 兼容跳转，不维护正文
```

## 第一性依据

当前项目使用 Unity `6000.3.14f1`，并以**本地 PackageCache 中实际安装包的 `Documentation~` 为第一性技术依据**。在线 `@major.minor` 地址会滚动到同一 minor 的较新 patch，只能作为升级风险、版本差异或补充阅读入口。

| 包 | 实际版本 | 本地路径（包含 hash） | 官方在线入口 |
|---|---:|---|---|
| Entities | `1.4.6` | `Library/PackageCache/com.unity.entities@e90944159b94/Documentation~` | https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/ |
| Unity Physics | `1.4.6` | `Library/PackageCache/com.unity.physics@22d10f355559/Documentation~` | https://docs.unity3d.com/Packages/com.unity.physics@1.4/manual/ |
| Entities Graphics | `1.4.19` | `Library/PackageCache/com.unity.entities.graphics@1e91bb5cdef3/Documentation~` | https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.4/manual/ |
| Burst | `1.8.29` | `Library/PackageCache/com.unity.burst@6bb9aca3ef38/Documentation~` | https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/ |
| Collections | `2.6.6` | `Library/PackageCache/com.unity.collections@9796e5ee0d9e/Documentation~` | https://docs.unity3d.com/Packages/com.unity.collections@2.6/manual/ |
| Mathematics | `1.3.3` | `Library/PackageCache/com.unity.mathematics@19a9377c4ffa/Documentation~` | https://docs.unity3d.com/Packages/com.unity.mathematics@1.3/manual/ |

**禁止修改 `Library/PackageCache` 中的任何文件。** 该目录变更会被 Unity 自动还原。

`官方文档原件/` 保存审计快照；它必须与上表精确 PackageCache 逐文件一致。快照缺失或 hash 不一致时，不得宣称已完成对应包的离线官方依据覆盖。

## 速查

### 按技术领域

| 你想知道... | 读这个文档 |
|---|---|
| System/World/SystemGroup 怎么组织，phase 怎么设计 | [System-World-SystemGroup](主题/System-World-SystemGroup/_index.md) |
| IJobEntity vs IJobChunk vs SystemAPI.Query 该用哪个 | [Query-Job-遍历](主题/Query-Job-遍历/_index.md) |
| ECB 怎么用，结构变化怎么避免 | [结构变化-ECB](主题/结构变化-ECB/_index.md) |
| Enableable 怎么替代 Tag Component | [Enableable-Component选型](主题/Enableable-Component选型/_index.md) |
| DynamicBuffer 容量策略，Archetype 布局 | [DynamicBuffer-Chunk-Archetype](主题/DynamicBuffer-Chunk-Archetype/_index.md) |
| Store 怎么选型，数据性质分类 | [Store选型-数据承载策略](主题/Store选型-数据承载策略/_index.md) |
| Baking/Blob 机制，config 怎么 immutable 化 | [Baking-BlobAsset](主题/Baking-BlobAsset/_index.md) |
| Prefab 怎么管理，资源引用怎么处理 | [Prefab-Content管理](主题/Prefab-Content管理/_index.md) |
| Profiler/Journaling/Debugger 诊断体系怎么搭 | [Diagnostics-Debugger](主题/Diagnostics-Debugger/_index.md) |
| Physics 范围检测、碰撞事件怎么接入 GAS | [UnityPhysics](主题/UnityPhysics/_index.md) |
| Entities Graphics 渲染桥接、无头/有头等价性 | [EntitiesGraphics](主题/EntitiesGraphics/_index.md) |
| Burst 编译条件、FunctionPointer、AOT 注意事项 | [Burst-AOT](主题/Burst-AOT/_index.md) |
| NativeStream/Allocator/ParallelWriter 怎么选 | [NativeContainer-Allocator](主题/NativeContainer-Allocator/_index.md) |
| Mathematics 确定性随机、battle hash | [Mathematics-确定性](主题/Mathematics-确定性/_index.md) |
| Transform 层级、LocalToWorld 正确用法 | [Transform-层级](主题/Transform-层级/_index.md) |
| ECS 状态机三种策略、GAS 选型映射 | [状态机策略](主题/状态机策略/_index.md) |
| 数据流确定性、Allocator 归属、System 生命周期 | [数据流-系统生命周期](主题/数据流-系统生命周期/_index.md) |
| 行动报告里引用哪个规则编号 | [90-规则编号索引](元信息/90-规则编号索引.md) |
| 执行任务前 API 选型 checkpoint | [20-GASRuntimeCore-API选型基线](主题/20-GASRuntimeCore-API选型基线.md) |
| 官方文档发现怎么反哺到项目文档 | [21-官方文档覆盖与流程闭环](主题/21-官方文档覆盖与流程闭环.md) |
| 当前 PackageCache 版本和 hash 证据 | [版本与PackageCache](元信息/版本与PackageCache.md) |
| Agent 执行 DOTS 任务的报告模板 | [行动报告模板](元信息/行动报告模板.md) |

### 按任务类型

| 任务类型 | 必读技术领域文档 | 最小行动报告内容 |
|---|---|---|
| **Runtime Core** | System-World-SystemGroup、Query-Job-遍历、结构变化-ECB、DynamicBuffer-Chunk-Archetype、Burst-AOT、NativeContainer-Allocator、状态机策略、数据流-系统生命周期、API选型基线 | API 选型表、query/buffer/job/structural metrics、状态机选型、规范检查清单 |
| **Debugger** | Query-Job-遍历、结构变化-ECB、DynamicBuffer-Chunk-Archetype、Diagnostics-Debugger、Burst-AOT、NativeContainer-Allocator、数据流-系统生命周期 | 分组归因、Profiler/Journaling 对照 |
| **Luban/SourceGenerator** | 版本与PackageCache、Baking-BlobAsset、Burst-AOT、Prefab-Content管理、API选型基线 | generated lookup、managed boundary 证据 |
| **AutoChessDemo** | Query-Job-遍历、结构变化-ECB、DynamicBuffer-Chunk-Archetype、UnityPhysics、EntitiesGraphics、NativeContainer-Allocator、Mathematics-确定性、状态机策略、数据流-系统生命周期、Transform-层级、API选型基线 | battle hash、core/physics/render profile、状态机选型 |
| **Presentation/Cue** | Baking-BlobAsset、EntitiesGraphics、NativeContainer-Allocator、数据流-系统生命周期、API选型基线 | outbox、resource binding 等价性 |
| **Physics 目标获取** | UnityPhysics、NativeContainer-Allocator、Mathematics-确定性、数据流-系统生命周期、API选型基线 | broadphase policy、query count |

## 渐进式阅读规则

1. **不全文重读**。先读本 README 定位任务相关技术领域，再只读对应 `_index.md`。
2. **先读 `API与EX-GAS解读.md` 的官方机制**，再读原子规则中的项目约束。
3. 引用时必须写明“官方事实/官方建议/EX-GAS 项目规则/项目基准阈值”，不能只写“官方规定”。
4. 行动报告中必须引用具体规则编号（如 `SC-01`、`BUF-02`）及采用/拒绝理由。
5. 涉及未覆盖 API、安全性、AOT、序列化或版本差异时，必须回查精确 PackageCache 原文或源码，不能把本目录当作替代官方原文的永久副本。

## 维护规则

1. 新增官方依据先进入对应技术领域文档，不直接塞进目标态 Spec。
2. 一个技术领域文档维护一个 DOTS 机制；如果开始同时承担两类职责，必须拆分（参见 [维护规范](维护规范.md) Part B）。
3. 每个技术领域文档包含：**核心概念**（最关键的章节）、编写规范、模式与反模式、EX-GAS 项目解读、常见陷阱、官方证据、验收指标。
4. PackageCache hash 变化时先更新 [版本与PackageCache](元信息/版本与PackageCache.md)，再更新受影响技术领域文档。
5. **不修改** `Library/PackageCache` 中的任何文件。
6. 官方依据改变目标设计 → 反哺 `01-目标态架构共识`。
7. 官方依据改变任务拆分或验收 → 反哺 `02-主线任务树`。
8. 官方依据暴露当前实现问题 → 反哺 `00-当前架构事实`。
9. 主题根部旧数字聚合文档只保留兼容跳转；正文只维护在主题目录和 `元信息/` 中。
10. 固定数量、倍率、毫秒预算或百分比必须提供项目基准证据；否则改写为定性选型条件。

## 验收标准

1. README 中每个主题入口可达，规则编号唯一，内部相对链接可解析。
2. 官方事实能追溯到精确包版本、PackageCache hash 和真实文件名。
3. EX-GAS 项目规则不会伪装成 Unity 官方硬性规范；项目阈值具有基准证据。
4. 行动报告中的 API 选型和规则引用都定位到具体主题与规则。
5. PackageCache 版本更新时，版本表与官方原件快照先更新，再复核关联主题文档。
