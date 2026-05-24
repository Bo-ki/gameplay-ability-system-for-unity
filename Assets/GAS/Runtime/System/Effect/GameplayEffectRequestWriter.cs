using System.Collections.Generic;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 统一写入 ApplyGameplayEffect request，避免各 producer 手写不同结构。
    /// </summary>
    internal static class GameplayEffectRequestWriter
    {
        public static Entity Create(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            in CTargetDataHeader targetDataHeader,
            string namePrefix = "ApplyGERequest")
        {
            var requestEntity = em.CreateEntity();
            em.AddComponentData(requestEntity, request);
            em.AddComponentData(requestEntity, targetDataHeader);
            return requestEntity;
        }

        public static Entity Create(
            ref EntityCommandBuffer ecb,
            in CApplyGameplayEffectRequest request,
            in CTargetDataHeader targetDataHeader,
            string namePrefix = "ApplyGERequest")
        {
            var requestEntity = ecb.CreateEntity();
            ecb.AddComponent(requestEntity, request);
            ecb.AddComponent(requestEntity, targetDataHeader);
            return requestEntity;
        }

        public static void AddTarget(EntityManager em, Entity requestEntity, Entity targetAsc)
        {
            var targets = em.HasBuffer<BTargetEntity>(requestEntity)
                ? em.GetBuffer<BTargetEntity>(requestEntity)
                : em.AddBuffer<BTargetEntity>(requestEntity);

            targets.Add(new BTargetEntity { TargetAsc = targetAsc });
        }

        public static bool TryApplyLegacyInstantModifierBypass(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind)
        {
            return SApplyGameplayEffectRequest.TryApplyLegacyInstantModifierBypassDirect(
                em,
                request,
                targetAsc,
                targetDataKind);
        }

        public static bool TryApplyLegacyInstantModifierBypass(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            in BSetByCallerValue setByCallerValue)
        {
            return SApplyGameplayEffectRequest.TryApplyLegacyInstantModifierBypassDirect(
                em,
                request,
                targetAsc,
                targetDataKind,
                setByCallerValue);
        }

        public static Entity ApplyLegacyInstantBypassOrCreateSingleTargetRequest(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            string namePrefix = "ApplyGERequest")
        {
            if (TryApplyLegacyInstantModifierBypass(em, request, targetAsc, targetDataKind))
                return Entity.Null;

            var requestEntity = Create(
                em,
                request,
                new CTargetDataHeader
                {
                    SourceAsc = request.SourceAsc,
                    SourceAbility = request.SourceAbility,
                    Kind = targetDataKind,
                },
                namePrefix);
            AddTarget(em, requestEntity, targetAsc);
            return requestEntity;
        }

        public static Entity AppendSimpleInstantCommandOrCreateSingleTargetRequest(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            string namePrefix = "ApplyGERequest")
        {
            if (TryAppendSimpleInstantCommand(em, request, targetAsc, targetDataKind, null))
                return Entity.Null;

            var requestEntity = Create(
                em,
                request,
                new CTargetDataHeader
                {
                    SourceAsc = request.SourceAsc,
                    SourceAbility = request.SourceAbility,
                    Kind = targetDataKind,
                },
                namePrefix);
            AddTarget(em, requestEntity, targetAsc);
            return requestEntity;
        }

        public static Entity ApplyLegacyInstantBypassOrCreateSingleTargetRequest(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            in BSetByCallerValue setByCallerValue,
            string namePrefix = "ApplyGERequest")
        {
            var canUseSetByCaller = setByCallerValue.Key > 0;
            var appliedLegacy = canUseSetByCaller
                ? TryApplyLegacyInstantModifierBypass(em, request, targetAsc, targetDataKind, setByCallerValue)
                : TryApplyLegacyInstantModifierBypass(em, request, targetAsc, targetDataKind);
            if (appliedLegacy)
                return Entity.Null;

            var requestEntity = Create(
                em,
                request,
                new CTargetDataHeader
                {
                    SourceAsc = request.SourceAsc,
                    SourceAbility = request.SourceAbility,
                    Kind = targetDataKind,
                },
                namePrefix);
            AddTarget(em, requestEntity, targetAsc);

            if (!canUseSetByCaller)
                return requestEntity;

            em.AddBuffer<BSetByCallerValue>(requestEntity).Add(setByCallerValue);
            return requestEntity;
        }

        public static Entity AppendSimpleInstantCommandOrCreateSingleTargetRequest(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            in BSetByCallerValue setByCallerValue,
            string namePrefix = "ApplyGERequest")
        {
            var canUseSetByCaller = setByCallerValue.Key > 0;
            var setByCallerValues = canUseSetByCaller
                ? new[] { setByCallerValue }
                : null;

            if (TryAppendSimpleInstantCommand(em, request, targetAsc, targetDataKind, setByCallerValues))
                return Entity.Null;

            var requestEntity = Create(
                em,
                request,
                new CTargetDataHeader
                {
                    SourceAsc = request.SourceAsc,
                    SourceAbility = request.SourceAbility,
                    Kind = targetDataKind,
                },
                namePrefix);
            AddTarget(em, requestEntity, targetAsc);

            if (!canUseSetByCaller)
                return requestEntity;

            em.AddBuffer<BSetByCallerValue>(requestEntity).Add(setByCallerValue);
            return requestEntity;
        }

        public static bool TryAppendSimpleInstantCommand(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            IReadOnlyList<BSetByCallerValue> setByCallerValues)
        {
            if (targetAsc == Entity.Null)
                return false;

            var command = EffectCommandSpecStream.ToCommand(
                request,
                targetAsc,
                targetDataKind,
                ResolveCommandSource(in request));

            if (command.Kind != EEffectCommandKind.Instant
                || !EffectCommandSpecStreamPhaseUtility.CanBuildSimpleInstantSpec(em, in command))
            {
                return false;
            }

            EffectCommandSpecStream.AppendCommand(em, command, setByCallerValues);
            return true;
        }

        public static void AddTarget(ref EntityCommandBuffer ecb, Entity requestEntity, Entity targetAsc)
        {
            var targets = ecb.AddBuffer<BTargetEntity>(requestEntity);
            targets.Add(new BTargetEntity { TargetAsc = targetAsc });
        }

        public static void AddSetByCallerValues(
            EntityManager em,
            Entity requestEntity,
            IReadOnlyList<BSetByCallerValue> setByCallerValues)
        {
            if (setByCallerValues == null || setByCallerValues.Count == 0)
                return;

            var values = em.HasBuffer<BSetByCallerValue>(requestEntity)
                ? em.GetBuffer<BSetByCallerValue>(requestEntity)
                : em.AddBuffer<BSetByCallerValue>(requestEntity);

            for (var i = 0; i < setByCallerValues.Count; i++)
                values.Add(setByCallerValues[i]);
        }

        public static void AddSetByCallerValues(
            EntityManager em,
            Entity requestEntity,
            DynamicBuffer<BSetByCallerValue> setByCallerValues)
        {
            if (!setByCallerValues.IsCreated || setByCallerValues.Length == 0)
                return;

            var values = em.HasBuffer<BSetByCallerValue>(requestEntity)
                ? em.GetBuffer<BSetByCallerValue>(requestEntity)
                : em.AddBuffer<BSetByCallerValue>(requestEntity);

            for (var i = 0; i < setByCallerValues.Length; i++)
                values.Add(setByCallerValues[i]);
        }

        public static void AddSetByCallerValues(
            ref EntityCommandBuffer ecb,
            Entity requestEntity,
            DynamicBuffer<BSetByCallerValue> setByCallerValues)
        {
            if (!setByCallerValues.IsCreated || setByCallerValues.Length == 0)
                return;

            var values = ecb.AddBuffer<BSetByCallerValue>(requestEntity);
            for (var i = 0; i < setByCallerValues.Length; i++)
                values.Add(setByCallerValues[i]);
        }

        private static EEffectCommandSource ResolveCommandSource(in CApplyGameplayEffectRequest request)
        {
            return request.SourceAbility != Entity.Null
                ? EEffectCommandSource.Ability
                : EEffectCommandSource.RuntimeBoundary;
        }
    }
}
