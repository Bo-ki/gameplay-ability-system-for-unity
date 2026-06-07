using System;
using System.Collections.Generic;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessBattleReportBuilder
    {
        public static AutoChessBattleReport Build(
            AutoChessBattleUnitResult[] units,
            AutoChessBattleReportFact[] reportFacts)
        {
            var reportUnits = CreateReportUnits(units);
            var events = CreateReportEvents(reportFacts);
            return new AutoChessBattleReport(reportUnits, events);
        }

        private static AutoChessBattleReportUnit[] CreateReportUnits(
            AutoChessBattleUnitResult[] units)
        {
            if (units == null || units.Length == 0)
                return Array.Empty<AutoChessBattleReportUnit>();

            var reportUnits = new AutoChessBattleReportUnit[units.Length];
            for (var i = 0; i < units.Length; i++)
            {
                var unit = units[i];
                reportUnits[i] = new AutoChessBattleReportUnit(
                    i,
                    unit.Id,
                    unit.DisplayName,
                    unit.OwnerName,
                    unit.Team,
                    unit.Slot,
                    AutoChessGameRoomFactory.ResolveBattleGroup(unit.Id));
            }

            return reportUnits;
        }

        private static AutoChessBattleReportEvent[] CreateReportEvents(
            AutoChessBattleReportFact[] reportFacts)
        {
            reportFacts ??= Array.Empty<AutoChessBattleReportFact>();
            var events = new List<AutoChessBattleReportEvent>(reportFacts.Length);
            var killedUnits = new HashSet<int>();
            for (var i = 0; i < reportFacts.Length; i++)
            {
                var fact = reportFacts[i];
                TryCreateReportEvents(in fact, killedUnits, events);
            }

            return events.ToArray();
        }

        private static bool TryCreateReportEvents(
            in AutoChessBattleReportFact fact,
            HashSet<int> killedUnits,
            List<AutoChessBattleReportEvent> events)
        {
            if (fact.Kind == AutoChessBattleReportFactKind.SkillResolved)
            {
                events.Add(new AutoChessBattleReportEvent(
                    fact.Frame,
                    AutoChessBattleReportEventKind.SkillResolved,
                    fact.SourceUnitIndex,
                    fact.TargetUnitIndex,
                    fact.AbilityCode,
                    fact.GameplayEffectCode,
                    fact.Value,
                    fact.OldValue,
                    fact.NewValue));
                return true;
            }

            if (fact.Kind != AutoChessBattleReportFactKind.CombatHealthReduced)
            {
                return false;
            }

            events.Add(new AutoChessBattleReportEvent(
                fact.Frame,
                AutoChessBattleReportEventKind.DamageApplied,
                fact.SourceUnitIndex,
                fact.TargetUnitIndex,
                0,
                fact.GameplayEffectCode,
                fact.Value,
                fact.OldValue,
                fact.NewValue));

            if (fact.NewValue <= 0f
                && fact.OldValue > 0f
                && fact.TargetUnitIndex >= 0
                && killedUnits.Add(fact.TargetUnitIndex))
            {
                events.Add(new AutoChessBattleReportEvent(
                    fact.Frame,
                    AutoChessBattleReportEventKind.UnitDied,
                    fact.SourceUnitIndex,
                    fact.TargetUnitIndex,
                    0,
                    fact.GameplayEffectCode,
                    0f,
                    fact.OldValue,
                    fact.NewValue));
            }

            return true;
        }
    }
}
