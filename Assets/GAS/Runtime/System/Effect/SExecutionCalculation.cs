using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Produces built-in data-driven ExecutionCalculation outputs before custom ECS extensions run.
    /// </summary>
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SApplyGameplayEffectRequest))]
    [UpdateBefore(typeof(GASExecutionCalculationExtensionGroup))]
    public partial struct SExecutionCalculation : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CEffectContext, CEffectSpecData, BExecutionCalculationDefinition>()
                .WithNone<CEffectDestroy>()
                .WithNone<CEffectCleanup>()
                .WithNone<CEffectFinalDestroy>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var eventBusWriter = EventBusHelper.BeginGameplayEventBatch(em, GASManager.EntityEventBus);
            try
            {
                using var effects = _query.ToEntityArray(Allocator.Temp);

                for (var i = 0; i < effects.Length; i++)
                {
                    var ge = effects[i];
                    if (!em.Exists(ge)
                        || !em.HasComponent<CEffectContext>(ge)
                        || !em.HasComponent<CEffectSpecData>(ge)
                        || !em.HasBuffer<BExecutionCalculationDefinition>(ge))
                    {
                        continue;
                    }

                    var context = em.GetComponentData<CEffectContext>(ge);
                    var spec = em.GetComponentData<CEffectSpecData>(ge);
                    var definitions = em.GetBuffer<BExecutionCalculationDefinition>(ge);
                    var hadOutputValues = em.HasBuffer<BExecutionCalculationValue>(ge);
                    var outputValues = hadOutputValues
                        ? em.GetBuffer<BExecutionCalculationValue>(ge)
                        : ecb.AddBuffer<BExecutionCalculationValue>(ge);
                    var hasInputDefinitions = em.HasBuffer<BExecutionCalculationInputDefinition>(ge);
                    var inputDefinitions = hasInputDefinitions
                        ? em.GetBuffer<BExecutionCalculationInputDefinition>(ge)
                        : default;
                    for (var j = 0; j < definitions.Length; j++)
                    {
                        var definition = definitions[j];
                        var hasMissingInput = false;
                        var missingInputValue = 0f;
                        var input = 0f;
                        var hasResolvedInput = false;
                        if (hasInputDefinitions
                            && TryResolveInputAggregate(
                                em,
                                ge,
                                context,
                                spec,
                                definition,
                                inputDefinitions,
                                out input,
                                out hasMissingInput,
                                out missingInputValue))
                        {
                            hasResolvedInput = true;
                        }

                        if (!hasResolvedInput && !TryResolveInput(em, ge, context, spec, definition, out input))
                        {
                            input = definition.FallbackValue;
                            hasMissingInput = true;
                            missingInputValue = input;
                        }

                        var result = ApplyMath(input, definition);
                        if (ExecutionCalculationRuntimeActions.WriteOutputValue(outputValues, definition.OutputKey, result))
                        {
                            if (hasMissingInput)
                            {
                                EnqueueExecutionFact(
                                    ref eventBusWriter,
                                    ge,
                                    context,
                                    EGameplayEventType.ExecutionCalculationInputMissing,
                                    ResolveEventCode(definition),
                                    missingInputValue);
                            }

                            ExecutionCalculationRuntimeActions.EnqueueOutputUpdatedFact(
                                ref eventBusWriter,
                                ge,
                                context,
                                ResolveEventCode(definition),
                                result);
                        }
                    }
                }
            }
            finally
            {
                eventBusWriter.Dispose();
            }

            ecb.Playback(em);
            GasRuntimeDebugger.RecordRuntimeCoreEcbPlayback(
                em,
                GasRuntimeDebugger.ResolveCurrentFrame(em),
                EGasRuntimeDiagnosticModule.Effect);
            ecb.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static bool TryResolveInput(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            in CEffectSpecData spec,
            in BExecutionCalculationDefinition definition,
            out float value)
        {
            switch (definition.Source)
            {
                case EExecutionCalculationInputSource.Constant:
                    value = definition.ConstantValue;
                    return true;
                case EExecutionCalculationInputSource.SetByCaller:
                    return EffectMagnitudeResolver.TryGetSetByCallerValue(em, ge, definition.Key, out value);
                case EExecutionCalculationInputSource.SourceAttribute:
                    return TryResolveAttributeInput(
                        em,
                        ge,
                        context.SourceAsc,
                        ResolveCaptureKey(definition),
                        EMagnitudeSource.SourceAttribute,
                        definition,
                        out value);
                case EExecutionCalculationInputSource.TargetAttribute:
                    return TryResolveAttributeInput(
                        em,
                        ge,
                        context.TargetAsc,
                        ResolveCaptureKey(definition),
                        EMagnitudeSource.TargetAttribute,
                        definition,
                        out value);
                case EExecutionCalculationInputSource.SpecLevel:
                    value = spec.Level;
                    return spec.Level > 0;
                case EExecutionCalculationInputSource.StackCount:
                    value = EffectMagnitudeResolver.ResolveStackCount(spec);
                    return true;
                default:
                    value = definition.FallbackValue;
                    return false;
            }
        }

        private static bool TryResolveInputAggregate(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            in CEffectSpecData spec,
            in BExecutionCalculationDefinition definition,
            DynamicBuffer<BExecutionCalculationInputDefinition> inputDefinitions,
            out float value,
            out bool hasMissingInput,
            out float missingInputValue)
        {
            var hasAnyInput = false;
            hasMissingInput = false;
            missingInputValue = 0f;
            value = 0f;

            for (var i = 0; i < inputDefinitions.Length; i++)
            {
                var inputDefinition = inputDefinitions[i];
                if (!BelongsToCalculation(definition, inputDefinition))
                    continue;

                hasAnyInput = true;
                var inputAvailable = TryResolveInput(em, ge, context, spec, inputDefinition, out var input);
                if (!inputAvailable)
                {
                    if (!hasMissingInput)
                        missingInputValue = inputDefinition.FallbackValue;

                    hasMissingInput = true;
                    input = inputDefinition.FallbackValue;
                }

                value += ApplyInputMath(input, inputDefinition);
            }

            if (!hasAnyInput)
                return false;

            return true;
        }

        private static bool TryResolveInput(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            in CEffectSpecData spec,
            in BExecutionCalculationInputDefinition definition,
            out float value)
        {
            switch (definition.Source)
            {
                case EExecutionCalculationInputSource.Constant:
                    value = definition.ConstantValue;
                    return true;
                case EExecutionCalculationInputSource.SetByCaller:
                    return EffectMagnitudeResolver.TryGetSetByCallerValue(em, ge, definition.Key, out value);
                case EExecutionCalculationInputSource.SourceAttribute:
                    return TryResolveAttributeInput(
                        em,
                        ge,
                        context.SourceAsc,
                        ResolveCaptureKey(definition),
                        EMagnitudeSource.SourceAttribute,
                        definition,
                        out value);
                case EExecutionCalculationInputSource.TargetAttribute:
                    return TryResolveAttributeInput(
                        em,
                        ge,
                        context.TargetAsc,
                        ResolveCaptureKey(definition),
                        EMagnitudeSource.TargetAttribute,
                        definition,
                        out value);
                case EExecutionCalculationInputSource.SpecLevel:
                    value = spec.Level;
                    return spec.Level > 0;
                case EExecutionCalculationInputSource.StackCount:
                    value = EffectMagnitudeResolver.ResolveStackCount(spec);
                    return true;
                default:
                    value = definition.FallbackValue;
                    return false;
            }
        }

        private static bool TryResolveAttributeInput(
            EntityManager em,
            Entity ge,
            Entity asc,
            int captureKey,
            EMagnitudeSource source,
            in BExecutionCalculationDefinition definition,
            out float value)
        {
            if (definition.CaptureTiming == EAttributeCaptureTiming.CurrentValue)
            {
                return EffectMagnitudeResolver.TryReadAttributeValue(
                    em,
                    asc,
                    definition.AttributeSetCode,
                    definition.AttributeCode,
                    out value);
            }

            if (EffectMagnitudeResolver.TryGetCapturedAttributeValue(
                    em,
                    ge,
                    captureKey,
                    source,
                    definition.AttributeSetCode,
                    definition.AttributeCode,
                    out value))
            {
                return true;
            }

            var inputAvailable = EffectMagnitudeResolver.TryReadAttributeValue(
                em,
                asc,
                definition.AttributeSetCode,
                definition.AttributeCode,
                out value);
            if (!inputAvailable)
                value = definition.FallbackValue;

            EffectMagnitudeResolver.StoreCapturedAttributeValue(
                em,
                ge,
                captureKey,
                source,
                definition.AttributeSetCode,
                definition.AttributeCode,
                value);
            return inputAvailable;
        }

        private static bool TryResolveAttributeInput(
            EntityManager em,
            Entity ge,
            Entity asc,
            int captureKey,
            EMagnitudeSource source,
            in BExecutionCalculationInputDefinition definition,
            out float value)
        {
            if (definition.CaptureTiming == EAttributeCaptureTiming.CurrentValue)
            {
                return EffectMagnitudeResolver.TryReadAttributeValue(
                    em,
                    asc,
                    definition.AttributeSetCode,
                    definition.AttributeCode,
                    out value);
            }

            if (EffectMagnitudeResolver.TryGetCapturedAttributeValue(
                    em,
                    ge,
                    captureKey,
                    source,
                    definition.AttributeSetCode,
                    definition.AttributeCode,
                    out value))
            {
                return true;
            }

            var inputAvailable = EffectMagnitudeResolver.TryReadAttributeValue(
                em,
                asc,
                definition.AttributeSetCode,
                definition.AttributeCode,
                out value);
            if (!inputAvailable)
                value = definition.FallbackValue;

            EffectMagnitudeResolver.StoreCapturedAttributeValue(
                em,
                ge,
                captureKey,
                source,
                definition.AttributeSetCode,
                definition.AttributeCode,
                value);
            return inputAvailable;
        }

        private static float ApplyMath(float input, in BExecutionCalculationDefinition definition)
        {
            var coefficient = definition.Coefficient == 0 ? 1f : definition.Coefficient;
            return ((input + definition.PreAdd) * coefficient) + definition.PostAdd;
        }

        private static float ApplyInputMath(float input, in BExecutionCalculationInputDefinition definition)
        {
            var coefficient = definition.Coefficient == 0 ? 1f : definition.Coefficient;
            return ((input + definition.PreAdd) * coefficient) + definition.PostAdd;
        }

        private static int ResolveCaptureKey(in BExecutionCalculationDefinition definition)
        {
            if (definition.CalculationCode != 0)
                return definition.CalculationCode;

            return definition.OutputKey;
        }

        private static int ResolveCaptureKey(in BExecutionCalculationInputDefinition definition)
        {
            if (definition.InputIndex != 0)
                return definition.InputIndex;

            if (definition.Key != 0)
                return definition.Key;

            if (definition.CalculationCode != 0)
                return definition.CalculationCode;

            return definition.OutputKey;
        }

        private static bool BelongsToCalculation(
            in BExecutionCalculationDefinition definition,
            in BExecutionCalculationInputDefinition inputDefinition)
        {
            if (inputDefinition.CalculationCode != 0)
                return definition.CalculationCode == inputDefinition.CalculationCode;

            return definition.OutputKey == inputDefinition.OutputKey;
        }

        private static int ResolveEventCode(in BExecutionCalculationDefinition definition)
        {
            if (definition.CalculationCode != 0)
                return definition.CalculationCode;

            return definition.OutputKey;
        }

        private static void EnqueueExecutionFact(
            ref EventBusHelper.GameplayEventBusWriter eventBusWriter,
            Entity ge,
            in CEffectContext context,
            EGameplayEventType type,
            int eventCode,
            float value)
        {
            if (!eventBusWriter.IsCreated)
                return;

            eventBusWriter.EnqueueGameplayEvent(new BGameplayEvent
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
