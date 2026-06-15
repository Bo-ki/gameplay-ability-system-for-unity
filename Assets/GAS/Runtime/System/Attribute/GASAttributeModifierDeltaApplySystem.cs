using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

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
                    ComponentType.ReadWrite<OwnerLocalGameplayFactBuffer>(),
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
            state.Dependency = new ApplyOwnerLocalPendingAttributeModifierDeltaChunkJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                PendingOwnerTypeHandle =
                    SystemAPI.GetComponentTypeHandle<PendingAttributeModifierComponent>(isReadOnly: false),
                DestroyingTypeHandle = SystemAPI.GetComponentTypeHandle<ASCDestroyingComponent>(isReadOnly: true),
                DirtyTypeHandle = SystemAPI.GetComponentTypeHandle<AttributeDirtyComponent>(isReadOnly: false),
                DeltaBufferTypeHandle = SystemAPI.GetBufferTypeHandle<AttributeModifierBuffer>(isReadOnly: false),
                AttributeBufferTypeHandle = SystemAPI.GetBufferTypeHandle<AttributeValueBuffer>(isReadOnly: false),
                OwnerFactBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<OwnerLocalGameplayFactBuffer>(isReadOnly: false),
                StreamEntity = streamEntity,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                OwnerLocalGameplayFactDirtyOwnerLookup =
                    SystemAPI.GetBufferLookup<OwnerLocalGameplayFactDirtyOwnerBuffer>(isReadOnly: false),
            }.Schedule(_ownerDeltaQuery, state.Dependency);
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
            public BufferTypeHandle<OwnerLocalGameplayFactBuffer> OwnerFactBufferTypeHandle;
            public Entity StreamEntity;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<OwnerLocalGameplayFactDirtyOwnerBuffer> OwnerLocalGameplayFactDirtyOwnerLookup;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity))
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
                var ownerFactBuffers = chunk.GetBufferAccessor(ref OwnerFactBufferTypeHandle);
                var stream = StreamLookup[StreamEntity];
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
                    var ownerFacts = ownerFactBuffers[entityIndex];
                    var groupApplied = false;
                    var groupFactDirty = false;
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
                        var patchedFacts = UpdateLinkedExecutionFact(ownerFacts, delta.Sequence, oldValue, newValue);
                        factPatchCount += patchedFacts;
                        if (patchedFacts > 0)
                            groupFactDirty = true;
                        AppendAttributeChangeFact(ownerFacts, ref stream, in delta, oldValue, newValue);
                        groupFactDirty = true;
                    }

                    if (groupApplied)
                        dirtyMask[entityIndex] = true;
                    if (groupFactDirty)
                    {
                        EffectCommandSpecStream.MarkOwnerLocalGameplayFactDirty(
                            OwnerLocalGameplayFactDirtyOwnerLookup,
                            StreamEntity,
                            owner);
                    }

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
                    factPatchCount);
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
            DynamicBuffer<OwnerLocalGameplayFactBuffer> facts,
            int deltaSequence,
            float oldValue,
            float newValue)
        {
            var patchCount = 0;
            for (var i = 0; i < facts.Length; i++)
            {
                var ownerFact = facts[i];
                var fact = ownerFact.Fact;
                if (fact.SourceDeltaSequence != deltaSequence
                    || fact.EventType != EGameplayEventType.ExecutionCalculationOutputUpdated)
                {
                    continue;
                }

                fact.Value = oldValue - newValue;
                fact.OldValue = oldValue;
                fact.NewValue = newValue;
                ownerFact.Fact = fact;
                facts[i] = ownerFact;
                patchCount++;
            }

            return patchCount;
        }

        private static void AppendAttributeChangeFact(
            DynamicBuffer<OwnerLocalGameplayFactBuffer> facts,
            ref GEEffectCommandStreamComponent stream,
            in AttributeModifierBuffer delta,
            float oldValue,
            float newValue)
        {
            facts.Add(new OwnerLocalGameplayFactBuffer
            {
                Fact = new GameplayEventBuffer
                {
                    Sequence = GASRuntimeSequenceAllocator.AllocateFactSequence(ref stream),
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
                },
            });
        }

        private static void WritePendingAttributeDeltaStats(
            ref GEEffectCommandStreamComponent stream,
            int pendingCount,
            int appliedCount,
            int skippedCount,
            int targetGroupCount,
            int maxTargetRange,
            int estimatedRandomLookupCount,
            int factPatchCount)
        {
            stream.PendingAttributeDeltaCount += pendingCount;
            stream.PendingAttributeAppliedDeltaCount += appliedCount;
            stream.PendingAttributeSkippedDeltaCount += skippedCount;
            stream.PendingAttributeTargetGroupCount += targetGroupCount;
            if (maxTargetRange > stream.PendingAttributeMaxTargetRange)
                stream.PendingAttributeMaxTargetRange = maxTargetRange;
            stream.PendingAttributeEstimatedRandomLookupCount += estimatedRandomLookupCount;
            stream.PendingAttributeFactPatchCount += factPatchCount;
        }
    }
}
