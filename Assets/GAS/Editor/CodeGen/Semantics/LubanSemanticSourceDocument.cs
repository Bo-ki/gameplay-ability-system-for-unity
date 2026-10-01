using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GAS.Editor.CodeGen.Semantics
{
    /// <summary>
    /// 冻结同一次 Luban parse 交给 projector 的 v2 source document；不得由 JSON、generated C# 或 reflection 二次回推。
    /// </summary>
    public sealed class LubanSemanticSourceDocument
    {
        private readonly ReadOnlyCollection<LubanSemanticSourceDefinition> _definitions;

        /// <summary>
        /// 创建含 source hash、toolchain identity 与完整 typed tree 的不可变 parse 边界。
        /// </summary>
        public LubanSemanticSourceDocument(
            CanonicalSemanticToolchainIdentity toolchainIdentity,
            string generatorVersion,
            string sourceInputHash,
            IReadOnlyList<LubanSemanticSourceDefinition> definitions)
        {
            _definitions = LubanSemanticSourceFreeze.Copy(definitions, false);
            CanonicalSemanticResourceBudget.ValidateSourceParts(
                toolchainIdentity,
                generatorVersion,
                sourceInputHash,
                _definitions);
            ToolchainIdentity = toolchainIdentity;
            GeneratorVersion = generatorVersion;
            SourceInputHash = sourceInputHash;
        }

        public ushort SourceContractVersion => CanonicalSemanticVersions.LubanSourceContractVersion;

        public ushort ResourceBudgetVersion => CanonicalSemanticVersions.ResourceBudgetVersion;

        public string ResourceBudgetId => CanonicalSemanticVersions.ResourceBudgetId;

        public CanonicalSemanticToolchainIdentity ToolchainIdentity { get; }

        public string GeneratorVersion { get; }

        public string SourceInputHash { get; }

        public IReadOnlyList<LubanSemanticSourceDefinition> Definitions => _definitions;
    }

    /// <summary>
    /// 使用稳定 target domain、DefinitionKind 与复合 ID 表示未分配 canonical ordinal 的 source reference。
    /// </summary>
    public sealed class LubanSemanticSourceDefinitionId
    {
        private readonly ReadOnlyCollection<long> _stableIdParts;

        /// <summary>
        /// 创建显式声明 target domain 的 source Definition id，projector 会拒绝 domain/kind 不一致。
        /// </summary>
        public LubanSemanticSourceDefinitionId(
            string stableDomainId,
            CanonicalDefinitionKind definitionKind,
            IReadOnlyList<long> stableIdParts)
        {
            CanonicalSemanticResourceBudget.RequireString(stableDomainId, nameof(stableDomainId));
            StableDomainId = stableDomainId;
            DefinitionKind = definitionKind;
            _stableIdParts = LubanSemanticSourceFreeze.Copy(stableIdParts);
        }

        public string StableDomainId { get; }

        public CanonicalDefinitionKind DefinitionKind { get; }

        public IReadOnlyList<long> StableIdParts => _stableIdParts;
    }

    /// <summary>
    /// 保存 source tree 每个字段或元素的稳定来源、原始/规范值、RuleId 与关联目标。
    /// </summary>
    public sealed class LubanSemanticSourceProvenance
    {
        private readonly ReadOnlyCollection<LubanSemanticSourceDefinitionId> _relatedDefinitions;

        /// <summary>
        /// 创建 same-parse 阶段的逐字段 provenance，禁止用绝对路径代替 workbook/table identity。
        /// </summary>
        public LubanSemanticSourceProvenance(
            string workbookId,
            string tableId,
            string rowStableId,
            string fieldPath,
            string rawValue,
            string normalizedValue,
            string ruleId,
            uint ruleVersion,
            string generatorVersion,
            IReadOnlyList<LubanSemanticSourceDefinitionId> relatedDefinitions)
        {
            ValidateTexts(workbookId, tableId, rowStableId, fieldPath,
                rawValue, normalizedValue, ruleId, generatorVersion);
            WorkbookId = workbookId;
            TableId = tableId;
            RowStableId = rowStableId;
            FieldPath = fieldPath;
            RawValue = rawValue;
            NormalizedValue = normalizedValue;
            RuleId = ruleId;
            RuleVersion = ruleVersion;
            GeneratorVersion = generatorVersion;
            _relatedDefinitions = LubanSemanticSourceFreeze.Copy(relatedDefinitions);
        }

        /// <summary>
        /// 在 provenance 字符串冻结前校验每个实际 strict UTF-8 byte count。
        /// </summary>
        private static void ValidateTexts(
            string workbookId,
            string tableId,
            string rowStableId,
            string fieldPath,
            string rawValue,
            string normalizedValue,
            string ruleId,
            string generatorVersion)
        {
            CanonicalSemanticResourceBudget.RequireString(workbookId, "source workbook");
            CanonicalSemanticResourceBudget.RequireString(tableId, "source table");
            CanonicalSemanticResourceBudget.RequireString(rowStableId, "source row");
            CanonicalSemanticResourceBudget.RequireString(fieldPath, "source field path");
            CanonicalSemanticResourceBudget.RequireString(rawValue, "source raw value");
            CanonicalSemanticResourceBudget.RequireString(normalizedValue, "source normalized value");
            CanonicalSemanticResourceBudget.RequireString(ruleId, "source rule id");
            CanonicalSemanticResourceBudget.RequireString(generatorVersion, "source generator");
        }

        public string WorkbookId { get; }

        public string TableId { get; }

        public string RowStableId { get; }

        public string FieldPath { get; }

        public string RawValue { get; }

        public string NormalizedValue { get; }

        public string RuleId { get; }

        public uint RuleVersion { get; }

        public string GeneratorVersion { get; }

        public IReadOnlyList<LubanSemanticSourceDefinitionId> RelatedDefinitions => _relatedDefinitions;
    }

    /// <summary>
    /// 冻结一个真实 Luban row；program、dependency 与 role 均不接受调用方输入，只能由 projector 编译。
    /// </summary>
    public sealed class LubanSemanticSourceDefinition
    {
        private readonly ReadOnlyCollection<LubanSemanticSourceField> _fields;

        /// <summary>
        /// 创建只有 authoring identity、字段树与 provenance 的 source row。
        /// </summary>
        public LubanSemanticSourceDefinition(
            LubanSemanticSourceDefinitionId definitionId,
            LubanSemanticSourceProvenance provenance,
            IReadOnlyList<LubanSemanticSourceField> fields)
        {
            DefinitionId = definitionId;
            Provenance = provenance;
            _fields = LubanSemanticSourceFreeze.Copy(fields);
        }

        public LubanSemanticSourceDefinitionId DefinitionId { get; }

        public LubanSemanticSourceProvenance Provenance { get; }

        public IReadOnlyList<LubanSemanticSourceField> Fields => _fields;
    }

    /// <summary>
    /// 将一个 registry FieldId 绑定到完整 source semantic tree；字段缺失也必须由 Absent root 显式表达。
    /// </summary>
    public sealed class LubanSemanticSourceField
    {
        /// <summary>
        /// 创建一个由固定 registry 裁决的 source field。
        /// </summary>
        public LubanSemanticSourceField(string fieldId, LubanSemanticSourceNode root)
        {
            CanonicalSemanticResourceBudget.RequireString(fieldId, nameof(fieldId));
            FieldId = fieldId;
            Root = root;
        }

        public string FieldId { get; }

        public LubanSemanticSourceNode Root { get; }
    }

    /// <summary>
    /// 表达 optional、record、variant 与完整 collection 的 source tree 节点，每个节点都有独立 provenance。
    /// </summary>
    public sealed class LubanSemanticSourceNode
    {
        private readonly ReadOnlyCollection<LubanSemanticSourceNode> _children;

        /// <summary>
        /// 创建冻结 source 节点；调用方使用具名工厂避免伪造不完整 union。
        /// </summary>
        private LubanSemanticSourceNode(
            string nodeId,
            CanonicalSemanticNodeKind nodeKind,
            string typeId,
            string targetDomainId,
            CanonicalCollectionSemantics collectionSemantics,
            LubanSemanticSourceValue scalarValue,
            IReadOnlyList<LubanSemanticSourceNode> children,
            LubanSemanticSourceProvenance provenance)
        {
            CanonicalSemanticResourceBudget.RequireString(nodeId, nameof(nodeId));
            CanonicalSemanticResourceBudget.RequireString(typeId, nameof(typeId));
            CanonicalSemanticResourceBudget.RequireString(targetDomainId, nameof(targetDomainId));
            NodeId = nodeId;
            NodeKind = nodeKind;
            TypeId = typeId;
            TargetDomainId = targetDomainId;
            CollectionSemantics = collectionSemantics;
            ScalarValue = scalarValue;
            _children = LubanSemanticSourceFreeze.Copy(children);
            Provenance = provenance;
        }

        public string NodeId { get; }

        public CanonicalSemanticNodeKind NodeKind { get; }

        public string TypeId { get; }

        public string TargetDomainId { get; }

        public CanonicalCollectionSemantics CollectionSemantics { get; }

        public LubanSemanticSourceValue ScalarValue { get; }

        public IReadOnlyList<LubanSemanticSourceNode> Children => _children;

        public LubanSemanticSourceProvenance Provenance { get; }

        /// <summary>
        /// 创建显式 absent 节点，使 optional absent 与 present-zero 使用不同编码。
        /// </summary>
        public static LubanSemanticSourceNode Absent(
            string nodeId,
            string typeId,
            string targetDomainId,
            LubanSemanticSourceProvenance provenance)
        {
            return new LubanSemanticSourceNode(
                nodeId,
                CanonicalSemanticNodeKind.Absent,
                typeId,
                targetDomainId,
                CanonicalCollectionSemantics.None,
                null,
                null,
                provenance);
        }

        /// <summary>
        /// 创建 primitive、enum 或显式 target-domain reference 的 scalar 节点。
        /// </summary>
        public static LubanSemanticSourceNode Scalar(
            string nodeId,
            string typeId,
            string targetDomainId,
            LubanSemanticSourceValue value,
            LubanSemanticSourceProvenance provenance)
        {
            return new LubanSemanticSourceNode(
                nodeId,
                CanonicalSemanticNodeKind.Scalar,
                typeId,
                targetDomainId,
                CanonicalCollectionSemantics.None,
                value,
                null,
                provenance);
        }

        /// <summary>
        /// 创建字段成员由 registry 排序和穷尽校验的 record 节点。
        /// </summary>
        public static LubanSemanticSourceNode Record(
            string nodeId,
            string typeId,
            IReadOnlyList<LubanSemanticSourceNode> members,
            LubanSemanticSourceProvenance provenance)
        {
            return new LubanSemanticSourceNode(
                nodeId,
                CanonicalSemanticNodeKind.Record,
                typeId,
                string.Empty,
                CanonicalCollectionSemantics.None,
                null,
                members,
                provenance);
        }

        /// <summary>
        /// 创建带稳定 discriminator type id 的多态节点，成员必须匹配该 variant 的 payload schema。
        /// </summary>
        public static LubanSemanticSourceNode Variant(
            string nodeId,
            string variantTypeId,
            IReadOnlyList<LubanSemanticSourceNode> members,
            LubanSemanticSourceProvenance provenance)
        {
            return new LubanSemanticSourceNode(
                nodeId,
                CanonicalSemanticNodeKind.Variant,
                variantTypeId,
                string.Empty,
                CanonicalCollectionSemantics.None,
                null,
                members,
                provenance);
        }

        /// <summary>
        /// 创建显式 ordered/set/multiset 的完整集合，任何元素都不得由 FirstPositive 裁剪。
        /// </summary>
        public static LubanSemanticSourceNode Collection(
            string nodeId,
            string typeId,
            string targetDomainId,
            CanonicalCollectionSemantics semantics,
            IReadOnlyList<LubanSemanticSourceNode> elements,
            LubanSemanticSourceProvenance provenance)
        {
            return new LubanSemanticSourceNode(
                nodeId,
                CanonicalSemanticNodeKind.Collection,
                typeId,
                targetDomainId,
                semantics,
                null,
                elements,
                provenance);
        }
    }

    /// <summary>
    /// 保存 source scalar 的闭集 typed value；record、variant 与 collection 由 source node 表达而非塞入 object。
    /// </summary>
    public sealed class LubanSemanticSourceValue
    {
        private readonly byte[] _bytes;

        /// <summary>
        /// 创建内部冻结 scalar union；公开调用方只能使用具名工厂。
        /// </summary>
        private LubanSemanticSourceValue(
            CanonicalSemanticValueKind kind,
            ulong rawBits = 0,
            string text = null,
            byte[] bytes = null,
            DateTimeKind dateTimeKind = DateTimeKind.Unspecified,
            CanonicalEnumUnderlyingType enumUnderlyingType = 0,
            LubanSemanticSourceDefinitionId definitionReference = null)
        {
            if (text != null)
                CanonicalSemanticResourceBudget.RequireString(text, nameof(text));
            if (bytes != null)
                CanonicalSemanticResourceBudget.RequirePayloadLength(bytes.Length, nameof(bytes));
            Kind = kind;
            RawBits = rawBits;
            Text = text;
            _bytes = bytes == null ? null : (byte[])bytes.Clone();
            DateTimeKind = dateTimeKind;
            EnumUnderlyingType = enumUnderlyingType;
            DefinitionReference = definitionReference;
        }

        public CanonicalSemanticValueKind Kind { get; }

        public ulong RawBits { get; }

        public string Text { get; }

        public DateTimeKind DateTimeKind { get; }

        public CanonicalEnumUnderlyingType EnumUnderlyingType { get; }

        public LubanSemanticSourceDefinitionId DefinitionReference { get; }

        public byte[] ByteValue => _bytes == null ? null : (byte[])_bytes.Clone();

        internal byte[] ByteValueUnsafe => _bytes;

        /// <summary>
        /// 创建显式 null scalar；optional 缺失仍必须使用 Absent node。
        /// </summary>
        public static LubanSemanticSourceValue Null()
        {
            return new LubanSemanticSourceValue(CanonicalSemanticValueKind.Null);
        }

        /// <summary>
        /// 创建 Boolean scalar。
        /// </summary>
        public static LubanSemanticSourceValue Boolean(bool value)
        {
            return new LubanSemanticSourceValue(CanonicalSemanticValueKind.Boolean, value ? 1UL : 0UL);
        }

        /// <summary>
        /// 创建 Int32 scalar。
        /// </summary>
        public static LubanSemanticSourceValue Int32(int value)
        {
            return new LubanSemanticSourceValue(CanonicalSemanticValueKind.Int32, unchecked((uint)value));
        }

        /// <summary>
        /// 创建 UInt32 scalar。
        /// </summary>
        public static LubanSemanticSourceValue UInt32(uint value)
        {
            return new LubanSemanticSourceValue(CanonicalSemanticValueKind.UInt32, value);
        }

        /// <summary>
        /// 创建 Int64 scalar。
        /// </summary>
        public static LubanSemanticSourceValue Int64(long value)
        {
            return new LubanSemanticSourceValue(CanonicalSemanticValueKind.Int64, unchecked((ulong)value));
        }

        /// <summary>
        /// 创建 UInt64 scalar。
        /// </summary>
        public static LubanSemanticSourceValue UInt64(ulong value)
        {
            return new LubanSemanticSourceValue(CanonicalSemanticValueKind.UInt64, value);
        }

        /// <summary>
        /// 从 IEEE 754 原始位创建 Float32 scalar。
        /// </summary>
        public static LubanSemanticSourceValue Float32Bits(uint bits)
        {
            return new LubanSemanticSourceValue(CanonicalSemanticValueKind.Float32, bits);
        }

        /// <summary>
        /// 从 IEEE 754 原始位创建 Float64 scalar。
        /// </summary>
        public static LubanSemanticSourceValue Float64Bits(ulong bits)
        {
            return new LubanSemanticSourceValue(CanonicalSemanticValueKind.Float64, bits);
        }

        /// <summary>
        /// 创建非 null String scalar。
        /// </summary>
        public static LubanSemanticSourceValue String(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return new LubanSemanticSourceValue(CanonicalSemanticValueKind.String, text: value);
        }

        /// <summary>
        /// 创建防御复制后的 Bytes scalar。
        /// </summary>
        public static LubanSemanticSourceValue Bytes(byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return new LubanSemanticSourceValue(CanonicalSemanticValueKind.Bytes, bytes: value);
        }

        /// <summary>
        /// 创建 ticks 与 kind 明确的 DateTime scalar。
        /// </summary>
        public static LubanSemanticSourceValue DateTime(DateTime value)
        {
            return new LubanSemanticSourceValue(
                CanonicalSemanticValueKind.DateTime,
                unchecked((ulong)value.Ticks),
                dateTimeKind: value.Kind);
        }

        /// <summary>
        /// 创建稳定 enum type id、底层宽度与整数位明确的 enum scalar。
        /// </summary>
        public static LubanSemanticSourceValue Enum(
            string enumTypeId,
            CanonicalEnumUnderlyingType underlyingType,
            ulong rawBits)
        {
            return new LubanSemanticSourceValue(
                CanonicalSemanticValueKind.Enum,
                rawBits,
                enumTypeId,
                enumUnderlyingType: underlyingType);
        }

        /// <summary>
        /// 创建 target domain 已由 DefinitionId 明确声明的 reference scalar。
        /// </summary>
        public static LubanSemanticSourceValue Reference(LubanSemanticSourceDefinitionId definitionId)
        {
            if (definitionId == null)
                throw new ArgumentNullException(nameof(definitionId));
            return new LubanSemanticSourceValue(
                CanonicalSemanticValueKind.DefinitionReference,
                definitionReference: definitionId);
        }
    }

    /// <summary>
    /// 为 source DTO 集中提供防御复制，避免可变输入集合泄漏到冻结协议。
    /// </summary>
    internal static class LubanSemanticSourceFreeze
    {
        /// <summary>
        /// 将任意只读列表复制为独立只读集合并保留 same-parse 顺序。
        /// </summary>
        public static ReadOnlyCollection<T> Copy<T>(
            IReadOnlyList<T> values,
            bool nullAsEmpty = true)
        {
            var copy = CanonicalSemanticResourceBudget.SnapshotList(
                values,
                "source freeze copy",
                nullAsEmpty);
            return Array.AsReadOnly(copy);
        }
    }
}
