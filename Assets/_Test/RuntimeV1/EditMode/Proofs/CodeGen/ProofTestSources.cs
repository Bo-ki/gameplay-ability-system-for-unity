using System;
using System.Collections.Generic;
using GAS.Editor.CodeGen.Proofs;
using GAS.Runtime;

namespace GAS.Editor.Tests.CodeGen.Proofs
{
    /// <summary>
    /// 提供不复制 canonical graph DTO 的最小 LayoutProof 测试 adapter。
    /// </summary>
    internal sealed class TestLayoutProofSource : IGasLayoutProofSource
    {
        private readonly GasLayoutAttributeProofEntry[] _attributes;
        private readonly GasLayoutTagProofEntry[] _tags;
        private readonly int[] _ancestors;
        private readonly GasLayoutTagQueryProofEntry[] _queries;
        private readonly int[] _requirementTags;
        private readonly GasLayoutRangeProofEntry[] _ranges;

        public int GraphSchemaVersion { get; set; } = 1;
        public string GeneratorVersion { get; set; } = "proof-tests/1";
        public string CanonicalGraphHash { get; set; } = new string('a', 64);
        public int? AttributeCountOverride { get; set; }
        public int? TagCountOverride { get; set; }
        public int? AncestorIndexCountOverride { get; set; }
        public int? TagQueryProgramCountOverride { get; set; }
        public int? RequirementTagIndexCountOverride { get; set; }
        public int? BlobRangeCountOverride { get; set; }
        public int BlobRangeReadCount { get; private set; }
        public int AttributeCount => AttributeCountOverride ?? _attributes.Length;
        public int TagCount => TagCountOverride ?? _tags.Length;
        public int AncestorIndexCount => AncestorIndexCountOverride ?? _ancestors.Length;
        public int TagQueryProgramCount => TagQueryProgramCountOverride ?? _queries.Length;
        public int RequirementTagIndexCount =>
            RequirementTagIndexCountOverride ?? _requirementTags.Length;
        public int BlobRangeCount => BlobRangeCountOverride ?? _ranges.Length;

        /// <summary>
        /// 构造一个字段均可独立修改的最小 LayoutProof source。
        /// </summary>
        private TestLayoutProofSource(
            GasLayoutAttributeProofEntry[] attributes,
            GasLayoutTagProofEntry[] tags,
            int[] ancestors,
            GasLayoutTagQueryProofEntry[] queries,
            int[] requirementTags,
            GasLayoutRangeProofEntry[] ranges)
        {
            _attributes = attributes;
            _tags = tags;
            _ancestors = ancestors;
            _queries = queries;
            _requirementTags = requirementTags;
            _ranges = ranges;
        }

        /// <summary>
        /// 创建每个 Tag ancestor range 仅包含自身的合法离散层级。
        /// </summary>
        public static TestLayoutProofSource CreateValid(
            int tagCount = 2,
            int queryCount = 1)
        {
            var attributes = new[]
            {
                new GasLayoutAttributeProofEntry
                {
                    AttributeId = 101,
                    LayoutIndex = 0,
                    CanonicalOrdinal = 0,
                    Provenance = ProofTestData.Provenance(0, 101, "Attributes[101]"),
                },
                new GasLayoutAttributeProofEntry
                {
                    AttributeId = 205,
                    LayoutIndex = 1,
                    CanonicalOrdinal = 1,
                    Provenance = ProofTestData.Provenance(1, 205, "Attributes[205]"),
                },
            };
            var tags = new GasLayoutTagProofEntry[tagCount];
            var ancestors = new int[tagCount];
            for (var ordinal = 0; ordinal < tagCount; ordinal++)
            {
                tags[ordinal] = new GasLayoutTagProofEntry
                {
                    TagId = 1000 + ordinal,
                    TagIndex = ordinal,
                    CanonicalOrdinal = ordinal,
                    AncestorStart = ordinal,
                    AncestorCount = 1,
                    Provenance = ProofTestData.Provenance(100 + ordinal, 9001,
                        "Tags[" + ordinal + "]"),
                };
                ancestors[ordinal] = ordinal;
            }
            var queries = new GasLayoutTagQueryProofEntry[queryCount];
            var requirementTags = new int[queryCount];
            for (var ordinal = 0; ordinal < queryCount; ordinal++)
            {
                queries[ordinal] = new GasLayoutTagQueryProofEntry
                {
                    RequirementId = 3001 + ordinal,
                    CanonicalOrdinal = ordinal,
                    Phase = GasRequirementPhase.Application,
                    Match = GasTagRequirementMatch.All,
                    TagIndexStart = ordinal,
                    TagIndexCount = 1,
                    Provenance = ProofTestData.Provenance(400 + ordinal, 9001,
                        "Requirements[" + (3001 + ordinal) + "]"),
                };
                requirementTags[ordinal] = ordinal % tagCount;
            }
            var ranges = new[]
            {
                new GasLayoutRangeProofEntry
                {
                    RangeKind = GasCatalogRangeKind.ApplicationRequirement,
                    DefinitionKey = "runtime/gameplay-effect/9001",
                    DefinitionId = 9001,
                    CanonicalOrdinal = 0,
                    Start = 0,
                    Count = 1,
                    BackingLength = 1,
                    Provenance = ProofTestData.Provenance(500, 9001,
                        "Definitions[9001].ApplicationRequirementRange"),
                },
            };
            return new TestLayoutProofSource(attributes, tags, ancestors, queries,
                requirementTags, ranges);
        }

        /// <summary>
        /// 创建保留最小 Attribute/Tag/query 的自定义 range source。
        /// </summary>
        public static TestLayoutProofSource CreateWithRanges(
            GasLayoutRangeProofEntry[] ranges)
        {
            var source = CreateValid();
            return new TestLayoutProofSource(source._attributes, source._tags,
                source._ancestors, source._queries, source._requirementTags, ranges);
        }

        /// <summary>
        /// 按 canonical ordinal 读取 Attribute projection。
        /// </summary>
        public bool TryGetAttribute(int ordinal, out GasLayoutAttributeProofEntry entry)
        {
            return TryGet(_attributes, ordinal, out entry);
        }

        /// <summary>
        /// 按 canonical ordinal 读取 Tag projection。
        /// </summary>
        public bool TryGetTag(int ordinal, out GasLayoutTagProofEntry entry)
        {
            return TryGet(_tags, ordinal, out entry);
        }

        /// <summary>
        /// 读取 ancestor flat index。
        /// </summary>
        public bool TryGetAncestorIndex(int ordinal, out int tagIndex)
        {
            return TryGet(_ancestors, ordinal, out tagIndex);
        }

        /// <summary>
        /// 按 canonical ordinal 读取 TagQueryProgram projection。
        /// </summary>
        public bool TryGetTagQueryProgram(
            int ordinal,
            out GasLayoutTagQueryProofEntry entry)
        {
            return TryGet(_queries, ordinal, out entry);
        }

        /// <summary>
        /// 读取 requirement tag flat index。
        /// </summary>
        public bool TryGetRequirementTagIndex(int ordinal, out int tagIndex)
        {
            return TryGet(_requirementTags, ordinal, out tagIndex);
        }

        /// <summary>
        /// 按 canonical ordinal 读取 Blob range projection。
        /// </summary>
        public bool TryGetBlobRange(int ordinal, out GasLayoutRangeProofEntry entry)
        {
            BlobRangeReadCount++;
            return TryGet(_ranges, ordinal, out entry);
        }

        /// <summary>
        /// 返回可直接修改的 Attribute projection，供 hash/provenance 最小差异测试使用。
        /// </summary>
        public GasLayoutAttributeProofEntry ReadAttribute(int ordinal)
        {
            return _attributes[ordinal];
        }

        /// <summary>
        /// 覆盖一条 Attribute projection，保留其余 graph facts 不变。
        /// </summary>
        public void WriteAttribute(int ordinal, in GasLayoutAttributeProofEntry entry)
        {
            _attributes[ordinal] = entry;
        }

        /// <summary>
        /// 返回可直接修改的 Tag projection，供 Red 测试构造最小差异。
        /// </summary>
        public GasLayoutTagProofEntry ReadTag(int ordinal)
        {
            return _tags[ordinal];
        }

        /// <summary>
        /// 覆盖一条 Tag projection，保留其余 graph facts 不变。
        /// </summary>
        public void WriteTag(int ordinal, in GasLayoutTagProofEntry entry)
        {
            _tags[ordinal] = entry;
        }

        /// <summary>
        /// 返回一条可修改的 TagQueryProgram projection。
        /// </summary>
        public GasLayoutTagQueryProofEntry ReadQuery(int ordinal)
        {
            return _queries[ordinal];
        }

        /// <summary>
        /// 覆盖一条 TagQueryProgram projection，供 RequirementId 唯一性测试使用。
        /// </summary>
        public void WriteQuery(int ordinal, in GasLayoutTagQueryProofEntry entry)
        {
            _queries[ordinal] = entry;
        }

        /// <summary>
        /// 返回可直接修改的 Blob range projection。
        /// </summary>
        public GasLayoutRangeProofEntry ReadRange(int ordinal)
        {
            return _ranges[ordinal];
        }

        /// <summary>
        /// 覆盖一条 Blob range projection，保留其余 graph facts 不变。
        /// </summary>
        public void WriteRange(int ordinal, in GasLayoutRangeProofEntry entry)
        {
            _ranges[ordinal] = entry;
        }

        /// <summary>
        /// 从数组安全读取一个测试 projection。
        /// </summary>
        private static bool TryGet<T>(T[] values, int ordinal, out T value)
        {
            if (ordinal < 0 || ordinal >= values.Length)
            {
                value = default;
                return false;
            }
            value = values[ordinal];
            return true;
        }
    }

    /// <summary>
    /// 提供逐 dimension 显式零上界的最小 CapacityProof 测试 adapter。
    /// </summary>
    internal sealed class TestCapacityProofSource : IGasCapacityProofSource
    {
        private const int DimensionCount = (int)GasCapacityDimension.DynamicDependencyBackEdges;
        private readonly long[] _bounds = new long[DimensionCount + 1];
        private readonly bool[] _present = new bool[DimensionCount + 1];

        public int GraphSchemaVersion { get; set; } = 1;
        public string GeneratorVersion { get; set; } = "proof-tests/1";
        public string CanonicalGraphHash { get; set; } = new string('a', 64);
        public string ContractMatrixHash { get; set; } = new string('b', 64);
        public string TargetScaleId { get; set; } = "unit";
        public GasScaleProfile ScaleProfile { get; set; }
        public GasProofProvenance ScaleProfileProvenance { get; set; }
        public long DeclaredMemoryBudgetBytes { get; set; } = 1024L * 1024L * 1024L;
        public GasProofProvenance MemoryBudgetProvenance { get; set; }
        public GasProofProvenance DefinitionIdentityProvenance { get; set; }
        public GasProofProvenance? BoundProvenanceOverride { get; set; }
        public int DefinitionIdentityReadCount { get; private set; }
        public int BoundReadCount { get; private set; }
        public int DefinitionCountOverride { get; set; } = 1;
        public int DefinitionCount => DefinitionCountOverride;

        /// <summary>
        /// 创建所有必需 dimension 都以显式零投影的合法 source。
        /// </summary>
        public TestCapacityProofSource()
        {
            for (var dimension = 1; dimension <= DimensionCount; dimension++)
                _present[dimension] = true;
            ScaleProfile = ProofTestData.CreateProfile(8);
            ScaleProfileProvenance = ProofTestData.Provenance(800, 9001, "ScaleProfiles[1]");
            MemoryBudgetProvenance = ProofTestData.Provenance(801, 9001,
                "ScaleProfiles[1].MemoryBudgetBytes");
            DefinitionIdentityProvenance = ProofTestData.Provenance(900, 9001,
                "Definitions[9001]");
        }

        /// <summary>
        /// 读取唯一测试 Definition 的 identity。
        /// </summary>
        public bool TryGetDefinitionIdentity(
            int definitionOrdinal,
            out GasProofDefinitionIdentity identity,
            out GasProofProvenance provenance)
        {
            DefinitionIdentityReadCount++;
            var definitionId = 9001 + definitionOrdinal;
            identity = new GasProofDefinitionIdentity(
                "runtime/gameplay-effect/" + definitionId, definitionId);
            provenance = definitionOrdinal == 0
                ? DefinitionIdentityProvenance
                : ProofTestData.Provenance(900 + definitionOrdinal, definitionId,
                    "Definitions[" + definitionId + "]");
            return definitionOrdinal >= 0 && definitionOrdinal < DefinitionCount;
        }

        /// <summary>
        /// 读取一个显式 bound 及其字段 provenance。
        /// </summary>
        public bool TryGetBound(
            int definitionOrdinal,
            GasCapacityDimension dimension,
            out long maximum,
            out GasProofProvenance provenance)
        {
            BoundReadCount++;
            var raw = (int)dimension;
            var definitionId = 9001 + definitionOrdinal;
            maximum = raw >= 0 && raw < _bounds.Length ? _bounds[raw] : 0;
            provenance = BoundProvenanceOverride ?? ProofTestData.Provenance(
                1000 + raw, definitionId,
                "Definitions[" + definitionId + "]." + dimension);
            return definitionOrdinal >= 0 && definitionOrdinal < DefinitionCount &&
                   raw > 0 && raw < _present.Length && _present[raw];
        }

        /// <summary>
        /// 设置一个显式 semantic upper bound。
        /// </summary>
        public void SetBound(GasCapacityDimension dimension, long maximum)
        {
            _bounds[(int)dimension] = maximum;
            _present[(int)dimension] = true;
        }

        /// <summary>
        /// 移除一个 dimension，用于验证 missing-bound Red。
        /// </summary>
        public void RemoveBound(GasCapacityDimension dimension)
        {
            _present[(int)dimension] = false;
        }
    }

    /// <summary>
    /// 集中创建 proof 测试使用的稳定 provenance 与 ScaleProfile，不依赖 Demo 运行时代码。
    /// </summary>
    internal static class ProofTestData
    {
        /// <summary>
        /// 创建路径无关且 opaque DefinitionKey 已排序的完整 provenance。
        /// </summary>
        public static GasProofProvenance Provenance(
            int ordinal,
            int definitionId,
            string fieldPath)
        {
            return new GasProofProvenance(
                ordinal,
                "workbook://proof-tests",
                "ProofTable",
                "row-" + ordinal,
                fieldPath,
                "raw-" + ordinal,
                "normalized-" + ordinal,
                GasProofRuleIds.CapacityProofMissing,
                "1",
                "proof-tests/1",
                definitionId > 0
                    ? new[] { "runtime/gameplay-effect/" + definitionId }
                    : Array.Empty<string>());
        }

        /// <summary>
        /// 复制 provenance identity，仅替换 RawValue 以构造实际 UTF-8 累计预算夹具。
        /// </summary>
        public static GasProofProvenance WithRawValue(
            in GasProofProvenance provenance,
            string rawValue)
        {
            return new GasProofProvenance(
                provenance.ProvenanceOrdinal,
                provenance.WorkbookId,
                provenance.TableId,
                provenance.RowStableId,
                provenance.FieldPath,
                rawValue,
                provenance.NormalizedValue,
                provenance.RuleId,
                provenance.RuleVersion,
                provenance.GeneratorVersion,
                provenance.RelatedDefinitionKeys);
        }

        /// <summary>
        /// 按当前 AutoChess 公式创建指定 ASC 数量的完整非负 ScaleProfile。
        /// </summary>
        public static GasScaleProfile CreateProfile(int ascCount)
        {
            var capacity = Math.Max(ascCount, 1);
            return new GasScaleProfile
            {
                ProfileId = 1,
                ProfileVersion = 1,
                ProfileHash = 1,
                MaxFixedTicksPerBatch = 1,
                MaximumDeltaTimeTicks = 1,
                MaxSpawnBatchSize = capacity,
                MaxBattleInstanceCount = 1,
                MaxAscRegistryCount = capacity,
                MaxBoundaryCommandCount = checked(capacity * 4),
                MaxBoundaryCommandPayloadCount = checked(capacity * 8),
                MaxOwnerPlanCount = checked(capacity * 4),
                MaxResolvedTargetCount = checked(capacity * 4),
                MaxEffectOperationCount = checked(capacity * 4),
                MaxOwnerReservationCount = checked(capacity * 4),
                MaxTargetReservationCount = checked(capacity * 4),
                MaxCoreFactCount = checked(capacity * 16),
                MaxNextTickRouteCount = checked(capacity * 4),
                MaxStructuralIntentCount = checked(capacity * 4),
                MaxSessionBoundaryFactCount = checked(capacity * 16),
                MaxAscBoundaryFactCount = checked(capacity * 16),
                MaxPendingAttributeInitializationCount = checked(capacity * 2),
                MaxPendingTagInitializationCount = 0,
                MaxPendingGrantedAbilityInitializationCount = checked(capacity * 3),
                MaxGrantedAbilityCount = checked(capacity * 3),
                MaxAbilityActivationCount = checked(capacity * 2),
                MaxAbilityContinuationCount = checked(capacity * 2),
                MaxAbilitySubscriptionCount = checked(capacity * 2),
                MaxCooldownGateCount = checked(capacity * 2),
                MaxActivationOwnedContributionCount = checked(capacity * 2),
                MaxEmittedApplicationRefCount = checked(capacity * 2),
                MaxActiveEffectCount = checked(capacity * 4),
                MaxPayloadRangeRecordCount = checked(capacity * 4),
                MaxPayloadValueCount = checked(capacity * 4),
                MaxAttributeAggregatorCount = checked(capacity * 2),
                MaxLiveDependencyCount = checked(capacity * 2),
                MaxLiveDependencyRouteCount = checked(capacity * 2),
                MaxPendingCommandCount = checked(capacity * 4),
            };
        }

        /// <summary>
        /// 构建与测试 graph identity 一致的成功 LayoutProof。
        /// </summary>
        public static GasRuntimeV1LayoutProofPayload BuildLayout(int tagCount = 2)
        {
            return GasRuntimeV1LayoutProofBuilder.Build(TestLayoutProofSource.CreateValid(tagCount));
        }
    }
}
