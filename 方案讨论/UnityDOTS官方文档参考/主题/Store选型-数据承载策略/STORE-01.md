# STORE-01：Store 必须声明 owner、生命周期、访问和顺序契约

**严重度**：P1
**Primary Owner**：Store选型-数据承载策略
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`；Collections `2.6.6`
**官方来源**：无直接官方规则；相关生命周期见 Entities `systems-data.md`、Collections `allocator-overview.md`

## 规则声明
Store 必须在类型名或邻近文档中清楚声明 owner、有效生命周期、主要访问/索引方式、writer 模型、顺序语义和 teardown。类型名优先表达最能消除歧义的维度，不强制一个名称同时编码所有信息。

## 为什么
`DataStore`/`EffectStore` 无法说明谁能写、何时失效和是否参与 replay；但把全部契约塞入名称会制造不可读类型。名称与 owner 文档共同形成单一契约。

## EX-GAS 诊断
`FrameEffectCommandStream` 可由名称表达生命周期/内容，并在 owner 文档补充 allocator、producer、total order 和 Dispose handle；`ActiveEffectSlots` 则由所在 owner entity 表达归属。

## 检查方法
Code review 能否仅通过类型与邻近 owner 文档回答六个选型维度；缺一项则补契约，不机械改成长名称。
