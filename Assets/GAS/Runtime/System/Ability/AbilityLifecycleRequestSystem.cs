using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Normalizes phase-driven lifecycle transitions into explicit End/Cancel requests.
    /// Cleanup only consumes lifecycle request components.
    /// </summary>
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(AbilityStateTickSystem))]
    [UpdateBefore(typeof(AbilityStateCleanupSystem))]
    [DisableAutoCreation]
    public partial struct AbilityLifecycleRequestSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<AbilityStateComponent>()
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

            try
            {
                var scanJob = new AbilityLifecycleRequestScanJob
                {
                    EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                    StateTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityStateComponent>(isReadOnly: true),
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
                        var abilityRecord = abilityRecordReader.Read<AbilityLifecycleRequestRecord>();
                        var ability = abilityRecord.Ability;
                        if (!em.Exists(ability))
                            continue;

                        if (abilityRecord.State.Phase != EAbilityPhase.Ending)
                            continue;

                        if (AbilityRuntimeActions.IsCancelRequested(ability, em)
                            || AbilityRuntimeActions.IsEndRequested(ability, em))
                        {
                            continue;
                        }

                        AbilityRuntimeActions.RequestAbilityEnd(
                            ability,
                            em,
                            EAbilityLifecycleReason.LifetimeExpired,
                            sourceAbility: ability,
                            sourceAbilityCode: abilityRecord.State.Code);
                    }
                    abilityRecordReader.EndForEachIndex();
                }
            }
            finally
            {
                abilityRecordStream.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private struct AbilityLifecycleRequestRecord
        {
            public Entity Ability;
            public AbilityStateComponent State;
        }

        private struct AbilityLifecycleRequestScanJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<AbilityStateComponent> StateTypeHandle;
            public NativeStream.Writer AbilityRecordWriter;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                AbilityRecordWriter.BeginForEachIndex(unfilteredChunkIndex);
                var abilities = chunk.GetNativeArray(EntityTypeHandle);
                var states = chunk.GetNativeArray(ref StateTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    AbilityRecordWriter.Write(new AbilityLifecycleRequestRecord
                    {
                        Ability = abilities[entityIndex],
                        State = states[entityIndex],
                    });
                }
                AbilityRecordWriter.EndForEachIndex();
            }
        }
    }
}
