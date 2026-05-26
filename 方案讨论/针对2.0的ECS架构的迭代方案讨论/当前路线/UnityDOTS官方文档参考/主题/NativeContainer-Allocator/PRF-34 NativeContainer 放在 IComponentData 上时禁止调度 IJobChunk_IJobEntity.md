# PRF-34: NativeContainer 放在 IComponentData 上时禁止调度 IJobChunk/IJobEntity

**严重度**: P1
**Primary Owner**: NativeContainer-Allocator
**来源**: `13-DOTS编写规范与性能陷阱.md` — `components-nativecontainers.md`

## 规则声明
禁止将 `NativeContainer`（如 `NativeList<T>`、`NativeArray<T>`）直接作为 `IComponentData` 字段并对该 component 调度 `IJobChunk` 或 `IJobEntity`。

## 为什么
ECS 安全系统会对 NativeContainer 字段自动建立 safety handle。当对具有 NativeContainer 字段的 component 调度并行 job 时，safety 系统无法正确处理 job 间的读写依赖，导致未定义行为。正确的做法是在主线程提取 NativeContainer 内容后单独调度 job。

## EX-GAS 诊断
如果 EffectStore、ActiveEffectSlot 等 store 使用 NativeContainer 作为 IComponentData 的字段，必须先在主线程提取数据再做 job 调度。

## 检查方法
Grep 搜索 IComponentData struct 定义中的 NativeContainer 类型字段（`NativeList`、`NativeArray`、`NativeHashMap` 等）；对每个匹配项检查是否存在对该 component 的 IJobChunk/IJobEntity 调度。
