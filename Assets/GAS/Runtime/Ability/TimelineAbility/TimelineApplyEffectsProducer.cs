using System.Collections.Generic;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Produces ECS GameplayEffect requests from Timeline ApplyEffects actions.
    /// </summary>
    public static class TimelineApplyEffectsProducer
    {
        private static EntityManager EntityManager => GASManager.EntityManager;

        public static int RequestApplyEffects(
            Entity ability,
            Entity mainTarget,
            XParamApplyEffects parameter,
            List<Entity> createdRequests = null)
        {
            return RequestApplyEffects(EntityManager, ability, mainTarget, parameter, createdRequests);
        }

        public static int RequestApplyEffects(
            EntityManager entityManager,
            Entity ability,
            Entity mainTarget,
            XParamApplyEffects parameter,
            List<Entity> createdRequests = null)
        {
            if (parameter == null
                || parameter.IDs == null
                || parameter.IDs.Length == 0
                || string.IsNullOrEmpty(parameter.CatcherType))
                return 0;

            var catcher = TargetCatcherHelper.TryCreateTargetCatcher(parameter.CatcherType);
            if (catcher == null)
                return 0;

            return RequestApplyEffects(
                entityManager,
                ability,
                mainTarget,
                parameter.IDs,
                catcher,
                parameter.Param,
                createdRequests);
        }

        internal static int RequestApplyEffects(
            EntityManager entityManager,
            Entity ability,
            Entity mainTarget,
            IReadOnlyList<int> effectCodes,
            TargetCatcherBase targetCatcher,
            XParam targetCatcherParameter = null,
            List<Entity> createdRequests = null)
        {
            if (effectCodes == null
                || effectCodes.Count == 0
                || targetCatcher == null
                || !TryGetBaseInfo(entityManager, ability, out var baseInfo)
                || !IsAvailableAsc(entityManager, baseInfo.Owner))
                return 0;

            var caughtTargets = new List<Entity>();
            var validTargets = new List<Entity>();

            targetCatcher.Init(baseInfo.Owner);
            if (targetCatcherParameter != null)
                targetCatcher.InitParameters(targetCatcherParameter);

            targetCatcher.CatchTargetsNonAllocSafe(mainTarget, ref caughtTargets);
            for (var i = 0; i < caughtTargets.Count; i++)
            {
                var target = caughtTargets[i];
                if (IsAvailableAsc(entityManager, target))
                    validTargets.Add(target);
            }

            if (validTargets.Count == 0)
                return 0;

            var createdCount = 0;
            var targetKind = ResolveTargetKind(targetCatcher, baseInfo.Owner, validTargets);

            for (var i = 0; i < effectCodes.Count; i++)
            {
                var effectCode = effectCodes[i];
                if (effectCode <= 0)
                    continue;

                var requestData = new CApplyGameplayEffectRequest
                {
                    SourceAsc = baseInfo.Owner,
                    SourceAbility = ability,
                    Instigator = baseInfo.Owner,
                    Causer = ability,
                    GameplayEffectCode = effectCode,
                    Level = baseInfo.Level,
                };

                var request = GameplayEffectRequestWriter.AppendSimpleInstantCommandsOrCreateTargetListRequest(
                    entityManager,
                    requestData,
                    validTargets,
                    targetKind,
                    "TimelineApplyEffectsRequest");

                if (request != Entity.Null)
                    createdRequests?.Add(request);

                createdCount++;
            }

            return createdCount;
        }

        private static bool TryGetBaseInfo(
            EntityManager entityManager,
            Entity ability,
            out CAbilityBaseInfo baseInfo)
        {
            baseInfo = default;
            if (!entityManager.Exists(ability) || !entityManager.HasComponent<CAbilityBaseInfo>(ability))
                return false;

            baseInfo = entityManager.GetComponentData<CAbilityBaseInfo>(ability);
            return true;
        }

        private static bool IsAvailableAsc(EntityManager entityManager, Entity asc)
        {
            return asc != Entity.Null
                   && entityManager.Exists(asc)
                   && !entityManager.HasComponent<CAscDestroying>(asc);
        }

        private static ETargetDataKind ResolveTargetKind(
            TargetCatcherBase targetCatcher,
            Entity sourceAsc,
            List<Entity> targets)
        {
            if (targets.Count > 1)
                return ETargetDataKind.EntityList;

            return targetCatcher is CatchSelf && targets[0] == sourceAsc
                ? ETargetDataKind.Self
                : ETargetDataKind.Entity;
        }
    }
}
