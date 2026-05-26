# PRF-12: 审核 SharedComponent 使用

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md`

## 规则声明
SharedComponent 在 IJobEntity 中使用时，参数必须以 `in` 传递且只能只读。托管类型 SharedComponent 会导致 Burst 编译回退。避免在高频路径中使用 SharedComponent 作为分类/过滤条件，优先使用 enableable component 或 tag component。

## 为什么
SharedComponent 的托管特性在多线程环境中有额外管理成本。Burst 编译回退到托管代码的性能损失可达 10-100x。SharedComponent 的变化会导致 archetype chunk 重组，影响 query 遍历效率和 chunk 排列。

## EX-GAS 诊断
审查 Runtime Core 中 SharedComponent 的使用模式。检查高频路径中是否意外使用了 SharedComponent 作为遍历条件。

## 检查方法
- 搜索 `ISharedComponent` 实现和 `GetSharedComponentManaged` 调用
- 评估 SharedComponent 在 hot path 中使用的影响
