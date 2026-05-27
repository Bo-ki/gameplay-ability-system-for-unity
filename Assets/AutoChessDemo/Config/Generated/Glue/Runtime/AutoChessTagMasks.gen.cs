///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;

namespace GAS.Runtime.Generated
{
    public static class AutoChessTagCodes
    {
        public const int AbilityActing = 4;
        public const int ManaBurstCooldown = 5;
        public const int ArcaneTeamBuff = 6;
        public const int ArcaneStormDebuff = 7;
        public const int AutoChessStunned = 8;
        public const int PlayerStunCooldown = 9;
        public const int AutoChessShielded = 10;
        public const int PlayerBarrierCooldown = 11;
        public const int AutoChessSummoned = 12;
        public const int PlayerSummonCooldown = 13;
        public const int AutoChessCounterReady = 14;
        public const int PlayerCleanseCooldown = 15;
        public const int AutoChessCleanseRallied = 16;
        public const int AutoChessLifeStealReady = 17;
        public const int AutoChessPoisoned = 18;
        public const int AutoChessExecutionReady = 19;
        public const int AutoChessExecuted = 20;
        public const int AutoChessDeathBurstReady = 21;
        public const int AutoChessEnraged = 22;
    }

    public static class AutoChessTagBits
    {
        public const int AbilityActingIndex = 0;
        public const int AbilityActingBlock = 0;
        public const ulong AbilityActingMask = 1UL << 0;
        public const int ManaBurstCooldownIndex = 1;
        public const int ManaBurstCooldownBlock = 0;
        public const ulong ManaBurstCooldownMask = 1UL << 1;
        public const int ArcaneTeamBuffIndex = 2;
        public const int ArcaneTeamBuffBlock = 0;
        public const ulong ArcaneTeamBuffMask = 1UL << 2;
        public const int ArcaneStormDebuffIndex = 3;
        public const int ArcaneStormDebuffBlock = 0;
        public const ulong ArcaneStormDebuffMask = 1UL << 3;
        public const int AutoChessStunnedIndex = 4;
        public const int AutoChessStunnedBlock = 0;
        public const ulong AutoChessStunnedMask = 1UL << 4;
        public const int PlayerStunCooldownIndex = 5;
        public const int PlayerStunCooldownBlock = 0;
        public const ulong PlayerStunCooldownMask = 1UL << 5;
        public const int AutoChessShieldedIndex = 6;
        public const int AutoChessShieldedBlock = 0;
        public const ulong AutoChessShieldedMask = 1UL << 6;
        public const int PlayerBarrierCooldownIndex = 7;
        public const int PlayerBarrierCooldownBlock = 0;
        public const ulong PlayerBarrierCooldownMask = 1UL << 7;
        public const int AutoChessSummonedIndex = 8;
        public const int AutoChessSummonedBlock = 0;
        public const ulong AutoChessSummonedMask = 1UL << 8;
        public const int PlayerSummonCooldownIndex = 9;
        public const int PlayerSummonCooldownBlock = 0;
        public const ulong PlayerSummonCooldownMask = 1UL << 9;
        public const int AutoChessCounterReadyIndex = 10;
        public const int AutoChessCounterReadyBlock = 0;
        public const ulong AutoChessCounterReadyMask = 1UL << 10;
        public const int PlayerCleanseCooldownIndex = 11;
        public const int PlayerCleanseCooldownBlock = 0;
        public const ulong PlayerCleanseCooldownMask = 1UL << 11;
        public const int AutoChessCleanseRalliedIndex = 12;
        public const int AutoChessCleanseRalliedBlock = 0;
        public const ulong AutoChessCleanseRalliedMask = 1UL << 12;
        public const int AutoChessLifeStealReadyIndex = 13;
        public const int AutoChessLifeStealReadyBlock = 0;
        public const ulong AutoChessLifeStealReadyMask = 1UL << 13;
        public const int AutoChessPoisonedIndex = 14;
        public const int AutoChessPoisonedBlock = 0;
        public const ulong AutoChessPoisonedMask = 1UL << 14;
        public const int AutoChessExecutionReadyIndex = 15;
        public const int AutoChessExecutionReadyBlock = 0;
        public const ulong AutoChessExecutionReadyMask = 1UL << 15;
        public const int AutoChessExecutedIndex = 16;
        public const int AutoChessExecutedBlock = 0;
        public const ulong AutoChessExecutedMask = 1UL << 16;
        public const int AutoChessDeathBurstReadyIndex = 17;
        public const int AutoChessDeathBurstReadyBlock = 0;
        public const ulong AutoChessDeathBurstReadyMask = 1UL << 17;
        public const int AutoChessEnragedIndex = 18;
        public const int AutoChessEnragedBlock = 0;
        public const ulong AutoChessEnragedMask = 1UL << 18;
    }

    public static class AutoChessTagMaskTable
    {
        public const int Count = 19;

        public static bool TryGetDenseIndex(int tagCode, out int denseIndex)
        {
            switch (tagCode)
            {
                case AutoChessTagCodes.AbilityActing: denseIndex = AutoChessTagBits.AbilityActingIndex; return true;
                case AutoChessTagCodes.ManaBurstCooldown: denseIndex = AutoChessTagBits.ManaBurstCooldownIndex; return true;
                case AutoChessTagCodes.ArcaneTeamBuff: denseIndex = AutoChessTagBits.ArcaneTeamBuffIndex; return true;
                case AutoChessTagCodes.ArcaneStormDebuff: denseIndex = AutoChessTagBits.ArcaneStormDebuffIndex; return true;
                case AutoChessTagCodes.AutoChessStunned: denseIndex = AutoChessTagBits.AutoChessStunnedIndex; return true;
                case AutoChessTagCodes.PlayerStunCooldown: denseIndex = AutoChessTagBits.PlayerStunCooldownIndex; return true;
                case AutoChessTagCodes.AutoChessShielded: denseIndex = AutoChessTagBits.AutoChessShieldedIndex; return true;
                case AutoChessTagCodes.PlayerBarrierCooldown: denseIndex = AutoChessTagBits.PlayerBarrierCooldownIndex; return true;
                case AutoChessTagCodes.AutoChessSummoned: denseIndex = AutoChessTagBits.AutoChessSummonedIndex; return true;
                case AutoChessTagCodes.PlayerSummonCooldown: denseIndex = AutoChessTagBits.PlayerSummonCooldownIndex; return true;
                case AutoChessTagCodes.AutoChessCounterReady: denseIndex = AutoChessTagBits.AutoChessCounterReadyIndex; return true;
                case AutoChessTagCodes.PlayerCleanseCooldown: denseIndex = AutoChessTagBits.PlayerCleanseCooldownIndex; return true;
                case AutoChessTagCodes.AutoChessCleanseRallied: denseIndex = AutoChessTagBits.AutoChessCleanseRalliedIndex; return true;
                case AutoChessTagCodes.AutoChessLifeStealReady: denseIndex = AutoChessTagBits.AutoChessLifeStealReadyIndex; return true;
                case AutoChessTagCodes.AutoChessPoisoned: denseIndex = AutoChessTagBits.AutoChessPoisonedIndex; return true;
                case AutoChessTagCodes.AutoChessExecutionReady: denseIndex = AutoChessTagBits.AutoChessExecutionReadyIndex; return true;
                case AutoChessTagCodes.AutoChessExecuted: denseIndex = AutoChessTagBits.AutoChessExecutedIndex; return true;
                case AutoChessTagCodes.AutoChessDeathBurstReady: denseIndex = AutoChessTagBits.AutoChessDeathBurstReadyIndex; return true;
                case AutoChessTagCodes.AutoChessEnraged: denseIndex = AutoChessTagBits.AutoChessEnragedIndex; return true;
                default: denseIndex = -1; return false;
            }
        }

        public static bool TryCreateMask(int tagCode, out CTagMask mask)
        {
            mask = default;
            if (!TryGetDenseIndex(tagCode, out var denseIndex)) return false;
            mask.AddTag(denseIndex);
            return true;
        }
    }

    public static class AutoChessTagCheck
    {
        public static bool HasTag(in CTagMask mask, int tagCode)
        {
            return AutoChessTagMaskTable.TryGetDenseIndex(tagCode, out var denseIndex) && mask.HasTag(denseIndex);
        }

        public static bool HasAnyTag(in CTagMask mask, in CTagMask other) => mask.HasAnyTag(other);
        public static bool HasAllTags(in CTagMask mask, in CTagMask other) => mask.HasAllTags(other);
        public static bool HasAbilityActing(in CTagMask mask) => mask.HasTag(AutoChessTagBits.AbilityActingIndex);
        public static bool HasManaBurstCooldown(in CTagMask mask) => mask.HasTag(AutoChessTagBits.ManaBurstCooldownIndex);
        public static bool HasArcaneTeamBuff(in CTagMask mask) => mask.HasTag(AutoChessTagBits.ArcaneTeamBuffIndex);
        public static bool HasArcaneStormDebuff(in CTagMask mask) => mask.HasTag(AutoChessTagBits.ArcaneStormDebuffIndex);
        public static bool HasAutoChessStunned(in CTagMask mask) => mask.HasTag(AutoChessTagBits.AutoChessStunnedIndex);
        public static bool HasPlayerStunCooldown(in CTagMask mask) => mask.HasTag(AutoChessTagBits.PlayerStunCooldownIndex);
        public static bool HasAutoChessShielded(in CTagMask mask) => mask.HasTag(AutoChessTagBits.AutoChessShieldedIndex);
        public static bool HasPlayerBarrierCooldown(in CTagMask mask) => mask.HasTag(AutoChessTagBits.PlayerBarrierCooldownIndex);
        public static bool HasAutoChessSummoned(in CTagMask mask) => mask.HasTag(AutoChessTagBits.AutoChessSummonedIndex);
        public static bool HasPlayerSummonCooldown(in CTagMask mask) => mask.HasTag(AutoChessTagBits.PlayerSummonCooldownIndex);
        public static bool HasAutoChessCounterReady(in CTagMask mask) => mask.HasTag(AutoChessTagBits.AutoChessCounterReadyIndex);
        public static bool HasPlayerCleanseCooldown(in CTagMask mask) => mask.HasTag(AutoChessTagBits.PlayerCleanseCooldownIndex);
        public static bool HasAutoChessCleanseRallied(in CTagMask mask) => mask.HasTag(AutoChessTagBits.AutoChessCleanseRalliedIndex);
        public static bool HasAutoChessLifeStealReady(in CTagMask mask) => mask.HasTag(AutoChessTagBits.AutoChessLifeStealReadyIndex);
        public static bool HasAutoChessPoisoned(in CTagMask mask) => mask.HasTag(AutoChessTagBits.AutoChessPoisonedIndex);
        public static bool HasAutoChessExecutionReady(in CTagMask mask) => mask.HasTag(AutoChessTagBits.AutoChessExecutionReadyIndex);
        public static bool HasAutoChessExecuted(in CTagMask mask) => mask.HasTag(AutoChessTagBits.AutoChessExecutedIndex);
        public static bool HasAutoChessDeathBurstReady(in CTagMask mask) => mask.HasTag(AutoChessTagBits.AutoChessDeathBurstReadyIndex);
        public static bool HasAutoChessEnraged(in CTagMask mask) => mask.HasTag(AutoChessTagBits.AutoChessEnragedIndex);
    }
}
