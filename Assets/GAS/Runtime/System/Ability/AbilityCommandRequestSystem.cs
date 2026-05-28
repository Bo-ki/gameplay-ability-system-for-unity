using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateBefore(typeof(AbilityTryActivateSystem))]
    public partial struct AbilityCommandRequestSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<AbilityCommandRequestComponent>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var requestChunkCount = _query.CalculateChunkCount();
            if (requestChunkCount <= 0)
                return;

            var em = state.EntityManager;
            var requestRecordStream = new NativeStream(requestChunkCount, Allocator.TempJob);
            var requestRecords = new NativeList<AbilityCommandRequestRecord>(Allocator.Temp);
            var ecb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var catalog = SystemAPI.TryGetSingleton<GASDefinitionCatalogComponent>(out var catalogComponent)
                ? catalogComponent.Catalog
                : default;

            try
            {
                var scanJob = new AbilityCommandRequestScanJob
                {
                    EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                    RequestTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityCommandRequestComponent>(isReadOnly: true),
                    RequestRecordWriter = requestRecordStream.AsWriter(),
                };
                state.Dependency = scanJob.ScheduleParallel(_query, state.Dependency);
                state.Dependency.Complete();

                var requestRecordReader = requestRecordStream.AsReader();
                for (var streamIndex = 0; streamIndex < requestRecordReader.ForEachCount; streamIndex++)
                {
                    var recordCount = requestRecordReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                        requestRecords.Add(requestRecordReader.Read<AbilityCommandRequestRecord>());
                    requestRecordReader.EndForEachIndex();
                }

                for (var i = 0; i < requestRecords.Length; i++)
                {
                    var request = requestRecords[i].Request;
                    if (request.CommandType == EAbilityCommandType.Grant
                        && request.Owner != Entity.Null
                        && em.Exists(request.Owner))
                    {
                        ProcessGrantRequest(em, ref ecb, request, catalog);
                    }
                }

                for (var i = 0; i < requestRecords.Length; i++)
                {
                    var requestRecord = requestRecords[i];
                    var request = requestRecord.Request;
                    if (request.Owner != Entity.Null && em.Exists(request.Owner))
                        ProcessRuntimeRequest(em, ref ecb, request);

                    ecb.DestroyEntity(requestRecord.RequestEntity);
                }
            }
            finally
            {
                requestRecords.Dispose();
                requestRecordStream.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private struct AbilityCommandRequestRecord
        {
            public Entity RequestEntity;
            public AbilityCommandRequestComponent Request;
        }

        private struct AbilityCommandRequestScanJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<AbilityCommandRequestComponent> RequestTypeHandle;
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
                    RequestRecordWriter.Write(new AbilityCommandRequestRecord
                    {
                        RequestEntity = requestEntities[entityIndex],
                        Request = requests[entityIndex],
                    });
                }
                RequestRecordWriter.EndForEachIndex();
            }
        }

        private static void ProcessGrantRequest(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            AbilityCommandRequestComponent request,
            BlobAssetReference<GASDefinitionCatalogBlob> catalog)
        {
            if (!ASCEntityFactory.HasASCRuntimeCoreComponents(em, request.Owner))
                return;
            if (ASCEntityFactory.IsDestroying(em, request.Owner))
                return;

            var grantedAbilities = em.GetBuffer<AbilitySlotBuffer>(request.Owner);
            if (request.AbilityCode > 0 && HasGrantedAbility(em, grantedAbilities, Entity.Null, request.AbilityCode))
                return;

            var abilityEntity = request.AbilityEntity;
            if ((abilityEntity == Entity.Null || !em.Exists(abilityEntity)) && request.AbilityCode > 0)
                abilityEntity = CreateAbilityEntity(em, ref ecb, request.Owner, request.AbilityCode, catalog);

            if (abilityEntity == Entity.Null)
                return;

            if (em.Exists(abilityEntity) && em.HasComponent<AbilityStateComponent>(abilityEntity))
            {
                var info = em.GetComponentData<AbilityStateComponent>(abilityEntity);
                info.Owner = request.Owner;
                em.SetComponentData(abilityEntity, info);
            }

            if (HasGrantedAbility(em, grantedAbilities, abilityEntity, request.AbilityCode))
                return;

            ecb.AppendToBuffer(request.Owner, new AbilitySlotBuffer
            {
                AbilityEntity = abilityEntity,
            });
        }

        private static Entity CreateAbilityEntity(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity owner,
            int abilityCode,
            BlobAssetReference<GASDefinitionCatalogBlob> catalogReference)
        {
            if (!catalogReference.IsCreated)
                return Entity.Null;

            ref var catalog = ref catalogReference.Value;
            if (!TryGetAbilityDefinition(ref catalog, abilityCode, out var abilityDefinition))
                return Entity.Null;

            var ability = ecb.CreateEntity(GASRuntimeEntityArchetypes.Ability(em));
            GASRuntimeEntityArchetypes.InitializeAbilityEntity(ecb, ability);
            ecb.SetName(ability, $"Ability_{abilityDefinition.AbilityCode}");
            ecb.SetComponent(
                ability,
                AbilityStateComponent.Create(
                    abilityDefinition.AbilityCode,
                    abilityDefinition.Level,
                    owner));
            ecb.SetComponent(ability, new AbilityMainTargetComponent { TargetAsc = Entity.Null });

            return ability;
        }

        private static bool TryGetAbilityDefinition(
            ref GASDefinitionCatalogBlob catalog,
            int abilityCode,
            out GASCatalogAbilityDefinitionBlob abilityDefinition)
        {
            for (var i = 0; i < catalog.Abilities.Length; i++)
            {
                var candidate = catalog.Abilities[i];
                if (candidate.AbilityCode != abilityCode)
                    continue;

                abilityDefinition = candidate;
                return true;
            }

            abilityDefinition = default;
            return false;
        }

        private static void ProcessRuntimeRequest(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            AbilityCommandRequestComponent request)
        {
            if (request.CommandType == EAbilityCommandType.Grant)
                return;
            if (!ASCEntityFactory.HasASCRuntimeCoreComponents(em, request.Owner))
                return;
            if (ASCEntityFactory.IsDestroying(em, request.Owner))
                return;

            var ability = FindAbility(em, request.Owner, request.AbilityCode);
            if (ability == Entity.Null)
                return;

            switch (request.CommandType)
            {
                case EAbilityCommandType.Activate:
                    SetMainTarget(em, ref ecb, ability, request.TargetAsc);
                    EnableMarker<AbilityActivationPendingComponent>(em, ref ecb, ability);
                    break;
                case EAbilityCommandType.End:
                    AbilityRuntimeActions.RequestAbilityEnd(
                        ability,
                        em,
                        EAbilityLifecycleReason.ExplicitEnd);
                    break;
                case EAbilityCommandType.Cancel:
                    AbilityRuntimeActions.RequestAbilityCancel(
                        ability,
                        em,
                        EAbilityLifecycleReason.ExplicitCancel);
                    break;
                case EAbilityCommandType.Remove:
                    RemoveAbility(em, ref ecb, request.Owner, ability);
                    break;
            }
        }

        private static Entity FindAbility(EntityManager em, Entity owner, int abilityCode)
        {
            if (abilityCode <= 0 || !em.HasBuffer<AbilitySlotBuffer>(owner))
                return Entity.Null;

            var abilities = em.GetBuffer<AbilitySlotBuffer>(owner);
            for (var i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i].AbilityEntity;
                if (!em.Exists(ability) || !em.HasComponent<AbilityStateComponent>(ability))
                    continue;

                var baseInfo = em.GetComponentData<AbilityStateComponent>(ability);
                if (baseInfo.Code == abilityCode)
                    return ability;
            }

            return Entity.Null;
        }

        private static bool HasGrantedAbility(
            EntityManager em,
            DynamicBuffer<AbilitySlotBuffer> abilities,
            Entity abilityEntity,
            int abilityCode)
        {
            for (var i = 0; i < abilities.Length; i++)
            {
                var granted = abilities[i].AbilityEntity;
                if (granted == abilityEntity)
                    return true;
                if (abilityCode <= 0
                    || !em.Exists(granted)
                    || !em.HasComponent<AbilityStateComponent>(granted))
                {
                    continue;
                }

                if (em.GetComponentData<AbilityStateComponent>(granted).Code == abilityCode)
                    return true;
            }

            return false;
        }

        private static void SetMainTarget(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ability,
            Entity targetAsc)
        {
            if (targetAsc == Entity.Null || !em.Exists(targetAsc) || ASCEntityFactory.IsDestroying(em, targetAsc))
            {
                if (em.HasComponent<AbilityMainTargetComponent>(ability))
                    em.SetComponentData(ability, new AbilityMainTargetComponent { TargetAsc = Entity.Null });
                return;
            }

            var target = new AbilityMainTargetComponent
            {
                TargetAsc = targetAsc,
            };

            if (em.HasComponent<AbilityMainTargetComponent>(ability))
                em.SetComponentData(ability, target);
        }

        private static void RemoveAbility(EntityManager em, ref EntityCommandBuffer ecb, Entity owner, Entity ability)
        {
            RemoveAbilityFromOwner(em, owner, ability);

            if (IsAbilityRunning(em, ability))
            {
                AbilityRuntimeActions.RequestAbilityCancel(
                        ability,
                        em,
                        EAbilityLifecycleReason.RemoveAbility);
                AbilityRuntimeActions.EnableDestroyOnCleanup(ability, em, ref ecb);
                return;
            }

            DestroyAbilityEntity(em, ref ecb, ability);
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

        private static void EnableMarker<T>(EntityManager em, ref EntityCommandBuffer ecb, Entity entity)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            if (em.HasComponent<T>(entity))
                em.SetComponentEnabled<T>(entity, true);
        }

        private static void DisableMarker<T>(ref EntityCommandBuffer ecb, Entity entity)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            ecb.SetComponentEnabled<T>(entity, false);
        }
    }
}
