using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using GAS.Editor.CodeGen.Proofs.Typed;
using GAS.Editor.CodeGen.Semantics;

namespace GAS.Editor.CodeGen.Semantics.Tests
{
    /// <summary>
    /// 在不启动 Unity、Luban 或 SourceGen 的隔离 C#9 宿主中验证 v2 semantic source、sealed graph 与 TypedContract。
    /// </summary>
    internal static class Program
    {
        private const string ExpectedTypedContractHash = "47fd322a971d026603f04ab7e9bc0a6ca0b71a374a4441d46abe6ee5ccceeb6f";
        private const string ExpectedTypedValueTreeContractHash = "ddcb0eab144656adc03283c4d3a01ee21aff373632e9d3a214469f82db4e54d2";
        private const string ExpectedTypedValueTreeHash = "9bd19cbeb34677652549175e22d03f3167781dbac237db328af6d10051f5bc52";

        /// <summary>
        /// 运行全部断言，或在子进程模式输出可逐字节比较的确定性向量。
        /// </summary>
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && string.Equals(args[0], "--emit", StringComparison.Ordinal))
                {
                    EmitDeterminismVector(args);
                    return 0;
                }

                RunAll();
                Console.WriteLine("PASS GasCanonicalSemanticsDeterminism-v2");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        /// <summary>
        /// 执行 P0 source/graph/TypedContract 安全回归与独立确定性测试。
        /// </summary>
        private static void RunAll()
        {
            RegistryClassifiesEveryRealField();
            RealGe3001CueOnTickIsStableCfg1101();
            UnsupportedCostAndCooldownFieldsAreCfg1101();
            OptionalAndOrderedArraysDoNotCollide();
            EveryRichGameplayShapeChangesContentIdentity();
            SetSemanticsAreCanonicalAndFailClosed();
            CallerCannotInjectCompileResults();
            DualOwnerAndSignedCyclePolicyArePrecise();
            LongDependencyChainIsDepthSafe();
            CollisionResistantDefinitionLookupIsDeterministic();
            CompilerProvenanceExpansionIsBounded();
            DuplicateCollectionElementOrdinalIsRejected();
            SourceBudgetsFailBeforeCopyOrTraversal();
            GraphAndTypedAggregateBudgetsCoverNestedFields();
            SourceAndToolchainIdentityAreBound();
            TypedFieldRootShapeMustMatchRegistry();
            TypedEnumsAndFixedSchemaAreClosed();
            TypedContractRoundTripAndExpectedHashAreBound();
            TypedValueTreeAdapterIsBoundAndReadOnly();
            DifferentProcessesCulturesAndWorkdirsAreDeterministic();
        }

        /// <summary>
        /// 验证真实 Ability/GameplayEffect registry 字段数与分类均为闭集且 coverage 固定为 Red。
        /// </summary>
        private static void RegistryClassifiesEveryRealField()
        {
            AssertRegistry(CanonicalSemanticSchemaRegistry.AbilityDomain, 14);
            AssertRegistry(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, 22);
            var graph = CanonicalSemanticProjector.Project(SourceFixture.CreateOptionalDurationDocument(false));
            AssertEx.Equal(CanonicalSemanticCoverageState.Red, graph.Coverage.SchemaCoverage, "SchemaCoverage");
            AssertEx.Equal(CanonicalSemanticCoverageState.Red, graph.Coverage.ReferenceCoverage, "ReferenceCoverage");
            AssertEx.Equal(CanonicalSemanticCoverageState.Red, graph.Coverage.ProgramCoverage, "ProgramCoverage");
            AssertEx.Equal(CanonicalSemanticCoverageState.Red, graph.Coverage.SameParseCoverage, "SameParseCoverage");
            AssertEx.Equal(CanonicalSemanticCoverageState.Red, graph.Coverage.Eligibility, "Eligibility");
        }

        /// <summary>
        /// 检查一个 domain 的每个字段均有 ordinal、分类、RuleId 与完整节点 schema。
        /// </summary>
        private static void AssertRegistry(string domainId, int expectedCount)
        {
            AssertEx.True(CanonicalSemanticSchemaRegistry.TryResolve(domainId, out var domain), domainId + " registry");
            AssertEx.Equal(expectedCount, domain.Fields.Count, domainId + " field count");
            for (var index = 0; index < domain.Fields.Count; index++)
            {
                var field = domain.Fields[index];
                AssertEx.Equal(index, field.FieldOrdinal, field.FieldId + " ordinal");
                AssertEx.True(field.Classification != CanonicalFieldClassification.Unknown, field.FieldId + " classification");
                AssertEx.True(!string.IsNullOrWhiteSpace(field.RuleId), field.FieldId + " RuleId");
                AssertEx.True(field.Root != null, field.FieldId + " root schema");
            }
        }

        /// <summary>
        /// 用仓库真实 GE3001 shape 验证 CueOnTick=5000 在 cooldown 角色下先于 dangling/双 owner 稳定返回 CFG1101。
        /// </summary>
        private static void RealGe3001CueOnTickIsStableCfg1101()
        {
            var failure = AssertEx.Rule(
                () => CanonicalSemanticProjector.Project(SourceFixture.CreateRealGe3001Document(true)),
                CanonicalSemanticRuleCatalog.UnsupportedContractField,
                "GE3001 CueOnTick=5000");
            AssertEx.True(failure.Provenance != null, "GE3001 diagnostic provenance");
            AssertEx.True(failure.Provenance.FieldPath.Contains("CueOnTick"), "GE3001 diagnostic field path");
            AssertEx.Equal("3001", failure.Provenance.RowStableId, "GE3001 diagnostic row");
        }

        /// <summary>
        /// 验证 Cooldown GrantedTags/Duration 与 Cost Modifier 在未冻结 RuntimeV1 语义前均显式 CFG1101。
        /// </summary>
        private static void UnsupportedCostAndCooldownFieldsAreCfg1101()
        {
            AssertFieldRule(
                SourceFixture.CreateCooldownGrantedTagsDocument(),
                "GrantedTags",
                "Cooldown GrantedTags");
            AssertFieldRule(
                SourceFixture.CreateCooldownDurationDocument(),
                "Duration",
                "Cooldown Duration subfields");
            AssertFieldRule(
                SourceFixture.CreateCostModifierDocument(),
                "Modifiers",
                "Cost Modifier operation mapping");
        }

        /// <summary>
        /// 断言 source 投影以 CFG1101 失败且 provenance 指向预期字段。
        /// </summary>
        private static void AssertFieldRule(
            LubanSemanticSourceDocument source,
            string fieldId,
            string context)
        {
            var failure = AssertEx.Rule(
                () => Project(source),
                CanonicalSemanticRuleCatalog.UnsupportedContractField,
                context);
            AssertEx.True(failure.Provenance.FieldPath.Contains(fieldId), context + " provenance");
        }

        /// <summary>
        /// 验证 optional absent/present-zero 与 ordered AbilityExecution 第二项都进入 content identity。
        /// </summary>
        private static void OptionalAndOrderedArraysDoNotCollide()
        {
            var absent = Project(SourceFixture.CreateOptionalDurationDocument(false));
            var presentZero = Project(SourceFixture.CreateOptionalDurationDocument(true));
            AssertEx.NotEqual(ContentVector(absent), ContentVector(presentZero), "Duration absent vs present-zero");

            var second1001 = Project(SourceFixture.CreateAbilityExecutionDocument(1001));
            var second1003 = Project(SourceFixture.CreateAbilityExecutionDocument(1003));
            AssertEx.NotEqual(ContentVector(second1001), ContentVector(second1003), "AbilityExecution IDs second element");
            var ids = FindNode(FindDefinition(second1001, CanonicalDefinitionKind.Ability, 10002), "AbilityExecution", "Param", "IDs");
            AssertEx.Equal(2, ids.Children.Count, "AbilityExecution complete ordered IDs");
            AssertEx.Equal(0, ids.Children[0].NodeOrdinal, "AbilityExecution first ordinal");
            AssertEx.Equal(1, ids.Children[1].NodeOrdinal, "AbilityExecution second ordinal");
            AssertEx.Equal(1001L, ids.Children[1].ScalarValue.DefinitionReference.StableIdParts[0], "AbilityExecution second ID");
        }

        /// <summary>
        /// 验证 TargetPolicy/Evaluator、六类 Cue、Granted、Duration/Period/Modifier/requirements/Stacking 任一变化均改 hash。
        /// </summary>
        private static void EveryRichGameplayShapeChangesContentIdentity()
        {
            var baseline = ContentVector(Project(SourceFixture.CreateRichDocument(RichMutation.None, false, false)));
            foreach (RichMutation mutation in Enum.GetValues(typeof(RichMutation)))
            {
                if (mutation == RichMutation.None)
                    continue;
                var changed = ContentVector(Project(SourceFixture.CreateRichDocument(mutation, false, false)));
                AssertEx.NotEqual(baseline, changed, "Rich field mutation " + mutation);
            }
        }

        /// <summary>
        /// 验证 set 输入乱序得到同 graph/hash，重复元素以双端 provenance CFG1001 拒绝。
        /// </summary>
        private static void SetSemanticsAreCanonicalAndFailClosed()
        {
            var forward = Project(SourceFixture.CreateRichDocument(RichMutation.None, false, false));
            var reversed = Project(SourceFixture.CreateRichDocument(RichMutation.None, true, false));
            AssertEx.BytesEqual(CanonicalSemanticBinaryCodec.Encode(forward), CanonicalSemanticBinaryCodec.Encode(reversed), "Set order graph bytes");
            AssertEx.Equal(GraphIdentity(forward).GraphHash, GraphIdentity(reversed).GraphHash, "Set order GraphHash");

            var failure = AssertEx.Rule(
                () => Project(SourceFixture.CreateRichDocument(RichMutation.None, false, true)),
                CanonicalSemanticRuleCatalog.SemanticOwnerConflict,
                "Duplicate set element");
            AssertEx.Equal(2, failure.Evidence.Count, "Duplicate set evidence endpoints");
        }

        /// <summary>
        /// 验证 source DTO 无 role/program/dependency 注入口且 graph 无公开构造入口。
        /// </summary>
        private static void CallerCannotInjectCompileResults()
        {
            var sourceType = typeof(LubanSemanticSourceDefinition);
            var forbidden = new[] { "Program", "Role", "Dependency", "ReadSet", "Operation" };
            foreach (var property in sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                AssertNameDoesNotContain(property.Name, forbidden, "source property");
            foreach (var constructor in sourceType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                foreach (var parameter in constructor.GetParameters())
                    AssertNameDoesNotContain(parameter.ParameterType.Name, forbidden, "source constructor parameter");
            AssertEx.Equal(0, typeof(CanonicalNormalizedSemanticGraph).GetConstructors().Length, "graph public constructors");

            var graph = Project(SourceFixture.CreateAbilityExecutionDocument(1001));
            var ability = FindDefinition(graph, CanonicalDefinitionKind.Ability, 10002);
            AssertEx.True(ability.Programs.Count > 0, "sealed compiler generated programs");
            AssertEx.True(ability.Dependencies.Count == 2, "sealed compiler generated complete dependencies");
        }

        /// <summary>
        /// 拒绝公开 DTO 名称中出现 caller 可伪造的编译结果概念。
        /// </summary>
        private static void AssertNameDoesNotContain(string name, IReadOnlyList<string> forbidden, string context)
        {
            for (var index = 0; index < forbidden.Count; index++)
                AssertEx.True(name.IndexOf(forbidden[index], StringComparison.OrdinalIgnoreCase) < 0, context + " " + name);
        }

        /// <summary>
        /// 验证双 owner 仍 CFG1001，neutral/positive 环允许，含 negative 边的环稳定 CFG1301。
        /// </summary>
        private static void DualOwnerAndSignedCyclePolicyArePrecise()
        {
            var owner = AssertEx.Rule(
                () => Project(SourceFixture.CreateRealGe3001Document(false)),
                CanonicalSemanticRuleCatalog.SemanticOwnerConflict,
                "Ability Cd and GE Duration dual owner");
            AssertEx.Equal(2, owner.Evidence.Count, "Dual owner evidence endpoints");
            var neutral = Project(SourceFixture.CreateNeutralTagCycleDocument());
            var neutralEdge = FindDefinition(neutral, CanonicalDefinitionKind.GameplayTag, 7001).Dependencies[0];
            AssertEx.Equal(CanonicalDependencyKind.Reference, neutralEdge.DependencyKind, "Neutral cycle dependency kind");
            AssertEx.Equal(CanonicalDependencySign.Neutral, neutralEdge.Sign, "Neutral cycle dependency sign");
            var positive = Project(SourceFixture.CreateCycleDocument());
            var positiveEdge = FindDefinition(positive, CanonicalDefinitionKind.Ability, 10002).Dependencies[0];
            AssertEx.Equal(CanonicalDependencyKind.Positive, positiveEdge.DependencyKind, "Positive cycle dependency kind");
            AssertEx.Equal(CanonicalDependencySign.Positive, positiveEdge.Sign, "Positive cycle dependency sign");
            AssertNegativeCycleFails();
        }

        /// <summary>
        /// 构造一条 negative 边和一条 neutral 回边，直接验证 proof-relevant 环拒绝。
        /// </summary>
        private static void AssertNegativeCycleFails()
        {
            var graph = Project(SourceFixture.CreateNeutralTagCycleDocument());
            var first = FindDefinition(graph, CanonicalDefinitionKind.GameplayTag, 7001);
            var second = FindDefinition(graph, CanonicalDefinitionKind.GameplayTag, 7002);
            var arcs = new[]
            {
                new CanonicalDependencyCycleArc(
                    first.Key, second.Key, CanonicalDependencyKind.Negative,
                    CanonicalDependencySign.Negative, CanonicalDependencyWorkKind.Mutation,
                    CanonicalCleanupPolicy.None, 1, first.Provenance),
                new CanonicalDependencyCycleArc(
                    second.Key, first.Key, CanonicalDependencyKind.Reference,
                    CanonicalDependencySign.Neutral, CanonicalDependencyWorkKind.Read,
                    CanonicalCleanupPolicy.None, 1, second.Provenance),
            };
            AssertEx.Rule(
                () => CanonicalSemanticDependencyCycleValidator.Validate(arcs),
                CanonicalSemanticRuleCatalog.DependencyOrCleanupUnbounded,
                "Negative dependency cycle");
        }

        /// <summary>
        /// 用 4000 个 Definition 的非环长链验证环证明不使用递归 stack。
        /// </summary>
        private static void LongDependencyChainIsDepthSafe()
        {
            const int count = 4000;
            var keys = new CanonicalDefinitionKey[count];
            for (var index = 0; index < count; index++)
                keys[index] = new CanonicalDefinitionKey(5, CanonicalDefinitionKind.GameplayTag, new[] { (long)index + 1 });
            var arcs = new CanonicalDependencyCycleArc[count - 1];
            for (var index = 0; index < arcs.Length; index++)
            {
                arcs[index] = new CanonicalDependencyCycleArc(
                    keys[index], keys[index + 1],
                    CanonicalDependencyKind.Negative,
                    CanonicalDependencySign.Negative,
                    CanonicalDependencyWorkKind.Mutation,
                    CanonicalCleanupPolicy.None, 1, null);
            }
            CanonicalSemanticDependencyCycleValidator.Validate(arcs);
            var parameter = typeof(CanonicalSemanticDependencyCycleValidator)
                .GetMethod("Validate", BindingFlags.Public | BindingFlags.Static)
                .GetParameters()[0];
            AssertEx.Equal(
                typeof(CanonicalDependencyCycleArc[]),
                parameter.ParameterType,
                "Dependency SCC accepts only sealed array input");
        }

        /// <summary>
        /// 用 4096 个相同 GetHashCode 的不同 key 验证生产索引只依赖排序数组与二分查找。
        /// </summary>
        private static void CollisionResistantDefinitionLookupIsDeterministic()
        {
            const int count = CanonicalSemanticResourceBudget.MaxSingleCollectionCount;
            var keys = new CanonicalDefinitionKey[count];
            for (var index = 0; index < count; index++)
            {
                var collisionPart = (unchecked((long)(uint)index) << 32) | unchecked((uint)index);
                keys[index] = new CanonicalDefinitionKey(
                    1,
                    CanonicalDefinitionKind.Ability,
                    new[] { collisionPart });
            }
            AssertEx.Equal(keys[0].GetHashCode(), keys[count - 1].GetHashCode(), "Controlled key hash collision");
            var lookup = new CanonicalDefinitionLookup<int>(keys);
            for (var index = count - 1; index >= 0; index--)
                lookup.Add(keys[index], index);
            for (var index = 0; index < count; index++)
                AssertEx.Equal(index, lookup[keys[index]], "Collision-free binary lookup " + index);
        }

        /// <summary>
        /// 验证 dependency path 在拼接前失败，且 dependency/projection provenance 仅保存固定长度 digest。
        /// </summary>
        private static void CompilerProvenanceExpansionIsBounded()
        {
            var oversizedSegment = new string('p', CanonicalSemanticResourceBudget.MaxSingleStringBytes);
            var child = new CanonicalSemanticNode(
                0,
                oversizedSegment,
                CanonicalSemanticNodeKind.Absent,
                string.Empty,
                string.Empty,
                CanonicalCollectionSemantics.None,
                null,
                null,
                null);
            AssertEx.Rule(
                () => CanonicalSemanticCompiler.AppendDependencyPath(oversizedSegment, child),
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                "Dependency path pre-allocation budget");
            AssertDependencyPathAggregateBudget();

            var graph = Project(SourceFixture.CreateAbilityExecutionDocument(1001));
            var ability = FindDefinition(graph, CanonicalDefinitionKind.Ability, 10002);
            AssertEx.True(ability.Dependencies.Count > 0, "Dependency digest fixture has dependencies");
            AssertFixedDigest(ability.Dependencies[0].Provenance.NormalizedValue, "Dependency provenance digest");
            AssertFixedDigest(CanonicalSemanticCompiler.ComputePayloadEvidence(new byte[] { 1, 2, 3 }),
                "Projection provenance digest");
        }

        /// <summary>
        /// 重用同一编译计量器，验证多条合法派生 path 不能绕过累计字符串预算。
        /// </summary>
        private static void AssertDependencyPathAggregateBudget()
        {
            var segment = new string('s', (64 * 1024) - 1);
            var child = new CanonicalSemanticNode(
                0,
                segment,
                CanonicalSemanticNodeKind.Absent,
                string.Empty,
                string.Empty,
                CanonicalCollectionSemantics.None,
                null,
                null,
                null);
            var meter = new CanonicalSemanticBudgetMeter();
            AssertEx.Rule(
                () =>
                {
                    for (var index = 0; index <= 128; index++)
                        CanonicalSemanticCompiler.AppendDependencyPath(string.Empty, child, meter);
                },
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                "Dependency path aggregate budget");
        }

        /// <summary>
        /// 断言 compiler evidence 使用 sha256: 加 64 位小写十六进制的固定长度格式。
        /// </summary>
        private static void AssertFixedDigest(string value, string context)
        {
            AssertEx.Equal(71, value.Length, context + " length");
            AssertEx.True(value.StartsWith("sha256:", StringComparison.Ordinal), context + " prefix");
            for (var index = 7; index < value.Length; index++)
                AssertEx.True((value[index] >= '0' && value[index] <= '9')
                              || (value[index] >= 'a' && value[index] <= 'f'), context + " hex");
        }

        /// <summary>
        /// 验证 oversized count 不访问 indexer、非法 UTF-16 不编码，节点总量越界立即 CFG1501。
        /// </summary>
        private static void SourceBudgetsFailBeforeCopyOrTraversal()
        {
            AssertEx.Rule(
                () => new LubanSemanticSourceDefinitionId(
                    CanonicalSemanticSchemaRegistry.GameplayTagDomain,
                    CanonicalDefinitionKind.GameplayTag,
                    new OversizedReadOnlyList<long>()),
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                "Source count before copy");
            AssertEx.Rule(
                () => new CanonicalDefinitionKey(
                    1,
                    CanonicalDefinitionKind.Ability,
                    new SafeToHugeReadOnlyList<long>(1L)),
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                "Source safe-to-huge count after bounded copy");
            AssertEx.Rule(
                () => new CanonicalDefinitionKey(
                    1,
                    CanonicalDefinitionKind.Ability,
                    new SameCountChangingReadOnlyList<long>(1L, 2L)),
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                "Source same-count item replacement");
            AssertEx.Rule(
                () => LubanSemanticSourceValue.String("\ud800"),
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                "Strict UTF-8 before allocation");
            AssertEx.Rule(ExhaustNodeBudget, CanonicalSemanticRuleCatalog.NonCanonicalIdentity, "Total node budget");
        }

        /// <summary>
        /// 累加到第一个超限 node 并确认计量器立即失败。
        /// </summary>
        private static void ExhaustNodeBudget()
        {
            var meter = new CanonicalSemanticBudgetMeter();
            for (var index = 0; index <= CanonicalSemanticResourceBudget.MaxTotalNodes; index++)
                meter.ChargeNode(0);
        }

        /// <summary>
        /// 验证 graph node provenance 与 typed role/key/read-set 的累计预算均先于结构/hash 门失败。
        /// </summary>
        private static void GraphAndTypedAggregateBudgetsCoverNestedFields()
        {
            var graph = Project(SourceFixture.CreateRichDocument(RichMutation.None, false, false));
            AssertEx.Rule(
                () => BuildGraphWithRepeatedNodeProvenance(graph),
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                "Graph node provenance aggregate budget");

            var contract = TypedContractBuilder.Create(graph);
            var oversizedRule = new string('r', CanonicalSemanticResourceBudget.MaxSingleStringBytes);
            AssertEx.Rule(
                () => TypedContractBinaryCodec.Encode(
                    CreateTypedRoleBudgetContract(contract, oversizedRule, null, 33)),
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                "Typed role string aggregate budget");
            AssertEx.Rule(
                () => TypedContractBinaryCodec.Encode(CreateTypedRelatedKeyBudgetContract(contract)),
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                "Typed provenance/key aggregate budget");
            AssertEx.Rule(
                () => TypedContractBinaryCodec.Encode(CreateTypedOrdinalBudgetContract(contract)),
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                "Typed ordinal aggregate budget");
        }

        /// <summary>
        /// 重复引用一个仅 node provenance 含大字符串的 Definition，证明深层字段进入 graph 总预算。
        /// </summary>
        private static CanonicalNormalizedSemanticGraph BuildGraphWithRepeatedNodeProvenance(
            CanonicalNormalizedSemanticGraph graph)
        {
            var sourceDefinition = graph.Definitions[0];
            var sourceField = sourceDefinition.Fields[0];
            var sourceNode = sourceField.Root;
            var largeRawValue = new string('n', CanonicalSemanticResourceBudget.MaxSingleStringBytes);
            var provenance = CloneProvenance(sourceNode.Provenance, largeRawValue, null);
            var node = new CanonicalSemanticNode(
                sourceNode.NodeOrdinal, sourceNode.NodeId, sourceNode.NodeKind, sourceNode.TypeId,
                sourceNode.TargetDomainId, sourceNode.CollectionSemantics, sourceNode.ScalarValue,
                sourceNode.Children, provenance);
            var fields = new List<CanonicalSemanticField>(sourceDefinition.Fields);
            fields[0] = new CanonicalSemanticField(
                sourceField.FieldOrdinal, sourceField.FieldId, sourceField.Classification,
                sourceField.Disposition, node, sourceField.Provenance);
            var repeated = new CanonicalSemanticDefinition(
                sourceDefinition.DefinitionOrdinal, sourceDefinition.Key, fields,
                sourceDefinition.Roles, sourceDefinition.Programs, sourceDefinition.Dependencies,
                sourceDefinition.Provenance);
            var definitions = new CanonicalSemanticDefinition[33];
            for (var index = 0; index < definitions.Length; index++)
                definitions[index] = repeated;
            return CanonicalNormalizedSemanticGraph.Create(
                graph.ToolchainIdentity, graph.GeneratorVersion, graph.SourceInputHash,
                graph.Coverage, graph.Rules, definitions);
        }

        /// <summary>
        /// 复制 provenance 并仅替换 raw value 或 related key 集合。
        /// </summary>
        private static CanonicalSemanticProvenance CloneProvenance(
            CanonicalSemanticProvenance source,
            string rawValue,
            IReadOnlyList<CanonicalDefinitionKey> related)
        {
            return new CanonicalSemanticProvenance(
                source.WorkbookId, source.TableId, source.RowStableId, source.FieldPath,
                rawValue ?? source.RawValue, source.NormalizedValue, source.RuleId,
                source.RuleVersion, source.GeneratorVersion,
                related ?? source.RelatedDefinitionIds);
        }

        /// <summary>
        /// 用重复 role 构造仅预算门可接受检查的 typed contract。
        /// </summary>
        private static TypedContract CreateTypedRoleBudgetContract(
            TypedContract contract,
            string ruleId,
            CanonicalSemanticProvenance provenance,
            int roleCount)
        {
            var definitions = new List<TypedDefinitionContract>(contract.Definitions);
            var definitionIndex = FindTypedDefinitionWithRole(definitions);
            var definition = definitions[definitionIndex];
            var source = definition.Roles[0];
            var roles = new TypedRoleContract[roleCount];
            for (var index = 0; index < roles.Length; index++)
            {
                roles[index] = new TypedRoleContract(
                    index, source.RoleKind, source.OwnerDefinition, source.OwnerFieldOrdinal,
                    ruleId, source.RuleVersion, provenance ?? source.Provenance);
            }
            definitions[definitionIndex] = CloneTypedDefinition(definition, roles: roles);
            return CloneTypedContract(contract, definitions);
        }

        /// <summary>
        /// 构造重复 related key 的 provenance，验证复合 key parts 进入 typed aggregate budget。
        /// </summary>
        private static TypedContract CreateTypedRelatedKeyBudgetContract(TypedContract contract)
        {
            var definitions = new List<TypedDefinitionContract>(contract.Definitions);
            var definitionIndex = FindTypedDefinitionWithRole(definitions);
            var sourceRole = definitions[definitionIndex].Roles[0];
            var parts = new long[CanonicalSemanticResourceBudget.MaxSingleCollectionCount];
            var key = new CanonicalDefinitionKey(5, CanonicalDefinitionKind.GameplayTag, parts);
            var provenance = CloneProvenance(
                sourceRole.Provenance,
                null,
                new[] { key });
            return CreateTypedRoleBudgetContract(
                contract, sourceRole.RuleId, provenance, 129);
        }

        /// <summary>
        /// 构造重复 4096 项 read set，验证 ordinal 列表进入 typed 总 collection budget。
        /// </summary>
        private static TypedContract CreateTypedOrdinalBudgetContract(TypedContract contract)
        {
            var definitions = new List<TypedDefinitionContract>(contract.Definitions);
            var definitionIndex = FindTypedDefinitionWithProgram(definitions, out var programIndex);
            var definition = definitions[definitionIndex];
            var sourceProgram = definition.Programs[programIndex];
            var inflatedNode = new TypedProgramNodeContract(
                0, sourceProgram.ProgramId,
                new int[CanonicalSemanticResourceBudget.MaxSingleCollectionCount],
                Array.Empty<int>(), null,
                sourceProgram.RuleId, sourceProgram.RuleVersion, sourceProgram.Provenance);
            var nodes = new TypedProgramNodeContract[129];
            for (var index = 0; index < nodes.Length; index++)
                nodes[index] = inflatedNode;
            var inflatedProgram = new TypedProgramContract(
                sourceProgram.ProgramOrdinal, sourceProgram.ProgramId, sourceProgram.ProgramKind,
                sourceProgram.Disposition, sourceProgram.Decisions, nodes, sourceProgram.Edges,
                sourceProgram.RuleId, sourceProgram.RuleVersion, sourceProgram.Provenance);
            var programs = new List<TypedProgramContract>(definition.Programs);
            programs[programIndex] = inflatedProgram;
            definitions[definitionIndex] = CloneTypedDefinition(definition, programs: programs);
            return CloneTypedContract(contract, definitions);
        }

        /// <summary>
        /// 真正制造两个 collection child 共用 ordinal=0，验证 dense ordinal 校验不会被普通乱序测试冒充。
        /// </summary>
        private static void DuplicateCollectionElementOrdinalIsRejected()
        {
            var graph = Project(SourceFixture.CreateRichDocument(RichMutation.None, false, false));
            AssertEx.Rule(
                () => BuildGraphWithDuplicateCollectionOrdinal(graph),
                CanonicalSemanticRuleCatalog.InvalidDomainOrReference,
                "Duplicate collection element ordinal");
        }

        /// <summary>
        /// 克隆合法 graph 并仅将 GrantedTags 第二个元素 ordinal 改为零。
        /// </summary>
        private static CanonicalNormalizedSemanticGraph BuildGraphWithDuplicateCollectionOrdinal(
            CanonicalNormalizedSemanticGraph graph)
        {
            var definitions = new List<CanonicalSemanticDefinition>(graph.Definitions);
            var definitionIndex = FindDefinitionIndex(graph, CanonicalDefinitionKind.GameplayEffect, 1005);
            var original = definitions[definitionIndex];
            var fields = new List<CanonicalSemanticField>(original.Fields);
            var fieldIndex = FindFieldIndex(original, "GrantedTags");
            var field = fields[fieldIndex];
            var children = new List<CanonicalSemanticNode>(field.Root.Children);
            children[1] = CloneNode(children[1], 0);
            var root = CloneNode(field.Root, field.Root.NodeOrdinal, children);
            fields[fieldIndex] = new CanonicalSemanticField(
                field.FieldOrdinal, field.FieldId, field.Classification, field.Disposition, root, field.Provenance);
            definitions[definitionIndex] = new CanonicalSemanticDefinition(
                original.DefinitionOrdinal, original.Key, fields, original.Roles, original.Programs, original.Dependencies, original.Provenance);
            return CanonicalNormalizedSemanticGraph.Create(
                graph.ToolchainIdentity, graph.GeneratorVersion, graph.SourceInputHash, graph.Coverage, graph.Rules, definitions);
        }

        /// <summary>
        /// 克隆 canonical node 并允许测试替换当前 ordinal 或 children。
        /// </summary>
        private static CanonicalSemanticNode CloneNode(
            CanonicalSemanticNode source,
            int ordinal,
            IReadOnlyList<CanonicalSemanticNode> children = null)
        {
            return new CanonicalSemanticNode(
                ordinal,
                source.NodeId,
                source.NodeKind,
                source.TypeId,
                source.TargetDomainId,
                source.CollectionSemantics,
                source.ScalarValue,
                children ?? source.Children,
                source.Provenance);
        }

        /// <summary>
        /// 验证 SourceInputHash 与结构化 toolchain identity 都进入完整 graph/Typed 可验证边界。
        /// </summary>
        private static void SourceAndToolchainIdentityAreBound()
        {
            var baseline = Project(SourceFixture.CreateIdentityDocument(IdentityMutation.None));
            var sourceHash = Project(SourceFixture.CreateIdentityDocument(IdentityMutation.SourceInputHash));
            var toolchain = Project(SourceFixture.CreateIdentityDocument(IdentityMutation.Toolchain));
            AssertEx.Equal(ContentVector(baseline), ContentVector(sourceHash), "Source hash content projection");
            AssertEx.Equal(ContentVector(baseline), ContentVector(toolchain), "Toolchain content projection");
            AssertEx.NotEqual(GraphIdentity(baseline).GraphHash, GraphIdentity(sourceHash).GraphHash, "SourceInputHash GraphHash");
            AssertEx.NotEqual(GraphIdentity(baseline).GraphHash, GraphIdentity(toolchain).GraphHash, "Toolchain GraphHash");

            var decoded = CanonicalSemanticBinaryCodec.Decode(CanonicalSemanticBinaryCodec.Encode(baseline));
            AssertEx.Equal(baseline.SourceInputHash, decoded.SourceInputHash, "SourceInputHash round-trip");
            AssertEx.Equal(baseline.ToolchainIdentity.LubanBinaryHash, decoded.ToolchainIdentity.LubanBinaryHash, "Toolchain round-trip");
            AssertEx.Equal(baseline.ToolchainIdentity.SchemaInputHash, decoded.ToolchainIdentity.SchemaInputHash, "Schema input round-trip");
        }

        /// <summary>
        /// 对 root NodeKind/TypeId/TargetDomain/CollectionSemantics 逐项篡改并重算 expected hash，均必须 CFG1002。
        /// </summary>
        private static void TypedFieldRootShapeMustMatchRegistry()
        {
            var graph = Project(SourceFixture.CreateRichDocument(RichMutation.None, false, false));
            var contract = TypedContractBuilder.Create(graph);
            foreach (TypedShapeMutation mutation in Enum.GetValues(typeof(TypedShapeMutation)))
            {
                var tampered = CloneTypedFieldShape(contract, mutation);
                AssertTamperedTypedDecode(tampered, CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Typed root " + mutation);
            }
        }

        /// <summary>
        /// 验证 RoleKind、Dependency Owner/WorkKind 枚举闭集与 fixed schema payload 都不能用新 hash 洗白。
        /// </summary>
        private static void TypedEnumsAndFixedSchemaAreClosed()
        {
            var graph = Project(SourceFixture.CreateRichDocument(RichMutation.None, false, false));
            var contract = TypedContractBuilder.Create(graph);
            AssertTamperedTypedDecode(
                CloneTypedEnum(contract, TypedEnumMutation.RoleKind),
                CanonicalSemanticRuleCatalog.InvalidDomainOrReference,
                "Typed RoleKind enum");
            AssertTamperedTypedDecode(
                CloneTypedEnum(contract, TypedEnumMutation.DependencyOwner),
                CanonicalSemanticRuleCatalog.InvalidDomainOrReference,
                "Typed dependency owner enum");
            AssertTamperedTypedDecode(
                CloneTypedEnum(contract, TypedEnumMutation.DependencyWork),
                CanonicalSemanticRuleCatalog.InvalidDomainOrReference,
                "Typed dependency work enum");
            AssertFixedSchemaTamperFails(contract);
        }

        /// <summary>
        /// 验证 TypedContract round-trip、强 graph 绑定与硬编码回归向量；该向量不是外部 trust anchor。
        /// </summary>
        private static void TypedContractRoundTripAndExpectedHashAreBound()
        {
            AssertEx.True(
                typeof(TypedContractBinaryCodec).GetMethod("DecodeAndEnsureMatches") != null,
                "Typed strong decode public entry");
            AssertEx.True(
                typeof(TypedContractBuilder).GetMethod("EnsureMatches") == null,
                "Typed raw EnsureMatches is not public");
            var graph = Project(SourceFixture.CreateRichDocument(RichMutation.None, false, false));
            var contract = TypedContractBuilder.Create(graph);
            var bytes = TypedContractBinaryCodec.Encode(contract);
            var decoded = TypedContractBinaryCodec.DecodeAndEnsureMatches(bytes, contract.TypedContractHash, graph);
            AssertEx.BytesEqual(bytes, TypedContractBinaryCodec.Encode(decoded), "TypedContract round-trip");
            EnsureTypedMatches(graph, decoded, contract.TypedContractHash);
            AssertEx.Equal(TypedContractBinaryCodec.ComputeMatrixHash(), contract.ContractMatrixHash, "ContractMatrixHash");
            AssertTypedBoundary(graph, contract);

            AssertEx.Equal(ExpectedTypedContractHash, contract.TypedContractHash, "TypedContractHash golden");
            AssertEx.Rule(
                () => EnsureTypedMatches(graph, decoded, new string('0', 64)),
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                "External expected TypedContractHash");
            var otherGraph = Project(SourceFixture.CreateIdentityDocument(IdentityMutation.SourceInputHash));
            AssertEx.Rule(
                () => TypedContractBinaryCodec.DecodeAndEnsureMatches(bytes, contract.TypedContractHash, otherGraph),
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                "Strong decode graph binding");
        }

        /// <summary>
        /// 验证 adapter 只能经 external hash 强绑定创建，能读取嵌套 typed value 且所有集合不可写。
        /// </summary>
        private static void TypedValueTreeAdapterIsBoundAndReadOnly()
        {
            var graph = Project(SourceFixture.CreateAbilityExecutionDocument(1001));
            var contract = TypedContractBuilder.Create(graph);
            AssertEx.Equal(ExpectedTypedValueTreeContractHash, contract.TypedContractHash, "Adapter external trust anchor fixture");
            var tree = TypedValueTreeAdapter.CreateAndEnsureMatches(
                graph,
                contract,
                ExpectedTypedValueTreeContractHash);
            var definition = FindTypedValueDefinition(
                tree,
                CanonicalDefinitionKind.Ability,
                10002);
            var field = FindTypedValueField(definition, "AbilityExecution");
            var ids = FindTypedValueNode(field.Root, "Param", "IDs");
            AssertEx.Equal(2, ids.Children.Count, "Typed value adapter ordered IDs");
            AssertEx.Equal(
                1001L,
                ids.Children[1].ScalarValue.DefinitionReference.StableIdParts[0],
                "Typed value adapter nested reference");
            AssertEx.Equal(contract.TypedContractHash, tree.TypedContractHash, "Typed value adapter hash");
            AssertEx.Equal(contract.GraphHash, tree.GraphHash, "Typed value adapter graph hash");
            AssertEx.Equal(
                ExpectedTypedValueTreeHash,
                ComputeTypedValueTreeHash(tree),
                "Typed value adapter independent hash");
            AssertEx.Equal(CanonicalSemanticCoverageState.Red, contract.Coverage.SameParseCoverage, "Adapter same-parse remains Red");
            AssertEx.Equal(CanonicalSemanticCoverageState.Red, contract.Coverage.Eligibility, "Adapter eligibility remains Red");
            AssertEx.True(typeof(TypedValueTree).GetConstructors().Length == 0, "Typed value tree has no public constructor");
            AssertEx.True(typeof(TypedValueNode).GetConstructors().Length == 0, "Typed value node has no public constructor");
            AssertEx.Equal(typeof(TypedScalarValue), typeof(TypedValueNode).GetProperty("ScalarValue").PropertyType,
                "Typed value node does not expose CanonicalSemanticValue");
            AssertTypedScalarBytesAreImmutable();
            AssertEx.True(typeof(TypedValueTree).GetProperty("Eligibility") == null, "Adapter exposes no eligibility claim");
            AssertEx.Throws<NotSupportedException>(
                () => ((IList<TypedDefinitionValueTree>)tree.Definitions).Clear(),
                "Typed value definitions are read-only");
            AssertEx.Throws<NotSupportedException>(
                () => ((IList<TypedValueNode>)ids.Children).Clear(),
                "Typed value children are read-only");
            AssertEx.Rule(
                () => TypedValueTreeAdapter.CreateAndEnsureMatches(
                    graph, contract, new string('0', 64)),
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                "Typed value adapter external hash");
        }

        /// <summary>
        /// 验证 typed scalar 在构造和每次公开读取时都复制 byte payload。
        /// </summary>
        private static void AssertTypedScalarBytesAreImmutable()
        {
            var source = new byte[] { 1, 2, 3 };
            var scalar = new TypedScalarValue(
                CanonicalSemanticValueKind.Bytes,
                0,
                DateTimeKind.Unspecified,
                null,
                0,
                null,
                source);
            source[0] = 9;
            var firstRead = scalar.ByteValue;
            firstRead[1] = 9;
            AssertEx.BytesEqual(new byte[] { 1, 2, 3 }, scalar.ByteValue, "Typed scalar byte clones");
            AssertEx.True(
                typeof(TypedScalarValue).GetProperty(
                    "ByteValueUnsafe",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null,
                "Typed scalar exposes no unsafe byte accessor");
        }

        /// <summary>
        /// 用测试侧独立 BinaryWriter 口径计算 typed value tree hash，不复用 production codec/hash。
        /// </summary>
        private static string ComputeTypedValueTreeHash(TypedValueTree tree)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
            {
                writer.Write(tree.Definitions.Count);
                for (var index = 0; index < tree.Definitions.Count; index++)
                    WriteTypedDefinition(writer, tree.Definitions[index]);
                writer.Flush();
                using (var sha256 = SHA256.Create())
                    return ToLowerHex(sha256.ComputeHash(stream.ToArray()));
            }
        }

        /// <summary>
        /// 写入测试侧 typed Definition identity、字段 metadata 与完整 value roots。
        /// </summary>
        private static void WriteTypedDefinition(BinaryWriter writer, TypedDefinitionValueTree definition)
        {
            writer.Write(definition.DefinitionOrdinal);
            WriteDefinitionKey(writer, definition.DefinitionKey);
            writer.Write(definition.Fields.Count);
            for (var index = 0; index < definition.Fields.Count; index++)
            {
                var field = definition.Fields[index];
                writer.Write(field.FieldOrdinal);
                writer.Write(field.FieldId);
                writer.Write((byte)field.Classification);
                writer.Write((byte)field.Disposition);
                WriteTypedNode(writer, field.Root);
            }
        }

        /// <summary>
        /// 递归写入独立 typed node shape、scalar union 与有序 children。
        /// </summary>
        private static void WriteTypedNode(BinaryWriter writer, TypedValueNode node)
        {
            writer.Write(node.NodeOrdinal);
            writer.Write(node.NodeId);
            writer.Write((byte)node.NodeKind);
            writer.Write(node.TypeId);
            writer.Write(node.TargetDomainId);
            writer.Write((byte)node.CollectionSemantics);
            WriteTypedScalar(writer, node.ScalarValue);
            writer.Write(node.Children.Count);
            for (var index = 0; index < node.Children.Count; index++)
                WriteTypedNode(writer, node.Children[index]);
        }

        /// <summary>
        /// 写入 closed typed scalar union，并显式区分 null、空文本和空 bytes。
        /// </summary>
        private static void WriteTypedScalar(BinaryWriter writer, TypedScalarValue scalar)
        {
            writer.Write(scalar != null);
            if (scalar == null)
                return;
            writer.Write((byte)scalar.Kind);
            writer.Write((byte)scalar.EnumUnderlyingType);
            writer.Write((byte)scalar.DateTimeKind);
            writer.Write(scalar.RawBits);
            WriteNullableString(writer, scalar.Text);
            writer.Write(scalar.DefinitionReference != null);
            if (scalar.DefinitionReference != null)
                WriteDefinitionKey(writer, scalar.DefinitionReference);
            var bytes = scalar.ByteValue;
            writer.Write(bytes != null);
            if (bytes != null)
            {
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
        }

        /// <summary>
        /// 写入复合 canonical key，避免测试 hash 依赖 ToString。
        /// </summary>
        private static void WriteDefinitionKey(BinaryWriter writer, CanonicalDefinitionKey key)
        {
            writer.Write(key.DomainOrdinal);
            writer.Write((byte)key.DefinitionKind);
            writer.Write(key.StableIdParts.Count);
            for (var index = 0; index < key.StableIdParts.Count; index++)
                writer.Write(key.StableIdParts[index]);
        }

        /// <summary>
        /// 以显式 presence 位写入可空文本。
        /// </summary>
        private static void WriteNullableString(BinaryWriter writer, string value)
        {
            writer.Write(value != null);
            if (value != null)
                writer.Write(value);
        }

        /// <summary>
        /// 将固定 digest 转为测试侧小写十六进制文本。
        /// </summary>
        private static string ToLowerHex(byte[] bytes)
        {
            const string digits = "0123456789abcdef";
            var result = new char[bytes.Length * 2];
            for (var index = 0; index < bytes.Length; index++)
            {
                result[index * 2] = digits[bytes[index] >> 4];
                result[index * 2 + 1] = digits[bytes[index] & 15];
            }
            return new string(result);
        }

        /// <summary>
        /// 替换 Ability.ID 的单个 root shape 维度。
        /// </summary>
        private static TypedContract CloneTypedFieldShape(
            TypedContract contract,
            TypedShapeMutation mutation)
        {
            var definitions = new List<TypedDefinitionContract>(contract.Definitions);
            var definitionIndex = FindTypedDefinitionIndex(definitions, CanonicalDefinitionKind.Ability, false);
            var definition = definitions[definitionIndex];
            var fields = new List<TypedFieldContract>(definition.Fields);
            var source = fields[0];
            fields[0] = new TypedFieldContract(
                source.FieldOrdinal,
                source.FieldId,
                mutation == TypedShapeMutation.NodeKind ? CanonicalSemanticNodeKind.Record : source.NodeKind,
                mutation == TypedShapeMutation.TypeId ? source.TypeId + ".tampered" : source.TypeId,
                mutation == TypedShapeMutation.TargetDomain
                    ? CanonicalSemanticSchemaRegistry.GameplayEffectDomain
                    : source.TargetDomainId,
                mutation == TypedShapeMutation.Collection
                    ? CanonicalCollectionSemantics.Ordered
                    : source.CollectionSemantics,
                source.Classification,
                source.Disposition,
                source.RuleId,
                source.RuleVersion,
                source.Provenance);
            definitions[definitionIndex] = CloneTypedDefinition(definition, fields: fields);
            return CloneTypedContract(contract, definitions);
        }

        /// <summary>
        /// 替换一个 Typed enum 为 255，保留其余所有 bytes 不变。
        /// </summary>
        private static TypedContract CloneTypedEnum(TypedContract contract, TypedEnumMutation mutation)
        {
            var definitions = new List<TypedDefinitionContract>(contract.Definitions);
            var needDependency = mutation != TypedEnumMutation.RoleKind;
            var definitionIndex = FindTypedDefinitionIndex(definitions, CanonicalDefinitionKind.Unknown, needDependency);
            var definition = definitions[definitionIndex];
            if (mutation == TypedEnumMutation.RoleKind)
            {
                var roles = new List<TypedRoleContract>(definition.Roles);
                var role = roles[0];
                roles[0] = new TypedRoleContract(
                    role.RoleOrdinal, (CanonicalDefinitionRoleKind)255, role.OwnerDefinition,
                    role.OwnerFieldOrdinal, role.RuleId, role.RuleVersion, role.Provenance);
                definitions[definitionIndex] = CloneTypedDefinition(definition, roles: roles);
            }
            else
            {
                definitions[definitionIndex] = CloneTypedDependencyEnum(definition, mutation);
            }
            return CloneTypedContract(contract, definitions);
        }

        /// <summary>
        /// 替换首条 dependency 的 OwnerKind 或 WorkKind 枚举。
        /// </summary>
        private static TypedDefinitionContract CloneTypedDependencyEnum(
            TypedDefinitionContract definition,
            TypedEnumMutation mutation)
        {
            var dependencies = new List<TypedDependencyContract>(definition.Dependencies);
            var source = dependencies[0];
            dependencies[0] = new TypedDependencyContract(
                source.DependencyOrdinal, source.DependencyId, source.SourceFieldOrdinal,
                source.DependencyKind, source.TargetDefinition, source.TargetDomainId,
                mutation == TypedEnumMutation.DependencyOwner ? (CanonicalDependencyOwnerKind)255 : source.OwnerKind,
                source.Sign,
                mutation == TypedEnumMutation.DependencyWork ? (CanonicalDependencyWorkKind)255 : source.WorkKind,
                source.CleanupPolicy, source.MaxExpansion, source.RuleId, source.RuleVersion, source.Provenance);
            return CloneTypedDefinition(definition, dependencies: dependencies);
        }

        /// <summary>
        /// 修改 schema payload 并同步重算自带 hash，验证 fixed registry 逐 byte 比较仍拒绝。
        /// </summary>
        private static void AssertFixedSchemaTamperFails(TypedContract contract)
        {
            var payload = contract.SchemaProjectionPayload;
            payload[payload.Length - 1] ^= 1;
            var schemaHash = CanonicalSemanticHash.Compute(CanonicalSemanticVersions.SchemaHashDomain, payload);
            var tampered = CloneTypedContract(contract, contract.Definitions, schemaHash, payload);
            AssertTamperedTypedDecode(
                tampered,
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                "Fixed schema projection payload");
        }

        /// <summary>
        /// 对篡改后 payload 重算 expected hash，确保断言命中结构门而非旧 hash 门。
        /// </summary>
        private static void AssertTamperedTypedDecode(
            TypedContract contract,
            string expectedRule,
            string context)
        {
            var bytes = TypedContractBinaryCodec.EncodePayload(contract);
            var expectedHash = TypedContractBinaryCodec.ComputeTypedContractHash(bytes);
            AssertEx.Rule(
                () => TypedContractBinaryCodec.Decode(bytes, expectedHash),
                expectedRule,
                context);
        }

        /// <summary>
        /// 复制 Typed definition 并仅替换指定子合同集合。
        /// </summary>
        private static TypedDefinitionContract CloneTypedDefinition(
            TypedDefinitionContract source,
            IReadOnlyList<TypedFieldContract> fields = null,
            IReadOnlyList<TypedRoleContract> roles = null,
            IReadOnlyList<TypedProgramContract> programs = null,
            IReadOnlyList<TypedDependencyContract> dependencies = null)
        {
            return new TypedDefinitionContract(
                source.DefinitionOrdinal,
                source.DefinitionKey,
                fields ?? source.Fields,
                roles ?? source.Roles,
                programs ?? source.Programs,
                dependencies ?? source.Dependencies,
                source.Provenance);
        }

        /// <summary>
        /// 复制 TypedContract 并可选替换 schema hash/payload。
        /// </summary>
        private static TypedContract CloneTypedContract(
            TypedContract source,
            IReadOnlyList<TypedDefinitionContract> definitions,
            string schemaHash = null,
            byte[] schemaPayload = null)
        {
            return new TypedContract(
                source.SourceContractVersion, source.ToolchainIdentity, source.GeneratorVersion,
                source.SourceInputHash, source.Coverage, source.GraphHash, source.RegistryHash,
                schemaHash ?? source.SchemaHash, source.ContentHash, source.ContractMatrixHash,
                source.TypedContractHash, source.ContractMatrixPayload,
                schemaPayload ?? source.SchemaProjectionPayload,
                source.ContentProjectionPayload, definitions);
        }

        /// <summary>
        /// 查找指定 kind 或首个含 dependency 的 Typed definition。
        /// </summary>
        private static int FindTypedDefinitionIndex(
            IReadOnlyList<TypedDefinitionContract> definitions,
            CanonicalDefinitionKind kind,
            bool requireDependency)
        {
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if ((kind == CanonicalDefinitionKind.Unknown || definition.DefinitionKey.DefinitionKind == kind)
                    && (!requireDependency || definition.Dependencies.Count > 0))
                    return index;
            }
            throw new InvalidOperationException("Typed definition fixture not found.");
        }

        /// <summary>
        /// 查找首个含 compiler-derived role 的 typed Definition。
        /// </summary>
        private static int FindTypedDefinitionWithRole(
            IReadOnlyList<TypedDefinitionContract> definitions)
        {
            for (var index = 0; index < definitions.Count; index++)
                if (definitions[index].Roles.Count > 0)
                    return index;
            throw new InvalidOperationException("Typed role fixture not found.");
        }

        /// <summary>
        /// 查找首个含 program 的 typed Definition/program 索引。
        /// </summary>
        private static int FindTypedDefinitionWithProgram(
            IReadOnlyList<TypedDefinitionContract> definitions,
            out int programIndex)
        {
            for (var definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                var programs = definitions[definitionIndex].Programs;
                if (programs.Count > 0)
                {
                    programIndex = 0;
                    return definitionIndex;
                }
            }
            throw new InvalidOperationException("Typed program fixture not found.");
        }

        /// <summary>
        /// 集中适配 Typed v2 expected-hash API，避免 fixture 构造与并行 DTO 收口互相耦合。
        /// </summary>
        private static void EnsureTypedMatches(
            CanonicalNormalizedSemanticGraph graph,
            TypedContract contract,
            string expectedTypedContractHash)
        {
            TypedContractBuilder.EnsureMatches(graph, contract, expectedTypedContractHash);
        }

        /// <summary>
        /// 验证 TypedContract 显式携带 source hash、结构化 toolchain、graph/content/matrix/typed hashes。
        /// </summary>
        private static void AssertTypedBoundary(CanonicalNormalizedSemanticGraph graph, TypedContract contract)
        {
            AssertEx.Equal(graph.SourceInputHash, contract.SourceInputHash, "Typed SourceInputHash");
            AssertEx.Equal(graph.ToolchainIdentity.LubanProductVersion, contract.ToolchainIdentity.LubanProductVersion, "Typed product version");
            AssertEx.Equal(graph.ToolchainIdentity.LubanBinaryHash, contract.ToolchainIdentity.LubanBinaryHash, "Typed binary hash");
            AssertEx.Equal(graph.ToolchainIdentity.SchemaInputHash, contract.ToolchainIdentity.SchemaInputHash, "Typed schema input hash");
            AssertEx.Equal(graph.ToolchainIdentity.GeneratorIdentity, contract.ToolchainIdentity.GeneratorIdentity, "Typed generator identity");
            AssertEx.True(contract.GraphHash.Length == 64, "Typed GraphHash");
            AssertEx.True(contract.ContentHash.Length == 64, "Typed ContentHash");
            AssertEx.True(contract.ContractMatrixHash.Length == 64, "Typed ContractMatrixHash");
            AssertEx.True(contract.TypedContractHash.Length == 64, "Typed TypedContractHash");
        }

        /// <summary>
        /// 在不同文化区、输入 set 顺序与工作目录的独立进程中比较 graph/Typed bytes。
        /// </summary>
        private static void DifferentProcessesCulturesAndWorkdirsAreDeterministic()
        {
            var firstDirectory = CreateTemporaryDirectory("gas-sem-a");
            var secondDirectory = CreateTemporaryDirectory("gas-sem-b");
            try
            {
                var first = RunChild("tr-TR", false, firstDirectory);
                var second = RunChild("zh-CN", true, secondDirectory);
                AssertEx.Equal(first, second, "Independent process/culture/workdir vector");
            }
            finally
            {
                Directory.Delete(firstDirectory, true);
                Directory.Delete(secondDirectory, true);
            }
        }

        /// <summary>
        /// 创建当前测试独占的精确临时目录。
        /// </summary>
        private static string CreateTemporaryDirectory(string prefix)
        {
            var path = Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// 启动当前 exe 子进程并返回唯一标准输出向量。
        /// </summary>
        private static string RunChild(string culture, bool reverseSets, string workingDirectory)
        {
            var executable = Assembly.GetExecutingAssembly().Location;
            var arguments = "--emit " + culture + " " + (reverseSets ? "1" : "0");
            var start = new ProcessStartInfo(executable, arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (var process = Process.Start(start))
            {
                var output = process.StandardOutput.ReadToEnd().Trim();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Child failed: " + error);
                return output;
            }
        }

        /// <summary>
        /// 设置指定文化区并输出 graph bytes 与 TypedContract bytes 的 Base64 组合向量。
        /// </summary>
        private static void EmitDeterminismVector(IReadOnlyList<string> args)
        {
            if (args.Count != 3)
                throw new ArgumentException("--emit requires culture and reverse-set flag.");
            var culture = CultureInfo.GetCultureInfo(args[1]);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            var reverseSets = string.Equals(args[2], "1", StringComparison.Ordinal);
            var graph = Project(SourceFixture.CreateRichDocument(RichMutation.None, reverseSets, false));
            var contract = TypedContractBuilder.Create(graph);
            Console.WriteLine(
                Convert.ToBase64String(CanonicalSemanticBinaryCodec.Encode(graph)) + "." +
                Convert.ToBase64String(TypedContractBinaryCodec.Encode(contract)));
        }

        /// <summary>
        /// 将 source document 投影为 sealed canonical graph。
        /// </summary>
        private static CanonicalNormalizedSemanticGraph Project(LubanSemanticSourceDocument document)
        {
            return CanonicalSemanticProjector.Project(document);
        }

        /// <summary>
        /// 计算不含 provenance/toolchain 的 semantic content 比较向量。
        /// </summary>
        private static string ContentVector(CanonicalNormalizedSemanticGraph graph)
        {
            return Convert.ToBase64String(CanonicalSemanticProjectionCodec.EncodeContent(graph));
        }

        /// <summary>
        /// 使用固定 ABI/matrix 域计算完整 graph identity。
        /// </summary>
        private static CanonicalSemanticIdentity GraphIdentity(CanonicalNormalizedSemanticGraph graph)
        {
            return CanonicalSemanticIdentity.Create(
                graph,
                CanonicalSemanticVersions.TypedContractAbi,
                TypedContractBinaryCodec.ComputeMatrixHash());
        }

        /// <summary>
        /// 按 kind 与首个 stable ID 查找 canonical definition。
        /// </summary>
        private static CanonicalSemanticDefinition FindDefinition(
            CanonicalNormalizedSemanticGraph graph,
            CanonicalDefinitionKind kind,
            long stableId)
        {
            return graph.Definitions[FindDefinitionIndex(graph, kind, stableId)];
        }

        /// <summary>
        /// 按 kind 与首个 stable ID 查找 definition 索引。
        /// </summary>
        private static int FindDefinitionIndex(
            CanonicalNormalizedSemanticGraph graph,
            CanonicalDefinitionKind kind,
            long stableId)
        {
            for (var index = 0; index < graph.Definitions.Count; index++)
            {
                var definition = graph.Definitions[index];
                if (definition.Key.DefinitionKind == kind && definition.Key.StableIdParts[0] == stableId)
                    return index;
            }
            throw new InvalidOperationException("Definition not found: " + kind + "/" + stableId);
        }

        /// <summary>
        /// 按 FieldId 查找字段索引。
        /// </summary>
        private static int FindFieldIndex(CanonicalSemanticDefinition definition, string fieldId)
        {
            for (var index = 0; index < definition.Fields.Count; index++)
                if (string.Equals(definition.Fields[index].FieldId, fieldId, StringComparison.Ordinal))
                    return index;
            throw new InvalidOperationException("Field not found: " + fieldId);
        }

        /// <summary>
        /// 从字段根开始按 NodeId 路径查找 canonical node。
        /// </summary>
        private static CanonicalSemanticNode FindNode(
            CanonicalSemanticDefinition definition,
            string fieldId,
            params string[] memberIds)
        {
            var node = definition.Fields[FindFieldIndex(definition, fieldId)].Root;
            for (var pathIndex = 0; pathIndex < memberIds.Length; pathIndex++)
            {
                CanonicalSemanticNode found = null;
                for (var childIndex = 0; childIndex < node.Children.Count; childIndex++)
                    if (string.Equals(node.Children[childIndex].NodeId, memberIds[pathIndex], StringComparison.Ordinal))
                        found = node.Children[childIndex];
                node = found ?? throw new InvalidOperationException("Node not found: " + memberIds[pathIndex]);
            }
            return node;
        }

        /// <summary>
        /// 按 kind 与首个 stable ID 查找 adapter Definition value tree。
        /// </summary>
        private static TypedDefinitionValueTree FindTypedValueDefinition(
            TypedValueTree tree,
            CanonicalDefinitionKind kind,
            long stableId)
        {
            for (var index = 0; index < tree.Definitions.Count; index++)
            {
                var definition = tree.Definitions[index];
                if (definition.DefinitionKey.DefinitionKind == kind
                    && definition.DefinitionKey.StableIdParts[0] == stableId)
                    return definition;
            }
            throw new InvalidOperationException("Typed value Definition not found: " + kind + "/" + stableId);
        }

        /// <summary>
        /// 按 FieldId 查找 adapter field value tree。
        /// </summary>
        private static TypedFieldValueTree FindTypedValueField(
            TypedDefinitionValueTree definition,
            string fieldId)
        {
            for (var index = 0; index < definition.Fields.Count; index++)
                if (string.Equals(definition.Fields[index].FieldId, fieldId, StringComparison.Ordinal))
                    return definition.Fields[index];
            throw new InvalidOperationException("Typed value field not found: " + fieldId);
        }

        /// <summary>
        /// 从 adapter root 按 NodeId 路径查找只读 typed value node。
        /// </summary>
        private static TypedValueNode FindTypedValueNode(
            TypedValueNode root,
            params string[] memberIds)
        {
            var node = root;
            for (var pathIndex = 0; pathIndex < memberIds.Length; pathIndex++)
            {
                TypedValueNode found = null;
                for (var childIndex = 0; childIndex < node.Children.Count; childIndex++)
                    if (string.Equals(node.Children[childIndex].NodeId, memberIds[pathIndex], StringComparison.Ordinal))
                        found = node.Children[childIndex];
                node = found ?? throw new InvalidOperationException("Typed value node not found: " + memberIds[pathIndex]);
            }
            return node;
        }
    }

    /// <summary>
    /// 标记 rich GameplayEffect fixture 中一次且仅一次的 semantic value 变化。
    /// </summary>
    internal enum RichMutation
    {
        None,
        TargetPolicy,
        Evaluator,
        CueOnApply,
        CueOnTick,
        CueOnAdd,
        CueOnRemove,
        CueOnActivate,
        CueOnDeactivate,
        GrantedAbility,
        GrantedTags,
        Duration,
        PeriodSecondEffect,
        ModifierMagnitude,
        ApplicationRequirement,
        StackingOverflow,
    }

    /// <summary>
    /// 标记 source/toolchain identity fixture 的单一变更域。
    /// </summary>
    internal enum IdentityMutation
    {
        None,
        SourceInputHash,
        Toolchain,
    }

    /// <summary>
    /// 标记 Typed field root 的单一篡改维度。
    /// </summary>
    internal enum TypedShapeMutation
    {
        NodeKind,
        TypeId,
        TargetDomain,
        Collection,
    }

    /// <summary>
    /// 标记 Typed 枚举闭集测试的单一篡改位点。
    /// </summary>
    internal enum TypedEnumMutation
    {
        RoleKind,
        DependencyOwner,
        DependencyWork,
    }

    /// <summary>
    /// 构造与仓库真实 Luban schema 同形的内存 source DTO；测试运行时不得二次读取 JSON 或 generated C#。
    /// </summary>
    internal static class SourceFixture
    {
        internal const string GeneratorVersion = "N2-G0-A-SemanticSafety-v2";
        private const string SourceInputHash = "14ef98716e107c9f17d5c81265f1c8ffa73f913822bbda63e3647385e0fe9aff";
        private const string AlternateSourceInputHash = "24ef98716e107c9f17d5c81265f1c8ffa73f913822bbda63e3647385e0fe9aff";
        private const string LubanBinaryHash = "83f9f2ade60f4e225b8172d518e5692d33ad06db18399dcf483944a2ed633a5d";
        private const string SchemaInputHash = "9847f9dab4e54209e4f3dbb44f708ea5040550b289411a171652c78fa5646c9e";

        /// <summary>
        /// 构造真实 GE3001：GrantedTags=[6001]、Duration=60、CueOnTick=[5000]；Ability5000 同时声明 Cd=120/CdEffect=3001。
        /// </summary>
        public static LubanSemanticSourceDocument CreateRealGe3001Document(bool includeCueOnTick)
        {
            var ability = Definition(CanonicalSemanticSchemaRegistry.AbilityDomain, new[] { 5000L },
                Override("CdEffect", ReferenceField(CanonicalSemanticSchemaRegistry.AbilityDomain, "CdEffect", 5000, 3001)),
                Override("Cd", IntField(CanonicalSemanticSchemaRegistry.AbilityDomain, "Cd", 5000, 120)));
            var effectOverrides = new List<FieldOverride>
            {
                Override("GrantedTags", ReferenceFieldCollection(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, "GrantedTags", 3001, new[] { 6001L })),
                Override("Duration", Duration(3001, 60)),
            };
            if (includeCueOnTick)
                effectOverrides.Add(Override("CueOnTick", ReferenceFieldCollection(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, "CueOnTick", 3001, new[] { 5000L })));
            var effect = Definition(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, new[] { 3001L }, effectOverrides.ToArray());
            var tag = Definition(CanonicalSemanticSchemaRegistry.GameplayTagDomain, new[] { 6001L });
            return Document(new[] { tag, effect, ability }, IdentityMutation.None);
        }

        /// <summary>
        /// 构造 Duration absent 或 present-zero 的同 key GameplayEffect source。
        /// </summary>
        public static LubanSemanticSourceDocument CreateOptionalDurationDocument(bool presentZero)
        {
            var overrides = presentZero
                ? new[] { Override("Duration", Duration(4000, 0)) }
                : Array.Empty<FieldOverride>();
            return Document(
                new[] { Definition(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, new[] { 4000L }, overrides) },
                IdentityMutation.None);
        }

        /// <summary>
        /// 构造真实 Ability10002 的 AbilityExecution.IDs=[1002, second] 并保留全部候选 GE 定义。
        /// </summary>
        public static LubanSemanticSourceDocument CreateAbilityExecutionDocument(long secondId)
        {
            var ability = Definition(CanonicalSemanticSchemaRegistry.AbilityDomain, new[] { 10002L },
                Override("AbilityExecution", AbilityExecution(10002, new[] { 1002L, secondId })));
            return Document(new[]
            {
                Definition(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, new[] { 1001L }),
                Definition(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, new[] { 1002L }),
                Definition(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, new[] { 1003L }),
                ability,
            }, IdentityMutation.None);
        }

        /// <summary>
        /// 构造覆盖 TargetPolicy/Evaluator/六 Cue/Granted/temporal/modifier/requirements 的完整 GE source。
        /// </summary>
        public static LubanSemanticSourceDocument CreateRichDocument(
            RichMutation mutation,
            bool reverseSets,
            bool duplicateGrantedTags)
        {
            var definitions = CreateRichReferencedDefinitions();
            definitions.Add(CreateRichEffect(mutation, reverseSets, duplicateGrantedTags));
            return Document(definitions, IdentityMutation.None);
        }

        /// <summary>
        /// 创建 rich GE 的所有非循环引用目标定义。
        /// </summary>
        private static List<LubanSemanticSourceDefinition> CreateRichReferencedDefinitions()
        {
            return new List<LubanSemanticSourceDefinition>
            {
                Definition(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, new[] { 1001L }),
                Definition(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, new[] { 1002L }),
                Definition(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, new[] { 1003L }),
                Definition(CanonicalSemanticSchemaRegistry.AbilityDomain, new[] { 5000L }),
                Definition(CanonicalSemanticSchemaRegistry.GameplayTagDomain, new[] { 2006L }),
                Definition(CanonicalSemanticSchemaRegistry.GameplayTagDomain, new[] { 6001L }),
                Definition(CanonicalSemanticSchemaRegistry.GameplayCueDomain, new[] { 1001L }),
                Definition(CanonicalSemanticSchemaRegistry.GameplayCueDomain, new[] { 1002L }),
                Definition(CanonicalSemanticSchemaRegistry.AttributeSetDomain, new[] { 1L }),
                Definition(CanonicalSemanticSchemaRegistry.AttributeDomain, new[] { 1L, 1L }),
            };
        }

        /// <summary>
        /// 创建 rich GE1005 并按 mutation 仅改变一个真实字段。
        /// </summary>
        private static LubanSemanticSourceDefinition CreateRichEffect(
            RichMutation mutation,
            bool reverseSets,
            bool duplicateGrantedTags)
        {
            var tags = duplicateGrantedTags ? new[] { 2006L, 2006L } : new[] { 2006L, 6001L };
            if (reverseSets)
                Array.Reverse(tags);
            if (mutation == RichMutation.GrantedTags)
                tags = new[] { 2006L };
            return Definition(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, new[] { 1005L },
                Override("GrantedTags", ReferenceFieldCollection(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, "GrantedTags", 1005, tags)),
                Override("ApplicationRequiredTags", TagRequirement(1005, mutation == RichMutation.ApplicationRequirement)),
                Override("Duration", Duration(1005, mutation == RichMutation.Duration ? 601 : 600)),
                Override("Period", Period(1005, mutation == RichMutation.PeriodSecondEffect ? 1003 : 1002)),
                Override("Modifiers", Modifiers(1005, mutation == RichMutation.ModifierMagnitude)),
                Override("CueOnApply", Cue(1005, "CueOnApply", mutation == RichMutation.CueOnApply)),
                Override("CueOnTick", Cue(1005, "CueOnTick", mutation == RichMutation.CueOnTick)),
                Override("CueOnAdd", Cue(1005, "CueOnAdd", mutation == RichMutation.CueOnAdd)),
                Override("CueOnRemove", Cue(1005, "CueOnRemove", mutation == RichMutation.CueOnRemove)),
                Override("CueOnActivate", Cue(1005, "CueOnActivate", mutation == RichMutation.CueOnActivate)),
                Override("CueOnDeactivate", Cue(1005, "CueOnDeactivate", mutation == RichMutation.CueOnDeactivate)),
                Override("GrantedAbility", GrantedAbilities(1005, mutation == RichMutation.GrantedAbility)),
                Override("Stacking", Stacking(1005, mutation == RichMutation.StackingOverflow)),
                Override("RuntimeV1TargetPolicy", TargetPolicy(1005, mutation == RichMutation.TargetPolicy)),
                Override("RuntimeV1Evaluator", Evaluator(1005, mutation == RichMutation.Evaluator)));
        }

        /// <summary>
        /// 构造 Ability→GE 与 GE→GrantedAbility 的跨 definition cycle。
        /// </summary>
        public static LubanSemanticSourceDocument CreateCycleDocument()
        {
            var ability = Definition(CanonicalSemanticSchemaRegistry.AbilityDomain, new[] { 10002L },
                Override("AbilityExecution", AbilityExecution(10002, new[] { 1001L })));
            var effect = Definition(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, new[] { 1001L },
                Override("GrantedAbility", GrantedAbilities(1001, false, 10002)));
            return Document(new[] { effect, ability }, IdentityMutation.None);
        }

        /// <summary>
        /// 构造 GameplayTag 7001/7002 的 neutral 双向引用，该环不是 Spec25 负向 SCC。
        /// </summary>
        public static LubanSemanticSourceDocument CreateNeutralTagCycleDocument()
        {
            var domain = CanonicalSemanticSchemaRegistry.GameplayTagDomain;
            var first = Definition(domain, new[] { 7001L },
                Override("ParentCodes", ReferenceFieldCollection(domain, "ParentCodes", 7001, new[] { 7002L })));
            var second = Definition(domain, new[] { 7002L },
                Override("ChildCodes", ReferenceFieldCollection(domain, "ChildCodes", 7002, new[] { 7001L })));
            return Document(new[] { first, second }, IdentityMutation.None);
        }

        /// <summary>
        /// 构造仅含 Cooldown GrantedTags 与必需 Duration 的 role fixture。
        /// </summary>
        public static LubanSemanticSourceDocument CreateCooldownGrantedTagsDocument()
        {
            var abilityDomain = CanonicalSemanticSchemaRegistry.AbilityDomain;
            var effectDomain = CanonicalSemanticSchemaRegistry.GameplayEffectDomain;
            var ability = Definition(abilityDomain, new[] { 7101L },
                Override("CdEffect", ReferenceField(abilityDomain, "CdEffect", 7101, 7102)));
            var effect = Definition(effectDomain, new[] { 7102L },
                Override("GrantedTags", ReferenceFieldCollection(effectDomain, "GrantedTags", 7102, new[] { 7103L })),
                Override("Duration", Duration(7102, 60)));
            var tag = Definition(CanonicalSemanticSchemaRegistry.GameplayTagDomain, new[] { 7103L });
            return Document(new[] { ability, effect, tag }, IdentityMutation.None);
        }

        /// <summary>
        /// 构造仅含 Duration 的 Cooldown role，验证未冻结 TimeUnit/reset 不被粗粒度接受。
        /// </summary>
        public static LubanSemanticSourceDocument CreateCooldownDurationDocument()
        {
            var abilityDomain = CanonicalSemanticSchemaRegistry.AbilityDomain;
            var effectDomain = CanonicalSemanticSchemaRegistry.GameplayEffectDomain;
            var ability = Definition(abilityDomain, new[] { 7201L },
                Override("CdEffect", ReferenceField(abilityDomain, "CdEffect", 7201, 7202)));
            var effect = Definition(effectDomain, new[] { 7202L },
                Override("Duration", Duration(7202, 60)));
            return Document(new[] { ability, effect }, IdentityMutation.None);
        }

        /// <summary>
        /// 构造带 raw Modifier operation 的 Cost role，在 RuntimeV1 op/ValueView 归一化前必须 CFG1101。
        /// </summary>
        public static LubanSemanticSourceDocument CreateCostModifierDocument()
        {
            var abilityDomain = CanonicalSemanticSchemaRegistry.AbilityDomain;
            var effectDomain = CanonicalSemanticSchemaRegistry.GameplayEffectDomain;
            var ability = Definition(abilityDomain, new[] { 7301L },
                Override("Cost", ReferenceField(abilityDomain, "Cost", 7301, 7302)));
            var effect = Definition(effectDomain, new[] { 7302L },
                Override("Modifiers", Modifiers(7302, false)));
            var attributeSet = Definition(CanonicalSemanticSchemaRegistry.AttributeSetDomain, new[] { 1L });
            var attribute = Definition(CanonicalSemanticSchemaRegistry.AttributeDomain, new[] { 1L, 1L });
            return Document(new[] { ability, effect, attributeSet, attribute }, IdentityMutation.None);
        }

        /// <summary>
        /// 构造 semantic 内容相同但 source hash 或 toolchain identity 单独变化的文档。
        /// </summary>
        public static LubanSemanticSourceDocument CreateIdentityDocument(IdentityMutation mutation)
        {
            return Document(
                new[] { Definition(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, new[] { 4000L }) },
                mutation);
        }

        /// <summary>
        /// 冻结 source document 根身份并保持 definitions 的 same-parse 输入顺序。
        /// </summary>
        private static LubanSemanticSourceDocument Document(
            IReadOnlyList<LubanSemanticSourceDefinition> definitions,
            IdentityMutation mutation)
        {
            var binaryHash = mutation == IdentityMutation.Toolchain
                ? "93f9f2ade60f4e225b8172d518e5692d33ad06db18399dcf483944a2ed633a5d"
                : LubanBinaryHash;
            var toolchain = new CanonicalSemanticToolchainIdentity(
                "4.2.1+c67b69d5c6aaabe7acce0b0126c1157b72554bb5",
                binaryHash,
                SchemaInputHash,
                GeneratorVersion);
            var sourceHash = mutation == IdentityMutation.SourceInputHash ? AlternateSourceInputHash : SourceInputHash;
            return new LubanSemanticSourceDocument(toolchain, GeneratorVersion, sourceHash, definitions);
        }

        /// <summary>
        /// 按 registry 字段闭集创建一行并用 overrides 替换指定完整根节点。
        /// </summary>
        private static LubanSemanticSourceDefinition Definition(
            string domainId,
            long[] stableIds,
            params FieldOverride[] overrides)
        {
            CanonicalSemanticSchemaRegistry.TryResolve(domainId, out var domain);
            var sourceId = new LubanSemanticSourceDefinitionId(domainId, domain.DefinitionKind, stableIds);
            var fields = new LubanSemanticSourceField[domain.Fields.Count];
            var row = RowId(stableIds);
            for (var index = 0; index < fields.Length; index++)
            {
                var field = domain.Fields[index];
                var root = FindOverride(overrides, field.FieldId);
                if (root == null)
                {
                    var keyValue = index < stableIds.Length ? (long?)stableIds[index] : null;
                    root = DefaultNode(field.Root, domainId, row, field.FieldId, keyValue);
                }
                fields[index] = new LubanSemanticSourceField(field.FieldId, root);
            }
            return new LubanSemanticSourceDefinition(
                sourceId,
                Provenance(domainId, row, "$row", CanonicalSemanticRuleCatalog.InvalidDomainOrReference),
                fields);
        }

        /// <summary>
        /// 查找指定字段 override。
        /// </summary>
        private static LubanSemanticSourceNode FindOverride(IReadOnlyList<FieldOverride> overrides, string fieldId)
        {
            for (var index = 0; index < overrides.Count; index++)
                if (string.Equals(overrides[index].FieldId, fieldId, StringComparison.Ordinal))
                    return overrides[index].Root;
            return null;
        }

        /// <summary>
        /// 为未覆写字段递归创建 required 默认值或显式 optional absent。
        /// </summary>
        private static LubanSemanticSourceNode DefaultNode(
            CanonicalSemanticNodeSchema schema,
            string domainId,
            string row,
            string path,
            long? stableKey)
        {
            var provenance = Provenance(domainId, row, path, schema.RuleId);
            if (schema.PresencePolicy == CanonicalPresencePolicy.Optional)
                return LubanSemanticSourceNode.Absent(schema.NodeId, schema.TypeId, schema.TargetDomainId, provenance);
            if (schema.NodeKind == CanonicalSemanticNodeKind.Scalar)
                return Scalar(schema, domainId, row, path, DefaultScalarValue(schema, stableKey), null);
            if (schema.NodeKind == CanonicalSemanticNodeKind.Collection)
                return LubanSemanticSourceNode.Collection(schema.NodeId, schema.TypeId, schema.TargetDomainId, schema.CollectionSemantics,
                    Array.Empty<LubanSemanticSourceNode>(), provenance);
            if (schema.NodeKind == CanonicalSemanticNodeKind.Variant)
                return DefaultVariant(schema, domainId, row, path);
            return Record(schema, domainId, row, path, null);
        }

        /// <summary>
        /// 为 primitive/enum/reference schema 创建合法的闭集默认值。
        /// </summary>
        private static LubanSemanticSourceValue DefaultScalarValue(CanonicalSemanticNodeSchema schema, long? stableKey)
        {
            switch (schema.ScalarKind)
            {
                case CanonicalSemanticValueKind.Int32: return LubanSemanticSourceValue.Int32((int)(stableKey ?? 0));
                case CanonicalSemanticValueKind.Boolean: return LubanSemanticSourceValue.Boolean(false);
                case CanonicalSemanticValueKind.Float32: return LubanSemanticSourceValue.Float32Bits(0);
                case CanonicalSemanticValueKind.String: return LubanSemanticSourceValue.String(string.Empty);
                case CanonicalSemanticValueKind.Enum:
                    return LubanSemanticSourceValue.Enum(schema.TypeId, CanonicalEnumUnderlyingType.Int32, EnumDefault(schema.TypeId));
                case CanonicalSemanticValueKind.DefinitionReference:
                    return LubanSemanticSourceValue.Reference(ZeroReference(schema.TargetDomainId));
                default: throw new InvalidOperationException("No fixture default for " + schema.ScalarKind);
            }
        }

        /// <summary>
        /// 为闭集 enum 选择最小合法值。
        /// </summary>
        private static ulong EnumDefault(string typeId)
        {
            return typeId.StartsWith("exgas.RuntimeV1", StringComparison.Ordinal)
                   && !string.Equals(typeId, "exgas.RuntimeV1EvaluatorKind", StringComparison.Ordinal)
                ? 1UL
                : 0UL;
        }

        /// <summary>
        /// 创建默认 ApplyEffectsOnActivate variant，IDs 为完整空 ordered list。
        /// </summary>
        private static LubanSemanticSourceNode DefaultVariant(
            CanonicalSemanticNodeSchema schema,
            string domainId,
            string row,
            string path)
        {
            var variant = schema.Variants[0];
            var members = new LubanSemanticSourceNode[variant.Members.Count];
            for (var index = 0; index < members.Length; index++)
                members[index] = DefaultNode(variant.Members[index], domainId, row, path + "." + variant.Members[index].NodeId, null);
            return LubanSemanticSourceNode.Variant(
                schema.NodeId,
                variant.VariantTypeId,
                members,
                Provenance(domainId, row, path, schema.RuleId));
        }

        /// <summary>
        /// 创建 record 并允许调用方按 child NodeId 覆写完整成员。
        /// </summary>
        private static LubanSemanticSourceNode Record(
            CanonicalSemanticNodeSchema schema,
            string domainId,
            string row,
            string path,
            IReadOnlyList<NodeOverride> overrides)
        {
            var members = new LubanSemanticSourceNode[schema.Children.Count];
            for (var index = 0; index < members.Length; index++)
            {
                var child = schema.Children[index];
                members[index] = FindNodeOverride(overrides, child.NodeId)
                    ?? DefaultNode(child, domainId, row, path + "." + child.NodeId, null);
            }
            return LubanSemanticSourceNode.Record(
                schema.NodeId, schema.TypeId, members, Provenance(domainId, row, path, schema.RuleId));
        }

        /// <summary>
        /// 查找 record child override。
        /// </summary>
        private static LubanSemanticSourceNode FindNodeOverride(IReadOnlyList<NodeOverride> overrides, string nodeId)
        {
            if (overrides == null)
                return null;
            for (var index = 0; index < overrides.Count; index++)
                if (string.Equals(overrides[index].NodeId, nodeId, StringComparison.Ordinal))
                    return overrides[index].Node;
            return null;
        }

        /// <summary>
        /// 创建 Duration record，Time=0 仍保持 present 节点而非 absent。
        /// </summary>
        private static LubanSemanticSourceNode Duration(long owner, int time)
        {
            var schema = FieldRoot(CanonicalSemanticSchemaRegistry.GameplayEffectDomain, "Duration");
            var row = owner.ToString(CultureInfo.InvariantCulture);
            return Record(schema, CanonicalSemanticSchemaRegistry.GameplayEffectDomain, row, "Duration", new[]
            {
                Node("TimeUnit", EnumChild(schema, "TimeUnit", row, 0)),
                Node("Time", IntChild(schema, "Time", row, time)),
                Node("ResetStartTimeWhenActivated", BoolChild(schema, "ResetStartTimeWhenActivated", row, false)),
            });
        }

        /// <summary>
        /// 创建 AbilityExecution.ApplyEffectsOnActivate 并保留 IDs 的完整 authored 顺序。
        /// </summary>
        private static LubanSemanticSourceNode AbilityExecution(long owner, IReadOnlyList<long> effectIds)
        {
            var domain = CanonicalSemanticSchemaRegistry.AbilityDomain;
            var schema = FieldRoot(domain, "AbilityExecution");
            var variant = schema.Variants[0];
            var paramSchema = variant.Members[0];
            var idsSchema = Child(paramSchema, "IDs");
            var row = owner.ToString(CultureInfo.InvariantCulture);
            var ids = ReferenceCollection(idsSchema, domain, row, "AbilityExecution.Param.IDs", effectIds);
            var param = Record(paramSchema, domain, row, "AbilityExecution.Param", new[] { Node("IDs", ids) });
            return LubanSemanticSourceNode.Variant(
                schema.NodeId,
                variant.VariantTypeId,
                new[] { param },
                Provenance(domain, row, "AbilityExecution", schema.RuleId));
        }

        /// <summary>
        /// 创建 TagRequirementSpec，其中 mutation 在 All/Any 间移动同一 tag。
        /// </summary>
        private static LubanSemanticSourceNode TagRequirement(long owner, bool mutation)
        {
            var domain = CanonicalSemanticSchemaRegistry.GameplayEffectDomain;
            var schema = FieldRoot(domain, "ApplicationRequiredTags");
            var row = owner.ToString(CultureInfo.InvariantCulture);
            var all = ReferenceCollection(Child(schema, "All"), domain, row, "ApplicationRequiredTags.All",
                mutation ? Array.Empty<long>() : new[] { 2006L });
            var any = ReferenceCollection(Child(schema, "Any"), domain, row, "ApplicationRequiredTags.Any",
                mutation ? new[] { 2006L } : Array.Empty<long>());
            var none = ReferenceCollection(Child(schema, "None"), domain, row, "ApplicationRequiredTags.None", Array.Empty<long>());
            return Record(schema, domain, row, "ApplicationRequiredTags", new[]
            {
                Node("All", all), Node("Any", any), Node("None", none),
            });
        }

        /// <summary>
        /// 创建 Period record 并保留 Effects=[1001, second] 的完整 ordered list。
        /// </summary>
        private static LubanSemanticSourceNode Period(long owner, long secondEffect)
        {
            var domain = CanonicalSemanticSchemaRegistry.GameplayEffectDomain;
            var schema = FieldRoot(domain, "Period");
            var row = owner.ToString(CultureInfo.InvariantCulture);
            return Record(schema, domain, row, "Period", new[]
            {
                Node("Time", IntChild(schema, "Time", row, 100)),
                Node("Effects", ReferenceCollection(Child(schema, "Effects"), domain, row, "Period.Effects", new[] { 1001L, secondEffect })),
                Node("FirstTrigger", BoolChild(schema, "FirstTrigger", row, true)),
            });
        }

        /// <summary>
        /// 创建单元素 ordered Modifier record，保留复合引用、float bits 与 enum。
        /// </summary>
        private static LubanSemanticSourceNode Modifiers(long owner, bool mutation)
        {
            var domain = CanonicalSemanticSchemaRegistry.GameplayEffectDomain;
            var schema = FieldRoot(domain, "Modifiers");
            var elementSchema = schema.Children[0];
            var row = owner.ToString(CultureInfo.InvariantCulture);
            var element = Record(elementSchema, domain, row, "Modifiers[0]", new[]
            {
                Node("AttrSet", ReferenceChild(elementSchema, "AttrSet", domain, row, "Modifiers[0].AttrSet", new[] { 1L })),
                Node("Attribute", ReferenceChild(elementSchema, "Attribute", domain, row, "Modifiers[0].Attribute", new[] { 1L, 1L })),
                Node("Magnitude", FloatChild(elementSchema, "Magnitude", row, mutation ? 0x40000000U : 0x3fc00000U)),
                Node("Operation", EnumChild(elementSchema, "Operation", row, 0)),
            });
            return LubanSemanticSourceNode.Collection(
                schema.NodeId, schema.TypeId, schema.TargetDomainId, schema.CollectionSemantics,
                new[] { element }, Provenance(domain, row, "Modifiers", schema.RuleId));
        }

        /// <summary>
        /// 创建六类 Cue set 中一个字段，mutation 将 cue1001 改为1002。
        /// </summary>
        private static LubanSemanticSourceNode Cue(long owner, string fieldId, bool mutation)
        {
            return ReferenceFieldCollection(
                CanonicalSemanticSchemaRegistry.GameplayEffectDomain,
                fieldId,
                owner,
                new[] { mutation ? 1002L : 1001L });
        }

        /// <summary>
        /// 创建 ordered GrantedAbility record list并保留三类 policy enum。
        /// </summary>
        private static LubanSemanticSourceNode GrantedAbilities(long owner, bool mutation, long abilityId = 5000)
        {
            var domain = CanonicalSemanticSchemaRegistry.GameplayEffectDomain;
            var schema = FieldRoot(domain, "GrantedAbility");
            var elementSchema = schema.Children[0];
            var row = owner.ToString(CultureInfo.InvariantCulture);
            var element = Record(elementSchema, domain, row, "GrantedAbility[0]", new[]
            {
                Node("ID", ReferenceChild(elementSchema, "ID", domain, row, "GrantedAbility[0].ID", new[] { abilityId })),
                Node("Level", IntChild(elementSchema, "Level", row, mutation ? 2 : 1)),
                Node("ActivationPolicy", EnumChild(elementSchema, "ActivationPolicy", row, 0)),
                Node("DeactivationPolicy", EnumChild(elementSchema, "DeactivationPolicy", row, 0)),
                Node("RemovePolicy", EnumChild(elementSchema, "RemovePolicy", row, 0)),
            });
            return LubanSemanticSourceNode.Collection(
                schema.NodeId, schema.TypeId, schema.TargetDomainId, schema.CollectionSemantics,
                new[] { element }, Provenance(domain, row, "GrantedAbility", schema.RuleId));
        }

        /// <summary>
        /// 创建完整 Stacking record，overflow ordered list 的第二项参与 mutation。
        /// </summary>
        private static LubanSemanticSourceNode Stacking(long owner, bool mutation)
        {
            var domain = CanonicalSemanticSchemaRegistry.GameplayEffectDomain;
            var schema = FieldRoot(domain, "Stacking");
            var row = owner.ToString(CultureInfo.InvariantCulture);
            return Record(schema, domain, row, "Stacking", new[]
            {
                Node("StackingType", EnumChild(schema, "StackingType", row, 1)),
                Node("StackCode", IntChild(schema, "StackCode", row, 1005)),
                Node("LimitCount", IntChild(schema, "LimitCount", row, 3)),
                Node("DurationRefreshPolicy", EnumChild(schema, "DurationRefreshPolicy", row, 1)),
                Node("PeriodResetPolicy", EnumChild(schema, "PeriodResetPolicy", row, 1)),
                Node("ExpirationPolicy", EnumChild(schema, "ExpirationPolicy", row, 1)),
                Node("DenyOverflowApplication", BoolChild(schema, "DenyOverflowApplication", row, true)),
                Node("ClearStackOnOverflow", BoolChild(schema, "ClearStackOnOverflow", row, false)),
                Node("OverflowEffects", ReferenceCollection(Child(schema, "OverflowEffects"), domain, row,
                    "Stacking.OverflowEffects", new[] { 1001L, mutation ? 1003L : 1002L })),
            });
        }

        /// <summary>
        /// 创建 RuntimeV1TargetPolicy 的四个闭集 enum。
        /// </summary>
        private static LubanSemanticSourceNode TargetPolicy(long owner, bool mutation)
        {
            var domain = CanonicalSemanticSchemaRegistry.GameplayEffectDomain;
            var schema = FieldRoot(domain, "RuntimeV1TargetPolicy");
            var row = owner.ToString(CultureInfo.InvariantCulture);
            return Record(schema, domain, row, "RuntimeV1TargetPolicy", new[]
            {
                Node("LogicalTarget", EnumChild(schema, "LogicalTarget", row, mutation ? 1UL : 2UL)),
                Node("Avatar", EnumChild(schema, "Avatar", row, 2)),
                Node("Spatial", EnumChild(schema, "Spatial", row, 1)),
                Node("Life", EnumChild(schema, "Life", row, 1)),
            });
        }

        /// <summary>
        /// 创建 RuntimeV1Evaluator 的 enum、复合引用与全部 float 参数。
        /// </summary>
        private static LubanSemanticSourceNode Evaluator(long owner, bool mutation)
        {
            var domain = CanonicalSemanticSchemaRegistry.GameplayEffectDomain;
            var schema = FieldRoot(domain, "RuntimeV1Evaluator");
            var row = owner.ToString(CultureInfo.InvariantCulture);
            return Record(schema, domain, row, "RuntimeV1Evaluator", new[]
            {
                Node("Kind", EnumChild(schema, "Kind", row, 1)),
                Node("AttributeSet", ReferenceChild(schema, "AttributeSet", domain, row, "RuntimeV1Evaluator.AttributeSet", new[] { 1L })),
                Node("Attribute", ReferenceChild(schema, "Attribute", domain, row, "RuntimeV1Evaluator.Attribute", new[] { 1L, 1L })),
                Node("BaseValue", FloatChild(schema, "BaseValue", row, 0x3f800000U)),
                Node("Coefficient", FloatChild(schema, "Coefficient", row, mutation ? 0x40000000U : 0x3fa00000U)),
                Node("Minimum", FloatChild(schema, "Minimum", row, 0)),
                Node("Maximum", FloatChild(schema, "Maximum", row, 0x42c80000U)),
            });
        }

        /// <summary>
        /// 创建顶层 Int32 字段节点。
        /// </summary>
        private static LubanSemanticSourceNode IntField(string domain, string fieldId, long owner, int value)
        {
            var schema = FieldRoot(domain, fieldId);
            return Scalar(schema, domain, owner.ToString(CultureInfo.InvariantCulture), fieldId,
                LubanSemanticSourceValue.Int32(value), null);
        }

        /// <summary>
        /// 创建顶层 reference 字段节点。
        /// </summary>
        private static LubanSemanticSourceNode ReferenceField(string domain, string fieldId, long owner, long target)
        {
            return ReferenceNode(
                FieldRoot(domain, fieldId), domain, owner.ToString(CultureInfo.InvariantCulture), fieldId, new[] { target });
        }

        /// <summary>
        /// 创建顶层 reference collection 字段节点。
        /// </summary>
        private static LubanSemanticSourceNode ReferenceFieldCollection(
            string domain,
            string fieldId,
            long owner,
            IReadOnlyList<long> targets)
        {
            return ReferenceCollection(
                FieldRoot(domain, fieldId), domain, owner.ToString(CultureInfo.InvariantCulture), fieldId, targets);
        }

        /// <summary>
        /// 创建 reference collection；set 元素 provenance path 不依赖输入位置。
        /// </summary>
        private static LubanSemanticSourceNode ReferenceCollection(
            CanonicalSemanticNodeSchema schema,
            string domain,
            string row,
            string path,
            IReadOnlyList<long> targets)
        {
            var elements = new LubanSemanticSourceNode[targets.Count];
            var occurrences = new Dictionary<long, int>();
            for (var index = 0; index < elements.Length; index++)
            {
                occurrences.TryGetValue(targets[index], out var occurrence);
                occurrences[targets[index]] = occurrence + 1;
                var suffix = CollectionElementSuffix(schema, targets[index], index, occurrence);
                elements[index] = ReferenceNode(
                    schema.Children[0], domain, row, path + "[" + suffix + "]", new[] { targets[index] });
            }
            return LubanSemanticSourceNode.Collection(
                schema.NodeId, schema.TypeId, schema.TargetDomainId, schema.CollectionSemantics,
                elements, Provenance(domain, row, path, schema.RuleId));
        }

        /// <summary>
        /// 为 ordered 或 set 元素生成对应的稳定 provenance 后缀。
        /// </summary>
        private static string CollectionElementSuffix(
            CanonicalSemanticNodeSchema schema,
            long target,
            int index,
            int occurrence)
        {
            if (schema.CollectionSemantics == CanonicalCollectionSemantics.Ordered)
                return index.ToString(CultureInfo.InvariantCulture);
            var suffix = target.ToString(CultureInfo.InvariantCulture);
            return occurrence == 0 ? suffix : suffix + "#" + occurrence.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 创建 record child reference。
        /// </summary>
        private static LubanSemanticSourceNode ReferenceChild(
            CanonicalSemanticNodeSchema parent,
            string childId,
            string domain,
            string row,
            string path,
            long[] stableIds)
        {
            return ReferenceNode(Child(parent, childId), domain, row, path, stableIds);
        }

        /// <summary>
        /// 创建显式 target-domain source reference 并绑定 related definition provenance。
        /// </summary>
        private static LubanSemanticSourceNode ReferenceNode(
            CanonicalSemanticNodeSchema schema,
            string domain,
            string row,
            string path,
            long[] stableIds)
        {
            CanonicalSemanticSchemaRegistry.TryResolve(schema.TargetDomainId, out var target);
            var reference = new LubanSemanticSourceDefinitionId(schema.TargetDomainId, target.DefinitionKind, stableIds);
            return Scalar(schema, domain, row, path, LubanSemanticSourceValue.Reference(reference), new[] { reference });
        }

        /// <summary>
        /// 创建 record child Int32。
        /// </summary>
        private static LubanSemanticSourceNode IntChild(
            CanonicalSemanticNodeSchema parent,
            string childId,
            string row,
            int value)
        {
            var schema = Child(parent, childId);
            return Scalar(schema, CanonicalSemanticSchemaRegistry.GameplayEffectDomain, row, childId,
                LubanSemanticSourceValue.Int32(value), null);
        }

        /// <summary>
        /// 创建 record child Boolean。
        /// </summary>
        private static LubanSemanticSourceNode BoolChild(
            CanonicalSemanticNodeSchema parent,
            string childId,
            string row,
            bool value)
        {
            var schema = Child(parent, childId);
            return Scalar(schema, CanonicalSemanticSchemaRegistry.GameplayEffectDomain, row, childId,
                LubanSemanticSourceValue.Boolean(value), null);
        }

        /// <summary>
        /// 创建 record child Float32 原始位值。
        /// </summary>
        private static LubanSemanticSourceNode FloatChild(
            CanonicalSemanticNodeSchema parent,
            string childId,
            string row,
            uint bits)
        {
            var schema = Child(parent, childId);
            return Scalar(schema, CanonicalSemanticSchemaRegistry.GameplayEffectDomain, row, childId,
                LubanSemanticSourceValue.Float32Bits(bits), null);
        }

        /// <summary>
        /// 创建 record child Int32-underlying enum。
        /// </summary>
        private static LubanSemanticSourceNode EnumChild(
            CanonicalSemanticNodeSchema parent,
            string childId,
            string row,
            ulong value)
        {
            var schema = Child(parent, childId);
            return Scalar(schema, CanonicalSemanticSchemaRegistry.GameplayEffectDomain, row, childId,
                LubanSemanticSourceValue.Enum(schema.TypeId, CanonicalEnumUnderlyingType.Int32, value), null);
        }

        /// <summary>
        /// 创建 scalar source node 与逐节点 provenance。
        /// </summary>
        private static LubanSemanticSourceNode Scalar(
            CanonicalSemanticNodeSchema schema,
            string domain,
            string row,
            string path,
            LubanSemanticSourceValue value,
            IReadOnlyList<LubanSemanticSourceDefinitionId> related)
        {
            return LubanSemanticSourceNode.Scalar(
                schema.NodeId, schema.TypeId, schema.TargetDomainId, value,
                Provenance(domain, row, path, schema.RuleId, related));
        }

        /// <summary>
        /// 创建全零 none sentinel reference，stable ID arity 由 target registry 决定。
        /// </summary>
        private static LubanSemanticSourceDefinitionId ZeroReference(string targetDomain)
        {
            CanonicalSemanticSchemaRegistry.TryResolve(targetDomain, out var schema);
            return new LubanSemanticSourceDefinitionId(
                targetDomain, schema.DefinitionKind, new long[schema.StableIdPartCount]);
        }

        /// <summary>
        /// 按 domain/FieldId 取得顶层 root schema。
        /// </summary>
        private static CanonicalSemanticNodeSchema FieldRoot(string domainId, string fieldId)
        {
            CanonicalSemanticSchemaRegistry.TryResolve(domainId, out var domain);
            if (!domain.TryResolveField(fieldId, out var field))
                throw new InvalidOperationException("Fixture schema field missing: " + domainId + "/" + fieldId);
            return field.Root;
        }

        /// <summary>
        /// 按 NodeId 查找 record child schema。
        /// </summary>
        private static CanonicalSemanticNodeSchema Child(CanonicalSemanticNodeSchema parent, string nodeId)
        {
            for (var index = 0; index < parent.Children.Count; index++)
                if (string.Equals(parent.Children[index].NodeId, nodeId, StringComparison.Ordinal))
                    return parent.Children[index];
            throw new InvalidOperationException("Fixture child schema missing: " + nodeId);
        }

        /// <summary>
        /// 创建 source provenance；workbook/table 均为稳定相对 identity。
        /// </summary>
        private static LubanSemanticSourceProvenance Provenance(
            string domain,
            string row,
            string path,
            string ruleId,
            IReadOnlyList<LubanSemanticSourceDefinitionId> related = null)
        {
            CanonicalSemanticRuleCatalog.TryResolve(ruleId, out var version, out _);
            return new LubanSemanticSourceProvenance(
                "EX_GAS_Config/ProjectConfigTable/exgas_config/Datas",
                domain,
                row,
                path,
                "fixture:" + path,
                "fixture:" + path,
                ruleId,
                version,
                GeneratorVersion,
                related ?? Array.Empty<LubanSemanticSourceDefinitionId>());
        }

        /// <summary>
        /// 将复合 stable id 格式化为不受文化区影响的 row identity。
        /// </summary>
        private static string RowId(IReadOnlyList<long> stableIds)
        {
            var parts = new string[stableIds.Count];
            for (var index = 0; index < parts.Length; index++)
                parts[index] = stableIds[index].ToString(CultureInfo.InvariantCulture);
            return string.Join(".", parts);
        }

        /// <summary>
        /// 创建 record child override。
        /// </summary>
        private static NodeOverride Node(string nodeId, LubanSemanticSourceNode node)
        {
            return new NodeOverride(nodeId, node);
        }

        /// <summary>
        /// 创建一个顶层字段覆写。
        /// </summary>
        private static FieldOverride Override(string fieldId, LubanSemanticSourceNode root)
        {
            return new FieldOverride(fieldId, root);
        }

        /// <summary>
        /// 保存 fixture 对一个顶层字段的完整 source tree 覆写。
        /// </summary>
        private sealed class FieldOverride
        {
            /// <summary>
            /// 创建字段 ID 与完整 root 覆写。
            /// </summary>
            public FieldOverride(string fieldId, LubanSemanticSourceNode root)
            {
                FieldId = fieldId;
                Root = root;
            }

            public string FieldId { get; }

            public LubanSemanticSourceNode Root { get; }
        }

        /// <summary>
        /// 保存 record fixture 对一个具名 child 的显式覆写。
        /// </summary>
        private sealed class NodeOverride
        {
            /// <summary>
            /// 创建 child id 与完整 source node 覆写。
            /// </summary>
            public NodeOverride(string nodeId, LubanSemanticSourceNode node)
            {
                NodeId = nodeId;
                Node = node;
            }

            public string NodeId { get; }

            public LubanSemanticSourceNode Node { get; }
        }
    }

    /// <summary>
    /// 伪造超限 count 且任何枚举/索引都抛错的列表，证明预检不会继续遍历。
    /// </summary>
    internal sealed class OversizedReadOnlyList<T> : IReadOnlyList<T>
    {
        public int Count => CanonicalSemanticResourceBudget.MaxSingleCollectionCount + 1;

        public T this[int index] => throw new InvalidOperationException("Budget preflight traversed oversized input.");

        /// <summary>
        /// 禁止枚举；合法实现必须在调用前已因 count 越界失败。
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            throw new InvalidOperationException("Budget preflight enumerated oversized input.");
        }

        /// <summary>
        /// 禁止非泛型枚举，与泛型入口共用同一失败语义。
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// 第一次报告安全 count、复制后报告超限 count，验证分配上界不跟随 live Count 漂移。
    /// </summary>
    internal sealed class SafeToHugeReadOnlyList<T> : IReadOnlyList<T>
    {
        private readonly T _value;
        private int _countReads;

        /// <summary>
        /// 创建只允许固定索引零读取的 safe-to-huge 对抗列表。
        /// </summary>
        public SafeToHugeReadOnlyList(T value)
        {
            _value = value;
        }

        public int Count => ++_countReads == 1
            ? 1
            : CanonicalSemanticResourceBudget.MaxSingleCollectionCount + 1;

        public T this[int index] => index == 0
            ? _value
            : throw new InvalidOperationException("Unexpected safe-to-huge index.");

        /// <summary>
        /// 枚举器必须永不被冻结协议调用。
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            throw new InvalidOperationException("Snapshot enumerated safe-to-huge input.");
        }

        /// <summary>
        /// 非泛型枚举器委托给禁止枚举入口。
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// 保持 Count 不变但在复核读取替换元素，验证 same-count 变更也会 fail closed。
    /// </summary>
    internal sealed class SameCountChangingReadOnlyList<T> : IReadOnlyList<T>
    {
        private readonly T _first;
        private readonly T _second;
        private int _indexReads;

        /// <summary>
        /// 创建首次与复核读取返回不同值的单元素对抗列表。
        /// </summary>
        public SameCountChangingReadOnlyList(T first, T second)
        {
            _first = first;
            _second = second;
        }

        public int Count => 1;

        public T this[int index] => index != 0
            ? throw new InvalidOperationException("Unexpected same-count index.")
            : ++_indexReads == 1 ? _first : _second;

        /// <summary>
        /// 枚举器必须永不被冻结协议调用。
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            throw new InvalidOperationException("Snapshot enumerated same-count input.");
        }

        /// <summary>
        /// 非泛型枚举器委托给禁止枚举入口。
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// 为无测试框架的隔离宿主提供带稳定上下文的最小断言。
    /// </summary>
    internal static class AssertEx
    {
        /// <summary>
        /// 断言条件为真。
        /// </summary>
        public static void True(bool condition, string context)
        {
            if (!condition)
                throw new InvalidOperationException("Assert true failed: " + context);
        }

        /// <summary>
        /// 断言两个值相等。
        /// </summary>
        public static void Equal<T>(T expected, T actual, string context)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(context + ": expected=" + expected + ", actual=" + actual);
        }

        /// <summary>
        /// 断言两个值不同。
        /// </summary>
        public static void NotEqual<T>(T left, T right, string context)
        {
            if (EqualityComparer<T>.Default.Equals(left, right))
                throw new InvalidOperationException("Assert not equal failed: " + context);
        }

        /// <summary>
        /// 逐 byte 断言两个编码完全相等。
        /// </summary>
        public static void BytesEqual(byte[] expected, byte[] actual, string context)
        {
            Equal(expected.Length, actual.Length, context + " length");
            for (var index = 0; index < expected.Length; index++)
                if (expected[index] != actual[index])
                    throw new InvalidOperationException(context + ": byte mismatch at " + index);
        }

        /// <summary>
        /// 断言 action 抛出指定 RuleId 并返回异常供 provenance/evidence 继续核验。
        /// </summary>
        public static CanonicalSemanticValidationException Rule(Action action, string ruleId, string context)
        {
            try
            {
                action();
            }
            catch (CanonicalSemanticValidationException exception)
            {
                Equal(ruleId, exception.RuleId, context + " RuleId");
                return exception;
            }
            throw new InvalidOperationException("Expected rule was not thrown: " + context + "/" + ruleId);
        }

        /// <summary>
        /// 断言 action 抛出指定 CLR 异常类型。
        /// </summary>
        public static void Throws<TException>(Action action, string context)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            throw new InvalidOperationException("Expected exception was not thrown: " + context);
        }
    }
}
