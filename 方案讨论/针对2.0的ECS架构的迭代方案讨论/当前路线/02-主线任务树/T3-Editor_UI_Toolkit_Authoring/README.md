# T3 Editor / UI Toolkit Authoring

## 节点定位

本主线负责 Definition & Generation Layer 的 Authoring / Editor 输入侧。Editor 工具用于编辑、展示、校验定义输入，不反向定义 GAS Runtime Core Layer。

## 当前问题

1. 历史上 Editor / Odin / PackageCache 问题容易干扰 Runtime 主线。
2. Authoring 需要跟随 Definition contract，否则会重新产生 UI 状态和 runtime 状态不一致。
3. `09-Authoring-EditorSpec` 已在 AM-1K 审查中补齐 DOTS 规则引用（`BAKE-01`、`SYS-05`、`CONTENT-01`）和 Odin/Sirenix 拒绝理由；支线任务尚未展开。

## 目标态参考

1. `01-目标态架构共识/09-Authoring-EditorSpec.md`
2. `01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`
3. `01-目标态架构共识/02-四层架构Spec.md`
4. `01-目标态架构共识/12-命名规范Spec.md`

## 历史方案参考

1. `历史方案参考/方案14.md`、`方案15.md` 中的配置编辑、业务验证视角可参考。
2. 旧 1.x OOP authoring 方案无参考意义，不恢复。

## 主线目标

Editor authoring 只服务 Definition & Generation Layer，输出可校验的 authoring snapshot、diagnostics 和配置编辑体验。

## 非目标

1. 不恢复 Odin / Sirenix 作为 GAS authoring 主模型。
2. 不修改 `Library/PackageCache/...`，该目录变更会被 Unity 自动还原。
3. 不让 Editor UI 状态成为 runtime gameplay 权威。

## 执行范围

1. `Assets/GAS/Editor`
2. `Assets/GAS/Runtime/Definition`
3. `EX_GAS_Config/ProjectConfigTable/exgas_config`

## 执行细则

1. 新增 Editor 任务必须指向 Definition contract。
2. UI Toolkit 展示只消费 authoring / definition snapshot。
3. Editor 诊断信息应能追溯到 generated manifest / validation graph。

## 验收门槛

1. Authoring 展示和 Definition 数据来源一致。
2. Editor 错误能定位到配置项、生成物或 contract。
3. Runtime 不依赖 Editor 状态运行。

## 测试链路

1. Editor validation tests。
2. Definition generated artifact 检查。
3. Unity Editor 手动打开工具，仅作为 UI 验证证据。

## 支线索引

| 支线 | 文档 | 状态 |
|---|---|---|
| Definition Authoring | [DefinitionAuthoring.md](DefinitionAuthoring.md) | 后置 |

## 交还规则

交还时必须说明影响的 Definition contract、Editor 入口、验证方式和是否涉及 Package / generated 文件。
