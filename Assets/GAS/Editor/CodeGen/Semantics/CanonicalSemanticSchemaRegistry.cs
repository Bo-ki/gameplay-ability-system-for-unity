using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GAS.Editor.CodeGen.Semantics
{
    /// <summary>
    /// 冻结 semantic tree 一个节点的结构、presence、集合等价关系、目标域与字段分类。
    /// </summary>
    public sealed class CanonicalSemanticNodeSchema
    {
        private readonly ReadOnlyCollection<CanonicalSemanticNodeSchema> _children;
        private readonly ReadOnlyCollection<CanonicalSemanticVariantSchema> _variants;

        /// <summary>
        /// 创建由 registry 独占维护的节点 schema。
        /// </summary>
        internal CanonicalSemanticNodeSchema(
            int nodeOrdinal,
            string nodeId,
            CanonicalSemanticNodeKind nodeKind,
            string typeId,
            string targetDomainId,
            CanonicalSemanticValueKind scalarKind,
            CanonicalCollectionSemantics collectionSemantics,
            CanonicalPresencePolicy presencePolicy,
            CanonicalFieldClassification classification,
            CanonicalFieldDisposition disposition,
            string ruleId,
            IReadOnlyList<CanonicalSemanticNodeSchema> children,
            IReadOnlyList<CanonicalSemanticVariantSchema> variants)
        {
            NodeOrdinal = nodeOrdinal;
            NodeId = nodeId;
            NodeKind = nodeKind;
            TypeId = typeId;
            TargetDomainId = targetDomainId;
            ScalarKind = scalarKind;
            EnumUnderlyingType = scalarKind == CanonicalSemanticValueKind.Enum
                ? CanonicalEnumUnderlyingType.Int32
                : 0;
            CollectionSemantics = collectionSemantics;
            PresencePolicy = presencePolicy;
            Classification = classification;
            Disposition = disposition;
            RuleId = ruleId;
            CanonicalSemanticRuleCatalog.TryResolve(ruleId, out var version, out _);
            RuleVersion = version;
            _children = Copy(children);
            _variants = Copy(variants);
        }

        public int NodeOrdinal { get; }

        public string NodeId { get; }

        public CanonicalSemanticNodeKind NodeKind { get; }

        public string TypeId { get; }

        public string TargetDomainId { get; }

        public CanonicalSemanticValueKind ScalarKind { get; }

        public CanonicalEnumUnderlyingType EnumUnderlyingType { get; }

        public CanonicalCollectionSemantics CollectionSemantics { get; }

        public CanonicalPresencePolicy PresencePolicy { get; }

        public CanonicalFieldClassification Classification { get; }

        public CanonicalFieldDisposition Disposition { get; }

        public string RuleId { get; }

        public uint RuleVersion { get; }

        public IReadOnlyList<CanonicalSemanticNodeSchema> Children => _children;

        public IReadOnlyList<CanonicalSemanticVariantSchema> Variants => _variants;

        /// <summary>
        /// 复制 schema 并只替换当前 scope 的显式 ordinal。
        /// </summary>
        internal CanonicalSemanticNodeSchema WithOrdinal(int ordinal)
        {
            return new CanonicalSemanticNodeSchema(
                ordinal,
                NodeId,
                NodeKind,
                TypeId,
                TargetDomainId,
                ScalarKind,
                CollectionSemantics,
                PresencePolicy,
                Classification,
                Disposition,
                RuleId,
                _children,
                _variants);
        }

        /// <summary>
        /// 防御复制任意 schema DTO 集合。
        /// </summary>
        private static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> values)
        {
            var copy = CanonicalSemanticResourceBudget.SnapshotList(
                values,
                "node schema collection");
            return Array.AsReadOnly(copy);
        }
    }

    /// <summary>
    /// 冻结多态 discriminator 与其完整 payload 成员 schema。
    /// </summary>
    public sealed class CanonicalSemanticVariantSchema
    {
        private readonly ReadOnlyCollection<CanonicalSemanticNodeSchema> _members;

        /// <summary>
        /// 创建一个稳定 variant type id 与 payload 成员表。
        /// </summary>
        internal CanonicalSemanticVariantSchema(
            int variantOrdinal,
            string variantTypeId,
            IReadOnlyList<CanonicalSemanticNodeSchema> members)
        {
            VariantOrdinal = variantOrdinal;
            VariantTypeId = variantTypeId;
            var source = CanonicalSemanticResourceBudget.SnapshotList(
                members,
                "variant schema members");
            var copy = new CanonicalSemanticNodeSchema[source.Length];
            for (var index = 0; index < source.Length; index++)
                copy[index] = source[index].WithOrdinal(index);
            _members = Array.AsReadOnly(copy);
        }

        public int VariantOrdinal { get; }

        public string VariantTypeId { get; }

        public IReadOnlyList<CanonicalSemanticNodeSchema> Members => _members;
    }

    /// <summary>
    /// 冻结一个真实 Luban table 字段的 ordinal 与完整根节点 schema。
    /// </summary>
    public sealed class CanonicalSemanticFieldSchema
    {
        /// <summary>
        /// 创建由 domain registry 独占维护的字段 schema。
        /// </summary>
        internal CanonicalSemanticFieldSchema(int fieldOrdinal, CanonicalSemanticNodeSchema root)
        {
            FieldOrdinal = fieldOrdinal;
            FieldId = root.NodeId;
            Root = root.WithOrdinal(0);
        }

        public int FieldOrdinal { get; }

        public string FieldId { get; }

        public CanonicalSemanticNodeSchema Root { get; }

        public CanonicalFieldClassification Classification => Root.Classification;

        public CanonicalFieldDisposition Disposition => Root.Disposition;

        public string RuleId => Root.RuleId;

        public uint RuleVersion => Root.RuleVersion;
    }

    /// <summary>
    /// 冻结 ProgramKind 对一个真实字段的上下文裁决，字段即使未激活也不得从矩阵省略。
    /// </summary>
    public sealed class CanonicalSemanticFieldSupportDecision
    {
        /// <summary>
        /// 创建固定 ordinal 的逐字段上下文裁决。
        /// </summary>
        internal CanonicalSemanticFieldSupportDecision(
            int decisionOrdinal,
            int fieldOrdinal,
            string fieldId,
            CanonicalFieldClassification classification,
            CanonicalFieldDisposition disposition,
            string ruleId)
        {
            DecisionOrdinal = decisionOrdinal;
            FieldOrdinal = fieldOrdinal;
            FieldId = fieldId;
            Classification = classification;
            Disposition = disposition;
            RuleId = ruleId;
            CanonicalSemanticRuleCatalog.TryResolve(ruleId, out var version, out _);
            RuleVersion = version;
        }

        public int DecisionOrdinal { get; }

        public int FieldOrdinal { get; }

        public string FieldId { get; }

        public CanonicalFieldClassification Classification { get; }

        public CanonicalFieldDisposition Disposition { get; }

        public string RuleId { get; }

        public uint RuleVersion { get; }
    }

    /// <summary>
    /// 冻结 compiler 必须生成的 operation 顺序、read fields、ValueView、phase 与 bound 来源。
    /// </summary>
    public sealed class CanonicalSemanticOperationSchema
    {
        private readonly ReadOnlyCollection<string> _fieldIds;

        /// <summary>
        /// 创建不可由 source 调用方注入的 operation 模板。
        /// </summary>
        internal CanonicalSemanticOperationSchema(
            int operationOrdinal,
            string operationId,
            string valueView,
            string phase,
            string targetDomainId,
            string boundFieldId,
            int payloadBytesPerItem,
            IReadOnlyList<string> fieldIds,
            string ruleId)
        {
            OperationOrdinal = operationOrdinal;
            OperationId = operationId;
            ValueView = valueView;
            Phase = phase;
            TargetDomainId = targetDomainId;
            BoundFieldId = boundFieldId;
            PayloadBytesPerItem = payloadBytesPerItem;
            RuleId = ruleId;
            CanonicalSemanticRuleCatalog.TryResolve(ruleId, out var version, out _);
            RuleVersion = version;
            var copy = CanonicalSemanticResourceBudget.SnapshotList(
                fieldIds,
                "operation field ids");
            _fieldIds = Array.AsReadOnly(copy);
        }

        public int OperationOrdinal { get; }

        public string OperationId { get; }

        public string ValueView { get; }

        public string Phase { get; }

        public string TargetDomainId { get; }

        public string BoundFieldId { get; }

        public int PayloadBytesPerItem { get; }

        public IReadOnlyList<string> FieldIds => _fieldIds;

        public string RuleId { get; }

        public uint RuleVersion { get; }
    }

    /// <summary>
    /// 冻结一个 domain 内 ProgramKind 的准入、强制 operations 与完整逐字段决策。
    /// </summary>
    public sealed class CanonicalSemanticProgramSchema
    {
        private readonly ReadOnlyCollection<CanonicalSemanticOperationSchema> _operations;
        private readonly ReadOnlyCollection<CanonicalSemanticFieldSupportDecision> _fieldDecisions;

        /// <summary>
        /// 创建 fixed support matrix 的一个 program 项。
        /// </summary>
        internal CanonicalSemanticProgramSchema(
            int programSchemaOrdinal,
            CanonicalProgramKind programKind,
            CanonicalFieldDisposition disposition,
            string ruleId,
            IReadOnlyList<CanonicalSemanticOperationSchema> operations,
            IReadOnlyList<CanonicalSemanticFieldSupportDecision> fieldDecisions)
        {
            ProgramSchemaOrdinal = programSchemaOrdinal;
            ProgramKind = programKind;
            Disposition = disposition;
            RuleId = ruleId;
            CanonicalSemanticRuleCatalog.TryResolve(ruleId, out var version, out _);
            RuleVersion = version;
            _operations = Copy(operations);
            _fieldDecisions = Copy(fieldDecisions);
        }

        public int ProgramSchemaOrdinal { get; }

        public CanonicalProgramKind ProgramKind { get; }

        public CanonicalFieldDisposition Disposition { get; }

        public string RuleId { get; }

        public uint RuleVersion { get; }

        public IReadOnlyList<CanonicalSemanticOperationSchema> Operations => _operations;

        public IReadOnlyList<CanonicalSemanticFieldSupportDecision> FieldDecisions => _fieldDecisions;

        /// <summary>
        /// 防御复制 program schema 的子集合。
        /// </summary>
        private static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> values)
        {
            var copy = CanonicalSemanticResourceBudget.SnapshotList(
                values,
                "program schema collection");
            return Array.AsReadOnly(copy);
        }
    }

    /// <summary>
    /// 冻结一个 domain 的 ordinal、kind、key arity、字段树与 program 矩阵。
    /// </summary>
    public sealed class CanonicalSemanticDomainSchema
    {
        private readonly ReadOnlyCollection<CanonicalSemanticFieldSchema> _fields;
        private readonly ReadOnlyCollection<CanonicalSemanticProgramSchema> _programs;

        /// <summary>
        /// 创建一个固定 domain schema。
        /// </summary>
        internal CanonicalSemanticDomainSchema(
            int domainOrdinal,
            string stableDomainId,
            CanonicalDefinitionKind definitionKind,
            int stableIdPartCount,
            IReadOnlyList<CanonicalSemanticFieldSchema> fields,
            IReadOnlyList<CanonicalSemanticProgramSchema> programs)
        {
            DomainOrdinal = domainOrdinal;
            StableDomainId = stableDomainId;
            DefinitionKind = definitionKind;
            StableIdPartCount = stableIdPartCount;
            _fields = Copy(fields);
            _programs = Copy(programs);
        }

        public int DomainOrdinal { get; }

        public string StableDomainId { get; }

        public CanonicalDefinitionKind DefinitionKind { get; }

        public int StableIdPartCount { get; }

        public IReadOnlyList<CanonicalSemanticFieldSchema> Fields => _fields;

        public IReadOnlyList<CanonicalSemanticProgramSchema> Programs => _programs;

        /// <summary>
        /// 按 FieldId 解析真实字段 schema。
        /// </summary>
        public bool TryResolveField(string fieldId, out CanonicalSemanticFieldSchema schema)
        {
            for (var index = 0; index < _fields.Count; index++)
            {
                if (string.Equals(_fields[index].FieldId, fieldId, StringComparison.Ordinal))
                {
                    schema = _fields[index];
                    return true;
                }
            }

            schema = null;
            return false;
        }

        /// <summary>
        /// 按 ProgramKind 解析固定上下文矩阵项。
        /// </summary>
        public bool TryResolveProgram(CanonicalProgramKind kind, out CanonicalSemanticProgramSchema schema)
        {
            for (var index = 0; index < _programs.Count; index++)
            {
                if (_programs[index].ProgramKind == kind)
                {
                    schema = _programs[index];
                    return true;
                }
            }

            schema = null;
            return false;
        }

        /// <summary>
        /// 防御复制 domain schema 的子集合。
        /// </summary>
        private static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> values)
        {
            var copy = CanonicalSemanticResourceBudget.SnapshotList(
                values,
                "domain schema collection");
            return Array.AsReadOnly(copy);
        }
    }

    /// <summary>
    /// 作为 A/Z 唯一 v2 schema 与 support matrix registry，完整覆盖真实 Ability 与 GameplayEffect 字段。
    /// </summary>
    public static class CanonicalSemanticSchemaRegistry
    {
        public const string AbilityDomain = "exgas.ability";
        public const string GameplayEffectDomain = "exgas.gameplayEffect";
        public const string AttributeSetDomain = "exgas.attributeSet";
        public const string AttributeDomain = "exgas.attribute";
        public const string GameplayTagDomain = "exgas.gameplayTag";
        public const string GameplayCueDomain = "exgas.gameplayCue";
        public const string TimelineDomain = "exgas.timeline";

        private static readonly ReadOnlyCollection<CanonicalSemanticDomainSchema> Domains =
            Array.AsReadOnly(BuildDomains());

        public static IReadOnlyList<CanonicalSemanticDomainSchema> All => Domains;

        /// <summary>
        /// 按 DefinitionKind 解析固定 domain schema。
        /// </summary>
        public static bool TryResolve(
            CanonicalDefinitionKind definitionKind,
            out CanonicalSemanticDomainSchema schema)
        {
            for (var index = 0; index < Domains.Count; index++)
            {
                if (Domains[index].DefinitionKind == definitionKind)
                {
                    schema = Domains[index];
                    return true;
                }
            }

            schema = null;
            return false;
        }

        /// <summary>
        /// 按稳定 domain id 解析固定 domain schema。
        /// </summary>
        public static bool TryResolve(string stableDomainId, out CanonicalSemanticDomainSchema schema)
        {
            for (var index = 0; index < Domains.Count; index++)
            {
                if (string.Equals(Domains[index].StableDomainId, stableDomainId, StringComparison.Ordinal))
                {
                    schema = Domains[index];
                    return true;
                }
            }

            schema = null;
            return false;
        }

        /// <summary>
        /// 建立七个 domain；Ability/GameplayEffect 使用真实 Luban schema，其余 domain 保持 coverage Red。
        /// </summary>
        private static CanonicalSemanticDomainSchema[] BuildDomains()
        {
            return new[]
            {
                Domain(1, AbilityDomain, CanonicalDefinitionKind.Ability, 1, BuildAbilityFields()),
                Domain(2, GameplayEffectDomain, CanonicalDefinitionKind.GameplayEffect, 1, BuildGameplayEffectFields()),
                Domain(3, AttributeSetDomain, CanonicalDefinitionKind.AttributeSet, 1,
                    Scalar("AttributeSetCode", CanonicalSemanticValueKind.Int32)),
                Domain(4, AttributeDomain, CanonicalDefinitionKind.Attribute, 2,
                    Scalar("AttributeSetCode", CanonicalSemanticValueKind.Int32),
                    Scalar("AttributeCode", CanonicalSemanticValueKind.Int32),
                    Scalar("InitialValue", CanonicalSemanticValueKind.Float32),
                    Scalar("IsClampMin", CanonicalSemanticValueKind.Boolean),
                    Scalar("IsClampMax", CanonicalSemanticValueKind.Boolean),
                    Scalar("MinValue", CanonicalSemanticValueKind.Float32),
                    Scalar("MaxValue", CanonicalSemanticValueKind.Float32)),
                Domain(5, GameplayTagDomain, CanonicalDefinitionKind.GameplayTag, 1,
                    Scalar("GameplayTagCode", CanonicalSemanticValueKind.Int32),
                    References("ParentCodes", GameplayTagDomain, CanonicalDefinitionKind.GameplayTag, CanonicalCollectionSemantics.Set),
                    References("ChildCodes", GameplayTagDomain, CanonicalDefinitionKind.GameplayTag, CanonicalCollectionSemantics.Set)),
                Domain(6, GameplayCueDomain, CanonicalDefinitionKind.GameplayCue, 1,
                    Scalar("GameplayCueCode", CanonicalSemanticValueKind.Int32),
                    Scalar("PresentationKey", CanonicalSemanticValueKind.String)),
                Domain(7, TimelineDomain, CanonicalDefinitionKind.Timeline, 1,
                    Scalar("TimelineId", CanonicalSemanticValueKind.Int32),
                    Reference("GameplayEffectCode", GameplayEffectDomain, CanonicalDefinitionKind.GameplayEffect, CanonicalPresencePolicy.Optional),
                    Reference("SecondaryGameplayEffectCode", GameplayEffectDomain, CanonicalDefinitionKind.GameplayEffect, CanonicalPresencePolicy.Optional),
                    Scalar("TargetCatcherName", CanonicalSemanticValueKind.String)),
            };
        }

        /// <summary>
        /// 建立真实 Ability 的 14 个顶层字段，保留 optional、tag set、多态 execution 与 target policy。
        /// </summary>
        private static CanonicalSemanticNodeSchema[] BuildAbilityFields()
        {
            return new[]
            {
                Scalar("ID", CanonicalSemanticValueKind.Int32),
                Scalar("Name", CanonicalSemanticValueKind.String, CanonicalFieldClassification.ProvenanceOnly),
                Scalar("Desc", CanonicalSemanticValueKind.String, CanonicalFieldClassification.ProvenanceOnly),
                Reference("Cost", GameplayEffectDomain, CanonicalDefinitionKind.GameplayEffect, CanonicalPresencePolicy.Optional),
                Reference("CdEffect", GameplayEffectDomain, CanonicalDefinitionKind.GameplayEffect, CanonicalPresencePolicy.Optional),
                Scalar("Cd", CanonicalSemanticValueKind.Int32),
                References("AssetTags", GameplayTagDomain, CanonicalDefinitionKind.GameplayTag, CanonicalCollectionSemantics.Set),
                TagRequirement("CancelAbilityWithTags"),
                TagRequirement("BlockAbilityWithTags"),
                References("ActivationOwnedTags", GameplayTagDomain, CanonicalDefinitionKind.GameplayTag, CanonicalCollectionSemantics.Set),
                TagRequirement("ActivationRequiredTags"),
                TagRequirement("ActivationBlockedTags"),
                AbilityExecution(),
                TargetPolicy("RuntimeV1TargetPolicy"),
            };
        }

        /// <summary>
        /// 建立真实 GameplayEffect 的 22 个顶层字段，禁止 scalar mirror 或 FirstPositive 替代完整结构。
        /// </summary>
        private static CanonicalSemanticNodeSchema[] BuildGameplayEffectFields()
        {
            return new[]
            {
                Scalar("ID", CanonicalSemanticValueKind.Int32),
                Scalar("Name", CanonicalSemanticValueKind.String, CanonicalFieldClassification.ProvenanceOnly),
                Scalar("Desc", CanonicalSemanticValueKind.String, CanonicalFieldClassification.ProvenanceOnly),
                References("AssetTags", GameplayTagDomain, CanonicalDefinitionKind.GameplayTag, CanonicalCollectionSemantics.Set),
                References("GrantedTags", GameplayTagDomain, CanonicalDefinitionKind.GameplayTag, CanonicalCollectionSemantics.Set),
                TagRequirement("ApplicationRequiredTags"),
                TagRequirement("OngoingRequiredTags"),
                TagRequirement("RemoveGameplayEffectsWithTags"),
                TagRequirement("ImmunityTags"),
                Duration(),
                Period(),
                Modifiers(),
                CueSet("CueOnApply"),
                CueSet("CueOnTick"),
                CueSet("CueOnAdd"),
                CueSet("CueOnRemove"),
                CueSet("CueOnActivate"),
                CueSet("CueOnDeactivate"),
                GrantedAbilities(),
                Stacking(),
                TargetPolicy("RuntimeV1TargetPolicy"),
                Evaluator(),
            };
        }

        /// <summary>
        /// 创建 domain 并分配 FieldOrdinal、完整 program matrix 与 default-deny 决策。
        /// </summary>
        private static CanonicalSemanticDomainSchema Domain(
            int ordinal,
            string domainId,
            CanonicalDefinitionKind kind,
            int keyParts,
            params CanonicalSemanticNodeSchema[] roots)
        {
            var fields = new CanonicalSemanticFieldSchema[roots.Length];
            for (var index = 0; index < roots.Length; index++)
                fields[index] = new CanonicalSemanticFieldSchema(index, roots[index]);
            return new CanonicalSemanticDomainSchema(
                ordinal,
                domainId,
                kind,
                keyParts,
                fields,
                BuildProgramMatrix(kind, fields));
        }

        /// <summary>
    /// 建立覆盖六个 ProgramKind 的固定矩阵；Cost/Cooldown 未冻结子字段仍会显式 Red。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticProgramSchema> BuildProgramMatrix(
            CanonicalDefinitionKind kind,
            IReadOnlyList<CanonicalSemanticFieldSchema> fields)
        {
            var result = new CanonicalSemanticProgramSchema[6];
            for (var index = 0; index < result.Length; index++)
            {
                var programKind = (CanonicalProgramKind)(index + 1);
                var supported = kind == CanonicalDefinitionKind.GameplayEffect
                                && (programKind == CanonicalProgramKind.CostMutation
                                    || programKind == CanonicalProgramKind.CooldownGate);
                result[index] = new CanonicalSemanticProgramSchema(
                    index,
                    programKind,
                    supported ? CanonicalFieldDisposition.Allowed : CanonicalFieldDisposition.Denied,
                    supported
                        ? CanonicalSemanticRuleCatalog.UnsupportedExecutionProjection
                        : CanonicalSemanticRuleCatalog.UnsupportedContractField,
                    supported ? BuildOperations(programKind) : Array.Empty<CanonicalSemanticOperationSchema>(),
                    BuildDecisions(programKind, supported, fields));
            }

            return Array.AsReadOnly(result);
        }

        /// <summary>
        /// 建立 Cost/Cooldown 必须出现的 operation 顺序、read fields、ValueView 与 bound 规则。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticOperationSchema> BuildOperations(CanonicalProgramKind kind)
        {
            if (kind == CanonicalProgramKind.CostMutation)
            {
                return Array.AsReadOnly(new[]
                {
                    Operation(0, "Cost.ReadOwnerCommitPrecondition", "OwnerCommitShadow", "OwnerCommit",
                        string.Empty, string.Empty, 16, "ApplicationRequiredTags"),
                    Operation(1, "Cost.RejectUnnormalizedModifier", "OwnerCommitShadow", "OwnerCommit",
                        string.Empty, string.Empty, 1, "Modifiers"),
                });
            }

            return Array.AsReadOnly(new[]
            {
                Operation(0, "Cooldown.ReadOwnerCommitPrecondition", "OwnerCommitShadow", "OwnerCommit",
                    string.Empty, string.Empty, 16, "ApplicationRequiredTags"),
                Operation(1, "Cooldown.RejectUnfrozenDuration", "OwnerCommitShadow", "OwnerCommit",
                    string.Empty, string.Empty, 1, "Duration"),
            });
        }

        /// <summary>
        /// 创建一个固定 operation 模板。
        /// </summary>
        private static CanonicalSemanticOperationSchema Operation(
            int ordinal,
            string id,
            string valueView,
            string phase,
            string targetDomain,
            string boundField,
            int bytesPerItem,
            params string[] fieldIds)
        {
            return new CanonicalSemanticOperationSchema(
                ordinal,
                id,
                valueView,
                phase,
                targetDomain,
                boundField,
                bytesPerItem,
                fieldIds,
                CanonicalSemanticRuleCatalog.MissingValueViewOrPhase);
        }

        /// <summary>
        /// 为 ProgramKind 对 domain 每个真实字段生成一条不可省略的裁决。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticFieldSupportDecision> BuildDecisions(
            CanonicalProgramKind kind,
            bool supported,
            IReadOnlyList<CanonicalSemanticFieldSchema> fields)
        {
            var result = new CanonicalSemanticFieldSupportDecision[fields.Count];
            for (var index = 0; index < fields.Count; index++)
            {
                ResolveDecision(kind, supported, fields[index], out var disposition, out var ruleId);
                result[index] = new CanonicalSemanticFieldSupportDecision(
                    index,
                    fields[index].FieldOrdinal,
                    fields[index].FieldId,
                    fields[index].Classification,
                    disposition,
                    ruleId);
            }

            return Array.AsReadOnly(result);
        }

        /// <summary>
        /// 解析 Cost/Cooldown 的逐字段上下文裁决，任何未列字段稳定 CFG1101 deny。
        /// </summary>
        private static void ResolveDecision(
            CanonicalProgramKind kind,
            bool supported,
            CanonicalSemanticFieldSchema field,
            out CanonicalFieldDisposition disposition,
            out string ruleId)
        {
            if (field.Classification == CanonicalFieldClassification.ProvenanceOnly)
            {
                disposition = CanonicalFieldDisposition.Allowed;
                ruleId = CanonicalSemanticRuleCatalog.InvalidDomainOrReference;
                return;
            }

            disposition = CanonicalFieldDisposition.Denied;
            ruleId = CanonicalSemanticRuleCatalog.UnsupportedContractField;
            if (!supported)
                return;
            if (kind == CanonicalProgramKind.CostMutation)
                ResolveCostDecision(field.FieldId, out disposition, out ruleId);
            else
                ResolveCooldownDecision(field.FieldId, out disposition, out ruleId);
        }

        /// <summary>
        /// 冻结 CostMutation 的完整真实字段裁决。
        /// </summary>
        private static void ResolveCostDecision(
            string fieldId,
            out CanonicalFieldDisposition disposition,
            out string ruleId)
        {
            disposition = CanonicalFieldDisposition.Denied;
            ruleId = CanonicalSemanticRuleCatalog.UnsupportedContractField;
            if (fieldId == "ID")
            {
                disposition = CanonicalFieldDisposition.Allowed;
                ruleId = CanonicalSemanticRuleCatalog.InvalidDomainOrReference;
            }
            else if (fieldId == "ApplicationRequiredTags")
            {
                disposition = CanonicalFieldDisposition.RequiredProjection;
                ruleId = CanonicalSemanticRuleCatalog.MissingValueViewOrPhase;
            }
            else if (fieldId == "Modifiers")
                ruleId = CanonicalSemanticRuleCatalog.UnsupportedContractField;
            else if (fieldId == "GrantedTags")
                ruleId = CanonicalSemanticRuleCatalog.InvalidOwnedContribution;
            else if (fieldId == "Period" || fieldId == "Stacking")
                ruleId = CanonicalSemanticRuleCatalog.DependencyOrCleanupUnbounded;
            else if (fieldId == "RuntimeV1Evaluator")
                ruleId = CanonicalSemanticRuleCatalog.UnsupportedExecutionProjection;
        }

        /// <summary>
        /// 冻结 CooldownGate 的完整真实字段裁决。
        /// </summary>
        private static void ResolveCooldownDecision(
            string fieldId,
            out CanonicalFieldDisposition disposition,
            out string ruleId)
        {
            disposition = CanonicalFieldDisposition.Denied;
            ruleId = CanonicalSemanticRuleCatalog.UnsupportedContractField;
            if (fieldId == "ID")
            {
                disposition = CanonicalFieldDisposition.Allowed;
                ruleId = CanonicalSemanticRuleCatalog.InvalidDomainOrReference;
            }
            else if (fieldId == "ApplicationRequiredTags")
            {
                disposition = CanonicalFieldDisposition.RequiredProjection;
                ruleId = CanonicalSemanticRuleCatalog.MissingValueViewOrPhase;
            }
            else if (fieldId == "Duration" || fieldId == "GrantedTags")
                ruleId = CanonicalSemanticRuleCatalog.UnsupportedContractField;
            else if (fieldId == "Period" || fieldId == "Stacking")
                ruleId = CanonicalSemanticRuleCatalog.DependencyOrCleanupUnbounded;
            else if (fieldId == "RuntimeV1Evaluator")
                ruleId = CanonicalSemanticRuleCatalog.UnsupportedExecutionProjection;
        }

        /// <summary>
        /// 创建 required primitive scalar schema。
        /// </summary>
        private static CanonicalSemanticNodeSchema Scalar(
            string id,
            CanonicalSemanticValueKind kind,
            CanonicalFieldClassification classification = CanonicalFieldClassification.Gameplay,
            CanonicalPresencePolicy presence = CanonicalPresencePolicy.Required,
            string typeId = "")
        {
            return Node(id, CanonicalSemanticNodeKind.Scalar, typeId.Length == 0 ? kind.ToString() : typeId,
                string.Empty, kind, CanonicalCollectionSemantics.None, presence, classification, null, null);
        }

        /// <summary>
        /// 创建显式 target domain 的 scalar reference schema。
        /// </summary>
        private static CanonicalSemanticNodeSchema Reference(
            string id,
            string targetDomain,
            CanonicalDefinitionKind targetKind,
            CanonicalPresencePolicy presence)
        {
            return Node(id, CanonicalSemanticNodeKind.Scalar, targetKind + "Reference", targetDomain,
                CanonicalSemanticValueKind.DefinitionReference, CanonicalCollectionSemantics.None,
                presence, CanonicalFieldClassification.Gameplay, null, null);
        }

        /// <summary>
        /// 创建完整 target-domain reference collection schema。
        /// </summary>
        private static CanonicalSemanticNodeSchema References(
            string id,
            string targetDomain,
            CanonicalDefinitionKind targetKind,
            CanonicalCollectionSemantics semantics)
        {
            var element = Reference("$element", targetDomain, targetKind, CanonicalPresencePolicy.Required);
            return Node(id, CanonicalSemanticNodeKind.Collection, targetKind + "References", targetDomain,
                CanonicalSemanticValueKind.Null, semantics, CanonicalPresencePolicy.Required,
                CanonicalFieldClassification.Gameplay, new[] { element }, null);
        }

        /// <summary>
        /// 创建 optional TagRequirementSpec，All/Any/None 均为完整 GameplayTag set。
        /// </summary>
        private static CanonicalSemanticNodeSchema TagRequirement(string id)
        {
            return Node(id, CanonicalSemanticNodeKind.Record, "exgas.TagRequirementSpec", string.Empty,
                CanonicalSemanticValueKind.Null, CanonicalCollectionSemantics.None, CanonicalPresencePolicy.Optional,
                CanonicalFieldClassification.Gameplay,
                new[]
                {
                    References("All", GameplayTagDomain, CanonicalDefinitionKind.GameplayTag, CanonicalCollectionSemantics.Set),
                    References("Any", GameplayTagDomain, CanonicalDefinitionKind.GameplayTag, CanonicalCollectionSemantics.Set),
                    References("None", GameplayTagDomain, CanonicalDefinitionKind.GameplayTag, CanonicalCollectionSemantics.Set),
                }, null);
        }

        /// <summary>
        /// 创建 AbilityExecutionBase 的两个真实 variant 与完整 Param payload。
        /// </summary>
        private static CanonicalSemanticNodeSchema AbilityExecution()
        {
            var applyParam = Node("Param", CanonicalSemanticNodeKind.Record, "exgas.XParamEffectIDs", string.Empty,
                CanonicalSemanticValueKind.Null, CanonicalCollectionSemantics.None, CanonicalPresencePolicy.Required,
                CanonicalFieldClassification.Gameplay,
                new[] { References("IDs", GameplayEffectDomain, CanonicalDefinitionKind.GameplayEffect, CanonicalCollectionSemantics.Ordered) }, null);
            var timelineParam = Node("Param", CanonicalSemanticNodeKind.Record, "exgas.XParamTimelineID", string.Empty,
                CanonicalSemanticValueKind.Null, CanonicalCollectionSemantics.None, CanonicalPresencePolicy.Required,
                CanonicalFieldClassification.Gameplay,
                new[] { Reference("ID", TimelineDomain, CanonicalDefinitionKind.Timeline, CanonicalPresencePolicy.Required) }, null);
            var variants = new[]
            {
                new CanonicalSemanticVariantSchema(0, "ApplyEffectsOnActivate", new[] { applyParam }),
                new CanonicalSemanticVariantSchema(1, "TimelineRef", new[] { timelineParam }),
            };
            return Node("AbilityExecution", CanonicalSemanticNodeKind.Variant, "exgas.AbilityExecutionBase", string.Empty,
                CanonicalSemanticValueKind.Null, CanonicalCollectionSemantics.None, CanonicalPresencePolicy.Required,
                CanonicalFieldClassification.Gameplay, null, variants);
        }

        /// <summary>
        /// 创建 RuntimeV1TargetPolicy 的四个闭集 enum 字段。
        /// </summary>
        private static CanonicalSemanticNodeSchema TargetPolicy(string id)
        {
            return Node(id, CanonicalSemanticNodeKind.Record, "exgas.RuntimeV1TargetPolicy", string.Empty,
                CanonicalSemanticValueKind.Null, CanonicalCollectionSemantics.None, CanonicalPresencePolicy.Optional,
                CanonicalFieldClassification.Gameplay,
                new[]
                {
                    Scalar("LogicalTarget", CanonicalSemanticValueKind.Enum, typeId: "exgas.RuntimeV1LogicalTargetPolicy"),
                    Scalar("Avatar", CanonicalSemanticValueKind.Enum, typeId: "exgas.RuntimeV1AvatarTargetPolicy"),
                    Scalar("Spatial", CanonicalSemanticValueKind.Enum, typeId: "exgas.RuntimeV1SpatialTargetPolicy"),
                    Scalar("Life", CanonicalSemanticValueKind.Enum, typeId: "exgas.RuntimeV1TargetLifePolicy"),
                }, null);
        }

        /// <summary>
        /// 创建 optional Duration record，present-zero 与 absent 由节点类型区分。
        /// </summary>
        private static CanonicalSemanticNodeSchema Duration()
        {
            return Node("Duration", CanonicalSemanticNodeKind.Record, "exgas.Duration", string.Empty,
                CanonicalSemanticValueKind.Null, CanonicalCollectionSemantics.None, CanonicalPresencePolicy.Optional,
                CanonicalFieldClassification.Gameplay,
                new[]
                {
                    Scalar("TimeUnit", CanonicalSemanticValueKind.Enum, typeId: "exgas.TimeUnit"),
                    Scalar("Time", CanonicalSemanticValueKind.Int32),
                    Scalar("ResetStartTimeWhenActivated", CanonicalSemanticValueKind.Boolean),
                }, null);
        }

        /// <summary>
        /// 创建 optional Period record，Effects 保留完整 authored 顺序。
        /// </summary>
        private static CanonicalSemanticNodeSchema Period()
        {
            return Node("Period", CanonicalSemanticNodeKind.Record, "exgas.Period", string.Empty,
                CanonicalSemanticValueKind.Null, CanonicalCollectionSemantics.None, CanonicalPresencePolicy.Optional,
                CanonicalFieldClassification.Gameplay,
                new[]
                {
                    Scalar("Time", CanonicalSemanticValueKind.Int32),
                    References("Effects", GameplayEffectDomain, CanonicalDefinitionKind.GameplayEffect, CanonicalCollectionSemantics.Ordered),
                    Scalar("FirstTrigger", CanonicalSemanticValueKind.Boolean),
                }, null);
        }

        /// <summary>
        /// 创建 ordered Modifier record collection，避免旧 parallel-array/scalar mirror 双事实源。
        /// </summary>
        private static CanonicalSemanticNodeSchema Modifiers()
        {
            var element = Node("$element", CanonicalSemanticNodeKind.Record, "exgas.Modifier", string.Empty,
                CanonicalSemanticValueKind.Null, CanonicalCollectionSemantics.None, CanonicalPresencePolicy.Required,
                CanonicalFieldClassification.Gameplay,
                new[]
                {
                    Reference("AttrSet", AttributeSetDomain, CanonicalDefinitionKind.AttributeSet, CanonicalPresencePolicy.Required),
                    Reference("Attribute", AttributeDomain, CanonicalDefinitionKind.Attribute, CanonicalPresencePolicy.Required),
                    Scalar("Magnitude", CanonicalSemanticValueKind.Float32),
                    Scalar("Operation", CanonicalSemanticValueKind.Enum, typeId: "exgas.ModifierOperation"),
                }, null);
            return Node("Modifiers", CanonicalSemanticNodeKind.Collection, "exgas.Modifier[]", string.Empty,
                CanonicalSemanticValueKind.Null, CanonicalCollectionSemantics.Ordered, CanonicalPresencePolicy.Required,
                CanonicalFieldClassification.Gameplay, new[] { element }, null);
        }

        /// <summary>
        /// 创建独立 Cue 字段的 GameplayCue set，重复元素由 projector 稳定 CFG1001 拒绝。
        /// </summary>
        private static CanonicalSemanticNodeSchema CueSet(string id)
        {
            return References(id, GameplayCueDomain, CanonicalDefinitionKind.GameplayCue, CanonicalCollectionSemantics.Set);
        }

        /// <summary>
        /// 创建 ordered GrantedAbility record collection并保留全部 policy enum。
        /// </summary>
        private static CanonicalSemanticNodeSchema GrantedAbilities()
        {
            var element = Node("$element", CanonicalSemanticNodeKind.Record, "exgas.GrantedAbility", string.Empty,
                CanonicalSemanticValueKind.Null, CanonicalCollectionSemantics.None, CanonicalPresencePolicy.Required,
                CanonicalFieldClassification.Gameplay,
                new[]
                {
                    Reference("ID", AbilityDomain, CanonicalDefinitionKind.Ability, CanonicalPresencePolicy.Required),
                    Scalar("Level", CanonicalSemanticValueKind.Int32),
                    Scalar("ActivationPolicy", CanonicalSemanticValueKind.Enum, typeId: "exgas.GrantedAbilityActivationPolicy"),
                    Scalar("DeactivationPolicy", CanonicalSemanticValueKind.Enum, typeId: "exgas.GrantedAbilityDeactivationPolicy"),
                    Scalar("RemovePolicy", CanonicalSemanticValueKind.Enum, typeId: "exgas.GrantedAbilityRemovePolicy"),
                }, null);
            return Node("GrantedAbility", CanonicalSemanticNodeKind.Collection, "exgas.GrantedAbility[]", AbilityDomain,
                CanonicalSemanticValueKind.Null, CanonicalCollectionSemantics.Ordered, CanonicalPresencePolicy.Required,
                CanonicalFieldClassification.Gameplay, new[] { element }, null);
        }

        /// <summary>
        /// 创建 optional Stacking record并保留完整 overflow effect ordered list。
        /// </summary>
        private static CanonicalSemanticNodeSchema Stacking()
        {
            return Node("Stacking", CanonicalSemanticNodeKind.Record, "exgas.Stacking", string.Empty,
                CanonicalSemanticValueKind.Null, CanonicalCollectionSemantics.None, CanonicalPresencePolicy.Optional,
                CanonicalFieldClassification.Gameplay,
                new[]
                {
                    Scalar("StackingType", CanonicalSemanticValueKind.Enum, typeId: "exgas.StackingType"),
                    Scalar("StackCode", CanonicalSemanticValueKind.Int32),
                    Scalar("LimitCount", CanonicalSemanticValueKind.Int32),
                    Scalar("DurationRefreshPolicy", CanonicalSemanticValueKind.Enum, typeId: "exgas.DurationRefreshPolicy"),
                    Scalar("PeriodResetPolicy", CanonicalSemanticValueKind.Enum, typeId: "exgas.PeriodResetPolicy"),
                    Scalar("ExpirationPolicy", CanonicalSemanticValueKind.Enum, typeId: "exgas.ExpirationPolicy"),
                    Scalar("DenyOverflowApplication", CanonicalSemanticValueKind.Boolean),
                    Scalar("ClearStackOnOverflow", CanonicalSemanticValueKind.Boolean),
                    References("OverflowEffects", GameplayEffectDomain, CanonicalDefinitionKind.GameplayEffect, CanonicalCollectionSemantics.Ordered),
                }, null);
        }

        /// <summary>
        /// 创建 optional RuntimeV1Evaluator，enum、Attribute target domain 与全部 numeric 参数均进入 graph。
        /// </summary>
        private static CanonicalSemanticNodeSchema Evaluator()
        {
            return Node("RuntimeV1Evaluator", CanonicalSemanticNodeKind.Record, "exgas.RuntimeV1Evaluator", string.Empty,
                CanonicalSemanticValueKind.Null, CanonicalCollectionSemantics.None, CanonicalPresencePolicy.Optional,
                CanonicalFieldClassification.Gameplay,
                new[]
                {
                    Scalar("Kind", CanonicalSemanticValueKind.Enum, typeId: "exgas.RuntimeV1EvaluatorKind"),
                    Reference("AttributeSet", AttributeSetDomain, CanonicalDefinitionKind.AttributeSet, CanonicalPresencePolicy.Required),
                    Reference("Attribute", AttributeDomain, CanonicalDefinitionKind.Attribute, CanonicalPresencePolicy.Required),
                    Scalar("BaseValue", CanonicalSemanticValueKind.Float32),
                    Scalar("Coefficient", CanonicalSemanticValueKind.Float32),
                    Scalar("Minimum", CanonicalSemanticValueKind.Float32),
                    Scalar("Maximum", CanonicalSemanticValueKind.Float32),
                }, null);
        }

        /// <summary>
        /// 创建节点 schema 并为 record 成员分配当前 scope 的显式 ordinal。
        /// </summary>
        private static CanonicalSemanticNodeSchema Node(
            string id,
            CanonicalSemanticNodeKind kind,
            string typeId,
            string targetDomain,
            CanonicalSemanticValueKind scalarKind,
            CanonicalCollectionSemantics collectionSemantics,
            CanonicalPresencePolicy presence,
            CanonicalFieldClassification classification,
            IReadOnlyList<CanonicalSemanticNodeSchema> children,
            IReadOnlyList<CanonicalSemanticVariantSchema> variants)
        {
            var source = CanonicalSemanticResourceBudget.SnapshotList(
                children,
                "registry node children");
            var normalized = new CanonicalSemanticNodeSchema[source.Length];
            for (var index = 0; index < source.Length; index++)
                normalized[index] = source[index].WithOrdinal(index);
            return new CanonicalSemanticNodeSchema(
                -1,
                id,
                kind,
                typeId,
                targetDomain,
                scalarKind,
                collectionSemantics,
                presence,
                classification,
                classification == CanonicalFieldClassification.Denied
                    ? CanonicalFieldDisposition.Denied
                    : CanonicalFieldDisposition.Allowed,
                classification == CanonicalFieldClassification.Denied
                    ? CanonicalSemanticRuleCatalog.UnsupportedContractField
                    : CanonicalSemanticRuleCatalog.InvalidDomainOrReference,
                normalized,
                variants);
        }
    }
}
