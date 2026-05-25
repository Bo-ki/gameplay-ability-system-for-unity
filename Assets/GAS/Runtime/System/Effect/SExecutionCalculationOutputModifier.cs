using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Consumes ExecutionCalculation output values and appends their resolved GE modifiers.
    /// Runs after the custom execution extension slot so built-in and custom producers share one output contract.
    /// </summary>
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(GASExecutionCalculationExtensionGroup))]
    public partial struct SExecutionCalculationOutputModifier : ISystem
    {
        private EntityQuery _valueQuery;
        private EntityQuery _outputModifierQuery;

        public void OnCreate(ref SystemState state)
        {
            _valueQuery = SystemAPI.QueryBuilder()
                .WithAll<CEffectContext, CEffectSpecData, BExecutionCalculationValue>()
                .WithNone<CEffectDestroy>()
                .Build();
            _outputModifierQuery = SystemAPI.QueryBuilder()
                .WithAll<CEffectContext, CEffectSpecData, BExecutionCalculationOutputModifierDefinition>()
                .WithNone<CEffectDestroy>()
                .Build();
            state.RequireForUpdate(_outputModifierQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            using var effectsWithValues = _valueQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < effectsWithValues.Length; i++)
                ResolveEffectOutputModifiers(em, ref ecb, effectsWithValues[i]);

            using var effectsWithOutputDefinitions = _outputModifierQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < effectsWithOutputDefinitions.Length; i++)
            {
                var ge = effectsWithOutputDefinitions[i];
                if (em.Exists(ge) && !em.HasBuffer<BExecutionCalculationValue>(ge))
                    ResolveEffectOutputModifiers(em, ref ecb, ge);
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

        private static void ResolveEffectOutputModifiers(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge)
        {
            if (!em.Exists(ge)
                || !em.HasComponent<CEffectContext>(ge)
                || !em.HasComponent<CEffectSpecData>(ge)
                || em.HasComponent<CEffectDestroy>(ge))
            {
                return;
            }

            var context = em.GetComponentData<CEffectContext>(ge);
            var spec = em.GetComponentData<CEffectSpecData>(ge);

            EffectMagnitudeResolver.ResolveModifiers(em, ref ecb, ge, context, spec);
            var hasValues = em.HasBuffer<BExecutionCalculationValue>(ge);
            AppendExecutionOutputModifiers(em, ref ecb, ge, context, hasValues);

            if (EffectRuntimeUtility.IsActive(em, ge))
                EffectRuntimeUtility.SyncRuntimeModifiersFromResolved(em, ge, context);
        }

        private static void AppendExecutionOutputModifiers(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            in CEffectContext context,
            bool hasValues)
        {
            if (!em.HasBuffer<BExecutionCalculationOutputModifierDefinition>(ge))
                return;

            var definitionBuffer = em.GetBuffer<BExecutionCalculationOutputModifierDefinition>(ge);
            if (definitionBuffer.Length == 0)
                return;

            var resolvedModifiers = em.HasBuffer<BResolvedModifier>(ge)
                ? em.GetBuffer<BResolvedModifier>(ge)
                : AddResolvedModifierBuffer(em, ref ecb, ge);
            definitionBuffer = em.GetBuffer<BExecutionCalculationOutputModifierDefinition>(ge);
            var values = hasValues
                ? em.GetBuffer<BExecutionCalculationValue>(ge)
                : default;

            for (var i = 0; i < definitionBuffer.Length; i++)
            {
                var definition = definitionBuffer[i];
                var magnitude = ResolveOutputModifierMagnitude(em, ge, context, hasValues, values, definition);
                resolvedModifiers.Add(new BResolvedModifier
                {
                    AttrSetCode = definition.AttrSetCode,
                    AttributeCode = definition.AttributeCode,
                    Op = definition.Op,
                    Magnitude = magnitude,
                    SourceEffect = ge,
                });
            }
        }

        private static float ResolveOutputModifierMagnitude(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            bool hasValues,
            DynamicBuffer<BExecutionCalculationValue> values,
            in BExecutionCalculationOutputModifierDefinition definition)
        {
            if (hasValues
                && TryGetOutputValue(values, definition.OutputKey, out var value))
            {
                return ApplyOutputModifierMath(value, definition);
            }

            EnqueueOutputMissingFact(
                em,
                ge,
                context,
                ResolveEventCode(definition),
                definition.FallbackMagnitude);
            return ApplyOutputModifierMath(definition.FallbackMagnitude, definition);
        }

        private static bool TryGetOutputValue(
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

        private static float ApplyOutputModifierMath(
            float input,
            in BExecutionCalculationOutputModifierDefinition definition)
        {
            var coefficient = definition.Coefficient == 0 ? 1f : definition.Coefficient;
            return ((input + definition.PreAdd) * coefficient) + definition.PostAdd;
        }

        private static int ResolveEventCode(in BExecutionCalculationOutputModifierDefinition definition)
        {
            if (definition.CalculationCode != 0)
                return definition.CalculationCode;

            return definition.OutputKey;
        }

        private static void EnqueueOutputMissingFact(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            int eventCode,
            float value)
        {
            EventBusHelper.EnqueueGameplayEvent(em, GASManager.EntityEventBus, new BGameplayEvent
            {
                Type = EGameplayEventType.ExecutionCalculationOutputMissing,
                SourceAsc = context.SourceAsc,
                TargetAsc = context.TargetAsc,
                SourceAbility = context.SourceAbility,
                GameplayEffect = ge,
                ContextId = context.ContextId,
                EventCode = eventCode,
                Value = value,
            });
        }

        private static DynamicBuffer<BResolvedModifier> AddResolvedModifierBuffer(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge)
        {
            ecb.AddBuffer<BResolvedModifier>(ge);
            PlaybackAndReset(ref ecb, em);
            return em.GetBuffer<BResolvedModifier>(ge);
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
    }
}
