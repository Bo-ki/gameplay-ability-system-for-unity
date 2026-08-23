# 纯 ECS 内核与边界重划分 Spec

## 结论

v1 的深接口、浅边界由三条线决定：ASC Entity 内聚全部 gameplay 权威；`GasTickKernelSystem` 内聚全部 fixed-tick 计算；`GasBoundaryDrainSystem` 是唯一 ECS→managed 出站。Shell、Debugger、SourceGenerator 和派生 Entity 都不得成为第二 Runtime。

专题正文见 [16 子目录](16-纯ECS内核与边界重划分/README.md)。全局语义与物理管线分别由 [00 总览](00-总览Spec.md)、[03 Runtime Core](03-RuntimeCore管线Spec.md) 维护，本系列只定义跨层 interface、owner 和代码骨架。

## 深接口判据

调用方只需要知道业务 intent、稳定 identity、强类型 handle、不可变 batch/snapshot 和错误原因；query、allocator、container、dependency、slot、capacity、merge、playback 与 cleanup 都留在实现 owner 内。

删除一个 interface 后，复杂度必须集中回一个明确 owner，不能扩散给 Adapter/Helper/Generated code/Debugger。只把 `EntityManager` 藏到 facade 后面不算边界完成。

## 最小 owner

| owner | 深层职责 | 对外能力 |
|---|---|---|
| `GasRuntimeSession` | World/Epoch、catalog/layout、完整 FixedStep/EndFixed/Drain、dispose | install、tick batch、dispose result |
| `GasCommandPort` / `SessionIngressGate` | Boundary 持久 `BoundaryIngressJournal`、RequestSequence、accept/fault-close 线性化、stale/reject policy | `Request*` intent |
| `GasTickKernelSystem` | query、WorldUpdateAllocator、Job DAG、target mutation、facts | 无 managed gameplay API |
| ASC Entity | slots、Attribute/Tag、pending work、ASC-scope cleanup outbox | 仅 opaque handle/snapshot identity |
| Session Entity | Battle/ASC registry、Boundary inbox、Battle/Session-scope cleanup outbox | session/battle snapshot identity |
| `GasBoundaryDrainSystem` | 唯一 freeze BatchId/InFlightWatermark → copy/sort → staging receipt → accepted prefix clear、ASC/Session shell cleanup、immutable ring | read-only batch cursor/snapshot |
| Catalog lifetime | Blob install/hash/dispose | immutable catalog handle |

## 删除门

- 五个旧 GAS SystemGroup 和自定义 Structural ECB。
- Ability/ActiveEffect/Task/Spec 权威 Entity。
- Legacy GameplayEffect Entity 与 global ActiveEffect authority。
- 多个 ECS Boundary consumer、全局 EventBus 和 per-consumer Core cursor。
- generated lifecycle/query/ECB/container owner。
- raw Entity 穿过 Boundary 或以 Avatar 重绑销毁 ASC。

这是一刀切换，不设置 runtime feature flag、fallback backend 或双调度路线。
