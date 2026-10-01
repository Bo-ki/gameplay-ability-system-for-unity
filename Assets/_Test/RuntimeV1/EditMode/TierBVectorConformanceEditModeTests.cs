using System.Linq;
using NUnit.Framework;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Tier B 机器清单的 schema 与状态元数据契约，不把声明状态当作语义测试结果。
    /// </summary>
    [TestFixture]
    public sealed class TierBVectorManifestContractEditModeTests
    {
        /// <summary>
        /// 验证执行子清单使用当前 schema、固定 17 个 required Tier B，并拒绝冒充 master manifest。
        /// </summary>
        [Test]
        public void Manifest_Schema与Required数量有效()
        {
            var document = TierBVectorManifest.Load();

            Assert.That(document.SchemaVersion, Is.EqualTo(TierBVectorManifest.CurrentSchemaVersion));
            Assert.That(document.ManifestId, Is.EqualTo("runtime-v1-tier-b"));
            Assert.That(document.ManifestVersion, Is.GreaterThan(0));
            Assert.That(document.ManifestScope, Is.EqualTo("TierBExecutionSubManifest"));
            Assert.That(document.SemanticOwner, Is.Not.Null.And.Not.Empty);
            Assert.That(document.CanAuthorizeV0Exit, Is.False);
            Assert.That(document.RequiredMasterScopes, Is.EquivalentTo(new[]
            {
                "TierA-LegacyCharacterization",
                "TierB-TargetSemanticConformance",
                "R3-ThirdRoundFamilies",
            }));
            Assert.That(document.Vectors, Is.Not.Null);
            Assert.That(document.Vectors.Length, Is.EqualTo(17));
            Assert.That(document.Vectors.All(entry => entry.RequiredPass), Is.True);
        }

        /// <summary>
        /// 验证状态只允许 Green、ApprovedRed、Pending，并执行各状态的治理字段约束。
        /// </summary>
        [Test]
        public void Manifest_状态与批准元数据满足契约()
        {
            var document = TierBVectorManifest.Load();
            foreach (var entry in document.Vectors)
            {
                Assert.That(entry.Status,
                    Is.EqualTo("Green").Or.EqualTo("ApprovedRed").Or.EqualTo("Pending"), entry.VectorId);
                Assert.That(entry.StatusReason, Is.Not.Null.And.Not.Empty, entry.VectorId);
                Assert.That(entry.OwnerTask, Is.Not.Null.And.Not.Empty, entry.VectorId);
                Assert.That(entry.NextOwnerTask, Is.Not.Null.And.Not.Empty, entry.VectorId);
                Assert.That(entry.Approval, Is.Not.Null, entry.VectorId);
                AssertStatusContract(entry);
            }
        }

        /// <summary>
        /// 按声明状态验证测试结果和批准字段；这里只验证数据完整性，不执行语义裁决。
        /// </summary>
        private static void AssertStatusContract(TierBVectorManifestEntry entry)
        {
            var tests = entry.Tests ?? new TierBVectorTestEvidence[0];
            if (entry.Status == "Green")
            {
                Assert.That(tests.Length, Is.GreaterThan(0), entry.VectorId);
                Assert.That(tests.All(test => test.ExpectedResult == "Passed"), Is.True, entry.VectorId);
                Assert.That(entry.LastRunEvidenceId, Is.Not.Null.And.Not.Empty, entry.VectorId);
                return;
            }

            if (entry.Status == "Pending")
            {
                Assert.That(entry.ReviewRequired, Is.True, entry.VectorId);
                return;
            }

            Assert.That(tests.Length, Is.GreaterThan(0), entry.VectorId);
            Assert.That(tests.Any(test => test.ExpectedResult == "Failed"), Is.True, entry.VectorId);
            Assert.That(entry.Approval.ApprovedBy, Is.Not.Null.And.Not.Empty, entry.VectorId);
            Assert.That(entry.Approval.ApprovedAtUtc, Is.Not.Null.And.Not.Empty, entry.VectorId);
            Assert.That(entry.Approval.ApprovalRecordId, Is.Not.Null.And.Not.Empty, entry.VectorId);
            Assert.That(entry.Approval.ApprovalRecordHash, Is.Not.Null.And.Not.Empty, entry.VectorId);
            Assert.That(entry.Approval.ExpectedFailureSignature, Is.Not.Null.And.Not.Empty, entry.VectorId);
        }
    }
}
