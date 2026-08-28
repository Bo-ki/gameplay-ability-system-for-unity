# V2 ASC 稳定 Slab 与 Handle

> 状态：V1 后；与 V3/V4 同一集成窗口 | 前置：V1

## Owner 输入

- [当前 authority 基线](../../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)
- [目标 Spec：ASC-local slab](../../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md)
- [配置语义编译与 CapacityProof 唯一裁决](../../01-目标态架构共识/25-配置语义编译契约与CapacityProof统一裁决Spec.md)

## 目标

把 GrantedAbility、Activation、Continuation、ActiveEffect 与长期 payload 收敛为 ASC-local 非压缩 slab，建立唯一 authority。

## 执行范围

1. slot index + generation + tombstone + owner-local free-list。
2. Ability/Activation/Continuation/Effect/payload/aggregator slab 与 capacity/overflow。
3. stale handle、generation overflow、terminal fact before recycle。
4. 删除或断开 Ability Entity、legacy GE entity、global ActiveEffect authority 的写入面。
5. 为 slot/payload/outbox/wait/pending、target shadow overlay 与 durable publish range 提供可定界高水位、reservation API 与不改变既有 handle 的增长规则；生成的 `CapacityProof/consumer map` 必须逐字段映射到实际查询、checked arithmetic 与 reservation API，V3 的 `WholeTickInfraAdmission` 使用整 Tick生成上界一次预留，禁止 per-ingress/per-transaction 容量失败形成部分 Tick提交。
6. 在 ASC archetype 上明确 owner-local writer、稳定 free-list 与 contribution/application ref 分离布局。

## 验收

- lifecycle slab 无 `RemoveAt`、swap-back 或 compact。
- slot churn 后长度有界且 stale handle 必定拒绝。
- 同一 gameplay state 只有一个 authority 写入面。
- 不存在 per-definition slot/entity promotion。
- payload/capture range 不引用会压缩的 buffer。
- capacity/overflow 测试证明 `InfraAdmissionFault` 前后整个 Tick的 ASC gameplay state/hash 相同，allocator 与 slab high-water 可观测；业务 stack overflow仍是 target typed result，不得混入 infra fault。
- 每个 generated capacity 字段恰有 runtime consumer；物理 slab/reservation 峰值可与 `CapacityProof` 对账，shadow credit 与 durable publish credit 不复用同一 range。

## 交还

ASC archetype map、free-list不变量、删除清单和 V3 writer contract；V3 未接通前不得把该分支发布为可运行双 Runtime。
