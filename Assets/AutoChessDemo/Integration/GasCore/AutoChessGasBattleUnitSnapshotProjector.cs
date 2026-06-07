using System;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasBattleUnitSnapshotProjector
    {
        public static AutoChessCombatAttributeSnapshot[] Project(
            in GasStructuredLogExportSnapshot structuredLog,
            AutoChessGasBattleUnitHandle[] handles,
            AutoChessUnitDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
                return Array.Empty<AutoChessCombatAttributeSnapshot>();

            var count = definitions.Length;
            var health = new float[count];
            var energy = new float[count];
            var reportKeys = new int[count];

            for (var i = 0; i < count; i++)
            {
                var definition = definitions[i];
                health[i] = definition.Health;
                energy[i] = definition.Energy;
                reportKeys[i] = handles != null && i < handles.Length && handles[i].IsValid
                    ? handles[i].Key.ReportKey
                    : 0;
            }

            var entries = structuredLog.Entries ?? Array.Empty<GasStructuredLogEntry>();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.ReplayKind != EDebugReplayEventKind.AttributeChange
                    || entry.AttrSetCode != AutoChessBattleRules.AttributeSetCombat
                    || entry.TargetReportKey <= 0)
                {
                    continue;
                }

                var unitIndex = ResolveUnitIndex(reportKeys, entry.TargetReportKey);
                if (unitIndex < 0)
                    continue;

                if (entry.AttributeCode == AutoChessBattleRules.AttributeHealth)
                    health[unitIndex] = entry.NewValue;
                else if (entry.AttributeCode == AutoChessBattleRules.AttributeEnergy)
                    energy[unitIndex] = entry.NewValue;
            }

            var snapshots = new AutoChessCombatAttributeSnapshot[count];
            for (var i = 0; i < count; i++)
                snapshots[i] = new AutoChessCombatAttributeSnapshot(health[i], energy[i]);

            return snapshots;
        }

        private static int ResolveUnitIndex(int[] reportKeys, int reportKey)
        {
            for (var i = 0; i < reportKeys.Length; i++)
            {
                if (reportKeys[i] == reportKey)
                    return i;
            }

            return -1;
        }
    }
}
