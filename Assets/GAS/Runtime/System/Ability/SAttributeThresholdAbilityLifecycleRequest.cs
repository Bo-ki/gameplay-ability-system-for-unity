using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Produces ability lifecycle requests from attribute thresholds without using OOP callbacks.
    /// </summary>
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [UpdateAfter(typeof(SAbilityTick))]
    [UpdateBefore(typeof(SAbilityLifecycleRequest))]
    [DisableAutoCreation]
    public partial struct SAttributeThresholdAbilityLifecycleRequest : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CAttributeThresholdAbilityLifecycleRule>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ruleEntities = _query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < ruleEntities.Length; i++)
            {
                var ruleEntity = ruleEntities[i];
                if (!em.Exists(ruleEntity))
                    continue;

                var rule = em.GetComponentData<CAttributeThresholdAbilityLifecycleRule>(ruleEntity);
                var owner = rule.OwnerAsc != Entity.Null ? rule.OwnerAsc : ruleEntity;
                if (!CanEvaluateRule(em, owner, rule))
                    continue;

                if (!TryGetAttributeValue(em.GetBuffer<BAttribute>(owner), rule.AttrSetCode, rule.AttrCode, out var value)
                    || value > rule.Threshold)
                {
                    continue;
                }

                var ability = FindGrantedAbility(em, em.GetBuffer<BGrantedAbility>(owner), rule.AbilityCode);
                if (ability == Entity.Null || !ShouldRequestLifecycle(em, ability))
                    continue;

                var baseInfo = em.GetComponentData<CAbilityBaseInfo>(ability);
                var reason = rule.Reason == EAbilityLifecycleReason.Unknown
                    ? EAbilityLifecycleReason.AttributeThreshold
                    : rule.Reason;

                if (rule.RequestType == EAttributeThresholdAbilityLifecycleRequestType.Cancel)
                {
                    AbilityRuntimeActions.RequestAbilityCancel(
                        ability,
                        em,
                        reason,
                        sourceAbility: ability,
                        sourceAbilityCode: baseInfo.Code);
                }
                else
                {
                    AbilityRuntimeActions.RequestAbilityEnd(
                        ability,
                        em,
                        reason,
                        sourceAbility: ability,
                        sourceAbilityCode: baseInfo.Code);
                }
            }

            ruleEntities.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static bool CanEvaluateRule(
            EntityManager em,
            Entity owner,
            in CAttributeThresholdAbilityLifecycleRule rule)
        {
            return owner != Entity.Null
                && rule.AbilityCode > 0
                && em.Exists(owner)
                && em.HasBuffer<BAttribute>(owner)
                && em.HasBuffer<BGrantedAbility>(owner);
        }

        private static bool TryGetAttributeValue(
            DynamicBuffer<BAttribute> attributes,
            int attrSetCode,
            int attrCode,
            out float value)
        {
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttrSetCode != attrSetCode || attribute.Code != attrCode)
                    continue;

                value = attribute.CurrentValue;
                return true;
            }

            value = 0f;
            return false;
        }

        private static Entity FindGrantedAbility(
            EntityManager em,
            DynamicBuffer<BGrantedAbility> grantedAbilities,
            int abilityCode)
        {
            for (var i = 0; i < grantedAbilities.Length; i++)
            {
                var ability = grantedAbilities[i].AbilityEntity;
                if (ability == Entity.Null
                    || !em.Exists(ability)
                    || !em.HasComponent<CAbilityBaseInfo>(ability))
                {
                    continue;
                }

                var baseInfo = em.GetComponentData<CAbilityBaseInfo>(ability);
                if (baseInfo.Code == abilityCode)
                    return ability;
            }

            return Entity.Null;
        }

        private static bool ShouldRequestLifecycle(EntityManager em, Entity ability)
        {
            if (!em.Exists(ability)
                || !em.HasComponent<CAbilityBaseInfo>(ability)
                || !em.HasComponent<CAbilityRuntimeState>(ability)
                || em.HasComponent<CAbilityInTryEnd>(ability)
                || em.HasComponent<CAbilityInTryCancel>(ability))
            {
                return false;
            }

            var phase = em.GetComponentData<CAbilityRuntimeState>(ability).Phase;
            return phase == EAbilityPhase.Activating || phase == EAbilityPhase.Active;
        }
    }
}
