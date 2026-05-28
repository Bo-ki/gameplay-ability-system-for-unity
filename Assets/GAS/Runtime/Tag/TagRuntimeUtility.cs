using Unity.Entities;

namespace GAS.Runtime
{
    internal static class TagRuntimeUtility
    {
        public static void RemoveTagIndexFromEffectiveMaskIfUnreferenced(
            EntityManager entityManager,
            Entity asc,
            int tagIndex)
        {
            if (!entityManager.Exists(asc) || !entityManager.HasComponent<TagMaskComponent>(asc))
                return;

            if (HasFixedTagIndex(entityManager, asc, tagIndex)
                || HasAnyTemporarySourceForTag(entityManager, asc, tagIndex))
                return;

            var mask = entityManager.GetComponentData<TagMaskComponent>(asc);
            mask.RemoveTag(tagIndex);
            entityManager.SetComponentData(asc, mask);
        }

        private static bool HasFixedTagIndex(EntityManager entityManager, Entity asc, int tagIndex)
        {
            return entityManager.Exists(asc)
                   && entityManager.HasComponent<TagFixedMaskComponent>(asc)
                   && entityManager.GetComponentData<TagFixedMaskComponent>(asc).Mask.HasTag(tagIndex);
        }

        private static bool HasAnyTemporarySourceForTag(EntityManager entityManager, Entity asc, int tagIndex)
        {
            if (!entityManager.Exists(asc) || !entityManager.HasBuffer<TagTemporarySourceBuffer>(asc))
                return false;

            var temporaryTags = entityManager.GetBuffer<TagTemporarySourceBuffer>(asc);
            for (var i = 0; i < temporaryTags.Length; i++)
                if (temporaryTags[i].TagIndex == tagIndex)
                    return true;
            return false;
        }
    }
}
