using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime
{
    /// <summary>
    /// 集中组装 Runtime v1 唯一 Tick Job DAG，禁止通过主线程 Complete 读取中间结果决定后续调度。
    /// </summary>
    internal static class GasTickDag
    {
        /// <summary>
        /// 为所有未 Disposed Kernel update 预排 cleanup maintenance，并把 shell removal 写入 EndFixed ECB。
        /// </summary>
        internal static JobHandle ScheduleCleanupAcceptedPrepass(
            ref SystemState state,
            NativeList<Entity> acceptedShells,
            EntityCommandBuffer endFixed,
            JobHandle dependency)
        {
            var handle = new GasCleanupAcceptedPrepassJob
            {
                AcceptedShells = acceptedShells.AsDeferredJobArray(),
                DrainStates = state.GetComponentLookup<BoundaryDrainState>(true),
                FactBuffers = state.GetBufferLookup<BoundaryFactBuffer>(true),
                EndFixed = endFixed,
            }.Schedule(dependency);
            return acceptedShells.Dispose(handle);
        }

        /// <summary>
        /// 按 immutable ScaleProfile 创建当前 Tick 全部定长 scratch。
        /// </summary>
        internal static GasTickScratch CreateScratch(
            ref SystemState state,
            in GasScaleProfile profile,
            BlobAssetReference<GasDefinitionCatalogBlob> catalog)
        {
            return GasTickScratch.Create(in profile, catalog, state.WorldUpdateAllocator);
        }

        /// <summary>
        /// 保留仅供无 Catalog 的布局测试使用的 scratch 构造入口；正式 Kernel 必须传入 immutable Catalog。
        /// </summary>
        internal static GasTickScratch CreateScratch(
            ref SystemState state,
            in GasScaleProfile profile)
        {
            return GasTickScratch.Create(in profile, state.WorldUpdateAllocator);
        }

        /// <summary>
        /// 在 SpawnFinalize 前以 Job 冻结 lifecycle dispatch，避免主线程读取可变状态形成隐式 fence。
        /// </summary>
        internal static JobHandle ScheduleModeCapture(
            ref SystemState state,
            Entity session,
            in GasTickScratch scratch,
            JobHandle dependency)
        {
            return new GasCaptureKernelModeJob
            {
                Session = session,
                Lifecycles = state.GetComponentLookup<GasSessionLifecycle>(true),
                Execution = scratch.Execution,
            }.Schedule(dependency);
        }

        /// <summary>
        /// 使用已冻结 dispatch 和完整 scratch，无条件排出 Gather 到 TickFinalize 的完整依赖链。
        /// </summary>
        internal static JobHandle ScheduleGameplay(
            ref SystemState state,
            Entity session,
            in GasSessionIdentity identity,
            in GasScaleProfile profile,
            in GasDefinitionRegistry definitions,
            in GasTickScratch scratch,
            EntityCommandBuffer targetShadowCommands,
            JobHandle dependency)
        {
            dependency = SchedulePlanAndAdmission(
                ref state,
                session,
                identity.SimulationEpoch,
                in profile,
                definitions.Catalog,
                in scratch,
                dependency);
            dependency = ScheduleDownstream(
                ref state,
                session,
                identity.SimulationEpoch,
                in profile,
                definitions.Catalog,
                in scratch,
                targetShadowCommands,
                dependency);
            return ScheduleFinalize(ref state, session, in scratch, dependency);
        }

        /// <summary>
        /// 按 Gather、Provision、OwnerPlan、TargetResolve、Admission、FaultLatch 顺序排出零权威规划链。
        /// </summary>
        private static JobHandle SchedulePlanAndAdmission(
            ref SystemState state,
            Entity session,
            ulong simulationEpoch,
            in GasScaleProfile profile,
            BlobAssetReference<GasDefinitionCatalogBlob> catalog,
            in GasTickScratch scratch,
            JobHandle dependency)
        {
            dependency = new GasGatherTickStartSnapshotJob
            {
                Session = session,
                Ticks = state.GetComponentLookup<SimulationTickState>(true),
                Inboxes = state.GetBufferLookup<BoundaryCommandInbox>(),
                FrozenPayloads = state.GetBufferLookup<BoundaryCommandFrozenPayload>(true),
                Execution = scratch.Execution,
                SealedCommands = scratch.SealedCommands,
            }.Schedule(dependency);
            dependency = new GasPlanExpandScratchProvisionJob
            {
                Catalog = catalog,
                Profile = profile,
                Execution = scratch.Execution,
                Envelope = scratch.Envelope,
            }.Schedule(dependency);
            dependency = new GasOwnerPlanBuildJob
            {
                Session = session,
                SimulationEpoch = simulationEpoch,
                Catalog = catalog,
                Ticks = state.GetComponentLookup<SimulationTickState>(true),
                AscIdentities = state.GetComponentLookup<GasAscIdentity>(true),
                AscLifecycles = state.GetComponentLookup<AscLifecycle>(true),
                Registries = state.GetBufferLookup<AscRegistrySlot>(true),
                Grants = state.GetBufferLookup<GrantedAbilitySlot>(true),
                Activations = state.GetBufferLookup<AbilityActivationSlot>(true),
                Continuations = state.GetBufferLookup<AbilityContinuationSlot>(true),
                Subscriptions = state.GetBufferLookup<AbilitySubscriptionSlot>(true),
                ActiveEffects = state.GetBufferLookup<ActiveEffectSlot>(true),
                PendingCommands = state.GetBufferLookup<PendingCommand>(true),
                Attributes = state.GetBufferLookup<AttributeValueSlot>(true),
                TagCounts = state.GetBufferLookup<TagCountSlot>(true),
                Cooldowns = state.GetBufferLookup<CooldownGateSlot>(true),
                SealedCommands = scratch.SealedCommands,
                Envelope = scratch.Envelope,
                OwnerPlans = scratch.OwnerPlans,
                AbilityRoutes = scratch.AbilityRoutes,
                Execution = scratch.Execution,
            }.Schedule(dependency);
            dependency = new GasTargetResolveExpandJob
            {
                Session = session,
                SimulationEpoch = simulationEpoch,
                Catalog = catalog,
                Envelope = scratch.Envelope,
                SealedCommands = scratch.SealedCommands,
                OwnerPlans = scratch.OwnerPlans,
                Registries = state.GetBufferLookup<AscRegistrySlot>(true),
                AscIdentities = state.GetComponentLookup<GasAscIdentity>(true),
                AscLifecycles = state.GetComponentLookup<AscLifecycle>(true),
                ActorBindings = state.GetComponentLookup<GasActorBinding>(true),
                ResolvedTargets = scratch.ResolvedTargets,
                EffectOperations = scratch.EffectOperations,
                TargetResolveRejections = scratch.TargetResolveRejections,
                Execution = scratch.Execution,
            }.Schedule(dependency);
            dependency = new GasWholeTickInfrastructureAdmissionJob
            {
                Session = session,
                SimulationEpoch = simulationEpoch,
                Profile = profile,
                Catalog = catalog,
                Envelope = scratch.Envelope,
                OwnerPlans = scratch.OwnerPlans,
                ResolvedTargets = scratch.ResolvedTargets,
                EffectOperations = scratch.EffectOperations,
                TargetResolveRejections = scratch.TargetResolveRejections,
                AbilityRoutes = scratch.AbilityRoutes,
                Registries = state.GetBufferLookup<AscRegistrySlot>(true),
                AscIdentities = state.GetComponentLookup<GasAscIdentity>(true),
                AscLifecycles = state.GetComponentLookup<AscLifecycle>(true),
                Memberships = state.GetComponentLookup<AscBattleMembership>(true),
                Battles = state.GetBufferLookup<BattleInstanceSlot>(true),
                SlabHeads = state.GetComponentLookup<AscSlabHeads>(true),
                Grants = state.GetBufferLookup<GrantedAbilitySlot>(true),
                Activations = state.GetBufferLookup<AbilityActivationSlot>(true),
                Continuations = state.GetBufferLookup<AbilityContinuationSlot>(true),
                Subscriptions = state.GetBufferLookup<AbilitySubscriptionSlot>(true),
                ActiveEffects = state.GetBufferLookup<ActiveEffectSlot>(true),
                Cooldowns = state.GetBufferLookup<CooldownGateSlot>(true),
                PendingCommands = state.GetBufferLookup<PendingCommand>(true),
                Attributes = state.GetBufferLookup<AttributeValueSlot>(true),
                AttributeDirtyWords = state.GetBufferLookup<AttributeDirtyWord>(true),
                TagCounts = state.GetBufferLookup<TagCountSlot>(true),
                TagPresenceWords = state.GetBufferLookup<TagPresenceWord>(true),
                PayloadStates = state.GetComponentLookup<GasPayloadRangeAllocatorState>(true),
                PayloadRanges = state.GetBufferLookup<GasPayloadRangeRecord>(true),
                PayloadValues = state.GetBufferLookup<GasPayloadValueSlot>(true),
                BoundaryDrains = state.GetComponentLookup<BoundaryDrainState>(true),
                BoundaryFacts = state.GetBufferLookup<BoundaryFactBuffer>(true),
                RequestTerminalBuffers = state.GetBufferLookup<GasRequestTerminalIntent>(true),
                SourceSpecs = scratch.SourceSpecs,
                ApplicationOutcomes = scratch.ApplicationOutcomes,
                AttributeMutations = scratch.AttributeMutations,
                MutationOutcomes = scratch.MutationOutcomes,
                CoreFacts = scratch.CoreFacts,
                EvaluatorStack = scratch.EvaluatorStack,
                TargetShadows = scratch.TargetShadows,
                FinalPublishDecision = scratch.FinalPublishDecision,
                TerminalBattleIntents = scratch.TerminalBattleIntents,
                SessionLifecycleIntent = scratch.SessionLifecycleIntent,
                PendingCommandSlotIntents = scratch.PendingCommandSlotIntents,
                PendingCommandHeadIntents = scratch.PendingCommandHeadIntents,
                BoundaryFactIntents = scratch.BoundaryFactIntents,
                RequestTerminalIntents = scratch.RequestTerminalIntents,
                TargetActiveEffects = scratch.TargetActiveEffects,
                TargetAttributes = scratch.TargetAttributes,
                TargetAttributeDirtyWords = scratch.TargetAttributeDirtyWords,
                TargetTagCounts = scratch.TargetTagCounts,
                TargetTagPresenceWords = scratch.TargetTagPresenceWords,
                TargetPayloadRanges = scratch.TargetPayloadRanges,
                TargetPayloadValues = scratch.TargetPayloadValues,
                TargetActiveEffectStride = scratch.TargetActiveEffectStride,
                TargetAttributeStride = scratch.TargetAttributeStride,
                TargetAttributeDirtyWordStride = scratch.TargetAttributeDirtyWordStride,
                TargetTagStride = scratch.TargetTagStride,
                TargetTagPresenceWordStride = scratch.TargetTagPresenceWordStride,
                TargetPayloadRangeStride = scratch.TargetPayloadRangeStride,
                TargetPayloadValueStride = scratch.TargetPayloadValueStride,
                Admission = scratch.Admission,
                Execution = scratch.Execution,
            }.Schedule(dependency);
            return ScheduleFaultLatch(
                ref state,
                session,
                simulationEpoch,
                in scratch,
                dependency);
        }

        /// <summary>
        /// 在 admission 后紧接唯一 fault writer，使后续预排 lane 只读取 AdmissionResult 决定 no-op。
        /// </summary>
        private static JobHandle ScheduleFaultLatch(
            ref SystemState state,
            Entity session,
            ulong simulationEpoch,
            in GasTickScratch scratch,
            JobHandle dependency,
            byte includePostAdmissionFailure = 0)
        {
            return new GasFaultLatchJob
            {
                Session = session,
                SimulationEpoch = simulationEpoch,
                Admission = scratch.Admission,
                Execution = scratch.Execution,
                FaultLatches = state.GetComponentLookup<SessionFaultLatch>(),
                Lifecycles = state.GetComponentLookup<GasSessionLifecycle>(),
                IncludePostAdmissionFailure = includePostAdmissionFailure,
            }.Schedule(dependency);
        }

        /// <summary>
        /// 无条件排出 admission 后的完整 gameplay/投影节点链，失败时各节点统一入口 no-op。
        /// </summary>
        private static JobHandle ScheduleDownstream(
            ref SystemState state,
            Entity session,
            ulong simulationEpoch,
            in GasScaleProfile profile,
            BlobAssetReference<GasDefinitionCatalogBlob> catalog,
            in GasTickScratch scratch,
            EntityCommandBuffer targetShadowCommands,
            JobHandle dependency)
        {
            dependency = new GasAscOwnerCommandWaveJob
            {
                Session = session,
                SimulationEpoch = simulationEpoch,
                Profile = profile,
                Catalog = catalog,
                Admission = scratch.Admission,
                OwnerPlans = scratch.OwnerPlans,
                AbilityRoutes = scratch.AbilityRoutes,
                Execution = scratch.Execution,
                Registries = state.GetBufferLookup<AscRegistrySlot>(true),
                AscIdentities = state.GetComponentLookup<GasAscIdentity>(true),
                AscLifecycles = state.GetComponentLookup<AscLifecycle>(true),
                Ticks = state.GetComponentLookup<SimulationTickState>(),
                SlabHeads = state.GetComponentLookup<AscSlabHeads>(),
                Grants = state.GetBufferLookup<GrantedAbilitySlot>(),
                Activations = state.GetBufferLookup<AbilityActivationSlot>(),
                Continuations = state.GetBufferLookup<AbilityContinuationSlot>(),
                Subscriptions = state.GetBufferLookup<AbilitySubscriptionSlot>(),
                ActiveEffects = state.GetBufferLookup<ActiveEffectSlot>(true),
                Cooldowns = state.GetBufferLookup<CooldownGateSlot>(),
                PendingCommands = state.GetBufferLookup<PendingCommand>(),
                Attributes = state.GetBufferLookup<AttributeValueSlot>(),
                AttributeDirtyWords = state.GetBufferLookup<AttributeDirtyWord>(),
                TagCounts = state.GetBufferLookup<TagCountSlot>(),
                TagPresence = state.GetBufferLookup<TagPresenceWord>(),
            }.Schedule(dependency);
            dependency = new GasSourceSpecProjectionJob
            {
                Session = session,
                SimulationEpoch = simulationEpoch,
                Catalog = catalog,
                Admission = scratch.Admission,
                EffectOperations = scratch.EffectOperations,
                TargetResolveRejections = scratch.TargetResolveRejections,
                Registries = state.GetBufferLookup<AscRegistrySlot>(true),
                Attributes = state.GetBufferLookup<AttributeValueSlot>(true),
                AscIdentities = state.GetComponentLookup<GasAscIdentity>(true),
                SourceSpecs = scratch.SourceSpecs,
                CaptureValues = scratch.CaptureValues,
                ValueViews = scratch.ValueViews,
                CaptureStride = scratch.CaptureStride,
                ValueViewStride = scratch.ValueViewStride,
                Execution = scratch.Execution,
            }.Schedule(dependency);
            dependency = new GasGroupWorkByTargetJob
            {
                Admission = scratch.Admission,
                SourceSpecs = scratch.SourceSpecs,
                Execution = scratch.Execution,
            }.Schedule(dependency);
            dependency = new GasAscTargetPrepareJob
            {
                Session = session,
                SimulationEpoch = simulationEpoch,
                Profile = profile,
                Catalog = catalog,
                Admission = scratch.Admission,
                SourceSpecs = scratch.SourceSpecs,
                CaptureValues = scratch.CaptureValues,
                ValueViews = scratch.ValueViews,
                PeriodCaptureValues = scratch.PeriodCaptureValues,
                PeriodValueViews = scratch.PeriodValueViews,
                CaptureStride = scratch.CaptureStride,
                ValueViewStride = scratch.ValueViewStride,
                EvaluatorStack = scratch.EvaluatorStack,
                Registries = state.GetBufferLookup<AscRegistrySlot>(true),
                AscIdentities = state.GetComponentLookup<GasAscIdentity>(true),
                AscLifecycles = state.GetComponentLookup<AscLifecycle>(true),
                SlabHeads = state.GetComponentLookup<AscSlabHeads>(true),
                ActiveEffects = state.GetBufferLookup<ActiveEffectSlot>(true),
                ActorBindings = state.GetComponentLookup<GasActorBinding>(true),
                Attributes = state.GetBufferLookup<AttributeValueSlot>(true),
                AttributeDirtyWords = state.GetBufferLookup<AttributeDirtyWord>(true),
                TagCounts = state.GetBufferLookup<TagCountSlot>(true),
                TagPresenceWords = state.GetBufferLookup<TagPresenceWord>(true),
                PayloadStates = state.GetComponentLookup<GasPayloadRangeAllocatorState>(true),
                PayloadRanges = state.GetBufferLookup<GasPayloadRangeRecord>(true),
                PayloadValues = state.GetBufferLookup<GasPayloadValueSlot>(true),
                FaultInjections = state.GetComponentLookup<GasTargetPrepareFaultInjection>(true),
                ShadowCommands = targetShadowCommands,
                TargetShadows = scratch.TargetShadows,
                ShadowActiveEffects = scratch.TargetActiveEffects,
                ShadowAttributes = scratch.TargetAttributes,
                ShadowAttributeDirtyWords = scratch.TargetAttributeDirtyWords,
                ShadowTagCounts = scratch.TargetTagCounts,
                ShadowTagPresenceWords = scratch.TargetTagPresenceWords,
                ShadowPayloadRanges = scratch.TargetPayloadRanges,
                ShadowPayloadValues = scratch.TargetPayloadValues,
                ActiveEffectStride = scratch.TargetActiveEffectStride,
                AttributeStride = scratch.TargetAttributeStride,
                AttributeDirtyWordStride = scratch.TargetAttributeDirtyWordStride,
                TagStride = scratch.TargetTagStride,
                TagPresenceWordStride = scratch.TargetTagPresenceWordStride,
                PayloadRangeStride = scratch.TargetPayloadRangeStride,
                PayloadValueStride = scratch.TargetPayloadValueStride,
                ApplicationOutcomes = scratch.ApplicationOutcomes,
                AttributeMutations = scratch.AttributeMutations,
                MutationOutcomes = scratch.MutationOutcomes,
                Execution = scratch.Execution,
            }.Schedule(dependency);
            dependency = new GasTargetLocalStabilizationDeathJob
            {
                Admission = scratch.Admission,
                ApplicationOutcomes = scratch.ApplicationOutcomes,
                MutationOutcomes = scratch.MutationOutcomes,
                Catalog = catalog,
                CoreFacts = scratch.CoreFacts,
                Execution = scratch.Execution,
            }.Schedule(dependency);
            dependency = new GasTargetPublishPreflightJob
            {
                Session = session,
                SimulationEpoch = simulationEpoch,
                Profile = profile,
                Admission = scratch.Admission,
                OwnerPlans = scratch.OwnerPlans,
                TargetShadows = scratch.TargetShadows,
                ShadowActiveEffects = scratch.TargetActiveEffects,
                ShadowAttributes = scratch.TargetAttributes,
                ShadowAttributeDirtyWords = scratch.TargetAttributeDirtyWords,
                ShadowTagCounts = scratch.TargetTagCounts,
                ShadowTagPresenceWords = scratch.TargetTagPresenceWords,
                ShadowPayloadRanges = scratch.TargetPayloadRanges,
                ShadowPayloadValues = scratch.TargetPayloadValues,
                ActiveEffectStride = scratch.TargetActiveEffectStride,
                AttributeStride = scratch.TargetAttributeStride,
                AttributeDirtyWordStride = scratch.TargetAttributeDirtyWordStride,
                TagStride = scratch.TargetTagStride,
                TagPresenceWordStride = scratch.TargetTagPresenceWordStride,
                PayloadRangeStride = scratch.TargetPayloadRangeStride,
                PayloadValueStride = scratch.TargetPayloadValueStride,
                AscLifecycles = state.GetComponentLookup<AscLifecycle>(true),
                AscIdentities = state.GetComponentLookup<GasAscIdentity>(true),
                SlabHeads = state.GetComponentLookup<AscSlabHeads>(true),
                PayloadStates = state.GetComponentLookup<GasPayloadRangeAllocatorState>(true),
                Registries = state.GetBufferLookup<AscRegistrySlot>(true),
                Grants = state.GetBufferLookup<GrantedAbilitySlot>(true),
                Activations = state.GetBufferLookup<AbilityActivationSlot>(true),
                Continuations = state.GetBufferLookup<AbilityContinuationSlot>(true),
                Subscriptions = state.GetBufferLookup<AbilitySubscriptionSlot>(true),
                ActiveEffects = state.GetBufferLookup<ActiveEffectSlot>(true),
                Attributes = state.GetBufferLookup<AttributeValueSlot>(true),
                AttributeDirtyWords = state.GetBufferLookup<AttributeDirtyWord>(true),
                TagCounts = state.GetBufferLookup<TagCountSlot>(true),
                TagPresenceWords = state.GetBufferLookup<TagPresenceWord>(true),
                PayloadRanges = state.GetBufferLookup<GasPayloadRangeRecord>(true),
                PayloadValues = state.GetBufferLookup<GasPayloadValueSlot>(true),
                Execution = scratch.Execution,
            }.Schedule(dependency);
            dependency = new GasStableFactMergeTerminalPrepareJob
            {
                Session = session,
                SimulationEpoch = simulationEpoch,
                Admission = scratch.Admission,
                Registries = state.GetBufferLookup<AscRegistrySlot>(true),
                AscIdentities = state.GetComponentLookup<GasAscIdentity>(true),
                Memberships = state.GetComponentLookup<AscBattleMembership>(true),
                TargetShadows = scratch.TargetShadows,
                SessionLifecycles = state.GetComponentLookup<GasSessionLifecycle>(true),
                Battles = state.GetBufferLookup<BattleInstanceSlot>(true),
                FaultInjections = state.GetComponentLookup<GasFinalPublishFaultInjection>(true),
                BattleIntents = scratch.TerminalBattleIntents,
                SessionLifecycleIntent = scratch.SessionLifecycleIntent,
                CoreFacts = scratch.CoreFacts,
                Execution = scratch.Execution,
            }.Schedule(dependency);
            dependency = new GasNextTickRoutePrepareJob
            {
                Session = session,
                SimulationEpoch = simulationEpoch,
                Profile = profile,
                Admission = scratch.Admission,
                Registries = state.GetBufferLookup<AscRegistrySlot>(true),
                AscIdentities = state.GetComponentLookup<GasAscIdentity>(true),
                TargetShadows = scratch.TargetShadows,
                SlabHeads = state.GetComponentLookup<AscSlabHeads>(true),
                PendingCommands = state.GetBufferLookup<PendingCommand>(true),
                FaultInjections = state.GetComponentLookup<GasFinalPublishFaultInjection>(true),
                SlotIntents = scratch.PendingCommandSlotIntents,
                HeadIntents = scratch.PendingCommandHeadIntents,
                AbilityRoutes = scratch.AbilityRoutes,
                Execution = scratch.Execution,
            }.Schedule(dependency);
            dependency = new GasBoundaryProjectPrepareJob
            {
                Session = session,
                SimulationEpoch = simulationEpoch,
                CoreFacts = scratch.CoreFacts,
                Admission = scratch.Admission,
                Registries = state.GetBufferLookup<AscRegistrySlot>(true),
                AscIdentities = state.GetComponentLookup<GasAscIdentity>(true),
                TargetShadows = scratch.TargetShadows,
                Memberships = state.GetComponentLookup<AscBattleMembership>(true),
                Battles = state.GetBufferLookup<BattleInstanceSlot>(true),
                Drains = state.GetComponentLookup<BoundaryDrainState>(true),
                BoundaryFacts = state.GetBufferLookup<BoundaryFactBuffer>(true),
                FaultInjections = state.GetComponentLookup<GasFinalPublishFaultInjection>(true),
                BoundaryIntents = scratch.BoundaryFactIntents,
                Execution = scratch.Execution,
            }.Schedule(dependency);
            dependency = new GasRequestTerminalPrepareJob
            {
                Admission = scratch.Admission,
                SealedCommands = scratch.SealedCommands,
                OwnerPlans = scratch.OwnerPlans,
                ApplicationOutcomes = scratch.ApplicationOutcomes,
                Intents = scratch.RequestTerminalIntents,
                Execution = scratch.Execution,
            }.Schedule(dependency);
            dependency = new GasSessionFinalPublishFaultReduceJob
            {
                Admission = scratch.Admission,
                Execution = scratch.Execution,
                Decision = scratch.FinalPublishDecision,
            }.Schedule(dependency);
            dependency = ScheduleFaultLatch(
                ref state,
                session,
                simulationEpoch,
                in scratch,
                dependency,
                includePostAdmissionFailure: 1);
            dependency = new GasAscTargetPublishJob
            {
                Decision = scratch.FinalPublishDecision,
                TargetShadows = scratch.TargetShadows,
                ShadowActiveEffects = scratch.TargetActiveEffects,
                ShadowAttributes = scratch.TargetAttributes,
                ShadowAttributeDirtyWords = scratch.TargetAttributeDirtyWords,
                ShadowTagCounts = scratch.TargetTagCounts,
                ShadowTagPresenceWords = scratch.TargetTagPresenceWords,
                ShadowPayloadRanges = scratch.TargetPayloadRanges,
                ShadowPayloadValues = scratch.TargetPayloadValues,
                ActiveEffectStride = scratch.TargetActiveEffectStride,
                AttributeStride = scratch.TargetAttributeStride,
                AttributeDirtyWordStride = scratch.TargetAttributeDirtyWordStride,
                TagStride = scratch.TargetTagStride,
                TagPresenceWordStride = scratch.TargetTagPresenceWordStride,
                PayloadRangeStride = scratch.TargetPayloadRangeStride,
                PayloadValueStride = scratch.TargetPayloadValueStride,
                AscLifecycles = state.GetComponentLookup<AscLifecycle>(),
                SlabHeads = state.GetComponentLookup<AscSlabHeads>(),
                PayloadStates = state.GetComponentLookup<GasPayloadRangeAllocatorState>(),
                ActiveEffects = state.GetBufferLookup<ActiveEffectSlot>(),
                Attributes = state.GetBufferLookup<AttributeValueSlot>(),
                AttributeDirtyWords = state.GetBufferLookup<AttributeDirtyWord>(),
                TagCounts = state.GetBufferLookup<TagCountSlot>(),
                TagPresenceWords = state.GetBufferLookup<TagPresenceWord>(),
                PayloadRanges = state.GetBufferLookup<GasPayloadRangeRecord>(),
                PayloadValues = state.GetBufferLookup<GasPayloadValueSlot>(),
            }.Schedule(dependency);
            dependency = new GasAbilityLifecyclePublishJob
            {
                Session = session,
                SimulationEpoch = simulationEpoch,
                Decision = scratch.FinalPublishDecision,
                OwnerPlans = scratch.OwnerPlans,
                Execution = scratch.Execution,
                Registries = state.GetBufferLookup<AscRegistrySlot>(true),
                AscIdentities = state.GetComponentLookup<GasAscIdentity>(true),
                AscLifecycles = state.GetComponentLookup<AscLifecycle>(true),
                SlabHeads = state.GetComponentLookup<AscSlabHeads>(),
                Grants = state.GetBufferLookup<GrantedAbilitySlot>(),
                Activations = state.GetBufferLookup<AbilityActivationSlot>(),
                Continuations = state.GetBufferLookup<AbilityContinuationSlot>(true),
                Subscriptions = state.GetBufferLookup<AbilitySubscriptionSlot>(true),
            }.Schedule(dependency);
            dependency = new GasSessionFinalPublishFaultReduceJob
            {
                Admission = scratch.Admission,
                Execution = scratch.Execution,
                Decision = scratch.FinalPublishDecision,
            }.Schedule(dependency);
            dependency = ScheduleFaultLatch(
                ref state,
                session,
                simulationEpoch,
                in scratch,
                dependency,
                includePostAdmissionFailure: 1);
            dependency = new GasTerminalPublishJob
            {
                Session = session,
                Decision = scratch.FinalPublishDecision,
                BattleIntents = scratch.TerminalBattleIntents,
                SessionLifecycleIntent = scratch.SessionLifecycleIntent,
                Battles = state.GetBufferLookup<BattleInstanceSlot>(),
                SessionLifecycles = state.GetComponentLookup<GasSessionLifecycle>(),
            }.Schedule(dependency);
            dependency = new GasNextTickRoutePublishJob
            {
                Decision = scratch.FinalPublishDecision,
                SlotIntents = scratch.PendingCommandSlotIntents,
                HeadIntents = scratch.PendingCommandHeadIntents,
                SlabHeads = state.GetComponentLookup<AscSlabHeads>(),
                PendingCommands = state.GetBufferLookup<PendingCommand>(),
            }.Schedule(dependency);
            dependency = new GasBoundaryPublishJob
            {
                Decision = scratch.FinalPublishDecision,
                BoundaryIntents = scratch.BoundaryFactIntents,
                Drains = state.GetComponentLookup<BoundaryDrainState>(),
                BoundaryFacts = state.GetBufferLookup<BoundaryFactBuffer>(),
                Execution = scratch.Execution,
            }.Schedule(dependency);
            dependency = new GasRequestTerminalPublishJob
            {
                Session = session,
                Decision = scratch.FinalPublishDecision,
                Intents = scratch.RequestTerminalIntents,
                Execution = scratch.Execution,
                TerminalBuffers = state.GetBufferLookup<GasRequestTerminalIntent>(),
            }.Schedule(dependency);
            return new GasRecordEndFixedJob
                { Admission = scratch.Admission, Execution = scratch.Execution }.Schedule(dependency);
        }

        /// <summary>
        /// 在完整 DAG 尾部仅按 AdmissionResult 推进成功 Tick，并发布 lane 诊断与 consume 控制状态。
        /// </summary>
        private static JobHandle ScheduleFinalize(
            ref SystemState state,
            Entity session,
            in GasTickScratch scratch,
            JobHandle dependency)
        {
            return new GasTickFinalizeJob
            {
                Session = session,
                Admission = scratch.Admission,
                Execution = scratch.Execution,
                Ticks = state.GetComponentLookup<SimulationTickState>(),
                Lifecycles = state.GetComponentLookup<GasSessionLifecycle>(),
                Diagnostics = state.GetComponentLookup<GasTickDiagnostics>(),
                Inboxes = state.GetBufferLookup<BoundaryCommandInbox>(),
            }.Schedule(dependency);
        }
    }
}
