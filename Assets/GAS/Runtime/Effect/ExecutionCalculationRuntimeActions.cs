using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Shared write helpers for ExecutionCalculation producers.
    /// Custom ECS execution systems should write output values through this contract.
    /// </summary>
    public static class ExecutionCalculationRuntimeActions
    {
        public static bool SetOutputValue(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            int outputKey,
            float value,
            int calculationCode = 0)
        {
            if (!WriteOutputValue(em, ge, outputKey, value))
                return false;

            EnqueueOutputUpdatedFact(
                em,
                ge,
                context,
                ResolveEventCode(calculationCode, outputKey),
                value);
            return true;
        }

        public static bool WriteOutputValue(
            EntityManager em,
            Entity ge,
            int outputKey,
            float value)
        {
            if (ge == Entity.Null || !em.Exists(ge))
                return false;

            var values = em.HasBuffer<BExecutionCalculationValue>(ge)
                ? em.GetBuffer<BExecutionCalculationValue>(ge)
                : em.AddBuffer<BExecutionCalculationValue>(ge);

            return WriteOutputValue(values, outputKey, value);
        }

        public static bool TryGetOutputValue(
            DynamicBuffer<BExecutionCalculationValue> values,
            int outputKey,
            out float value)
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i].Key != outputKey)
                    continue;

                value = values[i].Value;
                return true;
            }

            value = 0f;
            return false;
        }

        public static void EnqueueOutputUpdatedFact(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            int eventCode,
            float value)
        {
            EnqueueExecutionFact(
                em,
                ge,
                context,
                EGameplayEventType.ExecutionCalculationOutputUpdated,
                eventCode,
                value);
        }

        public static bool WriteOutputValue(
            DynamicBuffer<BExecutionCalculationValue> values,
            int outputKey,
            float value)
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i].Key != outputKey)
                    continue;

                if (values[i].Value == value)
                    return false;

                values[i] = new BExecutionCalculationValue
                {
                    Key = outputKey,
                    Value = value,
                };
                return true;
            }

            values.Add(new BExecutionCalculationValue
            {
                Key = outputKey,
                Value = value,
            });
            return true;
        }

        private static int ResolveEventCode(int calculationCode, int outputKey)
        {
            return calculationCode != 0 ? calculationCode : outputKey;
        }

        private static void EnqueueExecutionFact(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            EGameplayEventType type,
            int eventCode,
            float value)
        {
            EventBusHelper.EnqueueGameplayEvent(em, GASManager.EntityEventBus, new BGameplayEvent
            {
                Type = type,
                SourceAsc = context.SourceAsc,
                TargetAsc = context.TargetAsc,
                SourceAbility = context.SourceAbility,
                GameplayEffect = ge,
                ContextId = context.ContextId,
                EventCode = eventCode,
                Value = value,
            });
        }
    }
}
