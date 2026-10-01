using GAS.Editor.CodeGen.Proofs;
using GAS.Runtime;
using NUnit.Framework;

namespace GAS.Editor.Tests.CodeGen.Proofs
{
    /// <summary>
    /// 验证 Runtime v1 LayoutProof 的 dense 映射、ABI、257 Tag、range 与 provenance Red。
    /// </summary>
    [TestFixture]
    public sealed class GasRuntimeV1LayoutProofTests
    {
        /// <summary>
        /// 验证 synthetic projection 重复构建得到相同 hash/结构自检，但 coverage 必须保持 Red。
        /// </summary>
        [Test]
        public void 合法输入_重复构建应得到稳定LayoutProof()
        {
            var first = GasRuntimeV1LayoutProofBuilder.Build(TestLayoutProofSource.CreateValid());
            var second = GasRuntimeV1LayoutProofBuilder.Build(TestLayoutProofSource.CreateValid());

            Assert.That(first.Succeeded, Is.False);
            Assert.That(second.Succeeded, Is.False);
            Assert.That(second.AttributeLayoutHash, Is.EqualTo(first.AttributeLayoutHash));
            Assert.That(second.TagCatalogHash, Is.EqualTo(first.TagCatalogHash));
            Assert.That(second.LayoutHash, Is.EqualTo(first.LayoutHash));
            Assert.That(second.ProofHash, Is.EqualTo(first.ProofHash));
            Assert.That(first.LayoutHash.Length, Is.EqualTo(64));
            Assert.That(first.ProofHash.Length, Is.EqualTo(64));
            Assert.That(GasRuntimeV1LayoutProofBuilder.TryVerifyPayload(first, out _), Is.True);
            Assert.That(first.CoverageGaps, Is.EqualTo(
                GasLayoutProofCoverageGap.CanonicalGraphAdapterMissing |
                GasLayoutProofCoverageGap.TargetPlayerAbiIdentityMissing));
            Assert.That(first.SlotAbiEvidenceStatus,
                Is.EqualTo(GasProofConsumerStatus.EvidenceOnly));
            Assert.That(FindFailure(first,
                GasProofFailureKind.ProofCoverageIncomplete).DimensionId,
                Is.EqualTo("LayoutCoverage"));
        }

        /// <summary>
        /// 以显式 synthetic ABI 和硬编码 expected 锁定 Layout v2 domain/version，不依赖宿主 SizeOf。
        /// </summary>
        [Test]
        public void LayoutV2显式AbiGolden_应命中硬编码Hash后只失败于ProofHash()
        {
            var payload = CreateLayoutGoldenPayload();

            var verified = GasRuntimeV1LayoutProofBuilder.TryVerifyPayload(
                payload, out var failure);

            Assert.That(payload.ProofSchemaVersion, Is.EqualTo(2));
            Assert.That(payload.AlgorithmVersion,
                Is.EqualTo("runtime-v1-layout-proof/2"));
            Assert.That(verified, Is.False);
            Assert.That(failure.DimensionId, Is.EqualTo("LayoutProofHash"));
        }

        /// <summary>
        /// 验证 hash 文本大小写归一，graph/provenance 仅改 ProofHash 而不污染物理 LayoutHash。
        /// </summary>
        [Test]
        public void Graph与Provenance变更_应只改变ProofIdentity()
        {
            var baselineSource = TestLayoutProofSource.CreateValid();
            var uppercaseSource = TestLayoutProofSource.CreateValid();
            uppercaseSource.CanonicalGraphHash = new string('A', 64);
            var changedGraphSource = TestLayoutProofSource.CreateValid();
            changedGraphSource.CanonicalGraphHash = new string('c', 64);
            var changedProvenanceSource = TestLayoutProofSource.CreateValid();
            var attribute = changedProvenanceSource.ReadAttribute(0);
            attribute.Provenance = ProofTestData.Provenance(999, 101,
                "Attributes[101].Changed");
            changedProvenanceSource.WriteAttribute(0, in attribute);

            var baseline = GasRuntimeV1LayoutProofBuilder.Build(baselineSource);
            var uppercase = GasRuntimeV1LayoutProofBuilder.Build(uppercaseSource);
            var changedGraph = GasRuntimeV1LayoutProofBuilder.Build(changedGraphSource);
            var changedProvenance = GasRuntimeV1LayoutProofBuilder.Build(changedProvenanceSource);

            Assert.That(uppercase.ProofHash, Is.EqualTo(baseline.ProofHash));
            Assert.That(changedGraph.LayoutHash, Is.EqualTo(baseline.LayoutHash));
            Assert.That(changedGraph.ProofHash, Is.Not.EqualTo(baseline.ProofHash));
            Assert.That(changedProvenance.LayoutHash, Is.EqualTo(baseline.LayoutHash));
            Assert.That(changedProvenance.ProofHash, Is.Not.EqualTo(baseline.ProofHash));
        }

        /// <summary>
        /// 验证快照构建后篡改 dense 内容时校验器返回带两侧 hash 的 CFG1501。
        /// </summary>
        [Test]
        public void LayoutPayload篡改_应FailClosed()
        {
            var payload = GasRuntimeV1LayoutProofBuilder.Build(
                TestLayoutProofSource.CreateValid());
            payload.Attributes[0].AttributeId = -1;

            var verified = GasRuntimeV1LayoutProofBuilder.TryVerifyPayload(
                payload, out var failure);

            Assert.That(verified, Is.False);
            Assert.That(failure.FailureKind,
                Is.EqualTo(GasProofFailureKind.ProofHashMismatch));
            Assert.That(failure.RuleId, Is.EqualTo(GasProofRuleIds.NonCanonicalIdentity));
            Assert.That(failure.ExpectedText, Has.Length.EqualTo(64));
            Assert.That(failure.ActualText, Has.Length.EqualTo(64));
        }

        /// <summary>
        /// 验证三类排序规则及四个 slot 的 owner/rebuild 文本逐字段进入 canonical LayoutHash。
        /// </summary>
        [TestCase("AttributeSortRule", -1)]
        [TestCase("TagSortRule", -1)]
        [TestCase("BlobRangeSortRule", -1)]
        [TestCase("RevisionOwner", 0)]
        [TestCase("RebuildRule", 0)]
        [TestCase("RevisionOwner", 1)]
        [TestCase("RebuildRule", 1)]
        [TestCase("RevisionOwner", 2)]
        [TestCase("RebuildRule", 2)]
        [TestCase("RevisionOwner", 3)]
        [TestCase("RebuildRule", 3)]
        public void Layout契约字段逐项篡改_应由LayoutHash拒绝(
            string fieldName,
            int slotOrdinal)
        {
            var payload = GasRuntimeV1LayoutProofBuilder.Build(
                TestLayoutProofSource.CreateValid());
            MutateLayoutContract(payload, fieldName, slotOrdinal);

            var verified = GasRuntimeV1LayoutProofBuilder.TryVerifyPayload(
                payload, out var failure);

            Assert.That(verified, Is.False);
            Assert.That(failure.FailureKind,
                Is.EqualTo(GasProofFailureKind.ProofHashMismatch));
            Assert.That(failure.DimensionId, Is.EqualTo("LayoutHash"));
        }

        /// <summary>
        /// 验证小数组配超长字符串不能绕过顶层 count 门并触发 UTF-8 大分配。
        /// </summary>
        [Test]
        public void 超长SortRule篡改_应在UTF8分配前FailClosed()
        {
            var payload = GasRuntimeV1LayoutProofBuilder.Build(
                TestLayoutProofSource.CreateValid());
            Assert.That(GasRuntimeV1LayoutProofBuilder.TryVerifyPayload(
                payload, out _), Is.True);
            payload.AttributeSortRule = new string('x',
                GasRuntimeV1ProofInventory.MaximumProofCanonicalStringBytes + 1);

            var verified = GasRuntimeV1LayoutProofBuilder.TryVerifyPayload(
                payload, out var failure);

            Assert.That(verified, Is.False);
            Assert.That(failure.DimensionId, Is.EqualTo("LayoutPayload"));
        }

        /// <summary>
        /// 验证孤立高/低代理项均被 strict UTF-8 预检拒绝，不能借 replacement fallback 验真。
        /// </summary>
        [TestCase(0xD800)]
        [TestCase(0xDC00)]
        public void 非法UTF16代理项篡改_应在Hash前FailClosed(int invalidCodeUnit)
        {
            var payload = GasRuntimeV1LayoutProofBuilder.Build(
                TestLayoutProofSource.CreateValid());
            var invalidText = new string((char)invalidCodeUnit, 1);
            payload.AttributeSortRule = invalidText;
            payload.LayoutHash = string.Empty;

            var verified = GasRuntimeV1LayoutProofBuilder.TryVerifyPayload(
                payload, out var failure);

            Assert.That(verified, Is.False);
            Assert.That(failure.DimensionId, Is.EqualTo("LayoutPayload"));
        }

        /// <summary>
        /// 验证 coverage flags 为 ProofHash 一等输入，清零不能伪造 Layout Complete。
        /// </summary>
        [Test]
        public void LayoutCoverageGaps篡改为None_应FailClosed()
        {
            var payload = GasRuntimeV1LayoutProofBuilder.Build(
                TestLayoutProofSource.CreateValid());
            Assert.That(GasRuntimeV1LayoutProofBuilder.TryVerifyPayload(
                payload, out _), Is.True);
            payload.CoverageGaps = GasLayoutProofCoverageGap.None;

            var verified = GasRuntimeV1LayoutProofBuilder.TryVerifyPayload(
                payload, out var failure);

            Assert.That(verified, Is.False);
            Assert.That(failure.DimensionId, Is.EqualTo("LayoutProofHash"));
        }

        /// <summary>
        /// 验证 builder 生成的多字节超预算 provenance 不会以空 hash 哨兵误判通过。
        /// </summary>
        [Test]
        public void 多字节Provenance超预算_应生成Red且拒绝空Hash哨兵()
        {
            var source = TestLayoutProofSource.CreateValid();
            var attribute = source.ReadAttribute(0);
            var provenance = attribute.Provenance;
            var oversizedRawValue = new string('界',
                GasRuntimeV1ProofInventory.MaximumProofCanonicalStringBytes / 3 + 1);
            attribute.Provenance = new GasProofProvenance(
                provenance.ProvenanceOrdinal,
                provenance.WorkbookId,
                provenance.TableId,
                provenance.RowStableId,
                provenance.FieldPath,
                oversizedRawValue,
                provenance.NormalizedValue,
                provenance.RuleId,
                provenance.RuleVersion,
                provenance.GeneratorVersion,
                provenance.RelatedDefinitionKeys);
            source.WriteAttribute(0, in attribute);

            var payload = GasRuntimeV1LayoutProofBuilder.Build(source);
            var verified = GasRuntimeV1LayoutProofBuilder.TryVerifyPayload(
                payload, out var failure);

            Assert.That(FindFailure(payload, GasProofFailureKind.ProvenanceMissing).DimensionId,
                Is.EqualTo("Attribute"));
            Assert.That(payload.ProofHash, Is.Empty);
            Assert.That(verified, Is.False);
            Assert.That(failure.DimensionId, Is.EqualTo("LayoutPayload"));
        }

        /// <summary>
        /// 验证 Red 矩阵的 identity 缺失与 dense gap 都有真实算法产生路径。
        /// </summary>
        [Test]
        public void Identity缺失与DenseGap_应分别输出Red()
        {
            var identitySource = TestLayoutProofSource.CreateValid();
            identitySource.CanonicalGraphHash = string.Empty;
            var denseSource = TestLayoutProofSource.CreateValid();
            var tag = denseSource.ReadTag(1);
            tag.TagIndex = 2;
            denseSource.WriteTag(1, in tag);

            var identity = GasRuntimeV1LayoutProofBuilder.Build(identitySource);
            var dense = GasRuntimeV1LayoutProofBuilder.Build(denseSource);

            Assert.That(FindFailure(identity, GasProofFailureKind.IdentityMissing).RuleId,
                Is.EqualTo(GasProofRuleIds.NonCanonicalIdentity));
            Assert.That(FindFailure(dense, GasProofFailureKind.DenseIndexInvalid).ActualValue,
                Is.EqualTo(2));
        }

        /// <summary>
        /// 验证当前宿主 ABI 仅作为 EvidenceOnly，并固定 revision owner/rebuild rule。
        /// </summary>
        [Test]
        public void 合法输入_应固定四类Slot的当前ABI与Authority()
        {
            var payload = GasRuntimeV1LayoutProofBuilder.Build(TestLayoutProofSource.CreateValid());
            var attribute = FindSlot(payload, GasLayoutSlotKind.AttributeValue);
            var tagCount = FindSlot(payload, GasLayoutSlotKind.TagCount);
            var tagPresence = FindSlot(payload, GasLayoutSlotKind.TagPresenceWord);

            Assert.That(payload.SlotAbiEvidenceStatus,
                Is.EqualTo(GasProofConsumerStatus.EvidenceOnly));
            Assert.That(attribute.ElementSizeBytes, Is.EqualTo(12));
            Assert.That(attribute.AlignmentBytes, Is.EqualTo(4));
            Assert.That(attribute.RevisionOwner, Is.EqualTo("AttributeValueSlot.Revision"));
            Assert.That(tagCount.ElementSizeBytes, Is.EqualTo(8));
            Assert.That(tagCount.AlignmentBytes, Is.EqualTo(4));
            Assert.That(tagCount.RevisionOwner, Does.Contain("InclusiveCount"));
            Assert.That(tagPresence.ElementSizeBytes, Is.EqualTo(8));
            Assert.That(tagPresence.AlignmentBytes, Is.EqualTo(8));
            Assert.That(tagPresence.RebuildRule, Does.Contain("InclusiveCount > 0"));
        }

        /// <summary>
        /// 验证 257 Tags 合法映射为 5 words，而不是被 256 位假上限截断。
        /// </summary>
        [Test]
        public void 二百五十七个Tag_应合法产生五个PresenceWord()
        {
            var payload = GasRuntimeV1LayoutProofBuilder.Build(
                TestLayoutProofSource.CreateValid(257));
            var tagCount = FindSlot(payload, GasLayoutSlotKind.TagCount);
            var tagPresence = FindSlot(payload, GasLayoutSlotKind.TagPresenceWord);

            Assert.That(payload.Succeeded, Is.False);
            Assert.That(payload.Tags.Length, Is.EqualTo(257));
            Assert.That(tagCount.ElementCount, Is.EqualTo(257));
            Assert.That(tagPresence.ElementCount, Is.EqualTo(5));
            Assert.That(tagPresence.CheckedByteCount, Is.EqualTo(40));
        }

        /// <summary>
        /// 验证重复 stable TagId 以 CFG1002 输出原字段 provenance。
        /// </summary>
        [Test]
        public void 重复TagId_应输出可定位Red()
        {
            var source = TestLayoutProofSource.CreateValid();
            var first = source.ReadTag(0);
            var duplicate = source.ReadTag(1);
            duplicate.TagId = first.TagId;
            source.WriteTag(1, in duplicate);

            var payload = GasRuntimeV1LayoutProofBuilder.Build(source);
            var failure = FindFailure(payload, GasProofFailureKind.StableIdInvalid);

            Assert.That(payload.Succeeded, Is.False);
            Assert.That(failure.RuleId, Is.EqualTo(GasProofRuleIds.InvalidDomainOrReference));
            Assert.That(failure.CanonicalOrdinal, Is.EqualTo(1));
            Assert.That(failure.Provenance.FieldPath, Is.EqualTo("Tags[1]"));
            Assert.That(failure.ActualValue, Is.EqualTo(first.TagId));
        }

        /// <summary>
        /// 验证 RequirementId 即使 ordinal 合法也必须在同一 canonical query 表内唯一。
        /// </summary>
        [Test]
        public void 重复RequirementId_应输出原Query来源Red()
        {
            var source = TestLayoutProofSource.CreateValid(queryCount: 2);
            var first = source.ReadQuery(0);
            var duplicate = source.ReadQuery(1);
            duplicate.RequirementId = first.RequirementId;
            source.WriteQuery(1, in duplicate);

            var payload = GasRuntimeV1LayoutProofBuilder.Build(source);
            var failure = FindFailure(payload, GasProofFailureKind.StableIdInvalid);

            Assert.That(failure.DimensionId, Is.EqualTo("RequirementId"));
            Assert.That(failure.CanonicalOrdinal, Is.EqualTo(1));
            Assert.That(failure.ActualValue, Is.EqualTo(first.RequirementId));
            Assert.That(failure.Provenance.FieldPath,
                Is.EqualTo("Requirements[3002]"));
        }

        /// <summary>
        /// 验证不同 RequirementId 仍必须按 canonical ID 严格递增，而不只是唯一。
        /// </summary>
        [Test]
        public void RequirementId降序_应输出Canonical顺序Red()
        {
            var source = TestLayoutProofSource.CreateValid(queryCount: 2);
            var first = source.ReadQuery(0);
            var second = source.ReadQuery(1);
            first.RequirementId = 4000;
            second.RequirementId = 3000;
            source.WriteQuery(0, in first);
            source.WriteQuery(1, in second);

            var failure = FindFailure(GasRuntimeV1LayoutProofBuilder.Build(source),
                GasProofFailureKind.StableIdInvalid);

            Assert.That(failure.DimensionId, Is.EqualTo("RequirementId"));
            Assert.That(failure.CanonicalOrdinal, Is.EqualTo(1));
            Assert.That(failure.ExpectedMaximum, Is.EqualTo(4000));
            Assert.That(failure.ActualValue, Is.EqualTo(3000));
        }

        /// <summary>
        /// 验证六类超大合法 int count 均在任何 source-sized 分配前稳定失败。
        /// </summary>
        [TestCase("AttributeCount")]
        [TestCase("TagCount")]
        [TestCase("AncestorIndexCount")]
        [TestCase("TagQueryProgramCount")]
        [TestCase("RequirementTagIndexCount")]
        [TestCase("BlobRangeCount")]
        public void 超大SourceCount_应在分配前输出稳定Red(string countName)
        {
            var firstSource = CreateWithCountOverride(countName, int.MaxValue);
            var secondSource = CreateWithCountOverride(countName, int.MaxValue);
            GasRuntimeV1LayoutProofPayload first = null;

            Assert.DoesNotThrow(() =>
                first = GasRuntimeV1LayoutProofBuilder.Build(firstSource));
            var second = GasRuntimeV1LayoutProofBuilder.Build(secondSource);
            var failure = FindFailure(first,
                GasProofFailureKind.ProofBuildBudgetExceeded);

            Assert.That(failure.DimensionId, Is.EqualTo(countName));
            Assert.That(failure.ActualValue, Is.EqualTo(int.MaxValue));
            Assert.That(first.Attributes, Is.Empty);
            Assert.That(first.Tags, Is.Empty);
            Assert.That(first.AncestorIndices, Is.Empty);
            Assert.That(first.TagQueryPrograms, Is.Empty);
            Assert.That(first.RequirementTagIndices, Is.Empty);
            Assert.That(first.BlobRanges, Is.Empty);
            Assert.That(first.ProofHash, Is.EqualTo(second.ProofHash));
        }

        /// <summary>
        /// 验证各单项 count 未越过整数门时，累计估算工作集仍受构建字节硬门约束。
        /// </summary>
        [Test]
        public void 累计Layout构建字节超预算_应在读取前稳定失败()
        {
            var source = TestLayoutProofSource.CreateValid();
            source.AttributeCountOverride = 100_000;
            source.TagCountOverride = 100_000;

            var payload = GasRuntimeV1LayoutProofBuilder.Build(source);
            var failure = FindFailure(payload,
                GasProofFailureKind.ProofBuildBudgetExceeded);

            Assert.That(failure.DimensionId, Is.EqualTo("LayoutProofBuildBytes"));
            Assert.That(failure.ActualValue,
                Is.GreaterThan(GasRuntimeV1ProofInventory.MaximumProofBuildBytes));
            Assert.That(payload.Attributes, Is.Empty);
            Assert.That(payload.Tags, Is.Empty);
        }

        /// <summary>
        /// 验证 source 实际 UTF-8 累计超门后立即停止后续 range getter，并输出可重验 Red。
        /// </summary>
        [Test]
        public void Source实际Utf8累计超预算_应停止后续读取()
        {
            const int queryCount = 300;
            var source = TestLayoutProofSource.CreateValid(queryCount: queryCount);
            var sharedRawValue = new string('x',
                GasRuntimeV1ProofInventory.MaximumProofCanonicalStringBytes);
            for (var ordinal = 0; ordinal < queryCount; ordinal++)
            {
                var query = source.ReadQuery(ordinal);
                query.Provenance = ProofTestData.WithRawValue(
                    in query.Provenance, sharedRawValue);
                source.WriteQuery(ordinal, in query);
            }

            var payload = GasRuntimeV1LayoutProofBuilder.Build(source);
            var failure = FindFailure(payload,
                GasProofFailureKind.ProofBuildBudgetExceeded);

            Assert.That(failure.DimensionId, Is.EqualTo("LayoutProofCanonicalBytes"));
            Assert.That(source.BlobRangeReadCount, Is.Zero);
            Assert.That(payload.TagQueryPrograms, Is.Empty);
            Assert.That(GasRuntimeV1LayoutProofBuilder.TryVerifyPayload(
                payload, out _), Is.True);
        }

        /// <summary>
        /// 验证 verifier 按真实累计文本预算短路，而不是只信 entry×2048 估算。
        /// </summary>
        [Test]
        public void Payload实际Utf8累计超预算_应在Hasher前拒绝()
        {
            var payload = GasRuntimeV1LayoutProofBuilder.Build(
                TestLayoutProofSource.CreateValid());
            var sharedText = new string('x',
                GasRuntimeV1ProofInventory.MaximumProofCanonicalStringBytes);
            payload.Failures = new GasProofFailure[300];
            for (var ordinal = 0; ordinal < payload.Failures.Length; ordinal++)
                payload.Failures[ordinal].ActualText = sharedText;

            var verified = GasRuntimeV1LayoutProofBuilder.TryVerifyPayload(
                payload, out var failure);

            Assert.That(verified, Is.False);
            Assert.That(failure.DimensionId, Is.EqualTo("LayoutPayload"));
        }

        /// <summary>
        /// 验证 range 使用字段元组排序：前缀 key 合法，重复 key/kind 元组稳定失败。
        /// </summary>
        [Test]
        public void BlobRange字段元组排序_应接受前缀Key并拒绝重复Tuple()
        {
            var first = CreateRange("a", 1,
                GasCatalogRangeKind.AbilityActivationRequirement, 0);
            var prefixed = CreateRange("a/001", 2,
                GasCatalogRangeKind.ApplicationRequirement, 1);
            var valid = GasRuntimeV1LayoutProofBuilder.Build(
                TestLayoutProofSource.CreateWithRanges(new[] { first, prefixed }));
            var duplicate = prefixed;
            duplicate.DefinitionKey = first.DefinitionKey;
            duplicate.RangeKind = first.RangeKind;
            var invalid = GasRuntimeV1LayoutProofBuilder.Build(
                TestLayoutProofSource.CreateWithRanges(new[] { first, duplicate }));

            Assert.That(ContainsFailure(valid,
                GasProofFailureKind.CanonicalOrdinalInvalid), Is.False);
            Assert.That(FindFailure(invalid,
                GasProofFailureKind.CanonicalOrdinalInvalid).CanonicalOrdinal,
                Is.EqualTo(1));
        }

        /// <summary>
        /// 验证 ancestor 反向环以 canonical Tag provenance 明确失败。
        /// </summary>
        [Test]
        public void Ancestor反向环_应输出闭包Red()
        {
            var source = TestLayoutProofSource.CreateValid();
            var first = source.ReadTag(0);
            var second = source.ReadTag(1);
            first.AncestorStart = 0;
            first.AncestorCount = 2;
            second.AncestorStart = 0;
            second.AncestorCount = 2;
            source.WriteTag(0, in first);
            source.WriteTag(1, in second);

            var payload = GasRuntimeV1LayoutProofBuilder.Build(source);
            var failure = FindFailure(payload, GasProofFailureKind.AncestorProgramInvalid);

            Assert.That(payload.Succeeded, Is.False);
            Assert.That(failure.RuleId, Is.EqualTo(GasProofRuleIds.InvalidDomainOrReference));
            Assert.That(failure.DimensionId, Is.EqualTo("TagAncestors"));
            Assert.That(failure.Provenance.FieldPath, Does.StartWith("Tags["));
        }

        /// <summary>
        /// 验证 int 半开 range 的 Start+Count 越过 int 上界时保留两个操作数。
        /// </summary>
        [Test]
        public void BlobRange加法越界_应输出Checked操作数()
        {
            var source = TestLayoutProofSource.CreateValid();
            var range = source.ReadRange(0);
            range.Start = int.MaxValue;
            range.Count = 1;
            range.BackingLength = int.MaxValue;
            source.WriteRange(0, in range);

            var payload = GasRuntimeV1LayoutProofBuilder.Build(source);
            var failure = FindFailure(payload, GasProofFailureKind.RangeArithmeticOverflow);

            Assert.That(failure.Operator, Is.EqualTo("+"));
            Assert.That(failure.LeftOperandName, Is.EqualTo("Start"));
            Assert.That(failure.LeftOperandValue, Is.EqualTo(int.MaxValue));
            Assert.That(failure.RightOperandName, Is.EqualTo("Count"));
            Assert.That(failure.RightOperandValue, Is.EqualTo(1));
            Assert.That(failure.Provenance.FieldPath,
                Is.EqualTo("Definitions[9001].ApplicationRequirementRange"));
        }

        /// <summary>
        /// 在 payload 中查找指定物理 slot 证据。
        /// </summary>
        private static GasLayoutSlotProofEntry FindSlot(
            GasRuntimeV1LayoutProofPayload payload,
            GasLayoutSlotKind kind)
        {
            foreach (var entry in payload.Slots)
            {
                if (entry.SlotKind == kind)
                    return entry;
            }
            Assert.Fail("Missing layout slot: " + kind);
            return default;
        }

        /// <summary>
        /// 创建固定字段的 synthetic Layout v2 golden payload，不读取宿主 ABI。
        /// </summary>
        private static GasRuntimeV1LayoutProofPayload CreateLayoutGoldenPayload()
        {
            return new GasRuntimeV1LayoutProofPayload
            {
                ProofSchemaVersion = 2,
                AlgorithmVersion = "runtime-v1-layout-proof/2",
                AttributeSortRule = "AttributeId asc; LayoutIndex == CanonicalOrdinal",
                TagSortRule = "TagId asc; TagIndex == CanonicalOrdinal; RequirementId asc",
                BlobRangeSortRule = "DefinitionKey ordinal asc; RangeKind numeric asc; CanonicalOrdinal dense",
                AttributeLayoutHash =
                    "f7cc5702b4b242fa12337d1e4e19908db5dc3520cf97aefe2ab699b626d64502",
                TagCatalogHash =
                    "d0b32b707a3487a607ad1b846f592065e44c13401120416826c7225f8fe556ab",
                LayoutHash =
                    "54d63624df7c67325f25c7755692851359b56329c5bd5a6d1b099b904b2a7853",
                Slots = CreateGoldenSlots(),
                CoverageGaps = GasLayoutProofCoverageGap.CanonicalGraphAdapterMissing |
                               GasLayoutProofCoverageGap.TargetPlayerAbiIdentityMissing,
                SlotAbiEvidenceStatus = GasProofConsumerStatus.EvidenceOnly,
            };
        }

        /// <summary>
        /// 创建不绑定 x64/Mono 的四个显式 synthetic slot golden。
        /// </summary>
        private static GasLayoutSlotProofEntry[] CreateGoldenSlots()
        {
            return new[]
            {
                GoldenSlot(GasLayoutSlotKind.AttributeValue, "golden.Attribute", 2, 12, 4),
                GoldenSlot(GasLayoutSlotKind.AttributeDirtyWord, "golden.AttributeWord", 1, 8, 8),
                GoldenSlot(GasLayoutSlotKind.TagCount, "golden.TagCount", 3, 8, 4),
                GoldenSlot(GasLayoutSlotKind.TagPresenceWord, "golden.TagWord", 1, 8, 8),
            };
        }

        /// <summary>
        /// 创建一条显式 ABI slot，字符串契约固定供 domain golden 使用。
        /// </summary>
        private static GasLayoutSlotProofEntry GoldenSlot(
            GasLayoutSlotKind kind,
            string typeName,
            long count,
            int size,
            int alignment)
        {
            return new GasLayoutSlotProofEntry
            {
                SlotKind = kind,
                RuntimeTypeName = typeName,
                ElementCount = count,
                ElementSizeBytes = size,
                AlignmentBytes = alignment,
                CheckedByteCount = count * size,
                RevisionContractId = "golden-revision/1",
                RebuildContractId = "golden-rebuild/1",
                RevisionOwner = "golden-owner",
                WordBitCount = kind == GasLayoutSlotKind.AttributeDirtyWord ||
                               kind == GasLayoutSlotKind.TagPresenceWord ? 64 : 0,
                RebuildRule = "golden-rule",
            };
        }

        /// <summary>
        /// 创建一条用于字段元组排序的零长度合法 range。
        /// </summary>
        private static GasLayoutRangeProofEntry CreateRange(
            string key,
            int definitionId,
            GasCatalogRangeKind kind,
            int ordinal)
        {
            return new GasLayoutRangeProofEntry
            {
                RangeKind = kind,
                DefinitionKey = key,
                DefinitionId = definitionId,
                CanonicalOrdinal = ordinal,
                Start = 0,
                Count = 0,
                BackingLength = 0,
                Provenance = ProofTestData.Provenance(
                    2000 + ordinal, definitionId, "Ranges[" + ordinal + "]"),
            };
        }

        /// <summary>
        /// 仅修改 mutation matrix 指定的一个排序或 slot authority 字段。
        /// </summary>
        private static void MutateLayoutContract(
            GasRuntimeV1LayoutProofPayload payload,
            string fieldName,
            int slotOrdinal)
        {
            if (slotOrdinal >= 0)
            {
                var slot = payload.Slots[slotOrdinal];
                if (fieldName == "RevisionOwner")
                    slot.RevisionOwner += "#tampered";
                else
                    slot.RebuildRule += "#tampered";
                payload.Slots[slotOrdinal] = slot;
                return;
            }
            if (fieldName == "AttributeSortRule")
                payload.AttributeSortRule += "#tampered";
            else if (fieldName == "TagSortRule")
                payload.TagSortRule += "#tampered";
            else
                payload.BlobRangeSortRule += "#tampered";
        }

        /// <summary>
        /// 创建仅覆盖一个 source count 的合法最小 adapter。
        /// </summary>
        private static TestLayoutProofSource CreateWithCountOverride(
            string countName,
            int value)
        {
            var source = TestLayoutProofSource.CreateValid();
            if (countName == "AttributeCount")
                source.AttributeCountOverride = value;
            else if (countName == "TagCount")
                source.TagCountOverride = value;
            else if (countName == "AncestorIndexCount")
                source.AncestorIndexCountOverride = value;
            else if (countName == "TagQueryProgramCount")
                source.TagQueryProgramCountOverride = value;
            else if (countName == "RequirementTagIndexCount")
                source.RequirementTagIndexCountOverride = value;
            else
                source.BlobRangeCountOverride = value;
            return source;
        }

        /// <summary>
        /// 在 payload 中查找指定失败类型。
        /// </summary>
        private static GasProofFailure FindFailure(
            GasRuntimeV1LayoutProofPayload payload,
            GasProofFailureKind kind)
        {
            foreach (var failure in payload.Failures)
            {
                if (failure.FailureKind == kind)
                    return failure;
            }
            Assert.Fail("Missing layout failure: " + kind);
            return default;
        }

        /// <summary>
        /// 判断 payload 是否包含指定 Layout failure kind。
        /// </summary>
        private static bool ContainsFailure(
            GasRuntimeV1LayoutProofPayload payload,
            GasProofFailureKind kind)
        {
            foreach (var failure in payload.Failures)
                if (failure.FailureKind == kind)
                    return true;
            return false;
        }
    }
}
