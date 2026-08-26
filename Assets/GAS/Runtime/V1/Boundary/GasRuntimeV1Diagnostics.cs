using System.Globalization;
using System.Text;

namespace GAS.Runtime
{
    /// <summary>
    /// 将 Runtime v1 managed read model 格式化为验证日志；不查询 World、不持有 Entity、也不写入 gameplay 状态。
    /// </summary>
    public static class GasRuntimeV1Diagnostics
    {
        /// <summary>
        /// 导出性能 scorecard 的稳定键值文本，供 Headless runner 与报告复用。
        /// </summary>
        public static string ExportDataOrientedScorecardToText(
            in GasRuntimeDataOrientedScorecard scorecard)
        {
            var builder = new StringBuilder(1024);
            builder.Append("runtimeDataOrientedScorecard|source=RuntimeV1ReadModel")
                .Append("|units=").Append(scorecard.UnitCount)
                .Append("|measuredTicks=").Append(scorecard.MeasuredTicks)
                .Append("|commands=").Append(scorecard.CommandCount)
                .Append("|coreFacts=").Append(scorecard.CoreFactCount)
                .Append("|activeEffects=").Append(scorecard.ActiveEffectSlotCount)
                .Append("|ownerLocalFacts=").Append(scorecard.OwnerLocalFactCount)
                .Append("|pendingAttributeDeltas=").Append(scorecard.PendingAttributeDeltaCount)
                .Append("|dominantRisk=").Append(scorecard.DominantRisk)
                .Append("|metricFamilyMask=0x")
                .Append(((int)scorecard.MetricFamilyMask).ToString("X", CultureInfo.InvariantCulture))
                .Append("|timingAvailable=").Append(scorecard.PerformanceTimingAvailable)
                .Append("|profilerEvidence=").Append(scorecard.ProfilerEvidencePassed)
                .Append("|averageTickMs=").Append(scorecard.MeasuredAverageTickMilliseconds.ToString("G9", CultureInfo.InvariantCulture))
                .Append("|gasTickAverageMs=").Append(scorecard.GasTickAverageMilliseconds.ToString("G9", CultureInfo.InvariantCulture))
                .AppendLine();
            return builder.ToString();
        }
    }
}
