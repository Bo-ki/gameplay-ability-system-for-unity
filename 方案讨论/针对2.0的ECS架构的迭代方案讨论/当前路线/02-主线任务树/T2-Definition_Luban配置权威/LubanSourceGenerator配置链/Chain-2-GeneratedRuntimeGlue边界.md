# Definition / Luban 配置权威 - Luban SourceGenerator 配置链 - Generated Runtime Glue 边界

## 父节点

[Luban SourceGenerator 配置链](README.md)

## 任务ID

`T2-LubanSG-Chain-2`

## 状态

`候选`

## 目标 Spec

`01/08 Luban SourceGenerator`

## 当前问题

Generated runtime glue（static lookup、query layout hint、buffer capacity hint、calculation registry）的生成边界需要在配置链稳定后明确，避免生成代码越过 Definition & Generation Layer 进入 Runtime Core 语义。

## 目标态参考

1. `01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`
2. `01-目标态架构共识/03-RuntimeCore管线Spec.md`

## 非目标

1. 不生成 Ability / GE active lifecycle。
2. 不让 generated glue 成为 Runtime Core 语义权威。

## 前置依赖

1. T2-LubanSG-Chain-1 配置生成链路验收完成。
2. T1 Runtime Core 语义基本稳定。

## 执行范围

1. generated static lookup / query glue
2. generated calculation registry
3. generated PhysicsProfile / RenderProfile binding plan

## 验收标准

1. Static lookup 和 query glue 只输出 Definition & Generation Layer 数据。
2. 不产生托管 delegate 或可变静态表。
3. PhysicsProfile / RenderProfile 生成物只包含 Definition & Boundary 数据。
