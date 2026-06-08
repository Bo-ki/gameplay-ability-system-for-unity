using Unity.Burst;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Jobs;
using static GAS.Runtime.EffectCommandSpecStreamPhaseUtility;

namespace GAS.Runtime
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASFramePrepareSystemGroup))]
    [UpdateAfter(typeof(GASGlobalTimerSystem))]
    [BurstCompile]
    public partial struct GEEffectCommandSpecStreamFramePrepareSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GlobalTimer>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            var frame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            state.Dependency = new PrepareFrameLocalDataJob
            {
                StreamEntity = streamEntity,
                Frame = frame,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private struct PrepareFrameLocalDataJob : IJob
        {
            public Entity StreamEntity;
            public int Frame;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity))
                {
                    return;
                }

                var stream = StreamLookup[StreamEntity];
                EffectCommandSpecStream.PrepareFrameLocalData(
                    ref stream,
                    Frame);
                StreamLookup[StreamEntity] = stream;
            }
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASFramePrepareSystemGroup))]
    [UpdateAfter(typeof(GEEffectCommandSpecStreamFramePrepareSystem))]
    [BurstCompile]
    public partial struct OwnerLocalInstantCommandFramePrepareSystem : ISystem
    {
        private EntityQuery _ownerInstantCommandQuery;

        public void OnCreate(ref SystemState state)
        {
            _ownerInstantCommandQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<GEEffectCommandBuffer>(),
                    ComponentType.ReadWrite<GESetByCallerValueBuffer>(),
                    ComponentType.ReadWrite<GEEffectSpecBuffer>(),
                    ComponentType.ReadWrite<OwnerLocalInstantNextFrameCommandBuffer>(),
                    ComponentType.ReadWrite<OwnerLocalInstantNextFrameSetByCallerValueBuffer>(),
                    ComponentType.ReadOnly<ASCIdentityComponent>(),
                },
            });
            state.RequireForUpdate(_ownerInstantCommandQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new PromoteOwnerLocalInstantCommandsJob
            {
                CommandType = SystemAPI.GetBufferTypeHandle<GEEffectCommandBuffer>(),
                SetByCallerType = SystemAPI.GetBufferTypeHandle<GESetByCallerValueBuffer>(),
                SpecType = SystemAPI.GetBufferTypeHandle<GEEffectSpecBuffer>(),
                NextFrameCommandType = SystemAPI.GetBufferTypeHandle<OwnerLocalInstantNextFrameCommandBuffer>(),
                NextFrameSetByCallerType = SystemAPI.GetBufferTypeHandle<OwnerLocalInstantNextFrameSetByCallerValueBuffer>(),
            }.Schedule(_ownerInstantCommandQuery, state.Dependency);
        }

        [BurstCompile]
        private struct PromoteOwnerLocalInstantCommandsJob : IJobChunk
        {
            public BufferTypeHandle<GEEffectCommandBuffer> CommandType;
            public BufferTypeHandle<GESetByCallerValueBuffer> SetByCallerType;
            public BufferTypeHandle<GEEffectSpecBuffer> SpecType;
            public BufferTypeHandle<OwnerLocalInstantNextFrameCommandBuffer> NextFrameCommandType;
            public BufferTypeHandle<OwnerLocalInstantNextFrameSetByCallerValueBuffer> NextFrameSetByCallerType;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var commands = chunk.GetBufferAccessor(ref CommandType);
                var setByCallerValues = chunk.GetBufferAccessor(ref SetByCallerType);
                var specs = chunk.GetBufferAccessor(ref SpecType);
                var nextFrameCommands = chunk.GetBufferAccessor(ref NextFrameCommandType);
                var nextFrameSetByCallerValues = chunk.GetBufferAccessor(ref NextFrameSetByCallerType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var currentCommands = commands[entityIndex];
                    var currentSetByCallerValues = setByCallerValues[entityIndex];
                    var currentSpecs = specs[entityIndex];
                    var deferredCommands = nextFrameCommands[entityIndex];
                    var deferredSetByCallerValues = nextFrameSetByCallerValues[entityIndex];

                    currentCommands.Clear();
                    currentSetByCallerValues.Clear();
                    currentSpecs.Clear();

                    for (var i = 0; i < deferredSetByCallerValues.Length; i++)
                        currentSetByCallerValues.Add(deferredSetByCallerValues[i].Value);

                    for (var i = 0; i < deferredCommands.Length; i++)
                        currentCommands.Add(deferredCommands[i].Command);

                    deferredCommands.Clear();
                    deferredSetByCallerValues.Clear();
                }
            }
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASFramePrepareSystemGroup))]
    [UpdateAfter(typeof(OwnerLocalInstantCommandFramePrepareSystem))]
    [BurstCompile]
    public partial struct ActiveEffectOwnerLocalMutationFramePrepareSystem : ISystem
    {
        private EntityQuery _ownerMutationQuery;

        public void OnCreate(ref SystemState state)
        {
            _ownerMutationQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<ActiveEffectMutationCommandBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectMutationSetByCallerValueBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectNextFrameMutationCommandBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectNextFrameMutationSetByCallerValueBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectMutationBuffer>(),
                    ComponentType.ReadOnly<ASCIdentityComponent>(),
                },
            });
            state.RequireForUpdate(_ownerMutationQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ClearOwnerLocalActiveEffectMutationsJob
            {
                CommandType = SystemAPI.GetBufferTypeHandle<ActiveEffectMutationCommandBuffer>(),
                SetByCallerType = SystemAPI.GetBufferTypeHandle<ActiveEffectMutationSetByCallerValueBuffer>(),
                NextFrameCommandType = SystemAPI.GetBufferTypeHandle<ActiveEffectNextFrameMutationCommandBuffer>(),
                NextFrameSetByCallerType = SystemAPI.GetBufferTypeHandle<ActiveEffectNextFrameMutationSetByCallerValueBuffer>(),
                MutationType = SystemAPI.GetBufferTypeHandle<ActiveEffectMutationBuffer>(),
            }.Schedule(_ownerMutationQuery, state.Dependency);
        }

        [BurstCompile]
        private struct ClearOwnerLocalActiveEffectMutationsJob : IJobChunk
        {
            public BufferTypeHandle<ActiveEffectMutationCommandBuffer> CommandType;
            public BufferTypeHandle<ActiveEffectMutationSetByCallerValueBuffer> SetByCallerType;
            public BufferTypeHandle<ActiveEffectNextFrameMutationCommandBuffer> NextFrameCommandType;
            public BufferTypeHandle<ActiveEffectNextFrameMutationSetByCallerValueBuffer> NextFrameSetByCallerType;
            public BufferTypeHandle<ActiveEffectMutationBuffer> MutationType;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var commands = chunk.GetBufferAccessor(ref CommandType);
                var setByCallerValues = chunk.GetBufferAccessor(ref SetByCallerType);
                var nextFrameCommands = chunk.GetBufferAccessor(ref NextFrameCommandType);
                var nextFrameSetByCallerValues = chunk.GetBufferAccessor(ref NextFrameSetByCallerType);
                var mutations = chunk.GetBufferAccessor(ref MutationType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var currentCommands = commands[entityIndex];
                    var currentSetByCallerValues = setByCallerValues[entityIndex];
                    var deferredCommands = nextFrameCommands[entityIndex];
                    var deferredSetByCallerValues = nextFrameSetByCallerValues[entityIndex];

                    currentCommands.Clear();
                    currentSetByCallerValues.Clear();
                    mutations[entityIndex].Clear();

                    for (var i = 0; i < deferredSetByCallerValues.Length; i++)
                    {
                        currentSetByCallerValues.Add(new ActiveEffectMutationSetByCallerValueBuffer
                        {
                            Value = deferredSetByCallerValues[i].Value,
                        });
                    }

                    for (var i = 0; i < deferredCommands.Length; i++)
                    {
                        currentCommands.Add(new ActiveEffectMutationCommandBuffer
                        {
                            Command = deferredCommands[i].Command,
                        });
                    }

                    deferredCommands.Clear();
                    deferredSetByCallerValues.Clear();
                }
            }
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASFramePrepareSystemGroup))]
    [UpdateAfter(typeof(ActiveEffectOwnerLocalMutationFramePrepareSystem))]
    [BurstCompile]
    public partial struct GameplayOwnerLocalFactFramePrepareSystem : ISystem
    {
        private EntityQuery _ownerFactQuery;

        public void OnCreate(ref SystemState state)
        {
            _ownerFactQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<OwnerLocalGameplayFactBuffer>(),
                    ComponentType.ReadOnly<ASCIdentityComponent>(),
                },
            });
            state.RequireForUpdate(_ownerFactQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ClearOwnerLocalGameplayFactsJob
            {
                OwnerFactType = SystemAPI.GetBufferTypeHandle<OwnerLocalGameplayFactBuffer>(),
            }.Schedule(_ownerFactQuery, state.Dependency);
        }

        [BurstCompile]
        private struct ClearOwnerLocalGameplayFactsJob : IJobChunk
        {
            public BufferTypeHandle<OwnerLocalGameplayFactBuffer> OwnerFactType;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var ownerFacts = chunk.GetBufferAccessor(ref OwnerFactType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                    ownerFacts[entityIndex].Clear();
            }
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEExecutionCalculationOutputModifierSystem))]
    [UpdateAfter(typeof(GASAttributeModifierDeltaApplySystem))]
    [BurstCompile]
    public partial struct GameplayFactProjectionSystem : ISystem
    {
        private EntityQuery _ownerSpecQuery;

        public void OnCreate(ref SystemState state)
        {
            _ownerSpecQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<ASCIdentityComponent>(),
                    ComponentType.ReadOnly<ASCDestroyingComponent>(),
                    ComponentType.ReadOnly<GEEffectSpecBuffer>(),
                    ComponentType.ReadWrite<OwnerLocalGameplayFactBuffer>(),
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState,
            });
            state.RequireForUpdate(_ownerSpecQuery);
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();

            state.Dependency = new GameplayFactProjectionJob
            {
                StreamEntity = streamEntity,
                EntityType = SystemAPI.GetEntityTypeHandle(),
                DestroyingType = SystemAPI.GetComponentTypeHandle<ASCDestroyingComponent>(isReadOnly: true),
                SpecType = SystemAPI.GetBufferTypeHandle<GEEffectSpecBuffer>(isReadOnly: true),
                OwnerFactType = SystemAPI.GetBufferTypeHandle<OwnerLocalGameplayFactBuffer>(),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
            }.Schedule(_ownerSpecQuery, state.Dependency);
        }

        [BurstCompile]
        private struct GameplayFactProjectionJob : IJobChunk
        {
            public Entity StreamEntity;
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public ComponentTypeHandle<ASCDestroyingComponent> DestroyingType;
            [ReadOnly] public BufferTypeHandle<GEEffectSpecBuffer> SpecType;
            public BufferTypeHandle<OwnerLocalGameplayFactBuffer> OwnerFactType;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;

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

                var stream = StreamLookup[StreamEntity];
                var owners = chunk.GetNativeArray(EntityType);
                var destroyingMask = chunk.GetEnabledMask(ref DestroyingType);
                var specBuffers = chunk.GetBufferAccessor(ref SpecType);
                var ownerFactBuffers = chunk.GetBufferAccessor(ref OwnerFactType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    if (destroyingMask[entityIndex])
                        continue;

                    var owner = owners[entityIndex];
                    var specs = specBuffers[entityIndex];
                    var facts = ownerFactBuffers[entityIndex];
                    for (var i = 0; i < specs.Length; i++)
                        ProjectCueRequest(specs[i], owner, facts, ref stream);
                }

                StreamLookup[StreamEntity] = stream;
            }
        }

        private static void ProjectCueRequest(
            in GEEffectSpecBuffer spec,
            Entity owner,
            DynamicBuffer<OwnerLocalGameplayFactBuffer> facts,
            ref GEEffectCommandStreamComponent stream)
        {
            if (spec.CueRequestOnApplyCode <= 0
                || spec.TargetAsc == Entity.Null
                || CompareEntity(spec.TargetAsc, owner) != 0)
            {
                return;
            }

            var factSequence = Allocate(ref stream.NextFactSequence);
            facts.Add(new OwnerLocalGameplayFactBuffer
            {
                Fact = new GameplayEventBuffer
                {
                    Sequence = factSequence,
                    SourceCommandSequence = spec.SourceCommandSequence,
                    SourceSpecSequence = spec.Sequence,
                    Frame = spec.Frame,
                    EventType = EGameplayEventType.CueRequested,
                    Domain = EGameplayFactDomain.Cue,
                    Category = EGameplayFactCategory.Request,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = spec.SourceAsc,
                    TargetAsc = spec.TargetAsc,
                    SourceAbility = spec.SourceAbility,
                    SourceEffect = spec.SourceEffect,
                    GameplayEffectCode = spec.GameplayEffectCode,
                    ContextId = spec.ContextId,
                    ParentContextId = spec.ParentContextId,
                    EventCode = (int)EGameplayCueEvent.OnApply,
                    ReasonCode = spec.CueRequestOnApplyCode,
                },
            });
        }

        private static int CompareEntity(Entity left, Entity right)
        {
            var result = left.Index.CompareTo(right.Index);
            return result != 0 ? result : left.Version.CompareTo(right.Version);
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(GameplayFactBoundaryProjectionSystem))]
    [BurstCompile]
    public partial struct GameplayBoundaryFactExportSystem : ISystem
    {
        private EntityQuery _ownerFactQuery;

        public void OnCreate(ref SystemState state)
        {
            _ownerFactQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<ASCIdentityComponent>(),
                    ComponentType.ReadOnly<OwnerLocalGameplayFactBuffer>(),
                },
            });
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GameplayEventBusComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<GEEffectCommandStreamComponent>(out var streamEntity)
                || !EffectCommandSpecStream.HasRequiredBuffers(state.EntityManager, streamEntity)
                || !SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var eventBusEntity))
            {
                return;
            }

            var records = new NativeList<BoundaryObservationFactRecord>(1, Allocator.TempJob);
            var collectHandle = new CollectOwnerLocalGameplayFactsJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                OwnerFactType = SystemAPI.GetBufferTypeHandle<OwnerLocalGameplayFactBuffer>(isReadOnly: true),
                Records = records,
            }.Schedule(_ownerFactQuery, state.Dependency);
            var exportHandle = new ExportBoundaryObservationFactsJob
            {
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                BoundaryObservationLookup = SystemAPI.GetBufferLookup<BoundaryObservationFactBuffer>(isReadOnly: false),
                Records = records,
            }.Schedule(collectHandle);

            state.Dependency = records.Dispose(exportHandle);
        }

        private struct BoundaryObservationFactRecord
        {
            public Entity Owner;
            public int LocalIndex;
            public GameplayEventBuffer Fact;
            public EBoundaryObservationFactSource Source;
        }

        [BurstCompile]
        private struct CollectOwnerLocalGameplayFactsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public BufferTypeHandle<OwnerLocalGameplayFactBuffer> OwnerFactType;
            public NativeList<BoundaryObservationFactRecord> Records;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var ownerFacts = chunk.GetBufferAccessor(ref OwnerFactType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var owner = entities[entityIndex];
                    var facts = ownerFacts[entityIndex];
                    for (var factIndex = 0; factIndex < facts.Length; factIndex++)
                    {
                        Records.Add(new BoundaryObservationFactRecord
                        {
                            Owner = owner,
                            LocalIndex = factIndex,
                            Fact = facts[factIndex].Fact,
                            Source = EBoundaryObservationFactSource.OwnerLocalCore,
                        });
                    }
                }
            }
        }

        [BurstCompile]
        private struct ExportBoundaryObservationFactsJob : IJob
        {
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<BoundaryObservationFactBuffer> BoundaryObservationLookup;
            public NativeList<BoundaryObservationFactRecord> Records;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || EventBusEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !BoundaryObservationLookup.HasBuffer(EventBusEntity))
                {
                    return;
                }

                var stream = StreamLookup[StreamEntity];
                if (Records.Length == 0)
                {
                    StreamLookup[StreamEntity] = stream;
                    return;
                }

                Records.Sort(new BoundaryObservationFactRecordComparer());
                var boundaryFacts = BoundaryObservationLookup[EventBusEntity];
                var ownerGroupCount = 0;
                var maxOwnerRange = 0;
                var ownerLocalFlushCount = 0;
                var groupStart = 0;
                while (groupStart < Records.Length)
                {
                    var owner = Records[groupStart].Owner;
                    var groupEnd = groupStart + 1;
                    while (groupEnd < Records.Length
                           && CompareEntity(Records[groupEnd].Owner, owner) == 0)
                    {
                        groupEnd++;
                    }

                    var ownerLocalRange = 0;

                    for (var i = groupStart; i < groupEnd; i++)
                    {
                        boundaryFacts.Add(new BoundaryObservationFactBuffer
                        {
                            Fact = Records[i].Fact,
                            Owner = Records[i].Owner,
                            LocalIndex = Records[i].LocalIndex,
                            Source = Records[i].Source,
                        });
                        if (Records[i].Source == EBoundaryObservationFactSource.OwnerLocalCore)
                            ownerLocalRange++;
                    }

                    if (ownerLocalRange > 0)
                    {
                        ownerGroupCount++;
                        ownerLocalFlushCount += ownerLocalRange;
                        if (ownerLocalRange > maxOwnerRange)
                            maxOwnerRange = ownerLocalRange;
                    }

                    groupStart = groupEnd;
                }

                stream.OwnerLocalFactCount += ownerLocalFlushCount;
                stream.OwnerLocalFactOwnerGroupCount += ownerGroupCount;
                if (maxOwnerRange > stream.OwnerLocalFactMaxOwnerRange)
                    stream.OwnerLocalFactMaxOwnerRange = maxOwnerRange;
                stream.OwnerLocalFactFlushCount += ownerLocalFlushCount;
                StreamLookup[StreamEntity] = stream;
            }
        }

        private struct BoundaryObservationFactRecordComparer : IComparer<BoundaryObservationFactRecord>
        {
            public int Compare(BoundaryObservationFactRecord x, BoundaryObservationFactRecord y)
            {
                var result = CompareEntity(x.Owner, y.Owner);
                if (result != 0)
                    return result;

                result = x.Fact.Frame.CompareTo(y.Fact.Frame);
                if (result != 0)
                    return result;

                result = x.Fact.Sequence.CompareTo(y.Fact.Sequence);
                if (result != 0)
                    return result;

                result = x.LocalIndex.CompareTo(y.LocalIndex);
                if (result != 0)
                    return result;

                return ((int)x.Source).CompareTo((int)y.Source);
            }
        }

        private static int CompareEntity(Entity left, Entity right)
        {
            var result = left.Index.CompareTo(right.Index);
            return result != 0 ? result : left.Version.CompareTo(right.Version);
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PresentationOutboxProjectionSystem))]
    [UpdateBefore(typeof(ReplayLogSystem))]
    [BurstCompile]
    public partial struct GameplayFactBoundaryProjectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayEventBusComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var eventBusEntity))
            {
                return;
            }

            state.Dependency = new GameplayFactBoundaryProjectionJob
            {
                EventBusEntity = eventBusEntity,
                BoundaryObservationLookup = SystemAPI.GetBufferLookup<BoundaryObservationFactBuffer>(isReadOnly: true),
                AttributeEventLookup = SystemAPI.GetBufferLookup<AttributeChangeEventBuffer>(isReadOnly: false),
                CueRequestLookup = SystemAPI.GetBufferLookup<CueRequestBuffer>(isReadOnly: false),
                TagChangeEventLookup = SystemAPI.GetBufferLookup<TagChangeEventBuffer>(isReadOnly: false),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private struct GameplayFactBoundaryProjectionJob : IJob
        {
            public Entity EventBusEntity;
            [ReadOnly] public BufferLookup<BoundaryObservationFactBuffer> BoundaryObservationLookup;
            public BufferLookup<AttributeChangeEventBuffer> AttributeEventLookup;
            public BufferLookup<CueRequestBuffer> CueRequestLookup;
            public BufferLookup<TagChangeEventBuffer> TagChangeEventLookup;

            public void Execute()
            {
                if (EventBusEntity == Entity.Null
                    || !BoundaryObservationLookup.HasBuffer(EventBusEntity))
                {
                    return;
                }

                var observations = BoundaryObservationLookup[EventBusEntity];

                var hasAttributeEvents = AttributeEventLookup.HasBuffer(EventBusEntity);
                var hasCueRequests = CueRequestLookup.HasBuffer(EventBusEntity);
                var hasTagChanges = TagChangeEventLookup.HasBuffer(EventBusEntity);
                if (!hasAttributeEvents && !hasCueRequests && !hasTagChanges)
                {
                    return;
                }

                var attributeEvents = hasAttributeEvents
                    ? AttributeEventLookup[EventBusEntity]
                    : default;
                var cueRequests = hasCueRequests
                    ? CueRequestLookup[EventBusEntity]
                    : default;
                var tagChanges = hasTagChanges
                    ? TagChangeEventLookup[EventBusEntity]
                    : default;

                for (var i = 0; i < observations.Length; i++)
                {
                    var fact = observations[i].Fact;
                    if (hasAttributeEvents
                        && TryCreateAttributeChangeEvent(in fact, out var attributeEvent))
                    {
                        attributeEvents.Add(attributeEvent);
                        continue;
                    }

                    if (hasCueRequests
                        && TryCreateCueRequest(in fact, out var cueRequest))
                    {
                        cueRequests.Add(cueRequest);
                        continue;
                    }

                    if (hasTagChanges
                        && TryCreateTagChange(in fact, out var tagChange))
                    {
                        tagChanges.Add(tagChange);
                    }
                }
            }
        }

        private static bool TryCreateAttributeChangeEvent(
            in GameplayEventBuffer fact,
            out AttributeChangeEventBuffer evt)
        {
            evt = default;

            if (fact.Domain != EGameplayFactDomain.Attribute
                || fact.EventType != EGameplayEventType.AttributeBaseValueChanged
                || fact.TargetAsc == Entity.Null)
            {
                return false;
            }

            evt = new AttributeChangeEventBuffer
            {
                ASC = fact.TargetAsc,
                SourceAsc = fact.SourceAsc,
                SourceAbility = fact.SourceAbility,
                GameplayEffect = fact.SourceEffect,
                SourceFactSequence = fact.Sequence,
                EventCode = fact.GameplayEffectCode,
                AttrSetCode = fact.AttrSetCode,
                AttributeCode = fact.AttributeCode,
                OldValue = fact.OldValue,
                NewValue = fact.NewValue,
                ContextId = fact.ContextId,
                IsBaseValue = true,
            };
            return true;
        }

        private static bool TryCreateCueRequest(
            in GameplayEventBuffer fact,
            out CueRequestBuffer request)
        {
            request = default;

            if (fact.Domain != EGameplayFactDomain.Cue
                || fact.EventType != EGameplayEventType.CueRequested
                || fact.TargetAsc == Entity.Null)
            {
                return false;
            }

            request = new CueRequestBuffer
            {
                TargetAsc = fact.TargetAsc,
                SourceAsc = fact.SourceAsc,
                SourceAbility = fact.SourceAbility,
                GameplayEffect = fact.SourceEffect,
                SourceEntity = fact.SourceAbility,
                SourceType = CueSourceType.GameplayEffect,
                CueEntity = Entity.Null,
                SourceFactSequence = fact.Sequence,
                ContextId = fact.ContextId,
                ReasonCode = fact.ReasonCode,
                CueEvent = (EGameplayCueEvent)fact.EventCode,
            };
            return true;
        }

        private static bool TryCreateTagChange(
            in GameplayEventBuffer fact,
            out TagChangeEventBuffer evt)
        {
            evt = default;

            if (fact.Domain != EGameplayFactDomain.Tag
                || fact.EventType != EGameplayEventType.TagChanged
                || fact.TargetAsc == Entity.Null)
            {
                return false;
            }

            evt = new TagChangeEventBuffer
            {
                SourceFactSequence = fact.Sequence,
                ASC = fact.TargetAsc,
                TagIndex = fact.EventCode,
                Added = fact.ReasonCode != 0,
            };
            return true;
        }
    }


    internal static class EffectCommandSpecStreamPhaseUtility
    {
        public static int ClampCursor(int cursor, int length)
        {
            if (cursor < 0)
                return 0;
            return cursor > length ? length : cursor;
        }

        public static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;

            return next++;
        }
    }
}
