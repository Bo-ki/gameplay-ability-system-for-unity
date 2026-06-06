using System;
using System.Collections.Generic;
using Unity.Entities;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    internal readonly struct AutoChessBattleRuntimeUnitIndex
    {
        public readonly int UnitIndex;
        public readonly Entity AscEntity;

        public AutoChessBattleRuntimeUnitIndex(int unitIndex, Entity ascEntity)
        {
            UnitIndex = unitIndex;
            AscEntity = ascEntity;
        }
    }

    internal static class AutoChessBattleResultBuilder
    {
        public static AutoChessBattleResult Build(
            AutoChessBattleSession session,
            bool completed,
            AutoChessTeam winner,
            int scenarioScale,
            int battleTicks,
            int totalTicks,
            int warmupDroppedTicks,
            int measuredTicks,
            AutoChessBattleDriverComponent driverStats,
            long elapsedTicks,
            double elapsedMilliseconds,
            long measuredElapsedTicks,
            double measuredElapsedMilliseconds,
            AutoChessBattleRuntimeTiming runtimeTiming,
            GasRuntimeOfficialToolDiffSnapshot officialToolDiff)
        {
            var units = session.CreateUnitResults();
            var runtimeUnitIndex = session.CreateRuntimeUnitIndex();
            var coreObservation = AutoChessGasCoreBridge.CreateObservationSnapshot();
            var battleReport = AutoChessBattleReportBuilder.Build(
                units,
                runtimeUnitIndex,
                coreObservation.StructuredLog);
            var battleLog = AutoChessBattleLogBuilder.Build(
                session.Room,
                battleReport,
                winner,
                battleTicks);

            return new AutoChessBattleResult(
                session.Room.RoomId,
                completed,
                winner,
                scenarioScale,
                battleTicks,
                totalTicks,
                warmupDroppedTicks,
                measuredTicks,
                driverStats.IssuedCommandCount,
                driverStats.IssuedPrimaryCommandCount,
                driverStats.IssuedFinisherCommandCount,
                driverStats.LowestHealthTargetCount,
                elapsedTicks,
                elapsedMilliseconds,
                measuredElapsedTicks,
                measuredElapsedMilliseconds,
                runtimeTiming,
                units,
                coreObservation.EventCounts,
                coreObservation.RuntimeDiagnostics,
                coreObservation.RuntimeDiagnosticsLog,
                officialToolDiff,
                coreObservation.StructuredLog,
                battleReport,
                battleLog,
                coreObservation.AssertionLog);
        }
    }

    internal static class AutoChessBattleReportBuilder
    {
        public static AutoChessBattleReport Build(
            AutoChessBattleUnitResult[] units,
            AutoChessBattleRuntimeUnitIndex[] runtimeUnitIndex,
            in GasStructuredLogExportSnapshot structuredLog)
        {
            var reportUnits = CreateReportUnits(units);
            var unitIndexByAsc = CreateUnitIndexByAsc(runtimeUnitIndex);
            var events = CreateReportEvents(structuredLog, unitIndexByAsc);
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

        private static Dictionary<Entity, int> CreateUnitIndexByAsc(
            AutoChessBattleRuntimeUnitIndex[] runtimeUnitIndex)
        {
            var map = new Dictionary<Entity, int>();
            if (runtimeUnitIndex == null)
                return map;

            for (var i = 0; i < runtimeUnitIndex.Length; i++)
            {
                var index = runtimeUnitIndex[i];
                var entity = index.AscEntity;
                if (entity != Entity.Null && !map.ContainsKey(entity))
                    map.Add(entity, index.UnitIndex);
            }

            return map;
        }

        private static AutoChessBattleReportEvent[] CreateReportEvents(
            in GasStructuredLogExportSnapshot structuredLog,
            Dictionary<Entity, int> unitIndexByAsc)
        {
            var entries = structuredLog.Entries ?? Array.Empty<GasStructuredLogEntry>();
            var events = new List<AutoChessBattleReportEvent>(entries.Length);
            var killedUnits = new HashSet<int>();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                TryCreateReportEvents(in entry, unitIndexByAsc, killedUnits, events);
            }

            return events.ToArray();
        }

        private static bool TryCreateReportEvents(
            in GasStructuredLogEntry entry,
            Dictionary<Entity, int> unitIndexByAsc,
            HashSet<int> killedUnits,
            List<AutoChessBattleReportEvent> events)
        {
            if (entry.ReplayKind == EDebugReplayEventKind.GameplayEvent
                && entry.GameplayEventType == EGameplayEventType.ExecutionCalculationOutputUpdated
                && entry.EventCode == AutoChessBattleRules.ExecutionCalculationExecuteDamage)
            {
                events.Add(new AutoChessBattleReportEvent(
                    entry.Frame,
                    AutoChessBattleReportEventKind.SkillResolved,
                    ResolveUnitIndex(unitIndexByAsc, entry.SourceAsc),
                    ResolveUnitIndex(unitIndexByAsc, entry.TargetAsc),
                    AutoChessBattleRules.AbilityPlayerExecute,
                    AutoChessBattleRules.GameplayEffectPlayerExecute,
                    entry.Value,
                    entry.OldValue,
                    entry.NewValue));
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
            var sourceUnitIndex = ResolveUnitIndex(unitIndexByAsc, entry.SourceAsc);
            var targetUnitIndex = ResolveUnitIndex(unitIndexByAsc, entry.TargetAsc);
            events.Add(new AutoChessBattleReportEvent(
                entry.Frame,
                AutoChessBattleReportEventKind.DamageApplied,
                sourceUnitIndex,
                targetUnitIndex,
                0,
                entry.EventCode,
                damage,
                entry.OldValue,
                entry.NewValue));

            if (entry.NewValue <= 0f
                && entry.OldValue > 0f
                && targetUnitIndex >= 0
                && killedUnits.Add(targetUnitIndex))
            {
                events.Add(new AutoChessBattleReportEvent(
                    entry.Frame,
                    AutoChessBattleReportEventKind.UnitDied,
                    sourceUnitIndex,
                    targetUnitIndex,
                    0,
                    entry.EventCode,
                    0f,
                    entry.OldValue,
                    entry.NewValue));
            }

            return true;
        }

        private static int ResolveUnitIndex(
            Dictionary<Entity, int> unitIndexByAsc,
            Entity entity)
        {
            return entity != Entity.Null && unitIndexByAsc.TryGetValue(entity, out var index)
                ? index
                : -1;
        }
    }
}
