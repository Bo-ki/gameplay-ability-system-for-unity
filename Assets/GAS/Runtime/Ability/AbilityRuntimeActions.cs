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
                || !entityManager.HasComponent<AbilityCostComponent>(ability)
                || !entityManager.IsComponentEnabled<AbilityCostComponent>(ability))
                return Entity.Null;

            var cost = entityManager.GetComponentData<AbilityCostComponent>(ability);
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
                || !entityManager.HasComponent<AbilityCooldownComponent>(ability)
                || !entityManager.IsComponentEnabled<AbilityCooldownComponent>(ability))
                return Entity.Null;

            var cooldown = entityManager.GetComponentData<AbilityCooldownComponent>(ability);
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
            if (!entityManager.HasComponent<AbilityStateComponent>(ability)) return;
            var owner = entityManager.GetComponentData<AbilityStateComponent>(ability).Owner;
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
            if (!entityManager.Exists(ability)
                || !entityManager.HasComponent<AbilityEndRequestComponent>(ability)
                || entityManager.IsComponentEnabled<AbilityEndRequestComponent>(ability))
                return false;

            var resolvedSourceAbilityCode = ResolveSourceAbilityCode(entityManager, sourceAbility, sourceAbilityCode);
            entityManager.SetComponentData(ability, new AbilityEndRequestComponent
            {
                Reason = reason,
                SourceAbility = sourceAbility,
                SourceEffect = sourceEffect,
                SourceAbilityCode = resolvedSourceAbilityCode,
            });
            entityManager.SetComponentEnabled<AbilityEndRequestComponent>(ability, true);
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
            if (!entityManager.Exists(ability)
                || !entityManager.HasComponent<AbilityCancelRequestComponent>(ability)
                || entityManager.IsComponentEnabled<AbilityCancelRequestComponent>(ability))
                return false;

            var resolvedSourceAbilityCode = ResolveSourceAbilityCode(entityManager, sourceAbility, sourceAbilityCode);
            entityManager.SetComponentData(ability, new AbilityCancelRequestComponent
            {
                Reason = reason,
                SourceAbility = sourceAbility,
                SourceEffect = sourceEffect,
                SourceAbilityCode = resolvedSourceAbilityCode,
            });
            entityManager.SetComponentEnabled<AbilityCancelRequestComponent>(ability, true);
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
            if (!entityManager.Exists(ability))
                return false;

            var resolvedSourceAbilityCode = ResolveSourceAbilityCode(entityManager, sourceAbility, sourceAbilityCode);
            if (entityManager.HasComponent<AbilityCancelRequestComponent>(ability))
            {
                if (entityManager.IsComponentEnabled<AbilityCancelRequestComponent>(ability))
                    return false;

                entityManager.SetComponentData(ability, new AbilityCancelRequestComponent
                {
                    Reason = reason,
                    SourceAbility = sourceAbility,
                    SourceEffect = sourceEffect,
                    SourceAbilityCode = resolvedSourceAbilityCode,
                });
                entityManager.SetComponentEnabled<AbilityCancelRequestComponent>(ability, true);
            }
            else
            {
                return false;
            }

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

        public static void EnableDestroyOnCleanup(
            Entity ability,
            EntityManager entityManager,
            ref EntityCommandBuffer ecb)
        {
            if (!entityManager.Exists(ability))
                return;

            if (entityManager.HasComponent<AbilityDestroyOnCleanupComponent>(ability))
            {
                if (!entityManager.IsComponentEnabled<AbilityDestroyOnCleanupComponent>(ability))
                    entityManager.SetComponentEnabled<AbilityDestroyOnCleanupComponent>(ability, true);
                return;
            }

            return;
        }

        public static bool IsDestroyOnCleanupEnabled(Entity ability, EntityManager entityManager)
        {
            return entityManager.Exists(ability)
                   && entityManager.HasComponent<AbilityDestroyOnCleanupComponent>(ability)
                   && entityManager.IsComponentEnabled<AbilityDestroyOnCleanupComponent>(ability);
        }

        public static bool IsCancelRequested(Entity ability, EntityManager entityManager)
        {
            return entityManager.Exists(ability)
                   && entityManager.HasComponent<AbilityCancelRequestComponent>(ability)
                   && entityManager.IsComponentEnabled<AbilityCancelRequestComponent>(ability);
        }

        public static bool IsEndRequested(Entity ability, EntityManager entityManager)
        {
            return entityManager.Exists(ability)
                   && entityManager.HasComponent<AbilityEndRequestComponent>(ability)
                   && entityManager.IsComponentEnabled<AbilityEndRequestComponent>(ability);
        }

        public static void DisableLifecycleRequestMarkers(Entity ability, EntityManager entityManager)
        {
            if (!entityManager.Exists(ability))
                return;

            if (entityManager.HasComponent<AbilityActivationPendingComponent>(ability)
                && entityManager.IsComponentEnabled<AbilityActivationPendingComponent>(ability))
                entityManager.SetComponentEnabled<AbilityActivationPendingComponent>(ability, false);
            if (entityManager.HasComponent<AbilityCommitRequestComponent>(ability)
                && entityManager.IsComponentEnabled<AbilityCommitRequestComponent>(ability))
                entityManager.SetComponentEnabled<AbilityCommitRequestComponent>(ability, false);
            if (entityManager.HasComponent<AbilityCancelRequestComponent>(ability)
                && entityManager.IsComponentEnabled<AbilityCancelRequestComponent>(ability))
                entityManager.SetComponentEnabled<AbilityCancelRequestComponent>(ability, false);
            if (entityManager.HasComponent<AbilityEndRequestComponent>(ability)
                && entityManager.IsComponentEnabled<AbilityEndRequestComponent>(ability))
                entityManager.SetComponentEnabled<AbilityEndRequestComponent>(ability, false);
            if (entityManager.HasComponent<AbilityDestroyOnCleanupComponent>(ability)
                && entityManager.IsComponentEnabled<AbilityDestroyOnCleanupComponent>(ability))
                entityManager.SetComponentEnabled<AbilityDestroyOnCleanupComponent>(ability, false);
        }

        public static void RemoveTagsFromSource(Entity owner, Entity source, EntityManager entityManager)
        {
            if (!entityManager.HasComponent<TagMaskComponent>(owner) || !entityManager.HasBuffer<TagTemporarySourceBuffer>(owner))
                return;

            var sources = entityManager.GetBuffer<TagTemporarySourceBuffer>(owner);

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
            in AbilityStateComponent baseInfo,
            int gameplayEffectCode,
            int durationFrameOverride,
            string namePrefix)
        {
            if (gameplayEffectCode <= 0
                || baseInfo.Owner == Entity.Null
                || !entityManager.Exists(baseInfo.Owner)
                || ASCEntityFactory.IsDestroying(entityManager, baseInfo.Owner))
            {
                return Entity.Null;
            }

            var request = new GEApplyRequestComponent
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
                || !entityManager.HasComponent<AbilityStateComponent>(sourceAbility))
            {
                return sourceAbilityCode;
            }

            return entityManager.GetComponentData<AbilityStateComponent>(sourceAbility).Code;
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
            EventBusHelper.EnqueueGameplayEvent(entityManager, GASManager.EntityEventBus, new GameplayEventBusEventBuffer
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

        private static bool TryGetBaseInfo(EntityManager entityManager, Entity ability, out AbilityStateComponent baseInfo)
        {
            baseInfo = default;
            if (!entityManager.Exists(ability) || !entityManager.HasComponent<AbilityStateComponent>(ability))
                return false;

            baseInfo = entityManager.GetComponentData<AbilityStateComponent>(ability);
            return true;
        }

    }
}
