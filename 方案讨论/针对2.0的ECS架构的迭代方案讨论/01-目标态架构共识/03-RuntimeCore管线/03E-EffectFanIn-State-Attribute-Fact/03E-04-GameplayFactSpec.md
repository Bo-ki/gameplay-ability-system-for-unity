# 03E-04：Gameplay Fact

> Owner：`01-目标态架构共识/03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-Fact` | 状态：目标态 Spec 子页 | 最近拆分：2026-06-08

本文件只描述目标态 Gameplay Fact 投影。当前代码事实、验证数字、执行流水和迁移 proof 不写入本文件。

## 定位

Gameplay Fact 分为两类：`CoreReactionFact` 是 Runtime Core 内的权威 reaction 输入，`BoundaryObservationFact` 是 Boundary Projection 导出的只读观察结果。Core fact 只写 target owner-local fact buffer；Presentation / Replay / Debugger 只能消费 Boundary observation，不得直接读取 Core owner-local buffer，也不得把观察结果反写 gameplay。

## Owner 分离验收

目标态 Gameplay Fact 不能只以“存在 fact buffer”作为完成证明，必须同时满足以下约束：

1. Core reaction 与 Boundary observation 使用不同 carrier；Core carrier 只在 Runtime Core lane 内读写，Boundary carrier 只在 BoundaryProjection 及其下游只读消费。
2. Core reaction 的消费者必须声明 bounded reaction pass、next-frame command seed 或明确的 no-reaction policy，避免同帧无界连锁。
3. Boundary observation 必须由 Core carrier 单向派生，并记录 export cursor、owner group、drop / sampling policy、projection lag 和 capacity / spill evidence。
4. Presentation、Replay、Debugger、structured log 和 AutoChess report 只能读取 Boundary observation 或 diagnostics snapshot；任何直接读取 Core fact carrier 的路径都判定为目标态失败。
5. Fact lane 的 release-ready gate 必须与 battle hash、Core / Boundary / Diagnostics timing split 和 scale profile 对账；字符串日志、图表或 summary 只能作为 derived export。

## 多源 Fact Lane 合约

目标态允许多个 Runtime Core lane 产生 `CoreReactionFact`，但每个来源都必须先声明 source lane contract，不能让 fact 写入面靠隐式 helper、生成物生命周期或全局 stream 约定扩散：

1. 每个 source lane 必须声明 source id、输入 carrier、目标 owner、排序 key、容量预算、drop / spill 策略和反应语义；未声明 source contract 的 fact 写入判定为目标态失败。
2. Attribute reduce、instant reduce、execution output reduce、period tick、tag change、death / interrupt 等 source lane 都只能写 target owner-local Core carrier；不得直接写 Boundary observation carrier。
3. SourceGenerator 只能生成 fact code、definition lookup、static requirement evaluator、pure record builder 和 validation graph；不得生成拥有 query、ECB、NativeContainer 生命周期或 fact flush 权限的 gameplay source lane。
4. Core lane 之间通过 bounded reaction pass 或 next-frame command seed 连接；禁止同帧无限递归地从 fact 触发 command、再立即触发新的 fact。
5. Boundary export 必须按 source lane、owner、frame、sequence 和 local index 对账，Debugger evidence 必须能区分各 source lane 的写入量、flush 量、drop / spill 和 projection lag。

## 目标代码骨架

```csharp
using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public enum EGameplayFactKind : byte
    {
        CoreReaction = 1,
        BoundaryObservation = 2,
    }

    public struct OwnerLocalGameplayFactPending : IComponentData, IEnableableComponent
    {
    }

    [InternalBufferCapacity(8)]
    public struct OwnerLocalGameplayFactBuffer : IBufferElementData
    {
        public EGameplayFactKind Kind;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int FactCode;
        public int AttributeCode;
        public int Frame;
        public int Sequence;
        public float OldValue;
        public float NewValue;
    }

    public struct BoundaryObservationFactOutboxComponent : IComponentData
    {
        public int ExportFrame;
        public int ExportSequence;
    }

    [InternalBufferCapacity(64)]
    public struct BoundaryObservationFactBuffer : IBufferElementData
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int FactCode;
        public int AttributeCode;
        public int Frame;
        public int Sequence;
        public float OldValue;
        public float NewValue;
    }

    internal struct BoundaryFactExportRecord
    {
        public Entity Owner;
        public int LocalIndex;
        public OwnerLocalGameplayFactBuffer Fact;
    }

    internal struct BoundaryFactExportRecordComparer : IComparer<BoundaryFactExportRecord>
    {
        public int Compare(BoundaryFactExportRecord x, BoundaryFactExportRecord y)
        {
            var result = x.Owner.Index.CompareTo(y.Owner.Index);
            if (result != 0) return result;

            result = x.Owner.Version.CompareTo(y.Owner.Version);
            if (result != 0) return result;

            result = x.Fact.Frame.CompareTo(y.Fact.Frame);
            if (result != 0) return result;

            result = x.Fact.Sequence.CompareTo(y.Fact.Sequence);
            if (result != 0) return result;

            return x.LocalIndex.CompareTo(y.LocalIndex);
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASAttributeSetReduceApplySystem))]
    public partial struct GASDeathFactProjectionSystem : ISystem
    {
        private EntityQuery _targetQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<CombatAttributeCurrentSetComponent>(),
                    ComponentType.ReadOnly<AttributeDirtyMaskComponent>(),
                    ComponentType.ReadWrite<OwnerLocalGameplayFactPending>(),
                    ComponentType.ReadWrite<OwnerLocalGameplayFactBuffer>()
                }
            });

            state.RequireForUpdate(_targetQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ProjectDeathFactsJob
            {
                EntityType = state.GetEntityTypeHandle(),
                CombatCurrentType = state.GetComponentTypeHandle<CombatAttributeCurrentSetComponent>(true),
                DirtyMaskType = state.GetComponentTypeHandle<AttributeDirtyMaskComponent>(true),
                FactPendingType = state.GetComponentTypeHandle<OwnerLocalGameplayFactPending>(false),
                FactBufferType = state.GetBufferTypeHandle<OwnerLocalGameplayFactBuffer>(false)
            }.ScheduleParallel(_targetQuery, state.Dependency);
        }

        [BurstCompile]
        private struct ProjectDeathFactsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public ComponentTypeHandle<CombatAttributeCurrentSetComponent> CombatCurrentType;
            [ReadOnly] public ComponentTypeHandle<AttributeDirtyMaskComponent> DirtyMaskType;
            public ComponentTypeHandle<OwnerLocalGameplayFactPending> FactPendingType;
            public BufferTypeHandle<OwnerLocalGameplayFactBuffer> FactBufferType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var combatValues = chunk.GetNativeArray(ref CombatCurrentType);
                var dirtyMasks = chunk.GetNativeArray(ref DirtyMaskType);
                var factsByTarget = chunk.GetBufferAccessor(ref FactBufferType);
                var factPending = chunk.GetEnabledMask(ref FactPendingType);

                if (!useEnabledMask)
                {
                    for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                    {
                        ProjectEntity(entityIndex, unfilteredChunkIndex, entities, combatValues, dirtyMasks, factsByTarget, factPending);
                    }

                    return;
                }

                var enumerator = new ChunkEntityEnumerator(true, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    ProjectEntity(entityIndex, unfilteredChunkIndex, entities, combatValues, dirtyMasks, factsByTarget, factPending);
                }
            }

            private static void ProjectEntity(
                int entityIndex,
                int unfilteredChunkIndex,
                NativeArray<Entity> entities,
                NativeArray<CombatAttributeCurrentSetComponent> combatValues,
                NativeArray<AttributeDirtyMaskComponent> dirtyMasks,
                BufferAccessor<OwnerLocalGameplayFactBuffer> factsByTarget,
                EnabledMask factPending)
            {
                var dirty = dirtyMasks[entityIndex];
                if ((dirty.CombatWord & AttributeDirtyBits.Health) == 0)
                    return;

                var combat = combatValues[entityIndex];
                if (combat.Health > 0f)
                    return;

                var facts = factsByTarget[entityIndex];
                facts.Add(new OwnerLocalGameplayFactBuffer
                {
                    Kind = EGameplayFactKind.CoreReaction,
                    SourceAsc = Entity.Null,
                    TargetAsc = entities[entityIndex],
                    FactCode = GameplayEventCodes.DamageResolved,
                    AttributeCode = AttributeCodes.Health,
                    Frame = combat.Frame,
                    Sequence = facts.Length,
                    OldValue = combat.PreviousHealth,
                    NewValue = combat.Health,
                });
                factPending[entityIndex] = true;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup))]
    [UpdateBefore(typeof(PresentationOutboxProjectionSystem))]
    public partial struct GASBoundaryFactExportSystem : ISystem
    {
        private EntityQuery _ownerFactQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _ownerFactQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<OwnerLocalGameplayFactPending>(),
                    ComponentType.ReadWrite<OwnerLocalGameplayFactBuffer>(),
                },
            });

            state.RequireForUpdate(_ownerFactQuery);
            state.RequireForUpdate<BoundaryObservationFactOutboxComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var outboxEntity = SystemAPI.GetSingletonEntity<BoundaryObservationFactOutboxComponent>();
            var records = new NativeList<BoundaryFactExportRecord>(
                AllocatorManager.ToAllocator(state.WorldUpdateAllocator));

            var collectHandle = new CollectOwnerFactsJob
            {
                EntityType = state.GetEntityTypeHandle(),
                FactPendingType = state.GetComponentTypeHandle<OwnerLocalGameplayFactPending>(false),
                FactBufferType = state.GetBufferTypeHandle<OwnerLocalGameplayFactBuffer>(false),
                Records = records,
            }.Schedule(_ownerFactQuery, state.Dependency);

            state.Dependency = new ExportBoundaryFactsJob
            {
                OutboxEntity = outboxEntity,
                OutboxLookup = SystemAPI.GetComponentLookup<BoundaryObservationFactOutboxComponent>(false),
                OutboxFacts = SystemAPI.GetBufferLookup<BoundaryObservationFactBuffer>(false),
                Records = records,
            }.Schedule(collectHandle);
        }

        [BurstCompile]
        private struct CollectOwnerFactsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            public ComponentTypeHandle<OwnerLocalGameplayFactPending> FactPendingType;
            public BufferTypeHandle<OwnerLocalGameplayFactBuffer> FactBufferType;
            public NativeList<BoundaryFactExportRecord> Records;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var owners = chunk.GetNativeArray(EntityType);
                var factPending = chunk.GetEnabledMask(ref FactPendingType);
                var ownerFacts = chunk.GetBufferAccessor(ref FactBufferType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var facts = ownerFacts[entityIndex];
                    for (var factIndex = 0; factIndex < facts.Length; factIndex++)
                    {
                        Records.Add(new BoundaryFactExportRecord
                        {
                            Owner = owners[entityIndex],
                            LocalIndex = factIndex,
                            Fact = facts[factIndex],
                        });
                    }

                    facts.Clear();
                    factPending[entityIndex] = false;
                }
            }
        }

        [BurstCompile]
        private struct ExportBoundaryFactsJob : IJob
        {
            public Entity OutboxEntity;
            public ComponentLookup<BoundaryObservationFactOutboxComponent> OutboxLookup;
            public BufferLookup<BoundaryObservationFactBuffer> OutboxFacts;
            public NativeList<BoundaryFactExportRecord> Records;

            public void Execute()
            {
                if (OutboxEntity == Entity.Null
                    || !OutboxLookup.HasComponent(OutboxEntity)
                    || !OutboxFacts.HasBuffer(OutboxEntity))
                {
                    return;
                }

                Records.Sort(new BoundaryFactExportRecordComparer());

                var outbox = OutboxLookup[OutboxEntity];
                var outboxFacts = OutboxFacts[OutboxEntity];
                outboxFacts.Clear();

                for (var i = 0; i < Records.Length; i++)
                {
                    var fact = Records[i].Fact;
                    if (fact.Kind != EGameplayFactKind.CoreReaction)
                        continue;

                    outboxFacts.Add(new BoundaryObservationFactBuffer
                    {
                        SourceAsc = fact.SourceAsc,
                        TargetAsc = fact.TargetAsc,
                        FactCode = fact.FactCode,
                        AttributeCode = fact.AttributeCode,
                        Frame = fact.Frame,
                        Sequence = outbox.ExportSequence++,
                        OldValue = fact.OldValue,
                        NewValue = fact.NewValue,
                    });
                }

                OutboxLookup[OutboxEntity] = outbox;
            }
        }
    }
}
```

## 合理性

1. `GASDeathFactProjectionSystem` 只读 committed AttributeSet 与 dirty mask、写 target owner-local fact buffer，不做结构变化；死亡销毁、grant / remove 等进入 Structural Commit。
2. `OwnerLocalGameplayFactBuffer` 是 Core reaction carrier，生命周期由 Core lane 和 Boundary Projection 明确管理；它不是 Presentation event bus，也不是全局 singleton fan-in 总线。
3. `GASBoundaryFactExportSystem` 负责把 owner-local fact 以 owner / frame / sequence / local index 排序后投影为 `BoundaryObservationFactBuffer`，保证 Presentation / Replay / Debugger 的观察顺序可复现。
4. `AttributeDirtyMaskComponent` 将 AttributeSet 打包后的业务变化重新收敛到具体 AttributeCode，避免 Boundary / Fact 因 set 打包而全量扫描所有属性字段。
5. CoreReactionFact 与 BoundaryObservationFact 分离后，Ability trigger / reactive GE 可以继续消费 Core fact 并重新进入 Effect Fan-In；表现、replay、日志和 UI 只能消费 Boundary observation，不反向驱动 simulation。

## 与 Structural Commit / Boundary Projection 的关系

1. [03F Structural Commit 与 Boundary Projection](../03F-StructuralCommit与BoundaryProjectionSpec.md) 负责结构变化意图、ECB playback、presentation outbox 和只读 projection。
2. 本文件只维护 Core reaction fact 的生成与 Boundary observation export 规则，不维护表现层资源、UI 文本或 replay 文件格式。
3. 如果 fact 被用于表现层或 replay，必须先由 Boundary Projection 生成只读派生，不能让 Presentation 反写 Core。
4. Boundary outbox 可以是 bounded buffer、snapshot ring 或 diagnostics sink，但必须声明容量、drop 策略、export cursor 和 cost domain；不得把 Core owner-local fact buffer 直接暴露给 Application Shell。

## 反向入口

- 03E 子页索引：[README.md](README.md)
- 03E 根索引：[../03E-EffectFanIn-State-Attribute-FactSpec.md](../03E-EffectFanIn-State-Attribute-FactSpec.md)
