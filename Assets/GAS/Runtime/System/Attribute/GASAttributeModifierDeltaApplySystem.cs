using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEExecutionCalculationOutputModifierSystem))]
    [UpdateBefore(typeof(AttributeOwnerMarkerRequestSystem))]
    [UpdateBefore(typeof(AttributeRecalculateSystem))]
    [UpdateBefore(typeof(GameplayFactProjectionSystem))]
    public partial struct GASAttributeModifierDeltaApplySystem : ISystem
    {
        private EntityQuery _ownerDeltaQuery;

        public void OnCreate(ref SystemState state)
        {
            _ownerDeltaQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<PendingAttributeModifierComponent>(),
                    ComponentType.ReadWrite<AttributeModifierBuffer>(),
                    ComponentType.ReadWrite<AttributeValueBuffer>(),
                    ComponentType.ReadWrite<AttributeDirtyComponent>(),
                    ComponentType.ReadOnly<ASCDestroyingComponent>(),
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState,
            });
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            var ownerApplyHandle = new ApplyOwnerLocalPendingAttributeModifierDeltaChunkJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                PendingOwnerTypeHandle =
                    SystemAPI.GetComponentTypeHandle<PendingAttributeModifierComponent>(isReadOnly: false),
                DestroyingTypeHandle = SystemAPI.GetComponentTypeHandle<ASCDestroyingComponent>(isReadOnly: true),
                DirtyTypeHandle = SystemAPI.GetComponentTypeHandle<AttributeDirtyComponent>(isReadOnly: false),
                DeltaBufferTypeHandle = SystemAPI.GetBufferTypeHandle<AttributeModifierBuffer>(isReadOnly: false),
                AttributeBufferTypeHandle = SystemAPI.GetBufferTypeHandle<AttributeValueBuffer>(isReadOnly: false),
                StreamEntity = streamEntity,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: false),
            }.Schedule(_ownerDeltaQuery, state.Dependency);

            var pendingDeltas = new NativeList<PendingDeltaApplyRecord>(64, state.WorldUpdateAllocator);
            state.Dependency = new ApplyStreamMigrationPendingAttributeModifierDeltaJob
            {
                StreamEntity = streamEntity,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                DeltaLookup = SystemAPI.GetBufferLookup<AttributeModifierBuffer>(isReadOnly: false),
                FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: false),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: false),
                DestroyingLookup = SystemAPI.GetComponentLookup<ASCDestroyingComponent>(isReadOnly: true),
                DirtyLookup = SystemAPI.GetComponentLookup<AttributeDirtyComponent>(isReadOnly: false),
                PendingDeltas = pendingDeltas,
            }.Schedule(ownerApplyHandle);
        }

        private struct PendingDeltaApplyRecord
        {
            public int DeltaIndex;
            public int Sequence;
            public Entity TargetAsc;
        }

        [BurstCompile]
        private struct ApplyOwnerLocalPendingAttributeModifierDeltaChunkJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<PendingAttributeModifierComponent> PendingOwnerTypeHandle;
            [ReadOnly] public ComponentTypeHandle<ASCDestroyingComponent> DestroyingTypeHandle;
            public ComponentTypeHandle<AttributeDirtyComponent> DirtyTypeHandle;
            public BufferTypeHandle<AttributeModifierBuffer> DeltaBufferTypeHandle;
            public BufferTypeHandle<AttributeValueBuffer> AttributeBufferTypeHandle;
            public Entity StreamEntity;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GameplayEventBuffer> FactLookup;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !FactLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                var pendingCount = 0;
                var appliedCount = 0;
                var skippedCount = 0;
                var targetGroupCount = 0;
                var maxTargetRange = 0;
                var factPatchCount = 0;

                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var pendingOwners = chunk.GetNativeArray(ref PendingOwnerTypeHandle);
                var pendingMask = chunk.GetEnabledMask(ref PendingOwnerTypeHandle);
                var destroyingMask = chunk.GetEnabledMask(ref DestroyingTypeHandle);
                var dirtyMask = chunk.GetEnabledMask(ref DirtyTypeHandle);
                var deltaBuffers = chunk.GetBufferAccessor(ref DeltaBufferTypeHandle);
                var attributeBuffers = chunk.GetBufferAccessor(ref AttributeBufferTypeHandle);
                var stream = StreamLookup[StreamEntity];
                var facts = FactLookup[StreamEntity];
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    if (!pendingMask[entityIndex])
                        continue;

                    var owner = entities[entityIndex];
                    var deltas = deltaBuffers[entityIndex];
                    var ownerPendingCount = CountPendingDeltas(deltas);
                    if (ownerPendingCount <= 0)
                    {
                        ClearOwnerPending(entityIndex, deltas, pendingOwners, pendingMask);
                        continue;
                    }

                    pendingCount += ownerPendingCount;
                    if (destroyingMask[entityIndex])
                    {
                        skippedCount += ownerPendingCount;
                        ClearOwnerPending(entityIndex, deltas, pendingOwners, pendingMask);
                        continue;
                    }

                    targetGroupCount++;
                    if (ownerPendingCount > maxTargetRange)
                        maxTargetRange = ownerPendingCount;

                    var attributes = attributeBuffers[entityIndex];
                    var groupApplied = false;
                    for (var deltaIndex = 0; deltaIndex < deltas.Length; deltaIndex++)
                    {
                        var delta = deltas[deltaIndex];
                        if (!AttributeModifierBufferFlags.RequiresApply(delta.Flags))
                            continue;

                        if (delta.TargetAsc == Entity.Null)
                            delta.TargetAsc = owner;

                        if (CompareEntity(delta.TargetAsc, owner) != 0
                            || !ApplyDelta(
                                attributes,
                                delta.AttrSetCode,
                                delta.AttributeCode,
                                delta.Op,
                                delta.Magnitude,
                                out var oldValue,
                                out var newValue))
                        {
                            skippedCount++;
                            continue;
                        }

                        delta.OldValue = oldValue;
                        delta.NewValue = newValue;
                        delta.Flags = (delta.Flags & ~AttributeModifierBufferFlags.RequiresCoreApply)
                                      | AttributeModifierBufferFlags.AppliedByCore;
                        deltas[deltaIndex] = delta;
                        appliedCount++;
                        groupApplied = true;
                        factPatchCount += UpdateLinkedExecutionFact(facts, delta.Sequence, oldValue, newValue);
                        AppendAttributeChangeFact(facts, ref stream, in delta, oldValue, newValue);
                    }

                    if (groupApplied)
                        dirtyMask[entityIndex] = true;

                    ClearOwnerPending(entityIndex, deltas, pendingOwners, pendingMask);
                }

                if (pendingCount <= 0)
                    return;

                WritePendingAttributeDeltaStats(
                    ref stream,
                    pendingCount,
                    appliedCount,
                    skippedCount,
                    targetGroupCount,
                    maxTargetRange,
                    estimatedRandomLookupCount: 0,
                    factPatchCount,
                    migrationCarrierCount: 0);
                StreamLookup[StreamEntity] = stream;
            }

            private int CountPendingDeltas(DynamicBuffer<AttributeModifierBuffer> deltas)
            {
                var count = 0;
                for (var i = 0; i < deltas.Length; i++)
                {
                    if (AttributeModifierBufferFlags.RequiresApply(deltas[i].Flags))
                        count++;
                }

                return count;
            }

            private void ClearOwnerPending(
                int entityIndex,
                DynamicBuffer<AttributeModifierBuffer> deltas,
                NativeArray<PendingAttributeModifierComponent> pendingOwners,
                EnabledMask pendingMask)
            {
                deltas.Clear();
                pendingOwners[entityIndex] = default;
                pendingMask[entityIndex] = false;
            }
        }

        [BurstCompile]
        private struct ApplyStreamMigrationPendingAttributeModifierDeltaJob : IJob
        {
            public Entity StreamEntity;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<AttributeModifierBuffer> DeltaLookup;
            public BufferLookup<GameplayEventBuffer> FactLookup;
            public BufferLookup<AttributeValueBuffer> AttributeLookup;
            [ReadOnly] public ComponentLookup<ASCDestroyingComponent> DestroyingLookup;
            public ComponentLookup<AttributeDirtyComponent> DirtyLookup;
            public NativeList<PendingDeltaApplyRecord> PendingDeltas;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !DeltaLookup.HasBuffer(StreamEntity)
                    || !FactLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                PendingDeltas.Clear();
                var pendingCount = 0;
                var deltas = DeltaLookup[StreamEntity];
                var facts = FactLookup[StreamEntity];
                for (var i = 0; i < deltas.Length; i++)
                {
                    var delta = deltas[i];
                    if (!AttributeModifierBufferFlags.RequiresApply(delta.Flags))
                        continue;

                    pendingCount++;
                    PendingDeltas.Add(new PendingDeltaApplyRecord
                    {
                        DeltaIndex = i,
                        Sequence = delta.Sequence,
                        TargetAsc = delta.TargetAsc,
                    });
                }

                if (pendingCount <= 0)
                    return;

                PendingDeltas.Sort(new PendingDeltaApplyRecordComparer());
                var appliedCount = 0;
                var skippedCount = 0;
                var targetGroupCount = 0;
                var maxTargetRange = 0;
                var estimatedRandomLookupCount = 0;
                var factPatchCount = 0;

                var rangeStart = 0;
                while (rangeStart < PendingDeltas.Length)
                {
                    var target = PendingDeltas[rangeStart].TargetAsc;
                    var rangeEnd = rangeStart + 1;
                    while (rangeEnd < PendingDeltas.Length
                           && CompareEntity(target, PendingDeltas[rangeEnd].TargetAsc) == 0)
                    {
                        rangeEnd++;
                    }

                    var rangeLength = rangeEnd - rangeStart;
                    if (target == Entity.Null
                        || IsDestroying(target)
                        || !AttributeLookup.HasBuffer(target))
                    {
                        skippedCount += rangeLength;
                        rangeStart = rangeEnd;
                        continue;
                    }

                    targetGroupCount++;
                    if (rangeLength > maxTargetRange)
                        maxTargetRange = rangeLength;
                    estimatedRandomLookupCount++;

                    var attributes = AttributeLookup[target];
                    var groupApplied = false;
                    for (var recordIndex = rangeStart; recordIndex < rangeEnd; recordIndex++)
                    {
                        var record = PendingDeltas[recordIndex];
                        var delta = deltas[record.DeltaIndex];
                        if (!AttributeModifierBufferFlags.RequiresApply(delta.Flags)
                            || !ApplyDelta(
                                attributes,
                                delta.AttrSetCode,
                                delta.AttributeCode,
                                delta.Op,
                                delta.Magnitude,
                                out var oldValue,
                                out var newValue))
                        {
                            skippedCount++;
                            continue;
                        }

                        delta.OldValue = oldValue;
                        delta.NewValue = newValue;
                        delta.Flags = (delta.Flags & ~AttributeModifierBufferFlags.RequiresCoreApply)
                                      | AttributeModifierBufferFlags.AppliedByCore;
                        deltas[record.DeltaIndex] = delta;
                        appliedCount++;
                        groupApplied = true;
                        factPatchCount += UpdateLinkedExecutionFact(facts, delta.Sequence, oldValue, newValue);
                    }

                    if (groupApplied)
                    {
                        MarkOwnerDirty(target);
                        estimatedRandomLookupCount++;
                    }

                    rangeStart = rangeEnd;
                }

                var stream = StreamLookup[StreamEntity];
                WritePendingAttributeDeltaStats(
                    ref stream,
                    pendingCount,
                    appliedCount,
                    skippedCount,
                    targetGroupCount,
                    maxTargetRange,
                    estimatedRandomLookupCount,
                    factPatchCount,
                    migrationCarrierCount: 1);
                StreamLookup[StreamEntity] = stream;
            }

            private bool IsDestroying(Entity asc)
            {
                return DestroyingLookup.HasComponent(asc)
                       && DestroyingLookup.IsComponentEnabled(asc);
            }

            private void MarkOwnerDirty(Entity asc)
            {
                if (DirtyLookup.HasComponent(asc))
                    DirtyLookup.SetComponentEnabled(asc, true);
            }
        }

        private struct PendingDeltaApplyRecordComparer : IComparer<PendingDeltaApplyRecord>
        {
            public int Compare(PendingDeltaApplyRecord x, PendingDeltaApplyRecord y)
            {
                var result = CompareEntity(x.TargetAsc, y.TargetAsc);
                if (result != 0)
                    return result;

                result = x.Sequence.CompareTo(y.Sequence);
                if (result != 0)
                    return result;

                return x.DeltaIndex.CompareTo(y.DeltaIndex);
            }
        }

        private static int CompareEntity(Entity left, Entity right)
        {
            var result = left.Index.CompareTo(right.Index);
            if (result != 0)
                return result;

            return left.Version.CompareTo(right.Version);
        }

        private static bool ApplyDelta(
            DynamicBuffer<AttributeValueBuffer> attributes,
            int attrSetCode,
            int attrCode,
            EModifierOp op,
            float magnitude,
            out float oldValue,
            out float newValue)
        {
            oldValue = 0f;
            newValue = 0f;

            var attrIndex = attributes.IndexOfAttribute(attrSetCode, attrCode);
            if (attrIndex < 0)
                return false;

            var attribute = attributes[attrIndex];
            oldValue = attribute.BaseValue;
            var oldCurrentValue = attribute.CurrentValue;
            newValue = AttributeHelper.ApplyModifier(attribute.BaseValue, op, magnitude);
            attribute.CurrentValue = newValue;
            AttributeHelper.Clamp(ref attribute);
            newValue = attribute.CurrentValue;
            attribute.BaseValue = newValue;
            attribute.CurrentValue = newValue;

            if (newValue == oldValue)
            {
                attributes[attrIndex] = attribute;
                return false;
            }

            attribute.Dirty = true;
            if (oldCurrentValue != attribute.CurrentValue)
            {
                attribute.PreviousCurrentValue = oldCurrentValue;
                attribute.CurrentValueChangePending = true;
            }

            attributes[attrIndex] = attribute;
            return true;
        }

        private static int UpdateLinkedExecutionFact(
            DynamicBuffer<GameplayEventBuffer> facts,
            int deltaSequence,
            float oldValue,
            float newValue)
        {
            var patchCount = 0;
            for (var i = 0; i < facts.Length; i++)
            {
                var fact = facts[i];
                if (fact.SourceDeltaSequence != deltaSequence
                    || fact.EventType != EGameplayEventType.ExecutionCalculationOutputUpdated)
                {
                    continue;
                }

                fact.Value = oldValue - newValue;
                fact.OldValue = oldValue;
                fact.NewValue = newValue;
                facts[i] = fact;
                patchCount++;
            }

            return patchCount;
        }

        private static void AppendAttributeChangeFact(
            DynamicBuffer<GameplayEventBuffer> facts,
            ref GEEffectCommandStreamComponent stream,
            in AttributeModifierBuffer delta,
            float oldValue,
            float newValue)
        {
            facts.Add(new GameplayEventBuffer
            {
                Sequence = Allocate(ref stream.NextFactSequence),
                SourceCommandSequence = delta.SourceCommandSequence,
                SourceSpecSequence = delta.SourceSpecSequence,
                SourceDeltaSequence = delta.Sequence,
                Frame = delta.Frame,
                EventType = EGameplayEventType.AttributeBaseValueChanged,
                Domain = EGameplayFactDomain.Attribute,
                Category = EGameplayFactCategory.StateChange,
                Severity = EGameplayFactSeverity.Info,
                SourceAsc = delta.SourceAsc,
                TargetAsc = delta.TargetAsc,
                SourceAbility = delta.SourceAbility,
                SourceEffect = delta.SourceEffect,
                GameplayEffectCode = delta.GameplayEffectCode,
                ContextId = delta.ContextId,
                ParentContextId = delta.ParentContextId,
                AttrSetCode = delta.AttrSetCode,
                AttributeCode = delta.AttributeCode,
                Value = delta.Magnitude,
                OldValue = oldValue,
                NewValue = newValue,
            });
        }

        private static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;

            return next++;
        }

        private static void WritePendingAttributeDeltaStats(
            ref GEEffectCommandStreamComponent stream,
            int pendingCount,
            int appliedCount,
            int skippedCount,
            int targetGroupCount,
            int maxTargetRange,
            int estimatedRandomLookupCount,
            int factPatchCount,
            int migrationCarrierCount)
        {
            stream.PendingAttributeDeltaCount += pendingCount;
            stream.PendingAttributeAppliedDeltaCount += appliedCount;
            stream.PendingAttributeSkippedDeltaCount += skippedCount;
            stream.PendingAttributeTargetGroupCount += targetGroupCount;
            if (maxTargetRange > stream.PendingAttributeMaxTargetRange)
                stream.PendingAttributeMaxTargetRange = maxTargetRange;
            stream.PendingAttributeEstimatedRandomLookupCount += estimatedRandomLookupCount;
            stream.PendingAttributeFactPatchCount += factPatchCount;
            stream.PendingAttributeMigrationCarrierCount += migrationCarrierCount;
        }
    }
}
