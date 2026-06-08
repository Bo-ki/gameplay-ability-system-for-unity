using Unity.Entities;

namespace GAS.Runtime
{
    internal static class AbilityRuntimeActions
    {
        internal static Entity RequestCostGameplayEffect(Entity ability, EntityManager entityManager)
        {
            if (!TryGetBaseInfo(entityManager, ability, out var baseInfo)
                || !entityManager.HasComponent<AbilityCostComponent>(ability)
                || !entityManager.IsComponentEnabled<AbilityCostComponent>(ability))
                return Entity.Null;

            var cost = entityManager.GetComponentData<AbilityCostComponent>(ability);
            return AppendSimpleInstantSelfCommandToStream(
                entityManager,
                ability,
                baseInfo,
                cost.GameplayEffectCode,
                durationFrameOverride: 0);
        }

        internal static Entity RequestCooldownGameplayEffect(Entity ability, EntityManager entityManager)
        {
            if (!TryGetBaseInfo(entityManager, ability, out var baseInfo)
                || !entityManager.HasComponent<AbilityCooldownComponent>(ability)
                || !entityManager.IsComponentEnabled<AbilityCooldownComponent>(ability))
                return Entity.Null;

            var cooldown = entityManager.GetComponentData<AbilityCooldownComponent>(ability);
            return AppendSimpleInstantSelfCommandToStream(
                entityManager,
                ability,
                baseInfo,
                cooldown.GameplayEffectCode,
                cooldown.Cooldown);
        }

        internal static void RemoveActivationOwnedTags(Entity ability, EntityManager entityManager)
        {
            if (!entityManager.HasComponent<AbilityStateComponent>(ability)) return;
            var owner = entityManager.GetComponentData<AbilityStateComponent>(ability).Owner;
            RemoveTagsFromSource(owner, ability, entityManager);
        }

        internal static bool RequestAbilityEnd(
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

        internal static bool RequestAbilityCancel(
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

        internal static bool RequestAbilityCancel(
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

        internal static void EnableDestroyOnCleanup(
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

        internal static bool IsDestroyOnCleanupEnabled(Entity ability, EntityManager entityManager)
        {
            return entityManager.Exists(ability)
                   && entityManager.HasComponent<AbilityDestroyOnCleanupComponent>(ability)
                   && entityManager.IsComponentEnabled<AbilityDestroyOnCleanupComponent>(ability);
        }

        internal static bool IsCancelRequested(Entity ability, EntityManager entityManager)
        {
            return entityManager.Exists(ability)
                   && entityManager.HasComponent<AbilityCancelRequestComponent>(ability)
                   && entityManager.IsComponentEnabled<AbilityCancelRequestComponent>(ability);
        }

        internal static bool IsEndRequested(Entity ability, EntityManager entityManager)
        {
            return entityManager.Exists(ability)
                   && entityManager.HasComponent<AbilityEndRequestComponent>(ability)
                   && entityManager.IsComponentEnabled<AbilityEndRequestComponent>(ability);
        }

        internal static void DisableLifecycleRequestMarkers(Entity ability, EntityManager entityManager)
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

        internal static void RemoveTagsFromSource(Entity owner, Entity source, EntityManager entityManager)
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

        private static Entity AppendSimpleInstantSelfCommandToStream(
            EntityManager entityManager,
            Entity ability,
            in AbilityStateComponent baseInfo,
            int gameplayEffectCode,
            int durationFrameOverride)
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

            return GameplayEffectRequestWriter.AppendSimpleInstantCommandToStream(
                entityManager,
                request,
                baseInfo.Owner,
                ETargetDataKind.Self);
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
            if (!EffectCommandSpecStream.TryGetSingleton(entityManager, out var streamEntity))
                return;

            var factWriter = EffectCommandSpecStream.BeginOwnerLocalFactWriter(
                entityManager,
                streamEntity,
                GASRuntimeFrameContext.ResolveCurrentFrame(entityManager));
            if (!factWriter.IsCreated)
                return;

            factWriter.AppendFact(new GameplayEventBuffer
            {
                EventType = type,
                Domain = EGameplayFactDomain.Ability,
                Category = EGameplayFactCategory.Request,
                Severity = EGameplayFactSeverity.Info,
                SourceAsc = baseInfo.Owner,
                TargetAsc = baseInfo.Owner,
                SourceAbility = ability,
                SourceEffect = sourceEffect,
                EventCode = baseInfo.Code,
                ReasonCode = (int)reason,
                Value = sourceAbilityCode,
            });
            factWriter.Flush();
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
