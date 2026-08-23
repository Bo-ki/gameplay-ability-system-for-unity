# PRF-34：禁止对嵌套 NativeContainer 的 component 调度 IJobChunk/IJobEntity

**严重度**：P0
**Primary Owner**：NativeContainer-Allocator
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `components-nativecontainers.md`

## 规则声明
若 `IComponentData` 字段包含 NativeContainer，不能把该 component 作为 `IJobChunk`/`IJobEntity` 的组件访问目标。应在主线程获取 component，提取其中的 container，并将 container 本身传给 job。

## 为什么
这两类 job 已通过 ECS 容器访问 component，调度安全系统不会递归扫描嵌套 NativeContainer，因为那会显著增加调度成本；Entities 因此明确禁止该调度方式。不是“提取到临时 NativeArray 后才能 job”，也不应描述为未定义行为：可直接对提取出的原 NativeContainer 调度并串联依赖。

## EX-GAS 诊断
若 singleton component 持有长期 store，使用 singleton API 取得 component/容器，再调度读写该容器的 job；不得让 query/job 直接匹配该嵌套 container component。

## 检查方法
搜索 component 中的 NativeContainer 字段；检查是否被 `IJobChunk`/`IJobEntity` 参数、handle 或 query 直接访问，并核对提取后 job 的依赖链。
