using System;
using Unity.Entities;

namespace GAS.Runtime
{
    public static class AbilityRuntimeActions
    {
        private static EntityManager EntityManager => GASManager.EntityManager;

        [Obsolete("Use RequestCostGameplayEffect. This method no longer applies cost directly.")]
        public static Entity ApplyCost(Entity ability)
        {
            return RequestCostGameplayEffect(ability);
        }

        [Obsolete("Use RequestCooldownGameplayEffect. This method no longer applies cooldown directly.")]
        public static Entity ApplyCooldown(Entity ability)
        {
            return RequestCooldownGameplayEffect(ability);
        }

        public static Entity RequestCostGameplayEffect(Entity ability)
        {
            return RequestCostGameplayEffect(ability, EntityManager);
        }

        public static Entity RequestCostGameplayEffect(Entity ability, EntityManager entityManager)
        {
            if (!TryGetBaseInfo(entityManager, ability, out var baseInfo)
                || !entityManager.HasComponent<CAbilityCost>(ability))
                return Entity.Null;

            var cost = entityManager.GetComponentData<CAbilityCost>(ability);
            return AppendSimpleInstantSelfCommandOrCreateRequest(
                entityManager,
                ability,
                baseInfo,
                cost.GameplayEffectCode,
                durationFrameOverride: 0,
                "AbilityCostRequest");
        }

        public static Entity RequestCooldownGameplayEffect(Entity ability)
        {
            return RequestCooldownGameplayEffect(ability, EntityManager);
        }

        public static Entity RequestCooldownGameplayEffect(Entity ability, EntityManager entityManager)
        {
            if (!TryGetBaseInfo(entityManager, ability, out var baseInfo)
                || !entityManager.HasComponent<CAbilityCooldown>(ability))
                return Entity.Null;

            var cooldown = entityManager.GetComponentData<CAbilityCooldown>(ability);
            return AppendSimpleInstantSelfCommandOrCreateRequest(
                entityManager,
                ability,
                baseInfo,
                cooldown.GameplayEffectCode,
                cooldown.Cooldown,
                "AbilityCooldownRequest");
        }

        public static void RemoveActivationOwnedTags(Entity ability, EntityManager entityManager)
        {
            if (!entityManager.HasComponent<CAbilityBaseInfo>(ability)) return;
            var owner = entityManager.GetComponentData<CAbilityBaseInfo>(ability).Owner;
            RemoveTagsFromSource(owner, ability, entityManager);
        }

        public static bool RequestAbilityEnd(
            Entity ability,
            EntityManager entityManager,
            EAbilityLifecycleReason reason,
            Entity sourceAbility = default,
            Entity sourceEffect = default,
            int sourceAbilityCode = 0)
        {
            if (!entityManager.Exists(ability) || entityManager.HasComponent<CAbilityInTryEnd>(ability))
                return false;

            var resolvedSourceAbilityCode = ResolveSourceAbilityCode(entityManager, sourceAbility, sourceAbilityCode);
            entityManager.AddComponentData(ability, new CAbilityInTryEnd
            {
                Reason = reason,
                SourceAbility = sourceAbility,
                SourceEffect = sourceEffect,
                SourceAbilityCode = resolvedSourceAbilityCode,
            });
            EnqueueAbilityLifecycleRequestFact(
                entityManager,
                ability,
                EGameplayEventType.AbilityEndRequested,
                reason,
                sourceAbility,
                sourceEffect,
                resolvedSourceAbilityCode);
            return true;
        }

        public static bool RequestAbilityCancel(
            Entity ability,
            EntityManager entityManager,
            EAbilityLifecycleReason reason,
            Entity sourceAbility = default,
            Entity sourceEffect = default,
            int sourceAbilityCode = 0)
        {
            if (!entityManager.Exists(ability) || entityManager.HasComponent<CAbilityInTryCancel>(ability))
                return false;

            var resolvedSourceAbilityCode = ResolveSourceAbilityCode(entityManager, sourceAbility, sourceAbilityCode);
            entityManager.AddComponentData(ability, new CAbilityInTryCancel
            {
                Reason = reason,
                SourceAbility = sourceAbility,
                SourceEffect = sourceEffect,
                SourceAbilityCode = resolvedSourceAbilityCode,
            });
            EnqueueAbilityLifecycleRequestFact(
                entityManager,
                ability,
                EGameplayEventType.AbilityCancelRequested,
                reason,
                sourceAbility,
                sourceEffect,
                resolvedSourceAbilityCode);
            return true;
        }

        public static bool RequestAbilityCancel(
            Entity ability,
            EntityManager entityManager,
            ref EntityCommandBuffer ecb,
            EAbilityLifecycleReason reason,
            Entity sourceAbility = default,
            Entity sourceEffect = default,
            int sourceAbilityCode = 0)
        {
            if (!entityManager.Exists(ability) || entityManager.HasComponent<CAbilityInTryCancel>(ability))
                return false;

            var resolvedSourceAbilityCode = ResolveSourceAbilityCode(entityManager, sourceAbility, sourceAbilityCode);
            ecb.AddComponent(ability, new CAbilityInTryCancel
            {
                Reason = reason,
                SourceAbility = sourceAbility,
                SourceEffect = sourceEffect,
                SourceAbilityCode = resolvedSourceAbilityCode,
            });
            EnqueueAbilityLifecycleRequestFact(
                entityManager,
                ability,
                EGameplayEventType.AbilityCancelRequested,
                reason,
                sourceAbility,
                sourceEffect,
                resolvedSourceAbilityCode);
            return true;
        }

        public static void RemoveTagsFromSource(Entity owner, Entity source, EntityManager entityManager)
        {
            if (!entityManager.HasComponent<CTagMask>(owner) || !entityManager.HasBuffer<BTempTagSource>(owner))
                return;

            var sources = entityManager.GetBuffer<BTempTagSource>(owner);

            for (var i = sources.Length - 1; i >= 0; i--)
            {
                if (sources[i].Source != source) continue;

                var tagIndex = sources[i].TagIndex;
                sources.RemoveAt(i);
                TagRuntimeUtility.RemoveTagIndexFromEffectiveMaskIfUnreferenced(entityManager, owner, tagIndex);
            }
        }

        private static Entity AppendSimpleInstantSelfCommandOrCreateRequest(
            EntityManager entityManager,
            Entity ability,
            in CAbilityBaseInfo baseInfo,
            int gameplayEffectCode,
            int durationFrameOverride,
            string namePrefix)
        {
            if (gameplayEffectCode <= 0
                || baseInfo.Owner == Entity.Null
                || !entityManager.Exists(baseInfo.Owner)
                || entityManager.HasComponent<CAscDestroying>(baseInfo.Owner))
            {
                return Entity.Null;
            }

            var request = new CApplyGameplayEffectRequest
            {
                SourceAsc = baseInfo.Owner,
                SourceAbility = ability,
                Instigator = baseInfo.Owner,
                Causer = ability,
                GameplayEffectCode = gameplayEffectCode,
                Level = baseInfo.Level,
                DurationFrameOverride = durationFrameOverride,
            };

            return GameplayEffectRequestWriter.AppendSimpleInstantCommandOrCreateSingleTargetRequest(
                entityManager,
                request,
                baseInfo.Owner,
                ETargetDataKind.Self,
                namePrefix);
        }

        private static int ResolveSourceAbilityCode(
            EntityManager entityManager,
            Entity sourceAbility,
            int sourceAbilityCode)
        {
            if (sourceAbilityCode != 0
                || sourceAbility == Entity.Null
                || !entityManager.Exists(sourceAbility)
                || !entityManager.HasComponent<CAbilityBaseInfo>(sourceAbility))
            {
                return sourceAbilityCode;
            }

            return entityManager.GetComponentData<CAbilityBaseInfo>(sourceAbility).Code;
        }

        private static void EnqueueAbilityLifecycleRequestFact(
            EntityManager entityManager,
            Entity ability,
            EGameplayEventType type,
            EAbilityLifecycleReason reason,
            Entity sourceAbility,
            Entity sourceEffect,
            int sourceAbilityCode)
        {
            TryGetBaseInfo(entityManager, ability, out var baseInfo);
            EventBusHelper.EnqueueGameplayEvent(entityManager, GASManager.EntityEventBus, new BGameplayEvent
            {
                Type = type,
                SourceAsc = baseInfo.Owner,
                TargetAsc = baseInfo.Owner,
                SourceAbility = ability,
                GameplayEffect = sourceEffect,
                RelatedAbility = sourceAbility,
                EventCode = baseInfo.Code,
                ReasonCode = (int)reason,
                RelatedAbilityCode = sourceAbilityCode,
                Value = sourceAbilityCode,
            });
        }

        private static bool TryGetBaseInfo(EntityManager entityManager, Entity ability, out CAbilityBaseInfo baseInfo)
        {
            baseInfo = default;
            if (!entityManager.Exists(ability) || !entityManager.HasComponent<CAbilityBaseInfo>(ability))
                return false;

            baseInfo = entityManager.GetComponentData<CAbilityBaseInfo>(ability);
            return true;
        }

    }
}
