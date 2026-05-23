using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SASCCreate))]
    [UpdateBefore(typeof(SAbilityCommandRequest))]
    public partial struct SAscCommandRequest : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CAscCommandRequest>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var requests = _query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < requests.Length; i++)
            {
                var requestEntity = requests[i];
                var request = em.GetComponentData<CAscCommandRequest>(requestEntity);
                if (request.ASC != Entity.Null && em.Exists(request.ASC))
                    ProcessRequest(em, request);

                em.DestroyEntity(requestEntity);
            }

            requests.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ProcessRequest(EntityManager em, CAscCommandRequest request)
        {
            if (em.HasComponent<CAscDestroying>(request.ASC))
                return;

            switch (request.CommandType)
            {
                case EAscCommandType.SetLevel:
                    SetLevel(em, request.ASC, request.Level);
                    break;
                case EAscCommandType.AddFixedTag:
                    AddFixedTag(em, request.ASC, request.TagCode);
                    break;
                case EAscCommandType.RemoveFixedTag:
                    RemoveFixedTag(em, request.ASC, request.TagCode);
                    break;
                case EAscCommandType.SetAttributeBaseValue:
                    SetAttributeBaseValue(em, request);
                    break;
                case EAscCommandType.AddAttribute:
                    AddAttribute(em, request);
                    break;
            }
        }

        private static void SetLevel(EntityManager em, Entity asc, int level)
        {
            if (!em.HasComponent<CAscBasicData>(asc))
                em.AddComponentData(asc, new CAscBasicData());

            var data = em.GetComponentData<CAscBasicData>(asc);
            data.Level = level;
            em.SetComponentData(asc, data);
        }

        private static void AddFixedTag(EntityManager em, Entity asc, int tagCode)
        {
            if (!em.HasComponent<CTagMask>(asc))
                em.AddComponentData(asc, new CTagMask());
            if (!em.HasComponent<CFixedTagMask>(asc))
                em.AddComponentData(asc, new CFixedTagMask());
            if (!em.HasBuffer<BFixedTagSource>(asc))
                em.AddBuffer<BFixedTagSource>(asc);

            if (!TagHelper.TryGetDenseIndex(tagCode, out var sourceTagIndex))
                return;
            var tagIndices = TagHelper.ToDenseIndices(new[] { tagCode }, includeParents: true);
            if (tagIndices.Length == 0)
                return;

            var changed = false;
            var sources = em.GetBuffer<BFixedTagSource>(asc);
            for (var i = 0; i < tagIndices.Length; i++)
            {
                var tagIndex = tagIndices[i];
                if (HasFixedTagSource(sources, sourceTagIndex, tagIndex))
                    continue;

                sources.Add(new BFixedTagSource
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
            if (!em.HasBuffer<BFixedTagSource>(asc))
                return;
            if (!TagHelper.TryGetDenseIndex(tagCode, out var sourceTagIndex))
                return;

            var sources = em.GetBuffer<BFixedTagSource>(asc);
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
            DynamicBuffer<BFixedTagSource> sources,
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
            var oldMask = em.HasComponent<CTagMask>(asc)
                ? em.GetComponentData<CTagMask>(asc)
                : default;

            var fixedMask = new CFixedTagMask();
            if (em.HasBuffer<BFixedTagSource>(asc))
            {
                var sources = em.GetBuffer<BFixedTagSource>(asc);
                for (var i = 0; i < sources.Length; i++)
                    fixedMask.Mask.AddTag(sources[i].TagIndex);
            }

            var newMask = fixedMask.Mask;
            if (em.HasBuffer<BTempTagSource>(asc))
            {
                var tempSources = em.GetBuffer<BTempTagSource>(asc);
                for (var i = 0; i < tempSources.Length; i++)
                    newMask.AddTag(tempSources[i].TagIndex);
            }

            if (em.HasComponent<CFixedTagMask>(asc))
                em.SetComponentData(asc, fixedMask);
            else
                em.AddComponentData(asc, fixedMask);

            if (em.HasComponent<CTagMask>(asc))
                em.SetComponentData(asc, newMask);
            else
                em.AddComponentData(asc, newMask);

            EnqueueTagDiffEvents(em, asc, oldMask, newMask);
        }

        private static void EnqueueTagDiffEvents(EntityManager em, Entity asc, in CTagMask oldMask, in CTagMask newMask)
        {
            for (var tagIndex = 0; tagIndex < CTagMask.Capacity; tagIndex++)
            {
                var wasActive = oldMask.HasTag(tagIndex);
                var isActive = newMask.HasTag(tagIndex);
                if (wasActive == isActive)
                    continue;

                EnqueueTagChange(em, asc, tagIndex, isActive);
            }
        }

        private static void SetAttributeBaseValue(EntityManager em, CAscCommandRequest request)
        {
            if (!em.HasBuffer<BAttribute>(request.ASC))
                return;

            var attributes = em.GetBuffer<BAttribute>(request.ASC);
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

                EventBusHelper.EnqueueAttributeChangeEvent(em, GASManager.EntityEventBus, new BAttributeChangeEvent
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

        private static void AddAttribute(EntityManager em, CAscCommandRequest request)
        {
            if (!em.HasBuffer<BAttribute>(request.ASC))
                em.AddBuffer<BAttribute>(request.ASC);

            em.GetBuffer<BAttribute>(request.ASC).Add(new BAttribute
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
            EventBusHelper.EnqueueTagChangeEvent(em, GASManager.EntityEventBus, new BTagChangeEvent
            {
                ASC = asc,
                TagIndex = tagIndex,
                Added = added,
            });
        }
    }
}
