using System;
using System.Collections.Generic;

namespace GAS.Editor.CodeGen.Semantics
{
    /// <summary>
    /// 对 v2 graph 的 source identity、tree、sealed compile 结果、局部 DAG 与跨 definition DAG 执行 fail-closed 校验。
    /// </summary>
    public static class CanonicalSemanticGraphValidator
    {
        /// <summary>
        /// 验证 graph 全部冻结不变量；coverage/eligibility 在本轮必须保持 Red。
        /// </summary>
        public static void Validate(CanonicalNormalizedSemanticGraph graph)
        {
            if (graph == null)
                Fail(CanonicalSemanticRuleCatalog.NonCanonicalIdentity, "Graph is required.");
            CanonicalSemanticResourceBudget.ValidateGraph(graph);
            ValidateRoot(graph);
            ValidateRules(graph);
            var definitions = IndexDefinitions(graph.Definitions);
            ValidateDefinitions(graph, definitions);
            ValidateSealedAuthoringOwnership(graph.Definitions);
            ValidateCrossDefinitionCycles(graph.Definitions);
        }

        /// <summary>
        /// 复用 compiler 的跨 Definition owner 检查，拒绝 Ability.Cd 与 GE.Duration 双事实源。
        /// </summary>
        private static void ValidateSealedAuthoringOwnership(
            IReadOnlyList<CanonicalSemanticDefinition> definitions)
        {
            var drafts = new SemanticDefinitionDraft[definitions.Count];
            var keys = new CanonicalDefinitionKey[definitions.Count];
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                drafts[index] = new SemanticDefinitionDraft(
                    definition.DefinitionOrdinal,
                    definition.Key,
                    definition.Fields,
                    definition.Provenance);
                keys[index] = definition.Key;
            }
            var byKey = new CanonicalDefinitionLookup<SemanticDefinitionDraft>(keys);
            for (var index = 0; index < drafts.Length; index++)
                byKey.Add(drafts[index].Key, drafts[index]);
            CanonicalSemanticCompiler.ValidateCooldownDurationOwnership(drafts, byKey);
        }

        /// <summary>
        /// 验证 structured toolchain、SourceInputHash、generator 与全 Red coverage。
        /// </summary>
        private static void ValidateRoot(CanonicalNormalizedSemanticGraph graph)
        {
            var toolchain = graph.ToolchainIdentity;
            if (toolchain == null
                || string.IsNullOrWhiteSpace(toolchain.LubanProductVersion)
                || string.IsNullOrWhiteSpace(toolchain.GeneratorIdentity)
                || string.IsNullOrWhiteSpace(graph.GeneratorVersion))
            {
                Fail(CanonicalSemanticRuleCatalog.NonCanonicalIdentity, "Graph identity is incomplete.");
            }
            CanonicalSemanticIdentity.RequireHash(toolchain.LubanBinaryHash, nameof(toolchain.LubanBinaryHash));
            CanonicalSemanticIdentity.RequireHash(toolchain.SchemaInputHash, nameof(toolchain.SchemaInputHash));
            CanonicalSemanticIdentity.RequireHash(graph.SourceInputHash, nameof(graph.SourceInputHash));
            if (!string.Equals(toolchain.GeneratorIdentity, graph.GeneratorVersion, StringComparison.Ordinal))
                Fail(CanonicalSemanticRuleCatalog.NonCanonicalIdentity, "Graph generator/toolchain mismatch.");
            ValidateCoverage(graph.Coverage);
        }

        /// <summary>
        /// 强制 A 阶段所有 coverage 与 eligibility 保持 Red。
        /// </summary>
        private static void ValidateCoverage(CanonicalSemanticCoverage coverage)
        {
            if (coverage == null
                || coverage.SchemaCoverage != CanonicalSemanticCoverageState.Red
                || coverage.ReferenceCoverage != CanonicalSemanticCoverageState.Red
                || coverage.ProgramCoverage != CanonicalSemanticCoverageState.Red
                || coverage.SameParseCoverage != CanonicalSemanticCoverageState.Red
                || coverage.Eligibility != CanonicalSemanticCoverageState.Red)
            {
                Fail(CanonicalSemanticRuleCatalog.CapacityProofMissing, "Semantic coverage/eligibility must remain Red.");
            }
        }

        /// <summary>
        /// 验证固定 RuleId registry 的数量、ordinal、id、version、kind 与 provenance。
        /// </summary>
        private static void ValidateRules(CanonicalNormalizedSemanticGraph graph)
        {
            var expected = CanonicalSemanticRuleCatalog.CreateDefaultRegistry(graph.GeneratorVersion);
            if (graph.Rules.Count != expected.Count)
                Fail(CanonicalSemanticRuleCatalog.NonCanonicalIdentity, "Rule registry is incomplete.");
            for (var index = 0; index < expected.Count; index++)
            {
                var actual = graph.Rules[index];
                if (actual == null
                    || actual.RuleOrdinal != index
                    || !string.Equals(actual.RuleId, expected[index].RuleId, StringComparison.Ordinal)
                    || actual.RuleVersion != expected[index].RuleVersion
                    || actual.RuleKind != expected[index].RuleKind)
                {
                    Fail(CanonicalSemanticRuleCatalog.NonCanonicalIdentity, "Rule registry entry is non-canonical.");
                }
                ValidateProvenance(actual.Provenance, graph.GeneratorVersion, actual.RuleId);
            }
        }

        /// <summary>
        /// 建立 DefinitionKey 唯一索引并验证全局 key sort 与 per-domain dense DefinitionOrdinal。
        /// </summary>
        private static CanonicalDefinitionLookup<CanonicalSemanticDefinition> IndexDefinitions(
            IReadOnlyList<CanonicalSemanticDefinition> definitions)
        {
            if (definitions.Count == 0)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Graph requires definitions.");
            var keys = new CanonicalDefinitionKey[definitions.Count];
            var nextByDomain = new Dictionary<int, int>();
            CanonicalDefinitionKey previous = null;
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition == null || definition.Key == null)
                    Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Definition/key is required.");
                if (previous != null && previous.CompareTo(definition.Key) >= 0)
                    Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Definition keys are duplicate or unsorted.", definition.Provenance);
                if (!nextByDomain.TryGetValue(definition.Key.DomainOrdinal, out var expectedOrdinal))
                    expectedOrdinal = 0;
                if (definition.DefinitionOrdinal != expectedOrdinal)
                    Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "DefinitionOrdinal is not dense.", definition.Provenance);
                nextByDomain[definition.Key.DomainOrdinal] = expectedOrdinal + 1;
                keys[index] = definition.Key;
                previous = definition.Key;
            }
            var result = new CanonicalDefinitionLookup<CanonicalSemanticDefinition>(keys);
            for (var index = 0; index < definitions.Count; index++)
                result.Add(definitions[index].Key, definitions[index]);
            return result;
        }

        /// <summary>
        /// 验证每个 Definition 的 domain/fields/roles/programs/dependencies 与 registry/sealed compiler 一致。
        /// </summary>
        private static void ValidateDefinitions(
            CanonicalNormalizedSemanticGraph graph,
            CanonicalDefinitionLookup<CanonicalSemanticDefinition> definitions)
        {
            for (var index = 0; index < graph.Definitions.Count; index++)
            {
                var definition = graph.Definitions[index];
                var schema = ResolveDomain(definition.Key);
                ValidateProvenance(definition.Provenance, graph.GeneratorVersion, CanonicalSemanticRuleCatalog.InvalidDomainOrReference);
                ValidateFields(definition, schema, graph.GeneratorVersion);
                ValidateRoles(definition, definitions, graph.GeneratorVersion);
                ValidateDependencies(definition, definitions, graph.GeneratorVersion);
                ValidatePrograms(definition, schema, graph.GeneratorVersion);
            }
        }

        /// <summary>
        /// 解析并验证 key 的 DomainOrdinal、kind 与 stable id arity。
        /// </summary>
        private static CanonicalSemanticDomainSchema ResolveDomain(CanonicalDefinitionKey key)
        {
            if (!CanonicalSemanticSchemaRegistry.TryResolve(key.DefinitionKind, out var schema)
                || key.DomainOrdinal != schema.DomainOrdinal
                || key.StableIdParts.Count != schema.StableIdPartCount)
            {
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Definition key does not match registry.");
            }
            return schema;
        }

        /// <summary>
        /// 验证字段闭集、ordinal、分类、裁决、根节点与稳定 key 字段。
        /// </summary>
        private static void ValidateFields(
            CanonicalSemanticDefinition definition,
            CanonicalSemanticDomainSchema schema,
            string generatorVersion)
        {
            if (definition.Fields.Count != schema.Fields.Count)
                Fail(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Definition field coverage is incomplete.", definition.Provenance);
            for (var index = 0; index < schema.Fields.Count; index++)
            {
                var field = definition.Fields[index];
                var expected = schema.Fields[index];
                if (field == null
                    || field.FieldOrdinal != index
                    || !string.Equals(field.FieldId, expected.FieldId, StringComparison.Ordinal)
                    || field.Classification != expected.Classification
                    || field.Disposition != expected.Disposition)
                {
                    Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Field registry binding is invalid.", field?.Provenance);
                }
                ValidateProvenance(field.Provenance, generatorVersion, expected.RuleId);
                ValidateNode(field.Root, expected.Root, generatorVersion, true);
            }
            ValidateStableKey(definition);
        }

        /// <summary>
        /// 递归验证 tree kind/type/target/presence、ordinal、scalar 与 children。
        /// </summary>
        private static void ValidateNode(
            CanonicalSemanticNode node,
            CanonicalSemanticNodeSchema schema,
            string generatorVersion,
            bool isRoot)
        {
            if (node == null || !string.Equals(node.NodeId, schema.NodeId, StringComparison.Ordinal))
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Semantic tree node is missing or misbound.");
            if (isRoot && node.NodeOrdinal != 0)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Field root ordinal must be zero.", node.Provenance);
            ValidateProvenance(node.Provenance, generatorVersion, schema.RuleId);
            ValidateNodeIdentity(node, schema);
            if (node.NodeKind == CanonicalSemanticNodeKind.Absent)
            {
                ValidateAbsentNode(node, schema);
                return;
            }
            if (node.NodeKind != schema.NodeKind)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Semantic node kind does not match schema.", node.Provenance);
            if (node.NodeKind == CanonicalSemanticNodeKind.Scalar)
                ValidateScalarNode(node, schema);
            else if (node.NodeKind == CanonicalSemanticNodeKind.Collection)
                ValidateCollectionNode(node, schema, generatorVersion);
            else
                ValidateStructuredNode(node, schema, generatorVersion);
        }

        /// <summary>
        /// 验证 absent 仅用于 optional 且不携带隐藏 scalar/children。
        /// </summary>
        private static void ValidateAbsentNode(
            CanonicalSemanticNode node,
            CanonicalSemanticNodeSchema schema)
        {
            if (schema.PresencePolicy != CanonicalPresencePolicy.Optional
                || node.ScalarValue != null
                || node.Children.Count != 0
                || node.CollectionSemantics != CanonicalCollectionSemantics.None)
            {
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Absent node is invalid.", node.Provenance);
            }
        }

        /// <summary>
        /// 验证 node type id、target domain 与 collection semantics；variant type 由 case 单独验证。
        /// </summary>
        private static void ValidateNodeIdentity(
            CanonicalSemanticNode node,
            CanonicalSemanticNodeSchema schema)
        {
            if (schema.NodeKind != CanonicalSemanticNodeKind.Variant
                && !string.Equals(node.TypeId, schema.TypeId, StringComparison.Ordinal))
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Node type id mismatch.", node.Provenance);
            if (!string.Equals(node.TargetDomainId ?? string.Empty, schema.TargetDomainId ?? string.Empty, StringComparison.Ordinal)
                || node.CollectionSemantics != schema.CollectionSemantics)
            {
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Node target domain/collection semantics mismatch.", node.Provenance);
            }
        }

        /// <summary>
        /// 验证 scalar union、enum domain、reference target domain 与无 hidden children。
        /// </summary>
        private static void ValidateScalarNode(
            CanonicalSemanticNode node,
            CanonicalSemanticNodeSchema schema)
        {
            var value = node.ScalarValue;
            if (value == null || node.Children.Count != 0 || value.Kind != schema.ScalarKind)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Scalar shape is invalid.", node.Provenance);
            if (value.Kind == CanonicalSemanticValueKind.Boolean && value.RawBits > 1)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Boolean bits are invalid.", node.Provenance);
            if (value.Kind == CanonicalSemanticValueKind.Enum
                && (!string.Equals(value.EnumTypeId, schema.TypeId, StringComparison.Ordinal)
                    || value.EnumUnderlyingType != schema.EnumUnderlyingType
                    || !CanonicalSemanticCompiler.IsKnownEnumValue(schema.TypeId, value.RawBits)))
            {
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Enum domain is invalid.", node.Provenance);
            }
            if (value.Kind == CanonicalSemanticValueKind.DefinitionReference)
                ValidateReferenceTarget(value.DefinitionReference, schema.TargetDomainId, node.Provenance);
        }

        /// <summary>
        /// 验证 reference key 对应固定 target domain/kind/arity。
        /// </summary>
        private static void ValidateReferenceTarget(
            CanonicalDefinitionKey key,
            string targetDomainId,
            CanonicalSemanticProvenance provenance)
        {
            if (key == null
                || !CanonicalSemanticSchemaRegistry.TryResolve(targetDomainId, out var targetSchema)
                || key.DomainOrdinal != targetSchema.DomainOrdinal
                || key.DefinitionKind != targetSchema.DefinitionKind
                || key.StableIdParts.Count != targetSchema.StableIdPartCount)
            {
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Reference target domain is invalid.", provenance);
            }
        }

        /// <summary>
        /// 验证 record/variant 成员闭集与 dense member ordinals。
        /// </summary>
        private static void ValidateStructuredNode(
            CanonicalSemanticNode node,
            CanonicalSemanticNodeSchema schema,
            string generatorVersion)
        {
            if (node.ScalarValue != null)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Structured node contains scalar payload.", node.Provenance);
            var members = schema.NodeKind == CanonicalSemanticNodeKind.Variant
                ? ResolveVariantMembers(node, schema)
                : schema.Children;
            if (node.Children.Count != members.Count)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Structured node member coverage is incomplete.", node.Provenance);
            for (var index = 0; index < members.Count; index++)
            {
                if (node.Children[index].NodeOrdinal != index)
                    Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Member ordinal is not dense.", node.Children[index].Provenance);
                ValidateNode(node.Children[index], members[index], generatorVersion, false);
            }
        }

        /// <summary>
        /// 解析 variant discriminator 对应的 payload members。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticNodeSchema> ResolveVariantMembers(
            CanonicalSemanticNode node,
            CanonicalSemanticNodeSchema schema)
        {
            for (var index = 0; index < schema.Variants.Count; index++)
                if (string.Equals(node.TypeId, schema.Variants[index].VariantTypeId, StringComparison.Ordinal))
                    return schema.Variants[index].Members;
            Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Variant discriminator is unknown.", node.Provenance);
            return null;
        }

        /// <summary>
        /// 验证 collection element ordinals、schema、canonical sort 与 set uniqueness。
        /// </summary>
        private static void ValidateCollectionNode(
            CanonicalSemanticNode node,
            CanonicalSemanticNodeSchema schema,
            string generatorVersion)
        {
            if (node.ScalarValue != null || schema.Children.Count != 1)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Collection shape is invalid.", node.Provenance);
            for (var index = 0; index < node.Children.Count; index++)
            {
                if (node.Children[index].NodeOrdinal != index)
                    Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Collection ordinal is not dense.", node.Children[index].Provenance);
                ValidateNode(node.Children[index], schema.Children[0], generatorVersion, false);
                if (index == 0 || schema.CollectionSemantics == CanonicalCollectionSemantics.Ordered)
                    continue;
                var comparison = CanonicalSemanticTreeValueComparer.Compare(node.Children[index - 1], node.Children[index]);
                if (comparison > 0 || (comparison == 0 && schema.CollectionSemantics == CanonicalCollectionSemantics.Set))
                    Fail(comparison == 0 ? CanonicalSemanticRuleCatalog.SemanticOwnerConflict : CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                        "Set/multiset order or uniqueness is invalid.", node.Children[index].Provenance);
            }
        }

        /// <summary>
        /// 验证前置 key 字段值与 DefinitionKey stable id parts 一致。
        /// </summary>
        private static void ValidateStableKey(CanonicalSemanticDefinition definition)
        {
            for (var index = 0; index < definition.Key.StableIdParts.Count; index++)
            {
                var root = definition.Fields[index].Root;
                if (root.NodeKind != CanonicalSemanticNodeKind.Scalar
                    || root.ScalarValue.Kind != CanonicalSemanticValueKind.Int32
                    || unchecked((int)root.ScalarValue.RawBits) != definition.Key.StableIdParts[index])
                    Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Stable key field mismatch.", root.Provenance);
            }
        }

        /// <summary>
        /// 验证 role dense ordinal、authoring role、owner target 与 role kind/domain 组合。
        /// </summary>
        private static void ValidateRoles(
            CanonicalSemanticDefinition definition,
            CanonicalDefinitionLookup<CanonicalSemanticDefinition> definitions,
            string generatorVersion)
        {
            var expected = BuildExpectedRoles(definition, definitions);
            if (definition.Roles.Count != expected.Count)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Sealed role coverage is non-canonical.", definition.Provenance);
            for (var index = 0; index < expected.Count; index++)
            {
                var role = definition.Roles[index];
                if (role.RoleOrdinal != index
                    || role.RoleKind != expected[index].RoleKind
                    || role.OwnerDefinition == null
                    || !role.OwnerDefinition.HasSameIdentity(expected[index].OwnerDefinition)
                    || role.OwnerFieldOrdinal != expected[index].OwnerFieldOrdinal
                    || !string.Equals(role.RuleId, CanonicalSemanticRuleCatalog.InvalidDomainOrReference, StringComparison.Ordinal)
                    || role.RuleVersion != 1)
                {
                    Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Role is not compiler-derived.", role.Provenance);
                }
                ValidateProvenance(role.Provenance, generatorVersion, role.RuleId);
            }
        }

        /// <summary>
        /// 从 authoring fields 与 incoming Ability references 重建唯一 role 列表。
        /// </summary>
        private static IReadOnlyList<ExpectedRole> BuildExpectedRoles(
            CanonicalSemanticDefinition target,
            CanonicalDefinitionLookup<CanonicalSemanticDefinition> definitions)
        {
            var result = new List<ExpectedRole>
            {
                new ExpectedRole(CanonicalDefinitionRoleKind.Authoring, target.Key, -1, target.Provenance),
            };
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions.GetValueAt(index);
                if (definition.Key.DefinitionKind != CanonicalDefinitionKind.Ability)
                    continue;
                AddExpectedAbilityRole(definition, target, "Cost", CanonicalDefinitionRoleKind.Cost, result);
                AddExpectedAbilityRole(definition, target, "CdEffect", CanonicalDefinitionRoleKind.Cooldown, result);
            }
            result.Sort(ExpectedRole.Compare);
            RejectExpectedRoleConflict(result);
            return result;
        }

        /// <summary>
        /// 在 Ability reference 精确指向目标时添加预期 Cost/Cooldown role。
        /// </summary>
        private static void AddExpectedAbilityRole(
            CanonicalSemanticDefinition ability,
            CanonicalSemanticDefinition target,
            string fieldId,
            CanonicalDefinitionRoleKind roleKind,
            ICollection<ExpectedRole> result)
        {
            var field = FindField(ability.Fields, fieldId);
            if (field == null || field.Root.NodeKind == CanonicalSemanticNodeKind.Absent)
                return;
            var value = field.Root.ScalarValue;
            if (value != null
                && value.Kind == CanonicalSemanticValueKind.DefinitionReference
                && value.DefinitionReference.HasSameIdentity(target.Key))
            {
                result.Add(new ExpectedRole(roleKind, ability.Key, field.FieldOrdinal, field.Provenance));
            }
        }

        /// <summary>
        /// 拒绝同一 Definition 的任何第二个非 Authoring owner，并返回双端 evidence。
        /// </summary>
        private static void RejectExpectedRoleConflict(IReadOnlyList<ExpectedRole> roles)
        {
            ExpectedRole owner = null;
            for (var index = 0; index < roles.Count; index++)
            {
                if (roles[index].RoleKind == CanonicalDefinitionRoleKind.Authoring)
                    continue;
                if (owner != null)
                {
                    throw new CanonicalSemanticValidationException(
                        CanonicalSemanticRuleCatalog.SemanticOwnerConflict,
                        "Definition has multiple semantic owners.",
                        owner.Provenance,
                        new[] { owner.Provenance, roles[index].Provenance });
                }
                owner = roles[index];
            }
        }

        /// <summary>
        /// 验证 dependency 完整覆盖 references、metadata、targets、ordinal 与 provenance。
        /// </summary>
        private static void ValidateDependencies(
            CanonicalSemanticDefinition definition,
            CanonicalDefinitionLookup<CanonicalSemanticDefinition> definitions,
            string generatorVersion)
        {
            var expected = CollectReferenceSignatures(definition.Fields);
            if (definition.Dependencies.Count != expected.Count)
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Dependency coverage does not match references.", definition.Provenance);
            for (var index = 0; index < expected.Count; index++)
            {
                var dependency = definition.Dependencies[index];
                var signature = expected[index];
                if (dependency.DependencyOrdinal != index
                    || dependency.SourceFieldOrdinal != signature.FieldOrdinal
                    || !string.Equals(dependency.DependencyId, signature.Path, StringComparison.Ordinal)
                    || !dependency.TargetDefinition.HasSameIdentity(signature.Target)
                    || !string.Equals(dependency.TargetDomainId, signature.TargetDomainId, StringComparison.Ordinal)
                    || dependency.DependencyKind != signature.DependencyKind
                    || dependency.OwnerKind != signature.OwnerKind
                    || dependency.Sign != signature.Sign
                    || dependency.WorkKind != signature.WorkKind
                    || dependency.CleanupPolicy != signature.CleanupPolicy
                    || !definitions.ContainsKey(dependency.TargetDefinition)
                    || dependency.MaxExpansion != 1)
                {
                    Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Dependency is not sealed to source reference.", dependency.Provenance);
                }
                ValidateDependencyMetadata(dependency);
                ValidateProvenance(dependency.Provenance, generatorVersion, CanonicalSemanticRuleCatalog.InvalidDomainOrReference);
            }
        }

        /// <summary>
        /// 验证 dependency owner/sign/work/cleanup 枚举与 target domain。
        /// </summary>
        private static void ValidateDependencyMetadata(CanonicalSemanticDependency dependency)
        {
            if (!CanonicalSemanticSchemaRegistry.TryResolve(dependency.TargetDomainId, out var schema)
                || dependency.TargetDefinition.DomainOrdinal != schema.DomainOrdinal
                || dependency.TargetDefinition.DefinitionKind != schema.DefinitionKind
                || dependency.DependencyKind < CanonicalDependencyKind.Reference
                || dependency.DependencyKind > CanonicalDependencyKind.Cleanup
                || dependency.OwnerKind < CanonicalDependencyOwnerKind.AuthoringField
                || dependency.OwnerKind > CanonicalDependencyOwnerKind.Execution
                || dependency.Sign > CanonicalDependencySign.Negative
                || dependency.WorkKind < CanonicalDependencyWorkKind.Read
                || dependency.WorkKind > CanonicalDependencyWorkKind.Cleanup
                || dependency.CleanupPolicy > CanonicalCleanupPolicy.Unsupported)
            {
                Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Dependency metadata is invalid.", dependency.Provenance);
            }
        }

        /// <summary>
        /// 收集全部显式 references 的 field/path/target signatures。
        /// </summary>
        private static IReadOnlyList<ReferenceSignature> CollectReferenceSignatures(
            IReadOnlyList<CanonicalSemanticField> fields)
        {
            var result = new List<ReferenceSignature>();
            for (var index = 0; index < fields.Count; index++)
                CollectReferenceSignatures(fields[index].Root, fields[index].FieldOrdinal, fields[index].FieldId, result);
            result.Sort(ReferenceSignature.Compare);
            return result;
        }

        /// <summary>
        /// 递归收集 reference signatures；optional none 只能由 Absent 节点表达。
        /// </summary>
        private static void CollectReferenceSignatures(
            CanonicalSemanticNode node,
            int fieldOrdinal,
            string path,
            ICollection<ReferenceSignature> result)
        {
            if (node.NodeKind == CanonicalSemanticNodeKind.Scalar
                && node.ScalarValue.Kind == CanonicalSemanticValueKind.DefinitionReference)
            {
                var fieldId = path;
                var separator = fieldId.IndexOfAny(new[] { '.', '[' });
                if (separator >= 0)
                    fieldId = fieldId.Substring(0, separator);
                CanonicalSemanticCompiler.ResolveDependencyMetadata(
                    fieldId, path, out var owner, out var sign, out var work, out var cleanup);
                result.Add(new ReferenceSignature(
                    fieldOrdinal,
                    path,
                    node.TargetDomainId,
                    node.ScalarValue.DefinitionReference,
                    CanonicalSemanticCompiler.ResolveDependencyKind(sign, work, cleanup),
                    owner,
                    sign,
                    work,
                    cleanup));
            }
            for (var index = 0; index < node.Children.Count; index++)
            {
                var child = node.Children[index];
                var suffix = child.NodeId == "$element"
                    ? "[" + child.NodeOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]"
                    : "." + child.NodeId;
                CollectReferenceSignatures(child, fieldOrdinal, path + suffix, result);
            }
        }

        /// <summary>
        /// 验证 programs 非自愿声明、matrix decisions、强制 operation/readset/order/bindings 与局部 DAG。
        /// </summary>
        private static void ValidatePrograms(
            CanonicalSemanticDefinition definition,
            CanonicalSemanticDomainSchema domain,
            string generatorVersion)
        {
            var expectedKinds = BuildExpectedProgramKinds(definition);
            if (definition.Programs.Count != expectedKinds.Count)
                Fail(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Sealed program coverage is non-canonical.", definition.Provenance);
            for (var index = 0; index < definition.Programs.Count; index++)
            {
                var program = definition.Programs[index];
                if (!domain.TryResolveProgram(program.ProgramKind, out var schema))
                    Fail(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Program kind is outside the fixed matrix.", program.Provenance);
                if (program.ProgramOrdinal != index
                    || program.ProgramKind != expectedKinds[index]
                    || program.Disposition != schema.Disposition
                    || !string.Equals(
                        program.ProgramId,
                        "sealed." + ((byte)program.ProgramKind).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        StringComparison.Ordinal))
                    Fail(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Program schema binding is invalid.", program.Provenance);
                if (program.ProgramKind == CanonicalProgramKind.CostMutation
                    || program.ProgramKind == CanonicalProgramKind.CooldownGate)
                {
                    CanonicalSemanticCompiler.ValidateAllowedProgram(
                        new SemanticDefinitionDraft(
                            definition.DefinitionOrdinal,
                            definition.Key,
                            definition.Fields,
                            definition.Provenance),
                        program.ProgramKind);
                }
                ValidateProvenance(program.Provenance, generatorVersion, schema.RuleId);
                ValidateDecisions(definition, program, schema, generatorVersion);
                ValidateProgramBody(definition, program, schema, generatorVersion);
            }
        }

        /// <summary>
        /// 由 exact roles 与 active Stacking 字段重建 compiler 唯一 program kind 顺序。
        /// </summary>
        private static IReadOnlyList<CanonicalProgramKind> BuildExpectedProgramKinds(
            CanonicalSemanticDefinition definition)
        {
            var result = new List<CanonicalProgramKind>();
            for (var index = 0; index < definition.Roles.Count; index++)
            {
                if (definition.Roles[index].RoleKind == CanonicalDefinitionRoleKind.Cost)
                    result.Add(CanonicalProgramKind.CostMutation);
                else if (definition.Roles[index].RoleKind == CanonicalDefinitionRoleKind.Cooldown)
                    result.Add(CanonicalProgramKind.CooldownGate);
            }
            if (result.Count == 0
                && (definition.Key.DefinitionKind == CanonicalDefinitionKind.Ability
                    || definition.Key.DefinitionKind == CanonicalDefinitionKind.GameplayEffect))
            {
                result.Add(CanonicalProgramKind.DirectEffect);
            }
            var stacking = FindField(definition.Fields, "Stacking");
            if (definition.Key.DefinitionKind == CanonicalDefinitionKind.GameplayEffect
                && stacking != null
                && CanonicalSemanticCompiler.IsActive(stacking.Root))
            {
                result.Add(CanonicalProgramKind.StackTemporal);
            }
            return result;
        }

        /// <summary>
        /// 验证 contextual decisions 全覆盖、dense ordinal、active fact、分类、裁决与 RuleId。
        /// </summary>
        private static void ValidateDecisions(
            CanonicalSemanticDefinition definition,
            CanonicalSemanticProgram program,
            CanonicalSemanticProgramSchema schema,
            string generatorVersion)
        {
            if (program.Decisions.Count != schema.FieldDecisions.Count)
                Fail(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Contextual decision coverage is incomplete.", program.Provenance);
            for (var index = 0; index < schema.FieldDecisions.Count; index++)
            {
                var actual = program.Decisions[index];
                var expected = schema.FieldDecisions[index];
                var field = definition.Fields[expected.FieldOrdinal];
                if (actual.DecisionOrdinal != index
                    || actual.FieldOrdinal != expected.FieldOrdinal
                    || !string.Equals(actual.FieldId, expected.FieldId, StringComparison.Ordinal)
                    || actual.IsActive != CanonicalSemanticCompiler.IsActive(field.Root)
                    || actual.Classification != expected.Classification
                    || actual.Disposition != expected.Disposition
                    || !string.Equals(actual.RuleId, expected.RuleId, StringComparison.Ordinal)
                    || actual.RuleVersion != expected.RuleVersion)
                    Fail(CanonicalSemanticRuleCatalog.NonCanonicalIdentity, "Contextual decision is non-canonical.", actual.Provenance);
                ValidateProvenance(actual.Provenance, generatorVersion, actual.RuleId);
            }
        }

        /// <summary>
        /// 验证 denied program 无执行体，allowed program exact operations、bindings 与连续 Control edges。
        /// </summary>
        private static void ValidateProgramBody(
            CanonicalSemanticDefinition definition,
            CanonicalSemanticProgram program,
            CanonicalSemanticProgramSchema schema,
            string generatorVersion)
        {
            if (schema.Disposition == CanonicalFieldDisposition.Denied)
            {
                if (program.Nodes.Count != 0 || program.Edges.Count != 0)
                    Fail(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Denied program contains executable nodes.", program.Provenance);
                return;
            }
            if (program.Nodes.Count != schema.Operations.Count || program.Edges.Count != Math.Max(0, program.Nodes.Count - 1))
                Fail(CanonicalSemanticRuleCatalog.MissingValueViewOrPhase, "Required operation count/order is incomplete.", program.Provenance);
            for (var index = 0; index < program.Nodes.Count; index++)
                ValidateProgramNode(definition, program, program.Nodes[index], schema.Operations[index], generatorVersion);
            for (var index = 0; index < program.Edges.Count; index++)
                ValidateProgramEdge(program.Edges[index], index, generatorVersion);
        }

        /// <summary>
        /// 验证 node operation、field/dependency readsets 与 sealed projection payload。
        /// </summary>
        private static void ValidateProgramNode(
            CanonicalSemanticDefinition definition,
            CanonicalSemanticProgram program,
            CanonicalSemanticProgramNode node,
            CanonicalSemanticOperationSchema operation,
            string generatorVersion)
        {
            var expectedFields = ResolveFieldOrdinals(definition.Fields, operation.FieldIds);
            var expectedDependencies = ResolveDependencyOrdinals(definition.Dependencies, expectedFields);
            if (node.NodeOrdinal != operation.OperationOrdinal
                || !string.Equals(node.OperationId, operation.OperationId, StringComparison.Ordinal)
                || !HaveSameOrdinals(node.FieldOrdinals, expectedFields)
                || !HaveSameOrdinals(node.DependencyOrdinals, expectedDependencies))
                Fail(CanonicalSemanticRuleCatalog.MissingValueViewOrPhase, "Operation/readset is not the sealed template.", node.Provenance);
            ValidateProvenance(node.Provenance, generatorVersion, operation.RuleId);
            ValidateProjection(definition, program, node, operation, expectedFields, expectedDependencies, generatorVersion);
        }

        /// <summary>
        /// 验证 projection owner/node/field/ValueView/phase/target/bounds/payload/rule 全部绑定。
        /// </summary>
        private static void ValidateProjection(
            CanonicalSemanticDefinition definition,
            CanonicalSemanticProgram program,
            CanonicalSemanticProgramNode node,
            CanonicalSemanticOperationSchema operation,
            IReadOnlyList<int> fieldOrdinals,
            IReadOnlyList<int> dependencyOrdinals,
            string generatorVersion)
        {
            var projection = node.Projection;
            var expectedCount = CanonicalSemanticCompiler.ResolveBoundCount(definition.Fields, operation.BoundFieldId);
            var payload = CanonicalSemanticCompiler.EncodeProjectionPayload(operation, definition.Fields, fieldOrdinals, dependencyOrdinals);
            var expectedBytes = payload.Length;
            if (projection == null
                || projection.ProjectionOrdinal != node.NodeOrdinal
                || !projection.OwnerDefinition.HasSameIdentity(definition.Key)
                || projection.ProgramOrdinal != program.ProgramOrdinal
                || projection.NodeOrdinal != node.NodeOrdinal
                || projection.SourceFieldOrdinal != fieldOrdinals[0]
                || !string.Equals(projection.OperationId, operation.OperationId, StringComparison.Ordinal)
                || !string.Equals(projection.ValueView, operation.ValueView, StringComparison.Ordinal)
                || !string.Equals(projection.Phase, operation.Phase, StringComparison.Ordinal)
                || !string.Equals(projection.TargetDomainId, operation.TargetDomainId, StringComparison.Ordinal)
                || projection.MaxItemCount != expectedCount
                || projection.MaxPayloadBytes != expectedBytes
                || !HaveSameBytes(projection.PayloadUnsafe, payload)
                || !string.Equals(projection.RuleId, operation.RuleId, StringComparison.Ordinal)
                || projection.RuleVersion != operation.RuleVersion)
                Fail(CanonicalSemanticRuleCatalog.NonCanonicalIdentity, "Projection binding is non-canonical.", projection?.Provenance);
            ValidateProvenance(projection.Provenance, generatorVersion, projection.RuleId);
        }

        /// <summary>
        /// 验证连续 Control edge 与 CFG1301 provenance。
        /// </summary>
        private static void ValidateProgramEdge(
            CanonicalSemanticProgramEdge edge,
            int ordinal,
            string generatorVersion)
        {
            if (edge.EdgeOrdinal != ordinal
                || edge.SourceNodeOrdinal != ordinal
                || edge.TargetNodeOrdinal != ordinal + 1
                || edge.EdgeKind != CanonicalProgramEdgeKind.Control)
                Fail(CanonicalSemanticRuleCatalog.DependencyOrCleanupUnbounded, "Program edge order is invalid.", edge.Provenance);
            ValidateProvenance(edge.Provenance, generatorVersion, CanonicalSemanticRuleCatalog.DependencyOrCleanupUnbounded);
        }

        /// <summary>
        /// 解析 operation 的固定 field ids 为 ordinal 列表。
        /// </summary>
        private static IReadOnlyList<int> ResolveFieldOrdinals(
            IReadOnlyList<CanonicalSemanticField> fields,
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
                    Fail(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Operation field is missing.");
            }
            Array.Sort(result);
            return result;
        }

        /// <summary>
        /// 汇总 source field ordinals 对应的 dependency ordinals。
        /// </summary>
        private static IReadOnlyList<int> ResolveDependencyOrdinals(
            IReadOnlyList<CanonicalSemanticDependency> dependencies,
            IReadOnlyList<int> fieldOrdinals)
        {
            var result = new List<int>();
            for (var index = 0; index < dependencies.Count; index++)
                if (Contains(fieldOrdinals, dependencies[index].SourceFieldOrdinal))
                    result.Add(dependencies[index].DependencyOrdinal);
            return result;
        }

        /// <summary>
        /// 判断 ordinal 列表是否包含目标值。
        /// </summary>
        private static bool Contains(IReadOnlyList<int> values, int target)
        {
            for (var index = 0; index < values.Count; index++)
                if (values[index] == target)
                    return true;
            return false;
        }

        /// <summary>
        /// 对 graph dependencies 再次执行 proof-relevant 跨 definition cycle 检查。
        /// </summary>
        private static void ValidateCrossDefinitionCycles(IReadOnlyList<CanonicalSemanticDefinition> definitions)
        {
            var arcs = new List<CanonicalDependencyCycleArc>();
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                for (var edgeIndex = 0; edgeIndex < definition.Dependencies.Count; edgeIndex++)
                    arcs.Add(new CanonicalDependencyCycleArc(definition.Key, definition.Dependencies[edgeIndex]));
            }
            CanonicalSemanticDependencyCycleValidator.Validate(arcs.ToArray());
        }

        /// <summary>
        /// 验证 provenance 稳定文本、RuleId/version、generator、路径与 related key 顺序。
        /// </summary>
        private static void ValidateProvenance(
            CanonicalSemanticProvenance provenance,
            string generatorVersion,
            string expectedRuleId)
        {
            if (provenance == null
                || string.IsNullOrWhiteSpace(provenance.WorkbookId)
                || string.IsNullOrWhiteSpace(provenance.TableId)
                || string.IsNullOrWhiteSpace(provenance.RowStableId)
                || string.IsNullOrWhiteSpace(provenance.FieldPath)
                || provenance.RawValue == null
                || provenance.NormalizedValue == null
                || !string.Equals(provenance.GeneratorVersion, generatorVersion, StringComparison.Ordinal)
                || !string.Equals(provenance.RuleId, expectedRuleId, StringComparison.Ordinal)
                || !CanonicalSemanticRuleCatalog.TryResolve(provenance.RuleId, out var version, out _)
                || provenance.RuleVersion != version
                || IsRootedPath(provenance.WorkbookId)
                || IsRootedPath(provenance.TableId))
                Fail(CanonicalSemanticRuleCatalog.NonCanonicalIdentity, "Provenance is invalid.", provenance);
            for (var index = 1; index < provenance.RelatedDefinitionIds.Count; index++)
                if (provenance.RelatedDefinitionIds[index - 1].CompareTo(provenance.RelatedDefinitionIds[index]) >= 0)
                    Fail(CanonicalSemanticRuleCatalog.NonCanonicalIdentity, "Related Definition ids are duplicate or unsorted.", provenance);
        }

        /// <summary>
        /// 以 host-independent lexical 规则拒绝绝对或 drive-rooted identity 路径。
        /// </summary>
        private static bool IsRootedPath(string value)
        {
            if (value.StartsWith("/", StringComparison.Ordinal)
                || value.StartsWith("\\", StringComparison.Ordinal))
                return true;
            return value.Length >= 3
                   && char.IsLetter(value[0])
                   && value[1] == ':'
                   && (value[2] == '/' || value[2] == '\\');
        }

        /// <summary>
        /// 按固定 FieldId 查找 canonical 字段。
        /// </summary>
        private static CanonicalSemanticField FindField(
            IReadOnlyList<CanonicalSemanticField> fields,
            string fieldId)
        {
            for (var index = 0; index < fields.Count; index++)
            {
                if (string.Equals(fields[index].FieldId, fieldId, StringComparison.Ordinal))
                    return fields[index];
            }

            return null;
        }

        /// <summary>
        /// 比较两个 ordinal 列表。
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
        /// 逐 byte 比较 payload。
        /// </summary>
        private static bool HaveSameBytes(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
                return false;
            for (var index = 0; index < left.Length; index++)
                if (left[index] != right[index])
                    return false;
            return true;
        }

        /// <summary>
        /// 抛出稳定 RuleId 验证失败。
        /// </summary>
        private static void Fail(
            string ruleId,
            string message,
            CanonicalSemanticProvenance provenance = null)
        {
            throw new CanonicalSemanticValidationException(ruleId, message, provenance);
        }

        /// <summary>
        /// 保存一个从 incoming reference 派生的预期 role。
        /// </summary>
        private sealed class ExpectedRole
        {
            /// <summary>
            /// 创建 role kind、owner、owner field 与冲突 evidence。
            /// </summary>
            public ExpectedRole(
                CanonicalDefinitionRoleKind roleKind,
                CanonicalDefinitionKey ownerDefinition,
                int ownerFieldOrdinal,
                CanonicalSemanticProvenance provenance)
            {
                RoleKind = roleKind;
                OwnerDefinition = ownerDefinition;
                OwnerFieldOrdinal = ownerFieldOrdinal;
                Provenance = provenance;
            }

            public CanonicalDefinitionRoleKind RoleKind { get; }

            public CanonicalDefinitionKey OwnerDefinition { get; }

            public int OwnerFieldOrdinal { get; }

            public CanonicalSemanticProvenance Provenance { get; }

            /// <summary>
            /// 按 compiler 的 kind、owner key 与 owner field 顺序比较。
            /// </summary>
            public static int Compare(ExpectedRole left, ExpectedRole right)
            {
                var result = ((byte)left.RoleKind).CompareTo((byte)right.RoleKind);
                if (result != 0)
                    return result;
                result = left.OwnerDefinition.CompareTo(right.OwnerDefinition);
                return result != 0 ? result : left.OwnerFieldOrdinal.CompareTo(right.OwnerFieldOrdinal);
            }
        }

        /// <summary>
        /// 保存一个从字段 tree 派生的预期 dependency signature。
        /// </summary>
        private sealed class ReferenceSignature
        {
            /// <summary>
            /// 创建 field/path/target signature。
            /// </summary>
            public ReferenceSignature(
                int fieldOrdinal,
                string path,
                string targetDomainId,
                CanonicalDefinitionKey target,
                CanonicalDependencyKind dependencyKind,
                CanonicalDependencyOwnerKind ownerKind,
                CanonicalDependencySign sign,
                CanonicalDependencyWorkKind workKind,
                CanonicalCleanupPolicy cleanupPolicy)
            {
                FieldOrdinal = fieldOrdinal;
                Path = path;
                TargetDomainId = targetDomainId;
                Target = target;
                DependencyKind = dependencyKind;
                OwnerKind = ownerKind;
                Sign = sign;
                WorkKind = workKind;
                CleanupPolicy = cleanupPolicy;
            }

            public int FieldOrdinal { get; }

            public string Path { get; }

            public string TargetDomainId { get; }

            public CanonicalDefinitionKey Target { get; }

            public CanonicalDependencyKind DependencyKind { get; }

            public CanonicalDependencyOwnerKind OwnerKind { get; }

            public CanonicalDependencySign Sign { get; }

            public CanonicalDependencyWorkKind WorkKind { get; }

            public CanonicalCleanupPolicy CleanupPolicy { get; }

            /// <summary>
            /// 按 field/path/target key 排序 signatures。
            /// </summary>
            public static int Compare(ReferenceSignature left, ReferenceSignature right)
            {
                var result = left.FieldOrdinal.CompareTo(right.FieldOrdinal);
                if (result != 0)
                    return result;
                result = string.CompareOrdinal(left.Path, right.Path);
                return result != 0 ? result : left.Target.CompareTo(right.Target);
            }
        }
    }
}
