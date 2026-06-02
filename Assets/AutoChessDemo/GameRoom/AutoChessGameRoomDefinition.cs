using System;

namespace GAS.AutoChessDemo
{
    public static class AutoChessBattleRules
    {
        public const int AttributeSetCombat = 9001;
        public const int AttributeHealth = 1;
        public const int AttributeEnergy = 2;

        public const int AbilityPlayerAttack = 9101;
        public const int AbilityEnemyAttack = 9102;
        public const int AbilityPlayerExecute = 9103;

        public const int GameplayEffectPlayerAttackDamage = 9201;
        public const int GameplayEffectEnemyAttackDamage = 9202;
        public const int GameplayEffectPlayerExecute = 9207;

        public const int ExecutionCalculationExecuteDamage = 9401;
        public const int ExecutionCalculationExecuteDamageOutput = 9402;

        public const int TagAttackCooldown = 1;

        public static string GetTeamName(AutoChessTeam team)
        {
            return team switch
            {
                AutoChessTeam.Player => "玩家A",
                AutoChessTeam.Enemy => "玩家B",
                AutoChessTeam.Draw => "平局",
                _ => "未分组",
            };
        }

        public static string GetAbilityName(int abilityCode)
        {
            return abilityCode switch
            {
                AbilityPlayerAttack => "破阵斩",
                AbilityEnemyAttack => "蛮力打击",
                AbilityPlayerExecute => "斩杀追击",
                _ => "未知技能",
            };
        }

        public static string GetGameplayEffectName(int gameplayEffectCode)
        {
            return gameplayEffectCode switch
            {
                GameplayEffectPlayerAttackDamage => "破阵斩伤害",
                GameplayEffectEnemyAttackDamage => "蛮力打击伤害",
                GameplayEffectPlayerExecute => "斩杀追击伤害",
                _ => "未知效果",
            };
        }

        public static string GetActionNameFromGameplayEffect(int gameplayEffectCode)
        {
            return gameplayEffectCode switch
            {
                GameplayEffectPlayerAttackDamage => GetAbilityName(AbilityPlayerAttack),
                GameplayEffectEnemyAttackDamage => GetAbilityName(AbilityEnemyAttack),
                GameplayEffectPlayerExecute => GetAbilityName(AbilityPlayerExecute),
                _ => GetGameplayEffectName(gameplayEffectCode),
            };
        }
    }

    public readonly struct AutoChessPlayerSeat
    {
        public readonly AutoChessTeam Team;
        public readonly string PlayerId;
        public readonly string DisplayName;

        public AutoChessPlayerSeat(
            AutoChessTeam team,
            string playerId,
            string displayName)
        {
            Team = team;
            PlayerId = playerId ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? AutoChessBattleRules.GetTeamName(team)
                : displayName;
        }
    }

    public readonly struct AutoChessUnitDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string ArchetypeName;
        public readonly int BattleGroup;
        public readonly AutoChessTeam Team;
        public readonly int Slot;
        public readonly float Health;
        public readonly float Energy;
        public readonly int PrimaryAbilityCode;
        public readonly int FinisherAbilityCode;
        public readonly float FinisherHealthThreshold;
        public readonly AutoChessTargetPolicy PrimaryTargetPolicy;
        public readonly AutoChessTargetPolicy FinisherTargetPolicy;

        public AutoChessUnitDefinition(
            string id,
            string displayName,
            string archetypeName,
            int battleGroup,
            AutoChessTeam team,
            int slot,
            float health,
            float energy,
            int primaryAbilityCode,
            int finisherAbilityCode,
            float finisherHealthThreshold,
            AutoChessTargetPolicy primaryTargetPolicy,
            AutoChessTargetPolicy finisherTargetPolicy)
        {
            Id = id ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
            ArchetypeName = string.IsNullOrWhiteSpace(archetypeName) ? DisplayName : archetypeName;
            BattleGroup = battleGroup;
            Team = team;
            Slot = slot;
            Health = health;
            Energy = energy;
            PrimaryAbilityCode = primaryAbilityCode;
            FinisherAbilityCode = finisherAbilityCode;
            FinisherHealthThreshold = finisherHealthThreshold;
            PrimaryTargetPolicy = primaryTargetPolicy;
            FinisherTargetPolicy = finisherTargetPolicy;
        }

        public AutoChessUnitDefinition WithScaleGroup(int battleGroup)
        {
            if (battleGroup == BattleGroup)
                return this;

            return new AutoChessUnitDefinition(
                Id + "-g" + battleGroup,
                DisplayName + " #" + (battleGroup + 1),
                ArchetypeName,
                battleGroup,
                Team,
                Slot,
                Health,
                Energy,
                PrimaryAbilityCode,
                FinisherAbilityCode,
                FinisherHealthThreshold,
                PrimaryTargetPolicy,
                FinisherTargetPolicy);
        }

        public int[] CreateAbilityCodes()
        {
            return FinisherAbilityCode > 0
                ? new[] { PrimaryAbilityCode, FinisherAbilityCode }
                : new[] { PrimaryAbilityCode };
        }
    }

    public readonly struct AutoChessGameRoomDefinition
    {
        public readonly string RoomId;
        public readonly string DisplayName;
        public readonly AutoChessPlayerSeat PlayerSeat;
        public readonly AutoChessPlayerSeat EnemySeat;
        public readonly AutoChessUnitDefinition[] Units;

        public AutoChessGameRoomDefinition(
            string roomId,
            string displayName,
            AutoChessPlayerSeat playerSeat,
            AutoChessPlayerSeat enemySeat,
            AutoChessUnitDefinition[] units)
        {
            RoomId = string.IsNullOrWhiteSpace(roomId) ? "autochess-room" : roomId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? RoomId : displayName;
            PlayerSeat = playerSeat;
            EnemySeat = enemySeat;
            Units = units ?? Array.Empty<AutoChessUnitDefinition>();
        }

        public string GetPlayerName(AutoChessTeam team)
        {
            return team switch
            {
                AutoChessTeam.Player => PlayerSeat.DisplayName,
                AutoChessTeam.Enemy => EnemySeat.DisplayName,
                AutoChessTeam.Draw => "双方",
                _ => AutoChessBattleRules.GetTeamName(team),
            };
        }
    }

    public static class AutoChessGameRoomFactory
    {
        public static AutoChessGameRoomDefinition CreateDefaultRoom(
            int scale,
            float healthMultiplier)
        {
            var multiplier = healthMultiplier > 0f ? healthMultiplier : 1f;
            var baseUnits = new[]
            {
                new AutoChessUnitDefinition(
                    "player-vanguard",
                    "霜卫先锋",
                    "前排战士",
                    0,
                    AutoChessTeam.Player,
                    0,
                    72f * multiplier,
                    8f,
                    AutoChessBattleRules.AbilityPlayerAttack,
                    AutoChessBattleRules.AbilityPlayerExecute,
                    44f * multiplier,
                    AutoChessTargetPolicy.Frontline,
                    AutoChessTargetPolicy.LowestHealth),
                new AutoChessUnitDefinition(
                    "player-ranger",
                    "游侠射手",
                    "后排射手",
                    0,
                    AutoChessTeam.Player,
                    1,
                    54f * multiplier,
                    8f,
                    AutoChessBattleRules.AbilityPlayerAttack,
                    AutoChessBattleRules.AbilityPlayerExecute,
                    44f * multiplier,
                    AutoChessTargetPolicy.LowestHealth,
                    AutoChessTargetPolicy.LowestHealth),
                new AutoChessUnitDefinition(
                    "enemy-brute",
                    "荒原斗士",
                    "前排斗士",
                    0,
                    AutoChessTeam.Enemy,
                    0,
                    48f * multiplier,
                    8f,
                    AutoChessBattleRules.AbilityEnemyAttack,
                    0,
                    0f,
                    AutoChessTargetPolicy.Frontline,
                    AutoChessTargetPolicy.Frontline),
                new AutoChessUnitDefinition(
                    "enemy-caster",
                    "秘术术士",
                    "后排法师",
                    0,
                    AutoChessTeam.Enemy,
                    1,
                    42f * multiplier,
                    8f,
                    AutoChessBattleRules.AbilityEnemyAttack,
                    0,
                    0f,
                    AutoChessTargetPolicy.Frontline,
                    AutoChessTargetPolicy.Frontline),
            };

            var normalizedScale = scale > 0 ? scale : 1;
            var units = normalizedScale <= 1
                ? baseUnits
                : ExpandScaleGroups(baseUnits, normalizedScale);

            return new AutoChessGameRoomDefinition(
                "room-standard-duel-x" + normalizedScale,
                "标准双人自走棋对局",
                new AutoChessPlayerSeat(AutoChessTeam.Player, "player-a", "玩家A"),
                new AutoChessPlayerSeat(AutoChessTeam.Enemy, "player-b", "玩家B"),
                units);
        }

        private static AutoChessUnitDefinition[] ExpandScaleGroups(
            AutoChessUnitDefinition[] baseUnits,
            int scale)
        {
            var units = new AutoChessUnitDefinition[baseUnits.Length * scale];
            var index = 0;
            for (var group = 0; group < scale; group++)
            {
                for (var i = 0; i < baseUnits.Length; i++)
                    units[index++] = baseUnits[i].WithScaleGroup(group);
            }

            return units;
        }
    }
}
