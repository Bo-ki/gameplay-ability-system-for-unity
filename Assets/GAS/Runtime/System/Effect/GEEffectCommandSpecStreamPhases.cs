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
            var ownerCapacity = _ownerInstantCommandQuery.CalculateEntityCountWithoutFiltering();
            if (ownerCapacity <= 0)
                return;
            if (ownerCapacity < 64)
                ownerCapacity = 64;

            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            if (!EffectCommandSpecStream.HasRequiredBuffers(state.EntityManager, streamEntity))
                return;

            var processedOwners =
                new NativeParallelHashMap<Entity, byte>(
                    ownerCapacity,
                    state.WorldUpdateAllocator);
            var carryoverOwners =
                new NativeList<OwnerLocalInstantPrepareDirtyOwnerBuffer>(
                    1,
                    state.WorldUpdateAllocator);
            var promoteDependency = new PromoteOwnerLocalInstantCommandsJob
            {
                StreamEntity = streamEntity,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                DirtyOwnerLookup =
                    SystemAPI.GetBufferLookup<OwnerLocalInstantPrepareDirtyOwnerBuffer>(isReadOnly: false),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: false),
                SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: false),
                SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(isReadOnly: false),
                NextFrameCommandLookup =
                    SystemAPI.GetBufferLookup<OwnerLocalInstantNextFrameCommandBuffer>(isReadOnly: false),
                NextFrameSetByCallerLookup =
                    SystemAPI.GetBufferLookup<OwnerLocalInstantNextFrameSetByCallerValueBuffer>(isReadOnly: false),
                ProcessedOwners = processedOwners,
                CarryoverOwners = carryoverOwners,
            }.Schedule(state.Dependency);
            state.Dependency = promoteDependency;
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
        private struct PromoteOwnerLocalInstantCommandsJob : IJob
        {
            public Entity StreamEntity;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<OwnerLocalInstantPrepareDirtyOwnerBuffer> DirtyOwnerLookup;
            public BufferLookup<GEEffectCommandBuffer> CommandLookup;
            public BufferLookup<GESetByCallerValueBuffer> SetByCallerLookup;
            public BufferLookup<GEEffectSpecBuffer> SpecLookup;
            public BufferLookup<OwnerLocalInstantNextFrameCommandBuffer> NextFrameCommandLookup;
            public BufferLookup<OwnerLocalInstantNextFrameSetByCallerValueBuffer> NextFrameSetByCallerLookup;
            public NativeParallelHashMap<Entity, byte> ProcessedOwners;
            public NativeList<OwnerLocalInstantPrepareDirtyOwnerBuffer> CarryoverOwners;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !DirtyOwnerLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                var dirtyOwners = DirtyOwnerLookup[StreamEntity];
                var counters = default(OwnerLocalInstantPrepareCounters);
                counters.ChunkCount = dirtyOwners.Length > 0 ? 1 : 0;
                for (var dirtyIndex = 0; dirtyIndex < dirtyOwners.Length; dirtyIndex++)
                {
                    var owner = dirtyOwners[dirtyIndex].Owner;
                    if (owner == Entity.Null
                        || !ProcessedOwners.TryAdd(owner, 1))
                    {
                        continue;
                    }

                    counters.ScannedOwnerCount++;
                    if (!CommandLookup.HasBuffer(owner)
                        || !SetByCallerLookup.HasBuffer(owner)
                        || !SpecLookup.HasBuffer(owner)
                        || !NextFrameCommandLookup.HasBuffer(owner)
                        || !NextFrameSetByCallerLookup.HasBuffer(owner))
                    {
                        counters.SkippedOwnerCount++;
                        continue;
                    }

                    var currentCommands = CommandLookup[owner];
                    var currentSetByCallerValues = SetByCallerLookup[owner];
                    var currentSpecs = SpecLookup[owner];
                    var deferredCommands = NextFrameCommandLookup[owner];
                    var deferredSetByCallerValues = NextFrameSetByCallerLookup[owner];
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

                    if (deferredCommands.Length > 0)
                    {
                        CarryoverOwners.Add(new OwnerLocalInstantPrepareDirtyOwnerBuffer
                        {
                            Owner = owner,
                        });
                    }

                    deferredCommands.Clear();
                    deferredSetByCallerValues.Clear();
                }

                dirtyOwners.Clear();
                for (var i = 0; i < CarryoverOwners.Length; i++)
                    dirtyOwners.Add(CarryoverOwners[i]);

                if (!counters.HasEvidence)
                    return;

                var stream = StreamLookup[StreamEntity];
                EffectCommandSpecStream.AddOwnerLocalInstantPrepareCounters(
                    ref stream,
                    counters.ChunkCount,
                    counters.ScannedOwnerCount,
                    counters.SkippedOwnerCount,
                    counters.DirtyOwnerCount,
                    counters.ClearedCommandCount,
                    counters.ClearedSpecCount,
                    counters.PromotedCommandCount);
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
            var ownerCapacity = _ownerMutationQuery.CalculateEntityCountWithoutFiltering();
            if (ownerCapacity <= 0)
                return;
            if (ownerCapacity < 64)
                ownerCapacity = 64;

            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            if (!EffectCommandSpecStream.HasRequiredBuffers(state.EntityManager, streamEntity))
                return;

            var processedOwners =
                new NativeParallelHashMap<Entity, byte>(
                    ownerCapacity,
                    state.WorldUpdateAllocator);
            var carryoverOwners =
                new NativeList<ActiveEffectMutationPrepareDirtyOwnerBuffer>(
                    1,
                    state.WorldUpdateAllocator);
            var clearDependency = new ClearOwnerLocalActiveEffectMutationsJob
            {
                StreamEntity = streamEntity,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                DirtyOwnerLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectMutationPrepareDirtyOwnerBuffer>(isReadOnly: false),
                CommandLookup = SystemAPI.GetBufferLookup<ActiveEffectMutationCommandBuffer>(isReadOnly: false),
                SetByCallerLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectMutationSetByCallerValueBuffer>(isReadOnly: false),
                NextFrameCommandLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectNextFrameMutationCommandBuffer>(isReadOnly: false),
                NextFrameSetByCallerLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectNextFrameMutationSetByCallerValueBuffer>(isReadOnly: false),
                MutationLookup = SystemAPI.GetBufferLookup<ActiveEffectMutationBuffer>(isReadOnly: false),
                ProcessedOwners = processedOwners,
                CarryoverOwners = carryoverOwners,
            }.Schedule(state.Dependency);
            state.Dependency = clearDependency;
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
        private struct ClearOwnerLocalActiveEffectMutationsJob : IJob
        {
            public Entity StreamEntity;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<ActiveEffectMutationPrepareDirtyOwnerBuffer> DirtyOwnerLookup;
            public BufferLookup<ActiveEffectMutationCommandBuffer> CommandLookup;
            public BufferLookup<ActiveEffectMutationSetByCallerValueBuffer> SetByCallerLookup;
            public BufferLookup<ActiveEffectNextFrameMutationCommandBuffer> NextFrameCommandLookup;
            public BufferLookup<ActiveEffectNextFrameMutationSetByCallerValueBuffer> NextFrameSetByCallerLookup;
            public BufferLookup<ActiveEffectMutationBuffer> MutationLookup;
            public NativeParallelHashMap<Entity, byte> ProcessedOwners;
            public NativeList<ActiveEffectMutationPrepareDirtyOwnerBuffer> CarryoverOwners;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !DirtyOwnerLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                var dirtyOwners = DirtyOwnerLookup[StreamEntity];
                var counters = default(ActiveMutationPrepareCounters);
                counters.ChunkCount = dirtyOwners.Length > 0 ? 1 : 0;
                for (var dirtyIndex = 0; dirtyIndex < dirtyOwners.Length; dirtyIndex++)
                {
                    var owner = dirtyOwners[dirtyIndex].Owner;
                    if (owner == Entity.Null
                        || !ProcessedOwners.TryAdd(owner, 1))
                    {
                        continue;
                    }

                    counters.ScannedOwnerCount++;
                    if (!CommandLookup.HasBuffer(owner)
                        || !SetByCallerLookup.HasBuffer(owner)
                        || !NextFrameCommandLookup.HasBuffer(owner)
                        || !NextFrameSetByCallerLookup.HasBuffer(owner)
                        || !MutationLookup.HasBuffer(owner))
                    {
                        counters.SkippedOwnerCount++;
                        continue;
                    }

                    var currentCommands = CommandLookup[owner];
                    var currentSetByCallerValues = SetByCallerLookup[owner];
                    var deferredCommands = NextFrameCommandLookup[owner];
                    var deferredSetByCallerValues = NextFrameSetByCallerLookup[owner];
                    var mutations = MutationLookup[owner];
                    if (currentCommands.Length == 0
                        && currentSetByCallerValues.Length == 0
                        && mutations.Length == 0
                        && deferredCommands.Length == 0
                        && deferredSetByCallerValues.Length == 0)
                    {
                        counters.SkippedOwnerCount++;
                        continue;
                    }

                    counters.DirtyOwnerCount++;
                    counters.ClearedMutationCount += mutations.Length;
                    counters.PromotedCommandCount += deferredCommands.Length;

                    currentCommands.Clear();
                    currentSetByCallerValues.Clear();
                    mutations.Clear();

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

                    if (deferredCommands.Length > 0)
                    {
                        CarryoverOwners.Add(new ActiveEffectMutationPrepareDirtyOwnerBuffer
                        {
                            Owner = owner,
                        });
                    }

                    deferredCommands.Clear();
                    deferredSetByCallerValues.Clear();
                }

                dirtyOwners.Clear();
                for (var i = 0; i < CarryoverOwners.Length; i++)
                    dirtyOwners.Add(CarryoverOwners[i]);

                if (!counters.HasEvidence)
                    return;

                var stream = StreamLookup[StreamEntity];
                EffectCommandSpecStream.AddActiveMutationPrepareCounters(
                    ref stream,
                    counters.ChunkCount,
                    counters.ScannedOwnerCount,
                    counters.SkippedOwnerCount,
                    counters.DirtyOwnerCount,
                    counters.ClearedMutationCount,
                    counters.PromotedCommandCount);
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
            if (!EffectCommandSpecStream.HasRequiredBuffers(state.EntityManager, streamEntity))
                return;

            state.Dependency = new GameplayFactProjectionJob
            {
                StreamEntity = streamEntity,
                EntityType = SystemAPI.GetEntityTypeHandle(),
                DestroyingType = SystemAPI.GetComponentTypeHandle<ASCDestroyingComponent>(isReadOnly: true),
                SpecType = SystemAPI.GetBufferTypeHandle<GEEffectSpecBuffer>(isReadOnly: true),
                OwnerFactType = SystemAPI.GetBufferTypeHandle<OwnerLocalGameplayFactBuffer>(),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                DirtyOwnerLookup =
                    SystemAPI.GetBufferLookup<OwnerLocalGameplayFactDirtyOwnerBuffer>(isReadOnly: false),
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
            public BufferLookup<OwnerLocalGameplayFactDirtyOwnerBuffer> DirtyOwnerLookup;

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
                    {
                        if (ProjectCueRequest(specs[i], owner, facts, ref stream))
                        {
                            EffectCommandSpecStream.MarkOwnerLocalGameplayFactDirty(
                                DirtyOwnerLookup,
                                StreamEntity,
                                owner);
                        }
                    }
                }

                StreamLookup[StreamEntity] = stream;
            }
        }

        private static bool ProjectCueRequest(
            in GEEffectSpecBuffer spec,
            Entity owner,
            DynamicBuffer<OwnerLocalGameplayFactBuffer> facts,
            ref GEEffectCommandStreamComponent stream)
        {
            if (spec.CueRequestOnApplyCode <= 0
                || spec.TargetAsc == Entity.Null
                || CompareEntity(spec.TargetAsc, owner) != 0)
            {
                return false;
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
            return true;
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

            var ownerCapacity = _ownerFactQuery.CalculateEntityCountWithoutFiltering();
            if (ownerCapacity < 64)
                ownerCapacity = 64;

            var records = new NativeList<BoundaryObservationFactRecord>(1, Allocator.TempJob);
            var collectCounters = new NativeArray<OwnerLocalFactCollectCounters>(1, Allocator.TempJob);
            var processedOwners =
                new NativeParallelHashMap<Entity, byte>(
                    ownerCapacity,
                    Allocator.TempJob);
            var collectHandle = new CollectOwnerLocalGameplayFactsJob
            {
                StreamEntity = streamEntity,
                DirtyOwnerLookup =
                    SystemAPI.GetBufferLookup<OwnerLocalGameplayFactDirtyOwnerBuffer>(isReadOnly: false),
                OwnerFactLookup = SystemAPI.GetBufferLookup<OwnerLocalGameplayFactBuffer>(isReadOnly: false),
                ProcessedOwners = processedOwners,
                Records = records,
                Counters = collectCounters,
            }.Schedule(state.Dependency);
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
            var processedOwnersDisposeHandle = processedOwners.Dispose(exportHandle);
            state.Dependency = JobHandle.CombineDependencies(
                recordsDisposeHandle,
                countersDisposeHandle,
                processedOwnersDisposeHandle);
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
        private struct CollectOwnerLocalGameplayFactsJob : IJob
        {
            public Entity StreamEntity;
            public BufferLookup<OwnerLocalGameplayFactDirtyOwnerBuffer> DirtyOwnerLookup;
            public BufferLookup<OwnerLocalGameplayFactBuffer> OwnerFactLookup;
            public NativeParallelHashMap<Entity, byte> ProcessedOwners;
            public NativeList<BoundaryObservationFactRecord> Records;
            public NativeArray<OwnerLocalFactCollectCounters> Counters;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || !DirtyOwnerLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                var dirtyOwners = DirtyOwnerLookup[StreamEntity];
                var counters = Counters[0];
                counters.ChangedChunkCount = dirtyOwners.Length > 0 ? 1 : 0;
                for (var dirtyIndex = 0; dirtyIndex < dirtyOwners.Length; dirtyIndex++)
                {
                    var owner = dirtyOwners[dirtyIndex].Owner;
                    if (owner == Entity.Null
                        || !ProcessedOwners.TryAdd(owner, 1))
                    {
                        continue;
                    }

                    counters.ScannedOwnerCount++;
                    if (!OwnerFactLookup.HasBuffer(owner))
                    {
                        counters.SkippedOwnerCount++;
                        continue;
                    }

                    var facts = OwnerFactLookup[owner];
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

                dirtyOwners.Clear();
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
