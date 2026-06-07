using Unity.Entities;

namespace GAS.Runtime
{
    public abstract class ConfCueBase: GameplayEffectComponentConfig
    {
        public GameplayCueConfig[] cues;

        public override bool SupportsPrototypeCache => true;
        public override bool SupportsStaticDefinitionBlob => false;

        protected static void MarkLegacyCueComponent<T>(EntityManager entityManager, Entity ge)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            entityManager.SetComponentData(ge, default(T));
            entityManager.SetComponentEnabled<T>(ge, true);
        }
    }
}
