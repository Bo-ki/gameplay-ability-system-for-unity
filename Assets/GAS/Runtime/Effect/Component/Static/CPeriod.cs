using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Period 的静态定义。由配置/prototype/Blob 共享，不承载 tick 游标。
    /// </summary>
    public struct CPeriodDefinition : IComponentData
    {
        public int Period;
        public bool ResetTimeCountWhenDeactivated;
    }

    /// <summary>
    /// Period 的运行时游标。只属于 runtime GE instance。
    /// </summary>
    public struct CPeriodRuntime : IComponentData
    {
        public int StartTime;
    }

    public sealed class ConfPeriod : GameplayEffectComponentConfig
    {
        public int Period;
        public bool ResetTimeCountWhenDeactivated;
        public int[] GameplayEffectCodes;

        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var em = GASManager.EntityManager;
            em.AddComponentData(ge, new CPeriodDefinition
            {
                Period = Period,
                ResetTimeCountWhenDeactivated = ResetTimeCountWhenDeactivated,
            });

            if (GameplayEffectCodes == null || GameplayEffectCodes.Length == 0)
                return;

            var periodGEs = em.AddBuffer<BPeriodGEConfig>(ge);
            foreach (var effectCode in GameplayEffectCodes)
            {
                if (effectCode > 0)
                    periodGEs.Add(new BPeriodGEConfig { GameplayEffectCode = effectCode });
            }
        }
    }
}
