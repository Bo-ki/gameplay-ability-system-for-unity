# ISSUE-012 策划配置能力缺失

> 最近复核：2026-06-07 | 状态：Active | 严重度：P1 | 视角：策划配置能力 / Authoring 配置链

## 当前结论

当前 GAS Runtime / 配置链已经开始消费 generated catalog、`AbilityActivationPlanRecord`、`GECommandSeedRecord`、`ResolvedModifierRecord` 等运行时记录，但默认 Editor 配置入口仍是 Ability / Effect 单表行编辑器。也就是说，Runtime 语义已经从“Excel 行”推进到“definition catalog + command/spec/delta/fact”，策划配置界面却仍要求使用者理解 ID、协议字符串、offset raw column 和手动导出步骤。

这不是普通 UI 易用性问题，而是架构接口深度不足：复杂度没有被 Editor / Luban / SourceGenerator 配置链吸收，而是泄露给策划。当前缺失的不是几个控件，而是一整套 **Planner Configuration Capability**：业务模板、业务能力包、配置草稿图、保存前配置图校验、影响分析、Runtime trace preview、发布校验快照和平衡预览。

## 当前证据

| 事实 | 代码证据 | 策划配置视角判定 |
|---|---|---|
| Ability 页默认工具栏是打开 Excel / Json、导出、刷新、保存 | `Assets/GAS/Editor/GASCenterUIToolkit/GASCenterAbilityPage.cs:64-72` | 默认 mental model 是表文件，不是业务技能 |
| Ability 页用 `当前 Ability` / `新 ID` 作为编辑入口 | `Assets/GAS/Editor/GASCenterUIToolkit/GASCenterAbilityPage.cs:81-97` | 策划首先处理技术 ID，而不是“火球术 / 中毒 / 光环” |
| Ability 标签字段提示使用 `;` 分隔 ID | `Assets/GAS/Editor/GASCenterUIToolkit/GASCenterAbilityPage.cs:199-204` | 暴露裸 ID 列表，缺少 typed reference 和引用上下文 |
| Ability 保存直接组装列值并调用 `SaveRowWithRawColumns(...)` | `Assets/GAS/Editor/GASCenterUIToolkit/GASCenterAbilityPage.cs:303-350` | 保存对象是 row，不是业务包 diff |
| AbilityExecution 后 50 列仍按 base column offset 读写 | `Assets/GAS/Editor/GASCenterUIToolkit/GASCenterAbilityPage.cs:479-510` | 参数协议依赖列相对位置，策划无法判断语义完整性 |
| Effect 页直接暴露 `Modifiers` 文本协议 | `Assets/GAS/Editor/GASCenterUIToolkit/GASCenterEffectPage.cs:124` | 伤害、治疗、属性公式没有结构化编辑和预览 |
| Effect 页直接暴露 `GrantedAbility` 文本协议 | `Assets/GAS/Editor/GASCenterUIToolkit/GASCenterEffectPage.cs:131` | 授予能力链路缺少业务图和生命周期诊断 |
| Effect 页 TagRequirement 仍按 All / Any / None 文本 ID 输入 | `Assets/GAS/Editor/GASCenterUIToolkit/GASCenterEffectPage.cs:151-168` | requirement 不是 schema-bound 引用编辑 |
| Duration / Period / Stacking 通过 raw offset columns 写入 | `Assets/GAS/Editor/GASCenterUIToolkit/GASCenterEffectPage.cs:560-584` | 周期、堆叠、溢出策略的真实业务关系被拆散到列偏移 |
| Ability / Effect 导出仍手动调用 `CodeGenerator.TryGenerateGasConfigTables()` | `Assets/GAS/Editor/GASCenterUIToolkit/GASCenterAbilityPage.cs:423-434`, `Assets/GAS/Editor/GASCenterUIToolkit/GASCenterEffectPage.cs:475-486` | 保存、导出、生成、验证是分离步骤 |
| CodeGen pipeline 在 Luban rows 之后输出 validation report | `Assets/GAS/Editor/CodeGen/GasCodeGenPipeline.cs:63-115` | validation 是生成链报告，不是策划保存前诊断体验 |
| generated runtime glue 已把 definition 转成运行时 record | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeDefinitionGlue.gen.cs:15-108` | Runtime 语义已有 trace 目标，但 Editor 未提供预览 |

## 缺陷拆解

| 缺陷 | 当前表现 | 后果 |
|---|---|---|
| 配置对象错误 | 默认对象是 Ability row / Effect row | 策划必须自己维护跨表业务语义，工具没有承载“一个技能”的完整上下文 |
| 缺少业务模板 | 火球术、DOT、光环都要从空表行和字段协议开始 | 新内容创建路径长，容易漏建 GE / cooldown / cue / requirement |
| 裸 ID 引用 | Tag / Effect / Cue / Ability 以 int 和分隔符表达 | 引用缺失、误填、重复和孤儿配置很晚才暴露 |
| 协议字符串泄露 | `Modifiers`、`GrantedAbility`、TagRequirement、offset columns 是默认编辑方式 | 策划要学习底层 serialization 细节，复杂度没有被配置链吸收 |
| 配置图校验后置 | 保存 row 不等于校验业务链路 | 缺失引用、错误 carrier、stack overflow、period child effect 等问题推迟到导出、运行或人工测试 |
| 缺少影响分析 | 修改一个 GE 不知道影响哪些 Ability、Unit、Scenario、Scale profile | 平衡调整风险不可见，回归范围只能靠经验 |
| 缺少 Runtime trace preview | 策划看不到配置如何变成 plan / seed / modifier / fact / cue | “配置看起来对”与“运行时实际走对 lane”之间没有证据链 |
| 缺少发布门禁 | 没有业务包级 draft / publish / validation snapshot | 配置变更无法形成可审计发布单元 |
| 缺少平衡预览 | 冷却、消耗、等级、伤害公式、DOT tick、stack limit 分散 | 调参要跨表心算，难以批量比较和发现异常曲线 |
| 缺少场景闭环 | Unit / Scenario / Scale / Validation expectation 未与技能包默认联动 | AutoChess 或真实业务验收无法从配置直接形成最小可跑案例 |
| Editor 与 Runtime 语义断层 | Editor 看列和协议，Runtime 看 catalog / record / lane | Debugger 只能事后定位，不能帮助策划在保存前修正 |

## 交叉视角补充诊断

| 视角 | 当前缺口 | 目标态归口 |
|---|---|---|
| 模板目录 | 没有由 Luban schema / SourceGenerator metadata 驱动的 Business Template Catalog | `../01-目标态架构共识/20-策划配置能力交叉审查Spec.md` |
| 引用图 | 只能从字段和 ID 推断引用，缺少正反向 Config Reference Graph | `20` 的引用图与影响分析 |
| 发布门禁 | 保存 row、导出 JSON、CodeGen report 仍是分离动作，没有 Business Package Change Set | `20` 的 Config Review Gate / Publish Validation Snapshot |
| 协作变更 | Excel diff 不能解释业务影响，缺少 row diff + impact + trace 的 review 单元 | `20` 的 Business Package Change Set |
| 平衡调参 | 公式、冷却、DOT tick、stack 和 overflow 分散，缺少 Balance Preview | `19` / `20` 的平衡预览验收 |
| 场景验证 | Unit / Scenario / Scale profile / validation expectation 未与业务包默认绑定 | `20` 的 Scenario Validation Binding |
| SourceGenerator 绑定 | Editor 仍复制表头、协议和 choice 逻辑，缺少 Generated Editor Binding 权威 | `20` 的 SourceGenerator Editor Binding |

## 真实业务复盘

### 火球术

策划想表达“单体目标、消耗、冷却、命中伤害、命中 Cue”。当前路径要求先建 Ability ID，再建 Damage GE，再建 Cooldown GE，再回填 `CdEffect`，再在 Effect `Modifiers` 中写协议，再填 Cue ID，再导出。任何一步错填都没有业务包级诊断。

目标态应让该内容以 `SingleTargetDamageAbility` 模板创建，一次保存投影出 Ability row、Damage GE row、Cooldown GE row、Cue reference 和 Runtime trace preview。

### 中毒 DOT

策划想表达“持续 5 秒、每 30 帧 tick、最多 3 层、免疫 Tag 不生效”。当前路径把 Duration、Period、Stacking、ImmunityTags、TickCue 分散到多个 foldout 和 raw offset columns。配置者看不到 period child GE 是否存在、stack overflow policy 是否完整，也看不到 active store 承载是否合理。

目标态应让 `PeriodicStackingEffect` 模板直接生成配置草稿图，并在保存前给出 period / stack / immunity / cue 的配置图校验。

### 被动光环

策划想表达“来源单位给范围内友方授予攻击 Buff，离开范围移除”。当前路径只能拆成 GrantedAbility / GrantedTags / OngoingRequirement / RemovePolicy 等多段协议和 ID 列表。工具不能证明它是 ongoing granted state，而不是 instant GE 或每帧创建销毁实体。

目标态应让 `AuraGrantedEffect` 模板把 granted state、target filter、ongoing requirement 和 remove policy 作为一个业务图校验。

### 平衡调整

策划想把一个公共燃烧 GE 的伤害从 20 调成 18。当前工具不能展示它被哪些 Ability 引用、哪些 Unit 默认持有、哪些 Scenario 覆盖、哪些 Scale profile 的输出会变化。该缺口会把一次数字调整变成运行时回归风险。

目标态必须提供 Effect -> Ability -> Unit -> Scenario -> Validation expectation 的影响分析，并输出发布前 validation snapshot。

## 为什么按 P1 处理

该问题不直接发生在 Runtime Core hot path，因此不是 P0。但它会持续制造 P1 风险：

1. 配置错误被推迟到运行期，增加 Debugger 和人工测试压力。
2. 业务语义散落在表行和协议字段中，维护成本随内容规模线性扩大。
3. Luban / SourceGenerator 的潜力没有转化为策划可用能力，只停留在生成产物层。
4. 策划无法形成“编辑 -> 校验 -> 预览 -> 发布”的闭环，导致破坏性 ECS Runtime 重构缺少内容侧证据。

## 官方规则对照

| 规则 | 对本 issue 的约束 |
|---|---|
| `BAKE-01`~`BAKE-03` / `CASE-39`~`CASE-41` | Authoring / Baking / Runtime 必须分开；Editor draft 可以存在，但不能成为 Runtime source，必须投影到 Luban rows / generated catalog |
| `BLOB-01` / `BLOB-02` / `CASE-07` / `CASE-24` | 静态定义最终应进入 immutable Blob / catalog；业务包必须能预览自己将如何进入 catalog，而不是让 Runtime 反查 managed row |
| `BUR-01` | 公式、requirement、magnitude evaluator 目标态应生成 Burst-friendly static glue；默认编辑体验不能鼓励托管协议和运行时反射 |
| `SEL-01` / `STORE-03` | 配置数据、运行时瞬时记录、诊断数据、表现引用要先分类；业务包只是 Authoring 聚合，不得进入 Runtime Core hot path |
| `DBG-01`~`DBG-05` / `SYS-05` | Runtime trace preview 和 validation snapshot 只能作为诊断 / Editor / CI 证据，不得反向驱动 gameplay |
| `ODF-01`~`ODF-18` | 配置链必须能说明采用或拒绝哪些 DOTS 官方主题；只输出表和代码不足以形成流程闭环 |

## 退出条件

1. 默认新建对象是 Ability Package / Effect Package，不是单张 Ability / Effect row。
2. 策划默认路径不要求手写 `Modifiers`、`GrantedAbility`、TagRequirement 分隔协议、Duration / Period / Stacking offset raw columns。
3. 保存前必须生成 Config Graph Validation，覆盖缺失引用、循环引用、互斥策略、DOTS carrier、capacity hint 和 SourceGenerator gate。
4. 修改任意 Ability / GE / Cue / Tag / Attribute 时，工具能展示影响到的 Ability Package、Unit、Scenario、Scale profile 和 validation expectation。
5. 保存或发布前必须输出 Runtime trace preview：`Business Intent -> AbilityActivationPlanRecord -> GECommandSeedRecord -> ResolvedModifierRecord -> GameplayFact -> Boundary Cue`。
6. 发布必须形成 Publish Validation Snapshot，至少包含 row diff、schema hash、content hash、business package id、SourceGenerator validation summary 和 trace preview id。
7. Raw Table 入口保留为 Advanced Mode，但不能作为默认业务编辑路径。
8. Luban schema / SourceGenerator 必须输出 Editor binding metadata，让模板字段、choice source、引用类型和诊断上下文由生成链维护。
