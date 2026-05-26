# GAS ECS Runtime - Runtime Core 重构 - Unity Physics / Entities Graphics 新包覆盖

## 父节点

[RuntimeCore重构](README.md)

## 任务ID

`T1-RuntimeCore-AM1G`

## 状态

已完成

## 当前问题

1. Unity Physics / Entities Graphics 新包接入到当前项目后，Runtime Core 任务必须明确二者是否相关。
2. Physics 只允许作为目标获取、命中确认、空间 query、collision / trigger event 输入层。
3. Entities Graphics 只允许作为 Presentation / Boundary 渲染桥，不得反向污染 GAS Core 语义。

## 目标态参考

1. `UnityDOTS官方文档参考/README.md`
2. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`
3. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
4. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`
5. `UnityDOTS官方文档参考/主题/07-UnityPhysics-管线-查询-事件.md`
6. `UnityDOTS官方文档参考/主题/08-EntitiesGraphics-表现桥接.md`

## 目标 / 目的

1. 建立 `PHY-*` 和 `GFX-*` 规则族，明确 Physics / Graphics 在 GAS 架构中的边界。
2. 补充 `ODF-15` 到 `ODF-18`，把 Physics 配置、Physics 采样、Entities Graphics 和 render evidence 纳入覆盖矩阵。
3. Runtime Core 任务行动报告必须说明 Physics / Graphics 是否相关。

## 非目标

1. 不修改 Runtime 代码。
2. 不接入真实 Physics / Graphics 资源。
3. 不在本任务完成 Physics / Graphics 的 runtime 验证。

## 执行范围

1. `UnityDOTS官方文档参考/README.md`
2. `UnityDOTS官方文档参考/主题/07-UnityPhysics-管线-查询-事件.md`
3. `UnityDOTS官方文档参考/主题/08-EntitiesGraphics-表现桥接.md`
4. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`
5. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`

## 已完成交付物

1. `PHY-*` 规则族建立，覆盖 PhysicsWorldSingleton、SimulationSingleton、collision/trigger event、query broadphase 等。
2. `GFX-*` 规则族建立，覆盖 RenderMeshArray、MaterialMeshInfo、material override、render evidence 等。
3. `ODF-15` 到 `ODF-18` 补充到覆盖矩阵。
4. Runtime Core 执行细则新增 Physics / Graphics 条款：涉及时必须覆盖相关规则，不涉及时必须给出 not-related reason。
5. 完整历史见 [T0 已完成文档校准日志](../../T0-文档治理与目标态共识/已完成文档校准日志.md)。

## 验收标准

1. `PHY-*` 和 `GFX-*` 规则出现在 `90-规则编号索引` 中。
2. Runtime Core 任务行动报告和交还必须覆盖 `ODF-15..18`。
3. Runtime Core 任务树看板登记 `AM1G`。

## 测试链路

1. `rg -n "PHY-|GFX-|ODF-15|ODF-16|ODF-17|ODF-18|AM1G" 当前路线`
