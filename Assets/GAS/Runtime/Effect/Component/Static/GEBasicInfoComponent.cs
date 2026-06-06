using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public struct GEBasicInfoComponent : IComponentData, IEnableableComponent
    {
        /// <summary>
        /// 仅调试显示用，不建议作为运算逻辑的依据
        /// </summary>
        public FixedString32Bytes name; 
        
        // -------------------------------------以下是RUNTIME数据，不需要初始化---------------------------------------//

    }
    
    public sealed class ConfEffectBasicInfo:GameplayEffectComponentConfig
    {
        public string Name;

        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            var em = entityManager;
            em.SetName(ge, $"GE_{Name}_V{ge.Version}_{ge.Index}");
            em.SetComponentData(ge, new GEBasicInfoComponent
            {
                name = Name
            });
            em.SetComponentEnabled<GEBasicInfoComponent>(ge, true);
        }
    }
}
