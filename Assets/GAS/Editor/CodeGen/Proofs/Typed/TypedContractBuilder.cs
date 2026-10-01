using System;
using System.Collections.Generic;
using GAS.Editor.CodeGen.Semantics;

namespace GAS.Editor.CodeGen.Proofs.Typed
{
    /// <summary>
    /// 仅从同一份已验证 v2 graph 派生 TypedContract 与全部 payload/hash，不产生 promotion 或 eligibility 副作用。
    /// </summary>
    public static class TypedContractBuilder
    {
        /// <summary>
        /// 构建绑定 source/toolchain、Red coverage、canonical payload 与 sealed compile 结果的 TypedContract。
        /// </summary>
        public static TypedContract Create(CanonicalNormalizedSemanticGraph graph)
        {
            CanonicalSemanticGraphValidator.Validate(graph);
            var matrixPayload = TypedContractBinaryCodec.EncodeContractMatrix();
            var matrixHash = TypedContractBinaryCodec.ComputeMatrixHash(matrixPayload);
            var schemaPayload = CanonicalSemanticProjectionCodec.EncodeSchema(
                graph,
                CanonicalSemanticVersions.TypedContractAbi,
                matrixHash);
            var contentPayload = CanonicalSemanticProjectionCodec.EncodeContent(graph);
            var identity = CanonicalSemanticIdentity.Create(
                graph,
                CanonicalSemanticVersions.TypedContractAbi,
                matrixHash);
            var definitions = BuildDefinitions(graph.Definitions);
            var coverage = BuildCoverage(graph.Coverage);
            var registryHash = TypedContractBinaryCodec.ComputeRegistryHash();
            var provisional = CreateContract(
                graph,
                coverage,
                identity,
                registryHash,
                matrixHash,
                string.Empty,
                matrixPayload,
                schemaPayload,
                contentPayload,
                definitions);
            var bytes = TypedContractBinaryCodec.EncodePayload(provisional);
            var typedHash = TypedContractBinaryCodec.ComputeTypedContractHash(bytes);
            var contract = CreateContract(
                graph,
                coverage,
                identity,
                registryHash,
                matrixHash,
                typedHash,
                matrixPayload,
                schemaPayload,
                contentPayload,
                definitions);
            TypedContractBinaryCodec.Encode(contract);
            return contract;
        }

        /// <summary>
        /// 强制 external expected hash、contract bytes、canonical payload 与当前 graph 全部逐 byte 相等。
        /// </summary>
        internal static void EnsureMatches(
            CanonicalNormalizedSemanticGraph graph,
            TypedContract contract,
            string expectedTypedContractHash)
        {
            if (contract == null)
                throw new ArgumentNullException(nameof(contract));
            CanonicalSemanticIdentity.RequireHash(
                expectedTypedContractHash,
                nameof(expectedTypedContractHash));
            var actualBytes = TypedContractBinaryCodec.Encode(contract);
            TypedContractBinaryCodec.EnsureExpectedHash(actualBytes, expectedTypedContractHash);
            var expected = Create(graph);
            var expectedBytes = TypedContractBinaryCodec.Encode(expected);
            if (!string.Equals(expected.TypedContractHash, expectedTypedContractHash, StringComparison.Ordinal)
                || !HaveSameBytes(expectedBytes, actualBytes))
            {
                Fail("TypedContract does not match the current canonical graph and external expected hash.");
            }
        }

        /// <summary>
        /// 以共享身份与 payload 参数创建 provisional 或最终 TypedContract。
        /// </summary>
        private static TypedContract CreateContract(
            CanonicalNormalizedSemanticGraph graph,
            TypedSemanticCoverageContract coverage,
            CanonicalSemanticIdentity identity,
            string registryHash,
            string matrixHash,
            string typedHash,
            byte[] matrixPayload,
            byte[] schemaPayload,
            byte[] contentPayload,
            IReadOnlyList<TypedDefinitionContract> definitions)
        {
            return new TypedContract(
                CanonicalSemanticVersions.LubanSourceContractVersion,
                graph.ToolchainIdentity,
                graph.GeneratorVersion,
                graph.SourceInputHash,
                coverage,
                identity.GraphHash,
                registryHash,
                identity.SchemaHash,
                identity.ContentHash,
                matrixHash,
                typedHash,
                matrixPayload,
                schemaPayload,
                contentPayload,
                definitions);
        }

        /// <summary>
        /// 复制 graph 的五项 Red coverage。
        /// </summary>
        private static TypedSemanticCoverageContract BuildCoverage(CanonicalSemanticCoverage coverage)
        {
            return new TypedSemanticCoverageContract(
                coverage.SchemaCoverage,
                coverage.ReferenceCoverage,
                coverage.ProgramCoverage,
                coverage.SameParseCoverage,
                coverage.Eligibility);
        }

        /// <summary>
        /// 按 graph canonical 顺序派生全部 Definition contracts。
        /// </summary>
        private static IReadOnlyList<TypedDefinitionContract> BuildDefinitions(
            IReadOnlyList<CanonicalSemanticDefinition> definitions)
        {
            var result = new TypedDefinitionContract[definitions.Count];
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                result[index] = new TypedDefinitionContract(
                    definition.DefinitionOrdinal,
                    definition.Key,
                    BuildFields(definition.Fields),
                    BuildRoles(definition.Roles),
                    BuildPrograms(definition.Programs),
                    BuildDependencies(definition.Dependencies),
                    definition.Provenance);
            }

            return result;
        }

        /// <summary>
        /// 从 canonical field 根节点派生 typed shape 与分类裁决。
        /// </summary>
        private static IReadOnlyList<TypedFieldContract> BuildFields(
            IReadOnlyList<CanonicalSemanticField> fields)
        {
            var result = new TypedFieldContract[fields.Count];
            for (var index = 0; index < fields.Count; index++)
            {
                var field = fields[index];
                var root = field.Root;
                result[index] = new TypedFieldContract(
                    field.FieldOrdinal,
                    field.FieldId,
                    root.NodeKind,
                    root.TypeId,
                    root.TargetDomainId,
                    root.CollectionSemantics,
                    field.Classification,
                    field.Disposition,
                    field.Provenance.RuleId,
                    field.Provenance.RuleVersion,
                    field.Provenance);
            }

            return result;
        }

        /// <summary>
        /// 从 compiler 推导的 roles 派生 typed role contracts。
        /// </summary>
        private static IReadOnlyList<TypedRoleContract> BuildRoles(
            IReadOnlyList<CanonicalSemanticRole> roles)
        {
            var result = new TypedRoleContract[roles.Count];
            for (var index = 0; index < roles.Count; index++)
            {
                var role = roles[index];
                result[index] = new TypedRoleContract(
                    role.RoleOrdinal,
                    role.RoleKind,
                    role.OwnerDefinition,
                    role.OwnerFieldOrdinal,
                    role.RuleId,
                    role.RuleVersion,
                    role.Provenance);
            }

            return result;
        }

        /// <summary>
        /// 从 canonical programs 派生 decisions、projection nodes 与 edges。
        /// </summary>
        private static IReadOnlyList<TypedProgramContract> BuildPrograms(
            IReadOnlyList<CanonicalSemanticProgram> programs)
        {
            var result = new TypedProgramContract[programs.Count];
            for (var index = 0; index < programs.Count; index++)
            {
                var program = programs[index];
                result[index] = new TypedProgramContract(
                    program.ProgramOrdinal,
                    program.ProgramId,
                    program.ProgramKind,
                    program.Disposition,
                    BuildDecisions(program.Decisions),
                    BuildProgramNodes(program.Nodes),
                    BuildProgramEdges(program.Edges),
                    program.Provenance.RuleId,
                    program.Provenance.RuleVersion,
                    program.Provenance);
            }

            return result;
        }

        /// <summary>
        /// 复制 program 的全部 contextual decisions，包括 inactive 与 denied 字段。
        /// </summary>
        private static IReadOnlyList<TypedContextDecisionContract> BuildDecisions(
            IReadOnlyList<CanonicalSemanticContextDecision> decisions)
        {
            var result = new TypedContextDecisionContract[decisions.Count];
            for (var index = 0; index < decisions.Count; index++)
            {
                var decision = decisions[index];
                result[index] = new TypedContextDecisionContract(
                    decision.DecisionOrdinal,
                    decision.FieldOrdinal,
                    decision.FieldId,
                    decision.IsActive,
                    decision.Classification,
                    decision.Disposition,
                    decision.RuleId,
                    decision.RuleVersion,
                    decision.Provenance);
            }

            return result;
        }

        /// <summary>
        /// 复制强制 operation node 与 sealed projection binding。
        /// </summary>
        private static IReadOnlyList<TypedProgramNodeContract> BuildProgramNodes(
            IReadOnlyList<CanonicalSemanticProgramNode> nodes)
        {
            var result = new TypedProgramNodeContract[nodes.Count];
            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                result[index] = new TypedProgramNodeContract(
                    node.NodeOrdinal,
                    node.OperationId,
                    node.FieldOrdinals,
                    node.DependencyOrdinals,
                    BuildProjection(node.Projection),
                    node.Provenance.RuleId,
                    node.Provenance.RuleVersion,
                    node.Provenance);
            }

            return result;
        }

        /// <summary>
        /// 复制 projection 的 binding identity、bounds 与 canonical payload。
        /// </summary>
        private static TypedProjectionBindingContract BuildProjection(
            CanonicalSemanticProjectionBinding projection)
        {
            if (projection == null)
                return null;
            return new TypedProjectionBindingContract(
                projection.ProjectionOrdinal,
                projection.OwnerDefinition,
                projection.ProgramOrdinal,
                projection.NodeOrdinal,
                projection.SourceFieldOrdinal,
                projection.OperationId,
                projection.ValueView,
                projection.Phase,
                projection.TargetDomainId,
                projection.MaxItemCount,
                projection.MaxPayloadBytes,
                projection.PayloadUnsafe,
                projection.RuleId,
                projection.RuleVersion,
                projection.Provenance);
        }

        /// <summary>
        /// 复制 program DAG edges 与 governing provenance。
        /// </summary>
        private static IReadOnlyList<TypedProgramEdgeContract> BuildProgramEdges(
            IReadOnlyList<CanonicalSemanticProgramEdge> edges)
        {
            var result = new TypedProgramEdgeContract[edges.Count];
            for (var index = 0; index < edges.Count; index++)
            {
                var edge = edges[index];
                result[index] = new TypedProgramEdgeContract(
                    edge.EdgeOrdinal,
                    edge.SourceNodeOrdinal,
                    edge.TargetNodeOrdinal,
                    edge.EdgeKind,
                    edge.Provenance.RuleId,
                    edge.Provenance.RuleVersion,
                    edge.Provenance);
            }

            return result;
        }

        /// <summary>
        /// 复制 dependency 的 target domain、owner/sign/work/cleanup 与 expansion bound。
        /// </summary>
        private static IReadOnlyList<TypedDependencyContract> BuildDependencies(
            IReadOnlyList<CanonicalSemanticDependency> dependencies)
        {
            var result = new TypedDependencyContract[dependencies.Count];
            for (var index = 0; index < dependencies.Count; index++)
            {
                var dependency = dependencies[index];
                result[index] = new TypedDependencyContract(
                    dependency.DependencyOrdinal,
                    dependency.DependencyId,
                    dependency.SourceFieldOrdinal,
                    dependency.DependencyKind,
                    dependency.TargetDefinition,
                    dependency.TargetDomainId,
                    dependency.OwnerKind,
                    dependency.Sign,
                    dependency.WorkKind,
                    dependency.CleanupPolicy,
                    dependency.MaxExpansion,
                    dependency.Provenance.RuleId,
                    dependency.Provenance.RuleVersion,
                    dependency.Provenance);
            }

            return result;
        }

        /// <summary>
        /// 逐 byte 比较两个 canonical payload。
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
        /// 抛出稳定 CFG1501 typed identity 异常。
        /// </summary>
        private static void Fail(string message)
        {
            throw new CanonicalSemanticValidationException(
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                message);
        }
    }
}
