using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;

namespace GAS.Editor.CodeGen.Semantics
{
    /// <summary>
    /// 仅从已投影字段树全局推导 role、program 与 dependency，并在生成 graph 前执行 fail-closed 校验。
    /// </summary>
    internal static class CanonicalSemanticCompiler
    {
        /// <summary>
        /// 编译全部 drafts；source 调用方无法注入任何 role、readset、operation 或 dependency。
        /// </summary>
        public static IReadOnlyList<CanonicalSemanticDefinition> Compile(
            IReadOnlyList<SemanticDefinitionDraft> drafts,
            string generatorVersion)
        {
            var frozenDrafts = CanonicalSemanticResourceBudget.SnapshotList(
                drafts,
                "compiler draft input",
                false);
            CanonicalSemanticResourceBudget.ValidateDrafts(frozenDrafts);
            var byKey = IndexDrafts(frozenDrafts);
            ValidateEnumDomains(frozenDrafts);
            var roles = BuildRoles(frozenDrafts, byKey, generatorVersion);
            ValidatePriorityDeniedCues(frozenDrafts, roles);
            ValidateCooldownDurationOwnership(frozenDrafts, byKey);
            ValidateProgramTriggers(frozenDrafts, roles);
            var dependencyPathBudget = new CanonicalSemanticBudgetMeter();
            var dependencies = BuildDependencies(frozenDrafts, byKey, generatorVersion, dependencyPathBudget);
            ValidateDependencyCycles(frozenDrafts, dependencies);
            var projectionPayloadBudget = new CanonicalSemanticBudgetMeter();
            var result = new CanonicalSemanticDefinition[frozenDrafts.Length];
            for (var index = 0; index < frozenDrafts.Length; index++)
            {
                var draft = frozenDrafts[index];
                var compiledPrograms = BuildPrograms(
                    draft,
                    roles[draft.Key],
                    dependencies[draft.Key],
                    generatorVersion,
                    projectionPayloadBudget);
                result[index] = new CanonicalSemanticDefinition(
                    draft.DefinitionOrdinal,
                    draft.Key,
                    draft.Fields,
                    roles[draft.Key],
                    compiledPrograms,
                    dependencies[draft.Key].Dependencies,
                    draft.Provenance);
            }

            return result;
        }

        /// <summary>
        /// 建立 DefinitionKey 唯一索引；重复 key 已由 projector 提前返回双端 CFG1001。
        /// </summary>
        private static CanonicalDefinitionLookup<SemanticDefinitionDraft> IndexDrafts(
            IReadOnlyList<SemanticDefinitionDraft> drafts)
        {
            var keys = new CanonicalDefinitionKey[drafts.Count];
            for (var index = 0; index < drafts.Count; index++)
                keys[index] = drafts[index].Key;
            var result = new CanonicalDefinitionLookup<SemanticDefinitionDraft>(keys);
            for (var index = 0; index < drafts.Count; index++)
                result.Add(drafts[index].Key, drafts[index]);
            return result;
        }

        /// <summary>
        /// 验证所有 enum type id 的闭集数值，禁止合法 type id 携带任意整数。
        /// </summary>
        private static void ValidateEnumDomains(IReadOnlyList<SemanticDefinitionDraft> drafts)
        {
            for (var definitionIndex = 0; definitionIndex < drafts.Count; definitionIndex++)
            {
                var fields = drafts[definitionIndex].Fields;
                for (var fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                    ValidateEnumNode(fields[fieldIndex].Root);
            }
        }

        /// <summary>
        /// 递归验证 enum 节点及全部 children。
        /// </summary>
        private static void ValidateEnumNode(CanonicalSemanticNode node)
        {
            if (node.NodeKind == CanonicalSemanticNodeKind.Scalar
                && node.ScalarValue.Kind == CanonicalSemanticValueKind.Enum)
            {
                var value = node.ScalarValue.RawBits;
                if (!IsKnownEnumValue(node.TypeId, value))
                    CanonicalSemanticProjector.Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Enum value is outside its closed domain.", node.Provenance);
            }

            for (var index = 0; index < node.Children.Count; index++)
                ValidateEnumNode(node.Children[index]);
        }

        /// <summary>
        /// 判断真实 Ability/GameplayEffect schema 中闭集 enum 的合法数值范围。
        /// </summary>
        internal static bool IsKnownEnumValue(string typeId, ulong value)
        {
            switch (typeId)
            {
                case "exgas.RuntimeV1LogicalTargetPolicy": return value >= 1 && value <= 2;
                case "exgas.RuntimeV1AvatarTargetPolicy": return value >= 1 && value <= 2;
                case "exgas.RuntimeV1SpatialTargetPolicy": return value >= 1 && value <= 2;
                case "exgas.RuntimeV1TargetLifePolicy": return value >= 1 && value <= 3;
                case "exgas.RuntimeV1EvaluatorKind": return value <= 1;
                case "exgas.TimeUnit": return value <= 1;
                case "exgas.ModifierOperation": return value <= 4;
                case "exgas.GrantedAbilityActivationPolicy": return value <= 2;
                case "exgas.GrantedAbilityDeactivationPolicy": return value <= 1;
                case "exgas.GrantedAbilityRemovePolicy": return value <= 4;
                case "exgas.StackingType": return value <= 1;
                case "exgas.DurationRefreshPolicy": return value <= 1;
                case "exgas.PeriodResetPolicy": return value <= 1;
                case "exgas.ExpirationPolicy": return value <= 2;
                default: return false;
            }
        }

        /// <summary>
        /// 为每个 Definition 建立 Authoring role，并从 Ability Cost/CdEffect references 推导 incoming roles。
        /// </summary>
        private static CanonicalDefinitionLookup<IReadOnlyList<CanonicalSemanticRole>> BuildRoles(
            IReadOnlyList<SemanticDefinitionDraft> drafts,
            CanonicalDefinitionLookup<SemanticDefinitionDraft> byKey,
            string generatorVersion)
        {
            var candidates = byKey.CreateSibling<List<RoleCandidate>>();
            for (var index = 0; index < drafts.Count; index++)
            {
                candidates.Add(drafts[index].Key, new List<RoleCandidate>
                {
                    new RoleCandidate(CanonicalDefinitionRoleKind.Authoring, drafts[index].Key, -1, drafts[index].Provenance),
                });
            }

            for (var index = 0; index < drafts.Count; index++)
            {
                if (drafts[index].Key.DefinitionKind == CanonicalDefinitionKind.Ability)
                    AddAbilityRoles(drafts[index], candidates, byKey);
            }

            return FreezeRoles(candidates, generatorVersion);
        }

        /// <summary>
        /// 从 Ability.Cost 与 Ability.CdEffect 的 typed target-domain reference 推导 Cost/Cooldown role。
        /// </summary>
        private static void AddAbilityRoles(
            SemanticDefinitionDraft ability,
            CanonicalDefinitionLookup<List<RoleCandidate>> candidates,
            CanonicalDefinitionLookup<SemanticDefinitionDraft> byKey)
        {
            AddAbilityRole(ability, "Cost", CanonicalDefinitionRoleKind.Cost, candidates, byKey);
            AddAbilityRole(ability, "CdEffect", CanonicalDefinitionRoleKind.Cooldown, candidates, byKey);
        }

        /// <summary>
        /// 添加一个显式 Ability reference role，并立即拒绝错误 target domain 或悬空目标。
        /// </summary>
        private static void AddAbilityRole(
            SemanticDefinitionDraft ability,
            string fieldId,
            CanonicalDefinitionRoleKind roleKind,
            CanonicalDefinitionLookup<List<RoleCandidate>> candidates,
            CanonicalDefinitionLookup<SemanticDefinitionDraft> byKey)
        {
            var field = FindField(ability.Fields, fieldId);
            var target = TryGetReference(field.Root);
            if (target == null)
                return;
            if (target.DefinitionKind != CanonicalDefinitionKind.GameplayEffect || !byKey.ContainsKey(target))
                CanonicalSemanticProjector.Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Ability role target is dangling or wrong-domain.", field.Provenance);
            var targetRoles = candidates[target];
            CanonicalSemanticResourceBudget.RequireCollectionCount(
                checked(targetRoles.Count + 1),
                "compiler role candidates");
            targetRoles.Add(new RoleCandidate(roleKind, ability.Key, field.FieldOrdinal, field.Provenance));
        }

        /// <summary>
        /// 排序 role candidates、拒绝同一 GE 同时承担 Cost/Cooldown，并分配 RoleOrdinal。
        /// </summary>
        private static CanonicalDefinitionLookup<IReadOnlyList<CanonicalSemanticRole>> FreezeRoles(
            CanonicalDefinitionLookup<List<RoleCandidate>> candidates,
            string generatorVersion)
        {
            var result = candidates.CreateSibling<IReadOnlyList<CanonicalSemanticRole>>();
            for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                var key = candidates.GetKeyAt(candidateIndex);
                var values = candidates.GetValueAt(candidateIndex);
                values.Sort(RoleCandidate.Compare);
                RejectConflictingRoles(values);
                CanonicalSemanticResourceBudget.RequireCollectionCount(
                    values.Count,
                    "compiler frozen roles");
                var roles = new CanonicalSemanticRole[values.Count];
                for (var index = 0; index < values.Count; index++)
                    roles[index] = CreateRole(values[index], index, key, generatorVersion);
                result.Add(key, roles);
            }

            return result;
        }

        /// <summary>
        /// 拒绝同一 Definition 的任何第二个非 Authoring owner，并返回双方 provenance。
        /// </summary>
        private static void RejectConflictingRoles(IReadOnlyList<RoleCandidate> roles)
        {
            RoleCandidate owner = null;
            for (var index = 0; index < roles.Count; index++)
            {
                if (roles[index].RoleKind == CanonicalDefinitionRoleKind.Authoring)
                    continue;
                if (owner != null)
                    CanonicalSemanticProjector.FailConflict("Definition has multiple semantic owners.", owner.Provenance, roles[index].Provenance);
                owner = roles[index];
            }
        }

        /// <summary>
        /// 将 role candidate 转为带关联 owner key 的 canonical role。
        /// </summary>
        private static CanonicalSemanticRole CreateRole(
            RoleCandidate candidate,
            int ordinal,
            CanonicalDefinitionKey target,
            string generatorVersion)
        {
            var related = candidate.OwnerDefinition.HasSameIdentity(target)
                ? new[] { target }
                : new[] { candidate.OwnerDefinition, target };
            var provenance = CopyProvenance(
                candidate.Provenance,
                CanonicalSemanticRuleCatalog.InvalidDomainOrReference,
                "role:" + ((byte)candidate.RoleKind).ToString(CultureInfo.InvariantCulture),
                related,
                generatorVersion);
            return new CanonicalSemanticRole(
                ordinal,
                candidate.RoleKind,
                candidate.OwnerDefinition,
                candidate.OwnerFieldOrdinal,
                provenance.RuleId,
                provenance.RuleVersion,
                provenance);
        }

        /// <summary>
        /// 在任何 dangling reference 检查前执行 role contextual deny，固定 GE3001 的 CFG1101 主诊断。
        /// </summary>
        private static void ValidateProgramTriggers(
            IReadOnlyList<SemanticDefinitionDraft> drafts,
            CanonicalDefinitionLookup<IReadOnlyList<CanonicalSemanticRole>> roles)
        {
            for (var index = 0; index < drafts.Count; index++)
            {
                var draft = drafts[index];
                for (var roleIndex = 0; roleIndex < roles[draft.Key].Count; roleIndex++)
                {
                    var kind = roles[draft.Key][roleIndex].RoleKind;
                    if (kind == CanonicalDefinitionRoleKind.Cost)
                        ValidateAllowedProgram(draft, CanonicalProgramKind.CostMutation);
                    else if (kind == CanonicalDefinitionRoleKind.Cooldown)
                        ValidateAllowedProgram(draft, CanonicalProgramKind.CooldownGate);
                }
            }
        }

        /// <summary>
        /// 在 owner 冲突前先拒绝六类 active Cue，固定真实 GE3001 CueOnTick 的 CFG1101 主诊断。
        /// </summary>
        private static void ValidatePriorityDeniedCues(
            IReadOnlyList<SemanticDefinitionDraft> drafts,
            CanonicalDefinitionLookup<IReadOnlyList<CanonicalSemanticRole>> roles)
        {
            for (var index = 0; index < drafts.Count; index++)
            {
                var definitionRoles = roles[drafts[index].Key];
                for (var roleIndex = 0; roleIndex < definitionRoles.Count; roleIndex++)
                {
                    var kind = definitionRoles[roleIndex].RoleKind;
                    if (kind == CanonicalDefinitionRoleKind.Cost)
                        RejectActiveDeniedCues(drafts[index], CanonicalProgramKind.CostMutation);
                    else if (kind == CanonicalDefinitionRoleKind.Cooldown)
                        RejectActiveDeniedCues(drafts[index], CanonicalProgramKind.CooldownGate);
                }
            }
        }

        /// <summary>
        /// 由 definition 字段激活事实强制验证全部 contextual decisions 与必要 projection 字段。
        /// </summary>
        internal static void ValidateAllowedProgram(
            SemanticDefinitionDraft draft,
            CanonicalProgramKind programKind)
        {
            CanonicalSemanticSchemaRegistry.TryResolve(draft.Key.DefinitionKind, out var domain);
            domain.TryResolveProgram(programKind, out var programSchema);
            RejectActiveDeniedCues(draft, programKind);
            for (var index = 0; index < programSchema.FieldDecisions.Count; index++)
            {
                var decision = programSchema.FieldDecisions[index];
                var field = draft.Fields[decision.FieldOrdinal];
                if (IsActive(field.Root) && decision.Disposition == CanonicalFieldDisposition.Denied)
                    CanonicalSemanticProjector.Fail(decision.RuleId, "Active authoring field is denied in this role.", field.Provenance);
            }

            var required = programKind == CanonicalProgramKind.CostMutation ? "Modifiers" : "Duration";
            var requiredField = FindField(draft.Fields, required);
            if (!IsActive(requiredField.Root))
                CanonicalSemanticProjector.Fail(CanonicalSemanticRuleCatalog.MissingValueViewOrPhase, "Required projection field is absent.", requiredField.Provenance);
        }

        /// <summary>
        /// 按六类 Cue 固定顺序拒绝 active denied field，避免低 ordinal 字段改变诊断。
        /// </summary>
        private static void RejectActiveDeniedCues(
            SemanticDefinitionDraft draft,
            CanonicalProgramKind programKind)
        {
            CanonicalSemanticSchemaRegistry.TryResolve(draft.Key.DefinitionKind, out var domain);
            domain.TryResolveProgram(programKind, out var schema);
            for (var index = 0; index < schema.FieldDecisions.Count; index++)
            {
                var decision = schema.FieldDecisions[index];
                if (!decision.FieldId.StartsWith("CueOn", StringComparison.Ordinal))
                    continue;
                var field = draft.Fields[decision.FieldOrdinal];
                if (IsActive(field.Root) && decision.Disposition == CanonicalFieldDisposition.Denied)
                    CanonicalSemanticProjector.Fail(decision.RuleId, "Active Cue field is denied in this role.", field.Provenance);
            }
        }

        /// <summary>
        /// 在 role field 合法后拒绝 Ability.Cd 与 GE.Duration 的 cooldown duration 双 owner。
        /// </summary>
        internal static void ValidateCooldownDurationOwnership(
            IReadOnlyList<SemanticDefinitionDraft> drafts,
            CanonicalDefinitionLookup<SemanticDefinitionDraft> byKey)
        {
            for (var index = 0; index < drafts.Count; index++)
            {
                var ability = drafts[index];
                if (ability.Key.DefinitionKind != CanonicalDefinitionKind.Ability)
                    continue;
                var cd = FindField(ability.Fields, "Cd");
                var cdEffect = FindField(ability.Fields, "CdEffect");
                var target = TryGetReference(cdEffect.Root);
                if (unchecked((int)cd.Root.ScalarValue.RawBits) == 0 || target == null || !byKey.TryGetValue(target, out var effect))
                    continue;
                var duration = FindField(effect.Fields, "Duration");
                if (IsActive(duration.Root))
                    CanonicalSemanticProjector.FailConflict("Cooldown duration has two semantic owners.", cd.Provenance, duration.Provenance);
            }
        }

        /// <summary>
        /// 从所有显式 typed references 派生 dependencies，并在构造前拒绝 dangling/wrong-domain target。
        /// </summary>
        private static CanonicalDefinitionLookup<CompiledDependencySet> BuildDependencies(
            IReadOnlyList<SemanticDefinitionDraft> drafts,
            CanonicalDefinitionLookup<SemanticDefinitionDraft> byKey,
            string generatorVersion,
            CanonicalSemanticBudgetMeter pathBudget)
        {
            var result = byKey.CreateSibling<CompiledDependencySet>();
            for (var index = 0; index < drafts.Count; index++)
            {
                var candidates = new List<DependencyCandidate>();
                for (var fieldIndex = 0; fieldIndex < drafts[index].Fields.Count; fieldIndex++)
                {
                    var field = drafts[index].Fields[fieldIndex];
                    CollectReferences(field.Root, field, field.FieldId, candidates, pathBudget);
                }
                candidates.Sort(DependencyCandidate.Compare);
                result.Add(drafts[index].Key, FreezeDependencies(candidates, byKey, generatorVersion));
            }

            return result;
        }

        /// <summary>
        /// 递归收集 tree 中 reference，并以 canonical child ordinal 构造稳定 dependency id。
        /// </summary>
        private static void CollectReferences(
            CanonicalSemanticNode node,
            CanonicalSemanticField field,
            string path,
            ICollection<DependencyCandidate> result,
            CanonicalSemanticBudgetMeter pathBudget)
        {
            if (node.NodeKind == CanonicalSemanticNodeKind.Scalar
                && node.ScalarValue.Kind == CanonicalSemanticValueKind.DefinitionReference)
            {
                ResolveDependencyMetadata(field.FieldId, path, out var owner, out var sign, out var work, out var cleanup);
                CanonicalSemanticResourceBudget.RequireCollectionCount(
                    checked(result.Count + 1),
                    "compiler dependency candidates");
                result.Add(new DependencyCandidate(
                    field.FieldOrdinal,
                    path,
                    node.TargetDomainId,
                    node.ScalarValue.DefinitionReference,
                    owner,
                    sign,
                    work,
                    cleanup,
                    node.Provenance));
            }

            for (var index = 0; index < node.Children.Count; index++)
            {
                var child = node.Children[index];
                CollectReferences(child, field, AppendDependencyPath(path, child, pathBudget), result, pathBudget);
            }
        }

        /// <summary>
        /// 在分配下一层 dependency path 前计量完整 UTF-8 长度。
        /// </summary>
        internal static string AppendDependencyPath(
            string path,
            CanonicalSemanticNode child)
        {
            return AppendDependencyPath(path, child, new CanonicalSemanticBudgetMeter());
        }

        /// <summary>
        /// 在分配下一层 path 前同时执行单路径与整次编译的累计计量。
        /// </summary>
        internal static string AppendDependencyPath(
            string path,
            CanonicalSemanticNode child,
            CanonicalSemanticBudgetMeter pathBudget)
        {
            if (pathBudget == null)
                throw new ArgumentNullException(nameof(pathBudget));
            if (child.NodeId == "$element")
            {
                var ordinal = child.NodeOrdinal.ToString(CultureInfo.InvariantCulture);
                var bytes = CanonicalSemanticResourceBudget.RequireStringComposition(
                    "compiler dependency path", path, "[", ordinal, "]");
                pathBudget.ChargeStringBytes(bytes, "compiler dependency path aggregate");
                return string.Concat(path, "[", ordinal, "]");
            }

            var pathBytes = CanonicalSemanticResourceBudget.RequireStringComposition(
                "compiler dependency path", path, ".", child.NodeId);
            pathBudget.ChargeStringBytes(pathBytes, "compiler dependency path aggregate");
            return string.Concat(path, ".", child.NodeId);
        }

        /// <summary>
        /// 按真实字段路径推导 dependency owner/sign/work/cleanup metadata。
        /// </summary>
        internal static void ResolveDependencyMetadata(
            string fieldId,
            string path,
            out CanonicalDependencyOwnerKind owner,
            out CanonicalDependencySign sign,
            out CanonicalDependencyWorkKind work,
            out CanonicalCleanupPolicy cleanup)
        {
            owner = CanonicalDependencyOwnerKind.AuthoringField;
            sign = CanonicalDependencySign.Neutral;
            work = CanonicalDependencyWorkKind.Read;
            cleanup = CanonicalCleanupPolicy.None;
            if (fieldId == "Cost")
                SetMetadata(CanonicalDependencyOwnerKind.Cost, CanonicalDependencySign.Negative, CanonicalDependencyWorkKind.Mutation, CanonicalCleanupPolicy.None, out owner, out sign, out work, out cleanup);
            else if (fieldId == "CdEffect")
                SetMetadata(CanonicalDependencyOwnerKind.Cooldown, CanonicalDependencySign.Positive, CanonicalDependencyWorkKind.Mutation, CanonicalCleanupPolicy.GateExpiry, out owner, out sign, out work, out cleanup);
            else if (fieldId.StartsWith("CueOn", StringComparison.Ordinal))
                SetMetadata(CanonicalDependencyOwnerKind.Cue, CanonicalDependencySign.Neutral, CanonicalDependencyWorkKind.Cue, CanonicalCleanupPolicy.None, out owner, out sign, out work, out cleanup);
            else if (fieldId == "GrantedAbility")
                SetMetadata(CanonicalDependencyOwnerKind.GrantedAbility, CanonicalDependencySign.Positive, CanonicalDependencyWorkKind.Grant, CanonicalCleanupPolicy.Unsupported, out owner, out sign, out work, out cleanup);
            else if (fieldId == "Period")
                SetMetadata(CanonicalDependencyOwnerKind.Period, CanonicalDependencySign.Positive, CanonicalDependencyWorkKind.Application, CanonicalCleanupPolicy.ExactApplication, out owner, out sign, out work, out cleanup);
            else if (fieldId == "Stacking" && path.Contains("OverflowEffects"))
                SetMetadata(CanonicalDependencyOwnerKind.Overflow, CanonicalDependencySign.Positive, CanonicalDependencyWorkKind.Application, CanonicalCleanupPolicy.Unsupported, out owner, out sign, out work, out cleanup);
            else if (fieldId == "AbilityExecution")
                SetMetadata(CanonicalDependencyOwnerKind.Execution, CanonicalDependencySign.Positive, CanonicalDependencyWorkKind.Application, CanonicalCleanupPolicy.None, out owner, out sign, out work, out cleanup);
            else if (fieldId.Contains("Tags") || fieldId.Contains("Tag"))
                owner = CanonicalDependencyOwnerKind.Tag;
        }

        /// <summary>
        /// 集中写入 dependency metadata，避免分支遗漏任一维度。
        /// </summary>
        private static void SetMetadata(
            CanonicalDependencyOwnerKind ownerValue,
            CanonicalDependencySign signValue,
            CanonicalDependencyWorkKind workValue,
            CanonicalCleanupPolicy cleanupValue,
            out CanonicalDependencyOwnerKind owner,
            out CanonicalDependencySign sign,
            out CanonicalDependencyWorkKind work,
            out CanonicalCleanupPolicy cleanup)
        {
            owner = ownerValue;
            sign = signValue;
            work = workValue;
            cleanup = cleanupValue;
        }

        /// <summary>
        /// 校验 dependency targets 并冻结 ordinal、metadata 与 field→dependency 映射。
        /// </summary>
        private static CompiledDependencySet FreezeDependencies(
            IReadOnlyList<DependencyCandidate> candidates,
            CanonicalDefinitionLookup<SemanticDefinitionDraft> byKey,
            string generatorVersion)
        {
            CanonicalSemanticResourceBudget.RequireCollectionCount(
                candidates.Count,
                "compiler frozen dependencies");
            var dependencies = new CanonicalSemanticDependency[candidates.Count];
            var byField = new Dictionary<int, List<int>>();
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (!byKey.ContainsKey(candidate.TargetDefinition))
                    CanonicalSemanticProjector.Fail(CanonicalSemanticRuleCatalog.InvalidDomainOrReference, "Cross-definition reference is dangling.", candidate.Provenance);
                var normalized = ComputeDependencyEvidence(candidate.Path, candidate.TargetDefinition);
                var provenance = CopyProvenance(candidate.Provenance, CanonicalSemanticRuleCatalog.InvalidDomainOrReference, normalized,
                    new[] { candidate.TargetDefinition }, generatorVersion);
                dependencies[index] = new CanonicalSemanticDependency(
                    index,
                    candidate.Path,
                    candidate.SourceFieldOrdinal,
                    ResolveDependencyKind(candidate.Sign, candidate.WorkKind, candidate.CleanupPolicy),
                    candidate.TargetDefinition,
                    candidate.TargetDomainId,
                    candidate.OwnerKind,
                    candidate.Sign,
                    candidate.WorkKind,
                    candidate.CleanupPolicy,
                    1,
                    provenance);
                if (!byField.TryGetValue(candidate.SourceFieldOrdinal, out var ordinals))
                {
                    ordinals = new List<int>();
                    byField.Add(candidate.SourceFieldOrdinal, ordinals);
                }
                ordinals.Add(index);
            }

            return new CompiledDependencySet(dependencies, byField);
        }

        /// <summary>
        /// 按 kind/sign 组装 proof arcs，仅拒绝 Spec25 要求的负向或 cleanup/work 环。
        /// </summary>
        private static void ValidateDependencyCycles(
            IReadOnlyList<SemanticDefinitionDraft> drafts,
            CanonicalDefinitionLookup<CompiledDependencySet> dependencies)
        {
            var arcs = new List<CanonicalDependencyCycleArc>();
            for (var index = 0; index < drafts.Count; index++)
            {
                var source = drafts[index].Key;
                var sourceDependencies = dependencies[source].Dependencies;
                for (var edgeIndex = 0; edgeIndex < sourceDependencies.Count; edgeIndex++)
                    arcs.Add(new CanonicalDependencyCycleArc(source, sourceDependencies[edgeIndex]));
            }
            CanonicalSemanticDependencyCycleValidator.Validate(arcs.ToArray());
        }

        /// <summary>
        /// 由 sign 与已冻结 cleanup 元数据推导 dependency kind，禁止调用方注入。
        /// </summary>
        internal static CanonicalDependencyKind ResolveDependencyKind(
            CanonicalDependencySign sign,
            CanonicalDependencyWorkKind work,
            CanonicalCleanupPolicy cleanup)
        {
            if (sign == CanonicalDependencySign.Negative)
                return CanonicalDependencyKind.Negative;
            if (work == CanonicalDependencyWorkKind.Cleanup
                || (cleanup != CanonicalCleanupPolicy.None && cleanup != CanonicalCleanupPolicy.Unsupported))
                return CanonicalDependencyKind.Cleanup;
            return sign == CanonicalDependencySign.Positive
                ? CanonicalDependencyKind.Positive
                : CanonicalDependencyKind.Reference;
        }

        /// <summary>
        /// 为 Definition 的推导 roles 创建 allowed 或显式 denied programs，永不信任 caller readset。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticProgram> BuildPrograms(
            SemanticDefinitionDraft draft,
            IReadOnlyList<CanonicalSemanticRole> roles,
            CompiledDependencySet dependencies,
            string generatorVersion,
            CanonicalSemanticBudgetMeter payloadBudget)
        {
            var result = new List<CanonicalSemanticProgram>();
            for (var index = 0; index < roles.Count; index++)
            {
                if (roles[index].RoleKind == CanonicalDefinitionRoleKind.Cost)
                    result.Add(BuildProgram(draft, CanonicalProgramKind.CostMutation, result.Count, dependencies, generatorVersion, payloadBudget));
                else if (roles[index].RoleKind == CanonicalDefinitionRoleKind.Cooldown)
                    result.Add(BuildProgram(draft, CanonicalProgramKind.CooldownGate, result.Count, dependencies, generatorVersion, payloadBudget));
            }
            if (result.Count == 0 && (draft.Key.DefinitionKind == CanonicalDefinitionKind.Ability
                                      || draft.Key.DefinitionKind == CanonicalDefinitionKind.GameplayEffect))
                result.Add(BuildProgram(draft, CanonicalProgramKind.DirectEffect, result.Count, dependencies, generatorVersion, payloadBudget));
            if (draft.Key.DefinitionKind == CanonicalDefinitionKind.GameplayEffect
                && IsActive(FindField(draft.Fields, "Stacking").Root))
                result.Add(BuildProgram(draft, CanonicalProgramKind.StackTemporal, result.Count, dependencies, generatorVersion, payloadBudget));
            return result;
        }

        /// <summary>
        /// 从 registry program schema 生成完整 decisions、强制 nodes/order、bindings 与 edges。
        /// </summary>
        private static CanonicalSemanticProgram BuildProgram(
            SemanticDefinitionDraft draft,
            CanonicalProgramKind kind,
            int programOrdinal,
            CompiledDependencySet dependencies,
            string generatorVersion,
            CanonicalSemanticBudgetMeter payloadBudget)
        {
            CanonicalSemanticSchemaRegistry.TryResolve(draft.Key.DefinitionKind, out var domain);
            domain.TryResolveProgram(kind, out var schema);
            var decisions = BuildDecisions(draft, schema, generatorVersion);
            var nodes = schema.Disposition == CanonicalFieldDisposition.Allowed
                ? BuildNodes(draft, schema, programOrdinal, dependencies, generatorVersion, payloadBudget)
                : Array.Empty<CanonicalSemanticProgramNode>();
            var edges = BuildEdges(nodes, draft, generatorVersion);
            var programId = "sealed." + ((byte)kind).ToString(CultureInfo.InvariantCulture);
            var provenance = CopyProvenance(draft.Provenance, schema.RuleId, programId,
                Array.Empty<CanonicalDefinitionKey>(), generatorVersion);
            return new CanonicalSemanticProgram(
                programOrdinal,
                programId,
                kind,
                schema.Disposition,
                decisions,
                nodes,
                edges,
                provenance);
        }

        /// <summary>
        /// 将固定矩阵逐字段决策绑定到当前 Definition 的 active/presence 事实。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticContextDecision> BuildDecisions(
            SemanticDefinitionDraft draft,
            CanonicalSemanticProgramSchema schema,
            string generatorVersion)
        {
            var result = new CanonicalSemanticContextDecision[schema.FieldDecisions.Count];
            for (var index = 0; index < result.Length; index++)
            {
                var decision = schema.FieldDecisions[index];
                var field = draft.Fields[decision.FieldOrdinal];
                var provenance = CopyProvenance(field.Provenance, decision.RuleId,
                    "active:" + (IsActive(field.Root) ? "1" : "0"), field.Provenance.RelatedDefinitionIds, generatorVersion);
                result[index] = new CanonicalSemanticContextDecision(
                    decision.DecisionOrdinal,
                    decision.FieldOrdinal,
                    decision.FieldId,
                    IsActive(field.Root),
                    decision.Classification,
                    decision.Disposition,
                    decision.RuleId,
                    decision.RuleVersion,
                    provenance);
            }
            return result;
        }

        /// <summary>
        /// 按 operation schema 的固定 ordinal/order/read fields 创建 nodes 与 projection bindings。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticProgramNode> BuildNodes(
            SemanticDefinitionDraft draft,
            CanonicalSemanticProgramSchema schema,
            int programOrdinal,
            CompiledDependencySet dependencies,
            string generatorVersion,
            CanonicalSemanticBudgetMeter payloadBudget)
        {
            var result = new CanonicalSemanticProgramNode[schema.Operations.Count];
            for (var index = 0; index < result.Length; index++)
            {
                var operation = schema.Operations[index];
                var fieldOrdinals = ResolveFieldOrdinals(draft.Fields, operation.FieldIds);
                var dependencyOrdinals = dependencies.Resolve(fieldOrdinals);
                var payload = EncodeProjectionPayload(
                    operation,
                    draft.Fields,
                    fieldOrdinals,
                    dependencyOrdinals,
                    payloadBudget);
                var projection = CreateProjection(draft, operation, programOrdinal, index, fieldOrdinals, payload, generatorVersion);
                result[index] = new CanonicalSemanticProgramNode(
                    index,
                    operation.OperationId,
                    fieldOrdinals,
                    dependencyOrdinals,
                    projection,
                    projection.Provenance);
            }
            return result;
        }

        /// <summary>
        /// 创建 operation projection identity、bounds 与 payload binding。
        /// </summary>
        private static CanonicalSemanticProjectionBinding CreateProjection(
            SemanticDefinitionDraft draft,
            CanonicalSemanticOperationSchema operation,
            int programOrdinal,
            int nodeOrdinal,
            IReadOnlyList<int> fieldOrdinals,
            byte[] payload,
            string generatorVersion)
        {
            var boundCount = ResolveBoundCount(draft.Fields, operation.BoundFieldId);
            var maxBytes = payload.Length;
            if (maxBytes <= 0 || maxBytes > CanonicalBinaryReader.MaxByteLength)
                CanonicalSemanticProjector.Fail(CanonicalSemanticRuleCatalog.ProjectionUnbounded, "Projection payload has no finite codec bound.");
            var sourceFieldOrdinal = fieldOrdinals.Count == 0 ? -1 : fieldOrdinals[0];
            var sourceProvenance = sourceFieldOrdinal < 0 ? draft.Provenance : draft.Fields[sourceFieldOrdinal].Provenance;
            var provenance = CopyProvenance(sourceProvenance, operation.RuleId, ComputePayloadEvidence(payload),
                sourceProvenance.RelatedDefinitionIds, generatorVersion);
            return new CanonicalSemanticProjectionBinding(
                nodeOrdinal,
                draft.Key,
                programOrdinal,
                nodeOrdinal,
                sourceFieldOrdinal,
                operation.OperationId,
                operation.ValueView,
                operation.Phase,
                operation.TargetDomainId,
                boundCount,
                maxBytes,
                payload,
                operation.RuleId,
                operation.RuleVersion,
                provenance);
        }

        /// <summary>
        /// 编码 operation/readset/bounds 与完整字段 semantic tree，不含 provenance。
        /// </summary>
        internal static byte[] EncodeProjectionPayload(
            CanonicalSemanticOperationSchema operation,
            IReadOnlyList<CanonicalSemanticField> fields,
            IReadOnlyList<int> fieldOrdinals,
            IReadOnlyList<int> dependencyOrdinals)
        {
            return EncodeProjectionPayload(
                operation,
                fields,
                fieldOrdinals,
                dependencyOrdinals,
                new CanonicalSemanticBudgetMeter());
        }

        /// <summary>
        /// 先精确计数并累计整次编译的 projection 预算，再分配实体 payload。
        /// </summary>
        internal static byte[] EncodeProjectionPayload(
            CanonicalSemanticOperationSchema operation,
            IReadOnlyList<CanonicalSemanticField> fields,
            IReadOnlyList<int> fieldOrdinals,
            IReadOnlyList<int> dependencyOrdinals,
            CanonicalSemanticBudgetMeter payloadBudget)
        {
            if (payloadBudget == null)
                throw new ArgumentNullException(nameof(payloadBudget));
            var counter = CanonicalBinaryWriter.CreateCounting(
                CanonicalSemanticResourceBudget.MaxSinglePayloadBytes);
            WriteProjectionPayload(counter, operation, fields, fieldOrdinals, dependencyOrdinals);
            payloadBudget.ChargePayload(counter.Length, "compiler projection payload aggregate");
            var writer = new CanonicalBinaryWriter(counter.Length);
            WriteProjectionPayload(writer, operation, fields, fieldOrdinals, dependencyOrdinals);
            return writer.ToArray();
        }

        /// <summary>
        /// 以同一协议过程写入 projection，保证计数与实体编码逐字节一致。
        /// </summary>
        private static void WriteProjectionPayload(
            CanonicalBinaryWriter writer,
            CanonicalSemanticOperationSchema operation,
            IReadOnlyList<CanonicalSemanticField> fields,
            IReadOnlyList<int> fieldOrdinals,
            IReadOnlyList<int> dependencyOrdinals)
        {
            writer.WriteInt32(operation.OperationOrdinal);
            writer.WriteString(operation.OperationId);
            writer.WriteString(operation.ValueView);
            writer.WriteString(operation.Phase);
            writer.WriteString(operation.TargetDomainId);
            WriteOrdinals(writer, fieldOrdinals);
            WriteOrdinals(writer, dependencyOrdinals);
            writer.WriteCount(fieldOrdinals.Count);
            for (var index = 0; index < fieldOrdinals.Count; index++)
                WriteNodeValue(writer, fields[fieldOrdinals[index]].Root);
        }

        /// <summary>
        /// 写入不含 provenance 的 tree value，供 projection payload 与 content identity复用语义。
        /// </summary>
        internal static void WriteNodeValue(CanonicalBinaryWriter writer, CanonicalSemanticNode node)
        {
            writer.WriteByte((byte)node.NodeKind);
            writer.WriteString(node.NodeId);
            writer.WriteString(node.TypeId);
            writer.WriteString(node.TargetDomainId);
            writer.WriteByte((byte)node.CollectionSemantics);
            writer.WriteBoolean(node.ScalarValue != null);
            if (node.ScalarValue != null)
                CanonicalSemanticBinaryCodec.WriteScalarValue(writer, node.ScalarValue);
            writer.WriteCount(node.Children.Count);
            for (var index = 0; index < node.Children.Count; index++)
            {
                writer.WriteInt32(node.Children[index].NodeOrdinal);
                WriteNodeValue(writer, node.Children[index]);
            }
        }

        /// <summary>
        /// 为强制 operation 链创建连续 Control edges。
        /// </summary>
        private static IReadOnlyList<CanonicalSemanticProgramEdge> BuildEdges(
            IReadOnlyList<CanonicalSemanticProgramNode> nodes,
            SemanticDefinitionDraft draft,
            string generatorVersion)
        {
            if (nodes.Count <= 1)
                return Array.Empty<CanonicalSemanticProgramEdge>();
            var result = new CanonicalSemanticProgramEdge[nodes.Count - 1];
            for (var index = 0; index < result.Length; index++)
            {
                var provenance = CopyProvenance(draft.Provenance, CanonicalSemanticRuleCatalog.DependencyOrCleanupUnbounded,
                    index.ToString(CultureInfo.InvariantCulture) + "->" + (index + 1).ToString(CultureInfo.InvariantCulture),
                    Array.Empty<CanonicalDefinitionKey>(), generatorVersion);
                result[index] = new CanonicalSemanticProgramEdge(
                    index,
                    index,
                    index + 1,
                    CanonicalProgramEdgeKind.Control,
                    provenance);
            }
            return result;
        }

        /// <summary>
        /// 将固定 FieldIds 解析为 sorted/dense-free readset ordinals。
        /// </summary>
        private static IReadOnlyList<int> ResolveFieldOrdinals(
            IReadOnlyList<CanonicalSemanticField> fields,
            IReadOnlyList<string> fieldIds)
        {
            var result = new int[fieldIds.Count];
            for (var index = 0; index < fieldIds.Count; index++)
                result[index] = FindField(fields, fieldIds[index]).FieldOrdinal;
            Array.Sort(result);
            for (var index = 1; index < result.Length; index++)
                if (result[index] == result[index - 1])
                    CanonicalSemanticProjector.Fail(CanonicalSemanticRuleCatalog.SemanticOwnerConflict, "Operation field readset contains duplicate ordinal.");
            return result;
        }

        /// <summary>
        /// 从 bound source field 的 presence/collection count 推导静态 item 上界。
        /// </summary>
        internal static int ResolveBoundCount(
            IReadOnlyList<CanonicalSemanticField> fields,
            string boundFieldId)
        {
            if (string.IsNullOrEmpty(boundFieldId))
                return 1;
            var root = FindField(fields, boundFieldId).Root;
            if (root.NodeKind == CanonicalSemanticNodeKind.Absent)
                return 0;
            return root.NodeKind == CanonicalSemanticNodeKind.Collection ? root.Children.Count : 1;
        }

        /// <summary>
        /// 写入 ordinal 列表。
        /// </summary>
        private static void WriteOrdinals(CanonicalBinaryWriter writer, IReadOnlyList<int> ordinals)
        {
            writer.WriteCount(ordinals.Count);
            for (var index = 0; index < ordinals.Count; index++)
                writer.WriteInt32(ordinals[index]);
        }

        /// <summary>
        /// 判断字段 tree 是否触发角色/program；不使用 FirstPositive 或 scalar mirror。
        /// </summary>
        internal static bool IsActive(CanonicalSemanticNode node)
        {
            if (node.NodeKind == CanonicalSemanticNodeKind.Absent)
                return false;
            if (node.NodeKind == CanonicalSemanticNodeKind.Collection)
                return node.Children.Count != 0;
            if (node.NodeKind != CanonicalSemanticNodeKind.Scalar)
                return true;
            return true;
        }

        /// <summary>
        /// 返回 scalar reference 或 null，不从整数/字符串猜测引用。
        /// </summary>
        private static CanonicalDefinitionKey TryGetReference(CanonicalSemanticNode node)
        {
            return node.NodeKind == CanonicalSemanticNodeKind.Scalar
                   && node.ScalarValue != null
                   && node.ScalarValue.Kind == CanonicalSemanticValueKind.DefinitionReference
                ? node.ScalarValue.DefinitionReference
                : null;
        }

        /// <summary>
        /// 按 FieldId 查找字段；registry 已保证字段闭集。
        /// </summary>
        private static CanonicalSemanticField FindField(
            IReadOnlyList<CanonicalSemanticField> fields,
            string fieldId)
        {
            for (var index = 0; index < fields.Count; index++)
                if (string.Equals(fields[index].FieldId, fieldId, StringComparison.Ordinal))
                    return fields[index];
            CanonicalSemanticProjector.Fail(CanonicalSemanticRuleCatalog.UnsupportedContractField, "Compiler required field is absent from registry.");
            return null;
        }

        /// <summary>
        /// 复制 provenance 并替换 governing rule/normalized payload/related definitions。
        /// </summary>
        private static CanonicalSemanticProvenance CopyProvenance(
            CanonicalSemanticProvenance source,
            string ruleId,
            string normalized,
            IReadOnlyList<CanonicalDefinitionKey> related,
            string generatorVersion)
        {
            CanonicalSemanticRuleCatalog.TryResolve(ruleId, out var version, out _);
            return new CanonicalSemanticProvenance(
                source.WorkbookId,
                source.TableId,
                source.RowStableId,
                source.FieldPath,
                source.RawValue,
                normalized,
                ruleId,
                version,
                generatorVersion,
                related);
        }

        /// <summary>
        /// 将 projection bytes 压缩为固定长度 SHA-256 provenance evidence。
        /// </summary>
        internal static string ComputePayloadEvidence(byte[] bytes)
        {
            CanonicalSemanticResourceBudget.RequirePayloadLength(
                bytes.Length,
                "projection provenance payload");
            using (var sha256 = SHA256.Create())
                return "sha256:" + EncodeHex(sha256.ComputeHash(bytes));
        }

        /// <summary>
        /// 以 bounded canonical binary 输入计算 dependency path/target 的固定长度 evidence。
        /// </summary>
        private static string ComputeDependencyEvidence(
            string path,
            CanonicalDefinitionKey target)
        {
            CanonicalSemanticResourceBudget.RequireString(path, "dependency evidence path");
            var writer = new CanonicalBinaryWriter();
            writer.WriteString(path);
            writer.WriteInt32(target.DomainOrdinal);
            writer.WriteByte((byte)target.DefinitionKind);
            writer.WriteCount(target.StableIdParts.Count);
            for (var index = 0; index < target.StableIdParts.Count; index++)
                writer.WriteInt64(target.StableIdParts[index]);
            return ComputePayloadEvidence(writer.ToArray());
        }

        /// <summary>
        /// 将固定长度 digest 编码为文化无关小写十六进制。
        /// </summary>
        private static string EncodeHex(byte[] bytes)
        {
            const string digits = "0123456789abcdef";
            var characters = new char[bytes.Length * 2];
            for (var index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = digits[bytes[index] >> 4];
                characters[index * 2 + 1] = digits[bytes[index] & 15];
            }
            return new string(characters);
        }

        /// <summary>
        /// 暂存一个待排序 role 与双方 owner evidence。
        /// </summary>
        private sealed class RoleCandidate
        {
            /// <summary>
            /// 创建 role candidate。
            /// </summary>
            public RoleCandidate(
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
            /// 按 role kind、owner key 与 owner field ordinal 排序。
            /// </summary>
            public static int Compare(RoleCandidate left, RoleCandidate right)
            {
                var result = ((byte)left.RoleKind).CompareTo((byte)right.RoleKind);
                if (result != 0)
                    return result;
                result = left.OwnerDefinition.CompareTo(right.OwnerDefinition);
                return result != 0 ? result : left.OwnerFieldOrdinal.CompareTo(right.OwnerFieldOrdinal);
            }
        }

        /// <summary>
        /// 暂存一个从 tree reference 推导的 dependency candidate。
        /// </summary>
        private sealed class DependencyCandidate
        {
            /// <summary>
            /// 创建完整 metadata 的 dependency candidate。
            /// </summary>
            public DependencyCandidate(
                int sourceFieldOrdinal,
                string path,
                string targetDomainId,
                CanonicalDefinitionKey targetDefinition,
                CanonicalDependencyOwnerKind ownerKind,
                CanonicalDependencySign sign,
                CanonicalDependencyWorkKind workKind,
                CanonicalCleanupPolicy cleanupPolicy,
                CanonicalSemanticProvenance provenance)
            {
                SourceFieldOrdinal = sourceFieldOrdinal;
                Path = path;
                TargetDomainId = targetDomainId;
                TargetDefinition = targetDefinition;
                OwnerKind = ownerKind;
                Sign = sign;
                WorkKind = workKind;
                CleanupPolicy = cleanupPolicy;
                Provenance = provenance;
            }

            public int SourceFieldOrdinal { get; }

            public string Path { get; }

            public string TargetDomainId { get; }

            public CanonicalDefinitionKey TargetDefinition { get; }

            public CanonicalDependencyOwnerKind OwnerKind { get; }

            public CanonicalDependencySign Sign { get; }

            public CanonicalDependencyWorkKind WorkKind { get; }

            public CanonicalCleanupPolicy CleanupPolicy { get; }

            public CanonicalSemanticProvenance Provenance { get; }

            /// <summary>
            /// 按 source field/path/target key 建立稳定 dependency ordinal。
            /// </summary>
            public static int Compare(DependencyCandidate left, DependencyCandidate right)
            {
                var result = left.SourceFieldOrdinal.CompareTo(right.SourceFieldOrdinal);
                if (result != 0)
                    return result;
                result = string.CompareOrdinal(left.Path, right.Path);
                return result != 0 ? result : left.TargetDefinition.CompareTo(right.TargetDefinition);
            }
        }

        /// <summary>
        /// 保存冻结 dependencies 及 source field 到 dependency ordinal 的 sealed 映射。
        /// </summary>
        private sealed class CompiledDependencySet
        {
            private readonly IReadOnlyDictionary<int, List<int>> _byField;

            /// <summary>
            /// 创建 dependency 集及只供 compiler 使用的 readset 索引。
            /// </summary>
            public CompiledDependencySet(
                IReadOnlyList<CanonicalSemanticDependency> dependencies,
                IReadOnlyDictionary<int, List<int>> byField)
            {
                Dependencies = dependencies;
                _byField = byField;
            }

            public IReadOnlyList<CanonicalSemanticDependency> Dependencies { get; }

            /// <summary>
            /// 汇总一组 field ordinals 对应的 dependency ordinals并排序去重。
            /// </summary>
            public IReadOnlyList<int> Resolve(IReadOnlyList<int> fieldOrdinals)
            {
                var result = new List<int>();
                for (var index = 0; index < fieldOrdinals.Count; index++)
                    if (_byField.TryGetValue(fieldOrdinals[index], out var values))
                        result.AddRange(values);
                result.Sort();
                for (var index = result.Count - 1; index > 0; index--)
                    if (result[index] == result[index - 1])
                        result.RemoveAt(index);
                return result;
            }
        }
    }
}
