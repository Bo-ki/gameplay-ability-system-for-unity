# PRF-21: ExclusiveEntityTransaction 仅限 Secondary/Streaming World

**严重度**: P1
**Primary Owner**: NativeContainer-Allocator
**来源**: `90-规则编号索引.md` — PRF-21

## 规则声明
Runtime Core job 内结构变化默认使用 ECB 录制，并在明确 playback phase 合并执行。只有当目标是 secondary / streaming World，且确实需要让单个 worker 线程执行大批量结构变化时，才评估 `ExclusiveEntityTransaction`。它不是通用 worker-thread `EntityManager` 替代。

## 为什么
`concepts-safety.md` 明确说明 `ExclusiveEntityTransaction` 的主要动机是让 secondary / streaming World 安全修改实体并执行结构变化，同时不阻塞 default World 主线程；官方也明确它不是完整通用的 `EntityManager` worker-thread 接口，只支持 `ExclusiveEntityTransaction` 直接暴露的 API 子集。`systems-entity-command-buffer-use.md` 中，job 内结构变化的常规路线仍是 ECB。

## EX-GAS 诊断
AutoChess battle init、Scene loading 中的批量 entity 创建若发生在 secondary / streaming World，可考虑 `ExclusiveEntityTransaction`。GAS Runtime Core 每帧的 ability grant/revoke、request entity 清理、effect cleanup 不使用它，统一进入 `GASStructuralCommitSystemGroup` 的 ECB / bulk query 路径。

## 检查方法
搜索 `ExclusiveEntityTransaction`：若出现在 Runtime Core hot path，要求移除或给出 secondary/streaming World 证明。搜索批量 entity 创建场景时，先判断是否能用预建 archetype + bulk `EntityManager` / ECB 解决；只有独立 streaming World 才进入 EET 评估。
