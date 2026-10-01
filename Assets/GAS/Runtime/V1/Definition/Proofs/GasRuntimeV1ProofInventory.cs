using System;
using System.Collections.Generic;

namespace GAS.Runtime
{
    /// <summary>
    /// 固定 N2-G0-B 的 proof 版本、Runtime v1 consumer 盘点与可执行 Red 契约。
    /// </summary>
    public static class GasRuntimeV1ProofInventory
    {
        public const int LayoutProofSchemaVersion = 2;
        public const int CapacityProofSchemaVersion = 2;
        public const string LayoutAlgorithmVersion = "runtime-v1-layout-proof/2";
        public const string CapacityAlgorithmVersion = "runtime-v1-capacity-proof/2";
        public const int MaximumDirectProgramNodeCount = ushort.MaxValue;
        public const int MaximumProofCollectionCount = 1_000_000;
        public const long MaximumProofBuildEntryCount = 1_000_000L;
        public const long MaximumProofBuildBytes = 256L * 1024L * 1024L;
        public const int MaximumProofCanonicalStringBytes = 1024 * 1024;
        public const int MaximumProofRelatedDefinitionCount = 64;

        /// <summary>
        /// 返回当前 Runtime v1 的第一消费者盘点；非权威写前真消费项必须阻止 CapacityProof Green。
        /// </summary>
        public static GasProofConsumerEntry[] CreateConsumerMap()
        {
            var entries = new List<GasProofConsumerEntry>();
            entries.AddRange(CreateExpansionAndTouchedConsumers());
            entries.AddRange(CreateTargetAndStabilizationConsumers());
            entries.AddRange(CreateProjectionAndCleanupConsumers());
            entries.AddRange(CreateObservationAndDescriptorConsumers());
            entries.AddRange(CreatePhysicalCapacityConsumers());
            return entries.ToArray();
        }

        /// <summary>
        /// 返回 expansion 与 touched-authority proof 字段的第一消费者盘点。
        /// </summary>
        private static GasProofConsumerEntry[] CreateExpansionAndTouchedConsumers()
        {
            return new[]
            {
                Consumer(GasCapacityDimension.ExpansionTargets, "MaxResolvedTargetCount", "PlanExpandJob/TargetResolve", "等价检查；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer(GasCapacityDimension.ExpansionProgramNodes, "GasDefinitionMaxima.MaximumDirectProgramNodeCount", "GasDefinitionCatalogValidator/PlanExpandJob", "等价检查；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer(GasCapacityDimension.ExpansionProgramEdges, string.Empty, string.Empty, "展开前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.ExpansionApplications, "MaxEffectOperationCount", "PlanExpandJob", "等价检查；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer(GasCapacityDimension.ExpansionDynamicNextTickWork, "MaxNextTickRouteCount", "GasAbilityRouteRecord", "权威写之前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.TouchedAttributes, string.Empty, string.Empty, "WholeTick admission 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.TouchedTags, string.Empty, string.Empty, "WholeTick admission 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.TouchedActiveEffects, "MaxActiveEffectCount", "target slab owner", "TargetPrepare 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.TouchedGrants, "MaxGrantedAbilityCount", "owner slab owner", "OwnerWave 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.TouchedActivations, "MaxAbilityActivationCount", "owner slab owner", "OwnerWave 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.TouchedContinuations, "MaxAbilityContinuationCount", "owner slab owner", "OwnerWave 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.TouchedSubscriptions, "MaxAbilitySubscriptionCount", "owner slab owner", "OwnerWave 前", GasProofConsumerStatus.Missing),
            };
        }

        /// <summary>
        /// 返回 target transaction 与 stabilization proof 字段的第一消费者盘点。
        /// </summary>
        private static GasProofConsumerEntry[] CreateTargetAndStabilizationConsumers()
        {
            return new[]
            {
                Consumer(GasCapacityDimension.TargetOverlayEntries, string.Empty, string.Empty, "TargetPrepare 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.TargetOverlayBytes, string.Empty, string.Empty, "TargetPrepare 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.TargetReadYourWritesIndexBytes, string.Empty, string.Empty, "TargetPrepare 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.TargetPublishDeltaEntries, string.Empty, string.Empty, "TargetPublish 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.TargetPublishDeltaBytes, string.Empty, string.Empty, "TargetPublish 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.StabilizationTransitions, string.Empty, string.Empty, "TargetStabilization 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.StabilizationRounds, string.Empty, string.Empty, "TargetStabilization 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.StabilizationWorkUnits, string.Empty, string.Empty, "WholeTick admission 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.StabilizationSignedDependencyEdges, string.Empty, string.Empty, "TargetStabilization 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.StabilizationStateHashes, string.Empty, string.Empty, "SessionFaultReduce 前", GasProofConsumerStatus.Missing),
            };
        }

        /// <summary>
        /// 返回 projection、capture、cleanup 与 audit proof 字段的第一消费者盘点。
        /// </summary>
        private static GasProofConsumerEntry[] CreateProjectionAndCleanupConsumers()
        {
            return new[]
            {
                Consumer(GasCapacityDimension.ProjectionPayloadBytes, string.Empty, string.Empty, "SourceSpecProjection 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.ProjectionSnapshotContributors, string.Empty, string.Empty, "SourceSpecProjection 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.ProjectionLiveDirtyFanOut, "MaxLiveDependencyRouteCount", string.Empty, "live propagation 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.ProjectionCoalescedUpdates, string.Empty, string.Empty, "live propagation 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.CleanupGrantChildren, string.Empty, string.Empty, "WholeTick admission 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.CleanupRightRemovals, string.Empty, string.Empty, "WholeTick admission 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.CleanupEmittedRefRetention, "MaxEmittedApplicationRefCount", "ASC buffer EnsureCapacity", "bootstrap", GasProofConsumerStatus.PreallocatedOnly),
                Consumer(GasCapacityDimension.CleanupEmittedRefHighWater, "MaxEmittedApplicationRefCount", "ASC buffer EnsureCapacity", "bootstrap", GasProofConsumerStatus.PreallocatedOnly),
                Consumer(GasCapacityDimension.CleanupContinuations, "MaxAbilityContinuationCount", "continuation slab owner", "cleanup 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.CleanupSubscriptions, "MaxAbilitySubscriptionCount", "subscription slab owner", "cleanup 前", GasProofConsumerStatus.Missing),
            };
        }

        /// <summary>
        /// 返回 observation、Catalog descriptor 与当前 unsupported 统计字段的消费者盘点。
        /// </summary>
        private static GasProofConsumerEntry[] CreateObservationAndDescriptorConsumers()
        {
            return new[]
            {
                Consumer(GasCapacityDimension.ObservationFacts, "MaxCoreFactCount", "GasCoreFactRecord/BoundaryFactPublishIntent", "仅下界证据；完整 Runtime fact 公式缺失", GasProofConsumerStatus.EvidenceOnly),
                Consumer(GasCapacityDimension.ObservationCueIntents, string.Empty, string.Empty, "fact publish 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.ObservationBoundaryRecords, "MaxSessionBoundaryFactCount/MaxAscBoundaryFactCount", "Boundary outbox", "等价检查；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer(GasCapacityDimension.ObservationEcbIntents, "MaxStructuralIntentCount", string.Empty, "authority mutation 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.RequirementDescriptors, "GasDefinitionMaxima.MaximumRequirementCount", "GasDefinitionCatalogValidator", "等价检查；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer(GasCapacityDimension.CaptureDescriptors, "GasDefinitionMaxima.MaximumCaptureDescriptorCount", "GasTickScratch.Create", "等价分配公式；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer(GasCapacityDimension.Modifiers, "GasDefinitionMaxima.MaximumModifierCount", "GasDefinitionCatalogValidator", "等价检查；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer(GasCapacityDimension.DirectProgramOutputs, "GasDefinitionMaxima.MaximumDirectProgramOutputCount", "GasDefinitionCatalogValidator", "等价检查；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer(GasCapacityDimension.DirectProgramMaximumNodeFanOut, string.Empty, string.Empty, "展开前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.ValueViews, "GasDefinitionMaxima.MaximumValueViewCount", "GasTickScratch.Create", "等价分配公式；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer(GasCapacityDimension.EvaluatorInstructions, "GasDefinitionMaxima.MaximumEvaluatorInstructionCount", "GasTickScratch.Create", "等价分配公式；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer(GasCapacityDimension.SetByCallerFields, "GasDefinitionMaxima.MaximumSetByCallerCount", "GasDefinitionCatalogValidator", "等价检查；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer(GasCapacityDimension.TargetDataFields, "GasDefinitionMaxima.MaximumTargetDataCount", "GasDefinitionCatalogValidator", "等价检查；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer(GasCapacityDimension.EffectContextFields, "GasDefinitionMaxima.MaximumEffectContextFieldCount", "GasDefinitionCatalogValidator", "等价检查；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer(GasCapacityDimension.ApplicationRequirementDescriptors, "GasDefinitionMaxima.MaximumRequirementCount", "CapacityProof checked sum", "Catalog 安装", GasProofConsumerStatus.EvidenceOnly),
                Consumer(GasCapacityDimension.OngoingRequirementDescriptors, "GasDefinitionMaxima.MaximumRequirementCount", "CapacityProof checked sum", "Catalog 安装", GasProofConsumerStatus.EvidenceOnly),
                Consumer(GasCapacityDimension.RemovalRequirementDescriptors, "GasDefinitionMaxima.MaximumRequirementCount", "CapacityProof checked sum", "Catalog 安装", GasProofConsumerStatus.EvidenceOnly),
                Consumer(GasCapacityDimension.ImmunityRequirementDescriptors, "GasDefinitionMaxima.MaximumRequirementCount", "CapacityProof checked sum", "Catalog 安装", GasProofConsumerStatus.EvidenceOnly),
                Consumer(GasCapacityDimension.LiveCaptureDescriptors, "MaxLiveDependencyCount", string.Empty, "projection 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.UnsupportedValueViewDescriptors, string.Empty, string.Empty, "projection 前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.CueMaximumOrdinal, string.Empty, "BoundaryFactBuffer.CueDefinitionOrdinal", "fact publish", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.DirectProgramNestedDependencies, string.Empty, string.Empty, "展开前", GasProofConsumerStatus.Missing),
                Consumer(GasCapacityDimension.DynamicDependencyBackEdges, string.Empty, string.Empty, "dependency install 前", GasProofConsumerStatus.Missing),
            };
        }

        /// <summary>
        /// 返回不直接对应 semantic enum 的 Runtime v1 物理 profile 容量盘点。
        /// </summary>
        private static GasProofConsumerEntry[] CreatePhysicalCapacityConsumers()
        {
            return new[]
            {
                Consumer("ProjectionCaptureRows", "MaxEffectOperationCount × CaptureStride", "GasTickScratch.Create", "等价分配公式；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer("ProjectionValueViewRows", "MaxEffectOperationCount × ValueViewStride", "GasTickScratch.Create", "等价分配公式；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer("TargetFullPlane", "MaxAscRegistryCount × target stride", "GasTickScratch.Create", "等价分配公式；未读取 proof", GasProofConsumerStatus.RuntimeEquivalentCheck),
                Consumer("TargetWorkUnits", string.Empty, string.Empty, "WholeTick admission 前", GasProofConsumerStatus.Missing),
                Consumer("OwnerReservations", "MaxOwnerReservationCount", "GasTickScratch.Create", "仅分配 OwnerDemands", GasProofConsumerStatus.DeclaredOnly),
                Consumer("TargetReservations", "MaxTargetReservationCount", "GasTickScratch.Create", "仅分配 TargetDemands", GasProofConsumerStatus.DeclaredOnly),
                Consumer("StructuralIntents", "MaxStructuralIntentCount", "GasScaleProfile hash/validation", "未分配且未消费", GasProofConsumerStatus.DeclaredOnly),
                Consumer("ActivationOwnedContributions", "MaxActivationOwnedContributionCount", "ASC buffer EnsureCapacity", "bootstrap", GasProofConsumerStatus.PreallocatedOnly),
                Consumer("AttributeAggregators", "MaxAttributeAggregatorCount", "ASC buffer EnsureCapacity", "bootstrap", GasProofConsumerStatus.PreallocatedOnly),
                Consumer("LiveDependencies", "MaxLiveDependencyCount", "ASC buffer EnsureCapacity", "bootstrap", GasProofConsumerStatus.PreallocatedOnly),
                Consumer("LiveDependencyRoutes", "MaxLiveDependencyRouteCount", "ASC buffer EnsureCapacity", "bootstrap", GasProofConsumerStatus.PreallocatedOnly),
            };
        }

        /// <summary>
        /// 返回 proof 测试必须覆盖的稳定 Red 矩阵，CaseId 是报告与参数化测试的对账键。
        /// </summary>
        public static GasProofRedMatrixEntry[] CreateRedMatrix()
        {
            return new[]
            {
                Red("LAY-001", GasProofKind.Layout, GasProofFailureKind.IdentityMissing, GasProofRuleIds.NonCanonicalIdentity, "graph hash/version 缺失", "graph identity 与 provenance"),
                Red("LAY-002", GasProofKind.Layout, GasProofFailureKind.StableIdInvalid, GasProofRuleIds.InvalidDomainOrReference, "stable id 非正、重复或未严格排序", "stable id、ordinal 与字段 provenance"),
                Red("LAY-003", GasProofKind.Layout, GasProofFailureKind.DenseIndexInvalid, GasProofRuleIds.InvalidDomainOrReference, "dense index/ordinal 存在缺口", "expected/actual index 与 provenance"),
                Red("LAY-004", GasProofKind.Layout, GasProofFailureKind.AncestorProgramInvalid, GasProofRuleIds.InvalidDomainOrReference, "ancestor 非 canonical、自身缺失、反环或闭包缺失", "tag、range、ancestor ordinal 与 provenance"),
                Red("LAY-005", GasProofKind.Layout, GasProofFailureKind.RangeArithmeticOverflow, GasProofRuleIds.InvalidDomainOrReference, "Blob range checked add 溢出 int 编码域", "Start、Count 与 provenance"),
                Red("LAY-006", GasProofKind.Layout, GasProofFailureKind.RangeInvalid, GasProofRuleIds.InvalidDomainOrReference, "Blob range 越过真实 backing length", "Start、Count、BackingLength 与 provenance"),
                Red("LAY-007", GasProofKind.Layout, GasProofFailureKind.ProofBuildBudgetExceeded, GasProofRuleIds.CapacityProofMissing, "任一 source count 或累计编码预算越过 proof 构建硬门", "count、checked bytes 与构建预算"),
                Red("CAP-001", GasProofKind.Capacity, GasProofFailureKind.CapacityDimensionMissing, GasProofRuleIds.CapacityProofMissing, "任一必需 semantic dimension 缺失", "Definition/Profile/Dimension 与 provenance"),
                Red("CAP-002", GasProofKind.Capacity, GasProofFailureKind.ArithmeticOverflow, GasProofRuleIds.CapacityProofMissing, "任一 proof add/multiply 溢出", "运算符、左右操作数、单位与 derivation id"),
                Red("CAP-003", GasProofKind.Capacity, GasProofFailureKind.RuntimeEncodingLimitExceeded, GasProofRuleIds.UnsupportedExecutionProjection, "DirectProgram node 超过 ushort ordinal 上界", "node maximum 与 definition provenance"),
                Red("CAP-003B", GasProofKind.Capacity, GasProofFailureKind.UnsupportedRuntimeCombination, GasProofRuleIds.UnsupportedExecutionProjection, "DirectProgram 单节点 fan-out 大于一", "fan-out maximum 与 definition provenance"),
                Red("CAP-004", GasProofKind.Capacity, GasProofFailureKind.ProjectionUnbounded, GasProofRuleIds.ProjectionUnbounded, "projection bytes/fanout/coalesce 任一无界", "projection contract 与缺失 bound"),
                Red("CAP-005", GasProofKind.Capacity, GasProofFailureKind.DependencyOrCleanupUnbounded, GasProofRuleIds.DependencyOrCleanupUnbounded, "dependency/cleanup/audit 任一无界", "cycle/edge/cleanup dimension 与 provenance"),
                Red("CAP-006", GasProofKind.Capacity, GasProofFailureKind.RuntimeConsumerMissing, GasProofRuleIds.CapacityProofMissing, "proof 字段仅声明、预分配或无第一消费者", "consumer status、第一消费点与 profile 字段"),
                Red("CAP-007", GasProofKind.Capacity, GasProofFailureKind.MemoryBudgetMissing, GasProofRuleIds.CapacityProofMissing, "没有显式目标内存预算", "checked byte lower-bound、ABI hash 与目标规模"),
                Red("CAP-008", GasProofKind.Capacity, GasProofFailureKind.MemoryBudgetExceeded, GasProofRuleIds.CapacityProofMissing, "已盘点目标 payload 字节已超过预算", "required/available bytes、inventory ABI hash 与 budget provenance"),
                Red("CAP-009", GasProofKind.Capacity, GasProofFailureKind.MemoryCoverageIncomplete, GasProofRuleIds.CapacityProofMissing, "当前只盘点元素 payload，未覆盖 allocator/container 峰值", "scoped payload bytes 与预算 provenance"),
                Red("CAP-010", GasProofKind.Capacity, GasProofFailureKind.ProofBuildBudgetExceeded, GasProofRuleIds.CapacityProofMissing, "Definition/bound 数量或估算工作集越过 proof 构建硬门", "definition count、bound count 与 checked bytes"),
                Red("CAP-011", GasProofKind.Capacity, GasProofFailureKind.ProofCoverageIncomplete, GasProofRuleIds.CapacityProofMissing, "canonical adapter、语义维度、Runtime 公式或 authority 消费仍不完整", "稳定 coverage gap flags"),
                Red("ID-001", GasProofKind.Capacity, GasProofFailureKind.ProofHashMismatch, GasProofRuleIds.NonCanonicalIdentity, "graph/layout/contract/proof hash 不一致", "两侧 hash、版本与 canonical ordinal"),
            };
        }

        /// <summary>
        /// 创建一条 Runtime v1 consumer map 记录。
        /// </summary>
        private static GasProofConsumerEntry Consumer(
            string dimensionId,
            string profileField,
            string firstConsumer,
            string consumptionMoment,
            GasProofConsumerStatus status)
        {
            return new GasProofConsumerEntry
            {
                DimensionId = dimensionId,
                ProfileField = profileField,
                FirstConsumer = firstConsumer,
                ConsumptionMoment = consumptionMoment,
                Status = status,
            };
        }

        /// <summary>
        /// 创建一条以 semantic enum 名称作为稳定 DimensionId 的 consumer map 记录。
        /// </summary>
        private static GasProofConsumerEntry Consumer(
            GasCapacityDimension dimension,
            string profileField,
            string firstConsumer,
            string consumptionMoment,
            GasProofConsumerStatus status)
        {
            return Consumer(dimension.ToString(), profileField, firstConsumer,
                consumptionMoment, status);
        }

        /// <summary>
        /// 创建一条稳定 Red 矩阵记录。
        /// </summary>
        private static GasProofRedMatrixEntry Red(
            string caseId,
            GasProofKind proofKind,
            GasProofFailureKind failureKind,
            string ruleId,
            string trigger,
            string requiredEvidence)
        {
            return new GasProofRedMatrixEntry(
                caseId,
                proofKind,
                failureKind,
                ruleId,
                trigger,
                requiredEvidence);
        }
    }
}
