using System;
using System.Globalization;

namespace GAS.AutoChessDemo
{
    public static class AutoChessBattleRules
    {
        public const int AttributeSetCombat = AutoChessGeneratedConfig.AttributeSetCombat;
        public const int AttributeHealth = AutoChessGeneratedConfig.AttributeHealth;
        public const int AttributeEnergy = AutoChessGeneratedConfig.AttributeEnergy;

        public const int AbilityPlayerAttack = AutoChessGeneratedConfig.AbilityPlayerAttack;
        public const int AbilityEnemyAttack = AutoChessGeneratedConfig.AbilityEnemyAttack;
        public const int AbilityPlayerExecute = AutoChessGeneratedConfig.AbilityPlayerExecute;

        public const int GameplayEffectPlayerAttackDamage = AutoChessGeneratedConfig.GameplayEffectPlayerAttackDamage;
        public const int GameplayEffectEnemyAttackDamage = AutoChessGeneratedConfig.GameplayEffectEnemyAttackDamage;
        public const int GameplayEffectPlayerExecute = AutoChessGeneratedConfig.GameplayEffectPlayerExecute;

        public const int ExecutionCalculationExecuteDamage = AutoChessGeneratedConfig.ExecutionCalculationExecuteDamage;
        public const int ExecutionCalculationExecuteDamageOutput = AutoChessGeneratedConfig.ExecutionCalculationExecuteDamageOutput;

        public const int TagAttackCooldown = AutoChessGeneratedConfig.TagAttackCooldown;

        public static string GetTeamName(AutoChessTeam team)
        {
            return team switch
            {
                AutoChessTeam.Player => AutoChessGeneratedConfig.PlayerDisplayName,
                AutoChessTeam.Enemy => AutoChessGeneratedConfig.EnemyDisplayName,
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
            var normalizedScale = scale > 0 ? scale : 1;
            var normalizedHealthMultiplier = healthMultiplier > 0f ? healthMultiplier : 1f;
            return new AutoChessGameRoomDefinition(
                AutoChessGeneratedConfig.RoomIdPrefix + normalizedScale,
                AutoChessGeneratedConfig.RoomDisplayName,
                new AutoChessPlayerSeat(
                    AutoChessTeam.Player,
                    AutoChessGeneratedConfig.PlayerId,
                    AutoChessGeneratedConfig.PlayerDisplayName),
                new AutoChessPlayerSeat(
                    AutoChessTeam.Enemy,
                    AutoChessGeneratedConfig.EnemyId,
                    AutoChessGeneratedConfig.EnemyDisplayName),
                ExpandScaleGroups(CreateBaseUnits(normalizedHealthMultiplier), normalizedScale));
        }

        public static int ResolveBattleGroup(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
                return 0;

            var marker = unitId.LastIndexOf("-g", StringComparison.Ordinal);
            if (marker < 0 || marker + 2 >= unitId.Length)
                return 0;

            return int.TryParse(
                unitId.Substring(marker + 2),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var group)
                ? group
                : 0;
        }

        private static AutoChessUnitDefinition[] CreateBaseUnits(float healthMultiplier)
        {
            var rows = AutoChessGeneratedConfig.CreateBaseUnitRows();
            var units = new AutoChessUnitDefinition[rows.Length];
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                units[i] = new AutoChessUnitDefinition(
                    row.Id,
                    row.DisplayName,
                    row.ArchetypeName,
                    0,
                    row.Team,
                    row.Slot,
                    row.Health * healthMultiplier,
                    row.Energy,
                    row.PrimaryAbilityCode,
                    row.FinisherAbilityCode,
                    row.FinisherHealthThreshold * healthMultiplier,
                    row.PrimaryTargetPolicy,
                    row.FinisherTargetPolicy);
            }

            return units;
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
                {
                    units[index] = baseUnits[i].WithScaleGroup(group);
                    index++;
                }
            }

            return units;
        }
    }
}
