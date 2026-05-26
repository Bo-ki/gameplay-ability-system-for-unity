using Unity.Entities;

namespace GAS.Runtime
{
    public readonly struct AbilitySystemObservation
    {
        private readonly Entity _entity;

        public AbilitySystemObservation(Entity entity)
        {
            _entity = entity;
        }

        public int GetLevel()
        {
            var em = GASManager.EntityManager;
            if (!em.Exists(_entity) || !em.HasComponent<CAscBasicData>(_entity))
                return 0;
            return em.GetComponentData<CAscBasicData>(_entity).Level;
        }

        public bool TryGetLevel(out int level)
        {
            var em = GASManager.EntityManager;
            if (!em.Exists(_entity) || !em.HasComponent<CAscBasicData>(_entity))
            {
                level = 0;
                return false;
            }
            level = em.GetComponentData<CAscBasicData>(_entity).Level;
            return true;
        }

        public bool HasTag(int tag)
        {
            if (TryGetTagMask(out var mask))
                return mask.HasTag(tag);
            return false;
        }

        public bool TryGetTagMask(out CTagMask mask)
        {
            var em = GASManager.EntityManager;
            if (!em.Exists(_entity) || !em.HasComponent<CTagMask>(_entity))
            {
                mask = default;
                return false;
            }
            mask = em.GetComponentData<CTagMask>(_entity);
            return true;
        }

        public float GetAttributeValue(int attrSetCode, int attrCode)
        {
            TryGetAttributeValue(attrSetCode, attrCode, out var value);
            return value;
        }

        public bool TryGetAttributeValue(int attrSetCode, int attrCode, out float value)
        {
            return TryGetAttributeCurrentValue(attrSetCode, attrCode, out value);
        }

        public bool TryGetAttributeCurrentValue(int attrSetCode, int attrCode, out float value)
        {
            var em = GASManager.EntityManager;
            if (!em.Exists(_entity) || !em.HasBuffer<BAttribute>(_entity))
            {
                value = 0f;
                return false;
            }

            var attributes = em.GetBuffer<BAttribute>(_entity);
            for (var i = 0; i < attributes.Length; i++)
            {
                if (attributes[i].AttrSetCode == attrSetCode && attributes[i].Code == attrCode)
                {
                    value = attributes[i].CurrentValue;
                    return true;
                }
            }

            value = 0f;
            return false;
        }
    }
}
