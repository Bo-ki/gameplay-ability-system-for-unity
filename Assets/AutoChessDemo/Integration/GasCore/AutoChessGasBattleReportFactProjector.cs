using System;
using System.Collections.Generic;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasBattleReportFactProjector
    {
        public static AutoChessBattleReportFact[] Project(
            in GasStructuredLogExportSnapshot structuredLog,
            AutoChessRuntimeUnitResolver runtimeUnitResolver)
        {
            var entries = structuredLog.Entries ?? Array.Empty<GasStructuredLogEntry>();
            var facts = new List<AutoChessBattleReportFact>(entries.Length);

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (TryCreateSkillResolvedFact(in entry, runtimeUnitResolver, out var skillFact)
                    || TryCreateHealthReducedFact(in entry, runtimeUnitResolver, out skillFact))
                {
                    facts.Add(skillFact);
                }
            }

            return facts.ToArray();
        }

        private static bool TryCreateSkillResolvedFact(
            in GasStructuredLogEntry entry,
            AutoChessRuntimeUnitResolver runtimeUnitResolver,
            out AutoChessBattleReportFact fact)
        {
            fact = default;
            if (entry.ReplayKind != EDebugReplayEventKind.GameplayEvent
                || entry.GameplayEventType != EGameplayEventType.ExecutionCalculationOutputUpdated
                || entry.EventCode != AutoChessBattleRules.ExecutionCalculationExecuteDamage)
            {
                return false;
            }

            fact = new AutoChessBattleReportFact(
                entry.Frame,
                AutoChessBattleReportFactKind.SkillResolved,
                runtimeUnitResolver.ResolveUnitIndex(entry.SourceAsc),
                runtimeUnitResolver.ResolveUnitIndex(entry.TargetAsc),
                AutoChessBattleRules.AbilityPlayerExecute,
                AutoChessBattleRules.GameplayEffectPlayerExecute,
                entry.Value,
                entry.OldValue,
                entry.NewValue);
            return true;
        }

        private static bool TryCreateHealthReducedFact(
            in GasStructuredLogEntry entry,
            AutoChessRuntimeUnitResolver runtimeUnitResolver,
            out AutoChessBattleReportFact fact)
        {
            fact = default;
            if (entry.ReplayKind != EDebugReplayEventKind.AttributeChange
                || entry.AttrSetCode != AutoChessBattleRules.AttributeSetCombat
                || entry.AttributeCode != AutoChessBattleRules.AttributeHealth
                || entry.NewValue >= entry.OldValue)
            {
                return false;
            }

            fact = new AutoChessBattleReportFact(
                entry.Frame,
                AutoChessBattleReportFactKind.CombatHealthReduced,
                runtimeUnitResolver.ResolveUnitIndex(entry.SourceAsc),
                runtimeUnitResolver.ResolveUnitIndex(entry.TargetAsc),
                0,
                entry.EventCode,
                entry.OldValue - entry.NewValue,
                entry.OldValue,
                entry.NewValue);
            return true;
        }
    }
}
