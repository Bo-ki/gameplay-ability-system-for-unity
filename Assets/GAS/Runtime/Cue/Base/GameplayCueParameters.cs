using Unity.Entities;

namespace GAS.Runtime
{
    public enum CueSourceType
    {
        None,
        AscEntity,
        GameplayEffect,
        GameplayAbility,
    }

    public class GameplayCueParametersBase
    {
        public CueSourceType SourceType;
        public Entity entity;
    }
}
