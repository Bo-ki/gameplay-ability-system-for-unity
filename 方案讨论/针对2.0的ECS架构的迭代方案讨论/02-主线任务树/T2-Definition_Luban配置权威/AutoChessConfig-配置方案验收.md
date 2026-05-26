# Definition / Luban 配置权威 - Luban SourceGenerator 配置链 - AutoChessDemo 配置方案验收

## 父节点

[Luban SourceGenerator 配置链](README.md)

## 任务ID

`T2-LubanSG-AutoChessConfig`

## 状态

`候选`

## 目标 Spec

`01/11 AutoChessDemo Luban`

## 当前问题

AutoChessDemo 的 Luban 配置方案需要独立验收，确保 Unit / Ability / GE / Cue / Scenario / ScaleProfile 表结构能支撑 x1 到 x100w 的配置驱动验收。

## 目标态参考

1. `01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`
2. `01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`

## 非目标

1. 不生成 gameplay lifecycle。
2. 不把真实资源导入作为本任务验收条件。

## 前置依赖

1. T2-LubanSG-Chain-1 配置生成链路验收完成。

## 执行范围

1. `Assets/AutoChessDemo/Config`
2. `EX_GAS_Config/ProjectConfigTable/exgas_config` AutoChess 表

## 验收标准

1. AutoChessDemo Unit / Ability / GE / Cue 表可 Luban 生成。
2. ScaleProfile 覆盖 x1 / x50 / x100 / x1000 / x10w / x100w。
3. PhysicsProfile / RenderProfile 有配置入口。
