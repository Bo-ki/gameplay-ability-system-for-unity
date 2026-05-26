# STORE-01: Store 命名表达 owner、生命周期、索引方式

**严重度**: P1
**Primary Owner**: Store选型-数据承载策略
**来源**: EX-GAS 项目内部约定（由 ActiveEffectStore 物理设计演化而来）

## 规则声明
所有 Store 类型名称必须明确表达三个维度：owner（谁持有）、生命周期（帧内/跨帧/持久）、索引方式（直接/哈希查找/顺序）。

## 为什么
Store 是 ECS 中跨多 entity 和多 system 的数据集合。名称含糊导致新加入者不知道数据在哪、谁可以写、生命周期多长。明确命名降低认知负担和误写风险。

## EX-GAS 诊断
- `OwnerLocalStore`：per-ASC，同 chunk 内访问
- `GlobalIndexedStore`：跨 entity 查询，需要 lookup 或 secondary index
- `LifecycleCleanupStore`：用于追踪待清理的 granted tag/ability
- 命名模式：`{生命周期前缀}{数据描述}Store`（如 `FrameCommandStore`、`PersistEffectStore`）

## 检查方法
Code review 检查新 Store 类型是否包含 owner/lifetime/index 维度信息。
