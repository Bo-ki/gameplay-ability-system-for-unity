using Unity.Burst;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Jobs;

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
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var chunkCount = _ownerInstantCommandQuery.CalculateChunkCountWithoutFiltering();
            if (chunkCount <= 0)
                return;

            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            var counters =
                CollectionHelper.CreateNativeArray<OwnerLocalInstantPrepareCounters>(
                    chunkCount,
                    state.WorldUpdateAllocator,
                    NativeArrayOptions.ClearMemory);
            var promoteDependency = new PromoteOwnerLocalInstantCommandsJob
            {
                CommandType = SystemAPI.GetBufferTypeHandle<GEEffectCommandBuffer>(),
                SetByCallerType = SystemAPI.GetBufferTypeHandle<GESetByCallerValueBuffer>(),
                SpecType = SystemAPI.GetBufferTypeHandle<GEEffectSpecBuffer>(),
                NextFrameCommandType = SystemAPI.GetBufferTypeHandle<OwnerLocalInstantNextFrameCommandBuffer>(),
                NextFrameSetByCallerType = SystemAPI.GetBufferTypeHandle<OwnerLocalInstantNextFrameSetByCallerValueBuffer>(),
                Counters = counters,
            }.Schedule(_ownerInstantCommandQuery, state.Dependency);
            state.Dependency = new FlushOwnerLocalInstantPrepareCountersJob
            {
                StreamEntity = streamEntity,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                Counters = counters,
            }.Schedule(promoteDependency);
        }

        private struct OwnerLocalInstantPrepareCounters
        {
            public int ChunkCount;
            public int ScannedOwnerCount;
            public int SkippedOwnerCount;
            public int DirtyOwnerCount;
            public int ClearedCommandCount;
            public int ClearedSpecCount;
            public int PromotedCommandCount;

            public bool HasEvidence =>
                ChunkCount > 0
                || ScannedOwnerCount > 0
                || SkippedOwnerCount > 0
                || DirtyOwnerCount > 0
                || ClearedCommandCount > 0
                || ClearedSpecCount > 0
                || PromotedCommandCount > 0;

            public void Add(in OwnerLocalInstantPrepareCounters other)
            {
                ChunkCount += other.ChunkCount;
                ScannedOwnerCount += other.ScannedOwnerCount;
                SkippedOwnerCount += other.SkippedOwnerCount;
                DirtyOwnerCount += other.DirtyOwnerCount;
                ClearedCommandCount += other.ClearedCommandCount;
                ClearedSpecCount += other.ClearedSpecCount;
                PromotedCommandCount += other.PromotedCommandCount;
            }
        }

        [BurstCompile]
        private struct PromoteOwnerLocalInstantCommandsJob : IJobChunk
        {
            public BufferTypeHandle<GEEffectCommandBuffer> CommandType;
            public BufferTypeHandle<GESetByCallerValueBuffer> SetByCallerType;
            public BufferTypeHandle<GEEffectSpecBuffer> SpecType;
            public BufferTypeHandle<OwnerLocalInstantNextFrameCommandBuffer> NextFrameCommandType;
            public BufferTypeHandle<OwnerLocalInstantNextFrameSetByCallerValueBuffer> NextFrameSetByCallerType;
            [NativeDisableParallelForRestriction] public NativeArray<OwnerLocalInstantPrepareCounters> Counters;

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
                var counters = default(OwnerLocalInstantPrepareCounters);
                counters.ChunkCount++;
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    counters.ScannedOwnerCount++;
                    var currentCommands = commands[entityIndex];
                    var currentSetByCallerValues = setByCallerValues[entityIndex];
                    var currentSpecs = specs[entityIndex];
                    var deferredCommands = nextFrameCommands[entityIndex];
                    var deferredSetByCallerValues = nextFrameSetByCallerValues[entityIndex];
                    if (currentCommands.Length == 0
                        && currentSetByCallerValues.Length == 0
                        && currentSpecs.Length == 0
                        && deferredCommands.Length == 0
                        && deferredSetByCallerValues.Length == 0)
                    {
                        counters.SkippedOwnerCount++;
                        continue;
                    }

                    counters.DirtyOwnerCount++;
                    counters.ClearedCommandCount += currentCommands.Length;
                    counters.ClearedSpecCount += currentSpecs.Length;
                    counters.PromotedCommandCount += deferredCommands.Length;

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

                if (Counters.IsCreated
                    && (uint)unfilteredChunkIndex < (uint)Counters.Length)
                {
                    Counters[unfilteredChunkIndex] = counters;
                }
            }
        }

        [BurstCompile]
        private struct FlushOwnerLocalInstantPrepareCountersJob : IJob
        {
            public Entity StreamEntity;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            [ReadOnly] public NativeArray<OwnerLocalInstantPrepareCounters> Counters;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !Counters.IsCreated)
                {
                    return;
                }

                var total = default(OwnerLocalInstantPrepareCounters);
                for (var i = 0; i < Counters.Length; i++)
                    total.Add(Counters[i]);

                if (!total.HasEvidence)
                    return;

                var stream = StreamLookup[StreamEntity];
                EffectCommandSpecStream.AddOwnerLocalInstantPrepareCounters(
                    ref stream,
                    total.ChunkCount,
                    total.ScannedOwnerCount,
                    total.SkippedOwnerCount,
                    total.DirtyOwnerCount,
                    total.ClearedCommandCount,
                    total.ClearedSpecCount,
                    total.PromotedCommandCount);
                StreamLookup[StreamEntity] = stream;
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
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var chunkCount = _ownerMutationQuery.CalculateChunkCountWithoutFiltering();
            if (chunkCount <= 0)
                return;

            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            var counters =
                CollectionHelper.CreateNativeArray<ActiveMutationPrepareCounters>(
                    chunkCount,
                    state.WorldUpdateAllocator,
                    NativeArrayOptions.ClearMemory);
            var clearDependency = new ClearOwnerLocalActiveEffectMutationsJob
            {
                CommandType = SystemAPI.GetBufferTypeHandle<ActiveEffectMutationCommandBuffer>(),
                SetByCallerType = SystemAPI.GetBufferTypeHandle<ActiveEffectMutationSetByCallerValueBuffer>(),
                NextFrameCommandType = SystemAPI.GetBufferTypeHandle<ActiveEffectNextFrameMutationCommandBuffer>(),
                NextFrameSetByCallerType = SystemAPI.GetBufferTypeHandle<ActiveEffectNextFrameMutationSetByCallerValueBuffer>(),
                MutationType = SystemAPI.GetBufferTypeHandle<ActiveEffectMutationBuffer>(),
                Counters = counters,
            }.Schedule(_ownerMutationQuery, state.Dependency);
            state.Dependency = new FlushActiveMutationPrepareCountersJob
            {
                StreamEntity = streamEntity,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                Counters = counters,
            }.Schedule(clearDependency);
        }

        private struct ActiveMutationPrepareCounters
        {
            public int ChunkCount;
            public int ScannedOwnerCount;
            public int SkippedOwnerCount;
            public int DirtyOwnerCount;
            public int ClearedMutationCount;
            public int PromotedCommandCount;

            public bool HasEvidence =>
                ChunkCount > 0
                || ScannedOwnerCount > 0
                || SkippedOwnerCount > 0
                || DirtyOwnerCount > 0
                || ClearedMutationCount > 0
                || PromotedCommandCount > 0;

            public void Add(in ActiveMutationPrepareCounters other)
            {
                ChunkCount += other.ChunkCount;
                ScannedOwnerCount += other.ScannedOwnerCount;
                SkippedOwnerCount += other.SkippedOwnerCount;
                DirtyOwnerCount += other.DirtyOwnerCount;
                ClearedMutationCount += other.ClearedMutationCount;
                PromotedCommandCount += other.PromotedCommandCount;
            }
        }

        [BurstCompile]
        private struct ClearOwnerLocalActiveEffectMutationsJob : IJobChunk
        {
            public BufferTypeHandle<ActiveEffectMutationCommandBuffer> CommandType;
            public BufferTypeHandle<ActiveEffectMutationSetByCallerValueBuffer> SetByCallerType;
            public BufferTypeHandle<ActiveEffectNextFrameMutationCommandBuffer> NextFrameCommandType;
            public BufferTypeHandle<ActiveEffectNextFrameMutationSetByCallerValueBuffer> NextFrameSetByCallerType;
            public BufferTypeHandle<ActiveEffectMutationBuffer> MutationType;
            [NativeDisableParallelForRestriction] public NativeArray<ActiveMutationPrepareCounters> Counters;

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
                var counters = default(ActiveMutationPrepareCounters);
                counters.ChunkCount++;
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    counters.ScannedOwnerCount++;
                    var currentCommands = commands[entityIndex];
                    var currentSetByCallerValues = setByCallerValues[entityIndex];
                    var deferredCommands = nextFrameCommands[entityIndex];
                    var deferredSetByCallerValues = nextFrameSetByCallerValues[entityIndex];
                    if (currentCommands.Length == 0
                        && currentSetByCallerValues.Length == 0
                        && mutations[entityIndex].Length == 0
                        && deferredCommands.Length == 0
                        && deferredSetByCallerValues.Length == 0)
                    {
                        counters.SkippedOwnerCount++;
                        continue;
                    }

                    counters.DirtyOwnerCount++;
                    counters.ClearedMutationCount += mutations[entityIndex].Length;
                    counters.PromotedCommandCount += deferredCommands.Length;

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

                if (Counters.IsCreated
                    && (uint)unfilteredChunkIndex < (uint)Counters.Length)
                {
                    Counters[unfilteredChunkIndex] = counters;
                }
            }
        }

        [BurstCompile]
        private struct FlushActiveMutationPrepareCountersJob : IJob
        {
            public Entity StreamEntity;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            [ReadOnly] public NativeArray<ActiveMutationPrepareCounters> Counters;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !Counters.IsCreated)
                {
                    return;
                }

                var total = default(ActiveMutationPrepareCounters);
                for (var i = 0; i < Counters.Length; i++)
                    total.Add(Counters[i]);

                if (!total.HasEvidence)
                    return;

                var stream = StreamLookup[StreamEntity];
                EffectCommandSpecStream.AddActiveMutationPrepareCounters(
                    ref stream,
                    total.ChunkCount,
                    total.ScannedOwnerCount,
                    total.SkippedOwnerCount,
                    total.DirtyOwnerCount,
                    total.ClearedMutationCount,
                    total.PromotedCommandCount);
                StreamLookup[StreamEntity] = stream;
            }
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASFramePrepareSystemGroup))]
    [UpdateAfter(typeof(ActiveEffectOwnerLocalMutationFramePrepareSystem))]
    [BurstCompile]
    public partial struct GameplayOwnerLocalFactFramePrepareSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // owner-local facts are cleared by GameplayBoundaryFactExportSystem after export.
            // Keeping FramePrepare read-only preserves the OwnerLocalGameplayFactBuffer changed-version lane.
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

            var factSequence = GASRuntimeSequenceAllocator.AllocateFactSequence(ref stream);
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
                    ComponentType.ReadWrite<OwnerLocalGameplayFactBuffer>(),
                },
            });
            _ownerFactQuery.SetChangedVersionFilter(ComponentType.ReadWrite<OwnerLocalGameplayFactBuffer>());
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
            var collectCounters = new NativeArray<OwnerLocalFactCollectCounters>(1, Allocator.TempJob);
            var collectHandle = new CollectOwnerLocalGameplayFactsJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                OwnerFactType = SystemAPI.GetBufferTypeHandle<OwnerLocalGameplayFactBuffer>(),
                Records = records,
                Counters = collectCounters,
            }.Schedule(_ownerFactQuery, state.Dependency);
            var exportHandle = new ExportBoundaryObservationFactsJob
            {
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                BoundaryObservationLookup = SystemAPI.GetBufferLookup<BoundaryObservationFactBuffer>(isReadOnly: false),
                Records = records,
                Counters = collectCounters,
            }.Schedule(collectHandle);

            var recordsDisposeHandle = records.Dispose(exportHandle);
            var countersDisposeHandle = collectCounters.Dispose(exportHandle);
            state.Dependency = JobHandle.CombineDependencies(recordsDisposeHandle, countersDisposeHandle);
        }

        private struct BoundaryObservationFactRecord
        {
            public Entity Owner;
            public int LocalIndex;
            public GameplayEventBuffer Fact;
            public EBoundaryObservationFactSource Source;
        }

        private struct OwnerLocalFactCollectCounters
        {
            public int ChangedChunkCount;
            public int ScannedOwnerCount;
            public int DirtyOwnerCount;
            public int DirtyFactCount;
            public int SkippedOwnerCount;
            public int ClearedOwnerCount;
        }

        [BurstCompile]
        private struct CollectOwnerLocalGameplayFactsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            public BufferTypeHandle<OwnerLocalGameplayFactBuffer> OwnerFactType;
            public NativeList<BoundaryObservationFactRecord> Records;
            public NativeArray<OwnerLocalFactCollectCounters> Counters;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var counters = Counters[0];
                counters.ChangedChunkCount++;
                var entities = chunk.GetNativeArray(EntityType);
                var ownerFacts = chunk.GetBufferAccessor(ref OwnerFactType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    counters.ScannedOwnerCount++;
                    var owner = entities[entityIndex];
                    var facts = ownerFacts[entityIndex];
                    if (facts.Length == 0)
                    {
                        counters.SkippedOwnerCount++;
                        continue;
                    }

                    counters.DirtyOwnerCount++;
                    counters.DirtyFactCount += facts.Length;
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

                    facts.Clear();
                    counters.ClearedOwnerCount++;
                }

                Counters[0] = counters;
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
            [ReadOnly] public NativeArray<OwnerLocalFactCollectCounters> Counters;

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
                    AddCollectCounters(ref stream, Counters[0]);
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
                AddCollectCounters(ref stream, Counters[0]);
                StreamLookup[StreamEntity] = stream;
            }

            private static void AddCollectCounters(
                ref GEEffectCommandStreamComponent stream,
                in OwnerLocalFactCollectCounters counters)
            {
                stream.OwnerLocalFactChangedChunkCount += counters.ChangedChunkCount;
                stream.OwnerLocalFactScannedOwnerCount += counters.ScannedOwnerCount;
                stream.OwnerLocalFactDirtyOwnerCount += counters.DirtyOwnerCount;
                stream.OwnerLocalFactSkippedOwnerCount += counters.SkippedOwnerCount;
                stream.OwnerLocalFactClearedOwnerCount += counters.ClearedOwnerCount;
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

}
