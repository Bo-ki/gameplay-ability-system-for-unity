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

        public override void LoadToGameplayAbilityEntity(EntityManager entityManager, Entity ability)
        {
            if (EffectCodes == null || EffectCodes.Length == 0)
                return;

            var effects = entityManager.GetBuffer<AbilityOwnerEffectOnActivateBuffer>(ability);
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

        public override void LoadToGameplayAbilityEntity(EntityManager entityManager, Entity ability)
        {
            if (EffectCodes == null || EffectCodes.Length == 0)
                return;

            var effects = entityManager.GetBuffer<AbilityTargetEffectOnActivateBuffer>(ability);
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

            entityManager.SetComponentEnabled<AbilityAutoEndOnCommitComponent>(ability, AutoEndOnCommit);
        }
    }

    public sealed class ConfAbilityMoveInput : AbilityComponentConfig
    {
        public float RotationOffset;

        public override void LoadToGameplayAbilityEntity(EntityManager entityManager, Entity ability)
        {
            entityManager.SetComponentData(ability, new AbilityMoveInputComponent { RotationOffset = RotationOffset });
            entityManager.SetComponentEnabled<AbilityMoveInputComponent>(ability, true);
        }
    }
}
