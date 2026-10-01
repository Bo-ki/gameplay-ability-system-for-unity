using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GAS.Editor.CodeGen.Semantics
{
    /// <summary>
    /// 冻结一条稳定 RuleId 声明；RuleOrdinal 与固定 registry 共同组成 rule-domain schema。
    /// </summary>
    public sealed class CanonicalSemanticRule
    {
        /// <summary>
        /// 创建显式 ordinal 的规则声明。
        /// </summary>
        internal CanonicalSemanticRule(
            int ruleOrdinal,
            string ruleId,
            uint ruleVersion,
            CanonicalRuleKind ruleKind,
            CanonicalSemanticProvenance provenance)
        {
            RuleOrdinal = ruleOrdinal;
            RuleId = ruleId;
            RuleVersion = ruleVersion;
            RuleKind = ruleKind;
            Provenance = provenance;
        }

        public int RuleOrdinal { get; }

        public string RuleId { get; }

        public uint RuleVersion { get; }

        public CanonicalRuleKind RuleKind { get; }

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 冻结 optional、record、variant、collection 与 scalar 的完整 canonical semantic tree 节点。
    /// </summary>
    public sealed class CanonicalSemanticNode
    {
        private readonly ReadOnlyCollection<CanonicalSemanticNode> _children;

        /// <summary>
        /// 创建显式 ordinal 的 tree 节点；构造器仅供 sealed compiler 与 v2 codec 使用。
        /// </summary>
        internal CanonicalSemanticNode(
            int nodeOrdinal,
            string nodeId,
            CanonicalSemanticNodeKind nodeKind,
            string typeId,
            string targetDomainId,
            CanonicalCollectionSemantics collectionSemantics,
            CanonicalSemanticValue scalarValue,
            IReadOnlyList<CanonicalSemanticNode> children,
            CanonicalSemanticProvenance provenance)
        {
            NodeOrdinal = nodeOrdinal;
            NodeId = nodeId;
            NodeKind = nodeKind;
            TypeId = typeId;
            TargetDomainId = targetDomainId;
            CollectionSemantics = collectionSemantics;
            ScalarValue = scalarValue;
            _children = FreezeAndSort(children);
            Provenance = provenance;
        }

        public int NodeOrdinal { get; }

        public string NodeId { get; }

        public CanonicalSemanticNodeKind NodeKind { get; }

        public string TypeId { get; }

        public string TargetDomainId { get; }

        public CanonicalCollectionSemantics CollectionSemantics { get; }

        public CanonicalSemanticValue ScalarValue { get; }

        public IReadOnlyList<CanonicalSemanticNode> Children => _children;

        public CanonicalSemanticProvenance Provenance { get; }

        /// <summary>
        /// 复制并按当前 scope 的 NodeOrdinal 排序子节点。
        /// </summary>
        private static ReadOnlyCollection<CanonicalSemanticNode> FreezeAndSort(
            IReadOnlyList<CanonicalSemanticNode> values)
        {
            var result = CanonicalSemanticResourceBudget.SnapshotList(
                values,
                "canonical node children");
            Array.Sort(result, CompareNodes);
            return Array.AsReadOnly(result);
        }

        /// <summary>
        /// 比较子节点的显式 ordinal。
        /// </summary>
        private static int CompareNodes(CanonicalSemanticNode left, CanonicalSemanticNode right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            return left.NodeOrdinal.CompareTo(right.NodeOrdinal);
        }
    }

    /// <summary>
    /// 表示 Definition 上一个显式 ordinal、字段分类、support disposition 与完整 semantic tree。
    /// </summary>
    public sealed class CanonicalSemanticField
    {
        /// <summary>
        /// 创建由 registry 与 projector 共同冻结的 canonical 字段。
        /// </summary>
        internal CanonicalSemanticField(
            int fieldOrdinal,
            string fieldId,
            CanonicalFieldClassification classification,
            CanonicalFieldDisposition disposition,
            CanonicalSemanticNode root,
            CanonicalSemanticProvenance provenance)
        {
            FieldOrdinal = fieldOrdinal;
            FieldId = fieldId;
            Classification = classification;
            Disposition = disposition;
            Root = root;
            Provenance = provenance;
        }

        public int FieldOrdinal { get; }

        public string FieldId { get; }

        public CanonicalFieldClassification Classification { get; }

        public CanonicalFieldDisposition Disposition { get; }

        public CanonicalSemanticNode Root { get; }

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 冻结 compiler 从 incoming authoring reference 推导的 Definition role，source DTO 无权注入。
    /// </summary>
    public sealed class CanonicalSemanticRole
    {
        /// <summary>
        /// 创建带 owner Definition、owner 字段与稳定 RuleId 的角色声明。
        /// </summary>
        internal CanonicalSemanticRole(
            int roleOrdinal,
            CanonicalDefinitionRoleKind roleKind,
            CanonicalDefinitionKey ownerDefinition,
            int ownerFieldOrdinal,
            string ruleId,
            uint ruleVersion,
            CanonicalSemanticProvenance provenance)
        {
            RoleOrdinal = roleOrdinal;
            RoleKind = roleKind;
            OwnerDefinition = ownerDefinition;
            OwnerFieldOrdinal = ownerFieldOrdinal;
            RuleId = ruleId;
            RuleVersion = ruleVersion;
            Provenance = provenance;
        }

        public int RoleOrdinal { get; }

        public CanonicalDefinitionRoleKind RoleKind { get; }

        public CanonicalDefinitionKey OwnerDefinition { get; }

        public int OwnerFieldOrdinal { get; }

        public string RuleId { get; }

        public uint RuleVersion { get; }

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 冻结一个 program 对真实字段的 contextual allow/deny/projection 决策及字段激活事实。
    /// </summary>
    public sealed class CanonicalSemanticContextDecision
    {
        /// <summary>
        /// 创建由 fixed matrix 与字段 tree 派生的不可伪造决策。
        /// </summary>
        internal CanonicalSemanticContextDecision(
            int decisionOrdinal,
            int fieldOrdinal,
            string fieldId,
            bool isActive,
            CanonicalFieldClassification classification,
            CanonicalFieldDisposition disposition,
            string ruleId,
            uint ruleVersion,
            CanonicalSemanticProvenance provenance)
        {
            DecisionOrdinal = decisionOrdinal;
            FieldOrdinal = fieldOrdinal;
            FieldId = fieldId;
            IsActive = isActive;
            Classification = classification;
            Disposition = disposition;
            RuleId = ruleId;
            RuleVersion = ruleVersion;
            Provenance = provenance;
        }

        public int DecisionOrdinal { get; }

        public int FieldOrdinal { get; }

        public string FieldId { get; }

        public bool IsActive { get; }

        public CanonicalFieldClassification Classification { get; }

        public CanonicalFieldDisposition Disposition { get; }

        public string RuleId { get; }

        public uint RuleVersion { get; }

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 冻结 operation 的 owner/node/field、ValueView、phase、target domain、bounds 与 canonical payload。
    /// </summary>
    public sealed class CanonicalSemanticProjectionBinding
    {
        private readonly byte[] _payload;

        /// <summary>
        /// 创建 sealed compiler 输出的投影绑定并防御复制 payload。
        /// </summary>
        internal CanonicalSemanticProjectionBinding(
            int projectionOrdinal,
            CanonicalDefinitionKey ownerDefinition,
            int programOrdinal,
            int nodeOrdinal,
            int sourceFieldOrdinal,
            string operationId,
            string valueView,
            string phase,
            string targetDomainId,
            int maxItemCount,
            int maxPayloadBytes,
            byte[] payload,
            string ruleId,
            uint ruleVersion,
            CanonicalSemanticProvenance provenance)
        {
            CanonicalSemanticResourceBudget.RequirePayloadLength(
                payload == null ? 0 : payload.Length,
                "canonical projection payload");
            ProjectionOrdinal = projectionOrdinal;
            OwnerDefinition = ownerDefinition;
            ProgramOrdinal = programOrdinal;
            NodeOrdinal = nodeOrdinal;
            SourceFieldOrdinal = sourceFieldOrdinal;
            OperationId = operationId;
            ValueView = valueView;
            Phase = phase;
            TargetDomainId = targetDomainId;
            MaxItemCount = maxItemCount;
            MaxPayloadBytes = maxPayloadBytes;
            _payload = payload == null ? Array.Empty<byte>() : (byte[])payload.Clone();
            RuleId = ruleId;
            RuleVersion = ruleVersion;
            Provenance = provenance;
        }

        public int ProjectionOrdinal { get; }

        public CanonicalDefinitionKey OwnerDefinition { get; }

        public int ProgramOrdinal { get; }

        public int NodeOrdinal { get; }

        public int SourceFieldOrdinal { get; }

        public string OperationId { get; }

        public string ValueView { get; }

        public string Phase { get; }

        public string TargetDomainId { get; }

        public int MaxItemCount { get; }

        public int MaxPayloadBytes { get; }

        public byte[] Payload => (byte[])_payload.Clone();

        internal byte[] PayloadUnsafe => _payload;

        public string RuleId { get; }

        public uint RuleVersion { get; }

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 冻结一个强制 operation node 的 read sets 与 projection binding。
    /// </summary>
    public sealed class CanonicalSemanticProgramNode
    {
        private readonly ReadOnlyCollection<int> _fieldOrdinals;
        private readonly ReadOnlyCollection<int> _dependencyOrdinals;

        /// <summary>
        /// 创建显式 node ordinal，并将 read sets 排序为集合语义。
        /// </summary>
        internal CanonicalSemanticProgramNode(
            int nodeOrdinal,
            string operationId,
            IReadOnlyList<int> fieldOrdinals,
            IReadOnlyList<int> dependencyOrdinals,
            CanonicalSemanticProjectionBinding projection,
            CanonicalSemanticProvenance provenance)
        {
            NodeOrdinal = nodeOrdinal;
            OperationId = operationId;
            _fieldOrdinals = FreezeAndSort(fieldOrdinals);
            _dependencyOrdinals = FreezeAndSort(dependencyOrdinals);
            Projection = projection;
            Provenance = provenance;
        }

        public int NodeOrdinal { get; }

        public string OperationId { get; }

        public IReadOnlyList<int> FieldOrdinals => _fieldOrdinals;

        public IReadOnlyList<int> DependencyOrdinals => _dependencyOrdinals;

        public CanonicalSemanticProjectionBinding Projection { get; }

        public CanonicalSemanticProvenance Provenance { get; }

        /// <summary>
        /// 复制并排序 read-set ordinal。
        /// </summary>
        private static ReadOnlyCollection<int> FreezeAndSort(IReadOnlyList<int> values)
        {
            var copy = CanonicalSemanticResourceBudget.SnapshotList(
                values,
                "canonical ordinal list");
            Array.Sort(copy);
            return Array.AsReadOnly(copy);
        }
    }

    /// <summary>
    /// 冻结 program DAG 中一条显式 ordinal 的有向边。
    /// </summary>
    public sealed class CanonicalSemanticProgramEdge
    {
        /// <summary>
        /// 创建有向边；validator 会校验端点、ordinal 与 DAG。
        /// </summary>
        internal CanonicalSemanticProgramEdge(
            int edgeOrdinal,
            int sourceNodeOrdinal,
            int targetNodeOrdinal,
            CanonicalProgramEdgeKind edgeKind,
            CanonicalSemanticProvenance provenance)
        {
            EdgeOrdinal = edgeOrdinal;
            SourceNodeOrdinal = sourceNodeOrdinal;
            TargetNodeOrdinal = targetNodeOrdinal;
            EdgeKind = edgeKind;
            Provenance = provenance;
        }

        public int EdgeOrdinal { get; }

        public int SourceNodeOrdinal { get; }

        public int TargetNodeOrdinal { get; }

        public CanonicalProgramEdgeKind EdgeKind { get; }

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 表示由 definition role 强制生成的 typed program、contextual decisions 与 canonical DAG。
    /// </summary>
    public sealed class CanonicalSemanticProgram
    {
        private readonly ReadOnlyCollection<CanonicalSemanticContextDecision> _decisions;
        private readonly ReadOnlyCollection<CanonicalSemanticProgramNode> _nodes;
        private readonly ReadOnlyCollection<CanonicalSemanticProgramEdge> _edges;

        /// <summary>
        /// 创建 sealed compiler program；Denied program 仍保留完整 decisions 并使 coverage 保持 Red。
        /// </summary>
        internal CanonicalSemanticProgram(
            int programOrdinal,
            string programId,
            CanonicalProgramKind programKind,
            CanonicalFieldDisposition disposition,
            IReadOnlyList<CanonicalSemanticContextDecision> decisions,
            IReadOnlyList<CanonicalSemanticProgramNode> nodes,
            IReadOnlyList<CanonicalSemanticProgramEdge> edges,
            CanonicalSemanticProvenance provenance)
        {
            ProgramOrdinal = programOrdinal;
            ProgramId = programId;
            ProgramKind = programKind;
            Disposition = disposition;
            _decisions = Freeze(decisions, CompareDecisions);
            _nodes = Freeze(nodes, CompareNodes);
            _edges = Freeze(edges, CompareEdges);
            Provenance = provenance;
        }

        public int ProgramOrdinal { get; }

        public string ProgramId { get; }

        public CanonicalProgramKind ProgramKind { get; }

        public CanonicalFieldDisposition Disposition { get; }

        public IReadOnlyList<CanonicalSemanticContextDecision> Decisions => _decisions;

        public IReadOnlyList<CanonicalSemanticProgramNode> Nodes => _nodes;

        public IReadOnlyList<CanonicalSemanticProgramEdge> Edges => _edges;

        public CanonicalSemanticProvenance Provenance { get; }

        /// <summary>
        /// 复制并按显式 ordinal 比较器排序 program 子集合。
        /// </summary>
        private static ReadOnlyCollection<T> Freeze<T>(IReadOnlyList<T> values, Comparison<T> comparison)
        {
            var result = CanonicalSemanticResourceBudget.SnapshotList(
                values,
                "canonical program list");
            Array.Sort(result, comparison);
            return Array.AsReadOnly(result);
        }

        /// <summary>
        /// 比较 decision ordinal。
        /// </summary>
        private static int CompareDecisions(CanonicalSemanticContextDecision left, CanonicalSemanticContextDecision right)
        {
            return left.DecisionOrdinal.CompareTo(right.DecisionOrdinal);
        }

        /// <summary>
        /// 比较 node ordinal。
        /// </summary>
        private static int CompareNodes(CanonicalSemanticProgramNode left, CanonicalSemanticProgramNode right)
        {
            return left.NodeOrdinal.CompareTo(right.NodeOrdinal);
        }

        /// <summary>
        /// 比较 edge ordinal。
        /// </summary>
        private static int CompareEdges(CanonicalSemanticProgramEdge left, CanonicalSemanticProgramEdge right)
        {
            return left.EdgeOrdinal.CompareTo(right.EdgeOrdinal);
        }
    }

    /// <summary>
    /// 表示 compiler 从 typed reference 派生的结构依赖边；完整 work/cleanup/MaxExpansion proof 仍保持 Red。
    /// </summary>
    public sealed class CanonicalSemanticDependency
    {
        /// <summary>
        /// 创建 sealed dependency；target 与分类不可由 source DTO 注入，bound 仍只是 Red 原型值。
        /// </summary>
        internal CanonicalSemanticDependency(
            int dependencyOrdinal,
            string dependencyId,
            int sourceFieldOrdinal,
            CanonicalDependencyKind dependencyKind,
            CanonicalDefinitionKey targetDefinition,
            string targetDomainId,
            CanonicalDependencyOwnerKind ownerKind,
            CanonicalDependencySign sign,
            CanonicalDependencyWorkKind workKind,
            CanonicalCleanupPolicy cleanupPolicy,
            int maxExpansion,
            CanonicalSemanticProvenance provenance)
        {
            DependencyOrdinal = dependencyOrdinal;
            DependencyId = dependencyId;
            SourceFieldOrdinal = sourceFieldOrdinal;
            DependencyKind = dependencyKind;
            TargetDefinition = targetDefinition;
            TargetDomainId = targetDomainId;
            OwnerKind = ownerKind;
            Sign = sign;
            WorkKind = workKind;
            CleanupPolicy = cleanupPolicy;
            MaxExpansion = maxExpansion;
            Provenance = provenance;
        }

        public int DependencyOrdinal { get; }

        public string DependencyId { get; }

        public int SourceFieldOrdinal { get; }

        public CanonicalDependencyKind DependencyKind { get; }

        public CanonicalDefinitionKey TargetDefinition { get; }

        public string TargetDomainId { get; }

        public CanonicalDependencyOwnerKind OwnerKind { get; }

        public CanonicalDependencySign Sign { get; }

        public CanonicalDependencyWorkKind WorkKind { get; }

        public CanonicalCleanupPolicy CleanupPolicy { get; }

        public int MaxExpansion { get; }

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 冻结一个 Definition 的字段树、推导角色、强制 programs 与跨 definition dependencies。
    /// </summary>
    public sealed class CanonicalSemanticDefinition
    {
        private readonly ReadOnlyCollection<CanonicalSemanticField> _fields;
        private readonly ReadOnlyCollection<CanonicalSemanticRole> _roles;
        private readonly ReadOnlyCollection<CanonicalSemanticProgram> _programs;
        private readonly ReadOnlyCollection<CanonicalSemanticDependency> _dependencies;

        /// <summary>
        /// 创建 Definition 并按各自显式 ordinal 固定内部顺序。
        /// </summary>
        internal CanonicalSemanticDefinition(
            int definitionOrdinal,
            CanonicalDefinitionKey key,
            IReadOnlyList<CanonicalSemanticField> fields,
            IReadOnlyList<CanonicalSemanticRole> roles,
            IReadOnlyList<CanonicalSemanticProgram> programs,
            IReadOnlyList<CanonicalSemanticDependency> dependencies,
            CanonicalSemanticProvenance provenance)
        {
            DefinitionOrdinal = definitionOrdinal;
            Key = key;
            _fields = Freeze(fields, CompareFields);
            _roles = Freeze(roles, CompareRoles);
            _programs = Freeze(programs, ComparePrograms);
            _dependencies = Freeze(dependencies, CompareDependencies);
            Provenance = provenance;
        }

        public int DefinitionOrdinal { get; }

        public CanonicalDefinitionKey Key { get; }

        public IReadOnlyList<CanonicalSemanticField> Fields => _fields;

        public IReadOnlyList<CanonicalSemanticRole> Roles => _roles;

        public IReadOnlyList<CanonicalSemanticProgram> Programs => _programs;

        public IReadOnlyList<CanonicalSemanticDependency> Dependencies => _dependencies;

        public CanonicalSemanticProvenance Provenance { get; }

        /// <summary>
        /// 复制并按显式 ordinal 比较器排序 Definition 子集合。
        /// </summary>
        private static ReadOnlyCollection<T> Freeze<T>(IReadOnlyList<T> values, Comparison<T> comparison)
        {
            var result = CanonicalSemanticResourceBudget.SnapshotList(
                values,
                "canonical definition list");
            Array.Sort(result, comparison);
            return Array.AsReadOnly(result);
        }

        /// <summary>
        /// 比较 FieldOrdinal。
        /// </summary>
        private static int CompareFields(CanonicalSemanticField left, CanonicalSemanticField right)
        {
            return left.FieldOrdinal.CompareTo(right.FieldOrdinal);
        }

        /// <summary>
        /// 比较 RoleOrdinal。
        /// </summary>
        private static int CompareRoles(CanonicalSemanticRole left, CanonicalSemanticRole right)
        {
            return left.RoleOrdinal.CompareTo(right.RoleOrdinal);
        }

        /// <summary>
        /// 比较 ProgramOrdinal。
        /// </summary>
        private static int ComparePrograms(CanonicalSemanticProgram left, CanonicalSemanticProgram right)
        {
            return left.ProgramOrdinal.CompareTo(right.ProgramOrdinal);
        }

        /// <summary>
        /// 比较 DependencyOrdinal。
        /// </summary>
        private static int CompareDependencies(CanonicalSemanticDependency left, CanonicalSemanticDependency right)
        {
            return left.DependencyOrdinal.CompareTo(right.DependencyOrdinal);
        }
    }

    /// <summary>
    /// 显式携带尚未闭合的 schema/reference/program/same-parse/eligibility coverage，防止 Red 合同被误装。
    /// </summary>
    public sealed class CanonicalSemanticCoverage
    {
        /// <summary>
        /// 创建 coverage 状态；本轮 validator 强制全部为 Red。
        /// </summary>
        internal CanonicalSemanticCoverage(
            CanonicalSemanticCoverageState schemaCoverage,
            CanonicalSemanticCoverageState referenceCoverage,
            CanonicalSemanticCoverageState programCoverage,
            CanonicalSemanticCoverageState sameParseCoverage,
            CanonicalSemanticCoverageState eligibility)
        {
            SchemaCoverage = schemaCoverage;
            ReferenceCoverage = referenceCoverage;
            ProgramCoverage = programCoverage;
            SameParseCoverage = sameParseCoverage;
            Eligibility = eligibility;
        }

        public CanonicalSemanticCoverageState SchemaCoverage { get; }

        public CanonicalSemanticCoverageState ReferenceCoverage { get; }

        public CanonicalSemanticCoverageState ProgramCoverage { get; }

        public CanonicalSemanticCoverageState SameParseCoverage { get; }

        public CanonicalSemanticCoverageState Eligibility { get; }

        /// <summary>
        /// 创建 A 阶段固定的全 Red coverage。
        /// </summary>
        internal static CanonicalSemanticCoverage CreateRed()
        {
            return new CanonicalSemanticCoverage(
                CanonicalSemanticCoverageState.Red,
                CanonicalSemanticCoverageState.Red,
                CanonicalSemanticCoverageState.Red,
                CanonicalSemanticCoverageState.Red,
                CanonicalSemanticCoverageState.Red);
        }
    }

    /// <summary>
    /// 保存 source identity、完整 semantic tree 与 sealed compile 结果；当前为全 Red 原型，不是 B/Z 冻结交付。
    /// </summary>
    public sealed class CanonicalNormalizedSemanticGraph
    {
        private readonly ReadOnlyCollection<CanonicalSemanticRule> _rules;
        private readonly ReadOnlyCollection<CanonicalSemanticDefinition> _definitions;

        /// <summary>
        /// 创建已完成排序和校验的 immutable graph。
        /// </summary>
        private CanonicalNormalizedSemanticGraph(
            CanonicalSemanticToolchainIdentity toolchainIdentity,
            string generatorVersion,
            string sourceInputHash,
            CanonicalSemanticCoverage coverage,
            IReadOnlyList<CanonicalSemanticRule> rules,
            IReadOnlyList<CanonicalSemanticDefinition> definitions)
        {
            ToolchainIdentity = toolchainIdentity;
            GeneratorVersion = generatorVersion;
            SourceInputHash = sourceInputHash;
            Coverage = coverage;
            _rules = FreezeRules(rules);
            _definitions = FreezeDefinitions(definitions);
        }

        public ushort CodecVersion => CanonicalSemanticVersions.GraphCodecVersion;

        public string GraphSchema => CanonicalSemanticVersions.GraphSchema;

        public string RuleDomain => CanonicalSemanticVersions.RuleDomain;

        public ushort SourceContractVersion => CanonicalSemanticVersions.LubanSourceContractVersion;

        public ushort ResourceBudgetVersion => CanonicalSemanticVersions.ResourceBudgetVersion;

        public string ResourceBudgetId => CanonicalSemanticVersions.ResourceBudgetId;

        public CanonicalSemanticToolchainIdentity ToolchainIdentity { get; }

        public string GeneratorVersion { get; }

        public string SourceInputHash { get; }

        public CanonicalSemanticCoverage Coverage { get; }

        public IReadOnlyList<CanonicalSemanticDomainSchema> DomainSchemas => CanonicalSemanticSchemaRegistry.All;

        public IReadOnlyList<CanonicalSemanticRule> Rules => _rules;

        public IReadOnlyList<CanonicalSemanticDefinition> Definitions => _definitions;

        /// <summary>
        /// 从 sealed compiler 或严格 v2 codec 创建 canonical graph。
        /// </summary>
        internal static CanonicalNormalizedSemanticGraph Create(
            CanonicalSemanticToolchainIdentity toolchainIdentity,
            string generatorVersion,
            string sourceInputHash,
            CanonicalSemanticCoverage coverage,
            IReadOnlyList<CanonicalSemanticRule> rules,
            IReadOnlyList<CanonicalSemanticDefinition> definitions)
        {
            var graph = new CanonicalNormalizedSemanticGraph(
                toolchainIdentity,
                generatorVersion,
                sourceInputHash,
                coverage,
                rules,
                definitions);
            CanonicalSemanticGraphValidator.Validate(graph);
            return graph;
        }

        /// <summary>
        /// 复制并按 RuleOrdinal 排序规则。
        /// </summary>
        private static ReadOnlyCollection<CanonicalSemanticRule> FreezeRules(
            IReadOnlyList<CanonicalSemanticRule> values)
        {
            var result = CanonicalSemanticResourceBudget.SnapshotList(
                values,
                "canonical rules");
            Array.Sort(result, (left, right) => left.RuleOrdinal.CompareTo(right.RuleOrdinal));
            return Array.AsReadOnly(result);
        }

        /// <summary>
        /// 复制并按 DefinitionKey 排序 Definition。
        /// </summary>
        private static ReadOnlyCollection<CanonicalSemanticDefinition> FreezeDefinitions(
            IReadOnlyList<CanonicalSemanticDefinition> values)
        {
            var result = CanonicalSemanticResourceBudget.SnapshotList(
                values,
                "canonical definitions");
            Array.Sort(result, (left, right) => left.Key.CompareTo(right.Key));
            return Array.AsReadOnly(result);
        }
    }
}
