using System;
using Unity.Entities;

namespace GAS.Runtime
{
    public struct BAbilityEffectOnActivate : IBufferElementData
    {
        public int EffectCode;
    }

    public struct CAbilityTimelineRef : IComponentData
    {
        public int TimelineId;
    }

    public struct CAbilityMoveInput : IComponentData
    {
        public float RotationOffset;
    }

    public sealed class ConfAbilityEffectsOnActivate : AbilityComponentConfig
    {
        public int[] EffectCodes = Array.Empty<int>();

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            if (EffectCodes == null || EffectCodes.Length == 0)
                return;

            var effects = _entityManager.HasBuffer<BAbilityEffectOnActivate>(ability)
                ? _entityManager.GetBuffer<BAbilityEffectOnActivate>(ability)
                : _entityManager.AddBuffer<BAbilityEffectOnActivate>(ability);

            for (var i = 0; i < EffectCodes.Length; i++)
            {
                if (EffectCodes[i] <= 0) continue;
                effects.Add(new BAbilityEffectOnActivate { EffectCode = EffectCodes[i] });
            }
        }
    }

    public sealed class ConfAbilityTimelineRef : AbilityComponentConfig
    {
        public int TimelineId;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            if (TimelineId <= 0)
                return;

            _entityManager.AddComponentData(ability, new CAbilityTimelineRef { TimelineId = TimelineId });
        }
    }

    public sealed class ConfAbilityMoveInput : AbilityComponentConfig
    {
        public float RotationOffset;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.AddComponentData(ability, new CAbilityMoveInput { RotationOffset = RotationOffset });
        }
    }
}
