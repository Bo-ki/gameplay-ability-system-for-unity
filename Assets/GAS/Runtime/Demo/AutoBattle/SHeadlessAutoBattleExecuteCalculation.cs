using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASExecutionCalculationExtensionGroup))]
    public partial struct SHeadlessAutoBattleExecuteCalculation : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoBattleExecuteCalculation, CEffectContext, CEffectSpecData>()
                .WithNone<CEffectDestroy>()
                .WithNone<CEffectCleanup>()
                .WithNone<CEffectFinalDestroy>()
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
                if (!em.Exists(ge)
                    || em.HasComponent<CEffectCleanup>(ge)
                    || em.HasComponent<CEffectDestroy>(ge)
                    || em.HasComponent<CEffectFinalDestroy>(ge))
                    continue;

                var calculation = em.GetComponentData<CHeadlessAutoBattleExecuteCalculation>(ge);
                var context = em.GetComponentData<CEffectContext>(ge);
                var damage = ResolveDamage(em, context.TargetAsc, calculation);

                ExecutionCalculationRuntimeActions.SetOutputValue(
                    em,
                    ge,
                    context,
                    calculation.OutputKey,
                    damage,
                    calculation.CalculationCode);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static float ResolveDamage(
            EntityManager em,
            Entity targetAsc,
            in CHeadlessAutoBattleExecuteCalculation calculation)
        {
            var damage = calculation.BaseDamage;
            if (TryGetAttribute(em, targetAsc, calculation.HealthAttrSetCode, calculation.HealthAttrCode, out var current, out var max))
            {
                var missingHealth = math.max(0f, max - current);
                damage += missingHealth * calculation.MissingHealthCoefficient;
            }

            if (calculation.MinDamage > 0f)
                damage = math.max(damage, calculation.MinDamage);
            if (calculation.MaxDamage > 0f)
                damage = math.min(damage, calculation.MaxDamage);

            return math.max(0f, damage);
        }

        private static bool TryGetAttribute(
            EntityManager em,
            Entity asc,
            int attrSetCode,
            int attrCode,
            out float currentValue,
            out float maxValue)
        {
            currentValue = 0f;
            maxValue = 0f;

            if (asc == Entity.Null || !em.Exists(asc) || !em.HasBuffer<BAttribute>(asc))
                return false;

            var attributes = em.GetBuffer<BAttribute>(asc);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttrSetCode != attrSetCode || attribute.Code != attrCode)
                    continue;

                currentValue = attribute.CurrentValue;
                maxValue = attribute.MaxValue > 0f ? attribute.MaxValue : attribute.BaseValue;
                return true;
            }

            return false;
        }
    }
}
