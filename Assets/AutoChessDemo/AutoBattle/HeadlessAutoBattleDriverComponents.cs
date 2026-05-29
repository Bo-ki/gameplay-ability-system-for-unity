using Unity.Entities;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    public enum AutoBattleTargetPolicy : byte
    {
        Frontline = 0,
        LowestHealth = 1,
    }

    public struct AutoBattleCommandDriverComponent : IComponentData
    {
        public bool Enabled;
        public int LastDecisionFrame;
        public int LastExecutionFrame;
        public int IssuedCommandCount;
        public int IssuedPrimaryCommandCount;
        public int IssuedFinisherCommandCount;
        public int LowestHealthTargetCount;
    }

    public struct AutoBattleUnitComponent : IComponentData
    {
        public int BattleGroup;
        public HeadlessAutoBattleTeam Team;
        public int Slot;
        public int PrimaryAbilityCode;
        public int FinisherAbilityCode;
        public int HealthAttrSetCode;
        public int HealthAttrCode;
        public int EnergyAttrSetCode;
        public int EnergyAttrCode;
        public int CooldownTagIndex;
        public float FinisherHealthThreshold;
        public AutoBattleTargetPolicy PrimaryTargetPolicy;
        public AutoBattleTargetPolicy FinisherTargetPolicy;
    }

    public struct AutoBattleExecuteDamageCalculationComponent : IComponentData
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

    public struct AutoBattleUnitTargetStateRecord
    {
        public Entity Asc;
        public int BattleGroup;
        public HeadlessAutoBattleTeam Team;
        public int Slot;
        public int PrimaryAbilityCode;
        public int FinisherAbilityCode;
        public int CooldownTagIndex;
        public float FinisherHealthThreshold;
        public AutoBattleTargetPolicy PrimaryTargetPolicy;
        public AutoBattleTargetPolicy FinisherTargetPolicy;
        public TagMaskComponent Tags;
        public float Health;
        public float Energy;
    }

    public struct AutoBattleIssuedCommandRecord
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int AbilityCode;
        public AutoBattleTargetPolicy TargetPolicy;
        public bool IsFinisher;
    }
}
