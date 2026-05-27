///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using Unity.Entities;

namespace GAS.Runtime.Generated
{
    public struct AutoChessAttributeDefinition
    {
        public int AttributeSetCode;
        public int AttributeCode;
        public float InitialValue;
        public bool IsClampMin;
        public bool IsClampMax;
        public float MinValue;
        public float MaxValue;
    }

    public struct HealthAttribute : IComponentData
    {
        public const int AttributeSetCode = 9601;
        public const int AttributeCode = 1;

        public float BaseValue;
        public float CurrentValue;
        public float PreviousCurrentValue;
        public bool IsClampMin;
        public bool IsClampMax;
        public float MinValue;
        public float MaxValue;
        public bool Dirty;
        public bool CurrentValueChangePending;

        public static HealthAttribute Create(float initialValue = 60f)
        {
            var value = AutoChessAttributeAccessor.Clamp(AttributeCode, initialValue);
            return new HealthAttribute
            {
                BaseValue = value,
                CurrentValue = value,
                PreviousCurrentValue = value,
                IsClampMin = true,
                IsClampMax = true,
                MinValue = 0f,
                MaxValue = 100f,
                Dirty = true,
                CurrentValueChangePending = true,
            };
        }
    }

    public struct ManaAttribute : IComponentData
    {
        public const int AttributeSetCode = 9601;
        public const int AttributeCode = 2;

        public float BaseValue;
        public float CurrentValue;
        public float PreviousCurrentValue;
        public bool IsClampMin;
        public bool IsClampMax;
        public float MinValue;
        public float MaxValue;
        public bool Dirty;
        public bool CurrentValueChangePending;

        public static ManaAttribute Create(float initialValue = 0f)
        {
            var value = AutoChessAttributeAccessor.Clamp(AttributeCode, initialValue);
            return new ManaAttribute
            {
                BaseValue = value,
                CurrentValue = value,
                PreviousCurrentValue = value,
                IsClampMin = true,
                IsClampMax = true,
                MinValue = 0f,
                MaxValue = 10f,
                Dirty = true,
                CurrentValueChangePending = true,
            };
        }
    }

    public struct ShieldAttribute : IComponentData
    {
        public const int AttributeSetCode = 9601;
        public const int AttributeCode = 3;

        public float BaseValue;
        public float CurrentValue;
        public float PreviousCurrentValue;
        public bool IsClampMin;
        public bool IsClampMax;
        public float MinValue;
        public float MaxValue;
        public bool Dirty;
        public bool CurrentValueChangePending;

        public static ShieldAttribute Create(float initialValue = 0f)
        {
            var value = AutoChessAttributeAccessor.Clamp(AttributeCode, initialValue);
            return new ShieldAttribute
            {
                BaseValue = value,
                CurrentValue = value,
                PreviousCurrentValue = value,
                IsClampMin = true,
                IsClampMax = true,
                MinValue = 0f,
                MaxValue = 32f,
                Dirty = true,
                CurrentValueChangePending = true,
            };
        }
    }

    public struct ArcaneResistanceAttribute : IComponentData
    {
        public const int AttributeSetCode = 9601;
        public const int AttributeCode = 4;

        public float BaseValue;
        public float CurrentValue;
        public float PreviousCurrentValue;
        public bool IsClampMin;
        public bool IsClampMax;
        public float MinValue;
        public float MaxValue;
        public bool Dirty;
        public bool CurrentValueChangePending;

        public static ArcaneResistanceAttribute Create(float initialValue = 0f)
        {
            var value = AutoChessAttributeAccessor.Clamp(AttributeCode, initialValue);
            return new ArcaneResistanceAttribute
            {
                BaseValue = value,
                CurrentValue = value,
                PreviousCurrentValue = value,
                IsClampMin = true,
                IsClampMax = true,
                MinValue = 0f,
                MaxValue = 0.75f,
                Dirty = true,
                CurrentValueChangePending = true,
            };
        }
    }

    public struct CounterDamageAttribute : IComponentData
    {
        public const int AttributeSetCode = 9601;
        public const int AttributeCode = 5;

        public float BaseValue;
        public float CurrentValue;
        public float PreviousCurrentValue;
        public bool IsClampMin;
        public bool IsClampMax;
        public float MinValue;
        public float MaxValue;
        public bool Dirty;
        public bool CurrentValueChangePending;

        public static CounterDamageAttribute Create(float initialValue = 0f)
        {
            var value = AutoChessAttributeAccessor.Clamp(AttributeCode, initialValue);
            return new CounterDamageAttribute
            {
                BaseValue = value,
                CurrentValue = value,
                PreviousCurrentValue = value,
                IsClampMin = true,
                IsClampMax = true,
                MinValue = 0f,
                MaxValue = 6f,
                Dirty = true,
                CurrentValueChangePending = true,
            };
        }
    }

    public struct LifeStealRatioAttribute : IComponentData
    {
        public const int AttributeSetCode = 9601;
        public const int AttributeCode = 6;

        public float BaseValue;
        public float CurrentValue;
        public float PreviousCurrentValue;
        public bool IsClampMin;
        public bool IsClampMax;
        public float MinValue;
        public float MaxValue;
        public bool Dirty;
        public bool CurrentValueChangePending;

        public static LifeStealRatioAttribute Create(float initialValue = 0f)
        {
            var value = AutoChessAttributeAccessor.Clamp(AttributeCode, initialValue);
            return new LifeStealRatioAttribute
            {
                BaseValue = value,
                CurrentValue = value,
                PreviousCurrentValue = value,
                IsClampMin = true,
                IsClampMax = true,
                MinValue = 0f,
                MaxValue = 1f,
                Dirty = true,
                CurrentValueChangePending = true,
            };
        }
    }

    public static class AutoChessAttributeCodes
    {
        public const int Health = 1;
        public const int Mana = 2;
        public const int Shield = 3;
        public const int ArcaneResistance = 4;
        public const int CounterDamage = 5;
        public const int LifeStealRatio = 6;
    }

    public static class AutoChessAttributeAccessor
    {
        public const int DefinitionCount = 6;

        public static bool TryGetDefinition(int attributeCode, out AutoChessAttributeDefinition definition)
        {
            switch (attributeCode)
            {
                case AutoChessAttributeCodes.Health:
                    definition = new AutoChessAttributeDefinition
                    {
                        AttributeSetCode = 9601,
                        AttributeCode = 1,
                        InitialValue = 60f,
                        IsClampMin = true,
                        IsClampMax = true,
                        MinValue = 0f,
                        MaxValue = 100f,
                    };
                    return true;
                case AutoChessAttributeCodes.Mana:
                    definition = new AutoChessAttributeDefinition
                    {
                        AttributeSetCode = 9601,
                        AttributeCode = 2,
                        InitialValue = 0f,
                        IsClampMin = true,
                        IsClampMax = true,
                        MinValue = 0f,
                        MaxValue = 10f,
                    };
                    return true;
                case AutoChessAttributeCodes.Shield:
                    definition = new AutoChessAttributeDefinition
                    {
                        AttributeSetCode = 9601,
                        AttributeCode = 3,
                        InitialValue = 0f,
                        IsClampMin = true,
                        IsClampMax = true,
                        MinValue = 0f,
                        MaxValue = 32f,
                    };
                    return true;
                case AutoChessAttributeCodes.ArcaneResistance:
                    definition = new AutoChessAttributeDefinition
                    {
                        AttributeSetCode = 9601,
                        AttributeCode = 4,
                        InitialValue = 0f,
                        IsClampMin = true,
                        IsClampMax = true,
                        MinValue = 0f,
                        MaxValue = 0.75f,
                    };
                    return true;
                case AutoChessAttributeCodes.CounterDamage:
                    definition = new AutoChessAttributeDefinition
                    {
                        AttributeSetCode = 9601,
                        AttributeCode = 5,
                        InitialValue = 0f,
                        IsClampMin = true,
                        IsClampMax = true,
                        MinValue = 0f,
                        MaxValue = 6f,
                    };
                    return true;
                case AutoChessAttributeCodes.LifeStealRatio:
                    definition = new AutoChessAttributeDefinition
                    {
                        AttributeSetCode = 9601,
                        AttributeCode = 6,
                        InitialValue = 0f,
                        IsClampMin = true,
                        IsClampMax = true,
                        MinValue = 0f,
                        MaxValue = 1f,
                    };
                    return true;
                default:
                    definition = default;
                    return false;
            }
        }

        public static float Clamp(int attributeCode, float value)
        {
            if (!TryGetDefinition(attributeCode, out var definition)) return value;
            if (definition.IsClampMin && value < definition.MinValue) value = definition.MinValue;
            if (definition.IsClampMax && value > definition.MaxValue) value = definition.MaxValue;
            return value;
        }

        public static float GetCurrentValue(int attributeCode, in HealthAttribute health, in ManaAttribute mana, in ShieldAttribute shield, in ArcaneResistanceAttribute arcaneResistance, in CounterDamageAttribute counterDamage, in LifeStealRatioAttribute lifeStealRatio)
        {
            switch (attributeCode)
            {
                case AutoChessAttributeCodes.Health: return health.CurrentValue;
                case AutoChessAttributeCodes.Mana: return mana.CurrentValue;
                case AutoChessAttributeCodes.Shield: return shield.CurrentValue;
                case AutoChessAttributeCodes.ArcaneResistance: return arcaneResistance.CurrentValue;
                case AutoChessAttributeCodes.CounterDamage: return counterDamage.CurrentValue;
                case AutoChessAttributeCodes.LifeStealRatio: return lifeStealRatio.CurrentValue;
                default: return 0f;
            }
        }

        public static bool SetCurrentValue(int attributeCode, ref HealthAttribute health, ref ManaAttribute mana, ref ShieldAttribute shield, ref ArcaneResistanceAttribute arcaneResistance, ref CounterDamageAttribute counterDamage, ref LifeStealRatioAttribute lifeStealRatio, float value)
        {
            switch (attributeCode)
            {
                case AutoChessAttributeCodes.Health:
                    health.PreviousCurrentValue = health.CurrentValue;
                    health.CurrentValue = Clamp(attributeCode, value);
                    health.CurrentValueChangePending = health.CurrentValue != health.PreviousCurrentValue;
                    health.Dirty = true;
                    return true;
                case AutoChessAttributeCodes.Mana:
                    mana.PreviousCurrentValue = mana.CurrentValue;
                    mana.CurrentValue = Clamp(attributeCode, value);
                    mana.CurrentValueChangePending = mana.CurrentValue != mana.PreviousCurrentValue;
                    mana.Dirty = true;
                    return true;
                case AutoChessAttributeCodes.Shield:
                    shield.PreviousCurrentValue = shield.CurrentValue;
                    shield.CurrentValue = Clamp(attributeCode, value);
                    shield.CurrentValueChangePending = shield.CurrentValue != shield.PreviousCurrentValue;
                    shield.Dirty = true;
                    return true;
                case AutoChessAttributeCodes.ArcaneResistance:
                    arcaneResistance.PreviousCurrentValue = arcaneResistance.CurrentValue;
                    arcaneResistance.CurrentValue = Clamp(attributeCode, value);
                    arcaneResistance.CurrentValueChangePending = arcaneResistance.CurrentValue != arcaneResistance.PreviousCurrentValue;
                    arcaneResistance.Dirty = true;
                    return true;
                case AutoChessAttributeCodes.CounterDamage:
                    counterDamage.PreviousCurrentValue = counterDamage.CurrentValue;
                    counterDamage.CurrentValue = Clamp(attributeCode, value);
                    counterDamage.CurrentValueChangePending = counterDamage.CurrentValue != counterDamage.PreviousCurrentValue;
                    counterDamage.Dirty = true;
                    return true;
                case AutoChessAttributeCodes.LifeStealRatio:
                    lifeStealRatio.PreviousCurrentValue = lifeStealRatio.CurrentValue;
                    lifeStealRatio.CurrentValue = Clamp(attributeCode, value);
                    lifeStealRatio.CurrentValueChangePending = lifeStealRatio.CurrentValue != lifeStealRatio.PreviousCurrentValue;
                    lifeStealRatio.Dirty = true;
                    return true;
                default: return false;
            }
        }

        public static bool TryGetComponentType(int attributeCode, out ComponentType componentType)
        {
            switch (attributeCode)
            {
                case AutoChessAttributeCodes.Health:
                    componentType = ComponentType.ReadWrite<HealthAttribute>();
                    return true;
                case AutoChessAttributeCodes.Mana:
                    componentType = ComponentType.ReadWrite<ManaAttribute>();
                    return true;
                case AutoChessAttributeCodes.Shield:
                    componentType = ComponentType.ReadWrite<ShieldAttribute>();
                    return true;
                case AutoChessAttributeCodes.ArcaneResistance:
                    componentType = ComponentType.ReadWrite<ArcaneResistanceAttribute>();
                    return true;
                case AutoChessAttributeCodes.CounterDamage:
                    componentType = ComponentType.ReadWrite<CounterDamageAttribute>();
                    return true;
                case AutoChessAttributeCodes.LifeStealRatio:
                    componentType = ComponentType.ReadWrite<LifeStealRatioAttribute>();
                    return true;
                default:
                    componentType = default;
                    return false;
            }
        }
    }
}
