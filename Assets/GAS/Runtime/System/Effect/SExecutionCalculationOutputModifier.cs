using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(GASExecutionCalculationExtensionGroup))]
    [UpdateBefore(typeof(SAttributeDeltaApply))]
    public partial struct SExecutionCalculationOutputModifier : ISystem
    {
        private EntityQuery _outputQuery;

        public void OnCreate(ref SystemState state)
        {
            _outputQuery = SystemAPI.QueryBuilder()
                .WithAll<
                    CEffectContext,
                    CEffectSpecData,
                    BExecutionCalculationValue,
                    BExecutionCalculationOutputModifierDefinition>()
                .WithNone<CExecutionCalculationOutputModifierApplied>()
                .Build();
            state.RequireForUpdate<CEffectCommandSpecStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_outputQuery.IsEmptyIgnoreFilter)
                return;

            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<CEffectCommandSpecStream>();
            EffectCommandSpecStream.EnsureBuffers(em, streamEntity);

            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            var deltas = em.GetBuffer<BAttributeDelta>(streamEntity);
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);

            using var effects = _outputQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                var deltaCount = ApplyOutputModifiers(em, effect, ref stream, deltas, frame);
                em.AddComponentData(effect, new CExecutionCalculationOutputModifierApplied
                {
                    Frame = frame,
                    DeltaCount = deltaCount,
                });
            }

            em.SetComponentData(streamEntity, stream);
        }

        public void OnDestroy(ref SystemState state) { }

        private static int ApplyOutputModifiers(
            EntityManager em,
            Entity effect,
            ref CEffectCommandSpecStream stream,
            DynamicBuffer<BAttributeDelta> deltas,
            int frame)
        {
            if (effect == Entity.Null || !em.Exists(effect))
                return 0;

            var context = em.GetComponentData<CEffectContext>(effect);
            if (context.TargetAsc == Entity.Null
                || !em.Exists(context.TargetAsc)
                || !em.HasBuffer<BAttribute>(context.TargetAsc))
            {
                return 0;
            }

            var spec = em.GetComponentData<CEffectSpecData>(effect);
            var values = em.GetBuffer<BExecutionCalculationValue>(effect);
            var definitions = em.GetBuffer<BExecutionCalculationOutputModifierDefinition>(effect);
            var resolved = em.HasBuffer<BResolvedModifier>(effect)
                ? em.GetBuffer<BResolvedModifier>(effect)
                : em.AddBuffer<BResolvedModifier>(effect);
            var attributes = em.GetBuffer<BAttribute>(context.TargetAsc);

            resolved.Clear();
            var deltaCount = 0;
            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                var rawMagnitude = ExecutionCalculationRuntimeActions.TryGetOutputValue(
                    values,
                    definition.OutputKey,
                    out var outputValue)
                    ? outputValue
                    : definition.FallbackMagnitude;
                var magnitude = ApplyTransform(
                    rawMagnitude,
                    definition.Coefficient,
                    definition.PreAdd,
                    definition.PostAdd);

                resolved.Add(new BResolvedModifier
                {
                    AttrSetCode = definition.AttrSetCode,
                    AttributeCode = definition.AttributeCode,
                    Op = definition.Op,
                    Magnitude = magnitude,
                    SourceEffect = effect,
                });

                if (ApplyModifierDelta(
                        attributes,
                        definition.AttrSetCode,
                        definition.AttributeCode,
                        definition.Op,
                        magnitude,
                        out var oldValue,
                        out var newValue))
                {
                    deltas.Add(new BAttributeDelta
                    {
                        Sequence = EffectCommandSpecStreamPhaseUtility.Allocate(ref stream.NextDeltaSequence),
                        Frame = frame,
                        SourceAsc = context.SourceAsc,
                        TargetAsc = context.TargetAsc,
                        SourceAbility = context.SourceAbility,
                        SourceEffect = effect,
                        GameplayEffectCode = spec.GameplayEffectCode,
                        ContextId = context.ContextId,
                        ParentContextId = context.ParentContextId,
                        AttrSetCode = definition.AttrSetCode,
                        AttributeCode = definition.AttributeCode,
                        Op = definition.Op,
                        ValueKind = EAttributeDeltaValueKind.BaseValue,
                        Magnitude = magnitude,
                        OldValue = oldValue,
                        NewValue = newValue,
                    });
                    deltaCount++;
                }
            }

            return deltaCount;
        }

        private static bool ApplyModifierDelta(
            DynamicBuffer<BAttribute> attributes,
            int attrSetCode,
            int attrCode,
            EModifierOp op,
            float magnitude,
            out float oldValue,
            out float newValue)
        {
            oldValue = 0f;
            newValue = 0f;

            var attrIndex = attributes.IndexOfAttribute(attrSetCode, attrCode);
            if (attrIndex < 0)
                return false;

            var attribute = attributes[attrIndex];
            oldValue = attribute.BaseValue;
            var oldCurrentValue = attribute.CurrentValue;
            newValue = AttributeHelper.ApplyModifier(attribute.BaseValue, op, magnitude);
            attribute.CurrentValue = newValue;
            AttributeHelper.Clamp(ref attribute);
            newValue = attribute.CurrentValue;
            attribute.BaseValue = newValue;
            attribute.CurrentValue = newValue;

            if (newValue == oldValue)
            {
                attributes[attrIndex] = attribute;
                return false;
            }

            attribute.Dirty = true;
            if (oldCurrentValue != attribute.CurrentValue)
            {
                attribute.PreviousCurrentValue = oldCurrentValue;
                attribute.CurrentValueChangePending = true;
            }

            attributes[attrIndex] = attribute;
            return true;
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
