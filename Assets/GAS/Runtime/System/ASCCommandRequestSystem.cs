using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateAfter(typeof(ASCEntityCreateSystem))]
    [UpdateBefore(typeof(AbilityCommandRequestSystem))]
    public partial struct ASCCommandRequestSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<ASCCommandRequestComponent>()
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
            var ecb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            try
            {
                var scanJob = new ASCCommandRequestScanJob
                {
                    EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                    RequestTypeHandle = SystemAPI.GetComponentTypeHandle<ASCCommandRequestComponent>(isReadOnly: true),
                    RequestRecordWriter = requestRecordStream.AsWriter(),
                };
                state.Dependency = scanJob.ScheduleParallel(_query, state.Dependency);
                state.Dependency.Complete();

                var requestRecordReader = requestRecordStream.AsReader();
                for (var streamIndex = 0; streamIndex < requestRecordReader.ForEachCount; streamIndex++)
                {
                    var recordCount = requestRecordReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                    {
                        var requestRecord = requestRecordReader.Read<ASCCommandRequestRecord>();
                        if (requestRecord.Request.ASC != Entity.Null && em.Exists(requestRecord.Request.ASC))
                            ProcessRequest(em, requestRecord.Request);

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

        private struct ASCCommandRequestRecord
        {
            public Entity RequestEntity;
            public ASCCommandRequestComponent Request;
        }

        private struct ASCCommandRequestScanJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<ASCCommandRequestComponent> RequestTypeHandle;
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
                    RequestRecordWriter.Write(new ASCCommandRequestRecord
                    {
                        RequestEntity = requestEntities[entityIndex],
                        Request = requests[entityIndex],
                    });
                }
                RequestRecordWriter.EndForEachIndex();
            }
        }

        private static void ProcessRequest(EntityManager em, ASCCommandRequestComponent request)
        {
            if (!ASCEntityFactory.HasASCRuntimeCoreComponents(em, request.ASC))
                return;
            if (ASCEntityFactory.IsDestroying(em, request.ASC))
                return;

            switch (request.CommandType)
            {
                case ASCCommandType.SetLevel:
                    SetLevel(em, request.ASC, request.Level);
                    break;
                case ASCCommandType.AddFixedTag:
                    AddFixedTag(em, request.ASC, request.TagCode);
                    break;
                case ASCCommandType.RemoveFixedTag:
                    RemoveFixedTag(em, request.ASC, request.TagCode);
                    break;
                case ASCCommandType.SetAttributeBaseValue:
                    SetAttributeBaseValue(em, request);
                    break;
                case ASCCommandType.AddAttribute:
                    AddAttribute(em, request);
                    break;
            }
        }

        private static void SetLevel(EntityManager em, Entity asc, int level)
        {
            var data = em.GetComponentData<ASCIdentityComponent>(asc);
            data.Level = level;
            em.SetComponentData(asc, data);
        }

        private static void AddFixedTag(EntityManager em, Entity asc, int tagCode)
        {
            if (!TagHelper.TryGetDenseIndex(tagCode, out var sourceTagIndex))
                return;
            var tagIndices = TagHelper.ToDenseIndices(new[] { tagCode }, includeParents: true);
            if (tagIndices.Length == 0)
                return;

            var changed = false;
            var sources = em.GetBuffer<TagFixedSourceBuffer>(asc);
            for (var i = 0; i < tagIndices.Length; i++)
            {
                var tagIndex = tagIndices[i];
                if (HasFixedTagSource(sources, sourceTagIndex, tagIndex))
                    continue;

                sources.Add(new TagFixedSourceBuffer
                {
                    SourceTagIndex = sourceTagIndex,
                    TagIndex = tagIndex,
                });
                changed = true;
            }

            if (changed)
                RebuildTagMasks(em, asc);
        }

        private static void RemoveFixedTag(EntityManager em, Entity asc, int tagCode)
        {
            if (!em.HasBuffer<TagFixedSourceBuffer>(asc))
                return;
            if (!TagHelper.TryGetDenseIndex(tagCode, out var sourceTagIndex))
                return;

            var sources = em.GetBuffer<TagFixedSourceBuffer>(asc);
            var removed = false;
            for (var i = sources.Length - 1; i >= 0; i--)
            {
                if (sources[i].SourceTagIndex != sourceTagIndex)
                    continue;

                sources.RemoveAt(i);
                removed = true;
            }

            if (removed)
                RebuildTagMasks(em, asc);
        }

        private static bool HasFixedTagSource(
            DynamicBuffer<TagFixedSourceBuffer> sources,
            int sourceTagIndex,
            int tagIndex)
        {
            for (var i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source.SourceTagIndex == sourceTagIndex && source.TagIndex == tagIndex)
                    return true;
            }

            return false;
        }

        private static void RebuildTagMasks(EntityManager em, Entity asc)
        {
            var oldMask = em.HasComponent<TagMaskComponent>(asc)
                ? em.GetComponentData<TagMaskComponent>(asc)
                : default;

            var fixedMask = new TagFixedMaskComponent();
            if (em.HasBuffer<TagFixedSourceBuffer>(asc))
            {
                var sources = em.GetBuffer<TagFixedSourceBuffer>(asc);
                for (var i = 0; i < sources.Length; i++)
                    fixedMask.Mask.AddTag(sources[i].TagIndex);
            }

            var newMask = fixedMask.Mask;
            if (em.HasBuffer<TagTemporarySourceBuffer>(asc))
            {
                var tempSources = em.GetBuffer<TagTemporarySourceBuffer>(asc);
                for (var i = 0; i < tempSources.Length; i++)
                    newMask.AddTag(tempSources[i].TagIndex);
            }

            em.SetComponentData(asc, fixedMask);
            em.SetComponentData(asc, newMask);

            EnqueueTagDiffEvents(em, asc, oldMask, newMask);
        }

        private static void EnqueueTagDiffEvents(EntityManager em, Entity asc, in TagMaskComponent oldMask, in TagMaskComponent newMask)
        {
            for (var tagIndex = 0; tagIndex < TagMaskComponent.Capacity; tagIndex++)
            {
                var wasActive = oldMask.HasTag(tagIndex);
                var isActive = newMask.HasTag(tagIndex);
                if (wasActive == isActive)
                    continue;

                EnqueueTagChange(em, asc, tagIndex, isActive);
            }
        }

        private static void SetAttributeBaseValue(EntityManager em, ASCCommandRequestComponent request)
        {
            if (!em.HasBuffer<AttributeValueBuffer>(request.ASC))
                return;

            var attributes = em.GetBuffer<AttributeValueBuffer>(request.ASC);
            for (var i = 0; i < attributes.Length; i++)
            {
                if (attributes[i].AttrSetCode != request.AttrSetCode
                    || attributes[i].Code != request.AttributeCode)
                    continue;

                var attr = attributes[i];
                var oldValue = attr.BaseValue;
                var oldCurrentValue = attr.CurrentValue;
                attr.BaseValue = request.AttributeValue;
                attr.CurrentValue = attr.BaseValue;
                attr.Dirty = true;
                if (oldCurrentValue != attr.CurrentValue)
                {
                    attr.PreviousCurrentValue = oldCurrentValue;
                    attr.CurrentValueChangePending = true;
                }
                attributes[i] = attr;

                EventBusHelper.EnqueueAttributeChangeEvent(em, GASManager.EntityEventBus, new AttributeChangeEventBuffer
                {
                    ASC = request.ASC,
                    SourceAsc = request.ASC,
                    AttrSetCode = request.AttrSetCode,
                    AttributeCode = request.AttributeCode,
                    OldValue = oldValue,
                    NewValue = attr.BaseValue,
                    IsBaseValue = true,
                });
                return;
            }
        }

        private static void AddAttribute(EntityManager em, ASCCommandRequestComponent request)
        {
            em.GetBuffer<AttributeValueBuffer>(request.ASC).Add(new AttributeValueBuffer
            {
                AttrSetCode = request.AttrSetCode,
                Code = request.AttributeCode,
                BaseValue = request.AttributeValue,
                CurrentValue = request.AttributeValue,
                PreviousCurrentValue = request.AttributeValue,
                IsClampMin = request.IsClampMin,
                IsClampMax = request.IsClampMax,
                MinValue = request.MinValue,
                MaxValue = request.MaxValue,
                Dirty = true,
            });
        }

        private static void EnqueueTagChange(EntityManager em, Entity asc, int tagIndex, bool added)
        {
            EventBusHelper.EnqueueTagChangeEvent(em, GASManager.EntityEventBus, new TagChangeEventBuffer
            {
                ASC = asc,
                TagIndex = tagIndex,
                Added = added,
            });
        }
    }
}
