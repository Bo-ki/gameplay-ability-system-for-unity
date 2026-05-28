using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Period 的静态定义。由配置/prototype/Blob 共享，不承载 tick 游标。
    /// </summary>
    public struct GEPeriodDefinitionComponent : IComponentData, IEnableableComponent
    {
        public int Period;
        public bool ResetTimeCountWhenDeactivated;
    }

    /// <summary>
    /// Period 的运行时游标。只属于 runtime GE instance。
    /// </summary>
    public struct GEPeriodRuntimeComponent : IComponentData, IEnableableComponent
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
            em.SetComponentData(ge, new GEPeriodDefinitionComponent
            {
                Period = Period,
                ResetTimeCountWhenDeactivated = ResetTimeCountWhenDeactivated,
            });
            em.SetComponentEnabled<GEPeriodDefinitionComponent>(ge, true);

            if (GameplayEffectCodes == null || GameplayEffectCodes.Length == 0)
                return;

            var periodGEs = em.GetBuffer<GEPeriodConfigBuffer>(ge);
            periodGEs.Clear();
            foreach (var effectCode in GameplayEffectCodes)
            {
                if (effectCode > 0)
                    periodGEs.Add(new GEPeriodConfigBuffer { GameplayEffectCode = effectCode });
            }
            em.SetComponentEnabled<GEPeriodConfigBuffer>(ge, periodGEs.Length > 0);
        }
    }
}
