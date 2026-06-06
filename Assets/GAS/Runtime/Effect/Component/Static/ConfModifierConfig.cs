using System;
using Unity.Entities;

namespace GAS.Runtime
{
    public sealed class ConfModifierConfig : GameplayEffectComponentConfig
    {
        public ModifierDefinitionSetting[] ModifierSettings;

        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            var modifiers = entityManager.GetBuffer<GEModifierConfigBuffer>(ge);
            modifiers.Clear();

            if (ModifierSettings == null)
            {
                entityManager.SetComponentEnabled<GEModifierConfigBuffer>(ge, false);
                return;
            }

            for (var i = 0; i < ModifierSettings.Length; i++)
            {
                var setting = ModifierSettings[i];
                modifiers.Add(new GEModifierConfigBuffer
                {
                    AttrSetCode = setting.AttrSetCode,
                    AttributeCode = setting.AttrCode,
                    Magnitude = setting.Magnitude,
                    Op = setting.Operation,
                });
            }
            entityManager.SetComponentEnabled<GEModifierConfigBuffer>(ge, modifiers.Length > 0);
        }
    }

    [Serializable]
    public struct ModifierDefinitionSetting
    {
        public int AttrSetCode;
        public int AttrCode;
        public EModifierOp Operation;
        public float Magnitude;
    }
}
