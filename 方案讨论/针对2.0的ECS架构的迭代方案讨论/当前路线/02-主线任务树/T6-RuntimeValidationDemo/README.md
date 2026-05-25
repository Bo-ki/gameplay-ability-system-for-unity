# T6 Runtime Validation Demo

## 节点定位

本主线负责真实业务预演和自动验收 demo。当前首选验收载体是无画面 AutoChess：可自动运行、自动结算、自动验证，同时保留 UI / VFX / SFX / FloatingText / Cue 逻辑的日志占位。

## 当前问题

1. 早期 demo 业务不足以放大 Runtime Core 架构问题。
2. AutoChess 已能暴露性能和管线问题，但当前验证报告还需要与 Debugger / diagnostics 更强绑定。
3. 无头不等于省略表现逻辑，表现 / Cue 边界需要继续验收。
4. AutoChess 当前放在 `Assets/GAS/Runtime/Demo/AutoChess`，这是错误目录；Demo 业务必须从 Runtime Core 中剥离到 `Assets/AutoChessDemo`。
5. 当前 AutoChess 存在巨类和层次混杂，不能作为长期业务样板。
6. Unity Physics / Entities Graphics 接入后，AutoChess 必须区分默认 headless、physics-enabled 和 rendered profile；默认可不启用真实物理 / 渲染，但必须输出 disabled reason，启用时必须拆分 physics / render counters。
7. `10B-AutoChess完整业务案例设计Spec` 已提供完整的 GAS 业务设计预演（棋子/技能/GE/羁绊系统 + C# ISystem 代码），但实际代码迁移仍需等 Runtime Core Frame Backbone 闭合后才能启动。

## 目标态参考

1. `01-目标态架构共识/10-AutoChess无头验收Spec.md`
2. `01-目标态架构共识/10B-AutoChess完整业务案例设计Spec.md`
3. `01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`
4. `01-目标态架构共识/06-Observation-Presentation-ReplaySpec.md`
5. `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`
6. `UnityDOTS官方文档参考/README.md`
7. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`

## 历史方案参考

1. `历史方案参考/方案14.md`、`方案15.md` 的自走棋主题、业务闭环、四层模型和 Debugger 设计优先参考。
2. `方案12.md`、`方案13.md` 的当前 demo 真实业务案例可作为后置迁移参考。

## 主线目标

用可自动运行、自动结算、自动验证的业务 demo 证明 Runtime Core、配置链、Debugger 和表现/Cue 逻辑成立。

## 非目标

1. 不接真实 UI / VFX / SFX 资源。
2. 不用更多业务机制掩盖 Runtime Core 管线问题。
3. 不把无头 demo 降级为纯数值测试。

## 执行范围

1. 目标目录：`Assets/AutoChessDemo`
2. 当前迁移来源：`Assets/GAS/Runtime/Demo/AutoChess`
3. AutoChess validation / profile / summary export
4. 当前 demo 业务迁移文档和后续测试入口

## 执行细则

1. 无头表现交互使用 log marker 占位，但逻辑链路必须完整。
2. 报告必须区分 core simulation、observation projection、presentation marker、export / bootstrap 成本。
3. 规模 gate 先服务诊断，后服务优化验收。
4. 默认业务链路要精而完整，不堆叠大量机制；新增机制必须说明覆盖的 GAS 概念缺口。
5. Demo 架构必须允许未来用真实 UI / VFX / SFX / 角色资源替换 log adapter。
6. 涉及目标获取、命中、碰撞、触发器、表现资源或 rendered profile 时，必须显式处理 `ODF-15..18`；不启用时必须在 validation summary 中给出 disabled reason。

## 验收门槛

1. x1 默认链路 `passed=true`。
2. x50 能输出 runtime diagnostics 并解释热点。
3. UI / VFX / SFX / FloatingText / Cue marker 在无头报告中可追溯。
4. x10w / x100w 具备配置入口和压力测试设计口径。
5. 默认 headless profile 输出 Physics / Graphics disabled reason；physics-enabled / rendered profile 输出独立 physics / render counters，不能混入 `coreTickMs`。

## 测试链路

1. AutoChess 默认 validation。
2. AutoChess x50 profile。
3. SceneRuntime runner / headless runner 对照。

## 支线索引

| 支线 | 文档 | 状态 |
|---|---|---|
| AutoChess 无头验收 | [AutoChess无头验收.md](AutoChess无头验收.md) | 进行中 |
| 当前 Demo 业务迁移 | [当前Demo业务迁移.md](当前Demo业务迁移.md) | 后置 |

## 交还规则

交还时必须同步最近验证摘要、当前架构事实、相关 Debugger / Observation Spec 和 demo 支线状态。

