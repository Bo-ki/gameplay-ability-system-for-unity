using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Converts try-activate markers into explicit commit requests.
    /// The gameplay commit gate lives in AbilityCommitSystem.
    /// </summary>
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateBefore(typeof(AbilityCommitSystem))]
    [BurstCompile]
    public partial struct AbilityTryActivateSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<AbilityActivationPendingComponent, AbilityStateComponent>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var abilityChunkCount = _query.CalculateChunkCount();
            if (abilityChunkCount <= 0)
                return;

            var em = state.EntityManager;
            var abilityRecordStream = new NativeStream(abilityChunkCount, Allocator.TempJob);
            var ecb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            try
            {
                var scanJob = new AbilityTryActivateScanJob
                {
                    EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                    AbilityRecordWriter = abilityRecordStream.AsWriter(),
                };
                state.Dependency = scanJob.ScheduleParallel(_query, state.Dependency);
                state.Dependency.Complete();

                var abilityRecordReader = abilityRecordStream.AsReader();
                for (var streamIndex = 0; streamIndex < abilityRecordReader.ForEachCount; streamIndex++)
                {
                    var recordCount = abilityRecordReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                    {
                        var record = abilityRecordReader.Read<AbilityTryActivateRecord>();
                        var ability = record.Ability;
                        if (!em.Exists(ability))
                            continue;

                        if (em.HasComponent<AbilityCommitRequestComponent>(ability))
                            em.SetComponentEnabled<AbilityCommitRequestComponent>(ability, true);

                        if (em.HasComponent<AbilityActivationPendingComponent>(ability))
                            em.SetComponentEnabled<AbilityActivationPendingComponent>(ability, false);
                    }
                    abilityRecordReader.EndForEachIndex();
                }
            }
            finally
            {
                abilityRecordStream.Dispose();
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        private struct AbilityTryActivateRecord
        {
            public Entity Ability;
        }

        private struct AbilityTryActivateScanJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public NativeStream.Writer AbilityRecordWriter;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                AbilityRecordWriter.BeginForEachIndex(unfilteredChunkIndex);
                var abilities = chunk.GetNativeArray(EntityTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    AbilityRecordWriter.Write(new AbilityTryActivateRecord
                    {
                        Ability = abilities[entityIndex],
                    });
                }
                AbilityRecordWriter.EndForEachIndex();
            }
        }
    }
}
