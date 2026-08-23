# PRF-02: 禁止在 Hot Path 直接执行结构变化（P0 致命）

**严重度**: P0
**Primary Owner**: 结构变化-ECB
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `performance-sync-points.md`、`systems-entity-command-buffer-use.md`、`optimize-structural-changes.md`

## 规则声明

job 内不能直接通过 `EntityManager` 执行结构变化；需要从 job 产生结构命令时使用 ECB。EX-GAS Runtime Core hot path 默认把结构变化延迟到约定 playback phase。若主线程必须立即看到结果，或大量同类变更用 `EntityManager` query bulk API 更快，可在明确 phase 中例外，但必须有正确性理由与 Profiler 证据。

## 为什么

直接结构变化会先完成所需 job；连续结构变化之间若没有新调度 job，未必每次都新增实际等待。散落的“调度 job → 结构变化”边界会反复造成等待，因此应集中或批量化。

## EX-GAS 诊断

[ISSUE-004](<../../../针对2.0的ECS架构的迭代方案讨论/00-当前架构事实/ISSUE-004-结构变化边界脆弱.md>) 记录了旧 request-entity 链路触发 handle 失效的历史故障；该链路现已退场。当前验收重点是证明剩余直接 EntityManager 路径只存在于声明过的低频边界，并用 Journaling/Profiler 记录结构变化来源和实际等待；旧整帧耗时不能单独归因为结构变化成本。

## 检查方法

- Grep 搜索 `EntityManager.CreateEntity` / `DestroyEntity` / `AddComponent` / `RemoveComponent` 在 Runtime Core hot path 中的出现
- Debugger 输出 `structuralChangeCount` 并按来源 system 分类
- 出现任意 hot path 直接结构变化则标为 P0 违规
