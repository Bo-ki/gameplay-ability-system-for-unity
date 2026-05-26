# TRF-05: Custom Transform 使用 WriteGroup + ManualOverride

**严重度**: P1
**Primary Owner**: Transform-层级
**来源**: `transforms-custom.md` / `TransformsCustom.cs`

## 规则声明
当实体需要完全替代标准 transform 系统（如 2D 网格坐标、固定轴旋转、非标准空间变换）时，通过 `[WriteGroup(typeof(LocalToWorld))]` 声明自定义 transform component，并在 Baker 中使用 `TransformUsageFlags.ManualOverride` 阻止标准 transform component 生成。

## 为什么
WriteGroup 使标准 `LocalToWorldSystem` 跳过持有自定义 transform 的 entity；`ManualOverride` 阻止 Baker 添加标准 transform component。两者结合确保自定义变换不被标准系统覆盖。

## EX-GAS 诊断
自定义 transform 必须有对应的 `[WriteGroup]` 声明；Baker 中 `TransformUsageFlags.ManualOverride`。

## 检查方法
搜索 `[WriteGroup(typeof(LocalToWorld))]` 确认与 `ManualOverride` 配对。检查自定义 transform system 是否在 `TransformSystemGroup` 中正确排序。
