# 16-01D：最小完整代码骨架判定门

> Owner：`01-目标态架构共识/16-纯ECS内核与边界重划分/16-01` | 状态：目标态 Spec 子页 / 判定入口 | 拆分来源：`../16-01-目标分层官方依据与不变量Spec.md` | 最近整理：2026-06-08

本文件只维护“什么样的目标代码骨架才算最小完整”的判定门、阅读顺序和冲突裁决。完整端到端代码正文的唯一 owner 是 [`../16-06-端到端消息流代码骨架Spec.md`](../16-06-端到端消息流代码骨架Spec.md)。本页不再复制第二份完整代码，避免 Shell、Boundary、Core、Fan-in、Debugger 和 SourceGenerator 规则在两个文件中分叉。

## 职责裁决

| 主题 | 唯一正文 owner | 本页是否维护正文 |
|---|---|---|
| 目标分层与官方依据 | [16-01A](16-01A-目标分层与官方依据Spec.md) | 否，只引用 |
| Owner Map 与重划分判定 | [16-01B](16-01B-判定准则与OwnerMapSpec.md) | 否，只引用 |
| Shell / Boundary / Core 消息协议 | [16-01C](16-01C-目标态消息流协议Spec.md) | 否，只引用 |
| 完整端到端代码骨架 | [16-06](../16-06-端到端消息流代码骨架Spec.md) | 否，只定义最小完整判定门 |
| Boundary command / Core resolve 局部骨架 | [16-02](../16-02-BoundaryCommand与CoreCommandResolveSpec.md) | 否，只检查是否被端到端链路消费 |
| Fan-in / Debugger / SourceGenerator 局部骨架 | [16-03](../16-03-FanInDebuggerSourceGeneratorSpec.md) | 否，只检查是否被端到端链路消费 |
| Shell capability public seam | [16-04](../16-04-ShellCapabilityContractSpec.md) | 否，只检查是否泄露 ECS handle |
| Snapshot / Identity / API Health | [16-05](../16-05-SnapshotIdentityApiHealthSpec.md) | 否，只检查是否进入验收门 |

## 最小完整判定门

一份目标态代码骨架只有同时满足下列条件，才允许称为“最小完整”：

| 判定项 | 必须出现 | 不合格信号 |
|---|---|---|
| Shell public seam | session、target ref、intent、request id、snapshot version、evidence snapshot 等业务 / 证据 value object | public interface 返回 `World`、`EntityManager`、raw `Entity`、`EntityQuery` 或 writable buffer |
| Runtime Boundary write/read 分权 | command append 与 snapshot read 分成不同 capability、不同 evidence 字段和不同计时口径 | command API 同步读 live state，snapshot API 顺手写 command |
| Core lane owner | query、type handle、lookup、allocator、dependency、carrier、structural policy 都归具体 `ISystem` / job owner | static helper、generated lifecycle 或 OOP manager 隐式拥有 Runtime Core lane |
| Command record | Shell intent 被压成 owner-local ECS record，再由 Core lane 批处理 | Shell 直接调用 damage、cooldown、stack、period、requirement 或 execution calculation |
| Fan-in | 多 producer 写入 transient carrier，随后 deterministic merge，并暴露 segment、capacity、spill、merge cost 或 battle hash evidence | 用 singleton `DynamicBuffer` 或 worker 写入顺序当作 scale-ready 证明 |
| ActiveEffect / Attribute / Fact owner | 跨帧 state、slot、period、stack、delta、fact 和 cleanup intent 均有明确 owner | lifecycle 散落在 helper、generated `OnUpdate`、command stream 或 Debugger export |
| Runtime lifecycle owner | Ability、GameplayEffect、ActiveEffect、Attribute、Tag、Cue 的生命周期由手写 ECS lane / store owner 承担；SourceGenerator 只提供 pure record / lookup / evaluator | generated artifact 拥有 lifecycle `ISystem`、`OnUpdate`、query、ECB、NativeContainer allocator 或 hidden dependency |
| StructuralCommit | create / destroy / add / remove 只能消费 structural intent，并在明确 ECB playback phase 发生 | Core simulation lane、Shell 或 generated glue 直接执行结构变化 |
| Diagnostics evidence | Debugger 输出机器可读 counter / fact / official capture state；日志、Mermaid、战报和 UI 只能派生 | 字符串日志、summary hash、Mermaid 图或 disabled profiler reason 单独证明性能 |
| Definition & Generation | SourceGenerator 只生成 immutable Blob lookup、static index、pure evaluator、validation metadata | SourceGenerator 生成 runtime lifecycle owner、query、ECB、NativeContainer owner 或 hidden scheduler |
| Module depth | 每个 Module 的 Interface 只暴露业务 intent、record、snapshot、evidence 或 owner-local result，Implementation 隐藏 query / lookup / allocator / dependency / carrier / capacity / merge / structural playback | facade 名义很薄，但调用方仍要理解 ECS handle、singleton owner、NativeContainer owner、capacity 或 diagnostics owner |
| Dependency graph | Shell / Adapter、Runtime Core、Diagnostics、Presentation、Definition Glue 之间是单向依赖，无反向环；每条依赖都能解释消息方向和 owner | 目录或 namespace 看似分层，但 Core 反向依赖 Shell / Debugger，或 generated lifecycle 与 Runtime Core 互相拥有调度 |
| Cost domain | Core、Boundary、Diagnostics、Runner、Presentation、Official Capture 和 Derived Export 有独立 timing / evidence 字段 | 平均 tick、字符串 summary 或单个 runner 耗时混合多个成本域 |
| Generation purity | generated artifact 只输出 catalog、lookup、pure evaluator、validation、Editor / Baker metadata；runtime lifecycle 由 hand-written Core owner 承担 | generated artifact 拥有 `OnUpdate`、system registration、query、ECB、NativeContainer allocator、mutation lifecycle 或 hidden dependency |
| DOTS API 依据 | 每个承载选择能对照 `SYS/JOB/QRY/SC/ECB/BUF/NAT/DBG/BLOB/BUR` 规则说明 | proof-only carrier、managed lookup 或同步 fence 没有重新选型触发条件 |

## 阅读顺序

```text
16-01A 目标分层与官方依据
  -> 16-01B Owner Map 与职责判定
  -> 16-01C 消息流协议
  -> 本页最小完整判定门
  -> 16-06 完整端到端代码骨架
  -> 16-02..16-05 局部专题 Spec
```

读取 `16-06` 时，本页只作为审查清单使用：检查代码骨架是否覆盖 Shell intent、Boundary command、Core lane、fan-in、Definition pure glue、Diagnostics evidence、Derived export 和 StructuralCommit。若缺某个局部主题，回到 `16-02..16-05` 补齐局部 Spec；不要在本页粘贴第二份代码。

## 冲突裁决

1. 若本页与 `16-06` 对同一完整代码骨架的字段、类型名、消息方向或验收门描述不一致，以 `16-06` 为完整代码正文 owner，本页只修正判定门。
2. 若 `16-06` 与 `16-02..16-05` 对局部专题规则不一致，先判断是否是局部 owner 规则升级；局部规则由对应专题 Spec 维护，`16-06` 负责同步端到端拼接，不新增第三份正文。
3. 若 `16-01C` 的协议与 `16-06` 的代码骨架不一致，`16-01C` 只维护消息协议，`16-06` 维护代码实现形态；两者必须收敛到同一单向消息流。
4. 若目标态代码骨架需要新增 DOTS API、carrier、allocator 或 measurement fence，先对照 `../../../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md` 和 `../../../../UnityDOTS官方文档参考/主题/90-规则编号索引.md`，再更新唯一正文 owner。

## 禁止方向

1. 不在本页恢复完整 C# 代码正文。
2. 不把 `16-06` 的端到端代码、`16-02` 的 Boundary command、`16-03` 的 fan-in 或 `16-04` 的 Shell capability 复制成第二份规则正文。
3. 不记录当前代码事实、文件行号、验证数字、执行流水、领取条件或完成证明。
4. 不用“已有接口”“已有 generated artifact”“已有日志字段”替代最小完整判定门。
5. 不把拆分作为唯一治理动作；当两个文件维护同一规则时，必须优先裁决唯一 owner，旧处降权为引用或判定入口。
