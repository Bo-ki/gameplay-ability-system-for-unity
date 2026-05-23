using Unity.Entities;

namespace GAS.Runtime
{
    public enum HeadlessAutoChessTeam : byte
    {
        None = 0,
        Player = 1,
        Enemy = 2,
        Draw = 3,
    }

    public enum HeadlessAutoChessTargetPolicy : byte
    {
        Frontline = 0,
        LowestHealth = 1,
    }

    public enum HeadlessAutoChessScenarioVariant : byte
    {
        DefaultBalanced = 0,
        PlayerAdvantage = 1,
        EnemyPressure = 2,
        LargeBoard = 3,
    }

    public enum HeadlessAutoChessPassiveReactionKind : byte
    {
        None = 0,
        KillManaGain = 1,
        SelfRevive = 2,
    }

    public enum HeadlessAutoChessSynergyKind : byte
    {
        None = 0,
        Arcane = 1,
    }

    public enum HeadlessAutoChessPresentationMarkerCode
    {
        UiBattleStarted = 9701,
        UiRoundStarted = 9702,
        UiUnitSpawned = 9703,
        UiHealthBarAttached = 9704,
        UiHealthChanged = 9705,
        UiUnitDefeated = 9706,
        UiBattleEnded = 9707,
        UiStatusIconChanged = 9708,
        UiSynergyBadgeChanged = 9709,
        UiCrowdControlSkipped = 9760,
        UiShieldChanged = 9763,
        UiSummonSpawned = 9770,
        UiSummonExpired = 9771,
        UiResistanceChanged = 9780,
        UiEquipmentChanged = 9790,
        UiCounterTriggered = 9791,
        UiCleanseTriggered = 9796,
        UiCleanseRallyTriggered = 9801,
        UiRallyComboTriggered = 9806,
        UiLifeStealTriggered = 9811,
        UiPoisonStacked = 9821,
        UiExecuteTriggered = 9831,
        UiDeathBurstTriggered = 9841,
        UiEnrageTriggered = 9851,
        VfxAbilityWindup = 9710,
        VfxAbilityImpact = 9711,
        VfxEffectApplied = 9712,
        VfxEffectExpired = 9713,
        VfxUnitDefeated = 9714,
        VfxSynergyAura = 9715,
        VfxCrowdControl = 9761,
        VfxShield = 9764,
        VfxSummon = 9772,
        VfxSummonExpired = 9773,
        VfxResistance = 9781,
        VfxCounter = 9792,
        VfxCleanse = 9797,
        VfxCleanseRally = 9802,
        VfxRallyCombo = 9807,
        VfxLifeSteal = 9812,
        VfxPoison = 9822,
        VfxExecute = 9832,
        VfxDeathBurst = 9842,
        VfxEnrage = 9852,
        SfxAbilityCast = 9720,
        SfxImpact = 9721,
        SfxUnitDefeated = 9722,
        SfxSynergy = 9723,
        SfxCrowdControl = 9762,
        SfxShield = 9765,
        SfxSummon = 9774,
        SfxSummonExpired = 9775,
        SfxResistance = 9782,
        SfxCounter = 9793,
        SfxCleanse = 9798,
        SfxCleanseRally = 9803,
        SfxRallyCombo = 9808,
        SfxLifeSteal = 9813,
        SfxPoison = 9823,
        SfxExecute = 9833,
        SfxDeathBurst = 9843,
        SfxEnrage = 9853,
        FloatingTextDamage = 9730,
        FloatingTextHeal = 9731,
        FloatingTextBuff = 9732,
        FloatingTextControl = 9733,
        FloatingTextShield = 9734,
        FloatingTextSummon = 9776,
        FloatingTextSummonExpired = 9777,
        FloatingTextResistance = 9783,
        FloatingTextCounter = 9794,
        FloatingTextCleanse = 9799,
        FloatingTextCleanseRally = 9804,
        FloatingTextRallyCombo = 9809,
        FloatingTextLifeSteal = 9814,
        FloatingTextPoison = 9824,
        FloatingTextExecute = 9834,
        FloatingTextDeathBurst = 9844,
        FloatingTextEnrage = 9854,
        CueRequest = 9740,
        CueAbilityCast = 9741,
        CueEffectApplied = 9742,
        CueEffectExpired = 9743,
        CueRevive = 9744,
        CueCrowdControl = 9745,
        CueShield = 9746,
        CueSummon = 9778,
        CueSummonExpired = 9779,
        CueResistance = 9784,
        CueCounter = 9795,
        CueCleanse = 9800,
        CueCleanseRally = 9805,
        CueRallyCombo = 9810,
        CueLifeSteal = 9815,
        CuePoison = 9825,
        CueExecute = 9835,
        CueDeathBurst = 9845,
        CueEnrage = 9855,
        SettlementScoreboard = 9750,
        SettlementPanel = 9751,
    }

    public struct CHeadlessAutoChessDriver : IComponentData
    {
        public bool Enabled;
        public int BoardWidth;
        public int BoardHeight;
        public int Round;
        public int NextTurnOrder;
        public int LastDecisionFrame;
        public int TurnCount;
        public int IssuedCommandCount;
        public int IssuedPrimaryCommandCount;
        public int IssuedManaAbilityCommandCount;
        public int IssuedControlAbilityCommandCount;
        public int IssuedSupportAbilityCommandCount;
        public int IssuedSummonAbilityCommandCount;
        public int CrowdControlTurnSkippedCount;
        public int FrontlineTargetCount;
        public int LowestHealthTargetCount;
        public bool Completed;
        public HeadlessAutoChessTeam Winner;
    }

    public struct CHeadlessAutoChessBattleFacts : IComponentData
    {
        public int LastDamageProjectionFrame;
        public int ProcessedAttributeEventCount;
        public int HealthDamageFactCount;
        public int ShieldAppliedFactCount;
        public int ShieldAbsorbedFactCount;
        public int ShieldBrokenFactCount;
        public int DamageTypeResolvedFactCount;
        public int DamageResistedFactCount;
        public int UnitDefeatedFactCount;
        public int BattleResolvedFactCount;
        public int PlayerDefeatedCount;
        public int EnemyDefeatedCount;
        public int FirstDefeatFrame;
        public int LastDefeatFrame;
        public int BattleResolvedFrame;
        public HeadlessAutoChessTeam Winner;
        public bool BattleResolved;
    }

    public struct CHeadlessAutoChessPassiveReactionFacts : IComponentData
    {
        public int LastReactionFrame;
        public int ProcessedGameplayEventCount;
        public int PassiveTriggeredFactCount;
        public int KillManaGrantedFactCount;
        public int ReviveRequestedFactCount;
        public int ReviveAppliedFactCount;
    }

    public struct CHeadlessAutoChessSynergyFacts : IComponentData
    {
        public int LastProjectionFrame;
        public int ProcessedGameplayEventCount;
        public int SynergyActivatedFactCount;
        public int SynergyExpiredFactCount;
        public int AllyBuffRequestedFactCount;
        public int EnemyDebuffRequestedFactCount;
        public int PeriodicTickFactCount;
    }

    public struct CHeadlessAutoChessSummonFacts : IComponentData
    {
        public int LastProjectionFrame;
        public int ProcessedGameplayEventCount;
        public int SummonRequestedFactCount;
        public int SummonSpawnedFactCount;
        public int SummonExpiredFactCount;
        public int SummonDespawnedFactCount;
        public int NextSummonSerial;
        public int ActiveSummonCount;
    }

    public struct CHeadlessAutoChessCounterFacts : IComponentData
    {
        public int LastProjectionFrame;
        public int ProcessedGameplayEventCount;
        public int ProcessedAttributeEventCount;
        public int ProcessedDamageEventCount;
        public int EquipmentAppliedFactCount;
        public int CounterTriggeredFactCount;
        public int CounterDamageAppliedFactCount;
    }

    public struct CHeadlessAutoChessCleanseFacts : IComponentData
    {
        public int LastProjectionFrame;
        public int ProcessedGameplayEventCount;
        public int ProcessedTagEventCount;
        public int CleanseRequestedFactCount;
        public int CleanseAppliedFactCount;
        public int CleanseEffectRemovedFactCount;
        public int CleanseRallyRequestedFactCount;
        public int CleanseRallyAppliedFactCount;
    }

    public struct CHeadlessAutoChessRallyComboFacts : IComponentData
    {
        public int LastProjectionFrame;
        public int ProcessedGameplayEventCount;
        public int ProcessedAttributeEventCount;
        public int RallyComboTriggeredFactCount;
        public int RallyComboDamageAppliedFactCount;
    }

    public struct CHeadlessAutoChessLifeStealFacts : IComponentData
    {
        public int LastProjectionFrame;
        public int ProcessedDamageEventCount;
        public int ProcessedAttributeEventCount;
        public int LifeStealTriggeredFactCount;
        public int LifeStealHealedFactCount;
    }

    public struct CHeadlessAutoChessPoisonFacts : IComponentData
    {
        public int LastProjectionFrame;
        public int ProcessedGameplayEventCount;
        public int ProcessedAttributeEventCount;
        public int PoisonStackRequestedFactCount;
        public int PoisonStackChangedFactCount;
        public int PoisonOverflowTriggeredFactCount;
        public int PoisonOverflowDamageAppliedFactCount;
        public int PoisonPeriodDamageAppliedFactCount;
    }

    public struct CHeadlessAutoChessExecuteFacts : IComponentData
    {
        public int LastProjectionFrame;
        public int ProcessedGameplayEventCount;
        public int ProcessedDamageEventCount;
        public int ProcessedAttributeEventCount;
        public int ExecuteTriggeredFactCount;
        public int ExecuteDamageAppliedFactCount;
    }

    public struct CHeadlessAutoChessDeathBurstFacts : IComponentData
    {
        public int LastProjectionFrame;
        public int ProcessedGameplayEventCount;
        public int ProcessedAttributeEventCount;
        public int DeathBurstTriggeredFactCount;
        public int DeathBurstDamageAppliedFactCount;
    }

    public struct CHeadlessAutoChessEnrageFacts : IComponentData
    {
        public int LastProjectionFrame;
        public int ProcessedGameplayEventCount;
        public int ProcessedAttributeEventCount;
        public int EnrageTriggeredFactCount;
        public int EnrageAppliedFactCount;
    }

    public struct CHeadlessAutoChessPresentationCueMarkerFacts : IComponentData
    {
        public int LastProjectionFrame;
        public int LastRound;
        public bool BattleStarted;
        public bool BattleEnded;
        public int ProcessedGameplayEventCount;
        public int ProcessedAttributeEventCount;
        public int ProcessedCueRequestCount;
        public int ProcessedTagEventCount;
        public int ProcessedDamageEventCount;
    }

    public struct CHeadlessAutoChessSynergyState : IComponentData
    {
        public int ActiveSynergyMask;
        public int LastEvaluationFrame;
    }

    public struct CHeadlessAutoChessDamageState : IComponentData
    {
        public Entity LastDamageSource;
        public int LastDamageFrame;
        public float LastDamageAmount;
    }

    public struct CHeadlessAutoChessDeathState : IComponentData
    {
        public bool Defeated;
        public int DefeatFrame;
        public int DefeatRound;
        public int DefeatTurn;
        public float FinalHealth;
        public Entity DefeatedBy;
    }

    public struct CHeadlessAutoChessPassiveRules : IComponentData
    {
        public int KillManaGainGameplayEffectCode;
        public int ReviveGameplayEffectCode;
        public int MaxReviveCount;
    }

    public struct CHeadlessAutoChessPassiveState : IComponentData
    {
        public int KillManaGainCount;
        public int ReviveCount;
        public bool RevivePending;
        public int LastReviveRequestFrame;
        public int LastReviveAppliedFrame;
    }

    public struct BHeadlessAutoChessSynergyMember : IBufferElementData
    {
        public int SynergyCode;
        public int Threshold;
        public int AllyBuffGameplayEffectCode;
        public int EnemyDebuffGameplayEffectCode;
    }

    public struct CHeadlessAutoChessUnit : IComponentData
    {
        public HeadlessAutoChessTeam Team;
        public int Slot;
        public int BoardX;
        public int BoardY;
        public int TurnOrder;
        public int PrimaryAbilityCode;
        public int ManaAbilityCode;
        public int ControlAbilityCode;
        public int SupportAbilityCode;
        public int SummonAbilityCode;
        public int HealthAttrSetCode;
        public int HealthAttrCode;
        public int ManaAttrSetCode;
        public int ManaAttrCode;
        public int ShieldAttrSetCode;
        public int ShieldAttrCode;
        public int ArcaneResistanceAttrSetCode;
        public int ArcaneResistanceAttrCode;
        public int CounterDamageAttrSetCode;
        public int CounterDamageAttrCode;
        public int LifeStealRatioAttrSetCode;
        public int LifeStealRatioAttrCode;
        public int PrimaryCooldownTagIndex;
        public int ManaCooldownTagIndex;
        public int ControlCooldownTagIndex;
        public int SupportCooldownTagIndex;
        public int SummonCooldownTagIndex;
        public int CrowdControlTagIndex;
        public float ManaAbilityThreshold;
        public int MaxActiveSummons;
        public HeadlessAutoChessTargetPolicy PrimaryTargetPolicy;
        public HeadlessAutoChessTargetPolicy ManaTargetPolicy;
        public HeadlessAutoChessTargetPolicy ControlTargetPolicy;
        public HeadlessAutoChessTargetPolicy SupportTargetPolicy;
    }

    public struct CHeadlessAutoChessCounterRules : IComponentData
    {
        public int EquipmentGameplayEffectCode;
        public int CounterDamageGameplayEffectCode;
        public int CounterReadyTagIndex;
        public int CounterDamageAttrSetCode;
        public int CounterDamageAttrCode;
        public int SetByCallerCounterDamageAmountKey;
        public int HealthAttrSetCode;
        public int HealthAttrCode;
        public float MinIncomingDamage;
    }

    public struct CHeadlessAutoChessCounterState : IComponentData
    {
        public int EquipmentAppliedCount;
        public int CounterRequestCount;
        public int CounterDamageAppliedCount;
        public int LastCounterFrame;
        public Entity LastCounterSource;
        public Entity LastCounterTarget;
        public float LastCounterDamage;
    }

    public struct CHeadlessAutoChessCleanseRules : IComponentData
    {
        public int CleanseAbilityCode;
        public int CleanseGameplayEffectCode;
        public int RallyGameplayEffectCode;
        public int RemovableTagIndex;
        public int CooldownTagIndex;
    }

    public struct CHeadlessAutoChessCleanseState : IComponentData
    {
        public int CleanseRequestCount;
        public int CleanseAppliedCount;
        public int CleanseEffectRemovedCount;
        public int CleanseRallyRequestCount;
        public int CleanseRallyAppliedCount;
        public int LastCleanseFrame;
        public Entity LastCleanseSource;
        public Entity LastCleanseTarget;
    }

    public struct CHeadlessAutoChessRallyComboRules : IComponentData
    {
        public int ComboDamageGameplayEffectCode;
        public int RalliedTagIndex;
        public int HealthAttrSetCode;
        public int HealthAttrCode;
        public float ComboDamage;
    }

    public struct CHeadlessAutoChessRallyComboState : IComponentData
    {
        public int ComboRequestCount;
        public int ComboDamageAppliedCount;
        public int LastComboFrame;
        public Entity LastComboSource;
        public Entity LastComboTarget;
        public Entity LastRallyGameplayEffect;
        public float LastComboDamage;
    }

    public struct CHeadlessAutoChessLifeStealRules : IComponentData
    {
        public int GearGameplayEffectCode;
        public int HealGameplayEffectCode;
        public int ReadyTagIndex;
        public int LifeStealRatioAttrSetCode;
        public int LifeStealRatioAttrCode;
        public int HealthAttrSetCode;
        public int HealthAttrCode;
        public int SetByCallerHealAmountKey;
        public float MinDamage;
    }

    public struct CHeadlessAutoChessLifeStealState : IComponentData
    {
        public int TriggerRequestCount;
        public int HealAppliedCount;
        public int LastLifeStealFrame;
        public Entity LastLifeStealSource;
        public Entity LastLifeStealTarget;
        public Entity LastLifeStealGameplayEffect;
        public float LastLifeStealDamage;
        public float LastLifeStealHealAmount;
    }

    public struct CHeadlessAutoChessPoisonRules : IComponentData
    {
        public int StackGameplayEffectCode;
        public int OverflowDamageGameplayEffectCode;
        public int PeriodDamageGameplayEffectCode;
        public int StackingCode;
        public int PoisonedTagIndex;
        public int HealthAttrSetCode;
        public int HealthAttrCode;
        public int DamageTypeCode;
        public float MinDamage;
    }

    public struct CHeadlessAutoChessPoisonState : IComponentData
    {
        public int StackRequestCount;
        public int StackChangedCount;
        public int OverflowTriggeredCount;
        public int OverflowDamageAppliedCount;
        public int PeriodDamageAppliedCount;
        public int LastPoisonFrame;
        public Entity LastPoisonSource;
        public Entity LastPoisonTarget;
        public Entity LastPoisonGameplayEffect;
        public int LastPoisonStackCount;
        public float LastPoisonDamage;
    }

    public struct CHeadlessAutoChessExecuteRules : IComponentData
    {
        public int GearGameplayEffectCode;
        public int DamageGameplayEffectCode;
        public int DamageTypeCode;
        public int ReadyTagIndex;
        public int ExecutedTagIndex;
        public int HealthAttrSetCode;
        public int HealthAttrCode;
        public float HealthThreshold;
        public float MinDamage;
    }

    public struct CHeadlessAutoChessExecuteState : IComponentData
    {
        public int TriggerRequestCount;
        public int DamageAppliedCount;
        public int LastExecuteFrame;
        public Entity LastExecuteSource;
        public Entity LastExecuteTarget;
        public Entity LastExecuteGameplayEffect;
        public float LastExecuteDamage;
    }

    public struct CHeadlessAutoChessDeathBurstRules : IComponentData
    {
        public int GearGameplayEffectCode;
        public int DamageGameplayEffectCode;
        public int DamageTypeCode;
        public int ReadyTagIndex;
        public int HealthAttrSetCode;
        public int HealthAttrCode;
        public float DamageAmount;
        public int MaxBoardDistance;
    }

    public struct CHeadlessAutoChessDeathBurstState : IComponentData
    {
        public int TriggerRequestCount;
        public int DamageAppliedCount;
        public int LastDeathBurstFrame;
        public Entity LastDeathBurstSource;
        public Entity LastDeathBurstCorpse;
        public Entity LastDeathBurstTarget;
        public Entity LastDeathBurstGameplayEffect;
        public float LastDeathBurstDamage;
    }

    public struct CHeadlessAutoChessEnrageRules : IComponentData
    {
        public int EnrageGameplayEffectCode;
        public int EnragedTagIndex;
        public int HealthAttrSetCode;
        public int HealthAttrCode;
        public int CounterDamageAttrSetCode;
        public int CounterDamageAttrCode;
        public float HealthThresholdRatio;
        public float CounterDamageBonus;
    }

    public struct CHeadlessAutoChessEnrageState : IComponentData
    {
        public int TriggerRequestCount;
        public int AppliedCount;
        public int LastEnrageFrame;
        public Entity LastEnrageSource;
        public Entity LastEnrageTarget;
        public Entity LastEnrageGameplayEffect;
        public float LastHealth;
        public float LastCounterDamage;
    }

    public struct CHeadlessAutoChessShieldDamageCalculation : IComponentData
    {
        public int AttributeSetCode;
        public int HealthAttrCode;
        public int ShieldAttrCode;
        public int ShieldDamageOutputKey;
        public int HealthDamageOutputKey;
        public int ResistedDamageOutputKey;
        public int DamageTypeCode;
        public int ResistanceAttrSetCode;
        public int ResistanceAttrCode;
        public float ResistanceCap;
        public float BaseDamage;
    }

    public struct CHeadlessAutoChessSummonRequest : IComponentData
    {
        public int SummonedUnitCode;
        public int HealthAttrSetCode;
        public int HealthAttrCode;
        public int ManaAttrSetCode;
        public int ManaAttrCode;
        public int ShieldAttrSetCode;
        public int ShieldAttrCode;
        public int ArcaneResistanceAttrSetCode;
        public int ArcaneResistanceAttrCode;
        public int FixedTagCode;
        public int PrimaryAbilityCode;
        public int PrimaryCooldownTagIndex;
        public int LifetimeTurns;
        public int SlotOffset;
        public int BoardXOffset;
        public int BoardYOffset;
        public int TurnOrderOffset;
        public float Health;
        public float Mana;
        public float Shield;
        public float ArcaneResistance;
        public float MaxHealth;
        public float MaxMana;
        public float MaxShield;
        public float MaxArcaneResistance;
        public HeadlessAutoChessTargetPolicy PrimaryTargetPolicy;
    }

    public struct CHeadlessAutoChessSummonedUnit : IComponentData
    {
        public Entity OwnerAsc;
        public Entity SourceAbility;
        public Entity SourceGameplayEffect;
        public int SummonedUnitCode;
        public int SummonGameplayEffectCode;
        public int SummonSerial;
        public int SpawnFrame;
        public int SpawnTurn;
        public int ExpireTurn;
        public bool DespawnRequested;
    }
}
