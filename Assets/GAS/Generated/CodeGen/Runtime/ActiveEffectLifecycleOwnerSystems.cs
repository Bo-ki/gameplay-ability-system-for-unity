using GAS.Runtime;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime.Generated
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASActiveEffectRemoveSystem))]
    [UpdateBefore(typeof(GEEffectSpecBuildSystem))]
    public partial struct GEEffectCommandCatalogNormalizeSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<GEEffectCommandBuffer>(),
                    ComponentType.ReadOnly<GESetByCallerValueBuffer>(),
                },
            });
            state.RequireForUpdate(_query);
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            state.Dependency = new GEEffectCommandCatalogNormalizeJob
            {
                CommandTypeHandle = SystemAPI.GetBufferTypeHandle<GEEffectCommandBuffer>(),
                SetByCallerTypeHandle = SystemAPI.GetBufferTypeHandle<GESetByCallerValueBuffer>(isReadOnly: true),
                ActiveMutationCommandLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectMutationCommandBuffer>(isReadOnly: false),
                ActiveMutationSetByCallerLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectMutationSetByCallerValueBuffer>(isReadOnly: false),
                Catalog = catalogComponent.Catalog,
            }.Schedule(_query, state.Dependency);
        }

        private struct GEEffectCommandCatalogNormalizeJob : IJobChunk
        {
            public BufferTypeHandle<GEEffectCommandBuffer> CommandTypeHandle;
            [ReadOnly] public BufferTypeHandle<GESetByCallerValueBuffer> SetByCallerTypeHandle;
            public BufferLookup<ActiveEffectMutationCommandBuffer> ActiveMutationCommandLookup;
            public BufferLookup<ActiveEffectMutationSetByCallerValueBuffer> ActiveMutationSetByCallerLookup;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!Catalog.IsCreated)
                    return;

                ref var catalog = ref Catalog.Value;
                var commandBuffers = chunk.GetBufferAccessor(ref CommandTypeHandle);
                var setByCallerBuffers = chunk.GetBufferAccessor(ref SetByCallerTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var bufferIndex))
                {
                    var commands = commandBuffers[bufferIndex];
                    var setByCallerValues = setByCallerBuffers[bufferIndex];
                    for (var i = 0; i < commands.Length; i++)
                    {
                        var command = commands[i];
                        if (GASGeneratedActiveEffectRuntime.TryNormalizeCommand(ref catalog, ref command))
                            commands[i] = command;
                        AppendActiveMutationCommand(in command, setByCallerValues);
                    }
                }
            }

            private void AppendActiveMutationCommand(
                in GEEffectCommandBuffer command,
                DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues)
            {
                if (command.Kind != GEEffectCommandKind.ActiveMutation
                    || command.TargetAsc == Entity.Null
                    || !ActiveMutationCommandLookup.HasBuffer(command.TargetAsc)
                    || !ActiveMutationSetByCallerLookup.HasBuffer(command.TargetAsc))
                {
                    return;
                }

                var ownerSetByCallerValues = ActiveMutationSetByCallerLookup[command.TargetAsc];
                var ownerCommand = CopySetByCallerValuesToOwner(in command, setByCallerValues, ownerSetByCallerValues);
                ActiveMutationCommandLookup[command.TargetAsc].Add(new ActiveEffectMutationCommandBuffer
                {
                    Command = ownerCommand,
                });
            }

            private static GEEffectCommandBuffer CopySetByCallerValuesToOwner(
                in GEEffectCommandBuffer command,
                DynamicBuffer<GESetByCallerValueBuffer> source,
                DynamicBuffer<ActiveEffectMutationSetByCallerValueBuffer> target)
            {
                if (command.SetByCallerCount <= 0)
                {
                    var emptyCommand = command;
                    emptyCommand.SetByCallerStart = 0;
                    emptyCommand.SetByCallerCount = 0;
                    return emptyCommand;
                }

                var ownerCommand = command;
                var ownerStart = target.Length;
                var copied = 0;
                var start = command.SetByCallerStart < 0 ? 0 : command.SetByCallerStart;
                var end = start + command.SetByCallerCount;
                if (end > source.Length)
                    end = source.Length;

                for (var i = start; i < end; i++)
                {
                    var value = source[i];
                    if (value.CommandSequence != command.Sequence)
                        continue;

                    target.Add(new ActiveEffectMutationSetByCallerValueBuffer
                    {
                        Value = new GESetByCallerValueBuffer
                        {
                            CommandSequence = command.Sequence,
                            SpecSequence = value.SpecSequence,
                            Key = value.Key,
                            Value = value.Value,
                        },
                    });
                    copied++;
                }

                ownerCommand.SetByCallerStart = copied > 0 ? ownerStart : 0;
                ownerCommand.SetByCallerCount = copied;
                return ownerCommand;
            }
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEEffectSpecBuildSystem))]
    [UpdateBefore(typeof(GASAttributeSetReduceApplySystem))]
    [UpdateBefore(typeof(GEExecutionCalculationSystem))]
    public partial struct GASActiveEffectMutationApplySystem : ISystem
    {
        private EntityQuery _ownerQuery;

        public void OnCreate(ref SystemState state)
        {
            _ownerQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<ASCDestroyingComponent>(),
                    ComponentType.ReadWrite<ASCActiveEffectsComponent>(),
                    ComponentType.ReadWrite<ActiveGameplayEffectBuffer>(),
                    ComponentType.ReadWrite<ActiveGameplayEffectSetByCallerValueBuffer>(),
                    ComponentType.ReadWrite<ActiveGameplayEffectCleanupRecordBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectMutationCommandBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectMutationSetByCallerValueBuffer>(),
                    ComponentType.ReadWrite<OwnerLocalInstantNextFrameCommandBuffer>(),
                    ComponentType.ReadWrite<OwnerLocalInstantNextFrameSetByCallerValueBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectNextFrameMutationCommandBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectNextFrameMutationSetByCallerValueBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectMutationBuffer>(),
                    ComponentType.ReadWrite<TagMaskComponent>(),
                    ComponentType.ReadOnly<TagFixedMaskComponent>(),
                    ComponentType.ReadWrite<AttributeValueBuffer>(),
                    ComponentType.ReadWrite<AttributeActiveModifierBuffer>(),
                    ComponentType.ReadWrite<TagTemporarySourceBuffer>(),
                    ComponentType.ReadWrite<AbilitySlotBuffer>(),
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState,
            });
            state.RequireForUpdate(_ownerQuery);
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            var em = state.EntityManager;
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            if (!EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var structuralEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var grantedAbilityArchetype = GASRuntimeEntityArchetypes.GrantedAbility(em);
            var commandCapacity = _ownerQuery.CalculateEntityCountWithoutFiltering() * 4;
            if (commandCapacity < 64)
                commandCapacity = 64;

            var activeMutationCommands = new NativeList<GEEffectCommandBuffer>(commandCapacity, state.WorldUpdateAllocator);
            var activeMutationSetByCallerValues =
                new NativeList<GESetByCallerValueBuffer>(commandCapacity * 2, state.WorldUpdateAllocator);
            var activeMutationOwnerRanges =
                new NativeParallelHashMap<Entity, GASGeneratedActiveEffectRuntime.ActiveMutationCommandRange>(
                    commandCapacity,
                    state.WorldUpdateAllocator);
            var activeMutationSourceAttributeSnapshotCapacity = commandCapacity * 16;
            if (activeMutationSourceAttributeSnapshotCapacity < 256)
                activeMutationSourceAttributeSnapshotCapacity = 256;
            var activeMutationSourceAttributeSnapshots =
                new NativeParallelHashMap<long, float>(
                    activeMutationSourceAttributeSnapshotCapacity,
                    state.WorldUpdateAllocator);

            var collectJob = new GASGeneratedActiveEffectRuntime.GEActiveEffectMutationOwnerCommandCollectJob
            {
                CommandBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<ActiveEffectMutationCommandBuffer>(isReadOnly: true),
                SetByCallerBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<ActiveEffectMutationSetByCallerValueBuffer>(isReadOnly: true),
                ActiveMutationCommands = activeMutationCommands,
                ActiveMutationSetByCallerValues = activeMutationSetByCallerValues,
            };
            var collectDependency = collectJob.Schedule(_ownerQuery, state.Dependency);

            var gatherJob = new GASGeneratedActiveEffectRuntime.GEActiveEffectMutationOwnerCommandFinalizeJob
            {
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: true),
                Catalog = catalogComponent.Catalog,
                ActiveMutationCommands = activeMutationCommands,
                ActiveMutationOwnerRanges = activeMutationOwnerRanges,
                ActiveMutationSourceAttributeSnapshots = activeMutationSourceAttributeSnapshots,
                StreamEntity = streamEntity,
            };
            var gatherDependency = gatherJob.Schedule(collectDependency);

            state.Dependency = new GASGeneratedActiveEffectRuntime.GEActiveEffectMutationChunkApplyJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                DestroyingTypeHandle = SystemAPI.GetComponentTypeHandle<ASCDestroyingComponent>(isReadOnly: true),
                ActiveEffectsTypeHandle =
                    SystemAPI.GetComponentTypeHandle<ASCActiveEffectsComponent>(isReadOnly: false),
                ActiveEffectSlotBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(isReadOnly: false),
                SetByCallerSnapshotBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectSetByCallerValueBuffer>(isReadOnly: false),
                CleanupRecordBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectCleanupRecordBuffer>(isReadOnly: false),
                MutationBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ActiveEffectMutationBuffer>(isReadOnly: false),
                TagMaskTypeHandle = SystemAPI.GetComponentTypeHandle<TagMaskComponent>(isReadOnly: false),
                TagFixedMaskTypeHandle = SystemAPI.GetComponentTypeHandle<TagFixedMaskComponent>(isReadOnly: true),
                AttributeBufferTypeHandle = SystemAPI.GetBufferTypeHandle<AttributeValueBuffer>(isReadOnly: false),
                ActiveModifierBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<AttributeActiveModifierBuffer>(isReadOnly: false),
                TagSourceBufferTypeHandle = SystemAPI.GetBufferTypeHandle<TagTemporarySourceBuffer>(isReadOnly: false),
                AbilitySlotBufferTypeHandle = SystemAPI.GetBufferTypeHandle<AbilitySlotBuffer>(isReadOnly: false),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                NextFrameInstantCommandLookup =
                    SystemAPI.GetBufferLookup<OwnerLocalInstantNextFrameCommandBuffer>(isReadOnly: false),
                NextFrameInstantSetByCallerLookup =
                    SystemAPI.GetBufferLookup<OwnerLocalInstantNextFrameSetByCallerValueBuffer>(isReadOnly: false),
                NextFrameActiveMutationCommandLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectNextFrameMutationCommandBuffer>(isReadOnly: false),
                NextFrameActiveMutationSetByCallerLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectNextFrameMutationSetByCallerValueBuffer>(isReadOnly: false),
                AttributeOwnerMarkerRequestLookup =
                    SystemAPI.GetBufferLookup<AttributeOwnerMarkerRequestBuffer>(isReadOnly: false),
                AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: false),
                AbilityGrantedLookup = SystemAPI.GetComponentLookup<AbilityGrantedByEffectComponent>(isReadOnly: true),
                AbilityLifecycleRequestLookup =
                    SystemAPI.GetBufferLookup<AbilityLifecycleRequestBuffer>(isReadOnly: false),
                OwnerFactLookup = SystemAPI.GetBufferLookup<OwnerLocalGameplayFactBuffer>(isReadOnly: false),
                StructuralEcb = structuralEcb,
                GrantedAbilityArchetype = grantedAbilityArchetype,
                Catalog = catalogComponent.Catalog,
                ActiveMutationCommands = activeMutationCommands,
                ActiveMutationSetByCallerValues = activeMutationSetByCallerValues,
                ActiveMutationOwnerRanges = activeMutationOwnerRanges,
                ActiveMutationSourceAttributeSnapshots = activeMutationSourceAttributeSnapshots,
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
            }.Schedule(_ownerQuery, gatherDependency);
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup), OrderFirst = true)]
    public partial struct GASActiveEffectPreTickSystem : ISystem
    {
        private EntityQuery _ownerQuery;

        public void OnCreate(ref SystemState state)
        {
            _ownerQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<ASCActiveEffectsComponent>(),
                    ComponentType.ReadWrite<ActiveGameplayEffectBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectMutationBuffer>(),
                },
            });
            state.RequireForUpdate(_ownerQuery);
            state.RequireForUpdate<GlobalTimer>();
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            var em = state.EntityManager;
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            if (!EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity)
                || !EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var structuralEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var grantedAbilityArchetype = GASRuntimeEntityArchetypes.GrantedAbility(em);
            var ownerChunkCount = _ownerQuery.CalculateChunkCountWithoutFiltering();
            if (ownerChunkCount <= 0)
                return;

            var ownerCapacity = _ownerQuery.CalculateEntityCount();
            ref var catalog = ref catalogComponent.Catalog.Value;
            var activeEffectSlotSourceAttributeSnapshotCapacity =
                GASGeneratedActiveEffectRuntime.EstimateActiveEffectSlotSourceAttributeSnapshotCapacity(
                    ref catalog,
                    ownerCapacity);
            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            EffectCommandSpecStream.SetActiveEffectSlotSourceSnapshotCapacity(
                ref stream,
                activeEffectSlotSourceAttributeSnapshotCapacity);
            em.SetComponentData(streamEntity, stream);

            var activeEffectSlotSourceAttributeSnapshots =
                new NativeParallelHashMap<GASGeneratedActiveEffectRuntime.ActiveEffectSlotSourceAttributeSnapshotKey, float>(
                    activeEffectSlotSourceAttributeSnapshotCapacity,
                    state.WorldUpdateAllocator);
            var activeEffectSlotSourceSnapshotLaneCounters =
                CollectionHelper.CreateNativeArray<GASGeneratedActiveEffectRuntime.ActiveEffectSlotSourceSnapshotLaneCounters>(
                    ownerChunkCount,
                    state.WorldUpdateAllocator,
                    NativeArrayOptions.ClearMemory);
            var snapshotGatherJob = new GASGeneratedActiveEffectRuntime.GEActiveEffectPreTickSourceAttributeSnapshotGatherJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                ActiveEffectSlotBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(isReadOnly: true),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: true),
                Catalog = catalogComponent.Catalog,
                ActiveEffectSlotSourceAttributeSnapshots = activeEffectSlotSourceAttributeSnapshots.AsParallelWriter(),
                SnapshotLaneCounters = activeEffectSlotSourceSnapshotLaneCounters,
                Frame = frame,
            };
            var snapshotDependency = snapshotGatherJob.ScheduleParallel(_ownerQuery, state.Dependency);
            var tickJob = new GASGeneratedActiveEffectRuntime.GEActiveEffectPreTickJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                ActiveEffectsTypeHandle = SystemAPI.GetComponentTypeHandle<ASCActiveEffectsComponent>(isReadOnly: false),
                ActiveEffectSlotBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(isReadOnly: false),
                MutationBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ActiveEffectMutationBuffer>(isReadOnly: false),
                CleanupRecordLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectCleanupRecordBuffer>(isReadOnly: false),
                SetByCallerSnapshotLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectSetByCallerValueBuffer>(isReadOnly: false),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: false),
                ActiveModifierLookup = SystemAPI.GetBufferLookup<AttributeActiveModifierBuffer>(isReadOnly: false),
                AttributeOwnerMarkerRequestLookup =
                    SystemAPI.GetBufferLookup<AttributeOwnerMarkerRequestBuffer>(isReadOnly: false),
                TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(isReadOnly: false),
                TagFixedMaskLookup = SystemAPI.GetComponentLookup<TagFixedMaskComponent>(isReadOnly: true),
                TagSourceLookup = SystemAPI.GetBufferLookup<TagTemporarySourceBuffer>(isReadOnly: false),
                AbilitySlotLookup = SystemAPI.GetBufferLookup<AbilitySlotBuffer>(isReadOnly: false),
                AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: false),
                AbilityGrantedLookup = SystemAPI.GetComponentLookup<AbilityGrantedByEffectComponent>(isReadOnly: true),
                RemoveCommandBufferTypeHandle = SystemAPI.GetBufferTypeHandle<GERemoveCommandBuffer>(isReadOnly: false),
                RemovePendingTypeHandle =
                    SystemAPI.GetComponentTypeHandle<GERemoveCommandPendingComponent>(isReadOnly: false),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: false),
                CommandSetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: false),
                ActiveMutationCommandLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectMutationCommandBuffer>(isReadOnly: false),
                ActiveMutationSetByCallerLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectMutationSetByCallerValueBuffer>(isReadOnly: false),
                AbilityLifecycleRequestLookup =
                    SystemAPI.GetBufferLookup<AbilityLifecycleRequestBuffer>(isReadOnly: false),
                OwnerFactLookup = SystemAPI.GetBufferLookup<OwnerLocalGameplayFactBuffer>(isReadOnly: false),
                StructuralEcb = structuralEcb,
                GrantedAbilityArchetype = grantedAbilityArchetype,
                Catalog = catalogComponent.Catalog,
                ActiveEffectSlotSourceAttributeSnapshots = activeEffectSlotSourceAttributeSnapshots,
                SnapshotLaneCounters = activeEffectSlotSourceSnapshotLaneCounters,
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
                ProcessTickRecords = true,
            };
            state.Dependency = tickJob.Schedule(_ownerQuery, snapshotDependency);
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASActiveEffectPreTickSystem))]
    [UpdateBefore(typeof(GAS.Runtime.GEEffectCommandCatalogNormalizeSystem))]
    public partial struct GASActiveEffectRemoveSystem : ISystem
    {
        private EntityQuery _removeCommandQuery;

        public void OnCreate(ref SystemState state)
        {
            _removeCommandQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<GERemoveCommandPendingComponent>(),
                    ComponentType.ReadWrite<GERemoveCommandBuffer>(),
                    ComponentType.ReadWrite<ASCActiveEffectsComponent>(),
                    ComponentType.ReadWrite<ActiveGameplayEffectBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectMutationBuffer>(),
                },
            });
            state.RequireForUpdate(_removeCommandQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            if (!EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity)
                || !EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var structuralEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var emptyActiveEffectSlotSourceAttributeSnapshots =
                new NativeParallelHashMap<GASGeneratedActiveEffectRuntime.ActiveEffectSlotSourceAttributeSnapshotKey, float>(
                    1,
                    state.WorldUpdateAllocator);
            var removeChunkCount = _removeCommandQuery.CalculateChunkCountWithoutFiltering();
            if (removeChunkCount <= 0)
                return;

            var activeEffectSlotSourceSnapshotLaneCounters =
                CollectionHelper.CreateNativeArray<GASGeneratedActiveEffectRuntime.ActiveEffectSlotSourceSnapshotLaneCounters>(
                    removeChunkCount,
                    state.WorldUpdateAllocator,
                    NativeArrayOptions.ClearMemory);

            state.Dependency = new GASGeneratedActiveEffectRuntime.GEActiveEffectPreTickJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                ActiveEffectsTypeHandle = SystemAPI.GetComponentTypeHandle<ASCActiveEffectsComponent>(isReadOnly: false),
                ActiveEffectSlotBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(isReadOnly: false),
                MutationBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ActiveEffectMutationBuffer>(isReadOnly: false),
                RemoveCommandBufferTypeHandle = SystemAPI.GetBufferTypeHandle<GERemoveCommandBuffer>(isReadOnly: false),
                CleanupRecordLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectCleanupRecordBuffer>(isReadOnly: false),
                SetByCallerSnapshotLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectSetByCallerValueBuffer>(isReadOnly: false),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: false),
                ActiveModifierLookup = SystemAPI.GetBufferLookup<AttributeActiveModifierBuffer>(isReadOnly: false),
                AttributeOwnerMarkerRequestLookup =
                    SystemAPI.GetBufferLookup<AttributeOwnerMarkerRequestBuffer>(isReadOnly: false),
                TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(isReadOnly: false),
                TagFixedMaskLookup = SystemAPI.GetComponentLookup<TagFixedMaskComponent>(isReadOnly: true),
                TagSourceLookup = SystemAPI.GetBufferLookup<TagTemporarySourceBuffer>(isReadOnly: false),
                AbilitySlotLookup = SystemAPI.GetBufferLookup<AbilitySlotBuffer>(isReadOnly: false),
                AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: false),
                AbilityGrantedLookup = SystemAPI.GetComponentLookup<AbilityGrantedByEffectComponent>(isReadOnly: true),
                RemovePendingTypeHandle =
                    SystemAPI.GetComponentTypeHandle<GERemoveCommandPendingComponent>(isReadOnly: false),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: false),
                CommandSetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: false),
                AbilityLifecycleRequestLookup =
                    SystemAPI.GetBufferLookup<AbilityLifecycleRequestBuffer>(isReadOnly: false),
                OwnerFactLookup = SystemAPI.GetBufferLookup<OwnerLocalGameplayFactBuffer>(isReadOnly: false),
                StructuralEcb = structuralEcb,
                ActiveEffectSlotSourceAttributeSnapshots = emptyActiveEffectSlotSourceAttributeSnapshots,
                SnapshotLaneCounters = activeEffectSlotSourceSnapshotLaneCounters,
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
                ProcessExplicitRemoveCommands = true,
            }.Schedule(_removeCommandQuery, state.Dependency);
        }
    }
}
