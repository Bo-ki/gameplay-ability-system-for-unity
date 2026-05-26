# GFX-05: Rendered profile 必须与 Core 成本独立计时

**严重度**: P1
**Primary Owner**: EntitiesGraphics
**来源**: `entities-graphics-performance.md`

## 规则声明
Profiling 输出中，`coreTickMs`（纯 GAS Runtime Core 时间）与 `renderMs`（Entities Graphics 时间）必须分离。无头模式中 `renderMs` 标记为 disabled，不能空白跳过。

## 为什么
1) 无头模式渲染时间为 0，Core 时间不变；2) Rendered profile 的 renderMs 应额外记录 batch、draw call、material override、render world 信息；3) 渲染成本混入 coreTickMs 掩盖 Core 性能瓶颈。

## EX-GAS 诊断
Summary 输出包含 `renderMs` 列（即使 0 也写入 disabled reason）；rendered profile 输出 batch count 和 draw call count。

## 检查方法
确认 `GASManager` 或 `Debugger` 中 renderTime 与 coreTime 使用独立计时器；无头 profile 中输出 "Entities Graphics disabled" 标记。
