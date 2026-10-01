using System;
using System.Collections.Generic;

namespace GAS.Editor.CodeGen.Semantics
{
    /// <summary>
    /// 将同一次 Luban parse 的 v2 typed tree 投影并 sealed compile 为唯一 canonical graph。
    /// </summary>
    public static class CanonicalSemanticProjector
    {
        /// <summary>
        /// 验证 source protocol、分配 tree ordinal，并由 compiler 推导 role/program/dependency。
        /// </summary>
        public static CanonicalNormalizedSemanticGraph Project(LubanSemanticSourceDocument source)
        {
            CanonicalSemanticResourceBudget.ValidateSource(source);
            ValidateDocument(source);
            var sorted = new List<LubanSemanticSourceDefinition>(source.Definitions);
            sorted.Sort(CompareSourceDefinitions);
            var drafts = new SemanticDefinitionDraft[sorted.Count];
            var nextOrdinalByDomain = new Dictionary<int, int>();
            for (var index = 0; index < sorted.Count; index++)
            {
                var key = ToCanonicalKey(sorted[index]?.DefinitionId);
                RejectDuplicateDefinition(sorted, index, key, source);
                if (!nextOrdinalByDomain.TryGetValue(key.DomainOrdinal, out var ordinal))
                    ordinal = 0;
                drafts[index] = ProjectDefinition(sorted[index], key, ordinal, source);
                nextOrdinalByDomain[key.DomainOrdinal] = ordinal + 1;
            }

            var definitions = CanonicalSemanticCompiler.Compile(drafts, source.GeneratorVersion);
            return CanonicalNormalizedSemanticGraph.Create(
                source.ToolchainIdentity,
                source.GeneratorVersion,
                source.SourceInputHash,
                CanonicalSemanticCoverage.CreateRed(),
                CanonicalSemanticRuleCatalog.CreateDefaultRegistry(source.GeneratorVersion),
                definitions);
        }

        /// <summary>
        /// 验证 source 根身份、toolchain hashes 与非空 definitions。
        /// </summary>
        private static void ValidateDocument(LubanSemanticSourceDocument source)
        {
            if (source == null)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Luban source document is required.");
            var toolchain = source.ToolchainIdentity;
            if (toolchain == null)
                Fail(CanonicalSemanticRuleCatalog.NonCanonicalIdentity, "Structured toolchain identity is required.");
            RequireText(toolchain.LubanProductVersion, "LubanProductVersion", null);
            RequireText(toolchain.GeneratorIdentity, "GeneratorIdentity", null);
            RequireText(source.GeneratorVersion, "GeneratorVersion", null);
            CanonicalSemanticIdentity.RequireHash(toolchain.LubanBinaryHash, nameof(toolchain.LubanBinaryHash));
            CanonicalSemanticIdentity.RequireHash(toolchain.SchemaInputHash, nameof(toolchain.SchemaInputHash));
            CanonicalSemanticIdentity.RequireHash(source.SourceInputHash, nameof(source.SourceInputHash));
            if (!string.Equals(toolchain.GeneratorIdentity, source.GeneratorVersion, StringComparison.Ordinal))
                Fail(CanonicalSemanticRuleCatalog.NonCanonicalIdentity, "Generator identity does not match document version.");
            if (source.Definitions.Count == 0)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "At least one source definition is required.");
        }

        /// <summary>
        /// 按 registry DomainOrdinal 与复合 stable id 比较 source rows。
        /// </summary>
        private static int CompareSourceDefinitions(
            LubanSemanticSourceDefinition left,
            LubanSemanticSourceDefinition right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            return ToCanonicalKey(left.DefinitionId).CompareTo(ToCanonicalKey(right.DefinitionId));
        }

        /// <summary>
        /// 拒绝重复 DefinitionKey 并在 CFG1001 evidence 中保留双方 provenance。
        /// </summary>
        private static void RejectDuplicateDefinition(
            IReadOnlyList<LubanSemanticSourceDefinition> sorted,
            int index,
            CanonicalDefinitionKey key,
            LubanSemanticSourceDocument source)
        {
            if (index == 0 || !key.HasSameIdentity(ToCanonicalKey(sorted[index - 1].DefinitionId)))
                return;
            var left = ProjectProvenance(sorted[index - 1].Provenance, source, null);
            var right = ProjectProvenance(sorted[index].Provenance, source, null);
            FailConflict("Duplicate Definition owners.", left, right);
        }

        /// <summary>
        /// 将显式 stable domain source id 映射为固定 DomainOrdinal key。
        /// </summary>
        internal static CanonicalDefinitionKey ToCanonicalKey(LubanSemanticSourceDefinitionId sourceId)
        {
            if (sourceId == null)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Source reference domain/kind is invalid.");
            if (!CanonicalSemanticSchemaRegistry.TryResolve(sourceId.StableDomainId, out var schema)
                || schema.DefinitionKind != sourceId.DefinitionKind)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Source reference domain/kind is invalid.");

            if (sourceId.StableIdParts.Count != schema.StableIdPartCount)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Source stable id arity is invalid.");
            return new CanonicalDefinitionKey(schema.DomainOrdinal, schema.DefinitionKind, sourceId.StableIdParts);
        }

        /// <summary>
        /// 投影一行的完整字段树并建立 sealed compiler draft。
        /// </summary>
        private static SemanticDefinitionDraft ProjectDefinition(
            LubanSemanticSourceDefinition sourceDefinition,
            CanonicalDefinitionKey key,
            int definitionOrdinal,
            LubanSemanticSourceDocument source)
        {
            if (sourceDefinition == null || sourceDefinition.Provenance == null)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Source definition/provenance is required.");
            CanonicalSemanticSchemaRegistry.TryResolve(key.DefinitionKind, out var schema);
            var provenance = ProjectProvenance(sourceDefinition.Provenance, source, null);
            if (!string.Equals(provenance.TableId, schema.StableDomainId, StringComparison.Ordinal))
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Source table does not match domain.", provenance);
            var fields = ProjectFields(sourceDefinition, schema, source);
            ValidateStableKey(fields, key, provenance);
            return new SemanticDefinitionDraft(definitionOrdinal, key, fields, provenance);
        }

        /// <summary>
        /// 拒绝字段缺失、重复、未知或 root id 不一致，并按 FieldOrdinal 投影。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticField> ProjectFields(
            LubanSemanticSourceDefinition definition,
            CanonicalSemanticDomainSchema schema,
            LubanSemanticSourceDocument document)
        {
            var indexed = IndexFields(definition, schema, document);
            var result = new CanonicalSemanticField[schema.Fields.Count];
            for (var index = 0; index < schema.Fields.Count; index++)
            {
                var fieldSchema = schema.Fields[index];
                if (!indexed.TryGetValue(fieldSchema.FieldId, out var sourceField))
                    Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Registered source field is missing.");
                var root = ProjectNode(sourceField.Root, fieldSchema.Root, document, 0);
                result[index] = new CanonicalSemanticField(
                    fieldSchema.FieldOrdinal,
                    fieldSchema.FieldId,
                    fieldSchema.Classification,
                    fieldSchema.Disposition,
                    root,
                    root.Provenance);
            }

            return result;
        }

        /// <summary>
        /// 建立 source FieldId 唯一索引，并将未知字段稳定 default-deny。
        /// </summary>
        private static Dictionary<string, LubanSemanticSourceField> IndexFields(
            LubanSemanticSourceDefinition definition,
            CanonicalSemanticDomainSchema schema,
            LubanSemanticSourceDocument document)
        {
            var result = new Dictionary<string, LubanSemanticSourceField>(StringComparer.Ordinal);
            for (var index = 0; index < definition.Fields.Count; index++)
            {
                var field = definition.Fields[index];
                if (field == null || field.Root == null || string.IsNullOrWhiteSpace(field.FieldId))
                    Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Source field/root is required.");
                var provenance = ProjectProvenance(field.Root.Provenance, document, null);
                if (!schema.TryResolveField(field.FieldId, out _))
                    Fail(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Unregistered source field is denied.", provenance);
                if (!string.Equals(field.FieldId, field.Root.NodeId, StringComparison.Ordinal))
                    Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "FieldId/root NodeId mismatch.", provenance);
                if (result.TryGetValue(field.FieldId, out var existing))
                    FailConflict("Duplicate source field owners.", ProjectProvenance(existing.Root.Provenance, document, null), provenance);
                result.Add(field.FieldId, field);
            }

            return result;
        }

        /// <summary>
        /// 按 recursive registry schema 投影 absent/scalar/record/variant/collection 节点。
        /// </summary>
        private static CanonicalSemanticNode ProjectNode(
            LubanSemanticSourceNode source,
            CanonicalSemanticNodeSchema schema,
            LubanSemanticSourceDocument document,
            int depth)
        {
            if (source == null || depth > CanonicalBinaryReader.MaxValueDepth)
                Fail(CanonicalSemanticRuleCatalog.NonCanonicalIdentity, "Source tree is null or too deep.");
            var provenance = ProjectProvenance(source.Provenance, document, schema);
            ValidateNodeHeader(source, schema, provenance);
            if (source.NodeKind == CanonicalSemanticNodeKind.Absent)
                return ProjectAbsent(source, schema, provenance);
            if (source.NodeKind == CanonicalSemanticNodeKind.Scalar)
                return ProjectScalarNode(source, schema, provenance);
            if (source.NodeKind == CanonicalSemanticNodeKind.Collection)
                return ProjectCollection(source, schema, document, provenance, depth);
            return ProjectStructuredNode(source, schema, document, provenance, depth);
        }

        /// <summary>
        /// 校验 node id/type/target/collection semantics 与 schema 完全一致。
        /// </summary>
        private static void ValidateNodeHeader(
            LubanSemanticSourceNode source,
            CanonicalSemanticNodeSchema schema,
            CanonicalSemanticProvenance provenance)
        {
            if (!string.Equals(source.NodeId, schema.NodeId, StringComparison.Ordinal))
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Source NodeId does not match schema.", provenance);
            if (source.NodeKind != CanonicalSemanticNodeKind.Absent && source.NodeKind != schema.NodeKind)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Source node kind does not match schema.", provenance);
            if (schema.NodeKind != CanonicalSemanticNodeKind.Variant
                && !string.Equals(source.TypeId, schema.TypeId, StringComparison.Ordinal))
            {
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Source type id does not match schema.", provenance);
            }
            if (!string.Equals(source.TargetDomainId ?? string.Empty, schema.TargetDomainId ?? string.Empty, StringComparison.Ordinal))
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Source target domain does not match schema.", provenance);
            if (source.NodeKind == CanonicalSemanticNodeKind.Collection
                && source.CollectionSemantics != schema.CollectionSemantics)
            {
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Collection semantics do not match schema.", provenance);
            }
        }

        /// <summary>
        /// 投影 optional absent 并拒绝 required 字段或携带隐藏 value/children 的 absent。
        /// </summary>
        private static CanonicalSemanticNode ProjectAbsent(
            LubanSemanticSourceNode source,
            CanonicalSemanticNodeSchema schema,
            CanonicalSemanticProvenance provenance)
        {
            if (schema.PresencePolicy != CanonicalPresencePolicy.Optional
                || source.ScalarValue != null
                || source.Children.Count != 0)
            {
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Invalid absent node.", provenance);
            }

            return new CanonicalSemanticNode(
                schema.NodeOrdinal,
                schema.NodeId,
                CanonicalSemanticNodeKind.Absent,
                source.TypeId,
                source.TargetDomainId ?? string.Empty,
                CanonicalCollectionSemantics.None,
                null,
                null,
                provenance);
        }

        /// <summary>
        /// 投影 primitive/enum/reference scalar 并拒绝任何隐藏 child。
        /// </summary>
        private static CanonicalSemanticNode ProjectScalarNode(
            LubanSemanticSourceNode source,
            CanonicalSemanticNodeSchema schema,
            CanonicalSemanticProvenance provenance)
        {
            if (source.ScalarValue == null || source.Children.Count != 0)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Scalar node shape is invalid.", provenance);
            var value = ProjectScalarValue(source.ScalarValue, schema, provenance);
            return new CanonicalSemanticNode(
                schema.NodeOrdinal,
                schema.NodeId,
                CanonicalSemanticNodeKind.Scalar,
                source.TypeId,
                source.TargetDomainId ?? string.Empty,
                CanonicalCollectionSemantics.None,
                value,
                null,
                provenance);
        }

        /// <summary>
        /// 投影 record 或 variant，并按 schema member ordinal 重排全部字段。
        /// </summary>
        private static CanonicalSemanticNode ProjectStructuredNode(
            LubanSemanticSourceNode source,
            CanonicalSemanticNodeSchema schema,
            LubanSemanticSourceDocument document,
            CanonicalSemanticProvenance provenance,
            int depth)
        {
            var members = schema.NodeKind == CanonicalSemanticNodeKind.Variant
                ? ResolveVariant(source, schema, provenance).Members
                : schema.Children;
            var projected = ProjectMembers(source.Children, members, document, depth + 1);
            return new CanonicalSemanticNode(
                schema.NodeOrdinal,
                schema.NodeId,
                schema.NodeKind,
                source.TypeId,
                string.Empty,
                CanonicalCollectionSemantics.None,
                null,
                projected,
                provenance);
        }

        /// <summary>
        /// 解析固定 variant discriminator；未知多态 case 稳定 CFG1002。
        /// </summary>
        private static CanonicalSemanticVariantSchema ResolveVariant(
            LubanSemanticSourceNode source,
            CanonicalSemanticNodeSchema schema,
            CanonicalSemanticProvenance provenance)
        {
            for (var index = 0; index < schema.Variants.Count; index++)
            {
                if (string.Equals(source.TypeId, schema.Variants[index].VariantTypeId, StringComparison.Ordinal))
                    return schema.Variants[index];
            }

            Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Unknown variant discriminator.", provenance);
            return null;
        }

        /// <summary>
        /// 投影 record/variant 的穷尽成员并拒绝未知、缺失或重复 member owner。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticNode> ProjectMembers(
            IReadOnlyList<LubanSemanticSourceNode> sourceMembers,
            IReadOnlyList<CanonicalSemanticNodeSchema> schemas,
            LubanSemanticSourceDocument document,
            int depth)
        {
            var indexed = new Dictionary<string, LubanSemanticSourceNode>(StringComparer.Ordinal);
            for (var index = 0; index < sourceMembers.Count; index++)
            {
                var member = sourceMembers[index];
                if (member == null || string.IsNullOrWhiteSpace(member.NodeId))
                    Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Source member is invalid.");
                if (indexed.TryGetValue(member.NodeId, out var existing))
                    FailConflict("Duplicate record member owners.", ProjectProvenance(existing.Provenance, document, null), ProjectProvenance(member.Provenance, document, null));
                indexed.Add(member.NodeId, member);
            }

            var result = new CanonicalSemanticNode[schemas.Count];
            for (var index = 0; index < schemas.Count; index++)
            {
                if (!indexed.TryGetValue(schemas[index].NodeId, out var source))
                    Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Registered record member is missing.");
                result[index] = ProjectNode(source, schemas[index], document, depth);
                indexed.Remove(schemas[index].NodeId);
            }

            if (indexed.Count != 0)
                Fail(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Unregistered record member is denied.");
            return result;
        }

        /// <summary>
        /// 投影完整 collection；set/multiset canonical 排序，set 重复元素以 CFG1001 拒绝。
        /// </summary>
        private static CanonicalSemanticNode ProjectCollection(
            LubanSemanticSourceNode source,
            CanonicalSemanticNodeSchema schema,
            LubanSemanticSourceDocument document,
            CanonicalSemanticProvenance provenance,
            int depth)
        {
            if (source.ScalarValue != null || schema.Children.Count != 1)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Collection schema/value is invalid.", provenance);
            if (schema.CollectionSemantics == CanonicalCollectionSemantics.Multiset)
                Fail(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Multiset is not admitted by the v2 registry.", provenance);
            CanonicalSemanticResourceBudget.RequireCollectionCount(source.Children.Count, "projected collection");
            var elements = new List<CanonicalSemanticNode>(source.Children.Count);
            for (var index = 0; index < source.Children.Count; index++)
                elements.Add(ProjectNode(source.Children[index], schema.Children[0], document, depth + 1));
            if (schema.CollectionSemantics != CanonicalCollectionSemantics.Ordered)
                elements.Sort(CanonicalSemanticTreeValueComparer.Compare);
            RejectDuplicateSetElements(elements, schema.CollectionSemantics);
            var normalized = new CanonicalSemanticNode[elements.Count];
            for (var index = 0; index < elements.Count; index++)
                normalized[index] = WithOrdinal(elements[index], index);
            return new CanonicalSemanticNode(
                schema.NodeOrdinal,
                schema.NodeId,
                CanonicalSemanticNodeKind.Collection,
                source.TypeId,
                source.TargetDomainId ?? string.Empty,
                schema.CollectionSemantics,
                null,
                normalized,
                provenance);
        }

        /// <summary>
        /// 拒绝 set 中两个语义相同但 provenance 不同的 source owner。
        /// </summary>
        private static void RejectDuplicateSetElements(
            IReadOnlyList<CanonicalSemanticNode> elements,
            CanonicalCollectionSemantics semantics)
        {
            if (semantics != CanonicalCollectionSemantics.Set)
                return;
            for (var index = 1; index < elements.Count; index++)
            {
                if (CanonicalSemanticTreeValueComparer.Compare(elements[index - 1], elements[index]) == 0)
                    FailConflict("Set contains duplicate semantic owners.", elements[index - 1].Provenance, elements[index].Provenance);
            }
        }

        /// <summary>
        /// 复制 projected node 并只替换当前 collection scope 的 ElementOrdinal。
        /// </summary>
        private static CanonicalSemanticNode WithOrdinal(CanonicalSemanticNode node, int ordinal)
        {
            return new CanonicalSemanticNode(
                ordinal,
                node.NodeId,
                node.NodeKind,
                node.TypeId,
                node.TargetDomainId,
                node.CollectionSemantics,
                node.ScalarValue,
                node.Children,
                node.Provenance);
        }

        /// <summary>
        /// 将 source scalar union 投影为不含 source ordinal 的 canonical primitive/reference。
        /// </summary>
        private static CanonicalSemanticValue ProjectScalarValue(
            LubanSemanticSourceValue source,
            CanonicalSemanticNodeSchema schema,
            CanonicalSemanticProvenance provenance)
        {
            if (source.Kind != schema.ScalarKind)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Scalar kind does not match schema.", provenance);
            switch (source.Kind)
            {
                case CanonicalSemanticValueKind.Null: return CanonicalSemanticValue.Null();
                case CanonicalSemanticValueKind.Boolean: return ProjectBoolean(source, provenance);
                case CanonicalSemanticValueKind.Int32: return CanonicalSemanticValue.Int32(unchecked((int)source.RawBits));
                case CanonicalSemanticValueKind.UInt32: return CanonicalSemanticValue.UInt32((uint)source.RawBits);
                case CanonicalSemanticValueKind.Int64: return CanonicalSemanticValue.Int64(unchecked((long)source.RawBits));
                case CanonicalSemanticValueKind.UInt64: return CanonicalSemanticValue.UInt64(source.RawBits);
                case CanonicalSemanticValueKind.Float32: return CanonicalSemanticValue.Float32Bits((uint)source.RawBits);
                case CanonicalSemanticValueKind.Float64: return CanonicalSemanticValue.Float64Bits(source.RawBits);
                case CanonicalSemanticValueKind.String: return CanonicalSemanticValue.String(source.Text);
                case CanonicalSemanticValueKind.Bytes: return CanonicalSemanticValue.Bytes(source.ByteValueUnsafe);
                case CanonicalSemanticValueKind.DateTime: return ProjectDateTime(source, provenance);
                case CanonicalSemanticValueKind.Enum: return ProjectEnum(source, schema, provenance);
                case CanonicalSemanticValueKind.DefinitionReference: return ProjectReference(source, schema, provenance);
                default:
                    Fail(CanonicalSemanticRuleCatalog.NonCanonicalIdentity, "Unknown scalar kind.", provenance);
                    return null;
            }
        }

        /// <summary>
        /// 投影规范 Boolean 并拒绝非 0/1 原始位。
        /// </summary>
        private static CanonicalSemanticValue ProjectBoolean(
            LubanSemanticSourceValue source,
            CanonicalSemanticProvenance provenance)
        {
            if (source.RawBits > 1)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Boolean raw bits are invalid.", provenance);
            return CanonicalSemanticValue.Boolean(source.RawBits == 1);
        }

        /// <summary>
        /// 投影 DateTime ticks/kind 并拒绝无效范围。
        /// </summary>
        private static CanonicalSemanticValue ProjectDateTime(
            LubanSemanticSourceValue source,
            CanonicalSemanticProvenance provenance)
        {
            if (source.RawBits > (ulong)DateTime.MaxValue.Ticks)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "DateTime ticks are invalid.", provenance);
            return CanonicalSemanticValue.DateTime(new DateTime((long)source.RawBits, source.DateTimeKind));
        }

        /// <summary>
        /// 投影显式 enum type id/underlying bits 并绑定 registry type id。
        /// </summary>
        private static CanonicalSemanticValue ProjectEnum(
            LubanSemanticSourceValue source,
            CanonicalSemanticNodeSchema schema,
            CanonicalSemanticProvenance provenance)
        {
            if (!string.Equals(source.Text, schema.TypeId, StringComparison.Ordinal)
                || source.EnumUnderlyingType != schema.EnumUnderlyingType)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Enum type id does not match schema.", provenance);
            return CanonicalSemanticValue.Enum(source.Text, source.EnumUnderlyingType, source.RawBits);
        }

        /// <summary>
        /// 投影 reference 并再次验证 source target domain、kind 与 key arity。
        /// </summary>
        private static CanonicalSemanticValue ProjectReference(
            LubanSemanticSourceValue source,
            CanonicalSemanticNodeSchema schema,
            CanonicalSemanticProvenance provenance)
        {
            var sourceId = source.DefinitionReference;
            if (sourceId == null || !string.Equals(sourceId.StableDomainId, schema.TargetDomainId, StringComparison.Ordinal))
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Reference target domain does not match schema.", provenance);
            return CanonicalSemanticValue.Reference(ToCanonicalKey(sourceId));
        }

        /// <summary>
        /// 将 source provenance 转成 canonical provenance并验证 RuleId、generator 与稳定文本。
        /// </summary>
        internal static CanonicalSemanticProvenance ProjectProvenance(
            LubanSemanticSourceProvenance source,
            LubanSemanticSourceDocument document,
            CanonicalSemanticNodeSchema schema)
        {
            if (source == null)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Source provenance is required.");
            RequireText(source.WorkbookId, "WorkbookId", null);
            RequireText(source.TableId, "TableId", null);
            RequireText(source.RowStableId, "RowStableId", null);
            RequireText(source.FieldPath, "FieldPath", null);
            if (source.RawValue == null || source.NormalizedValue == null)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Raw/normalized provenance is required.");
            if (!CanonicalSemanticRuleCatalog.TryResolve(source.RuleId, out var version, out _)
                || source.RuleVersion != version
                || !string.Equals(source.GeneratorVersion, document.GeneratorVersion, StringComparison.Ordinal))
            {
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Source provenance rule/generator is invalid.");
            }
            if (schema != null && (!string.Equals(source.RuleId, schema.RuleId, StringComparison.Ordinal)
                                   || source.RuleVersion != schema.RuleVersion))
            {
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Source provenance rule does not match registry.");
            }

            var related = new CanonicalDefinitionKey[source.RelatedDefinitions.Count];
            for (var index = 0; index < related.Length; index++)
                related[index] = ToCanonicalKey(source.RelatedDefinitions[index]);
            return new CanonicalSemanticProvenance(
                source.WorkbookId,
                source.TableId,
                source.RowStableId,
                source.FieldPath,
                source.RawValue,
                source.NormalizedValue,
                source.RuleId,
                source.RuleVersion,
                source.GeneratorVersion,
                related);
        }

        /// <summary>
        /// 验证 DefinitionKey 与 domain 前置 key 字段值完全一致。
        /// </summary>
        private static void ValidateStableKey(
            IReadOnlyList<CanonicalSemanticField> fields,
            CanonicalDefinitionKey key,
            CanonicalSemanticProvenance provenance)
        {
            for (var index = 0; index < key.StableIdParts.Count; index++)
            {
                var root = fields[index].Root;
                if (root.NodeKind != CanonicalSemanticNodeKind.Scalar
                    || root.ScalarValue.Kind != CanonicalSemanticValueKind.Int32
                    || unchecked((int)root.ScalarValue.RawBits) != key.StableIdParts[index])
                {
                    Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Stable key field does not match DefinitionId.", provenance);
                }
            }
        }

        /// <summary>
        /// 验证稳定文本非空白。
        /// </summary>
        private static void RequireText(string value, string fieldName, CanonicalSemanticProvenance provenance)
        {
            if (string.IsNullOrWhiteSpace(value))
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, fieldName + " is required.", provenance);
        }

        /// <summary>
        /// 抛出稳定 RuleId 与可选 provenance 的验证失败。
        /// </summary>
        internal static void Fail(
            string ruleId,
            string message,
            CanonicalSemanticProvenance provenance = null)
        {
            throw new CanonicalSemanticValidationException(ruleId, message, provenance);
        }

        /// <summary>
        /// 抛出含双方 provenance 的 CFG1001 owner conflict。
        /// </summary>
        internal static void FailConflict(
            string message,
            CanonicalSemanticProvenance left,
            CanonicalSemanticProvenance right)
        {
            throw new CanonicalSemanticValidationException(
                CanonicalSemanticRuleCatalog.SemanticOwnerConflict,
                message,
                left,
                new[] { left, right });
        }
    }

    /// <summary>
    /// 保存 source tree 投影后、sealed compile 前的不可变 Definition 草稿。
    /// </summary>
    internal sealed class SemanticDefinitionDraft
    {
        /// <summary>
        /// 创建只包含 key、字段树与 provenance 的编译草稿。
        /// </summary>
        public SemanticDefinitionDraft(
            int definitionOrdinal,
            CanonicalDefinitionKey key,
            IReadOnlyList<CanonicalSemanticField> fields,
            CanonicalSemanticProvenance provenance)
        {
            DefinitionOrdinal = definitionOrdinal;
            Key = key;
            Fields = fields;
            Provenance = provenance;
        }

        public int DefinitionOrdinal { get; }

        public CanonicalDefinitionKey Key { get; }

        public IReadOnlyList<CanonicalSemanticField> Fields { get; }

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 对 semantic tree 值执行不含 provenance 的 ordinal 比较，供 set/multiset canonical 排序。
    /// </summary>
    internal static class CanonicalSemanticTreeValueComparer
    {
        /// <summary>
        /// 递归比较两个节点的语义值，不读取 source 路径或 raw 文本。
        /// </summary>
        public static int Compare(CanonicalSemanticNode left, CanonicalSemanticNode right)
        {
            var result = ((byte)left.NodeKind).CompareTo((byte)right.NodeKind);
            if (result != 0)
                return result;
            result = string.CompareOrdinal(left.TypeId, right.TypeId);
            if (result != 0)
                return result;
            result = string.CompareOrdinal(left.TargetDomainId, right.TargetDomainId);
            if (result != 0)
                return result;
            result = ((byte)left.CollectionSemantics).CompareTo((byte)right.CollectionSemantics);
            if (result != 0)
                return result;
            result = CompareScalar(left.ScalarValue, right.ScalarValue);
            if (result != 0)
                return result;
            return CompareChildren(left.Children, right.Children);
        }

        /// <summary>
        /// 比较可选 scalar 的 kind、bits、text、bytes 与 reference key。
        /// </summary>
        private static int CompareScalar(CanonicalSemanticValue left, CanonicalSemanticValue right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            var result = ((byte)left.Kind).CompareTo((byte)right.Kind);
            if (result != 0)
                return result;
            result = left.RawBits.CompareTo(right.RawBits);
            if (result != 0)
                return result;
            result = string.CompareOrdinal(left.Text, right.Text);
            if (result != 0)
                return result;
            result = CompareBytes(left.ByteValueUnsafe, right.ByteValueUnsafe);
            if (result != 0)
                return result;
            return CompareKeys(left.DefinitionReference, right.DefinitionReference);
        }

        /// <summary>
        /// 按数量与逐项语义值比较 child trees。
        /// </summary>
        private static int CompareChildren(
            IReadOnlyList<CanonicalSemanticNode> left,
            IReadOnlyList<CanonicalSemanticNode> right)
        {
            var result = left.Count.CompareTo(right.Count);
            for (var index = 0; result == 0 && index < left.Count; index++)
                result = Compare(left[index], right[index]);
            return result;
        }

        /// <summary>
        /// 按 byte ordinal 比较可选 byte arrays。
        /// </summary>
        private static int CompareBytes(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            var result = left.Length.CompareTo(right.Length);
            for (var index = 0; result == 0 && index < left.Length; index++)
                result = left[index].CompareTo(right[index]);
            return result;
        }

        /// <summary>
        /// 比较可选 canonical reference keys。
        /// </summary>
        private static int CompareKeys(CanonicalDefinitionKey left, CanonicalDefinitionKey right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            return left.CompareTo(right);
        }
    }
}
