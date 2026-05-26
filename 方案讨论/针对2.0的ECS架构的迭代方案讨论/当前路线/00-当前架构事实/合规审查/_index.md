# 合规审查

本子目录维护代码 DOTS 合规审查报告，按严重度拆分。

原报告来源：`代码DOTS合规审查报告.md`（主篇，Assets/GAS/Runtime ~8000 行）+ `代码DOTS合规审查报告-补充篇.md`（补充篇，~50 个文件 ~15000 行）。

**最近审查**: 2026-05-26 | **审查范围**: Assets/GAS/Runtime ~223 个 C# 文件 + Luban 配置链 18 个文件

## 审查看板

| 严重度 | 文件 | 缺陷数 | 缺陷编号 |
|--------|------|--------|----------|
| P0 致命 | [P0-致命缺陷](P0-致命缺陷.md) | 7 | A, B, C, D, M, N, O |
| P1 高风险 | [P1-高风险缺陷](P1-高风险缺陷.md) | 14 | E, F, G, I, P, Q, R, S, T, U, V, W, AA, AB |
| P2 改进 | [P2-改进建议](P2-改进建议.md) | 8 | K, L, X, Y, Z, AC, AD, AE |

**合计**: 29 活跃缺陷（已修复的 H/J 已清除），覆盖 Runtime Core 热路径 ~65 个文件 + Luban 配置链 18 个文件。

## 缺陷速查

| 缺陷 | 摘要 | 违反规则 |
|------|------|----------|
| A | 全系统主线 foreach + EntityManager | `PRF-05`, `PRF-02`, `JOB-01` |
| B | 热路径直接 EntityManager 结构变化 | `PRF-02`, `SC-01`, `ECB-01` |
| C | 热路径 helper 中大量临时 EntityQuery | `PRF-09`, `PRF-33` |
| D | ECB.Playback 散布在 helper 方法中 | `ECB-01`, `PRF-09` |
| M | MCCue 为 managed class IComponentData | `SYS-01`, `BUR-01` |
| N | SCueRequestBridge 零 ECB + 直接 EM | `ECB-01`, `SC-01` |
| O | EffectMagnitudeResolver ECB 碎片化 | `ECB-03` |
| E | SystemGroup 层次未迁移到目标态 | `SYS-02` |
| F | EventBus + Stream 双重事件路径 | `BUF-02`, `SEL-02` |
| G | Period/Overflow 复杂派生回退 Request Entity | `SEL-01` |
| I | EffectRuntimeUtility 承载过多职责 | `FSM-04` |
| P | 三个 ASC 管理系统零 ECB | `SC-01`, `ECB-01` |
| Q | TagHelper 四个静态托管 Dictionary | `BUR-01` |
| R | GameplayCueUnit Hybrid ECS 遗留 class | `SYS-01` |
| S | SAbilityTimelineAction 零 ECB + 直接创建 | `SC-01` |
| T | CueHelper 运行时反射创建实例 | `SYS-01` |
| U | Definition/Bake Contract 层 struct 全线托管数组 | `BAKE-01` |
| V | BlobAsset 通过静态 Dictionary 管理生命周期 | `BLOB-02` |
| W | 代码库中零 Baker<T> 实现 | `CASE-39/40/41` |
| AA | ConfigRegistryDiagnostics 全局可变静态状态 | `BUR-01` |
| AB | GASDefinitionTable O(n) 线性查询 + 非 Burst 友好 | `PRF-05` |
| K | managed 数组分配在每帧调用路径 | `NAT-05` |
| L | IComponentData 上挂 NativeArray | `PRF-34` |
| X | 5 个 System OnUpdate [BurstCompile] 被注释 | `BUR-01` |
| Y | 4 个空 struct Tag Component | `PRF-03` |
| Z | CCueOn* 六个文件 NativeArray 挂 IComponentData | `PRF-34` |
| AC | Luban 配置目录结构与 Spec 11 不一致 | — |
| AD | 生成物未按 Spec 11 拆分为独立文件 | — |
| AE | BakingSystem 使用 SystemBase 而非 ISystem | `JOB-01` |

## 维护规则

- 新发现合规缺陷按严重度追加到对应文件。
- 已修复缺陷标注状态（已修复/已验证），不从文件中删除。
- 合规审查在每次 Runtime 代码变更超过 500 行或涉及热路径时重新执行。
