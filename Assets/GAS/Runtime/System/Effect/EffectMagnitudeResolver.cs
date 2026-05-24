using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    internal static class EffectMagnitudeResolver
    {
        public static void ResolveModifiers(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            in CEffectSpecData spec)
        {
            if (!em.HasBuffer<BModifierConfig>(ge))
            {
                if (em.HasBuffer<BResolvedModifier>(ge))
                    em.GetBuffer<BResolvedModifier>(ge).Clear();

                return;
            }

            EnsureResolverBuffers(em, ge);

            var configBuffer = em.GetBuffer<BModifierConfig>(ge);
            var resolvedModifiers = em.GetBuffer<BResolvedModifier>(ge);
            resolvedModifiers.Clear();
            for (var i = 0; i < configBuffer.Length; i++)
            {
                var config = configBuffer[i];
                var magnitude = ResolveMagnitude(em, ge, i, config.Magnitude, context, spec);
                resolvedModifiers.Add(new BResolvedModifier
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
            in CEffectContext context,
            in CEffectSpecData spec)
        {
            if (!em.HasBuffer<BModifierConfig>(ge))
            {
                if (em.HasBuffer<BResolvedModifier>(ge))
                    em.GetBuffer<BResolvedModifier>(ge).Clear();

                return;
            }

            EnsureResolverBuffers(em, ref ecb, ge);

            var configBuffer = em.GetBuffer<BModifierConfig>(ge);
            var resolvedModifiers = em.GetBuffer<BResolvedModifier>(ge);
            resolvedModifiers.Clear();
            for (var i = 0; i < configBuffer.Length; i++)
            {
                var config = configBuffer[i];
                var magnitude = ResolveMagnitude(em, ref ecb, ge, i, config.Magnitude, context, spec);
                resolvedModifiers.Add(new BResolvedModifier
                {
                    AttrSetCode = config.AttrSetCode,
                    AttributeCode = config.AttributeCode,
                    Op = config.Op,
                    Magnitude = magnitude,
                    SourceEffect = ge,
                });
            }
        }

        private static void EnsureResolverBuffers(EntityManager em, Entity ge)
        {
            if (!em.HasBuffer<BResolvedModifier>(ge))
                em.AddBuffer<BResolvedModifier>(ge);

            if (RequiresAttributeCaptureBuffer(em, ge) && !em.HasBuffer<BAttributeCaptureValue>(ge))
                em.AddBuffer<BAttributeCaptureValue>(ge);
        }

        private static void EnsureResolverBuffers(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge)
        {
            var requiresPlayback = false;
            if (!em.HasBuffer<BResolvedModifier>(ge))
            {
                ecb.AddBuffer<BResolvedModifier>(ge);
                requiresPlayback = true;
            }

            if (RequiresAttributeCaptureBuffer(em, ge) && !em.HasBuffer<BAttributeCaptureValue>(ge))
            {
                ecb.AddBuffer<BAttributeCaptureValue>(ge);
                requiresPlayback = true;
            }

            if (requiresPlayback)
                PlaybackAndReset(ref ecb, em);
        }

        private static bool RequiresAttributeCaptureBuffer(EntityManager em, Entity ge)
        {
            if (!em.HasBuffer<BMagnitudeDefinition>(ge))
                return false;

            var definitions = em.GetBuffer<BMagnitudeDefinition>(ge);
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
            in CEffectContext context,
            in CEffectSpecData spec)
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
            in CEffectContext context,
            in CEffectSpecData spec)
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
            out BMagnitudeDefinition definition)
        {
            if (!em.HasBuffer<BMagnitudeDefinition>(ge))
            {
                definition = default;
                return false;
            }

            var definitions = em.GetBuffer<BMagnitudeDefinition>(ge);
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
            if (em.HasBuffer<BSetByCallerValue>(ge))
            {
                var values = em.GetBuffer<BSetByCallerValue>(ge);
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

        internal static float ResolveStackCount(in CEffectSpecData spec)
        {
            return spec.StackCount > 0 ? spec.StackCount : 1;
        }

        private static float ResolveExecutionCalculation(
            EntityManager em,
            Entity ge,
            int key,
            float fallbackMagnitude,
            in CEffectContext context)
        {
            if (!em.HasBuffer<BExecutionCalculationValue>(ge))
            {
                EnqueueMagnitudeFact(em, ge, context, EGameplayEventType.ExecutionCalculationOutputMissing, key, fallbackMagnitude);
                return fallbackMagnitude;
            }

            var values = em.GetBuffer<BExecutionCalculationValue>(ge);
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
            if (asc != Entity.Null && em.Exists(asc) && em.HasBuffer<BAttribute>(asc))
            {
                var attributes = em.GetBuffer<BAttribute>(asc);
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
            if (!em.HasBuffer<BAttributeCaptureValue>(ge))
            {
                value = 0f;
                return false;
            }

            var captures = em.GetBuffer<BAttributeCaptureValue>(ge);
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
            var captures = GetOrCreateAttributeCaptures(em, ge);

            captures.Add(new BAttributeCaptureValue
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
            var captures = GetOrCreateAttributeCaptures(em, ref ecb, ge);

            captures.Add(new BAttributeCaptureValue
            {
                ModifierIndex = modifierIndex,
                Source = source,
                AttributeSetCode = attrSetCode,
                AttributeCode = attributeCode,
                Value = value,
            });
        }

        private static DynamicBuffer<BResolvedModifier> GetOrCreateResolvedModifiers(
            EntityManager em,
            Entity ge)
        {
            return em.HasBuffer<BResolvedModifier>(ge)
                ? em.GetBuffer<BResolvedModifier>(ge)
                : em.AddBuffer<BResolvedModifier>(ge);
        }

        private static DynamicBuffer<BResolvedModifier> GetOrCreateResolvedModifiers(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge)
        {
            if (!em.HasBuffer<BResolvedModifier>(ge))
            {
                ecb.AddBuffer<BResolvedModifier>(ge);
                PlaybackAndReset(ref ecb, em);
            }

            return em.GetBuffer<BResolvedModifier>(ge);
        }

        private static DynamicBuffer<BAttributeCaptureValue> GetOrCreateAttributeCaptures(
            EntityManager em,
            Entity ge)
        {
            return em.HasBuffer<BAttributeCaptureValue>(ge)
                ? em.GetBuffer<BAttributeCaptureValue>(ge)
                : em.AddBuffer<BAttributeCaptureValue>(ge);
        }

        private static DynamicBuffer<BAttributeCaptureValue> GetOrCreateAttributeCaptures(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge)
        {
            if (!em.HasBuffer<BAttributeCaptureValue>(ge))
            {
                ecb.AddBuffer<BAttributeCaptureValue>(ge);
                PlaybackAndReset(ref ecb, em);
            }

            return em.GetBuffer<BAttributeCaptureValue>(ge);
        }

        private static void PlaybackAndReset(ref EntityCommandBuffer ecb, EntityManager em)
        {
            ecb.Playback(em);
            GasRuntimeDebugger.RecordRuntimeCoreEcbPlayback(
                em,
                GasRuntimeDebugger.ResolveCurrentFrame(em),
                EGasRuntimeDiagnosticModule.Effect);
            ecb.Dispose();
            ecb = new EntityCommandBuffer(Allocator.Temp);
        }

        private static void EnqueueMagnitudeFact(
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
