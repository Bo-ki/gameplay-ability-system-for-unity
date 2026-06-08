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
            var hasFactWriter = TryBeginOwnerLocalFactWriter(em, out var factWriter);
            var changed = SetOutputValue(
                em,
                ge,
                context,
                outputKey,
                value,
                ref factWriter,
                calculationCode);
            if (hasFactWriter)
                factWriter.Flush();
            return changed;
        }

        public static bool SetOutputValue(
            EntityManager em,
            Entity ge,
            in GEContextComponent context,
            int outputKey,
            float value,
            ref EffectCommandSpecStream.OwnerLocalFactWriter factWriter,
            int calculationCode = 0)
        {
            if (!WriteOutputValue(em, ge, outputKey, value))
                return false;

            EnqueueOutputUpdatedFact(
                ref factWriter,
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
            if (!TryBeginOwnerLocalFactWriter(em, out var factWriter))
                return;

            EnqueueOutputUpdatedFact(
                ref factWriter,
                ge,
                context,
                eventCode,
                value);
            factWriter.Flush();
        }

        public static void EnqueueOutputUpdatedFact(
            ref EffectCommandSpecStream.OwnerLocalFactWriter factWriter,
            Entity ge,
            in GEContextComponent context,
            int eventCode,
            float value)
        {
            EnqueueExecutionFact(
                ref factWriter,
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
            ref EffectCommandSpecStream.OwnerLocalFactWriter factWriter,
            Entity ge,
            in GEContextComponent context,
            EGameplayEventType type,
            int eventCode,
            float value)
        {
            if (!factWriter.IsCreated)
                return;

            factWriter.AppendFact(new GameplayEventBuffer
            {
                EventType = type,
                Domain = EGameplayFactDomain.ExecutionCalculation,
                Category = EGameplayFactCategory.StateChange,
                Severity = EGameplayFactSeverity.Info,
                SourceAsc = context.SourceAsc,
                TargetAsc = context.TargetAsc,
                SourceAbility = context.SourceAbility,
                SourceEffect = ge,
                ContextId = context.ContextId,
                EventCode = eventCode,
                Value = value,
            });
        }

        private static bool TryBeginOwnerLocalFactWriter(
            EntityManager em,
            out EffectCommandSpecStream.OwnerLocalFactWriter factWriter)
        {
            factWriter = default;
            if (!EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity))
                return false;

            factWriter = EffectCommandSpecStream.BeginOwnerLocalFactWriter(
                em,
                streamEntity,
                GASRuntimeFrameContext.ResolveCurrentFrame(em));
            return factWriter.IsCreated;
        }
    }
}
