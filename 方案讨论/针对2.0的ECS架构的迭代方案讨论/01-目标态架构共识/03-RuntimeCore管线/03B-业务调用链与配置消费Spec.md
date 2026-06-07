# 03B：业务调用链与配置消费

> Owner：`01-目标态架构共识/03-RuntimeCore管线` | 状态：目标态子 Spec 索引 | 拆分来源：`../03-RuntimeCore管线Spec.md` | 最近拆分：2026-06-08

本文件只保留 03B 目标态主题索引。正文已拆入同名子目录；禁止在此追加当前代码事实、迁移流水、验证数字或下一步任务。

## 职责

03B 定义 Runtime Core 中业务调用链、Luban 配置生成链 Runtime 消费方式，以及 Generated Runtime Glue 面向真实 GAS 业务的纯消费接口。

## 阅读顺序

| 子页 | 职责 |
|---|---|
| [03B-01 业务调用链](03B-业务调用链与配置消费/03B-01-业务调用链Spec.md) | 目标态 Boundary / Core producer / fan-in / magnitude / active effect / attribute / fact / projection 的业务数据流。 |
| [03B-02 Luban 配置生成链 Runtime 消费](03B-业务调用链与配置消费/03B-02-Luban配置生成链Runtime消费Spec.md) | 目标态 Excel / Luban / CodeGen / Baker / Catalog / Runtime lane 的只读配置消费链。 |
| [03B-03 Generated Runtime Glue 消费接口](03B-业务调用链与配置消费/03B-03-GeneratedRuntimeGlue消费接口Spec.md) | 目标态 generated pure glue 的接口形态、禁止行为和业务消费代码骨架。 |

## Owner 边界

1. 当前代码事实、generated artifact 清单、验证命中数和迁移 proof 写入 `../../00-当前架构事实/`。
2. 可领取任务、退出门和测试链路写入 `../../02-主线任务树/`。
3. 短期接力和未跑项写入 `../../04-当前进度状态/`。
4. 本索引和子页只维护目标态 Spec、禁止方向、官方依据和验收门槛。

## 归档

拆分前全文快照见 [2026-06-08-03B-业务调用链与配置消费Spec拆分前](../_归档/2026-06-08-03B-业务调用链与配置消费Spec拆分前.md)。

## 反向入口

- 03 子 Spec 索引：[README.md](README.md)
- 03 总览：[../03-RuntimeCore管线Spec.md](../03-RuntimeCore管线Spec.md)
- 01 总入口：[../README.md](../README.md)
