using System;
using Unity.Entities;

namespace GAS.Runtime
{
    [InternalBufferCapacity(4)]
    public struct AbilityOwnerEffectOnActivateBuffer : IBufferElementData
    {
        public int EffectCode;
    }

    [InternalBufferCapacity(4)]
    public struct AbilityTargetEffectOnActivateBuffer : IBufferElementData
    {
        public int EffectCode;
    }

    public struct AbilityAutoEndOnCommitComponent : IComponentData, IEnableableComponent
    {
    }

    public struct AbilityMoveInputComponent : IComponentData, IEnableableComponent
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

            var effects = _entityManager.GetBuffer<AbilityOwnerEffectOnActivateBuffer>(ability);
            effects.Clear();

            for (var i = 0; i < EffectCodes.Length; i++)
            {
                if (EffectCodes[i] <= 0) continue;
                effects.Add(new AbilityOwnerEffectOnActivateBuffer { EffectCode = EffectCodes[i] });
            }
        }
    }

    public sealed class ConfAbilityTargetEffectsOnActivate : AbilityComponentConfig
    {
        public int[] EffectCodes = Array.Empty<int>();
        public bool AutoEndOnCommit = true;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            if (EffectCodes == null || EffectCodes.Length == 0)
                return;

            var effects = _entityManager.GetBuffer<AbilityTargetEffectOnActivateBuffer>(ability);
            effects.Clear();

            for (var i = 0; i < EffectCodes.Length; i++)
            {
                if (EffectCodes[i] <= 0)
                    continue;

                effects.Add(new AbilityTargetEffectOnActivateBuffer
                {
                    EffectCode = EffectCodes[i],
                });
            }

            _entityManager.SetComponentEnabled<AbilityAutoEndOnCommitComponent>(ability, AutoEndOnCommit);
        }
    }

    public sealed class ConfAbilityMoveInput : AbilityComponentConfig
    {
        public float RotationOffset;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.SetComponentData(ability, new AbilityMoveInputComponent { RotationOffset = RotationOffset });
            _entityManager.SetComponentEnabled<AbilityMoveInputComponent>(ability, true);
        }
    }
}
