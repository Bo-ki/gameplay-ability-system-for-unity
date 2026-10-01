using System;
using System.Collections.Generic;
using GAS.Editor.CodeGen.Proofs;
using GAS.Runtime;
using NUnit.Framework;

namespace GAS.Editor.Tests.CodeGen.Proofs
{
    /// <summary>
    /// 验证 Runtime v1 CapacityProof 的隐藏上界、consumer Red、checked provenance 与目标规模字节证据。
    /// </summary>
    [TestFixture]
    public sealed class GasRuntimeV1CapacityProofTests
    {
        /// <summary>
        /// 验证当前 Runtime v1 的缺失/仅声明/仅预留 consumer 会稳定阻止 proof Green。
        /// </summary>
        [Test]
        public void 当前Runtime消费者不完整_应输出稳定Red而非假Green()
        {
            var source = new TestCapacityProofSource();
            var layout = ProofTestData.BuildLayout();
            var first = GasRuntimeV1CapacityProofBuilder.Build(source, layout);
            var second = GasRuntimeV1CapacityProofBuilder.Build(new TestCapacityProofSource(),
                ProofTestData.BuildLayout());

            Assert.That(first.Succeeded, Is.False);
            Assert.That(first.ProofHash, Is.EqualTo(second.ProofHash));
            Assert.That(first.ProofHash.Length, Is.EqualTo(64));
            Assert.That(GasRuntimeV1CapacityProofBuilder.TryVerifyPayload(first, out _),
                Is.True);
            Assert.That(FindConsumer(first, "TargetWorkUnits").Status,
                Is.EqualTo(GasProofConsumerStatus.Missing));
            Assert.That(FindConsumer(first, "OwnerReservations").Status,
                Is.EqualTo(GasProofConsumerStatus.DeclaredOnly));
            Assert.That(FindConsumer(first, "LiveDependencies").Status,
                Is.EqualTo(GasProofConsumerStatus.PreallocatedOnly));
            Assert.That(FindConsumer(first, GasCapacityDimension.ExpansionTargets.ToString()).Status,
                Is.EqualTo(GasProofConsumerStatus.RuntimeEquivalentCheck));
            Assert.That(ContainsFailure(first, GasProofFailureKind.RuntimeConsumerMissing,
                "TargetWorkUnits"), Is.True);
            Assert.That(ContainsFailure(first, GasProofFailureKind.RuntimeConsumerMissing,
                GasCapacityDimension.ExpansionTargets.ToString()), Is.True);
            Assert.That(first.CoverageGaps, Is.EqualTo(
                GasCapacityProofCoverageGap.CanonicalGraphAdapterMissing |
                GasCapacityProofCoverageGap.SemanticDimensionModelIncomplete |
                GasCapacityProofCoverageGap.RuntimeRouteFormulaIncomplete |
                GasCapacityProofCoverageGap.RuntimeFactFormulaIncomplete |
                GasCapacityProofCoverageGap.AuthorityConsumerMissing |
                GasCapacityProofCoverageGap.ProvenanceBindingIncomplete));
            Assert.That(source.TryGetDefinitionIdentity(0, out var identity,
                out var identityProvenance), Is.True);
            Assert.That(identityProvenance.RelatedDefinitionKeys,
                Does.Contain(identity.CanonicalKey));
            Assert.That(ContainsFailure(first,
                GasProofFailureKind.ProofCoverageIncomplete,
                "CapacityModelCoverage"), Is.True);
        }

        /// <summary>
        /// 验证缺少任一显式零/正 bound 都以 CFG1401 和 dimension provenance 失败。
        /// </summary>
        [Test]
        public void 缺失语义维度_应输出DimensionMissing()
        {
            var source = new TestCapacityProofSource();
            source.RemoveBound(GasCapacityDimension.TargetPublishDeltaBytes);

            var payload = GasRuntimeV1CapacityProofBuilder.Build(source, ProofTestData.BuildLayout());
            var failure = FindFailure(payload, GasProofFailureKind.CapacityDimensionMissing,
                GasCapacityDimension.TargetPublishDeltaBytes.ToString());

            Assert.That(failure.RuleId, Is.EqualTo(GasProofRuleIds.CapacityProofMissing));
            Assert.That(failure.DefinitionId, Is.EqualTo(9001));
            Assert.That(failure.CanonicalOrdinal, Is.EqualTo(0));
            Assert.That(failure.Provenance.FieldPath, Is.EqualTo("Definitions[9001]"));
        }

        /// <summary>
        /// 验证 Direct Program 第 65,536 个节点越过 ApplicationId 低 16 位编码上界。
        /// </summary>
        [Test]
        public void DirectProgram节点超过六万五千五百三十五_应显式失败()
        {
            var source = new TestCapacityProofSource();
            source.SetBound(GasCapacityDimension.ExpansionProgramNodes,
                GasRuntimeV1ProofInventory.MaximumDirectProgramNodeCount + 1L);

            var payload = GasRuntimeV1CapacityProofBuilder.Build(source, ProofTestData.BuildLayout());
            var failure = FindFailure(payload,
                GasProofFailureKind.RuntimeEncodingLimitExceeded,
                GasCapacityDimension.ExpansionProgramNodes.ToString());

            Assert.That(failure.ExpectedMaximum,
                Is.EqualTo(GasRuntimeV1ProofInventory.MaximumDirectProgramNodeCount));
            Assert.That(failure.ActualValue,
                Is.EqualTo(GasRuntimeV1ProofInventory.MaximumDirectProgramNodeCount + 1L));
            Assert.That(failure.Provenance.FieldPath,
                Is.EqualTo("Definitions[9001].ExpansionProgramNodes"));
        }

        /// <summary>
        /// 验证 requirement 四阶段合计使用 checked add 并保留溢出的左右操作数。
        /// </summary>
        [Test]
        public void Requirement阶段合计溢出_应保留CheckedDerivation()
        {
            var source = new TestCapacityProofSource();
            source.SetBound(GasCapacityDimension.RequirementDescriptors, long.MaxValue);
            source.SetBound(GasCapacityDimension.ApplicationRequirementDescriptors, long.MaxValue);
            source.SetBound(GasCapacityDimension.OngoingRequirementDescriptors, 1);

            var payload = GasRuntimeV1CapacityProofBuilder.Build(source, ProofTestData.BuildLayout());
            var failure = FindFailure(payload, GasProofFailureKind.ArithmeticOverflow,
                "RequirementDescriptors");

            Assert.That(failure.DerivationId, Is.EqualTo("requirements.application+ongoing"));
            Assert.That(failure.Operator, Is.EqualTo("+"));
            Assert.That(failure.LeftOperandValue, Is.EqualTo(long.MaxValue));
            Assert.That(failure.RightOperandValue, Is.EqualTo(1));
            Assert.That(failure.Provenance.FieldPath,
                Is.EqualTo("Definitions[9001].RequirementDescriptors"));
        }

        /// <summary>
        /// 验证 target×stride 元素虽可用 long 表示，NativeArray int length 与 ABI bytes 仍分别失败。
        /// </summary>
        [Test]
        public void TargetPlane极值_应区分IntLength与ByteOverflow()
        {
            var source = new TestCapacityProofSource();
            var profile = source.ScaleProfile;
            profile.MaxAscRegistryCount = int.MaxValue;
            profile.MaxActiveEffectCount = int.MaxValue;
            source.ScaleProfile = profile;

            var payload = GasRuntimeV1CapacityProofBuilder.Build(source, ProofTestData.BuildLayout());
            var lengthFailure = FindFailure(payload,
                GasProofFailureKind.RuntimeEncodingLimitExceeded,
                "Scratch.TargetActiveEffects");
            var byteFailure = FindFailure(payload,
                GasProofFailureKind.ArithmeticOverflow,
                "Scratch.TargetActiveEffects");

            Assert.That(lengthFailure.ExpectedMaximum, Is.EqualTo(int.MaxValue));
            Assert.That(lengthFailure.ActualValue,
                Is.EqualTo((long)int.MaxValue * int.MaxValue));
            Assert.That(byteFailure.Operator, Is.EqualTo("*"));
            Assert.That(byteFailure.LeftOperandName, Is.EqualTo("ElementCount"));
            Assert.That(byteFailure.RightOperandName, Is.EqualTo("ElementSizeBytes"));
        }

        /// <summary>
        /// 验证 PlanExpand envelope 分别使用 target maximum 与 planned/direct-output maximum，不再误用 node count。
        /// </summary>
        [Test]
        public void RuntimePlanExpand公式_应分离Target与Application乘数()
        {
            var source = new TestCapacityProofSource();
            source.SetBound(GasCapacityDimension.ExpansionProgramNodes, 1);
            source.SetBound(GasCapacityDimension.ExpansionTargets, 3);
            source.SetBound(GasCapacityDimension.ExpansionApplications, 5);
            source.SetBound(GasCapacityDimension.DirectProgramOutputs, 7);

            var payload = GasRuntimeV1CapacityProofBuilder.Build(
                source, ProofTestData.BuildLayout());
            var targets = FindLimit(payload, "ResolvedTargets");
            var applications = FindLimit(payload, "EffectOperations");

            Assert.That(targets.Required,
                Is.EqualTo(source.ScaleProfile.MaxBoundaryCommandCount * 3L));
            Assert.That(applications.Required,
                Is.EqualTo(source.ScaleProfile.MaxBoundaryCommandCount * 7L));
            Assert.That(applications.Formula, Does.Contain("directProgramOutputs"));
        }

        /// <summary>
        /// 验证 Route/Facts 当前公式仅作为下界证据，不冒充 Runtime 完整容量门。
        /// </summary>
        [Test]
        public void Route与Facts公式不完整_应显式标记为下界证据()
        {
            var source = new TestCapacityProofSource();
            source.SetBound(GasCapacityDimension.ExpansionDynamicNextTickWork, 2);
            source.SetBound(GasCapacityDimension.ObservationFacts, 3);

            var payload = BuildCapacity(source);
            var routes = FindLimit(payload, "NextTickRoutesLowerBound");
            var facts = FindLimit(payload, "CoreFactsLowerBound");

            Assert.That(routes.ConsumerStatus, Is.EqualTo(GasProofConsumerStatus.EvidenceOnly));
            Assert.That(facts.ConsumerStatus, Is.EqualTo(GasProofConsumerStatus.EvidenceOnly));
            Assert.That(routes.Available, Is.EqualTo(-1));
            Assert.That(facts.Available, Is.EqualTo(-1));
            Assert.That(routes.Formula, Does.Contain("incomplete lower-bound"));
            Assert.That(facts.Formula, Does.Contain("incomplete lower-bound"));
        }

        /// <summary>
        /// 验证 Layout 输入与 Capacity payload 构建后篡改都以 CFG1501 fail-closed。
        /// </summary>
        [Test]
        public void ProofPayload篡改_应被完整性校验拒绝()
        {
            var layout = ProofTestData.BuildLayout();
            layout.Tags[0].TagId = -1;
            var rejected = GasRuntimeV1CapacityProofBuilder.Build(
                new TestCapacityProofSource(), layout);
            var payload = GasRuntimeV1CapacityProofBuilder.Build(
                new TestCapacityProofSource(), ProofTestData.BuildLayout());
            payload.ContractMatrixHash = new string('c', 64);

            var verified = GasRuntimeV1CapacityProofBuilder.TryVerifyPayload(
                payload, out var failure);

            Assert.That(ContainsFailure(rejected,
                GasProofFailureKind.ProofHashMismatch, "LayoutProof"), Is.True);
            Assert.That(verified, Is.False);
            Assert.That(failure.RuleId, Is.EqualTo(GasProofRuleIds.NonCanonicalIdentity));
            Assert.That(failure.ExpectedText, Has.Length.EqualTo(64));
            Assert.That(failure.ActualText, Has.Length.EqualTo(64));
        }

        /// <summary>
        /// 验证 identity/bound 数组形状篡改在进入 hasher 前直接 fail-closed。
        /// </summary>
        [Test]
        public void DefinitionIdentity数组形状篡改_应在Hash前拒绝()
        {
            var payload = BuildCapacity(new TestCapacityProofSource());
            payload.DefinitionIdentities =
                Array.Empty<GasCapacityDefinitionIdentityProofEntry>();

            var verified = GasRuntimeV1CapacityProofBuilder.TryVerifyPayload(
                payload, out var failure);

            Assert.That(verified, Is.False);
            Assert.That(failure.DimensionId, Is.EqualTo("CapacityPayload"));
            Assert.That(failure.FailureKind,
                Is.EqualTo(GasProofFailureKind.ProofHashMismatch));
        }

        /// <summary>
        /// 验证两类 provenance 作为一等 payload/hash 输入，单字段篡改不借助 Failures 变化。
        /// </summary>
        [TestCase("DefinitionIdentityProvenance")]
        [TestCase("MemoryBudgetProvenance")]
        public void ProvenanceOnly篡改_应由CapacityProofHash拒绝(string fieldName)
        {
            var payload = BuildCapacity(new TestCapacityProofSource());
            var originalFailures = payload.Failures;
            Assert.That(GasRuntimeV1CapacityProofBuilder.TryVerifyPayload(
                payload, out _), Is.True);
            if (fieldName == "DefinitionIdentityProvenance")
            {
                var identity = payload.DefinitionIdentities[0];
                identity.Provenance = ProofTestData.Provenance(
                    1900, identity.DefinitionId, "Definitions[9001].Tampered");
                payload.DefinitionIdentities[0] = identity;
            }
            else
            {
                payload.MemoryBudgetProvenance = ProofTestData.Provenance(
                    1901, 9001, "ScaleProfiles[1].MemoryBudgetBytes.Tampered");
            }

            var verified = GasRuntimeV1CapacityProofBuilder.TryVerifyPayload(
                payload, out var failure);

            Assert.That(payload.Failures, Is.SameAs(originalFailures));
            Assert.That(verified, Is.False);
            Assert.That(failure.DimensionId, Is.EqualTo("CapacityProofHash"));
        }

        /// <summary>
        /// 验证 coverage flags 直接进入 payload hash，不能通过清零伪造 Complete。
        /// </summary>
        [Test]
        public void CoverageGaps篡改为None_应FailClosed()
        {
            var payload = BuildCapacity(new TestCapacityProofSource());
            Assert.That(GasRuntimeV1CapacityProofBuilder.TryVerifyPayload(
                payload, out _), Is.True);
            payload.CoverageGaps = GasCapacityProofCoverageGap.None;

            var verified = GasRuntimeV1CapacityProofBuilder.TryVerifyPayload(
                payload, out var failure);

            Assert.That(verified, Is.False);
            Assert.That(failure.DimensionId, Is.EqualTo("CapacityProofHash"));
        }

        /// <summary>
        /// 验证 Capacity 顶层超长 identity 字符串在 UTF-8 分配前被 verifier 拒绝。
        /// </summary>
        [Test]
        public void 超长TargetScaleId篡改_应在UTF8分配前FailClosed()
        {
            var payload = BuildCapacity(new TestCapacityProofSource());
            Assert.That(GasRuntimeV1CapacityProofBuilder.TryVerifyPayload(
                payload, out _), Is.True);
            payload.TargetScaleId = new string('x',
                GasRuntimeV1ProofInventory.MaximumProofCanonicalStringBytes + 1);
            payload.ProofHash = string.Empty;

            var verified = GasRuntimeV1CapacityProofBuilder.TryVerifyPayload(
                payload, out var failure);

            Assert.That(verified, Is.False);
            Assert.That(failure.DimensionId, Is.EqualTo("CapacityPayload"));
        }

        /// <summary>
        /// 验证 Capacity ProofHash 强绑 contract、profile、semantic bound 与 provenance，hash 文本大小写归一。
        /// </summary>
        [Test]
        public void CapacityProofHash_应绑定所有证明输入()
        {
            var baseline = BuildCapacity(new TestCapacityProofSource());
            var uppercaseSource = new TestCapacityProofSource
            {
                CanonicalGraphHash = new string('A', 64),
                ContractMatrixHash = new string('B', 64),
            };
            var uppercaseLayoutSource = TestLayoutProofSource.CreateValid();
            uppercaseLayoutSource.CanonicalGraphHash = new string('A', 64);
            var uppercase = GasRuntimeV1CapacityProofBuilder.Build(uppercaseSource,
                GasRuntimeV1LayoutProofBuilder.Build(uppercaseLayoutSource));
            var contractSource = new TestCapacityProofSource
            {
                ContractMatrixHash = new string('c', 64),
            };
            var profileSource = new TestCapacityProofSource();
            var profile = profileSource.ScaleProfile;
            profile.MaxCoreFactCount++;
            profileSource.ScaleProfile = profile;
            var boundSource = new TestCapacityProofSource();
            boundSource.SetBound(GasCapacityDimension.ObservationFacts, 1);
            var provenanceSource = new TestCapacityProofSource
            {
                ScaleProfileProvenance = ProofTestData.Provenance(
                    1800, 9001, "ScaleProfiles[1].Changed"),
            };
            var identityProvenanceSource = new TestCapacityProofSource
            {
                DefinitionIdentityProvenance = ProofTestData.Provenance(
                    1801, 9001, "Definitions[9001].Changed"),
            };
            var budgetProvenanceSource = new TestCapacityProofSource
            {
                MemoryBudgetProvenance = ProofTestData.Provenance(
                    1802, 9001, "ScaleProfiles[1].MemoryBudgetBytes.Changed"),
            };

            Assert.That(uppercase.ProofHash, Is.EqualTo(baseline.ProofHash));
            Assert.That(BuildCapacity(contractSource).ProofHash, Is.Not.EqualTo(baseline.ProofHash));
            Assert.That(BuildCapacity(profileSource).ProofHash, Is.Not.EqualTo(baseline.ProofHash));
            Assert.That(BuildCapacity(boundSource).ProofHash, Is.Not.EqualTo(baseline.ProofHash));
            Assert.That(BuildCapacity(provenanceSource).ProofHash, Is.Not.EqualTo(baseline.ProofHash));
            Assert.That(BuildCapacity(identityProvenanceSource).ProofHash,
                Is.Not.EqualTo(baseline.ProofHash));
            Assert.That(BuildCapacity(budgetProvenanceSource).ProofHash,
                Is.Not.EqualTo(baseline.ProofHash));
        }

        /// <summary>
        /// 验证 Red 矩阵中 fan-out、Live 投影与 dependency 回边均有精确失败路径。
        /// </summary>
        [TestCase(GasCapacityDimension.DirectProgramMaximumNodeFanOut, 2,
            GasProofFailureKind.UnsupportedRuntimeCombination,
            GasProofRuleIds.UnsupportedExecutionProjection)]
        [TestCase(GasCapacityDimension.LiveCaptureDescriptors, 1,
            GasProofFailureKind.ProjectionUnbounded,
            GasProofRuleIds.ProjectionUnbounded)]
        [TestCase(GasCapacityDimension.DynamicDependencyBackEdges, 1,
            GasProofFailureKind.DependencyOrCleanupUnbounded,
            GasProofRuleIds.DependencyOrCleanupUnbounded)]
        public void Red矩阵语义分支_应输出稳定RuleId(
            GasCapacityDimension dimension,
            long maximum,
            GasProofFailureKind expectedKind,
            string expectedRuleId)
        {
            var source = new TestCapacityProofSource();
            source.SetBound(dimension, maximum);

            var payload = BuildCapacity(source);
            var failure = FindFailure(payload, expectedKind, dimension.ToString());

            Assert.That(failure.RuleId, Is.EqualTo(expectedRuleId));
            Assert.That(failure.ActualValue, Is.EqualTo(maximum));
            Assert.That(failure.Provenance.FieldPath,
                Is.EqualTo("Definitions[9001]." + dimension));
        }

        /// <summary>
        /// 验证 TargetDataFields 编码失败指向 Runtime 真实 enum 名称。
        /// </summary>
        [Test]
        public void TargetData字段超过闭集_应报告GasTargetDataVariant()
        {
            var source = new TestCapacityProofSource();
            source.SetBound(GasCapacityDimension.TargetDataFields, 5);

            var failure = FindFailure(BuildCapacity(source),
                GasProofFailureKind.CapacityBoundInvalid,
                GasCapacityDimension.TargetDataFields.ToString());

            Assert.That(failure.FirstConsumer,
                Is.EqualTo("GasTargetDataVariant closed enum"));
        }

        /// <summary>
        /// 验证超大合法 int 与未越 count 门但越 byte 门的输入均在分配前稳定 Red。
        /// </summary>
        [TestCase(int.MaxValue, "DefinitionCount")]
        [TestCase(3000, "CapacityProofBuildBytes")]
        public void 超大DefinitionCount_应在分配前稳定失败(
            int definitionCount,
            string expectedDimension)
        {
            var firstSource = new TestCapacityProofSource
            {
                DefinitionCountOverride = definitionCount,
            };
            var secondSource = new TestCapacityProofSource
            {
                DefinitionCountOverride = definitionCount,
            };
            GasRuntimeV1CapacityProofPayload first = null;

            Assert.DoesNotThrow(() => first = BuildCapacity(firstSource));
            var second = BuildCapacity(secondSource);
            var failure = FindFailure(first,
                GasProofFailureKind.ProofBuildBudgetExceeded, expectedDimension);

            Assert.That(first.DefinitionIdentities, Is.Empty);
            Assert.That(first.Bounds, Is.Empty);
            Assert.That(first.ProofHash, Is.EqualTo(second.ProofHash));
            Assert.That(GasRuntimeV1CapacityProofBuilder.TryVerifyPayload(
                first, out _), Is.True);
            Assert.That(failure.ActualValue,
                Is.GreaterThan(failure.ExpectedMaximum));
        }

        /// <summary>
        /// 验证 source 的真实 UTF-8 bytes 与 related-key 累计超门后停止后续 bound getter。
        /// </summary>
        [TestCase("Utf8Bytes", 100, "CapacityProofCanonicalBytes")]
        [TestCase("RelatedKeys", 300, "CapacityProofRelatedDefinitionCount")]
        public void Source实际累计预算超门_应停止后续读取(
            string budgetKind,
            int definitionCount,
            string expectedDimension)
        {
            var source = new TestCapacityProofSource
            {
                DefinitionCountOverride = definitionCount,
            };
            var provenance = ProofTestData.Provenance(
                1803, 9001, "Definitions[9001].BudgetProbe");
            source.BoundProvenanceOverride = budgetKind == "Utf8Bytes"
                ? ProofTestData.WithRawValue(in provenance, new string('x',
                    GasRuntimeV1ProofInventory.MaximumProofCanonicalStringBytes))
                : CreateRelatedKeyBudgetProvenance();

            var payload = BuildCapacity(source);
            var failure = FindFailure(payload,
                GasProofFailureKind.ProofBuildBudgetExceeded, expectedDimension);
            var possibleBoundReads = definitionCount *
                                     (int)GasCapacityDimension.DynamicDependencyBackEdges;

            Assert.That(source.BoundReadCount, Is.LessThan(possibleBoundReads));
            Assert.That(payload.DefinitionIdentities, Is.Empty);
            Assert.That(payload.Bounds, Is.Empty);
            Assert.That(failure.ActualValue, Is.GreaterThan(failure.ExpectedMaximum));
            Assert.That(GasRuntimeV1CapacityProofBuilder.TryVerifyPayload(
                payload, out _), Is.True);
        }

        /// <summary>
        /// 验证缺少显式内存预算时保持 Unknown/Red，不因 payload 下界可计算而假 Green。
        /// </summary>
        [Test]
        public void 内存预算缺失_应输出BudgetMissing()
        {
            var source = new TestCapacityProofSource
            {
                DeclaredMemoryBudgetBytes = 0,
            };

            var failure = FindFailure(BuildCapacity(source),
                GasProofFailureKind.MemoryBudgetMissing, "MemoryBudgetBytes");

            Assert.That(failure.RuleId, Is.EqualTo(GasProofRuleIds.CapacityProofMissing));
            Assert.That(failure.Provenance.FieldPath,
                Is.EqualTo("ScaleProfiles[1].MemoryBudgetBytes"));
        }

        /// <summary>
        /// 验证 provenance 嵌套 key count 在 Clone/遍历前被硬门转为可验真的稳定 Red。
        /// </summary>
        [Test]
        public void RelatedDefinitionKeys超门_应拒绝Clone并输出ProvenanceRed()
        {
            var related = new string[
                GasRuntimeV1ProofInventory.MaximumProofRelatedDefinitionCount + 1];
            var source = new TestCapacityProofSource
            {
                MemoryBudgetProvenance = new GasProofProvenance(
                    801, "workbook://proof-tests", "ProofTable", "row-801",
                    "ScaleProfiles[1].MemoryBudgetBytes", "raw", "normalized",
                    GasProofRuleIds.CapacityProofMissing, "1", "proof-tests/1", related),
            };

            var payload = BuildCapacity(source);
            var failure = FindFailure(payload, GasProofFailureKind.ProvenanceMissing,
                "MemoryBudgetBytes");

            Assert.That(payload.MemoryBudgetProvenance.RelatedDefinitionKeys, Is.Null);
            Assert.That(failure.Provenance.RelatedDefinitionKeys, Is.Null);
            Assert.That(GasRuntimeV1CapacityProofBuilder.TryVerifyPayload(
                payload, out _), Is.True);

            var validSource = new TestCapacityProofSource
            {
                MemoryBudgetProvenance = new GasProofProvenance(
                    801, "workbook://proof-tests", "ProofTable", "row-801",
                    "ScaleProfiles[1].MemoryBudgetBytes", "raw", "normalized",
                    GasProofRuleIds.CapacityProofMissing, "1", "proof-tests/1",
                    Array.Empty<string>()),
            };
            var tampered = BuildCapacity(validSource);
            Assert.That(GasRuntimeV1CapacityProofBuilder.TryVerifyPayload(
                tampered, out _), Is.True);
            tampered.MemoryBudgetProvenance = payload.MemoryBudgetProvenance;

            Assert.That(GasRuntimeV1CapacityProofBuilder.TryVerifyPayload(
                tampered, out _), Is.False);
        }

        /// <summary>
        /// 验证基于 AutoChess 4×1000 公式的静态审计夹具，不绑定真实 canonical graph 且不实际分配。
        /// </summary>
        [Test]
        public void AutoChess千倍规模_应输出六千四百万Plane与超预算证据()
        {
            const int ascCount = 4000;
            var source = new TestCapacityProofSource
            {
                TargetScaleId = "fixture:autochess-replicated-groups-x1000",
                ScaleProfile = ProofTestData.CreateProfile(ascCount),
                DeclaredMemoryBudgetBytes = 128L * 1024L * 1024L * 1024L,
            };
            source.SetBound(GasCapacityDimension.ExpansionProgramNodes, 1);
            var payload = GasRuntimeV1CapacityProofBuilder.Build(source,
                ProofTestData.BuildLayout(257));
            var activePlane = FindMemory(payload, "Scratch.TargetActiveEffects");
            var payloadRangePlane = FindMemory(payload, "Scratch.TargetPayloadRanges");
            var payloadValuePlane = FindMemory(payload, "Scratch.TargetPayloadValues");
            var tagCountPlane = FindMemory(payload, "Scratch.TargetTagCounts");
            var tagWordPlane = FindMemory(payload, "Scratch.TargetTagPresenceWords");
            var budgetFailure = FindFailure(payload,
                GasProofFailureKind.MemoryBudgetExceeded, "MemoryBudgetBytes");

            Assert.That(activePlane.ElementCount, Is.EqualTo(64_000_000));
            Assert.That(payloadRangePlane.ElementCount, Is.EqualTo(64_000_000));
            Assert.That(payloadValuePlane.ElementCount, Is.EqualTo(64_000_000));
            Assert.That(tagCountPlane.ElementCount, Is.EqualTo(1_028_000));
            Assert.That(tagWordPlane.ElementCount, Is.EqualTo(20_000));
            Assert.That(activePlane.ElementSizeBytes, Is.EqualTo(280));
            Assert.That(payloadRangePlane.ElementSizeBytes, Is.EqualTo(56));
            Assert.That(payloadValuePlane.ElementSizeBytes, Is.EqualTo(8));
            Assert.That(payload.MemorySummary.OneTickScratchBytes,
                Is.GreaterThanOrEqualTo(22_120_640_320L));
            Assert.That(payload.MemorySummary.DurableAllAscBytes,
                Is.GreaterThanOrEqualTo(175_872_000_000L));
            Assert.That(payload.MemorySummary.EffectiveScopedPayloadBytes,
                Is.GreaterThanOrEqualTo(197_992_640_320L));
            Assert.That(budgetFailure.ActualValue,
                Is.EqualTo(payload.MemorySummary.EffectiveScopedPayloadBytes));
            Assert.That(budgetFailure.ExpectedMaximum,
                Is.EqualTo(source.DeclaredMemoryBudgetBytes));
            Assert.That(payload.LayoutAbiHash.Length, Is.EqualTo(64));
            AssertMemorySummaryIsExact(payload);
        }

        /// <summary>
        /// 验证 replicated_groups_50 仅作为 4×50=200 ASC 的静态算术夹具，不因规模可表示而假 Green。
        /// </summary>
        [Test]
        public void AutoChess五十复制组_应记录二百Asc静态证据而保持ConsumerRed()
        {
            const int ascCount = 200;
            var source = new TestCapacityProofSource
            {
                TargetScaleId = "fixture:autochess-replicated-groups-50",
                ScaleProfile = ProofTestData.CreateProfile(ascCount),
                DeclaredMemoryBudgetBytes = 8L * 1024L * 1024L * 1024L,
            };
            source.SetBound(GasCapacityDimension.ExpansionProgramNodes, 1);

            var payload = GasRuntimeV1CapacityProofBuilder.Build(source,
                ProofTestData.BuildLayout(257));
            var activePlane = FindMemory(payload, "Scratch.TargetActiveEffects");
            var tagCountPlane = FindMemory(payload, "Scratch.TargetTagCounts");
            var tagWordPlane = FindMemory(payload, "Scratch.TargetTagPresenceWords");

            Assert.That(payload.Profile.MaxAscRegistryCount, Is.EqualTo(200));
            Assert.That(activePlane.ElementCount, Is.EqualTo(160_000));
            Assert.That(tagCountPlane.ElementCount, Is.EqualTo(51_400));
            Assert.That(tagWordPlane.ElementCount, Is.EqualTo(1_000));
            Assert.That(payload.MemorySummary.EffectiveScopedPayloadBytes,
                Is.LessThan(source.DeclaredMemoryBudgetBytes));
            Assert.That(payload.Succeeded, Is.False);
            Assert.That(ContainsFailure(payload, GasProofFailureKind.RuntimeConsumerMissing,
                "TargetWorkUnits"), Is.True);
            AssertMemorySummaryIsExact(payload);
        }

        /// <summary>
        /// 验证 Red 矩阵 CaseId 唯一且覆盖 Layout、overflow、consumer 与 memory budget。
        /// </summary>
        [Test]
        public void Red矩阵_应具有唯一CaseId并覆盖关键失败组()
        {
            var entries = GasRuntimeV1ProofInventory.CreateRedMatrix();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                Assert.That(ids.Add(entry.CaseId), Is.True, "Duplicate CaseId: " + entry.CaseId);
                Assert.That(entry.RuleId, Is.Not.Empty);
                Assert.That(entry.RequiredEvidence, Is.Not.Empty);
            }

            Assert.That(ids.Contains("LAY-004"), Is.True);
            Assert.That(ids.Contains("LAY-007"), Is.True);
            Assert.That(ids.Contains("CAP-002"), Is.True);
            Assert.That(ids.Contains("CAP-006"), Is.True);
            Assert.That(ids.Contains("CAP-008"), Is.True);
            Assert.That(ids.Contains("CAP-009"), Is.True);
            Assert.That(ids.Contains("CAP-010"), Is.True);
            Assert.That(ids.Contains("CAP-011"), Is.True);
            Assert.That(ids.Contains("ID-001"), Is.True);

            var consumerDimensions = new HashSet<string>(StringComparer.Ordinal);
            var observationFactStatus = GasProofConsumerStatus.Missing;
            foreach (var consumer in GasRuntimeV1ProofInventory.CreateConsumerMap())
            {
                consumerDimensions.Add(consumer.DimensionId);
                if (consumer.DimensionId == GasCapacityDimension.ObservationFacts.ToString())
                    observationFactStatus = consumer.Status;
                Assert.That(consumer.Status,
                    Is.Not.EqualTo(GasProofConsumerStatus.ConsumedBeforeAuthorityWrite),
                    "B 阶段不得伪造 Z 的 authority-write 前真消费: " +
                    consumer.DimensionId);
            }
            for (var raw = 1; raw <= (int)GasCapacityDimension.DynamicDependencyBackEdges; raw++)
                Assert.That(consumerDimensions.Contains(((GasCapacityDimension)raw).ToString()),
                    Is.True, "Missing consumer map dimension: " + (GasCapacityDimension)raw);
            Assert.That(observationFactStatus,
                Is.EqualTo(GasProofConsumerStatus.EvidenceOnly));
        }

        /// <summary>
        /// 使用稳定 LayoutProof 构建一个 CapacityProof 测试快照。
        /// </summary>
        private static GasRuntimeV1CapacityProofPayload BuildCapacity(
            TestCapacityProofSource source)
        {
            return GasRuntimeV1CapacityProofBuilder.Build(
                source, ProofTestData.BuildLayout());
        }

        /// <summary>
        /// 创建单项合法、累计必越 related-key 总预算的严格递增 provenance。
        /// </summary>
        private static GasProofProvenance CreateRelatedKeyBudgetProvenance()
        {
            var related = new string[
                GasRuntimeV1ProofInventory.MaximumProofRelatedDefinitionCount];
            for (var ordinal = 0; ordinal < related.Length; ordinal++)
                related[ordinal] = "runtime/key/" + ordinal.ToString("D2");
            return new GasProofProvenance(
                1804,
                "workbook://proof-tests",
                "ProofTable",
                "row-1804",
                "Definitions[9001].RelatedBudgetProbe",
                "raw",
                "normalized",
                GasProofRuleIds.CapacityProofMissing,
                "1",
                "proof-tests/1",
                related);
        }

        /// <summary>
        /// 逐项对账 memory evidence 与 summary，同时固定 35 scratch/29 durable 的完整盘点。
        /// </summary>
        private static void AssertMemorySummaryIsExact(
            GasRuntimeV1CapacityProofPayload payload)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var scratch = 0L;
            var session = 0L;
            var perAsc = 0L;
            var scratchCount = 0;
            var durableCount = 0;
            foreach (var evidence in payload.MemoryEvidence)
            {
                Assert.That(ids.Add(evidence.EvidenceId), Is.True,
                    "Duplicate evidence: " + evidence.EvidenceId);
                if (evidence.MemoryKind == GasCapacityMemoryKind.ScratchNativeArray)
                {
                    scratch = checked(scratch + evidence.CheckedByteCount);
                    scratchCount++;
                }
                else if (evidence.MemoryKind != GasCapacityMemoryKind.Summary)
                {
                    durableCount++;
                    if (evidence.Owner == "Session")
                        session = checked(session + evidence.CheckedByteCount);
                    else if (evidence.Owner == "ASC")
                        perAsc = checked(perAsc + evidence.CheckedByteCount);
                }
            }
            var summary = payload.MemorySummary;
            Assert.That(scratchCount, Is.EqualTo(35));
            Assert.That(durableCount, Is.EqualTo(29));
            Assert.That(summary.OneTickScratchBytes, Is.EqualTo(scratch));
            Assert.That(summary.DurableSessionBytes, Is.EqualTo(session));
            Assert.That(summary.DurableBytesPerAsc, Is.EqualTo(perAsc));
            Assert.That(summary.DurableAllAscBytes,
                Is.EqualTo(checked(perAsc * payload.Profile.MaxAscRegistryCount)));
            Assert.That(summary.EffectiveScopedPayloadBytes,
                Is.EqualTo(checked(summary.EffectiveTickBatchScratchBytes + session +
                                   summary.DurableAllAscBytes)));
            Assert.That(summary.DeclaredHardCapScopedPayloadBytes,
                Is.EqualTo(checked(summary.DeclaredHardCapTickBatchScratchBytes + session +
                                   summary.DurableAllAscBytes)));
        }

        /// <summary>
        /// 在 payload 中查找指定 consumer map 行。
        /// </summary>
        private static GasProofConsumerEntry FindConsumer(
            GasRuntimeV1CapacityProofPayload payload,
            string dimensionId)
        {
            foreach (var entry in payload.ConsumerMap)
            {
                if (entry.DimensionId == dimensionId)
                    return entry;
            }
            Assert.Fail("Missing consumer: " + dimensionId);
            return default;
        }

        /// <summary>
        /// 在 payload 中查找指定 Runtime/Profile capacity 对账行。
        /// </summary>
        private static GasRuntimeCapacityLimitEntry FindLimit(
            GasRuntimeV1CapacityProofPayload payload,
            string limitId)
        {
            foreach (var entry in payload.RuntimeLimits)
            {
                if (entry.LimitId == limitId)
                    return entry;
            }
            Assert.Fail("Missing runtime limit: " + limitId);
            return default;
        }

        /// <summary>
        /// 判断 payload 是否包含指定 failure kind 与 dimension。
        /// </summary>
        private static bool ContainsFailure(
            GasRuntimeV1CapacityProofPayload payload,
            GasProofFailureKind kind,
            string dimensionId)
        {
            foreach (var failure in payload.Failures)
            {
                if (failure.FailureKind == kind && failure.DimensionId == dimensionId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 在 payload 中查找指定 failure kind 与 dimension。
        /// </summary>
        private static GasProofFailure FindFailure(
            GasRuntimeV1CapacityProofPayload payload,
            GasProofFailureKind kind,
            string dimensionId)
        {
            foreach (var failure in payload.Failures)
            {
                if (failure.FailureKind == kind && failure.DimensionId == dimensionId)
                    return failure;
            }
            Assert.Fail("Missing capacity failure: " + kind + "/" + dimensionId);
            return default;
        }

        /// <summary>
        /// 在 payload 中查找指定 memory evidence。
        /// </summary>
        private static GasCapacityMemoryEvidence FindMemory(
            GasRuntimeV1CapacityProofPayload payload,
            string evidenceId)
        {
            foreach (var evidence in payload.MemoryEvidence)
            {
                if (evidence.EvidenceId == evidenceId)
                    return evidence;
            }
            Assert.Fail("Missing memory evidence: " + evidenceId);
            return default;
        }
    }
}
