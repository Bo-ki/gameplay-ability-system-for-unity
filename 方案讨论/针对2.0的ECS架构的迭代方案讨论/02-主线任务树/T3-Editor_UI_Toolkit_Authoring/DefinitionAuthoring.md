# Editor / UI Toolkit Authoring - Definition Authoring

## 父节点

[T3 Editor / UI Toolkit Authoring](README.md)

## 节点定位

本支线负责 Definition Authoring 的 UI Toolkit 展示、编辑入口、authoring snapshot 和 diagnostics。它只服务 Definition & Generation Layer。

## 当前问题

1. Authoring 若不跟随 Definition contract，会重新产生 Editor 状态和 runtime 状态断链。
2. 历史 Odin / PackageCache 问题曾污染工具链判断，需要明确不再作为主模型。

## 目标态参考

1. `01-目标态架构共识/09-Authoring-EditorSpec.md`
2. `01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`

## 历史方案参考

1. `历史方案参考/方案14.md`、`方案15.md` 的配置编辑和业务验证视角可参考。
2. 旧 1.x OOP authoring 方案不作为参考。

## 支线目标

提供能展示 Definition snapshot、校验配置 contract、辅助定位 generated artifact 问题的 UI Toolkit authoring 入口。

## 当前状态

后置。等待 T2 Definition / Luban 配置权威链进一步稳定。

## 非目标

1. 不恢复 Odin / Sirenix 作为 GAS authoring 主模型。
2. 不修改 `Library/PackageCache/...`。
3. 不让 Editor 状态成为 runtime gameplay 权威。

## 前置依赖

1. T2 Luban SourceGenerator 配置链完成基础验收。
2. Definition snapshot / validation graph 可供 Editor 消费。

## 执行范围

1. `Assets/GAS/Editor`
2. `Assets/GAS/Runtime/Definition`
3. UI Toolkit authoring views / diagnostics views

## 执行细则

1. Editor 只消费 Definition / Authoring snapshot。
2. 诊断必须能追溯到配置项、manifest 或 validation graph。
3. 不在 Editor 侧新增 runtime gameplay 规则。

## 验收门槛

1. Authoring Snapshot 展示接入。
2. 配置诊断可定位到具体定义项。
3. Runtime 不依赖 Editor 状态。

## 测试链路

1. Editor validation tests。
2. Definition generated artifact 检查。
3. Unity Editor 打开工具进行 UI 验证。

## 当前任务看板

| 任务ID | 任务名 | 状态 |
|---|---|---|
| T3-Authoring-Definition-1 | Editor / UI Toolkit Authoring - Definition Authoring - Authoring Snapshot 展示接入 | 后置 |

## 交还内容

1. 更新 T3 主线状态。
2. 更新 Authoring / Editor Spec。
3. 记录 Editor 验证证据。
