# 03B 业务调用链与配置消费子 Spec 索引

> Owner：`01-目标态架构共识/03-RuntimeCore管线/03B-业务调用链与配置消费` | 状态：目标态 Spec 子目录 | 最近拆分：2026-06-08

本目录从 `../03B-业务调用链与配置消费Spec.md` 拆出 03B 目标态正文。根 `03B` 文件只保留短索引，本目录内文件才是各主题正文 owner。

## 纯度规则

1. 本目录只回答 Runtime Core 目标态业务调用链、配置消费链和 generated pure glue 应该如何设计。
2. 当前代码事实、文件行号、generated report 数字、执行流水、迁移进度和下一步任务不得写入本目录正文。
3. 现实证据必须回到 `../../../00-当前架构事实/`；任务拆分必须回到 `../../../02-主线任务树/`；短期接力必须回到 `../../../04-当前进度状态/`。
4. 需要 DOTS API 依据时，优先引用 `../../../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`、`21-官方文档覆盖与流程闭环.md` 和 `90-规则编号索引.md`。

## 子页索引

| 文件 | 职责 |
|---|---|
| [03B-01 业务调用链](03B-01-业务调用链Spec.md) | 目标态业务调用链、lane 顺序和逻辑链不变量。 |
| [03B-02 Luban 配置生成链 Runtime 消费](03B-02-Luban配置生成链Runtime消费Spec.md) | 目标态 Definition Catalog / Blob / lookup / runtime read pattern。 |
| [03B-03 Generated Runtime Glue 消费接口](03B-03-GeneratedRuntimeGlue消费接口Spec.md) | 目标态 generated pure glue 的 Runtime Core 消费接口、禁止事项和代码骨架；生成链流程、artifact 责任和 SourceGenerator 权限分别回到 `08` / `14` / `15`。 |

## 反向入口

- 03B 根索引：[../03B-业务调用链与配置消费Spec.md](../03B-业务调用链与配置消费Spec.md)
- 03 Runtime Core 管线索引：[../README.md](../README.md)
- 01 总入口：[../../README.md](../../README.md)
