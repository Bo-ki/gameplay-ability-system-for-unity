using System;
using System.Collections.Generic;
using GAS.Editor.CodeGen.Semantics;

namespace GAS.Editor.CodeGen.Proofs.Typed
{
    /// <summary>
    /// 对 v2 TypedContract 与 ContractMatrix 执行 canonical 编解码并强制 external expected hash。
    /// </summary>
    public static class TypedContractBinaryCodec
    {
        public const string RegistryHashDomain = "EX-GAS-CanonicalRegistryHash-v2";

        private static readonly byte[] ContractMagic =
        {
            0x45, 0x58, 0x47, 0x53, 0x54, 0x59, 0x50, 0x43,
        };

        /// <summary>
        /// 编码完整合同 bytes，并核对 detached TypedContractHash 与 canonical preimage。
        /// </summary>
        public static byte[] Encode(TypedContract contract)
        {
            ValidateTypedBudget(contract);
            ValidateStructure(contract);
            ValidateCanonicalPayloads(contract);
            var bytes = EncodePayload(contract);
            EnsureExpectedHash(bytes, contract.TypedContractHash);
            return bytes;
        }

        /// <summary>
        /// 仅供同程序集强入口与测试解析 v2 TypedContract；该 API 不证明它与某个 graph 匹配。
        /// </summary>
        internal static TypedContract Decode(byte[] bytes, string expectedTypedContractHash)
        {
            try
            {
                if (bytes == null)
                    throw new ArgumentNullException(nameof(bytes));
                CanonicalSemanticResourceBudget.RequireWireLength(bytes.Length, "TypedContract decode");
                EnsureExpectedHash(bytes, expectedTypedContractHash);
                var reader = new CanonicalBinaryReader(bytes);
                ReadAndValidateHeader(reader);
                var contract = ReadContract(reader, expectedTypedContractHash);
                reader.EnsureComplete();
                ValidateTypedBudget(contract);
                ValidateStructure(contract);
                ValidateCanonicalPayloads(contract);
                EnsureSameBytes(bytes, EncodePayload(contract));
                return contract;
            }
            catch (CanonicalSemanticValidationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Fail("TypedContract decode failed: " + exception.Message);
                return null;
            }
        }

        /// <summary>
        /// 以唯一强入口解码并强制同一 graph、外部 hash 与全部 canonical payload 匹配。
        /// </summary>
        public static TypedContract DecodeAndEnsureMatches(
            byte[] bytes,
            string expectedTypedContractHash,
            CanonicalNormalizedSemanticGraph graph)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            var contract = Decode(bytes, expectedTypedContractHash);
            TypedContractBuilder.EnsureMatches(graph, contract, expectedTypedContractHash);
            return contract;
        }

        /// <summary>
        /// 强制 canonical bytes 的 detached hash 等于外部 expected TypedContractHash。
        /// </summary>
        public static void EnsureExpectedHash(byte[] bytes, string expectedTypedContractHash)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            CanonicalSemanticResourceBudget.RequireWireLength(bytes.Length, "TypedContract hash input");
            CanonicalSemanticIdentity.RequireHash(
                expectedTypedContractHash,
                nameof(expectedTypedContractHash));
            var actual = ComputeTypedContractHash(bytes);
            if (!string.Equals(actual, expectedTypedContractHash, StringComparison.Ordinal))
                Fail("TypedContract bytes do not match the external expected hash.");
        }

        /// <summary>
        /// 对不含自哈希字段的完整 canonical TypedContract bytes 计算 detached hash。
        /// </summary>
        public static string ComputeTypedContractHash(byte[] bytes)
        {
            return CanonicalSemanticHash.Compute(
                CanonicalSemanticVersions.TypedContractHashDomain,
                bytes);
        }

        /// <summary>
        /// 编码不含 TypedContractHash 自身的 canonical preimage，供 builder 冻结 detached hash。
        /// </summary>
        internal static byte[] EncodePayload(TypedContract contract)
        {
            var writer = new CanonicalBinaryWriter();
            WriteHeader(writer);
            writer.WriteUInt16(contract.SourceContractVersion);
            WriteToolchain(writer, contract.ToolchainIdentity);
            writer.WriteString(contract.GeneratorVersion);
            writer.WriteString(contract.SourceInputHash);
            WriteCoverage(writer, contract.Coverage);
            WriteIdentity(writer, contract);
            writer.WriteBytes(contract.ContractMatrixPayloadUnsafe);
            writer.WriteBytes(contract.SchemaProjectionPayloadUnsafe);
            writer.WriteBytes(contract.ContentProjectionPayloadUnsafe);
            WriteDefinitions(writer, contract.Definitions);
            return writer.ToArray();
        }

        /// <summary>
        /// 编码完整 v2 support matrix payload，实例数据不能改变其 bytes。
        /// </summary>
        public static byte[] EncodeContractMatrix()
        {
            return CanonicalSemanticProjectionCodec.EncodeRegistry();
        }

        /// <summary>
        /// 计算当前 v2 registry payload 的独立身份。
        /// </summary>
        public static string ComputeRegistryHash()
        {
            return CanonicalSemanticHash.Compute(
                RegistryHashDomain,
                CanonicalSemanticProjectionCodec.EncodeRegistry());
        }

        /// <summary>
        /// 计算当前 fixed ContractMatrixHash。
        /// </summary>
        public static string ComputeMatrixHash()
        {
            return ComputeMatrixHash(EncodeContractMatrix());
        }

        /// <summary>
        /// 对明确 matrix payload 计算版本化 ContractMatrixHash。
        /// </summary>
        public static string ComputeMatrixHash(byte[] matrixPayload)
        {
            return CanonicalSemanticHash.Compute(
                CanonicalSemanticVersions.ContractMatrixHashDomain,
                matrixPayload);
        }

        /// <summary>
        /// 写入 v2 contract header。
        /// </summary>
        private static void WriteHeader(CanonicalBinaryWriter writer)
        {
            writer.WriteRaw(ContractMagic);
            writer.WriteUInt16(CanonicalSemanticVersions.TypedContractCodecVersion);
            writer.WriteString(CanonicalSemanticVersions.TypedContractSchema);
            writer.WriteString(CanonicalSemanticVersions.TypedContractAbi);
            writer.WriteString(CanonicalSemanticVersions.RuleDomain);
            writer.WriteUInt16(CanonicalSemanticVersions.ResourceBudgetVersion);
            writer.WriteString(CanonicalSemanticVersions.ResourceBudgetId);
        }

        /// <summary>
        /// 校验 v2 codec/schema/ABI/rule-domain header，v1 bytes 必须被拒绝。
        /// </summary>
        private static void ReadAndValidateHeader(CanonicalBinaryReader reader)
        {
            reader.ExpectRaw(ContractMagic);
            var version = reader.ReadUInt16();
            var schema = reader.ReadString();
            var abi = reader.ReadString();
            var ruleDomain = reader.ReadString();
            var resourceBudgetVersion = reader.ReadUInt16();
            var resourceBudgetId = reader.ReadString();
            if (version != CanonicalSemanticVersions.TypedContractCodecVersion
                || !string.Equals(schema, CanonicalSemanticVersions.TypedContractSchema, StringComparison.Ordinal)
                || !string.Equals(abi, CanonicalSemanticVersions.TypedContractAbi, StringComparison.Ordinal)
                || !string.Equals(ruleDomain, CanonicalSemanticVersions.RuleDomain, StringComparison.Ordinal)
                || resourceBudgetVersion != CanonicalSemanticVersions.ResourceBudgetVersion
                || !string.Equals(resourceBudgetId, CanonicalSemanticVersions.ResourceBudgetId, StringComparison.Ordinal))
            {
                Fail("Unsupported TypedContract codec/schema/ABI/rule domain.");
            }
        }

        /// <summary>
        /// 写入结构化 toolchain identity。
        /// </summary>
        private static void WriteToolchain(
            CanonicalBinaryWriter writer,
            CanonicalSemanticToolchainIdentity identity)
        {
            writer.WriteString(identity.LubanProductVersion);
            writer.WriteString(identity.LubanBinaryHash);
            writer.WriteString(identity.SchemaInputHash);
            writer.WriteString(identity.GeneratorIdentity);
        }

        /// <summary>
        /// 读取结构化 toolchain identity。
        /// </summary>
        private static CanonicalSemanticToolchainIdentity ReadToolchain(CanonicalBinaryReader reader)
        {
            return new CanonicalSemanticToolchainIdentity(
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString());
        }

        /// <summary>
        /// 写入五项 coverage/eligibility 状态。
        /// </summary>
        private static void WriteCoverage(
            CanonicalBinaryWriter writer,
            TypedSemanticCoverageContract coverage)
        {
            writer.WriteByte((byte)coverage.SchemaCoverage);
            writer.WriteByte((byte)coverage.ReferenceCoverage);
            writer.WriteByte((byte)coverage.ProgramCoverage);
            writer.WriteByte((byte)coverage.SameParseCoverage);
            writer.WriteByte((byte)coverage.Eligibility);
        }

        /// <summary>
        /// 读取五项 coverage/eligibility 状态。
        /// </summary>
        private static TypedSemanticCoverageContract ReadCoverage(CanonicalBinaryReader reader)
        {
            return new TypedSemanticCoverageContract(
                (CanonicalSemanticCoverageState)reader.ReadByte(),
                (CanonicalSemanticCoverageState)reader.ReadByte(),
                (CanonicalSemanticCoverageState)reader.ReadByte(),
                (CanonicalSemanticCoverageState)reader.ReadByte(),
                (CanonicalSemanticCoverageState)reader.ReadByte());
        }

        /// <summary>
        /// 写入 Graph/Registry/Schema/Content/Matrix 五项身份。
        /// </summary>
        private static void WriteIdentity(CanonicalBinaryWriter writer, TypedContract contract)
        {
            writer.WriteString(contract.GraphHash);
            writer.WriteString(contract.RegistryHash);
            writer.WriteString(contract.SchemaHash);
            writer.WriteString(contract.ContentHash);
            writer.WriteString(contract.ContractMatrixHash);
        }

        /// <summary>
        /// 读取 root identity、payload 与 definitions 并构造 detached-hash contract。
        /// </summary>
        private static TypedContract ReadContract(
            CanonicalBinaryReader reader,
            string expectedTypedContractHash)
        {
            var sourceVersion = reader.ReadUInt16();
            var toolchain = ReadToolchain(reader);
            var generatorVersion = reader.ReadString();
            var sourceInputHash = reader.ReadString();
            var coverage = ReadCoverage(reader);
            var graphHash = reader.ReadString();
            var registryHash = reader.ReadString();
            var schemaHash = reader.ReadString();
            var contentHash = reader.ReadString();
            var matrixHash = reader.ReadString();
            var matrixPayload = reader.ReadBytes();
            var schemaPayload = reader.ReadBytes();
            var contentPayload = reader.ReadBytes();
            var definitions = ReadDefinitions(reader);
            return new TypedContract(
                sourceVersion,
                toolchain,
                generatorVersion,
                sourceInputHash,
                coverage,
                graphHash,
                registryHash,
                schemaHash,
                contentHash,
                matrixHash,
                expectedTypedContractHash,
                matrixPayload,
                schemaPayload,
                contentPayload,
                definitions);
        }

        /// <summary>
        /// 写入全部 Definition contracts。
        /// </summary>
        private static void WriteDefinitions(
            CanonicalBinaryWriter writer,
            IReadOnlyList<TypedDefinitionContract> definitions)
        {
            writer.WriteCount(definitions.Count);
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                writer.WriteInt32(definition.DefinitionOrdinal);
                CanonicalSemanticBinaryCodec.WriteDefinitionKey(writer, definition.DefinitionKey);
                CanonicalSemanticBinaryCodec.WriteProvenance(writer, definition.Provenance);
                WriteFields(writer, definition.Fields);
                WriteRoles(writer, definition.Roles);
                WritePrograms(writer, definition.Programs);
                WriteDependencies(writer, definition.Dependencies);
            }
        }

        /// <summary>
        /// 读取全部 Definition contracts。
        /// </summary>
        private static IReadOnlyList<TypedDefinitionContract> ReadDefinitions(CanonicalBinaryReader reader)
        {
            var result = new TypedDefinitionContract[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
            {
                var ordinal = reader.ReadInt32();
                var key = CanonicalSemanticBinaryCodec.ReadDefinitionKey(reader);
                var provenance = CanonicalSemanticBinaryCodec.ReadProvenance(reader);
                var fields = ReadFields(reader);
                var roles = ReadRoles(reader);
                var programs = ReadPrograms(reader);
                var dependencies = ReadDependencies(reader);
                result[index] = new TypedDefinitionContract(
                    ordinal,
                    key,
                    fields,
                    roles,
                    programs,
                    dependencies,
                    provenance);
            }

            return result;
        }

        /// <summary>
        /// 写入字段 typed shape 与分类裁决。
        /// </summary>
        private static void WriteFields(
            CanonicalBinaryWriter writer,
            IReadOnlyList<TypedFieldContract> fields)
        {
            writer.WriteCount(fields.Count);
            for (var index = 0; index < fields.Count; index++)
            {
                var field = fields[index];
                writer.AccountNode();
                writer.WriteInt32(field.FieldOrdinal);
                writer.WriteString(field.FieldId);
                writer.WriteByte((byte)field.NodeKind);
                writer.WriteString(field.TypeId);
                writer.WriteString(field.TargetDomainId);
                writer.WriteByte((byte)field.CollectionSemantics);
                writer.WriteByte((byte)field.Classification);
                writer.WriteByte((byte)field.Disposition);
                WriteRuleAndProvenance(writer, field.RuleId, field.RuleVersion, field.Provenance);
            }
        }

        /// <summary>
        /// 读取字段 typed contracts。
        /// </summary>
        private static IReadOnlyList<TypedFieldContract> ReadFields(CanonicalBinaryReader reader)
        {
            var result = new TypedFieldContract[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
            {
                reader.AccountNode();
                var ordinal = reader.ReadInt32();
                var fieldId = reader.ReadString();
                var nodeKind = (CanonicalSemanticNodeKind)reader.ReadByte();
                var typeId = reader.ReadString();
                var targetDomain = reader.ReadString();
                var semantics = (CanonicalCollectionSemantics)reader.ReadByte();
                var classification = (CanonicalFieldClassification)reader.ReadByte();
                var disposition = (CanonicalFieldDisposition)reader.ReadByte();
                var rule = ReadRuleAndProvenance(reader);
                result[index] = new TypedFieldContract(
                    ordinal,
                    fieldId,
                    nodeKind,
                    typeId,
                    targetDomain,
                    semantics,
                    classification,
                    disposition,
                    rule.RuleId,
                    rule.RuleVersion,
                    rule.Provenance);
            }

            return result;
        }

        /// <summary>
        /// 写入 compiler 推导的 roles。
        /// </summary>
        private static void WriteRoles(
            CanonicalBinaryWriter writer,
            IReadOnlyList<TypedRoleContract> roles)
        {
            writer.WriteCount(roles.Count);
            for (var index = 0; index < roles.Count; index++)
            {
                var role = roles[index];
                writer.WriteInt32(role.RoleOrdinal);
                writer.WriteByte((byte)role.RoleKind);
                CanonicalSemanticBinaryCodec.WriteDefinitionKey(writer, role.OwnerDefinition);
                writer.WriteInt32(role.OwnerFieldOrdinal);
                WriteRuleAndProvenance(writer, role.RuleId, role.RuleVersion, role.Provenance);
            }
        }

        /// <summary>
        /// 读取 compiler 推导的 roles。
        /// </summary>
        private static IReadOnlyList<TypedRoleContract> ReadRoles(CanonicalBinaryReader reader)
        {
            var result = new TypedRoleContract[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
            {
                var ordinal = reader.ReadInt32();
                var kind = (CanonicalDefinitionRoleKind)reader.ReadByte();
                var owner = CanonicalSemanticBinaryCodec.ReadDefinitionKey(reader);
                var fieldOrdinal = reader.ReadInt32();
                var rule = ReadRuleAndProvenance(reader);
                result[index] = new TypedRoleContract(
                    ordinal,
                    kind,
                    owner,
                    fieldOrdinal,
                    rule.RuleId,
                    rule.RuleVersion,
                    rule.Provenance);
            }

            return result;
        }

        /// <summary>
        /// 写入 programs、contextual decisions、nodes、projection bindings 与 edges。
        /// </summary>
        private static void WritePrograms(
            CanonicalBinaryWriter writer,
            IReadOnlyList<TypedProgramContract> programs)
        {
            writer.WriteCount(programs.Count);
            for (var index = 0; index < programs.Count; index++)
            {
                var program = programs[index];
                writer.WriteInt32(program.ProgramOrdinal);
                writer.WriteString(program.ProgramId);
                writer.WriteByte((byte)program.ProgramKind);
                writer.WriteByte((byte)program.Disposition);
                writer.WriteInt32(program.NodeStartOrdinal);
                writer.WriteInt32(program.NodeCount);
                writer.WriteInt32(program.EdgeStartOrdinal);
                writer.WriteInt32(program.EdgeCount);
                WriteRuleAndProvenance(writer, program.RuleId, program.RuleVersion, program.Provenance);
                WriteDecisions(writer, program.Decisions);
                WriteProgramNodes(writer, program.Nodes);
                WriteProgramEdges(writer, program.Edges);
            }
        }

        /// <summary>
        /// 读取 programs 并拒绝非零 local range 或 count 漂移。
        /// </summary>
        private static IReadOnlyList<TypedProgramContract> ReadPrograms(CanonicalBinaryReader reader)
        {
            var result = new TypedProgramContract[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
                result[index] = ReadProgram(reader);
            return result;
        }

        /// <summary>
        /// 读取一个完整 typed program。
        /// </summary>
        private static TypedProgramContract ReadProgram(CanonicalBinaryReader reader)
        {
            var ordinal = reader.ReadInt32();
            var id = reader.ReadString();
            var kind = (CanonicalProgramKind)reader.ReadByte();
            var disposition = (CanonicalFieldDisposition)reader.ReadByte();
            var nodeStart = reader.ReadInt32();
            var nodeCount = reader.ReadInt32();
            var edgeStart = reader.ReadInt32();
            var edgeCount = reader.ReadInt32();
            var rule = ReadRuleAndProvenance(reader);
            var decisions = ReadDecisions(reader);
            var nodes = ReadProgramNodes(reader);
            var edges = ReadProgramEdges(reader);
            if (nodeStart != 0 || edgeStart != 0 || nodeCount != nodes.Count || edgeCount != edges.Count)
                Fail("Typed program range is non-canonical.");
            return new TypedProgramContract(
                ordinal,
                id,
                kind,
                disposition,
                decisions,
                nodes,
                edges,
                rule.RuleId,
                rule.RuleVersion,
                rule.Provenance);
        }

        /// <summary>
        /// 写入全部 contextual decisions，包括 inactive 与 denied 字段。
        /// </summary>
        private static void WriteDecisions(
            CanonicalBinaryWriter writer,
            IReadOnlyList<TypedContextDecisionContract> decisions)
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
                WriteRuleAndProvenance(writer, decision.RuleId, decision.RuleVersion, decision.Provenance);
            }
        }

        /// <summary>
        /// 读取全部 contextual decisions。
        /// </summary>
        private static IReadOnlyList<TypedContextDecisionContract> ReadDecisions(
            CanonicalBinaryReader reader)
        {
            var result = new TypedContextDecisionContract[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
            {
                var ordinal = reader.ReadInt32();
                var fieldOrdinal = reader.ReadInt32();
                var fieldId = reader.ReadString();
                var isActive = reader.ReadBoolean();
                var classification = (CanonicalFieldClassification)reader.ReadByte();
                var disposition = (CanonicalFieldDisposition)reader.ReadByte();
                var rule = ReadRuleAndProvenance(reader);
                result[index] = new TypedContextDecisionContract(
                    ordinal,
                    fieldOrdinal,
                    fieldId,
                    isActive,
                    classification,
                    disposition,
                    rule.RuleId,
                    rule.RuleVersion,
                    rule.Provenance);
            }

            return result;
        }

        /// <summary>
        /// 写入 operation nodes、read sets 与 projection bindings。
        /// </summary>
        private static void WriteProgramNodes(
            CanonicalBinaryWriter writer,
            IReadOnlyList<TypedProgramNodeContract> nodes)
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
                WriteProjection(writer, node.Projection);
                WriteRuleAndProvenance(writer, node.RuleId, node.RuleVersion, node.Provenance);
            }
        }

        /// <summary>
        /// 读取 operation nodes、read sets 与 projection bindings。
        /// </summary>
        private static IReadOnlyList<TypedProgramNodeContract> ReadProgramNodes(
            CanonicalBinaryReader reader)
        {
            var result = new TypedProgramNodeContract[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
            {
                reader.AccountNode();
                var ordinal = reader.ReadInt32();
                var operationId = reader.ReadString();
                var fields = ReadOrdinals(reader);
                var dependencies = ReadOrdinals(reader);
                var projection = ReadProjection(reader);
                var rule = ReadRuleAndProvenance(reader);
                result[index] = new TypedProgramNodeContract(
                    ordinal,
                    operationId,
                    fields,
                    dependencies,
                    projection,
                    rule.RuleId,
                    rule.RuleVersion,
                    rule.Provenance);
            }

            return result;
        }

        /// <summary>
        /// 写入 projection binding identity、bounds 与 canonical payload。
        /// </summary>
        private static void WriteProjection(
            CanonicalBinaryWriter writer,
            TypedProjectionBindingContract projection)
        {
            writer.WriteBoolean(projection != null);
            if (projection == null)
                return;
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
            WriteRuleAndProvenance(writer, projection.RuleId, projection.RuleVersion, projection.Provenance);
        }

        /// <summary>
        /// 读取 projection binding identity、bounds 与 canonical payload。
        /// </summary>
        private static TypedProjectionBindingContract ReadProjection(CanonicalBinaryReader reader)
        {
            if (!reader.ReadBoolean())
                return null;
            var ordinal = reader.ReadInt32();
            var owner = CanonicalSemanticBinaryCodec.ReadDefinitionKey(reader);
            var programOrdinal = reader.ReadInt32();
            var nodeOrdinal = reader.ReadInt32();
            var fieldOrdinal = reader.ReadInt32();
            var operationId = reader.ReadString();
            var valueView = reader.ReadString();
            var phase = reader.ReadString();
            var targetDomain = reader.ReadString();
            var maxItems = reader.ReadInt32();
            var maxBytes = reader.ReadInt32();
            var payload = reader.ReadBytes();
            var rule = ReadRuleAndProvenance(reader);
            return new TypedProjectionBindingContract(
                ordinal,
                owner,
                programOrdinal,
                nodeOrdinal,
                fieldOrdinal,
                operationId,
                valueView,
                phase,
                targetDomain,
                maxItems,
                maxBytes,
                payload,
                rule.RuleId,
                rule.RuleVersion,
                rule.Provenance);
        }

        /// <summary>
        /// 写入 program DAG edges。
        /// </summary>
        private static void WriteProgramEdges(
            CanonicalBinaryWriter writer,
            IReadOnlyList<TypedProgramEdgeContract> edges)
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
                WriteRuleAndProvenance(writer, edge.RuleId, edge.RuleVersion, edge.Provenance);
            }
        }

        /// <summary>
        /// 读取 program DAG edges。
        /// </summary>
        private static IReadOnlyList<TypedProgramEdgeContract> ReadProgramEdges(
            CanonicalBinaryReader reader)
        {
            var result = new TypedProgramEdgeContract[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
            {
                reader.AccountEdge();
                var ordinal = reader.ReadInt32();
                var source = reader.ReadInt32();
                var target = reader.ReadInt32();
                var kind = (CanonicalProgramEdgeKind)reader.ReadByte();
                var rule = ReadRuleAndProvenance(reader);
                result[index] = new TypedProgramEdgeContract(
                    ordinal,
                    source,
                    target,
                    kind,
                    rule.RuleId,
                    rule.RuleVersion,
                    rule.Provenance);
            }

            return result;
        }

        /// <summary>
        /// 写入 dependency sealed metadata。
        /// </summary>
        private static void WriteDependencies(
            CanonicalBinaryWriter writer,
            IReadOnlyList<TypedDependencyContract> dependencies)
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
                WriteRuleAndProvenance(writer, dependency.RuleId, dependency.RuleVersion, dependency.Provenance);
            }
        }

        /// <summary>
        /// 读取 dependency sealed metadata。
        /// </summary>
        private static IReadOnlyList<TypedDependencyContract> ReadDependencies(
            CanonicalBinaryReader reader)
        {
            var result = new TypedDependencyContract[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
            {
                reader.AccountEdge();
                result[index] = ReadDependency(reader);
            }
            return result;
        }

        /// <summary>
        /// 读取一条完整 dependency contract。
        /// </summary>
        private static TypedDependencyContract ReadDependency(CanonicalBinaryReader reader)
        {
            var ordinal = reader.ReadInt32();
            var id = reader.ReadString();
            var sourceField = reader.ReadInt32();
            var kind = (CanonicalDependencyKind)reader.ReadByte();
            var target = CanonicalSemanticBinaryCodec.ReadDefinitionKey(reader);
            var targetDomain = reader.ReadString();
            var owner = (CanonicalDependencyOwnerKind)reader.ReadByte();
            var sign = (CanonicalDependencySign)reader.ReadByte();
            var work = (CanonicalDependencyWorkKind)reader.ReadByte();
            var cleanup = (CanonicalCleanupPolicy)reader.ReadByte();
            var maxExpansion = reader.ReadInt32();
            var rule = ReadRuleAndProvenance(reader);
            return new TypedDependencyContract(
                ordinal,
                id,
                sourceField,
                kind,
                target,
                targetDomain,
                owner,
                sign,
                work,
                cleanup,
                maxExpansion,
                rule.RuleId,
                rule.RuleVersion,
                rule.Provenance);
        }

        /// <summary>
        /// 写入 RuleId/version 与 provenance 公共尾部。
        /// </summary>
        private static void WriteRuleAndProvenance(
            CanonicalBinaryWriter writer,
            string ruleId,
            uint ruleVersion,
            CanonicalSemanticProvenance provenance)
        {
            writer.WriteString(ruleId);
            writer.WriteUInt32(ruleVersion);
            CanonicalSemanticBinaryCodec.WriteProvenance(writer, provenance);
        }

        /// <summary>
        /// 读取 RuleId/version 与 provenance 公共尾部。
        /// </summary>
        private static RuleAndProvenance ReadRuleAndProvenance(CanonicalBinaryReader reader)
        {
            return new RuleAndProvenance(
                reader.ReadString(),
                reader.ReadUInt32(),
                CanonicalSemanticBinaryCodec.ReadProvenance(reader));
        }

        /// <summary>
        /// 写入 canonical ordinal 列表。
        /// </summary>
        private static void WriteOrdinals(CanonicalBinaryWriter writer, IReadOnlyList<int> ordinals)
        {
            writer.WriteCount(ordinals.Count);
            for (var index = 0; index < ordinals.Count; index++)
                writer.WriteInt32(ordinals[index]);
        }

        /// <summary>
        /// 读取 canonical ordinal 列表。
        /// </summary>
        private static IReadOnlyList<int> ReadOrdinals(CanonicalBinaryReader reader)
        {
            var result = new int[reader.ReadCount()];
            for (var index = 0; index < result.Length; index++)
                result[index] = reader.ReadInt32();
            return result;
        }

        /// <summary>
        /// 在 Typed validator 创建索引前累计 DTO 的 collection/node/edge/payload 预算。
        /// </summary>
        private static void ValidateTypedBudget(TypedContract contract)
        {
            if (contract == null)
                Fail("TypedContract is required for budget validation.");
            var meter = new CanonicalSemanticBudgetMeter();
            meter.ChargeString(contract.ContractSchema, "typed contract schema");
            meter.ChargeString(contract.ContractAbi, "typed contract ABI");
            meter.ChargeString(contract.RuleDomain, "typed rule domain");
            meter.ChargeString(contract.ResourceBudgetId, "typed resource budget id");
            CanonicalSemanticResourceBudget.ChargeToolchain(
                meter,
                contract.ToolchainIdentity,
                contract.GeneratorVersion,
                contract.SourceInputHash);
            ChargeTypedIdentities(meter, contract);
            meter.ChargePayload(contract.ContractMatrixPayloadUnsafe.Length, "typed matrix payload");
            meter.ChargePayload(contract.SchemaProjectionPayloadUnsafe.Length, "typed schema payload");
            meter.ChargePayload(contract.ContentProjectionPayloadUnsafe.Length, "typed content payload");
            meter.ChargeCollection(contract.Definitions.Count, "typed definitions");
            for (var index = 0; index < contract.Definitions.Count; index++)
                ChargeTypedDefinition(meter, contract.Definitions[index]);
        }

        /// <summary>
        /// 充值 TypedContract 的 graph/registry/schema/content/matrix/detached hash 字符串。
        /// </summary>
        private static void ChargeTypedIdentities(
            CanonicalSemanticBudgetMeter meter,
            TypedContract contract)
        {
            meter.ChargeString(contract.GraphHash, "typed graph hash");
            meter.ChargeString(contract.RegistryHash, "typed registry hash");
            meter.ChargeString(contract.SchemaHash, "typed schema hash");
            meter.ChargeString(contract.ContentHash, "typed content hash");
            meter.ChargeString(contract.ContractMatrixHash, "typed matrix hash");
            meter.ChargeString(contract.TypedContractHash, "typed detached hash");
        }

        /// <summary>
        /// 充值一个 Typed definition 的 root fields、program nodes/edges 与 dependencies。
        /// </summary>
        private static void ChargeTypedDefinition(
            CanonicalSemanticBudgetMeter meter,
            TypedDefinitionContract definition)
        {
            if (definition == null)
                Fail("Typed definition is required for budget validation.");
            CanonicalSemanticResourceBudget.ChargeKey(meter, definition.DefinitionKey);
            CanonicalSemanticResourceBudget.ChargeProvenance(meter, definition.Provenance);
            meter.ChargeCollection(definition.Fields.Count, "typed fields");
            meter.ChargeCollection(definition.Roles.Count, "typed roles");
            meter.ChargeCollection(definition.Programs.Count, "typed programs");
            meter.ChargeCollection(definition.Dependencies.Count, "typed dependencies");
            for (var index = 0; index < definition.Fields.Count; index++)
                ChargeTypedField(meter, definition.Fields[index]);
            for (var index = 0; index < definition.Roles.Count; index++)
                ChargeTypedRole(meter, definition.Roles[index]);
            for (var index = 0; index < definition.Dependencies.Count; index++)
                ChargeTypedDependency(meter, definition.Dependencies[index]);
            for (var index = 0; index < definition.Programs.Count; index++)
                ChargeTypedProgram(meter, definition.Programs[index]);
        }

        /// <summary>
        /// 充值 typed field 的 root shape、规则与 provenance。
        /// </summary>
        private static void ChargeTypedField(
            CanonicalSemanticBudgetMeter meter,
            TypedFieldContract field)
        {
            if (field == null)
                Fail("Typed field is required for budget validation.");
            meter.ChargeNode(0);
            meter.ChargeString(field.FieldId, "typed field id");
            meter.ChargeString(field.TypeId, "typed field type id");
            meter.ChargeString(field.TargetDomainId, "typed field target domain");
            ChargeTypedRule(meter, field.RuleId, field.Provenance, "typed field rule id");
        }

        /// <summary>
        /// 充值 typed role 的 owner key、规则与 provenance。
        /// </summary>
        private static void ChargeTypedRole(
            CanonicalSemanticBudgetMeter meter,
            TypedRoleContract role)
        {
            if (role == null)
                Fail("Typed role is required for budget validation.");
            CanonicalSemanticResourceBudget.ChargeKey(meter, role.OwnerDefinition);
            ChargeTypedRule(meter, role.RuleId, role.Provenance, "typed role rule id");
        }

        /// <summary>
        /// 充值 Typed program 的 decisions、nodes、edges 与 projection payload。
        /// </summary>
        private static void ChargeTypedProgram(
            CanonicalSemanticBudgetMeter meter,
            TypedProgramContract program)
        {
            if (program == null)
                Fail("Typed program is required for budget validation.");
            meter.ChargeString(program.ProgramId, "typed program id");
            ChargeTypedRule(meter, program.RuleId, program.Provenance, "typed program rule id");
            meter.ChargeCollection(program.Decisions.Count, "typed decisions");
            meter.ChargeCollection(program.Nodes.Count, "typed program nodes");
            meter.ChargeCollection(program.Edges.Count, "typed program edges");
            for (var index = 0; index < program.Decisions.Count; index++)
                ChargeTypedDecision(meter, program.Decisions[index]);
            for (var index = 0; index < program.Nodes.Count; index++)
                ChargeTypedProgramNode(meter, program.Nodes[index]);
            for (var index = 0; index < program.Edges.Count; index++)
                ChargeTypedProgramEdge(meter, program.Edges[index]);
        }

        /// <summary>
        /// 充值 typed contextual decision 的字段、规则与 provenance。
        /// </summary>
        private static void ChargeTypedDecision(
            CanonicalSemanticBudgetMeter meter,
            TypedContextDecisionContract decision)
        {
            if (decision == null)
                Fail("Typed decision is required for budget validation.");
            meter.ChargeString(decision.FieldId, "typed decision field id");
            ChargeTypedRule(meter, decision.RuleId, decision.Provenance, "typed decision rule id");
        }

        /// <summary>
        /// 充值 typed program node 的 read-set ordinal、projection、规则与 provenance。
        /// </summary>
        private static void ChargeTypedProgramNode(
            CanonicalSemanticBudgetMeter meter,
            TypedProgramNodeContract node)
        {
            if (node == null)
                Fail("Typed program node is required for budget validation.");
            meter.ChargeNode(0);
            meter.ChargeString(node.OperationId, "typed operation id");
            meter.ChargeCollection(node.FieldOrdinals.Count, "typed field ordinals");
            meter.ChargeCollection(node.DependencyOrdinals.Count, "typed dependency ordinals");
            if (node.Projection != null)
                ChargeTypedProjection(meter, node.Projection);
            ChargeTypedRule(meter, node.RuleId, node.Provenance, "typed node rule id");
        }

        /// <summary>
        /// 充值 typed projection 的 owner、字符串、payload、规则与 provenance。
        /// </summary>
        private static void ChargeTypedProjection(
            CanonicalSemanticBudgetMeter meter,
            TypedProjectionBindingContract projection)
        {
            CanonicalSemanticResourceBudget.ChargeKey(meter, projection.OwnerDefinition);
            meter.ChargeString(projection.OperationId, "typed projection operation id");
            meter.ChargeString(projection.ValueView, "typed projection value view");
            meter.ChargeString(projection.Phase, "typed projection phase");
            meter.ChargeString(projection.TargetDomainId, "typed projection target domain");
            meter.ChargePayload(projection.PayloadUnsafe.Length, "typed projection payload");
            ChargeTypedRule(meter, projection.RuleId, projection.Provenance, "typed projection rule id");
        }

        /// <summary>
        /// 充值 typed program edge 与 governing provenance。
        /// </summary>
        private static void ChargeTypedProgramEdge(
            CanonicalSemanticBudgetMeter meter,
            TypedProgramEdgeContract edge)
        {
            if (edge == null)
                Fail("Typed program edge is required for budget validation.");
            meter.ChargeEdge("typed program edge");
            ChargeTypedRule(meter, edge.RuleId, edge.Provenance, "typed edge rule id");
        }

        /// <summary>
        /// 充值 typed dependency 的 target、字符串、规则、provenance 与 edge 总量。
        /// </summary>
        private static void ChargeTypedDependency(
            CanonicalSemanticBudgetMeter meter,
            TypedDependencyContract dependency)
        {
            if (dependency == null)
                Fail("Typed dependency is required for budget validation.");
            meter.ChargeEdge("typed dependency");
            meter.ChargeString(dependency.DependencyId, "typed dependency id");
            CanonicalSemanticResourceBudget.ChargeKey(meter, dependency.TargetDefinition);
            meter.ChargeString(dependency.TargetDomainId, "typed dependency target domain");
            ChargeTypedRule(meter, dependency.RuleId, dependency.Provenance, "typed dependency rule id");
        }

        /// <summary>
        /// 充值 typed DTO 重复编码的 RuleId 与完整 provenance。
        /// </summary>
        private static void ChargeTypedRule(
            CanonicalSemanticBudgetMeter meter,
            string ruleId,
            CanonicalSemanticProvenance provenance,
            string context)
        {
            meter.ChargeString(ruleId, context);
            CanonicalSemanticResourceBudget.ChargeProvenance(meter, provenance);
        }

        /// <summary>
        /// 验证 TypedContract 根身份、Red coverage 与所有 typed definitions。
        /// </summary>
        private static void ValidateStructure(TypedContract contract)
        {
            if (contract == null)
                Fail("TypedContract is required.");
            if (contract.SourceContractVersion != CanonicalSemanticVersions.LubanSourceContractVersion
                || string.IsNullOrWhiteSpace(contract.GeneratorVersion))
                Fail("TypedContract source protocol or generator is invalid.");
            ValidateToolchain(contract.ToolchainIdentity, contract.GeneratorVersion);
            CanonicalSemanticIdentity.RequireHash(contract.SourceInputHash, nameof(contract.SourceInputHash));
            ValidateCoverage(contract.Coverage);
            CanonicalSemanticIdentity.RequireHash(contract.GraphHash, nameof(contract.GraphHash));
            CanonicalSemanticIdentity.RequireHash(contract.RegistryHash, nameof(contract.RegistryHash));
            CanonicalSemanticIdentity.RequireHash(contract.SchemaHash, nameof(contract.SchemaHash));
            CanonicalSemanticIdentity.RequireHash(contract.ContentHash, nameof(contract.ContentHash));
            CanonicalSemanticIdentity.RequireHash(contract.ContractMatrixHash, nameof(contract.ContractMatrixHash));
            CanonicalSemanticIdentity.RequireHash(contract.TypedContractHash, nameof(contract.TypedContractHash));
            if (contract.Definitions == null || contract.Definitions.Count == 0)
                Fail("TypedContract requires at least one Definition.");
            ValidateDefinitions(contract.Definitions, contract.GeneratorVersion);
        }

        /// <summary>
        /// 验证结构化 toolchain 并绑定 generator identity。
        /// </summary>
        private static void ValidateToolchain(
            CanonicalSemanticToolchainIdentity toolchain,
            string generatorVersion)
        {
            if (toolchain == null
                || string.IsNullOrWhiteSpace(toolchain.LubanProductVersion)
                || string.IsNullOrWhiteSpace(toolchain.GeneratorIdentity)
                || !string.Equals(toolchain.GeneratorIdentity, generatorVersion, StringComparison.Ordinal))
            {
                Fail("TypedContract toolchain identity is incomplete or mismatched.");
            }
            CanonicalSemanticIdentity.RequireHash(toolchain.LubanBinaryHash, nameof(toolchain.LubanBinaryHash));
            CanonicalSemanticIdentity.RequireHash(toolchain.SchemaInputHash, nameof(toolchain.SchemaInputHash));
        }

        /// <summary>
        /// 强制五项 coverage 与 eligibility 保持 Red。
        /// </summary>
        private static void ValidateCoverage(TypedSemanticCoverageContract coverage)
        {
            if (coverage == null
                || coverage.SchemaCoverage != CanonicalSemanticCoverageState.Red
                || coverage.ReferenceCoverage != CanonicalSemanticCoverageState.Red
                || coverage.ProgramCoverage != CanonicalSemanticCoverageState.Red
                || coverage.SameParseCoverage != CanonicalSemanticCoverageState.Red
                || coverage.Eligibility != CanonicalSemanticCoverageState.Red)
            {
                FailRule(
                    CanonicalSemanticRuleCatalog.CapacityProofMissing,
                    "TypedContract coverage and eligibility must remain Red.");
            }
        }

        /// <summary>
        /// 验证 matrix/schema/content payload 与各自 hash，并绑定当前 v2 registry。
        /// </summary>
        private static void ValidateCanonicalPayloads(TypedContract contract)
        {
            var expectedMatrix = EncodeContractMatrix();
            if (!HaveSameBytes(expectedMatrix, contract.ContractMatrixPayloadUnsafe))
                Fail("ContractMatrix payload does not match the current v2 registry.");
            if (!string.Equals(ComputeRegistryHash(), contract.RegistryHash, StringComparison.Ordinal))
                Fail("RegistryHash does not match the current v2 registry.");
            var matrixHash = ComputeMatrixHash(contract.ContractMatrixPayloadUnsafe);
            if (!string.Equals(matrixHash, contract.ContractMatrixHash, StringComparison.Ordinal))
                Fail("ContractMatrixHash does not match its canonical payload.");
            var schemaHash = CanonicalSemanticHash.Compute(
                CanonicalSemanticVersions.SchemaHashDomain,
                contract.SchemaProjectionPayloadUnsafe);
            var expectedSchema = CanonicalSemanticProjectionCodec.EncodeFixedSchema(
                contract.GeneratorVersion,
                contract.ContractMatrixHash);
            if (!HaveSameBytes(expectedSchema, contract.SchemaProjectionPayloadUnsafe))
                Fail("Schema projection payload does not match the fixed v2 registry/rule schema.");
            if (!string.Equals(schemaHash, contract.SchemaHash, StringComparison.Ordinal))
                Fail("SchemaHash does not match its canonical projection payload.");
            var contentHash = CanonicalSemanticHash.Compute(
                CanonicalSemanticVersions.ContentHashDomain,
                contract.ContentProjectionPayloadUnsafe);
            if (!string.Equals(contentHash, contract.ContentHash, StringComparison.Ordinal))
                Fail("ContentHash does not match its canonical projection payload.");
        }

        /// <summary>
        /// 验证 Definition canonical order、domain ordinal、子合同与跨 Definition DAG。
        /// </summary>
        private static void ValidateDefinitions(
            IReadOnlyList<TypedDefinitionContract> definitions,
            string generatorVersion)
        {
            var keys = new CanonicalDefinitionKey[definitions.Count];
            var nextByDomain = new Dictionary<int, int>();
            CanonicalDefinitionKey previous = null;
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                ValidateDefinitionIdentity(definition, previous, nextByDomain);
                keys[index] = definition.DefinitionKey;
                previous = definition.DefinitionKey;
            }
            var indexed = new CanonicalDefinitionLookup<TypedDefinitionContract>(keys);
            for (var index = 0; index < definitions.Count; index++)
                indexed.Add(definitions[index].DefinitionKey, definitions[index]);
            for (var index = 0; index < definitions.Count; index++)
                ValidateDefinitionBody(definitions[index], indexed, generatorVersion);
            ValidateCrossDefinitionCycles(definitions);
        }

        /// <summary>
        /// 验证单个 Definition 的 key 顺序与 domain-local ordinal。
        /// </summary>
        private static void ValidateDefinitionIdentity(
            TypedDefinitionContract definition,
            CanonicalDefinitionKey previous,
            IDictionary<int, int> nextByDomain)
        {
            if (definition == null || definition.DefinitionKey == null)
                Fail("Typed Definition and key are required.");
            var key = definition.DefinitionKey;
            if (!CanonicalSemanticSchemaRegistry.TryResolve(key.DefinitionKind, out var schema)
                || key.DomainOrdinal != schema.DomainOrdinal
                || key.StableIdParts.Count != schema.StableIdPartCount
                || (previous != null && previous.CompareTo(key) >= 0))
                Fail("Typed Definition key/domain/order is invalid.");
            if (!nextByDomain.TryGetValue(key.DomainOrdinal, out var expectedOrdinal))
                expectedOrdinal = 0;
            if (definition.DefinitionOrdinal != expectedOrdinal)
                Fail("Typed DefinitionOrdinal is not dense within its domain.");
            nextByDomain[key.DomainOrdinal] = expectedOrdinal + 1;
        }

        /// <summary>
        /// 验证单个 Definition 的字段、roles、programs、dependencies 与 provenance。
        /// </summary>
        private static void ValidateDefinitionBody(
            TypedDefinitionContract definition,
            CanonicalDefinitionLookup<TypedDefinitionContract> definitions,
            string generatorVersion)
        {
            CanonicalSemanticSchemaRegistry.TryResolve(definition.DefinitionKey.DefinitionKind, out var schema);
            ValidateProvenance(definition.Provenance, generatorVersion, definitions, null, 0);
            ValidateFields(definition.Fields, schema, generatorVersion, definitions);
            ValidateRoles(definition.Roles, definitions, generatorVersion);
            ValidateDependencies(definition.Dependencies, definition.Fields, definitions, generatorVersion);
            ValidatePrograms(definition, schema, definitions, generatorVersion);
        }

        /// <summary>
        /// 验证字段合同完整覆盖 registry 并保留 typed root shape。
        /// </summary>
        private static void ValidateFields(
            IReadOnlyList<TypedFieldContract> fields,
            CanonicalSemanticDomainSchema schema,
            string generatorVersion,
            CanonicalDefinitionLookup<TypedDefinitionContract> definitions)
        {
            if (fields.Count != schema.Fields.Count)
                FailRule(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Typed field coverage is incomplete.");
            for (var index = 0; index < fields.Count; index++)
            {
                var field = fields[index];
                var expected = schema.Fields[index];
                if (field == null
                    || field.FieldOrdinal != index
                    || !string.Equals(field.FieldId, expected.FieldId, StringComparison.Ordinal)
                    || field.Classification != expected.Classification
                    || field.Disposition != expected.Disposition
                    || !string.Equals(field.RuleId, expected.RuleId, StringComparison.Ordinal)
                    || field.RuleVersion != expected.RuleVersion)
                    FailRule(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Typed field registry binding is invalid.");
                ValidateFieldShape(field, expected.Root);
                ValidateProvenance(
                    field.Provenance,
                    generatorVersion,
                    definitions,
                    field.RuleId,
                    field.RuleVersion);
            }
        }

        /// <summary>
        /// 验证字段根节点 shape 的 enum 范围与稳定文本。
        /// </summary>
        private static void ValidateFieldShape(
            TypedFieldContract field,
            CanonicalSemanticNodeSchema expected)
        {
            if ((byte)field.NodeKind < (byte)CanonicalSemanticNodeKind.Absent
                || (byte)field.NodeKind > (byte)CanonicalSemanticNodeKind.Collection
                || field.TypeId == null
                || field.TargetDomainId == null
                || (byte)field.CollectionSemantics > (byte)CanonicalCollectionSemantics.Multiset)
            {
                FailRule(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Typed field root shape is invalid.");
            }
            var absent = field.NodeKind == CanonicalSemanticNodeKind.Absent;
            var kindMatches = absent
                ? expected.PresencePolicy == CanonicalPresencePolicy.Optional
                : field.NodeKind == expected.NodeKind;
            var typeMatches = expected.NodeKind == CanonicalSemanticNodeKind.Variant && !absent
                ? IsRegisteredVariant(field.TypeId, expected)
                : string.Equals(field.TypeId, expected.TypeId, StringComparison.Ordinal);
            var collectionMatches = absent
                ? field.CollectionSemantics == CanonicalCollectionSemantics.None
                : field.CollectionSemantics == expected.CollectionSemantics;
            if (!kindMatches || !typeMatches || !collectionMatches
                || !string.Equals(field.TargetDomainId, expected.TargetDomainId, StringComparison.Ordinal))
                FailRule(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Typed field root does not match the registry shape.");
        }

        /// <summary>
        /// 判定 variant TypeId 是否属于 registry 的固定 discriminator 闭集。
        /// </summary>
        private static bool IsRegisteredVariant(string typeId, CanonicalSemanticNodeSchema expected)
        {
            for (var index = 0; index < expected.Variants.Count; index++)
                if (string.Equals(typeId, expected.Variants[index].VariantTypeId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>
        /// 验证 role ordinal、owner、owner field、RuleId 与 Cost/Cooldown 冲突。
        /// </summary>
        private static void ValidateRoles(
            IReadOnlyList<TypedRoleContract> roles,
            CanonicalDefinitionLookup<TypedDefinitionContract> definitions,
            string generatorVersion)
        {
            if (roles.Count == 0 || roles[0].RoleKind != CanonicalDefinitionRoleKind.Authoring)
                FailRule(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Typed Authoring role is required.");
            var hasCost = false;
            var hasCooldown = false;
            for (var index = 0; index < roles.Count; index++)
            {
                var role = roles[index];
                if (role == null || role.RoleOrdinal != index || role.OwnerDefinition == null
                    || role.RoleKind < CanonicalDefinitionRoleKind.Authoring
                    || role.RoleKind > CanonicalDefinitionRoleKind.GrantedAbilitySource
                    || !definitions.TryGetValue(role.OwnerDefinition, out var owner)
                    || !IsValidOwnerField(role, owner))
                    FailRule(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Typed role owner binding is invalid.");
                ValidateRule(role.RuleId, role.RuleVersion);
                ValidateProvenance(role.Provenance, generatorVersion, definitions, role.RuleId, role.RuleVersion);
                hasCost |= role.RoleKind == CanonicalDefinitionRoleKind.Cost;
                hasCooldown |= role.RoleKind == CanonicalDefinitionRoleKind.Cooldown;
            }
            if (hasCost && hasCooldown)
                FailRule(CanonicalSemanticRuleCatalog.SemanticOwnerConflict, "Typed role contains Cost/Cooldown conflict.");
        }

        /// <summary>
        /// 判断 role 的 owner field 是否符合 Authoring 或引用 owner 约束。
        /// </summary>
        private static bool IsValidOwnerField(
            TypedRoleContract role,
            TypedDefinitionContract owner)
        {
            if (role.RoleKind == CanonicalDefinitionRoleKind.Authoring)
                return role.OwnerFieldOrdinal == -1;
            return role.OwnerFieldOrdinal >= 0 && role.OwnerFieldOrdinal < owner.Fields.Count;
        }

        /// <summary>
        /// 验证 dependency metadata、targets、bounds、provenance 与 dense ordinals。
        /// </summary>
        private static void ValidateDependencies(
            IReadOnlyList<TypedDependencyContract> dependencies,
            IReadOnlyList<TypedFieldContract> fields,
            CanonicalDefinitionLookup<TypedDefinitionContract> definitions,
            string generatorVersion)
        {
            for (var index = 0; index < dependencies.Count; index++)
            {
                var dependency = dependencies[index];
                var validField = dependency != null
                                 && dependency.SourceFieldOrdinal >= 0
                                 && dependency.SourceFieldOrdinal < fields.Count;
                if (dependency == null || dependency.DependencyOrdinal != index
                    || string.IsNullOrWhiteSpace(dependency.DependencyId)
                    || !validField
                    || dependency.TargetDefinition == null || !definitions.ContainsKey(dependency.TargetDefinition)
                    || dependency.MaxExpansion != 1)
                    FailRule(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Typed dependency metadata is invalid.");
                ValidateSealedDependencyMetadata(dependency, fields[dependency.SourceFieldOrdinal]);
                ValidateDependencyDomain(dependency);
                ValidateRule(dependency.RuleId, dependency.RuleVersion);
                ValidateProvenance(
                    dependency.Provenance,
                    generatorVersion,
                    definitions,
                    dependency.RuleId,
                    dependency.RuleVersion);
            }
        }

        /// <summary>
        /// 从 source field/path 重新推导 kind/owner/sign/work/cleanup，拒绝 Typed DTO 注入。
        /// </summary>
        private static void ValidateSealedDependencyMetadata(
            TypedDependencyContract dependency,
            TypedFieldContract sourceField)
        {
            CanonicalSemanticCompiler.ResolveDependencyMetadata(
                sourceField.FieldId,
                dependency.DependencyId,
                out var owner,
                out var sign,
                out var work,
                out var cleanup);
            var kind = CanonicalSemanticCompiler.ResolveDependencyKind(sign, work, cleanup);
            if (dependency.DependencyKind != kind
                || dependency.OwnerKind != owner
                || dependency.Sign != sign
                || dependency.WorkKind != work
                || dependency.CleanupPolicy != cleanup)
                FailRule(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Typed dependency metadata is not sealed to its source path.");
        }

        /// <summary>
        /// 验证 dependency target domain 与 target DefinitionKey 一致。
        /// </summary>
        private static void ValidateDependencyDomain(TypedDependencyContract dependency)
        {
            if (!CanonicalSemanticSchemaRegistry.TryResolve(dependency.TargetDomainId, out var schema)
                || dependency.TargetDefinition.DomainOrdinal != schema.DomainOrdinal
                || dependency.TargetDefinition.DefinitionKind != schema.DefinitionKind
                || dependency.DependencyKind < CanonicalDependencyKind.Reference
                || dependency.DependencyKind > CanonicalDependencyKind.Cleanup
                || dependency.OwnerKind < CanonicalDependencyOwnerKind.AuthoringField
                || dependency.OwnerKind > CanonicalDependencyOwnerKind.Execution
                || (byte)dependency.Sign > (byte)CanonicalDependencySign.Negative
                || dependency.WorkKind < CanonicalDependencyWorkKind.Read
                || dependency.WorkKind > CanonicalDependencyWorkKind.Cleanup
                || (byte)dependency.CleanupPolicy > (byte)CanonicalCleanupPolicy.Unsupported)
            {
                FailRule(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Typed dependency target domain is invalid.");
            }
        }

        /// <summary>
        /// 验证 programs 完整 decisions、强制 operations、projection bindings 与连续 DAG。
        /// </summary>
        private static void ValidatePrograms(
            TypedDefinitionContract definition,
            CanonicalSemanticDomainSchema domain,
            CanonicalDefinitionLookup<TypedDefinitionContract> definitions,
            string generatorVersion)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < definition.Programs.Count; index++)
            {
                var program = definition.Programs[index];
                if (program == null)
                    FailRule(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Typed program is required.");
                if (!domain.TryResolveProgram(program.ProgramKind, out var schema))
                    FailRule(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Typed program kind is not registered.");
                if (program.ProgramOrdinal != index
                    || string.IsNullOrWhiteSpace(program.ProgramId) || !ids.Add(program.ProgramId)
                    || program.Disposition != schema.Disposition
                    || !string.Equals(program.RuleId, schema.RuleId, StringComparison.Ordinal)
                    || program.RuleVersion != schema.RuleVersion)
                    FailRule(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Typed program schema binding is invalid.");
                ValidateProvenance(program.Provenance, generatorVersion, definitions, program.RuleId, program.RuleVersion);
                ValidateDecisions(program.Decisions, schema, generatorVersion, definitions);
                ValidateProgramBody(definition, program, schema, generatorVersion, definitions);
            }
        }

        /// <summary>
        /// 验证 contextual decisions 全覆盖 fixed matrix。
        /// </summary>
        private static void ValidateDecisions(
            IReadOnlyList<TypedContextDecisionContract> decisions,
            CanonicalSemanticProgramSchema schema,
            string generatorVersion,
            CanonicalDefinitionLookup<TypedDefinitionContract> definitions)
        {
            if (decisions.Count != schema.FieldDecisions.Count)
                FailRule(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Typed contextual decision coverage is incomplete.");
            for (var index = 0; index < decisions.Count; index++)
            {
                var actual = decisions[index];
                var expected = schema.FieldDecisions[index];
                if (actual == null || actual.DecisionOrdinal != index
                    || actual.FieldOrdinal != expected.FieldOrdinal
                    || !string.Equals(actual.FieldId, expected.FieldId, StringComparison.Ordinal)
                    || actual.Classification != expected.Classification
                    || actual.Disposition != expected.Disposition
                    || !string.Equals(actual.RuleId, expected.RuleId, StringComparison.Ordinal)
                    || actual.RuleVersion != expected.RuleVersion)
                    Fail("Typed contextual decision is non-canonical.");
                ValidateProvenance(actual.Provenance, generatorVersion, definitions, actual.RuleId, actual.RuleVersion);
            }
        }

        /// <summary>
        /// 验证 denied program 无执行体，allowed program exact operation/order/bindings。
        /// </summary>
        private static void ValidateProgramBody(
            TypedDefinitionContract definition,
            TypedProgramContract program,
            CanonicalSemanticProgramSchema schema,
            string generatorVersion,
            CanonicalDefinitionLookup<TypedDefinitionContract> definitions)
        {
            if (schema.Disposition == CanonicalFieldDisposition.Denied)
            {
                if (program.Nodes.Count != 0 || program.Edges.Count != 0)
                    FailRule(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Denied typed program has executable body.");
                return;
            }
            if (program.Nodes.Count != schema.Operations.Count
                || program.Edges.Count != Math.Max(0, program.Nodes.Count - 1))
                FailRule(CanonicalSemanticRuleCatalog.MissingValueViewOrPhase, "Typed program operation/order is incomplete.");
            for (var index = 0; index < program.Nodes.Count; index++)
                ValidateProgramNode(definition, program, program.Nodes[index], schema.Operations[index], generatorVersion, definitions);
            for (var index = 0; index < program.Edges.Count; index++)
                ValidateProgramEdge(program.Edges[index], index, generatorVersion, definitions);
        }

        /// <summary>
        /// 验证 typed node operation、read sets 与 projection binding identity。
        /// </summary>
        private static void ValidateProgramNode(
            TypedDefinitionContract definition,
            TypedProgramContract program,
            TypedProgramNodeContract node,
            CanonicalSemanticOperationSchema operation,
            string generatorVersion,
            CanonicalDefinitionLookup<TypedDefinitionContract> definitions)
        {
            var expectedFields = ResolveFieldOrdinals(definition.Fields, operation.FieldIds);
            var expectedDependencies = ResolveDependencyOrdinals(definition.Dependencies, expectedFields);
            if (node == null || node.NodeOrdinal != operation.OperationOrdinal
                || !string.Equals(node.OperationId, operation.OperationId, StringComparison.Ordinal)
                || !HaveSameOrdinals(node.FieldOrdinals, expectedFields)
                || !HaveSameOrdinals(node.DependencyOrdinals, expectedDependencies)
                || !string.Equals(node.RuleId, operation.RuleId, StringComparison.Ordinal)
                || node.RuleVersion != operation.RuleVersion)
                FailRule(CanonicalSemanticRuleCatalog.MissingValueViewOrPhase, "Typed node is not the sealed operation template.");
            ValidateProvenance(node.Provenance, generatorVersion, definitions, node.RuleId, node.RuleVersion);
            ValidateProjection(definition, program, node, operation, expectedFields, generatorVersion, definitions);
        }

        /// <summary>
        /// 验证 projection owner、operation、ValueView、phase、target、bounds、payload 与 RuleId。
        /// </summary>
        private static void ValidateProjection(
            TypedDefinitionContract definition,
            TypedProgramContract program,
            TypedProgramNodeContract node,
            CanonicalSemanticOperationSchema operation,
            IReadOnlyList<int> fieldOrdinals,
            string generatorVersion,
            CanonicalDefinitionLookup<TypedDefinitionContract> definitions)
        {
            var projection = node.Projection;
            var sourceField = fieldOrdinals.Count == 0 ? -1 : fieldOrdinals[0];
            if (projection == null
                || projection.ProjectionOrdinal != node.NodeOrdinal
                || projection.OwnerDefinition == null
                || !projection.OwnerDefinition.HasSameIdentity(definition.DefinitionKey)
                || projection.ProgramOrdinal != program.ProgramOrdinal
                || projection.NodeOrdinal != node.NodeOrdinal
                || projection.SourceFieldOrdinal != sourceField
                || !string.Equals(projection.OperationId, operation.OperationId, StringComparison.Ordinal)
                || !string.Equals(projection.ValueView, operation.ValueView, StringComparison.Ordinal)
                || !string.Equals(projection.Phase, operation.Phase, StringComparison.Ordinal)
                || !string.Equals(projection.TargetDomainId, operation.TargetDomainId, StringComparison.Ordinal)
                || projection.MaxItemCount < 0 || projection.MaxPayloadBytes <= 0 || projection.PayloadUnsafe.Length == 0
                || !string.Equals(projection.RuleId, operation.RuleId, StringComparison.Ordinal)
                || projection.RuleVersion != operation.RuleVersion)
                Fail("Typed projection binding is non-canonical.");
            ValidateProvenance(projection.Provenance, generatorVersion, definitions, projection.RuleId, projection.RuleVersion);
        }

        /// <summary>
        /// 验证连续 Control edge 与 CFG1301 provenance。
        /// </summary>
        private static void ValidateProgramEdge(
            TypedProgramEdgeContract edge,
            int ordinal,
            string generatorVersion,
            CanonicalDefinitionLookup<TypedDefinitionContract> definitions)
        {
            if (edge == null || edge.EdgeOrdinal != ordinal
                || edge.SourceNodeOrdinal != ordinal
                || edge.TargetNodeOrdinal != ordinal + 1
                || edge.EdgeKind != CanonicalProgramEdgeKind.Control
                || !string.Equals(edge.RuleId, CanonicalSemanticRuleCatalog.DependencyOrCleanupUnbounded, StringComparison.Ordinal))
                FailRule(CanonicalSemanticRuleCatalog.DependencyOrCleanupUnbounded, "Typed program edge is invalid.");
            ValidateRule(edge.RuleId, edge.RuleVersion);
            ValidateProvenance(edge.Provenance, generatorVersion, definitions, edge.RuleId, edge.RuleVersion);
        }

        /// <summary>
        /// 将 operation field ids 解析为排序后的 typed field ordinals。
        /// </summary>
        private static IReadOnlyList<int> ResolveFieldOrdinals(
            IReadOnlyList<TypedFieldContract> fields,
            IReadOnlyList<string> fieldIds)
        {
            var result = new int[fieldIds.Count];
            for (var idIndex = 0; idIndex < fieldIds.Count; idIndex++)
            {
                var found = false;
                for (var fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                {
                    if (!string.Equals(fields[fieldIndex].FieldId, fieldIds[idIndex], StringComparison.Ordinal))
                        continue;
                    result[idIndex] = fields[fieldIndex].FieldOrdinal;
                    found = true;
                    break;
                }
                if (!found)
                    FailRule(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Typed operation field is missing.");
            }
            Array.Sort(result);
            return result;
        }

        /// <summary>
        /// 汇总 read fields 对应的 dependency ordinals。
        /// </summary>
        private static IReadOnlyList<int> ResolveDependencyOrdinals(
            IReadOnlyList<TypedDependencyContract> dependencies,
            IReadOnlyList<int> fieldOrdinals)
        {
            var result = new List<int>();
            for (var index = 0; index < dependencies.Count; index++)
                if (Contains(fieldOrdinals, dependencies[index].SourceFieldOrdinal))
                    result.Add(dependencies[index].DependencyOrdinal);
            return result;
        }

        /// <summary>
        /// 判断 ordinal 集合是否包含目标值。
        /// </summary>
        private static bool Contains(IReadOnlyList<int> values, int target)
        {
            for (var index = 0; index < values.Count; index++)
                if (values[index] == target)
                    return true;
            return false;
        }

        /// <summary>
        /// 比较两个 canonical ordinal 列表。
        /// </summary>
        private static bool HaveSameOrdinals(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            if (left.Count != right.Count)
                return false;
            for (var index = 0; index < left.Count; index++)
                if (left[index] != right[index])
                    return false;
            return true;
        }

        /// <summary>
        /// 验证 typed dependency graph 不含 proof-relevant 跨 Definition 环。
        /// </summary>
        private static void ValidateCrossDefinitionCycles(
            IReadOnlyList<TypedDefinitionContract> definitions)
        {
            var arcs = new List<CanonicalDependencyCycleArc>();
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                for (var edgeIndex = 0; edgeIndex < definition.Dependencies.Count; edgeIndex++)
                {
                    var dependency = definition.Dependencies[edgeIndex];
                    arcs.Add(new CanonicalDependencyCycleArc(
                        definition.DefinitionKey,
                        dependency.TargetDefinition,
                        dependency.DependencyKind,
                        dependency.Sign,
                        dependency.WorkKind,
                        dependency.CleanupPolicy,
                        dependency.MaxExpansion,
                        dependency.Provenance));
                }
            }
            CanonicalSemanticDependencyCycleValidator.Validate(arcs.ToArray());
        }

        /// <summary>
        /// 验证 provenance 稳定来源、RuleId、generator 与 related Definition 集合。
        /// </summary>
        private static void ValidateProvenance(
            CanonicalSemanticProvenance provenance,
            string generatorVersion,
            CanonicalDefinitionLookup<TypedDefinitionContract> definitions,
            string expectedRuleId,
            uint expectedRuleVersion)
        {
            if (provenance == null
                || string.IsNullOrWhiteSpace(provenance.WorkbookId)
                || string.IsNullOrWhiteSpace(provenance.TableId)
                || string.IsNullOrWhiteSpace(provenance.RowStableId)
                || string.IsNullOrWhiteSpace(provenance.FieldPath)
                || provenance.RawValue == null || provenance.NormalizedValue == null
                || !string.Equals(provenance.GeneratorVersion, generatorVersion, StringComparison.Ordinal)
                || LooksLikeAbsolutePath(provenance.WorkbookId)
                || LooksLikeAbsolutePath(provenance.TableId))
                Fail("Typed provenance is incomplete or non-canonical.");
            ValidateRule(provenance.RuleId, provenance.RuleVersion);
            if (expectedRuleId != null
                && (!string.Equals(provenance.RuleId, expectedRuleId, StringComparison.Ordinal)
                    || provenance.RuleVersion != expectedRuleVersion))
                Fail("Typed governing RuleId does not match provenance.");
            ValidateRelatedDefinitions(provenance.RelatedDefinitionIds, definitions);
        }

        /// <summary>
        /// 验证 provenance related Definition 集合唯一、存在且 canonical 排序。
        /// </summary>
        private static void ValidateRelatedDefinitions(
            IReadOnlyList<CanonicalDefinitionKey> related,
            CanonicalDefinitionLookup<TypedDefinitionContract> definitions)
        {
            CanonicalDefinitionKey previous = null;
            for (var index = 0; index < related.Count; index++)
            {
                var key = related[index];
                if (key == null || !definitions.ContainsKey(key)
                    || (previous != null && previous.CompareTo(key) >= 0))
                    Fail("Typed provenance contains duplicate, dangling or unsorted Definition ids.");
                previous = key;
            }
        }

        /// <summary>
        /// 验证 RuleId 位于固定 registry 且版本匹配。
        /// </summary>
        private static void ValidateRule(string ruleId, uint ruleVersion)
        {
            if (!CanonicalSemanticRuleCatalog.TryResolve(ruleId, out var expectedVersion, out _)
                || ruleVersion != expectedVersion)
                Fail("Typed contract references an unknown RuleId or version.");
        }

        /// <summary>
        /// 识别 Unix、Windows、UNC 与 drive-rooted 绝对路径。
        /// </summary>
        private static bool LooksLikeAbsolutePath(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            if (value[0] == '/' || value[0] == '\\')
                return true;
            return value.Length >= 3
                   && char.IsLetter(value[0])
                   && value[1] == ':'
                   && (value[2] == '\\' || value[2] == '/');
        }

        /// <summary>
        /// 比较两个 byte payload。
        /// </summary>
        private static bool HaveSameBytes(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (var index = 0; index < left.Length; index++)
                if (left[index] != right[index])
                    return false;
            return true;
        }

        /// <summary>
        /// 比较 decoder 输入与重新编码 bytes，拒绝替代顺序表达。
        /// </summary>
        private static void EnsureSameBytes(byte[] expected, byte[] actual)
        {
            if (!HaveSameBytes(expected, actual))
                Fail("Decoded TypedContract was not in canonical byte form.");
        }

        /// <summary>
        /// 抛出指定稳定 RuleId typed codec 异常。
        /// </summary>
        private static void FailRule(string ruleId, string message)
        {
            throw new CanonicalSemanticValidationException(ruleId, message);
        }

        /// <summary>
        /// 抛出稳定 CFG1501 typed codec 异常。
        /// </summary>
        private static void Fail(string message)
        {
            FailRule(CanonicalSemanticRuleCatalog.NonCanonicalIdentity, message);
        }

        /// <summary>
        /// 暂存 decoder 读取的 RuleId/version/provenance 公共尾部。
        /// </summary>
        private sealed class RuleAndProvenance
        {
            /// <summary>
            /// 创建 decoder 内部的公共尾部记录。
            /// </summary>
            public RuleAndProvenance(
                string ruleId,
                uint ruleVersion,
                CanonicalSemanticProvenance provenance)
            {
                RuleId = ruleId;
                RuleVersion = ruleVersion;
                Provenance = provenance;
            }

            public string RuleId { get; }

            public uint RuleVersion { get; }

            public CanonicalSemanticProvenance Provenance { get; }
        }
    }
}
