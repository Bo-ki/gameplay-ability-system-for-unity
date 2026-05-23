using System;
using Unity.Entities;

namespace GAS.Runtime
{
    public sealed class ConfModifierConfig : GameplayEffectComponentConfig
    {
        public ModifierDefinitionSetting[] ModifierSettings;

        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            if (!GASManager.EntityManager.HasBuffer<BModifierConfig>(ge))
                GASManager.EntityManager.AddBuffer<BModifierConfig>(ge);

            var modifiers = GASManager.EntityManager.GetBuffer<BModifierConfig>(ge);
            modifiers.Clear();

            if (ModifierSettings == null)
                return;

            for (var i = 0; i < ModifierSettings.Length; i++)
            {
                var setting = ModifierSettings[i];
                modifiers.Add(new BModifierConfig
                {
                    AttrSetCode = setting.AttrSetCode,
                    AttributeCode = setting.AttrCode,
                    Magnitude = setting.Magnitude,
                    Op = setting.Operation,
                });
            }
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
