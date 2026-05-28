using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateBefore(typeof(ASCInitializeRequestSystem))]
    [UpdateBefore(typeof(ASCCommandRequestSystem))]
    [UpdateBefore(typeof(AbilityCommandRequestSystem))]
    [UpdateBefore(typeof(GEEffectCommandIngestSystem))]
    public partial struct ASCDestroyRequestSystem : ISystem
    {
        private EntityQuery _requestQuery;

        public void OnCreate(ref SystemState state)
        {
            _requestQuery = SystemAPI.QueryBuilder()
                .WithAll<ASCDestroyRequestComponent>()
                .Build();
            state.RequireForUpdate(_requestQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var requestChunkCount = _requestQuery.CalculateChunkCount();
            if (requestChunkCount <= 0)
                return;

            var em = state.EntityManager;
            var requestRecordStream = new NativeStream(requestChunkCount, Allocator.TempJob);
            var ecb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            try
            {
                var scanJob = new ASCDestroyRequestScanJob
                {
                    EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                    RequestTypeHandle = SystemAPI.GetComponentTypeHandle<ASCDestroyRequestComponent>(isReadOnly: true),
                    RequestRecordWriter = requestRecordStream.AsWriter(),
                };
                state.Dependency = scanJob.ScheduleParallel(_requestQuery, state.Dependency);
                state.Dependency.Complete();

                var requestRecordReader = requestRecordStream.AsReader();
                for (var streamIndex = 0; streamIndex < requestRecordReader.ForEachCount; streamIndex++)
                {
                    var recordCount = requestRecordReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                    {
                        var requestRecord = requestRecordReader.Read<ASCDestroyRequestRecord>();
                        if (requestRecord.Request.ASC != Entity.Null && em.Exists(requestRecord.Request.ASC))
                            ProcessDestroyRequest(em, ref ecb, requestRecord.Request.ASC);

                        ecb.DestroyEntity(requestRecord.RequestEntity);
                    }
                    requestRecordReader.EndForEachIndex();
                }

            }
            finally
            {
                requestRecordStream.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private struct ASCDestroyRequestRecord
        {
            public Entity RequestEntity;
            public ASCDestroyRequestComponent Request;
        }

        private struct ASCDestroyRequestScanJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<ASCDestroyRequestComponent> RequestTypeHandle;
            public NativeStream.Writer RequestRecordWriter;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                RequestRecordWriter.BeginForEachIndex(unfilteredChunkIndex);
                var requestEntities = chunk.GetNativeArray(EntityTypeHandle);
                var requests = chunk.GetNativeArray(ref RequestTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    RequestRecordWriter.Write(new ASCDestroyRequestRecord
                    {
                        RequestEntity = requestEntities[entityIndex],
                        Request = requests[entityIndex],
                    });
                }
                RequestRecordWriter.EndForEachIndex();
            }
        }

        private static void ProcessDestroyRequest(EntityManager em, ref EntityCommandBuffer ecb, Entity asc)
        {
            if (em.HasComponent<ASCDestroyingComponent>(asc))
                em.SetComponentEnabled<ASCDestroyingComponent>(asc, true);

            DestroyOwnedAbilities(em, ref ecb, asc);
        }

        private static void DestroyOwnedAbilities(EntityManager em, ref EntityCommandBuffer ecb, Entity asc)
        {
            if (!em.HasBuffer<AbilitySlotBuffer>(asc))
                return;

            var grantedAbilities = em.GetBuffer<AbilitySlotBuffer>(asc);
            if (grantedAbilities.Length == 0)
                return;

            var abilitySnapshot = new NativeArray<Entity>(grantedAbilities.Length, Allocator.Temp);
            try
            {
                for (var i = 0; i < grantedAbilities.Length; i++)
                    abilitySnapshot[i] = grantedAbilities[i].AbilityEntity;

                for (var i = 0; i < abilitySnapshot.Length; i++)
                {
                    var ability = abilitySnapshot[i];
                    if (!em.Exists(ability) || !em.HasComponent<AbilityStateComponent>(ability))
                        continue;

                    var baseInfo = em.GetComponentData<AbilityStateComponent>(ability);
                    if (baseInfo.Owner != asc)
                        continue;

                    RemoveAbilityFromOwner(em, asc, ability);
                    if (IsAbilityRunning(em, ability))
                    {
                        AbilityRuntimeActions.RequestAbilityCancel(
                            ability,
                            em,
                            EAbilityLifecycleReason.AscDestroy);
                        AbilityRuntimeActions.EnableDestroyOnCleanup(ability, em, ref ecb);
                    }
                    else
                    {
                        DestroyAbilityEntity(em, ref ecb, ability);
                    }
                }
            }
            finally
            {
                abilitySnapshot.Dispose();
            }
        }

        private static bool IsAbilityRunning(EntityManager em, Entity ability)
        {
            if (!em.HasComponent<AbilityStateComponent>(ability))
                return false;

            var runtime = em.GetComponentData<AbilityStateComponent>(ability);
            return runtime.Phase is EAbilityPhase.Activating or EAbilityPhase.Active or EAbilityPhase.Ending;
        }

        private static void RemoveAbilityFromOwner(EntityManager em, Entity owner, Entity ability)
        {
            if (!em.Exists(owner) || !em.HasBuffer<AbilitySlotBuffer>(owner))
                return;

            var abilities = em.GetBuffer<AbilitySlotBuffer>(owner);
            for (var i = abilities.Length - 1; i >= 0; i--)
            {
                if (abilities[i].AbilityEntity == ability)
                    abilities.RemoveAt(i);
            }
        }

        private static void DestroyAbilityEntity(EntityManager em, ref EntityCommandBuffer ecb, Entity ability)
        {
            if (!em.Exists(ability))
                return;

            ecb.DestroyEntity(ability);
        }
    }
}
