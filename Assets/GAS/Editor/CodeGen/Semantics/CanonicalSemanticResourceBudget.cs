using System;
using System.Collections.Generic;
using System.Text;

namespace GAS.Editor.CodeGen.Semantics
{
    /// <summary>
    /// 冻结 v2 source/graph/codec 的资源上界，输入不能携带或放宽这些数值。
    /// </summary>
    internal static class CanonicalSemanticResourceBudget
    {
        public const int MaxWireBytes = 32 * 1024 * 1024;
        public const int MaxSingleCollectionCount = 4096;
        public const int MaxTotalCollectionItems = 524288;
        public const int MaxTotalNodes = 262144;
        public const int MaxTotalEdges = 262144;
        public const int MaxSingleStringBytes = 256 * 1024;
        public const int MaxTotalStringBytes = 8 * 1024 * 1024;
        public const int MaxSinglePayloadBytes = 4 * 1024 * 1024;
        // Total payload 是所有变长 UTF-8 与 byte/projection 数据的联合上界，不是仅 blob 上界。
        public const int MaxTotalPayloadBytes = 16 * 1024 * 1024;
        public const int MaxDepth = 64;

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// 在任何集合复制或数组/List 分配前验证局部 count。
        /// </summary>
        public static void RequireCollectionCount(int count, string context)
        {
            if (count < 0 || count > MaxSingleCollectionCount)
                Fail(context + " exceeds resource budget collection count.");
        }

        /// <summary>
        /// 在任何 string 冻结或 UTF-8 分配前验证实际 strict UTF-8 byte count。
        /// </summary>
        public static int RequireString(string value, string context)
        {
            if (value == null)
                Fail(context + " cannot be null.");
            try
            {
                var count = StrictUtf8.GetByteCount(value);
                if (count > MaxSingleStringBytes)
                    Fail(context + " exceeds resource budget string bytes.");
                return count;
            }
            catch (EncoderFallbackException)
            {
                Fail(context + " is not strict UTF-8 encodable.");
                return 0;
            }
        }

        /// <summary>
        /// 在字符串拼接分配前累计各片段 strict UTF-8 长度并执行单字符串上界。
        /// </summary>
        public static int RequireStringComposition(string context, params string[] parts)
        {
            long total = 0;
            for (var index = 0; index < parts.Length; index++)
                total += RequireString(parts[index], context + " part");
            if (total > MaxSingleStringBytes)
                Fail(context + " exceeds resource budget string bytes before concatenation.");
            return (int)total;
        }

        /// <summary>
        /// 在任何 byte payload 复制前验证局部长度。
        /// </summary>
        public static void RequirePayloadLength(int length, string context)
        {
            if (length < 0 || length > MaxSinglePayloadBytes)
                Fail(context + " exceeds resource budget payload bytes.");
        }

        /// <summary>
        /// 在 codec clone 或 hash 前校验完整 wire payload 长度。
        /// </summary>
        public static void RequireWireLength(int length, string context)
        {
            if (length < 0 || length > MaxWireBytes)
                Fail(context + " exceeds resource budget wire bytes.");
        }

        /// <summary>
        /// 以一次冻结 count、固定次数索引读取和复制后复核创建不可信列表快照。
        /// </summary>
        public static T[] SnapshotList<T>(
            IReadOnlyList<T> values,
            string context,
            bool nullAsEmpty = true)
        {
            if (values == null)
            {
                if (nullAsEmpty)
                    return Array.Empty<T>();
                Fail(context + " cannot be null.");
            }

            try
            {
                var count = values.Count;
                RequireCollectionCount(count, context);
                var snapshot = new T[count];
                for (var index = 0; index < count; index++)
                    snapshot[index] = values[index];
                VerifySnapshot(values, snapshot, count, context);
                return snapshot;
            }
            catch (CanonicalSemanticValidationException)
            {
                throw;
            }
            catch (Exception)
            {
                Fail(context + " changed or failed during snapshot.");
                return null;
            }
        }

        /// <summary>
        /// 复核 count 与每个位置仍指向同一值，拒绝 safe-to-huge 与同 count 替换。
        /// </summary>
        private static void VerifySnapshot<T>(
            IReadOnlyList<T> values,
            T[] snapshot,
            int count,
            string context)
        {
            if (values.Count != count)
                Fail(context + " changed count during snapshot.");
            for (var index = 0; index < count; index++)
            {
                if (!SnapshotItemEquals(snapshot[index], values[index]))
                    Fail(context + " changed an item during snapshot.");
            }
            if (values.Count != count)
                Fail(context + " changed count during snapshot verification.");
        }

        /// <summary>
        /// 引用值要求同一对象，值类型要求稳定值相等，避免调用对象自定义 hash。
        /// </summary>
        private static bool SnapshotItemEquals<T>(T left, T right)
        {
            return typeof(T).IsValueType
                ? EqualityComparer<T>.Default.Equals(left, right)
                : ReferenceEquals(left, right);
        }

        /// <summary>
        /// 在 document definitions freeze 与 projector sort 前扫描完整 source tree 累计预算。
        /// </summary>
        public static void ValidateSource(LubanSemanticSourceDocument source)
        {
            if (source == null)
                Fail("Source document is required for budget validation.");
            ValidateSourceParts(
                source.ToolchainIdentity,
                source.GeneratorVersion,
                source.SourceInputHash,
                source.Definitions);
        }

        /// <summary>
        /// 在 source document 构造器复制 definitions 前扫描输入对象图。
        /// </summary>
        public static void ValidateSourceParts(
            CanonicalSemanticToolchainIdentity toolchain,
            string generatorVersion,
            string sourceInputHash,
            IReadOnlyList<LubanSemanticSourceDefinition> definitions)
        {
            if (definitions == null)
                Fail("Source definitions cannot be null.");
            var frozenDefinitions = SnapshotList(
                definitions,
                "source definitions budget input",
                false);
            var meter = new CanonicalSemanticBudgetMeter();
            ChargeToolchain(meter, toolchain, generatorVersion, sourceInputHash);
            meter.ChargeCollection(frozenDefinitions.Length, "source definitions");
            for (var index = 0; index < frozenDefinitions.Length; index++)
                ChargeSourceDefinition(meter, frozenDefinitions[index]);
        }

        /// <summary>
        /// 在 compiler 创建 dictionary/roles/dependencies/programs 前重验 projected drafts 预算。
        /// </summary>
        public static void ValidateDrafts(IReadOnlyList<SemanticDefinitionDraft> drafts)
        {
            if (drafts == null)
                Fail("Semantic drafts cannot be null.");
            var frozenDrafts = SnapshotList(
                drafts,
                "semantic drafts budget input",
                false);
            var meter = new CanonicalSemanticBudgetMeter();
            meter.ChargeCollection(frozenDrafts.Length, "semantic drafts");
            for (var index = 0; index < frozenDrafts.Length; index++)
            {
                var draft = frozenDrafts[index];
                ChargeKey(meter, draft.Key);
                ChargeProvenance(meter, draft.Provenance);
                meter.ChargeCollection(CountOf(draft.Fields), "draft fields");
                for (var fieldIndex = 0; fieldIndex < draft.Fields.Count; fieldIndex++)
                    ChargeField(meter, draft.Fields[fieldIndex]);
            }
        }

        /// <summary>
        /// 在 graph 根复制/排序前校验 definitions、nodes、program/dependency edges 总量。
        /// </summary>
        public static void ValidateGraphParts(
            CanonicalSemanticToolchainIdentity toolchain,
            string generatorVersion,
            string sourceInputHash,
            IReadOnlyList<CanonicalSemanticRule> rules,
            IReadOnlyList<CanonicalSemanticDefinition> definitions)
        {
            if (rules == null || definitions == null)
                Fail("Graph rules/definitions cannot be null.");
            var frozenRules = SnapshotList(rules, "graph rules budget input", false);
            var frozenDefinitions = SnapshotList(
                definitions,
                "graph definitions budget input",
                false);
            var meter = new CanonicalSemanticBudgetMeter();
            ChargeToolchain(meter, toolchain, generatorVersion, sourceInputHash);
            meter.ChargeCollection(frozenRules.Length, "graph rules");
            meter.ChargeCollection(frozenDefinitions.Length, "graph definitions");
            for (var index = 0; index < frozenRules.Length; index++)
                ChargeRule(meter, frozenRules[index]);
            for (var index = 0; index < frozenDefinitions.Length; index++)
                ChargeDefinition(meter, frozenDefinitions[index]);
        }

        /// <summary>
        /// 对已冻结 graph 执行同一固定预算校验。
        /// </summary>
        public static void ValidateGraph(CanonicalNormalizedSemanticGraph graph)
        {
            if (graph == null)
                Fail("Canonical graph is required for budget validation.");
            ValidateGraphParts(
                graph.ToolchainIdentity,
                graph.GeneratorVersion,
                graph.SourceInputHash,
                graph.Rules,
                graph.Definitions);
        }

        /// <summary>
        /// 充值一条 graph rule 的字符串身份与完整 provenance。
        /// </summary>
        private static void ChargeRule(
            CanonicalSemanticBudgetMeter meter,
            CanonicalSemanticRule rule)
        {
            if (rule == null)
                Fail("Canonical rule cannot be null.");
            meter.ChargeString(rule.RuleId, "rule id");
            ChargeProvenance(meter, rule.Provenance);
        }

        /// <summary>
        /// 充值 source definition 的 identity、provenance 与全字段树。
        /// </summary>
        private static void ChargeSourceDefinition(
            CanonicalSemanticBudgetMeter meter,
            LubanSemanticSourceDefinition definition)
        {
            if (definition == null)
                Fail("Source definition cannot be null.");
            ChargeSourceId(meter, definition.DefinitionId);
            ChargeSourceProvenance(meter, definition.Provenance);
            meter.ChargeCollection(CountOf(definition.Fields), "source fields");
            for (var index = 0; index < definition.Fields.Count; index++)
            {
                var field = definition.Fields[index];
                if (field == null)
                    Fail("Source field cannot be null.");
                meter.ChargeString(field.FieldId, "source field id");
                ChargeSourceNode(meter, field.Root, 0);
            }
        }

        /// <summary>
        /// 递归充值 source node；深度门在访问 children 前立即失败。
        /// </summary>
        private static void ChargeSourceNode(
            CanonicalSemanticBudgetMeter meter,
            LubanSemanticSourceNode node,
            int depth)
        {
            if (node == null)
                Fail("Source node cannot be null.");
            meter.ChargeNode(depth);
            meter.ChargeString(node.NodeId, "source node id");
            meter.ChargeString(node.TypeId, "source type id");
            meter.ChargeString(node.TargetDomainId, "source target domain");
            ChargeSourceProvenance(meter, node.Provenance);
            ChargeSourceValue(meter, node.ScalarValue);
            meter.ChargeCollection(CountOf(node.Children), "source children");
            for (var index = 0; index < node.Children.Count; index++)
                ChargeSourceNode(meter, node.Children[index], depth + 1);
        }

        /// <summary>
        /// 充值 source scalar 的实际 string/blob/reference payload。
        /// </summary>
        private static void ChargeSourceValue(
            CanonicalSemanticBudgetMeter meter,
            LubanSemanticSourceValue value)
        {
            if (value == null)
                return;
            if (value.Text != null)
                meter.ChargeString(value.Text, "source scalar text");
            var bytes = value.ByteValueUnsafe;
            if (bytes != null)
                meter.ChargePayload(bytes.Length, "source scalar bytes");
            if (value.DefinitionReference != null)
                ChargeSourceId(meter, value.DefinitionReference);
        }

        /// <summary>
        /// 充值 canonical definition 的 fields、roles、programs 与 dependencies。
        /// </summary>
        private static void ChargeDefinition(
            CanonicalSemanticBudgetMeter meter,
            CanonicalSemanticDefinition definition)
        {
            if (definition == null)
                Fail("Canonical definition cannot be null.");
            ChargeKey(meter, definition.Key);
            ChargeProvenance(meter, definition.Provenance);
            meter.ChargeCollection(definition.Fields.Count, "graph fields");
            for (var index = 0; index < definition.Fields.Count; index++)
                ChargeField(meter, definition.Fields[index]);
            meter.ChargeCollection(definition.Roles.Count, "graph roles");
            for (var index = 0; index < definition.Roles.Count; index++)
                ChargeRole(meter, definition.Roles[index]);
            meter.ChargeCollection(definition.Programs.Count, "graph programs");
            meter.ChargeCollection(definition.Dependencies.Count, "graph dependencies");
            for (var index = 0; index < definition.Programs.Count; index++)
                ChargeProgram(meter, definition.Programs[index]);
            for (var index = 0; index < definition.Dependencies.Count; index++)
                ChargeDependency(meter, definition.Dependencies[index]);
        }

        /// <summary>
        /// 充值 canonical field 的字符串、provenance 与完整 value tree。
        /// </summary>
        private static void ChargeField(
            CanonicalSemanticBudgetMeter meter,
            CanonicalSemanticField field)
        {
            if (field == null)
                Fail("Canonical field cannot be null.");
            meter.ChargeString(field.FieldId, "graph field id");
            ChargeNode(meter, field.Root, 0);
            ChargeProvenance(meter, field.Provenance);
        }

        /// <summary>
        /// 充值 compiler-derived role 的 owner key、RuleId 与 provenance。
        /// </summary>
        private static void ChargeRole(
            CanonicalSemanticBudgetMeter meter,
            CanonicalSemanticRole role)
        {
            if (role == null)
                Fail("Canonical role cannot be null.");
            ChargeKey(meter, role.OwnerDefinition);
            meter.ChargeString(role.RuleId, "graph role rule id");
            ChargeProvenance(meter, role.Provenance);
        }

        /// <summary>
        /// 充值 canonical semantic tree 的总节点、字符串与 scalar payload。
        /// </summary>
        private static void ChargeNode(
            CanonicalSemanticBudgetMeter meter,
            CanonicalSemanticNode node,
            int depth)
        {
            if (node == null)
                Fail("Canonical node cannot be null.");
            meter.ChargeNode(depth);
            meter.ChargeString(node.NodeId, "graph node id");
            meter.ChargeString(node.TypeId, "graph type id");
            meter.ChargeString(node.TargetDomainId, "graph target domain");
            ChargeValue(meter, node.ScalarValue);
            meter.ChargeCollection(node.Children.Count, "graph children");
            for (var index = 0; index < node.Children.Count; index++)
                ChargeNode(meter, node.Children[index], depth + 1);
            ChargeProvenance(meter, node.Provenance);
        }

        /// <summary>
        /// 充值 program decisions/nodes/edges 与 projection payload。
        /// </summary>
        private static void ChargeProgram(
            CanonicalSemanticBudgetMeter meter,
            CanonicalSemanticProgram program)
        {
            if (program == null)
                Fail("Canonical program cannot be null.");
            meter.ChargeString(program.ProgramId, "program id");
            ChargeProvenance(meter, program.Provenance);
            meter.ChargeCollection(program.Decisions.Count, "program decisions");
            meter.ChargeCollection(program.Nodes.Count, "program nodes");
            meter.ChargeCollection(program.Edges.Count, "program edges");
            for (var index = 0; index < program.Decisions.Count; index++)
                ChargeDecision(meter, program.Decisions[index]);
            for (var index = 0; index < program.Nodes.Count; index++)
                ChargeProgramNode(meter, program.Nodes[index]);
            for (var index = 0; index < program.Edges.Count; index++)
                ChargeProgramEdge(meter, program.Edges[index]);
        }

        /// <summary>
        /// 充值 contextual decision 的字段、规则与 provenance 字符串。
        /// </summary>
        private static void ChargeDecision(
            CanonicalSemanticBudgetMeter meter,
            CanonicalSemanticContextDecision decision)
        {
            if (decision == null)
                Fail("Canonical program decision cannot be null.");
            meter.ChargeString(decision.FieldId, "program decision field id");
            meter.ChargeString(decision.RuleId, "program decision rule id");
            ChargeProvenance(meter, decision.Provenance);
        }

        /// <summary>
        /// 充值 program node、两个 ordinal read set、projection 与 provenance。
        /// </summary>
        private static void ChargeProgramNode(
            CanonicalSemanticBudgetMeter meter,
            CanonicalSemanticProgramNode node)
        {
            if (node == null)
                Fail("Canonical program node cannot be null.");
            meter.ChargeNode(0);
            meter.ChargeString(node.OperationId, "program node operation id");
            meter.ChargeCollection(CountOf(node.FieldOrdinals), "program field ordinals");
            meter.ChargeCollection(CountOf(node.DependencyOrdinals), "program dependency ordinals");
            ChargeProjection(meter, node.Projection);
            ChargeProvenance(meter, node.Provenance);
        }

        /// <summary>
        /// 充值 projection 的复合 owner、全部字符串、payload 与 provenance。
        /// </summary>
        private static void ChargeProjection(
            CanonicalSemanticBudgetMeter meter,
            CanonicalSemanticProjectionBinding projection)
        {
            if (projection == null)
                Fail("Canonical projection cannot be null.");
            ChargeKey(meter, projection.OwnerDefinition);
            meter.ChargeString(projection.OperationId, "projection operation id");
            meter.ChargeString(projection.ValueView, "projection value view");
            meter.ChargeString(projection.Phase, "projection phase");
            meter.ChargeString(projection.TargetDomainId, "projection target domain");
            meter.ChargePayload(projection.PayloadUnsafe.Length, "projection payload");
            meter.ChargeString(projection.RuleId, "projection rule id");
            ChargeProvenance(meter, projection.Provenance);
        }

        /// <summary>
        /// 充值 program edge 与 governing provenance。
        /// </summary>
        private static void ChargeProgramEdge(
            CanonicalSemanticBudgetMeter meter,
            CanonicalSemanticProgramEdge edge)
        {
            if (edge == null)
                Fail("Canonical program edge cannot be null.");
            meter.ChargeEdge("program edge");
            ChargeProvenance(meter, edge.Provenance);
        }

        /// <summary>
        /// 充值 dependency 的字符串、target key、provenance 与 edge 总量。
        /// </summary>
        private static void ChargeDependency(
            CanonicalSemanticBudgetMeter meter,
            CanonicalSemanticDependency dependency)
        {
            if (dependency == null)
                Fail("Canonical dependency cannot be null.");
            meter.ChargeEdge("graph dependency");
            meter.ChargeString(dependency.DependencyId, "dependency id");
            ChargeKey(meter, dependency.TargetDefinition);
            meter.ChargeString(dependency.TargetDomainId, "dependency target domain");
            ChargeProvenance(meter, dependency.Provenance);
        }

        /// <summary>
        /// 充值 canonical scalar 的实际 string/blob/reference payload。
        /// </summary>
        private static void ChargeValue(
            CanonicalSemanticBudgetMeter meter,
            CanonicalSemanticValue value)
        {
            if (value == null)
                return;
            if (value.Text != null)
                meter.ChargeString(value.Text, "graph scalar text");
            var bytes = value.ByteValueUnsafe;
            if (bytes != null)
                meter.ChargePayload(bytes.Length, "graph scalar bytes");
            if (value.DefinitionReference != null)
                ChargeKey(meter, value.DefinitionReference);
        }

        /// <summary>
        /// 充值 source provenance 及 related definitions。
        /// </summary>
        private static void ChargeSourceProvenance(
            CanonicalSemanticBudgetMeter meter,
            LubanSemanticSourceProvenance provenance)
        {
            if (provenance == null)
                Fail("Source provenance cannot be null.");
            meter.ChargeString(provenance.WorkbookId, "source workbook");
            meter.ChargeString(provenance.TableId, "source table");
            meter.ChargeString(provenance.RowStableId, "source row");
            meter.ChargeString(provenance.FieldPath, "source field path");
            meter.ChargeString(provenance.RawValue, "source raw value");
            meter.ChargeString(provenance.NormalizedValue, "source normalized value");
            meter.ChargeString(provenance.RuleId, "source rule id");
            meter.ChargeString(provenance.GeneratorVersion, "source generator");
            meter.ChargeCollection(provenance.RelatedDefinitions.Count, "source related definitions");
            for (var index = 0; index < provenance.RelatedDefinitions.Count; index++)
                ChargeSourceId(meter, provenance.RelatedDefinitions[index]);
        }

        /// <summary>
        /// 充值 canonical provenance 及 related DefinitionKey。
        /// </summary>
        internal static void ChargeProvenance(
            CanonicalSemanticBudgetMeter meter,
            CanonicalSemanticProvenance provenance)
        {
            if (provenance == null)
                Fail("Canonical provenance cannot be null.");
            meter.ChargeString(provenance.WorkbookId, "graph workbook");
            meter.ChargeString(provenance.TableId, "graph table");
            meter.ChargeString(provenance.RowStableId, "graph row");
            meter.ChargeString(provenance.FieldPath, "graph field path");
            meter.ChargeString(provenance.RawValue, "graph raw value");
            meter.ChargeString(provenance.NormalizedValue, "graph normalized value");
            meter.ChargeString(provenance.RuleId, "graph rule id");
            meter.ChargeString(provenance.GeneratorVersion, "graph generator");
            meter.ChargeCollection(provenance.RelatedDefinitionIds.Count, "graph related definitions");
            for (var index = 0; index < provenance.RelatedDefinitionIds.Count; index++)
                ChargeKey(meter, provenance.RelatedDefinitionIds[index]);
        }

        /// <summary>
        /// 充值 source definition id 的 domain 与 stable-id parts。
        /// </summary>
        private static void ChargeSourceId(
            CanonicalSemanticBudgetMeter meter,
            LubanSemanticSourceDefinitionId definitionId)
        {
            if (definitionId == null)
                Fail("Source DefinitionId cannot be null.");
            meter.ChargeString(definitionId.StableDomainId, "source domain id");
            meter.ChargeCollection(definitionId.StableIdParts.Count, "source stable id parts");
        }

        /// <summary>
        /// 充值 canonical key 的 stable-id parts。
        /// </summary>
        internal static void ChargeKey(CanonicalSemanticBudgetMeter meter, CanonicalDefinitionKey key)
        {
            if (key == null)
                Fail("Canonical DefinitionKey cannot be null.");
            meter.ChargeCollection(key.StableIdParts.Count, "canonical stable id parts");
        }

        /// <summary>
        /// 充值 document/toolchain 根字符串。
        /// </summary>
        internal static void ChargeToolchain(
            CanonicalSemanticBudgetMeter meter,
            CanonicalSemanticToolchainIdentity toolchain,
            string generatorVersion,
            string sourceInputHash)
        {
            if (toolchain == null)
                Fail("Source toolchain cannot be null.");
            meter.ChargeString(toolchain.LubanProductVersion, "Luban product version");
            meter.ChargeString(toolchain.LubanBinaryHash, "Luban binary hash");
            meter.ChargeString(toolchain.SchemaInputHash, "schema input hash");
            meter.ChargeString(toolchain.GeneratorIdentity, "generator identity");
            meter.ChargeString(generatorVersion, "generator version");
            meter.ChargeString(sourceInputHash, "source input hash");
        }

        /// <summary>
        /// 在不分配替代集合的前提下取得可空列表 count。
        /// </summary>
        private static int CountOf<T>(IReadOnlyList<T> values)
        {
            return values == null ? 0 : values.Count;
        }

        /// <summary>
        /// 以稳定 CFG1501 拒绝任何预算越界。
        /// </summary>
        private static void Fail(string message)
        {
            throw new CanonicalSemanticValidationException(
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                CanonicalSemanticVersions.ResourceBudgetId + ": " + message);
        }
    }

    /// <summary>
    /// 累计一次预检的 strict UTF-8、payload、collection、node 与 edge 数量。
    /// </summary>
    internal sealed class CanonicalSemanticBudgetMeter
    {
        private long _stringBytes;
        private long _payloadBytes;
        private long _collectionItems;
        private long _nodes;
        private long _edges;

        /// <summary>
        /// 充值一个集合 count，在越界后立即停止。
        /// </summary>
        public void ChargeCollection(int count, string context)
        {
            CanonicalSemanticResourceBudget.RequireCollectionCount(count, context);
            _collectionItems = CheckedAdd(_collectionItems, count, context);
            if (_collectionItems > CanonicalSemanticResourceBudget.MaxTotalCollectionItems)
                Fail(context + " exceeds total collection item budget.");
        }

        /// <summary>
        /// 按实际 strict UTF-8 bytes 充值字符串。
        /// </summary>
        public void ChargeString(string value, string context)
        {
            var bytes = CanonicalSemanticResourceBudget.RequireString(value, context);
            ChargeStringBytes(bytes, context);
        }

        /// <summary>
        /// 在 reader 分配 string 前按 strict UTF-8 bytes 同时充值 string 与联合变长 payload。
        /// </summary>
        public void ChargeStringBytes(int bytes, string context)
        {
            if (bytes < 0 || bytes > CanonicalSemanticResourceBudget.MaxSingleStringBytes)
                Fail(context + " exceeds resource budget string bytes.");
            _stringBytes = CheckedAdd(_stringBytes, bytes, context);
            ChargePayload(bytes, context);
            if (_stringBytes > CanonicalSemanticResourceBudget.MaxTotalStringBytes)
                Fail(context + " exceeds total string byte budget.");
        }

        /// <summary>
        /// 充值联合变长 payload；string bytes 与 byte/projection bytes 共用该总内存上界。
        /// </summary>
        public void ChargePayload(int length, string context)
        {
            CanonicalSemanticResourceBudget.RequirePayloadLength(length, context);
            _payloadBytes = CheckedAdd(_payloadBytes, length, context);
            if (_payloadBytes > CanonicalSemanticResourceBudget.MaxTotalPayloadBytes)
                Fail(context + " exceeds total payload byte budget.");
        }

        /// <summary>
        /// 在进入 child 前充值一个 semantic/program node 与深度。
        /// </summary>
        public void ChargeNode(int depth)
        {
            if (depth < 0 || depth > CanonicalSemanticResourceBudget.MaxDepth)
                Fail("Semantic node depth exceeds resource budget.");
            _nodes = CheckedAdd(_nodes, 1, "semantic nodes");
            if (_nodes > CanonicalSemanticResourceBudget.MaxTotalNodes)
                Fail("Semantic node count exceeds resource budget.");
        }

        /// <summary>
        /// 充值一条 program/dependency edge。
        /// </summary>
        public void ChargeEdge(string context)
        {
            _edges = CheckedAdd(_edges, 1, context);
            if (_edges > CanonicalSemanticResourceBudget.MaxTotalEdges)
                Fail(context + " exceeds total edge budget.");
        }

        /// <summary>
        /// 以 checked long 累加预算，防止整数溢出绕过。
        /// </summary>
        private static long CheckedAdd(long current, long value, string context)
        {
            try
            {
                return checked(current + value);
            }
            catch (OverflowException)
            {
                Fail(context + " overflowed the resource budget counter.");
                return 0;
            }
        }

        /// <summary>
        /// 以稳定 CFG1501 拒绝累计预算越界。
        /// </summary>
        private static void Fail(string message)
        {
            throw new CanonicalSemanticValidationException(
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                CanonicalSemanticVersions.ResourceBudgetId + ": " + message);
        }
    }
}
