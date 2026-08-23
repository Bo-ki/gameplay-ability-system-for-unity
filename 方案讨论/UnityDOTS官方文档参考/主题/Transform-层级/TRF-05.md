# TRF-05：Custom Transform 显式提供 LocalToWorld 并接管写入

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities 1.4.6
**严重度**：P1
**Primary Owner**：Transform-层级
**来源**：`transforms-custom.md`、官方 `TransformsCustom.cs` 示例

## 规则声明

完全替换内置 transform 时：

1. 自定义 transform component 标记 `[WriteGroup(typeof(LocalToWorld))]`。
2. Baker 使用 `TransformUsageFlags.ManualOverride`。
3. Baker 显式添加自定义 component 和 `LocalToWorld`；若有层级也显式添加 `Parent`。
4. 自定义 system 负责更新 `LocalToWorld` 并正确排序。

`ManualOverride` 本身不会生成 `LocalToWorld`。缺少第 3 步会使写入系统查询不到目标组件或 `SetComponent` 失败。
