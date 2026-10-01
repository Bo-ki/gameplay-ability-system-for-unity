using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Runtime v1 Tier B 机器清单的稳定编号与真实测试源码引用。
    /// </summary>
    [TestFixture]
    public sealed class RuntimeV1ImplementationManifestTests
    {
        /// <summary>
        /// 验证 VectorId 唯一、连续且完整覆盖 TB-01 到 TB-17。
        /// </summary>
        [Test]
        public void TierB清单_VectorId唯一且连续()
        {
            var document = TierBVectorManifest.Load();
            var observed = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < document.Vectors.Length; index++)
            {
                var expectedVectorId = "TB-" + (index + 1).ToString("D2");
                Assert.That(document.Vectors[index].VectorId, Is.EqualTo(expectedVectorId));
                Assert.That(observed.Add(document.Vectors[index].VectorId), Is.True, expectedVectorId);
                Assert.That(document.Vectors[index].Version, Is.GreaterThan(0), expectedVectorId);
                Assert.That(document.Vectors[index].Description, Is.Not.Null.And.Not.Empty, expectedVectorId);
            }
        }

        /// <summary>
        /// 验证每条已登记证据都指向项目内真实源码，并与机器清单固定的 SHA-256 一致。
        /// </summary>
        [Test]
        public void TierB清单_测试来源与源码指纹有效()
        {
            var document = TierBVectorManifest.Load();
            foreach (var entry in document.Vectors)
            {
                var observedTestIds = new HashSet<string>(StringComparer.Ordinal);
                var tests = entry.Tests ?? new TierBVectorTestEvidence[0];
                foreach (var test in tests)
                {
                    AssertTestMetadata(entry.VectorId, test);
                    Assert.That(observedTestIds.Add(test.TestId), Is.True,
                        entry.VectorId + " 重复 TestId：" + test.TestId);
                }
            }
        }

        /// <summary>
        /// 验证单条测试引用的平台、结果、发现数量、路径与源码 hash。
        /// </summary>
        private static void AssertTestMetadata(string vectorId, TierBVectorTestEvidence test)
        {
            Assert.That(test.TestAssembly, Is.Not.Null.And.Not.Empty, vectorId);
            Assert.That(test.TestPlatform, Is.EqualTo("EditMode").Or.EqualTo("PlayMode"), vectorId);
            Assert.That(test.TestSourcePath, Is.Not.Null.And.Not.Empty, vectorId);
            Assert.That(test.TestSourceHash, Does.Match("^[0-9a-f]{64}$"), vectorId);
            Assert.That(test.TestId, Is.Not.Null.And.Not.Empty, vectorId);
            Assert.That(test.TestId, Does.Contain("."), vectorId);
            Assert.That(test.ExpectedCaseCount, Is.GreaterThan(0), vectorId + ":" + test.TestId);
            Assert.That(test.ExpectedResult, Is.EqualTo("Passed").Or.EqualTo("Failed"),
                vectorId + ":" + test.TestId);

            var sourcePath = TierBVectorManifest.ResolveProjectPath(test.TestSourcePath);
            Assert.That(File.Exists(sourcePath), Is.True, sourcePath);
            Assert.That(ComputeSha256(sourcePath), Is.EqualTo(test.TestSourceHash), sourcePath);
        }

        /// <summary>
        /// 计算测试源码原始字节的 SHA-256，避免换行或编码规范化掩盖证据漂移。
        /// </summary>
        private static string ComputeSha256(string sourcePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(sourcePath))
            {
                return BitConverter.ToString(sha256.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}
