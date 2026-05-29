using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime
{
    public static class AttributeHelper
    {
        private static EntityManager _entityManager => GASManager.EntityManager;

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

        public static float RecalculateCurrentValue(Entity asc, int attrSetCode, int attrCode)
        {
            if (!_entityManager.Exists(asc) || !_entityManager.HasBuffer<AttributeValueBuffer>(asc))
                return 0f;

            var attributes = _entityManager.GetBuffer<AttributeValueBuffer>(asc);
            var attrIndex = attributes.IndexOfAttribute(attrSetCode, attrCode);
            if (attrIndex == -1) return 0f;

            var attr = attributes[attrIndex];
            var oldValue = attr.CurrentValue;
            attr.CurrentValue = attr.BaseValue;

            if (_entityManager.HasBuffer<AttributeActiveModifierBuffer>(asc))
            {
                var modifiers = _entityManager.GetBuffer<AttributeActiveModifierBuffer>(asc);
                foreach (var mod in modifiers)
                {
                    if (mod.AttrSetCode != attrSetCode || mod.AttributeCode != attrCode) continue;
                    attr.CurrentValue = ApplyModifier(attr.CurrentValue, mod.Op, mod.Magnitude);
                }
            }

            Clamp(ref attr);
            attr.Dirty = false;
            attr.PreviousCurrentValue = attr.CurrentValue;
            attr.CurrentValueChangePending = false;
            attributes[attrIndex] = attr;

            if (oldValue != attr.CurrentValue)
            {
                EventBusHelper.EnqueueAttributeChangeEvent(_entityManager, GASManager.EntityEventBus, new AttributeChangeEventBuffer
                {
                    ASC = asc,
                    AttrSetCode = attr.AttrSetCode,
                    AttributeCode = attr.Code,
                    OldValue = oldValue,
                    NewValue = attr.CurrentValue,
                    IsBaseValue = false,
                });
            }

            return attr.CurrentValue;
        }

        public static bool MarkCurrentValueDirty(Entity asc, int attrSetCode, int attrCode)
        {
            if (!_entityManager.Exists(asc) || !_entityManager.HasBuffer<AttributeValueBuffer>(asc))
                return false;

            var attributes = _entityManager.GetBuffer<AttributeValueBuffer>(asc);
            return MarkCurrentValueDirty(_entityManager, asc, attributes, attrSetCode, attrCode);
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
