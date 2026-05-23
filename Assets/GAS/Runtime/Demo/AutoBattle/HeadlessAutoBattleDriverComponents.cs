using Unity.Entities;

namespace GAS.Runtime
{
    public enum HeadlessAutoBattleTargetPolicy : byte
    {
        Frontline = 0,
        LowestHealth = 1,
    }

    public struct CHeadlessAutoBattleDriver : IComponentData
    {
        public bool Enabled;
        public int LastDecisionFrame;
        public int IssuedCommandCount;
        public int IssuedPrimaryCommandCount;
        public int IssuedFinisherCommandCount;
        public int LowestHealthTargetCount;
    }

    public struct CHeadlessAutoBattleUnit : IComponentData
    {
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
        public HeadlessAutoBattleTargetPolicy PrimaryTargetPolicy;
        public HeadlessAutoBattleTargetPolicy FinisherTargetPolicy;
    }

    public struct CHeadlessAutoBattleExecuteCalculation : IComponentData
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
}
