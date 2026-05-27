///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;
using Unity.Collections;

namespace GAS.Runtime.Generated
{
    public struct AutoChessScenarioSpawnEntry
    {
        public int UnitCode;
        public HeadlessAutoChessTeam Team;
        public int BoardX;
        public int BoardY;
        public int TurnOrder;
        public int PrimaryAbilityCode;
        public int ManaAbilityCode;
        public int ControlAbilityCode;
        public int SupportAbilityCode;
        public int SummonAbilityCode;
        public float Health;
        public float Mana;
        public float Shield;
        public float ArcaneResistance;
    }

    public struct AutoChessScenarioSummonEntry
    {
        public int SummonGameplayEffectCode;
        public int SummonedUnitCode;
        public int FixedTagCode;
        public int PrimaryAbilityCode;
        public int LifetimeTurns;
        public int SlotOffset;
        public int BoardXOffset;
        public int BoardYOffset;
        public int TurnOrderOffset;
        public float Health;
        public float Mana;
        public float Shield;
        public float MaxHealth;
        public float MaxMana;
        public float MaxShield;
        public HeadlessAutoChessTargetPolicy PrimaryTargetPolicy;
    }

    public struct AutoChessScenarioBuildPlan
    {
        public int BoardWidth;
        public int BoardHeight;
        public int AbilityDefinitionCount;
        public int GameplayEffectDefinitionCount;
        public int AttributeDefinitionCount;
        public int GameplayTagDefinitionCount;
        public int UnitDefinitionCount;
        public int ScenarioSpawnDefinitionCount;
        public FixedList512Bytes<AutoChessScenarioSpawnEntry> SpawnEntries;
        public FixedList512Bytes<AutoChessScenarioSummonEntry> SummonEntries;
    }

    public static class AutoChessScenarioBuildPlanFactory
    {
        public static AutoChessScenarioBuildPlan CreateDefault()
        {
            var plan = new AutoChessScenarioBuildPlan
            {
                BoardWidth = HeadlessAutoChessScenario.BoardWidth,
                BoardHeight = HeadlessAutoChessScenario.BoardHeight,
                AbilityDefinitionCount = 7,
                GameplayEffectDefinitionCount = 33,
                AttributeDefinitionCount = 6,
                GameplayTagDefinitionCount = 19,
                UnitDefinitionCount = 5,
                ScenarioSpawnDefinitionCount = 4,
            };
            plan.SpawnEntries.Add(new AutoChessScenarioSpawnEntry
            {
                UnitCode = 9901,
                Team = HeadlessAutoChessTeam.Player,
                BoardX = 1,
                BoardY = 1,
                TurnOrder = 10,
                PrimaryAbilityCode = 9611,
                ManaAbilityCode = 9613,
                ControlAbilityCode = 9614,
                SupportAbilityCode = 9615,
                SummonAbilityCode = 9616,
                Health = 72f,
                Mana = 4f,
                Shield = 0f,
                ArcaneResistance = 0f,
            });
            plan.SpawnEntries.Add(new AutoChessScenarioSpawnEntry
            {
                UnitCode = 9902,
                Team = HeadlessAutoChessTeam.Player,
                BoardX = 1,
                BoardY = 2,
                TurnOrder = 20,
                PrimaryAbilityCode = 9611,
                ManaAbilityCode = 9613,
                ControlAbilityCode = 9617,
                SupportAbilityCode = 9617,
                SummonAbilityCode = 9616,
                Health = 56f,
                Mana = 8f,
                Shield = 0f,
                ArcaneResistance = 0f,
            });
            plan.SpawnEntries.Add(new AutoChessScenarioSpawnEntry
            {
                UnitCode = 9903,
                Team = HeadlessAutoChessTeam.Enemy,
                BoardX = 4,
                BoardY = 1,
                TurnOrder = 30,
                PrimaryAbilityCode = 9612,
                ManaAbilityCode = 0,
                ControlAbilityCode = 0,
                SupportAbilityCode = 0,
                SummonAbilityCode = 0,
                Health = 64f,
                Mana = 2f,
                Shield = 0f,
                ArcaneResistance = 0.25f,
            });
            plan.SpawnEntries.Add(new AutoChessScenarioSpawnEntry
            {
                UnitCode = 9904,
                Team = HeadlessAutoChessTeam.Enemy,
                BoardX = 4,
                BoardY = 2,
                TurnOrder = 40,
                PrimaryAbilityCode = 9612,
                ManaAbilityCode = 0,
                ControlAbilityCode = 0,
                SupportAbilityCode = 0,
                SummonAbilityCode = 0,
                Health = 48f,
                Mana = 6f,
                Shield = 0f,
                ArcaneResistance = 0.25f,
            });
            plan.SummonEntries.Add(new AutoChessScenarioSummonEntry
            {
                SummonGameplayEffectCode = 9642,
                SummonedUnitCode = 9661,
                FixedTagCode = 12,
                PrimaryAbilityCode = 9611,
                LifetimeTurns = 6,
                SlotOffset = 100,
                BoardXOffset = 1,
                BoardYOffset = 0,
                TurnOrderOffset = 20,
                Health = 16f,
                Mana = 0f,
                Shield = 0f,
                MaxHealth = 16f,
                MaxMana = 10f,
                MaxShield = 32f,
                PrimaryTargetPolicy = HeadlessAutoChessTargetPolicy.LowestHealth,
            });
            return plan;
        }
    }
}
