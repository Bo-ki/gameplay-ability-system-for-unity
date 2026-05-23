using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime
{
    public static class AttributeHelper
    {
        private static EntityManager _entityManager => GASManager.EntityManager;

        public static int IndexOfAttribute(this DynamicBuffer<BAttribute> attributes, int attrSetCode, int attrCode)
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
            if (!_entityManager.Exists(asc) || !_entityManager.HasBuffer<BAttribute>(asc))
                return 0f;

            var attributes = _entityManager.GetBuffer<BAttribute>(asc);
            var attrIndex = attributes.IndexOfAttribute(attrSetCode, attrCode);
            if (attrIndex == -1) return 0f;

            var attr = attributes[attrIndex];
            var oldValue = attr.CurrentValue;
            attr.CurrentValue = attr.BaseValue;

            if (_entityManager.HasBuffer<BActiveModifier>(asc))
            {
                var modifiers = _entityManager.GetBuffer<BActiveModifier>(asc);
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
                EventBusHelper.EnqueueAttributeChangeEvent(_entityManager, GASManager.EntityEventBus, new BAttributeChangeEvent
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
            if (!_entityManager.Exists(asc) || !_entityManager.HasBuffer<BAttribute>(asc))
                return false;

            var attributes = _entityManager.GetBuffer<BAttribute>(asc);
            return MarkCurrentValueDirty(attributes, attrSetCode, attrCode);
        }

        public static bool MarkCurrentValueDirty(DynamicBuffer<BAttribute> attributes, int attrSetCode, int attrCode)
        {
            var attrIndex = attributes.IndexOfAttribute(attrSetCode, attrCode);
            if (attrIndex == -1) return false;

            var attr = attributes[attrIndex];
            attr.Dirty = true;
            attributes[attrIndex] = attr;
            return true;
        }

        public static void Clamp(ref BAttribute attribute)
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
