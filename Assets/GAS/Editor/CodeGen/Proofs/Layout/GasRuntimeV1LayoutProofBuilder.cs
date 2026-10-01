using System;
using System.Collections.Generic;
using GAS.Runtime;
using Unity.Collections.LowLevel.Unsafe;

namespace GAS.Editor.CodeGen.Proofs
{
    /// <summary>
    /// 从 A 链只读投影生成 Runtime v1 LayoutProof，并在任何身份、dense、range 或 ABI 不闭合时 fail-closed。
    /// </summary>
    public static class GasRuntimeV1LayoutProofBuilder
    {
        private const int FixedSlotCount = 4;
        private const int ConservativeSourceEntryBuildBytes = 2048;
        private const int ConservativePayloadEntryEncodingBytes =
            ConservativeSourceEntryBuildBytes;
        private const GasLayoutProofCoverageGap CurrentCoverageGaps =
            GasLayoutProofCoverageGap.CanonicalGraphAdapterMissing |
            GasLayoutProofCoverageGap.TargetPlayerAbiIdentityMissing;

        /// <summary>
        /// 构建独立 LayoutProof payload；返回值只代表该 payload 是否无 Red，不触发发布或资格提升。
        /// </summary>
        public static bool TryBuild(
            IGasLayoutProofSource source,
            out GasRuntimeV1LayoutProofPayload payload)
        {
            payload = Build(source);
            return payload.Succeeded;
        }

        /// <summary>
        /// 重新编码并校验一个 LayoutProof 快照，任何字段或数组篡改都以 CFG1501 失败。
        /// </summary>
        public static bool TryVerifyPayload(
            GasRuntimeV1LayoutProofPayload payload,
            out GasProofFailure failure)
        {
            failure = default;
            if (!HasCompletePayloadArrays(payload))
            {
                failure = IntegrityFailure("LayoutPayload", string.Empty, string.Empty);
                return false;
            }
            if (payload.ProofSchemaVersion != GasRuntimeV1ProofInventory.LayoutProofSchemaVersion ||
                payload.AlgorithmVersion != GasRuntimeV1ProofInventory.LayoutAlgorithmVersion)
            {
                failure = IntegrityFailure("LayoutProofSchema",
                    GasRuntimeV1ProofInventory.LayoutAlgorithmVersion,
                    payload.AlgorithmVersion);
                return false;
            }
            if (!VerifyHash(payload.AttributeLayoutHash, ComputeAttributeHash(payload),
                    "AttributeLayoutHash", out failure) ||
                !VerifyHash(payload.TagCatalogHash, ComputeTagHash(payload),
                    "TagCatalogHash", out failure) ||
                !VerifyHash(payload.LayoutHash, ComputeLayoutHash(payload),
                    "LayoutHash", out failure) ||
                !VerifyHash(payload.ProofHash, ComputeProofHash(payload),
                    "LayoutProofHash", out failure))
                return false;
            return true;
        }

        /// <summary>
        /// 构建包含成功证据或完整失败 provenance 的确定性 LayoutProof payload。
        /// </summary>
        public static GasRuntimeV1LayoutProofPayload Build(IGasLayoutProofSource source)
        {
            var payload = CreatePayload(source);
            var failures = new List<GasProofFailure>();
            if (source == null)
            {
                failures.Add(Failure(
                    GasProofFailureKind.InputUnavailable,
                    GasProofRuleIds.CapacityProofMissing,
                    "LayoutSource",
                    -1,
                    default));
            }
            else
            {
                ValidateIdentity(source, failures);
                var counts = new LayoutSourceCounts(source);
                if (TryValidateBuildBudget(in counts, failures))
                {
                    if (TryReadSource(source, in counts, payload, failures))
                    {
                        ValidateAncestors(payload.Tags, payload.AncestorIndices, failures);
                        ValidateQueries(payload.TagQueryPrograms,
                            payload.RequirementTagIndices, payload.Tags.Length, failures);
                    }
                }
            }

            payload.Slots = CreateSlotProofs(payload.Attributes.Length, payload.Tags.Length, failures);
            BuildCoverageEvidence(payload, failures);
            FinalizeHashes(payload, failures);
            return payload;
        }

        /// <summary>
        /// 创建只含 proof identity 与固定排序规则的空 payload。
        /// </summary>
        private static GasRuntimeV1LayoutProofPayload CreatePayload(IGasLayoutProofSource source)
        {
            return new GasRuntimeV1LayoutProofPayload
            {
                ProofSchemaVersion = GasRuntimeV1ProofInventory.LayoutProofSchemaVersion,
                AlgorithmVersion = GasRuntimeV1ProofInventory.LayoutAlgorithmVersion,
                GraphSchemaVersion = source == null ? 0 : source.GraphSchemaVersion,
                GeneratorVersion = source == null ||
                                   !GasProofCanonicalHashWriter.IsWithinStringBudget(
                                       source.GeneratorVersion)
                    ? string.Empty
                    : source.GeneratorVersion ?? string.Empty,
                CanonicalGraphHash = source == null
                    ? string.Empty
                    : GasProofCanonicalHashWriter.NormalizeSha256(source.CanonicalGraphHash),
                AttributeSortRule = "AttributeId asc; LayoutIndex == CanonicalOrdinal",
                TagSortRule = "TagId asc; TagIndex == CanonicalOrdinal; RequirementId asc",
                BlobRangeSortRule = "DefinitionKey ordinal asc; RangeKind numeric asc; CanonicalOrdinal dense",
                CoverageGaps = CurrentCoverageGaps,
                SlotAbiEvidenceStatus = GasProofConsumerStatus.EvidenceOnly,
            };
        }

        /// <summary>
        /// 验证 canonical graph identity 的最小版本、生成器与 SHA-256 字段。
        /// </summary>
        private static void ValidateIdentity(
            IGasLayoutProofSource source,
            List<GasProofFailure> failures)
        {
            if (source.GraphSchemaVersion > 0 &&
                !string.IsNullOrWhiteSpace(source.GeneratorVersion) &&
                GasProofCanonicalHashWriter.IsWithinStringBudget(source.GeneratorVersion) &&
                GasProofCanonicalHashWriter.IsSha256Hex(source.CanonicalGraphHash))
                return;

            failures.Add(Failure(
                GasProofFailureKind.IdentityMissing,
                GasProofRuleIds.NonCanonicalIdentity,
                "CanonicalGraphIdentity",
                -1,
                default,
                actual: source.GraphSchemaVersion));
        }

        /// <summary>
        /// 以单一真实编码预算按 canonical 顺序读取 source，任一超门后不再调用后续 getter。
        /// </summary>
        private static bool TryReadSource(
            IGasLayoutProofSource source,
            in LayoutSourceCounts counts,
            GasRuntimeV1LayoutProofPayload payload,
            List<GasProofFailure> failures)
        {
            var budget = new GasProofEncodingBudget();
            if (!TryAddLayoutHeaderStrings(payload, ref budget))
                return AbortSourceRead(payload, in budget, failures);
            payload.Attributes = ReadAttributes(source, counts.AttributeCount,
                ref budget, out var budgetValid, failures);
            if (!budgetValid)
                return AbortSourceRead(payload, in budget, failures);
            payload.Tags = ReadTags(source, counts.TagCount,
                counts.AncestorIndexCount, ref budget, out budgetValid, failures);
            if (!budgetValid)
                return AbortSourceRead(payload, in budget, failures);
            payload.AncestorIndices = ReadIndices(counts.AncestorIndexCount,
                source.TryGetAncestorIndex, "AncestorIndices", failures);
            payload.TagQueryPrograms = ReadQueries(source,
                counts.TagQueryProgramCount, counts.RequirementTagIndexCount,
                ref budget, out budgetValid, failures);
            if (!budgetValid)
                return AbortSourceRead(payload, in budget, failures);
            payload.RequirementTagIndices = ReadIndices(
                counts.RequirementTagIndexCount, source.TryGetRequirementTagIndex,
                "RequirementTagIndices", failures);
            payload.BlobRanges = ReadRanges(source, counts.BlobRangeCount,
                ref budget, out budgetValid, failures);
            return budgetValid || AbortSourceRead(payload, in budget, failures);
        }

        /// <summary>
        /// 把 Layout 顶层 identity 与排序契约计入 source 阶段真实 UTF-8 预算。
        /// </summary>
        private static bool TryAddLayoutHeaderStrings(
            GasRuntimeV1LayoutProofPayload payload,
            ref GasProofEncodingBudget budget)
        {
            return budget.TryAddString(payload.AlgorithmVersion) &&
                   budget.TryAddString(payload.GeneratorVersion) &&
                   budget.TryAddString(payload.CanonicalGraphHash) &&
                   budget.TryAddString(payload.AttributeSortRule) &&
                   budget.TryAddString(payload.TagSortRule) &&
                   budget.TryAddString(payload.BlobRangeSortRule);
        }

        /// <summary>
        /// source 实际编码超门时清空部分投影并输出带 actual/max 的稳定预算 Red。
        /// </summary>
        private static bool AbortSourceRead(
            GasRuntimeV1LayoutProofPayload payload,
            in GasProofEncodingBudget budget,
            List<GasProofFailure> failures)
        {
            payload.Attributes = Array.Empty<GasLayoutAttributeProofEntry>();
            payload.Tags = Array.Empty<GasLayoutTagProofEntry>();
            payload.AncestorIndices = Array.Empty<int>();
            payload.TagQueryPrograms = Array.Empty<GasLayoutTagQueryProofEntry>();
            payload.RequirementTagIndices = Array.Empty<int>();
            payload.BlobRanges = Array.Empty<GasLayoutRangeProofEntry>();
            var related = budget.FailedOnRelatedDefinitions;
            return AddBuildBudgetFailure(
                related ? "LayoutProofRelatedDefinitionCount" : "LayoutProofCanonicalBytes",
                budget.FailureActual,
                related
                    ? GasRuntimeV1ProofInventory.MaximumProofBuildEntryCount
                    : GasRuntimeV1ProofInventory.MaximumProofBuildBytes,
                related ? "related-definitions" : "bytes", failures);
        }

        /// <summary>
        /// 读取并验证 stable AttributeId 严格递增及 dense index/ordinal 无缺口。
        /// </summary>
        private static GasLayoutAttributeProofEntry[] ReadAttributes(
            IGasLayoutProofSource source,
            int count,
            ref GasProofEncodingBudget budget,
            out bool budgetValid,
            List<GasProofFailure> failures)
        {
            var entries = new GasLayoutAttributeProofEntry[count];
            var previousId = int.MinValue;
            budgetValid = true;
            for (var ordinal = 0; ordinal < count; ordinal++)
            {
                if (!source.TryGetAttribute(ordinal, out var entry))
                {
                    failures.Add(Failure(GasProofFailureKind.InputUnavailable,
                        GasProofRuleIds.CapacityProofMissing, "Attribute", ordinal, default));
                    continue;
                }

                entries[ordinal] = entry;
                ValidateProvenance(entry.Provenance, "Attribute", entry.AttributeId, ordinal, failures);
                if (!budget.TryAddProvenance(in entry.Provenance))
                {
                    budgetValid = false;
                    break;
                }
                if (entry.AttributeId <= 0 || entry.AttributeId <= previousId)
                    failures.Add(Failure(GasProofFailureKind.StableIdInvalid,
                        GasProofRuleIds.InvalidDomainOrReference, "AttributeId", ordinal,
                        entry.Provenance, expected: previousId, actual: entry.AttributeId));
                if (entry.LayoutIndex != ordinal || entry.CanonicalOrdinal != ordinal)
                    failures.Add(Failure(GasProofFailureKind.DenseIndexInvalid,
                        GasProofRuleIds.InvalidDomainOrReference, "AttributeLayoutIndex", ordinal,
                        entry.Provenance, expected: ordinal, actual: entry.LayoutIndex));
                previousId = entry.AttributeId;
            }

            return entries;
        }

        /// <summary>
        /// 读取并验证 stable TagId 严格递增、dense index 与 ancestor range。
        /// </summary>
        private static GasLayoutTagProofEntry[] ReadTags(
            IGasLayoutProofSource source,
            int count,
            int ancestorCount,
            ref GasProofEncodingBudget budget,
            out bool budgetValid,
            List<GasProofFailure> failures)
        {
            var entries = new GasLayoutTagProofEntry[count];
            var previousId = int.MinValue;
            budgetValid = true;
            for (var ordinal = 0; ordinal < count; ordinal++)
            {
                if (!source.TryGetTag(ordinal, out var entry))
                {
                    failures.Add(Failure(GasProofFailureKind.InputUnavailable,
                        GasProofRuleIds.CapacityProofMissing, "Tag", ordinal, default));
                    continue;
                }

                entries[ordinal] = entry;
                ValidateProvenance(entry.Provenance, "Tag", entry.TagId, ordinal, failures);
                if (!budget.TryAddProvenance(in entry.Provenance))
                {
                    budgetValid = false;
                    break;
                }
                if (entry.TagId <= 0 || entry.TagId <= previousId)
                    failures.Add(Failure(GasProofFailureKind.StableIdInvalid,
                        GasProofRuleIds.InvalidDomainOrReference, "TagId", ordinal,
                        entry.Provenance, expected: previousId, actual: entry.TagId));
                if (entry.TagIndex != ordinal || entry.CanonicalOrdinal != ordinal)
                    failures.Add(Failure(GasProofFailureKind.DenseIndexInvalid,
                        GasProofRuleIds.InvalidDomainOrReference, "TagCatalogIndex", ordinal,
                        entry.Provenance, expected: ordinal, actual: entry.TagIndex));
                ValidateRange(entry.AncestorStart, entry.AncestorCount, ancestorCount,
                    "TagAncestors", entry.TagId, ordinal, entry.Provenance, failures);
                previousId = entry.TagId;
            }

            return entries;
        }

        /// <summary>
        /// 读取一个 canonical int flat array，缺失元素以 -1 保留其 ordinal 后输出 Red。
        /// </summary>
        private static int[] ReadIndices(
            int count,
            TryReadIndex tryRead,
            string dimensionId,
            List<GasProofFailure> failures)
        {
            var values = new int[count];
            for (var ordinal = 0; ordinal < count; ordinal++)
            {
                if (tryRead(ordinal, out values[ordinal]))
                    continue;
                values[ordinal] = -1;
                failures.Add(Failure(GasProofFailureKind.InputUnavailable,
                    GasProofRuleIds.CapacityProofMissing, dimensionId, ordinal, default));
            }

            return values;
        }

        /// <summary>
        /// 读取 TagQueryProgram 并验证 identity/provenance，range 内容在 flat array 就绪后统一检查。
        /// </summary>
        private static GasLayoutTagQueryProofEntry[] ReadQueries(
            IGasLayoutProofSource source,
            int count,
            int flatCount,
            ref GasProofEncodingBudget budget,
            out bool budgetValid,
            List<GasProofFailure> failures)
        {
            var entries = new GasLayoutTagQueryProofEntry[count];
            var previousRequirementId = int.MinValue;
            budgetValid = true;
            for (var ordinal = 0; ordinal < count; ordinal++)
            {
                if (!source.TryGetTagQueryProgram(ordinal, out var entry))
                {
                    failures.Add(Failure(GasProofFailureKind.InputUnavailable,
                        GasProofRuleIds.CapacityProofMissing, "TagQueryProgram", ordinal, default));
                    continue;
                }

                entries[ordinal] = entry;
                ValidateProvenance(entry.Provenance, "TagQueryProgram", entry.RequirementId,
                    ordinal, failures);
                if (!budget.TryAddProvenance(in entry.Provenance))
                {
                    budgetValid = false;
                    break;
                }
                if (entry.RequirementId <= 0 ||
                    entry.RequirementId <= previousRequirementId)
                    failures.Add(Failure(GasProofFailureKind.StableIdInvalid,
                        GasProofRuleIds.InvalidDomainOrReference, "RequirementId", ordinal,
                        entry.Provenance, expected: previousRequirementId,
                        actual: entry.RequirementId));
                if (entry.CanonicalOrdinal != ordinal)
                    failures.Add(Failure(GasProofFailureKind.CanonicalOrdinalInvalid,
                        GasProofRuleIds.InvalidDomainOrReference, "TagQueryProgram", ordinal,
                        entry.Provenance, expected: ordinal, actual: entry.CanonicalOrdinal));
                ValidateRange(entry.TagIndexStart, entry.TagIndexCount, flatCount,
                    "RequirementTags", entry.RequirementId, ordinal, entry.Provenance, failures);
                previousRequirementId = entry.RequirementId;
            }

            return entries;
        }

        /// <summary>
        /// 读取并验证所有 Blob range 的 canonical ordinal 与 checked 半开区间。
        /// </summary>
        private static GasLayoutRangeProofEntry[] ReadRanges(
            IGasLayoutProofSource source,
            int count,
            ref GasProofEncodingBudget budget,
            out bool budgetValid,
            List<GasProofFailure> failures)
        {
            var entries = new GasLayoutRangeProofEntry[count];
            var backingLengthByKind = new Dictionary<GasCatalogRangeKind, int>();
            var previousEntry = default(GasLayoutRangeProofEntry);
            budgetValid = true;
            for (var ordinal = 0; ordinal < count; ordinal++)
            {
                if (!source.TryGetBlobRange(ordinal, out var entry))
                {
                    failures.Add(Failure(GasProofFailureKind.InputUnavailable,
                        GasProofRuleIds.CapacityProofMissing, "BlobRange", ordinal, default));
                    continue;
                }

                var validDefinitionKey =
                    GasProofCanonicalHashWriter.IsWithinStringBudget(entry.DefinitionKey);
                if (!validDefinitionKey)
                    entry.DefinitionKey = string.Empty;
                entries[ordinal] = entry;
                ValidateProvenance(entry.Provenance, "BlobRange", entry.DefinitionId,
                    ordinal, failures);
                if (!budget.TryAddString(entry.DefinitionKey) ||
                    !budget.TryAddProvenance(in entry.Provenance))
                {
                    budgetValid = false;
                    break;
                }
                var validKind = entry.RangeKind >= GasCatalogRangeKind.ApplicationRequirement &&
                                entry.RangeKind <= GasCatalogRangeKind.AbilityActivationRequirement;
                var validIdentity = entry.DefinitionId > 0 &&
                                    !string.IsNullOrWhiteSpace(entry.DefinitionKey) &&
                                    validDefinitionKey &&
                                    (ordinal == 0 ||
                                     IsRangeIdentityAfter(in entry, in previousEntry));
                if (entry.CanonicalOrdinal != ordinal || !validKind || !validIdentity)
                    failures.Add(Failure(GasProofFailureKind.CanonicalOrdinalInvalid,
                        GasProofRuleIds.InvalidDomainOrReference, "BlobRangeOrdinal", ordinal,
                        entry.Provenance, expected: ordinal, actual: entry.CanonicalOrdinal));
                ValidateRange(entry.Start, entry.Count, entry.BackingLength,
                    entry.RangeKind.ToString(), entry.DefinitionId, ordinal,
                    entry.Provenance, failures);
                ValidateBackingLength(entry, backingLengthByKind, ordinal, failures);
                previousEntry = entry;
            }

            return entries;
        }

        /// <summary>
        /// 以 DefinitionKey ordinal、RangeKind numeric 的字段元组比较 range identity，不构造拼接串。
        /// </summary>
        private static bool IsRangeIdentityAfter(
            in GasLayoutRangeProofEntry current,
            in GasLayoutRangeProofEntry previous)
        {
            var keyComparison = string.CompareOrdinal(
                current.DefinitionKey, previous.DefinitionKey);
            return keyComparison > 0 ||
                   (keyComparison == 0 && current.RangeKind > previous.RangeKind);
        }

        /// <summary>
        /// 验证同一 RangeKind 全部记录引用同一 backing array 长度。
        /// </summary>
        private static void ValidateBackingLength(
            in GasLayoutRangeProofEntry entry,
            Dictionary<GasCatalogRangeKind, int> backingLengthByKind,
            int ordinal,
            List<GasProofFailure> failures)
        {
            if (!backingLengthByKind.TryGetValue(entry.RangeKind, out var expected))
            {
                backingLengthByKind[entry.RangeKind] = entry.BackingLength;
                return;
            }
            if (entry.BackingLength != expected)
                failures.Add(Failure(GasProofFailureKind.RangeInvalid,
                    GasProofRuleIds.InvalidDomainOrReference,
                    entry.RangeKind + ".BackingLength", ordinal,
                    entry.Provenance, entry.DefinitionId, expected,
                    entry.BackingLength));
        }

        /// <summary>
        /// 验证每个 ancestor range 严格递增、包含自身、无反向环且包含完整传递闭包。
        /// </summary>
        private static void ValidateAncestors(
            GasLayoutTagProofEntry[] tags,
            int[] ancestors,
            List<GasProofFailure> failures)
        {
            for (var tagIndex = 0; tagIndex < tags.Length; tagIndex++)
            {
                var tag = tags[tagIndex];
                if (!IsRangeInBounds(tag.AncestorStart, tag.AncestorCount, ancestors.Length))
                    continue;
                var valid = IsCanonicalAncestorRange(tagIndex, tag, ancestors, tags.Length);
                valid &= valid && IsAncestorClosureValid(tagIndex, tags, ancestors);
                if (!valid)
                    failures.Add(Failure(GasProofFailureKind.AncestorProgramInvalid,
                        GasProofRuleIds.InvalidDomainOrReference, "TagAncestors", tagIndex,
                        tag.Provenance, expected: tags.Length, actual: tag.AncestorCount));
            }
        }

        /// <summary>
        /// 验证 TagQueryProgram 的 phase/match、非空 tag range 与严格递增 dense indices。
        /// </summary>
        private static void ValidateQueries(
            GasLayoutTagQueryProofEntry[] queries,
            int[] tagIndices,
            int tagCount,
            List<GasProofFailure> failures)
        {
            for (var ordinal = 0; ordinal < queries.Length; ordinal++)
            {
                var query = queries[ordinal];
                var validEnums = query.Phase >= GasRequirementPhase.Application &&
                                 query.Phase <= GasRequirementPhase.AbilityActivation &&
                                 query.Match >= GasTagRequirementMatch.All &&
                                 query.Match <= GasTagRequirementMatch.None;
                var validRange = query.TagIndexCount > 0 &&
                                 IsRangeInBounds(query.TagIndexStart, query.TagIndexCount, tagIndices.Length);
                if (validEnums && validRange &&
                    IsStrictlyIncreasingRange(tagIndices, query.TagIndexStart,
                        query.TagIndexCount, tagCount))
                    continue;
                failures.Add(Failure(GasProofFailureKind.AncestorProgramInvalid,
                    GasProofRuleIds.InvalidDomainOrReference, "TagQueryProgram", ordinal,
                    query.Provenance, expected: tagCount, actual: query.TagIndexCount));
            }
        }

        /// <summary>
        /// 创建四类 Runtime v1 固定逻辑槽的当前宿主 UnsafeUtility ABI EvidenceOnly 与 checked bytes。
        /// </summary>
        private static GasLayoutSlotProofEntry[] CreateSlotProofs(
            int attributeCount,
            int tagCount,
            List<GasProofFailure> failures)
        {
            GasCheckedProofMath.TryCalculateWordCount(attributeCount, out var attributeWords);
            GasCheckedProofMath.TryCalculateWordCount(tagCount, out var tagWords);
            return new[]
            {
                Slot<AttributeValueSlot>(GasLayoutSlotKind.AttributeValue, attributeCount,
                    "attribute-value-slot-revision/1", "attribute-authority/1",
                    "AttributeValueSlot.Revision", 0,
                    "Base/Current/Revision 是唯一 Attribute authority", failures),
                Slot<AttributeDirtyWord>(GasLayoutSlotKind.AttributeDirtyWord, attributeWords,
                    "attribute-value-slot-revision/1", "attribute-dirty-rebuild/1",
                    "AttributeValueSlot.Revision", 64,
                    "由本 Tick mutation 或 AttributeValueSlot 全量比较重建", failures),
                Slot<TagCountSlot>(GasLayoutSlotKind.TagCount, tagCount,
                    "tag-count-slot-counts/1", "tag-authority/1",
                    "TagCountSlot.ExactCount/InclusiveCount", 0,
                    "Exact/Inclusive count 是唯一 Tag authority", failures),
                Slot<TagPresenceWord>(GasLayoutSlotKind.TagPresenceWord, tagWords,
                    "tag-count-slot-inclusive-count/1", "tag-presence-rebuild/1",
                    "TagCountSlot.InclusiveCount", 64,
                    "按 dense TagCatalog 以 InclusiveCount > 0 全量重建", failures),
            };
        }

        /// <summary>
        /// 创建一个 Runtime unmanaged 槽的元素数量、宿主 ABI 证据与 checked byte 数。
        /// </summary>
        private static GasLayoutSlotProofEntry Slot<T>(
            GasLayoutSlotKind kind,
            long count,
            string revisionContractId,
            string rebuildContractId,
            string revisionOwner,
            int wordBits,
            string rebuildRule,
            List<GasProofFailure> failures)
            where T : unmanaged
        {
            var size = UnsafeUtility.SizeOf<T>();
            var alignment = UnsafeUtility.AlignOf<T>();
            if (!GasCheckedProofMath.TryMultiply(count, size, out var bytes))
            {
                failures.Add(Failure(GasProofFailureKind.ArithmeticOverflow,
                    GasProofRuleIds.CapacityProofMissing, kind.ToString(), -1, default,
                    leftName: "ElementCount", left: count,
                    rightName: "ElementSizeBytes", right: size));
            }

            return new GasLayoutSlotProofEntry
            {
                SlotKind = kind,
                RuntimeTypeName = typeof(T).FullName,
                ElementCount = count,
                ElementSizeBytes = size,
                AlignmentBytes = alignment,
                CheckedByteCount = bytes,
                RevisionContractId = revisionContractId,
                RebuildContractId = rebuildContractId,
                RevisionOwner = revisionOwner,
                WordBitCount = wordBits,
                RebuildRule = rebuildRule,
            };
        }

        /// <summary>
        /// 将未冻结 canonical adapter 与目标 Player ABI 身份固化为可哈希的稳定 Layout Red。
        /// </summary>
        private static void BuildCoverageEvidence(
            GasRuntimeV1LayoutProofPayload payload,
            List<GasProofFailure> failures)
        {
            if (payload.CoverageGaps == GasLayoutProofCoverageGap.None)
                return;
            failures.Add(Failure(GasProofFailureKind.ProofCoverageIncomplete,
                GasProofRuleIds.CapacityProofMissing, "LayoutCoverage", -1, default,
                expected: 0, actual: (long)(uint)payload.CoverageGaps,
                operation: "==", leftName: "CoverageGaps",
                left: (long)(uint)payload.CoverageGaps,
                rightName: "None", right: 0, unit: "flags"));
        }

        /// <summary>
        /// 排序失败并计算 Attribute、Tag、Layout 与含 provenance 的最终 ProofHash。
        /// </summary>
        private static void FinalizeHashes(
            GasRuntimeV1LayoutProofPayload payload,
            List<GasProofFailure> failures)
        {
            failures.Sort(GasProofCanonicalRules.CompareFailures);
            payload.Failures = failures.ToArray();
            if (!TryMeasurePayloadEncoding(payload, out var budget))
            {
                AddPayloadEncodingBudgetFailure(in budget, failures);
                failures.Sort(GasProofCanonicalRules.CompareFailures);
                payload.Failures = failures.ToArray();
                payload.AttributeLayoutHash = string.Empty;
                payload.TagCatalogHash = string.Empty;
                payload.LayoutHash = string.Empty;
                payload.ProofHash = string.Empty;
                return;
            }
            payload.AttributeLayoutHash = ComputeAttributeHash(payload);
            payload.TagCatalogHash = ComputeTagHash(payload);
            payload.LayoutHash = ComputeLayoutHash(payload);
            if (!GasProofCanonicalHashWriter.IsSha256Hex(payload.AttributeLayoutHash) ||
                !GasProofCanonicalHashWriter.IsSha256Hex(payload.TagCatalogHash) ||
                !GasProofCanonicalHashWriter.IsSha256Hex(payload.LayoutHash))
            {
                payload.ProofHash = string.Empty;
                return;
            }
            if (!TryMeasurePayloadEncoding(payload, out budget))
            {
                AddPayloadEncodingBudgetFailure(in budget, failures);
                failures.Sort(GasProofCanonicalRules.CompareFailures);
                payload.Failures = failures.ToArray();
                payload.ProofHash = string.Empty;
                return;
            }
            payload.ProofHash = ComputeProofHash(payload);
        }

        /// <summary>
        /// 判断校验编码必需的全部数组是否存在。
        /// </summary>
        private static bool HasCompletePayloadArrays(GasRuntimeV1LayoutProofPayload payload)
        {
            if (payload == null ||
                payload.Attributes == null ||
                payload.Tags == null ||
                payload.AncestorIndices == null ||
                payload.TagQueryPrograms == null ||
                payload.RequirementTagIndices == null ||
                payload.BlobRanges == null ||
                payload.Slots == null ||
                payload.Failures == null ||
                payload.Slots.Length != FixedSlotCount)
                return false;
            return IsPayloadEncodingBudgetSafe(payload);
        }

        /// <summary>
        /// 在 canonical 重编码前限制所有顶层数组，避免篡改 payload 触发无界 hash 工作集。
        /// </summary>
        private static bool IsPayloadEncodingBudgetSafe(
            GasRuntimeV1LayoutProofPayload payload)
        {
            return TryMeasurePayloadEncoding(payload, out _);
        }

        /// <summary>
        /// 先校验数组工作集，再按 canonical 顺序累计真实 UTF-8 与 related-key 预算。
        /// </summary>
        private static bool TryMeasurePayloadEncoding(
            GasRuntimeV1LayoutProofPayload payload,
            out GasProofEncodingBudget budget)
        {
            budget = new GasProofEncodingBudget();
            if (!ArePayloadCollectionsSafe(payload))
                return false;
            if (!TryAddLayoutHeaderStrings(payload, ref budget) ||
                !budget.TryAddString(payload.AttributeLayoutHash) ||
                !budget.TryAddString(payload.TagCatalogHash) ||
                !budget.TryAddString(payload.LayoutHash) ||
                !budget.TryAddString(payload.ProofHash))
                return false;
            return TryAddLayoutEntries(payload, ref budget);
        }

        /// <summary>
        /// 以短路顺序校验全部顶层 collection，失败后不再遍历其余数组。
        /// </summary>
        private static bool ArePayloadCollectionsSafe(
            GasRuntimeV1LayoutProofPayload payload)
        {
            var entryCount = 0L;
            var byteCount = 0L;
            return TryAccumulatePayloadEntries(payload.Attributes.Length,
                       ref entryCount, ref byteCount) &&
                   TryAccumulatePayloadEntries(payload.Tags.Length,
                       ref entryCount, ref byteCount) &&
                   TryAccumulatePayloadEntries(payload.AncestorIndices.Length,
                       ref entryCount, ref byteCount) &&
                   TryAccumulatePayloadEntries(payload.TagQueryPrograms.Length,
                       ref entryCount, ref byteCount) &&
                   TryAccumulatePayloadEntries(payload.RequirementTagIndices.Length,
                       ref entryCount, ref byteCount) &&
                   TryAccumulatePayloadEntries(payload.BlobRanges.Length,
                       ref entryCount, ref byteCount) &&
                   TryAccumulatePayloadEntries(payload.Slots.Length,
                       ref entryCount, ref byteCount) &&
                   TryAccumulatePayloadEntries(payload.Failures.Length,
                       ref entryCount, ref byteCount);
        }

        /// <summary>
        /// 累加 Layout entries 的真实字符串与 provenance，任何失败立即停止。
        /// </summary>
        private static bool TryAddLayoutEntries(
            GasRuntimeV1LayoutProofPayload payload,
            ref GasProofEncodingBudget budget)
        {
            foreach (var entry in payload.Attributes)
                if (!budget.TryAddProvenance(in entry.Provenance))
                    return false;
            foreach (var entry in payload.Tags)
                if (!budget.TryAddProvenance(in entry.Provenance))
                    return false;
            foreach (var entry in payload.TagQueryPrograms)
                if (!budget.TryAddProvenance(in entry.Provenance))
                    return false;
            foreach (var entry in payload.BlobRanges)
                if (!budget.TryAddString(entry.DefinitionKey) ||
                    !budget.TryAddProvenance(in entry.Provenance))
                    return false;
            return TryAddSlotAndFailureStrings(payload, ref budget);
        }

        /// <summary>
        /// 累加 slot 契约与 failure 尾部，防止 verifier 在 hash 前扫描超预算文本。
        /// </summary>
        private static bool TryAddSlotAndFailureStrings(
            GasRuntimeV1LayoutProofPayload payload,
            ref GasProofEncodingBudget budget)
        {
            foreach (var entry in payload.Slots)
            {
                if (!budget.TryAddString(entry.RuntimeTypeName) ||
                    !budget.TryAddString(entry.RevisionContractId) ||
                    !budget.TryAddString(entry.RebuildContractId) ||
                    !budget.TryAddString(entry.RevisionOwner) ||
                    !budget.TryAddString(entry.RebuildRule))
                    return false;
            }
            foreach (var failure in payload.Failures)
                if (!budget.TryAddFailure(in failure))
                    return false;
            return true;
        }

        /// <summary>
        /// 为最终 payload 实际编码超门补充结构化 budget Red。
        /// </summary>
        private static void AddPayloadEncodingBudgetFailure(
            in GasProofEncodingBudget budget,
            List<GasProofFailure> failures)
        {
            var related = budget.FailedOnRelatedDefinitions;
            var actual = budget.FailureActual > 0
                ? budget.FailureActual
                : (related
                    ? GasRuntimeV1ProofInventory.MaximumProofBuildEntryCount + 1
                    : GasRuntimeV1ProofInventory.MaximumProofBuildBytes + 1);
            AddBuildBudgetFailure(
                related ? "LayoutProofRelatedDefinitionCount" : "LayoutProofCanonicalBytes",
                actual,
                related
                    ? GasRuntimeV1ProofInventory.MaximumProofBuildEntryCount
                    : GasRuntimeV1ProofInventory.MaximumProofBuildBytes,
                related ? "related-definitions" : "bytes", failures);
        }

        /// <summary>
        /// 累加一个 payload 数组的保守编码预算，任何 count、add 或 multiply 越界都拒绝 hash。
        /// </summary>
        private static bool TryAccumulatePayloadEntries(
            int count,
            ref long entryCount,
            ref long byteCount)
        {
            if (count > GasRuntimeV1ProofInventory.MaximumProofCollectionCount ||
                !GasCheckedProofMath.TryAdd(entryCount, count, out var nextEntryCount) ||
                nextEntryCount > GasRuntimeV1ProofInventory.MaximumProofBuildEntryCount ||
                !GasCheckedProofMath.TryMultiply(count,
                    ConservativePayloadEntryEncodingBytes, out var collectionBytes) ||
                !GasCheckedProofMath.TryAdd(byteCount, collectionBytes, out var nextByteCount) ||
                nextByteCount > GasRuntimeV1ProofInventory.MaximumProofBuildBytes)
                return false;
            entryCount = nextEntryCount;
            byteCount = nextByteCount;
            return true;
        }

        /// <summary>
        /// 比较一项 payload hash，并在失配时保留两侧文本。
        /// </summary>
        private static bool VerifyHash(
            string actual,
            string expected,
            string dimensionId,
            out GasProofFailure failure)
        {
            if (GasProofCanonicalHashWriter.IsSha256Hex(actual) &&
                GasProofCanonicalHashWriter.IsSha256Hex(expected) &&
                string.Equals(actual, expected, StringComparison.Ordinal))
            {
                failure = default;
                return true;
            }
            failure = IntegrityFailure(dimensionId, expected, actual);
            return false;
        }

        /// <summary>
        /// 构造一条不丢失 expected/actual hash 的稳定完整性 Red。
        /// </summary>
        private static GasProofFailure IntegrityFailure(
            string dimensionId,
            string expected,
            string actual)
        {
            return new GasProofFailure
            {
                ProofKind = GasProofKind.Layout,
                FailureKind = GasProofFailureKind.ProofHashMismatch,
                RuleId = GasProofRuleIds.NonCanonicalIdentity,
                DimensionId = dimensionId,
                Operator = "==",
                LeftOperandName = "ExpectedHash",
                RightOperandName = "ActualHash",
                ExpectedText = expected ?? string.Empty,
                ActualText = actual ?? string.Empty,
                Unit = "sha256",
                CanonicalOrdinal = -1,
            };
        }

        /// <summary>
        /// 计算不含 provenance 的 AttributeLayout 子哈希。
        /// </summary>
        private static string ComputeAttributeHash(GasRuntimeV1LayoutProofPayload payload)
        {
            using (var writer = new GasProofCanonicalHashWriter())
            {
                writer.WriteString("gas-runtime-v1-attribute-layout/1");
                writer.WriteInt32(payload.Attributes.Length);
                foreach (var entry in payload.Attributes)
                {
                    writer.WriteInt32(entry.AttributeId);
                    writer.WriteInt32(entry.LayoutIndex);
                    writer.WriteInt32(entry.CanonicalOrdinal);
                }
                return writer.ComputeHash();
            }
        }

        /// <summary>
        /// 计算不含 provenance 的 TagCatalog、ancestor 与 query program 子哈希。
        /// </summary>
        private static string ComputeTagHash(GasRuntimeV1LayoutProofPayload payload)
        {
            using (var writer = new GasProofCanonicalHashWriter())
            {
                writer.WriteString("gas-runtime-v1-tag-catalog/1");
                WriteTagContent(writer, payload);
                return writer.ComputeHash();
            }
        }

        /// <summary>
        /// 写入 TagCatalog、ancestor 与 query program 的 gameplay identity 字段。
        /// </summary>
        private static void WriteTagContent(
            GasProofCanonicalHashWriter writer,
            GasRuntimeV1LayoutProofPayload payload)
        {
            writer.WriteInt32(payload.Tags.Length);
            foreach (var entry in payload.Tags)
            {
                writer.WriteInt32(entry.TagId);
                writer.WriteInt32(entry.TagIndex);
                writer.WriteInt32(entry.CanonicalOrdinal);
                writer.WriteInt32(entry.AncestorStart);
                writer.WriteInt32(entry.AncestorCount);
            }
            WriteIntArray(writer, payload.AncestorIndices);
            writer.WriteInt32(payload.TagQueryPrograms.Length);
            foreach (var entry in payload.TagQueryPrograms)
            {
                writer.WriteInt32(entry.RequirementId);
                writer.WriteInt32(entry.CanonicalOrdinal);
                writer.WriteByte((byte)entry.Phase);
                writer.WriteByte((byte)entry.Match);
                writer.WriteInt32(entry.TagIndexStart);
                writer.WriteInt32(entry.TagIndexCount);
            }
            WriteIntArray(writer, payload.RequirementTagIndices);
        }

        /// <summary>
        /// 计算绑定排序规则、ranges、slot ABI 与两个子哈希的物理 LayoutHash；不含 graph/provenance。
        /// </summary>
        private static string ComputeLayoutHash(GasRuntimeV1LayoutProofPayload payload)
        {
            using (var writer = new GasProofCanonicalHashWriter())
            {
                writer.WriteString("gas-runtime-v1-layout/2");
                writer.WriteInt32(payload.ProofSchemaVersion);
                writer.WriteString(payload.AlgorithmVersion);
                writer.WriteString(payload.AttributeSortRule);
                writer.WriteString(payload.TagSortRule);
                writer.WriteString(payload.BlobRangeSortRule);
                writer.WriteString(payload.AttributeLayoutHash);
                writer.WriteString(payload.TagCatalogHash);
                WriteRanges(writer, payload.BlobRanges, false);
                WriteSlots(writer, payload.Slots);
                return writer.ComputeHash();
            }
        }

        /// <summary>
        /// 计算含 generator、provenance 与稳定 Red 的最终 ProofHash。
        /// </summary>
        private static string ComputeProofHash(GasRuntimeV1LayoutProofPayload payload)
        {
            using (var writer = new GasProofCanonicalHashWriter())
            {
                writer.WriteString("gas-runtime-v1-layout-proof-payload/2");
                writer.WriteString(payload.LayoutHash);
                writer.WriteInt32(payload.GraphSchemaVersion);
                writer.WriteString(payload.CanonicalGraphHash);
                writer.WriteString(payload.GeneratorVersion);
                writer.WriteUInt32((uint)payload.CoverageGaps);
                writer.WriteByte((byte)payload.SlotAbiEvidenceStatus);
                WriteAttributeProvenance(writer, payload.Attributes);
                WriteTagProvenance(writer, payload.Tags, payload.TagQueryPrograms);
                WriteRanges(writer, payload.BlobRanges, true);
                writer.WriteInt32(payload.Failures.Length);
                foreach (var failure in payload.Failures)
                    writer.WriteFailure(in failure);
                return writer.ComputeHash();
            }
        }

        /// <summary>
        /// 写入 Attribute provenance 表。
        /// </summary>
        private static void WriteAttributeProvenance(
            GasProofCanonicalHashWriter writer,
            GasLayoutAttributeProofEntry[] entries)
        {
            writer.WriteInt32(entries.Length);
            foreach (var entry in entries)
                writer.WriteProvenance(in entry.Provenance);
        }

        /// <summary>
        /// 写入 Tag 与 query program provenance 表。
        /// </summary>
        private static void WriteTagProvenance(
            GasProofCanonicalHashWriter writer,
            GasLayoutTagProofEntry[] tags,
            GasLayoutTagQueryProofEntry[] queries)
        {
            writer.WriteInt32(tags.Length);
            foreach (var entry in tags)
                writer.WriteProvenance(in entry.Provenance);
            writer.WriteInt32(queries.Length);
            foreach (var entry in queries)
                writer.WriteProvenance(in entry.Provenance);
        }

        /// <summary>
        /// 写入 Blob range identity，并按需包含 provenance。
        /// </summary>
        private static void WriteRanges(
            GasProofCanonicalHashWriter writer,
            GasLayoutRangeProofEntry[] entries,
            bool includeProvenance)
        {
            writer.WriteInt32(entries.Length);
            foreach (var entry in entries)
            {
                writer.WriteByte((byte)entry.RangeKind);
                writer.WriteString(entry.DefinitionKey);
                writer.WriteInt32(entry.DefinitionId);
                writer.WriteInt32(entry.CanonicalOrdinal);
                writer.WriteInt32(entry.Start);
                writer.WriteInt32(entry.Count);
                writer.WriteInt32(entry.BackingLength);
                if (includeProvenance)
                    writer.WriteProvenance(in entry.Provenance);
            }
        }

        /// <summary>
        /// 写入 Runtime slot ABI 与重建契约。
        /// </summary>
        private static void WriteSlots(
            GasProofCanonicalHashWriter writer,
            GasLayoutSlotProofEntry[] entries)
        {
            writer.WriteInt32(entries.Length);
            foreach (var entry in entries)
            {
                writer.WriteByte((byte)entry.SlotKind);
                writer.WriteString(entry.RuntimeTypeName);
                writer.WriteInt64(entry.ElementCount);
                writer.WriteInt32(entry.ElementSizeBytes);
                writer.WriteInt32(entry.AlignmentBytes);
                writer.WriteInt64(entry.CheckedByteCount);
                writer.WriteString(entry.RevisionContractId);
                writer.WriteString(entry.RebuildContractId);
                writer.WriteString(entry.RevisionOwner);
                writer.WriteInt32(entry.WordBitCount);
                writer.WriteString(entry.RebuildRule);
            }
        }

        /// <summary>
        /// 写入 canonical int array。
        /// </summary>
        private static void WriteIntArray(GasProofCanonicalHashWriter writer, int[] values)
        {
            writer.WriteInt32(values.Length);
            foreach (var value in values)
                writer.WriteInt32(value);
        }

        /// <summary>
        /// 验证一个 Tag ancestor range 是否严格递增、位于 domain 内并包含自身一次。
        /// </summary>
        private static bool IsCanonicalAncestorRange(
            int tagIndex,
            in GasLayoutTagProofEntry tag,
            int[] ancestors,
            int tagCount)
        {
            var previous = -1;
            var containsSelf = false;
            for (var offset = 0; offset < tag.AncestorCount; offset++)
            {
                var ancestor = ancestors[tag.AncestorStart + offset];
                if (ancestor <= previous || ancestor >= tagCount)
                    return false;
                containsSelf |= ancestor == tagIndex;
                previous = ancestor;
            }
            return containsSelf;
        }

        /// <summary>
        /// 验证 ancestor 反向环与传递闭包均未被 canonical range 遗漏。
        /// </summary>
        private static bool IsAncestorClosureValid(
            int tagIndex,
            GasLayoutTagProofEntry[] tags,
            int[] ancestors)
        {
            var range = tags[tagIndex];
            for (var offset = 0; offset < range.AncestorCount; offset++)
            {
                var ancestorIndex = ancestors[range.AncestorStart + offset];
                if (ancestorIndex < 0 || ancestorIndex >= tags.Length)
                    return false;
                var ancestorRange = tags[ancestorIndex];
                if (!IsRangeInBounds(ancestorRange.AncestorStart,
                        ancestorRange.AncestorCount, ancestors.Length))
                    return false;
                if (ancestorIndex != tagIndex && Contains(
                        ancestors, ancestorRange.AncestorStart,
                        ancestorRange.AncestorCount, tagIndex))
                    return false;
                for (var nested = 0; nested < ancestorRange.AncestorCount; nested++)
                {
                    var transitive = ancestors[ancestorRange.AncestorStart + nested];
                    if (!Contains(ancestors, range.AncestorStart, range.AncestorCount, transitive))
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 在已排序 flat range 中查找一个 dense index。
        /// </summary>
        private static bool Contains(int[] values, int start, int count, int expected)
        {
            for (var offset = 0; offset < count; offset++)
            {
                var value = values[start + offset];
                if (value == expected)
                    return true;
                if (value > expected)
                    return false;
            }
            return false;
        }

        /// <summary>
        /// 验证 flat range 严格递增且每项处于指定 dense domain。
        /// </summary>
        private static bool IsStrictlyIncreasingRange(
            int[] values,
            int start,
            int count,
            int domainCount)
        {
            var previous = -1;
            for (var offset = 0; offset < count; offset++)
            {
                var value = values[start + offset];
                if (value <= previous || value >= domainCount)
                    return false;
                previous = value;
            }
            return true;
        }

        /// <summary>
        /// 以 checked add 验证半开区间，区分数值溢出与普通越界。
        /// </summary>
        private static void ValidateRange(
            int start,
            int count,
            int backingLength,
            string dimensionId,
            int definitionId,
            int ordinal,
            in GasProofProvenance provenance,
            List<GasProofFailure> failures)
        {
            if (start < 0 || count < 0 || backingLength < 0)
            {
                failures.Add(Failure(GasProofFailureKind.RangeInvalid,
                    GasProofRuleIds.InvalidDomainOrReference, dimensionId, ordinal,
                    provenance, definitionId, backingLength, count));
                return;
            }
            if (!GasCheckedProofMath.TryAdd(start, count, out var end) || end > int.MaxValue)
            {
                failures.Add(Failure(GasProofFailureKind.RangeArithmeticOverflow,
                    GasProofRuleIds.InvalidDomainOrReference, dimensionId, ordinal,
                    provenance, definitionId, int.MaxValue, end,
                    "+", "Start", start, "Count", count));
                return;
            }
            if (end > backingLength)
                failures.Add(Failure(GasProofFailureKind.RangeInvalid,
                    GasProofRuleIds.InvalidDomainOrReference, dimensionId, ordinal,
                    provenance, definitionId, backingLength, end));
        }

        /// <summary>
        /// 判断一个已校验为非负的半开区间是否处于 backing array 内。
        /// </summary>
        private static bool IsRangeInBounds(int start, int count, int backingLength)
        {
            return start >= 0 && count >= 0 && backingLength >= 0 &&
                   (long)start + count <= backingLength;
        }

        /// <summary>
        /// 验证 provenance 并输出定位到 Definition/ordinal 的稳定 Red。
        /// </summary>
        private static void ValidateProvenance(
            in GasProofProvenance provenance,
            string dimensionId,
            int definitionId,
            int ordinal,
            List<GasProofFailure> failures)
        {
            if (GasProofCanonicalRules.IsCanonicalProvenance(in provenance))
                return;
            failures.Add(Failure(GasProofFailureKind.ProvenanceMissing,
                GasProofRuleIds.NonCanonicalIdentity, dimensionId, ordinal,
                provenance, definitionId));
        }

        /// <summary>
        /// 在任何 source-sized 读取或分配前校验六类 count、累计条目与保守字节预算。
        /// </summary>
        private static bool TryValidateBuildBudget(
            in LayoutSourceCounts counts,
            List<GasProofFailure> failures)
        {
            var entryCount = 0L;
            var byteCount = 0L;
            var valid = true;
            valid &= TryReserveSourceCollection(counts.AttributeCount,
                "AttributeCount", ref entryCount, ref byteCount, failures);
            valid &= TryReserveSourceCollection(counts.TagCount,
                "TagCount", ref entryCount, ref byteCount, failures);
            valid &= TryReserveSourceCollection(counts.AncestorIndexCount,
                "AncestorIndexCount", ref entryCount, ref byteCount, failures);
            valid &= TryReserveSourceCollection(counts.TagQueryProgramCount,
                "TagQueryProgramCount", ref entryCount, ref byteCount, failures);
            valid &= TryReserveSourceCollection(counts.RequirementTagIndexCount,
                "RequirementTagIndexCount", ref entryCount, ref byteCount, failures);
            valid &= TryReserveSourceCollection(counts.BlobRangeCount,
                "BlobRangeCount", ref entryCount, ref byteCount, failures);
            return valid;
        }

        /// <summary>
        /// 为一个 source collection 预留固定保守成本，失败时保留精确 count 或 byte 操作数。
        /// </summary>
        private static bool TryReserveSourceCollection(
            int count,
            string dimensionId,
            ref long entryCount,
            ref long byteCount,
            List<GasProofFailure> failures)
        {
            if (count < 0)
            {
                failures.Add(Failure(GasProofFailureKind.CapacityBoundInvalid,
                    GasProofRuleIds.CapacityProofMissing, dimensionId, -1, default,
                    expected: 0, actual: count));
                return false;
            }
            if (count > GasRuntimeV1ProofInventory.MaximumProofCollectionCount)
                return AddBuildBudgetFailure(dimensionId, count,
                    GasRuntimeV1ProofInventory.MaximumProofCollectionCount,
                    "elements", failures);
            if (!GasCheckedProofMath.TryAdd(entryCount, count, out var nextEntryCount))
                return AddBuildBudgetFailure("LayoutProofBuildEntryCount",
                    long.MaxValue, GasRuntimeV1ProofInventory.MaximumProofBuildEntryCount,
                    "elements", failures);
            if (nextEntryCount > GasRuntimeV1ProofInventory.MaximumProofBuildEntryCount)
                return AddBuildBudgetFailure("LayoutProofBuildEntryCount",
                    nextEntryCount, GasRuntimeV1ProofInventory.MaximumProofBuildEntryCount,
                    "elements", failures);
            if (!GasCheckedProofMath.TryMultiply(count,
                    ConservativeSourceEntryBuildBytes, out var collectionBytes))
                return AddBuildBudgetFailure("LayoutProofBuildBytes", long.MaxValue,
                    GasRuntimeV1ProofInventory.MaximumProofBuildBytes, "bytes", failures);
            if (!GasCheckedProofMath.TryAdd(byteCount, collectionBytes, out var nextByteCount))
                return AddBuildBudgetFailure("LayoutProofBuildBytes", long.MaxValue,
                    GasRuntimeV1ProofInventory.MaximumProofBuildBytes, "bytes", failures);
            if (nextByteCount > GasRuntimeV1ProofInventory.MaximumProofBuildBytes)
                return AddBuildBudgetFailure("LayoutProofBuildBytes", nextByteCount,
                    GasRuntimeV1ProofInventory.MaximumProofBuildBytes, "bytes", failures);
            entryCount = nextEntryCount;
            byteCount = nextByteCount;
            return true;
        }

        /// <summary>
        /// 添加一条稳定 proof 构建预算 Red，并返回 false 供门禁直接组合。
        /// </summary>
        private static bool AddBuildBudgetFailure(
            string dimensionId,
            long actual,
            long maximum,
            string unit,
            List<GasProofFailure> failures)
        {
            failures.Add(Failure(GasProofFailureKind.ProofBuildBudgetExceeded,
                GasProofRuleIds.CapacityProofMissing, dimensionId, -1, default,
                expected: maximum, actual: actual, operation: "<=",
                leftName: "Actual", left: actual, rightName: "Maximum", right: maximum,
                unit: unit));
            return false;
        }

        /// <summary>
        /// 创建一条带算术操作数和 authoring provenance 的 Layout Red。
        /// </summary>
        private static GasProofFailure Failure(
            GasProofFailureKind kind,
            string ruleId,
            string dimensionId,
            int ordinal,
            in GasProofProvenance provenance,
            int definitionId = 0,
            long expected = 0,
            long actual = 0,
            string operation = "",
            string leftName = "",
            long left = 0,
            string rightName = "",
            long right = 0,
            string expectedText = "",
            string actualText = "",
            string unit = "elements")
        {
            return new GasProofFailure
            {
                ProofKind = GasProofKind.Layout,
                FailureKind = kind,
                RuleId = ruleId,
                DimensionId = dimensionId,
                DerivationId = string.IsNullOrEmpty(operation) ? string.Empty : dimensionId + ".checked",
                Operator = operation,
                LeftOperandName = leftName,
                LeftOperandValue = left,
                RightOperandName = rightName,
                RightOperandValue = right,
                ExpectedMaximum = expected,
                ActualValue = actual,
                ExpectedText = expectedText,
                ActualText = actualText,
                Unit = unit,
                DefinitionId = definitionId,
                DefinitionKey = FirstRelatedDefinitionKey(in provenance),
                CanonicalOrdinal = ordinal,
                Provenance = provenance,
            };
        }

        /// <summary>
        /// 从 proof provenance 取出稳定的首个 opaque DefinitionKey。
        /// </summary>
        private static string FirstRelatedDefinitionKey(in GasProofProvenance provenance)
        {
            var related = provenance.RelatedDefinitionKeys;
            return related == null || related.Length == 0 ? string.Empty : related[0];
        }

        /// <summary>
        /// 一次性冻结 source 的六类 count，防止预检与实际分配之间重新读取可变 getter。
        /// </summary>
        private readonly struct LayoutSourceCounts
        {
            public readonly int AttributeCount;
            public readonly int TagCount;
            public readonly int AncestorIndexCount;
            public readonly int TagQueryProgramCount;
            public readonly int RequirementTagIndexCount;
            public readonly int BlobRangeCount;

            /// <summary>
            /// 按固定顺序读取全部 source count，后续 reader 只能消费此快照。
            /// </summary>
            public LayoutSourceCounts(IGasLayoutProofSource source)
            {
                AttributeCount = source.AttributeCount;
                TagCount = source.TagCount;
                AncestorIndexCount = source.AncestorIndexCount;
                TagQueryProgramCount = source.TagQueryProgramCount;
                RequirementTagIndexCount = source.RequirementTagIndexCount;
                BlobRangeCount = source.BlobRangeCount;
            }
        }

        /// <summary>
        /// 表示从最小 graph adapter 读取一个 canonical int index 的函数。
        /// </summary>
        private delegate bool TryReadIndex(int ordinal, out int value);
    }
}
