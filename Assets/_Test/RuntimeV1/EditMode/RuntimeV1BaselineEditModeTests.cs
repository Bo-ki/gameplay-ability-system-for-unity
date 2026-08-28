using System.Linq;
using NUnit.Framework;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Runtime v1 Tier B 最低向量清单的完整编号、owner 与当前实现状态。
    /// </summary>
    [TestFixture]
    public class RuntimeV1ImplementationManifestTests
    {
        private static readonly string[] ExpectedOwners =
        {
            "V3", "V3", "V3", "V3",
            "V3 + V4", "V3 + V4", "V3 + V4", "V3",
            "V4", "V4 + V5", "V4 + V5", "V1 + V3 + V6",
            "V3 + V6", "V3 + V6", "V2 + V3", "V5 + V6", "V7",
        };

        /// <summary>
        /// 验证清单编号唯一、连续且完整覆盖 1 到 17。
        /// </summary>
        [Test]
        public void TierB清单_编号唯一且覆盖一到十七()
        {
            var entries = TierBVectorManifest.Entries;
            var numbers = entries.Select(entry => entry.Number).ToArray();

            Assert.That(entries.Count, Is.EqualTo(17));
            Assert.That(numbers.Distinct().Count(), Is.EqualTo(17));
            Assert.That(numbers, Is.EqualTo(Enumerable.Range(1, 17).ToArray()));
        }

        /// <summary>
        /// 验证每个最低向量均映射到任务树冻结的实施 owner。
        /// </summary>
        [Test]
        public void TierB清单_Owner映射符合冻结任务树()
        {
            var entries = TierBVectorManifest.Entries;
            Assert.That(entries.Count, Is.EqualTo(ExpectedOwners.Length));

            for (var index = 0; index < entries.Count; index++)
                Assert.That(entries[index].Owner, Is.EqualTo(ExpectedOwners[index]));
        }

        /// <summary>
        /// 验证 V0 只登记目标向量，尚未实现的 Tier B 语义不得误报为 green。
        /// </summary>
        [Test]
        public void TierB清单_V0阶段全部保持Red()
        {
            Assert.That(
                TierBVectorManifest.Entries.All(entry => entry.Status == TierBVectorStatus.Red),
                Is.True);
        }
    }
}
