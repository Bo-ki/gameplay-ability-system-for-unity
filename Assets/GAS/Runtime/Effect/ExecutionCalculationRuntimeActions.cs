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
            in GEContextComponent context,
            int outputKey,
            float value,
            int calculationCode = 0)
        {
            var eventBusWriter = EventBusHelper.BeginGameplayEventBatch(em, GASManager.EntityEventBus);
            try
            {
                return SetOutputValue(
                    em,
                    ge,
                    context,
                    outputKey,
                    value,
                    ref eventBusWriter,
                    calculationCode);
            }
            finally
            {
                eventBusWriter.Dispose();
            }
        }

        public static bool SetOutputValue(
            EntityManager em,
            Entity ge,
            in GEContextComponent context,
            int outputKey,
            float value,
            ref EventBusHelper.GameplayEventBusWriter eventBusWriter,
            int calculationCode = 0)
        {
            if (!WriteOutputValue(em, ge, outputKey, value))
                return false;

            EnqueueOutputUpdatedFact(
                ref eventBusWriter,
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

            if (!em.HasBuffer<GEExecutionCalculationValueBuffer>(ge))
                return false;

            var values = em.GetBuffer<GEExecutionCalculationValueBuffer>(ge);

            return WriteOutputValue(values, outputKey, value);
        }

        public static bool TryGetOutputValue(
            DynamicBuffer<GEExecutionCalculationValueBuffer> values,
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
            in GEContextComponent context,
            int eventCode,
            float value)
        {
            var eventBusWriter = EventBusHelper.BeginGameplayEventBatch(em, GASManager.EntityEventBus);
            try
            {
                EnqueueOutputUpdatedFact(
                    ref eventBusWriter,
                    ge,
                    context,
                    eventCode,
                    value);
            }
            finally
            {
                eventBusWriter.Dispose();
            }
        }

        public static void EnqueueOutputUpdatedFact(
            ref EventBusHelper.GameplayEventBusWriter eventBusWriter,
            Entity ge,
            in GEContextComponent context,
            int eventCode,
            float value)
        {
            EnqueueExecutionFact(
                ref eventBusWriter,
                ge,
                context,
                EGameplayEventType.ExecutionCalculationOutputUpdated,
                eventCode,
                value);
        }

        public static bool WriteOutputValue(
            DynamicBuffer<GEExecutionCalculationValueBuffer> values,
            int outputKey,
            float value)
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i].Key != outputKey)
                    continue;

                if (values[i].Value == value)
                    return false;

                values[i] = new GEExecutionCalculationValueBuffer
                {
                    Key = outputKey,
                    Value = value,
                };
                return true;
            }

            values.Add(new GEExecutionCalculationValueBuffer
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
            ref EventBusHelper.GameplayEventBusWriter eventBusWriter,
            Entity ge,
            in GEContextComponent context,
            EGameplayEventType type,
            int eventCode,
            float value)
        {
            if (!eventBusWriter.IsCreated)
                return;

            eventBusWriter.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
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
