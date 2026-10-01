using System;
using GAS.Runtime;
using Unity.Collections.LowLevel.Unsafe;

namespace GAS.Editor.CodeGen.Proofs
{
    /// <summary>
    /// 盘点 GasTickScratch.Create、固定 Length 与 EnsureCapacity 的真实元素公式及目标规模 payload 字节。
    /// </summary>
    internal static class GasRuntimeV1CapacityMemoryInventory
    {
        /// <summary>
        /// 构建 scratch、durable、effective/hard-cap batch 与预算筛查证据；只做算术，不申请目标规模内存。
        /// </summary>
        public static void Build(GasCapacityProofBuildState state)
        {
            var profile = state.Payload.Profile;
            var attributeCount = state.Layout == null || state.Layout.Attributes == null
                ? 0
                : state.Layout.Attributes.Length;
            var tagCount = state.Layout == null || state.Layout.Tags == null
                ? 0
                : state.Layout.Tags.Length;
            GasCheckedProofMath.TryCalculateWordCount(attributeCount, out var attributeWords);
            GasCheckedProofMath.TryCalculateWordCount(tagCount, out var tagWords);
            BuildScratch(state, attributeCount, attributeWords, tagCount, tagWords);
            BuildSessionDurable(state);
            BuildAscDurable(state, attributeCount, attributeWords, tagCount, tagWords);
            Summarize(state, profile.MaxAscRegistryCount, profile.MaxFixedTicksPerBatch);
            ValidateMemoryBudget(state);
        }

        /// <summary>
        /// 盘点 GasTickScratch.Create 的标量、工作记录、target full-plane 与 capture arrays。
        /// </summary>
        private static void BuildScratch(
            GasCapacityProofBuildState state,
            int attributeCount,
            int attributeWords,
            int tagCount,
            int tagWords)
        {
            BuildScratchCore(state);
            BuildScratchPublish(state);
            BuildScratchTargetPlanes(state, attributeCount, attributeWords, tagCount, tagWords);
            BuildScratchProjectionPlanes(state);
        }

        /// <summary>
        /// 盘点 Tick DAG 主工作记录 arrays，其 count 与 GasTickScratch.Create 保持一一对应。
        /// </summary>
        private static void BuildScratchCore(GasCapacityProofBuildState state)
        {
            var profile = state.Payload.Profile;
            AddScratch<GasTickExecutionState>(state, "Scratch.Execution", 1, "1");
            AddScratch<PlanExpandScratchEnvelopeToken>(state, "Scratch.Envelope", 1, "1");
            AddScratch<GasAdmissionResult>(state, "Scratch.Admission", 1, "1");
            AddScratch<GasSealedCommand>(state, "Scratch.SealedCommands",
                profile.MaxBoundaryCommandCount, "MaxBoundaryCommandCount");
            AddScratch<GasOwnerPlanRecord>(state, "Scratch.OwnerPlans",
                profile.MaxOwnerPlanCount, "MaxOwnerPlanCount");
            AddScratch<GasResolvedTargetRecord>(state, "Scratch.ResolvedTargets",
                profile.MaxResolvedTargetCount, "MaxResolvedTargetCount");
            AddScratch<GasEffectOperationRecord>(state, "Scratch.EffectOperations",
                profile.MaxEffectOperationCount, "MaxEffectOperationCount");
            AddScratch<GasTargetResolveRejectionRecord>(state, "Scratch.TargetResolveRejections",
                profile.MaxEffectOperationCount, "MaxEffectOperationCount");
            AddScratch<GasSourceSpecRecord>(state, "Scratch.SourceSpecs",
                profile.MaxEffectOperationCount, "MaxEffectOperationCount");
            AddScratch<GasApplicationOutcomeRecord>(state, "Scratch.ApplicationOutcomes",
                Math.Max(profile.MaxEffectOperationCount, profile.MaxCoreFactCount),
                "max(MaxEffectOperationCount, MaxCoreFactCount)");
            AddScratch<GasAttributeMutationRecord>(state, "Scratch.AttributeMutations",
                profile.MaxCoreFactCount, "MaxCoreFactCount");
            AddScratch<GasAttributeMutationOutcomeRecord>(state, "Scratch.MutationOutcomes",
                profile.MaxCoreFactCount, "MaxCoreFactCount");
            AddScratch<GasCoreFactRecord>(state, "Scratch.CoreFacts",
                profile.MaxCoreFactCount, "MaxCoreFactCount");
            AddScratch<GasAbilityRouteRecord>(state, "Scratch.AbilityRoutes",
                profile.MaxNextTickRouteCount, "MaxNextTickRouteCount");
            AddScratch<GasOwnerResourceDemand>(state, "Scratch.OwnerDemands",
                profile.MaxOwnerReservationCount, "MaxOwnerReservationCount");
            AddScratch<GasTargetResourceDemand>(state, "Scratch.TargetDemands",
                profile.MaxTargetReservationCount, "MaxTargetReservationCount");
        }

        /// <summary>
        /// 盘点 admission、publish intent 与 target shadow header arrays。
        /// </summary>
        private static void BuildScratchPublish(GasCapacityProofBuildState state)
        {
            var profile = state.Payload.Profile;
            AddScratch<GasTargetShadowState>(state, "Scratch.TargetShadows",
                profile.MaxAscRegistryCount, "MaxAscRegistryCount");
            AddScratch<GasFinalPublishDecision>(state, "Scratch.FinalPublishDecision", 1, "1");
            AddScratch<GasTerminalBattlePublishIntent>(state, "Scratch.TerminalBattleIntents",
                profile.MaxBattleInstanceCount, "MaxBattleInstanceCount");
            AddScratch<GasSessionLifecyclePublishIntent>(state, "Scratch.SessionLifecycleIntent", 1, "1");
            var pendingSlotCount = Product(state, "scratch.pending-slot-intents",
                "PendingCommandSlotIntents", "MaxNextTickRouteCount",
                profile.MaxNextTickRouteCount, "SlotAndHead", 2);
            AddScratch<GasPendingCommandSlotPublishIntent>(state,
                "Scratch.PendingCommandSlotIntents", pendingSlotCount,
                "MaxNextTickRouteCount * 2");
            AddScratch<GasPendingCommandHeadPublishIntent>(state,
                "Scratch.PendingCommandHeadIntents", profile.MaxAscRegistryCount,
                "MaxAscRegistryCount");
            AddScratch<GasBoundaryFactPublishIntent>(state, "Scratch.BoundaryFactIntents",
                profile.MaxCoreFactCount, "MaxCoreFactCount");
        }

        /// <summary>
        /// 盘点当前 Runtime v1 的 target × stride 全平面，不把它误标为 sparse overlay。
        /// </summary>
        private static void BuildScratchTargetPlanes(
            GasCapacityProofBuildState state,
            int attributeCount,
            int attributeWords,
            int tagCount,
            int tagWords)
        {
            var profile = state.Payload.Profile;
            AddTargetPlane<ActiveEffectSlot>(state, "Scratch.TargetActiveEffects",
                profile.MaxActiveEffectCount, "MaxActiveEffectCount");
            AddTargetPlane<AttributeValueSlot>(state, "Scratch.TargetAttributes",
                attributeCount, "AttributeCount");
            AddTargetPlane<AttributeDirtyWord>(state, "Scratch.TargetAttributeDirtyWords",
                attributeWords, "ceil(AttributeCount / 64)");
            AddTargetPlane<TagCountSlot>(state, "Scratch.TargetTagCounts",
                tagCount, "TagCount");
            AddTargetPlane<TagPresenceWord>(state, "Scratch.TargetTagPresenceWords",
                tagWords, "ceil(TagCount / 64)");
            AddTargetPlane<GasPayloadRangeRecord>(state, "Scratch.TargetPayloadRanges",
                profile.MaxPayloadRangeRecordCount, "MaxPayloadRangeRecordCount");
            AddTargetPlane<GasPayloadValueSlot>(state, "Scratch.TargetPayloadValues",
                profile.MaxPayloadValueCount, "MaxPayloadValueCount");
        }

        /// <summary>
        /// 盘点 capture/value/evaluator arrays，并固定 evaluator stack 是 max 而不是 product。
        /// </summary>
        private static void BuildScratchProjectionPlanes(GasCapacityProofBuildState state)
        {
            var profile = state.Payload.Profile;
            var rowCount = Math.Max(1L, profile.MaxEffectOperationCount);
            var captureStride = Math.Max(1L,
                state.Maximum(GasCapacityDimension.CaptureDescriptors));
            var valueViewStride = Math.Max(1L,
                state.Maximum(GasCapacityDimension.ValueViews));
            var evaluatorLength = Math.Max(1L,
                state.Maximum(GasCapacityDimension.EvaluatorInstructions));
            var captureCount = Product(state, "scratch.capture-values", "CaptureValues",
                "EffectRows", rowCount, "CaptureStride", captureStride);
            var valueViewCount = Product(state, "scratch.value-views", "ValueViews",
                "EffectRows", rowCount, "ValueViewStride", valueViewStride);
            AddScratch<float>(state, "Scratch.CaptureValues", captureCount,
                "max(1, MaxEffectOperationCount) * max(1, CaptureStride)");
            AddScratch<float>(state, "Scratch.ValueViews", valueViewCount,
                "max(1, MaxEffectOperationCount) * max(1, ValueViewStride)");
            AddScratch<float>(state, "Scratch.PeriodCaptureValues", captureStride,
                "max(1, CaptureStride)");
            AddScratch<float>(state, "Scratch.PeriodValueViews", valueViewStride,
                "max(1, ValueViewStride)");
            AddScratch<float>(state, "Scratch.EvaluatorStack",
                Math.Max(1L, Math.Max(profile.MaxEffectOperationCount, evaluatorLength)),
                "max(1, MaxEffectOperationCount, EvaluatorProgramLength)");
        }

        /// <summary>
        /// 盘点 Session 持久 Buffer 的 EnsureCapacity 元素字节下界。
        /// </summary>
        private static void BuildSessionDurable(GasCapacityProofBuildState state)
        {
            var profile = state.Payload.Profile;
            AddDurable<BattleInstanceSlot>(state, "Durable.Session.Battles", "Session",
                profile.MaxBattleInstanceCount, "MaxBattleInstanceCount");
            AddDurable<AscRegistrySlot>(state, "Durable.Session.AscRegistry", "Session",
                profile.MaxAscRegistryCount, "MaxAscRegistryCount");
            AddDurable<SpawnBatchMemberManifestSlot>(state, "Durable.Session.SpawnManifest", "Session",
                profile.MaxAscRegistryCount, "MaxAscRegistryCount");
            AddDurable<BoundaryCommandInbox>(state, "Durable.Session.BoundaryCommands", "Session",
                profile.MaxBoundaryCommandCount, "MaxBoundaryCommandCount");
            AddDurable<BoundaryCommandFrozenPayload>(state, "Durable.Session.CommandPayload", "Session",
                profile.MaxBoundaryCommandPayloadCount, "MaxBoundaryCommandPayloadCount");
            AddDurable<BoundaryFactBuffer>(state, "Durable.Session.BoundaryFacts", "Session",
                profile.MaxSessionBoundaryFactCount, "MaxSessionBoundaryFactCount");
        }

        /// <summary>
        /// 盘点单 ASC 固定逻辑 Buffer 与全部可变 EnsureCapacity 元素字节下界。
        /// </summary>
        private static void BuildAscDurable(
            GasCapacityProofBuildState state,
            int attributeCount,
            int attributeWords,
            int tagCount,
            int tagWords)
        {
            BuildAscFixedDurable(state, attributeCount, attributeWords, tagCount, tagWords);
            BuildAscInitializationDurable(state);
            BuildAscAbilityDurable(state);
            BuildAscEffectDurable(state);
        }

        /// <summary>
        /// 盘点每 ASC 的 Attribute/Tag authority 与派生 words 固定长度。
        /// </summary>
        private static void BuildAscFixedDurable(
            GasCapacityProofBuildState state,
            int attributeCount,
            int attributeWords,
            int tagCount,
            int tagWords)
        {
            AddFixed<AttributeValueSlot>(state, "Durable.Asc.Attributes", "ASC",
                attributeCount, "AttributeCount");
            AddFixed<AttributeDirtyWord>(state, "Durable.Asc.AttributeDirtyWords", "ASC",
                attributeWords, "ceil(AttributeCount / 64)");
            AddFixed<TagCountSlot>(state, "Durable.Asc.TagCounts", "ASC",
                tagCount, "TagCount");
            AddFixed<TagPresenceWord>(state, "Durable.Asc.TagPresenceWords", "ASC",
                tagWords, "ceil(TagCount / 64)");
        }

        /// <summary>
        /// 盘点每 ASC 的 SpawnFinalize pending buffers。
        /// </summary>
        private static void BuildAscInitializationDurable(GasCapacityProofBuildState state)
        {
            var profile = state.Payload.Profile;
            AddDurable<PendingAttributeInitialization>(state, "Durable.Asc.PendingAttributes", "ASC",
                profile.MaxPendingAttributeInitializationCount, "MaxPendingAttributeInitializationCount");
            AddDurable<PendingTagInitialization>(state, "Durable.Asc.PendingTags", "ASC",
                profile.MaxPendingTagInitializationCount, "MaxPendingTagInitializationCount");
            AddDurable<PendingGrantedAbilityInitialization>(state, "Durable.Asc.PendingGrants", "ASC",
                profile.MaxPendingGrantedAbilityInitializationCount, "MaxPendingGrantedAbilityInitializationCount");
            AddDurable<PendingInitialGameplayEffect>(state, "Durable.Asc.PendingInitialEffects", "ASC",
                profile.MaxEffectOperationCount, "MaxEffectOperationCount");
        }

        /// <summary>
        /// 盘点每 ASC 的 Ability 生命周期与反向引用 slab capacities。
        /// </summary>
        private static void BuildAscAbilityDurable(GasCapacityProofBuildState state)
        {
            var profile = state.Payload.Profile;
            AddDurable<GrantedAbilitySlot>(state, "Durable.Asc.Grants", "ASC",
                profile.MaxGrantedAbilityCount, "MaxGrantedAbilityCount");
            AddDurable<AbilityActivationSlot>(state, "Durable.Asc.Activations", "ASC",
                profile.MaxAbilityActivationCount, "MaxAbilityActivationCount");
            AddDurable<AbilityContinuationSlot>(state, "Durable.Asc.Continuations", "ASC",
                profile.MaxAbilityContinuationCount, "MaxAbilityContinuationCount");
            AddDurable<AbilitySubscriptionSlot>(state, "Durable.Asc.Subscriptions", "ASC",
                profile.MaxAbilitySubscriptionCount, "MaxAbilitySubscriptionCount");
            AddDurable<CooldownGateSlot>(state, "Durable.Asc.Cooldowns", "ASC",
                profile.MaxCooldownGateCount, "MaxCooldownGateCount");
            AddDurable<ActivationOwnedContributionSlot>(state, "Durable.Asc.OwnedContributions", "ASC",
                profile.MaxActivationOwnedContributionCount, "MaxActivationOwnedContributionCount");
            AddDurable<EmittedApplicationRefSlot>(state, "Durable.Asc.EmittedRefs", "ASC",
                profile.MaxEmittedApplicationRefCount, "MaxEmittedApplicationRefCount");
        }

        /// <summary>
        /// 盘点每 ASC 的 Effect、payload、dependency、pending command 与 outbox capacities。
        /// </summary>
        private static void BuildAscEffectDurable(GasCapacityProofBuildState state)
        {
            var profile = state.Payload.Profile;
            AddDurable<ActiveEffectSlot>(state, "Durable.Asc.ActiveEffects", "ASC",
                profile.MaxActiveEffectCount, "MaxActiveEffectCount");
            AddDurable<GasPayloadRangeRecord>(state, "Durable.Asc.PayloadRanges", "ASC",
                profile.MaxPayloadRangeRecordCount, "MaxPayloadRangeRecordCount");
            AddDurable<GasPayloadValueSlot>(state, "Durable.Asc.PayloadValues", "ASC",
                profile.MaxPayloadValueCount, "MaxPayloadValueCount");
            AddDurable<AttributeAggregatorSlot>(state, "Durable.Asc.Aggregators", "ASC",
                profile.MaxAttributeAggregatorCount, "MaxAttributeAggregatorCount");
            AddDurable<LiveDependencySlot>(state, "Durable.Asc.LiveDependencies", "ASC",
                profile.MaxLiveDependencyCount, "MaxLiveDependencyCount");
            AddDurable<LiveDependencyRouteSlot>(state, "Durable.Asc.LiveRoutes", "ASC",
                profile.MaxLiveDependencyRouteCount, "MaxLiveDependencyRouteCount");
            AddDurable<PendingCommand>(state, "Durable.Asc.PendingCommands", "ASC",
                profile.MaxPendingCommandCount, "MaxPendingCommandCount");
            AddDurable<BoundaryFactBuffer>(state, "Durable.Asc.BoundaryFacts", "ASC",
                profile.MaxAscBoundaryFactCount, "MaxAscBoundaryFactCount");
        }

        /// <summary>
        /// 汇总单 Tick scratch、最大 batch、Session 与全部 ASC 的 checked 字节下界。
        /// </summary>
        private static void Summarize(
            GasCapacityProofBuildState state,
            int ascCount,
            int tickBatchCount)
        {
            var provenance = state.Payload.ScaleProfileProvenance;
            var scratch = Sum(state, GasCapacityMemoryKind.ScratchNativeArray, string.Empty,
                "memory.scratch.sum", provenance);
            var session = Sum(state, GasCapacityMemoryKind.DurableBufferCapacity, "Session",
                "memory.session.sum", provenance);
            var perAscCapacity = Sum(state, GasCapacityMemoryKind.DurableBufferCapacity, "ASC",
                "memory.asc.capacity.sum", provenance);
            var perAscFixed = Sum(state, GasCapacityMemoryKind.DurableFixedLength, "ASC",
                "memory.asc.fixed.sum", provenance);
            state.TryAdd("memory.asc.total", "DurableBytesPerAsc",
                "EnsureCapacityBytes", perAscCapacity, "FixedLengthBytes", perAscFixed,
                "bytes", provenance, 0, out var perAsc);
            state.TryMultiply("memory.scratch.effective-batch",
                "EffectiveTickBatchScratchBytes",
                "OneTickScratchBytes", scratch, "MaximumDeltaTimeTicks",
                state.Payload.Profile.MaximumDeltaTimeTicks,
                "bytes", provenance, 0, out var effectiveBatchScratch);
            state.TryMultiply("memory.scratch.hard-cap-batch",
                "DeclaredHardCapTickBatchScratchBytes",
                "OneTickScratchBytes", scratch, "MaxFixedTicksPerBatch", tickBatchCount,
                "bytes", provenance, 0, out var hardCapBatchScratch);
            state.TryMultiply("memory.asc.all", "DurableAllAscBytes",
                "DurableBytesPerAsc", perAsc, "MaxAscRegistryCount", ascCount,
                "bytes", provenance, 0, out var allAsc);
            state.TryAdd("memory.total.durable", "DurableScopedPayloadBytes",
                "DurableSessionBytes", session, "DurableAllAscBytes", allAsc,
                "bytes", provenance, 0, out var durable);
            state.TryAdd("memory.total.effective", "EffectiveScopedPayloadBytes",
                "EffectiveTickBatchScratchBytes", effectiveBatchScratch,
                "DurableBytes", durable, "bytes", provenance, 0,
                out var effectiveTotal);
            state.TryAdd("memory.total.hard-cap", "DeclaredHardCapScopedPayloadBytes",
                "DeclaredHardCapTickBatchScratchBytes", hardCapBatchScratch,
                "DurableBytes", durable, "bytes", provenance, 0,
                out var hardCapTotal);
            var declaredBudget = state.Source == null
                ? 0
                : state.Source.DeclaredMemoryBudgetBytes;
            state.Payload.MemorySummary = new GasCapacityMemorySummary
            {
                OneTickScratchBytes = scratch,
                EffectiveTickBatchScratchBytes = effectiveBatchScratch,
                DeclaredHardCapTickBatchScratchBytes = hardCapBatchScratch,
                DurableSessionBytes = session,
                DurableBytesPerAsc = perAsc,
                DurableAllAscBytes = allAsc,
                EffectiveScopedPayloadBytes = effectiveTotal,
                DeclaredHardCapScopedPayloadBytes = hardCapTotal,
                DeclaredMemoryBudgetBytes = declaredBudget,
            };
            AddSummary(state, "Summary.OneTickScratchBytes", scratch);
            AddSummary(state, "Summary.DurableAllAscBytes", allAsc);
            AddSummary(state, "Summary.EffectiveScopedPayloadBytes", effectiveTotal);
            AddSummary(state, "Summary.DeclaredHardCapScopedPayloadBytes", hardCapTotal);
        }

        /// <summary>
        /// 用已盘点 payload 只做必败筛查；未覆盖峰值始终保持 Unknown/Red。
        /// </summary>
        private static void ValidateMemoryBudget(GasCapacityProofBuildState state)
        {
            if (state.Source == null)
                return;
            var budget = state.Payload.MemorySummary.DeclaredMemoryBudgetBytes;
            var required = state.Payload.MemorySummary.EffectiveScopedPayloadBytes;
            var provenance = state.Payload.MemoryBudgetProvenance;
            if (budget <= 0)
            {
                state.AddFailure(GasProofFailureKind.MemoryBudgetMissing,
                    GasProofRuleIds.CapacityProofMissing, "MemoryBudgetBytes", string.Empty,
                    ">", "DeclaredMemoryBudgetBytes", budget, "Zero", 0,
                    0, required, "bytes", 0, -1, "Scale/Profile release gate",
                    provenance);
            }
            else if (required > budget)
                state.AddFailure(GasProofFailureKind.MemoryBudgetExceeded,
                    GasProofRuleIds.CapacityProofMissing, "MemoryBudgetBytes", string.Empty,
                    "<=", "RequiredBytes", required, "BudgetBytes", budget,
                    budget, required, "bytes", 0, -1, "Scale/Profile release gate",
                    provenance);
            state.AddFailure(GasProofFailureKind.MemoryCoverageIncomplete,
                GasProofRuleIds.CapacityProofMissing, "MemoryCoverage", string.Empty,
                "contains", "CoveredPayloadBytes", required,
                "AllocatorAndContainerOverhead", 0, 0, required, "bytes",
                0, -1, "Scale/Profile release gate", provenance);
        }

        /// <summary>
        /// 添加 target full-plane count = MaxAscRegistryCount × stride 的 checked scratch 证据。
        /// </summary>
        private static void AddTargetPlane<T>(
            GasCapacityProofBuildState state,
            string evidenceId,
            long stride,
            string strideName)
            where T : unmanaged
        {
            var count = Product(state, "target-plane." + evidenceId, evidenceId,
                "MaxAscRegistryCount", state.Payload.Profile.MaxAscRegistryCount,
                strideName, stride);
            AddScratch<T>(state, evidenceId, count,
                "MaxAscRegistryCount * " + strideName);
        }

        /// <summary>
        /// 添加一个真实 NativeArray 的 int length、ABI 与 checked byte evidence。
        /// </summary>
        private static void AddScratch<T>(
            GasCapacityProofBuildState state,
            string evidenceId,
            long count,
            string formula)
            where T : unmanaged
        {
            AddMemory<T>(state, evidenceId, GasCapacityMemoryKind.ScratchNativeArray,
                "TickScratch", count, formula, true);
        }

        /// <summary>
        /// 添加一个 DynamicBuffer EnsureCapacity 的元素字节下界 evidence。
        /// </summary>
        private static void AddDurable<T>(
            GasCapacityProofBuildState state,
            string evidenceId,
            string owner,
            long count,
            string formula)
            where T : unmanaged
        {
            AddMemory<T>(state, evidenceId, GasCapacityMemoryKind.DurableBufferCapacity,
                owner, count, formula, false);
        }

        /// <summary>
        /// 添加一个 TryInitializeFixedBuffers 精确逻辑 Length 的元素字节证据。
        /// </summary>
        private static void AddFixed<T>(
            GasCapacityProofBuildState state,
            string evidenceId,
            string owner,
            long count,
            string formula)
            where T : unmanaged
        {
            AddMemory<T>(state, evidenceId, GasCapacityMemoryKind.DurableFixedLength,
                owner, count, formula, false);
        }

        /// <summary>
        /// 用 UnsafeUtility 固定一个 unmanaged 类型的 size/alignment 并计算 checked bytes。
        /// </summary>
        private static void AddMemory<T>(
            GasCapacityProofBuildState state,
            string evidenceId,
            GasCapacityMemoryKind kind,
            string owner,
            long count,
            string formula,
            bool requireIntLength)
            where T : unmanaged
        {
            var safeCount = count < 0 ? 0 : count;
            var size = UnsafeUtility.SizeOf<T>();
            var alignment = UnsafeUtility.AlignOf<T>();
            if (requireIntLength && !GasCheckedProofMath.TryToArrayLength(safeCount, out _))
                state.AddFailure(GasProofFailureKind.RuntimeEncodingLimitExceeded,
                    GasProofRuleIds.CapacityProofMissing, evidenceId, evidenceId + ".length",
                    "<=", "ElementCount", safeCount, "Int32Max", int.MaxValue,
                    int.MaxValue, safeCount, "elements", 0, -1,
                    "CollectionHelper.CreateNativeArray", state.Payload.ScaleProfileProvenance);
            state.TryMultiply(evidenceId + ".bytes", evidenceId,
                "ElementCount", safeCount, "ElementSizeBytes", size,
                "bytes", state.Payload.ScaleProfileProvenance, 0, out var bytes);
            state.Memory.Add(new GasCapacityMemoryEvidence
            {
                EvidenceId = evidenceId,
                MemoryKind = kind,
                Owner = owner,
                ElementTypeName = typeof(T).FullName,
                Formula = formula,
                ElementCount = safeCount,
                ElementSizeBytes = size,
                AlignmentBytes = alignment,
                CheckedByteCount = bytes,
                AbiFingerprint = ComputeAbiFingerprint(typeof(T).FullName, size, alignment),
            });
        }

        /// <summary>
        /// 执行并记录一个 capacity count 的 checked 乘法。
        /// </summary>
        private static long Product(
            GasCapacityProofBuildState state,
            string derivationId,
            string dimensionId,
            string leftName,
            long left,
            string rightName,
            long right)
        {
            state.TryMultiply(derivationId, dimensionId, leftName, left,
                rightName, right, "elements", state.Payload.ScaleProfileProvenance,
                0, out var result);
            return result;
        }

        /// <summary>
        /// checked-sum 指定 memory kind/owner 的 byte evidence。
        /// </summary>
        private static long Sum(
            GasCapacityProofBuildState state,
            GasCapacityMemoryKind kind,
            string owner,
            string derivationPrefix,
            in GasProofProvenance provenance)
        {
            var total = 0L;
            var ordinal = 0;
            foreach (var evidence in state.Memory)
            {
                if (evidence.MemoryKind != kind ||
                    (!string.IsNullOrEmpty(owner) && evidence.Owner != owner))
                    continue;
                state.TryAdd(derivationPrefix + "." + ordinal, derivationPrefix,
                    "AccumulatedBytes", total, evidence.EvidenceId,
                    evidence.CheckedByteCount, "bytes", provenance, 0, out total);
                ordinal++;
            }
            return total;
        }

        /// <summary>
        /// 添加一个不重复计算 ABI 的汇总字节 evidence。
        /// </summary>
        private static void AddSummary(
            GasCapacityProofBuildState state,
            string evidenceId,
            long bytes)
        {
            state.Memory.Add(new GasCapacityMemoryEvidence
            {
                EvidenceId = evidenceId,
                MemoryKind = GasCapacityMemoryKind.Summary,
                Owner = "Proof",
                ElementTypeName = "byte",
                Formula = evidenceId,
                ElementCount = bytes,
                ElementSizeBytes = 1,
                AlignmentBytes = 1,
                CheckedByteCount = bytes,
                AbiFingerprint = ComputeAbiFingerprint("byte", 1, 1),
            });
        }

        /// <summary>
        /// 计算一个 Runtime element type 的 size/alignment ABI 指纹。
        /// </summary>
        private static string ComputeAbiFingerprint(string typeName, int size, int alignment)
        {
            using (var writer = new GasProofCanonicalHashWriter())
            {
                writer.WriteString("gas-runtime-v1-element-abi/1");
                writer.WriteString(typeName);
                writer.WriteInt32(size);
                writer.WriteInt32(alignment);
                return writer.ComputeHash();
            }
        }
    }
}
