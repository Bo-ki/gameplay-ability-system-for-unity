using System.Collections.Generic;
using System.Globalization;
using Unity.Entities;

namespace GAS.Runtime
{
    public static partial class HeadlessAutoChessScenario
    {
        private readonly struct UnitDefinition
        {
            public readonly string Id;
            public readonly HeadlessAutoChessTeam Team;
            public readonly int Slot;
            public readonly int BoardX;
            public readonly int BoardY;
            public readonly int TurnOrder;
            public readonly float Health;
            public readonly float Mana;
            public readonly int PrimaryAbilityCode;
            public readonly int ManaAbilityCode;
            public readonly int ControlAbilityCode;
            public readonly int SupportAbilityCode;
            public readonly int SummonAbilityCode;
            public readonly float ManaAbilityThreshold;
            public readonly HeadlessAutoChessTargetPolicy PrimaryTargetPolicy;
            public readonly HeadlessAutoChessTargetPolicy ManaTargetPolicy;
            public readonly HeadlessAutoChessTargetPolicy ControlTargetPolicy;
            public readonly HeadlessAutoChessTargetPolicy SupportTargetPolicy;
            public readonly int SynergyCode;
            public readonly int SynergyThreshold;
            public readonly int SynergyAllyBuffGameplayEffectCode;
            public readonly int SynergyEnemyDebuffGameplayEffectCode;
            public readonly int KillManaGainGameplayEffectCode;
            public readonly int ReviveGameplayEffectCode;
            public readonly int MaxReviveCount;
            public readonly int MaxActiveSummons;
            public readonly float ArcaneResistance;
            public readonly int EquipmentGameplayEffectCode;
            public readonly int CounterDamageGameplayEffectCode;
            public readonly float InitialHealth;

            public UnitDefinition(
                string id,
                HeadlessAutoChessTeam team,
                int slot,
                int boardX,
                int boardY,
                int turnOrder,
                float health,
                float mana,
                int primaryAbilityCode,
                int manaAbilityCode,
                float manaAbilityThreshold,
                HeadlessAutoChessTargetPolicy primaryTargetPolicy,
                HeadlessAutoChessTargetPolicy manaTargetPolicy,
                int controlAbilityCode = 0,
                HeadlessAutoChessTargetPolicy controlTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                int supportAbilityCode = 0,
                HeadlessAutoChessTargetPolicy supportTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                int summonAbilityCode = 0,
                int maxActiveSummons = 0,
                int synergyCode = 0,
                int synergyThreshold = 0,
                int synergyAllyBuffGameplayEffectCode = 0,
                int synergyEnemyDebuffGameplayEffectCode = 0,
                int killManaGainGameplayEffectCode = 0,
                int reviveGameplayEffectCode = 0,
                int maxReviveCount = 0,
                float arcaneResistance = 0f,
                int equipmentGameplayEffectCode = 0,
                int counterDamageGameplayEffectCode = 0,
                float initialHealth = -1f)
            {
                Id = id;
                Team = team;
                Slot = slot;
                BoardX = boardX;
                BoardY = boardY;
                TurnOrder = turnOrder;
                Health = health;
                Mana = mana;
                PrimaryAbilityCode = primaryAbilityCode;
                ManaAbilityCode = manaAbilityCode;
                ControlAbilityCode = controlAbilityCode;
                SupportAbilityCode = supportAbilityCode;
                SummonAbilityCode = summonAbilityCode;
                ManaAbilityThreshold = manaAbilityThreshold;
                PrimaryTargetPolicy = primaryTargetPolicy;
                ManaTargetPolicy = manaTargetPolicy;
                ControlTargetPolicy = controlTargetPolicy;
                SupportTargetPolicy = supportTargetPolicy;
                SynergyCode = synergyCode;
                SynergyThreshold = synergyThreshold;
                SynergyAllyBuffGameplayEffectCode = synergyAllyBuffGameplayEffectCode;
                SynergyEnemyDebuffGameplayEffectCode = synergyEnemyDebuffGameplayEffectCode;
                KillManaGainGameplayEffectCode = killManaGainGameplayEffectCode;
                ReviveGameplayEffectCode = reviveGameplayEffectCode;
                MaxReviveCount = maxReviveCount;
                MaxActiveSummons = maxActiveSummons;
                ArcaneResistance = arcaneResistance;
                EquipmentGameplayEffectCode = equipmentGameplayEffectCode;
                CounterDamageGameplayEffectCode = counterDamageGameplayEffectCode;
                InitialHealth = initialHealth > 0f && initialHealth < health ? initialHealth : health;
            }

            public UnitDefinition WithStats(float health, float mana)
            {
                var initialHealth = health;
                if (InitialHealth > 0f && InitialHealth < Health)
                {
                    var deficit = Health - InitialHealth;
                    initialHealth = health > deficit ? health - deficit : health;
                }

                return new UnitDefinition(
                    Id,
                    Team,
                    Slot,
                    BoardX,
                    BoardY,
                    TurnOrder,
                    health,
                    mana,
                    PrimaryAbilityCode,
                    ManaAbilityCode,
                    ManaAbilityThreshold,
                    PrimaryTargetPolicy,
                    ManaTargetPolicy,
                    ControlAbilityCode,
                    ControlTargetPolicy,
                    SupportAbilityCode,
                    SupportTargetPolicy,
                    SummonAbilityCode,
                    MaxActiveSummons,
                    SynergyCode,
                    SynergyThreshold,
                    SynergyAllyBuffGameplayEffectCode,
                    SynergyEnemyDebuffGameplayEffectCode,
                    KillManaGainGameplayEffectCode,
                    ReviveGameplayEffectCode,
                    MaxReviveCount,
                    ArcaneResistance,
                    EquipmentGameplayEffectCode,
                    CounterDamageGameplayEffectCode,
                    initialHealth);
            }

            public UnitDefinition WithScaleCopy(int copyIndex, int turnOrderOffset)
            {
                return new UnitDefinition(
                    Id + "#" + copyIndex.ToString("D4", CultureInfo.InvariantCulture),
                    Team,
                    Slot + copyIndex * 100,
                    BoardX,
                    (BoardY + copyIndex) % BoardHeight,
                    TurnOrder + turnOrderOffset,
                    Health,
                    Mana,
                    PrimaryAbilityCode,
                    ManaAbilityCode,
                    ManaAbilityThreshold,
                    PrimaryTargetPolicy,
                    ManaTargetPolicy,
                    ControlAbilityCode,
                    ControlTargetPolicy,
                    SupportAbilityCode,
                    SupportTargetPolicy,
                    SummonAbilityCode,
                    MaxActiveSummons,
                    SynergyCode,
                    SynergyThreshold,
                    SynergyAllyBuffGameplayEffectCode,
                    SynergyEnemyDebuffGameplayEffectCode,
                    KillManaGainGameplayEffectCode,
                    ReviveGameplayEffectCode,
                    MaxReviveCount,
                    ArcaneResistance,
                    EquipmentGameplayEffectCode,
                    CounterDamageGameplayEffectCode,
                    InitialHealth);
            }

            public int[] CreateAbilityCodes()
            {
                var codes = new List<int>(5);
                if (PrimaryAbilityCode > 0)
                    codes.Add(PrimaryAbilityCode);
                if (ManaAbilityCode > 0)
                    codes.Add(ManaAbilityCode);
                if (ControlAbilityCode > 0)
                    codes.Add(ControlAbilityCode);
                if (SupportAbilityCode > 0)
                    codes.Add(SupportAbilityCode);
                if (SummonAbilityCode > 0)
                    codes.Add(SummonAbilityCode);

                return codes.ToArray();
            }
        }

        private readonly struct UnitRuntime
        {
            public readonly UnitDefinition Definition;
            public readonly AbilitySystemFacade Facade;
            public readonly float Health;
            public readonly float Mana;
            public readonly float Shield;
            public readonly float ArcaneResistance;
            public readonly bool Alive;
            public readonly bool RevivePending;
            public readonly int ReviveCount;

            public UnitRuntime(UnitDefinition definition)
                : this(
                    definition,
                    default,
                    definition.Health,
                    definition.Mana,
                    0f,
                    definition.ArcaneResistance,
                    false,
                    false,
                    0)
            {
            }

            private UnitRuntime(
                UnitDefinition definition,
                AbilitySystemFacade facade,
                float health,
                float mana,
                float shield,
                float arcaneResistance,
                bool alive,
                bool revivePending,
                int reviveCount)
            {
                Definition = definition;
                Facade = facade;
                Health = health;
                Mana = mana;
                Shield = shield;
                ArcaneResistance = arcaneResistance;
                Alive = alive;
                RevivePending = revivePending;
                ReviveCount = reviveCount;
            }

            public UnitRuntime WithFacade(AbilitySystemFacade facade)
            {
                return new UnitRuntime(
                    Definition,
                    facade,
                    Health,
                    Mana,
                    Shield,
                    ArcaneResistance,
                    true,
                    RevivePending,
                    ReviveCount);
            }

            public UnitRuntime Refresh()
            {
                var health = GetAttribute(Facade.Entity, AttributeHealth);
                var mana = GetAttribute(Facade.Entity, AttributeMana);
                var shield = GetAttribute(Facade.Entity, AttributeShield);
                var arcaneResistance = GetAttribute(Facade.Entity, AttributeArcaneResistance);
                var revivePending = false;
                var reviveCount = 0;
                var em = GASManager.EntityManager;
                if (Facade.Entity != Entity.Null
                    && em.Exists(Facade.Entity)
                    && em.HasComponent<CHeadlessAutoChessPassiveState>(Facade.Entity))
                {
                    var passiveState = em.GetComponentData<CHeadlessAutoChessPassiveState>(Facade.Entity);
                    revivePending = passiveState.RevivePending;
                    reviveCount = passiveState.ReviveCount;
                }

                return new UnitRuntime(
                    Definition,
                    Facade,
                    health,
                    mana,
                    shield,
                    arcaneResistance,
                    health > 0f,
                    revivePending,
                    reviveCount);
            }

            public bool CanStillParticipateInResolution
            {
                get
                {
                    return Alive
                           || RevivePending
                           || (Definition.ReviveGameplayEffectCode > 0
                               && Definition.MaxReviveCount > 0
                               && ReviveCount < Definition.MaxReviveCount);
                }
            }
        }

        private sealed class ScenarioState
        {
            public readonly UnitRuntime[] Units;
            public Entity DriverEntity;
            public HeadlessAutoChessPresentationOutboxCounts PresentationOutboxCounts;

            public ScenarioState(UnitDefinition[] definitions)
            {
                Units = new UnitRuntime[definitions.Length];
                for (var i = 0; i < definitions.Length; i++)
                    Units[i] = new UnitRuntime(definitions[i]);
            }
        }
    }
}
