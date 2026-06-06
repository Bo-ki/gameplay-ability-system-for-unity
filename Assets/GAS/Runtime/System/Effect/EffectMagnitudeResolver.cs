using Unity.Entities;

namespace GAS.Runtime
{
    internal static class EffectMagnitudeResolver
    {
        public static void ResolveModifiers(
            EntityManager em,
            Entity ge,
            in GEContextComponent context,
            in GEEffectSpecComponent spec)
        {
            if (!em.IsComponentEnabled<GEModifierConfigBuffer>(ge))
            {
                if (em.HasBuffer<GEResolvedModifierBuffer>(ge))
                    em.GetBuffer<GEResolvedModifierBuffer>(ge).Clear();

                return;
            }

            if (!TryGetResolvedModifierBuffer(em, ge, out var resolvedModifiers))
                return;

            resolvedModifiers.Clear();
            if (!HasRequiredCaptureBuffer(em, ge))
                return;

            var configBuffer = em.GetBuffer<GEModifierConfigBuffer>(ge);
            for (var i = 0; i < configBuffer.Length; i++)
            {
                var config = configBuffer[i];
                var magnitude = ResolveMagnitude(em, ge, i, config.Magnitude, context, spec);
                resolvedModifiers.Add(new GEResolvedModifierBuffer
                {
                    AttrSetCode = config.AttrSetCode,
                    AttributeCode = config.AttributeCode,
                    Op = config.Op,
                    Magnitude = magnitude,
                    SourceEffect = ge,
                });
            }
        }

        public static void ResolveModifiers(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            in GEContextComponent context,
            in GEEffectSpecComponent spec)
        {
            if (!em.IsComponentEnabled<GEModifierConfigBuffer>(ge))
            {
                if (em.HasBuffer<GEResolvedModifierBuffer>(ge))
                    em.GetBuffer<GEResolvedModifierBuffer>(ge).Clear();

                return;
            }

            if (!TryGetResolvedModifierBuffer(em, ge, out var resolvedModifiers))
                return;

            resolvedModifiers.Clear();
            if (!HasRequiredCaptureBuffer(em, ge))
                return;

            var configBuffer = em.GetBuffer<GEModifierConfigBuffer>(ge);
            for (var i = 0; i < configBuffer.Length; i++)
            {
                var config = configBuffer[i];
                var magnitude = ResolveMagnitude(em, ref ecb, ge, i, config.Magnitude, context, spec);
                resolvedModifiers.Add(new GEResolvedModifierBuffer
                {
                    AttrSetCode = config.AttrSetCode,
                    AttributeCode = config.AttributeCode,
                    Op = config.Op,
                    Magnitude = magnitude,
                    SourceEffect = ge,
                });
            }
        }

        private static bool TryGetResolvedModifierBuffer(
            EntityManager em,
            Entity ge,
            out DynamicBuffer<GEResolvedModifierBuffer> resolvedModifiers)
        {
            if (!em.HasBuffer<GEResolvedModifierBuffer>(ge))
            {
                resolvedModifiers = default;
                return false;
            }

            resolvedModifiers = em.GetBuffer<GEResolvedModifierBuffer>(ge);
            return true;
        }

        private static bool HasRequiredCaptureBuffer(EntityManager em, Entity ge)
        {
            return !RequiresAttributeCaptureBuffer(em, ge)
                || em.HasBuffer<GEAttributeCaptureValueBuffer>(ge);
        }

        private static bool RequiresAttributeCaptureBuffer(EntityManager em, Entity ge)
        {
            if (!em.IsComponentEnabled<GEMagnitudeDefinitionBuffer>(ge))
                return false;

            var definitions = em.GetBuffer<GEMagnitudeDefinitionBuffer>(ge);
            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definition.CaptureTiming == EAttributeCaptureTiming.CurrentValue)
                    continue;

                if (definition.Source == EMagnitudeSource.SourceAttribute
                    || definition.Source == EMagnitudeSource.TargetAttribute)
                {
                    return true;
                }
            }

            return false;
        }

        private static float ResolveMagnitude(
            EntityManager em,
            Entity ge,
            int modifierIndex,
            float constantMagnitude,
            in GEContextComponent context,
            in GEEffectSpecComponent spec)
        {
            if (!TryGetMagnitudeDefinition(em, ge, modifierIndex, out var definition))
                return constantMagnitude;

            var rawMagnitude = definition.Source switch
            {
                EMagnitudeSource.Constant => constantMagnitude,
                EMagnitudeSource.SetByCaller => ResolveSetByCaller(em, ge, definition.Key, definition.FallbackMagnitude),
                EMagnitudeSource.SourceAttribute => ResolveAttributeCapture(
                    em,
                    ge,
                    modifierIndex,
                    EMagnitudeSource.SourceAttribute,
                    context.SourceAsc,
                    definition.AttributeSetCode,
                    definition.AttributeCode,
                    definition.CaptureTiming,
                    definition.FallbackMagnitude),
                EMagnitudeSource.TargetAttribute => ResolveAttributeCapture(
                    em,
                    ge,
                    modifierIndex,
                    EMagnitudeSource.TargetAttribute,
                    context.TargetAsc,
                    definition.AttributeSetCode,
                    definition.AttributeCode,
                    definition.CaptureTiming,
                    definition.FallbackMagnitude),
                EMagnitudeSource.ExecutionCalculation => ResolveExecutionCalculation(
                    em,
                    ge,
                    definition.Key,
                    definition.FallbackMagnitude,
                    context),
                EMagnitudeSource.StackCount => ResolveStackCount(spec),
                _ => constantMagnitude,
            };

            var coefficient = definition.Coefficient == 0 ? 1f : definition.Coefficient;
            return ((rawMagnitude + definition.PreAdd) * coefficient) + definition.PostAdd;
        }

        private static float ResolveMagnitude(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            int modifierIndex,
            float constantMagnitude,
            in GEContextComponent context,
            in GEEffectSpecComponent spec)
        {
            if (!TryGetMagnitudeDefinition(em, ge, modifierIndex, out var definition))
                return constantMagnitude;

            var rawMagnitude = definition.Source switch
            {
                EMagnitudeSource.Constant => constantMagnitude,
                EMagnitudeSource.SetByCaller => ResolveSetByCaller(em, ge, definition.Key, definition.FallbackMagnitude),
                EMagnitudeSource.SourceAttribute => ResolveAttributeCapture(
                    em,
                    ref ecb,
                    ge,
                    modifierIndex,
                    EMagnitudeSource.SourceAttribute,
                    context.SourceAsc,
                    definition.AttributeSetCode,
                    definition.AttributeCode,
                    definition.CaptureTiming,
                    definition.FallbackMagnitude),
                EMagnitudeSource.TargetAttribute => ResolveAttributeCapture(
                    em,
                    ref ecb,
                    ge,
                    modifierIndex,
                    EMagnitudeSource.TargetAttribute,
                    context.TargetAsc,
                    definition.AttributeSetCode,
                    definition.AttributeCode,
                    definition.CaptureTiming,
                    definition.FallbackMagnitude),
                EMagnitudeSource.ExecutionCalculation => ResolveExecutionCalculation(
                    em,
                    ge,
                    definition.Key,
                    definition.FallbackMagnitude,
                    context),
                EMagnitudeSource.StackCount => ResolveStackCount(spec),
                _ => constantMagnitude,
            };

            var coefficient = definition.Coefficient == 0 ? 1f : definition.Coefficient;
            return ((rawMagnitude + definition.PreAdd) * coefficient) + definition.PostAdd;
        }

        private static bool TryGetMagnitudeDefinition(
            EntityManager em,
            Entity ge,
            int modifierIndex,
            out GEMagnitudeDefinitionBuffer definition)
        {
            if (!em.IsComponentEnabled<GEMagnitudeDefinitionBuffer>(ge))
            {
                definition = default;
                return false;
            }

            var definitions = em.GetBuffer<GEMagnitudeDefinitionBuffer>(ge);
            for (var i = 0; i < definitions.Length; i++)
            {
                if (definitions[i].ModifierIndex != modifierIndex)
                    continue;

                definition = definitions[i];
                return true;
            }

            definition = default;
            return false;
        }

        internal static float ResolveSetByCaller(
            EntityManager em,
            Entity ge,
            int key,
            float fallbackMagnitude)
        {
            return TryGetSetByCallerValue(em, ge, key, out var value)
                ? value
                : fallbackMagnitude;
        }

        internal static bool TryGetSetByCallerValue(
            EntityManager em,
            Entity ge,
            int key,
            out float value)
        {
            if (em.HasBuffer<GESetByCallerRequestValueBuffer>(ge))
            {
                var values = em.GetBuffer<GESetByCallerRequestValueBuffer>(ge);
                for (var i = 0; i < values.Length; i++)
                {
                    if (values[i].Key == key)
                    {
                        value = values[i].Value;
                        return true;
                    }
                }
            }

            value = 0f;
            return false;
        }

        internal static float ResolveStackCount(in GEEffectSpecComponent spec)
        {
            return spec.StackCount > 0 ? spec.StackCount : 1;
        }

        private static float ResolveExecutionCalculation(
            EntityManager em,
            Entity ge,
            int key,
            float fallbackMagnitude,
            in GEContextComponent context)
        {
            if (!em.HasBuffer<GEExecutionCalculationValueBuffer>(ge))
            {
                EnqueueMagnitudeFact(em, ge, context, EGameplayEventType.ExecutionCalculationOutputMissing, key, fallbackMagnitude);
                return fallbackMagnitude;
            }

            var values = em.GetBuffer<GEExecutionCalculationValueBuffer>(ge);
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i].Key == key)
                    return values[i].Value;
            }

            EnqueueMagnitudeFact(em, ge, context, EGameplayEventType.ExecutionCalculationOutputMissing, key, fallbackMagnitude);
            return fallbackMagnitude;
        }

        internal static float ResolveAttributeCapture(
            EntityManager em,
            Entity ge,
            int modifierIndex,
            EMagnitudeSource source,
            Entity asc,
            int attrSetCode,
            int attributeCode,
            EAttributeCaptureTiming captureTiming,
            float fallbackMagnitude)
        {
            if (captureTiming == EAttributeCaptureTiming.CurrentValue)
                return ReadAttributeValue(em, asc, attrSetCode, attributeCode, fallbackMagnitude);

            if (TryGetCapturedAttributeValue(
                    em,
                    ge,
                    modifierIndex,
                    source,
                    attrSetCode,
                    attributeCode,
                    out var capturedValue))
            {
                return capturedValue;
            }

            var value = ReadAttributeValue(em, asc, attrSetCode, attributeCode, fallbackMagnitude);
            StoreCapturedAttributeValue(em, ge, modifierIndex, source, attrSetCode, attributeCode, value);
            return value;
        }

        internal static float ResolveAttributeCapture(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            int modifierIndex,
            EMagnitudeSource source,
            Entity asc,
            int attrSetCode,
            int attributeCode,
            EAttributeCaptureTiming captureTiming,
            float fallbackMagnitude)
        {
            if (captureTiming == EAttributeCaptureTiming.CurrentValue)
                return ReadAttributeValue(em, asc, attrSetCode, attributeCode, fallbackMagnitude);

            if (TryGetCapturedAttributeValue(
                    em,
                    ge,
                    modifierIndex,
                    source,
                    attrSetCode,
                    attributeCode,
                    out var capturedValue))
            {
                return capturedValue;
            }

            var value = ReadAttributeValue(em, asc, attrSetCode, attributeCode, fallbackMagnitude);
            StoreCapturedAttributeValue(em, ref ecb, ge, modifierIndex, source, attrSetCode, attributeCode, value);
            return value;
        }

        internal static float ReadAttributeValue(
            EntityManager em,
            Entity asc,
            int attrSetCode,
            int attributeCode,
            float fallbackMagnitude)
        {
            return TryReadAttributeValue(em, asc, attrSetCode, attributeCode, out var value)
                ? value
                : fallbackMagnitude;
        }

        internal static bool TryReadAttributeValue(
            EntityManager em,
            Entity asc,
            int attrSetCode,
            int attributeCode,
            out float value)
        {
            if (asc != Entity.Null && em.Exists(asc) && em.HasBuffer<AttributeValueBuffer>(asc))
            {
                var attributes = em.GetBuffer<AttributeValueBuffer>(asc);
                for (var i = 0; i < attributes.Length; i++)
                {
                    var attribute = attributes[i];
                    if (attribute.AttrSetCode == attrSetCode && attribute.Code == attributeCode)
                    {
                        value = attribute.CurrentValue;
                        return true;
                    }
                }
            }

            value = 0f;
            return false;
        }

        internal static bool TryGetCapturedAttributeValue(
            EntityManager em,
            Entity ge,
            int modifierIndex,
            EMagnitudeSource source,
            int attrSetCode,
            int attributeCode,
            out float value)
        {
            if (!em.HasBuffer<GEAttributeCaptureValueBuffer>(ge))
            {
                value = 0f;
                return false;
            }

            var captures = em.GetBuffer<GEAttributeCaptureValueBuffer>(ge);
            for (var i = 0; i < captures.Length; i++)
            {
                var capture = captures[i];
                if (capture.ModifierIndex == modifierIndex
                    && capture.Source == source
                    && capture.AttributeSetCode == attrSetCode
                    && capture.AttributeCode == attributeCode)
                {
                    value = capture.Value;
                    return true;
                }
            }

            value = 0f;
            return false;
        }

        internal static void StoreCapturedAttributeValue(
            EntityManager em,
            Entity ge,
            int modifierIndex,
            EMagnitudeSource source,
            int attrSetCode,
            int attributeCode,
            float value)
        {
            if (!em.HasBuffer<GEAttributeCaptureValueBuffer>(ge))
                return;

            var captures = em.GetBuffer<GEAttributeCaptureValueBuffer>(ge);
            captures.Add(new GEAttributeCaptureValueBuffer
            {
                ModifierIndex = modifierIndex,
                Source = source,
                AttributeSetCode = attrSetCode,
                AttributeCode = attributeCode,
                Value = value,
            });
        }

        internal static void StoreCapturedAttributeValue(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            int modifierIndex,
            EMagnitudeSource source,
            int attrSetCode,
            int attributeCode,
            float value)
        {
            if (!em.HasBuffer<GEAttributeCaptureValueBuffer>(ge))
                return;

            var captures = em.GetBuffer<GEAttributeCaptureValueBuffer>(ge);
            captures.Add(new GEAttributeCaptureValueBuffer
            {
                ModifierIndex = modifierIndex,
                Source = source,
                AttributeSetCode = attrSetCode,
                AttributeCode = attributeCode,
                Value = value,
            });
        }

        private static void EnqueueMagnitudeFact(
            EntityManager em,
            Entity ge,
            in GEContextComponent context,
            EGameplayEventType type,
            int eventCode,
            float value)
        {
            var eventWriter = EffectCommandSpecStream.BeginGameplayEventWriter(em);
            EnqueueMagnitudeFact(ref eventWriter, ge, context, type, eventCode, value);
            eventWriter.Flush();
        }

        private static void EnqueueMagnitudeFact(
            ref EffectCommandSpecStream.GameplayEventWriter eventWriter,
            Entity ge,
            in GEContextComponent context,
            EGameplayEventType type,
            int eventCode,
            float value)
        {
            if (!eventWriter.IsCreated)
                return;

            eventWriter.AppendGameplayEvent(new GameplayEventBuffer
            {
                EventType = type,
                Domain = EGameplayFactDomain.ExecutionCalculation,
                Category = EGameplayFactCategory.Failure,
                Severity = EGameplayFactSeverity.Warning,
                SourceAsc = context.SourceAsc,
                TargetAsc = context.TargetAsc,
                SourceAbility = context.SourceAbility,
                SourceEffect = ge,
                ContextId = context.ContextId,
                EventCode = eventCode,
                Value = value,
            });
        }
    }
}
