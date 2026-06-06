using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime
{
    public static class AttributeHelper
    {
        public static int IndexOfAttribute(this DynamicBuffer<AttributeValueBuffer> attributes, int attrSetCode, int attrCode)
        {
            for (var i = 0; i < attributes.Length; i++)
            {
                var attr = attributes[i];
                if (attr.AttrSetCode == attrSetCode && attr.Code == attrCode)
                    return i;
            }

            return -1;
        }

        public static bool MarkCurrentValueDirty(
            EntityManager entityManager,
            Entity asc,
            DynamicBuffer<AttributeValueBuffer> attributes,
            int attrSetCode,
            int attrCode)
        {
            if (!MarkCurrentValueDirty(attributes, attrSetCode, attrCode))
                return false;

            MarkOwnerDirty(entityManager, asc);
            return true;
        }

        public static bool MarkCurrentValueDirty(DynamicBuffer<AttributeValueBuffer> attributes, int attrSetCode, int attrCode)
        {
            var attrIndex = attributes.IndexOfAttribute(attrSetCode, attrCode);
            if (attrIndex == -1) return false;

            var attr = attributes[attrIndex];
            attr.Dirty = true;
            attributes[attrIndex] = attr;
            return true;
        }

        public static void MarkOwnerDirty(EntityManager entityManager, Entity asc)
        {
            if (asc == Entity.Null
                || !entityManager.Exists(asc)
                || !entityManager.HasComponent<AttributeDirtyComponent>(asc)
                || entityManager.IsComponentEnabled<AttributeDirtyComponent>(asc))
            {
                return;
            }

            entityManager.SetComponentEnabled<AttributeDirtyComponent>(asc, true);
        }

        public static void MarkOwnerChangeEventPending(EntityManager entityManager, Entity asc)
        {
            if (asc == Entity.Null
                || !entityManager.Exists(asc)
                || !entityManager.HasComponent<AttributeChangeEventPendingComponent>(asc)
                || entityManager.IsComponentEnabled<AttributeChangeEventPendingComponent>(asc))
            {
                return;
            }

            entityManager.SetComponentEnabled<AttributeChangeEventPendingComponent>(asc, true);
        }

        public static void MarkDirectCurrentValueChanged(
            EntityManager entityManager,
            Entity asc,
            ref AttributeValueBuffer attribute)
        {
            if (OwnerHasActiveModifiers(entityManager, asc))
            {
                attribute.Dirty = true;
                MarkOwnerDirty(entityManager, asc);
                return;
            }

            attribute.Dirty = false;
            MarkOwnerChangeEventPending(entityManager, asc);
        }

        private static bool OwnerHasActiveModifiers(EntityManager entityManager, Entity asc)
        {
            if (asc == Entity.Null
                || !entityManager.Exists(asc)
                || !entityManager.HasBuffer<AttributeActiveModifierBuffer>(asc))
            {
                return false;
            }

            return entityManager.HasComponent<AttributeActiveModifierPresentComponent>(asc)
                   && entityManager.IsComponentEnabled<AttributeActiveModifierPresentComponent>(asc);
        }

        public static void MarkActiveModifierAdded(EntityManager entityManager, Entity asc)
        {
            if (asc == Entity.Null
                || !entityManager.Exists(asc)
                || !entityManager.HasComponent<AttributeActiveModifierPresentComponent>(asc)
                || entityManager.IsComponentEnabled<AttributeActiveModifierPresentComponent>(asc))
            {
                return;
            }

            entityManager.SetComponentEnabled<AttributeActiveModifierPresentComponent>(asc, true);
        }

        public static void RefreshActiveModifierPresence(
            EntityManager entityManager,
            Entity asc,
            DynamicBuffer<AttributeActiveModifierBuffer> modifiers)
        {
            if (asc == Entity.Null
                || !entityManager.Exists(asc)
                || !entityManager.HasComponent<AttributeActiveModifierPresentComponent>(asc))
            {
                return;
            }

            entityManager.SetComponentEnabled<AttributeActiveModifierPresentComponent>(asc, modifiers.Length > 0);
        }

        public static void Clamp(ref AttributeValueBuffer attribute)
        {
            if (attribute.IsClampMin) attribute.CurrentValue = math.max(attribute.CurrentValue, attribute.MinValue);
            if (attribute.IsClampMax) attribute.CurrentValue = math.min(attribute.CurrentValue, attribute.MaxValue);
        }

        public static float ApplyModifier(float currentValue, EModifierOp op, float magnitude)
        {
            return op switch
            {
                EModifierOp.Add => currentValue + magnitude,
                EModifierOp.Subtract => currentValue - magnitude,
                EModifierOp.Multiply => currentValue * magnitude,
                EModifierOp.Divide => currentValue / magnitude,
                EModifierOp.Override => magnitude,
                _ => currentValue,
            };
        }
    }
}
