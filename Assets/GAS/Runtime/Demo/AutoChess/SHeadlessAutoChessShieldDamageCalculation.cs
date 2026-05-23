using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASExecutionCalculationExtensionGroup))]
    public partial struct SHeadlessAutoChessShieldDamageCalculation : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessShieldDamageCalculation, CEffectContext, CEffectSpecData>()
                .WithNone<CEffectDestroy>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            using var effects = _query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < effects.Length; i++)
            {
                var ge = effects[i];
                if (!em.Exists(ge) || em.HasComponent<CEffectDestroy>(ge))
                    continue;

                var calculation = em.GetComponentData<CHeadlessAutoChessShieldDamageCalculation>(ge);
                var context = em.GetComponentData<CEffectContext>(ge);
                ResolveDamageSplit(
                    em,
                    context.TargetAsc,
                    calculation,
                    out var shieldDamage,
                    out var healthDamage,
                    out var resistedDamage);

                var wroteShield = ExecutionCalculationRuntimeActions.SetOutputValue(
                    em,
                    ge,
                    context,
                    calculation.ShieldDamageOutputKey,
                    shieldDamage,
                    calculation.ShieldDamageOutputKey);
                var wroteHealth = ExecutionCalculationRuntimeActions.SetOutputValue(
                    em,
                    ge,
                    context,
                    calculation.HealthDamageOutputKey,
                    healthDamage,
                    calculation.HealthDamageOutputKey);
                var wroteResistance = calculation.ResistedDamageOutputKey > 0
                    && ExecutionCalculationRuntimeActions.SetOutputValue(
                        em,
                        ge,
                        context,
                        calculation.ResistedDamageOutputKey,
                        resistedDamage,
                        calculation.DamageTypeCode);

                if (wroteShield || wroteHealth || wroteResistance)
                    EmitDamageTypeFacts(em, ge, context, calculation, shieldDamage + healthDamage, resistedDamage);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ResolveDamageSplit(
            EntityManager em,
            Entity targetAsc,
            in CHeadlessAutoChessShieldDamageCalculation calculation,
            out float shieldDamage,
            out float healthDamage,
            out float resistedDamage)
        {
            var incomingDamage = math.max(0f, calculation.BaseDamage);
            var resistance = ResolveResistance(em, targetAsc, calculation);
            var damageAfterResistance = incomingDamage * (1f - resistance);
            resistedDamage = math.max(0f, incomingDamage - damageAfterResistance);
            var shield = math.max(
                0f,
                GetAttribute(
                    em,
                    targetAsc,
                    calculation.AttributeSetCode,
                    calculation.ShieldAttrCode));

            shieldDamage = math.min(damageAfterResistance, shield);
            healthDamage = math.max(0f, damageAfterResistance - shieldDamage);
        }

        private static float ResolveResistance(
            EntityManager em,
            Entity targetAsc,
            in CHeadlessAutoChessShieldDamageCalculation calculation)
        {
            if (calculation.ResistanceAttrSetCode <= 0 || calculation.ResistanceAttrCode <= 0)
                return 0f;

            var cap = calculation.ResistanceCap > 0f ? calculation.ResistanceCap : 1f;
            return math.clamp(
                GetAttribute(
                    em,
                    targetAsc,
                    calculation.ResistanceAttrSetCode,
                    calculation.ResistanceAttrCode),
                0f,
                cap);
        }

        private static void EmitDamageTypeFacts(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            in CHeadlessAutoChessShieldDamageCalculation calculation,
            float effectiveDamage,
            float resistedDamage)
        {
            if (calculation.DamageTypeCode <= 0)
                return;

            EventBusHelper.EnqueueGameplayEvent(em, GASManager.EntityEventBus, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessDamageTypeResolved,
                SourceAsc = context.SourceAsc,
                TargetAsc = context.TargetAsc,
                SourceAbility = context.SourceAbility,
                GameplayEffect = ge,
                ContextId = context.ContextId,
                EventCode = calculation.DamageTypeCode,
                ReasonCode = calculation.ResistanceAttrCode,
                Value = effectiveDamage,
            });

            if (resistedDamage <= 0f)
                return;

            EventBusHelper.EnqueueGameplayEvent(em, GASManager.EntityEventBus, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessDamageResisted,
                SourceAsc = context.SourceAsc,
                TargetAsc = context.TargetAsc,
                SourceAbility = context.SourceAbility,
                GameplayEffect = ge,
                ContextId = context.ContextId,
                EventCode = calculation.DamageTypeCode,
                ReasonCode = calculation.ResistanceAttrCode,
                Value = resistedDamage,
            });
        }

        private static float GetAttribute(
            EntityManager em,
            Entity asc,
            int attrSetCode,
            int attrCode)
        {
            if (asc == Entity.Null || !em.Exists(asc) || !em.HasBuffer<BAttribute>(asc))
                return 0f;

            var attributes = em.GetBuffer<BAttribute>(asc);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttrSetCode == attrSetCode && attribute.Code == attrCode)
                    return attribute.CurrentValue;
            }

            return 0f;
        }
    }
}
