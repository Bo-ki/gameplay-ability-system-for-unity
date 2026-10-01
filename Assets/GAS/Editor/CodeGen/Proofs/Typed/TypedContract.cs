using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GAS.Editor.CodeGen.Semantics;

namespace GAS.Editor.CodeGen.Proofs.Typed
{
    /// <summary>
    /// 冻结字段的 typed shape、完整分类裁决与 governing provenance，不从 normalized 文本回推值。
    /// </summary>
    public sealed class TypedFieldContract
    {
        /// <summary>
        /// 从 canonical field 根节点创建不可变 typed 字段合同。
        /// </summary>
        internal TypedFieldContract(
            int fieldOrdinal,
            string fieldId,
            CanonicalSemanticNodeKind nodeKind,
            string typeId,
            string targetDomainId,
            CanonicalCollectionSemantics collectionSemantics,
            CanonicalFieldClassification classification,
            CanonicalFieldDisposition disposition,
            string ruleId,
            uint ruleVersion,
            CanonicalSemanticProvenance provenance)
        {
            FieldOrdinal = fieldOrdinal;
            FieldId = fieldId;
            NodeKind = nodeKind;
            TypeId = typeId;
            TargetDomainId = targetDomainId;
            CollectionSemantics = collectionSemantics;
            Classification = classification;
            Disposition = disposition;
            RuleId = ruleId;
            RuleVersion = ruleVersion;
            Provenance = provenance;
        }

        public int FieldOrdinal { get; }

        public string FieldId { get; }

        public CanonicalSemanticNodeKind NodeKind { get; }

        public string TypeId { get; }

        public string TargetDomainId { get; }

        public CanonicalCollectionSemantics CollectionSemantics { get; }

        public CanonicalFieldClassification Classification { get; }

        public CanonicalFieldDisposition Disposition { get; }

        public string RuleId { get; }

        public uint RuleVersion { get; }

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 冻结 compiler 推导的 Definition role 与 owner 字段绑定，调用方不能自行注入角色。
    /// </summary>
    public sealed class TypedRoleContract
    {
        /// <summary>
        /// 从 sealed canonical role 创建 typed role 合同。
        /// </summary>
        internal TypedRoleContract(
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
    /// 冻结 ProgramKind 对真实字段的 contextual decision 与字段激活事实。
    /// </summary>
    public sealed class TypedContextDecisionContract
    {
        /// <summary>
        /// 从 fixed matrix 已绑定的 canonical decision 创建 typed decision。
        /// </summary>
        internal TypedContextDecisionContract(
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
    /// 冻结 operation 的 owner、source field、target domain、ValueView、bounds 与 canonical payload。
    /// </summary>
    public sealed class TypedProjectionBindingContract
    {
        private readonly byte[] _payload;

        /// <summary>
        /// 从 sealed compiler projection 创建防御复制后的 typed binding。
        /// </summary>
        internal TypedProjectionBindingContract(
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
            _payload = TypedContractFreeze.CopyBytes(payload);
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

        public byte[] Payload => TypedContractFreeze.CopyBytes(_payload);

        internal byte[] PayloadUnsafe => _payload;

        public string RuleId { get; }

        public uint RuleVersion { get; }

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 冻结 typed operation node 的 read sets、projection binding、RuleId 与 provenance。
    /// </summary>
    public sealed class TypedProgramNodeContract
    {
        private readonly ReadOnlyCollection<int> _fieldOrdinals;
        private readonly ReadOnlyCollection<int> _dependencyOrdinals;

        /// <summary>
        /// 从 sealed canonical node 创建 typed node，不重新解释 read sets。
        /// </summary>
        internal TypedProgramNodeContract(
            int nodeOrdinal,
            string operationId,
            IReadOnlyList<int> fieldOrdinals,
            IReadOnlyList<int> dependencyOrdinals,
            TypedProjectionBindingContract projection,
            string ruleId,
            uint ruleVersion,
            CanonicalSemanticProvenance provenance)
        {
            NodeOrdinal = nodeOrdinal;
            OperationId = operationId;
            _fieldOrdinals = TypedContractFreeze.Copy(fieldOrdinals);
            _dependencyOrdinals = TypedContractFreeze.Copy(dependencyOrdinals);
            Projection = projection;
            RuleId = ruleId;
            RuleVersion = ruleVersion;
            Provenance = provenance;
        }

        public int NodeOrdinal { get; }

        public string OperationId { get; }

        public IReadOnlyList<int> FieldOrdinals => _fieldOrdinals;

        public IReadOnlyList<int> DependencyOrdinals => _dependencyOrdinals;

        public TypedProjectionBindingContract Projection { get; }

        public string RuleId { get; }

        public uint RuleVersion { get; }

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 冻结 typed program DAG 的一条显式 edge 与 governing provenance。
    /// </summary>
    public sealed class TypedProgramEdgeContract
    {
        /// <summary>
        /// 从 canonical edge 创建 typed edge 合同。
        /// </summary>
        internal TypedProgramEdgeContract(
            int edgeOrdinal,
            int sourceNodeOrdinal,
            int targetNodeOrdinal,
            CanonicalProgramEdgeKind edgeKind,
            string ruleId,
            uint ruleVersion,
            CanonicalSemanticProvenance provenance)
        {
            EdgeOrdinal = edgeOrdinal;
            SourceNodeOrdinal = sourceNodeOrdinal;
            TargetNodeOrdinal = targetNodeOrdinal;
            EdgeKind = edgeKind;
            RuleId = ruleId;
            RuleVersion = ruleVersion;
            Provenance = provenance;
        }

        public int EdgeOrdinal { get; }

        public int SourceNodeOrdinal { get; }

        public int TargetNodeOrdinal { get; }

        public CanonicalProgramEdgeKind EdgeKind { get; }

        public string RuleId { get; }

        public uint RuleVersion { get; }

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 冻结一个 role-triggered program 的完整 contextual decisions、projection nodes 与 DAG ranges。
    /// </summary>
    public sealed class TypedProgramContract
    {
        private readonly ReadOnlyCollection<TypedContextDecisionContract> _decisions;
        private readonly ReadOnlyCollection<TypedProgramNodeContract> _nodes;
        private readonly ReadOnlyCollection<TypedProgramEdgeContract> _edges;

        /// <summary>
        /// 从 sealed canonical program 创建完整 typed program 合同。
        /// </summary>
        internal TypedProgramContract(
            int programOrdinal,
            string programId,
            CanonicalProgramKind programKind,
            CanonicalFieldDisposition disposition,
            IReadOnlyList<TypedContextDecisionContract> decisions,
            IReadOnlyList<TypedProgramNodeContract> nodes,
            IReadOnlyList<TypedProgramEdgeContract> edges,
            string ruleId,
            uint ruleVersion,
            CanonicalSemanticProvenance provenance)
        {
            ProgramOrdinal = programOrdinal;
            ProgramId = programId;
            ProgramKind = programKind;
            Disposition = disposition;
            _decisions = TypedContractFreeze.Copy(decisions);
            _nodes = TypedContractFreeze.Copy(nodes);
            _edges = TypedContractFreeze.Copy(edges);
            RuleId = ruleId;
            RuleVersion = ruleVersion;
            Provenance = provenance;
        }

        public int ProgramOrdinal { get; }

        public string ProgramId { get; }

        public CanonicalProgramKind ProgramKind { get; }

        public CanonicalFieldDisposition Disposition { get; }

        public int NodeStartOrdinal => 0;

        public int NodeCount => _nodes.Count;

        public int EdgeStartOrdinal => 0;

        public int EdgeCount => _edges.Count;

        public IReadOnlyList<TypedContextDecisionContract> Decisions => _decisions;

        public IReadOnlyList<TypedProgramNodeContract> Nodes => _nodes;

        public IReadOnlyList<TypedProgramEdgeContract> Edges => _edges;

        public string RuleId { get; }

        public uint RuleVersion { get; }

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 冻结 dependency 的结构元数据；work/cleanup/expansion 完整证明未闭合时只能保持 Red。
    /// </summary>
    public sealed class TypedDependencyContract
    {
        /// <summary>
        /// 从 sealed canonical dependency 创建 typed dependency 合同。
        /// </summary>
        internal TypedDependencyContract(
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
            string ruleId,
            uint ruleVersion,
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
            RuleId = ruleId;
            RuleVersion = ruleVersion;
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

        public string RuleId { get; }

        public uint RuleVersion { get; }

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 冻结单个 Definition 的字段、roles、programs 与 dependency sealed contracts。
    /// </summary>
    public sealed class TypedDefinitionContract
    {
        private readonly ReadOnlyCollection<TypedFieldContract> _fields;
        private readonly ReadOnlyCollection<TypedRoleContract> _roles;
        private readonly ReadOnlyCollection<TypedProgramContract> _programs;
        private readonly ReadOnlyCollection<TypedDependencyContract> _dependencies;

        /// <summary>
        /// 创建 Definition contract 并防御复制全部子集合。
        /// </summary>
        internal TypedDefinitionContract(
            int definitionOrdinal,
            CanonicalDefinitionKey definitionKey,
            IReadOnlyList<TypedFieldContract> fields,
            IReadOnlyList<TypedRoleContract> roles,
            IReadOnlyList<TypedProgramContract> programs,
            IReadOnlyList<TypedDependencyContract> dependencies,
            CanonicalSemanticProvenance provenance)
        {
            DefinitionOrdinal = definitionOrdinal;
            DefinitionKey = definitionKey;
            _fields = TypedContractFreeze.Copy(fields);
            _roles = TypedContractFreeze.Copy(roles);
            _programs = TypedContractFreeze.Copy(programs);
            _dependencies = TypedContractFreeze.Copy(dependencies);
            Provenance = provenance;
        }

        public int DefinitionOrdinal { get; }

        public CanonicalDefinitionKey DefinitionKey { get; }

        public IReadOnlyList<TypedFieldContract> Fields => _fields;

        public IReadOnlyList<TypedRoleContract> Roles => _roles;

        public IReadOnlyList<TypedProgramContract> Programs => _programs;

        public IReadOnlyList<TypedDependencyContract> Dependencies => _dependencies;

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 冻结 schema/reference/program/same-parse/eligibility 五项 Red coverage。
    /// </summary>
    public sealed class TypedSemanticCoverageContract
    {
        /// <summary>
        /// 从 graph coverage 创建不可变 typed coverage。
        /// </summary>
        internal TypedSemanticCoverageContract(
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
    }

    /// <summary>
    /// 绑定 source/toolchain、全 Red coverage、五类身份与 canonical payload；值树只能经强绑定 adapter 读取且当前不可供 B 接纳。
    /// </summary>
    public sealed class TypedContract
    {
        private readonly byte[] _contractMatrixPayload;
        private readonly byte[] _schemaProjectionPayload;
        private readonly byte[] _contentProjectionPayload;
        private readonly ReadOnlyCollection<TypedDefinitionContract> _definitions;

        /// <summary>
        /// 创建完整 TypedContract；TypedContractHash 是 canonical bytes 的 detached hash，不写回自身 payload。
        /// </summary>
        internal TypedContract(
            ushort sourceContractVersion,
            CanonicalSemanticToolchainIdentity toolchainIdentity,
            string generatorVersion,
            string sourceInputHash,
            TypedSemanticCoverageContract coverage,
            string graphHash,
            string registryHash,
            string schemaHash,
            string contentHash,
            string contractMatrixHash,
            string typedContractHash,
            byte[] contractMatrixPayload,
            byte[] schemaProjectionPayload,
            byte[] contentProjectionPayload,
            IReadOnlyList<TypedDefinitionContract> definitions)
        {
            SourceContractVersion = sourceContractVersion;
            ToolchainIdentity = toolchainIdentity;
            GeneratorVersion = generatorVersion;
            SourceInputHash = sourceInputHash;
            Coverage = coverage;
            GraphHash = graphHash;
            RegistryHash = registryHash;
            SchemaHash = schemaHash;
            ContentHash = contentHash;
            ContractMatrixHash = contractMatrixHash;
            TypedContractHash = typedContractHash;
            _contractMatrixPayload = TypedContractFreeze.CopyBytes(contractMatrixPayload);
            _schemaProjectionPayload = TypedContractFreeze.CopyBytes(schemaProjectionPayload);
            _contentProjectionPayload = TypedContractFreeze.CopyBytes(contentProjectionPayload);
            _definitions = TypedContractFreeze.Copy(definitions);
        }

        public ushort CodecVersion => CanonicalSemanticVersions.TypedContractCodecVersion;

        public string ContractSchema => CanonicalSemanticVersions.TypedContractSchema;

        public string ContractAbi => CanonicalSemanticVersions.TypedContractAbi;

        public string RuleDomain => CanonicalSemanticVersions.RuleDomain;

        public ushort ResourceBudgetVersion => CanonicalSemanticVersions.ResourceBudgetVersion;

        public string ResourceBudgetId => CanonicalSemanticVersions.ResourceBudgetId;

        public ushort SourceContractVersion { get; }

        public CanonicalSemanticToolchainIdentity ToolchainIdentity { get; }

        public string GeneratorVersion { get; }

        public string SourceInputHash { get; }

        public TypedSemanticCoverageContract Coverage { get; }

        public string GraphHash { get; }

        public string RegistryHash { get; }

        public string SchemaHash { get; }

        public string ContentHash { get; }

        public string ContractMatrixHash { get; }

        public string TypedContractHash { get; }

        public byte[] ContractMatrixPayload => TypedContractFreeze.CopyBytes(_contractMatrixPayload);

        internal byte[] ContractMatrixPayloadUnsafe => _contractMatrixPayload;

        public byte[] SchemaProjectionPayload => TypedContractFreeze.CopyBytes(_schemaProjectionPayload);

        internal byte[] SchemaProjectionPayloadUnsafe => _schemaProjectionPayload;

        public byte[] ContentProjectionPayload => TypedContractFreeze.CopyBytes(_contentProjectionPayload);

        internal byte[] ContentProjectionPayloadUnsafe => _contentProjectionPayload;

        public IReadOnlyList<TypedDefinitionContract> Definitions => _definitions;
    }

    /// <summary>
    /// 集中执行 TypedContract DTO 的防御复制并保留已验证的 canonical 顺序。
    /// </summary>
    internal static class TypedContractFreeze
    {
        /// <summary>
        /// 复制任意只读列表并保留输入顺序。
        /// </summary>
        public static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> values)
        {
            var copy = CanonicalSemanticResourceBudget.SnapshotList(
                values,
                "typed freeze copy");
            return Array.AsReadOnly(copy);
        }

        /// <summary>
        /// 复制 byte payload，null 仅在内部 provisional contract 中视为空数组。
        /// </summary>
        public static byte[] CopyBytes(byte[] values)
        {
            var source = values ?? Array.Empty<byte>();
            CanonicalSemanticResourceBudget.RequirePayloadLength(source.Length, "typed freeze payload");
            return source.Length == 0 ? Array.Empty<byte>() : (byte[])source.Clone();
        }
    }
}
