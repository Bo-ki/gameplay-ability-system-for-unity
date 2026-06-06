using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace GAS.Runtime
{
    /// <summary>
    /// Burst 兼容的属性重算 System。遍历 AttributeActiveModifierBuffer Buffer 计算 CurrentValue。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [BurstCompile]
    public partial struct AttributeRecalculateSystem : ISystem
    {
        private EntityQuery _dirtyQuery;
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _dirtyQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<TagMaskComponent>(),
                    ComponentType.ReadWrite<AttributeValueBuffer>(),
                    ComponentType.ReadOnly<AttributeDirtyComponent>(),
                },
            });

            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<TagMaskComponent>(),
                    ComponentType.ReadWrite<AttributeValueBuffer>(),
                    ComponentType.ReadWrite<AttributeDirtyComponent>(),
                    ComponentType.ReadWrite<AttributeChangeEventPendingComponent>(),
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState,
            });
            state.RequireForUpdate(_dirtyQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var modifierLookup = SystemAPI.GetBufferLookup<AttributeActiveModifierBuffer>(true);
            state.Dependency = new AttributeRecalculateJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                AttributeTypeHandle = SystemAPI.GetBufferTypeHandle<AttributeValueBuffer>(),
                DirtyTypeHandle = SystemAPI.GetComponentTypeHandle<AttributeDirtyComponent>(),
                ChangeEventPendingTypeHandle =
                    SystemAPI.GetComponentTypeHandle<AttributeChangeEventPendingComponent>(),
                ModifierBufferLookup = modifierLookup,
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private struct AttributeRecalculateJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public BufferTypeHandle<AttributeValueBuffer> AttributeTypeHandle;
            public ComponentTypeHandle<AttributeDirtyComponent> DirtyTypeHandle;
            public ComponentTypeHandle<AttributeChangeEventPendingComponent> ChangeEventPendingTypeHandle;
            [ReadOnly] public BufferLookup<AttributeActiveModifierBuffer> ModifierBufferLookup;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var attributes = chunk.GetBufferAccessor(ref AttributeTypeHandle);
                var dirtyMask = chunk.GetEnabledMask(ref DirtyTypeHandle);
                var changeEventPendingMask = chunk.GetEnabledMask(ref ChangeEventPendingTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    if (!dirtyMask.GetBit(entityIndex))
                        continue;

                    if (RecalculateAttributes(
                            entities[entityIndex],
                            attributes[entityIndex],
                            ModifierBufferLookup))
                    {
                        changeEventPendingMask[entityIndex] = true;
                    }

                    dirtyMask[entityIndex] = false;
                }
            }
        }

        private static bool RecalculateAttributes(
            Entity entity,
            DynamicBuffer<AttributeValueBuffer> attributes,
            BufferLookup<AttributeActiveModifierBuffer> modifierLookup)
        {
            var hasModifiers = modifierLookup.HasBuffer(entity);
            var modifiers = hasModifiers ? modifierLookup[entity] : default;
            var hasPendingChange = false;

            for (var i = 0; i < attributes.Length; i++)
            {
                var attr = attributes[i];
                if (!attr.Dirty)
                {
                    if (attr.CurrentValueChangePending)
                        hasPendingChange = true;
                    continue;
                }

                var eventPreviousValue = attr.CurrentValueChangePending
                    ? attr.PreviousCurrentValue
                    : attr.CurrentValue;
                attr.CurrentValue = attr.BaseValue;

                if (hasModifiers)
                {
                    for (var j = 0; j < modifiers.Length; j++)
                    {
                        var mod = modifiers[j];
                        if (mod.AttrSetCode != attr.AttrSetCode || mod.AttributeCode != attr.Code)
                            continue;

                        attr.CurrentValue = ApplyModifier(attr.CurrentValue, mod.Op, mod.Magnitude);
                    }
                }

                if (attr.IsClampMin)
                    attr.CurrentValue = math.max(attr.CurrentValue, attr.MinValue);
                if (attr.IsClampMax)
                    attr.CurrentValue = math.min(attr.CurrentValue, attr.MaxValue);

                if (eventPreviousValue != attr.CurrentValue)
                {
                    attr.PreviousCurrentValue = eventPreviousValue;
                    attr.CurrentValueChangePending = true;
                    hasPendingChange = true;
                }
                else
                {
                    attr.PreviousCurrentValue = attr.CurrentValue;
                    attr.CurrentValueChangePending = false;
                }

                attr.Dirty = false;
                attributes[i] = attr;
            }

            return hasPendingChange;
        }

        private static float ApplyModifier(float currentValue, EModifierOp op, float magnitude)
        {
            return op switch
            {
                EModifierOp.Add => currentValue + magnitude,
                EModifierOp.Subtract => currentValue - magnitude,
                EModifierOp.Multiply => currentValue * magnitude,
                EModifierOp.Divide => currentValue / magnitude,
                EModifierOp.Override => magnitude,
                _ => currentValue,
            };
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateBefore(typeof(AttributeRecalculateSystem))]
    [BurstCompile]
    public partial struct AttributeOwnerMarkerRequestSystem : ISystem
    {
        private EntityQuery _ownerQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _ownerQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<AttributeDirtyComponent>(),
                    ComponentType.ReadWrite<AttributeActiveModifierPresentComponent>(),
                    ComponentType.ReadOnly<AttributeActiveModifierBuffer>(),
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState,
            });
            state.RequireForUpdate<GameplayEventBusComponent>();
            state.RequireForUpdate(_ownerQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var em = state.EntityManager;
            var bufferedRequestCount = eventBusEntity != Entity.Null
                                       && em.HasBuffer<AttributeOwnerMarkerRequestBuffer>(eventBusEntity)
                ? em.GetBuffer<AttributeOwnerMarkerRequestBuffer>(eventBusEntity).Length
                : 0;
            if (bufferedRequestCount <= 0)
                return;

            var requests = new NativeList<AttributeOwnerMarkerRequestRecord>(
                math.max(1, bufferedRequestCount),
                Allocator.TempJob);

            var collectDependency = new AttributeOwnerMarkerRequestCollectJob
            {
                RequestLookup = SystemAPI.GetBufferLookup<AttributeOwnerMarkerRequestBuffer>(isReadOnly: true),
                EventBusEntity = eventBusEntity,
                Requests = requests,
            }.Schedule(state.Dependency);

            state.Dependency = new AttributeOwnerMarkerRequestApplyJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                DirtyTypeHandle = SystemAPI.GetComponentTypeHandle<AttributeDirtyComponent>(),
                ActiveModifierPresentTypeHandle =
                    SystemAPI.GetComponentTypeHandle<AttributeActiveModifierPresentComponent>(),
                ActiveModifierTypeHandle =
                    SystemAPI.GetBufferTypeHandle<AttributeActiveModifierBuffer>(isReadOnly: true),
                Requests = requests,
            }.Schedule(_ownerQuery, collectDependency);
            state.Dependency = requests.Dispose(state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private struct AttributeOwnerMarkerRequestCollectJob : IJob
        {
            [ReadOnly] public BufferLookup<AttributeOwnerMarkerRequestBuffer> RequestLookup;
            public Entity EventBusEntity;
            public NativeList<AttributeOwnerMarkerRequestRecord> Requests;

            public void Execute()
            {
                if (EventBusEntity == Entity.Null || !RequestLookup.HasBuffer(EventBusEntity))
                    return;

                var source = RequestLookup[EventBusEntity];
                for (var i = 0; i < source.Length; i++)
                {
                    var request = source[i];
                    if (request.ASC == Entity.Null)
                        continue;

                    Requests.Add(new AttributeOwnerMarkerRequestRecord
                    {
                        Sequence = request.Sequence,
                        ASC = request.ASC,
                        RequestKind = request.RequestKind,
                    });
                }
            }
        }

        [BurstCompile]
        private struct AttributeOwnerMarkerRequestApplyJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<AttributeDirtyComponent> DirtyTypeHandle;
            public ComponentTypeHandle<AttributeActiveModifierPresentComponent> ActiveModifierPresentTypeHandle;
            [ReadOnly] public BufferTypeHandle<AttributeActiveModifierBuffer> ActiveModifierTypeHandle;
            [ReadOnly] public NativeList<AttributeOwnerMarkerRequestRecord> Requests;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var owners = chunk.GetNativeArray(EntityTypeHandle);
                var dirtyMask = chunk.GetEnabledMask(ref DirtyTypeHandle);
                var activeModifierPresentMask = chunk.GetEnabledMask(ref ActiveModifierPresentTypeHandle);
                var activeModifiers = chunk.GetBufferAccessor(ref ActiveModifierTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var owner = owners[entityIndex];
                    var shouldMarkDirty = false;
                    var shouldRefreshActiveModifierPresent = false;
                    for (var requestIndex = 0; requestIndex < Requests.Length; requestIndex++)
                    {
                        var request = Requests[requestIndex];
                        if (request.ASC != owner)
                            continue;

                        if (request.RequestKind == EAttributeOwnerMarkerRequestKind.MarkDirty)
                            shouldMarkDirty = true;
                        else if (request.RequestKind == EAttributeOwnerMarkerRequestKind.SetActiveModifierPresent)
                            shouldRefreshActiveModifierPresent = true;
                    }

                    if (shouldMarkDirty)
                        dirtyMask[entityIndex] = true;
                    if (shouldRefreshActiveModifierPresent)
                        activeModifierPresentMask[entityIndex] = activeModifiers[entityIndex].Length > 0;
                }
            }
        }

        private struct AttributeOwnerMarkerRequestRecord
        {
            public int Sequence;
            public Entity ASC;
            public EAttributeOwnerMarkerRequestKind RequestKind;
        }
    }
}
