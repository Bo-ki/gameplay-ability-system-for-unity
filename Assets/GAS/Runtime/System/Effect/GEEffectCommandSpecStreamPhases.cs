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
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: false),
                SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: false),
                SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(isReadOnly: false),
                FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: false),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private struct PrepareFrameLocalDataJob : IJob
        {
            public Entity StreamEntity;
            public int Frame;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GEEffectCommandBuffer> CommandLookup;
            public BufferLookup<GESetByCallerValueBuffer> SetByCallerLookup;
            public BufferLookup<GEEffectSpecBuffer> SpecLookup;
            public BufferLookup<GameplayEventBuffer> FactLookup;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !CommandLookup.HasBuffer(StreamEntity)
                    || !SetByCallerLookup.HasBuffer(StreamEntity)
                    || !SpecLookup.HasBuffer(StreamEntity)
                    || !FactLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                var stream = StreamLookup[StreamEntity];
                EffectCommandSpecStream.PrepareFrameLocalData(
                    ref stream,
                    CommandLookup[StreamEntity],
                    SetByCallerLookup[StreamEntity],
                    SpecLookup[StreamEntity],
                    FactLookup[StreamEntity],
                    Frame);
                StreamLookup[StreamEntity] = stream;
            }
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASFramePrepareSystemGroup))]
    [UpdateAfter(typeof(GEEffectCommandSpecStreamFramePrepareSystem))]
    [BurstCompile]
    public partial struct ActiveEffectOwnerLocalMutationFramePrepareSystem : ISystem
    {
        private EntityQuery _ownerMutationQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _ownerMutationQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
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
                MutationType = SystemAPI.GetBufferTypeHandle<ActiveEffectMutationBuffer>(),
            }.Schedule(_ownerMutationQuery, state.Dependency);
        }

        [BurstCompile]
        private struct ClearOwnerLocalActiveEffectMutationsJob : IJobChunk
        {
            public BufferTypeHandle<ActiveEffectMutationBuffer> MutationType;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var mutations = chunk.GetBufferAccessor(ref MutationType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                    mutations[entityIndex].Clear();
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

        [BurstCompile]
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

            state.Dependency = new GameplayFactProjectionJob
            {
                StreamEntity = streamEntity,
                Frame = frame,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(isReadOnly: true),
                FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: false),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private struct GameplayFactProjectionJob : IJob
        {
            public Entity StreamEntity;
            public int Frame;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GEEffectSpecBuffer> SpecLookup;
            public BufferLookup<GameplayEventBuffer> FactLookup;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !SpecLookup.HasBuffer(StreamEntity)
                    || !FactLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                var stream = StreamLookup[StreamEntity];
                var specs = SpecLookup[StreamEntity];
                var facts = FactLookup[StreamEntity];

                var cueStart = ClampCursor(stream.CueProjectionSpecCursor, specs.Length);
                for (var i = cueStart; i < specs.Length; i++)
                    ProjectCueRequest(specs[i], facts, ref stream);

                stream.CueProjectionSpecCursor = specs.Length;
                StreamLookup[StreamEntity] = stream;
            }
        }

        private static void ProjectCueRequest(
            in GEEffectSpecBuffer spec,
            DynamicBuffer<GameplayEventBuffer> facts,
            ref GEEffectCommandStreamComponent stream)
        {
            if (spec.CueRequestOnApplyCode <= 0
                || spec.TargetAsc == Entity.Null)
            {
                return;
            }

            var factSequence = Allocate(ref stream.NextFactSequence);
            facts.Add(new GameplayEventBuffer
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
            });
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASAttributeModifierDeltaApplySystem))]
    [UpdateBefore(typeof(GameplayFactProjectionSystem))]
    [BurstCompile]
    public partial struct GameplayOwnerLocalFactFlushSystem : ISystem
    {
        private EntityQuery _ownerFactQuery;

        [BurstCompile]
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
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<GEEffectCommandStreamComponent>(out var streamEntity)
                || !EffectCommandSpecStream.HasRequiredBuffers(state.EntityManager, streamEntity))
            {
                return;
            }

            var records = new NativeList<OwnerLocalGameplayFactRecord>(1, Allocator.TempJob);
            var collectHandle = new CollectOwnerLocalGameplayFactsJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                OwnerFactType = SystemAPI.GetBufferTypeHandle<OwnerLocalGameplayFactBuffer>(isReadOnly: true),
                Records = records,
            }.Schedule(_ownerFactQuery, state.Dependency);
            var flushHandle = new FlushOwnerLocalGameplayFactsJob
            {
                StreamEntity = streamEntity,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                StreamFactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: false),
                Records = records,
            }.Schedule(collectHandle);

            state.Dependency = records.Dispose(flushHandle);
        }

        private struct OwnerLocalGameplayFactRecord
        {
            public Entity Owner;
            public int LocalIndex;
            public GameplayEventBuffer Fact;
        }

        [BurstCompile]
        private struct CollectOwnerLocalGameplayFactsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public BufferTypeHandle<OwnerLocalGameplayFactBuffer> OwnerFactType;
            public NativeList<OwnerLocalGameplayFactRecord> Records;

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
                        Records.Add(new OwnerLocalGameplayFactRecord
                        {
                            Owner = owner,
                            LocalIndex = factIndex,
                            Fact = facts[factIndex].Fact,
                        });
                    }
                }
            }
        }

        [BurstCompile]
        private struct FlushOwnerLocalGameplayFactsJob : IJob
        {
            public Entity StreamEntity;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GameplayEventBuffer> StreamFactLookup;
            public NativeList<OwnerLocalGameplayFactRecord> Records;

            public void Execute()
            {
                if (Records.Length == 0
                    || StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !StreamFactLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                Records.Sort(new OwnerLocalGameplayFactRecordComparer());
                var stream = StreamLookup[StreamEntity];
                var streamFacts = StreamFactLookup[StreamEntity];
                var ownerGroupCount = 0;
                var maxOwnerRange = 0;
                var flushCount = 0;
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

                    ownerGroupCount++;
                    var ownerRange = groupEnd - groupStart;
                    if (ownerRange > maxOwnerRange)
                        maxOwnerRange = ownerRange;

                    for (var i = groupStart; i < groupEnd; i++)
                    {
                        streamFacts.Add(Records[i].Fact);
                        flushCount++;
                    }

                    groupStart = groupEnd;
                }

                stream.OwnerLocalFactCount += Records.Length;
                stream.OwnerLocalFactOwnerGroupCount += ownerGroupCount;
                if (maxOwnerRange > stream.OwnerLocalFactMaxOwnerRange)
                    stream.OwnerLocalFactMaxOwnerRange = maxOwnerRange;
                stream.OwnerLocalFactFlushCount += flushCount;
                StreamLookup[StreamEntity] = stream;
            }
        }

        private struct OwnerLocalGameplayFactRecordComparer : IComparer<OwnerLocalGameplayFactRecord>
        {
            public int Compare(OwnerLocalGameplayFactRecord x, OwnerLocalGameplayFactRecord y)
            {
                var result = CompareEntity(x.Owner, y.Owner);
                if (result != 0)
                    return result;

                result = x.Fact.Sequence.CompareTo(y.Fact.Sequence);
                if (result != 0)
                    return result;

                return x.LocalIndex.CompareTo(y.LocalIndex);
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
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GameplayEventBusComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<GEEffectCommandStreamComponent>(out var streamEntity)
                || !SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var eventBusEntity))
            {
                return;
            }

            state.Dependency = new GameplayFactBoundaryProjectionJob
            {
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: true),
                AttributeEventLookup = SystemAPI.GetBufferLookup<AttributeChangeEventBuffer>(isReadOnly: false),
                CueRequestLookup = SystemAPI.GetBufferLookup<CueRequestBuffer>(isReadOnly: false),
                TagChangeEventLookup = SystemAPI.GetBufferLookup<TagChangeEventBuffer>(isReadOnly: false),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private struct GameplayFactBoundaryProjectionJob : IJob
        {
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            [ReadOnly] public BufferLookup<GameplayEventBuffer> FactLookup;
            public BufferLookup<AttributeChangeEventBuffer> AttributeEventLookup;
            public BufferLookup<CueRequestBuffer> CueRequestLookup;
            public BufferLookup<TagChangeEventBuffer> TagChangeEventLookup;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || EventBusEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !FactLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                var stream = StreamLookup[StreamEntity];
                var facts = FactLookup[StreamEntity];
                var start = ClampCursor(stream.EventBridgeFactCursor, facts.Length);

                var hasAttributeEvents = AttributeEventLookup.HasBuffer(EventBusEntity);
                var hasCueRequests = CueRequestLookup.HasBuffer(EventBusEntity);
                var hasTagChanges = TagChangeEventLookup.HasBuffer(EventBusEntity);
                if (!hasAttributeEvents && !hasCueRequests && !hasTagChanges)
                {
                    stream.EventBridgeFactCursor = facts.Length;
                    StreamLookup[StreamEntity] = stream;
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

                for (var i = start; i < facts.Length; i++)
                {
                    var fact = facts[i];
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

                stream.EventBridgeFactCursor = facts.Length;
                StreamLookup[StreamEntity] = stream;
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
