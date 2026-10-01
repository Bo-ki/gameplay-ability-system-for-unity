using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GAS.Editor.CodeGen.Semantics;

namespace GAS.Editor.CodeGen.Proofs.Typed
{
    /// <summary>
    /// 暴露已与 external hash、TypedContract 和同一 canonical graph 绑定的只读 value tree；它不授予 eligibility。
    /// </summary>
    public sealed class TypedValueTree
    {
        private readonly ReadOnlyCollection<TypedDefinitionValueTree> _definitions;

        /// <summary>
        /// 创建仅由强绑定 adapter 产出的只读 value tree 根。
        /// </summary>
        internal TypedValueTree(
            string typedContractHash,
            string graphHash,
            IReadOnlyList<TypedDefinitionValueTree> definitions)
        {
            TypedContractHash = typedContractHash;
            GraphHash = graphHash;
            _definitions = TypedContractFreeze.Copy(definitions);
        }

        public string TypedContractHash { get; }

        public string GraphHash { get; }

        public IReadOnlyList<TypedDefinitionValueTree> Definitions => _definitions;
    }

    /// <summary>
    /// 将一个 typed Definition 的稳定 identity 与逐字段 value tree 组合为只读视图。
    /// </summary>
    public sealed class TypedDefinitionValueTree
    {
        private readonly ReadOnlyCollection<TypedFieldValueTree> _fields;

        /// <summary>
        /// 创建与 TypedDefinitionContract ordinal/key 一致的 Definition value tree。
        /// </summary>
        internal TypedDefinitionValueTree(
            int definitionOrdinal,
            CanonicalDefinitionKey definitionKey,
            IReadOnlyList<TypedFieldValueTree> fields)
        {
            DefinitionOrdinal = definitionOrdinal;
            DefinitionKey = definitionKey;
            _fields = TypedContractFreeze.Copy(fields);
        }

        public int DefinitionOrdinal { get; }

        public CanonicalDefinitionKey DefinitionKey { get; }

        public IReadOnlyList<TypedFieldValueTree> Fields => _fields;
    }

    /// <summary>
    /// 暴露 TypedFieldContract 的分类裁决与同一字段完整 canonical value root。
    /// </summary>
    public sealed class TypedFieldValueTree
    {
        /// <summary>
        /// 创建字段 metadata 与 value root 不可拆分的只读视图。
        /// </summary>
        internal TypedFieldValueTree(
            int fieldOrdinal,
            string fieldId,
            CanonicalFieldClassification classification,
            CanonicalFieldDisposition disposition,
            TypedValueNode root)
        {
            FieldOrdinal = fieldOrdinal;
            FieldId = fieldId;
            Classification = classification;
            Disposition = disposition;
            Root = root;
        }

        public int FieldOrdinal { get; }

        public string FieldId { get; }

        public CanonicalFieldClassification Classification { get; }

        public CanonicalFieldDisposition Disposition { get; }

        public TypedValueNode Root { get; }
    }

    /// <summary>
    /// 以独立 immutable DTO 表达 typed scalar，避免把 canonical value 对象或其内部 byte 所有权泄漏给消费者。
    /// </summary>
    public sealed class TypedScalarValue
    {
        private readonly byte[] _bytes;

        /// <summary>
        /// 从 adapter 已验证输入冻结 closed scalar union 的全部状态。
        /// </summary>
        internal TypedScalarValue(
            CanonicalSemanticValueKind kind,
            CanonicalEnumUnderlyingType enumUnderlyingType,
            DateTimeKind dateTimeKind,
            CanonicalDefinitionKey definitionReference,
            ulong rawBits,
            string text,
            byte[] bytes)
        {
            Kind = kind;
            EnumUnderlyingType = enumUnderlyingType;
            DateTimeKind = dateTimeKind;
            DefinitionReference = definitionReference;
            RawBits = rawBits;
            Text = text;
            _bytes = bytes == null ? null : TypedContractFreeze.CopyBytes(bytes);
        }

        public CanonicalSemanticValueKind Kind { get; }

        public CanonicalEnumUnderlyingType EnumUnderlyingType { get; }

        public DateTimeKind DateTimeKind { get; }

        public CanonicalDefinitionKey DefinitionReference { get; }

        public ulong RawBits { get; }

        public string Text { get; }

        public string StringValue => Kind == CanonicalSemanticValueKind.String ? Text : null;

        public byte[] ByteValue => _bytes == null ? null : TypedContractFreeze.CopyBytes(_bytes);
    }

    /// <summary>
    /// 以 closed typed union、显式 shape、provenance 与只读 children 表达一个 semantic value 节点。
    /// </summary>
    public sealed class TypedValueNode
    {
        private readonly ReadOnlyCollection<TypedValueNode> _children;

        /// <summary>
        /// 从已验证 canonical node 复制只读 shape/value/provenance，并冻结子节点顺序。
        /// </summary>
        internal TypedValueNode(
            int nodeOrdinal,
            string nodeId,
            CanonicalSemanticNodeKind nodeKind,
            string typeId,
            string targetDomainId,
            CanonicalCollectionSemantics collectionSemantics,
            TypedScalarValue scalarValue,
            IReadOnlyList<TypedValueNode> children,
            CanonicalSemanticProvenance provenance)
        {
            NodeOrdinal = nodeOrdinal;
            NodeId = nodeId;
            NodeKind = nodeKind;
            TypeId = typeId;
            TargetDomainId = targetDomainId;
            CollectionSemantics = collectionSemantics;
            ScalarValue = scalarValue;
            _children = TypedContractFreeze.Copy(children);
            Provenance = provenance;
        }

        public int NodeOrdinal { get; }

        public string NodeId { get; }

        public CanonicalSemanticNodeKind NodeKind { get; }

        public string TypeId { get; }

        public string TargetDomainId { get; }

        public CanonicalCollectionSemantics CollectionSemantics { get; }

        public TypedScalarValue ScalarValue { get; }

        public IReadOnlyList<TypedValueNode> Children => _children;

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 从同一 canonical graph 与强校验 TypedContract 派生 B 可读取的 value tree，不连接 B 或 promotion 路径。
    /// </summary>
    public static class TypedValueTreeAdapter
    {
        /// <summary>
        /// 强制 external expected hash 与 graph/contract 逐 byte 匹配后创建只读 value tree。
        /// </summary>
        public static TypedValueTree CreateAndEnsureMatches(
            CanonicalNormalizedSemanticGraph graph,
            TypedContract contract,
            string expectedTypedContractHash)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            if (contract == null)
                throw new ArgumentNullException(nameof(contract));
            TypedContractBuilder.EnsureMatches(graph, contract, expectedTypedContractHash);
            CanonicalSemanticResourceBudget.ValidateGraph(graph);
            var definitions = BuildDefinitions(graph.Definitions, contract.Definitions);
            return new TypedValueTree(expectedTypedContractHash, contract.GraphHash, definitions);
        }

        /// <summary>
        /// 按已验证 canonical 顺序绑定 graph 与 typed Definition。
        /// </summary>
        private static IReadOnlyList<TypedDefinitionValueTree> BuildDefinitions(
            IReadOnlyList<CanonicalSemanticDefinition> graphDefinitions,
            IReadOnlyList<TypedDefinitionContract> typedDefinitions)
        {
            if (graphDefinitions.Count != typedDefinitions.Count)
                Fail("Typed value adapter Definition coverage is incomplete.");
            var result = new TypedDefinitionValueTree[graphDefinitions.Count];
            for (var index = 0; index < result.Length; index++)
            {
                var graphDefinition = graphDefinitions[index];
                var typedDefinition = typedDefinitions[index];
                result[index] = new TypedDefinitionValueTree(
                    typedDefinition.DefinitionOrdinal,
                    typedDefinition.DefinitionKey,
                    BuildFields(graphDefinition.Fields, typedDefinition.Fields));
            }
            return result;
        }

        /// <summary>
        /// 按 field ordinal 将 typed classification 与 canonical value root 绑定。
        /// </summary>
        private static IReadOnlyList<TypedFieldValueTree> BuildFields(
            IReadOnlyList<CanonicalSemanticField> graphFields,
            IReadOnlyList<TypedFieldContract> typedFields)
        {
            if (graphFields.Count != typedFields.Count)
                Fail("Typed value adapter field coverage is incomplete.");
            var result = new TypedFieldValueTree[graphFields.Count];
            for (var index = 0; index < result.Length; index++)
            {
                var graphField = graphFields[index];
                var typedField = typedFields[index];
                result[index] = new TypedFieldValueTree(
                    typedField.FieldOrdinal,
                    typedField.FieldId,
                    typedField.Classification,
                    typedField.Disposition,
                    BuildNode(graphField.Root));
            }
            return result;
        }

        /// <summary>
        /// 递归复制深度已受 v2 预算限制的 canonical node 为只读 typed node。
        /// </summary>
        private static TypedValueNode BuildNode(CanonicalSemanticNode node)
        {
            var children = new TypedValueNode[node.Children.Count];
            for (var index = 0; index < children.Length; index++)
                children[index] = BuildNode(node.Children[index]);
            return new TypedValueNode(
                node.NodeOrdinal,
                node.NodeId,
                node.NodeKind,
                node.TypeId,
                node.TargetDomainId,
                node.CollectionSemantics,
                BuildScalar(node.ScalarValue),
                children,
                node.Provenance);
        }

        /// <summary>
        /// 把 canonical scalar 的值状态复制为不共享 byte 所有权的独立 typed DTO。
        /// </summary>
        private static TypedScalarValue BuildScalar(CanonicalSemanticValue value)
        {
            if (value == null)
                return null;
            return new TypedScalarValue(
                value.Kind,
                value.EnumUnderlyingType,
                value.DateTimeKind,
                value.DefinitionReference,
                value.RawBits,
                value.EnumTypeId ?? value.StringValue,
                value.ByteValue);
        }

        /// <summary>
        /// 以稳定 CFG1501 拒绝任何内部 graph/typed coverage 漂移。
        /// </summary>
        private static void Fail(string message)
        {
            throw new CanonicalSemanticValidationException(
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                message);
        }
    }
}
