# PRF-27: 禁止手动调用其他数据处理 System 的 Update()

**严重度**: P1
**Primary Owner**: 数据流-系统生命周期
**来源**: `systems-version-numbers.md`

## 规则声明

禁止在 `ISystem.OnUpdate()` 中手动调用另一个数据处理 system 的 `Update()` 方法。若两个 system 都处理 entity 数据（尤其使用 `SetChangedVersionFilter` 时），调用方 EntityQuery 会看到被调用方处理产生的错误变更版本号。

## 为什么

变更版本号被破坏 → `SetChangedVersionFilter` 失效 → 响应式系统漏触发或误触发。编译器不报错，运行时静默产生错误行为。在 GAS 中表现为：effect 重评估遗漏变更、attribute 变更事件丢失、reactive system 不触发。

## EX-GAS 诊断

GAS Runtime Core 中任何 `otherSystem.Update()` 调用在 `ISystem.OnUpdate` 内 → 必须重构为：a) 抽取公共 job struct 在两个 system 中分别调度；b) 通过 SystemGroup 排序确保执行顺序。

## 检查方法

Grep `.Update()` 调用在 `OnUpdate()` 方法内部；确认被调用方是否处理 entity 数据。
