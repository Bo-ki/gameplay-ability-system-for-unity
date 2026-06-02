using Unity.Entities;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    public enum AutoChessTargetPolicy : byte
    {
        Frontline = 0,
        LowestHealth = 1,
    }

    public struct AutoChessBattleDriverComponent : IComponentData
    {
        public bool Enabled;
        public int LastDecisionFrame;
        public int LastExecutionFrame;
        public int LastOutcomeFrame;
        public int PlayerAliveCount;
        public int EnemyAliveCount;
        public int IssuedCommandCount;
        public int IssuedPrimaryCommandCount;
        public int IssuedFinisherCommandCount;
        public int LowestHealthTargetCount;
    }

    public struct AutoChessBattleUnitComponent : IComponentData
    {
        public int BattleGroup;
        public AutoChessTeam Team;
        public int Slot;
        public int PrimaryAbilityCode;
        public int FinisherAbilityCode;
        public Entity PrimaryAbilityEntity;
        public Entity FinisherAbilityEntity;
        public int HealthAttrSetCode;
        public int HealthAttrCode;
        public int EnergyAttrSetCode;
        public int EnergyAttrCode;
        public int CooldownTagIndex;
        public float FinisherHealthThreshold;
        public AutoChessTargetPolicy PrimaryTargetPolicy;
        public AutoChessTargetPolicy FinisherTargetPolicy;
    }

    public struct AutoChessExecuteDamageCalculationComponent : IComponentData
    {
        public int CalculationCode;
        public int OutputKey;
        public int HealthAttrSetCode;
        public int HealthAttrCode;
        public float BaseDamage;
        public float MissingHealthCoefficient;
        public float MinDamage;
        public float MaxDamage;
    }

    public struct AutoChessBattleUnitTargetStateRecord
    {
        public Entity Asc;
        public int BattleGroup;
        public AutoChessTeam Team;
        public int Slot;
        public int PrimaryAbilityCode;
        public int FinisherAbilityCode;
        public Entity PrimaryAbilityEntity;
        public Entity FinisherAbilityEntity;
        public int CooldownTagIndex;
        public float FinisherHealthThreshold;
        public AutoChessTargetPolicy PrimaryTargetPolicy;
        public AutoChessTargetPolicy FinisherTargetPolicy;
        public TagMaskComponent Tags;
        public float Health;
        public float Energy;
    }

    public struct AutoChessBattleIssuedCommandRecord
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int AbilityCode;
        public AutoChessTargetPolicy TargetPolicy;
        public bool IsFinisher;
    }
}
