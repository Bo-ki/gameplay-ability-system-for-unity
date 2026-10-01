using System;
using System.Collections.Generic;

namespace GAS.Editor.CodeGen.Semantics
{
    /// <summary>
    /// 对完整 v2 canonical semantic graph 执行可逆二进制编解码，并拒绝任何旧协议或非 canonical 表达。
    /// </summary>
    public static class CanonicalSemanticBinaryCodec
    {
        private static readonly byte[] GraphMagic =
        {
            0x45, 0x58, 0x47, 0x53, 0x47, 0x52, 0x50, 0x32,
        };

        /// <summary>
        /// 将已验证 graph 编码为版本化 little-endian canonical bytes。
        /// </summary>
        public static byte[] Encode(CanonicalNormalizedSemanticGraph graph)
        {
            CanonicalSemanticGraphValidator.Validate(graph);
            var writer = new CanonicalBinaryWriter();
            writer.WriteRaw(GraphMagic);
            writer.WriteUInt16(CanonicalSemanticVersions.GraphCodecVersion);
            writer.WriteString(CanonicalSemanticVersions.GraphSchema);
            writer.WriteString(CanonicalSemanticVersions.RuleDomain);
            writer.WriteUInt16(graph.SourceContractVersion);
            writer.WriteUInt16(graph.ResourceBudgetVersion);
            writer.WriteString(graph.ResourceBudgetId);
            WriteToolchain(writer, graph.ToolchainIdentity);
            writer.WriteString(graph.GeneratorVersion);
            writer.WriteString(graph.SourceInputHash);
            WriteCoverage(writer, graph.Coverage);
            writer.WriteBytes(CanonicalSemanticProjectionCodec.EncodeRegistry());
            WriteRules(writer, graph.Rules);
            WriteDefinitions(writer, graph.Definitions);
            return writer.ToArray();
        }

        /// <summary>
        /// 仅解码 v2 graph，并通过重新编码拒绝非 canonical 顺序、旧格式与替代表达。
        /// </summary>
        public static CanonicalNormalizedSemanticGraph Decode(byte[] bytes)
        {
            try
            {
                var reader = new CanonicalBinaryReader(bytes);
                reader.ExpectRaw(GraphMagic);
                ValidateHeader(
                    reader.ReadUInt16(),
                    reader.ReadString(),
                    reader.ReadString(),
                    reader.ReadUInt16(),
                    reader.ReadUInt16(),
                    reader.ReadString());
                var toolchain = ReadToolchain(reader);
                var generator = reader.ReadString();
                var sourceInputHash = reader.ReadString();
                var coverage = ReadCoverage(reader);
                ValidateRegistryBytes(reader.ReadBytes());
                var rules = ReadRules(reader);
                var definitions = ReadDefinitions(reader);
                reader.EnsureComplete();
                var graph = CanonicalNormalizedSemanticGraph.Create(
                    toolchain, generator, sourceInputHash, coverage, rules, definitions);
                EnsureCanonicalRoundTrip(bytes, Encode(graph));
                return graph;
            }
            catch (CanonicalSemanticValidationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Fail("Canonical graph v2 decode failed: " + exception.Message);
                return null;
            }
        }

        /// <summary>
        /// 校验 codec、graph schema 与 RuleId domain 版本，且不提供 v1 fallback。
        /// </summary>
        private static void ValidateHeader(
            ushort version,
            string schema,
            string ruleDomain,
            ushort sourceContractVersion,
            ushort resourceBudgetVersion,
            string resourceBudgetId)
        {
            if (version != CanonicalSemanticVersions.GraphCodecVersion
                || !string.Equals(schema, CanonicalSemanticVersions.GraphSchema, StringComparison.Ordinal)
                || !string.Equals(ruleDomain, CanonicalSemanticVersions.RuleDomain, StringComparison.Ordinal)
                || sourceContractVersion != CanonicalSemanticVersions.LubanSourceContractVersion
                || resourceBudgetVersion != CanonicalSemanticVersions.ResourceBudgetVersion
                || !string.Equals(resourceBudgetId, CanonicalSemanticVersions.ResourceBudgetId, StringComparison.Ordinal))
            {
                Fail("Unsupported canonical graph codec/schema/rule domain.");
            }
        }

        /// <summary>
        /// 写入结构化工具链身份，避免单字符串别名碰撞。
        /// </summary>
        internal static void WriteToolchain(
            CanonicalBinaryWriter writer,
            CanonicalSemanticToolchainIdentity identity)
        {
            writer.WriteString(identity.LubanProductVersion);
            writer.WriteString(identity.LubanBinaryHash);
            writer.WriteString(identity.SchemaInputHash);
            writer.WriteString(identity.GeneratorIdentity);
        }

        /// <summary>
        /// 读取结构化工具链身份。
        /// </summary>
        internal static CanonicalSemanticToolchainIdentity ReadToolchain(CanonicalBinaryReader reader)
        {
            return new CanonicalSemanticToolchainIdentity(
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString());
        }

        /// <summary>
        /// 写入五项显式 coverage 状态。
        /// </summary>
        internal static void WriteCoverage(CanonicalBinaryWriter writer, CanonicalSemanticCoverage coverage)
        {
            writer.WriteByte((byte)coverage.SchemaCoverage);
            writer.WriteByte((byte)coverage.ReferenceCoverage);
            writer.WriteByte((byte)coverage.ProgramCoverage);
            writer.WriteByte((byte)coverage.SameParseCoverage);
            writer.WriteByte((byte)coverage.Eligibility);
        }

        /// <summary>
        /// 读取五项显式 coverage 状态；validator 将本阶段强制为 Red。
        /// </summary>
        internal static CanonicalSemanticCoverage ReadCoverage(CanonicalBinaryReader reader)
        {
            return new CanonicalSemanticCoverage(
                (CanonicalSemanticCoverageState)reader.ReadByte(),
                (CanonicalSemanticCoverageState)reader.ReadByte(),
                (CanonicalSemanticCoverageState)reader.ReadByte(),
                (CanonicalSemanticCoverageState)reader.ReadByte(),
                (CanonicalSemanticCoverageState)reader.ReadByte());
        }

        /// <summary>
        /// 验证 graph 内嵌 registry 与当前 fixed schema/support matrix 逐 byte 一致。
        /// </summary>
        private static void ValidateRegistryBytes(byte[] actual)
        {
            var expected = CanonicalSemanticProjectionCodec.EncodeRegistry();
            if (!BytesEqual(actual, expected))
                Fail("Canonical graph registry does not match the fixed v2 schema/support matrix.");
        }

        /// <summary>
        /// 写入完整固定 RuleId registry。
        /// </summary>
        private static void WriteRules(CanonicalBinaryWriter writer, IReadOnlyList<CanonicalSemanticRule> rules)
        {
            writer.WriteCount(rules.Count);
            for (var index = 0; index < rules.Count; index++)
            {
                var rule = rules[index];
                writer.WriteInt32(rule.RuleOrdinal);
                writer.WriteString(rule.RuleId);
                writer.WriteUInt32(rule.RuleVersion);
                writer.WriteByte((byte)rule.RuleKind);
                WriteProvenance(writer, rule.Provenance);
            }
        }

        /// <summary>
        /// 读取完整固定 RuleId registry。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticRule> ReadRules(CanonicalBinaryReader reader)
        {
            var result = new CanonicalSemanticRule[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new CanonicalSemanticRule(
                    reader.ReadInt32(), reader.ReadString(), reader.ReadUInt32(),
                    (CanonicalRuleKind)reader.ReadByte(), ReadProvenance(reader));
            }

            return result;
        }

        /// <summary>
        /// 写入全部 Definition。
        /// </summary>
        private static void WriteDefinitions(
            CanonicalBinaryWriter writer,
            IReadOnlyList<CanonicalSemanticDefinition> definitions)
        {
            writer.WriteCount(definitions.Count);
            for (var index = 0; index < definitions.Count; index++)
                WriteDefinition(writer, definitions[index]);
        }

        /// <summary>
        /// 写入单个 Definition 的字段树与 sealed compile 结果。
        /// </summary>
        private static void WriteDefinition(CanonicalBinaryWriter writer, CanonicalSemanticDefinition definition)
        {
            writer.WriteInt32(definition.DefinitionOrdinal);
            WriteDefinitionKey(writer, definition.Key);
            WriteProvenance(writer, definition.Provenance);
            WriteFields(writer, definition.Fields);
            WriteRoles(writer, definition.Roles);
            WritePrograms(writer, definition.Programs);
            WriteDependencies(writer, definition.Dependencies);
        }

        /// <summary>
        /// 读取全部 Definition。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticDefinition> ReadDefinitions(CanonicalBinaryReader reader)
        {
            var result = new CanonicalSemanticDefinition[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
                result[index] = ReadDefinition(reader);
            return result;
        }

        /// <summary>
        /// 读取单个 Definition 的字段树与 sealed compile 结果。
        /// </summary>
        private static CanonicalSemanticDefinition ReadDefinition(CanonicalBinaryReader reader)
        {
            var ordinal = reader.ReadInt32();
            var key = ReadDefinitionKey(reader);
            var provenance = ReadProvenance(reader);
            var fields = ReadFields(reader);
            var roles = ReadRoles(reader);
            var programs = ReadPrograms(reader);
            var dependencies = ReadDependencies(reader);
            return new CanonicalSemanticDefinition(
                ordinal, key, fields, roles, programs, dependencies, provenance);
        }

        /// <summary>
        /// 写入一个复合 DefinitionKey。
        /// </summary>
        internal static void WriteDefinitionKey(CanonicalBinaryWriter writer, CanonicalDefinitionKey key)
        {
            writer.WriteInt32(key.DomainOrdinal);
            writer.WriteInt32((int)key.DefinitionKind);
            writer.WriteCount(key.StableIdParts.Count);
            for (var index = 0; index < key.StableIdParts.Count; index++)
                writer.WriteInt64(key.StableIdParts[index]);
        }

        /// <summary>
        /// 读取一个复合 DefinitionKey。
        /// </summary>
        internal static CanonicalDefinitionKey ReadDefinitionKey(CanonicalBinaryReader reader)
        {
            var domainOrdinal = reader.ReadInt32();
            var kind = (CanonicalDefinitionKind)reader.ReadInt32();
            var parts = new long[reader.ReadCount()];
            for (var index = 0; index < parts.Length; index++)
                parts[index] = reader.ReadInt64();
            return new CanonicalDefinitionKey(domainOrdinal, kind, parts);
        }

        /// <summary>
        /// 写入字段分类、裁决与完整 tree。
        /// </summary>
        private static void WriteFields(CanonicalBinaryWriter writer, IReadOnlyList<CanonicalSemanticField> fields)
        {
            writer.WriteCount(fields.Count);
            for (var index = 0; index < fields.Count; index++)
            {
                var field = fields[index];
                writer.WriteInt32(field.FieldOrdinal);
                writer.WriteString(field.FieldId);
                writer.WriteByte((byte)field.Classification);
                writer.WriteByte((byte)field.Disposition);
                WriteNode(writer, field.Root, 0);
                WriteProvenance(writer, field.Provenance);
            }
        }

        /// <summary>
        /// 读取字段分类、裁决与完整 tree。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticField> ReadFields(CanonicalBinaryReader reader)
        {
            var result = new CanonicalSemanticField[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new CanonicalSemanticField(
                    reader.ReadInt32(), reader.ReadString(),
                    (CanonicalFieldClassification)reader.ReadByte(),
                    (CanonicalFieldDisposition)reader.ReadByte(), ReadNode(reader, 0), ReadProvenance(reader));
            }

            return result;
        }

        /// <summary>
        /// 递归写入 absent/scalar/record/variant/collection 节点与逐节点 provenance。
        /// </summary>
        internal static void WriteNode(CanonicalBinaryWriter writer, CanonicalSemanticNode node, int depth)
        {
            if (depth > CanonicalBinaryReader.MaxValueDepth)
                Fail("Canonical semantic tree exceeds the codec depth limit.");
            writer.AccountNode(depth);
            writer.WriteInt32(node.NodeOrdinal);
            writer.WriteString(node.NodeId);
            writer.WriteByte((byte)node.NodeKind);
            writer.WriteString(node.TypeId);
            writer.WriteString(node.TargetDomainId);
            writer.WriteByte((byte)node.CollectionSemantics);
            if (node.NodeKind == CanonicalSemanticNodeKind.Scalar)
                WriteScalarValue(writer, node.ScalarValue);
            writer.WriteCount(node.Children.Count);
            for (var index = 0; index < node.Children.Count; index++)
                WriteNode(writer, node.Children[index], depth + 1);
            WriteProvenance(writer, node.Provenance);
        }

        /// <summary>
        /// 递归读取 semantic tree 节点。
        /// </summary>
        internal static CanonicalSemanticNode ReadNode(CanonicalBinaryReader reader, int depth)
        {
            if (depth > CanonicalBinaryReader.MaxValueDepth)
                Fail("Canonical semantic tree exceeds the codec depth limit.");
            reader.AccountNode(depth);
            var ordinal = reader.ReadInt32();
            var nodeId = reader.ReadString();
            var kind = (CanonicalSemanticNodeKind)reader.ReadByte();
            var typeId = reader.ReadString();
            var targetDomain = reader.ReadString();
            var semantics = (CanonicalCollectionSemantics)reader.ReadByte();
            var scalar = kind == CanonicalSemanticNodeKind.Scalar ? ReadScalarValue(reader) : null;
            var children = new CanonicalSemanticNode[reader.ReadCount()];
            for (var index = 0; index < children.Length; index++)
                children[index] = ReadNode(reader, depth + 1);
            var provenance = ReadProvenance(reader);
            return new CanonicalSemanticNode(
                ordinal, nodeId, kind, typeId, targetDomain, semantics, scalar, children, provenance);
        }

        /// <summary>
        /// 写入 compiler 推导的角色。
        /// </summary>
        private static void WriteRoles(CanonicalBinaryWriter writer, IReadOnlyList<CanonicalSemanticRole> roles)
        {
            writer.WriteCount(roles.Count);
            for (var index = 0; index < roles.Count; index++)
            {
                var role = roles[index];
                writer.WriteInt32(role.RoleOrdinal);
                writer.WriteByte((byte)role.RoleKind);
                WriteDefinitionKey(writer, role.OwnerDefinition);
                writer.WriteInt32(role.OwnerFieldOrdinal);
                writer.WriteString(role.RuleId);
                writer.WriteUInt32(role.RuleVersion);
                WriteProvenance(writer, role.Provenance);
            }
        }

        /// <summary>
        /// 读取 compiler 推导的角色。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticRole> ReadRoles(CanonicalBinaryReader reader)
        {
            var result = new CanonicalSemanticRole[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new CanonicalSemanticRole(
                    reader.ReadInt32(), (CanonicalDefinitionRoleKind)reader.ReadByte(),
                    ReadDefinitionKey(reader), reader.ReadInt32(), reader.ReadString(),
                    reader.ReadUInt32(), ReadProvenance(reader));
            }

            return result;
        }

        /// <summary>
        /// 写入 program 及完整上下文矩阵实例。
        /// </summary>
        private static void WritePrograms(CanonicalBinaryWriter writer, IReadOnlyList<CanonicalSemanticProgram> programs)
        {
            writer.WriteCount(programs.Count);
            for (var index = 0; index < programs.Count; index++)
            {
                var program = programs[index];
                writer.WriteInt32(program.ProgramOrdinal);
                writer.WriteString(program.ProgramId);
                writer.WriteByte((byte)program.ProgramKind);
                writer.WriteByte((byte)program.Disposition);
                WriteDecisions(writer, program.Decisions);
                WriteProgramNodes(writer, program.Nodes);
                WriteProgramEdges(writer, program.Edges);
                WriteProvenance(writer, program.Provenance);
            }
        }

        /// <summary>
        /// 读取 program 及完整上下文矩阵实例。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticProgram> ReadPrograms(CanonicalBinaryReader reader)
        {
            var result = new CanonicalSemanticProgram[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new CanonicalSemanticProgram(
                    reader.ReadInt32(), reader.ReadString(), (CanonicalProgramKind)reader.ReadByte(),
                    (CanonicalFieldDisposition)reader.ReadByte(), ReadDecisions(reader),
                    ReadProgramNodes(reader), ReadProgramEdges(reader), ReadProvenance(reader));
            }

            return result;
        }

        /// <summary>
        /// 写入 program 的完整逐字段 contextual decisions。
        /// </summary>
        private static void WriteDecisions(
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
                WriteProvenance(writer, decision.Provenance);
            }
        }

        /// <summary>
        /// 读取 program 的完整逐字段 contextual decisions。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticContextDecision> ReadDecisions(CanonicalBinaryReader reader)
        {
            var result = new CanonicalSemanticContextDecision[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new CanonicalSemanticContextDecision(
                    reader.ReadInt32(), reader.ReadInt32(), reader.ReadString(), reader.ReadBoolean(),
                    (CanonicalFieldClassification)reader.ReadByte(), (CanonicalFieldDisposition)reader.ReadByte(),
                    reader.ReadString(), reader.ReadUInt32(), ReadProvenance(reader));
            }

            return result;
        }

        /// <summary>
        /// 写入 program nodes、read sets 与 projection binding。
        /// </summary>
        private static void WriteProgramNodes(
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
                WriteIntOrdinals(writer, node.FieldOrdinals);
                WriteIntOrdinals(writer, node.DependencyOrdinals);
                WriteProjection(writer, node.Projection);
                WriteProvenance(writer, node.Provenance);
            }
        }

        /// <summary>
        /// 读取 program nodes、read sets 与 projection binding。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticProgramNode> ReadProgramNodes(CanonicalBinaryReader reader)
        {
            var result = new CanonicalSemanticProgramNode[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
            {
                reader.AccountNode();
                result[index] = new CanonicalSemanticProgramNode(
                    reader.ReadInt32(), reader.ReadString(), ReadIntOrdinals(reader),
                    ReadIntOrdinals(reader), ReadProjection(reader), ReadProvenance(reader));
            }

            return result;
        }

        /// <summary>
        /// 写入 projection 的 owner、ValueView、phase、bounds、payload 与规则身份。
        /// </summary>
        private static void WriteProjection(
            CanonicalBinaryWriter writer,
            CanonicalSemanticProjectionBinding projection)
        {
            writer.WriteInt32(projection.ProjectionOrdinal);
            WriteDefinitionKey(writer, projection.OwnerDefinition);
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
            WriteProvenance(writer, projection.Provenance);
        }

        /// <summary>
        /// 读取 projection 的 owner、ValueView、phase、bounds、payload 与规则身份。
        /// </summary>
        private static CanonicalSemanticProjectionBinding ReadProjection(CanonicalBinaryReader reader)
        {
            return new CanonicalSemanticProjectionBinding(
                reader.ReadInt32(), ReadDefinitionKey(reader), reader.ReadInt32(), reader.ReadInt32(),
                reader.ReadInt32(), reader.ReadString(), reader.ReadString(), reader.ReadString(),
                reader.ReadString(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadBytes(),
                reader.ReadString(), reader.ReadUInt32(), ReadProvenance(reader));
        }

        /// <summary>
        /// 写入 program DAG 边。
        /// </summary>
        private static void WriteProgramEdges(
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
                WriteProvenance(writer, edge.Provenance);
            }
        }

        /// <summary>
        /// 读取 program DAG 边。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticProgramEdge> ReadProgramEdges(CanonicalBinaryReader reader)
        {
            var result = new CanonicalSemanticProgramEdge[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
            {
                reader.AccountEdge();
                result[index] = new CanonicalSemanticProgramEdge(
                    reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
                    (CanonicalProgramEdgeKind)reader.ReadByte(), ReadProvenance(reader));
            }

            return result;
        }

        /// <summary>
        /// 写入跨 Definition dependency 的完整 sealed 元数据。
        /// </summary>
        private static void WriteDependencies(
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
                WriteDefinitionKey(writer, dependency.TargetDefinition);
                writer.WriteString(dependency.TargetDomainId);
                writer.WriteByte((byte)dependency.OwnerKind);
                writer.WriteByte((byte)dependency.Sign);
                writer.WriteByte((byte)dependency.WorkKind);
                writer.WriteByte((byte)dependency.CleanupPolicy);
                writer.WriteInt32(dependency.MaxExpansion);
                WriteProvenance(writer, dependency.Provenance);
            }
        }

        /// <summary>
        /// 读取跨 Definition dependency 的完整 sealed 元数据。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticDependency> ReadDependencies(CanonicalBinaryReader reader)
        {
            var result = new CanonicalSemanticDependency[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
            {
                reader.AccountEdge();
                result[index] = new CanonicalSemanticDependency(
                    reader.ReadInt32(), reader.ReadString(), reader.ReadInt32(),
                    (CanonicalDependencyKind)reader.ReadByte(), ReadDefinitionKey(reader), reader.ReadString(),
                    (CanonicalDependencyOwnerKind)reader.ReadByte(), (CanonicalDependencySign)reader.ReadByte(),
                    (CanonicalDependencyWorkKind)reader.ReadByte(), (CanonicalCleanupPolicy)reader.ReadByte(),
                    reader.ReadInt32(), ReadProvenance(reader));
            }

            return result;
        }

        /// <summary>
        /// 写入一组显式 read-set ordinal。
        /// </summary>
        private static void WriteIntOrdinals(CanonicalBinaryWriter writer, IReadOnlyList<int> values)
        {
            writer.WriteCount(values.Count);
            for (var index = 0; index < values.Count; index++)
                writer.WriteInt32(values[index]);
        }

        /// <summary>
        /// 读取一组显式 read-set ordinal。
        /// </summary>
        private static IReadOnlyList<int> ReadIntOrdinals(CanonicalBinaryReader reader)
        {
            var result = new int[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
                result[index] = reader.ReadInt32();
            return result;
        }

        /// <summary>
        /// 写入完整 provenance，包括排序后的关联 DefinitionKey。
        /// </summary>
        internal static void WriteProvenance(
            CanonicalBinaryWriter writer,
            CanonicalSemanticProvenance provenance)
        {
            writer.WriteString(provenance.WorkbookId);
            writer.WriteString(provenance.TableId);
            writer.WriteString(provenance.RowStableId);
            writer.WriteString(provenance.FieldPath);
            writer.WriteString(provenance.RawValue);
            writer.WriteString(provenance.NormalizedValue);
            writer.WriteString(provenance.RuleId);
            writer.WriteUInt32(provenance.RuleVersion);
            writer.WriteString(provenance.GeneratorVersion);
            writer.WriteCount(provenance.RelatedDefinitionIds.Count);
            for (var index = 0; index < provenance.RelatedDefinitionIds.Count; index++)
                WriteDefinitionKey(writer, provenance.RelatedDefinitionIds[index]);
        }

        /// <summary>
        /// 读取完整 provenance。
        /// </summary>
        internal static CanonicalSemanticProvenance ReadProvenance(CanonicalBinaryReader reader)
        {
            var workbook = reader.ReadString();
            var table = reader.ReadString();
            var row = reader.ReadString();
            var path = reader.ReadString();
            var raw = reader.ReadString();
            var normalized = reader.ReadString();
            var ruleId = reader.ReadString();
            var ruleVersion = reader.ReadUInt32();
            var generator = reader.ReadString();
            var related = new CanonicalDefinitionKey[reader.ReadCount()];
            for (var index = 0; index < related.Length; index++)
                related[index] = ReadDefinitionKey(reader);
            return new CanonicalSemanticProvenance(
                workbook, table, row, path, raw, normalized,
                ruleId, ruleVersion, generator, related);
        }

        /// <summary>
        /// 写入 v2 scalar union；集合只由 semantic tree collection 节点表达。
        /// </summary>
        internal static void WriteScalarValue(CanonicalBinaryWriter writer, CanonicalSemanticValue value)
        {
            writer.WriteByte((byte)value.Kind);
            switch (value.Kind)
            {
                case CanonicalSemanticValueKind.Null:
                    return;
                case CanonicalSemanticValueKind.Boolean:
                case CanonicalSemanticValueKind.Int32:
                case CanonicalSemanticValueKind.UInt32:
                case CanonicalSemanticValueKind.Float32:
                    writer.WriteUInt32((uint)value.RawBits);
                    return;
                case CanonicalSemanticValueKind.Int64:
                case CanonicalSemanticValueKind.UInt64:
                case CanonicalSemanticValueKind.Float64:
                    writer.WriteUInt64(value.RawBits);
                    return;
                case CanonicalSemanticValueKind.String:
                    writer.WriteString(value.StringValue);
                    return;
                case CanonicalSemanticValueKind.Bytes:
                    writer.WriteBytes(value.ByteValueUnsafe);
                    return;
                case CanonicalSemanticValueKind.Enum:
                    WriteEnum(writer, value);
                    return;
                case CanonicalSemanticValueKind.DateTime:
                    writer.WriteInt64(unchecked((long)value.RawBits));
                    writer.WriteInt32((int)value.DateTimeKind);
                    return;
                case CanonicalSemanticValueKind.DefinitionReference:
                    WriteDefinitionKey(writer, value.DefinitionReference);
                    return;
                default:
                    Fail("Unsupported scalar value kind in v2 semantic tree.");
                    return;
            }
        }

        /// <summary>
        /// 读取 v2 scalar union；未知或旧标签稳定拒绝。
        /// </summary>
        internal static CanonicalSemanticValue ReadScalarValue(CanonicalBinaryReader reader)
        {
            var kind = (CanonicalSemanticValueKind)reader.ReadByte();
            switch (kind)
            {
                case CanonicalSemanticValueKind.Null:
                    return CanonicalSemanticValue.Null();
                case CanonicalSemanticValueKind.Boolean:
                    return ReadBooleanValue(reader);
                case CanonicalSemanticValueKind.Int32:
                    return CanonicalSemanticValue.Int32(unchecked((int)reader.ReadUInt32()));
                case CanonicalSemanticValueKind.UInt32:
                    return CanonicalSemanticValue.UInt32(reader.ReadUInt32());
                case CanonicalSemanticValueKind.Int64:
                    return CanonicalSemanticValue.Int64(unchecked((long)reader.ReadUInt64()));
                case CanonicalSemanticValueKind.UInt64:
                    return CanonicalSemanticValue.UInt64(reader.ReadUInt64());
                case CanonicalSemanticValueKind.Float32:
                    return CanonicalSemanticValue.Float32Bits(reader.ReadUInt32());
                case CanonicalSemanticValueKind.Float64:
                    return CanonicalSemanticValue.Float64Bits(reader.ReadUInt64());
                case CanonicalSemanticValueKind.String:
                    return CanonicalSemanticValue.String(reader.ReadString());
                case CanonicalSemanticValueKind.Bytes:
                    return CanonicalSemanticValue.Bytes(reader.ReadBytes());
                case CanonicalSemanticValueKind.Enum:
                    return ReadEnum(reader);
                case CanonicalSemanticValueKind.DateTime:
                    return ReadDateTime(reader);
                case CanonicalSemanticValueKind.DefinitionReference:
                    return CanonicalSemanticValue.Reference(ReadDefinitionKey(reader));
                default:
                    Fail("Unknown or legacy scalar value kind tag.");
                    return null;
            }
        }

        /// <summary>
        /// 写入 enum type、底层宽度与原始 bits。
        /// </summary>
        private static void WriteEnum(CanonicalBinaryWriter writer, CanonicalSemanticValue value)
        {
            writer.WriteString(value.EnumTypeId);
            writer.WriteByte((byte)value.EnumUnderlyingType);
            switch (value.EnumUnderlyingType)
            {
                case CanonicalEnumUnderlyingType.SByte:
                case CanonicalEnumUnderlyingType.Byte:
                    writer.WriteByte((byte)value.RawBits);
                    return;
                case CanonicalEnumUnderlyingType.Int16:
                case CanonicalEnumUnderlyingType.UInt16:
                    writer.WriteUInt16((ushort)value.RawBits);
                    return;
                case CanonicalEnumUnderlyingType.Int32:
                case CanonicalEnumUnderlyingType.UInt32:
                    writer.WriteUInt32((uint)value.RawBits);
                    return;
                case CanonicalEnumUnderlyingType.Int64:
                case CanonicalEnumUnderlyingType.UInt64:
                    writer.WriteUInt64(value.RawBits);
                    return;
                default:
                    Fail("Unknown enum underlying type tag.");
                    return;
            }
        }

        /// <summary>
        /// 读取 enum type、底层宽度与原始 bits。
        /// </summary>
        private static CanonicalSemanticValue ReadEnum(CanonicalBinaryReader reader)
        {
            var typeId = reader.ReadString();
            var underlying = (CanonicalEnumUnderlyingType)reader.ReadByte();
            ulong rawBits;
            switch (underlying)
            {
                case CanonicalEnumUnderlyingType.SByte:
                case CanonicalEnumUnderlyingType.Byte:
                    rawBits = reader.ReadByte();
                    break;
                case CanonicalEnumUnderlyingType.Int16:
                case CanonicalEnumUnderlyingType.UInt16:
                    rawBits = reader.ReadUInt16();
                    break;
                case CanonicalEnumUnderlyingType.Int32:
                case CanonicalEnumUnderlyingType.UInt32:
                    rawBits = reader.ReadUInt32();
                    break;
                case CanonicalEnumUnderlyingType.Int64:
                case CanonicalEnumUnderlyingType.UInt64:
                    rawBits = reader.ReadUInt64();
                    break;
                default:
                    Fail("Unknown enum underlying type tag.");
                    return null;
            }

            return CanonicalSemanticValue.Enum(typeId, underlying, rawBits);
        }

        /// <summary>
        /// 读取并校验 canonical 布尔的固定表示。
        /// </summary>
        private static CanonicalSemanticValue ReadBooleanValue(CanonicalBinaryReader reader)
        {
            var raw = reader.ReadUInt32();
            if (raw > 1)
                Fail("Canonical boolean must be encoded as zero or one.");
            return CanonicalSemanticValue.Boolean(raw == 1);
        }

        /// <summary>
        /// 读取 ticks 与 kind 并拒绝无效 DateTime 表达。
        /// </summary>
        private static CanonicalSemanticValue ReadDateTime(CanonicalBinaryReader reader)
        {
            var ticks = reader.ReadInt64();
            var kind = (DateTimeKind)reader.ReadInt32();
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                Fail("Invalid canonical DateTime ticks.");
            if (kind != DateTimeKind.Unspecified && kind != DateTimeKind.Utc && kind != DateTimeKind.Local)
                Fail("Invalid canonical DateTime kind.");
            return CanonicalSemanticValue.DateTime(new DateTime(ticks, kind));
        }

        /// <summary>
        /// 比较两个 byte 数组的完整内容。
        /// </summary>
        internal static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (var index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 比较原始 payload 与 canonical 重新编码结果。
        /// </summary>
        private static void EnsureCanonicalRoundTrip(byte[] original, byte[] canonical)
        {
            if (!BytesEqual(original, canonical))
                Fail("Decoded graph was not in canonical byte form.");
        }

        /// <summary>
        /// 抛出稳定 CFG1501 codec 异常。
        /// </summary>
        private static void Fail(string message)
        {
            throw new CanonicalSemanticValidationException(
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                message);
        }
    }
}
