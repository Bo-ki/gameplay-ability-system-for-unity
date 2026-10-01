using System;
using System.Collections.Generic;
using GAS.Runtime;

namespace GAS.Editor.CodeGen.Proofs
{
    /// <summary>
    /// 保存一次 CapacityProof 构建的 canonical 输入、聚合上界、推导和 Red，供分离的算法阶段共享。
    /// </summary>
    internal sealed class GasCapacityProofBuildState
    {
        public readonly IGasCapacityProofSource Source;
        public readonly GasRuntimeV1LayoutProofPayload Layout;
        public readonly GasRuntimeV1CapacityProofPayload Payload;
        public readonly List<GasCapacityBoundProofEntry> Bounds = new List<GasCapacityBoundProofEntry>();
        public readonly List<GasProofDerivationEntry> Derivations = new List<GasProofDerivationEntry>();
        public readonly List<GasRuntimeCapacityLimitEntry> RuntimeLimits = new List<GasRuntimeCapacityLimitEntry>();
        public readonly List<GasProofConsumerEntry> Consumers = new List<GasProofConsumerEntry>();
        public readonly List<GasCapacityMemoryEvidence> Memory = new List<GasCapacityMemoryEvidence>();
        public readonly List<GasProofFailure> Failures = new List<GasProofFailure>();
        public readonly long[] MaximumByDimension;
        public readonly GasCapacityBoundProofEntry[] MaximumBoundByDimension;
        public GasProofEncodingBudget SourceEncodingBudget;
        public GasCapacityDefinitionIdentityProofEntry[] DefinitionIdentities =
            Array.Empty<GasCapacityDefinitionIdentityProofEntry>();

        /// <summary>
        /// 创建一次 build state，并为 enum 全域分配固定 maxima 索引。
        /// </summary>
        public GasCapacityProofBuildState(
            IGasCapacityProofSource source,
            GasRuntimeV1LayoutProofPayload layout,
            GasRuntimeV1CapacityProofPayload payload)
        {
            Source = source;
            Layout = layout;
            Payload = payload;
            MaximumByDimension = new long[GasRuntimeV1CapacityProofBuilder.DimensionCount + 1];
            MaximumBoundByDimension =
                new GasCapacityBoundProofEntry[GasRuntimeV1CapacityProofBuilder.DimensionCount + 1];
        }

        /// <summary>
        /// 返回指定 semantic dimension 在全部 Definition 上的最大显式上界。
        /// </summary>
        public long Maximum(GasCapacityDimension dimension)
        {
            return MaximumByDimension[(int)dimension];
        }

        /// <summary>
        /// 返回驱动跨 Definition 最大值的精确 bound 及 provenance。
        /// </summary>
        public GasCapacityBoundProofEntry MaximumBound(GasCapacityDimension dimension)
        {
            return MaximumBoundByDimension[(int)dimension];
        }

        /// <summary>
        /// 返回一个 Definition 的指定 bound；payload 采用 definition-major、dimension-minor canonical 顺序。
        /// </summary>
        public GasCapacityBoundProofEntry Bound(int definitionOrdinal, GasCapacityDimension dimension)
        {
            var index = checked(definitionOrdinal * GasRuntimeV1CapacityProofBuilder.DimensionCount +
                                ((int)dimension - 1));
            return Bounds[index];
        }

        /// <summary>
        /// 执行 checked 乘法并同时记录成功 derivation 或带操作数的稳定 Red。
        /// </summary>
        public bool TryMultiply(
            string derivationId,
            string dimensionId,
            string leftName,
            long left,
            string rightName,
            long right,
            string unit,
            in GasProofProvenance provenance,
            int definitionId,
            out long result,
            string definitionKey = "")
        {
            var succeeded = GasCheckedProofMath.TryMultiply(left, right, out result);
            RecordDerivation(derivationId, dimensionId, "*", leftName, left,
                rightName, right, result, unit, definitionId, provenance, definitionKey);
            if (!succeeded)
                AddFailure(left < 0 || right < 0
                        ? GasProofFailureKind.CapacityBoundInvalid
                        : GasProofFailureKind.ArithmeticOverflow,
                    GasProofRuleIds.CapacityProofMissing, dimensionId, derivationId,
                    "*", leftName, left, rightName, right, long.MaxValue, 0,
                    unit, definitionId, -1, string.Empty, provenance, definitionKey);
            return succeeded;
        }

        /// <summary>
        /// 执行 checked 加法并同时记录成功 derivation 或带操作数的稳定 Red。
        /// </summary>
        public bool TryAdd(
            string derivationId,
            string dimensionId,
            string leftName,
            long left,
            string rightName,
            long right,
            string unit,
            in GasProofProvenance provenance,
            int definitionId,
            out long result,
            string definitionKey = "")
        {
            var succeeded = GasCheckedProofMath.TryAdd(left, right, out result);
            RecordDerivation(derivationId, dimensionId, "+", leftName, left,
                rightName, right, result, unit, definitionId, provenance, definitionKey);
            if (!succeeded)
                AddFailure(left < 0 || right < 0
                        ? GasProofFailureKind.CapacityBoundInvalid
                        : GasProofFailureKind.ArithmeticOverflow,
                    GasProofRuleIds.CapacityProofMissing, dimensionId, derivationId,
                    "+", leftName, left, rightName, right, long.MaxValue, 0,
                    unit, definitionId, -1, string.Empty, provenance, definitionKey);
            return succeeded;
        }

        /// <summary>
        /// 添加一条实际需求与 Runtime/Profile 可用容量的稳定对账，并在超限时 fail-closed。
        /// </summary>
        public void AddLimit(
            string limitId,
            string profileField,
            string formula,
            long required,
            long available,
            string unit,
            GasProofConsumerStatus consumerStatus,
            string firstConsumer,
            in GasProofProvenance provenance,
            int definitionId = 0,
            string definitionKey = "")
        {
            RuntimeLimits.Add(new GasRuntimeCapacityLimitEntry
            {
                LimitId = limitId,
                ProfileField = profileField,
                Formula = formula,
                Required = required,
                Available = available,
                Unit = unit,
                ConsumerStatus = consumerStatus,
                FirstConsumer = firstConsumer,
            });
            if (available >= 0 && required > available)
                AddFailure(GasProofFailureKind.ProfileCapacityExceeded,
                    GasProofRuleIds.CapacityProofMissing, limitId, limitId,
                    "<=", "Required", required, profileField, available,
                    available, required, unit, definitionId, -1, firstConsumer,
                    provenance, definitionKey);
        }

        /// <summary>
        /// 追加一条完整 Capacity Red；排序在最终 hash 前统一完成。
        /// </summary>
        public void AddFailure(
            GasProofFailureKind kind,
            string ruleId,
            string dimensionId,
            string derivationId,
            string operation,
            string leftName,
            long left,
            string rightName,
            long right,
            long expected,
            long actual,
            string unit,
            int definitionId,
            int ordinal,
            string firstConsumer,
            in GasProofProvenance provenance,
            string definitionKey = "",
            string expectedText = "",
            string actualText = "")
        {
            Failures.Add(new GasProofFailure
            {
                ProofKind = GasProofKind.Capacity,
                FailureKind = kind,
                RuleId = ruleId,
                DimensionId = dimensionId,
                DerivationId = derivationId,
                Operator = operation,
                LeftOperandName = leftName,
                LeftOperandValue = left,
                RightOperandName = rightName,
                RightOperandValue = right,
                ExpectedMaximum = expected,
                ActualValue = actual,
                ExpectedText = expectedText,
                ActualText = actualText,
                Unit = unit,
                DefinitionId = definitionId,
                DefinitionKey = definitionKey,
                ProfileId = Payload.Profile.ProfileId,
                CanonicalOrdinal = ordinal,
                FirstConsumer = firstConsumer,
                Provenance = provenance,
            });
        }

        /// <summary>
        /// 记录一次成功或失败均可重演的二元 checked derivation。
        /// </summary>
        private void RecordDerivation(
            string derivationId,
            string dimensionId,
            string operation,
            string leftName,
            long left,
            string rightName,
            long right,
            long result,
            string unit,
            int definitionId,
            in GasProofProvenance provenance,
            string definitionKey)
        {
            Derivations.Add(new GasProofDerivationEntry
            {
                DerivationId = derivationId,
                DimensionId = dimensionId,
                Expression = leftName + " " + operation + " " + rightName,
                Operator = operation,
                LeftOperandName = leftName,
                LeftOperandValue = left,
                RightOperandName = rightName,
                RightOperandValue = right,
                Result = result,
                Unit = unit,
                DefinitionId = definitionId,
                DefinitionKey = definitionKey,
                ProfileId = Payload.Profile.ProfileId,
                CanonicalOrdinal = -1,
                Provenance = provenance,
            });
        }
    }

    /// <summary>
    /// 将 canonical semantic bounds、LayoutProof 与 GasScaleProfile 组合成 Runtime v1 CapacityProof。
    /// </summary>
    public static class GasRuntimeV1CapacityProofBuilder
    {
        internal const int DimensionCount = (int)GasCapacityDimension.DynamicDependencyBackEdges;
        private const int ConservativeDefinitionIdentityBuildBytes = 2048;
        private const int ConservativeBoundBuildBytes = 2048;
        private const int ConservativePayloadEntryEncodingBytes = 512;
        private const GasCapacityProofCoverageGap CurrentCoverageGaps =
            GasCapacityProofCoverageGap.CanonicalGraphAdapterMissing |
            GasCapacityProofCoverageGap.SemanticDimensionModelIncomplete |
            GasCapacityProofCoverageGap.RuntimeRouteFormulaIncomplete |
            GasCapacityProofCoverageGap.RuntimeFactFormulaIncomplete |
            GasCapacityProofCoverageGap.AuthorityConsumerMissing |
            GasCapacityProofCoverageGap.ProvenanceBindingIncomplete;

        /// <summary>
        /// 构建 CapacityProof payload；返回值只代表 proof 闭合，不修改 Pipeline、Store 或发布资格。
        /// </summary>
        public static bool TryBuild(
            IGasCapacityProofSource source,
            GasRuntimeV1LayoutProofPayload layoutProof,
            out GasRuntimeV1CapacityProofPayload payload)
        {
            payload = Build(source, layoutProof);
            return payload.Succeeded;
        }

        /// <summary>
        /// 重新编码校验 CapacityProof 快照，任何 identity、profile、bound、证据或 Red 篡改都 fail-closed。
        /// </summary>
        public static bool TryVerifyPayload(
            GasRuntimeV1CapacityProofPayload payload,
            out GasProofFailure failure)
        {
            failure = default;
            if (!HasCompletePayloadArrays(payload))
            {
                failure = IntegrityFailure("CapacityPayload", string.Empty, string.Empty);
                return false;
            }
            if (payload.ProofSchemaVersion != GasRuntimeV1ProofInventory.CapacityProofSchemaVersion ||
                payload.AlgorithmVersion != GasRuntimeV1ProofInventory.CapacityAlgorithmVersion)
            {
                failure = IntegrityFailure("CapacityProofSchema",
                    GasRuntimeV1ProofInventory.CapacityAlgorithmVersion,
                    payload.AlgorithmVersion);
                return false;
            }
            var expectedAbiHash = GasRuntimeV1CapacityProofHasher.ComputeLayoutAbiHash(
                payload.MemoryEvidence);
            if (!VerifyHash(payload.LayoutAbiHash, expectedAbiHash,
                    "CapacityInventoryAbiHash", out failure) ||
                !VerifyHash(payload.ProofHash,
                    GasRuntimeV1CapacityProofHasher.ComputeProofHash(payload),
                    "CapacityProofHash", out failure))
                return false;
            return true;
        }

        /// <summary>
        /// 构建包含 checked 推导、consumer map、字节证据与稳定失败 provenance 的完整 payload。
        /// </summary>
        public static GasRuntimeV1CapacityProofPayload Build(
            IGasCapacityProofSource source,
            GasRuntimeV1LayoutProofPayload layoutProof)
        {
            var payload = CreatePayload(source, layoutProof);
            var state = new GasCapacityProofBuildState(source, layoutProof, payload);
            ValidateInputs(state);
            if (source != null && TryValidateBuildBudget(
                    state, out var definitionCount, out var boundCount))
            {
                if (ReadBounds(state, definitionCount, boundCount))
                {
                    ValidateDefinitions(state);
                    BuildRuntimeLimits(state);
                }
            }
            BuildCoverageEvidence(state);
            BuildConsumerEvidence(state);
            GasRuntimeV1CapacityMemoryInventory.Build(state);
            GasRuntimeV1CapacityProofHasher.Finalize(state);
            return payload;
        }

        /// <summary>
        /// 创建绑定 graph、contract、layout、target scale 与完整 profile 快照的空 payload。
        /// </summary>
        private static GasRuntimeV1CapacityProofPayload CreatePayload(
            IGasCapacityProofSource source,
            GasRuntimeV1LayoutProofPayload layoutProof)
        {
            return new GasRuntimeV1CapacityProofPayload
            {
                ProofSchemaVersion = GasRuntimeV1ProofInventory.CapacityProofSchemaVersion,
                AlgorithmVersion = GasRuntimeV1ProofInventory.CapacityAlgorithmVersion,
                GraphSchemaVersion = source == null ? 0 : source.GraphSchemaVersion,
                GeneratorVersion = source == null ||
                                   !GasProofCanonicalHashWriter.IsWithinStringBudget(
                                       source.GeneratorVersion)
                    ? string.Empty
                    : source.GeneratorVersion ?? string.Empty,
                CanonicalGraphHash = source == null
                    ? string.Empty
                    : GasProofCanonicalHashWriter.NormalizeSha256(source.CanonicalGraphHash),
                ContractMatrixHash = source == null
                    ? string.Empty
                    : GasProofCanonicalHashWriter.NormalizeSha256(source.ContractMatrixHash),
                TargetScaleId = source == null ||
                                !GasProofCanonicalHashWriter.IsWithinStringBudget(
                                    source.TargetScaleId)
                    ? string.Empty
                    : source.TargetScaleId ?? string.Empty,
                LayoutHash = layoutProof == null ? string.Empty : layoutProof.LayoutHash ?? string.Empty,
                Profile = source == null ? default : source.ScaleProfile,
                ScaleProfileProvenance = source == null ? default : source.ScaleProfileProvenance,
                MemoryBudgetProvenance = source == null ? default : source.MemoryBudgetProvenance,
                CoverageGaps = CurrentCoverageGaps,
            };
        }

        /// <summary>
        /// 验证 source、LayoutProof、四项 identity 与 ScaleProfile，不因 Red 跳过后续可计算证据。
        /// </summary>
        private static void ValidateInputs(GasCapacityProofBuildState state)
        {
            if (state.Source == null)
            {
                state.AddFailure(GasProofFailureKind.InputUnavailable,
                    GasProofRuleIds.CapacityProofMissing, "CapacitySource", string.Empty,
                    string.Empty, string.Empty, 0, string.Empty, 0, 0, 0,
                    "source", 0, -1, string.Empty, default);
                return;
            }

            ValidateIdentity(state);
            ValidateLayout(state);
            ValidateProfile(state);
            ValidateMemoryBudgetProvenance(state);
        }

        /// <summary>
        /// 要求内存预算来源本身具有完整 canonical identity，不依赖后续 failure 间接绑定。
        /// </summary>
        private static void ValidateMemoryBudgetProvenance(GasCapacityProofBuildState state)
        {
            var provenance = state.Payload.MemoryBudgetProvenance;
            if (GasProofCanonicalRules.IsCanonicalProvenance(in provenance))
                return;
            state.AddFailure(GasProofFailureKind.ProvenanceMissing,
                GasProofRuleIds.NonCanonicalIdentity, "MemoryBudgetBytes", string.Empty,
                "canonical", "ProvenanceOrdinal", provenance.ProvenanceOrdinal,
                "RequiredMinimum", 0, 0, provenance.ProvenanceOrdinal,
                "provenance", 0, -1, "Scale/Profile release gate", provenance);
        }

        /// <summary>
        /// 验证 canonical graph、contract matrix、generator 与 target-scale identity。
        /// </summary>
        private static void ValidateIdentity(GasCapacityProofBuildState state)
        {
            var source = state.Source;
            if (source.GraphSchemaVersion > 0 &&
                !string.IsNullOrWhiteSpace(source.GeneratorVersion) &&
                !string.IsNullOrWhiteSpace(source.TargetScaleId) &&
                GasProofCanonicalHashWriter.IsWithinStringBudget(source.GeneratorVersion) &&
                GasProofCanonicalHashWriter.IsWithinStringBudget(source.TargetScaleId) &&
                GasProofCanonicalHashWriter.IsSha256Hex(source.CanonicalGraphHash) &&
                GasProofCanonicalHashWriter.IsSha256Hex(source.ContractMatrixHash))
                return;

            state.AddFailure(GasProofFailureKind.IdentityMissing,
                GasProofRuleIds.NonCanonicalIdentity, "CapacityIdentity", string.Empty,
                string.Empty, "GraphSchemaVersion", source.GraphSchemaVersion,
                "TargetScaleIdLength", source.TargetScaleId == null ? 0 : source.TargetScaleId.Length,
                1, source.GraphSchemaVersion, "identity", 0, -1, string.Empty,
                source.ScaleProfileProvenance);
        }

        /// <summary>
        /// 验证 LayoutProof 成功且与 Capacity source 绑定同一个 canonical graph。
        /// </summary>
        private static void ValidateLayout(GasCapacityProofBuildState state)
        {
            var normalizedGraphHash = GasProofCanonicalHashWriter.NormalizeSha256(
                state.Source.CanonicalGraphHash);
            var verified = GasRuntimeV1LayoutProofBuilder.TryVerifyPayload(
                state.Layout, out var integrityFailure);
            if (verified && state.Layout.Succeeded &&
                string.Equals(normalizedGraphHash, state.Layout.CanonicalGraphHash,
                    StringComparison.Ordinal))
                return;

            var kind = verified
                ? GasProofFailureKind.LayoutProofFailed
                : GasProofFailureKind.ProofHashMismatch;
            state.AddFailure(kind,
                verified ? GasProofRuleIds.CapacityProofMissing : GasProofRuleIds.NonCanonicalIdentity,
                "LayoutProof", string.Empty,
                "==", "CapacityGraphHash", 0, "LayoutGraphHash", 0,
                0, 0, "hash", 0, -1, "Runtime catalog install",
                state.Source.ScaleProfileProvenance, string.Empty,
                verified ? normalizedGraphHash : integrityFailure.ExpectedText,
                verified ? state.Layout.CanonicalGraphHash : integrityFailure.ActualText);
        }

        /// <summary>
        /// 镜像 Runtime v1 profile 的合法性条件并要求完整 profile provenance。
        /// </summary>
        private static void ValidateProfile(GasCapacityProofBuildState state)
        {
            var profile = state.Payload.Profile;
            var valid = profile.ProfileId > 0 && profile.ProfileVersion > 0 &&
                        profile.ProfileHash != 0 && profile.MaxFixedTicksPerBatch > 0 &&
                        profile.MaximumDeltaTimeTicks > 0 &&
                        profile.MaximumDeltaTimeTicks <= profile.MaxFixedTicksPerBatch &&
                        GasProofCanonicalRules.IsCanonicalProvenance(
                            in state.Payload.ScaleProfileProvenance);
            foreach (var field in CreateNonNegativeProfileFields(in profile))
                valid &= field.Value >= 0;
            if (valid)
                return;

            state.AddFailure(GasProofFailureKind.ProfileInvalid,
                GasProofRuleIds.CapacityProofMissing, "GasScaleProfile", string.Empty,
                "validate", "ProfileId", profile.ProfileId,
                "ProfileVersion", profile.ProfileVersion, 1, 0,
                "profile", 0, -1, "SpawnFinalize/Profile validation",
                state.Payload.ScaleProfileProvenance);
        }

        /// <summary>
        /// 返回 Runtime v1 要求非负的全部 ScaleProfile 容量字段。
        /// </summary>
        private static ProfileField[] CreateNonNegativeProfileFields(in GasScaleProfile profile)
        {
            return new[]
            {
                Field("MaxSpawnBatchSize", profile.MaxSpawnBatchSize),
                Field("MaxBattleInstanceCount", profile.MaxBattleInstanceCount),
                Field("MaxAscRegistryCount", profile.MaxAscRegistryCount),
                Field("MaxBoundaryCommandCount", profile.MaxBoundaryCommandCount),
                Field("MaxBoundaryCommandPayloadCount", profile.MaxBoundaryCommandPayloadCount),
                Field("MaxOwnerPlanCount", profile.MaxOwnerPlanCount),
                Field("MaxResolvedTargetCount", profile.MaxResolvedTargetCount),
                Field("MaxEffectOperationCount", profile.MaxEffectOperationCount),
                Field("MaxOwnerReservationCount", profile.MaxOwnerReservationCount),
                Field("MaxTargetReservationCount", profile.MaxTargetReservationCount),
                Field("MaxCoreFactCount", profile.MaxCoreFactCount),
                Field("MaxNextTickRouteCount", profile.MaxNextTickRouteCount),
                Field("MaxStructuralIntentCount", profile.MaxStructuralIntentCount),
                Field("MaxSessionBoundaryFactCount", profile.MaxSessionBoundaryFactCount),
                Field("MaxAscBoundaryFactCount", profile.MaxAscBoundaryFactCount),
                Field("MaxPendingAttributeInitializationCount", profile.MaxPendingAttributeInitializationCount),
                Field("MaxPendingTagInitializationCount", profile.MaxPendingTagInitializationCount),
                Field("MaxPendingGrantedAbilityInitializationCount", profile.MaxPendingGrantedAbilityInitializationCount),
                Field("MaxGrantedAbilityCount", profile.MaxGrantedAbilityCount),
                Field("MaxAbilityActivationCount", profile.MaxAbilityActivationCount),
                Field("MaxAbilityContinuationCount", profile.MaxAbilityContinuationCount),
                Field("MaxAbilitySubscriptionCount", profile.MaxAbilitySubscriptionCount),
                Field("MaxCooldownGateCount", profile.MaxCooldownGateCount),
                Field("MaxActivationOwnedContributionCount", profile.MaxActivationOwnedContributionCount),
                Field("MaxEmittedApplicationRefCount", profile.MaxEmittedApplicationRefCount),
                Field("MaxActiveEffectCount", profile.MaxActiveEffectCount),
                Field("MaxPayloadRangeRecordCount", profile.MaxPayloadRangeRecordCount),
                Field("MaxPayloadValueCount", profile.MaxPayloadValueCount),
                Field("MaxAttributeAggregatorCount", profile.MaxAttributeAggregatorCount),
                Field("MaxLiveDependencyCount", profile.MaxLiveDependencyCount),
                Field("MaxLiveDependencyRouteCount", profile.MaxLiveDependencyRouteCount),
                Field("MaxPendingCommandCount", profile.MaxPendingCommandCount),
            };
        }

        /// <summary>
        /// 创建一个 profile 字段记录以便统一非负校验。
        /// </summary>
        private static ProfileField Field(string name, int value)
        {
            return new ProfileField(name, value);
        }

        /// <summary>
        /// 在任何 Definition-sized 分配或循环前校验 count、bound entries 与保守工作集预算。
        /// </summary>
        private static bool TryValidateBuildBudget(
            GasCapacityProofBuildState state,
            out int definitionCount,
            out int boundCount)
        {
            definitionCount = 0;
            boundCount = 0;
            var rawCount = state.Source.DefinitionCount;
            if (rawCount < 0)
            {
                state.AddFailure(GasProofFailureKind.CapacityBoundInvalid,
                    GasProofRuleIds.CapacityProofMissing, "DefinitionCount", string.Empty,
                    ">=", "DefinitionCount", rawCount, "Zero", 0,
                    0, rawCount, "definitions", 0, -1, string.Empty,
                    state.Payload.ScaleProfileProvenance);
                return false;
            }
            if (rawCount > GasRuntimeV1ProofInventory.MaximumProofCollectionCount)
                return AddBuildBudgetFailure(state, "DefinitionCount", rawCount,
                    GasRuntimeV1ProofInventory.MaximumProofCollectionCount, "definitions");
            if (!GasCheckedProofMath.TryMultiply(rawCount, DimensionCount,
                    out var rawBoundCount) ||
                rawBoundCount > GasRuntimeV1ProofInventory.MaximumProofCollectionCount)
                return AddBuildBudgetFailure(state, "CapacityBoundEntryCount",
                    rawBoundCount, GasRuntimeV1ProofInventory.MaximumProofCollectionCount,
                    "bounds");
            if (!GasCheckedProofMath.TryAdd(rawCount, rawBoundCount,
                    out var totalEntryCount) ||
                totalEntryCount > GasRuntimeV1ProofInventory.MaximumProofBuildEntryCount)
                return AddBuildBudgetFailure(state, "CapacityProofBuildEntryCount",
                    totalEntryCount, GasRuntimeV1ProofInventory.MaximumProofBuildEntryCount,
                    "entries");
            if (!TryCalculateBuildBytes(rawCount, rawBoundCount, out var buildBytes) ||
                buildBytes > GasRuntimeV1ProofInventory.MaximumProofBuildBytes)
                return AddBuildBudgetFailure(state, "CapacityProofBuildBytes", buildBytes,
                    GasRuntimeV1ProofInventory.MaximumProofBuildBytes, "bytes");
            if (!GasCheckedProofMath.TryToArrayLength(rawBoundCount, out boundCount))
                return AddBuildBudgetFailure(state, "CapacityBoundEntryCount",
                    rawBoundCount, int.MaxValue, "bounds");
            if (!TryReserveCapacitySourceHeader(state))
                return AddSourceEncodingBudgetFailure(state);
            definitionCount = rawCount;
            return true;
        }

        /// <summary>
        /// 计算 Definition identity、全部 bounds 及其最坏失败证据的保守构建字节。
        /// </summary>
        private static bool TryCalculateBuildBytes(
            long definitionCount,
            long boundCount,
            out long buildBytes)
        {
            buildBytes = 0;
            if (!GasCheckedProofMath.TryMultiply(definitionCount,
                    ConservativeDefinitionIdentityBuildBytes, out var identityBytes) ||
                !GasCheckedProofMath.TryMultiply(boundCount,
                    ConservativeBoundBuildBytes, out var boundBytes))
                return false;
            return GasCheckedProofMath.TryAdd(identityBytes, boundBytes, out buildBytes);
        }

        /// <summary>
        /// 将 Capacity 顶层 identity 与两类 provenance 纳入 source 阶段真实编码预算。
        /// </summary>
        private static bool TryReserveCapacitySourceHeader(
            GasCapacityProofBuildState state)
        {
            var payload = state.Payload;
            return state.SourceEncodingBudget.TryAddString(payload.AlgorithmVersion) &&
                   state.SourceEncodingBudget.TryAddString(payload.GeneratorVersion) &&
                   state.SourceEncodingBudget.TryAddString(payload.CanonicalGraphHash) &&
                   state.SourceEncodingBudget.TryAddString(payload.ContractMatrixHash) &&
                   state.SourceEncodingBudget.TryAddString(payload.TargetScaleId) &&
                   state.SourceEncodingBudget.TryAddString(payload.LayoutHash) &&
                   state.SourceEncodingBudget.TryAddProvenance(
                       in payload.ScaleProfileProvenance) &&
                   state.SourceEncodingBudget.TryAddProvenance(
                       in payload.MemoryBudgetProvenance);
        }

        /// <summary>
        /// 将 source 实际 UTF-8 或 related-key 累计超门转换为带 actual/max 的稳定 Red。
        /// </summary>
        private static bool AddSourceEncodingBudgetFailure(
            GasCapacityProofBuildState state)
        {
            var budget = state.SourceEncodingBudget;
            var related = budget.FailedOnRelatedDefinitions;
            return AddBuildBudgetFailure(state,
                related ? "CapacityProofRelatedDefinitionCount" : "CapacityProofCanonicalBytes",
                budget.FailureActual,
                related
                    ? GasRuntimeV1ProofInventory.MaximumProofBuildEntryCount
                    : GasRuntimeV1ProofInventory.MaximumProofBuildBytes,
                related ? "related-definitions" : "bytes");
        }

        /// <summary>
        /// 添加一条带实际数量/字节与版本化硬上界的构建预算 Red。
        /// </summary>
        private static bool AddBuildBudgetFailure(
            GasCapacityProofBuildState state,
            string dimensionId,
            long actual,
            long maximum,
            string unit)
        {
            state.AddFailure(GasProofFailureKind.ProofBuildBudgetExceeded,
                GasProofRuleIds.CapacityProofMissing, dimensionId, "proof-build-budget",
                "<=", "Actual", actual, "Maximum", maximum,
                maximum, actual, unit, 0, -1, string.Empty,
                state.Payload.ScaleProfileProvenance);
            return false;
        }

        /// <summary>
        /// 读取 Definition identity 与每个必需 dimension，缺失与负值均保留 canonical 占位和 Red。
        /// </summary>
        private static bool ReadBounds(
            GasCapacityProofBuildState state,
            int count,
            int boundCount)
        {
            state.DefinitionIdentities =
                new GasCapacityDefinitionIdentityProofEntry[count];
            state.Bounds.Capacity = boundCount;
            var previousKey = string.Empty;
            for (var ordinal = 0; ordinal < count; ordinal++)
            {
                if (!ReadDefinitionIdentity(state, ordinal, ref previousKey))
                    return AbortCapacitySourceRead(state);
                for (var rawDimension = 1; rawDimension <= DimensionCount; rawDimension++)
                {
                    if (!ReadBound(state, ordinal, (GasCapacityDimension)rawDimension))
                        return AbortCapacitySourceRead(state);
                }
            }
            return true;
        }

        /// <summary>
        /// 读取一个 stable Definition identity 并验证 canonical 严格递增顺序。
        /// </summary>
        private static bool ReadDefinitionIdentity(
            GasCapacityProofBuildState state,
            int ordinal,
            ref string previousKey)
        {
            if (!state.Source.TryGetDefinitionIdentity(ordinal, out var identity,
                    out var provenance))
            {
                state.AddFailure(GasProofFailureKind.InputUnavailable,
                    GasProofRuleIds.CapacityProofMissing, "DefinitionIdentity", string.Empty,
                    string.Empty, string.Empty, 0, string.Empty, 0, 0, 0,
                    "definition", 0, ordinal, string.Empty, default);
                identity = default;
                provenance = default;
            }
            var rawDefinitionKey = identity.CanonicalKey ?? string.Empty;
            var validDefinitionKey =
                GasProofCanonicalHashWriter.IsWithinStringBudget(rawDefinitionKey);
            var definitionKey = validDefinitionKey ? rawDefinitionKey : string.Empty;
            var definitionId = identity.RuntimeDefinitionId;
            state.DefinitionIdentities[ordinal] =
                new GasCapacityDefinitionIdentityProofEntry
                {
                    DefinitionKey = definitionKey,
                    DefinitionId = definitionId,
                    DefinitionOrdinal = ordinal,
                    Provenance = provenance,
                };
            if (!identity.IsComplete || !validDefinitionKey ||
                (ordinal > 0 && string.CompareOrdinal(definitionKey, previousKey) <= 0))
                state.AddFailure(GasProofFailureKind.CanonicalOrdinalInvalid,
                    GasProofRuleIds.NonCanonicalIdentity, "DefinitionIdentity", string.Empty,
                    ">", "DefinitionKeyLength", definitionKey.Length,
                    "PreviousDefinitionKeyLength", previousKey.Length,
                    previousKey.Length, definitionKey.Length, "definition", definitionId,
                    ordinal, string.Empty, provenance, definitionKey,
                    previousKey, definitionKey);
            if (!GasProofCanonicalRules.IsCanonicalProvenance(in provenance))
                state.AddFailure(GasProofFailureKind.ProvenanceMissing,
                    GasProofRuleIds.NonCanonicalIdentity, "DefinitionIdentityProvenance",
                    string.Empty, "canonical", "ProvenanceOrdinal",
                    provenance.ProvenanceOrdinal, "RequiredMinimum", 0,
                    0, provenance.ProvenanceOrdinal, "provenance", definitionId,
                    ordinal, string.Empty, provenance, definitionKey);
            previousKey = definitionKey;
            return state.SourceEncodingBudget.TryAddString(rawDefinitionKey) &&
                   state.SourceEncodingBudget.TryAddProvenance(in provenance);
        }

        /// <summary>
        /// 读取一个显式 bound，并更新跨 Definition 最大值。
        /// </summary>
        private static bool ReadBound(
            GasCapacityProofBuildState state,
            int definitionOrdinal,
            GasCapacityDimension dimension)
        {
            var identity = state.DefinitionIdentities[definitionOrdinal];
            var definitionKey = identity.DefinitionKey;
            var definitionId = identity.DefinitionId;
            var found = state.Source.TryGetBound(definitionOrdinal, dimension,
                out var maximum, out var provenance);
            if (!found)
            {
                maximum = 0;
                state.AddFailure(GasProofFailureKind.CapacityDimensionMissing,
                    GasProofRuleIds.CapacityProofMissing, dimension.ToString(), string.Empty,
                    string.Empty, string.Empty, 0, string.Empty, 0, 0, 0,
                    "bound", definitionId, definitionOrdinal, string.Empty,
                    identity.Provenance, definitionKey);
            }
            if (maximum < 0 || !GasProofCanonicalRules.IsCanonicalProvenance(in provenance))
                state.AddFailure(maximum < 0
                        ? GasProofFailureKind.CapacityBoundInvalid
                        : GasProofFailureKind.ProvenanceMissing,
                    maximum < 0 ? GasProofRuleIds.CapacityProofMissing : GasProofRuleIds.NonCanonicalIdentity,
                    dimension.ToString(), string.Empty, ">=", "Maximum", maximum,
                    "Zero", 0, 0, maximum, "bound", definitionId, definitionOrdinal,
                    string.Empty, provenance, definitionKey);
            var entry = new GasCapacityBoundProofEntry
            {
                DefinitionKey = definitionKey,
                DefinitionId = definitionId,
                DefinitionOrdinal = definitionOrdinal,
                Dimension = dimension,
                Maximum = maximum,
                Provenance = provenance,
            };
            state.Bounds.Add(entry);
            if (definitionOrdinal == 0 || maximum > state.MaximumByDimension[(int)dimension])
            {
                state.MaximumByDimension[(int)dimension] = maximum;
                state.MaximumBoundByDimension[(int)dimension] = entry;
            }
            return state.SourceEncodingBudget.TryAddString(definitionKey) &&
                   state.SourceEncodingBudget.TryAddProvenance(in provenance);
        }

        /// <summary>
        /// 实际 source 编码超门后丢弃部分 projection，保持空 identity/bounds 形状可重验。
        /// </summary>
        private static bool AbortCapacitySourceRead(GasCapacityProofBuildState state)
        {
            state.DefinitionIdentities =
                Array.Empty<GasCapacityDefinitionIdentityProofEntry>();
            state.Bounds.Clear();
            Array.Clear(state.MaximumByDimension, 0, state.MaximumByDimension.Length);
            Array.Clear(state.MaximumBoundByDimension, 0,
                state.MaximumBoundByDimension.Length);
            AddSourceEncodingBudgetFailure(state);
            return false;
        }

        /// <summary>
        /// 对每个 Definition 执行 requirement checked-sum、隐藏编码上界与 Runtime 支持矩阵校验。
        /// </summary>
        private static void ValidateDefinitions(GasCapacityProofBuildState state)
        {
            for (var ordinal = 0; ordinal < state.DefinitionIdentities.Length; ordinal++)
            {
                ValidateRequirementSum(state, ordinal);
                ValidateEncodingLimits(state, ordinal);
                ValidateRuntimeSupport(state, ordinal);
            }
        }

        /// <summary>
        /// 用 checked add 组合四个 requirement phase 并对账声明总上界。
        /// </summary>
        private static void ValidateRequirementSum(GasCapacityProofBuildState state, int ordinal)
        {
            var identity = state.DefinitionIdentities[ordinal];
            var definitionKey = identity.DefinitionKey;
            var definitionId = identity.DefinitionId;
            var provenance = state.Bound(ordinal, GasCapacityDimension.RequirementDescriptors).Provenance;
            var application = state.Bound(ordinal,
                GasCapacityDimension.ApplicationRequirementDescriptors).Maximum;
            var ongoing = state.Bound(ordinal,
                GasCapacityDimension.OngoingRequirementDescriptors).Maximum;
            var removal = state.Bound(ordinal,
                GasCapacityDimension.RemovalRequirementDescriptors).Maximum;
            var immunity = state.Bound(ordinal,
                GasCapacityDimension.ImmunityRequirementDescriptors).Maximum;
            state.TryAdd("requirements.application+ongoing", "RequirementDescriptors",
                "Application", application, "Ongoing", ongoing, "requirements",
                provenance, definitionId, out var first, definitionKey);
            state.TryAdd("requirements.+removal", "RequirementDescriptors",
                "ApplicationAndOngoing", first, "Removal", removal, "requirements",
                provenance, definitionId, out var second, definitionKey);
            state.TryAdd("requirements.+immunity", "RequirementDescriptors",
                "PriorPhases", second, "Immunity", immunity, "requirements",
                provenance, definitionId, out var total, definitionKey);
            var declared = state.Bound(ordinal, GasCapacityDimension.RequirementDescriptors).Maximum;
            if (total > declared)
                state.AddFailure(GasProofFailureKind.CapacityBoundInvalid,
                    GasProofRuleIds.CapacityProofMissing, "RequirementDescriptors",
                    "requirements.total", "<=", "PhaseTotal", total,
                    "DeclaredMaximum", declared, declared, total, "requirements",
                    definitionId, ordinal, "GasDefinitionCatalogValidator", provenance,
                    definitionKey);
        }

        /// <summary>
        /// 验证 Direct Program、Cue carrier 与闭世界 descriptor enum 的真实编码上界。
        /// </summary>
        private static void ValidateEncodingLimits(GasCapacityProofBuildState state, int ordinal)
        {
            CheckMaximum(state, ordinal, GasCapacityDimension.ExpansionProgramNodes,
                GasRuntimeV1ProofInventory.MaximumDirectProgramNodeCount,
                GasProofFailureKind.RuntimeEncodingLimitExceeded,
                GasProofRuleIds.UnsupportedExecutionProjection, "EffectApplicationId low 16-bit node ordinal");
            CheckMaximum(state, ordinal, GasCapacityDimension.DirectProgramMaximumNodeFanOut,
                1, GasProofFailureKind.UnsupportedRuntimeCombination,
                GasProofRuleIds.UnsupportedExecutionProjection, "GasTickJobs emits one operation per node");
            CheckMaximum(state, ordinal, GasCapacityDimension.ExpansionTargets,
                1, GasProofFailureKind.UnsupportedRuntimeCombination,
                GasProofRuleIds.UnsupportedExecutionProjection, "Runtime v1 target resolver emits one target per node");
            CheckMaximum(state, ordinal, GasCapacityDimension.CueMaximumOrdinal,
                ushort.MaxValue, GasProofFailureKind.RuntimeEncodingLimitExceeded,
                GasProofRuleIds.UnsupportedExecutionProjection, "BoundaryFactBuffer.CueDefinitionOrdinal");
            CheckMaximum(state, ordinal, GasCapacityDimension.ValueViews,
                7, GasProofFailureKind.CapacityBoundInvalid,
                GasProofRuleIds.InvalidDomainOrReference, "GasAttributeValueView closed enum");
            CheckMaximum(state, ordinal, GasCapacityDimension.TargetDataFields,
                4, GasProofFailureKind.CapacityBoundInvalid,
                GasProofRuleIds.InvalidDomainOrReference, "GasTargetDataVariant closed enum");
            CheckMaximum(state, ordinal, GasCapacityDimension.EffectContextFields,
                8, GasProofFailureKind.CapacityBoundInvalid,
                GasProofRuleIds.InvalidDomainOrReference, "GasEffectContextFieldKind closed enum");
        }

        /// <summary>
        /// 对账当前 Runtime v1 明确不支持的 live、ValueView、nested dependency 与 sparse target 契约。
        /// </summary>
        private static void ValidateRuntimeSupport(GasCapacityProofBuildState state, int ordinal)
        {
            FailWhenPositive(state, ordinal, GasCapacityDimension.LiveCaptureDescriptors,
                GasProofFailureKind.ProjectionUnbounded, GasProofRuleIds.ProjectionUnbounded,
                "Source/Target projection only accepts Snapshot");
            FailWhenPositive(state, ordinal, GasCapacityDimension.UnsupportedValueViewDescriptors,
                GasProofFailureKind.UnsupportedRuntimeCombination,
                GasProofRuleIds.UnsupportedContractField,
                "Runtime v1 does not consume Bonus/Contribution/ModifierList");
            FailWhenPositive(state, ordinal, GasCapacityDimension.DirectProgramNestedDependencies,
                GasProofFailureKind.UnsupportedRuntimeCombination,
                GasProofRuleIds.UnsupportedExecutionProjection,
                "Runtime v1 does not recursively expand GE direct programs");
            FailWhenPositive(state, ordinal, GasCapacityDimension.DynamicDependencyBackEdges,
                GasProofFailureKind.DependencyOrCleanupUnbounded,
                GasProofRuleIds.DependencyOrCleanupUnbounded,
                "dynamic dependency back-edge is forbidden");
            FailWhenPositive(state, ordinal, GasCapacityDimension.TargetOverlayEntries,
                GasProofFailureKind.UnsupportedRuntimeCombination,
                GasProofRuleIds.CapacityProofMissing,
                "Runtime v1 allocates full target planes rather than sparse overlay");
        }

        /// <summary>
        /// 验证一个 bound 不超过固定 Runtime/enum 上界。
        /// </summary>
        private static void CheckMaximum(
            GasCapacityProofBuildState state,
            int ordinal,
            GasCapacityDimension dimension,
            long maximum,
            GasProofFailureKind failureKind,
            string ruleId,
            string firstConsumer)
        {
            var bound = state.Bound(ordinal, dimension);
            if (bound.Maximum <= maximum)
                return;
            state.AddFailure(failureKind, ruleId, dimension.ToString(), string.Empty,
                "<=", "Maximum", bound.Maximum, "RuntimeMaximum", maximum,
                maximum, bound.Maximum, "elements", bound.DefinitionId,
                ordinal, firstConsumer, bound.Provenance, bound.DefinitionKey);
        }

        /// <summary>
        /// 对当前 Runtime v1 明确不支持的非零 semantic dimension 生成带 provenance 的 Red。
        /// </summary>
        private static void FailWhenPositive(
            GasCapacityProofBuildState state,
            int ordinal,
            GasCapacityDimension dimension,
            GasProofFailureKind failureKind,
            string ruleId,
            string firstConsumer)
        {
            var bound = state.Bound(ordinal, dimension);
            if (bound.Maximum <= 0)
                return;
            state.AddFailure(failureKind, ruleId, dimension.ToString(), string.Empty,
                "==", "Maximum", bound.Maximum, "SupportedMaximum", 0,
                0, bound.Maximum, "elements", bound.DefinitionId, ordinal,
                firstConsumer, bound.Provenance, bound.DefinitionKey);
        }

        /// <summary>
        /// 推导 Plan/Expand 与 payload 的实际需求，并保留 Route/Facts 的非完整下界证据。
        /// </summary>
        private static void BuildRuntimeLimits(GasCapacityProofBuildState state)
        {
            var profile = state.Payload.Profile;
            var scaleProvenance = state.Payload.ScaleProfileProvenance;
            var targetBound = state.MaximumBound(GasCapacityDimension.ExpansionTargets);
            var outputBound = SelectGreaterBound(
                state.MaximumBound(GasCapacityDimension.ExpansionApplications),
                state.MaximumBound(GasCapacityDimension.DirectProgramOutputs));
            var targetMultiplier = Math.Max(1L, targetBound.Maximum);
            var outputMultiplier = Math.Max(1L, outputBound.Maximum);
            state.TryMultiply("plan.resolved-targets", "ResolvedTargets",
                "MaxBoundaryCommandCount", profile.MaxBoundaryCommandCount,
                "MaxTargetCount", targetMultiplier, "records",
                targetBound.Provenance, targetBound.DefinitionId,
                out var resolvedTargets, targetBound.DefinitionKey);
            state.TryMultiply("plan.effect-operations", "EffectOperations",
                "MaxBoundaryCommandCount", profile.MaxBoundaryCommandCount,
                "MaxPlannedOrDirectOutputCount", outputMultiplier, "records",
                outputBound.Provenance, outputBound.DefinitionId,
                out var effectOperations, outputBound.DefinitionKey);
            state.AddLimit("OwnerPlans", "MaxOwnerPlanCount", "commandCount",
                profile.MaxBoundaryCommandCount, profile.MaxOwnerPlanCount, "records",
                GasProofConsumerStatus.RuntimeEquivalentCheck, "PlanExpandJob", scaleProvenance);
            state.AddLimit("ResolvedTargets", "MaxResolvedTargetCount",
                "commandCount * max(1, MaximumTargetCount)", resolvedTargets,
                profile.MaxResolvedTargetCount, "records", GasProofConsumerStatus.RuntimeEquivalentCheck,
                "PlanExpandJob/TargetResolve", targetBound.Provenance,
                targetBound.DefinitionId, targetBound.DefinitionKey);
            state.AddLimit("EffectOperations", "MaxEffectOperationCount",
                "commandCount * max(1, plannedApplications, directProgramOutputs)",
                effectOperations,
                profile.MaxEffectOperationCount, "records", GasProofConsumerStatus.RuntimeEquivalentCheck,
                "PlanExpandJob", outputBound.Provenance,
                outputBound.DefinitionId, outputBound.DefinitionKey);
            AddDerivedRuntimeLimits(state, effectOperations);
        }

        /// <summary>
        /// 推导 dynamic route/fact 的非完整下界与长期 payload 的交叉乘积上界。
        /// </summary>
        private static void AddDerivedRuntimeLimits(
            GasCapacityProofBuildState state,
            long effectOperations)
        {
            var profile = state.Payload.Profile;
            var routeBound = state.MaximumBound(
                GasCapacityDimension.ExpansionDynamicNextTickWork);
            state.TryMultiply("routes.required", "DynamicNextTickWork",
                "EffectOperations", effectOperations, "MaxDynamicWorkPerApplication",
                routeBound.Maximum, "routes", routeBound.Provenance,
                routeBound.DefinitionId, out var routes, routeBound.DefinitionKey);
            state.AddLimit("NextTickRoutesLowerBound", "MaxNextTickRouteCount",
                "incomplete lower-bound: effectOperations * dynamicWorkPerApplication", routes,
                -1, "routes", GasProofConsumerStatus.EvidenceOnly,
                string.Empty, routeBound.Provenance,
                routeBound.DefinitionId, routeBound.DefinitionKey);
            var factBound = state.MaximumBound(GasCapacityDimension.ObservationFacts);
            state.TryMultiply("facts.required", "ObservationFacts",
                "EffectOperations", effectOperations, "MaxFactsPerApplication",
                factBound.Maximum, "facts", factBound.Provenance,
                factBound.DefinitionId, out var facts, factBound.DefinitionKey);
            state.AddLimit("CoreFactsLowerBound", "MaxCoreFactCount",
                "incomplete lower-bound: effectOperations * factsPerApplication", facts, -1,
                "facts", GasProofConsumerStatus.EvidenceOnly,
                string.Empty, factBound.Provenance,
                factBound.DefinitionId, factBound.DefinitionKey);
            var captureBound = state.MaximumBound(GasCapacityDimension.CaptureDescriptors);
            state.TryMultiply("payload.values", "PayloadValueCapacity",
                "MaxPayloadRangeRecordCount", profile.MaxPayloadRangeRecordCount,
                "MaxCaptureDescriptors", captureBound.Maximum,
                "values", captureBound.Provenance, captureBound.DefinitionId,
                out var payloadValues, captureBound.DefinitionKey);
            state.AddLimit("PayloadValues", "MaxPayloadValueCount",
                "rangeRecords * maxLongLivedCaptureCount", payloadValues,
                profile.MaxPayloadValueCount, "values", GasProofConsumerStatus.RuntimeEquivalentCheck,
                "GasPayloadRangeAllocator", captureBound.Provenance,
                captureBound.DefinitionId, captureBound.DefinitionKey);
            AddStabilizationWorkEvidence(state, effectOperations);
        }

        /// <summary>
        /// 只计算 stabilization 分量，不冒充 Runtime 尚缺的完整 TargetWorkUnits 上界。
        /// </summary>
        private static void AddStabilizationWorkEvidence(
            GasCapacityProofBuildState state,
            long effectOperations)
        {
            var workBound = state.MaximumBound(GasCapacityDimension.StabilizationWorkUnits);
            state.TryMultiply("target.stabilization-work", "StabilizationWorkUnitsRequired",
                "EffectOperations", effectOperations, "MaxStabilizationWorkPerApplication",
                workBound.Maximum, "work-units", workBound.Provenance,
                workBound.DefinitionId, out var work, workBound.DefinitionKey);
            state.AddLimit("StabilizationWorkUnits", string.Empty,
                "effectOperations * stabilizationWorkUnits", work, -1,
                "work-units", GasProofConsumerStatus.Missing, string.Empty,
                workBound.Provenance, workBound.DefinitionId, workBound.DefinitionKey);
        }

        /// <summary>
        /// 以稳定左优先规则选择两个 Runtime output maxima 的 argmax witness。
        /// </summary>
        private static GasCapacityBoundProofEntry SelectGreaterBound(
            in GasCapacityBoundProofEntry left,
            in GasCapacityBoundProofEntry right)
        {
            return right.Maximum > left.Maximum ? right : left;
        }

        /// <summary>
        /// 将 adapter、provenance 关联、模型/公式与 authority 消费缺口固化为稳定 Red。
        /// </summary>
        private static void BuildCoverageEvidence(GasCapacityProofBuildState state)
        {
            var gaps = state.Payload.CoverageGaps;
            if (gaps == GasCapacityProofCoverageGap.None)
                return;
            state.AddFailure(GasProofFailureKind.ProofCoverageIncomplete,
                GasProofRuleIds.CapacityProofMissing, "CapacityModelCoverage",
                "capacity-model-coverage", "==", "CoverageGaps",
                (long)(uint)gaps, "None", 0, 0, (long)(uint)gaps,
                "flags", 0, -1, "Z/WholeTick authority admission",
                state.Payload.ScaleProfileProvenance);
        }

        /// <summary>
        /// 固定 Runtime v1 consumer map，并把 missing/declared/preallocated 项转成 Capacity Red。
        /// </summary>
        private static void BuildConsumerEvidence(GasCapacityProofBuildState state)
        {
            var consumerMap = GasRuntimeV1ProofInventory.CreateConsumerMap();
            state.Consumers.AddRange(consumerMap);
            foreach (var consumer in consumerMap)
            {
                if (consumer.Status == GasProofConsumerStatus.ConsumedBeforeAuthorityWrite)
                    continue;
                var ruleId = ConsumerRuleId(consumer.DimensionId);
                state.AddFailure(GasProofFailureKind.RuntimeConsumerMissing,
                    ruleId, consumer.DimensionId, string.Empty, "consume",
                    "ConsumerStatus", (long)consumer.Status, "ConsumedBeforeAuthorityWrite",
                    (long)GasProofConsumerStatus.ConsumedBeforeAuthorityWrite,
                    (long)GasProofConsumerStatus.ConsumedBeforeAuthorityWrite, (long)consumer.Status,
                    "consumer", 0, -1, consumer.FirstConsumer,
                    state.Payload.ScaleProfileProvenance);
            }
        }

        /// <summary>
        /// 按 consumer dimension 选择 Spec 已冻结的粗粒度 RuleId。
        /// </summary>
        private static string ConsumerRuleId(string dimensionId)
        {
            if (dimensionId.IndexOf("Projection", StringComparison.Ordinal) >= 0)
                return GasProofRuleIds.ProjectionUnbounded;
            if (dimensionId.IndexOf("Cleanup", StringComparison.Ordinal) >= 0 ||
                dimensionId.IndexOf("Depend", StringComparison.Ordinal) >= 0)
                return GasProofRuleIds.DependencyOrCleanupUnbounded;
            return GasProofRuleIds.CapacityProofMissing;
        }

        /// <summary>
        /// 判断 CapacityProof canonical 重编码所需的所有数组是否存在。
        /// </summary>
        private static bool HasCompletePayloadArrays(GasRuntimeV1CapacityProofPayload payload)
        {
            if (payload == null ||
                payload.DefinitionIdentities == null ||
                payload.Bounds == null ||
                payload.Derivations == null ||
                payload.RuntimeLimits == null ||
                payload.ConsumerMap == null ||
                payload.MemoryEvidence == null ||
                payload.Failures == null)
                return false;
            if (!GasCheckedProofMath.TryMultiply(payload.DefinitionIdentities.Length,
                    DimensionCount, out var expectedBoundCount) ||
                expectedBoundCount != payload.Bounds.Length)
                return false;
            return IsPayloadEncodingBudgetSafe(payload);
        }

        /// <summary>
        /// 在进入 MemoryStream hasher 前限制全部顶层数组的 count 与保守编码字节。
        /// </summary>
        private static bool IsPayloadEncodingBudgetSafe(
            GasRuntimeV1CapacityProofPayload payload)
        {
            return TryMeasurePayloadEncoding(payload, out _);
        }

        /// <summary>
        /// 冻结数组后执行真实 payload 预算；失败时追加结构化 Red 并阻止任何 hasher 分配。
        /// </summary>
        internal static bool TryFinalizePayloadEncodingBudget(
            GasCapacityProofBuildState state)
        {
            if (TryMeasurePayloadEncoding(state.Payload, out var budget))
                return true;
            var related = budget.FailedOnRelatedDefinitions;
            var actual = budget.FailureActual > 0
                ? budget.FailureActual
                : (related
                    ? GasRuntimeV1ProofInventory.MaximumProofBuildEntryCount + 1
                    : GasRuntimeV1ProofInventory.MaximumProofBuildBytes + 1);
            AddBuildBudgetFailure(state,
                related ? "CapacityProofRelatedDefinitionCount" : "CapacityProofCanonicalBytes",
                actual,
                related
                    ? GasRuntimeV1ProofInventory.MaximumProofBuildEntryCount
                    : GasRuntimeV1ProofInventory.MaximumProofBuildBytes,
                related ? "related-definitions" : "bytes");
            state.Failures.Sort(GasProofCanonicalRules.CompareFailures);
            state.Payload.Failures = state.Failures.ToArray();
            return false;
        }

        /// <summary>
        /// 先校验 Capacity collections，再累计全部真实 UTF-8 与 related-key 编码。
        /// </summary>
        private static bool TryMeasurePayloadEncoding(
            GasRuntimeV1CapacityProofPayload payload,
            out GasProofEncodingBudget budget)
        {
            budget = new GasProofEncodingBudget();
            if (!ArePayloadCollectionsSafe(payload) ||
                !TryAddCapacityHeaderStrings(payload, ref budget))
                return false;
            if (!TryAddDefinitionStrings(payload, ref budget) ||
                !TryAddDerivationStrings(payload, ref budget) ||
                !TryAddRuntimeEvidenceStrings(payload, ref budget))
                return false;
            foreach (var failure in payload.Failures)
                if (!budget.TryAddFailure(in failure))
                    return false;
            return true;
        }

        /// <summary>
        /// 以短路顺序校验全部顶层数组的 count 与保守 allocation 工作集。
        /// </summary>
        private static bool ArePayloadCollectionsSafe(
            GasRuntimeV1CapacityProofPayload payload)
        {
            var entryCount = 0L;
            var byteCount = 0L;
            return TryAccumulatePayloadEntries(payload.DefinitionIdentities.Length,
                       ConservativeDefinitionIdentityBuildBytes, ref entryCount, ref byteCount) &&
                   TryAccumulatePayloadEntries(payload.Bounds.Length,
                       ConservativeBoundBuildBytes, ref entryCount, ref byteCount) &&
                   TryAccumulatePayloadEntries(payload.Derivations.Length,
                       ConservativePayloadEntryEncodingBytes, ref entryCount, ref byteCount) &&
                   TryAccumulatePayloadEntries(payload.RuntimeLimits.Length,
                       ConservativePayloadEntryEncodingBytes, ref entryCount, ref byteCount) &&
                   TryAccumulatePayloadEntries(payload.ConsumerMap.Length,
                       ConservativePayloadEntryEncodingBytes, ref entryCount, ref byteCount) &&
                   TryAccumulatePayloadEntries(payload.MemoryEvidence.Length,
                       ConservativePayloadEntryEncodingBytes, ref entryCount, ref byteCount) &&
                   TryAccumulatePayloadEntries(payload.Failures.Length,
                       ConservativePayloadEntryEncodingBytes, ref entryCount, ref byteCount);
        }

        /// <summary>
        /// 累加 Capacity 顶层 identity、两类 provenance 与 ABI summary 文本。
        /// </summary>
        private static bool TryAddCapacityHeaderStrings(
            GasRuntimeV1CapacityProofPayload payload,
            ref GasProofEncodingBudget budget)
        {
            return budget.TryAddString(payload.AlgorithmVersion) &&
                   budget.TryAddString(payload.GeneratorVersion) &&
                   budget.TryAddString(payload.CanonicalGraphHash) &&
                   budget.TryAddString(payload.ContractMatrixHash) &&
                   budget.TryAddString(payload.TargetScaleId) &&
                   budget.TryAddString(payload.LayoutHash) &&
                   budget.TryAddString(payload.LayoutAbiHash) &&
                   budget.TryAddString(payload.ProofHash) &&
                   budget.TryAddString(payload.MemorySummary.LayoutAbiHash) &&
                   budget.TryAddProvenance(in payload.ScaleProfileProvenance) &&
                   budget.TryAddProvenance(in payload.MemoryBudgetProvenance);
        }

        /// <summary>
        /// 累加 Definition identity/bound 的 key 与 provenance，失败时立即停止。
        /// </summary>
        private static bool TryAddDefinitionStrings(
            GasRuntimeV1CapacityProofPayload payload,
            ref GasProofEncodingBudget budget)
        {
            foreach (var entry in payload.DefinitionIdentities)
                if (!budget.TryAddString(entry.DefinitionKey) ||
                    !budget.TryAddProvenance(in entry.Provenance))
                    return false;
            foreach (var entry in payload.Bounds)
                if (!budget.TryAddString(entry.DefinitionKey) ||
                    !budget.TryAddProvenance(in entry.Provenance))
                    return false;
            return true;
        }

        /// <summary>
        /// 累加 checked derivation 的表达式、identity 与 provenance。
        /// </summary>
        private static bool TryAddDerivationStrings(
            GasRuntimeV1CapacityProofPayload payload,
            ref GasProofEncodingBudget budget)
        {
            foreach (var entry in payload.Derivations)
            {
                if (!budget.TryAddString(entry.DerivationId) ||
                    !budget.TryAddString(entry.DimensionId) ||
                    !budget.TryAddString(entry.Expression) ||
                    !budget.TryAddString(entry.Operator) ||
                    !budget.TryAddString(entry.LeftOperandName) ||
                    !budget.TryAddString(entry.RightOperandName) ||
                    !budget.TryAddString(entry.Unit) ||
                    !budget.TryAddString(entry.DefinitionKey) ||
                    !budget.TryAddProvenance(in entry.Provenance))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 累加 limit、consumer 与 memory evidence 文本，覆盖 ABI writer 的全部字符串。
        /// </summary>
        private static bool TryAddRuntimeEvidenceStrings(
            GasRuntimeV1CapacityProofPayload payload,
            ref GasProofEncodingBudget budget)
        {
            foreach (var entry in payload.RuntimeLimits)
                if (!budget.TryAddString(entry.LimitId) ||
                    !budget.TryAddString(entry.ProfileField) ||
                    !budget.TryAddString(entry.Formula) ||
                    !budget.TryAddString(entry.Unit) ||
                    !budget.TryAddString(entry.FirstConsumer))
                    return false;
            foreach (var entry in payload.ConsumerMap)
                if (!budget.TryAddString(entry.DimensionId) ||
                    !budget.TryAddString(entry.ProfileField) ||
                    !budget.TryAddString(entry.FirstConsumer) ||
                    !budget.TryAddString(entry.ConsumptionMoment))
                    return false;
            foreach (var entry in payload.MemoryEvidence)
                if (!budget.TryAddString(entry.EvidenceId) ||
                    !budget.TryAddString(entry.Owner) ||
                    !budget.TryAddString(entry.ElementTypeName) ||
                    !budget.TryAddString(entry.Formula) ||
                    !budget.TryAddString(entry.AbiFingerprint))
                    return false;
            return true;
        }

        /// <summary>
        /// 累加一个 Capacity payload 数组的 count 与编码预算，失败时不进入 hash 分配。
        /// </summary>
        private static bool TryAccumulatePayloadEntries(
            int count,
            int bytesPerEntry,
            ref long entryCount,
            ref long byteCount)
        {
            if (count > GasRuntimeV1ProofInventory.MaximumProofCollectionCount ||
                !GasCheckedProofMath.TryAdd(entryCount, count, out var nextEntryCount) ||
                nextEntryCount > GasRuntimeV1ProofInventory.MaximumProofBuildEntryCount ||
                !GasCheckedProofMath.TryMultiply(count,
                    bytesPerEntry, out var collectionBytes) ||
                !GasCheckedProofMath.TryAdd(byteCount, collectionBytes, out var nextByteCount) ||
                nextByteCount > GasRuntimeV1ProofInventory.MaximumProofBuildBytes)
                return false;
            entryCount = nextEntryCount;
            byteCount = nextByteCount;
            return true;
        }

        /// <summary>
        /// 比较一项 canonical hash，并在失配时返回两侧完整文本。
        /// </summary>
        private static bool VerifyHash(
            string actual,
            string expected,
            string dimensionId,
            out GasProofFailure failure)
        {
            if (GasProofCanonicalHashWriter.IsSha256Hex(actual) &&
                GasProofCanonicalHashWriter.IsSha256Hex(expected) &&
                string.Equals(actual, expected, StringComparison.Ordinal))
            {
                failure = default;
                return true;
            }
            failure = IntegrityFailure(dimensionId, expected, actual);
            return false;
        }

        /// <summary>
        /// 构造带 expected/actual hash 的 Capacity 完整性 Red。
        /// </summary>
        private static GasProofFailure IntegrityFailure(
            string dimensionId,
            string expected,
            string actual)
        {
            return new GasProofFailure
            {
                ProofKind = GasProofKind.Capacity,
                FailureKind = GasProofFailureKind.ProofHashMismatch,
                RuleId = GasProofRuleIds.NonCanonicalIdentity,
                DimensionId = dimensionId,
                Operator = "==",
                LeftOperandName = "ExpectedHash",
                RightOperandName = "ActualHash",
                ExpectedText = expected ?? string.Empty,
                ActualText = actual ?? string.Empty,
                Unit = "sha256",
                CanonicalOrdinal = -1,
            };
        }

        /// <summary>
        /// 保存一个 ScaleProfile 字段名和值，避免使用反射建立不稳定字段顺序。
        /// </summary>
        private readonly struct ProfileField
        {
            public readonly string Name;
            public readonly int Value;

            /// <summary>
            /// 构造一个显式顺序的 profile 字段记录。
            /// </summary>
            public ProfileField(string name, int value)
            {
                Name = name;
                Value = value;
            }
        }
    }
}
