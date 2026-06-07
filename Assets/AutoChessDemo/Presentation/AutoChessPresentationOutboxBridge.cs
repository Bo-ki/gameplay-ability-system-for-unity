using System;
using System.Globalization;
using System.Text;

namespace GAS.AutoChessDemo
{
    public readonly struct AutoChessPresentationSnapshot
    {
        public readonly string Status;
        public readonly AutoChessBattleLogLine[] Lines;
        public readonly int SourceLineCount;
        public readonly int DisplayLineCount;
        public readonly int DroppedLineCount;
        public readonly int RuntimeMarkerCount;
        public readonly string DisabledReason;

        public AutoChessPresentationSnapshot(
            string status,
            AutoChessBattleLogLine[] lines,
            int sourceLineCount,
            int displayLineCount,
            int droppedLineCount,
            int runtimeMarkerCount,
            string disabledReason)
        {
            Status = status ?? string.Empty;
            Lines = lines ?? Array.Empty<AutoChessBattleLogLine>();
            SourceLineCount = sourceLineCount;
            DisplayLineCount = displayLineCount;
            DroppedLineCount = droppedLineCount;
            RuntimeMarkerCount = runtimeMarkerCount;
            DisabledReason = disabledReason ?? string.Empty;
        }

        public string ToText()
        {
            var builder = new StringBuilder(Math.Max(128, Lines.Length * 64));
            for (var i = 0; i < Lines.Length; i++)
                builder.AppendLine(Lines[i].ToString());
            return builder.ToString();
        }
    }

    public readonly struct AutoChessBattlePresentationSource
    {
        public readonly string RoomId;
        public readonly AutoChessTeam Winner;
        public readonly int BattleTicks;
        public readonly int DriverIssuedCommands;
        public readonly int RuntimeMarkerCount;
        public readonly string DisabledReason;
        public readonly AutoChessBattleLogSnapshot BattleLog;

        public AutoChessBattlePresentationSource(
            string roomId,
            AutoChessTeam winner,
            int battleTicks,
            int driverIssuedCommands,
            int runtimeMarkerCount,
            string disabledReason,
            AutoChessBattleLogSnapshot battleLog)
        {
            RoomId = roomId ?? string.Empty;
            Winner = winner;
            BattleTicks = battleTicks;
            DriverIssuedCommands = driverIssuedCommands;
            RuntimeMarkerCount = runtimeMarkerCount;
            DisabledReason = disabledReason ?? string.Empty;
            BattleLog = battleLog;
        }

        public static AutoChessBattlePresentationSource FromResult(
            in AutoChessBattleResult result)
        {
            var counters = result.RuntimeDiagnostics.CoreCounters;
            var backbone = result.RuntimeDiagnostics.FrameBackboneCounters;
            var disabledReason = backbone.RenderDisabledReasonCount > 0
                ? "reported-by-runtime"
                : "headless-log-bridge";
            return new AutoChessBattlePresentationSource(
                result.RoomId,
                result.Winner,
                result.BattleTicks,
                result.DriverIssuedCommands,
                counters.PresentationCount,
                disabledReason,
                result.BattleLog);
        }
    }

    public interface IAutoChessPresentationOutboxBridge
    {
        AutoChessPresentationSnapshot CreateSnapshot(
            in AutoChessBattlePresentationSource source,
            int maxLines);
    }

    public sealed class AutoChessLogPresentationOutboxBridge : IAutoChessPresentationOutboxBridge
    {
        public static readonly AutoChessLogPresentationOutboxBridge Instance =
            new AutoChessLogPresentationOutboxBridge();

        private AutoChessLogPresentationOutboxBridge()
        {
        }

        public AutoChessPresentationSnapshot CreateSnapshot(
            in AutoChessBattlePresentationSource source,
            int maxLines)
        {
            var sourceLines = source.BattleLog.Lines ?? Array.Empty<AutoChessBattleLogLine>();
            maxLines = Math.Max(1, maxLines);
            var start = Math.Max(0, sourceLines.Length - maxLines);
            var displayCount = sourceLines.Length - start;
            var displayLines = new AutoChessBattleLogLine[displayCount];
            for (var i = 0; i < displayCount; i++)
                displayLines[i] = sourceLines[start + i];

            var status = "房间 " + source.RoomId
                         + " | 胜者 " + source.Winner
                         + " | 帧 " + source.BattleTicks.ToString(CultureInfo.InvariantCulture)
                         + " | 命令 " + source.DriverIssuedCommands.ToString(CultureInfo.InvariantCulture)
                         + " | marker " + source.RuntimeMarkerCount.ToString(CultureInfo.InvariantCulture);

            return new AutoChessPresentationSnapshot(
                status,
                displayLines,
                sourceLines.Length,
                displayCount,
                start,
                source.RuntimeMarkerCount,
                source.DisabledReason);
        }
    }
}
