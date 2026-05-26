# TRF-04: 使用正确 TransformUsageFlags 避免冗余 transform component

**严重度**: P1
**Primary Owner**: Transform-层级
**来源**: `transforms-usage-flags.md`

## 规则声明
Baker 中必须根据实体的运行时需求选择最小 `TransformUsageFlags`。纯逻辑 entity（无表现、无 world-space 坐标需求）使用 `None`；静态表现 entity 使用 `Renderable`；仅在需要运行时修改 transform 时使用 `Dynamic`。

## 为什么
`Dynamic` 生成完整 transform hierarchy（`LocalTransform` + `Parent` + `LocalToWorld`），每多一个 component 增加 chunk 内存占用和 archetype 排列数。静态 entity 使用 `Renderable` 只生成 `LocalToWorld`，减少 2/3 的 transform component 存储。

## EX-GAS 诊断
检查每个 Baker 的 `TransformUsageFlags` 声明；`Dynamic` 必须附带注释说明运行时修改 transform 的理由。

## 检查方法
审计所有 Baker 文件中 `bakingType` / `GetEntity` / `CreateAdditionalEntity` 的 TransformUsageFlags 参数。
