using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SActiveEffectMutationApply))]
    [UpdateBefore(typeof(GASExecutionCalculationExtensionGroup))]
    public partial struct SExecutionCalculation : ISystem
    {
        private EntityQuery _definitionQuery;

        public void OnCreate(ref SystemState state)
        {
            _definitionQuery = SystemAPI.QueryBuilder()
                .WithAll<
                    CEffectContext,
                    CEffectSpecData,
                    BExecutionCalculationDefinition,
                    BExecutionCalculationValue>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_definitionQuery.IsEmptyIgnoreFilter)
                return;

            var em = state.EntityManager;
            var eventBusEntity = SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var eventWriter = eventBusEntity != Entity.Null
                ? EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity)
                : default;

            try
            {
                using var effects = _definitionQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                for (var i = 0; i < effects.Length; i++)
                    EvaluateEffect(em, effects[i], ref eventWriter);
            }
            finally
            {
                eventWriter.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state) { }

        private static void EvaluateEffect(
            EntityManager em,
            Entity effect,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (effect == Entity.Null || !em.Exists(effect))
                return;

            var context = em.GetComponentData<CEffectContext>(effect);
            var spec = em.GetComponentData<CEffectSpecData>(effect);
            var definitions = em.GetBuffer<BExecutionCalculationDefinition>(effect);
            var outputs = em.GetBuffer<BExecutionCalculationValue>(effect);
            var hasInputs = em.HasBuffer<BExecutionCalculationInputDefinition>(effect);
            var inputs = hasInputs
                ? em.GetBuffer<BExecutionCalculationInputDefinition>(effect)
                : default;

            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                var rawValue = hasInputs && HasInputsForOutput(inputs, definition.CalculationCode, definition.OutputKey)
                    ? ResolveInputAggregate(em, effect, in context, in spec, inputs, definition.CalculationCode, definition.OutputKey)
                    : ResolveInput(
                        em,
                        effect,
                        definition.Source,
                        definition.AttributeSetCode,
                        definition.AttributeCode,
                        definition.Key,
                        definition.CaptureTiming,
                        definition.ConstantValue,
                        definition.FallbackValue,
                        in context,
                        in spec,
                        inputIndex: i);

                var value = ApplyTransform(
                    rawValue,
                    definition.Coefficient,
                    definition.PreAdd,
                    definition.PostAdd);

                if (ExecutionCalculationRuntimeActions.WriteOutputValue(outputs, definition.OutputKey, value))
                {
                    ExecutionCalculationRuntimeActions.EnqueueOutputUpdatedFact(
                        ref eventWriter,
                        effect,
                        in context,
                        definition.CalculationCode != 0 ? definition.CalculationCode : definition.OutputKey,
                        value);
                }
            }
        }

        private static bool HasInputsForOutput(
            DynamicBuffer<BExecutionCalculationInputDefinition> inputs,
            int calculationCode,
            int outputKey)
        {
            for (var i = 0; i < inputs.Length; i++)
            {
                var input = inputs[i];
                if (input.CalculationCode == calculationCode && input.OutputKey == outputKey)
                    return true;
            }

            return false;
        }

        private static float ResolveInputAggregate(
            EntityManager em,
            Entity effect,
            in CEffectContext context,
            in CEffectSpecData spec,
            DynamicBuffer<BExecutionCalculationInputDefinition> inputs,
            int calculationCode,
            int outputKey)
        {
            var value = 0f;
            for (var i = 0; i < inputs.Length; i++)
            {
                var input = inputs[i];
                if (input.CalculationCode != calculationCode || input.OutputKey != outputKey)
                    continue;

                var rawInput = ResolveInput(
                    em,
                    effect,
                    input.Source,
                    input.AttributeSetCode,
                    input.AttributeCode,
                    input.Key,
                    input.CaptureTiming,
                    input.ConstantValue,
                    input.FallbackValue,
                    in context,
                    in spec,
                    input.InputIndex);

                value += ApplyTransform(
                    rawInput,
                    input.Coefficient,
                    input.PreAdd,
                    input.PostAdd);
            }

            return value;
        }

        private static float ResolveInput(
            EntityManager em,
            Entity effect,
            EExecutionCalculationInputSource source,
            int attrSetCode,
            int attributeCode,
            int key,
            EAttributeCaptureTiming captureTiming,
            float constantValue,
            float fallbackValue,
            in CEffectContext context,
            in CEffectSpecData spec,
            int inputIndex)
        {
            return source switch
            {
                EExecutionCalculationInputSource.Constant => constantValue,
                EExecutionCalculationInputSource.SetByCaller =>
                    EffectMagnitudeResolver.ResolveSetByCaller(em, effect, key, fallbackValue),
                EExecutionCalculationInputSource.SourceAttribute =>
                    EffectMagnitudeResolver.ResolveAttributeCapture(
                        em,
                        effect,
                        inputIndex,
                        EMagnitudeSource.SourceAttribute,
                        context.SourceAsc,
                        attrSetCode,
                        attributeCode,
                        captureTiming,
                        fallbackValue),
                EExecutionCalculationInputSource.TargetAttribute =>
                    EffectMagnitudeResolver.ResolveAttributeCapture(
                        em,
                        effect,
                        inputIndex,
                        EMagnitudeSource.TargetAttribute,
                        context.TargetAsc,
                        attrSetCode,
                        attributeCode,
                        captureTiming,
                        fallbackValue),
                EExecutionCalculationInputSource.SpecLevel => spec.Level,
                EExecutionCalculationInputSource.StackCount => EffectMagnitudeResolver.ResolveStackCount(in spec),
                _ => fallbackValue,
            };
        }

        private static float ApplyTransform(
            float value,
            float coefficient,
            float preAdd,
            float postAdd)
        {
            var resolvedCoefficient = coefficient == 0f ? 1f : coefficient;
            return ((value + preAdd) * resolvedCoefficient) + postAdd;
        }
    }
}
