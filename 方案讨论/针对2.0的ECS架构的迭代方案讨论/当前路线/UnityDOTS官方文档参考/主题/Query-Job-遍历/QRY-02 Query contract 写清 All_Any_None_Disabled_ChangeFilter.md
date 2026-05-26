# QRY-02: Query contract 写清 All/Any/None/Disabled/ChangeFilter

**严重度**: P0
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — QRY-01 节 + EntityQuery 与 Query Filter 节

## 规则声明
使用 `EntityQueryBuilder` 显式构造 EntityQuery 时（包括 IJobEntity 的 `[WithAll]`/`[WithNone]`/`[WithAny]`/`[WithChangeFilter]`/`[WithOptions]` 属性），必须完整声明 All、Any、None、Options 及 ChangeFilter。禁止依赖隐式 query 条件或不完整声明。

## 为什么
隐式 query 条件意味着后续维护者无法仅从代码看出该 system 处理哪些 entity。缺少 `WithNone` 可能导致已销毁或不应处理的 entity 进入 job。`IgnoreComponentEnabledState` 的省略与否决定是否触发 sync point。不清晰的 contract 导致静默数据错误。

## EX-GAS 诊断
每个 `IJobEntity`/`IJobChunk` 旁注释列出对应的 EntityQuery 条件。Code review 时交叉验证。当前各 IJobEntity 缺少 EntityQuery 注释 → 增加 PRF-20 风险。

## 检查方法
- Grep 搜索 `IJobEntity` 和 `IJobChunk` 在 Runtime Core 目录中的使用
- 检查 query 属性（`[WithAll]`/`[WithNone]`/`[WithAny]`/`[WithOptions]`）是否完整
- Code review 交叉验证 query 属性和 Execute 参数是否一一对应
