# GFX-03: Presentation group 内禁止结构变化

**严重度**: P1
**Primary Owner**: EntitiesGraphics
**来源**: `requirements-and-compatibility.md`

## 规则声明
Entities Graphics 的 Presentation system group 内不允许结构变化操作。需要创建/销毁渲染 entity 的结构变化必须通过 ECB 延迟到合适的 playback phase。

## 为什么
Graphics 渲染管线（BRG）在 Presentation group 内持有内部缓冲区引用。结构变化使这些引用失效，导致渲染错误或安全系统异常。

## EX-GAS 诊断
Debugger 标记 `PresentationSystemGroup` 内的直接结构变化调用。

## 检查方法
搜索 `CreateEntity` / `DestroyEntity` / `AddComponent` / `RemoveComponent` 在 `[UpdateInGroup(typeof(PresentationSystemGroup))]` 标记的 system 中。
