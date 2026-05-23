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
            em.SetName(requestEntity, $"{namePrefix}_{request.GameplayEffectCode}_{requestEntity.Index}");
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
            ecb.SetName(requestEntity, $"{namePrefix}_{request.GameplayEffectCode}");
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
    }
}
