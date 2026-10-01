# EX-GAS 2.0：按 ECS 规范逐模块审查

**基线：61daa507e52e823ff42a8cb8c8ec91716c7e80e7 · 分支 EX-GAS-2.0 · 日期 2026-10-01**

本目录是对“每个模块分别出具审查报告，并以当前项目 ECS 文档为标准”的交付。包含 **24 份模块报告**，以及标准方法和文件归属清单；不同于前轮集中列出六项问题的总报告。

本次仅提交审查文档，没有修改产品代码、配置或原有规范，也没有运行 Unity、项目脚本、测试或性能测量。源码静态符合不等于测试通过或发布合格。项目文件已按用户要求浅克隆到云端，用于读取与报告引用核对。

## 阅读顺序

1. [审查标准、冲突处理与证据等级](00-审查标准与方法.md)
2. 下表选择模块，查看“规范条目 → 源码证据 → 判定 → 验收”
3. [第一方 C# 文件归属清单](02-第一方文件归属清单.md)，核对范围；归属不等于该文件全部路径已动态验证
4. 按模块报告中“实际阅读/抽样/未读”理解覆盖限度

## 模块报告索引

| 模块 | 独立报告 | 总体发现摘要 |
|---|---|---|
| M01 Session / World | [报告](M01-Session-World.md) | World唯一owner与父链正确；同World重装偏差、多Battle终局隔离目标缺口 |
| M02 Identity / Handles | [报告](M02-Identity-Handles.md) | 代际句柄与验证静态符合；跨模块复用、溢出和ABA动态证据待补 |
| M03 Layout / Archetypes | [报告](M03-Layout-Archetypes.md) | ASC-local布局与固定逻辑长度；IBC选型、预算维度及规模证据未闭合 |
| M04 Storage / Allocators | [报告](M04-Storage-Allocators.md) | 非压缩槽/范围分配；全表校验成本与exact-size碎片需测量 |
| M05 Install / Admission | [报告](M05-Install-Admission.md) | 当前C0 negative-only，完整production install admission仍是目标差距 |
| M06 Ability / Wait | [报告](M06-Ability.md) | one-shot生命周期已实现；Commit二次requirement、Wait/cooldown通用收尾待闭合 |
| M07 Effect / Policy | [报告](M07-Effect.md) | typed effect与period已有；immunity/contribution/cycle等扩展语义存在目标差距 |
| M08 Attribute | [报告](M08-Attribute.md) | finite/clamp/revision与失败原子性；持久Aggregator及可逆贡献未闭合 |
| M09 Tag | [报告](M09-Tag.md) | count权威/presence派生；贡献ledger与大规模重建成本待补 |
| M10 System / Tick DAG | [报告](M10-System-Tick-DAG.md) | 单Kernel/DAG与标准EndFixed；target并行目标、shadow成本及发布证明待补 |
| M11 Boundary / Command / Drain | [报告](M11-Boundary-Command-Drain.md) | typed command与单drain；Cue envelope、消费策略分层与回执目标待补 |
| M12 Diagnostics / Replay | [报告](M12-Diagnostics-Replay.md) | 诊断派生出口；未采集项、固定70%阈值与只读快照契约需完善 |
| M13 Targeting / Physics | [报告](M13-Targeting-Physics.md) | TargetCatcher作为authoring；实际Physics adapter与spatial profile尚未证明 |
| M14 Definition / Catalog / Proof contracts | [报告](M14-Definition-Catalog-Proofs.md) | immutable Catalog/lookup/校验有效；closed-world限制与proof消费覆盖未解耦 |
| M15 CodeGen Semantics / Proofs | [报告](M15-CodeGen-Semantics-Proofs.md) | canonical/typed/budget基础已建；full eligibility与真实consumer接线未闭合 |
| M16 Generation / SourceGenerator / CLI | [报告](M16-Generation-SourceGenerator-Tools.md) | 当前SourceGenerator/单selector路线成立；编译计划不等于真实编译资格 |
| M17 Luban / Authoring configuration | [报告](M17-Luban-Authoring-Configuration.md) | Luban与authoring配置链；跨阶段设置快照、输入一致性与能力范围需明确 |
| M18 Editor / GAS Center / Timeline | [报告](M18-Editor-GASCenter-Timeline.md) | Editor与Timeline属于managed authoring；写回完整性及预览反馈风险 |
| M19 AutoChess Application / Integration | [报告](M19-AutoChess-Application-Integration.md) | 独立World与公开命令链；最终结果封印和计时归因目标偏差 |
| M20 Presentation / Cue / Resources | [报告](M20-Presentation-Cue-Resources.md) | 表现层合法managed边界；prefab lease/复用以及Cue lifecycle目标不足 |
| M21 General / XParam / Utilities | [报告](M21-General-XParam-Utilities.md) | General/XParam可托管；culture、wall-clock限域与资源/池契约待完善 |
| M22 Tests / Verification tooling | [报告](M22-Tests-Verification-Tooling.md) | 已有广泛测试代码；过时生成文件门、退出码和证据可复验性问题 |
| M23 Packaging / Dependencies / Repository | [报告](M23-Packaging-Dependencies-Repository.md) | 依赖与版本可查；构建工程遗漏、包元数据及独立安装未闭合 |
| M24 Documentation / Standards governance | [报告](M24-Documentation-Standards-Governance.md) | 标准层级明确；Cleanup错误案例、SEL索引与状态漂移需治理 |

## 覆盖与适用性

- Runtime：Session、Identity、Layout、Storage、Install、Ability/Wait、Effect、Attribute、Tag、System、Boundary、Diagnostics/Replay、Targeting、Definition全部有主报告
- 配置与工具：CodeGen语义/Proof、物理生成路线、SourceGenerator/CLI、Luban、编辑器与Timeline分别审查
- 应用与交付：AutoChess、Cue/Presentation/资源、General/XParam、测试工具、包依赖及文档治理分别审查
- 目录中的测试、生成物、元数据、第三方库和历史案例不与产品Runtime混算；二进制、第三方实现、全量场景和平台设置的排除范围在M23及相关模块明确列出
- 逻辑模块跨多个子目录时合并为一个报告；跨模块共用文件可交叉引用。不会为每个空目录或纯meta目录机械新建“模块”
- 第一方C#路径清单共 366 项，全部分配主报告。该数字包含工具/测试及目录内生成C#，不是“已逐行验证文件数”，更不是通过数

## 跨模块裁决与避免误判

### 1. 当前范围与最终目标分开

当前Runnable closed-world明确限制Tag、requirements、cost/cooldown、定义数量与ID。它解释某些缺口为何不会在现有样例触发，但不把这些目标差距删除。报告同时列受限符合与目标缺口，不给虚构“整体合规率”。

### 2. 既有设计应保留

不可变Catalog、stable handle、typed command、owner-local slab、显式依赖、preflight/final token、Boundary派生和已有测试是资产。不因存在目标差距就推倒重写，也不要求所有Shell、Editor、Cue强行Burst化。

### 3. 标准本身发生冲突时

M24记录CASE-15与同提交官方cleanup原件相矛盾。本轮采用“原实体保留至最后cleanup移除”的机制，不按错误案例要求复制或反复DestroyEntity。原子规则属于EX-GAS项目规则，不伪装成Unity官方强制。

### 4. SourceGenerator当前路线

ADR-0001已接受StableGraphSourceGenerator单selector。保留历史tarball实验与旧任务树作为证据，不恢复过时路线。Compile plan封存、物理发布、Unity编译、完整semantic eligibility是不同状态，不能互相替代。

### 5. 共享问题只建立一个整改项

- 同World Dispose→Install：M01主问题，M10交叉引用
- Cue cycle/Boundary envelope/consumer配对：M07、M11、M20不同层证据，实施时统一生命周期合同
- 证据与构建可复现：M22验证链、M23仓库交付各管职责，不重复宣称历史测试失败
- Cleanup规范错误：M24主问题，M23仅交叉提示

## 建议推进层次（未实施）

1. **先保护源数据和真实结论**：Excel冲突/ID校验、过时验证脚本、退出码与证据可达性、错误标准案例
2. **恢复可重复交付**：必要构建工程、clean-clone bootstrap、固定源码/配置/selector/结果身份
3. **再扩一个业务能力**：现有即时伤害语义的新ID技能，从authoring到公开Activate/Commit再到Boundary精确结果；不为该ID增加Runtime专用分支
4. **按目标逐项闭合**：需求驱动开放Cancel、Tag/requirements、持久contribution、Wait、Cue lifecycle、多Battle与完整install；每项有正负验收
5. **最后以目标ScaleProfile证明性能/发布**：Core/Physics/EndFixed/Drain/Presentation分别采样，记录JIT/AOT、SafetyChecks、平台和编译选项

通用GAS框架优先还是实际游戏优先仍由项目owner决定。上述报告和建议不自动更改现有任务状态、ADR、支持面或发布承诺。

## 交付核对

本目录源码与规范链接绑定审查SHA。提交前检查文档文件完整性、模块编号、目录归属、相对链接、引用文件存在与行号边界；这些仅是**报告质量检查**，不是项目编译/测试。审查覆盖以各模块已读清单为准，没有声称全仓逐行穷举或所有动态条件已证实。

