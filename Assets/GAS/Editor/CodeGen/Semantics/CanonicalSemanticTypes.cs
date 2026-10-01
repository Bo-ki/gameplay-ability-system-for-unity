using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace GAS.Editor.CodeGen.Semantics
{
    /// <summary>
    /// 集中冻结 canonical semantic graph、TypedContract、资源预算与哈希投影共享的版本化 domain。
    /// </summary>
    public static class CanonicalSemanticVersions
    {
        public const ushort GraphCodecVersion = 2;
        public const ushort SchemaProjectionVersion = 2;
        public const ushort ContentProjectionVersion = 2;
        public const ushort TypedContractCodecVersion = 2;
        public const ushort ContractMatrixCodecVersion = 2;
        public const ushort LubanSourceContractVersion = 2;
        public const ushort ResourceBudgetVersion = 2;
        public const string GraphSchema = "CanonicalNormalizedSemanticGraph-v2";
        public const string RuleDomain = "EX-GAS-RuleDomain-v2";
        public const string TypedContractSchema = "TypedContract-v2";
        public const string TypedContractAbi = "RuntimeV1-TypedContractAbi-v2";
        public const string GraphHashDomain = "EX-GAS-CanonicalGraphHash-v2";
        public const string SchemaHashDomain = "EX-GAS-CanonicalSchemaHash-v2";
        public const string ContentHashDomain = "EX-GAS-CanonicalContentHash-v2";
        public const string ContractMatrixHashDomain = "EX-GAS-ContractMatrixHash-v2";
        public const string TypedContractHashDomain = "EX-GAS-TypedContractHash-v2";
        public const string ResourceBudgetId = "CanonicalSemanticResourceBudget-v2";
        public const string ByteOrder = "LittleEndian";
    }

    /// <summary>
    /// 冻结 Luban product、binary、schema 输入与生成器身份，禁止用单个可歧义显示字符串替代工具链边界。
    /// </summary>
    public sealed class CanonicalSemanticToolchainIdentity
    {
        /// <summary>
        /// 创建结构化 toolchain identity；hash 格式由 projector/graph validator 统一校验。
        /// </summary>
        public CanonicalSemanticToolchainIdentity(
            string lubanProductVersion,
            string lubanBinaryHash,
            string schemaInputHash,
            string generatorIdentity)
        {
            LubanProductVersion = lubanProductVersion;
            LubanBinaryHash = lubanBinaryHash;
            SchemaInputHash = schemaInputHash;
            GeneratorIdentity = generatorIdentity;
        }

        public string LubanProductVersion { get; }

        public string LubanBinaryHash { get; }

        public string SchemaInputHash { get; }

        public string GeneratorIdentity { get; }
    }

    /// <summary>
    /// 定义 canonical graph 支持的业务领域；该枚举独立于旧 GasDefinitionKind，并显式覆盖 Timeline。
    /// </summary>
    public enum CanonicalDefinitionKind : int
    {
        Unknown = 0,
        Ability = 1,
        GameplayEffect = 2,
        AttributeSet = 3,
        Attribute = 4,
        GameplayTag = 5,
        GameplayCue = 6,
        Timeline = 7,
    }

    /// <summary>
    /// 定义 canonical value 的二进制类型标签，所有数值均由 codec 按固定宽度写入。
    /// </summary>
    public enum CanonicalSemanticValueKind : byte
    {
        Null = 0,
        Boolean = 1,
        Int32 = 2,
        UInt32 = 3,
        Int64 = 4,
        UInt64 = 5,
        Float32 = 6,
        Float64 = 7,
        String = 8,
        Bytes = 9,
        Enum = 10,
        DateTime = 11,
        DefinitionReference = 12,
    }

    /// <summary>
    /// 区分 semantic tree 中 absent、scalar、record、variant 与 collection，避免 optional 和显式零碰撞。
    /// </summary>
    public enum CanonicalSemanticNodeKind : byte
    {
        Unknown = 0,
        Absent = 1,
        Scalar = 2,
        Record = 3,
        Variant = 4,
        Collection = 5,
    }

    /// <summary>
    /// 冻结集合的业务等价关系；ordered 保序，set 去重排序，multiset 保留重数排序。
    /// </summary>
    public enum CanonicalCollectionSemantics : byte
    {
        None = 0,
        Ordered = 1,
        Set = 2,
        Multiset = 3,
    }

    /// <summary>
    /// 表示 schema 字段是否允许 absent，present-zero 始终与 absent 使用不同节点编码。
    /// </summary>
    public enum CanonicalPresencePolicy : byte
    {
        Required = 1,
        Optional = 2,
    }

    /// <summary>
    /// 对真实 Luban 每个字段作 gameplay、仅 provenance 或显式 deny 的穷尽分类。
    /// </summary>
    public enum CanonicalFieldClassification : byte
    {
        Unknown = 0,
        Gameplay = 1,
        ProvenanceOnly = 2,
        Denied = 3,
    }

    /// <summary>
    /// 冻结本轮 coverage 与 eligibility 的 fail-closed 状态；接线完成前不得伪造 Green。
    /// </summary>
    public enum CanonicalSemanticCoverageState : byte
    {
        Unknown = 0,
        Red = 1,
        Green = 2,
    }

    /// <summary>
    /// 冻结 enum 的真实底层整数宽度与有符号性，codec 不读取运行时 Type 或反射信息。
    /// </summary>
    public enum CanonicalEnumUnderlyingType : byte
    {
        SByte = 1,
        Byte = 2,
        Int16 = 3,
        UInt16 = 4,
        Int32 = 5,
        UInt32 = 6,
        Int64 = 7,
        UInt64 = 8,
    }

    /// <summary>
    /// 表示 typed support matrix 对字段的唯一裁决，未知值必须 fail closed。
    /// </summary>
    public enum CanonicalFieldDisposition : byte
    {
        Unknown = 0,
        Allowed = 1,
        Denied = 2,
        RequiredProjection = 3,
    }

    /// <summary>
    /// 冻结 Spec 25 中允许出现的 program 类型，禁止用普通 GE 压平专用 contract。
    /// </summary>
    public enum CanonicalProgramKind : byte
    {
        Unknown = 0,
        CostMutation = 1,
        CooldownGate = 2,
        CaptureProjection = 3,
        StackTemporal = 4,
        DirectEffect = 5,
        SpawnInitialization = 6,
    }

    /// <summary>
    /// 区分 program 内部的控制、数据、目标与清理边，避免依赖字符串约定。
    /// </summary>
    public enum CanonicalProgramEdgeKind : byte
    {
        Unknown = 0,
        Control = 1,
        Data = 2,
        Target = 3,
        Cleanup = 4,
    }

    /// <summary>
    /// 表达跨 Definition 依赖的稳定语义类型；当前仅是全 Red 原型，不是 B 的可接纳 proof 输入。
    /// </summary>
    public enum CanonicalDependencyKind : byte
    {
        Unknown = 0,
        Reference = 1,
        Positive = 2,
        Negative = 3,
        Program = 4,
        Cleanup = 5,
    }

    /// <summary>
    /// 表示由 compiler 推导、调用方不可注入的 Definition 语义角色。
    /// </summary>
    public enum CanonicalDefinitionRoleKind : byte
    {
        Unknown = 0,
        Authoring = 1,
        Cost = 2,
        Cooldown = 3,
        DirectEffect = 4,
        PeriodicChild = 5,
        OverflowChild = 6,
        GrantedAbilitySource = 7,
    }

    /// <summary>
    /// 标记 dependency 的语义 owner；完整 same-parse proof 闭合前不得作为 Runtime/B 权威输入。
    /// </summary>
    public enum CanonicalDependencyOwnerKind : byte
    {
        Unknown = 0,
        AuthoringField = 1,
        Cost = 2,
        Cooldown = 3,
        Period = 4,
        Overflow = 5,
        GrantedAbility = 6,
        Cue = 7,
        Tag = 8,
        Execution = 9,
    }

    /// <summary>
    /// 标记 dependency 对稳定化方程的正、负或中性影响。
    /// </summary>
    public enum CanonicalDependencySign : byte
    {
        Neutral = 0,
        Positive = 1,
        Negative = 2,
    }

    /// <summary>
    /// 表示 dependency 可能展开的工作类别；当前 MaxExpansion 仍为 Red 原型数据。
    /// </summary>
    public enum CanonicalDependencyWorkKind : byte
    {
        Unknown = 0,
        Read = 1,
        Mutation = 2,
        Application = 3,
        Cue = 4,
        Grant = 5,
        Cleanup = 6,
    }

    /// <summary>
    /// 表示 dependency 的清理权利；Unsupported 必须保持 coverage Red。
    /// </summary>
    public enum CanonicalCleanupPolicy : byte
    {
        None = 0,
        GateExpiry = 1,
        ExactApplication = 2,
        GrantedAbilityPolicy = 3,
        Unsupported = 4,
    }

    /// <summary>
    /// 分类固定 RuleId 的机器语义，错误文本本地化不会进入身份。
    /// </summary>
    public enum CanonicalRuleKind : byte
    {
        Unknown = 0,
        Ownership = 1,
        DomainOrReference = 2,
        ContractSupport = 3,
        ValueViewOrPhase = 4,
        OwnedContribution = 5,
        ExecutionProjection = 6,
        ProjectionBound = 7,
        DependencyBound = 8,
        CapacityProof = 9,
        CanonicalIdentity = 10,
    }

    /// <summary>
    /// 冻结 Spec 25 的固定 RuleId registry；任何未登记 RuleId 都必须在构图阶段拒绝。
    /// </summary>
    public static class CanonicalSemanticRuleCatalog
    {
        private static readonly ReadOnlyCollection<string> OrderedRuleIds = Array.AsReadOnly(new[]
        {
            SemanticOwnerConflict,
            InvalidDomainOrReference,
            UnsupportedContractField,
            MissingValueViewOrPhase,
            InvalidOwnedContribution,
            UnsupportedExecutionProjection,
            ProjectionUnbounded,
            DependencyOrCleanupUnbounded,
            CapacityProofMissing,
            NonCanonicalIdentity,
        });

        public const string SemanticOwnerConflict = "CFG1001";
        public const string InvalidDomainOrReference = "CFG1002";
        public const string UnsupportedContractField = "CFG1101";
        public const string MissingValueViewOrPhase = "CFG1102";
        public const string InvalidOwnedContribution = "CFG1103";
        public const string UnsupportedExecutionProjection = "CFG1104";
        public const string ProjectionUnbounded = "CFG1201";
        public const string DependencyOrCleanupUnbounded = "CFG1301";
        public const string CapacityProofMissing = "CFG1401";
        public const string NonCanonicalIdentity = "CFG1501";

        public static IReadOnlyList<string> RuleIds => OrderedRuleIds;

        /// <summary>
        /// 创建固定顺序、完整闭集的 RuleId registry，调用方不得按当前命中规则裁剪 domain。
        /// </summary>
        public static IReadOnlyList<CanonicalSemanticRule> CreateDefaultRegistry(string generatorVersion)
        {
            var result = new CanonicalSemanticRule[OrderedRuleIds.Count];
            for (var index = 0; index < OrderedRuleIds.Count; index++)
            {
                var ruleId = OrderedRuleIds[index];
                TryResolve(ruleId, out var version, out var kind);
                var provenance = new CanonicalSemanticProvenance(
                    "EX-GAS-Architecture",
                    "Spec25.RuleRegistry",
                    ruleId,
                    "RuleId",
                    ruleId,
                    ruleId,
                    ruleId,
                    version,
                    generatorVersion,
                    Array.Empty<CanonicalDefinitionKey>());
                result[index] = new CanonicalSemanticRule(index, ruleId, version, kind, provenance);
            }

            return Array.AsReadOnly(result);
        }

        /// <summary>
        /// 解析固定 RuleId 对应的版本与机器分类。
        /// </summary>
        public static bool TryResolve(string ruleId, out uint version, out CanonicalRuleKind kind)
        {
            version = 1;
            switch (ruleId)
            {
                case SemanticOwnerConflict:
                    kind = CanonicalRuleKind.Ownership;
                    return true;
                case InvalidDomainOrReference:
                    kind = CanonicalRuleKind.DomainOrReference;
                    return true;
                case UnsupportedContractField:
                    kind = CanonicalRuleKind.ContractSupport;
                    return true;
                case MissingValueViewOrPhase:
                    kind = CanonicalRuleKind.ValueViewOrPhase;
                    return true;
                case InvalidOwnedContribution:
                    kind = CanonicalRuleKind.OwnedContribution;
                    return true;
                case UnsupportedExecutionProjection:
                    kind = CanonicalRuleKind.ExecutionProjection;
                    return true;
                case ProjectionUnbounded:
                    kind = CanonicalRuleKind.ProjectionBound;
                    return true;
                case DependencyOrCleanupUnbounded:
                    kind = CanonicalRuleKind.DependencyBound;
                    return true;
                case CapacityProofMissing:
                    kind = CanonicalRuleKind.CapacityProof;
                    return true;
                case NonCanonicalIdentity:
                    kind = CanonicalRuleKind.CanonicalIdentity;
                    return true;
                default:
                    version = 0;
                    kind = CanonicalRuleKind.Unknown;
                    return false;
            }
        }
    }

    /// <summary>
    /// 以 domain、kind 与复合稳定 ID 唯一标识 Definition，避免 Attribute 复合键被压成单整数。
    /// </summary>
    public sealed class CanonicalDefinitionKey : IComparable<CanonicalDefinitionKey>, IEquatable<CanonicalDefinitionKey>
    {
        private readonly ReadOnlyCollection<long> _stableIdParts;

        /// <summary>
        /// 冻结 DefinitionKey 并复制复合 ID，调用方后续修改输入集合不会影响身份。
        /// </summary>
        public CanonicalDefinitionKey(
            int domainOrdinal,
            CanonicalDefinitionKind definitionKind,
            IReadOnlyList<long> stableIdParts)
        {
            DomainOrdinal = domainOrdinal;
            DefinitionKind = definitionKind;
            _stableIdParts = Freeze(stableIdParts);
        }

        public int DomainOrdinal { get; }

        public CanonicalDefinitionKind DefinitionKind { get; }

        public IReadOnlyList<long> StableIdParts => _stableIdParts;

        /// <summary>
        /// 按 DomainOrdinal、kind、复合 ID 长度和值建立跨运行时稳定顺序。
        /// </summary>
        public int CompareTo(CanonicalDefinitionKey other)
        {
            if (other == null)
                return 1;

            var result = DomainOrdinal.CompareTo(other.DomainOrdinal);
            if (result != 0)
                return result;
            result = ((int)DefinitionKind).CompareTo((int)other.DefinitionKind);
            if (result != 0)
                return result;
            result = _stableIdParts.Count.CompareTo(other._stableIdParts.Count);
            for (var index = 0; result == 0 && index < _stableIdParts.Count; index++)
                result = _stableIdParts[index].CompareTo(other._stableIdParts[index]);
            return result;
        }

        /// <summary>
        /// 比较两个 DefinitionKey 的全部稳定组成部分。
        /// </summary>
        public bool HasSameIdentity(CanonicalDefinitionKey other)
        {
            return CompareTo(other) == 0;
        }

        /// <summary>
        /// 以完整稳定组成部分实现值相等，供当前 Red 原型内部使用。
        /// </summary>
        public bool Equals(CanonicalDefinitionKey other)
        {
            return other != null && HasSameIdentity(other);
        }

        /// <summary>
        /// 将 object 相等委托给强类型 DefinitionKey 值比较。
        /// </summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as CanonicalDefinitionKey);
        }

        /// <summary>
        /// 由全部稳定组成部分计算 hash code，不依赖引用身份。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = DomainOrdinal;
                hash = (hash * 397) ^ (int)DefinitionKind;
                for (var index = 0; index < _stableIdParts.Count; index++)
                    hash = (hash * 397) ^ _stableIdParts[index].GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// 生成仅用于诊断的文化无关文本；canonical codec 从不调用该方法。
        /// </summary>
        public override string ToString()
        {
            var result = new StringBuilder();
            result.Append(DomainOrdinal.ToString(CultureInfo.InvariantCulture));
            result.Append('/');
            result.Append(((int)DefinitionKind).ToString(CultureInfo.InvariantCulture));
            result.Append('/');
            for (var index = 0; index < _stableIdParts.Count; index++)
            {
                if (index > 0)
                    result.Append('.');
                result.Append(_stableIdParts[index].ToString(CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }

        /// <summary>
        /// 复制稳定 ID 列表并以只读包装暴露。
        /// </summary>
        private static ReadOnlyCollection<long> Freeze(IReadOnlyList<long> values)
        {
            var copy = CanonicalSemanticResourceBudget.SnapshotList(
                values,
                "canonical stable id parts");
            return Array.AsReadOnly(copy);
        }
    }

    /// <summary>
    /// 保存 Spec 25 要求的稳定 authoring 来源、原始/归一化值、RuleId 与关联 Definition。
    /// </summary>
    public sealed class CanonicalSemanticProvenance
    {
        private readonly ReadOnlyCollection<CanonicalDefinitionKey> _relatedDefinitionIds;

        /// <summary>
        /// 冻结 provenance；合法性由 graph validator 统一判定并返回稳定 RuleId。
        /// </summary>
        public CanonicalSemanticProvenance(
            string workbookId,
            string tableId,
            string rowStableId,
            string fieldPath,
            string rawValue,
            string normalizedValue,
            string ruleId,
            uint ruleVersion,
            string generatorVersion,
            IReadOnlyList<CanonicalDefinitionKey> relatedDefinitionIds)
        {
            ValidateTexts(workbookId, tableId, rowStableId, fieldPath,
                rawValue, normalizedValue, ruleId, generatorVersion);
            WorkbookId = workbookId;
            TableId = tableId;
            RowStableId = rowStableId;
            FieldPath = fieldPath;
            RawValue = rawValue;
            NormalizedValue = normalizedValue;
            RuleId = ruleId;
            RuleVersion = ruleVersion;
            GeneratorVersion = generatorVersion;
            _relatedDefinitionIds = FreezeAndSort(relatedDefinitionIds);
        }

        /// <summary>
        /// 在 canonical provenance 冻结前校验所有实际 strict UTF-8 byte count。
        /// </summary>
        private static void ValidateTexts(
            string workbookId,
            string tableId,
            string rowStableId,
            string fieldPath,
            string rawValue,
            string normalizedValue,
            string ruleId,
            string generatorVersion)
        {
            CanonicalSemanticResourceBudget.RequireString(workbookId, "canonical workbook");
            CanonicalSemanticResourceBudget.RequireString(tableId, "canonical table");
            CanonicalSemanticResourceBudget.RequireString(rowStableId, "canonical row");
            CanonicalSemanticResourceBudget.RequireString(fieldPath, "canonical field path");
            CanonicalSemanticResourceBudget.RequireString(rawValue, "canonical raw value");
            CanonicalSemanticResourceBudget.RequireString(normalizedValue, "canonical normalized value");
            CanonicalSemanticResourceBudget.RequireString(ruleId, "canonical rule id");
            CanonicalSemanticResourceBudget.RequireString(generatorVersion, "canonical generator");
        }

        public string WorkbookId { get; }

        public string TableId { get; }

        public string RowStableId { get; }

        public string FieldPath { get; }

        public string RawValue { get; }

        public string NormalizedValue { get; }

        public string RuleId { get; }

        public uint RuleVersion { get; }

        public string GeneratorVersion { get; }

        public IReadOnlyList<CanonicalDefinitionKey> RelatedDefinitionIds => _relatedDefinitionIds;

        /// <summary>
        /// 复制并按 DefinitionKey 排序关联项，使来源集合的枚举顺序不影响 canonical bytes。
        /// </summary>
        private static ReadOnlyCollection<CanonicalDefinitionKey> FreezeAndSort(
            IReadOnlyList<CanonicalDefinitionKey> values)
        {
            var copy = CanonicalSemanticResourceBudget.SnapshotList(
                values,
                "canonical related definitions");
            Array.Sort(copy, CompareKeys);
            return Array.AsReadOnly(copy);
        }

        /// <summary>
        /// 按 DefinitionKey 的冻结比较器排序。
        /// </summary>
        private static int CompareKeys(CanonicalDefinitionKey left, CanonicalDefinitionKey right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            return left.CompareTo(right);
        }
    }

    /// <summary>
    /// 保存不依赖 object、反射或默认 ToString 的强类型 canonical value 树。
    /// </summary>
    public sealed class CanonicalSemanticValue
    {
        private readonly byte[] _bytes;

        /// <summary>
        /// 构造内部冻结值；调用方使用显式工厂避免不完整 union 状态。
        /// </summary>
        private CanonicalSemanticValue(
            CanonicalSemanticValueKind kind,
            ulong rawBits = 0,
            string text = null,
            byte[] bytes = null,
            DateTimeKind dateTimeKind = DateTimeKind.Unspecified,
            CanonicalEnumUnderlyingType enumUnderlyingType = 0,
            CanonicalDefinitionKey definitionReference = null)
        {
            if (text != null)
                CanonicalSemanticResourceBudget.RequireString(text, "canonical scalar text");
            if (bytes != null)
                CanonicalSemanticResourceBudget.RequirePayloadLength(bytes.Length, "canonical scalar bytes");
            Kind = kind;
            RawBits = rawBits;
            Text = text;
            _bytes = bytes == null ? null : (byte[])bytes.Clone();
            DateTimeKind = dateTimeKind;
            EnumUnderlyingType = enumUnderlyingType;
            DefinitionReference = definitionReference;
        }

        public CanonicalSemanticValueKind Kind { get; }

        public CanonicalEnumUnderlyingType EnumUnderlyingType { get; }

        public string EnumTypeId => Kind == CanonicalSemanticValueKind.Enum ? Text : null;

        public DateTimeKind DateTimeKind { get; }

        public CanonicalDefinitionKey DefinitionReference { get; }

        public ulong RawBits { get; }

        internal string Text { get; }

        public string StringValue => Kind == CanonicalSemanticValueKind.String ? Text : null;

        public byte[] ByteValue => _bytes == null ? null : (byte[])_bytes.Clone();

        internal byte[] ByteValueUnsafe => _bytes;

        /// <summary>
        /// 创建显式 null 值，使 null 与空字符串、空 bytes 和空 sequence 编码不同。
        /// </summary>
        public static CanonicalSemanticValue Null()
        {
            return new CanonicalSemanticValue(CanonicalSemanticValueKind.Null);
        }

        /// <summary>
        /// 创建单字节规范布尔值。
        /// </summary>
        public static CanonicalSemanticValue Boolean(bool value)
        {
            return new CanonicalSemanticValue(CanonicalSemanticValueKind.Boolean, value ? 1UL : 0UL);
        }

        /// <summary>
        /// 创建固定 32 位有符号整数值。
        /// </summary>
        public static CanonicalSemanticValue Int32(int value)
        {
            return new CanonicalSemanticValue(CanonicalSemanticValueKind.Int32, unchecked((uint)value));
        }

        /// <summary>
        /// 创建固定 32 位无符号整数值。
        /// </summary>
        public static CanonicalSemanticValue UInt32(uint value)
        {
            return new CanonicalSemanticValue(CanonicalSemanticValueKind.UInt32, value);
        }

        /// <summary>
        /// 创建固定 64 位有符号整数值。
        /// </summary>
        public static CanonicalSemanticValue Int64(long value)
        {
            return new CanonicalSemanticValue(CanonicalSemanticValueKind.Int64, unchecked((ulong)value));
        }

        /// <summary>
        /// 创建固定 64 位无符号整数值。
        /// </summary>
        public static CanonicalSemanticValue UInt64(ulong value)
        {
            return new CanonicalSemanticValue(CanonicalSemanticValueKind.UInt64, value);
        }

        /// <summary>
        /// 从 float 的 IEEE 754 原始位创建值，保留正负零与 NaN payload。
        /// </summary>
        public static CanonicalSemanticValue Float32Bits(uint bits)
        {
            return new CanonicalSemanticValue(CanonicalSemanticValueKind.Float32, bits);
        }

        /// <summary>
        /// 从 double 的 IEEE 754 原始位创建值，保留正负零与 NaN payload。
        /// </summary>
        public static CanonicalSemanticValue Float64Bits(ulong bits)
        {
            return new CanonicalSemanticValue(CanonicalSemanticValueKind.Float64, bits);
        }

        /// <summary>
        /// 创建 UTF-16 内存字符串值，codec 会以严格 UTF-8 byte length 写入。
        /// </summary>
        public static CanonicalSemanticValue String(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return new CanonicalSemanticValue(CanonicalSemanticValueKind.String, text: value);
        }

        /// <summary>
        /// 创建复制后的原始 bytes 值。
        /// </summary>
        public static CanonicalSemanticValue Bytes(byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return new CanonicalSemanticValue(CanonicalSemanticValueKind.Bytes, bytes: value);
        }

        /// <summary>
        /// 创建以 ticks 与 DateTimeKind 唯一表达的时间值。
        /// </summary>
        public static CanonicalSemanticValue DateTime(DateTime value)
        {
            return new CanonicalSemanticValue(
                CanonicalSemanticValueKind.DateTime,
                unchecked((ulong)value.Ticks),
                dateTimeKind: value.Kind);
        }

        /// <summary>
        /// 创建带稳定 enum type id 与底层整数位的 enum 值。
        /// </summary>
        public static CanonicalSemanticValue Enum(
            string enumTypeId,
            CanonicalEnumUnderlyingType underlyingType,
            ulong rawBits)
        {
            return new CanonicalSemanticValue(
                CanonicalSemanticValueKind.Enum,
                rawBits,
                enumTypeId,
                enumUnderlyingType: underlyingType);
        }

        /// <summary>
        /// 创建显式 DefinitionKey 引用，validator 会拒绝悬空目标。
        /// </summary>
        public static CanonicalSemanticValue Reference(CanonicalDefinitionKey definitionKey)
        {
            if (definitionKey == null)
                throw new ArgumentNullException(nameof(definitionKey));
            return new CanonicalSemanticValue(
                CanonicalSemanticValueKind.DefinitionReference,
                definitionReference: definitionKey);
        }

    }

    /// <summary>
    /// 以稳定 RuleId 与可选 provenance 表达 fail-closed 构图失败，消息文本不参与协议身份。
    /// </summary>
    public sealed class CanonicalSemanticValidationException : Exception
    {
        private readonly ReadOnlyCollection<CanonicalSemanticProvenance> _evidence;

        /// <summary>
        /// 创建带固定 RuleId、主 provenance 与稳定多端 evidence 的验证失败。
        /// </summary>
        public CanonicalSemanticValidationException(
            string ruleId,
            string message,
            CanonicalSemanticProvenance provenance = null,
            IReadOnlyList<CanonicalSemanticProvenance> evidence = null)
            : base(message)
        {
            RuleId = ruleId;
            Provenance = provenance;
            var source = evidence ?? (provenance == null
                ? Array.Empty<CanonicalSemanticProvenance>()
                : new[] { provenance });
            var copy = CanonicalSemanticResourceBudget.SnapshotList(
                source,
                "validation evidence");
            _evidence = Array.AsReadOnly(copy);
        }

        public string RuleId { get; }

        public CanonicalSemanticProvenance Provenance { get; }

        public IReadOnlyList<CanonicalSemanticProvenance> Evidence => _evidence;
    }
}
