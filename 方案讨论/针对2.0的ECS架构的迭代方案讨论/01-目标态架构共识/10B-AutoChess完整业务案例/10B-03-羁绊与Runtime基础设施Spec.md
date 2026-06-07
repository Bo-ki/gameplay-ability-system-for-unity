# 10B-03：羁绊与 Runtime 基础设施目标设计

> Owner：`01-目标态架构共识/10B-AutoChess完整业务案例` | 状态：目标态子 Spec 入口 | 最近拆分：2026-06-07

本文件只作为 10B-03 的阅读入口和职责索引。拆分前全文已归档到 `../_归档/2026-06-07-10B-03-羁绊与Runtime基础设施Spec拆分前.md`；当前目标态正文由 10B-03A 和 10B-03B 分别维护。

## 拆分边界

| 子 Spec | 唯一职责 | 不承载 |
|---|---|---|
| [10B-03A 羁绊系统](10B-03A-羁绊系统Spec.md) | AutoChess 羁绊配置、羁绊检测、typed fact 输出、deterministic merge 和 fact reaction 目标约束 | Runtime Core 通用组件 / buffer 目录 |
| [10B-03B Runtime 基础设施](10B-03B-Runtime基础设施Spec.md) | Frame-local stream/range、lane metadata、target grouped range、structural intent、command/request、ASC identity、static lookup 和 buffer capacity 目标约束 | 具体羁绊业务规则 |

## 纯度规则

1. 本组文件只回答 AutoChess 完整业务案例中“羁绊机制和 Runtime 基础设施的目标态应该如何设计”。
2. 当前代码事实、文件行号、验证日志、执行流水、迁移进度和下一步任务必须写入 `../../00-当前架构事实/`、`../../02-主线任务树/` 或 `../../04-当前进度状态/`。
3. 若需要对照当前实现，只能在事实 owner 中记录对照结果，再由任务树引用本组目标约束。
4. `10B-03A` 和 `10B-03B` 不互相复制正文；一个规则属于业务机制还是底层承载，先按“是否可被非羁绊业务复用”判定。

## 目标态总览

```text
AutoChess Unit Tags / Camp / Alive State
  -> Synergy Detect IJobChunk
  -> NativeStream typed facts
  -> Deterministic Fact Merge
  -> next-frame command seed / gameplay fact reaction

Frame Arena / Runtime Infrastructure
  -> lane metadata
  -> frame-local command/spec/mutation/fact stream
  -> target grouped compact range
  -> owner-local apply / boundary projection
```

## 关联 Spec

1. Runtime Core fan-in、state、attribute 和 fact 规则：[03E Effect Fan-In / State / Attribute / Fact](../03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-FactSpec.md)；具体正文见 [03E 子页索引](../03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-Fact/README.md)。
2. Entity / Component 物理布局规则：[13 EntityComponent 物理布局](../13-EntityComponent物理布局Spec.md)。
3. Effect command / spec / attribute delta 规则：[04 EffectCommand-SpecStream-AttributeDelta](../04-EffectCommand-SpecStream-AttributeDeltaSpec.md)。
4. Runtime Core Debugger evidence 规则：[07 RuntimeCoreDebugger](../07-RuntimeCoreDebuggerSpec.md)。
5. AutoChess Luban 配置规则：[11 AutoChessDemo Luban 配置方案](../11-AutoChessDemo-Luban配置方案Spec.md)。

## 验收门槛

1. 羁绊业务规则能通过配置和 tag/camp/alive state 进入 ECS 数据流，不依赖 OOP event bus 或 GameObject hot path。
2. 羁绊检测默认使用 chunk/job 化统计和 frame-local typed fact 输出，不在 Core hot path 做主线程全局扫描。
3. Runtime 基础设施只提供 frame-local writer/reader、owner-local range 和 lane metadata；不把 singleton DynamicBuffer 当 gameplay command/spec/fact/mutation 总线。
4. Structural change 只通过 structural intent 进入 StructuralCommit，不允许羁绊业务 System 直接播放 ECB 或直接改表现层。
5. Debugger / validation / diagram 只能消费 structured fact、range 和 evidence snapshot，不反写 gameplay truth。
