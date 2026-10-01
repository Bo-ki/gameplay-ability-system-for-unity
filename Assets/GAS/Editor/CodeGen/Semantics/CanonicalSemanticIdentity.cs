using System;
using System.Collections.Generic;

namespace GAS.Editor.CodeGen.Semantics
{
    /// <summary>
    /// 保存由同一 v2 graph 派生的 GraphHash、SchemaHash 与 ContentHash；三类 hash 使用互不混淆的 domain。
    /// </summary>
    public sealed class CanonicalSemanticIdentity
    {
        /// <summary>
        /// 创建已经过 canonical projection codec 计算的冻结身份。
        /// </summary>
        private CanonicalSemanticIdentity(string graphHash, string schemaHash, string contentHash)
        {
            GraphHash = graphHash;
            SchemaHash = schemaHash;
            ContentHash = contentHash;
        }

        public string GraphHash { get; }

        public string SchemaHash { get; }

        public string ContentHash { get; }

        /// <summary>
        /// 从 graph、typed ABI 与外部固定 ContractMatrixHash 计算三项独立身份。
        /// </summary>
        public static CanonicalSemanticIdentity Create(
            CanonicalNormalizedSemanticGraph graph,
            string typedContractAbi,
            string contractMatrixHash)
        {
            RequireHash(contractMatrixHash, nameof(contractMatrixHash));
            ValidateFixedContractBoundary(typedContractAbi, contractMatrixHash);
            var graphBytes = CanonicalSemanticBinaryCodec.Encode(graph);
            var schemaBytes = CanonicalSemanticProjectionCodec.EncodeSchema(
                graph, typedContractAbi, contractMatrixHash);
            var contentBytes = CanonicalSemanticProjectionCodec.EncodeContent(graph);
            return new CanonicalSemanticIdentity(
                CanonicalSemanticHash.Compute(CanonicalSemanticVersions.GraphHashDomain, graphBytes),
                CanonicalSemanticHash.Compute(CanonicalSemanticVersions.SchemaHashDomain, schemaBytes),
                CanonicalSemanticHash.Compute(CanonicalSemanticVersions.ContentHashDomain, contentBytes));
        }

        /// <summary>
        /// 验证 SHA-256 文本恰为 64 个小写十六进制字符。
        /// </summary>
        internal static void RequireHash(string value, string fieldName)
        {
            if (value == null || value.Length != 64)
                Fail(fieldName + " must be a lowercase SHA-256 hex value.");
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if ((character < '0' || character > '9') && (character < 'a' || character > 'f'))
                    Fail(fieldName + " must be a lowercase SHA-256 hex value.");
            }
        }

        /// <summary>
        /// 拒绝调用方注入替代 ABI 或 matrix 身份。
        /// </summary>
        private static void ValidateFixedContractBoundary(string typedContractAbi, string contractMatrixHash)
        {
            if (!string.Equals(typedContractAbi, CanonicalSemanticVersions.TypedContractAbi, StringComparison.Ordinal)
                || !string.Equals(
                    contractMatrixHash,
                    CanonicalSemanticProjectionCodec.ComputeContractMatrixHash(),
                    StringComparison.Ordinal))
            {
                Fail("Typed ABI or ContractMatrixHash does not match the fixed v2 boundary.");
            }
        }

        /// <summary>
        /// 以 CFG1501 拒绝非 canonical 身份文本。
        /// </summary>
        private static void Fail(string message)
        {
            throw new CanonicalSemanticValidationException(
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                message);
        }
    }

    /// <summary>
    /// 将完整 graph 投影为固定 matrix、schema 与 content bytes；provenance 和 source/toolchain 只进入 GraphHash。
    /// </summary>
    public static class CanonicalSemanticProjectionCodec
    {
        private static readonly byte[] SchemaMagic =
        {
            0x45, 0x58, 0x47, 0x53, 0x53, 0x43, 0x48, 0x32,
        };

        private static readonly byte[] ContentMagic =
        {
            0x45, 0x58, 0x47, 0x53, 0x43, 0x4E, 0x54, 0x32,
        };

        private static readonly byte[] RegistryMagic =
        {
            0x45, 0x58, 0x47, 0x53, 0x52, 0x45, 0x47, 0x32,
        };

        /// <summary>
        /// 编码 schema、typed ABI、RuleId domain 与外部 ContractMatrixHash。
        /// </summary>
        public static byte[] EncodeSchema(
            CanonicalNormalizedSemanticGraph graph,
            string typedContractAbi,
            string contractMatrixHash)
        {
            CanonicalSemanticGraphValidator.Validate(graph);
            CanonicalSemanticIdentity.RequireHash(contractMatrixHash, nameof(contractMatrixHash));
            if (!string.Equals(typedContractAbi, CanonicalSemanticVersions.TypedContractAbi, StringComparison.Ordinal)
                || !string.Equals(contractMatrixHash, ComputeContractMatrixHash(), StringComparison.Ordinal))
            {
                throw new CanonicalSemanticValidationException(
                    CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                    "Schema projection ABI/matrix boundary is not fixed v2.");
            }
            return EncodeSchemaPayload(graph.SourceContractVersion, graph.Rules, typedContractAbi, contractMatrixHash);
        }

        /// <summary>
        /// 不依赖实例数据生成固定 v2 schema payload，供 Typed parse 阶段直接逐 byte 核对。
        /// </summary>
        public static byte[] EncodeFixedSchema(string generatorVersion, string contractMatrixHash)
        {
            if (string.IsNullOrWhiteSpace(generatorVersion))
                throw new CanonicalSemanticValidationException(
                    CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                    "GeneratorVersion is required for fixed schema projection.");
            CanonicalSemanticIdentity.RequireHash(contractMatrixHash, nameof(contractMatrixHash));
            if (!string.Equals(contractMatrixHash, ComputeContractMatrixHash(), StringComparison.Ordinal))
                throw new CanonicalSemanticValidationException(
                    CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                    "Fixed schema projection matrix is not current v2.");
            var rules = CanonicalSemanticRuleCatalog.CreateDefaultRegistry(generatorVersion);
            return EncodeSchemaPayload(
                CanonicalSemanticVersions.LubanSourceContractVersion,
                rules,
                CanonicalSemanticVersions.TypedContractAbi,
                contractMatrixHash);
        }

        /// <summary>
        /// 写入 graph 与 fixed-parse 共用的 schema canonical payload。
        /// </summary>
        private static byte[] EncodeSchemaPayload(
            ushort sourceContractVersion,
            IReadOnlyList<CanonicalSemanticRule> rules,
            string typedContractAbi,
            string contractMatrixHash)
        {
            var writer = new CanonicalBinaryWriter();
            writer.WriteRaw(SchemaMagic);
            writer.WriteUInt16(CanonicalSemanticVersions.SchemaProjectionVersion);
            writer.WriteUInt16(CanonicalSemanticVersions.ResourceBudgetVersion);
            writer.WriteString(CanonicalSemanticVersions.ResourceBudgetId);
            writer.WriteString(CanonicalSemanticVersions.GraphSchema);
            writer.WriteString(CanonicalSemanticVersions.RuleDomain);
            writer.WriteUInt16(sourceContractVersion);
            writer.WriteString(typedContractAbi);
            writer.WriteString(contractMatrixHash);
            WriteRuleSchema(writer, rules);
            writer.WriteBytes(EncodeRegistry());
            return writer.ToArray();
        }

        /// <summary>
        /// 编码全部 gameplay tree 与 sealed compile 结果，不写 provenance-only 值或 source/toolchain identity。
        /// </summary>
        public static byte[] EncodeContent(CanonicalNormalizedSemanticGraph graph)
        {
            CanonicalSemanticGraphValidator.Validate(graph);
            var writer = new CanonicalBinaryWriter();
            writer.WriteRaw(ContentMagic);
            writer.WriteUInt16(CanonicalSemanticVersions.ContentProjectionVersion);
            writer.WriteUInt16(CanonicalSemanticVersions.ResourceBudgetVersion);
            writer.WriteString(CanonicalSemanticVersions.ResourceBudgetId);
            writer.WriteString(CanonicalSemanticVersions.GraphSchema);
            writer.WriteCount(graph.Definitions.Count);
            for (var index = 0; index < graph.Definitions.Count; index++)
                WriteDefinitionContent(writer, graph.Definitions[index]);
            return writer.ToArray();
        }

        /// <summary>
        /// 编码固定 v2 domain/schema/support matrix，实例数据不能改变这些 bytes。
        /// </summary>
        public static byte[] EncodeRegistry()
        {
            var writer = new CanonicalBinaryWriter();
            writer.WriteRaw(RegistryMagic);
            writer.WriteUInt16(CanonicalSemanticVersions.ContractMatrixCodecVersion);
            writer.WriteUInt16(CanonicalSemanticVersions.ResourceBudgetVersion);
            writer.WriteString(CanonicalSemanticVersions.ResourceBudgetId);
            writer.WriteString(CanonicalSemanticVersions.GraphSchema);
            var domains = CanonicalSemanticSchemaRegistry.All;
            writer.WriteCount(domains.Count);
            for (var index = 0; index < domains.Count; index++)
                WriteDomainSchema(writer, domains[index]);
            return writer.ToArray();
        }

        /// <summary>
        /// 计算固定 registry 的 ContractMatrixHash，供 TypedContract 绑定。
        /// </summary>
        public static string ComputeContractMatrixHash()
        {
            return CanonicalSemanticHash.Compute(
                CanonicalSemanticVersions.ContractMatrixHashDomain,
                EncodeRegistry());
        }

        /// <summary>
        /// 写入固定 RuleId registry 的 schema 部分。
        /// </summary>
        private static void WriteRuleSchema(
            CanonicalBinaryWriter writer,
            IReadOnlyList<CanonicalSemanticRule> rules)
        {
            writer.WriteCount(rules.Count);
            for (var index = 0; index < rules.Count; index++)
            {
                writer.WriteInt32(rules[index].RuleOrdinal);
                writer.WriteString(rules[index].RuleId);
                writer.WriteUInt32(rules[index].RuleVersion);
                writer.WriteByte((byte)rules[index].RuleKind);
            }
        }

        /// <summary>
        /// 写入一个 domain 的 key、递归字段 schema 与完整 program matrix。
        /// </summary>
        private static void WriteDomainSchema(
            CanonicalBinaryWriter writer,
            CanonicalSemanticDomainSchema domain)
        {
            writer.WriteInt32(domain.DomainOrdinal);
            writer.WriteString(domain.StableDomainId);
            writer.WriteInt32((int)domain.DefinitionKind);
            writer.WriteInt32(domain.StableIdPartCount);
            writer.WriteCount(domain.Fields.Count);
            for (var index = 0; index < domain.Fields.Count; index++)
            {
                writer.WriteInt32(domain.Fields[index].FieldOrdinal);
                WriteNodeSchema(writer, domain.Fields[index].Root, 0);
            }

            writer.WriteCount(domain.Programs.Count);
            for (var index = 0; index < domain.Programs.Count; index++)
                WriteProgramSchema(writer, domain.Programs[index]);
        }

        /// <summary>
        /// 递归写入 presence、collection、target domain、分类、裁决与 variant schema。
        /// </summary>
        private static void WriteNodeSchema(
            CanonicalBinaryWriter writer,
            CanonicalSemanticNodeSchema node,
            int depth)
        {
            if (depth > CanonicalBinaryReader.MaxValueDepth)
                throw new InvalidOperationException("Registry node depth exceeds the codec limit.");
            writer.AccountNode(depth);
            writer.WriteInt32(node.NodeOrdinal);
            writer.WriteString(node.NodeId);
            writer.WriteByte((byte)node.NodeKind);
            writer.WriteString(node.TypeId);
            writer.WriteString(node.TargetDomainId);
            writer.WriteByte((byte)node.ScalarKind);
            writer.WriteByte((byte)node.EnumUnderlyingType);
            writer.WriteByte((byte)node.CollectionSemantics);
            writer.WriteByte((byte)node.PresencePolicy);
            writer.WriteByte((byte)node.Classification);
            writer.WriteByte((byte)node.Disposition);
            writer.WriteString(node.RuleId);
            writer.WriteUInt32(node.RuleVersion);
            WriteNodeSchemaChildren(writer, node.Children, depth);
            WriteVariants(writer, node.Variants, depth);
        }

        /// <summary>
        /// 写入当前节点的普通 child schemas。
        /// </summary>
        private static void WriteNodeSchemaChildren(
            CanonicalBinaryWriter writer,
            IReadOnlyList<CanonicalSemanticNodeSchema> children,
            int depth)
        {
            writer.WriteCount(children.Count);
            for (var index = 0; index < children.Count; index++)
                WriteNodeSchema(writer, children[index], depth + 1);
        }

        /// <summary>
        /// 写入 variant discriminator 与每个 case 的完整 payload schema。
        /// </summary>
        private static void WriteVariants(
            CanonicalBinaryWriter writer,
            IReadOnlyList<CanonicalSemanticVariantSchema> variants,
            int depth)
        {
            writer.WriteCount(variants.Count);
            for (var index = 0; index < variants.Count; index++)
            {
                var variant = variants[index];
                writer.WriteInt32(variant.VariantOrdinal);
                writer.WriteString(variant.VariantTypeId);
                WriteNodeSchemaChildren(writer, variant.Members, depth + 1);
            }
        }

        /// <summary>
        /// 写入一个 ProgramKind 的准入、operation 模板与逐字段矩阵。
        /// </summary>
        private static void WriteProgramSchema(
            CanonicalBinaryWriter writer,
            CanonicalSemanticProgramSchema program)
        {
            writer.WriteInt32(program.ProgramSchemaOrdinal);
            writer.WriteByte((byte)program.ProgramKind);
            writer.WriteByte((byte)program.Disposition);
            writer.WriteString(program.RuleId);
            writer.WriteUInt32(program.RuleVersion);
            writer.WriteCount(program.Operations.Count);
            for (var index = 0; index < program.Operations.Count; index++)
                WriteOperationSchema(writer, program.Operations[index]);
            writer.WriteCount(program.FieldDecisions.Count);
            for (var index = 0; index < program.FieldDecisions.Count; index++)
                WriteRegistryDecision(writer, program.FieldDecisions[index]);
        }

        /// <summary>
        /// 写入强制 operation 顺序、read fields、ValueView、phase、target 与 bound。
        /// </summary>
        private static void WriteOperationSchema(
            CanonicalBinaryWriter writer,
            CanonicalSemanticOperationSchema operation)
        {
            writer.WriteInt32(operation.OperationOrdinal);
            writer.WriteString(operation.OperationId);
            writer.WriteString(operation.ValueView);
            writer.WriteString(operation.Phase);
            writer.WriteString(operation.TargetDomainId);
            writer.WriteString(operation.BoundFieldId);
            writer.WriteInt32(operation.PayloadBytesPerItem);
            writer.WriteCount(operation.FieldIds.Count);
            for (var index = 0; index < operation.FieldIds.Count; index++)
                writer.WriteString(operation.FieldIds[index]);
            writer.WriteString(operation.RuleId);
            writer.WriteUInt32(operation.RuleVersion);
        }

        /// <summary>
        /// 写入 program 对一个真实字段的 contextual support decision。
        /// </summary>
        private static void WriteRegistryDecision(
            CanonicalBinaryWriter writer,
            CanonicalSemanticFieldSupportDecision decision)
        {
            writer.WriteInt32(decision.DecisionOrdinal);
            writer.WriteInt32(decision.FieldOrdinal);
            writer.WriteString(decision.FieldId);
            writer.WriteByte((byte)decision.Classification);
            writer.WriteByte((byte)decision.Disposition);
            writer.WriteString(decision.RuleId);
            writer.WriteUInt32(decision.RuleVersion);
        }

        /// <summary>
        /// 写入一个 Definition 的全部 content identity 输入。
        /// </summary>
        private static void WriteDefinitionContent(
            CanonicalBinaryWriter writer,
            CanonicalSemanticDefinition definition)
        {
            writer.WriteInt32(definition.DefinitionOrdinal);
            CanonicalSemanticBinaryCodec.WriteDefinitionKey(writer, definition.Key);
            WriteFieldContent(writer, definition.Fields);
            WriteRoleContent(writer, definition.Roles);
            WriteProgramContent(writer, definition.Programs);
            WriteDependencyContent(writer, definition.Dependencies);
        }

        /// <summary>
        /// 写入所有 gameplay/denied 字段 tree；provenance-only 字段仅写结构标记。
        /// </summary>
        private static void WriteFieldContent(
            CanonicalBinaryWriter writer,
            IReadOnlyList<CanonicalSemanticField> fields)
        {
            writer.WriteCount(fields.Count);
            for (var index = 0; index < fields.Count; index++)
            {
                var field = fields[index];
                writer.WriteInt32(field.FieldOrdinal);
                writer.WriteByte((byte)field.Classification);
                writer.WriteByte((byte)field.Disposition);
                var includeValue = field.Classification != CanonicalFieldClassification.ProvenanceOnly;
                writer.WriteBoolean(includeValue);
                if (includeValue)
                    WriteNodeContent(writer, field.Root, 0);
            }
        }

        /// <summary>
        /// 递归写入 tree 内容，不包含 provenance。
        /// </summary>
        private static void WriteNodeContent(
            CanonicalBinaryWriter writer,
            CanonicalSemanticNode node,
            int depth)
        {
            if (depth > CanonicalBinaryReader.MaxValueDepth)
                throw new InvalidOperationException("Content node depth exceeds the codec limit.");
            writer.AccountNode(depth);
            writer.WriteInt32(node.NodeOrdinal);
            writer.WriteString(node.NodeId);
            writer.WriteByte((byte)node.NodeKind);
            writer.WriteString(node.TypeId);
            writer.WriteString(node.TargetDomainId);
            writer.WriteByte((byte)node.CollectionSemantics);
            if (node.NodeKind == CanonicalSemanticNodeKind.Scalar)
                CanonicalSemanticBinaryCodec.WriteScalarValue(writer, node.ScalarValue);
            writer.WriteCount(node.Children.Count);
            for (var index = 0; index < node.Children.Count; index++)
                WriteNodeContent(writer, node.Children[index], depth + 1);
        }

        /// <summary>
        /// 写入 compiler 推导角色的全部结构身份。
        /// </summary>
        private static void WriteRoleContent(
            CanonicalBinaryWriter writer,
            IReadOnlyList<CanonicalSemanticRole> roles)
        {
            writer.WriteCount(roles.Count);
            for (var index = 0; index < roles.Count; index++)
            {
                var role = roles[index];
                writer.WriteInt32(role.RoleOrdinal);
                writer.WriteByte((byte)role.RoleKind);
                CanonicalSemanticBinaryCodec.WriteDefinitionKey(writer, role.OwnerDefinition);
                writer.WriteInt32(role.OwnerFieldOrdinal);
                writer.WriteString(role.RuleId);
                writer.WriteUInt32(role.RuleVersion);
            }
        }

        /// <summary>
        /// 写入 program、decisions、nodes、projection payload 与 DAG edges。
        /// </summary>
        private static void WriteProgramContent(
            CanonicalBinaryWriter writer,
            IReadOnlyList<CanonicalSemanticProgram> programs)
        {
            writer.WriteCount(programs.Count);
            for (var index = 0; index < programs.Count; index++)
            {
                var program = programs[index];
                writer.WriteInt32(program.ProgramOrdinal);
                writer.WriteString(program.ProgramId);
                writer.WriteByte((byte)program.ProgramKind);
                writer.WriteByte((byte)program.Disposition);
                WriteDecisionContent(writer, program.Decisions);
                WriteProgramNodeContent(writer, program.Nodes);
                WriteProgramEdgeContent(writer, program.Edges);
            }
        }

        /// <summary>
        /// 写入 contextual decision 的字段激活与裁决身份。
        /// </summary>
        private static void WriteDecisionContent(
            CanonicalBinaryWriter writer,
            IReadOnlyList<CanonicalSemanticContextDecision> decisions)
        {
            writer.WriteCount(decisions.Count);
            for (var index = 0; index < decisions.Count; index++)
            {
                var decision = decisions[index];
                writer.WriteInt32(decision.DecisionOrdinal);
                writer.WriteInt32(decision.FieldOrdinal);
                writer.WriteString(decision.FieldId);
                writer.WriteBoolean(decision.IsActive);
                writer.WriteByte((byte)decision.Classification);
                writer.WriteByte((byte)decision.Disposition);
                writer.WriteString(decision.RuleId);
                writer.WriteUInt32(decision.RuleVersion);
            }
        }

        /// <summary>
        /// 写入 program node 的强制操作、read sets 与 projection binding。
        /// </summary>
        private static void WriteProgramNodeContent(
            CanonicalBinaryWriter writer,
            IReadOnlyList<CanonicalSemanticProgramNode> nodes)
        {
            writer.WriteCount(nodes.Count);
            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                writer.AccountNode();
                writer.WriteInt32(node.NodeOrdinal);
                writer.WriteString(node.OperationId);
                WriteOrdinals(writer, node.FieldOrdinals);
                WriteOrdinals(writer, node.DependencyOrdinals);
                WriteProjectionContent(writer, node.Projection);
            }
        }

        /// <summary>
        /// 写入 projection 的绑定身份、ValueView、bounds 与 payload。
        /// </summary>
        private static void WriteProjectionContent(
            CanonicalBinaryWriter writer,
            CanonicalSemanticProjectionBinding projection)
        {
            writer.WriteInt32(projection.ProjectionOrdinal);
            CanonicalSemanticBinaryCodec.WriteDefinitionKey(writer, projection.OwnerDefinition);
            writer.WriteInt32(projection.ProgramOrdinal);
            writer.WriteInt32(projection.NodeOrdinal);
            writer.WriteInt32(projection.SourceFieldOrdinal);
            writer.WriteString(projection.OperationId);
            writer.WriteString(projection.ValueView);
            writer.WriteString(projection.Phase);
            writer.WriteString(projection.TargetDomainId);
            writer.WriteInt32(projection.MaxItemCount);
            writer.WriteInt32(projection.MaxPayloadBytes);
            writer.WriteBytes(projection.PayloadUnsafe);
            writer.WriteString(projection.RuleId);
            writer.WriteUInt32(projection.RuleVersion);
        }

        /// <summary>
        /// 写入 program edge ordinal、端点与类型。
        /// </summary>
        private static void WriteProgramEdgeContent(
            CanonicalBinaryWriter writer,
            IReadOnlyList<CanonicalSemanticProgramEdge> edges)
        {
            writer.WriteCount(edges.Count);
            for (var index = 0; index < edges.Count; index++)
            {
                var edge = edges[index];
                writer.AccountEdge();
                writer.WriteInt32(edge.EdgeOrdinal);
                writer.WriteInt32(edge.SourceNodeOrdinal);
                writer.WriteInt32(edge.TargetNodeOrdinal);
                writer.WriteByte((byte)edge.EdgeKind);
            }
        }

        /// <summary>
        /// 写入 dependency target、source field 与 owner/sign/work/cleanup/bound 元数据。
        /// </summary>
        private static void WriteDependencyContent(
            CanonicalBinaryWriter writer,
            IReadOnlyList<CanonicalSemanticDependency> dependencies)
        {
            writer.WriteCount(dependencies.Count);
            for (var index = 0; index < dependencies.Count; index++)
            {
                var dependency = dependencies[index];
                writer.AccountEdge();
                writer.WriteInt32(dependency.DependencyOrdinal);
                writer.WriteString(dependency.DependencyId);
                writer.WriteInt32(dependency.SourceFieldOrdinal);
                writer.WriteByte((byte)dependency.DependencyKind);
                CanonicalSemanticBinaryCodec.WriteDefinitionKey(writer, dependency.TargetDefinition);
                writer.WriteString(dependency.TargetDomainId);
                writer.WriteByte((byte)dependency.OwnerKind);
                writer.WriteByte((byte)dependency.Sign);
                writer.WriteByte((byte)dependency.WorkKind);
                writer.WriteByte((byte)dependency.CleanupPolicy);
                writer.WriteInt32(dependency.MaxExpansion);
            }
        }

        /// <summary>
        /// 写入一组显式引用 ordinal。
        /// </summary>
        private static void WriteOrdinals(CanonicalBinaryWriter writer, IReadOnlyList<int> ordinals)
        {
            writer.WriteCount(ordinals.Count);
            for (var index = 0; index < ordinals.Count; index++)
                writer.WriteInt32(ordinals[index]);
        }
    }
}
