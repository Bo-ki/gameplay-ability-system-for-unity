using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
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
            if (!GASDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
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
                        if (GASRuntimeDefinitionResolver.TryNormalizeGameplayEffectCommand(ref catalog, ref command))
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
}
