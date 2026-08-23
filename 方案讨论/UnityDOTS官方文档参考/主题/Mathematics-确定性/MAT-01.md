# MAT-01：确定性 Runtime Core 禁用 UnityEngine.Random

**严重度**：P0
**Primary Owner**：Mathematics-确定性
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Unity `6000.3.14f1`；Mathematics `1.3.3`
**官方来源**：Mathematics `compatibility.md`、`random-numbers.md`

## 规则声明
影响 battle state、battle hash 或 replay 的 Runtime Core 禁止调用 `UnityEngine.Random`；统一使用具有显式 owner 的 `Unity.Mathematics.Random` 或项目定义的确定性随机服务。Presentation、Editor 工具和不反馈 gameplay 的诊断路径不在此禁令内。

## 为什么
`UnityEngine.Random` 可以通过 `InitState`/`state` 控制状态，但它是共享静态状态，任意调用者都会推进同一序列，难以隔离和审计，也不适合 Burst job。禁用理由不是“seed 无法控制”。

## EX-GAS 诊断
随机输入通过 battle seed 与稳定逻辑 stream ID 派生；记录 owner 和消费位置。Presentation 的随机变化必须保证不会反向写入 gameplay。

## 检查方法
CI 搜索确定性 Runtime Core 对 `UnityEngine.Random` 的引用；允许项必须位于明确边界并注明不参与 gameplay/replay。
