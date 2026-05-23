using Unity.Entities;

namespace GAS.Runtime
{
    public struct CRemoveGameplayEffectRequest : IComponentData
    {
        public Entity GameplayEffect;
    }
}
