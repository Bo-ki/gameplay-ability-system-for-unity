using System;
using NUnit.Framework;

namespace GAS.AutoChessDemo.Tests.EditMode
{
    /// <summary>
    /// 通过公开 AutoChess 默认战斗入口约束 Luban 伤害配置贯通到公开 BattleReport 的业务事实。
    /// </summary>
    [TestFixture]
    public sealed class AutoChessLubanDamageRegressionTests
    {
        private const int MaximumBattleTicks = 4;
        private const float ExpectedLubanPlayerAttackDamage = 12f;

        /// <summary>
        /// 验证首个玩家攻击 DamageApplied 事实严格采用 Luban 配置值 12，而不是旧手工 Catalog 值 44。
        /// </summary>
        [Test]
        public void RunDefault_FirstPlayerAttackDamage_EqualsLubanAuthoredValue()
        {
            try
            {
                var options = new AutoChessBattleOptions(
                    maxTicks: MaximumBattleTicks,
                    captureOfficialToolDiff: false,
                    debuggerEnabled: true,
                    captureSystemTimings: false,
                    captureBufferPressure: false);
                var result = AutoChessBattleManager.RunDefault(options);
                var battleEvents = result.BattleReport.Events
                                   ?? Array.Empty<AutoChessBattleReportEvent>();
                var found = false;
                var firstPlayerAttackDamage = default(AutoChessBattleReportEvent);

                // 只读取公开报告事实；Luban 审定值由测试独立冻结，避免从 Catalog 或生成物自证期望值。
                for (var eventIndex = 0; eventIndex < battleEvents.Length; eventIndex++)
                {
                    var battleEvent = battleEvents[eventIndex];
                    if (battleEvent.Kind != AutoChessBattleReportEventKind.DamageApplied
                        || battleEvent.GameplayEffectCode
                        != AutoChessBattleRules.GameplayEffectPlayerAttackDamage)
                    {
                        continue;
                    }

                    firstPlayerAttackDamage = battleEvent;
                    found = true;
                    break;
                }

                Assert.That(
                    found,
                    Is.True,
                    $"前 {MaximumBattleTicks} 个战斗 tick 内未观测到玩家攻击伤害事实。");
                Assert.That(
                    firstPlayerAttackDamage.Value,
                    Is.EqualTo(ExpectedLubanPlayerAttackDamage),
                    "首个玩家攻击伤害必须来自 Luban 主表，旧手工 Catalog 的 44 不得继续生效。");
            }
            finally
            {
                AutoChessBattleManager.ShutdownRuntime();
            }
        }
    }
}
