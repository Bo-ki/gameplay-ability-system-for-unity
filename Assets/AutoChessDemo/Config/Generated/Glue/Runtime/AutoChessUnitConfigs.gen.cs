///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;

namespace GAS.Runtime.Generated
{
    public struct AutoChessUnitDefinition
    {
        public int UnitCode;
        public int FixedTagCode;
        public int PrimaryAbilityCode;
        public int ManaAbilityCode;
        public int ControlAbilityCode;
        public int SupportAbilityCode;
        public int SummonAbilityCode;
        public float Health;
        public float Mana;
        public float Shield;
        public float ArcaneResistance;
        public float MaxHealth;
        public float MaxMana;
        public float MaxShield;
        public float MaxArcaneResistance;
        public HeadlessAutoChessTargetPolicy PrimaryTargetPolicy;
        public HeadlessAutoChessTargetPolicy ManaTargetPolicy;
        public HeadlessAutoChessTargetPolicy ControlTargetPolicy;
        public HeadlessAutoChessTargetPolicy SupportTargetPolicy;
    }

    public static class AutoChessUnitConfigTable
    {
        public const int DefinitionCount = 5;

        public static bool TryGetDefinition(int unitCode, out AutoChessUnitDefinition definition)
        {
            switch (unitCode)
            {
                case 9661:
                    definition = new AutoChessUnitDefinition
                    {
                        UnitCode = 9661,
                        FixedTagCode = 12,
                        PrimaryAbilityCode = 9611,
                        ManaAbilityCode = 0,
                        ControlAbilityCode = 0,
                        SupportAbilityCode = 0,
                        SummonAbilityCode = 0,
                        Health = 16f,
                        Mana = 0f,
                        Shield = 0f,
                        ArcaneResistance = 0f,
                        MaxHealth = 16f,
                        MaxMana = 10f,
                        MaxShield = 32f,
                        MaxArcaneResistance = 0.75f,
                        PrimaryTargetPolicy = HeadlessAutoChessTargetPolicy.LowestHealth,
                        ManaTargetPolicy = HeadlessAutoChessTargetPolicy.LowestHealth,
                        ControlTargetPolicy = HeadlessAutoChessTargetPolicy.LowestHealth,
                        SupportTargetPolicy = HeadlessAutoChessTargetPolicy.LowestHealth,
                    };
                    return true;
                case 9901:
                    definition = new AutoChessUnitDefinition
                    {
                        UnitCode = 9901,
                        FixedTagCode = 0,
                        PrimaryAbilityCode = 9611,
                        ManaAbilityCode = 9613,
                        ControlAbilityCode = 9614,
                        SupportAbilityCode = 9615,
                        SummonAbilityCode = 9616,
                        Health = 72f,
                        Mana = 4f,
                        Shield = 0f,
                        ArcaneResistance = 0f,
                        MaxHealth = 100f,
                        MaxMana = 10f,
                        MaxShield = 32f,
                        MaxArcaneResistance = 0.75f,
                        PrimaryTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                        ManaTargetPolicy = HeadlessAutoChessTargetPolicy.LowestHealth,
                        ControlTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                        SupportTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                    };
                    return true;
                case 9902:
                    definition = new AutoChessUnitDefinition
                    {
                        UnitCode = 9902,
                        FixedTagCode = 0,
                        PrimaryAbilityCode = 9611,
                        ManaAbilityCode = 9613,
                        ControlAbilityCode = 9617,
                        SupportAbilityCode = 9617,
                        SummonAbilityCode = 9616,
                        Health = 56f,
                        Mana = 8f,
                        Shield = 0f,
                        ArcaneResistance = 0f,
                        MaxHealth = 100f,
                        MaxMana = 10f,
                        MaxShield = 32f,
                        MaxArcaneResistance = 0.75f,
                        PrimaryTargetPolicy = HeadlessAutoChessTargetPolicy.LowestHealth,
                        ManaTargetPolicy = HeadlessAutoChessTargetPolicy.LowestHealth,
                        ControlTargetPolicy = HeadlessAutoChessTargetPolicy.LowestHealth,
                        SupportTargetPolicy = HeadlessAutoChessTargetPolicy.LowestHealth,
                    };
                    return true;
                case 9903:
                    definition = new AutoChessUnitDefinition
                    {
                        UnitCode = 9903,
                        FixedTagCode = 0,
                        PrimaryAbilityCode = 9612,
                        ManaAbilityCode = 0,
                        ControlAbilityCode = 0,
                        SupportAbilityCode = 0,
                        SummonAbilityCode = 0,
                        Health = 64f,
                        Mana = 2f,
                        Shield = 0f,
                        ArcaneResistance = 0.25f,
                        MaxHealth = 100f,
                        MaxMana = 10f,
                        MaxShield = 32f,
                        MaxArcaneResistance = 0.75f,
                        PrimaryTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                        ManaTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                        ControlTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                        SupportTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                    };
                    return true;
                case 9904:
                    definition = new AutoChessUnitDefinition
                    {
                        UnitCode = 9904,
                        FixedTagCode = 0,
                        PrimaryAbilityCode = 9612,
                        ManaAbilityCode = 0,
                        ControlAbilityCode = 0,
                        SupportAbilityCode = 0,
                        SummonAbilityCode = 0,
                        Health = 48f,
                        Mana = 6f,
                        Shield = 0f,
                        ArcaneResistance = 0.25f,
                        MaxHealth = 100f,
                        MaxMana = 10f,
                        MaxShield = 32f,
                        MaxArcaneResistance = 0.75f,
                        PrimaryTargetPolicy = HeadlessAutoChessTargetPolicy.LowestHealth,
                        ManaTargetPolicy = HeadlessAutoChessTargetPolicy.LowestHealth,
                        ControlTargetPolicy = HeadlessAutoChessTargetPolicy.LowestHealth,
                        SupportTargetPolicy = HeadlessAutoChessTargetPolicy.LowestHealth,
                    };
                    return true;
                default:
                    definition = default;
                    return false;
            }
        }
    }
}
