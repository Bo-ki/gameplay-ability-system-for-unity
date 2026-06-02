using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Entities;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    public enum AutoChessBattleLogKind : byte
    {
        System = 0,
        Skill = 1,
        Damage = 2,
        Death = 3,
        Result = 4,
    }

    public readonly struct AutoChessBattleLogLine
    {
        public readonly int Frame;
        public readonly AutoChessBattleLogKind Kind;
        public readonly string Message;

        public AutoChessBattleLogLine(
            int frame,
            AutoChessBattleLogKind kind,
            string message)
        {
            Frame = frame;
            Kind = kind;
            Message = message ?? string.Empty;
        }

        public override string ToString()
        {
            return "[F" + Frame.ToString("0000", CultureInfo.InvariantCulture) + "] " + Message;
        }
    }

    public readonly struct AutoChessBattleLogSnapshot
    {
        public readonly AutoChessBattleLogLine[] Lines;

        public AutoChessBattleLogSnapshot(AutoChessBattleLogLine[] lines)
        {
            Lines = lines ?? Array.Empty<AutoChessBattleLogLine>();
        }

        public int Count => Lines?.Length ?? 0;

        public string ToText()
        {
            var lines = Lines ?? Array.Empty<AutoChessBattleLogLine>();
            var builder = new StringBuilder(Math.Max(128, lines.Length * 64));
            for (var i = 0; i < lines.Length; i++)
                builder.AppendLine(lines[i].ToString());
            return builder.ToString();
        }
    }

    public static class AutoChessBattleLogBuilder
    {
        private const int MaxLogLines = 220;
        private const int MaxDeploymentLines = 8;
        private const int MaxDetailedBattleGroups = 3;

        public static AutoChessBattleLogSnapshot Build(
            in AutoChessGameRoomDefinition room,
            in GasStructuredLogExportSnapshot structuredLog,
            AutoChessBattleUnitResult[] units,
            AutoChessTeam winner,
            int battleTicks)
        {
            var lines = new List<AutoChessBattleLogLine>(128);
            var unitMap = BuildUnitMap(units);

            Add(lines, 0, AutoChessBattleLogKind.System,
                "游戏开始：" + room.GetPlayerName(AutoChessTeam.Player)
                + " 对阵 " + room.GetPlayerName(AutoChessTeam.Enemy)
                + "，房间=" + room.DisplayName
                + "，棋子数=" + room.Units.Length.ToString(CultureInfo.InvariantCulture) + "。");
            AppendDeploymentLines(lines, room);

            var entries = structuredLog.Entries ?? Array.Empty<GasStructuredLogEntry>();
            var skippedMirrorEvents = 0;
            for (var i = 0; i < entries.Length && lines.Count < MaxLogLines - 1; i++)
            {
                var entry = entries[i];
                if (ShouldFoldMirrorBattleEvent(unitMap, in entry))
                {
                    skippedMirrorEvents++;
                    continue;
                }

                if (TryAppendBattleEvent(lines, unitMap, in entry))
                    continue;
            }

            if (skippedMirrorEvents > 0 && lines.Count < MaxLogLines - 1)
            {
                Add(lines, battleTicks, AutoChessBattleLogKind.System,
                    "已折叠 "
                    + skippedMirrorEvents.ToString(CultureInfo.InvariantCulture)
                    + " 条镜像战场重复日志，当前画面只展示前 "
                    + MaxDetailedBattleGroups.ToString(CultureInfo.InvariantCulture)
                    + " 组的详细过程。");
            }

            Add(lines, battleTicks, AutoChessBattleLogKind.Result,
                "战斗结束：胜者="
                + ResolveWinnerName(room, winner)
                + "，战斗帧="
                + battleTicks.ToString(CultureInfo.InvariantCulture)
                + "。");

            return new AutoChessBattleLogSnapshot(lines.ToArray());
        }

        private static Dictionary<Entity, AutoChessBattleUnitResult> BuildUnitMap(
            AutoChessBattleUnitResult[] units)
        {
            var map = new Dictionary<Entity, AutoChessBattleUnitResult>();
            if (units == null)
                return map;

            for (var i = 0; i < units.Length; i++)
            {
                var unit = units[i];
                if (unit.AscEntity != Entity.Null && !map.ContainsKey(unit.AscEntity))
                    map.Add(unit.AscEntity, unit);
            }

            return map;
        }

        private static void AppendDeploymentLines(
            List<AutoChessBattleLogLine> lines,
            in AutoChessGameRoomDefinition room)
        {
            var units = room.Units ?? Array.Empty<AutoChessUnitDefinition>();
            if (units.Length == 0)
                return;

            var count = Math.Min(units.Length, MaxDeploymentLines);
            for (var i = 0; i < count; i++)
            {
                var unit = units[i];
                Add(lines, 0, AutoChessBattleLogKind.System,
                    room.GetPlayerName(unit.Team)
                    + " 上阵 "
                    + unit.DisplayName
                    + "（"
                    + unit.ArchetypeName
                    + "，槽位"
                    + unit.Slot.ToString(CultureInfo.InvariantCulture)
                    + "，HP "
                    + Format(unit.Health)
                    + "）。");
            }

            if (units.Length > count)
            {
                Add(lines, 0, AutoChessBattleLogKind.System,
                    "其余 " + (units.Length - count).ToString(CultureInfo.InvariantCulture)
                    + " 名缩放棋子已进入各自镜像战场。");
            }
        }

        private static bool TryAppendBattleEvent(
            List<AutoChessBattleLogLine> lines,
            Dictionary<Entity, AutoChessBattleUnitResult> unitMap,
            in GasStructuredLogEntry entry)
        {
            if (entry.ReplayKind == EDebugReplayEventKind.GameplayEvent
                && entry.GameplayEventType == EGameplayEventType.ExecutionCalculationOutputUpdated
                && entry.EventCode == AutoChessBattleRules.ExecutionCalculationExecuteDamage)
            {
                Add(lines, entry.Frame, AutoChessBattleLogKind.Skill,
                    ResolveUnitName(unitMap, entry.SourceAsc)
                    + " 对 "
                    + ResolveUnitName(unitMap, entry.TargetAsc)
                    + " 发动了 "
                    + AutoChessBattleRules.GetAbilityName(AutoChessBattleRules.AbilityPlayerExecute)
                    + "，造成 "
                    + Format(entry.Value)
                    + " 点处决伤害。");
                return true;
            }

            if (entry.ReplayKind != EDebugReplayEventKind.AttributeChange
                || entry.AttrSetCode != AutoChessBattleRules.AttributeSetCombat
                || entry.AttributeCode != AutoChessBattleRules.AttributeHealth
                || entry.NewValue >= entry.OldValue)
            {
                return false;
            }

            var damage = entry.OldValue - entry.NewValue;
            Add(lines, entry.Frame, AutoChessBattleLogKind.Damage,
                ResolveUnitName(unitMap, entry.SourceAsc)
                + " 对 "
                + ResolveUnitName(unitMap, entry.TargetAsc)
                + " 发动了 "
                + AutoChessBattleRules.GetActionNameFromGameplayEffect(entry.EventCode)
                + "，造成 "
                + Format(damage)
                + " 点伤害（HP "
                + Format(entry.OldValue)
                + " -> "
                + Format(entry.NewValue)
                + "）。");

            if (entry.NewValue <= 0f && entry.OldValue > 0f)
            {
                Add(lines, entry.Frame, AutoChessBattleLogKind.Death,
                    ResolveUnitName(unitMap, entry.TargetAsc)
                    + " 受到致命伤害，死亡。");
            }

            return true;
        }

        private static bool ShouldFoldMirrorBattleEvent(
            Dictionary<Entity, AutoChessBattleUnitResult> unitMap,
            in GasStructuredLogEntry entry)
        {
            if (entry.Frame <= 0)
                return false;

            return IsBeyondDetailedGroup(unitMap, entry.SourceAsc)
                   || IsBeyondDetailedGroup(unitMap, entry.TargetAsc);
        }

        private static bool IsBeyondDetailedGroup(
            Dictionary<Entity, AutoChessBattleUnitResult> unitMap,
            Entity entity)
        {
            if (entity == Entity.Null || !unitMap.TryGetValue(entity, out var unit))
                return false;

            return ResolveBattleGroup(unit.Id) >= MaxDetailedBattleGroups;
        }

        private static int ResolveBattleGroup(string unitId)
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

        private static string ResolveUnitName(
            Dictionary<Entity, AutoChessBattleUnitResult> unitMap,
            Entity entity)
        {
            if (entity != Entity.Null && unitMap.TryGetValue(entity, out var unit))
                return unit.OwnerName + "的" + unit.DisplayName;

            return "战场系统";
        }

        private static string ResolveWinnerName(
            in AutoChessGameRoomDefinition room,
            AutoChessTeam winner)
        {
            if (winner == AutoChessTeam.Draw)
                return "平局";

            if (winner == AutoChessTeam.None)
                return "未决";

            return room.GetPlayerName(winner);
        }

        private static void Add(
            List<AutoChessBattleLogLine> lines,
            int frame,
            AutoChessBattleLogKind kind,
            string message)
        {
            if (lines.Count >= MaxLogLines)
                return;

            lines.Add(new AutoChessBattleLogLine(frame, kind, message));
        }

        private static string Format(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
